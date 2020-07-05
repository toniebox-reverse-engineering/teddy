using ScottPlot;
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
            formsPlot1.plt.Title("LF Antenna Plot (no relevance)");
            formsPlot1.plt.AxisBounds(46.0f, 600.0f, 0, 65.0f);
            formsPlot1.plt.Style(Style.Gray2);
            formsPlot1.Render();
            formsPlot1.Show();

            lblV125.Text = result.vLF125.ToString("0.00") + " V";
            lblV134.Text = result.vLF134.ToString("0.00") + " V";
            lblVHF.Text = result.vHF.ToString("0.00") + " V";
            lblOptimalFreq.Text = (result.GetPeakFrequency() / 1000.0f).ToString("0.00") + " kHz";
            lblVopt.Text = result.peakV.ToString("0.00") + " V";

            if(result.vHF > 33)
            {
                lblVHF.BackColor = Color.Green;
                lblVHF.Text += " (wow)";
            }
            else if (result.vHF > 25)
            {
                lblVHF.BackColor = Color.GreenYellow;
                lblVHF.Text += " (good)";
            }
            else if (result.vHF > 20)
            {
                lblVHF.BackColor = Color.Yellow;
                lblVHF.Text += " (weak)";
            }
            else if (result.vHF > 10)
            {
                lblVHF.BackColor = Color.Orange;
                lblVHF.Text += " (meh)";
            }
            else
            {
                lblVHF.BackColor = Color.Red;
                lblVHF.Text += " (bad)";
            }
        }
    }
}
