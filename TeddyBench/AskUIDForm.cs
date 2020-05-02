using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TeddyBench
{
    public partial class AskUIDForm : Form
    {
        internal string Uid;

        public AskUIDForm()
        {
            InitializeComponent();
            txtUid.Select();
            txtUid.Select(6, 10);
            txtUid_TextChanged(null, null);
        }

        protected override void OnShown(EventArgs e)
        {
            txtUid.Text = Uid;
            txtUid.Select();
            txtUid.Select(6, 10);
            base.OnShown(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Uid = txtUid.Text;
            base.OnClosing(e);
        }

        private void txtUid_TextChanged(object sender, EventArgs e)
        {
            if (!txtUid.Text.StartsWith("E00403"))
            {
                txtUid.Text = "E00403";
                txtUid.Select(6, 1);
            }

            if (txtUid.Text.Length != 16 || !txtUid.Text.All("0123456789abcdefABCDEF".Contains))
            {
                txtUid.BackColor = Color.PaleVioletRed;
                btnOk.Enabled = false;
            }
            else
            {
                txtUid.BackColor = Color.LightGreen;
                btnOk.Enabled = true;
            }
        }

        private void txtUid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnOk.PerformClick();
            }
        }
    }
}
