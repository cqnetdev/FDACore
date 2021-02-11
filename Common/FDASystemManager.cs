using System;
using System.Collections.Generic;
using System.ComponentModel;
using Common;
using System.Data.SqlClient;
using System.Threading;
using TableDependency.SqlClient;
using TableDependency.SqlClient.Base;
using TableDependency.SqlClient.Base.Enums;
using TableDependency.SqlClient.Base.EventArgs;
using System.Diagnostics;
using System.Text;
using Npgsql;

namespace Common
{
    public class FDASystemManager : IDisposable
    {
        public string SystemDBConnectionString { get; set; }
        public string AppDBConnectionString { get; set; }
        public double CommsLogMaxDays { get => _commsLogMaxDays; set => _commsLogMaxDays = value; }
        public double EventLogMaxDays { get => _eventLogMaxDays; set => _eventLogMaxDays = value; }
        public bool EnableDebugMessages { get => _enableDebugMessages; set => _enableDebugMessages = value; }

        private List<string> ReadOnlyOptions;

        private Dictionary<string, FDAConfig> _appConfig;
        PostgreSQLListener<FDAConfig> _appConfigMonitor;

        private Dictionary<string, RocDataTypes> _rocDataTypes;
        //SqlTableDependency<RocDataTypes> _rocDataTypesMonitor;
        PostgreSQLListener<RocDataTypes> _rocDataTypesMonitor;

        private Dictionary<int, RocEventFormats> _RocEventFormats;
        //SqlTableDependency<RocEventFormats> _RocEventsFormatsMonitor;
        PostgreSQLListener<RocEventFormats> _RocEventsFormatsMonitor;

        private Queue<CommsLogItemBase> _commsLogInputBuffer;
        private Queue<EventLogItem> _eventLogInputBuffer;
        private BackgroundWorker _bgCommsLogger;
        private BackgroundWorker _bgEventLogger;
        private double _commsLogMaxDays = 1;
        private double _eventLogMaxDays = 1;
        private bool _enableDebugMessages;
        //private readonly Timer _logTrimTimer;
        private readonly Guid _executionID;
        private readonly string systemDBSQLInstance;
        private readonly string systemDBLogin;
        private readonly string systemDBPass;

        public delegate void ConfigChangeHandler(object sender, ConfigEventArgs e);
        public event ConfigChangeHandler ConfigChange;

        //public delegate void AppConfigMonitorError(object sender, ErrorEventArgs e);
        //public event AppConfigMonitorError AppconfigMonitorError;

        public FDASystemManager(string SQLInstance, string systemDBName, string login, string pass, string version, Guid executionID)
        {
            Globals.SystemManager = this;

            // Default enable debug messages to true
            EnableDebugMessages = true;

            // sql server conn string
            //SystemDBConnectionString = "Server=" + SQLInstance + "; Database = " + systemDBName + "; user = " + login + "; password = " + pass + ";";

            // postgresql conn string
            SystemDBConnectionString = "Server=" + SQLInstance + ";Port=5432;User Id=" + login + ";Password=" + pass + ";Database=" + systemDBName + ";Keepalive=1;";

            systemDBSQLInstance = SQLInstance;
            systemDBLogin = login;
            systemDBPass = pass;

            _executionID = executionID;

            _commsLogInputBuffer = new Queue<CommsLogItemBase>();
            _eventLogInputBuffer = new Queue<EventLogItem>();

            //find the amount of time between now and midnight and set the timer to execute then, and every 24 hours from then
            //DateTime currentDateTime = DateTime.Now.AddDays(1);
            //DateTime tomorrow = currentDateTime;//.AddDays(1);

            //DateTime midnightTomorrow = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 13, 57, 0);
        

            //TimeSpan timeToFirstRun = midnightTomorrow.Subtract(currentDateTime);

            //_logTrimTimer = new Timer(LogTrimTimerTick, null, timeToFirstRun, TimeSpan.FromDays(1));

            _bgCommsLogger = new BackgroundWorker();
            _bgCommsLogger.DoWork += _bgCommsLogger_DoWork;

            _bgEventLogger = new BackgroundWorker();
            _bgEventLogger.DoWork += _bgEventLogger_DoWork;


            // load the FDAConfig table from the database
            LoadAppConfigOptions();

            ReadOnlyOptions = new List<string>();
            ReadOnlyOptions.AddRange(new string[] {"AppLog","CommsLog","DataPointDefinitionStructures","FDADataBlockRequestGroup","FDADBLogin","FDADBName","FDADBPass",
                                                   "FDADevices","FDAHistoricReferences","FDALastDataValues","FDARequestGroupDemand","FDARequestGroupScheduler","FDAScripts",
                                                    "FDASourceConnections","FDASQLInstanceName"});

            // get the UTCOffset setting (we need this before we can log any events to the db)
            try
            {
                if (_appConfig.ContainsKey("UTCOffset"))
                    Globals.UTCOffset = int.Parse(_appConfig["UTCOffset"].OptionValue);
            }
            catch (Exception ex)
            {
                LogApplicationError(Globals.FDANow(), ex, "Error while parsing the value of the UTCOffset option in the FDAConfig table. Using default value of 0");
            }

            LogApplicationEvent(Globals.FDANow(), "FDA Application", "", "Starting FDA (version " + version + "), Execution ID: " + executionID);



            LogApplicationEvent(this, "", "Starting system manager");
            // LogApplicationEvent(Globals.GetOffsetUTC(),this.GetType().ToString(), "", "Starting system manager");

            // clean up any old triggers (no need,  SqlTableDependency doesn't support postgresql)
            //TriggerCleanup();

            // set up monitoring of the appconfig table

            // ************ PostgreSQL version *******************
            _appConfigMonitor = new PostgreSQLListener<FDAConfig>(SystemDBConnectionString, "FDAConfig");
            _appConfigMonitor.Notification += _appConfigMonitor_Notification;
            _appConfigMonitor.StartListening();

            // ************* SQL Server version  ******************
            /*
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
                _appConfigMonitor.OnChanged += _appConfigMonitor_OnChanged;
                _appConfigMonitor.OnStatusChanged += AppConfigMonitor_OnStatusChanged;
                _appConfigMonitor.OnError += AppConfigMonitor_OnError;
                _appConfigMonitor.Start();
            }
            */



            // load ROC lookup tables
            LoadRocLookupTables();



            // set up monitoring of RocDataTypes tables (PostgreSQL version)
            _rocDataTypesMonitor = new PostgreSQLListener<RocDataTypes>(SystemDBConnectionString, "rocdatatypes");
            _rocDataTypesMonitor.Notification += _rocDataTypesMonitor_Notification;
            _rocDataTypesMonitor.StartListening();


            // set up monitoring of RocDataTypes tables (SQL Server version)
            /*
            try
            {
                _rocDataTypesMonitor = new SqlTableDependency<RocDataTypes>(SystemDBConnectionString, "RocDataTypes");
            }
            catch (Exception ex)
            {
                if (ex.Message == "I cannot find a database table named 'RocDataTypes'.")
                    LogApplicationError(Globals.GetOffsetUTC(), ex, "RocDataTypes table not found, the FDA will log all event values as 32 bit unsigned integers");
                else
                {
                    LogApplicationError(Globals.GetOffsetUTC(), ex, "Error while creating RocDataTypes monitoring (this table will not be monitored for changes) : " + ex.Message);
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
                _rocDataTypesMonitor.OnChanged += _rocDataTypesMonitor_OnChanged;
                _rocDataTypesMonitor.OnStatusChanged += _rocDataTypesMonitor_OnStatusChanged;
                _rocDataTypesMonitor.OnError += _rocDataTypesMonitor_OnError;
                _rocDataTypesMonitor.Start();
            }
            */

            // set up monitoring of the RocEventFormats table (PostgreSQL version)
            _RocEventsFormatsMonitor = new PostgreSQLListener<RocEventFormats>(SystemDBConnectionString, "RocEventFormats");
            _RocEventsFormatsMonitor.Notification += _RocEventsFormatsMonitor_Notification;
            _RocEventsFormatsMonitor.StartListening();

            /*
            // set up monitoring of RocEventFormats table (SQL Server version)
            try
            {
                _RocEventsFormatsMonitor = new SqlTableDependency<RocEventFormats>(SystemDBConnectionString, "RocEventFormats");
            }
            catch (Exception ex)
            {
                if (ex.Message == "I cannot find a database table named 'RocEventFormats'.")
                    LogApplicationError(Globals.GetOffsetUTC(), ex, "RocEventFormats table not found, the FDA will not be able to log ROC events");
                else
                {
                    LogApplicationError(Globals.GetOffsetUTC(), ex, "Error while creating RocEventFormats monitoring (this table will not be monitored for changes) : " + ex.Message);
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
                _RocEventsFormatsMonitor.OnChanged += _RocEventsFormatsMonitor_OnChanged;
                _RocEventsFormatsMonitor.OnStatusChanged += _RocEventsFormatsMonitor_OnStatusChanged;
                _RocEventsFormatsMonitor.OnError += _RocEventFormatsMonitor_OnError;
                _RocEventsFormatsMonitor.Start();
            }
            */
        }

       


        /* SQL Server
        void TriggerCleanup()
        {
            // get a list of triggers to be cleaned
            using (NpgsqlConnection conn = new NpgsqlConnection(SystemDBConnectionString))
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
                    using (NpgsqlCommand sqlCommand = conn.CreateCommand())
                    {
                        string triggerDropSQL = "";
                        string triggerQuerySQL = "select trigger_name from information_schema.triggers where event_object_table = 'fdaconfig';";

                        sqlCommand.CommandText = triggerQuerySQL;

                        using (var sqlDataReader = sqlCommand.ExecuteReader())
                        {
                            string triggerName = "";
                            if (sqlDataReader.HasRows)
                            {
                                while (sqlDataReader.Read())
                                {
                                    triggerName = sqlDataReader.GetString(sqlDataReader.GetOrdinal("name"));
                                    Globals.SystemManager.LogApplicationEvent(this, "", "Removing old trigger '" + triggerName + "'");
                                    triggerDropSQL += "drop trigger " + triggerName + " on fdaconfig;";
                                }
                            }
                        }

                        if (triggerDropSQL != string.Empty)
                        {
                            sqlCommand.CommandText = triggerDropSQL;
                            sqlCommand.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while removing old triggers " + ex.Message);
                    return;
                }
            }
        }

        
        private void _rocDataTypesMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            _rocDataTypesMonitor.OnChanged -= _rocDataTypesMonitor_OnChanged;
            _rocDataTypesMonitor.OnStatusChanged -= _rocDataTypesMonitor_OnStatusChanged;
            _rocDataTypesMonitor.OnError -= _rocDataTypesMonitor_OnError;
            _rocDataTypesMonitor = null;
            Globals.SystemManager.LogApplicationError(Globals.GetOffsetUTC(), e.Error, "SQL Table change monitor object (RocDataTypes) error: " + e.Error.Message);
        }
        */

        private void _rocDataTypesMonitor_Notification(object sender, PostgreSQLListener<RocDataTypes>.PostgreSQLNotification notifyEvent)
        {
           
            string message = "";

            switch (notifyEvent.Notification.operation)
            {
                case "INSERT":
                    if (_rocDataTypes == null)
                        _rocDataTypes = new Dictionary<string, RocDataTypes>();
                    if (!_rocDataTypes.ContainsKey(notifyEvent.Notification.row.Key))
                        _rocDataTypes.Add(notifyEvent.Notification.row.Key, notifyEvent.Notification.row);
                    message = "RocDataTypes new row - PointType:Parameter = " + notifyEvent.Notification.row.Key;

                    break;
                case "DELETE":
                    if (_rocDataTypes != null)
                    {
                        if (_rocDataTypes.ContainsKey(notifyEvent.Notification.row.Key))
                            _rocDataTypes.Remove(notifyEvent.Notification.row.Key);
                    }
                    message = "RocDataTypes row deleted - PointType:Parameter = " + notifyEvent.Notification.row.Key;

                    break;
                case "UPDATE":
                    if (_rocDataTypes == null)
                        _rocDataTypes = new Dictionary<string, RocDataTypes>();
                    if (_rocDataTypes.ContainsKey(notifyEvent.Notification.row.Key))
                    {
                        _rocDataTypes.Remove(notifyEvent.Notification.row.Key);
                        _rocDataTypes.Add(notifyEvent.Notification.row.Key, notifyEvent.Notification.row);
                    }
                    else
                        _rocDataTypes.Add(notifyEvent.Notification.row.Key, notifyEvent.Notification.row);
                    message = "RocDataTypes row updated - PointType:Parameter = " + notifyEvent.Notification.row.Key;
                    break;
            }
            Globals.SystemManager.LogApplicationEvent(this, "", message);
        }

        /*
        private void _rocDataTypesMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<RocDataTypes> e)
        {
            if (e.ChangeType == ChangeType.None)
                return;

            string message = "";

            switch (e.ChangeType)
            {
                case ChangeType.Insert:
                    if (_rocDataTypes == null)
                        _rocDataTypes = new Dictionary<string, RocDataTypes>();
                    if (!_rocDataTypes.ContainsKey(e.Entity.Key))
                        _rocDataTypes.Add(e.Entity.Key, e.Entity);
                    message = "RocDataTypes new row - PointType:Parameter = " + e.Entity.Key;

                    break;
                case ChangeType.Delete:
                    if (_rocDataTypes != null)
                    {
                        if (_rocDataTypes.ContainsKey(e.Entity.Key))
                            _rocDataTypes.Remove(e.Entity.Key);
                    }
                    message = "RocDataTypes row deleted - PointType:Parameter = " + e.Entity.Key;

                    break;
                case ChangeType.Update:
                    if (_rocDataTypes == null)
                        _rocDataTypes = new Dictionary<string, RocDataTypes>();
                    if (_rocDataTypes.ContainsKey(e.Entity.Key))
                    {
                        _rocDataTypes.Remove(e.Entity.Key);
                        _rocDataTypes.Add(e.Entity.Key, e.Entity);
                    }
                    else
                        _rocDataTypes.Add(e.Entity.Key, e.Entity);
                    message = "RocDataTypes row updated - PointType:Parameter = " + e.Entity.Key;
                    break;
            }
            Globals.SystemManager.LogApplicationEvent(this, "", message);
        }


        private void _rocDataTypesMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            if (e.Status != TableDependencyStatus.StopDueToError)
                LogApplicationEvent(Globals.GetOffsetUTC(), "SQLTableDependency", "RocDataTypesMonitor", "Status change: " + e.Status.ToString());
        }

        private void _RocEventFormatsMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {
            _RocEventsFormatsMonitor.OnChanged -= _RocEventsFormatsMonitor_OnChanged;
            _RocEventsFormatsMonitor.OnStatusChanged -= _RocEventsFormatsMonitor_OnStatusChanged;
            _RocEventsFormatsMonitor.OnError -= _RocEventFormatsMonitor_OnError;
            _RocEventsFormatsMonitor = null;
            Globals.SystemManager.LogApplicationError(Globals.GetOffsetUTC(), e.Error, "SQL Table change monitor object (RocEventFormats) error: " + e.Error.Message);
        }
        */

         private void _RocEventsFormatsMonitor_Notification(object sender, PostgreSQLListener<RocEventFormats>.PostgreSQLNotification notifyEvent)
        {
      
            string message = "";



            switch (notifyEvent.Notification.operation)
            {
                case "INSERT":
                    if (_RocEventFormats == null)
                        _RocEventFormats = new Dictionary<int, RocEventFormats>();
                    if (!_RocEventFormats.ContainsKey(notifyEvent.Notification.row.PointType))
                        _RocEventFormats.Add(notifyEvent.Notification.row.PointType, notifyEvent.Notification.row);
                    message = "RocEventFormats new row: PointType = " + notifyEvent.Notification.row.PointType;

                    break;
                case "DELETE":
                    if (_rocDataTypes != null)
                    {
                        if (_RocEventFormats.ContainsKey(notifyEvent.Notification.row.PointType))
                            _RocEventFormats.Remove(notifyEvent.Notification.row.PointType);
                    }
                    message = "RocEventFormats row deleted: PointType = " + notifyEvent.Notification.row.PointType;

                    break;
                case "UPDATE":
                    if (_RocEventFormats == null)
                        _RocEventFormats = new Dictionary<int, RocEventFormats>();
                    if (_RocEventFormats.ContainsKey(notifyEvent.Notification.row.PointType))
                    {
                        _RocEventFormats.Remove(notifyEvent.Notification.row.PointType);
                        _RocEventFormats.Add(notifyEvent.Notification.row.PointType, notifyEvent.Notification.row);
                    }
                    else
                        _RocEventFormats.Add(notifyEvent.Notification.row.PointType, notifyEvent.Notification.row);

                    message = "RocEventFormats row updated: PointType = " + notifyEvent.Notification.row.PointType;
                    break;
            }
            Globals.SystemManager.LogApplicationEvent(this, "", message);
        }

        /*
        
        private void _RocEventsFormatsMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<RocEventFormats> e)
        {
            if (e.ChangeType == ChangeType.None)
                return;

            string message = "";



            switch (e.ChangeType)
            {
                case ChangeType.Insert:
                    if (_RocEventFormats == null)
                        _RocEventFormats = new Dictionary<int, RocEventFormats>();
                    if (!_RocEventFormats.ContainsKey(e.Entity.PointType))
                        _RocEventFormats.Add(e.Entity.PointType, e.Entity);
                    message = "RocEventFormats new row: PointType = " + e.Entity.PointType;

                    break;
                case ChangeType.Delete:
                    if (_rocDataTypes != null)
                    {
                        if (_RocEventFormats.ContainsKey(e.Entity.PointType))
                            _RocEventFormats.Remove(e.Entity.PointType);
                    }
                    message = "RocEventFormats row deleted: PointType = " + e.Entity.PointType;

                    break;
                case ChangeType.Update:
                    if (_RocEventFormats == null)
                        _RocEventFormats = new Dictionary<int, RocEventFormats>();
                    if (_RocEventFormats.ContainsKey(e.Entity.PointType))
                    {
                        _RocEventFormats.Remove(e.Entity.PointType);
                        _RocEventFormats.Add(e.Entity.PointType, e.Entity);
                    }
                    else
                        _RocEventFormats.Add(e.Entity.PointType, e.Entity);

                    message = "RocEventFormats row updated: PointType = " + e.Entity.PointType;
                    break;
            }
            Globals.SystemManager.LogApplicationEvent(this, "",message);
        }


        private void _RocEventsFormatsMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            if (e.Status != TableDependencyStatus.StopDueToError)
                LogApplicationEvent(Globals.GetOffsetUTC(), "SQLTableDependency", "RocEventFormatsMonitor", "Status change: " + e.Status.ToString());
        }

        */

        /* SQL Server
        private void AppConfigMonitor_OnError(object sender, TableDependency.SqlClient.Base.EventArgs.ErrorEventArgs e)
        {

            _appConfigMonitor.OnChanged -= _appConfigMonitor_OnChanged;
            _appConfigMonitor.OnStatusChanged -= AppConfigMonitor_OnStatusChanged;
            _appConfigMonitor.OnError -= AppConfigMonitor_OnError;
            _appConfigMonitor = null;
            Globals.SystemManager.LogApplicationError(Globals.FDANow(), e.Error, "SQL Table change monitor object (AppConfigMonitor) error: " + e.Error.Message);

            // enable the database downtime checking routine, this will repeatedly attempt to connect to the databse, raise some kind of alert if  the downtime is too long, and re-initialize the database
            //manager when the connection is restored

            AppconfigMonitorError?.Invoke(this, e);
        }

        private void AppConfigMonitor_OnStatusChanged(object sender, TableDependency.SqlClient.Base.EventArgs.StatusChangedEventArgs e)
        {
            if (e.Status != TableDependencyStatus.StopDueToError)
                LogApplicationEvent(Globals.FDANow(), "SQLTableDependency", "AppConfigMonitor", "Status change: " + e.Status.ToString());
        }
        */

        private void LoadRocLookupTables()
        {
            if (_rocDataTypes == null)
                _rocDataTypes = new Dictionary<string, RocDataTypes>();
            if (_RocEventFormats == null)
                _RocEventFormats = new Dictionary<int, RocEventFormats>();

            _rocDataTypes.Clear();
            _RocEventFormats.Clear();

            //------------------ load RocEventFormats table into a Dictionary --------------------

            //SqlDataReader dataReader = ExecuteDataQuery("select PointType,Format,DescShort,DescLong from RocEventFormats");
            string query = "select PointType,Format,DescShort,DescLong from RocEventFormats";
            try
            {
                using (NpgsqlConnection conn = new NpgsqlConnection(SystemDBConnectionString))
                {
                    try
                    {
                        conn.Open();
                    }
                    catch (Exception ex)
                    {
                        // failed to connect to DB, log the error and exit the function
                        LogApplicationError(Globals.FDANow(), ex, "Event Logger: Unable to connect to the database");
                    }

                    using (NpgsqlCommand sqlCommand = conn.CreateCommand())
                    {
                        sqlCommand.CommandText = query;
                        NpgsqlDataReader dataReader = sqlCommand.ExecuteReader();

                        int pointType = -1;

                        while (dataReader.Read())
                        {
                            try
                            {
                                pointType = dataReader.GetInt32(dataReader.GetOrdinal("PointType"));
                                _RocEventFormats.Add(pointType,
                                    new RocEventFormats()
                                    {
                                        PointType = dataReader.GetInt32(dataReader.GetOrdinal("PointType")),
                                        Format = dataReader.GetInt32(dataReader.GetOrdinal("Format")),
                                        DescShort = dataReader.GetString(dataReader.GetOrdinal("DescShort")),
                                        DescLong = dataReader.GetString(dataReader.GetOrdinal("DescLong"))
                                    });
                            }
                            catch (Exception ex)
                            {
                                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to parse RocEventFormats PointType " + pointType);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query failed : " + query);
            }


            //------------------ load RocDataTypes table into a Dictionary --------------------

            query = "select PointType,Parm,DataType,DescShort,DescLong from RocDataTypes";

            RocDataTypes datatypeEntry;

            try
            {
                using (NpgsqlConnection conn = new NpgsqlConnection(SystemDBConnectionString))
                {
                    try
                    {
                        conn.Open();
                    }
                    catch (Exception ex)
                    {
                        // failed to connect to DB, log the error and exit the function
                        LogApplicationError(Globals.FDANow(), ex, "Event Logger: Unable to connect to the database");
                    }

                    using (NpgsqlCommand sqlCommand = conn.CreateCommand())
                    {
                        sqlCommand.CommandText = query;
                        NpgsqlDataReader dataReader = sqlCommand.ExecuteReader();
                        int pointType = -1;
                        while (dataReader.Read())
                        {
                            try
                            {
                                pointType = dataReader.GetInt32(dataReader.GetOrdinal("PointType"));
                                datatypeEntry = new RocDataTypes()
                                {
                                    PointType = dataReader.GetInt32(dataReader.GetOrdinal("PointType")),
                                    Parm = dataReader.GetInt32(dataReader.GetOrdinal("Parm")),
                                    DataType = dataReader.GetString(dataReader.GetOrdinal("DataType")),
                                    DescShort = dataReader.GetString(dataReader.GetOrdinal("DescShort")),
                                    DescLong = dataReader.GetString(dataReader.GetOrdinal("DescLong"))
                                };

                                _rocDataTypes.Add(datatypeEntry.Key, datatypeEntry);
                            }
                            catch (Exception ex)
                            {
                                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to parse RocEventFormats PointType " + pointType);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query failed : " + query);
            }
        }

        public RocDataTypes GetRocDataType(int pointType, int parm)
        {
            string key = pointType + ":" + parm;
            if (_rocDataTypes.ContainsKey(key))
                return _rocDataTypes[key];
            else
                return null;
        }

        public RocEventFormats GetRocEventFormat(int pointType)
        {
            if (_RocEventFormats.ContainsKey(pointType))
                return _RocEventFormats[pointType];
            else
                return null;
        }

        private bool LoadAppConfigOptions()
        {
            if (_appConfig == null)
                _appConfig = new Dictionary<string, FDAConfig>();

            _appConfig.Clear();
            // load app config settings (if present)
            string optionName = "";
            string query = string.Empty;
            try
            {
                query = "SELECT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'fdaconfig');";
                    
                object result = PG_ExecuteScalar(query);
                bool tableExists = false;
                if (result != null)
                {
                    tableExists = (bool)result;
                }
                
                if (tableExists)
                {
                    NpgsqlConnection conn = new NpgsqlConnection(SystemDBConnectionString);

                        query = "select OptionName,OptionValue,ConfigType from FDAConfig";
                    NpgsqlDataReader reader = PG_ExecuteDataReader(query, ref conn);
                    while (reader.Read())
                    {
                        try
                        {
                            optionName = reader.GetString(0);
                            _appConfig.Add(optionName,
                                new FDAConfig()
                                {
                                    OptionName = reader.GetString(reader.GetOrdinal("OptionName")),
                                    OptionValue = reader.GetString(reader.GetOrdinal("OptionValue")),
                                    ConfigType = reader.GetInt32(reader.GetOrdinal("ConfigType"))
                                });
                        }
                        catch (Exception ex)
                        {
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to parse option '" + optionName);
                            conn.Close();
                            conn.Dispose();
                            return false;
                        }
                    }  
                }

            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query for options from FDAConfig table failed. Query = " + query);
                return false;
            }
            _appConfigMonitor?.StartListening();
            return true;
        }

        private SqlDataReader ExecuteDataQuery(string query)
        {

            try
            {
                using (SqlConnection conn = new SqlConnection(SystemDBConnectionString))
                {
                    try
                    {
                        conn.Open();
                    }
                    catch (Exception ex)
                    {
                        // failed to connect to DB, log the error and exit the function
                        LogApplicationError(Globals.FDANow(), ex, "Event Logger: Unable to connect to the database");
                    }

                    using (SqlCommand sqlCommand = conn.CreateCommand())
                    {
                        sqlCommand.CommandText = query;
                        SqlDataReader reader = sqlCommand.ExecuteReader();
                        return reader;
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Query failed : " + query);
            }

            return null;
        }

        public string GetAppDBConnectionString()
        {
            Dictionary<String, FDAConfig> options = GetAppConfig();
            string FDASqlInstance = systemDBSQLInstance;
            string FDAdb = "FDA";
            string FDALogin = systemDBLogin;
            string FDAPass = systemDBPass;

            if (options != null)
            {
                if (options.ContainsKey("FDASQLInstanceName"))
                    FDASqlInstance = options["FDASQLInstanceName"].OptionValue;

                if (options.ContainsKey("FDADBName"))
                    FDAdb = options["FDADBName"].OptionValue;

                if (options.ContainsKey("FDADBLogin"))
                    FDALogin = options["FDADBLogin"].OptionValue;

                if (options.ContainsKey("FDADBPass"))
                    FDAPass = options["FDADBPass"].OptionValue;
            }

            AppDBConnectionString = "Server=" + FDASqlInstance + ";port=5432; Database = " + FDAdb + ";User Id = " + FDALogin + "; password = " + FDAPass + ";Keepalive=1;";

            return AppDBConnectionString;
        }

        public string GetTableName(string defaultName)
        {
            string alias = defaultName;
            if (_appConfig.ContainsKey(defaultName))
                alias = _appConfig[defaultName].OptionValue;

            return alias;
        }

        public Dictionary<string, FDAConfig> GetAppConfig()
        {
            return _appConfig;
        }



        private void _appConfigMonitor_Notification(object sender, PostgreSQLListener<FDAConfig>.PostgreSQLNotification notifyEvent)
        {
            string restartMessage = " (change will be applied after a restart)";
            string message = "";

            /* Mar 9, 2020 Ignore BackfillDataLapseLimit global setting */
            if (notifyEvent.Notification.row.OptionName == "BackfillDataLapseLimit")
                return;
            bool isReadOnly = false;
            if (notifyEvent.Notification.operation != "NONE")
            {
                isReadOnly = ReadOnlyOptions.Contains(notifyEvent.Notification.row.OptionName);
                if (notifyEvent.Notification.operation == "INSERT")
                {
                    if (!isReadOnly)
                        _appConfig.Add(notifyEvent.Notification.row.OptionName, notifyEvent.Notification.row);

                    message = "FDAConfig new option entered : " + notifyEvent.Notification.row.OptionName + " = " + notifyEvent.Notification.row.OptionValue;

                    // publish the default comms stats table to MQTT
                    if (notifyEvent.Notification.row.OptionName.ToUpper() == "COMMSSTATS")
                    {
                        PublishCommsStatsTable(notifyEvent.Notification.row.OptionValue);
                    }
                }

                if (notifyEvent.Notification.operation == "UPDATE")
                {
                    if (_appConfig.ContainsKey(notifyEvent.Notification.row.OptionName))
                    {
                        if (!isReadOnly)
                            _appConfig[notifyEvent.Notification.row.OptionName] = notifyEvent.Notification.row;

                        message = "FDAConfig option change : " + notifyEvent.Notification.row.OptionName + " = " + notifyEvent.Notification.row.OptionValue;

                        if (notifyEvent.Notification.row.OptionName.ToUpper() == "COMMSSTATS")
                        {
                            PublishCommsStatsTable(notifyEvent.Notification.row.OptionValue);
                        }
                    }
                    else
                    {
                        if (!isReadOnly)
                            _appConfig.Add(notifyEvent.Notification.row.OptionName, notifyEvent.Notification.row);

                        message = "FDAConfig new option : " + notifyEvent.Notification.row.OptionName + " = " + notifyEvent.Notification.row.OptionValue;
                        if (notifyEvent.Notification.row.OptionName.ToUpper() == "COMMSSTATS")
                        {
                            PublishCommsStatsTable(notifyEvent.Notification.row.OptionValue);
                        }
                    }
                }

                if (notifyEvent.Notification.operation == "DELETE")
                    if (_appConfig.ContainsKey(notifyEvent.Notification.row.OptionName))
                    {
                        if (!isReadOnly)
                        {
                            if (_appConfig.ContainsKey(notifyEvent.Notification.row.OptionName))
                            {
                                _appConfig.Remove(notifyEvent.Notification.row.OptionName);
                            }
                        }
                        message = "FDAConfig option deleted, reverting to default : " + notifyEvent.Notification.row.OptionName;

                        if (notifyEvent.Notification.row.OptionName.ToUpper() == "COMMSSTATS")
                        {
                            PublishCommsStatsTable(notifyEvent.Notification.row.OptionName);
                        }

                    }

                if (isReadOnly)
                    message += restartMessage;

                Globals.SystemManager.LogApplicationEvent(this, "", message);

                ConfigChange?.Invoke(this, new ConfigEventArgs(notifyEvent.Notification.operation, "FDAConfig", Guid.Empty));
            }
        }

        // SQL Server Version

        //private void _appConfigMonitor_OnChanged(object sender, TableDependency.SqlClient.Base.EventArgs.RecordChangedEventArgs<FDAConfig> e)
        //{
        //    string restartMessage = " (change will be applied after a restart)";
        //    string message = "";

        //    /* Mar 9, 2020 Ignore BackfillDataLapseLimit global setting */
        //    if (e.Entity.OptionName == "BackfillDataLapseLimit")
        //        return;
        //    bool isReadOnly = false;
        //    if (e.ChangeType != ChangeType.None)
        //    {
        //        isReadOnly = ReadOnlyOptions.Contains(e.Entity.OptionName);
        //        if (e.ChangeType == ChangeType.Insert)
        //        {
        //            if (!isReadOnly)
        //                _appConfig.Add(e.Entity.OptionName, e.Entity);

        //            message = "FDAConfig new option entered : " + e.Entity.OptionName + " = " + e.Entity.OptionValue;

        //            // publish the default comms stats table to MQTT
        //            if (e.Entity.OptionName.ToUpper() == "COMMSSTATS")
        //            {
        //                PublishCommsStatsTable(e.Entity.OptionValue);
        //            }
        //        }

        //        if (e.ChangeType == ChangeType.Update)
        //        {
        //            if (_appConfig.ContainsKey(e.Entity.OptionName))
        //            {
        //                if (!isReadOnly)
        //                    _appConfig[e.Entity.OptionName] = e.Entity;

        //                message = "FDAConfig option change : " + e.Entity.OptionName + " = " + e.Entity.OptionValue;

        //                if (e.Entity.OptionName.ToUpper() == "COMMSSTATS")
        //                {
        //                    PublishCommsStatsTable(e.Entity.OptionValue);
        //                }
        //            }
        //            else
        //            {
        //                if (!isReadOnly)
        //                    _appConfig.Add(e.Entity.OptionName, e.Entity);

        //                message = "FDAConfig new option : " + e.Entity.OptionName + " = " + e.Entity.OptionValue;
        //                if (e.Entity.OptionName.ToUpper() == "COMMSSTATS")
        //                {
        //                    PublishCommsStatsTable(e.Entity.OptionValue);
        //                }
        //            }
        //        }

        //        if (e.ChangeType == ChangeType.Delete)
        //            if (_appConfig.ContainsKey(e.Entity.OptionName))
        //            {
        //                if (!isReadOnly)
        //                    _appConfig.Remove(e.Entity.OptionName);

        //                message = "FDAConfig option deleted, reverting to default : " + e.Entity.OptionName;

        //                if (e.Entity.OptionName.ToUpper() == "COMMSSTATS")
        //                {
        //                    PublishCommsStatsTable(e.Entity.OptionName);
        //                }

        //            }

        //        if (isReadOnly)
        //            message += restartMessage;

        //        Globals.SystemManager.LogApplicationEvent(this, "", message);

        //        ConfigChange?.Invoke(this, new ConfigEventArgs(e.ChangeType.ToString(), "FDAConfig", Guid.Empty));
        //    }
        //}
         
        private void PublishCommsStatsTable(string table)
        {
            // publish changes to the default CommsStats output table to MQTT
            byte[] tableBytes = Encoding.UTF8.GetBytes(table);
            Globals.MQTT.Publish("FDA/DefaultCommsStatsTable", tableBytes, 0, true);
        }


        //private void LogTrimTimerTick(object o)
        //{
        //    Globals.SystemManager.LogApplicationEvent(this, "", "Trimming the event log and comms history tables");
   
        //    string CommsLog = GetTableName("CommsLog");
        //    string AppLog = GetTableName("AppLog");
        //    int CommsLogDel = 0;
        //    int AppLogDel = 0;

        //    string query = "DELETE FROM " + AppLog + " where Timestamp < DATEADD(HOUR," + Globals.UTCOffset + ",GETUTCDATE()) - " + _eventLogMaxDays;
        //    try
        //    {
        //        AppLogDel = PG_ExecuteNonQuery(query);
        //    }
        //    catch (Exception ex)
        //    {
        //        LogApplicationError(Globals.FDANow(), ex, "Trim log tables failed: query = " + query);
        //        return;
        //    }
        //    Globals.SystemManager.LogApplicationEvent(this, "", "Trimmed " + AppLogDel + " rows from " + AppLog);

        //    query = "DELETE FROM " + CommsLog + " where TimestampUTC1 < DATEADD(HOUR," + Globals.UTCOffset + ",GETUTCDATE()) - " + _commsLogMaxDays;
        //    try
        //    {
        //        CommsLogDel = PG_ExecuteNonQuery(query);
        //    }
        //    catch (Exception ex)
        //    {
        //        LogApplicationError(Globals.FDANow(), ex, "Trim log tables failed: query = " + query);
        //        return;
        //    }
        //    if (CommsLogDel >= 0)
        //    {
        //        Globals.SystemManager.LogApplicationEvent(this, "", "Trimmed " + CommsLogDel + " rows from " + CommsLog);
        //    }
        //}

        public void LogApplicationError(DateTime timestamp, Exception ex, string description = "")
        {
            // temporary: display it in the console too
            if (Globals.ConsoleMode)
            {
                Console.WriteLine(Helpers.FormatDateTime(timestamp) + ": " + ex.Message + ", " + description);
            }
            //    description = description.Replace("'", "''");

            // log application error events
            EventLogItem ELI = new EventLogItem(timestamp, ex, description);

            lock (_eventLogInputBuffer)
            {
                _eventLogInputBuffer.Enqueue(ELI);
                if (!_bgEventLogger.IsBusy)
                    _bgEventLogger.RunWorkerAsync();
            }
        }


        public void LogApplicationEvent(object caller, string callerName, string description, bool configError = false, bool detailed = false)
        {
            string callerType;
            if (caller != null)
                callerType = caller.GetType().ToString();
            else
                callerType = "FDA"; // called from program.Main()
            LogApplicationEvent(Globals.FDANow(), callerType, callerName, description, configError, detailed);
        }

        public void LogApplicationEvent(DateTime timestamp, string objectType, string objectName, string description, bool configError = false, bool detailed = false)
        {
            // ignore events marked as detailed if detailed messaging is not enabled
            if (detailed && !Globals.DetailedMessaging)
                return;

            if (Globals.ConsoleMode)
            {
                Console.WriteLine(Helpers.FormatDateTime(timestamp) + ": " + objectType + "(" + objectName + "), " + description);
            }

            //description = description.Replace("'", "''");

            EventLogItem ELI = new EventLogItem(timestamp, objectType, objectName, description, configError);

            lock (_eventLogInputBuffer)
            {
                _eventLogInputBuffer.Enqueue(ELI);

                if (!_bgEventLogger.IsBusy)
                    _bgEventLogger.RunWorkerAsync();

            }

        }

        public void LogCommsEvent(TransactionLogItem logItem)
        {

            lock (_commsLogInputBuffer)
            {
                _commsLogInputBuffer.Enqueue(logItem);

                if (!_bgCommsLogger.IsBusy)
                    _bgCommsLogger.RunWorkerAsync();
            }
        }

        public void LogAckEvent(AcknowledgementEvent logItem)
        {

            lock (_commsLogInputBuffer)
            {
                _commsLogInputBuffer.Enqueue(logItem);

                if (!_bgCommsLogger.IsBusy)
                    _bgCommsLogger.RunWorkerAsync();
            }
        }

        public void LogCommsEvent(Guid connectionID, DateTime timestamp, string eventDetails, byte transCode = 0)
        {
            //eventDetails = eventDetails.Replace("'", "''");
            CommsEventLogItem logItem = new CommsEventLogItem(connectionID, timestamp, eventDetails, transCode);

            lock (_commsLogInputBuffer)
            {
                _commsLogInputBuffer.Enqueue(logItem);

                if (!_bgCommsLogger.IsBusy)
                    _bgCommsLogger.RunWorkerAsync();
            }
        }

        public void LogConnectionCommsEvent(Guid connectionID, int attemptNum, DateTime startTime, TimeSpan elapsed, byte status, string message)
        {
            CommsConnectionEventLogItem logItem = new CommsConnectionEventLogItem(connectionID, attemptNum, startTime, elapsed, status, message);
            lock (_commsLogInputBuffer)
            {
                _commsLogInputBuffer.Enqueue(logItem);

                if (!_bgCommsLogger.IsBusy)
                    _bgCommsLogger.RunWorkerAsync();
            }
        }


        private void _bgEventLogger_DoWork(object sender, DoWorkEventArgs e)
        {
            using (NpgsqlConnection  conn = new NpgsqlConnection(GetAppDBConnectionString()))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    // failed to connect to DB, exit the DB write routine
                    LogApplicationError(Globals.FDANow(), ex, "Event Logger: Unable to connect to the database");
                    return;
                }

                EventLogItem logItem;
                bool success = true;
                StringBuilder batchbuilder = new StringBuilder();
                string tblName = Globals.SystemManager.GetTableName("AppLog");
                using (NpgsqlCommand sqlCommand = conn.CreateCommand())
                {

                    while (_eventLogInputBuffer.Count > 0 && conn.State == System.Data.ConnectionState.Open)
                    {


                        //logItem = _eventLogInputBuffer.Peek();


                        // batch up to 50 events together for writing to the db
                        int batchCount = 0;
                        ResetEventBatch(batchbuilder, tblName);
                        lock (_eventLogInputBuffer)
                        {
                            while (_eventLogInputBuffer.Count > 0 && batchCount <= 50)
                            {
                                logItem = _eventLogInputBuffer.Dequeue();
                                logItem.ToSQL(tblName, _executionID, batchbuilder, batchCount == 0);
                                batchCount++;
                            }
                        }



                        sqlCommand.CommandText = batchbuilder.ToString();

                        int tries = 0;
                        success = false;
                        while (!success && tries < 3)
                        {
                            try
                            {
                                sqlCommand.ExecuteNonQuery();
                                success = true;
                            }
                            catch
                            {
                                success = false;
                                tries++;
                                Console.WriteLine(Globals.FDANow() + " : Event Logger: Query failed while logging event(s) : " + sqlCommand.CommandText);
                                Thread.Sleep(1000); // one second wait between retries
                            }
                        }

                        Thread.Sleep(200);   // short break between batches gives other threads a chance to queue new events

                    }

                    if (conn.State == System.Data.ConnectionState.Open)
                        conn.Close();
                }
            }
        }

        private void ResetEventBatch(StringBuilder sb, string table)
        {
            sb.Clear();
            sb.Append("Insert INTO ");
            sb.Append(table);
            sb.Append(" (FDAExecutionID,Timestamp,EventType,ObjectType,ObjectName,Description,ErrorCode,StackTrace) VALUES ");
        }



        private void _bgCommsLogger_DoWork(object sender, DoWorkEventArgs e)
        {
            StringBuilder batchBuilder = new StringBuilder();
            string tblName = Globals.SystemManager.GetTableName("CommsLog");
            CommsLogItemBase logItem;
            bool success;

            using (NpgsqlConnection conn = new NpgsqlConnection(GetAppDBConnectionString()))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    // failed to connect to DB, log the error and exit the DB write routine
                    LogApplicationError(Globals.FDANow(), ex, "Event Logger: Unable to connect to the application database");
                    return;
                }


                using (NpgsqlCommand sqlCommand = conn.CreateCommand())
                {

                    while (_commsLogInputBuffer.Count > 0 && conn.State == System.Data.ConnectionState.Open)
                    {
                        int batchCount = 0;

                        lock (_commsLogInputBuffer)
                        {
                            ResetCommsBatch(batchBuilder, tblName);

                            while (_commsLogInputBuffer.Count > 0 && batchCount <= 50)
                            {
                                logItem = _commsLogInputBuffer.Dequeue();
                                logItem.ToSQL(tblName, _executionID, batchBuilder, batchCount == 0);
                                batchCount++;
                            }
                        }

                        if (batchCount > 0)
                        {

                            sqlCommand.CommandText = batchBuilder.ToString();
                            int tries = 0;
                            success = false;
                            while (!success && tries < 3)
                            {
                                try
                                {
                                    sqlCommand.ExecuteNonQuery();
                                    success = true;
                                }
                                catch (Exception ex)
                                {
                                    success = false;
                                    tries++;
                                    if (tries >= 2)
                                    {
                                        LogApplicationError(Globals.FDANow(), ex, "Query failed while writing a comms event to the log: query = " + sqlCommand.CommandText);
                                    }
                                    Thread.Sleep(1000);
                                }
                            }
                        }

                        Thread.Sleep(200);
                    }

                    if (conn.State == System.Data.ConnectionState.Open)
                        conn.Close();
                }
            }
        }

        private void ResetCommsBatch(StringBuilder sb, string table)
        {
            sb.Clear();
            sb.Append("Insert INTO ");
            sb.Append(table);
            sb.Append(" (FDAExecutionID, connectionID, DeviceAddress, Attempt, TimestampUTC1, TimestampUTC2, ElapsedPeriod, TransCode, TransStatus, ApplicationMessage,DBRGUID,DBRGIdx,DBRGSize,Details01,TxSize,Details02,RxSize,ProtocolNote,Protocol) values ");
        }


        private NpgsqlDataReader PG_ExecuteDataReader(string sql, ref NpgsqlConnection conn)
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

            using (NpgsqlConnection conn = new NpgsqlConnection(SystemDBConnectionString))
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
                        Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "PG_ExecuteScalar() Failed to execute query after " + (maxRetries + 1) + " attempts. Error=" + ex.Message + ",Query = " + sql);
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
            using (NpgsqlConnection conn = new NpgsqlConnection(SystemDBConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "PG_ExecuateScalar() Failed to connect to database");
                    return null;
                }


                using (NpgsqlCommand sqlCommand = conn.CreateCommand())
                {
                    sqlCommand.CommandText = sql;

                    try
                    {
                        return sqlCommand.ExecuteScalar();
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

        private int ExecuteSQLSync(string sql, bool isScalar = false)
        {
            int result = 0;
            using (NpgsqlConnection conn = new NpgsqlConnection(SystemDBConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    // failed to connect to DB, exit the DB write routine
                    LogApplicationError(Globals.FDANow(), ex, "Event Logger: Unable to connect to the application database");
                    return 0;
                }

                using (NpgsqlCommand sqlCommand = conn.CreateCommand())
                {

                    sqlCommand.CommandText = sql;

                    try
                    {
                        if (isScalar)
                            result = (int)sqlCommand.ExecuteScalar();
                        else
                            result = sqlCommand.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        LogApplicationError(Globals.FDANow(), ex, "System Manager: Query failed : " + sqlCommand.CommandText);
                        return 0;
                    }
                }
            }

            return result;
        }



        public void LogStartup(Guid instanceID, DateTime timestamp, string version)
        {
            string sql = "select cast(count(1) as integer) from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = 'fdastarts' and COLUMN_NAME = 'fdaversion';";
            int versioncolumnCheck = ExecuteSQLSync(sql, true);
            bool versionColExists = true;

            if (versioncolumnCheck < 1)
            {
                versionColExists = false;
                sql = "ALTER TABLE FDAStarts ADD FDAVersion varchar(50);";
                versionColExists = (ExecuteSQLSync(sql) == -1);
            }

            if (versionColExists)
            {
                sql = "insert into FDAStarts (FDAExecutionID,UTCTimestamp,FDAVersion) values ('" + instanceID.ToString() + "','" + Helpers.FormatDateTime(timestamp) + "','" + version + "');";
            }
            else
            {
                sql = "insert into FDAStarts(FDAExecutionID, UTCTimestamp) values('" + instanceID.ToString() + "', '" + Helpers.FormatDateTime(timestamp) + "'); ";
            }
          
            ExecuteSQLSync(sql, false);

        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stopwatch stopwatch = new Stopwatch();

                    LogApplicationEvent(this, "", "Stopping FDAConfig table monitor");
                    _appConfigMonitor?.StopListening();
                    _appConfigMonitor?.Dispose();

                    if (_bgCommsLogger != null)
                    {
                        if (_bgCommsLogger.IsBusy)
                            Globals.SystemManager.LogApplicationEvent(this, "", "Waiting for Comms Logger thread to finish and exit");

                        stopwatch.Reset();
                        stopwatch.Start();
                        while (_bgCommsLogger.IsBusy && stopwatch.Elapsed.Seconds < 5)
                            Thread.Sleep(50);
                        stopwatch.Stop();
                        _bgCommsLogger.Dispose();
                    }


                    if (_bgEventLogger != null)
                    {

                        if (_bgEventLogger.IsBusy)
                        {
                            Globals.SystemManager.LogApplicationEvent(this, "", "Waiting for event logger thread to finish and exit");
                            stopwatch.Reset();
                            stopwatch.Start();
                            while (_bgEventLogger.IsBusy && stopwatch.Elapsed.Seconds < 5)
                                Thread.Sleep(50);
                            stopwatch.Stop();
                        }
                        _bgEventLogger.Dispose();
                    }

                    Console2.Flush();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~FDASystemManager() {
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

    }



}
