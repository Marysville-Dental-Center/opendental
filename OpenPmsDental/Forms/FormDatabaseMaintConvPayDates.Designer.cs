namespace OpenPmsDental {
	partial class FormDatabaseMaintConvPayDates {
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
			this.labelDateConversion = new System.Windows.Forms.Label();
			this.textDateConversion = new OpenPmsDental.ValidDate();
			this.butFind = new OpenPmsDental.UI.Button();
			this.labelSummary = new System.Windows.Forms.Label();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.labelCannotFix = new System.Windows.Forms.Label();
			this.butFix = new OpenPmsDental.UI.Button();
			this.butClose = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelExplain
			//
			this.labelExplain.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelExplain.Location = new System.Drawing.Point(12, 9);
			this.labelExplain.Name = "labelExplain";
			this.labelExplain.Size = new System.Drawing.Size(876, 60);
			this.labelExplain.TabIndex = 0;
			this.labelExplain.Text = "labelExplain";
			//
			// labelDateConversion
			//
			this.labelDateConversion.Location = new System.Drawing.Point(12, 76);
			this.labelDateConversion.Name = "labelDateConversion";
			this.labelDateConversion.Size = new System.Drawing.Size(110, 20);
			this.labelDateConversion.TabIndex = 1;
			this.labelDateConversion.Text = "Conversion date";
			this.labelDateConversion.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDateConversion
			//
			this.textDateConversion.Location = new System.Drawing.Point(126, 76);
			this.textDateConversion.Name = "textDateConversion";
			this.textDateConversion.Size = new System.Drawing.Size(90, 20);
			this.textDateConversion.TabIndex = 2;
			//
			// butFind
			//
			this.butFind.Location = new System.Drawing.Point(226, 74);
			this.butFind.Name = "butFind";
			this.butFind.Size = new System.Drawing.Size(75, 24);
			this.butFind.TabIndex = 3;
			this.butFind.Text = "Find";
			this.butFind.Click += new System.EventHandler(this.butFind_Click);
			//
			// labelSummary
			//
			this.labelSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelSummary.Location = new System.Drawing.Point(12, 104);
			this.labelSummary.Name = "labelSummary";
			this.labelSummary.Size = new System.Drawing.Size(876, 20);
			this.labelSummary.TabIndex = 4;
			this.labelSummary.Text = "Enter the conversion date and click Find.";
			this.labelSummary.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 128);
			this.gridMain.Name = "gridMain";
			this.gridMain.SelectionMode = OpenPmsDental.UI.GridSelectionMode.MultiExtended;
			this.gridMain.Size = new System.Drawing.Size(876, 464);
			this.gridMain.TabIndex = 5;
			this.gridMain.Title = "Income Transfers (selected rows are dated on the conversion date)";
			this.gridMain.TranslationName = "TableDbmConvPayDates";
			//
			// labelCannotFix
			//
			this.labelCannotFix.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelCannotFix.ForeColor = System.Drawing.Color.Firebrick;
			this.labelCannotFix.Location = new System.Drawing.Point(12, 604);
			this.labelCannotFix.Name = "labelCannotFix";
			this.labelCannotFix.Size = new System.Drawing.Size(560, 20);
			this.labelCannotFix.TabIndex = 6;
			this.labelCannotFix.Text = "labelCannotFix";
			this.labelCannotFix.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// butFix
			//
			this.butFix.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butFix.Location = new System.Drawing.Point(707, 602);
			this.butFix.Name = "butFix";
			this.butFix.Size = new System.Drawing.Size(100, 24);
			this.butFix.TabIndex = 7;
			this.butFix.Text = "Change Dates";
			this.butFix.Click += new System.EventHandler(this.butFix_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(813, 602);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 8;
			this.butClose.Text = "Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormDatabaseMaintConvPayDates
			//
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(900, 638);
			this.Controls.Add(this.labelExplain);
			this.Controls.Add(this.labelDateConversion);
			this.Controls.Add(this.textDateConversion);
			this.Controls.Add(this.butFind);
			this.Controls.Add(this.labelSummary);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.labelCannotFix);
			this.Controls.Add(this.butFix);
			this.Controls.Add(this.butClose);
			this.MinimizeBox = false;
			this.Name = "FormDatabaseMaintConvPayDates";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Conversion Payment Dates";
			this.Load += new System.EventHandler(this.FormDatabaseMaintConvPayDates_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelExplain;
		private System.Windows.Forms.Label labelDateConversion;
		private OpenPmsDental.ValidDate textDateConversion;
		private OpenPmsDental.UI.Button butFind;
		private System.Windows.Forms.Label labelSummary;
		private OpenPmsDental.UI.GridOD gridMain;
		private System.Windows.Forms.Label labelCannotFix;
		private OpenPmsDental.UI.Button butFix;
		private OpenPmsDental.UI.Button butClose;
	}
}
