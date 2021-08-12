using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using uPLibrary.Networking.M2Mqtt;

namespace FDAManager
{
    public partial class frmCustomQuery : Form
    {
        private MqttClient _mqtt;

        private delegate void DataReceivedHandler(byte[] data);

        private string _queryID;

        public frmCustomQuery(MqttClient mqtt)
        {
            InitializeComponent();
            _mqtt = mqtt;
            _mqtt.MqttMsgPublishReceived += _mqtt_MqttMsgPublishReceived;
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            string UCaseQuery = textBox1.Text.ToUpper();
            if (!UCaseQuery.StartsWith("SELECT") || UCaseQuery.Contains("INSERT") || UCaseQuery.Contains("UPDATE") || UCaseQuery.Contains("DELETE") || UCaseQuery.Contains("DROP") || UCaseQuery.Contains("CREATE") || UCaseQuery.Contains("ALTER"))
            {
                MessageBox.Show("Only SELECT queries are permitted, please enter a query that starts with SELECT and does not contain the words INSERT, UPDATE, DELETE, CREATE, DROP, or ALTER", "Restricted query");
                return;
            }

            progressBar1.Visible = true;
            _queryID = Guid.NewGuid().ToString();
            string topic = "DBQUERY/" + _queryID;
            byte[] serializedQuery = Encoding.UTF8.GetBytes(textBox1.Text);

            // subscribe to the result
            _mqtt.Subscribe(new string[] { "DBQUERYRESULT/" + _queryID }, new byte[] { 0 });

            // publish the query request
            _mqtt.Publish(topic, serializedQuery);
        }

        private void _mqtt_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            string[] topic = e.Topic.Split('/');
            if (topic.Length < 2)
                return;

            if (topic[0] != "DBQUERYRESULT")
                return;
            if (topic[1] == _queryID)
            {
                // unsubscribe from results for this query
                _mqtt.Unsubscribe(new string[] { "DBQUERYRESULT/" + topic[1] });
                HandleResult(e.Message);
            }
        }

        private void HandleResult(byte[] result)
        {
            if (dataGridView1.InvokeRequired)
            {
                Invoke(new DataReceivedHandler(HandleResult), new object[] { result });
            }
            else
            {
                progressBar1.Visible = false;
                string rawResult = Encoding.UTF8.GetString(result);
                if (rawResult.StartsWith("Error"))
                {
                    dataGridView1.DataSource = null;
                    lbl_rowcount.Text = "(" + dataGridView1.Rows.Count + " rows)";
                    MessageBox.Show("SQL returned an error: " + rawResult, "Query Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // convert the XML to a dataset that can be displayed in a datagridview
                StringReader theReader = new(rawResult);
                DataSet theDataSet = new();
                theDataSet.ReadXml(theReader);
                if (theDataSet.Tables.Count > 0)
                {
                    dataGridView1.DataSource = theDataSet.Tables[0];
                    lbl_rowcount.Text = "(" + dataGridView1.Rows.Count + " rows)";
                }
                else
                {
                    dataGridView1.DataSource = null;
                    lbl_rowcount.Text = "(" + dataGridView1.Rows.Count + " rows)";
                }
            }
        }

        private void WriteDataTableToCSV(DataTable table)
        {
            StringBuilder sb = new();

            string[] columnNames = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
            sb.AppendLine(string.Join(",", columnNames));

            foreach (DataRow row in table.Rows)
            {
                string[] fields = row.ItemArray.Select(field => field.ToString()).ToArray();
                sb.AppendLine(string.Join(",", fields));
            }

            DateTime currentTime = DateTime.Now;
            string filename = "QueryResult_" + currentTime.Year + "-" + currentTime.Month + "-" + currentTime.Day + "_" + currentTime.Hour + "-" + currentTime.Minute + "-" + currentTime.Second + ".csv";

            File.WriteAllText(filename, sb.ToString());
        }

        private string DataTableToCSV(DataTable table)
        {
            StringBuilder sb = new();
            string[] columnNames = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray();
            sb.AppendLine(string.Join(",", columnNames));

            foreach (DataRow row in table.Rows)
            {
                string[] fields = row.ItemArray.Select(field => field.ToString()).ToArray();
                sb.AppendLine(string.Join(",", fields));
            }

            return sb.ToString();
        }

        private void btn_export_Click(object sender, EventArgs e)
        {
            String filename;
            if (saveFileDialog1.InitialDirectory == "")
            {
                string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                saveFileDialog1.InitialDirectory = System.IO.Path.GetDirectoryName(strExeFilePath) + "\\QueryResults";
            }
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                filename = saveFileDialog1.FileName;

                DataTable tbl = (DataTable)dataGridView1.DataSource;
                string CSV = DataTableToCSV(tbl);
                File.WriteAllText(filename, CSV);
            }
        }

        private void dataGridView1_DataSourceChanged(object sender, EventArgs e)
        {
            btnExport.Enabled = (dataGridView1.DataSource != null);
            if (btnExport.Enabled)
            {
                btnExport.BackColor = Color.PaleGreen;
            }
            else
            {
                btnExport.BackColor = SystemColors.ControlDark;
            }
        }
    }
}