// v26_1-tools-dbm-duplicate-blockouts: the Database Maintenance check SchedulesDeleteDuplicateBlockouts, which replaces the Clear Duplicate
// Blockouts tool, against a scratch database with synthetic schedules.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2SHELLTests {
	[TestClass]
	public class DuplicateBlockoutDbTests {
		private const long BLOCKOUT_TYPE_LUNCH=11;
		private const long BLOCKOUT_TYPE_MEETING=12;

		[TestInitialize]
		public void TestInitialize() {
			string skipReason=ScratchDb.EnsureCreated();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			ScratchDb.Reset("schedule","scheduleop","signalod","securitylog","securityloghash");
		}

		///<summary>Inserts a schedule row linked to the operatories given, in that order. Returns its ScheduleNum.</summary>
		private static long AddSchedule(DateTime date,string start,string stop,long blockoutType,string note,ScheduleType scheduleType,params long[] arrayOpNums) {
			string noteSql=note==null ? "NULL" : "'"+POut.String(note)+"'";
			DataCore.NonQ("INSERT INTO schedule (SchedDate,StartTime,StopTime,SchedType,ProvNum,BlockoutType,Note,Status,EmployeeNum,ClinicNum) VALUES("
				+POut.Date(date)+",'"+start+"','"+stop+"',"+(int)scheduleType+",0,"+blockoutType+","+noteSql+",0,0,0)");
			long scheduleNum=ScratchDb.GetLong("SELECT MAX(ScheduleNum) FROM schedule");
			foreach(long opNum in arrayOpNums) {
				DataCore.NonQ("INSERT INTO scheduleop (ScheduleNum,OperatoryNum) VALUES("+scheduleNum+","+opNum+")");
			}
			return scheduleNum;
		}

		private static long AddBlockout(DateTime date,string note,params long[] arrayOpNums) {
			return AddSchedule(date,"12:00:00","13:00:00",BLOCKOUT_TYPE_LUNCH,note,ScheduleType.Blockout,arrayOpNums);
		}

		private static List<long> GetOps(long scheduleNum) {
			return DataCore.GetTable("SELECT OperatoryNum FROM scheduleop WHERE ScheduleNum="+scheduleNum+" ORDER BY OperatoryNum").Select()
				.Select(x => Convert.ToInt64(x[0])).ToList();
		}

		private static bool Exists(long scheduleNum) {
			return ScratchDb.GetLong("SELECT COUNT(*) FROM schedule WHERE ScheduleNum="+scheduleNum)==1;
		}

		private static string RunDbm(DbmMode dbmMode,bool verbose=false) {
			return DatabaseMaintenances.SchedulesDeleteDuplicateBlockouts(verbose,dbmMode);
		}

		[TestMethod]
		public void PublishedExample_KeepsTheFirstLinkOfEachGroup() {
			//Identical blockouts A, B and C: A on operatories 1, 3 and 5, B on 1, 2 and 3, C on 3. Duplicates: (A1,B1) and (A3,B3,C3).
			DateTime date=DateTime.Today;
			long a=AddBlockout(date,"Lunch",1,3,5);
			long b=AddBlockout(date,"Lunch",1,2,3);
			long c=AddBlockout(date,"Lunch",3);
			Assert.AreEqual(3,Schedules.GetDuplicateBlockoutCount());
			Assert.AreEqual("Duplicate blockouts found: 3\r\n",RunDbm(DbmMode.Check));
			Assert.AreEqual("Duplicate blockouts removed: 3\r\n",RunDbm(DbmMode.Fix));
			CollectionAssert.AreEqual(new long[] { 1,3,5 },GetOps(a));
			CollectionAssert.AreEqual(new long[] { 2 },GetOps(b));
			Assert.IsFalse(Exists(c),"C lost its only operatory, so the blockout itself is deleted.");
			Assert.IsTrue(Exists(a) && Exists(b));
			Assert.AreEqual(0,Schedules.GetDuplicateBlockoutCount());
			Assert.IsTrue(ScratchDb.GetLong("SELECT COUNT(*) FROM signalod WHERE IType="+(int)InvalidType.Schedules)>0,
				"Workstations showing today are told to refresh.");
			//Combined review (2026-11-01): the fix deletes blockout links and blockouts, so it leaves an Audit Trail entry like the removed tool did.
			CollectionAssert.AreEqual(new[] { (int)EnumPermType.Setup+"/0/1/"+(int)LogSources.DBM+"/Database Maintenance removed 3 duplicate blockout link(s)." },
				GetAuditEntries());
			Assert.AreEqual(1,ScratchDb.GetLong("SELECT COUNT(*) FROM securityloghash"),"The entry is hashed like other audit entries.");
			Assert.AreEqual("",RunDbm(DbmMode.Fix),"A second run finds nothing.");
			Assert.AreEqual(1,GetAuditEntries().Count,"A run that removes nothing writes no entry.");
		}

		///<summary>Every security log entry as "PermType/PatNum/UserNum/LogSource/LogText", oldest first.</summary>
		private static List<string> GetAuditEntries() {
			return DataCore.GetTable("SELECT PermType,PatNum,UserNum,LogSource,LogText FROM securitylog ORDER BY SecurityLogNum").Select()
				.Select(x => x[0]+"/"+x[1]+"/"+x[2]+"/"+x[3]+"/"+x[4]).ToList();
		}

		[TestMethod]
		public void NoDuplicates_CheckIsQuiet_FixIsValidSql() {
			AddBlockout(DateTime.Today,"Lunch",1);
			AddBlockout(DateTime.Today,"Lunch",2);
			Assert.AreEqual("",RunDbm(DbmMode.Check));
			Assert.AreEqual("",RunDbm(DbmMode.Fix),"With no duplicates the fix must not build an empty IN () list.");
			Assert.AreEqual("Duplicate blockouts found: 0\r\n",RunDbm(DbmMode.Check,verbose:true));
			Assert.AreEqual("Duplicate blockouts removed: 0\r\n",RunDbm(DbmMode.Fix,verbose:true));
			Assert.AreEqual(2,ScratchDb.GetLong("SELECT COUNT(*) FROM scheduleop"));
			Assert.AreEqual(0,GetAuditEntries().Count,"Nothing removed, so no Audit Trail entry, not even in verbose mode.");
		}

		[TestMethod]
		public void OnlyExactMatchesAreDuplicates() {
			DateTime date=new DateTime(2026,11,2);
			long first=AddBlockout(date,"Lunch",1);
			long otherNote=AddBlockout(date,"Staff meeting",1);
			long otherDate=AddBlockout(date.AddDays(1),"Lunch",1);
			long otherTime=AddSchedule(date,"12:00:00","13:30:00",BLOCKOUT_TYPE_LUNCH,"Lunch",ScheduleType.Blockout,1);
			long otherType=AddSchedule(date,"12:00:00","13:00:00",BLOCKOUT_TYPE_MEETING,"Lunch",ScheduleType.Blockout,1);
			long provider=AddSchedule(date,"12:00:00","13:00:00",BLOCKOUT_TYPE_LUNCH,"Lunch",ScheduleType.Provider,1);
			Assert.AreEqual(0,Schedules.GetDuplicateBlockoutCount());
			RunDbm(DbmMode.Fix);
			foreach(long scheduleNum in new[] { first,otherNote,otherDate,otherTime,otherType,provider }) {
				Assert.IsTrue(Exists(scheduleNum));
				CollectionAssert.AreEqual(new long[] { 1 },GetOps(scheduleNum));
			}
		}

		[TestMethod]
		public void NullNotes_MatchEachOther() {
			DateTime date=new DateTime(2026,11,3);
			long first=AddBlockout(date,null,4);
			long second=AddBlockout(date,null,4);
			long blank=AddBlockout(date,"",4);//Blank is not NULL: Open Dental's grouping keeps them apart as well.
			Assert.AreEqual(1,Schedules.GetDuplicateBlockoutCount());
			Assert.AreEqual(1,Schedules.ClearDuplicates());
			Assert.IsTrue(Exists(first));
			Assert.IsFalse(Exists(second));
			Assert.IsTrue(Exists(blank));
		}

		[TestMethod]
		public void BlockoutWithNoOperatoryBeforeTheRun_IsLeftAlone() {
			DateTime date=new DateTime(2026,11,4);
			long orphan=AddBlockout(date,"Not linked to any operatory");
			AddBlockout(date,"Lunch",1);
			long duplicate=AddBlockout(date,"Lunch",1);
			Assert.AreEqual(1,Schedules.ClearDuplicates());
			Assert.IsFalse(Exists(duplicate));
			Assert.IsTrue(Exists(orphan),"Only blockouts this fix emptied are deleted.");
		}

		[TestMethod]
		public void LargeGroup_IsNotCutOff() {
			//The 24.3 query listed each group with GROUP_CONCAT, which a server cuts at group_concat_max_len (1024 bytes by default).
			DateTime date=new DateTime(2026,11,5);
			long first=AddBlockout(date,"Converted",9);
			for(int i=0;i<300;i++) {
				AddBlockout(date,"Converted",9);
			}
			Assert.AreEqual(300,Schedules.GetDuplicateBlockoutCount());
			Assert.AreEqual(300,Schedules.ClearDuplicates());
			Assert.AreEqual(1,ScratchDb.GetLong("SELECT COUNT(*) FROM schedule"));
			Assert.IsTrue(Exists(first));
		}

		[TestMethod]
		public void DbmMethod_IsListedLikeTheOthers() {
			MethodInfo methodInfo=typeof(DatabaseMaintenances).GetMethod(nameof(DatabaseMaintenances.SchedulesDeleteDuplicateBlockouts));
			DbmMethodAttr dbmMethodAttr=(DbmMethodAttr)Attribute.GetCustomAttribute(methodInfo,typeof(DbmMethodAttr));
			Assert.IsNotNull(dbmMethodAttr);
			Assert.IsFalse(dbmMethodAttr.HasPatNum || dbmMethodAttr.IsPatDependent || dbmMethodAttr.IsCanada || dbmMethodAttr.IsOneOff);
			CollectionAssert.AreEqual(new[] { typeof(bool),typeof(DbmMode) },methodInfo.GetParameters().Select(x => x.ParameterType).ToArray());
			Assert.AreEqual(typeof(string),methodInfo.ReturnType);
		}
	}
}
