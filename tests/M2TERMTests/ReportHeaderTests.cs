// Report headers: the 25.4 header text sits over the column that holds the procedure fee. Report columns are bound by position (QueryObject
// keeps the header list and reads ReportTable.Rows[i][index]), so the header and data lists must line up.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2TERMTests {
	[TestClass]
	public class ReportHeaderTests {
		[TestMethod]
		public void Capitation_StandardFeeHeader_OverTheProcFeeColumn() {
			//RpCapitation.GetCapitationTable selects carrier, subscriber, SSN, patient, birthdate, code, description, [tooth, surface,] date,
			//ProcFee, ProcFee minus write-off. The fee is the next-to-last column in both layouts.
			List<string> listHeaders=TermSources.GetAddColumnHeaders(TestPaths.ReadRepoFile(TermSources.RP_CAPITATION));
			List<string> listMedical=new List<string> { "Carrier","Subscriber","Subsc SSN","Patient","Pat DOB","Code","Proc Description","Date","Standard Fee","Co-Pay" };
			List<string> listDental=new List<string> { "Carrier","Subscriber","Subsc SSN","Patient","Pat DOB","Code","Proc Description","Tth","Surf","Date",
				"Standard Fee","Co-Pay" };
			CollectionAssert.AreEqual(listMedical.Concat(listDental.Skip(5)).ToList(),listHeaders,
				"The shared columns, then the medical branch, then the dental branch.");
			string sql=TestPaths.ReadRepoFile("OpenDentBusiness/Reports/RpCapitation.cs");
			Assert.IsTrue(Regex.IsMatch(sql,@"procedurelog\.ProcDate,procedurelog\.ProcFee\s*,procedurelog\.ProcFee-claimproc\.WriteOff\s"),
				"The query's fee follows the date and comes before the co-pay, as the headers do.");
		}

		[TestMethod]
		public void ProviderPayroll_FormHeaders() {
			List<string> listHeaders=TermSources.GetAddColumnHeaders(TestPaths.ReadRepoFile(TermSources.RP_PROVIDER_PAYROLL));
			//Detailed shows Patient, the summary shows Day, in the second column.
			Assert.AreEqual("Date",listHeaders[0]);
			Assert.AreEqual("Patient",listHeaders[1]);
			Assert.AreEqual("Day",listHeaders[2]);
			Assert.AreEqual("Standard Fee Production",listHeaders[3]);
			Assert.AreEqual(13,listHeaders.Count,"Date, Patient or Day, and ten amount columns.");
		}

		[TestMethod]
		public void NetProdDetail_FormHeaders() {
			List<string> listHeaders=TermSources.GetAddColumnHeaders(TestPaths.ReadRepoFile(TermSources.RP_NET_PROD_DETAIL));
			CollectionAssert.AreEqual(new List<string> { "Type","Date","Clinic","PatNum","Patient","ProcCode","Provider","Standard Fee","OrigEstWO",
				"EstVsActualWO","Adjustment","NPR" },listHeaders);
		}
	}

	[TestClass]
	public class ReportHeaderDbTests {
		private static readonly DateTime _dateReport=new DateTime(2030,1,15);
		private static long _procNum;

		[TestInitialize]
		public void TestInitialize() {
			TermDb.Begin();
			if(_procNum==0) {
				_procNum=OpenDentBusiness.Crud.ProcedureCrud.Insert(new Procedure { PatNum=TermDb.PatNum,CodeNum=TermDb.CodeNumWithFee,ProvNum=TermDb.ProvNum,
					ProcStatus=ProcStat.C,ProcDate=_dateReport,DateComplete=_dateReport,ProcFee=125,UnitQty=1,BaseUnits=0 });
			}
		}

		private static List<Provider> GetTestProvider() {
			return new List<Provider> { Providers.GetProv(TermDb.ProvNum) };
		}

		[TestMethod]
		public void ProviderPayroll_Summary_StandardFeeProductionColumn() {
			DataSet ds=RpProdInc.GetProviderPayrollDataForClinics(_dateReport,_dateReport,GetTestProvider(),new List<Clinic>(),false,true,false);
			DataTable table=ds.Tables["Total"];
			List<string> listColumns=table.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToList();
			Assert.AreEqual("Standard Fee Production",listColumns[2],"The typo \"Poduction\" is fixed too.");
			Assert.IsFalse(listColumns.Any(x => TermSources.RegexUcrWord.IsMatch(x)),string.Join(", ",listColumns));
			Assert.AreEqual(12,listColumns.Count);
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("125.00",table.Rows[0][2].ToString(),"Production lands under the header the form puts third.");
			//The form's summary headers: Date, Day, then the production column.
			List<string> listHeaders=TermSources.GetAddColumnHeaders(TestPaths.ReadRepoFile(TermSources.RP_PROVIDER_PAYROLL));
			listHeaders.Remove("Patient");
			Assert.AreEqual(listColumns.Count,listHeaders.Count);
			Assert.AreEqual(listColumns[2],listHeaders[2]);
		}

		[TestMethod]
		public void ProviderPayroll_Detailed_ProductionUnderTheThirdHeader() {
			DataSet ds=RpProdInc.GetProviderPayrollDataForClinics(_dateReport,_dateReport,GetTestProvider(),new List<Clinic>(),false,true,true);
			DataTable table=ds.Tables["Total"];
			Assert.AreEqual(1,table.Rows.Count);
			//Positional: the form shows Date, Patient, Standard Fee Production. The data rows put the patient's name second and production third.
			StringAssert.StartsWith(table.Rows[0][1].ToString(),"Termtest, Pat");
			Assert.AreEqual("125.00",table.Rows[0][2].ToString());
		}

		[TestMethod]
		public void NetProdDetail_StandardFeeHeader_OverTheFeeColumn() {
			DataTable table=RpProdInc.GetNetProductionDetailDataSet(_dateReport,_dateReport,GetTestProvider(),new List<Clinic>(),false,true);
			List<string> listHeaders=TermSources.GetAddColumnHeaders(TestPaths.ReadRepoFile(TermSources.RP_NET_PROD_DETAIL));
			Assert.AreEqual(listHeaders.Count,table.Columns.Count);
			int index=listHeaders.IndexOf("Standard Fee");
			Assert.AreEqual("UCR",table.Columns[index].ColumnName,"The query's own column name (not shown) stays, for anything that reads it by name.");
			DataRow row=table.Select().Single(x => x[0].ToString()=="Procedure Completed");
			Assert.AreEqual(125,Convert.ToDouble(row[index]),0.001);
		}
	}
}
