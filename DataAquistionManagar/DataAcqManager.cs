using System;
using System.Collections.Generic;
using Common;
using System.IO.Ports;
using TableDependency.SqlClient.Base.EventArgs;
using System.Text;
using System.Threading;
using uPLibrary.Networking.M2Mqtt;

namespace FDA
{
    public class DataAcqManager : IDisposable
    {
        public static Dictionary<Guid, ConnectionManager> _connectionsDictionary;
        private Dictionary<Guid,FDAScheduler> _schedulersDictionary;
        private string _dbConnectionString;
        private DBManager _dbManager;

        private BackfillManager _backfillManager;


        public string DBConnectionString { get => _dbConnectionString; set => _dbConnectionString = value; }
        public string ID { get; set; }
        public readonly Guid ExecutionID;



        // temporary test variable
        //private bool firstGoAround = true;

        public DataAcqManager(string FDAID,string connString,Guid executionID)
        {
            _dbManager = new DBManager(connString);

            _backfillManager = new BackfillManager(_dbManager);
            _backfillManager.BackfillAnalysisComplete += BackfillAnalysisCompleteHandler;

            ExecutionID = executionID;

            _schedulersDictionary = new Dictionary<Guid, FDAScheduler>();
            _connectionsDictionary = new Dictionary<Guid, ConnectionManager>();

            try
            {
                //Globals.SystemManager.AppconfigMonitorError += Logger_AppconfigMonitorError;
                Globals.SystemManager.ConfigChange += ConfigChangeHandler;
            }
            catch 
            {
                // FDASystemManager is dead...and there's no way to log the problem
                //Console.WriteLine(Globals.GetOffsetUTC().ToString() + ": Error encountered when subscribing to FDASystemManager events '" + ex.Message + "'");
            }
        }

        public int GetTotalQueueCounts()
        {
            int count = 0;
            foreach (ConnectionManager mgr in _connectionsDictionary.Values)
                count += mgr.TotalQueueCount;
            
            return count;
        }
        private void Logger_AppconfigMonitorError(object sender, Exception e)
        {
            _dbManager?.HandleTableMonitorError(e);
        }

        public bool TestDBConnection()
        {
            return _dbManager.TestDatabaseConnection();
        }

        public void Start()
        {
            try // general error catching for the start() function
            {
                // load the initial configuration
                _dbManager.LoadConfig();

                _dbManager.Initialize();

                Globals.DBManager = _dbManager;

                // apply any app configuration options
                ApplyConfigOptions();

                // subscribe to configuration change events from the DBManager
                _dbManager.ConfigChange += ConfigChangeHandler;
                _dbManager.DemandRequest += DemandRequestHandler;

                // create connection objects for each DSSourceConnection            
                List<FDASourceConnection> ConnectionList = _dbManager.GetAllConnectionconfigs();

                string[] connDetails;
                foreach (FDASourceConnection connectionconfig in ConnectionList)
                {
                    // separate the hostname from the port
                    connDetails = connectionconfig.SCDetail01.Split(':');

                    // create a new connection manager
                    ConnectionManager newConn = null;
                    bool success = true;
                    try
                    {
                        switch (connectionconfig.SCType.ToUpper())
                        {
                            case "ETHERNET":
                                newConn = new ConnectionManager(connectionconfig.SCUID, connectionconfig.Description,
                                connDetails[0],              // IP Address 
                                int.Parse(connDetails[1]));   // Port Number
                       
                                break;
                            case "ETHERNETUDP":
                                newConn = new ConnectionManager(connectionconfig.SCUID, connectionconfig.Description, "", int.Parse(connDetails[0]), "UDP");
                                break;
                            case "SERIAL":
                                string comPort = connDetails[0];
                                Parity parity;
                                switch (connDetails[3].ToUpper())
                                {
                                    case "N": parity = Parity.None; break;
                                    case "E": parity = Parity.Even; break;
                                    case "O": parity = Parity.Odd; break;
                                    case "S": parity = Parity.Space; break;
                                    default: throw new FormatException("unrecognized serial parity option '" + connDetails[3] + "'. Valid options are N, E, O, or S");
                                }
                                StopBits stopBits;
                                switch (connDetails[4].ToUpper())
                                {
                                    case "N": stopBits = StopBits.None; break;
                                    case "1": stopBits = StopBits.One; break;
                                    case "1.5": stopBits = StopBits.OnePointFive; break;
                                    case "2": stopBits = StopBits.Two; break;
                                    default: throw new FormatException("unrecognized serial stop bits option '" + connDetails[4] + "'. Valid options are N,1,1.5, or 2");
                                }

                                newConn = new ConnectionManager(connectionconfig.SCUID, connectionconfig.Description,
                                    comPort,                                            // COM Port
                                    int.Parse(connDetails[1]),                                 // baud
                                    parity,
                                    int.Parse(connDetails[2]),                                 // databits
                                    stopBits,
                                    Handshake.None); // handshaking (hard coded to "none")
                                break;
                        }
                        newConn.RequestRetryDelay = connectionconfig.RequestRetryDelay;
                        newConn.SocketConnectionAttemptTimeout = connectionconfig.SocketConnectionAttemptTimeout;
                        newConn.MaxSocketConnectionAttempts = connectionconfig.MaxSocketConnectionAttempts;
                        newConn.SocketConnectionRetryDelay = connectionconfig.SocketConnectionRetryDelay;
                        newConn.PostConnectionCommsDelay = connectionconfig.PostConnectionCommsDelay;
                        newConn.InterRequestDelay = connectionconfig.InterRequestDelay;
                        newConn.MaxRequestAttempts = connectionconfig.MaxRequestAttempts;
                        newConn.RequestResponseTimeout = connectionconfig.RequestResponseTimeout;
                        newConn.CommsLogEnabled = connectionconfig.CommsLogEnabled;
                        newConn.MQTTEnabled = false; // connectionconfig.MQTTEnabled;
                        newConn.ConnDetails = connectionconfig.SCDetail01;
                    }
                    catch (Exception ex)
                    {
                        Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while creating connection object " + connectionconfig.SCUID);
                        success = false;
                    }

                    if (success)
                    {
                        try
                        {
                            // add it to our dictionary
                            _connectionsDictionary.Add(connectionconfig.SCUID, newConn);

                            // subscribe to "transaction complete" events from it
                            newConn.TransactionComplete += TransactionCompleteHandler;

                            // enable it (if configured to be enabled)
                            newConn.CommunicationsEnabled = connectionconfig.CommunicationsEnabled;
                            newConn.ConnectionEnabled = connectionconfig.ConnectionEnabled;
                        }
                        catch (Exception ex)
                        {
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while preparing the connection for use: " + connectionconfig.SCUID + " (" + connectionconfig.Description + ")");
                            newConn.ConnectionEnabled = false;
                            newConn.Dispose();
                            newConn = null;
                        }
                    }
                }


                // start the database config change monitors (not needed, SQLTableDependency doesn't support postgres)
                //_dbManager.StartChangeMonitoring();

                // create timers for each Schedule
                try
                {
                    Random rnd = new Random(DateTime.Now.Millisecond);
                    foreach (FDARequestGroupScheduler sched in _dbManager.GetAllSched())
                    {

                        switch (sched.FRGSType.ToUpper())
                        {
                            case "REALTIME": _schedulersDictionary.Add(sched.FRGSUID, new FDASchedulerRealtime(sched.FRGSUID, sched.Description, sched.RequestGroups,sched.Tasks,sched.RealTimeRate,TimeSpan.FromSeconds(rnd.Next(10)))); break;
                            case "DAILY": _schedulersDictionary.Add(sched.FRGSUID, new FDASchedulerDaily(sched.FRGSUID, sched.Description, sched.RequestGroups, sched.Tasks, new DateTime(sched.Year, sched.Month, sched.Day, sched.Hour, sched.Minute, sched.Second))); break;
                            case "HOURLY": _schedulersDictionary.Add(sched.FRGSUID, new FDASchedulerHourly(sched.FRGSUID, sched.Description, sched.RequestGroups, sched.Tasks, new DateTime(sched.Year, sched.Month, sched.Day, sched.Hour, sched.Minute, sched.Second))); break;
                            case "MONTHLY": _schedulersDictionary.Add(sched.FRGSUID, new FDASchedulerMonthly(sched.FRGSUID, sched.Description, sched.RequestGroups, sched.Tasks,new DateTime(sched.Year, sched.Month, sched.Day, sched.Hour, sched.Minute, sched.Second))); break;
                            default: Globals.SystemManager.LogApplicationEvent(this, "", "Schedule " + sched.FRGSUID + " has unrecognized FRGDType '" + sched.FRGSType + "' - this schedule will not be loaded"); break;
                        }

                        if (_schedulersDictionary.ContainsKey(sched.FRGSUID))
                            _schedulersDictionary[sched.FRGSUID].Enabled = sched.FRGSEnabled;
                        else
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), new KeyNotFoundException(), "Scheduler with ID " + sched.FRGSUID + " not found");
                    }

                    // add listeners for each schedule
                    foreach (FDAScheduler schedule in _schedulersDictionary.Values)
                        schedule.TimerElapsed += ScheduleTickHandler;
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "DataAcqManager: Error while creating timers for the configured schedule(s)");
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "DataAcqManager: General error in Start()");
            }

         

            PublishUpdatedConnectionsList();

            // publish the default CommsStats table name
            string tableString = Globals.SystemManager.GetTableName("CommsStats");
            byte[] tableBytes = Encoding.UTF8.GetBytes(tableString);
            Globals.MQTT.Publish("FDA/DefaultCommsStatsTable",tableBytes,0,true);
        }

        public void PublishUpdatedConnectionsList()
        {
            StringBuilder sb = new StringBuilder();
            ushort[] Qcounts;
            if (_connectionsDictionary != null)
            {
                foreach (KeyValuePair<Guid, ConnectionManager> kvp in DataAcqManager._connectionsDictionary)
                {
                    sb.Append(kvp.Value.ConnectionStatus.ToString());
                    sb.Append(".");
                    sb.Append(kvp.Value.Description);
                    sb.Append(".");
                    sb.Append(kvp.Key.ToString());
                    sb.Append(".");
                    sb.Append(kvp.Value.ConnectionEnabled ? 1 : 0);
                    sb.Append(".");
                    sb.Append(kvp.Value.CommunicationsEnabled ? 1 : 0);
                    sb.Append(".");
                    Qcounts = kvp.Value._queueManager.GetQueueCounts();
                    foreach (ushort count in Qcounts)
                    {
                        sb.Append(count);
                        sb.Append(".");
                    }
                    //remove the last .
                    if (sb.Length > 0)
                        sb.Remove(sb.Length - 1, 1);
                    sb.Append("|");
                }
            }
            //remove the last |
            if (sb.Length > 0)
                sb.Remove(sb.Length - 1, 1);

            Globals.MQTT.Publish("FDA/connectionlist", Encoding.UTF8.GetBytes(sb.ToString()), 0, true);
        }

        private void TransactionCompleteHandler(object sender, ConnectionManager.TransactionEventArgs e)
        {

            if (e.RequestRef.Status != DataRequest.RequestStatus.Success && e.RequestRef.Status != DataRequest.RequestStatus.PartialSuccess)
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "The transaction was not successful : Group " + e.RequestRef.GroupID + ", request " + e.RequestRef.GroupIdxNumber + ", " + e.RequestRef.ErrorMessage);
                return;
            }


            if (e.RequestRef.MessageType == DataRequest.RequestType.AlarmEventPointers)
            {
                //  send it to the protocol for processing, get a requestgroup back containing the request(s) for alarms or events, now that we know the where the current pointers are
                switch (e.RequestRef.Protocol)
                {
                    case "ROC":
                        // update the historicreferences table with the current alarm/event ptrs
                        _dbManager.UpdateAlmEvtCurrentPtrs(e.RequestRef);

                        // if the opcode 120 was a system request (triggered by an opcode 121 or 122 with the + modifier), generate a request group for the Alarms/Events
                        if (e.RequestRef.RequesterType == Globals.RequesterType.System)
                        {
                            // generate a RequestGroup for the alarms or events that were originally trigged by the schedule or on demand
                            RequestGroup AlarmEventsRequestGroup = ROC.ROCProtocol.GenerateAlarmEventsRequests(e.RequestRef);

                            // queue the new group in the connectionmanager
                            SendToConnectionManager(AlarmEventsRequestGroup);
                        }
                        break;
                    case "MODBUS": break;
                    case "MODBUSTCP": break;
                    case "ENRONMODBUS": break;
                }
            }

            if (e.RequestRef.MessageType == DataRequest.RequestType.Alarms || e.RequestRef.MessageType == DataRequest.RequestType.Events)
            {
                // send the alarm records to the database to get written out to the tables
                List<AlarmEventRecord> records = (List<AlarmEventRecord>)e.RequestRef.TagList[2].Value;
                _dbManager.WriteAlarmsEvents(records);
            }


            if ( (e.RequestRef.MessageType == DataRequest.RequestType.Read || e.RequestRef.MessageType == DataRequest.RequestType.ReadbackAfterWrite) && e.RequestRef.TagList.Count > 0)
            {
                // update the last read properties in the tag def                  
                FDADataPointDefinitionStructure tagDef;
                foreach (Tag tag in e.RequestRef.TagList)
                {
                    tagDef = _dbManager.GetTagDef(tag.TagID);
                    if (tagDef == null)
                        continue;

                    if (!tagDef.DPDSEnabled)
                        continue;

                    // scale the value, if enabled
                    if (tagDef.read_scaling && tagDef.read_scale_raw_high != tagDef.read_scale_raw_low)
                    {

                        if (Double.TryParse(Convert.ToString(tag.Value), out double numericValue))
                            tag.Value = (object)(((numericValue - tagDef.read_scale_raw_low) * (tagDef.read_scale_eu_high - tagDef.read_scale_eu_low) / (tagDef.read_scale_raw_high - tagDef.read_scale_raw_low)) + tagDef.read_scale_eu_low);
                    }


                    // update the last read data value in the tag definition object
                    if (tag.Quality == 192)
                    {
                        lock (tagDef)
                        {
                            tagDef.PreviousTimestamp = tagDef.LastReadDataTimestamp;
                            tagDef.LastReadDataValue = Convert.ToDouble(tag.Value);
                            tagDef.LastReadDataTimestamp = tag.Timestamp;
                            tagDef.LastReadQuality = tag.Quality;
                        }
                    }
                }


                // send it to the backfill manager for analysis (backfill manager will return it in a BackfillAnalysisComplete event with the results of the analysis)
                if (e.RequestRef.MessageType == DataRequest.RequestType.Read)
                    _backfillManager.AnalyzeResponse(e.RequestRef);
            }  
            
            // if this is a completed backfill request, send it to the dbMananger to be saved in the DB
            if (e.RequestRef.MessageType == DataRequest.RequestType.Backfill)
                _dbManager.WriteDataToDB(e.RequestRef);
        }



        private void BackfillAnalysisCompleteHandler(object sender, BackfillManager.BackfillEventArgs e)
        {
            // log the value(s) in the database
            _dbManager.WriteDataToDB(e.Request);

            // if the analysis resulted in any backfill groups, send them to the connection manager for processing
            // (do they need to go to "PrepareAndSendRequestGroups()"? or can they go directly to the connection?
            if (e.BackfillGroups != null)
                if (e.BackfillGroups.Count > 0)
                {
                    PrepareAndSendRequestGroups(e.BackfillGroups, Globals.RequesterType.System, Guid.Empty);
                }
        }
        
        /// <summary>
        /// Mark request groups for re-validation
        /// </summary>
        /// <param name="mode">0 = all groups,1 = only groups marked as valid,2 = only groups marked as invalid</param>
        private void RevalidateRequestGroups(int mode)
        {
           foreach (FDAScheduler sched in _schedulersDictionary.Values)
            {
                foreach (RequestGroup group in sched.RequestGroupList)
                {
                    switch (mode)
                    {
                        case 0: group.Validation = RequestGroup.ValidationState.Unvalidated; break;
                        case 1:
                            if (group.Validation == RequestGroup.ValidationState.Valid)
                                group.Validation = RequestGroup.ValidationState.Unvalidated;
                            break;
                        case 2:
                            if (group.Validation == RequestGroup.ValidationState.Invalid)
                                group.Validation = RequestGroup.ValidationState.Unvalidated;
                            break;
                    }                    
                }
            }
        }


        
        private void ConfigChangeHandler(object sender, ConfigEventArgs e)
        {           
            try
            {
                // handle scheduler changes
                if (e.TableName == Globals.SystemManager.GetTableName("FDARequestGroupScheduler"))
                {
                    FDARequestGroupScheduler SchedulerConfig = _dbManager.GetSched(e.ID);
                    switch (e.ChangeType)

                    {
                        case "UPDATE":
                            // find the existing schedule
                            FDAScheduler oldScheduler=null;
                            if (_schedulersDictionary.ContainsKey(e.ID))
                            {
                                oldScheduler = _schedulersDictionary[e.ID];
                            }
                            else
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "", "Schedule ID " + e.ID + " was updated in the database, but was not found in the FDA. Creating a new schedule with this ID");
                            }

                            // build a new one
                            FDAScheduler newScheduler = null;
                            TimeSpan delayTime = TimeSpan.Zero;
                            switch (SchedulerConfig.FRGSType.ToUpper())
                            {
                                case "REALTIME":
                                    if (oldScheduler != null)
                                    {
                                        if (oldScheduler.GetType().ToString() == "FDA.FDASchedulerRealtime")
                                        {
                                            DateTime nexttick = ((FDASchedulerRealtime)oldScheduler).NextTick;
                                            delayTime = nexttick.Subtract(Globals.FDANow());
                                        }
                                    }
                                    newScheduler = new FDASchedulerRealtime(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups,SchedulerConfig.Tasks, SchedulerConfig.RealTimeRate,delayTime);
                                    break;
                                case "DAILY":  newScheduler = new FDASchedulerDaily(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, new DateTime(SchedulerConfig.Year, SchedulerConfig.Month, SchedulerConfig.Day, SchedulerConfig.Hour, SchedulerConfig.Minute, SchedulerConfig.Second)); break;
                                case "HOURLY": newScheduler = new FDASchedulerHourly(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, new DateTime(SchedulerConfig.Year, SchedulerConfig.Month, SchedulerConfig.Day, SchedulerConfig.Hour, SchedulerConfig.Minute, SchedulerConfig.Second)); break;
                                case "MONTHLY":newScheduler = new FDASchedulerMonthly(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, new DateTime(SchedulerConfig.Year, SchedulerConfig.Month, SchedulerConfig.Day, SchedulerConfig.Hour, SchedulerConfig.Minute, SchedulerConfig.Second)); break;
                                default:
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Schedule ID " + e.ID + " has unrecognized schedule type '" + SchedulerConfig.FRGSType.ToString() + "'");
                                    break;
                            }

                            if (newScheduler == null)
                                return;

                            

                            // delete the existing (if present)
                            if (oldScheduler != null)
                            { 
                                oldScheduler.Enabled = false;   
                                oldScheduler.TimerElapsed -= ScheduleTickHandler;
                                if (_schedulersDictionary.ContainsKey(e.ID))
                                {
                                    lock (_schedulersDictionary)
                                    {

                                        _schedulersDictionary.Remove(e.ID);
                                    }
                                }
                                oldScheduler = null;
                            }

                            // insert the new (updated) schedule
                            newScheduler.TimerElapsed += ScheduleTickHandler;
                            lock (_schedulersDictionary)
                            {
                                _schedulersDictionary.Add(newScheduler.ID, newScheduler);
                            }
                            newScheduler.Enabled = SchedulerConfig.FRGSEnabled;

                            break;

                        case "DELETE":
                            _schedulersDictionary[e.ID].Enabled = false;
                            _schedulersDictionary[e.ID].TimerElapsed -= ScheduleTickHandler;
                            lock (_schedulersDictionary) { _schedulersDictionary.Remove(e.ID); }
                            break;
 
                        case "INSERT":
                            lock (_schedulersDictionary)
                            {
                                switch (SchedulerConfig.FRGSType.ToUpper())
                                {
                                    case "REALTIME": _schedulersDictionary.Add(SchedulerConfig.FRGSUID, new FDASchedulerRealtime(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, SchedulerConfig.RealTimeRate, TimeSpan.Zero)); break;
                                    case "DAILY": _schedulersDictionary.Add(SchedulerConfig.FRGSUID, new FDASchedulerDaily(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, new DateTime(SchedulerConfig.Year, SchedulerConfig.Month, SchedulerConfig.Day, SchedulerConfig.Hour, SchedulerConfig.Minute, SchedulerConfig.Second))); break;
                                    case "HOURLY": _schedulersDictionary.Add(SchedulerConfig.FRGSUID, new FDASchedulerHourly(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, new DateTime(SchedulerConfig.Year, SchedulerConfig.Month, SchedulerConfig.Day, SchedulerConfig.Hour, SchedulerConfig.Minute, SchedulerConfig.Second))); break;
                                    case "MONTHLY": _schedulersDictionary.Add(SchedulerConfig.FRGSUID, new FDASchedulerMonthly(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, new DateTime(SchedulerConfig.Year, SchedulerConfig.Month, SchedulerConfig.Day, SchedulerConfig.Hour, SchedulerConfig.Minute, SchedulerConfig.Second))); break;
                                    default:
                                        Globals.SystemManager.LogApplicationEvent(this, "", "Schedule ID " + e.ID + " has unrecognized schedule type '" + SchedulerConfig.FRGSType.ToString() + "'");
                                        break;
                                }
                            }
                            _schedulersDictionary[SchedulerConfig.FRGSUID].TimerElapsed += ScheduleTickHandler;
                            break;
                      
                    }
                }

                // handle connection details change
                if (e.TableName == Globals.SystemManager.GetTableName("FDASourceConnections"))
                {                    
                    string changeType = e.ChangeType;
                    if (changeType == "UPDATE")
                    {

                        // get the connection object from the dictionary
                        if (_connectionsDictionary == null)
                            return;
                        if (_connectionsDictionary.ContainsKey(e.ID))
                        {
                            ConnectionManager conn = _connectionsDictionary[e.ID];

                            // get the connection configuration object that was updated from the database manager
                            FDASourceConnection updatedConfig = _dbManager.GetConnectionConfig(e.ID);

                            if (conn.ConnectionType.ToString().ToUpper() != updatedConfig.SCType.ToUpper())
                            {
                                try
                                {
                                    conn.ConnectionType = (ConnectionManager.ConnType)Enum.Parse(typeof(ConnectionManager.ConnType), updatedConfig.SCType);
                                }
                                catch (Exception ex)
                                {
                                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "unrecognized connection type '" + updatedConfig.SCType.ToString() + "' in connection " + updatedConfig.SCUID.ToString());
                                }
                            }

                            conn.ConnDetails = updatedConfig.SCDetail01;
                            string[] connParams = updatedConfig.SCDetail01.Split(':');
                            
                            
                            switch (conn.ConnectionType)
                            {   
                                // Ethernet specific configuration
                                case ConnectionManager.ConnType.Ethernet:
                                    if (conn.RemoteIPAddress != connParams[0]) conn.RemoteIPAddress = connParams[0];
                                    int newPort;
                                    newPort = int.Parse(connParams[1]);
                                    if (conn.PortNumber != newPort) conn.PortNumber = newPort;
                                    break;
                                case ConnectionManager.ConnType.EthernetUDP:
                                    newPort = int.Parse(connParams[0]);
                                    if (conn.PortNumber != newPort) conn.PortNumber = newPort;
                                    break;
                                case ConnectionManager.ConnType.Serial:
                                    // Serial specific configuration
                                    Parity parity = Parity.None;
                                    switch (connParams[3])
                                    {
                                        case "N": parity = Parity.None; break;
                                        case "E": parity = Parity.Even; break;
                                        case "M": parity = Parity.Mark; break;
                                        case "O": parity = Parity.Odd; break;
                                        case "S": parity = Parity.Space; break;
                                    }

                                    StopBits stopBits = StopBits.None;
                                    switch (connParams[4])
                                    {
                                        case "N": stopBits = StopBits.None; break;
                                        case "1": stopBits = StopBits.One; break;
                                        case "1.5": stopBits = StopBits.OnePointFive; break;
                                        case "2": stopBits = StopBits.Two; break;
                                    }


                                    if (conn.SerialPortName != connParams[0]) conn.SerialPortName = "COM"+connParams[0];
                                    if (conn.SerialBaudRate != int.Parse(connParams[1])) conn.SerialBaudRate = int.Parse(connParams[1]);
                                    if (conn.SerialDataBits != int.Parse(connParams[2])) conn.SerialDataBits = int.Parse(connParams[2]);
                                    if (conn.SerialParity != parity) conn.SerialParity = parity;
                                    if (conn.SerialStopBits != stopBits) conn.SerialStopBits = stopBits;
                                    break;
                            }

                            if (conn.Description != updatedConfig.Description) conn.Description = updatedConfig.Description;
                            if (conn.MaxRequestAttempts != updatedConfig.MaxRequestAttempts) conn.MaxRequestAttempts = updatedConfig.MaxRequestAttempts;
                            if (conn.MaxSocketConnectionAttempts != updatedConfig.MaxSocketConnectionAttempts) conn.MaxSocketConnectionAttempts = updatedConfig.MaxSocketConnectionAttempts;
                            if (conn.PostConnectionCommsDelay != updatedConfig.PostConnectionCommsDelay) conn.PostConnectionCommsDelay = updatedConfig.PostConnectionCommsDelay;
                            if (conn.RequestResponseTimeout != updatedConfig.RequestResponseTimeout) conn.RequestResponseTimeout = updatedConfig.RequestResponseTimeout;
                            if (conn.RequestRetryDelay != updatedConfig.RequestRetryDelay) conn.RequestRetryDelay = updatedConfig.RequestRetryDelay;
                            if (conn.SocketConnectionAttemptTimeout != updatedConfig.SocketConnectionAttemptTimeout) conn.SocketConnectionAttemptTimeout = updatedConfig.SocketConnectionAttemptTimeout;
                            if (conn.SocketConnectionRetryDelay != updatedConfig.SocketConnectionRetryDelay) conn.SocketConnectionRetryDelay = updatedConfig.SocketConnectionRetryDelay;
                            if (conn.CommunicationsEnabled != updatedConfig.CommunicationsEnabled) conn.CommunicationsEnabled = updatedConfig.CommunicationsEnabled;
                            if (conn.ConnectionEnabled != updatedConfig.ConnectionEnabled) conn.ConnectionEnabled = updatedConfig.ConnectionEnabled;
                            if (conn.CommsLogEnabled != updatedConfig.CommsLogEnabled) conn.CommsLogEnabled = updatedConfig.CommsLogEnabled;
                            if (conn.InterRequestDelay != updatedConfig.InterRequestDelay) conn.InterRequestDelay = updatedConfig.InterRequestDelay;
                            //if (conn.MQTTEnabled != connConfig.MQTTEnabled) conn.MQTTEnabled = connConfig.MQTTEnabled;
                        }
                        else
                            changeType = "Insert"; // this was an update but no connection object with this ID was found, change it to an insert instead (will be processed as an insert below)

                    }

                    if (changeType == "DELETE")
                    {
                        if (_connectionsDictionary.ContainsKey(e.ID))
                        {
                            ConnectionManager connToDelete = _connectionsDictionary[e.ID];
                            connToDelete.TransactionComplete -= TransactionCompleteHandler;

                            // remove the doomed connection object from the dictionary
                            _connectionsDictionary.Remove(e.ID);

                            // disable it, and dispose of it
                            connToDelete.CommunicationsEnabled = false;
                            connToDelete.ConnectionEnabled = false;
                            connToDelete.Dispose();
                            connToDelete = null;

                            //PublishUpdatedConnectionsList();
                        }

                        // connection deletion may cause previously valid request groups to become invalid, mark all valid groups for re-validation
                        RevalidateRequestGroups(1);
                    }

                    if (changeType == "INSERT")
                    {
                        FDASourceConnection connConfig = _dbManager.GetConnectionConfig(e.ID);

                        // separate the hostname from the port
                        string[] connDetails = connConfig.SCDetail01.Split(':');

                        // create the new connection
                        ConnectionManager newConn = null;
                        bool success = true;
                        try
                        {
                            newConn = new ConnectionManager(connConfig.SCUID, connConfig.Description, connDetails[0], int.Parse(connDetails[1]))
                            {
                                RequestRetryDelay = (short)connConfig.RequestRetryDelay,
                                SocketConnectionAttemptTimeout = connConfig.SocketConnectionAttemptTimeout,
                                MaxSocketConnectionAttempts = connConfig.MaxSocketConnectionAttempts,
                                SocketConnectionRetryDelay = connConfig.SocketConnectionRetryDelay,
                                PostConnectionCommsDelay = connConfig.PostConnectionCommsDelay,
                                InterRequestDelay = connConfig.InterRequestDelay,
                                MaxRequestAttempts = connConfig.MaxRequestAttempts,
                                RequestResponseTimeout = connConfig.RequestResponseTimeout,
                                CommsLogEnabled = connConfig.CommsLogEnabled
                            };
                        }
                        catch (Exception ex)
                        {
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while creating connection object " + connConfig.SCUID);
                            success = false;
                        }

                        if (success)
                        {
                            try
                            {
                                newConn.ConnectionType = (ConnectionManager.ConnType)Enum.Parse(typeof(ConnectionManager.ConnType), connConfig.SCType);
                                newConn.TransactionComplete += TransactionCompleteHandler;
                                // add it to our dictionary
                                _connectionsDictionary.Add(e.ID, newConn);

                                // enable it (if configured to be enabled)
                                newConn.CommunicationsEnabled = connConfig.CommunicationsEnabled;
                                newConn.ConnectionEnabled = connConfig.ConnectionEnabled;

                                //PublishUpdatedConnectionsList();
                            }
                            catch (Exception ex)
                            {
                                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "unrecognized connection type '" + connConfig.SCType.ToString() + "' in connection " + connConfig.SCUID.ToString());
                                newConn.ConnectionEnabled = false;
                                newConn.Dispose();
                                newConn = null;
                            }
                        }

                        // adding a new connection may cause previously invalid groups to become valid, mark all invalid groups for revalidation
                        RevalidateRequestGroups(2);
                    }

                    PublishUpdatedConnectionsList();
                }

                // handle request group config changes 
                if (e.TableName == Globals.SystemManager.GetTableName("FDADataBlockRequestGroup"))
                {
                    if (e.ChangeType == "UPDATE")
                    {
                        // get the updated group config from the database
                        FDADataBlockRequestGroup updatedGroupConfig = _dbManager.GetRequestGroup(e.ID);

                        // find any schedulers that reference this group
                        foreach (FDAScheduler scheduler in _schedulersDictionary.Values)
                        {
                            RequestGroup groupToUpdate = scheduler.RequestGroupList.Find(group => group.ID == updatedGroupConfig.DRGUID);

                            // update the group properties
                            if (groupToUpdate != null)
                            {
                                groupToUpdate.DBGroupRequestConfig.DataPointBlockRequestListVals = updatedGroupConfig.DataPointBlockRequestListVals;
        
                                groupToUpdate.Description = updatedGroupConfig.Description;
                       
                                groupToUpdate.Enabled = updatedGroupConfig.DRGEnabled;
                           
                                groupToUpdate.CommsLogEnabled = updatedGroupConfig.CommsLogEnabled;
       
                                groupToUpdate.Protocol = updatedGroupConfig.DPSType;

                                groupToUpdate.Validation = RequestGroup.ValidationState.Unvalidated;
                            }
                        }
                    }

                    if (e.ChangeType == "DELETE")
                    {
                        foreach (FDAScheduler scheduler in _schedulersDictionary.Values)
                        {
                            RequestGroup deletedGroup = scheduler.RequestGroupList.Find(group => group.ID == e.ID);
                            while (deletedGroup != null)
                            {
                                scheduler.RequestGroupList.Remove(deletedGroup);
                                deletedGroup = scheduler.RequestGroupList.Find(group => group.ID == e.ID);
                            }
                        }
                    }

                    if (e.ChangeType == "Insert")
                    {
                        // check if the new group is referenced by any scheduler configs
                        List<FDARequestGroupScheduler> schedConfigList = _dbManager.GetAllSched();
                        FDADataBlockRequestGroup groupConfig = _dbManager.GetRequestGroup(e.ID);
                        int idx = -1;
                        int separatorIdx;
                        int length;
                        string requestConfig;
      
                        foreach (FDARequestGroupScheduler sched in schedConfigList)
                        {
                            
                            idx = sched.RequestGroupList.IndexOf(e.ID.ToString().ToUpper());
                            if (idx > -1)  // the group ID was found in the scheduler's configured RequestGroupList
                            {
                                separatorIdx = sched.RequestGroupList.IndexOf('|', idx);

                                // if there was no separator found after the index of the group id, it's the last group in the list
                                if (separatorIdx == -1)
                                    separatorIdx = sched.RequestGroupList.Length;

                                length = separatorIdx - idx;
                                requestConfig = sched.RequestGroupList.Substring(idx, length);

                                if (_schedulersDictionary.ContainsKey(sched.FRGSUID))
                                {
                                    RequestGroup newGroup = _dbManager.RequestGroupFromConfigString("Schedule " + sched.FRGSUID.ToString(), requestConfig, sched.Priority);
                                    newGroup.Protocol = groupConfig.DPSType.ToUpper();
                                    // add the new group the schedule
                                    lock (_schedulersDictionary[sched.FRGSUID].RequestGroupList)
                                    {
                                        _schedulersDictionary[sched.FRGSUID].RequestGroupList.Add(newGroup);
                                    }
                                }
                            }
                        }
                        
                    }
                }

                // handle task changes
                if (e.TableName == Globals.SystemManager.GetTableName("FDATasks"))
                {
                    if (e.ChangeType == "UPDATE")
                    {
                        // get the updated task definition from the database manager
                        FDATask updatedTask = _dbManager.GetTask(e.ID);

                        // find any schedulers that reference this group
                        foreach (FDAScheduler scheduler in _schedulersDictionary.Values)
                        {
                            FDATask taskToUpdate = scheduler.TasksList.Find(task => task.TASK_ID == updatedTask.TASK_ID);
                            if (taskToUpdate != null)
                            {
                                taskToUpdate.task_type = updatedTask.task_type;
                                taskToUpdate.task_details = updatedTask.task_details;
                            }
                        }
                    }

                    if (e.ChangeType == "DELETE")
                    {
                        foreach (FDAScheduler scheduler in _schedulersDictionary.Values)
                        {
                            FDATask deletedTask = scheduler.TasksList.Find(task => task.TASK_ID == e.ID);
                            while (deletedTask != null)
                            {
                                scheduler.TasksList.Remove(deletedTask);
                                deletedTask = scheduler.TasksList.Find(task => task.TASK_ID == e.ID);
                            }
                        }
                    }
                }


                // handle data point definition changes 
                if (e.TableName == Globals.SystemManager.GetTableName("DataPointDefinitionStructures"))
                {
                    // mark request groups for revalidation
                    if (e.ChangeType == "INSERT")
                        RevalidateRequestGroups(2);

                    if (e.ChangeType == "DELETE")
                        RevalidateRequestGroups(1);
                    
                }
                

                // handle system options changes
                if (e.TableName == "FDAConfig")
                {
                    ApplyConfigOptions();
                }
            } catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while handling a database event (insert,delete, or update) in table " + e.TableName + ", item ID " + e.ID);
            }
        }
        

        private void ApplyConfigOptions()
        {
            Dictionary<string, FDAConfig> configs = Globals.SystemManager.GetAppConfig();

            if (configs.ContainsKey("EnableDebugLogging"))
                Globals.SystemManager.EnableDebugMessages = (configs["EnableDebugLogging"].OptionValue == "1");

            if (configs.ContainsKey("DetailedMessaging"))
                Globals.DetailedMessaging = (configs["DetailedMessaging"].OptionValue == "on");

            try
            {
                if (configs.ContainsKey("BatchInsertMaxRecords"))
                {
                    int cacheSize = int.Parse(configs["BatchInsertMaxRecords"].OptionValue);
                    if (cacheSize > 500)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "", "FDAConfig option 'BatchInsertMaxRecords' value of " + cacheSize + " is higher than the maximum of 500. Using default value of 500");
                        cacheSize = 500;
                    }
                    _dbManager.UpdateCacheSize(cacheSize);
                }
                else
                    _dbManager.UpdateCacheSize(500);
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while parsing the value of the BatchInsertMaxRecords option in the FDAConfig table. Using default value of 900");
                _dbManager.UpdateCacheSize(900);
            }

            try
            {
                if (configs.ContainsKey("BatchInsertMaxTime"))
                    _dbManager.UpdateCacheTimeout(int.Parse(configs["BatchInsertMaxTime"].OptionValue));
                else
                    _dbManager.UpdateCacheTimeout(500);
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while parsing the value of the BatchInsertMaxTime option in the FDAConfig table. Using default value of 500 ms");
                _dbManager.UpdateCacheTimeout(500);
            }


            try
            {
                if (configs.ContainsKey("CommsLogMaxDays"))
                    Globals.SystemManager.CommsLogMaxDays = double.Parse(configs["CommsLogMaxDays"].OptionValue);
                else
                    Globals.SystemManager.CommsLogMaxDays = 1;
            }
            catch(Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while parsing the value of the CommsLogMaxDays option in the FDAConfig table. Using default value of 1");
                Globals.SystemManager.CommsLogMaxDays = 1;
            }

            try
            {
                if (configs.ContainsKey("EventLogMaxDays"))
                    Globals.SystemManager.EventLogMaxDays = double.Parse(configs["EventLogMaxDays"].OptionValue);
                else
                    Globals.SystemManager.EventLogMaxDays = 1;
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while parsing the value of the EventLogMaxDays option in the FDAConfig table. Using default value of 1");
                Globals.SystemManager.EventLogMaxDays = 1;
            }

            try
            {
                if (configs.ContainsKey("UTCOffset"))
                {
                    Globals.UTCOffset = int.Parse(configs["UTCOffset"].OptionValue);
                }
                else
                    Globals.UTCOffset = 0;
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while parsing the value of the UTCOffset option in the FDAConfig table. Using default value of 0");
                Globals.UTCOffset = 0;
            }

        }

        private void DemandRequestHandler(object sender, DBManager.DemandEventArgs e)
        {
            if (Globals.FDAStatus == Globals.AppState.ShuttingDown || Globals.FDAStatus == Globals.AppState.Pausing || Globals.FDAStatus == Globals.AppState.Paused)
                return;

            Globals.SystemManager.LogApplicationEvent(this, "", "Demand command received : " + e.DemandRequestObject.FRGDUID.ToString());

            // validate requested group(s)
            List<RequestGroup> validGroups = ValidateRequestedGroups(e.RequestGroups,"Demand " + e.DemandRequestObject.FRGDUID);

          
            // if there are still any groups in the list after validation, continue
            if (validGroups.Count > 0)
            {
                PrepareAndSendRequestGroups(validGroups, Globals.RequesterType.Demand, e.DemandRequestObject.FRGDUID);
            }
        }


        private void ScheduleTickHandler(object sender, FDAScheduler.TimerEventArgs e)
        {
            if (Globals.FDAStatus == Globals.AppState.ShuttingDown || Globals.FDAStatus == Globals.AppState.Pausing || Globals.FDAStatus == Globals.AppState.Paused)
                return;

            FDAScheduler timer = (FDAScheduler)sender;

            Globals.SystemManager.LogApplicationEvent(this, "", "Schedule '" + timer.Description + "' triggered");

            //validate requested group(s)
            List<RequestGroup> validGroups = ValidateRequestedGroups(timer.RequestGroupList, "Schedule " + timer.ID);

            // if there are still any groups in the list after validation, continue
            if (validGroups.Count > 0)
            {
                PrepareAndSendRequestGroups(validGroups, Globals.RequesterType.Schedule, timer.ID);
            }

            // if there are any tasks in the taskList, do those too
            if (timer.TasksList.Count > 0)
            {
                foreach (FDATask task in timer.TasksList)
                {
                    switch (task.task_type.ToUpper())
                    {
                        case "CALCCOMMSSTATS":
                            // old style
                            /*
                            string[] task_details = task.task_details.Split(':');
                            int hoursBack;
                            if (!int.TryParse(task_details[0], out hoursBack))
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "", "unable to execute task id " + task.task_id + ", due to invalid entry in task_details[0]");
                                continue;
                            }
                            string alternateTable = "";
                            string connectionFilter = "";
                            string deviceFilter = "";
                            if (task_details.Length > 1)
                                connectionFilter = task_details[1];
                            if (task_details.Length > 2)
                                 deviceFilter = task_details[2];
                            if (task_details.Length > 3)
                                alternateTable = task_details[3];

                            DateTime toTime = Globals.FDANow();
                            DateTime fromTime = toTime.AddHours(-1 * hoursBack);
                            */
                            string error = "";
                            List<Object> commsStatsParams = _dbManager.ParseStatsCalcParams(task.task_details,out error);
                            if (error == "" && commsStatsParams != null)
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "", "Calculating communications statistics (requested by schedule " + timer.ID);
                                _dbManager.RemoteQueryManager.DoCommsStats(commsStatsParams);
                            }
                            else
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "", "invalid task_details in task " + task.TASK_ID + ": " + error +". The task will not be exectuted");
                            }
                            break;

                    }
                }
            }
        }

                                       


        /// <summary>
        /// confirms that all references contained in each requested group are valid
        ///  requestor contains a string for event messages, consisting of "[System/Schedule/Demand] [ID]"
        ///  posts a message for any invalid groups
        ///  returns a list of valid groups
        /// </summary>
        /// <param name="groupstoValidate"></param>
        /// <param name="requestor"></param>
        private List<RequestGroup> ValidateRequestedGroups(List<RequestGroup> groupstoValidate,string requestor)
        {
            List<RequestGroup> goodGroups = new List<RequestGroup>(groupstoValidate);
            List<RequestGroup> badGroups = null;


            foreach (RequestGroup group in groupstoValidate)
            {
                // if the group is disabled, add it to the badGroups list so it gets removed
                if (!group.Enabled)
                {
                    if (badGroups == null)
                    {
                        badGroups = new List<RequestGroup>();
                    }
                    badGroups.Add(group);
                    continue;
                }
                // if the group was requested by a scheduler, check if it's already been validated (don't do validation if it's already been done)
                if (requestor.Contains("Schedule"))
                {
                    // group has been previously validated and is good
                    if (group.Validation == RequestGroup.ValidationState.Valid)
                    {
                        //Globals.SystemManager.LogApplicationEvent(this, "", "The DataBlockRequestGroup '" + group.Description + "' (" + group.ID + ") requested by " + requestor + " has been previously marked as valid. Skipping validation for this group");
                        continue;
                    }
                    // group has been previously validated and was found to be invalid
                    if (group.Validation == RequestGroup.ValidationState.Invalid)
                    {
                        //Globals.SystemManager.LogApplicationEvent(this, "", "The DataBlockRequestGroup '" + group.Description + "' (" + group.ID + ") requested by " + requestor + " has been previously marked as invalid. This group will not be processed",true);
                        if (badGroups == null)
                            badGroups = new List<RequestGroup>();
                        badGroups.Add(group);
                        continue;
                    }
                }

                // group hasn't been previously validated, do that now
                // Globals.SystemManager.LogApplicationEvent(this, "", "The DataBlockRequestGroup '" + group.Description + "' (" + group.ID + ") requested by " + requestor + " has not been validated, doing validation now");

               

                // connection reference is good
                if (!_connectionsDictionary.ContainsKey(group.ConnectionID))
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "The DataBlockRequestGroup '" + group.Description + "' (" + group.ID + ") requested by " + requestor + " references a connection that does not exist (" + group.ConnectionID + "). This group will not be processed",true);
                    if (badGroups == null)
                        badGroups = new List<RequestGroup>();
                    badGroups.Add(group);
                }

                // valid protocol
                if (!Globals.SupportedProtocols.Contains(group.Protocol))
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "The DataBlockRequestGroup '" + group.Description + "' (" + group.ID + ") requested by " + requestor + " references an unrecognized protocol '" + group.Protocol + "'. This group will not be processed",true);
                    if (badGroups == null)
                        badGroups = new List<RequestGroup>();
                    badGroups.Add(group);
                }

                if (group.DBGroupRequestConfig.DataPointBlockRequestListVals == null)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "The DataBlockRequestGroup '" + group.Description + "' (" + group.ID + ") requested by " + requestor + " has a null DataPointBlockRequestListVals field. This group will not be processed", true);
                    if (badGroups == null)
                        badGroups = new List<RequestGroup>();
                    badGroups.Add(group);
                }

                // group config string ("DataPointBlockRequestListVals") is correctly formatted
                if (group.DBGroupRequestConfig.DataPointBlockRequestListVals != null)
                {
                    // check for empty string in DataPointBlockRequestListVals
                    if (group.DBGroupRequestConfig.DataPointBlockRequestListVals == string.Empty)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "", "The DataBlockRequestGroup '" + group.Description + "' (" + group.ID + ") requested by " + requestor + " has an empty DataPointBlockRequestListVals field. This group will not be processed", true);
                        if (badGroups == null)
                            badGroups = new List<RequestGroup>();
                        badGroups.Add(group);
                    }
                    else
                    {

                        string[] requests = group.DBGroupRequestConfig.DataPointBlockRequestListVals.Split('$');

                        bool goodRequestString = true;
                        foreach (string request in requests)
                        {
                            // ask the protocol to validate the request (request format is protocol dependent)
                            switch (group.Protocol)
                            {
                                case "ROC": goodRequestString = ROC.ROCProtocol.ValidateRequestString(this, group.ID.ToString(), requestor, request, _dbManager.GetAllTagDefs(),_dbManager.GetAllDevices()); break;
                                case "MODBUS": goodRequestString = Modbus.ModbusProtocol.ValidateRequestString(this, group.ID.ToString(), requestor, request, _dbManager.GetAllTagDefs()); break;
                                case "MODBUSTCP": goodRequestString = Modbus.ModbusProtocol.ValidateRequestString(this, group.ID.ToString(), requestor, request, _dbManager.GetAllTagDefs()); break;
                                case "ENRONMODBUS": goodRequestString = Modbus.ModbusProtocol.ValidateRequestString(this, group.ID.ToString(), requestor, request, _dbManager.GetAllTagDefs()); break;
                                case "BSAP": goodRequestString = BSAP.BSAPProtocol.ValidateRequestString(this, group.ID.ToString(), requestor, request, _dbManager.GetAllTagDefs(),_dbManager.GetAllDevices(),false); break;
                                case "BSAPUDP": goodRequestString = BSAP.BSAPProtocol.ValidateRequestString(this, group.ID.ToString(), requestor, request, _dbManager.GetAllTagDefs(), _dbManager.GetAllDevices(),true); break;
                            }

                            if (!goodRequestString)
                            {
                                if (badGroups == null)
                                    badGroups = new List<RequestGroup>();
                                if (!badGroups.Contains(group))
                                    badGroups.Add(group);
                            }
                        }
                    }
                }

                if (badGroups != null)
                {
                    if (badGroups.Contains(group))
                    {
                        group.Validation = RequestGroup.ValidationState.Invalid;
                    }
                    else
                    {
                        group.Validation = RequestGroup.ValidationState.Valid;
                        //Globals.SystemManager.LogApplicationEvent(this, "", "Validation for the DataBlockRequestGroup '" + group.Description + "' (" + group.ID + ") requested by " + requestor + " is complete, the group is valid");
                    }
                }
                else
                {
                    group.Validation = RequestGroup.ValidationState.Valid;
                   //Globals.SystemManager.LogApplicationEvent(this, "", "Validation for the DataBlockRequestGroup '" + group.Description + "' (" + group.ID + ") requested by " + requestor + " is complete, the group is valid");
                }
            }

      
            if (badGroups != null)
            {
                foreach (RequestGroup badGroup in badGroups)
                {
                    goodGroups.Remove(badGroup);
                }
            }

            return goodGroups;
        }

   
        private void PrepareAndSendRequestGroups(List<RequestGroup> requestGroupTemplates, Globals.RequesterType requesterType,Guid requesterID)
        {
             RequestGroup clonedGroup;

            foreach (RequestGroup templateGroup in requestGroupTemplates)
            {

                if (requesterType == Globals.RequesterType.Demand || requesterType == Globals.RequesterType.Schedule)
                {
                    // make a copy of the RequestGroup template
                    clonedGroup = (RequestGroup)templateGroup.Clone();
                    clonedGroup.RequesterType = requesterType;
                    clonedGroup.RequesterID = requesterID;
                }
                else
                {
                    // requesterType is System, these groups are generated on the fly and are one-use, so don't clone anything just use them 'as is'
                    clonedGroup = templateGroup;
                }
       
                // send the group to the protocol to identify write requests and create a list of tags that need to have values looked up
                switch (clonedGroup.Protocol.ToUpper())
                {
                    case "ROC": ROC.ROCProtocol.IdentifyWrites(clonedGroup); break;
                    case "MODBUS": Modbus.ModbusProtocol.IdentifyWrites(clonedGroup); break;
                    case "MODBUSTCP": Modbus.ModbusProtocol.IdentifyWrites(clonedGroup); break;
                    case "ENRONMODBUS":  Modbus.ModbusProtocol.IdentifyWrites(clonedGroup); break;
                    case "BSAP": BSAP.BSAPProtocol.IdentifyWrites(clonedGroup); break;
                    case "BSAPUDP": BSAP.BSAPProtocol.IdentifyWrites(clonedGroup); break;
                    default:
                        Globals.SystemManager.LogApplicationEvent(this, "", "unrecognized protocol '" + clonedGroup.Protocol + "' in group '" + clonedGroup.Description + "' (" + clonedGroup.ID + ")");
                        continue;
                }

                // if any of the requests in the group have been identified by the proctocol as 'write' requests,
                // look up the values to be written (from the database), and add them to the request
                if (clonedGroup.writeLookup != null)
                {
                    if (clonedGroup.writeLookup.Count > 0)
                        GetValuesToWriteFromDB(clonedGroup);
                }

                // send the request string to the protocol for pre-processing
                // currently only modbus does any pre-processing, it can be added to other protocols if needed
                string requestString = clonedGroup.DBGroupRequestConfig.DataPointBlockRequestListVals;
                switch (clonedGroup.Protocol.ToUpper())
                {
                    case "MODBUS": clonedGroup.DBGroupRequestConfig.DataPointBlockRequestListVals = Modbus.ModbusProtocol.PreprocessRequestString(requestString); break;
                    case "MODBUSTCP": clonedGroup.DBGroupRequestConfig.DataPointBlockRequestListVals = Modbus.ModbusProtocol.PreprocessRequestString(requestString); break;
                    case "ENRONMODBUS": clonedGroup.DBGroupRequestConfig.DataPointBlockRequestListVals = Modbus.ModbusProtocol.PreprocessRequestString(requestString); break;
                }


                // send the RequestGroup out to the connection to be processed
                SendToConnectionManager(clonedGroup);             
            }
            
        }


        private void SendToConnectionManager(RequestGroup group)
        {

            if (_connectionsDictionary.ContainsKey(group.ConnectionID))
            {
                if (group.RequesterType == Globals.RequesterType.System)
                {
                    _connectionsDictionary[group.ConnectionID].QueueTransactionGroup(group);
                }
                else
                {
                    FDADataBlockRequestGroup groupConfig = _dbManager.GetRequestGroup(group.ID);
                    if (groupConfig != null)
                    {
                        if (groupConfig.DRGEnabled)
                            _connectionsDictionary[group.ConnectionID].QueueTransactionGroup(group);
                    }
                }


            }
        }

        private void GetValuesToWriteFromDB(RequestGroup group)
        {
            string[] tableName = group.DestinationID.Split(',');
           
            // ask the dbManager to get the values from the database
            _dbManager.GetLastValues(tableName[0],group.writeLookup);

           
            // reverse scale the values, if needed
            FDADataPointDefinitionStructure config;
            Guid[] keys = new Guid[group.writeLookup.Count];
            double[] values = new double[group.writeLookup.Count];

            group.writeLookup.Keys.CopyTo(keys, 0);
            group.writeLookup.Values.CopyTo(values, 0);

            Dictionary<Guid, double> updated = new Dictionary<Guid, double>();

            for (int i=0;i<keys.Length;i++)                
            {

                config = _dbManager.GetTagDef(keys[i]);
                Double scaledValue = values[i];
                if (config.write_scaling && config.write_scale_eu_high != config.write_scale_eu_low)
                {
                    // reverse scale the value to be written to device (using the write scaling parameters)
                    double unscaledvalue = ((scaledValue - config.write_scale_eu_low) / (config.write_scale_eu_high - config.write_scale_eu_low) * (config.write_scale_raw_high - config.write_scale_raw_low) + config.write_scale_raw_low);
                    updated.Add(keys[i], unscaledvalue);
                }
                else
                {
                    updated.Add(keys[i], scaledValue);
                }
            }

            group.writeLookup.Clear();
            group.writeLookup = updated;


        }

        public void AddDataConnection(ConnectionManager connection)
        {
            _connectionsDictionary.Add(connection.ConnectionID, connection);
        }

        public ConnectionManager GetDataConnection(Guid connectionID)
        {
            if (_connectionsDictionary.ContainsKey(connectionID))
                return _connectionsDictionary[connectionID];
            else
                return null;
        }

        public void Dispose()
        {
            

            if (_schedulersDictionary != null)
            {
                foreach (FDAScheduler sched in _schedulersDictionary.Values)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "Stopping Scheduler '" + sched.Description + "' (" + sched.ID + ")");
                    sched.Enabled = false;
                    sched.Dispose();
                }
                _schedulersDictionary.Clear();
                _schedulersDictionary = null;
            }

            // dispose connections and then clear the connections dictionary
            if (_connectionsDictionary != null)
            {
                foreach (ConnectionManager conn in _connectionsDictionary.Values)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "Shutting down connection manager for '" + conn.Description + "' (" + conn.ConnectionID + ")");
                    conn.Dispose();
                }

                _connectionsDictionary.Clear();
                _connectionsDictionary = null;
            }

            _dbManager?.Dispose();
        }
    }
}
