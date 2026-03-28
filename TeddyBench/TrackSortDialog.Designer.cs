namespace TeddyBench
{
    partial class TrackSortDialog
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
            System.Windows.Forms.ListViewItem listViewItem1 = new System.Windows.Forms.ListViewItem(new string[] {
            "1",
            ""}, -1);
            System.Windows.Forms.ListViewItem listViewItem2 = new System.Windows.Forms.ListViewItem(new string[] {
            "2",
            ""}, -1);
            System.Windows.Forms.ListViewItem listViewItem3 = new System.Windows.Forms.ListViewItem(new string[] {
            "3",
            ""}, -1);
            System.Windows.Forms.ListViewItem listViewItem4 = new System.Windows.Forms.ListViewItem(new string[] {
            "4",
            ""}, -1);
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TrackSortDialog));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnEncode = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.btnDown = new System.Windows.Forms.Button();
            this.btnUp = new System.Windows.Forms.Button();
            this.lstTracks = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCancel.Location = new System.Drawing.Point(733, 0);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 26);
            this.btnCancel.TabIndex = 0;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnEncode
            // 
            this.btnEncode.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnEncode.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnEncode.Location = new System.Drawing.Point(658, 0);
            this.btnEncode.Name = "btnEncode";
            this.btnEncode.Size = new System.Drawing.Size(75, 26);
            this.btnEncode.TabIndex = 1;
            this.btnEncode.Text = "Encode";
            this.btnEncode.UseVisualStyleBackColor = true;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.btnEncode);
            this.splitContainer1.Panel2.Controls.Add(this.btnCancel);
            this.splitContainer1.Size = new System.Drawing.Size(808, 401);
            this.splitContainer1.SplitterDistance = 371;
            this.splitContainer1.TabIndex = 2;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.btnDown);
            this.splitContainer2.Panel1.Controls.Add(this.btnUp);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.lstTracks);
            this.splitContainer2.Size = new System.Drawing.Size(808, 371);
            this.splitContainer2.SplitterDistance = 85;
            this.splitContainer2.TabIndex = 1;
            // 
            // btnDown
            // 
            this.btnDown.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnDown.Location = new System.Drawing.Point(0, 23);
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(85, 23);
            this.btnDown.TabIndex = 0;
            this.btnDown.Text = "Down";
            this.btnDown.UseVisualStyleBackColor = true;
            this.btnDown.Click += new System.EventHandler(this.btnDown_Click);
            // 
            // btnUp
            // 
            this.btnUp.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnUp.Location = new System.Drawing.Point(0, 0);
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(85, 23);
            this.btnUp.TabIndex = 0;
            this.btnUp.Text = "Up";
            this.btnUp.UseVisualStyleBackColor = true;
            this.btnUp.Click += new System.EventHandler(this.btnUp_Click);
            // 
            // lstTracks
            // 
            this.lstTracks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader3});
            this.lstTracks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstTracks.FullRowSelect = true;
            this.lstTracks.HideSelection = false;
            this.lstTracks.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            listViewItem1,
            listViewItem2,
            listViewItem3,
            listViewItem4});
            this.lstTracks.Location = new System.Drawing.Point(0, 0);
            this.lstTracks.Name = "lstTracks";
            this.lstTracks.Size = new System.Drawing.Size(719, 371);
            this.lstTracks.TabIndex = 0;
            this.lstTracks.UseCompatibleStateImageBehavior = false;
            this.lstTracks.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "#";
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Filename";
            this.columnHeader2.Width = 431;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "ID3";
            this.columnHeader3.Width = 219;
            // 
            // TrackSortDialog
            // 
            this.AcceptButton = this.btnEncode;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(808, 401);
            this.Controls.Add(this.splitContainer1);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "TrackSortDialog";
            this.Text = "Sort your tracks";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnEncode;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Button btnUp;
        private System.Windows.Forms.Button btnDown;
        private System.Windows.Forms.ListView lstTracks;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ColumnHeader columnHeader3;
    }
}