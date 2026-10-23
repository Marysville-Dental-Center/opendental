// Date-aware fee lookup without a database: the as-of matrix, the "current" index, rows in effect, future changes and the import file format.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace FeeTests {
	[TestClass]
	public class FeeLookupTests {
		private const long SCHED=10;
		private const long CODE=100;
		private const long CLINIC=5;
		private const long PROV=7;

		private static Fee MakeFee(long clinicNum,long provNum,string dateEffective,double amount,long feeNum,long codeNum=CODE,long feeSched=SCHED) {
			Fee fee=new Fee();
			fee.FeeNum=feeNum;
			fee.FeeSched=feeSched;
			fee.CodeNum=codeNum;
			fee.ClinicNum=clinicNum;
			fee.ProvNum=provNum;
			fee.Amount=amount;
			fee.DateEffective=(dateEffective==null ? DateTime.MinValue : DateTime.Parse(dateEffective));
			return fee;
		}

		private static DateTime D(string date) {
			return DateTime.Parse(date);
		}

		///<summary>Synthetic rows. Default level: always 100, 110 from 2025-01-01, 150 from 2030-01-01. Clinic override: 120 from 2026-06-01.
		///Provider override: 170 from 2031-01-01 only. Clinic and provider: always 130, 140 from 2029-01-01. Plus another code and schedule.</summary>
		private static List<Fee> GetMatrix() {
			return new List<Fee> {
				MakeFee(0,0,null,100,1),
				MakeFee(0,0,"2025-01-01",110,2),
				MakeFee(0,0,"2030-01-01",150,3),
				MakeFee(CLINIC,0,"2026-06-01",120,4),
				MakeFee(0,PROV,"2031-01-01",170,5),
				MakeFee(CLINIC,PROV,null,130,6),
				MakeFee(CLINIC,PROV,"2029-01-01",140,7),
				MakeFee(0,0,null,999,8,codeNum:CODE+1),
				MakeFee(0,0,null,888,9,feeSched:SCHED+1),
			};
		}

		private static double? Amount(List<Fee> listFees,long clinicNum,long provNum,string dateAsOf,bool isGlobal=false,bool doGetExactMatch=false) {
			return Fees.GetFeeFromList(listFees,CODE,SCHED,clinicNum,provNum,isGlobal,D(dateAsOf),doGetExactMatch)?.Amount;
		}

		[TestMethod]
		public void Lookup_DefaultLevel_LatestRowOnOrBeforeAsOfDate() {
			List<Fee> listFees=GetMatrix();
			Assert.AreEqual(100,Amount(listFees,0,0,"2024-12-31"));
			Assert.AreEqual(110,Amount(listFees,0,0,"2025-01-01"));
			Assert.AreEqual(110,Amount(listFees,0,0,"2029-12-31"));
			Assert.AreEqual(150,Amount(listFees,0,0,"2030-01-01"));
			Assert.AreEqual(150,Amount(listFees,0,0,"2099-01-01"));
		}

		[TestMethod]
		public void Lookup_AlwaysRow_IsInEffectOnAnyDate() {
			List<Fee> listFees=new List<Fee> { MakeFee(0,0,null,100,1) };
			Assert.AreEqual(100,Amount(listFees,0,0,"1900-01-01"));
			Assert.AreEqual(100,Amount(listFees,0,0,"2100-01-01"));
		}

		[TestMethod]
		public void Lookup_FutureOnlyRow_IsNeverFoundBeforeItsDate() {
			List<Fee> listFees=new List<Fee> { MakeFee(0,0,"2030-01-01",150,1) };
			Assert.IsNull(Amount(listFees,0,0,"2029-12-31"));
			Assert.AreEqual(150,Amount(listFees,0,0,"2030-01-01"));
		}

		[TestMethod]
		public void Lookup_ClinicOverride_DateFilterAppliesAtEachLevel() {
			List<Fee> listFees=GetMatrix();
			//Before the clinic override starts, the default level answers.
			Assert.AreEqual(110,Amount(listFees,CLINIC,0,"2026-05-31"));
			Assert.AreEqual(120,Amount(listFees,CLINIC,0,"2026-06-01"));
			//A newer default row does not beat a clinic override that is in effect.
			Assert.AreEqual(120,Amount(listFees,CLINIC,0,"2030-02-01"));
		}

		[TestMethod]
		public void Lookup_ProviderOverride_FutureOnlyFallsBackUntilItsDate() {
			List<Fee> listFees=GetMatrix();
			Assert.AreEqual(150,Amount(listFees,0,PROV,"2030-06-01"));
			Assert.AreEqual(170,Amount(listFees,0,PROV,"2031-01-01"));
			//Provider override (clinic 0) comes before a clinic override, as before effective dates.
			Assert.AreEqual(170,Amount(listFees,CLINIC+4,PROV,"2031-02-01"));
		}

		[TestMethod]
		public void Lookup_ClinicAndProvider_DatedRowsOfExactKey() {
			List<Fee> listFees=GetMatrix();
			Assert.AreEqual(130,Amount(listFees,CLINIC,PROV,"2028-12-31"));
			Assert.AreEqual(140,Amount(listFees,CLINIC,PROV,"2029-01-01"));
			Assert.AreEqual(140,Amount(listFees,CLINIC,PROV,"2032-01-01"));
		}

		[TestMethod]
		public void Lookup_ExactMatch_OnlyTheExactKeyCounts() {
			List<Fee> listFees=GetMatrix();
			Assert.IsNull(Amount(listFees,CLINIC,0,"2026-01-01",doGetExactMatch:true));
			Assert.AreEqual(120,Amount(listFees,CLINIC,0,"2026-07-01",doGetExactMatch:true));
		}

		[TestMethod]
		public void Lookup_GlobalSchedule_IgnoresClinicAndProvider() {
			List<Fee> listFees=GetMatrix();
			Assert.AreEqual(110,Amount(listFees,CLINIC,PROV,"2025-02-01",isGlobal:true));
			Assert.AreEqual(150,Amount(listFees,CLINIC,PROV,"2030-02-01",isGlobal:true));
		}

		[TestMethod]
		public void Lookup_OtherCodesAndSchedulesAreIgnored() {
			List<Fee> listFees=GetMatrix();
			Assert.AreEqual(999,Fees.GetFeeFromList(listFees,CODE+1,SCHED,0,0,false,D("2026-01-01")).Amount);
			Assert.AreEqual(888,Fees.GetFeeFromList(listFees,CODE,SCHED+1,0,0,false,D("2026-01-01")).Amount);
			Assert.IsNull(Fees.GetFeeFromList(listFees,CODE+2,SCHED,0,0,false,D("2026-01-01")));
			Assert.IsNull(Fees.GetFeeFromList(null,CODE,SCHED,0,0,false,D("2026-01-01")));
		}

		[TestMethod]
		public void Lookup_SameKeyAndDate_FirstRowWins() {
			List<Fee> listFees=new List<Fee> { MakeFee(0,0,"2025-01-01",1,1),MakeFee(0,0,"2025-01-01",2,2) };
			Assert.AreEqual(1,Amount(listFees,0,0,"2026-01-01"));
		}

		[TestMethod]
		public void Lookup_TimeOfDayIsIgnored() {
			List<Fee> listFees=new List<Fee> { MakeFee(0,0,"2025-01-01",1,1) };
			Assert.AreEqual(1,Fees.GetFeeFromList(listFees,CODE,SCHED,0,0,false,D("2025-01-01").AddHours(23)).Amount);
		}

		[TestMethod]
		public void GetFeeExactDate_MatchesKeyAndDateOnly() {
			List<Fee> listFees=GetMatrix();
			Assert.AreEqual(2,Fees.GetFeeExactDate(listFees,CODE,SCHED,0,0,D("2025-01-01")).FeeNum);
			Assert.AreEqual(1,Fees.GetFeeExactDate(listFees,CODE,SCHED,0,0,DateTime.MinValue).FeeNum);
			Assert.IsNull(Fees.GetFeeExactDate(listFees,CODE,SCHED,0,0,D("2025-01-02")));
			Assert.IsNull(Fees.GetFeeExactDate(listFees,CODE,SCHED,CLINIC,0,DateTime.MinValue));
		}

		[TestMethod]
		public void GetRowsInEffect_OneRowPerKeyAndNoFutureOnlyKeys() {
			List<Fee> listFees=GetMatrix();
			List<Fee> listInEffect=Fees.GetRowsInEffect(listFees,D("2027-01-01"));
			CollectionAssert.AreEqual(new long[] { 2,4,6,8,9 },listInEffect.Select(x => x.FeeNum).ToArray());
			listInEffect=Fees.GetRowsInEffect(listFees,D("2031-06-01"));
			CollectionAssert.AreEqual(new long[] { 3,4,5,7,8,9 },listInEffect.Select(x => x.FeeNum).ToArray());
		}

		[TestMethod]
		public void GetFutureChanges_OnlyDatesWhereTheLookupChanges() {
			List<Fee> listFees=GetMatrix();
			List<Fee> listChanges=Fees.GetFutureChanges(listFees,CODE,SCHED,0,0,false,D("2026-01-01"));
			CollectionAssert.AreEqual(new long[] { 3 },listChanges.Select(x => x.FeeNum).ToArray());
			//The 2030 default row does not change what the clinic sees.
			listChanges=Fees.GetFutureChanges(listFees,CODE,SCHED,CLINIC,0,false,D("2026-01-01"));
			CollectionAssert.AreEqual(new long[] { 4 },listChanges.Select(x => x.FeeNum).ToArray());
			listChanges=Fees.GetFutureChanges(listFees,CODE,SCHED,0,PROV,false,D("2026-01-01"));
			CollectionAssert.AreEqual(new long[] { 3,5 },listChanges.Select(x => x.FeeNum).ToArray());
			Assert.AreEqual(0,Fees.GetFutureChanges(listFees,CODE,SCHED,0,0,false,D("2035-01-01")).Count);
		}

		[TestMethod]
		public void DateAsOf_ExplicitDateThenScopeThenToday() {
			Assert.AreEqual(DateTime.Today,Fees.GetDateAsOf());
			Assert.AreEqual(D("2027-03-04"),Fees.GetDateAsOf(D("2027-03-04").AddHours(5)));
			using(Fees.ScopeDateAsOf(D("2020-05-05"))) {
				Assert.AreEqual(D("2020-05-05"),Fees.GetDateAsOf());
				Assert.AreEqual(D("2027-03-04"),Fees.GetDateAsOf(D("2027-03-04")));
				using(Fees.ScopeDateAsOf(D("2021-06-06"))) {
					Assert.AreEqual(D("2021-06-06"),Fees.GetDateAsOf());
				}
				//A scope without a date keeps the outer one.
				using(Fees.ScopeDateAsOf(DateTime.MinValue)) {
					Assert.AreEqual(D("2020-05-05"),Fees.GetDateAsOf());
				}
				Assert.AreEqual(D("2020-05-05"),Fees.GetDateAsOf());
			}
			Assert.AreEqual(DateTime.Today,Fees.GetDateAsOf());
		}

		[TestMethod]
		public void DateAsOf_ScopeIsPerThread() {
			using(Fees.ScopeDateAsOf(D("2020-05-05"))) {
				DateTime dateOtherThread=DateTime.MinValue;
				System.Threading.Thread thread=new System.Threading.Thread(() => dateOtherThread=Fees.GetDateAsOf());
				thread.Start();
				thread.Join();
				Assert.AreEqual(DateTime.Today,dateOtherThread);
			}
		}

		[TestMethod]
		public void DateAsOfForProc_CompletedUsesProcDate_OthersToday() {
			Procedure procedure=new Procedure();
			procedure.ProcStatus=ProcStat.C;
			procedure.ProcDate=D("2025-06-01");
			Assert.AreEqual(D("2025-06-01"),Fees.GetDateAsOfForProc(procedure));
			procedure.ProcStatus=ProcStat.TP;
			Assert.AreEqual(DateTime.Today,Fees.GetDateAsOfForProc(procedure));
			procedure.ProcStatus=ProcStat.C;
			procedure.ProcDate=DateTime.Today.AddDays(10);//future-dated completion: today
			Assert.AreEqual(DateTime.Today,Fees.GetDateAsOfForProc(procedure));
			procedure.ProcDate=DateTime.MinValue;
			Assert.AreEqual(DateTime.Today,Fees.GetDateAsOfForProc(procedure));
			Assert.AreEqual(DateTime.Today,Fees.GetDateAsOfForProc(null));
		}

		[TestMethod]
		public void ParseImportLines_BothFormats() {
			List<string> listLines=new List<string> {
				"D9991\t85.00\tProphy\tFictional prophylaxis",
				"D9991\t90.00\tProphy\tFictional prophylaxis\t2027-01-01",
				"D9992\t\tExam\tFictional exam\t",
				"too short",
				"D9993\t1,234.50\tX\tY\tnot a date",
			};
			List<Fees.FeeImportLine> listParsed=Fees.ParseImportLines(listLines);
			Assert.AreEqual(4,listParsed.Count);
			Assert.AreEqual("D9991",listParsed[0].ProcCode);
			Assert.AreEqual("85.00",listParsed[0].AmountText);
			Assert.IsFalse(listParsed[0].HasDate);
			Assert.IsTrue(listParsed[1].HasDate);
			Assert.AreEqual(D("2027-01-01"),listParsed[1].DateEffective);
			Assert.AreEqual("",listParsed[2].AmountText);
			Assert.IsFalse(listParsed[2].HasDate);
			Assert.IsFalse(listParsed[3].HasDate);
		}

		[TestMethod]
		public void FeeLim_CarriesDateEffective() {
			Fee fee=MakeFee(CLINIC,PROV,"2027-01-01",5,42);
			FeeLim feeLim=(FeeLim)fee;
			Assert.AreEqual(D("2027-01-01"),feeLim.DateEffective);
			Assert.AreEqual(D("2027-01-01"),((Fee)feeLim).DateEffective);
		}
	}
}
