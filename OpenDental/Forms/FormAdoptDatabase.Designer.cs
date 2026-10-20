namespace OpenDental {
	partial class FormAdoptDatabase {
		/// <summary>Required designer variable.</summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>Clean up any resources being used.</summary>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		private void InitializeComponent() {
			this.labelExplain = new System.Windows.Forms.Label();
			this.labelDatabase = new System.Windows.Forms.Label();
			this.labelStatus = new System.Windows.Forms.Label();
			this.checkRestoredCopy = new System.Windows.Forms.CheckBox();
			this.labelConfirmName = new System.Windows.Forms.Label();
			this.textConfirmName = new System.Windows.Forms.TextBox();
			this.textLog = new System.Windows.Forms.TextBox();
			this.butAdopt = new OpenDental.UI.Button();
			this.butClose = new OpenDental.UI.Button();
			this.SuspendLayout();
			//
			// labelExplain
			//
			this.labelExplain.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelExplain.Location = new System.Drawing.Point(12, 9);
			this.labelExplain.Name = "labelExplain";
			this.labelExplain.Size = new System.Drawing.Size(600, 104);
			this.labelExplain.TabIndex = 0;
			this.labelExplain.Text = "labelExplain";
			//
			// labelDatabase
			//
			this.labelDatabase.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelDatabase.Location = new System.Drawing.Point(12, 118);
			this.labelDatabase.Name = "labelDatabase";
			this.labelDatabase.Size = new System.Drawing.Size(600, 18);
			this.labelDatabase.TabIndex = 1;
			this.labelDatabase.Text = "labelDatabase";
			//
			// labelStatus
			//
			this.labelStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelStatus.Location = new System.Drawing.Point(12, 140);
			this.labelStatus.Name = "labelStatus";
			this.labelStatus.Size = new System.Drawing.Size(600, 36);
			this.labelStatus.TabIndex = 2;
			this.labelStatus.Text = "labelStatus";
			//
			// checkRestoredCopy
			//
			this.checkRestoredCopy.Location = new System.Drawing.Point(12, 180);
			this.checkRestoredCopy.Name = "checkRestoredCopy";
			this.checkRestoredCopy.Size = new System.Drawing.Size(600, 20);
			this.checkRestoredCopy.TabIndex = 3;
			this.checkRestoredCopy.Text = "This is a restored copy, not the live database, and I have a backup of it.";
			//
			// labelConfirmName
			//
			this.labelConfirmName.Location = new System.Drawing.Point(12, 206);
			this.labelConfirmName.Name = "labelConfirmName";
			this.labelConfirmName.Size = new System.Drawing.Size(250, 18);
			this.labelConfirmName.TabIndex = 4;
			this.labelConfirmName.Text = "Type the database name to confirm";
			this.labelConfirmName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// textConfirmName
			//
			this.textConfirmName.Location = new System.Drawing.Point(266, 206);
			this.textConfirmName.Name = "textConfirmName";
			this.textConfirmName.Size = new System.Drawing.Size(220, 20);
			this.textConfirmName.TabIndex = 5;
			//
			// textLog
			//
			this.textLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textLog.Location = new System.Drawing.Point(12, 236);
			this.textLog.Multiline = true;
			this.textLog.Name = "textLog";
			this.textLog.ReadOnly = true;
			this.textLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textLog.Size = new System.Drawing.Size(600, 160);
			this.textLog.TabIndex = 6;
			//
			// butAdopt
			//
			this.butAdopt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butAdopt.Location = new System.Drawing.Point(456, 406);
			this.butAdopt.Name = "butAdopt";
			this.butAdopt.Size = new System.Drawing.Size(75, 24);
			this.butAdopt.TabIndex = 7;
			this.butAdopt.Text = "Adopt";
			this.butAdopt.Click += new System.EventHandler(this.butAdopt_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(537, 406);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 8;
			this.butClose.Text = "Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormAdoptDatabase
			//
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(624, 441);
			this.Controls.Add(this.labelExplain);
			this.Controls.Add(this.labelDatabase);
			this.Controls.Add(this.labelStatus);
			this.Controls.Add(this.checkRestoredCopy);
			this.Controls.Add(this.labelConfirmName);
			this.Controls.Add(this.textConfirmName);
			this.Controls.Add(this.textLog);
			this.Controls.Add(this.butAdopt);
			this.Controls.Add(this.butClose);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormAdoptDatabase";
			this.Text = "Adopt Database";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormAdoptDatabase_FormClosing);
			this.Load += new System.EventHandler(this.FormAdoptDatabase_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelExplain;
		private System.Windows.Forms.Label labelDatabase;
		private System.Windows.Forms.Label labelStatus;
		private System.Windows.Forms.CheckBox checkRestoredCopy;
		private System.Windows.Forms.Label labelConfirmName;
		private System.Windows.Forms.TextBox textConfirmName;
		private System.Windows.Forms.TextBox textLog;
		private OpenDental.UI.Button butAdopt;
		private OpenDental.UI.Button butClose;
	}
}
