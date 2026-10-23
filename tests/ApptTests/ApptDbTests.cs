// Database tests for package A-APPT on a scratch database (see ScratchDb): the blockout clears, copies and audit entries that follow the
// flagged-blockout permission, and the appointment rows that carry the mirror flag. Synthetic rows only. Skipped (inconclusive) when the local
// MariaDB does not answer.
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
using OpenDentBusiness;

namespace ApptTests {
	[TestClass]
	public class ApptDbTests {
		private static readonly string DB_NAME=ScratchDb.NewName();
		private static string _skipReason;
		//A date far from anything in the seed dump, so these rows are the only blockouts and appointments on it.
		private static readonly DateTime _date=new DateTime(2031,3,4);
		private static long _defNumUnflagged;
		private static long _defNumNoSched;
		private static long _defNumDontCopy;
		private static long _opNumA;
		private static long _opNumB;

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
				return;
			}
			_defNumUnflagged=Defs.Insert(new Def { Category=DefCat.BlockoutTypes,ItemOrder=90,ItemName="ApptTest Lunch",ItemValue="",ItemColor=Color.Gray });
			_defNumNoSched=Defs.Insert(new Def { Category=DefCat.BlockoutTypes,ItemOrder=91,ItemName="ApptTest Meeting",ItemValue="NS",ItemColor=Color.Gray });
			_defNumDontCopy=Defs.Insert(new Def { Category=DefCat.BlockoutTypes,ItemOrder=92,ItemName="ApptTest Hold",ItemValue="DC",ItemColor=Color.Gray });
			_opNumA=Operatories.Insert(new Operatory { OpName="ApptTest A",Abbrev="ATA",ItemOrder=90 });
			_opNumB=Operatories.Insert(new Operatory { OpName="ApptTest B",Abbrev="ATB",ItemOrder=91 });
			Cache.Refresh(InvalidType.Defs,InvalidType.Operatories);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("APPT_TEST_KEEP_DB")=="1") {
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
			Security.CurUser=new Userod { UserNum=1,UserName="appttest" };
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			DataCore.NonQ("DELETE scheduleop FROM scheduleop INNER JOIN schedule ON schedule.ScheduleNum=scheduleop.ScheduleNum "
				+"WHERE schedule.SchedDate>="+POut.Date(_date));
			DataCore.NonQ("DELETE FROM schedule WHERE SchedDate>="+POut.Date(_date));
			DataCore.NonQ("DELETE FROM securitylog");
		}

		private static Schedule AddBlockout(long defNum,long opNum,int hour,DateTime? date=null) {
			Schedule schedule=new Schedule { SchedDate=date??_date,StartTime=TimeSpan.FromHours(hour),StopTime=TimeSpan.FromHours(hour+1),
				SchedType=ScheduleType.Blockout,BlockoutType=defNum,Note="",Ops=new List<long> { opNum } };
			Schedules.Insert(schedule,false,false);
			return schedule;
		}

		///<summary>Unflagged at 8 and NS-flagged at 10 in op A; DC-flagged at 12 in op B.</summary>
		private static void AddThreeBlockouts() {
			AddBlockout(_defNumUnflagged,_opNumA,8);
			AddBlockout(_defNumNoSched,_opNumA,10);
			AddBlockout(_defNumDontCopy,_opNumB,12);
		}

		///<summary>Blockout types left on the date, as "type@op" sorted.</summary>
		private static string Remaining(DateTime? date=null) {
			DataTable table=DataCore.GetTable("SELECT schedule.BlockoutType,scheduleop.OperatoryNum FROM schedule "
				+"INNER JOIN scheduleop ON scheduleop.ScheduleNum=schedule.ScheduleNum "
				+"WHERE schedule.SchedType="+POut.Int((int)ScheduleType.Blockout)+" AND schedule.SchedDate="+POut.Date(date??_date));
			return string.Join(",",table.Select().Select(x => Name(PIn.Long(x[0].ToString()))+"@"+(PIn.Long(x[1].ToString())==_opNumA ? "A" : "B"))
				.OrderBy(x => x));
		}

		private static string Name(long defNum) {
			if(defNum==_defNumUnflagged) {
				return "Lunch";
			}
			if(defNum==_defNumNoSched) {
				return "NS";
			}
			if(defNum==_defNumDontCopy) {
				return "DC";
			}
			return "other";
		}

		private static List<int> LoggedPermTypes() {
			return DataCore.GetTable("SELECT PermType FROM securitylog ORDER BY SecurityLogNum").Select().Select(x => PIn.Int(x[0].ToString())).ToList();
		}

		#region Blockout types from the definitions cache
		[TestMethod]
		public void Blockout_FlagAndPermissionFromDefinition() {
			Assert.IsFalse(Schedules.IsBlockoutTypeFlagged(_defNumUnflagged));
			Assert.IsTrue(Schedules.IsBlockoutTypeFlagged(_defNumNoSched));
			Assert.IsTrue(Schedules.IsBlockoutTypeFlagged(_defNumDontCopy));
			Assert.IsFalse(Schedules.IsBlockoutTypeFlagged(987654321),"Unknown type counts as unflagged.");
			Assert.AreEqual(EnumPermType.Blockouts,Schedules.GetBlockoutPermType(_defNumUnflagged));
			Assert.AreEqual(EnumPermType.BlockoutsFlagged,Schedules.GetBlockoutPermType(_defNumNoSched));
			Assert.IsTrue(Schedules.IsBlockoutIncluded(_defNumUnflagged,true,false));
			Assert.IsFalse(Schedules.IsBlockoutIncluded(_defNumNoSched,true,false));
			Assert.IsTrue(Schedules.IsBlockoutIncluded(_defNumDontCopy,false,true));
			Assert.IsFalse(Schedules.IsBlockoutIncluded(_defNumUnflagged,false,true));
		}
		#endregion Blockout types from the definitions cache

		#region Clears
		[TestMethod]
		public void ClearDay_UnflaggedOnly_LeavesFlagged() {
			AddThreeBlockouts();
			Assert.AreEqual(2,Schedules.ClearBlockoutsForDay(_date,includeUnflagged:true,includeFlagged:false));
			Assert.AreEqual("DC@B,NS@A",Remaining());
		}

		[TestMethod]
		public void ClearDay_FlaggedOnly_LeavesUnflagged() {
			AddThreeBlockouts();
			Assert.AreEqual(1,Schedules.ClearBlockoutsForDay(_date,includeUnflagged:false,includeFlagged:true));
			Assert.AreEqual("Lunch@A",Remaining());
		}

		[TestMethod]
		public void ClearDay_Both_ClearsAll() {
			AddThreeBlockouts();
			Assert.AreEqual(0,Schedules.ClearBlockoutsForDay(_date));
			Assert.AreEqual("",Remaining());
		}

		[TestMethod]
		public void ClearOp_FlaggedOnly_OtherOpUntouched() {
			AddThreeBlockouts();
			Assert.AreEqual(1,Schedules.ClearBlockoutsForOp(_opNumA,_date,includeUnflagged:false,includeFlagged:true));
			Assert.AreEqual("DC@B,Lunch@A",Remaining());
		}

		[TestMethod]
		public void ClearClinic_UnflaggedOnly() {
			AddThreeBlockouts();
			Assert.AreEqual(2,Schedules.ClearBlockoutsForClinic(0,_date,includeUnflagged:true,includeFlagged:false));
			Assert.AreEqual("DC@B,NS@A",Remaining());
		}
		#endregion Clears

		#region Copy and paste of days
		[TestMethod]
		public void CopyDay_WithoutFlaggedPermission_CopiesOnlyUnflagged() {
			AddThreeBlockouts();
			DateTime dateTo=_date.AddDays(1);
			string errors=Schedules.CopyBlockouts(0,false,true,false,_date,_date,dateTo,dateTo,1,includeUnflagged:true,includeFlagged:false);
			Assert.AreEqual("",errors);
			Assert.AreEqual("Lunch@A",Remaining(dateTo));
			CollectionAssert.AreEqual(new List<int> { (int)EnumPermType.Blockouts },LoggedPermTypes());
		}

		[TestMethod]
		public void CopyDay_WithBothPermissions_CopiesNoSchedButNeverDontCopy() {
			AddThreeBlockouts();
			DateTime dateTo=_date.AddDays(1);
			Assert.AreEqual("",Schedules.CopyBlockouts(0,false,true,false,_date,_date,dateTo,dateTo,1));
			Assert.AreEqual("Lunch@A,NS@A",Remaining(dateTo));
			CollectionAssert.AreEqual(new List<int> { (int)EnumPermType.Blockouts,(int)EnumPermType.BlockoutsFlagged },LoggedPermTypes(),
				"One paste entry per kind, each under its own permission.");
		}

		[TestMethod]
		public void CopyDay_ReplaceWithoutUnflaggedPermission_KeepsUnflaggedTarget() {
			AddBlockout(_defNumNoSched,_opNumA,10);
			DateTime dateTo=_date.AddDays(1);
			AddBlockout(_defNumUnflagged,_opNumB,15,dateTo);
			AddBlockout(_defNumNoSched,_opNumB,16,dateTo);
			Assert.AreEqual("",Schedules.CopyBlockouts(0,false,true,true,_date,_date,dateTo,dateTo,1,includeUnflagged:false,includeFlagged:true));
			//The unflagged blockout on the target day stays; the flagged one there is replaced by the pasted one.
			Assert.AreEqual("Lunch@B,NS@A",Remaining(dateTo));
		}
		#endregion Copy and paste of days

		#region Audit entries
		[TestMethod]
		public void Log_FlaggedBlockoutUsesBlockoutsFlagged() {
			Schedule scheduleFlagged=AddBlockout(_defNumNoSched,_opNumA,10);
			Schedule scheduleUnflagged=AddBlockout(_defNumUnflagged,_opNumA,8);
			Schedules.BlockoutLogHelper(BlockoutAction.Create,scheduleFlagged);
			Schedules.BlockoutLogHelper(BlockoutAction.Delete,scheduleUnflagged);
			Schedules.BlockoutLogHelper(BlockoutAction.Clear,dateTime:_date,permTypeClear:EnumPermType.BlockoutsFlagged);
			Schedules.BlockoutLogHelper(BlockoutAction.Clear,dateTime:_date);
			CollectionAssert.AreEqual(new List<int> { 266,13,266,13 },LoggedPermTypes());
		}
		#endregion Audit entries

		#region Appointments
		private static long AddPatient() {
			Patient patient=new Patient { LName="Mirror",FName="Synthetic",PatStatus=PatientStatus.Patient,Birthdate=new DateTime(1990,1,1) };
			return Patients.Insert(patient,false);
		}

		private static Appointment AddAppt(long patNum,bool isMirrored,int hour) {
			Appointment appointment=new Appointment { PatNum=patNum,AptStatus=ApptStatus.Scheduled,AptDateTime=_date.AddHours(hour),Op=_opNumA,
				Pattern="XXXX",PatternSecondary="//XX",ProvNum=1,IsMirrored=isMirrored,ProcDescript="",Note="",ProvBarText="",ProcsColored="",
				ColorOverride=Color.Empty };
			Appointments.Insert(appointment);
			return appointment;
		}

		[TestMethod]
		public void PeriodTable_CarriesIsMirrored() {
			long patNum=AddPatient();
			Appointment appointmentMirrored=AddAppt(patNum,true,9);
			Appointment appointmentPlain=AddAppt(patNum,false,11);
			DataTable table=Appointments.GetPeriodApptsTable(_date,_date,0,false,null,new List<long> { _opNumA });
			Assert.IsTrue(table.Columns.Contains("IsMirrored"));
			DataRow dataRowMirrored=table.Select().First(x => PIn.Long(x["AptNum"].ToString())==appointmentMirrored.AptNum);
			DataRow dataRowPlain=table.Select().First(x => PIn.Long(x["AptNum"].ToString())==appointmentPlain.AptNum);
			Assert.IsTrue(PIn.Bool(dataRowMirrored["IsMirrored"].ToString()));
			Assert.IsFalse(PIn.Bool(dataRowPlain["IsMirrored"].ToString()));
			Assert.AreEqual("//XX",dataRowMirrored["PatternSecondary"].ToString());
		}

		[TestMethod]
		public void History_RowCopiesIsMirrored() {
			Appointment appointment=AddAppt(AddPatient(),true,13);
			HistAppointment histAppointment=HistAppointments.CreateHistoryEntry(appointment,HistAppointmentAction.Changed);
			Assert.IsNotNull(histAppointment);
			Assert.AreEqual("1",DataCore.GetScalar("SELECT IsMirrored FROM histappointment WHERE HistApptNum="+POut.Long(histAppointment.HistApptNum)));
		}

		[TestMethod]
		public void NewAppointment_TakesTheMirrorPreference() {
			try {
				Prefs.UpdateBool(PrefName.ApptMirrorSecondary,true);
				Assert.IsTrue(Appointments.MakeNewAppointment(null,DateTime.MinValue,0,false).IsMirrored);
				Prefs.UpdateBool(PrefName.ApptMirrorSecondary,false);
				Assert.IsFalse(Appointments.MakeNewAppointment(null,DateTime.MinValue,0,false).IsMirrored);
			}
			finally {
				Prefs.UpdateBool(PrefName.ApptMirrorSecondary,false);
			}
		}

		[TestMethod]
		public void ChangeSummary_UsesCachedNames() {
			Appointment appointmentOld=new Appointment { Op=_opNumA,AptDateTime=_date.AddHours(9),Pattern="XX" };
			Appointment appointmentNew=new Appointment { Op=_opNumB,AptDateTime=_date.AddHours(9),Pattern="XX" };
			Assert.AreEqual("Operatory: ATA → ATB",Appointments.GetChangeSummary(appointmentOld,appointmentNew));
			Assert.AreEqual("Operatory: ATA → ATB",Appointments.GetMoveSummary(appointmentOld,appointmentNew));
		}
		#endregion Appointments
	}
}
