using Common;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FDA
{
    public class ConnectionWrapper : IDisposable
    {
        private TcpClient _tcpClient;
        private SerialPort _serialPort;
        private UdpClient _UDPClient;
        private readonly int _UDPPort;
        private IPEndPoint _receivedFrom;

        private readonly string _connType = "";
        private readonly string _connectionID;

        public bool DataAvailable { get { return CheckIfDataAvailable(); } }
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }

        public bool Connected { get { return IsConnected(); } }

        #region interface methods

        public ConnectionWrapper(TcpClient client, string connectionID)
        {
            _tcpClient = client;
            _connType = "TCP";
            _connectionID = connectionID;
        }

        public ConnectionWrapper(SerialPort serialPort, string connectionID)
        {
            _serialPort = serialPort;
            _connType = "Serial";
            _connectionID = connectionID;
        }

        public ConnectionWrapper(UdpClient client, int port, string connectionID)
        {
            _connectionID = connectionID;
            _UDPClient = client;
            _UDPPort = port;
            _receivedFrom = new IPEndPoint(IPAddress.Any, 0);
            _connType = "UDP";
        }

        private bool IsConnected()
        {
            bool connected = false;
            switch (_connType.ToUpper())
            {
                case "UDP": return _UDPClient.Client.IsBound;
                case "SERIAL": return _serialPort.IsOpen;
            }
            return connected;
        }

        public bool Flush()
        {
            bool flushResult = true;
            switch (_connType.ToUpper())
            {
                case "TCP":
                    _tcpClient.GetStream().Flush(); // this doesn't seem to work

                    // so we'll do it ourselves
                    byte[] garbagePail = new byte[1000];
                    int garbageCount;
                    MemoryStream garbageBin = new();

                    Stopwatch stopwatch = new();
                    stopwatch.Start();
                    while (_tcpClient.GetStream().DataAvailable && garbageBin.Length < 2000 && stopwatch.ElapsedMilliseconds < 1000)
                    {
                        garbageCount = _tcpClient.GetStream().Read(garbagePail, 0, 1000);
                        garbageBin.Write(garbagePail, 0, garbageCount);
                    }
                    stopwatch.Stop();

                    if (garbageBin.Length >= 2000 || stopwatch.ElapsedMilliseconds >= 1000)
                    {
                        flushResult = false;
                    }

                    if (garbageBin.Length > 0 && garbageBin.Length < 2000 && stopwatch.ElapsedMilliseconds < 1000)
                    {
                        Globals.SystemManager.LogCommsEvent(Guid.Parse(_connectionID), Globals.FDANow(), "Connection " + _connectionID + ": Pre - transmit recieve buffer flushing removed " + garbageBin.Length + " bytes of unexpected data: " + BitConverter.ToString(garbageBin.ToArray()));
                    }

                    garbageBin.SetLength(0);

                    break;

                case "SERIAL":
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                    break;

                case "UDP":
                    if (_UDPClient.Available > 0)
                    {
                        int totalRemoved = 0;
                        Stopwatch timer = new();
                        byte[] buffer;
                        IPEndPoint receivedFrom = new(IPAddress.Any, 0);
                        timer.Start();
                        while (_UDPClient.Available > 0 && timer.ElapsedMilliseconds < 500)
                        {
                            buffer = _UDPClient.Receive(ref receivedFrom);
                            totalRemoved += buffer.Length;
                            Thread.Sleep(20);
                        }
                        timer.Stop();

                        if (totalRemoved > 0)
                        {
                            Globals.SystemManager.LogCommsEvent(Guid.Parse(_connectionID), Globals.FDANow(), "Connection " + _connectionID + ": Pre - transmit recieve buffer flushing removed " + totalRemoved + " bytes of unexpected data");
                            Globals.SystemManager.LogApplicationEvent(DateTime.Now, "Connection Wrapper - Flush()", "Connection " + _connectionID + ": Pre - transmit recieve buffer flushing removed " + totalRemoved + " bytes of unexpected data");
                        }

                        if (timer.ElapsedMilliseconds >= 1000)
                            flushResult = false;
                    }
                    break;
            }

            return flushResult;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;

            switch (_connType.ToUpper())
            {
                case "TCP":
                    bytesRead = _tcpClient.GetStream().Read(buffer, offset, count);
                    break;

                case "SERIAL":
                    bytesRead = _serialPort.Read(buffer, offset, count);
                    break;

                case "UDP":
                    byte[] temp;
                    temp = _UDPClient.Receive(ref _receivedFrom);
                    Array.Copy(temp, buffer, temp.Length);
                    bytesRead = temp.Length;
                    break;
            }

            return bytesRead;
        }

        // SetDestination only applies to UDP connections, all others do nothing and return success (true)
        public bool SetDestination(string ipAddress)
        {
            if (_connType.ToUpper() == "UDP")
            {
                if (!IPAddress.TryParse(ipAddress, out IPAddress addr))
                    return false;

                string currentEndpoint = String.Empty;
                if (_UDPClient.Client.RemoteEndPoint != null)
                {
                    currentEndpoint = _UDPClient.Client.RemoteEndPoint.ToString();
                }

                if (currentEndpoint != ipAddress + ":" + _UDPPort)  // if it's the same endpoint we're already connected to, just leave it, otherwise disconnect and connect to the new endpoint
                {
                    _UDPClient.Close();
                    _UDPClient = new UdpClient(_UDPPort);
                    _UDPClient.Connect(new IPEndPoint(addr, _UDPPort));
                }
            }

            return true;
        }

        public bool Write(byte[] buffer, int offset, int count)
        {
            switch (_connType.ToUpper())
            {
                case "TCP":
                    if (_tcpClient.Connected)
                    {
                        _tcpClient.GetStream().Write(buffer, offset, count);
                        return true;
                    }
                    break;

                case "SERIAL":
                    _serialPort.Write(buffer, offset, count);
                    return true;

                case "UDP":
                    _UDPClient.Send(buffer, buffer.Length);
                    return true;
            }
            return false;
        }

        public void Close()
        {
            switch (_connType.ToUpper())
            {
                case "TCP":
                    _tcpClient?.Close();
                    break;

                case "SERIAL":
                    _serialPort?.Close();
                    break;

                case "UDP":
                    _UDPClient?.Close();
                    break;
            }
        }

        #endregion interface methods

        #region internal methods

        private bool CheckIfDataAvailable()
        {
            switch (_connType.ToUpper())
            {
                case "TCP":
                    return _tcpClient.GetStream().DataAvailable;

                case "SERIAL":
                    return (_serialPort.BytesToRead > 0);

                case "UDP":
                    return _UDPClient.Available > 0;

                default: return false;
            }
        }

        #endregion internal methods

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _tcpClient?.Dispose();
                    _tcpClient = null;
                    _serialPort?.Dispose();
                    _serialPort = null;
                    _UDPClient?.Dispose();
                    _UDPClient = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Connection() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}