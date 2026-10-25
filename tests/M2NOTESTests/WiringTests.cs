// The Group Note window, Procedure Info and the merge window can't be opened in a test run (the GUI is not launched), so these tests read their
// sources and check that they use the rules the other files test, and that the project files include the new files.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2NOTESTests {
	[TestClass]
	public class WiringTests {
		private static string FormProcGroup() {
			return ScratchDb.ReadRepoFile("OpenDental","Forms","FormProcGroup.cs");
		}

		private static string FormProcGroupDesigner() {
			return ScratchDb.ReadRepoFile("OpenDental","Forms","FormProcGroup.designer.cs");
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
			StringAssert.Contains(changeUser2,"new FormLogOn(isSimpleSwitch:true)");
			StringAssert.Contains(changeUser2,"formLogOn.IsDomainAuthenticated");
			StringAssert.Contains(changeUser2,"DomainLogin.GetSwitchLogText(");
			StringAssert.Contains(changeUser2,"Userods.CanUserSignNote(_userod2)");
			//Saving: auto note block, invalid signature, a different second signer, the cleared alert and the log.
			string checks=Between(source,"private bool AreSecondSignatureEntriesValid(","private void SaveAndClose()");
			StringAssert.Contains(checks,"PrefName.ProcNoteSigsBlockedAutoNoteIncomplete");
			StringAssert.Contains(checks,"signatureBoxWrapper2.IsValid");
			StringAssert.Contains(checks,"GroupNoteSignatures.GetSignersError(");
			StringAssert.Contains(source,"GroupNoteSignatures.GetSecondSignerToAlert(_procedureGroupOld,_hasSig2Changed,listUserNumsActing)");
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
		}

		[TestMethod]
		public void ProcedureInfo_TwoHooksOnly() {
			string source=ScratchDb.ReadRepoFile("OpenDental","Forms","FormProcEdit.cs");
			Assert.AreEqual(1,Regex.Matches(source,@"NoteMerge_Load\(\);").Count);
			Assert.AreEqual(1,Regex.Matches(source,@"if\(!NoteMerge_TryResolve\(\)\) \{ return; \}").Count);
			int idxSaveAndClose=source.IndexOf("private void SaveAndClose() {");
			int idxHook=source.IndexOf("NoteMerge_TryResolve()");
			int idxFirstWrite=source.IndexOf("Procedures.UpdateProcedureFields(",idxSaveAndClose);
			Assert.IsTrue(idxSaveAndClose>0 && idxHook>idxSaveAndClose && idxHook<idxFirstWrite,"The merge is resolved before anything is saved.");
			string partial=ScratchDb.ReadRepoFile("OpenDental","Forms","FormProcEdit.NoteMerge.cs");
			StringAssert.Contains(partial,"ProcNotes.GetNewestForProc(_procedure.ProcNum)");
			StringAssert.Contains(partial,"ProcNoteMerges.IsMergeNeeded(_procNoteNewestAtLoad,procNoteNow,isNoteRowWritten)");
			StringAssert.Contains(partial,"new FrmProcNoteMerge(");
			StringAssert.Contains(partial,"ProcNoteMerges.GetLogPermission(ProcNoteMerges.IsCompleteStatus(_procedure.ProcStatus))");
			StringAssert.Contains(ScratchDb.ReadRepoFile("OpenDental","OpenDental.csproj"),"<Compile Include=\"Forms\\FormProcEdit.NoteMerge.cs\">");
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
			string csproj=ScratchDb.ReadRepoFile("WpfControlsOD","WpfControlsOD.csproj");
			StringAssert.Contains(csproj,"<Compile Include=\"Frms\\FrmProcNoteMerge.xaml.cs\">");
			StringAssert.Contains(csproj,"<Page Include=\"Frms\\FrmProcNoteMerge.xaml\">");
		}

		[TestMethod]
		public void BusinessLayer_HooksInSharedFiles() {
			string procedures=ScratchDb.ReadRepoFile("OpenDentBusiness","Data Interface","Procedures.cs");
			StringAssert.Contains(procedures,"|| ProcNotes.IsSecondSignatureChanged(procedure,oldProcedure)//26.1 group note second signature (M2-NOTES)");
			int idxSet=procedures.IndexOf("ProcNotes.SetSecondSignature(note,procedure,oldProcedure);");
			int idxInsert=procedures.IndexOf("ProcNotes.Insert(note);",idxSet);
			Assert.IsTrue(idxSet>0 && idxInsert>idxSet,"The second signature is set before the row is inserted.");
			string chart=ScratchDb.ReadRepoFile("OpenDentBusiness","Db Multi Table","ChartModules.cs");
			StringAssert.Contains(chart,",UserNum2,CASE WHEN Signature2!='' THEN 1 ELSE 0 END AS Sig2Present ");
			StringAssert.Contains(chart,"GroupNoteSignatures.FillProgNotesSigned(row,noteRowCur,userName,dictUserNames);");
			StringAssert.Contains(chart,"GroupNoteSignatures.GetAuditSecondSignatureText(rowCur,dictUserNames);");
			string csproj=ScratchDb.ReadRepoFile("OpenDentBusiness","OpenDentBusiness.csproj");
			foreach(string file in new[] { "Data Interface\\GroupNoteSignatures.cs","Data Interface\\ProcNoteMerges.cs","Misc\\LocalWindowSizes.cs" }) {
				StringAssert.Contains(csproj,"<Compile Include=\""+file+"\" />",file);
			}
		}
	}
}
