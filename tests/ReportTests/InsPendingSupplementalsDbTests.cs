// Insurance Pending Supplementals report (25.2) on a scratch database with the Open Dental 25.3.48 table definitions and synthetic rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using Filter=OpenDentBusiness.RpInsPendingSupplementals.PendingSupplementalFilter;

namespace ReportTests {
	[TestClass]
	public class InsPendingSupplementalsDbTests {
		private static string _dbName;
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ReportTestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			_dbName=ReportTestDb.ScratchName("pendsupp");
			ReportTestDb.Create(_dbName,"patient","claimproc","claim","insplan","carrier");
			int notReceived=(int)ClaimProcStatus.NotReceived;
			int supplemental=(int)ClaimProcStatus.Supplemental;
			ReportTestDb.Seed(_dbName,
				"INSERT INTO patient (PatNum,LName,FName) VALUES (1,'Adams','Ann'),(2,'Baker','Bob')",
				"INSERT INTO carrier (CarrierNum,CarrierName) VALUES (1,'Synthetic Mutual'),(2,'Sample Dental Co')",
				"INSERT INTO insplan (PlanNum,CarrierNum) VALUES (1,1),(2,2)",
				"INSERT INTO claim (ClaimNum,PatNum,PlanNum,DateSent) VALUES (1,1,1,'2026-04-10'),(2,2,2,'0001-01-01')",
				"INSERT INTO claimproc (ClaimProcNum,ClaimNum,PatNum,PlanNum,ProcNum,IsOverpay,Status,InsEstTotalOverride,ProcDate,ClinicNum) VALUES "
					//Listed: an overpayment, an underpayment, and another overpayment with no clinic.
					+"(1,1,1,1,11,1,"+notReceived+",-40,'2026-04-01',1),"
					+"(2,2,2,2,12,1,"+notReceived+",25.5,'2026-04-05',2),"
					+"(7,2,2,2,17,1,"+notReceived+",-10,'2026-05-01',0),"
					//Not listed: amount cleared to zero, already converted to a supplemental, a regular claimproc, and an amount that rounds to zero.
					+"(3,1,1,1,13,1,"+notReceived+",0,'2026-04-01',1),"
					+"(4,1,1,1,14,0,"+supplemental+",-40,'2026-04-01',1),"
					+"(5,1,1,1,15,0,"+notReceived+",100,'2026-04-01',1),"
					+"(6,2,2,2,16,1,"+notReceived+",-0.004,'2026-04-05',2)");
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_dbName!=null) {
				ReportTestDb.Drop(_dbName);
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ReportTestDb.Connect(_dbName);
		}

		private static DataTable Run(List<long> listClinicNums,Filter filter,DateTime dateFrom,DateTime dateTo) {
			return DataCore.GetTable(RpInsPendingSupplementals.GetQuery(listClinicNums,filter,dateFrom,dateTo));
		}

		private static List<long> Nums(DataTable table) {
			return table.Select().Select(x => Convert.ToInt64(x["ClaimProcNum"])).ToList();
		}

		[TestMethod]
		public void All_ListsOutstandingPendingSupplementalsOnly() {
			DataTable table=Run(new List<long>(),Filter.All,DateTime.MinValue,DateTime.MinValue);
			CollectionAssert.AreEqual(new List<long> { 1,2,7 },Nums(table),"Ordered by date of service.");
			Assert.AreEqual(-24.5,table.Select().Sum(x => Convert.ToDouble(x["Amount"])),0.001);
			DataRow row1=table.Rows[0];
			Assert.AreEqual("Adams, Ann",row1["PatName"].ToString());
			Assert.AreEqual("Synthetic Mutual",row1["CarrierName"].ToString());
			Assert.AreEqual(new DateTime(2026,4,10),PIn.Date(row1["DateSent"].ToString()));
			Assert.AreEqual(1L,Convert.ToInt64(row1["ClaimNum"]));
			Assert.AreEqual(1L,Convert.ToInt64(row1["ClinicNum"]));
			Assert.AreEqual(new DateTime(2026,4,1),PIn.Date(row1["ProcDate"].ToString()));
			Assert.AreEqual("Sample Dental Co",table.Rows[1]["CarrierName"].ToString());
			Assert.IsTrue(PIn.Date(table.Rows[1]["DateSent"].ToString()).Year<1880,"A claim that was never sent has no date.");
		}

		[TestMethod]
		public void TypeFilter_UsesTheSignOfTheAmount() {
			CollectionAssert.AreEqual(new List<long> { 1,7 },Nums(Run(new List<long>(),Filter.Overpaid,DateTime.MinValue,DateTime.MinValue)));
			CollectionAssert.AreEqual(new List<long> { 2 },Nums(Run(new List<long>(),Filter.Underpaid,DateTime.MinValue,DateTime.MinValue)));
		}

		[TestMethod]
		public void ClinicFilter_LimitsToTheGivenClinics() {
			CollectionAssert.AreEqual(new List<long> { 1 },Nums(Run(new List<long> { 1 },Filter.All,DateTime.MinValue,DateTime.MinValue)));
			CollectionAssert.AreEqual(new List<long> { 2,7 },Nums(Run(new List<long> { 0,2 },Filter.All,DateTime.MinValue,DateTime.MinValue)));
		}

		[TestMethod]
		public void DateFilter_BlankSideMeansNoLimit() {
			CollectionAssert.AreEqual(new List<long> { 2,7 },Nums(Run(new List<long>(),Filter.All,new DateTime(2026,4,2),DateTime.MinValue)));
			//The date range picker returns the end of the day for To, and a blank To as the first day plus 23:59:59.
			CollectionAssert.AreEqual(new List<long> { 1,2 },Nums(Run(new List<long>(),Filter.All,DateTime.MinValue,new DateTime(2026,4,30,23,59,59))));
			CollectionAssert.AreEqual(new List<long> { 1,2,7 },Nums(Run(new List<long>(),Filter.All,DateTime.MinValue,DateTime.MinValue.AddHours(23).AddMinutes(59))));
			CollectionAssert.AreEqual(new List<long> { 2 },Nums(Run(new List<long>(),Filter.All,new DateTime(2026,4,2),new DateTime(2026,4,30))));
		}

		[TestMethod]
		public void GetTable_RunsThroughTheReportServerPath() {
			DataTable table=RpInsPendingSupplementals.GetTable(new List<long>(),Filter.Overpaid,DateTime.MinValue,DateTime.MinValue);
			CollectionAssert.AreEqual(new List<long> { 1,7 },Nums(table));
		}
	}

	[TestClass]
	public class InsPendingSupplementalsKindTests {
		[TestMethod]
		public void GetKind_FromTheSign() {
			Assert.AreEqual(Filter.Overpaid,RpInsPendingSupplementals.GetKind(-40));
			Assert.AreEqual(Filter.Underpaid,RpInsPendingSupplementals.GetKind(25.5));
			Assert.AreEqual(Filter.All,RpInsPendingSupplementals.GetKind(0));
			Assert.AreEqual(Filter.All,RpInsPendingSupplementals.GetKind(-0.004),"Rounds to zero cents.");
		}

		[TestMethod]
		public void Query_AlwaysExcludesZeroAndRequiresPending() {
			string query=RpInsPendingSupplementals.GetQuery(new List<long>(),Filter.All,DateTime.MinValue,DateTime.MinValue);
			StringAssert.Contains(query,"claimproc.IsOverpay=1");
			StringAssert.Contains(query,"claimproc.Status="+(int)ClaimProcStatus.NotReceived);
			StringAssert.Contains(query,"ROUND(claimproc.InsEstTotalOverride,2)!=0");
			Assert.IsFalse(query.Contains("ClinicNum IN"),"No clinic filter for an empty list.");
			Assert.IsFalse(query.Contains("ProcDate>="),"No date filter for blank dates.");
		}
	}
}
