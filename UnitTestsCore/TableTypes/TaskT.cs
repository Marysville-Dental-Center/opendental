using System;
using System.Collections.Generic;
using System.Text;
using OpenPmsDentalBusiness;

namespace UnitTestsCore {
	public class TaskT {
		///<summary>Creates a TaskSubscription, dateTimeEntry will be DateTime.Now if not specified.</summary>
		public static Task CreateTask(long taskListNum=0,long keyNum=0,string descript="",
			TaskStatusEnum taskStatus=TaskStatusEnum.New,bool isRepeating=false,TaskDateType dateType=TaskDateType.None,long fromNum=0,
			TaskObjectType objectType=TaskObjectType.None,DateTime dateTimeEntry=new DateTime(),long userNum=0,bool isUnread=false,string parentDesc="",string patientName="",
			long priorityDefNum=0,string reminderGroupId="",TaskReminderType reminderType=TaskReminderType.NoReminder,int reminderFrequency=0)
		{
			if(dateTimeEntry==DateTime.MinValue) {
				dateTimeEntry=DateTime.Now;
			}
			Task task=new Task
			{
				TaskListNum=taskListNum,
				DateTask=DateTime.MinValue,
				KeyNum=keyNum,
				Descript=descript,
				TaskStatus=taskStatus,
				IsRepeating=isRepeating,
				DateType=dateType,
				FromNum=fromNum,
				ObjectType=objectType,
				DateTimeEntry=dateTimeEntry,
				UserNum=userNum,
				DateTimeFinished=DateTime.MinValue,
				IsUnread=isUnread,
				ParentDesc=parentDesc,
				PatientName=patientName,
				PriorityDefNum=priorityDefNum,
				ReminderGroupId=reminderGroupId,
				ReminderType=reminderType,
				ReminderFrequency=reminderFrequency,
				DateTimeOriginal=DateTime.Now
			};
			Tasks.Insert(task);
			task=Tasks.GetOne(task.TaskNum);//Make sure task matches Db. Avoids problems with DateTime columns.
			return task;
		}

		///<summary>Deletes everything from the TaskSubscription table.  Does not truncate the table so that PKs are not reused on accident.</summary>
		public static void ClearTaskTable() {
			string command="DELETE FROM task";
			DataCore.NonQ(command);
		}

		///<summary>Creates and returns a list of 7 tasks with all fields utilized and varied. Useful for testing a task search for different fields.</summary>
		public static List<OpenPmsDentalBusiness.Task> CreateVariedTaskSet() {
			#region Create TaskLists
			TaskListT.ClearTaskListTable();
			TaskList tasklist1=TaskListT.CreateTaskList(descript:"TaskList 1",objectType:OpenPmsDentalBusiness.TaskObjectType.None);
			TaskList tasklist2=TaskListT.CreateTaskList(descript:"TaskList 2",objectType:OpenPmsDentalBusiness.TaskObjectType.Patient);
			TaskList tasklist3=TaskListT.CreateTaskList(descript:"TaskList 3",objectType:OpenPmsDentalBusiness.TaskObjectType.Appointment);
			#endregion
			List<OpenPmsDentalBusiness.Task> listTasks=new List<OpenPmsDentalBusiness.Task>();
			#region Creating Tasks and Adding to listTasks
			listTasks.Add(new OpenPmsDentalBusiness.Task {
				TaskListNum=tasklist1.TaskListNum, 
				KeyNum=0, 
				Descript="Task 1",
				TaskStatus=OpenPmsDentalBusiness.TaskStatusEnum.New, 
				IsRepeating=false,
				DateType=OpenPmsDentalBusiness.TaskDateType.None,
				FromNum=0,
				ObjectType=OpenPmsDentalBusiness.TaskObjectType.None, 
				DateTimeEntry=default,
				UserNum=0,
				IsUnread=false,
				ParentDesc="",
				PatientName="",
				PriorityDefNum=0,
				ReminderGroupId="",
				ReminderType=OpenPmsDentalBusiness.TaskReminderType.NoReminder,
				ReminderFrequency=0,
			});
			listTasks.Add(new OpenPmsDentalBusiness.Task {
				TaskListNum=tasklist1.TaskListNum, 
				KeyNum=0, 
				Descript="Task 2",
				TaskStatus=OpenPmsDentalBusiness.TaskStatusEnum.Viewed, 
				IsRepeating=false,
				DateType=OpenPmsDentalBusiness.TaskDateType.Week,
				FromNum=0,
				ObjectType=OpenPmsDentalBusiness.TaskObjectType.None, 
				DateTimeEntry=default,
				UserNum=0,
				IsUnread=false,
				ParentDesc="",
				PatientName="",
				PriorityDefNum=0,
				ReminderGroupId="",
				ReminderType=OpenPmsDentalBusiness.TaskReminderType.NoReminder,
				ReminderFrequency=0,
			});
			listTasks.Add(new OpenPmsDentalBusiness.Task {
				TaskListNum=tasklist1.TaskListNum,
				KeyNum=0, 
				Descript="Task 3",
				TaskStatus=OpenPmsDentalBusiness.TaskStatusEnum.Done, 
				IsRepeating=true,
				DateType=OpenPmsDentalBusiness.TaskDateType.Month,
				FromNum=0,
				ObjectType=OpenPmsDentalBusiness.TaskObjectType.None, 
				DateTimeEntry=default,
				UserNum=0,
				IsUnread=false,
				ParentDesc="",
				PatientName="",
				PriorityDefNum=0,
				ReminderGroupId="",
				ReminderType=OpenPmsDentalBusiness.TaskReminderType.NoReminder,
				ReminderFrequency=0,
			});
			OpenPmsDentalBusiness.Patient patient=PatientT.CreatePatient("Patient1");
			listTasks.Add(new OpenPmsDentalBusiness.Task {
				TaskListNum=tasklist2.TaskListNum, 
				KeyNum=patient.PatNum, 
				Descript="Task 4",
				TaskStatus=OpenPmsDentalBusiness.TaskStatusEnum.New, 
				IsRepeating=true,
				DateType=OpenPmsDentalBusiness.TaskDateType.None,
				FromNum=0,
				ObjectType=OpenPmsDentalBusiness.TaskObjectType.Patient, 
				DateTimeEntry=default,
				UserNum=0,
				IsUnread=true,
				ParentDesc="",
				PatientName="",
				PriorityDefNum=0,
				ReminderGroupId="",
				ReminderType=OpenPmsDentalBusiness.TaskReminderType.NoReminder,
				ReminderFrequency=0,
			});
			patient=PatientT.CreatePatient("Patient2");
			listTasks.Add(new OpenPmsDentalBusiness.Task {
				TaskListNum=tasklist2.TaskListNum, 
				KeyNum=patient.PatNum, 
				Descript="Task 5",
				TaskStatus=OpenPmsDentalBusiness.TaskStatusEnum.Done, 
				IsRepeating=true,
				DateType=OpenPmsDentalBusiness.TaskDateType.None,
				FromNum=0,
				ObjectType=OpenPmsDentalBusiness.TaskObjectType.Patient, 
				DateTimeEntry=default,
				UserNum=0,
				IsUnread=true,
				ParentDesc="",
				PatientName="",
				PriorityDefNum=0,
				ReminderGroupId="",
				ReminderType=OpenPmsDentalBusiness.TaskReminderType.NoReminder,
				ReminderFrequency=0,
			});
			patient=PatientT.CreatePatient("Patient3");
			OpenPmsDentalBusiness.Operatory operatory=OperatoryT.CreateOperatory();
			long provNum=ProviderT.CreateProvider("provider");
			OpenPmsDentalBusiness.Appointment appointment=AppointmentT.CreateAppointment(patient.PatNum,DateTime.Now.AddDays(-10),operatory.OperatoryNum,provNum);
			listTasks.Add(new OpenPmsDentalBusiness.Task {
				TaskListNum=tasklist3.TaskListNum, 
				KeyNum=appointment.AptNum, 
				Descript="Task 6",
				TaskStatus=OpenPmsDentalBusiness.TaskStatusEnum.New, 
				IsRepeating=true,
				DateType=OpenPmsDentalBusiness.TaskDateType.None,
				FromNum=0,
				ObjectType=OpenPmsDentalBusiness.TaskObjectType.Appointment, 
				DateTimeEntry=default,
				UserNum=0,
				IsUnread=true,
				ParentDesc="",
				PatientName="",
				PriorityDefNum=0,
				ReminderGroupId="",
				ReminderType=OpenPmsDentalBusiness.TaskReminderType.NoReminder,
				ReminderFrequency=0,
			});
			patient=PatientT.CreatePatient("Patient4");
			appointment=AppointmentT.CreateAppointment(patient.PatNum,DateTime.Now.AddDays(-15),operatory.OperatoryNum,provNum);
			listTasks.Add(new OpenPmsDentalBusiness.Task {
				TaskListNum=tasklist3.TaskListNum, 
				KeyNum=appointment.AptNum, 
				Descript="Task 7",
				TaskStatus=OpenPmsDentalBusiness.TaskStatusEnum.Viewed, 
				IsRepeating=true,
				DateType=OpenPmsDentalBusiness.TaskDateType.None,
				FromNum=0,
				ObjectType=OpenPmsDentalBusiness.TaskObjectType.Appointment, 
				DateTimeEntry=default,
				UserNum=0,
				IsUnread=true,
				ParentDesc="",
				PatientName="",
				PriorityDefNum=0,
				ReminderGroupId="",
				ReminderType=OpenPmsDentalBusiness.TaskReminderType.NoReminder,
				ReminderFrequency=0,
			});
			#endregion
			List<OpenPmsDentalBusiness.Task> listTasksReturned=new List<OpenPmsDentalBusiness.Task>();
			for(int i=0;i<listTasks.Count;i++) {
				OpenPmsDentalBusiness.Crud.TaskCrud.Insert(listTasks[i]);
				SetDateTimeOriginal(listTasks[i],DateTime.Now.AddDays(-i)); //Offset each tasks DateTimeOriginal by -1 day for one per day in the week.
				listTasksReturned.Add(OpenPmsDentalBusiness.Tasks.GetOne(listTasks[i].TaskNum));
			}
			return listTasksReturned;
		}

		///<summary>Updates the DateTimeOriginal. Unable to set the DateTimeOriginal to anything but DateTime.Now in the CRUD Layer.</summary>
		public static void SetDateTimeOriginal(Task Task, DateTime dateTime) {
			string command="UPDATE task SET DateTimeOriginal = "+POut.Date(dateTime)
				+" WHERE TaskNum = "+POut.Long(Task.TaskNum);
			DataCore.NonQ(command);
    }




	}
}
