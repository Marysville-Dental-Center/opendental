// Routine maintenance (docs/specs/routine-maintenance.md, "Recording a completion" and "Correcting a completion"): the checklist and results of
// one completion.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.FileIO;

namespace OpenPmsDental {
	///<summary>Records that a maintenance item was done: the date done, the checklist ticks, the results and a note. The logged-on user is who did it
	///and recorded it; there is no second sign-off. The completion closes the due date it was opened with. Saving logs it under MaintenanceRecord.
	///The caller checks MaintenanceRecord before opening it.
	///<para>Opened with a recorded completion, it corrects that completion instead: the same lines as when it was recorded (no line is added or
	///removed), their ticks and values, and the note; the date done, the due date and who did it are shown read only. MaintRecords.Correct saves
	///the changes, sets who corrected it and when, and logs each old and new value. The caller checks MaintRecords.CanCorrect before opening it;
	///Correct checks it again.</para></summary>
	public partial class FormMaintRecordEdit:FormODBase {
		private MaintItem _maintItem;
		private DateTime _dateDue;
		private DateTime _dateToday;
		private List<MaintStep> _listMaintSteps=new List<MaintStep>();
		private List<bool> _listStepsDone=new List<bool>();
		///<summary>The result fields shown, one per line. When correcting, each is built from the recorded line (its label and unit) with the limits
		///and Required of the item's current field, or none when that field was deleted since.</summary>
		private List<MaintField> _listMaintFields=new List<MaintField>();
		///<summary>The value of each field, as stored (MaintValues formats), in the order of _listMaintFields.</summary>
		private List<string> _listFieldValues=new List<string>();
		///<summary>Attachments picked but not yet copied, by field index: the local file. Copied into the item's folder on Save.</summary>
		private Dictionary<int,string> _dictAttachmentPaths=new Dictionary<int,string>();
		///<summary>The completion being corrected; null when recording a new one.</summary>
		private MaintRecord _maintRecordCorrect;
		///<summary>Correcting: the recorded checklist lines, in the order of _listMaintSteps.</summary>
		private List<MaintRecordValue> _listValuesStepsStored=new List<MaintRecordValue>();
		///<summary>Correcting: the recorded result lines, in the order of _listMaintFields.</summary>
		private List<MaintRecordValue> _listValuesFieldsStored=new List<MaintRecordValue>();
		///<summary>Correcting: the item's current fields, which the corrected values are checked and flagged against.</summary>
		private List<MaintField> _listMaintFieldsCurrent=new List<MaintField>();

		///<summary>Records a new completion of the item, closing dateDue.</summary>
		public FormMaintRecordEdit(MaintItem maintItem,DateTime dateDue,DateTime dateToday) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_maintItem=maintItem;
			_dateDue=dateDue;
			_dateToday=dateToday.Date;
		}

		///<summary>Corrects a recorded completion of the item.</summary>
		public FormMaintRecordEdit(MaintItem maintItem,MaintRecord maintRecord) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_maintItem=maintItem;
			_maintRecordCorrect=maintRecord;
			_dateDue=maintRecord.DateDue;
			_dateToday=maintRecord.DateDone.Date;
		}

		private bool IsCorrection {
			get { return _maintRecordCorrect!=null; }
		}

		private void FormMaintRecordEdit_Load(object sender,EventArgs e) {
			labelItem.Text=_maintItem.ItemName;
			textDescription.Text=_maintItem.Description;
			if(IsCorrection) {
				LoadCorrection();
			}
			else {
				labelDue.Text=Lan.g(this,"Closes the due date")+" "+(_dateDue.Year>1880 ? _dateDue.ToShortDateString() : Lan.g(this,"(none)"));
				textDateDone.Text=_dateToday.ToShortDateString();
				labelDoneBy.Text=Lan.g(this,"Done and recorded by")+" "+Security.CurUser.UserName;
				_listMaintSteps=MaintSteps.GetForItem(_maintItem.MaintItemNum);
				_listStepsDone=_listMaintSteps.Select(x => false).ToList();
				_listMaintFields=MaintFields.GetForItem(_maintItem.MaintItemNum);
				_listFieldValues=_listMaintFields.Select(x => "").ToList();
			}
			FillGridSteps();
			FillGridFields();
		}

		///<summary>Fills the window with the recorded completion: its own lines, as recorded.</summary>
		private void LoadCorrection() {
			Text=Lan.g(this,"Correct Maintenance Completion");
			labelDue.Text=Lan.g(this,"Closed the due date")+" "+(_dateDue.Year>1880 ? _dateDue.ToShortDateString() : Lan.g(this,"(none)"));
			textDateDone.Text=_maintRecordCorrect.DateDone.ToShortDateString();
			textDateDone.ReadOnly=true;
			labelDoneBy.Text=Lan.g(this,"Done and recorded by")+" "+Userods.GetName(_maintRecordCorrect.UserNum);
			textNote.Text=_maintRecordCorrect.Note;
			_listMaintFieldsCurrent=MaintFields.GetForItem(_maintItem.MaintItemNum);
			foreach(MaintRecordValue maintRecordValue in MaintRecordValues.GetForRecord(_maintRecordCorrect.MaintRecordNum)) {
				if(maintRecordValue.FieldType==EnumMaintFieldType.ChecklistStep) {
					_listValuesStepsStored.Add(maintRecordValue);
					_listMaintSteps.Add(new MaintStep { MaintStepNum=maintRecordValue.MaintStepNum,Description=maintRecordValue.Label });
					_listStepsDone.Add(maintRecordValue.ValueText==MaintValues.VALUE_TRUE);
					continue;
				}
				_listValuesFieldsStored.Add(maintRecordValue);
				MaintField maintFieldCurrent=MaintCorrections.GetCurrentField(maintRecordValue,_listMaintFieldsCurrent);
				MaintField maintField=maintFieldCurrent==null ? new MaintField() : maintFieldCurrent.Copy();
				//The line reads as it was recorded: its own label, type and unit.
				maintField.MaintFieldNum=maintRecordValue.MaintFieldNum;
				maintField.FieldName=maintRecordValue.Label;
				maintField.FieldType=maintRecordValue.FieldType;
				maintField.Unit=maintRecordValue.Unit;
				_listMaintFields.Add(maintField);
				_listFieldValues.Add(maintRecordValue.ValueText??"");
			}
		}

		#region Checklist
		private void FillGridSteps() {
			gridSteps.BeginUpdate();
			gridSteps.Columns.Clear();
			gridSteps.Columns.Add(new GridColumn(Lan.g(gridSteps.TranslationName,"Done"),45,HorizontalAlignment.Center));
			gridSteps.Columns.Add(new GridColumn(Lan.g(gridSteps.TranslationName,"Step"),100){ IsWidthDynamic=true });
			gridSteps.ListGridRows.Clear();
			for(int i=0;i<_listMaintSteps.Count;i++) {
				GridRow row=new GridRow();
				row.Cells.Add(_listStepsDone[i] ? "X" : "");
				row.Cells.Add((i+1)+". "+_listMaintSteps[i].Description);
				gridSteps.ListGridRows.Add(row);
			}
			gridSteps.EndUpdate();
		}

		private void gridSteps_CellClick(object sender,ODGridClickEventArgs e) {
			if(e.Row<0 || e.Row>=_listStepsDone.Count) {
				return;
			}
			_listStepsDone[e.Row]=!_listStepsDone[e.Row];
			FillGridSteps();
			gridSteps.SetSelected(e.Row,true);
		}

		private void butCheckAll_Click(object sender,EventArgs e) {
			for(int i=0;i<_listStepsDone.Count;i++) {
				_listStepsDone[i]=true;
			}
			FillGridSteps();
		}
		#endregion Checklist

		#region Results
		///<summary>True when the value of field line index is flagged: a Fail or a number outside the field's limits. When correcting, a line whose
		///value did not change keeps its recorded flag (MaintCorrections.GetFlag).</summary>
		private bool IsFieldFlagged(int index) {
			if(IsCorrection) {
				MaintRecordValue maintRecordValueStored=_listValuesFieldsStored[index];
				return MaintCorrections.GetFlag(maintRecordValueStored,_listFieldValues[index],
					MaintCorrections.GetCurrentField(maintRecordValueStored,_listMaintFieldsCurrent));
			}
			return MaintValues.IsFlagged(_listMaintFields[index],_listFieldValues[index]);
		}

		private void FillGridFields() {
			gridFields.BeginUpdate();
			gridFields.Columns.Clear();
			gridFields.Columns.Add(new GridColumn(Lan.g(gridFields.TranslationName,"Field"),180));
			gridFields.Columns.Add(new GridColumn(Lan.g(gridFields.TranslationName,"Value"),200));
			gridFields.Columns.Add(new GridColumn(Lan.g(gridFields.TranslationName,"Limits"),130));
			gridFields.Columns.Add(new GridColumn(Lan.g(gridFields.TranslationName,"Required"),60,HorizontalAlignment.Center));
			gridFields.Columns.Add(new GridColumn(Lan.g(gridFields.TranslationName,"Flag"),100){ IsWidthDynamic=true });
			gridFields.ListGridRows.Clear();
			for(int i=0;i<_listMaintFields.Count;i++) {
				MaintField maintField=_listMaintFields[i];
				string valueText=_listFieldValues[i];
				GridRow row=new GridRow();
				row.Cells.Add(maintField.FieldName);
				row.Cells.Add(MaintValues.GetDisplayText(maintField.FieldType,valueText,maintField.Unit));
				row.Cells.Add(MaintValues.GetLimitsText(maintField));
				row.Cells.Add(maintField.IsRequired ? "X" : "");
				string flag="";
				if(IsFieldFlagged(i)) {
					flag=maintField.FieldType==EnumMaintFieldType.PassFail ? Lan.g(this,"Fail") : Lan.g(this,"Out of range");
					row.ColorBackG=Color.FromArgb(255,215,215);
				}
				row.Cells.Add(flag);
				gridFields.ListGridRows.Add(row);
			}
			gridFields.EndUpdate();
		}

		private void gridFields_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			EnterValue(e.Row);
		}

		private void butEnterValue_Click(object sender,EventArgs e) {
			int index=gridFields.GetSelectedIndex();
			if(index<0) {
				MsgBox.Show(this,"Please select a result first.");
				return;
			}
			EnterValue(index);
		}

		///<summary>Asks for the value of one field in the way its type needs, and keeps it in the stored format.</summary>
		private void EnterValue(int index) {
			if(index<0 || index>=_listMaintFields.Count) {
				return;
			}
			MaintField maintField=_listMaintFields[index];
			string valueText=_listFieldValues[index];
			switch(maintField.FieldType) {
				case EnumMaintFieldType.PassFail: {
					int indexSelected=valueText==MaintValues.VALUE_TRUE ? 0 : (valueText==MaintValues.VALUE_FALSE ? 1 : -1);
					InputBox inputBox=new InputBox(maintField.FieldName,new List<string> { Lan.g(this,"Pass"),Lan.g(this,"Fail") },indexSelected);
					inputBox.ShowDialog();
					if(inputBox.IsDialogCancel) {
						return;
					}
					valueText=inputBox.SelectedIndex==0 ? MaintValues.VALUE_TRUE : MaintValues.VALUE_FALSE;
					break;
				}
				case EnumMaintFieldType.Number: {
					string textDefault="";
					if(MaintValues.TryNumberFromDb(valueText,out double valueOld)) {
						textDefault=MaintValues.FormatNumber(valueOld);
					}
					string prompt=maintField.FieldName+(string.IsNullOrWhiteSpace(maintField.Unit) ? "" : " ("+maintField.Unit+")");
					InputBox inputBox=new InputBox(prompt,textDefault);
					inputBox.ShowDialog();
					if(inputBox.IsDialogCancel) {
						return;
					}
					if(string.IsNullOrWhiteSpace(inputBox.StringResult)) {
						valueText="";
						break;
					}
					if(!MaintValues.TryParseNumber(inputBox.StringResult,out double value)) {
						MsgBox.Show(this,"Please enter a number.");
						return;
					}
					valueText=MaintValues.NumberToDb(value);
					break;
				}
				case EnumMaintFieldType.Date: {
					InputBoxParam inputBoxParam=new InputBoxParam();
					inputBoxParam.InputBoxType_=InputBoxType.ValidDate;
					inputBoxParam.LabelText=maintField.FieldName;
					DateTime dateOld=MaintValues.DateFromDb(valueText);
					inputBoxParam.Text=dateOld.Year>1880 ? dateOld.ToShortDateString() : "";
					InputBox inputBox=new InputBox(inputBoxParam);
					inputBox.ShowDialog();
					if(inputBox.IsDialogCancel) {
						return;
					}
					DateTime date=inputBox.DateResult;
					valueText=date.Year>1880 ? MaintValues.DateToDb(date) : "";
					break;
				}
				case EnumMaintFieldType.Attachment: {
					if(valueText!="") {
						//An attachment is there: pick another file, or remove it. A removed file stays in the item's folder.
						InputBox inputBoxChoice=new InputBox(maintField.FieldName,new List<string> { Lan.g(this,"Attach another file instead"),
							Lan.g(this,"Remove the attachment") },0);
						inputBoxChoice.ShowDialog();
						if(inputBoxChoice.IsDialogCancel) {
							return;
						}
						if(inputBoxChoice.SelectedIndex==1) {
							_dictAttachmentPaths.Remove(index);
							valueText="";
							break;
						}
					}
					if(!MaintAttachments.IsAvailable() || MaintAttachments.GetFolder(_maintItem.MaintItemNum)==null) {
						MsgBox.Show(this,"Attachments are kept in the images folder. This office stores images in the database, so attachments cannot be added.");
						return;
					}
					using OpenFileDialog openFileDialog=new OpenFileDialog();
					openFileDialog.Title=maintField.FieldName;
					if(openFileDialog.ShowDialog()!=DialogResult.OK) {
						return;
					}
					_dictAttachmentPaths[index]=openFileDialog.FileName;
					valueText=MaintAttachments.MakeFileName(openFileDialog.FileName,DateTime.Now);
					break;
				}
				default: {
					InputBox inputBox=new InputBox(maintField.FieldName,valueText);
					inputBox.ShowDialog();
					if(inputBox.IsDialogCancel) {
						return;
					}
					valueText=inputBox.StringResult??"";
					break;
				}
			}
			_listFieldValues[index]=valueText;
			FillGridFields();
			gridFields.SetSelected(index,true);
		}
		#endregion Results

		#region Save
		private void butSave_Click(object sender,EventArgs e) {
			if(IsCorrection) {
				SaveCorrection();
				return;
			}
			if(!textDateDone.IsValid() || textDateDone.Text=="") {
				MsgBox.Show(this,"Please enter a valid date done.");
				return;
			}
			DateTime dateDone=PIn.Date(textDateDone.Text);
			if(dateDone>_dateToday) {
				MsgBox.Show(this,"The date done cannot be later than today.");
				return;
			}
			List<string> listErrors=new List<string>();
			for(int i=0;i<_listMaintFields.Count;i++) {
				string error=MaintValues.GetValueError(_listMaintFields[i],_listFieldValues[i]);
				if(error!=null) {
					listErrors.Add(error);
				}
			}
			if(listErrors.Count>0) {
				MessageBox.Show(string.Join("\r\n",listErrors));
				return;
			}
			List<string> listFlagged=new List<string>();
			for(int i=0;i<_listMaintFields.Count;i++) {
				if(MaintValues.IsFlagged(_listMaintFields[i],_listFieldValues[i])) {
					listFlagged.Add(_listMaintFields[i].FieldName+": "+MaintValues.GetDisplayText(_listMaintFields[i].FieldType,_listFieldValues[i],_listMaintFields[i].Unit));
				}
			}
			if(listFlagged.Count>0 && !MsgBox.Show(MsgBoxButtons.OKCancel,Lan.g(this,"These results failed or are outside their limits:")+"\r\n"
				+string.Join("\r\n",listFlagged)+"\r\n"+Lan.g(this,"Record the completion anyway? It will be flagged.")))
			{
				return;
			}
			int countNotDone=_listStepsDone.Count(x => !x);
			if(countNotDone>0 && !MsgBox.Show(MsgBoxButtons.OKCancel,countNotDone+" "+Lan.g(this,"checklist step(s) are not ticked. Record the completion anyway?"))) {
				return;
			}
			if(!CopyAttachments()) {
				return;
			}
			List<MaintRecordValue> listMaintRecordValues=new List<MaintRecordValue>();
			for(int i=0;i<_listMaintSteps.Count;i++) {
				listMaintRecordValues.Add(MaintValues.CreateStepValue(_listMaintSteps[i],_listStepsDone[i]));
			}
			for(int i=0;i<_listMaintFields.Count;i++) {
				listMaintRecordValues.Add(MaintValues.CreateFieldValue(_listMaintFields[i],_listFieldValues[i]));
			}
			MaintRecord maintRecord=new MaintRecord();
			maintRecord.MaintItemNum=_maintItem.MaintItemNum;
			maintRecord.DateDone=dateDone;
			maintRecord.DateDue=_dateDue.Year>1880 ? _dateDue : DateTime.MinValue;
			maintRecord.Note=textNote.Text;
			MaintRecords.Insert(maintRecord,listMaintRecordValues);
			MaintAudit.LogRecorded(_maintItem,maintRecord);
			DialogResult=DialogResult.OK;
		}

		///<summary>Saves the corrections of the recorded completion: checks them as MaintRecords.Correct will (MaintCorrections.GetPlan), shows what
		///changes and asks to confirm, copies new attachments into the item's folder, and saves.</summary>
		private void SaveCorrection() {
			List<MaintRecordValue> listValuesStored=new List<MaintRecordValue>();
			List<MaintRecordValue> listValuesNew=new List<MaintRecordValue>();
			for(int i=0;i<_listValuesStepsStored.Count;i++) {
				listValuesStored.Add(_listValuesStepsStored[i]);
				MaintRecordValue maintRecordValue=_listValuesStepsStored[i].Copy();
				maintRecordValue.ValueText=_listStepsDone[i] ? MaintValues.VALUE_TRUE : MaintValues.VALUE_FALSE;
				listValuesNew.Add(maintRecordValue);
			}
			for(int i=0;i<_listValuesFieldsStored.Count;i++) {
				listValuesStored.Add(_listValuesFieldsStored[i]);
				MaintRecordValue maintRecordValue=_listValuesFieldsStored[i].Copy();
				maintRecordValue.ValueText=_listFieldValues[i];
				listValuesNew.Add(maintRecordValue);
			}
			//The note box gives its lines with "\n" only; a note that reads the same is not a correction.
			string noteStored=_maintRecordCorrect.Note??"";
			string note=textNote.Text;
			if(note==noteStored.Replace("\r\n","\n")) {
				note=noteStored;
			}
			MaintCorrections.CorrectionPlan correctionPlan=MaintCorrections.GetPlan(_maintRecordCorrect,listValuesStored,note,listValuesNew,_listMaintFieldsCurrent);
			if(correctionPlan.Error!=null) {
				MessageBox.Show(correctionPlan.Error);
				return;
			}
			if(!correctionPlan.IsChanging) {
				MsgBox.Show(this,"Nothing was changed.");
				return;
			}
			string message=Lan.g(this,"Save these corrections? Each one is kept in the audit trail.")+"\r\n"+string.Join("\r\n",correctionPlan.ListChanges);
			if(correctionPlan.IsFlagChanged(_maintRecordCorrect)) {
				message+="\r\n"+(correctionPlan.MaintRecord.IsFlagged ? Lan.g(this,"The completion will be flagged.") : Lan.g(this,"The completion will no longer be flagged."));
			}
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,message)) {
				return;
			}
			if(!CopyAttachments()) {
				return;
			}
			try {
				MaintRecords.Correct(_maintRecordCorrect.MaintRecordNum,note,listValuesNew);
			}
			catch(ApplicationException ex) {
				MessageBox.Show(ex.Message);
				return;
			}
			DialogResult=DialogResult.OK;
		}

		///<summary>Copies the picked attachments into the item's folder in the images folder. False, with a message, when one cannot be copied.</summary>
		private bool CopyAttachments() {
			if(_dictAttachmentPaths.Count==0) {
				return true;
			}
			string folder=MaintAttachments.GetFolder(_maintItem.MaintItemNum);
			if(folder==null) {
				MsgBox.Show(this,"Attachments cannot be added: there is no images folder.");
				return false;
			}
			try {
				OpenPmsDentalBusiness.FileIO.FileAtoZ.CreateDirectory(folder);
				foreach(KeyValuePair<int,string> pair in _dictAttachmentPaths) {
					string fileName=_listFieldValues[pair.Key];
					if(string.IsNullOrEmpty(fileName)) {
						continue;
					}
					FileAtoZ.Copy(pair.Value,FileAtoZ.CombinePaths(folder,fileName),FileAtoZSourceDestination.LocalToAtoZ,Lan.g(this,"Copying attachment..."));
				}
			}
			catch(Exception ex) {
				MessageBox.Show(Lan.g(this,"The attachment could not be copied into the images folder:")+" "+ex.Message);
				return false;
			}
			return true;
		}
		#endregion Save
	}
}
