namespace OpenPmsDental{
	partial class FormBillingDefaults {
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormBillingDefaults));
			this.butSave = new OpenPmsDental.UI.Button();
			this.butEbill = new OpenPmsDental.UI.Button();
			this.butSendSetup = new OpenPmsDental.UI.Button();
			this.comboClinic = new OpenPmsDental.UI.ComboBoxClinicPicker();
			this.checkUsePracticeDefaults = new OpenPmsDental.UI.CheckBox();
			this.groupStatements = new OpenPmsDental.UI.GroupBox();
			this.checkIntermingled = new OpenPmsDental.UI.CheckBox();
			this.checkSinglePatient = new OpenPmsDental.UI.CheckBox();
			this.labelStartDate = new System.Windows.Forms.Label();
			this.textDays = new OpenPmsDental.ValidNum();
			this.label1 = new System.Windows.Forms.Label();
			this.checkBoxBillShowTransSinceZero = new OpenPmsDental.UI.CheckBox();
			this.checkInserterBarcode = new OpenPmsDental.UI.CheckBox();
			this.groupMessages = new OpenPmsDental.UI.GroupBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textNote = new OpenPmsDental.ODtextBox();
			this.groupBox3 = new OpenPmsDental.UI.GroupBox();
			this.label10 = new System.Windows.Forms.Label();
			this.label9 = new System.Windows.Forms.Label();
			this.textBillingEmailSubject = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBillingEmailBody = new OpenPmsDental.ODtextBox();
			this.checkBillingEmailIncludeAutograph = new OpenPmsDental.UI.CheckBox();
			this.labelBillingEmailType = new System.Windows.Forms.Label();
			this.comboBillingEmailType = new OpenPmsDental.UI.ComboBox();
			this.butBillingEmailEditHtml = new OpenPmsDental.UI.Button();
			this.label11 = new System.Windows.Forms.Label();
			this.textInvoiceNote = new OpenPmsDental.ODtextBox();
			this.groupStatements.SuspendLayout();
			this.groupMessages.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.SuspendLayout();
			//
			// butSave
			//
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(790, 568);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 5;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// butEbill
			//
			this.butEbill.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butEbill.Location = new System.Drawing.Point(12, 568);
			this.butEbill.Name = "butEbill";
			this.butEbill.Size = new System.Drawing.Size(130, 24);
			this.butEbill.TabIndex = 4;
			this.butEbill.Text = "Electronic Billing...";
			this.butEbill.Click += new System.EventHandler(this.butEbill_Click);
			//
			// butSendSetup
			//
			this.butSendSetup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butSendSetup.Location = new System.Drawing.Point(148, 568);
			this.butSendSetup.Name = "butSendSetup";
			this.butSendSetup.Size = new System.Drawing.Size(98, 24);
			this.butSendSetup.TabIndex = 9;
			this.butSendSetup.Text = "Send Setup...";
			this.butSendSetup.Click += new System.EventHandler(this.butSendSetup_Click);
			//
			// comboClinic
			//
			this.comboClinic.HqDescription = "Practice Defaults";
			this.comboClinic.IncludeUnassigned = true;
			this.comboClinic.Location = new System.Drawing.Point(12, 10);
			this.comboClinic.Name = "comboClinic";
			this.comboClinic.Size = new System.Drawing.Size(250, 21);
			this.comboClinic.TabIndex = 0;
			this.comboClinic.SelectionChangeCommitted += new System.EventHandler(this.comboClinic_SelectionChangeCommitted);
			//
			// checkUsePracticeDefaults
			//
			this.checkUsePracticeDefaults.Location = new System.Drawing.Point(280, 11);
			this.checkUsePracticeDefaults.Name = "checkUsePracticeDefaults";
			this.checkUsePracticeDefaults.Size = new System.Drawing.Size(420, 18);
			this.checkUsePracticeDefaults.TabIndex = 1;
			this.checkUsePracticeDefaults.Text = "Use the practice defaults for this clinic";
			this.checkUsePracticeDefaults.Visible = false;
			this.checkUsePracticeDefaults.Click += new System.EventHandler(this.checkUsePracticeDefaults_Click);
			//
			// groupStatements
			//
			this.groupStatements.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupStatements.Controls.Add(this.checkIntermingled);
			this.groupStatements.Controls.Add(this.checkSinglePatient);
			this.groupStatements.Controls.Add(this.labelStartDate);
			this.groupStatements.Controls.Add(this.textDays);
			this.groupStatements.Controls.Add(this.label1);
			this.groupStatements.Controls.Add(this.checkBoxBillShowTransSinceZero);
			this.groupStatements.Controls.Add(this.checkInserterBarcode);
			this.groupStatements.Location = new System.Drawing.Point(12, 38);
			this.groupStatements.Name = "groupStatements";
			this.groupStatements.Size = new System.Drawing.Size(853, 70);
			this.groupStatements.TabIndex = 2;
			this.groupStatements.Text = "Statements";
			//
			// checkIntermingled
			//
			this.checkIntermingled.Location = new System.Drawing.Point(12, 18);
			this.checkIntermingled.Name = "checkIntermingled";
			this.checkIntermingled.Size = new System.Drawing.Size(216, 20);
			this.checkIntermingled.TabIndex = 0;
			this.checkIntermingled.Text = "Intermingle family members";
			//
			// checkSinglePatient
			//
			this.checkSinglePatient.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
			this.checkSinglePatient.Location = new System.Drawing.Point(240, 19);
			this.checkSinglePatient.Name = "checkSinglePatient";
			this.checkSinglePatient.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
			this.checkSinglePatient.Size = new System.Drawing.Size(158, 17);
			this.checkSinglePatient.TabIndex = 1;
			this.checkSinglePatient.Text = "Single patient only";
			this.checkSinglePatient.Visible = false;
			//
			// labelStartDate
			//
			this.labelStartDate.Location = new System.Drawing.Point(480, 20);
			this.labelStartDate.Name = "labelStartDate";
			this.labelStartDate.Size = new System.Drawing.Size(147, 14);
			this.labelStartDate.TabIndex = 2;
			this.labelStartDate.Text = "Start Date Last";
			this.labelStartDate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDays
			//
			this.textDays.Location = new System.Drawing.Point(629, 18);
			this.textDays.Name = "textDays";
			this.textDays.Size = new System.Drawing.Size(44, 20);
			this.textDays.TabIndex = 3;
			//
			// label1
			//
			this.label1.Location = new System.Drawing.Point(675, 20);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(65, 14);
			this.label1.TabIndex = 4;
			this.label1.Text = "Days";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// checkBoxBillShowTransSinceZero
			//
			this.checkBoxBillShowTransSinceZero.Location = new System.Drawing.Point(12, 43);
			this.checkBoxBillShowTransSinceZero.Name = "checkBoxBillShowTransSinceZero";
			this.checkBoxBillShowTransSinceZero.Size = new System.Drawing.Size(330, 18);
			this.checkBoxBillShowTransSinceZero.TabIndex = 5;
			this.checkBoxBillShowTransSinceZero.Text = "Show all transactions since zero or negative balance";
			//
			// checkInserterBarcode
			//
			this.checkInserterBarcode.Location = new System.Drawing.Point(420, 43);
			this.checkInserterBarcode.Name = "checkInserterBarcode";
			this.checkInserterBarcode.Size = new System.Drawing.Size(320, 18);
			this.checkInserterBarcode.TabIndex = 6;
			this.checkInserterBarcode.Text = "Print inserter barcode on statements";
			//
			// groupMessages
			//
			this.groupMessages.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupMessages.Controls.Add(this.label4);
			this.groupMessages.Controls.Add(this.textNote);
			this.groupMessages.Controls.Add(this.groupBox3);
			this.groupMessages.Controls.Add(this.label11);
			this.groupMessages.Controls.Add(this.textInvoiceNote);
			this.groupMessages.Location = new System.Drawing.Point(12, 114);
			this.groupMessages.Name = "groupMessages";
			this.groupMessages.Size = new System.Drawing.Size(853, 446);
			this.groupMessages.TabIndex = 3;
			this.groupMessages.Text = "Messages";
			//
			// label4
			//
			this.label4.Location = new System.Drawing.Point(11, 16);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(575, 32);
			this.label4.TabIndex = 0;
			this.label4.Text = "General Message (in addition to any dunning messages and appointment messages).\r\n" +
    "You may use the variable [InstallmentPlanTerms] below to show the terms of the i" +
    "nstallment plan.";
			this.label4.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// textNote
			//
			this.textNote.AcceptsTab = true;
			this.textNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textNote.BackColor = System.Drawing.SystemColors.Window;
			this.textNote.DetectLinksEnabled = false;
			this.textNote.DetectUrls = false;
			this.textNote.Location = new System.Drawing.Point(12, 49);
			this.textNote.Name = "textNote";
			this.textNote.QuickPasteType = OpenPmsDentalBusiness.EnumQuickPasteType.Statement;
			this.textNote.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.textNote.Size = new System.Drawing.Size(829, 66);
			this.textNote.TabIndex = 1;
			this.textNote.Text = "";
			//
			// groupBox3
			//
			this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox3.Controls.Add(this.label10);
			this.groupBox3.Controls.Add(this.label9);
			this.groupBox3.Controls.Add(this.textBillingEmailSubject);
			this.groupBox3.Controls.Add(this.label8);
			this.groupBox3.Controls.Add(this.textBillingEmailBody);
			this.groupBox3.Controls.Add(this.checkBillingEmailIncludeAutograph);
			this.groupBox3.Controls.Add(this.labelBillingEmailType);
			this.groupBox3.Controls.Add(this.comboBillingEmailType);
			this.groupBox3.Controls.Add(this.butBillingEmailEditHtml);
			this.groupBox3.Location = new System.Drawing.Point(6, 122);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(841, 210);
			this.groupBox3.TabIndex = 2;
			this.groupBox3.Text = "Email Statements";
			//
			// label10
			//
			this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.label10.Location = new System.Drawing.Point(11, 15);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(814, 29);
			this.label10.TabIndex = 0;
			this.label10.Text = resources.GetString("label10.Text");
			//
			// label9
			//
			this.label9.Location = new System.Drawing.Point(11, 45);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(218, 16);
			this.label9.TabIndex = 1;
			this.label9.Text = "Subject";
			this.label9.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// textBillingEmailSubject
			//
			this.textBillingEmailSubject.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textBillingEmailSubject.Location = new System.Drawing.Point(12, 62);
			this.textBillingEmailSubject.MaxLength = 200;
			this.textBillingEmailSubject.Name = "textBillingEmailSubject";
			this.textBillingEmailSubject.Size = new System.Drawing.Size(816, 20);
			this.textBillingEmailSubject.TabIndex = 2;
			//
			// label8
			//
			this.label8.Location = new System.Drawing.Point(11, 83);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(87, 16);
			this.label8.TabIndex = 3;
			this.label8.Text = "Body";
			this.label8.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// textBillingEmailBody
			//
			this.textBillingEmailBody.AcceptsTab = true;
			this.textBillingEmailBody.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textBillingEmailBody.BackColor = System.Drawing.SystemColors.Window;
			this.textBillingEmailBody.DetectLinksEnabled = false;
			this.textBillingEmailBody.DetectUrls = false;
			this.textBillingEmailBody.Location = new System.Drawing.Point(12, 100);
			this.textBillingEmailBody.Name = "textBillingEmailBody";
			this.textBillingEmailBody.QuickPasteType = OpenPmsDentalBusiness.EnumQuickPasteType.Statement;
			this.textBillingEmailBody.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.textBillingEmailBody.Size = new System.Drawing.Size(818, 72);
			this.textBillingEmailBody.TabIndex = 4;
			this.textBillingEmailBody.Text = "";
			//
			// checkBillingEmailIncludeAutograph
			//
			this.checkBillingEmailIncludeAutograph.Location = new System.Drawing.Point(12, 181);
			this.checkBillingEmailIncludeAutograph.Name = "checkBillingEmailIncludeAutograph";
			this.checkBillingEmailIncludeAutograph.Size = new System.Drawing.Size(270, 18);
			this.checkBillingEmailIncludeAutograph.TabIndex = 5;
			this.checkBillingEmailIncludeAutograph.Text = "Include Email Autograph of sender address";
			//
			// labelBillingEmailType
			//
			this.labelBillingEmailType.Location = new System.Drawing.Point(296, 181);
			this.labelBillingEmailType.Name = "labelBillingEmailType";
			this.labelBillingEmailType.Size = new System.Drawing.Size(94, 18);
			this.labelBillingEmailType.TabIndex = 6;
			this.labelBillingEmailType.Text = "Email type";
			this.labelBillingEmailType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboBillingEmailType
			//
			this.comboBillingEmailType.Location = new System.Drawing.Point(392, 180);
			this.comboBillingEmailType.Name = "comboBillingEmailType";
			this.comboBillingEmailType.Size = new System.Drawing.Size(140, 21);
			this.comboBillingEmailType.TabIndex = 7;
			//
			// butBillingEmailEditHtml
			//
			this.butBillingEmailEditHtml.Location = new System.Drawing.Point(540, 178);
			this.butBillingEmailEditHtml.Name = "butBillingEmailEditHtml";
			this.butBillingEmailEditHtml.Size = new System.Drawing.Size(100, 24);
			this.butBillingEmailEditHtml.TabIndex = 8;
			this.butBillingEmailEditHtml.Text = "Edit HTML...";
			this.butBillingEmailEditHtml.Click += new System.EventHandler(this.butBillingEmailEditHtml_Click);
			//
			// label11
			//
			this.label11.Location = new System.Drawing.Point(11, 338);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(152, 16);
			this.label11.TabIndex = 3;
			this.label11.Text = "Invoice Note";
			this.label11.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			//
			// textInvoiceNote
			//
			this.textInvoiceNote.AcceptsTab = true;
			this.textInvoiceNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textInvoiceNote.BackColor = System.Drawing.SystemColors.Window;
			this.textInvoiceNote.DetectLinksEnabled = false;
			this.textInvoiceNote.DetectUrls = false;
			this.textInvoiceNote.Location = new System.Drawing.Point(12, 355);
			this.textInvoiceNote.Name = "textInvoiceNote";
			this.textInvoiceNote.QuickPasteType = OpenPmsDentalBusiness.EnumQuickPasteType.Statement;
			this.textInvoiceNote.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.textInvoiceNote.Size = new System.Drawing.Size(829, 66);
			this.textInvoiceNote.TabIndex = 4;
			this.textInvoiceNote.Text = "";
			//
			// FormBillingDefaults
			//
			this.ClientSize = new System.Drawing.Size(877, 602);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.butEbill);
			this.Controls.Add(this.butSendSetup);
			this.Controls.Add(this.groupMessages);
			this.Controls.Add(this.groupStatements);
			this.Controls.Add(this.checkUsePracticeDefaults);
			this.Controls.Add(this.comboClinic);
			this.Name = "FormBillingDefaults";
			this.Text = "Billing Defaults";
			this.Load += new System.EventHandler(this.FormBillingDefaults_Load);
			this.groupStatements.ResumeLayout(false);
			this.groupStatements.PerformLayout();
			this.groupMessages.ResumeLayout(false);
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private OpenPmsDental.UI.Button butSave;
		private OpenPmsDental.UI.Button butEbill;
		private OpenPmsDental.UI.Button butSendSetup;
		private OpenPmsDental.UI.ComboBoxClinicPicker comboClinic;
		private OpenPmsDental.UI.CheckBox checkUsePracticeDefaults;
		private OpenPmsDental.UI.GroupBox groupStatements;
		private OpenPmsDental.UI.CheckBox checkIntermingled;
		private OpenPmsDental.UI.CheckBox checkSinglePatient;
		private System.Windows.Forms.Label labelStartDate;
		private ValidNum textDays;
		private System.Windows.Forms.Label label1;
		private OpenPmsDental.UI.CheckBox checkBoxBillShowTransSinceZero;
		private OpenPmsDental.UI.CheckBox checkInserterBarcode;
		private OpenPmsDental.UI.GroupBox groupMessages;
		private System.Windows.Forms.Label label4;
		private ODtextBox textNote;
		private OpenPmsDental.UI.GroupBox groupBox3;
		private System.Windows.Forms.Label label10;
		private System.Windows.Forms.Label label9;
		private System.Windows.Forms.TextBox textBillingEmailSubject;
		private System.Windows.Forms.Label label8;
		private ODtextBox textBillingEmailBody;
		private UI.CheckBox checkBillingEmailIncludeAutograph;
		private System.Windows.Forms.Label labelBillingEmailType;
		private UI.ComboBox comboBillingEmailType;
		private UI.Button butBillingEmailEditHtml;
		private System.Windows.Forms.Label label11;
		private ODtextBox textInvoiceNote;
	}
}
