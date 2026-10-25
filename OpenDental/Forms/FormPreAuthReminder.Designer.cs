namespace OpenDental {
	partial class FormPreAuthReminder {
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
			this.gridMain = new OpenDental.UI.GridOD();
			this.butCreate = new OpenDental.UI.Button();
			this.butLater = new OpenDental.UI.Button();
			this.SuspendLayout();
			//
			// labelExplain
			//
			this.labelExplain.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelExplain.Location = new System.Drawing.Point(12, 9);
			this.labelExplain.Name = "labelExplain";
			this.labelExplain.Size = new System.Drawing.Size(600, 36);
			this.labelExplain.TabIndex = 0;
			this.labelExplain.Text = "labelExplain";
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 48);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(600, 200);
			this.gridMain.TabIndex = 1;
			this.gridMain.Title = "Procedures Needing Preauthorization";
			this.gridMain.TranslationName = "TablePreAuthReminder";
			//
			// butCreate
			//
			this.butCreate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butCreate.Location = new System.Drawing.Point(406, 258);
			this.butCreate.Name = "butCreate";
			this.butCreate.Size = new System.Drawing.Size(125, 24);
			this.butCreate.TabIndex = 2;
			this.butCreate.Text = "Create Preauth Now";
			this.butCreate.Click += new System.EventHandler(this.butCreate_Click);
			//
			// butLater
			//
			this.butLater.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butLater.Location = new System.Drawing.Point(537, 258);
			this.butLater.Name = "butLater";
			this.butLater.Size = new System.Drawing.Size(75, 24);
			this.butLater.TabIndex = 3;
			this.butLater.Text = "Later";
			this.butLater.Click += new System.EventHandler(this.butLater_Click);
			//
			// FormPreAuthReminder
			//
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(624, 294);
			this.Controls.Add(this.labelExplain);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.butCreate);
			this.Controls.Add(this.butLater);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormPreAuthReminder";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Preauthorization Reminder";
			this.Load += new System.EventHandler(this.FormPreAuthReminder_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label labelExplain;
		private OpenDental.UI.GridOD gridMain;
		private OpenDental.UI.Button butCreate;
		private OpenDental.UI.Button butLater;
	}
}
