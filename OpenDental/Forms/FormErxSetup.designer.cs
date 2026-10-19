namespace OpenDental{
	partial class FormErxSetup {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormErxSetup));
			this.butSave = new OpenDental.UI.Button();
			this.checkEnabled = new OpenDental.UI.CheckBox();
			this.groupErxOptions = new OpenDental.UI.GroupBox();
			this.radioDoseSpotLegacy = new System.Windows.Forms.RadioButton();
			this.radioDoseSpot = new System.Windows.Forms.RadioButton();
			this.radioNewCrop = new System.Windows.Forms.RadioButton();
			this.labelInfo = new System.Windows.Forms.Label();
			this.tabControlVendors = new OpenDental.UI.TabControl();
			this.tabPageDoseSpot = new OpenDental.UI.TabPage();
			this.labelDoseSpotEnvironment = new System.Windows.Forms.Label();
			this.comboDoseSpotEnvironment = new System.Windows.Forms.ComboBox();
			this.labelSubscriptionKey = new System.Windows.Forms.Label();
			this.textSubscriptionKey = new System.Windows.Forms.TextBox();
			this.checkShowSubscriptionKey = new OpenDental.UI.CheckBox();
			this.checkShowHiddenClinics = new OpenDental.UI.CheckBox();
			this.gridProperties = new OpenDental.UI.GridOD();
			this.butDelete = new OpenDental.UI.Button();
			this.butDoseSpotTest = new OpenDental.UI.Button();
			this.tabPageNewCrop = new OpenDental.UI.TabPage();
			this.labelNewCropEnvironment = new System.Windows.Forms.Label();
			this.comboNewCropEnvironment = new System.Windows.Forms.ComboBox();
			this.labelPartnerName = new System.Windows.Forms.Label();
			this.textPartnerName = new System.Windows.Forms.TextBox();
			this.labelAccountName = new System.Windows.Forms.Label();
			this.textAccountName = new System.Windows.Forms.TextBox();
			this.labelPassword = new System.Windows.Forms.Label();
			this.textPassword = new System.Windows.Forms.TextBox();
			this.checkShowPassword = new OpenDental.UI.CheckBox();
			this.labelAccountId = new System.Windows.Forms.Label();
			this.textAccountId = new System.Windows.Forms.TextBox();
			this.butGenerateAccountId = new OpenDental.UI.Button();
			this.butClearAccountId = new OpenDental.UI.Button();
			this.labelSiteId = new System.Windows.Forms.Label();
			this.textSiteId = new System.Windows.Forms.TextBox();
			this.labelProductName = new System.Windows.Forms.Label();
			this.textProductName = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textNewCropAccountID = new System.Windows.Forms.TextBox();
			this.butNewCropTest = new OpenDental.UI.Button();
			this.gridNewCropProviders = new OpenDental.UI.GridOD();
			this.groupErxOptions.SuspendLayout();
			this.tabControlVendors.SuspendLayout();
			this.tabPageDoseSpot.SuspendLayout();
			this.tabPageNewCrop.SuspendLayout();
			this.SuspendLayout();
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(693, 566);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 3;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// checkEnabled
			//
			this.checkEnabled.Location = new System.Drawing.Point(23, 4);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(98, 18);
			this.checkEnabled.TabIndex = 42;
			this.checkEnabled.Text = "Enabled";
			//
			// groupErxOptions
			//
			this.groupErxOptions.Controls.Add(this.radioDoseSpotLegacy);
			this.groupErxOptions.Controls.Add(this.radioDoseSpot);
			this.groupErxOptions.Controls.Add(this.radioNewCrop);
			this.groupErxOptions.Location = new System.Drawing.Point(23, 25);
			this.groupErxOptions.Name = "groupErxOptions";
			this.groupErxOptions.Size = new System.Drawing.Size(171, 80);
			this.groupErxOptions.TabIndex = 43;
			this.groupErxOptions.Text = "eRx Solution";
			//
			// radioDoseSpotLegacy
			//
			this.radioDoseSpotLegacy.Location = new System.Drawing.Point(16, 55);
			this.radioDoseSpotLegacy.Name = "radioDoseSpotLegacy";
			this.radioDoseSpotLegacy.Size = new System.Drawing.Size(149, 17);
			this.radioDoseSpotLegacy.TabIndex = 2;
			this.radioDoseSpotLegacy.Text = "DoseSpot with NewCrop";
			this.radioDoseSpotLegacy.UseVisualStyleBackColor = true;
			this.radioDoseSpotLegacy.Click += new System.EventHandler(this.radioDoseSpotLegacy_Click);
			//
			// radioDoseSpot
			//
			this.radioDoseSpot.Location = new System.Drawing.Point(16, 36);
			this.radioDoseSpot.Name = "radioDoseSpot";
			this.radioDoseSpot.Size = new System.Drawing.Size(96, 17);
			this.radioDoseSpot.TabIndex = 1;
			this.radioDoseSpot.Text = "DoseSpot";
			this.radioDoseSpot.UseVisualStyleBackColor = true;
			this.radioDoseSpot.Click += new System.EventHandler(this.radioDoseSpot_Click);
			//
			// radioNewCrop
			//
			this.radioNewCrop.Checked = true;
			this.radioNewCrop.Location = new System.Drawing.Point(16, 17);
			this.radioNewCrop.Name = "radioNewCrop";
			this.radioNewCrop.Size = new System.Drawing.Size(96, 17);
			this.radioNewCrop.TabIndex = 0;
			this.radioNewCrop.TabStop = true;
			this.radioNewCrop.Text = "NewCrop";
			this.radioNewCrop.UseVisualStyleBackColor = true;
			this.radioNewCrop.Click += new System.EventHandler(this.radioNewCrop_Click);
			//
			// labelInfo
			//
			this.labelInfo.Location = new System.Drawing.Point(210, 29);
			this.labelInfo.Name = "labelInfo";
			this.labelInfo.Size = new System.Drawing.Size(558, 74);
			this.labelInfo.TabIndex = 64;
			this.labelInfo.Text = "Prescriptions are written in the eRx vendor\'s own screens, opened from the Chart. Enter the values the office\'s agreement with the vendor provides. Keys and passwords are stored encrypted, and changing them needs the high-security program property permission. Nothing is sent to the vendor until eRx is enabled and its values are entered.";
			//
			// tabControlVendors
			//
			this.tabControlVendors.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.tabControlVendors.Controls.Add(this.tabPageDoseSpot);
			this.tabControlVendors.Controls.Add(this.tabPageNewCrop);
			this.tabControlVendors.Location = new System.Drawing.Point(12, 112);
			this.tabControlVendors.Name = "tabControlVendors";
			this.tabControlVendors.Size = new System.Drawing.Size(756, 446);
			this.tabControlVendors.TabIndex = 65;
			//
			// tabPageDoseSpot
			//
			this.tabPageDoseSpot.BackColor = System.Drawing.SystemColors.Window;
			this.tabPageDoseSpot.Controls.Add(this.labelDoseSpotEnvironment);
			this.tabPageDoseSpot.Controls.Add(this.comboDoseSpotEnvironment);
			this.tabPageDoseSpot.Controls.Add(this.labelSubscriptionKey);
			this.tabPageDoseSpot.Controls.Add(this.textSubscriptionKey);
			this.tabPageDoseSpot.Controls.Add(this.checkShowSubscriptionKey);
			this.tabPageDoseSpot.Controls.Add(this.checkShowHiddenClinics);
			this.tabPageDoseSpot.Controls.Add(this.gridProperties);
			this.tabPageDoseSpot.Controls.Add(this.butDelete);
			this.tabPageDoseSpot.Controls.Add(this.butDoseSpotTest);
			this.tabPageDoseSpot.Location = new System.Drawing.Point(2, 21);
			this.tabPageDoseSpot.Name = "tabPageDoseSpot";
			this.tabPageDoseSpot.Padding = new System.Windows.Forms.Padding(3);
			this.tabPageDoseSpot.Size = new System.Drawing.Size(752, 423);
			this.tabPageDoseSpot.TabIndex = 0;
			this.tabPageDoseSpot.Text = "DoseSpot";
			//
			// labelDoseSpotEnvironment
			//
			this.labelDoseSpotEnvironment.Location = new System.Drawing.Point(6, 10);
			this.labelDoseSpotEnvironment.Name = "labelDoseSpotEnvironment";
			this.labelDoseSpotEnvironment.Size = new System.Drawing.Size(120, 20);
			this.labelDoseSpotEnvironment.TabIndex = 0;
			this.labelDoseSpotEnvironment.Text = "Environment";
			this.labelDoseSpotEnvironment.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboDoseSpotEnvironment
			//
			this.comboDoseSpotEnvironment.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboDoseSpotEnvironment.Location = new System.Drawing.Point(130, 10);
			this.comboDoseSpotEnvironment.Name = "comboDoseSpotEnvironment";
			this.comboDoseSpotEnvironment.Size = new System.Drawing.Size(140, 21);
			this.comboDoseSpotEnvironment.TabIndex = 1;
			//
			// labelSubscriptionKey
			//
			this.labelSubscriptionKey.Location = new System.Drawing.Point(6, 37);
			this.labelSubscriptionKey.Name = "labelSubscriptionKey";
			this.labelSubscriptionKey.Size = new System.Drawing.Size(120, 20);
			this.labelSubscriptionKey.TabIndex = 2;
			this.labelSubscriptionKey.Text = "Subscription Key";
			this.labelSubscriptionKey.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textSubscriptionKey
			//
			this.textSubscriptionKey.Location = new System.Drawing.Point(130, 37);
			this.textSubscriptionKey.Name = "textSubscriptionKey";
			this.textSubscriptionKey.Size = new System.Drawing.Size(300, 20);
			this.textSubscriptionKey.TabIndex = 3;
			this.textSubscriptionKey.UseSystemPasswordChar = true;
			//
			// checkShowSubscriptionKey
			//
			this.checkShowSubscriptionKey.Location = new System.Drawing.Point(440, 38);
			this.checkShowSubscriptionKey.Name = "checkShowSubscriptionKey";
			this.checkShowSubscriptionKey.Size = new System.Drawing.Size(90, 18);
			this.checkShowSubscriptionKey.TabIndex = 4;
			this.checkShowSubscriptionKey.Text = "Show key";
			this.checkShowSubscriptionKey.CheckedChanged += new System.EventHandler(this.checkShowSubscriptionKey_CheckedChanged);
			//
			// checkShowHiddenClinics
			//
			this.checkShowHiddenClinics.Location = new System.Drawing.Point(10, 66);
			this.checkShowHiddenClinics.Name = "checkShowHiddenClinics";
			this.checkShowHiddenClinics.Size = new System.Drawing.Size(123, 17);
			this.checkShowHiddenClinics.TabIndex = 6;
			this.checkShowHiddenClinics.Text = "Show Hidden Clinics";
			this.checkShowHiddenClinics.CheckedChanged += new System.EventHandler(this.checkShowHiddenClinics_CheckedChanged);
			//
			// gridProperties
			//
			this.gridProperties.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridProperties.Location = new System.Drawing.Point(10, 88);
			this.gridProperties.Name = "gridProperties";
			this.gridProperties.Size = new System.Drawing.Size(732, 296);
			this.gridProperties.TabIndex = 5;
			this.gridProperties.Title = "Clinics";
			this.gridProperties.TranslationName = "GridProperties";
			this.gridProperties.CellDoubleClick += new OpenDental.UI.ODGridClickEventHandler(this.gridProperties_CellDoubleClick);
			//
			// butDelete
			//
			this.butDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butDelete.Icon = OpenDental.UI.EnumIcons.DeleteX;
			this.butDelete.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDelete.Location = new System.Drawing.Point(10, 391);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(88, 24);
			this.butDelete.TabIndex = 63;
			this.butDelete.Text = "&Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			//
			// butDoseSpotTest
			//
			this.butDoseSpotTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butDoseSpotTest.Location = new System.Drawing.Point(632, 391);
			this.butDoseSpotTest.Name = "butDoseSpotTest";
			this.butDoseSpotTest.Size = new System.Drawing.Size(110, 24);
			this.butDoseSpotTest.TabIndex = 64;
			this.butDoseSpotTest.Text = "Test Connection";
			this.butDoseSpotTest.Click += new System.EventHandler(this.butDoseSpotTest_Click);
			//
			// tabPageNewCrop
			//
			this.tabPageNewCrop.BackColor = System.Drawing.SystemColors.Window;
			this.tabPageNewCrop.Controls.Add(this.labelNewCropEnvironment);
			this.tabPageNewCrop.Controls.Add(this.comboNewCropEnvironment);
			this.tabPageNewCrop.Controls.Add(this.labelPartnerName);
			this.tabPageNewCrop.Controls.Add(this.textPartnerName);
			this.tabPageNewCrop.Controls.Add(this.labelAccountName);
			this.tabPageNewCrop.Controls.Add(this.textAccountName);
			this.tabPageNewCrop.Controls.Add(this.labelPassword);
			this.tabPageNewCrop.Controls.Add(this.textPassword);
			this.tabPageNewCrop.Controls.Add(this.checkShowPassword);
			this.tabPageNewCrop.Controls.Add(this.labelAccountId);
			this.tabPageNewCrop.Controls.Add(this.textAccountId);
			this.tabPageNewCrop.Controls.Add(this.butGenerateAccountId);
			this.tabPageNewCrop.Controls.Add(this.butClearAccountId);
			this.tabPageNewCrop.Controls.Add(this.labelSiteId);
			this.tabPageNewCrop.Controls.Add(this.textSiteId);
			this.tabPageNewCrop.Controls.Add(this.labelProductName);
			this.tabPageNewCrop.Controls.Add(this.textProductName);
			this.tabPageNewCrop.Controls.Add(this.label7);
			this.tabPageNewCrop.Controls.Add(this.textNewCropAccountID);
			this.tabPageNewCrop.Controls.Add(this.butNewCropTest);
			this.tabPageNewCrop.Controls.Add(this.gridNewCropProviders);
			this.tabPageNewCrop.Location = new System.Drawing.Point(2, 21);
			this.tabPageNewCrop.Name = "tabPageNewCrop";
			this.tabPageNewCrop.Padding = new System.Windows.Forms.Padding(3);
			this.tabPageNewCrop.Size = new System.Drawing.Size(752, 423);
			this.tabPageNewCrop.TabIndex = 1;
			this.tabPageNewCrop.Text = "NewCrop";
			//
			// labelNewCropEnvironment
			//
			this.labelNewCropEnvironment.Location = new System.Drawing.Point(6, 10);
			this.labelNewCropEnvironment.Name = "labelNewCropEnvironment";
			this.labelNewCropEnvironment.Size = new System.Drawing.Size(120, 20);
			this.labelNewCropEnvironment.TabIndex = 0;
			this.labelNewCropEnvironment.Text = "Environment";
			this.labelNewCropEnvironment.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboNewCropEnvironment
			//
			this.comboNewCropEnvironment.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboNewCropEnvironment.Location = new System.Drawing.Point(130, 10);
			this.comboNewCropEnvironment.Name = "comboNewCropEnvironment";
			this.comboNewCropEnvironment.Size = new System.Drawing.Size(140, 21);
			this.comboNewCropEnvironment.TabIndex = 1;
			//
			// labelPartnerName
			//
			this.labelPartnerName.Location = new System.Drawing.Point(6, 37);
			this.labelPartnerName.Name = "labelPartnerName";
			this.labelPartnerName.Size = new System.Drawing.Size(120, 20);
			this.labelPartnerName.TabIndex = 2;
			this.labelPartnerName.Text = "Partner Name";
			this.labelPartnerName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textPartnerName
			//
			this.textPartnerName.Location = new System.Drawing.Point(130, 37);
			this.textPartnerName.Name = "textPartnerName";
			this.textPartnerName.Size = new System.Drawing.Size(200, 20);
			this.textPartnerName.TabIndex = 3;
			//
			// labelAccountName
			//
			this.labelAccountName.Location = new System.Drawing.Point(6, 63);
			this.labelAccountName.Name = "labelAccountName";
			this.labelAccountName.Size = new System.Drawing.Size(120, 20);
			this.labelAccountName.TabIndex = 4;
			this.labelAccountName.Text = "Account Name";
			this.labelAccountName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textAccountName
			//
			this.textAccountName.Location = new System.Drawing.Point(130, 63);
			this.textAccountName.Name = "textAccountName";
			this.textAccountName.Size = new System.Drawing.Size(200, 20);
			this.textAccountName.TabIndex = 5;
			//
			// labelPassword
			//
			this.labelPassword.Location = new System.Drawing.Point(6, 89);
			this.labelPassword.Name = "labelPassword";
			this.labelPassword.Size = new System.Drawing.Size(120, 20);
			this.labelPassword.TabIndex = 6;
			this.labelPassword.Text = "Password";
			this.labelPassword.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textPassword
			//
			this.textPassword.Location = new System.Drawing.Point(130, 89);
			this.textPassword.Name = "textPassword";
			this.textPassword.Size = new System.Drawing.Size(200, 20);
			this.textPassword.TabIndex = 7;
			this.textPassword.UseSystemPasswordChar = true;
			//
			// checkShowPassword
			//
			this.checkShowPassword.Location = new System.Drawing.Point(336, 90);
			this.checkShowPassword.Name = "checkShowPassword";
			this.checkShowPassword.Size = new System.Drawing.Size(80, 18);
			this.checkShowPassword.TabIndex = 8;
			this.checkShowPassword.Text = "Show";
			this.checkShowPassword.CheckedChanged += new System.EventHandler(this.checkShowPassword_CheckedChanged);
			//
			// labelAccountId
			//
			this.labelAccountId.Location = new System.Drawing.Point(6, 115);
			this.labelAccountId.Name = "labelAccountId";
			this.labelAccountId.Size = new System.Drawing.Size(120, 20);
			this.labelAccountId.TabIndex = 9;
			this.labelAccountId.Text = "Account ID";
			this.labelAccountId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textAccountId
			//
			this.textAccountId.Location = new System.Drawing.Point(130, 115);
			this.textAccountId.Name = "textAccountId";
			this.textAccountId.Size = new System.Drawing.Size(200, 20);
			this.textAccountId.TabIndex = 10;
			//
			// butGenerateAccountId
			//
			this.butGenerateAccountId.Location = new System.Drawing.Point(336, 113);
			this.butGenerateAccountId.Name = "butGenerateAccountId";
			this.butGenerateAccountId.Size = new System.Drawing.Size(70, 24);
			this.butGenerateAccountId.TabIndex = 11;
			this.butGenerateAccountId.Text = "Generate";
			this.butGenerateAccountId.Click += new System.EventHandler(this.butGenerateAccountId_Click);
			//
			// butClearAccountId
			//
			this.butClearAccountId.Location = new System.Drawing.Point(410, 113);
			this.butClearAccountId.Name = "butClearAccountId";
			this.butClearAccountId.Size = new System.Drawing.Size(60, 24);
			this.butClearAccountId.TabIndex = 12;
			this.butClearAccountId.Text = "Clear";
			this.butClearAccountId.UseVisualStyleBackColor = true;
			this.butClearAccountId.Click += new System.EventHandler(this.butClearAccountId_Click);
			//
			// labelSiteId
			//
			this.labelSiteId.Location = new System.Drawing.Point(6, 141);
			this.labelSiteId.Name = "labelSiteId";
			this.labelSiteId.Size = new System.Drawing.Size(120, 20);
			this.labelSiteId.TabIndex = 13;
			this.labelSiteId.Text = "Site ID";
			this.labelSiteId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textSiteId
			//
			this.textSiteId.Location = new System.Drawing.Point(130, 141);
			this.textSiteId.Name = "textSiteId";
			this.textSiteId.Size = new System.Drawing.Size(60, 20);
			this.textSiteId.TabIndex = 14;
			//
			// labelProductName
			//
			this.labelProductName.Location = new System.Drawing.Point(6, 167);
			this.labelProductName.Name = "labelProductName";
			this.labelProductName.Size = new System.Drawing.Size(120, 20);
			this.labelProductName.TabIndex = 15;
			this.labelProductName.Text = "Product Name";
			this.labelProductName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textProductName
			//
			this.textProductName.Location = new System.Drawing.Point(130, 167);
			this.textProductName.Name = "textProductName";
			this.textProductName.Size = new System.Drawing.Size(200, 20);
			this.textProductName.TabIndex = 16;
			//
			// label7
			//
			this.label7.Location = new System.Drawing.Point(6, 193);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(120, 20);
			this.label7.TabIndex = 17;
			this.label7.Text = "Previous Account ID";
			this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textNewCropAccountID
			//
			this.textNewCropAccountID.Location = new System.Drawing.Point(130, 193);
			this.textNewCropAccountID.Name = "textNewCropAccountID";
			this.textNewCropAccountID.ReadOnly = true;
			this.textNewCropAccountID.Size = new System.Drawing.Size(200, 20);
			this.textNewCropAccountID.TabIndex = 18;
			//
			// butNewCropTest
			//
			this.butNewCropTest.Location = new System.Drawing.Point(130, 223);
			this.butNewCropTest.Name = "butNewCropTest";
			this.butNewCropTest.Size = new System.Drawing.Size(110, 24);
			this.butNewCropTest.TabIndex = 19;
			this.butNewCropTest.Text = "Test Connection";
			this.butNewCropTest.Click += new System.EventHandler(this.butNewCropTest_Click);
			//
			// gridNewCropProviders
			//
			this.gridNewCropProviders.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridNewCropProviders.Location = new System.Drawing.Point(482, 10);
			this.gridNewCropProviders.Name = "gridNewCropProviders";
			this.gridNewCropProviders.Size = new System.Drawing.Size(260, 405);
			this.gridNewCropProviders.TabIndex = 20;
			this.gridNewCropProviders.Title = "Providers Enabled for NewCrop";
			this.gridNewCropProviders.TranslationName = "GridNewCropProviders";
			this.gridNewCropProviders.CellDoubleClick += new OpenDental.UI.ODGridClickEventHandler(this.gridNewCropProviders_CellDoubleClick);
			//
			// FormErxSetup
			//
			this.ClientSize = new System.Drawing.Size(780, 602);
			this.Controls.Add(this.tabControlVendors);
			this.Controls.Add(this.labelInfo);
			this.Controls.Add(this.groupErxOptions);
			this.Controls.Add(this.checkEnabled);
			this.Controls.Add(this.butSave);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Name = "FormErxSetup";
			this.Text = "eRx Setup";
			this.Load += new System.EventHandler(this.FormErxSetup_Load);
			this.groupErxOptions.ResumeLayout(false);
			this.tabPageDoseSpot.ResumeLayout(false);
			this.tabPageDoseSpot.PerformLayout();
			this.tabPageNewCrop.ResumeLayout(false);
			this.tabPageNewCrop.PerformLayout();
			this.tabControlVendors.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private OpenDental.UI.Button butSave;
		private OpenDental.UI.CheckBox checkEnabled;
		private OpenDental.UI.GroupBox groupErxOptions;
		private System.Windows.Forms.RadioButton radioDoseSpot;
		private System.Windows.Forms.RadioButton radioNewCrop;
		private System.Windows.Forms.RadioButton radioDoseSpotLegacy;
		private System.Windows.Forms.Label labelInfo;
		private OpenDental.UI.TabControl tabControlVendors;
		private OpenDental.UI.TabPage tabPageDoseSpot;
		private System.Windows.Forms.Label labelDoseSpotEnvironment;
		private System.Windows.Forms.ComboBox comboDoseSpotEnvironment;
		private System.Windows.Forms.Label labelSubscriptionKey;
		private System.Windows.Forms.TextBox textSubscriptionKey;
		private OpenDental.UI.CheckBox checkShowSubscriptionKey;
		private OpenDental.UI.CheckBox checkShowHiddenClinics;
		private UI.GridOD gridProperties;
		private UI.Button butDelete;
		private UI.Button butDoseSpotTest;
		private OpenDental.UI.TabPage tabPageNewCrop;
		private System.Windows.Forms.Label labelNewCropEnvironment;
		private System.Windows.Forms.ComboBox comboNewCropEnvironment;
		private System.Windows.Forms.Label labelPartnerName;
		private System.Windows.Forms.TextBox textPartnerName;
		private System.Windows.Forms.Label labelAccountName;
		private System.Windows.Forms.TextBox textAccountName;
		private System.Windows.Forms.Label labelPassword;
		private System.Windows.Forms.TextBox textPassword;
		private OpenDental.UI.CheckBox checkShowPassword;
		private System.Windows.Forms.Label labelAccountId;
		private System.Windows.Forms.TextBox textAccountId;
		private UI.Button butGenerateAccountId;
		private UI.Button butClearAccountId;
		private System.Windows.Forms.Label labelSiteId;
		private System.Windows.Forms.TextBox textSiteId;
		private System.Windows.Forms.Label labelProductName;
		private System.Windows.Forms.TextBox textProductName;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.TextBox textNewCropAccountID;
		private UI.Button butNewCropTest;
		private UI.GridOD gridNewCropProviders;
	}
}
