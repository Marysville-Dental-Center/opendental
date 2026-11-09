// The Group Note window, Procedure Info and the merge window can't be opened in a test run (the GUI is not launched), so these tests read their
// sources and check that they use the rules the other files test, and that the project files include the new files.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace M2NOTESTests {
	[TestClass]
	public class WiringTests {
		private static string FormProcGroup() {
			return ScratchDb.ReadRepoFile("OpenPmsDental","Forms","FormProcGroup.cs");
		}

		private static string FormProcGroupDesigner() {
			return ScratchDb.ReadRepoFile("OpenPmsDental","Forms","FormProcGroup.designer.cs");
		}

		///<summary>A project file's evaluated items, so a check holds for the classic project files and the SDK-style ones alike.</summary>
		private static ProjectFile Project(string relativePath) {
			return ProjectFile.Load(ScratchDb.FindRepoFile(relativePath));
		}

		private static string Between(string source,string start,string end) {
			int idx=source.IndexOf(start);
			Assert.IsTrue(idx>=0,start);
			int idxEnd=source.IndexOf(end,idx);
			Assert.IsTrue(idxEnd>idx,end);
			return source.Substring(idx,idxEnd-idx);
		}

		[TestMethod]
		public void GroupNoteWindow_LargerAndResizable_NotesTakeTheSpace() {
			string designer=FormProcGroupDesigner();
			StringAssert.Contains(designer,"this.ClientSize = new System.Drawing.Size(990, 690);");
			StringAssert.DoesNotMatch(designer,new Regex(@"FormBorderStyle\.FixedSingle"),"Resizable.");
			StringAssert.DoesNotMatch(designer,new Regex(@"this\.MaximizeBox = false"));
			//The Notes box grows in both directions.
			StringAssert.Contains(Between(designer,"// textNotes","// butDelete"),
				"AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) \r\n            | System.Windows.Forms.AnchorStyles.Left) \r\n            | System.Windows.Forms.AnchorStyles.Right");
			StringAssert.Contains(designer,"this.textNotes.Size = new System.Drawing.Size(880, 231);");
			//The procedures grid keeps its height (bottom anchored, not top) and scrolls.
			string grid=Between(designer,"// gridProc","// textDateEntry");
			StringAssert.Contains(grid,"this.gridProc.Size = new System.Drawing.Size(864, 222);");
			StringAssert.DoesNotMatch(grid,new Regex(@"AnchorStyles\.Top"));
			StringAssert.Contains(grid,"AnchorStyles.Bottom");
			//The signature boxes stay visible at the bottom.
			foreach(string control in new[] { "signatureBoxWrapper","signatureBoxWrapper2","label15","labelSignature2","textUser2","butChangeUser2","labelPermAlert" }) {
				StringAssert.Contains(designer,"this."+control+".Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));",control);
			}
		}

		[TestMethod]
		public void GroupNoteWindow_SizeRememberedPerUser() {
			string source=FormProcGroup();
			StringAssert.Contains(source,"LocalWindowSizes.Get(WINDOW_SIZE_NAME,Security.CurUser.UserNum)");
			StringAssert.Contains(source,"LocalWindowSizes.Set(WINDOW_SIZE_NAME,Security.CurUser.UserNum,LayoutManager.Unscale(Width),LayoutManager.Unscale(Height))");
			StringAssert.Contains(source,"LocalWindowSizes.GetSizeToOpen(");
			StringAssert.Contains(FormProcGroupDesigner(),"this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FormProcGroup_FormClosed);");
		}

		[TestMethod]
		public void GroupNoteWindow_SecondSignatureBox() {
			string designer=FormProcGroupDesigner();
			StringAssert.Contains(designer,"this.labelSignature2.Text = \"Signature 2\";");
			StringAssert.Contains(designer,"this.signatureBoxWrapper2.SignatureChanged += new System.EventHandler(this.signatureBoxWrapper2_SignatureChanged);");
			StringAssert.Contains(designer,"this.butChangeUser2.Click += new System.EventHandler(this.butChangeUser2_Click);");
			string source=FormProcGroup();
			//Filled and saved with its own key.
			StringAssert.Contains(source,"signatureBoxWrapper2.FillSignature(ProcedureGroup.SigIsTopaz2,GroupNoteSignatures.GetSignatureKey2(ProcedureGroup,ListProcGroupItems),");
			StringAssert.Contains(source,"string keyData2=GroupNoteSignatures.GetSignatureKey2(ProcedureGroup,ListProcGroupItems);");
			StringAssert.Contains(source,"ProcedureGroup.SigIsTopaz2=signatureBoxWrapper2.GetSigIsTopaz();");
			//The edit rule applies to either signature; the second box can co-sign.
			StringAssert.Contains(source,"GroupNoteSignatures.CanEditNoteText(hasEditSignedPermission,Security.CurUser.UserNum,ProcedureGroup.UserNum,");
			StringAssert.Contains(source,"GroupNoteSignatures.CanSignBox2(hasEditSignedPermission,Security.CurUser.UserNum,ProcedureGroup.UserNum2,");
			//Editing the note, the date or the procedures clears both.
			StringAssert.Contains(Between(source,"private void textNotes_TextChanged(","private string GetSignatureKey()"),"signatureBoxWrapper2.ClearSignature();");
			StringAssert.Contains(Between(source,"private void ClearLoadedSignature()","private DateTime GetProcDateEntered()"),"signatureBoxWrapper2.ClearSignature();");
			//Change User with the domain login option, like the first box.
			string changeUser2=Between(source,"private void butChangeUser2_Click(","#endregion");
			StringAssert.Contains(changeUser2,"new FormLogOn(isSimpleSwitch:true,isSigBox:true)");
			StringAssert.Contains(changeUser2,"formLogOn.IsDomainAuthenticated");
			StringAssert.Contains(changeUser2,"DomainLogin.GetSwitchLogText(");
			StringAssert.Contains(changeUser2,"Userods.CanUserSignNote(_userod2)");
			//Saving: auto note block, invalid signature, a different second signer, the cleared alert and the log.
			string checks=Between(source,"private bool AreSecondSignatureEntriesValid(","private void SaveAndClose()");
			StringAssert.Contains(checks,"PrefName.ProcNoteSigsBlockedAutoNoteIncomplete");
			StringAssert.Contains(checks,"signatureBoxWrapper2.IsValid");
			StringAssert.Contains(checks,"GroupNoteSignatures.GetSignersError(");
			StringAssert.Contains(source,"bool isSig2Changed=_hasSig2Changed || ProcNotes.IsSecondSignatureChanged(ProcedureGroup,_procedureGroupOld);");
			StringAssert.Contains(source,"GroupNoteSignatures.GetSecondSignerToAlert(_procedureGroupOld,isSig2Changed,listUserNumsActing)");
			StringAssert.Contains(source,"GroupNoteSignatures.GetSecondSignatureLogPart(_procedureGroupOld,ProcedureGroup,");
			StringAssert.Contains(source,"signatureBoxWrapper2?.SetTabletState(0);");
		}

		[TestMethod]
		public void GroupNoteWindow_MergeBeforeSaveAndLock() {
			string source=FormProcGroup();
			foreach(string method in new[] { "private void butSave_Click(","private void butLock_Click(" }) {
				string body=Between(source,method,"\n\t\t}");
				int idxValid=body.IndexOf("EntriesAreValid()");
				int idxMerge=body.IndexOf("TryResolveNoteMerge()");
				Assert.IsTrue(idxValid>0 && idxMerge>idxValid,method);
			}
			StringAssert.Contains(source,"_procNoteNewestAtLoad=ProcNotes.GetNewestForProc(ProcedureGroup.ProcNum);");
			string merge=Between(source,"private bool TryResolveNoteMerge()","private bool KeepTheirNote(");
			StringAssert.Contains(merge,"ProcNoteMerges.IsAutoMergeOn()");
			StringAssert.Contains(merge,"ProcNoteMerges.IsMergeNeeded(_procNoteNewestAtLoad,procNoteNow,isNoteRowWritten)");
			StringAssert.Contains(merge,"new FrmProcNoteMerge(");
			StringAssert.Contains(merge,"ProcNoteMerges.IsResignNeeded(");
			StringAssert.Contains(source,"ProcNoteMerges.GetLogPermission(GroupNotes.IsTreatedAsComplete(_listProcStatsAtLoad))");
			//Whether this save writes a note row: Procedures.Update's own rule (exact text), not the line-break-blind comparison.
			StringAssert.Contains(merge,"bool isNoteRowWritten=ProcNoteMerges.IsNoteRowWritten(ProcedureGroup,_procedureGroupOld,textNotes.Text,_hasSigChanged || _hasSig2Changed);");
			StringAssert.DoesNotMatch(merge,new Regex(@"isNoteRowWritten=ProcNoteMerges\.IsNoteTextChanged"));
			//Rule 4 in the merge window: the text changes only when the note box was editable and the newer note may be edited by this user.
			StringAssert.Contains(merge,"bool canEditTheirs=GroupNoteSignatures.CanEditNoteText(Security.IsAuthorized(EnumPermType.GroupNoteEditSigned,true),Security.CurUser.UserNum,");
			StringAssert.Contains(merge,"procNoteNow.UserNum,procNoteNow.Signature.IsNullOrEmpty(),procNoteNow.UserNum2,procNoteNow.Signature2.IsNullOrEmpty());");
			StringAssert.Contains(merge,"bool canChangeText=ProcNoteMerges.CanChangeText(!textNotes.ReadOnly,canEditTheirs);");
			StringAssert.Contains(merge,",isSig1Drawn || isSig2Drawn,canChangeText);");
			StringAssert.Contains(merge,"if(choice==ProcNoteMergeChoice.Cancel || !ProcNoteMerges.IsChoiceAllowed(choice,canChangeText)) {");
			StringAssert.Contains(merge,"if(choice==ProcNoteMergeChoice.KeepTheirs && !canEditTheirs && IsStructureOrProviderChanged()) {");
			StringAssert.Contains(merge,"ProcNoteMerges.TakeAsBaseline(_procedureGroupOld,procNoteNow);");
			//Keep Theirs writes no row, drops a signature drawn over my note, and locks a note the user may not edit.
			string keepTheirs=Between(source,"private bool KeepTheirNote(","private bool IsStructureChanged()");
			StringAssert.Contains(keepTheirs,"ProcNoteMerges.ApplyTheirNote(ProcedureGroup,_procedureGroupOld,procNoteTheirs,textNotes.Text);");
			StringAssert.Contains(keepTheirs,"LockForSignedNote();");
			StringAssert.Contains(keepTheirs,"if(!isSigDrawn) {");
			string lockNote=Between(source,"private void LockForSignedNote()","#endregion 25.4 Procedure Note Merge");
			StringAssert.Contains(lockNote,"textNotes.ReadOnly=true;");
			StringAssert.Contains(lockNote,"signatureBoxWrapper2.Enabled=false;");
			StringAssert.Contains(lockNote,"GroupNoteSignatures.CanSignBox2(");
		}

		[TestMethod]
		public void ProcedureInfo_Hooks() {
			string source=ScratchDb.ReadRepoFile("OpenPmsDental","Forms","FormProcEdit.cs");
			Assert.AreEqual(1,Regex.Matches(source,@"NoteMerge_Load\(\);").Count);
			//In SaveAndClose (Save, Set Complete, Lock) and in butLock_Click.
			Assert.AreEqual(2,Regex.Matches(source,@"if\(!NoteMerge_TryResolve\(\)\) \{ return; \}").Count);
			Assert.AreEqual(1,Regex.Matches(source,@"NoteMerge_Saved\(\);").Count);
			string saveAndClose=Between(source,"private void SaveAndClose() {","\n\t\t}");
			int idxHook=saveAndClose.IndexOf("if(!NoteMerge_TryResolve()) { return; }");
			int idxFirstWrite=saveAndClose.IndexOf("Procedures.UpdateProcedureFields(");
			Assert.IsTrue(idxHook>0 && idxHook<idxFirstWrite,"The merge is resolved before anything is saved.");
			//Alerts and audit entries only for a save that went through: after the procedure is saved, right before the window closes as saved.
			int idxSaved=saveAndClose.IndexOf("NoteMerge_Saved();");
			Assert.IsTrue(idxSaved>saveAndClose.IndexOf("Procedures.FormProcEditUpdate("),"After the save.");
			Assert.IsTrue(idxSaved>saveAndClose.LastIndexOf("return;"),"After the last early return of SaveAndClose.");
			Assert.IsTrue(saveAndClose.IndexOf("DialogResult=DialogResult.OK;",idxSaved)>idxSaved,"Before the window closes as saved.");
			//Lock: the merge is resolved before the lock flag is set, as in the Group Note window; the window closes as saved only when SaveAndClose
			//saved (it sets DialogResult OK itself), otherwise the lock flag is undone and the window stays open.
			string lockBody=Between(source,"private void butLock_Click(","\n\t\t}");
			int idxValid=lockBody.IndexOf("if(!EntriesAreValid()) {");
			int idxMerge=lockBody.IndexOf("if(!NoteMerge_TryResolve()) { return; }");
			int idxLocked=lockBody.IndexOf("_procedure.IsLocked=true;");
			int idxSave=lockBody.IndexOf("SaveAndClose();");
			Assert.IsTrue(idxValid>0 && idxMerge>idxValid && idxLocked>idxMerge && idxSave>idxLocked,"Valid, merge, lock flag, save.");
			string afterSave=lockBody.Substring(idxSave);
			StringAssert.DoesNotMatch(afterSave,new Regex(@"DialogResult\s*=\s*DialogResult\.OK"),"Lock does not close as saved when the save stopped.");
			StringAssert.Contains(afterSave,"if(DialogResult!=DialogResult.OK) {");
			StringAssert.Contains(afterSave,"_procedure.IsLocked=false;");
			//The signer butSave_Click alerted in this click (the user was changed over a signed note) is not alerted again by the merge.
			string saveBody=Between(source,"private void butSave_Click(","\n\t\t}");
			string changeUserBlock=Between(saveBody,"if(_hasUserChanged && !_procedureOld.Signature.IsNullOrEmpty()) {","AlertItems.Insert(");
			StringAssert.Contains(changeUserBlock,"_noteMergeUserNumAlertedOnSave=_procedureOld.UserNum;");
			string partial=ScratchDb.ReadRepoFile("OpenPmsDental","Forms","FormProcEdit.NoteMerge.cs");
			//Nothing is alerted or logged while resolving; NoteMerge_Saved does both, and the window has no close handler that could log an unsaved merge.
			string resolve=Between(partial,"private bool NoteMerge_TryResolve() {","private bool NoteMerge_KeepTheirs(");
			StringAssert.DoesNotMatch(resolve,new Regex(@"AlertItems\.Insert|SecurityLogs\.MakeLogEntry"));
			StringAssert.Contains(resolve,"_listNoteMergeUserNumsToAlert.AddRange(ProcNoteMerges.GetTheirSignersToAlert(choice,procNoteNow,");
			StringAssert.Contains(resolve,"_noteMergeUserNumAlertedThisClick=_noteMergeUserNumAlertedOnSave;");
			StringAssert.Contains(resolve,"_noteMergeUserNumAlertedOnSave=0;");
			string saved=Between(partial,"private void NoteMerge_Saved() {","\n\t\t}");
			StringAssert.Contains(saved,"ProcNoteMerges.GetSignersToAlertOnSave(_listNoteMergeUserNumsToAlert,listUserNumsActing)");
			StringAssert.Contains(saved,"AlertItems.Insert(");
			StringAssert.Contains(saved,"SecurityLogs.MakeLogEntry(");
			StringAssert.DoesNotMatch(partial,new Regex(@"FormClosed\s*\+="));
			Assert.AreEqual(1,Regex.Matches(partial,@"AlertItems\.Insert\(").Count);
			Assert.AreEqual(1,Regex.Matches(partial,@"SecurityLogs\.MakeLogEntry\(").Count);
			StringAssert.Contains(partial,"ProcNotes.GetNewestForProc(_procedure.ProcNum)");
			StringAssert.Contains(partial,"ProcNoteMerges.IsMergeNeeded(_procNoteNewestAtLoad,procNoteNow,isNoteRowWritten)");
			StringAssert.Contains(partial,"bool isNoteRowWritten=ProcNoteMerges.IsNoteRowWritten(_procedure,_procedureOld,textNotes.Text,_signatureChanged);");
			StringAssert.DoesNotMatch(partial,new Regex(@"isNoteRowWritten=ProcNoteMerges\.IsNoteTextChanged"));
			StringAssert.Contains(partial,"ProcNoteMerges.CanEditProcNote(Security.IsAuthorized(EnumPermType.ProcedureNoteFull,true),");
			StringAssert.Contains(partial,"bool canChangeText=ProcNoteMerges.CanChangeText(!textNotes.ReadOnly,canEditTheirs);");
			StringAssert.Contains(partial,",isSigDrawn,canChangeText);");
			StringAssert.Contains(partial,"if(choice==ProcNoteMergeChoice.Cancel || !ProcNoteMerges.IsChoiceAllowed(choice,canChangeText)) {");
			StringAssert.Contains(partial,"ProcNoteMerges.ApplyTheirNote(_procedure,_procedureOld,procNoteTheirs,textNotes.Text);");
			StringAssert.Contains(partial,"NoteMerge_LockNote();");
			StringAssert.Contains(partial,"new FrmProcNoteMerge(");
			StringAssert.Contains(partial,"ProcNoteMerges.GetLogPermission(ProcNoteMerges.IsCompleteStatus(_procedure.ProcStatus))");
			Project(@"OpenPmsDental\OpenPmsDental.csproj").AssertCompiles(@"Forms\FormProcEdit.NoteMerge.cs");
		}

		[TestMethod]
		public void MergeWindow_ThreePanesAndFourButtons() {
			string xaml=ScratchDb.ReadRepoFile("WpfControlsOD","Frms","FrmProcNoteMerge.xaml");
			StringAssert.Contains(xaml,"Text=\"Procedure Note Merge\"");
			foreach(string name in new[] { "textTheirs","textMine","textMerged","butKeepMine","butKeepTheirs","butSaveMerged","butCancel" }) {
				StringAssert.Contains(xaml,"x:Name=\""+name+"\"",name);
			}
			StringAssert.Contains(Between(xaml,"x:Name=\"textTheirs\"","/>"),"ReadOnly=\"True\"");
			StringAssert.Contains(Between(xaml,"x:Name=\"textMine\"","/>"),"ReadOnly=\"True\"");
			StringAssert.DoesNotMatch(Between(xaml,"x:Name=\"textMerged\"","/>"),new Regex("ReadOnly"),"The merged note is edited.");
			string code=ScratchDb.ReadRepoFile("WpfControlsOD","Frms","FrmProcNoteMerge.xaml.cs");
			StringAssert.Contains(code,"MergeChoice=ProcNoteMergeChoice.KeepMine;");
			StringAssert.Contains(code,"MergeChoice=ProcNoteMergeChoice.KeepTheirs;");
			StringAssert.Contains(code,"MergeChoice=ProcNoteMergeChoice.SaveMerged;");
			StringAssert.Contains(code,"MergeChoice=ProcNoteMergeChoice.Cancel;");
			//Only Keep Theirs and Cancel when the text may not be changed (ProcNoteMerges.CanChangeText).
			string load=Between(code,"if(!_canChangeText) {","textMerged.Focus();");
			StringAssert.Contains(load,"textMerged.ReadOnly=true;");
			StringAssert.Contains(load,"butKeepMine.IsEnabled=false;");
			StringAssert.Contains(load,"butSaveMerged.IsEnabled=false;");
			StringAssert.Contains(Between(code,"private void butKeepMine_Click(","IsDialogOK=true;"),"if(!_canChangeText) {");
			StringAssert.Contains(Between(code,"private void butSaveMerged_Click(","IsDialogOK=true;"),"if(!_canChangeText) {");
			ProjectFile projectFile=Project(@"WpfControlsOD\WpfControlsOD.csproj");
			projectFile.AssertCompiles(@"Frms\FrmProcNoteMerge.xaml.cs");
			projectFile.AssertPages(@"Frms\FrmProcNoteMerge.xaml");
		}

		[TestMethod]
		public void BusinessLayer_HooksInSharedFiles() {
			string procedures=ScratchDb.ReadRepoFile("OpenPmsDentalBusiness","Data Interface","Procedures.cs");
			//One rule for writing a note row, shared with the merge check (ProcNotes.IsNoteRowNeeded).
			StringAssert.Contains(procedures,"if(ProcNotes.IsNoteRowNeeded(procedure,oldProcedure)) {");
			StringAssert.DoesNotMatch(procedures,new Regex(@"if\(procedure\.Note!=oldProcedure\.Note"));
			int idxSet=procedures.IndexOf("ProcNotes.SetSecondSignature(note,procedure,oldProcedure);");
			int idxInsert=procedures.IndexOf("ProcNotes.Insert(note);",idxSet);
			Assert.IsTrue(idxSet>0 && idxInsert>idxSet,"The second signature is set before the row is inserted.");
			string chart=ScratchDb.ReadRepoFile("OpenPmsDentalBusiness","Db Multi Table","ChartModules.cs");
			StringAssert.Contains(chart,",UserNum2,CASE WHEN Signature2!='' THEN 1 ELSE 0 END AS Sig2Present ");
			StringAssert.Contains(chart,"GroupNoteSignatures.FillProgNotesSigned(row,noteRowCur,userName,dictUserNames);");
			StringAssert.Contains(chart,"GroupNoteSignatures.GetAuditSecondSignatureText(rowCur,dictUserNames);");
			Project(@"OpenPmsDentalBusiness\OpenPmsDentalBusiness.csproj").AssertCompiles(@"Data Interface\GroupNoteSignatures.cs",
				@"Data Interface\ProcNoteMerges.cs",@"Misc\LocalWindowSizes.cs");
		}
	}
}
