// Package B-APPT (docs/specs/port-backlog.md), item v25_1-appt-multiple-completed-tasks: when TasksForApptAllowMultiple is off, the limit of
// one task per appointment counts only tasks that are not complete, so an appointment can collect any number of completed tasks plus at most
// one open task.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>What the Task button of Edit Appointment does.</summary>
	public enum ApptTaskButtonAction {
		///<summary>Start a new task for the appointment.</summary>
		NewTask,
		///<summary>Open the appointment's one open task.</summary>
		OpenTask,
		///<summary>Show the appointment's tasks in a list (Tasks for Appointment).</summary>
		ShowList,
	}

	///<summary>Counting rules for tasks attached to appointments, and the query the attach window uses. "Complete" means TaskStatus Done.</summary>
	public static class ApptTasks {
		///<summary>True if the task is not complete, so it counts toward the one-task limit.</summary>
		public static bool IsOpen(Task task) {
			return task!=null && task.TaskStatus!=TaskStatusEnum.Done;
		}

		///<summary>True if the task is attached to an appointment.</summary>
		public static bool IsAttachedToAppt(Task task) {
			return task!=null && task.ObjectType==TaskObjectType.Appointment && task.KeyNum>0;
		}

		///<summary>The Task button of Edit Appointment. With multiple tasks allowed, it always starts a new task (as in 24.3). Otherwise: one
		///open task opens it, several open tasks (possible after the preference changed or a task was reopened) show the list, and no open task
		///starts a new one even when completed tasks exist. taskToOpen is set only for OpenTask.</summary>
		public static ApptTaskButtonAction GetTaskButtonAction(bool isMultipleAllowed,List<Task> listTasksForAppt,out Task taskToOpen) {
			taskToOpen=null;
			if(isMultipleAllowed) {
				return ApptTaskButtonAction.NewTask;
			}
			List<Task> listTasksOpen=(listTasksForAppt??new List<Task>()).FindAll(x => IsOpen(x));
			if(listTasksOpen.Count==1) {
				taskToOpen=listTasksOpen[0];
				return ApptTaskButtonAction.OpenTask;
			}
			if(listTasksOpen.Count>1) {
				return ApptTaskButtonAction.ShowList;
			}
			return ApptTaskButtonAction.NewTask;
		}

		///<summary>True if copying this task in the task tree is refused: a copy of an open task attached to an appointment would be a second
		///open task on that appointment. A completed task can be copied.</summary>
		public static bool IsCopyBlocked(bool isMultipleAllowed,Task task) {
			return !isMultipleAllowed && IsAttachedToAppt(task) && IsOpen(task);
		}

		///<summary>True if the task tree clears its clipboard after pasting a cut task, so the task cannot be pasted again as a copy. Only for an
		///open task attached to an appointment; pasting a completed one again only adds completed tasks.</summary>
		public static bool IsClipboardClearedAfterPaste(bool isMultipleAllowed,Task task,bool wasCut) {
			return wasCut && !isMultipleAllowed && IsAttachedToAppt(task) && IsOpen(task);
		}

		///<summary>The subset of listAptNums that already has an open (not complete) task attached. These cannot get another task while
		///TasksForApptAllowMultiple is off. Completed tasks are ignored.</summary>
		public static List<long> GetApptNumsWithOpenTask(List<long> listAptNums) {
			if(listAptNums.IsNullOrEmpty()) {
				return new List<long>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod(),listAptNums);
			}
			string command="SELECT DISTINCT KeyNum FROM task "
				+"WHERE ObjectType="+POut.Int((int)TaskObjectType.Appointment)+" "
				+"AND TaskStatus!="+POut.Int((int)TaskStatusEnum.Done)+" "
				+"AND KeyNum IN("+string.Join(",",listAptNums.Distinct().Select(x => POut.Long(x)))+")";
			return Db.GetListLong(command,false);
		}
	}
}
