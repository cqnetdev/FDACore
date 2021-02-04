using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using uPLibrary.Networking.M2Mqtt;

namespace DBQueryTest
{
    public partial class Form1 : Form
    {
        private MqttClient mqtt;
        private delegate void DataReceivedHandler(byte[] data);

        string connectionstring = "Server=LAPTOP-HM4ID092; Database = FDA; user = Intricatesql; password = Intricate2790!;";
        public Form1()
        {
            InitializeComponent();
            mqtt = new MqttClient("127.0.0.1");
            mqtt.MqttMsgPublishReceived += Mqtt_MqttMsgPublishReceived;
            mqtt.Connect("dbtestapp", "FDA", "Intricate2790!");

            // subscribe to queries
            mqtt.Subscribe(new string[] { "DBQUERY/#" },new byte[] { 0 });
        }

    

        private void button1_Click(object sender, EventArgs e)
        {
            string query = textBox1.Text;
            string queryID = Guid.NewGuid().ToString();
            
            // subscribe to the result
            mqtt.Subscribe(new string[] { "DBQUERYRESULT/" + queryID },new byte[] { 0 });

            // publish the query
            mqtt.Publish("DBQUERY/" + queryID, Encoding.UTF8.GetBytes(query));

        }


        private void Mqtt_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            string[] topic = e.Topic.Split('/');

            if (topic[0] == "DBQUERY")
            {
                DoQuery(e.Message,topic[1]);
            }

            if (topic[0] == "DBQUERYRESULT")
            {
                HandleResult(e.Message);
            }
        }



        private void DoQuery(byte[] query,string queryID)
        {
            string querystring = Encoding.UTF8.GetString(query);

            SqlConnection conn = new SqlConnection(connectionstring);
            SqlDataAdapter da = new SqlDataAdapter();
            SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = querystring;
            da.SelectCommand = cmd;
            DataSet ds = new DataSet();

            conn.Open();
            da.Fill(ds);
            conn.Close();
            StringWriter sw = new StringWriter();
            ds.WriteXml(sw);

            mqtt.Publish("DBQUERYRESULT/" + queryID, Encoding.UTF8.GetBytes(sw.ToString()));

        }

        private void HandleResult(byte[] result)
        {
            if (dataGridView1.InvokeRequired)
            {
                Invoke(new DataReceivedHandler(HandleResult), new object[] { result });
            }
            else
            {
                string XMLresult = Encoding.UTF8.GetString(result);
                StringReader theReader = new StringReader(XMLresult);
                DataSet theDataSet = new DataSet();
                theDataSet.ReadXml(theReader);
                dataGridView1.DataSource = theDataSet.Tables[0];
            }
     
        }

    }
}
