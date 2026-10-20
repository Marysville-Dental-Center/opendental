// The QueryFilters helpers that need no database: cleaning, saving rules, renaming, unique names, import and export (backlog package A-QM).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace QueryMonitorTests {
	[TestClass]
	public class QueryFiltersHelperTests {
		private static QueryFilter Make(string groupName,string filterText,long queryFilterNum=0) {
			QueryFilter queryFilter=new QueryFilter();
			queryFilter.QueryFilterNum=queryFilterNum;
			queryFilter.GroupName=groupName;
			queryFilter.FilterText=filterText;
			return queryFilter;
		}

		[TestMethod]
		public void CleanFilterText_TrimsJoinsLinesAndCuts() {
			Assert.AreEqual("",QueryFilters.CleanFilterText(null));
			Assert.AreEqual("",QueryFilters.CleanFilterText("   \r\n "));
			Assert.AreEqual("FROM signalod",QueryFilters.CleanFilterText("  FROM signalod  "));
			Assert.AreEqual("SELECT a FROM b",QueryFilters.CleanFilterText("SELECT a\r\nFROM b"));
			Assert.AreEqual(QueryFilters.MaxLength,QueryFilters.CleanFilterText(new string('x',400)).Length);
		}

		[TestMethod]
		public void GetGroupNames_DistinctIgnoringCaseAndSorted() {
			List<QueryFilter> list=new List<QueryFilter> { Make("Signals","a"),Make("billing","b"),Make("signals","c"),Make("","d") };
			CollectionAssert.AreEqual(new[] { "billing","Signals" },QueryFilters.GetGroupNames(list));
		}

		[TestMethod]
		public void GetFilterTextsForGroup_OnlyThatGroupWithoutBlanksOrDuplicates() {
			List<QueryFilter> list=new List<QueryFilter> {
				Make("Signals","signalod"),Make("signals","SIGNALOD"),Make("Signals"," "),Make("Signals","computer"),Make("Other","patient")
			};
			CollectionAssert.AreEqual(new[] { "signalod","computer" },QueryFilters.GetFilterTextsForGroup(list,"SIGNALS"));
			Assert.AreEqual(0,QueryFilters.GetFilterTextsForGroup(list,"").Count);
			Assert.AreEqual(0,QueryFilters.GetFilterTextsForGroup(list,"Missing").Count);
		}

		[TestMethod]
		public void NormalizeForSave_RemovesBlanksAndDuplicatesWithinAGroup() {
			QueryFilter queryFilterSaved=Make("Signals","signalod",7);
			List<QueryFilter> list=new List<QueryFilter> {
				Make("Signals","SIGNALOD"),//new duplicate of a saved row: dropped, the saved row is kept
				queryFilterSaved,
				Make("Signals","  "),//blank text
				Make("  ","patient"),//blank group
				Make("Other","signalod"),//same text in another group is fine
				Make("Signals"," computer\r\n"),
			};
			List<QueryFilter> listSaved=QueryFilters.NormalizeForSave(list);
			Assert.AreEqual(3,listSaved.Count);
			Assert.AreSame(queryFilterSaved,listSaved[0]);
			Assert.AreEqual("Other",listSaved[1].GroupName);
			Assert.AreEqual("computer",listSaved[2].FilterText);
		}

		[TestMethod]
		public void RenameGroup_ChangesEveryRowOfTheGroupOnly() {
			List<QueryFilter> list=new List<QueryFilter> { Make("Signals","a"),Make("signals","b"),Make("Other","c") };
			Assert.AreEqual(2,QueryFilters.RenameGroup(list,"Signals","Polling"));
			CollectionAssert.AreEqual(new[] { "Polling","Polling","Other" },list.Select(x => x.GroupName).ToList());
		}

		[TestMethod]
		public void GetUniqueGroupName_AddsANumericSuffix() {
			Assert.AreEqual("Signals",QueryFilters.GetUniqueGroupName("Signals",new[] { "Other" }));
			Assert.AreEqual("Signals (2)",QueryFilters.GetUniqueGroupName("Signals",new[] { "signals" }));
			Assert.AreEqual("Signals (3)",QueryFilters.GetUniqueGroupName("Signals",new[] { "Signals","Signals (2)" }));
			Assert.AreEqual("Imported",QueryFilters.GetUniqueGroupName("  ",new string[0]));
			string longName=new string('g',QueryFilters.MaxLength);
			string unique=QueryFilters.GetUniqueGroupName(longName,new[] { longName });
			Assert.IsTrue(unique.Length<=QueryFilters.MaxLength);
			Assert.IsTrue(unique.EndsWith(" (2)"));
		}

		[TestMethod]
		public void ExportThenImport_RoundTrips() {
			string text=QueryFilters.ExportGroupToText("Signals",new[] { "FROM signalod","processid","" });
			StringAssert.StartsWith(text,QueryFilters.ExportHeaderPrefix+" Signals\r\n");
			Assert.IsTrue(QueryFilters.TryParseImportText(text,out string groupName,out List<string> listFilterTexts,out string error),error);
			Assert.AreEqual("Signals",groupName);
			CollectionAssert.AreEqual(new[] { "FROM signalod","processid" },listFilterTexts);
		}

		[TestMethod]
		public void TryParseImportText_AcceptsBomBlankLinesAndDropsDuplicates() {
			string text="\uFEFF\r\n"+QueryFilters.ExportHeaderPrefix+"  Polling \n\nsignalod\nSIGNALOD\n  computer  \n";
			Assert.IsTrue(QueryFilters.TryParseImportText(text,out string groupName,out List<string> listFilterTexts,out string error),error);
			Assert.AreEqual("Polling",groupName);
			CollectionAssert.AreEqual(new[] { "signalod","computer" },listFilterTexts);
		}

		[TestMethod]
		public void TryParseImportText_RejectsBadFiles() {
			Assert.IsFalse(QueryFilters.TryParseImportText("",out _,out _,out string error));
			Assert.AreNotEqual("",error);
			Assert.IsFalse(QueryFilters.TryParseImportText("signalod\ncomputer",out _,out _,out _),"No header line.");
			Assert.IsFalse(QueryFilters.TryParseImportText(QueryFilters.ExportHeaderPrefix+"   \nsignalod",out _,out _,out _),"No group name.");
			Assert.IsFalse(QueryFilters.TryParseImportText(QueryFilters.ExportHeaderPrefix+" Empty\n\n",out _,out _,out _),"No filters.");
		}

		[TestMethod]
		public void GetChangedGroupNames_ReportsEditedAddedRemovedAndRenamedGroups() {
			List<QueryFilter> listOld=new List<QueryFilter> {
				Make("Same","a",1),Make("Edited","b",2),Make("Removed","c",3),Make("Renamed","d",4)
			};
			List<QueryFilter> listNew=new List<QueryFilter> {
				Make("Same","a",1),Make("Edited","b2",2),Make("NewName","d",4),Make("Added","e")
			};
			List<string> listChanged=QueryFilters.GetChangedGroupNames(listNew,listOld);
			CollectionAssert.AreEquivalent(new[] { "Added","Edited","NewName","Removed","Renamed" },listChanged);
			Assert.AreEqual(0,QueryFilters.GetChangedGroupNames(listOld,listOld).Count);
		}

		[TestMethod]
		public void GetLogText_NamesTheGroup() {
			Assert.AreEqual("Query filter group 'Signals' changed.",QueryFilters.GetLogText("Signals"));
		}
	}
}
