namespace OpenPmsDental {
	partial class FormMaintRecordEdit {
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
			this.labelItem = new System.Windows.Forms.Label();
			this.textDescription = new System.Windows.Forms.TextBox();
			this.labelDue = new System.Windows.Forms.Label();
			this.labelDateDone = new System.Windows.Forms.Label();
			this.textDateDone = new OpenPmsDental.ValidDate();
			this.labelDoneBy = new System.Windows.Forms.Label();
			this.gridSteps = new OpenPmsDental.UI.GridOD();
			this.butCheckAll = new OpenPmsDental.UI.Button();
			this.gridFields = new OpenPmsDental.UI.GridOD();
			this.butEnterValue = new OpenPmsDental.UI.Button();
			this.labelNote = new System.Windows.Forms.Label();
			this.textNote = new OpenPmsDental.ODtextBox();
			this.butSave = new OpenPmsDental.UI.Button();
			this.SuspendLayout();
			//
			// labelItem
			//
			this.labelItem.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.labelItem.Location = new System.Drawing.Point(12, 10);
			this.labelItem.Name = "labelItem";
			this.labelItem.Size = new System.Drawing.Size(736, 20);
			this.labelItem.TabIndex = 0;
			this.labelItem.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// textDescription
			//
			this.textDescription.BackColor = System.Drawing.SystemColors.Control;
			this.textDescription.Location = new System.Drawing.Point(12, 34);
			this.textDescription.Multiline = true;
			this.textDescription.Name = "textDescription";
			this.textDescription.ReadOnly = true;
			this.textDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textDescription.Size = new System.Drawing.Size(736, 50);
			this.textDescription.TabIndex = 1;
			this.textDescription.TabStop = false;
			//
			// labelDue
			//
			this.labelDue.Location = new System.Drawing.Point(12, 92);
			this.labelDue.Name = "labelDue";
			this.labelDue.Size = new System.Drawing.Size(250, 20);
			this.labelDue.TabIndex = 2;
			this.labelDue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			//
			// labelDateDone
			//
			this.labelDateDone.Location = new System.Drawing.Point(270, 92);
			this.labelDateDone.Name = "labelDateDone";
			this.labelDateDone.Size = new System.Drawing.Size(80, 20);
			this.labelDateDone.TabIndex = 3;
			this.labelDateDone.Text = "Date done";
			this.labelDateDone.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textDateDone
			//
			this.textDateDone.Location = new System.Drawing.Point(353, 92);
			this.textDateDone.Name = "textDateDone";
			this.textDateDone.Size = new System.Drawing.Size(100, 20);
			this.textDateDone.TabIndex = 4;
			//
			// labelDoneBy
			//
			this.labelDoneBy.Location = new System.Drawing.Point(470, 92);
			this.labelDoneBy.Name = "labelDoneBy";
			this.labelDoneBy.Size = new System.Drawing.Size(278, 20);
			this.labelDoneBy.TabIndex = 5;
			this.labelDoneBy.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// gridSteps
			//
			this.gridSteps.Location = new System.Drawing.Point(12, 120);
			this.gridSteps.Name = "gridSteps";
			this.gridSteps.Size = new System.Drawing.Size(736, 165);
			this.gridSteps.TabIndex = 6;
			this.gridSteps.Title = "Checklist (click a step to tick it)";
			this.gridSteps.TranslationName = "TableMaintRecordSteps";
			this.gridSteps.CellClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridSteps_CellClick);
			//
			// butCheckAll
			//
			this.butCheckAll.Location = new System.Drawing.Point(12, 290);
			this.butCheckAll.Name = "butCheckAll";
			this.butCheckAll.Size = new System.Drawing.Size(90, 24);
			this.butCheckAll.TabIndex = 7;
			this.butCheckAll.Text = "Tick All";
			this.butCheckAll.Click += new System.EventHandler(this.butCheckAll_Click);
			//
			// gridFields
			//
			this.gridFields.Location = new System.Drawing.Point(12, 322);
			this.gridFields.Name = "gridFields";
			this.gridFields.Size = new System.Drawing.Size(736, 165);
			this.gridFields.TabIndex = 8;
			this.gridFields.Title = "Results (double-click a result to enter it)";
			this.gridFields.TranslationName = "TableMaintRecordFields";
			this.gridFields.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridFields_CellDoubleClick);
			//
			// butEnterValue
			//
			this.butEnterValue.Location = new System.Drawing.Point(12, 492);
			this.butEnterValue.Name = "butEnterValue";
			this.butEnterValue.Size = new System.Drawing.Size(90, 24);
			this.butEnterValue.TabIndex = 9;
			this.butEnterValue.Text = "Enter Value";
			this.butEnterValue.Click += new System.EventHandler(this.butEnterValue_Click);
			//
			// labelNote
			//
			this.labelNote.Location = new System.Drawing.Point(12, 524);
			this.labelNote.Name = "labelNote";
			this.labelNote.Size = new System.Drawing.Size(60, 20);
			this.labelNote.TabIndex = 10;
			this.labelNote.Text = "Note";
			this.labelNote.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// textNote
			//
			this.textNote.AcceptsTab = true;
			this.textNote.BackColor = System.Drawing.SystemColors.Window;
			this.textNote.DetectLinksEnabled = false;
			this.textNote.DetectUrls = false;
			this.textNote.Location = new System.Drawing.Point(75, 524);
			this.textNote.Name = "textNote";
			this.textNote.QuickPasteType = OpenPmsDentalBusiness.EnumQuickPasteType.Equipment;
			this.textNote.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
			this.textNote.Size = new System.Drawing.Size(673, 50);
			this.textNote.TabIndex = 11;
			this.textNote.Text = "";
			//
			// butSave
			//
			this.butSave.Location = new System.Drawing.Point(673, 584);
			this.butSave.Name = "butSave";
			this.butSave.Size = new System.Drawing.Size(75, 24);
			this.butSave.TabIndex = 12;
			this.butSave.Text = "&Save";
			this.butSave.Click += new System.EventHandler(this.butSave_Click);
			//
			// FormMaintRecordEdit
			//
			this.ClientSize = new System.Drawing.Size(760, 620);
			this.Controls.Add(this.butSave);
			this.Controls.Add(this.textNote);
			this.Controls.Add(this.labelNote);
			this.Controls.Add(this.butEnterValue);
			this.Controls.Add(this.gridFields);
			this.Controls.Add(this.butCheckAll);
			this.Controls.Add(this.gridSteps);
			this.Controls.Add(this.labelDoneBy);
			this.Controls.Add(this.textDateDone);
			this.Controls.Add(this.labelDateDone);
			this.Controls.Add(this.labelDue);
			this.Controls.Add(this.textDescription);
			this.Controls.Add(this.labelItem);
			this.Name = "FormMaintRecordEdit";
			this.Text = "Record Maintenance";
			this.Load += new System.EventHandler(this.FormMaintRecordEdit_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelItem;
		private System.Windows.Forms.TextBox textDescription;
		private System.Windows.Forms.Label labelDue;
		private System.Windows.Forms.Label labelDateDone;
		private OpenPmsDental.ValidDate textDateDone;
		private System.Windows.Forms.Label labelDoneBy;
		private OpenPmsDental.UI.GridOD gridSteps;
		private OpenPmsDental.UI.Button butCheckAll;
		private OpenPmsDental.UI.GridOD gridFields;
		private OpenPmsDental.UI.Button butEnterValue;
		private System.Windows.Forms.Label labelNote;
		private OpenPmsDental.ODtextBox textNote;
		private OpenPmsDental.UI.Button butSave;
	}
}
