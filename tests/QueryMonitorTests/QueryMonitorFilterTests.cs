// Filter matching in DataConnectionBase.QueryMonitor (backlog package A-QM, feature v24_4-tools-querymonitor-filters).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace QueryMonitorTests {
	[TestClass]
	public class QueryMonitorFilterTests {
		[TestMethod]
		public void IsCommandFiltered_MatchesSubstringIgnoringCase() {
			List<string> listFilters=new List<string> { "FROM signalod" };
			Assert.IsTrue(QueryMonitor.IsCommandFiltered("SELECT * FROM signalod WHERE SigDateTime > NOW()",listFilters));
			Assert.IsTrue(QueryMonitor.IsCommandFiltered("select * from SIGNALOD",listFilters));
			Assert.IsFalse(QueryMonitor.IsCommandFiltered("SELECT * FROM patient",listFilters));
		}

		[TestMethod]
		public void IsCommandFiltered_AnyFilterOfTheGroupMatches() {
			List<string> listFilters=new List<string> { "signalod","processid" };
			Assert.IsTrue(QueryMonitor.IsCommandFiltered("SELECT ProcessId FROM computer",listFilters));
			Assert.IsTrue(QueryMonitor.IsCommandFiltered("DELETE FROM signalod",listFilters));
			Assert.IsFalse(QueryMonitor.IsCommandFiltered("UPDATE patient SET LName='x'",listFilters));
		}

		[TestMethod]
		public void IsCommandFiltered_BlankFiltersAndNullsNeverMatch() {
			Assert.IsFalse(QueryMonitor.IsCommandFiltered("SELECT 1",new List<string> { "", "   ", null }));
			Assert.IsFalse(QueryMonitor.IsCommandFiltered("SELECT 1",new List<string>()));
			Assert.IsFalse(QueryMonitor.IsCommandFiltered("SELECT 1",null));
			Assert.IsFalse(QueryMonitor.IsCommandFiltered(null,new List<string> { "SELECT" }));
		}

		[TestMethod]
		public void SetFilterTexts_DropsBlanksAndDuplicatesAndNullClears() {
			QueryMonitor queryMonitor=new QueryMonitor();
			queryMonitor.SetFilterTexts(new List<string> { "signalod","", "  ", null, "SIGNALOD", "computer" });
			CollectionAssert.AreEqual(new[] { "signalod","computer" },queryMonitor.FilterTexts);
			Assert.IsTrue(queryMonitor.IsFiltered("SELECT * FROM Computer"));
			queryMonitor.SetFilterTexts(null);
			Assert.AreEqual(0,queryMonitor.FilterTexts.Length);
			Assert.IsFalse(queryMonitor.IsFiltered("SELECT * FROM computer"));
		}

		[TestMethod]
		public void FilterTexts_ReturnsACopy() {
			QueryMonitor queryMonitor=new QueryMonitor();
			queryMonitor.SetFilterTexts(new List<string> { "signalod" });
			string[] arrayFilters=queryMonitor.FilterTexts;
			arrayFilters[0]="patient";
			Assert.IsTrue(queryMonitor.IsFiltered("SELECT * FROM signalod"));
			Assert.IsFalse(queryMonitor.IsFiltered("SELECT * FROM patient"));
		}

		///<summary>A filtered query still runs, but no event is fired for it, so it never reaches the feed or the log.</summary>
		[TestMethod]
		public void RunMonitoredQuery_FilteredQueryRunsWithoutEvents() {
			QueryMonitor queryMonitor=new QueryMonitor();
			queryMonitor.IsMonitoring=true;
			queryMonitor.SetFilterTexts(new List<string> { "signalod" });
			int countEvents=0;
			queryMonitor.Fired+=(ODEventArgs e) => countEvents++;
			int countRuns=0;
			using(SqlConnection connection=new SqlConnection("Data Source=synthetic-test-server"))
			using(SqlCommand command=new SqlCommand("SELECT * FROM SignalOD WHERE SigDateTime > @Date",connection)) {
				command.Parameters.AddWithValue("@Date","2026-01-01");
				queryMonitor.RunMonitoredQuery(() => countRuns++,command);
			}
			Assert.AreEqual(1,countRuns,"The filtered query must still run.");
			Assert.AreEqual(0,countEvents,"A filtered query must not be reported.");
		}

		[TestMethod]
		public void RunMonitoredQuery_UnfilteredQueryFiresStartAndStop() {
			QueryMonitor queryMonitor=new QueryMonitor();
			queryMonitor.IsMonitoring=true;
			queryMonitor.SetFilterTexts(new List<string> { "signalod" });
			List<DbQueryObj> listReported=new List<DbQueryObj>();
			queryMonitor.Fired+=(ODEventArgs e) => listReported.Add(e.Tag as DbQueryObj);
			int countRuns=0;
			using(SqlConnection connection=new SqlConnection("Data Source=synthetic-test-server"))
			using(SqlCommand command=new SqlCommand("SELECT * FROM patient WHERE PatNum=@PatNum",connection)) {
				command.Parameters.AddWithValue("@PatNum","5");
				queryMonitor.RunMonitoredQuery(() => countRuns++,command);
			}
			Assert.AreEqual(1,countRuns);
			Assert.AreEqual(2,listReported.Count,"Start and stop are reported.");
			StringAssert.Contains(listReported[0].Command,"PatNum='5'");
		}

		///<summary>The filter is matched against the runnable text, with the parameter values filled in.</summary>
		[TestMethod]
		public void RunMonitoredQuery_FilterMatchesParameterValues() {
			QueryMonitor queryMonitor=new QueryMonitor();
			queryMonitor.IsMonitoring=true;
			queryMonitor.SetFilterTexts(new List<string> { "'heartbeat'" });
			int countEvents=0;
			queryMonitor.Fired+=(ODEventArgs e) => countEvents++;
			using(SqlConnection connection=new SqlConnection("Data Source=synthetic-test-server"))
			using(SqlCommand command=new SqlCommand("UPDATE computer SET Note=@Note",connection)) {
				command.Parameters.AddWithValue("@Note","heartbeat");
				queryMonitor.RunMonitoredQuery(() => { },command);
			}
			Assert.AreEqual(0,countEvents);
		}

		[TestMethod]
		public void RunMonitoredQuery_NotMonitoringIgnoresFilters() {
			QueryMonitor queryMonitor=new QueryMonitor();
			queryMonitor.IsMonitoring=false;
			queryMonitor.SetFilterTexts(new List<string> { "patient" });
			int countEvents=0;
			queryMonitor.Fired+=(ODEventArgs e) => countEvents++;
			int countRuns=0;
			using(SqlConnection connection=new SqlConnection("Data Source=synthetic-test-server"))
			using(SqlCommand command=new SqlCommand("SELECT * FROM patient",connection)) {
				queryMonitor.RunMonitoredQuery(() => countRuns++,command);
			}
			Assert.AreEqual(1,countRuns);
			Assert.AreEqual(0,countEvents);
		}

		///<summary>Payloads (Middle Tier) are never filtered, even when filter texts are set.</summary>
		[TestMethod]
		public void ProcessMonitoredPayload_IsNeverFiltered() {
			QueryMonitor queryMonitor=new QueryMonitor();
			queryMonitor.IsMonitoring=true;
			queryMonitor.SetFilterTexts(new List<string> { "Payload" });
			int countEvents=0;
			queryMonitor.Fired+=(ODEventArgs e) => countEvents++;
			string result=queryMonitor.ProcessMonitoredPayload(x => "done","<Payload><MethodName>Test</MethodName></Payload>");
			Assert.AreEqual("done",result);
			Assert.AreEqual(2,countEvents);
		}
	}
}
