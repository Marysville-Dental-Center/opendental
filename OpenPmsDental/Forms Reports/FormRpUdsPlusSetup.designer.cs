namespace OpenPmsDental {
	partial class FormRpUdsPlusSetup {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormRpUdsPlusSetup));
			this.labelInstructions = new System.Windows.Forms.Label();
			this.gridMain = new OpenPmsDental.UI.GridOD();
			this.butAdd = new OpenPmsDental.UI.Button();
			this.butDelete = new OpenPmsDental.UI.Button();
			this.butTemplate = new OpenPmsDental.UI.Button();
			this.butSave = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelInstructions
			//
			this.labelInstructions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelInstructions.Location = new System.Drawing.Point(12, 9);
			this.labelInstructions.Name = "labelInstructions";
			this.labelInstructions.Size = new System.Drawing.Size(736, 70);
			this.labelInstructions.TabIndex = 0;
			this.labelInstructions.Text = "Enter each dental service line of UDS Table 6A, as the UDS manual for the reporting year numbers and names it, with the procedure codes counted on it. No codes ship with this program. Separate codes with commas. A range is two codes of the same length joined by a hyphen (for example D1000-D1999); a code ending in * counts every code that starts with it (for example D1*). Only completed procedures are counted.";
			//
			// gridMain
			//
			this.gridMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridMain.Location = new System.Drawing.Point(12, 84);
			this.gridMain.Name = "gridMain";
			this.gridMain.SelectionMode = OpenPmsDental.UI.GridSelectionMode.OneCell;
			this.gridMain.Size = new System.Drawing.Size(736, 384);
			this.gridMain.TabIndex = 1;
			this.gridMain.Title = "UDS Dental Service Lines";
			this.gridMain.TranslationName = "TableUdsPlusLines";
			//
			// butAdd
			//
			this.butAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAdd.Image = global::OpenPmsDental.Properties.Resources.Add;
			this.butAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAdd.Location = new System.Drawing.Point(12, 476);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(80, 24);
			this.butAdd.TabIndex = 2;
			this.butAdd.Text = "&Add";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			//
			// butDelete
			//
			this.butDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butDelete.Image = global::OpenPmsDental.Properties.Resources.DeleteX;
			this.butDelete.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDelete.Location = new System.Drawing.Point(98, 476);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(80, 24);
			this.butDelete.TabIndex = 3;
			this.butDelete.Text = "&Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			//
			// butTemplate
			//
			this.butTemplate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butTemplate.Location = new System.Drawing.Point(184, 476);
			this.butTemplate.Name = "butTemplate";
			this.butTemplate.Size = new System.Drawing.Size(150, 24);
			this.butTemplate.TabIndex = 4;
			this.butTemplate.Text = "Add Table 6A &Lines";
			this.butTemplate.Click += new System.EventHandler(this.butTemplate_Click);
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(673, 476);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 5;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormRpUdsPlusSetup
			//
			this.ClientSize = new System.Drawing.Size(760, 512);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.butTemplate);
			this.Controls.Add(this.butDelete);
			this.Controls.Add(this.butAdd);
			this.Controls.Add(this.gridMain);
			this.Controls.Add(this.labelInstructions);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormRpUdsPlusSetup";
			this.Text = "UDS+ Report Setup";
			this.Load += new System.EventHandler(this.FormRpUdsPlusSetup_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label labelInstructions;
		private OpenPmsDental.UI.GridOD gridMain;
		private OpenPmsDental.UI.Button butAdd;
		private OpenPmsDental.UI.Button butDelete;
		private OpenPmsDental.UI.Button butTemplate;
		private OpenPmsDental.UI.Button butSave;
	}
}
