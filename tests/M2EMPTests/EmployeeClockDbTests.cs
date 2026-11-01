// Integration tests for package M2-EMP against a scratch MariaDB database on this computer: employee.Title through the business layer, and the
// clock status, clock-in and clock-out of an employee whose clock-out was entered for a later day (Open Dental 26.1.63). The database gets the
// Open Dental 26.1.70 reference schema (package M2-01) and synthetic rows only. Its name is unique to the run and it is dropped afterwards. The
// tests are skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when M2EMP_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2EMPTests {
	[TestClass]
	public class EmployeeClockDbTests {
		private static readonly string DB_NAME=ScratchDb.Name("emp");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropStale();
			ScratchDb.Create26_1(DB_NAME);
			//The CRUD layer reads RandomPrimaryKeys on every insert; the clock buttons read LocalTimeOverridesServerTime.
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('LocalTimeOverridesServerTime','0')");
			Prefs.RefreshCache();
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropQuietly(DB_NAME);
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_NAME);
			foreach(string table in new[] { "employee","clockevent","definition","signalod","securitylog","securityloghash" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			Defs.RefreshCache();
			Employees.RefreshCache();
		}

		#region Synthetic data
		private static long AddEmployee(string fName,long title=0) {
			Employee employee=new Employee { FName=fName,LName="Synthetic",MiddleI="",ClockStatus="",PayrollID="",WirelessPhone="",EmailWork="",
				EmailPersonal="",Title=title };
			long employeeNum=Employees.Insert(employee);
			Employees.RefreshCache();
			return employeeNum;
		}

		private static long AddTitle(string itemName,int itemOrder,bool isHidden=false) {
			Def def=new Def { Category=DefCat.EmployeeTitles,ItemName=itemName,ItemOrder=itemOrder,IsHidden=isHidden,ItemValue="" };
			long defNum=Defs.Insert(def);
			Defs.RefreshCache();
			return defNum;
		}

		///<summary>A clock event with these displayed (and entered) times. DateTime.MinValue leaves the second time empty.</summary>
		private static long AddClockEvent(long employeeNum,TimeClockStatus timeClockStatus,DateTime time1,DateTime time2) {
			DataCore.NonQ("INSERT INTO clockevent (EmployeeNum,TimeEntered1,TimeDisplayed1,ClockStatus,Note,TimeEntered2,TimeDisplayed2,OTimeHours,"
				+"OTimeAuto,Adjust,AdjustAuto,AdjustIsOverridden,Rate2Hours,Rate2Auto,ClinicNum,Rate3Hours,Rate3Auto,IsWorkingHome) VALUES("
				+POut.Long(employeeNum)+","+POut.DateT(time1)+","+POut.DateT(time1)+","+POut.Int((int)timeClockStatus)+",'',"
				+POut.DateT(time2)+","+POut.DateT(time2)+",'-01:00:00','00:00:00','00:00:00','00:00:00',0,'-01:00:00','00:00:00',0,'-01:00:00','00:00:00',0)");
			return PIn.Long(DataCore.GetScalar("SELECT MAX(ClockEventNum) FROM clockevent"));
		}

		private static DateTime GetNow() {
			return MiscData.GetNowDateTime();
		}

		///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
		private static T AssertThrows<T>(Action action) where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				return ex;
			}
			Assert.Fail("Expected an exception of type "+typeof(T).Name+".");
			return null;
		}
		#endregion Synthetic data

		[TestMethod]
		public void Title_SavesAndLoads() {
			long defNumHygienist=AddTitle("Hygienist",0);
			long defNumAssistant=AddTitle("Assistant",1);
			long employeeNum=AddEmployee("Avery",defNumHygienist);
			Employee employee=Employees.GetEmpFromDB(employeeNum);
			Assert.AreEqual(defNumHygienist,employee.Title);
			Employee employeeOld=employee.Copy();
			employee.Title=defNumAssistant;
			Employees.UpdateChanged(employee,employeeOld);
			Assert.AreEqual(defNumAssistant,Employees.GetEmpFromDB(employeeNum).Title);
			employeeOld=employee.Copy();
			employee.Title=0;
			Employees.UpdateChanged(employee,employeeOld);
			Assert.AreEqual(0,Employees.GetEmpFromDB(employeeNum).Title);
		}

		[TestMethod]
		public void Title_TextAndPickerFromTheCache() {
			long defNumHygienist=AddTitle("Hygienist",0);
			long defNumManager=AddTitle("Office Manager",1,isHidden:true);
			Assert.AreEqual("Hygienist",Employees.GetTitleText(defNumHygienist));
			Assert.AreEqual("Office Manager (hidden)",Employees.GetTitleText(defNumManager));
			CollectionAssert.AreEqual(new[] { defNumHygienist },Employees.GetTitlesForPicker(0).Select(x => x.DefNum).ToArray());
			CollectionAssert.AreEqual(new[] { defNumHygienist,defNumManager },Employees.GetTitlesForPicker(defNumManager).Select(x => x.DefNum).ToArray());
		}

		[TestMethod]
		public void Title_InUseDefinitionCannotBeDeleted() {
			long defNumHygienist=AddTitle("Hygienist",0);
			long defNumUnused=AddTitle("Unused",1);
			AddEmployee("Avery",defNumHygienist);
			Def defUsed=Defs.GetDef(DefCat.EmployeeTitles,defNumHygienist);
			AssertThrows<ApplicationException>(() => Defs.Delete(defUsed));
			Defs.Delete(Defs.GetDef(DefCat.EmployeeTitles,defNumUnused));
			Defs.RefreshCache();
			Assert.IsNull(Defs.GetDef(DefCat.EmployeeTitles,defNumUnused));
		}

		[TestMethod]
		public void UpdateClockStatus_ClockOutOnALaterDay_StaysClockedIn() {
			long employeeNum=AddEmployee("Blake");
			DateTime now=GetNow();
			AddClockEvent(employeeNum,TimeClockStatus.Home,now.AddDays(-1).AddHours(-9),now.AddDays(-1).AddHours(-1));//Yesterday, clocked out.
			AddClockEvent(employeeNum,TimeClockStatus.Home,now.AddHours(-2),now.Date.AddDays(1).AddHours(17));//Today, clock-out tomorrow.
			Employees.UpdateClockStatus(employeeNum);
			//The 24.3 query left out a clock-out after today, so yesterday's event made this "Home". Now it is a clock-out still ahead, shown the
			//way a clock-out later today always was.
			Assert.AreEqual("Manual Entry",Employees.GetEmpFromDB(employeeNum).ClockStatus);
		}

		[TestMethod]
		public void UpdateClockStatus_PastClockOut_Unchanged() {
			long employeeNum=AddEmployee("Casey");
			DateTime now=GetNow();
			AddClockEvent(employeeNum,TimeClockStatus.Home,now.AddHours(-9),now.AddMinutes(-5));
			Employees.UpdateClockStatus(employeeNum);
			Assert.AreEqual("Home",Employees.GetEmpFromDB(employeeNum).ClockStatus);
			AddClockEvent(employeeNum,TimeClockStatus.Home,now.AddMinutes(-1),DateTime.MinValue);
			Employees.UpdateClockStatus(employeeNum);
			Assert.AreEqual("Working",Employees.GetEmpFromDB(employeeNum).ClockStatus);
		}

		[TestMethod]
		public void ClockButtons_FutureClockOut_CountsAsClockedIn() {
			long employeeNum=AddEmployee("Devon");
			DateTime now=GetNow();
			long clockEventNum=AddClockEvent(employeeNum,TimeClockStatus.Home,now.AddHours(-2),now.Date.AddDays(1).AddHours(17));
			Assert.IsTrue(ClockEvents.HasFutureClockOut(ClockEvents.GetLastEvent(employeeNum)));
			Assert.IsTrue(ClockEvents.IsClockedIn(employeeNum));
			//Clocking in again would start a second event inside this one.
			Exception ex=AssertThrows<Exception>(() => ClockEvents.ClockIn(employeeNum,isAtHome:false));
			StringAssert.Contains(ex.Message,"Already clocked in");
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM clockevent WHERE EmployeeNum="+POut.Long(employeeNum)));
			//Clocking out now replaces the clock-out that was entered ahead of time.
			ClockEvents.ClockOut(employeeNum,TimeClockStatus.Home);
			ClockEvent clockEvent=ClockEvents.GetOne(clockEventNum);
			Assert.IsTrue(clockEvent.TimeDisplayed2<=GetNow(),"Clock-out is now, not tomorrow.");
			Assert.IsTrue(clockEvent.TimeDisplayed2>=now.AddMinutes(-1));
			Assert.IsFalse(ClockEvents.IsClockedIn(employeeNum));
			Assert.IsFalse(ClockEvents.HasFutureClockOut(clockEvent));
		}

		[TestMethod]
		public void ClockButtons_PastClockOut_Unchanged() {
			long employeeNum=AddEmployee("Emery");
			DateTime now=GetNow();
			AddClockEvent(employeeNum,TimeClockStatus.Home,now.AddHours(-9),now.AddHours(-1));
			Assert.IsFalse(ClockEvents.IsClockedIn(employeeNum));
			Exception ex=AssertThrows<Exception>(() => ClockEvents.ClockOut(employeeNum,TimeClockStatus.Home));
			StringAssert.Contains(ex.Message,"Already clocked out");
		}

		[TestMethod]
		public void DatabaseMaintenance_FutureClockOut_NotCountedAndNotChanged() {
			//Database Maintenance's ClockEventInFuture used to count every clock-out more than 15 minutes ahead as invalid and, in Fix, overwrite it
			//with the time of the run: a Home or Lunch event entered for a later day lost its clock-out and the employee counted as clocked out. Now
			//only what CheckFutureDates still refuses counts: a clock-in in the future, and a break's return in the future.
			long employeeNum=AddEmployee("Gray");
			DateTime now=GetNow();
			long clockEventNumHome=AddClockEvent(employeeNum,TimeClockStatus.Home,now.AddHours(-2),now.Date.AddDays(1).AddHours(6));
			long clockEventNumLunch=AddClockEvent(employeeNum,TimeClockStatus.Lunch,now.AddHours(-1),now.Date.AddDays(1).AddHours(12));
			ClockEvent clockEventHomeBefore=ClockEvents.GetOne(clockEventNumHome);
			ClockEvent clockEventLunchBefore=ClockEvents.GetOne(clockEventNumLunch);
			Assert.AreEqual("",DatabaseMaintenances.ClockEventInFuture(false,DbmMode.Check),"A future clock-out is not an invalid entry.");
			StringAssert.Contains(DatabaseMaintenances.ClockEventInFuture(true,DbmMode.Check),"Time card entries invalid: 0");
			Assert.AreEqual("",DatabaseMaintenances.ClockEventInFuture(false,DbmMode.Fix),"Fix has nothing to change.");
			Assert.AreEqual(clockEventHomeBefore.TimeDisplayed2,ClockEvents.GetOne(clockEventNumHome).TimeDisplayed2);
			Assert.AreEqual(clockEventLunchBefore.TimeDisplayed2,ClockEvents.GetOne(clockEventNumLunch).TimeDisplayed2);
			Assert.IsTrue(ClockEvents.IsClockedIn(employeeNum),"Still clocked in until the entered clock-out.");
			//What stays invalid: a clock-in in the future, and a break whose return is in the future.
			long clockEventNumFutureIn=AddClockEvent(employeeNum,TimeClockStatus.Home,now.AddHours(3),DateTime.MinValue);
			long clockEventNumBreak=AddClockEvent(employeeNum,TimeClockStatus.Break,now.AddMinutes(-10),now.AddHours(2));
			Assert.AreEqual("Time card entries invalid: 2\r\n",DatabaseMaintenances.ClockEventInFuture(false,DbmMode.Check));
			Assert.AreEqual("Future timecard entry times fixed: 2\r\n",DatabaseMaintenances.ClockEventInFuture(false,DbmMode.Fix));
			DateTime nowAfterFix=GetNow();
			Assert.IsTrue(ClockEvents.GetOne(clockEventNumFutureIn).TimeDisplayed1<=nowAfterFix,"Fix still corrects a future clock-in.");
			Assert.IsTrue(ClockEvents.GetOne(clockEventNumBreak).TimeDisplayed2<=nowAfterFix,"Fix still corrects a break's future return.");
			Assert.AreEqual(clockEventHomeBefore.TimeDisplayed2,ClockEvents.GetOne(clockEventNumHome).TimeDisplayed2,"Home clock-out kept.");
			Assert.AreEqual(clockEventLunchBefore.TimeDisplayed2,ClockEvents.GetOne(clockEventNumLunch).TimeDisplayed2,"Lunch clock-out kept.");
			Assert.AreEqual("",DatabaseMaintenances.ClockEventInFuture(false,DbmMode.Check),"Nothing left to fix.");
		}

		[TestMethod]
		public void FutureClockOut_TotalsCountTheStoredTimes() {
			//Rule 6 (INFERRED): totals use the stored times as they are. GetSimpleList returns the stored event unchanged.
			long employeeNum=AddEmployee("Finley");
			DateTime now=GetNow();
			DateTime time1=now.Date.AddHours(8);
			DateTime time2=now.Date.AddDays(1).AddHours(17);
			AddClockEvent(employeeNum,TimeClockStatus.Home,time1,time2);
			List<ClockEvent> listClockEvents=ClockEvents.GetSimpleList(employeeNum,now.Date.AddDays(-1),now.Date.AddDays(2));
			Assert.AreEqual(1,listClockEvents.Count);
			Assert.AreEqual(time2-time1,listClockEvents[0].TimeDisplayed2-listClockEvents[0].TimeDisplayed1);
		}
	}
}
