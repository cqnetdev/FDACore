using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Common;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Diagnostics;

namespace Tests
{
    [TestClass]
    public class TCPServerTests
    {
        private static FDA.TCPServer _testServer;
            
            
        static TCPServerTests()
        {
            _testServer = FDA.TCPServer.NewTCPServer(12345);
            _testServer.DataAvailable += _testServer_DataAvailable;
            _testServer.Start();
        }

        private static void _testServer_DataAvailable(object sender, FDA.TCPServer.TCPCommandEventArgs e)
        {
            // when data is received on the test server, echo it back
            string received = Encoding.UTF8.GetString(e.Data);
            _testServer.Send(e.ClientID, received);
        }

        private static Random random = new Random();
     
        [TestMethod]
        public void TCPEchoTest()
        {
            TcpClient client = new TcpClient();
            client.Connect("127.0.0.1", 12345);

            string testString = "this is a test" + random.Next();
            string response = "";

            byte[] testData = Encoding.UTF8.GetBytes(testString);

            Stopwatch writeTimer = new Stopwatch();
            Stopwatch waitforResponseTimer = new Stopwatch();
            Stopwatch readTimer = new Stopwatch();

            int waitLimit = 500;
            int wait = 0;
            for (int i = 0; i < 100; i++)
            {
                writeTimer.Start();
                client.GetStream().Write(testData, 0, testData.Length);
                writeTimer.Stop();

                waitforResponseTimer.Start();
                wait = 0;
                while (!client.GetStream().DataAvailable && wait < waitLimit)
                {
                    Thread.Sleep(50);
                    wait += 50;
                }
                waitforResponseTimer.Stop();

                readTimer.Start();
                byte[] buffer = new byte[1000];
                int responsesize;
                if (client.GetStream().DataAvailable)
                {
                    responsesize = client.GetStream().Read(buffer, 0, buffer.Length);
                    response = Encoding.UTF8.GetString(buffer, 0, responsesize);
                }
                else
                    response = "no response";
                readTimer.Stop();
                Assert.AreEqual(testString, response);
            }

            Console.WriteLine("Writing: " + writeTimer.ElapsedMilliseconds);
            Console.WriteLine("Waiting: " + waitforResponseTimer.ElapsedMilliseconds);
            Console.WriteLine("Reading: " + readTimer.ElapsedMilliseconds);
        }
    }
}
