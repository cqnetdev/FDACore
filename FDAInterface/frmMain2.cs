using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using FDA;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using uPLibrary.Networking.M2Mqtt;

namespace FDAInterface
{
    public partial class frmMain2 : Form, IDisposable
    {

        public class FDAConnection : INotifyPropertyChanged
        {
            private string _FDAName;
            private string _host;

            public string FDAName { get { return _FDAName; } set { _FDAName = value; RaisePropertyChanged("Description"); } }
            public string Host { get { return _host; } set { _host = value; RaisePropertyChanged("Host"); } }

            public FDAConnection(string connString)
            {
                string[] parsed = connString.Split('|');
                if (parsed.Length == 2)
                {
                    FDAName = parsed[0];
                    Host = parsed[1];
                }
                else
                {
                    FDAName = "";
                    Host = "";
                }
            }

    
           

            public event PropertyChangedEventHandler PropertyChanged;

            private void RaisePropertyChanged(string property)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
            }

            public static bool operator ==(FDAConnection a,FDAConnection b)
            {
                if ((object)a == null || (object)b == null)
                    return false;

                return (a.FDAName == b.FDAName && a.Host == b.Host);
            }

            public static bool operator !=(FDAConnection a, FDAConnection b)
            {
                if ((object)a == null || (object)b == null)
                    return true;

                return !(a.FDAName == b.FDAName && a.Host == b.Host);
            }

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                    return false;

                FDAConnection other = (FDAConnection)obj;
                return (this.FDAName == other.FDAName && this.Host == other.Host);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
        private string _FDADBConnstring;
        private bool closing = false;
        private string SelectedConnID = "";
        private MqttClient _mqtt;
        private string DBType = "";
        private string[] FDAState;

        private Dictionary<Guid, ConnectionNode> _connOverviewDict;        

        private delegate void DataReceivedHandler(byte[] data,byte dataType);
        private delegate void SafeCallNoParams();
        private delegate void SafeCall2StringParams(string param1,string param2);
        private delegate void SafeMQQTEventHandler(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e);

        //private frmDBVal _dbValidationForm = null;
        private bool FDAConnected = false;
        private System.Threading.Timer elevationWaitTimer;
        private TimeSpan UpTimespan;
        private enum DetailsType { Queue, Connection, None };
        internal MqttClient MQTT { get => _mqtt; set { _mqtt = value; 
                if (_mqtt != null) { _mqtt.MqttMsgPublishReceived += MQTT_MqttMsgPublishReceived; _mqtt.ConnectionClosed += _mqtt_ConnectionClosed; SubscribeToFDATopics(null); 
                } } }

      

        private List<FDAConnection> _recent;

        /******************************************** CONSTRUCTOR / initialization ***************************************/
        #region initialization
        public frmMain2()
        {
            InitializeComponent();
            _connOverviewDict = new Dictionary<Guid, ConnectionNode>();
            elevationWaitTimer = new System.Threading.Timer(SubscribeToFDATopics,null,Timeout.Infinite, Timeout.Infinite);

            tabControl1.Visible = false;
            //SetDetailsVisibility(DetailsType.None);

            FDAState = new string[] { "starting", "running", "shutting down", "pausing", "paused", "stopped" };

            // load the recent FDA connections
            _recent = new List<FDAConnection>();
            StringCollection recentList = Properties.Settings.Default.RecentConnections;
            string[] recentsArray = new string[recentList.Count];
            recentList.CopyTo(recentsArray,0);
            foreach (string recent in recentsArray)
            {
                AddToRecent(new FDAConnection(recent));
            }
       
        }

        private void frmMain2_Shown(object sender, EventArgs e)
        {
            tree.Nodes[0].Expand();


            //if (FDAManagerContext.elevatedPermissions)
            //{
            //    SubscribeToFDATopics(null);
            //}
            //else
            //{
            //    elevationWaitTimer.Change(500, Timeout.Infinite);
            //}
        }

   

        #endregion

        /************************************ handle data received over TCP connection to FDA  **************************************************/
        #region TCPConnectionToFDA
        internal void DataReceivedFromFDA(byte[] message,byte dataType)
        {
            if (InvokeRequired)
                Invoke(new DataReceivedHandler(DataReceivedFromFDA), new object[] { message,dataType });
            else
            {
                string ASCIIMessage;
                if (dataType == 0) // ASCII data
                {
                    ASCIIMessage = Encoding.ASCII.GetString(message);
                    string[] parsedMsg = ASCIIMessage.Split(':');

                    // message must have two parts, the operation and the data
                    // if it doesn't have a :, it's not valid so respond with an error
                    if (parsedMsg.Length < 2)
                    {
                        return;
                    }
                    string operation = parsedMsg[0].ToUpper();
                    string data = parsedMsg[1];

                    switch (operation)
                    {
                        case "FDACONNECTIONSTATUS":
                            FDAStatus.Text = "FDA Status: " + data;
                            SetMenuItemEnabledStates();
                            if ((data.ToUpper() == "RUNNING" || data.ToUpper() == "PAUSED") && !FDAConnected)
                            {
                                FDAConnected = true;
                                ClearForm();
                                elevationWaitTimer.Change(500, Timeout.Infinite);
                            }

                            if (data.ToUpper() == "STOPPED" && FDAConnected)
                            {
                                FDAConnected = false;
                                ClearForm();
                            }
                            break;
                            /*
                        case "CONNOVERVIEW":
                            string[] connections = data.Split('|');
                            ConnectionNode connNode;
                            ConnectionNode existingNode;
                            TreeNode root = tree.Nodes[0];

                            foreach (string connection in connections)
                            {
                                connNode = new ConnectionNode(connection);

                                if (!_connOverviewDict.ContainsKey(connNode.ID))
                                {
                                    // this is a new connection overview, add it
                                    root.Nodes.Add(connNode.GetNode());
                                    _connOverviewDict.Add(connNode.ID, connNode);


                                    // now with MQTT                            
                                    string ID = connNode.ID.ToString().ToLower();
                                    SubscribeToConnectionStatus(ID, 0);


                                }
                                else
                                {
                                    // update existing connection overview
                                    existingNode = _connOverviewDict[connNode.ID];
                                    existingNode.Update(connNode);
                                }
                            }
                            if (!root.IsExpanded)
                                root.Expand();
                            break;
                            */
                    }
                }

               
            }
        }
        #endregion

        /************************************* MQTT subscriptions and publication received handling **************************************************/
        #region MQTT
        
        internal void SetMQTT(uPLibrary.Networking.M2Mqtt.MqttClient mqtt)
        {
            MQTT = mqtt;
        }

        public void MQTTConnected(string FDAName,string host)
        {
            if (InvokeRequired)
            {
                Invoke(new SafeCall2StringParams(MQTTConnected), new object[] {FDAName,host});
            }
            else
            {
                mqttStatus.Text = "MQTT Status: Connected";
                //btn_Connect.Enabled = false;
                tb_activeFDA.Text = FDAName + " (" + host + ")";
                FDAStatus.Text = "FDA Status: Unknown";
                startToolStripMenuItem.Enabled = true;
                startwithConsoleToolStripMenuItem.Enabled = true;

                SubscribeToFDATopics(null);
                if (_connOverviewDict != null)
                    foreach (ConnectionNode connNode in _connOverviewDict.Values)
                    {
                        SubscribeToConnectionStatus(connNode.ID.ToString(), 0);
                        if (connNode.GetNode().IsSelected)
                        {
                            if (tabControl1.SelectedTab.Name == "tabDetails")
                            {
                                SubscribeToConnectionDetails(connNode.ID.ToString(), 0);
                            }

                            if (tabControl1.SelectedTab.Name == "tabQueues")
                            {
                                SubscribeToQueueCounts(connNode.ID.ToString(), 0);
                            }
                        }
                    }
            }
        }


        private void _mqtt_ConnectionClosed(object sender, EventArgs e)
        {
            MQTTDisconnected();
        }

        public void MQTTDisconnected()
        {
            if (Disposing || IsDisposed || closing)
                return;

            if (InvokeRequired)
            {
                { Invoke(new SafeCallNoParams(MQTTDisconnected)); }
            }
            else
            {
                mqttStatus.Text = "MQTT Status: Disconnected";
                //btn_Connect.Enabled = true;
            }
        }

    
        /*************************************   handle MQTT 'publication received' events  ****************************************************/
        private void MQTT_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            if (closing || Disposing || IsDisposed)
                return;


            if (InvokeRequired)
            {
                Invoke(new SafeMQQTEventHandler(MQTT_MqttMsgPublishReceived), new object[] { sender, e });
            }
            else
            {
                if (e.Message.Length == 0)
                    return;

                string[] topic = e.Topic.Split('/');

                switch (topic[0])
                {
                    case "connection":
                        Guid connectionID = Guid.Parse(topic[1]);
                        if (!_connOverviewDict.ContainsKey(connectionID))
                            break;
                        switch (topic[2])
                        {
                            case "connectionstatus":
                                _connOverviewDict[connectionID].Status = Encoding.UTF8.GetString(e.Message);                               
                                break;
                            case "connectionenabled":
                                bool enb = BitConverter.ToBoolean(e.Message, 0);
                                _connOverviewDict[connectionID].ConnectionEnabled = enb;
                                break;
                            case "communicationsenabled":
                                enb = BitConverter.ToBoolean(e.Message, 0);
                                _connOverviewDict[connectionID].CommunicationsEnabled = enb;
                                break;
                            case "description":
                                _connOverviewDict[connectionID].Description = Encoding.UTF8.GetString(e.Message);
                                break;

                            case "priority0count": HandleQueueCountUpdate(connectionID,0, e.Message); break;
                            case "priority1count": HandleQueueCountUpdate(connectionID,1, e.Message); break;
                            case "priority2count": HandleQueueCountUpdate(connectionID,2, e.Message); break;
                            case "priority3count": HandleQueueCountUpdate(connectionID,3, e.Message); break;
                        }

                        

                        if (connDetails.ConnDetailsObj != null)
                        {
                            if (connDetails.ConnDetailsObj.ID == connectionID.ToString())
                            {
                                connDetails.ConnDetailsObj.Update(topic[2],e.Message);
                            }
                        }
                        break;
                    case "FDA":
                        switch (topic[1])
                        {
                            case "DBType": 
                                DBType = Encoding.UTF8.GetString(e.Message);
                                dbtypeDisplay.Text = "Database type: " + DBType;
                                break;
                            case "version":
                                Version.Text = "FDA Version: " + Encoding.UTF8.GetString(e.Message);
                                break;
                            case "uptime":
                                long ticks = BitConverter.ToInt64(e.Message, 0);
                                UpTimespan = new TimeSpan(ticks);
                                Uptime.Text = "FDA Runtime: " + UpTimespan.ToString(@"dd\:hh\:mm");
                                break;
                            case "dbconnstring":
                                _FDADBConnstring = Encoding.UTF8.GetString(e.Message);
                                connDetails.DbConnStr = _FDADBConnstring;
                                break;
                            case "executionid":
                                connDetails.FDAExecutionID = Encoding.UTF8.GetString(e.Message);
                                break;
                            case "connectionlist":
                                string connList = Encoding.UTF8.GetString(e.Message);
                                string[] connectionStrings = connList.Split('|');
                                Guid ID;
                                string[] connparts;
                                ConnectionNode newConnNode;
                                List<Guid> updatedConnList = new List<Guid>();



                                // look for new connections
                                foreach (string connStr in connectionStrings)
                                {
                                    connparts = connStr.Split('.');

                                    ID = Guid.Parse(connparts[2]);
                                    updatedConnList.Add(ID);

                                    if (!_connOverviewDict.ContainsKey(ID)) // new connection, add it to the tree and subscribe to status updates for it
                                    {
                                        newConnNode = new ConnectionNode(connStr);
                                        _connOverviewDict.Add(ID, newConnNode);
                                        tree.Nodes[0].Nodes.Add(newConnNode.GetNode());
                                        SubscribeToConnectionStatus(connparts[2].ToLower(),1);
                                    }
                                }

                                // look for deleted connections
                                new List<string>(connectionStrings);
                                List<Guid> toRemove = null;
                                foreach (Guid existingID in _connOverviewDict.Keys)
                                {
                                    if (!updatedConnList.Contains(existingID)) // this connection has been deleted, remove it from the tree and unsubscribe updates
                                    {
                                        ConnectionNode toDelete = _connOverviewDict[existingID];
                                        if (toRemove == null)
                                            toRemove = new List<Guid>();

                                        toRemove.Add(existingID);

                                        tree.Nodes[0].Nodes.Remove(toDelete.GetNode());
                                        UnsubscribeConnectionStatus(toDelete.ID.ToString().ToLower());
                                        toDelete = null;
                                    }
                                }

                                if (toRemove != null)
                                    foreach (Guid IDtoRemove in toRemove)
                                    {
                                        _connOverviewDict.Remove(IDtoRemove);
                                    }


                                // make sure the root node is expanded
                                tree.Nodes[0].Expand();

                                break;
                            case "runstatus":
                                string statusfromFDA = Encoding.UTF8.GetString(e.Message);
                                string status = statusfromFDA;
                                if (status == "ShuttingDown")
                                    status = "Shutting Down";

                                FDAStatus.Text = "FDA Status: " + status;

                                // if the FDA status is 'stopped' clear the GUI
                                if (status == "Stopped")
                                {
                                    ClearForm();
                                    startwithConsoleToolStripMenuItem.Enabled = true;
                                    startToolStripMenuItem.Enabled = true;
                                    stopToolStripMenuItem.Enabled = false;
                                    pauseToolStripMenuItem.Enabled = false;
                                    mQTTQueryTestToolStripMenuItem.Enabled = false;
                                    communicationsStatsToolStripMenuItem.Enabled = false;
                                }

                                if (status == "Normal")
                                {
                                    startwithConsoleToolStripMenuItem.Enabled = false;
                                    startToolStripMenuItem.Enabled = false;
                                    stopToolStripMenuItem.Enabled = true;
                                    pauseToolStripMenuItem.Enabled = true;
                                    mQTTQueryTestToolStripMenuItem.Enabled = true;
                                    communicationsStatsToolStripMenuItem.Enabled = true;
                                }

                                if (status == "Stopping")
                                {
                                    startwithConsoleToolStripMenuItem.Enabled = false;
                                    startToolStripMenuItem.Enabled = false;
                                    stopToolStripMenuItem.Enabled = false;
                                    pauseToolStripMenuItem.Enabled = false;
                                    mQTTQueryTestToolStripMenuItem.Enabled = false;
                                    communicationsStatsToolStripMenuItem.Enabled = false;
                                }

                                if (status == "Starting")
                                {
                                    startwithConsoleToolStripMenuItem.Enabled = false;
                                    startToolStripMenuItem.Enabled = false;
                                    stopToolStripMenuItem.Enabled = false;
                                    pauseToolStripMenuItem.Enabled = false;
                                    mQTTQueryTestToolStripMenuItem.Enabled = false;
                                    communicationsStatsToolStripMenuItem.Enabled = false;
                                }
                                break;
                        }
                        break;
                }
            }
        }

        private void HandleQueueCountUpdate(Guid connectionID,int priority, byte[] data)
        {
            if (connectionID != qHist.ConnectionID)
                return;

            int count = BitConverter.ToInt32(data, 0);
            if (data.Length >= 12 && qHist.ConnectionID == connectionID)
            {
                qHist.NewDataPoint(priority, count, new DateTime(BitConverter.ToInt64(data, 4)));
            }
            //_connOverviewDict[connectionID].UpdateQueueNodeText(priority, count);
        }

        private void SubscribeToFDATopics(object o)
        {
            MQTT?.Subscribe(new string[] { "FDA/version", "FDA/uptime", "FDA/connectionlist", "FDA/DBType" }, new byte[] { 0, 0, 0, 0 });         
        }

        private void UnsubscribeFDATopics()
        {
            MQTT?.Unsubscribe(new string[] { "FDA/version","FDA/uptime","FDA/connectionlist"});
        }
        private void SubscribeToConnectionStatus(string ID, byte QOS)
        {

            string[] topics = new string[4];
            topics[0] = "connection/" + ID + "/connectionstatus";
            topics[1] = "connection/" + ID + "/connectionenabled";
            topics[2] = "connection/" + ID + "/communicationsenabled";
            topics[3] = "connection/" + ID + "/description";

            byte[] QOSList = new byte[] { QOS, QOS, QOS ,QOS};
            MQTT.Subscribe(topics, QOSList);
        }

        private void UnsubscribeConnectionStatus(string ID)
        {
            string[] topics = new string[4];
            topics[0] = "connection/" + ID + "/connectionstatus";
            topics[1] = "connection/" + ID + "/connectionenabled";
            topics[2] = "connection/" + ID + "/communicationsenabled";
            topics[3] = "connection/" + ID + "/description";
            MQTT.Unsubscribe(topics);
        }

        private void SubscribeToQueueCounts(string ID, byte QOS)
        {
            string[] topics = new string[4];
            topics[0] = "connection/" + ID + "/priority0count";
            topics[1] = "connection/" + ID + "/priority1count";
            topics[2] = "connection/" + ID + "/priority2count";
            topics[3] = "connection/" + ID + "/priority3count";
            byte[] QOSList = new byte[] { QOS, QOS, QOS, QOS };
            MQTT.Subscribe(topics, QOSList);
        }

     

        private void UnsubscribeQueueCounts(string ID)
        {
            string[] topics = new string[4];
            topics[0] = "connection/" + ID + "/priority0count";
            topics[1] = "connection/" + ID + "/priority1count";
            topics[2] = "connection/" + ID + "/priority2count";
            topics[3] = "connection/" + ID + "/priority3count";

            MQTT.Unsubscribe(topics);
        }



        private void SubscribeToConnectionDetails(string ID, byte QOS)
        {
            string[] topics = new string[14];
            topics[0] = "connection/" + ID + "/connectiontype";
            topics[1] = "connection/" + ID + "/lastcommstime";
            topics[2] = "connection/" + ID + "/requestretrydelay";
            topics[3] = "connection/" + ID + "/socketconnectionattempttimeout";
            topics[4] = "connection/" + ID + "/maxsocketconnectionattempts";
            topics[5] = "connection/" + ID + "/socketconnectionretrydelay";
            topics[6] = "connection/" + ID + "/postconnectioncommsdelay";
            topics[7] = "connection/" + ID + "/interrequestdelay";
            topics[8] = "connection/" + ID + "/maxrequestattempts";
            topics[9] = "connection/" + ID + "/requestresponsetimeout";
            topics[10] = "connection/" + ID + "/idledisconnect";
            topics[11] = "connection/" + ID + "/idledisconnecttime";
            topics[12] = "connection/" + ID + "/description";
            topics[13] = "connection/" + ID + "/conndetails";

            byte[] QOSList = new byte[] { QOS, QOS, QOS, QOS, QOS, QOS, QOS, QOS, QOS, QOS, QOS, QOS, QOS,QOS};
            MQTT.Subscribe(topics, QOSList);
        }

        private void UnsubscribeConnectionDetails(string ID)
        {
            string[] topics = new string[14];
            topics[0] = "connection/" + ID + "/connectiontype";
            topics[1] = "connection/" + ID + "/lastcommstime";
            topics[2] = "connection/" + ID + "/requestretrydelay";
            topics[3] = "connection/" + ID + "/socketconnectionattempttimeout";
            topics[4] = "connection/" + ID + "/maxsocketconnectionattempts";
            topics[5] = "connection/" + ID + "/socketconnectionretrydelay";
            topics[6] = "connection/" + ID + "/postconnectioncommsdelay";
            topics[7] = "connection/" + ID + "/interrequestdelay";
            topics[8] = "connection/" + ID + "/maxrequestattempts";
            topics[9] = "connection/" + ID + "/requestresponsetimeout";
            topics[10] = "connection/" + ID + "/idledisconnect";
            topics[11] = "connection/" + ID + "/idledisconnecttime";
            topics[12] = "connection/" + ID + "/description";
            topics[13] = "connection/" + ID + "/conndetails";

            MQTT.Unsubscribe(topics);
        }
        
        private void SubscribeConnectionLog(string ID)
        {
            string[] topics = new string[] { "connection/" + ID + "/log" };
            byte[] QOS = new byte[] { 0 };
            MQTT.Subscribe(topics, QOS);
        }

        private void UnsubscribeConnectionLog(string ID)
        {
            string[] topics = new string[] { "connection/" + ID + "/log" };
            MQTT.Unsubscribe(topics);
        }
        #endregion

        /*************************************  form appearance (control visibility, enabled/disabled status etc ********************************/
        #region FormControlManagement
        private void SetMenuItemEnabledStates()
        {
            if (FDAManagerContext._MQTTConnectionStatus)
            {

                switch (FDAStatus.Text)
                {
                    case "FDA Status: ":
                        startToolStripMenuItem.Enabled = true;
                        startwithConsoleToolStripMenuItem.Enabled = true;
                        stopToolStripMenuItem.Enabled = false;
                        pauseToolStripMenuItem.Enabled = false;
                        mQTTQueryTestToolStripMenuItem.Enabled = false;
                        communicationsStatsToolStripMenuItem.Enabled = false;
                        break;
                    case "FDA Status: Normal":
                        startToolStripMenuItem.Enabled = false;
                        startwithConsoleToolStripMenuItem.Enabled = false;
                        stopToolStripMenuItem.Enabled = true;
                        pauseToolStripMenuItem.Enabled = true;
                        mQTTQueryTestToolStripMenuItem.Enabled = true;
                        communicationsStatsToolStripMenuItem.Enabled = true;
                        break;
                    case "FDA Status: Stopped":                   
                        startToolStripMenuItem.Enabled = true;
                        startwithConsoleToolStripMenuItem.Enabled = true;
                        stopToolStripMenuItem.Enabled = false;
                        pauseToolStripMenuItem.Enabled = false;
                        mQTTQueryTestToolStripMenuItem.Enabled = false;
                        communicationsStatsToolStripMenuItem.Enabled = false;
                        break;
                    default:
                        startToolStripMenuItem.Enabled = false;
                        startwithConsoleToolStripMenuItem.Enabled = false;
                        stopToolStripMenuItem.Enabled = false;
                        pauseToolStripMenuItem.Enabled = false;
                        mQTTQueryTestToolStripMenuItem.Enabled = false;
                        communicationsStatsToolStripMenuItem.Enabled = false;
                        break;
                }
            }
            else
            {
                startToolStripMenuItem.Enabled = false;
                startwithConsoleToolStripMenuItem.Enabled = false;
                stopToolStripMenuItem.Enabled = false;
                pauseToolStripMenuItem.Enabled = false;
                mQTTQueryTestToolStripMenuItem.Enabled = false;
                communicationsStatsToolStripMenuItem.Enabled = false;
            }

            //_dbValidationForm?.UpdateFDAStatus(FDAManagerContext._MQTTConnectionStatus);
        }


        //private void SetDetailsVisibility(DetailsType dType)
        //{
        //    switch (dType)
        //    {
        //        case DetailsType.Queue:
        //            qHist.Visible = true;
        //            connDetails.Visible = false;
        //            //btnQueues.Enabled = false;
        //            //btnDetails.Enabled = true;
        //            //panel_viewselector.Visible = true;
        //            break;
        //        case DetailsType.Connection:
        //            qHist.Visible = false;
        //            connDetails.Visible = true;
        //            //btnDetails.Enabled = false;
        //            //btnQueues.Enabled = true;
        //            //panel_viewselector.Visible = true;
        //            //panel_viewselector.BringToFront();
        //            break;
        //        case DetailsType.None:
        //            qHist.Visible = false;
        //            connDetails.Visible = false;
        //            //btnQueues.Enabled = false;
        //            //btnDetails.Enabled = false;
        //            //panel_viewselector.Visible = false;
        //            break;
        //    }
        //}


        internal void ClearForm()
        {
            if (InvokeRequired)
            {
                Invoke(new SafeCallNoParams(ClearForm), new object[0]);
            }
            else
            {
                tree.Nodes[0].Nodes.Clear();
                Version.Text = "FDA Version: unknown";
                Uptime.Text = "FDA Runtime: unknown";
                //FDAStatus.Text = "FDA Status: unknown";
                dbtypeDisplay.Text = "Database Type: unknown";



                foreach (ConnectionNode connection in _connOverviewDict.Values)
                {
                    UnsubscribeConnectionDetails(connection.ID.ToString());
                    UnsubscribeConnectionStatus(connection.ID.ToString());
                    UnsubscribeQueueCounts(connection.ID.ToString());
                }

                _connOverviewDict.Clear();

                //tb_activeFDA.Text = "Not Connected";

                tabControl1.Visible = false;
                //SetDetailsVisibility(DetailsType.None);
            }
        }
        #endregion

        /*************************************** user action handling (handle clicks)************************************************************/
        #region UserInteraction

        /*     private void btnEditFDA_Click(object sender, EventArgs e)
             {
                 frmEditFDAList editFDADlg = new frmEditFDAList(_FDAList);

                 editFDADlg.ShowDialog();

             }
        */

        //FDA Menu
        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FDAManagerContext.SendStartFDACommand(null, new EventArgs());
            // startwithConsoleToolStripMenuItem.Enabled = false;
            // startToolStripMenuItem.Enabled = false;
            // stopToolStripMenuItem.Enabled = true;
        }


        private void startwithConsoleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //FDAManagerContext.SendStartFDAConsoleCommand(null, new EventArgs());
            //startwithConsoleToolStripMenuItem.Enabled = false;
            //startToolStripMenuItem.Enabled = false;
            //stopToolStripMenuItem.Enabled = true;
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FDAManagerContext.StopFDA(null, new EventArgs());
            //startwithConsoleToolStripMenuItem.Enabled = true;
            //startToolStripMenuItem.Enabled = true;
            //stopToolStripMenuItem.Enabled = false;
        }


        // tools menu
        /*
        private void databaseValidatorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_dbValidationForm != null)
            {
                _dbValidationForm.Focus();
                return;
            }

            _dbValidationForm = new frmDBVal();
            _dbValidationForm.FDADBConnString = _FDADBConnstring;
            _dbValidationForm.Disposed += DbValidationForm_Disposed;
            _dbValidationForm.Show();
        }
        */

        // item selected in the tree
        private void tree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Level == 1) // connection node
            {
                string newlySelectedID = ((Guid)e.Node.Tag).ToString();

                // if a connection was previously selected, unsubscribe from the previously selected connection 
                if (SelectedConnID != "" && SelectedConnID != newlySelectedID)
                {

                    if (newlySelectedID != SelectedConnID)
                    {
                        UnsubscribeConnectionDetails(connDetails.ConnDetailsObj.ID);
                        UnsubscribeQueueCounts(connDetails.ConnDetailsObj.ID);

                        SetConnectionMQTTEnabledStatus(SelectedConnID, false);
                    }
                }

                SelectedConnID = newlySelectedID;
               
                connDetails.ConnDetailsObj = new ConnDetailsCtrl.ConnDetails(SelectedConnID);

                // subscribe to updates for the properties of the selected connection
                SubscribeToConnectionDetails(SelectedConnID, 0);
                SubscribeToQueueCounts(SelectedConnID, 2);

                // enable MQTT for the selected connection
                SetConnectionMQTTEnabledStatus(SelectedConnID, true);



                // default to the connection details view mode
                //tabControl1.SelectedIndex = 0;
                
                tabControl1.Visible = true;
                //SetDetailsVisibility(DetailsType.Connection);
                //if (cb_viewMode.SelectedIndex < 0)
                //{
                //    cb_viewMode.SelectedIndex = 0;
                //}

                qHist.ConnectionID = Guid.Parse(SelectedConnID);
                qHist.Clear();
                
            } 
        }



        private void SetConnectionMQTTEnabledStatus(string id, bool enabled)
        {
            byte enabledVal = 0;
            if (enabled)
                enabledVal = 1;

            if (MQTT != null)
                if (MQTT.IsConnected)
                {
                    MQTT.Publish("connection/" + id + "/setmqttenabled", new byte[] { enabledVal });
                }
        }

        //private void cb_viewMode_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //    switch (cb_viewMode.SelectedIndex)
        //    {
        //        case 0: SetDetailsVisibility(DetailsType.Connection); break;
        //        case 1: SetDetailsVisibility(DetailsType.Queue); break;
        //        default: SetDetailsVisibility(DetailsType.None); break;
        //    }
        //}

        //private void btnDetails_Click(object sender, EventArgs e)
        //{
        //    SetDetailsVisibility(DetailsType.Connection);
        //}

        //private void btnQueues_Click(object sender, EventArgs e)
        //{
        //    SetDetailsVisibility(DetailsType.Queue);
        //}

        // refresh button
        //private void btnRefresh_Click(object sender, EventArgs e)
        //{
        //    RefreshForm();
        //}

        /*
        private void cb_FDA_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cb_FDA.SelectedIndex >= 0)
            {
                FDAConnection selectedConnection = (FDAConnection)cb_FDA.SelectedItem;

                if (selectedConnection.Host != FDAManagerContext.Host)
                {
                    ClearForm();
                    //MessageBox.Show("connecting to " + selectedConnection.Host + "(" + selectedConnection.FDAName + ")");
                    FDAManagerContext.ChangeHost(selectedConnection.Host, selectedConnection.FDAName);
                }
                else
                {
                   // MessageBox.Show("same host '" + selectedConnection.Host + "', nothing to do");
                }

            }
        }
        */

        #endregion

        /************************************ misc **********************************************/
        #region misc
        // function to allow FDAManagerContext to update the form with the FDA connection status
        /*
        internal void UpdateFDAConnStatus(string status)
        {
            FDAStatus.Text = status;
        }
        */

        // cleanup the DBValidation form after its closed
        /*
        private void DbValidationForm_Disposed(object sender, EventArgs e)
        {
            _dbValidationForm.Disposed -= DbValidationForm_Disposed;
            _dbValidationForm = null;
        }
        */
        #endregion


        private void frmMain2_FormClosing(object sender, FormClosingEventArgs e)
        {
            closing = true;

            // Unsubscribe to general FDA topics
            UnsubscribeFDATopics();

            // unsubscribe from each connection
            foreach (TreeNode node in tree.Nodes[0].Nodes)
            {
                UnsubscribeConnectionStatus(node.Tag.ToString());
                if (node.IsSelected)
                {
                    UnsubscribeConnectionDetails(node.Tag.ToString());
                    UnsubscribeQueueCounts(node.Tag.ToString());
                }
            }

            MQTT = null;
        }



        /*********************************** ConnectionNode Class ***********************************/
        /**** contains all the details about a given connection                                  ****/
        /**** automatically updates the tree when connection status changes                      ****/
        /********************************************************************************************/
        internal class ConnectionNode : IComparable
        {
           
            private string _status;          
            private bool _connectionEnabled;
            private bool _communicationsEnabled;
            private string _description;
            private ushort[] _qCounts = new ushort[0];
            private TreeNode _node;

            public readonly Guid ID;
            public string Description { get => _description; set { _description = value; _node.Text = Description; UpdateIcon(); } }
            public string Status { get => _status; set { _status = value; UpdateIcon(); } }
            public bool ConnectionEnabled { get => _connectionEnabled; set { _connectionEnabled = value; UpdateIcon(); } }
            public bool CommunicationsEnabled { get => _communicationsEnabled; set { _communicationsEnabled = value; UpdateIcon(); } }
            public ushort[] Qcounts { get => _qCounts; set => _qCounts = value;}

            public ConnectionNode(string connOverview)
            {
                _node = new TreeNode();

                string[] connOverviewArr = connOverview.Split('.');
                Description = connOverviewArr[1];
               
                Status = connOverviewArr[0];
                ConnectionEnabled = (connOverviewArr[3] == "1");
                CommunicationsEnabled = (connOverviewArr[4] == "1");

                ID = Guid.Parse(connOverviewArr[2]);               
                
                List<ushort> qCountList = new List<ushort>();
                for (int i=5;i<connOverviewArr.Length;i++)
                {
                    qCountList.Add(UInt16.Parse(connOverviewArr[i]));
                }
                _qCounts = qCountList.ToArray();

                
                _node.Tag = ID;

                // don't create queue nodes anymore
                //CreateQueueSubNodes(_qCounts);

            }       

            private void UpdateIcon()
            {
                if (_node == null)
                    return;
               

                int imageIdx = 3;

                // connection enabled status
                if (ConnectionEnabled)
                {
                    switch (Status)
                    {
                        // connection enabled and connected -> green or yellow (delayed)
                        case "Connected_Ready": imageIdx = 0; break;
                        case "Connecting": imageIdx = 1; break;
                        case "Connected_Delayed": imageIdx = 1; break;
                        // connection enabled, but disconnected -> red
                        default: imageIdx = 2; break;
                    }
                }
                else
                {
                    // connection disabled -> Grey
                    imageIdx = 3;
                }

                // communication not enabled -> add a 'pause' symbol over the connection status indicator
                if (!CommunicationsEnabled)
                {
                    imageIdx += 4;
                }

                _node.ImageIndex = imageIdx;
                _node.SelectedImageIndex = imageIdx;
                _node.Text = Description;
            }

            private void CreateQueueSubNodes(ushort[] qCounts)
            {
                TreeNode subNode;
                if (_node == null)
                    return;

                for (int i=0;i< qCounts.Length; i++)
                {
                    subNode = new TreeNode("Priority Queue " + i + " (" + _qCounts[i] + ")", 8, 8);
                    subNode.Tag = ID + "." + i;
                    _node.Nodes.Add(subNode);
                }
            }

            public void UpdateQueueNodeText(int priority,int count)
            {
                _node.Nodes[priority].Text = "Priority Queue " + priority + " (" + count + ")";
            }

            public TreeNode GetNode()
            {
                return _node;
            }

            public int CompareTo(object obj)
            {
                ConnectionNode node = (ConnectionNode)obj;
                return Description.CompareTo(node.Description);
            }
        }

        //==========================================
        private void mQTTQueryTestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmCustomQuery testForm = new frmCustomQuery(MQTT);
            testForm.Show();
        }

        private void communicationsStatsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmCommsStats statsForm = new frmCommsStats(MQTT,DBType);
            statsForm.SetConnectionList(_connOverviewDict);
            statsForm.Show();
        }

        private void ChangeConnectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem selectedItem = (ToolStripMenuItem)sender;
            FDAConnection selectedConnection;
            if (selectedItem.Text == "New Connection")  // creating a new connection
            {
                frmAddFDADialog dlg = new frmAddFDADialog();
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    selectedConnection = new FDAConnection(dlg.FDAConnection);
                }
                else
                    return;
            }
            else   // selecting a previous connection from the recent connections list
            {
                selectedConnection = (FDAConnection)selectedItem.Tag;
            }

            if (selectedConnection.Host != FDAManagerContext.Host) // if the selected connection is different from the currently connected one
            {
                ClearForm();
                FDAManagerContext.ChangeHost(selectedConnection.Host, selectedConnection.FDAName);
                AddToRecent(selectedConnection);
            }
        }

        private void AddToRecent(FDAConnection connection)
        {
            bool alreadyListed = false;
            foreach (FDAConnection recentConn in _recent)
            {
                if (connection == recentConn)
                {
                    alreadyListed = true;
                    break;
                }
            }

            if (!alreadyListed)
            {
                _recent.Add(connection);
                ToolStripMenuItem newMenuItem = new ToolStripMenuItem(connection.FDAName + " (" + connection.Host + ")");
                newMenuItem.Click += ChangeConnectionToolStripMenuItem_Click;
                newMenuItem.Tag = connection;
                recentToolStripMenuItem.DropDownItems.Add(newMenuItem);
                Properties.Settings.Default.RecentConnections.Add(connection.FDAName + "|" + connection.Host);
                Properties.Settings.Default.Save();
            }
        }

        internal void SetFDAStatus(string status)
        {
            FDAStatus.Text = "FDA Status: " + status;
            if (status == "Disconnected")
                tb_activeFDA.Text = "Not Connected";

            SetMenuItemEnabledStates();

        }

        private void btn_Connect_Click(object sender, EventArgs e)
        {
            string localFDAName = Properties.Settings.Default.LocalFDAIdentifier;
            FDAManagerContext.ChangeHost("127.0.0.1", localFDAName);
        }

        private void pauseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pauseToolStripMenuItem.Text == "Pause FDA")
            {
                FDAManagerContext.PauseFDA(this, new EventArgs());
                pauseToolStripMenuItem.Text = "Resume FDA";
            }
            else
            {
                FDAManagerContext.PauseFDA(this, new EventArgs());
                pauseToolStripMenuItem.Text = "Pause FDA";
            }
        }

    }
  
 
}
