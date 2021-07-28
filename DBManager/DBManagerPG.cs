using System;
using Common;
using System.Threading;
using Npgsql;
using System.Data;


namespace FDA
{
    public class DBManagerPG : DBManager, IDisposable
    {

        PostgreSQLListener<FDARequestGroupScheduler> _schedMonitor;
        PostgreSQLListener<FDARequestGroupDemand> _demandMonitor;
        PostgreSQLListener<FDADataBlockRequestGroup> _requestGroupDefMonitor;
        PostgreSQLListener<FDADataPointDefinitionStructure> _dataPointDefMonitor;
        PostgreSQLListener<FDASourceConnection> _connectionDefMonitor;
        PostgreSQLListener<FDADevice> _deviceDefMonitor;
        PostgreSQLListener<FDATask> _taskDefMonitor;
        PostgreSQLListener<DataSubscription> _datasubscriptionMonitor;
        PostgreSQLListener<UserScriptDefinition> _userScriptsMonitor;

        // constructor
        public DBManagerPG(string connString) : base(connString)
        {
              
        }

  
        protected override bool TestConnection()
        {
            using (NpgsqlConnection conn = new (ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch
                {
                }

                return (conn.State == System.Data.ConnectionState.Open);
            }
        }

        public static string GenerateConnectionString(string instance, string database, string user, string pass)
        {
            return "Server=" + instance + ";Database=" + database + ";user=" + user + ";password=" + pass;

        }


        protected override DataTable ExecuteQuery(string sql)
        {
            int retries = 0;
            int maxRetries = 3;
            DataTable result = new();
        Retry:
            using (NpgsqlConnection conn = new (ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "ExecuteQuery() Failed to connect to database");
                    return result;
                }

                try
                {
                    using (NpgsqlDataAdapter da = new (sql, conn))
                    {
                        da.Fill(result);
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
                        Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "ExecuteQuery() Failed to execute query after " + (maxRetries + 1) + " attempts. Query = " + sql);
                        return result;
                    }
                }
            }

            return result;
        }

        protected override int ExecuteNonQuery(string sql)
        {
            int rowsaffected = -99;
            int retries = 0;
            int maxRetries = 3;
            Retry:

            using (NpgsqlConnection conn = new (ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "ExecuteNonQuery() Failed to connect to database");
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
                        Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "ExecuteNonQuery() Failed to execute query after " + (maxRetries + 1) + " attempts. Query = " + sql);
                        return -99;
                    }

                }
          
                conn.Close();
            }

            return rowsaffected;         
        }

        protected override object ExecuteScalar(string sql)
        {
            int maxRetries = 3;
            int retries = 0;

            Retry:
            using (NpgsqlConnection conn = new (ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "ExecuteScalar() Failed to connect to database");
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
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "ExecuteScalar() Failed to execute query after " + (maxRetries + 1) + " attempts.");
                            return null;
                        }
                    }

                }
               
            }
        }


        protected override DateTime GetDBStartTime()
        {
            object scalarResult = ExecuteScalar("SELECT pg_postmaster_start_time()");
            DateTime startTime = DateTime.MinValue;

            if (scalarResult != null)
                startTime = (DateTime)scalarResult;

            return startTime;

        }


        public override void Initialize()
        {
            base.Initialize();


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
            if (_datasubscriptionsTableExists)
                _datasubscriptionMonitor = new PostgreSQLListener<DataSubscription>(ConnectionString, Globals.SystemManager.GetTableName("FDASubscriptions"));
            if (_scriptsTableExists)
                _userScriptsMonitor = new PostgreSQLListener<UserScriptDefinition>(ConnectionString, Globals.SystemManager.GetTableName("fda_scripts"));

            _demandMonitor.Notification += DemandMonitor_Notification;
            _schedMonitor.Notification += SchedMonitor_Notification;
            _requestGroupDefMonitor.Notification += RequestGroupDefMonitor_Notification;
            _connectionDefMonitor.Notification += ConnectionDefMonitor_Notification;
            _dataPointDefMonitor.Notification += DataPointDefMonitor_Notification; 
            if (_datasubscriptionMonitor != null)
                _datasubscriptionMonitor.Notification += DatasubscriptionMonitor_Notification;
            if (_deviceDefMonitor != null)
                _deviceDefMonitor.Notification += DeviceDefMonitor_Notification;
            if (_taskDefMonitor != null)
                _taskDefMonitor.Notification += TaskDefMonitor_Notification;
            if (_userScriptsMonitor != null)
                _userScriptsMonitor.Notification += UserScriptsMonitor_Notification;

            _demandMonitor.Error += PostgresSQLMonitor_Error;
            _schedMonitor.Error += PostgresSQLMonitor_Error;
            _requestGroupDefMonitor.Error += PostgresSQLMonitor_Error;
            _connectionDefMonitor.Error += PostgresSQLMonitor_Error;
            _dataPointDefMonitor.Error += PostgresSQLMonitor_Error;
            if (_deviceDefMonitor != null)
                _deviceDefMonitor.Error += PostgresSQLMonitor_Error;
            if (_taskDefMonitor != null)
                _taskDefMonitor.Error += PostgresSQLMonitor_Error;
            if (_datasubscriptionMonitor != null)
                _datasubscriptionMonitor.Error += PostgresSQLMonitor_Error;
            if (_userScriptsMonitor != null)
                _userScriptsMonitor.Error += PostgresSQLMonitor_Error;

            StartChangeMonitoring();

            

            

        }


        private void PostgresSQLMonitor_Error(object sender,Exception e)
        {
            Globals.SystemManager.LogApplicationError(Globals.FDANow(), e, "Error reported by PostgreSQLListener : " + e.Message);
        }


     
    

        protected new bool PreReqCheck()
        {
            // basic pre-requisites (comms log and app log table exist)
            bool baseCheck = base.PreReqCheck();

            // postgreSQL specific pre-req checks (none for now)
            bool PGCheck = true;


            return baseCheck && PGCheck;              
        }

        // these build table queries are for SQL, need to create postgresql versions
        protected override bool BuildAppLogTable(string tableName)
        {
            string sql="CREATE TABLE applog(fdaexecutionid uuid NOT NULL,\"timestamp\" timestamp(6) NOT NULL,eventtype varchar(10) NOT NULL,objecttype varchar(100) NOT NULL,objectname varchar(500) NULL,description text NULL,errorcode varchar(10) NULL,stacktrace text NULL);";
            int result = ExecuteNonQuery(sql);
            return (result == -1);
        }

        protected override bool BuildCommsLogTable(string tableName)
        {
            string sql = "CREATE TABLE commslog(fdaexecutionid uuid NOT NULL,connectionid uuid NOT NULL,deviceid uuid NULL,deviceaddress varchar(100) NOT NULL,timestamputc1 timestamp(6) NOT NULL,timestamputc2 timestamp(6) NULL,attempt int NULL, transstatus bit NULL,transcode int NULL,elapsedperiod bigint NULL,dbrguid uuid NULL,dbrgidx varchar(30) NULL,dbrgsize int NULL,details01 varchar(1000) NULL,txsize int NULL,details02 varchar(1000) NULL,rxsize int NULL,protocol varchar(20) NULL,protocolnote varchar(4000) NULL,applicationmessage varchar(8000) NULL)";
            int result = ExecuteNonQuery(sql);
            return (result == -1);
        }

   

        #region configuration change monitoring


        // start monitoring for config changes
        public override void StartChangeMonitoring()
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
                if (_datasubscriptionsTableExists)
                    _datasubscriptionMonitor?.StartListening();
                if (_scriptsTableExists)
                    _userScriptsMonitor?.StartListening();
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error when starting Table Change Monitoring objects");
            }

        }

        //pause monitoring for config changes
        public override void PauseChangeMonitoring()
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
            if (_datasubscriptionsTableExists)
                _datasubscriptionMonitor?.StopListening();
            if (_scriptsTableExists)
                _userScriptsMonitor?.StopListening();
            Globals.SystemManager.LogApplicationEvent(this, "", "Database change monitoring stopped");
        }



        #region PostgreSQL table change events




        private void UserScriptsMonitor_Notification(object sender, PostgreSQLListener<UserScriptDefinition>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            UserScriptDefinition script = notifyEvent.Notification.row;
            UserScriptChangeNotification(changeType, script);
        }

        private void TaskDefMonitor_Notification(object sender, PostgreSQLListener<FDATask>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDATask task = notifyEvent.Notification.row;
            TaskMonitorNotification(changeType, task);
        }


        private void DatasubscriptionMonitor_Notification(object sender, PostgreSQLListener<DataSubscription>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            DataSubscription sub = notifyEvent.Notification.row;
            SubscriptionChangeNotification(changeType, sub);
        }

        private void DeviceDefMonitor_Notification(object sender, PostgreSQLListener<FDADevice>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDADevice device = notifyEvent.Notification.row;
            DeviceMonitorNotification(changeType, device);
        }

        private void DataPointDefMonitor_Notification(object sender, PostgreSQLListener<FDADataPointDefinitionStructure>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDADataPointDefinitionStructure datapoint = notifyEvent.Notification.row;
            DataPointMonitorNotification(changeType, datapoint);
        }

        private void ConnectionDefMonitor_Notification(object sender, PostgreSQLListener<FDASourceConnection>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDASourceConnection connection = notifyEvent.Notification.row;

            SourceConnectionMonitorNotification(changeType, connection);

        }

        private void RequestGroupDefMonitor_Notification(object sender, PostgreSQLListener<FDADataBlockRequestGroup>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDADataBlockRequestGroup requestGroup = notifyEvent.Notification.row;
            RequestGroupMonitorNotification(changeType, requestGroup);

        }

        private void SchedMonitor_Notification(object sender, PostgreSQLListener<FDARequestGroupScheduler>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDARequestGroupScheduler sched = notifyEvent.Notification.row;

            SchedulerMonitorNotification(changeType, sched);
        }

        private void DemandMonitor_Notification(object sender, PostgreSQLListener<FDARequestGroupDemand>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDARequestGroupDemand demand = notifyEvent.Notification.row;

            DemandMonitorNotification(changeType, demand);
        }
        #endregion

     
    
        #region IDisposable Support
         public override void Dispose()
        {
            GC.SuppressFinalize(this);
            // postgreSQL specific disposal
            _demandMonitor?.StopListening();
            _schedMonitor?.StopListening();
            _dataPointDefMonitor?.StopListening();
            _requestGroupDefMonitor?.StopListening();
            _connectionDefMonitor?.StopListening();
            _datasubscriptionMonitor?.StopListening();
            _userScriptsMonitor?.StopListening();


            _demandMonitor?.Dispose();
            _schedMonitor?.Dispose();
            _dataPointDefMonitor?.Dispose();
            _requestGroupDefMonitor?.Dispose();
            _connectionDefMonitor?.Dispose();
            _datasubscriptionMonitor?.Dispose();
            _userScriptsMonitor?.Dispose();

            // general disposal
            base.Dispose();

        }
        #endregion

    #endregion
    }


}

