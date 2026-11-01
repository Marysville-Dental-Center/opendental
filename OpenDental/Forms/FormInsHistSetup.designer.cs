namespace OpenDental{
	partial class FormInsHistSetup {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormInsHistSetup));
			this.butSave = new OpenDental.UI.Button();
			this.labelHeading = new System.Windows.Forms.Label();
			this.labelMostRecent = new System.Windows.Forms.Label();
			this.labelPrevious = new System.Windows.Forms.Label();
			this.toolTip = new System.Windows.Forms.ToolTip(this.components);
			this.SuspendLayout();
			//
			// butSave
			//
			this.butSave.Location = new System.Drawing.Point(441, 312);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 100;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// labelHeading
			//
			this.labelHeading.Location = new System.Drawing.Point(12, 9);
			this.labelHeading.Name = "labelHeading";
			this.labelHeading.Size = new System.Drawing.Size(504, 33);
			this.labelHeading.TabIndex = 4;
			this.labelHeading.Text = "Set procedures for each category in Preferences.  Adding a date creates an Existing Other procedure." +
    "  Dates from completed procedures are edited in the Chart.";
			//
			// labelMostRecent
			//
			this.labelMostRecent.Location = new System.Drawing.Point(146, 45);
			this.labelMostRecent.Name = "labelMostRecent";
			this.labelMostRecent.Size = new System.Drawing.Size(180, 16);
			this.labelMostRecent.TabIndex = 5;
			this.labelMostRecent.Text = "Most Recent";
			this.labelMostRecent.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// labelPrevious
			//
			this.labelPrevious.Location = new System.Drawing.Point(336, 45);
			this.labelPrevious.Name = "labelPrevious";
			this.labelPrevious.Size = new System.Drawing.Size(180, 16);
			this.labelPrevious.TabIndex = 6;
			this.labelPrevious.Text = "Previous";
			this.labelPrevious.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// FormInsHistSetup
			//
			this.ClientSize = new System.Drawing.Size(530, 348);
			this.Controls.Add(this.labelPrevious);
			this.Controls.Add(this.labelMostRecent);
			this.Controls.Add(this.labelHeading);
			this.Controls.Add(this.butSave);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormInsHistSetup";
			this.Text = "Insurance History";
			this.Load += new System.EventHandler(this.FormInsHistSetup_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private OpenDental.UI.Button butSave;
		private System.Windows.Forms.Label labelHeading;
		private System.Windows.Forms.Label labelMostRecent;
		private System.Windows.Forms.Label labelPrevious;
		private System.Windows.Forms.ToolTip toolTip;
	}
}
