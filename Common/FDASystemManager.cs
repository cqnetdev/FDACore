using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;
using System.Text;
using System.Data;

namespace Common
{
    public abstract class FDASystemManager : IDisposable
    {
        public string SystemDBConnectionString { get; set; }
        public string AppDBConnectionString { get; set; }
        public double CommsLogMaxDays { get => _commsLogMaxDays; set => _commsLogMaxDays = value; }
        public double EventLogMaxDays { get => _eventLogMaxDays; set => _eventLogMaxDays = value; }
        public bool EnableDebugMessages { get => _enableDebugMessages; set => _enableDebugMessages = value; }

        private List<string> ReadOnlyOptions;

        private Dictionary<string, FDAConfig> _appConfig;


        private Dictionary<string, RocDataTypes> _rocDataTypes;
  

        private Dictionary<int, RocEventFormats> _RocEventFormats;
        
  

        private Queue<CommsLogItemBase> _commsLogInputBuffer;
        private Queue<EventLogItem> _eventLogInputBuffer;
        private BackgroundWorker _bgCommsLogger;
        private BackgroundWorker _bgEventLogger;
        private double _commsLogMaxDays = 1;
        private double _eventLogMaxDays = 1;
        private bool _enableDebugMessages;
        private readonly Guid _executionID;
        protected readonly string systemDBSQLInstance;
        protected readonly string systemDBLogin;
        protected readonly string systemDBPass;

        public delegate void ConfigChangeHandler(object sender, ConfigEventArgs e);
        public event ConfigChangeHandler ConfigChange;

   

        public FDASystemManager(string DBInstance, string systemDBName, string login, string pass, string version, Guid executionID)
        {
            Globals.SystemManager = this;

            // Default enable debug messages to true
            EnableDebugMessages = true;

            SystemDBConnectionString = GetSystemDBConnectionString(DBInstance,systemDBName,login,pass);
            
           

            systemDBSQLInstance = DBInstance;
            systemDBLogin = login;
            systemDBPass = pass;

            _executionID = executionID;

            _commsLogInputBuffer = new Queue<CommsLogItemBase>();
            _eventLogInputBuffer = new Queue<EventLogItem>();

            _bgCommsLogger = new BackgroundWorker();
            _bgCommsLogger.DoWork += _bgCommsLogger_DoWork;

            _bgEventLogger = new BackgroundWorker();
            _bgEventLogger.DoWork += _bgEventLogger_DoWork;


            // load the FDAConfig table from the database
            LoadAppConfigOptions();

            string FDASqlInstance = systemDBSQLInstance;
            string FDAdb = "FDA";
            string FDALogin = systemDBLogin;
            string FDAPass = systemDBPass;
            
            Dictionary<string,FDAConfig> options = GetAppConfig();

            if (options != null)
            {
                if (options.ContainsKey("FDADBName"))
                    FDAdb = options["FDADBName"].OptionValue;
            }

            AppDBConnectionString = GetAppDBConnectionString(FDASqlInstance, FDAdb,FDALogin,FDAPass);

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

          

            // load ROC lookup tables
            LoadRocLookupTables();

        }

        public string GetAppDBConnectionString()
        {
            return AppDBConnectionString;
        }


        protected abstract string GetSystemDBConnectionString(string instance,string dbname,string user,string pass);

        protected void ROCDataTypes_Notification(string operation,RocDataTypes dataType)
        {
            string message = "";

            switch (operation)
            {
                case "INSERT":
                    if (_rocDataTypes == null)
                        _rocDataTypes = new Dictionary<string, RocDataTypes>();
                    if (!_rocDataTypes.ContainsKey(dataType.Key))
                        _rocDataTypes.Add(dataType.Key,dataType);
                    message = "RocDataTypes new row - PointType:Parameter = " + dataType.Key;

                    break;
                case "DELETE":
                    if (_rocDataTypes != null)
                    {
                        if (_rocDataTypes.ContainsKey(dataType.Key))
                            _rocDataTypes.Remove(dataType.Key);
                    }
                    message = "RocDataTypes row deleted - PointType:Parameter = " + dataType.Key;

                    break;
                case "UPDATE":
                    if (_rocDataTypes == null)
                        _rocDataTypes = new Dictionary<string, RocDataTypes>();
                    if (_rocDataTypes.ContainsKey(dataType.Key))
                    {
                        _rocDataTypes.Remove(dataType.Key);
                        _rocDataTypes.Add(dataType.Key, dataType);
                    }
                    else
                        _rocDataTypes.Add(dataType.Key, dataType);
                    message = "RocDataTypes row updated - PointType:Parameter = " + dataType.Key;
                    break;
            }
            Globals.SystemManager.LogApplicationEvent(this, "", message);
        }


    

        protected void ROCEventsNotification(string operation,RocEventFormats eventFormat)
        {
            string message = "";

            switch (operation)
            {
                case "INSERT":
                    if (_RocEventFormats == null)
                        _RocEventFormats = new Dictionary<int, RocEventFormats>();
                    if (!_RocEventFormats.ContainsKey(eventFormat.POINTTYPE))
                        _RocEventFormats.Add(eventFormat.POINTTYPE, eventFormat);
                    message = "RocEventFormats new row: PointType = " + eventFormat.POINTTYPE;

                    break;
                case "DELETE":
                    if (_rocDataTypes != null)
                    {
                        if (_RocEventFormats.ContainsKey(eventFormat.POINTTYPE))
                            _RocEventFormats.Remove(eventFormat.POINTTYPE);
                    }
                    message = "RocEventFormats row deleted: PointType = " + eventFormat.POINTTYPE;

                    break;
                case "UPDATE":
                    if (_RocEventFormats == null)
                        _RocEventFormats = new Dictionary<int, RocEventFormats>();
                    if (_RocEventFormats.ContainsKey(eventFormat.POINTTYPE))
                    {
                        _RocEventFormats.Remove(eventFormat.POINTTYPE);
                        _RocEventFormats.Add(eventFormat.POINTTYPE, eventFormat);
                    }
                    else
                        _RocEventFormats.Add(eventFormat.POINTTYPE, eventFormat);

                    message = "RocEventFormats row updated: PointType = " + eventFormat.POINTTYPE;
                    break;
            }
            Globals.SystemManager.LogApplicationEvent(this, "", message);
        }



      


  

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
    

            int pointType = -1;
            DataTable result = ExecuteQuery(query,SystemDBConnectionString);
            foreach (DataRow row in result.Rows)
            {
                try
                {
                    pointType = (Int32)row["PointType"];
                    _RocEventFormats.Add(pointType,
                        new RocEventFormats()
                        {
                            POINTTYPE = (Int32)row["PointType"],
                            FORMAT = (Int32)row["Format"],
                            DescShort = (string)row["DescShort"],
                            DescLong = (string)"DescLong"
                        });
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to parse RocEventFormats PointType " + pointType);
                }
            }
                    
                
           


            //------------------ load RocDataTypes table into a Dictionary --------------------

            query = "select PointType,Parm,DataType,DescShort,DescLong from RocDataTypes";

            RocDataTypes datatypeEntry;

            pointType = -1;
            result = ExecuteQuery(query,SystemDBConnectionString);
            foreach (DataRow row in result.Rows)
            {
                try
                {
                    pointType =(Int32)row["PointType"];
                    datatypeEntry = new RocDataTypes()
                    {
                        POINTTYPE = (Int32)row["PointType"],
                        PARM = (Int32)row["Parm"],
                        DataType = (string)row["DataType"],
                        DescShort = (string)row["DescShort"],
                        DescLong = (string)row["DescLong"]
                    };

                    _rocDataTypes.Add(datatypeEntry.Key, datatypeEntry);
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to parse RocEventFormats PointType " + pointType);
                }
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
     
                query = "SELECT count(1) from (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'fdaconfig') A;";
                    
                object result = ExecuteScalar(query,SystemDBConnectionString);
                bool tableExists = false;
                if (result != null)
                {
                        tableExists = ((Int32)Convert.ChangeType(result,typeof(Int32)) > 0);
                }
                
                if (tableExists)
                {
                   query = "select OptionName,OptionValue,ConfigType from FDAConfig";
                    DataTable resultsTable = ExecuteQuery(query,SystemDBConnectionString);
                    foreach (DataRow row in resultsTable.Rows)
                    {
                        try
                        {
                            optionName = (string)row["OptionName"];
                            _appConfig.Add(optionName,
                                new FDAConfig()
                                {
                                    OPTIONNAME = optionName,
                                    OptionValue = (string)row["OptionValue"],
                                    ConfigType = (Int32)row["ConfigType"]
                                });
                        }
                        catch (Exception ex)
                        {
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Failed to parse option '" + optionName);
                            return false;
                        }
                    }  
                }     
            StartListening();
            return true;
        }

        protected abstract void StartListening();

       protected abstract string GetAppDBConnectionString(string instance, string db, string login, string pass);

   

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


        protected void AppConfigNotification(string changeType,FDAConfig config)
        {
            string restartMessage = " (change will be applied after a restart)";
            string message = "";

            /* Mar 9, 2020 Ignore BackfillDataLapseLimit global setting */
            if (config.OPTIONNAME == "BackfillDataLapseLimit")
                return;
            bool isReadOnly = false;
            if (changeType != "NONE")
            {
                isReadOnly = ReadOnlyOptions.Contains(config.OPTIONNAME);
                if (changeType == "INSERT")
                {
                    if (!isReadOnly)
                        _appConfig.Add(config.OPTIONNAME, config);

                    message = "FDAConfig new option entered : " +config.OPTIONNAME + " = " + config.OptionValue;

                    // publish the default comms stats table to MQTT
                    if (config.OPTIONNAME.ToUpper() == "COMMSSTATS")
                    {
                        PublishCommsStatsTable(config.OptionValue);
                    }
                }

                if (changeType == "UPDATE")
                {
                    if (_appConfig.ContainsKey(config.OPTIONNAME))
                    {
                        if (!isReadOnly)
                            _appConfig[config.OPTIONNAME] = config;

                        message = "FDAConfig option change : " + config.OPTIONNAME + " = " +config.OptionValue;

                        if (config.OPTIONNAME.ToUpper() == "COMMSSTATS")
                        {
                            PublishCommsStatsTable(config.OptionValue);
                        }
                    }
                    else
                    {
                        if (!isReadOnly)
                            _appConfig.Add(config.OPTIONNAME, config);

                        message = "FDAConfig new option : " + config.OPTIONNAME + " = " + config.OptionValue;
                        if (config.OPTIONNAME.ToUpper() == "COMMSSTATS")
                        {
                            PublishCommsStatsTable(config.OptionValue);
                        }
                    }
                }

                if (changeType == "DELETE")
                    if (_appConfig.ContainsKey(config.OPTIONNAME))
                    {
                        if (!isReadOnly)
                        {
                            if (_appConfig.ContainsKey(config.OPTIONNAME))
                            {
                                _appConfig.Remove(config.OPTIONNAME);
                            }
                        }
                        message = "FDAConfig option deleted, reverting to default : " + config.OPTIONNAME;

                        if (config.OPTIONNAME.ToUpper() == "COMMSSTATS")
                        {
                            PublishCommsStatsTable(config.OPTIONNAME);
                        }

                    }

                if (isReadOnly)
                    message += restartMessage;

                Globals.SystemManager.LogApplicationEvent(this, "", message);

                ConfigChange?.Invoke(this, new ConfigEventArgs(changeType, "FDAConfig", Guid.Empty));
            }
        }



    
         
        private void PublishCommsStatsTable(string table)
        {
            // publish changes to the default CommsStats output table to MQTT
            byte[] tableBytes = Encoding.UTF8.GetBytes(table);
            Globals.MQTT.Publish("FDA/DefaultCommsStatsTable", tableBytes, 0, true);
        }


    

        public void LogApplicationError(DateTime timestamp, Exception ex, string description = "")
        {
            string eventText = Helpers.FormatDateTime(timestamp) + ": " + ex.Message + ", " + description;
            
            // display it in the console if we're in interactive mode
            if (Environment.UserInteractive)
            {
                Console.WriteLine(eventText);
            }

            // send it to any terminal clients
            OperationalMessageServer.WriteLine(eventText);

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

            string eventText = Helpers.FormatDateTime(timestamp) + ": " + objectType + "(" + objectName + "), " + description;

            // write it to the console
            if (Globals.ConsoleMode && Environment.UserInteractive)
            {
                Console2.WriteLine(eventText);
            }

            // send it to any tcp clients (typically the ControllerService, which will forward it to remote clients, if any are connected)
            OperationalMessageServer.WriteLine(eventText);

            // write it to the database
            description = description.Replace("'", "''");
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
            string query;
            EventLogItem logItem;
            StringBuilder batchbuilder = new StringBuilder();
            string tblName = Globals.SystemManager.GetTableName("AppLog");
  

            while (_eventLogInputBuffer.Count > 0)
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

                query = batchbuilder.ToString();

                ExecuteNonQuery(query,AppDBConnectionString);

                Thread.Sleep(200);   // short break between batches gives other threads a chance to queue new events
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
            string query;

            while (_commsLogInputBuffer.Count > 0)
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
                    query = batchBuilder.ToString();
                    ExecuteNonQuery(query,AppDBConnectionString);
                }

                Thread.Sleep(200);
            }         
        }

        private void ResetCommsBatch(StringBuilder sb, string table)
        {
            sb.Clear();
            sb.Append("Insert INTO ");
            sb.Append(table);
            sb.Append(" (FDAExecutionID, connectionID, DeviceAddress, Attempt, TimestampUTC1, TimestampUTC2, ElapsedPeriod, TransCode, TransStatus, ApplicationMessage,DBRGUID,DBRGIdx,DBRGSize,Details01,TxSize,Details02,RxSize,ProtocolNote,Protocol) values ");
        }


        protected abstract DataTable ExecuteQuery(string sql,string dbConnString);
    

        protected abstract int ExecuteNonQuery(string sql,string dbConnString);
      
        protected abstract object ExecuteScalar(string sql,string dbConnString);
     

        private int ExecuteSQLSync(string sql,string connString, bool isScalar = false)
        {
            int result = 0;

            if (isScalar)
                result = (int)ExecuteScalar(sql,connString);
            else
                result = ExecuteNonQuery(sql,connString);
                 
            return result;
        }



        public void LogStartup(Guid instanceID, DateTime timestamp, string version)
        {
            string sql = "select cast(count(1) as integer) from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = 'fdastarts' and COLUMN_NAME = 'fdaversion';";
            int versioncolumnCheck = ExecuteSQLSync(sql,SystemDBConnectionString,true);
            bool versionColExists = true;

            if (versioncolumnCheck < 1)
            {
                versionColExists = false;
                sql = "ALTER TABLE FDAStarts ADD FDAVersion varchar(50);";
                versionColExists = (ExecuteSQLSync(sql,SystemDBConnectionString) == -1);
            }

            if (versionColExists)
            {
                sql = "insert into FDAStarts (FDAExecutionID,UTCTimestamp,FDAVersion) values ('" + instanceID.ToString() + "','" + Helpers.FormatDateTime(timestamp) + "','" + version + "');";
            }
            else
            {
                sql = "insert into FDAStarts(FDAExecutionID, UTCTimestamp) values('" + instanceID.ToString() + "', '" + Helpers.FormatDateTime(timestamp) + "'); ";
            }
          
            ExecuteSQLSync(sql,SystemDBConnectionString, false);

        }

        #region IDisposable Support
        protected abstract void Cleanup();

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stopwatch stopwatch = new Stopwatch();

                    Cleanup();

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

                    Globals.SystemManager.LogApplicationEvent(this, "", "Event logger thread closed");
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
