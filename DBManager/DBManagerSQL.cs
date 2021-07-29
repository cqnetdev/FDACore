using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using TableDependency.SqlClient;
using TableDependency.SqlClient.Base;
using TableDependency.SqlClient.Base.Enums;
using Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading;

namespace FDA
{
    public class DBManagerSQL : DBManager, IDisposable
    {
        SqlTableDependency<FDARequestGroupScheduler> _schedMonitor;
        SqlTableDependency<FDARequestGroupDemand> _demandMonitor;
        SqlTableDependency<FDADataBlockRequestGroup> _requestGroupDefMonitor;
        SqlTableDependency<FDADataPointDefinitionStructure> _dataPointDefMonitor;
        SqlTableDependency<FDASourceConnection> _connectionDefMonitor;
        SqlTableDependency<FDADevice> _deviceDefMonitor;
        SqlTableDependency<FDATask> _taskDefMonitor;
        SqlTableDependency<UserScriptDefinition> _scriptMonitor;
        SqlTableDependency<DataSubscription> _subscriptionMonitor;
      

        public DBManagerSQL(string connString) : base(connString)
        {

        }

        public override void Initialize()
        {
            base.Initialize();

            TriggerCleanup();

            try
            {
                _schedMonitor = new SqlTableDependency<FDARequestGroupScheduler>(ConnectionString, Globals.SystemManager.GetTableName("FDARequestGroupScheduler"));
                _demandMonitor = new SqlTableDependency<FDARequestGroupDemand>(ConnectionString, Globals.SystemManager.GetTableName("FDARequestGroupDemand"));
                _requestGroupDefMonitor = new SqlTableDependency<FDADataBlockRequestGroup>(ConnectionString, Globals.SystemManager.GetTableName("FDADataBlockRequestGroup"));
                _dataPointDefMonitor = new SqlTableDependency<FDADataPointDefinitionStructure>(ConnectionString, Globals.SystemManager.GetTableName("DataPointDefinitionStructures"));
                _connectionDefMonitor = new SqlTableDependency<FDASourceConnection>(ConnectionString, Globals.SystemManager.GetTableName("FDASourceConnections"));
                
                if (_devicesTableExists)
                    _deviceDefMonitor = new SqlTableDependency<FDADevice>(ConnectionString, Globals.SystemManager.GetTableName("FDADevices"));

                if (_tasksTableExists)
                    _taskDefMonitor = new SqlTableDependency<FDATask>(ConnectionString, Globals.SystemManager.GetTableName("FDATasks"));

                if (_scriptsTableExists)
                    _scriptMonitor = new SqlTableDependency<UserScriptDefinition>(ConnectionString, Globals.SystemManager.GetTableName("fda_scripts"));

                if (_datasubscriptionsTableExists)
                    _subscriptionMonitor = new SqlTableDependency<DataSubscription>(ConnectionString, Globals.SystemManager.GetTableName("FDASubscriptions"));

                //----------------------verbose messaging for the SQLTableDependency Objects------------------------------------------
                //_schedMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                //_schedMonitor.TraceListener = new TextWriterTraceListener(Console.Out);

                //_demandMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                //_demandMonitor.TraceListener = new TextWriterTraceListener(Console.Out);

                //_requestGroupDefMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                //_requestGroupDefMonitor.TraceListener = new TextWriterTraceListener(Console.Out);

                //_dataPointDefMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                //_dataPointDefMonitor.TraceListener = new TextWriterTraceListener(Console.Out);

                //_connectionDefMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                //_connectionDefMonitor.TraceListener = new TextWriterTraceListener(Console.Out);

                //_deviceDefMonitor.TraceLevel = System.Diagnostics.TraceLevel.Verbose;
                //_deviceDefMonitor.TraceListener = new TextWriterTraceListener(Console.Out);
            } catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while initializing monitoring of table " +
                       Globals.SystemManager.GetTableName("FDARequestGroupScheduler") + ", or " +
                       Globals.SystemManager.GetTableName("FDARequestGroupDemand") + ", or " +
                       Globals.SystemManager.GetTableName("FDADataBlockRequestGroup") + ", or " +
                       Globals.SystemManager.GetTableName("DataPointDefinitionStructures") + ", or " +
                       Globals.SystemManager.GetTableName("FDASourceConnections") + ", or " +
                       Globals.SystemManager.GetTableName("FDADevices") + ", or " +
                       Globals.SystemManager.GetTableName("FDATasks") + ", or " +
                       Globals.SystemManager.GetTableName("fda_scripts."));

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

                if (_scriptMonitor != null)
                {
                    _scriptMonitor.Stop();
                    _scriptMonitor.Dispose();
                    _scriptMonitor = null;
                }

                if (_subscriptionMonitor != null)
                {
                    _subscriptionMonitor.Stop();
                    _subscriptionMonitor.Dispose();
                    _subscriptionMonitor = null;
                }

                return;
            }


            // subscribe to events, so we'll be notified when something changes in the tables, or if something changes in the status of the monitor objects
            _demandMonitor.OnChanged += DemandMonitor_OnChanged;
            _schedMonitor.OnChanged += SchedMonitor_OnChanged;
            _requestGroupDefMonitor.OnChanged += RequestGroupMonitor_OnChanged;
            _connectionDefMonitor.OnChanged += ConnectionDefMonitor_OnChanged;
            _dataPointDefMonitor.OnChanged += DataPointDefMonitor_OnChanged;
            if (_deviceDefMonitor != null)
                _deviceDefMonitor.OnChanged += DeviceDefMonitor_OnChanged;
            if (_taskDefMonitor != null)
                _taskDefMonitor.OnChanged += TaskDefMonitor_OnChanged; 
            if (_scriptMonitor != null)
                _scriptMonitor.OnChanged += ScriptMonitor_OnChanged;
            if (_subscriptionMonitor != null)
                _subscriptionMonitor.OnChanged += SubscriptionMonitor_OnChanged;

            _schedMonitor.OnStatusChanged += SchedMonitor_OnStatusChanged;
            _demandMonitor.OnStatusChanged += DemandMonitor_OnStatusChanged;
            _requestGroupDefMonitor.OnStatusChanged += RequestGroupDefMonitor_OnStatusChanged;
            _connectionDefMonitor.OnStatusChanged += ConnectionDefMonitor_OnStatusChanged;
            _dataPointDefMonitor.OnStatusChanged += DataPointDefMonitor_OnStatusChanged;
            if (_deviceDefMonitor != null) _deviceDefMonitor.OnStatusChanged += DeviceDefMonitor_OnStatusChanged;
            if (_taskDefMonitor != null) _taskDefMonitor.OnStatusChanged += TaskDefMonitor_OnStatusChanged;
            if (_scriptMonitor != null) _scriptMonitor.OnStatusChanged += ScriptMonitor_OnStatusChanged;
            if (_subscriptionMonitor != null) _subscriptionMonitor.OnStatusChanged += SubscriptionMonitor_OnStatusChanged;

            _schedMonitor.OnError += SchedMonitor_OnError;
            _demandMonitor.OnError += DemandMonitor_OnError; ;
            _requestGroupDefMonitor.OnError += RequestGroupDefMonitor_OnError;
            _connectionDefMonitor.OnError += ConnectionDefMonitor_OnError;
            _dataPointDefMonitor.OnError += DataPointDefMonitor_OnError;
            if (_deviceDefMonitor != null) _deviceDefMonitor.OnError += DeviceDefMonitor_OnError;
            if (_taskDefMonitor != null) _taskDefMonitor.OnError += TaskDefMonitor_OnError;
            if (_scriptMonitor != null) _scriptMonitor.OnError += ScriptMonitor_OnError;
            if (_subscriptionMonitor != null) _subscriptionMonitor.OnError += SubscriptionMonitor_OnError; 
            StartChangeMonitoring();
        }

     

      

    

        protected new bool PreReqCheck()
        {
            // basic pre-requisites (comms log and app log table exist etc)
            bool baseCheck = base.PreReqCheck();

            // sql server specific pre-requesites
            bool brokerEnabled = true;
            string dbname;
            string AppDBName = "FDA";
            if (Globals.SystemManager.GetAppConfig().ContainsKey("FDADBName"))
            {
                AppDBName = Globals.SystemManager.GetAppConfig()["FDADBName"].OptionValue;
            }
            string sql = "SELECT name,is_broker_enabled FROM sys.databases WHERE name in ('" + AppDBName + "','FDASystem')";
            DataTable result = ExecuteQuery(sql);
            foreach (DataRow row in result.Rows)
            {
                dbname = (string)row["name"];
                bool enabled = (bool)row["is_broker_enabled"];
                if (!enabled) Globals.SystemManager.LogApplicationEvent(this, "", "Prequisite fail: Service broker not enabled on database '" + dbname + "'");

                brokerEnabled = brokerEnabled && (bool)row["is_broker_enabled"];               
            }
            result.Clear();

            return baseCheck && brokerEnabled;
        }

        public override void PauseChangeMonitoring()
        {
            _schedMonitor.Stop();
            _demandMonitor.Stop();
            _requestGroupDefMonitor.Stop();
            _dataPointDefMonitor.Stop();
            _connectionDefMonitor.Stop();
            _deviceDefMonitor.Stop();
            _taskDefMonitor.Stop();
            _subscriptionMonitor.Stop();
            Globals.SystemManager.LogApplicationEvent(this, "", "Database change monitoring stopped");
        }

        public override void StartChangeMonitoring()
        {
            _schedMonitor?.Start();
            _demandMonitor?.Start();
            _requestGroupDefMonitor?.Start();
            _dataPointDefMonitor?.Start();
            _connectionDefMonitor?.Start();
            _deviceDefMonitor?.Start();
            _taskDefMonitor?.Start();
            _scriptMonitor?.Start();
            _subscriptionMonitor?.Start();
            Globals.SystemManager.LogApplicationEvent(this, "", "Database change monitoring started");
        }

        protected override bool BuildAppLogTable(string tableName)
        {
            string sql = "CREATE TABLE applog(fdaexecutionid uuid NOT NULL,\"timestamp\" timestamp(6) NOT NULL,eventtype varchar(10) NOT NULL,objecttype varchar(100) NOT NULL,objectname varchar(500) NULL,description text NULL,errorcode varchar(10) NULL,stacktrace text NULL);";
            int result = ExecuteNonQuery(sql);
            return (result == -1);
        }

        protected override bool BuildCommsLogTable(string tableName)
        {
            string sql = "CREATE TABLE commslog(fdaexecutionid uuid NOT NULL,connectionid uuid NOT NULL,deviceid uuid NULL,deviceaddress varchar(100) NOT NULL,timestamputc1 timestamp(6) NOT NULL,timestamputc2 timestamp(6) NULL,attempt int NULL, transstatus bit NULL,transcode int NULL,elapsedperiod bigint NULL,dbrguid uuid NULL,dbrgidx varchar(30) NULL,dbrgsize int NULL,details01 varchar(1000) NULL,txsize int NULL,details02 varchar(1000) NULL,rxsize int NULL,protocol varchar(20) NULL,protocolnote varchar(4000) NULL,applicationmessage varchar(8000) NULL)";
            int result = ExecuteNonQuery(sql);
            return (result == -1);
        }

        protected override int ExecuteNonQuery(string sql)
        {
            int rowsaffected = -99;
            int retries = 0;
            int maxRetries = 3;
        Retry:

            using (SqlConnection conn = new(ConnectionString))
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
                    using (SqlCommand sqlCommand = conn.CreateCommand())
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

        protected override DataTable ExecuteQuery(string sql)
        {
            int retries = 0;
            int maxRetries = 3;
            DataTable result = new();
        Retry:
            using (SqlConnection conn = new(ConnectionString))
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
                    using (SqlDataAdapter da = new(sql, conn))
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

        protected override object ExecuteScalar(string sql)
        {
            int maxRetries = 3;
            int retries = 0;

        Retry:
            using (SqlConnection conn = new(ConnectionString))
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


                using (SqlCommand sqlCommand = conn.CreateCommand())
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
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "ExecuteScalar(" + sql + ") Failed to execute query after " + (maxRetries + 1) + " attempts.");
                            return null;
                        }
                    }

                }

            }
        }

        protected override DateTime GetDBStartTime()
        {
            object scalarResult = ExecuteScalar("SELECT sqlserver_start_time FROM sys.dm_os_sys_info");
            DateTime startTime = DateTime.MinValue;

            if (scalarResult != null)
                startTime = (DateTime)scalarResult;

            return startTime;
        }

        protected override bool TestConnection()
        {
            using (SqlConnection conn = new(ConnectionString))
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

        private void HandleTableMonitorError(object sender,TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            Globals.SystemManager.LogApplicationError(Globals.FDANow(),e.Error, "Error reported by SQLTableDependency : " + e.Message);
        }
        private void ScriptMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<UserScriptDefinition> e)
        {
            string changeType = e.ChangeType.ToString().ToUpper();
            base.UserScriptChangeNotification(changeType, e.Entity);
        }

        private void TaskDefMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDATask> e)
        {
            string changeType = e.ChangeType.ToString().ToUpper();
            base.TaskMonitorNotification(changeType, e.Entity);
        }

        private void DeviceDefMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDADevice> e)
        {
            string changeType = e.ChangeType.ToString().ToUpper();
            base.DeviceMonitorNotification(changeType, e.Entity);
        }

        private void DataPointDefMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDADataPointDefinitionStructure> e)
        {
            string changeType = e.ChangeType.ToString().ToUpper();
            base.DataPointMonitorNotification(changeType, e.Entity);
        }

        private void RequestGroupMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDADataBlockRequestGroup> e)
        {
            string changeType = e.ChangeType.ToString().ToUpper();
            base.RequestGroupMonitorNotification(changeType, e.Entity);
        }

        private void SchedMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDARequestGroupScheduler> e)
        {
            string changeType = e.ChangeType.ToString().ToUpper();
            base.SchedulerMonitorNotification(changeType, e.Entity);
        }

        private void DemandMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDARequestGroupDemand> e)
        {
            string changeType = e.ChangeType.ToString().ToUpper();
            base.DemandMonitorNotification(changeType, e.Entity);
        }


        private void ConnectionDefMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDASourceConnection> e)
        {
            string changeType = e.ChangeType.ToString().ToUpper();
            base.SourceConnectionMonitorNotification(changeType, e.Entity);
        }

        private void SubscriptionMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<DataSubscription> e)
        {
            string changeType = e.ChangeType.ToString().ToUpper();
            base.SubscriptionChangeNotification(changeType, e.Entity);
        }

        private void ConnectionDefMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            if (_connectionDefMonitor != null)
            {
                _connectionDefMonitor.OnChanged -= ConnectionDefMonitor_OnChanged;
                _connectionDefMonitor.OnStatusChanged -= ConnectionDefMonitor_OnStatusChanged;
                _connectionDefMonitor.OnError -= ConnectionDefMonitor_OnError;
                _connectionDefMonitor = null;
            }
            HandleTableMonitorError("ConnectionDef", e);
        }

        private void SubscriptionMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            if (_subscriptionMonitor != null)
            {
                _subscriptionMonitor.OnChanged -= SubscriptionMonitor_OnChanged;
                _subscriptionMonitor.OnStatusChanged -= SubscriptionMonitor_OnStatusChanged;
                _subscriptionMonitor.OnError -= SubscriptionMonitor_OnError;
                _subscriptionMonitor = null;
            }
            HandleTableMonitorError("Subscriptions", e);
        }


        private void DataPointDefMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            if (_dataPointDefMonitor != null)
            {
                _dataPointDefMonitor.OnChanged -= DataPointDefMonitor_OnChanged;
                _dataPointDefMonitor.OnStatusChanged -= DataPointDefMonitor_OnStatusChanged;
                _dataPointDefMonitor.OnError -= DataPointDefMonitor_OnError;
                _dataPointDefMonitor = null;
            }
            HandleTableMonitorError("DataPointDefMonitor", e);
        }

        private void ScriptMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            if (_scriptMonitor != null)
            {
                _scriptMonitor.OnChanged -= ScriptMonitor_OnChanged;
                _scriptMonitor.OnStatusChanged -= ScriptMonitor_OnStatusChanged;
                _scriptMonitor.OnError -= ScriptMonitor_OnError;
                _scriptMonitor = null;
            }
            HandleTableMonitorError("ScriptMonitor", e);
        }

      

        private void RequestGroupDefMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            if (_requestGroupDefMonitor != null)
            {
                _requestGroupDefMonitor.OnChanged -= RequestGroupMonitor_OnChanged;
                _requestGroupDefMonitor.OnStatusChanged -= RequestGroupDefMonitor_OnStatusChanged;
                _requestGroupDefMonitor.OnError -= RequestGroupDefMonitor_OnError;
                _requestGroupDefMonitor = null;
            }
            HandleTableMonitorError("RequestGroupDefMonitor", e);

        }


        private void DemandMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            if (_demandMonitor != null)
            {
                _demandMonitor.OnChanged -= DemandMonitor_OnChanged;
                _demandMonitor.OnStatusChanged -= DemandMonitor_OnStatusChanged;
                _demandMonitor.OnError -= DemandMonitor_OnError;
                _demandMonitor = null;
            }
            HandleTableMonitorError("DemandMonitor", e);
        }


        private void SchedMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            if (_schedMonitor != null)
            {
                _schedMonitor.OnChanged -= SchedMonitor_OnChanged;
                _schedMonitor.OnStatusChanged -= SchedMonitor_OnStatusChanged;
                _schedMonitor.OnError -= SchedMonitor_OnError;
                _schedMonitor = null;
            }
            HandleTableMonitorError("SchedMonitor", e);
        }


        private void DeviceDefMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            if (_deviceDefMonitor != null)
            {
                _deviceDefMonitor.OnChanged -= DeviceDefMonitor_OnChanged;
                _deviceDefMonitor.OnStatusChanged -= DeviceDefMonitor_OnStatusChanged;
                _deviceDefMonitor.OnError -= DeviceDefMonitor_OnError;
                _deviceDefMonitor = null;
            }
            HandleTableMonitorError("DeviceMonitor", e);
        }

        private void TaskDefMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            if (_taskDefMonitor != null)
            {
                _taskDefMonitor.OnChanged -= TaskDefMonitor_OnChanged;
                _taskDefMonitor.OnStatusChanged -= TaskDefMonitor_OnStatusChanged;
                _taskDefMonitor.OnError -= TaskDefMonitor_OnError;
                _taskDefMonitor = null;
            }
            HandleTableMonitorError("TaskMonitor", e);
        }

        
        private void DemandMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("DemandMonitor", e);
        }

        private void SubscriptionMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("SubscriptionsMonitor", e);
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

        private void ScriptMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("UserScriptMonitor", e);
        }

     
        private void SchedMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("ScheduleMonitor", e);
        }

        private void DeviceDefMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("DeviceMonitor", e);
        }

        private void TaskDefMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            LogMonitorStatusChangeEvent("TaskMonitor", e);
        }



        private static void LogMonitorStatusChangeEvent(string monitorName, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            if (e.Status != TableDependencyStatus.StopDueToError)
                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "SQLTableDependency", monitorName, "Status change: " + e.Status.ToString());
        }
        


        private void TriggerCleanup()
        {
            // get a list of triggers to be cleaned  
            string triggerDropSQL = "";
            string triggerQuerySQL = "select TR.name as trigname,U.name as tablename from (select name, parent_obj from sysobjects where type = 'TR') TR inner join (select name, id from sysobjects where name in ('DataPointDefinitionStructures', 'FDADataBlockRequestGroup', 'FDADevices','FDARequestGroupDemand','FDARequestGroupScheduler','FDASourceConnections','FDATasks')) U on TR.parent_obj = U.id;";
            DataTable triggers = ExecuteQuery(triggerQuerySQL);

            string triggerName;
            string tableName;
            foreach (DataRow row in triggers.Rows)
            {
                triggerName = (string)row["trigname"];
                tableName = (string)row["tablename"];
                Globals.SystemManager.LogApplicationEvent(this, "", "Removing old trigger '" + triggerName + "' from table '" + tableName + "'",false,true);
                triggerDropSQL += "drop trigger if exists [" + triggerName + "];";

                ExecuteNonQuery(triggerDropSQL);
            }
        }


        #region IDisposable Support


        public override void Dispose()
        {

            // sql server specific disposal
            _demandMonitor?.Stop();
            _schedMonitor?.Stop();
            _dataPointDefMonitor?.Stop();
            _requestGroupDefMonitor?.Stop();
            _connectionDefMonitor?.Stop();
            _scriptMonitor?.Stop();
            _subscriptionMonitor?.Stop();

            _demandMonitor?.Dispose();
            _schedMonitor?.Dispose();
            _dataPointDefMonitor?.Dispose();
            _requestGroupDefMonitor?.Dispose();
            _connectionDefMonitor?.Dispose();
            _scriptMonitor?.Dispose();
            _subscriptionMonitor?.Dispose();

            // general disposal
            base.Dispose();

            GC.SuppressFinalize(this);
        }

   

        public static string GenerateConnectionString(string instance, string database,string user, string pass)
        {
            return "Server=" + instance + ";Database=" + database + ";user=" + user + ";password=" + pass;

        }
        #endregion
    }
}
