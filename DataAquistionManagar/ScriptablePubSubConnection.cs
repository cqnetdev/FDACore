using Scripting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FDA
{
    public class ScriptablePubSubConnection : ScriptableObject
    {
        public static List<ScriptablePubSubConnection> WrapConn(Dictionary<Guid, PubSubConnectionManager> toWrap)
        {
            List<ScriptablePubSubConnection> output = new();
            foreach (PubSubConnectionManager connMgr in toWrap.Values)
            {
                output.Add(new ScriptablePubSubConnection(connMgr));
            }

            return output;
        }

        private readonly PubSubConnectionManager _connMgr;

        public bool ConnectionEnabled { get => _connMgr.ConnectionEnabled; set { _connMgr.ConnectionEnabled = value; OnPropertyChanged(); } }
        public bool CommsEnabled { get => _connMgr.CommunicationsEnabled; set { _connMgr.CommunicationsEnabled = value; OnPropertyChanged(); } }

        public override event PropertyChangedEventHandler PropertyChanged;

        public ScriptablePubSubConnection(PubSubConnectionManager connMgr) : base(connMgr.ID)
        {
            _connMgr = connMgr;
            _connMgr.PropertyChanged += ConnMgr_PropertyChanged;
        }

        private void ConnMgr_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}