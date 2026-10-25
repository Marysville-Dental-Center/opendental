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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormInsHistSetup));
			this.butSave = new OpenDental.UI.Button();
			this.labelHeading = new System.Windows.Forms.Label();
			this.SuspendLayout();
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(306, 272);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 10;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// labelHeading
			//
			this.labelHeading.Location = new System.Drawing.Point(12, 9);
			this.labelHeading.Name = "labelHeading";
			this.labelHeading.Size = new System.Drawing.Size(379, 33);
			this.labelHeading.TabIndex = 4;
			this.labelHeading.Text = "Rows come from code groups marked Show In History.  Adding dates creates procedures with status of E" +
    "xistingOther.";
			//
			// FormInsHistSetup
			//
			this.ClientSize = new System.Drawing.Size(393, 308);
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
	}
}
