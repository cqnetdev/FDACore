using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ControllerService
{
    class BasicServicesClient : IDisposable
    {
        private ILogger<Worker> _logger;
        private TcpClient _FDA;
        NetworkStream FDAstream;

        private int _port;
        private Queue<string> _sendQueue;
        public int FDAQueueCount;
        public string FDAMode = "";

        private BackgroundWorker _bgWorker;

        public BasicServicesClient(int port,ILogger<Worker> logger)
        {
            _logger = logger;
            _FDA = new TcpClient();
            _port = port;
            _sendQueue = new Queue<string>();

            FDAQueueCount = -1;

            _bgWorker = new BackgroundWorker();
            _bgWorker.WorkerSupportsCancellation = true;
            _bgWorker.DoWork += _bgWorker_DoWork;
            _bgWorker.RunWorkerCompleted += _bgWorker_RunWorkerCompleted;
        }

        public void Send(string message)
        {
            lock (_sendQueue)
            {
                _sendQueue.Enqueue(message);
            }
        }

        private void _bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _FDA?.Dispose();
        }

        private void _bgWorker_DoWork(object sender, DoWorkEventArgs e)
        {

            Stopwatch stopwatch = new Stopwatch();
            string message;
            string responseStr;
            int qCount;


            stopwatch.Start();
            while (!e.Cancel)
            {
                if (!_FDA.Connected)
                {
                    FDAMode = "";
                    Connect(e);
                }
              

                while (_sendQueue.Count > 0)
                {
                    message = _sendQueue.Dequeue();
                    responseStr = DoTransaction(message);
                    _logger.LogInformation("Sending '" + message + "' to the FDA...response = '" + responseStr + "'");
                }

                if (stopwatch.ElapsedMilliseconds >= 3000)
                {              
                    // Get the current queue count from the FDA
                    responseStr = DoTransaction("TOTALQUEUECOUNT\0");
                    _logger.LogInformation("Total Queue count received: " + responseStr);
                    if (int.TryParse(responseStr, out qCount))
                    {
                        FDAQueueCount = qCount;
                    }

                    // get the FDA run mode if we don't already know it (only need to get this once)
                    if (FDAMode == "")
                    {
                        FDAMode = DoTransaction("RUNMODE\0");
                        _logger.LogInformation("Run mode received: " + FDAMode);
                    }

                    stopwatch.Reset();
                    stopwatch.Start();
                }
                
                Thread.Sleep(250);
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
                return "not connected";

            try
            {
                // send the message
                FDAstream.Flush();
                commandBytes = Encoding.UTF8.GetBytes(message);
                FDAstream.Write(commandBytes, 0, commandBytes.Length);

                //read the response
                readsize = FDAstream.Read(buffer, 0, buffer.Length);
                response = new byte[readsize];
                Array.Copy(buffer, response, readsize);
                responseStr = Encoding.UTF8.GetString(response);
            } catch (Exception ex)
            {
                return "error: " + ex.Message;
            }

            return responseStr;
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
