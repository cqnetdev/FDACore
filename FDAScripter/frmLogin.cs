using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace FDAScripter
{
    public partial class frmLogin : Form
    {

        public frmLogin()
        {
            InitializeComponent();
       
            foreach (string recent in Program.RecentConnections.Keys)
            {
                cbRecent.Items.Add(recent);
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            btnConnect.Enabled = false;

            try
            {
                if (rbSQL.Checked)
                {
                   Program.ConnectSQL(tbInstance.Text, "FDA", tbUser.Text, tbPwd.Text,chkSavePwd.Checked);
                }
                else
                {
                    // connect to postgresql
                }
            } catch (Exception ex)
            {
                MessageBox.Show("Failed to connect to " + tbInstance.Text + ": " + ex.Message);
            }

            btnConnect.Enabled = true;
        }

        private void cbRecent_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedRecent = cbRecent.SelectedItem.ToString();
            Program.RecentConn recent = Program.RecentConnections[selectedRecent];
            tbInstance.Text = recent.server;
            tbUser.Text = recent.user;
            tbPwd.Text = recent.pass;
        }
    }
}
