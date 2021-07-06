using System;
using System.Collections.Generic;
using System.Text;
using Opc.UaFx;
using Opc.UaFx.Client;
using Opc.UaFx.Client.Classic;

namespace OPC
{
    public class DAClient : Client
    {

        /// <summary>
        /// Create an OPC DA (classic) Client
        /// </summary>
        /// <param name="host">The hostname or IP Address of the OPC server</param>
        /// <param name="port">he port to use for connecting to the OPC UA Server</param>
        /// <param name="progID">The program ID of the OPC Server</param>
        /// <param name="classID">The class ID of the OPC Server </param>
        public DAClient(string host,string progID,string classID)
        {
            _connectionString = "opc.com://" + host + ":/" + progID + "/" + classID;
            _client = new OpcClient(_connectionString);
            base.RegisterForClientEvents();
        }


        public override void Subscribe(string node,int ns)
        {
            OpcSubscription sub = _client.SubscribeDataChange("ns=" + ns + ";s=0:" + node, DataChangeReceived);
        }

        public override OpcValue Read(string node, int ns)
        {
            OpcValue value = _client.ReadNode("ns=" + ns + ";s=0:" + node);
           
            return value;
        }
    }
}
