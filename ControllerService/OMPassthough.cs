using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            string dataStr;

            OMClient = new TcpClient();                      // FDA operational messages port    
            
            OMServer = TCPServer.NewTCPServer(_clientPort);  // server for external clients who wish to receive operational messages
            OMServer.ClientConnected += OMServer_ClientConnected;
            OMServer.ClientDisconnected += OMServer_ClientDisconnected;
            OMServer.Start();
            _logger.LogInformation("Listening for client connections on port " + _clientPort);

            while (!e.Cancel)
            {
                // is the client connected to the FDA? go into a connection loop if not
                if (!OMClient.Connected)
                {
                    while (!e.Cancel && !OMClient.Connected)
                    {
                        OMClient?.Dispose();
                        OMClient = new TcpClient();
                        string logmessage = "";
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
                            logmessage += "success";
                            _logger.LogInformation(logmessage);
                        }
                    }
                }
                else
                {
                    // we're connected to the FDA, check for available data
                    if (OMClient.GetStream().DataAvailable)
                    {
                        // read the data from the FDA stream
                        readsize = OMClient.GetStream().Read(buffer, 0, buffer.Length);
                        data = new byte[readsize];
                        Array.Copy(buffer, data, readsize);
                        dataStr = Encoding.UTF8.GetString(data);

                        // if any clients are connected to the OM server port, forward the message to them
                        if (OMServer.ClientCount > 0)
                        {
                            OMServer.Send(Guid.Empty, dataStr);
                        }
                    }
                }
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
