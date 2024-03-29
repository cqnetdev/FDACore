﻿using System;
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

        private void BTN_Connect_Click(object sender, EventArgs e)
        {
            btnConnect.Enabled = false;

            try
            {
                if (rbSQL.Checked)
                {
                    Program.ConnectSQL(tbInstance.Text, "FDA", tbUser.Text, tbPwd.Text, chkSavePwd.Checked);
                }
                else
                {
                    // connect to postgresql
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to connect to " + tbInstance.Text + ": " + ex.Message);
            }

            btnConnect.Enabled = true;
        }

        private void CB_Recent_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedRecent = cbRecent.SelectedItem.ToString();
            Program.RecentConn recent = Program.RecentConnections[selectedRecent];
            tbInstance.Text = recent.server;
            tbUser.Text = recent.user;
            tbPwd.Text = recent.pass;
        }
    }
}