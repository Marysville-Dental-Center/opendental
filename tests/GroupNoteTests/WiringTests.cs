// The Chart module and the Group Note window can't be opened in a test run (the GUI is not launched), so these tests read their sources and check
// that they use the shared group note rules, which the other files test.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GroupNoteTests {
	[TestClass]
	public class WiringTests {
		private static string Read(params string[] arrayParts) {
			string path=ScratchDb.FindRepoFile(Path.Combine(arrayParts));
			return File.ReadAllText(path);
		}

		private static string ControlChart() {
			return Read("OpenDental","Main Modules","ControlChart.cs");
		}

		private static string FormProcGroup() {
			return Read("OpenDental","Forms","FormProcGroup.cs");
		}

		[TestMethod]
		public void Chart_No243GroupingChecks() {
			string source=ControlChart();
			StringAssert.DoesNotMatch(source,new System.Text.RegularExpressions.Regex("Procedures must be complete to attach a group note"));
			StringAssert.DoesNotMatch(source,new System.Text.RegularExpressions.Regex("must have the same (date|clinic|provider) to attach a group note"));
			StringAssert.Contains(source,"GroupNotes.GetAttachBlockReason(procedure)");
		}

		[TestMethod]
		public void Chart_NewGroupNoteDefaultsAndEmptySelection() {
			string source=ControlChart();
			StringAssert.Contains(source,"GroupNotes.GetDefaultProcDate(listProcedures,DateTime.Today)");
			StringAssert.Contains(source,"GroupNotes.GetDefaultProvNum(listProcedures,Security.CurUser.ProvNum,PrefC.GetLong(PrefName.PracticeDefaultProv))");
			StringAssert.Contains(source,"GroupNotes.GetDefaultClinicNum(listProcedures,Clinics.ClinicNum,PrefC.HasClinicsEnabled)");
			StringAssert.Contains(source,"menuItemGroupSelected.Visible=!IsPatientNull()");
		}

		[TestMethod]
		public void Chart_ToothFilterUsesTheRule() {
			StringAssert.Contains(ControlChart(),"GroupNotes.IsVisibleWithToothFilter(procNum,listProcGroupItems,listProcNums)");
		}

		[TestMethod]
		public void ProgressNotes_PlaceGroupNotes() {
			string source=Read("OpenDentBusiness","Db Multi Table","ChartModules.cs");
			StringAssert.Contains(source,"GroupNotes.PlaceAfterAttachedProcs(rows,ProcGroupItems.GetPatientData(patNum))");
		}

		[TestMethod]
		public void Window_AddRemoveDateProvider() {
			string source=FormProcGroup();
			StringAssert.Contains(source,"private void butAdd_Click(");
			StringAssert.Contains(source,"private void butRemove_Click(");
			StringAssert.Contains(source,"comboProv.GetSelectedProvNum()");
			StringAssert.Contains(source,"textProcDate.ReadOnly=!_canEditStructure");
			StringAssert.Contains(source,"GroupNotes.GetStructurePermission(_listProcStatsAtLoad)");
			string designer=Read("OpenDental","Forms","FormProcGroup.designer.cs");
			StringAssert.Contains(designer,"this.butAdd.Click += new System.EventHandler(this.butAdd_Click);");
			StringAssert.Contains(designer,"this.butRemove.Click += new System.EventHandler(this.butRemove_Click);");
			StringAssert.Contains(designer,"this.textProcDate.TextChanged += new System.EventHandler(this.textProcDate_TextChanged);");
			StringAssert.Contains(designer,"GridSelectionMode.MultiExtended");
		}

		[TestMethod]
		public void Window_SignatureKeyUnchanged_AndLinksSavedBeforeSigning() {
			string source=FormProcGroup();
			StringAssert.Contains(source,"return GroupNotes.GetSignatureKey(ProcedureGroup,ListProcGroupItems);");
			int idxSync=source.IndexOf("ProcGroupItems.SyncForGroup(");
			int idxSign=source.IndexOf("SaveSignature();",idxSync);
			Assert.IsTrue(idxSync>0 && idxSign>idxSync,"Links are saved before the signature, which reads them from the database.");
			StringAssert.Contains(source,"ClearLoadedSignature();");
			//A-SIG wiring stays.
			StringAssert.Contains(source,".IsDomainAuthenticated");
			StringAssert.Contains(source,"DomainLogin.GetSwitchLogText(");
		}

		[TestMethod]
		public void Window_ChecksPermissionsForWhatIsSaved() {
			string source=FormProcGroup();
			//The checks come from the shared rule (RulesTests, GroupNoteDbTests) and run against the logged-in user.
			StringAssert.Contains(source,"GroupNotes.GetSaveChecks(_listProcStatsAtLoad,listProcStatsNow,");
			StringAssert.Contains(source,"GroupNotes.IsSaveAuthorized(listGroupNotePermChecks,(permType,dateLimit) => Security.IsAuthorized(permType,dateLimit))");
			//Saving (and locking, which saves) checks them for the procedures attached when saving.
			int idxValid=source.IndexOf("private bool EntriesAreValid()");
			int idxValidEnd=source.IndexOf("private void SaveAndClose()",idxValid);
			string entriesAreValid=source.Substring(idxValid,idxValidEnd-idxValid);
			StringAssert.Contains(entriesAreValid,"IsAuthorizedForChanges(ListProcedures,GetProcDateEntered(),IsNoteOrSignatureChanged())");
			StringAssert.DoesNotMatch(entriesAreValid,new System.Text.RegularExpressions.Regex(@"_permissionNote"),
				"The date is checked with the permission for the group note as saved, not the note permission from when the window opened.");
			//Add and Remove refuse a change the user could not save.
			foreach(string method in new[] { "private void butAdd_Click(","private void butRemove_Click(" }) {
				int idx=source.IndexOf(method);
				int idxEnd=source.IndexOf("\n\t\t}",idx);
				StringAssert.Contains(source.Substring(idx,idxEnd-idx),"IsAuthorizedForChanges(listProceduresAfter,GetProcDateEntered(),isNoteChanged:false)",method);
			}
		}

		[TestMethod]
		public void Window_LogsChangesAsProcEdit() {
			StringAssert.Contains(FormProcGroup(),"SecurityLogs.MakeLogEntry(EnumPermType.ProcEdit,ProcedureGroup.PatNum,logText,ProcedureGroup.ProcNum,");
		}
	}
}
