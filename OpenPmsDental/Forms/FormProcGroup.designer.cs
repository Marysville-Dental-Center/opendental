using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenPmsDental {
	public partial class FormProcGroup {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing) {
			if(disposing) {
				components?.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		private void InitializeComponent(){
			this.label7 = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.label16 = new System.Windows.Forms.Label();
			this.textUser = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.label26 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.textProcDate = new OpenPmsDental.ValidDate();
			this.signatureBoxWrapper = new OpenPmsDental.UI.SignatureBoxWrapper();
			this.gridProc = new OpenPmsDental.UI.GridOD();
			this.textDateEntry = new OpenPmsDental.ValidDate();
			this.buttonUseAutoNote = new OpenPmsDental.UI.Button();
			this.textNotes = new OpenPmsDental.ODtextBox();
			this.butDelete = new OpenPmsDental.UI.Button();
			this.butSave = new OpenPmsDental.UI.Button();
			this.butLock = new OpenPmsDental.UI.Button();
			this.butInvalidate = new OpenPmsDental.UI.Button();
			this.butAppend = new OpenPmsDental.UI.Button();
			this.labelLocked = new System.Windows.Forms.Label();
			this.labelInvalid = new System.Windows.Forms.Label();
			this.butChangeUser = new OpenPmsDental.UI.Button();
			this.labelPermAlert = new System.Windows.Forms.Label();
			this.butEditAutoNote = new OpenPmsDental.UI.Button();
			this.labelProv = new System.Windows.Forms.Label();
			this.comboProv = new OpenPmsDental.UI.ComboBox();
			this.butAdd = new OpenPmsDental.UI.Button();
			this.butRemove = new OpenPmsDental.UI.Button();
			this.labelSignature2 = new System.Windows.Forms.Label();
			this.signatureBoxWrapper2 = new OpenPmsDental.UI.SignatureBoxWrapper();
			this.labelUser2 = new System.Windows.Forms.Label();
			this.textUser2 = new System.Windows.Forms.TextBox();
			this.butChangeUser2 = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			// 
			// label7
			// 
			this.label7.Location = new System.Drawing.Point(23, 78);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(73, 16);
			this.label7.TabIndex = 0;
			this.label7.Text = "&Notes";
			this.label7.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// label15
			// 
			this.label15.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label15.Location = new System.Drawing.Point(5, 309);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(91, 41);
			this.label15.TabIndex = 79;
			this.label15.Text = "Signature /\r\nInitials";
			this.label15.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// label16
			// 
			this.label16.Location = new System.Drawing.Point(23, 55);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(73, 16);
			this.label16.TabIndex = 80;
			this.label16.Text = "User";
			this.label16.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textUser
			// 
			this.textUser.Location = new System.Drawing.Point(98, 52);
			this.textUser.Name = "textUser";
			this.textUser.ReadOnly = true;
			this.textUser.Size = new System.Drawing.Size(119, 20);
			this.textUser.TabIndex = 101;
			// 
			// label12
			// 
			this.label12.Location = new System.Drawing.Point(-25, 34);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(125, 14);
			this.label12.TabIndex = 96;
			this.label12.Text = "Date Entry";
			this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// label26
			// 
			this.label26.Location = new System.Drawing.Point(177, 32);
			this.label26.Name = "label26";
			this.label26.Size = new System.Drawing.Size(83, 18);
			this.label26.TabIndex = 97;
			this.label26.Text = "(for security)";
			this.label26.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(2, 14);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(96, 14);
			this.label2.TabIndex = 101;
			this.label2.Text = "Procedure Date";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textProcDate
			// 
			this.textProcDate.Location = new System.Drawing.Point(98, 12);
			this.textProcDate.Name = "textProcDate";
			this.textProcDate.Size = new System.Drawing.Size(76, 20);
			this.textProcDate.TabIndex = 100;
			this.textProcDate.TextChanged += new System.EventHandler(this.textProcDate_TextChanged);
			// 
			// signatureBoxWrapper
			// 
			this.signatureBoxWrapper.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.signatureBoxWrapper.BackColor = System.Drawing.SystemColors.ControlDark;
			this.signatureBoxWrapper.Location = new System.Drawing.Point(98, 309);
			this.signatureBoxWrapper.Name = "signatureBoxWrapper";
			this.signatureBoxWrapper.Size = new System.Drawing.Size(394, 81);
			this.signatureBoxWrapper.TabIndex = 194;
			this.signatureBoxWrapper.UserSig = null;
			this.signatureBoxWrapper.SignatureChanged += new System.EventHandler(this.signatureBoxWrapper_SignatureChanged);
			// 
			// gridProc
			// 
			this.gridProc.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.gridProc.HScrollVisible = true;
			this.gridProc.Location = new System.Drawing.Point(10, 420);
			this.gridProc.Name = "gridProc";
			this.gridProc.SelectionMode = OpenPmsDental.UI.GridSelectionMode.MultiExtended;
			this.gridProc.Size = new System.Drawing.Size(864, 222);
			this.gridProc.TabIndex = 193;
			this.gridProc.Title = "Procedures";
			this.gridProc.TranslationName = "TableProg";
			// 
			// textDateEntry
			// 
			this.textDateEntry.Location = new System.Drawing.Point(98, 32);
			this.textDateEntry.Name = "textDateEntry";
			this.textDateEntry.ReadOnly = true;
			this.textDateEntry.Size = new System.Drawing.Size(76, 20);
			this.textDateEntry.TabIndex = 95;
			// 
			// buttonUseAutoNote
			// 
			this.buttonUseAutoNote.Location = new System.Drawing.Point(329, 50);
			this.buttonUseAutoNote.Name = "buttonUseAutoNote";
			this.buttonUseAutoNote.Size = new System.Drawing.Size(82, 22);
			this.buttonUseAutoNote.TabIndex = 106;
			this.buttonUseAutoNote.Text = "Auto Note";
			this.buttonUseAutoNote.Click += new System.EventHandler(this.buttonUseAutoNote_Click);
			// 
			// textNotes
			// 
			this.textNotes.AcceptsTab = true;
			this.textNotes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textNotes.BackColor = System.Drawing.SystemColors.Window;
			this.textNotes.DetectLinksEnabled = false;
			this.textNotes.DetectUrls = false;
			this.textNotes.HasAutoNotes = true;
			this.textNotes.Location = new System.Drawing.Point(98, 72);
			this.textNotes.Name = "textNotes";
			this.textNotes.QuickPasteType = OpenPmsDentalBusiness.EnumQuickPasteType.Procedure;
			this.textNotes.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.textNotes.Size = new System.Drawing.Size(880, 231);
			this.textNotes.TabIndex = 1;
			this.textNotes.Text = "";
			this.textNotes.TextChanged += new System.EventHandler(this.textNotes_TextChanged);
			// 
			// butDelete
			// 
			this.butDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butDelete.Icon = OpenPmsDental.UI.EnumIcons.DeleteX;
			this.butDelete.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
			this.butDelete.Location = new System.Drawing.Point(19, 651);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(83, 24);
			this.butDelete.TabIndex = 8;
			this.butDelete.Text = "&Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			// 
			// butSave
			// 
			this.butSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butSave.Location = new System.Drawing.Point(882, 654);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(76, 24);
			this.butSave.TabIndex = 12;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			// 
			// butLock
			// 
			this.butLock.Location = new System.Drawing.Point(413, 22);
			this.butLock.Name = "butLock";
			this.butLock.Size = new System.Drawing.Size(80, 22);
			this.butLock.TabIndex = 204;
			this.butLock.Text = "Lock";
			this.butLock.Click += new System.EventHandler(this.butLock_Click);
			// 
			// butInvalidate
			// 
			this.butInvalidate.Location = new System.Drawing.Point(412, 3);
			this.butInvalidate.Name = "butInvalidate";
			this.butInvalidate.Size = new System.Drawing.Size(80, 22);
			this.butInvalidate.TabIndex = 205;
			this.butInvalidate.Text = "Invalidate";
			this.butInvalidate.Visible = false;
			this.butInvalidate.Click += new System.EventHandler(this.butInvalidate_Click);
			// 
			// butAppend
			// 
			this.butAppend.Location = new System.Drawing.Point(413, 50);
			this.butAppend.Name = "butAppend";
			this.butAppend.Size = new System.Drawing.Size(80, 22);
			this.butAppend.TabIndex = 203;
			this.butAppend.Text = "Append";
			this.butAppend.Visible = false;
			this.butAppend.Click += new System.EventHandler(this.butAppend_Click);
			// 
			// labelLocked
			// 
			this.labelLocked.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.labelLocked.ForeColor = System.Drawing.Color.DarkRed;
			this.labelLocked.Location = new System.Drawing.Point(372, 29);
			this.labelLocked.Name = "labelLocked";
			this.labelLocked.Size = new System.Drawing.Size(123, 18);
			this.labelLocked.TabIndex = 202;
			this.labelLocked.Text = "Locked";
			this.labelLocked.TextAlign = System.Drawing.ContentAlignment.BottomRight;
			this.labelLocked.Visible = false;
			// 
			// labelInvalid
			// 
			this.labelInvalid.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.labelInvalid.ForeColor = System.Drawing.Color.DarkRed;
			this.labelInvalid.Location = new System.Drawing.Point(304, 7);
			this.labelInvalid.Name = "labelInvalid";
			this.labelInvalid.Size = new System.Drawing.Size(102, 18);
			this.labelInvalid.TabIndex = 206;
			this.labelInvalid.Text = "Invalid";
			this.labelInvalid.TextAlign = System.Drawing.ContentAlignment.BottomRight;
			this.labelInvalid.Visible = false;
			// 
			// butChangeUser
			// 
			this.butChangeUser.Location = new System.Drawing.Point(218, 50);
			this.butChangeUser.Name = "butChangeUser";
			this.butChangeUser.Size = new System.Drawing.Size(23, 22);
			this.butChangeUser.TabIndex = 207;
			this.butChangeUser.Text = "...";
			this.butChangeUser.Click += new System.EventHandler(this.butChangeUser_Click);
			// 
			// labelPermAlert
			// 
			this.labelPermAlert.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelPermAlert.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.labelPermAlert.ForeColor = System.Drawing.Color.DarkRed;
			this.labelPermAlert.Location = new System.Drawing.Point(95, 393);
			this.labelPermAlert.Name = "labelPermAlert";
			this.labelPermAlert.Size = new System.Drawing.Size(367, 24);
			this.labelPermAlert.TabIndex = 209;
			this.labelPermAlert.Text = "Notes and Signature locked. Need GroupNoteUser permission.";
			this.labelPermAlert.Visible = false;
			// 
			// butEditAutoNote
			// 
			this.butEditAutoNote.Location = new System.Drawing.Point(245, 50);
			this.butEditAutoNote.Name = "butEditAutoNote";
			this.butEditAutoNote.Size = new System.Drawing.Size(82, 22);
			this.butEditAutoNote.TabIndex = 210;
			this.butEditAutoNote.Text = "Edit Auto Note";
			this.butEditAutoNote.Click += new System.EventHandler(this.ButEditAutoNote_Click);
			// 
			// labelProv
			// 
			this.labelProv.Location = new System.Drawing.Point(174, 13);
			this.labelProv.Name = "labelProv";
			this.labelProv.Size = new System.Drawing.Size(50, 18);
			this.labelProv.TabIndex = 211;
			this.labelProv.Text = "Provider";
			this.labelProv.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// comboProv
			// 
			this.comboProv.Location = new System.Drawing.Point(225, 11);
			this.comboProv.Name = "comboProv";
			this.comboProv.Size = new System.Drawing.Size(78, 21);
			this.comboProv.TabIndex = 212;
			// 
			// butAdd
			// 
			this.butAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butAdd.Location = new System.Drawing.Point(882, 420);
			this.butAdd.Name = "butAdd";
			this.butAdd.Size = new System.Drawing.Size(76, 24);
			this.butAdd.TabIndex = 213;
			this.butAdd.Text = "Add";
			this.butAdd.Click += new System.EventHandler(this.butAdd_Click);
			// 
			// butRemove
			// 
			this.butRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.butRemove.Location = new System.Drawing.Point(882, 450);
			this.butRemove.Name = "butRemove";
			this.butRemove.Size = new System.Drawing.Size(76, 24);
			this.butRemove.TabIndex = 214;
			this.butRemove.Text = "Remove";
			this.butRemove.Click += new System.EventHandler(this.butRemove_Click);
			// 
			// labelSignature2
			// 
			this.labelSignature2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelSignature2.Location = new System.Drawing.Point(496, 309);
			this.labelSignature2.Name = "labelSignature2";
			this.labelSignature2.Size = new System.Drawing.Size(87, 30);
			this.labelSignature2.TabIndex = 215;
			this.labelSignature2.Text = "Signature 2";
			this.labelSignature2.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// signatureBoxWrapper2
			// 
			this.signatureBoxWrapper2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.signatureBoxWrapper2.BackColor = System.Drawing.SystemColors.ControlDark;
			this.signatureBoxWrapper2.Location = new System.Drawing.Point(586, 309);
			this.signatureBoxWrapper2.Name = "signatureBoxWrapper2";
			this.signatureBoxWrapper2.Size = new System.Drawing.Size(394, 81);
			this.signatureBoxWrapper2.TabIndex = 216;
			this.signatureBoxWrapper2.UserSig = null;
			this.signatureBoxWrapper2.SignatureChanged += new System.EventHandler(this.signatureBoxWrapper2_SignatureChanged);
			// 
			// labelUser2
			// 
			this.labelUser2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.labelUser2.Location = new System.Drawing.Point(496, 395);
			this.labelUser2.Name = "labelUser2";
			this.labelUser2.Size = new System.Drawing.Size(87, 16);
			this.labelUser2.TabIndex = 217;
			this.labelUser2.Text = "User 2";
			this.labelUser2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// textUser2
			// 
			this.textUser2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.textUser2.Location = new System.Drawing.Point(586, 393);
			this.textUser2.Name = "textUser2";
			this.textUser2.ReadOnly = true;
			this.textUser2.Size = new System.Drawing.Size(119, 20);
			this.textUser2.TabIndex = 218;
			// 
			// butChangeUser2
			// 
			this.butChangeUser2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.butChangeUser2.Location = new System.Drawing.Point(706, 392);
			this.butChangeUser2.Name = "butChangeUser2";
			this.butChangeUser2.Size = new System.Drawing.Size(23, 22);
			this.butChangeUser2.TabIndex = 219;
			this.butChangeUser2.Text = "...";
			this.butChangeUser2.Click += new System.EventHandler(this.butChangeUser2_Click);
			// 
			// FormProcGroup
			// 
			this.ClientSize = new System.Drawing.Size(990, 690);
			this.Controls.Add(this.butChangeUser2);
			this.Controls.Add(this.textUser2);
			this.Controls.Add(this.labelUser2);
			this.Controls.Add(this.signatureBoxWrapper2);
			this.Controls.Add(this.labelSignature2);
			this.Controls.Add(this.butRemove);
			this.Controls.Add(this.butAdd);
			this.Controls.Add(this.comboProv);
			this.Controls.Add(this.labelProv);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.butEditAutoNote);
			this.Controls.Add(this.labelPermAlert);
			this.Controls.Add(this.butChangeUser);
			this.Controls.Add(this.labelInvalid);
			this.Controls.Add(this.butLock);
			this.Controls.Add(this.butInvalidate);
			this.Controls.Add(this.butAppend);
			this.Controls.Add(this.labelLocked);
			this.Controls.Add(this.buttonUseAutoNote);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.textProcDate);
			this.Controls.Add(this.signatureBoxWrapper);
			this.Controls.Add(this.label26);
			this.Controls.Add(this.gridProc);
			this.Controls.Add(this.textDateEntry);
			this.Controls.Add(this.label12);
			this.Controls.Add(this.textUser);
			this.Controls.Add(this.textNotes);
			this.Controls.Add(this.label16);
			this.Controls.Add(this.label15);
			this.Controls.Add(this.label7);
			this.Controls.Add(this.butDelete);
			this.MinimizeBox = false;
			this.Name = "FormProcGroup";
			this.ShowInTaskbar = false;
			this.Text = "Group Note";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormProcGroup_FormClosing);
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FormProcGroup_FormClosed);
			this.Load += new System.EventHandler(this.FormProcGroup_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		#endregion

		private System.Windows.Forms.Label label7;
		private OpenPmsDental.UI.Button butSave;
		private OpenPmsDental.UI.Button butDelete;
		private OpenPmsDental.ODtextBox textNotes;
		private Label label15;
		private Label label16;
		private TextBox textUser;
		private OpenPmsDental.UI.Button buttonUseAutoNote;
		private UI.GridOD gridProc;
		private UI.SignatureBoxWrapper signatureBoxWrapper;
		private Label label12;
		private ValidDate textDateEntry;
		private Label label26;
		private ValidDate textProcDate;
		private Label label2;
		private UI.Button butLock;
		private UI.Button butInvalidate;
		private UI.Button butAppend;
		private Label labelLocked;
		private Label labelInvalid;
		private UI.Button butChangeUser;
		private UI.Button butEditAutoNote;
		private Label labelPermAlert;
		private Label labelProv;
		private UI.ComboBox comboProv;
		private UI.Button butAdd;
		private UI.Button butRemove;
		private Label labelSignature2;
		private UI.SignatureBoxWrapper signatureBoxWrapper2;
		private Label labelUser2;
		private TextBox textUser2;
		private UI.Button butChangeUser2;
	}
}
