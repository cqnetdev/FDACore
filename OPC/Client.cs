using Opc.UaFx;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace OPC
{
  
    public abstract class Client : IDisposable
    {
        protected string _connectionString;
        protected OpcClient _client;

        public OpcSubscriptionReadOnlyCollection Subscriptions { get { if (_client != null) return _client.Subscriptions; else return new OpcSubscriptionReadOnlyCollection(new List<OpcSubscription>()); } }

        public delegate void DataChangeHandler(string NodeID, int ns, OpcValue value);
        public event DataChangeHandler DataChange;

        public delegate void BreakDetectedHandler();
        public event BreakDetectedHandler BreakDetected;

        private bool _breakDetectionArmed = false;


        public bool Connected = false;

        public Client ()
        {

        }

        public bool Connect()
        {
            bool result = true;
            try { _client.Connect(); } catch { result = false; }

            Connected = result;

            if (Connected)
                _breakDetectionArmed = true;

            return result;

        }

        protected void RegisterForClientEvents()
        {
            _client.KeepAlive.Updated += KeepAlive_Updated;
            //_client.UseBreakDetection = true;
            //_client.BreakDetected += _client_BreakDetected;
        }

        private void KeepAlive_Updated(object sender, EventArgs e)
        {
            if (_client.KeepAlive.ServerState != OpcServerState.Running && _breakDetectionArmed)
            {
                _breakDetectionArmed = false;
                BreakDetected?.Invoke();
            }
        }

        //private void _client_BreakDetected(object sender, EventArgs e)
        //{
        //    BreakDetected?.Invoke();
        //}

    

        public void Disconnect()
        {
            _client?.Disconnect();
        }

        public void GetNodes()
        {
            var node = _client.BrowseNode(OpcObjectTypes.ObjectsFolder);
            Browse(node);
        }

        private void Browse(OpcNodeInfo node, int level = 0)
        {
            Console.WriteLine("{0}{1}({2})",
                    new string('.', level * 4),
                    node.Attribute(OpcAttribute.DisplayName).Value,
                    node.NodeId);

            level++;

            foreach (var childNode in node.Children())
                Browse(childNode, level);
        }


        public abstract OpcValue Read(string node,int ns);
  

        public abstract OpcSubscription Subscribe(string node,int ns);


        protected void DataChangeReceived(object sender, OpcDataChangeReceivedEventArgs e)
        {
            
            OpcMonitoredItem item = (OpcMonitoredItem)sender;
            DataChange?.Invoke(item.NodeId.ValueAsString,item.NodeId.NamespaceIndex,e.Item.Value);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
