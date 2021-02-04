using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FDAInterface
{
    public partial class frmTransactions : Form
    {
        private string ConnString;
        private string CurrentExecutionID;
        private string ViewingExecutionID;
        private string connectionID;
        private SqlDataAdapter adapter;
        private DataSet ds;
        private List<Control> filterControls;
        private bool filterApplied = false;
        private bool FilterApplied { get { return filterApplied; } set { filterApplied = value; btnClearFilter.Enabled = value; } }

        private class FDAExecution
        {
            string _display;
            Guid _ID;
            bool _current;

            public Guid ID { get { return _ID; } }
            public bool IsCurrent { get { return _current; } }

            //Constructor
            public FDAExecution(string display,Guid executionID,bool current=false)
            {
                _display = display;
                _ID = executionID;
                _current = current;
            }


            public override string ToString()
            {
                return _display;
            }
        }


    public frmTransactions(string executionID,ConnDetailsCtrl.ConnDetails connDetails,string DBconnectionString)
        {
            InitializeComponent();

            lbl_connDescription.Text = connDetails.Description;
            ConnString = DBconnectionString;
            connectionID = connDetails.ID;
            CurrentExecutionID = executionID;
            ViewingExecutionID = executionID;
            SetDoubleBuffered(dataGridView1);
            filterControls = new List<Control>();
            filterControls.Add(filter_text);
            filterControls.Add(filter_boolean);
            filterControls.Add(filter_numeric);
            filterControls.Add(filter_timebetween);
            filterControls.Add(filter_timeBeforeAfter);

                
            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                filter_columns.Items.Add(column.Name);
            }
            filter_comparatorStr.SelectedIndex = 0;

            FDAExecution[] executionList = GetExecutionIDs();
            foreach (FDAExecution exec in executionList)
            {
                cb_execution.Items.Add(exec);
                if (exec.IsCurrent)
                    cb_execution.SelectedItem = exec;
            }

            cb_recordtype.SelectedIndex = 0;
        }

        private FDAExecution[] GetExecutionIDs()
        {

            string entry = "";
            Guid ID;
            string IDStr = "";
            bool isCurrentExec = false;
            List<FDAExecution> executionList = new List<FDAExecution>();
            using (SqlConnection conn = new SqlConnection(ConnString))
            {
                conn.Open();
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "select FDAExecutionID,min(TimestampUTC1) as [From],max(TimestampUTC1) as [To] from CommsLog group by FDAExecutionID  order by [From] desc";
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ID = reader.GetGuid(0);
                            IDStr = ID.ToString();
                            entry = "";
                            if (IDStr == CurrentExecutionID)
                            {
                                isCurrentExec = true;
                                entry = "Current Execution     ";

                            }
                            else
                            {
                                entry += reader.GetDateTime(1).ToString() + " to " + reader.GetDateTime(2).ToString() + "     ";
                                isCurrentExec = false;
                            }
                            entry += "ID: " + IDStr;

                            executionList.Add(new FDAExecution(entry,ID,isCurrentExec));
                        }
                    }
                }
                conn.Close();
            }
            return executionList.ToArray();
        }

        public static void SetDoubleBuffered(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        private int GetData()
        {
            SqlConnection conn = new SqlConnection(ConnString);
           
            
            dataGridView1.DataSource = null;
            BindingSource bs = new BindingSource();
    
            using (conn)
            {
                conn.Open();
                string query = "select TimestampUTC1,TimestampUTC2,Attempt,TransStatus,TransCode,ElapsedPeriod,DBRGUID,Details01,TxSize,Details02,RxSize,ProtocolNote,ApplicationMessage from CommsLog where FDAExecutionID = '" + ViewingExecutionID + "' and ConnectionID = '" +  connectionID + "' order by TimestampUTC1";
                adapter = new SqlDataAdapter(query, conn);
                ds = new DataSet();
                adapter.Fill(ds, "CommsLog");
                bs.DataSource = new DataView(ds.Tables[0]);
                dataGridView1.DataSource = bs;
                dataGridView1.Columns["TimestampUTC1"].DefaultCellStyle.Format = "yyyy/MM/dd hh:mm:ss.fff tt";
                dataGridView1.Columns["TimestampUTC2"].DefaultCellStyle.Format = "yyyy/MM/dd hh:mm:ss.fff tt";
                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    column.ReadOnly = true;
                    column.SortMode = DataGridViewColumnSortMode.Automatic;
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
                }
                dataGridView1.Columns[dataGridView1.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                dataGridView1.Sort(dataGridView1.Columns["TimestampUTC1"], ListSortDirection.Ascending);
            }

            if (FilterApplied)
                btnApplyFilter_Click(this, new EventArgs());

            return ds.Tables[0].Rows.Count;
        }


        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            GetData();
           
        }

        private void frmTransactions_Shown(object sender, EventArgs e)
        {
            GetData();
            cb_execution.SelectedIndexChanged += new System.EventHandler(this.cb_execution_SelectedIndexChanged);
        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (dataGridView1.DataSource == null)
                return;

            BindingSource bs = (BindingSource)dataGridView1.DataSource;
            DataView dv = (DataView)bs.DataSource;
            frmRowDetails detailsForm = new frmRowDetails("Comms Log Entry Details","",dv, e.RowIndex);
            detailsForm.ShowDialog();
        }

        private void btnApplyFilter_Click(object sender, EventArgs e)
        {
            if (filter_columns.SelectedIndex < 0)
                return;
            string filterColumn = filter_columns.SelectedItem.ToString();
            string filterType = filter_comparatorStr.SelectedItem.ToString();
            string filterText = filter_text.Text;

            Type columnDataType = dataGridView1.Columns[filter_columns.SelectedItem.ToString()].ValueType;

            if (ds == null)
                return;

            if (ds.Tables.Count < 1)
                return;

            DataView filteredView = new DataView(ds.Tables[0]);
            BindingSource filteredBS = new BindingSource();
            filteredBS.DataSource = filteredView;

            StringBuilder sb = new StringBuilder();
            sb.Append(filter_columns.SelectedItem);

            if (columnDataType == typeof(string))
            {
                if (filter_comparatorStr.SelectedItem.ToString() == "equals")
                    sb.Append("=");
                else
                    sb.Append(" like '%");

                sb.Append(filter_text.Text);

                if (filter_comparatorStr.SelectedItem.ToString() != "equals")
                    sb.Append("%");

                sb.Append("'");
            }
            else
            if (columnDataType == typeof(Boolean))
            {
                sb.Append("='");
                sb.Append(filter_boolean.SelectedItem.ToString());
                sb.Append("'");
            }
            else
            if (columnDataType == typeof(Guid))
            {
                sb.Append("='");
                sb.Append(filter_text.Text);
                sb.Append("'");
            }
            else
            if (columnDataType == typeof(DateTime))
            {
                switch(filter_comparatorTime.SelectedItem.ToString())
                {
                    case "Before":
                        sb.Append(" < '");
                        sb.Append(filter_timeBeforeAfter.Value.ToString());
                        sb.Append("'");
                        break;
                    case "After":
                        sb.Append(" > '");
                        sb.Append(filter_timeBeforeAfter.Value.ToString());
                        sb.Append("'");
                        break;
                    case "Between":
                        sb.Append(" > '");
                        sb.Append(filter_timeFrom.Value.ToString());
                        sb.Append("' and ");
                        sb.Append(filter_columns.SelectedItem.ToString());
                        sb.Append(" < '");
                        sb.Append(filter_timeTo.Value.ToString());
                        sb.Append("'");
                        break;
                }
            }
            else
            {
                // numeric
                switch (filter_comparatorNum.SelectedItem.ToString())
                {
                    case "equal to": sb.Append("="); break;
                    case "less than": sb.Append("<");  break;
                    case "greater than":sb.Append(">"); break;
                    case "less than or equal to": sb.Append("<="); break;
                    case "greater than or equal to": sb.Append(">="); break;

                }

                sb.Append(filter_numeric.Value);
            }

            if (cb_recordtype.SelectedItem.ToString()=="Connection only")
            {
                sb.Append(" and TransCode = 0");
            }
            else
            if (cb_recordtype.SelectedItem.ToString()=="Transaction only")
            {
                sb.Append(" and TransCode = 1");
            }

            filteredBS.Filter = sb.ToString();

            dataGridView1.DataSource = filteredBS;
            FilterApplied = true;
        }

        private void btnClearFilter_Click(object sender, EventArgs e)
        {
            dataGridView1.DataSource = ds.Tables[0];
            FilterApplied = false;
            filter_columns.SelectedIndex = -1;
            filter_text.Text = "";
            filter_numeric.Value = 0;
        }

        private void filter_columns_SelectedValueChanged(object sender, EventArgs e)
        {
            if (filter_columns.SelectedIndex < 0)
                return;

            Type columnDataType = dataGridView1.Columns[filter_columns.SelectedItem.ToString()].ValueType;

            if (columnDataType == typeof(string))
            {
                filter_comparatorStr.BringToFront();
                if (filter_comparatorStr.SelectedItem == null)
                    filter_comparatorStr.SelectedIndex = 0;
                ShowFilter(filter_text);
                return;
            }

            if (columnDataType == typeof(Guid))
            {
                filter_comparatorBool.BringToFront();
                if (filter_comparatorBool.SelectedItem == null)
                    filter_comparatorBool.SelectedIndex = 0;
                filter_comparatorBool.SelectedIndex = 0;
                ShowFilter(filter_text);
                return;
            }

            if (columnDataType == typeof(Boolean))
            {
                filter_comparatorBool.BringToFront();
                if (filter_boolean.SelectedItem == null)
                    filter_boolean.SelectedIndex = 0;
                ShowFilter(filter_boolean);                
                return;
            }

            if (columnDataType == typeof(DateTime))
            {
                filter_comparatorTime.BringToFront();
                if (filter_comparatorTime.SelectedItem == null)
                    filter_comparatorTime.SelectedIndex = 0;
                if (filter_comparatorTime.SelectedIndex >= 0)
                {
                    if (filter_comparatorTime.SelectedItem.ToString() == "Between")
                        ShowFilter(filter_timebetween);
                    else
                        ShowFilter(filter_timeBeforeAfter);
                }
                else
                    ShowFilter(null);

                return;
            }

            // all other types (numeric)
            if (filter_comparatorNum.SelectedItem == null)
                filter_comparatorNum.SelectedIndex = 0;
            filter_comparatorNum.BringToFront();
            ShowFilter(filter_numeric);

        }

        private void ShowFilter(Control showControl)
        {
            foreach (Control control in filterControls)
            {
                if (control == showControl)
                {
                    if (control.GetType() == typeof(ComboBox))
                        ((ComboBox)control).SelectedIndex = 0;

                    control.Visible = true;
                }
                else
                    control.Visible = false;
            }

            Refresh();
        }

        private void filter_comparatorTime_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (filter_comparatorTime.SelectedItem.ToString())
            {
                case "Before": ShowFilter(filter_timeBeforeAfter); break;
                case "After": ShowFilter(filter_timeBeforeAfter); break;
                case "Between": ShowFilter(filter_timebetween); break;
            }
        }

        private void btn_columnSelector_Click(object sender, EventArgs e)
        {
            string[] columns = new string[dataGridView1.ColumnCount];

            for (int idx = 0;idx < columns.Length;idx++)
            {
                columns[idx] = dataGridView1.Columns[idx].Name + "|" + dataGridView1.Columns[idx].Visible.ToString();
            }

            frmColumnSelector dialog = new frmColumnSelector(columns);

            DialogResult result = dialog.ShowDialog();
    
            if (result == DialogResult.OK)
            {
                List<string> selectedColumns = dialog.SelectedColumns;
                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    column.Visible = (selectedColumns.Contains(column.Name));
                }
            }
        }

        private void cb_execution_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedID = ((FDAExecution)cb_execution.SelectedItem).ID.ToString();           
            ViewingExecutionID = selectedID;
            int rows = GetData();
            lbl_recordcount.Text = rows + " Records found";
        }

    
    }
}
