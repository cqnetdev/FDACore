using System;
using System.Collections.Generic;
using Common;
using System.IO.Ports;
using TableDependency.SqlClient.Base.EventArgs;
using System.Text;
using System.Threading;
using uPLibrary.Networking.M2Mqtt;
using Support;
using Scripting;
using System.Threading.Tasks;

namespace FDA
{
    public class DataAcqManager : IDisposable
    {
        public static Dictionary<Guid, RRConnectionManager> _RRconnectionsDictionary;
        public static Dictionary<Guid, PubSubConnectionManager> _PubSubConnectionsDictionary;
        private readonly Dictionary<Guid,FDAScheduler> _schedulersDictionary;
        private string _dbConnectionString;
        private readonly DBManager _dbManager;

        private readonly BackfillManager _backfillManager;

        public string DBConnectionString { get => _dbConnectionString; set => _dbConnectionString = value; }
        public string ID { get; set; }
        public readonly Guid ExecutionID;


        public delegate void MQTTEnabledChangeHandler(object sender, BoolEventArgs e);
        public event MQTTEnabledChangeHandler MQTTEnableStatusChanged;

        // temporary test variable
        //private bool firstGoAround = true;

        public DataAcqManager(DBManager dbManager,Guid executionID)
        {
            _dbManager = dbManager;


            _backfillManager = new BackfillManager(_dbManager);
            _backfillManager.BackfillAnalysisComplete += BackfillAnalysisCompleteHandler;

            ExecutionID = executionID;

            _schedulersDictionary = new Dictionary<Guid, FDAScheduler>();
            _RRconnectionsDictionary = new Dictionary<Guid, RRConnectionManager>();
            _PubSubConnectionsDictionary = new Dictionary<Guid, PubSubConnectionManager>();

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

        private void HandleUserScriptCompileError(string scriptID, List<string> errors)
        {
            Scripter.GetScript(scriptID).Enabled = false;
            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(),"Script","","User script '" + scriptID + "' has been disabled because it failed to compile, error(s): " + String.Join("\n",errors));
        }
        private void HandleUserScriptRuntimeError(string scriptID, string errorMsg)
        {
            Scripter.GetScript(scriptID).Enabled = false;
            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "Script", "", "User script '" + scriptID + "' has been disabled because it produced a run-time error: " + errorMsg);
        }

        private void HandleUserScriptExecutedEvent(string scriptID)
        {
            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(),"Script", "", "User script " + scriptID + "() executed");
        }

        public static int GetTotalQueueCounts()
        {
            int count = 0;
            if (_RRconnectionsDictionary != null)
            {
                foreach (RRConnectionManager mgr in _RRconnectionsDictionary.Values)
                    count += mgr.TotalQueueCount;
            }

            if (_PubSubConnectionsDictionary != null)
            {
                foreach (PubSubConnectionManager mgr in _PubSubConnectionsDictionary.Values)
                    count += mgr.TotalQueueCount;
            }
            
            return count;
        }


        public bool TestDBConnection()
        {
            return _dbManager.TestDatabaseConnection();
        }

        public void Start()
        {
            try // general error catching for the start() function
            {

                // this was after loadconfig...move it back there if putting it first causes some problem
                _dbManager.Initialize();

                // load the initial configuration
                _dbManager.LoadConfig();



                Globals.DBManager = _dbManager;
               
         
                // apply any app configuration options
                HandleAppConfigChanges();

                // subscribe to configuration change events from the DBManager
                _dbManager.ConfigChange += ConfigChangeHandler;
                _dbManager.DemandRequest += DemandRequestHandler;
                _dbManager.ForceScheduleExecution += ForceScheduleExecutionHandler;

                // create connection objects for each DSSourceConnection            
                List<FDASourceConnection> ConnectionList = _dbManager.GetAllConnectionconfigs();

                List<Task> taskList = new();
                foreach (FDASourceConnection connectionconfig in ConnectionList)
                {
                    string sctype = connectionconfig.SCType.ToUpper();

                    if (sctype == "ETHERNET" || sctype == "ETHERNETUDP" || sctype == "SERIAL")
                    {
                        // CreateRRConnectionMgr(connectionconfig)
                        taskList.Add(Task.Factory.StartNew(()=> CreateRRConnectionMgr(connectionconfig)));
                    }

                    if (sctype == "OPCDA" || sctype == "OPCUA" || sctype == "MQTT")
                    {
                        taskList.Add(Task.Factory.StartNew(() => CreatePubSubConnectionMgr(connectionconfig)));
                    }
                }

                Task.WaitAll(taskList.ToArray());
                taskList.Clear();

                // start the database config change monitors (not needed, SQLTableDependency doesn't support postgres)
                //_dbManager.StartChangeMonitoring();

                // create timers for each Schedule
                try
                {
                    Random rnd = new(DateTime.Now.Millisecond);
                    foreach (FDARequestGroupScheduler sched in _dbManager.GetAllSched())
                    {

                        switch (sched.FRGSType.ToUpper())
                        {
                            case "REALTIME": _schedulersDictionary.Add(sched.FRGSUID, new FDASchedulerRealtime(sched.FRGSUID, sched.Description, sched.RequestGroups,sched.Tasks,sched.RealTimeRate,TimeSpan.FromSeconds(rnd.Next(10)))); break;
                            case "DAILY": _schedulersDictionary.Add(sched.FRGSUID, new FDASchedulerDaily(sched.FRGSUID, sched.Description, sched.RequestGroups, sched.Tasks, new DateTime(sched.Year, sched.Month, sched.Day, sched.Hour, sched.Minute, sched.Second))); break;
                            case "HOURLY": _schedulersDictionary.Add(sched.FRGSUID, new FDASchedulerHourly(sched.FRGSUID, sched.Description, sched.RequestGroups, sched.Tasks, new DateTime(sched.Year, sched.Month, sched.Day, sched.Hour, sched.Minute, sched.Second))); break;
                            case "MONTHLY": _schedulersDictionary.Add(sched.FRGSUID, new FDASchedulerMonthly(sched.FRGSUID, sched.Description, sched.RequestGroups, sched.Tasks,new DateTime(sched.Year, sched.Month, sched.Day, sched.Hour, sched.Minute, sched.Second))); break;
                            case "ONSTARTUP":  _schedulersDictionary.Add(sched.FRGSUID, new FDASchedulerOneshot(sched.FRGSUID, sched.Description, sched.RequestGroups, sched.Tasks)); break;
                            default: Globals.SystemManager.LogApplicationEvent(this, "", "Schedule " + sched.FRGSUID + " has unrecognized schedule type '" + sched.FRGSType + "' - this schedule will not be loaded"); break;
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

                if (Globals.MQTTEnabled)
                {
                    PublishUpdatedConnectionsList();

                    // publish the default CommsStats table name
                    string tableString = Globals.SystemManager.GetTableName("CommsStats");
                    byte[] tableBytes = Encoding.UTF8.GetBytes(tableString);
                    Globals.MQTT.Publish("FDA/DefaultCommsStatsTable", tableBytes, 0, true);
                }


                // set up the scripter
                Scripter.AddNamespace(new string[] {"System"});
                //Scripter.AddReference(new string[] { }); // Todo: what references do user scripts need?
                Scripter.ScriptTriggered += HandleUserScriptExecutedEvent;
                Scripter.RunTimeError += HandleUserScriptRuntimeError;
                Scripter.CompileError += HandleUserScriptCompileError;
                Scripter.AddScriptableObject(ScriptableRRConnection.WrapConn(_RRconnectionsDictionary));
                Scripter.AddScriptableObject(ScriptablePubSubConnection.WrapConn(_PubSubConnectionsDictionary));
                Scripter.AddScriptableObject(ScriptableTag.WrapDPD(_dbManager.GetAllTagDefs()));
                LoadUserScripts();
                Scripter.Enabled = true;


                Globals.SystemManager.LogApplicationEvent(this, "", "FDA initialization complete");

            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "DataAcqManager: General error in Start()");
            }     
        }

        private void CreatePubSubConnectionMgr(FDASourceConnection connectionconfig)
        {
           Globals.SystemManager.LogApplicationEvent(this,"","CreatePubSubConnectionMgr(" + connectionconfig.Description + ")");
            string[] connDetails;
            PubSubConnectionManager newConn = null;

            switch (connectionconfig.SCType.ToUpper())
            {
                case "OPCUA": // SCDetail01 format is  host:port
                    connDetails = connectionconfig.SCDetail01.Split(':');  // separate the hostname from the port
                    newConn = new PubSubConnectionManager(connectionconfig.SCUID, connectionconfig.Description)
                    {
                        MaxSocketConnectionAttempts = connectionconfig.MaxSocketConnectionAttempts,
                        SocketConnectionRetryDelay = connectionconfig.SocketConnectionRetryDelay,
                        PostConnectionCommsDelay = connectionconfig.PostConnectionCommsDelay,
                        CommsLogEnabled = connectionconfig.CommsLogEnabled,
                        MQTTEnabled = false // connectionconfig.MQTTEnabled;
                    };
                    //Globals.SystemManager.LogApplicationEvent(this, "", "Configuring manager as OPCUA " + connDetails[0] + ":" + connDetails[1]);
                    newConn.ConfigureAsOPCUA(
                        connDetails[0],             // host
                        int.Parse(connDetails[1])); // port

                    // subscribe to any configured data points for this connection
                    List<DataSubscription> subs = _dbManager.GetSubscriptions(connectionconfig.SCUID);
                    foreach (DataSubscription sub in subs)
                        newConn.Subscribe(sub);
                    break;
                case "OPCDA": // SCDetail01 format is  host:ProgID:ClassID
                    connDetails = connectionconfig.SCDetail01.Split(':');  // separate the hostname from the port
                    newConn = new PubSubConnectionManager(connectionconfig.SCUID, connectionconfig.Description)
                    {
                        MaxSocketConnectionAttempts = connectionconfig.MaxSocketConnectionAttempts,
                        SocketConnectionRetryDelay = connectionconfig.SocketConnectionRetryDelay,
                        PostConnectionCommsDelay = connectionconfig.PostConnectionCommsDelay,
                        CommsLogEnabled = connectionconfig.CommsLogEnabled,
                        MQTTEnabled = false // connectionconfig.MQTTEnabled;
                    };

                    newConn.ConfigureAsOPCDA(
                        connDetails[0],             // Host
                        connDetails[1],             // ProgID
                        connDetails[2]);            // ClassID

                    // subscribe to any configured data points
                    subs = _dbManager.GetSubscriptions(connectionconfig.SCUID);
                    foreach (DataSubscription sub in subs)
                        newConn.Subscribe(sub);
                    break;
            }

            newConn.ConnDetails = connectionconfig.SCDetail01;
            newConn.CommunicationsEnabled = connectionconfig.CommunicationsEnabled;

            newConn.ConnectionEnabled = connectionconfig.ConnectionEnabled;

            // subscribe to "transaction complete" events from it
            newConn.DataUpdate += TransactionCompleteHandler;

            lock (_PubSubConnectionsDictionary)
            {
                _PubSubConnectionsDictionary.Add(newConn.ConnectionID, newConn);
            }
        }
      
        private void CreateRRConnectionMgr(FDASourceConnection connectionconfig)
        {
            Globals.SystemManager.LogApplicationEvent(this, "", "CreateRRConnectionMgr(" + connectionconfig.Description + ")");

            string[] connDetails = connectionconfig.SCDetail01.Split(':');  // separate the hostname from the port
   
            // create a new connection manager
            RRConnectionManager newConn = null;
            bool success = true;
            try
            {
                switch (connectionconfig.SCType.ToUpper())
                {
                    case "ETHERNET":
                        newConn = new RRConnectionManager(connectionconfig.SCUID, connectionconfig.Description,
                        connDetails[0],              // IP Address 
                        int.Parse(connDetails[1]));   // Port Number

                        break;
                    case "ETHERNETUDP":
                        newConn = new RRConnectionManager(connectionconfig.SCUID, connectionconfig.Description, "", int.Parse(connDetails[0]), "UDP");
                        break;
                    case "SERIAL":
                        string comPort = connDetails[0];
                        Parity parity = (connDetails[3].ToUpper()) switch
                        {
                            "N" => Parity.None,
                            "E" => Parity.Even,
                            "O" => Parity.Odd,
                            "S" => Parity.Space,
                            _ => throw new FormatException("unrecognized serial parity option '" + connDetails[3] + "'. Valid options are N, E, O, or S"),
                        };
                        StopBits stopBits = (connDetails[4].ToUpper()) switch
                        {
                            "N" => StopBits.None,
                            "1" => StopBits.One,
                            "1.5" => StopBits.OnePointFive,
                            "2" => StopBits.Two,
                            _ => throw new FormatException("unrecognized serial stop bits option '" + connDetails[4] + "'. Valid options are N,1,1.5, or 2"),
                        };

                        newConn = new RRConnectionManager(connectionconfig.SCUID, connectionconfig.Description)
                        {
                            SerialPortName = comPort,
                            SerialBaudRate = int.Parse(connDetails[1]),
                            SerialParity = parity,
                            SerialDataBits = int.Parse(connDetails[2]),
                            SerialStopBits = stopBits,
                            SerialHandshake = Handshake.None
                        };              // handshaking (hard coded to "none")
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
                    lock (_RRconnectionsDictionary)
                    {
                        _RRconnectionsDictionary.Add(connectionconfig.SCUID, newConn);
                    }

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

        private void LoadUserScripts()
        {
            Dictionary<string, UserScriptDefinition> modules = _dbManager.GetUserScripts();

            //get a the list of scripts and sort them by the load_order
            List<UserScriptDefinition> sortedScriptDef = new(modules.Values);
            sortedScriptDef.Sort();

            foreach (UserScriptDefinition scriptDef in sortedScriptDef)
            {
                // skip any disabled modules
                //if (!module.enabled)
                //   continue;

                 Globals.SystemManager.LogApplicationEvent(this, "", "Loading user script '" + scriptDef.script_name + "'");
                try
                {
                    Scripter.LoadScript(scriptDef.script_name, scriptDef.script, scriptDef.enabled, scriptDef.run_spec);
                    
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "Scripter", ex.Message);
                }
            }
        }

        private void ForceScheduleExecutionHandler(object sender, DBManager.ForceScheduleEventArgs e)
        {
            if (_schedulersDictionary.ContainsKey(e.ScheduleID))
            {
                _schedulersDictionary[e.ScheduleID].ForceExecute();
            }
        }

        public static void PublishUpdatedConnectionsList()
        {
            StringBuilder sb = new();
            ushort[] Qcounts;
            if (_RRconnectionsDictionary != null)
            {
                foreach (KeyValuePair<Guid, RRConnectionManager> kvp in DataAcqManager._RRconnectionsDictionary)
                {
                    sb.Append(kvp.Value.ConnectionStatus.ToString());
                    sb.Append('.');
                    sb.Append(kvp.Value.Description);
                    sb.Append('.');
                    sb.Append(kvp.Key.ToString());
                    sb.Append('.');
                    sb.Append(kvp.Value.ConnectionEnabled ? 1 : 0);
                    sb.Append('.');
                    sb.Append(kvp.Value.CommunicationsEnabled ? 1 : 0);
                    sb.Append('.');
                    Qcounts = kvp.Value._queueManager.GetQueueCounts();
                    foreach (ushort count in Qcounts)
                    {
                        sb.Append(count);
                        sb.Append('.');
                    }
                    //remove the last .
                    if (sb.Length > 0)
                        sb.Remove(sb.Length - 1, 1);
                    sb.Append('|');
                }
            }
            //remove the last |
            if (sb.Length > 0)
                sb.Remove(sb.Length - 1, 1);

            Globals.MQTT.Publish("FDA/connectionlist", Encoding.UTF8.GetBytes(sb.ToString()), 0, true);
        }

        private void TransactionCompleteHandler(object sender, TransactionEventArgs e)
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


                    // update the tag definition object
                    if (tag.Quality == 192)
                    {
                        lock (tagDef)
                        {
                            tagDef.PreviousTimestamp = tagDef.LastRead.Timestamp;

                            tagDef.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                                 Convert.ToDouble(tag.Value),
                                 tag.Quality,
                                 tag.Timestamp,
                                 e.RequestRef.Destination,
                                 tag.ProtocolDataType,
                                 e.RequestRef.DBWriteMode
                                );

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
            // log the value(s) in the database, if a destination was specified
            if (e.Request.Destination != "")
            {
                _dbManager.WriteDataToDB(e.Request);
            }

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

        private void HandleSchedulerChanges(ConfigEventArgs e)
        {
                FDARequestGroupScheduler SchedulerConfig = _dbManager.GetSched((Guid)e.Item);
                switch (e.ChangeType)
                {
                    case "UPDATE":
                        // find the existing schedule
                        FDAScheduler oldScheduler = null;
                        if (_schedulersDictionary.ContainsKey((Guid)e.Item))
                        {
                            oldScheduler = _schedulersDictionary[(Guid)e.Item];
                        }
                        else
                        {
                            Globals.SystemManager.LogApplicationEvent(this, "", "Schedule ID " + e.Item + " was updated in the database, but was not found in the FDA. Creating a new schedule with this ID");
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
                                newScheduler = new FDASchedulerRealtime(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, SchedulerConfig.RealTimeRate, delayTime);
                                break;
                            case "DAILY": newScheduler = new FDASchedulerDaily(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, new DateTime(SchedulerConfig.Year, SchedulerConfig.Month, SchedulerConfig.Day, SchedulerConfig.Hour, SchedulerConfig.Minute, SchedulerConfig.Second)); break;
                            case "HOURLY": newScheduler = new FDASchedulerHourly(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, new DateTime(SchedulerConfig.Year, SchedulerConfig.Month, SchedulerConfig.Day, SchedulerConfig.Hour, SchedulerConfig.Minute, SchedulerConfig.Second)); break;
                            case "MONTHLY": newScheduler = new FDASchedulerMonthly(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, new DateTime(SchedulerConfig.Year, SchedulerConfig.Month, SchedulerConfig.Day, SchedulerConfig.Hour, SchedulerConfig.Minute, SchedulerConfig.Second)); break;
                            case "ONSTARTUP": newScheduler = new FDASchedulerOneshot(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, true); break;
                            default:
                                Globals.SystemManager.LogApplicationEvent(this, "", "Schedule ID " + e.Item + " has unrecognized schedule type '" + SchedulerConfig.FRGSType.ToString() + "'");
                                break;
                        }

                        if (newScheduler == null)
                            return;



                        // delete the existing (if present)
                        if (oldScheduler != null)
                        {
                            oldScheduler.Enabled = false;
                            oldScheduler.TimerElapsed -= ScheduleTickHandler;
                            if (_schedulersDictionary.ContainsKey((Guid)e.Item))
                            {
                                lock (_schedulersDictionary)
                                {

                                    _schedulersDictionary.Remove((Guid)e.Item);
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
                        _schedulersDictionary[(Guid)e.Item].Enabled = false;
                        _schedulersDictionary[(Guid)e.Item].TimerElapsed -= ScheduleTickHandler;
                        lock (_schedulersDictionary) { _schedulersDictionary.Remove((Guid)e.Item); }
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
                                case "ONSTARTUP": _schedulersDictionary.Add(SchedulerConfig.FRGSUID, new FDASchedulerOneshot(SchedulerConfig.FRGSUID, SchedulerConfig.Description, SchedulerConfig.RequestGroups, SchedulerConfig.Tasks, true)); break;
                                default:
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Schedule ID " + e.Item + " has unrecognized schedule type '" + SchedulerConfig.FRGSType.ToString() + "'");
                                    break;
                            }
                        }
                        _schedulersDictionary[SchedulerConfig.FRGSUID].TimerElapsed += ScheduleTickHandler;
                        break;
                } 
        }


        private void HandleConnectionChanges(ConfigEventArgs e)
        {
            string changeType = e.ChangeType.ToUpper();

            if (changeType == "UPDATE")
            {
                bool found = false;
                if (_RRconnectionsDictionary != null)
                {
                    if (_RRconnectionsDictionary.ContainsKey((Guid)e.Item))
                    {
                        found = true;
                        // get the connection object to be modified
                        RRConnectionManager conn = _RRconnectionsDictionary[(Guid)e.Item];

                        // get the updated connection configuration
                        FDASourceConnection updatedConfig = _dbManager.GetConnectionConfig((Guid)e.Item);

                        UpdateRRConnection(conn, updatedConfig);
                        
                    }
                }

                if (_PubSubConnectionsDictionary != null)
                {
                    if (_PubSubConnectionsDictionary.ContainsKey((Guid)e.Item))
                    {
                        found = true;
                        // get the connection object to be modified
                        PubSubConnectionManager conn = _PubSubConnectionsDictionary[(Guid)e.Item];

                        // get the updated connection configuration
                        FDASourceConnection updatedConfig = _dbManager.GetConnectionConfig((Guid)e.Item);

                        UpdatePubSubConnection(conn, updatedConfig);
                    }
                }


                 if (!found)
                    changeType = "INSERT"; // this was an update but no connection object with this ID was found, change it to an insert instead (will be processed as an insert below)
            }

            if (changeType == "DELETE")
            {
                // Delete from RRConnections
                if (_RRconnectionsDictionary != null)
                {
                    if (_RRconnectionsDictionary.ContainsKey((Guid)e.Item))
                    {
                        DeleteRRConnection((Guid)e.Item);
                    }
                }

                // Delete from PubSub Connections
                if (_PubSubConnectionsDictionary != null)
                {
                    if (_PubSubConnectionsDictionary.ContainsKey((Guid)e.Item))
                    {
                        DeletePubSubConnection((Guid)e.Item);
                    }
                }
               

                // connection deletion may cause previously valid request groups to become invalid, mark all valid groups for re-validation
                RevalidateRequestGroups(1);
            }

            if (changeType == "INSERT")
            {
                FDASourceConnection connConfig = _dbManager.GetConnectionConfig((Guid)e.Item);
                string connType = connConfig.SCType.ToUpper();

                if (connType == "ETHERNET" || connType == "ETHERNETUDP" || connType == "SERIAL")
                    CreateRRConnectionMgr(connConfig);

                if (connType == "OPCDA" || connType == "OPCUA" || connType == "MQTT")
                    CreatePubSubConnectionMgr(connConfig);

                // this stuff all covered in CreateRRConnectionManager() now
                //-------------------------------------------------------------------
                //// separate the connection details
                //string[] connDetails = connConfig.SCDetail01.Split(':');

                //// create the new connection
                //RRConnectionManager newConn = null;
                //bool success = true;
                //try
                //{
                //    newConn = new RRConnectionManager(connConfig.SCUID, connConfig.Description, connDetails[0], int.Parse(connDetails[1]))
                //    {
                //        RequestRetryDelay = (short)connConfig.RequestRetryDelay,
                //        SocketConnectionAttemptTimeout = connConfig.SocketConnectionAttemptTimeout,
                //        MaxSocketConnectionAttempts = connConfig.MaxSocketConnectionAttempts,
                //        SocketConnectionRetryDelay = connConfig.SocketConnectionRetryDelay,
                //        PostConnectionCommsDelay = connConfig.PostConnectionCommsDelay,
                //        InterRequestDelay = connConfig.InterRequestDelay,
                //        MaxRequestAttempts = connConfig.MaxRequestAttempts,
                //        RequestResponseTimeout = connConfig.RequestResponseTimeout,
                //        CommsLogEnabled = connConfig.CommsLogEnabled
                //    };
                //}
                //catch (Exception ex)
                //{
                //    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while creating connection object " + connConfig.SCUID);
                //    success = false;
                //}

                //if (success)
                //{
                //    try
                //    {
                //        newConn.ConnectionType = (RRConnectionManager.ConnType)Enum.Parse(typeof(RRConnectionManager.ConnType), connConfig.SCType);
                //        newConn.TransactionComplete += TransactionCompleteHandler;
                //        // add it to our dictionary
                //        _RRconnectionsDictionary.Add((Guid)e.ID, newConn);

                //        // enable it (if configured to be enabled)
                //        newConn.CommunicationsEnabled = connConfig.CommunicationsEnabled;
                //        newConn.ConnectionEnabled = connConfig.ConnectionEnabled;

                //        //PublishUpdatedConnectionsList();
                //    }
                //    catch (Exception ex)
                //    {
                //        Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "unrecognized connection type '" + connConfig.SCType.ToString() + "' in connection " + connConfig.SCUID.ToString());
                //        newConn.ConnectionEnabled = false;
                //        newConn.Dispose();
                //        newConn = null;
                //    }
                //}

                // adding a new connection may cause previously invalid groups to become valid, mark all invalid groups for revalidation
                RevalidateRequestGroups(2);
            }

            if (Globals.MQTTEnabled)
            {
                PublishUpdatedConnectionsList();
            }
            
        }


        private void HandleRequestGroupChanges(ConfigEventArgs e)
        {
            // handle request group config changes 
                if (e.ChangeType == "UPDATE")
                {
                    // get the updated group config from the database
                    FDADataBlockRequestGroup updatedGroupConfig = _dbManager.GetRequestGroup((Guid)e.Item);

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
                        RequestGroup deletedGroup = scheduler.RequestGroupList.Find(group => group.ID == (Guid)e.Item);
                        while (deletedGroup != null)
                        {
                            scheduler.RequestGroupList.Remove(deletedGroup);
                            deletedGroup = scheduler.RequestGroupList.Find(group => group.ID == (Guid)e.Item);
                        }
                    }
                }

                if (e.ChangeType == "Insert")
                {
                    // check if the new group is referenced by any scheduler configs
                    List<FDARequestGroupScheduler> schedConfigList = _dbManager.GetAllSched();
                    FDADataBlockRequestGroup groupConfig = _dbManager.GetRequestGroup((Guid)e.Item);
                    int idx = -1;
                    int separatorIdx;
                    int length;
                    string requestConfig;

                    foreach (FDARequestGroupScheduler sched in schedConfigList)
                    {

                        idx = sched.RequestGroupList.IndexOf(e.Item.ToString().ToUpper());
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
    
        private void HandleFDATasksChanges(ConfigEventArgs e)
        {
            if (e.ChangeType == "UPDATE")
            {
                // get the updated task definition from the database manager
                FDATask updatedTask = _dbManager.GetTask((Guid)e.Item);

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
                    FDATask deletedTask = scheduler.TasksList.Find(task => task.TASK_ID == (Guid)e.Item);
                    while (deletedTask != null)
                    {
                        scheduler.TasksList.Remove(deletedTask);
                        deletedTask = scheduler.TasksList.Find(task => task.TASK_ID == (Guid)e.Item);
                    }
                }
            }
        }

        private void HandleDataPointDefinitionChanges(ConfigEventArgs e)
        {

            // mark request groups for revalidation
            if (e.ChangeType == "INSERT")
                RevalidateRequestGroups(2);

            if (e.ChangeType == "DELETE")
                RevalidateRequestGroups(1);

        }

        private void HandleScriptChanges(ConfigEventArgs e)
        {
            UserScriptDefinition scriptdef = _dbManager.GetUserScript((string)e.Item);
            if (e.ChangeType == "INSERT")
            {
                if (Scripter.GetScript(e.Item.ToString()) == null) // check if a script with this ID already exists
                {
                    Scripter.LoadScript(scriptdef.script_name, scriptdef.script, scriptdef.enabled, scriptdef.run_spec);
                }

            }

            if (e.ChangeType == "UPDATE")
            {
                UserScript scriptToUpdate = Scripter.GetScript(e.Item.ToString());

                if (scriptToUpdate != null)
                {
                    scriptToUpdate.Code = scriptdef.script;
                    scriptToUpdate.RunSpec = scriptdef.run_spec;
                    scriptToUpdate.Enabled = scriptdef.enabled;
                }
            }

            if (e.ChangeType == "DELETE")
            {
                Scripter.UnloadScript(e.Item.ToString());
            }
          
            
        }
    
        private void ConfigChangeHandler(object sender, ConfigEventArgs e)
        {
            try
            {
                if (e.TableName == Globals.SystemManager.GetTableName("FDARequestGroupScheduler"))
                {
                    HandleSchedulerChanges(e);
                    return;
                }

                if (e.TableName == Globals.SystemManager.GetTableName("FDASourceConnections"))
                {                  
                    HandleConnectionChanges(e);  
                    return;
                }

                if (e.TableName == Globals.SystemManager.GetTableName("FDADataBlockRequestGroup"))
                {
                    HandleRequestGroupChanges(e);
                    return;
                }
                    
                if (e.TableName == Globals.SystemManager.GetTableName("FDATasks"))
                {
                    HandleFDATasksChanges(e);
                    return;
                }

                if (e.TableName == Globals.SystemManager.GetTableName("DataPointDefinitionStructures"))
                {
                    HandleDataPointDefinitionChanges(e);
                    return;
                }

                if (e.TableName == Globals.SystemManager.GetTableName("fda_scripts"))
                {
                    HandleScriptChanges(e);
                    return;
                }

                if (e.TableName == "FDAConfig")
                {
                    HandleAppConfigChanges();
                    return;
                }

                if (e.TableName == "FDASubscriptions")
                {
                    HandleSubscriptionChanges(e);
                    return;
                }
                
            } catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while handling a database event (insert,delete, or update) in table " + e.TableName + ", item ID " + e.Item);
            }
        }      

        public static void HandleSubscriptionChanges(ConfigEventArgs e)
        {
            DataSubscription sub = (DataSubscription)e.Item;
            DataSubscription oldSub = (DataSubscription)e.OldItem;

            if (e.ChangeType == "UPDATE")
            {
                // did the connection reference change? will need to unsubscribe from the old connection and subscribe with the new connection
                if (sub.source_connection_ref != oldSub.source_connection_ref)
                {
                    if (_PubSubConnectionsDictionary.ContainsKey(oldSub.source_connection_ref))
                        _PubSubConnectionsDictionary[oldSub.source_connection_ref].UnSubscribe(oldSub);

                    if (_PubSubConnectionsDictionary.ContainsKey(sub.source_connection_ref))
                        _PubSubConnectionsDictionary[sub.source_connection_ref].Subscribe(sub);
                }
                else
                {   // not a connection change, just update the existing subscription
                    if (_PubSubConnectionsDictionary.ContainsKey(sub.source_connection_ref))
                    {
                        _PubSubConnectionsDictionary[sub.source_connection_ref].UpdateSubscription(sub);
                    }
                }
            }

            if (e.ChangeType == "INSERT")
            {
                if (_PubSubConnectionsDictionary.ContainsKey(sub.source_connection_ref))
                {
                    _PubSubConnectionsDictionary[sub.source_connection_ref].Subscribe(sub);
                }
            }

            if (e.ChangeType == "DELETE")
            {
                if (_PubSubConnectionsDictionary.ContainsKey(sub.source_connection_ref))
                {
                    _PubSubConnectionsDictionary[sub.source_connection_ref].UnSubscribe(sub);
                }
            }

        }

        private void HandleAppConfigChanges()
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

            if (configs.ContainsKey("MQTTEnabled"))
            {
                bool mqttEnb = false;
                try
                {
                  mqttEnb = (int.Parse(configs["MQTTEnabled"].OptionValue) == 1);
                }  catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while parsing the value of the MQTTEnabled option in the FDAConfig table. Using default value of 0");
                }

                // handle this at the program level
                MQTTEnableStatusChanged?.Invoke(this, new BoolEventArgs(mqttEnb));
            }

        }


        private void DeletePubSubConnection(Guid ID)
        {
            PubSubConnectionManager connToDelete = _PubSubConnectionsDictionary[ID];
            connToDelete.DataUpdate -= TransactionCompleteHandler;
            // remove the doomed connection object from the dictionary
            _PubSubConnectionsDictionary.Remove(ID);

            // disable it, and dispose of it
            connToDelete.CommunicationsEnabled = false;
            connToDelete.ConnectionEnabled = false;
            connToDelete.Dispose();
            connToDelete = null;
        }

        private void DeleteRRConnection(Guid ID)
        {
            RRConnectionManager connToDelete = _RRconnectionsDictionary[ID];
            connToDelete.TransactionComplete -= TransactionCompleteHandler;

            // remove the doomed connection object from the dictionary
            _RRconnectionsDictionary.Remove(ID);

            // disable it, and dispose of it
            connToDelete.CommunicationsEnabled = false;
            connToDelete.ConnectionEnabled = false;
            connToDelete.Dispose();
            connToDelete = null;

            //PublishUpdatedConnectionsList();
        }
        private void UpdatePubSubConnection(PubSubConnectionManager conn, FDASourceConnection updatedConfig)
        {
            // ---------------------------------- Handle SCType changes ----------------------------------
            bool isValidConnType;
            if (conn.ConnectionType.ToString().ToUpper() != updatedConfig.SCType.ToUpper())
            {
                isValidConnType = Enum.TryParse(typeof(PubSubConnectionManager.ConnType), updatedConfig.SCType, out object connType);

                if (isValidConnType)
                {
                    conn.ConnectionType = (PubSubConnectionManager.ConnType)connType;
                }
                else
                {
                    // check if its a valid conn type for an RR connection
                    isValidConnType = Enum.TryParse(typeof(RRConnectionManager.ConnType), updatedConfig.SCType, out _);

                    if (isValidConnType)
                    {
                        // we've switched from a PubSub connection to a RR connection

                        // so delete the PubSub connection
                        DeletePubSubConnection(conn.ConnectionID);

                        // and create a new RRConnection
                        CreateRRConnectionMgr(updatedConfig);
                        return;
                    }
                    else
                        Globals.SystemManager.LogApplicationEvent(this, "", "Invalid SCType value '" + updatedConfig.SCType.ToString() + "' for connection '" + updatedConfig.Description + "'(" + updatedConfig.SCUID + ")");

                }
            }

            //------------------------------ Handle connection details changes (ip/port/ProgID/ClassID) ---------------------------------------
            if (conn.ConnDetails != updatedConfig.SCDetail01)
            {
                conn.ConnDetails = updatedConfig.SCDetail01;
                string[] connParams = updatedConfig.SCDetail01.Split(':');

                switch (conn.ConnectionType)
                {
                    case PubSubConnectionManager.ConnType.OPCUA:
                        if (conn.Host != connParams[0]) conn.Host = connParams[0];

                        int newPort;
                        newPort = int.Parse(connParams[1]);
                        if (conn.Port != newPort) conn.Port = newPort;

                        conn.ResetConnection();
                        break;
                    case PubSubConnectionManager.ConnType.OPCDA:
                        if (conn.Host != connParams[0]) conn.Host = connParams[0];
                        if (conn.ProgID != connParams[1]) conn.ProgID = connParams[1];
                        if (conn.ClassID != connParams[1]) conn.ClassID = connParams[1];

                        conn.ResetConnection();
                        break;

                }
            }

            //--------------------------------- Handle all other changes -----------------------------
            if (conn.Description != updatedConfig.Description) conn.Description = updatedConfig.Description;
            if (conn.MaxSocketConnectionAttempts != updatedConfig.MaxSocketConnectionAttempts) conn.MaxSocketConnectionAttempts = updatedConfig.MaxSocketConnectionAttempts;
            if (conn.PostConnectionCommsDelay != updatedConfig.PostConnectionCommsDelay) conn.PostConnectionCommsDelay = updatedConfig.PostConnectionCommsDelay;
            if (conn.SocketConnectionRetryDelay != updatedConfig.SocketConnectionRetryDelay) conn.SocketConnectionRetryDelay = updatedConfig.SocketConnectionRetryDelay;
            if (conn.CommunicationsEnabled != updatedConfig.CommunicationsEnabled) conn.CommunicationsEnabled = updatedConfig.CommunicationsEnabled;
            if (conn.ConnectionEnabled != updatedConfig.ConnectionEnabled) conn.ConnectionEnabled = updatedConfig.ConnectionEnabled;
            if (conn.CommsLogEnabled != updatedConfig.CommsLogEnabled) conn.CommsLogEnabled = updatedConfig.CommsLogEnabled;
        }
        private void UpdateRRConnection(RRConnectionManager conn,FDASourceConnection updatedConfig)
        {
            // SCType has changed
            bool isValidConnType;
            if (conn.ConnectionType.ToString().ToUpper() != updatedConfig.SCType.ToUpper())
            { 
                isValidConnType = Enum.TryParse(typeof(RRConnectionManager.ConnType), updatedConfig.SCType, out object connType);

                if (isValidConnType)
                {
                    conn.ConnectionType = (RRConnectionManager.ConnType)connType;
                }
                else
                {
                    // check if its a valid conn type for a PubSub connection
                    
                    isValidConnType = Enum.TryParse(typeof(PubSubConnectionManager.ConnType), updatedConfig.SCType, out _);

                    if (isValidConnType)
                    {
                        // we've switched from a RR connection to a PubSub connection

                        // so delete the RR connection
                        DeleteRRConnection(conn.ConnectionID);

                        // and create a new PubSubConnection
                        CreatePubSubConnectionMgr(updatedConfig);
                        return;
                    }
                    else
                        Globals.SystemManager.LogApplicationEvent(this, "", "Invalid SCType value '" + updatedConfig.SCType.ToString() + "' for connection '" + updatedConfig.Description + "'(" + updatedConfig.SCUID + ")");
                }
            }

            conn.ConnDetails = updatedConfig.SCDetail01;
            string[] connParams = updatedConfig.SCDetail01.Split(':');


            switch (conn.ConnectionType)
            {
                // Ethernet specific configuration
                case RRConnectionManager.ConnType.Ethernet:
                    if (conn.RemoteIPAddress != connParams[0]) conn.RemoteIPAddress = connParams[0];
                    int newPort;
                    newPort = int.Parse(connParams[1]);
                    if (conn.PortNumber != newPort) conn.PortNumber = newPort;
                    break;
                case RRConnectionManager.ConnType.EthernetUDP:
                    newPort = int.Parse(connParams[0]);
                    if (conn.PortNumber != newPort) conn.PortNumber = newPort;
                    break;
                case RRConnectionManager.ConnType.Serial:
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


                    if (conn.SerialPortName != connParams[0]) conn.SerialPortName = "COM" + connParams[0];
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

        private void DemandRequestHandler(object sender, DBManagerPG.DemandEventArgs e)
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

            FDAScheduler scheduler = (FDAScheduler)sender;

            Globals.SystemManager.LogApplicationEvent(this, "", "Schedule '" + scheduler.Description + "' triggered");

            //validate requested group(s)
            List<RequestGroup> validGroups = ValidateRequestedGroups(scheduler.RequestGroupList, "Schedule " + scheduler.ID);

            // if there are still any groups in the list after validation, continue
            if (validGroups.Count > 0)
            {
                PrepareAndSendRequestGroups(validGroups, Globals.RequesterType.Schedule, scheduler.ID);
            }

            // if there are any tasks in the taskList, do those too
            if (scheduler.TasksList.Count > 0)
            {
                foreach (FDATask task in scheduler.TasksList)
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
                            string error;
                            List<Object> commsStatsParams = _dbManager.ParseStatsCalcParams(task.task_details,out error);
                            if (error == "" && commsStatsParams != null)
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "", "Calculating communications statistics (requested by schedule " + scheduler.ID);
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


            // if the schedule is of the OneShot type, dispose of it
            // change of plans, don't dispose of it because we might get a demand for a 'force execute' to run it again later
            /*
            if (scheduler.ScheduleType == ScheduleType.OneShot)
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "Disposing of Schedule '" + scheduler.Description +"'");
                if (_schedulersDictionary.ContainsKey(scheduler.ID))
                {
                    _schedulersDictionary.Remove(scheduler.ID);
                    scheduler.Dispose();
                    scheduler = null;
                }
            }
            */
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
            List<RequestGroup> goodGroups = new(groupstoValidate);
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
                if ( !(_RRconnectionsDictionary.ContainsKey(group.ConnectionID) || _PubSubConnectionsDictionary.ContainsKey(group.ConnectionID)))
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

                // non-null DataPointBlockRequestListVals
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
                                case "OPC": goodRequestString = OPC.OPCProtocol.ValidateRequestString(request,_dbManager.GetAllTagDefs(),group.ID.ToString(),requestor); break;
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
                    case "OPC": break; // TODO: writes for OPC
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

            if (_RRconnectionsDictionary.ContainsKey(group.ConnectionID))
            {
                if (group.RequesterType == Globals.RequesterType.System)
                {
                    _RRconnectionsDictionary[group.ConnectionID].QueueTransactionGroup(group);
                }
                else
                {
                    FDADataBlockRequestGroup groupConfig = _dbManager.GetRequestGroup(group.ID);
                    if (groupConfig != null)
                    {
                        if (groupConfig.DRGEnabled)
                            _RRconnectionsDictionary[group.ConnectionID].QueueTransactionGroup(group);
                    }
                }
            }

            if (_PubSubConnectionsDictionary.ContainsKey(group.ConnectionID))
            {
                if (group.RequesterType == Globals.RequesterType.System)
                {
                    _PubSubConnectionsDictionary[group.ConnectionID].QueueTransactionGroup(group);
                }
                else
                {
                    FDADataBlockRequestGroup groupConfig = _dbManager.GetRequestGroup(group.ID);
                    if (groupConfig != null)
                    {
                        if (groupConfig.DRGEnabled)
                            _PubSubConnectionsDictionary[group.ConnectionID].QueueTransactionGroup(group);
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

            Dictionary<Guid, double> updated = new();

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

        public static void AddDataConnection(RRConnectionManager connection)
        {
            _RRconnectionsDictionary.Add(connection.ConnectionID, connection);
        }

        public static RRConnectionManager GetDataConnection(Guid connectionID)
        {
            if (_RRconnectionsDictionary.ContainsKey(connectionID))
                return _RRconnectionsDictionary[connectionID];
            else
                return null;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            // stop and unload all user scripts
            Globals.SystemManager.LogApplicationEvent(this, "", "Unloading user scripts");
            Scripter.Cleanup();

            // dispose schedulers and clear schedulers dictionary
            if (_schedulersDictionary != null)
            {
                foreach (FDAScheduler sched in _schedulersDictionary.Values)
                {
                      sched.Dispose();
                }
                _schedulersDictionary.Clear();
            }

            // dispose connections and then clear the connections dictionary
            List<Task> disposalTasks = new();
            if (_RRconnectionsDictionary != null)
            {
                foreach (RRConnectionManager conn in _RRconnectionsDictionary.Values)
                {
                    // dispose of the connections in parallel to reduce shutdown time
                    Task task = Task.Factory.StartNew(conn.Dispose);
                }

                // wait for all connection disposal tasks to complete
                Task.WaitAll(disposalTasks.ToArray());
                
                // clear the connections dictionary
                _RRconnectionsDictionary.Clear();
            }

            _dbManager?.Dispose();
        }
    }
}
