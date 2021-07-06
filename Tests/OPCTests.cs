using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OPC;
using Opc.UaFx;
using System.Threading;
using Common;

namespace Tests
{
    [TestClass]
    public class OPCTests
    {
        static Guid TagID = Guid.NewGuid();
        static bool updateReceived = false;

        [TestMethod]
        public void OPCConnectAndReadTest()
        {
            // connect to local kepware server (UA)
            OPC.Client client = new UAClient("127.0.0.1", 49320);
            Assert.IsTrue(client.Connect());

            // demand read a value
            OpcValue value = client.Read("Simulation Examples.Functions.Ramp2", 2);
            Assert.IsTrue(value.Status.IsGood);


            // subscribe to a value
            client.DataChange += Client_DataChange;
            client.Subscribe("Simulation Examples.Functions.Ramp2", 2);  // this format is outdated

            // wait up to 2 seconds for a DataChanged event
            Thread.Sleep(2000);

            // and clean up
            client.DataChange -= Client_DataChange;
            client.Dispose();
          


            // now do it again, using OPC DA
            client = new DAClient("127.0.0.1", "Kepware.KEPServerEX.V6", "{7BC0CC8E-482C-47CA-ABDC-0FE7F9C6E729}"); Assert.IsTrue(client.Connect());

            // demand read a value
            value = client.Read("Simulation Examples.Functions.Ramp2", 2);
            Assert.IsTrue(value.Status.IsGood);


            // subscribe to a value
            client.DataChange += Client_DataChange;
            //client.Subscribe("Simulation Examples.Functions.Ramp2", 2);   this format is outdated

            // wait up to 2 seconds for a DataChanged event
            Thread.Sleep(2000);

            // and clean up
            client.DataChange -= Client_DataChange;
            client.Dispose();

        }

        private void Client_DataChange(string NodeID,int ns, dynamic value)
        {
            Assert.IsTrue(value.Status.IsGood);
        }


        [TestMethod]
        public void PubSubConnectionManagerTest()
        {
            FDA.PubSubConnectionManager connMgr = new FDA.PubSubConnectionManager(Guid.NewGuid(), "Kepware connection");
            connMgr.ConfigureAsOPCUA("127.0.0.1", 49320);
            connMgr.CommunicationsEnabled = true;
            connMgr.ConnectionEnabled = true;

            // give it a couple seconds to connect
            Thread.Sleep(5000);

            // confirm that it's connected
            Assert.IsTrue(connMgr.ConnectionStatus == FDA.PubSubConnectionManager.ConnStatus.Connected_Ready);

            // handle the manager's "DataUpdate" event
            connMgr.DataUpdate += ConnMgr_DataUpdate;

            // Subscribe to a tag on the OPC server
           // connMgr.Subscribe(TagID, "Simulation Examples.Functions.Ramp2;2"); this format is outdated

            updateReceived = false;

            // wait up to 2 seconds for a DataChanged event
            Thread.Sleep(2000);

            Assert.IsTrue(updateReceived);

        }

        private void ConnMgr_DataUpdate(object sender, Common.TransactionEventArgs e)
        {
            Assert.AreEqual(TagID, e.RequestRef.TagList[0].TagID);
            updateReceived = true;
        }

    }

   
}
