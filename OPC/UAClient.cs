using System;
using System.Collections.Generic;
using System.Text;
using Opc.UaFx;
using Opc.UaFx.Client;

namespace OPC
{
    public class UAClient: Client
    {
        /// <summary>
        /// Create an OPC UA Client
        /// </summary>
        /// <param name="host">The hostname or IP Address of the OPC UA server</param>
        /// <param name="port">The port to use for connecting to the OPC UA Server</param>
        public UAClient(string host, int port)
        {        
            _client = new OpcClient("opc.tcp://" + host + ":" + port);
        }

        public override void Subscribe(string node)
        {
            OpcSubscription sub = _client.SubscribeDataChange(node, base.DataChangeReceived);
        }

        public override OpcValue Read(string node)
        {
            OpcValue value = _client.ReadNode(node);
            return value;
        }
    }
}
