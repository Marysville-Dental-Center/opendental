namespace OpenDental {
	partial class FormMicrosoft365Setup {
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
			this.checkEnabled = new OpenDental.UI.CheckBox();
			this.labelDescription = new System.Windows.Forms.Label();
			this.labelClientId = new System.Windows.Forms.Label();
			this.textClientId = new System.Windows.Forms.TextBox();
			this.labelTenantId = new System.Windows.Forms.Label();
			this.textTenantId = new System.Windows.Forms.TextBox();
			this.groupAdvanced = new OpenDental.UI.GroupBox();
			this.labelRedirectUri = new System.Windows.Forms.Label();
			this.textRedirectUri = new System.Windows.Forms.TextBox();
			this.labelSignInMethod = new System.Windows.Forms.Label();
			this.comboSignInMethod = new OpenDental.UI.ComboBox();
			this.checkAllowPersonal = new OpenDental.UI.CheckBox();
			this.labelPersonalWarning = new System.Windows.Forms.Label();
			this.labelSteps = new System.Windows.Forms.Label();
			this.textSteps = new System.Windows.Forms.TextBox();
			this.butSave = new OpenDental.UI.Button();
			this.groupAdvanced.SuspendLayout();
			this.SuspendLayout();
			//
			// checkEnabled
			//
			this.checkEnabled.Location = new System.Drawing.Point(12, 12);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(250, 18);
			this.checkEnabled.TabIndex = 0;
			this.checkEnabled.Text = "Enabled";
			//
			// labelDescription
			//
			this.labelDescription.Location = new System.Drawing.Point(12, 36);
			this.labelDescription.Name = "labelDescription";
			this.labelDescription.Size = new System.Drawing.Size(496, 44);
			this.labelDescription.TabIndex = 1;
			this.labelDescription.Text = "Sends email through Microsoft Graph for addresses set to Microsoft sign-in in Setup > Email, and downloads their inbox when the address asks for that. It uses an app registration in the office\'s own Microsoft Entra tenant. There is no client secret.";
			//
			// labelClientId
			//
			this.labelClientId.Location = new System.Drawing.Point(12, 89);
			this.labelClientId.Name = "labelClientId";
			this.labelClientId.Size = new System.Drawing.Size(160, 18);
			this.labelClientId.TabIndex = 2;
			this.labelClientId.Text = "Application (client) ID";
			this.labelClientId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textClientId
			//
			this.textClientId.Location = new System.Drawing.Point(175, 88);
			this.textClientId.Name = "textClientId";
			this.textClientId.Size = new System.Drawing.Size(330, 20);
			this.textClientId.TabIndex = 3;
			//
			// labelTenantId
			//
			this.labelTenantId.Location = new System.Drawing.Point(12, 115);
			this.labelTenantId.Name = "labelTenantId";
			this.labelTenantId.Size = new System.Drawing.Size(160, 18);
			this.labelTenantId.TabIndex = 4;
			this.labelTenantId.Text = "Directory (tenant) ID";
			this.labelTenantId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textTenantId
			//
			this.textTenantId.Location = new System.Drawing.Point(175, 114);
			this.textTenantId.Name = "textTenantId";
			this.textTenantId.Size = new System.Drawing.Size(330, 20);
			this.textTenantId.TabIndex = 5;
			//
			// groupAdvanced
			//
			this.groupAdvanced.Controls.Add(this.labelRedirectUri);
			this.groupAdvanced.Controls.Add(this.textRedirectUri);
			this.groupAdvanced.Controls.Add(this.labelSignInMethod);
			this.groupAdvanced.Controls.Add(this.comboSignInMethod);
			this.groupAdvanced.Controls.Add(this.checkAllowPersonal);
			this.groupAdvanced.Location = new System.Drawing.Point(12, 146);
			this.groupAdvanced.Name = "groupAdvanced";
			this.groupAdvanced.Size = new System.Drawing.Size(496, 104);
			this.groupAdvanced.TabIndex = 6;
			this.groupAdvanced.Text = "Advanced";
			//
			// labelRedirectUri
			//
			this.labelRedirectUri.Location = new System.Drawing.Point(6, 21);
			this.labelRedirectUri.Name = "labelRedirectUri";
			this.labelRedirectUri.Size = new System.Drawing.Size(154, 18);
			this.labelRedirectUri.TabIndex = 0;
			this.labelRedirectUri.Text = "Redirect URI";
			this.labelRedirectUri.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textRedirectUri
			//
			this.textRedirectUri.Location = new System.Drawing.Point(163, 20);
			this.textRedirectUri.Name = "textRedirectUri";
			this.textRedirectUri.Size = new System.Drawing.Size(320, 20);
			this.textRedirectUri.TabIndex = 1;
			//
			// labelSignInMethod
			//
			this.labelSignInMethod.Location = new System.Drawing.Point(6, 47);
			this.labelSignInMethod.Name = "labelSignInMethod";
			this.labelSignInMethod.Size = new System.Drawing.Size(154, 18);
			this.labelSignInMethod.TabIndex = 2;
			this.labelSignInMethod.Text = "Sign-in method";
			this.labelSignInMethod.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboSignInMethod
			//
			this.comboSignInMethod.Location = new System.Drawing.Point(163, 46);
			this.comboSignInMethod.Name = "comboSignInMethod";
			this.comboSignInMethod.Size = new System.Drawing.Size(200, 21);
			this.comboSignInMethod.TabIndex = 3;
			//
			// checkAllowPersonal
			//
			this.checkAllowPersonal.Location = new System.Drawing.Point(163, 75);
			this.checkAllowPersonal.Name = "checkAllowPersonal";
			this.checkAllowPersonal.Size = new System.Drawing.Size(320, 18);
			this.checkAllowPersonal.TabIndex = 4;
			this.checkAllowPersonal.Text = "Allow personal Microsoft accounts";
			this.checkAllowPersonal.CheckedChanged += new System.EventHandler(this.checkAllowPersonal_CheckedChanged);
			//
			// labelPersonalWarning
			//
			this.labelPersonalWarning.ForeColor = System.Drawing.Color.DarkRed;
			this.labelPersonalWarning.Location = new System.Drawing.Point(12, 255);
			this.labelPersonalWarning.Name = "labelPersonalWarning";
			this.labelPersonalWarning.Size = new System.Drawing.Size(496, 32);
			this.labelPersonalWarning.TabIndex = 7;
			this.labelPersonalWarning.Text = "Microsoft\'s HIPAA business associate agreement covers its business online services. Do not use personal Microsoft accounts for patient information.";
			this.labelPersonalWarning.Visible = false;
			//
			// labelSteps
			//
			this.labelSteps.Location = new System.Drawing.Point(12, 292);
			this.labelSteps.Name = "labelSteps";
			this.labelSteps.Size = new System.Drawing.Size(496, 18);
			this.labelSteps.TabIndex = 8;
			this.labelSteps.Text = "Registering the app in the office\'s tenant";
			//
			// textSteps
			//
			this.textSteps.BackColor = System.Drawing.SystemColors.Control;
			this.textSteps.Location = new System.Drawing.Point(12, 312);
			this.textSteps.Multiline = true;
			this.textSteps.Name = "textSteps";
			this.textSteps.ReadOnly = true;
			this.textSteps.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textSteps.Size = new System.Drawing.Size(496, 128);
			this.textSteps.TabIndex = 9;
			this.textSteps.TabStop = false;
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(433, 450);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 10;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormMicrosoft365Setup
			//
			this.ClientSize = new System.Drawing.Size(520, 486);
			this.Controls.Add(this.checkEnabled);
			this.Controls.Add(this.labelDescription);
			this.Controls.Add(this.labelClientId);
			this.Controls.Add(this.textClientId);
			this.Controls.Add(this.labelTenantId);
			this.Controls.Add(this.textTenantId);
			this.Controls.Add(this.groupAdvanced);
			this.Controls.Add(this.labelPersonalWarning);
			this.Controls.Add(this.labelSteps);
			this.Controls.Add(this.textSteps);
			this.Controls.Add(this.butSave);
			this.Name = "FormMicrosoft365Setup";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Microsoft 365 Email Setup";
			this.Load += new System.EventHandler(this.FormMicrosoft365Setup_Load);
			this.groupAdvanced.ResumeLayout(false);
			this.groupAdvanced.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private OpenDental.UI.CheckBox checkEnabled;
		private System.Windows.Forms.Label labelDescription;
		private System.Windows.Forms.Label labelClientId;
		private System.Windows.Forms.TextBox textClientId;
		private System.Windows.Forms.Label labelTenantId;
		private System.Windows.Forms.TextBox textTenantId;
		private OpenDental.UI.GroupBox groupAdvanced;
		private System.Windows.Forms.Label labelRedirectUri;
		private System.Windows.Forms.TextBox textRedirectUri;
		private System.Windows.Forms.Label labelSignInMethod;
		private OpenDental.UI.ComboBox comboSignInMethod;
		private OpenDental.UI.CheckBox checkAllowPersonal;
		private System.Windows.Forms.Label labelPersonalWarning;
		private System.Windows.Forms.Label labelSteps;
		private System.Windows.Forms.TextBox textSteps;
		private OpenDental.UI.Button butSave;
	}
}
