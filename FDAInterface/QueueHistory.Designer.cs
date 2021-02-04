namespace FDAInterface
{
    partial class QueueHistory
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series2 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series3 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series4 = new System.Windows.Forms.DataVisualization.Charting.Series();
            this.btn_graphview = new System.Windows.Forms.Button();
            this.btn_tabview = new System.Windows.Forms.Button();
            this.btn_Export = new System.Windows.Forms.Button();
            this.bottompanel = new System.Windows.Forms.Panel();
            this.dgvQHist = new System.Windows.Forms.DataGridView();
            this.chart_queueHistory = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.DetailsPanel = new System.Windows.Forms.Panel();
            this.gridviewPanel = new System.Windows.Forms.Panel();
            this.pri3enb = new System.Windows.Forms.CheckBox();
            this.pri2enb = new System.Windows.Forms.CheckBox();
            this.pri1enb = new System.Windows.Forms.CheckBox();
            this.pri0enb = new System.Windows.Forms.CheckBox();
            this.bottompanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvQHist)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.chart_queueHistory)).BeginInit();
            this.DetailsPanel.SuspendLayout();
            this.gridviewPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // btn_graphview
            // 
            this.btn_graphview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_graphview.Enabled = false;
            this.btn_graphview.Location = new System.Drawing.Point(7, 3);
            this.btn_graphview.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btn_graphview.Name = "btn_graphview";
            this.btn_graphview.Size = new System.Drawing.Size(112, 35);
            this.btn_graphview.TabIndex = 13;
            this.btn_graphview.Text = "Graph View";
            this.btn_graphview.UseVisualStyleBackColor = true;
            this.btn_graphview.Visible = false;
            this.btn_graphview.Click += new System.EventHandler(this.btn_graphview_Click);
            // 
            // btn_tabview
            // 
            this.btn_tabview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_tabview.Location = new System.Drawing.Point(129, 3);
            this.btn_tabview.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btn_tabview.Name = "btn_tabview";
            this.btn_tabview.Size = new System.Drawing.Size(118, 35);
            this.btn_tabview.TabIndex = 14;
            this.btn_tabview.Text = "Tabular View";
            this.btn_tabview.UseVisualStyleBackColor = true;
            this.btn_tabview.Visible = false;
            this.btn_tabview.Click += new System.EventHandler(this.btn_tabview_Click);
            // 
            // btn_Export
            // 
            this.btn_Export.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_Export.Location = new System.Drawing.Point(985, 3);
            this.btn_Export.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btn_Export.Name = "btn_Export";
            this.btn_Export.Size = new System.Drawing.Size(118, 35);
            this.btn_Export.TabIndex = 17;
            this.btn_Export.Text = "Export";
            this.btn_Export.UseVisualStyleBackColor = true;
            this.btn_Export.Visible = false;
            this.btn_Export.Click += new System.EventHandler(this.btn_Export_Click);
            // 
            // bottompanel
            // 
            this.bottompanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.bottompanel.Controls.Add(this.btn_Export);
            this.bottompanel.Controls.Add(this.btn_tabview);
            this.bottompanel.Controls.Add(this.btn_graphview);
            this.bottompanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottompanel.Location = new System.Drawing.Point(0, 706);
            this.bottompanel.Name = "bottompanel";
            this.bottompanel.Size = new System.Drawing.Size(1110, 45);
            this.bottompanel.TabIndex = 18;
            // 
            // dgvQHist
            // 
            this.dgvQHist.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvQHist.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvQHist.Location = new System.Drawing.Point(0, 51);
            this.dgvQHist.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.dgvQHist.Name = "dgvQHist";
            this.dgvQHist.RowHeadersWidth = 62;
            this.dgvQHist.Size = new System.Drawing.Size(1110, 655);
            this.dgvQHist.TabIndex = 20;
            // 
            // chart_queueHistory
            // 
            this.chart_queueHistory.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chart_queueHistory.BackColor = System.Drawing.Color.WhiteSmoke;
            this.chart_queueHistory.BorderlineColor = System.Drawing.Color.Black;
            this.chart_queueHistory.BorderlineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            chartArea1.AxisX.IsLabelAutoFit = false;
            chartArea1.AxisX.IsStartedFromZero = false;
            chartArea1.AxisX.LabelStyle.Angle = 45;
            chartArea1.AxisX.LabelStyle.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            chartArea1.AxisX.LabelStyle.Format = "hh:mm tt";
            chartArea1.AxisX.MinorGrid.Enabled = true;
            chartArea1.AxisX.MinorGrid.LineColor = System.Drawing.Color.LightGray;
            chartArea1.AxisX.TitleFont = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            chartArea1.AxisY.Minimum = 0D;
            chartArea1.AxisY.MinorGrid.Enabled = true;
            chartArea1.AxisY.MinorGrid.Interval = 10D;
            chartArea1.AxisY.MinorGrid.LineColor = System.Drawing.Color.Gainsboro;
            chartArea1.AxisY.Title = "Queue Count";
            chartArea1.AxisY.TitleFont = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            chartArea1.BorderColor = System.Drawing.Color.Transparent;
            chartArea1.InnerPlotPosition.Auto = false;
            chartArea1.InnerPlotPosition.Height = 85F;
            chartArea1.InnerPlotPosition.Width = 88F;
            chartArea1.InnerPlotPosition.X = 6F;
            chartArea1.Name = "ChartArea1";
            chartArea1.Position.Auto = false;
            chartArea1.Position.Height = 90F;
            chartArea1.Position.Width = 95F;
            chartArea1.Position.X = 5F;
            chartArea1.Position.Y = 3F;
            chartArea1.ShadowOffset = 5;
            this.chart_queueHistory.ChartAreas.Add(chartArea1);
            legend1.LegendStyle = System.Windows.Forms.DataVisualization.Charting.LegendStyle.Row;
            legend1.Name = "Legend1";
            legend1.Position.Auto = false;
            legend1.Position.Height = 7F;
            legend1.Position.Width = 45.08197F;
            legend1.Position.X = 10F;
            legend1.Position.Y = 93F;
            legend1.TableStyle = System.Windows.Forms.DataVisualization.Charting.LegendTableStyle.Wide;
            this.chart_queueHistory.Legends.Add(legend1);
            this.chart_queueHistory.Location = new System.Drawing.Point(0, 0);
            this.chart_queueHistory.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.chart_queueHistory.Name = "chart_queueHistory";
            series1.BorderWidth = 2;
            series1.ChartArea = "ChartArea1";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series1.Color = System.Drawing.Color.Navy;
            series1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            series1.LabelBackColor = System.Drawing.Color.White;
            series1.LabelBorderColor = System.Drawing.Color.DarkBlue;
            series1.LabelForeColor = System.Drawing.Color.Navy;
            series1.Legend = "Legend1";
            series1.MarkerColor = System.Drawing.Color.Black;
            series1.MarkerSize = 0;
            series1.MarkerStyle = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Circle;
            series1.Name = "Priority0";
            series1.SmartLabelStyle.AllowOutsidePlotArea = System.Windows.Forms.DataVisualization.Charting.LabelOutsidePlotAreaStyle.No;
            series1.SmartLabelStyle.CalloutBackColor = System.Drawing.Color.White;
            series1.SmartLabelStyle.CalloutStyle = System.Windows.Forms.DataVisualization.Charting.LabelCalloutStyle.Box;
            series1.SmartLabelStyle.MinMovingDistance = 5D;
            series1.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime;
            series1.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            series2.ChartArea = "ChartArea1";
            series2.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series2.Color = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(192)))), ((int)(((byte)(0)))));
            series2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            series2.LabelBackColor = System.Drawing.Color.White;
            series2.LabelBorderColor = System.Drawing.Color.DarkBlue;
            series2.LabelForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(192)))), ((int)(((byte)(0)))));
            series2.Legend = "Legend1";
            series2.Name = "Priority1";
            series2.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime;
            series2.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            series3.ChartArea = "ChartArea1";
            series3.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series3.Color = System.Drawing.Color.DarkRed;
            series3.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            series3.LabelBackColor = System.Drawing.Color.White;
            series3.LabelBorderColor = System.Drawing.Color.DarkBlue;
            series3.LabelForeColor = System.Drawing.Color.DarkRed;
            series3.Legend = "Legend1";
            series3.Name = "Priority2";
            series3.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime;
            series3.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            series4.ChartArea = "ChartArea1";
            series4.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series4.Color = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            series4.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            series4.LabelBackColor = System.Drawing.Color.White;
            series4.LabelBorderColor = System.Drawing.Color.DarkBlue;
            series4.LabelForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            series4.Legend = "Legend1";
            series4.Name = "Priority3";
            series4.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime;
            series4.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Int32;
            this.chart_queueHistory.Series.Add(series1);
            this.chart_queueHistory.Series.Add(series2);
            this.chart_queueHistory.Series.Add(series3);
            this.chart_queueHistory.Series.Add(series4);
            this.chart_queueHistory.Size = new System.Drawing.Size(1110, 706);
            this.chart_queueHistory.TabIndex = 18;
            this.chart_queueHistory.Text = "chart1";
            // 
            // DetailsPanel
            // 
            this.DetailsPanel.Controls.Add(this.chart_queueHistory);
            this.DetailsPanel.Controls.Add(this.gridviewPanel);
            this.DetailsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DetailsPanel.Location = new System.Drawing.Point(0, 0);
            this.DetailsPanel.Name = "DetailsPanel";
            this.DetailsPanel.Size = new System.Drawing.Size(1110, 706);
            this.DetailsPanel.TabIndex = 21;
            // 
            // gridviewPanel
            // 
            this.gridviewPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gridviewPanel.Controls.Add(this.pri3enb);
            this.gridviewPanel.Controls.Add(this.pri2enb);
            this.gridviewPanel.Controls.Add(this.pri1enb);
            this.gridviewPanel.Controls.Add(this.pri0enb);
            this.gridviewPanel.Controls.Add(this.dgvQHist);
            this.gridviewPanel.Location = new System.Drawing.Point(0, 0);
            this.gridviewPanel.Name = "gridviewPanel";
            this.gridviewPanel.Size = new System.Drawing.Size(1110, 706);
            this.gridviewPanel.TabIndex = 21;
            this.gridviewPanel.Visible = false;
            // 
            // pri3enb
            // 
            this.pri3enb.AutoSize = true;
            this.pri3enb.Checked = true;
            this.pri3enb.CheckState = System.Windows.Forms.CheckState.Checked;
            this.pri3enb.Location = new System.Drawing.Point(385, 19);
            this.pri3enb.Name = "pri3enb";
            this.pri3enb.Size = new System.Drawing.Size(94, 24);
            this.pri3enb.TabIndex = 24;
            this.pri3enb.Text = "priority 3";
            this.pri3enb.UseVisualStyleBackColor = true;
            this.pri3enb.CheckedChanged += new System.EventHandler(this.priorityEnabled_CheckedChanged);
            // 
            // pri2enb
            // 
            this.pri2enb.AutoSize = true;
            this.pri2enb.Checked = true;
            this.pri2enb.CheckState = System.Windows.Forms.CheckState.Checked;
            this.pri2enb.Location = new System.Drawing.Point(254, 19);
            this.pri2enb.Name = "pri2enb";
            this.pri2enb.Size = new System.Drawing.Size(94, 24);
            this.pri2enb.TabIndex = 23;
            this.pri2enb.Text = "priority 2";
            this.pri2enb.UseVisualStyleBackColor = true;
            this.pri2enb.CheckedChanged += new System.EventHandler(this.priorityEnabled_CheckedChanged);
            // 
            // pri1enb
            // 
            this.pri1enb.AutoSize = true;
            this.pri1enb.Checked = true;
            this.pri1enb.CheckState = System.Windows.Forms.CheckState.Checked;
            this.pri1enb.Location = new System.Drawing.Point(132, 19);
            this.pri1enb.Name = "pri1enb";
            this.pri1enb.Size = new System.Drawing.Size(94, 24);
            this.pri1enb.TabIndex = 22;
            this.pri1enb.Text = "priority 1";
            this.pri1enb.UseVisualStyleBackColor = true;
            this.pri1enb.CheckedChanged += new System.EventHandler(this.priorityEnabled_CheckedChanged);
            // 
            // pri0enb
            // 
            this.pri0enb.AutoSize = true;
            this.pri0enb.Checked = true;
            this.pri0enb.CheckState = System.Windows.Forms.CheckState.Checked;
            this.pri0enb.Location = new System.Drawing.Point(13, 19);
            this.pri0enb.Name = "pri0enb";
            this.pri0enb.Size = new System.Drawing.Size(95, 24);
            this.pri0enb.TabIndex = 21;
            this.pri0enb.Text = "Priority 0";
            this.pri0enb.UseVisualStyleBackColor = true;
            this.pri0enb.CheckedChanged += new System.EventHandler(this.priorityEnabled_CheckedChanged);
            // 
            // QueueHistory
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.DetailsPanel);
            this.Controls.Add(this.bottompanel);
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "QueueHistory";
            this.Size = new System.Drawing.Size(1110, 751);
            this.bottompanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvQHist)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.chart_queueHistory)).EndInit();
            this.DetailsPanel.ResumeLayout(false);
            this.gridviewPanel.ResumeLayout(false);
            this.gridviewPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btn_graphview;
        private System.Windows.Forms.Button btn_tabview;
        private System.Windows.Forms.Button btn_Export;
        private System.Windows.Forms.Panel bottompanel;
        private System.Windows.Forms.DataGridView dgvQHist;
        private System.Windows.Forms.DataVisualization.Charting.Chart chart_queueHistory;
        private System.Windows.Forms.Panel DetailsPanel;
        private System.Windows.Forms.Panel gridviewPanel;
        private System.Windows.Forms.CheckBox pri3enb;
        private System.Windows.Forms.CheckBox pri2enb;
        private System.Windows.Forms.CheckBox pri1enb;
        private System.Windows.Forms.CheckBox pri0enb;
    }
}
