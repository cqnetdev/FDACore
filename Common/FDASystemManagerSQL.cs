using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Threading;
using TableDependency.SqlClient;
using TableDependency.SqlClient.Base.Enums;

namespace Common
{
    public class FDASystemManagerSQL : FDASystemManager, IDisposable
    {

        SqlTableDependency<RocDataTypes> _rocDataTypesMonitor;
        SqlTableDependency<RocEventFormats> _RocEventsFormatsMonitor;
        SqlTableDependency<FDAConfig> _appConfigMonitor;

        public delegate void AppConfigMonitorError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e);
        public event AppConfigMonitorError AppconfigMonitorError;

        public FDASystemManagerSQL(string DBInstance, string systemDBName, string login, string pass, string version, Guid executionID) : base(DBInstance, systemDBName, login, pass, version, executionID)
        {
            // clean up any old triggers
            TriggerCleanup();

            // set up monitoring of the appconfig table
            try
            {
                _appConfigMonitor = new SqlTableDependency<FDAConfig>(SystemDBConnectionString, "FDAConfig");
            }
            catch (Exception ex)
            {
                if (ex.Message == "I cannot find a database table named 'FDAConfig'.")
                    LogApplicationError(Globals.FDANow(), ex, "FDAConfig table not found, using all default options");
                else
                {
                    LogApplicationError(Globals.FDANow(), ex, "Error while creating FDAConfig monitoring (this table will not be monitored for changes) : " + ex.Message);
                    if (_appConfigMonitor != null)
                    {
                        _appConfigMonitor.Stop();
                        _appConfigMonitor.Dispose();
                        _appConfigMonitor = null;
                    }
                }
            }

            if (_appConfigMonitor != null)
            {
                _appConfigMonitor.OnChanged += AppConfigMonitor_OnChanged;
                _appConfigMonitor.OnStatusChanged += AppConfigMonitor_OnStatusChanged;
                _appConfigMonitor.OnError += AppConfigMonitor_OnError;
            }

            // ******* set up roc data types table monitoring ***********/
            try
            {
                _rocDataTypesMonitor = new SqlTableDependency<RocDataTypes>(SystemDBConnectionString, "RocDataTypes");
            }
            catch (Exception ex)
            {
                if (ex.Message == "I cannot find a database table named 'RocDataTypes'.")
                    LogApplicationError(Globals.FDANow(), ex, "RocDataTypes table not found, the FDA will log all event values as 32 bit unsigned integers");
                else
                {
                    LogApplicationError(Globals.FDANow(), ex, "Error while creating RocDataTypes monitoring (this table will not be monitored for changes) : " + ex.Message);
                    if (_rocDataTypesMonitor != null)
                    {
                        _rocDataTypesMonitor.Stop();
                        _rocDataTypesMonitor.Dispose();
                        _rocDataTypesMonitor = null;
                    }
                }
            }

            if (_rocDataTypesMonitor != null)
            {
                _rocDataTypesMonitor.OnChanged += RocDataTypesMonitor_OnChanged;
                _rocDataTypesMonitor.OnStatusChanged += RocDataTypesMonitor_OnStatusChanged;
                _rocDataTypesMonitor.OnError += RocDataTypesMonitor_OnError;
                _rocDataTypesMonitor.Start();
            }

            // set up monitoring of RocEventFormats table 
            try
            {
                _RocEventsFormatsMonitor = new SqlTableDependency<RocEventFormats>(SystemDBConnectionString, "RocEventFormats");
            }
            catch (Exception ex)
            {
                if (ex.Message == "I cannot find a database table named 'RocEventFormats'.")
                    LogApplicationError(Globals.FDANow(), ex, "RocEventFormats table not found, the FDA will not be able to log ROC events");
                else
                {
                    LogApplicationError(Globals.FDANow(), ex, "Error while creating RocEventFormats monitoring (this table will not be monitored for changes) : " + ex.Message);
                    if (_RocEventsFormatsMonitor != null)
                    {
                        _RocEventsFormatsMonitor.Stop();
                        _RocEventsFormatsMonitor.Dispose();
                        _RocEventsFormatsMonitor = null;
                    }
                }
            }

            if (_RocEventsFormatsMonitor != null)
            {
                _RocEventsFormatsMonitor.OnChanged += RocEventsFormatsMonitor_OnChanged;
                _RocEventsFormatsMonitor.OnStatusChanged += RocEventsFormatsMonitor_OnStatusChanged;
                _RocEventsFormatsMonitor.OnError += RocEventFormatsMonitor_OnError;
                _RocEventsFormatsMonitor.Start();
            }

            StartListening();
        }

      


        protected override void Cleanup()
        {
            _appConfigMonitor?.Stop();
            _appConfigMonitor?.Dispose();

            _rocDataTypesMonitor?.Stop();
            _rocDataTypesMonitor?.Dispose();

            _RocEventsFormatsMonitor?.Stop();
            _RocEventsFormatsMonitor?.Dispose();
        }

        protected override int ExecuteNonQuery(string sql,string connString)
        {
            int rowsaffected = -99;
            int retries = 0;
            int maxRetries = 3;
            Retry:

            using (SqlConnection conn = new(connString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "ExecuteNonQuery(" + sql + "): Failed to connect to database");
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

        protected override DataTable ExecuteQuery(string sql,string connString)
        {
            int retries = 0;
            int maxRetries = 3;
            DataTable result = new();
            Retry:
            using (SqlConnection conn = new(connString))
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

        protected override object ExecuteScalar(string sql,string connString)
        {
            int maxRetries = 3;
            int retries = 0;

            Retry:
            using (SqlConnection conn = new(connString))
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
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "ExecuteScalar() Failed to execute query after " + (maxRetries + 1) + " attempts.");
                            return null;
                        }
                    }

                }

            }
        }

        protected override string GetAppDBConnectionString(string instance, string dbname, string user, string pass)
        {
            return "Server=" + instance + "; Database = " + dbname + "; user = " + user + "; password = " + pass + ";";
        }

        protected override string GetSystemDBConnectionString(string instance, string dbname, string user, string pass)
        {
            return "Server=" + instance + "; Database = " + dbname + "; user = " + user + "; password = " + pass + ";";
        }

        protected override void StartListening()
        {
            _appConfigMonitor?.Start();
            _rocDataTypesMonitor?.Start();
            _RocEventsFormatsMonitor?.Start();

        }

        private void AppConfigMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDAConfig> e)
        {
            AppConfigNotification(e.ChangeType.ToString().ToUpper(), e.Entity);
        }

        private void AppConfigMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            if (e.Status != TableDependencyStatus.StopDueToError)
                LogApplicationEvent(Globals.FDANow(), "SQLTableDependency", "AppConfigMonitor", "Status change: " + e.Status.ToString());
        }

        private void AppConfigMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            _appConfigMonitor.OnChanged -= AppConfigMonitor_OnChanged;
            _appConfigMonitor.OnStatusChanged -= AppConfigMonitor_OnStatusChanged;
            _appConfigMonitor.OnError -= AppConfigMonitor_OnError;
            _appConfigMonitor = null;
            Globals.SystemManager.LogApplicationError(Globals.FDANow(), e.Error, "SQL Table change monitor object (AppConfigMonitor) error: " + e.Error.Message);

            // enable the database downtime checking routine, this will repeatedly attempt to connect to the databse, raise some kind of alert if  the downtime is too long, and re-initialize the database
            //manager when the connection is restored

            AppconfigMonitorError?.Invoke(this, e);
        }

        void TriggerCleanup()
        {
            // get a list of triggers to be cleaned  
            string triggerDropSQL = "";
            string triggerQuerySQL = "select TR.name as trigname,U.name as tablename from (select name, parent_obj from sysobjects where type = 'TR') TR inner join (select name, id from sysobjects where name in ('FDAConfig', 'RocDataTypes', 'RocEventFormats')) U on TR.parent_obj = U.id;";
            DataTable triggers = ExecuteQuery(triggerQuerySQL,SystemDBConnectionString);

            string triggerName;
            string tableName;
            foreach (DataRow row in triggers.Rows)
            {
                triggerName = (string)row["trigname"];
                tableName = (string)row["tablename"];
                Globals.SystemManager.LogApplicationEvent(this, "", "Removing old trigger '" + triggerName + "' from table '" + tableName + "'",false,true);
                triggerDropSQL += "drop trigger if exists [" + triggerName + "];";

                ExecuteNonQuery(triggerDropSQL,SystemDBConnectionString);
            }         

        }

        private void RocEventsFormatsMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<RocEventFormats> e)
        {
            ROCEventsNotification(e.ChangeType.ToString().ToUpper(), e.Entity);
        }


        private void RocEventsFormatsMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            if (e.Status != TableDependencyStatus.StopDueToError)
                LogApplicationEvent(Globals.FDANow(), "SQLTableDependency", "RocEventFormatsMonitor", "Status change: " + e.Status.ToString());
        }


        private void RocEventFormatsMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            _RocEventsFormatsMonitor.OnChanged -= RocEventsFormatsMonitor_OnChanged;
            _RocEventsFormatsMonitor.OnStatusChanged -= RocEventsFormatsMonitor_OnStatusChanged;
            _RocEventsFormatsMonitor.OnError -= RocEventFormatsMonitor_OnError;
            _RocEventsFormatsMonitor = null;
            Globals.SystemManager.LogApplicationError(Globals.FDANow(), e.Error, "SQL Table change monitor object (RocEventFormats) error: " + e.Error.Message);
        }
    
        private void RocDataTypesMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            if (e.Status != TableDependencyStatus.StopDueToError)
                LogApplicationEvent(Globals.FDANow(), "SQLTableDependency", "RocDataTypesMonitor", "Status change: " + e.Status.ToString());
        }

        private void RocDataTypesMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<RocDataTypes> e)
        {
            ROCDataTypes_Notification(e.ChangeType.ToString().ToUpper(), e.Entity);
        }

        private void RocDataTypesMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            _rocDataTypesMonitor.OnChanged -= RocDataTypesMonitor_OnChanged;
            _rocDataTypesMonitor.OnStatusChanged -= RocDataTypesMonitor_OnStatusChanged;
            _rocDataTypesMonitor.OnError -= RocDataTypesMonitor_OnError;
            _rocDataTypesMonitor = null;
            Globals.SystemManager.LogApplicationError(Globals.FDANow(), e.Error, "SQL Table change monitor object (RocDataTypes) error: " + e.Error.Message);
        }
    }
}
