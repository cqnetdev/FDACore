using System;
using Common;
using System.Threading;
using Npgsql;
using System.Data;


namespace FDA
{
    public class PG_DBManager : DBManager, IDisposable
    {

        PostgreSQLListener<FDARequestGroupScheduler> _schedMonitor;
        PostgreSQLListener<FDARequestGroupDemand> _demandMonitor;
        PostgreSQLListener<FDADataBlockRequestGroup> _requestGroupDefMonitor;
        PostgreSQLListener<FDADataPointDefinitionStructure> _dataPointDefMonitor;
        PostgreSQLListener<FDASourceConnection> _connectionDefMonitor;
        PostgreSQLListener<FDADevice> _deviceDefMonitor;
        PostgreSQLListener<FDATask> _taskDefMonitor;


        // constructor
        public PG_DBManager(string connString) : base(connString)
        {
              
        }

  
        protected override bool TestConnection()
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(ConnectionString))
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
            DataTable result = new DataTable();
        Retry:
            using (NpgsqlConnection conn = new NpgsqlConnection(ConnectionString))
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
                    using (NpgsqlDataAdapter da = new NpgsqlDataAdapter(sql, conn))
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


        protected override DateTime GetDBStartTime()
        {
            object scalarResult = ExecuteScalar("SELECT pg_postmaster_start_time()");
            DateTime startTime = DateTime.MinValue;

            if (scalarResult != null)
                startTime = (DateTime)scalarResult;

            return startTime;

        }


        public new void Initialize()
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

            

            

        }

        private void _PostgresSQLMonitor_Error(object sender,Exception e)
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
            Globals.SystemManager.LogApplicationEvent(this, "", "Database change monitoring stopped");
        }



        #region PostgreSQL table change events
        private void _taskDefMonitor_Notification(object sender, PostgreSQLListener<FDATask>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDATask task = notifyEvent.Notification.row;
            TaskMonitorNotification(changeType, task);
        }
    

        private void _deviceDefMonitor_Notification(object sender, PostgreSQLListener<FDADevice>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDADevice device = notifyEvent.Notification.row;
            DeviceMonitorNotification(changeType, device);
        }

        private void _dataPointDefMonitor_Notification(object sender, PostgreSQLListener<FDADataPointDefinitionStructure>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDADataPointDefinitionStructure datapoint = notifyEvent.Notification.row;
            DataPointMonitorNotification(changeType, datapoint);
        }

        private void _connectionDefMonitor_Notification(object sender, PostgreSQLListener<FDASourceConnection>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDASourceConnection connection = notifyEvent.Notification.row;

            SourceConnectionMonitorNotification(changeType, connection);

        }

        private void _requestGroupDefMonitor_Notification(object sender, PostgreSQLListener<FDADataBlockRequestGroup>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDADataBlockRequestGroup requestGroup = notifyEvent.Notification.row;
            RequestGroupMonitorNotification(changeType, requestGroup);

        }

        private void _schedMonitor_Notification(object sender, PostgreSQLListener<FDARequestGroupScheduler>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDARequestGroupScheduler sched = notifyEvent.Notification.row;

            SchedulerMonitorNotification(changeType, sched);
        }

        private void _demandMonitor_Notification(object sender, PostgreSQLListener<FDARequestGroupDemand>.PostgreSQLNotification notifyEvent)
        {
            string changeType = notifyEvent.Notification.operation;
            FDARequestGroupDemand demand = notifyEvent.Notification.row;

            DemandMonitorNotification(changeType, demand);
        }
        #endregion

     
    
        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected override void Dispose(bool disposing)
        {

            if (!disposedValue)
            {
                if (disposing)
                {
                    // postgreSQL specific disposal
                    _demandMonitor?.StopListening();
                    _schedMonitor?.StopListening();
                    _dataPointDefMonitor?.StopListening();
                    _requestGroupDefMonitor?.StopListening();
                    _connectionDefMonitor?.StopListening();

                    _demandMonitor?.Dispose();
                    _schedMonitor?.Dispose();
                    _dataPointDefMonitor?.Dispose();
                    _requestGroupDefMonitor?.Dispose();
                    _connectionDefMonitor?.Dispose();

                    // general disposal
                    base.Dispose();

                  
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
        public new void Dispose()
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

