using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Threading;
using FDAInterface.FDAApplication;
using System.Net.Sockets;
using System.Text;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using uPLibrary.Networking.M2Mqtt;
using FDAInterface.Properties;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace FDAInterface
{
    [XmlRoot("ConnectionsHistory")]
    public class ConnectionHistory
    {
        [XmlElement("LastConnection")]
        public Connection LastConnection { get; set; }
        [XmlArray("RecentConnections")]
        public List<Connection> RecentConnections { get; set; }

        public ConnectionHistory()
        {
            RecentConnections = new List<Connection>();
        }
    }

    public class Connection
    {
        [XmlAttribute("Host")]
        public string Host { get; set; }

        [XmlAttribute("Description")]
        public string Description { get; set; }

        public Connection(string host,string description)
        {
            Host = host;
            Description = description;
        }

        public Connection()
        {

        }

        public static bool operator ==(Connection lhs, Connection rhs)
        {
            if (lhs is null && rhs is null)
                return true;

            if (lhs is object && rhs is null)
                return false;


            return (lhs.Description == rhs.Description && lhs.Host == rhs.Host);
        }

        public static bool operator !=(Connection lhs, Connection rhs)
        {
            if (lhs is null && rhs is null)
                return false;

            if (lhs is object && rhs is null)
                return true;

            return !(lhs.Description == rhs.Description && lhs.Host == rhs.Host);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            Connection other = (Connection)obj;
            return (this.Description == other.Description && this.Host == other.Host);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    static class Program
    {


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (FDAManagerContext.ProcessRunning("FDAManager",true))
            {
                //MessageBox.Show("The FDA Manager is already running","FDA Manager", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FDAManagerContext(args));
        }
    }    



    internal class  FDAManagerContext : ApplicationContext
    {
        internal enum ConnectionStatus {Default,Disconnected,Connecting,Connected};

        // statuses 
        internal static ConnectionStatus _MQTTConnectionStatus = ConnectionStatus.Default;
        internal static ConnectionStatus _ControllerConnectionStatus = ConnectionStatus.Default;
        internal static string _CurrentFDA = "";
        internal static string _FDAName;
        internal static string _FDAStatus;
        internal static long _FDARuntime;

        internal static ConnectionStatus MQTTConnectionStatus { get { return _MQTTConnectionStatus; } set { _MQTTConnectionStatus = value; StatusUpdate?.Invoke(null, new StatusUpdateArgs("MQTT",value)); } }
        internal static ConnectionStatus ControllerConnectionStatus { get { return _ControllerConnectionStatus; } set { _ControllerConnectionStatus = value; StatusUpdate?.Invoke(null, new StatusUpdateArgs("Controller", value)); } }
        internal static string CurrentFDA { get { return _CurrentFDA; } set { _CurrentFDA = value; FDANameUpdate?.Invoke(null, _CurrentFDA); } }
        internal static string FDAName { get { return _FDAName; } set { _FDAName = value; } }
        internal static string FDARunStatus { get { return _FDAStatus; } set { _FDAStatus = value; } }
        internal static string _FDAVersion = "";
        internal static string _FDADBType = "";


        internal static FDAStatus Status;

        internal static ConnectionHistory ConnHistory;
        private static NotifyIcon notifyIcon;     
        private static bool _paused = false;
        private static readonly MenuItem pauseMenuItem;
        private static MenuItem openGuiMenuItem;
        private static frmMain2 _mainForm;
        // private System.Threading.Timer dataReceivedCheckTimer;
        //private int dataReceivedCheckRate = 100;
        internal static bool elevatedPermissions = false;
        internal static string Host;
        
     
        //private static int _FDAport = 9572;
        internal static MqttClient MQTT;
        internal static TcpClient FDAControllerClient;

        internal static BackgroundWorker bg_MQTTConnect;
        internal static BackgroundWorker bg_ControllerConnect;

        private static System.Threading.Timer MQTTReconnectTimer;
        private static System.Threading.Timer ControllerRetryTimer;
        private static System.Threading.Timer ControllerPinger;


        private static bool IntentionalDisconnect = false;

        private static TimeSpan ControllerPingRate = new TimeSpan(0, 0, 1);
        private static TimeSpan ControllerRetryRate = new TimeSpan(0, 0, 5);
        private static TimeSpan ReconnectRate = new TimeSpan(0,0,5); 

        public delegate void ConnectionStatusUpdateHandler(object sender, StatusUpdateArgs e);
        public static event ConnectionStatusUpdateHandler StatusUpdate;

        public delegate void FDANameUpdateHandler(object sender, string name);
        public static event FDANameUpdateHandler FDANameUpdate;

        public class StatusUpdateArgs : EventArgs
        {
            public string StatusName;
            public ConnectionStatus Status;

            public StatusUpdateArgs(string statusName,ConnectionStatus status)
            {
                StatusName = statusName;
                Status = status;
            }
        }


        public FDAManagerContext(string[] args)
        {
            //dataReceivedCheckTimer = new System.Threading.Timer(TCPDataReceivedCheck, null, Timeout.Infinite, Timeout.Infinite);
            for (int i = 0; i < args.Length; i++)
                args[i] = args[i].ToUpper();


            string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(exePath);

            MenuItem titleItem = new MenuItem("FDA Manager ver " + versionInfo.FileVersion);
            openGuiMenuItem = new MenuItem("Open FDA Tools", new EventHandler(OpenGui));
            MenuItem exitMenuItem = new MenuItem("Close FDA Manager", DoExit);

            notifyIcon = new NotifyIcon()
            {
                Icon = Properties.Resources.ManagerGray,
                ContextMenu = new ContextMenu(new MenuItem[] {titleItem,new MenuItem("-"),openGuiMenuItem,new MenuItem("-"), exitMenuItem }),
                Visible = true
            };

            bg_MQTTConnect = new BackgroundWorker();
            bg_MQTTConnect.DoWork += Bg_MQTTConnect_DoWork;
            bg_MQTTConnect.RunWorkerCompleted += Bg_MQTTConnect_RunWorkerCompleted;

            bg_ControllerConnect = new BackgroundWorker();
            bg_ControllerConnect.DoWork += Bg_ControllerConnect_DoWork;
            bg_ControllerConnect.RunWorkerCompleted += Bg_ControllerConnect_RunWorkerCompleted;

            MQTTReconnectTimer = new System.Threading.Timer(MQTTReconnectTimer_Tick);
            ControllerPinger = new System.Threading.Timer(PingController);
            ControllerRetryTimer = new System.Threading.Timer(ControllerReconnectTimer_Tick);

       


            // read in the history XML (or create an empy history object if the xml doesn't exist
            if (File.Exists("ConnectionHistory.xml"))
            {
                try
                {
                    using (FileStream stream = new FileStream("ConnectionHistory.xml", FileMode.Open))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(ConnectionHistory));
                        ConnHistory = (ConnectionHistory)serializer.Deserialize(stream);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    ConnHistory = new ConnectionHistory();
                }
            }
            else
                ConnHistory = new ConnectionHistory();
           


            OpenGui(this, new EventArgs());

            // get the details of the previous connection and try to reconnect to it            
            //string connDetails = Properties.Settings.Default.LastConnectedFDA;
            //string[] details = connDetails.Split('|');
            //if (details.Length >= 2 && details[0] != "")
            //{
            if (ConnHistory.LastConnection != null)
            {
               ChangeHost(ConnHistory.LastConnection.Host, ConnHistory.LastConnection.Description);
            }
            //}
           
        }

      

        internal static void ChangeHost(string newHost,string newFDAName)
        {
            if (bg_ControllerConnect.IsBusy || bg_MQTTConnect.IsBusy)
            {
                MessageBox.Show("Please wait for the current connection attempt to complete, and then try again");
                return;
            }

            if (newHost != Host)
            {
                FDAName = newFDAName;
                Host = newHost;
                CurrentFDA = newFDAName + " (" + newHost + ")";
                

                IntentionalDisconnect = false;
                MQTTReconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
                ControllerRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                ControllerPinger.Change(Timeout.Infinite, Timeout.Infinite);

               
              

                if (bg_MQTTConnect == null)
                {
                    bg_MQTTConnect = new BackgroundWorker();
                    bg_MQTTConnect.DoWork += Bg_MQTTConnect_DoWork;
                    bg_MQTTConnect.RunWorkerCompleted += Bg_MQTTConnect_RunWorkerCompleted;
                }

                if (bg_ControllerConnect == null)
                {
                    bg_ControllerConnect = new BackgroundWorker();
                    bg_ControllerConnect.DoWork += Bg_ControllerConnect_DoWork;
                    bg_ControllerConnect.RunWorkerCompleted += Bg_ControllerConnect_RunWorkerCompleted;
                }

                
                if (MainFormActive())
                {
                     //_mainForm.SetConnectionMenuItems(false);
                    _mainForm.SetFDAStatus(Status);
                }
                string[] args = new string[] { newHost, newFDAName,"False"};

                bg_MQTTConnect.RunWorkerAsync(args);
                bg_ControllerConnect.RunWorkerAsync(args);
            }
        }

        internal static void Disconnect()
        {
            IntentionalDisconnect = true;
            ControllerPinger.Change(Timeout.Infinite, Timeout.Infinite);

            if (MQTT != null)
            {
                if (MQTT.IsConnected)
                {
                    MQTT.Disconnect();
                    MQTTConnectionStatus = ConnectionStatus.Default;
                }
            }

            if (FDAControllerClient != null)
            {
                FDAControllerClient.Dispose();
                FDAControllerClient = null;
                ControllerConnectionStatus = ConnectionStatus.Default;
                
            }

            Host = "";
            FDAName = "";

  

            UpdateFDAStatusIcon("default");
        }

        private static void Bg_ControllerConnect_DoWork(object sender, DoWorkEventArgs e)
        {
            string[] args = (string[])e.Argument;
            string host = args[0];
            string name = args[1];

            ControllerConnectionStatus = ConnectionStatus.Connecting;
            
            bool isRetry = false;
        Retry:
            // kill existing client if present
            if (FDAControllerClient != null)
            {                
                FDAControllerClient.Dispose();
                FDAControllerClient = null;
            }

            // create a client and try to connect
            FDAControllerClient = new TcpClient();
            string message = "";
            bool success = true;

            try
            {
                FDAControllerClient.Connect(host, 9571);
            } catch (Exception ex)
            {
                message = ex.Message;
                success = false;
            }

            if (!success && !isRetry)
            {
                isRetry = true;
                goto Retry;
            }

            e.Result = new string[] { host,name,(success && FDAControllerClient.Connected).ToString(), message};
        }

        private static void Bg_ControllerConnect_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //if (PendingChangeHost != null)
            //{
            //    string host = PendingChangeHost[0];
            //    string name = PendingChangeHost[1];
            //    PendingChangeHost = null;

            //    ChangeHost(host,name);
            //}

            string[] results = (string[])e.Result;

            if (results[2] == "True") // successful connection
            {
                ControllerConnectionStatus = ConnectionStatus.Connected;
                ControllerPinger.Change(ControllerPingRate,ControllerPingRate);
                // ControllerRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else // not so successful connection
            {  
                ControllerConnectionStatus = ConnectionStatus.Disconnected;
                //ControllerRetryTimer.Change(ControllerRetryRate, ControllerRetryRate);
                ControllerPinger.Change(Timeout.Infinite, Timeout.Infinite);
                
                string message = "Failed to connect to FDAController on FDA server " + results[1] + " (" + results[0] + ")";
                if (results[3] != "")
                {
                    message += "\nThe error message was \"" + results[3] + "\"";
                }

                if (MessageBox.Show(message, "Failed Connection", MessageBoxButtons.RetryCancel) == DialogResult.Retry)
                    bg_ControllerConnect.RunWorkerAsync(new string[] { results[0], results[1] });
            }
        }

        private static void Bg_MQTTConnect_DoWork(object sender, DoWorkEventArgs e)
        {
            string[] args = (string[])e.Argument;
            string errorMsg = "";
            // arguments array too short, exit with an error
            if (args.Length < 2)
            {
                // results array is 4 elements long (0/1 are the host IP/Name, 2 is the result (can be success or failure), 3 is any messages (like errors)
                e.Result = new string[] { args.ToString(), "", "failure", "invalid argument passed to Bg_MQTTConnect_DoWork()" };
                return;
            }

            // arguments array too long, take the first 2 and dump the rest
            if (args.Length > 2)
            {
                args = new string[] { args[0], args[1]};
            }

            MQTTConnectionStatus = ConnectionStatus.Connecting;

            string MQTTServer_IP = args[0];
            string MQTTServer_Name = args[1];
           // bool suppressMessages = (args[2] == "True");


            bool isRetry = false;
        
            Retry:
            bool success = false;
            try
            {
                // no previous connection (MQTT is null)
                if (MQTT == null)
                {
                    MQTT = new MqttClient(MQTTServer_IP);
                }
                else
                {
                    // a previous connection exists, disconnect it (if it's connected) and dispose of it
                    if (MQTT.IsConnected)
                    {
                        MQTT.Disconnect();
                    }
                    MQTT = null;
                }

                string pass = Encryption.DecryptString(Properties.Settings.Default.MQTT, "MC9fSLAfwkrHCx0j6LG4");
                MQTT = new MqttClient(MQTTServer_IP);
                MQTT.Connect("FDAManager" + Guid.NewGuid().ToString(), "FDAManager", pass);
                if (MQTT.IsConnected)
                {
                    success = true;
                }
            }
            catch (Exception ex)
            {
                success = false;
                errorMsg = ex.Message;
            }

            if (!success && !isRetry)
            {
                isRetry = true;
                goto Retry;
            }

            if (success)
            {
                // results array is 4 elements long (0/1 are the host IP/Name, 2 is the result (can be success or failure), 3 is any messages (like errors)
                string[] results = new string[] { args[0], args[1], "success", "" };
                e.Result = results;
            }
            else
            {
                string[] results = new string[] { args[0], args[1], "failure", errorMsg };           
                e.Result = results;
            }

            return;
        }

        private static void Bg_MQTTConnect_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // results is a four element string array {IP,name,connection result,message}
            string[] results = (string[])e.Result;

            if (results[2]=="success")
            {
                ConnHistory.LastConnection = new Connection(Host, FDAName);


                MQTTConnectionStatus = ConnectionStatus.Connected;
                MQTT.MqttMsgPublishReceived += MQTT_MqttMsgPublishReceived;
                MQTT.ConnectionClosed += MQTT_ConnectionClosed;
                MQTT.Subscribe(new string[] { "FDAManager/command" }, new byte[] { 0 });

                // default to the 'FDA Status Unknown' red icon until we get a status update from the FDA
                notifyIcon.Icon = Properties.Resources.ManagerGray;

                //MQTT.Subscribe(new string[] { "FDA/runstatus" }, new byte[] { 0 });
                MQTT.Subscribe(new string[] { "FDA/identifier" }, new byte[] { 0 });


                if (MainFormActive())
                {
                    _mainForm.SetMQTT(MQTT);

                    // notify the GUI that the MQTT connection has been established
                    _mainForm.MQTTConnected(_FDAName, Host);
                }

                MQTTReconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                notifyIcon.Icon = Properties.Resources.ManagerGray;
                notifyIcon.Text = "FDA Status unknown - not connected";

                MQTTConnectionStatus = ConnectionStatus.Disconnected;

                string message = "Failed to connect to MQTT Broker on FDA server " + results[1] + " (" + results[0] + ")";
                if (results[3] != "")
                {
                    message += "\nThe error message was \"" + results[3] + "\"";
                }

                if (MessageBox.Show(message, "Failed Connection", MessageBoxButtons.RetryCancel) == DialogResult.Retry)
                    bg_MQTTConnect.RunWorkerAsync(new string[] { results[0], results[1] });
     
            }


        }
        private static void WaitForResponse(NetworkStream stream, int limit)
        {
            int wait = 0;
            while (!stream.DataAvailable && wait < limit)
            {
                Thread.Sleep(50);
                wait += 50;
            }
        }

        private static void PingController(object state)
        {
            ControllerPinger.Change(Timeout.Infinite, Timeout.Infinite);
            bool connected = true;

            if (FDAControllerClient == null)
            {
                connected = false;
            }
            else
                if (FDAControllerClient.Client == null)
            {
                connected = false;
            }
            else
            {
                if (!FDAControllerClient.Connected)
                {
                    connected = false;
                }
                else
                {
                    try
                    {
                        connected = !(FDAControllerClient.Client.Poll(1, SelectMode.SelectRead));
                    }
                    catch (SocketException)
                    {
                        connected = false;
                    }
                }
            }

            if (!connected)
            {
                ControllerConnectionStatus = ConnectionStatus.Disconnected;
                if (!bg_ControllerConnect.IsBusy)
                    bg_ControllerConnect.RunWorkerAsync(new string[] { Host, FDAName, "True" });
            }
            else
            {
                // controller is connected, ask it for the FDA status
                string statusJson = SendCommandToFDAController("FDASTATUS\0");
                Status = FDAStatus.Deserialize(statusJson);

                // and update the GUI, if it's open
                _mainForm?.SetFDAStatus(Status);
                
                //if (Status.RunStatus == "Running")
                //{
                //    _mainForm?.SetFDAStatus("Normal");
                    
                //    // get the FDA Run time
                //    string ticksString = SendCommandToFDAController("RUNTIME\0");
                //    long ticks;

                //    if (long.TryParse(ticksString, out ticks))
                //    {
                //        _FDARuntime = ticks;
                //        _mainForm?.SetRunTime(ticks);
                //    }

                //    //Thread.Sleep(1000);
                //    // and the DBType (if we don't already know it)
                //    if (_FDADBType == "")
                //    {
                //        _FDADBType = SendCommandToFDAController("DBTYPE\0");
                //        if (_FDADBType != "")
                //            _mainForm?.SetDBType(_FDADBType);
                //    }

                //    // and the version number (if we don't already know it)
                //    if (_FDAVersion == "")
                //    {
                //        _FDAVersion = SendCommandToFDAController("VERSION\0");
                //        if (_FDAVersion != "")
                //            _mainForm?.SetVersion(_FDAVersion);
                //    }

                  

                //}
                //else 
                //{
                //    _mainForm?.SetFDAStatus("Stopped");
                //    _FDARuntime = 0;
                //    _FDAVersion = "";
                //    _FDADBType = "";
                //}

                // reset the ping timer
                ControllerPinger.Change(ControllerPingRate, ControllerPingRate);
            }
        }

        private static void MQTT_ConnectionClosed(object sender, EventArgs e)
        {
            if (IntentionalDisconnect)
                MQTTConnectionStatus = ConnectionStatus.Default;
            else
                MQTTConnectionStatus = ConnectionStatus.Disconnected;

            //try to reconnect
            if (!IntentionalDisconnect && !bg_MQTTConnect.IsBusy)
            {
                bg_MQTTConnect.RunWorkerAsync(new string[] { Host, FDAName });
            }

        }

        private static void ControllerReconnectTimer_Tick(object state)
        {
            ControllerRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            if (!bg_ControllerConnect.IsBusy)
            {
                bg_ControllerConnect.RunWorkerAsync(new string[] { Host, FDAName, "True" });
            }
        }

        private static void MQTTReconnectTimer_Tick(object state)
        {
            MQTTReconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
            if (!bg_MQTTConnect.IsBusy)
            {
                bg_MQTTConnect.RunWorkerAsync(new string[] { Host, FDAName,"True"}); // host/ip, description, supress messages (don't show failed to connect messages on re-connect attempts)
            }
        }

     

   
        private static void MQTT_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            switch(e.Topic)
            {
                case "FDAManager/command":
                    //MessageBox.Show("Received command: " + Encoding.UTF8.GetString(e.Message));
                    string[] command = Encoding.UTF8.GetString(e.Message).Split(' ');
                    if (command.Length < 2)
                        return;
                    switch (command[0])
                    {
                        case "START": HandleStartCommand(command[1],false); break;
                        case "STARTCONSOLE": HandleStartCommand(command[1],true); break;
                    }
                    break;
                // not through MQTT anymore
                //case "FDA/runstatus":
                //    if (e.Message.Length > 0)
                //        UpdateFDAStatusIcon(Encoding.UTF8.GetString(e.Message));
                //    break;
                case "FDA/identifier":
                    _FDAName = Encoding.UTF8.GetString(e.Message);
                    break;
             
            }
        }

    

        private static bool MainFormActive()
        {
            bool result = false;
            if (_mainForm != null)
            {
                if (!(_mainForm.IsDisposed || _mainForm.Disposing))
                    result = true;
            }
            return result;
        }

        private static void UpdateFDAStatusIcon(string FDAstatus)
        {
            _FDAStatus = FDAstatus;
            switch (FDAstatus)
            {
                // starting
                case "Starting":
                    notifyIcon.Icon = Properties.Resources.ManagerYellow;
                    notifyIcon.Text = "FDA Starting";
                    break;
                // running
                case "Normal":
                    notifyIcon.Icon = Properties.Resources.ManagerGreen;

                    notifyIcon.Text = "FDA Running";
                    break;
                // Shutting down
                case "ShuttingDown":
                    notifyIcon.Icon = Properties.Resources.ManagerYellow;
                    notifyIcon.Text = "FDA Shutting Down";
                    break;
                // Pausing
                case "Pausing":
                    notifyIcon.Icon = Properties.Resources.ManagerYellow;
                    notifyIcon.Text = "FDA Pausing";
                    break;
                //paused
                case "Paused":
                    notifyIcon.Icon = Properties.Resources.ManagerYellow;
                    notifyIcon.Text = "FDA Paused";
                    break;
                //stopped
                case "Stopped":
                    notifyIcon.Icon = Properties.Resources.ManagerRed;
                    notifyIcon.Text = "FDA Stopped";
                    break;
                case "default":
                    notifyIcon.Icon = Properties.Resources.ManagerGray;
                    break;
            }

   
        }



        internal static void PauseFDA(object sender, EventArgs e)
        {
            if (!_paused)
            {
                if (MQTT != null)
                {
                    if (MQTT.IsConnected)
                    {
                        MQTT.Publish("FDAManager/command", Encoding.UTF8.GetBytes("PAUSE"));
                    }
                }
                _paused = true;
                pauseMenuItem.Text = "Resume FDA";
            }
            else
            {
                if (MQTT != null)
                {
                    if (MQTT.IsConnected)
                    {
                        MQTT.Publish("FDAManager/command", Encoding.UTF8.GetBytes("RESUME"));
                    }
                }
                pauseMenuItem.Text = "Pause FDA";
                _paused = false;                
            }
        }


        internal static string SendCommandToFDAController(string command)
        {
            try
            {
                if (FDAControllerClient != null)
                {
                    if (FDAControllerClient.Connected)
                    {
                        NetworkStream stream = FDAControllerClient.GetStream();
                        stream.ReadTimeout = 1000;
                        stream.WriteTimeout = 1000;
                        byte[] toSend = Encoding.UTF8.GetBytes(command);
                        stream.Write(toSend, 0, toSend.Length);

                        WaitForResponse(stream, 1000);

                        // read any response
                        byte[] response = new byte[1000];

                       
                        if (FDAControllerClient.GetStream().DataAvailable)
                        {
                            int readsize = FDAControllerClient.GetStream().Read(response, 0, 1000);
                            return Encoding.UTF8.GetString(response, 0, readsize);
                        }
                        else
                            return "";
                       
                    }
                    else
                    {
                        MessageBox.Show("Unable to send the command because the FDA controller service at " + Host + " is not connected");
                        return "";
                    }
                }
                else
                {
                    MessageBox.Show("Unable to send the command because the FDA controller service at " + Host + " is not connected");
                    return "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while attempting to send the command to the FDA controller service at " + Host + " : " + ex.Message);
            }

            return "";
        }



        private static void HandleStartCommand(string FDA_ID,bool consolemode)
        {
            string localFDAID = Settings.Default.LocalFDAIdentifier;
            //MessageBox.Show("local FDA = '" + localFDAID + "', requested FDA start = '" + FDA_ID);
            if (FDA_ID != localFDAID)
            {
                //MessageBox.Show("no match, bailing out");
                return;
            }
            //MessageBox.Show("ID match, handling the start command");
            if (!ProcessRunning("FDA"))
            {
                string FDAPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\FDA.exe";
                if (File.Exists(FDAPath))
                {
                    if (consolemode)
                        Process.Start(FDAPath, "-show");
                    else
                        Process.Start(FDAPath);

                    pauseMenuItem.Enabled = true;
                }
                else
                    MessageBox.Show("Start FDA Command received, but FDAManager couldn't start the FDA because the executable wasn't found at " + FDAPath,"FDAMananger");
            }
        }


        
        private void OpenGui(object sender, EventArgs e)
        {
            if (_mainForm == null)
            {
                _mainForm = new frmMain2();
                _mainForm.MQTT = MQTT;
                _mainForm.Disposed += MainForm_Disposed;
                _mainForm.Shown += _mainForm_Shown;

                
                // start checking for subscription updates from the FDA
                //dataReceivedCheckTimer.Change(dataReceivedCheckRate, Timeout.Infinite);


                _mainForm.Show();
                _mainForm.SetFDAStatus(Status);

                //_mainForm.SetVersion(_FDAVersion);
                //_mainForm.SetDBType(_FDADBType);
                //_mainForm.SetRunTime(_FDARuntime);

                //if (bg_MQTTConnect != null)
                //{
                //    _mainForm.SetConnectionMenuItems(!bg_MQTTConnect.IsBusy);
                //}

                
            }
            else
                _mainForm.Focus();
        }

        private void _mainForm_Shown(object sender, EventArgs e)
        {

            
            if (MQTTConnectionStatus == ConnectionStatus.Connected)
            {
                _mainForm.SetMQTT(MQTT);
                _mainForm.MQTTConnected(_FDAName, Host);
            }
            
            
            _mainForm.SetFDAStatus(Status);
           
        }

        internal static bool ProcessRunning(string processname, bool self = false)
        {
            Process[] proc = Process.GetProcessesByName(processname);
            return (self && proc.Length > 1) || (!self && proc.Length > 0);
        }

        private void MainForm_Disposed(object sender, EventArgs e)
        {
            _mainForm.Disposed -= MainForm_Disposed;
            _mainForm = null;
            //dataReceivedCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private static void DoExit(object sender, EventArgs e)
        {
            //notifyIcon.Visible = false;
            ControllerPinger.Change(Timeout.Infinite, Timeout.Infinite);
            ControllerRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            MQTTReconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            // shut down the connection to MQTT
            if (MQTT != null)
            {
                if (MQTT.IsConnected)
                {
                    IntentionalDisconnect = true;
                    MQTT.Disconnect();
                }
                MQTT = null;
            }

            // shut down the connection to the FDAController
            if (FDAControllerClient != null)
            {
                if (FDAControllerClient.Connected)
                    FDAControllerClient.Close();
                FDAControllerClient.Dispose();
                FDAControllerClient = null;
            }



            // save the recent FDA connection history
            try
            {
                using (FileStream stream = new FileStream("ConnectionHistory.xml", FileMode.Create))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(ConnectionHistory));
                    serializer.Serialize(stream, ConnHistory);
                }
            }
            catch
            {
               
            }
            
            Application.Exit();
        }

        public class FDAStatus
        {
            public TimeSpan UpTime { get; set; }
            public String RunStatus { get; set; }
            public String Version { get; set; }
            public String DB { get; set; }

            public String RunMode { get; set; }

            public int TotalQueueCount { get; set; }
            public string JSON()
            {
                return JsonSerializer.Serialize<FDAStatus>(this);
            }

            public static FDAStatus Deserialize(string json)
            {
                FDAStatus status;
                try
                {
                    status = JsonSerializer.Deserialize<FDAStatus>(json);
                }
                catch
                {
                    return new FDAStatus();
                }

                return status;
            }



        }
          
    }

   
}
