namespace OpenPmsDental {
	partial class FormSupplyAdjust {
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
			this.labelSupply = new System.Windows.Forms.Label();
			this.textSupply = new System.Windows.Forms.TextBox();
			this.labelLot = new System.Windows.Forms.Label();
			this.comboLot = new OpenPmsDental.UI.ComboBox();
			this.labelChange = new System.Windows.Forms.Label();
			this.textChange = new OpenPmsDental.ValidDouble();
			this.labelChangeNote = new System.Windows.Forms.Label();
			this.labelReason = new System.Windows.Forms.Label();
			this.textReason = new System.Windows.Forms.TextBox();
			this.butSave = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelSupply
			//
			this.labelSupply.Location = new System.Drawing.Point(12, 14);
			this.labelSupply.Name = "labelSupply";
			this.labelSupply.Size = new System.Drawing.Size(100, 18);
			this.labelSupply.TabIndex = 0;
			this.labelSupply.Text = "Supply";
			this.labelSupply.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textSupply
			//
			this.textSupply.Location = new System.Drawing.Point(115, 13);
			this.textSupply.Name = "textSupply";
			this.textSupply.ReadOnly = true;
			this.textSupply.Size = new System.Drawing.Size(300, 20);
			this.textSupply.TabIndex = 1;
			//
			// labelLot
			//
			this.labelLot.Location = new System.Drawing.Point(12, 41);
			this.labelLot.Name = "labelLot";
			this.labelLot.Size = new System.Drawing.Size(100, 18);
			this.labelLot.TabIndex = 2;
			this.labelLot.Text = "Lot";
			this.labelLot.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboLot
			//
			this.comboLot.Location = new System.Drawing.Point(115, 40);
			this.comboLot.Name = "comboLot";
			this.comboLot.Size = new System.Drawing.Size(300, 21);
			this.comboLot.TabIndex = 3;
			//
			// labelChange
			//
			this.labelChange.Location = new System.Drawing.Point(12, 68);
			this.labelChange.Name = "labelChange";
			this.labelChange.Size = new System.Drawing.Size(100, 18);
			this.labelChange.TabIndex = 4;
			this.labelChange.Text = "Change";
			this.labelChange.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textChange
			//
			this.textChange.Location = new System.Drawing.Point(115, 67);
			this.textChange.MaxVal = 1000000D;
			this.textChange.MinVal = -1000000D;
			this.textChange.Name = "textChange";
			this.textChange.Size = new System.Drawing.Size(70, 20);
			this.textChange.TabIndex = 5;
			//
			// labelChangeNote
			//
			this.labelChangeNote.Location = new System.Drawing.Point(190, 68);
			this.labelChangeNote.Name = "labelChangeNote";
			this.labelChangeNote.Size = new System.Drawing.Size(225, 18);
			this.labelChangeNote.TabIndex = 6;
			this.labelChangeNote.Text = "For example 5 to add, -2 to take away";
			this.labelChangeNote.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// labelReason
			//
			this.labelReason.Location = new System.Drawing.Point(12, 95);
			this.labelReason.Name = "labelReason";
			this.labelReason.Size = new System.Drawing.Size(100, 18);
			this.labelReason.TabIndex = 7;
			this.labelReason.Text = "Reason";
			this.labelReason.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textReason
			//
			this.textReason.Location = new System.Drawing.Point(115, 94);
			this.textReason.MaxLength = 255;
			this.textReason.Name = "textReason";
			this.textReason.Size = new System.Drawing.Size(300, 20);
			this.textReason.TabIndex = 8;
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(340, 130);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 9;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormSupplyAdjust
			//
			this.ClientSize = new System.Drawing.Size(427, 166);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.textReason);
			this.Controls.Add(this.labelReason);
			this.Controls.Add(this.labelChangeNote);
			this.Controls.Add(this.textChange);
			this.Controls.Add(this.labelChange);
			this.Controls.Add(this.comboLot);
			this.Controls.Add(this.labelLot);
			this.Controls.Add(this.textSupply);
			this.Controls.Add(this.labelSupply);
			this.Name = "FormSupplyAdjust";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Adjust Stock";
			this.Load += new System.EventHandler(this.FormSupplyAdjust_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelSupply;
		private System.Windows.Forms.TextBox textSupply;
		private System.Windows.Forms.Label labelLot;
		private UI.ComboBox comboLot;
		private System.Windows.Forms.Label labelChange;
		private ValidDouble textChange;
		private System.Windows.Forms.Label labelChangeNote;
		private System.Windows.Forms.Label labelReason;
		private System.Windows.Forms.TextBox textReason;
		private UI.Button butSave;
	}
}
