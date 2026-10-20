namespace OpenDental {
	partial class FormXConnectValidation {
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
			this.labelHeader = new System.Windows.Forms.Label();
			this.gridMain = new OpenDental.UI.GridOD();
			this.butEditClaim = new OpenDental.UI.Button();
			this.butRevalidate = new OpenDental.UI.Button();
			this.butSendAnyway = new OpenDental.UI.Button();
			this.butClose = new OpenDental.UI.Button();
			this.SuspendLayout();
			//
			// labelHeader
			//
			this.labelHeader.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelHeader.Location = new System.Drawing.Point(12, 9);
			this.labelHeader.Name = "labelHeader";
			this.labelHeader.Size = new System.Drawing.Size(860, 34);
			this.labelHeader.TabIndex = 0;
			this.labelHeader.Text = "Claim";
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 46);
			this.gridMain.Name = "gridMain";
			this.gridMain.Size = new System.Drawing.Size(860, 380);
			this.gridMain.TabIndex = 1;
			this.gridMain.Title = "DentalXChange Validation";
			this.gridMain.TranslationName = "TableXConnectValidation";
			this.gridMain.CellDoubleClick += new OpenDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);
			//
			// butEditClaim
			//
			this.butEditClaim.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butEditClaim.Location = new System.Drawing.Point(12, 437);
			this.butEditClaim.Name = "butEditClaim";
			this.butEditClaim.Size = new System.Drawing.Size(85, 24);
			this.butEditClaim.TabIndex = 2;
			this.butEditClaim.Text = "&Edit Claim";
			this.butEditClaim.Click += new System.EventHandler(this.butEditClaim_Click);
			//
			// butRevalidate
			//
			this.butRevalidate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butRevalidate.Location = new System.Drawing.Point(103, 437);
			this.butRevalidate.Name = "butRevalidate";
			this.butRevalidate.Size = new System.Drawing.Size(85, 24);
			this.butRevalidate.TabIndex = 3;
			this.butRevalidate.Text = "&Re-validate";
			this.butRevalidate.Click += new System.EventHandler(this.butRevalidate_Click);
			//
			// butSendAnyway
			//
			this.butSendAnyway.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSendAnyway.Location = new System.Drawing.Point(691, 437);
			this.butSendAnyway.Name = "butSendAnyway";
			this.butSendAnyway.Size = new System.Drawing.Size(100, 24);
			this.butSendAnyway.TabIndex = 4;
			this.butSendAnyway.Text = "&Send Anyway";
			this.butSendAnyway.Visible = false;
			this.butSendAnyway.Click += new System.EventHandler(this.butSendAnyway_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(797, 437);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 5;
			this.butClose.Text = "&Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormXConnectValidation
			//
			this.ClientSize = new System.Drawing.Size(884, 473);
			this.Controls.Add(this.labelHeader);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.butEditClaim);
			this.Controls.Add(this.butRevalidate);
			this.Controls.Add(this.butSendAnyway);
			this.Controls.Add(this.butClose);
			this.Name = "FormXConnectValidation";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Claim Validation";
			this.Load += new System.EventHandler(this.FormXConnectValidation_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label labelHeader;
		private OpenDental.UI.GridOD gridMain;
		private OpenDental.UI.Button butEditClaim;
		private OpenDental.UI.Button butRevalidate;
		private OpenDental.UI.Button butSendAnyway;
		private OpenDental.UI.Button butClose;
	}
}
