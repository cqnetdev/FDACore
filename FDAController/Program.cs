using System;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace FDAController
{

    class Program
    {
        // private static FDA.TCPServer _tcpServer;
        // private static TcpClient _FDAtcp;
        // private static string _FDAPath;
        // private static IConfiguration _config;
        // private static int _currentQueueCount = -1;
        // private static bool _busy = false;
        private static bool _stopService = false; // for future use, on service stop, a utility will connect to this service and issue the stop command

        static void Main(string[] args)
        {
            // check for root account (required for starting the FDA service)            
            if (Environment.UserName != "root")
            {
                Console.WriteLine("FDAController service is running under the account '" + Environment.UserName + "'. Service must run as root to be able to start the FDA service");
            }

            Globals.Server = new ControllerServer(9571);
            Globals.Server.Start();


            Globals.FDAClient = new FDAClient(9572);
            Globals.FDAClient.Start();


            while (!_stopService)
            {              
                Thread.Sleep(3000);
            }

            Globals.Server.Dispose();
            Globals.FDAClient.Dispose();
        }

      


        /*
        private static void SendCommandToFDA(string command)
        {
            while (!_busy)
            {
                Thread.Sleep(50);
            }

            _busy = true;

            if (!_FDAtcp.Connected)
                ConnectToFDA("Send Command");

            try
            {
                if (_FDAtcp.Connected)
                {
                    using (NetworkStream stream = _FDAtcp.GetStream())
                    {
                        byte[] toSend = Encoding.UTF8.GetBytes(command);
                        stream.Write(toSend, 0, toSend.Length);
                        Console.WriteLine("Command sent to the FDA");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            _busy = false;
        }
        

        private static string GetFDAQueueCount()
        {
          
            string command = "TOTALQUEUECOUNT\0";
            byte[] response = new byte[0];
    
            try
            {
                if (_FDAtcp.Connected)
                {
                    using (NetworkStream stream = _FDAtcp.GetStream())
                    {
                        stream.ReadTimeout = 1000;
                        if (stream.CanWrite)
                        {
                            byte[] toSend = Encoding.UTF8.GetBytes(command);
                            stream.Write(toSend, 0, toSend.Length);
                        }

                        byte[] buffer = new byte[1000];
                        int readsize = stream.Read(buffer, 0, 1000);
                        response = new byte[readsize];
                        Array.Copy(buffer, response, readsize);
                    }
                }
                else
                    return "failed to connect";
         
            } catch (Exception ex)
            {
                return ex.Message;
            }

            return Encoding.UTF8.GetString(response);
        }
        */

    }


    
}
