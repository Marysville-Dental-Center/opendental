using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Procedure Note Merge in Procedure Info (Open Dental 25.4, package M2-NOTES). Kept in its own file so FormProcEdit.cs only has short
	///hooks: NoteMerge_Load at the start of the load, NoteMerge_TryResolve at the start of SaveAndClose and in butLock_Click before the lock flag is
	///set, NoteMerge_Saved at the end of SaveAndClose, and one line in butSave_Click that records the signer it alerted.</summary>
	public partial class FormProcEdit {
		///<summary>The newest procnote row of the procedure when the window opened (null when it had none).</summary>
		private ProcNote _procNoteNewestAtLoad;
		///<summary>Merges resolved in this window, logged once the procedure is saved (NoteMerge_Saved).</summary>
		private List<string> _listNoteMergeLogTexts=new List<string>();
		///<summary>Signers of the newer notes that Keep Mine or Save Merged replaced, alerted once the procedure is saved (NoteMerge_Saved), never
		///before: the save can still stop (sign again, Cancel, or a later check in SaveAndClose).</summary>
		private List<long> _listNoteMergeUserNumsToAlert=new List<long>();
		///<summary>Set by butSave_Click when, in this click, it raised the "signature cleared" alert for the signer of the note as loaded. Read and reset
		///by NoteMerge_TryResolve, so it never carries over to another click (Lock and Set Complete raise no such alert).</summary>
		private long _noteMergeUserNumAlertedOnSave;
		///<summary>The signer the save code alerted in the click now being saved (0 when it alerted nobody).</summary>
		private long _noteMergeUserNumAlertedThisClick;

		///<summary>Remembers the newest note. Called at the start of the load.</summary>
		private void NoteMerge_Load() {
			if(_procedure.ProcNum!=0) {
				_procNoteNewestAtLoad=ProcNotes.GetNewestForProc(_procedure.ProcNum);
			}
		}

		///<summary>Called before anything is written: first in SaveAndClose (Save, Set Complete, Lock), and in butLock_Click before the lock flag is
		///set. When somebody else saved a newer note on this procedure since the window opened, and this save writes a new note row, shows the Procedure
		///Note Merge window. False when nothing may be saved yet: Cancel (back to Procedure Info), or the saved text differs from the text signed here,
		///so the user signs again. Not shown for a new procedure, or when the office uses the older automatic merge (preference
		///ProcNoteConcurrencyMerge, Procedures.SetNote). Alerts and audit entries wait for NoteMerge_Saved.</summary>
		private bool NoteMerge_TryResolve() {
			_noteMergeUserNumAlertedThisClick=_noteMergeUserNumAlertedOnSave;
			_noteMergeUserNumAlertedOnSave=0;
			if(IsNew || _procedure.ProcNum==0 || ProcNoteMerges.IsAutoMergeOn()) {
				return true;
			}
			//Exactly when Procedures.Update will write a note row (the text compared exactly, as it does).
			bool isNoteRowWritten=ProcNoteMerges.IsNoteRowWritten(_procedure,_procedureOld,textNotes.Text,_signatureChanged);
			ProcNote procNoteNow=ProcNotes.GetNewestForProc(_procedure.ProcNum);
			if(!ProcNoteMerges.IsMergeNeeded(_procNoteNewestAtLoad,procNoteNow,isNoteRowWritten)) {
				return true;
			}
			bool isSigDrawn=_signatureChanged && !signatureBoxWrapper.SigIsBlank;
			bool isTheirsSigned=!procNoteNow.Signature.IsNullOrEmpty();
			//The note's permissions apply to the newer note too: without them only Keep Theirs and Cancel are offered.
			bool canEditTheirs=ProcNoteMerges.CanEditProcNote(Security.IsAuthorized(EnumPermType.ProcedureNoteFull,true),
				Security.IsAuthorized(EnumPermType.ProcedureNoteUser,true),Security.CurUser.UserNum,procNoteNow.UserNum,procNoteNow.Signature.IsNullOrEmpty());
			bool canChangeText=ProcNoteMerges.CanChangeText(!textNotes.ReadOnly,canEditTheirs);
			string noteMine=textNotes.Text;
			FrmProcNoteMerge frmProcNoteMerge=new FrmProcNoteMerge(procNoteNow.Note,Userods.GetName(procNoteNow.UserNum),procNoteNow.EntryDateTime,isTheirsSigned,
				noteMine,ProcNoteMerges.GetMergedDefault(procNoteNow.Note,_procedureOld.Note,noteMine),isSigDrawn,canChangeText);
			frmProcNoteMerge.ShowDialog();
			ProcNoteMergeChoice choice=frmProcNoteMerge.MergeChoice;
			if(choice==ProcNoteMergeChoice.Cancel || !ProcNoteMerges.IsChoiceAllowed(choice,canChangeText)) {
				return false;
			}
			//From now on the newer note is the one this window saves over.
			_procNoteNewestAtLoad=procNoteNow;
			ProcNoteMerges.TakeAsBaseline(_procedureOld,procNoteNow);
			ProcNotes.CopySecondSignatureTo(procNoteNow,_procedure);
			_listNoteMergeLogTexts.Add(ProcNoteMerges.GetLogText(choice,
				ProcedureCodes.GetStringProcCode(_procedure.CodeNum,doThrowIfMissing:false)+" "+_procedure.ProcDate.ToShortDateString(),
				Userods.GetName(procNoteNow.UserNum)));
			//Keep Mine and Save Merged replace their signatures (none for Keep Theirs). butSave_Click already alerted the signer of the note as loaded
			//when another user cleared that signature in this click.
			_listNoteMergeUserNumsToAlert.AddRange(ProcNoteMerges.GetTheirSignersToAlert(choice,procNoteNow,
				new List<long> { Security.CurUser.UserNum,_Userod.UserNum,_noteMergeUserNumAlertedThisClick }));
			if(choice==ProcNoteMergeChoice.KeepTheirs) {
				return NoteMerge_KeepTheirs(procNoteNow,canEditTheirs,isSigDrawn);
			}
			string noteResult=ProcNoteMerges.GetResultText(choice,procNoteNow.Note,noteMine,frmProcNoteMerge.NoteMerged);
			if(ProcNoteMerges.IsNoteTextChanged(textNotes.Text,noteResult)) {
				textNotes.Text=noteResult;//Clears a signature loaded with the note; a signature drawn here is checked below.
			}
			if(ProcNoteMerges.IsResignNeeded(isSigDrawn,noteMine,noteResult)) {
				signatureBoxWrapper.ClearSignature();
				MsgBox.Show(this,"The saved note differs from the note you signed. Please sign again, then save.");
				return false;
			}
			return true;
		}

		///<summary>Keep Theirs: the newer note with its signature and user; the other changes in the window are still saved, and no note row is
		///written. A signature drawn here was made over my note, which is not kept, so it is dropped: false (the window stays open) so the user can sign
		///the kept note, when they may, and save again. When they may not edit the kept note (canEditTheirs), the note and its signature box become
		///read-only, as when such a note is opened.</summary>
		private bool NoteMerge_KeepTheirs(ProcNote procNoteTheirs,bool canEditTheirs,bool isSigDrawn) {
			bool isStartingUpOld=_isStartingUp;
			_isStartingUp=true;//Keeps the note box from clearing the signature.
			signatureBoxWrapper.ClearSignature();
			textNotes.Text=procNoteTheirs.Note??"";
			//The text as the note box returns it, in both copies, so Procedures.Update writes no note row.
			ProcNoteMerges.ApplyTheirNote(_procedure,_procedureOld,procNoteTheirs,textNotes.Text);
			textUser.Text=Userods.GetName(procNoteTheirs.UserNum);
			_signatureChanged=false;//SaveSignature keeps their signature.
			_hasUserChanged=false;//Clearing the box above is not a change of the note's user.
			signatureBoxWrapper.FillSignature(_procedure.SigIsTopaz,Procedures.GetSignatureKeyData(_procedure),_procedure.Signature);
			_isStartingUp=isStartingUpOld;
			if(!canEditTheirs) {
				NoteMerge_LockNote();
			}
			if(!isSigDrawn) {
				return true;
			}
			if(canEditTheirs) {
				MsgBox.Show(this,"Your signature was made over your note, which was not kept. Please sign the note again, then save.");
			}
			else {
				MsgBox.Show(this,"Your signature was not saved, because the note you kept was signed by another user. Save again to save your other changes.");
			}
			return false;
		}

		///<summary>The kept note is signed by another user and this user may not edit it (CanEditProcNote): the note box and its signature are locked,
		///as when such a note is opened.</summary>
		private void NoteMerge_LockNote() {
			textNotes.ReadOnly=true;
			buttonUseAutoNote.Enabled=false;
			butEditAutoNote.Enabled=false;
			signatureBoxWrapper.Enabled=false;
			butAppend.Enabled=false;
			butChangeUser.Enabled=false;
			labelPermAlert.Text=Lan.g(this,"Notes and Signature locked. The note you kept was signed by another user.");
			labelPermAlert.Visible=true;
		}

		///<summary>Called at the end of SaveAndClose, once the procedure was saved, and only then: the "signature cleared" alert for the signers whose
		///newer note Keep Mine or Save Merged replaced (when subscribed), and one audit entry per resolved merge, under the permission the note is saved
		///with. A merge whose save never went through (Cancel, sign again and then close, a check later in SaveAndClose, Delete) leaves neither.</summary>
		private void NoteMerge_Saved() {
			List<long> listUserNumsActing=new List<long> { Security.CurUser.UserNum,_Userod.UserNum,_noteMergeUserNumAlertedThisClick };
			List<long> listUserNums=ProcNoteMerges.GetSignersToAlertOnSave(_listNoteMergeUserNumsToAlert,listUserNumsActing);
			for(int i=0;i<listUserNums.Count;i++) {
				if(!AlertSubs.GetAllAlertTypesForUser(listUserNums[i]).Contains(AlertType.SignatureCleared)) {
					continue;
				}
				string alertDescription="Procedure Note Changed for PatNum: "+_procedure.PatNum.ToString()
					+" Date: "+_procedure.ProcDate.ToShortDateString()
					+" Procedure Code: "+ProcedureCodes.GetStringProcCode(_procedure.CodeNum,doThrowIfMissing:false);
				AlertItems.Insert(new AlertItem {
					Actions=ActionType.MarkAsRead | ActionType.Delete,
					Description=Lans.g("Procedures",alertDescription),
					Severity=SeverityType.Low,
					Type=AlertType.SignatureCleared,
					UserNum=listUserNums[i],
					ClinicNum=_procedure.ClinicNum,
				});
			}
			EnumPermType permType=ProcNoteMerges.GetLogPermission(ProcNoteMerges.IsCompleteStatus(_procedure.ProcStatus));
			for(int i=0;i<_listNoteMergeLogTexts.Count;i++) {
				SecurityLogs.MakeLogEntry(permType,_procedure.PatNum,_listNoteMergeLogTexts[i],_procedure.ProcNum,DateTime.MinValue);
			}
			_listNoteMergeUserNumsToAlert.Clear();
			_listNoteMergeLogTexts.Clear();
		}
	}
}
