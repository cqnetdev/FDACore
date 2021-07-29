using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ControllerService
{
    class BasicServicesClient : IDisposable
    {
        private readonly ILogger<Worker> _logger;
        private TcpClient _FDA;
        NetworkStream FDAstream;

        private readonly int _port;
        private readonly Queue<string> _sendQueue;
        public int FDAQueueCount;
        public string FDAMode = "";
        public string FDAVersion = "";
        public string FDADBType = "";
        public string FDAUpTime = "";
        public Common.FDAStatus Status = null;

        public bool FDAConnected { get { if (_FDA == null) return false; else return _FDA.Connected;} }

        private readonly BackgroundWorker _bgWorker;

        public BasicServicesClient(int port,ILogger<Worker> logger)
        {
            _logger = logger;
            _FDA = new TcpClient();
            _port = port;
            _sendQueue = new Queue<string>();

            FDAQueueCount = -1;

            _bgWorker = new BackgroundWorker
            {
                WorkerSupportsCancellation = true
            };
            _bgWorker.DoWork += BGWorker_DoWork;
            _bgWorker.RunWorkerCompleted += BGWorker_RunWorkerCompleted;
        }

        public void Send(string message)
        {
            lock (_sendQueue)
            {
                _sendQueue.Enqueue(message);
            }
        }

        private void BGWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _FDA?.Dispose();
        }

        private void BGWorker_DoWork(object sender, DoWorkEventArgs e)
        {

            Stopwatch stopwatch = new();
            string message;
            string responseStr;


            stopwatch.Start();
            while (!e.Cancel)
            {
                // if not connected, try to connect until successful
                if (!_FDA.Connected)
                {
                    Status = new Common.FDAStatus(); // default FDA status (runtime = 0:00:00, all other fields null)                  
                    Connect(e);
                }
              
                // we're connected now

                // send any pending requests
                while (_sendQueue.Count > 0)
                {
                    message = _sendQueue.Dequeue();
                    responseStr = DoTransaction(message);
                    _logger.LogInformation("Sending '" + message + "' to the FDA...response = '" + responseStr + "'");
                }

                // get a status update from the FDA
                if (stopwatch.ElapsedMilliseconds >= 3000)
                {
                    responseStr = DoTransaction("STATUS\0");

                    if (responseStr != "")
                        Status = Common.FDAStatus.Deserialize(responseStr);
                    else
                        Status = new Common.FDAStatus(); // no response from FDA, set the current status to the default status


                    stopwatch.Reset();
                    stopwatch.Start();
                }
                
                Thread.Sleep(100);
            }
        }

        private string DoTransaction(string message)
        {
            byte[] commandBytes;
            byte[] response;
            byte[] buffer = new byte[1000];
            int readsize;
            string responseStr;

            if (!_FDA.Connected)
                return "";

            try
            {
                // send the message
                commandBytes = Encoding.UTF8.GetBytes(message);
                FDAstream.Write(commandBytes, 0, commandBytes.Length);

                WaitForResponse(FDAstream, 2000);

                //read the response
                if (FDAstream.DataAvailable)
                {
                    readsize = FDAstream.Read(buffer, 0, buffer.Length);
                    response = new byte[readsize];
                    Array.Copy(buffer, response, readsize);
                    responseStr = Encoding.UTF8.GetString(response);
                }
                else
                    return ""; // no response

            } catch (Exception ex)
            {
                return "error: " + ex.Message;
            }

            return responseStr;
        }

        private static void WaitForResponse(NetworkStream stream,int limit)
        {
            int wait = 0;
            while (!stream.DataAvailable && wait < limit)
            {
                Thread.Sleep(50);
                wait += 50;
            }
        }

        private void Connect(DoWorkEventArgs e)
        {
            _FDA?.Dispose();
            _FDA = new TcpClient();
            string logmessage = "";
            while (!_FDA.Connected && !e.Cancel)
            {
                try
                {
                    logmessage = "Attempting to connect to FDA on port " + _port + "...";
                    _FDA.Connect("127.0.0.1", _port);
                }
                catch
                {
                    logmessage += "Failed to connect";
                    _logger.LogInformation(logmessage);
                }
                Thread.Sleep(3000);
            }

            if (_FDA.Connected)
            {
                FDAstream = _FDA.GetStream();
                logmessage += "success";
                _logger.LogInformation(logmessage);
            }
        }

        public void Start()
        {
            _bgWorker.RunWorkerAsync();

        }

        public void Dispose()
        {
            _bgWorker.CancelAsync();      
        }

    }
}
