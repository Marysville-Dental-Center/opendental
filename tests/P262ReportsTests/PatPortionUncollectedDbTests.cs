// The Patient Portion Uncollected report (RpPatPortionUncollected) on the scratch database: the rows of the date range as before 26.2, the prior
// uncollected balances option (26.2 item 33) and the Pay Plan column (26.2 item 34). Each test has its own patients, numbers and months, so the
// tests do not see each other's rows. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262ReportsTests {
	[TestClass]
	public class PatPortionUncollectedDbTests {
		private const long CODE_NUM=990001;

		[TestInitialize]
		public void TestInitialize() {
			ReportsDb.Begin();
			ReportsDb.Seed($"INSERT IGNORE INTO procedurecode (CodeNum,ProcCode,Descript,AbbrDesc) VALUES ({CODE_NUM},'T26201','Synthetic exam','SynExam')");
		}

		#region Synthetic rows
		private static string Patient(long patNum,string lName) {
			return $"INSERT INTO patient (PatNum,LName,FName,Guarantor) VALUES ({patNum},'{lName}','Test',{patNum})";
		}

		private static string Proc(long procNum,long patNum,string date,double fee,long clinicNum=0) {
			return $"INSERT INTO procedurelog (ProcNum,PatNum,ProcDate,DateComplete,ProcFee,ProcStatus,CodeNum,ClinicNum,BaseUnits,UnitQty) "
				+$"VALUES ({procNum},{patNum},'{date}','{date}',{fee},{(int)ProcStat.C},{CODE_NUM},{clinicNum},0,1)";
		}

		private static string ClaimProc(long claimProcNum,long procNum,long patNum,ClaimProcStatus status,double insPayEst,double insPayAmt,double writeOff) {
			return $"INSERT INTO claimproc (ClaimProcNum,ProcNum,PatNum,Status,InsPayEst,InsPayAmt,WriteOff) "
				+$"VALUES ({claimProcNum},{procNum},{patNum},{(int)status},{insPayEst},{insPayAmt},{writeOff})";
		}

		private static string Adjustment(long adjNum,long patNum,long procNum,double amount,string date) {
			return $"INSERT INTO adjustment (AdjNum,PatNum,ProcNum,AdjAmt,ProcDate,AdjDate) VALUES ({adjNum},{patNum},{procNum},{amount},'{date}','{date}')";
		}

		private static string Split(long splitNum,long patNum,long procNum,double amount,long payPlanNum=0,long payPlanChargeNum=0) {
			return $"INSERT INTO paysplit (SplitNum,PatNum,ProcNum,SplitAmt,PayPlanNum,PayPlanChargeNum,DatePay) "
				+$"VALUES ({splitNum},{patNum},{procNum},{amount},{payPlanNum},{payPlanChargeNum},'2031-12-31')";
		}

		///<summary>A payment split attached to an adjustment (paysplit.AdjNum), not to a procedure.</summary>
		private static string AdjSplit(long splitNum,long patNum,long adjNum,double amount) {
			return $"INSERT INTO paysplit (SplitNum,PatNum,ProcNum,AdjNum,SplitAmt,PayPlanNum,PayPlanChargeNum,DatePay) "
				+$"VALUES ({splitNum},{patNum},0,{adjNum},{amount},0,0,'2031-12-31')";
		}

		private static string PayPlan(long payPlanNum,long patNum,long planNum,bool isDynamic) {
			return $"INSERT INTO payplan (PayPlanNum,PatNum,Guarantor,PlanNum,IsDynamic) VALUES ({payPlanNum},{patNum},{patNum},{planNum},{(isDynamic ? 1 : 0)})";
		}

		private static string PayPlanCharge(long payPlanChargeNum,long payPlanNum,long patNum,long procNum,double principal,PayPlanChargeType chargeType) {
			return $"INSERT INTO payplancharge (PayPlanChargeNum,PayPlanNum,PatNum,Guarantor,ProcNum,Principal,ChargeType,ChargeDate) "
				+$"VALUES ({payPlanChargeNum},{payPlanNum},{patNum},{patNum},{procNum},{principal},{(int)chargeType},'2031-12-01')";
		}

		private static string PayPlanLink(long payPlanLinkNum,long payPlanNum,long procNum,double amountOverride) {
			return $"INSERT INTO payplanlink (PayPlanLinkNum,PayPlanNum,LinkType,FKey,AmountOverride) "
				+$"VALUES ({payPlanLinkNum},{payPlanNum},{(int)PayPlanLinkType.Procedure},{procNum},{amountOverride})";
		}
		#endregion Synthetic rows

		///<summary>The rows of the given patients, in the report's order.</summary>
		private static List<DataRow> Run(string dateFrom,string dateTo,bool doShowPrior,List<long> listClinicNums,params long[] arrayPatNums) {
			DataTable table=RpPatPortionUncollected.GetPatUncollected(DateTime.Parse(dateFrom),DateTime.Parse(dateTo),listClinicNums,doShowPrior);
			Assert.IsTrue(table.Columns.Contains(RpPatPortionUncollected.ColumnPayPlan),"The Pay Plan column.");
			Assert.IsTrue(table.Columns.Contains(RpPatPortionUncollected.ColumnIsPrior),"The prior marker column.");
			return table.Rows.Cast<DataRow>().Where(x => arrayPatNums.Contains(PIn.Long(x["PatNum"].ToString()))).ToList();
		}

		private static List<DataRow> Run(string dateFrom,string dateTo,bool doShowPrior,params long[] arrayPatNums) {
			return Run(dateFrom,dateTo,doShowPrior,new List<long>(),arrayPatNums);
		}

		private static double Value(DataRow row,string column) {
			return PIn.Double(row[column].ToString());
		}

		private static bool IsPrior(DataRow row) {
			return PIn.Int(row[RpPatPortionUncollected.ColumnIsPrior].ToString())==1;
		}

		private static DateTime Date(DataRow row) {
			return PIn.Date(row["ProcDate"].ToString());
		}

		[TestMethod]
		public void DateRange_ListsTheUncollectedProceduresAsBefore() {
			ReportsDb.Seed(
				Patient(911001,"Range"),
				//Fee 100, insurance estimate 60, adjustment -10 in the range, paid 5: patient portion 40, uncollected 25.
				Proc(911101,911001,"2031-01-15",100),
				ClaimProc(911201,911101,911001,ClaimProcStatus.NotReceived,60,0,0),
				Adjustment(911301,911001,911101,-10,"2031-01-15"),
				Adjustment(911302,911001,911101,-7,"2031-02-15"),//After the range: not counted, as before.
				Split(911401,911001,911101,5),
				//Paid in full: not listed.
				Proc(911102,911001,"2031-01-16",50),
				Split(911402,911001,911102,50),
				//Before the range: listed only with the option.
				Proc(911103,911001,"2030-12-01",80)
			);
			List<DataRow> listRows=Run("2031-01-10","2031-01-20",false,911001);
			Assert.AreEqual(1,listRows.Count);
			DataRow row=listRows[0];
			Assert.AreEqual(new DateTime(2031,1,15),Date(row));
			Assert.AreEqual("Range, Test",row["Patient"].ToString());
			Assert.AreEqual("SynExam",row["AbbrDesc"].ToString());
			Assert.AreEqual(100,Value(row,"Fee"),0.001);
			Assert.AreEqual(40,Value(row,"PatPortion"),0.001);
			Assert.AreEqual(-10,Value(row,"Adjustment"),0.001);
			Assert.AreEqual(5,Value(row,"Payment"),0.001);
			Assert.AreEqual(25,Value(row,"Uncollected"),0.001);
			Assert.AreEqual(0,Value(row,RpPatPortionUncollected.ColumnPayPlan),0.001);
			Assert.IsFalse(IsPrior(row));
			//The option adds the prior procedure only; the row of the range is the same.
			List<DataRow> listRowsPrior=Run("2031-01-10","2031-01-20",true,911001);
			Assert.AreEqual(2,listRowsPrior.Count);
			Assert.IsTrue(IsPrior(listRowsPrior[0]));
			Assert.AreEqual(new DateTime(2030,12,1),Date(listRowsPrior[0]));
			Assert.AreEqual(80,Value(listRowsPrior[0],"Uncollected"),0.001);
			Assert.IsFalse(IsPrior(listRowsPrior[1]));
			foreach(string column in new[] { "ProcDate","Patient","AbbrDesc","Fee","PatPortion","Adjustment","Payment","Uncollected",RpPatPortionUncollected.ColumnPayPlan }) {
				Assert.AreEqual(row[column].ToString(),listRowsPrior[1][column].ToString(),column);
			}
		}

		///<summary>Owner decision of 2026-11-07: an adjustment with several payment splits attached counts its amount once, less those splits. The 26.1
		///query counted it once per split (here the first adjustment twice, so an Adjustment of 60 instead of 10 and 120-30=90 instead of 10 on the
		///prior row). The prior rows use the same subquery.</summary>
		[TestMethod]
		public void Adjustment_WithSeveralPaymentSplits_CountsOnce() {
			ReportsDb.Seed(
				Patient(911501,"Adjsplits"),
				//Fee 200, no insurance. Adjustment +50 with two splits of 10 and 15 on it: 25. Adjustment +10 with one split of 5: 5. Adjustment -20
				//with no split: -20. Adjustment column 25+5-20=10; uncollected 200+10-30 (paid on the procedure)=180.
				Proc(911511,911501,"2031-07-15",200),
				Adjustment(911521,911501,911511,50,"2031-07-15"),
				AdjSplit(911531,911501,911521,10),
				AdjSplit(911532,911501,911521,15),
				Adjustment(911522,911501,911511,10,"2031-07-16"),
				AdjSplit(911533,911501,911522,5),
				Adjustment(911523,911501,911511,-20,"2031-07-17"),
				Split(911541,911501,911511,30),
				//The same pattern on a procedure before the range, for the prior rows: +40 with splits 10, 10 and 10, so 10; uncollected 100+10=110.
				Proc(911512,911501,"2031-05-01",100),
				Adjustment(911524,911501,911512,40,"2031-05-01"),
				AdjSplit(911534,911501,911524,10),
				AdjSplit(911535,911501,911524,10),
				AdjSplit(911536,911501,911524,10)
			);
			List<DataRow> listRows=Run("2031-07-10","2031-07-20",false,911501);
			Assert.AreEqual(1,listRows.Count);
			Assert.AreEqual(10,Value(listRows[0],"Adjustment"),0.001,"Each adjustment once, less the splits attached to it.");
			Assert.AreEqual(30,Value(listRows[0],"Payment"),0.001);
			Assert.AreEqual(180,Value(listRows[0],"Uncollected"),0.001);
			List<DataRow> listRowsPrior=Run("2031-07-10","2031-07-20",true,911501);
			Assert.AreEqual(2,listRowsPrior.Count);
			Assert.IsTrue(IsPrior(listRowsPrior[0]));
			Assert.AreEqual(10,Value(listRowsPrior[0],"Adjustment"),0.001,"The prior rows count each adjustment once too.");
			Assert.AreEqual(110,Value(listRowsPrior[0],"Uncollected"),0.001);
			Assert.AreEqual(180,Value(listRowsPrior[1],"Uncollected"),0.001);
		}

		[TestMethod]
		public void ShowPrior_ListsEarlierUncollectedProceduresOfThePatientsOfTheRange() {
			ReportsDb.Seed(
				//Seen in the range with a procedure paid in full (not listed itself), and earlier balances.
				Patient(912001,"Seen"),
				Proc(912101,912001,"2031-02-12",60),
				Split(912401,912001,912101,60),
				//Prior, fee 90, insurance paid 20, adjustment -20 at the procedure's date, another after the range: uncollected 50.
				Proc(912102,912001,"2030-06-01",90),
				ClaimProc(912201,912102,912001,ClaimProcStatus.Received,20,20,0),
				Adjustment(912301,912001,912102,-20,"2030-06-01"),
				Adjustment(912302,912001,912102,-5,"2031-03-01"),
				//Prior and paid in full: not listed.
				Proc(912103,912001,"2030-07-01",40),
				Split(912402,912001,912103,40),
				//Prior, a day before the range: listed.
				Proc(912104,912001,"2031-02-09",15),
				//Not seen in the range: never listed, however much is uncollected.
				Patient(912002,"Unseen"),
				Proc(912105,912002,"2030-06-01",500),
				//Seen only after the range: never listed.
				Patient(912003,"Later"),
				Proc(912106,912003,"2031-02-25",30),
				Proc(912107,912003,"2030-06-01",30)
			);
			Assert.AreEqual(0,Run("2031-02-10","2031-02-20",false,912001,912002,912003).Count,"Without the option nothing of these patients is listed.");
			List<DataRow> listRows=Run("2031-02-10","2031-02-20",true,912001,912002,912003);
			Assert.AreEqual(2,listRows.Count);
			Assert.IsTrue(listRows.All(x => IsPrior(x)));
			Assert.IsTrue(listRows.All(x => PIn.Long(x["PatNum"].ToString())==912001));
			Assert.AreEqual(new DateTime(2030,6,1),Date(listRows[0]),"Ordered by date.");
			Assert.AreEqual(90,Value(listRows[0],"Fee"),0.001);
			Assert.AreEqual(70,Value(listRows[0],"PatPortion"),0.001);
			Assert.AreEqual(-20,Value(listRows[0],"Adjustment"),0.001,"Adjustments dated up to the end of the range.");
			Assert.AreEqual(50,Value(listRows[0],"Uncollected"),0.001);
			Assert.AreEqual(new DateTime(2031,2,9),Date(listRows[1]));
			Assert.AreEqual(15,Value(listRows[1],"Uncollected"),0.001);
		}

		[TestMethod]
		public void ShowPrior_RowsOfTheRangeComeAfterThePriorRows() {
			ReportsDb.Seed(
				Patient(913001,"Aaa"),
				Patient(913002,"Bbb"),
				Proc(913101,913001,"2031-03-12",10),
				Proc(913102,913002,"2031-03-11",20),
				Proc(913103,913002,"2029-01-01",30),
				Proc(913104,913001,"2030-01-01",40)
			);
			List<DataRow> listRows=Run("2031-03-10","2031-03-20",true,913001,913002);
			Assert.AreEqual(4,listRows.Count);
			CollectionAssert.AreEqual(new[] { true,true,false,false },listRows.Select(x => IsPrior(x)).ToArray());
			CollectionAssert.AreEqual(new[] { new DateTime(2029,1,1),new DateTime(2030,1,1),new DateTime(2031,3,11),new DateTime(2031,3,12) },
				listRows.Select(x => Date(x)).ToArray());
		}

		[TestMethod]
		public void ShowPrior_KeepsTheClinicFilter() {
			string easyNoClinics;
			using(MySqlConnector.MySqlConnection connection=ReportsDb.OpenServer(ReportsDb.DbName)) {
				easyNoClinics=ReportsDb.Scalar(connection,"SELECT ValueString FROM preference WHERE PrefName='EasyNoClinics'");
			}
			try {
				ReportsDb.Seed(
					"INSERT INTO clinic (ClinicNum,Abbr,Description,IsHidden) VALUES (990011,'SynA','Synthetic A',0),(990012,'SynB','Synthetic B',0)",
					"UPDATE preference SET ValueString='0' WHERE PrefName='EasyNoClinics'",
					//Seen at clinic A; a prior balance at A and one at B.
					Patient(914001,"ClinicA"),
					Proc(914101,914001,"2031-04-12",25,990011),
					Proc(914102,914001,"2030-04-01",35,990011),
					Proc(914103,914001,"2030-04-02",45,990012),
					//Seen only at clinic B, with a prior balance at A.
					Patient(914002,"ClinicB"),
					Proc(914104,914002,"2031-04-12",55,990012),
					Proc(914105,914002,"2030-04-03",65,990011)
				);
				List<DataRow> listRows=Run("2031-04-10","2031-04-20",true,new List<long> { 990011 },914001,914002);
				//Clinic A: the visit, and the prior balance at clinic A of the patient seen there; nothing of the patient seen only at clinic B.
				CollectionAssert.AreEqual(new[] { 35.0,25.0 },listRows.Select(x => Value(x,"Fee")).ToArray());
				CollectionAssert.AreEqual(new[] { true,false },listRows.Select(x => IsPrior(x)).ToArray());
				listRows=Run("2031-04-10","2031-04-20",true,new List<long> { 990011,990012 },914001,914002);
				CollectionAssert.AreEqual(new[] { 35.0,45.0,65.0,25.0,55.0 },listRows.Select(x => Value(x,"Fee")).ToArray(),"Both clinics: every row.");
			}
			finally {
				ReportsDb.Seed(
					"UPDATE preference SET ValueString='"+easyNoClinics+"' WHERE PrefName='EasyNoClinics'",
					"DELETE FROM clinic WHERE ClinicNum IN (990011,990012)"
				);
			}
		}

		[TestMethod]
		public void PayPlan_PatientPaymentPlanCredits() {
			ReportsDb.Seed(
				Patient(915001,"Plan"),
				//Fee 300 with two treatment credits on a patient payment plan (200 and 50), and 40 paid through the plan.
				Proc(915101,915001,"2031-05-12",300),
				PayPlan(915501,915001,0,false),
				PayPlanCharge(915601,915501,915001,915101,200,PayPlanChargeType.Credit),
				PayPlanCharge(915602,915501,915001,915101,50,PayPlanChargeType.Credit),
				PayPlanCharge(915603,915501,915001,915101,100,PayPlanChargeType.Debit),//A charge due, not a credit.
				Split(915401,915001,915101,40,915501,915603),
				//A credit of an insurance payment plan does not count.
				Proc(915102,915001,"2031-05-13",70),
				PayPlan(915502,915001,915999,false),
				PayPlanCharge(915604,915502,915001,915102,70,PayPlanChargeType.Credit),
				//On a plan but paid in full: not listed.
				Proc(915103,915001,"2031-05-14",80),
				PayPlanCharge(915605,915501,915001,915103,80,PayPlanChargeType.Credit),
				Split(915402,915001,915103,80,915501)
			);
			List<DataRow> listRows=Run("2031-05-10","2031-05-20",false,915001);
			Assert.AreEqual(2,listRows.Count);
			Assert.AreEqual(300,Value(listRows[0],"Fee"),0.001);
			Assert.AreEqual(40,Value(listRows[0],"Payment"),0.001,"Payments through the plan are patient payments.");
			Assert.AreEqual(260,Value(listRows[0],"Uncollected"),0.001,"The Pay Plan column does not change Uncollected.");
			Assert.AreEqual(250,Value(listRows[0],RpPatPortionUncollected.ColumnPayPlan),0.001);
			Assert.AreEqual(70,Value(listRows[1],"Uncollected"),0.001);
			Assert.AreEqual(0,Value(listRows[1],RpPatPortionUncollected.ColumnPayPlan),0.001,"Insurance payment plan.");
		}

		[TestMethod]
		public void PayPlan_DynamicPaymentPlanLinks() {
			ReportsDb.Seed(
				Patient(916001,"Dynamic"),
				PayPlan(916501,916001,0,true),
				//Linked with an amount override: the override.
				Proc(916101,916001,"2031-06-12",200),
				PayPlanLink(916701,916501,916101,120),
				//Linked without an override: what remains of the procedure after insurance, adjustments and payments outside the plan.
				//Fee 150, insurance 50, adjustment -10, 30 paid outside the plan, 20 through the plan: 150-50-10-30=60 on the plan, 40 uncollected.
				Proc(916102,916001,"2031-06-13",150),
				ClaimProc(916201,916102,916001,ClaimProcStatus.NotReceived,50,0,0),
				Adjustment(916301,916001,916102,-10,"2031-06-13"),
				Split(916401,916001,916102,30),
				Split(916402,916001,916102,20,916501),
				PayPlanLink(916702,916501,916102,0),
				//Not on a plan.
				Proc(916103,916001,"2031-06-14",10)
			);
			List<DataRow> listRows=Run("2031-06-10","2031-06-20",false,916001);
			Assert.AreEqual(3,listRows.Count);
			Assert.AreEqual(120,Value(listRows[0],RpPatPortionUncollected.ColumnPayPlan),0.001);
			Assert.AreEqual(200,Value(listRows[0],"Uncollected"),0.001);
			Assert.AreEqual(60,Value(listRows[1],RpPatPortionUncollected.ColumnPayPlan),0.001);
			Assert.AreEqual(50,Value(listRows[1],"Payment"),0.001);
			Assert.AreEqual(40,Value(listRows[1],"Uncollected"),0.001);
			Assert.AreEqual(0,Value(listRows[2],RpPatPortionUncollected.ColumnPayPlan),0.001);
		}

		[TestMethod]
		public void PayPlan_IsShownOnPriorRowsToo() {
			ReportsDb.Seed(
				Patient(917001,"PriorPlan"),
				Proc(917101,917001,"2031-07-12",10),
				Proc(917102,917001,"2030-07-01",400),
				PayPlan(917501,917001,0,false),
				PayPlanCharge(917601,917501,917001,917102,400,PayPlanChargeType.Credit)
			);
			List<DataRow> listRows=Run("2031-07-10","2031-07-20",true,917001);
			Assert.AreEqual(2,listRows.Count);
			Assert.IsTrue(IsPrior(listRows[0]));
			Assert.AreEqual(400,Value(listRows[0],RpPatPortionUncollected.ColumnPayPlan),0.001);
			Assert.AreEqual(400,Value(listRows[0],"Uncollected"),0.001);
		}
	}
}
