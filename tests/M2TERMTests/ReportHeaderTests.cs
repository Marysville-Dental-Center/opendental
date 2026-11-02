// Report headers: the 25.4 header text sits over the column that holds the procedure fee. Report columns are bound by position (QueryObject
// keeps the header list and reads ReportTable.Rows[i][index]), so the header and data lists must line up. The export keeps the old header as
// its column name (the spec keeps report export header keys stable for offices' own tools).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental.ReportingComplex;
using OpenPmsDentalBusiness;

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
			string sql=TestPaths.ReadRepoFile("OpenPmsDentalBusiness/Reports/RpCapitation.cs");
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

		///<summary>The export keys before 25.4: the headers as Open Dental 24.3 added them, in source order (both branches of an if-else).
		///The report window's Export writes them as the file's header line, and offices' spreadsheets and tools read the columns by these names.
		///</summary>
		private static readonly Dictionary<string,List<string>> _dictExportKeysBefore=new Dictionary<string,List<string>> {
			{ TermSources.RP_CAPITATION,new List<string> { "Carrier","Subscriber","Subsc SSN","Patient","Pat DOB",
				"Code","Proc Description","Date","UCR Fee","Co-Pay",
				"Code","Proc Description","Tth","Surf","Date","UCR Fee","Co-Pay" } },
			{ TermSources.RP_NET_PROD_DETAIL,new List<string> { "Type","Date","Clinic","PatNum","Patient","ProcCode","Provider","UCR","OrigEstWO",
				"EstVsActualWO","Adjustment","NPR" } },
			{ TermSources.RP_PROVIDER_PAYROLL,new List<string> { "Date","Patient","Day","UCR Production","Est Write-off","Prod Adj","Change in Write-off",
				"Net Prod(NPR)","Pat Inc Alloc","Pat Inc Unalloc","Ins Income","Ins Not Final","Net Income" } },
		};

		[TestMethod]
		public void ExportKeys_Unchanged_ShownHeadersRenamed() {
			foreach(KeyValuePair<string,List<string>> pair in _dictExportKeysBefore) {
				string source=TestPaths.ReadRepoFile(pair.Key);
				List<ReportColumn> listColumns=TermSources.GetReportColumns(source);
				CollectionAssert.AreEqual(TermSources.GetAddColumnHeaders(source),listColumns.Select(x => x.Header).ToList(),
					pair.Key+": every AddColumn is read, with its width and type.");
				CollectionAssert.AreEqual(pair.Value,listColumns.Select(x => x.ExportKey).ToList(),pair.Key+": the export keys are the ones before 25.4.");
				Assert.AreEqual(listColumns.Count(x => x.HasExportKeyStatement),TermSources.CountExportKeyStatements(source),
					pair.Key+": each export key statement comes right after the column it renames.");
				foreach(ReportColumn column in listColumns.FindAll(x => x.HasExportKeyStatement)) {
					Assert.AreNotEqual(column.ExportKey,column.Header,pair.Key+": "+column+" renames nothing.");
				}
				Assert.IsFalse(listColumns.Any(x => TermSources.RegexUcrWord.IsMatch(x.Header)),pair.Key+": a shown header still says UCR.");
			}
			Assert.AreEqual(4,_dictExportKeysBefore.Keys.Sum(x => TermSources.GetReportColumns(TestPaths.ReadRepoFile(x)).Count(y => y.HasExportKeyStatement)),
				"The four renamed headers: Capitation (two layouts), Net Production Detail, Provider Payroll.");
		}

		[TestMethod]
		public void QueryObject_ExportWritesTheOldKey_ScreenShowsTheNewHeader() {
			//Builds each report's QueryObject the way the form does (its AddColumn calls and export key statements, read from the source) on a filled
			//table, as ReportComplex.AddQuery(table,"","",SplitByKind.None,1,true) and SubmitQueries do. No window is shown.
			List<ReportColumn> listCapitation=TermSources.GetReportColumns(TestPaths.ReadRepoFile(TermSources.RP_CAPITATION));
			//Medical practice or clinic: the five shared columns, then the first branch. Dental: the shared columns, then the second branch.
			AssertQueryObject(listCapitation.Take(10).ToList());
			AssertQueryObject(listCapitation.Take(5).Concat(listCapitation.Skip(10)).ToList());
			List<ReportColumn> listPayroll=TermSources.GetReportColumns(TestPaths.ReadRepoFile(TermSources.RP_PROVIDER_PAYROLL));
			AssertQueryObject(listPayroll.FindAll(x => x.Header!="Day"));//Detailed.
			AssertQueryObject(listPayroll.FindAll(x => x.Header!="Patient"));//Summary.
			AssertQueryObject(TermSources.GetReportColumns(TestPaths.ReadRepoFile(TermSources.RP_NET_PROD_DETAIL)));
		}

		private static void AssertQueryObject(List<ReportColumn> listColumns) {
			string columns=string.Join(", ",listColumns);
			Assert.AreEqual(1,listColumns.Count(x => x.HasExportKeyStatement),"One renamed column in each layout: "+columns);
			DataTable table=new DataTable();
			for(int i=0;i<listColumns.Count;i++) {
				table.Columns.Add("col"+i);
			}
			DataRow row=table.NewRow();
			for(int i=0;i<listColumns.Count;i++) {
				row[i]=((i+1)*11).ToString();
			}
			table.Rows.Add(row);
			QueryObject query=new QueryObject("",table,isCentered:true,queryGroupValue:1);
			Font font=new Font("Tahoma",8,FontStyle.Regular);
			foreach(ReportColumn column in listColumns) {
				query.AddColumn(column.Header,column.Width,(FieldValueType)Enum.Parse(typeof(FieldValueType),column.TypeName),font);
				if(column.HasExportKeyStatement) {
					query.ExportTable.Columns[query.ExportTable.Columns.Count-1].ColumnName=column.ExportKey;//The forms' statement.
				}
			}
			Assert.IsTrue(query.SubmitQuery(),"The table is filled already, so no query runs.");
			List<string> listKeys=listColumns.Select(x => x.ExportKey).ToList();
			List<string> listHeaders=listColumns.Select(x => x.Header).ToList();
			//The export's header line: FormReportComplex.OnExport_Click writes each export column's Caption, which follows ColumnName.
			CollectionAssert.AreEqual(listKeys,query.ExportTable.Columns.Cast<DataColumn>().Select(x => x.Caption).ToList(),columns);
			CollectionAssert.AreEqual(listKeys,query.ExportTable.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToList(),columns);
			//On screen and in print: the column headers and the data field names keep the new text.
			List<ReportObject> listReportObjects=query.ReportObjects.Cast<ReportObject>().ToList();
			CollectionAssert.AreEqual(listHeaders,listReportObjects.FindAll(x => x.SectionType==AreaSectionType.GroupHeader).Select(x => x.StaticText).ToList(),
				columns);
			CollectionAssert.AreEqual(listHeaders,query.ArrDataFields.Cast<string>().ToList(),columns);
			//Export cells and totals are found by position through ArrDataFields, not by the export column's name.
			Assert.AreEqual(1,query.ExportTable.Rows.Count);
			foreach(ReportObject reportObject in listReportObjects.FindAll(x => x.SectionType==AreaSectionType.Detail)) {
				int index=query.ArrDataFields.IndexOf(reportObject.DataField);
				Assert.IsTrue(index>=0,reportObject.DataField);
				Assert.AreEqual(table.Rows[0][index].ToString(),query.ExportTable.Rows[0][index].ToString(),reportObject.DataField);
			}
			foreach(ReportObject reportObject in listReportObjects.FindAll(x => x.SectionType==AreaSectionType.GroupFooter && x.Name.EndsWith("Footer"))) {
				int index=query.ArrDataFields.IndexOf(reportObject.SummarizedField);
				Assert.IsTrue(index>=0,reportObject.SummarizedField);
				Assert.AreEqual(PIn.Double(table.Rows[0][index].ToString()),reportObject.GetSummaryValue(query.ExportTable,index),0.001,reportObject.SummarizedField);
			}
			//A split report copies the query object; the copy keeps the export keys.
			QueryObject queryCopy=query.DeepCopyQueryObject();
			CollectionAssert.AreEqual(listKeys,queryCopy.ExportTable.Columns.Cast<DataColumn>().Select(x => x.Caption).ToList(),columns);
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
				_procNum=OpenPmsDentalBusiness.Crud.ProcedureCrud.Insert(new Procedure { PatNum=TermDb.PatNum,CodeNum=TermDb.CodeNumWithFee,ProvNum=TermDb.ProvNum,
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
