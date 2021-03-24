using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ControllerService;
using FDA;
using Microsoft.Extensions.Logging;

namespace ControllerService
{
    class OMPassthough : IDisposable
    {
        TcpClient OMClient;
        int _fdaPort;
        int _clientPort;
        string _FDAAddress = "127.0.0.1";

        TCPServer OMServer;
        BackgroundWorker worker;
        ILogger<Worker> _logger;

        public OMPassthough(int fdaPort,int clientPort, ILogger<Worker> logger)
        {
            _logger = logger;
            _fdaPort = fdaPort;
            _clientPort = clientPort;
        }

        public void Start()
        {
            worker = new BackgroundWorker();
            worker.DoWork += Worker_DoWork;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.WorkerSupportsCancellation = true;

            worker.RunWorkerAsync();
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            OMClient?.Dispose();
            OMServer?.Dispose();
        }
        public void Dispose()
        {
            worker.CancelAsync();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            int readsize;
            byte[] data;
            byte[] buffer = new byte[1048576]; // 1 MB input buffer for operational messages
            string messages;
            string logmessage = "";
            Stopwatch quietTimer = new Stopwatch();
            TimeSpan quietLimit = new TimeSpan(0, 0, 5); // 5 seconds
            bool FDAConnectionHalfOpen = false;

            OMClient = new TcpClient();                      // FDA operational messages port    
            OMClient.ReceiveTimeout = 1; 

            OMServer = TCPServer.NewTCPServer(_clientPort);  // server for external clients who wish to receive operational messages
            OMServer.ClientConnected += OMServer_ClientConnected;
            OMServer.ClientDisconnected += OMServer_ClientDisconnected;
            OMServer.WelcomeMessage = "Connected to FDAController operational messages passthrough port\r\n";
            OMServer.Start();
            _logger.LogInformation("Listening for client connections on port " + _clientPort);

            quietTimer.Start();
            while (!e.Cancel)
            {
                // if the FDA has been quiet for too long, check if its still there
                if (OMClient.Connected && quietTimer.Elapsed > quietLimit)
                {
                    try
                    {
                        _logger.LogInformation("FDA has been quiet for a while, checking to see if it's still alive");
                        OMClient.GetStream().Write(Encoding.UTF8.GetBytes("hey you there?"));
                        int respSize = OMClient.GetStream().Read(buffer, 0, 10);
                        /*
                        byte[] response = new byte[respSize];
                        Array.Copy(buffer, response, respSize);
                        if (Encoding.UTF8.GetString(response) != "yes")
                        {
                            FDAConnectionHalfOpen = true;
                        }
                        */
                    } catch
                    {
                        FDAConnectionHalfOpen = true;
                    }

                    if (!FDAConnectionHalfOpen)
                    {
                        _logger.LogInformation("FDA is still alive");
                        quietTimer.Restart();
                    }
                    else
                    {
                        _logger.LogInformation("FDA seems to be dead, going into re-connection mode");
                    }

                }

                // is the client connected to the FDA? go into a connection loop if not
                if (!OMClient.Connected || FDAConnectionHalfOpen)
                {
                    while (!e.Cancel && !OMClient.Connected)
                    {
                        OMClient?.Dispose();
                        OMClient = new TcpClient();
                        logmessage = "";
                        while (!OMClient.Connected && !e.Cancel)
                        {
                            try
                            {
                                logmessage = "Attempting to connect to FDA on port " + _fdaPort + "...";
                                OMClient.Connect("127.0.0.1", _fdaPort);
                            }
                            catch
                            {
                                logmessage += "Failed to connect";
                                _logger.LogInformation(logmessage);
                            }
                            Thread.Sleep(3000);
                        }

                        if (e.Cancel)
                            break;

                        if (OMClient.Connected)
                        {
                            FDAConnectionHalfOpen = false;
                            quietTimer.Restart();
                            logmessage += "success";
                            _logger.LogInformation(logmessage);
                        }
                    }
                }
                else
                {
                    logmessage = "";
                    // we're connected to the FDA, check for available data
                    if (OMClient.GetStream().DataAvailable)
                    {
                        quietTimer.Restart();
                        logmessage = "OpMsg Data received from FDA";
                        // read the data from the FDA stream
                        readsize = OMClient.GetStream().Read(buffer, 0, buffer.Length);                       
                        data = new byte[readsize];                        
                        Array.Copy(buffer, data, readsize);
                        messages = Encoding.UTF8.GetString(data);

                        // if any clients are connected to the OM server port, forward the message to them
                        if (OMServer.ClientCount > 0)
                        {
                            logmessage += ", " + OMServer.ClientCount + " client(s) connected, forwarding the data";
                            if (Environment.OSVersion.Platform == PlatformID.Unix)
                            {
                                messages = messages.Replace("\n", "\r\n");
                            }
                           
                            OMServer.Send(Guid.Empty, messages);
                        }
                        else
                        {
                            logmessage += ", no clients connected so I'm dropping this data";
                        }

                    }
                }
                if (logmessage != "")
                    _logger.LogInformation(logmessage);
                Thread.Sleep(50);
            }
            
        }

        private void OMServer_ClientDisconnected(object sender, TCPServer.ClientEventArgs e)
        {
            _logger.LogInformation("Client disconnected from port " + _clientPort);
        }

        private void OMServer_ClientConnected(object sender, TCPServer.ClientEventArgs e)
        {
            _logger.LogInformation("Client connected on port " + _clientPort);
        }
    }
}
