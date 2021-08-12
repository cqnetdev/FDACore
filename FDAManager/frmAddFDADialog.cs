using System;
using System.Windows.Forms;

namespace FDAManager
{
    public partial class FrmAddFDADialog : Form
    {
        public Connection connection;

        public FrmAddFDADialog()
        {
            InitializeComponent();
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            connection = new Connection(tb_host.Text, tb_FDAName.Text);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}