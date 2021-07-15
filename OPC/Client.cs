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

        public delegate void DataChangeHandler(OpcMonitoredItem item);
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
            _client.Connect(); 

            Connected = result;

            if (Connected)
                _breakDetectionArmed = true;

            return result;
        }

        protected void RegisterForClientEvents()
        {
            _client.KeepAlive.Updated += KeepAlive_Updated;
            _client.DataChangeReceived += DataChangeReceived;
        }

     

        private void KeepAlive_Updated(object sender, EventArgs e)
        {
            if (_client.KeepAlive.ServerState != OpcServerState.Running && _breakDetectionArmed)
            {
                _breakDetectionArmed = false;
                Connected = false;
                BreakDetected?.Invoke();
            }
        }

   

        public void Disconnect()
        {
            _client?.Disconnect();
            Connected = false;

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

        public List<OpcValue> ReadNodes(string nodelist,out List<Guid> datapointdefs)
        {
            List<OpcReadNode> toRead = new List<OpcReadNode>();
            List<Guid> datapoints = new List<Guid>();
            string[] nodes = nodelist.Split("$", StringSplitOptions.RemoveEmptyEntries);
            string[] nodeparts;
            int ns;
            foreach (string node in nodes)
            {
                nodeparts = node.Split(":", StringSplitOptions.RemoveEmptyEntries);
                ns = int.Parse(nodeparts[0]);
                toRead.Add(new OpcReadNode(nodeparts[1], ns));
                datapoints.Add(Guid.Parse(nodeparts[2]));
            }
           List<OpcValue> values = new List<OpcValue>(_client.ReadNodes(toRead));
            datapointdefs = datapoints;
            return values;
        }

        public OpcSubscription Subscribe(Common.DataSubscription subscriptionDef)
        {
            string[] nodes = subscriptionDef.monitored_items.Split("$");
            string[] nodeparts;
            int ns;
            string path;
            List<OpcSubscribeNode> nodesList = new List<OpcSubscribeNode>();
            OpcSubscribeNode thisNode;

            foreach (string node in nodes)
            {
                nodeparts = node.Split(":");
                ns = int.Parse(nodeparts[0]);
                path = nodeparts[1];
                thisNode = new OpcSubscribeNode(path, ns);
            
                nodesList.Add(thisNode);
            }

            OpcSubscription sub = _client.SubscribeNodes(nodesList);
           
            sub.Tag = subscriptionDef;
            return sub;
        }

        protected void DataChangeReceived(object sender, OpcDataChangeReceivedEventArgs e)
        {
            OpcMonitoredItem item = e.MonitoredItem;
          
            DataChange?.Invoke(item);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }

  
}
