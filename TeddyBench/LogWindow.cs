using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeddyBench.Core;

namespace TeddyBench
{
    /// <summary>Displays the UI independent <see cref="Logger"/> and pops up when new messages arrive.</summary>
    public partial class LogWindow : Form
    {
        private DateTime LastWindowUpdate = DateTime.MinValue;

        public LogWindow()
        {
            InitializeComponent();

            Timer Updatetimer = new Timer();
            Updatetimer.Tick += Updatetimer_Tick;
            Updatetimer.Interval = 250;
            Updatetimer.Start();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();

            base.OnClosing(e);
        }

        private void Updatetimer_Tick(object sender, EventArgs e)
        {
            DateTime lastTextUpdate = Logger.LastUpdate;

            if (LastWindowUpdate < lastTextUpdate)
            {
                if(!Visible)
                {
                    Show();
                }

                txtLog.Text = Logger.GetText();
                if (txtLog.Text.Length > 0)
                {
                    txtLog.SelectionStart = txtLog.Text.Length - 1;
                    txtLog.ScrollToCaret();
                }
                LastWindowUpdate = lastTextUpdate;
            }
        }
    }
}
