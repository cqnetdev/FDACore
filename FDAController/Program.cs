using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FDAController
{
    [SupportedOSPlatform("windows")]
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Process[] proc = Process.GetProcessesByName("FDAController");
            if (proc.Length > 1)
            {
                MessageBox.Show("There is already an instance of the FDA Controller running");
                Application.Exit();
            }
            else
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FrmMain());
            }
        }
    }
}