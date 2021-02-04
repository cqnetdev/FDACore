namespace FDAInterface
{
    partial class frmCommsViewer
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
            this.dgvCommsLog = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCommsLog)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvCommsLog
            // 
            this.dgvCommsLog.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCommsLog.Location = new System.Drawing.Point(12, 107);
            this.dgvCommsLog.Name = "dgvCommsLog";
            this.dgvCommsLog.RowHeadersWidth = 62;
            this.dgvCommsLog.RowTemplate.Height = 28;
            this.dgvCommsLog.Size = new System.Drawing.Size(1186, 569);
            this.dgvCommsLog.TabIndex = 0;
            // 
            // frmCommsViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1210, 688);
            this.Controls.Add(this.dgvCommsLog);
            this.Name = "frmCommsViewer";
            this.Text = "frmCommsViewer";
            ((System.ComponentModel.ISupportInitialize)(this.dgvCommsLog)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvCommsLog;
    }
}