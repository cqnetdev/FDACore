using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FDAController
{
    public partial class frmMain : Form
    {

        private int _controllerPort = 9571;
        private string _controllerIP = "127.0.0.1";
        private bool _isStopping = false;
        private bool _isStarting = false;
        private Timer timer;

        public frmMain()
        {
            InitializeComponent();
            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            string result;
            string status;

            // is the controller service running?
            if (ProcessRunning("ControllerService"))
            {
                status = "FDA Controller service is running";
              

                // is the controller responsive to requests?
                result = SendRequest(_controllerIP, _controllerPort, "PING");
                if (result == "UP")
                {
                    status += " and responsive";
                }
                else
                {
                    status += " but is not responsive";
                }

            }
            else
            {
                status = "FDA Controller service is not running";
                btnStop.Enabled = false;
                btnStart.Enabled = false;
            }
            lblControllerService.Text = status;

            // is the FDA running?
            if (ProcessRunning("FDACore"))
            {
                _isStarting = false;
                status = "FDA service is running";
                if (!_isStopping)
                {
                    btnStop.Enabled = true;
                }
                btnStart.Enabled = false;
                btnStart.Text = "Start";

                // ask the FDA controller for the FDA's current queue count (serves as an 'FDA is responsive' test)
                result = SendRequest(_controllerIP, _controllerPort, "TOTALQUEUECOUNT");
                int count;
                if (int.TryParse(result, out count))
                {
                    status += ", " + count + " items in the communications queues";
                }
                else
                {
                    status += ", unknown number of items in the communications queues";
                }
            }
            else
            {
                status = "The FDA is not running";
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
                }
                
            }
            lblFDA.Text = status;


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
                } catch (Exception ex)
                {
                    return "failed to connect to FDA";
                }

                // send the request
                try
                {
                    byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                    client.GetStream().Write(requestBytes, 0, requestBytes.Length);
                } catch (Exception ex)
                {
                    return "failed to send the request to the FDA";
                }

                bytesRead = client.GetStream().Read(buffer, 0, buffer.Length);
                client.Close();

            }

            response = new byte[bytesRead];
            Array.Copy(buffer, response, bytesRead);

            return Encoding.UTF8.GetString(response);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {

            // instead of sending the start command to the FDAController like we do in linux (because the FDA is a service)
            // we can just run the FDA directly from here
            _isStarting = true;
            btnStart.Enabled = false;
            RunConsoleCommand("FDACore.exe","","C:\\FDA\\");


            /*
            string response = SendRequest(_controllerIP, _controllerPort, "start");
            if (response != "OK")
            {
                MessageBox.Show("Failed to start the FDA: " + response);
            }
            */
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
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

