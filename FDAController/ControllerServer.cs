using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;


namespace FDAController
{
  

    class ControllerServer : IDisposable
    {
        private static FDA.TCPServer _tcpServer;

        public ControllerServer(int port)
        {
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

            Console.WriteLine("Received command '" + command + "'");

            command = command.ToUpper();

            switch (command)
            {
                case "START":
                    Console.WriteLine("Start command received, starting FDA");
                    StartFDA();
                    Console.WriteLine("Replying 'OK' to requestor");
                    _tcpServer.Send(e.ClientID, "OK");
                    break;
                case "PING":
                    Console.WriteLine("replying with 'UP'");
                    _tcpServer.Send(e.ClientID, "UP"); // yes, I'm here
                    break;
                case "TOTALQUEUECOUNT":
                    string count = Globals.FDAClient.FDAQueueCount.ToString();
                    Console.WriteLine("Returning the total queue count (" + count + ") to the requestor");
                    _tcpServer.Send(e.ClientID, count);  // return the last known queue count to the requestor
                    break;
                default:
                    Console.WriteLine("Forwarding command '" + command + "' to the FDA");
                    Globals.FDAClient.Send(command); // forward all other messages to the FDA
                    _tcpServer.Send(e.ClientID, "FORWARDED"); // reply  back to the requestor that the command was forwarded to the FDA
                    break;
            }

        }

        private static void StartFDA()
        {
            //_FDAPath = _config["FDAPath"];
            //Console.WriteLine("Starting FDA (" + _FDAPath + ")");

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // TO DO
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
