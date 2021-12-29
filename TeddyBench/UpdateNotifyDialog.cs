using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace TeddyBench
{
    public partial class UpdateNotifyDialog : Form
    {
        public UpdateNotifyDialog(string version, string title)
        {
            InitializeComponent();
            Text += " " + version;
            label1.Text += " " + title;
        }

        private void btnYes_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Yes;
            Close();
        }

        private void btnNo_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.No;
            Close();
        }
    }
}
