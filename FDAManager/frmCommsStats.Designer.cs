namespace FDAManager
{
    partial class frmCommsStats
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmCommsStats));
            this.CalcButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.label4 = new System.Windows.Forms.Label();
            this.device = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.outputtable = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.rb_hours = new System.Windows.Forms.RadioButton();
            this.rbdays = new System.Windows.Forms.RadioButton();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label9 = new System.Windows.Forms.Label();
            this.startTime = new System.Windows.Forms.DateTimePicker();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.btn_export = new System.Windows.Forms.Button();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.cb_connection = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.endtime = new System.Windows.Forms.DateTimePicker();
            this.btnSetStart = new System.Windows.Forms.Button();
            this.dayshoursago = new System.Windows.Forms.NumericUpDown();
            this.btnSetEnd = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.description = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.lbl_defaultOutput = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.chkSaveToDB = new System.Windows.Forms.CheckBox();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dayshoursago)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // CalcButton
            // 
            this.CalcButton.BackColor = System.Drawing.Color.PaleGreen;
            this.CalcButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.CalcButton.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CalcButton.Image = FDAManager.Program.FDAManagerContext.GetImage("executetriangle.png"); // global::FDAManager.Properties.Resources.executetriangle;
            this.CalcButton.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.CalcButton.Location = new System.Drawing.Point(29, 348);
            this.CalcButton.Name = "CalcButton";
            this.CalcButton.Size = new System.Drawing.Size(117, 36);
            this.CalcButton.TabIndex = 0;
            this.CalcButton.Text = "Execute";
            this.CalcButton.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.CalcButton.UseVisualStyleBackColor = false;
            this.CalcButton.Click += new System.EventHandler(this.CalcButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.label1.Location = new System.Drawing.Point(25, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(144, 21);
            this.label1.TabIndex = 2;
            this.label1.Text = "Statistics Start Time";
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(168, 352);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(174, 32);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.TabIndex = 9;
            this.progressBar.Visible = false;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.label4.Location = new System.Drawing.Point(23, 29);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(89, 21);
            this.label4.TabIndex = 10;
            this.label4.Text = "Connection";
            // 
            // device
            // 
            this.device.Location = new System.Drawing.Point(250, 58);
            this.device.Name = "device";
            this.device.Size = new System.Drawing.Size(562, 29);
            this.device.TabIndex = 13;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.label5.Location = new System.Drawing.Point(23, 61);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(56, 21);
            this.label5.TabIndex = 12;
            this.label5.Text = "Device";
            // 
            // outputtable
            // 
            this.outputtable.BackColor = System.Drawing.SystemColors.Control;
            this.outputtable.Enabled = false;
            this.outputtable.Location = new System.Drawing.Point(250, 110);
            this.outputtable.Name = "outputtable";
            this.outputtable.ReadOnly = true;
            this.outputtable.Size = new System.Drawing.Size(562, 29);
            this.outputtable.TabIndex = 15;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.label6.Location = new System.Drawing.Point(23, 113);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(177, 21);
            this.label6.TabIndex = 14;
            this.label6.Text = "FDA Output Table Name";
            // 
            // rb_hours
            // 
            this.rb_hours.AutoSize = true;
            this.rb_hours.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.rb_hours.Location = new System.Drawing.Point(6, 7);
            this.rb_hours.Name = "rb_hours";
            this.rb_hours.Size = new System.Drawing.Size(77, 25);
            this.rb_hours.TabIndex = 16;
            this.rb_hours.Text = "Hours";
            this.rb_hours.UseVisualStyleBackColor = true;
            // 
            // rbdays
            // 
            this.rbdays.AutoSize = true;
            this.rbdays.Checked = true;
            this.rbdays.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.rbdays.Location = new System.Drawing.Point(82, 7);
            this.rbdays.Name = "rbdays";
            this.rbdays.Size = new System.Drawing.Size(69, 25);
            this.rbdays.TabIndex = 17;
            this.rbdays.TabStop = true;
            this.rbdays.Text = "Days";
            this.rbdays.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.label9);
            this.panel1.Controls.Add(this.rbdays);
            this.panel1.Controls.Add(this.rb_hours);
            this.panel1.Location = new System.Drawing.Point(622, -2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(214, 41);
            this.panel1.TabIndex = 18;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(150, 10);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(36, 20);
            this.label9.TabIndex = 29;
            this.label9.Text = "ago";
            // 
            // startTime
            // 
            this.startTime.CustomFormat = "yyyy-MM-dd hh:mm:ss tt";
            this.startTime.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.startTime.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.startTime.Location = new System.Drawing.Point(198, 6);
            this.startTime.Name = "startTime";
            this.startTime.Size = new System.Drawing.Size(260, 29);
            this.startTime.TabIndex = 19;
            // 
            // dataGridView1
            // 
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(29, 389);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersWidth = 62;
            this.dataGridView1.RowTemplate.Height = 28;
            this.dataGridView1.Size = new System.Drawing.Size(1269, 244);
            this.dataGridView1.TabIndex = 20;
            this.dataGridView1.DataSourceChanged += new System.EventHandler(this.dataGridView1_DataSourceChanged);
            // 
            // btn_export
            // 
            this.btn_export.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn_export.BackColor = System.Drawing.SystemColors.ControlDark;
            this.btn_export.Enabled = false;
            this.btn_export.FlatAppearance.BorderColor = System.Drawing.Color.Black;
            this.btn_export.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_export.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_export.Location = new System.Drawing.Point(29, 660);
            this.btn_export.Name = "btn_export";
            this.btn_export.Size = new System.Drawing.Size(141, 36);
            this.btn_export.TabIndex = 21;
            this.btn_export.Text = "Export Results";
            this.btn_export.UseVisualStyleBackColor = false;
            this.btn_export.Click += new System.EventHandler(this.btn_export_Click);
            // 
            // saveFileDialog1
            // 
            this.saveFileDialog1.DefaultExt = "csv";
            this.saveFileDialog1.FileName = "CommsStats";
            this.saveFileDialog1.Filter = "csv|*.csv";
            // 
            // cb_connection
            // 
            this.cb_connection.FormattingEnabled = true;
            this.cb_connection.Items.AddRange(new object[] {
            ""});
            this.cb_connection.Location = new System.Drawing.Point(250, 26);
            this.cb_connection.Name = "cb_connection";
            this.cb_connection.Size = new System.Drawing.Size(562, 29);
            this.cb_connection.TabIndex = 22;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.label7.Location = new System.Drawing.Point(25, 50);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(138, 21);
            this.label7.TabIndex = 23;
            this.label7.Text = "Statistics End Time";
            // 
            // endtime
            // 
            this.endtime.CustomFormat = "yyyy-MM-dd hh:mm:ss tt";
            this.endtime.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.endtime.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.endtime.Location = new System.Drawing.Point(198, 47);
            this.endtime.Name = "endtime";
            this.endtime.Size = new System.Drawing.Size(260, 29);
            this.endtime.TabIndex = 24;
            // 
            // btnSetStart
            // 
            this.btnSetStart.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.btnSetStart.Location = new System.Drawing.Point(464, 2);
            this.btnSetStart.Name = "btnSetStart";
            this.btnSetStart.Size = new System.Drawing.Size(65, 35);
            this.btnSetStart.TabIndex = 25;
            this.btnSetStart.Text = "Set to";
            this.btnSetStart.UseVisualStyleBackColor = true;
            this.btnSetStart.Click += new System.EventHandler(this.btnSetStart_Click);
            // 
            // dayshoursago
            // 
            this.dayshoursago.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.dayshoursago.Location = new System.Drawing.Point(535, 6);
            this.dayshoursago.Maximum = new decimal(new int[] {
            365,
            0,
            0,
            0});
            this.dayshoursago.Name = "dayshoursago";
            this.dayshoursago.Size = new System.Drawing.Size(85, 29);
            this.dayshoursago.TabIndex = 26;
            this.dayshoursago.Value = new decimal(new int[] {
            30,
            0,
            0,
            0});
            // 
            // btnSetEnd
            // 
            this.btnSetEnd.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.btnSetEnd.Location = new System.Drawing.Point(464, 43);
            this.btnSetEnd.Name = "btnSetEnd";
            this.btnSetEnd.Size = new System.Drawing.Size(65, 35);
            this.btnSetEnd.TabIndex = 30;
            this.btnSetEnd.Text = "Now";
            this.btnSetEnd.UseVisualStyleBackColor = true;
            this.btnSetEnd.Click += new System.EventHandler(this.btnSetEnd_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cb_connection);
            this.groupBox1.Controls.Add(this.device);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.groupBox1.Location = new System.Drawing.Point(22, 84);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(1276, 98);
            this.groupBox1.TabIndex = 31;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Optional Filters";
            // 
            // description
            // 
            this.description.Location = new System.Drawing.Point(250, 23);
            this.description.Name = "description";
            this.description.Size = new System.Drawing.Size(562, 29);
            this.description.TabIndex = 33;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.label3.Location = new System.Drawing.Point(23, 26);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(170, 21);
            this.label3.TabIndex = 32;
            this.label3.Text = "Calculation Description";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.lbl_defaultOutput);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.chkSaveToDB);
            this.groupBox2.Controls.Add(this.description);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.outputtable);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.groupBox2.Location = new System.Drawing.Point(22, 189);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(1276, 153);
            this.groupBox2.TabIndex = 34;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Optional Statistics Output Settings";
            // 
            // lbl_defaultOutput
            // 
            this.lbl_defaultOutput.AutoSize = true;
            this.lbl_defaultOutput.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lbl_defaultOutput.Location = new System.Drawing.Point(1142, 114);
            this.lbl_defaultOutput.Name = "lbl_defaultOutput";
            this.lbl_defaultOutput.Size = new System.Drawing.Size(118, 21);
            this.lbl_defaultOutput.TabIndex = 36;
            this.lbl_defaultOutput.Text = "<default table>";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.label2.Location = new System.Drawing.Point(819, 114);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(287, 21);
            this.label2.TabIndex = 35;
            this.label2.Text = "Leave empty for output to default table: ";
            // 
            // chkSaveToDB
            // 
            this.chkSaveToDB.AutoSize = true;
            this.chkSaveToDB.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.chkSaveToDB.Location = new System.Drawing.Point(27, 71);
            this.chkSaveToDB.Name = "chkSaveToDB";
            this.chkSaveToDB.Size = new System.Drawing.Size(220, 25);
            this.chkSaveToDB.TabIndex = 34;
            this.chkSaveToDB.Text = "Save data to FDA database";
            this.chkSaveToDB.UseVisualStyleBackColor = true;
            this.chkSaveToDB.CheckedChanged += new System.EventHandler(this.chkSaveToDB_CheckedChanged);
            // 
            // frmCommsStats
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1310, 765);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnSetEnd);
            this.Controls.Add(this.dayshoursago);
            this.Controls.Add(this.btnSetStart);
            this.Controls.Add(this.endtime);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.btn_export);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.startTime);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.CalcButton);
            //this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmCommsStats";
            this.Text = "Comms Stats";
            this.Load += new System.EventHandler(this.frmCommsStats_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dayshoursago)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button CalcButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox device;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox outputtable;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.RadioButton rb_hours;
        private System.Windows.Forms.RadioButton rbdays;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.DateTimePicker startTime;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button btn_export;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.ComboBox cb_connection;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.DateTimePicker endtime;
        private System.Windows.Forms.Button btnSetStart;
        private System.Windows.Forms.NumericUpDown dayshoursago;
        private System.Windows.Forms.Button btnSetEnd;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox description;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label lbl_defaultOutput;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox chkSaveToDB;
    }
}