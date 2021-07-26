using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using Common;
using System.Runtime.CompilerServices;
using FDAManager;

namespace FDAManager
{

    public partial class ConnDetailsCtrl : UserControl
    {
        private ConnDetails _connDetailsObj;
        public ConnDetails ConnDetailsObj { get { return _connDetailsObj; } set { _connDetailsObj = value; UpdateDataBinding(); } }
        public string DbConnStr { get => _dbConnStr; set { _dbConnStr = value;} }
        public string FDAExecutionID { get => _executionID; set { _executionID = value;  } }

        private string _dbConnStr = "";
        private string _executionID = "";

        public ConnDetailsCtrl()
        {
            InitializeComponent();
        }

  
        private void UpdateDataBinding()
        {            
            foreach (Control ctrl in this.Controls)
            {
                if (ctrl.GetType() == typeof(GroupBox))
                {
                    foreach (Control subctrl in ((GroupBox)(ctrl)).Controls)
                    {
                        if (subctrl.GetType() == typeof(FancyTextBox))
                            ((FancyTextBox)subctrl).Clear();
                    }
                }
            }
            

            if (_connDetailsObj == null)
                return;

            foreach (Control ctrl in this.Controls)
            {
                if (ctrl.GetType() == typeof(GroupBox))
                {
                    foreach (Control subctrl in ((GroupBox)(ctrl)).Controls)
                    {
                        if (subctrl.GetType() == typeof(FancyTextBox) && subctrl.Tag != null)
                        {
                            ((TextBox)subctrl).DataBindings.Add("Text", _connDetailsObj, subctrl.Tag.ToString());
                        }
                    }
                }
            }

            Refresh();
        }

 

        public class ConnDetails : INotifyPropertyChanged
        {
            private string _ID;
            private bool _connEnabled;
            private bool _commsEnabled;
            private string _connectionType;
            private long _lastComms;
            private int _requestRetryDelay;
            private int _socketConnectionAttemptTimeout;
            private int _maxSocketConnectionAttempts;
            private int _socketConnectionRetryDelay;
            private int _postConnectionCommsDelay;
            private int _interRequestDelay;
            private int _maxRequestAttempts;
            private int _requestResponseTimeout;
            private string _connectionStatus;
            private bool _idleDisconnect;
            private int _idleDisconnectTime;
            private string _description;
            private int _priority0QueueCount;
            private int _priority1QueueCount;
            private int _priority2QueueCount;
            private int _priority3QueueCount;
            private string _conndetail;

            private DateTime _lastCommsDT;



            public string ID { get { return _ID; } set { _ID = value; NotifyPropertyChanged(); } }
            public bool ConnectionEnabled { get { return _connEnabled; } set { _connEnabled = value; NotifyPropertyChanged(); } }
            public bool CommunicationsEnabled { get { return _commsEnabled; } set { _commsEnabled = value; NotifyPropertyChanged(); } }
            public string ConnectionType { get { return _connectionType; } set { _connectionType = value; NotifyPropertyChanged(); } }
            public long LastCommsTime { get { return _lastComms; } set { _lastComms = value; NotifyPropertyChanged(); } }
            public int RequestRetryDelay { get { return _requestRetryDelay; } set { _requestRetryDelay = value; NotifyPropertyChanged(); } }
            public int SocketConnectionAttemptTimeout { get { return _socketConnectionAttemptTimeout; } set { _socketConnectionAttemptTimeout = value; NotifyPropertyChanged(); } }
            public int MaxSocketConnectionAttempts { get { return _maxSocketConnectionAttempts; } set { _maxSocketConnectionAttempts = value; NotifyPropertyChanged(); } }
            public int SocketConnectionRetryDelay { get { return _socketConnectionRetryDelay; } set { _socketConnectionRetryDelay = value; NotifyPropertyChanged(); } }
            public int PostConnectionCommsDelay { get { return _postConnectionCommsDelay; } set { _postConnectionCommsDelay = value; NotifyPropertyChanged(); } }
            public int InterRequestDelay { get { return _interRequestDelay; } set { _interRequestDelay = value; NotifyPropertyChanged(); } }
            public int MaxRequestAttempts { get { return _maxRequestAttempts; } set { _maxRequestAttempts = value; NotifyPropertyChanged(); } }
            public int RequestResponseTimeout { get { return _requestResponseTimeout; } set { _requestResponseTimeout = value; NotifyPropertyChanged(); } }
            public String ConnectionStatus { get { return _connectionStatus; } set { _connectionStatus = value; NotifyPropertyChanged(); } }
            public bool IdleDisconnect { get { return _idleDisconnect; } set { _idleDisconnect = value; NotifyPropertyChanged(); } }
            public int IdleDisconnectTime { get { return _idleDisconnectTime; } set { _idleDisconnectTime = value; NotifyPropertyChanged(); } }
            public string Description { get { return _description; } set { _description = value; NotifyPropertyChanged(); } }
            public DateTime LastCommsDT { get { return _lastCommsDT; } set { _lastCommsDT = value; NotifyPropertyChanged(); } }
            public int Priority0QueueCount { get { return _priority0QueueCount; } set { _priority0QueueCount = value; NotifyPropertyChanged(); } }
            public int Priority1QueueCount { get { return _priority1QueueCount; } set { _priority1QueueCount = value; NotifyPropertyChanged(); } }
            public int Priority2QueueCount { get { return _priority2QueueCount; } set { _priority2QueueCount = value; NotifyPropertyChanged(); } }
            public int Priority3QueueCount { get { return _priority3QueueCount; } set { _priority3QueueCount = value; NotifyPropertyChanged(); } }
            public string ConnDetail {  get { return _conndetail; } set { _conndetail = value; NotifyPropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;

            public ConnDetails(string id)
            {
                ID = id;
                ConnectionEnabled = false;
                CommunicationsEnabled = false;
                ConnectionType = string.Empty;
                LastCommsTime = 0;
                LastCommsDT = DateTime.MinValue;
                RequestRetryDelay = 0;
                SocketConnectionAttemptTimeout = 0;
                MaxSocketConnectionAttempts = 0;
                SocketConnectionRetryDelay = 0;
                PostConnectionCommsDelay = 0;
                InterRequestDelay = 0;
                MaxRequestAttempts = 0;
                RequestResponseTimeout = 0;
                ConnectionStatus = string.Empty;
                IdleDisconnect = false;
                IdleDisconnectTime = 0;
                Description = string.Empty;
                Priority0QueueCount = 0;
                Priority1QueueCount = 0;
                Priority2QueueCount = 0;
                Priority3QueueCount = 0;
                ConnDetail = string.Empty;
            }

            protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            public void Update(string propertyName, byte[] valueBytes)
            {
                switch (propertyName)
                {
                    case "id": ID = Encoding.UTF8.GetString(valueBytes); break;
                    case "connectionenabled": ConnectionEnabled = BitConverter.ToBoolean(valueBytes, 0); break;
                    case "communicationsenabled": CommunicationsEnabled = BitConverter.ToBoolean(valueBytes, 0); break;
                    case "connectiontype": ConnectionType = Encoding.UTF8.GetString(valueBytes); break;
                    case "lastcommstime": long ticks = BitConverter.ToInt64(valueBytes, 0); LastCommsTime = ticks; LastCommsDT = new DateTime(ticks); break;
                    case "requestretrydelay": RequestRetryDelay = BitConverter.ToInt32(valueBytes, 0); break;
                    case "socketconnectionattempttimeout": SocketConnectionAttemptTimeout = BitConverter.ToInt32(valueBytes, 0); break;
                    case "maxsocketconnectionattempts": MaxSocketConnectionAttempts = BitConverter.ToInt32(valueBytes, 0); break;
                    case "socketconnectionretrydelay": SocketConnectionRetryDelay = BitConverter.ToInt32(valueBytes, 0); break;
                    case "postconnectioncommsdelay": PostConnectionCommsDelay = BitConverter.ToInt32(valueBytes, 0); break;
                    case "interrequestdelay": InterRequestDelay = BitConverter.ToInt32(valueBytes, 0); break;
                    case "maxrequestattempts": MaxRequestAttempts = BitConverter.ToInt32(valueBytes,0); break;
                    case "requestresponsetimeout": RequestResponseTimeout = BitConverter.ToInt32(valueBytes,0); break;
                    case "connectionstatus": ConnectionStatus = Encoding.UTF8.GetString(valueBytes); break;
                    case "idledisconnect": IdleDisconnect = BitConverter.ToBoolean(valueBytes,0); break;
                    case "idledisconnecttime": IdleDisconnectTime = BitConverter.ToInt32(valueBytes,0); break;
                    case "description": Description = Encoding.UTF8.GetString(valueBytes); break;
                    case "priority0count": Priority0QueueCount = BitConverter.ToInt32(valueBytes, 0); break;
                    case "priority1count": Priority1QueueCount = BitConverter.ToInt32(valueBytes, 0); break;
                    case "priority2count": Priority2QueueCount = BitConverter.ToInt32(valueBytes, 0); break;
                    case "priority3count": Priority3QueueCount = BitConverter.ToInt32(valueBytes, 0); break;
                    case "conndetails": ConnDetail = Encoding.UTF8.GetString(valueBytes); break;                        
                }

            }

        }


    }

}
