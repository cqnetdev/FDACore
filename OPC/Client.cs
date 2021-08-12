using Opc.UaFx;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;

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

        public Client()
        {
        }

        public bool Connect()
        {
            bool result = true;
            _client.Connect();
            _client.SessionTimeout = 120000;

            Connected = result;

            if (Connected)
                _breakDetectionArmed = true;

            return result;
        }

        protected void RegisterForClientEvents()
        {
            _client.KeepAlive.Updated += KeepAlive_Updated;
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

        public abstract OpcValue Read(string node, int ns);

        public List<OpcValue> ReadNodes(string nodelist, out List<Guid> datapointdefs)
        {
            List<OpcReadNode> toRead = new();
            List<Guid> datapoints = new();
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
            List<OpcValue> values = new(_client.ReadNodes(toRead));
            datapointdefs = datapoints;
            return values;
        }

        public OpcSubscription Subscribe(Common.DataSubscription subscriptionDef)
        {
            string[] nodes = subscriptionDef.monitored_items.Split("$");
            string[] nodeparts;
            int ns;
            string path;
            List<OpcSubscribeNode> nodesList = new();

            OpcDataChangeFilter filter;
            if (subscriptionDef.report_on_timestamp_change)
                filter = new OpcDataChangeFilter(OpcDataChangeTrigger.StatusValueTimestamp);
            else
                filter = new OpcDataChangeFilter(OpcDataChangeTrigger.StatusValue);

            switch (subscriptionDef.deadband_type.ToLower())
            {
                case "percent": filter.DeadbandType = OpcDeadbandType.Percent; break;
                case "absolute": filter.DeadbandType = OpcDeadbandType.Absolute; break;
                default: filter.DeadbandType = OpcDeadbandType.None; break;
            }
            filter.DeadbandValue = subscriptionDef.deadband;

            // create an empty subscription
            OpcSubscription sub = _client.SubscribeNodes();

            // create a monitoredItem for each tag to be monitored and add it to the subscription
            OpcMonitoredItem thisItem;
            foreach (string node in nodes)
            {
                nodeparts = node.Split(":");
                ns = int.Parse(nodeparts[0]);
                path = nodeparts[1];
                thisItem = new OpcMonitoredItem(new OpcNodeId(path, ns), OpcAttribute.Value);
                thisItem.DataChangeReceived += DataChangeReceived;
                thisItem.Tag = Guid.Parse(nodeparts[2]);
                thisItem.Filter = filter;
                sub.AddMonitoredItem(thisItem);
            }

            //set the interval (milliseconds, 0 = whenever the value changes)
            sub.PublishingInterval = subscriptionDef.interval;
            sub.PublishingIsEnabled = true;

            // make the server aware of the changes to the subscription
            sub.ApplyChanges();

            // set the Tag property of the subscription to the DataSubscription object that came from the database for later reference
            sub.Tag = subscriptionDef;

            // set the subscription-enabled status
            if (subscriptionDef.enabled)
                sub.ChangeMonitoringMode(OpcMonitoringMode.Reporting);
            else
                sub.ChangeMonitoringMode(OpcMonitoringMode.Disabled);

            sub.StartPublishing();

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
            GC.SuppressFinalize(this);
        }
    }
}