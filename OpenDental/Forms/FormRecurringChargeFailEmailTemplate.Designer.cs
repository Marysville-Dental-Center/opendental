namespace OpenDental {
	partial class FormRecurringChargeFailEmailTemplate {
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
			this.labelIntro = new System.Windows.Forms.Label();
			this.labelSubject = new System.Windows.Forms.Label();
			this.textSubject = new System.Windows.Forms.TextBox();
			this.labelBody = new System.Windows.Forms.Label();
			this.textBody = new System.Windows.Forms.TextBox();
			this.labelTags = new System.Windows.Forms.Label();
			this.butDefault = new OpenDental.UI.Button();
			this.butSave = new OpenDental.UI.Button();
			this.SuspendLayout();
			//
			// labelIntro
			//
			this.labelIntro.Location = new System.Drawing.Point(12, 9);
			this.labelIntro.Name = "labelIntro";
			this.labelIntro.Size = new System.Drawing.Size(536, 32);
			this.labelIntro.TabIndex = 0;
			this.labelIntro.Text = "Sent from the office\'s own e-mail address to the patient, or to the guarantor when the patient has no address, when a recurring charge is declined.";
			//
			// labelSubject
			//
			this.labelSubject.Location = new System.Drawing.Point(12, 48);
			this.labelSubject.Name = "labelSubject";
			this.labelSubject.Size = new System.Drawing.Size(60, 18);
			this.labelSubject.TabIndex = 1;
			this.labelSubject.Text = "Subject";
			this.labelSubject.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textSubject
			//
			this.textSubject.Location = new System.Drawing.Point(74, 47);
			this.textSubject.Name = "textSubject";
			this.textSubject.Size = new System.Drawing.Size(474, 20);
			this.textSubject.TabIndex = 2;
			//
			// labelBody
			//
			this.labelBody.Location = new System.Drawing.Point(12, 76);
			this.labelBody.Name = "labelBody";
			this.labelBody.Size = new System.Drawing.Size(60, 18);
			this.labelBody.TabIndex = 3;
			this.labelBody.Text = "Body";
			this.labelBody.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textBody
			//
			this.textBody.AcceptsReturn = true;
			this.textBody.Location = new System.Drawing.Point(74, 75);
			this.textBody.Multiline = true;
			this.textBody.Name = "textBody";
			this.textBody.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBody.Size = new System.Drawing.Size(474, 200);
			this.textBody.TabIndex = 4;
			//
			// labelTags
			//
			this.labelTags.Location = new System.Drawing.Point(74, 281);
			this.labelTags.Name = "labelTags";
			this.labelTags.Size = new System.Drawing.Size(474, 32);
			this.labelTags.TabIndex = 5;
			this.labelTags.Text = "Replacement tags";
			//
			// butDefault
			//
			this.butDefault.Location = new System.Drawing.Point(74, 321);
			this.butDefault.Name = "butDefault";
			this.butDefault.Size = new System.Drawing.Size(90, 24);
			this.butDefault.TabIndex = 6;
			this.butDefault.Text = "Use Default";
			this.butDefault.Click += new System.EventHandler(this.butDefault_Click);
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(473, 321);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 7;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormRecurringChargeFailEmailTemplate
			//
			this.ClientSize = new System.Drawing.Size(560, 357);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.butDefault);
			this.Controls.Add(this.labelTags);
			this.Controls.Add(this.textBody);
			this.Controls.Add(this.labelBody);
			this.Controls.Add(this.textSubject);
			this.Controls.Add(this.labelSubject);
			this.Controls.Add(this.labelIntro);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormRecurringChargeFailEmailTemplate";
			this.ShowInTaskbar = false;
			this.Text = "Recurring Charge Failure E-mail";
			this.Load += new System.EventHandler(this.FormRecurringChargeFailEmailTemplate_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelIntro;
		private System.Windows.Forms.Label labelSubject;
		private System.Windows.Forms.TextBox textSubject;
		private System.Windows.Forms.Label labelBody;
		private System.Windows.Forms.TextBox textBody;
		private System.Windows.Forms.Label labelTags;
		private OpenDental.UI.Button butDefault;
		private OpenDental.UI.Button butSave;
	}
}
