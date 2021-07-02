using Opc.UaFx;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace OPC
{
  
    public abstract class Client : IDisposable
    {
        protected OpcClient _client;
        protected List<OpcSubscription> _subscriptions;

        public delegate void DataChangeHandler(string NodeID, dynamic value);
        public event DataChangeHandler DataChange;

      public Client ()
        {
            _subscriptions = new List<OpcSubscription>();
        }

        public bool Connect()
        {
            bool result = true;
            try { _client.Connect(); } catch { result = false; }

            return result;

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


        public abstract OpcValue Read(string node);

    

        public abstract void Subscribe(string node);

        public void Subscribe(List<string> nodes)
        {
            foreach (string node in nodes)
                Subscribe(node);
        }

        protected void DataChangeReceived(object sender, OpcDataChangeReceivedEventArgs e)
        {
            OpcMonitoredItem item = (OpcMonitoredItem)sender;
            DataChange?.Invoke(item.NodeId.ValueAsString, e.Item.Value);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
