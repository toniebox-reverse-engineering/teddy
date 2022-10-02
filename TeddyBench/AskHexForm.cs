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
    public partial class AskHexForm : Form
    {
        internal string ValueText;

        public uint Value => uint.Parse(ValueText, System.Globalization.NumberStyles.HexNumber);

        public AskHexForm(string value = null)
        {
            InitializeComponent();

            if(value == null)
            {
                value = (DateTimeOffset.Now.ToUnixTimeSeconds() - 0x50000000).ToString("X8");
            }
            ValueText = value;
            txtValue.Text = ValueText;
        }
        protected override void OnShown(EventArgs e)
        {
            txtValue.Text = ValueText;
            txtValue.Select();
            txtValue.Select(0, 8);
            base.OnShown(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            ValueText = txtValue.Text;
            base.OnClosing(e);
        }

        private void txtPass_TextChanged(object sender, EventArgs e)
        {
            if (txtValue.Text.Length != 8 || !txtValue.Text.All("0123456789abcdefABCDEF".Contains))
            {
                txtValue.BackColor = Color.PaleVioletRed;
                btnOk.Enabled = false;
            }
            else
            {
                txtValue.BackColor = Color.LightGreen;
                btnOk.Enabled = true;
            }
        }
    }
}
