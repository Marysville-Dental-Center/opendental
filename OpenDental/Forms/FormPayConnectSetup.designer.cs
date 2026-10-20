using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenDental {
	public partial class FormPayConnectSetup {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing) {
			if(disposing) {
				if(components != null) {
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormPayConnectSetup));
			this.checkEnabled = new OpenDental.UI.CheckBox();
			this.label1 = new System.Windows.Forms.Label();
			this.comboPaymentType = new OpenDental.UI.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			this.comboClinic = new OpenDental.UI.ComboBox();
			this.labelClinic = new System.Windows.Forms.Label();
			this.groupPaySettings = new OpenDental.UI.GroupBox();
			this.butMerchantInfo = new OpenDental.UI.Button();
			this.checkSurcharge = new OpenDental.UI.CheckBox();
			this.textAPISecret = new System.Windows.Forms.TextBox();
			this.groupBox1 = new OpenDental.UI.GroupBox();
			this.checkPatientPortalPayEnabled = new OpenDental.UI.CheckBox();
			this.checkPreventSavingNewCC = new OpenDental.UI.CheckBox();
			this.comboDefaultProcessing = new OpenDental.UI.ComboBox();
			this.labelDefaultProcMethod = new System.Windows.Forms.Label();
			this.checkTerminal = new OpenDental.UI.CheckBox();
			this.labelClinicEnable = new System.Windows.Forms.Label();
			this.butErrorLog = new OpenDental.UI.Button();
			this.butSave = new OpenDental.UI.Button();
			this.groupBoxTerminals = new OpenDental.UI.GroupBox();
			this.butAddTerminal = new OpenDental.UI.Button();
			this.gridODTerminals = new OpenDental.UI.GridOD();
			this.butImportTerminals = new OpenDental.UI.Button();
			this.groupPracticeWide = new OpenDental.UI.GroupBox();
			this.labelApiKey = new System.Windows.Forms.Label();
			this.textAPIKey = new System.Windows.Forms.TextBox();
			this.checkUseSandbox = new OpenDental.UI.CheckBox();
			this.labelPracticeWide = new System.Windows.Forms.Label();
			this.groupPaySettings.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.groupBoxTerminals.SuspendLayout();
			this.groupPracticeWide.SuspendLayout();
			this.SuspendLayout();
			// 
			// checkEnabled
			// 
			this.checkEnabled.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkEnabled.Location = new System.Drawing.Point(97, 34);
			this.checkEnabled.Name = "checkEnabled";
			this.checkEnabled.Size = new System.Drawing.Size(226, 18);
			this.checkEnabled.TabIndex = 1;
			this.checkEnabled.Text = "Enabled (affects all clinics)";
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(432, 22);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(124, 16);
			this.label1.TabIndex = 0;
			this.label1.Text = "Payment Type";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// comboPaymentType
			// 
			this.comboPaymentType.Location = new System.Drawing.Point(557, 19);
			this.comboPaymentType.Name = "comboPaymentType";
			this.comboPaymentType.Size = new System.Drawing.Size(175, 21);
			this.comboPaymentType.TabIndex = 0;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(129, 23);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(88, 16);
			this.label2.TabIndex = 0;
			this.label2.Text = "Secret";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// comboClinic
			// 
			this.comboClinic.Location = new System.Drawing.Point(148, 89);
			this.comboClinic.Name = "comboClinic";
			this.comboClinic.Size = new System.Drawing.Size(175, 21);
			this.comboClinic.TabIndex = 3;
			this.comboClinic.SelectionChangeCommitted += new System.EventHandler(this.comboClinic_SelectionChangeCommitted);
			// 
			// labelClinic
			// 
			this.labelClinic.Location = new System.Drawing.Point(69, 89);
			this.labelClinic.Name = "labelClinic";
			this.labelClinic.Size = new System.Drawing.Size(73, 16);
			this.labelClinic.TabIndex = 0;
			this.labelClinic.Text = "Clinic";
			this.labelClinic.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// groupPaySettings
			// 
			this.groupPaySettings.Controls.Add(this.butMerchantInfo);
			this.groupPaySettings.Controls.Add(this.checkSurcharge);
			this.groupPaySettings.Controls.Add(this.textAPISecret);
			this.groupPaySettings.Controls.Add(this.groupBox1);
			this.groupPaySettings.Controls.Add(this.checkPreventSavingNewCC);
			this.groupPaySettings.Controls.Add(this.comboDefaultProcessing);
			this.groupPaySettings.Controls.Add(this.labelDefaultProcMethod);
			this.groupPaySettings.Controls.Add(this.checkTerminal);
			this.groupPaySettings.Controls.Add(this.label2);
			this.groupPaySettings.Controls.Add(this.comboPaymentType);
			this.groupPaySettings.Controls.Add(this.label1);
			this.groupPaySettings.Location = new System.Drawing.Point(9, 122);
			this.groupPaySettings.Name = "groupPaySettings";
			this.groupPaySettings.Size = new System.Drawing.Size(754, 173);
			this.groupPaySettings.TabIndex = 4;
			this.groupPaySettings.Text = "Clinic Payment Settings";
			// 
			// butMerchantInfo
			// 
			this.butMerchantInfo.Location = new System.Drawing.Point(21, 97);
			this.butMerchantInfo.Name = "butMerchantInfo";
			this.butMerchantInfo.Size = new System.Drawing.Size(106, 24);
			this.butMerchantInfo.TabIndex = 13;
			this.butMerchantInfo.Text = "Test connection";
			this.butMerchantInfo.UseVisualStyleBackColor = true;
			this.butMerchantInfo.Click += new System.EventHandler(this.butMerchantInfo_Click);
			// 
			// checkSurcharge
			// 
			this.checkSurcharge.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkSurcharge.Location = new System.Drawing.Point(243, 141);
			this.checkSurcharge.Name = "checkSurcharge";
			this.checkSurcharge.Size = new System.Drawing.Size(150, 18);
			this.checkSurcharge.TabIndex = 12;
			this.checkSurcharge.Text = "Surcharge account";
			// 
			// textAPISecret
			// 
			this.textAPISecret.Location = new System.Drawing.Point(218, 21);
			this.textAPISecret.Name = "textAPISecret";
			this.textAPISecret.Size = new System.Drawing.Size(175, 20);
			this.textAPISecret.TabIndex = 11;
			this.textAPISecret.UseSystemPasswordChar = true;
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.checkPatientPortalPayEnabled);
			this.groupBox1.Location = new System.Drawing.Point(435, 82);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(307, 74);
			this.groupBox1.TabIndex = 9;
			this.groupBox1.Text = "Payment Portal Payments";
			// 
			// checkPatientPortalPayEnabled
			// 
			this.checkPatientPortalPayEnabled.Location = new System.Drawing.Point(106, 19);
			this.checkPatientPortalPayEnabled.Name = "checkPatientPortalPayEnabled";
			this.checkPatientPortalPayEnabled.Size = new System.Drawing.Size(127, 17);
			this.checkPatientPortalPayEnabled.TabIndex = 9;
			this.checkPatientPortalPayEnabled.Text = "Enabled";
			this.checkPatientPortalPayEnabled.Click += new System.EventHandler(this.checkPatientPortalPayEnabled_Click);
			// 
			// checkPreventSavingNewCC
			// 
			this.checkPreventSavingNewCC.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkPreventSavingNewCC.Location = new System.Drawing.Point(175, 119);
			this.checkPreventSavingNewCC.Name = "checkPreventSavingNewCC";
			this.checkPreventSavingNewCC.Size = new System.Drawing.Size(218, 17);
			this.checkPreventSavingNewCC.TabIndex = 8;
			this.checkPreventSavingNewCC.Text = "Prevent saving new cards";
			// 
			// comboDefaultProcessing
			// 
			this.comboDefaultProcessing.Location = new System.Drawing.Point(557, 46);
			this.comboDefaultProcessing.Name = "comboDefaultProcessing";
			this.comboDefaultProcessing.Size = new System.Drawing.Size(175, 21);
			this.comboDefaultProcessing.TabIndex = 6;
			// 
			// labelDefaultProcMethod
			// 
			this.labelDefaultProcMethod.Location = new System.Drawing.Point(432, 43);
			this.labelDefaultProcMethod.Name = "labelDefaultProcMethod";
			this.labelDefaultProcMethod.Size = new System.Drawing.Size(124, 26);
			this.labelDefaultProcMethod.TabIndex = 7;
			this.labelDefaultProcMethod.Text = "Default Processing Method";
			this.labelDefaultProcMethod.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// checkTerminal
			// 
			this.checkTerminal.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkTerminal.Location = new System.Drawing.Point(175, 74);
			this.checkTerminal.Name = "checkTerminal";
			this.checkTerminal.Size = new System.Drawing.Size(218, 17);
			this.checkTerminal.TabIndex = 3;
			this.checkTerminal.Text = "Enable terminal processing";
			// 
			// labelClinicEnable
			// 
			this.labelClinicEnable.Location = new System.Drawing.Point(77, 55);
			this.labelClinicEnable.Name = "labelClinicEnable";
			this.labelClinicEnable.Size = new System.Drawing.Size(246, 28);
			this.labelClinicEnable.TabIndex = 2;
			this.labelClinicEnable.Text = "To enable PayConnect for a clinic, set the credentials for that clinic.";
			this.labelClinicEnable.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(688, 670);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 26);
			this.butSave.TabIndex = 6;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// groupBoxTerminals
			// 
			this.groupBoxTerminals.Controls.Add(this.butImportTerminals);
			this.groupBoxTerminals.Controls.Add(this.butAddTerminal);
			this.groupBoxTerminals.Controls.Add(this.gridODTerminals);
			this.groupBoxTerminals.Location = new System.Drawing.Point(9, 302);
			this.groupBoxTerminals.Name = "groupBoxTerminals";
			this.groupBoxTerminals.Size = new System.Drawing.Size(754, 355);
			this.groupBoxTerminals.TabIndex = 8;
			this.groupBoxTerminals.Text = "Terminals";
			// 
			// butAddTerminal
			// 
			this.butAddTerminal.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butAddTerminal.Icon = OpenDental.UI.EnumIcons.Add;
			this.butAddTerminal.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butAddTerminal.Location = new System.Drawing.Point(632, 318);
			this.butAddTerminal.Name = "butAddTerminal";
			this.butAddTerminal.Size = new System.Drawing.Size(110, 24);
			this.butAddTerminal.TabIndex = 152;
			this.butAddTerminal.Text = "Add Terminal";
			this.butAddTerminal.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
			this.butAddTerminal.Click += new System.EventHandler(this.butAddTerminal_Click);
			// 
			// gridODTerminals
			// 
			this.gridODTerminals.Location = new System.Drawing.Point(9, 21);
			this.gridODTerminals.Name = "gridODTerminals";
			this.gridODTerminals.Size = new System.Drawing.Size(733, 291);
			this.gridODTerminals.TabIndex = 0;
			this.gridODTerminals.Title = "Terminals";
			this.gridODTerminals.TranslationName = "table devices";
			this.gridODTerminals.CellDoubleClick += new OpenDental.UI.ODGridClickEventHandler(this.gridODTerminals_CellDoubleClick);
			// 
			// butImportTerminals
			// 
			this.butImportTerminals.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butImportTerminals.Location = new System.Drawing.Point(496, 318);
			this.butImportTerminals.Name = "butImportTerminals";
			this.butImportTerminals.Size = new System.Drawing.Size(130, 24);
			this.butImportTerminals.TabIndex = 153;
			this.butImportTerminals.Text = "Import Terminals";
			this.butImportTerminals.Click += new System.EventHandler(this.butImportTerminals_Click);
			// 
			// groupPracticeWide
			// 
			this.groupPracticeWide.Controls.Add(this.labelPracticeWide);
			this.groupPracticeWide.Controls.Add(this.checkUseSandbox);
			this.groupPracticeWide.Controls.Add(this.textAPIKey);
			this.groupPracticeWide.Controls.Add(this.labelApiKey);
			this.groupPracticeWide.Location = new System.Drawing.Point(344, 6);
			this.groupPracticeWide.Name = "groupPracticeWide";
			this.groupPracticeWide.Size = new System.Drawing.Size(419, 110);
			this.groupPracticeWide.TabIndex = 7;
			this.groupPracticeWide.Text = "Version 2.0, all clinics";
			// 
			// labelApiKey
			// 
			this.labelApiKey.Location = new System.Drawing.Point(6, 23);
			this.labelApiKey.Name = "labelApiKey";
			this.labelApiKey.Size = new System.Drawing.Size(120, 16);
			this.labelApiKey.TabIndex = 0;
			this.labelApiKey.Text = "API key";
			this.labelApiKey.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textAPIKey
			// 
			this.textAPIKey.Location = new System.Drawing.Point(127, 21);
			this.textAPIKey.Name = "textAPIKey";
			this.textAPIKey.Size = new System.Drawing.Size(280, 20);
			this.textAPIKey.TabIndex = 1;
			this.textAPIKey.UseSystemPasswordChar = true;
			// 
			// checkUseSandbox
			// 
			this.checkUseSandbox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkUseSandbox.Location = new System.Drawing.Point(6, 46);
			this.checkUseSandbox.Name = "checkUseSandbox";
			this.checkUseSandbox.Size = new System.Drawing.Size(135, 18);
			this.checkUseSandbox.TabIndex = 2;
			this.checkUseSandbox.Text = "Use sandbox";
			// 
			// labelPracticeWide
			// 
			this.labelPracticeWide.Location = new System.Drawing.Point(6, 68);
			this.labelPracticeWide.Name = "labelPracticeWide";
			this.labelPracticeWide.Size = new System.Drawing.Size(401, 36);
			this.labelPracticeWide.TabIndex = 3;
			this.labelPracticeWide.Text = "The API key and the sandbox setting apply to every clinic. Each clinic enters its own Secret below. In the sandbox, transactions are tests and nothing is charged.";
			// 
			// butErrorLog
			// 
			this.butErrorLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butErrorLog.Location = new System.Drawing.Point(9, 670);
			this.butErrorLog.Name = "butErrorLog";
			this.butErrorLog.Size = new System.Drawing.Size(110, 26);
			this.butErrorLog.TabIndex = 20;
			this.butErrorLog.Text = "Error Log";
			this.butErrorLog.Click += new System.EventHandler(this.butErrorLog_Click);
			// 
			// FormPayConnectSetup
			// 
			this.ClientSize = new System.Drawing.Size(776, 708);
			this.Controls.Add(this.butErrorLog);
			this.Controls.Add(this.groupBoxTerminals);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.labelClinicEnable);
			this.Controls.Add(this.groupPaySettings);
			this.Controls.Add(this.comboClinic);
			this.Controls.Add(this.labelClinic);
			this.Controls.Add(this.checkEnabled);
			this.Controls.Add(this.groupPracticeWide);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormPayConnectSetup";
			this.ShowInTaskbar = false;
			this.Text = "PayConnect Setup";
			this.Load += new System.EventHandler(this.FormPayConnectSetup_Load);
			this.groupPaySettings.ResumeLayout(false);
			this.groupPaySettings.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupBoxTerminals.ResumeLayout(false);
			this.groupPracticeWide.ResumeLayout(false);
			this.groupPracticeWide.PerformLayout();
			this.ResumeLayout(false);

		}
		#endregion
		private UI.Button butErrorLog;
		private OpenDental.UI.Button butSave;
		private OpenDental.UI.CheckBox checkEnabled;
		private Label label1;
		private OpenDental.UI.ComboBox comboPaymentType;
		private Label label2;
		private OpenDental.UI.ComboBox comboClinic;
		private Label labelClinic;
		private OpenDental.UI.GroupBox groupPaySettings;
		private Label labelClinicEnable;
		private OpenDental.UI.CheckBox checkTerminal;
		private OpenDental.UI.ComboBox comboDefaultProcessing;
		private Label labelDefaultProcMethod;
		private OpenDental.UI.CheckBox checkPreventSavingNewCC;
		private OpenDental.UI.GroupBox groupBox1;
		private OpenDental.UI.CheckBox checkPatientPortalPayEnabled;
		private UI.GroupBox groupBoxTerminals;
		private UI.GridOD gridODTerminals;
		private TextBox textAPISecret;
		private UI.CheckBox checkSurcharge;
		private UI.Button butAddTerminal;
		private UI.Button butMerchantInfo;
		private UI.Button butImportTerminals;
		private UI.GroupBox groupPracticeWide;
		private Label labelApiKey;
		private TextBox textAPIKey;
		private UI.CheckBox checkUseSandbox;
		private Label labelPracticeWide;
	}
}
