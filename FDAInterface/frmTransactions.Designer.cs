namespace FDAInterface
{
    partial class frmTransactions
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmTransactions));
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.btnClose = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.lbl_connDescription = new System.Windows.Forms.Label();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.btnClearFilter = new System.Windows.Forms.Button();
            this.btnApplyFilter = new System.Windows.Forms.Button();
            this.filter_comparatorStr = new System.Windows.Forms.ComboBox();
            this.filter_columns = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.filter_text = new System.Windows.Forms.TextBox();
            this.filter_boolean = new System.Windows.Forms.ComboBox();
            this.filter_comparatorNum = new System.Windows.Forms.ComboBox();
            this.filter_comparatorBool = new System.Windows.Forms.ComboBox();
            this.filter_numeric = new System.Windows.Forms.NumericUpDown();
            this.filter_comparatorTime = new System.Windows.Forms.ComboBox();
            this.filter_timeFrom = new System.Windows.Forms.DateTimePicker();
            this.filter_timeTo = new System.Windows.Forms.DateTimePicker();
            this.filter_timebetween = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.filter_timeBeforeAfter = new System.Windows.Forms.DateTimePicker();
            this.btn_columnSelector = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label3 = new System.Windows.Forms.Label();
            this.cb_execution = new System.Windows.Forms.ComboBox();
            this.lbl_recordcount = new System.Windows.Forms.Label();
            this.cb_recordtype = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.filter_numeric)).BeginInit();
            this.filter_timebetween.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToOrderColumns = true;
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dataGridView1.Location = new System.Drawing.Point(12, 148);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dataGridView1.Size = new System.Drawing.Size(1174, 415);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellDoubleClick);
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(1111, 569);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 23);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(258, 17);
            this.label1.TabIndex = 2;
            this.label1.Text = "Communications log for the connection:";
            // 
            // lbl_connDescription
            // 
            this.lbl_connDescription.AutoSize = true;
            this.lbl_connDescription.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbl_connDescription.Location = new System.Drawing.Point(277, 13);
            this.lbl_connDescription.Name = "lbl_connDescription";
            this.lbl_connDescription.Size = new System.Drawing.Size(91, 17);
            this.lbl_connDescription.TabIndex = 3;
            this.lbl_connDescription.Text = "<description>";
            // 
            // btnRefresh
            // 
            this.btnRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRefresh.Location = new System.Drawing.Point(1030, 569);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(75, 23);
            this.btnRefresh.TabIndex = 4;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // btnClearFilter
            // 
            this.btnClearFilter.Enabled = false;
            this.btnClearFilter.Location = new System.Drawing.Point(848, 11);
            this.btnClearFilter.Name = "btnClearFilter";
            this.btnClearFilter.Size = new System.Drawing.Size(75, 23);
            this.btnClearFilter.TabIndex = 28;
            this.btnClearFilter.Text = "Clear Filter";
            this.btnClearFilter.UseVisualStyleBackColor = true;
            this.btnClearFilter.Click += new System.EventHandler(this.btnClearFilter_Click);
            // 
            // btnApplyFilter
            // 
            this.btnApplyFilter.Location = new System.Drawing.Point(767, 11);
            this.btnApplyFilter.Name = "btnApplyFilter";
            this.btnApplyFilter.Size = new System.Drawing.Size(75, 23);
            this.btnApplyFilter.TabIndex = 27;
            this.btnApplyFilter.Text = "Apply";
            this.btnApplyFilter.UseVisualStyleBackColor = true;
            this.btnApplyFilter.Click += new System.EventHandler(this.btnApplyFilter_Click);
            // 
            // filter_comparatorStr
            // 
            this.filter_comparatorStr.FormattingEnabled = true;
            this.filter_comparatorStr.Items.AddRange(new object[] {
            "equal to",
            "containing"});
            this.filter_comparatorStr.Location = new System.Drawing.Point(229, 13);
            this.filter_comparatorStr.Name = "filter_comparatorStr";
            this.filter_comparatorStr.Size = new System.Drawing.Size(121, 21);
            this.filter_comparatorStr.TabIndex = 26;
            // 
            // filter_columns
            // 
            this.filter_columns.FormattingEnabled = true;
            this.filter_columns.Items.AddRange(new object[] {
            "TimestampUTC1",
            "TimestampUTC2",
            "Attempt",
            "TransStatus",
            "TransCode",
            "ElapsedPeriod",
            "DBRGUID",
            "Details01",
            "TxSize",
            "Details02",
            "RxSize",
            "ProtocolNote",
            "ApplicationMessage"});
            this.filter_columns.Location = new System.Drawing.Point(102, 13);
            this.filter_columns.Name = "filter_columns";
            this.filter_columns.Size = new System.Drawing.Size(121, 21);
            this.filter_columns.TabIndex = 25;
            this.filter_columns.SelectedValueChanged += new System.EventHandler(this.filter_columns_SelectedValueChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(1, 16);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(103, 13);
            this.label4.TabIndex = 24;
            this.label4.Text = "Show only rows with";
            // 
            // filter_text
            // 
            this.filter_text.Location = new System.Drawing.Point(356, 13);
            this.filter_text.Name = "filter_text";
            this.filter_text.Size = new System.Drawing.Size(262, 20);
            this.filter_text.TabIndex = 29;
            // 
            // filter_boolean
            // 
            this.filter_boolean.FormattingEnabled = true;
            this.filter_boolean.Items.AddRange(new object[] {
            "True",
            "False"});
            this.filter_boolean.Location = new System.Drawing.Point(356, 12);
            this.filter_boolean.Name = "filter_boolean";
            this.filter_boolean.Size = new System.Drawing.Size(263, 21);
            this.filter_boolean.TabIndex = 30;
            this.filter_boolean.Visible = false;
            // 
            // filter_comparatorNum
            // 
            this.filter_comparatorNum.FormattingEnabled = true;
            this.filter_comparatorNum.Items.AddRange(new object[] {
            "equal to",
            "greater than",
            "less than",
            "greater than or equal to",
            "less than or equal to"});
            this.filter_comparatorNum.Location = new System.Drawing.Point(229, 13);
            this.filter_comparatorNum.Name = "filter_comparatorNum";
            this.filter_comparatorNum.Size = new System.Drawing.Size(121, 21);
            this.filter_comparatorNum.TabIndex = 31;
            // 
            // filter_comparatorBool
            // 
            this.filter_comparatorBool.FormattingEnabled = true;
            this.filter_comparatorBool.Items.AddRange(new object[] {
            "equal to"});
            this.filter_comparatorBool.Location = new System.Drawing.Point(229, 13);
            this.filter_comparatorBool.Name = "filter_comparatorBool";
            this.filter_comparatorBool.Size = new System.Drawing.Size(121, 21);
            this.filter_comparatorBool.TabIndex = 32;
            // 
            // filter_numeric
            // 
            this.filter_numeric.Location = new System.Drawing.Point(355, 12);
            this.filter_numeric.Maximum = new decimal(new int[] {
            32767,
            0,
            0,
            0});
            this.filter_numeric.Name = "filter_numeric";
            this.filter_numeric.Size = new System.Drawing.Size(263, 20);
            this.filter_numeric.TabIndex = 33;
            // 
            // filter_comparatorTime
            // 
            this.filter_comparatorTime.FormattingEnabled = true;
            this.filter_comparatorTime.Items.AddRange(new object[] {
            "Between",
            "Before",
            "After"});
            this.filter_comparatorTime.Location = new System.Drawing.Point(229, 13);
            this.filter_comparatorTime.Name = "filter_comparatorTime";
            this.filter_comparatorTime.Size = new System.Drawing.Size(121, 21);
            this.filter_comparatorTime.TabIndex = 34;
            this.filter_comparatorTime.SelectedIndexChanged += new System.EventHandler(this.filter_comparatorTime_SelectedIndexChanged);
            // 
            // filter_timeFrom
            // 
            this.filter_timeFrom.CustomFormat = "yyyy-MM-dd hh:mm:ss tt";
            this.filter_timeFrom.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.filter_timeFrom.Location = new System.Drawing.Point(2, 3);
            this.filter_timeFrom.Name = "filter_timeFrom";
            this.filter_timeFrom.Size = new System.Drawing.Size(160, 20);
            this.filter_timeFrom.TabIndex = 35;
            // 
            // filter_timeTo
            // 
            this.filter_timeTo.CustomFormat = "yyyy-MM-dd hh:mm:ss tt";
            this.filter_timeTo.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.filter_timeTo.Location = new System.Drawing.Point(200, 3);
            this.filter_timeTo.Name = "filter_timeTo";
            this.filter_timeTo.Size = new System.Drawing.Size(206, 20);
            this.filter_timeTo.TabIndex = 36;
            // 
            // filter_timebetween
            // 
            this.filter_timebetween.Controls.Add(this.filter_timeFrom);
            this.filter_timebetween.Controls.Add(this.filter_timeTo);
            this.filter_timebetween.Controls.Add(this.label2);
            this.filter_timebetween.Location = new System.Drawing.Point(355, 10);
            this.filter_timebetween.Name = "filter_timebetween";
            this.filter_timebetween.Size = new System.Drawing.Size(408, 26);
            this.filter_timebetween.TabIndex = 37;
            this.filter_timebetween.Visible = false;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(167, 4);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(30, 17);
            this.label2.TabIndex = 37;
            this.label2.Text = "and";
            // 
            // filter_timeBeforeAfter
            // 
            this.filter_timeBeforeAfter.CustomFormat = "yyyy-MM-dd hh:mm:ss tt";
            this.filter_timeBeforeAfter.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.filter_timeBeforeAfter.Location = new System.Drawing.Point(357, 13);
            this.filter_timeBeforeAfter.Name = "filter_timeBeforeAfter";
            this.filter_timeBeforeAfter.Size = new System.Drawing.Size(160, 20);
            this.filter_timeBeforeAfter.TabIndex = 38;
            this.filter_timeBeforeAfter.Visible = false;
            // 
            // btn_columnSelector
            // 
            this.btn_columnSelector.Location = new System.Drawing.Point(12, 77);
            this.btn_columnSelector.Name = "btn_columnSelector";
            this.btn_columnSelector.Size = new System.Drawing.Size(88, 23);
            this.btn_columnSelector.TabIndex = 39;
            this.btn_columnSelector.Text = "Select columns";
            this.btn_columnSelector.UseVisualStyleBackColor = true;
            this.btn_columnSelector.Click += new System.EventHandler(this.btn_columnSelector_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.label5);
            this.panel1.Controls.Add(this.cb_recordtype);
            this.panel1.Controls.Add(this.filter_timeBeforeAfter);
            this.panel1.Controls.Add(this.filter_comparatorTime);
            this.panel1.Controls.Add(this.filter_comparatorBool);
            this.panel1.Controls.Add(this.filter_comparatorNum);
            this.panel1.Controls.Add(this.filter_boolean);
            this.panel1.Controls.Add(this.filter_text);
            this.panel1.Controls.Add(this.btnClearFilter);
            this.panel1.Controls.Add(this.btnApplyFilter);
            this.panel1.Controls.Add(this.filter_comparatorStr);
            this.panel1.Controls.Add(this.filter_columns);
            this.panel1.Controls.Add(this.label4);
            this.panel1.Controls.Add(this.filter_numeric);
            this.panel1.Controls.Add(this.filter_timebetween);
            this.panel1.Location = new System.Drawing.Point(222, 60);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(964, 70);
            this.panel1.TabIndex = 40;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(13, 37);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(108, 13);
            this.label3.TabIndex = 41;
            this.label3.Text = "FDA Execution ID";
            // 
            // cb_execution
            // 
            this.cb_execution.FormattingEnabled = true;
            this.cb_execution.Location = new System.Drawing.Point(127, 33);
            this.cb_execution.Name = "cb_execution";
            this.cb_execution.Size = new System.Drawing.Size(498, 21);
            this.cb_execution.TabIndex = 42;
            this.cb_execution.SelectedIndexChanged += new System.EventHandler(this.cb_execution_SelectedIndexChanged);
            // 
            // lbl_recordcount
            // 
            this.lbl_recordcount.AutoSize = true;
            this.lbl_recordcount.Location = new System.Drawing.Point(636, 37);
            this.lbl_recordcount.Name = "lbl_recordcount";
            this.lbl_recordcount.Size = new System.Drawing.Size(84, 13);
            this.lbl_recordcount.TabIndex = 39;
            this.lbl_recordcount.Text = "<records found>";
            // 
            // cb_recordtype
            // 
            this.cb_recordtype.FormattingEnabled = true;
            this.cb_recordtype.Items.AddRange(new object[] {
            "Connection and Transaction",
            "Connection only",
            "Transaction only"});
            this.cb_recordtype.Location = new System.Drawing.Point(102, 41);
            this.cb_recordtype.Name = "cb_recordtype";
            this.cb_recordtype.Size = new System.Drawing.Size(248, 21);
            this.cb_recordtype.TabIndex = 39;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 44);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(90, 13);
            this.label5.TabIndex = 40;
            this.label5.Text = "Show record type";
            // 
            // frmTransactions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1198, 604);
            this.Controls.Add(this.lbl_recordcount);
            this.Controls.Add(this.cb_execution);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.btn_columnSelector);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.lbl_connDescription);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.dataGridView1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmTransactions";
            this.Text = "Communications Log";
            this.Shown += new System.EventHandler(this.frmTransactions_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.filter_numeric)).EndInit();
            this.filter_timebetween.ResumeLayout(false);
            this.filter_timebetween.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lbl_connDescription;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnClearFilter;
        private System.Windows.Forms.Button btnApplyFilter;
        private System.Windows.Forms.ComboBox filter_comparatorStr;
        private System.Windows.Forms.ComboBox filter_columns;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox filter_text;
        private System.Windows.Forms.ComboBox filter_boolean;
        private System.Windows.Forms.ComboBox filter_comparatorNum;
        private System.Windows.Forms.ComboBox filter_comparatorBool;
        private System.Windows.Forms.NumericUpDown filter_numeric;
        private System.Windows.Forms.ComboBox filter_comparatorTime;
        private System.Windows.Forms.DateTimePicker filter_timeFrom;
        private System.Windows.Forms.DateTimePicker filter_timeTo;
        private System.Windows.Forms.Panel filter_timebetween;
        private System.Windows.Forms.DateTimePicker filter_timeBeforeAfter;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btn_columnSelector;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cb_execution;
        private System.Windows.Forms.Label lbl_recordcount;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox cb_recordtype;
    }
}