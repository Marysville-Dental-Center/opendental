namespace OpenDental {
	partial class FormDatabaseMaintInvalidChars {
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
			this.gridColumns = new OpenDental.UI.GridOD();
			this.gridPreview = new OpenDental.UI.GridOD();
			this.labelSummary = new System.Windows.Forms.Label();
			this.labelCannotFix = new System.Windows.Forms.Label();
			this.butScan = new OpenDental.UI.Button();
			this.butFix = new OpenDental.UI.Button();
			this.butClose = new OpenDental.UI.Button();
			this.SuspendLayout();
			//
			// labelExplain
			//
			this.labelExplain.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelExplain.Location = new System.Drawing.Point(12, 9);
			this.labelExplain.Name = "labelExplain";
			this.labelExplain.Size = new System.Drawing.Size(876, 48);
			this.labelExplain.TabIndex = 0;
			this.labelExplain.Text = "labelExplain";
			//
			// gridColumns
			//
			this.gridColumns.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridColumns.Location = new System.Drawing.Point(12, 62);
			this.gridColumns.Name = "gridColumns";
			this.gridColumns.SelectionMode = OpenDental.UI.GridSelectionMode.MultiExtended;
			this.gridColumns.Size = new System.Drawing.Size(876, 230);
			this.gridColumns.TabIndex = 1;
			this.gridColumns.Title = "Columns (selected rows are scanned)";
			this.gridColumns.TranslationName = "TableDbmInvalidCharsColumns";
			//
			// gridPreview
			//
			this.gridPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridPreview.Location = new System.Drawing.Point(12, 324);
			this.gridPreview.Name = "gridPreview";
			this.gridPreview.SelectionMode = OpenDental.UI.GridSelectionMode.OneRow;
			this.gridPreview.Size = new System.Drawing.Size(876, 268);
			this.gridPreview.TabIndex = 3;
			this.gridPreview.Title = "First 50 affected values (invalid characters shown as [U+XXXX])";
			this.gridPreview.TranslationName = "TableDbmInvalidCharsPreview";
			//
			// labelSummary
			//
			this.labelSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelSummary.Location = new System.Drawing.Point(12, 298);
			this.labelSummary.Name = "labelSummary";
			this.labelSummary.Size = new System.Drawing.Size(876, 20);
			this.labelSummary.TabIndex = 2;
			this.labelSummary.Text = "Click Scan to count the invalid characters in the selected columns.";
			this.labelSummary.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// labelCannotFix
			//
			this.labelCannotFix.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelCannotFix.ForeColor = System.Drawing.Color.Firebrick;
			this.labelCannotFix.Location = new System.Drawing.Point(12, 604);
			this.labelCannotFix.Name = "labelCannotFix";
			this.labelCannotFix.Size = new System.Drawing.Size(560, 20);
			this.labelCannotFix.TabIndex = 4;
			this.labelCannotFix.Text = "labelCannotFix";
			this.labelCannotFix.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// butScan
			//
			this.butScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butScan.Location = new System.Drawing.Point(651, 602);
			this.butScan.Name = "butScan";
			this.butScan.Size = new System.Drawing.Size(75, 24);
			this.butScan.TabIndex = 5;
			this.butScan.Text = "Scan";
			this.butScan.Click += new System.EventHandler(this.butScan_Click);
			//
			// butFix
			//
			this.butFix.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butFix.Location = new System.Drawing.Point(732, 602);
			this.butFix.Name = "butFix";
			this.butFix.Size = new System.Drawing.Size(75, 24);
			this.butFix.TabIndex = 6;
			this.butFix.Text = "Fix";
			this.butFix.Click += new System.EventHandler(this.butFix_Click);
			//
			// butClose
			//
			this.butClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butClose.Location = new System.Drawing.Point(813, 602);
			this.butClose.Name = "butClose";
			this.butClose.Size = new System.Drawing.Size(75, 24);
			this.butClose.TabIndex = 7;
			this.butClose.Text = "Close";
			this.butClose.Click += new System.EventHandler(this.butClose_Click);
			//
			// FormDatabaseMaintInvalidChars
			//
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(900, 638);
			this.Controls.Add(this.labelExplain);
			this.Controls.Add(this.gridColumns);
			this.Controls.Add(this.labelSummary);
			this.Controls.Add(this.gridPreview);
			this.Controls.Add(this.labelCannotFix);
			this.Controls.Add(this.butScan);
			this.Controls.Add(this.butFix);
			this.Controls.Add(this.butClose);
			this.MinimizeBox = false;
			this.Name = "FormDatabaseMaintInvalidChars";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Invalid Characters (GDI+)";
			this.Load += new System.EventHandler(this.FormDatabaseMaintInvalidChars_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label labelExplain;
		private OpenDental.UI.GridOD gridColumns;
		private OpenDental.UI.GridOD gridPreview;
		private System.Windows.Forms.Label labelSummary;
		private System.Windows.Forms.Label labelCannotFix;
		private OpenDental.UI.Button butScan;
		private OpenDental.UI.Button butFix;
		private OpenDental.UI.Button butClose;
	}
}
