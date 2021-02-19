using Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
namespace FDA
{
    public class RemoteQueryManager
    {
    
        private string _connString;

        private static string _storedProcCheck = "";
        private static string _createStoredProc = "";

        private readonly string _DBManagerType;
 
        public RemoteQueryManager(string dbType, string connString)
        {
            _DBManagerType = dbType;
            _connString = connString;
            Globals.MQTT.Subscribe(new string[] { "DBQUERY/#" }, new byte[] { 0 });
            Globals.MQTT.MqttMsgPublishReceived += MQTT_MqttMsgPublishReceived;

            switch (_DBManagerType)
            {
                case "DBManagerPG": 
                    _storedProcCheck = "SELECT count(1) FROM pg_catalog.pg_proc JOIN pg_namespace ON pg_catalog.pg_proc.pronamespace = pg_namespace.oid WHERE proname = 'calcstats' AND pg_namespace.nspname = 'public';";
                    break;
                case "DBManagerSQL":
                    _storedProcCheck = ""; // to do
                    break;
            }
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

            BackgroundWorker worker = new BackgroundWorker();

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

            BackgroundWorker worker = new BackgroundWorker();
            switch (_DBManagerType)
            {
                case "DBManagerPG": worker.DoWork += Worker_DoWorkPG; break;
                case "DBManagerSQL": worker.DoWork += Worker_DoWorkSQL; break;

            }
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync(new QueryParameters(topic[1],query));
        }

        private void Worker_DoWorkSQL(object sender, DoWorkEventArgs e)
        {
            QueryParameters queryParams = (QueryParameters)e.Argument;
            SqlDataAdapter da = new SqlDataAdapter();
            DataSet ds = new DataSet();
            StringWriter resultXML = new StringWriter();
            e.Result = ""; // default to empty string as result

            using (SqlConnection conn = new SqlConnection(_connString))
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
                        // special case, CommStats query: first check if the stored proc exists, create if not
                        if (queryParams.QueryText.ToUpper().Contains("CALCSTATS"))
                        {
                            if (_storedProcCheck == "")
                            {
                                // if not previously loaded, load the embedded text file containing the script that creates the stored procedure
                                var assembly = Assembly.GetExecutingAssembly();
                                var resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("CreateStoredProc.txt"));

                                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    _createStoredProc = reader.ReadToEnd();
                                }
                            }

                            sqlCommand.CommandText = _storedProcCheck;
                            int exists = (int)sqlCommand.ExecuteScalar();
                            if (exists == 0)
                            {
                                sqlCommand.CommandText = _createStoredProc;
                                sqlCommand.ExecuteNonQuery();
                            }
                        }

                        // now run the original query                      
                        sqlCommand.CommandText = queryParams.QueryText;
                        da.SelectCommand = sqlCommand;
                        da.Fill(ds);

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
            NpgsqlDataAdapter da = new NpgsqlDataAdapter();
            DataSet ds = new DataSet();
            StringWriter resultXML = new StringWriter();
            e.Result = ""; // default to empty string as result
         
            using (NpgsqlConnection conn = new NpgsqlConnection(_connString))
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
                        // special case, CommStats query: first check if the stored proc exists, create if not
                        if (queryParams.QueryText.ToUpper().Contains("CALCSTATS"))
                        {
                            if (_storedProcCheck == "")
                            {
                                // if not previously loaded, load the embedded text file containing the script that creates the stored procedure
                                var assembly = Assembly.GetExecutingAssembly();
                                var resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("CreateStoredProc.txt"));

                                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    _createStoredProc = reader.ReadToEnd();
                                }
                            }

                            sqlCommand.CommandText = _storedProcCheck;
                            int exists = (int)sqlCommand.ExecuteScalar();
                            if (exists == 0)
                            {
                                sqlCommand.CommandText = _createStoredProc;
                                sqlCommand.ExecuteNonQuery();
                            }
                        }

                        // now run the original query                      
                        sqlCommand.CommandText = queryParams.QueryText;
                        da.SelectCommand = sqlCommand;
                        da.Fill(ds);
                       
                        ds.WriteXml(resultXML);
                        e.Result = new QueryResult(queryParams.QueryID,resultXML.ToString(),"");    
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
            worker.Dispose();
            worker = null;
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

            public QueryResult(string ID, string xml,string err)
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
