
namespace FDAScripter
{
    partial class frmCompileResult
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
            this.dgvDiagnostics = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDiagnostics)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvDiagnostics
            // 
            this.dgvDiagnostics.AllowUserToAddRows = false;
            this.dgvDiagnostics.AllowUserToDeleteRows = false;
            this.dgvDiagnostics.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvDiagnostics.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvDiagnostics.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvDiagnostics.Location = new System.Drawing.Point(0, 0);
            this.dgvDiagnostics.Name = "dgvDiagnostics";
            this.dgvDiagnostics.ReadOnly = true;
            this.dgvDiagnostics.RowHeadersWidth = 62;
            this.dgvDiagnostics.RowTemplate.Height = 33;
            this.dgvDiagnostics.Size = new System.Drawing.Size(1238, 330);
            this.dgvDiagnostics.TabIndex = 0;
            // 
            // frmCompileResult
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1238, 330);
            this.Controls.Add(this.dgvDiagnostics);
            this.Name = "frmCompileResult";
            this.Text = "Script Check Results";
            ((System.ComponentModel.ISupportInitialize)(this.dgvDiagnostics)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvDiagnostics;
    }
}