using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text;
using System.Data;
using Microsoft.Win32;
using OpenDentBusiness;
using CodeBase;
using SparksToothChart;
using OpenDental.UI;
using System.Text.RegularExpressions;

namespace OpenDental{
///<summary></summary>
	public partial class FormProcGroup:FormODBase {
		private ErrorProvider _errorProvider=new ErrorProvider();
		public List<ClaimProcHist> ListClaimProcHists;
		public Procedure ProcedureGroup;
		private Procedure _procedureGroupOld;
		public List<ProcGroupItem> ListProcGroupItems;
		public List<Procedure> ListProcedures;
		private List<Procedure> ListProceduresOld;
		///<summary>This keeps the noteChanged event from erasing the signature when first loading.</summary>
		private bool _isStartingUp;
		private bool _hasSigChanged;
		private PatField[] _patFieldArray;
		private Patient _patient;
		private Family _family;
		///<summary>Used when making an Rx.  Only used when the Rx button is pushed when Orion is enabled.</summary>
		public static bool IsOpen;
		///<summary>Used when making an Rx.  Only used when the Rx button is pushed when Orion is enabled.</summary>
		public static long RxNum;
		private DataTable _tablePlanned;
		///<summary>Users can temporarily log in on this form.  Defaults to Security.CurUser.</summary>
		private Userod _userod=Security.CurUser;
		///<summary>True if the user clicked the Change User button.</summary>
		private bool _hasUserChanged;
		private List<PatFieldDef> _listPatFieldDefs;
		///<summary>Statuses of the procedures attached when the window opened. Decides which permission applies (see GroupNotes): a group note on at least
		///one completed procedure, or on none (25.1), is treated as complete.</summary>
		private List<ProcStat> _listProcStatsAtLoad;
		///<summary>The permission and date that guard editing this group note, from _listProcStatsAtLoad.</summary>
		private EnumPermType _permissionNote;
		private DateTime _datePermission;
		///<summary>25.1: true if the user may change the date, the provider and the attached procedures in this window.</summary>
		private bool _canEditStructure;
		private const string _autoNotePromptRegex=@"\[Prompt:""[a-zA-Z_0-9 ]+""\]";

		public FormProcGroup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		///<summary>Inserts are no longer done within this dialog, but must be done ahead of time from outside.You must specify a procedure to edit, and only the changes that are made in this dialog get saved.  Only used when double click in Account, Chart, TP, and in ContrChart.AddProcedure().  The procedure may be deleted if new, and user hits Cancel.</summary>

		//Constructor from ProcEdit. Lots of this will need to be copied into the new Load function.
		/*public FormProcGroup(long groupNum) {
			GroupCur=Procedures.GetOneProc(groupNum,true);
			ProcGroupItem=ProcGroupItems.Refresh(groupNum);
			//Proc
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}*/

		private void FormProcGroup_Load(object sender, System.EventArgs e){
			signatureBoxWrapper.SetAllowDigitalSig(true);
			IsOpen=true;
			_isStartingUp=true;
			//ProcList gets set in ContrChart where this form is created.
			_patient=Patients.GetPat(ProcedureGroup.PatNum);
			_family=Patients.GetFamily(ProcedureGroup.PatNum);
			_procedureGroupOld=ProcedureGroup.Copy();
			ListProcedures=ListProcedures.FindAll(x => x!=null && x.ProcNum!=0);
			ListProceduresOld=new List<Procedure>();
			for(int i=0;i<ListProcedures.Count;i++){
				ListProceduresOld.Add(ListProcedures[i].Copy());
			}
			_listProcStatsAtLoad=ListProcedures.Select(x => x.ProcStatus).ToList();
			textProcDate.Text=ProcedureGroup.ProcDate.ToShortDateString();
			comboProv.Items.AddProvsAbbr(Providers.GetProvsForClinic(ProcedureGroup.ClinicNum));
			comboProv.SetSelectedProvNum(ProcedureGroup.ProvNum);
			textDateEntry.Text=ProcedureGroup.DateEntryC.ToShortDateString();
			textUser.Text=Userods.GetName(ProcedureGroup.UserNum);//might be blank. Will change automatically if user changes note or alters sig.
			textNotes.Text=ProcedureGroup.Note;
			if(ProcedureGroup.ProcStatus==ProcStat.EC && PrefC.GetBool(PrefName.ProcLockingIsAllowed) && !ProcedureGroup.IsLocked) {
				butLock.Visible=true;
			}
			else {
				butLock.Visible=false;
			}
			EnumPermType permissions=EnumPermType.ProcCompleteEdit;
			if(ProcedureGroup.ProcStatus.In(ProcStat.EO,ProcStat.EC)) {
				permissions=EnumPermType.ProcExistingEdit;
			}
			if(Security.IsGlobalDateLock(permissions,ProcedureGroup.ProcDate)) {
				butLock.Enabled=false;
			}
			//Edit Note on Completed Procedure when a completed procedure is attached or nothing is attached (25.1), otherwise TP Procedure Delete.
			_permissionNote=GroupNotes.GetNotePermission(_listProcStatsAtLoad);
			_datePermission=GroupNotes.GetPermissionDate(ProcedureGroup,_listProcStatsAtLoad);
			if(ProcedureGroup.IsLocked) {//Whether locking is currently allowed, this proc group may have been locked previously.
				butSave.Enabled=false;
				butDelete.Enabled=false;
				labelLocked.Visible=true;
				butAppend.Visible=true;
				textNotes.ReadOnly=true;//just for visual cue.  No way to save changes, anyway.
				textNotes.BackColor=SystemColors.Control;
				butInvalidate.Visible=true;
				butInvalidate.Location=butLock.Location;
			}
			else {
				butInvalidate.Visible=false;
				permissions=_permissionNote;
				DateTime dateForPerm=_datePermission;
				//because islocked overrides security:
				//This is mainly to make sure that the global security lock date is considered.
				if(!Security.IsAuthorized(permissions,dateForPerm)) {
					butSave.Enabled=false;
					butDelete.Enabled=false;
					textNotes.ReadOnly=true;
					textNotes.BackColor=SystemColors.Control;
					butAppend.Enabled=false;
					butChangeUser.Enabled=false;
					signatureBoxWrapper.Enabled=false;
					buttonUseAutoNote.Enabled=false;
					butLock.Enabled=false;
					butInvalidate.Enabled=false;
				}
			}
			if(ProcedureGroup.ProcStatus==ProcStat.D) {//an invalidated proc
				labelInvalid.Visible=true;
				butInvalidate.Visible=false;
				labelLocked.Visible=false;
				butAppend.Visible=false;
				butSave.Enabled=false;
				butDelete.Enabled=false;
			}
			FillProcedures();
			textNotes.Select();
			string keyData=GetSignatureKey();
			signatureBoxWrapper.FillSignature(ProcedureGroup.SigIsTopaz,keyData,ProcedureGroup.Signature);
			signatureBoxWrapper.BringToFront();
			if(!(Security.IsAuthorized(EnumPermType.GroupNoteEditSigned,true) || signatureBoxWrapper.SigIsBlank || ProcedureGroup.UserNum==Security.CurUser.UserNum)) {
				//User does not have permission and this note was signed by someone else.
				textNotes.ReadOnly=true;
				signatureBoxWrapper.Enabled=false;
				labelPermAlert.Visible=true;
				butAppend.Enabled=false;
				buttonUseAutoNote.Enabled=false;
				butChangeUser.Enabled=false;
				butDelete.Enabled=false;
			}
			else if(!Userods.CanUserSignNote()) {
				signatureBoxWrapper.Enabled=false;
				labelPermAlert.Visible=true;
				labelPermAlert.Text=Lans.g(this,"Notes can only be signed by providers.");
			}
			//25.1: the date, the provider and the attached procedures can be changed when the note itself can be, and the user has the permission for
			//changing a procedure (Edit Completed Procedure when the group note is treated as complete, otherwise TP Procedure Delete).
			_canEditStructure=butSave.Enabled && !textNotes.ReadOnly
				&& Security.IsAuthorized(GroupNotes.GetStructurePermission(_listProcStatsAtLoad),_datePermission,true,true);
			textProcDate.ReadOnly=!_canEditStructure;
			comboProv.Enabled=_canEditStructure;
			butAdd.Enabled=_canEditStructure;
			butRemove.Enabled=_canEditStructure;
			_listPatFieldDefs=PatFieldDefs.GetDeepCopy(true);
			textNotes.Select(textNotes.Text.Length,0);
			_isStartingUp=false;
			butEditAutoNote.Visible=HasAutoNotePrompt();
			//string retVal=GroupCur.Note+GroupCur.UserNum.ToString();
			//using MsgBoxCopyPaste msgb=new MsgBoxCopyPaste(retVal);
			//msgb.ShowDialog();
		}

		private void FillProcedures(){
			gridProc.BeginUpdate();
			gridProc.Columns.Clear();
			GridColumn col;
			DisplayFields.RefreshCache();//probably needs to be removed
			List<DisplayField> listDisplayFields=DisplayFields.GetForCategory(DisplayFieldCategory.ProcedureGroupNote);
			for(int i=0;i<listDisplayFields.Count;i++) {
				if(listDisplayFields[i].Description=="") {
					col=new GridColumn(listDisplayFields[i].InternalName,listDisplayFields[i].ColumnWidth);
				}
				else {
					col=new GridColumn(listDisplayFields[i].Description,listDisplayFields[i].ColumnWidth);
				}
				if(listDisplayFields[i].InternalName=="Amount") {
					col.TextAlign=HorizontalAlignment.Right;
				}
				if(listDisplayFields[i].InternalName=="Proc Code") {
					col.TextAlign=HorizontalAlignment.Center;
				}
				gridProc.Columns.Add(col);
			}
			gridProc.ListGridRows.Clear();
			for(int i=0;i<ListProcedures.Count;i++) {
				GridRow row=new GridRow();
				for(int f=0;f<listDisplayFields.Count;f++) {
					switch(listDisplayFields[f].InternalName) {
						case "Date":
							row.Cells.Add(ListProcedures[i].ProcDate.ToShortDateString());
							break;
						case "Th":
							row.Cells.Add(Tooth.Display(ListProcedures[i].ToothNum));
							break;
						case "Surf":
							row.Cells.Add(ListProcedures[i].Surf);
							break;
						case "Description":
							row.Cells.Add(ProcedureCodes.GetLaymanTerm(ListProcedures[i].CodeNum));
							break;
						case "Stat":
							if(ProcMultiVisits.IsProcInProcess(ListProcedures[i].ProcNum)) {
								row.Cells.Add(Lan.g("enumProcStat",ProcStatExt.InProcess));
							}
							else {
								row.Cells.Add(Lan.g("enumProcStat",ListProcedures[i].ProcStatus.ToString()));
							}
							break;
						case "Prov":
							row.Cells.Add(Providers.GetAbbr(ListProcedures[i].ProvNum));
							break;
						case "Amount":
							row.Cells.Add(ListProcedures[i].ProcFee.ToString("F"));
							break;
						case "Proc Code":
							row.Cells.Add(ProcedureCodes.GetStringProcCode(ListProcedures[i].CodeNum));
							break;
						case "Stat 2":
							row.Cells.Add("");//deprecated
							break;
						case "On Call":
							row.Cells.Add("");
							break;
						case "Effective Comm":
							row.Cells.Add("");
							break;
						case "Repair":
							row.Cells.Add("");
							break;
						case "DPCpost":
							row.Cells.Add("");//deprecated
							break;
					}
				}
				row.Tag=ListProcedures[i];
				gridProc.ListGridRows.Add(row);
			}
			gridProc.EndUpdate();
		}

		///<summary>25.1: attaches procedures picked from the patient's chart. Any status, date or provider is allowed. Group notes, locked procedures and
		///deleted or invalidated procedures are refused. Saved when the window is saved.</summary>
		private void butAdd_Click(object sender,EventArgs e) {
			using FormSheetProcSelect formSheetProcSelect=new FormSheetProcSelect();
			formSheetProcSelect.PatNum=ProcedureGroup.PatNum;
			formSheetProcSelect.ShowDialog();
			if(formSheetProcSelect.DialogResult!=DialogResult.OK || formSheetProcSelect.ListProcNumsSelected==null) {
				return;
			}
			List<string> listRefused=new List<string>();
			bool isChanged=false;
			for(int i=0;i<formSheetProcSelect.ListProcNumsSelected.Count;i++) {
				long procNum=formSheetProcSelect.ListProcNumsSelected[i];
				if(procNum==ProcedureGroup.ProcNum && procNum!=0) {
					listRefused.Add(Lan.g(this,"You cannot attach a group note to another group note."));
					continue;
				}
				if(ListProcedures.Any(x => x.ProcNum==procNum)) {
					continue;//already attached
				}
				Procedure procedure=Procedures.GetOneProc(procNum,false);
				string blockReason=GroupNotes.GetAttachBlockReason(procedure);
				if(blockReason!=null) {
					string procText=(procedure==null) ? "" : ProcedureCodes.GetStringProcCode(procedure.CodeNum,doThrowIfMissing:false)+" "+procedure.ProcDate.ToShortDateString()+": ";
					listRefused.Add(procText+Lan.g(this,blockReason));
					continue;
				}
				ListProcedures.Add(procedure);
				isChanged=true;
			}
			if(isChanged) {
				OnAttachedProceduresChanged();
			}
			if(listRefused.Count>0) {
				MessageBox.Show(Lan.g(this,"These procedures were not attached:")+"\r\n"+string.Join("\r\n",listRefused.Distinct()));
			}
		}

		///<summary>25.1: detaches the selected procedures. Saved when the window is saved.</summary>
		private void butRemove_Click(object sender,EventArgs e) {
			List<Procedure> listProceduresSelected=gridProc.SelectedTags<Procedure>();
			if(listProceduresSelected.Count==0) {
				MsgBox.Show(this,"Please select procedures to remove first.");
				return;
			}
			ListProcedures.RemoveAll(x => listProceduresSelected.Contains(x));
			OnAttachedProceduresChanged();
		}

		private void OnAttachedProceduresChanged() {
			//The attached procedures are part of the signature key, so the signature that was loaded no longer applies.
			ClearLoadedSignature();
			FillProcedures();
		}

		private void textProcDate_TextChanged(object sender,EventArgs e) {
			if(_isStartingUp) {
				return;
			}
			//The date is part of the signature key. Reformatting the same date (when the box loses focus) doesn't count.
			if(DateTime.TryParse(textProcDate.Text,out DateTime dateProc) && dateProc.Date==_procedureGroupOld.ProcDate.Date) {
				return;
			}
			ClearLoadedSignature();
		}

		///<summary>Clears the signature that was loaded with the group note, as editing the note does, when something it is bound to changes. A signature
		///drawn since the window opened is kept, because it is bound to the data when the window is saved.</summary>
		private void ClearLoadedSignature() {
			if(_isStartingUp || _hasSigChanged) {
				return;
			}
			signatureBoxWrapper.ClearSignature();
		}

		///<summary>The procedures attached now that weren't attached when the window opened.</summary>
		private List<Procedure> GetProceduresAdded() {
			return ListProcedures.FindAll(x => !ListProceduresOld.Any(y => y.ProcNum==x.ProcNum));
		}

		///<summary>The procedures attached when the window opened that aren't attached now.</summary>
		private List<Procedure> GetProceduresRemoved() {
			return ListProceduresOld.FindAll(x => !ListProcedures.Any(y => y.ProcNum==x.ProcNum));
		}

		private void RefreshGrids(){
			FillProcedures();
		}

		private void butRx_Click(object sender,EventArgs e) {
			//only visible in Orion mode
			if(!Security.IsAuthorized(EnumPermType.RxCreate)) {
				return;
			}
			using FormRxSelect formRxSelect=new FormRxSelect(_patient);
			formRxSelect.ShowDialog();
			if(formRxSelect.DialogResult!=DialogResult.OK) {
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.RxCreate,_patient.PatNum,_patient.GetNameLF());
			RxPat rxPat=RxPats.GetRx(RxNum);
			if(textNotes.Text!=""){
				textNotes.Text+="\r\n";
			}
			textNotes.Text+="Rx - "+rxPat.Drug+" - #"+rxPat.Disp;
			string rxNote=Pharmacies.GetDescription(RxNum);
			if(rxNote!=""){
				textNotes.Text+="\r\n"+rxNote;
			}
		}

		private void buttonUseAutoNote_Click(object sender,EventArgs e) {
			FrmAutoNoteCompose frmAutoNoteCompose=new FrmAutoNoteCompose();
			frmAutoNoteCompose.ShowDialog();
			if(frmAutoNoteCompose.IsDialogOK) {
				textNotes.AppendText(frmAutoNoteCompose.StrCompletedNote);
				butEditAutoNote.Visible=HasAutoNotePrompt();
			}
		}

		private void ButEditAutoNote_Click(object sender,EventArgs e) {
			if(HasAutoNotePrompt()) {
				FrmAutoNoteCompose frmAutoNoteCompose=new FrmAutoNoteCompose();
				frmAutoNoteCompose.StrMainTextNote=textNotes.Text;
				frmAutoNoteCompose.ShowDialog();
				if(frmAutoNoteCompose.IsDialogOK) {
					textNotes.Text=frmAutoNoteCompose.StrCompletedNote;
					butEditAutoNote.Visible=HasAutoNotePrompt();
				}
				return;
			}
			MessageBox.Show(Lan.g(this,"No Auto Note available to edit."));
		}

		private bool HasAutoNotePrompt() {
			return Regex.IsMatch(textNotes.Text,_autoNotePromptRegex);
		}

		private void butExamSheets_Click(object sender,EventArgs e) {
			using FormExamSheets formExamSheets=new FormExamSheets();
			formExamSheets.PatNum=ProcedureGroup.PatNum;
			formExamSheets.ShowDialog();
			//TODO: Print a note about Exam Sheet added.
		}

		private void textNotes_TextChanged(object sender,EventArgs e) {
			if(!_isStartingUp//so this happens only if user changes the note
				&& !_hasSigChanged)//and the original signature is still showing.
			{
				//SigChanged=true;//happens automatically through the event.
				signatureBoxWrapper.ClearSignature();
			}
		}

		///<summary>Unchanged from 24.3 (see GroupNotes.GetSignatureKey), so existing signatures stay valid. Reads the links from the database, so it must
		///run after added or removed procedures are saved.</summary>
		private string GetSignatureKey(){
			ListProcGroupItems=ProcGroupItems.GetForGroup(ProcedureGroup.ProcNum);//Orders the list to ensure same key in all cases.
			return GroupNotes.GetSignatureKey(ProcedureGroup,ListProcGroupItems);
		}

		private void butChangeUser_Click(object sender,EventArgs e) {
			using FormLogOn formLogOn=new FormLogOn(isSimpleSwitch:true);
			formLogOn.ShowDialog();
			if(formLogOn.DialogResult!=DialogResult.OK) {
				return;
			}
			if(formLogOn.IsDomainAuthenticated) {
				SecurityLogs.MakeLogEntry(EnumPermType.GroupNoteEditSigned,_patient.PatNum,
					DomainLogin.GetSwitchLogText(Lan.g(this,"Group note"),_userod,formLogOn.UserodSimpleSwitch,isDomainAuthenticated:true),
					ProcedureGroup.ProcNum,DateTime.MinValue);
			}
			_userod=formLogOn.UserodSimpleSwitch; //assign temp user
			bool canUserSignNote=Userods.CanUserSignNote(_userod);//only show if user can sign
			signatureBoxWrapper.Enabled=canUserSignNote;
			if(!labelPermAlert.Visible && !canUserSignNote) {
				labelPermAlert.Text=Lans.g(this,"Notes can only be signed by providers.");
				labelPermAlert.Visible=true;
			}
			signatureBoxWrapper.ClearSignature(); //clear sig
			signatureBoxWrapper.UserSig=_userod;
			textUser.Text=_userod.UserName; //update user textbox.
			_hasSigChanged=true;
			_hasUserChanged=true;
		}

		private void SaveSignature(){
			if(_hasSigChanged){
				string keyData=GetSignatureKey();
				ProcedureGroup.Signature=signatureBoxWrapper.GetSignature(keyData);
				ProcedureGroup.SigIsTopaz=signatureBoxWrapper.GetSigIsTopaz();
			}
		}

		private void signatureBoxWrapper_SignatureChanged(object sender,EventArgs e) {
			ProcedureGroup.UserNum=_userod.UserNum;
			if(!textUser.Text.IsNullOrEmpty() && textUser.Text!=_userod.UserName) {
				_hasUserChanged=true;
			}
			textUser.Text=_userod.UserName;
			_hasSigChanged=true;
		}

		///<summary>This button is only visible if 1. Pref ProcLockingIsAllowed is true, 2. Proc isn't already locked, 3. Proc status is C.</summary>
		private void butLock_Click(object sender,EventArgs e) {
			if(!EntriesAreValid()) {
				return;
			}
			ProcedureGroup.IsLocked=true;
			SaveAndClose();//saves all the other various changes that the user made
		}

		///<summary>This button is only visible when proc IsLocked.</summary>
		private void butInvalidate_Click(object sender,EventArgs e) {
			//What this will really do is "delete" the procedure.
			if(!Security.IsAuthorized(EnumPermType.ProcDelete,ProcedureGroup.DateEntryC)) {
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.GroupNoteEditSigned)) {
				return;
			}
			try {
				Procedures.Delete(ProcedureGroup.ProcNum);//also deletes any claimprocs (other than ins payments of course).
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
				return;
			}
			//Log entry does not show procstatus because group notes don't technically have a status, always EC.
			SecurityLogs.MakeLogEntry(EnumPermType.ProcDelete,_patient.PatNum,Lan.g(this,"Invalidated: ")+
				ProcedureCodes.GetStringProcCode(ProcedureGroup.CodeNum).ToString()+", "+ProcedureGroup.ProcDate.ToShortDateString());
			DialogResult=DialogResult.OK;
		}

		///<summary>This button is only visible when proc IsLocked.</summary>
		private void butAppend_Click(object sender,EventArgs e) {
			using FormProcNoteAppend formProcNoteAppend=new FormProcNoteAppend();
			formProcNoteAppend.ProcedureCur=ProcedureGroup;
			formProcNoteAppend.ShowDialog();
			if(formProcNoteAppend.DialogResult!=DialogResult.OK) {
				return;
			}
			DialogResult=DialogResult.OK;//exit out of this window.  Change already saved, and OK button is disabled in this window, anyway.
		}

		private bool EntriesAreValid() {
			if(!textProcDate.IsValid()) {
				MsgBox.Show(this,"Please fix data entry errors first.");
				return false;
			}
			if(_canEditStructure) {
				DateTime dateProc=PIn.Date(textProcDate.Text);
				if(dateProc.Year<1880) {
					MsgBox.Show(this,"Please enter a procedure date.");
					return false;
				}
				if(dateProc.Date!=_procedureGroupOld.ProcDate.Date) {
					if(dateProc.Date>DateTime.Today && !PrefC.GetBool(PrefName.FutureTransDatesAllowed)) {
						MsgBox.Show(this,"Group notes cannot be dated in the future.");
						return false;
					}
					//The new date must not fall before the user's date limit or the global lock date either.
					if(GroupNotes.IsTreatedAsComplete(_listProcStatsAtLoad) && !Security.IsAuthorized(_permissionNote,dateProc)) {
						return false;
					}
				}
			}
			bool hasAutoNotePrompt=Regex.IsMatch(textNotes.Text,_autoNotePromptRegex);
			//If ProcNoteSigsBlockedAutoNoteIncomplete is true, do not allow the user to save a changed signature if there are still autonote prompts.
			if(_hasSigChanged && !signatureBoxWrapper.SigIsBlank && hasAutoNotePrompt && PrefC.GetBool(PrefName.ProcNoteSigsBlockedAutoNoteIncomplete)) {
				MessageBox.Show(Lan.g(this,"Remaining auto note prompts must be completed to sign this note. Use Edit Auto Note to resume."));
				return false;
			}
			if(!signatureBoxWrapper.IsValid) {
				MsgBox.Show(this,"Your signature is invalid. Please sign and click OK again.");
				return false;
			}
			return true;
		}

		private void SaveAndClose() {
			ProcedureGroup.Note=textNotes.Text;
			List<Procedure> listProceduresAdded=new List<Procedure>();
			List<Procedure> listProceduresRemoved=new List<Procedure>();
			if(_canEditStructure) {
				ProcedureGroup.ProcDate=PIn.Date(textProcDate.Text);
				ProcedureGroup.ProvNum=comboProv.GetSelectedProvNum();
				listProceduresAdded=GetProceduresAdded();
				listProceduresRemoved=GetProceduresRemoved();
				if(listProceduresAdded.Count>0 || listProceduresRemoved.Count>0) {
					//Before the signature is saved: the signature key is built from the links in the database.
					ListProcGroupItems=ProcGroupItems.SyncForGroup(ProcedureGroup.ProcNum,ListProcedures.Select(x => x.ProcNum).ToList());
				}
			}
			try {
				SaveSignature();
			}
			catch(Exception ex){
				MessageBox.Show(Lan.g(this,"Error saving signature.")+"\r\n"+ex.Message);
			}
			Procedures.Update(ProcedureGroup,_procedureGroupOld);
			string logText=GroupNotes.GetEditLogText(_procedureGroupOld,ProcedureGroup,listProceduresAdded,listProceduresRemoved,
				x => ProcedureCodes.GetStringProcCode(x,doThrowIfMissing:false),x => Providers.GetAbbr(x));
			if(logText!="") {
				//Logged as a procedure edit on the group note.
				SecurityLogs.MakeLogEntry(EnumPermType.ProcEdit,ProcedureGroup.PatNum,logText,ProcedureGroup.ProcNum,_procedureGroupOld.DateTStamp);
			}
			DialogResult=DialogResult.OK;
			IsOpen=false;
		}

		private void butDelete_Click(object sender, System.EventArgs e) {
			if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Delete this group note?")){
				return;
			}
			try { 
				Procedures.Delete(ProcedureGroup.ProcNum);
			}
			catch(Exception ex){
				MessageBox.Show(ex.Message+"\r\n"+Lan.g(this,"Please call support."));//GroupNotes should never fail deletion.
				return;
			}
			ListProcGroupItems=ProcGroupItems.GetForGroup(ProcedureGroup.ProcNum);
			for(int i=0;i<ListProcGroupItems.Count;i++){
				ProcGroupItems.Delete(ListProcGroupItems[i].ProcGroupItemNum);
			}
			//This log entry is similar to the log entry made when right-clicking in the Chart and using the delete option,
			//except there is an extra : in the description for this log entry, so we programmers can know for sure where the entry was made from.
			//Change Status or Delete a Completed Procedure when the group note was treated as complete (on a completed procedure, or on none).
			SecurityLogs.MakeLogEntry(GroupNotes.GetDeleteLogPermission(_listProcStatsAtLoad),_patient.PatNum,
				":"+ProcedureCodes.GetStringProcCode(ProcedureGroup.CodeNum).ToString()+" ("+ProcedureGroup.ProcStatus+"), "+ProcedureGroup.ProcDate.ToShortDateString());
			ListProcedures=new List<Procedure>();//Nothing is attached any more, so the Chart doesn't clear aggregated notes.
			DialogResult=DialogResult.OK;
			IsOpen=false;
		}		

		private void butSave_Click(object sender,System.EventArgs e) {
			if(!EntriesAreValid()){
				return;
			}
			if(_hasUserChanged && !_procedureGroupOld.Signature.IsNullOrEmpty()) {
				//Ask the user to re-sign if the user has changed, there was a signature when the window loaded, and the signature box is currently blank.
				if(signatureBoxWrapper.SigIsBlank) {
					if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,
						"The signature box has not been re-signed.  Continuing will remove the previous signature from this procedure.  Exit anyway?"))
					{
						return;
					}
				}
				//Check if user is subcribed to the alertType.SignatureCleared and create an alert.
				if(AlertSubs.GetAllAlertTypesForUser(_procedureGroupOld.UserNum).Contains(AlertType.SignatureCleared)) {
					string procCode=ProcedureCodes.GetStringProcCode(_procedureGroupOld.CodeNum);
					string provider=Providers.GetAbbr(_procedureGroupOld.ProvNum);
					string alertDescription="Group Note Changed for PatNum: "+_procedureGroupOld.PatNum
					+" Provider: "+provider
					+" Date: "+_procedureGroupOld.ProcDate.ToShortDateString()
					+" Procedure Code: "+procCode;
					AlertItems.Insert(new AlertItem {
						//Allow to alert to be deleted or marked as read.
						Actions=ActionType.MarkAsRead | ActionType.Delete,
						Description=Lans.g("Procedures",alertDescription),
						Severity=SeverityType.Low,
						Type=AlertType.SignatureCleared,
						UserNum=_procedureGroupOld.UserNum,
						ClinicNum=_procedureGroupOld.ClinicNum,
					});
				}
			}
			SaveAndClose();
		}

		private void FormProcGroup_FormClosing(object sender,FormClosingEventArgs e) {
			signatureBoxWrapper?.SetTabletState(0);
			if(DialogResult==DialogResult.OK) {
				return;
			}
			if(_procedureGroupOld.Note.Replace("\r","").Trim()!=textNotes.Text.Replace("\r","").Trim()) {
				if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Note has been changed.  Unsaved changes will be lost.  Continue?")) {
					e.Cancel=true;//Prevent the form from closing.
					IsOpen=true;
					return;
				}
			}
			if(ProcedureGroup.IsNew) {
				Procedures.Delete(ProcedureGroup.ProcNum);
				ListProcGroupItems=ProcGroupItems.GetForGroup(ProcedureGroup.ProcNum);
				for(int i=0;i<ListProcGroupItems.Count;i++) {
					ProcGroupItems.Delete(ListProcGroupItems[i].ProcGroupItemNum);
				}
			}
			DialogResult=DialogResult.Cancel;
			IsOpen=false;
		}
	}
}
