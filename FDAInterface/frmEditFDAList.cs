using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FDAInterface
{
    public partial class frmEditFDAList : Form
    {
        public frmEditFDAList(BindingList<frmMain2.FDAConnection> connectionList)
        {
            InitializeComponent();
            dgvFDAList.DataSource = connectionList;

            if (dgvFDAList.Columns.Contains("FDAName"))
            {
                dgvFDAList.Columns["FDAName"].HeaderText = "FDA Name";
            }
        }


        private void btnAdd_Click(object sender, EventArgs e)
        {
            frmAddFDADialog dlg = new frmAddFDADialog();

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                // add it to the settings file
                Properties.Settings.Default.RecentConnections.Add(dlg.FDAConnection);
                Properties.Settings.Default.Save();

                // add it to the connections list
                BindingList<frmMain2.FDAConnection> connList = (BindingList<frmMain2.FDAConnection>)dgvFDAList.DataSource;
                connList.Add(new frmMain2.FDAConnection(dlg.FDAConnection));
            }


        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            BindingList<frmMain2.FDAConnection> connList = (BindingList<frmMain2.FDAConnection>)dgvFDAList.DataSource;
            foreach (DataGridViewRow row in dgvFDAList.SelectedRows)
            {
                // remove it from the list
                frmMain2.FDAConnection connSelectedForDeletion = (frmMain2.FDAConnection)row.DataBoundItem;
                connList.Remove(connSelectedForDeletion);

                // delete it from the settings file
                Properties.Settings.Default.RecentConnections.Remove(connSelectedForDeletion.FDAName + "|" + connSelectedForDeletion.Host);
                Properties.Settings.Default.Save();
            }
        }
    }
}
