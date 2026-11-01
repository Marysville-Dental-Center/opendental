// UDS+ counting and the patient-level file, on hand-built rows (the shape RpUdsPlus.GetDataSet returns). No database.
// Codes are made-up placeholders (X-codes); patients are synthetic.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2UDSTests {
	[TestClass]
	public class RpUdsPlusComputeTests {
		private static UdsPlusLineMap GetMap() {
			UdsPlusLineMap udsPlusLineMap=new UdsPlusLineMap();
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="27",Description="Emergency services",Codes="X100" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="28",Description="Oral exams",Codes="X200-X299" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="32",Description="Restorative (direct, indirect)",Codes="X5*" });
			udsPlusLineMap.ListLines.Add(new UdsPlusLine { Line="34",Description="Rehabilitative",Codes="X800" });
			return udsPlusLineMap;
		}

		private static DataSet GetDataSet() {
			DataSet dataSet=new DataSet();
			DataTable tableProcs=new DataTable("Procs");
			foreach(string column in new[] { "PatNum","ProcDate","ProcCode","Birthdate","Gender","Zip","Language" }) {
				tableProcs.Columns.Add(column);
			}
			//Patient 1: one visit with a repeated exam, an emergency and a restoration; a second visit with an exam.
			AddProc(tableProcs,1,"2025-02-01","X210","1990-05-01",1,"97201","eng");
			AddProc(tableProcs,1,"2025-02-01","X210","1990-05-01",1,"97201","eng");
			AddProc(tableProcs,1,"2025-02-01","X500","1990-05-01",1,"97201","eng");
			AddProc(tableProcs,1,"2025-02-01","X100","1990-05-01",1,"97201","eng");
			AddProc(tableProcs,1,"2025-03-15","X220","1990-05-01",1,"97201","eng");
			//Patient 2: two restorations on one visit, an emergency on another.
			AddProc(tableProcs,2,"2025-02-01","X599","2015-07-04",0,"97202","spa");
			AddProc(tableProcs,2,"2025-02-01","X501","2015-07-04",0,"97202","spa");
			AddProc(tableProcs,2,"2025-06-30","X100","2015-07-04",0,"97202","spa");
			//Patient 3: a code on no line, so no visit.
			AddProc(tableProcs,3,"2025-04-01","X900","0001-01-01",2,"","");
			dataSet.Tables.Add(tableProcs);
			DataTable tableRaces=new DataTable("Races");
			foreach(string column in new[] { "PatNum","CdcrecCode","Description","HeirarchicalCode" }) {
				tableRaces.Columns.Add(column);
			}
			tableRaces.Rows.Add("1","2106-3","WHITE","R5");
			tableRaces.Rows.Add("1","2135-2","HISPANIC OR LATINO","E1");
			tableRaces.Rows.Add("2","2054-5","","");//cdcrec table not filled
			tableRaces.Rows.Add("2","2028-9","ASIAN","R2");
			tableRaces.Rows.Add("2","2186-5","","");
			tableRaces.Rows.Add("2","2186-5","","");//duplicate row, shown once
			tableRaces.Rows.Add("3",PatientRace.DECLINE_SPECIFY_RACE_CODE,"","");
			dataSet.Tables.Add(tableRaces);
			DataTable tableIns=new DataTable("Insurance");
			foreach(string column in new[] { "PatNum","Ordinal","DateEffective","DateTerm","EclaimCode" }) {
				tableIns.Columns.Add(column);
			}
			//Patient 1: Medicaid from 2025-03-01 (primary), commercial all along (secondary).
			tableIns.Rows.Add("1","1","2025-03-01","0001-01-01","MC");
			tableIns.Rows.Add("1","2","0001-01-01","0001-01-01","CI");
			//Patient 2: Medicare until 2025-06-29, Medicaid all along.
			tableIns.Rows.Add("2","1","0001-01-01","2025-06-29","MB");
			tableIns.Rows.Add("2","2","0001-01-01","0001-01-01","MC");
			dataSet.Tables.Add(tableIns);
			return dataSet;
		}

		private static void AddProc(DataTable table,long patNum,string procDate,string procCode,string birthdate,int gender,string zip,string language) {
			table.Rows.Add(patNum.ToString(),procDate,procCode,birthdate,gender.ToString(),zip,language);
		}

		[TestMethod]
		public void Compute_CountsVisitsAndPatientsPerLine() {
			UdsPlusResult udsPlusResult=RpUdsPlus.Compute(GetMap(),GetDataSet());
			//Hand-computed: line 27 = P1 Feb 1, P2 Jun 30; line 28 = P1 Feb 1 (exam twice, one service), P1 Mar 15; line 32 = P1 Feb 1, P2 Feb 1.
			CollectionAssert.AreEqual(new[] { "27","28","32","34" },udsPlusResult.ListLineCounts.Select(x => x.Line).ToArray(),"Setup order.");
			CollectionAssert.AreEqual(new[] { 2,2,2,0 },udsPlusResult.ListLineCounts.Select(x => x.Visits).ToArray(),"Visits");
			CollectionAssert.AreEqual(new[] { 2,1,2,0 },udsPlusResult.ListLineCounts.Select(x => x.Patients).ToArray(),"Patients");
			Assert.AreEqual("Oral exams",udsPlusResult.ListLineCounts[1].Description);
			//Any line: P1 Feb 1, P1 Mar 15, P2 Feb 1, P2 Jun 30. Patient 3 has nothing on a line.
			Assert.AreEqual(4,udsPlusResult.TotalVisits);
			Assert.AreEqual(2,udsPlusResult.TotalPatients);
		}

		[TestMethod]
		public void Compute_OneRowPerPatientVisitAndLine() {
			List<UdsPlusVisitRow> listRows=RpUdsPlus.Compute(GetMap(),GetDataSet()).ListVisitRows;
			CollectionAssert.AreEqual(new[] { "1|2025-02-01|27|X100","1|2025-02-01|28|X210","1|2025-02-01|32|X500","1|2025-03-15|28|X220",
				"2|2025-02-01|32|X501;X599","2|2025-06-30|27|X100" },
				listRows.Select(x => x.PatNum+"|"+x.VisitDate.ToString("yyyy-MM-dd")+"|"+x.UdsLine+"|"+x.ProcCodes).ToArray());
			UdsPlusVisitRow row=listRows[0];
			Assert.AreEqual(new DateTime(1990,5,1),row.DateOfBirth);
			Assert.AreEqual("Female",row.Sex);
			Assert.AreEqual("97201",row.Zip);
			Assert.AreEqual("eng",row.PreferredLanguage);
			Assert.AreEqual("WHITE",row.Race);
			Assert.AreEqual("2106-3",row.RaceCodes);
			Assert.AreEqual("HISPANIC OR LATINO",row.Ethnicity);
			Assert.AreEqual("2135-2",row.EthnicityCodes);
			Assert.AreEqual("Emergency services",row.UdsService);
			UdsPlusVisitRow rowP2=listRows[4];
			Assert.AreEqual("Male",rowP2.Sex);
			Assert.AreEqual("Black or African American; ASIAN",rowP2.Race,"Named without the cdcrec table too.");
			Assert.AreEqual("2054-5; 2028-9",rowP2.RaceCodes);
			Assert.AreEqual("Not Hispanic or Latino",rowP2.Ethnicity,"Known as an ethnicity without the cdcrec table; the duplicate is shown once.");
		}

		[TestMethod]
		public void Compute_InsuranceTypeOnTheVisitDate() {
			List<UdsPlusVisitRow> listRows=RpUdsPlus.Compute(GetMap(),GetDataSet()).ListVisitRows;
			//P1 Feb 1: Medicaid not yet in effect, so the commercial plan decides.
			Assert.AreEqual(RpUdsPlus.INS_PRIVATE,listRows[0].InsuranceType);
			Assert.AreEqual("CI",listRows[0].InsuranceFilingCode);
			//P1 Mar 15: Medicaid is primary.
			Assert.AreEqual(RpUdsPlus.INS_MEDICAID,listRows[3].InsuranceType);
			Assert.AreEqual("MC",listRows[3].InsuranceFilingCode);
			//P2 Feb 1: Medicare and Medicaid.
			Assert.AreEqual(RpUdsPlus.INS_DUAL,listRows[4].InsuranceType);
			Assert.AreEqual("MB",listRows[4].InsuranceFilingCode);
			//P2 Jun 30: Medicare ended the day before.
			Assert.AreEqual(RpUdsPlus.INS_MEDICAID,listRows[5].InsuranceType);
		}

		[TestMethod]
		public void InsuranceCategory_FromFilingCode() {
			Assert.AreEqual(RpUdsPlus.INS_MEDICAID,RpUdsPlus.GetInsuranceCategory("mc"));
			Assert.AreEqual(RpUdsPlus.INS_MEDICARE,RpUdsPlus.GetInsuranceCategory("MA"));
			Assert.AreEqual(RpUdsPlus.INS_MEDICARE,RpUdsPlus.GetInsuranceCategory("16"));
			Assert.AreEqual(RpUdsPlus.INS_OTHER_PUBLIC,RpUdsPlus.GetInsuranceCategory("VA"));
			Assert.AreEqual(RpUdsPlus.INS_OTHER_PUBLIC,RpUdsPlus.GetInsuranceCategory("CH"));
			Assert.AreEqual(RpUdsPlus.INS_PRIVATE,RpUdsPlus.GetInsuranceCategory("HM"));
			Assert.AreEqual(RpUdsPlus.INS_PRIVATE,RpUdsPlus.GetInsuranceCategory(""),"No filing code: claims go out as commercial.");
			RpUdsPlus.GetInsuranceType(null,new DateTime(2025,1,1),out string insuranceType,out string filingCode);
			Assert.AreEqual(RpUdsPlus.INS_NONE,insuranceType);
			Assert.AreEqual("",filingCode);
		}

		[TestMethod]
		public void RaceRow_SpecialCodes() {
			RpUdsPlus.RaceRow raceRow=RpUdsPlus.GetRaceRow(PatientRace.DECLINE_SPECIFY_ETHNICITY_CODE,"","");
			Assert.IsTrue(raceRow.IsEthnicity);
			Assert.AreEqual(RpUdsPlus.DECLINED,raceRow.Description);
			raceRow=RpUdsPlus.GetRaceRow(PatientRace.MULTI_RACE_CODE,"","");
			Assert.IsFalse(raceRow.IsEthnicity);
			Assert.AreEqual(RpUdsPlus.MULTIRACIAL,raceRow.Description);
			raceRow=RpUdsPlus.GetRaceRow("9999-9","","");
			Assert.AreEqual("9999-9",raceRow.Description,"An unknown code without a cdcrec row shows the code.");
			Assert.IsFalse(raceRow.IsEthnicity);
			Assert.IsTrue(RpUdsPlus.GetRaceRow("2137-8","SPANIARD","E1.01").IsEthnicity,"cdcrec's hierarchy decides when present.");
		}

		[TestMethod]
		public void Compute_NoDataGivesZeroLines() {
			UdsPlusResult udsPlusResult=RpUdsPlus.Compute(GetMap(),null);
			Assert.AreEqual(4,udsPlusResult.ListLineCounts.Count);
			Assert.IsTrue(udsPlusResult.ListLineCounts.All(x => x.Visits==0 && x.Patients==0));
			Assert.AreEqual(0,udsPlusResult.ListVisitRows.Count);
			Assert.AreEqual(0,udsPlusResult.TotalVisits);
		}

		[TestMethod]
		public void Csv_HeaderRowsAndQuoting() {
			List<UdsPlusVisitRow> listRows=RpUdsPlus.Compute(GetMap(),GetDataSet()).ListVisitRows;
			string csv=RpUdsPlus.ToCsv(listRows);
			string[] arrayLines=csv.Split(new[] { "\r\n" },StringSplitOptions.None);
			Assert.AreEqual(listRows.Count+2,arrayLines.Length,"Header, rows and the empty piece after the last line end.");
			Assert.AreEqual("",arrayLines.Last());
			Assert.AreEqual("PatNum,DateOfBirth,Sex,Zip,Race,RaceCodes,Ethnicity,EthnicityCodes,PreferredLanguage,InsuranceType,InsuranceFilingCode,"
				+"VisitDate,ProcCodes,UdsLine,UdsService",arrayLines[0]);
			Assert.AreEqual("1,1990-05-01,Female,97201,WHITE,2106-3,HISPANIC OR LATINO,2135-2,eng,Private,CI,2025-02-01,X100,27,Emergency services",
				arrayLines[1]);
			Assert.AreEqual("1,1990-05-01,Female,97201,WHITE,2106-3,HISPANIC OR LATINO,2135-2,eng,Private,CI,2025-02-01,X500,32,"
				+"\"Restorative (direct, indirect)\"",arrayLines[3]);
			Assert.AreEqual("\"say \"\"hi\"\"\"",RpUdsPlus.CsvField("say \"hi\""));
			Assert.AreEqual("\" lead\"",RpUdsPlus.CsvField(" lead"));
			Assert.AreEqual("",RpUdsPlus.CsvField(null));
			//A patient without a birthdate gets a blank date.
			Assert.AreEqual("",RpUdsPlus.ToCsv(new List<UdsPlusVisitRow> { new UdsPlusVisitRow { PatNum=9 } }).Split(new[] { "\r\n" },StringSplitOptions.None)[1]
				.Split(',')[1]);
		}

		[TestMethod]
		public void Sex_FromGender() {
			Assert.AreEqual("Male",RpUdsPlus.GetSexText(0));
			Assert.AreEqual("Female",RpUdsPlus.GetSexText(1));
			Assert.AreEqual("Unknown",RpUdsPlus.GetSexText(2));
			Assert.AreEqual("Other",RpUdsPlus.GetSexText(3));
			Assert.AreEqual("Unknown",RpUdsPlus.GetSexText(17));
		}
	}
}
