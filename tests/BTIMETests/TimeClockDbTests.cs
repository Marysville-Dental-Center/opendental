// Integration tests for B-TIME against a scratch MariaDB database on this computer. The database gets the current reference schema
// (SchemaVerifier.GetReference(), the one the CRUD matches) from the snapshot embedded in OpenPmsDentalBusiness and is filled with synthetic rows only. The tests are skipped (inconclusive)
// when no server answers at 127.0.0.1:3306 as root with a blank password, or when BTIME_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace BTIMETests {
	[TestClass]
	public class TimeClockDbTests {
		private const string SERVER="127.0.0.1";
		private const string CONNECTION="Server="+SERVER+";Port=3306;User ID=root;Password=;Connection Timeout=15";
		///<summary>Unique to this run, because several worktrees and test runners share the one local server. Always starts with btime_.</summary>
		private static readonly string _dbName="btime_"+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		private static string _skipReason;
		private static readonly DateTime _dateStart=new DateTime(2026,3,2);
		private static readonly DateTime _dateStop=new DateTime(2026,3,15);
		private CultureInfo _cultureInfoOld;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("BTIME_TEST_DB")=="skip") {
				_skipReason="BTIME_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(CONNECTION))) {
					connection.Open();
					Exec(connection,"CREATE DATABASE `"+_dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
				return;
			}
			//The server answers, so from here on a failure is a real failure, not a reason to skip.
			using(MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(CONNECTION+";Database="+_dbName))) {
				connection.Open();
				foreach(SchemaTable table in SchemaVerifier.GetReference().Tables) {
					Exec(connection,table.ToDdl());
				}
			}
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(SERVER,_dbName,"root","","","",DatabaseType.MySql,true,DevDbTls.GetSslCa(SERVER));
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(DevDbTls.Apply(CONNECTION))) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS `"+_dbName+"`");
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			foreach(string table in new[] { "employee","clockevent","timeadjust","preference" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			foreach(KeyValuePair<string,string> pair in _dictPrefsDefault) {
				DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('"+pair.Key+"','"+pair.Value+"')");
			}
			Prefs.RefreshCache();
			Employees.RefreshCache();
		}

		[TestCleanup]
		public void TestCleanup() {
			if(_cultureInfoOld!=null) {
				Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
			}
		}

		///<summary>The preferences the time clock code reads, at Open Dental's defaults.</summary>
		private static readonly Dictionary<string,string> _dictPrefsDefault=new Dictionary<string,string> {
			{ "RandomPrimaryKeys","0" },
			{ "LocalTimeOverridesServerTime","0" },
			{ "TimeCardsUseDecimalInsteadOfColon","0" },
			{ "TimeCardShowSeconds","0" },
		};

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static void SetPref(string prefName,string valueString) {
			DataCore.NonQ("UPDATE preference SET ValueString='"+valueString+"' WHERE PrefName='"+prefName+"'");
			Prefs.RefreshCache();
		}

		private static long InsertEmployee(string fName,long reportsTo,bool isHidden=false) {
			return Employees.Insert(new Employee { FName=fName,LName="Synthetic",ReportsTo=reportsTo,IsHidden=isHidden,ClockStatus="" });
		}

		private static void InsertClockEvent(long employeeNum,DateTime dateTimeIn,DateTime dateTimeOut,TimeClockStatus timeClockStatus,
			TimeSpan? timeSpanOTimeAuto=null)
		{
			ClockEvent clockEvent=new ClockEvent {
				EmployeeNum=employeeNum,
				TimeEntered1=dateTimeIn,
				TimeDisplayed1=dateTimeIn,
				TimeEntered2=dateTimeOut,
				TimeDisplayed2=dateTimeOut,
				ClockStatus=timeClockStatus,
				Note="",
			};
			if(timeSpanOTimeAuto.HasValue) {
				clockEvent.OTimeAuto=timeSpanOTimeAuto.Value;
			}
			ClockEvents.Insert(clockEvent);//TimeDisplayed1 is a date-entry column: the insert stamps it with the server time.
			clockEvent.TimeDisplayed1=dateTimeIn;
			ClockEvents.Update(clockEvent);//Like Edit Clock Event, which can change the displayed time afterwards.
		}

		[TestMethod]
		public void PeriodTotals_FromSavedRows_BothFormats() {
			long employeeNum=InsertEmployee("Tess",0);
			InsertClockEvent(employeeNum,_dateStart.AddHours(8),_dateStart.AddHours(17),TimeClockStatus.Home);
			InsertClockEvent(employeeNum,_dateStart.AddDays(1).AddHours(7),_dateStart.AddDays(1).AddHours(19),TimeClockStatus.Home,TimeSpan.FromHours(2));
			InsertClockEvent(employeeNum,_dateStart.AddDays(1).AddHours(10),_dateStart.AddDays(1).AddHours(10.25),TimeClockStatus.Break);
			InsertClockEvent(employeeNum,_dateStop.AddDays(1).AddHours(8),_dateStop.AddDays(1).AddHours(12),TimeClockStatus.Home);//next pay period
			TimeAdjusts.Insert(new TimeAdjust { EmployeeNum=employeeNum,TimeEntry=_dateStart.AddDays(2).AddHours(9),PtoDefNum=3,
				PtoHours=TimeSpan.FromHours(8),Note="" });
			TimeAdjusts.Insert(new TimeAdjust { EmployeeNum=employeeNum,TimeEntry=_dateStart.AddDays(3).AddHours(9),
				RegHours=TimeSpan.FromMinutes(-30),Note="" });
			TimeAdjusts.Insert(new TimeAdjust { EmployeeNum=employeeNum,TimeEntry=_dateStart,Note="Pay period note" });//the note row, zero hours
			//What the Time Card window reads for the Time Card view.
			List<ClockEvent> listClockEvents=ClockEvents.Refresh(employeeNum,_dateStart,_dateStop,isBreaks:false);
			List<TimeAdjust> listTimeAdjusts=TimeAdjusts.Refresh(employeeNum,_dateStart,_dateStop);
			TimeCardPeriodTotals totals=TimeCardTotals.Calculate(listClockEvents,listTimeAdjusts,isBreaks:false);
			Assert.AreEqual(TimeSpan.FromHours(21),totals.Total,"9:00 and 12:00; the break and the next period's event are not in this view.");
			Assert.AreEqual(TimeSpan.FromHours(2),totals.OT);
			Assert.AreEqual(TimeSpan.FromHours(8),totals.PTO);
			Assert.AreEqual(TimeSpan.FromMinutes(-30),totals.Adjust);
			Assert.AreEqual(TimeSpan.FromHours(18.5),totals.Regular);
			CollectionAssert.AreEqual(new List<string> { "21:00","-0:30","0:00","0:00","8:00","2:00","0:00","18:30" },
				TimeCardTotals.GetCells(totals,TimeCardTotals.GetDefaultFormat(),PrefC.GetBool(PrefName.TimeCardShowSeconds)));
			SetPref("TimeCardsUseDecimalInsteadOfColon","1");
			CollectionAssert.AreEqual(new List<string> { "21.00","-0.50","0.00","0.00","8.00","2.00","0.00","18.50" },
				TimeCardTotals.GetCells(totals,TimeCardTotals.GetDefaultFormat(),PrefC.GetBool(PrefName.TimeCardShowSeconds)));
			//The Breaks view.
			List<ClockEvent> listBreaks=ClockEvents.Refresh(employeeNum,_dateStart,_dateStop,isBreaks:true);
			TimeCardPeriodTotals totalsBreaks=TimeCardTotals.Calculate(listBreaks,new List<TimeAdjust>(),isBreaks:true);
			Assert.AreEqual(TimeSpan.FromMinutes(15),totalsBreaks.Total);
			Assert.AreEqual(TimeSpan.FromMinutes(15),totalsBreaks.Regular);
		}

		[TestMethod]
		public void DefaultFormat_ComesFromThePreference_AndShowSecondsAppliesToColon() {
			Assert.AreEqual(EnumTimeCardTotalsFormat.Colon,TimeCardTotals.GetDefaultFormat());
			SetPref("TimeCardShowSeconds","1");
			Assert.AreEqual("7:45:30",TimeCardTotals.FormatTotal(new TimeSpan(7,45,30),TimeCardTotals.GetDefaultFormat(),
				PrefC.GetBool(PrefName.TimeCardShowSeconds)));
			SetPref("TimeCardsUseDecimalInsteadOfColon","1");
			Assert.AreEqual(EnumTimeCardTotalsFormat.Decimal,TimeCardTotals.GetDefaultFormat());
		}

		[TestMethod]
		public void ReportsTo_FromTheEmployeeCache_HiddenSupervisorFallsBackToAll() {
			long employeeNumBoss=InsertEmployee("Ida",0);
			long employeeNumLead=InsertEmployee("Jon",employeeNumBoss);
			long employeeNumAssistant=InsertEmployee("Kim",employeeNumLead);
			long employeeNumHygienist=InsertEmployee("Lea",employeeNumBoss);
			Employees.RefreshCache();
			List<Employee> listShown=Employees.GetDeepCopy(true);//the time clock's list with clinics off
			List<Employee> listSupervisors=EmployeeReportsTo.GetSupervisors(listShown);//looks supervisors up in the cache
			CollectionAssert.AreEqual(new List<long> { employeeNumBoss,employeeNumLead },listSupervisors.Select(x => x.EmployeeNum).ToList());
			CollectionAssert.AreEquivalent(new List<long> { employeeNumLead,employeeNumHygienist },
				EmployeeReportsTo.Filter(listShown,employeeNumBoss).Select(x => x.EmployeeNum).ToList());
			EmployeeReportsTo.Remember(Security.CurUser.UserNum,employeeNumLead);
			Assert.AreEqual(employeeNumLead,EmployeeReportsTo.GetValidSelection(EmployeeReportsTo.GetRemembered(Security.CurUser.UserNum),listSupervisors));
			//Jon is hidden (Edit Employee, Hidden). His report Kim stays visible, but Jon is no longer offered and the filter falls back to All.
			DataCore.NonQ("UPDATE employee SET IsHidden=1 WHERE EmployeeNum="+employeeNumLead);
			Employees.RefreshCache();
			listShown=Employees.GetDeepCopy(true);
			listSupervisors=EmployeeReportsTo.GetSupervisors(listShown);
			CollectionAssert.AreEqual(new List<long> { employeeNumBoss },listSupervisors.Select(x => x.EmployeeNum).ToList());
			long supervisorEmployeeNum=EmployeeReportsTo.GetValidSelection(EmployeeReportsTo.GetRemembered(Security.CurUser.UserNum),listSupervisors);
			Assert.AreEqual(0,supervisorEmployeeNum);
			Assert.AreEqual(listShown.Count,EmployeeReportsTo.Filter(listShown,supervisorEmployeeNum).Count);
			Assert.IsTrue(listShown.Any(x => x.EmployeeNum==employeeNumAssistant));
			EmployeeReportsTo.Remember(Security.CurUser.UserNum,0);
		}
	}
}
