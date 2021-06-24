using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FDAScripter
{
    public partial class frmNewScriptName : Form
    {
        public string ScriptName;
        public frmNewScriptName()
        {
            InitializeComponent();
        }


        private void btnContinue(object sender, EventArgs e)
        {
            ScriptName = tbName.Text;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
