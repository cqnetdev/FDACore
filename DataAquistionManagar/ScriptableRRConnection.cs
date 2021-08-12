using Scripting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FDA
{
    public class ScriptableRRConnection : ScriptableObject
    {
        public static List<ScriptableRRConnection> WrapConn(Dictionary<Guid, RRConnectionManager> toWrap)
        {
            List<ScriptableRRConnection> output = new();
            foreach (RRConnectionManager connMgr in toWrap.Values)
            {
                output.Add(new ScriptableRRConnection(connMgr));
            }

            return output;
        }

        private readonly RRConnectionManager _connMgr;

        public bool ConnectionEnabled { get => _connMgr.ConnectionEnabled; set { _connMgr.ConnectionEnabled = value; OnPropertyChanged(); } }
        public bool CommsEnabled { get => _connMgr.CommunicationsEnabled; set { _connMgr.CommunicationsEnabled = value; OnPropertyChanged(); } }

        public override event PropertyChangedEventHandler PropertyChanged;

        public ScriptableRRConnection(RRConnectionManager connMgr) : base(connMgr.ID)
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