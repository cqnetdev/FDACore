using FDA;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Text;

namespace ControllerService
{
    internal class BasicServicesServer : IDisposable
    {
        private static TCPServer _basicServicesPort;
        private static ILogger<Worker> _logger;

        public BasicServicesServer(int port, ILogger<Worker> logger)
        {
            _logger = logger;
            _basicServicesPort = TCPServer.NewTCPServer(port);
            _basicServicesPort.DataAvailable += BasicServices_DataAvailable;
        }

        public static void Start()
        {
            _basicServicesPort.Start();
        }

        private static void BasicServices_DataAvailable(object sender, FDA.TCPServer.TCPCommandEventArgs e)
        {
            string command = Encoding.UTF8.GetString(e.Data);

            // if the command is null terminated, remove the null so that the command is recognized in the switch statement below
            if (e.Data.Length > 0)
            {
                if (e.Data[e.Data.Length - 1] == 0)
                {
                    command = Encoding.UTF8.GetString(e.Data, 0, e.Data.Length - 1);
                }
            }

            //_logger.LogInformation("Received command '" + command + "'", new object[] { });

            command = command.ToUpper();

            switch (command)
            {
                case "START":
                    _logger.LogInformation("Start command received, starting FDA", Array.Empty<object>());
                    StartFDA();
                    //_logger.LogInformation("Replying 'OK' to requestor", new object[] { });
                    _basicServicesPort.Send(e.ClientID, "OK");
                    break;

                case "PING":
                    //_logger.LogInformation("replying with 'UP'", new object[] { });
                    _basicServicesPort.Send(e.ClientID, "UP"); // yes, I'm here
                    break;

                case "FDASTATUS":
                    string jsonStatus = ControllerGlobals.BasicServicesClient.Status.JSON();
                    _basicServicesPort.Send(e.ClientID, jsonStatus);
                    break;

                default:
                    //_logger.LogInformation("Forwarding command '" + command + "' to the FDA", new object[] { });
                    if (ControllerGlobals.BasicServicesClient.FDAConnected)
                    {
                        ControllerGlobals.BasicServicesClient.Send(command); // forward all other messages to the FDA (Eg. SHUTDOWN command)
                        _basicServicesPort.Send(e.ClientID, "FORWARDED"); // reply  back to the requestor that the command was forwarded to the FDA
                    }
                    break;
            }
        }

        private static void StartFDA()
        {
            if (OperatingSystem.IsWindows())
            {
                RunConsoleCommand("FDACore.exe", "", "C:\\IntricateFDA\\FDACore\\");
            }

            if (OperatingSystem.IsLinux())
            {
                //string result = RunTerminalCommand("gnome-terminal", "-- bash -c '" + _FDAPath + "'; exec bash");
                RunConsoleCommand("systemctl", "start fda");
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

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            process.Start();
            //string output = process.StandardOutput.ReadToEnd();
            //string error = process.StandardError.ReadToEnd();

            //process.WaitForExit();

            return;

            //if (string.IsNullOrEmpty(error)) { return output; }
            //else { return error; }
        }

        public void Dispose()
        {
            _basicServicesPort.Dispose();
        }
    }
}