
namespace FDAScripter
{
    partial class frmFDAObjects
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
            this.dgvFDAObjects = new System.Windows.Forms.DataGridView();
            this.rbConn = new System.Windows.Forms.RadioButton();
            this.panel1 = new System.Windows.Forms.Panel();
            this.rbTags = new System.Windows.Forms.RadioButton();
            this.ctxMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuItemInsert = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFDAObjects)).BeginInit();
            this.panel1.SuspendLayout();
            this.ctxMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // dgvFDAObjects
            // 
            this.dgvFDAObjects.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.dgvFDAObjects.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvFDAObjects.Location = new System.Drawing.Point(22, 57);
            this.dgvFDAObjects.MultiSelect = false;
            this.dgvFDAObjects.Name = "dgvFDAObjects";
            this.dgvFDAObjects.ReadOnly = true;
            this.dgvFDAObjects.RowHeadersWidth = 62;
            this.dgvFDAObjects.RowTemplate.Height = 33;
            this.dgvFDAObjects.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvFDAObjects.Size = new System.Drawing.Size(1117, 459);
            this.dgvFDAObjects.TabIndex = 0;
            this.dgvFDAObjects.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dgvFDAObjects_CellMouseDown);
            // 
            // rbConn
            // 
            this.rbConn.AutoSize = true;
            this.rbConn.Location = new System.Drawing.Point(92, 6);
            this.rbConn.Name = "rbConn";
            this.rbConn.Size = new System.Drawing.Size(135, 29);
            this.rbConn.TabIndex = 2;
            this.rbConn.TabStop = true;
            this.rbConn.Text = "Connections";
            this.rbConn.UseVisualStyleBackColor = true;
            this.rbConn.CheckedChanged += new System.EventHandler(this.rbConn_CheckedChanged);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.rbTags);
            this.panel1.Controls.Add(this.rbConn);
            this.panel1.Location = new System.Drawing.Point(22, 7);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(239, 44);
            this.panel1.TabIndex = 3;
            // 
            // rbTags
            // 
            this.rbTags.AutoSize = true;
            this.rbTags.Location = new System.Drawing.Point(9, 6);
            this.rbTags.Name = "rbTags";
            this.rbTags.Size = new System.Drawing.Size(72, 29);
            this.rbTags.TabIndex = 3;
            this.rbTags.TabStop = true;
            this.rbTags.Text = "Tags";
            this.rbTags.UseVisualStyleBackColor = true;
            this.rbTags.CheckedChanged += new System.EventHandler(this.rbTags_CheckedChanged);
            // 
            // ctxMenu
            // 
            this.ctxMenu.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.ctxMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemInsert});
            this.ctxMenu.Name = "ctxMenu";
            this.ctxMenu.Size = new System.Drawing.Size(215, 36);
            // 
            // menuItemInsert
            // 
            this.menuItemInsert.Name = "menuItemInsert";
            this.menuItemInsert.Size = new System.Drawing.Size(240, 32);
            this.menuItemInsert.Text = "Insert into Script";
            this.menuItemInsert.Click += new System.EventHandler(this.menuItemInsert_Click);
            // 
            // frmFDAObjects
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1165, 528);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.dgvFDAObjects);
            this.Name = "frmFDAObjects";
            this.Text = "frmFDAObjects";
            ((System.ComponentModel.ISupportInitialize)(this.dgvFDAObjects)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ctxMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvFDAObjects;
        private System.Windows.Forms.RadioButton rbConn;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.RadioButton rbTags;
        private System.Windows.Forms.ContextMenuStrip ctxMenu;
        private System.Windows.Forms.ToolStripMenuItem menuItemInsert;
    }
}