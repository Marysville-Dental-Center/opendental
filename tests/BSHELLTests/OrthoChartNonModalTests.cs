// v25_3-chart-orthochart-nonmodal: the Ortho Chart stays open while the user works elsewhere.
using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental;
using OpenDentBusiness;

namespace BSHELLTests {
	[TestClass]
	public class OrthoChartNonModalTests {
		private static string Form() {
			return TestPaths.ReadRepoFile("OpenDental","Forms","FormOrthoChart.cs");
		}

		[TestMethod]
		public void CloseActions_ReleaseTheLockForEveryCloseKind_WhenHeld() {
			foreach(OrthoChartCloseKind closeKind in Enum.GetValues(typeof(OrthoChartCloseKind))) {
				OrthoChartCloseActions closeActions=OrthoChartWindowRules.GetCloseActions(closeKind,isLockedByMe:true,isReadOnly:false);
				Assert.IsTrue(closeActions.DoReleaseLock,closeKind.ToString());
			}
		}

		[TestMethod]
		public void CloseActions_NeverTouchAnotherUsersLockOrData() {
			foreach(OrthoChartCloseKind closeKind in Enum.GetValues(typeof(OrthoChartCloseKind))) {
				foreach(bool isReadOnly in new[] { true,false }) {
					OrthoChartCloseActions closeActions=OrthoChartWindowRules.GetCloseActions(closeKind,isLockedByMe:false,isReadOnly:isReadOnly);
					Assert.IsFalse(closeActions.DoSave || closeActions.DoReleaseLock || closeActions.DoDeleteEmptyRows,closeKind+" without the lock");
				}
				OrthoChartCloseActions closeActionsReadOnly=OrthoChartWindowRules.GetCloseActions(closeKind,isLockedByMe:true,isReadOnly:true);
				Assert.IsFalse(closeActionsReadOnly.DoSave || closeActionsReadOnly.DoReleaseLock,closeKind+" read-only");
			}
		}

		[TestMethod]
		public void CloseActions_SaveOrDiscard() {
			Assert.IsTrue(OrthoChartWindowRules.GetCloseActions(OrthoChartCloseKind.Save,true,false).DoSave);
			Assert.IsTrue(OrthoChartWindowRules.GetCloseActions(OrthoChartCloseKind.PromptSave,true,false).DoSave);
			Assert.IsTrue(OrthoChartWindowRules.GetCloseActions(OrthoChartCloseKind.Forced,true,false).DoSave,"An unattended close keeps the work, as before.");
			foreach(OrthoChartCloseKind closeKind in new[] { OrthoChartCloseKind.UserCancel,OrthoChartCloseKind.PromptDiscard,OrthoChartCloseKind.PatientGone }) {
				OrthoChartCloseActions closeActions=OrthoChartWindowRules.GetCloseActions(closeKind,true,false);
				Assert.IsFalse(closeActions.DoSave,closeKind.ToString());
				Assert.IsTrue(closeActions.DoDeleteEmptyRows,closeKind.ToString());
			}
		}

		[TestMethod]
		public void LogOffPrompt_OnlyForTheLockHolderWithEdits() {
			Assert.IsTrue(OrthoChartWindowRules.NeedsClosePrompt(isLockedByMe:true,isReadOnly:false,hasUnsavedChanges:true));
			Assert.IsFalse(OrthoChartWindowRules.NeedsClosePrompt(isLockedByMe:true,isReadOnly:false,hasUnsavedChanges:false));
			Assert.IsFalse(OrthoChartWindowRules.NeedsClosePrompt(isLockedByMe:false,isReadOnly:false,hasUnsavedChanges:true));
			Assert.IsFalse(OrthoChartWindowRules.NeedsClosePrompt(isLockedByMe:true,isReadOnly:true,hasUnsavedChanges:true));
		}

		[TestMethod]
		public void PatientGone_MergedArchivedDeletedOrMissing() {
			Assert.IsTrue(OrthoChartWindowRules.IsPatientGone(PatientStatus.Patient,null),"Row gone.");
			Assert.IsTrue(OrthoChartWindowRules.IsPatientGone(PatientStatus.Patient,PatientStatus.Deleted));
			Assert.IsTrue(OrthoChartWindowRules.IsPatientGone(PatientStatus.Patient,PatientStatus.Archived),"A merge archives the patient merged from.");
			Assert.IsFalse(OrthoChartWindowRules.IsPatientGone(PatientStatus.Archived,PatientStatus.Archived),"Opened on an archived patient.");
			Assert.IsFalse(OrthoChartWindowRules.IsPatientGone(PatientStatus.Patient,PatientStatus.Patient));
			Assert.IsFalse(OrthoChartWindowRules.IsPatientGone(PatientStatus.Patient,PatientStatus.Inactive));
			Assert.IsNull(OrthoChartWindowRules.ToPatientStatus(-1));
			Assert.AreEqual(PatientStatus.Deleted,OrthoChartWindowRules.ToPatientStatus((int)PatientStatus.Deleted));
		}

		[TestMethod]
		public void PatStatusNoCache_ReadsTheDatabase() {
			ScratchDb.RequireDb();
			DataCore.NonQ("INSERT INTO patient (PatNum,LName,FName,PatStatus) VALUES (5,'Synthetic','Pat',"+(int)PatientStatus.Patient+"),"
				+"(6,'Synthetic','Merged',"+(int)PatientStatus.Archived+")");
			Assert.AreEqual((int)PatientStatus.Patient,OrthoChartWindowRules.GetPatStatusNoCache(5));
			Assert.AreEqual((int)PatientStatus.Archived,OrthoChartWindowRules.GetPatStatusNoCache(6));
			Assert.AreEqual(-1,OrthoChartWindowRules.GetPatStatusNoCache(99));
		}

		[TestMethod]
		public void NoPatient_OpensNothing_AndNothingIsOpen() {
			Assert.IsNull(FormOrthoChart.ShowNonModal(null,null,0,null));
			Assert.IsNull(FormOrthoChart.GetOpenForPatient(12345));
			Assert.IsTrue(FormOrthoChart.PromptCloseAllForLogOff(),"No windows: nothing stops a log off.");
		}

		[TestMethod]
		public void Modules_OpenItNonModal() {
			string chart=TestPaths.ReadRepoFile("OpenDental","Main Modules","ControlChart.cs");
			Assert.IsFalse(chart.Contains("new FormOrthoChart("),"Every Chart module opening goes through ShowNonModal.");
			Assert.AreEqual(3,TestPaths.Count(chart,"FormOrthoChart.ShowNonModal(FindForm(),Pd.Patient,"),"Toolbar, tab menu and ortho grid.");
			string saved=TestPaths.GetMethodBody(chart,"private void OrthoChartSaved(long patNum)");
			StringAssert.Contains(saved,"Visible && !IsPatientNull() && Pd.PatNum==patNum","Refreshes only when the Chart shows that patient.");
			string appt=TestPaths.ReadRepoFile("OpenDental","Main Modules","ControlAppt.cs");
			Assert.IsFalse(appt.Contains("new FormOrthoChart("));
			StringAssert.Contains(appt,"FormOrthoChart.ShowNonModal(FindForm(),_patient,0,null);");
		}

		[TestMethod]
		public void Window_OnePerPatient_TiedToItsPatient() {
			string source=Form();
			string show=TestPaths.GetMethodBody(source,"public static FormOrthoChart ShowNonModal(");
			StringAssert.Contains(show,"GetOpenForPatient(patient.PatNum)");
			StringAssert.Contains(show,"formOrthoChart.Show(formOwner);");
			Assert.IsFalse(show.Contains("ShowDialog"));
			StringAssert.Contains(source,"Text=_listOrthoChartTabs[0].TabName+\" - \"+_patient.GetNameFL();","The title names the patient.");
			Assert.IsFalse(source.Contains("FormOpenDental.PatNumCur") || source.Contains("PatNumCur"),"Never follows the main window's patient.");
		}

		[TestMethod]
		public void Save_ClosesTheNonModalWindow_AndTellsTheModule() {
			string save=TestPaths.GetMethodBody(Form(),"private void butSave_Click(object sender,EventArgs e)");
			Assert.AreEqual(2,TestPaths.Count(save,"CloseIfNonModal();"),"Both ways out of Save close the window.");
			StringAssert.Contains(save,"OrthoChartSaved?.Invoke(_patient.PatNum);");
			StringAssert.Contains(save,"PatientNotes.SetUserNumOrthoLocked(_patient.PatNum,0);//Unlock");
		}

		[TestMethod]
		public void ProgramClose_NeverOverwritesAnotherUsersLock() {
			string closing=TestPaths.GetMethodBody(Form(),"private void FormOrthoChart_FormClosing(object sender,FormClosingEventArgs e)");
			int indexNoLock=closing.IndexOf("if(!_isLockedByMe) {\r\n\t\t\t\t//this user does not have a lock, so nothing to save.\r\n\t\t\t\treturn;",StringComparison.Ordinal);
			if(indexNoLock<0) {
				indexNoLock=closing.IndexOf("if(!_isLockedByMe) {\n\t\t\t\t//this user does not have a lock, so nothing to save.\n\t\t\t\treturn;",StringComparison.Ordinal);
			}
			Assert.IsTrue(indexNoLock>0,"Before 25.3 this branch fell through to SaveToDb and the unlock.");
			int indexRules=closing.IndexOf("OrthoChartWindowRules.GetCloseActions(_orthoChartCloseKind,_isLockedByMe,_isReadOnly)",StringComparison.Ordinal);
			Assert.IsTrue(indexRules>indexNoLock);
		}

		[TestMethod]
		public void LogOff_PromptsBeforeClosingForms_AndPatientCheckRunsOnTheTimer() {
			string main=TestPaths.ReadRepoFile("OpenDental","Main Modules","FormOpenDental.cs");
			string closeOpen=TestPaths.GetMethodBody(main,"public bool CloseOpenForms(bool isForceClose");
			int indexPrompt=closeOpen.IndexOf("if(!isForceClose && !FormOrthoChart.PromptCloseAllForLogOff()) {",StringComparison.Ordinal);
			int indexGetForms=closeOpen.IndexOf("List<Form> listFormsToClose=GetOpenForms();",StringComparison.Ordinal);
			Assert.IsTrue(indexPrompt>0 && indexPrompt<indexGetForms,"Asked about unsaved edits before any window closes; Cancel stops the log off.");
			string timer=TestPaths.GetMethodBody(Form(),"private void timerLocked_Tick(object sender, EventArgs e)");
			Assert.IsTrue(Regex.IsMatch(timer,@"^\{\s*//Every 4 seconds\.\s*if\(CloseIfPatientGone\(\)\) \{"),"Checked first, also for read-only windows.");
			string gone=TestPaths.GetMethodBody(Form(),"private bool CloseIfPatientGone()");
			StringAssert.Contains(gone,"_orthoChartCloseKind=OrthoChartCloseKind.PatientGone;");
			StringAssert.Contains(gone,"MsgBox.Show(this,");
		}

		[TestMethod]
		public void SignatureWiring_FromASig_Kept() {
			string source=Form();
			StringAssert.Contains(source,"PatientNotes.SetUserNumOrthoLocked(_patient.PatNum,PatientNotes.ORTHO_LOCK_LOCAL_SWITCH)");
			StringAssert.Contains(source,"DomainLogin.GetSwitchLogText(");
		}
	}
}
