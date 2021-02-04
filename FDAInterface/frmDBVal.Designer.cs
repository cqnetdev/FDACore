namespace FDAInterface
{
    partial class frmDBVal
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmDBVal));
            this.lbl_connstatus = new System.Windows.Forms.Label();
            this.btn_start = new System.Windows.Forms.Button();
            this.dgvErrorList = new System.Windows.Forms.DataGridView();
            this.lbl_validating = new System.Windows.Forms.Label();
            this.btn_getCurrentDBConn = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.filter_columns = new System.Windows.Forms.ComboBox();
            this.filter_comparator = new System.Windows.Forms.ComboBox();
            this.filter_text = new System.Windows.Forms.TextBox();
            this.btnApplyFilter = new System.Windows.Forms.Button();
            this.btnClearFilter = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.lbl_filteredCount = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.lblTotalCount = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.filter_severityFilter = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btn_disconnect = new System.Windows.Forms.Button();
            this.tb_DB = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.tb_pass = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.tb_user = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.tb_SQLInstance = new System.Windows.Forms.TextBox();
            this.btn_connect = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.legend = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.label13 = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.cb_enabledonly = new System.Windows.Forms.CheckBox();
            this.cb_errorsOnly = new System.Windows.Forms.CheckBox();
            this.menu_ID = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuitem_filterByID = new System.Windows.Forms.ToolStripMenuItem();
            this.menu_row = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menu_item_retest = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.dgvErrorList)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.legend.SuspendLayout();
            this.menu_ID.SuspendLayout();
            this.menu_row.SuspendLayout();
            this.SuspendLayout();
            // 
            // lbl_connstatus
            // 
            this.lbl_connstatus.AutoSize = true;
            this.lbl_connstatus.Location = new System.Drawing.Point(431, 109);
            this.lbl_connstatus.Name = "lbl_connstatus";
            this.lbl_connstatus.Size = new System.Drawing.Size(74, 13);
            this.lbl_connstatus.TabIndex = 0;
            this.lbl_connstatus.Text = "<conn status>";
            // 
            // btn_start
            // 
            this.btn_start.Enabled = false;
            this.btn_start.Location = new System.Drawing.Point(21, 139);
            this.btn_start.Name = "btn_start";
            this.btn_start.Size = new System.Drawing.Size(90, 24);
            this.btn_start.TabIndex = 1;
            this.btn_start.Text = "Start Validation";
            this.btn_start.UseVisualStyleBackColor = true;
            this.btn_start.Click += new System.EventHandler(this.btn_start_Click);
            // 
            // dgvErrorList
            // 
            this.dgvErrorList.AllowUserToAddRows = false;
            this.dgvErrorList.AllowUserToDeleteRows = false;
            this.dgvErrorList.AllowUserToOrderColumns = true;
            this.dgvErrorList.AllowUserToResizeRows = false;
            this.dgvErrorList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvErrorList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvErrorList.Location = new System.Drawing.Point(18, 247);
            this.dgvErrorList.Name = "dgvErrorList";
            this.dgvErrorList.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dgvErrorList.Size = new System.Drawing.Size(977, 424);
            this.dgvErrorList.TabIndex = 2;
            this.dgvErrorList.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvErrorList_CellDoubleClick);
            this.dgvErrorList.CellMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dgvErrorList_CellMouseClick);
            this.dgvErrorList.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dgvErrorList_ColumnHeaderMouseClick);
            this.dgvErrorList.RowHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dgvErrorList_RowHeaderMouseClick);
            // 
            // lbl_validating
            // 
            this.lbl_validating.AutoSize = true;
            this.lbl_validating.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbl_validating.Location = new System.Drawing.Point(126, 145);
            this.lbl_validating.Name = "lbl_validating";
            this.lbl_validating.Size = new System.Drawing.Size(79, 13);
            this.lbl_validating.TabIndex = 3;
            this.lbl_validating.Text = "Validating....";
            this.lbl_validating.Visible = false;
            // 
            // btn_getCurrentDBConn
            // 
            this.btn_getCurrentDBConn.Location = new System.Drawing.Point(434, 16);
            this.btn_getCurrentDBConn.Name = "btn_getCurrentDBConn";
            this.btn_getCurrentDBConn.Size = new System.Drawing.Size(168, 25);
            this.btn_getCurrentDBConn.TabIndex = 6;
            this.btn_getCurrentDBConn.Text = "Get FDA Connection";
            this.btn_getCurrentDBConn.UseVisualStyleBackColor = true;
            this.btn_getCurrentDBConn.Click += new System.EventHandler(this.btn_GetCurrentDBConn_click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(21, 179);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(0, 13);
            this.label3.TabIndex = 10;
            this.label3.Visible = false;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(19, 171);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(103, 13);
            this.label4.TabIndex = 12;
            this.label4.Text = "Show only rows with";
            // 
            // filter_columns
            // 
            this.filter_columns.FormattingEnabled = true;
            this.filter_columns.Items.AddRange(new object[] {
            "TableName",
            "ID",
            "Description"});
            this.filter_columns.Location = new System.Drawing.Point(120, 168);
            this.filter_columns.Name = "filter_columns";
            this.filter_columns.Size = new System.Drawing.Size(121, 21);
            this.filter_columns.TabIndex = 13;
            this.filter_columns.SelectedValueChanged += new System.EventHandler(this.filter_columns_SelectedValueChanged);
            // 
            // filter_comparator
            // 
            this.filter_comparator.FormattingEnabled = true;
            this.filter_comparator.Items.AddRange(new object[] {
            "equal to",
            "containing"});
            this.filter_comparator.Location = new System.Drawing.Point(247, 168);
            this.filter_comparator.Name = "filter_comparator";
            this.filter_comparator.Size = new System.Drawing.Size(121, 21);
            this.filter_comparator.TabIndex = 14;
            // 
            // filter_text
            // 
            this.filter_text.Location = new System.Drawing.Point(375, 168);
            this.filter_text.Name = "filter_text";
            this.filter_text.Size = new System.Drawing.Size(262, 20);
            this.filter_text.TabIndex = 15;
            // 
            // btnApplyFilter
            // 
            this.btnApplyFilter.Location = new System.Drawing.Point(643, 166);
            this.btnApplyFilter.Name = "btnApplyFilter";
            this.btnApplyFilter.Size = new System.Drawing.Size(75, 23);
            this.btnApplyFilter.TabIndex = 16;
            this.btnApplyFilter.Text = "Apply";
            this.btnApplyFilter.UseVisualStyleBackColor = true;
            this.btnApplyFilter.Click += new System.EventHandler(this.btnApplyFilter_Click);
            // 
            // btnClearFilter
            // 
            this.btnClearFilter.Location = new System.Drawing.Point(724, 166);
            this.btnClearFilter.Name = "btnClearFilter";
            this.btnClearFilter.Size = new System.Drawing.Size(75, 23);
            this.btnClearFilter.TabIndex = 17;
            this.btnClearFilter.Text = "Clear Filter";
            this.btnClearFilter.UseVisualStyleBackColor = true;
            this.btnClearFilter.Click += new System.EventHandler(this.btnClearFilter_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(16, 231);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(48, 13);
            this.label5.TabIndex = 18;
            this.label5.Text = "Showing";
            // 
            // lbl_filteredCount
            // 
            this.lbl_filteredCount.AutoSize = true;
            this.lbl_filteredCount.Location = new System.Drawing.Point(62, 231);
            this.lbl_filteredCount.Name = "lbl_filteredCount";
            this.lbl_filteredCount.Size = new System.Drawing.Size(13, 13);
            this.lbl_filteredCount.TabIndex = 19;
            this.lbl_filteredCount.Text = "0";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(88, 230);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(16, 13);
            this.label6.TabIndex = 20;
            this.label6.Text = "of";
            // 
            // lblTotalCount
            // 
            this.lblTotalCount.AutoSize = true;
            this.lblTotalCount.Location = new System.Drawing.Point(105, 231);
            this.lblTotalCount.Name = "lblTotalCount";
            this.lblTotalCount.Size = new System.Drawing.Size(13, 13);
            this.lblTotalCount.TabIndex = 21;
            this.lblTotalCount.Text = "0";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(134, 230);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(29, 13);
            this.label7.TabIndex = 22;
            this.label7.Text = "rows";
            // 
            // filter_severityFilter
            // 
            this.filter_severityFilter.FormattingEnabled = true;
            this.filter_severityFilter.Items.AddRange(new object[] {
            "Severe",
            "Warning"});
            this.filter_severityFilter.Location = new System.Drawing.Point(375, 167);
            this.filter_severityFilter.Name = "filter_severityFilter";
            this.filter_severityFilter.Size = new System.Drawing.Size(262, 21);
            this.filter_severityFilter.TabIndex = 23;
            this.filter_severityFilter.Visible = false;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(189, 231);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(361, 13);
            this.label8.TabIndex = 24;
            this.label8.Text = "Double click to see the the details of the record related to the error/warning";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btn_disconnect);
            this.groupBox1.Controls.Add(this.tb_DB);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.label11);
            this.groupBox1.Controls.Add(this.tb_pass);
            this.groupBox1.Controls.Add(this.label10);
            this.groupBox1.Controls.Add(this.tb_user);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.tb_SQLInstance);
            this.groupBox1.Controls.Add(this.btn_connect);
            this.groupBox1.Controls.Add(this.btn_getCurrentDBConn);
            this.groupBox1.Controls.Add(this.lbl_connstatus);
            this.groupBox1.Location = new System.Drawing.Point(18, 4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(619, 132);
            this.groupBox1.TabIndex = 31;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Database Connection";
            // 
            // btn_disconnect
            // 
            this.btn_disconnect.Enabled = false;
            this.btn_disconnect.Location = new System.Drawing.Point(434, 76);
            this.btn_disconnect.Name = "btn_disconnect";
            this.btn_disconnect.Size = new System.Drawing.Size(168, 23);
            this.btn_disconnect.TabIndex = 40;
            this.btn_disconnect.Text = "Disconnect";
            this.btn_disconnect.UseVisualStyleBackColor = true;
            this.btn_disconnect.Click += new System.EventHandler(this.btn_disconnect_Click);
            // 
            // tb_DB
            // 
            this.tb_DB.Location = new System.Drawing.Point(129, 100);
            this.tb_DB.Name = "tb_DB";
            this.tb_DB.Size = new System.Drawing.Size(260, 20);
            this.tb_DB.TabIndex = 39;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 105);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(100, 13);
            this.label1.TabIndex = 38;
            this.label1.Text = "Database (override)";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(12, 76);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(53, 13);
            this.label11.TabIndex = 37;
            this.label11.Text = "Password";
            // 
            // tb_pass
            // 
            this.tb_pass.Location = new System.Drawing.Point(129, 73);
            this.tb_pass.Name = "tb_pass";
            this.tb_pass.PasswordChar = '*';
            this.tb_pass.Size = new System.Drawing.Size(260, 20);
            this.tb_pass.TabIndex = 36;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(12, 50);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(29, 13);
            this.label10.TabIndex = 35;
            this.label10.Text = "User";
            // 
            // tb_user
            // 
            this.tb_user.Location = new System.Drawing.Point(129, 46);
            this.tb_user.Name = "tb_user";
            this.tb_user.Size = new System.Drawing.Size(260, 20);
            this.tb_user.TabIndex = 34;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(12, 24);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(72, 13);
            this.label9.TabIndex = 33;
            this.label9.Text = "SQL Instance";
            // 
            // tb_SQLInstance
            // 
            this.tb_SQLInstance.Location = new System.Drawing.Point(129, 19);
            this.tb_SQLInstance.Name = "tb_SQLInstance";
            this.tb_SQLInstance.Size = new System.Drawing.Size(260, 20);
            this.tb_SQLInstance.TabIndex = 32;
            // 
            // btn_connect
            // 
            this.btn_connect.Location = new System.Drawing.Point(434, 47);
            this.btn_connect.Name = "btn_connect";
            this.btn_connect.Size = new System.Drawing.Size(168, 23);
            this.btn_connect.TabIndex = 31;
            this.btn_connect.Text = "Connect";
            this.btn_connect.UseVisualStyleBackColor = true;
            this.btn_connect.Click += new System.EventHandler(this.btn_connect_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(110, 13);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(47, 13);
            this.label2.TabIndex = 33;
            this.label2.Text = "Warning";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(36, 13);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(29, 13);
            this.label12.TabIndex = 35;
            this.label12.Text = "Error";
            // 
            // legend
            // 
            this.legend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.legend.Controls.Add(this.panel3);
            this.legend.Controls.Add(this.label13);
            this.legend.Controls.Add(this.panel2);
            this.legend.Controls.Add(this.panel1);
            this.legend.Controls.Add(this.label12);
            this.legend.Controls.Add(this.label2);
            this.legend.Location = new System.Drawing.Point(18, 674);
            this.legend.Name = "legend";
            this.legend.Size = new System.Drawing.Size(310, 38);
            this.legend.TabIndex = 36;
            // 
            // panel3
            // 
            this.panel3.BackColor = System.Drawing.Color.Orange;
            this.panel3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel3.Location = new System.Drawing.Point(174, 7);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(27, 25);
            this.panel3.TabIndex = 38;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(204, 13);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(77, 13);
            this.label13.TabIndex = 37;
            this.label13.Text = "Orphaned Item";
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.Red;
            this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel2.Location = new System.Drawing.Point(6, 7);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(27, 25);
            this.panel2.TabIndex = 37;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.Yellow;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Location = new System.Drawing.Point(80, 7);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(27, 25);
            this.panel1.TabIndex = 36;
            // 
            // cb_enabledonly
            // 
            this.cb_enabledonly.AutoSize = true;
            this.cb_enabledonly.Location = new System.Drawing.Point(21, 192);
            this.cb_enabledonly.Name = "cb_enabledonly";
            this.cb_enabledonly.Size = new System.Drawing.Size(143, 17);
            this.cb_enabledonly.TabIndex = 38;
            this.cb_enabledonly.Text = "Show enabled items only";
            this.cb_enabledonly.UseVisualStyleBackColor = true;
            // 
            // cb_errorsOnly
            // 
            this.cb_errorsOnly.AutoSize = true;
            this.cb_errorsOnly.Location = new System.Drawing.Point(21, 210);
            this.cb_errorsOnly.Name = "cb_errorsOnly";
            this.cb_errorsOnly.Size = new System.Drawing.Size(104, 17);
            this.cb_errorsOnly.TabIndex = 39;
            this.cb_errorsOnly.Text = "Show errors only";
            this.cb_errorsOnly.UseVisualStyleBackColor = true;
            // 
            // menu_ID
            // 
            this.menu_ID.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuitem_filterByID});
            this.menu_ID.Name = "menu_ID";
            this.menu_ID.Size = new System.Drawing.Size(153, 26);
            // 
            // menuitem_filterByID
            // 
            this.menuitem_filterByID.Name = "menuitem_filterByID";
            this.menuitem_filterByID.Size = new System.Drawing.Size(152, 22);
            this.menuitem_filterByID.Text = "Filter by this ID";
            // 
            // menu_row
            // 
            this.menu_row.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menu_item_retest});
            this.menu_row.Name = "menu_ID";
            this.menu_row.Size = new System.Drawing.Size(183, 48);
            // 
            // menu_item_retest
            // 
            this.menu_item_retest.Enabled = false;
            this.menu_item_retest.Name = "menu_item_retest";
            this.menu_item_retest.Size = new System.Drawing.Size(182, 22);
            this.menu_item_retest.Text = "Re-validate this item";
            // 
            // frmDBVal
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1006, 714);
            this.Controls.Add(this.cb_errorsOnly);
            this.Controls.Add(this.cb_enabledonly);
            this.Controls.Add(this.legend);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.lblTotalCount);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.lbl_filteredCount);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.btnClearFilter);
            this.Controls.Add(this.btnApplyFilter);
            this.Controls.Add(this.filter_comparator);
            this.Controls.Add(this.filter_columns);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.lbl_validating);
            this.Controls.Add(this.dgvErrorList);
            this.Controls.Add(this.btn_start);
            this.Controls.Add(this.filter_severityFilter);
            this.Controls.Add(this.filter_text);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmDBVal";
            this.Text = "Database Validator Tool";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmDBVal_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.dgvErrorList)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.legend.ResumeLayout(false);
            this.legend.PerformLayout();
            this.menu_ID.ResumeLayout(false);
            this.menu_row.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lbl_connstatus;
        private System.Windows.Forms.Button btn_start;
        private System.Windows.Forms.DataGridView dgvErrorList;
        private System.Windows.Forms.Label lbl_validating;
        private System.Windows.Forms.Button btn_getCurrentDBConn;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox filter_columns;
        private System.Windows.Forms.ComboBox filter_comparator;
        private System.Windows.Forms.TextBox filter_text;
        private System.Windows.Forms.Button btnApplyFilter;
        private System.Windows.Forms.Button btnClearFilter;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label lbl_filteredCount;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label lblTotalCount;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox filter_severityFilter;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox tb_pass;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox tb_user;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox tb_SQLInstance;
        private System.Windows.Forms.Button btn_connect;
        private System.Windows.Forms.TextBox tb_DB;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btn_disconnect;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Panel legend;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.CheckBox cb_enabledonly;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.CheckBox cb_errorsOnly;
        private System.Windows.Forms.ContextMenuStrip menu_ID;
        private System.Windows.Forms.ToolStripMenuItem menuitem_filterByID;
        private System.Windows.Forms.ContextMenuStrip menu_row;
        private System.Windows.Forms.ToolStripMenuItem menu_item_retest;
    }
}