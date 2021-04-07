﻿namespace FDAInterface
{
    partial class frmMain2
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain2));
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("Connections", 9, 9);
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.Version = new System.Windows.Forms.ToolStripStatusLabel();
            this.FDAStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.Uptime = new System.Windows.Forms.ToolStripStatusLabel();
            this.dbtypeDisplay = new System.Windows.Forms.ToolStripStatusLabel();
            this.mqttStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fDAToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.connectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startwithConsoleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pauseToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.recentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.communicationsStatsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mQTTQueryTestToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabDetails = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tabQueues = new System.Windows.Forms.TabPage();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.imgControllerServiceStatus = new System.Windows.Forms.PictureBox();
            this.imgBrokerStatus = new System.Windows.Forms.PictureBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.tree = new System.Windows.Forms.TreeView();
            this.tb_connectionstate = new System.Windows.Forms.TextBox();
            this.connDetails = new FDAInterface.ConnDetailsCtrl();
            this.qHist = new FDAInterface.QueueHistory();
            this.statusStrip1.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabDetails.SuspendLayout();
            this.panel1.SuspendLayout();
            this.tabQueues.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imgControllerServiceStatus)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.imgBrokerStatus)).BeginInit();
            this.SuspendLayout();
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "green.png");
            this.imageList1.Images.SetKeyName(1, "yellow.png");
            this.imageList1.Images.SetKeyName(2, "red.png");
            this.imageList1.Images.SetKeyName(3, "grey.png");
            this.imageList1.Images.SetKeyName(4, "green-pause.png");
            this.imageList1.Images.SetKeyName(5, "yellow-pause.png");
            this.imageList1.Images.SetKeyName(6, "red-pause.png");
            this.imageList1.Images.SetKeyName(7, "grey-pause.png");
            this.imageList1.Images.SetKeyName(8, "list3.png");
            this.imageList1.Images.SetKeyName(9, "connection.png");
            // 
            // statusStrip1
            // 
            this.statusStrip1.BackColor = System.Drawing.Color.Silver;
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.Version,
            this.FDAStatus,
            this.Uptime,
            this.dbtypeDisplay});
            this.statusStrip1.Location = new System.Drawing.Point(0, 667);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(2, 0, 21, 0);
            this.statusStrip1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.statusStrip1.Size = new System.Drawing.Size(1836, 36);
            this.statusStrip1.TabIndex = 23;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // Version
            // 
            this.Version.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.Version.BorderStyle = System.Windows.Forms.Border3DStyle.Bump;
            this.Version.Name = "Version";
            this.Version.Size = new System.Drawing.Size(195, 29);
            this.Version.Text = "FDA Version: unknown";
            // 
            // FDAStatus
            // 
            this.FDAStatus.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.FDAStatus.BorderStyle = System.Windows.Forms.Border3DStyle.Bump;
            this.FDAStatus.Name = "FDAStatus";
            this.FDAStatus.Size = new System.Drawing.Size(185, 29);
            this.FDAStatus.Text = "FDA Status: unknown";
            // 
            // Uptime
            // 
            this.Uptime.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.Uptime.BorderStyle = System.Windows.Forms.Border3DStyle.Bump;
            this.Uptime.Name = "Uptime";
            this.Uptime.Size = new System.Drawing.Size(203, 29);
            this.Uptime.Text = "FDA Runtime: unknown";
            // 
            // dbtypeDisplay
            // 
            this.dbtypeDisplay.Name = "dbtypeDisplay";
            this.dbtypeDisplay.Size = new System.Drawing.Size(132, 29);
            this.dbtypeDisplay.Text = "Database Type:";
            // 
            // mqttStatus
            // 
            this.mqttStatus.Margin = new System.Windows.Forms.Padding(0, 3, 0, 2);
            this.mqttStatus.Name = "mqttStatus";
            this.mqttStatus.Size = new System.Drawing.Size(23, 23);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.menuStrip1.GripMargin = new System.Windows.Forms.Padding(2, 2, 0, 2);
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fDAToolStripMenuItem,
            this.toolsToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1836, 38);
            this.menuStrip1.TabIndex = 22;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fDAToolStripMenuItem
            // 
            this.fDAToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.connectToolStripMenuItem,
            this.startToolStripMenuItem,
            this.startwithConsoleToolStripMenuItem,
            this.pauseToolStripMenuItem,
            this.stopToolStripMenuItem,
            this.recentToolStripMenuItem});
            this.fDAToolStripMenuItem.Name = "fDAToolStripMenuItem";
            this.fDAToolStripMenuItem.Size = new System.Drawing.Size(136, 34);
            this.fDAToolStripMenuItem.Text = "FDA Control";
            // 
            // connectToolStripMenuItem
            // 
            this.connectToolStripMenuItem.Name = "connectToolStripMenuItem";
            this.connectToolStripMenuItem.Size = new System.Drawing.Size(285, 36);
            this.connectToolStripMenuItem.Text = "New Connection";
            this.connectToolStripMenuItem.Click += new System.EventHandler(this.ConnectionToolStripMenuItem_Click);
            // 
            // startToolStripMenuItem
            // 
            this.startToolStripMenuItem.Enabled = false;
            this.startToolStripMenuItem.Name = "startToolStripMenuItem";
            this.startToolStripMenuItem.Size = new System.Drawing.Size(285, 36);
            this.startToolStripMenuItem.Text = "Start FDA";
            this.startToolStripMenuItem.Click += new System.EventHandler(this.startToolStripMenuItem_Click);
            // 
            // startwithConsoleToolStripMenuItem
            // 
            this.startwithConsoleToolStripMenuItem.Enabled = false;
            this.startwithConsoleToolStripMenuItem.Name = "startwithConsoleToolStripMenuItem";
            this.startwithConsoleToolStripMenuItem.Size = new System.Drawing.Size(285, 36);
            this.startwithConsoleToolStripMenuItem.Text = "Start (with console)";
            this.startwithConsoleToolStripMenuItem.Visible = false;
            this.startwithConsoleToolStripMenuItem.Click += new System.EventHandler(this.startwithConsoleToolStripMenuItem_Click);
            // 
            // pauseToolStripMenuItem
            // 
            this.pauseToolStripMenuItem.Enabled = false;
            this.pauseToolStripMenuItem.Name = "pauseToolStripMenuItem";
            this.pauseToolStripMenuItem.Size = new System.Drawing.Size(285, 36);
            this.pauseToolStripMenuItem.Text = "Pause";
            this.pauseToolStripMenuItem.Visible = false;
            this.pauseToolStripMenuItem.Click += new System.EventHandler(this.pauseToolStripMenuItem_Click);
            // 
            // stopToolStripMenuItem
            // 
            this.stopToolStripMenuItem.Enabled = false;
            this.stopToolStripMenuItem.Name = "stopToolStripMenuItem";
            this.stopToolStripMenuItem.Size = new System.Drawing.Size(285, 36);
            this.stopToolStripMenuItem.Text = "Stop FDA";
            this.stopToolStripMenuItem.Click += new System.EventHandler(this.stopToolStripMenuItem_Click);
            // 
            // recentToolStripMenuItem
            // 
            this.recentToolStripMenuItem.Name = "recentToolStripMenuItem";
            this.recentToolStripMenuItem.Size = new System.Drawing.Size(285, 36);
            this.recentToolStripMenuItem.Text = "Recent Connections";
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.communicationsStatsToolStripMenuItem,
            this.mQTTQueryTestToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(115, 34);
            this.toolsToolStripMenuItem.Text = "FDA Tools";
            // 
            // communicationsStatsToolStripMenuItem
            // 
            this.communicationsStatsToolStripMenuItem.Enabled = false;
            this.communicationsStatsToolStripMenuItem.Name = "communicationsStatsToolStripMenuItem";
            this.communicationsStatsToolStripMenuItem.Size = new System.Drawing.Size(308, 36);
            this.communicationsStatsToolStripMenuItem.Text = "Communications Stats";
            this.communicationsStatsToolStripMenuItem.Click += new System.EventHandler(this.communicationsStatsToolStripMenuItem_Click);
            // 
            // mQTTQueryTestToolStripMenuItem
            // 
            this.mQTTQueryTestToolStripMenuItem.Enabled = false;
            this.mQTTQueryTestToolStripMenuItem.Name = "mQTTQueryTestToolStripMenuItem";
            this.mQTTQueryTestToolStripMenuItem.Size = new System.Drawing.Size(308, 36);
            this.mQTTQueryTestToolStripMenuItem.Text = "Database Query";
            this.mQTTQueryTestToolStripMenuItem.Click += new System.EventHandler(this.mQTTQueryTestToolStripMenuItem_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabDetails);
            this.tabControl1.Controls.Add(this.tabQueues);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabControl1.ItemSize = new System.Drawing.Size(61, 20);
            this.tabControl1.Location = new System.Drawing.Point(409, 38);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1427, 629);
            this.tabControl1.TabIndex = 37;
            // 
            // tabDetails
            // 
            this.tabDetails.Controls.Add(this.panel1);
            this.tabDetails.Location = new System.Drawing.Point(4, 24);
            this.tabDetails.Name = "tabDetails";
            this.tabDetails.Padding = new System.Windows.Forms.Padding(3);
            this.tabDetails.Size = new System.Drawing.Size(1419, 601);
            this.tabDetails.TabIndex = 0;
            this.tabDetails.Text = "Connection Details";
            this.tabDetails.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.AutoScroll = true;
            this.panel1.AutoScrollMargin = new System.Drawing.Size(0, 30);
            this.panel1.AutoSize = true;
            this.panel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panel1.Controls.Add(this.connDetails);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1413, 595);
            this.panel1.TabIndex = 38;
            // 
            // tabQueues
            // 
            this.tabQueues.Controls.Add(this.qHist);
            this.tabQueues.Location = new System.Drawing.Point(4, 24);
            this.tabQueues.Name = "tabQueues";
            this.tabQueues.Padding = new System.Windows.Forms.Padding(3);
            this.tabQueues.Size = new System.Drawing.Size(1419, 603);
            this.tabQueues.TabIndex = 1;
            this.tabQueues.Text = "Queues";
            this.tabQueues.UseVisualStyleBackColor = true;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.panel3);
            this.panel2.Controls.Add(this.tree);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel2.Location = new System.Drawing.Point(0, 38);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(409, 629);
            this.panel2.TabIndex = 38;
            // 
            // panel3
            // 
            this.panel3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel3.Controls.Add(this.tb_connectionstate);
            this.panel3.Controls.Add(this.imgControllerServiceStatus);
            this.panel3.Controls.Add(this.imgBrokerStatus);
            this.panel3.Controls.Add(this.label3);
            this.panel3.Controls.Add(this.label2);
            this.panel3.Location = new System.Drawing.Point(19, 3);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(380, 142);
            this.panel3.TabIndex = 37;
            // 
            // imgControllerServiceStatus
            // 
            this.imgControllerServiceStatus.Location = new System.Drawing.Point(11, 109);
            this.imgControllerServiceStatus.Name = "imgControllerServiceStatus";
            this.imgControllerServiceStatus.Size = new System.Drawing.Size(25, 21);
            this.imgControllerServiceStatus.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.imgControllerServiceStatus.TabIndex = 3;
            this.imgControllerServiceStatus.TabStop = false;
            // 
            // imgBrokerStatus
            // 
            this.imgBrokerStatus.Location = new System.Drawing.Point(11, 72);
            this.imgBrokerStatus.Name = "imgBrokerStatus";
            this.imgBrokerStatus.Size = new System.Drawing.Size(25, 21);
            this.imgBrokerStatus.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.imgBrokerStatus.TabIndex = 2;
            this.imgBrokerStatus.TabStop = false;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(42, 104);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(209, 28);
            this.label3.TabIndex = 1;
            this.label3.Text = "FDA Controller Service";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(42, 67);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(126, 28);
            this.label2.TabIndex = 0;
            this.label2.Text = "MQTT Broker";
            // 
            // tree
            // 
            this.tree.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.tree.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tree.ImageIndex = 0;
            this.tree.ImageList = this.imageList1;
            this.tree.Location = new System.Drawing.Point(19, 153);
            this.tree.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tree.Name = "tree";
            treeNode1.ImageIndex = 9;
            treeNode1.Name = "Node0";
            treeNode1.SelectedImageIndex = 9;
            treeNode1.Text = "Connections";
            this.tree.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode1});
            this.tree.SelectedImageIndex = 0;
            this.tree.Size = new System.Drawing.Size(384, 471);
            this.tree.StateImageList = this.imageList1;
            this.tree.TabIndex = 31;
            this.tree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tree_AfterSelect);
            // 
            // tb_connectionstate
            // 
            this.tb_connectionstate.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tb_connectionstate.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tb_connectionstate.Location = new System.Drawing.Point(-1, 3);
            this.tb_connectionstate.Multiline = true;
            this.tb_connectionstate.Name = "tb_connectionstate";
            this.tb_connectionstate.ReadOnly = true;
            this.tb_connectionstate.Size = new System.Drawing.Size(380, 61);
            this.tb_connectionstate.TabIndex = 39;
            this.tb_connectionstate.Text = "FDA Connection Status";
            // 
            // connDetails
            // 
            this.connDetails.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.connDetails.ConnDetailsObj = null;
            this.connDetails.DbConnStr = "";
            this.connDetails.FDAExecutionID = "";
            this.connDetails.Location = new System.Drawing.Point(4, 5);
            this.connDetails.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.connDetails.Name = "connDetails";
            this.connDetails.Size = new System.Drawing.Size(1377, 934);
            this.connDetails.TabIndex = 37;
            // 
            // qHist
            // 
            this.qHist.ConnectionID = new System.Guid("00000000-0000-0000-0000-000000000000");
            this.qHist.Dock = System.Windows.Forms.DockStyle.Fill;
            this.qHist.Location = new System.Drawing.Point(3, 3);
            this.qHist.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.qHist.Name = "qHist";
            this.qHist.Priority = 0;
            this.qHist.Size = new System.Drawing.Size(1413, 597);
            this.qHist.TabIndex = 34;
            // 
            // frmMain2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1836, 703);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "frmMain2";
            this.Text = "FDA Manager";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmMain2_FormClosing);
            this.Shown += new System.EventHandler(this.frmMain2_Shown);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.tabDetails.ResumeLayout(false);
            this.tabDetails.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.tabQueues.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.imgControllerServiceStatus)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.imgBrokerStatus)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel FDAStatus;
        private System.Windows.Forms.ToolStripStatusLabel Version;
        private System.Windows.Forms.ToolStripStatusLabel Uptime;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fDAToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem startToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem startwithConsoleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pauseToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stopToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabDetails;
        private System.Windows.Forms.Panel panel1;
        private ConnDetailsCtrl connDetails;
        private System.Windows.Forms.TabPage tabQueues;
        private QueueHistory qHist;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.TreeView tree;
        private System.Windows.Forms.ToolStripMenuItem mQTTQueryTestToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem communicationsStatsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem connectToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel mqttStatus;
        private System.Windows.Forms.ToolStripStatusLabel dbtypeDisplay;
        private System.Windows.Forms.ToolStripMenuItem recentToolStripMenuItem;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.PictureBox imgControllerServiceStatus;
        private System.Windows.Forms.PictureBox imgBrokerStatus;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tb_connectionstate;
    }
}