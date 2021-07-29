using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FDAManager
{
    public partial class FancyTextBox : TextBox
    {
        private readonly Color _textcolor;
        private readonly Color _backcolor;
        
        public int FlashTime { get { return flashtimer.Interval; } set { flashtimer.Interval = value; } }
        public Color FlashForeColor { get; set; }
        public Color FlashBackgroundColor { get; set; }

        public FancyTextBox()
        {
            InitializeComponent();
            _textcolor = this.ForeColor;
            _backcolor = this.BackColor;

            this.TextChanged += FancyTextBox_TextChanged;
            FlashTime = 500;
            FlashForeColor = Color.White;
            FlashBackgroundColor = Color.RoyalBlue;
            InhibitFlash();
        }

  
        private void FancyTextBox_TextChanged(object sender, EventArgs e)
        {
            if (inhibitTimer.Enabled)
                return;

            this.ForeColor = FlashForeColor;
            this.BackColor = FlashBackgroundColor;
            this.Refresh();
            if (flashtimer.Enabled)
            {
                flashtimer.Enabled = false;
            }
            flashtimer.Enabled = true;
        }


        private void Flashtimer_Tick(object sender, EventArgs e)
        {
            flashtimer.Enabled = false;
            this.ForeColor = _textcolor;
            this.BackColor = _backcolor;            
            this.Refresh();
        }

        private void InhibitFlash()
        {
            flashtimer.Enabled = false;
            this.ForeColor = _textcolor;
            this.BackColor = _backcolor;
            if (inhibitTimer.Enabled)
                inhibitTimer.Enabled = false;
            inhibitTimer.Enabled = true;
        }
        private void InhibitTimer_Tick(object sender, EventArgs e)
        {
            inhibitTimer.Enabled = false;
        }

        public new void Clear()
        {
            InhibitFlash();
            DataBindings.Clear();
            base.Clear();
        }

    
    }
}
