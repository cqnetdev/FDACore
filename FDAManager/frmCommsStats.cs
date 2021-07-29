using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using uPLibrary.Networking.M2Mqtt;

namespace FDAManager
{
    public partial class frmCommsStats : Form
    {
        private readonly MqttClient _mqtt;
        private delegate void DataReceivedHandler(byte[] data);
        private string _queryID;
        private readonly string _dbtype;

        private delegate void ThreadsafeUpdateText(string text);

        public frmCommsStats(MqttClient mqtt,string DBType)
        {
            InitializeComponent();

            _dbtype = DBType;
            _mqtt = mqtt;
            _mqtt.MqttMsgPublishReceived += MqttMsgPublishReceived;
            _mqtt.Subscribe(new string[] { "FDA/DefaultCommsStatsTable" }, new byte[] { 1 });

        }

        private void CalcButton_Click(object sender, EventArgs e)
        {
            CalcButton.Enabled = false;
            progressBar.Visible = true;
            _queryID = Guid.NewGuid().ToString();

            DateTime calcstarttime = startTime.Value;
            DateTime calcendtime = endtime.Value;

            string startTimeString = calcstarttime.ToString("yyyy-MM-dd H:mm:ss.fff");
            string endTimeString = calcendtime.ToString("yyyy-MM-dd H:mm:ss.fff");
            
            string topic = "DBQUERY/" + _queryID;
            string fulldescription = description.Text.Replace("%timestamp%", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt"));
            ComboBoxConnection conn = (ComboBoxConnection)cb_connection.SelectedItem;

            // SQL Server version
            string query = "";
            switch (_dbtype)
            {
                case "SQLSERVER":
                    query = "EXECUTE CalcStats @StartTime = '" + startTimeString + "',@EndTime = '" + endTimeString + "',@returnResults = 1";
                    if (description.Text != "")
                    {
                        query += ",@description='" + fulldescription + "'";
                    }

                    if (cb_connection.SelectedItem != null && cb_connection.SelectedIndex > 0)
                    {
                        query += ",@connection = '" + conn.ID + "'";
                    }

                    if (device.Text != "")
                        query += ",@device = '" + device.Text + "'";

                    if (chkSaveToDB.Checked)
                    {
                        query += ",@saveOutput = 1";
                        if (outputtable.Text != "")
                            query += ",@outputTable = '" + outputtable.Text + "'";
                    }
                    else
                    {
                        query += ",@saveOutput = 0";
                    }
                    break;
                case "POSTGRESQL":
                    {
                        // start time, end time, return results,description
                        query = "SELECT * from calcstats('" + startTimeString + "','" + endTimeString + "',1::bit,'" + description.Text + "',";
                        
                        // connection filter
                        if (cb_connection.SelectedItem != null && cb_connection.SelectedIndex > 0)
                        {
                            query += "'" + conn.ID + "',";
                        }
                        else
                        {
                            query += "null,";
                        }

                        // device filter
                        if (device.Text != "")
                            query += "'" + device.Text + "',";
                        else
                            query += "null,";

                        // output table
                        if (chkSaveToDB.Checked && outputtable.Text != "")
                            query += "'" + outputtable.Text + "',";
                        else
                            query += "null,";

                        // save to DB enabled
                        if (chkSaveToDB.Checked)
                            query += "1::bit";
                        else
                            query += "0::bit";

                        query += ");";

                        break;
                    }
            }
      
            byte[] serializedQuery = Encoding.UTF8.GetBytes(query);

            // subscribe to the result
            _mqtt.Subscribe(new string[] { "DBQUERYRESULT/" + _queryID }, new byte[] { 0 });

            // publish the query request
            _mqtt.Publish(topic, serializedQuery);
        }


        private void UpdateTableLabel(string text)
        {
            if (this.InvokeRequired)
                Invoke(new ThreadsafeUpdateText(UpdateTableLabel), new object[] { text });
            else
                lbl_defaultOutput.Text = text;
        }

        private void MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            if (e.Topic == "FDA/DefaultCommsStatsTable")
            {
                UpdateTableLabel(Encoding.UTF8.GetString(e.Message));
                return;
            }

            string[] topic = e.Topic.Split('/');
            
            if (topic.Length < 2)
                return;

            if (topic[0] != "DBQUERYRESULT" || topic[1] != _queryID)
                return;

            // unsubscribe from results for this query
            _mqtt.Unsubscribe(new string[] { "DBQUERYRESULT/" + topic[1] });

            HandleResult(e.Message);
        }

        private void HandleResult(byte[] result)
        {
            if (dataGridView1.InvokeRequired)
            {
                Invoke(new DataReceivedHandler(HandleResult), new object[] { result });
            }
            else
            {
                CalcButton.Enabled = true;
                progressBar.Visible = false;
                try
                {
                    // convert the XML to a dataset that can be displayed in a datagridview
                    string XMLresult = Encoding.UTF8.GetString(result);
                    StringReader theReader = new(XMLresult);
                    DataSet theDataSet = new();
                    theDataSet.ReadXml(theReader);

                    if (theDataSet.Tables.Count == 0)
                    {
                        dataGridView1.DataSource = null;
                        MessageBox.Show("Unable to calculate sstatistics, there were no records found");
                    }
                    else
                        dataGridView1.DataSource = theDataSet.Tables[0];
                }
                catch
                {
                    MessageBox.Show(Encoding.UTF8.GetString(result));
                }

                
                




            }

        }

        private void Btn_export_Click(object sender, EventArgs e)
        {
            DataTable table = (DataTable)dataGridView1.DataSource;
            string desc = description.Text;

            // make sure the QueryResults folder exists (create it if it doesn't exist)
            string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string queryResultsFolder = System.IO.Path.GetDirectoryName(strExeFilePath) + "\\QueryResults";
            if (!System.IO.Directory.Exists(queryResultsFolder))
            {
                System.IO.Directory.CreateDirectory(queryResultsFolder);
            }

            // build the csv from the datatable
            StringBuilder sb = new();
            string[] columnNames = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
            sb.AppendLine(string.Join(",", columnNames));
            foreach (DataRow row in table.Rows)
            {
                string[] fields = row.ItemArray.Select(field => field.ToString()).ToArray();
                sb.AppendLine(string.Join(",", fields));
            }

            // get the filename
            DateTime currentTime = DateTime.Now;
            string filename;
            // if a description was supplied, use it as the start of the filename
            if (desc != "")
            {
                filename = desc;

                // but be sure to remove any invalid characters
                string invalid = new(Path.GetInvalidFileNameChars());
                foreach (char c in invalid)
                {
                    filename = filename.Replace(c.ToString(), "");
                }
            }
            else
                filename = "CommStat";  // default description if none was supplied
            saveFileDialog1.FileName = filename;

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                // save the file
                filename = saveFileDialog1.FileName;
                File.WriteAllText(filename, sb.ToString());
            }


        }

     

        private void FrmCommsStats_Load(object sender, EventArgs e)
        {
            string displayString = DateTime.Now.AddDays(-365).ToString("yyyy-MM-dd hh:mm:ss tt");
            startTime.Text =displayString;
        }

        private void DataGridView1_DataSourceChanged(object sender, EventArgs e)
        {
            btn_export.Enabled = (dataGridView1.DataSource != null);
            if (btn_export.Enabled)
            {
                btn_export.BackColor = Color.PaleGreen;
            }
            else
            {
                btn_export.BackColor = SystemColors.ControlDark;
            }
        }


        private class ComboBoxConnection
        {
            private readonly string Description;
            public string ID;

           
            public ComboBoxConnection(string id,string description)
            {
                Description = description;
                ID = id;
            }
            public override string ToString()
            {
                return Description;
            }
        }
        
        internal void SetConnectionList(Dictionary<Guid,frmMain2.ConnectionNode> connections)
        {
            List<frmMain2.ConnectionNode> connList = connections.Values.ToList();
            connList.Sort();
            foreach (frmMain2.ConnectionNode item in connList)
                cb_connection.Items.Add(new ComboBoxConnection(item.ID.ToString(),item.Description));
        }

        private void BtnSetStart_Click(object sender, EventArgs e)
        {
            int hoursago = (int)dayshoursago.Value;
            if (rbdays.Checked)
                hoursago *= 24;

            DateTime calcstartTime = DateTime.Now.Subtract(new TimeSpan(hoursago, 0, 0));
            startTime.Value = calcstartTime;
        }

        private void BtnSetEnd_Click(object sender, EventArgs e)
        {
            endtime.Value = DateTime.Now;
        }

        private void ChkSaveToDB_CheckedChanged(object sender, EventArgs e)
        {
            outputtable.Enabled = chkSaveToDB.Checked;
            outputtable.ReadOnly = !chkSaveToDB.Checked;

            if (outputtable.Enabled)
                outputtable.BackColor = SystemColors.Window;
            else
                outputtable.BackColor = SystemColors.Control;
        }
    }
}
