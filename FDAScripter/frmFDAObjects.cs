using System;
using System.Data;
using System.Windows.Forms;

namespace FDAScripter
{
    public partial class FrmFDAObjects : Form
    {
        public FrmFDAObjects()
        {
            InitializeComponent();
        }

        private void RBTags_CheckedChanged(object sender, EventArgs e)
        {
            if (rbTags.Checked)
            {
                DataTable tagsTable = Program.QueryDB("select * from DataPointDefinitionStructures order by DPDUID");
                dgvFDAObjects.DataSource = tagsTable;
            }
        }

        private void RBConn_CheckedChanged(object sender, EventArgs e)
        {
            if (rbConn.Checked)
            {
                DataTable tagsTable = Program.QueryDB("select * from FDASourceConnections order by Description");
                dgvFDAObjects.DataSource = tagsTable;
            }
        }

        private void DGV_FDAObjects_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex != -1 && e.RowIndex != -1 && e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                DataGridViewCell c = (sender as DataGridView)[e.ColumnIndex, e.RowIndex];
                if (!c.Selected)
                {
                    c.DataGridView.ClearSelection();
                    c.DataGridView.CurrentCell = c;
                    c.Selected = true;
                }
                // open context menu
                ctxMenu.Show(Cursor.Position);
            }
        }

        private void MI_Insert_Click(object sender, EventArgs e)
        {
            // insert the selected item into the script on the script editor form
            string objectType = "";
            string objectID;
            string IDColName = "";

            FrmScriptEditor editor = (FrmScriptEditor)this.Owner;

            if (rbTags.Checked)
            {
                objectType = "tag";
                IDColName = "DPDUID";
            }
            else if (rbConn.Checked)
            {
                objectType = "conn";
                IDColName = "SCUID";
            }

            objectID = dgvFDAObjects.SelectedRows[0].Cells[dgvFDAObjects.Columns[IDColName].Index].Value.ToString();

            editor.InsertFDAObject(objectID, objectType);
        }
    }
}