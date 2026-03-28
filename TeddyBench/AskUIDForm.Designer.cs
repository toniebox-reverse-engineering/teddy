namespace TeddyBench
{
    partial class AskUIDForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AskUIDForm));
            this.label1 = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.Cancel = new System.Windows.Forms.Button();
            this.txtUid = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 7);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(268, 26);
            this.label1.TabIndex = 0;
            this.label1.Text = "You are about to add a file to the box.\r\nPlease enter the UID of the tag you want" +
    " to assign it to.";
            // 
            // btnOk
            // 
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(66, 64);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 2;
            this.btnOk.Text = "Ok";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // Cancel
            // 
            this.Cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Cancel.Location = new System.Drawing.Point(162, 64);
            this.Cancel.Name = "Cancel";
            this.Cancel.Size = new System.Drawing.Size(75, 23);
            this.Cancel.TabIndex = 3;
            this.Cancel.Text = "Cancel";
            this.Cancel.UseVisualStyleBackColor = true;
            this.Cancel.Click += new System.EventHandler(this.Cancel_Click);
            // 
            // txtUid
            // 
            this.txtUid.Font = new System.Drawing.Font("Courier New", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtUid.Location = new System.Drawing.Point(66, 38);
            this.txtUid.MaxLength = 16;
            this.txtUid.Name = "txtUid";
            this.txtUid.Size = new System.Drawing.Size(171, 23);
            this.txtUid.TabIndex = 1;
            this.txtUid.Text = "E00403xxxxxxxxxx";
            this.txtUid.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txtUid.TextChanged += new System.EventHandler(this.txtUid_TextChanged);
            this.txtUid.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtUid_KeyDown);
            // 
            // AskUIDForm
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.Cancel;
            this.ClientSize = new System.Drawing.Size(296, 93);
            this.Controls.Add(this.txtUid);
            this.Controls.Add(this.Cancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AskUIDForm";
            this.Text = "Enter tag UID";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button Cancel;
        private System.Windows.Forms.TextBox txtUid;
    }
}