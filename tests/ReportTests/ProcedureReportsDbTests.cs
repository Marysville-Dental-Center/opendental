// Golden numbers for the itemized Procedures report and Production by Procedure (24.4 split of the Daily Procedures report), on a scratch
// database with the Open Dental 25.3.48 table definitions and synthetic rows. The expected totals are worked out by hand below.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ReportTests {
	[TestClass]
	public class ProcedureReportsDbTests {
		private static string _dbName;
		private static string _skipReason;
		private static readonly DateTime _dateFrom=new DateTime(2026,3,1);
		private static readonly DateTime _dateTo=new DateTime(2026,3,5);

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ReportTestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			_dbName=ReportTestDb.ScratchName("procs");
			ReportTestDb.Create(_dbName,"patient","procedurelog","procedurecode","provider","clinic","claimproc","definition");
			int statusC=(int)ProcStat.C;
			int statusTP=(int)ProcStat.TP;
			int capComplete=(int)ClaimProcStatus.CapComplete;
			int received=(int)ClaimProcStatus.Received;
			ReportTestDb.Seed(_dbName,
				//Synthetic procedure codes (not real code-set codes) in two categories.
				"INSERT INTO definition (DefNum,Category,ItemOrder,ItemName) VALUES (1,11,0,'Category A'),(2,11,1,'Category B')",
				"INSERT INTO procedurecode (CodeNum,ProcCode,Descript,ProcCat) VALUES (1,'X0101','Synthetic exam',1),(2,'X2201','Synthetic filling',2),"
					+"(3,'X2202','Synthetic crown',2)",
				"INSERT INTO provider (ProvNum,Abbr,LName,FName) VALUES (1,'DOC1','Doc','One'),(2,'HYG1','Hyg','One')",
				"INSERT INTO clinic (ClinicNum,Abbr,Description,IsHidden) VALUES (1,'North','North',0),(2,'South','South',1)",
				"INSERT INTO patient (PatNum,LName,FName,MiddleI) VALUES (1,'Adams','Ann',''),(2,'Baker','Bob','')",
				//In range and complete: P1 50, P2 120 less a CapComplete write-off of 20 = 100, P3 2 units of 120 = 240, P4 900. Total 1290.
				"INSERT INTO procedurelog (ProcNum,PatNum,CodeNum,ProvNum,ClinicNum,ProcDate,ProcFee,UnitQty,BaseUnits,ProcStatus,ToothNum,Surf) VALUES "
					+"(1,1,1,1,1,'2026-03-02',50,1,0,"+statusC+",'',''),"
					+"(2,1,2,1,1,'2026-03-02',120,1,0,"+statusC+",'3','O'),"
					+"(3,2,2,2,2,'2026-03-03',120,2,0,"+statusC+",'14','MO'),"
					+"(4,2,3,1,0,'2026-03-03',900,1,0,"+statusC+",'19',''),"
					//Excluded: treatment planned, and complete but before the range.
					+"(5,2,1,1,1,'2026-03-04',50,1,0,"+statusTP+",'',''),"
					+"(6,1,1,2,1,'2026-02-28',50,1,0,"+statusC+",'','')",
				//Only CapComplete write-offs are subtracted; the Received write-off on P1 is not.
				"INSERT INTO claimproc (ClaimProcNum,ProcNum,PatNum,Status,WriteOff) VALUES (1,2,1,"+capComplete+",20),(2,1,1,"+received+",10)");
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

		private static DataTable GetItemized(List<long> listProvNums,List<long> listClinicNums,string procCode,bool hasAllProvs,bool hasClinicsEnabled) {
			return DataCore.GetTable(RpProcSheet.GetIndividualQuery(_dateFrom,_dateTo,listProvNums,listClinicNums,procCode,false,hasAllProvs,hasClinicsEnabled,true));
		}

		private static DataTable GetGrouped(List<long> listProvNums,List<long> listClinicNums,string procCode,bool hasAllProvs,bool hasClinicsEnabled) {
			return DataCore.GetTable(RpProcSheet.GetGroupedQuery(_dateFrom,_dateTo,listProvNums,listClinicNums,procCode,hasAllProvs,hasClinicsEnabled));
		}

		private static double SumItemized(DataTable table) {
			return table.Select().Sum(x => Convert.ToDouble(x["$fee"]));
		}

		private static double SumGrouped(DataTable table) {
			return table.Select().Sum(x => Convert.ToDouble(x["$TotFee"]));
		}

		[TestMethod]
		public void Itemized_ListsEachCompletedProcedureInRange_WithKeys() {
			DataTable table=GetItemized(new List<long>(),new List<long>(),"",true,false);
			CollectionAssert.AreEqual(new List<long> { 1,2,3,4 },table.Select().Select(x => Convert.ToInt64(x["ProcNum"])).ToList());
			Assert.AreEqual(1290,SumItemized(table),0.001);
			DataRow rowFilling=table.Select().First(x => Convert.ToInt64(x["ProcNum"])==2);
			Assert.AreEqual(100,Convert.ToDouble(rowFilling["$fee"]),0.001,"Fee less the CapComplete write-off.");
			Assert.AreEqual(1L,Convert.ToInt64(rowFilling["PatNum"]));
			Assert.AreEqual("Adams, Ann ",rowFilling["plfname"].ToString());
			Assert.AreEqual("O",rowFilling["Area"].ToString());
			Assert.AreEqual("DOC1",rowFilling["Abbr"].ToString());
			Assert.AreEqual(240,Convert.ToDouble(table.Select().First(x => Convert.ToInt64(x["ProcNum"])==3)["$fee"]),0.001,"Fee times units.");
		}

		[TestMethod]
		public void Grouped_GroupsByCode_InCategoryOrder() {
			DataTable table=GetGrouped(new List<long>(),new List<long>(),"",true,false);
			CollectionAssert.AreEqual(new List<string> { "X0101","X2201","X2202" },table.Select().Select(x => x["ProcCode"].ToString()).ToList());
			Assert.AreEqual("Category A",table.Rows[0]["ItemName"].ToString());
			Assert.AreEqual(2L,Convert.ToInt64(table.Rows[1][3]),"Quantity of X2201.");
			Assert.AreEqual(340,Convert.ToDouble(table.Rows[1]["$TotFee"]),0.001);
			Assert.AreEqual("170.00",table.Rows[1]["$AvgFee"].ToString());
			Assert.AreEqual(900,Convert.ToDouble(table.Rows[2]["$TotFee"]),0.001);
		}

		[TestMethod]
		public void TotalsAgree_ForTheSameFilters() {
			var listCases=new List<(string Name,List<long> ProvNums,List<long> ClinicNums,string Code,bool HasAllProvs,bool HasClinics,double Expected)> {
				("all",new List<long>(),new List<long>(),"",true,false,1290),
				("provider 1",new List<long> { 1 },new List<long>(),"",false,false,1050),
				("provider 2",new List<long> { 2 },new List<long>(),"",false,false,240),
				("clinic North",new List<long>(),new List<long> { 1 },"",true,true,150),
				("clinics Unassigned and South (hidden)",new List<long>(),new List<long> { 0,2 },"",true,true,1140),
				("code like x22",new List<long>(),new List<long>(),"x22",true,false,1240),
				("no provider picked",new List<long>(),new List<long>(),"",false,false,0),
			};
			foreach(var testCase in listCases) {
				double sumItemized=SumItemized(GetItemized(testCase.ProvNums,testCase.ClinicNums,testCase.Code,testCase.HasAllProvs,testCase.HasClinics));
				double sumGrouped=SumGrouped(GetGrouped(testCase.ProvNums,testCase.ClinicNums,testCase.Code,testCase.HasAllProvs,testCase.HasClinics));
				Assert.AreEqual(testCase.Expected,sumItemized,0.001,testCase.Name+": itemized");
				Assert.AreEqual(testCase.Expected,sumGrouped,0.001,testCase.Name+": grouped");
			}
		}

		[TestMethod]
		public void Itemized_ShowsHiddenAndUnassignedClinics() {
			DataTable table=GetItemized(new List<long>(),new List<long>(),"",true,true);
			Dictionary<long,string> dictClinic=table.Select().ToDictionary(x => Convert.ToInt64(x["ProcNum"]),x => x["Clinic"].ToString());
			Assert.AreEqual("North",dictClinic[1]);
			Assert.AreEqual("South(hidden)",dictClinic[3]);
			Assert.AreEqual("Unassigned",dictClinic[4]);
		}

		[TestMethod]
		public void MedicalClinics_LeaveOutTheToothColumn() {
			string query=RpProcSheet.GetIndividualQuery(_dateFrom,_dateTo,new List<long>(),new List<long>(),"",true,true,false,true);
			DataTable table=DataCore.GetTable(query);
			Assert.IsFalse(table.Columns.Contains("ToothNum"));
			Assert.AreEqual(4,table.Rows.Count);
		}

		[TestMethod]
		public void GetProceduresTable_RunsThroughTheReportServerPath() {
			DataTable table=RpProcSheet.GetProceduresTable(_dateFrom,_dateTo,new List<long>(),new List<long>(),"",false,true,false);
			Assert.AreEqual(4,table.Rows.Count);
			Assert.AreEqual(1290,SumItemized(table),0.001);
		}
	}
}
