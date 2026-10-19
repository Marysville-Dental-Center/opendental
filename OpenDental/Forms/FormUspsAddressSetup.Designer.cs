namespace OpenDental {
	partial class FormUspsAddressSetup {
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
			this.labelConsumerKey = new System.Windows.Forms.Label();
			this.textConsumerKey = new System.Windows.Forms.TextBox();
			this.labelConsumerSecret = new System.Windows.Forms.Label();
			this.textConsumerSecret = new System.Windows.Forms.TextBox();
			this.labelSecretStatus = new System.Windows.Forms.Label();
			this.labelEnvironment = new System.Windows.Forms.Label();
			this.comboEnvironment = new OpenDental.UI.ComboBox();
			this.butTest = new OpenDental.UI.Button();
			this.labelTestResult = new System.Windows.Forms.Label();
			this.butSave = new OpenDental.UI.Button();
			this.SuspendLayout();
			//
			// checkEnabled
			//
			this.checkEnabled.Location = new System.Drawing.Point(12, 12);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(200, 18);
			this.checkEnabled.TabIndex = 0;
			this.checkEnabled.Text = "Enabled";
			//
			// labelDescription
			//
			this.labelDescription.Location = new System.Drawing.Point(12, 38);
			this.labelDescription.Name = "labelDescription";
			this.labelDescription.Size = new System.Drawing.Size(470, 58);
			this.labelDescription.TabIndex = 1;
			this.labelDescription.Text = "Sends the street, unit, city, state and ZIP of a patient address being saved or imported to the U.S. Postal Service for standardization. No patient name is sent. Requires the office\'s own USPS API credentials and license; USPS may charge per lookup.";
			//
			// labelConsumerKey
			//
			this.labelConsumerKey.Location = new System.Drawing.Point(12, 106);
			this.labelConsumerKey.Name = "labelConsumerKey";
			this.labelConsumerKey.Size = new System.Drawing.Size(130, 16);
			this.labelConsumerKey.TabIndex = 2;
			this.labelConsumerKey.Text = "Consumer Key";
			this.labelConsumerKey.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textConsumerKey
			//
			this.textConsumerKey.Location = new System.Drawing.Point(144, 104);
			this.textConsumerKey.Name = "textConsumerKey";
			this.textConsumerKey.Size = new System.Drawing.Size(320, 20);
			this.textConsumerKey.TabIndex = 3;
			//
			// labelConsumerSecret
			//
			this.labelConsumerSecret.Location = new System.Drawing.Point(12, 133);
			this.labelConsumerSecret.Name = "labelConsumerSecret";
			this.labelConsumerSecret.Size = new System.Drawing.Size(130, 16);
			this.labelConsumerSecret.TabIndex = 4;
			this.labelConsumerSecret.Text = "Consumer Secret";
			this.labelConsumerSecret.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textConsumerSecret
			//
			this.textConsumerSecret.Location = new System.Drawing.Point(144, 131);
			this.textConsumerSecret.Name = "textConsumerSecret";
			this.textConsumerSecret.PasswordChar = '*';
			this.textConsumerSecret.Size = new System.Drawing.Size(320, 20);
			this.textConsumerSecret.TabIndex = 5;
			//
			// labelSecretStatus
			//
			this.labelSecretStatus.Location = new System.Drawing.Point(144, 154);
			this.labelSecretStatus.Name = "labelSecretStatus";
			this.labelSecretStatus.Size = new System.Drawing.Size(320, 30);
			this.labelSecretStatus.TabIndex = 6;
			this.labelSecretStatus.Text = "Not set.";
			//
			// labelEnvironment
			//
			this.labelEnvironment.Location = new System.Drawing.Point(12, 191);
			this.labelEnvironment.Name = "labelEnvironment";
			this.labelEnvironment.Size = new System.Drawing.Size(130, 16);
			this.labelEnvironment.TabIndex = 7;
			this.labelEnvironment.Text = "Environment";
			this.labelEnvironment.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboEnvironment
			//
			this.comboEnvironment.Location = new System.Drawing.Point(144, 189);
			this.comboEnvironment.Name = "comboEnvironment";
			this.comboEnvironment.Size = new System.Drawing.Size(150, 21);
			this.comboEnvironment.TabIndex = 8;
			//
			// butTest
			//
			this.butTest.Location = new System.Drawing.Point(144, 222);
			this.butTest.Name = "butTest";
			this.butTest.Size = new System.Drawing.Size(110, 24);
			this.butTest.TabIndex = 9;
			this.butTest.Text = "Test connection";
			this.butTest.Click += new System.EventHandler(this.butTest_Click);
			//
			// labelTestResult
			//
			this.labelTestResult.Location = new System.Drawing.Point(12, 254);
			this.labelTestResult.Name = "labelTestResult";
			this.labelTestResult.Size = new System.Drawing.Size(470, 64);
			this.labelTestResult.TabIndex = 10;
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(407, 326);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 11;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormUspsAddressSetup
			//
			this.ClientSize = new System.Drawing.Size(494, 362);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.labelTestResult);
			this.Controls.Add(this.butTest);
			this.Controls.Add(this.comboEnvironment);
			this.Controls.Add(this.labelEnvironment);
			this.Controls.Add(this.labelSecretStatus);
			this.Controls.Add(this.textConsumerSecret);
			this.Controls.Add(this.labelConsumerSecret);
			this.Controls.Add(this.textConsumerKey);
			this.Controls.Add(this.labelConsumerKey);
			this.Controls.Add(this.labelDescription);
			this.Controls.Add(this.checkEnabled);
			this.Name = "FormUspsAddressSetup";
			this.Text = "USPS address validation setup";
			this.Load += new System.EventHandler(this.FormUspsAddressSetup_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private OpenDental.UI.CheckBox checkEnabled;
		private System.Windows.Forms.Label labelDescription;
		private System.Windows.Forms.Label labelConsumerKey;
		private System.Windows.Forms.TextBox textConsumerKey;
		private System.Windows.Forms.Label labelConsumerSecret;
		private System.Windows.Forms.TextBox textConsumerSecret;
		private System.Windows.Forms.Label labelSecretStatus;
		private System.Windows.Forms.Label labelEnvironment;
		private OpenDental.UI.ComboBox comboEnvironment;
		private OpenDental.UI.Button butTest;
		private System.Windows.Forms.Label labelTestResult;
		private OpenDental.UI.Button butSave;
	}
}
