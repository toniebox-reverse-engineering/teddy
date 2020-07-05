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
    public partial class PlotAntennaForm : Form
    {
        public PlotAntennaForm(Proxmark3.MeasurementResult result)
        {
            InitializeComponent();

            formsPlot1.plt.PlotScatter(result.GetFrequencieskHz(), result.GetVoltages());
            formsPlot1.plt.XLabel("Frequency [kHz]");
            formsPlot1.plt.YLabel("Amplitude [V]");
            formsPlot1.plt.AxisBounds(46.0f, 600.0f, 0, 65.0f);
            formsPlot1.Render();
            formsPlot1.Show();

            lblV125.Text = result.vLF125.ToString("0.00") + " V";
            lblV134.Text = result.vLF134.ToString("0.00") + " V";
            lblOptimalFreq.Text = (result.GetPeakFrequency() / 1000.0f).ToString("0.00") + " kHz";
            lblVopt.Text = result.peakV.ToString("0.00") + " V";

            if(result.vLF125 > 32)
            {
                lblV125.BackColor = Color.Green;
            }
            else if (result.vLF125 > 25)
            {
                lblV125.BackColor = Color.GreenYellow;
            }
            else if (result.vLF125 > 20)
            {
                lblV125.BackColor = Color.Orange;
            }
            else
            {
                lblV125.BackColor = Color.Red;
            }
        }
    }
}
