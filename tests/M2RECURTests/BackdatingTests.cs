// 26.1 v26_1-account-repeatcharge-backdate-pref: the preference that allows or prevents backdated repeating charges. Runs the Repeating Charges
// tool on the scratch database with a synthetic procedure code and patient.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace M2RECURTests {
	[TestClass]
	public class BackdatingTests {
		///<summary>A synthetic code (not a CDT code).</summary>
		private const string PROC_CODE="M2RC1";

		[TestInitialize]
		public void TestInitialize() {
			RecurDb.Begin();
			Security.CurUser=Security.CurUser??new Userod();
			ProcedureCodeT.CreateProcCode(PROC_CODE,descript:"Synthetic membership fee");
			ProcedureCodes.RefreshCache();
			Prefs.UpdateBool(PrefName.BillingUseBillingCycleDay,false);
			//The tool runs every repeating charge in the database; each test starts from none.
			foreach(RepeatCharge repeatCharge in RepeatCharges.Refresh(0)) {
				RepeatCharges.Delete(repeatCharge);
			}
		}

		[TestCleanup]
		public void TestCleanup() {
			if(RecurDb.SkipReason!=null) {
				return;
			}
			Prefs.UpdateBool(PrefName.RepeatingChargesAllowBackdating,true);
		}

		private static RepeatCharge AddRepeatCharge(Patient patient,DateTime dateStart) {
			RepeatCharge repeatCharge=new RepeatCharge {
				PatNum=patient.PatNum,
				ProcCode=PROC_CODE,
				ChargeAmt=40,
				ChargeAmtAlt=-1,
				DateStart=dateStart,
				IsEnabled=true,
				Frequency=EnumRepeatChargeFrequency.Monthly,
			};
			repeatCharge.RepeatChargeNum=RepeatCharges.Insert(repeatCharge);
			return repeatCharge;
		}

		private static List<DateTime> GetProcDates(Patient patient) {
			return Procedures.Refresh(patient.PatNum).Select(x => x.ProcDate).OrderBy(x => x).ToList();
		}

		[TestMethod]
		public void Allowed_MissedBillingDatesArePostedOnTheirDates() {
			//The default (24.3 behavior): run late on 3/20, the 2/5 and 3/5 billing dates are posted, dated 2/5 and 3/5.
			Prefs.UpdateBool(PrefName.RepeatingChargesAllowBackdating,true);
			Patient patient=RecurDb.CreatePatient("BackOn");
			AddRepeatCharge(patient,new DateTime(2026,2,5));
			RepeatChargeResult result=RepeatCharges.RunRepeatingCharges(new DateTime(2026,3,20,9,30,0),doComputeAging:false);
			Assert.AreEqual("",result.ErrorMsg.ToString());
			CollectionAssert.AreEqual(new List<DateTime> { new DateTime(2026,2,5),new DateTime(2026,3,5) },GetProcDates(patient));
		}

		[TestMethod]
		public void Prevented_NothingBeforeTheRunDate_AndMissedDatesAreNeverPostedLater() {
			Prefs.UpdateBool(PrefName.RepeatingChargesAllowBackdating,false);
			Patient patient=RecurDb.CreatePatient("BackOff");
			AddRepeatCharge(patient,new DateTime(2026,2,5));
			RepeatCharges.RunRepeatingCharges(new DateTime(2026,3,20,9,30,0),doComputeAging:false);
			Assert.AreEqual(0,GetProcDates(patient).Count,"2/5 and 3/5 are before the run date.");
			//On the next billing date the charge posts, dated that day; the skipped dates stay skipped.
			RepeatCharges.RunRepeatingCharges(new DateTime(2026,4,5,8,0,0),doComputeAging:false);
			CollectionAssert.AreEqual(new List<DateTime> { new DateTime(2026,4,5) },GetProcDates(patient));
			//Running again the same day adds nothing (duplicate detection unchanged).
			RepeatCharges.RunRepeatingCharges(new DateTime(2026,4,5,15,0,0),doComputeAging:false);
			Assert.AreEqual(1,GetProcDates(patient).Count);
			//Turning backdating back on lets a later run post what is still inside the window: 4/5 exists, nothing older than 1 month 20 days.
			Prefs.UpdateBool(PrefName.RepeatingChargesAllowBackdating,true);
			RepeatCharges.RunRepeatingCharges(new DateTime(2026,4,6,8,0,0),doComputeAging:false);
			CollectionAssert.AreEqual(new List<DateTime> { new DateTime(2026,3,5),new DateTime(2026,4,5) },GetProcDates(patient));
		}

		[TestMethod]
		public void Prevented_BillingDatesHelperKeepsOnlyTodayOrLater() {
			RepeatCharge repeatCharge=new RepeatCharge {
				DateStart=new DateTime(2026,1,10),
				Frequency=EnumRepeatChargeFrequency.Monthly,
			};
			DateTime dateRun=new DateTime(2026,3,10,14,0,0);
			Prefs.UpdateBool(PrefName.RepeatingChargesAllowBackdating,true);
			CollectionAssert.AreEqual(new List<DateTime> { new DateTime(2026,2,10),new DateTime(2026,3,10) },
				RepeatCharges.GetBillingDatesHelper(repeatCharge,dateRun),"Allowed: back to one month and 20 days.");
			Prefs.UpdateBool(PrefName.RepeatingChargesAllowBackdating,false);
			CollectionAssert.AreEqual(new List<DateTime> { new DateTime(2026,3,10) },RepeatCharges.GetBillingDatesHelper(repeatCharge,dateRun),
				"Prevented: today's billing date stays even though the run time is later in the day.");
			Assert.AreEqual(0,RepeatCharges.GetBillingDatesHelper(repeatCharge,new DateTime(2026,3,11)).Count);
		}

		[TestMethod]
		public void EditWindowNote_OnlyWhenPreventedAndTheStartDateHasPassed() {
			DateTime dateToday=new DateTime(2026,3,10);
			Prefs.UpdateBool(PrefName.RepeatingChargesAllowBackdating,true);
			Assert.IsFalse(RepeatChargeBackdating.IsStartDateSkipped(new DateTime(2026,3,8),dateToday));
			Prefs.UpdateBool(PrefName.RepeatingChargesAllowBackdating,false);
			Assert.IsTrue(RepeatChargeBackdating.IsStartDateSkipped(new DateTime(2026,3,8),dateToday));
			Assert.IsFalse(RepeatChargeBackdating.IsStartDateSkipped(dateToday,dateToday));
			Assert.IsFalse(RepeatChargeBackdating.IsStartDateSkipped(new DateTime(2026,3,12),dateToday));
			Assert.IsFalse(RepeatChargeBackdating.IsStartDateSkipped(DateTime.MinValue,dateToday),"No start date.");
		}

		[TestMethod]
		public void MissingPreferenceRow_AllowsBackdating() {
			//A database without the row (one the 26.1 seed has not reached) keeps the old behavior.
			DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+nameof(PrefName.RepeatingChargesAllowBackdating)+"'");
			try {
				Prefs.RefreshCache();
				Assert.IsTrue(RepeatChargeBackdating.IsAllowed());
			}
			finally {
				DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('"+nameof(PrefName.RepeatingChargesAllowBackdating)+"','1')");
				Prefs.RefreshCache();
			}
		}
	}
}
