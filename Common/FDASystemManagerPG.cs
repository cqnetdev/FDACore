using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;
using Npgsql;

namespace Common
{
    public class FDASystemManagerPG : FDASystemManager, IDisposable
    {
        private PostgreSQLListener<FDAConfig> _appConfigMonitor;
        PostgreSQLListener<RocDataTypes> _rocDataTypesMonitor;
        PostgreSQLListener<RocEventFormats> _RocEventsFormatsMonitor;

        public FDASystemManagerPG(string DBInstance, string systemDBName, string login, string pass, string version, Guid executionID) : base(DBInstance, systemDBName, login, pass, version, executionID)
        {
            // ************ PostgreSQL version *******************
            _appConfigMonitor = new PostgreSQLListener<FDAConfig>(SystemDBConnectionString, "FDAConfig");
            _appConfigMonitor.Notification += _appConfigMonitor_Notification;

            // set up monitoring of RocDataTypes tables (PostgreSQL version)
            _rocDataTypesMonitor = new PostgreSQLListener<RocDataTypes>(SystemDBConnectionString, "rocdatatypes");
            _rocDataTypesMonitor.Notification += _rocDataTypesMonitor_Notification;

            // set up monitoring of the RocEventFormats table (PostgreSQL version)
            _RocEventsFormatsMonitor = new PostgreSQLListener<RocEventFormats>(SystemDBConnectionString, "RocEventFormats");
            _RocEventsFormatsMonitor.Notification += _RocEventsFormatsMonitor_Notification;

            StartListening();
        }


        protected override DataTable ExecuteQuery(string sql,string dbConnString)
        {
            int retries = 0;
            int maxRetries = 3;
            DataTable result = new DataTable();
        Retry:
            using (NpgsqlConnection conn = new NpgsqlConnection(dbConnString))
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

        protected override int ExecuteNonQuery(string sql,string dbConnString)
        {
            int rowsaffected = -99;
            int retries = 0;
            int maxRetries = 3;
        Retry:

            using (NpgsqlConnection conn = new NpgsqlConnection(dbConnString))
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

        protected override object ExecuteScalar(string sql,string dbConnString)
        {
            int maxRetries = 3;
            int retries = 0;

        Retry:
            using (NpgsqlConnection conn = new NpgsqlConnection(dbConnString))
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
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "ExecuteScalar(" + sql + ") Failed to execute query after " + (maxRetries + 1) + " attempts.");
                            return null;
                        }
                    }

                }

            }
        }

        private void _appConfigMonitor_Notification(object sender, PostgreSQLListener<FDAConfig>.PostgreSQLNotification notifyEvent)
        {
            AppConfigNotification(notifyEvent.Notification.operation, notifyEvent.Notification.row);
        }

        private void _rocDataTypesMonitor_Notification(object sender, PostgreSQLListener<RocDataTypes>.PostgreSQLNotification notifyEvent)
        {
            ROCDataTypes_Notification(notifyEvent.Notification.operation, notifyEvent.Notification.row);
        }

        private void _RocEventsFormatsMonitor_Notification(object sender, PostgreSQLListener<RocEventFormats>.PostgreSQLNotification notifyEvent)
        {
            ROCEventsNotification(notifyEvent.Notification.operation, notifyEvent.Notification.row);

        }


        protected override void StartListening()
        {
            _appConfigMonitor?.StartListening();
            _rocDataTypesMonitor?.StartListening();
            _RocEventsFormatsMonitor?.StartListening();
        }

        protected override string GetSystemDBConnectionString(string instance,string dbname,string user,string pass)
        {           
            return "Server=" + instance + ";Port=5432;User Id=" + user + ";Password=" + pass + ";Database=" + dbname + ";Keepalive=1;";
        }

   

        protected override string GetAppDBConnectionString(string instance,string db,string login,string pass)
        {
   
            return "Server=" + instance + ";port=5432; Database = " + db + ";User Id = " + login + "; password = " + pass + ";Keepalive=1;";
        }

        protected override void Cleanup()
        {
            LogApplicationEvent(this, "", "Stopping FDAConfig table monitor");
            _appConfigMonitor?.StopListening();
            _appConfigMonitor?.Dispose();

            _rocDataTypesMonitor?.StopListening();
            _rocDataTypesMonitor?.Dispose();

            _RocEventsFormatsMonitor?.StopListening();
            _RocEventsFormatsMonitor?.Dispose();

            base.Dispose();
        }
    }
}
