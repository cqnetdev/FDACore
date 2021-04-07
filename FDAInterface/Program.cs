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

        public static bool operator ==(Connection a, Connection b)
        {
            if ((object)a == null || (object)b == null)
                return false;

            return (a.Description == b.Description && a.Host == b.Host);
        }

        public static bool operator !=(Connection a, Connection b)
        {
            if ((object)a == null || (object)b == null)
                return false;

            return !(a.Description == b.Description && a.Host == b.Host);
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
        internal enum ConnectionStatus {Disconnected,Connecting,Connected};

        // statuses 
        internal static ConnectionStatus _MQTTConnectionStatus;
        internal static ConnectionStatus _ControllerConnectionStatus;
        internal static string _CurrentFDA = "";
        internal static string _FDAName;
        internal static string _FDAStatus;

        internal static ConnectionStatus MQTTConnectionStatus { get { return _MQTTConnectionStatus; } set { _MQTTConnectionStatus = value; StatusUpdate?.Invoke(null, new StatusUpdateArgs("MQTT",value)); } }
        internal static ConnectionStatus ControllerConnectionStatus { get { return _ControllerConnectionStatus; } set { _ControllerConnectionStatus = value; StatusUpdate?.Invoke(null, new StatusUpdateArgs("Controller", value)); } }
        internal static string CurrentFDA { get { return _CurrentFDA; } set { _CurrentFDA = value; FDANameUpdate?.Invoke(null, _CurrentFDA); } }
        internal static string FDAName { get { return _FDAName; } set { _FDAName = value; } }
        internal static string FDAStatus { get { return _FDAStatus; } set { _FDAStatus = value; } }

        internal static ConnectionHistory ConnHistory;
        private static NotifyIcon notifyIcon;     
        private static bool _paused = false;
        private static MenuItem stopMenuItem;
        private static MenuItem startMenuItem;
        private static MenuItem startWithConsoleMenuItem;
        private static MenuItem pauseMenuItem;
        private static MenuItem openGuiMenuItem;
        private static byte[] ShutdownCommand;
        private static byte[] PauseCommand;
        private static byte[] ResumeCommand;
        //static TcpClient _TcpClient;
        //static NetworkStream _stream;
        //static SslStream _sslStream;
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

        private static string[] PendingChangeHost;

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
            Application.ApplicationExit += Application_ApplicationExit;
            //dataReceivedCheckTimer = new System.Threading.Timer(TCPDataReceivedCheck, null, Timeout.Infinite, Timeout.Infinite);
            for (int i = 0; i < args.Length; i++)
                args[i] = args[i].ToUpper();


            string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(exePath);

            MenuItem titleItem = new MenuItem("FDA Manager ver " + versionInfo.FileVersion);
            //startMenuItem = new MenuItem("Start FDA", new EventHandler(SendStartFDACommand));
            //stopMenuItem = new MenuItem("Shutdown FDA", new EventHandler(StopFDA));
            //startWithConsoleMenuItem = new MenuItem("Start FDA (with console)",new EventHandler(SendStartFDAConsoleCommand));
            openGuiMenuItem = new MenuItem("Open FDA Tools", new EventHandler(OpenGui));
            //pauseMenuItem = new MenuItem("Pause FDA", new EventHandler(PauseFDA));
            MenuItem exitMenuItem = new MenuItem("Close FDA Manager", DoExit);

            notifyIcon = new NotifyIcon()
            {
                Icon = Properties.Resources.ManagerGray,
                //ContextMenu = new ContextMenu(new MenuItem[] {titleItem,new MenuItem("-"),startMenuItem, startWithConsoleMenuItem, stopMenuItem,/*pauseMenuItem,*/openGuiMenuItem,new MenuItem("-"), exitMenuItem }),
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
            ControllerPinger = new System.Threading.Timer(ControllerPing);
            ControllerRetryTimer = new System.Threading.Timer(ControllerReconnectTimer_Tick);

            //ShutdownCommand = Encoding.ASCII.GetBytes("Shutdown");
            //PauseCommand = Encoding.ASCII.GetBytes("Pause");
            //ResumeCommand = Encoding.ASCII.GetBytes("Resume");

            // temporary ... connect to MQTT on linux machine
            //ChangeHost("10.0.0.186", "DevelopmentFDA");

            // if a running MQTT service is found on the local machine, automatically connect to it         
            //if (ProcessRunning("mosquitto"))
            //{
            //    string localFDAID = Settings.Default.LocalFDAIdentifier;
            //    ChangeHost("127.0.0.1", localFDAID);
            //}


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

        private void Application_ApplicationExit(object sender, EventArgs e)
        {
          
        }


        /*
        private void TCPDataReceivedCheck(object o)
        {
            // stop the timer (prevent ticks from piling up if the system gets busy)
            dataReceivedCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (_sslStream == null)
                goto ResetTimer;

            if (_sslStream == null)
                goto ResetTimer;

            if (!_TcpClient.Connected || !_sslStream.CanRead)
                goto ResetTimer;

            while (_stream.DataAvailable)
            {
                int readSize;
                byte[] readBuffer;
                byte[] header = new byte[3];
                int dataSize;
                byte dataType;

                // first two bytes are an int indicating the length of the rest of the message
                readSize = _sslStream.Read(header, 0, 3);
                if (readSize < 3)
                    return;
                dataSize = BitConverter.ToUInt16(header,0);

                // third byte indicates the type of the data
                dataType = header[2];

                // read the data portion of the message
                readBuffer = new byte[dataSize];
                readSize = _sslStream.Read(readBuffer, 0,dataSize);

                if (readSize < dataSize)
                    return;

                HandleMessage(readBuffer, dataType);

            }

            // set timer to tick again in x ms
            ResetTimer:
            dataReceivedCheckTimer.Change(dataReceivedCheckRate, Timeout.Infinite);
        }
    
        void HandleMessage(byte[] message, byte datatype)
        {
            if (datatype == 0) // ASCII data
            {
                string ASCIIMessage = Encoding.ASCII.GetString(message);
                string[] parsed = ASCIIMessage.Split(':');

                //response to elevate requests
                if (parsed[0].ToUpper() == "ELEVATE")
                {
                    elevatedPermissions = (parsed[1].ToUpper() == "SUCCESS");
                }
                else
                {
                    // hand anything else off to the GUI, if it's open (disabled, GUI doesn't get data this way anymore, all goes through MQTT
                    if (MainFormActive())
                        _mainForm.DataReceivedFromFDA(message, datatype);
                }
            } 
        }
        */

        internal static void ChangeHost(string newHost,string newFDAName)
        {
            if (newHost != Host)
            {
                MQTTReconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
                ControllerRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                ControllerPinger.Change(Timeout.Infinite, Timeout.Infinite);

                // connection thread(s) are busy, set the pending host change property and exit
                // when the thread(s) finish, they'll check for a pending host change and call this function again if it isn't null
                if (bg_MQTTConnect.IsBusy || bg_ControllerConnect.IsBusy)
                {
                    PendingChangeHost = new string[] { newHost, newFDAName, "False" };
                    return;
                }

               

                Host = newHost;
                FDAName = newFDAName;

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

                CurrentFDA = newFDAName + " (" + newHost + ")";
                if (MainFormActive())
                {
                     _mainForm.SetConnectionMenuItems(false);
                    _mainForm.SetFDAStatus("Unknown");
                }
                string[] args = new string[] { newHost, newFDAName,"False"};

                bg_MQTTConnect.RunWorkerAsync(args);
                bg_ControllerConnect.RunWorkerAsync(args);
            }
        }

        private static void Bg_ControllerConnect_DoWork(object sender, DoWorkEventArgs e)
        {
            string[] args = (string[])e.Argument;
            string host = args[0];
            string name = args[1];

            ControllerConnectionStatus = ConnectionStatus.Connecting;

            string message = "";
            bool error = false;
            if (FDAControllerClient != null)
            {
                if (FDAControllerClient.Connected)
                {
                    FDAControllerClient.Close();
                }
                FDAControllerClient.Dispose();
            }

            FDAControllerClient = new TcpClient();

            var result = FDAControllerClient.BeginConnect(host, 9571,null,null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
            if (success)
            {
                FDAControllerClient.EndConnect(result);
            }

            e.Result = new string[] { host,name,(success && FDAControllerClient.Connected).ToString(), message};
        }

        private static void Bg_ControllerConnect_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (PendingChangeHost != null)
            {
                string host = PendingChangeHost[0];
                string name = PendingChangeHost[1];
                PendingChangeHost = null;

                ChangeHost(host,name);
            }

            string[] results = (string[])e.Result;

            if (results[2] == "True") // successful connection
            {
                ControllerConnectionStatus = ConnectionStatus.Connected;
                ControllerPinger.Change(ControllerPingRate,ControllerPingRate);
                ControllerRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                ControllerConnectionStatus = ConnectionStatus.Disconnected;
                ControllerRetryTimer.Change(ControllerRetryRate, ControllerRetryRate);
                ControllerPinger.Change(Timeout.Infinite, Timeout.Infinite);
                /*
                string message = "Failed to connect to FDAController on FDA server " + results[1] + " (" + results[0] + ")";
                if (results[3] != "")
                {
                    message += "\nThe error message was \"" + results[3] + "\"";
                }

                if (MessageBox.Show(message, "Failed Connection", MessageBoxButtons.RetryCancel) == DialogResult.Retry)
                    bg_ControllerConnect.RunWorkerAsync(new string[] { results[0], results[1] });
                */

            }
        }

        private static void Bg_MQTTConnect_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (PendingChangeHost != null)
            {
                string host = PendingChangeHost[0];
                string name = PendingChangeHost[1];
                PendingChangeHost = null;
                
                ChangeHost(host,name);
            }
            // results is a four element string array {IP,name,connection result,message}
            string[] results = (string[])e.Result;

            if (results[2]=="success")
            {
                //FDAName = results[1];
                //Host = results[0];

                //CurrentConnectionStatus = "Connected to MQTT broker at " + Host + " (" + _FDAName + ")";
                //if (MainFormActive())
                //{
                //     _mainForm.SetConnectionStateText(CurrentConnectionStatus);
                //}

                // this is what we're replacing
                //Properties.Settings.Default.LastConnectedFDA = Host + "|" + FDAName;
                //Properties.Settings.Default.Save();
                ConnHistory.LastConnection = new Connection(Host, FDAName);


                MQTTConnectionStatus = ConnectionStatus.Connected;
                MQTT.MqttMsgPublishReceived += MQTT_MqttMsgPublishReceived;
                MQTT.ConnectionClosed += MQTT_ConnectionClosed;
                MQTT.Subscribe(new string[] { "FDAManager/command" }, new byte[] { 0 });

                // default to the 'FDA Stopped' red icon until we get a status update from the FDA
                notifyIcon.Icon = Properties.Resources.ManagerRed;

                MQTT.Subscribe(new string[] { "FDA/runstatus" }, new byte[] { 0 });
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

                //CurrentConnectionStatus = "FDA Connection Status";
                if (MainFormActive())
                {                  
                     //_mainForm.SetConnectionStateText(CurrentConnectionStatus);
                    _mainForm.SetFDAStatus("Unknown");
                }

                MQTTReconnectTimer.Change(ReconnectRate, ReconnectRate);               
            }


            if (MainFormActive())
                _mainForm.SetConnectionMenuItems(true);
        }

        private static void ControllerPing(object state)
        {
            ControllerPinger.Change(Timeout.Infinite, Timeout.Infinite);
            bool connected = true;

            if (FDAControllerClient == null)
            {
                connected = false;
            }
            else
                if (FDAControllerClient.Connected == false)
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

            if (!connected)
            {
                ControllerConnectionStatus = ConnectionStatus.Disconnected;
                if (!bg_ControllerConnect.IsBusy)
                    bg_ControllerConnect.RunWorkerAsync(new string[] { Host, FDAName, "True" });
            }
            else
                ControllerPinger.Change(ControllerPingRate, ControllerPingRate);
        }

        private static void MQTT_ConnectionClosed(object sender, EventArgs e)
        {
            MQTTConnectionStatus = ConnectionStatus.Disconnected;

            MQTTReconnectTimer.Change(ReconnectRate, ReconnectRate);
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

        private static void Bg_MQTTConnect_DoWork(object sender, DoWorkEventArgs e)
        {
            string[] args = (string[])e.Argument;
            string errorMsg = "";
            // arguments array too short, exit with an error
            if (args.Length < 3)
            {
                // results array is 4 elements long (0/1 are the host IP/Name, 2 is the result (can be success or failure), 3 is any messages (like errors)
                e.Result = new string[] { args.ToString(), "", "failure","invalid argument passed to Bg_MQTTConnect_DoWork()"};
                return;
            }

            // arguments array too long, take the first three and dump the rest
            if (args.Length > 3)
            {
                args = new string[] { args[0], args[1],args[2] };
            }

            MQTTConnectionStatus = ConnectionStatus.Connecting;

            string MQTTServer_IP = args[0];
            string MQTTServer_Name = args[1];
            bool suppressMessages = (args[2] == "True");
            
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

                // get the MQTT password (this should probably be entered in the form and passed to this procedure, instead of this)
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

            if (success)
            {
                // results array is 4 elements long (0/1 are the host IP/Name, 2 is the result (can be success or failure), 3 is any messages (like errors)
                string[] results = new string[] { args[0], args[1], "success", "" };
                e.Result = results;
            }
            else
            {
                string[] results;
                if (!suppressMessages)
                {
                    results = new string[] { args[0], args[1], "failure", errorMsg };                
                }
                else
                {
                    results = new string[] { args[0], args[1], "failure-suppress", errorMsg };
                }
                e.Result = results;
            }

            return;
      

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
                case "FDA/runstatus":
                    if (e.Message.Length > 0)
                        UpdateFDAStatusIcon(Encoding.UTF8.GetString(e.Message));
                    break;
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
                    //startMenuItem.Enabled = false;
                    //startWithConsoleMenuItem.Enabled = false;
                    //stopMenuItem.Enabled = false;
                    //pauseMenuItem.Enabled = false;
                    notifyIcon.Text = "FDA Starting";
                    break;
                // running
                case "Normal":
                    notifyIcon.Icon = Properties.Resources.ManagerGreen;
                    //startMenuItem.Enabled = false;
                    //startWithConsoleMenuItem.Enabled = false;
                    //stopMenuItem.Enabled = true;
                    //pauseMenuItem.Enabled = true;
                    notifyIcon.Text = "FDA Running";
                    break;
                // Shutting down
                case "ShuttingDown":
                    notifyIcon.Icon = Properties.Resources.ManagerYellow;
                    //startMenuItem.Enabled = false;
                    //startWithConsoleMenuItem.Enabled = false;
                    //stopMenuItem.Enabled = false;
                    //pauseMenuItem.Enabled = false;
                    notifyIcon.Text = "FDA Shutting Down";
                    break;
                // Pausing
                case "Pausing":
                    notifyIcon.Icon = Properties.Resources.ManagerYellow;
                    //startMenuItem.Enabled = false;
                    //startWithConsoleMenuItem.Enabled = false;
                    //stopMenuItem.Enabled = false;
                    //pauseMenuItem.Enabled = false;
                    notifyIcon.Text = "FDA Pausing";
                    break;
                //paused
                case "Paused":
                    notifyIcon.Icon = Properties.Resources.ManagerYellow;
                    //startMenuItem.Enabled = false;
                    //startWithConsoleMenuItem.Enabled = false;
                    //stopMenuItem.Enabled = false;
                    //pauseMenuItem.Enabled = true;
                    notifyIcon.Text = "FDA Paused";
                    break;
                //stopped
                case "Stopped":
                    notifyIcon.Icon = Properties.Resources.ManagerRed;
                    //startMenuItem.Enabled = true;
                    //startWithConsoleMenuItem.Enabled = true;
                    //stopMenuItem.Enabled = false;
                    //pauseMenuItem.Enabled = false;
                    notifyIcon.Text = "FDA Stopped";
                    break;

            }

            /*

            if (connected && !_paused)
            {
                notifyIcon.Icon = Properties.Resources.ManagerGreen;
                startMenuItem.Enabled = false;
                startWithConsoleMenuItem.Enabled = false;
                stopMenuItem.Enabled = true;
                pauseMenuItem.Enabled = true;
                notifyIcon.BalloonTipText = "FDA Running";
            }

            if (connected && _paused)
            {
                notifyIcon.Icon = Properties.Resources.ManagerYellow;
                startMenuItem.Enabled = false;
                startWithConsoleMenuItem.Enabled = false;
                stopMenuItem.Enabled = true;
                pauseMenuItem.Enabled = true;
                notifyIcon.BalloonTipText = "FDA Paused";
            }

            if (!connected)
            {
                notifyIcon.Icon = Properties.Resources.ManagerRed;
                startMenuItem.Enabled = true;
                startWithConsoleMenuItem.Enabled = true;
                stopMenuItem.Enabled = false;
                pauseMenuItem.Enabled = false;
                notifyIcon.BalloonTipText = "FDA Stopped";
            }
            */
            /*
            if (MainFormActive())
            {
                _mainForm.UpdateFDAConnStatus(notifyIcon.BalloonTipText);
            }
            */
        }



        internal static void PauseFDA(object sender, EventArgs e)
        {
            if (!_paused)
            {
                //SendToFDA("PAUSE");
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
                //SendToFDA("RESUME");
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


        internal static void SendCommandToFDAController(string command)
        {
            try
            {
                if (FDAControllerClient != null)
                {
                    if (FDAControllerClient.Connected)
                    {
                        byte[] toSend = Encoding.UTF8.GetBytes(command);
                        FDAControllerClient.GetStream().Write(toSend, 0, toSend.Length);
                    }
                    else
                        MessageBox.Show("Unable to send the start command because the FDA controller service at " + Host + " is not connected");
                }
                else
                    MessageBox.Show("Unable to send the start command because the FDA controller service at " + Host + " is not connected");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while attempting to send a command to the FDA controller service at " + Host + " : " + ex.Message);
            }
        }


        //internal static void SendStartFDAConsoleCommand(object sender, EventArgs e)
        //{
        //    SendCommandToFDAController("START"); // to do, implement a "start with console" command, once a "console viewer" app exists

        //    /*
        //    bool success = false;
        //    if (MQTT != null)
        //    {
        //        if (MQTT.IsConnected)
        //        {
        //            //MessageBox.Show("sending command: STARTCONSOLE " + ConnectedFDAName);
        //            MQTT.Publish("FDAManager/command", Encoding.UTF8.GetBytes("STARTCONSOLE " + ConnectedFDAName));
        //            success = true;
        //        }
        //    }

        //    if (!success)
        //        MessageBox.Show("Unable to send FDA startup command, MQTT Broker not available");
        //    */
        //    /*
        //    if (!ConnectionStatus)
        //    {
        //        string FDAPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\FDA.exe";
        //        if (File.Exists(FDAPath))
        //        {
        //            Process.Start(FDAPath, "-show");
        //            pauseMenuItem.Enabled = true;
        //        }
        //        else
        //            MessageBox.Show("FDA executable not found at " + FDAPath, "FDA Manager", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        //    }
        //    */
        //}


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

                if (bg_MQTTConnect != null)
                {
                    _mainForm.SetConnectionMenuItems(!bg_MQTTConnect.IsBusy);
                }

                
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
            
            
            _mainForm.SetFDAStatus(_FDAStatus);
            

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
                    MQTT.Disconnect();
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
            catch (Exception ex)
            {
               
            }
            
            Application.Exit();
        }

    }

   
}
