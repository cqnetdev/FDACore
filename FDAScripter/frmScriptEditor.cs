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
    public partial class frmScriptEditor : Form
    {
        private bool inhibitTextChangedTrigger = true;

        public frmScriptEditor()
        {
            InitializeComponent();

            RefreshScriptList();
       
        }


        private void RefreshScriptList()
        {
            DataTable result = Program.QueryDB("select enabled,script_name from fda_scripts order by script_name");
            //lbScripts.DataSource = result;
            dgvScripts.DataSource = result;
            dgvScripts.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dgvScripts.Columns[0].ReadOnly = false;
            dgvScripts.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void lbScripts_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }

        private void dgvScripts_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvScripts.SelectedRows.Count < 1) return;
            string scriptName = dgvScripts.SelectedRows[0].Cells[1].Value.ToString();
            DataTable result = Program.QueryDB("select enabled,script from fda_scripts where script_name = '" + scriptName + "'");

            inhibitTextChangedTrigger = true;
            btnSave.Enabled = false;
            tbCodeEditor.Text = result.Rows[0].Field<string>("script");
           
        }

        private void tbCodeEditor_TextChanged(object sender, EventArgs e)
        {
            if (inhibitTextChangedTrigger)
            {
                inhibitTextChangedTrigger = false;
                return;
            }
            else
            {
                btnSave.Enabled = true;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            Program.ExecuteDBquery("update fda_scripts set script = '" + tbCodeEditor.Text + "' where script_name = '" + dgvScripts.SelectedRows[0].Cells[0].Value.ToString() + "'");
            btnSave.Enabled = false;
        }

      
    }
}
