using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Common;
using DynamicCode;

namespace FDA
{
    public abstract class DBManager : IDisposable
    {
        protected Dictionary<Guid, FDARequestGroupScheduler> _schedConfig;
        protected Dictionary<Guid, FDADataBlockRequestGroup> _requestgroupConfig;
        protected Dictionary<Guid, FDADataPointDefinitionStructure> _dataPointConfig;
        protected Dictionary<Guid, FDASourceConnection> _connectionsConfig;
        protected Dictionary<Guid, FDADevice> _deviceConfig;
        protected Dictionary<Guid, FDATask> _taskConfig;

        protected Queue<DataRequest> _writeQueue;
        protected BackgroundWorker _dataWriter;

        private Queue<List<AlarmEventRecord>> _alarmEventQueue;
        private BackgroundWorker _alarmsEventsWriter;

        private readonly CacheManager _cacheManager;
        protected DateTime _PreviousDBStartTime = DateTime.MinValue;

        private readonly Timer _logTrimTimer;

        public delegate void ConfigChangeHandler(object sender, ConfigEventArgs e);
        public event ConfigChangeHandler ConfigChange;

        public delegate void DemandRequestHandler(object sender, DemandEventArgs e);
        public event DemandRequestHandler DemandRequest;

        public delegate void ForceScheduleHandler(object sender, ForceScheduleEventArgs e);
        public event ForceScheduleHandler ForceScheduleExecution;

        protected bool _DBStatus = false;
        protected bool DBStatus 
              { get { return _DBStatus; } 
                set { _DBStatus = value;
                      if (Globals.MQTTEnabled && Globals.MQTT != null)
                      {
                        if (Globals.MQTT.IsConnected)
                        {
                            Globals.MQTT.Publish("FDA/DBConnStatus",Encoding.UTF8.GetBytes(value.ToString()),1,true);   
                        }
                      }
                    }
               }

        protected bool _devicesTableExists = false;
        protected bool _tasksTableExists;
        protected bool _scriptsTableExists = false;


        protected Timer _keepAliveTimer;
        protected TimeSpan _DBKeepAliveRate = new TimeSpan(0, 0, 3);
        protected Stopwatch _DBDownTimer = new Stopwatch();
        protected readonly TimeSpan _databaseDownNotificationLimit = new TimeSpan(0, 30, 0); // in days (default to 30 minutes)

        public string ConnectionString { get; set; }

        public RemoteQueryManager RemoteQueryManager;

        public DBManager(string connString)
        {
            ConnectionString = connString;

            // structures to hold the config info in memory
            _schedConfig = new Dictionary<Guid, FDARequestGroupScheduler>();
            _requestgroupConfig = new Dictionary<Guid, FDADataBlockRequestGroup>();
            _dataPointConfig = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            _connectionsConfig = new Dictionary<Guid, FDASourceConnection>();
            _deviceConfig = new Dictionary<Guid, FDADevice>();
            _taskConfig = new Dictionary<Guid, FDATask>();
            //_derivedTagConfig = new Dictionary<Guid, DerivedTag>();


            _dataWriter = new BackgroundWorker();
            _dataWriter.DoWork += DataWriter_DoWork;

            // default write batch settings
            int batchLimit = 500;
            int batchTimeout = 500;

            // load custom batch write settings (if specified)
            if (Globals.SystemManager.GetAppConfig().ContainsKey("BatchInsertMaxRecords"))
                int.TryParse(Globals.SystemManager.GetAppConfig()["BatchInsertMaxRecords"].OptionValue, out batchLimit);

            if (Globals.SystemManager.GetAppConfig().ContainsKey("BatchInsertMaxTime"))
                int.TryParse(Globals.SystemManager.GetAppConfig()["BatchInsertMaxTime"].OptionValue, out batchTimeout);

            // cap write batches at 500 records
            if (batchLimit > 500)
                batchLimit = 500;

            _cacheManager = new CacheManager(batchLimit, batchTimeout);
            _cacheManager.CacheFlush += CacheMananger_CacheFlush;

            // start the log trimming timer

            //find the amount of time between now and midnight and set the timer to execute then, and every 24 hours from then
            DateTime currentDateTime = DateTime.Now;
            DateTime tomorrow = DateTime.Now.AddDays(1);
            DateTime timeOfFirstRun = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 0, 0, 0);
            TimeSpan timeFromNowToFirstRun = timeOfFirstRun.Subtract(currentDateTime);


            _logTrimTimer = new Timer(LogTrimTimerTick, null, timeFromNowToFirstRun, TimeSpan.FromDays(1));

            // check if device table exists
            _devicesTableExists = ((int)ExecuteScalar("SELECT cast(count(1) as integer) from information_schema.tables where table_name = '" + Globals.SystemManager.GetTableName("fdadevices") + "';") > 0);

            // check if tasks table exists
            _tasksTableExists = ((int)ExecuteScalar("SELECT cast(count(1) as integer) from information_schema.tables where table_name = '" + Globals.SystemManager.GetTableName("fdatasks") + "';") > 0);

            // check if scripts table exists
            _scriptsTableExists = ((int)ExecuteScalar("SELECT cast(count(1) as integer) from information_schema.tables where table_name = '" + Globals.SystemManager.GetTableName("fda_scripts") + "';") > 0);

 
            _writeQueue = new Queue<DataRequest>();

            _keepAliveTimer = new Timer(DBCheckTimerTick, this, Timeout.Infinite, Timeout.Infinite);
        }

        public virtual void Initialize()
        {
            Globals.SystemManager.LogApplicationEvent(this, "", "Initializing database manager");

            // start the remote query manager (handles queries from FDAManagers)
            StartRemoteQueryManager();


            // get the last DB start time
            _PreviousDBStartTime = GetDBStartTime();

            _keepAliveTimer.Change(_DBKeepAliveRate, _DBKeepAliveRate);
        }

        public void StartRemoteQueryManager()
        {
            if (Globals.MQTTEnabled)
            {
                if (RemoteQueryManager != null)
                {
                    StartRemoteQueryManager();
                }

                RemoteQueryManager = new RemoteQueryManager(this.GetType().Name, ConnectionString);
            }
        }

        public void StopRemoteQueryManager()
        {
            if (RemoteQueryManager != null)
            {
                RemoteQueryManager.Dispose();
            }
            RemoteQueryManager = null;
        }


        #region abstract functions


        protected abstract int ExecuteNonQuery(string sql);
        protected abstract object ExecuteScalar(string sql);

        protected abstract DataTable ExecuteQuery(string sql);

        protected abstract DateTime GetDBStartTime();

        public abstract void StartChangeMonitoring();

        public abstract void PauseChangeMonitoring();

        
        protected abstract bool TestConnection();

        protected abstract bool BuildAppLogTable(string tableName);

        protected abstract bool BuildCommsLogTable(string tableName);


        #endregion

        #region configuration access functions

        public FDADevice GetDevice(Guid ID)
        {
            if (_deviceConfig.ContainsKey(ID))
                return _deviceConfig[ID];
            else
                return null;
        }

        public Dictionary<Guid, FDADevice> GetAllDevices()
        {
            return _deviceConfig;
        }

        public FDATask GetTask(Guid ID)
        {
            if (_taskConfig.ContainsKey(ID))
                return _taskConfig[ID];
            else
                return null;
        }

        public Dictionary<Guid, FDATask> GetAllTasks()
        {
            return _taskConfig;
        }

        public FDADataPointDefinitionStructure GetTagDef(Guid ID)
        {

            if (_dataPointConfig.ContainsKey(ID))
                return _dataPointConfig[ID];
            else
                return null;
        }

        public Dictionary<Guid, FDADataPointDefinitionStructure> GetAllTagDefs()
        {
            return _dataPointConfig;
        }

        public List<FDARequestGroupScheduler> GetAllSched()
        {
            return _schedConfig.Values.ToList();
        }

        public FDARequestGroupScheduler GetSched(Guid ID)
        {
            if (_schedConfig.ContainsKey(ID))
                return _schedConfig[ID];
            else
                return null;
        }



        public FDASourceConnection GetConnectionConfig(Guid ID)
        {
            if (_connectionsConfig.ContainsKey(ID))
                return _connectionsConfig[ID];
            else
                return null;
        }

        public List<FDASourceConnection> GetAllConnectionconfigs()
        {
            return _connectionsConfig.Values.ToList();
        }

        public FDADataBlockRequestGroup GetRequestGroup(Guid ID)
        {
            if (_requestgroupConfig.ContainsKey(ID))
                return _requestgroupConfig[ID];
            else
                return null;
        }

        public void UpdateCacheSize(int cacheLimit)
        {
            if (cacheLimit > 500)
                cacheLimit = 500;
            _cacheManager.CacheLimit = cacheLimit;
        }

        public void UpdateCacheTimeout(int cacheTimeout)
        {
            _cacheManager.CacheTimeout = cacheTimeout;
        }

        protected void DeleteDemand(Guid DemandID)
        {
            string sql = "delete from " + Globals.SystemManager.GetTableName("FDARequestGroupDemand") + " where FRGDUID  = '" + DemandID.ToString() + "';";
            ExecuteNonQuery(sql);
            return;
        }

        protected void DeleteRequestGroup(List<RequestGroup> groupsToDelete)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("delete from ");
            sb.Append(Globals.SystemManager.GetTableName("FDADataBlockRequestGroup"));
            sb.Append(" where DRGUID in (");

            if (groupsToDelete.Count == 0)
                return;


            foreach (RequestGroup group in groupsToDelete)
            {
                sb.Append("'");
                sb.Append(group.ID);
                sb.Append("',");
            }
            sb.Remove(sb.Length - 1, 1);
            sb.Append(");");
            ExecuteNonQuery(sb.ToString());
        }

        #endregion

        #region Data Logging
        public void WriteDataToDB(DataRequest completedRequest)
        {
            ///Globals.SystemManager.LogApplicationEvent(this, "", "Received completed request");

            // don't accept any new data if we're in the process of shutting down, just log what we've got
            if (Globals.FDAStatus == Globals.AppState.ShuttingDown) return;

            lock (_writeQueue)
            {
                _writeQueue.Enqueue(completedRequest);
            }
            if (!_dataWriter.IsBusy)
                _dataWriter.RunWorkerAsync();
        }


        private void DataWriter_DoWork(object sender, DoWorkEventArgs e)
        {

            try
            {

                //int attempt = 0;
                //using (NpgsqlConnection conn = new NpgsqlConnection(ConnectionString))
                //{
                //  try
                //  {
                //     conn.Open();
                //  }
                //  catch (Exception ex)
                //  {
                //      // failed to connect to DB, exit the DB write routine
                //      Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to connect to the database");
                //      return;
                //  }

                // db connection successful
                DataRequest transactionToLog = null;

                while (_writeQueue.Count > 0 /*&& conn.State == System.Data.ConnectionState.Open*/)
                {
                    // Get the next transaction out of the queue
                    lock (_writeQueue)
                    {
                        if (transactionToLog != _writeQueue.Peek())
                        {
                            transactionToLog = _writeQueue.Peek();
                            //attempt = 0;
                        }
                    }

                    StringBuilder batch = new StringBuilder();
                    object value;
                    int querycount = 0;


                    string[] destList = transactionToLog.Destination.Split(',');



                    foreach (Tag tag in transactionToLog.TagList)
                    {
                        // don't try to write placeholder tags
                        if (tag.TagID == Guid.Empty)
                            continue;

                        // don't write disabled tags, or tags that have been deleted
                        FDADataPointDefinitionStructure tagDef = GetTagDef(tag.TagID);
                        if (tagDef == null)
                            continue;
                        if (!tagDef.DPDSEnabled)
                            continue;

                        if (tag.Value != null)
                            value = tag.Value.ToString();
                        else
                            value = "null";

                        bool firstDest = true;


                        foreach (string dest in destList)
                        {
                            if (transactionToLog.DBWriteMode == DataRequest.WriteMode.Insert || !firstDest) // always do an insert on additional destination tables (even if the writemode is set to update)
                            {

                                // Globals.SystemManager.LogApplicationEvent(this, "","Caching " + transactionToLog.TagList.Count(p => p.TagID != Guid.Empty) + " data points to be written to the table '" + dest + "'");


                                // cache inserts instead of writing them out right now
                                _cacheManager.CacheDataPoint(dest, tag);
                            }

                            // do an update on the first destination table, if write mode is update (if the tag isn't found in the table, do an insert instead)
                            if (transactionToLog.DBWriteMode == DataRequest.WriteMode.Update && firstDest)
                            {
                                batch.Append("if (select count(1) from ");
                                batch.Append(dest);
                                batch.Append(" where DPDUID = '");
                                batch.Append(tag.TagID.ToString());
                                batch.Append("')>0 ");
                                batch.Append("update ");
                                batch.Append(dest);
                                batch.Append(" set Value = ");
                                batch.Append(value);
                                batch.Append(", Timestamp = '");
                                batch.Append(Helpers.FormatDateTime(tag.Timestamp));
                                batch.Append("',Quality = ");
                                batch.Append(tag.Quality);
                                batch.Append(" where DPDUID = '");
                                batch.Append(tag.TagID);
                                batch.Append("' and Timestamp = (select MAX(Timestamp) from ");
                                batch.Append(dest);
                                batch.Append(" where DPDUID = '");
                                batch.Append(tag.TagID);
                                batch.Append("') else insert into ");
                                batch.Append(dest);
                                batch.Append(" (DPDUID, Timestamp, Value, Quality) values('");
                                batch.Append(tag.TagID);
                                batch.Append("','");
                                batch.Append(Helpers.FormatDateTime(tag.Timestamp));
                                batch.Append("',");
                                batch.Append(value);
                                batch.Append(",");
                                batch.Append(tag.Quality);
                                batch.Append(");");

                                querycount++;
                            }
                            firstDest = false;
                        }

                        // if this isn't a backfill request, update the last read value and timestamp in the DataPointDefinitions table
                        // Feb 12, 2020: disabled writing last value to DataPointDefinitions table 
                        /*
                        if (transactionToLog.MessageType != DataRequest.RequestType.Backfill)
                        {
                            batch += "update " + Globals.SystemManager.GetTableName("DataPointDefinitionStructures") + " set LastReadDataValue = " + tag.Value + ", LastReadDataTimestamp = '" + Helpers.FormatDateTime(tag.Timestamp) + "' "
                                  + " where DPDUID = '" + tag.TagID.ToString() + "';";
                        }
                        */

                        // if tag quality is good, write the value and timestamp to the FDALastDataValues table (update if it already exists in the table, insert otherwise)
                        if (tag.Quality == 192)
                        {
                            batch.Append("UPDATE FDALastDataValues SET value = ");
                            batch.Append(tag.Value);
                            batch.Append(",timestamp='");
                            batch.Append(Helpers.FormatDateTime(tag.Timestamp));
                            batch.Append("',");
                            batch.Append("quality=");
                            batch.Append(tag.Quality);
                            batch.Append(" where DPDUID='");
                            batch.Append(tag.TagID);
                            batch.Append("';  INSERT INTO FDALastDataValues(dpduid, value, timestamp, quality) SELECT '");
                            batch.Append(tag.TagID);
                            batch.Append("',");
                            batch.Append(tag.Value);
                            batch.Append(",'");
                            batch.Append(Helpers.FormatDateTime(tag.Timestamp));
                            batch.Append("',");
                            batch.Append(tag.Quality);
                            batch.Append(" WHERE NOT EXISTS (SELECT 1 FROM FDALastDataValues WHERE dpduid = '");
                            batch.Append(tag.TagID);
                            batch.Append("');");
                        }
                    }

                    // write it out to the database
                    if (batch.Length != 0)
                    {
                        // note: retries and error messages are handled in ExecuteNonQuery() in child classes
                        int result = ExecuteNonQuery(batch.ToString());
                        //if (result > 0)
                        //{
                        //    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "DBManager", "Group " + transactionToLog.GroupID + ", index " + transactionToLog.GroupIdxNumber + " successfully recorded in the database");
                        //}
                        //else
                        //{
                        //    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "DBManager", "Group " + transactionToLog.GroupID + ", index " + transactionToLog.GroupIdxNumber + " failed to write to the database");
                        //}

                        lock (_writeQueue) { _writeQueue.Dequeue(); }
                    }
                    Thread.Sleep(20); // slow down the queries a bit to reduce the load on SQL server while clearing a backlog 

                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "DBManager: General error in data writer thread");
            }
        }


        private void CacheMananger_CacheFlush(object sender, CacheManager.CacheFlushEventArgs e)
        {
            ExecuteNonQuery(e.Query);
        }

        public void WriteAlarmsEvents(List<AlarmEventRecord> recordsList)
        {
            if (_alarmEventQueue == null)
                _alarmEventQueue = new Queue<List<AlarmEventRecord>>();

            if (_alarmsEventsWriter == null)
            {
                _alarmsEventsWriter = new BackgroundWorker();
                _alarmsEventsWriter.DoWork += AlarmsEventsWriter_DoWork;
            }

            lock (_alarmEventQueue)
            {
                _alarmEventQueue.Enqueue(recordsList);
            }

            if (!_alarmsEventsWriter.IsBusy)
                _alarmsEventsWriter.RunWorkerAsync();
        }

        private void AlarmsEventsWriter_DoWork(object sender, DoWorkEventArgs e)
        {
            string sql;
            List<AlarmEventRecord> recordsList;




            while (_alarmEventQueue.Count > 0)
            {
                lock (_alarmEventQueue)
                {
                    recordsList = _alarmEventQueue.Dequeue();
                }

                if (recordsList.Count == 0)
                    continue;

                foreach (AlarmEventRecord record in recordsList)
                {
                    sql = record.GetWriteSQL();
                    int result = ExecuteNonQuery(sql);


                    if (result > 0) // if the write was successful, update the last read record in the "HistoricReferences" Table
                    {
                        sql = record.GetUpdateLastRecordSQL();
                        if (sql != "")
                        {
                            ExecuteNonQuery(sql);
                        }
                    }
                    Thread.Sleep(50); // a little breather for the database, this stuff doesn't need to be written as fast as possible       
                }

            }

        }
        #endregion


        public void GetLastValues(string table, Dictionary<Guid, double> values)
        {
            StringBuilder query = new StringBuilder("select A.DPDUID,A.Value FROM ");
            query.Append(table);
            query.Append(" A inner join (select DPDUID, MAX(Timestamp) as LastEntry FROM ");
            query.Append(table);
            query.Append(" Group by DPDUID having DPDUID in (");
            bool first = true;
            foreach (Guid id in values.Keys)
            {
                if (!first)
                    query.Append(",");
                else
                    first = false;

                query.Append("'");
                query.Append(id);
                query.Append("'");
            }
            query.Append(")) B on A.DPDUID = B.DPDUID and A.Timestamp = B.LastEntry");


            try
            {
                DataTable queryResultTable = ExecuteQuery(query.ToString());
                Guid key;
                foreach (DataRow row in queryResultTable.Rows)
                {
                    key = (Guid)row["DPDUID"];
                    values[key] = (Double)row["Value"];
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to retrieve values to write to field device: query = " + query);
                return;
            }


            // look for any tags that did not get a value, mention them in an event message
            StringBuilder errorList = new StringBuilder();
            foreach (KeyValuePair<Guid, double> kvp in values)
            {
                if (double.IsNaN(kvp.Value))
                {
                    errorList.Append(kvp.Key);
                    errorList.Append(",");
                }
            }

            if (errorList.Length > 0)
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "The tag(s) " + errorList + " were not found in the table " + table + " while looking for values to be written to the device. This/these tag(s) will be dropped from the write request ");
            }
            return;

        }
        public RequestGroup RequestGroupFromConfigString(string RequestingEntity, string groupconfig, int priority, bool forceCommsLogging = false)
        {

            string[] groupconfigparts;
            string[] connectionsList;

            RequestGroup newGroup;

            /*
             * "FDA Start, config error: DBRG ID: " + groupconfig +", Invalid format - DataPointBlockRequestListVals"
             */

            //"FDA Demand, config error: DBRG ID: " + groupconfig + ", Invalid format - DataPointBlockRequestListVals"

            try
            {
                groupconfigparts = groupconfig.Split(':');
                connectionsList = groupconfigparts[1].Split('^'); // separate primary and backup connection IDs

                string destList = "";
                for (int i = 2; i < groupconfigparts.Length; i++)
                {
                    destList += groupconfigparts[i];
                    if (i + 1 < groupconfigparts.Length)
                        destList += ",";
                }

                newGroup = new RequestGroup()
                {
                    ID = Guid.Parse(groupconfigparts[0]),
                    ConnectionID = Guid.Parse(connectionsList[0]),
                    DestinationID = destList,
                    Priority = priority
                };
            }
            catch
            {
                if (RequestingEntity.StartsWith("Sched"))
                    Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, config error: " + RequestingEntity + ", Invalid format - RequestGroupList", true);
                else if (RequestingEntity.StartsWith("Demand"))
                    Globals.SystemManager.LogApplicationEvent(this, "", "FDA Demand, config error: " + RequestingEntity + ", Invalid format - RequestGroupList", true);
                return null;
            }

            lock (_requestgroupConfig)
            {
                if (_requestgroupConfig.ContainsKey(newGroup.ID))
                {
                    newGroup.Enabled = _requestgroupConfig[newGroup.ID].DRGEnabled;
                    newGroup.DBGroupRequestConfig = _requestgroupConfig[newGroup.ID];
                    newGroup.Description = _requestgroupConfig[newGroup.ID].Description;
                    newGroup.CommsLogEnabled = (_requestgroupConfig[newGroup.ID].CommsLogEnabled || forceCommsLogging);
                }
                else
                {
                    if (RequestingEntity.StartsWith("Sched"))
                        Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, config error: Invalid DBRG ID: '" + groupconfig.Split(':')[0] + "' in " + RequestingEntity + ". The Invalid DBRG will not be processed.", true);
                    else if (RequestingEntity.StartsWith("Demand"))
                        Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, config error: Invalid DBRG ID: '" + groupconfig.Split(':')[0] + "' in " + RequestingEntity + ". The Invalid DBRG will not be processed.", true);
                    return null;
                }
            }
            if (connectionsList.Length > 1)
                newGroup.BackupConnectionID = Guid.Parse(connectionsList[1]);



            newGroup.TagsRef = _dataPointConfig;

            return newGroup;
        }


        protected string[] FindNulls(object thing, List<string> exceptionList = null)
        {
            List<string> nullProperties = new List<string>();

            foreach (System.Reflection.PropertyInfo property in thing.GetType().GetProperties())
            {
                if (property.GetValue(thing) == null)
                {
                    if (exceptionList == null)
                    {
                        nullProperties.Add(property.Name);
                    }
                    else
                    {
                        if (!exceptionList.Contains(property.Name))
                            nullProperties.Add(property.Name);
                    }
                }
            }

            return nullProperties.ToArray();
        }

        private void LogTrimTimerTick(object o)
        {
            Globals.SystemManager.LogApplicationEvent(this, "", "Trimming the event log and comms history tables");

            string CommsLog = Globals.SystemManager.GetTableName("CommsLog");
            string AppLog = Globals.SystemManager.GetTableName("AppLog");
            int result = 0;

            // SQL Server version
            //string query = "DELETE FROM " + AppLog + " where Timestamp < DATEADD(HOUR," + Globals.UTCOffset + ",GETUTCDATE()) - " + Globals.SystemManager.EventLogMaxDays;

            // PostgreSQL version
            //string query = "DELETE FROM " + AppLog + " where Timestamp < (current_timestamp at time zone 'UTC' + INTERVAL '" + Globals.UTCOffset + " hours') - INTERVAL '" +  Globals.SystemManager.EventLogMaxDays + " days'";

            // general version
            DateTime oldestLimit = DateTime.UtcNow.Subtract(TimeSpan.FromDays(Globals.SystemManager.EventLogMaxDays));
            string query = "DELETE FROM " + AppLog + " where Timestamp < '" + Helpers.FormatDateTime(oldestLimit) + "'";

            result = ExecuteNonQuery(query);

            if (result >= 0)
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "Trimmed " + result + " rows from " + AppLog);
            }

            //SQL Server version
            //string query = "DELETE FROM " + CommsLog + " where Timestamp < DATEADD(HOUR," + Globals.UTCOffset + ",GETUTCDATE()) - " + Globals.SystemManager.CommsLogMaxDays;

            // PostgreSQL version
            //query = "DELETE FROM " + CommsLog + " where TimestampUTC1 < (current_timestamp at time zone 'UTC' + INTERVAL '" + Globals.UTCOffset + " hours') - INTERVAL '" + Globals.SystemManager.CommsLogMaxDays + " days'";

            // general version
            oldestLimit = DateTime.UtcNow.Subtract(TimeSpan.FromDays(Globals.SystemManager.CommsLogMaxDays));
            query = "DELETE FROM " + CommsLog + " where TimestampUTC1 < '" + Helpers.FormatDateTime(oldestLimit) + "'";

            result = ExecuteNonQuery(query);
            if (result >= 0)
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "Trimmed " + result + " rows from " + CommsLog);
            }
        }



        protected void DBCheckTimerTick(Object o)
        {
            if (TestConnection() == false)
            {
                if (!_DBDownTimer.IsRunning)
                    _DBDownTimer.Start();

                // failed to connect                  
                Console.WriteLine(Globals.FDANow().ToString() + ": The database connection is down (" + _DBDownTimer.Elapsed.ToString() + ")");

                if (_DBDownTimer.Elapsed >= _databaseDownNotificationLimit)
                {
                    // perform the notification (email? audible alarm?)
                    Console.WriteLine(Globals.FDANow().ToString() + ": The database connection downtime has exceeded the limit (future: email? audible alarm?");
                }
                DBStatus = false;
                return;
            }

            // current status = true, previous status = false means a recovered connection
            if (DBStatus == false)
            {
                _DBDownTimer.Stop();
                _DBDownTimer.Reset();

                DBStatus = true;
                Globals.SystemManager.LogApplicationEvent(this, "DBManager", "Database connection restored, re-initializing the DBManager");
                Initialize();
                LoadConfig();
                StartChangeMonitoring();
                return;
            }


            DateTime DB_starttime = GetDBStartTime();

            if (DB_starttime > _PreviousDBStartTime)
            {
                Globals.SystemManager.LogApplicationEvent(this, "DBManager", "Database connection start time mismatch, re-initializing DBManager");

                Initialize();
                LoadConfig();
                StartChangeMonitoring();

                if (_writeQueue.Count > 0 && !_dataWriter.IsBusy)
                    _dataWriter.RunWorkerAsync();
            }
        }

        public class DemandEventArgs : EventArgs
        {
            public FDARequestGroupDemand DemandRequestObject { get; }
            public List<RequestGroup> RequestGroups;

            public DemandEventArgs(FDARequestGroupDemand demand, List<RequestGroup> requestGroups)
            {
                DemandRequestObject = demand;
                RequestGroups = requestGroups;
            }
        }

        public class ForceScheduleEventArgs : EventArgs
        {
            public Guid ScheduleID { get; }

            public ForceScheduleEventArgs(Guid scheduleID)
            {
                ScheduleID = scheduleID;
            }
        }

        protected void RaiseDemandRequestEvent(FDARequestGroupDemand demand, List<RequestGroup> requestGroups)
        {
            DemandRequest?.Invoke(this, new DemandEventArgs(demand, requestGroups));
        }


        protected void RaiseConfigChangeEvent(string type, string table, Guid item)
        {
            //Globals.SystemManager.LogApplicationEvent(this, "", table + " table changed (" + type + "), ID = " + item.ToString());
            ConfigChange?.Invoke(this, new ConfigEventArgs(type, table, item));
        }


        protected bool PreReqCheck()
        {
            // applog table & comms log table
            bool appLogexists = false;
            bool commsLogExists = false;
            //bool FDAServiceBrokerEnabled = false;
            // bool FDASystemServiceBrokerEnabled = false;
            string FDAdbname;
            object result;
            string query;


            // check if commslog and applog tables exist
            query = "select count(1) from INFORMATION_SCHEMA.TABLES where TABLE_NAME  = '" + Globals.SystemManager.GetTableName("applog") + "';";
            result = ExecuteScalar(query);
            if (result != null)
            {
                appLogexists = ((Int32)Convert.ChangeType(result, typeof(Int32)) > 0);
            }


            query = "select count(1) from INFORMATION_SCHEMA.TABLES where TABLE_NAME  = '" + Globals.SystemManager.GetTableName("commslog") + "';";
            result = ExecuteScalar(query);
            if (result != null)
            {
                commsLogExists = ((Int32)Convert.ChangeType(result, typeof(Int32)) > 0);
            }


            FDAdbname = "FDA";
            if (Globals.SystemManager.GetAppConfig().ContainsKey("FDADBName"))
                FDAdbname = Globals.SystemManager.GetAppConfig()["FDADBName"].OptionValue;



            if (!appLogexists)
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "Prequisite fail: The table " + Globals.SystemManager.GetTableName("AppLog") + " doesn't exist, creating it");

                appLogexists = BuildAppLogTable(Globals.SystemManager.GetTableName("AppLog"));
            }

            if (!commsLogExists)
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "Prerequisite fail: The table " + Globals.SystemManager.GetTableName("CommsLog") + " doesn't exist, creating it");

                commsLogExists = BuildCommsLogTable(Globals.SystemManager.GetTableName("CommsLog"));
            }

            return appLogexists && commsLogExists;
        }

        protected List<RequestGroup> RequestGroupListToRequestGroups(Guid requestorID, int priority, string requestGroupString, out List<FDATask> otherTasks)
        {
            List<RequestGroup> requestGroups = new List<RequestGroup>();
            string[] requestgroupconfigs;
            RequestGroup newRequestGroup;
            requestgroupconfigs = requestGroupString.Split('|');
            Guid requestGroupID;
            otherTasks = new List<FDATask>();

            foreach (string groupconfig in requestgroupconfigs)
            {
                if (groupconfig.StartsWith("^"))
                {
                    if (!Guid.TryParse(groupconfig.Remove(0, 1), out Guid taskID))
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "", "Invalid Task ID in schedule " + requestorID + ", the correct format is xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", true);
                        continue;
                    }

                    if (_taskConfig.ContainsKey(taskID))
                    {
                        switch (_taskConfig[taskID].task_type.ToUpper())
                        {
                            case "DATAACQ":
                                newRequestGroup = RequestGroupFromConfigString("Schedule " + requestorID, _taskConfig[taskID].task_details, priority);
                                if (newRequestGroup != null)
                                {
                                    requestGroups.Add(newRequestGroup);
                                    requestGroupID = Guid.Parse(_taskConfig[taskID].task_details.Split(':')[0]);
                                    newRequestGroup.Protocol = GetRequestGroup(requestGroupID).DPSType.ToUpper();
                                }
                                break;
                            case "CALCCOMMSSTATS":
                                otherTasks.Add(_taskConfig[taskID]);
                                break;
                            default:
                                Globals.SystemManager.LogApplicationEvent(this, "", "unable to execute task id " + taskID + ", due to invalid entry in task_details[0]");
                                break;
                        }
                    }
                    else
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "", "Task " + taskID + " was not found. Task was requested by schedule " + requestorID, true);
                    }
                }
                else
                {

                    newRequestGroup = RequestGroupFromConfigString("Schedule " + requestorID, groupconfig, priority);
                    if (newRequestGroup != null)
                    {
                        requestGroups.Add(newRequestGroup);
                        requestGroupID = Guid.Parse(groupconfig.Split(':')[0]);
                        newRequestGroup.Protocol = GetRequestGroup(requestGroupID).DPSType.ToUpper();
                    }
                }
            }

            return requestGroups;
        }

        // load the configuration from the database tables into Dictionary objects in memory (return false if unable to connect to DB, true if successful)
        public bool LoadConfig()
        {
            // clear any existing configuration
            lock (_schedConfig) { _schedConfig.Clear(); }
            lock (_requestgroupConfig) { _requestgroupConfig.Clear(); }
            lock (_dataPointConfig) { _dataPointConfig.Clear(); }
            lock (_connectionsConfig) { _connectionsConfig.Clear(); }
            lock (_taskConfig) { _taskConfig.Clear(); }
            lock (_deviceConfig) { _deviceConfig.Clear(); }
            //lock (_derivedTagConfig) { _derivedTagConfig.Clear(); }


            string tableName = "";


            //bool MQTTColumnExists;
            string ID; // a temporary place to store the ID (as a string) of whatever we're trying to parse, so we can include it in the error message if parsing fails 
            string query = "";

            // ***********************    load connections ******************************************
            tableName = Globals.SystemManager.GetTableName("FDASourceConnections");
            query = "select * from " + tableName;

            //sqlCommand.CommandText = query;

            //MQTTColumnExists = HasColumn(sqlDataReader, "MQTTEnabled");
            FDASourceConnection newConnConfig;
            DataTable table = ExecuteQuery(query);
            foreach (DataRow row in table.Rows)
            {
                ID = "(unknown)";
                try
                {
                    ID = row["SCUID"].ToString(); //reader.GetGuid(reader.GetOrdinal("SCUID")).ToString();
                    newConnConfig = new FDASourceConnection()
                    {
                        SCUID = (Guid)row["SCUID"],
                        SCType = row["SCType"].ToString(),
                        SCDetail01 = row["SCDetail01"].ToString(),
                        SCDetail02 = row["SCDetail02"].ToString(),
                        Description = row["Description"].ToString(),
                        RequestRetryDelay = (Int32)row["RequestRetryDelay"],
                        SocketConnectionAttemptTimeout = (Int32)row["SocketConnectionAttemptTimeout"],
                        MaxSocketConnectionAttempts = (Int32)row["MaxSocketConnectionAttempts"],
                        SocketConnectionRetryDelay = (Int32)row["SocketConnectionRetryDelay"],
                        PostConnectionCommsDelay = (Int32)row["PostConnectionCommsDelay"],
                        InterRequestDelay = (Int32)row["InterRequestDelay"],
                        MaxRequestAttempts = (Int32)row["maxRequestAttempts"],
                        RequestResponseTimeout = (Int32)row["RequestResponseTimeout"],
                        ConnectionEnabled = (bool)row["ConnectionEnabled"],
                        CommunicationsEnabled = (bool)row["CommunicationsEnabled"],
                        CommsLogEnabled = (bool)row["CommsLogEnabled"]
                        //MQTTEnabled = false
                    };

                    //if (MQTTColumnExists)
                    //    newConnConfig.MQTTEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("MQTTEnabled"));

                    lock (_connectionsConfig)
                    {
                        _connectionsConfig.Add(newConnConfig.SCUID, newConnConfig);

                    }
                }
                catch
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - SCUID '" + ID + "' rejected", true);
                }
            }
            table.Clear();



            // load datapoint definitions (both PLC tags and soft tags)
            DateTime currentTime = Globals.FDANow();
            tableName = Globals.SystemManager.GetTableName("DataPointDefinitionStructures");

            query = "select *," +
                    "case when backfill_enabled is NULL then cast(0 as bit) else backfill_enabled END as nullsafe_backfill_enabled," +
                    "case when backfill_data_id is NULL then - 1 else backfill_data_id END as nullsafe_backfill_data_id," +
                    "case when backfill_data_structure_type is NULL then 0 else backfill_data_structure_type END as nullsafe_backfill_data_structure_type," +
                    "case when backfill_data_lapse_limit is NULL then 60 else backfill_data_lapse_limit END as nullsafe_backfill_data_lapse_limit," +
                    "case when backfill_data_interval is NULL then 1 else backfill_data_interval END as nullsafe_backfill_data_interval " +
                    "from DataPointDefinitionStructures";

            FDADataPointDefinitionStructure newTag;


            //MQTTColumnExists = HasColumn(sqlDataReader, "MQTTEnabled");
            table = ExecuteQuery(query);
            string DPSType;
            string softtagtype;
            string softtagarguments;
            bool softtagenabled;
            foreach (DataRow row in table.Rows)
            {
                ID = "(unknown)";
                try
                {
                    ID = row["DPDUID"].ToString();
                    DPSType = row["DPSType"].ToString().ToUpper();
                    if (DPSType == "SOFTTAG")
                    {
                        // create a soft tag
                        softtagtype = (string)row["read_detail_01"];
                        softtagarguments = (string)row["physical_point"];
                        softtagenabled = (bool)row["DPDSEnabled"];
                        DerivedTag newDerivedTag = DerivedTag.Create(ID, softtagtype, softtagarguments);
                        if (newDerivedTag == null)
                        {
                            Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - DPDS ID '" + ID + "' rejected. Unrecognized soft tag type '"+ softtagtype + "'", true);
                            continue;
                        }
                       
                        newDerivedTag.DPDSEnabled = (bool)row["DPDSEnabled"];
                        newDerivedTag.DPSType = row["DPSType"].ToString();
                        newDerivedTag.read_scaling = (bool)row["read_scaling"];
                        newDerivedTag.read_scale_raw_low = (double)row["read_scale_raw_low"];
                        newDerivedTag.read_scale_raw_high = (double)row["read_scale_raw_high"];
                        newDerivedTag.read_scale_eu_low = (double)row["read_scale_eu_low"];
                        newDerivedTag.read_scale_eu_high = (double)row["read_scale_eu_high"];
                        newDerivedTag.write_scaling = (bool)row["write_scaling"];
                        newDerivedTag.write_scale_raw_low = (double)row["write_scale_raw_low"];
                        newDerivedTag.write_scale_raw_high = (double)row["write_scale_raw_high"];
                        newDerivedTag.write_scale_eu_low = (double)row["write_scale_eu_low"];
                        newDerivedTag.write_scale_eu_high = (double)row["write_scale_eu_high"];
                        //newDerivedTag.LastRead = FDADataPointDefinitionStructure.Datapoint.Empty;
                        //newDerivedTag.LastReadDataValue = 0.0;
                        //newDerivedTag.LastReadQuality = 32;
                        //newDerivedTag.LastReadDataTimestamp = currentTime;

                        // these properties don't apply to soft tags
                        newDerivedTag.backfill_enabled = false;
                        newDerivedTag.backfill_data_ID = -1;
                        newDerivedTag.backfill_data_structure_type = 0;
                        newDerivedTag.backfill_data_lapse_limit = 0;
                        newDerivedTag.backfill_data_interval = 0;

                        newTag = newDerivedTag;
                    }
                    else
                    {
                        // create a regular tag
                        newTag = new FDADataPointDefinitionStructure()
                        {
                            DPDUID = (Guid)row["DPDUID"],
                            DPDSEnabled = (bool)row["DPDSEnabled"],
                            DPSType = row["DPSType"].ToString(),
                            read_scaling = (bool)row["read_scaling"],
                            read_scale_raw_low = (double)row["read_scale_raw_low"],
                            read_scale_raw_high = (double)row["read_scale_raw_high"],
                            read_scale_eu_low = (double)row["read_scale_eu_low"],
                            read_scale_eu_high = (double)row["read_scale_eu_high"],
                            write_scaling = (bool)row["write_scaling"],
                            write_scale_raw_low = (double)row["write_scale_raw_low"],
                            write_scale_raw_high = (double)row["write_scale_raw_high"],
                            write_scale_eu_low = (double)row["write_scale_eu_low"],
                            write_scale_eu_high = (double)row["write_scale_eu_high"],
                            backfill_enabled = (bool)row["nullsafe_backfill_enabled"],
                            backfill_data_ID = (Int32)row["nullsafe_backfill_data_id"],
                            //LastRead = new FDADataPointDefinitionStructure.Datapoint(0, 32, currentTime, "", null, DataRequest.WriteMode.Insert),
                            backfill_data_structure_type = (Int32)row["nullsafe_backfill_data_structure_type"],
                            backfill_data_lapse_limit = (double)row["nullsafe_backfill_data_lapse_limit"],
                            backfill_data_interval = (double)row["nullsafe_backfill_data_interval"]
                            //MQTTEnabled = false
                        };
                    }

                    //if (MQTTColumnExists)
                    //    newTag.MQTTEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("MQTTEnabled"));

                    lock (_dataPointConfig)
                    {
                        _dataPointConfig.Add(newTag.DPDUID, newTag);
                    }
                }
                catch
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - DPDS ID '" + ID + "' rejected", true);
                }
            }
            table.Clear();


            // new Mar 9, 2020  load last values from the FDALastDataValues table and update the DataPointDefinitionStructure objects in memory
            Guid DPDUID;
            tableName = Globals.SystemManager.GetTableName("FDALastDataValues");
            query = "select DPDUID,value,timestamp,quality from " + tableName + ";";


            //sqlCommand.CommandText = query;
            table = ExecuteQuery(query);
            foreach (DataRow row in table.Rows)
            {
                ID = "(unknown)";
                try
                {
                    ID = row["DPDUID"].ToString();
                    lock (_dataPointConfig)
                    {
                        DPDUID = (Guid)row["DPDUID"];
                        if (_dataPointConfig.ContainsKey(DPDUID))
                        {
                            _dataPointConfig[DPDUID].LastRead = new FDADataPointDefinitionStructure.Datapoint(
                                (Double)row["value"],                               
                                (Int32)row["quality"],
                                (DateTime)row["timestamp"],
                                "",
                                DataType.UNKNOWN,
                                DataRequest.WriteMode.Insert
                                );
                        }
                    }
                }
                catch
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - DPDS ID '" + ID + "' rejected", true);
                }
            }
            table.Clear();

            // make the dictionary of all tags (PLC and soft tags) available to each individual soft tag
            DerivedTag.Tags = _dataPointConfig;

            // validate and activate any soft tags
            DerivedTag thisTag;
            foreach (FDADataPointDefinitionStructure tag in _dataPointConfig.Values)
            {
                if (tag.DPSType.ToLower() == "softtag")
                {
                    thisTag = (DerivedTag)tag;
                    thisTag.Initialize();
                    if (!thisTag.IsValid)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - DPDS ID '" + thisTag.DPDUID + "' rejected. " + thisTag.ErrorMessage, true);
                    }
                    thisTag.OnUpdate += SoftPoint_OnUpdate;
                }
            }

            if (_devicesTableExists)
            {

                tableName = Globals.SystemManager.GetTableName("FDADevices");
                query = "SELECT * from " + tableName + ";";

                //sqlCommand.CommandText = query;
                table = ExecuteQuery(query);

                foreach (DataRow row in table.Rows)
                {
                    ID = "(unknown)";
                    try
                    {
                        ID = row["device_id"].ToString();
                        lock (_deviceConfig)
                        {
                            _deviceConfig.Add((Guid)row["device_id"],
                                new FDADevice()
                                {
                                    DEVICE_ID = (Guid)row["device_id"],
                                    request_timeout = (Int32)row["request_timeout"],
                                    max_request_attempts = (Int32)row["max_request_attempts"],
                                    inter_request_delay = (Int32)row["inter_request_delay"],
                                    request_retry_delay = (Int32)row["request_retry_delay"]
                                });
                        }
                    }
                    catch
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - Device ID '" + ID + "' rejected", true);
                    }
                }
                table.Clear();
            }

            if (_tasksTableExists)
            {

                tableName = Globals.SystemManager.GetTableName("FDATasks");
                query = "SELECT * from " + tableName + ";";

                table = ExecuteQuery(query);
                foreach (DataRow row in table.Rows)
                {
                    ID = "(unknown)";
                    try
                    {
                        ID = row["task_id"].ToString();
                        lock (_taskConfig)
                        {
                            _taskConfig.Add((Guid)row["task_id"],
                                new FDATask()
                                {
                                    TASK_ID = (Guid)row["task_id"],
                                    task_type = (string)row["task_type"],
                                    task_details = (string)row["task_details"]
                                });
                        }
                    }
                    catch
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - Task ID '" + ID + "' rejected", true);
                    }
                }
                table.Clear();
            }


            // load Datablock Request Groups (groups of individual requests)
            tableName = Globals.SystemManager.GetTableName("FDADataBlockRequestGroup");
            query = "SELECT DRGUID,Description,DRGEnabled,DPSType,DataPointBlockRequestListVals,CommsLogEnabled from " + tableName + ";";

            //sqlCommand.CommandText = query;
            table = ExecuteQuery(query);
            foreach (DataRow row in table.Rows)
            {
                ID = "(unknown)";
                try
                {
                    ID = row["DRGUID"].ToString();
                    //if (sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("DRGEnabled")))
                    // {
                    lock (_requestgroupConfig)
                    {
                        _requestgroupConfig.Add((Guid)row["DRGUID"],
                            new FDADataBlockRequestGroup()
                            {
                                DRGUID = (Guid)row["DRGUID"],
                                Description = (string)row["Description"],
                                DRGEnabled = (bool)row["DRGEnabled"],
                                DPSType = (string)row["DPSType"],
                                DataPointBlockRequestListVals = (string)row["DataPointBlockRequestListVals"],
                                CommsLogEnabled = (bool)row["CommsLogEnabled"]
                            });
                    }
                    //}
                }
                catch
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - DBRG ID '" + ID + "' rejected", true);
                }
            }
            table.Clear();



            // load Schedules
            tableName = Globals.SystemManager.GetTableName("FDARequestGroupScheduler");
            query = "SELECT FRGSUID,Description,FRGSEnabled,FRGSType,RealTimeRate,Year,Month,Day,Hour,Minute,Second,Priority,RequestGroupList from " + tableName + ";";


            table = ExecuteQuery(query);

            foreach (DataRow row in table.Rows)
            {
                ID = "(unknown)";

                try
                {
                    ID = row["FRGSUID"].ToString();
                    FDARequestGroupScheduler scheduler = new FDARequestGroupScheduler()
                    {
                        FRGSUID = (Guid)row["FRGSUID"],
                        Description = (string)row["Description"],
                        FRGSEnabled = (bool)row["FRGSEnabled"],
                        FRGSType = (string)row["FRGSType"],
                        RealTimeRate = (Int32)row["RealTimeRate"],
                        Year = (Int32)row["Year"],
                        Month = (Int32)row["Month"],
                        Day = (Int32)row["Day"],
                        Hour = (Int32)row["Hour"],
                        Minute = (Int32)row["Minute"],
                        Second = (Int32)row["Second"],
                        Priority = (Int32)row["Priority"],
                        RequestGroupList = (string)row["RequestGroupList"],  // requestgroupID:connectionID:DestinationID | requestgroupID:connectionID:DestinationID | .....
                    };
                    if (scheduler.Year == 0) scheduler.Year = 1;
                    if (scheduler.Month == 0) scheduler.Month = 1;
                    if (scheduler.Day == 0) scheduler.Day = 1;

                    lock (_schedConfig) { _schedConfig.Add(scheduler.FRGSUID, scheduler); }
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - FRGS ID '" + ID + "' rejected (exception:" + ex.Message + ")", true);
                }
            }


            // after loading the Schedule configs, turn the requestgroup strings in the schedulers into actual RequestGroup objects, ready to be passed to a connection manager          
            List<RequestGroup> requestGroups;
            lock (_schedConfig)
            {
                foreach (FDARequestGroupScheduler sched in _schedConfig.Values)
                {
                    requestGroups = RequestGroupListToRequestGroups(sched.FRGSUID, sched.Priority, sched.RequestGroupList, out List<FDATask> tasks);
                    sched.RequestGroups.AddRange(requestGroups);
                    sched.Tasks.AddRange(tasks);
                }
            }
            return true;
        }

        private void SoftPoint_OnUpdate(object sender, EventArgs e)
        {
            DerivedTag updatedSoftpoint = (DerivedTag)sender;

            // the data logger requires a DataRequest object, containing a list of Tag objects with data to be written
            // we'll create one for the updated soft point

            Tag softPointTag = new Tag(updatedSoftpoint.DPDUID)
            {
                Timestamp = updatedSoftpoint.LastRead.Timestamp,
                Quality = updatedSoftpoint.LastRead.Quality,
                Value = updatedSoftpoint.LastRead.Value,
                TagID = updatedSoftpoint.DPDUID
            };

            DataRequest softTagRequestObject = new DataRequest()
            {
                RequestID = Guid.NewGuid().ToString(),
                TagList = new List<Tag> { softPointTag },
                Destination = updatedSoftpoint.LastRead.DestTable,
                DBWriteMode = updatedSoftpoint.LastRead.WriteMode
            };

            // and insert it into the pipeline to be written to the database
            WriteDataToDB(softTagRequestObject);

            // and log a message about the update 
            Globals.SystemManager.LogApplicationEvent(this, "", "Soft tag " + updatedSoftpoint.DPDUID.ToString() + " re-calculated",false,true);
        }

        public void UpdateAlmEvtCurrentPtrs(DataRequest PtrPositionRequest)
        {
            string sql;
            ushort almsPtr = (ushort)PtrPositionRequest.TagList[0].Value;
            DateTime almsTimestamp = PtrPositionRequest.TagList[0].Timestamp;
            ushort evtsPtr = (ushort)PtrPositionRequest.TagList[1].Value;
            DateTime evtsTimestamp = PtrPositionRequest.TagList[1].Timestamp;
            string nodeID = PtrPositionRequest.NodeID;
            int affectedRows = 0;

            string ResponseTimestamp = Helpers.FormatDateTime(PtrPositionRequest.ResponseTimestamp);


            // update the alarm or event current ptr and timestamp
            sql = "update " + Globals.SystemManager.GetTableName("FDAHistoricReferences") + " set AlarmsCurPtrPosition = " + almsPtr + ", AlarmsCurPtrTimestamp = '" + Helpers.FormatDateTime(almsTimestamp) + "'";
            sql += ",EventsCurPtrPosition = " + evtsPtr + ", EventsCurPtrTimestamp = '" + Helpers.FormatDateTime(evtsTimestamp) + "'";
            sql += " where NodeDetails = '" + nodeID + "' and ConnectionUID = '" + PtrPositionRequest.ConnectionID + "';";
            affectedRows = ExecuteNonQuery(sql);

            if (affectedRows == 0) // update had no effect because row doesn't exist, do an insert instead
            {
                sql = "insert into " + Globals.SystemManager.GetTableName("FDAHistoricReferences") + " (DPSType,HistoricStructureType,ConnectionUID,NodeDetails,EventsCurPtrTimestamp,EventsCurPtrPosition,EventsCurMaxNumRecords,";
                sql += "EventsLastPtrReadTimestamp,EventsLastPtrReadPosition,AlarmsCurPtrTimestamp,AlarmsCurPtrPosition,AlarmsCurMaxNumRecords,AlarmsLastPtrReadTimestamp,AlarmsLastPtrReadPosition)";
                sql += " values (";
                sql += "'" + PtrPositionRequest.Protocol + "',1,'" + PtrPositionRequest.ConnectionID + "','" + PtrPositionRequest.NodeID + "','" + ResponseTimestamp + "',";
                sql += evtsPtr + ",240,'" + ResponseTimestamp + "',0,'" + ResponseTimestamp + "'," + almsPtr + ",240,'" + ResponseTimestamp + "',0);";
                ExecuteNonQuery(sql);
            }


        }

        public byte[] GetAlmEvtPtrs(Guid connID, string NodeID, string ptrType)
        {
            string sql;
            int lastRead = 0;
            int currPtr = 0;



            // update the alarm or event current ptr and timestamp
            sql = "select " + ptrType + "CurPtrPosition," + ptrType + "LastPtrReadPosition from " + Globals.SystemManager.GetTableName("FDAHistoricReferences");
            sql += " where NodeDetails = '" + NodeID + "' and ConnectionUID = '" + connID + "';";

            DataTable resultsTable = ExecuteQuery(sql);
            foreach (DataRow row in resultsTable.Rows)
            {
                try
                {
                    lastRead = (byte)row[ptrType + "LastPtrReadPosition"];
                    currPtr = (byte)row[ptrType + "CurPtrPosition"];
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "DBManager: out of range current or last read pointer in FDAHistoricReferences table (connectionID " + connID + ", Node " + NodeID);
                    return null;
                }
            }



            lastRead = Helpers.AddCircular(lastRead, -1, 0, 239);

            return new byte[] { Convert.ToByte(lastRead), Convert.ToByte(currPtr) };
        }

        public List<object> ParseStatsCalcParams(string paramstring, out string error)
        {
            string[] calcParams = paramstring.Split(':');

            if (calcParams.Length < 2)
            {
                error = "at least two parameters are required  (EndTime:StartTime or recent:minutes)";
                return null;
            }

            DateTime fromTime;
            DateTime toTime;
            List<object> statsCalcParams = new List<object>();
            var ci = new CultureInfo("en-CA");
            if (calcParams[0].ToUpper() == "RECENT")
            {
                toTime = Globals.FDANow();

                if (uint.TryParse(calcParams[1], out uint minutesBack))
                {
                    if (minutesBack < 1)
                    {
                        error = "invalid parameter 2, must be an integer value between 1 and 4,294,967,295 if parameter 1 is 'recent'";
                        return null;
                    }
                    fromTime = toTime.AddMinutes(-1 * minutesBack);
                }
                else
                {
                    error = "invalid parameter 2, must be an integer value between 1 and 4,294,967,295 if parameter 1 is 'recent'";
                    return null;
                }
            }
            else
            {
                try
                {
                    toTime = DateTime.ParseExact(calcParams[0], "yyyy-MM-dd HH-mm", ci);
                    fromTime = DateTime.ParseExact(calcParams[1], "yyyy-MM-dd HH-mm", ci);
                }
                catch
                {
                    error = "Incorrectly formatted date/time (correct format is YYYY-MM-dd HH-mm)";
                    return null;
                }
            }

            statsCalcParams.Add(toTime);
            statsCalcParams.Add(fromTime);

            if (calcParams.Length > 2)
            {
                // connection filter
                statsCalcParams.Add(calcParams[2]);
            }


            if (calcParams.Length > 3)
            {
                // device filter
                statsCalcParams.Add(calcParams[3]);
            }

            if (calcParams.Length > 4)
            {
                // Description
                statsCalcParams.Add(calcParams[4]);
            }

            if (calcParams.Length > 5)
            {
                // alternate output
                string tablename = (string)calcParams[5];
                if (tablename.Length > 0)
                {
                    statsCalcParams.Add(calcParams[5]);
                }
                else
                {
                    error = "alternate output table name must contain at least one character";
                    return null;
                }
            }

            error = "";
            return statsCalcParams;
        }

        protected void TaskMonitorNotification(string changeType, FDATask task)
        {
            // check for nulls
            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                string[] nulls = FindNulls(task);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "task_id " + task.TASK_ID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            string action = "";

            lock (_taskConfig)
            {
                switch (changeType)
                {
                    case "DELETE":
                        if (_taskConfig.ContainsKey(task.TASK_ID))
                        {
                            _taskConfig.Remove(task.TASK_ID);
                            action = "deleted";
                        }
                        else
                        {
                            action = "was deleted from the database but not found in the FDA, so no action was taken";
                        }
                        break;
                    case "INSERT":
                        _taskConfig.Add(task.TASK_ID, task);
                        action = "added";
                        break;

                    case "UPDATE":

                        if (_taskConfig.ContainsKey(task.TASK_ID))
                        {
                            action = "updated";
                            _taskConfig[task.TASK_ID] = task;
                        }
                        else
                        {
                            /* not yet supported, old values not available.... might be able to add these to the stored proc output in the future
                            if (e.EntityOldValues != null)
                            {
                                if (e.EntityOldValues.task_id != e.Entity.task_id)
                                {
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the task_id are not permitted, update for FDATask " + e.EntityOldValues.task_id + " rejected");
                                    return;
                                }
                            }
                            else
                            {
                                // record not found (not due to a change in the ID) so do an insert instead
                                _taskConfig.Add(e.Entity.task_id, e.Entity);
                                action = "not found, adding it as a new FDA Task";
                                changeType = ChangeType.Insert;
                                break;
                            }
                            */
                            // record not found, so do an insert instead
                            _taskConfig.Add(task.TASK_ID, task);
                            action = "not found, adding it as a new FDA Task";
                            changeType = "INSERT";
                            break;

                        }
                        break;
                }
            }

            Globals.SystemManager.LogApplicationEvent(this, "", "task_id  " + task.TASK_ID + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDATasks"), task.TASK_ID);
        }

        protected void UserScriptNotification(string changeType, UserScriptModule scriptModule)
        {
            // check for nulls
            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                string[] nulls = FindNulls(scriptModule);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "script module " + scriptModule.module_name + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            if (changeType == "INSERT")
            {
                DynamicCodeManager.LoadModule(scriptModule.module_name, scriptModule.script);
            }
            

        }

        protected void DeviceMonitorNotification(string changeType, FDADevice device)
        {


            // check for nulls
            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                string[] nulls = FindNulls(device);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "device_id " + device.DEVICE_ID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            string action = "";

            lock (_deviceConfig)
            {
                switch (changeType)
                {
                    case "DELETE":
                        if (_deviceConfig.ContainsKey(device.DEVICE_ID))
                        {
                            _deviceConfig.Remove(device.DEVICE_ID);
                            action = "deleted";
                        }
                        else
                        {
                            action = "deleted from the database but was not found in the FDA, so no action was taken";
                        }

                        break;
                    case "INSERT":
                        _deviceConfig.Add(device.DEVICE_ID, device);
                        action = "added";
                        break;

                    case "UPDATE":

                        if (_deviceConfig.ContainsKey(device.DEVICE_ID))
                        {
                            action = "updated";
                            _deviceConfig[device.DEVICE_ID] = device;
                        }
                        else
                        {
                            /* OLD VALUES NOT YET SUPPORTED 
                            if (e.EntityOldValues != null)
                            {
                                if (e.EntityOldValues.device_id != e.Entity.device_id)
                                {
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the DeviceID are not permitted, update for FDADevice " + e.EntityOldValues.device_id + " rejected");
                                    return;
                                }
                            }
                            else
                            {
                                // record not found (not due to a change in the ID) so do an insert instead
                                _deviceConfig.Add(e.Entity.device_id, e.Entity);
                                action = "not found, adding it as a new FDADevice";
                                changeType = ChangeType.Insert;
                                break;
                            }
                            */
                            _deviceConfig.Add(device.DEVICE_ID, device);
                            action = "not found, adding it as a new FDADevice";
                            changeType = "INSERT";

                        }
                        break;
                }
            }

            Globals.SystemManager.LogApplicationEvent(this, "", "device_id " + device.DEVICE_ID + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDADevices"), device.DEVICE_ID);
        }


        protected void DataPointMonitorNotification(string changeType, FDADataPointDefinitionStructure datapoint)
        {

            // check for nulls
            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                List<string> exceptionList = new List<string>(new string[] { "backfill_enabled", "backfill_dataID", "backfill_data_structure_type", "backfill_data_lapse_limit", "backfill_data_interval","LastReadDataType"});
                string[] nulls = FindNulls(datapoint, exceptionList);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "DPDUID " + datapoint.DPDUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            string action = "";
            lock (_dataPointConfig)
            {
                switch (changeType)
                {

                    case "INSERT":                    
                        if (datapoint.DPSType.ToUpper() == "SOFTTAG")
                        {
                            // create a soft tag
                            string softtagtype = datapoint.read_detail_01;
                            string softtagarguments = datapoint.physical_point;
                            DerivedTag newTag = DerivedTag.Create(datapoint.DPDUID.ToString(), softtagtype, softtagarguments);
                            if (newTag == null)
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "", "Config Error: DPDS ID '" + datapoint.DPDUID + "' insert rejected. Unrecognized soft tag type '" + datapoint.read_detail_01 + "'", true);
                                return;    
                            }
                            
                            newTag.DPDSEnabled = datapoint.DPDSEnabled;
                            newTag.DPSType = datapoint.DPSType;
                            newTag.read_scaling = datapoint.read_scaling;
                            newTag.read_scale_raw_low = datapoint.read_scale_raw_low;
                            newTag.read_scale_raw_high = datapoint.read_scale_raw_high;
                            newTag.read_scale_eu_low = datapoint.read_scale_eu_low;
                            newTag.read_scale_eu_high = datapoint.read_scale_eu_high;
                            newTag.write_scaling = datapoint.write_scaling;
                            newTag.write_scale_raw_low = datapoint.write_scale_raw_low;
                            newTag.write_scale_raw_high = datapoint.write_scale_raw_high;
                            newTag.write_scale_eu_low = datapoint.write_scale_eu_low;
                            newTag.write_scale_eu_high = datapoint.write_scale_eu_high;
                            newTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(0, 32, Globals.FDANow(), "", null, DataRequest.WriteMode.Insert);
                            //newTag.LastReadDataValue = 0.0;
                            //newTag.LastReadQuality = 32;
                            //newTag.LastReadDataTimestamp = Globals.FDANow();

                            // these properties don't apply to soft tags
                            newTag.backfill_enabled = false;
                            newTag.backfill_data_ID = -1;
                            newTag.backfill_data_structure_type = 0;
                            newTag.backfill_data_lapse_limit = 0;
                            newTag.backfill_data_interval = 0;
                            
                            
                            newTag.Initialize();
                            if (!newTag.IsValid)
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "", "Config Error: DPDS ID '" + datapoint.DPDUID + "' insert rejected. " + newTag.ErrorMessage, true);                               
                            }
                            newTag.OnUpdate += SoftPoint_OnUpdate;
                            _dataPointConfig.Add(newTag.DPDUID, newTag);
                        }
                        else
                        {
                            _dataPointConfig.Add(datapoint.DPDUID, datapoint);
                        }

                        
                        action = "added";
                        break;
                    case "DELETE":
                        if (_dataPointConfig.ContainsKey(datapoint.DPDUID))
                        {
                            if (datapoint.DPSType.ToLower() == "softtag")
                            {
                                DerivedTag deletedTag = (DerivedTag)_dataPointConfig[datapoint.DPDUID];
                                deletedTag.Dispose();
                            }
                            _dataPointConfig.Remove(datapoint.DPDUID);

                            action = "deleted";
                        }
                        else
                        {
                            action = "deleted from the database but was not found in the FDA so no action was taken";
                        }

                        break;
                    case "UPDATE":
                        if (_dataPointConfig.ContainsKey(datapoint.DPDUID))
                        {
                            if (_dataPointConfig[datapoint.DPDUID].DPSType.ToLower() == "softtag")
                            {
                                DerivedTag tagToChange = (DerivedTag)_dataPointConfig[datapoint.DPDUID];
                                if (datapoint.read_detail_01.ToLower() != tagToChange.read_detail_01.ToLower())
                                {
                                    // derived tag type has changed, we'll need to destroy the old one and create a brand new one
                                    Globals.SystemManager.LogApplicationEvent(this, "", "DPDS ID '" + datapoint.DPDUID + "' update requires removing and re-inserting the datapoint because the soft type type was changed. Removing old datapoint now", true);

                                    tagToChange.Dispose();
                                    _dataPointConfig.Remove(datapoint.DPDUID);
                                    DataPointMonitorNotification("INSERT", datapoint);
                                    return;
                                }

                                // same derived tag type, we can just change its arguments
                                tagToChange.Alter(datapoint.physical_point);
                                if (!tagToChange.IsValid)
                                {
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Config Error: DPDS ID '" + datapoint.DPDUID + "' update invalid. " + tagToChange.ErrorMessage, true);
                                }
                            }
                            else
                            {
                                _dataPointConfig[datapoint.DPDUID] = datapoint;
                            }
                            action = "updated";
                        }
                        else
                        {
                            /* old values not yet supported
                            if (e.entity.OldValues != null)
                            {
                                if (e.entityOldValues.DPDUID != affectedRow.DPDUID)
                                {
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the DPDUID are not permitted, update for DataPointDefinitionStructure " + affectedRowOldValues.DPDUID + " rejected");
                                    return;
                                }
                            }
                            else
                            {
                                // record not found (not due to a change in the ID) so do an insert instead
                                _dataPointConfig.Add(affectedRow.DPDUID, affectedRow);
                                action = "not found, adding it as a new DataPointDefinitionStructure";
                                changeType = ChangeType.Insert;
                                break;
                            }
                            */

                            // record not found, re-run this function with action set to insert instead
                            DataPointMonitorNotification("INSERT", datapoint);
                            return;
                        }


                        break;

                }

            }

            Globals.SystemManager.LogApplicationEvent(this, "", "DPDUID " + datapoint.DPDUID + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("DataPointDefinitionStructures"), datapoint.DPDUID);

        }

        protected void SourceConnectionMonitorNotification(string changeType, FDASourceConnection connection)
        {

            // check for nulls
            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                List<string> exceptions = new List<string> { "SCDetail02" };
                string[] nulls = FindNulls(connection, exceptions);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "SCUID " + connection.SCUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            string action = "";
            lock (_connectionsConfig)
            {
                switch (changeType)
                {
                    case "INSERT":
                        _connectionsConfig.Add(connection.SCUID, connection);
                        action = "added";
                        break;
                    case "UPDATE":
                        if (_connectionsConfig.ContainsKey(connection.SCUID))
                        {
                            _connectionsConfig[connection.SCUID] = connection;
                        }
                        else
                        {
                            /* old values not yet supported
                            if (e.entity.OldValues != null)
                            {
                                if (e.entity.OldValues.SCUID != affectedRow.SCUID)
                                {
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the SCUID are not permitted, update for connection " + affectedRowOldValues.SCUID + " rejected");
                                    return;
                                }
                            }
                            else
                            {
                                // record not found (not due to a change in the ID) so do an insert instead
                                _connectionsConfig.Add(affectedRow.SCUID, affectedRow);
                                action = "not found, adding it as a new connection";
                                changeType = ChangeType.Insert;
                                break;
                            }
                            */
                            // record not found (not due to a change in the ID) so do an insert instead
                            _connectionsConfig.Add(connection.SCUID, connection);
                            action = "not found, adding it as a new connection";
                            changeType = "INSERT";

                        }
                        action = "updated";
                        break;
                    case "DELETE":
                        _connectionsConfig.Remove(connection.SCUID);
                        action = "deleted";
                        break;
                }
            }
            Globals.SystemManager.LogApplicationEvent(this, "", "SCUID " + connection.SCUID + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDASourceConnections"), connection.SCUID);
        }

        protected void RequestGroupMonitorNotification(string changeType, FDADataBlockRequestGroup requestGroup)
        {

            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                // check for nulls
                string[] nulls = FindNulls(requestGroup);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "DRGUID " + requestGroup.DRGUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            string action = "";
            lock (_requestgroupConfig)
            {
                switch (changeType)
                {
                    case "INSERT":
                        _requestgroupConfig.Add(requestGroup.DRGUID, requestGroup);
                        action = "inserted";
                        break;

                    case "UPDATE":

                        if (_requestgroupConfig.ContainsKey(requestGroup.DRGUID))
                        {
                            _requestgroupConfig[requestGroup.DRGUID] = requestGroup;
                            action = "updated";
                        }
                        else
                        {
                            /* old values not yet supported
                            if (e.Entity.OldValues != null)
                            {
                                if (e.Entity.OldValues.DRGUID != affectedRow.DRGUID)
                                {
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the DRGUID are not permitted, update for FDADataBlockRequestGroup " + affectedRowOldValues.DRGUID + " rejected");
                                    return;
                                }
                            }
                            else
                            {
                                // record not found (not due to a change in the ID) so do an insert instead
                                _requestgroupConfig.Add(affectedRow.DRGUID, affectedRow);
                                action = "not found, adding it as a new FDADataBlockRequestGroup";
                                changeType = ChangeType.Insert;
                                break;

                            }
                            */
                            _requestgroupConfig.Add(requestGroup.DRGUID, requestGroup);
                            action = "not found, adding it as a new FDADataBlockRequestGroup";
                            changeType = "INSERT";
                        }


                        break;
                    case "DELETE":
                        if (_requestgroupConfig.ContainsKey(requestGroup.DRGUID))
                        {
                            _requestgroupConfig.Remove(requestGroup.DRGUID);
                            action = "deleted";
                        }
                        else
                        {
                            action = " was deleted from the database but was not found in the FDA, so no action was taken";
                        }
                        break;
                }
            }
            Globals.SystemManager.LogApplicationEvent(this, "", "DRGUID " + requestGroup.DRGUID + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDADataBlockRequestGroup"), requestGroup.DRGUID);

        }

        protected void SchedulerMonitorNotification(string changeType, FDARequestGroupScheduler sched)
        {

            string action = "";


            // check for nulls
            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                string[] nulls = FindNulls(sched);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "FRGSUID " + sched.FRGSUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;

                }
            }


            lock (_schedConfig)
            {
                switch (changeType)
                {
                    case "DELETE":
                        _schedConfig.Remove(sched.FRGSUID);
                        action = " deleted";
                        break;
                    case "INSERT":
                        List<FDATask> taskList;
                        sched.RequestGroups = RequestGroupListToRequestGroups(sched.FRGSUID, sched.Priority, sched.RequestGroupList, out taskList);
                        sched.Tasks = taskList;
                        _schedConfig.Add(sched.FRGSUID, sched);
                        action = " added";
                        break;

                    case "UPDATE":

                        if (_schedConfig.ContainsKey(sched.FRGSUID))
                        {
                            action = " updated";
                            lock (_schedConfig) { _schedConfig[sched.FRGSUID] = sched; }
                            taskList = null;
                            sched.RequestGroups = RequestGroupListToRequestGroups(sched.FRGSUID, sched.Priority, sched.RequestGroupList, out taskList);
                            sched.Tasks = taskList;
                        }
                        else
                        {
                            /* old values not yet supported
                            if (e.entity.OldValues != null)
                            {
                                if (e.entity.OldValues.FRGSUID != affectedRow.FRGSUID)
                                {
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the FRGSUID are not permitted, update for FDARequestGroupScheduler " + e.entity.OldValues.FRGSUID + " rejected");
                                    return;
                                }
                            }
                            else
                            {
                                // record not found (not due to a change in the ID) so do an insert instead
                                _schedConfig.Add(affectedRow.FRGSUID, affectedRow);
                                action = "not found, adding it as a new FDARequestGroupScheduler";
                                changeType = ChangeType.Insert;
                                break;
                            }
                            */
                            _schedConfig.Add(sched.FRGSUID, sched);
                            action = "not found, adding it as a new FDARequestGroupScheduler";
                            changeType = "INSERT";

                        }
                        break;
                }
            }

            Globals.SystemManager.LogApplicationEvent(this, "", "FRGSUID " + sched.FRGSUID + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDARequestGroupScheduler"), sched.FRGSUID);

        }

        protected void DemandMonitorNotification(string changeType,FDARequestGroupDemand demand)
        {

            try
            {
                if (changeType == "INSERT" || changeType == "UPDATE")
                {
                    RequestGroup newRequestGroup;
                    if (demand.FRGDEnabled)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "", "Demand Request Received: " + demand.Description);

                        // check for nulls
                        string[] nulls = FindNulls(demand);
                        if (nulls.Length > 0)
                        {
                            Globals.SystemManager.LogApplicationEvent(this, "", "FRGDUID " + demand.FRGDUID + " rejected, null values in field(s) " + string.Join(",", nulls));
                            return;
                        }

                        List<RequestGroup> demandedGroupList = new List<RequestGroup>();

                        string[] requestGroupConfigs = demand.RequestGroupList.Split('|');

                        Guid requestGroupID;
                        foreach (string groupConfig in requestGroupConfigs)
                        {
                            if (groupConfig.StartsWith("^"))
                            {
                                // task reference
                                if (!Guid.TryParse(groupConfig.Remove(0, 1), out Guid taskID))
                                {
                                    // bad Guid
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Invalid Task ID in demand " + demand.FRGDUID + ", the correct format is xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", true);
                                    continue;
                                }

                                FDATask task = GetTask(taskID);
                                if (task != null)
                                {
                                    switch (task.task_type.ToUpper())
                                    {
                                        case "DATAACQ":
                                            newRequestGroup = RequestGroupFromConfigString("Demand " + demand.FRGDUID, task.task_details, demand.Priority, demand.CommsLogEnabled);
                                            if (newRequestGroup != null)
                                            {
                                                demandedGroupList.Add(newRequestGroup);
                                                requestGroupID = Guid.Parse(task.task_details.Split(':')[0]);
                                                newRequestGroup.Protocol = GetRequestGroup(requestGroupID).DPSType;
                                            }
                                            break;
                                        case "CALCCOMMSSTATS":
                                            string error;
                                            List<object> calcParams = ParseStatsCalcParams(task.task_details, out error);
                                            if (error == "" && calcParams != null)
                                            {
                                                Globals.SystemManager.LogApplicationEvent(this, "", "Calculating communications statistics (requested by demand " + demand.FRGDUID);
                                                RemoteQueryManager.DoCommsStats(calcParams);// fromTime, toTime, altOutput, connectionFilter, deviceFilter,description,altOutput);
                                            }
                                            else
                                            {
                                                Globals.SystemManager.LogApplicationEvent(this, "", "invalid task_details in task " + task.TASK_ID + ": " + error + ". The task will not be executed");
                                            }
                                            break;
                                        default:
                                            Globals.SystemManager.LogApplicationEvent(this, "", "FDA Task id " + taskID + ", requested by demand " + demand.FRGDUID + " has an unrecognized task type '" + task.task_type + "'. The task will not be executed");
                                            continue;
                                    }
                                }
                                else
                                {
                                    // task with this ID not found, check the schedulers table
                                    FDARequestGroupScheduler scheduler = GetSched(taskID);
                                    if (scheduler != null)
                                    {
                                        ForceScheduleExecution?.Invoke(this, new ForceScheduleEventArgs(scheduler.FRGSUID));
                                        return; 
                                    }
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Task " + taskID + " was not found. Task was requested by demand " + demand.FRGDUID, true);
                                }
                            }
                            else
                            {
                                newRequestGroup = RequestGroupFromConfigString("Demand " + demand.FRGDUID, groupConfig, demand.Priority, demand.CommsLogEnabled); // if the demand object has comms logging enabled, override the comms log setting on the group

                                if (newRequestGroup != null)
                                {
                                    demandedGroupList.Add(newRequestGroup);
                                    requestGroupID = Guid.Parse(groupConfig.Split(':')[0]);
                                    newRequestGroup.Protocol = GetRequestGroup(requestGroupID).DPSType;
                                }
                            }
                        }

                        if (demandedGroupList.Count > 0)
                        {
                            RaiseDemandRequestEvent(demand, demandedGroupList);
                            
                            if (demand.DestroyDRG)
                                DeleteRequestGroup(demandedGroupList);

                            if (demand.DestroyFRGD)
                                DeleteDemand(demand.FRGDUID);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while parsing demand request " + demand.FRGDUID);
            }
        }


        public bool TestDatabaseConnection()
        {
            Globals.SystemManager.LogApplicationEvent(this, "", "Testing Database connection");

            if (!TestConnection())
            {
                Globals.SystemManager.LogApplicationEvent(this,"", "Unable to connect to the database");
                DBStatus = false;
                return false;
            }

            DBStatus = true;

            _PreviousDBStartTime = GetDBStartTime();


            return PreReqCheck();
        }

        public virtual void Dispose()
        {
            // log any data that's sitting in the caches before shutting down
            Globals.SystemManager.LogApplicationEvent(this, "", "Flushing all cached data");

            if (_cacheManager != null)
            {
                List<string> batches = _cacheManager.FlushAll();
                foreach (string batch in batches)
                {
                    ExecuteNonQuery(batch);
                }
            }
            _cacheManager.Dispose();

            Stopwatch stopwatch = new Stopwatch();
            // wait for datawriter thread to finish (up to 10 seconds)
            if (_dataWriter != null)
            {
                if (_dataWriter.IsBusy)
                    Globals.SystemManager.LogApplicationEvent(this, "", "Waiting for data writer thread to finish and exit");

                stopwatch.Start();
                while (_dataWriter.IsBusy && stopwatch.Elapsed.Seconds < 10)
                    Thread.Sleep(50);
                stopwatch.Stop();
                _dataWriter.Dispose();
            }

            // wait for alarms and events writing thread to finish (up to 10 seconds)
            if (_alarmsEventsWriter != null)
            {
                if (_alarmsEventsWriter.IsBusy)
                    Globals.SystemManager.LogApplicationEvent(this, "", "Waiting for Alarms and Events writer thread to finish and exit");

                stopwatch.Reset();
                stopwatch.Start();
                while (_alarmsEventsWriter.IsBusy && stopwatch.Elapsed.Seconds < 10)
                    Thread.Sleep(50);
                stopwatch.Stop();
                _alarmsEventsWriter.Dispose();
            }

            stopwatch = null;


            lock (_schedConfig) { _schedConfig.Clear(); }
            lock (_requestgroupConfig) { _requestgroupConfig.Clear(); }
            lock (_dataPointConfig) { _dataPointConfig.Clear(); }
            lock (_connectionsConfig) { _connectionsConfig.Clear(); }
        }
    

        //protected virtual void Dispose(bool disposing)
        //{
        //    if (!disposedValue)
        //    {
        //        if (disposing)
        //        {

        //            // log any data that's sitting in the caches before shutting down
        //            Globals.SystemManager.LogApplicationEvent(this, "", "Flushing all cached data");

        //            if (_cacheManager != null)
        //            {
        //                List<string> batches = _cacheManager.FlushAll();
        //                foreach (string batch in batches)
        //                {
        //                    ExecuteNonQuery(batch);
        //                }
        //            }
        //            _cacheManager.Dispose();

        //            Stopwatch stopwatch = new Stopwatch();
        //            // wait for datawriter thread to finish (up to 10 seconds)
        //            if (_dataWriter != null)
        //            {
        //                if (_dataWriter.IsBusy)
        //                    Globals.SystemManager.LogApplicationEvent(this, "", "Waiting for data writer thread to finish and exit");

        //                stopwatch.Start();
        //                while (_dataWriter.IsBusy && stopwatch.Elapsed.Seconds < 10)
        //                    Thread.Sleep(50);
        //                stopwatch.Stop();
        //                _dataWriter.Dispose();
        //            }

        //            // wait for alarms and events writing thread to finish (up to 10 seconds)
        //            if (_alarmsEventsWriter != null)
        //            {
        //                if (_alarmsEventsWriter.IsBusy)
        //                    Globals.SystemManager.LogApplicationEvent(this, "", "Waiting for Alarms and Events writer thread to finish and exit");

        //                stopwatch.Reset();
        //                stopwatch.Start();
        //                while (_alarmsEventsWriter.IsBusy && stopwatch.Elapsed.Seconds < 10)
        //                    Thread.Sleep(50);
        //                stopwatch.Stop();
        //                _alarmsEventsWriter.Dispose();
        //            }

        //            stopwatch = null;


        //            lock (_schedConfig) { _schedConfig.Clear(); }
        //            lock (_requestgroupConfig) { _requestgroupConfig.Clear(); }
        //            lock (_dataPointConfig) { _dataPointConfig.Clear(); }
        //            lock (_connectionsConfig) { _connectionsConfig.Clear(); }
        //        }

        //        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        //        // TODO: set large fields to null
        //        disposedValue = true;
        //    }
        //}

        //// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        //// ~DBManagerBase()
        //// {
        ////     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        ////     Dispose(disposing: false);
        //// }

        //public void Dispose()
        //{
        //    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //    Dispose(disposing: true);
        //    GC.SuppressFinalize(this);
        //}

    }
}
