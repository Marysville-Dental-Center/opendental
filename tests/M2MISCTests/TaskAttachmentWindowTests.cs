// v26_1-misc-task-attach-nonmodal: the Task Attachments and Task Attachment Edit windows are non-modal (OpenDental.TaskAttachmentWindows).
// The windows themselves are not opened here (the desktop client is never launched); their bookkeeping and the code that opens them are tested.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace M2MISCTests {
	[TestClass]
	public class TaskAttachmentWindowTests {
		private class FakeWindow {
			public bool IsOpen=true;
			public string Name;
		}

		#region Registry
		[TestMethod]
		public void Registry_GetReturnsTheOpenWindow_AndForgetsClosedOnes() {
			OpenWindowRegistry<FakeWindow> registry=new OpenWindowRegistry<FakeWindow>(x => x.IsOpen);
			Assert.IsNull(registry.Get("task:1"));
			FakeWindow window=new FakeWindow { Name="A" };
			registry.Add("task:1",window);
			Assert.AreSame(window,registry.Get("task:1"),"A second click finds the open window, so it is brought to the front, not opened again.");
			window.IsOpen=false;
			Assert.IsNull(registry.Get("task:1"));
			Assert.AreEqual(0,registry.Count);
		}

		[TestMethod]
		public void Registry_RemoveOnlyForgetsTheSameWindow() {
			OpenWindowRegistry<FakeWindow> registry=new OpenWindowRegistry<FakeWindow>(x => x.IsOpen);
			FakeWindow windowOld=new FakeWindow { Name="old" };
			FakeWindow windowNew=new FakeWindow { Name="new" };
			registry.Add("task:1",windowOld);
			registry.Add("task:1",windowNew);//the old one closed and a new one opened
			registry.Remove("task:1",windowOld);//the old one's late Closed event
			Assert.AreSame(windowNew,registry.Get("task:1"));
			registry.Remove("task:1",windowNew);
			Assert.IsNull(registry.Get("task:1"));
		}

		[TestMethod]
		public void Registry_GetWhere_FindsTheWindowsOfOneTask() {
			OpenWindowRegistry<FakeWindow> registry=new OpenWindowRegistry<FakeWindow>(x => x.IsOpen);
			Task task1=new Task { TaskNum=1 };
			Task task12=new Task { TaskNum=12 };
			registry.Add(TaskAttachmentWindows.GetKeyAttachmentEdit(1,new TaskAttachment { TaskAttachmentNum=5 }),new FakeWindow { Name="1-5" });
			registry.Add(TaskAttachmentWindows.GetKeyAttachmentEdit(1,new TaskAttachment { IsNew=true }),new FakeWindow { Name="1-new" });
			registry.Add(TaskAttachmentWindows.GetKeyAttachmentEdit(12,new TaskAttachment { TaskAttachmentNum=6 }),new FakeWindow { Name="12-6" });
			string prefixTask1=TaskAttachmentWindows.GetKeyAttachments(task1.TaskNum)+":";
			List<string> listNames=registry.GetWhere(x => x.StartsWith(prefixTask1)).Select(x => x.Name).OrderBy(x => x).ToList();
			CollectionAssert.AreEqual(new List<string> { "1-5","1-new" },listNames,"Task 12's window is not task 1's.");
			Assert.AreEqual(3,registry.Count);
		}

		[TestMethod]
		public void Keys_OneWindowPerTask_OnePerAttachment_OneNewPerTask() {
			Assert.AreEqual("task:7",TaskAttachmentWindows.GetKeyAttachments(7));
			Assert.AreEqual("task:7:attachment:3",TaskAttachmentWindows.GetKeyAttachmentEdit(7,new TaskAttachment { TaskAttachmentNum=3 }));
			string keyNew1=TaskAttachmentWindows.GetKeyAttachmentEdit(7,new TaskAttachment { IsNew=true });
			string keyNew2=TaskAttachmentWindows.GetKeyAttachmentEdit(7,new TaskAttachment { IsNew=true });
			Assert.AreEqual(keyNew1,keyNew2,"Only one new-attachment window per task.");
			Assert.AreEqual("task:7:attachment:new",keyNew1);
			Assert.AreNotEqual(keyNew1,TaskAttachmentWindows.GetKeyAttachmentEdit(8,new TaskAttachment { IsNew=true }));
		}

		[TestMethod]
		public void NothingOpen_CloseAndDeleteAreHarmless() {
			Assert.IsFalse(TaskAttachmentWindows.HasOpenWindows(987654));
			TaskAttachmentWindows.CloseForTask(987654);
			TaskAttachmentWindows.SetTaskDeleted(987654);
		}
		#endregion Registry

		#region The code that opens the windows
		[TestMethod]
		public void TaskEdit_OpensAttachmentsNonModal_AndClosesThemWithTheTask() {
			string code=TestDb.ReadRepoFile("OpenDental","Forms","FormTaskEdit.cs");
			string method=GetMethod(code,"private void butAttachmnents_Click(");
			StringAssert.Contains(method,"TaskAttachmentWindows.ShowAttachments(this,TaskCur,checkIsReadOnly.Checked,OnTaskAttachmentsChanged);");
			Assert.IsFalse(method.Contains("ShowDialog"),"The Task Attachments window is shown modally again.");
			StringAssert.Contains(code,"FormClosed+=(sender,e) => TaskAttachmentWindows.CloseForTask(TaskCur.TaskNum);");
			StringAssert.Contains(GetMethod(code,"private void SetFormToDeletedMode("),"TaskAttachmentWindows.SetTaskDeleted(TaskCur.TaskNum);");
			//The changes still refresh the task window and the task lists.
			string methodChanged=GetMethod(code,"private void OnTaskAttachmentsChanged(");
			StringAssert.Contains(methodChanged,"FillTextAttachments();");
			StringAssert.Contains(methodChanged,"UserControlTasks.RefillLocalTaskGrids(");
		}

		[TestMethod]
		public void AttachmentsWindow_OpensEditWindowsNonModal() {
			string code=TestDb.ReadRepoFile("OpenDental","Forms","FormTaskAttachments.cs");
			Assert.IsFalse(code.Contains("ShowDialog"),"An attachment edit window is shown modally again.");
			Assert.AreEqual(2,Regex.Matches(code,@"TaskAttachmentWindows\.ShowAttachmentEdit\(this,_task,").Count,"Add and double-click");
			StringAssert.Contains(GetMethod(code,"private void OnAttachmentSaved("),"ActionAttachmentsChanged?.Invoke();");
		}

		[TestMethod]
		public void EditWindow_ClosesItselfAfterSaveAndDelete_AndDiscardsWhenTheTaskIsGone() {
			string code=TestDb.ReadRepoFile("OpenDental","Forms","FormTaskAttachmentEdit.cs");
			//A non-modal window does not close when DialogResult is set: every DialogResult must be followed by Close().
			MatchCollection matchCollection=Regex.Matches(code,@"DialogResult=DialogResult\.\w+;\s*\r?\n\s*(\S[^\r\n]*)");
			Assert.IsTrue(matchCollection.Count>=4);
			foreach(Match match in matchCollection) {
				StringAssert.StartsWith(match.Groups[1].Value,"Close();",match.Value);
			}
			string method=GetMethod(code,"private bool IsTaskDeletedDiscard(");
			StringAssert.Contains(method,"The task for this attachment was deleted.");
			StringAssert.Contains(GetMethod(code,"private void butSave_Click("),"if(IsTaskDeletedDiscard()) {");
			StringAssert.Contains(GetMethod(code,"private void butDelete_Click("),"if(IsTaskDeletedDiscard()) {");
		}

		[TestMethod]
		public void TaskList_OpensAnAttachmentNonModal() {
			string code=TestDb.ReadRepoFile("OpenDental","User Controls","UserControlTasks.cs");
			string method=GetMethod(code,"private void NavAtt_Clicked(");
			StringAssert.Contains(method,"TaskAttachmentWindows.ShowAttachmentEdit(FindForm(),task,taskAttachment,");
			Assert.IsFalse(method.Contains("ShowDialog"));
		}
		#endregion The code that opens the windows

		#region Database
		[TestMethod]
		public void DeletedTask_IsSeenByTheWindows() {
			//FormTaskAttachments.CanAddAttachment and FormTaskAttachmentEdit.IsTaskDeletedDiscard rely on Tasks.IsTaskDeleted.
			TestDb.ResetOrSkip();
			Task task=new Task { Descript="Synthetic task",DateTimeEntry=DateTime.Now,ObjectType=TaskObjectType.None };
			long taskNum=OpenPmsDentalBusiness.Crud.TaskCrud.Insert(task);
			Assert.IsFalse(Tasks.IsTaskDeleted(taskNum));
			DataCore.NonQ("DELETE FROM task WHERE TaskNum="+taskNum);
			Assert.IsTrue(Tasks.IsTaskDeleted(taskNum));
		}
		#endregion Database

		///<summary>The text of a method, from its signature to the next method's signature.</summary>
		internal static string GetMethod(string code,string signature) {
			int start=code.IndexOf(signature);
			Assert.IsTrue(start>=0,"Not found: "+signature);
			Match matchNext=new Regex(@"\n\t\t(public|private|protected|internal) ").Match(code,start+signature.Length);
			int end=matchNext.Success ? matchNext.Index : code.Length;
			return code.Substring(start,end-start);
		}
	}
}
