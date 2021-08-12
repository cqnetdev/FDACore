using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Data;
using System.Windows.Forms;

namespace FDAScripter
{
    public partial class FrmScriptEditor : Form
    {
        private bool inhibitTextChangedTrigger = true;
        private string _currentScriptName;
        private FrmFDAObjects objectsForm;

        public FrmScriptEditor()
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

        private void DGV_Scripts_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvScripts.SelectedRows.Count < 1) return;

            if (!DiscardCheckandConfirm())
                return;

            _currentScriptName = dgvScripts.SelectedRows[0].Cells[1].Value.ToString();
            DataTable result = Program.QueryDB("select enabled,script from fda_scripts where script_name = '" + _currentScriptName + "'");

            inhibitTextChangedTrigger = true;
            btnSave.Enabled = false;
            tbCodeEditor.Text = result.Rows[0].Field<string>("script");
        }

        private void CodeEditor_TextChanged(object sender, EventArgs e)
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

        private void Save_Click(object sender, EventArgs e)
        {
            string scriptName = MakeStringQuerySafe(_currentScriptName);
            string code = MakeStringQuerySafe(tbCodeEditor.Text);
            Program.ExecuteDBquery("update fda_scripts set script = '" + code + "' where script_name = '" + scriptName + "'");
            btnSave.Enabled = false;
        }

        private void MenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            MessageBox.Show(e.ClickedItem.Text);
            if (e.ClickedItem.Text == "Exit")
                Close();
        }

        private void AddAscript_Click(object sender, EventArgs e)
        {
            FrmNewScriptName nameDlg = new();
            if (nameDlg.ShowDialog() == DialogResult.OK)
            {
                if (!DiscardCheckandConfirm()) return;

                string scriptName = MakeStringQuerySafe(nameDlg.ScriptName);

                Program.ExecuteDBquery(@"insert into fda_scripts (script_name,script,depends_on,run_spec,load_order,enabled)
                    values ('" + scriptName + "','','','',1,0)");
            }

            RefreshScriptList();

            SetSelectedScript(nameDlg.ScriptName);
        }

        private static string MakeStringQuerySafe(string unsafeString)
        {
            return unsafeString.Replace("'", "''");
        }

        /// <summary>
        /// Check if the user wants to save changes to the current script and saves it if the response is 'yes'
        /// </summary>
        /// <returns>true if the operation can continue (user selected yes or no, or there are no changes to save).
        /// false if the user selected cancel</returns>
        private bool DiscardCheckandConfirm()
        {
            if (btnSave.Enabled)
            {
                DialogResult response = MessageBox.Show("Save changes to the script '" + _currentScriptName + "'?", "Confirmation", MessageBoxButtons.YesNoCancel);
                if (response == DialogResult.Yes)
                {
                    Save_Click(this, new EventArgs());
                    return true;
                }
                else if (response == DialogResult.No)
                {
                    return true;
                }
                else if (response == DialogResult.Cancel)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetSelectedScript(string scriptname)
        {
            int rowIndex = -1;
            foreach (DataGridViewRow row in dgvScripts.Rows)
            {
                if (row.Cells[1].Value.ToString().Equals(scriptname))
                {
                    rowIndex = row.Index;
                    break;
                }
            }

            if (rowIndex >= 0)
            {
                dgvScripts.Rows[rowIndex].Selected = true;
            }
        }

        private void CheckScript_Click(object sender, EventArgs e)
        {
            ImmutableArray<Diagnostic> result = Program.CheckScript(tbCodeEditor.Text);

            if (result.IsEmpty)
                MessageBox.Show("Looking good!");
            else
            {
                FrmCompileResult resultsForm = new(result);
                resultsForm.Show();
            }
        }

        private void OpenFDAObjects_Click(object sender, EventArgs e)
        {
            if (objectsForm == null)
            {
                objectsForm = new FrmFDAObjects();
                objectsForm.Show(this);
            }
            else
                objectsForm.BringToFront();
        }

        public void InsertFDAObject(string ID, string type)
        {
            switch (type)
            {
                case "tag": tbCodeEditor.SelectedText = "GetTag(\"" + ID + "\")"; break;
                case "conn": tbCodeEditor.SelectedText = "GetConnection(\"" + ID + "\")"; break;
            }
        }

        private void ScriptEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!DiscardCheckandConfirm())
                e.Cancel = true;
        }
    }
}