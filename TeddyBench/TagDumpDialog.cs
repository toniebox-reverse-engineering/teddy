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
    public partial class TagDumpDialog : Form
    {
        internal string String = "";

        public TagDumpDialog(bool display, string defaultString = "")
        {
            InitializeComponent();

            if(display)
            {
                label1.Text = "This is the UID and memory content of your tag";
                BtnCancel.Visible = false;
            }
            else
            {
                label1.Text = "Enter the UID and memory content of your tag";
            }

            textBox1.Text = defaultString;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            String = textBox1.Text.Trim();
            base.OnClosing(e);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
