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
    public partial class AskPassForm : Form
    {
        internal string Password;

        public AskPassForm(bool current, string pass = "00000000")
        {
            InitializeComponent();

            Password = pass;
            txtPass.Text = Password;
            label1.Text = label1.Text.Replace("_current_new_", current ? "current" : "new");
        }
        protected override void OnShown(EventArgs e)
        {
            txtPass.Text = Password;
            txtPass.Select();
            txtPass.Select(0, 8);
            base.OnShown(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Password = txtPass.Text;
            base.OnClosing(e);
        }

        private void txtPass_TextChanged(object sender, EventArgs e)
        {
            if (txtPass.Text.Length != 8 || !txtPass.Text.All("0123456789abcdefABCDEF".Contains))
            {
                txtPass.BackColor = Color.PaleVioletRed;
                btnOk.Enabled = false;
            }
            else
            {
                txtPass.BackColor = Color.LightGreen;
                btnOk.Enabled = true;
            }
        }
    }
}
