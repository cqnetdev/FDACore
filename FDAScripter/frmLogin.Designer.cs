
namespace FDAScripter
{
    partial class frmLogin
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panel1 = new System.Windows.Forms.Panel();
            this.rbPG = new System.Windows.Forms.RadioButton();
            this.rbSQL = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this.tbInstance = new System.Windows.Forms.TextBox();
            this.tbUser = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.chkSavePwd = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.cbRecent = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tbPwd = new System.Windows.Forms.TextBox();
            this.btnConnect = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.rbPG);
            this.panel1.Controls.Add(this.rbSQL);
            this.panel1.Location = new System.Drawing.Point(33, 137);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(319, 108);
            this.panel1.TabIndex = 0;
            // 
            // rbPG
            // 
            this.rbPG.AutoSize = true;
            this.rbPG.Location = new System.Drawing.Point(24, 59);
            this.rbPG.Name = "rbPG";
            this.rbPG.Size = new System.Drawing.Size(129, 29);
            this.rbPG.TabIndex = 1;
            this.rbPG.TabStop = true;
            this.rbPG.Text = "PostgreSQL";
            this.rbPG.UseVisualStyleBackColor = true;
            // 
            // rbSQL
            // 
            this.rbSQL.AutoSize = true;
            this.rbSQL.Location = new System.Drawing.Point(24, 13);
            this.rbSQL.Name = "rbSQL";
            this.rbSQL.Size = new System.Drawing.Size(123, 29);
            this.rbSQL.TabIndex = 0;
            this.rbSQL.TabStop = true;
            this.rbSQL.Text = "SQL Server";
            this.rbSQL.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(43, 261);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(113, 25);
            this.label1.TabIndex = 1;
            this.label1.Text = "Server Name";
            // 
            // tbInstance
            // 
            this.tbInstance.Location = new System.Drawing.Point(192, 262);
            this.tbInstance.Name = "tbInstance";
            this.tbInstance.Size = new System.Drawing.Size(247, 31);
            this.tbInstance.TabIndex = 2;
            // 
            // tbUser
            // 
            this.tbUser.Location = new System.Drawing.Point(192, 299);
            this.tbUser.Name = "tbUser";
            this.tbUser.Size = new System.Drawing.Size(247, 31);
            this.tbUser.TabIndex = 4;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(43, 298);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(47, 25);
            this.label2.TabIndex = 3;
            this.label2.Text = "User";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(43, 335);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(87, 25);
            this.label3.TabIndex = 5;
            this.label3.Text = "Password";
            // 
            // chkSavePwd
            // 
            this.chkSavePwd.AutoSize = true;
            this.chkSavePwd.Location = new System.Drawing.Point(455, 338);
            this.chkSavePwd.Name = "chkSavePwd";
            this.chkSavePwd.Size = new System.Drawing.Size(155, 29);
            this.chkSavePwd.TabIndex = 7;
            this.chkSavePwd.Text = "Save Password";
            this.chkSavePwd.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label4.Location = new System.Drawing.Point(33, 29);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(393, 38);
            this.label4.TabIndex = 8;
            this.label4.Text = "Connect to an FDA Database";
            // 
            // cbRecent
            // 
            this.cbRecent.FormattingEnabled = true;
            this.cbRecent.Location = new System.Drawing.Point(103, 77);
            this.cbRecent.Name = "cbRecent";
            this.cbRecent.Size = new System.Drawing.Size(312, 33);
            this.cbRecent.TabIndex = 9;
            this.cbRecent.SelectedIndexChanged += new System.EventHandler(this.CB_Recent_SelectedIndexChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(33, 85);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(64, 25);
            this.label5.TabIndex = 10;
            this.label5.Text = "Recent";
            // 
            // tbPwd
            // 
            this.tbPwd.Location = new System.Drawing.Point(192, 336);
            this.tbPwd.Name = "tbPwd";
            this.tbPwd.PasswordChar = '*';
            this.tbPwd.Size = new System.Drawing.Size(247, 31);
            this.tbPwd.TabIndex = 6;
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(53, 396);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(118, 38);
            this.btnConnect.TabIndex = 11;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.BTN_Connect_Click);
            // 
            // frmLogin
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.cbRecent);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.chkSavePwd);
            this.Controls.Add(this.tbPwd);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.tbUser);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.tbInstance);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.panel1);
            this.Name = "frmLogin";
            this.Text = "Form1";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.RadioButton rbPG;
        private System.Windows.Forms.RadioButton rbSQL;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbInstance;
        private System.Windows.Forms.TextBox tbUser;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox chkSavePwd;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox cbRecent;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbPwd;
        private System.Windows.Forms.Button btnConnect;
    }
}

