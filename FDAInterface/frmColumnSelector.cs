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
    public partial class frmColumnSelector : Form
    {
  
        public List<string> SelectedColumns
        {
            get
            {
                List<string> selected = new List<string>();
                foreach(string item in list_columns.CheckedItems)
                {
                    selected.Add(item);
                }
                return selected;
            }
         
        }

        public frmColumnSelector(string[] columns)
        {
            InitializeComponent();
            string[] columnDetails;
            int idx = 0;
            foreach (string column in columns)
            {
                columnDetails = column.Split('|');
                list_columns.Items.Add(columnDetails[0]);
                if (columnDetails[1] == "True")
                    list_columns.SetItemChecked(idx, true);
                else
                    list_columns.SetItemChecked(idx, false);
                idx++;
            }
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
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
