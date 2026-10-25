using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Procedure Note Merge in Procedure Info (Open Dental 25.4, package M2-NOTES). Kept in its own file so FormProcEdit.cs only has two
	///one-line hooks: NoteMerge_Load at the start of the load, and NoteMerge_TryResolve at the start of SaveAndClose.</summary>
	public partial class FormProcEdit {
		///<summary>The newest procnote row of the procedure when the window opened (null when it had none).</summary>
		private ProcNote _procNoteNewestAtLoad;
		///<summary>Merges resolved in this window, logged once the procedure is saved.</summary>
		private List<string> _listNoteMergeLogTexts=new List<string>();

		///<summary>Remembers the newest note. Called at the start of the load.</summary>
		private void NoteMerge_Load() {
			if(_procedure.ProcNum!=0) {
				_procNoteNewestAtLoad=ProcNotes.GetNewestForProc(_procedure.ProcNum);
			}
			FormClosed+=NoteMerge_FormClosed;
		}

		///<summary>Called first in SaveAndClose, before anything is written. When somebody else saved a newer note on this procedure since the window
		///opened, and this save writes a new note row, shows the Procedure Note Merge window. False when nothing may be saved yet: Cancel (back to
		///Procedure Info), or the saved text differs from the text signed here, so the user signs again. Not shown for a new procedure, or when the office
		///uses the older automatic merge (preference ProcNoteConcurrencyMerge, Procedures.SetNote).</summary>
		private bool NoteMerge_TryResolve() {
			if(IsNew || _procedure.ProcNum==0 || ProcNoteMerges.IsAutoMergeOn()) {
				return true;
			}
			bool isNoteRowWritten=ProcNoteMerges.IsNoteTextChanged(_procedureOld.Note,textNotes.Text) || _signatureChanged;
			ProcNote procNoteNow=ProcNotes.GetNewestForProc(_procedure.ProcNum);
			if(!ProcNoteMerges.IsMergeNeeded(_procNoteNewestAtLoad,procNoteNow,isNoteRowWritten)) {
				return true;
			}
			bool isSigDrawn=_signatureChanged && !signatureBoxWrapper.SigIsBlank;
			bool isTheirsSigned=!procNoteNow.Signature.IsNullOrEmpty();
			string noteMine=textNotes.Text;
			FrmProcNoteMerge frmProcNoteMerge=new FrmProcNoteMerge(procNoteNow.Note,Userods.GetName(procNoteNow.UserNum),procNoteNow.EntryDateTime,isTheirsSigned,
				noteMine,ProcNoteMerges.GetMergedDefault(procNoteNow.Note,_procedureOld.Note,noteMine),isSigDrawn);
			frmProcNoteMerge.ShowDialog();
			ProcNoteMergeChoice choice=frmProcNoteMerge.MergeChoice;
			if(choice==ProcNoteMergeChoice.Cancel) {
				return false;
			}
			//butSave_Click already alerted the signer of the note as loaded when another user cleared that signature.
			long userNumAlertedOnSave=(_hasUserChanged && !_procedureOld.Signature.IsNullOrEmpty()) ? _procedureOld.UserNum : 0;
			//From now on the newer note is the one this window saves over.
			_procNoteNewestAtLoad=procNoteNow;
			_procedureOld.Note=procNoteNow.Note??"";
			_procedureOld.UserNum=procNoteNow.UserNum;
			_procedureOld.Signature=procNoteNow.Signature??"";
			_procedureOld.SigIsTopaz=procNoteNow.SigIsTopaz;
			ProcNotes.CopySecondSignatureTo(procNoteNow,_procedureOld);
			ProcNotes.CopySecondSignatureTo(procNoteNow,_procedure);
			_listNoteMergeLogTexts.Add(ProcNoteMerges.GetLogText(choice,
				ProcedureCodes.GetStringProcCode(_procedure.CodeNum,doThrowIfMissing:false)+" "+_procedure.ProcDate.ToShortDateString(),
				Userods.GetName(procNoteNow.UserNum)));
			if(choice==ProcNoteMergeChoice.KeepTheirs) {
				NoteMerge_KeepTheirs(procNoteNow);
				return true;
			}
			NoteMerge_AlertTheirSigner(choice,procNoteNow,userNumAlertedOnSave);
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

		///<summary>Keep Theirs: the newer note with its signature and user; the other changes in the window are still saved.</summary>
		private void NoteMerge_KeepTheirs(ProcNote procNoteTheirs) {
			bool isStartingUpOld=_isStartingUp;
			_isStartingUp=true;//Keeps the note box from clearing the signature.
			signatureBoxWrapper.ClearSignature();
			textNotes.Text=procNoteTheirs.Note??"";
			_procedure.Note=procNoteTheirs.Note??"";
			_procedure.UserNum=procNoteTheirs.UserNum;
			_procedure.Signature=procNoteTheirs.Signature??"";
			_procedure.SigIsTopaz=procNoteTheirs.SigIsTopaz;
			textUser.Text=Userods.GetName(procNoteTheirs.UserNum);
			_signatureChanged=false;//SaveSignature keeps their signature.
			signatureBoxWrapper.FillSignature(_procedure.SigIsTopaz,Procedures.GetSignatureKeyData(_procedure),_procedure.Signature);
			_isStartingUp=isStartingUpOld;
		}

		///<summary>Keep Mine and Save Merged replace the newer note, so its signer gets the "signature cleared" alert (when subscribed), unless that is
		///the user at the keyboard or the user switched to here, or the loaded note's signer, who was already alerted by the save.</summary>
		private void NoteMerge_AlertTheirSigner(ProcNoteMergeChoice choice,ProcNote procNoteTheirs,long userNumAlertedOnSave) {
			List<long> listUserNumsActing=new List<long> { Security.CurUser.UserNum,_Userod.UserNum,userNumAlertedOnSave };
			List<long> listUserNums=ProcNoteMerges.GetTheirSignersToAlert(choice,procNoteTheirs,listUserNumsActing);
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
		}

		///<summary>Logs the merges once the procedure was saved, under the permission the note is saved with.</summary>
		private void NoteMerge_FormClosed(object sender,FormClosedEventArgs e) {
			if(DialogResult!=DialogResult.OK || _listNoteMergeLogTexts.Count==0) {
				return;
			}
			EnumPermType permType=ProcNoteMerges.GetLogPermission(ProcNoteMerges.IsCompleteStatus(_procedure.ProcStatus));
			for(int i=0;i<_listNoteMergeLogTexts.Count;i++) {
				SecurityLogs.MakeLogEntry(permType,_procedure.PatNum,_listNoteMergeLogTexts[i],_procedure.ProcNum,DateTime.MinValue);
			}
			_listNoteMergeLogTexts.Clear();
		}
	}
}
