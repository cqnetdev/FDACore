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

namespace FDAInterface
{
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
        private static NotifyIcon notifyIcon;
        internal static bool _MQTTConnectionStatus;
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
        // static SslStream _sslStream;
        private static frmMain2 _mainForm;
       // private System.Threading.Timer dataReceivedCheckTimer;
        //private int dataReceivedCheckRate = 100;
        internal static bool elevatedPermissions = false;
        internal static string Host;
        
        internal static string ConnectedFDAName;
        private static string ConnectingFDAName;

        //private static int _FDAport = 9572;
        private static System.Threading.Timer MqttRetryTimer;

        internal static MqttClient MQTT;

        private static string _FDAStatus;

        private static bool MQTTConnectionStatus { get { return _MQTTConnectionStatus; } set { _MQTTConnectionStatus = value; } }

        //private static System.Threading.Timer FDACheckTimer;

        internal static bool ProcessRunning(string processname,bool self = false)
        {
            Process[] proc = Process.GetProcessesByName(processname);
            return (self && proc.Length > 1) || (!self && proc.Length > 0);          
        }


        public FDAManagerContext(string[] args)
        {
            //dataReceivedCheckTimer = new System.Threading.Timer(TCPDataReceivedCheck, null, Timeout.Infinite, Timeout.Infinite);
            for (int i = 0; i < args.Length; i++)
                args[i] = args[i].ToUpper();



            //if (args.Contains("-MASTER"))
            //    _isMaster = true;

            /*
            if (args.Length > 0)
            {
                if (args[0].ToUpper() == "-STARTFDA")
                    SendStartFDACommand(this, new EventArgs());

                if (args[0].ToUpper() == "-STARTFDACONSOLE")
                    SendStartFDAConsoleCommand(this, new EventArgs());
            }
            */

            string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(exePath);

            MenuItem titleItem = new MenuItem("FDA Manager ver " + versionInfo.FileVersion);
            startMenuItem = new MenuItem("Start FDA", new EventHandler(SendStartFDACommand));
            stopMenuItem = new MenuItem("Shutdown FDA", new EventHandler(StopFDA));
            startWithConsoleMenuItem = new MenuItem("Start FDA (with console)",new EventHandler(SendStartFDAConsoleCommand));
            openGuiMenuItem = new MenuItem("Open FDA Tools", new EventHandler(OpenGui));
            pauseMenuItem = new MenuItem("Pause FDA", new EventHandler(PauseFDA));
            MenuItem exitMenuItem = new MenuItem("Close FDA Manager", DoExit);

            notifyIcon = new NotifyIcon()
            {
                Icon = Properties.Resources.ManagerGray,
                ContextMenu = new ContextMenu(new MenuItem[] {titleItem,new MenuItem("-"),startMenuItem, startWithConsoleMenuItem, stopMenuItem,/*pauseMenuItem,*/openGuiMenuItem,new MenuItem("-"), exitMenuItem }),
                Visible = true,
            };

            ShutdownCommand = Encoding.ASCII.GetBytes("Shutdown");
            PauseCommand = Encoding.ASCII.GetBytes("Pause");
            ResumeCommand = Encoding.ASCII.GetBytes("Resume");

            // temporary ... connect to MQTT on linux machine
            //ChangeHost("10.0.0.186", "DevelopmentFDA");

            // if a running MQTT service is found on the local machine, automatically connect to it
          
            if (ProcessRunning("mosquitto"))
            {
                string localFDAID = Settings.Default.LocalFDAIdentifier;
                ChangeHost("127.0.0.1", localFDAID);
            }
            
            
            // get the details of the previous connection
            /*
            string connDetails = Properties.Settings.Default.LastConnectedFDA;
            string[] details = connDetails.Split(':');
            if (details.Length >= 2 && details[0] != "")
            {
                ChangeHost(details[0], details[1]);
            }
            */

            /*
            if (MQTT != null)
            {
                if (MQTT.IsConnected)
                {
                    if (args.Contains("-STARTFDA"))
                        SendStartFDACommand(this, new EventArgs());

                    if (args.Contains("-STARTFDACONSOLE"))
                        SendStartFDAConsoleCommand(this, new EventArgs());
                }
            }
            */
            //FDACheckTimer = new System.Threading.Timer(CheckConnection, null,TimeSpan.Zero,Timeout.InfiniteTimeSpan);
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
                /*
                FDACheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
                if (_TcpClient != null)
                {
                    if (_TcpClient.Connected)
                    {
                        _TcpClient.Close();
                        _TcpClient.Dispose();
                        _TcpClient = new TcpClient();
                    }
                }
                */

                ConnectingFDAName = newFDAName;
                if (MQTT != null)
                {
                    if (MQTT.IsConnected)
                    {
                        MQTT.Disconnect();                      
                    }
                    MQTT = null;
                }

                MQTTConnect(newHost);
               // FDACheckTimer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            }
        }

        /*
        protected virtual void CheckConnection(object o)
        {
           // FDACheckTimer.Change(Timeout.Infinite, Timeout.Infinite);


            // send a ping to the FDA (so we can check if we have a connection)
            //SendToFDA("ping");

            bool lastStatus = MQTTConnectionStatus;

            
            if (_TcpClient == null)
            {
                _TcpClient = new TcpClient();
            }

            if (_TcpClient.Connected)
            {
                if (_TcpClient.Client.Connected)
                {
                    if (!MQTTConnectionStatus)
                    {
                        MQTTConnectionStatus = true;
                    }
                    goto Reset;
                }
                else
                {
                    if (MQTTConnectionStatus) MQTTConnectionStatus = false;
                }
            }
            else
            {
                if (MQTTConnectionStatus) MQTTConnectionStatus = false;
            }
                
            
            if (!MQTTConnectionStatus)
            {
                if (Host == "")
                {
                    goto Reset;
                }
                try
                {
                    _TcpClient.Connect(Host, _FDAport);
                    _stream = _TcpClient.GetStream();
                    _sslStream = new SslStream(_stream, false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                    _sslStream.ReadTimeout = 2000;
                    _sslStream.WriteTimeout = 2000;
                    _sslStream.AuthenticateAsClient("AVERYANDMCCLEOD\\Mike");
                    MQTTConnectionStatus = true;
                }
                catch
                {
                    MQTTConnectionStatus = false;
                    _stream?.Dispose();
                    _TcpClient?.Dispose();
                    _TcpClient = new TcpClient();
                    goto Reset;
                }            
            }

            if (lastStatus != MQTTConnectionStatus)
            {
                if (MQTTConnectionStatus) // this is a new connection, we need to request elevated permissions and connect to MQTT
                {
                    Random rnd = new Random();

                    string requeststring = "Elevate:" + Common.Encrypt.EncryptString(DateTime.UtcNow.ToOADate().ToString(), "KVvvc5thpcrsc5Hkpkof");
                    SendToFDA(requeststring);

                    Properties.Settings.Default.LastConnectedFDA = Host;
                    Properties.Settings.Default.Save();

                    // connect to MQTT
                    MQTTConnect(Host);
                }
                else
                {
                    // this is a new disconnection, we need to notify the GUI (if open), so it can reset the form
                    if (MainFormActive())
                        _mainForm.ClearForm();
                }

            }

        Reset:
            FDACheckTimer.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
        }

        */
        private static void MQTTConnect(object o)
        {
            //MessageBox.Show("Connecting to MQTT at " + o.ToString());
            retry:
            bool success = false;
            try
            {
                if (MQTT != null)
                {
                    if (MQTT.IsConnected)
                    {
                        MQTT.Disconnect();
                        MQTT = null;
                    }
                }

                MQTT = new MqttClient(o.ToString());

                string pass = Encryption.DecryptString(Properties.Settings.Default.MQTT, "MC9fSLAfwkrHCx0j6LG4");
                
                MQTT.Connect("FDAManager" + Guid.NewGuid().ToString(), "FDAManager", pass);
                if (MQTT.IsConnected)
                {
                    success = true;
                }
            }
            catch (Exception ex)
            {
                success = false;
            }

            if (success)
            {
                ConnectedFDAName = ConnectingFDAName;
                Host = o.ToString();
                
                Properties.Settings.Default.LastConnectedFDA = Host + "|" + ConnectedFDAName;
                Properties.Settings.Default.Save();

                MQTT.ConnectionClosed += MQTT_ConnectionClosed;
                MQTTConnectionStatus = true;
                MQTT.MqttMsgPublishReceived += MQTT_MqttMsgPublishReceived;
                MQTT.Subscribe(new string[] { "FDAManager/command" }, new byte[] { 0 });

                // default to the 'FDA Stopped' red icon until we get a status update from the FDA
                notifyIcon.Icon = Properties.Resources.ManagerRed;

                MQTT.Subscribe(new string[] { "FDA/runstatus" }, new byte[] { 0 });
                MQTT.Subscribe(new string[] { "FDA/identifier" }, new byte[] { 0 });

                if (MainFormActive())
                {
                    _mainForm.SetMQTT(MQTT);

                    // notify the GUI that the MQTT connection has been established
                    _mainForm.MQTTConnected(ConnectedFDAName,Host);
                }


                if (MqttRetryTimer != null)
                {
                    MqttRetryTimer.Change(0, 0);
                    MqttRetryTimer.Dispose();
                    MqttRetryTimer = null;
                }
            }
            else
            {
                notifyIcon.Icon = Properties.Resources.ManagerGray;
                notifyIcon.Text = "FDA Status unknown - not connected";
                Host = "";
                //MessageBox.Show("Failed to connect to MQTT");
                MQTTConnectionStatus = false;

                if (MainFormActive())
                {
                    _mainForm.SetFDAStatus("Disconnected");
                }
                if (MessageBox.Show("Failed to connect to " + o.ToString(), "Connection failed", MessageBoxButtons.RetryCancel) == DialogResult.Retry)
                    goto retry;

                // start a 5 second timer for re-attempting the connection
                // MqttRetryTimer = new System.Threading.Timer(MQTTConnect,o, 5000, 5000);
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
                case "FDA/runstatus":
                    if (e.Message.Length > 0)
                        UpdateFDAStatusIcon(Encoding.UTF8.GetString(e.Message));
                    break;
                case "FDA/identifier":
                    ConnectedFDAName = Encoding.UTF8.GetString(e.Message);
                    break;
            }
        }

        private static void MQTT_ConnectionClosed(object sender, EventArgs e)
        {
            if (MQTT != null)
                MQTT.ConnectionClosed -= MQTT_ConnectionClosed;

            if (MainFormActive())
            {
                _mainForm.MQTTDisconnected();
            }
            //if (MqttRetryTimer == null)
            //    MqttRetryTimer = new System.Threading.Timer(MQTTConnect,Host, 5000, 5000);
            //else
            //    MqttRetryTimer.Change(5000, 5000);
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
                    startMenuItem.Enabled = false;
                    startWithConsoleMenuItem.Enabled = false;
                    stopMenuItem.Enabled = false;
                    pauseMenuItem.Enabled = false;
                    notifyIcon.Text = "FDA Starting";
                    break;
                // running
                case "Normal":
                    notifyIcon.Icon = Properties.Resources.ManagerGreen;
                    startMenuItem.Enabled = false;
                    startWithConsoleMenuItem.Enabled = false;
                    stopMenuItem.Enabled = true;
                    pauseMenuItem.Enabled = true;
                    notifyIcon.Text = "FDA Running";
                    break;
                // Shutting down
                case "ShuttingDown":
                    notifyIcon.Icon = Properties.Resources.ManagerYellow;
                    startMenuItem.Enabled = false;
                    startWithConsoleMenuItem.Enabled = false;
                    stopMenuItem.Enabled = false;
                    pauseMenuItem.Enabled = false;
                    notifyIcon.Text = "FDA Shutting Down";
                    break;
                // Pausing
                case "Pausing":
                    notifyIcon.Icon = Properties.Resources.ManagerYellow;
                    startMenuItem.Enabled = false;
                    startWithConsoleMenuItem.Enabled = false;
                    stopMenuItem.Enabled = false;
                    pauseMenuItem.Enabled = false;
                    notifyIcon.Text = "FDA Pausing";
                    break;
                //paused
                case "Paused":
                    notifyIcon.Icon = Properties.Resources.ManagerYellow;
                    startMenuItem.Enabled = false;
                    startWithConsoleMenuItem.Enabled = false;
                    stopMenuItem.Enabled = false;
                    pauseMenuItem.Enabled = true;
                    notifyIcon.Text = "FDA Paused";
                    break;
                //stopped
                case "Stopped":
                    notifyIcon.Icon = Properties.Resources.ManagerRed;
                    startMenuItem.Enabled = true;
                    startWithConsoleMenuItem.Enabled = true;
                    stopMenuItem.Enabled = false;
                    pauseMenuItem.Enabled = false;
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

        internal static void SendStartFDACommand(object sender, EventArgs e)
        {
            SendCommandToFDAController("START");
        
            

            /* start command doesn't go over MQTT anymore
            bool success = false;
            if (MQTT != null)
            {
                if (MQTT.IsConnected)
                {
                    MQTT.Publish("FDAManager/command", Encoding.UTF8.GetBytes("START " + ConnectedFDAName));
                    success = true;
                }
            }

            if (!success)
                MessageBox.Show("Unable to send FDA startup command, MQTT Broker not available");
            */

        }

        internal static void SendCommandToFDAController(string command)
        {
            try
            {
                // connect to the FDA Controller Service on the FDA machine and send the start command
                using (TcpClient FDAControllerClient = new TcpClient())
                {
                    FDAControllerClient.Connect("10.0.0.186", 9571);

                    using (NetworkStream stream = FDAControllerClient.GetStream())
                    {
                        byte[] toSend = Encoding.UTF8.GetBytes(command);
                        stream.Write(toSend, 0, toSend.Length);
                        Thread.Sleep(500);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while attempting to send a command to the FDA controller service: " + ex.Message);
            }
        }


        internal static void SendStartFDAConsoleCommand(object sender, EventArgs e)
        {
            SendCommandToFDAController("START"); // to do, implement a "start with console" command, once a "console viewer" app exists

            /*
            bool success = false;
            if (MQTT != null)
            {
                if (MQTT.IsConnected)
                {
                    //MessageBox.Show("sending command: STARTCONSOLE " + ConnectedFDAName);
                    MQTT.Publish("FDAManager/command", Encoding.UTF8.GetBytes("STARTCONSOLE " + ConnectedFDAName));
                    success = true;
                }
            }

            if (!success)
                MessageBox.Show("Unable to send FDA startup command, MQTT Broker not available");
            */
            /*
            if (!ConnectionStatus)
            {
                string FDAPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\FDA.exe";
                if (File.Exists(FDAPath))
                {
                    Process.Start(FDAPath, "-show");
                    pauseMenuItem.Enabled = true;
                }
                else
                    MessageBox.Show("FDA executable not found at " + FDAPath, "FDA Manager", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            */
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


        internal static void StopFDA(object sender, EventArgs e)
        {
            SendCommandToFDAController("SHUTDOWN");
            //MQTT?.Publish("FDAManager/command", Encoding.UTF8.GetBytes("SHUTDOWN"));
            //SendToFDA("SHUTDOWN");
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
              
            }
            else
                _mainForm.Focus();
        }

        private void _mainForm_Shown(object sender, EventArgs e)
        {

            if (MQTTConnectionStatus == true)
            {
                _mainForm.SetMQTT(MQTT);
                _mainForm.MQTTConnected(ConnectedFDAName, Host);
            }

            _mainForm.SetFDAStatus(_FDAStatus);
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

            if (MQTT != null)
            {
                if (MQTT.IsConnected)
                    MQTT.Disconnect();
                MQTT = null;
            }

            /*
            if (_TcpClient != null)
            {
                if (_TcpClient.Connected)
                {
                    _TcpClient.Close();

                }
                _TcpClient.Dispose();
            }
            */
            Application.Exit();
        }

     /*
        public static void SendToFDA(string command)
        {
            if (_sslStream == null)
                return;

            try {
                //byte[] requestIDbytes;
                if (!_sslStream.CanWrite)
                    return;// new byte[0];             

                _sslStream.ReadTimeout = 1000;
                _sslStream.WriteTimeout = 1000;


                // convert the command from a string to bytes
                byte[] dataBlock = Encoding.ASCII.GetBytes(command);

                byte[] header = new byte[3];
                Array.Copy(BitConverter.GetBytes((ushort)dataBlock.Length), 0, header, 0, 2);
                header[2] = 0; // ASCII data type

                byte[] toSend = new byte[header.Length + dataBlock.Length];

                Array.Copy(header, toSend, header.Length);
                Array.Copy(dataBlock, 0, toSend, header.Length, dataBlock.Length);


                // and send it off
                _sslStream.Write(toSend, 0, toSend.Length);
            }
            catch 
            {
                return;// new byte[0];
            }
        }


        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors) { return true; }

            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }
        */
    }

   
}
