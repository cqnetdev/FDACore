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
    public partial class frmRowDetails : Form
    {
        private class FieldValue
        {
            public string Field { get; }
            public string Value { get; }

            public FieldValue(string field,object value)
            {
                Field = field;
                if (value.GetType() == typeof(DBNull))
                    Value = "NULL";
                else
                    Value = value.ToString();
            }
        }

        private BindingList<FieldValue> CurrentRecord;
        private int CurrentIdx;
        private DataView Data;



        public frmRowDetails(string title,string subtitle,DataView data,int idx)
        {
            InitializeComponent();
            lbl_title.Text = title;
            lbl_subtitle.Text = subtitle;
            Data = data;
            CurrentRecord = new BindingList<FieldValue>();
            lbl_count.Text = (Data.Count).ToString();
            MoveTo(idx);
            recordBrowser.Visible = true;
        }

      


        public frmRowDetails(string title,string subtitle,string[] columns,object[] values)
        {
            InitializeComponent();

            lbl_title.Text = title;
            lbl_subtitle.Text = subtitle;

            BindingList<FieldValue> fieldValues = new BindingList<FieldValue>();
            FieldValue fv;
            object value;
            for (int i = 0; i < columns.Length; i++)
            {
                fv = new FieldValue(columns[i], values[i]);
                if (values[i].GetType() == typeof(DateTime))
                {
                    value = ((DateTime)values[i]).ToString("yyyy / MM / dd hh: mm:ss.fff tt");
                }
                else
                    value = values[i];
                fieldValues.Add(new FieldValue(columns[i],value));                     
            }

            detailsGrid.DataSource = fieldValues;   
            detailsGrid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            detailsGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            recordBrowser.Visible = false;
        }

        private void MoveTo(int idx)
        {
            if (idx >= 0 && idx < Data.Count)
            {
                lbl_idx.Text = (idx+1).ToString();
                CurrentIdx = idx;

                if (idx == Data.Count - 1)
                {
                    btnFirst.Enabled = true;
                    btnBack.Enabled = true;
                    btnNext.Enabled = false;
                    btnLast.Enabled = false;
                }
                else
                if (idx == 0)
                {
                    btnFirst.Enabled = false;
                    btnBack.Enabled = false;
                    btnNext.Enabled = true;
                    btnLast.Enabled = true;
                }
                else
                {
                    btnFirst.Enabled = true;
                    btnBack.Enabled = true;
                    btnNext.Enabled = true;
                    btnLast.Enabled = true;
                }


                CurrentRecord.Clear();
                string columnName;
                object value;
                for (int i = 0; i < Data.Table.Columns.Count; i++)
                {
                    
                    columnName = Data.Table.Columns[i].ColumnName;
                    if (Data.Table.Columns[i].DataType == typeof(DateTime))
                    {
                        value = ((DateTime)Data[idx][columnName]).ToString("yyyy/MM/dd hh:mm:ss.fff tt");
                    }
                    else
                    {
                        value = Data[idx][columnName];
                    }
                    CurrentRecord.Add(new FieldValue(columnName, value));
                }

                detailsGrid.DataSource = CurrentRecord;
                detailsGrid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                detailsGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            }
            
        }


        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnFirst_Click(object sender, EventArgs e)
        {
            MoveTo(0);
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            MoveTo(CurrentIdx - 1);
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            MoveTo(CurrentIdx + 1);
        }

        private void btnLast_Click(object sender, EventArgs e)
        {
            MoveTo(Data.Count - 1);
        }

   
    }
}
