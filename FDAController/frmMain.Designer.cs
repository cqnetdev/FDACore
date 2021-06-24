
namespace FDAController
{
    partial class frmMain
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
            this.btnFDA = new System.Windows.Forms.Button();
            this.lblLastupdate = new System.Windows.Forms.Label();
            this.lblControllerService = new System.Windows.Forms.Label();
            this.lblFDA = new System.Windows.Forms.Label();
            this.lblMQTT = new System.Windows.Forms.Label();
            this.btnFDAMonitor = new System.Windows.Forms.Button();
            this.btnMQTT = new System.Windows.Forms.Button();
            this.btnController = new System.Windows.Forms.Button();
            this.ctxStartFDA = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miStartFDAbg = new System.Windows.Forms.ToolStripMenuItem();
            this.miStartFDAConsole = new System.Windows.Forms.ToolStripMenuItem();
            this.ttFDAStart = new System.Windows.Forms.ToolTip(this.components);
            this.ctxStartFDA.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnFDA
            // 
            this.btnFDA.Location = new System.Drawing.Point(11, 107);
            this.btnFDA.Name = "btnFDA";
            this.btnFDA.Size = new System.Drawing.Size(57, 41);
            this.btnFDA.TabIndex = 0;
            this.btnFDA.Text = "Start";
            this.ttFDAStart.SetToolTip(this.btnFDA, "Right Click for start options");
            this.btnFDA.UseVisualStyleBackColor = true;
            this.btnFDA.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnFDA_MouseClick);
            // 
            // lblLastupdate
            // 
            this.lblLastupdate.AutoSize = true;
            this.lblLastupdate.Location = new System.Drawing.Point(12, 208);
            this.lblLastupdate.Name = "lblLastupdate";
            this.lblLastupdate.Size = new System.Drawing.Size(150, 20);
            this.lblLastupdate.TabIndex = 2;
            this.lblLastupdate.Text = "Last status update :";
            // 
            // lblControllerService
            // 
            this.lblControllerService.AutoSize = true;
            this.lblControllerService.Location = new System.Drawing.Point(74, 23);
            this.lblControllerService.Name = "lblControllerService";
            this.lblControllerService.Size = new System.Drawing.Size(184, 20);
            this.lblControllerService.TabIndex = 3;
            this.lblControllerService.Text = "Controller Service Status";
            // 
            // lblFDA
            // 
            this.lblFDA.AutoSize = true;
            this.lblFDA.Location = new System.Drawing.Point(76, 118);
            this.lblFDA.Name = "lblFDA";
            this.lblFDA.Size = new System.Drawing.Size(93, 20);
            this.lblFDA.TabIndex = 4;
            this.lblFDA.Text = "FDA Status";
            // 
            // lblMQTT
            // 
            this.lblMQTT.AutoSize = true;
            this.lblMQTT.Location = new System.Drawing.Point(76, 70);
            this.lblMQTT.Name = "lblMQTT";
            this.lblMQTT.Size = new System.Drawing.Size(103, 20);
            this.lblMQTT.TabIndex = 5;
            this.lblMQTT.Text = "MQTT Status";
            // 
            // btnFDAMonitor
            // 
            this.btnFDAMonitor.Location = new System.Drawing.Point(468, 187);
            this.btnFDAMonitor.Name = "btnFDAMonitor";
            this.btnFDAMonitor.Size = new System.Drawing.Size(129, 41);
            this.btnFDAMonitor.TabIndex = 7;
            this.btnFDAMonitor.Text = "FDA Monitor";
            this.btnFDAMonitor.UseVisualStyleBackColor = true;
            this.btnFDAMonitor.Click += new System.EventHandler(this.BtnFDAMonitor_Click);
            // 
            // btnMQTT
            // 
            this.btnMQTT.Location = new System.Drawing.Point(11, 60);
            this.btnMQTT.Name = "btnMQTT";
            this.btnMQTT.Size = new System.Drawing.Size(57, 41);
            this.btnMQTT.TabIndex = 8;
            this.btnMQTT.Text = "Start";
            this.btnMQTT.UseVisualStyleBackColor = true;
            this.btnMQTT.Click += new System.EventHandler(this.startstopBtn_Click);
            // 
            // btnController
            // 
            this.btnController.Location = new System.Drawing.Point(11, 13);
            this.btnController.Name = "btnController";
            this.btnController.Size = new System.Drawing.Size(57, 41);
            this.btnController.TabIndex = 9;
            this.btnController.Text = "Start";
            this.btnController.UseVisualStyleBackColor = true;
            this.btnController.Click += new System.EventHandler(this.startstopBtn_Click);
            // 
            // ctxStartFDA
            // 
            this.ctxStartFDA.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.ctxStartFDA.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miStartFDAbg,
            this.miStartFDAConsole});
            this.ctxStartFDA.Name = "ctxStartFDA";
            this.ctxStartFDA.Size = new System.Drawing.Size(369, 68);
            // 
            // miStartFDAbg
            // 
            this.miStartFDAbg.Name = "miStartFDAbg";
            this.miStartFDAbg.Size = new System.Drawing.Size(368, 32);
            this.miStartFDAbg.Text = "Start in Background Mode";
            this.miStartFDAbg.Click += new System.EventHandler(this.miStartFDAbg_Click);
            // 
            // miStartFDAConsole
            // 
            this.miStartFDAConsole.Name = "miStartFDAConsole";
            this.miStartFDAConsole.Size = new System.Drawing.Size(368, 32);
            this.miStartFDAConsole.Text = "Start FDA in Console (Debug) Mode";
            this.miStartFDAConsole.Click += new System.EventHandler(this.miStartFDAConsole_Click);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(609, 237);
            this.Controls.Add(this.btnController);
            this.Controls.Add(this.btnMQTT);
            this.Controls.Add(this.btnFDAMonitor);
            this.Controls.Add(this.lblMQTT);
            this.Controls.Add(this.lblFDA);
            this.Controls.Add(this.lblControllerService);
            this.Controls.Add(this.lblLastupdate);
            this.Controls.Add(this.btnFDA);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.Name = "frmMain";
            this.Text = "FDA Controller";
            this.ctxStartFDA.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnFDA;
        private System.Windows.Forms.Label lblLastupdate;
        private System.Windows.Forms.Label lblControllerService;
        private System.Windows.Forms.Label lblFDA;
        private System.Windows.Forms.Label lblMQTT;
        private System.Windows.Forms.Button btnFDAMonitor;
        private System.Windows.Forms.Button btnMQTT;
        private System.Windows.Forms.Button btnController;
        private System.Windows.Forms.ContextMenuStrip ctxStartFDA;
        private System.Windows.Forms.ToolStripMenuItem miStartFDAbg;
        private System.Windows.Forms.ToolStripMenuItem miStartFDAConsole;
        private System.Windows.Forms.ToolTip ttFDAStart;
    }
}

