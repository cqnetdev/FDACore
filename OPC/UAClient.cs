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
            _connectionString = "opc.tcp://" + host + ":" + port;
            _client = new OpcClient(_connectionString);
            base.RegisterForClientEvents();
        }

     

        public override OpcSubscription Subscribe(string node,int ns)
        {
            return _client.SubscribeDataChange("ns=" + ns + ";s=" + node, DataChangeReceived);
        }



        public override OpcValue Read(string node,int ns)
        {
            OpcValue value = _client.ReadNode("ns=" + ns + ";s=" + node);
            return value;
        }
    }
}
