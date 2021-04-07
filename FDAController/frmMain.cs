using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.ServiceProcess;

namespace FDAController
{
    public partial class frmMain : Form
    {
        private Color GoodColor = Color.Green;
        private Color WarningColor = Color.Blue;
        private Color BadColor = Color.Red;
        private int _controllerPort = 9571;
        private string _controllerIP = "127.0.0.1";
        private bool _isStopping = false;
        private bool _isStarting = false;
        private Timer timer;
        private ServiceController FDAControllerService;
        private BackgroundWorker bg_StatusChecker;

        private delegate void SafePropertyUpdateDelgate(Control control, string property, object value);

        public frmMain()
        {
            InitializeComponent();
            bg_StatusChecker = new BackgroundWorker();
            bg_StatusChecker.DoWork += Bg_StatusChecker_DoWork;
            bg_StatusChecker.WorkerSupportsCancellation = true;
            bg_StatusChecker.RunWorkerAsync();

            //timer = new Timer();
            //timer.Interval = 1000;
            //timer.Tick += Timer_Tick;
            //timer.Start();
        }

        private void SafePropertySet(Control control,string property,object value)
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

        private void Bg_StatusChecker_DoWork(object sender, DoWorkEventArgs e)
        {
            FDAControllerService = new ServiceController("FDAControllerService");
            while (!e.Cancel)
            {
                string result;
                string status;
                Color textcolor = GoodColor;
                bool controllerGood = false;

                // is the controller service running?
                if (ProcessRunning("FDAControllerService"))
                {
                    status = "FDAControllerService is running";
                    textcolor = GoodColor;

                    // is the controller responsive to requests?
                    result = SendRequest(_controllerIP, _controllerPort, "PING");
                    if (result == "UP")
                    {
                        status += " and responsive";
                        controllerGood = true;
                    }
                    else
                    {
                        status += " but is not responsive";
                        textcolor = WarningColor;
                    }
                }
                else
                {
                    status = "FDA Controller service is not running";
                    textcolor = BadColor;
                    SafePropertySet(btnStop, "Enabled", false);
                    SafePropertySet(btnStart, "Enabled", false);
                    SafePropertySet(btnStartConsole, "Enabled", false);

                }

                SafePropertySet(lblControllerService, "Text", status);
                SafePropertySet(lblControllerService, "ForeColor", textcolor);
                SafePropertySet(btnFDAMonitor, "Enabled", controllerGood);
                


                // is MQTT running?
                if (ProcessRunning("mosquitto"))
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



                // is the FDA running?
                if (ProcessRunning("FDACore"))
                {
                    _isStarting = false;
                    status = "FDACore is running";
                    textcolor = GoodColor;
                    if (!_isStopping)
                    {
                        SafePropertySet(btnStop, "Enabled", true);
                    }

                    SafePropertySet(btnStart, "Enabled", false);
                    SafePropertySet(btnStartConsole, "Enabled", false);
                    SafePropertySet(btnStart, "Text", "Start");
  

                    // ask the FDA controller for the FDA's run mode (background or console)
                    result = SendRequest(_controllerIP, _controllerPort, "RUNMODE");
                    if (result != "")
                    {
                        status += " in " + result + " mode";
                    }

                    // ask the FDA controller for the FDA's current queue count (serves as an 'FDA is responsive' test)
                    result = SendRequest(_controllerIP, _controllerPort, "TOTALQUEUECOUNT");
                    int count;
                    if (int.TryParse(result, out count))
                    {
                        if (count > -1)
                        {
                            status += ", " + count + " items in the communications queues";
                        }
                    }
                    else
                    {
                        status += ", unknown number of items in the communications queues";
                        textcolor = WarningColor;
                    }
                }
                else
                {
                    status = "FDACore is not running";
                    textcolor = BadColor;
                    if (_isStopping)
                    {
                        SafePropertySet(btnStop, "Text", "Stop");

                    }
                    _isStopping = false;
                    
                    SafePropertySet(btnStop, "Enabled", false);

                    if (!_isStarting)
                    {
                        SafePropertySet(btnStart, "Enabled",true);
                        SafePropertySet(btnStart, "Text", "Start");
                        SafePropertySet(btnStartConsole, "Enabled", true);

                    }

                }
                SafePropertySet(lblFDA, "Text", status);
                SafePropertySet(lblFDA, "ForeColor", textcolor);



                // update the 'last status update' timestamp
                SafePropertySet(lblLastupdate, "Text", "Last status update: " + DateTime.Now.ToString());

    
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            string result;
            string status;
            Color textcolor = GoodColor;
            bool controllerGood = false;

            // is the controller service running?
            if (ProcessRunning("FDAControllerService"))
            {
                status = "FDAControllerService is running";
                textcolor = GoodColor;

                // is the controller responsive to requests?
                result = SendRequest(_controllerIP, _controllerPort, "PING");
                if (result == "UP")
                {
                    status += " and responsive";
                    controllerGood = true;
                }
                else
                {
                    status += " but is not responsive";
                    textcolor = WarningColor;
                }
            }
            else
            {
                status = "FDA Controller service is not running";
                textcolor = BadColor;
                btnStop.Enabled = false;
                btnStart.Enabled = false;
                btnStartConsole.Enabled = false;               
            }
            lblControllerService.Text = status;
            lblControllerService.ForeColor = textcolor;
            btnFDAMonitor.Enabled = controllerGood;

            // is MQTT running?
            if (ProcessRunning("mosquitto"))
            {
                lblMQTT.Text = "MQTT service is running";
                textcolor = GoodColor;
            }
            else
            {
                lblMQTT.Text = "MQTT service is not running";
                textcolor = BadColor;
            }
            lblMQTT.ForeColor = textcolor;

            // is the FDA running?
            if (ProcessRunning("FDACore"))
            {
                _isStarting = false;
                status = "FDACore is running";
                textcolor = GoodColor;
                if (!_isStopping)
                {
                    btnStop.Enabled = true;
                }
                btnStart.Enabled = false;
                btnStartConsole.Enabled = false;
                btnStart.Text = "Start";

                // ask the FDA controller for the FDA's run mode (background or console)
                result = SendRequest(_controllerIP, _controllerPort, "RUNMODE");
                if (result != "")
                {
                    status += " in " + result + " mode";
                }

                // ask the FDA controller for the FDA's current queue count (serves as an 'FDA is responsive' test)
                result = SendRequest(_controllerIP, _controllerPort, "TOTALQUEUECOUNT");
                int count;
                if (int.TryParse(result, out count))
                {
                    if (count > -1)
                    {
                        status += ", " + count + " items in the communications queues";
                    }
                }
                else
                {
                    status += ", unknown number of items in the communications queues";
                    textcolor = WarningColor;
                }
            }
            else
            {
                status = "FDACore is not running";
                textcolor = BadColor;
                if (_isStopping)
                {
                    btnStop.Text = "Stop";
                }
                _isStopping = false;
                btnStop.Enabled = false;
                if (!_isStarting)
                {
                    btnStart.Enabled = true;
                    btnStart.Text = "Start";
                    btnStartConsole.Enabled = true;
                }
                
            }
            lblFDA.Text = status;
            lblFDA.ForeColor = textcolor;


            // update the 'last status update' timestamp
            lblLastupdate.Text = "Last status update: " + DateTime.Now.ToString();
        }

        private string SendRequest(string address,int port, string request)
        {
            byte[] buffer = new byte[1024];
            byte[] response;
            int bytesRead = 0;
            using (TcpClient client = new TcpClient())
            {
                // connect
                try
                {
                    client.Connect(address, port);
                    client.GetStream().WriteTimeout = 1000;
                    client.GetStream().ReadTimeout = 1000;
                } catch (Exception ex)
                {
                    return "";
                }

                // send the request
                try
                {
                    byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                    client.GetStream().Write(requestBytes, 0, requestBytes.Length);
                } catch (Exception ex)
                {
                    client.Close();
                    return "";
                }

                try
                {
                    bytesRead = client.GetStream().Read(buffer, 0, buffer.Length);
                } catch (Exception ex)
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

        private void btnStartConsole_Click(object sender, EventArgs e)
        {
            StartFDA("-console");
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            StartFDA("");
        }


        private void btnFDAMonitor_Click(object sender, EventArgs e)
        {
            RunConsoleCommand("openFDAMonitor.bat","");
            //RunConsoleCommand("notepad.exe", "");
            //RunConsoleCommand("cmd.exe","");
            //RunConsoleCommand("%SystemRoot%\\System32\\telnet.exe", "127.0.0.1 9570"); //<--- this is not working grrr
            //RunConsoleCommand("cmd.exe", "/c cd c:\\");
            //RunConsoleCommand("telnet.exe","");       
        }

        private void StartFDA(string args)
        {
            // instead of sending the start command to the FDAController like we do in linux (because the FDA is a service)
            // we can just run the FDA directly from here
            _isStarting = true;
            btnStart.Enabled = false;
            btnStartConsole.Enabled = false;
            string applicationDir = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);

            if (File.Exists("FDACore.exe"))
                RunConsoleCommand("cmd.exe","/c FDACore.exe " + args, applicationDir);
            else
            {
                MessageBox.Show("FDA executable not found in the current folder \"" + applicationDir + "\\FDACore.exe");
            }

        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            btnStartConsole.Enabled = false;
            btnStop.Enabled = false;


            string response = SendRequest(_controllerIP, _controllerPort, "SHUTDOWN");
            if (response != "FORWARDED")
            {
                MessageBox.Show("Shutdown command failed: " + response);
            }
            else
            {
                _isStopping = true;
                btnStop.Text = "Stopping";
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

                var process = new Process();
                process.StartInfo = processStartInfo;

                process.Start();
                //string output = process.StandardOutput.ReadToEnd();
                //string error = process.StandardError.ReadToEnd();
                              

                //process.WaitForExit();
            } catch (Exception ex)
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

      

    }
}

