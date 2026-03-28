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
    public partial class ReportForm : Form
    {
        public static string DefaultUsername = "";
        public static string DefaultMessage = "<please provide information for every file>";
        public string Username = "";
        public string Message = "";

        public ReportForm(string content)
        {
            InitializeComponent();
            txtUser.Text = DefaultUsername;
            txtMessage.Text = DefaultMessage;
            txtContent.Text = content;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            Username = txtUser.Text;
            Message = txtMessage.Text;
            DefaultUsername = Username;
            DefaultMessage = Message;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
