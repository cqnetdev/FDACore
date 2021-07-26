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
    public partial class SuperSpecialPictureBox : UserControl
    {
        private ConnStatus _currentStatus;

        public Image ImageConnected { get; set; }
        public Image ImageDisconnected { get; set; }
        public Image ImageConnecting { get; set; }

        public Image ImageDefault { get; set; }

        public delegate void ClickHandler(object sender, EventArgs e);
        public event ClickHandler RetryRequested;

        public enum ConnStatus { Default, Connecting, Connected, Disconnected };
        public ConnStatus Status
        {
            get { return _currentStatus; }
            set
            {
                _currentStatus = value;
                switch (_currentStatus)
                {
                    case ConnStatus.Default:
                        pictureBox.Image = ImageDefault;
                        pictureBox.Cursor = Cursors.Default;
                        break;
                    case ConnStatus.Connected:
                        pictureBox.Image = ImageConnected;
                        pictureBox.Cursor = Cursors.Default;
                        break;
                    case ConnStatus.Connecting:
                        pictureBox.Image = ImageConnecting;
                        pictureBox.Cursor = Cursors.Default;
                        break;
                    case ConnStatus.Disconnected:
                        pictureBox.Image = ImageDisconnected;
                        pictureBox.Cursor = Cursors.Hand;
                        break;
                }
            }
        }

        public SuperSpecialPictureBox()
        {
            InitializeComponent();

            Status = ConnStatus.Default;
            pictureBox.Click += PictureBox_Click;
        }

       

        private void PictureBox_Click(object sender, EventArgs e)
        {
            if (_currentStatus == ConnStatus.Disconnected)
            {
                RetryRequested?.Invoke(this, new EventArgs());
            }
        }

        
    }
}
