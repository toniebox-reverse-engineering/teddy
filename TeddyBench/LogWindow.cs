using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TeddyBench
{
    public partial class LogWindow : Form
    {
        protected static LogWindow Instance;
        protected StringBuilder LogString = new StringBuilder();
        private DateTime LastWindowUpdate;
        private DateTime LastTextUpdate;

        public static eLogLevel LogLevel = eLogLevel.Warning;

        public enum eLogLevel
        {
            DebugVerbose = 0,
            Debug = 1,
            Information = 2,
            Warning = 3,
            Error = 4
        }

        public LogWindow()
        {
            InitializeComponent();
            Instance = this;

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
            if (LastWindowUpdate < LastTextUpdate)
            {
                if(!Visible)
                {
                    Show();
                }

                lock (Instance.LogString)
                {
                    txtLog.Text = LogString.ToString();
                    if (txtLog.Text.Length > 0)
                    {
                        txtLog.SelectionStart = txtLog.Text.Length - 1;
                        txtLog.ScrollToCaret();
                    }
                    LastWindowUpdate = DateTime.Now;
                }
            }
        }

        public static void Log(eLogLevel level, string message)
        {
            if(level < LogLevel)
            {
                return;
            }

            lock (Instance.LogString)
            {
                Instance.LogString.AppendLine(message);
                Instance.LastTextUpdate = DateTime.Now;
            }
        }
    }
}
