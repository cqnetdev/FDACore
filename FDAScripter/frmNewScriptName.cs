﻿using System;
using System.Windows.Forms;

namespace FDAScripter
{
    public partial class FrmNewScriptName : Form
    {
        public string ScriptName;

        public FrmNewScriptName()
        {
            InitializeComponent();
        }

        private void BTN_Continue(object sender, EventArgs e)
        {
            ScriptName = tbName.Text;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BTN_Cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}