using Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OPC;
using System.Runtime.CompilerServices;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Diagnostics;

namespace FDA
{
    // Manages communications for protocols that follow the Publish/Subscribe communication pattern
    public class PubSubConnectionManager : SubscriptionManager.SubscribeableObject, IDisposable
    {
        // public properties
        public int SocketConnectionRetryDelay { get => _socketConnectionRetryDelay; set { _socketConnectionRetryDelay = value; HandlePropertyChanged(); } }
        public int PostConnectionCommsDelay { get => _postConnectionCommsDelay; set { _postConnectionCommsDelay = value; HandlePropertyChanged(); } }
        public bool ConnectionEnabled { get => _connectionEnabled; set { _connectionEnabled = value; HandlePropertyChanged(); } }
        public bool CommunicationsEnabled { get => _communicationsEnabled; set { _communicationsEnabled = value; HandlePropertyChanged(); } }
        public ConnStatus ConnectionStatus { get => _connStatus; private set { _connStatus = value; HandlePropertyChanged(); } }
        public Guid ConnectionID { get => _connectionID; }
        public string Description { get => _description; set => _description = value; } 
        public int MaxSocketConnectionAttempts { get => _maxSocketConnectionAttempts; set { _maxSocketConnectionAttempts = value; HandlePropertyChanged(); } }
        public bool CommsLogEnabled { get => _commsLogEnabled; set { _commsLogEnabled = value; HandlePropertyChanged(); } }
        public ConnType ConnectionType { get => _connType; set => _connType = value; }
        public string ConnDetails { get => _connDetails; set => _connDetails = value; }
        public string Host { get => _host; set => _host = value; }
        public int Port { get => _port; set => _port = value; }
        public string ProgID { get => _progID; set => _progID = value; }
        public string ClassID { get => _classID; set => _classID = value; }


        // public events
        public delegate void DataUpdateHandler(object sender, TransactionEventArgs e);
        public event DataUpdateHandler DataUpdate;


        // enums
        public enum ConnType {OPCUA,OPCDA,MQTT};
        public enum ConnStatus { Disconnected, ConnectionRetry_Delay, Connected_Ready, Connecting, Connected_Delayed }

        // connection settings from database
        private int _socketConnectionRetryDelay = 10;     // seconds
        private int _postConnectionCommsDelay = 0;        // milliseconds
        private bool _connectionEnabled = false;
        private bool _communicationsEnabled = false;
        private int _maxSocketConnectionAttempts = 3;
        private bool _commsLogEnabled = false;
        private string _connDetails = "";

        // pubsub common member variables
        private ConnType _connType;
        private string _host;
        private int _port;
        private ConnStatus _connStatus = ConnStatus.Disconnected;
        private CancellationTokenSource _connectionCancel;
        private Task _connectionTask;
        private Dictionary<string, DataSubscription> _subscriptions = new Dictionary<string,DataSubscription>();
        private Guid _connectionID;
        private string _description;
       
        // OPC specific member variables
        private OPC.Client _OPCClient;
        private string _progID;  // OPC DA only;
        private string _classID; // OPC DA only;
        private Dictionary<string, OpcSubscription> _OpcSubLookup = new Dictionary<string, OpcSubscription>();

     
        public PubSubConnectionManager(Guid id, string description) 
        {
            base.ID = id.ToString();
            base.ObjectType = "connection";

            _connectionID = id;
            _description = description;
            //_connectionCancel = new CancellationTokenSource();
            //_connectionTask = new Task(new Action(DoConnect), _connectionCancel.Token);
        }

        public void ConfigureAsOPCUA(string host,int port)
        {
            _host = host;
            _port = port;
            _connType = ConnType.OPCUA;
        }

        public void ConfigureAsOPCDA(string host,string progID, string classID)
        {
            _host = host;
            _progID = progID;
            _classID = classID;
            _connType = ConnType.OPCDA;
        }
     

        private void ConnectAsync()
        {
            _connectionCancel = new CancellationTokenSource();
            _connectionTask = Task.Factory.StartNew(new Action(DoConnect), _connectionCancel.Token);
        }


        private async void DoConnect()
        {

            DateTime startTime = Globals.FDANow();
            Stopwatch connectionTimer = new Stopwatch();

            switch (_connType)
            {
                case ConnType.OPCUA:
                    if (_OPCClient != null)
                        _OPCClient.Dispose();
                    _OPCClient = new UAClient(_host, _port); break;
                case ConnType.OPCDA:
                    if (_OPCClient != null)
                        _OPCClient.Dispose();
                    _OPCClient = new DAClient(_host, _progID, _classID); break;
                case ConnType.MQTT: // future
                    break;
            }

            connectionTimer.Restart();
            if (_connType == ConnType.OPCUA || _connType == ConnType.OPCDA)
            {
                _OpcSubLookup.Clear();
                bool result = false;

                while (ConnectionStatus != ConnStatus.Connected_Ready && !_connectionCancel.IsCancellationRequested && ConnectionEnabled == true)
                {
                    int attemptCount = 0;
                    while (!result && attemptCount < MaxSocketConnectionAttempts && !_connectionCancel.IsCancellationRequested)
                    {                       
                        ConnectionStatus = ConnStatus.Connecting;
                        result = _OPCClient.Connect();
                        attemptCount++;
                    }

                    if (_connectionCancel.IsCancellationRequested)
                    {
                        LogConnectionCommsEvent(attemptCount, Globals.FDANow(), connectionTimer.Elapsed, 0, "Connection: " + Description + " Connection attempt cancelled");
                        break;
                    }

                    if (!result)
                    {
                        LogConnectionCommsEvent(attemptCount,Globals.FDANow(), connectionTimer.Elapsed, 0, "Connection " + Description + " Failed to connect to " + _host);
                        LogCommsEvent(Globals.FDANow(), "Connection: " + Description + " Initiating reconnection delay of " + SocketConnectionRetryDelay + " second(s)");

                        ConnectionStatus = ConnStatus.ConnectionRetry_Delay;
                        await Task.Delay(_socketConnectionRetryDelay * 1000, _connectionCancel.Token);
                    }
                    else
                    {
                        // connection successfull
                        LogConnectionCommsEvent(attemptCount, startTime, connectionTimer.Elapsed, 1, "Connection: " + Description + " Connected");
                        if (_postConnectionCommsDelay > 0)
                        {
                            LogCommsEvent(Globals.FDANow(), "Connection: " + Description + " Post Connection delay of " + PostConnectionCommsDelay + " ms");
                            ConnectionStatus = ConnStatus.Connected_Delayed;
                            await Task.Delay(_postConnectionCommsDelay, _connectionCancel.Token);
                        }

                        ConnectionStatus = ConnStatus.Connected_Ready;
                        _OPCClient.DataChange += _OPCClient_DataChange;
                        _OPCClient.BreakDetected += _OPCClient_BreakDetected;

                        // apply subscriptions
                  
                        foreach (DataSubscription sub in _subscriptions.Values)
                        {
                            ApplySubscription(sub);
                        }
                    }
                }

            }

      

        }

        private void _OPCClient_BreakDetected()
        {
            ConnectionStatus = ConnStatus.Disconnected;
            LogConnectionCommsEvent(0,Globals.FDANow(),TimeSpan.Zero, 0, "Connection " + Description + " connection broken");

            if (ConnectionEnabled)
            {
                ConnectAsync(); // try to re-connect
            }
        }

        private void _OPCClient_DataChange(string NodeID,int ns, OpcValue opcvalue)
        {
            DateTime data_timestamp = Globals.FDANow();
            
            if (!_subscriptions.ContainsKey(NodeID + ";" + ns))
                return;

            DataSubscription sub = _subscriptions[NodeID + ";" + ns];

            DataRequest request = new DataRequest()
            {
                Protocol = "OPC",
                ConnectionID = _connectionID,
                MessageType = DataRequest.RequestType.Read,
                Destination = sub.destination_table,
                DBWriteMode = DataRequest.WriteMode.Insert,
                GroupID = Guid.Empty,
                RequestBytes = new byte[] { },
                ResponseBytes = new byte[] { },
                RequestTimestamp = data_timestamp,
                ResponseTimestamp = data_timestamp,
                ErrorMessage = "",
                NodeID = sub.subscription_path
            };

            if (opcvalue.Status.IsGood)
                request.SetStatus(DataRequest.RequestStatus.Success);
            else
                request.SetStatus(DataRequest.RequestStatus.Error);


            Guid tagID = sub.datapoint_definition_ref;

            Tag thisTag = new Tag(tagID)
            {
                Value = Convert.ToDouble(opcvalue.Value),
                Timestamp = (DateTime)opcvalue.SourceTimestamp,
                ProtocolDataType = DataType.UNKNOWN
            };

            if (opcvalue.Status.IsGood)
                thisTag.Quality = 192;
            else
                thisTag.Quality = 0;

            request.TagList.Add(thisTag);

            TransactionLogItem logItem = new TransactionLogItem(request, 1, this.ConnectionID,Guid.Empty, 1,"1", "");
            Globals.SystemManager.LogCommsEvent(logItem);
            Thread.Sleep(1);


            TransactionEventArgs eventArgs = new TransactionEventArgs(request);
            DataUpdate?.Invoke(this, eventArgs);
        }

        private void Disconnect()
        {
            // cancel the async connect task if its running
            if (_connectionTask != null)
            {
                if (_connectionTask.Status == TaskStatus.Running)
                {
                    _connectionCancel.Cancel();
                    _connectionTask.Wait();
                }
            }

            // Disconnect from the OPC server
            if (_OPCClient != null)
            {
                if (_OPCClient.Connected)
                    _OPCClient.Disconnect();

                _OPCClient.DataChange -= _OPCClient_DataChange;
                _OPCClient.Dispose();
                _OpcSubLookup.Clear();
                //_subscriptions.Clear();
            }
         
            _OPCClient = null;

            ConnectionStatus = ConnStatus.Disconnected;
        }

        public void Subscribe(DataSubscription sub) 
        {
            _subscriptions.Add(sub.subscription_path,sub);

            if (ConnectionStatus == ConnStatus.Connected_Ready)
                ApplySubscription(sub);         
        }

        

        public void UnSubscribe(DataSubscription sub)
        {
            if (_connType == ConnType.OPCUA || _connType == ConnType.OPCDA)
            {
                if (_OpcSubLookup.ContainsKey(sub.subscription_path))
                {
                    _OpcSubLookup[sub.subscription_path].Unsubscribe();
                    _OpcSubLookup.Remove(sub.subscription_path);
                    _subscriptions.Remove(sub.subscription_path);
                    LogCommsEvent(Globals.FDANow(), "Unsubscribed from OPC tag '" + sub.subscription_path + "'");
                }
            }
        }

        public void UpdateSubEnabledStatus(DataSubscription sub)
        {
            string action = "";
            if (_connType == ConnType.OPCUA || _connType == ConnType.OPCDA)
            {
                if (_OpcSubLookup.ContainsKey(sub.subscription_path))
                {
                    if (sub.enabled)
                    {
                        _OpcSubLookup[sub.subscription_path].ChangeMonitoringMode(OpcMonitoringMode.Reporting);
                        action = " enabled";
                    }
                    else
                    {
                        _OpcSubLookup[sub.subscription_path].ChangeMonitoringMode(OpcMonitoringMode.Disabled);
                        action = "disabled";
                    }
                    LogCommsEvent(Globals.FDANow(), "OPC tag '" + sub.subscription_path + "' " + action);
                }
            }
        }

        private void ApplySubscription(DataSubscription sub)
        {
            if (_connType == ConnType.OPCUA || _connType == ConnType.OPCDA)
            {
    
                string[] tagParts = sub.subscription_path.Split(";", StringSplitOptions.RemoveEmptyEntries);
                int ns;
                if (tagParts.Length > 1)
                {
                    if (int.TryParse(tagParts[1], out ns))
                    {
                        OpcSubscription opcsub = _OPCClient.Subscribe(tagParts[0], ns);

                        // if the subscription is disabled, or communications are disabled at the connection level, set the subscription monitoring mode to 'disabled'
                        if (!sub.enabled || !CommunicationsEnabled)
                            opcsub.ChangeMonitoringMode(OpcMonitoringMode.Disabled);

                       _OpcSubLookup.Add(sub.subscription_path, opcsub);
                        LogCommsEvent(Globals.FDANow(), "Subscribed to OPC tag '" + sub.subscription_path + "'");
                    }
                }
                
            }
        }

        private void HandlePropertyChanged([CallerMemberName] string propertyName = "", long timestamp = 0)
        {
            // raise a property changed event (for property changes that don't require special handling)
            NotifyPropertyChanged(propertyName, timestamp);

            // property changes that require handling
            switch (propertyName)
            {
                case "ConnectionStatus": Globals.SystemManager.LogApplicationEvent(this, Description, " Connection status: " + ConnectionStatus.ToString()); break;

                case "ConnectionEnabled":
                    if (ConnectionEnabled)
                        ConnectAsync();
                    else
                        Disconnect();
                    break;
                case "CommunicationsEnabled":
                    if (_OPCClient == null)
                        return;
                    if (!CommunicationsEnabled)
                    {
                        if (_connType == ConnType.OPCUA || _connType == ConnType.OPCDA)
                        // disable all OPC subscriptions
                        foreach (OpcSubscription sub in _OPCClient.Subscriptions)
                        {
                            sub.ChangeMonitoringMode(OpcMonitoringMode.Disabled);
                        }
                    }
                    else
                    {
                        if (_connType == ConnType.OPCUA || _connType == ConnType.OPCDA)
                            // enable all OPC subscriptions
                            foreach (OpcSubscription sub in _OPCClient.Subscriptions)
                            {
                                sub.ChangeMonitoringMode(OpcMonitoringMode.Reporting);
                            }

                    }
                    break;
            }
        }

        public void ResetConnection()
        {
            Disconnect();
            ConnectAsync();
        }

        internal void LogCommsEvent(DateTime timestamp, string msg)
        {
            Globals.SystemManager.LogCommsEvent(ConnectionID, timestamp, msg);
            Thread.Sleep(1);
        }


        internal void LogConnectionCommsEvent(int attemptNum, DateTime startTime, TimeSpan elapsed, byte success, string message)
        {
            Globals.SystemManager.LogConnectionCommsEvent(ConnectionID, attemptNum, startTime, elapsed, success, message);
            Thread.Sleep(1);
        }

        public void Dispose()
        {
            Disconnect();
            _OPCClient?.Dispose();
        }
    }
}
