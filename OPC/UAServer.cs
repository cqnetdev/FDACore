using Opc.UaFx;
using Opc.UaFx.Server;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OPC
{
    public class UAServer : IDisposable
    {
        private OpcServer _server;

        //private OpcNodeManager _nodeManager;
        private readonly OpcDataVariableNode<double> _temperatureNode;

        public UAServer(string host)
        {
            //_nodeManager = new NodeManager();
            _temperatureNode = new OpcDataVariableNode<double>("Temperature", 100.0);

            _server = new OpcServer(host, _temperatureNode);
            _server.Start();

            Task task = Task.Factory.StartNew(TemperatureMaker);
        }

        public void Dispose()
        {
            _server?.Stop();
            _server?.Dispose();
            _server = null;

            GC.SuppressFinalize(this);
        }

        private void TemperatureMaker()
        {
            //           _temperatureNode = new OpcDataVariableNode<double>("Temperature", 100.0);
            while (true)
            {
                if (_temperatureNode.Value == 110)
                    _temperatureNode.Value = 100;
                else
                    _temperatureNode.Value++;

                _temperatureNode.ApplyChanges(_server.SystemContext);
                Thread.Sleep(1000);
            }
        }
    }
}