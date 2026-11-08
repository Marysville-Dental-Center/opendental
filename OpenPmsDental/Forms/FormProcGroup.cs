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
using OpenPmsDentalBusiness;
using CodeBase;
using SparksToothChart;
using OpenPmsDental.UI;
using System.Text.RegularExpressions;

namespace OpenPmsDental{
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
		///<summary>26.1: the user who signs the second box. Defaults to Security.CurUser; Change User next to the box switches it.</summary>
		private Userod _userod2=Security.CurUser;
		///<summary>26.1: true once the second box was signed, cleared or changed since the window opened. A second signature loaded with the note is
		///kept as it is until then.</summary>
		private bool _hasSig2Changed;
		///<summary>26.1: true when Change User was used for the second box.</summary>
		private bool _hasUser2Changed;
		///<summary>25.4 Procedure Note Merge: the newest procnote row of the group note when the window opened (null when it had none). Saving reads it
		///again; when somebody else saved a newer note meanwhile, the merge window is shown.</summary>
		private ProcNote _procNoteNewestAtLoad;
		///<summary>25.4: the merges resolved in this window, logged when the group note is saved.</summary>
		private List<string> _listNoteMergeLogTexts=new List<string>();
		///<summary>Name under which this window's size is remembered per user (LocalWindowSizes).</summary>
		private const string WINDOW_SIZE_NAME="FormProcGroup";
		///<summary>The size the window opened at before a remembered size was applied (screen pixels). It is also the smallest width, so the second
		///signature box always fits.</summary>
		private Size _sizeDefault;

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
			signatureBoxWrapper2.SetAllowDigitalSig(true);
			ApplyRememberedSize();
			IsOpen=true;
			_isStartingUp=true;
			//ProcList gets set in ContrChart where this form is created.
			_patient=Patients.GetPat(ProcedureGroup.PatNum);
			_family=Patients.GetFamily(ProcedureGroup.PatNum);
			//25.4: remember the newest note, to find a newer one saved by somebody else before this window saves (Procedure Note Merge).
			_procNoteNewestAtLoad=ProcNotes.GetNewestForProc(ProcedureGroup.ProcNum);
			LoadSecondSignature();
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
					butChangeUser2.Enabled=false;
					signatureBoxWrapper2.Enabled=false;
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
			//26.1: the second signature, bound to the same data with its own signer (GroupNoteSignatures.GetSignatureKey2).
			signatureBoxWrapper2.FillSignature(ProcedureGroup.SigIsTopaz2,GroupNoteSignatures.GetSignatureKey2(ProcedureGroup,ListProcGroupItems),
				ProcedureGroup.Signature2);
			signatureBoxWrapper2.BringToFront();
			textUser2.Text=(ProcedureGroup.UserNum2==0) ? "" : Userods.GetName(ProcedureGroup.UserNum2);
			bool hasEditSignedPermission=Security.IsAuthorized(EnumPermType.GroupNoteEditSigned,true);
			//As in 24.3, applied to each signature: only the signer, or a user with Group Note Edit Signed, may edit a signed note.
			if(!GroupNoteSignatures.CanEditNoteText(hasEditSignedPermission,Security.CurUser.UserNum,ProcedureGroup.UserNum,signatureBoxWrapper.SigIsBlank,
				ProcedureGroup.UserNum2,signatureBoxWrapper2.SigIsBlank))
			{
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
			//26.1: the second box can be signed by a second user who may not edit the text (signing does not change it), as long as it is blank or
			//theirs, the note can be saved, and the user may sign notes. Locked and invalidated group notes can't be saved, so both boxes stay read-only.
			bool canUseBox2=butSave.Enabled
				&& GroupNoteSignatures.CanSignBox2(hasEditSignedPermission,Security.CurUser.UserNum,ProcedureGroup.UserNum2,signatureBoxWrapper2.SigIsBlank);
			butChangeUser2.Enabled=canUseBox2;
			signatureBoxWrapper2.Enabled=canUseBox2 && Userods.CanUserSignNote();
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
		///deleted or invalidated procedures are refused. Nothing is attached when the user may not save the result, for example a completed procedure
		///on a group note on TP procedures only without Edit Completed Procedure; saving checks again. Saved when the window is saved.</summary>
		private void butAdd_Click(object sender,EventArgs e) {
			using FormSheetProcSelect formSheetProcSelect=new FormSheetProcSelect();
			formSheetProcSelect.PatNum=ProcedureGroup.PatNum;
			formSheetProcSelect.ShowDialog();
			if(formSheetProcSelect.DialogResult!=DialogResult.OK || formSheetProcSelect.ListProcNumsSelected==null) {
				return;
			}
			List<string> listRefused=new List<string>();
			List<Procedure> listProceduresToAdd=new List<Procedure>();
			for(int i=0;i<formSheetProcSelect.ListProcNumsSelected.Count;i++) {
				long procNum=formSheetProcSelect.ListProcNumsSelected[i];
				if(procNum==ProcedureGroup.ProcNum && procNum!=0) {
					listRefused.Add(Lan.g(this,"You cannot attach a group note to another group note."));
					continue;
				}
				if(ListProcedures.Any(x => x.ProcNum==procNum) || listProceduresToAdd.Any(x => x.ProcNum==procNum)) {
					continue;//already attached
				}
				Procedure procedure=Procedures.GetOneProc(procNum,false);
				string blockReason=GroupNotes.GetAttachBlockReason(procedure);
				if(blockReason!=null) {
					string procText=(procedure==null) ? "" : ProcedureCodes.GetStringProcCode(procedure.CodeNum,doThrowIfMissing:false)+" "+procedure.ProcDate.ToShortDateString()+": ";
					listRefused.Add(procText+Lan.g(this,blockReason));
					continue;
				}
				listProceduresToAdd.Add(procedure);
			}
			if(listProceduresToAdd.Count>0) {
				List<Procedure> listProceduresAfter=ListProcedures.Concat(listProceduresToAdd).ToList();
				if(!IsAuthorizedForChanges(listProceduresAfter,GetProcDateEntered(),isNoteChanged:false)) {
					return;//The user was told which permission is missing.
				}
				ListProcedures.AddRange(listProceduresToAdd);
				OnAttachedProceduresChanged();
			}
			if(listRefused.Count>0) {
				MessageBox.Show(Lan.g(this,"These procedures were not attached:")+"\r\n"+string.Join("\r\n",listRefused.Distinct()));
			}
		}

		///<summary>25.1: detaches the selected procedures. Nothing is detached when the user may not save the result, for example removing every
		///procedure (a group note with nothing attached counts as complete) without Edit Completed Procedure; saving checks again. Saved when the
		///window is saved.</summary>
		private void butRemove_Click(object sender,EventArgs e) {
			List<Procedure> listProceduresSelected=gridProc.SelectedTags<Procedure>();
			if(listProceduresSelected.Count==0) {
				MsgBox.Show(this,"Please select procedures to remove first.");
				return;
			}
			List<Procedure> listProceduresAfter=ListProcedures.FindAll(x => !listProceduresSelected.Contains(x));
			if(!IsAuthorizedForChanges(listProceduresAfter,GetProcDateEntered(),isNoteChanged:false)) {
				return;//The user was told which permission is missing.
			}
			ListProcedures=listProceduresAfter;
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

		///<summary>Clears the signatures that were loaded with the group note, as editing the note does, when something they are bound to changes. A
		///signature drawn since the window opened is kept, because it is bound to the data when the window is saved. 26.1: both boxes.</summary>
		private void ClearLoadedSignature() {
			if(_isStartingUp) {
				return;
			}
			if(!_hasSigChanged) {
				signatureBoxWrapper.ClearSignature();
			}
			if(!_hasSig2Changed) {
				signatureBoxWrapper2.ClearSignature();
			}
		}

		///<summary>The date in the Procedure Date box when it can be edited and holds a date, otherwise the date the group note had when the window
		///opened.</summary>
		private DateTime GetProcDateEntered() {
			if(_canEditStructure && textProcDate.IsValid()) {
				DateTime dateProc=PIn.Date(textProcDate.Text);
				if(dateProc.Year>=1880) {
					return dateProc;
				}
			}
			return _procedureGroupOld.ProcDate;
		}

		///<summary>True if the note's text differs from the saved one, or a signature (in either box) was drawn since the window opened.</summary>
		private bool IsNoteOrSignatureChanged() {
			if((_procedureGroupOld.Note??"").Replace("\r","")!=textNotes.Text.Replace("\r","")) {
				return true;
			}
			if(_hasSig2Changed && !signatureBoxWrapper2.SigIsBlank) {
				return true;
			}
			return _hasSigChanged && !signatureBoxWrapper.SigIsBlank;
		}

		///<summary>Checks, and tells the user when refused, the permissions needed to save the group note with listProceduresAttached attached and
		///dated dateProc (GroupNotes.GetSaveChecks). The statuses are read again from the database, because they may have changed since the window
		///opened.</summary>
		private bool IsAuthorizedForChanges(List<Procedure> listProceduresAttached,DateTime dateProc,bool isNoteChanged) {
			List<Procedure> listProceduresDb=Procedures.GetManyProc(listProceduresAttached.Select(x => x.ProcNum).ToList(),false);
			List<ProcStat> listProcStatsNow=listProceduresAttached
				.Select(x => (listProceduresDb.FirstOrDefault(y => y.ProcNum==x.ProcNum)??x).ProcStatus).ToList();
			bool isAttachedChanged=!new HashSet<long>(listProceduresAttached.Select(x => x.ProcNum)).SetEquals(ListProceduresOld.Select(x => x.ProcNum));
			bool isProvChanged=_canEditStructure && comboProv.GetSelectedProvNum()!=_procedureGroupOld.ProvNum;
			List<GroupNotePermCheck> listGroupNotePermChecks=GroupNotes.GetSaveChecks(_listProcStatsAtLoad,listProcStatsNow,isAttachedChanged,isProvChanged,
				_procedureGroupOld.ProcDate,dateProc,_procedureGroupOld.DateEntryC,isNoteChanged);
			return GroupNotes.IsSaveAuthorized(listGroupNotePermChecks,(permType,dateLimit) => Security.IsAuthorized(permType,dateLimit));
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
			if(!_isStartingUp && !_hasSig2Changed) {//26.1: editing the note clears both signatures.
				signatureBoxWrapper2.ClearSignature();
			}
		}

		///<summary>Unchanged from 24.3 (see GroupNotes.GetSignatureKey), so existing signatures stay valid. Reads the links from the database, so it must
		///run after added or removed procedures are saved.</summary>
		private string GetSignatureKey(){
			ListProcGroupItems=ProcGroupItems.GetForGroup(ProcedureGroup.ProcNum);//Orders the list to ensure same key in all cases.
			return GroupNotes.GetSignatureKey(ProcedureGroup,ListProcGroupItems);
		}

		private void butChangeUser_Click(object sender,EventArgs e) {
			using FormLogOn formLogOn=new FormLogOn(isSimpleSwitch:true,isSigBox:true);
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
			if(_hasSig2Changed) {
				SaveSignature2();
			}
		}

		///<summary>26.1: the second box. Blank: no second signature (UserNum2 0). Signed: by the box's user, bound to the note as saved.</summary>
		private void SaveSignature2() {
			if(signatureBoxWrapper2.SigIsBlank) {
				ProcedureGroup.UserNum2=0;
				ProcedureGroup.Signature2="";
				ProcedureGroup.SigIsTopaz2=false;
				return;
			}
			ProcedureGroup.UserNum2=_userod2.UserNum;
			GetSignatureKey();//Reads the links from the database, which the key needs.
			string keyData2=GroupNoteSignatures.GetSignatureKey2(ProcedureGroup,ListProcGroupItems);
			ProcedureGroup.Signature2=signatureBoxWrapper2.GetSignature(keyData2);
			ProcedureGroup.SigIsTopaz2=signatureBoxWrapper2.GetSigIsTopaz();
		}

		private void signatureBoxWrapper_SignatureChanged(object sender,EventArgs e) {
			ProcedureGroup.UserNum=_userod.UserNum;
			if(!textUser.Text.IsNullOrEmpty() && textUser.Text!=_userod.UserName) {
				_hasUserChanged=true;
			}
			textUser.Text=_userod.UserName;
			_hasSigChanged=true;
		}

		///<summary>26.1: the second box was signed, cleared or started. Its user is the box's user; nobody while it is blank.</summary>
		private void signatureBoxWrapper2_SignatureChanged(object sender,EventArgs e) {
			_hasSig2Changed=true;
			if(signatureBoxWrapper2.SigIsBlank && !_hasUser2Changed) {
				textUser2.Text="";
				return;
			}
			textUser2.Text=_userod2.UserName;
		}

		///<summary>26.1: switches the user who signs the second box, like Change User for the first box (with the domain login option).</summary>
		private void butChangeUser2_Click(object sender,EventArgs e) {
			using FormLogOn formLogOn=new FormLogOn(isSimpleSwitch:true,isSigBox:true);
			formLogOn.ShowDialog();
			if(formLogOn.DialogResult!=DialogResult.OK) {
				return;
			}
			if(formLogOn.IsDomainAuthenticated) {
				SecurityLogs.MakeLogEntry(EnumPermType.GroupNoteEditSigned,_patient.PatNum,
					DomainLogin.GetSwitchLogText(Lan.g(this,"Group note signature 2"),_userod2,formLogOn.UserodSimpleSwitch,isDomainAuthenticated:true),
					ProcedureGroup.ProcNum,DateTime.MinValue);
			}
			_userod2=formLogOn.UserodSimpleSwitch;
			bool canUserSignNote=Userods.CanUserSignNote(_userod2);
			signatureBoxWrapper2.Enabled=canUserSignNote;
			if(!labelPermAlert.Visible && !canUserSignNote) {
				labelPermAlert.Text=Lans.g(this,"Notes can only be signed by providers.");
				labelPermAlert.Visible=true;
			}
			_hasUser2Changed=true;
			signatureBoxWrapper2.ClearSignature();
			signatureBoxWrapper2.UserSig=_userod2;
			textUser2.Text=_userod2.UserName;
			_hasSig2Changed=true;
		}

		///<summary>This button is only visible if 1. Pref ProcLockingIsAllowed is true, 2. Proc isn't already locked, 3. Proc status is C.</summary>
		private void butLock_Click(object sender,EventArgs e) {
			if(!EntriesAreValid()) {
				return;
			}
			if(!TryResolveNoteMerge()) {
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
				if(dateProc.Date!=_procedureGroupOld.ProcDate.Date && dateProc.Date>DateTime.Today && !PrefC.GetBool(PrefName.FutureTransDatesAllowed)) {
					MsgBox.Show(this,"Group notes cannot be dated in the future.");
					return false;
				}
			}
			//The permissions for what is saved, not only for what was attached when the window opened: adding a completed procedure, removing every
			//procedure or moving the date can need Edit Completed Procedure, on the new date (which also applies the global lock date) and the old one.
			if(!IsAuthorizedForChanges(ListProcedures,GetProcDateEntered(),IsNoteOrSignatureChanged())) {
				return false;
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
			return AreSecondSignatureEntriesValid(hasAutoNotePrompt);
		}

		///<summary>26.1: the same checks for the second box, and the second signer must be another user than the first.</summary>
		private bool AreSecondSignatureEntriesValid(bool hasAutoNotePrompt) {
			if(_hasSig2Changed && !signatureBoxWrapper2.SigIsBlank && hasAutoNotePrompt && PrefC.GetBool(PrefName.ProcNoteSigsBlockedAutoNoteIncomplete)) {
				MessageBox.Show(Lan.g(this,"Remaining auto note prompts must be completed to sign this note. Use Edit Auto Note to resume."));
				return false;
			}
			if(!signatureBoxWrapper2.IsValid) {
				MsgBox.Show(this,"Signature 2 is invalid. Please sign it again, or clear it, and click OK again.");
				return false;
			}
			long userNum2=_hasSig2Changed ? _userod2.UserNum : ProcedureGroup.UserNum2;
			string error=GroupNoteSignatures.GetSignersError(ProcedureGroup.UserNum,signatureBoxWrapper.SigIsBlank,userNum2,signatureBoxWrapper2.SigIsBlank);
			if(error!=null) {
				MsgBox.Show(this,error);
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
			//26.1: who made or cleared the second signature.
			logText=GroupNoteSignatures.AppendSecondSignatureLog(logText,
				GroupNoteSignatures.GetSecondSignatureLogPart(_procedureGroupOld,ProcedureGroup,x => Userods.GetName(x)),ProcedureGroup.ProcDate);
			if(logText!="") {
				//Logged as a procedure edit on the group note.
				SecurityLogs.MakeLogEntry(EnumPermType.ProcEdit,ProcedureGroup.PatNum,logText,ProcedureGroup.ProcNum,_procedureGroupOld.DateTStamp);
			}
			//25.4: merges resolved in this window, under the permission the note is saved with.
			for(int i=0;i<_listNoteMergeLogTexts.Count;i++) {
				SecurityLogs.MakeLogEntry(ProcNoteMerges.GetLogPermission(GroupNotes.IsTreatedAsComplete(_listProcStatsAtLoad)),ProcedureGroup.PatNum,
					_listNoteMergeLogTexts[i],ProcedureGroup.ProcNum,DateTime.MinValue);
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
			if(!TryResolveNoteMerge()) {
				return;
			}
			if(!ConfirmSecondSignatureRemoved()) {
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
			AlertSecondSignerCleared();
			SaveAndClose();
		}

		#region 26.1 second signature
		///<summary>Fills the second signature from the newest note row when the group note was loaded by a reader that does not select it (most do not,
		///for example Procedures.GetOneProc), and the row is the note the window shows.</summary>
		private void LoadSecondSignature() {
			if(_procNoteNewestAtLoad==null || ProcedureGroup.UserNum2!=0 || !string.IsNullOrEmpty(ProcedureGroup.Signature2)) {
				return;
			}
			if(ProcNoteMerges.IsNoteTextChanged(_procNoteNewestAtLoad.Note,ProcedureGroup.Note) || _procNoteNewestAtLoad.UserNum!=ProcedureGroup.UserNum
				|| (_procNoteNewestAtLoad.Signature??"")!=(ProcedureGroup.Signature??""))
			{
				return;
			}
			ProcNotes.CopySecondSignatureTo(_procNoteNewestAtLoad,ProcedureGroup);
		}

		///<summary>Asks before a second signature that was saved with the note is removed (the box is blank now). False when the user goes back.</summary>
		private bool ConfirmSecondSignatureRemoved() {
			if(_procedureGroupOld.Signature2.IsNullOrEmpty() || !_hasSig2Changed || !signatureBoxWrapper2.SigIsBlank) {
				return true;
			}
			return MsgBox.Show(this,MsgBoxButtons.OKCancel,
				"Signature 2 has not been re-signed.  Continuing will remove the previous second signature from this group note.  Exit anyway?");
		}

		///<summary>The "signature cleared" alert for the second signer, as the first signer gets it: when somebody else cleared or replaced their
		///signature, and they subscribe to the alert.</summary>
		private void AlertSecondSignerCleared() {
			List<long> listUserNumsActing=new List<long> { Security.CurUser.UserNum,_userod.UserNum,_userod2.UserNum };
			//After Keep Mine or Save Merged the old copy holds the newer note's second signature, which the save replaces even when box 2 was not touched.
			bool isSig2Changed=_hasSig2Changed || ProcNotes.IsSecondSignatureChanged(ProcedureGroup,_procedureGroupOld);
			long userNum2=GroupNoteSignatures.GetSecondSignerToAlert(_procedureGroupOld,isSig2Changed,listUserNumsActing);
			if(userNum2==0 || !AlertSubs.GetAllAlertTypesForUser(userNum2).Contains(AlertType.SignatureCleared)) {
				return;
			}
			string alertDescription=GroupNoteSignatures.GetSignatureClearedAlertText(_procedureGroupOld,ProcedureCodes.GetStringProcCode(_procedureGroupOld.CodeNum),
				Providers.GetAbbr(_procedureGroupOld.ProvNum),isSecondSignature:true);
			AlertItems.Insert(new AlertItem {
				Actions=ActionType.MarkAsRead | ActionType.Delete,
				Description=Lans.g("Procedures",alertDescription),
				Severity=SeverityType.Low,
				Type=AlertType.SignatureCleared,
				UserNum=userNum2,
				ClinicNum=_procedureGroupOld.ClinicNum,
			});
		}
		#endregion 26.1 second signature

		#region 25.4 Procedure Note Merge
		///<summary>Before saving: when somebody else saved a newer note on this group note since the window opened, and this save writes a new note row,
		///shows the Procedure Note Merge window. False when the window stays open: Cancel, or the saved text differs from what was signed here, so the
		///user signs again. Not shown when the office uses the older automatic merge (preference ProcNoteConcurrencyMerge).</summary>
		private bool TryResolveNoteMerge() {
			if(ProcedureGroup.ProcNum==0 || ProcNoteMerges.IsAutoMergeOn()) {
				return true;
			}
			//Exactly when Procedures.Update will write a note row (the text compared exactly, as it does).
			bool isNoteRowWritten=ProcNoteMerges.IsNoteRowWritten(ProcedureGroup,_procedureGroupOld,textNotes.Text,_hasSigChanged || _hasSig2Changed);
			ProcNote procNoteNow=ProcNotes.GetNewestForProc(ProcedureGroup.ProcNum);
			if(!ProcNoteMerges.IsMergeNeeded(_procNoteNewestAtLoad,procNoteNow,isNoteRowWritten)) {
				return true;
			}
			bool isSig1Drawn=_hasSigChanged && !signatureBoxWrapper.SigIsBlank;
			bool isSig2Drawn=_hasSig2Changed && !signatureBoxWrapper2.SigIsBlank;
			bool isTheirsSigned=!procNoteNow.Signature.IsNullOrEmpty() || !procNoteNow.Signature2.IsNullOrEmpty();
			//Rule 4 applies to the newer note too: only its signers, or a user with Group Note Edit Signed, may change it. A user whose note box is
			//read-only here (for example a co-signer of someone else's note) never changes the text through the merge window.
			bool canEditTheirs=GroupNoteSignatures.CanEditNoteText(Security.IsAuthorized(EnumPermType.GroupNoteEditSigned,true),Security.CurUser.UserNum,
				procNoteNow.UserNum,procNoteNow.Signature.IsNullOrEmpty(),procNoteNow.UserNum2,procNoteNow.Signature2.IsNullOrEmpty());
			bool canChangeText=ProcNoteMerges.CanChangeText(!textNotes.ReadOnly,canEditTheirs);
			string noteMine=textNotes.Text;
			FrmProcNoteMerge frmProcNoteMerge=new FrmProcNoteMerge(procNoteNow.Note,Userods.GetName(procNoteNow.UserNum),procNoteNow.EntryDateTime,isTheirsSigned,
				noteMine,ProcNoteMerges.GetMergedDefault(procNoteNow.Note,_procedureGroupOld.Note,noteMine),isSig1Drawn || isSig2Drawn,canChangeText);
			frmProcNoteMerge.ShowDialog();
			ProcNoteMergeChoice choice=frmProcNoteMerge.MergeChoice;
			if(choice==ProcNoteMergeChoice.Cancel || !ProcNoteMerges.IsChoiceAllowed(choice,canChangeText)) {
				return false;
			}
			if(choice==ProcNoteMergeChoice.KeepTheirs && !canEditTheirs && IsStructureOrProviderChanged()) {
				//Saving them would change (and unsign) a note this user may not edit. Nothing is taken over yet; the next save asks again.
				MsgBox.Show(this,"The newer note is signed by another user, so the date, the provider and the attached procedures can't be changed here. Undo those changes, or close this window without saving.");
				return false;
			}
			//From now on the newer note is the one this window saves over.
			_procNoteNewestAtLoad=procNoteNow;
			ProcNoteMerges.TakeAsBaseline(_procedureGroupOld,procNoteNow);
			_listNoteMergeLogTexts.Add(ProcNoteMerges.GetLogText(choice,Lan.g(this,"Group note")+" "+ProcedureGroup.ProcDate.ToShortDateString(),
				Userods.GetName(procNoteNow.UserNum)));
			if(choice==ProcNoteMergeChoice.KeepTheirs) {
				return KeepTheirNote(procNoteNow,canEditTheirs,isSig1Drawn || isSig2Drawn);
			}
			if(!procNoteNow.Signature.IsNullOrEmpty() && procNoteNow.UserNum!=Security.CurUser.UserNum && procNoteNow.UserNum!=_userod.UserNum) {
				_hasUserChanged=true;//Their signature is not kept: the usual prompt and "signature cleared" alert go to their signer.
			}
			string noteResult=ProcNoteMerges.GetResultText(choice,procNoteNow.Note,noteMine,frmProcNoteMerge.NoteMerged);
			if(ProcNoteMerges.IsNoteTextChanged(textNotes.Text,noteResult)) {
				textNotes.Text=noteResult;//Clears signatures loaded with the note; signatures drawn here are checked below.
			}
			bool isResign1=ProcNoteMerges.IsResignNeeded(isSig1Drawn,noteMine,noteResult);
			bool isResign2=ProcNoteMerges.IsResignNeeded(isSig2Drawn,noteMine,noteResult);
			if(isResign1) {
				signatureBoxWrapper.ClearSignature();
			}
			if(isResign2) {
				signatureBoxWrapper2.ClearSignature();
			}
			if(isResign1 || isResign2) {
				MsgBox.Show(this,"The saved note differs from the note you signed. Please sign again, then save.");
				return false;
			}
			return true;
		}

		///<summary>Keep Theirs: the window takes the newer note with its signatures and users; my other changes are still saved, and no note row is
		///written. When the date or the attached procedures changed here, their signatures no longer fit, so they are cleared and, when there were any,
		///the user signs again (only reached when the user may edit their note). A signature drawn here was made over my note, which is not kept, so it
		///is dropped and the window stays open (false) for signing the kept note again. When the user may not edit the kept note (canEditTheirs), the
		///note and the boxes they may not sign become read-only, as when such a note is opened.</summary>
		private bool KeepTheirNote(ProcNote procNoteTheirs,bool canEditTheirs,bool isSigDrawn) {
			bool isStartingUpOld=_isStartingUp;
			_isStartingUp=true;
			signatureBoxWrapper.ClearSignature();
			signatureBoxWrapper2.ClearSignature();
			textNotes.Text=procNoteTheirs.Note??"";
			//The text as the note box returns it, in both copies, so Procedures.Update writes no note row.
			ProcNoteMerges.ApplyTheirNote(ProcedureGroup,_procedureGroupOld,procNoteTheirs,textNotes.Text);
			textUser.Text=Userods.GetName(procNoteTheirs.UserNum);
			textUser2.Text=(procNoteTheirs.UserNum2==0) ? "" : Userods.GetName(procNoteTheirs.UserNum2);
			_hasUserChanged=false;
			bool isTheirsSigned=!procNoteTheirs.Signature.IsNullOrEmpty() || !procNoteTheirs.Signature2.IsNullOrEmpty();
			if(IsStructureChanged()) {
				//Their signatures were made over the old date and procedures. The blank boxes are saved.
				_hasSigChanged=true;
				_hasSig2Changed=true;
				//Their first signer gets the usual prompt and "signature cleared" alert on save (the second signer gets AlertSecondSignerCleared).
				_hasUserChanged=!procNoteTheirs.Signature.IsNullOrEmpty() && procNoteTheirs.UserNum!=Security.CurUser.UserNum
					&& procNoteTheirs.UserNum!=_userod.UserNum;
				_isStartingUp=isStartingUpOld;
				if(isTheirsSigned || isSigDrawn) {
					MsgBox.Show(this,"The signatures on that note no longer apply because the date or the attached procedures changed here. Please sign again, then save.");
					return false;
				}
				return true;
			}
			_hasSigChanged=false;
			_hasSig2Changed=false;
			signatureBoxWrapper.FillSignature(ProcedureGroup.SigIsTopaz,GetSignatureKey(),ProcedureGroup.Signature);
			signatureBoxWrapper2.FillSignature(ProcedureGroup.SigIsTopaz2,GroupNoteSignatures.GetSignatureKey2(ProcedureGroup,ListProcGroupItems),
				ProcedureGroup.Signature2);
			_isStartingUp=isStartingUpOld;
			if(!canEditTheirs) {
				LockForSignedNote();
			}
			if(!isSigDrawn) {
				return true;
			}
			if(signatureBoxWrapper.Enabled || signatureBoxWrapper2.Enabled) {
				MsgBox.Show(this,"Your signature was made over your note, which was not kept. Please sign the note again, then save.");
			}
			else {
				MsgBox.Show(this,"Your signature was not saved, because the note you kept was signed by another user. Save again to save your other changes.");
			}
			return false;
		}

		///<summary>True when the date or the attached procedures were changed in this window. The signatures are bound to both.</summary>
		private bool IsStructureChanged() {
			if(!_canEditStructure) {
				return false;
			}
			if(GetProcDateEntered().Date!=_procedureGroupOld.ProcDate.Date) {
				return true;
			}
			return GetProceduresAdded().Count>0 || GetProceduresRemoved().Count>0;
		}

		///<summary>True when the date, the provider or the attached procedures were changed in this window: the changes only a user who may edit the
		///note can make (25.1).</summary>
		private bool IsStructureOrProviderChanged() {
			return IsStructureChanged() || (_canEditStructure && comboProv.GetSelectedProvNum()!=_procedureGroupOld.ProvNum);
		}

		///<summary>Keep Theirs kept a note this user may not edit (rule 4: signed by another user, and no Group Note Edit Signed): the note, the first
		///box and the date, provider and procedures become read-only, as when such a note is opened, and the second box stays usable only when the user
		///may still sign it (GroupNoteSignatures.CanSignBox2). Called only when no date, provider or procedure change is pending.</summary>
		private void LockForSignedNote() {
			bool hasEditSignedPermission=Security.IsAuthorized(EnumPermType.GroupNoteEditSigned,true);
			if(!GroupNoteSignatures.CanEditNoteText(hasEditSignedPermission,Security.CurUser.UserNum,ProcedureGroup.UserNum,signatureBoxWrapper.SigIsBlank,
				ProcedureGroup.UserNum2,signatureBoxWrapper2.SigIsBlank))
			{
				textNotes.ReadOnly=true;
				signatureBoxWrapper.Enabled=false;
				labelPermAlert.Text=Lan.g(this,"Notes and Signature locked. The note you kept was signed by another user.");
				labelPermAlert.Visible=true;
				butAppend.Enabled=false;
				buttonUseAutoNote.Enabled=false;
				butChangeUser.Enabled=false;
				butDelete.Enabled=false;
				_canEditStructure=false;
				textProcDate.ReadOnly=true;
				comboProv.Enabled=false;
				butAdd.Enabled=false;
				butRemove.Enabled=false;
			}
			if(!GroupNoteSignatures.CanSignBox2(hasEditSignedPermission,Security.CurUser.UserNum,ProcedureGroup.UserNum2,signatureBoxWrapper2.SigIsBlank)) {
				butChangeUser2.Enabled=false;
				signatureBoxWrapper2.Enabled=false;
			}
		}
		#endregion 25.4 Procedure Note Merge

		#region 25.4 window size
		///<summary>The window opens larger than before 25.4 and can be resized; the Notes box takes the extra space. A size the user chose is
		///remembered per user on this workstation (fork) and used the next time, within the screen's working area.</summary>
		private void ApplyRememberedSize() {
			_sizeDefault=Size;
			MinimumSize=new Size(_sizeDefault.Width,Math.Min(_sizeDefault.Height,LayoutManager.Scale(560)));
			Tuple<int,int> size96=LocalWindowSizes.Get(WINDOW_SIZE_NAME,Security.CurUser.UserNum);
			if(size96==null) {
				return;
			}
			Rectangle rectangleWorkingArea=System.Windows.Forms.Screen.FromControl(this).WorkingArea;
			Tuple<int,int> sizeOpen=LocalWindowSizes.GetSizeToOpen(Tuple.Create(LayoutManager.Scale(size96.Item1),LayoutManager.Scale(size96.Item2)),
				MinimumSize.Width,MinimumSize.Height,rectangleWorkingArea.Width,rectangleWorkingArea.Height);
			Point pointCenter=new Point(Left+Width/2,Top+Height/2);
			Size=new Size(sizeOpen.Item1,sizeOpen.Item2);
			int x=Math.Min(Math.Max(rectangleWorkingArea.Left,pointCenter.X-Width/2),Math.Max(rectangleWorkingArea.Left,rectangleWorkingArea.Right-Width));
			int y=Math.Min(Math.Max(rectangleWorkingArea.Top,pointCenter.Y-Height/2),Math.Max(rectangleWorkingArea.Top,rectangleWorkingArea.Bottom-Height));
			Location=new Point(x,y);
		}

		private void FormProcGroup_FormClosed(object sender,FormClosedEventArgs e) {
			if(_sizeDefault.IsEmpty || WindowState!=FormWindowState.Normal || Security.CurUser==null) {
				return;
			}
			LocalWindowSizes.Set(WINDOW_SIZE_NAME,Security.CurUser.UserNum,LayoutManager.Unscale(Width),LayoutManager.Unscale(Height));
		}
		#endregion 25.4 window size

		private void FormProcGroup_FormClosing(object sender,FormClosingEventArgs e) {
			signatureBoxWrapper?.SetTabletState(0);
			signatureBoxWrapper2?.SetTabletState(0);
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
