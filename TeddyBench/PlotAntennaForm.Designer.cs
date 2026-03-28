namespace TeddyBench
{
    partial class PlotAntennaForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PlotAntennaForm));
            this.antennaPlot = new ScottPlot.FormsPlot();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.lblVopt = new System.Windows.Forms.Label();
            this.lblV134 = new System.Windows.Forms.Label();
            this.lblOptimalFreq = new System.Windows.Forms.Label();
            this.lblVHF = new System.Windows.Forms.Label();
            this.lblV125 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // formsPlot1
            // 
            this.antennaPlot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.antennaPlot.Location = new System.Drawing.Point(0, 0);
            this.antennaPlot.Name = "formsPlot1";
            this.antennaPlot.Size = new System.Drawing.Size(606, 367);
            this.antennaPlot.TabIndex = 0;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.label7);
            this.splitContainer1.Panel1.Controls.Add(this.label6);
            this.splitContainer1.Panel1.Controls.Add(this.label8);
            this.splitContainer1.Panel1.Controls.Add(this.label5);
            this.splitContainer1.Panel1.Controls.Add(this.label4);
            this.splitContainer1.Panel1.Controls.Add(this.label3);
            this.splitContainer1.Panel1.Controls.Add(this.lblVopt);
            this.splitContainer1.Panel1.Controls.Add(this.lblV134);
            this.splitContainer1.Panel1.Controls.Add(this.lblOptimalFreq);
            this.splitContainer1.Panel1.Controls.Add(this.lblVHF);
            this.splitContainer1.Panel1.Controls.Add(this.lblV125);
            this.splitContainer1.Panel1.Controls.Add(this.label2);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.antennaPlot);
            this.splitContainer1.Size = new System.Drawing.Size(606, 450);
            this.splitContainer1.SplitterDistance = 79;
            this.splitContainer1.TabIndex = 1;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(49, 13);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(113, 13);
            this.label5.TabIndex = 3;
            this.label5.Text = "Voltage at 13.56 MHz:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(269, 58);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(88, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "Antenna voltage:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(269, 42);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(113, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Optimal LF Frequency:";
            // 
            // lblVopt
            // 
            this.lblVopt.AutoSize = true;
            this.lblVopt.Location = new System.Drawing.Point(401, 58);
            this.lblVopt.Name = "lblVopt";
            this.lblVopt.Size = new System.Drawing.Size(14, 13);
            this.lblVopt.TabIndex = 1;
            this.lblVopt.Text = "V";
            // 
            // lblV134
            // 
            this.lblV134.AutoSize = true;
            this.lblV134.Location = new System.Drawing.Point(165, 58);
            this.lblV134.Name = "lblV134";
            this.lblV134.Size = new System.Drawing.Size(14, 13);
            this.lblV134.TabIndex = 1;
            this.lblV134.Text = "V";
            // 
            // lblOptimalFreq
            // 
            this.lblOptimalFreq.AutoSize = true;
            this.lblOptimalFreq.Location = new System.Drawing.Point(401, 42);
            this.lblOptimalFreq.Name = "lblOptimalFreq";
            this.lblOptimalFreq.Size = new System.Drawing.Size(26, 13);
            this.lblOptimalFreq.TabIndex = 1;
            this.lblOptimalFreq.Text = "kHz";
            // 
            // lblVHF
            // 
            this.lblVHF.AutoSize = true;
            this.lblVHF.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblVHF.Location = new System.Drawing.Point(165, 13);
            this.lblVHF.Name = "lblVHF";
            this.lblVHF.Size = new System.Drawing.Size(14, 13);
            this.lblVHF.TabIndex = 1;
            this.lblVHF.Text = "V";
            // 
            // lblV125
            // 
            this.lblV125.AutoSize = true;
            this.lblV125.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblV125.Location = new System.Drawing.Point(165, 42);
            this.lblV125.Name = "lblV125";
            this.lblV125.Size = new System.Drawing.Size(14, 13);
            this.lblV125.TabIndex = 1;
            this.lblV125.Text = "V";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(49, 58);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(101, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "Voltage at 134 kHz:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(49, 42);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(101, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Voltage at 125 kHz:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(20, 13);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(24, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "HF:";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(20, 42);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(22, 13);
            this.label7.TabIndex = 4;
            this.label7.Text = "LF:";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(269, 13);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(256, 13);
            this.label8.TabIndex = 3;
            this.label8.Text = "TeddyBench uses the HF (13.56 MHz) antenna only.";
            // 
            // PlotAntennaForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(606, 450);
            this.Controls.Add(this.splitContainer1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "PlotAntennaForm";
            this.Text = "Proxmark 3 Antenna Plot";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private ScottPlot.FormsPlot antennaPlot;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblVopt;
        private System.Windows.Forms.Label lblV134;
        private System.Windows.Forms.Label lblOptimalFreq;
        private System.Windows.Forms.Label lblV125;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label lblVHF;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label8;
    }
}