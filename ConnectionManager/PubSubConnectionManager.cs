using Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OPC;

namespace ConnectionManager
{
    // Manages communications for protocols that follow the Publish/Subscribe communication pattern
    class PubSubConnectionManager : SubscriptionManager.SubscribeableObject, IDisposable
    {
        // enums
        public enum ConnType {OPCUA,OPCDA,MQTT};
        public enum ConnStatus { Disconnected, ConnectionRetry_Delay, Connected_Ready, Connecting, Connected_Delayed }

        // connection settings from database
        private int _socketConnectionRetryDelay = 10;     // seconds
        private int _postConnectionCommsDelay = 0;        // milliseconds

        // pubsub common member variables
        private ConnType _connType;
        private string _host;
        private int _port;
        private ConnStatus _connStatus = ConnStatus.Disconnected;
        private CancellationTokenSource _connectionCancel;
        private Task _connectionTask;
        private List<string> _subscriptions = new List<string>();


       
        // OPC specific member variables
        private OPC.Client _OPCClient;
        private string _progID;  // OPC DA only;
        private string _classID; // OPC DA only;

     
        public PubSubConnectionManager(Guid id, string description) : base(id.ToString(),description)
        {
            
        }

        public void StartOPCUA(string host,int port)
        {
            _host = host;
            _port = port;
            _connType = ConnType.OPCUA;

            Start();
        }

        public void StartOPCDA(string host,string progID, string classID)
        {
            _host = host;
            _progID = progID;
            _classID = classID;
            _connType = ConnType.OPCDA;

            Start();
        }
     

        private void Start()
        {
            _connectionCancel = new CancellationTokenSource();
            _connectionTask = Task.Factory.StartNew(new Action(Connect), _connectionCancel.Token);
        }


        private async void Connect()
        {
            switch (_connType)
            {
                case ConnType.OPCUA:
                    _OPCClient = new OPC.UAClient(_host, _port); break;
                case ConnType.OPCDA:
                    _OPCClient = new OPC.DAClient(_host, _progID, _classID); break;
                case ConnType.MQTT: // future
                    break;

            }

            if (_connType == ConnType.OPCUA || _connType == ConnType.OPCDA)
            { 
                bool result;
                while (_connStatus == ConnStatus.Disconnected && !_connectionCancel.IsCancellationRequested)
                {
                    _connStatus = ConnStatus.Connecting;
                    result = _OPCClient.Connect();
                    
                    if (!result)
                    {
                        // failed to connect, wait for the reconnect delay time and try again
                        _connStatus = ConnStatus.Disconnected;
                        await Task.Delay(_socketConnectionRetryDelay * 1000, _connectionCancel.Token);
                    }
                    else
                    {
                        // connection successfull
                        if (_postConnectionCommsDelay > 0)
                        {
                            _connStatus = ConnStatus.Connected_Delayed;
                            await Task.Delay(_postConnectionCommsDelay, _connectionCancel.Token);
                            _connStatus = ConnStatus.Connected_Ready;
                        }
                    }
                }
            }
            
        }

        public void Subscribe(string tagPath)
        {
            _subscriptions.Add(tagPath);
            
            if (_connType == ConnType.OPCUA || _connType == ConnType.OPCDA)
            {
                _OPCClient.Subscribe(tagPath);
            }
        }

        public void Subscribe(List<string> tagPathList)
        {
           

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
