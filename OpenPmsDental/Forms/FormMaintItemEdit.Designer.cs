namespace OpenPmsDental {
	partial class FormMaintItemEdit {
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
			this.labelName = new System.Windows.Forms.Label();
			this.textName = new System.Windows.Forms.TextBox();
			this.labelDescription = new System.Windows.Forms.Label();
			this.textDescription = new OpenPmsDental.ODtextBox();
			this.labelEquipment = new System.Windows.Forms.Label();
			this.comboEquipment = new OpenPmsDental.UI.ComboBox();
			this.groupSchedule = new OpenPmsDental.UI.GroupBox();
			this.labelScheduleType = new System.Windows.Forms.Label();
			this.comboScheduleType = new OpenPmsDental.UI.ComboBox();
			this.labelEvery = new System.Windows.Forms.Label();
			this.textCount = new OpenPmsDental.ValidNum();
			this.comboUnit = new OpenPmsDental.UI.ComboBox();
			this.labelAfter = new System.Windows.Forms.Label();
			this.labelWeekdays = new System.Windows.Forms.Label();
			this.checkSun = new OpenPmsDental.UI.CheckBox();
			this.checkMon = new OpenPmsDental.UI.CheckBox();
			this.checkTue = new OpenPmsDental.UI.CheckBox();
			this.checkWed = new OpenPmsDental.UI.CheckBox();
			this.checkThu = new OpenPmsDental.UI.CheckBox();
			this.checkFri = new OpenPmsDental.UI.CheckBox();
			this.checkSat = new OpenPmsDental.UI.CheckBox();
			this.labelAnchor = new System.Windows.Forms.Label();
			this.textAnchor = new OpenPmsDental.ValidDate();
			this.labelLead = new System.Windows.Forms.Label();
			this.textLead = new OpenPmsDental.ValidNum();
			this.labelLeadDays = new System.Windows.Forms.Label();
			this.labelNextDue = new System.Windows.Forms.Label();
			this.labelAssigned = new System.Windows.Forms.Label();
			this.comboAssigned = new OpenPmsDental.UI.ComboBox();
			this.checkHidden = new OpenPmsDental.UI.CheckBox();
			this.gridSteps = new OpenPmsDental.UI.GridOD();
			this.butStepAdd = new OpenPmsDental.UI.Button();
			this.butStepEdit = new OpenPmsDental.UI.Button();
			this.butStepDelete = new OpenPmsDental.UI.Button();
			this.butStepUp = new OpenPmsDental.UI.Button();
			this.butStepDown = new OpenPmsDental.UI.Button();
			this.gridFields = new OpenPmsDental.UI.GridOD();
			this.butFieldAdd = new OpenPmsDental.UI.Button();
			this.butFieldEdit = new OpenPmsDental.UI.Button();
			this.butFieldDelete = new OpenPmsDental.UI.Button();
			this.butFieldUp = new OpenPmsDental.UI.Button();
			this.butFieldDown = new OpenPmsDental.UI.Button();
			this.butDelete = new OpenPmsDental.UI.Button();
			this.butSave = new OpenPmsDental.UI.Button();
			this.groupSchedule.SuspendLayout();
			this.SuspendLayout();
			//
			// labelName
			//
			this.labelName.Location = new System.Drawing.Point(12, 15);
			this.labelName.Name = "labelName";
			this.labelName.Size = new System.Drawing.Size(110, 20);
			this.labelName.TabIndex = 0;
			this.labelName.Text = "Name";
			this.labelName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textName
			//
			this.textName.Location = new System.Drawing.Point(125, 15);
			this.textName.MaxLength = 255;
			this.textName.Name = "textName";
			this.textName.Size = new System.Drawing.Size(312, 20);
			this.textName.TabIndex = 1;
			this.textName.TextChanged += new System.EventHandler(this.Schedule_Changed);
			//
			// labelDescription
			//
			this.labelDescription.Location = new System.Drawing.Point(12, 41);
			this.labelDescription.Name = "labelDescription";
			this.labelDescription.Size = new System.Drawing.Size(110, 20);
			this.labelDescription.TabIndex = 2;
			this.labelDescription.Text = "Description";
			this.labelDescription.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDescription
			//
			this.textDescription.AcceptsTab = true;
			this.textDescription.BackColor = System.Drawing.SystemColors.Window;
			this.textDescription.DetectLinksEnabled = false;
			this.textDescription.DetectUrls = false;
			this.textDescription.Location = new System.Drawing.Point(125, 41);
			this.textDescription.Name = "textDescription";
			this.textDescription.QuickPasteType = OpenPmsDentalBusiness.EnumQuickPasteType.Equipment;
			this.textDescription.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.textDescription.Size = new System.Drawing.Size(312, 70);
			this.textDescription.TabIndex = 3;
			this.textDescription.Text = "";
			//
			// labelEquipment
			//
			this.labelEquipment.Location = new System.Drawing.Point(12, 117);
			this.labelEquipment.Name = "labelEquipment";
			this.labelEquipment.Size = new System.Drawing.Size(110, 20);
			this.labelEquipment.TabIndex = 4;
			this.labelEquipment.Text = "Equipment";
			this.labelEquipment.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboEquipment
			//
			this.comboEquipment.Location = new System.Drawing.Point(125, 117);
			this.comboEquipment.Name = "comboEquipment";
			this.comboEquipment.Size = new System.Drawing.Size(312, 21);
			this.comboEquipment.TabIndex = 5;
			//
			// groupSchedule
			//
			this.groupSchedule.Controls.Add(this.labelScheduleType);
			this.groupSchedule.Controls.Add(this.comboScheduleType);
			this.groupSchedule.Controls.Add(this.labelEvery);
			this.groupSchedule.Controls.Add(this.textCount);
			this.groupSchedule.Controls.Add(this.comboUnit);
			this.groupSchedule.Controls.Add(this.labelAfter);
			this.groupSchedule.Controls.Add(this.labelWeekdays);
			this.groupSchedule.Controls.Add(this.checkSun);
			this.groupSchedule.Controls.Add(this.checkMon);
			this.groupSchedule.Controls.Add(this.checkTue);
			this.groupSchedule.Controls.Add(this.checkWed);
			this.groupSchedule.Controls.Add(this.checkThu);
			this.groupSchedule.Controls.Add(this.checkFri);
			this.groupSchedule.Controls.Add(this.checkSat);
			this.groupSchedule.Controls.Add(this.labelAnchor);
			this.groupSchedule.Controls.Add(this.textAnchor);
			this.groupSchedule.Controls.Add(this.labelLead);
			this.groupSchedule.Controls.Add(this.textLead);
			this.groupSchedule.Controls.Add(this.labelLeadDays);
			this.groupSchedule.Controls.Add(this.labelNextDue);
			this.groupSchedule.Location = new System.Drawing.Point(12, 148);
			this.groupSchedule.Name = "groupSchedule";
			this.groupSchedule.Size = new System.Drawing.Size(425, 200);
			this.groupSchedule.TabIndex = 6;
			this.groupSchedule.Text = "Schedule";
			//
			// labelScheduleType
			//
			this.labelScheduleType.Location = new System.Drawing.Point(6, 22);
			this.labelScheduleType.Name = "labelScheduleType";
			this.labelScheduleType.Size = new System.Drawing.Size(104, 20);
			this.labelScheduleType.TabIndex = 0;
			this.labelScheduleType.Text = "Repeats";
			this.labelScheduleType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboScheduleType
			//
			this.comboScheduleType.Location = new System.Drawing.Point(113, 22);
			this.comboScheduleType.Name = "comboScheduleType";
			this.comboScheduleType.Size = new System.Drawing.Size(300, 21);
			this.comboScheduleType.TabIndex = 1;
			this.comboScheduleType.SelectionChangeCommitted += new System.EventHandler(this.comboScheduleType_SelectionChangeCommitted);
			//
			// labelEvery
			//
			this.labelEvery.Location = new System.Drawing.Point(6, 50);
			this.labelEvery.Name = "labelEvery";
			this.labelEvery.Size = new System.Drawing.Size(104, 20);
			this.labelEvery.TabIndex = 2;
			this.labelEvery.Text = "Every";
			this.labelEvery.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textCount
			//
			this.textCount.Location = new System.Drawing.Point(113, 50);
			this.textCount.MaxVal = 1000;
			this.textCount.MinVal = 1;
			this.textCount.Name = "textCount";
			this.textCount.ShowZero = false;
			this.textCount.Size = new System.Drawing.Size(50, 20);
			this.textCount.TabIndex = 3;
			this.textCount.TextChanged += new System.EventHandler(this.Schedule_Changed);
			//
			// comboUnit
			//
			this.comboUnit.Location = new System.Drawing.Point(169, 50);
			this.comboUnit.Name = "comboUnit";
			this.comboUnit.Size = new System.Drawing.Size(100, 21);
			this.comboUnit.TabIndex = 4;
			this.comboUnit.SelectionChangeCommitted += new System.EventHandler(this.Schedule_Changed);
			//
			// labelAfter
			//
			this.labelAfter.Location = new System.Drawing.Point(273, 50);
			this.labelAfter.Name = "labelAfter";
			this.labelAfter.Size = new System.Drawing.Size(140, 20);
			this.labelAfter.TabIndex = 5;
			this.labelAfter.Text = "after the last completion";
			this.labelAfter.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// labelWeekdays
			//
			this.labelWeekdays.Location = new System.Drawing.Point(6, 78);
			this.labelWeekdays.Name = "labelWeekdays";
			this.labelWeekdays.Size = new System.Drawing.Size(104, 20);
			this.labelWeekdays.TabIndex = 6;
			this.labelWeekdays.Text = "On";
			this.labelWeekdays.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// checkSun
			//
			this.checkSun.Location = new System.Drawing.Point(113, 78);
			this.checkSun.Name = "checkSun";
			this.checkSun.Size = new System.Drawing.Size(43, 20);
			this.checkSun.TabIndex = 7;
			this.checkSun.Text = "Sun";
			this.checkSun.Click += new System.EventHandler(this.Schedule_Changed);
			//
			// checkMon
			//
			this.checkMon.Location = new System.Drawing.Point(156, 78);
			this.checkMon.Name = "checkMon";
			this.checkMon.Size = new System.Drawing.Size(43, 20);
			this.checkMon.TabIndex = 8;
			this.checkMon.Text = "Mon";
			this.checkMon.Click += new System.EventHandler(this.Schedule_Changed);
			//
			// checkTue
			//
			this.checkTue.Location = new System.Drawing.Point(199, 78);
			this.checkTue.Name = "checkTue";
			this.checkTue.Size = new System.Drawing.Size(43, 20);
			this.checkTue.TabIndex = 9;
			this.checkTue.Text = "Tue";
			this.checkTue.Click += new System.EventHandler(this.Schedule_Changed);
			//
			// checkWed
			//
			this.checkWed.Location = new System.Drawing.Point(242, 78);
			this.checkWed.Name = "checkWed";
			this.checkWed.Size = new System.Drawing.Size(43, 20);
			this.checkWed.TabIndex = 10;
			this.checkWed.Text = "Wed";
			this.checkWed.Click += new System.EventHandler(this.Schedule_Changed);
			//
			// checkThu
			//
			this.checkThu.Location = new System.Drawing.Point(285, 78);
			this.checkThu.Name = "checkThu";
			this.checkThu.Size = new System.Drawing.Size(43, 20);
			this.checkThu.TabIndex = 11;
			this.checkThu.Text = "Thu";
			this.checkThu.Click += new System.EventHandler(this.Schedule_Changed);
			//
			// checkFri
			//
			this.checkFri.Location = new System.Drawing.Point(328, 78);
			this.checkFri.Name = "checkFri";
			this.checkFri.Size = new System.Drawing.Size(43, 20);
			this.checkFri.TabIndex = 12;
			this.checkFri.Text = "Fri";
			this.checkFri.Click += new System.EventHandler(this.Schedule_Changed);
			//
			// checkSat
			//
			this.checkSat.Location = new System.Drawing.Point(371, 78);
			this.checkSat.Name = "checkSat";
			this.checkSat.Size = new System.Drawing.Size(43, 20);
			this.checkSat.TabIndex = 13;
			this.checkSat.Text = "Sat";
			this.checkSat.Click += new System.EventHandler(this.Schedule_Changed);
			//
			// labelAnchor
			//
			this.labelAnchor.Location = new System.Drawing.Point(6, 106);
			this.labelAnchor.Name = "labelAnchor";
			this.labelAnchor.Size = new System.Drawing.Size(104, 20);
			this.labelAnchor.TabIndex = 14;
			this.labelAnchor.Text = "First due date";
			this.labelAnchor.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textAnchor
			//
			this.textAnchor.Location = new System.Drawing.Point(113, 106);
			this.textAnchor.Name = "textAnchor";
			this.textAnchor.Size = new System.Drawing.Size(100, 20);
			this.textAnchor.TabIndex = 15;
			this.textAnchor.TextChanged += new System.EventHandler(this.Schedule_Changed);
			//
			// labelLead
			//
			this.labelLead.Location = new System.Drawing.Point(6, 134);
			this.labelLead.Name = "labelLead";
			this.labelLead.Size = new System.Drawing.Size(104, 20);
			this.labelLead.TabIndex = 16;
			this.labelLead.Text = "Remind";
			this.labelLead.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textLead
			//
			this.textLead.Location = new System.Drawing.Point(113, 134);
			this.textLead.MaxVal = 365;
			this.textLead.MinVal = 0;
			this.textLead.Name = "textLead";
			this.textLead.Size = new System.Drawing.Size(50, 20);
			this.textLead.TabIndex = 17;
			this.textLead.TextChanged += new System.EventHandler(this.Schedule_Changed);
			//
			// labelLeadDays
			//
			this.labelLeadDays.Location = new System.Drawing.Point(169, 134);
			this.labelLeadDays.Name = "labelLeadDays";
			this.labelLeadDays.Size = new System.Drawing.Size(244, 20);
			this.labelLeadDays.TabIndex = 18;
			this.labelLeadDays.Text = "days before the due date";
			this.labelLeadDays.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// labelNextDue
			//
			this.labelNextDue.Location = new System.Drawing.Point(10, 164);
			this.labelNextDue.Name = "labelNextDue";
			this.labelNextDue.Size = new System.Drawing.Size(405, 30);
			this.labelNextDue.TabIndex = 19;
			this.labelNextDue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// labelAssigned
			//
			this.labelAssigned.Location = new System.Drawing.Point(12, 358);
			this.labelAssigned.Name = "labelAssigned";
			this.labelAssigned.Size = new System.Drawing.Size(110, 20);
			this.labelAssigned.TabIndex = 7;
			this.labelAssigned.Text = "Assigned to";
			this.labelAssigned.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// comboAssigned
			//
			this.comboAssigned.Location = new System.Drawing.Point(125, 358);
			this.comboAssigned.Name = "comboAssigned";
			this.comboAssigned.Size = new System.Drawing.Size(312, 21);
			this.comboAssigned.TabIndex = 8;
			//
			// checkHidden
			//
			this.checkHidden.Location = new System.Drawing.Point(125, 386);
			this.checkHidden.Name = "checkHidden";
			this.checkHidden.Size = new System.Drawing.Size(312, 20);
			this.checkHidden.TabIndex = 9;
			this.checkHidden.Text = "Hidden (retired; its history is kept)";
			this.checkHidden.Click += new System.EventHandler(this.Schedule_Changed);
			//
			// gridSteps
			//
			this.gridSteps.Location = new System.Drawing.Point(452, 15);
			this.gridSteps.Name = "gridSteps";
			this.gridSteps.Size = new System.Drawing.Size(436, 230);
			this.gridSteps.TabIndex = 10;
			this.gridSteps.Title = "Checklist";
			this.gridSteps.TranslationName = "TableMaintSteps";
			this.gridSteps.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridSteps_CellDoubleClick);
			//
			// butStepAdd
			//
			this.butStepAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butStepAdd.Location = new System.Drawing.Point(452, 251);
			this.butStepAdd.Name = "butStepAdd";
			this.butStepAdd.Size = new System.Drawing.Size(75, 24);
			this.butStepAdd.TabIndex = 11;
			this.butStepAdd.Text = "Add";
			this.butStepAdd.Click += new System.EventHandler(this.butStepAdd_Click);
			//
			// butStepEdit
			//
			this.butStepEdit.Location = new System.Drawing.Point(533, 251);
			this.butStepEdit.Name = "butStepEdit";
			this.butStepEdit.Size = new System.Drawing.Size(75, 24);
			this.butStepEdit.TabIndex = 12;
			this.butStepEdit.Text = "Edit";
			this.butStepEdit.Click += new System.EventHandler(this.butStepEdit_Click);
			//
			// butStepDelete
			//
			this.butStepDelete.Location = new System.Drawing.Point(614, 251);
			this.butStepDelete.Name = "butStepDelete";
			this.butStepDelete.Size = new System.Drawing.Size(75, 24);
			this.butStepDelete.TabIndex = 13;
			this.butStepDelete.Text = "Delete";
			this.butStepDelete.Click += new System.EventHandler(this.butStepDelete_Click);
			//
			// butStepUp
			//
			this.butStepUp.Location = new System.Drawing.Point(732, 251);
			this.butStepUp.Name = "butStepUp";
			this.butStepUp.Size = new System.Drawing.Size(75, 24);
			this.butStepUp.TabIndex = 14;
			this.butStepUp.Text = "Up";
			this.butStepUp.Click += new System.EventHandler(this.butStepUp_Click);
			//
			// butStepDown
			//
			this.butStepDown.Location = new System.Drawing.Point(813, 251);
			this.butStepDown.Name = "butStepDown";
			this.butStepDown.Size = new System.Drawing.Size(75, 24);
			this.butStepDown.TabIndex = 15;
			this.butStepDown.Text = "Down";
			this.butStepDown.Click += new System.EventHandler(this.butStepDown_Click);
			//
			// gridFields
			//
			this.gridFields.Location = new System.Drawing.Point(452, 290);
			this.gridFields.Name = "gridFields";
			this.gridFields.Size = new System.Drawing.Size(436, 230);
			this.gridFields.TabIndex = 16;
			this.gridFields.Title = "Result Fields";
			this.gridFields.TranslationName = "TableMaintFields";
			this.gridFields.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridFields_CellDoubleClick);
			//
			// butFieldAdd
			//
			this.butFieldAdd.Icon = OpenPmsDental.UI.EnumIcons.Add;
			this.butFieldAdd.Location = new System.Drawing.Point(452, 526);
			this.butFieldAdd.Name = "butFieldAdd";
			this.butFieldAdd.Size = new System.Drawing.Size(75, 24);
			this.butFieldAdd.TabIndex = 17;
			this.butFieldAdd.Text = "Add";
			this.butFieldAdd.Click += new System.EventHandler(this.butFieldAdd_Click);
			//
			// butFieldEdit
			//
			this.butFieldEdit.Location = new System.Drawing.Point(533, 526);
			this.butFieldEdit.Name = "butFieldEdit";
			this.butFieldEdit.Size = new System.Drawing.Size(75, 24);
			this.butFieldEdit.TabIndex = 18;
			this.butFieldEdit.Text = "Edit";
			this.butFieldEdit.Click += new System.EventHandler(this.butFieldEdit_Click);
			//
			// butFieldDelete
			//
			this.butFieldDelete.Location = new System.Drawing.Point(614, 526);
			this.butFieldDelete.Name = "butFieldDelete";
			this.butFieldDelete.Size = new System.Drawing.Size(75, 24);
			this.butFieldDelete.TabIndex = 19;
			this.butFieldDelete.Text = "Delete";
			this.butFieldDelete.Click += new System.EventHandler(this.butFieldDelete_Click);
			//
			// butFieldUp
			//
			this.butFieldUp.Location = new System.Drawing.Point(732, 526);
			this.butFieldUp.Name = "butFieldUp";
			this.butFieldUp.Size = new System.Drawing.Size(75, 24);
			this.butFieldUp.TabIndex = 20;
			this.butFieldUp.Text = "Up";
			this.butFieldUp.Click += new System.EventHandler(this.butFieldUp_Click);
			//
			// butFieldDown
			//
			this.butFieldDown.Location = new System.Drawing.Point(813, 526);
			this.butFieldDown.Name = "butFieldDown";
			this.butFieldDown.Size = new System.Drawing.Size(75, 24);
			this.butFieldDown.TabIndex = 21;
			this.butFieldDown.Text = "Down";
			this.butFieldDown.Click += new System.EventHandler(this.butFieldDown_Click);
			//
			// butDelete
			//
			this.butDelete.Icon = OpenPmsDental.UI.EnumIcons.DeleteX;
			this.butDelete.Location = new System.Drawing.Point(12, 572);
			this.butDelete.Name = "butDelete";
			this.butDelete.Size = new System.Drawing.Size(80, 24);
			this.butDelete.TabIndex = 22;
			this.butDelete.Text = "Delete";
			this.butDelete.Click += new System.EventHandler(this.butDelete_Click);
			//
			// butSave
			//
			this.butSave.Location = new System.Drawing.Point(813, 572);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 23;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormMaintItemEdit
			//
			this.ClientSize = new System.Drawing.Size(900, 608);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.butDelete);
			this.Controls.Add(this.butFieldDown);
			this.Controls.Add(this.butFieldUp);
			this.Controls.Add(this.butFieldDelete);
			this.Controls.Add(this.butFieldEdit);
			this.Controls.Add(this.butFieldAdd);
			this.Controls.Add(this.gridFields);
			this.Controls.Add(this.butStepDown);
			this.Controls.Add(this.butStepUp);
			this.Controls.Add(this.butStepDelete);
			this.Controls.Add(this.butStepEdit);
			this.Controls.Add(this.butStepAdd);
			this.Controls.Add(this.gridSteps);
			this.Controls.Add(this.checkHidden);
			this.Controls.Add(this.comboAssigned);
			this.Controls.Add(this.labelAssigned);
			this.Controls.Add(this.groupSchedule);
			this.Controls.Add(this.comboEquipment);
			this.Controls.Add(this.labelEquipment);
			this.Controls.Add(this.textDescription);
			this.Controls.Add(this.labelDescription);
			this.Controls.Add(this.textName);
			this.Controls.Add(this.labelName);
			this.Name = "FormMaintItemEdit";
			this.Text = "Maintenance Item";
			this.Load += new System.EventHandler(this.FormMaintItemEdit_Load);
			this.groupSchedule.ResumeLayout(false);
			this.groupSchedule.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelName;
		private System.Windows.Forms.TextBox textName;
		private System.Windows.Forms.Label labelDescription;
		private OpenPmsDental.ODtextBox textDescription;
		private System.Windows.Forms.Label labelEquipment;
		private OpenPmsDental.UI.ComboBox comboEquipment;
		private OpenPmsDental.UI.GroupBox groupSchedule;
		private System.Windows.Forms.Label labelScheduleType;
		private OpenPmsDental.UI.ComboBox comboScheduleType;
		private System.Windows.Forms.Label labelEvery;
		private OpenPmsDental.ValidNum textCount;
		private OpenPmsDental.UI.ComboBox comboUnit;
		private System.Windows.Forms.Label labelAfter;
		private System.Windows.Forms.Label labelWeekdays;
		private OpenPmsDental.UI.CheckBox checkSun;
		private OpenPmsDental.UI.CheckBox checkMon;
		private OpenPmsDental.UI.CheckBox checkTue;
		private OpenPmsDental.UI.CheckBox checkWed;
		private OpenPmsDental.UI.CheckBox checkThu;
		private OpenPmsDental.UI.CheckBox checkFri;
		private OpenPmsDental.UI.CheckBox checkSat;
		private System.Windows.Forms.Label labelAnchor;
		private OpenPmsDental.ValidDate textAnchor;
		private System.Windows.Forms.Label labelLead;
		private OpenPmsDental.ValidNum textLead;
		private System.Windows.Forms.Label labelLeadDays;
		private System.Windows.Forms.Label labelNextDue;
		private System.Windows.Forms.Label labelAssigned;
		private OpenPmsDental.UI.ComboBox comboAssigned;
		private OpenPmsDental.UI.CheckBox checkHidden;
		private OpenPmsDental.UI.GridOD gridSteps;
		private OpenPmsDental.UI.Button butStepAdd;
		private OpenPmsDental.UI.Button butStepEdit;
		private OpenPmsDental.UI.Button butStepDelete;
		private OpenPmsDental.UI.Button butStepUp;
		private OpenPmsDental.UI.Button butStepDown;
		private OpenPmsDental.UI.GridOD gridFields;
		private OpenPmsDental.UI.Button butFieldAdd;
		private OpenPmsDental.UI.Button butFieldEdit;
		private OpenPmsDental.UI.Button butFieldDelete;
		private OpenPmsDental.UI.Button butFieldUp;
		private OpenPmsDental.UI.Button butFieldDown;
		private OpenPmsDental.UI.Button butDelete;
		private OpenPmsDental.UI.Button butSave;
	}
}
