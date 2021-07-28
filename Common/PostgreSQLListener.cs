using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Reflection;

namespace Common
{
    public class PostgreSQLListener<T> : IDisposable where T : new() 
    {
        private readonly string _channel = "db_notifications";
        private readonly string _connstring;
        private readonly NpgsqlConnection _conn;
        private readonly string _table;

        public delegate void ConnErrorHandler(object sender, Exception e);
        public event ConnErrorHandler Error;

        public delegate void NotificationHandler(object sender, PostgreSQLNotification notifyEvent);
        public event NotificationHandler Notification;

        /// <summary>
        /// Connects to the specified PostgreSQL database and listens for the specified notification (where the table name = the name of the generic type T)
        /// </summary>
        /// <param name="connString">The connection string for the database</param>
        /// <param name="tablename">The name of the table to listen for changes to</param>
        public PostgreSQLListener(string connString,string tablename)
        {
            _table = tablename.ToLower();

            // Connect to the database
            _connstring = connString;// + ";Keepalive=1";


            _conn = new NpgsqlConnection(connString);
            try
            {
                _conn.Open();
            }
            catch (Exception ex)
            {
                Error?.Invoke(this,ex);
                return;
            }

            // add handlers for notifications and state changes
            _conn.Notification += Conn_Notification;
            _conn.StateChange += Conn_StateChange;
        }

        private void Conn_StateChange(object sender, System.Data.StateChangeEventArgs e)
        {
            if (Globals.FDAStatus != Globals.AppState.Normal)
                return;

            if ((e.CurrentState == System.Data.ConnectionState.Broken || e.CurrentState == System.Data.ConnectionState.Closed) && e.OriginalState == System.Data.ConnectionState.Open)
                Error?.Invoke(this, new Exception("Database connection lost"));
        }

        public void StartListening()
        {
            if (_conn == null) return;

            if (_conn.State != System.Data.ConnectionState.Open) return;

            // start listening for notifications
            try
            {
                using (NpgsqlCommand command = new ("listen " + _channel + ";", _conn))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this,ex);
            }
        }

        public void StopListening()
        {
            if (_conn == null) return;
            if (_conn.State != System.Data.ConnectionState.Open) return;

            // start listening for notifications
            try
            {
                using (NpgsqlCommand command = new("unlisten " + _channel + ";", _conn))
                {
                    command.ExecuteNonQuery();
                }
            }catch (Exception ex)
            {
                Error?.Invoke(this,ex);
            }

        }

        // handle notifications of a change in one of the monitored table
        private void Conn_Notification(object sender, NpgsqlNotificationEventArgs e)
        {
           
            JObject json = JObject.Parse(e.Payload);

            // get the name of the table that changed, if it's not the one this listener is watching, ignore it
            string table = (string)json["table"];
            if (table.ToLower() != _table)
                return;

            // get the change type (update, delete, insert)
            string operation = (string)json["operation"];

            DBNotification<T> notificationData = new();
            notificationData.operation = operation;
            notificationData.table = (string)json["table"];
            notificationData.schema = (string)json["schema"];
            notificationData.Timestamp = (DateTime)json["timestamp"];


            // get the key column name(s), and the key value(s) of the row that set off the trigger
            string key_columns =((string)json["keycolumns"]).ToUpper();
            string key_values = (string)json["keyvalues"];

            string[] keycolsarray = key_columns.Split(",");
            string[] keyvalsarray = key_values.Split(",");

            PropertyInfo propInfo;
            if (operation == "DELETE")
            {
                notificationData.row = new T();
                for (int i = 0; i < keycolsarray.Length; i++)
                {
                    propInfo = notificationData.row.GetType().GetProperty(keycolsarray[i]);
                    if (propInfo.PropertyType == typeof(Guid))
                        propInfo.SetValue(notificationData.row, Guid.Parse(keyvalsarray[i]));
                    if (propInfo.PropertyType == typeof(Int32))
                        propInfo.SetValue(notificationData.row, Int32.Parse(keyvalsarray[i]));

                    if (propInfo.PropertyType == typeof(string))
                        propInfo.SetValue(notificationData.row, keyvalsarray[i]);
                }
            }
            else
            {
                string where = " where ";
                for (int i=0;i<keycolsarray.Length;i++)
                {
                    if (i > 0)
                        where += " and ";

                    where += keycolsarray[i] + " = '" + keyvalsarray[i] + "'";
                }

                // query for the row that changed or was inserted
                string query = "select row_to_json(t) from( select * from " + table + where + ") t;";
                string jsonResult;
                using (NpgsqlConnection conn = new (_connstring))
                {
                    conn.Open();
                    using (NpgsqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = query;
                        jsonResult = (string)command.ExecuteScalar();
                    }
                }

                // convert the row into an object of type T (very cool! I could use this elsewhere, like in DatabaseManager.LoadConfig() )
                notificationData.row = JsonConvert.DeserializeObject<T>(jsonResult, new CustomJsonConvert());
            }

            // raise an event to notify the app that the monitored table has changed, include the row data and operation type
            Notification?.Invoke(this, new PostgreSQLNotification(notificationData));
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            StopListening();
            _conn?.Close();
            _conn.Dispose();
            
        }

     

        public class PostgreSQLNotifierError : EventArgs
        {
            public Exception ex;

            public PostgreSQLNotifierError(Exception except)
            {
                ex = except;
            }
        }

        public class PostgreSQLNotification : EventArgs
        {
            public DBNotification<T> Notification;

            public PostgreSQLNotification(DBNotification<T> notification)
            {
                Notification = notification;
            }
        }

        public class DBNotification<innerT>
        {
            public DateTime Timestamp;
            public string operation;
            public string schema;
            public string table;
            public innerT row;
        }


    }
}
