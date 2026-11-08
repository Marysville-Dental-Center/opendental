using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormLabCaseEdit {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.label1 = new System.Windows.Forms.Label();
			this.textPatient = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.textAppointment = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textPlanned = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.textDateCreated = new System.Windows.Forms.TextBox();
			this.textDateSent = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textDateRecd = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textDateChecked = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textDateDue = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.comboProv = new OpenPmsDental.UI.ComboBox();
			this.label11 = new System.Windows.Forms.Label();
			this.listLab = new OpenPmsDental.UI.ListBox();
			this.groupBox1 = new OpenPmsDental.UI.GroupBox();
			this.butCheckedNow = new OpenPmsDental.UI.Button();
			this.butRecdNow = new OpenPmsDental.UI.Button();
			this.butSentNow = new OpenPmsDental.UI.Button();
			this.butCreatedNow = new OpenPmsDental.UI.Button();
			this.listTurnaround = new OpenPmsDental.UI.ListBox();
			this.label12 = new System.Windows.Forms.Label();
			this.textWeekday = new System.Windows.Forms.TextBox();
			this.butSlip = new OpenPmsDental.UI.Button();
			this.butDetachPlanned = new OpenPmsDental.UI.Button();
			this.butDetach = new OpenPmsDental.UI.Button();
			this.butDelete = new OpenPmsDental.UI.Button();
			this.butSave = new OpenPmsDental.UI.Button();
			this.labelLabFee = new System.Windows.Forms.Label();
			this.textLabFee = new OpenPmsDental.ValidDouble();
			this.labelInvoiceNumber = new System.Windows.Forms.Label();
			this.textInvoiceNumber = new System.Windows.Forms.TextBox();
			this.textInstructions = new OpenPmsDental.ODtextBox();
			this.groupGlidewell = new OpenPmsDental.UI.GroupBox();
			this.labelGwAccount = new System.Windows.Forms.Label();
			this.comboGwAccount = new OpenPmsDental.UI.ComboBox();
			this.labelGwScanner = new System.Windows.Forms.Label();
			this.comboGwScanner = new OpenPmsDental.UI.ComboBox();
			this.gridGwProducts = new OpenPmsDental.UI.GridOD();
			this.butGwProductAdd = new OpenPmsDental.UI.Button();
			this.labelGwStatus = new System.Windows.Forms.Label();
			this.butGwSend = new OpenPmsDental.UI.Button();
			this.butGwDetails = new OpenPmsDental.UI.Button();
			this.groupBox1.SuspendLayout();
			this.groupGlidewell.SuspendLayout();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(5, 16);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(89, 17);
			this.label1.TabIndex = 2;
			this.label1.Text = "Patient";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textPatient
			// 
			this.textPatient.Location = new System.Drawing.Point(94, 15);
			this.textPatient.Name = "textPatient";
			this.textPatient.ReadOnly = true;
			this.textPatient.Size = new System.Drawing.Size(319, 20);
			this.textPatient.TabIndex = 0;
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(5, 306);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(89, 17);
			this.label3.TabIndex = 101;
			this.label3.Text = "Instructions";
			this.label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(91, 135);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(89, 17);
			this.label2.TabIndex = 99;
			this.label2.Text = "Lab";
			this.label2.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			// 
			// textAppointment
			// 
			this.textAppointment.Location = new System.Drawing.Point(94, 40);
			this.textAppointment.Name = "textAppointment";
			this.textAppointment.ReadOnly = true;
			this.textAppointment.Size = new System.Drawing.Size(319, 20);
			this.textAppointment.TabIndex = 103;
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(5, 41);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(89, 17);
			this.label4.TabIndex = 104;
			this.label4.Text = "Appointment";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textPlanned
			// 
			this.textPlanned.Location = new System.Drawing.Point(94, 65);
			this.textPlanned.Name = "textPlanned";
			this.textPlanned.ReadOnly = true;
			this.textPlanned.Size = new System.Drawing.Size(319, 20);
			this.textPlanned.TabIndex = 106;
			// 
			// label5
			// 
			this.label5.Location = new System.Drawing.Point(5, 66);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(89, 17);
			this.label5.TabIndex = 107;
			this.label5.Text = "Planned Appt";
			this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// label6
			// 
			this.label6.Location = new System.Drawing.Point(17, 20);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(81, 17);
			this.label6.TabIndex = 110;
			this.label6.Text = "Created";
			this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textDateCreated
			// 
			this.textDateCreated.Location = new System.Drawing.Point(100, 19);
			this.textDateCreated.Name = "textDateCreated";
			this.textDateCreated.Size = new System.Drawing.Size(147, 20);
			this.textDateCreated.TabIndex = 111;
			// 
			// textDateSent
			// 
			this.textDateSent.Location = new System.Drawing.Point(100, 44);
			this.textDateSent.Name = "textDateSent";
			this.textDateSent.Size = new System.Drawing.Size(147, 20);
			this.textDateSent.TabIndex = 113;
			// 
			// label7
			// 
			this.label7.Location = new System.Drawing.Point(17, 45);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(81, 17);
			this.label7.TabIndex = 112;
			this.label7.Text = "Sent";
			this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textDateRecd
			// 
			this.textDateRecd.Location = new System.Drawing.Point(100, 69);
			this.textDateRecd.Name = "textDateRecd";
			this.textDateRecd.Size = new System.Drawing.Size(147, 20);
			this.textDateRecd.TabIndex = 115;
			// 
			// label8
			// 
			this.label8.Location = new System.Drawing.Point(17, 70);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(81, 17);
			this.label8.TabIndex = 114;
			this.label8.Text = "Received";
			this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textDateChecked
			// 
			this.textDateChecked.Location = new System.Drawing.Point(100, 94);
			this.textDateChecked.Name = "textDateChecked";
			this.textDateChecked.Size = new System.Drawing.Size(147, 20);
			this.textDateChecked.TabIndex = 117;
			// 
			// label9
			// 
			this.label9.Location = new System.Drawing.Point(2, 95);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(96, 17);
			this.label9.TabIndex = 116;
			this.label9.Text = "Quality Checked";
			this.label9.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textDateDue
			// 
			this.textDateDue.Location = new System.Drawing.Point(336, 280);
			this.textDateDue.Name = "textDateDue";
			this.textDateDue.Size = new System.Drawing.Size(158, 20);
			this.textDateDue.TabIndex = 119;
			this.textDateDue.TextChanged += new System.EventHandler(this.textDateDue_TextChanged);
			// 
			// label10
			// 
			this.label10.Location = new System.Drawing.Point(207, 281);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(89, 17);
			this.label10.TabIndex = 118;
			this.label10.Text = "Date Time Due";
			this.label10.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// comboProv
			// 
			this.comboProv.Location = new System.Drawing.Point(94, 90);
			this.comboProv.Name = "comboProv";
			this.comboProv.Size = new System.Drawing.Size(158, 21);
			this.comboProv.TabIndex = 121;
			// 
			// label11
			// 
			this.label11.Location = new System.Drawing.Point(5, 93);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(89, 17);
			this.label11.TabIndex = 120;
			this.label11.Text = "Provider";
			this.label11.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// listLab
			// 
			this.listLab.Location = new System.Drawing.Point(94, 155);
			this.listLab.Name = "listLab";
			this.listLab.Size = new System.Drawing.Size(198, 121);
			this.listLab.TabIndex = 0;
			this.listLab.SelectedIndexChanged += new System.EventHandler(this.listLab_SelectedIndexChanged);
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.butCheckedNow);
			this.groupBox1.Controls.Add(this.butRecdNow);
			this.groupBox1.Controls.Add(this.butSentNow);
			this.groupBox1.Controls.Add(this.butCreatedNow);
			this.groupBox1.Controls.Add(this.textDateCreated);
			this.groupBox1.Controls.Add(this.label6);
			this.groupBox1.Controls.Add(this.label7);
			this.groupBox1.Controls.Add(this.textDateSent);
			this.groupBox1.Controls.Add(this.label8);
			this.groupBox1.Controls.Add(this.textDateRecd);
			this.groupBox1.Controls.Add(this.textDateChecked);
			this.groupBox1.Controls.Add(this.label9);
			this.groupBox1.Location = new System.Drawing.Point(523, 10);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(329, 123);
			this.groupBox1.TabIndex = 122;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Tracking";
			// 
			// butCheckedNow
			// 
			this.butCheckedNow.Location = new System.Drawing.Point(248, 92);
			this.butCheckedNow.Name = "butCheckedNow";
			this.butCheckedNow.Size = new System.Drawing.Size(75, 24);
			this.butCheckedNow.TabIndex = 121;
			this.butCheckedNow.Text = "Now";
			this.butCheckedNow.Click += new System.EventHandler(this.butCheckedNow_Click);
			// 
			// butRecdNow
			// 
			this.butRecdNow.Location = new System.Drawing.Point(248, 67);
			this.butRecdNow.Name = "butRecdNow";
			this.butRecdNow.Size = new System.Drawing.Size(75, 24);
			this.butRecdNow.TabIndex = 120;
			this.butRecdNow.Text = "Now";
			this.butRecdNow.Click += new System.EventHandler(this.butRecdNow_Click);
			// 
			// butSentNow
			// 
			this.butSentNow.Location = new System.Drawing.Point(248, 42);
			this.butSentNow.Name = "butSentNow";
			this.butSentNow.Size = new System.Drawing.Size(75, 24);
			this.butSentNow.TabIndex = 119;
			this.butSentNow.Text = "Now";
			this.butSentNow.Click += new System.EventHandler(this.butSentNow_Click);
			// 
			// butCreatedNow
			// 
			this.butCreatedNow.Location = new System.Drawing.Point(248, 17);
			this.butCreatedNow.Name = "butCreatedNow";
			this.butCreatedNow.Size = new System.Drawing.Size(75, 24);
			this.butCreatedNow.TabIndex = 118;
			this.butCreatedNow.Text = "Now";
			this.butCreatedNow.Click += new System.EventHandler(this.butCreatedNow_Click);
			// 
			// listTurnaround
			// 
			this.listTurnaround.Location = new System.Drawing.Point(296, 155);
			this.listTurnaround.Name = "listTurnaround";
			this.listTurnaround.Size = new System.Drawing.Size(198, 121);
			this.listTurnaround.TabIndex = 124;
			this.listTurnaround.Click += new System.EventHandler(this.listTurnaround_Click);
			// 
			// label12
			// 
			this.label12.Location = new System.Drawing.Point(293, 135);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(89, 17);
			this.label12.TabIndex = 125;
			this.label12.Text = "Set Due Date";
			this.label12.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
			// 
			// textWeekday
			// 
			this.textWeekday.BackColor = System.Drawing.Color.White;
			this.textWeekday.Location = new System.Drawing.Point(296, 280);
			this.textWeekday.Name = "textWeekday";
			this.textWeekday.ReadOnly = true;
			this.textWeekday.Size = new System.Drawing.Size(40, 20);
			this.textWeekday.TabIndex = 126;
			// 
			// butSlip
			// 
			this.butSlip.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butSlip.Location = new System.Drawing.Point(434, 444);
			this.butSlip.Name = "butSlip";
			this.butSlip.Size = new System.Drawing.Size(85, 24);
			this.butSlip.TabIndex = 127;
			this.butSlip.Text = "New Slip";
			this.butSlip.Click += new System.EventHandler(this.butSlip_Click);
			// 
			// butDetachPlanned
			// 
			this.butDetachPlanned.Location = new System.Drawing.Point(419, 62);
			this.butDetachPlanned.Name = "butDetachPlanned";
			this.butDetachPlanned.Size = new System.Drawing.Size(75, 24);
			this.butDetachPlanned.TabIndex = 108;
			this.butDetachPlanned.Text = "Detach";
			this.butDetachPlanned.Click += new System.EventHandler(this.butDetachPlanned_Click);
			// 
			// butDetach
			// 
			this.butDetach.Location = new System.Drawing.Point(419, 37);
			this.butDetach.Name = "butDetach";
			this.butDetach.Size = new System.Drawing.Size(75, 24);
			this.butDetach.TabIndex = 105;
			this.butDetach.Text = "Detach";
			this.butDetach.Click += new System.EventHandler(this.butDetach_Click);
			// 
			// butDelete
			// 
			this.butDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butDelete.Icon = OpenPmsDental.UI.EnumIcons.DeleteX;
			this.butDelete.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDelete.Location = new System.Drawing.Point(13, 444);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(81, 24);
			this.butDelete.TabIndex = 4;
			this.butDelete.Text = "Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(777, 444);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 2;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// labelLabFee
			// 
			this.labelLabFee.Location = new System.Drawing.Point(5, 118);
			this.labelLabFee.Name = "labelLabFee";
			this.labelLabFee.Size = new System.Drawing.Size(89, 17);
			this.labelLabFee.TabIndex = 128;
			this.labelLabFee.Text = "Fee";
			this.labelLabFee.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// textLabFee
			// 
			this.textLabFee.Location = new System.Drawing.Point(94, 115);
			this.textLabFee.MaxVal = 100000000D;
			this.textLabFee.MinVal = -100000000D;
			this.textLabFee.Name = "textLabFee";
			this.textLabFee.Size = new System.Drawing.Size(68, 20);
			this.textLabFee.TabIndex = 129;
			// 
			// labelInvoiceNumber
			// 
			this.labelInvoiceNumber.Location = new System.Drawing.Point(257, 117);
			this.labelInvoiceNumber.Name = "labelInvoiceNumber";
			this.labelInvoiceNumber.Size = new System.Drawing.Size(118, 17);
			this.labelInvoiceNumber.TabIndex = 130;
			this.labelInvoiceNumber.Text = "Invoice Number";
			this.labelInvoiceNumber.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textInvoiceNumber
			// 
			this.textInvoiceNumber.Location = new System.Drawing.Point(375, 115);
			this.textInvoiceNumber.MaxLength = 255;
			this.textInvoiceNumber.Name = "textInvoiceNumber";
			this.textInvoiceNumber.Size = new System.Drawing.Size(119, 20);
			this.textInvoiceNumber.TabIndex = 131;
			// 
			// textInstructions
			// 
			this.textInstructions.AcceptsTab = true;
			this.textInstructions.BackColor = System.Drawing.SystemColors.Window;
			this.textInstructions.DetectLinksEnabled = false;
			this.textInstructions.DetectUrls = false;
			this.textInstructions.HasAutoNotes = true;
			this.textInstructions.Location = new System.Drawing.Point(94, 304);
			this.textInstructions.Name = "textInstructions";
			this.textInstructions.QuickPasteType = OpenPmsDentalBusiness.EnumQuickPasteType.Lab;
			this.textInstructions.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.textInstructions.Size = new System.Drawing.Size(400, 127);
			this.textInstructions.TabIndex = 1;
			this.textInstructions.Text = "";
			//
			// groupGlidewell
			//
			this.groupGlidewell.Controls.Add(this.butGwDetails);
			this.groupGlidewell.Controls.Add(this.butGwSend);
			this.groupGlidewell.Controls.Add(this.labelGwStatus);
			this.groupGlidewell.Controls.Add(this.butGwProductAdd);
			this.groupGlidewell.Controls.Add(this.gridGwProducts);
			this.groupGlidewell.Controls.Add(this.comboGwScanner);
			this.groupGlidewell.Controls.Add(this.labelGwScanner);
			this.groupGlidewell.Controls.Add(this.comboGwAccount);
			this.groupGlidewell.Controls.Add(this.labelGwAccount);
			this.groupGlidewell.Location = new System.Drawing.Point(523, 140);
			this.groupGlidewell.Name = "groupGlidewell";
			this.groupGlidewell.Size = new System.Drawing.Size(329, 291);
			this.groupGlidewell.TabIndex = 132;
			this.groupGlidewell.TabStop = false;
			this.groupGlidewell.Text = "Glidewell";
			this.groupGlidewell.Visible = false;
			//
			// labelGwAccount
			//
			this.labelGwAccount.Location = new System.Drawing.Point(6, 19);
			this.labelGwAccount.Name = "labelGwAccount";
			this.labelGwAccount.Size = new System.Drawing.Size(70, 17);
			this.labelGwAccount.TabIndex = 0;
			this.labelGwAccount.Text = "Account";
			this.labelGwAccount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboGwAccount
			//
			this.comboGwAccount.Location = new System.Drawing.Point(78, 18);
			this.comboGwAccount.Name = "comboGwAccount";
			this.comboGwAccount.Size = new System.Drawing.Size(170, 21);
			this.comboGwAccount.TabIndex = 1;
			this.comboGwAccount.SelectionChangeCommitted += new System.EventHandler(this.comboGwAccount_SelectionChangeCommitted);
			//
			// labelGwScanner
			//
			this.labelGwScanner.Location = new System.Drawing.Point(6, 44);
			this.labelGwScanner.Name = "labelGwScanner";
			this.labelGwScanner.Size = new System.Drawing.Size(70, 17);
			this.labelGwScanner.TabIndex = 2;
			this.labelGwScanner.Text = "Scanner";
			this.labelGwScanner.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboGwScanner
			//
			this.comboGwScanner.Location = new System.Drawing.Point(78, 43);
			this.comboGwScanner.Name = "comboGwScanner";
			this.comboGwScanner.Size = new System.Drawing.Size(100, 21);
			this.comboGwScanner.TabIndex = 3;
			//
			// gridGwProducts
			//
			this.gridGwProducts.Location = new System.Drawing.Point(6, 70);
			this.gridGwProducts.Name = "gridGwProducts";
			this.gridGwProducts.Size = new System.Drawing.Size(317, 110);
			this.gridGwProducts.TabIndex = 4;
			this.gridGwProducts.Title = "Products";
			this.gridGwProducts.TranslationName = "TableLabCaseProducts";
			this.gridGwProducts.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridGwProducts_CellDoubleClick);
			//
			// butGwProductAdd
			//
			this.butGwProductAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butGwProductAdd.Location = new System.Drawing.Point(6, 183);
			this.butGwProductAdd.Name = "butGwProductAdd";
			this.butGwProductAdd.Size = new System.Drawing.Size(80, 24);
			this.butGwProductAdd.TabIndex = 5;
			this.butGwProductAdd.Text = "Add";
			this.butGwProductAdd.Click += new System.EventHandler(this.butGwProductAdd_Click);
			//
			// labelGwStatus
			//
			this.labelGwStatus.Location = new System.Drawing.Point(6, 211);
			this.labelGwStatus.Name = "labelGwStatus";
			this.labelGwStatus.Size = new System.Drawing.Size(317, 45);
			this.labelGwStatus.TabIndex = 6;
			//
			// butGwSend
			//
			this.butGwSend.Location = new System.Drawing.Point(6, 259);
			this.butGwSend.Name = "butGwSend";
			this.butGwSend.Size = new System.Drawing.Size(120, 24);
			this.butGwSend.TabIndex = 7;
			this.butGwSend.Text = "Send to Glidewell";
			this.butGwSend.Click += new System.EventHandler(this.butGwSend_Click);
			//
			// butGwDetails
			//
			this.butGwDetails.Location = new System.Drawing.Point(130, 259);
			this.butGwDetails.Name = "butGwDetails";
			this.butGwDetails.Size = new System.Drawing.Size(75, 24);
			this.butGwDetails.TabIndex = 8;
			this.butGwDetails.Text = "Details";
			this.butGwDetails.Click += new System.EventHandler(this.butGwDetails_Click);
			//
			// FormLabCaseEdit
			//
			this.ClientSize = new System.Drawing.Size(864, 480);
			this.Controls.Add(this.groupGlidewell);
			this.Controls.Add(this.textInstructions);
			this.Controls.Add(this.textInvoiceNumber);
			this.Controls.Add(this.labelInvoiceNumber);
			this.Controls.Add(this.textLabFee);
			this.Controls.Add(this.labelLabFee);
			this.Controls.Add(this.butSlip);
			this.Controls.Add(this.textWeekday);
			this.Controls.Add(this.listTurnaround);
			this.Controls.Add(this.label12);
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.listLab);
			this.Controls.Add(this.comboProv);
			this.Controls.Add(this.label11);
			this.Controls.Add(this.textDateDue);
			this.Controls.Add(this.label10);
			this.Controls.Add(this.butDetachPlanned);
			this.Controls.Add(this.textPlanned);
			this.Controls.Add(this.label5);
			this.Controls.Add(this.butDetach);
			this.Controls.Add(this.textAppointment);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.butDelete);
			this.Controls.Add(this.textPatient);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.label1);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "FormLabCaseEdit";
			this.ShowInTaskbar = false;
			this.Text = "Edit Lab Case";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormLabCaseEdit_FormClosing);
			this.Load += new System.EventHandler(this.FormLabCaseEdit_Load);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupGlidewell.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion
		private OpenPmsDental.UI.Button butSave;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox textPatient;
		private OpenPmsDental.UI.Button butDelete;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label2;
		private TextBox textAppointment;
		private Label label4;
		private OpenPmsDental.UI.Button butDetach;
		private OpenPmsDental.UI.Button butDetachPlanned;
		private TextBox textPlanned;
		private Label label5;
		private Label label6;
		private TextBox textDateCreated;
		private TextBox textDateSent;
		private Label label7;
		private TextBox textDateRecd;
		private Label label8;
		private TextBox textDateChecked;
		private Label label9;
		private TextBox textDateDue;
		private Label label10;
		private OpenPmsDental.UI.ComboBox comboProv;
		private Label label11;
		private OpenPmsDental.UI.ListBox listLab;
		private OpenPmsDental.UI.GroupBox groupBox1;
		private OpenPmsDental.UI.Button butCheckedNow;
		private OpenPmsDental.UI.Button butRecdNow;
		private OpenPmsDental.UI.Button butSentNow;
		private OpenPmsDental.UI.Button butCreatedNow;
		private OpenPmsDental.UI.ListBox listTurnaround;
		private Label label12;
		private TextBox textWeekday;
		private OpenPmsDental.UI.Button butSlip;
		private Label labelLabFee;
		private ValidDouble textLabFee;
		private Label labelInvoiceNumber;
		private TextBox textInvoiceNumber;
		private ODtextBox textInstructions;
		private OpenPmsDental.UI.GroupBox groupGlidewell;
		private Label labelGwAccount;
		private OpenPmsDental.UI.ComboBox comboGwAccount;
		private Label labelGwScanner;
		private OpenPmsDental.UI.ComboBox comboGwScanner;
		private OpenPmsDental.UI.GridOD gridGwProducts;
		private OpenPmsDental.UI.Button butGwProductAdd;
		private Label labelGwStatus;
		private OpenPmsDental.UI.Button butGwSend;
		private OpenPmsDental.UI.Button butGwDetails;
	}
}
