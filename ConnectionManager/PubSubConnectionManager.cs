using Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OPC;
using System.Runtime.CompilerServices;
using Opc.UaFx;

namespace FDA
{
    // Manages communications for protocols that follow the Publish/Subscribe communication pattern
    public class PubSubConnectionManager : SubscriptionManager.SubscribeableObject, IDisposable
    {
        // public properties
        public int SocketConnectionRetryDelay { get => _socketConnectionRetryDelay; set { _socketConnectionRetryDelay = value; HandlePropertyChanged(); } }
        public int PostConnectionCommsDelay { get => _postConnectionCommsDelay; set { _postConnectionCommsDelay = value; HandlePropertyChanged(); } }
        public bool ConnectionEnabled { get => _connectionEnabled; set { _connectionEnabled = value; HandlePropertyChanged(); } }
        public bool CommunicationsEnabled { get => CommunicationsEnabled; set { _communicationsEnabled = value; HandlePropertyChanged(); } }
        public ConnStatus ConnectionStatus { get => _connStatus; private set { _connStatus = value; HandlePropertyChanged(); } }
        public Guid ConnectionID { get => _connectionID; }
        public string Description { get => _description; } 
        public int MaxSocketConnectionAttempts { get => _maxSocketConnectionAttempts; set { _maxSocketConnectionAttempts = value; HandlePropertyChanged(); } }
        public bool CommsLogEnabled { get => _commsLogEnabled; set { _commsLogEnabled = value; HandlePropertyChanged(); } }

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

     
        public PubSubConnectionManager(Guid id, string description) 
        {
            base.ID = id.ToString();
            base.ObjectType = "connection";

            _connectionID = id;
            _description = description;
            _connectionCancel = new CancellationTokenSource();
            _connectionTask = new Task(new Action(DoConnect), _connectionCancel.Token);
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
            //_connectionCancel = new CancellationTokenSource();
            //_connectionTask = Task.Factory.StartNew(new Action(DoConnect), _connectionCancel.Token);
            _connectionTask.Start(); // run DoConnect() asynchronously
        }


        private async void DoConnect()
        {
            switch (_connType)
            {
                case ConnType.OPCUA:
                    _OPCClient = new UAClient(_host, _port); break;
                case ConnType.OPCDA:
                    _OPCClient = new DAClient(_host, _progID, _classID); break;
                case ConnType.MQTT: // future
                    break;
            }


            if (_connType == ConnType.OPCUA || _connType == ConnType.OPCDA)
            {
                bool result = false;

                while (ConnectionStatus == ConnStatus.Disconnected && !_connectionCancel.IsCancellationRequested)
                {
                    int attemptCount = 0;
                    while (!result && attemptCount < MaxSocketConnectionAttempts && !_connectionCancel.IsCancellationRequested)
                    {
                        ConnectionStatus = ConnStatus.Connecting;
                        result = _OPCClient.Connect();
                        attemptCount++;
                    }

                    if (_connectionCancel.IsCancellationRequested)
                        break;

                    if (!result)
                    {
                        // failed to connect, wait for the reconnect delay time and try again later
                        ConnectionStatus = ConnStatus.ConnectionRetry_Delay;
                        await Task.Delay(_socketConnectionRetryDelay * 1000, _connectionCancel.Token);
                    }
                    else
                    {
                        // connection successfull
                        if (_postConnectionCommsDelay > 0)
                        {
                            ConnectionStatus = ConnStatus.Connected_Delayed;
                            await Task.Delay(_postConnectionCommsDelay, _connectionCancel.Token);
                        }

                        ConnectionStatus = ConnStatus.Connected_Ready;
                        _OPCClient.DataChange += _OPCClient_DataChange;
                        _OPCClient.StateChange += _OPCClient_StateChange;

                        // apply subscriptions
                        foreach (DataSubscription sub in _subscriptions.Values)
                        {
                            ApplySubscription(sub);
                        }
                    }
                }

                if (!result)
                    ConnectionStatus = ConnStatus.Disconnected;
            }
            
        }

        private void _OPCClient_StateChange(string state)
        {
            if (state == "Disconnected")
            {
                _connStatus = ConnStatus.Disconnected;
                
                // if the connection is enabled, try to re-connect
                if (_connectionEnabled)
                    ConnectAsync();
            }
        }

        private void _OPCClient_Disconnected()
        {
            _connStatus = ConnStatus.Disconnected;
        }

        private void _OPCClient_DataChange(string NodeID,int ns, OpcValue opcvalue)
        {

            DataSubscription sub = _subscriptions[NodeID + ";" + ns];

            DataRequest request = new DataRequest()
            {
                Protocol = "OPC",
                ConnectionID = _connectionID,
                MessageType = DataRequest.RequestType.Read,
                Destination = sub.destination_table,
                DBWriteMode = DataRequest.WriteMode.Insert
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
            

            TransactionEventArgs eventArgs = new TransactionEventArgs(request);
            DataUpdate?.Invoke(this, eventArgs);
        }

        private void Disconnect()
        {
            // cancel the async connect task if its running
            if (_connectionTask.Status == TaskStatus.Running)
            {
                _connectionCancel.Cancel();
                _connectionTask.Wait();
            }

            // Disconnect from the OPC server
            if (_OPCClient.Connected)
                _OPCClient.Disconnect();

            _OPCClient.DataChange -= _OPCClient_DataChange;

            _OPCClient.Dispose();
            _OPCClient = null;

            _connStatus = ConnStatus.Disconnected;
        }

        public void Subscribe(DataSubscription sub) // for OPC, tagPath is followed by a ; and then namespace id
        {
            _subscriptions.Add(sub.subscription_path,sub);

            if (ConnectionStatus == ConnStatus.Connected_Ready)
                ApplySubscription(sub);         
        }

        private void ApplySubscription(DataSubscription sub)
        {
            if (_connType == ConnType.OPCUA || _connType == ConnType.OPCDA)
            {
    
                string[] tagPargs = sub.subscription_path.Split(";", StringSplitOptions.RemoveEmptyEntries);
                int ns;
                if (tagPargs.Length > 1)
                {
                    if (int.TryParse(tagPargs[1], out ns))
                    {
                        _OPCClient.Subscribe(tagPargs[0], ns);
                    }
                }
                
            }
        }

        private void HandlePropertyChanged([CallerMemberName] string propertyName = "", long timestamp = 0)
        {
            // raise a property changed event (for property changes that don't require special handling)
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
            }
        }


            public void Dispose()
        {
            if (_connectionTask.Status == TaskStatus.Running)
            {
                _connectionCancel.Cancel();
                _connectionTask.Wait();
            }

         
            _OPCClient?.Dispose();
        }
    }
}
