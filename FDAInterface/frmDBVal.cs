using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using Common;
using System.Threading;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FDAInterface
{
    public partial class frmDBVal : Form
    {
        private BindingList<Error> errors;
        private List<string> validProtocols;
        private Dictionary<string, List<int>> validBackfillStructures;
        private List<string> validConnectionTypes;
        private List<string> validScheduleTypes;
        private List<string> confirmedTables;
        private Dictionary<string, string> tableIDNames;
        internal string FDADBConnString { get; set; }


        private HashSet<Guid> referencedTags;
        private HashSet<Guid> referencedGroups;
        private HashSet<Guid> referencedConnections;

        private Dictionary<Guid, FDADataPointDefinitionStructure> tags;
        private Dictionary<Guid, FDASourceConnections> connections;
        private Dictionary<Guid, FDADataBlockRequestGroup> groups;
        private Dictionary<Guid, FDARequestGroupScheduler> schedules;
        private Dictionary<Guid, FDARequestGroupDemand> demands;
        private Dictionary<Guid, FDADevice> devices;
        private Dictionary<string, List<string>> requiredColumns;

        private Dictionary<string, string> _dbSettings;

        private SqlConnection _FDADBConnection;

        private BackgroundWorker bgWorker;

        private string activityType;


        public frmDBVal()
        {
            InitializeComponent();

            menuitem_filterByID.Click += Menuitem_filterByID_Click;
            menu_item_retest.Click += Menu_item_retest_Click;

            // set up the background worker
            bgWorker = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = true

            };
            bgWorker.DoWork += BgWorker_DoWork;
            //bgWorker.ProgressChanged += BgWorker_ProgressChanged;
            bgWorker.RunWorkerCompleted += BgWorker_RunWorkerCompleted;

            btn_getCurrentDBConn.Enabled = FDAManagerContext._MQTTConnectionStatus;


            // initialize the connection status
            lbl_connstatus.Text = "Database connection status: Closed";

            // create the dictionaries for objects loaded from the database
            tags = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            connections = new Dictionary<Guid, FDASourceConnections>();
            groups = new Dictionary<Guid, FDADataBlockRequestGroup>();
            schedules = new Dictionary<Guid, FDARequestGroupScheduler>();
            demands = new Dictionary<Guid, FDARequestGroupDemand>();
            devices = new Dictionary<Guid, FDADevice>();

            // create the hashsets for the unreferenced object checks
            referencedTags = new HashSet<Guid>();
            referencedConnections = new HashSet<Guid>();
            referencedGroups = new HashSet<Guid>();

            // create a list of tables that have been confirmed to exist and have the required columns
            confirmedTables = new List<string>();

            // create a list of detected errors, to be displayed in the grid
            errors = new BindingList<Error>();

            // set up lookups for valid protocols, connection types, timer types, required database tables and , etc
            validProtocols = new List<string>();
            validProtocols.AddRange(new string[] { "ROC", "MODBUS", "MODBUSTCP", "ENRONMODBUS", "BSAP" });

            validBackfillStructures = new Dictionary<string, List<int>>();
            validBackfillStructures.Add("ROC", new List<int>(new int[] { 0 }));

            validConnectionTypes = new List<string>();
            validConnectionTypes.AddRange(new string[] { "ETHERNET", "SERIAL" });

            validScheduleTypes = new List<string>();
            validScheduleTypes.AddRange(new string[] { "REALTIME", "MONTHLY", "DAILY", "HOURLY" });


            requiredColumns = new Dictionary<string, List<string>>();
            requiredColumns.Add("DataPointDefinitionStructures", new List<string>(
                new string[]
                {
                    "DPDSEnabled",
                    "DPSType",
                    "read_scaling",
                    "read_scale_raw_low",
                    "read_scale_raw_high",
                    "read_scale_eu_low",
                    "read_scale_eu_high",
                    "write_scaling",
                    "write_scale_raw_low",
                    "write_scale_raw_high",
                    "write_scale_eu_low",
                    "write_scale_eu_high",
                    "backfill_enabled",
                    "backfill_data_ID",
                    "backfill_data_structure_type",
                    "backfill_data_lapse_limit",
                    "backfill_data_interval",
                    "log_last_data_enabled",
                    "DPDUID"
                }));
            requiredColumns.Add("AppLog", new List<string>(
                new string[]
                {
                    "FDAExecutionID",
                    "Timestamp",
                    "EventType",
                    "ObjectType",
                    "ObjectName",
                    "Description",
                    "ErrorCode",
                    "StackTrace"
                }));
            requiredColumns.Add("CommsLog", new List<string>(
                new string[]
                {
                    "FDAExecutionID",
                    "ConnectionID",
                    "TimestampUTC1",
                    "TimestampUTC2",
                    "Attempt",
                    "TransStatus",
                    "TransCode",
                    "ElapsedPeriod",
                    "DBRGUID",
                    "DBRGIdx",
                    "DBRGSize",
                    "Details01",
                    "TxSize",
                    "Details02",
                    "RxSize",
                    "ProtocolNote",
                    "ApplicationMessage"
                }));
            requiredColumns.Add("FDADataBlockRequestGroup", new List<string>(
              new string[]
              {
                    "Description",
                    "DRGEnabled",
                    "DPSType",
                    "DataPointBlockRequestListVals",
                    "CommsLogEnabled",
                    "DRGUID"
              }));

            requiredColumns.Add("FDALastDataValues", new List<string>(
               new string[]
               {
                    "DPDUID",
                    "value",
                    "timestamp",
                    "quality"
                }));

            requiredColumns.Add("FDARequestGroupDemand", new List<string>(
               new string[]
               {
                    "Description",
                    "FRGDEnabled",
                    "UTCTimeStamp",
                    "DestroyDRG",
                    "DestroyFRGD",
                    "Priority",
                    "RequestGroupList",
                    "CommsLogEnabled",
                    "FRGDUID"
                }));
            requiredColumns.Add("FDARequestGroupScheduler", new List<string>(
                 new string[]
                 {
                    "Description",
                    "FRGSEnabled",
                    "FRGSType",
                    "RealTimeRate",
                    "Year",
                    "Month",
                    "Day",
                    "Hour",
                    "Minute",
                    "Second",
                    "Priority",
                    "RequestGroupList",
                    "FRGSUID"
                 }));
            requiredColumns.Add("FDASourceConnections", new List<string>(
                new string[]
                {
                    "Description",
                    "SCType",
                    "SCDetail01",
                    "SCDetail02",
                    "RequestRetryDelay",
                    "SocketConnectionAttemptTimeout",
                    "MaxSocketConnectionAttempts",
                    "SocketConnectionRetryDelay",
                    "PostConnectionCommsDelay",
                    "InterRequestDelay",
                    "MaxRequestAttempts",
                    "RequestResponseTimeout",
                    "ConnectionEnabled",
                    "CommunicationsEnabled",
                    "CommsLogEnabled",
                    "SCUID"
                }));

            tableIDNames = new Dictionary<string, string>();
            tableIDNames.Add("DataPointDefinitionStructures", "DPDUID");
            tableIDNames.Add("FDADataBlockRequestGroup", "DRGUID");
            tableIDNames.Add("FDADevices", "device_id");
            tableIDNames.Add("FDARequestGroupDemand", "FRGDUID");
            tableIDNames.Add("FDARequestGroupScheduler", "FRGSUID");
            tableIDNames.Add("FDASourceConnections", "SCUID");

            // set up the filter controls
            foreach (DataGridViewColumn column in dgvErrorList.Columns)
            {
                filter_columns.Items.Add(column.Name);
            }
            filter_columns.SelectedItem = "ID";
            filter_comparator.SelectedIndex = 0;
            filter_severityFilter.SelectedIndex = 0;

            // set double buffering on the DGV
            SetDoubleBuffered(dgvErrorList);
        }


        public void UpdateFDAStatus(bool FDARunstatus)
        {
            btn_getCurrentDBConn.Enabled = FDARunstatus;
        }


        private SqlConnection DBConnect(string connectionString, bool eventsubscribe)
        {
            SqlConnection conn;
            
            try
            {
                lbl_connstatus.Text = "Database connection status: Connecting";
                conn = new SqlConnection(connectionString);
                if (eventsubscribe)
                {
                    conn.StateChange += _sqlConnection_StateChange;
                    conn.InfoMessage += _sqlConnection_InfoMessage;
                }
                conn.Open();
            }
            catch
            {              
                lbl_connstatus.Text = "Database connection status: Failed to connect";
                return null;
            }

            return conn;
        }

        private void _sqlConnection_InfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            MessageBox.Show("InfoMessage: " + e.Message);
        }

        private void _sqlConnection_StateChange(object sender, StateChangeEventArgs e)
        {
            SqlConnection conn = (SqlConnection)sender;
            lbl_connstatus.Text = "Database connection status: " + conn.State.ToString();
            if (conn.State == ConnectionState.Open)
            {
                lbl_connstatus.ForeColor = Color.Black;
                btn_start.Enabled = true;
                btn_disconnect.Enabled = true;
            }
            else
            {
                lbl_connstatus.ForeColor = Color.Red;
                btn_start.Enabled = false;
                btn_disconnect.Enabled = false;
            }
        }

        // user clicked the 'start validation' button
        private void btn_start_Click(object sender, EventArgs e)
        {
            EvaluateDB();
        }

        private void EvaluateDB()
        {
            btn_start.Enabled = false;
            lbl_validating.Text = "Validating....";
            lbl_validating.Visible = true;

            // reset from any previous validation
            errors.Clear();
            dgvErrorList.Rows.Clear();

            tags.Clear();
            connections.Clear();
            groups.Clear();
            schedules.Clear();
            demands.Clear();
            devices.Clear();

            referencedConnections.Clear();
            referencedGroups.Clear();
            referencedTags.Clear();
            confirmedTables.Clear();

            // run the validation background worker
            bgWorker.RunWorkerAsync();
        }

        // validation background worker
        private void BgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
     
             

            BindingList<Error> errList = new BindingList<Error>();

            activityType = "Checking Database pre-requisites";
            PreReqCheck(errList);


            // load DataPointDefinitionStructures, identify any that can't be loaded
            string alias = GetTableAlias("DataPointDefinitionStructures");
            if (confirmedTables.Contains(alias))
            {
                activityType = "Loading " + alias;
                LoadDataPointDefinitionStructures(errList);
            }

            // load FDASourceConnections, identify any that can't be loaded
            alias = GetTableAlias("FDASourceConnections");
            if (confirmedTables.Contains(alias))
            {
                activityType = "Loading " + alias;
                LoadFDASourceConnections(errList);
            }

            // load FDADataBlockRequestGroup, identify any that can't be loaded
            alias = GetTableAlias("FDADataBlockRequestGroup");
            if (confirmedTables.Contains(alias))
            {
                activityType = "Loading " + alias;
                LoadRequestGroups(errList);
            }

            // load FDARequestGroupScheduler, identify any that can't be loaded
            alias = GetTableAlias("FDARequestGroupScheduler");
            if (confirmedTables.Contains(alias))
            {
                activityType = "Loading " + alias;
                LoadSchedulers(errList);
            }

            // load FDARequestGroupDemand, identify any that can't be loaded
            alias = GetTableAlias("FDARequestGroupDemand");
            if (confirmedTables.Contains(alias))
            {
                activityType = "Loading " + alias;
                LoadDemands(errList);
            }

            // load optional FDADevices table (if it exists), identify any that can't be loaded
            alias = GetTableAlias("FDARequestGroupDemand");
            if (TableExists(alias))
            {
                activityType = "Loading " + alias;
                LoadDevices(errList);
            }

            // validate each object type
            activityType = "Validating DataPointDefinitions";
            ValidateDataPointDefinitionStructures(errList);

            activityType = "Validating Connections";
            ValidateConnections(errList);

            activityType = "Validating RequestGroups";
            ValidateRequestGroups(errList);

            activityType = "Validating Schedules";
            ValidateSchedules(errList);

            // check for orhpaned request groups
            activityType = "Checking for enabled but unreferenced request groups";
            UpdateUI( new UpdateStatus(activityType, ""));
            foreach (FDADataBlockRequestGroup group in groups.Values)
            {
                if (group.DRGEnabled)
                {
                    if (!referencedGroups.Contains(group.DRGUID))
                        AddError("FDADataBlockRequestGroup",errList, Error.ErrorStatus.Orphan, GetTableAlias("FDADataBlockRequestGroup"), group.DRGUID, "Group is enabled but not referenced by any schedule or demand",group.DRGEnabled);
                }
            }


            // check for orphaned tags    
            activityType = "checking for enabled but unreferenced tags";
            UpdateUI(new UpdateStatus(activityType, ""));
            foreach (FDADataPointDefinitionStructure tag in tags.Values)
            {
                if (tag.DPDSEnabled)
                {
                    if (!referencedTags.Contains(tag.DPDUID))
                        AddError("DataPointDefinitionStructures",errList, Error.ErrorStatus.Orphan, GetTableAlias("DataPointDefinitionStructures"), tag.DPDUID, "Tag is enabled, but not referenced by any request group",tag.DPDSEnabled);
                }
            }

            e.Result = errList;
        }

        // handle updates from the background worker
  /*
        private void BgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 0)
            {
                UpdateStatus status = (UpdateStatus)e.UserState;
                lbl_validating.Text = status.ActivityType;
                lbl_activityDetail.Text = status.Detail;
                lbl_validating.Visible = true;
                lbl_activityDetail.Visible = true;

                lbl_validating.Refresh();
                lbl_activityDetail.Refresh();
            }
        }
  */

        private void BgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btn_start.Enabled = true;
            errors = (BindingList<Error>)e.Result;
            lbl_validating.Text = "Database validation complete, " + errors.Count + " errors or warnings detected";

            dgvErrorList.DataSource = errors;

            FormatTable();
            lblTotalCount.Text = errors.Count.ToString();
            lbl_filteredCount.Text = lblTotalCount.Text;
            dgvErrorList.Refresh();

        }

        private string FindNulls(SqlDataReader reader)
        {
            StringBuilder nullColumns = new StringBuilder();
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                    {
                        nullColumns.Append(reader.GetName(i));
                        nullColumns.Append(", ");
                    }
                }
                if (nullColumns.Length > 0)
                    nullColumns.Remove(nullColumns.Length - 2, 2);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message + ". stacktrace: " + ex.StackTrace);
            }

            return nullColumns.ToString();

        }

        private void PreReqCheck(BindingList<Error> errList)
        {

            string query;
            int result = 0;
            string FDAdbName = _FDADBConnection.Database;
            using (SqlCommand sqlCommand = _FDADBConnection.CreateCommand())
            {
                // service broker is enabled
                query = "SELECT count(1) from SYS.databases where is_broker_enabled=1 and name ='" + FDAdbName + "'";
                sqlCommand.CommandText = query;
                result = (int)sqlCommand.ExecuteScalar();
                if (result < 1)
                    AddError("Database",errList, Error.ErrorStatus.Severe, "Database", Guid.Empty, "FDA application database '" + FDAdbName + " does not have service broker enabled",true);


                // all required tables exist
                string[] requiredTables = new string[] { GetTableAlias("DataPointDefinitionStructures"),GetTableAlias("FDADataBlockRequestGroup"), GetTableAlias("FDARequestGroupDemand"),GetTableAlias("FDARequestGroupScheduler"),GetTableAlias("FDASourceConnections"),GetTableAlias("AppLog"),GetTableAlias("CommsLog"),GetTableAlias("FDALastDataValues") };
                foreach (string tablename in requiredTables)
                    if (TableExists(tablename))
                        confirmedTables.Add(tablename);
                    else
                        AddError(tablename,errList, Error.ErrorStatus.Severe,tablename, Guid.Empty, "Required table not found: " + tablename,true);


                // all tables confirmed to exist have all the required columns, with the correct data types
                StringBuilder queryBuilder = new StringBuilder();
                Dictionary<string, List<string>> foundColumns = new Dictionary<string, List<string>>();
                queryBuilder.Append("select B.name as [table],A.name as [column] from sys.columns A right join(select name, object_id from sys.Tables where name in (");
                foreach (string tablename in confirmedTables)
                {
                    queryBuilder.Append("'");
                    queryBuilder.Append(tablename);
                    queryBuilder.Append("',");
                }
                queryBuilder.Remove(queryBuilder.Length - 1, 1);

                queryBuilder.Append("))B on B.object_id = A.object_id order by[table]");
                sqlCommand.CommandText = queryBuilder.ToString();
                string tableAlias;
                string column;
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        tableAlias = sqlDataReader.GetString(sqlDataReader.GetOrdinal("table"));
                        column = sqlDataReader.GetString(sqlDataReader.GetOrdinal("column"));
                        if (!foundColumns.ContainsKey(tableAlias))
                            foundColumns.Add(tableAlias, new List<string>());
                        foundColumns[tableAlias].Add(column);
                    }
                }

                HashSet<string> badtables = new HashSet<string>();
                string tableTrueName;
                foreach (string tblAlias in confirmedTables)
                {
                    tableTrueName = GetTableTrueName(tblAlias);
                    foreach (string reqCol in requiredColumns[tableTrueName])
                        if (!foundColumns[tblAlias].Contains(reqCol))
                        {
                            AddError(tblAlias,errList, Error.ErrorStatus.Severe, tblAlias, Guid.Empty, "Table is missing a required column '" + reqCol + "'",true);
                            badtables.Add(tblAlias);
                        }
                }

                if (badtables.Count > 0)
                {
                    foreach (string tablename in badtables)
                        confirmedTables.Remove(tablename);
                }
            }

        }

        private void LoadDataPointDefinitionStructures(BindingList<Error> errList)
        {
            string table = GetTableAlias("DataPointDefinitionStructures");
            string query = "select DPDUID,DPDSEnabled,DPSType,read_scaling,read_scale_raw_low,read_scale_raw_high,read_scale_eu_low,read_scale_eu_high,write_scaling,write_scale_raw_low,write_scale_raw_high,write_scale_eu_low,write_scale_eu_high,isnull(backfill_enabled,0) as backfill_enabled, isnull(backfill_data_id,-1) as backfill_data_id,isnull(backfill_data_structure_type,0) as backfill_data_structure_type,isnull(backfill_data_lapse_limit,60) as backfill_data_lapse_limit,isnull(backfill_data_interval,1) as backfill_data_interval from " + table;


            FDADataPointDefinitionStructure newTag = null;
            string columnName = "";
            string datatype = "";
            using (SqlCommand sqlCommand = _FDADBConnection.CreateCommand())
            {
                sqlCommand.CommandText = query;
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        try
                        {
                            // check for nulls
                            string nullcolumns = FindNulls(sqlDataReader);
                            if (nullcolumns.Length > 0)
                            {
                                bool enabled = true;
                                if (!nullcolumns.Contains("DPDSEnabled"))
                                {
                                    enabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("DPDSEnabled"));
                                }
                                AddError("DataPointDefinitionStructures",errList, Error.ErrorStatus.Severe, table, sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID")), "null value in column(s) " + nullcolumns,enabled);
                            }
                            else
                            {
                                // no nulls, load it in
                                newTag = new FDADataPointDefinitionStructure();
                                columnName = "DPDUID";
                                datatype = "UID";
                                newTag.DPDUID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID"));
                                UpdateUI( new UpdateStatus(activityType, "ID: " + newTag.DPDUID));
                                columnName = "DPDSEnabled";
                                datatype = "boolean";
                                newTag.DPDSEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("DPDSEnabled"));
                                columnName = "DPSType";
                                datatype = "string";
                                newTag.DPSType = sqlDataReader.GetString(sqlDataReader.GetOrdinal("DPSType"));
                                columnName = "read_scaling";
                                datatype = "boolean";
                                newTag.read_scaling = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("read_scaling"));
                                columnName = "read_scale_raw_low";
                                datatype = "double";
                                newTag.read_scale_raw_low = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("read_scale_raw_low"));
                                columnName = "read_scale_raw_high";
                                newTag.read_scale_raw_high = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("read_scale_raw_high"));
                                columnName = "read_scale_eu_low";
                                newTag.read_scale_eu_low = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("read_scale_eu_low"));
                                columnName = "read_scale_eu_high";
                                newTag.read_scale_eu_high = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("read_scale_eu_high"));
                                columnName = "write_scaling";
                                datatype = "boolean";
                                newTag.write_scaling = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("write_scaling"));
                                columnName = "write_scale_raw_low";
                                datatype = "double";
                                newTag.write_scale_raw_low = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("write_scale_raw_low"));
                                columnName = "write_scale_raw_high";
                                newTag.write_scale_raw_high = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("write_scale_raw_high"));
                                columnName = "write_scale_eu_low";
                                newTag.write_scale_eu_low = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("write_scale_eu_low"));
                                columnName = "write_scale_eu_high";
                                newTag.write_scale_eu_high = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("write_scale_eu_high"));
                                columnName = "backfill_enabled";
                                newTag.backfill_enabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("backfill_enabled"));
                                columnName = "backfill_data_id";
                                datatype = "Int32";
                                newTag.backfill_data_ID = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("backfill_data_id"));
                                columnName = "backfill_data_structure_type";
                                newTag.backfill_data_structure_type = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal("backfill_data_structure_type"));
                                columnName = "backfill_data_lapse_limit";
                                datatype = "double";
                                newTag.backfill_data_lapse_limit = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("backfill_data_lapse_limit"));
                                columnName = "backfill_data_interval";
                                newTag.backfill_data_interval = sqlDataReader.GetDouble(sqlDataReader.GetOrdinal("backfill_data_interval"));

                                tags.Add(newTag.DPDUID, newTag);
                            }
                        }
                        catch
                        {
                            if (newTag != null)
                                AddError("DatapointDefinitionStructures",errList, Error.ErrorStatus.Severe, table, newTag.DPDUID, "Unable to load row, cannot convert value for " + columnName + " to datatype '" + datatype + "'",newTag.DPDSEnabled);
                        }
                    }
                }
            }
        }

        private void LoadFDASourceConnections(BindingList<Error> errList)
        {
            string table = GetTableAlias("FDASourceConnections");
            string query = "select * from " + table;
            FDASourceConnections newConn = null;
            string columnName = "";
            string datatype = "";
            using (SqlCommand sqlCommand = _FDADBConnection.CreateCommand())
            {
                sqlCommand.CommandText = query;
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {

                        try
                        {
                            // check for nulls
                            string nullcolumns = FindNulls(sqlDataReader);
                            if (nullcolumns.Length > 0)
                            {
                                bool enabled = true;
                                if (!nullcolumns.Contains("ConnectionEnabled"))
                                {
                                    enabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("ConnectionEnabled"));
                                }
                                AddError("FDASourceConnections",errList, Error.ErrorStatus.Severe, table, sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID")), "null value in column(s) " + nullcolumns,enabled);
                            }
                            else
                            {
                                newConn = new FDASourceConnections();
                                columnName = "ConnectionEnabled";
                                datatype = "boolean";
                                newConn.ConnectionEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal(columnName));

                                columnName = "SCUID";
                                datatype = "UID";
                                newConn.SCUID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal(columnName));                                
                                columnName = "CommunicationsEnabled";
                                newConn.CommunicationsEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal(columnName));
                                columnName = "CommsLogEnabled";
                                newConn.CommunicationsEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal(columnName));
                                columnName = "RequestRetryDelay";
                                datatype = "Int32";
                                newConn.RequestRetryDelay = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "SocketConnectionAttemptTimeout";
                                newConn.SocketConnectionAttemptTimeout = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "MaxSocketConnectionAttempts";
                                newConn.MaxSocketConnectionAttempts = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "SocketConnectionRetryDelay";
                                newConn.SocketConnectionRetryDelay = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "PostConnectionCommsDelay";
                                newConn.SocketConnectionRetryDelay = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "InterRequestDelay";
                                newConn.InterRequestDelay = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "MaxRequestAttempts";
                                newConn.MaxRequestAttempts = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "RequestResponseTimeout";
                                newConn.RequestResponseTimeout = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Description";
                                datatype = "string";
                                newConn.Description = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));
                                columnName = "SCType";
                                newConn.SCType = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));
                                columnName = "SCDetail01";
                                newConn.SCDetail01 = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));
                                columnName = "SCDetail02";
                                newConn.SCDetail02 = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));

                                connections.Add(newConn.SCUID, newConn);
                            }
                        }
                        catch
                        {
                            if (newConn != null)
                                AddError("FDASourceConnections",errList, Error.ErrorStatus.Severe, table, newConn.SCUID, "Unable to load row, cannot convert value for " + columnName + " to datatype '" + datatype + "'",newConn.ConnectionEnabled);

                        }

                    }
                }
            }
        }

        private void LoadRequestGroups(BindingList<Error> errList)
        {
            string table = GetTableAlias("FDADataBlockRequestGroup");
            string query = "select * from " + table;
            FDADataBlockRequestGroup newGroup = null;
            string columnName = "";
            string datatype = "";
            using (SqlCommand sqlCommand = _FDADBConnection.CreateCommand())
            {
                sqlCommand.CommandText = query;
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        try
                        {
                            // check for nulls
                            string nullcolumns = FindNulls(sqlDataReader);
                            if (nullcolumns.Length > 0)
                            {
                                bool enabled = true;
                                if (!nullcolumns.Contains("DRGEnabled"))
                                {
                                    enabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("DRGEnabled"));
                                }
                                AddError("FDADataBlockRequestGroup",errList, Error.ErrorStatus.Severe, table, sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID")), "null value in column(s) " + nullcolumns,enabled);
                            }
                            else
                            {
                                newGroup = new FDADataBlockRequestGroup();
                                columnName = "DRGEnabled";
                                datatype = "boolean";
                                newGroup.DRGEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal(columnName));

                                columnName = "DRGUID";
                                datatype = "UID";
                                newGroup.DRGUID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal(columnName));
                                
                                columnName = "CommsLogEnabled";
                                newGroup.CommsLogEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal(columnName));
                                columnName = "DPSType";
                                datatype = "string";
                                newGroup.DPSType = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Description";
                                newGroup.Description = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));
                                columnName = "DataPointBlockRequestListVals";
                                newGroup.DataPointBlockRequestListVals = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));

                                groups.Add(newGroup.DRGUID, newGroup);
                            }
                        }
                        catch
                        {
                            if (newGroup != null)
                                AddError("FDADataBlockRequestGroup",errList, Error.ErrorStatus.Severe, table, newGroup.DRGUID, "Unable to load row, cannot convert value for " + columnName + " to datatype '" + datatype + "'",newGroup.DRGEnabled);
                        }
                    }
                }
            }
        }

        private void LoadSchedulers(BindingList<Error> errList)
        {
            string table = GetTableAlias("FDARequestGroupScheduler");
            string query = "select * from " + table;
            FDARequestGroupScheduler newSchedule = null;
            string columnName = "";
            string datatype = "";
            using (SqlCommand sqlCommand = _FDADBConnection.CreateCommand())
            {
                sqlCommand.CommandText = query;
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        try
                        {
                            // check for nulls
                            string nullcolumns = FindNulls(sqlDataReader);
                            if (nullcolumns.Length > 0)
                            {
                                bool enabled = true;
                                if (!nullcolumns.Contains("DRGEnabled"))
                                {
                                    enabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("DRGEnabled"));
                                }
                                AddError("FDARequestGroupScheduler",errList, Error.ErrorStatus.Severe, table, sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID")), "null value in column(s) " + nullcolumns,enabled);
                            }
                            else
                            {
                                newSchedule = new FDARequestGroupScheduler();
                                columnName = "FRGSEnabled";
                                datatype = "boolean";
                                newSchedule.FRGSEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal(columnName));

                                columnName = "FRGSUID";
                                datatype = "UID";
                                newSchedule.FRGSUID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal(columnName));
                              
                                columnName = "RealTimeRate";
                                datatype = "Int32";
                                newSchedule.RealTimeRate = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Year";
                                newSchedule.Year = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Month";
                                newSchedule.Month = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Day";
                                newSchedule.Day = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Hour";
                                newSchedule.Hour = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Minute";
                                newSchedule.Minute = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Second";
                                newSchedule.Second = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Priority";
                                newSchedule.Priority = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "RequestGroupList";
                                datatype = "string";
                                newSchedule.RequestGroupList = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));
                                columnName = "FRGSType";
                                newSchedule.FRGSType = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Description";
                                newSchedule.Description = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));

                                schedules.Add(newSchedule.FRGSUID, newSchedule);
                            }

                        }
                        catch
                        {
                            if (newSchedule != null)
                                AddError("FDARequestGroupScheduler",errList, Error.ErrorStatus.Severe, table, newSchedule.FRGSUID, "Unable to load row, cannot convert value for " + columnName + " to datatype '" + datatype + "'",newSchedule.FRGSEnabled);
                        }
                    }
                }
            }
        }

        private void LoadDemands(BindingList<Error> errList)
        {
            string table = GetTableAlias("FDARequestGroupDemand");
            string query = "select * from " + table;
            FDARequestGroupDemand newDemand = null;
            string columnName = "";
            string datatype = "";
            using (SqlCommand sqlCommand = _FDADBConnection.CreateCommand())
            {
                sqlCommand.CommandText = query;
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        try
                        {
                            // check for nulls
                            string nullcolumns = FindNulls(sqlDataReader);
                            if (nullcolumns.Length > 0)
                            {
                                bool enabled = true;
                                if (!nullcolumns.Contains("FRGDEnabled"))
                                {
                                    enabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal("FRGDEnabled"));
                                }
                                AddError("FDARequestGroupDemand",errList, Error.ErrorStatus.Severe, table, sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID")), "null value in column(s) " + nullcolumns,enabled);
                            }
                            else
                            {
                                newDemand = new FDARequestGroupDemand();
                                datatype = "boolean";
                                columnName = "FRGDEnabled";
                                newDemand.FRGDEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal(columnName));

                                columnName = "FRGDUID";
                                datatype = "UID";
                                newDemand.FRGDUID = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal(columnName));
                                columnName = "DestroyDRG";
                                datatype = "boolean";
                                newDemand.DestroyDRG = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal(columnName));
                                columnName = "DestroyFRGD";
                                newDemand.DestroyFRGD = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal(columnName));
               
                                columnName = "CommsLogEnabled";
                                newDemand.CommsLogEnabled = sqlDataReader.GetBoolean(sqlDataReader.GetOrdinal(columnName));
                                columnName = "UTCTimeStamp";
                                datatype = "DateTime";
                                newDemand.UTCTimeStamp = sqlDataReader.GetDateTime(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Priority";
                                datatype = "Int32";
                                newDemand.Priority = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "RequestGroupList";
                                datatype = "string";
                                newDemand.RequestGroupList = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));
                                columnName = "Description";
                                newDemand.RequestGroupList = sqlDataReader.GetString(sqlDataReader.GetOrdinal(columnName));

                                demands.Add(newDemand.FRGDUID, newDemand);
                            }
                        }
                        catch
                        {
                            if (newDemand != null)
                                AddError("FDARequestGroupDemand",errList, Error.ErrorStatus.Severe, table, newDemand.FRGDUID, "Unable to load row, cannot convert value for " + columnName + " to datatype '" + datatype + "'",newDemand.FRGDEnabled);
                        }
                    }
                }
            }
        }

        private void LoadDevices(BindingList<Error> errList)
        {
            string table = GetTableAlias("FDADevices");
            string query = "select * from " + table;
            FDADevice newDevice = null;
            string columnName = "";
            string datatype = "";
            using (SqlCommand sqlCommand = _FDADBConnection.CreateCommand())
            {
                sqlCommand.CommandText = query;
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        try
                        {
                            // check for nulls
                            string nullcolumns = FindNulls(sqlDataReader);
                            if (nullcolumns.Length > 0)
                            {             
                                AddError("FDADevices",errList, Error.ErrorStatus.Severe, table, sqlDataReader.GetGuid(sqlDataReader.GetOrdinal("DPDUID")), "null value in column(s) " + nullcolumns,true);
                            }
                            else
                            {
                                newDevice = new FDADevice();
                                columnName = "device_id";
                                datatype = "UID";
                                newDevice.device_id = sqlDataReader.GetGuid(sqlDataReader.GetOrdinal(columnName));
                                columnName = "request_timeout";
                                datatype = "Int32";
                                newDevice.request_timeout = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "max_request_attempts";
                                newDevice.max_request_attempts = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "inter_request_delay";
                                newDevice.inter_request_delay = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                columnName = "request_retry_delay";
                                newDevice.request_retry_delay = sqlDataReader.GetInt32(sqlDataReader.GetOrdinal(columnName));
                                devices.Add(newDevice.device_id, newDevice);
                            }
                        }
                        catch
                        {
                            if (newDevice != null)
                                AddError("FDADevices",errList, Error.ErrorStatus.Severe, table, newDevice.device_id, "Unable to load row, cannot convert value for " + columnName + " to datatype '" + datatype + "'",true);
                        }
                    }
                }
            }
        }

        private void ValidateDataPointDefinitionStructures(BindingList<Error> errList)
        {
            string tablename = GetTableAlias("DataPointDefinitionStructures");
            Error.ErrorStatus severity;

            foreach (FDADataPointDefinitionStructure tag in tags.Values)
            {
                UpdateUI( new UpdateStatus(activityType, tag.DPDUID.ToString()));

                if (tag.DPDSEnabled)
                    severity = Error.ErrorStatus.Severe;
                else
                    severity = Error.ErrorStatus.Warning;

                // valid DPSType
                if (!validProtocols.Contains(tag.DPSType.ToUpper()))
                {
                    AddError("DataPointDefinitionStructures",errList, severity, tablename, tag.DPDUID, "The protocol '" + tag.DPSType + "' is not recognized",tag.DPDSEnabled);
                }

                if (tag.read_scaling)
                {
                    if (tag.read_scale_raw_high == tag.read_scale_raw_low)
                        AddError("DataPointDefinitionStructures", errList, severity, tablename, tag.DPDUID, "read scaling is enabled, but the raw high and raw low values are equal. The read scaling can not be applied",tag.DPDSEnabled);

                    if (tag.read_scale_raw_low > tag.read_scale_raw_high)
                        AddError("DataPointDefinitionStructures",errList, severity, tablename, tag.DPDUID, "read scaling is enabled, but the raw low is higher than the raw high. The read scaling can not be applied",tag.DPDSEnabled);

                    if (tag.read_scale_eu_low > tag.read_scale_eu_high)
                        AddError("DataPointDefinitionStructures", errList, severity, tablename, tag.DPDUID, "read scaling is enabled, but the EU low is higher than the EU high. The read scaling can not be applied",tag.DPDSEnabled);

                }

                if (tag.write_scaling)
                {
                    if (tag.write_scale_raw_high == tag.write_scale_raw_low)
                        AddError("DataPointDefinitionStructures", errList, severity, tablename, tag.DPDUID, "write scaling is enabled, but the raw high and raw low values are equal. The read scaling can not be applied", tag.DPDSEnabled);

                    if (tag.write_scale_raw_low > tag.write_scale_raw_high)
                        AddError("DataPointDefinitionStructures", errList, severity, tablename, tag.DPDUID, "write scaling is enabled, but the raw low is higher than the raw high. The read scaling can not be applied", tag.DPDSEnabled);

                    if (tag.write_scale_eu_low > tag.write_scale_eu_high)
                        AddError("DataPointDefinitionStructures", errList, severity, tablename, tag.DPDUID, "write scaling is enabled, but the EU low is higher than the EU high. The read scaling can not be applied", tag.DPDSEnabled);

                }

                if (tag.backfill_enabled)
                {
                    if (tag.backfill_data_ID < 1)
                        AddError("DataPointDefinitionStructures", errList, severity, tablename, tag.DPDUID, "Backfill is enabled, but the backfill data ID is less than 1", tag.DPDSEnabled);

                    if (!validBackfillStructures.ContainsKey(tag.DPSType.ToUpper()))
                    {
                        AddError("DataPointDefinitionStructures", errList, severity, tablename, tag.DPDUID, "Backfill is enabled, but backfill is not currently supported for the " + tag.DPSType + " protocol", tag.DPDSEnabled);
                    }
                    else
                    {
                        if (!validBackfillStructures[tag.DPSType.ToUpper()].Contains(tag.backfill_data_structure_type))
                        {
                            AddError("DataPointDefinitionStructures", errList, severity, tablename, tag.DPDUID, "Backfill is enabled, but the backfill_data_structure type " + tag.backfill_data_structure_type + " is not supported by the " + tag.DPSType + " protocol", tag.DPDSEnabled);
                        }
                    }
                }


            }
        }

        private void ValidateConnections(BindingList<Error> errList)
        {
            string tablename = GetTableAlias("FDASourceConnections");
            Error.ErrorStatus severity;
            foreach (FDASourceConnections conn in connections.Values)
            {
                UpdateUI( new UpdateStatus(activityType, conn.SCUID.ToString()));
                if (conn.ConnectionEnabled)
                    severity = Error.ErrorStatus.Severe;
                else
                    severity = Error.ErrorStatus.Warning;

                if (!validConnectionTypes.Contains(conn.SCType.ToUpper()))
                {
                    AddError("FDASourceConnections",errList, severity, tablename, conn.SCUID, "Unrecognized connection type '" + conn.SCType + "'",conn.ConnectionEnabled);
                }
            }
        }

        private void ValidateRequestGroups(BindingList<Error> errList)
        {
            string tablename = GetTableAlias("FDADataBlockRequestGroup");
            Error.ErrorStatus severity;
            foreach (FDADataBlockRequestGroup group in groups.Values)
            {
                UpdateUI( new UpdateStatus(activityType, group.DRGUID.ToString()));
                if (group.DRGEnabled)
                    severity = Error.ErrorStatus.Severe;
                else
                    severity = Error.ErrorStatus.Warning;

                // valid DPSType
                if (!validProtocols.Contains(group.DPSType))
                    AddError("FDADataBlockRequestGroup",errList, severity, tablename, group.DRGUID, "The protocol '" + group.DPSType + "' is not recognized. The request string cannot be validated",group.DRGEnabled);

                // correct basic structure (header|data...)
                string[] parsed = group.DataPointBlockRequestListVals.Split('|');
                if (parsed.Length < 2)
                {
                    AddError("FDADataBlockRequestGroup",errList, severity, tablename, group.DRGUID, "The DataPointBlockRequestListVals must contain at least one | character",group.DRGEnabled);
                }

                // now ask the protocol to evaluate the DataPointBlockRequestListVals (invalid device and tag references are covered here)                string[] errorList = new string[0];
                string[] errorList = null;
                switch (group.DPSType)
                {
                    case "ROC": errorList = ROC.ROCProtocol.ValidateRequestString(group.DataPointBlockRequestListVals, tags, devices, ref referencedTags); break;
                    case "MODBUS": errorList = Modbus.ModbusProtocol.ValidateRequestString(group.DataPointBlockRequestListVals, tags, devices, ref referencedTags); break;
                    case "MODBUSTCP": errorList = Modbus.ModbusProtocol.ValidateRequestString(group.DataPointBlockRequestListVals, tags, devices, ref referencedTags); break;
                    case "ENRONMODBUS": errorList = Modbus.ModbusProtocol.ValidateRequestString(group.DataPointBlockRequestListVals, tags, devices, ref referencedTags); break;
                    case "BSAP": /*temporary*/ errorList = new string[0]; break;
                    default: errorList = new string[0]; break;
                }


                foreach (string error in errorList)
                {
                    string error2 = error;
                    Error.ErrorStatus severity2 = severity;
                    if (error2.Contains("<warning>"))
                    {
                        error2 = error2.Replace("<warning>", "");
                        severity2 = Error.ErrorStatus.Warning;
                    }
                    AddError("FDADataBlockRequestGroup",errList, severity2, tablename, group.DRGUID, error2,group.DRGEnabled);
                }
            }
        }

        private void ValidateSchedules(BindingList<Error> errList)
        {
            string tablename = GetTableAlias("FDARequestGroupScheduler");
            Error.ErrorStatus severity;
            foreach (FDARequestGroupScheduler sched in schedules.Values)
            {
                UpdateUI( new UpdateStatus(activityType, sched.FRGSUID.ToString()));

                if (sched.FRGSEnabled)
                    severity = Error.ErrorStatus.Severe;
                else
                    severity = Error.ErrorStatus.Warning;

                // Schedule type is valid
                if (!validScheduleTypes.Contains(sched.FRGSType.ToUpper()))
                {
                    AddError("FDARequestGroupScheduler",errList, severity, tablename, sched.FRGSUID, "Unrecognized scheduler type '" + sched.FRGSType + "'",sched.FRGSEnabled);
                }

                // for non-realtime schedules, make sure the date/time values are valid
                if (sched.FRGSType.ToUpper() == "REALTIME")
                {
                    if (sched.RealTimeRate < 1)
                    {
                        AddError("FDARequestGroupScheduler",errList, severity, tablename, sched.FRGSUID, "Realtime schedule with invalid rate (must be at least 1 second)",sched.FRGSEnabled);
                    }
                }
                else
                {
                    if (sched.Year < 1000)
                    {
                        AddError("FDARequestGroupScheduler", errList, severity, tablename, sched.FRGSUID, sched.FRGSType + " schedule with invalid year (requires 4 digit year)",sched.FRGSEnabled);
                    }
                    DateTime dt;
                    try
                    {
                        dt = new DateTime(sched.Year, sched.Month, sched.Day, sched.Hour, sched.Minute, 0);
                    }
                    catch
                    {
                        AddError("FDARequestGroupScheduler", errList, severity, tablename, sched.FRGSUID, sched.FRGSType + " schedule with invalid date/time value(s)",sched.FRGSEnabled);
                    }
                }

                // check the request group list
                string[] requestGroupList = sched.RequestGroupList.Split('|');
                string[] requestGroupParts;

                foreach (string requestGroup in requestGroupList)
                {
                    UpdateUI( new UpdateStatus(activityType, sched.FRGSUID.ToString() + " - RequestGroup '" + requestGroup + "'"));
                    requestGroupParts = requestGroup.Split(':');

                    // has the required three elements?
                    if (requestGroupParts.Length < 3)
                    {
                        AddError("FDARequestGroupScheduler", errList, severity, tablename, sched.FRGSUID, "RequestGroupList has an invalid requestGroup '" + requestGroup + "'. Format should be 'GroupID:ConnectionID:DestinationTable'",sched.FRGSEnabled);
                        continue; // don't bother continuing to check this group because it is missing required elements
                    }

                    // group reference is a valid GUID
                    Guid groupID;
                    if (!Guid.TryParse(requestGroupParts[0], out groupID))
                    {
                        AddError("FDARequestGroupScheduler", errList, severity, tablename, sched.FRGSUID, "RequestGroupList has an invalid reference: '" + requestGroupParts[0] + "' is not a valid ID",sched.FRGSEnabled);
                    }
                    else
                    {
                        // ID is valid, does it reference an existing group?
                        if (!groups.ContainsKey(groupID))
                        {
                            AddError("FDARequestGroupScheduler", errList, severity, tablename, sched.FRGSUID, "RequestGroupList has an invalid reference: The request group '" + requestGroupParts[0] + "' is not found", sched.FRGSEnabled);
                        }
                        else
                        {
                            referencedGroups.Add(groupID);
                            
                            // is the group enabled?
                            if (!groups[groupID].DRGEnabled)
                            {
                                AddError("FDARequestGroupScheduler", errList, Error.ErrorStatus.Warning, tablename, sched.FRGSUID, "RequestGroupList references a disabled RequestGroup '" + groupID + "'", sched.FRGSEnabled);
                            }
                        }
                    }

                    // connection reference is a valid GUID
                    Guid connID;
                    if (!Guid.TryParse(requestGroupParts[1], out connID))
                    {
                        AddError("FDARequestGroupScheduler", errList, severity, tablename, sched.FRGSUID, "RequestGroupList has an invalid reference: '" + requestGroupParts[1] + "' is not a valid ID",sched.FRGSEnabled);
                    }
                    else
                    {
                        // ID is valid, does it reference an existing group?
                        if (!connections.ContainsKey(connID))
                        {
                            AddError("FDARequestGroupScheduler", errList, severity, tablename, sched.FRGSUID, "RequestGroupList has an invalid reference: The connection '" + requestGroupParts[1] + "' is not found", sched.FRGSEnabled);
                        }
                        else
                        {
                            referencedConnections.Add(connID);

                            // is the connection enabled?
                            if (!connections[connID].ConnectionEnabled)
                                AddError("FDARequestGroupScheduler", errList, Error.ErrorStatus.Warning, tablename, sched.FRGSUID, "RequestGroupList references a disabled connection '" + connID + "'", sched.FRGSEnabled);

                            // does the connection have communications enabled?
                            if (!connections[connID].CommunicationsEnabled)
                                AddError("FDARequestGroupScheduler", errList, Error.ErrorStatus.Warning, tablename, sched.FRGSUID, "RequestGroupList references a connection with communications disabled '" + connID + "'", sched.FRGSEnabled);
                        }
                    }

                }
            }
        }

        private void UpdateUI(UpdateStatus updateStatus)
        {
          /*
            if (bgWorker_targetItem == Guid.Empty)
            {
                bgWorker.ReportProgress(0, updateStatus);
                Thread.Sleep(5);
            }
            */
        }

        private void AddError(string classname,BindingList<Error> errorList, Error.ErrorStatus severity, string table, Guid ID, string description, bool enabled)
        {

            Error error = new Error(classname, severity, table, ID.ToString(), description, enabled);
            errorList.Add(error);
 
            //bgWorker.ReportProgress(1, error);
        }

        private bool TableExists(string table)
        {
            int count = 0;
            using (SqlCommand sqlCommand = _FDADBConnection.CreateCommand())
            {
                sqlCommand.CommandText = "select count(1) from INFORMATION_SCHEMA.TABLES where TABLE_NAME = '" + table + "'";
                count = (int)sqlCommand.ExecuteScalar();
            }
            return (count > 0);
        }

        private class UpdateStatus
        {
            public string ActivityType;
            public string Detail;

            public UpdateStatus(string activity, String detail)
            {
                ActivityType = activity;
                Detail = detail;
            }
        }

        private class Error :INotifyPropertyChanged
        {
            public enum ErrorStatus { NoError, Severe, Warning, Orphan };

            private readonly string _class;
            private int _idx;
            private string _description;
            private ErrorStatus _status;

            
            public ErrorStatus Status { get { return _status; } }
            public string TableName { get; }
            public string ID { get; }
            public string Enabled { get; }
            public string Description { get { return _description; } }

            

            public event PropertyChangedEventHandler PropertyChanged;

            private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            public Error(string classname,ErrorStatus errorStatus, string table, string id, string description,bool enabled)
            {
                TableName = table;
                ID = id;
                _description = description;
                _status = errorStatus;
                if (enabled)
                { Enabled = "Enabled"; }
                else
                { Enabled = "Disabled"; }
                _class = classname;
            }

            internal string GetClass()
            {
                return _class;
            }

            internal void UpdateDescription(string desc)
            {
                _description = desc;
                NotifyPropertyChanged("Description");
            }

            internal void UpdateStatus(ErrorStatus status)
            {
                _status = status;
                NotifyPropertyChanged("Status");
            }

            internal void SetIdx(int idx)
            {
                _idx = idx;
            }

            internal int GetIdx()
            {
                return _idx;
            }

            
        }

        private void btn_GetCurrentDBConn_click(object sender, EventArgs e)
        {
            // get the connection string being used by the connected FDA
            if (FDADBConnString == "")
            {
                tb_SQLInstance.Text = "";
                tb_user.Text = "";
                tb_pass.Text = "";
                tb_DB.Text = "";

                return;
            }
            string[] connStringpieces = FDADBConnString.Split(';');
            string[] keyValuePair;
            Dictionary<string, string> connSettings = new Dictionary<string, string>();
            foreach (string piece in connStringpieces)
            {
                if (piece != "")
                {
                    keyValuePair = piece.Split('=');
                    connSettings.Add(keyValuePair[0].Trim().ToUpper(), keyValuePair[1].Trim());
                }
            }

            tb_SQLInstance.Text = connSettings["SERVER"];
            tb_user.Text = connSettings["USER"];
            tb_pass.Text = connSettings["PASSWORD"];
            tb_DB.Text = connSettings["DATABASE"];

        }


        private void btn_connect_Click(object sender, EventArgs e)
        {
            // get the connection string from the textbox on the form
            string instance = tb_SQLInstance.Text;
            string user = tb_user.Text;
            string pass = tb_pass.Text;
            string FDAdbOverride = tb_DB.Text;

            // build the connection string
            string FDASystemConnString = "Server=" + instance + ";user=" + user + ";password=" + pass + ";Database=FDASystem";
            btn_connect.Enabled = false;

            SqlConnection FDASystemConn = DBConnect(FDASystemConnString,false);
            if (FDASystemConn == null)
            {
                lbl_connstatus.Text = "Database connection status: Failed to connect";
                btn_connect.Enabled = true;
                return;
            }

            // get the FDA database name, any alternate table names    
            string query = "select * from FDAConfig where configtype in (0,2)";
            _dbSettings = new Dictionary<string, string>();
            string option;
            string value;

            using (SqlCommand sqlCommand = FDASystemConn.CreateCommand())
            {
                sqlCommand.CommandText = query;
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        option = sqlDataReader.GetString(sqlDataReader.GetOrdinal("OptionName"));
                        value = sqlDataReader.GetString(sqlDataReader.GetOrdinal("OptionValue"));
                        _dbSettings.Add(option, value);
                    }
                }
            }

            FDASystemConn.Close();
            FDASystemConn.Dispose();
            

            string FDAConnString = "";
            if (!_dbSettings.ContainsKey("FDASQLInstanceName") || !_dbSettings.ContainsKey("FDADBLogin") || !_dbSettings.ContainsKey("FDADBPass"))
            {
                MessageBox.Show("Unable to connect to the application database, FDASQLInstanceName, FDADBLogin, or FDADBPass is missing from the FDAConfig database");
                btn_connect.Enabled = true;
                return;
            }

            string FDAdb = "FDA";

            if (_dbSettings.ContainsKey("FDADBName"))
                FDAdb = _dbSettings["FDADBName"];

            if (FDAdbOverride != "")
                FDAdb = FDAdbOverride;


            FDAConnString += "Server=" + _dbSettings["FDASQLInstanceName"] + ";user=" + _dbSettings["FDADBLogin"] + ";password=" + _dbSettings["FDADBPass"] + ";Database=" + FDAdb;
            _FDADBConnection = DBConnect(FDAConnString,true);
            if (_FDADBConnection == null)
                btn_connect.Enabled = true;
        }

        private void btnApplyFilter_Click(object sender, EventArgs e)
        {
            string filterColumn = filter_columns.SelectedItem.ToString();
            string filterType = filter_comparator.SelectedItem.ToString();
            string filterText = filter_text.Text;
            bool enabledonly = cb_enabledonly.Checked;
            bool errorsonly = cb_errorsOnly.Checked;

            BindingList<Error> filtered;
            switch (filterColumn)
            {
                case "TableName": filtered = FilterByType(filterText, filterType, enabledonly,errorsonly); break;
                case "ID": filtered = FilterByID(filterText, filterType, enabledonly,errorsonly); break;
                case "Description": filtered = FilterByDescription(filterText, filterType, enabledonly,errorsonly); break;
                default: filtered = errors; break;

            }

            dgvErrorList.DataSource = filtered;
            FormatTable();
            lbl_filteredCount.Text = filtered.Count.ToString();
            dgvErrorList.Refresh();
        }

        private BindingList<Error> FilterBySeverity(string filter, string filtertype,bool enabledonly,bool errorsonly)
        {
            BindingList<Error> filtered = null;
            switch (filtertype)
            {
                case "equal to":
                    filtered = new BindingList<Error>(errors.Where(e => e.Status.ToString() == filter).ToList()); break;
                case "containing":
                    filtered = new BindingList<Error>(errors.Where(e => e.Status.ToString().Contains(filter)).ToList()); break;
                default: filtered = new BindingList<Error>(errors); break;
            }
            if (enabledonly)
                filtered = new BindingList<Error>(filtered.Where(e => e.Enabled == "Enabled").ToList());

            if (errorsonly)
                filtered = new BindingList<Error>(filtered.Where(e => e.Status == Error.ErrorStatus.Severe).ToList());

            return filtered;
        }

        private BindingList<Error> FilterByType(string filter, string filtertype,bool enabledonly,bool errorsonly)
        {
            BindingList<Error> filtered = null;
            switch (filtertype)
            {
                case "equal to":
                    filtered = new BindingList<Error>(errors.Where(e => e.TableName == filter).ToList()); break;
                case "containing":
                    filtered = new BindingList<Error>(errors.Where(e => e.TableName.Contains(filter)).ToList()); break;
                default: filtered = new BindingList<Error>(errors); break;
            }
            if (enabledonly)
                filtered = new BindingList<Error>(filtered.Where(e => e.Enabled == "Enabled").ToList());

            if (errorsonly)
                filtered = new BindingList<Error>(filtered.Where(e => e.Status == Error.ErrorStatus.Severe).ToList());

            return filtered;
        }

        private BindingList<Error> FilterByID(string filter, string filtertype, bool enabledonly, bool errorsonly)
        {
            BindingList<Error> filtered = null;
            switch (filtertype)
            {
                case "equal to":
                    filtered = new BindingList<Error>(errors.Where(e => e.ID == filter).ToList()); break;
                case "containing":
                    filtered = new BindingList<Error>(errors.Where(e => e.ID.Contains(filter)).ToList()); break;
                default: filtered = new BindingList<Error>(errors); break;
            }
            if (enabledonly)
                filtered = new BindingList<Error>(filtered.Where(e => e.Enabled == "Enabled").ToList());

            if (errorsonly)
                filtered = new BindingList<Error>(filtered.Where(e => e.Status == Error.ErrorStatus.Severe).ToList());
            return filtered;
        }

        private BindingList<Error> FilterByDescription(string filter, string filtertype,bool enabledonly,bool errorsonly)
        {
            BindingList<Error> filtered = null;
            switch (filtertype)
            {
                case "equal to":
                    filtered = new BindingList<Error>(errors.Where(e => e.Description == filter).ToList()); break;
                case "containing":
                    filtered= new BindingList<Error>(errors.Where(e => e.Description.Contains(filter)).ToList()); break;
                default: filtered = new BindingList<Error>(errors); break;
            }
            if (enabledonly)
                filtered = new BindingList<Error>(filtered.Where(e => e.Enabled == "Enabled").ToList());

            if (errorsonly)
                filtered = new BindingList<Error>(filtered.Where(e => e.Status == Error.ErrorStatus.Severe).ToList());


            return filtered;
        }

        private void btnClearFilter_Click(object sender, EventArgs e)
        {
            dgvErrorList.DataSource = errors;
            FormatTable();
            lbl_filteredCount.Text = errors.Count.ToString();
            dgvErrorList.Refresh();
        }

        private string GetTableAlias(string name)
        {
            if (_dbSettings.ContainsKey(name))
                return _dbSettings[name];
            else
                return name;

        }

        private string GetTableTrueName(string alias)
        {
            foreach (KeyValuePair<string,string> kvp in _dbSettings)
            {
                if (kvp.Value == alias)
                    return kvp.Key;
            }

            return null;
        }

        private void FormatTable(int rowIdx=-1)
        {
            if (dgvErrorList.Columns.Count < 5)
                return;        

            foreach (DataGridViewRow row in dgvErrorList.Rows)
            {
                if (rowIdx == -1 || rowIdx == row.Index)
                {
                    switch ((Error.ErrorStatus)row.Cells[0].Value)
                    {
                        case Error.ErrorStatus.Severe:
                            row.Cells[0].Style.BackColor = Color.Red;
                            row.Cells[0].Style.ForeColor = Color.Red;
                            row.Cells[0].Style.SelectionBackColor = Color.Crimson;
                            row.Cells[0].Style.SelectionForeColor = Color.Crimson;
                            break;
                        case Error.ErrorStatus.Warning:
                            row.Cells[0].Style.BackColor = Color.Yellow;
                            row.Cells[0].Style.ForeColor = Color.Yellow;
                            row.Cells[0].Style.SelectionBackColor = Color.FromArgb(240, 240, 0);
                            row.Cells[0].Style.SelectionForeColor = Color.FromArgb(240, 240, 0);
                            break;
                        case Error.ErrorStatus.Orphan:
                            row.Cells[0].Style.BackColor = Color.Orange;
                            row.Cells[0].Style.ForeColor = Color.Orange;
                            row.Cells[0].Style.SelectionBackColor = Color.DarkOrange;
                            row.Cells[0].Style.SelectionForeColor = Color.DarkOrange;
                            break;
                        case Error.ErrorStatus.NoError:
                            row.Cells[0].Style.BackColor = Color.LightGreen;
                            row.Cells[0].Style.ForeColor = Color.LightGreen;
                            row.Cells[0].Style.SelectionBackColor = Color.Green;
                            row.Cells[0].Style.SelectionForeColor = Color.Green;
                            break;
                    }

                    // set the colors in the enabled column
                    if ((string)row.Cells[3].Value == "Enabled")
                    {
                        row.Cells[3].Style.BackColor = Color.LightGreen;
                        row.Cells[3].Style.ForeColor = Color.LightGreen;
                        row.Cells[3].Style.SelectionBackColor = Color.DarkSeaGreen;
                        row.Cells[3].Style.SelectionForeColor = Color.DarkSeaGreen;
                    }
                    else
                    {
                        row.Cells[3].Style.BackColor = Color.Tomato;
                        row.Cells[3].Style.ForeColor = Color.Tomato;
                        row.Cells[3].Style.SelectionBackColor = Color.IndianRed;
                        row.Cells[3].Style.SelectionForeColor = Color.IndianRed;
                    }


                    // only change this stuff if we're doing a 'whole table' format, not just a particular row
                    if (rowIdx == -1)
                    {
                        // error status
                        dgvErrorList.Columns[0].HeaderText = "";
                        dgvErrorList.Columns[0].Width = 25;
                        dgvErrorList.Columns[0].Visible = true;

                        // TableName
                        dgvErrorList.Columns[1].HeaderText = "Table Name";
                        dgvErrorList.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;

                        // ID
                        dgvErrorList.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;

                        // enabled
                        dgvErrorList.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;
                        dgvErrorList.Columns[3].Width = 60;

                        // description
                        dgvErrorList.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }


                    
                }
            }
        }

        private void filter_columns_SelectedValueChanged(object sender, EventArgs e)
        {
            if (filter_columns.SelectedItem == null)
            {
                filter_severityFilter.Visible = false;
                return;
            }
            filter_comparator.Tag = filter_comparator.SelectedItem;
            if (filter_columns.SelectedItem.ToString() == "Severity")
            {
                filter_comparator.SelectedItem = "equal to";
                filter_comparator.Enabled = false;
                filter_severityFilter.Visible = true;
            }
            else
            {
                filter_severityFilter.Visible = false;
                filter_comparator.SelectedItem = filter_comparator.Tag;
                filter_comparator.Enabled = true;
            }
        }

        private void frmDBVal_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_FDADBConnection != null)
            {
                _FDADBConnection.Close();
                _FDADBConnection.Dispose();
                _FDADBConnection = null;
            }
        }

        private void dgvErrorList_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            DataGridView grid = (DataGridView)sender;

            System.Windows.Forms.SortOrder so = System.Windows.Forms.SortOrder.None;
            if (grid.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.None ||
                grid.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
            {
                so = System.Windows.Forms.SortOrder.Descending;
            }
            else
            {
                so = System.Windows.Forms.SortOrder.Ascending;
            }

            //set SortGlyphDirection after databinding otherwise will always be none 
            Sort(grid.Columns[e.ColumnIndex].Name, so);
            grid.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection = so;
            FormatTable();
        }

        private void Sort(string column, System.Windows.Forms.SortOrder sortOrder)
        {
            BindingList<Error> currentSource = (BindingList<Error>)dgvErrorList.DataSource;
            switch (column)
            {
                case "Status":
                    {
                        if (sortOrder == System.Windows.Forms.SortOrder.Ascending)
                        {
                            dgvErrorList.DataSource = new BindingList<Error>(currentSource.OrderBy(x => x.Status).ToList());
                        }
                        else
                        {
                            dgvErrorList.DataSource = new BindingList<Error>(currentSource.OrderByDescending(x => x.Status).ToList());
                        }
                        break;
                    }
                case "TableName":
                    {
                        if (sortOrder == System.Windows.Forms.SortOrder.Ascending)
                        {
                            dgvErrorList.DataSource = new BindingList<Error>(currentSource.OrderBy(x => x.TableName).ToList());
                        }
                        else
                        {
                            dgvErrorList.DataSource = new BindingList<Error>(currentSource.OrderByDescending(x => x.TableName).ToList());
                        }
                        break;
                    }
                case "ID":
                    {
                        if (sortOrder == System.Windows.Forms.SortOrder.Ascending)
                        {
                            dgvErrorList.DataSource = new BindingList<Error>(currentSource.OrderBy(x => x.ID).ToList());
                        }
                        else
                        {
                            dgvErrorList.DataSource = new BindingList<Error>(currentSource.OrderByDescending(x => x.ID).ToList());
                        }
                        break;
                    }
                case "Enabled":
                    {
                        if (sortOrder == System.Windows.Forms.SortOrder.Ascending)
                        {
                            dgvErrorList.DataSource = new BindingList<Error>(currentSource.OrderBy(x => x.Enabled).ToList());
                        }
                        else
                        {
                            dgvErrorList.DataSource = new BindingList<Error>(currentSource.OrderByDescending(x => x.Enabled).ToList());
                        }
                        break;
                    }
                case "Description":
                    {
                        if (sortOrder == System.Windows.Forms.SortOrder.Ascending)
                        {
                            dgvErrorList.DataSource = new BindingList<Error>(currentSource.OrderBy(x => x.Description).ToList());
                        }
                        else
                        {
                            dgvErrorList.DataSource = new BindingList<Error>(currentSource.OrderByDescending(x => x.Description).ToList());
                        }
                        break;
                    }
            }

        }

        public static void SetDoubleBuffered(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        private void dgvErrorList_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;
            

            int idx = e.RowIndex;
            string type = dgvErrorList.Rows[idx].Cells["TableName"].Value.ToString();
            string ID = dgvErrorList.Rows[idx].Cells["ID"].Value.ToString();
            string title = "Exception description: " + dgvErrorList.Rows[idx].Cells["Description"].Value.ToString();
            string subtitle = dgvErrorList.Rows[idx].Cells["TableName"].Value.ToString() + " record information";
            string IDColumn = "";
            if (tableIDNames.ContainsKey(type))
            {
                IDColumn = tableIDNames[type];
            }
            else
                return;

                string query;
            if (ID != "00000000-0000-0000-0000-000000000000")
            {
                query = "select * from " + type + " where " + IDColumn + " = '" + ID + "'";
            }
            else
                return;

            string[] columns;
            object[] values;
            using (SqlCommand sqlCommand = _FDADBConnection.CreateCommand())
            {
                sqlCommand.CommandText = query;
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    columns = Enumerable.Range(0, sqlDataReader.FieldCount).Select(sqlDataReader.GetName).ToArray();
                    sqlDataReader.Read();
                    values = new object[sqlDataReader.FieldCount];
                    sqlDataReader.GetValues(values);
                }
            }

            frmRowDetails details = new frmRowDetails(title,subtitle,columns, values);
            details.ShowDialog();
        }

        private void btn_disconnect_Click(object sender, EventArgs e)
        {
            _FDADBConnection.Close();
            _FDADBConnection.StateChange -= _sqlConnection_StateChange;
            _FDADBConnection.Dispose();
            _FDADBConnection = null;
            btn_connect.Enabled = true;
        }

 

        private void dgvErrorList_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            DataGridView dgv = (DataGridView)sender;
            if (e.RowIndex < 0)
                return;

            if (e.ColumnIndex != 2)
                return;

            if (e.Button != MouseButtons.Right)
                return;

            if (e.Button == MouseButtons.Right)
            {
                dgv.ClearSelection();
                dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected = true;
                menu_ID.Tag = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();
                menu_ID.Show(MousePosition);
            }
            
        }


        private void Menuitem_filterByID_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;

            filter_columns.SelectedIndex = 1;
            filter_comparator.SelectedIndex = 0;
            filter_text.Text = (string)menuItem.GetCurrentParent().Tag;
            btnApplyFilter_Click(btnApplyFilter, new EventArgs());
        }

        private void dgvErrorList_RowHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                dgvErrorList.ClearSelection();
                dgvErrorList.Rows[e.RowIndex].Selected = true;
                Error selError = (Error)dgvErrorList.Rows[e.RowIndex].DataBoundItem;
                selError.SetIdx(e.RowIndex);
                menu_row.Tag = selError;
                menu_row.Show(MousePosition);
            }
        }


        private void Menu_item_retest_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            Error selectedError = (Error)menuItem.GetCurrentParent().Tag;
            string ErrorClass = selectedError.GetClass();
            string ID = selectedError.ID;

            selectedError.UpdateStatus(Error.ErrorStatus.NoError);
            selectedError.UpdateDescription("yay, you fixed it!");
            FormatTable(selectedError.GetIdx());
        }

     
    }
}
