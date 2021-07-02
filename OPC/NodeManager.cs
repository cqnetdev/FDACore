using System;
using System.Collections.Generic;
using System.Text;
using Opc.UaFx;
using Opc.UaFx.Server;

namespace OPC
{
    class NodeManager : OpcNodeManager
    {

        public NodeManager() : base("http://FDA/")
        {

        }

        protected override IEnumerable<IOpcNode> CreateNodes(OpcNodeReferenceCollection references)
        {
            // Define custom root node.
            var FDANode = new OpcFolderNode(new OpcName("FDA", this.DefaultNamespaceIndex));

            // Add custom root node to the Objects-Folder (the root of all server nodes):
            references.Add(FDANode, OpcObjectTypes.ObjectsFolder);

            // Add custom sub node beneath of the custom root node:
            //var isMachineRunningNode = new OpcDataVariableNode<bool>(FDANode, "IsRunning");

            // Return each custom root node using yield return.
            yield return FDANode;
        }

    }
}
