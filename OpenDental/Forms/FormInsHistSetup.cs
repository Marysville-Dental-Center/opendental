using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Insurance History: two dates per category (Open Dental 25.1), each with Edit (date picker) and Remove (25.2), also on the right-click
	///menu of the date. The rules are in InsHistDates. Dates are applied when the user clicks Save.</summary>
	public partial class FormInsHistSetup:FormODBase {
		private Patient _patient;
		///<summary>EO and C procedures associated with the InsHist and Received claimprocs for the inssub passed into the constructor.</summary>
		private List<Procedure> _listProceduresEoAndC;
		///<summary>InsHist and Received claimprocs associated with EO and C procedures for the inssub passed into the constructor.</summary>
		private List<ClaimProc> _listClaimProcsForEoAndCProcs;
		private InsSub _insSub;
		private const string NO_INSHISTSET="Not Set";
		///<summary>One row per category, in the order of Prefs.GetInsHistPrefNames.</summary>
		private List<CategoryRow> _listCategoryRows=new List<CategoryRow>();
		///<summary>Shared by every date box. Its SourceControl says which box was right-clicked.</summary>
		private ContextMenu _contextMenuDate;
		private MenuItem _menuItemEdit;
		private MenuItem _menuItemRemove;
		private const int ROW_TOP=64;
		private const int ROW_HEIGHT=24;

		public FormInsHistSetup(long patNum,InsSub insSub) {
			InitializeComponent();
			AddCategoryRows();//Before the layout manager, so the rows are scaled like controls from the designer.
			InitializeLayoutManager();
			Lan.F(this);
			_patient=Patients.GetPat(patNum);
			_insSub=insSub;
		}

		///<summary>Adds a label, two date boxes and their Edit and Remove buttons for each category, and sizes the window to fit.</summary>
		private void AddCategoryRows() {
			_contextMenuDate=new ContextMenu();
			_menuItemEdit=new MenuItem(Lan.g(this,"Edit Date"),menuItemEdit_Click);
			_menuItemRemove=new MenuItem(Lan.g(this,"Remove Date"),menuItemRemove_Click);
			_contextMenuDate.MenuItems.Add(_menuItemEdit);
			_contextMenuDate.MenuItems.Add(_menuItemRemove);
			_contextMenuDate.Popup+=contextMenuDate_Popup;
			Disposed+=(sender,e) => _contextMenuDate.Dispose();
			List<PrefName> listPrefNames=Prefs.GetInsHistPrefNames();
			int y=ROW_TOP;
			int tabIndex=10;
			for(int i=0;i<listPrefNames.Count;i++) {
				CategoryRow categoryRow=new CategoryRow();
				categoryRow.PrefName=listPrefNames[i];
				categoryRow.Label=new Label();
				categoryRow.Label.Location=new Point(7,y+2);
				categoryRow.Label.Size=new Size(138,17);
				categoryRow.Label.Text=InsHistDates.GetCategoryName(listPrefNames[i]);
				categoryRow.Label.TextAlign=ContentAlignment.MiddleRight;
				categoryRow.Label.Name="labelInsHist"+listPrefNames[i].ToString();
				Controls.Add(categoryRow.Label);
				for(int slot=0;slot<InsHistDates.DATES_PER_CATEGORY;slot++) {
					int x=146+slot*190;
					TextBox textBox=new TextBox();
					textBox.Location=new Point(x,y);
					textBox.Size=new Size(80,20);
					textBox.Name="textDate"+listPrefNames[i].ToString()+slot.ToString();
					textBox.TabIndex=tabIndex++;
					textBox.ContextMenu=_contextMenuDate;
					textBox.Validating+=TextBoxValidating;
					textBox.TextChanged+=textDate_TextChanged;
					Controls.Add(textBox);
					UI.Button butEdit=new UI.Button();
					butEdit.Location=new Point(x+82,y-1);
					butEdit.Size=new Size(40,22);
					butEdit.Text="Edit";
					butEdit.Name="butEdit"+listPrefNames[i].ToString()+slot.ToString();
					butEdit.TabIndex=tabIndex++;
					butEdit.Click+=butEdit_Click;
					Controls.Add(butEdit);
					UI.Button butRemove=new UI.Button();
					butRemove.Location=new Point(x+124,y-1);
					butRemove.Size=new Size(56,22);
					butRemove.Text="Remove";
					butRemove.Name="butRemove"+listPrefNames[i].ToString()+slot.ToString();
					butRemove.TabIndex=tabIndex++;
					butRemove.Click+=butRemove_Click;
					Controls.Add(butRemove);
					DateSlot dateSlot=new DateSlot();
					dateSlot.CategoryRow=categoryRow;
					dateSlot.TextBox=textBox;
					dateSlot.ButEdit=butEdit;
					dateSlot.ButRemove=butRemove;
					textBox.Tag=dateSlot;
					butEdit.Tag=dateSlot;
					butRemove.Tag=dateSlot;
					categoryRow.ListDateSlots.Add(dateSlot);
				}
				_listCategoryRows.Add(categoryRow);
				y+=ROW_HEIGHT;
			}
			butSave.Location=new Point(441,y+8);
			ClientSize=new Size(530,y+44);
		}

		private void FormInsHistSetup_Load(object sender,EventArgs e) {
			_listProceduresEoAndC=InsHistDates.GetProcsForInsSub(_patient.PatNum,_insSub.InsSubNum,out _listClaimProcsForEoAndCProcs);
			List<Pref> listPrefs=Prefs.GetInsHistPrefs();
			for(int i=0;i<_listCategoryRows.Count;i++) {
				CategoryRow categoryRow=_listCategoryRows[i];
				//Check to see if this InsHistPref has been set by the user.
				categoryRow.IsSet=listPrefs.Exists(x => x.PrefName==categoryRow.PrefName.ToString() && !string.IsNullOrWhiteSpace(x.ValueString));
				List<Procedure> listProcedures=new List<Procedure>();
				if(categoryRow.IsSet) {
					List<long> listCodeNums=ProcedureCodes.GetCodeNumsForInsHistPref(categoryRow.PrefName);
					listProcedures=InsHistDates.GetMostRecentProcs(_listProceduresEoAndC,listCodeNums,categoryRow.PrefName);
				}
				for(int slot=0;slot<categoryRow.ListDateSlots.Count;slot++) {
					DateSlot dateSlot=categoryRow.ListDateSlots[slot];
					dateSlot.InsHistBox=new InsHistBox();
					if(slot<listProcedures.Count) {
						dateSlot.InsHistBox.ProcedureLoaded=listProcedures[slot];
					}
					FillSlot(dateSlot);
				}
			}
		}

		///<summary>Shows the date a box loaded with and sets what can be done with it.</summary>
		private void FillSlot(DateSlot dateSlot) {
			TextBox textBox=dateSlot.TextBox;
			if(!dateSlot.CategoryRow.IsSet) {
				//The user has not set this InsHistPref up.
				textBox.Text=Lan.g(this,NO_INSHISTSET);
				textBox.Enabled=false;
				SetButtons(dateSlot);
				return;
			}
			DateTime dateLoaded=dateSlot.InsHistBox.GetDateLoaded();
			textBox.Text=dateLoaded==DateTime.MinValue ? "" : dateLoaded.ToShortDateString();
			if(dateSlot.InsHistBox.IsReadOnly()) {
				textBox.ReadOnly=true;
				toolTip.SetToolTip(textBox,Lan.g(this,"From a completed procedure on")+" "+dateLoaded.ToShortDateString()+"; "
					+Lan.g(this,"edit it in the Chart."));
			}
			SetButtons(dateSlot);
		}

		///<summary>Edit is available for an empty box and for an EO date. Remove only when there is a date to remove. Completed dates allow neither.
		///</summary>
		private void SetButtons(DateSlot dateSlot) {
			bool canChange=dateSlot.TextBox.Enabled && !dateSlot.TextBox.ReadOnly;
			dateSlot.ButEdit.Enabled=canChange;
			dateSlot.ButRemove.Enabled=canChange && dateSlot.TextBox.Text.Trim()!="";
		}

		private void textDate_TextChanged(object sender,EventArgs e) {
			if(((Control)sender).Tag is DateSlot dateSlot && dateSlot.InsHistBox!=null) {
				SetButtons(dateSlot);
			}
		}

		private void butEdit_Click(object sender,EventArgs e) {
			EditDate((DateSlot)((Control)sender).Tag);
		}

		private void butRemove_Click(object sender,EventArgs e) {
			RemoveDate((DateSlot)((Control)sender).Tag);
		}

		private void contextMenuDate_Popup(object sender,EventArgs e) {
			DateSlot dateSlot=_contextMenuDate.SourceControl?.Tag as DateSlot;
			_menuItemEdit.Enabled=dateSlot!=null && dateSlot.ButEdit.Enabled;
			_menuItemRemove.Enabled=dateSlot!=null && dateSlot.ButRemove.Enabled;
		}

		private void menuItemEdit_Click(object sender,EventArgs e) {
			if(_contextMenuDate.SourceControl?.Tag is DateSlot dateSlot) {
				EditDate(dateSlot);
			}
		}

		private void menuItemRemove_Click(object sender,EventArgs e) {
			if(_contextMenuDate.SourceControl?.Tag is DateSlot dateSlot) {
				RemoveDate(dateSlot);
			}
		}

		///<summary>Picks a date for the box from a calendar. Changing an EO date needs ProcExistingEdit; it is checked here so the user finds out
		///early, and again when saving.</summary>
		private void EditDate(DateSlot dateSlot) {
			if(!dateSlot.ButEdit.Enabled) {
				return;
			}
			Procedure procedure=dateSlot.InsHistBox.ProcedureLoaded;
			if(procedure!=null && !Security.IsAuthorized(EnumPermType.ProcExistingEdit,Procedures.GetDateForPermCheck(procedure))) {
				return;
			}
			DateTime date=PIn.Date(dateSlot.TextBox.Text);
			if(date.Year<1880 || date.Year>2100) {
				date=DateTime.Today;
			}
			using FormCalendar formCalendar=new FormCalendar();
			formCalendar.MinDate=new DateTime(1880,1,1);
			formCalendar.DateSelected=date;
			if(formCalendar.ShowDialog()!=DialogResult.OK) {
				return;
			}
			DateTime dateSelected=formCalendar.DateSelected;//A local copy: the form is a marshal-by-reference object.
			dateSlot.TextBox.Text=dateSelected.ToShortDateString();
		}

		///<summary>Clears the box. The EO procedure is deleted when the user saves and confirms. Needs ProcDelete with its date restrictions.</summary>
		private void RemoveDate(DateSlot dateSlot) {
			if(!dateSlot.ButRemove.Enabled) {
				return;
			}
			Procedure procedure=dateSlot.InsHistBox.ProcedureLoaded;
			if(procedure!=null && !Security.IsAuthorized(EnumPermType.ProcDelete,Procedures.GetDateForPermCheck(procedure))) {
				return;
			}
			dateSlot.TextBox.Text="";
		}

		///<summary>Reads the date boxes into the InsHistBoxes. Returns false after a message when a date is not valid.</summary>
		private bool ReadDates() {
			for(int i=0;i<_listCategoryRows.Count;i++) {
				for(int slot=0;slot<_listCategoryRows[i].ListDateSlots.Count;slot++) {
					DateSlot dateSlot=_listCategoryRows[i].ListDateSlots[slot];
					if(!dateSlot.TextBox.Enabled || dateSlot.InsHistBox.IsReadOnly()) {
						continue;
					}
					string text=dateSlot.TextBox.Text.Trim();
					if(text=="") {
						dateSlot.InsHistBox.DateEntered=DateTime.MinValue;
						continue;
					}
					if(!DateTime.TryParse(text,out DateTime dateEntry)) {
						MsgBox.Show(this,"Invalid date");
						return false;
					}
					if(dateEntry.Year<1880 || dateEntry.Year>2100) {
						MsgBox.Show(this,"Invalid date. Valid dates between 1880 and 2100");
						return false;
					}
					dateSlot.InsHistBox.DateEntered=dateEntry.Date;
				}
			}
			return true;
		}

		private void TextBoxValidating(object sender,System.ComponentModel.CancelEventArgs e) {
			if(sender.GetType()!=typeof(TextBox)) {
				return;
			}
			TextBox textBox=(TextBox)sender;
			//If its disabled, read-only or empty return.
			if(!textBox.Enabled || textBox.ReadOnly || string.IsNullOrEmpty(textBox.Text)) {
				return;
			}
			bool allNums=true;
			for(int i=0;i<textBox.Text.Length;i++) {
				if(!Char.IsNumber(textBox.Text,i)) {
					allNums=false;
				}
			}
			if(CultureInfo.CurrentCulture.TwoLetterISOLanguageName=="en") {
				if(allNums) {
					if(textBox.Text.Length==6) {
						textBox.Text=textBox.Text.Substring(0,2)+"/"+textBox.Text.Substring(2,2)+"/"+textBox.Text.Substring(4,2);
					}
					else if(textBox.Text.Length==8) {
						textBox.Text=textBox.Text.Substring(0,2)+"/"+textBox.Text.Substring(2,2)+"/"+textBox.Text.Substring(4,4);
					}
				}
			}
			try {
				textBox.Text=DateTime.Parse(textBox.Text).ToString("d");//will throw exception if invalid
			}
			catch(Exception ex) {
				//We don't want a full exception, just a popup.  Save will block them from putting invalid data in the db.
				ex.DoNothing();
				MsgBox.Show(this,"Invalid date.");
				return;
			}
			if(DateTime.Parse(textBox.Text).Year<1880 || DateTime.Parse(textBox.Text).Year>2100) {
				MsgBox.Show("Valid dates between 1880 and 2100");
				return;
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(!ReadDates()) {
				return;
			}
			List<InsHistAction> listActions=new List<InsHistAction>();
			for(int i=0;i<_listCategoryRows.Count;i++) {
				CategoryRow categoryRow=_listCategoryRows[i];
				if(!categoryRow.IsSet) {
					continue;
				}
				List<InsHistBox> listInsHistBoxes=categoryRow.ListDateSlots.Select(x => x.InsHistBox).ToList();
				listActions.AddRange(InsHistDates.GetActions(categoryRow.PrefName,listInsHistBoxes,out string error));
				if(error!="") {
					MessageBox.Show(error);
					return;
				}
			}
			for(int i=0;i<listActions.Count;i++) {
				if(InsHistDates.TryGetPermission(listActions[i],out EnumPermType enumPermType,out DateTime dateForPermCheck)
					&& !Security.IsAuthorized(enumPermType,dateForPermCheck))
				{
					return;
				}
			}
			List<InsHistAction> listActionsRemove=listActions.FindAll(x => x.ActionType==InsHistActionType.Remove);
			if(listActionsRemove.Count>0) {
				string message=Lan.g(this,"Removing these dates deletes the Existing Other procedure with that date for this patient:")+"\r\n"
					+string.Join("\r\n",listActionsRemove.Select(x => InsHistDates.GetCategoryName(x.PrefName)+": "+x.Procedure.ProcDate.ToShortDateString()))
					+"\r\n"+Lan.g(this,"Continue?");
				if(MessageBox.Show(message,"",MessageBoxButtons.YesNo)!=DialogResult.Yes) {
					return;
				}
			}
			if(listActions.Count==0) {
				DialogResult=DialogResult.OK;
				return;
			}
			List<string> listMessages=InsHistDates.ApplyActions(_patient,_insSub,listActions,_listClaimProcsForEoAndCProcs);
			List<Procedure> listProceduresAfter=InsHistDates.GetProcsForInsSub(_patient.PatNum,_insSub.InsSubNum,out List<ClaimProc> _);
			listMessages.AddRange(InsHistDates.GetNotesForHiddenDates(listActions,listProceduresAfter));
			if(listMessages.Count>0) {
				MessageBox.Show(string.Join("\r\n",listMessages));
			}
			DialogResult=DialogResult.OK;
		}

		///<summary>The controls of one category.</summary>
		private class CategoryRow {
			public PrefName PrefName;
			public Label Label;
			///<summary>True when the category's preference holds codes. Otherwise its boxes show "Not Set" and are disabled.</summary>
			public bool IsSet;
			///<summary>Most recent first.</summary>
			public List<DateSlot> ListDateSlots=new List<DateSlot>();
		}

		///<summary>One date box with its buttons.</summary>
		private class DateSlot {
			public CategoryRow CategoryRow;
			public TextBox TextBox;
			public UI.Button ButEdit;
			public UI.Button ButRemove;
			///<summary>Set on load.</summary>
			public InsHistBox InsHistBox;
		}
	}
}
