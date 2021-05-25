using Common;
using FDA;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
//using System.Windows.Forms;
using uPLibrary.Networking.M2Mqtt;

namespace FDAApp
{
    static class Program
    {
        static DataAcqManager _dataAquisitionManager;
        static TCPServer _FDAControlServer;


        static public bool InitSuccess { get; }

        static private Guid ExecutionID;
        static private string DBType;
        static private System.Threading.Timer upTimeReporterTmr;
        static private System.Threading.Timer MqttRetryTimer;
        static private bool FDAIsElevated = false;
        static private bool FDAElevationMessagePosted = false;
        static private bool ShutdownComplete = false;
        static IConfiguration configuration;
        //private string FDAidentifier = "";

        static private class AppSettings
        {
            static string FDAID { get; set; }
            static string SQLServerInstance { get; set; }
        }
        
        /* not .NET Core compatible */
        static private class NativeMethods
        {
            public const int SW_HIDE = 0;
            public const int SW_SHOW = 5;
            public const int MF_BYCOMMAND = 0x00000000;
            public const int SC_CLOSE = 0xF060;
            public const int QuickEditMode = 64;
            public const int ExtendedFlags = 128;
            public const int STD_INPUT_HANDLE = -10;
            [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
            [DllImport("user32.dll")] public static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);
            [DllImport("user32.dll")] public static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
            //[DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleMode(IntPtr hConsoleHandle, int ioMode);
            [DllImport("Kernel32.dll", SetLastError = true)] public static extern IntPtr GetStdHandle(int nStdHandle);
        }
        

              

        //static ConsoleEventDelegate handler;   // Keeps it from getting garbage collected
       // private delegate bool ConsoleEventDelegate(int eventType);


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Globals.ConsoleMode = args.Contains("-console");

            string message = "Starting the FDA in background mode";
            if (Globals.ConsoleMode)
                message = "Starting the FDA in console mode";
            Console.WriteLine(message);

            Console.WriteLine("Starting the basic services control port server");
            _FDAControlServer = TCPServer.NewTCPServer(9572);
            if (_FDAControlServer != null)
            {
                _FDAControlServer.DataAvailable += TCPServer_DataAvailable;
                _FDAControlServer.ClientDisconnected += TCPServer_ClientDisconnected;
                _FDAControlServer.ClientConnected += TCPServer_ClientConnected;
                _FDAControlServer.Start();
            }
            else
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), TCPServer.LastError, "Error occurred while initializing the Basic Services Control Port Server");
            }

            Console.WriteLine("Starting the operational messages streaming server");
            OperationalMessageServer.Start();

            Console.WriteLine("Waiting five seconds to allow clients to connect to the BSCP or OMSP ports");

            Thread.Sleep(5000);

            // hiding the console, disabling the x button, disabling quick edit mode are windows only functions
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // hide the console window if the -show command line option is not specified
                if (!args.Contains("-console"))
                {
                    var handle = NativeMethods.GetConsoleWindow();
                    NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE);
                }
                else
                {
                    // the console window is shown, we need to change some settings

                    IntPtr conHandle = NativeMethods.GetConsoleWindow();

                    //disable the window close button(X)
                    NativeMethods.DeleteMenu(NativeMethods.GetSystemMenu(conHandle, false), NativeMethods.SC_CLOSE, NativeMethods.MF_BYCOMMAND);

                    // disable quick edit mode (this causes the app to pause when the user clicks in the console window)             
                    IntPtr stdHandle = NativeMethods.GetStdHandle(NativeMethods.STD_INPUT_HANDLE);
                    if (!NativeMethods.GetConsoleMode(stdHandle, out int mode))
                    {
                        // error getting the console mode
                        Console.WriteLine("Error retrieving console mode");
                        OperationalMessageServer.WriteLine("Error retrieving the console mode");
                    }
                    mode &= ~(NativeMethods.QuickEditMode | NativeMethods.ExtendedFlags);
                    if (!NativeMethods.SetConsoleMode(stdHandle, mode))
                    {
                        // error setting console mode.
                        Console.WriteLine("Error setting console mode");
                        OperationalMessageServer.WriteLine("Error while settting the console mode");
                    }
                }
            }


          

      
            // check if an instance is already running
            Process[] proc = Process.GetProcessesByName("FDACore");
            if (proc.Length > 1)
            {
                Console.WriteLine("An existing FDA instance was detected, closing");
                OperationalMessageServer.WriteLine("An existing FDA instance was detected, closing");
                return;
            }


            /* not .NET Code compatible
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
            }
            */




  

            // initialize and start data acquisition
            try // general error catching
            {
                IConfigurationSection appConfig = null;
                try
                {
                    configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
                    appConfig = configuration.GetSection(nameof(AppSettings));
                } catch (Exception ex)
                {
                    OperationalMessageServer.WriteLine("Error while reading appsettings.json \"" + ex.Message + "\"\nFDA Exiting");
                    Console.WriteLine("Error while reading appsettings.json \"" + ex.Message + "\"");
                    Console.WriteLine("FDA Exiting");
                    Console.Read();
                    return;

                }

                FDAIsElevated = false;
                FDAIsElevated = MQTTUtils.ThisProcessIsAdmin();

                ExecutionID = Guid.NewGuid();
 
                string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                Console.Title = "FDA version " + versionInfo.FileVersion;
                Globals.FDAVersion = versionInfo.FileVersion;

                // start the FDASystemManager

                
                string FDAID = appConfig["FDAID"];
                string DBInstance = appConfig["DatabaseInstance"]; 
                DBType = appConfig["DatabaseType"];
                string DBName = appConfig["SystemDBName"]; 
                string userName = appConfig["SystemLogin"]; 
                string userPass = appConfig["SystemDBPass"]; 

                if (DBInstance.Contains("(default"))
                {
                    DBInstance = DBInstance.Replace("(default)", "");
                    LogEvent("DatabaseInstance setting not found, using the default value");
                }

                if (DBName.Contains("(default"))
                {
                    DBName = DBName.Replace("(default)", "");
                    LogEvent("SystemDBName setting not found, using the default value");
                }

                if (userName.Contains("(default"))
                {
                    userName = userName.Replace("(default)", "");
                    LogEvent("SystemLogin setting not found, using the default value");
                }

                if (userPass.Contains("(default"))
                {
                    userPass = userPass.Replace("(default)", "");
                    LogEvent("SystemLogin setting not found, using the default value ");
                }


                if (FDAID.Contains("(default"))
                {
                    FDAID = FDAID.Replace("(default)", "");
                    LogEvent("FDAID setting not found, using the default");
                }

                //this.ThreadExit += FDAContext_ThreadExit;


                FDASystemManager systemManager;
                switch (DBType.ToUpper())
                {
                    case "POSTGRESQL": systemManager = new FDASystemManagerPG(DBInstance, DBName, userName, userPass, versionInfo.FileVersion, ExecutionID); break;
                    case "SQLSERVER": systemManager = new FDASystemManagerSQL(DBInstance, DBName, userName, userPass, versionInfo.FileVersion, ExecutionID); break;
                    default:
                        LogEvent("Unrecognized database server type '" + DBType + "'. Should be 'POSTGRESQL' or 'SQLSERVER'");
                        return;
                }



                Dictionary<string, FDAConfig> options = systemManager.GetAppConfig();
                //if (options.ContainsKey("FDAIdentifier"))
                //    FDAidentifier = options["FDAIdentifier"].OptionValue;

                systemManager.LogStartup(ExecutionID, Globals.FDANow(), versionInfo.FileVersion);

                

                Globals.ExecutionTime = Globals.FDANow();

                //elevatedClients = new List<Guid>();


                // set the "detailed messaging" flag
                if (options.ContainsKey("DetailedMessaging"))
                    Globals.DetailedMessaging = (systemManager.GetAppConfig()["DetailedMessaging"].OptionValue == "on");

                // connect to the MQTT broker
                Globals.MQTTEnabled = false;
                if (options.ContainsKey("MQTTEnabled"))
                {
                    Globals.MQTTEnabled = (options["MQTTEnabled"].OptionValue == "1");
                }
                
                if (Globals.MQTTEnabled)
                { 
                    MQTTConnect("localhost");
                    Globals.MQTT?.Publish("FDA/DBType", Encoding.UTF8.GetBytes(DBType.ToUpper()), 0, true);
                }
                else
                {
                    Console.WriteLine("MQTT is disabled in AppConfig table, skipping MQTT connection");
                }

                Globals.FDAStatus = Globals.AppState.Starting;

                // create a DBManager of the selected type
                string FDADBConnString = systemManager.GetAppDBConnectionString();
                DBManager dbManager;
                switch (DBType.ToUpper())
                {
                    case "SQLSERVER": dbManager = new DBManagerSQL(FDADBConnString); break;
                    case "POSTGRESQL": dbManager = new DBManagerPG(FDADBConnString); break;
                    default:
                        Globals.SystemManager.LogApplicationEvent("FDA App", "", "unrecognized database server type '" + DBType + "', unable to continue");
                        return;
                }

                

                // start the DataAcqManager             
                _dataAquisitionManager = new DataAcqManager(FDAID, dbManager, ExecutionID);

                // watch for changes to the MQTTEnabled option
                _dataAquisitionManager.MQTTEnableStatusChanged += DataAquisitionManager_MQTTEnableStatusChanged;

                if (_dataAquisitionManager.TestDBConnection())
                {
                    _dataAquisitionManager.Start();
                    Globals.FDAStatus = Globals.AppState.Normal;
                }
                else
                {
                    Console.WriteLine("Failed to connect to database, exiting");
                    return;
                }
            }
            catch (Exception ex)
            {

                if (Globals.SystemManager != null)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "FDA: error in application initilization");
                }
                else
                    Console.WriteLine("FDA: error in application initilization: " + ex.Message + " Stacktrace: " + ex.StackTrace);

                return;
            }

            while (!ShutdownComplete)
            {
                Thread.Sleep(1000);
            }
        }

        private static void DataAquisitionManager_MQTTEnableStatusChanged(object sender, BoolEventArgs e)
        {
            bool mqttEnabled = e.Value;

            if (mqttEnabled == Globals.MQTTEnabled) return;



            Globals.MQTTEnabled = mqttEnabled;

            if (mqttEnabled)
            {
                Globals.SystemManager.LogApplicationEvent(null,"","Enabling MQTT");
                MQTTConnect("localhost");
                Globals.SystemManager.LogApplicationEvent(null, "", "Starting Remote Query Manager");

                ((DBManager)Globals.DBManager).StartRemoteQueryManager();
                
            }
            else
            {
                Globals.SystemManager.LogApplicationEvent(null, "", "Disabling Remote Query Manager");
                ((DBManager)Globals.DBManager).StopRemoteQueryManager();

 
                if (Globals.MQTT != null)
                    if (Globals.MQTT.IsConnected)
                        Globals.MQTT.Disconnect(); 
                Globals.MQTT = null;
            }
        }

        private static void TCPServer_ClientDisconnected(object sender, TCPServer.ClientEventArgs e)
        {
            LogEvent("Client disconnected");
        }

        private static void TCPServer_ClientConnected(object sender, TCPServer.ClientEventArgs e)
        {        
            LogEvent("TCP client connected on port " + _FDAControlServer.Port);
        }

        static private void MQTTConnect(object o)
        {
            bool success = false;
            try
            {
                if (Globals.MQTT == null)
                {
                    Globals.MQTT = new MqttClient(o.ToString());
                }
  
                IConfigurationSection appConfig = configuration.GetSection(nameof(AppSettings));
                string pass = Common.Encrypt.DecryptString(appConfig["MQTT"], "KrdXI6HhS3B8C0CulLtB");

                if (Globals.MQTT.IsConnected)
                    Globals.MQTT.Disconnect();

                Globals.MQTT.Connect("FDA" + ExecutionID.ToString(), "FDA", pass);
                if (Globals.MQTT.IsConnected)
                {
                    success = true;
                }
            }
            catch
            {
                success = false;
            }

            if (success)
            {
                HandleMQTTConnected();  
            }
            else
            {
                Globals.SystemManager.LogApplicationEvent(null, "", "Failed to connect to MQTT broker. Will attempt to reconnect every 30 seconds until a connection is established", false, false);
                // check if the Mosquitto service is not started, try to start it if it isn't running
                RepairMosquittoService();
                
                // start a 5 second timer for re-attempting the connection
                if (MqttRetryTimer == null)
                    MqttRetryTimer = new System.Threading.Timer(MQTTConnect, o, 30000, 30000);              
            }
        }

        private static void HandleMQTTConnected()
        {
            // cancel the retry timer (if it's running)
            if (MqttRetryTimer != null)
            {
                MqttRetryTimer.Change(0, 0);
                MqttRetryTimer.Dispose();
                MqttRetryTimer = null;
            }

            Globals.MQTT.ConnectionClosed += MQTT_ConnectionClosed;
            Globals.MQTT.MqttMsgPublishReceived += MQTT_MqttMsgPublishReceived;
            Globals.SystemManager.LogApplicationEvent(null, "", "Connected to MQTT broker");

            PublishFDAInfo();

            // subscribe to FDAManager commands
            Globals.MQTT.Subscribe(new string[] { "FDAManager/command" }, new byte[] { 0 });

            // subscribe to changes to MQTTenabled status of connections and tags
            Globals.MQTT.Subscribe(new string[] { "connection/+/setmqttenabled", "tag/+/setmqttenabled" }, new byte[] { 0, 0 });

            // start uptime reporting
            upTimeReporterTmr = new System.Threading.Timer(ReportUptime, null, 0, 60000);

            // publish the current connection list
            if (_dataAquisitionManager != null)
                _dataAquisitionManager.PublishUpdatedConnectionsList();


            PublishSubscribables();
        }

        /// <summary>
        /// publish any Subscribeable Objects that have MQTT enabled
        /// </summary>
        private static void PublishSubscribables()
        {
             if (_dataAquisitionManager != null)
            {
                if (DataAcqManager._connectionsDictionary != null)
                {
                    foreach (SubscriptionManager.SubscribeableObject connection in DataAcqManager._connectionsDictionary.Values)
                    {
                        if (connection.MQTTEnabled)
                            connection.PublishAll();  // PublishAll checks if the MQTT enabled flag is set
                    }
                }
            }

            if (Globals.DBManager != null)
            {
                Dictionary<Guid, FDADataPointDefinitionStructure> tags = ((DBManager)Globals.DBManager).GetAllTagDefs();
                if (tags != null)
                {
                    foreach (SubscriptionManager.SubscribeableObject tag in tags.Values)
                    {
                        if (tag.MQTTEnabled)
                            tag.PublishAll();
                    }
                }
            }
        }

        private static void PublishFDAInfo()
        {
            // publish FDA version number (with retain)
            Globals.MQTT.Publish("FDA/version", Encoding.UTF8.GetBytes(Globals.FDAVersion), 0, true);

            // publish the FDA execution ID (with retain)
            Globals.MQTT.Publish("FDA/executionid", Encoding.UTF8.GetBytes(ExecutionID.ToString()), 0, true);

            // publish the DB connection string (with retain)
            //Globals.MQTT.Publish("FDA/dbconnstring", Encoding.UTF8.GetBytes(Globals.SystemManager.GetAppDBConnectionString()), 0, true);

            // publish the run status
            Globals.MQTT.Publish("FDA/runstatus", new byte[] { (byte)Globals.AppState.Normal }, 0, true);

            // publish the FDA identifier
            //Globals.MQTT.Publish("FDA/identifier", Encoding.UTF8.GetBytes(FDAidentifier), 0, true);

            // publish the DB type;
            Globals.MQTT?.Publish("FDA/DBType", Encoding.UTF8.GetBytes(DBType.ToUpper()), 0, true);

        }
        static internal void MQTT_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            string[] topic = e.Topic.Split('/');
            if (topic.Length < 2)
                return;

             /* these commands don't come through MQTT anymore
            if (topic[0].ToUpper() == "FDAMANAGER")
            {

                if (topic[1].ToUpper() == "COMMAND")
                {
                    string command = Encoding.UTF8.GetString(e.Message).ToUpper();
                    switch (command)
                    {
                        // these commands will come over TCP from the FDAController service now
                        //case "SHUTDOWN":
                        //    DoShutdown();
                        //    break;
                        //case "PAUSE":
                        //    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Pause command received from FDA Manager", false, true);
                        //    Console2.Flush();
                        //    Globals.FDAStatus = Globals.AppState.Pausing;
                        //    break;
                        //case "RESUME":
                        //    Globals.FDAStatus = Globals.AppState.Normal;
                        //    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Resume command received from FDA Manager", false, true);
                        //    break;
                    }
                }
            }
            */

            if (topic.Length < 3)
                return;

            if (topic[2].ToUpper() == "SETMQTTENABLED")
            {
                bool validGuid = Guid.TryParse(topic[1], out Guid objectID);
                if (!validGuid)
                    return;
                bool enabled = (e.Message[0] == 1);
                switch (topic[0].ToUpper())
                {
                    case "CONNECTION":
                        ConnectionManager conn = _dataAquisitionManager.GetDataConnection(objectID);
                        if (conn != null)
                            conn.MQTTEnabled = enabled;
                        break;
                    case "TAG":
                        FDADataPointDefinitionStructure tag = ((DBManager)(Globals.DBManager)).GetTagDef(objectID);
                        if (tag != null)
                            tag.MQTTEnabled = enabled;
                        break;

                }
            }
        }

        private static void TCPServer_DataAvailable(object sender, TCPServer.TCPCommandEventArgs e)
        {
            string receivedStr = Encoding.UTF8.GetString(e.Data);
            
            
            string command = receivedStr;

            // if the command is null terminated, remove the null so that the command is recognized in the switch statement below
            if (e.Data[e.Data.Length - 1] == 0)
            {
                command = Encoding.UTF8.GetString(e.Data, 0, e.Data.Length - 1);
            }

            switch (command.ToUpper())
            {
                case "PING":
                    _FDAControlServer.Send(e.ClientID, "OK");
                    break;
                case "SHUTDOWN":
                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Shutdown command received");
                    //            if (!PermissionCheck(e.ClientID,e.Host, parsedCommand[0]))
                    //            {
                    //                return;
                    //            }
                    _FDAControlServer.Send(e.ClientID, "OK");

                    // give the TCP server a second to send the response before starting the shutdown
                    Thread.Sleep(1000);

                    Globals.FDAStatus = Globals.AppState.ShuttingDown;
                    DoShutdown();
                    break;
                case "TOTALQUEUECOUNT":
                    //LogEvent("Getting queue counts");
                    int count = -1;
                    if (_dataAquisitionManager != null)
                        count = _dataAquisitionManager.GetTotalQueueCounts();
                    //LogEvent("Replying with the count (" + count.ToString() + ")");
                    _FDAControlServer.Send(e.ClientID, count.ToString());
                    break;
                case "RUNMODE":
                    if (Globals.ConsoleMode)
                        _FDAControlServer.Send(e.ClientID, "debug (console)");
                    else
                        _FDAControlServer.Send(e.ClientID, "background");
                    break;
                case "PAUSE":
                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Pause command received", false, true);
                    Console2.Flush();
                    Globals.FDAStatus = Globals.AppState.Pausing;
                    break;
                case "RESUME":
                    Globals.FDAStatus = Globals.AppState.Normal;
                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Resume command received", false, true);
                    break;

                //        case "ELEVATE":
                //            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Received request for elevated permissions from TCP client " + e.Host, false, true);
                //            string auth = parsedCommand[1];
                //            string decrypted = Common.Encrypt.DecryptString(auth, "KVvvc5thpcrsc5Hkpkof");
                //            double diff = double.MinValue;

                //            double OAtimestamp;
                //            if (!double.TryParse(decrypted, out OAtimestamp))
                //                goto Rejected;

                //            DateTime messageTimestamp = DateTime.FromOADate(OAtimestamp);
                //            DateTime currentTime = DateTime.UtcNow;

                //            // elevated permissions request is only good for 5 seconds, becomes invalid after that
                //            diff = currentTime.Subtract(messageTimestamp).TotalSeconds;
                //            if (diff > 5 || diff < 0)
                //                goto Rejected;

                //            if (!elevatedClients.Contains(e.ClientID))
                //            {
                //                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "TCP Client (" + e.Host + ") identified as FDAManager - elevated permissions granted", false, true);
                //                elevatedClients.Add(e.ClientID);
                //                _TCPServer.Send(e.ClientID, "ELEVATE:Success");
                //                return;
                //            }
                //            else
                //            {
                //                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "TCP Client (" + e.Host + ") already has elevated permissions", false, true);
                //                return;
                //            }


                //        Rejected:
                //            _TCPServer.Send(e.ClientID, "ELEVATE:Failed");
                //            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "TCP Client (" + e.Host + ") request for elevated permission rejected", false, true);
                //            break;
                //        case "PAUSE":
                //            if (!PermissionCheck(e.ClientID, e.Host, parsedCommand[0]))
                //                return;

                //            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Pause command received from FDA Manager (" + e.Host + ")", false, true);
                //            Console2.Flush();
                //            Globals.FDAStatus = Globals.AppState.Pausing;
                //            break;
                //        case "RESUME":
                //            if (!PermissionCheck(e.ClientID, e.Host, parsedCommand[0]))
                //                return;
                //            Globals.FDAStatus = Globals.AppState.Normal;
                //            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Resume command received from FDA Manager (" + e.Host + ")", false, true);
                //            break;
                //        /*               
                //        case "CONNOVERVIEW":
                //            if (!PermissionCheck(e.ClientID, e.Host, parsedCommand[0]))
                //                return;


                //            Dictionary<Guid, ConnectionManager> connList = DataAcqManager._connectionsDictionary;
                //            sb = new StringBuilder("CONNOVERVIEW:");
                //            ushort[] Qcounts;
                //            if (DataAcqManager._connectionsDictionary != null)
                //            {
                //                foreach (KeyValuePair<Guid, ConnectionManager> kvp in DataAcqManager._connectionsDictionary)
                //                {
                //                    sb.Append(kvp.Value.ConnectionStatus.ToString());
                //                    sb.Append(".");
                //                    sb.Append(kvp.Value.Description);
                //                    sb.Append(".");
                //                    sb.Append(kvp.Key.ToString());
                //                    sb.Append(".");
                //                    sb.Append(kvp.Value.ConnectionEnabled ? 1 : 0);
                //                    sb.Append(".");
                //                    sb.Append(kvp.Value.CommunicationsEnabled ? 1 : 0);
                //                    sb.Append(".");
                //                    Qcounts = kvp.Value._queueManager.GetQueueCounts();
                //                    foreach (ushort count in Qcounts)
                //                    {
                //                        sb.Append(count);
                //                        sb.Append(".");
                //                    }
                //                    //remove the last .
                //                    if (sb.Length > 0)
                //                        sb.Remove(sb.Length - 1, 1);
                //                    sb.Append("|");
                //                }
                //            }
                //            //remove the last |
                //            if (sb.Length > 0)
                //                sb.Remove(sb.Length - 1, 1);

                //            //send the response to the client
                //            if (_TCPServer.Send(e.ClientID, sb.ToString()))
                //                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Responded to TCP Client: " + sb.ToString(), false, true);
                //            else
                //                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Attempted to respond to TCP Client, but the attempt failed", false, true);
                //            break;

                //        case "QUEUECOUNTS":
                //            if (!PermissionCheck(e.ClientID, e.Host, parsedCommand[0]))
                //                return;
                //            if (parsedCommand.Length < 2)
                //            {
                //                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Received invalid command from FDA Manager, QueueCounts command must specify a connection", false, true);
                //                return;
                //            }

                //            connectionIDstr = parsedCommand[1];
                //            connectionID = Guid.Parse(connectionIDstr);
                //            if (DataAcqManager._connectionsDictionary.ContainsKey(connectionID))
                //            {
                //                ushort[] counts = DataAcqManager._connectionsDictionary[connectionID]._queueManager.GetQueueCounts();
                //                sb = new StringBuilder();
                //                for (int i = 0; i < counts.Length; i++)
                //                {
                //                    sb.Append(i);
                //                    sb.Append(":");
                //                    sb.Append(counts[i]);
                //                    sb.Append("|");
                //                }
                //                if (sb.Length > 0)
                //                    sb.Remove(sb.Length - 1, 1);
                //                _TCPServer.Send(e.ClientID, sb.ToString());
                //            }
                //            break;
                //         */


                default:
                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Unrecognized command received over TCP '" + receivedStr + "'");
                    _FDAControlServer.Send(e.ClientID, "Unrecognized command '" + receivedStr + "'");
                    break;

            }
        }

        static void DoShutdown()
        {
            Globals.FDAStatus = Globals.AppState.ShuttingDown;

            // stop listening for MQTT publications while shutting down
            if (Globals.MQTT != null)
            {
                Globals.MQTT.ConnectionClosed -= MQTT_ConnectionClosed;
                Globals.MQTT.MqttMsgPublishReceived -= MQTT_MqttMsgPublishReceived;
            }



            // stop listening for changes to the DB while shutting down
            //((DBManager)Globals.DBManager).PauseChangeMonitoring();

            // stop updating the uptime
            if (upTimeReporterTmr != null)
            {
                upTimeReporterTmr.Change(0, 0);
                upTimeReporterTmr.Dispose();
            }



            // stop the TCP Server
            _FDAControlServer.Dispose();
            _FDAControlServer = null;

            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Shutting down the Data Acquisition Manager");
            _dataAquisitionManager?.Dispose();

            Globals.SystemManager?.LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Shutting down System Manager");
            Globals.SystemManager?.Dispose();



            // unpublish FDA topics,set run status to "stopped", and shut down the MQTT connection
            if (Globals.MQTTEnabled && Globals.MQTT != null)
            {
                if (Globals.MQTT.IsConnected)
                {
                    Globals.MQTT.Publish("FDA/version", new byte[0], 0, true);
                    Globals.MQTT.Publish("FDA/executionid", new byte[0], 0, true);
                    Globals.MQTT.Publish("FDA/dbconnstring", new byte[0], 0, true);
                    Globals.MQTT.Publish("FDA/uptime", new byte[0], 0, true);
                    Globals.MQTT.Publish("FDA/connectionlist", new byte[0], 0, true);
                    Globals.MQTT.Publish("FDA/runstatus", Encoding.UTF8.GetBytes("Stopped"), 0, true);
                    Globals.MQTT.Publish("FDA/DBType", new byte[0], 0, true);
                    Thread.Sleep(3000);
                    Globals.MQTT.Disconnect();
                    Globals.MQTT = null;
                }
            }


            ShutdownComplete = true;
            OperationalMessageServer.WriteLine("Goodbye");

           
            //Application.Exit();
        }

        static private void MQTT_ConnectionClosed(object sender, EventArgs e)
        {
            if (Globals.MQTTEnabled)
            {
                Globals.SystemManager.LogApplicationEvent(null, "", "Disconnected from MQTT broker. Will attempt to reconnect every 5 seconds until connection is re-established");


                if (MqttRetryTimer == null)
                    MqttRetryTimer = new System.Threading.Timer(MQTTConnect, null, 5000, 5000);
                else
                    MqttRetryTimer.Change(5000, 5000);
            }
            else
            {
                Globals.SystemManager.LogApplicationEvent(null, "", "Disconnected from MQTT broker");
                if (FDAIsElevated)
                {
                    MQTTUtils.StopMosquittoService();
                }
                else
                {
                    Globals.SystemManager.LogApplicationEvent(null, "", "Unable to stop MQTT broker service, FDA not run as Administrator");
                }
            }
        }


        private static void RepairMosquittoService()
        {
            try
            { 
                // check if FDA process was run with elevated permissions
                if (FDAIsElevated)
                { 
                    Globals.SystemManager.LogApplicationEvent(null, "", "Unable to connect to the MQTT broker, attempting to repair the service");
                    string result = MQTTUtils.RepairMQTT();
                    if (result == "")
                    {
                        Globals.SystemManager.LogApplicationEvent(null, "", "MQTT succesfully repaired");
                    }
                    else
                        Globals.SystemManager.LogApplicationEvent(null, "", "Unable to repair MQTT broker: " + result);
                    //ProcessStartInfo startInfo = new ProcessStartInfo();
                    //startInfo.FileName = "MQTTBrokerUtility.exe";
                    //Process.Start(startInfo);                
                }
                else
                {
                    if (!FDAElevationMessagePosted)
                    {
                        Globals.SystemManager.LogApplicationEvent(null, "", "Warning: Unable to connect to the MQTT Broker, and the FDA cannot repair the issue because it does not have administrator privileges, please run the FDA as Administrator/SuperUser");
                        FDAElevationMessagePosted = true;
                    }
                }                
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while attempting to repair the Mosquitto Broker service");
            }
        }
        

        static private void ReportUptime(object o)
        {
            if (Globals.MQTTEnabled)
            {
                TimeSpan uptime = Globals.FDANow().Subtract(Globals.ExecutionTime);
                Globals.MQTT?.Publish("FDA/uptime", BitConverter.GetBytes(uptime.Ticks), 0, true);
            }
        }



        static private void LogEvent(string message)
        {
            if (Globals.SystemManager != null)
                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA", "", message);
            else
                Console.WriteLine(Globals.FDANow().ToString() + ": " + message);

        }

    }

}
