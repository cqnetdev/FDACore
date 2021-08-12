using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace FDAController
{
    [SupportedOSPlatform("windows")]
    public partial class FrmMain : Form
    {
        private readonly Color GoodColor = Color.Green;
        private readonly Color WarningColor = Color.Blue;
        private readonly Color BadColor = Color.Red;
        private const int _controllerPort = 9571;
        private const string _controllerIP = "127.0.0.1";
        private bool _isStopping = false;
        private bool _isStarting = false;
        private readonly ServiceController FDAControllerService;
        private readonly ServiceController MQTTService;
        private readonly BackgroundWorker bg_StatusChecker;

        private delegate void SafePropertyUpdateDelgate(Control control, string property, object value);

        public FrmMain()
        {
            InitializeComponent();
            bg_StatusChecker = new BackgroundWorker();
            bg_StatusChecker.DoWork += Bg_StatusChecker_DoWork;
            bg_StatusChecker.WorkerSupportsCancellation = true;
            bg_StatusChecker.RunWorkerAsync();

            FDAControllerService = new ServiceController("FDAControllerService");
            MQTTService = new ServiceController("mosquitto");

            // set the Tag properties of the buttons to the names of the services, so we know what service to start/stop when they're clicked
            btnController.Tag = "FDAControllerService";
            btnMQTT.Tag = "mosquitto";

            //timer = new Timer();
            //timer.Interval = 1000;
            //timer.Tick += Timer_Tick;
            //timer.Start();
        }

        private void SafePropertySet(Control control, string property, object value)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new SafePropertyUpdateDelgate(SafePropertySet), new object[] { control, property, value });
            }
            else
            {
                control.GetType().GetProperty(property).SetValue(control, value);
            }
        }

        private bool CheckControllerService()
        {
            string result;
            string status;
            Color textcolor;
            bool controllerStatus;

            // is the controller service running?
            FDAControllerService.Refresh();
            if (FDAControllerService.Status == ServiceControllerStatus.Running)
            {
                status = "FDAControllerService is running";
                textcolor = GoodColor;

                // is the controller responsive to requests?
                result = SendRequest(_controllerIP, _controllerPort, "PING");
                if (result == "UP")
                {
                    status += " and responsive";
                    controllerStatus = true;
                    if (!_isStarting && !_isStopping)
                        SafePropertySet(btnFDA, "Enabled", true);
                }
                else
                {
                    status += " but is not responsive";
                    textcolor = WarningColor;
                    controllerStatus = false;
                    SafePropertySet(btnFDA, "Enabled", false);
                }
            }
            else
            {
                status = "FDA Controller service is not running";
                textcolor = BadColor;
                SafePropertySet(btnFDA, "Enabled", false);
                controllerStatus = false;
            }

            SafePropertySet(lblControllerService, "Text", status);
            SafePropertySet(lblControllerService, "ForeColor", textcolor);
            SafePropertySet(btnFDAMonitor, "Enabled", controllerStatus);

            SetButtonMode(btnController, controllerStatus);

            return controllerStatus;
        }

        private void SetButtonMode(Button button, bool status)
        {
            if (status)
            {
                SafePropertySet(button, "Text", "Stop");
                // ttFDAStart.Active = false;
            }
            else
            {
                SafePropertySet(button, "Text", "Start");
                // ttFDAStart.Active = true;
            }
        }

        private void CheckMosquittoService()
        {
            MQTTService.Refresh();
            bool status = MQTTService.Status == ServiceControllerStatus.Running;

            Color textcolor;
            if (status)
            {
                SafePropertySet(lblMQTT, "Text", "MQTT service is running");
                textcolor = GoodColor;
            }
            else
            {
                SafePropertySet(lblMQTT, "Text", "MQTT service is not running");
                textcolor = BadColor;
            }

            SafePropertySet(lblMQTT, "ForeColor", textcolor);

            SetButtonMode(btnMQTT, status);
        }

        private void CheckFDAProcess(bool controllerRunning)
        {
            Color textcolor;
            string result;
            string statusText;

            bool runstatus = ProcessRunning("FDACore");

            // is the FDA running?
            if (runstatus)
            {
                _isStarting = false;
                statusText = "FDACore process is running";
                textcolor = GoodColor;

                if (controllerRunning)
                {
                    // Get the current FDA status
                    result = SendRequest(_controllerIP, _controllerPort, "FDASTATUS\0");
                    FDAStatus status = null;
                    try
                    {
                        status = FDAStatus.Deserialize(result);
                    }
                    catch { }

                    if (result == null)
                    {
                        return;
                    }

                    if (status.RunStatus != "")
                    {
                        statusText += ", the status is '" + status.RunStatus + "'";
                    }

                    if (status.RunMode != "")
                    {
                        statusText += "\nFDACore mode is '" + status.RunMode + "'";
                    }

                    // ask the FDA controller for the FDA's current queue count (serves as an 'FDA is responsive' test)
                    //result = SendRequest(_controllerIP, _controllerPort, "TOTALQUEUECOUNT\0");
                    if (status.TotalQueueCount > -1)
                    {
                        statusText += "\n" + status.TotalQueueCount + " items in the communications queues";
                    }
                }
            }
            else
            {
                statusText = "FDACore process is not running";
                textcolor = BadColor;

                if (!_isStarting && controllerRunning)
                {
                    SafePropertySet(btnFDA, "Enabled", true);
                    SafePropertySet(btnFDA, "Text", "Start");
                }
            }
            SafePropertySet(lblFDA, "Text", statusText);
            SafePropertySet(lblFDA, "ForeColor", textcolor);

            SetButtonMode(btnFDA, runstatus);
        }

        private void Bg_StatusChecker_DoWork(object sender, DoWorkEventArgs e)
        {
            bool controllerStatus;
            while (!e.Cancel)
            {
                try
                {
                    controllerStatus = CheckControllerService();

                    CheckMosquittoService();

                    CheckFDAProcess(controllerStatus);

                    // update the 'last status update' timestamp
                    SafePropertySet(lblLastupdate, "Text", "Last status update: " + DateTime.Now.ToString());
                }
                catch { }
            }
        }

        private static string SendRequest(string address, int port, string request)
        {
            byte[] buffer = new byte[1024];
            byte[] response;
            int bytesRead = 0;
            using (TcpClient client = new())
            {
                // connect
                try
                {
                    client.Connect(address, port);
                    client.GetStream().WriteTimeout = 1000;
                    client.GetStream().ReadTimeout = 1000;
                }
                catch
                {
                    return "";
                }

                // send the request
                try
                {
                    byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                    client.GetStream().Write(requestBytes, 0, requestBytes.Length);
                }
                catch
                {
                    client.Close();
                    return "";
                }

                try
                {
                    bytesRead = client.GetStream().Read(buffer, 0, buffer.Length);
                }
                catch
                {
                    client.Close();
                    return "";
                }

                client.Close();
            }

            response = new byte[bytesRead];
            Array.Copy(buffer, response, bytesRead);

            return Encoding.UTF8.GetString(response);
        }

        private void BtnStartConsole_Click(object sender, EventArgs e)
        {
            StartFDA("-console");
        }

        private void BtnFDA_click(object sender, EventArgs e)
        {
        }

        private void BtnFDA_MouseClick(object sender, MouseEventArgs e)
        {
            if (!ProcessRunning("FDACore"))
            {
                if (e.Button == MouseButtons.Right)
                {
                    ctxStartFDA.Show(Cursor.Position);
                }
                else
                {
                    StartFDA("");
                }
            }
            else
                StopFDA();
        }

        private void MenuItemStartFDAbg_Click(object sender, EventArgs e)
        {
            StartFDA("");
        }

        private void MenuItemStartFDAConsole_Click(object sender, EventArgs e)
        {
            StartFDA("-console");
        }

        private void StopFDA()
        {
            string response = SendRequest(_controllerIP, _controllerPort, "SHUTDOWN\0");
            if (response != "FORWARDED")
            {
                MessageBox.Show("Shutdown command failed: " + response);
            }
            else
            {
                _isStopping = true;
                btnFDA.Enabled = false;
            }
        }

        private void BtnFDAMonitor_Click(object sender, EventArgs e)
        {
            RunConsoleCommand("telnet.exe", "127.0.0.1 9570");
        }

        private void StartFDA(string args)
        {
            // instead of sending the start command to the FDAController like we do in linux (because the FDA is a service)
            // we can just run the FDA directly from here
            _isStarting = true;

            string applicationDir = "C:\\IntricateFDA\\FDACore\\";

            if (File.Exists(applicationDir + "FDACore.exe"))
                RunConsoleCommand("cmd.exe", "/c FDACore.exe " + args, applicationDir);
            else
            {
                MessageBox.Show("FDA executable not found in the current folder \"" + applicationDir + "\\FDACore.exe");
            }
        }

        private static void RunConsoleCommand(string command, string args, string workingDir = "")
        {
            try
            {
                var processStartInfo = new ProcessStartInfo()
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                };

                if (workingDir != "")
                    processStartInfo.WorkingDirectory = workingDir;

                var process = new Process() { StartInfo = processStartInfo };

                process.Start();
                //string output = process.StandardOutput.ReadToEnd();
                //string error = process.StandardError.ReadToEnd();

                //process.WaitForExit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed execute command '" + command + "', " + ex.Message);
            }
            return;

            //if (string.IsNullOrEmpty(error)) { return output; }
            //else { return error; }
        }

        internal static bool ProcessRunning(string processname, bool self = false)
        {
            Process[] proc = Process.GetProcessesByName(processname);
            return (self && proc.Length > 1) || (!self && proc.Length > 0);
        }

        private void StartStopBtn_Click(object sender, EventArgs e)
        {
            // check if controller gui was run as admin
            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            if (!isAdmin)
            {
                MessageBox.Show("Please close the app and Run as Administrator to enable starting/stopping services");
                return;
            }

            string serviceName = ((Button)sender).Tag.ToString();
            ServiceController sc = new(serviceName);

            try
            {
                if (sc.Status == ServiceControllerStatus.Running)
                    sc.Stop();
                if (sc.Status == ServiceControllerStatus.Stopped)
                    sc.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public class FDAStatus
        {
            public TimeSpan UpTime { get; set; }
            public String RunStatus { get; set; }
            public String Version { get; set; }
            public String DB { get; set; }

            public String RunMode { get; set; }

            public int TotalQueueCount { get; set; }

            public FDAStatus()
            {
                UpTime = new TimeSpan(0);
                RunStatus = "";
                Version = "";
                DB = "";
                RunMode = "";
                TotalQueueCount = -1;
            }

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