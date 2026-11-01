namespace OpenDental {
	partial class FormProcCodesOther {
		///<summary>Required designer variable.</summary>
		private System.ComponentModel.IContainer components = null;

		///<summary>Clean up any resources being used.</summary>
		protected override void Dispose(bool disposing) {
			if(disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		///<summary>Required method for Designer support - do not modify the contents of this method with the code editor.</summary>
		private void InitializeComponent() {
			this.labelInfo = new System.Windows.Forms.Label();
			this.labelCodeSystem = new System.Windows.Forms.Label();
			this.listCodeSystem = new OpenDental.UI.ListBox();
			this.groupAdvanced = new OpenDental.UI.GroupBox();
			this.checkUpdateDescriptions = new OpenDental.UI.CheckBox();
			this.checkUpdateAbbreviations = new OpenDental.UI.CheckBox();
			this.checkUpdateTreatArea = new OpenDental.UI.CheckBox();
			this.checkUpdatePaintType = new OpenDental.UI.CheckBox();
			this.labelAdvanced = new System.Windows.Forms.Label();
			this.labelFile = new System.Windows.Forms.Label();
			this.textFile = new System.Windows.Forms.TextBox();
			this.butBrowse = new OpenDental.UI.Button();
			this.butOK = new OpenDental.UI.Button();
			this.groupAdvanced.SuspendLayout();
			this.SuspendLayout();
			//
			// labelInfo
			//
			this.labelInfo.Location = new System.Drawing.Point(12, 9);
			this.labelInfo.Name = "labelInfo";
			this.labelInfo.Size = new System.Drawing.Size(496, 58);
			this.labelInfo.TabIndex = 0;
			this.labelInfo.Text = "Imports another Canadian code system from a code file that you supply: comma or tab separated text with the columns code, description, abbreviation, treatment area, paint type and time units (a header row is optional). Each code system gets its own procedure code category. Codes that already exist are skipped unless an Advanced Option is checked. Codes are never deleted.";
			//
			// labelCodeSystem
			//
			this.labelCodeSystem.Location = new System.Drawing.Point(12, 72);
			this.labelCodeSystem.Name = "labelCodeSystem";
			this.labelCodeSystem.Size = new System.Drawing.Size(240, 18);
			this.labelCodeSystem.TabIndex = 1;
			this.labelCodeSystem.Text = "Code System";
			this.labelCodeSystem.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// listCodeSystem
			//
			this.listCodeSystem.Location = new System.Drawing.Point(12, 92);
			this.listCodeSystem.Name = "listCodeSystem";
			this.listCodeSystem.Size = new System.Drawing.Size(240, 82);
			this.listCodeSystem.TabIndex = 2;
			//
			// groupAdvanced
			//
			this.groupAdvanced.Controls.Add(this.checkUpdateDescriptions);
			this.groupAdvanced.Controls.Add(this.checkUpdateAbbreviations);
			this.groupAdvanced.Controls.Add(this.checkUpdateTreatArea);
			this.groupAdvanced.Controls.Add(this.checkUpdatePaintType);
			this.groupAdvanced.Location = new System.Drawing.Point(270, 72);
			this.groupAdvanced.Name = "groupAdvanced";
			this.groupAdvanced.Size = new System.Drawing.Size(238, 102);
			this.groupAdvanced.TabIndex = 3;
			this.groupAdvanced.Text = "Advanced Options";
			//
			// checkUpdateDescriptions
			//
			this.checkUpdateDescriptions.Location = new System.Drawing.Point(10, 20);
			this.checkUpdateDescriptions.Name = "checkUpdateDescriptions";
			this.checkUpdateDescriptions.Size = new System.Drawing.Size(220, 18);
			this.checkUpdateDescriptions.TabIndex = 0;
			this.checkUpdateDescriptions.Text = "Update descriptions";
			//
			// checkUpdateAbbreviations
			//
			this.checkUpdateAbbreviations.Location = new System.Drawing.Point(10, 40);
			this.checkUpdateAbbreviations.Name = "checkUpdateAbbreviations";
			this.checkUpdateAbbreviations.Size = new System.Drawing.Size(220, 18);
			this.checkUpdateAbbreviations.TabIndex = 1;
			this.checkUpdateAbbreviations.Text = "Update abbreviations";
			//
			// checkUpdateTreatArea
			//
			this.checkUpdateTreatArea.Location = new System.Drawing.Point(10, 60);
			this.checkUpdateTreatArea.Name = "checkUpdateTreatArea";
			this.checkUpdateTreatArea.Size = new System.Drawing.Size(220, 18);
			this.checkUpdateTreatArea.TabIndex = 2;
			this.checkUpdateTreatArea.Text = "Update treatment area";
			//
			// checkUpdatePaintType
			//
			this.checkUpdatePaintType.Location = new System.Drawing.Point(10, 80);
			this.checkUpdatePaintType.Name = "checkUpdatePaintType";
			this.checkUpdatePaintType.Size = new System.Drawing.Size(220, 18);
			this.checkUpdatePaintType.TabIndex = 3;
			this.checkUpdatePaintType.Text = "Update paint type";
			//
			// labelAdvanced
			//
			this.labelAdvanced.Location = new System.Drawing.Point(270, 177);
			this.labelAdvanced.Name = "labelAdvanced";
			this.labelAdvanced.Size = new System.Drawing.Size(238, 44);
			this.labelAdvanced.TabIndex = 4;
			this.labelAdvanced.Text = "Advanced Options change the codes of the file that already exist. They ask you to type a confirmation phrase first.";
			//
			// labelFile
			//
			this.labelFile.Location = new System.Drawing.Point(12, 224);
			this.labelFile.Name = "labelFile";
			this.labelFile.Size = new System.Drawing.Size(240, 18);
			this.labelFile.TabIndex = 5;
			this.labelFile.Text = "Code File";
			this.labelFile.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// textFile
			//
			this.textFile.Location = new System.Drawing.Point(12, 244);
			this.textFile.Name = "textFile";
			this.textFile.Size = new System.Drawing.Size(415, 20);
			this.textFile.TabIndex = 6;
			//
			// butBrowse
			//
			this.butBrowse.Location = new System.Drawing.Point(433, 242);
			this.butBrowse.Name = "butBrowse";
			this.butBrowse.Size = new System.Drawing.Size(75, 24);
			this.butBrowse.TabIndex = 7;
			this.butBrowse.Text = "&Browse";
			this.butBrowse.Click += new System.EventHandler(this.butBrowse_Click);
			//
			// butOK
			//
			this.butOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOK.Location = new System.Drawing.Point(433, 290);
			this.butOK.Name = "butOK";
			this.butOK.Size = new System.Drawing.Size(75, 24);
			this.butOK.TabIndex = 8;
			this.butOK.Text = "&OK";
			this.butOK.Click += new System.EventHandler(this.butOK_Click);
			//
			// FormProcCodesOther
			//
			this.ClientSize = new System.Drawing.Size(520, 326);
			this.Controls.Add(this.labelInfo);
			this.Controls.Add(this.labelCodeSystem);
			this.Controls.Add(this.listCodeSystem);
			this.Controls.Add(this.groupAdvanced);
			this.Controls.Add(this.labelAdvanced);
			this.Controls.Add(this.labelFile);
			this.Controls.Add(this.textFile);
			this.Controls.Add(this.butBrowse);
			this.Controls.Add(this.butOK);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormProcCodesOther";
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Other Procedure Codes";
			this.Load += new System.EventHandler(this.FormProcCodesOther_Load);
			this.groupAdvanced.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelInfo;
		private System.Windows.Forms.Label labelCodeSystem;
		private OpenDental.UI.ListBox listCodeSystem;
		private OpenDental.UI.GroupBox groupAdvanced;
		private OpenDental.UI.CheckBox checkUpdateDescriptions;
		private OpenDental.UI.CheckBox checkUpdateAbbreviations;
		private OpenDental.UI.CheckBox checkUpdateTreatArea;
		private OpenDental.UI.CheckBox checkUpdatePaintType;
		private System.Windows.Forms.Label labelAdvanced;
		private System.Windows.Forms.Label labelFile;
		private System.Windows.Forms.TextBox textFile;
		private OpenDental.UI.Button butBrowse;
		private OpenDental.UI.Button butOK;
	}
}
