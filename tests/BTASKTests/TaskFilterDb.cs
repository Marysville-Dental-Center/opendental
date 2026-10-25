// One scratch database for the B-TASK tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the Open Dental
// 25.3.48 schema by the fork migration runner, then filled with a small synthetic clinic, patient, appointment and task set. Skipped
// (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using UnitTestsCore;

namespace BTASKTests {
	[TestClass]
	public class TaskFilterDb {
		public static readonly string DbName=TestDb.ScratchName("btask");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		//The synthetic set. Regions 1 and 2; clinic A in region 1, B in region 2, C with no region.
		public static Def Region1;
		public static Def Region2;
		public static Clinic ClinicA;
		public static Clinic ClinicB;
		public static Clinic ClinicC;
		public static Patient PatA;
		public static Patient PatB;
		public static Patient PatC;
		///<summary>A patient with no clinic.</summary>
		public static Patient Pat0;
		///<summary>The unrestricted user the task queries run for.</summary>
		public static Userod UserAll;
		///<summary>A user restricted to clinic A.</summary>
		public static Userod UserClinicA;
		public static TaskList TaskListMain;
		///<summary>Attached to patient A (clinic A).</summary>
		public static long TaskPatA;
		///<summary>Attached to patient B (clinic B).</summary>
		public static long TaskPatB;
		///<summary>Attached to patient C (clinic C, no region).</summary>
		public static long TaskPatC;
		///<summary>Attached to the patient with no clinic.</summary>
		public static long TaskPat0;
		///<summary>Attached to an appointment of patient A that has no clinic: the appointment's clinic counts, not the patient's.</summary>
		public static long TaskAptPatANoClinic;
		///<summary>Attached to an appointment of the no-clinic patient in clinic B.</summary>
		public static long TaskAptPat0ClinicB;
		///<summary>Attached to nothing: never filtered out.</summary>
		public static long TaskNone;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//Lans.g returns the English text without touching the database only in en-US.
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
			SkipReason=TestDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateFresh24_3(DbName);
			ForkMigrationRunner.Run();
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Security);
			Security.CurUser=Userods.GetFirstOrDefault(x => !x.IsHidden)??new Userod { UserNum=1,UserName="Synthetic" };
			//Clinics on, and the task filter on (default filter by clinic), as an office with several clinics has it.
			Prefs.UpdateBool(PrefName.EasyNoClinics,false);
			Prefs.UpdateInt(PrefName.TasksGlobalFilterType,(int)EnumTaskFilterType.Clinic);
			Cache.Refresh(InvalidType.Prefs);
			CreateSyntheticSet();
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(SkipReason!=null || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		///<summary>Call from [TestInitialize]: skips the test when there is no database, otherwise connects to it.</summary>
		public static void Begin() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			TestDb.Connect(DbName);
		}

		private static void CreateSyntheticSet() {
			Region1=DefT.CreateDefinition(DefCat.Regions,"Synthetic Region 1");
			Region2=DefT.CreateDefinition(DefCat.Regions,"Synthetic Region 2");
			ClinicA=ClinicT.CreateClinic("Synthetic A",regionDef:Region1);
			ClinicB=ClinicT.CreateClinic("Synthetic B",regionDef:Region2);
			ClinicC=ClinicT.CreateClinic("Synthetic C");
			Assert.IsTrue(PrefC.HasClinicsEnabled,"The synthetic office uses clinics.");
			PatA=PatientT.CreatePatient("A",clinicNum:ClinicA.ClinicNum,lName:"Btask",fName:"Synthetic");
			PatB=PatientT.CreatePatient("B",clinicNum:ClinicB.ClinicNum,lName:"Btask",fName:"Synthetic");
			PatC=PatientT.CreatePatient("C",clinicNum:ClinicC.ClinicNum,lName:"Btask",fName:"Synthetic");
			Pat0=PatientT.CreatePatient("Zero",clinicNum:0,lName:"Btask",fName:"Synthetic");
			UserAll=UserodT.CreateUser();
			UserClinicA=UserodT.CreateUser(clinicNum:ClinicA.ClinicNum,isClinicIsRestricted:true);
			UserClinics.Insert(new UserClinic(ClinicA.ClinicNum,UserClinicA.UserNum));
			UserClinics.RefreshCache();
			Appointment aptPatANoClinic=AppointmentT.CreateAppointment(PatA.PatNum,DateTime.Today.AddHours(9),0,0,clinicNum:0);
			Appointment aptPat0ClinicB=AppointmentT.CreateAppointment(Pat0.PatNum,DateTime.Today.AddHours(10),0,0,clinicNum:ClinicB.ClinicNum);
			TaskListMain=TaskListT.CreateTaskList("B-TASK synthetic list");
			DateTime dateTimeEntry=DateTime.Today.AddHours(8);
			TaskPatA=CreateTask(TaskObjectType.Patient,PatA.PatNum,"Patient A",dateTimeEntry);
			TaskPatB=CreateTask(TaskObjectType.Patient,PatB.PatNum,"Patient B",dateTimeEntry.AddMinutes(1));
			TaskPatC=CreateTask(TaskObjectType.Patient,PatC.PatNum,"Patient C",dateTimeEntry.AddMinutes(2));
			TaskPat0=CreateTask(TaskObjectType.Patient,Pat0.PatNum,"Patient with no clinic",dateTimeEntry.AddMinutes(3));
			TaskAptPatANoClinic=CreateTask(TaskObjectType.Appointment,aptPatANoClinic.AptNum,"Appointment of A with no clinic",dateTimeEntry.AddMinutes(4));
			TaskAptPat0ClinicB=CreateTask(TaskObjectType.Appointment,aptPat0ClinicB.AptNum,"Appointment in clinic B",dateTimeEntry.AddMinutes(5));
			TaskNone=CreateTask(TaskObjectType.None,0,"Nothing attached",dateTimeEntry.AddMinutes(6));
		}

		private static long CreateTask(TaskObjectType taskObjectType,long keyNum,string descript,DateTime dateTimeEntry) {
			return TaskT.CreateTask(taskListNum:TaskListMain.TaskListNum,keyNum:keyNum,descript:"Synthetic: "+descript,objectType:taskObjectType,
				dateTimeEntry:dateTimeEntry,userNum:Security.CurUser.UserNum).TaskNum;
		}
	}
}
