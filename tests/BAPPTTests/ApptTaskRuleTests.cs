// Unit tests for item v25_1-appt-multiple-completed-tasks: with TasksForApptAllowMultiple off, the one-task limit counts only open tasks.
// No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BAPPTTests {
	[TestClass]
	public class ApptTaskRuleTests {
		private static Task ApptTask(long taskNum,TaskStatusEnum taskStatus,long aptNum=500) {
			return new Task { TaskNum=taskNum,TaskStatus=taskStatus,ObjectType=TaskObjectType.Appointment,KeyNum=aptNum };
		}

		private static ApptTaskButtonAction Action(bool isMultipleAllowed,List<Task> listTasks,out Task taskToOpen) {
			return ApptTasks.GetTaskButtonAction(isMultipleAllowed,listTasks,out taskToOpen);
		}

		[TestMethod]
		public void Complete_MeansDone() {
			Assert.IsTrue(ApptTasks.IsOpen(ApptTask(1,TaskStatusEnum.New)));
			Assert.IsTrue(ApptTasks.IsOpen(ApptTask(1,TaskStatusEnum.Viewed)));
			Assert.IsFalse(ApptTasks.IsOpen(ApptTask(1,TaskStatusEnum.Done)));
			Assert.IsFalse(ApptTasks.IsOpen(null));
		}

		[TestMethod]
		public void TaskButton_MultipleAllowed_AlwaysNew() {
			Assert.AreEqual(ApptTaskButtonAction.NewTask,Action(true,new List<Task> { ApptTask(1,TaskStatusEnum.New) },out Task taskToOpen));
			Assert.IsNull(taskToOpen);
		}

		[TestMethod]
		public void TaskButton_NoTasks_New() {
			Assert.AreEqual(ApptTaskButtonAction.NewTask,Action(false,new List<Task>(),out _));
			Assert.AreEqual(ApptTaskButtonAction.NewTask,Action(false,null,out _));
		}

		[TestMethod]
		public void TaskButton_OnlyCompletedTasks_StartsANewOne() {
			Assert.AreEqual(ApptTaskButtonAction.NewTask,Action(false,new List<Task> { ApptTask(1,TaskStatusEnum.Done) },out Task taskToOpen));
			Assert.IsNull(taskToOpen);
			Assert.AreEqual(ApptTaskButtonAction.NewTask,Action(false,new List<Task> { ApptTask(1,TaskStatusEnum.Done),ApptTask(2,TaskStatusEnum.Done) },out _),
				"In 24.3 two tasks showed the list; completed tasks no longer count.");
		}

		[TestMethod]
		public void TaskButton_OneOpenTask_OpensIt_EvenWithCompletedOnes() {
			Assert.AreEqual(ApptTaskButtonAction.OpenTask,Action(false,new List<Task> { ApptTask(1,TaskStatusEnum.Viewed) },out Task taskToOpen));
			Assert.AreEqual(1,taskToOpen.TaskNum);
			Assert.AreEqual(ApptTaskButtonAction.OpenTask,
				Action(false,new List<Task> { ApptTask(1,TaskStatusEnum.Done),ApptTask(2,TaskStatusEnum.New),ApptTask(3,TaskStatusEnum.Done) },out taskToOpen));
			Assert.AreEqual(2,taskToOpen.TaskNum);
		}

		[TestMethod]
		public void TaskButton_SeveralOpenTasks_ShowsTheList() {
			//Possible after the preference was switched off, or after a completed task was reopened.
			Assert.AreEqual(ApptTaskButtonAction.ShowList,Action(false,new List<Task> { ApptTask(1,TaskStatusEnum.New),ApptTask(2,TaskStatusEnum.Viewed) },out _));
		}

		[TestMethod]
		public void TaskTree_CopyBlockedOnlyForOpenApptTasks() {
			Assert.IsTrue(ApptTasks.IsCopyBlocked(false,ApptTask(1,TaskStatusEnum.New)));
			Assert.IsFalse(ApptTasks.IsCopyBlocked(false,ApptTask(1,TaskStatusEnum.Done)),"A completed task can be copied.");
			Assert.IsFalse(ApptTasks.IsCopyBlocked(true,ApptTask(1,TaskStatusEnum.New)),"Multiple tasks allowed.");
			Assert.IsFalse(ApptTasks.IsCopyBlocked(false,ApptTask(1,TaskStatusEnum.New,aptNum:0)),"Not attached to an appointment.");
			Assert.IsFalse(ApptTasks.IsCopyBlocked(false,new Task { TaskStatus=TaskStatusEnum.New,ObjectType=TaskObjectType.Patient,KeyNum=500 }));
		}

		[TestMethod]
		public void TaskTree_ClipboardClearedAfterPastingACutOpenApptTask() {
			Assert.IsTrue(ApptTasks.IsClipboardClearedAfterPaste(false,ApptTask(1,TaskStatusEnum.New),wasCut:true));
			Assert.IsFalse(ApptTasks.IsClipboardClearedAfterPaste(false,ApptTask(1,TaskStatusEnum.Done),wasCut:true));
			Assert.IsFalse(ApptTasks.IsClipboardClearedAfterPaste(false,ApptTask(1,TaskStatusEnum.New),wasCut:false));
			Assert.IsFalse(ApptTasks.IsClipboardClearedAfterPaste(true,ApptTask(1,TaskStatusEnum.New),wasCut:true));
		}
	}
}
