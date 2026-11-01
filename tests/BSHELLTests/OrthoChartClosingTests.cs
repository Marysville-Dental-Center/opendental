// v25_3-chart-orthochart-nonmodal: how each close of the non-modal Ortho Chart window is handled (OrthoChartWindowRules.GetClosingPath).
// A window shown with Show() keeps DialogResult None when it is closed with Esc, Alt+F4, the window menu or the taskbar, so DialogResult alone can
// no longer tell a user close (asks about unsaved edits) from a program close (does not ask).
using System;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace BSHELLTests {
	[TestClass]
	public class OrthoChartClosingTests {
		///<summary>The window's close kind when the program set none: a user close.</summary>
		private const OrthoChartCloseKind DEFAULT_KIND=OrthoChartCloseKind.UserCancel;

		private static string Form() {
			return TestPaths.ReadRepoFile("OpenDental","Forms","FormOrthoChart.cs");
		}

		private static string Closing() {
			return TestPaths.GetMethodBody(Form(),"private void FormOrthoChart_FormClosing(object sender,FormClosingEventArgs e)");
		}

		///<summary>What closing a window that holds the lock does, for a path and the window's close kind.</summary>
		private static OrthoChartCloseActions ActionsFor(OrthoChartClosingPath closingPath,OrthoChartCloseKind closeKindSet) {
			switch(closingPath) {
				case OrthoChartClosingPath.Program:
					return OrthoChartWindowRules.GetCloseActions(OrthoChartWindowRules.GetProgramCloseKind(closeKindSet),isLockedByMe:true,isReadOnly:false);
				case OrthoChartClosingPath.AskThenDiscard://OK on "Unsaved changes will be lost": unlock and delete today's empty row, nothing saved.
					return OrthoChartWindowRules.GetCloseActions(OrthoChartCloseKind.UserCancel,isLockedByMe:true,isReadOnly:false);
				default://DeferToOwner, AlreadySaved: nothing here.
					return new OrthoChartCloseActions();
			}
		}

		[TestMethod]
		public void Esc_OrAltF4_OnTheNonModalWindow_AsksAndDoesNotSave() {
			//Esc: FormODBase.ProcessCmdKey calls Close(). Alt+F4, the window menu and the taskbar: SC_CLOSE. Both arrive as UserClosing with DialogResult None.
			OrthoChartClosingPath closingPath=OrthoChartWindowRules.GetClosingPath(isModal:false,DialogResult.None,CloseReason.UserClosing,DEFAULT_KIND);
			Assert.AreEqual(OrthoChartClosingPath.AskThenDiscard,closingPath,"Before the fix this was the 'closing automatically' branch, which saved.");
			OrthoChartCloseActions closeActions=ActionsFor(closingPath,DEFAULT_KIND);
			Assert.IsFalse(closeActions.DoSave);
			Assert.IsTrue(closeActions.DoReleaseLock && closeActions.DoDeleteEmptyRows);
		}

		[TestMethod]
		public void DrawnX_AndTheModalWindow_AskAsBefore() {
			Assert.AreEqual(OrthoChartClosingPath.AskThenDiscard,
				OrthoChartWindowRules.GetClosingPath(isModal:false,DialogResult.Cancel,CloseReason.UserClosing,DEFAULT_KIND),"[X]: OnCloseXClicked sets Cancel.");
			Assert.AreEqual(OrthoChartClosingPath.AskThenDiscard,
				OrthoChartWindowRules.GetClosingPath(isModal:true,DialogResult.Cancel,CloseReason.UserClosing,DEFAULT_KIND),"Modal: WinForms sets Cancel.");
		}

		[TestMethod]
		public void Save_IsNotHandledAgain() {
			Assert.AreEqual(OrthoChartClosingPath.AlreadySaved,
				OrthoChartWindowRules.GetClosingPath(isModal:false,DialogResult.OK,CloseReason.UserClosing,DEFAULT_KIND));
			Assert.AreEqual(OrthoChartClosingPath.AlreadySaved,
				OrthoChartWindowRules.GetClosingPath(isModal:true,DialogResult.OK,CloseReason.UserClosing,DEFAULT_KIND));
		}

		[TestMethod]
		public void CancelledPrompt_ThenSaveAtLogOff_Saves() {
			//1. [X] with edits: the prompt shows, the user clicks Cancel. FormClosing puts DialogResult back to None (pinned below).
			Assert.AreEqual(OrthoChartClosingPath.AskThenDiscard,
				OrthoChartWindowRules.GetClosingPath(isModal:false,DialogResult.Cancel,CloseReason.UserClosing,DEFAULT_KIND));
			//2. Log off: the user picks Save, PromptCloseForLogOff sets PromptSave and calls Close().
			OrthoChartClosingPath closingPath=OrthoChartWindowRules.GetClosingPath(isModal:false,DialogResult.None,CloseReason.UserClosing,
				OrthoChartCloseKind.PromptSave);
			Assert.AreEqual(OrthoChartClosingPath.Program,closingPath,"No second 'Unsaved changes will be lost' prompt.");
			Assert.AreEqual(OrthoChartCloseKind.PromptSave,OrthoChartWindowRules.GetProgramCloseKind(OrthoChartCloseKind.PromptSave));
			Assert.IsTrue(ActionsFor(closingPath,OrthoChartCloseKind.PromptSave).DoSave,"The user chose Save.");
			//Even with a Cancel left over from step 1, the kind the program set wins.
			Assert.AreEqual(OrthoChartClosingPath.Program,
				OrthoChartWindowRules.GetClosingPath(isModal:false,DialogResult.Cancel,CloseReason.UserClosing,OrthoChartCloseKind.PromptSave));
			//Don't Save discards without asking again.
			OrthoChartClosingPath closingPathDiscard=OrthoChartWindowRules.GetClosingPath(isModal:false,DialogResult.None,CloseReason.UserClosing,
				OrthoChartCloseKind.PromptDiscard);
			Assert.AreEqual(OrthoChartClosingPath.Program,closingPathDiscard);
			Assert.IsFalse(ActionsFor(closingPathDiscard,OrthoChartCloseKind.PromptDiscard).DoSave);
		}

		[TestMethod]
		public void ForcedClose_AfterACancelledPrompt_DoesNotAsk() {
			foreach(DialogResult dialogResult in new[] { DialogResult.None,DialogResult.Cancel }) {
				OrthoChartClosingPath closingPath=OrthoChartWindowRules.GetClosingPath(isModal:false,dialogResult,CloseReason.UserClosing,
					OrthoChartCloseKind.Forced);//MarkAllForForcedClose, then CloseOpenForms' thread calls Close().
				Assert.AreEqual(OrthoChartClosingPath.Program,closingPath,dialogResult.ToString());
				Assert.IsTrue(ActionsFor(closingPath,OrthoChartCloseKind.Forced).DoSave,"A forced close keeps the work, as before 25.3.");
			}
		}

		[TestMethod]
		public void PatientGone_ClosesWithoutAsking_AndSavesNothing() {
			OrthoChartClosingPath closingPath=OrthoChartWindowRules.GetClosingPath(isModal:false,DialogResult.None,CloseReason.UserClosing,
				OrthoChartCloseKind.PatientGone);
			Assert.AreEqual(OrthoChartClosingPath.Program,closingPath);
			Assert.IsFalse(ActionsFor(closingPath,OrthoChartCloseKind.PatientGone).DoSave);
		}

		[TestMethod]
		public void MainWindowClosing_DefersToItsCloseOpenForms() {
			//WinForms raises FormClosing(FormOwnerClosing) on owned windows before the main window's own FormClosing. Doing anything then would save
			//and unlock before the main window asks, even when the user then cancels the program close.
			foreach(OrthoChartCloseKind closeKind in Enum.GetValues(typeof(OrthoChartCloseKind))) {
				foreach(DialogResult dialogResult in new[] { DialogResult.None,DialogResult.Cancel }) {
					OrthoChartClosingPath closingPath=OrthoChartWindowRules.GetClosingPath(isModal:false,dialogResult,CloseReason.FormOwnerClosing,closeKind);
					Assert.AreEqual(OrthoChartClosingPath.DeferToOwner,closingPath,closeKind+" "+dialogResult);
					OrthoChartCloseActions closeActions=ActionsFor(closingPath,closeKind);
					Assert.IsFalse(closeActions.DoSave || closeActions.DoReleaseLock || closeActions.DoDeleteEmptyRows);
				}
			}
			//A modal window can't be left open behind its owner's prompt: as before 25.3, it closes as forced.
			Assert.AreEqual(OrthoChartClosingPath.Program,
				OrthoChartWindowRules.GetClosingPath(isModal:true,DialogResult.None,CloseReason.FormOwnerClosing,DEFAULT_KIND));
		}

		[TestMethod]
		public void WindowsShutdown_ApplicationExit_OutsideCloseMessage_AreForced() {
			foreach(CloseReason closeReason in new[] { CloseReason.WindowsShutDown,CloseReason.ApplicationExitCall,CloseReason.TaskManagerClosing }) {
				foreach(bool isModal in new[] { false,true }) {
					foreach(DialogResult dialogResult in new[] { DialogResult.None,DialogResult.Cancel }) {
						OrthoChartClosingPath closingPath=OrthoChartWindowRules.GetClosingPath(isModal,dialogResult,closeReason,DEFAULT_KIND);
						Assert.AreEqual(OrthoChartClosingPath.Program,closingPath,closeReason+" modal="+isModal+" "+dialogResult);
					}
				}
				Assert.AreEqual(OrthoChartCloseKind.Forced,OrthoChartWindowRules.GetProgramCloseKind(DEFAULT_KIND));
			}
		}

		[TestMethod]
		public void OnlyAUserClose_EverAsks() {
			foreach(OrthoChartCloseKind closeKind in Enum.GetValues(typeof(OrthoChartCloseKind))) {
				foreach(CloseReason closeReason in Enum.GetValues(typeof(CloseReason))) {
					foreach(bool isModal in new[] { false,true }) {
						foreach(DialogResult dialogResult in new[] { DialogResult.None,DialogResult.Cancel,DialogResult.OK }) {
							OrthoChartClosingPath closingPath=OrthoChartWindowRules.GetClosingPath(isModal,dialogResult,closeReason,closeKind);
							if(closingPath!=OrthoChartClosingPath.AskThenDiscard) {
								continue;
							}
							string context=closeKind+" "+closeReason+" modal="+isModal+" "+dialogResult;
							Assert.IsFalse(OrthoChartWindowRules.IsProgramCloseKind(closeKind),context);
							Assert.IsTrue(closeReason==CloseReason.UserClosing || closeReason==CloseReason.None || closeReason==CloseReason.MdiFormClosing,context);
							Assert.AreNotEqual(DialogResult.OK,dialogResult,context);
						}
					}
				}
			}
			Assert.IsFalse(OrthoChartWindowRules.IsProgramCloseKind(OrthoChartCloseKind.UserCancel),"The default is a user close.");
			Assert.IsFalse(OrthoChartWindowRules.IsProgramCloseKind(OrthoChartCloseKind.Save));
		}

		[TestMethod]
		public void Window_DefaultsToAUserClose_AndOnlyTheForcedHookSetsForced() {
			string source=Form();
			StringAssert.Contains(source,"private OrthoChartCloseKind _orthoChartCloseKind=OrthoChartCloseKind.UserCancel;");
			Assert.AreEqual(1,TestPaths.Count(source,"_orthoChartCloseKind=OrthoChartCloseKind.Forced"),"Forced only by MarkAllForForcedClose.");
			string mark=TestPaths.GetMethodBody(source,"public static void MarkAllForForcedClose()");
			StringAssert.Contains(mark,"formOrthoChart._orthoChartCloseKind=OrthoChartCloseKind.Forced");
			StringAssert.Contains(mark,"InvokeIfRequired(","Set on the window's thread; CloseOpenForms(true) can run on a background thread.");
		}

		[TestMethod]
		public void FormClosing_UsesTheRules_AndDefersBeforeTouchingAnything() {
			string closing=Closing();
			int indexPath=closing.IndexOf("OrthoChartWindowRules.GetClosingPath(Modal,DialogResult,e.CloseReason,_orthoChartCloseKind);",StringComparison.Ordinal);
			int indexDefer=closing.IndexOf("if(orthoChartClosingPath==OrthoChartClosingPath.DeferToOwner) {",StringComparison.Ordinal);
			Assert.IsTrue(indexPath>0 && indexDefer>indexPath);
			string beforeDefer=closing.Substring(0,closing.IndexOf("return;",indexDefer,StringComparison.Ordinal));
			foreach(string action in new[] { "SaveToDb()","SetUserNumOrthoLocked","DeleteRowsIfEmpty()","SetTabletState","MsgBox.Show" }) {
				Assert.IsFalse(beforeDefer.Contains(action),action+" before the owner-closing return.");
			}
			StringAssert.Contains(closing,"if(orthoChartClosingPath==OrthoChartClosingPath.AskThenDiscard) {");
			StringAssert.Contains(closing,"if(orthoChartClosingPath!=OrthoChartClosingPath.Program){");
			Assert.IsFalse(closing.Contains("if(DialogResult==DialogResult.Cancel)"),"DialogResult alone no longer picks the branch.");
			Assert.IsFalse(closing.Contains("if(DialogResult!=DialogResult.None)"));
		}

		[TestMethod]
		public void CancelledPrompt_PutsDialogResultBack() {
			string closing=Closing();
			int indexPrompt=closing.IndexOf("if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,\"Unsaved changes will be lost.\")) {",StringComparison.Ordinal);
			Assert.IsTrue(indexPrompt>0);
			int indexReset=closing.IndexOf("DialogResult=DialogResult.None;",indexPrompt,StringComparison.Ordinal);
			int indexCancel=closing.IndexOf("e.Cancel=true;",indexPrompt,StringComparison.Ordinal);
			Assert.IsTrue(indexReset>0 && indexReset<indexCancel,"A window shown with Show() keeps DialogResult; a later close must not see the old Cancel.");
		}

		[TestMethod]
		public void LogOffPrompt_ReportsAWindowThatStayedOpen() {
			string prompt=TestPaths.GetMethodBody(Form(),"private bool PromptCloseForLogOff()");
			int indexClose=prompt.IndexOf("Close();",StringComparison.Ordinal);
			int indexStillOpen=prompt.IndexOf("if(!IsDisposedOrClosed(this)) {",StringComparison.Ordinal);
			Assert.IsTrue(indexClose>0 && indexStillOpen>indexClose);
			string stillOpen=TestPaths.GetMethodBody(prompt.Substring(indexStillOpen),"if(!IsDisposedOrClosed(this)) {");
			StringAssert.Contains(stillOpen,"_orthoChartCloseKind=OrthoChartCloseKind.UserCancel;","Never left marked for a later user close.");
			StringAssert.Contains(stillOpen,"return false;");
			Assert.AreEqual(1,TestPaths.Count(prompt,"Close();"),"Every window is closed here, with or without edits, so none keeps a prompt kind.");
			string promptAll=TestPaths.GetMethodBody(Form(),"public static bool PromptCloseAllForLogOff()");
			StringAssert.Contains(promptAll,"if(!canClose) {");
		}

		[TestMethod]
		public void ForcedCloseOpenForms_MarksTheWindows() {
			string main=TestPaths.ReadRepoFile("OpenDental","Main Modules","FormOpenDental.cs");
			string closeOpen=TestPaths.GetMethodBody(main,"public bool CloseOpenForms(bool isForceClose");
			int indexMark=closeOpen.IndexOf("if(isForceClose && !isSnip) {",StringComparison.Ordinal);
			int indexCall=closeOpen.IndexOf("FormOrthoChart.MarkAllForForcedClose();",StringComparison.Ordinal);
			int indexGetForms=closeOpen.IndexOf("List<Form> listFormsToClose=GetOpenForms();",StringComparison.Ordinal);
			Assert.IsTrue(indexMark>0 && indexCall>indexMark && indexCall<indexGetForms,"Marked before any window is closed.");
		}

		[TestMethod]
		public void MarkAllForForcedClose_NoWindows_DoesNothing() {
			FormOrthoChart.MarkAllForForcedClose();
			Assert.IsNull(FormOrthoChart.GetOpenForPatient(12345));
		}
	}
}
