using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;


namespace ControllerService
{
  

    class ControllerServer : IDisposable
    {
        private static FDA.TCPServer _tcpServer;
        private static ILogger<Worker> _logger;

        public ControllerServer(int port,ILogger<Worker> logger)
        {
            _logger = logger;
            _tcpServer = FDA.TCPServer.NewTCPServer(port);
            _tcpServer.DataAvailable += _tcpServer_DataAvailable;
        }

        public void Start()
        {
            _tcpServer.Start();
        }

        private static void _tcpServer_DataAvailable(object sender, FDA.TCPServer.TCPCommandEventArgs e)
        {

            string receivedMsg = Encoding.UTF8.GetString(e.Data);
            string receivedHex = BitConverter.ToString(e.Data);

            string command = receivedMsg;


            // if the command is null terminated, remove the null so that the command is recognized in the switch statement below
            if (e.Data[e.Data.Length - 1] == 0)
            {
                command = Encoding.UTF8.GetString(e.Data, 0, e.Data.Length - 1);
            }

            _logger.LogInformation("Received command '" + command + "'", new object[] { });

            command = command.ToUpper();

            switch (command)
            {
                case "START":
                    _logger.LogInformation("Start command received, starting FDA", new object[] { });
                    StartFDA();
                    _logger.LogInformation("Replying 'OK' to requestor", new object[] { });
                    _tcpServer.Send(e.ClientID, "OK");
                    break;
                case "PING":
                    _logger.LogInformation("replying with 'UP'", new object[] { });
                    _tcpServer.Send(e.ClientID, "UP"); // yes, I'm here
                    break;
                case "TOTALQUEUECOUNT":
                    string count = Globals.FDAClient.FDAQueueCount.ToString();
                    _logger.LogInformation("Returning the total queue count (" + count + ") to the requestor", new object[] { });
                    _tcpServer.Send(e.ClientID, count);  // return the last known queue count to the requestor
                    break;
                default:
                    _logger.LogInformation("Forwarding command '" + command + "' to the FDA", new object[] { });
                    Globals.FDAClient.Send(command); // forward all other messages to the FDA
                    _tcpServer.Send(e.ClientID, "FORWARDED"); // reply  back to the requestor that the command was forwarded to the FDA
                    break;
            }

        }

        private static void StartFDA()
        {


            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                RunConsoleCommand("FDACore.exe", "", "c:\\FDA\\");
            }

            if (Environment.OSVersion.Platform == PlatformID.Unix)
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

        public void Dispose()
        {
            _tcpServer.Dispose();
        }
    }
}
