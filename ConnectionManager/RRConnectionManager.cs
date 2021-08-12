using BSAP;
using Common;
using Modbus;
using ROC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FDA
{
    /// <summary>
    /// Manages communications for protcols that follow the Request-Respose communication pattern
    /// </summary>
    public class RRConnectionManager : SubscriptionManager.SubscribeableObject, IDisposable//, INotifyPropertyChanged
    {
        #region private properties/objects

        // currently unused member variables
        /*
        private string _name;
        private string _userNotes;

        private byte[] _testPacket;
        private short _deviceConnectionTestPacketSchedule; // minutes
        private DateTime _connectDateTime;
        private DateTime _disconnectDateTime;
        private Byte _disconnectType; // 0 = local, 1 = remote
        private bool _preConnectionPing = false;
        private short _preConnectionPingTimeout = 1000; // milliseconds
        */

        private DateTime initTime;
        private Stopwatch runTime;

        private ConnType _connectionType;
        private Guid _connectionID;
        private DateTime _lastCommsDateTime;
        private int _requestRetryDelay = 0; // milliseconds
        private int _socketConnectionAttemptTimeout = 1000;     // milliseconds
        private int _maxSocketConnectionAttempts = 3;              // seconds
        private int _socketConnectionRetryDelay = 10;         // seconds
        private int _postConnectionCommsDelay = 0; // milliseconds
        private int _interRequestDelay = 0;      // milliseconds
        private int _maxRequestAttempts = 3;
        private int _requestResponseTimeout = 1000;      // milliseconds
        private Globals.ConnStatus _connectionStatus = Globals.ConnStatus.Disconnected;
        private bool _connectionEnabled = false;
        private bool _communicationsEnabled = false;
        private bool _idleDisconnect = false;
        private int _idleDisconnectTime = 30; // seconds
        private string _description;
        private BackgroundWorker _bgCommsWorker;
        private BackgroundWorker _bgCompletedTransHandler;
        private DateTime _lastLogTimestamp = DateTime.MinValue;
        private string _connDetails = String.Empty;

        private Queue<DataRequest> _completedTransQueue;

        //private BackgroundWorker _bgConnection;
        private ConnectionWrapper _stream;

        // serial connection parameters
        private SerialPort _serialPort;

        private string _serialPortName = "";
        private int _serialBaudRate = 2400;
        private Parity _serialParity = Parity.None;
        private int _serialDataBits = 8;
        private StopBits _serialStopBits = StopBits.One;
        private Handshake _serialHandshake = Handshake.None;

        // TCP connection parameters
        private TcpClient _tcpConnection;

        private string _host;
        private int _portNumber;
        private bool _LocalConnected;
        private bool _RemoteConnected;

        // private connection manager objects
        private Timer SocketConnectionRetryTimer;

        public QueueManager _queueManager;
        private Common.CircularBuffer<TimeSpan> _recentTimeMeasurements;
        private bool _commsLogEnabled;

        // queue counts
        private int _priority0Count;

        private int _priority1Count;
        private int _priority2Count;
        private int _priority3Count;

        private Timer _connectionIdleTimer;

        private DBManager _dbManager;

        //private delegate void RequeueHandler(object sender,EventArgs e);
        //private event RequeueHandler RequeueGroup;

        #endregion private properties/objects

        #region public properties

        public int InterRequestDelay { get => _interRequestDelay; set { if (value != _interRequestDelay) { _interRequestDelay = value; HandlePropertyChanged(); } } }
        public string RemoteIPAddress { get => _host; set { if (value != _host) { _host = value; HandlePropertyChanged(); } } }
        public int PortNumber { get => _portNumber; set { if (value != _portNumber) { _portNumber = value; HandlePropertyChanged(); } } }
        public int SocketConnectionAttemptTimeout { get => _socketConnectionAttemptTimeout; set { if (value != _socketConnectionAttemptTimeout) { _socketConnectionAttemptTimeout = value; HandlePropertyChanged(); } } }
        public int PostConnectionCommsDelay { get => _postConnectionCommsDelay; set { if (value != _postConnectionCommsDelay) { _postConnectionCommsDelay = value; HandlePropertyChanged(); } } }
        public int SocketConnectionRetryDelay { get => _socketConnectionRetryDelay; set { if (value != _socketConnectionRetryDelay) { _socketConnectionRetryDelay = value; HandlePropertyChanged(); } } }
        public Globals.ConnStatus ConnectionStatus { get => _connectionStatus; private set { if (_connectionStatus != value) { _connectionStatus = value; HandlePropertyChanged(); } } }
        public UInt16[] RequestCounts { get => _queueManager.GetQueueCounts(); }
        public bool ConnectionEnabled { get => _connectionEnabled; set { if (value != _connectionEnabled) { _connectionEnabled = value; HandlePropertyChanged(); } } }
        public bool CommunicationsEnabled { get => _communicationsEnabled; set { if (value != _communicationsEnabled) { _communicationsEnabled = value; HandlePropertyChanged(); } } }
        public bool TCPLocalConnected { get => _LocalConnected; set { if (value != _LocalConnected) { _LocalConnected = value; HandlePropertyChanged(); } } }
        public bool TCPRemoteConnected { get => _RemoteConnected; set { if (value != _RemoteConnected) { _RemoteConnected = value; HandlePropertyChanged(); } } }
        public int MaxRequestAttempts { get => _maxRequestAttempts; set { if (value != _maxRequestAttempts) { _maxRequestAttempts = value; HandlePropertyChanged(); } } }
        public int RequestResponseTimeout { get => _requestResponseTimeout; set { if (value != _requestResponseTimeout) { _requestResponseTimeout = value; HandlePropertyChanged(); } } }
        public int MaxSocketConnectionAttempts { get => _maxSocketConnectionAttempts; set { if (value != _maxSocketConnectionAttempts) { _maxSocketConnectionAttempts = value; HandlePropertyChanged(); } } }
        public Guid ConnectionID { get => _connectionID; set => _connectionID = value; }
        public string Description { get => _description; set { if (value != _description) { _description = value; HandlePropertyChanged(); } } }
        public int RequestRetryDelay { get => _requestRetryDelay; set { if (value != _requestRetryDelay) { _requestRetryDelay = value; HandlePropertyChanged(); } } }
        public bool CommsLogEnabled { get => _commsLogEnabled; set { if (value != _commsLogEnabled) { _commsLogEnabled = value; HandlePropertyChanged(); } } }
        public ConnType ConnectionType { get => _connectionType; set { if (value != _connectionType) { _connectionType = value; HandlePropertyChanged(); } } }
        public bool IdleDisconnect { get => _idleDisconnect; set { if (value != _idleDisconnect) { _idleDisconnect = value; HandlePropertyChanged(); } } }
        public int IdleDisconnectTime { get => _idleDisconnectTime; set { if (value != _idleDisconnectTime) { _idleDisconnectTime = value; HandlePropertyChanged(); } } }
        public int SerialBaudRate { get => _serialBaudRate; set { if (value != _serialBaudRate) { _serialBaudRate = value; HandlePropertyChanged(); } } }
        public Parity SerialParity { get => _serialParity; set { if (value != _serialParity) { _serialParity = value; HandlePropertyChanged(); } } }
        public int SerialDataBits { get => _serialDataBits; set { if (value != _serialDataBits) { _serialDataBits = value; HandlePropertyChanged(); } } }
        public StopBits SerialStopBits { get => _serialStopBits; set { if (value != _serialStopBits) { _serialStopBits = value; HandlePropertyChanged(); } } }
        public Handshake SerialHandshake { get => _serialHandshake; set { if (value != _serialHandshake) { _serialHandshake = value; HandlePropertyChanged(); } } }
        public string SerialPortName { get => _serialPortName; set { if (value != _serialPortName) { _serialPortName = value; HandlePropertyChanged(); } } }
        public int Priority0Count { get => _priority0Count; set { if (value != _priority0Count) { _priority0Count = value; HandlePropertyChanged(nameof(Priority0Count), Globals.FDANow().Ticks); } } }
        public int Priority1Count { get => _priority1Count; set { if (value != _priority1Count) { _priority1Count = value; HandlePropertyChanged(nameof(Priority1Count), Globals.FDANow().Ticks); } } }
        public int Priority2Count { get => _priority2Count; set { if (value != _priority2Count) { _priority2Count = value; HandlePropertyChanged(nameof(Priority2Count), Globals.FDANow().Ticks); } } }
        public int Priority3Count { get => _priority3Count; set { if (value != _priority3Count) { _priority3Count = value; HandlePropertyChanged(nameof(Priority3Count), Globals.FDANow().Ticks); } } }
        public DateTime LastCommsTime { get => _lastCommsDateTime; set { if (value != _lastCommsDateTime) { _lastCommsDateTime = value; HandlePropertyChanged(); } } }
        public string ConnDetails { get => _connDetails; set { if (value != _connDetails) { _connDetails = value; HandlePropertyChanged(); } } }

        public int TotalQueueCount { get => _queueManager.TotalQueueCount; }

        // public types/events/delegates/classes

        public enum ConnType { Serial, Ethernet, EthernetUDP };

        public delegate void RequestCompleteHandler(object sender, TransactionEventArgs e);

        public event RequestCompleteHandler TransactionComplete;

        //public event PropertyChangedEventHandler PropertyChanged;

        #endregion public properties

        #region constructors

        // constructor code that is common to TCP,UDP, and Serial connections
        private void CommonConstructor(Guid ID, string description)
        {
            base.ID = ID.ToString();
            base.ObjectType = "connection";

            // set all properties to be retained in MQTT except for the queue counts
            string[] MQTTRetainProperties = new string[]
            {
                "InterRequestDelay",
                "IPAddress",
                "PortNumber",
                "SocketConnectionAttemptTimeout",
                "PostConnectionCommsDelay",
                "SocketConnectionRetryDelay",
                "ConnectionStatus",
                "RequestCounts",
                "ConnectionEnabled",
                "CommunicationsEnabled",
                "TCPLocalConnected",
                "TCPRemoteConnected",
                "MaxRequestAttempts",
                "RequestResponseTimeout",
                "MaxSocketConnectionAttempts",
                "Description",
                "RequestRetryDelay",
                "CommsLogEnabled",
                "ConnectionType",
                "IdleDisconnect",
                "IdleDisconnectTime",
                "SerialBaudRate",
                "SerialParity",
                "SerialDataBits",
                "SerialStopBits",
                "SerialHandshake",
                "SerialPortName",
                "LastCommsTime",
                "ConnDetails"
            };

            string[] alwayspublishProperties = new string[]
            {
                "ConnectionEnabled",
                "CommunicationsEnabled",
                "ConnectionStatus",
                "Description"
            };

            base.SetMQTTAlwaysPublishProperties(alwayspublishProperties);
            base.SetMQTTRetainedProperties(MQTTRetainProperties);
            string[] MQTTIncludeTimestampProps = new string[]
            {
                "Priority0Count","Priority1Count","Priority2Count","Priority3Count"
            };

            base.SetMQTTIncludeTimestampProperties(MQTTIncludeTimestampProps);

            runTime = new Stopwatch();
            initTime = Globals.FDANow();
            runTime.Start();

            ConnectionStatus = Globals.ConnStatus.Disconnected;
            TCPLocalConnected = false;
            TCPRemoteConnected = false;
            _dbManager = (DBManager)Globals.DBManager;

            //this.PropertyChanged += PropertyChangedHandler;

            _description = description;
            _connectionID = ID;

            _bgCommsWorker = new BackgroundWorker
            {
                WorkerSupportsCancellation = true
            };
            _bgCommsWorker.RunWorkerCompleted += BgCommsWorker_RunWorkerCompleted;
            _bgCommsWorker.DoWork += BgCommsWorker_DoWork;

            _bgCompletedTransHandler = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true
            };

            _bgCompletedTransHandler.DoWork += BgCompletedTransHandler_DoWork;
            _completedTransQueue = new Queue<DataRequest>();

            // start the communication thread
            _bgCompletedTransHandler.RunWorkerAsync();

            //this.RequeueGroup += RequeueGroupHandler;

            _connectionIdleTimer = new Timer(ConnectionTimeoutHandler, null, _idleDisconnectTime * 1000, Timeout.Infinite);

            _queueManager = new QueueManager(this, 4);
            _queueManager.QueueActivated += QueueActivated;
            _queueManager.QueueEmpty += QueueEmpty;

            _recentTimeMeasurements = new CircularBuffer<TimeSpan>(5);
        }

        public Globals.ConnDetails GetConnSettings()
        {
            Globals.ConnDetails settings = new()
            {
                ID = ConnectionID.ToString(),
                Description = Description,
                ConnectionEnabled = ConnectionEnabled,
                CommunicationsEnabled = CommunicationsEnabled,
                ConnectionType = ConnectionType.ToString(),
                LastCommsTime = _lastCommsDateTime.Ticks,
                RequestRetryDelay = _requestRetryDelay,
                SocketConnectionAttemptTimeout = _socketConnectionAttemptTimeout,
                MaxSocketConnectionAttempts = _maxSocketConnectionAttempts,
                SocketConnectionRetryDelay = _socketConnectionRetryDelay,
                PostConnectionCommsDelay = _postConnectionCommsDelay,
                InterRequestDelay = _interRequestDelay,
                MaxRequestAttempts = _maxRequestAttempts,
                RequestResponseTimeout = RequestResponseTimeout,
                ConnectionStatus = _connectionStatus.ToString(),
                IdleDisconnect = _idleDisconnect,
                IdleDisconnectTime = _idleDisconnectTime
            };

            return settings;
        }

        private void BgCompletedTransHandler_DoWork(object sender, DoWorkEventArgs e)
        {
            DataRequest trans;
            while (!_bgCompletedTransHandler.CancellationPending || _completedTransQueue.Count > 0) // outer loop ensures that the thread keeps running if an exception is thrown and caught
            {
                try
                {
                    while (!_bgCompletedTransHandler.CancellationPending || _completedTransQueue.Count > 0)
                    {
                        if (_completedTransQueue.Count > 0)
                        {
                            lock (_completedTransQueue)
                            {
                                trans = _completedTransQueue.Dequeue();
                            }
                            TransactionComplete?.Invoke(this, new TransactionEventArgs(trans));
                            Thread.Sleep(20);
                        }
                        else
                            Thread.Sleep(500);
                    }
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(initTime.Add(runTime.Elapsed), ex, "Error occurred while processing a completed transaction");
                }
            }
        }

        private void RequeueGroup(RequestGroup group)
        {
            group.RequeueCount -= 1;
            group.ProtocolRequestList.Clear();
            LogCommsEvent(initTime.Add(runTime.Elapsed), "Request Group " + group.ID + " requeued. " + group.RequeueCount + " requeues remaining");
            Globals.SystemManager.LogApplicationEvent(this, this.ConnectionID.ToString(), "Request Group " + group.ID + " requeued. " + group.RequeueCount + " requeues remaining");
            _queueManager.QueueTransactionGroup(group);
        }

        private void ConnectionTimeoutHandler(object o)
        {
            if (!_bgCommsWorker.IsBusy && _idleDisconnect & ConnectionStatus == Globals.ConnStatus.Connected_Ready)
            {
                Globals.SystemManager.LogApplicationEvent(this, Description, "Connection maximum idle time of " + _idleDisconnectTime + " seconds reached, disconnecting");
                Disconnect();
            }
        }

        private void QueueEmpty(object sender, QueueManager.QueueCountEventArgs e)
        {
            LogCommsEvent(initTime.Add(runTime.Elapsed), "Connection: " + Description + " (" + ConnectionID + "), Queue " + e.QueueNumber + " empty");
        }

        private void QueueActivated(object sender, QueueManager.QueueCountEventArgs e)
        {
            if (e.ChangeFromZero)
                LogCommsEvent(initTime.Add(runTime.Elapsed), "Connection: " + Description + " (" + ConnectionID + "), Queue " + e.QueueNumber + " changed from empty to non-empty");

            if (!_bgCommsWorker.IsBusy)
            {
                if (ConnectionEnabled && CommunicationsEnabled && ConnectionStatus != Globals.ConnStatus.ConnectionRetry_Delay && Globals.FDAStatus == Globals.AppState.Normal)
                {
                    _bgCommsWorker.RunWorkerAsync();
                }
                else
                {
                    LogCommsEvent(initTime.Add(runTime.Elapsed), "Connection:" + Description + " (" + ConnectionID + "), Queue is not empty.  ");
                }
            }
        }

        // constructor for serial connections
        public RRConnectionManager(Guid ID, string description)  //,string ComPort,int baud,Parity parity,int dataBits,StopBits stopbits,Handshake handshake)
        {
            CommonConstructor(ID, description);

            //SerialPortName = ComPort;
            //SerialBaudRate = baud;
            //SerialParity = parity;
            //SerialDataBits = dataBits;
            //SerialStopBits = stopbits;
            //SerialHandshake = handshake;
            ConnectionType = ConnType.Serial;
        }

        // constructor for TCP or UDP connections
        public RRConnectionManager(Guid ID, string description, string host, int port, string protocol = "TCP")
        {
            CommonConstructor(ID, description);
            RemoteIPAddress = host;
            PortNumber = port;
            if (protocol == "TCP")
                ConnectionType = ConnType.Ethernet;
            if (protocol == "UDP")
                ConnectionType = ConnType.EthernetUDP;
        }

        #endregion constructors

        #region Events and EventHandlers

        private void HandlePropertyChanged([CallerMemberName] string propertyName = "", long timestamp = 0)
        {
            // raise a property changed event (for property changes that don't require special handling)
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            NotifyPropertyChanged(propertyName, timestamp);

            // take care of any actions needed as a result of the property changes
            switch (propertyName)
            {
                case "ConnectionEnabled": HandleConnectionEnabledStatusChange(ConnectionEnabled); break;
                case "CommunicationsEnabled": HandleCommsEnabledStatusChange(CommunicationsEnabled); break;
                case "RemoteIPAddress": ResetConnection(); break;
                case "PortNumber": ResetConnection(); break;
                case "ConnectionStatus": Globals.SystemManager.LogApplicationEvent(this, Description, " Connection status : " + ConnectionStatus.ToString()); break;
                    //case "ConnectionStatus": LogCommsEvent(Globals.GetOffsetUTC(),"Connection " + Description + ": source connection status = " + ConnectionStatus); break;
            }
            if (propertyName.StartsWith("Serial"))
                ResetConnection();
        }

        private void ResetConnection()
        {
            // disconnect
            if (ConnectionStatus == Globals.ConnStatus.Connected_Ready || ConnectionStatus == Globals.ConnStatus.Connected_Delayed)
                HandleConnectionEnabledStatusChange(false);

            if (ConnectionEnabled)
                HandleConnectionEnabledStatusChange(true);
        }

        private void HandleConnectionEnabledStatusChange(bool enabled)
        {
            if (enabled && ConnectionStatus != Globals.ConnStatus.ConnectionRetry_Delay)
            {
                if (!_bgCommsWorker.IsBusy && Globals.FDAStatus == Globals.AppState.Normal || Globals.FDAStatus == Globals.AppState.Starting)
                    _bgCommsWorker.RunWorkerAsync();
            }
            else
            {
                _queueManager.ClearQueues();

                if (_bgCommsWorker.IsBusy)
                    _bgCommsWorker.CancelAsync();

                TimeSpan timeLimit = TimeSpan.FromSeconds(90); // TimeSpan.FromMilliseconds(_socketConnectionAttemptTimeout * _maxSocketConnectionAttempts * 2);
                Stopwatch timeoutTimer = new();
                timeoutTimer.Start();

                // new Dec 17 - ensure that the threads have exited before disconnecting (avoid crashes due to connection becoming null in the middle of an operation)
                // allow up to twice the total amount of time required for a connection attempt for the threads to exit (should be plenty of time, but prevents an infinite loop in the event that something goes haywire)
                while (timeoutTimer.Elapsed < timeLimit && _bgCommsWorker.IsBusy)
                {
                    Thread.Sleep(50);
                }

                if (timeoutTimer.Elapsed >= timeLimit)
                    Globals.SystemManager.LogApplicationEvent(this, "", "Timeout while waiting for comms thread to exit");

                timeoutTimer.Stop();
                Disconnect();
            }
        }

        private void HandleCommsEnabledStatusChange(bool enabled)
        {
            if (!enabled)
            {
                if (_bgCommsWorker.IsBusy)
                    _bgCommsWorker.CancelAsync();

                //if (_bgConnection.IsBusy)
                //    _bgConnection.CancelAsync();

                // new Dec 17 - ensure that the threads have exited before disconnecting (avoid crashes due to connection becoming null in the middle of an operation)
                // allow up to twice the total amount of time required for a connection attempt for the threads to exit (should be plenty of time, but prevents an infinite loop in the event that something goes haywire)
                TimeSpan timeLimit = TimeSpan.FromMilliseconds(_socketConnectionAttemptTimeout * _maxSocketConnectionAttempts * 2);
                Stopwatch timeoutTimer = new();
                timeoutTimer.Start();
                while (timeoutTimer.Elapsed < timeLimit && _bgCommsWorker.IsBusy)
                {
                    Thread.Sleep(50);
                }

                if (timeoutTimer.Elapsed >= timeLimit)
                    Globals.SystemManager.LogApplicationEvent(this, "", "Timeout while waiting for comms thread to exit");

                timeoutTimer.Stop();
            }
            else
            {
                if (!_bgCommsWorker.IsBusy && CommunicationsEnabled && _queueManager.TotalQueueCount > 0 && ConnectionStatus != Globals.ConnStatus.ConnectionRetry_Delay && Globals.FDAStatus == Globals.AppState.Normal)
                    _bgCommsWorker.RunWorkerAsync();
            }
        }

        private void SocketConnectionRetryTick(object state)
        {
            SocketConnectionRetryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            if (!_bgCommsWorker.IsBusy && ConnectionEnabled == true && Globals.FDAStatus == Globals.AppState.Normal)
                _bgCommsWorker.RunWorkerAsync();
        }

        private void BgCommsWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                //Globals.SystemManager.LogApplicationEvent(this, Description, "Comms thread exited");
                LogCommsEvent(initTime.Add(runTime.Elapsed), "Comms thread exited");

                if (!(Globals.FDAStatus == Globals.AppState.Starting || Globals.FDAStatus == Globals.AppState.Normal))
                    return;

                // detect failed-to-connect, set timer for next connection attempt
                if (ConnectionEnabled && ConnectionType == ConnType.Ethernet && !(TCPLocalConnected && TCPRemoteConnected))
                {
                    Globals.SystemManager.LogApplicationEvent(this, Description, "Failed to connect to remote device, waiting for " + SocketConnectionRetryDelay + " seconds before trying again");
                    Globals.SystemManager.LogCommsEvent(ConnectionID, initTime.Add(runTime.Elapsed), "Failed to connect to remote device, waiting for " + SocketConnectionRetryDelay + " seconds before trying again");
                    if (SocketConnectionRetryTimer == null)
                    {
                        SocketConnectionRetryTimer = new Timer(SocketConnectionRetryTick, null, SocketConnectionRetryDelay * 1000, Timeout.Infinite);
                    }
                    else
                        SocketConnectionRetryTimer.Change(SocketConnectionRetryDelay * 1000, Timeout.Infinite);

                    ConnectionStatus = Globals.ConnStatus.ConnectionRetry_Delay;
                }
                else
                {
                    //ConnectionManagement();

                    if (_idleDisconnect)
                    {
                        _connectionIdleTimer.Change(_idleDisconnectTime * 1000, Timeout.Infinite);
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(initTime.Add(runTime.Elapsed), ex, "Error occurred in BgCommsWorker_RunWorkerCompleted()");
            }
        }

        private void AddToCompletedQueue(DataRequest trans)
        {
            lock (_completedTransQueue)
            {
                _completedTransQueue.Enqueue(trans);
            }
        }

        #endregion Events and EventHandlers

        #region public functions

        public bool QueueTransactionGroup(RequestGroup requestGroup)
        {
            if (!ConnectionEnabled)
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "Connection " + requestGroup.ConnectionID + " rejected group " + requestGroup.ID + " from Data Acquisition Manager because connection not enabled");
                return false;
            }
            //Globals.SystemManager.LogApplicationEvent(this, "", "Connection " + requestGroup.ConnectionID + "[" + requestGroup.Priority + "] received group " + requestGroup.ID + " from Data Acquisition Manager");

            return _queueManager.QueueTransactionGroup(requestGroup);
        }

        /*
        private UInt16 GetAverageTransactionTime()
        {
            List<TimeSpan> buffer = _recentTimeMeasurements.GetBuffer();

            if (buffer.Count == 0)
                return 0;

            double doubleAverageTicks = buffer.Average(timeSpan => timeSpan.Ticks);
            long longAverageTicks = Convert.ToInt64(doubleAverageTicks);

            return (UInt16)(new TimeSpan(longAverageTicks).TotalMilliseconds);
        }
        */

        /*
        private UInt16 GetEstimatedBacklogTime(int priority)
        {
            return (UInt16)_queueManager.GetEstimatedBacklogtime(priority).TotalSeconds;
        }
        */

        public int GetQueueCount(int priority)
        {
            return _queueManager.GetQueueCount(priority);
        }

        #endregion public functions

        #region private helper functions

        private void LogCommsEvent(DataRequest currentRequest, RequestGroup currentGroup, int attemptCount)
        {
            //string devaddr;
            //if (currentRequest.Protocol == "BSAPUDP")
            //    devaddr = currentRequest.UDPIPAddr;
            //else
            //    devaddr = currentRequest.NodeID;

            if (currentGroup.CommsLogEnabled || CommsLogEnabled)
            {
                TransactionLogItem logItem = new(currentRequest, attemptCount, this.ConnectionID, currentGroup.ID, currentRequest.GroupSize, currentRequest.GroupIdxNumber, "Queue " + currentGroup.Priority + " count = " + _queueManager.GetQueueCount(currentGroup.Priority));
                Globals.SystemManager.LogCommsEvent(logItem);
                Thread.Sleep(1);
            }
        }

        private void LogAckEvent(DateTime eventTime, DataRequest currentRequest)
        {
            AcknowledgementEvent ack = new(currentRequest, this.ConnectionID, eventTime, currentRequest.AckBytes, currentRequest.GroupID, currentRequest.GroupSize, currentRequest.GroupIdxNumber);
            Globals.SystemManager.LogAckEvent(ack);
        }

        private string GetStatusSuffix()
        {
            //TCPLocalConnected = IsTCPLocalConnected();
            //TCPRemoteConnected = IsTCPClientConnected();
            return " L" + Convert.ToInt32(TCPLocalConnected) + ":R" + Convert.ToInt32(TCPRemoteConnected) + ":CN" + Convert.ToInt32(ConnectionEnabled) + ":CM" + Convert.ToInt32(CommunicationsEnabled);
        }

        internal void LogCommsEvent(DateTime timestamp, string msg)
        {
            Globals.SystemManager.LogCommsEvent(ConnectionID, timestamp, msg + GetStatusSuffix());
            Thread.Sleep(1);
        }

        internal void LogConnectionCommsEvent(int attemptNum, DateTime startTime, TimeSpan elapsed, byte success, string message)
        {
            Globals.SystemManager.LogConnectionCommsEvent(ConnectionID, attemptNum, startTime, elapsed, success, message + GetStatusSuffix());
            Thread.Sleep(1);
        }

        private static void ProtocolResponseValidityCheck(DataRequest transaction, bool finalAttempt)
        {
            switch (transaction.Protocol.ToUpper())
            {
                case "ROC": ROCProtocol.ValidateResponse(transaction, finalAttempt); break;
                case "MODBUS": ModbusProtocol.ValidateResponse(transaction); break;
                case "MODBUSTCP": ModbusProtocol.ValidateResponse(transaction); break;
                case "ENRONMODBUS": ModbusProtocol.ValidateResponse(transaction); break;
                case "BSAP": BSAPProtocol.ValidateResponse(transaction); break;
                case "BSAPUDP": BSAPProtocol.ValidateResponse(transaction); break;
                default: return;
            }
        }

        private void AddRequestTimeSpanToBuffer(DateTime startTime, DateTime endTime)
        {
            _recentTimeMeasurements.AddItem(endTime.Subtract(startTime));
        }

        private bool IsTCPClientConnected()
        {
            bool clientConnected;

            if (_tcpConnection == null)
                clientConnected = false;
            else
            {
                if (!_tcpConnection.Connected) // if we're not connected at our end, we're not connected at the remote end either
                    clientConnected = false;
                else
                {
                    Socket socket = _tcpConnection.Client;
                    try
                    {
                        clientConnected = !(socket.Poll(1, SelectMode.SelectRead)); //&& socket.Available == 0);
                    }
                    catch (SocketException)
                    {
                        clientConnected = false;
                    }
                }
            }

            TCPRemoteConnected = clientConnected;

            if (!clientConnected)
            {
                //clientConnected = false;
                ConnectionStatus = Globals.ConnStatus.Disconnected;
                LogCommsEvent(initTime.Add(runTime.Elapsed), "Connection: " + Description + " - remote device not connected");
            }

            return clientConnected;
        }

        #endregion private helper functions

        #region communications

        private bool CommsThreadCancelled()
        {
            bool canceling = (_bgCommsWorker.CancellationPending || Globals.FDAStatus == Globals.AppState.ShuttingDown || Globals.FDAStatus == Globals.AppState.Pausing);
            if (canceling)
                Globals.SystemManager.LogApplicationEvent(this, Description, "Stopping communications thread");
            return canceling;
        }

        private bool IsTCPLocalConnected()
        {
            if (_tcpConnection != null)
                return _tcpConnection.Connected;
            else
                return false;
        }

        private bool ConnectionManagement() // returns whether a connection exists after connection mananger or not
        {
            bool result = false;
            if (ConnectionEnabled)
            {
                switch (ConnectionType)
                {
                    case ConnType.Ethernet:
                        TCPLocalConnected = IsTCPLocalConnected();
                        TCPRemoteConnected = IsTCPClientConnected();
                        if (!(TCPLocalConnected && TCPRemoteConnected))
                        {
                            Globals.SystemManager.LogApplicationEvent(this, Description, "Attempting to connect/re-connect");
                            TCPConnect();
                            if (TCPLocalConnected && TCPRemoteConnected)
                            {
                                result = true;
                                //Globals.SystemManager.LogApplicationEvent(this, Description, " connected");
                            }
                        }
                        else
                            result = true;
                        break;

                    case ConnType.EthernetUDP:
                        if (_stream == null)
                        {
                            UDPConnect();
                            if (_stream != null)
                            {
                                if (_stream.Connected)
                                {
                                    //Globals.SystemManager.LogApplicationEvent(this, Description, " connected");
                                    result = true;
                                }
                            }
                        }
                        else
                        {
                            if (!_stream.Connected)
                            {
                                UDPConnect();
                                if (_stream.Connected)
                                {
                                    //Globals.SystemManager.LogApplicationEvent(this, Description, " connected");
                                    result = true;
                                }
                            }
                            else
                                result = true;
                        }

                        break;

                    case ConnType.Serial:
                        if (!(ConnectionStatus == Globals.ConnStatus.Connected_Ready))
                            SerialConnect();
                        if (ConnectionStatus == Globals.ConnStatus.Connected_Ready)
                            result = true;
                        break;

                    default:
                        LogCommsEvent(initTime.Add(runTime.Elapsed), "Connection: " + Description + " Unsupported connection type");
                        result = false;
                        break;
                }
            }
            else
            {
                if (ConnectionStatus == Globals.ConnStatus.Connected_Ready)
                {
                    Globals.SystemManager.LogApplicationEvent(this, Description, "Disconnecting (ConnectionEnabled = false");
                    LogCommsEvent(initTime.Add(runTime.Elapsed), "Disconnecting");
                    Disconnect();
                    result = false;
                }
            }
            return result;
        }

        private void BgCommsWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (!(Globals.FDAStatus == Globals.AppState.Starting || Globals.FDAStatus == Globals.AppState.Normal))
                return;

            // Globals.SystemManager.LogApplicationEvent(this, Description, "Comms thread started");
            LogCommsEvent(initTime.Add(runTime.Elapsed), "Comms thread started");

            try
            {
                int attemptCount;
                Int32 bytesRead;
                Int32 totalBytes;
                int checkReadBufferCount;
                int maxReadBufferCheckCount = RequestResponseTimeout / 20;   // default timeout for the connection
                int maxRequestAttempts = _maxRequestAttempts;                // default max request attempts
                int interRequestDelay = _interRequestDelay;                  // default interrequest delay
                int requestRetryDelay = _requestRetryDelay;                  // default request retry delay
                Byte[] readBuffer = new byte[65535];
                bool extraData;
                long elapsedtime;
                bool connActive;
                FDADevice deviceSettings = null;

                connActive = ConnectionManagement();

                if (connActive)
                    _connectionIdleTimer.Change(Timeout.Infinite, Timeout.Infinite);
                else
                    return;

                MemoryStream memStream = new();

                while (_queueManager.TotalQueueCount > 0 && CommunicationsEnabled) // repeat as long as there are request groups found in the queue
                {
                    if (CommsThreadCancelled()) return;

                    RequestGroup currentRequestGroup = null;

                    //Thread.Sleep(InterRequestDelay);

                    currentRequestGroup = _queueManager.GetNextRequestGroup();

                    // if the request group has been deleted or disabled since it was queued, skip it (ignore this for system requested groups)
                    if (currentRequestGroup.RequesterType != Globals.RequesterType.System)
                    {
                        FDADataBlockRequestGroup template = _dbManager.GetRequestGroup(currentRequestGroup.ID);
                        if (template == null)
                            continue;
                        else
                            if (!template.DRGEnabled)
                            continue;
                    }

                    // send it to the protocol to get the requests built
                    // but only do this if it hasn't already been done
                    // for example, if this group is a system generated group resulting from a pre-write read
                    if (currentRequestGroup.ProtocolRequestList.Count == 0)
                    {
                        switch (currentRequestGroup.Protocol.ToUpper())
                        {
                            case "ROC": ROCProtocol.CreateRequests(currentRequestGroup); break;
                            case "MODBUS": ModbusProtocol.CreateRequests(currentRequestGroup); break;
                            case "MODBUSTCP": ModbusProtocol.CreateRequests(currentRequestGroup, "TCP"); break;
                            case "ENRONMODBUS": ModbusProtocol.CreateRequests(currentRequestGroup, "ENRON"); break;
                            case "BSAP": BSAPProtocol.CreateRequests(currentRequestGroup); break;
                            case "BSAPUDP": BSAPProtocol.CreateRequests(currentRequestGroup); break;
                            default:
                                Globals.SystemManager.LogApplicationEvent(this, "", "unrecognized protocol '" + currentRequestGroup.Protocol + "' in group '" + currentRequestGroup.Description + "' (" + currentRequestGroup.ID + ")");
                                continue;
                        }
                    }

                    DataRequest currentRequest;
                    DataRequest errorRepairRequest = null;

                    // loop through the transactions in the group
                    int requestIdx = 0;

                    while (requestIdx < currentRequestGroup.ProtocolRequestList.Count)
                    {
                        if (CommsThreadCancelled()) return;
                        GroupLoop:

                        // in case we jumped into this loop (using the GroupLoop label) with a bad idx
                        if (requestIdx >= currentRequestGroup.ProtocolRequestList.Count)
                            continue;

                        if (errorRepairRequest != null)
                        {
                            currentRequest = errorRepairRequest;
                        }
                        else
                        {
                            currentRequest = currentRequestGroup.ProtocolRequestList[requestIdx];
                            currentRequest.Destination = currentRequestGroup.DestinationID;
                        }

                        // check if this request has been marked to be skipped (if a device has not responded to a previous request, skip this request)
                        if (currentRequest.SkipRequestFlag)
                        {
                            requestIdx++;
                            DateTime eventTime = initTime.Add(runTime.Elapsed);
                            if (eventTime.ToString() == _lastLogTimestamp.ToString()) eventTime = eventTime.AddMilliseconds(1);
                            _lastLogTimestamp = eventTime;
                            LogCommsEvent(eventTime, "Skipped request " + requestIdx + ", device failed to respond to previous request");
                            continue;
                        }

                        // check for device specific communications settings (overrides the connection comms settings)
                        deviceSettings = currentRequest.DeviceSettings;
                        if (deviceSettings != null)
                        {
                            // device settings override default settings
                            DateTime eventTime = initTime.Add(runTime.Elapsed);
                            if (eventTime.ToString() == _lastLogTimestamp.ToString()) eventTime = eventTime.AddMilliseconds(1);
                            _lastLogTimestamp = eventTime;
                            Globals.SystemManager.LogCommsEvent(ConnectionID, eventTime, "Device specific comms settings overriding default settings for request group " + currentRequestGroup.ID);
                            maxReadBufferCheckCount = deviceSettings.request_timeout / 20;
                            maxRequestAttempts = deviceSettings.max_request_attempts;
                            interRequestDelay = deviceSettings.inter_request_delay;
                            requestRetryDelay = deviceSettings.request_retry_delay;
                        }
                        else
                        {
                            // use the default settings specified for the connection
                            maxReadBufferCheckCount = _requestResponseTimeout / 20;
                            maxRequestAttempts = _maxRequestAttempts;
                            interRequestDelay = _interRequestDelay;
                            requestRetryDelay = _requestRetryDelay;
                        }

                        if (CommsThreadCancelled()) return;

                        // reset for the next transaction
                        attemptCount = 0;
                        totalBytes = 0;
                        memStream.SetLength(0); // reset the memory stream

                        // if flushing the receive buffer fails (too much data in the buffer, or data continuously pouring in for > 1 sec), exit the comms thread
                        /*
                        if (!_stream.Flush())
                        {
                            Globals.SystemManager.LogApplicationEvent(this, "Connection " + _connectionID + "(" + Description + ")", "Pre-transmit receive buffer flush found more than 2 kB of data or continuous data for more than 1 second. Disabling the noisy connection");
                            Globals.SystemManager.LogCommsEvent(_connectionID, Globals.GetOffsetUTC(), "Pre-transmit receive buffer flush found > 2 kB of data or continuous data for more than 1 second. Disabling the noisy connection");
                            ConnectionEnabled = false;
                            ((DBManager)Globals.DBMananger).ExecuteSQL("update " + Globals.SystemManager.GetTableName("FDASourceConnections") + " set ConnectionEnabled = 'False' where SCUID = '" + ConnectionID + "';");
                            return;
                        }
                        */

                        if (CommsThreadCancelled()) return;

                        Thread.Sleep(interRequestDelay);

                        // record the request timestamp for the first transaction as the start time for the group

                        if (currentRequestGroup.CommsInitiatedTimestamp == DateTime.MinValue)
                            currentRequestGroup.CommsInitiatedTimestamp = currentRequest.RequestTimestamp;

                        if (ConnectionType == ConnType.EthernetUDP && currentRequest.UDPIPAddr != null)
                        {
                            _stream.SetDestination(currentRequest.UDPIPAddr);
                        }

                        while (attemptCount < maxRequestAttempts && currentRequest.Status != DataRequest.RequestStatus.Success && currentRequest.Status != DataRequest.RequestStatus.PartialSuccess)
                        {
                            if (CommsThreadCancelled()) return;

                            connActive = ConnectionManagement();

                            if (!connActive)
                            {
                                return;
                            }
                            // perform the transaction (attempt up to the set maximum number of times)
                            Stopwatch transactionStopwatch = new();
                            DateTime eventTime = initTime.Add(runTime.Elapsed);
                            if (eventTime.ToString() == _lastLogTimestamp.ToString()) eventTime = eventTime.AddMilliseconds(1);
                            _lastLogTimestamp = eventTime;
                            currentRequest.RequestTimestamp = eventTime;
                            transactionStopwatch.Start();

                            attemptCount++;
                            if (attemptCount > 1)
                            {
                                Thread.Sleep(requestRetryDelay);
                                currentRequest.ErrorMessage = "";
                            }

                            bytesRead = 0;          // number of bytes received in one read
                            totalBytes = 0;         // total number of bytes received
                            memStream.SetLength(0); // reset the memory stream

                            // if flushing the receive buffer fails (too much data in the buffer, or data continuously pouring in for > 1 sec), exit the comms thread
                            if (!_stream.Flush())
                            {
                                //((DBManager)Globals.DBMananger).ExecuteSQL("update " + Globals.SystemManager.GetTableName("FDASourceConnections") + " set ConnectionEnabled = 'False' where SCUID = '" + ConnectionID + "';");
                                //ConnectionEnabled = false;
                                //return;

                                Globals.SystemManager.LogApplicationEvent(this, Description, "Pre-transmit receive buffer flush found more than 2 kB of data or continuous data for more than 1 second. Resetting the noisy connection (waiting " + SocketConnectionRetryDelay + " seconds before re-connecting");

                                eventTime = initTime.Add(runTime.Elapsed);
                                if (eventTime.ToString() == _lastLogTimestamp.ToString()) eventTime = eventTime.AddMilliseconds(1);
                                _lastLogTimestamp = eventTime;
                                Globals.SystemManager.LogCommsEvent(ConnectionID, eventTime, "Pre-transmit receive buffer flush found more than 2 kB of data or continuous data for more than 1 second.Resetting the noisy connection (waiting " + SocketConnectionRetryDelay + " seconds before re-connecting)");

                                if (ConnectionType == ConnType.Ethernet)
                                {
                                    Disconnect();
                                    Thread.Sleep(SocketConnectionRetryDelay * 1000);  // wait for the connection retry delay time before attempting to reconnect
                                    TCPConnect();
                                }
                            }

                            //_stream.SetDestination()

                            // send the request (increment the attempt counter and return to the top of the loop if not successful)
                            if (!SendRequest(currentRequest))
                                continue;

                            // read the response
                            Array.Clear(readBuffer, 0, readBuffer.Length); // clear the input buffer

                            checkReadBufferCount = 0;
                            extraData = false;

                            if (CommsThreadCancelled()) return;

                            try
                            {
                                while (checkReadBufferCount < maxReadBufferCheckCount && (totalBytes < currentRequest.ExpectedResponseSize) || extraData)
                                {
                                    if (CommsThreadCancelled()) return;

                                    checkReadBufferCount = 0;

                                    // wait up to the request timeout for data before giving up (checking every 20 ms)
                                    while (!_stream.DataAvailable && checkReadBufferCount < maxReadBufferCheckCount)
                                    {
                                        Thread.Sleep(20);
                                        checkReadBufferCount++;
                                    }

                                    if (_stream.DataAvailable)
                                    {
                                        bytesRead = _stream.Read(readBuffer, 0, readBuffer.Length);
                                        totalBytes += bytesRead;
                                        memStream.Write(readBuffer, 0, bytesRead);
                                        //Message("read " + bytesRead + " bytes");
                                    }

                                    // one extra wait cycle to check for more data than expected
                                    // if more data is found, resume the wait/read cyle until it times out to ensure that all data has been received
                                    if (!extraData && totalBytes >= currentRequest.ExpectedResponseSize)
                                    {
                                        Thread.Sleep(20);
                                        if (totalBytes > currentRequest.ExpectedResponseSize || _stream.DataAvailable)
                                        {
                                            extraData = true;
                                        }
                                    }
                                    else
                                        extraData = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                // failed to read the response, record the error message and move on to the next DataRequest
                                currentRequest.ErrorMessage = ex.Message;
                                currentRequest.ResponseBytes = Array.Empty<byte>();
                                currentRequest.SetStatus(DataRequest.RequestStatus.Error);
                                Globals.SystemManager.LogApplicationError(initTime.Add(runTime.Elapsed), ex, "Error while attempting to read the response to request " + currentRequest.GroupIdxNumber + " of group " + currentRequestGroup.ID);
                                transactionStopwatch.Stop();
                                elapsedtime = transactionStopwatch.ElapsedMilliseconds;
                                eventTime = currentRequest.RequestTimestamp.AddMilliseconds(elapsedtime);
                                if (eventTime.ToString() == _lastLogTimestamp.ToString()) eventTime = eventTime.AddMilliseconds(1);
                                _lastLogTimestamp = eventTime;
                                currentRequest.ResponseTimestamp = eventTime;
                                LogCommsEvent(currentRequest, currentRequestGroup, attemptCount);
                                continue;
                            }

                            transactionStopwatch.Stop();
                            elapsedtime = transactionStopwatch.ElapsedMilliseconds;

                            eventTime = currentRequest.RequestTimestamp.AddMilliseconds(elapsedtime);
                            if (eventTime.ToString() == _lastLogTimestamp.ToString()) eventTime = eventTime.AddMilliseconds(1);
                            _lastLogTimestamp = eventTime;
                            currentRequest.ResponseTimestamp = eventTime;
                            currentRequest.ResponseBytes = memStream.ToArray();
                            //Message("Rx: " + BitConverter.ToString(currentRequest.ResponseBytes));

                            if (CommsThreadCancelled()) return;

                            // request a response validity check from the protocol (CRC + anything else important to the protocol)
                            // if the response is valid, the protocol will extract the data and interpret it, and put the values in the "ReturnValues" array
                            ProtocolResponseValidityCheck(currentRequest, attemptCount == MaxRequestAttempts);

                            // if the protocol produced an acknowledgement to be sent back to the device, do that now
                            if (currentRequest.AckBytes != null)
                            {
                                _stream.Write(currentRequest.AckBytes, 0, currentRequest.AckBytes.Length);
                                eventTime = initTime.Add(runTime.Elapsed);
                                if (eventTime.ToString() == _lastLogTimestamp.ToString()) eventTime = eventTime.AddMilliseconds(1);
                                _lastLogTimestamp = eventTime;

                                LogAckEvent(eventTime, currentRequest);
                            }

                            // original message has an error repair suggestion from the protocol, try it
                            if (currentRequest.Status == DataRequest.RequestStatus.Error && currentRequest.ErrorCorrectionRequest != null && errorRepairRequest == null)
                            {
                                errorRepairRequest = currentRequest.ErrorCorrectionRequest;
                                currentRequest.ErrorCorrectionRequest = null;
                                LogCommsEvent(currentRequest, currentRequestGroup, attemptCount);
                                goto GroupLoop;
                            }

                            // check if this request is an error repair request
                            if (errorRepairRequest != null)
                            {
                                // it received a success response, clear the error request, and go back to the top to process the original data request
                                if (currentRequest.Status == DataRequest.RequestStatus.Success)
                                {
                                    errorRepairRequest = null;
                                    LogCommsEvent(currentRequest, currentRequestGroup, attemptCount);
                                    goto GroupLoop;
                                }

                                // the error repair received an error response, move on to the next data request
                                if (currentRequest.Status != DataRequest.RequestStatus.Success)
                                {
                                    errorRepairRequest = null;
                                    requestIdx += 1;
                                    LogCommsEvent(currentRequest, currentRequestGroup, attemptCount);
                                    goto GroupLoop;
                                }
                            }

                            if (currentRequest.Status != DataRequest.RequestStatus.Success)
                            {
                                LogCommsEvent(currentRequest, currentRequestGroup, attemptCount);
                                continue;
                            }
                        } // end of attempt loop

                        // if after all attempts are exhausted and the request is not a success, indicate that the group
                        // contains failed requests so it can be requeued (if the Requeue count > 0)
                        if (currentRequest.Status != DataRequest.RequestStatus.Success)
                        {
                            currentRequestGroup.containsFailedReq = true;
                        }

                        if ((currentRequest.MessageType == DataRequest.RequestType.PreWriteRead || currentRequest.MessageType == DataRequest.RequestType.HourHistoryPtr || currentRequest.MessageType == DataRequest.RequestType.Backfill) && currentRequest.Status == DataRequest.RequestStatus.Success)
                        {
                            if (currentRequest.SuperPriorityRequestGroup != null)
                            {
                                // insert this requestgroup at the top of the highest priority queue, to ensure that it is the next group processes
                                _queueManager.QueueTransactionGroup(currentRequest.SuperPriorityRequestGroup, true);
                            }

                            if (currentRequest.NextGroup != null)
                            {
                                // insert this requestgroup in a regular queue
                                _queueManager.QueueTransactionGroup(currentRequest.NextGroup);
                            }
                        }

                        if (CommsThreadCancelled()) return;

                        // if no response after max attempts reached, move on to the next request in the group
                        if (totalBytes == 0)
                        {
                            // cancel other requests to the current device from the request group
                            switch (currentRequest.Protocol)
                            {
                                case "MODBUS": ModbusProtocol.CancelDeviceRequests(currentRequest); break;
                                case "MODBUSTCP": ModbusProtocol.CancelDeviceRequests(currentRequest); break;
                                case "ENRONMODBUS": ModbusProtocol.CancelDeviceRequests(currentRequest); break;
                                case "ROC": ROCProtocol.CancelDeviceRequests(currentRequest); break;
                                case "BSAP": BSAPProtocol.CancelDeviceRequests(currentRequest); break;
                            }
                            AddRequestTimeSpanToBuffer(currentRequest.RequestTimestamp, initTime.Add(runTime.Elapsed));
                            currentRequest.ResponseBytes = Array.Empty<byte>();
                            AddToCompletedQueue(currentRequest);
                            requestIdx++;
                            continue;  // next request
                        }
                        else
                        {
                            // received a response
                            AddRequestTimeSpanToBuffer(currentRequest.RequestTimestamp, currentRequest.ResponseTimestamp);
                            LastCommsTime = currentRequest.ResponseTimestamp;

                            // don't return intermediate transactions to DataAcq, only the final response
                            if (currentRequest.MessageType != DataRequest.RequestType.PreWriteRead)
                                AddToCompletedQueue(currentRequest);
                        }

                        // record the response timestamp as the completion time for the group
                        // (gets overwritten with each request)
                        currentRequestGroup.CommsCompletedTimestamp = currentRequest.ResponseTimestamp;
                        if (currentRequest.Status == DataRequest.RequestStatus.Success)
                            LogCommsEvent(currentRequest, currentRequestGroup, attemptCount);
                        requestIdx += 1;

                        //LogCommsEvent("Request Group \"" + currentRequestGroup.Description + "\" complete");
                    } // end of loop that handles a single request within a group

                    if (currentRequestGroup.containsFailedReq && currentRequestGroup.RequeueCount > 0)
                    {
                        // for backfills, clear the protocol request list so that it'll be regenerated next time the group comes up for processing
                        if (currentRequestGroup.ProtocolRequestList.Count > 0)
                            if (currentRequestGroup.ProtocolRequestList[0].MessageType == DataRequest.RequestType.Backfill)
                                currentRequestGroup.ProtocolRequestList.Clear();

                        // if any of the requests in the group failed, requeue the group
                        RequeueGroup(currentRequestGroup);
                    }
                } // end of loop for a request group

                // no more requests in the queue or comms not enabled, exit the background comms thread until there's something to do
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(initTime.Add(runTime.Elapsed), ex, "Error in comms thread (general error catching)");
            }
        }

        private bool SendRequest(DataRequest currentTrans)
        {
            bool result;
            try
            {
                //Message("Tx: " + BitConverter.ToString(currentTrans.RequestBytes));
                result = _stream.Write(currentTrans.RequestBytes, 0, currentTrans.RequestBytes.Length);

                return result;
            }
            catch (Exception ex)
            {
                // send request failed (application error) record error message and move on to the next DataRequest
                currentTrans.ResponseBytes = Array.Empty<byte>();
                currentTrans.ErrorMessage = ex.Message;
                currentTrans.SetStatus(DataRequest.RequestStatus.Error);
                Globals.SystemManager.LogApplicationError(initTime.Add(runTime.Elapsed), ex, "Error occurred while attempting to send request " + currentTrans.GroupIdxNumber + " of group " + currentTrans.GroupID);
                return false;
            }
        }

        /* Feb 17 - removed this background worker, all connection operations now handled in the comms thread (prvent thread conflicts)
               private void BgConnection_DoWork(object sender, DoWorkEventArgs e)
               {
                   switch (ConnectionType)
                   {
                       case ConnType.Ethernet: TCPConnect(); break;
                       case ConnType.Serial: SerialConnect(); break;
                       default: LogCommsEvent("Connection: " + Description + " Unsupported connection type for connection '" + Description + "', valid types are 'Ethernet' or 'Serial'"); break;
                   }
               }
       */

        private void SerialConnect()
        {
            ConnectionStatus = Globals.ConnStatus.Connecting;
            string errorMsg = "";
            byte status = 0;
            Stopwatch connectionTimer = new();
            DateTime startTime = initTime.Add(runTime.Elapsed);
            connectionTimer.Start();

            if (_stream != null)
            {
                _stream.Close();
                _stream.Dispose();
            }

            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }

            try
            {
                _serialPort = new SerialPort(_serialPortName, _serialBaudRate, _serialParity, _serialDataBits, SerialStopBits)
                {
                    Handshake = _serialHandshake,
                    ReadTimeout = _requestResponseTimeout
                };
            }
            catch (Exception ex)
            {
                connectionTimer.Stop();
                ConnectionStatus = Globals.ConnStatus.Disconnected;
                errorMsg = ex.Message;
            }

            try
            {
                _serialPort.Open();
            }
            catch (Exception ex)
            {
                connectionTimer.Stop();
                ConnectionStatus = Globals.ConnStatus.Disconnected;
                errorMsg = ex.Message;
            }

            if (_serialPort.IsOpen)
            {
                connectionTimer.Stop();
                ConnectionStatus = Globals.ConnStatus.Connected_Ready;
                status = 1;
                _stream = new ConnectionWrapper(_serialPort, ConnectionID.ToString());
                _LocalConnected = true;
            }
            else
            {
                connectionTimer.Stop();
                ConnectionStatus = Globals.ConnStatus.Disconnected;
                Globals.SystemManager.LogApplicationEvent(this, "Connection '" + Description + "'", "Failed to connect : " + errorMsg);
                //errorMsg = "Failed to open " + _serialPort.PortName + " : unknown reason";
            }

            string logMessage;
            if (status == 1)
                logMessage = "Connection: " + Description + ": Connected";
            else
                logMessage = "Connection: " + Description + ": Failed to Connect ('" + errorMsg + "')";

            Globals.SystemManager.LogConnectionCommsEvent(ConnectionID, 1, startTime, connectionTimer.Elapsed, status, logMessage);
        }

        private void TCPConnect()
        {
            DateTime startTime = initTime.Add(runTime.Elapsed);
            try
            {
                // clean up any previous connection
                if (_tcpConnection != null)
                {
                    _tcpConnection.Close();
                    _tcpConnection = null;
                    ConnectionStatus = Globals.ConnStatus.Disconnected;
                }

                TCPLocalConnected = false;
                TCPRemoteConnected = false;

                if (_stream != null)
                {
                    _stream.Close();
                    _stream.Dispose();
                    _stream = null;
                }

                // make a nice new connection
                _tcpConnection = new TcpClient
                {
                    ReceiveTimeout = RequestResponseTimeout,
                    SendTimeout = RequestResponseTimeout,
                    ReceiveBufferSize = 1024
                };
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(initTime.Add(runTime.Elapsed), ex, "Error occurred during pre-connection operations (" + Description + ")");
            }

            // attempt connection
            int attemptCount = 1;
            Stopwatch connectionTimer = new();
            Stopwatch attemptTimer = new();

            //while (!_tcpConnection.Connected && !Globals.ShuttingDown && !CommsThreadCancelled() )
            //{
            connectionTimer.Reset();
            connectionTimer.Start();
            //attemptCount = 1;
            try
            {
                while (!_tcpConnection.Connected && attemptCount <= MaxSocketConnectionAttempts && !CommsThreadCancelled())
                {
                    ConnectionStatus = Globals.ConnStatus.Connecting;
                    string errorMsg = "";
                    attemptTimer.Reset();
                    attemptTimer.Start();
                    if (Globals.FDAStatus == Globals.AppState.ShuttingDown)
                    {
                        connectionTimer.Stop();
                        ConnectionStatus = Globals.ConnStatus.Disconnected;
                        LogConnectionCommsEvent(attemptCount, initTime.Add(runTime.Elapsed), attemptTimer.Elapsed, 0, "Connection: " + Description + " Connection attempt cancelled");
                        return;
                    }

                    try
                    {
                        Globals.SystemManager.LogApplicationEvent(this, this.Description, "Connecting to " + RemoteIPAddress + ":" + PortNumber + "...");
                        _tcpConnection.Connect(RemoteIPAddress, PortNumber);
                    }
                    catch (Exception ex)
                    {
                        //Globals.Logger.LogApplicationError(Globals.GetOffsetUTC(), ex);
                        connectionTimer.Stop();
                        ConnectionStatus = Globals.ConnStatus.Disconnected;
                        TCPLocalConnected = false;
                        TCPRemoteConnected = false;
                        LogConnectionCommsEvent(attemptCount, initTime.Add(runTime.Elapsed), attemptTimer.Elapsed, 0, "Connection " + Description + " Failed to connect to " + _host + ":" + _portNumber);
                        errorMsg = ex.Message;
                        attemptCount++;
                    }
                }

                if (!_tcpConnection.Connected)
                {
                    connectionTimer.Stop();
                    LogConnectionCommsEvent(attemptCount, startTime, connectionTimer.Elapsed, 0, "Connection " + Description + ": Connecting - Failed to connect to " + _host + ":" + _portNumber);
                    TCPLocalConnected = false;
                    TCPRemoteConnected = false;
                    ConnectionStatus = Globals.ConnStatus.Disconnected;
                    LogCommsEvent(initTime.Add(runTime.Elapsed), "Connection: " + Description + " Initiating reconnection delay of " + SocketConnectionRetryDelay + " second(s)");
                    Thread.Sleep(SocketConnectionRetryDelay * 1000);
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(initTime.Add(runTime.Elapsed), ex, "Error while attempting to establish a TCP connection to " + _host + ":" + _portNumber + " (" + Description + ")");
            }
            //}

            TCPLocalConnected = IsTCPLocalConnected();
            TCPRemoteConnected = IsTCPClientConnected();

            if (TCPLocalConnected && TCPRemoteConnected)
            {
                try
                {
                    _stream = new ConnectionWrapper(_tcpConnection, ConnectionID.ToString())
                    {
                        ReadTimeout = RequestResponseTimeout,
                        WriteTimeout = RequestResponseTimeout,
                    };

                    connectionTimer.Stop();
                    LogConnectionCommsEvent(attemptCount, startTime, connectionTimer.Elapsed, 1, "Connection: " + Description + " Connected");

                    if (PostConnectionCommsDelay > 0)
                    {
                        LogCommsEvent(initTime.Add(runTime.Elapsed), "Connection: " + Description + " Post Connection delay of " + PostConnectionCommsDelay + " ms");
                        ConnectionStatus = Globals.ConnStatus.Connected_Delayed;
                        Thread.Sleep(PostConnectionCommsDelay);
                        ConnectionStatus = Globals.ConnStatus.Connected_Ready;
                    }
                    else
                        ConnectionStatus = Globals.ConnStatus.Connected_Ready;
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(initTime.Add(runTime.Elapsed), ex, "Error occurred after TCP connection established (" + Description + "), during post connection processing");
                    ConnectionStatus = Globals.ConnStatus.Disconnected;
                }
            }
            else
                ConnectionStatus = Globals.ConnStatus.Disconnected;
        }

        private void UDPConnect()
        {
            //udp.ExclusiveAddressUse = false;
            //udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UdpClient udp = new();
            if (!udp.Client.IsBound)
            {
                try
                {
                    udp.Client.Bind(new IPEndPoint(IPAddress.Any, PortNumber));
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Connection " + ConnectionID + " was unable to bind port " + PortNumber + ", because the port is already in use");
                    ConnectionStatus = Globals.ConnStatus.Disconnected;
                    return;
                }
            }

            if (udp.Client.IsBound)
                _LocalConnected = true;

            //IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(RemoteIPAddress), PortNumber);
            _stream = new ConnectionWrapper(udp, PortNumber, ConnectionID.ToString());
            udp.Client.ReceiveTimeout = RequestResponseTimeout;

            //udp.Connect(remoteEP);

            if (PostConnectionCommsDelay > 0)
            {
                LogCommsEvent(initTime.Add(runTime.Elapsed), "Connection: " + Description + " Post Connection delay of " + PostConnectionCommsDelay + " ms");
                ConnectionStatus = Globals.ConnStatus.Connected_Delayed;
                Thread.Sleep(PostConnectionCommsDelay);
                ConnectionStatus = Globals.ConnStatus.Connected_Ready;
            }
            else
            {
                Globals.SystemManager.LogApplicationEvent(this, this.Description, "UDPConnect() success, bound port " + PortNumber);
                ConnectionStatus = Globals.ConnStatus.Connected_Ready;
            }
        }

        private void Disconnect()
        {
            try
            {
                if (_stream != null)
                {
                    _stream.Close();
                    _stream.Dispose();
                    _stream = null;
                    TCPLocalConnected = false;
                    TCPRemoteConnected = false;
                    ConnectionStatus = Globals.ConnStatus.Disconnected;
                }

                /*
                    if (_tcpConnection != null)
                    {
                        _tcpConnection.Close();
                        _tcpConnection.Dispose();
                        _tcpConnection = null;
                        TCPLocalConnected = false;
                        TCPRemoteConnected = false;
                        ConnectionStatus = Globals.ConnStatus.Disconnected;
                    }
                    else
                    {
                        TCPLocalConnected = false;
                        TCPRemoteConnected = false;
                        ConnectionStatus = Globals.ConnStatus.Disconnected;
                    }

                    if (_serialPort != null)
                    {
                        _serialPort.Close();
                        _serialPort.Dispose();
                        _serialPort = null;
                    }
                */
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(initTime.Add(runTime.Elapsed), ex, "Error during disconnection (" + Description + ")");
            }
        }

        #endregion communications

        #region cleanup

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Globals.SystemManager.LogApplicationEvent(this, "", "Shutting down connection manager for '" + Description + "' (" + ConnectionID + ")");

            base.MQTTEnabled = false;

            ConnectionEnabled = false;
            CommunicationsEnabled = false;
            _queueManager.ClearQueues();

            _bgCommsWorker.CancelAsync();
            _bgCompletedTransHandler.CancelAsync();

            // wait for the comms worker and completed transaction handling threads to exit
            Globals.SystemManager.LogApplicationEvent(this, Description, "Shutting down connection '" + Description + "' background processes");
            while (_bgCommsWorker.IsBusy || _bgCompletedTransHandler.IsBusy)
            {
                Thread.Sleep(50);
            }

            _bgCommsWorker.Dispose();
            _bgCompletedTransHandler.Dispose();

            if (_tcpConnection != null)
            {
                if (_tcpConnection.Client != null)
                    if (_tcpConnection.Connected)
                        _tcpConnection.Close();
                _tcpConnection.Dispose();
                _tcpConnection = null;
            }

            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();

                _serialPort.Dispose();
                _serialPort = null;
            }

            if (_stream != null)
            {
                _stream.Close();
                _stream.Dispose();
            }

            Globals.SystemManager.LogApplicationEvent(this, Description, "Connection '" + Description + "' shutdown complete");
        }

        #endregion cleanup
    }
}