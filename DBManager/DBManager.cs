using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using TableDependency.SqlClient;
using TableDependency.SqlClient.Base;
using TableDependency.SqlClient.Base.Enums;
using Common;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.Globalization;
using Npgsql;

namespace FDA
{
    public class DBManager : IDisposable
    {
        #region property and event definitions 

        private Queue<DataRequest> _writeQueue;
        private Queue<List<AlarmEventRecord>> _alarmEventQueue;
        private BackgroundWorker _dataWriter;
        private BackgroundWorker _alarmsEventsWriter;

        private CacheManager _cacheManager;

        private bool _DBStatus = false;
        private DateTime _PreviousDBStartTime = DateTime.MinValue;

        private Dictionary<Guid, FDARequestGroupScheduler> _schedConfig;
        private Dictionary<Guid, FDADataBlockRequestGroup> _requestgroupConfig;
        private Dictionary<Guid, FDADataPointDefinitionStructure> _dataPointConfig;
        private Dictionary<Guid, FDASourceConnection> _connectionsConfig;
        private Dictionary<Guid, FDADevice> _deviceConfig;
        private Dictionary<Guid, FDATask> _taskConfig;


        private Timer _keepAliveTimer;
        private TimeSpan _DBKeepAliveRate = new TimeSpan(0, 0, 3);
        private Stopwatch _DBDownTimer = new Stopwatch();
        private readonly TimeSpan _databaseDownNotificationLimit = new TimeSpan(0, 30, 0); // in days (default to 30 minutes)
        private bool _devicesTableExists = false;
        private bool _tasksTableExists;
        public RemoteQueryManager RemoteQueryManager;

        private readonly Timer _logTrimTimer;
        //private double _commsLogMaxDays = 1;
        //private double _eventLogMaxDays = 1;

        /*
        SqlTableDependency<FDARequestGroupScheduler> _schedMonitor;
        SqlTableDependency<FDARequestGroupDemand> _demandMonitor;
        SqlTableDependency<FDADataBlockRequestGroup> _requestGroupDefMonitor;
        SqlTableDependency<FDADataPointDefinitionStructure> _dataPointDefMonitor;
        SqlTableDependency<FDASourceConnections> _connectionDefMonitor;
        SqlTableDependency<FDADevice> _deviceDefMonitor;
        SqlTableDependency<FDATask> _taskDefMonitor;
        */

        PostgreSQLListener<FDARequestGroupScheduler> _schedMonitor;
        PostgreSQLListener<FDARequestGroupDemand> _demandMonitor;
        PostgreSQLListener<FDADataBlockRequestGroup> _requestGroupDefMonitor;
        PostgreSQLListener<FDADataPointDefinitionStructure> _dataPointDefMonitor;
        PostgreSQLListener<FDASourceConnection> _connectionDefMonitor;
        PostgreSQLListener<FDADevice> _deviceDefMonitor;
        PostgreSQLListener<FDATask> _taskDefMonitor;

        public delegate void ConfigChangeHandler(object sender, ConfigEventArgs e);
        public event ConfigChangeHandler ConfigChange;

        public delegate void DemandRequestHandler(object sender, DemandEventArgs e);
        public event DemandRequestHandler DemandRequest;

       // public double CommsLogMaxDays { get => _commsLogMaxDays; set => _commsLogMaxDays = value; }
        //public double EventLogMaxDays { get => _eventLogMaxDays; set => _eventLogMaxDays = value; }

        public string ConnectionString { get; set; }

        #endregion  

        // constructor
        public DBManager(string connString)
        {
            ConnectionString = connString;
            _dataWriter = new BackgroundWorker();
            _dataWriter.DoWork += _dataWriter_DoWork;
            _writeQueue = new Queue<DataRequest>();

            // structures to hold the config info in memory
            // Dictionary structure allows superfast lookups
            _schedConfig = new Dictionary<Guid, FDARequestGroupScheduler>();
            _requestgroupConfig = new Dictionary<Guid, FDADataBlockRequestGroup>();
            _dataPointConfig = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            _connectionsConfig = new Dictionary<Guid, FDASourceConnection>();
            _deviceConfig = new Dictionary<Guid, FDADevice>();
            _taskConfig = new Dictionary<Guid, FDATask>();


            _keepAliveTimer = new Timer(DBCheckTimerTick, this, Timeout.Infinite, Timeout.Infinite);

            int batchLimit = 500;
            int batchTimeout = 500;

            if (Globals.SystemManager.GetAppConfig().ContainsKey("BatchInsertMaxRecords"))
                int.TryParse(Globals.SystemManager.GetAppConfig()["BatchInsertMaxRecords"].OptionValue, out batchLimit);

            if (Globals.SystemManager.GetAppConfig().ContainsKey("BatchInsertMaxTime"))
                int.TryParse(Globals.SystemManager.GetAppConfig()["BatchInsertMaxTime"].OptionValue, out batchTimeout);

            // cap write batches at 500 records
            if (batchLimit > 500)
                batchLimit = 500;

            _cacheManager = new CacheManager(batchLimit, batchTimeout);
            _cacheManager.CacheFlush += _cacheMananger_CacheFlush;

            // check if device table exists
            _devicesTableExists =((int)PG_ExecuteScalar("SELECT cast(count(1) as integer) from information_schema.tables where table_name = '" + Globals.SystemManager.GetTableName("fdadevices") + "';") > 0);

            // check if tasks table exists
            _tasksTableExists = ((int)PG_ExecuteScalar("SELECT cast(count(1) as integer) from information_schema.tables where table_name = '" + Globals.SystemManager.GetTableName("fdatasks") + "';") > 0);

            // start the log trimming timer

            //find the amount of time between now and midnight and set the timer to execute then, and every 24 hours from then
            DateTime currentDateTime = DateTime.Now;
            DateTime tomorrow = DateTime.Now.AddDays(1);
            DateTime timeOfFirstRun = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 0, 0, 0); 
            TimeSpan timeFromNowToFirstRun = timeOfFirstRun.Subtract(currentDateTime);


            _logTrimTimer = new Timer(LogTrimTimerTick, null, timeFromNowToFirstRun, TimeSpan.FromDays(1));
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
            string query = "DELETE FROM " + AppLog + " where Timestamp < (current_timestamp at time zone 'UTC' + INTERVAL '" + Globals.UTCOffset + " hours') - INTERVAL '" +  Globals.SystemManager.EventLogMaxDays + " days'";

            result = PG_ExecuteNonQuery(query);

            if (result >= 0)
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "Trimmed " + result + " rows from " + AppLog);
            }

            //SQL Server version
            //string query = "DELETE FROM " + CommsLog + " where Timestamp < DATEADD(HOUR," + Globals.UTCOffset + ",GETUTCDATE()) - " + Globals.SystemManager.CommsLogMaxDays;
            
            // PostgreSQL version
            query = "DELETE FROM " + CommsLog + " where TimestampUTC1 < (current_timestamp at time zone 'UTC' + INTERVAL '" + Globals.UTCOffset + " hours') - INTERVAL '" + Globals.SystemManager.CommsLogMaxDays + " days'";

            result = PG_ExecuteNonQuery(query);
            if (result >= 0)
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "Trimmed " + result + " rows from " + CommsLog);
            }
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

        private void _cacheMananger_CacheFlush(object sender, CacheManager.CacheFlushEventArgs e)
        {
            PG_ExecuteNonQuery(e.Query);

        }

        private void DBCheckTimerTick(Object o)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch
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
                    _DBStatus = false;
                    return;
                }

                // current status = true, previous status = false means a recovered connection
                if (_DBStatus == false)
                {
                    _DBDownTimer.Stop();
                    _DBDownTimer.Reset();

                    _DBStatus = true;
                    Globals.SystemManager.LogApplicationEvent(this, "DBManager", "Database connection restored, re-initializing the DBManager");
                    Initialize();
                    LoadConfig();
                    StartChangeMonitoring();
                    return;
                }


                DateTime DB_starttime = PG_GetDBStartTime();

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
        }

       
        private NpgsqlDataReader PG_ExecuteDataReader(string sql,ref NpgsqlConnection conn)
        {
            int maxRetries = 3;
            int retries = 0;
            NpgsqlDataReader reader;
        Retry:

            if (conn.State != System.Data.ConnectionState.Open)
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "PG_ExecuteDataReader() Failed to connect to database");
                    return null;
                }
            }

            using (NpgsqlCommand command = conn.CreateCommand())
            {
                command.CommandText = sql;

                try
                {
                    reader = command.ExecuteReader();
                }
                catch (Exception ex)
                {
                    retries++;
                    if (retries < maxRetries)
                    {
                        Thread.Sleep(250);
                        goto Retry;
                    }
                    else
                    {
                        Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "PG_ExecuteScalar() Failed to execute query after " + (maxRetries + 1) + " attempts.");
                        return null;
                    }
                }
                return reader;
            }

        }
          

        private int PG_ExecuteNonQuery(string sql)
        {
            int rowsaffected = -99;
            int retries = 0;
            int maxRetries = 3;
            Retry:

            using (NpgsqlConnection conn = new NpgsqlConnection(ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "PG_ExecuteNonQuery() Failed to connect to database");
                    return -99;
                }

                try
                {
                    using (NpgsqlCommand sqlCommand = conn.CreateCommand())
                    {
                        retries++;
                        sqlCommand.CommandText = sql;
                        rowsaffected = sqlCommand.ExecuteNonQuery();                       
                    }                   
                }
                catch (Exception ex)
                {
                    retries++;
                    if (retries < maxRetries)
                    {
                        Thread.Sleep(250);
                        goto Retry;
                    }
                    else
                    {
                        Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "PG_ExecuteScalar() Failed to execute query after " + (maxRetries + 1) + " attempts. Query = " + sql);
                        return -99;
                    }

                }
          
                conn.Close();
            }

            return rowsaffected;         
        }

     


        private object PG_ExecuteScalar(string sql)
        {
            int maxRetries = 3;
            int retries = 0;

            Retry:
            using (NpgsqlConnection conn = new NpgsqlConnection(ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "PG_ExecuteScalar() Failed to connect to database");
                    return null;
                }


                using (NpgsqlCommand sqlCommand = conn.CreateCommand())
                {
                    sqlCommand.CommandText = sql;

                    try
                    {
                        object result = sqlCommand.ExecuteScalar();
                        return result;
                    }
                    catch (Exception ex)
                    {
                        retries++;
                        if (retries < maxRetries)
                        {
                            Thread.Sleep(250);
                            goto Retry;
                        }
                        else
                        {
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "PG_ExecuteScalar() Failed to execute query after " + (maxRetries + 1) + " attempts.");
                            return null;
                        }
                    }

                }
               
            }
        }


        private DateTime PG_GetDBStartTime()
        {
            object scalarResult = PG_ExecuteScalar("SELECT pg_postmaster_start_time()");
            DateTime startTime = DateTime.MinValue;

            if (scalarResult != null)
                startTime = (DateTime)scalarResult;

            return startTime;

        }
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

            NpgsqlConnection conn = new NpgsqlConnection(ConnectionString);
              
            try
            {

                //sqlCommand.CommandText = query.ToString();
                using (NpgsqlDataReader reader = PG_ExecuteDataReader(query.ToString(), ref conn))
                {
                    Guid key;
                    while (reader.Read())
                    {
                        key = reader.GetGuid(reader.GetOrdinal("DPDUID"));
                        values[key] = reader.GetDouble(reader.GetOrdinal("Value"));
                    }
                }

            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to retrieve values to write to field device: query = " + query);
                conn.Close();
                conn.Dispose();
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



            conn.Close();
            conn.Dispose();
            return;
            
        }

        private void _dataWriter_DoWork(object sender, DoWorkEventArgs e)
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
                        // retries and error messages are handled in PG_ExecuteNonQuery()
                        int result = PG_ExecuteNonQuery(batch.ToString());
                        if (result > 0)
                        {
                            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "DBManager", "Group " + transactionToLog.GroupID + ", index " + transactionToLog.GroupIdxNumber + " successfully recorded in the database");
                        }
                        else
                        {
                            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "DBManager", "Group " + transactionToLog.GroupID + ", index " + transactionToLog.GroupIdxNumber + " failed to write to the database");
                        }

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


        public void Initialize()
        {

            Globals.SystemManager.LogApplicationEvent(this, "", "Initializing database manager");

            // start the remote query manager (handles queries from FDAManagers)
            Globals.SystemManager.LogApplicationEvent(this, "", "Starting Remote Query Manager");
            RemoteQueryManager = new RemoteQueryManager(ConnectionString);


            // get the last DB start time
            _PreviousDBStartTime = PG_GetDBStartTime();

            Globals.SystemManager.LogApplicationEvent(this, "", "FDA initialization complete");


            // PostgreSQL table monitors
            _schedMonitor = new PostgreSQLListener<FDARequestGroupScheduler>(ConnectionString, Globals.SystemManager.GetTableName("FDARequestGroupScheduler"));
            _demandMonitor = new PostgreSQLListener<FDARequestGroupDemand>(ConnectionString, Globals.SystemManager.GetTableName("FDARequestGroupDemand"));
            _requestGroupDefMonitor = new PostgreSQLListener<FDADataBlockRequestGroup>(ConnectionString, Globals.SystemManager.GetTableName("FDADataBlockRequestGroup"));
            _dataPointDefMonitor = new PostgreSQLListener<FDADataPointDefinitionStructure>(ConnectionString, Globals.SystemManager.GetTableName("DataPointDefinitionStructures"));
            _connectionDefMonitor = new PostgreSQLListener<FDASourceConnection>(ConnectionString, Globals.SystemManager.GetTableName("FDASourceConnections"));
            if (_devicesTableExists)
                _deviceDefMonitor = new PostgreSQLListener<FDADevice>(ConnectionString, Globals.SystemManager.GetTableName("FDADevices"));

            if (_tasksTableExists)
                _taskDefMonitor = new PostgreSQLListener<FDATask>(ConnectionString, Globals.SystemManager.GetTableName("FDATasks"));

            _demandMonitor.Notification += _demandMonitor_Notification;
            _schedMonitor.Notification += _schedMonitor_Notification;
            _requestGroupDefMonitor.Notification += _requestGroupDefMonitor_Notification;
            _connectionDefMonitor.Notification += _connectionDefMonitor_Notification;
            _dataPointDefMonitor.Notification += _dataPointDefMonitor_Notification;

            if (_deviceDefMonitor != null)
                _deviceDefMonitor.Notification += _deviceDefMonitor_Notification;
            if (_taskDefMonitor != null)
                _taskDefMonitor.Notification += _taskDefMonitor_Notification;

            _demandMonitor.Error += _PostgresSQLMonitor_Error;
            _schedMonitor.Error += _PostgresSQLMonitor_Error;
            _requestGroupDefMonitor.Error += _PostgresSQLMonitor_Error;
            _connectionDefMonitor.Error += _PostgresSQLMonitor_Error;
            _dataPointDefMonitor.Error += _PostgresSQLMonitor_Error;
            if (_deviceDefMonitor != null)
                _deviceDefMonitor.Error += _PostgresSQLMonitor_Error;
            if (_taskDefMonitor != null)
                _taskDefMonitor.Error += _PostgresSQLMonitor_Error;

            StartChangeMonitoring();

            _keepAliveTimer.Change(_DBKeepAliveRate, _DBKeepAliveRate);

            // SQlTableDependency objects (one per table), monitor for changes/additions/deletions and raise event when changes detects


            // clean up any triggers that my be hanging around from the last run (SQL Server only)
            //TriggerCleanup();


            // build SqlTableDependency Objects (SQL Server Only)
            /*
            try
            {
                _schedMonitor = new SqlTableDependency<FDARequestGroupScheduler>(ConnectionString, Globals.SystemManager.GetTableName("FDARequestGroupScheduler"));
                _demandMonitor = new SqlTableDependency<FDARequestGroupDemand>(ConnectionString, Globals.SystemManager.GetTableName("FDARequestGroupDemand"));
                _requestGroupDefMonitor = new SqlTableDependency<FDADataBlockRequestGroup>(ConnectionString, Globals.SystemManager.GetTableName("FDADataBlockRequestGroup"));
                _dataPointDefMonitor = new SqlTableDependency<FDADataPointDefinitionStructure>(ConnectionString, Globals.SystemManager.GetTableName("DataPointDefinitionStructures"));
                _connectionDefMonitor = new SqlTableDependency<FDASourceConnections>(ConnectionString, Globals.SystemManager.GetTableName("FDASourceConnections"));
                if (_devicesTableExists)
                    _deviceDefMonitor = new SqlTableDependency<FDADevice>(ConnectionString, Globals.SystemManager.GetTableName("FDADevices"));

                if (_tasksTableExists)
                    _taskDefMonitor = new SqlTableDependency<FDATask>(ConnectionString, Globals.SystemManager.GetTableName("FDATasks"));


                ----------------------   verbose messaging for the SQLTableDependency Objects ------------------------------------------
                 
                _schedMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                _schedMonitor.TraceListener = new TextWriterTraceListener(Console.Out);

                _demandMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                _demandMonitor.TraceListener = new TextWriterTraceListener(Console.Out);

                _requestGroupDefMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                _requestGroupDefMonitor.TraceListener = new TextWriterTraceListener(Console.Out);

                _dataPointDefMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                _dataPointDefMonitor.TraceListener = new TextWriterTraceListener(Console.Out);

                _connectionDefMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                _connectionDefMonitor.TraceListener = new TextWriterTraceListener(Console.Out);

                _deviceDefMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                _deviceDefMonitor.TraceListener = new TextWriterTraceListener(Console.Out);
                ---------------------------------------------------------------------------------------
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while initializing monitoring of table " +
                    Globals.SystemManager.GetTableName("FDARequestGroupScheduler") + ", or " +
                    Globals.SystemManager.GetTableName("FDARequestGroupDemand") + ", or " +
                    Globals.SystemManager.GetTableName("FDADataBlockRequestGroup") + ", or " +
                    Globals.SystemManager.GetTableName("DataPointDefinitionStructures") + ", or " +
                    Globals.SystemManager.GetTableName("FDASourceConnections") + ", or " +
                    Globals.SystemManager.GetTableName("FDADevices") + ", or " +
                    Globals.SystemManager.GetTableName("FDATasks") + ".");

                if (_schedMonitor != null)
                {
                    _schedMonitor.Stop();
                    _schedMonitor.Dispose();
                    _schedMonitor = null;

                }

                if (_demandMonitor != null)
                {
                    _demandMonitor.Stop();
                    _demandMonitor.Dispose();
                    _demandMonitor = null;
                }


                if (_requestGroupDefMonitor != null)
                {
                    _requestGroupDefMonitor.Stop();
                    _requestGroupDefMonitor.Dispose();
                    _requestGroupDefMonitor = null;

                }

                if (_dataPointDefMonitor != null)
                {
                    _dataPointDefMonitor.Stop();

                    _dataPointDefMonitor.Dispose();
                    _dataPointDefMonitor = null;
                }


                if (_connectionDefMonitor != null)
                {
                    _connectionDefMonitor.Stop();
                    _connectionDefMonitor.Dispose();
                    _connectionDefMonitor = null;
                }

                if (_deviceDefMonitor != null)
                {
                    _deviceDefMonitor.Stop();
                    _deviceDefMonitor.Dispose();
                    _deviceDefMonitor = null;
                }

                if (_taskDefMonitor != null)
                {
                    _taskDefMonitor.Stop();
                    _taskDefMonitor.Dispose();
                    _taskDefMonitor = null;
                }

                return;
            }
            

            // subscribe to events, so we'll be notified when something changes in the tables, or if something changes in the status of the monitor objects
            try
            {
                _demandMonitor.OnChanged += _DemandMonitor_OnChanged;
                _schedMonitor.OnChanged += _SchedMonitor_OnChanged;                
                _requestGroupDefMonitor.OnChanged += _requestGroupMonitor_OnChanged;
                _connectionDefMonitor.OnChanged += _connectionDefMonitor_OnChanged;
                _dataPointDefMonitor.OnChanged += _dataPointDefMonitor_OnChanged;
                if (_deviceDefMonitor != null)
                    _deviceDefMonitor.OnChanged += _deviceDefMonitor_OnChanged;
                if (_taskDefMonitor != null)
                    _taskDefMonitor.OnChanged += _taskDefMonitor_OnChanged;

                _schedMonitor.OnStatusChanged += SchedMonitor_OnStatusChanged;
                _demandMonitor.OnStatusChanged += DemandMonitor_OnStatusChanged;
                _requestGroupDefMonitor.OnStatusChanged += RequestGroupDefMonitor_OnStatusChanged;
                _connectionDefMonitor.OnStatusChanged += ConnectionDefMonitor_OnStatusChanged;
                _dataPointDefMonitor.OnStatusChanged += DataPointDefMonitor_OnStatusChanged;
                if (_deviceDefMonitor != null)
                    _deviceDefMonitor.OnStatusChanged += _deviceDefMonitor_OnStatusChanged;
                if (_taskDefMonitor != null)
                    _taskDefMonitor.OnStatusChanged += _taskDefMonitor_OnStatusChanged;

                _schedMonitor.OnError += SchedMonitor_OnError;
                _demandMonitor.OnError += DemandMonitor_OnError; ;
                _requestGroupDefMonitor.OnError += RequestGroupDefMonitor_OnError;
                _connectionDefMonitor.OnError += ConnectionDefMonitor_OnError;
                _dataPointDefMonitor.OnError += DataPointDefMonitor_OnError;
                if (_deviceDefMonitor != null)
                    _deviceDefMonitor.OnError += _deviceDefMonitor_OnError;
                if (_taskDefMonitor != null)
                    _taskDefMonitor.OnError += _taskDefMonitor_OnError;

            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while subscribing to 'table changed' events, some tables may not be monitored");
            }
            */

        }

        private void _PostgresSQLMonitor_Error(object sender,Exception e)
        {
            HandleTableMonitorError(e);
        }




        /* not needed, no triggers because SQlTableDependency doesn't support postgres
        void TriggerCleanup()
        {
            // get a list of triggers to be cleaned
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    // failed to connect to DB, exit the DB write routine
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to connect to the database");
                    return;
                }

                try
                {
                    using (SqlCommand sqlCommand = conn.CreateCommand())
                    {
                        string triggerDropSQL = string.Empty;
                        string triggerQuerySQL = "select T.name from sys.triggers T left join sys.objects O on T.parent_id = O.object_id ";
                        triggerQuerySQL += "where O.name in ('" + Globals.SystemManager.GetTableName("FDARequestGroupDemand") + "',";
                        triggerQuerySQL += "'" + Globals.SystemManager.GetTableName("FDARequestGroupScheduler") + "',";
                        triggerQuerySQL += "'" + Globals.SystemManager.GetTableName("FDASourceConnections") + "',";
                        triggerQuerySQL += "'" + Globals.SystemManager.GetTableName("FDADatablockRequestGroup") + "',";
                        triggerQuerySQL += "'" + Globals.SystemManager.GetTableName("DataPointDefinitionStructures") + "');";

                        sqlCommand.CommandText = triggerQuerySQL;

                        using (var sqlDataReader = sqlCommand.ExecuteReader())
                        {
                            string triggerName = "";
                            if (sqlDataReader.HasRows)
                            {
                                while (sqlDataReader.Read())
                                {
                                    triggerName = sqlDataReader.GetString(sqlDataReader.GetOrdinal("name"));
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Removing old trigger '" + triggerName + "'",false,true);
                                    triggerDropSQL += "drop trigger [" + triggerName + "];";
                                }
                            }
                        }

                        if (triggerDropSQL != string.Empty)
                        {
                            sqlCommand.CommandText = triggerDropSQL;
                            sqlCommand.ExecuteNonQuery();
                        }
                    }
                } catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while removing old triggers: " + ex.Message);
                    return;
                }
            }
        }
        */

        //private void ConnectionDefMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        //{
        //    if (_connectionDefMonitor != null)
        //    {
        //        _connectionDefMonitor.OnChanged -= _connectionDefMonitor_OnChanged;
        //        _connectionDefMonitor.OnStatusChanged -= ConnectionDefMonitor_OnStatusChanged;
        //        _connectionDefMonitor.OnError -= ConnectionDefMonitor_OnError;
        //        _connectionDefMonitor = null;
        //    }
        //    HandleTableMonitorError("ConnectionDef", e);
        //}


        //private void DataPointDefMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        //{
        //    if (_dataPointDefMonitor != null)
        //    {
        //        _dataPointDefMonitor.OnChanged -= _dataPointDefMonitor_OnChanged;
        //        _dataPointDefMonitor.OnStatusChanged -= DataPointDefMonitor_OnStatusChanged;
        //        _dataPointDefMonitor.OnError -= DataPointDefMonitor_OnError;
        //        _dataPointDefMonitor = null;
        //    }
        //    HandleTableMonitorError("DataPointDefMonitor", e);
        //}



        //private void RequestGroupDefMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        //{
        //    if (_requestGroupDefMonitor != null)
        //    {
        //        _requestGroupDefMonitor.OnChanged -= _requestGroupMonitor_OnChanged;
        //        _requestGroupDefMonitor.OnStatusChanged -= RequestGroupDefMonitor_OnStatusChanged;
        //        _requestGroupDefMonitor.OnError -= RequestGroupDefMonitor_OnError;
        //        _requestGroupDefMonitor = null;
        //    }
        //    HandleTableMonitorError("RequestGroupDefMonitor", e);

        //}


        //private void DemandMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        //{
        //    if (_demandMonitor != null)
        //    {
        //        _demandMonitor.OnChanged -= _DemandMonitor_OnChanged;
        //        _demandMonitor.OnStatusChanged -= DemandMonitor_OnStatusChanged;
        //        _demandMonitor.OnError -= DemandMonitor_OnError;
        //        _demandMonitor = null;
        //    }
        //    HandleTableMonitorError("DemandMonitor", e);
        //}


        //private void SchedMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        //{
        //    if (_schedMonitor != null)
        //    {
        //        _schedMonitor.OnChanged -= _SchedMonitor_OnChanged;
        //        _schedMonitor.OnStatusChanged -= SchedMonitor_OnStatusChanged;
        //        _schedMonitor.OnError -= SchedMonitor_OnError;
        //        _schedMonitor = null;
        //    }
        //    HandleTableMonitorError("SchedMonitor", e);
        //}


        //private void _deviceDefMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        //{
        //    if (_deviceDefMonitor != null)
        //    {
        //        _deviceDefMonitor.OnChanged -= _deviceDefMonitor_OnChanged;
        //        _deviceDefMonitor.OnStatusChanged -= _deviceDefMonitor_OnStatusChanged;
        //        _deviceDefMonitor.OnError -= _deviceDefMonitor_OnError;
        //        _deviceDefMonitor = null;
        //    }
        //    HandleTableMonitorError("DeviceMonitor", e);
        //}

        //private void _taskDefMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        //{
        //    if (_taskDefMonitor != null)
        //    {
        //        _taskDefMonitor.OnChanged -= _taskDefMonitor_OnChanged;
        //        _taskDefMonitor.OnStatusChanged -= _taskDefMonitor_OnStatusChanged;
        //        _taskDefMonitor.OnError -= _taskDefMonitor_OnError;
        //        _taskDefMonitor = null;
        //    }
        //    HandleTableMonitorError("TaskMonitor", e);
        //}




        public void HandleTableMonitorError(Exception e)
        {
            Globals.SystemManager.LogApplicationError(Globals.FDANow(), e, "Database error reported by table change monitoring object.  Error: " + e.Message);

            // enable the database downtime checking routine, this will repeatedly attempt to connect to the database, raise some of alert if  the downtime is too long, and re-initialize the database
            //manager when the connection is restored
            //if (!_databaseDownTimerActive)
            //{
            //    _databaseDownTimerActive = true;
            //    _databaseConnectionCheckTimer.Change(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5));
            //}
        }

        /*
        private void DemandMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("DemandMonitor", e);
        }


        private void ConnectionDefMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {

            LogMonitorStatusChangeEvent("ConnectionDefMonitor", e);
        }


        private void DataPointDefMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("DataPointDefMonitor", e);
        }


        private void RequestGroupDefMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("RequestGroupDefMonitor", e);
        }


        private void SchedMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("ScheduleMonitor", e);
        }

        private void _deviceDefMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("DeviceMonitor", e);
        }

        private void _taskDefMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("TaskMonitor", e);
        }



        private void LogMonitorStatusChangeEvent(string monitorName, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            if (e.Status != TableDependencyStatus.StopDueToError)
                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "SQLTableDependency", monitorName, "Status change: " + e.Status.ToString());
        }
        */


    
        public bool TestDatabaseConnection()
        {
           Globals.SystemManager.LogApplicationEvent(this, "", "Testing Database connection");

            using (NpgsqlConnection conn = new NpgsqlConnection(ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    // failed to connect
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while attempting to connect to the database");
                    _DBStatus = false;
                    return false;
                }

               _DBStatus = true;
                DateTime returnedDateTime = DateTime.MinValue;

                object scalarresult = PG_ExecuteScalar("SELECT pg_postmaster_start_time()");
                if (scalarresult != null)
                    returnedDateTime = (DateTime)scalarresult;
                
                 _PreviousDBStartTime = (DateTime)scalarresult;

                
            }

            Globals.SystemManager.LogApplicationEvent(this, "", "Successfully connected to the database", false, true);

         
            

            Globals.SystemManager.LogApplicationEvent(this, "", "Checking database pre-requisites", false, true);

            return PreReqCheck();
        }

        private bool PreReqCheck()
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
            result = PG_ExecuteScalar(query);
            if (result != null)
            {
                appLogexists = ((long)result > 0);
            }
           

            query = "select count(1) from INFORMATION_SCHEMA.TABLES where TABLE_NAME  = '" + Globals.SystemManager.GetTableName("commslog") + "';";
            result = PG_ExecuteScalar(query);
            if (result!=null)
            {
                commsLogExists = ((long)result > 0);
            }

            
            FDAdbname = "FDA";
            if (Globals.SystemManager.GetAppConfig().ContainsKey("FDADBName"))
                FDAdbname = Globals.SystemManager.GetAppConfig()["FDADBName"].OptionValue;
       

                
                /* not supported in postgres
                query = "SELECT name,is_broker_enabled FROM sys.databases WHERE name in ('" + FDAdbname + "','FDASystem')";
                sqlCommand.CommandText = query;
                string dbName;
                bool enabled;
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        dbName = sqlDataReader.GetString(sqlDataReader.GetOrdinal("name"));
                        enabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("is_broker_enabled"));
                        if (dbName == FDAdbname)
                            FDAServiceBrokerEnabled = enabled;
                        if (dbName == "FDASystem")
                            FDASystemServiceBrokerEnabled = enabled;
                    }
                }
            }
        }

        */
              

                /*
                if (!FDAServiceBrokerEnabled)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "Prequisite fail: Service broker not enabled on database '" + FDAdbname + "'");
                }

                if (!FDASystemServiceBrokerEnabled)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "Prequisite fail: Service broker not enabled on database 'FDASystem'");
                }
                */

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
            


            return appLogexists && commsLogExists;// && FDASystemServiceBrokerEnabled && FDAServiceBrokerEnabled;
        }

        private bool BuildAppLogTable(string tableName)
        {
            string sql="CREATE TABLE applog(fdaexecutionid uuid NOT NULL,\"timestamp\" timestamp(6) NOT NULL,eventtype varchar(10) NOT NULL,objecttype varchar(100) NOT NULL,objectname varchar(500) NULL,description text NULL,errorcode varchar(10) NULL,stacktrace text NULL);";
            int result = PG_ExecuteNonQuery(sql);
            return (result == -1);
        }

        private bool BuildCommsLogTable(string tableName)
        {
            string sql = "CREATE TABLE commslog(fdaexecutionid uuid NOT NULL,connectionid uuid NOT NULL,deviceid uuid NULL,deviceaddress varchar(100) NOT NULL,timestamputc1 timestamp(6) NOT NULL,timestamputc2 timestamp(6) NULL,attempt int NULL, transstatus bit NULL,transcode int NULL,elapsedperiod bigint NULL,dbrguid uuid NULL,dbrgidx varchar(30) NULL,dbrgsize int NULL,details01 varchar(1000) NULL,txsize int NULL,details02 varchar(1000) NULL,rxsize int NULL,protocol varchar(20) NULL,protocolnote varchar(4000) NULL,applicationmessage varchar(8000) NULL)";
            int result = PG_ExecuteNonQuery(sql);
            return (result == -1);
        }

        private bool HasColumn(SqlDataReader dr,string column)
        {
            for (int i=0; i<dr.FieldCount; i++)
            {
                if (dr.GetName(i) == column)
                    return true;
            }
            return false;
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

            NpgsqlConnection conn = new NpgsqlConnection(ConnectionString);
           
            try
            {
                conn.Open();
            }
            catch (Exception ex)
            {
                // failed to connect
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Unable to connect to the database");
                return false;
            }



            string tableName = "";


            //bool MQTTColumnExists;
            string ID; // a temporary place to store the ID (as a string) of whatever we're trying to parse, so we can include it in the error message if parsing fails 
            string query = "";

            // ***********************    load connections ******************************************
            tableName = Globals.SystemManager.GetTableName("FDASourceConnections");
            query = "select * from " + tableName;
           
            //sqlCommand.CommandText = query;
            using (NpgsqlDataReader reader = PG_ExecuteDataReader(query,ref conn))
            {
                //MQTTColumnExists = HasColumn(sqlDataReader, "MQTTEnabled");
                FDASourceConnection newConnConfig;
                while (reader.Read())
                {
                    ID = "(unknown)";
                    try
                    {
                        ID = reader.GetGuid(reader.GetOrdinal("SCUID")).ToString();
                        newConnConfig = new FDASourceConnection()
                        {
                            SCUID = reader.GetGuid(reader.GetOrdinal("SCUID")),
                            SCType = reader.GetString(reader.GetOrdinal("SCType")),
                            SCDetail01 = reader.GetString(reader.GetOrdinal("SCDetail01")),
                            SCDetail02 = reader.GetString(reader.GetOrdinal("SCDetail02")),
                            Description = reader.GetString(reader.GetOrdinal("Description")),
                            RequestRetryDelay = reader.GetInt32(reader.GetOrdinal("RequestRetryDelay")),
                            SocketConnectionAttemptTimeout = reader.GetInt32(reader.GetOrdinal("SocketConnectionAttemptTimeout")),
                            MaxSocketConnectionAttempts = reader.GetInt32(reader.GetOrdinal("MaxSocketConnectionAttempts")),
                            SocketConnectionRetryDelay = reader.GetInt32(reader.GetOrdinal("SocketConnectionRetryDelay")),
                            PostConnectionCommsDelay = reader.GetInt32(reader.GetOrdinal("PostConnectionCommsDelay")),
                            InterRequestDelay = reader.GetInt32(reader.GetOrdinal("InterRequestDelay")),
                            MaxRequestAttempts = reader.GetInt32(reader.GetOrdinal("maxRequestAttempts")),
                            RequestResponseTimeout = reader.GetInt32(reader.GetOrdinal("RequestResponseTimeout")),
                            ConnectionEnabled = reader.GetBoolean(reader.GetOrdinal("ConnectionEnabled")),
                            CommunicationsEnabled = reader.GetBoolean(reader.GetOrdinal("CommunicationsEnabled")),
                            CommsLogEnabled = reader.GetBoolean(reader.GetOrdinal("CommsLogEnabled")),
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
            }
                    
            
                  
                
            try
            {

                // load datapoint (tag) definitions
                DateTime currentTime = Globals.FDANow();
                tableName = Globals.SystemManager.GetTableName("DataPointDefinitionStructures");
                                  
                query = "select *," + 
                        "case when backfill_enabled is NULL then cast(0 as bit) else backfill_enabled END as nullsafe_backfill_enabled," +
                        "case when backfill_data_id is NULL then - 1 else backfill_data_id END as nullsafe_backfill_data_id," +
                        "case when backfill_data_structure_type is NULL then 0 else backfill_data_structure_type END as nullsafe_backfill_data_structure_type," +
                        "case when backfill_data_lapse_limit is NULL then 60 else backfill_data_lapse_limit END as nullsafe_backfill_data_lapse_limit," +
                        "case when backfill_data_interval is NULL then 1 else backfill_data_interval END as nullsafe_backfill_data_interval " +
                        "from DataPointDefinitionStructures;";
                //query = "select *, isnull(backfill_enabled,0) as nullsafe_backfill_enabled, isnull(backfill_data_id,-1) as nullsafe_backfill_data_id,isnull(backfill_data_structure_type,0) as nullsafe_backfill_data_structure_type,isnull(backfill_data_lapse_limit,60) as nullsafe_backfill_data_lapse_limit,isnull(backfill_data_interval,1) as nullsafe_backfill_data_interval from "
                //        + tableName + ";";
                FDADataPointDefinitionStructure newTag;

                //sqlCommand.CommandText = query;
                using (NpgsqlDataReader sqlDataReader = PG_ExecuteDataReader(query, ref conn))
                {
                    //MQTTColumnExists = HasColumn(sqlDataReader, "MQTTEnabled");
                    while (sqlDataReader.Read())
                    {
                        ID = "(unknown)";
                        try
                        {
                            ID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID")).ToString();
                            newTag = new FDADataPointDefinitionStructure()
                            {
                                DPDUID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID")),
                                DPDSEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("DPDSEnabled")),
                                DPSType = sqlDataReader.GetString(sqlDataReader.GetOrdinal("DPSType")),
                                read_scaling = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("read_scaling")),
                                read_scale_raw_low = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("read_scale_raw_low")),
                                read_scale_raw_high = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("read_scale_raw_high")),
                                read_scale_eu_low = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("read_scale_eu_low")),
                                read_scale_eu_high = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("read_scale_eu_high")),
                                write_scaling = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("write_scaling")),
                                write_scale_raw_low = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("write_scale_raw_low")),
                                write_scale_raw_high = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("write_scale_raw_high")),
                                write_scale_eu_low = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("write_scale_eu_low")),
                                write_scale_eu_high = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("write_scale_eu_high")),
                                backfill_enabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("nullsafe_backfill_enabled")),
                                backfill_data_ID = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("nullsafe_backfill_data_id")),
                                // Feb 12, 2020: ignore the last read columns from the database, default to 0 value at current timestamp
                                LastReadDataValue = 0.0,// sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("LastReadDataValue")),
                                LastReadQuality = 32,
                                LastReadDataTimestamp = currentTime,// sqlDataReader.GetDateTime(sqlDataReader.GetOrdinal("LastReadDataTimestamp")),
                                backfill_data_structure_type = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("nullsafe_backfill_data_structure_type")),
                                backfill_data_lapse_limit = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("nullsafe_backfill_data_lapse_limit")),
                                backfill_data_interval = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("nullsafe_backfill_data_interval")),
                                //MQTTEnabled = false
                            };

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
                }
                
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query of table " + tableName + " failed: query = " + query);
            }

            // new Mar 9, 2020  load last values from the FDALastDataValues table and update the DataPointDefinitionStructure objects in memory
            try
            {
                Guid DPDUID;
                tableName = Globals.SystemManager.GetTableName("FDALastDataValues");
                query = "select DPDUID,value,timestamp,quality from " + tableName + ";";

                //sqlCommand.CommandText = query;
                using (NpgsqlDataReader sqlDataReader = PG_ExecuteDataReader(query, ref conn))
                {
                    while (sqlDataReader.Read())
                    {
                        ID = "(unknown)";
                        try
                        {
                            ID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID")).ToString();
                            lock (_dataPointConfig)
                            {
                                DPDUID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID"));
                                if (_dataPointConfig.ContainsKey(DPDUID))
                                {
                                    _dataPointConfig[DPDUID].LastReadDataValue = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("value"));
                                    _dataPointConfig[DPDUID].LastReadDataTimestamp = sqlDataReader.GetDateTime(sqlDataReader.GetOrdinal("timestamp"));
                                    _dataPointConfig[DPDUID].LastReadQuality = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("quality"));
                                }
                            }
                        }
                        catch
                        {
                            Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - DPDS ID '" + ID + "' rejected", true);
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query of table " + tableName + " failed: query = " + query);
            }


            if (_devicesTableExists)
            {
                try
                {
                    tableName = Globals.SystemManager.GetTableName("FDADevices");
                    query = "SELECT * from " + tableName + ";";

                    //sqlCommand.CommandText = query;
                    using (NpgsqlDataReader sqlDataReader = PG_ExecuteDataReader(query, ref conn))
                    {
                        while (sqlDataReader.Read())
                        {
                            ID = "(unknown)";
                            try
                            {
                                ID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("device_id")).ToString();
                                lock (_deviceConfig)
                                {
                                    _deviceConfig.Add(sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("device_id")),
                                        new FDADevice()
                                        {
                                            device_id = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("device_id")),
                                            request_timeout = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("request_timeout")),
                                            max_request_attempts = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("max_request_attempts")),
                                            inter_request_delay = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("inter_request_delay")),
                                            request_retry_delay = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("request_retry_delay")),
                                        });
                                }
                            }
                            catch
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - Device ID '" + ID + "' rejected", true);
                            }
                        }
                    }
                    

                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query of table " + tableName + " failed: query = " + query);
                }
            }

            if (_tasksTableExists)
            {
                try
                {
                    tableName = Globals.SystemManager.GetTableName("FDATasks");
                    query = "SELECT * from " + tableName + ";";

                    //sqlCommand.CommandText = query;
                    using (NpgsqlDataReader sqlDataReader = PG_ExecuteDataReader(query, ref conn))
                    {
                        while (sqlDataReader.Read())
                        {
                            ID = "(unknown)";
                            try
                            {
                                ID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("task_id")).ToString();
                                lock (_taskConfig)
                                {
                                    _taskConfig.Add(sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("task_id")),
                                        new FDATask()
                                        {
                                            task_id = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("task_id")),
                                            task_type = sqlDataReader.GetString(sqlDataReader.GetOrdinal("task_type")),
                                            task_details = sqlDataReader.GetString(sqlDataReader.GetOrdinal("task_details"))
                                        });
                                }
                            }
                            catch
                            {
                                Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - Task ID '" + ID + "' rejected", true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query of table " + tableName + " failed: query = " + query);
                }
            }

            try
            {
                // load Datablock Request Groups (groups of individual requests)
                tableName = Globals.SystemManager.GetTableName("FDADataBlockRequestGroup");
                query = "SELECT DRGUID,Description,DRGEnabled,DPSType,DataPointBlockRequestListVals,CommsLogEnabled from " + tableName + ";";

                //sqlCommand.CommandText = query;
                using (NpgsqlDataReader sqlDataReader = PG_ExecuteDataReader(query, ref conn))
                {
                    while (sqlDataReader.Read())
                    {
                        ID = "(unknown)";
                        try
                        {
                            ID = sqlDataReader.GetGuid(0).ToString();
                            //if (sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("DRGEnabled")))
                            // {
                            lock (_requestgroupConfig)
                            {
                                _requestgroupConfig.Add(sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DRGUID")),
                                    new FDADataBlockRequestGroup()
                                    {
                                        DRGUID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DRGUID")),
                                        Description = sqlDataReader.GetString(sqlDataReader.GetOrdinal("Description")),
                                        DRGEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("DRGEnabled")),
                                        DPSType = sqlDataReader.GetString(sqlDataReader.GetOrdinal("DPSType")),
                                        DataPointBlockRequestListVals = sqlDataReader.GetString(sqlDataReader.GetOrdinal("DataPointBlockRequestListVals")),
                                        CommsLogEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("CommsLogEnabled"))
                                    });
                            }
                            //}
                        }
                        catch
                        {
                            Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - DBRG ID '" + ID + "' rejected", true);
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query of table " + tableName + " failed: query = " + query);
            }

            try
            {
                // load Schedules
                tableName = Globals.SystemManager.GetTableName("FDARequestGroupScheduler");
                query = "SELECT FRGSUID,Description,FRGSEnabled,FRGSType,RealTimeRate,Year,Month,Day,Hour,Minute,Second,Priority,RequestGroupList from " + tableName + ";";

              

                // sqlCommand.CommandText = query;
                using (NpgsqlDataReader sqlDataReader = PG_ExecuteDataReader(query,ref conn))
                {
                    while (sqlDataReader.Read())
                    {
                        ID = "(unknown)";

                        try
                        {
                            ID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("FRGSUID")).ToString();
                            FDARequestGroupScheduler scheduler = new FDARequestGroupScheduler()
                            {
                                FRGSUID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("FRGSUID")),
                                Description = sqlDataReader.GetString(sqlDataReader.GetOrdinal("Description")),
                                FRGSEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("FRGSEnabled")),
                                FRGSType = sqlDataReader.GetString(sqlDataReader.GetOrdinal("FRGSType")),
                                RealTimeRate = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("RealTimeRate")),
                                Year = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("Year")),
                                Month = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("Month")),
                                Day = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("Day")),
                                Hour = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("Hour")),
                                Minute = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("Minute")),
                                Second = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("Second")),
                                Priority = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("Priority")),
                                RequestGroupList = sqlDataReader.GetString(sqlDataReader.GetOrdinal("RequestGroupList")),  // requestgroupID:connectionID:DestinationID | requestgroupID:connectionID:DestinationID | .....
                            };
                            if (scheduler.Year == 0) scheduler.Year = 1;
                            if (scheduler.Month == 0) scheduler.Month = 1;
                            if (scheduler.Day == 0) scheduler.Day = 1;

                            lock (_schedConfig) { _schedConfig.Add(scheduler.FRGSUID, scheduler); }
                        }
                        catch
                        {
                            Globals.SystemManager.LogApplicationEvent(this, "", "FDA Start, Config Error - FRGS ID '" + ID + "' rejected", true);
                        }
                    }
                }
                
                conn.Close();
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query of table " + tableName + " failed: query = " + query);
                return false;
            }
            
            conn.Close();
            conn.Dispose();


            // after loading the Schedule configs, turn the requestgroup strings in the schedulers into actual RequestGroup objects, ready to be passed to a connection manager          
            List<RequestGroup> requestGroups;
            List<FDATask> tasks;
            lock (_schedConfig)
            {
                foreach (FDARequestGroupScheduler sched in _schedConfig.Values)
                {
                    requestGroups = RequestGroupListToRequestGroups(sched.FRGSUID, sched.Priority, sched.RequestGroupList, out tasks);
                    sched.RequestGroups.AddRange(requestGroups);
                    sched.Tasks.AddRange(tasks);
                }
            }
            return true;
        }

        public List<object> ParseStatsCalcParams(string paramstring,out string error)
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

                uint minutesBack;
                if (uint.TryParse(calcParams[1],out minutesBack))
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

        private List<RequestGroup> RequestGroupListToRequestGroups(Guid requestorID, int priority, string requestGroupString,out List<FDATask> otherTasks)
        {
            List<RequestGroup> requestGroups = new List<RequestGroup>();
            string[] requestgroupconfigs;
            RequestGroup newRequestGroup;
            requestgroupconfigs = requestGroupString.Split('|');
            Guid requestGroupID;
            otherTasks = new List<FDATask>();
            
            Guid taskID;
            foreach (string groupconfig in requestgroupconfigs)
            {
                if (groupconfig.StartsWith("^"))
                {
                    if (!Guid.TryParse(groupconfig.Remove(0, 1),out taskID))
                    {
                        Globals.SystemManager.LogApplicationEvent(this,"","Invalid Task ID in schedule " + requestorID + ", the correct format is xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",true);
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
                        Globals.SystemManager.LogApplicationEvent(this, "", "Task " + taskID + " was not found. Task was requested by schedule " + requestorID,true);
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

        #endregion

        #region configuration change monitoring


        // start monitoring for config changes
        public void StartChangeMonitoring()
        {
            try
            {
                _demandMonitor?.StartListening();
                _schedMonitor?.StartListening();
                _requestGroupDefMonitor?.StartListening();
                _connectionDefMonitor?.StartListening();
                _dataPointDefMonitor?.StartListening();
                if (_devicesTableExists)
                    _deviceDefMonitor?.StartListening();
                if (_tasksTableExists)
                    _taskDefMonitor?.StartListening();
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error when starting Table Change Monitoring objects");
            }

            
            // no need for this message now, each monitor object reports it's own status when it changes
            // Globals.SystemManager.LogApplicationEvent(this, "", "Database change monitoring started");

        }

        //pause monitoring for config changes
        public void PauseChangeMonitoring()
        {
            _demandMonitor?.StopListening();
            _schedMonitor?.StopListening();
            _requestGroupDefMonitor?.StopListening();
            _connectionDefMonitor?.StopListening();
            _dataPointDefMonitor?.StopListening();
            if (_devicesTableExists)
                _deviceDefMonitor?.StopListening();
            if (_tasksTableExists)
                _deviceDefMonitor?.StopListening();
            Globals.SystemManager.LogApplicationEvent(this, "", "Database change monitoring stopped");
        }


        private string[] FindNulls(object thing,List<string> exceptionList = null)
        {
            List<string> nullProperties = new List<string>();

            System.Reflection.PropertyInfo[] properties = thing.GetType().GetProperties();

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

        #region PostgreSQL table change events
        private void _taskDefMonitor_Notification(object sender, PostgreSQLListener<FDATask>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDATask affectedRow = notifyEvent.Notification.row;


            // check for nulls
            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                string[] nulls = FindNulls(affectedRow);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "task_id " + affectedRow.task_id + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            string action = "";

            lock (_taskConfig)
            {
                switch (changeType)
                {
                    case "DELETE":
                        if (_taskConfig.ContainsKey(affectedRow.task_id))
                        {
                            _taskConfig.Remove(affectedRow.task_id);
                            action = "deleted";
                        }
                        else
                        {
                            action = "was deleted from the database but not found in the FDA, so no action was taken";
                        }
                        break;
                    case "INSERT":
                        _taskConfig.Add(affectedRow.task_id, affectedRow);
                        action = "added";
                        break;

                    case "UPDATE":

                        if (_taskConfig.ContainsKey(affectedRow.task_id))
                        {
                            action = "updated";
                            _taskConfig[affectedRow.task_id] = affectedRow;
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
                            _taskConfig.Add(affectedRow.task_id, affectedRow);
                            action = "not found, adding it as a new FDA Task";
                            changeType = "INSERT";
                            break;

                        }
                        break;
                }
            }

            Globals.SystemManager.LogApplicationEvent(this, "", "task_id  " + affectedRow.task_id + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDATasks"), affectedRow.task_id);
        }
    

        private void _deviceDefMonitor_Notification(object sender, PostgreSQLListener<FDADevice>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDADevice affectedRow = notifyEvent.Notification.row;
           
            // check for nulls
            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                string[] nulls = FindNulls(affectedRow);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "device_id " + affectedRow.device_id + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            string action = "";

            lock (_deviceConfig)
            {
                switch (changeType)
                {
                    case "DELETE":
                        if (_deviceConfig.ContainsKey(affectedRow.device_id))
                        {
                            _deviceConfig.Remove(affectedRow.device_id);
                            action = "deleted";
                        }
                        else
                        {
                            action = "deleted from the database but was not found in the FDA, so no action was taken";
                        }
                        
                        break;
                    case "INSERT":
                        _deviceConfig.Add(affectedRow.device_id, affectedRow);
                        action = "added";
                        break;

                    case "UPDATE":

                        if (_deviceConfig.ContainsKey(affectedRow.device_id))
                        {
                            action = "updated";
                            _deviceConfig[affectedRow.device_id] = affectedRow;
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
                            _deviceConfig.Add(affectedRow.device_id, affectedRow);
                            action = "not found, adding it as a new FDADevice";
                            changeType = "INSERT";

                        }
                        break;
                }
            }

            Globals.SystemManager.LogApplicationEvent(this, "", "device_id " + affectedRow.device_id + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDADevices"), affectedRow.device_id);
        }

        private void _dataPointDefMonitor_Notification(object sender, PostgreSQLListener<FDADataPointDefinitionStructure>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDADataPointDefinitionStructure affectedRow = notifyEvent.Notification.row;

            // check for nulls
            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                List<string> exceptionList = new List<string>(new string[] { "backfill_enabled", "backfill_dataID", "backfill_data_structure_type", "backfill_data_lapse_limit", "backfill_data_interval" });
                string[] nulls = FindNulls(affectedRow, exceptionList);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "DPDUID " + affectedRow.DPDUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            string action = "";
            lock (_dataPointConfig)
            {
                switch (changeType)
                {

                    case "INSERT":
                        _dataPointConfig.Add(affectedRow.DPDUID, affectedRow);
                        action = "added";
                        break;
                    case "DELETE":
                        if (_dataPointConfig.ContainsKey(affectedRow.DPDUID))
                        {
                            _dataPointConfig.Remove(affectedRow.DPDUID);
                            action = "deleted";
                        }
                        else
                        {
                            action = "deleted from the database but was not found in the FDA so no action was taken";
                        }
                        
                        break;
                    case "UPDATE":
                        if (_dataPointConfig.ContainsKey(affectedRow.DPDUID))
                        {
                            _dataPointConfig[affectedRow.DPDUID] = affectedRow;
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

                            // record not found so do an insert instead
                            _dataPointConfig.Add(affectedRow.DPDUID, affectedRow);
                            action = "not found, adding it as a new DataPointDefinitionStructure";
                            changeType = "INSERT";
                            break;

                        }


                        break;

                }

            }

            Globals.SystemManager.LogApplicationEvent(this, "", "DPDUID " + affectedRow.DPDUID + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("DataPointDefinitionStructures"), affectedRow.DPDUID);

        }

        private void _connectionDefMonitor_Notification(object sender, PostgreSQLListener<FDASourceConnection>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDASourceConnection affectedRow = notifyEvent.Notification.row;

            // check for nulls
            if (changeType == "INSERT" || changeType == "UPDATE")
            {
                List<string> exceptions = new List<string> { "SCDetail02" };
                string[] nulls = FindNulls(affectedRow, exceptions);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "SCUID " + affectedRow.SCUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            string action = "";
            lock (_connectionsConfig)
            {
                switch (changeType)
                {
                    case "INSERT":
                        _connectionsConfig.Add(affectedRow.SCUID, affectedRow);
                        action = "added";
                        break;
                    case "UPDATE":
                        if (_connectionsConfig.ContainsKey(affectedRow.SCUID))
                        {
                            _connectionsConfig[affectedRow.SCUID] = affectedRow;
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
                            _connectionsConfig.Add(affectedRow.SCUID, affectedRow);
                            action = "not found, adding it as a new connection";
                            changeType = "INSERT";

                        }
                        action = "updated";
                        break;
                    case "DELETE":
                        _connectionsConfig.Remove(affectedRow.SCUID);
                        action = "deleted";
                        break;
                }
            }
            Globals.SystemManager.LogApplicationEvent(this, "", "SCUID " + affectedRow.SCUID + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDASourceConnections"), affectedRow.SCUID);
        }

        private void _requestGroupDefMonitor_Notification(object sender, PostgreSQLListener<FDADataBlockRequestGroup>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDADataBlockRequestGroup affectedRow = notifyEvent.Notification.row;
         

            if (changeType =="INSERT" || changeType == "UPDATE")
            {
                // check for nulls
                string[] nulls = FindNulls(affectedRow);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "DRGUID " + affectedRow.DRGUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;
                }
            }

            string action = "";
            lock (_requestgroupConfig)
            {
                switch (changeType)
                {
                    case "INSERT":
                        _requestgroupConfig.Add(affectedRow.DRGUID, affectedRow);
                        action = "inserted";
                        break;

                    case "UPDATE":

                        if (_requestgroupConfig.ContainsKey(affectedRow.DRGUID))
                        {
                            _requestgroupConfig[affectedRow.DRGUID] = affectedRow;
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
                            _requestgroupConfig.Add(affectedRow.DRGUID, affectedRow);
                            action = "not found, adding it as a new FDADataBlockRequestGroup";
                            changeType = "INSERT";
                        }


                        break;
                    case "DELETE":
                        if (_requestgroupConfig.ContainsKey(affectedRow.DRGUID))
                        {
                            _requestgroupConfig.Remove(affectedRow.DRGUID);
                            action = "deleted";
                        }
                        else
                        {
                            action = " was deleted from the database but was not found in the FDA, so no action was taken";
                        }
                        break;
                }
            }
            Globals.SystemManager.LogApplicationEvent(this, "", "DRGUID " + affectedRow.DRGUID + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDADataBlockRequestGroup"), affectedRow.DRGUID);

        }

        private void _schedMonitor_Notification(object sender, PostgreSQLListener<FDARequestGroupScheduler>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDARequestGroupScheduler affectedRow = notifyEvent.Notification.row;
                
            string action = "";


            // check for nulls
            if (changeType =="INSERT" || changeType == "UPDATE")
            {
                string[] nulls = FindNulls(affectedRow);
                if (nulls.Length > 0)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "FRGSUID " + affectedRow.FRGSUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
                    return;

                }
            }


            lock (_schedConfig)
            {
                switch (changeType)
                {
                    case "DELETE":
                        _schedConfig.Remove(affectedRow.FRGSUID);
                        action = " deleted";
                        break;
                    case "INSERT":
                        List<FDATask> taskList;
                        affectedRow.RequestGroups = RequestGroupListToRequestGroups(affectedRow.FRGSUID, affectedRow.Priority, affectedRow.RequestGroupList, out taskList);
                        affectedRow.Tasks = taskList;
                        _schedConfig.Add(affectedRow.FRGSUID, affectedRow);
                        action = " added";
                        break;

                    case "UPDATE":

                        if (_schedConfig.ContainsKey(affectedRow.FRGSUID))
                        {
                            action = " updated";
                            lock (_schedConfig) { _schedConfig[affectedRow.FRGSUID] = affectedRow; }
                            taskList = null;
                            affectedRow.RequestGroups = RequestGroupListToRequestGroups(affectedRow.FRGSUID, affectedRow.Priority, affectedRow.RequestGroupList, out taskList);
                            affectedRow.Tasks = taskList;
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
                            _schedConfig.Add(affectedRow.FRGSUID, affectedRow);
                            action = "not found, adding it as a new FDARequestGroupScheduler";
                            changeType = "INSERT";

                        }
                        break;
                }
            }

            Globals.SystemManager.LogApplicationEvent(this, "", "FRGSUID " + affectedRow.FRGSUID + " " + action);

            RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDARequestGroupScheduler"), affectedRow.FRGSUID);

        }

        private void _demandMonitor_Notification(object sender, PostgreSQLListener<FDARequestGroupDemand>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDARequestGroupDemand affectedRow = notifyEvent.Notification.row;
            try
            {
                if (changeType =="INSERT" || changeType == "UPDATE")
                {
                    RequestGroup newRequestGroup;
                    if (affectedRow.FRGDEnabled)
                    {
                        Globals.SystemManager.LogApplicationEvent(this, "", "Demand Request Received: " + affectedRow.Description);

                        // check for nulls
                        string[] nulls = FindNulls(affectedRow);
                        if (nulls.Length > 0)
                        {
                            Globals.SystemManager.LogApplicationEvent(this, "", "FRGDUID " + affectedRow.FRGDUID + " rejected, null values in field(s) " + string.Join(",", nulls));
                            return;
                        }

                        List<RequestGroup> demandedGroupList = new List<RequestGroup>();

                        string[] requestGroupConfigs = affectedRow.RequestGroupList.Split('|');

                        Guid requestGroupID;
                        foreach (string groupConfig in requestGroupConfigs)
                        {
                            if (groupConfig.StartsWith("^"))
                            {
                                // task reference
                                Guid taskID;
                                if (!Guid.TryParse(groupConfig.Remove(0, 1), out taskID))
                                {
                                    // bad Guid
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Invalid Task ID in demand " + affectedRow.FRGDUID + ", the correct format is xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", true);
                                    continue;
                                }

                                FDATask task = GetTask(taskID);
                                if (task != null)
                                {
                                    switch (task.task_type.ToUpper())
                                    {
                                        case "DATAACQ":
                                            newRequestGroup = RequestGroupFromConfigString("Demand " + affectedRow.FRGDUID, task.task_details, affectedRow.Priority, affectedRow.CommsLogEnabled);
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
                                                Globals.SystemManager.LogApplicationEvent(this, "", "Calculating communications statistics (requested by demand " + affectedRow.FRGDUID);
                                                RemoteQueryManager.DoCommsStats(calcParams);// fromTime, toTime, altOutput, connectionFilter, deviceFilter,description,altOutput);
                                            }
                                            else
                                            {
                                                Globals.SystemManager.LogApplicationEvent(this, "", "invalid task_details in task " + task.task_id + ": " + error + ". The task will not be executed");
                                            }
                                            break;
                                        default:
                                            Globals.SystemManager.LogApplicationEvent(this, "", "FDA Task id " + taskID + ", requested by demand " + affectedRow.FRGDUID + " has an unrecognized task type '" + task.task_type + "'. The task will not be executed");
                                            continue;
                                    }
                                }
                                else
                                {
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Task " + taskID + " was not found. Task was requested by demand " + affectedRow.FRGDUID, true);
                                }
                            }
                            else
                            {
                                newRequestGroup = RequestGroupFromConfigString("Demand " + affectedRow.FRGDUID, groupConfig, affectedRow.Priority, affectedRow.CommsLogEnabled); // if the demand object has comms logging enabled, override the comms log setting on the group

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
                            RaiseDemandRequestEvent(affectedRow, demandedGroupList);
                            if (affectedRow.DestroyDRG)
                                DeleteRequestGroup(demandedGroupList);

                            if (affectedRow.DestroyFRGD)
                                DeleteDemand(affectedRow.FRGDUID);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while parsing demand request " + affectedRow.FRGDUID);
            }
        }
        #endregion

        #region SQLServer Table change events

        //private void _connectionDefMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDASourceConnection> e)
        //{
        //    ChangeType changeType = e.ChangeType;

        //    if (changeType == ChangeType.None)
        //        return;

        //    // check for nulls
        //    if (changeType == ChangeType.Insert || changeType == ChangeType.Update)
        //    {
        //        List<string> exceptions = new List<string> { "SCDetail02" };
        //        string[] nulls = FindNulls(e.Entity,exceptions);
        //        if (nulls.Length > 0)
        //        {
        //            Globals.SystemManager.LogApplicationEvent(this, "", "SCUID " + e.Entity.SCUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
        //            return;
        //        }
        //    }

        //    string action = "";
        //    lock (_connectionsConfig)
        //    {
        //        switch (changeType)
        //        {
        //            case ChangeType.Insert:
        //                _connectionsConfig.Add(e.Entity.SCUID, e.Entity);
        //                action = "added";
        //                break;                    
        //            case ChangeType.Update:
        //                if (_connectionsConfig.ContainsKey(e.Entity.SCUID))
        //                {
        //                    _connectionsConfig[e.Entity.SCUID] = e.Entity;
        //                }
        //                else
        //                {
        //                    if (e.EntityOldValues != null)
        //                    {
        //                        if (e.EntityOldValues.SCUID != e.Entity.SCUID)
        //                        {
        //                            Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the SCUID are not permitted, update for connection " + e.EntityOldValues.SCUID + " rejected");
        //                            return;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        // record not found (not due to a change in the ID) so do an insert instead
        //                        _connectionsConfig.Add(e.Entity.SCUID, e.Entity);
        //                        action = "not found, adding it as a new connection";
        //                        changeType = ChangeType.Insert;
        //                        break;
        //                    }
        //                }
        //                action = "updated";
        //                break;
        //            case ChangeType.Delete:
        //                _connectionsConfig.Remove(e.Entity.SCUID);
        //                action = "deleted";
        //                break;                     
        //        }
        //    }
        //    Globals.SystemManager.LogApplicationEvent(this, "", "SCUID " + e.Entity.SCUID + " " + action);

        //    RaiseConfigChangeEvent(changeType.ToString(),Globals.SystemManager.GetTableName("FDASourceConnections"), e.Entity.SCUID);          
        //}


        //private void _dataPointDefMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDADataPointDefinitionStructure> e)
        //{
        //    ChangeType changeType = e.ChangeType;

        //    if (changeType == ChangeType.None)
        //        return;

        //    // check for nulls
        //    if (changeType == ChangeType.Insert || changeType == ChangeType.Update)
        //    {
        //        List<string> exceptionList = new List<string>(new string[] { "backfill_enabled", "backfill_dataID", "backfill_data_structure_type", "backfill_data_lapse_limit", "backfill_data_interval" });
        //        string[] nulls = FindNulls(e.Entity, exceptionList);
        //        if (nulls.Length > 0)
        //        {
        //            Globals.SystemManager.LogApplicationEvent(this, "", "DPDUID " + e.Entity.DPDUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
        //            return;
        //        }
        //    }

        //    string action = "";
        //    lock (_dataPointConfig)
        //    {
        //        switch (changeType)
        //        {

        //            case ChangeType.Insert:
        //                _dataPointConfig.Add(e.Entity.DPDUID, e.Entity);
        //                action = "added";                       
        //                break;
        //            case ChangeType.Delete:
        //                _dataPointConfig.Remove(e.Entity.DPDUID); 
        //                action = "deleted";
        //                break;
        //            case ChangeType.Update:
        //                if (_dataPointConfig.ContainsKey(e.Entity.DPDUID))
        //                {
        //                    _dataPointConfig[e.Entity.DPDUID] = e.Entity;
        //                    action = "updated";
        //                }
        //                else
        //                {
        //                    if (e.EntityOldValues != null)
        //                    {
        //                        if (e.EntityOldValues.DPDUID != e.Entity.DPDUID)
        //                        {
        //                            Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the DPDUID are not permitted, update for DataPointDefinitionStructure " + e.EntityOldValues.DPDUID + " rejected");
        //                            return;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        // record not found (not due to a change in the ID) so do an insert instead
        //                        _dataPointConfig.Add(e.Entity.DPDUID, e.Entity);
        //                        action = "not found, adding it as a new DataPointDefinitionStructure";
        //                        changeType = ChangeType.Insert;
        //                        break;
        //                    }

        //                }


        //                break;

        //        }

        //    }

        //    Globals.SystemManager.LogApplicationEvent(this, "", "DPDUID " + e.Entity.DPDUID + " " + action);

        //    RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("DataPointDefinitionStructures"), e.Entity.DPDUID);

        //}

        //private void _DemandMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDARequestGroupDemand> e)
        //{
        //    try
        //    {
        //        if (e.ChangeType == ChangeType.Insert || e.ChangeType == ChangeType.Update)
        //        {
        //            RequestGroup newRequestGroup;
        //            if (e.Entity.FRGDEnabled)
        //            {
        //                Globals.SystemManager.LogApplicationEvent(this, "", "Demand Request Received: " + e.Entity.Description);

        //                // check for nulls
        //                string[] nulls = FindNulls(e.Entity);
        //                if (nulls.Length > 0)
        //                {
        //                    Globals.SystemManager.LogApplicationEvent(this, "", "FRGDUID " + e.Entity.FRGDUID + " rejected, null values in field(s) " + string.Join(",", nulls));
        //                    return;
        //                }

        //                List<RequestGroup> demandedGroupList = new List<RequestGroup>();

        //                string[] requestGroupConfigs = e.Entity.RequestGroupList.Split('|');

        //                Guid requestGroupID;
        //                foreach (string groupConfig in requestGroupConfigs)
        //                {
        //                    if (groupConfig.StartsWith("^"))
        //                    {
        //                        // task reference
        //                        Guid taskID;
        //                        if (!Guid.TryParse(groupConfig.Remove(0, 1),out taskID))
        //                        {
        //                            // cbad Guid
        //                            Globals.SystemManager.LogApplicationEvent(this, "", "Invalid Task ID in demand " + e.Entity.FRGDUID + ", the correct format is xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", true);
        //                            continue;
        //                        }

        //                        FDATask task = GetTask(taskID);
        //                        if (task != null)
        //                        {
        //                            switch (task.task_type.ToUpper())
        //                            {
        //                                case "DATAACQ":
        //                                    newRequestGroup = RequestGroupFromConfigString("Demand " + e.Entity.FRGDUID, task.task_details, e.Entity.Priority, e.Entity.CommsLogEnabled);
        //                                    if (newRequestGroup != null)
        //                                    {
        //                                        demandedGroupList.Add(newRequestGroup);
        //                                        requestGroupID = Guid.Parse(task.task_details.Split(':')[0]);
        //                                        newRequestGroup.Protocol = GetRequestGroup(requestGroupID).DPSType;
        //                                    }
        //                                    break;
        //                                case "CALCCOMMSSTATS":
        //                                    string error;
        //                                    List<object> calcParams = ParseStatsCalcParams(task.task_details,out error);
        //                                    if (error == "" && calcParams != null)
        //                                    {
        //                                        Globals.SystemManager.LogApplicationEvent(this, "", "Calculating communications statistics (requested by demand " + e.Entity.FRGDUID);
        //                                        RemoteQueryManager.DoCommsStats(calcParams);// fromTime, toTime, altOutput, connectionFilter, deviceFilter,description,altOutput);
        //                                    }
        //                                    else
        //                                    {
        //                                        Globals.SystemManager.LogApplicationEvent(this, "", "invalid task_details in task " + task.task_id + ": " + error + ". The task will not be executed");
        //                                    }
        //                                    break;
        //                                default:
        //                                    Globals.SystemManager.LogApplicationEvent(this, "", "FDA Task id " + taskID + ", requested by demand " + e.Entity.FRGDUID + " has an unrecognized task type '" + task.task_type + "'. The task will not be executed");
        //                                    continue;
        //                            }
        //                        }
        //                        else
        //                        {
        //                            Globals.SystemManager.LogApplicationEvent(this, "", "Task " + taskID + " was not found. Task was requested by demand " + e.Entity.FRGDUID, true);
        //                        }
        //                    }
        //                    else
        //                    {
        //                        newRequestGroup = RequestGroupFromConfigString("Demand " + e.Entity.FRGDUID, groupConfig, e.Entity.Priority, e.Entity.CommsLogEnabled); // if the demand object has comms logging enabled, override the comms log setting on the group

        //                        if (newRequestGroup != null)
        //                        {
        //                            demandedGroupList.Add(newRequestGroup);
        //                            requestGroupID = Guid.Parse(groupConfig.Split(':')[0]);
        //                            newRequestGroup.Protocol = GetRequestGroup(requestGroupID).DPSType;
        //                        }
        //                    }
        //                }

        //                if (demandedGroupList.Count > 0)
        //                {
        //                    RaiseDemandRequestEvent(e.Entity, demandedGroupList);
        //                    if (e.Entity.DestroyDRG)
        //                        DeleteRequestGroup(demandedGroupList);

        //                    if (e.Entity.DestroyFRGD)
        //                        DeleteDemand(e.Entity.FRGDUID);
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while parsing demand request " + e.Entity.FRGDUID);
        //    }
        //}

        //private void _requestGroupMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDADataBlockRequestGroup> e)
        //{
        //    ChangeType changeType = e.ChangeType;

        //    if (changeType == ChangeType.None)
        //        return;

        //    if (changeType == ChangeType.Insert || changeType == ChangeType.Update)
        //    {
        //        // check for nulls
        //        string[] nulls = FindNulls(e.Entity);
        //        if (nulls.Length > 0)
        //        {
        //            Globals.SystemManager.LogApplicationEvent(this, "", "DRGUID " + e.Entity.DRGUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
        //            return;
        //        }
        //    }

        //    string action = "";
        //    lock (_requestgroupConfig)
        //    {
        //        switch (changeType)
        //        {
        //            case ChangeType.Insert:
        //                _requestgroupConfig.Add(e.Entity.DRGUID, e.Entity); 
        //                action = "inserted";
        //                break;

        //            case ChangeType.Update:

        //                if (_requestgroupConfig.ContainsKey(e.Entity.DRGUID))
        //                {
        //                    _requestgroupConfig[e.Entity.DRGUID] = e.Entity;
        //                    action = "updated";
        //                }
        //                else
        //                {
        //                    if (e.EntityOldValues != null)
        //                    {
        //                        if (e.EntityOldValues.DRGUID != e.Entity.DRGUID)
        //                        {
        //                            Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the DRGUID are not permitted, update for FDADataBlockRequestGroup " + e.EntityOldValues.DRGUID + " rejected");
        //                            return;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        // record not found (not due to a change in the ID) so do an insert instead
        //                        _requestgroupConfig.Add(e.Entity.DRGUID, e.Entity);
        //                        action = "not found, adding it as a new FDADataBlockRequestGroup";
        //                        changeType = ChangeType.Insert;
        //                        break;

        //                    }
        //                }


        //                break;
        //            case ChangeType.Delete:
        //                _requestgroupConfig.Remove(e.Entity.DRGUID);
        //                action = "deleted";
        //                break;                      
        //        }
        //    }
        //     Globals.SystemManager.LogApplicationEvent(this, "", "DRGUID " + e.Entity.DRGUID + " " + action);

        //    RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDADataBlockRequestGroup"), e.Entity.DRGUID);

        //}

        //private void _SchedMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDARequestGroupScheduler> e)
        //{

        //    ChangeType changeType = e.ChangeType;
        //    if (changeType == ChangeType.None)
        //        return;

        //    string action = "";


        //    // check for nulls
        //    if (changeType == ChangeType.Insert || changeType == ChangeType.Update)
        //    {
        //        string[] nulls = FindNulls(e.Entity);
        //        if (nulls.Length > 0)
        //        {
        //            Globals.SystemManager.LogApplicationEvent(this, "", "FRGSUID " + e.Entity.FRGSUID + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
        //            return;

        //        }
        //    }


        //    lock (_schedConfig)
        //    {
        //        switch (changeType)
        //        {
        //            case ChangeType.Delete:
        //                 _schedConfig.Remove(e.Entity.FRGSUID); 
        //                action = " deleted";
        //                break;
        //            case ChangeType.Insert:
        //                List<FDATask> taskList;
        //                e.Entity.RequestGroups = RequestGroupListToRequestGroups(e.Entity.FRGSUID, e.Entity.Priority, e.Entity.RequestGroupList,out taskList);
        //                e.Entity.Tasks = taskList;
        //                 _schedConfig.Add(e.Entity.FRGSUID, e.Entity);
        //                action = " added";
        //                break;

        //            case ChangeType.Update:

        //                if (_schedConfig.ContainsKey(e.Entity.FRGSUID))
        //                {                                                 
        //                    action = " updated";
        //                    lock (_schedConfig) { _schedConfig[e.Entity.FRGSUID] = e.Entity; }
        //                    taskList = null;
        //                    e.Entity.RequestGroups = RequestGroupListToRequestGroups(e.Entity.FRGSUID, e.Entity.Priority, e.Entity.RequestGroupList,out taskList);
        //                    e.Entity.Tasks = taskList;
        //                }
        //                else
        //                {
        //                    if (e.EntityOldValues != null)
        //                    {
        //                        if (e.EntityOldValues.FRGSUID != e.Entity.FRGSUID)
        //                        {
        //                            Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the FRGSUID are not permitted, update for FDARequestGroupScheduler " + e.EntityOldValues.FRGSUID + " rejected");
        //                            return;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        // record not found (not due to a change in the ID) so do an insert instead
        //                        _schedConfig.Add(e.Entity.FRGSUID, e.Entity);
        //                        action = "not found, adding it as a new FDARequestGroupScheduler";
        //                        changeType = ChangeType.Insert;
        //                        break;
        //                    }

        //                }
        //                break;
        //        }
        //    }

        //    Globals.SystemManager.LogApplicationEvent(this, "", "FRGSUID " + e.Entity.FRGSUID + " " + action);

        //    RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDARequestGroupScheduler"), e.Entity.FRGSUID);


        //}

        //private void _deviceDefMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDADevice> e)
        //{
        //    ChangeType changeType = e.ChangeType;

        //    if (changeType == ChangeType.None)
        //        return;

        //    // check for nulls
        //    if (changeType == ChangeType.Insert || changeType == ChangeType.Update)
        //    {
        //        string[] nulls = FindNulls(e.Entity);
        //        if (nulls.Length > 0)
        //        {
        //            Globals.SystemManager.LogApplicationEvent(this, "", "device_id " + e.Entity.device_id + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
        //            return;
        //        }
        //    }

        //    string action = "";

        //    lock (_deviceConfig)
        //    {
        //        switch (changeType)
        //        {
        //            case ChangeType.Delete:
        //                _deviceConfig.Remove(e.Entity.device_id);
        //                action = "deleted";
        //                break;
        //            case ChangeType.Insert:
        //                _deviceConfig.Add(e.Entity.device_id, e.Entity);
        //                action = "added";
        //                break;

        //            case ChangeType.Update:

        //                if (_deviceConfig.ContainsKey(e.Entity.device_id))
        //                {
        //                    action = "updated";
        //                    _deviceConfig[e.Entity.device_id] = e.Entity;
        //                }
        //                else
        //                {
        //                    if (e.EntityOldValues != null)
        //                    {
        //                        if (e.EntityOldValues.device_id != e.Entity.device_id)
        //                        {
        //                            Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the DeviceID are not permitted, update for FDADevice " + e.EntityOldValues.device_id + " rejected");
        //                            return;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        // record not found (not due to a change in the ID) so do an insert instead
        //                        _deviceConfig.Add(e.Entity.device_id, e.Entity);
        //                        action = "not found, adding it as a new FDADevice";
        //                        changeType = ChangeType.Insert;
        //                        break;
        //                    }
        //                }
        //                break;
        //        }
        //    }

        //    Globals.SystemManager.LogApplicationEvent(this, "", "device_id " + e.Entity.device_id + " " + action);

        //    RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDADevices"), e.Entity.device_id);
        //}
        //private void _taskDefMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDATask> e)
        //{
        //    ChangeType changeType = e.ChangeType;

        //    if (changeType == ChangeType.None)
        //        return;

        //    // check for nulls
        //    if (changeType == ChangeType.Insert || changeType == ChangeType.Update)
        //    {
        //        string[] nulls = FindNulls(e.Entity);
        //        if (nulls.Length > 0)
        //        {
        //            Globals.SystemManager.LogApplicationEvent(this, "", "task_id " + e.Entity.task_id + " " + changeType.ToString().ToLower() + " rejected, null values in field(s) " + string.Join(",", nulls));
        //            return;
        //        }
        //    }

        //    string action = "";

        //    lock (_taskConfig)
        //    {
        //        switch (changeType)
        //        {
        //            case ChangeType.Delete:
        //                _taskConfig.Remove(e.Entity.task_id);
        //                action = "deleted";
        //                break;
        //            case ChangeType.Insert:
        //                _taskConfig.Add(e.Entity.task_id, e.Entity);
        //                action = "added";
        //                break;

        //            case ChangeType.Update:

        //                if (_taskConfig.ContainsKey(e.Entity.task_id))
        //                {
        //                    action = "updated";
        //                    _taskConfig[e.Entity.task_id] = e.Entity;
        //                }
        //                else
        //                {
        //                    if (e.EntityOldValues != null)
        //                    {
        //                        if (e.EntityOldValues.task_id != e.Entity.task_id)
        //                        {
        //                            Globals.SystemManager.LogApplicationEvent(this, "", "Changes to the task_id are not permitted, update for FDATask " + e.EntityOldValues.task_id + " rejected");
        //                            return;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        // record not found (not due to a change in the ID) so do an insert instead
        //                        _taskConfig.Add(e.Entity.task_id, e.Entity);
        //                        action = "not found, adding it as a new FDA Task";
        //                        changeType = ChangeType.Insert;
        //                        break;
        //                    }
        //                }
        //                break;
        //        }
        //    }

        //    Globals.SystemManager.LogApplicationEvent(this, "", "task_id  " + e.Entity.task_id + " " + action);

        //    RaiseConfigChangeEvent(changeType.ToString(), Globals.SystemManager.GetTableName("FDATasks"), e.Entity.task_id);
        //}

        #endregion


        private void DeleteDemand(Guid DemandID)
        {
            string sql = "delete from " + Globals.SystemManager.GetTableName("FDARequestGroupDemand") + " where FRGDUID  = '" + DemandID.ToString() + "';";
            PG_ExecuteNonQuery(sql);
            return;
        }

  

        private void DeleteRequestGroup(List<RequestGroup> groupsToDelete)
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
            PG_ExecuteNonQuery(sb.ToString());    
        }

        public void WriteAlarmsEvents(List<AlarmEventRecord> recordsList)
        {
            if (_alarmEventQueue == null)
                _alarmEventQueue = new Queue<List<AlarmEventRecord>>();

            if (_alarmsEventsWriter == null)
            {
                _alarmsEventsWriter = new BackgroundWorker();
                _alarmsEventsWriter.DoWork += _alarmsEventsWriter_DoWork;
            }

            lock (_alarmEventQueue)
            {
                _alarmEventQueue.Enqueue(recordsList);
            }

            if (!_alarmsEventsWriter.IsBusy)
                _alarmsEventsWriter.RunWorkerAsync();
        }

        private void _alarmsEventsWriter_DoWork(object sender, DoWorkEventArgs e)
        {
            string sql = "";
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
                    int result = PG_ExecuteNonQuery(sql);
                   

                    if (result > 0) // if the write was successful, update the last read record in the "HistoricReferences" Table
                    {
                        sql = record.GetUpdateLastRecordSQL();
                        if (sql != "")
                        {
                            PG_ExecuteNonQuery(sql);
                        }
                    }
                    Thread.Sleep(50); // a little breather for the database, this stuff doesn't need to be written as fast as possible       
                }
            
            }
 
        }

        public byte[] GetAlmEvtPtrs(Guid connID, string NodeID, string ptrType)
        {
            string sql = "";
            int lastRead = 0;
            int currPtr = 0;

            NpgsqlConnection conn = new NpgsqlConnection(ConnectionString);
            try
            {
               
               
                    //conn.Open();

                   
                    // update the alarm or event current ptr and timestamp
                    sql = "select " + ptrType + "CurPtrPosition,"+ptrType+"LastPtrReadPosition from " + Globals.SystemManager.GetTableName("FDAHistoricReferences");
                    sql += " where NodeDetails = '" + NodeID + "' and ConnectionUID = '" + connID + "';";
                    //sqlCommand.CommandText = sql;
                    using (NpgsqlDataReader sqlDataReader = PG_ExecuteDataReader(sql, ref conn))
                    {
                        while (sqlDataReader.Read())
                        {
                            try
                            {
                                lastRead = (byte)sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(ptrType + "LastPtrReadPosition"));
                                currPtr = (byte)sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(ptrType + "CurPtrPosition"));
                            }
                            catch (Exception ex)
                            {
                                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "DBManager: out of range current or last read pointer in FDAHistoricReferences table (connectionID " + connID + ", Node " + NodeID);
                                return null;
                            }
                        }
                    }               
            }
            catch
            {
                Globals.SystemManager.LogApplicationEvent(this, "", "Query Failed: " + sql);
                return null;
            }
            conn.Close();
            conn.Dispose();
            
            lastRead = Helpers.AddCircular(lastRead, -1, 0, 239);

            return new byte[] {Convert.ToByte(lastRead),Convert.ToByte(currPtr) };
        }



        public void UpdateAlmEvtCurrentPtrs(DataRequest PtrPositionRequest)
        {
            string sql = "";
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
            affectedRows = PG_ExecuteNonQuery(sql);

            if (affectedRows == 0) // update had no effect because row doesn't exist, do an insert instead
            {
                sql = "insert into " + Globals.SystemManager.GetTableName("FDAHistoricReferences") + " (DPSType,HistoricStructureType,ConnectionUID,NodeDetails,EventsCurPtrTimestamp,EventsCurPtrPosition,EventsCurMaxNumRecords,";
                sql += "EventsLastPtrReadTimestamp,EventsLastPtrReadPosition,AlarmsCurPtrTimestamp,AlarmsCurPtrPosition,AlarmsCurMaxNumRecords,AlarmsLastPtrReadTimestamp,AlarmsLastPtrReadPosition)";
                sql += " values (";
                sql += "'" + PtrPositionRequest.Protocol + "',1,'" + PtrPositionRequest.ConnectionID + "','" + PtrPositionRequest.NodeID + "','" + ResponseTimestamp + "',";
                sql += evtsPtr + ",240,'" + ResponseTimestamp + "',0,'" + ResponseTimestamp + "'," + almsPtr + ",240,'" + ResponseTimestamp + "',0);";
                PG_ExecuteNonQuery(sql);
            }

        
        }


        public class DemandEventArgs: EventArgs
        {
            public FDARequestGroupDemand DemandRequestObject { get; }
            public List<RequestGroup> RequestGroups;

            public DemandEventArgs(FDARequestGroupDemand demand,List<RequestGroup> requestGroups)
            {
                DemandRequestObject = demand;
                RequestGroups = requestGroups;
            }
        }

        private void RaiseDemandRequestEvent(FDARequestGroupDemand demand,List<RequestGroup> requestGroups)
        {
            DemandRequest?.Invoke(this, new DemandEventArgs(demand,requestGroups));
        }


        private void RaiseConfigChangeEvent(string type,string table, Guid item)
        {
            //Globals.SystemManager.LogApplicationEvent(this, "", table + " table changed (" + type + "), ID = " + item.ToString());
            ConfigChange?.Invoke(this, new ConfigEventArgs(type,table, item));
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {

            if (!disposedValue)
            {
                if (disposing)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "Stopping Demand Table Monitor");
                    _demandMonitor?.StopListening();
                    _demandMonitor?.Dispose();

                    // log any data that's sitting in the caches before shutting down
                    Globals.SystemManager.LogApplicationEvent(this, "", "Flushing all cached data");

                    if (_cacheManager != null)
                    {
                        List<string> batches = _cacheManager.FlushAll();
                        foreach (string batch in batches)
                        {
                            PG_ExecuteNonQuery(batch);
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


                    _schedMonitor?.StopListening();
                    _dataPointDefMonitor?.StopListening();
                    _requestGroupDefMonitor?.StopListening();
                    _connectionDefMonitor?.StopListening();

                    _schedMonitor?.Dispose();
                    _dataPointDefMonitor?.Dispose();
                    _requestGroupDefMonitor?.Dispose();
                    _connectionDefMonitor?.Dispose();


                    Globals.SystemManager.LogApplicationEvent(this, "", "Clearing configuration data");
                    lock (_schedConfig) { _schedConfig.Clear(); }
                    lock (_requestgroupConfig) { _requestgroupConfig.Clear(); }
                    lock (_dataPointConfig) { _dataPointConfig.Clear(); }
                    lock (_connectionsConfig) { _connectionsConfig.Clear(); }                   
                }
                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DBManager() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    #endregion
    }


}

