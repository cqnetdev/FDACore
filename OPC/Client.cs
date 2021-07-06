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
        protected List<OpcSubscription> _subscriptions;

        public delegate void DataChangeHandler(string NodeID, int ns, OpcValue value);
        public event DataChangeHandler DataChange;

        public delegate void StateChangeHandler(string state);
        public event StateChangeHandler StateChange;

        public bool Connected = false;

      public Client ()
        {
            _subscriptions = new List<OpcSubscription>();
        }

        public bool Connect()
        {
            bool result = true;
            try { _client.Connect(); } catch { result = false; }

            Connected = result;
            return result;

        }

        protected void RegisterForClientEvents()
        {
            _client.StateChanged += _client_StateChanged;
        }

        private void _client_StateChanged(object sender, OpcClientStateChangedEventArgs e)
        {
            if (e.NewState == OpcClientState.Disconnected)
                StateChange?.Invoke(e.NewState.ToString());
        }

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

    

        public abstract void Subscribe(string node,int ns);

      

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
