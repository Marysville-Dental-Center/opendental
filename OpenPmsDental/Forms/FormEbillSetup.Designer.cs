namespace OpenPmsDental{
	partial class FormEbillSetup {
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
			this.groupBoxBilling = new OpenPmsDental.UI.GroupBox();
			this.checkIncludeClinicNum = new OpenPmsDental.UI.CheckBox();
			this.checkIncludeAdjust = new OpenPmsDental.UI.CheckBox();
			this.checkCreatePDF = new OpenPmsDental.UI.CheckBox();
			this.labelBlankForDefault = new System.Windows.Forms.Label();
			this.textStatementURL = new System.Windows.Forms.TextBox();
			this.labelStatementURL = new System.Windows.Forms.Label();
			this.comboPracticeAddr = new OpenPmsDental.UI.ComboBox();
			this.comboRemitAddr = new OpenPmsDental.UI.ComboBox();
			this.labelPracticeAddr = new System.Windows.Forms.Label();
			this.labelRemitAddr = new System.Windows.Forms.Label();
			this.comboClinic = new OpenPmsDental.UI.ComboBoxClinicPicker();
			this.listElectBilling = new OpenPmsDental.UI.ListBox();
			this.textPassword = new System.Windows.Forms.TextBox();
			this.labelPassword = new System.Windows.Forms.Label();
			this.textUserName = new System.Windows.Forms.TextBox();
			this.labelUserName = new System.Windows.Forms.Label();
			this.groupBox2 = new OpenPmsDental.UI.GroupBox();
			this.checkAmEx = new OpenPmsDental.UI.CheckBox();
			this.checkD = new OpenPmsDental.UI.CheckBox();
			this.checkV = new OpenPmsDental.UI.CheckBox();
			this.checkMC = new OpenPmsDental.UI.CheckBox();
			this.textClientAcctNumber = new System.Windows.Forms.TextBox();
			this.labelAcctNum = new System.Windows.Forms.Label();
			this.textVendorPMScode = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.textVendorId = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.butSave = new OpenPmsDental.UI.Button();
			this.groupBoxBilling.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.SuspendLayout();
			// 
			// groupBoxBilling
			// 
			this.groupBoxBilling.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBoxBilling.Controls.Add(this.checkIncludeClinicNum);
			this.groupBoxBilling.Controls.Add(this.checkIncludeAdjust);
			this.groupBoxBilling.Controls.Add(this.checkCreatePDF);
			this.groupBoxBilling.Controls.Add(this.labelBlankForDefault);
			this.groupBoxBilling.Controls.Add(this.textStatementURL);
			this.groupBoxBilling.Controls.Add(this.labelStatementURL);
			this.groupBoxBilling.Controls.Add(this.comboPracticeAddr);
			this.groupBoxBilling.Controls.Add(this.comboRemitAddr);
			this.groupBoxBilling.Controls.Add(this.labelPracticeAddr);
			this.groupBoxBilling.Controls.Add(this.labelRemitAddr);
			this.groupBoxBilling.Controls.Add(this.comboClinic);
			this.groupBoxBilling.Controls.Add(this.listElectBilling);
			this.groupBoxBilling.Controls.Add(this.textPassword);
			this.groupBoxBilling.Controls.Add(this.labelPassword);
			this.groupBoxBilling.Controls.Add(this.textUserName);
			this.groupBoxBilling.Controls.Add(this.labelUserName);
			this.groupBoxBilling.Controls.Add(this.groupBox2);
			this.groupBoxBilling.Controls.Add(this.textClientAcctNumber);
			this.groupBoxBilling.Controls.Add(this.labelAcctNum);
			this.groupBoxBilling.Controls.Add(this.textVendorPMScode);
			this.groupBoxBilling.Controls.Add(this.label3);
			this.groupBoxBilling.Controls.Add(this.textVendorId);
			this.groupBoxBilling.Controls.Add(this.label2);
			this.groupBoxBilling.Location = new System.Drawing.Point(12, 12);
			this.groupBoxBilling.Name = "groupBoxBilling";
			this.groupBoxBilling.Size = new System.Drawing.Size(850, 140);
			this.groupBoxBilling.TabIndex = 0;
			this.groupBoxBilling.Text = "Electronic Billing";
			// 
			// checkIncludeClinicNum
			// 
			this.checkIncludeClinicNum.Location = new System.Drawing.Point(12, 113);
			this.checkIncludeClinicNum.Name = "checkIncludeClinicNum";
			this.checkIncludeClinicNum.Size = new System.Drawing.Size(270, 18);
			this.checkIncludeClinicNum.TabIndex = 265;
			this.checkIncludeClinicNum.Text = "Include Clinic Number";
			this.checkIncludeClinicNum.Visible = false;
			// 
			// checkIncludeAdjust
			// 
			this.checkIncludeAdjust.Location = new System.Drawing.Point(12, 94);
			this.checkIncludeAdjust.Name = "checkIncludeAdjust";
			this.checkIncludeAdjust.Size = new System.Drawing.Size(270, 18);
			this.checkIncludeAdjust.TabIndex = 264;
			this.checkIncludeAdjust.Text = "Include \'Adjust\' in the description for adjustments";
			// 
			// checkCreatePDF
			// 
			this.checkCreatePDF.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkCreatePDF.Enabled = false;
			this.checkCreatePDF.Location = new System.Drawing.Point(576, 97);
			this.checkCreatePDF.Name = "checkCreatePDF";
			this.checkCreatePDF.Size = new System.Drawing.Size(112, 16);
			this.checkCreatePDF.TabIndex = 4;
			this.checkCreatePDF.Text = "Generate PDF";
			// 
			// labelBlankForDefault
			// 
			this.labelBlankForDefault.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.labelBlankForDefault.Location = new System.Drawing.Point(694, 117);
			this.labelBlankForDefault.Name = "labelBlankForDefault";
			this.labelBlankForDefault.Size = new System.Drawing.Size(99, 16);
			this.labelBlankForDefault.TabIndex = 263;
			this.labelBlankForDefault.Text = "(blank for default)";
			this.labelBlankForDefault.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.labelBlankForDefault.Visible = false;
			// 
			// textStatementURL
			// 
			this.textStatementURL.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textStatementURL.Location = new System.Drawing.Point(417, 113);
			this.textStatementURL.Name = "textStatementURL";
			this.textStatementURL.Size = new System.Drawing.Size(271, 20);
			this.textStatementURL.TabIndex = 261;
			this.textStatementURL.Text = "https://prelive.dentalxchange.com/dci/upload.svl";
			this.textStatementURL.KeyUp += new System.Windows.Forms.KeyEventHandler(this.textStatementURL_KeyUp);
			// 
			// labelStatementURL
			// 
			this.labelStatementURL.Location = new System.Drawing.Point(315, 114);
			this.labelStatementURL.Name = "labelStatementURL";
			this.labelStatementURL.Size = new System.Drawing.Size(101, 16);
			this.labelStatementURL.TabIndex = 262;
			this.labelStatementURL.Text = "URL Override";
			this.labelStatementURL.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// comboPracticeAddr
			// 
			this.comboPracticeAddr.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.comboPracticeAddr.Location = new System.Drawing.Point(689, 14);
			this.comboPracticeAddr.Name = "comboPracticeAddr";
			this.comboPracticeAddr.Size = new System.Drawing.Size(150, 21);
			this.comboPracticeAddr.TabIndex = 260;
			// 
			// comboRemitAddr
			// 
			this.comboRemitAddr.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.comboRemitAddr.Enabled = false;
			this.comboRemitAddr.Location = new System.Drawing.Point(689, 36);
			this.comboRemitAddr.Name = "comboRemitAddr";
			this.comboRemitAddr.Size = new System.Drawing.Size(150, 21);
			this.comboRemitAddr.TabIndex = 259;
			// 
			// labelPracticeAddr
			// 
			this.labelPracticeAddr.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.labelPracticeAddr.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.labelPracticeAddr.Location = new System.Drawing.Point(582, 16);
			this.labelPracticeAddr.Name = "labelPracticeAddr";
			this.labelPracticeAddr.Size = new System.Drawing.Size(106, 16);
			this.labelPracticeAddr.TabIndex = 258;
			this.labelPracticeAddr.Text = "Practice Address";
			this.labelPracticeAddr.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// labelRemitAddr
			// 
			this.labelRemitAddr.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.labelRemitAddr.Location = new System.Drawing.Point(582, 38);
			this.labelRemitAddr.Name = "labelRemitAddr";
			this.labelRemitAddr.Size = new System.Drawing.Size(106, 16);
			this.labelRemitAddr.TabIndex = 257;
			this.labelRemitAddr.Text = "Remit Address";
			this.labelRemitAddr.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// comboClinic
			// 
			this.comboClinic.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.comboClinic.HqDescription = "Unassigned/Default";
			this.comboClinic.IncludeUnassigned = true;
			this.comboClinic.Location = new System.Drawing.Point(652, 58);
			this.comboClinic.Name = "comboClinic";
			this.comboClinic.Size = new System.Drawing.Size(187, 21);
			this.comboClinic.TabIndex = 255;
			this.comboClinic.SelectionChangeCommitted += new System.EventHandler(this.comboClinic_SelectionChangeCommitted);
			// 
			// listElectBilling
			// 
			this.listElectBilling.Location = new System.Drawing.Point(12, 19);
			this.listElectBilling.Name = "listElectBilling";
			this.listElectBilling.Size = new System.Drawing.Size(120, 69);
			this.listElectBilling.TabIndex = 0;
			this.listElectBilling.SelectedIndexChanged += new System.EventHandler(this.listElectBilling_SelectedIndexChanged);
			// 
			// textPassword
			// 
			this.textPassword.Location = new System.Drawing.Point(417, 93);
			this.textPassword.Name = "textPassword";
			this.textPassword.PasswordChar = '*';
			this.textPassword.Size = new System.Drawing.Size(100, 20);
			this.textPassword.TabIndex = 5;
			// 
			// labelPassword
			// 
			this.labelPassword.Location = new System.Drawing.Point(285, 95);
			this.labelPassword.Name = "labelPassword";
			this.labelPassword.Size = new System.Drawing.Size(130, 16);
			this.labelPassword.TabIndex = 254;
			this.labelPassword.Text = "Password";
			this.labelPassword.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textUserName
			// 
			this.textUserName.Location = new System.Drawing.Point(417, 73);
			this.textUserName.Name = "textUserName";
			this.textUserName.Size = new System.Drawing.Size(100, 20);
			this.textUserName.TabIndex = 4;
			// 
			// labelUserName
			// 
			this.labelUserName.Location = new System.Drawing.Point(285, 75);
			this.labelUserName.Name = "labelUserName";
			this.labelUserName.Size = new System.Drawing.Size(130, 16);
			this.labelUserName.TabIndex = 252;
			this.labelUserName.Text = "User Name";
			this.labelUserName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.checkAmEx);
			this.groupBox2.Controls.Add(this.checkD);
			this.groupBox2.Controls.Add(this.checkV);
			this.groupBox2.Controls.Add(this.checkMC);
			this.groupBox2.Location = new System.Drawing.Point(138, 14);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(144, 76);
			this.groupBox2.TabIndex = 251;
			this.groupBox2.Text = "Credit Card Choices";
			// 
			// checkAmEx
			// 
			this.checkAmEx.Location = new System.Drawing.Point(9, 58);
			this.checkAmEx.Name = "checkAmEx";
			this.checkAmEx.Size = new System.Drawing.Size(132, 16);
			this.checkAmEx.TabIndex = 3;
			this.checkAmEx.Text = "American Express";
			// 
			// checkD
			// 
			this.checkD.Location = new System.Drawing.Point(9, 43);
			this.checkD.Name = "checkD";
			this.checkD.Size = new System.Drawing.Size(132, 16);
			this.checkD.TabIndex = 2;
			this.checkD.Text = "Discover";
			// 
			// checkV
			// 
			this.checkV.Location = new System.Drawing.Point(9, 28);
			this.checkV.Name = "checkV";
			this.checkV.Size = new System.Drawing.Size(132, 16);
			this.checkV.TabIndex = 1;
			this.checkV.Text = "Visa";
			// 
			// checkMC
			// 
			this.checkMC.Location = new System.Drawing.Point(9, 13);
			this.checkMC.Name = "checkMC";
			this.checkMC.Size = new System.Drawing.Size(132, 16);
			this.checkMC.TabIndex = 0;
			this.checkMC.Text = "Master Card";
			// 
			// textClientAcctNumber
			// 
			this.textClientAcctNumber.Location = new System.Drawing.Point(417, 53);
			this.textClientAcctNumber.Name = "textClientAcctNumber";
			this.textClientAcctNumber.Size = new System.Drawing.Size(100, 20);
			this.textClientAcctNumber.TabIndex = 3;
			// 
			// labelAcctNum
			// 
			this.labelAcctNum.Location = new System.Drawing.Point(285, 55);
			this.labelAcctNum.Name = "labelAcctNum";
			this.labelAcctNum.Size = new System.Drawing.Size(130, 16);
			this.labelAcctNum.TabIndex = 249;
			this.labelAcctNum.Text = "Account Number";
			this.labelAcctNum.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textVendorPMScode
			// 
			this.textVendorPMScode.Location = new System.Drawing.Point(417, 33);
			this.textVendorPMScode.Name = "textVendorPMScode";
			this.textVendorPMScode.Size = new System.Drawing.Size(100, 20);
			this.textVendorPMScode.TabIndex = 2;
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(285, 35);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(130, 16);
			this.label3.TabIndex = 247;
			this.label3.Text = "Vendor PMS Code";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textVendorId
			// 
			this.textVendorId.Location = new System.Drawing.Point(417, 13);
			this.textVendorId.Name = "textVendorId";
			this.textVendorId.Size = new System.Drawing.Size(100, 20);
			this.textVendorId.TabIndex = 1;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(285, 15);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(130, 16);
			this.label2.TabIndex = 245;
			this.label2.Text = "Vendor ID";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(787, 160);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 1;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// FormEbillSetup
			// 
			this.ClientSize = new System.Drawing.Size(874, 196);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.groupBoxBilling);
			this.Name = "FormEbillSetup";
			this.Text = "Electronic Billing";
			this.Load += new System.EventHandler(this.FormEbillSetup_Load);
			this.groupBoxBilling.ResumeLayout(false);
			this.groupBoxBilling.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private OpenPmsDental.UI.GroupBox groupBoxBilling;
		private OpenPmsDental.UI.CheckBox checkIncludeClinicNum;
		private OpenPmsDental.UI.CheckBox checkIncludeAdjust;
		private OpenPmsDental.UI.CheckBox checkCreatePDF;
		private System.Windows.Forms.Label labelBlankForDefault;
		private System.Windows.Forms.TextBox textStatementURL;
		private System.Windows.Forms.Label labelStatementURL;
		private OpenPmsDental.UI.ComboBox comboPracticeAddr;
		private OpenPmsDental.UI.ComboBox comboRemitAddr;
		private System.Windows.Forms.Label labelPracticeAddr;
		private System.Windows.Forms.Label labelRemitAddr;
		private OpenPmsDental.UI.ComboBoxClinicPicker comboClinic;
		private OpenPmsDental.UI.ListBox listElectBilling;
		private System.Windows.Forms.TextBox textPassword;
		private System.Windows.Forms.Label labelPassword;
		private System.Windows.Forms.TextBox textUserName;
		private System.Windows.Forms.Label labelUserName;
		private OpenPmsDental.UI.GroupBox groupBox2;
		private OpenPmsDental.UI.CheckBox checkAmEx;
		private OpenPmsDental.UI.CheckBox checkD;
		private OpenPmsDental.UI.CheckBox checkV;
		private OpenPmsDental.UI.CheckBox checkMC;
		private System.Windows.Forms.TextBox textClientAcctNumber;
		private System.Windows.Forms.Label labelAcctNum;
		private System.Windows.Forms.TextBox textVendorPMScode;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox textVendorId;
		private System.Windows.Forms.Label label2;
		private OpenPmsDental.UI.Button butSave;
	}
}
