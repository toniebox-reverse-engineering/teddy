using System.Diagnostics;
using System.Windows.Forms;

namespace TeddyBench
{
    public partial class AboutDialog : Form
    {
        public AboutDialog()
        {
            InitializeComponent();

            lblVersion.Text = ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." + ThisAssembly.Git.SemVer.Patch;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://www.flaticon.com/de/kostenloses-icon/hund_2829818");
            Process.Start("https://www.flaticon.com/de/kostenloses-icon/fragen_1923795");

        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://t.me/toniebox_reverse_engineering");
        }
    }
}
