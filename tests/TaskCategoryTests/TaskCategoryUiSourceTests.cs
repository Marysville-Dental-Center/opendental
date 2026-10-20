// Source checks for the task category UI. The Task window, Tasks Preferences and the task list are WinForms code that these tests cannot open
// (the desktop client is never launched by tests), so they check the code that decides what is shown instead.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TaskCategoryTests {
	[TestClass]
	public class TaskCategoryUiSourceTests {
		private static string GetRepoRoot() {
			DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir.FullName,"OpenDental.sln"))) {
					return dir.FullName;
				}
				dir=dir.Parent;
			}
			throw new DirectoryNotFoundException("The repository root (OpenDental.sln) was not found above the test folder.");
		}

		private static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(new[] { GetRepoRoot() }.Concat(arrayParts).ToArray()));
		}

		[TestMethod]
		public void TriagePosition_IsNeverShown() {
			//task.TriagePosition is Open Dental HQ's support triage data. It is kept for data compatibility; no window or grid may show it.
			string root=GetRepoRoot();
			foreach(string folder in new[] { "OpenDental","WpfControlsOD" }) {
				foreach(string path in Directory.EnumerateFiles(Path.Combine(root,folder),"*.cs",SearchOption.AllDirectories)) {
					if(path.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar)
						|| path.Contains(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar))
					{
						continue;
					}
					Assert.IsFalse(File.ReadAllText(path).Contains("TriagePosition"),"UI code reads the HQ triage position: "+path);
				}
			}
		}

		[TestMethod]
		public void TaskWindow_ShowsCategoryFromThePreference_NotFromHq() {
			string code=ReadRepoFile("OpenDental","Forms","FormTaskEdit.cs");
			string designer=ReadRepoFile("OpenDental","Forms","FormTaskEdit.designer.cs");
			StringAssert.Contains(code,"Tasks.IsCategoryShown()");
			StringAssert.Contains(code,"Tasks.GetCategoryChoices(TaskCur.Category)");
			Assert.IsFalse(code.Contains("Triage Category"),"The HQ-only Invalid Triage Category message is replaced by ordinary None handling.");
			StringAssert.Contains(designer,"this.comboCategory = new OpenDental.UI.ComboBox();");
			StringAssert.Contains(designer,"this.labelCategory.Text = \"Category\";");
		}

		[TestMethod]
		public void TaskList_AddsTheCategoryColumnFromThePreference() {
			string code=ReadRepoFile("OpenDental","User Controls","UserControlTasks.cs");
			StringAssert.Contains(code,"Tasks.IsCategoryShown()");
			StringAssert.Contains(code,"Lan.g(\"TableTasks\",\"Category\")");
			StringAssert.Contains(code,"Tasks.GetCategoryColor(");
		}

		[TestMethod]
		public void TaskPreferences_SavesTaskCategoryShow() {
			string code=ReadRepoFile("OpenDental","Forms","FormTaskPreferences.cs");
			string designer=ReadRepoFile("OpenDental","Forms","FormTaskPreferences.designer.cs");
			StringAssert.Contains(code,"checkTaskCategoryShow.Checked=PrefC.GetBool(PrefName.TaskCategoryShow);");
			StringAssert.Contains(code,"Prefs.UpdateBool(PrefName.TaskCategoryShow,checkTaskCategoryShow.Checked)");
			StringAssert.Contains(designer,"this.checkTaskCategoryShow.Text = \"Show task category\";");
		}

		[TestMethod]
		public void Definitions_TaskCategoriesHelpText_IsNotHqOnly() {
			string code=ReadRepoFile("OpenDental","Logic","DefL.cs");
			Assert.IsFalse(code.Contains("HQ Only as of now"));
		}
	}
}
