// Where group notes show in the progress notes (package A-GN): after the last procedure attached to them, by their own date when nothing is
// attached, and with the tooth filter on. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace GroupNoteTests {
	[TestClass]
	public class ProgressNoteTests {
		private DataTable _table;

		[TestInitialize]
		public void TestInitialize() {
			_table=new DataTable();
			_table.Columns.Add("ProcNum");
			_table.Columns.Add("ProcCode");
			_table.Columns.Add("CommlogNum");
		}

		private DataRow Proc(long procNum,string procCode="GNT01") {
			DataRow dataRow=_table.NewRow();
			dataRow["ProcNum"]=procNum.ToString();
			dataRow["ProcCode"]=procCode;
			dataRow["CommlogNum"]="0";
			return dataRow;
		}

		private DataRow GroupNote(long procNum) {
			return Proc(procNum,ProcedureCodes.GroupProcCode);
		}

		private DataRow Commlog(long commlogNum) {
			DataRow dataRow=_table.NewRow();
			dataRow["ProcNum"]="0";
			dataRow["ProcCode"]="";
			dataRow["CommlogNum"]=commlogNum.ToString();
			return dataRow;
		}

		private static List<ProcGroupItem> Links(long groupNum,params long[] arrayProcNums) {
			return arrayProcNums.Select(x => new ProcGroupItem { GroupNum=groupNum,ProcNum=x }).ToList();
		}

		private static string Order(List<DataRow> listDataRows) {
			return string.Join(",",listDataRows.Select(x => x["ProcNum"].ToString()!="0" ? x["ProcNum"].ToString() : "c"+x["CommlogNum"]));
		}

		[TestMethod]
		public void GroupNote_MovesAfterTheLastAttachedProcedure() {
			//Sorted by date: the group note is dated before one of its procedures (25.1 allows procedures on other dates).
			List<DataRow> listDataRows=new List<DataRow> { Proc(1),GroupNote(90),Proc(2),Commlog(5),Proc(3),Proc(4) };
			GroupNotes.PlaceAfterAttachedProcs(listDataRows,Links(90,1,3));
			Assert.AreEqual("1,2,c5,3,90,4",Order(listDataRows));
		}

		[TestMethod]
		public void GroupNote_AlreadyAfterItsProcedures_StaysAtItsOwnDate() {
			List<DataRow> listDataRows=new List<DataRow> { Proc(1),Proc(2),Proc(3),GroupNote(90),Proc(4) };
			GroupNotes.PlaceAfterAttachedProcs(listDataRows,Links(90,1,2));
			Assert.AreEqual("1,2,3,90,4",Order(listDataRows));
		}

		[TestMethod]
		public void GroupNote_NothingAttached_SortsByItsOwnDate() {
			List<DataRow> listDataRows=new List<DataRow> { GroupNote(91),Proc(1),GroupNote(90),Proc(2) };
			GroupNotes.PlaceAfterAttachedProcs(listDataRows,Links(90,1));
			Assert.AreEqual("91,1,90,2",Order(listDataRows));
		}

		[TestMethod]
		public void GroupNote_ProceduresNotInTheList_SortsByItsOwnDate() {
			List<DataRow> listDataRows=new List<DataRow> { GroupNote(90),Proc(1),Proc(2) };
			GroupNotes.PlaceAfterAttachedProcs(listDataRows,Links(90,55,56));
			Assert.AreEqual("90,1,2",Order(listDataRows));
		}

		[TestMethod]
		public void TwoGroupNotes_SharingAProcedure_BothFollowIt() {
			List<DataRow> listDataRows=new List<DataRow> { GroupNote(90),GroupNote(91),Proc(1),Proc(2) };
			List<ProcGroupItem> listProcGroupItems=Links(90,1);
			listProcGroupItems.AddRange(Links(91,1,2));
			GroupNotes.PlaceAfterAttachedProcs(listDataRows,listProcGroupItems);
			Assert.AreEqual("1,90,2,91",Order(listDataRows));
		}

		[TestMethod]
		public void NoLinks_NothingMoves() {
			List<DataRow> listDataRows=new List<DataRow> { GroupNote(90),Proc(1) };
			GroupNotes.PlaceAfterAttachedProcs(listDataRows,new List<ProcGroupItem>());
			GroupNotes.PlaceAfterAttachedProcs(listDataRows,null);
			Assert.AreEqual("90,1",Order(listDataRows));
		}

		[TestMethod]
		public void ToothFilter_ShowsWhenAnAttachedProcedureIsVisible() {
			List<ProcGroupItem> listProcGroupItems=Links(90,1,2);
			Assert.IsTrue(GroupNotes.IsVisibleWithToothFilter(90,listProcGroupItems,new List<long> { 2,7 }));
			Assert.IsFalse(GroupNotes.IsVisibleWithToothFilter(90,listProcGroupItems,new List<long> { 7 }));
			Assert.IsFalse(GroupNotes.IsVisibleWithToothFilter(90,listProcGroupItems,new List<long>()));
		}

		[TestMethod]
		public void ToothFilter_NothingAttached_AlwaysPasses() {
			//24.3 hid it whenever the tooth filter was on. The date range still applies afterwards in the Chart module.
			Assert.IsTrue(GroupNotes.IsVisibleWithToothFilter(91,Links(90,1),new List<long>()));
			Assert.IsTrue(GroupNotes.IsVisibleWithToothFilter(91,null,null));
		}
	}
}
