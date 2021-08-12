using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace FDAManager
{
    public partial class QueueHistory : UserControl
    {
        // public delegate void StartCaptureHandler(object sender, EventArgs e);
        // public event StartCaptureHandler StartCapture;

        // public delegate void StopCaptureHandler(object sender, EventArgs e);
        // public event StopCaptureHandler StopCapture;

        public int Priority { get; set; }
        public Guid ConnectionID { get; set; }

        public string CurrentQueue { get { return ConnectionID.ToString() + "." + Priority; } }

        // private bool capturing = false;

        private class HistoryDataPoint
        {
            public int Priority { get; }
            public DateTime Timestamp { get; }
            public int Value { get; }

            public HistoryDataPoint(int priority, DateTime ts, int value)
            {
                Priority = priority;
                Timestamp = ts;
                Value = value;
            }
        }

        private enum mode { Graph, Tab };

        private mode _mode;

        private mode CurrentMode
        {
            get { return _mode; }
            set
            {
                btn_graphview.Enabled = (value == mode.Tab);
                btn_tabview.Enabled = !btn_graphview.Enabled;

                chart_queueHistory.Visible = (value == mode.Graph);
                gridviewPanel.Visible = (value == mode.Tab);
                _mode = value;
            }
        }

        public QueueHistory()
        {
            InitializeComponent();
            CurrentMode = mode.Graph;
        }

        public void NewDataPoint(int priority, int value, DateTime timestamp)
        {
            DataPoint newPoint = new();
            newPoint.SetValueXY(timestamp, value);

            // add a label to the new point
            //newPoint.Label = value.ToString();

            // remove the label from the previous point
            //if (chart_queueHistory.Series[priority].Points.Count > 0)
            //    chart_queueHistory.Series[priority].Points.Last().Label = null;

            // add the new point to the series in the chart
            chart_queueHistory.Series[priority].Points.Add(newPoint);
            BindingList<HistoryDataPoint> tabularData;

            // add the new data point to the table
            if (dgvQHist.DataSource == null)
            {
                tabularData = new BindingList<HistoryDataPoint>();
                dgvQHist.DataSource = tabularData;
                dgvQHist.Columns[1].DefaultCellStyle.Format = "hh:mm:ss.fff tt";
                dgvQHist.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                dgvQHist.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            else
            {
                tabularData = (BindingList<HistoryDataPoint>)dgvQHist.DataSource;
            }

            tabularData.Add(new HistoryDataPoint(priority, timestamp, value));
        }

        public void Clear()
        {
            for (int i = 0; i < chart_queueHistory.Series.Count; i++)
            {
                chart_queueHistory.Series[i].Points.Clear();
            }
            dgvQHist.DataSource = null;

            //if (capturing)
            //    btn_stopcapture_Click(this, new EventArgs());
        }

        private void btn_graphview_Click(object sender, EventArgs e)
        {
            CurrentMode = mode.Graph;
        }

        private void btn_tabview_Click(object sender, EventArgs e)
        {
            CurrentMode = mode.Tab;
        }

        private void btn_Export_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg = new();
            dlg.AddExtension = true;
            dlg.DefaultExt = "csv";
            dlg.Filter = "CSV|*.csv";
            string path = "";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                path = dlg.FileName;
            }
            if (path == "")
                return;
            try
            {
                StreamWriter fs = File.CreateText(path);

                BindingList<HistoryDataPoint> data = (BindingList<HistoryDataPoint>)dgvQHist.DataSource;

                fs.WriteLine("Timestamp,Queue Count");
                foreach (HistoryDataPoint point in data)
                {
                    fs.WriteLine(point.Timestamp.ToString("yyyy/MM/dd hh:mm:ss.fff tt") + ", " + point.Value);
                }
                fs.Close();
                fs = null;
            }
            catch (IOException ex)
            {
                MessageBox.Show(ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void priorityEnabled_CheckedChanged(object sender, EventArgs e)
        {
        }
    }
}