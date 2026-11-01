namespace OpenPmsDental {
	partial class FormDiseaseDefMerge {
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
			this.radioKeep1 = new System.Windows.Forms.RadioButton();
			this.radioKeep2 = new System.Windows.Forms.RadioButton();
			this.butOK = new OpenPmsDental.UI.Button();
			this.butCancel = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelExplain
			//
			this.labelExplain.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.labelExplain.Location = new System.Drawing.Point(12, 9);
			this.labelExplain.Name = "labelExplain";
			this.labelExplain.Size = new System.Drawing.Size(476, 58);
			this.labelExplain.TabIndex = 0;
			this.labelExplain.Text = "Choose the problem to keep. Patient problems, Rx alerts, education resources, family health entries, CDS triggers and reminder rules that use the other problem will use the kept one, and the other problem will be deleted. This cannot be undone.";
			//
			// radioKeep1
			//
			this.radioKeep1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.radioKeep1.CheckAlign = System.Drawing.ContentAlignment.TopLeft;
			this.radioKeep1.Location = new System.Drawing.Point(15, 72);
			this.radioKeep1.Name = "radioKeep1";
			this.radioKeep1.Size = new System.Drawing.Size(473, 36);
			this.radioKeep1.TabIndex = 1;
			this.radioKeep1.TextAlign = System.Drawing.ContentAlignment.TopLeft;
			this.radioKeep1.UseMnemonic = false;
			//
			// radioKeep2
			//
			this.radioKeep2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.radioKeep2.CheckAlign = System.Drawing.ContentAlignment.TopLeft;
			this.radioKeep2.Location = new System.Drawing.Point(15, 112);
			this.radioKeep2.Name = "radioKeep2";
			this.radioKeep2.Size = new System.Drawing.Size(473, 36);
			this.radioKeep2.TabIndex = 2;
			this.radioKeep2.TextAlign = System.Drawing.ContentAlignment.TopLeft;
			this.radioKeep2.UseMnemonic = false;
			//
			// butOK
			//
			this.butOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butOK.Location = new System.Drawing.Point(332, 162);
			this.butOK.Name = "butOK";
			this.butOK.Size = new System.Drawing.Size(75, 24);
			this.butOK.TabIndex = 3;
			this.butOK.Text = "&OK";
			this.butOK.Click += new System.EventHandler(this.butOK_Click);
			//
			// butCancel
			//
			this.butCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butCancel.Location = new System.Drawing.Point(413, 162);
			this.butCancel.Name = "butCancel";
			this.butCancel.Size = new System.Drawing.Size(75, 24);
			this.butCancel.TabIndex = 4;
			this.butCancel.Text = "&Cancel";
			this.butCancel.Click += new System.EventHandler(this.butCancel_Click);
			//
			// FormDiseaseDefMerge
			//
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(500, 198);
			this.Controls.Add(this.labelExplain);
			this.Controls.Add(this.radioKeep1);
			this.Controls.Add(this.radioKeep2);
			this.Controls.Add(this.butOK);
			this.Controls.Add(this.butCancel);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormDiseaseDefMerge";
			this.ShowInTaskbar = false;
			this.Text = "Merge Problems";
			this.Load += new System.EventHandler(this.FormDiseaseDefMerge_Load);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Label labelExplain;
		private System.Windows.Forms.RadioButton radioKeep1;
		private System.Windows.Forms.RadioButton radioKeep2;
		private OpenPmsDental.UI.Button butOK;
		private OpenPmsDental.UI.Button butCancel;
	}
}
