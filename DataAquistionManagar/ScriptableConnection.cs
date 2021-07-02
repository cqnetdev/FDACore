using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Scripting;
using Common;

namespace FDA
{

    public class ScriptableConnection : ScriptableObject
    {
        public static List<ScriptableConnection> WrapConn(Dictionary<Guid, RRConnectionManager> toWrap)
        {
            List<ScriptableConnection> output = new List<ScriptableConnection>();
            foreach (RRConnectionManager connMgr in toWrap.Values)
            {
                output.Add(new ScriptableConnection(connMgr));
            }

            return output;
        }

        private RRConnectionManager _connMgr;

        public bool ConnectionEnabled { get => _connMgr.ConnectionEnabled; set { _connMgr.ConnectionEnabled = value; OnPropertyChanged(); } }
        public bool CommsEnabled { get => _connMgr.CommunicationsEnabled; set { _connMgr.CommunicationsEnabled = value; OnPropertyChanged(); } }

        public override event PropertyChangedEventHandler PropertyChanged;

        public ScriptableConnection(RRConnectionManager connMgr) : base(connMgr.ID)
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
