﻿using Common;
using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace FDA
{
    public class RemoteQueryManager : IDisposable
    {
        private readonly string _connString;

        private static string _storedProcCheck = "";
        private static string _createStoredProc = "";

        private readonly string _DBManagerType;

        private readonly List<BackgroundWorker> currentWorkers;

        public RemoteQueryManager(string dbType, string connString)
        {
            _DBManagerType = dbType;
            _connString = connString;
            Globals.MQTT.Subscribe(new string[] { "DBQUERY/#" }, new byte[] { 0 });
            Globals.MQTT.MqttMsgPublishReceived += MQTT_MqttMsgPublishReceived;
            currentWorkers = new List<BackgroundWorker>();
        }

        public void DoCommsStats(List<object> commsStatsParams) // DateTime fromTime,DateTime ToTime,string outputtable="",string connectionfilter="", string devicefilter="",string description,string altoutput)
        {
            DateTime toTime = (DateTime)commsStatsParams[0];
            DateTime fromTime = (DateTime)commsStatsParams[1];
            string startTimeString = fromTime.ToString("yyyy-MM-dd hh:mm:ss tt");
            string endTimeString = toTime.ToString("yyyy-MM-dd hh:mm:ss tt");

            string query = "EXECUTE CalcStats @StartTime = '" + startTimeString + "',@EndTime = '" + endTimeString + "',@ReturnResults=0";
            if (commsStatsParams.Count > 2)
            {
                if ((string)commsStatsParams[2] != String.Empty)
                {
                    query += ",@connection='" + commsStatsParams[2] + "'";
                }
            }
            if (commsStatsParams.Count > 3)
            {
                if ((string)commsStatsParams[3] != String.Empty)
                {
                    query += ",@device='" + commsStatsParams[3] + "'";
                }
            }
            if (commsStatsParams.Count > 4)
            {
                string description = commsStatsParams[4].ToString();
                string fulldescription = description.Replace("%timestamp%", Globals.FDANow().ToString("yyyy-MM-dd hh:mm:ss tt"));
                query += ",@description='" + fulldescription + "'";
            }
            if (commsStatsParams.Count > 5)
                query += ",@outputTable='" + commsStatsParams[5] + "'";
            query += ",@saveOutput=1";

            BackgroundWorker worker = new();
            lock (currentWorkers) { currentWorkers.Add(worker); }

            switch (_DBManagerType)
            {
                case "DBManagerPG": worker.DoWork += Worker_DoWorkPG; break;
                case "DBManagerSQL": worker.DoWork += Worker_DoWorkSQL; break;
            }
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync(new QueryParameters(Guid.Empty.ToString(), query));
        }

        private void MQTT_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            string[] topic = e.Topic.Split('/');
            if (topic[0] != "DBQUERY")
                return;

            // topic must include the elements "DBQUERY" and the query ID
            if (topic.Length < 2)
                return;

            string query = Encoding.UTF8.GetString(e.Message);

            BackgroundWorker worker = new();
            switch (_DBManagerType)
            {
                case "DBManagerPG": worker.DoWork += Worker_DoWorkPG; break;
                case "DBManagerSQL": worker.DoWork += Worker_DoWorkSQL; break;
            }
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            lock (currentWorkers) { currentWorkers.Add(worker); }
            worker.RunWorkerAsync(new QueryParameters(topic[1], query));
        }

        private void Worker_DoWorkSQL(object sender, DoWorkEventArgs e)
        {
            QueryParameters queryParams = (QueryParameters)e.Argument;
            SqlDataAdapter da = new();
            DataSet ds = new();
            StringWriter resultXML = new();
            e.Result = ""; // default to empty string as result

            using (SqlConnection conn = new(_connString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to connect to database");
                }

                try
                {
                    using (SqlCommand sqlCommand = conn.CreateCommand())
                    {
                        // special case for CommStats query: first check if the stored proc exists, create if not
                        if (queryParams.QueryText.ToUpper().Contains("CALCSTATS"))
                        {
                            // check if stored proc exist
                            _storedProcCheck = "SELECT cast(count(1) as int) FROM sys.procedures WHERE object_id = OBJECT_ID(N'CalcStats')";
                            sqlCommand.CommandText = _storedProcCheck;
                            int exists = (int)sqlCommand.ExecuteScalar();

                            // if it does not exist, load the CREATE query from resources and run it to create the stored proc
                            if (exists == 0)
                            {
                                // if not previously loaded, load the embedded text file containing the script that creates the stored procedure
                                var assembly = Assembly.GetExecutingAssembly();
                                var resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("CreateStoredProcSQL.txt"));

                                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                                using (StreamReader reader = new(stream))
                                {
                                    _createStoredProc = reader.ReadToEnd();
                                }

                                sqlCommand.CommandText = _createStoredProc;
                                sqlCommand.ExecuteNonQuery();
                            }
                        }

                        // now run the original query
                        sqlCommand.CommandText = queryParams.QueryText;
                        da.SelectCommand = sqlCommand;
                        da.Fill(ds);

                        // return the results in XML format
                        ds.WriteXml(resultXML);
                        e.Result = new QueryResult(queryParams.QueryID, resultXML.ToString(), "");
                    }
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query failed : " + queryParams.QueryText);
                    e.Result = new QueryResult(queryParams.QueryID, "", "Error: " + ex.Message);
                }
            }
            ds.Dispose();
            da.Dispose();
            return;
        }

        private void Worker_DoWorkPG(object sender, DoWorkEventArgs e)
        {
            QueryParameters queryParams = (QueryParameters)e.Argument;
            NpgsqlDataAdapter da = new();
            DataSet ds = new();
            StringWriter resultXML = new();
            e.Result = ""; // default to empty string as result

            using (NpgsqlConnection conn = new(_connString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to connect to database");
                }

                try
                {
                    using (NpgsqlCommand sqlCommand = conn.CreateCommand())
                    {
                        // special case for CommStats query: first check if the stored proc exists, create if not
                        if (queryParams.QueryText.ToUpper().Contains("CALCSTATS"))
                        {
                            // check if stored proc exist

                            _storedProcCheck = "SELECT cast(count(1) as int) FROM pg_catalog.pg_proc JOIN pg_namespace ON pg_catalog.pg_proc.pronamespace = pg_namespace.oid WHERE proname = 'calcstats' AND pg_namespace.nspname = 'public';";
                            sqlCommand.CommandText = _storedProcCheck;
                            int exists = (int)sqlCommand.ExecuteScalar();

                            // if it does not exist, load the CREATE query from resources and run it to create the stored proc
                            if (exists == 0)
                            {
                                // if not previously loaded, load the embedded text file containing the script that creates the stored procedure
                                var assembly = Assembly.GetExecutingAssembly();
                                var resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("CreateStoredProcPG.txt"));

                                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                                using (StreamReader reader = new(stream))
                                {
                                    _createStoredProc = reader.ReadToEnd();
                                }

                                sqlCommand.CommandText = _createStoredProc;
                                sqlCommand.ExecuteNonQuery();
                            }
                        }

                        // now run the original query
                        sqlCommand.CommandText = queryParams.QueryText;
                        da.SelectCommand = sqlCommand;
                        da.Fill(ds);

                        // return the results in XML format
                        ds.WriteXml(resultXML);
                        e.Result = new QueryResult(queryParams.QueryID, resultXML.ToString(), "");
                    }
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query failed : " + queryParams.QueryText);
                    e.Result = new QueryResult(queryParams.QueryID, "", "Error: " + ex.Message);
                }
            }
            ds.Dispose();
            da.Dispose();
            return;
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            QueryResult result = (QueryResult)e.Result;

            // internal requests
            if (result.QueryID == Guid.Empty.ToString())
            {
                if (result.Error)
                {
                    Globals.SystemManager.LogApplicationEvent(this, "", "Commmunication statistics calculation failure, SQL server says' " + result.errorMsg + "'");
                }
                goto cleanup;
            }

            // external requests
            if (result.Error)
            {
                // return any errors to the requestor
                Globals.MQTT.Publish("DBQUERYRESULT/" + result.QueryID, Encoding.UTF8.GetBytes(result.errorMsg));
            }
            else
            {
                byte[] serializedResult = Encoding.UTF8.GetBytes(result.XMLResult);

                if (serializedResult.Length > 200000000)
                    Globals.MQTT.Publish("DBQUERYRESULT/" + result.QueryID, Encoding.UTF8.GetBytes("Error: Result set too large"));
                else
                    Globals.MQTT.Publish("DBQUERYRESULT/" + result.QueryID, serializedResult);
            }

        cleanup:
            BackgroundWorker worker = (BackgroundWorker)sender;
            switch (_DBManagerType)
            {
                case "DBManagerPG": worker.DoWork -= Worker_DoWorkPG; break;
                case "DBManagerSQL": worker.DoWork -= Worker_DoWorkSQL; break;
            }
            worker.RunWorkerCompleted -= Worker_RunWorkerCompleted;
            lock (currentWorkers) { currentWorkers.Remove(worker); }
            worker.Dispose();
            worker = null;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (currentWorkers.Count > 0)
            {
                // let any active workers finish up (give them 5 seconds)
                Stopwatch timer = new();
                TimeSpan timeout = new(0, 0, 5);
                timer.Start();
                while (timer.Elapsed < timeout && currentWorkers.Count > 0)
                {
                    Thread.Sleep(100);
                }
                timer.Stop();
                timer = null;
            }

            if (Globals.MQTT != null)
            {
                Globals.MQTT.Disconnect();
                Globals.MQTT.MqttMsgPublishReceived -= MQTT_MqttMsgPublishReceived;
            }
        }

        private class QueryParameters
        {
            public string QueryID;
            public string QueryText;

            public QueryParameters(string ID, string text)
            {
                QueryID = ID;
                QueryText = text;
            }
        }

        private class QueryResult
        {
            public string XMLResult;
            public string QueryID;
            public string errorMsg;
            public bool Error;

            public QueryResult(string ID, string xml, string err)
            {
                XMLResult = xml;
                QueryID = ID;
                errorMsg = err;

                Error = false;
                if (errorMsg != "")
                    Error = true;
            }
        }
    }
}