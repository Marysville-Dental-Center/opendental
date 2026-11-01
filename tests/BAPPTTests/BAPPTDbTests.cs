// Database tests for package B-APPT on a scratch database (see ScratchDb): Copy View, the fork-owned time bar preference rows, the open-task
// query used when attaching appointments to tasks, and the keys the Waiting Room window needs. Synthetic rows only. Skipped (inconclusive)
// when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BAPPTTests {
	[TestClass]
	public class BAPPTDbTests {
		private static readonly string DB_NAME=ScratchDb.NewName();
		private static string _skipReason;
		///<summary>Clinic numbers no seed row uses, so the test views are the only views in their clinic lists.</summary>
		private const long CLINIC_A=9101;
		private const long CLINIC_B=9102;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				ScratchDb.Create(DB_NAME);
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("BAPPT_TEST_KEEP_DB")=="1") {
				return;
			}
			try {
				ScratchDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//Harmless to leave behind; the name is unique to this run.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_NAME);
			Security.CurUser=new Userod { UserNum=1,UserName="bappttest" };
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		private static long GetLong(string command) {
			return PIn.Long(DataCore.GetScalar(command));
		}

		#region Copy View
		private static ApptView AddView(string description,long clinicNum,int itemOrder) {
			ApptView apptView=new ApptView {
				Description=description,ClinicNum=clinicNum,ItemOrder=itemOrder,RowsPerIncr=2,OnlyScheduledProvs=true,
				OnlySchedBeforeTime=TimeSpan.FromHours(12),OnlySchedAfterTime=TimeSpan.FromHours(7),StackBehavUR=ApptViewStackBehavior.Horizontal,
				StackBehavLR=ApptViewStackBehavior.Vertical,ApptTimeScrollStart=TimeSpan.FromHours(7.5),IsScrollStartDynamic=true,
				IsApptBubblesDisabled=true,WidthOpMinimum=120,WaitingRmName=EnumWaitingRmName.FirstLastI,OnlyScheduledProvDays=true,ShowMirroredAppts=true,
			};
			ApptViews.Insert(apptView);
			return apptView;
		}

		private static void AddItems(long apptViewNum) {
			ApptViewItems.Insert(new ApptViewItem { ApptViewNum=apptViewNum,OpNum=31 });
			ApptViewItems.Insert(new ApptViewItem { ApptViewNum=apptViewNum,ProvNum=1 });
			ApptViewItems.Insert(new ApptViewItem { ApptViewNum=apptViewNum,ElementDesc="PatientName",ElementOrder=0,ElementColor=Color.Navy,ElementAlignment=ApptViewAlignment.Main });
			ApptViewItems.Insert(new ApptViewItem { ApptViewNum=apptViewNum,ElementDesc="Note",ElementOrder=1,ElementColor=Color.Black,ElementAlignment=ApptViewAlignment.Main });
			ApptViewItems.Insert(new ApptViewItem { ApptViewNum=apptViewNum,ElementDesc="ConfirmedColor",ElementOrder=0,ElementColor=Color.Red,ElementAlignment=ApptViewAlignment.UR });
			ApptViewItems.Insert(new ApptViewItem { ApptViewNum=apptViewNum,ElementDesc="Lab",ElementOrder=0,ElementColor=Color.Green,ElementAlignment=ApptViewAlignment.LR });
			ApptViewItems.Insert(new ApptViewItem { ApptViewNum=apptViewNum,ApptFieldDefNum=41,ElementOrder=2,ElementAlignment=ApptViewAlignment.Main });
			ApptViewItems.Insert(new ApptViewItem { ApptViewNum=apptViewNum,PatFieldDefNum=51,ElementOrder=3,ElementAlignment=ApptViewAlignment.Main });
			ApptViewItems.Insert(new ApptViewItem { ApptViewNum=apptViewNum,ElementDesc="PatientName",ElementOrder=0,ElementAlignment=ApptViewAlignment.Main,IsMobile=true });
		}

		private static List<ApptViewItem> GetItems(long apptViewNum) {
			return OpenPmsDentalBusiness.Crud.ApptViewItemCrud.SelectMany("SELECT * FROM apptviewitem WHERE ApptViewNum="+apptViewNum+" ORDER BY ApptViewItemNum");
		}

		private static string DescribeItem(ApptViewItem item) {
			return string.Join(",",item.OpNum,item.ProvNum,item.ElementDesc,item.ElementOrder,item.ElementColor.ToArgb(),item.ElementAlignment,
				item.ApptFieldDefNum,item.PatFieldDefNum,item.IsMobile);
		}

		private static int GetItemOrder(long apptViewNum) {
			return (int)GetLong("SELECT ItemOrder FROM apptview WHERE ApptViewNum="+apptViewNum);
		}

		[TestMethod]
		public void CopyView_CopiesEveryColumnAndItem() {
			ApptView apptViewSource=AddView("Hygiene "+Guid.NewGuid().ToString("N").Substring(0,6),CLINIC_A,0);
			AddItems(apptViewSource.ApptViewNum);
			ApptView apptViewCopy=ApptViews.CopyView(apptViewSource.ApptViewNum);
			Assert.IsNotNull(apptViewCopy);
			Assert.AreNotEqual(apptViewSource.ApptViewNum,apptViewCopy.ApptViewNum);
			ApptView apptViewCopyDb=OpenPmsDentalBusiness.Crud.ApptViewCrud.SelectOne(apptViewCopy.ApptViewNum);
			Assert.AreEqual(apptViewSource.Description+" (copy)",apptViewCopyDb.Description);
			Assert.AreEqual(apptViewSource.ItemOrder+1,apptViewCopyDb.ItemOrder);
			//Every other column is the source's.
			Assert.AreEqual(apptViewSource.ClinicNum,apptViewCopyDb.ClinicNum);
			Assert.AreEqual(apptViewSource.RowsPerIncr,apptViewCopyDb.RowsPerIncr);
			Assert.AreEqual(apptViewSource.OnlyScheduledProvs,apptViewCopyDb.OnlyScheduledProvs);
			Assert.AreEqual(apptViewSource.OnlySchedBeforeTime,apptViewCopyDb.OnlySchedBeforeTime);
			Assert.AreEqual(apptViewSource.OnlySchedAfterTime,apptViewCopyDb.OnlySchedAfterTime);
			Assert.AreEqual(apptViewSource.StackBehavUR,apptViewCopyDb.StackBehavUR);
			Assert.AreEqual(apptViewSource.StackBehavLR,apptViewCopyDb.StackBehavLR);
			Assert.AreEqual(apptViewSource.ApptTimeScrollStart,apptViewCopyDb.ApptTimeScrollStart);
			Assert.AreEqual(apptViewSource.IsScrollStartDynamic,apptViewCopyDb.IsScrollStartDynamic);
			Assert.AreEqual(apptViewSource.IsApptBubblesDisabled,apptViewCopyDb.IsApptBubblesDisabled);
			Assert.AreEqual(apptViewSource.WidthOpMinimum,apptViewCopyDb.WidthOpMinimum);
			Assert.AreEqual(apptViewSource.WaitingRmName,apptViewCopyDb.WaitingRmName);
			Assert.AreEqual(apptViewSource.OnlyScheduledProvDays,apptViewCopyDb.OnlyScheduledProvDays);
			Assert.AreEqual(apptViewSource.ShowMirroredAppts,apptViewCopyDb.ShowMirroredAppts);
			//Every item, including the IsMobile rows, with the copy's key.
			List<ApptViewItem> listItemsSource=GetItems(apptViewSource.ApptViewNum);
			List<ApptViewItem> listItemsCopy=GetItems(apptViewCopy.ApptViewNum);
			Assert.AreEqual(9,listItemsSource.Count);
			CollectionAssert.AreEqual(listItemsSource.Select(DescribeItem).ToList(),listItemsCopy.Select(DescribeItem).ToList());
			Assert.IsTrue(listItemsCopy.All(x => x.ApptViewNum==apptViewCopy.ApptViewNum));
			Assert.AreEqual(9,listItemsSource.Count(x => x.ApptViewNum==apptViewSource.ApptViewNum),"The source keeps its items.");
		}

		[TestMethod]
		public void CopyView_GoesRightAfterTheSource_InItsClinicOnly() {
			string tag=Guid.NewGuid().ToString("N").Substring(0,6);
			ApptView apptViewFirst=AddView("First "+tag,CLINIC_B,0);
			ApptView apptViewSecond=AddView("Second "+tag,CLINIC_B,1);
			ApptView apptViewThird=AddView("Third "+tag,CLINIC_B,2);
			ApptView apptViewOtherClinic=AddView("Other "+tag,CLINIC_B+1,2);
			ApptView apptViewCopy=ApptViews.CopyView(apptViewSecond.ApptViewNum);
			Assert.AreEqual(0,GetItemOrder(apptViewFirst.ApptViewNum));
			Assert.AreEqual(1,GetItemOrder(apptViewSecond.ApptViewNum));
			Assert.AreEqual(2,GetItemOrder(apptViewCopy.ApptViewNum));
			Assert.AreEqual(3,GetItemOrder(apptViewThird.ApptViewNum),"The views after the source move down one.");
			Assert.AreEqual(2,GetItemOrder(apptViewOtherClinic.ApptViewNum),"Another clinic's list is not touched.");
			ApptViews.RefreshCache();
			List<string> listDescriptions=ApptViews.GetWhere(x => x.ClinicNum==CLINIC_B).OrderBy(x => x.ItemOrder).Select(x => x.Description).ToList();
			CollectionAssert.AreEqual(new[] { "First "+tag,"Second "+tag,"Second "+tag+" (copy)","Third "+tag },listDescriptions);
		}

		[TestMethod]
		public void CopyView_DoesNotCopyRememberedViews() {
			ApptView apptViewSource=AddView("Remembered "+Guid.NewGuid().ToString("N").Substring(0,6),CLINIC_A+10,0);
			DataCore.NonQ("INSERT INTO userodapptview (UserNum,ClinicNum,ApptViewNum) VALUES (1,"+(CLINIC_A+10)+","+apptViewSource.ApptViewNum+")");
			ApptView apptViewCopy=ApptViews.CopyView(apptViewSource.ApptViewNum);
			Assert.AreEqual(1,GetLong("SELECT COUNT(*) FROM userodapptview WHERE ApptViewNum="+apptViewSource.ApptViewNum));
			Assert.AreEqual(0,GetLong("SELECT COUNT(*) FROM userodapptview WHERE ApptViewNum="+apptViewCopy.ApptViewNum));
		}

		[TestMethod]
		public void CopyView_MissingSourceGivesNull() {
			long countBefore=GetLong("SELECT COUNT(*) FROM apptview");
			Assert.IsNull(ApptViews.CopyView(987654321));
			Assert.AreEqual(countBefore,GetLong("SELECT COUNT(*) FROM apptview"));
		}

		[TestMethod]
		public void CopyDescription_FitsTheColumn() {
			string description=new string('v',255);
			string descriptionCopy=ApptViews.GetCopyDescription(description);
			Assert.AreEqual(255,descriptionCopy.Length);
			StringAssert.EndsWith(descriptionCopy," (copy)");
			Assert.AreEqual(" (copy)",ApptViews.GetCopyDescription(null));
			ApptView apptViewSource=AddView(description,CLINIC_A+20,0);
			ApptView apptViewCopy=ApptViews.CopyView(apptViewSource.ApptViewNum);
			Assert.AreEqual(descriptionCopy,DataCore.GetScalar("SELECT Description FROM apptview WHERE ApptViewNum="+apptViewCopy.ApptViewNum));
		}

		[TestMethod]
		public void CopyLogText() {
			Assert.AreEqual("Appointment view 'Hyg' copied to 'Hyg (copy)'",ApptViews.GetCopyLogText("Hyg","Hyg (copy)"));
		}
		#endregion Copy View

		#region Time bar preference rows
		private static string PrefRowValue(string prefName) {
			DataTable table=DataCore.GetTable("SELECT ValueString FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
			return table.Rows.Count==0 ? null : table.Rows[0][0].ToString();
		}

		[TestMethod]
		public void TimeBarPrefs_MissingReadsOff_AndTurningOnInsertsTheRow() {
			foreach(string prefName in new[] { ApptTimeBars.PREF_PROV_TIME_ON_OP_BACKGROUND,ApptTimeBars.PREF_SECONDARY_COLOR_NO_PROV_TIME }) {
				DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+prefName+"'");
				Prefs.RefreshCache();
				Assert.IsNull(PrefRowValue(prefName),"No migration step adds the row.");
				Assert.IsFalse(ApptTimeBars.GetBoolPref(prefName),"A missing row reads as off.");
				Assert.IsFalse(ApptTimeBars.UpdateBoolPref(prefName,false),"Off is already off: nothing written.");
				Assert.IsNull(PrefRowValue(prefName));
				Assert.IsTrue(ApptTimeBars.UpdateBoolPref(prefName,true));
				Assert.AreEqual("1",PrefRowValue(prefName));
				Assert.IsTrue(ApptTimeBars.GetBoolPref(prefName),"The local cache is updated.");
				Assert.IsFalse(ApptTimeBars.UpdateBoolPref(prefName,true),"No change.");
				Assert.IsTrue(ApptTimeBars.UpdateBoolPref(prefName,false));
				Assert.AreEqual("0",PrefRowValue(prefName));
				Assert.AreEqual(1,GetLong("SELECT COUNT(*) FROM preference WHERE PrefName='"+prefName+"'"),"Never a duplicate row.");
				DataCore.NonQ("UPDATE preference SET ValueString='1' WHERE PrefName='"+prefName+"'");
				Prefs.RefreshCache();
				Assert.IsTrue(ApptTimeBars.GetBoolPref(prefName),"Another workstation's change arrives with the Prefs cache.");
				DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+prefName+"'");
				Prefs.RefreshCache();
			}
			Assert.IsFalse(ApptTimeBars.IsProvTimeOnOpBackground());
			Assert.IsFalse(ApptTimeBars.IsSecondaryColorNoProvTime());
		}

		[TestMethod]
		public void TimeBarPrefs_OnlyTheForkNamesCanBeWritten() {
			try {
				ApptTimeBars.UpsertPrefRaw("DataBaseVersion","1.0");
				Assert.Fail("Expected an ArgumentException.");
			}
			catch(ArgumentException) {
			}
			Assert.AreNotEqual("1.0",PrefRowValue("DataBaseVersion"));
		}
		#endregion Time bar preference rows

		#region Appointment tasks
		private static void AddTask(long aptNum,TaskStatusEnum taskStatus,TaskObjectType taskObjectType=TaskObjectType.Appointment) {
			Task task=new Task { Descript="Synthetic task",DateTimeEntry=DateTime.Now,UserNum=1,ObjectType=taskObjectType,KeyNum=aptNum,TaskStatus=taskStatus };
			Tasks.Insert(task);
		}

		[TestMethod]
		public void OpenTaskQuery_IgnoresCompletedTasks() {
			long aptNumOpen=990001;
			long aptNumDone=990002;
			long aptNumMixed=990003;
			long aptNumNone=990004;
			long aptNumPatientTask=990005;
			AddTask(aptNumOpen,TaskStatusEnum.New);
			AddTask(aptNumDone,TaskStatusEnum.Done);
			AddTask(aptNumDone,TaskStatusEnum.Done);
			AddTask(aptNumMixed,TaskStatusEnum.Done);
			AddTask(aptNumMixed,TaskStatusEnum.Viewed);
			AddTask(aptNumPatientTask,TaskStatusEnum.New,TaskObjectType.Patient);//KeyNum is a PatNum here, not an AptNum.
			List<long> listAptNums=ApptTasks.GetApptNumsWithOpenTask(new List<long> { aptNumOpen,aptNumDone,aptNumMixed,aptNumNone,aptNumPatientTask,aptNumOpen });
			CollectionAssert.AreEquivalent(new List<long> { aptNumOpen,aptNumMixed },listAptNums);
			Assert.AreEqual(0,ApptTasks.GetApptNumsWithOpenTask(new List<long>()).Count);
			Assert.AreEqual(0,ApptTasks.GetApptNumsWithOpenTask(null).Count);
			//24.3 blocked every appointment with any task; the old query still says so.
			CollectionAssert.IsSubsetOf(new List<long> { aptNumDone },Appointments.GetApptNumsAttachedToTask(new List<long> { aptNumDone }));
		}

		[TestMethod]
		public void TaskButton_FromTheDatabase() {
			long aptNum=990101;
			AddTask(aptNum,TaskStatusEnum.Done);
			Assert.AreEqual(ApptTaskButtonAction.NewTask,ApptTasks.GetTaskButtonAction(false,Tasks.GetMany(aptNum),out _),"Only a completed task: start a new one.");
			AddTask(aptNum,TaskStatusEnum.New);
			Assert.AreEqual(ApptTaskButtonAction.OpenTask,ApptTasks.GetTaskButtonAction(false,Tasks.GetMany(aptNum),out Task taskOpen));
			Assert.AreEqual(TaskStatusEnum.New,taskOpen.TaskStatus);
		}
		#endregion Appointment tasks

		#region Waiting room table
		[TestMethod]
		public void WaitingRoomTable_CarriesAptNumAndPatNum() {
			DateTime dateTimeNow=DateTime.Now;
			if(dateTimeNow.TimeOfDay<TimeSpan.FromMinutes(2) || dateTimeNow.TimeOfDay>new TimeSpan(23,58,0)) {
				Assert.Inconclusive("Too close to midnight for a same-day waiting room row.");
			}
			Patient patient=new Patient { LName="Waiting",FName="Synthetic",PatStatus=PatientStatus.Patient,Birthdate=new DateTime(1985,5,5) };
			Patients.Insert(patient,false);
			Appointment appointment=new Appointment { PatNum=patient.PatNum,AptStatus=ApptStatus.Scheduled,AptDateTime=dateTimeNow.AddMinutes(-1),Op=77,
				Pattern="XXXX",ProvNum=1,ProcDescript="",Note="",ProvBarText="",ProcsColored="",ColorOverride=Color.Empty,
				DateTimeArrived=dateTimeNow.AddMinutes(-1) };
			Appointments.Insert(appointment);
			DataTable table=Appointments.GetPeriodWaitingRoomTable(dateTimeNow);
			Assert.IsTrue(table.Columns.Contains("AptNum"));
			Assert.IsTrue(table.Columns.Contains("PatNum"));
			DataRow dataRow=table.Select().FirstOrDefault(x => x["AptNum"].ToString()==appointment.AptNum.ToString());
			Assert.IsNotNull(dataRow,"The arrived, unseated appointment is in the waiting room.");
			Assert.AreEqual(patient.PatNum.ToString(),dataRow["PatNum"].ToString());
			Assert.AreEqual("77",dataRow["OpNum"].ToString());
			List<ApptWaitingRoomRow> listRows=ApptWaitingRoom.GetRows(table,TimeSpan.Zero,new ApptWaitingRoomFilter { NameFormat=EnumWaitingRmName.LastFirst });
			ApptWaitingRoomRow row=listRows.Single(x => x.AptNum==appointment.AptNum);
			Assert.AreEqual("Waiting, Synthetic",row.PatName);
			Assert.AreEqual(patient.PatNum,row.PatNum);
		}
		#endregion Waiting room table
	}
}
