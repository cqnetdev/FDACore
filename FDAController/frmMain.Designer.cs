
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
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.lblLastupdate = new System.Windows.Forms.Label();
            this.lblControllerService = new System.Windows.Forms.Label();
            this.lblFDA = new System.Windows.Forms.Label();
            this.lblMQTT = new System.Windows.Forms.Label();
            this.btnStartConsole = new System.Windows.Forms.Button();
            this.btnFDAMonitor = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(30, 49);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(174, 100);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "Start FDA";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(210, 49);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(174, 100);
            this.btnStop.TabIndex = 1;
            this.btnStop.Text = "Stop FDA";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // lblLastupdate
            // 
            this.lblLastupdate.AutoSize = true;
            this.lblLastupdate.Location = new System.Drawing.Point(30, 179);
            this.lblLastupdate.Name = "lblLastupdate";
            this.lblLastupdate.Size = new System.Drawing.Size(150, 20);
            this.lblLastupdate.TabIndex = 2;
            this.lblLastupdate.Text = "Last status update :";
            // 
            // lblControllerService
            // 
            this.lblControllerService.AutoSize = true;
            this.lblControllerService.Location = new System.Drawing.Point(30, 217);
            this.lblControllerService.Name = "lblControllerService";
            this.lblControllerService.Size = new System.Drawing.Size(170, 20);
            this.lblControllerService.TabIndex = 3;
            this.lblControllerService.Text = "FDA Controller Service";
            // 
            // lblFDA
            // 
            this.lblFDA.AutoSize = true;
            this.lblFDA.Location = new System.Drawing.Point(30, 289);
            this.lblFDA.Name = "lblFDA";
            this.lblFDA.Size = new System.Drawing.Size(42, 20);
            this.lblFDA.TabIndex = 4;
            this.lblFDA.Text = "FDA";
            // 
            // lblMQTT
            // 
            this.lblMQTT.AutoSize = true;
            this.lblMQTT.Location = new System.Drawing.Point(30, 253);
            this.lblMQTT.Name = "lblMQTT";
            this.lblMQTT.Size = new System.Drawing.Size(52, 20);
            this.lblMQTT.TabIndex = 5;
            this.lblMQTT.Text = "MQTT";
            // 
            // btnStartConsole
            // 
            this.btnStartConsole.Location = new System.Drawing.Point(535, 49);
            this.btnStartConsole.Name = "btnStartConsole";
            this.btnStartConsole.Size = new System.Drawing.Size(174, 100);
            this.btnStartConsole.TabIndex = 6;
            this.btnStartConsole.Text = "Start FDA in console (debug) mode";
            this.btnStartConsole.UseVisualStyleBackColor = true;
            this.btnStartConsole.Click += new System.EventHandler(this.btnStartConsole_Click);
            // 
            // btnFDAMonitor
            // 
            this.btnFDAMonitor.Location = new System.Drawing.Point(727, 49);
            this.btnFDAMonitor.Name = "btnFDAMonitor";
            this.btnFDAMonitor.Size = new System.Drawing.Size(174, 100);
            this.btnFDAMonitor.TabIndex = 7;
            this.btnFDAMonitor.Text = "Open FDA Monitor (doesn\'t work)";
            this.btnFDAMonitor.UseVisualStyleBackColor = true;
            this.btnFDAMonitor.Visible = false;
            this.btnFDAMonitor.Click += new System.EventHandler(this.btnFDAMonitor_Click);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(924, 400);
            this.Controls.Add(this.btnFDAMonitor);
            this.Controls.Add(this.btnStartConsole);
            this.Controls.Add(this.lblMQTT);
            this.Controls.Add(this.lblFDA);
            this.Controls.Add(this.lblControllerService);
            this.Controls.Add(this.lblLastupdate);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.Name = "frmMain";
            this.Text = "FDA Controller";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Label lblLastupdate;
        private System.Windows.Forms.Label lblControllerService;
        private System.Windows.Forms.Label lblFDA;
        private System.Windows.Forms.Label lblMQTT;
        private System.Windows.Forms.Button btnStartConsole;
        private System.Windows.Forms.Button btnFDAMonitor;
    }
}

