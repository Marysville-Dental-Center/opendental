// The program's RDL reports that load ODR.dll as their code module, rendered by RdlEngine and RdlViewer built from the source in this repository,
// with ODR.dll built from ODR/ (package RDL-SRC, docs/port-notes/RDL-SRC.md): Unsched Treat Plans.rdl (ODR.Query and its parameter window,
// ODR.DataConnection.GetODConnStr as the connection), ReportAccountingBalanceSheet.txt (ODR.GetData) and ReportAccountingGenLedger.txt
// (ODR.Format, ODR.Aggregate). Each must produce pages without an engine message and show the synthetic rows of RdlReportDb.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ReportTests {
	[TestClass]
	public class RdlReportRenderTests {
		private const string FILE_UNSCHED="Unsched Treat Plans.rdl";
		private const string FILE_BALANCE_SHEET="ReportAccountingBalanceSheet.txt";
		private const string FILE_GEN_LEDGER="ReportAccountingGenLedger.txt";
		///<summary>Typed into the Start Date and Stop Date boxes of the Unsched Treat Plans parameter window.</summary>
		private static readonly string[] _arrayDates2025=new[] { "01/01/2025","12/31/2025" };

		[TestInitialize]
		public void TestInitialize() {
			RdlReportDb.Begin();
		}

		///<summary>The accounting reports leave their connection string as the placeholder @@@ConnectionString@@@ for the host to fill in; this is
		///the connection of the running program, as ODR.DataConnection.GetODConnStr() returns it.</summary>
		private static string ReadAccountingReport(string fileName) {
			string rdl=RdlRender.ReadProgramReport(fileName);
			StringAssert.Contains(rdl,"<ConnectString>@@@ConnectionString@@@</ConnectString>",fileName);
			string connectionString=ODR.DataConnection.GetODConnStr();
			Assert.AreNotEqual("",connectionString,"ODR.DataConnection.GetODConnStr() is empty although the program is connected.");
			return rdl.Replace("@@@ConnectionString@@@",SecurityElement.Escape(connectionString));
		}

		private static void AssertPagesWithoutMessages(RdlRenderResult result,string reportName) {
			Assert.AreEqual(0,result.ParseMaxSeverity,reportName+": parse messages. "+result.Describe());
			Assert.AreEqual(0,result.RunMaxSeverity,reportName+": run messages. "+result.Describe());
			Assert.AreEqual(0,result.ListParseMessages.Count+result.ListRunMessages.Count,reportName+": "+result.Describe());
			Assert.IsTrue(result.PageCount>=1,reportName+": no pages. "+result.Describe());
		}

		private static void AssertShows(RdlRenderResult result,string text,string reportName) {
			Assert.IsTrue(result.ListTexts.Any(x => x.Contains(text)),reportName+" does not show \""+text+"\". "+result.Describe());
		}

		private static void AssertDoesNotShow(RdlRenderResult result,string text,string reportName) {
			Assert.IsFalse(result.ListTexts.Any(x => x.Contains(text)),reportName+" shows \""+text+"\". "+result.Describe());
		}

		///<summary>True if one text item is the amount, however the report formats it (1234.5, 1,234.50, $1,234.50).</summary>
		private static bool ShowsAmount(RdlRenderResult result,decimal amount) {
			foreach(string text in result.ListTexts) {
				decimal value;
				if(decimal.TryParse(text.Trim().Replace("$",""),NumberStyles.Number,CultureInfo.GetCultureInfo("en-US"),out value) && value==amount) {
					return true;
				}
			}
			return false;
		}

		[TestMethod]
		public void UnschedTreatPlans_Engine_AsksForTheDatesOnce_AndListsThePlannedProcedure() {
			RdlRenderResult result=RdlRender.RenderWithEngine(RdlRender.ReadProgramReport(FILE_UNSCHED),_arrayDates2025);
			AssertPagesWithoutMessages(result,FILE_UNSCHED);
			//ODR.Query asks once per instance, and the engine makes one instance when it compiles the report (Query.FinalPass reads the column list)
			//and another when it runs it. The prebuilt 2.9.3 engine did the same (docs/port-notes/RDL-SRC.md).
			Assert.AreEqual(2,result.CountParameterWindows,"Parameter windows: one for the compile pass, one for the run.");
			CollectionAssert.AreEqual(new[] { "Start Date","Stop Date" },result.DictParameterPrompts.Keys.ToArray());
			AssertShows(result,RdlReportDb.PatLName+", "+RdlReportDb.PatFName,FILE_UNSCHED);
			AssertShows(result,RdlReportDb.ProcCode,FILE_UNSCHED);
			AssertShows(result,RdlReportDb.ProcDescript,FILE_UNSCHED);
			Assert.IsTrue(ShowsAmount(result,123.45m),"The planned fee. "+result.Describe());
			Assert.IsFalse(ShowsAmount(result,99.99m),"A completed procedure is not a treatment plan. "+result.Describe());
			Assert.IsFalse(ShowsAmount(result,77.77m),"Planned before the start date. "+result.Describe());
		}

		[TestMethod]
		public void UnschedTreatPlans_Viewer_BuildsPagesWithoutMessages() {
			RdlRenderResult result=RdlRender.RenderWithViewer(RdlRender.ReadProgramReport(FILE_UNSCHED),_arrayDates2025);
			Assert.AreEqual(0,result.ListRunMessages.Count,"Messages the viewer hides with the parameter panel off: "+result.Describe());
			Assert.IsTrue(result.PageCount>=1,result.Describe());
			//Rebuild loads the report (compile pass and run) and then runs it once more: three windows, as with the prebuilt viewer and engine.
			Assert.AreEqual(3,result.CountParameterWindows,"The viewer runs ODR.Query, which asks for the dates. "+result.Describe());
		}

		[TestMethod]
		public void BalanceSheet_Engine_ShowsTheAccountsAndOdrTotals() {
			RdlRenderResult result=RdlRender.RenderWithEngine(ReadAccountingReport(FILE_BALANCE_SHEET));
			AssertPagesWithoutMessages(result,FILE_BALANCE_SHEET);
			Assert.AreEqual(0,result.CountParameterWindows);
			AssertShows(result,RdlReportDb.PracticeTitle,FILE_BALANCE_SHEET);//ODR.GetData.Pref("PracticeTitle")
			AssertShows(result,"Synthetic Checking",FILE_BALANCE_SHEET);
			AssertShows(result,"Synthetic Loan",FILE_BALANCE_SHEET);
			AssertShows(result,"Synthetic Capital",FILE_BALANCE_SHEET);
			AssertDoesNotShow(result,"Synthetic Fees",FILE_BALANCE_SHEET);//income and expense accounts are not on a balance sheet
			Assert.IsTrue(ShowsAmount(result,6500m),"Checking at the end of 2007. "+result.Describe());
			Assert.IsTrue(ShowsAmount(result,400m),"ODR.GetData.RetainedEarningsAuto: the 2006 income. "+result.Describe());
			Assert.IsTrue(ShowsAmount(result,800m),"ODR.GetData.NetIncomeThisYear: 2007 fees less supplies. "+result.Describe());
		}

		[TestMethod]
		public void BalanceSheet_Viewer_BuildsPagesWithoutMessages() {
			RdlRenderResult result=RdlRender.RenderWithViewer(ReadAccountingReport(FILE_BALANCE_SHEET));
			Assert.AreEqual(0,result.ListRunMessages.Count,result.Describe());
			Assert.IsTrue(result.PageCount>=1,result.Describe());
		}

		[TestMethod]
		public void GeneralLedger_Engine_ShowsEveryAccountWithOdrRunningBalances() {
			RdlRenderResult result=RdlRender.RenderWithEngine(ReadAccountingReport(FILE_GEN_LEDGER));
			AssertPagesWithoutMessages(result,FILE_GEN_LEDGER);
			foreach(string account in new[] { "Synthetic Checking","Synthetic Loan","Synthetic Capital","Synthetic Fees","Synthetic Supplies" }) {
				AssertShows(result,account,FILE_GEN_LEDGER);
			}
			AssertShows(result,"Synthetic supplies",FILE_GEN_LEDGER);//a 2007 memo
			AssertDoesNotShow(result,"Synthetic loan",FILE_GEN_LEDGER);//a 2006 memo: only its starting balance shows
			//ODR.Aggregate.RunningSumForAccounts formats with "N": Checking starts 2007 at 5,700.00, then 6,700.00 and 6,500.00.
			AssertShows(result,"5,700.00",FILE_GEN_LEDGER);
			AssertShows(result,"6,700.00",FILE_GEN_LEDGER);
			AssertShows(result,"6,500.00",FILE_GEN_LEDGER);
		}

		[TestMethod]
		public void GeneralLedger_Viewer_BuildsPagesWithoutMessages() {
			RdlRenderResult result=RdlRender.RenderWithViewer(ReadAccountingReport(FILE_GEN_LEDGER));
			Assert.AreEqual(0,result.ListRunMessages.Count,result.Describe());
			Assert.IsTrue(result.PageCount>=1,result.Describe());
		}
	}
}
