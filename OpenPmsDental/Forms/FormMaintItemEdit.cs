// Routine maintenance (docs/specs/routine-maintenance.md, "Item setup"): one maintenance item with its schedule, checklist and result fields.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Sets up one maintenance item: name, description, equipment link, schedule, reminder lead time, assignee, hidden, the ordered checklist
	///and the result fields. A new item's first due date (the anchor) is today unless the caller set one. Saving logs the change under
	///MaintenanceSetup. The caller checks MaintenanceSetup before opening it.</summary>
	public partial class FormMaintItemEdit:FormODBase {
		private MaintItem _maintItem;
		private MaintItem _maintItemOld;
		private bool _isNew;
		private List<MaintStep> _listMaintSteps=new List<MaintStep>();
		private List<MaintStep> _listMaintStepsOld=new List<MaintStep>();
		private List<MaintField> _listMaintFields=new List<MaintField>();
		private List<MaintField> _listMaintFieldsOld=new List<MaintField>();
		///<summary>The item's completions, for the "Next due" line.</summary>
		private List<MaintRecord> _listMaintRecords=new List<MaintRecord>();
		private DateTime _dateToday;
		private List<UI.CheckBox> _listCheckWeekdays;
		private const string ASSIGNEE_NONE="none";

		public FormMaintItemEdit(MaintItem maintItem,bool isNew) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_maintItem=maintItem;
			_maintItemOld=maintItem.Copy();
			_isNew=isNew;
		}

		private void FormMaintItemEdit_Load(object sender,EventArgs e) {
			_dateToday=MaintAlerts.GetToday();
			_listCheckWeekdays=new List<UI.CheckBox> { checkSun,checkMon,checkTue,checkWed,checkThu,checkFri,checkSat };
			string[] arrayDayNames=System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
			for(int i=0;i<7;i++) {
				_listCheckWeekdays[i].Text=arrayDayNames[i];
			}
			if(!_isNew) {
				_listMaintSteps=MaintSteps.GetForItem(_maintItem.MaintItemNum);
				_listMaintFields=MaintFields.GetForItem(_maintItem.MaintItemNum);
				_listMaintRecords=MaintRecords.GetForItems(new List<long> { _maintItem.MaintItemNum });
			}
			_listMaintStepsOld=_listMaintSteps.Select(x => x.Copy()).ToList();
			_listMaintFieldsOld=_listMaintFields.Select(x => x.Copy()).ToList();
			textName.Text=_maintItem.ItemName;
			textDescription.Text=_maintItem.Description;
			FillComboEquipment();
			comboScheduleType.Items.Clear();
			comboScheduleType.Items.Add(Lan.g(this,"Every N days, weeks, months or years"),EnumMaintScheduleType.Interval);
			comboScheduleType.Items.Add(Lan.g(this,"On weekdays"),EnumMaintScheduleType.Weekdays);
			comboScheduleType.Items.Add(Lan.g(this,"N days, weeks, months or years after last done"),EnumMaintScheduleType.AfterCompletion);
			comboScheduleType.SelectedIndex=Math.Max(0,comboScheduleType.Items.GetAll<EnumMaintScheduleType>().IndexOf(_maintItem.ScheduleType));
			textCount.Text=Math.Max(1,_maintItem.IntervalCount).ToString();
			FillComboUnit(_maintItem.IntervalUnit);
			for(int i=0;i<7;i++) {
				_listCheckWeekdays[i].Checked=MaintSchedules.HasWeekday(_maintItem.Weekdays,(DayOfWeek)i);
			}
			if(_isNew && _maintItem.DateAnchor.Year<1880) {
				//The anchor is the first due date; a new item starts today unless the office picks another day.
				_maintItem.DateAnchor=_dateToday;
			}
			if(_maintItem.DateAnchor.Year>1880) {
				textAnchor.Text=_maintItem.DateAnchor.ToShortDateString();
			}
			textLead.Text=_maintItem.LeadDays.ToString();
			FillComboAssigned();
			SetUnassignedLabel();
			checkHidden.Checked=_maintItem.IsHidden;
			butDelete.Visible=!_isNew;
			FillGridSteps();
			FillGridFields();
			SetScheduleControls();
			UpdateNextDue();
		}

		#region Combos
		private void FillComboEquipment() {
			List<Equipment> listEquipments=Equipments.GetList(DateTime.MinValue.AddDays(1),DateTime.MaxValue,EnumEquipmentDisplayMode.All,"")
				.OrderBy(x => x.Description).ToList();
			comboEquipment.Items.Clear();
			comboEquipment.Items.Add(Lan.g(this,"None"),0L);
			foreach(Equipment equipment in listEquipments) {
				comboEquipment.Items.Add(equipment.Description+(string.IsNullOrEmpty(equipment.SerialNumber) ? "" : " ("+equipment.SerialNumber+")"),
					equipment.EquipmentNum);
			}
			if(_maintItem.EquipmentNum!=0 && !listEquipments.Any(x => x.EquipmentNum==_maintItem.EquipmentNum)) {
				comboEquipment.Items.Add(Lan.g(this,"(not in the Equipment list)"),_maintItem.EquipmentNum);
			}
			comboEquipment.SelectedIndex=Math.Max(0,comboEquipment.Items.GetAll<long>().IndexOf(_maintItem.EquipmentNum));
		}

		///<summary>Days, weeks, months and years, for "every N" and for "N after the last completion" alike.</summary>
		private void FillComboUnit(EnumMaintIntervalUnit intervalUnitSelected) {
			comboUnit.Items.Clear();
			comboUnit.Items.Add(Lan.g(this,"days"),EnumMaintIntervalUnit.Days);
			comboUnit.Items.Add(Lan.g(this,"weeks"),EnumMaintIntervalUnit.Weeks);
			comboUnit.Items.Add(Lan.g(this,"months"),EnumMaintIntervalUnit.Months);
			comboUnit.Items.Add(Lan.g(this,"years"),EnumMaintIntervalUnit.Years);
			int index=comboUnit.Items.GetAll<EnumMaintIntervalUnit>().IndexOf(intervalUnitSelected);
			comboUnit.SelectedIndex=index>=0 ? index : 2;//Months for a unit that is not offered.
		}

		///<summary>Shows, while nobody is assigned, that the reminders go to every user with Maintenance Setup.</summary>
		private void SetUnassignedLabel() {
			string tagAssigned=comboAssigned.SelectedIndex<0 ? ASSIGNEE_NONE : comboAssigned.GetSelected<string>();
			labelUnassigned.Visible=tagAssigned==ASSIGNEE_NONE;
		}

		private void comboAssigned_SelectionChangeCommitted(object sender,EventArgs e) {
			SetUnassignedLabel();
		}

		private void FillComboAssigned() {
			comboAssigned.Items.Clear();
			comboAssigned.Items.Add(Lan.g(this,"Nobody"),ASSIGNEE_NONE);
			List<Userod> listUserods=Userods.GetUsers().OrderBy(x => x.UserName).ToList();
			if(_maintItem.UserNumAssigned!=0 && !listUserods.Any(x => x.UserNum==_maintItem.UserNumAssigned)) {
				Userod userodHidden=Userods.GetUser(_maintItem.UserNumAssigned);
				if(userodHidden!=null) {
					listUserods.Add(userodHidden);
				}
			}
			foreach(Userod userod in listUserods) {
				comboAssigned.Items.Add(userod.UserName+(userod.IsHidden ? " ("+Lan.g(this,"hidden")+")" : ""),"U"+userod.UserNum);
			}
			foreach(UserGroup userGroup in UserGroups.GetList().OrderBy(x => x.Description)) {
				comboAssigned.Items.Add(Lan.g(this,"Group")+": "+userGroup.Description,"G"+userGroup.UserGroupNum);
			}
			string tag=ASSIGNEE_NONE;
			if(_maintItem.UserNumAssigned!=0) {
				tag="U"+_maintItem.UserNumAssigned;
			}
			else if(_maintItem.UserGroupNumAssigned!=0) {
				tag="G"+_maintItem.UserGroupNumAssigned;
			}
			comboAssigned.SelectedIndex=Math.Max(0,comboAssigned.Items.GetAll<string>().IndexOf(tag));
		}

		private EnumMaintScheduleType GetScheduleType() {
			if(comboScheduleType.SelectedIndex<0) {
				return EnumMaintScheduleType.Interval;
			}
			return comboScheduleType.GetSelected<EnumMaintScheduleType>();
		}
		#endregion Combos

		#region Schedule
		///<summary>Shows the controls the schedule type uses.</summary>
		private void SetScheduleControls() {
			EnumMaintScheduleType scheduleType=GetScheduleType();
			bool isWeekdays=scheduleType==EnumMaintScheduleType.Weekdays;
			labelEvery.Visible=!isWeekdays;
			textCount.Visible=!isWeekdays;
			comboUnit.Visible=!isWeekdays;
			labelAfter.Visible=scheduleType==EnumMaintScheduleType.AfterCompletion;
			labelWeekdays.Visible=isWeekdays;
			foreach(UI.CheckBox checkBox in _listCheckWeekdays) {
				checkBox.Visible=isWeekdays;
			}
			labelEvery.Text=scheduleType==EnumMaintScheduleType.AfterCompletion ? Lan.g(this,"Due") : Lan.g(this,"Every");
			labelAnchor.Text=isWeekdays ? Lan.g(this,"Starting") : Lan.g(this,"First due date");
		}

		///<summary>The item as the controls describe it, without saving. Null when a number or date is not valid.</summary>
		private MaintItem GetItemFromControls() {
			if(!textCount.IsValid() || !textLead.IsValid() || !textAnchor.IsValid()) {
				return null;
			}
			MaintItem maintItem=_maintItem.Copy();
			maintItem.ItemName=textName.Text.Trim();
			maintItem.Description=textDescription.Text;
			maintItem.EquipmentNum=comboEquipment.SelectedIndex<0 ? 0 : comboEquipment.GetSelected<long>();
			maintItem.ScheduleType=GetScheduleType();
			maintItem.IntervalCount=PIn.Int(textCount.Text);
			maintItem.IntervalUnit=comboUnit.SelectedIndex<0 ? EnumMaintIntervalUnit.Months : comboUnit.GetSelected<EnumMaintIntervalUnit>();
			int weekdays=0;
			if(maintItem.ScheduleType==EnumMaintScheduleType.Weekdays) {
				for(int i=0;i<7;i++) {
					if(_listCheckWeekdays[i].Checked) {
						weekdays|=MaintSchedules.GetWeekdayBit((DayOfWeek)i);
					}
				}
			}
			maintItem.Weekdays=weekdays;
			maintItem.DateAnchor=PIn.Date(textAnchor.Text);
			maintItem.LeadDays=PIn.Int(textLead.Text);
			maintItem.UserNumAssigned=0;
			maintItem.UserGroupNumAssigned=0;
			string tagAssigned=comboAssigned.SelectedIndex<0 ? ASSIGNEE_NONE : comboAssigned.GetSelected<string>();
			if(tagAssigned.StartsWith("U")) {
				maintItem.UserNumAssigned=PIn.Long(tagAssigned.Substring(1));
			}
			else if(tagAssigned.StartsWith("G")) {
				maintItem.UserGroupNumAssigned=PIn.Long(tagAssigned.Substring(1));
			}
			maintItem.IsHidden=checkHidden.Checked;
			return maintItem;
		}

		///<summary>The "Next due" line: the due date the current settings and the item's completions give.</summary>
		private void UpdateNextDue() {
			MaintItem maintItem=GetItemFromControls();
			if(maintItem==null) {
				labelNextDue.Text="";
				return;
			}
			string error=MaintSchedules.GetItemError(maintItem);
			if(error!=null && maintItem.ItemName!="") {
				labelNextDue.Text=error;
				return;
			}
			DateTime dateDue=MaintSchedules.GetDueDate(maintItem,_listMaintRecords);
			if(dateDue.Year<1880) {
				labelNextDue.Text="";
				return;
			}
			maintItem.IsHidden=false;
			labelNextDue.Text=Lan.g(this,"Next due")+": "+dateDue.ToShortDateString()+" ("
				+MaintSchedules.GetStatusText(MaintSchedules.GetStatus(maintItem,dateDue,_dateToday))+", "+MaintSchedules.GetDaysText(dateDue,_dateToday)+")";
		}

		private void comboScheduleType_SelectionChangeCommitted(object sender,EventArgs e) {
			EnumMaintIntervalUnit intervalUnit=comboUnit.SelectedIndex<0 ? EnumMaintIntervalUnit.Months : comboUnit.GetSelected<EnumMaintIntervalUnit>();
			FillComboUnit(intervalUnit);
			SetScheduleControls();
			UpdateNextDue();
		}

		private void Schedule_Changed(object sender,EventArgs e) {
			UpdateNextDue();
		}
		#endregion Schedule

		#region Checklist
		private void FillGridSteps() {
			gridSteps.BeginUpdate();
			gridSteps.Columns.Clear();
			gridSteps.Columns.Add(new GridColumn("#",30));
			gridSteps.Columns.Add(new GridColumn(Lan.g(gridSteps.TranslationName,"Step"),100){ IsWidthDynamic=true });
			gridSteps.ListGridRows.Clear();
			for(int i=0;i<_listMaintSteps.Count;i++) {
				GridRow row=new GridRow();
				row.Cells.Add((i+1).ToString());
				row.Cells.Add(_listMaintSteps[i].Description);
				row.Tag=_listMaintSteps[i];
				gridSteps.ListGridRows.Add(row);
			}
			gridSteps.EndUpdate();
		}

		private string AskStepText(string text) {
			InputBox inputBox=new InputBox(Lan.g(this,"Checklist step"),text);
			inputBox.SetTitle(Lan.g(this,"Checklist Step"));
			inputBox.ShowDialog();
			if(inputBox.IsDialogCancel || string.IsNullOrWhiteSpace(inputBox.StringResult)) {
				return null;
			}
			string result=inputBox.StringResult.Trim();
			if(result.Length>255) {
				result=result.Substring(0,255);
			}
			return result;
		}

		private void butStepAdd_Click(object sender,EventArgs e) {
			string text=AskStepText("");
			if(text==null) {
				return;
			}
			_listMaintSteps.Add(new MaintStep { Description=text });
			FillGridSteps();
			gridSteps.SetSelected(_listMaintSteps.Count-1,true);
		}

		private void EditStep() {
			int index=gridSteps.GetSelectedIndex();
			if(index<0) {
				MsgBox.Show(this,"Please select a step first.");
				return;
			}
			string text=AskStepText(_listMaintSteps[index].Description);
			if(text==null) {
				return;
			}
			_listMaintSteps[index].Description=text;
			FillGridSteps();
			gridSteps.SetSelected(index,true);
		}

		private void butStepEdit_Click(object sender,EventArgs e) {
			EditStep();
		}

		private void gridSteps_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			EditStep();
		}

		private void butStepDelete_Click(object sender,EventArgs e) {
			int index=gridSteps.GetSelectedIndex();
			if(index<0) {
				MsgBox.Show(this,"Please select a step first.");
				return;
			}
			_listMaintSteps.RemoveAt(index);
			FillGridSteps();
		}

		private void butStepUp_Click(object sender,EventArgs e) {
			MoveInList(_listMaintSteps,gridSteps,-1);
			FillGridStepsKeepSelection(-1);
		}

		private void butStepDown_Click(object sender,EventArgs e) {
			MoveInList(_listMaintSteps,gridSteps,1);
			FillGridStepsKeepSelection(1);
		}

		private void FillGridStepsKeepSelection(int offset) {
			int index=gridSteps.GetSelectedIndex();
			FillGridSteps();
			int indexNew=index+offset;
			if(index>=0 && indexNew>=0 && indexNew<_listMaintSteps.Count) {
				gridSteps.SetSelected(indexNew,true);
			}
			else if(index>=0) {
				gridSteps.SetSelected(index,true);
			}
		}

		///<summary>Moves the selected entry of the list up (-1) or down (1) one place. Does nothing at either end.</summary>
		private static void MoveInList<T>(List<T> list,GridOD grid,int offset) {
			int index=grid.GetSelectedIndex();
			int indexNew=index+offset;
			if(index<0 || indexNew<0 || indexNew>=list.Count) {
				return;
			}
			T item=list[index];
			list[index]=list[indexNew];
			list[indexNew]=item;
		}
		#endregion Checklist

		#region Result fields
		private void FillGridFields() {
			gridFields.BeginUpdate();
			gridFields.Columns.Clear();
			gridFields.Columns.Add(new GridColumn(Lan.g(gridFields.TranslationName,"Field"),150));
			gridFields.Columns.Add(new GridColumn(Lan.g(gridFields.TranslationName,"Type"),80));
			gridFields.Columns.Add(new GridColumn(Lan.g(gridFields.TranslationName,"Limits"),100){ IsWidthDynamic=true });
			gridFields.Columns.Add(new GridColumn(Lan.g(gridFields.TranslationName,"Required"),60,HorizontalAlignment.Center));
			gridFields.ListGridRows.Clear();
			for(int i=0;i<_listMaintFields.Count;i++) {
				MaintField maintField=_listMaintFields[i];
				GridRow row=new GridRow();
				row.Cells.Add(maintField.FieldName);
				row.Cells.Add(maintField.FieldType.GetDescription());
				string limits=MaintValues.GetLimitsText(maintField);
				if(limits=="" && maintField.FieldType==EnumMaintFieldType.Number && !string.IsNullOrWhiteSpace(maintField.Unit)) {
					limits=maintField.Unit;
				}
				row.Cells.Add(limits);
				row.Cells.Add(maintField.IsRequired ? "X" : "");
				row.Tag=maintField;
				gridFields.ListGridRows.Add(row);
			}
			gridFields.EndUpdate();
		}

		private void butFieldAdd_Click(object sender,EventArgs e) {
			MaintField maintField=new MaintField { FieldType=EnumMaintFieldType.PassFail,FieldName="",Unit="" };
			using FormMaintFieldEdit formMaintFieldEdit=new FormMaintFieldEdit(maintField);
			if(formMaintFieldEdit.ShowDialog()!=DialogResult.OK) {
				return;
			}
			_listMaintFields.Add(maintField);
			FillGridFields();
			gridFields.SetSelected(_listMaintFields.Count-1,true);
		}

		private void EditField() {
			int index=gridFields.GetSelectedIndex();
			if(index<0) {
				MsgBox.Show(this,"Please select a field first.");
				return;
			}
			MaintField maintField=_listMaintFields[index].Copy();
			using FormMaintFieldEdit formMaintFieldEdit=new FormMaintFieldEdit(maintField);
			if(formMaintFieldEdit.ShowDialog()!=DialogResult.OK) {
				return;
			}
			_listMaintFields[index]=maintField;
			FillGridFields();
			gridFields.SetSelected(index,true);
		}

		private void butFieldEdit_Click(object sender,EventArgs e) {
			EditField();
		}

		private void gridFields_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			EditField();
		}

		private void butFieldDelete_Click(object sender,EventArgs e) {
			int index=gridFields.GetSelectedIndex();
			if(index<0) {
				MsgBox.Show(this,"Please select a field first.");
				return;
			}
			_listMaintFields.RemoveAt(index);
			FillGridFields();
		}

		private void butFieldUp_Click(object sender,EventArgs e) {
			MoveFieldAndKeepSelection(-1);
		}

		private void butFieldDown_Click(object sender,EventArgs e) {
			MoveFieldAndKeepSelection(1);
		}

		private void MoveFieldAndKeepSelection(int offset) {
			int index=gridFields.GetSelectedIndex();
			MoveInList(_listMaintFields,gridFields,offset);
			FillGridFields();
			int indexNew=index+offset;
			if(index>=0 && indexNew>=0 && indexNew<_listMaintFields.Count) {
				gridFields.SetSelected(indexNew,true);
			}
			else if(index>=0) {
				gridFields.SetSelected(index,true);
			}
		}
		#endregion Result fields

		#region Save and delete
		///<summary>True when the steps differ from the saved ones in count, order or text.</summary>
		private bool AreStepsChanged() {
			return !_listMaintSteps.Select(x => x.MaintStepNum+"|"+x.Description).SequenceEqual(_listMaintStepsOld.Select(x => x.MaintStepNum+"|"+x.Description));
		}

		private static string DescribeField(MaintField maintField) {
			return maintField.MaintFieldNum+"|"+maintField.FieldName+"|"+maintField.FieldType+"|"+maintField.Unit+"|"+maintField.HasMin+"|"+maintField.ValueMin
				+"|"+maintField.HasMax+"|"+maintField.ValueMax+"|"+maintField.IsRequired;
		}

		private bool AreFieldsChanged() {
			return !_listMaintFields.Select(DescribeField).SequenceEqual(_listMaintFieldsOld.Select(DescribeField));
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(!textCount.IsValid() || !textLead.IsValid() || !textAnchor.IsValid()) {
				MsgBox.Show(this,"Please fix data entry errors first.");
				return;
			}
			MaintItem maintItem=GetItemFromControls();
			string error=MaintSchedules.GetItemError(maintItem);
			if(error!=null) {
				MessageBox.Show(error);
				return;
			}
			foreach(MaintField maintField in _listMaintFields) {
				string errorField=MaintValues.GetFieldError(maintField);
				if(errorField!=null) {
					MessageBox.Show(maintField.FieldName+": "+errorField);
					return;
				}
			}
			//An item without an assignee sends its reminders to every user with Maintenance Setup (labelUnassigned says so).
			if(_isNew) {
				MaintItems.Insert(maintItem);
				MaintSteps.Sync(maintItem.MaintItemNum,_listMaintSteps);
				MaintFields.Sync(maintItem.MaintItemNum,_listMaintFields);
				MaintAudit.LogSetup(maintItem,MaintAudit.GetTextAdded(maintItem));
				if(maintItem.IsHidden) {
					MaintAudit.LogSetup(maintItem,MaintAudit.GetTextHidden(maintItem,true));
				}
				_maintItem=maintItem;
				DialogResult=DialogResult.OK;
				return;
			}
			MaintItem maintItemCompare=maintItem.Copy();
			maintItemCompare.IsHidden=_maintItemOld.IsHidden;//Hiding is logged on its own.
			List<string> listChanges=new List<string>();
			string changes=MaintItems.DescribeChanges(maintItemCompare,_maintItemOld);
			if(changes!="") {
				listChanges.Add(changes);
			}
			if(AreStepsChanged()) {
				listChanges.Add(Lan.g(this,"checklist"));
			}
			if(AreFieldsChanged()) {
				listChanges.Add(Lan.g(this,"result fields"));
			}
			MaintItems.Update(maintItem,_maintItemOld);
			if(AreStepsChanged()) {
				MaintSteps.Sync(maintItem.MaintItemNum,_listMaintSteps);
			}
			if(AreFieldsChanged()) {
				MaintFields.Sync(maintItem.MaintItemNum,_listMaintFields);
			}
			if(listChanges.Count>0) {
				MaintAudit.LogSetup(maintItem,MaintAudit.GetTextEdited(maintItem,string.Join(", ",listChanges)));
			}
			if(maintItem.IsHidden!=_maintItemOld.IsHidden) {
				MaintAudit.LogSetup(maintItem,MaintAudit.GetTextHidden(maintItem,maintItem.IsHidden));
			}
			_maintItem=maintItem;
			DialogResult=DialogResult.OK;
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(_isNew) {
				DialogResult=DialogResult.Cancel;
				return;
			}
			if(MaintItems.HasRecords(_maintItem.MaintItemNum)) {
				MsgBox.Show(this,"This item has recorded completions, so it cannot be deleted. Check Hidden instead: the item leaves the board and its history is kept.");
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete this maintenance item, its checklist and its result fields?")) {
				return;
			}
			try {
				MaintItems.Delete(_maintItem.MaintItemNum);
			}
			catch(ApplicationException ex) {
				MessageBox.Show(ex.Message);
				return;
			}
			MaintAudit.LogSetup(_maintItemOld,MaintAudit.GetTextDeleted(_maintItemOld));
			DialogResult=DialogResult.OK;
		}
		#endregion Save and delete
	}
}
