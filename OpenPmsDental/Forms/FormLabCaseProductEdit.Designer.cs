namespace OpenPmsDental {
	partial class FormLabCaseProductEdit {
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
			this.labelMaterial = new System.Windows.Forms.Label();
			this.textMaterial = new System.Windows.Forms.TextBox();
			this.labelShade = new System.Windows.Forms.Label();
			this.textShade = new System.Windows.Forms.TextBox();
			this.labelToothRange = new System.Windows.Forms.Label();
			this.textToothRange = new System.Windows.Forms.TextBox();
			this.butToothRangeFromProcs = new OpenPmsDental.UI.Button();
			this.labelNotes = new System.Windows.Forms.Label();
			this.textNotes = new System.Windows.Forms.TextBox();
			this.labelProductListValue = new System.Windows.Forms.Label();
			this.butDelete = new OpenPmsDental.UI.Button();
			this.butSave = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelMaterial
			//
			this.labelMaterial.Location = new System.Drawing.Point(12, 15);
			this.labelMaterial.Name = "labelMaterial";
			this.labelMaterial.Size = new System.Drawing.Size(90, 17);
			this.labelMaterial.TabIndex = 0;
			this.labelMaterial.Text = "Material";
			this.labelMaterial.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textMaterial
			//
			this.textMaterial.Location = new System.Drawing.Point(104, 14);
			this.textMaterial.MaxLength = 255;
			this.textMaterial.Name = "textMaterial";
			this.textMaterial.Size = new System.Drawing.Size(220, 20);
			this.textMaterial.TabIndex = 1;
			//
			// labelShade
			//
			this.labelShade.Location = new System.Drawing.Point(12, 41);
			this.labelShade.Name = "labelShade";
			this.labelShade.Size = new System.Drawing.Size(90, 17);
			this.labelShade.TabIndex = 2;
			this.labelShade.Text = "Shade";
			this.labelShade.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textShade
			//
			this.textShade.Location = new System.Drawing.Point(104, 40);
			this.textShade.MaxLength = 255;
			this.textShade.Name = "textShade";
			this.textShade.Size = new System.Drawing.Size(100, 20);
			this.textShade.TabIndex = 3;
			//
			// labelToothRange
			//
			this.labelToothRange.Location = new System.Drawing.Point(12, 67);
			this.labelToothRange.Name = "labelToothRange";
			this.labelToothRange.Size = new System.Drawing.Size(90, 17);
			this.labelToothRange.TabIndex = 4;
			this.labelToothRange.Text = "Teeth";
			this.labelToothRange.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textToothRange
			//
			this.textToothRange.Location = new System.Drawing.Point(104, 66);
			this.textToothRange.MaxLength = 255;
			this.textToothRange.Name = "textToothRange";
			this.textToothRange.Size = new System.Drawing.Size(150, 20);
			this.textToothRange.TabIndex = 5;
			//
			// butToothRangeFromProcs
			//
			this.butToothRangeFromProcs.Location = new System.Drawing.Point(260, 64);
			this.butToothRangeFromProcs.Name = "butToothRangeFromProcs";
			this.butToothRangeFromProcs.Size = new System.Drawing.Size(110, 24);
			this.butToothRangeFromProcs.TabIndex = 11;
			this.butToothRangeFromProcs.Text = "From Procedures";
			this.butToothRangeFromProcs.Click += new System.EventHandler(this.butToothRangeFromProcs_Click);
			//
			// labelNotes
			//
			this.labelNotes.Location = new System.Drawing.Point(12, 93);
			this.labelNotes.Name = "labelNotes";
			this.labelNotes.Size = new System.Drawing.Size(90, 17);
			this.labelNotes.TabIndex = 6;
			this.labelNotes.Text = "Notes";
			this.labelNotes.TextAlign = System.Drawing.ContentAlignment.TopRight;
			//
			// textNotes
			//
			this.textNotes.Location = new System.Drawing.Point(104, 92);
			this.textNotes.Multiline = true;
			this.textNotes.Name = "textNotes";
			this.textNotes.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textNotes.Size = new System.Drawing.Size(300, 80);
			this.textNotes.TabIndex = 7;
			//
			// labelProductListValue
			//
			this.labelProductListValue.Location = new System.Drawing.Point(104, 177);
			this.labelProductListValue.Name = "labelProductListValue";
			this.labelProductListValue.Size = new System.Drawing.Size(300, 17);
			this.labelProductListValue.TabIndex = 8;
			//
			// butDelete
			//
			this.butDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butDelete.Icon = OpenPmsDental.UI.EnumIcons.DeleteX;
			this.butDelete.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDelete.Location = new System.Drawing.Point(12, 209);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(81, 24);
			this.butDelete.TabIndex = 9;
			this.butDelete.Text = "Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(329, 209);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 10;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormLabCaseProductEdit
			//
			this.ClientSize = new System.Drawing.Size(416, 245);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.butDelete);
			this.Controls.Add(this.labelProductListValue);
			this.Controls.Add(this.textNotes);
			this.Controls.Add(this.labelNotes);
			this.Controls.Add(this.butToothRangeFromProcs);
			this.Controls.Add(this.textToothRange);
			this.Controls.Add(this.labelToothRange);
			this.Controls.Add(this.textShade);
			this.Controls.Add(this.labelShade);
			this.Controls.Add(this.textMaterial);
			this.Controls.Add(this.labelMaterial);
			this.Name = "FormLabCaseProductEdit";
			this.Text = "Lab Case Product";
			this.Load += new System.EventHandler(this.FormLabCaseProductEdit_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelMaterial;
		private System.Windows.Forms.TextBox textMaterial;
		private System.Windows.Forms.Label labelShade;
		private System.Windows.Forms.TextBox textShade;
		private System.Windows.Forms.Label labelToothRange;
		private System.Windows.Forms.TextBox textToothRange;
		private OpenPmsDental.UI.Button butToothRangeFromProcs;
		private System.Windows.Forms.Label labelNotes;
		private System.Windows.Forms.TextBox textNotes;
		private System.Windows.Forms.Label labelProductListValue;
		private OpenPmsDental.UI.Button butDelete;
		private OpenPmsDental.UI.Button butSave;
	}
}
