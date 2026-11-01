namespace OpenPmsDental {
	partial class FormFileMonitor {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			this.components = new System.ComponentModel.Container();
			this.gridFeed = new OpenPmsDental.UI.GridOD();
			this.butStart = new OpenPmsDental.UI.Button();
			this.butClear = new OpenPmsDental.UI.Button();
			this.labelStatus = new System.Windows.Forms.Label();
			this.timerProcessQueue = new System.Windows.Forms.Timer(this.components);
			this.SuspendLayout();
			//
			// gridFeed
			//
			this.gridFeed.AllowSortingByColumn = true;
			this.gridFeed.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridFeed.Location = new System.Drawing.Point(12, 12);
			this.gridFeed.Name = "gridFeed";
			this.gridFeed.Size = new System.Drawing.Size(916, 488);
			this.gridFeed.TabIndex = 0;
			this.gridFeed.Title = "File Feed";
			this.gridFeed.TranslationName = "TableFileFeed";
			this.gridFeed.WrapText = false;
			//
			// butStart
			//
			this.butStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butStart.Location = new System.Drawing.Point(941, 12);
			this.butStart.Name = "butStart";
			this.butStart.Size = new System.Drawing.Size(75, 24);
			this.butStart.TabIndex = 1;
			this.butStart.Text = "Start";
			this.butStart.Click += new System.EventHandler(this.butStart_Click);
			//
			// butClear
			//
			this.butClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.butClear.Location = new System.Drawing.Point(941, 46);
			this.butClear.Name = "butClear";
			this.butClear.Size = new System.Drawing.Size(75, 24);
			this.butClear.TabIndex = 2;
			this.butClear.Text = "Clear";
			this.butClear.Click += new System.EventHandler(this.butClear_Click);
			//
			// labelStatus
			//
			this.labelStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.labelStatus.Location = new System.Drawing.Point(934, 80);
			this.labelStatus.Name = "labelStatus";
			this.labelStatus.Size = new System.Drawing.Size(90, 160);
			this.labelStatus.TabIndex = 3;
			this.labelStatus.Text = "Stopped";
			//
			// timerProcessQueue
			//
			this.timerProcessQueue.Interval = 250;
			this.timerProcessQueue.Tick += new System.EventHandler(this.timerProcessQueue_Tick);
			//
			// FormFileMonitor
			//
			this.ClientSize = new System.Drawing.Size(1028, 512);
			this.Controls.Add(this.labelStatus);
			this.Controls.Add(this.butClear);
			this.Controls.Add(this.butStart);
			this.Controls.Add(this.gridFeed);
			this.Name = "FormFileMonitor";
			this.Text = "File Monitor";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormFileMonitor_FormClosing);
			this.Load += new System.EventHandler(this.FormFileMonitor_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private UI.GridOD gridFeed;
		private UI.Button butStart;
		private UI.Button butClear;
		private System.Windows.Forms.Label labelStatus;
		private System.Windows.Forms.Timer timerProcessQueue;
	}
}
