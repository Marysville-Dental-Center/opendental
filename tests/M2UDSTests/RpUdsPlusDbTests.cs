// The UDS+ report against a scratch database with the reference table definitions and synthetic rows; every expected number is hand-computed
// below. Codes are made-up placeholders (X-codes).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2UDSTests {
	[TestClass]
	public class RpUdsPlusDbTests {
		private static string _dbName;
		private static string _skipReason;
		private static readonly DateTime _dateFrom=new DateTime(2025,1,1);
		private static readonly DateTime _dateTo=new DateTime(2025,12,31);

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=UdsTestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			_dbName=UdsTestDb.ScratchName("report");
			UdsTestDb.Create(_dbName,"patient","procedurelog","procedurecode","patientrace","cdcrec","patplan","inssub","insplan","insfilingcode");
			int complete=(int)ProcStat.C;
			UdsTestDb.Seed(_dbName,
				"INSERT INTO procedurecode (CodeNum,ProcCode) VALUES (1,'X100'),(2,'X210'),(3,'X500'),(4,'X900'),(5,'X220')",
				//1 and 2 active, 3 deleted, 4 inactive (still a patient who was seen).
				"INSERT INTO patient (PatNum,LName,FName,PatStatus,Birthdate,Gender,Zip,Language) VALUES "
					+"(1,'Adams','Ann',0,'1990-05-01',1,'97201','eng'),(2,'Baker','Bob',0,'2015-07-04',0,'97202','spa'),"
					+"(3,'Clark','Del',"+(int)PatientStatus.Deleted+",'1970-01-01',0,'97203',''),(4,'Diaz','Eve',"+(int)PatientStatus.Inactive+",'2000-02-02',1,'97204','')",
				"INSERT INTO procedurelog (ProcNum,PatNum,CodeNum,ProcDate,ProcStatus,ClinicNum,ProvNum) VALUES "
					+"(1,1,2,'2025-02-01',"+complete+",1,1),"//exam
					+"(2,1,2,'2025-02-01',"+complete+",1,1),"//the same exam again: one service
					+"(3,1,1,'2025-02-01',"+complete+",1,2),"//emergency, provider 2
					+"(4,1,5,'2025-03-15',"+complete+",2,1),"//exam, clinic 2
					+"(5,2,3,'2025-02-01',"+complete+",1,1),"//restoration
					+"(6,2,1,'2025-06-30',"+(int)ProcStat.TP+",1,1),"//planned only
					+"(7,2,1,'2025-07-01',"+(int)ProcStat.D+",1,1),"//deleted procedure
					+"(8,3,1,'2025-02-01',"+complete+",1,1),"//deleted patient
					+"(9,4,2,'2025-12-31',"+complete+",2,2),"//last day of the range
					+"(10,1,1,'2026-01-01',"+complete+",1,1),"//after the range
					+"(11,2,2,'2024-12-31',"+complete+",1,1),"//before the range
					+"(12,2,4,'2025-05-05',"+complete+",1,1),"//code on no line
					+"(13,4,1,'2025-08-08',"+(int)ProcStat.EC+",1,1)",//existing work, not done here
				"INSERT INTO cdcrec (CdcrecCode,HeirarchicalCode,Description) VALUES ('2106-3','R5','WHITE')",
				"INSERT INTO patientrace (PatNum,Race,CdcrecCode) VALUES (1,-1,'2106-3'),(2,-1,'2135-2'),(3,-1,'2106-3')",
				"INSERT INTO insfilingcode (InsFilingCodeNum,Descript,EclaimCode,ItemOrder,GroupType) VALUES (1,'Medicaid','MC',0,0)",
				"INSERT INTO insplan (PlanNum,FilingCode) VALUES (1,1),(2,0)",
				"INSERT INTO inssub (InsSubNum,PlanNum,Subscriber) VALUES (1,1,1),(2,2,2),(3,2,3)",
				"INSERT INTO patplan (PatNum,Ordinal,InsSubNum) VALUES (1,1,1),(2,1,2),(3,1,3)");
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_dbName!=null) {
				UdsTestDb.Drop(_dbName);
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			UdsTestDb.Connect(_dbName);
		}

		private static UdsPlusLineMap GetMap() {
			UdsPlusLineMap udsPlusLineMap=new UdsPlusLineMap();
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="27",Description="Emergency services",Codes="X100" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="28",Description="Oral exams",Codes="X200-X299" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="32",Description="Restorative services",Codes="X5*" });
			return udsPlusLineMap;
		}

		private static string Counts(UdsPlusResult udsPlusResult) {
			return string.Join(" ",udsPlusResult.ListLineCounts.Select(x => x.Line+":"+x.Visits+"/"+x.Patients))
				+" all:"+udsPlusResult.TotalVisits+"/"+udsPlusResult.TotalPatients;
		}

		[TestMethod]
		public void Year_CountsCompletedProceduresOfPatientsNotDeleted() {
			UdsPlusResult udsPlusResult=RpUdsPlus.Run(GetMap(),_dateFrom,_dateTo,new List<long>(),new List<long>());
			//27: Ann Feb 1 (planned, deleted, existing, deleted-patient and out-of-range rows left out).
			//28: Ann Feb 1 (two exams, one service), Ann Mar 15, Eve Dec 31. 32: Bob Feb 1.
			//Any line: Ann Feb 1, Ann Mar 15, Bob Feb 1, Eve Dec 31; patients Ann, Bob, Eve.
			Assert.AreEqual("27:1/1 28:3/2 32:1/1 all:4/3",Counts(udsPlusResult));
			Assert.AreEqual(5,udsPlusResult.ListVisitRows.Count);
		}

		[TestMethod]
		public void Filters_ClinicsProvidersAndDates() {
			Assert.AreEqual("27:1/1 28:1/1 32:1/1 all:2/2",
				Counts(RpUdsPlus.Run(GetMap(),_dateFrom,_dateTo,new List<long> { 1 },new List<long>())),"Clinic 1: Ann Feb 1, Bob Feb 1.");
			Assert.AreEqual("27:1/1 28:1/1 32:0/0 all:2/2",
				Counts(RpUdsPlus.Run(GetMap(),_dateFrom,_dateTo,new List<long>(),new List<long> { 2 })),"Provider 2: Ann's emergency, Eve's exam.");
			Assert.AreEqual("27:1/1 28:1/1 32:1/1 all:2/2",
				Counts(RpUdsPlus.Run(GetMap(),new DateTime(2025,2,1),new DateTime(2025,2,1),new List<long>(),new List<long>())),"One day.");
			Assert.AreEqual("27:0/0 28:1/1 32:0/0 all:1/1",
				Counts(RpUdsPlus.Run(GetMap(),new DateTime(2025,12,31),new DateTime(2026,1,1),new List<long> { 2 },new List<long>())),
				"Clinic 2 over the turn of the year: Eve Dec 31 (Ann's Jan 1 emergency is clinic 1).");
		}

		[TestMethod]
		public void Rows_CarryDemographicsAndInsurance() {
			List<UdsPlusVisitRow> listRows=RpUdsPlus.Run(GetMap(),_dateFrom,_dateTo,new List<long>(),new List<long>()).ListVisitRows;
			CollectionAssert.AreEqual(new[] { "1|2025-02-01|27|X100","1|2025-02-01|28|X210","1|2025-03-15|28|X220","2|2025-02-01|32|X500",
				"4|2025-12-31|28|X210" },
				listRows.Select(x => x.PatNum+"|"+x.VisitDate.ToString("yyyy-MM-dd")+"|"+x.UdsLine+"|"+x.ProcCodes).ToArray());
			UdsPlusVisitRow rowAnn=listRows[0];
			Assert.AreEqual(new DateTime(1990,5,1),rowAnn.DateOfBirth);
			Assert.AreEqual("Female",rowAnn.Sex);
			Assert.AreEqual("97201",rowAnn.Zip);
			Assert.AreEqual("eng",rowAnn.PreferredLanguage);
			Assert.AreEqual("WHITE",rowAnn.Race);
			Assert.AreEqual("",rowAnn.Ethnicity);
			Assert.AreEqual(RpUdsPlus.INS_MEDICAID,rowAnn.InsuranceType);
			Assert.AreEqual("MC",rowAnn.InsuranceFilingCode);
			UdsPlusVisitRow rowBob=listRows[3];
			Assert.AreEqual("Male",rowBob.Sex);
			Assert.AreEqual("",rowBob.Race);
			Assert.AreEqual("Hispanic or Latino",rowBob.Ethnicity,"No cdcrec row: the OMB category is still named.");
			Assert.AreEqual(RpUdsPlus.INS_PRIVATE,rowBob.InsuranceType,"Plan without a filing code.");
			Assert.AreEqual(RpUdsPlus.INS_NONE,listRows[4].InsuranceType,"Eve has no plan.");
		}

		[TestMethod]
		public void DataSet_HoldsOnlyTheReportPatients() {
			List<long> listCodeNums=RpUdsPlus.GetCodeNums(GetMap(),RpUdsPlus.GetProcCodeTable());
			CollectionAssert.AreEquivalent(new List<long> { 1,2,3,5 },listCodeNums,"X900 is on no line.");
			DataSet dataSet=RpUdsPlus.GetDataSet(_dateFrom,_dateTo,new List<long>(),new List<long>(),listCodeNums);
			List<long> listPatNumsRace=dataSet.Tables["Races"].Select().Select(x => PIn.Long(x["PatNum"].ToString())).ToList();
			List<long> listPatNumsIns=dataSet.Tables["Insurance"].Select().Select(x => PIn.Long(x["PatNum"].ToString())).ToList();
			CollectionAssert.AreEquivalent(new List<long> { 1,2 },listPatNumsRace,"The deleted patient's race row is not read.");
			CollectionAssert.AreEquivalent(new List<long> { 1,2 },listPatNumsIns,"Nor its plan.");
			Assert.AreEqual(6,dataSet.Tables["Procs"].Rows.Count,"Procedures 1, 2, 3, 4, 5 and 9.");
		}

		[TestMethod]
		public void EmptyOrUnmatchedMap_CountsNothingWithoutAQuery() {
			UdsPlusLineMap udsPlusLineMap=new UdsPlusLineMap();
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="30",Description="Sealants",Codes="Y1" });
			UdsPlusResult udsPlusResult=RpUdsPlus.Run(udsPlusLineMap,_dateFrom,_dateTo,new List<long>(),new List<long>());
			Assert.AreEqual("30:0/0 all:0/0",Counts(udsPlusResult));
			Assert.AreEqual(0,RpUdsPlus.Run(new UdsPlusLineMap(),_dateFrom,_dateTo,new List<long>(),new List<long>()).ListLineCounts.Count);
			try {
				RpUdsPlus.GetProcFromWhere(_dateFrom,_dateTo,null,null,new List<long>());
				Assert.Fail("An empty code list is refused rather than written as IN ().");
			}
			catch(ArgumentException) {
			}
		}
	}
}
