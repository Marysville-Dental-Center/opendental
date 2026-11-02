namespace OpenPmsDental {
	partial class FormInventorySetup {
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
			this.checkEnabled = new OpenPmsDental.UI.CheckBox();
			this.labelEnabled = new System.Windows.Forms.Label();
			this.labelExpiryDays = new System.Windows.Forms.Label();
			this.textExpiryDays = new OpenPmsDental.ValidNum();
			this.labelExpiryNote = new System.Windows.Forms.Label();
			this.butSave = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// checkEnabled
			//
			this.checkEnabled.Location = new System.Drawing.Point(20, 14);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(420, 18);
			this.checkEnabled.TabIndex = 0;
			this.checkEnabled.Text = "Inventory tracking is on";
			//
			// labelEnabled
			//
			this.labelEnabled.Location = new System.Drawing.Point(38, 34);
			this.labelEnabled.Name = "labelEnabled";
			this.labelEnabled.Size = new System.Drawing.Size(400, 58);
			this.labelEnabled.TabIndex = 1;
			this.labelEnabled.Text = "When a procedure whose code has default materials is completed, the Materials used window opens, and what is confirmed" +
    " there is deducted from stock. Low-stock and expiring alerts are raised. While it is off, nothing opens and nothing is deducted.";
			//
			// labelExpiryDays
			//
			this.labelExpiryDays.Location = new System.Drawing.Point(20, 102);
			this.labelExpiryDays.Name = "labelExpiryDays";
			this.labelExpiryDays.Size = new System.Drawing.Size(220, 18);
			this.labelExpiryDays.TabIndex = 2;
			this.labelExpiryDays.Text = "Expiring alert, days before the expiry date";
			this.labelExpiryDays.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textExpiryDays
			//
			this.textExpiryDays.Location = new System.Drawing.Point(244, 101);
			this.textExpiryDays.MaxVal = 3650;
			this.textExpiryDays.MinVal = 0;
			this.textExpiryDays.Name = "textExpiryDays";
			this.textExpiryDays.Size = new System.Drawing.Size(50, 20);
			this.textExpiryDays.TabIndex = 3;
			//
			// labelExpiryNote
			//
			this.labelExpiryNote.Location = new System.Drawing.Point(300, 102);
			this.labelExpiryNote.Name = "labelExpiryNote";
			this.labelExpiryNote.Size = new System.Drawing.Size(140, 18);
			this.labelExpiryNote.TabIndex = 4;
			this.labelExpiryNote.Text = "0 turns it off";
			this.labelExpiryNote.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(365, 142);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 5;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormInventorySetup
			//
			this.ClientSize = new System.Drawing.Size(452, 178);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.labelExpiryNote);
			this.Controls.Add(this.textExpiryDays);
			this.Controls.Add(this.labelExpiryDays);
			this.Controls.Add(this.labelEnabled);
			this.Controls.Add(this.checkEnabled);
			this.Name = "FormInventorySetup";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Inventory Setup";
			this.Load += new System.EventHandler(this.FormInventorySetup_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private UI.CheckBox checkEnabled;
		private System.Windows.Forms.Label labelEnabled;
		private System.Windows.Forms.Label labelExpiryDays;
		private ValidNum textExpiryDays;
		private System.Windows.Forms.Label labelExpiryNote;
		private UI.Button butSave;
	}
}
