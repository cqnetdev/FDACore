
namespace FDAScripter
{
    partial class frmScriptEditor
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
            this.label1 = new System.Windows.Forms.Label();
            this.tbCodeEditor = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.dgvScripts = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dgvScripts)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(65, 25);
            this.label1.TabIndex = 1;
            this.label1.Text = "Scripts";
            // 
            // tbCodeEditor
            // 
            this.tbCodeEditor.Location = new System.Drawing.Point(283, 88);
            this.tbCodeEditor.Multiline = true;
            this.tbCodeEditor.Name = "tbCodeEditor";
            this.tbCodeEditor.Size = new System.Drawing.Size(739, 569);
            this.tbCodeEditor.TabIndex = 2;
            this.tbCodeEditor.TextChanged += new System.EventHandler(this.tbCodeEditor_TextChanged);
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(906, 671);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(115, 42);
            this.btnSave.TabIndex = 4;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // dgvScripts
            // 
            this.dgvScripts.AllowUserToAddRows = false;
            this.dgvScripts.AllowUserToDeleteRows = false;
            this.dgvScripts.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvScripts.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.dgvScripts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvScripts.ColumnHeadersVisible = false;
            this.dgvScripts.Location = new System.Drawing.Point(13, 88);
            this.dgvScripts.MultiSelect = false;
            this.dgvScripts.Name = "dgvScripts";
            this.dgvScripts.ReadOnly = true;
            this.dgvScripts.RowHeadersVisible = false;
            this.dgvScripts.RowHeadersWidth = 62;
            this.dgvScripts.RowTemplate.Height = 33;
            this.dgvScripts.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dgvScripts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvScripts.Size = new System.Drawing.Size(264, 569);
            this.dgvScripts.TabIndex = 6;
            this.dgvScripts.SelectionChanged += new System.EventHandler(this.dgvScripts_SelectionChanged);
            // 
            // frmScriptEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1040, 727);
            this.Controls.Add(this.dgvScripts);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.tbCodeEditor);
            this.Controls.Add(this.label1);
            this.Name = "frmScriptEditor";
            this.Text = "frScriptEditor";
            ((System.ComponentModel.ISupportInitialize)(this.dgvScripts)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbCodeEditor;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.DataGridView dgvScripts;
    }
}