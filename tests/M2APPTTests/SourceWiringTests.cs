// Source checks for the M2-APPT UI wiring that cannot run without a window: the Appointments module's blockout menu, the blockout windows, the
// Make Recall search preference, and the move of the default provider for unassigned operatories from Edit Day to Preferences.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2APPTTests {
	[TestClass]
	public class SourceWiringTests {
		private static string ReadControlAppt() {
			return RepoFiles.Read("OpenDental","Main Modules","ControlAppt.cs");
		}

		#region Blockout permissions (25.4)
		[TestMethod]
		public void ControlAppt_EachBlockoutHandlerChecksItsOwnAction() {
			string source=ReadControlAppt();
			Dictionary<string,string> dictHandlerAction=new Dictionary<string,string> {
				{ "menuBlockAdd_Click","BlockoutAction.Create" },
				{ "menuBlockEdit_Click","BlockoutAction.Edit" },
				{ "menuBlockCopy_Click","BlockoutAction.Copy" },
				{ "menuBlockCut_Click","BlockoutAction.Cut" },
				{ "menuBlockPaste_Click","BlockoutAction.Paste" },
				{ "menuBlockDelete_Click","BlockoutAction.Delete" },
			};
			foreach(KeyValuePair<string,string> kvp in dictHandlerAction) {
				string method=RepoFiles.StripLineComments(RepoFiles.GetMethod(source,kvp.Key));
				StringAssert.Contains(method,kvp.Value,kvp.Key);
			}
			StringAssert.Contains(RepoFiles.GetMethod(source,"menuBlockCutCopyPaste_Click"),"CanOpenCutCopyPasteTool()");
			StringAssert.Contains(RepoFiles.GetMethod(source,"menuBlockPaste_Click"),"IsAuthorizedToReplaceBlockouts(");
			StringAssert.Contains(RepoFiles.GetMethod(source,"GetBlockoutClearPerms"),"EnumPermType.BlockoutDelete,true");
			StringAssert.Contains(RepoFiles.GetMethod(source,"LogBlockoutClearAndReport"),"permTypeClear:EnumPermType.BlockoutDelete");
			StringAssert.Contains(RepoFiles.GetMethod(source,"DeleteWebSchedAsapBlockout_Click"),"EnumPermType.BlockoutDelete");
		}

		[TestMethod]
		public void ControlAppt_RightClickMenuEnablesEachItemByItsAction() {
			string method=RepoFiles.StripLineComments(RepoFiles.GetMethod(ReadControlAppt(),"ContrApptPanel_ApptMainAreaRightClicked"));
			StringAssert.Contains(method,"toolStripItemAdd.Enabled=blockoutPerms.CanDoAny(BlockoutAction.Create)");
			StringAssert.Contains(method,"toolStripItemEdit.Enabled=blockoutPerms.CanDo(BlockoutAction.Edit,isFlagged)");
			StringAssert.Contains(method,"toolStripItemCopy.Enabled=blockoutPerms.CanDo(BlockoutAction.Copy,isFlagged)");
			StringAssert.Contains(method,"toolStripItemCut.Enabled=blockoutPerms.CanDo(BlockoutAction.Cut,isFlagged)");
			StringAssert.Contains(method,"toolStripItemDelete.Enabled=blockoutPerms.CanDo(BlockoutAction.Delete,isFlagged)");
			StringAssert.Contains(method,"toolStripItemPaste.Enabled=blockoutPerms.CanDo(BlockoutAction.Paste,");
			StringAssert.Contains(method,"toolStripItemCutCopyPaste.Enabled=blockoutPerms.CanOpenCutCopyPasteTool()");
			Assert.AreEqual(3,Regex.Matches(method,@"toolStripItemClearForDay\w*\.Enabled=blockoutPerms\.CanDoAny\(BlockoutAction\.Clear\)").Count,
				"Clear for day, for day and operatory, and for day and clinic.");
		}

		[TestMethod]
		public void BlockoutCode_NoLongerChecksTheOldSinglePermission() {
			//13 now means Blockout Edit; every check goes through BlockoutPerms with an action instead.
			List<string> listFiles=new List<string> {
				RepoFiles.Read("OpenDental","Main Modules","ControlAppt.cs"),
				RepoFiles.Read("OpenDental","Forms","FormBlockoutCutCopyPaste.cs"),
				RepoFiles.Read("OpenDental","Forms","FormScheduleBlockEdit.cs"),
			};
			foreach(string source in listFiles.Select(x => RepoFiles.StripLineComments(x))) {
				Assert.IsFalse(Regex.IsMatch(source,@"EnumPermType\.Blockouts\b(?!Flagged)"),"EnumPermType.Blockouts is used directly.");
				Assert.IsFalse(source.Contains("Schedules.GetBlockoutPermType("),"The type-only permission lookup is used.");
			}
		}

		[TestMethod]
		public void FormScheduleBlockEdit_AddForNewEditForExistingDeleteForDelete() {
			string source=RepoFiles.StripLineComments(RepoFiles.Read("OpenDental","Forms","FormScheduleBlockEdit.cs"));
			StringAssert.Contains(RepoFiles.GetMethod(source,"FormScheduleBlockEdit_Load"),"IsNew ? BlockoutAction.Create : BlockoutAction.Edit");
			StringAssert.Contains(RepoFiles.GetMethod(source,"butDelete_Click"),"BlockoutAction.Delete");
			string methodSave=RepoFiles.GetMethod(source,"butSave_Click");
			StringAssert.Contains(methodSave,"IsNew ? BlockoutAction.Create : BlockoutAction.Edit");
			StringAssert.Contains(methodSave,"BlockoutPerms.GetPermType(BlockoutAction.Edit,_blockoutTypeOriginal)");
			StringAssert.Contains(methodSave,"BlockoutPerms.GetPermTypesToDelete(listOverlapSchedules)");
		}

		[TestMethod]
		public void FormBlockoutCutCopyPaste_ButtonsFollowAddDeleteAndReplace() {
			string source=RepoFiles.StripLineComments(RepoFiles.Read("OpenDental","Forms","FormBlockoutCutCopyPaste.cs"));
			string methodEnable=RepoFiles.GetMethod(source,"EnableForBlockoutPerms");
			StringAssert.Contains(methodEnable,"butCopyDay.Enabled=blockoutPerms.CanDoAny(BlockoutAction.Copy)");
			StringAssert.Contains(methodEnable,"butCopyWeek.Enabled=blockoutPerms.CanDoAny(BlockoutAction.Copy)");
			StringAssert.Contains(methodEnable,"butPaste.Enabled=blockoutPerms.CanDoAny(BlockoutAction.Paste)");
			StringAssert.Contains(methodEnable,"butRepeat.Enabled=blockoutPerms.CanDoAny(BlockoutAction.Paste)");
			StringAssert.Contains(methodEnable,"butClearDay.Enabled=blockoutPerms.CanDoAny(BlockoutAction.Clear)");
			StringAssert.Contains(methodEnable,"CanReplaceOnPaste()");
			StringAssert.Contains(RepoFiles.GetMethod(source,"FormBlockoutCutCopyPaste_Load"),"EnableForBlockoutPerms();");
			string methodPaste=RepoFiles.GetMethod(source,"CopyOverBlockouts");
			StringAssert.Contains(methodPaste,"checkReplace.Checked && !blockoutPerms.CanReplaceOnPaste()");
			StringAssert.Contains(methodPaste,"blockoutPerms.HasUnflagged(BlockoutAction.Paste),blockoutPerms.HasFlagged");
			StringAssert.Contains(RepoFiles.GetMethod(source,"butClearDay_Click"),"EnumPermType.BlockoutDelete,true");
			StringAssert.Contains(RepoFiles.GetMethod(source,"LogClear"),"permTypeClear:EnumPermType.BlockoutDelete");
		}
		#endregion Blockout permissions (25.4)

		#region Make Recall search preference (26.1)
		private static void AssertPrefCheckedBeforeSearch(string code,string where) {
			int indexPref=code.IndexOf("PrefC.GetBool(PrefName.AppointmentMakeRecallSearchesOpenings)");
			int indexShow=code.IndexOf("ShowSearch()");
			int indexDo=code.IndexOf("DoSearch(isForMakeRecall:true)");
			Assert.IsTrue(indexPref>=0,where+": the preference is read.");
			Assert.IsTrue(indexShow>indexPref && indexDo>indexPref,where+": the preference is read before the search opens and runs.");
		}

		[TestMethod]
		public void MakeRecall_ToolbarButtonSkipsTheSearchWhenOff() {
			string method=RepoFiles.StripLineComments(RepoFiles.GetMethod(ReadControlAppt(),"butMakeRecall_Click"));
			AssertPrefCheckedBeforeSearch(method,"butMakeRecall_Click");
			//Pinning and the search date still happen first.
			Assert.IsTrue(method.IndexOf("SendToPinBoardAptNums(")<method.IndexOf("PrefName.AppointmentMakeRecallSearchesOpenings"));
			Assert.IsTrue(method.IndexOf("dateSearch.Text=")<method.IndexOf("PrefName.AppointmentMakeRecallSearchesOpenings"));
		}

		[TestMethod]
		public void MakeRecall_PinboardAndSearchResultSkipsTheSearchWhenOff() {
			string method=RepoFiles.StripLineComments(RepoFiles.GetMethod(ReadControlAppt(),"ProcessOtherDlg"));
			int indexCase=method.IndexOf("case OtherResult.PinboardAndSearch:");
			Assert.IsTrue(indexCase>=0);
			int indexNextCase=method.IndexOf("case OtherResult.",indexCase+10);
			string block=method.Substring(indexCase,indexNextCase-indexCase);
			AssertPrefCheckedBeforeSearch(block,"OtherResult.PinboardAndSearch");
			//When off, the pinned appointments are still drawn.
			string blockOff=block.Substring(block.IndexOf("PrefName.AppointmentMakeRecallSearchesOpenings"));
			Assert.IsTrue(blockOff.IndexOf("RefreshPeriod(listPinApptNums:listSelectedAptNums)")<blockOff.IndexOf("ShowSearch()"),
				"The 'off' branch refreshes before it leaves.");
		}
		#endregion Make Recall search preference (26.1)

		#region Preferences window (26.1)
		[TestMethod]
		public void ApptGeneral_HasControlsNamedForTheirPrefInfos() {
			//M2-02 registered the hover help under these control names; FormPreferences finds the entry by control name and category.
			XDocument xDocument=XDocument.Load(RepoFiles.GetPath("OpenDental","Resources","PrefInfos.xml"));
			string designer=RepoFiles.Read("OpenDental","User Controls","Preferences","UserControlApptGeneral.Designer.cs");
			foreach(string prefName in new[] { "AppointmentMakeRecallSearchesOpenings","ScheduleProvUnassigned" }) {
				XElement xElement=xDocument.Descendants("PrefInf").Single(x => (string)x.Element("PrefName")==prefName);
				Assert.AreEqual("ApptGeneral",(string)xElement.Element("Category"),prefName);
				string controlName=(string)xElement.Element("ControlName");
				StringAssert.Contains(designer,"this."+controlName+".Name = \""+controlName+"\";",prefName);
				StringAssert.Contains(designer,"Controls.Add(this."+controlName+");",prefName);
			}
			StringAssert.Contains(designer,"\"Make Recall automatically searches for openings\"");
			StringAssert.Contains(designer,"\"Default provider for unassigned operatories\"");
		}

		[TestMethod]
		public void ApptGeneral_ReadsAndSavesBothPreferences() {
			string source=RepoFiles.StripLineComments(RepoFiles.Read("OpenDental","User Controls","Preferences","UserControlApptGeneral.cs"));
			string methodFill=RepoFiles.GetMethod(source,"FillApptGeneral");
			StringAssert.Contains(methodFill,"checkAppointmentMakeRecallSearchesOpenings.Checked=PrefC.GetBool(PrefName.AppointmentMakeRecallSearchesOpenings)");
			StringAssert.Contains(methodFill,"comboScheduleProvUnassigned.SetSelectedProvNum(PrefC.GetLong(PrefName.ScheduleProvUnassigned))");
			string methodSave=RepoFiles.GetMethod(source,"SaveApptGeneral");
			StringAssert.Contains(methodSave,"Prefs.UpdateBool(PrefName.AppointmentMakeRecallSearchesOpenings,checkAppointmentMakeRecallSearchesOpenings.Checked)");
			StringAssert.Contains(methodSave,"Prefs.UpdateLong(PrefName.ScheduleProvUnassigned,comboScheduleProvUnassigned.GetSelectedProvNum())");
		}

		[TestMethod]
		public void EditDay_NoLongerShowsOrSavesTheDefaultProvider() {
			string source=RepoFiles.StripLineComments(RepoFiles.Read("OpenDental","Forms","FormScheduleDayEdit.cs"));
			string designer=RepoFiles.Read("OpenDental","Forms","FormScheduleDayEdit.designer.cs");
			string resx=RepoFiles.Read("OpenDental","Forms","FormScheduleDayEdit.resx");
			Assert.IsFalse(source.Contains("PrefName.ScheduleProvUnassigned"),"Saving schedules no longer writes the preference.");
			Assert.IsFalse(source.Contains("comboProv"));
			Assert.IsFalse(designer.Contains("comboProv"));
			Assert.IsFalse(designer.Contains("Default Prov for Unassigned"));
			Assert.IsFalse(resx.Contains("Default Prov for Unassigned"),"The footnote is gone too.");
			StringAssert.Contains(designer,"this.listProv","The provider list stays.");
		}
		#endregion Preferences window (26.1)
	}
}
