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
    public partial class TagOperationDialog : Form
    {
        public TagOperationDialog(bool cancelEnabled = true)
        {
            InitializeComponent();
            btnCancel.Enabled = cancelEnabled;
        }
    }
}
