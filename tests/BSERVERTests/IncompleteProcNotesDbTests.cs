// v24_4-reports-incomplete-procnotes-all: the Incomplete Procedure Notes report's "Include all procedures with no notes" option, on a scratch
// database with the Open Dental 25.3.48 table definitions and synthetic rows. Procedure codes are synthetic (X0001...), not code-set codes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BSERVERTests {
	[TestClass]
	public class IncompleteProcNotesDbTests {
		private static string _dbName;
		private static string _skipReason;
		private static readonly DateTime _dateFrom=new DateTime(2026,4,1);
		private static readonly DateTime _dateTo=new DateTime(2026,4,30);
		private static readonly string[] _arrayExcludedCodes=new[] { "X0099" };

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ServerTestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			_dbName=ServerTestDb.ScratchName("procnote");
			ServerTestDb.Create(_dbName,"patient","procedurelog","procedurecode","procnote","procgroupitem");
			int c=(int)ProcStat.C;
			int ec=(int)ProcStat.EC;
			int tp=(int)ProcStat.TP;
			List<string> listCodes=new List<string>();
			for(int i=1;i<=14;i++) {
				listCodes.Add("("+i+",'X"+i.ToString("0000")+"','Synthetic procedure "+i+"')");
			}
			listCodes.Add("(99,'X0099','Synthetic excluded procedure')");
			listCodes.Add("(100,'~GRP~','Group Note')");
			ServerTestDb.Seed(_dbName,
				"INSERT INTO patient (PatNum,LName,FName) VALUES (1,'Adams','Ann'),(2,'Baker','Bob')",
				"INSERT INTO procedurecode (CodeNum,ProcCode,Descript) VALUES "+string.Join(",",listCodes),
				"INSERT INTO procedurelog (ProcNum,PatNum,CodeNum,ProcDate,ProcStatus,ToothNum,Surf) VALUES "
					//Day 1, patient 1: a noted procedure, one with no note row, one whose latest note is blank, one with an incomplete note.
					+"(1,1,1,'2026-04-01',"+c+",'',''),(2,1,2,'2026-04-01',"+c+",'',''),(3,1,3,'2026-04-01',"+c+",'',''),(4,1,4,'2026-04-01',"+c+",'',''),"
					//Day 2, patient 2: nothing noted all day.
					+"(5,2,5,'2026-04-02',"+c+",'',''),(6,2,6,'2026-04-02',"+c+",'',''),"
					//Day 3, patient 1: a group note with text covering procedures 7 and 8; procedure 9 is not in the group.
					+"(20,1,100,'2026-04-03',"+ec+",'',''),(7,1,7,'2026-04-03',"+c+",'',''),(8,1,8,'2026-04-03',"+c+",'',''),(9,1,9,'2026-04-03',"+c+",'',''),"
					//Day 4, patient 2: an excluded code, a treatment-planned procedure, an unsigned note.
					+"(10,2,99,'2026-04-04',"+c+",'',''),(11,2,11,'2026-04-04',"+tp+",'',''),(12,2,12,'2026-04-04',"+c+",'',''),"
					//Day 5, patient 2: a note of spaces only.
					+"(13,2,13,'2026-04-05',"+c+",'',''),"
					//Day 6, patient 1: a group note with blank text does not cover procedure 14.
					+"(21,1,100,'2026-04-06',"+ec+",'',''),(14,1,14,'2026-04-06',"+c+",'','')",
				"INSERT INTO procgroupitem (ProcGroupItemNum,ProcNum,GroupNum) VALUES (1,7,20),(2,8,20),(3,14,21)",
				"INSERT INTO procnote (ProcNoteNum,PatNum,ProcNum,EntryDateTime,Note,Signature) VALUES "
					+"(1,1,1,'2026-04-01 09:00:00','Exam done.','sig'),"
					+"(2,1,3,'2026-04-01 09:00:00','Older text','sig'),(3,1,3,'2026-04-01 10:00:00','','sig'),"
					+"(4,1,4,'2026-04-01 09:00:00','Tolerated well [Prompt:\"Anesthetic\"]','sig'),"
					+"(5,1,20,'2026-04-03 09:00:00','Group visit note.','sig'),"
					+"(6,2,12,'2026-04-04 09:00:00','Unsigned text',''),"
					+"(7,2,13,'2026-04-05 09:00:00','   ','sig'),"
					+"(8,1,21,'2026-04-06 09:00:00','','sig')");
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_dbName!=null) {
				ServerTestDb.Drop(_dbName);
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ServerTestDb.Connect(_dbName);
		}

		private static DataTable Run(bool includeNoNotes,bool includeUnsigned,bool includeAllNoNotes,bool showExcludedCodes=false,
			RpProcNote.ProcNoteGroupBy groupBy=RpProcNote.ProcNoteGroupBy.Procedure)
		{
			string command=RpProcNote.GetQuery(new List<long>(),new List<long>(),_dateFrom,_dateTo,includeNoNotes,includeUnsigned,groupBy,showExcludedCodes,
				_arrayExcludedCodes,includeAllNoNotes);
			return DataCore.GetTable(command);
		}

		///<summary>Rows as "code date", for example "X0002 04-01", sorted.</summary>
		private static List<string> Keys(DataTable table) {
			return table.Select().Select(x => x["ProcCode"].ToString()+" "+PIn.Date(x["ProcDate"].ToString()).ToString("MM-dd"))
				.OrderBy(x => x,StringComparer.Ordinal).ToList();
		}

		private static DataRow Row(DataTable table,string procCode) {
			return table.Select().Single(x => x["ProcCode"].ToString()==procCode);
		}

		[TestMethod]
		public void NoOptions_ListsOnlyIncompleteNotes() {
			CollectionAssert.AreEqual(new[] { "X0004 04-01" },Keys(Run(false,false,false)));
		}

		[TestMethod]
		public void SameDayOption_SingleProcedureIsNotShownAsMultiple() {
			//Procedure 4 is joined once per noted procedure of its day (1 and 4). 24.3 counted the joined rows and showed it as "Multiple procedures".
			DataRow row=Row(Run(true,false,false),"X0004");
			Assert.AreEqual("Synthetic procedure 4",row["Descript"].ToString());
		}

		[TestMethod]
		public void SameDayOption_ListsOnlyDaysWithoutAnyNote() {
			DataTable table=Run(true,false,false);
			List<string> listKeys=Keys(table);
			CollectionAssert.AreEqual(new[] { "X0004 04-01","X0005 04-02","X0006 04-02","X0014 04-06","~GRP~ 04-06" },listKeys,string.Join(";",listKeys));
			Assert.IsTrue(table.Columns.Contains("HasNoNote"));
		}

		[TestMethod]
		public void AllNoNotesOption_ListsEveryProcedureWithoutItsOwnNote() {
			DataTable table=Run(false,false,true);
			CollectionAssert.AreEqual(new[] {
				"X0002 04-01",//no note row, on a day other procedures have notes
				"X0003 04-01",//latest note blank, older note had text
				"X0004 04-01",//incomplete note, as before
				"X0005 04-02","X0006 04-02",
				"X0009 04-03",//not in the group note of that day
				"X0013 04-05",//note of spaces only
				"X0014 04-06","~GRP~ 04-06",//the group note is blank, so it covers nothing and is listed itself
			},Keys(table));
		}

		[TestMethod]
		public void AllNoNotesOption_GroupNoteWithTextCoversItsProcedures() {
			List<string> listKeys=Keys(Run(false,false,true));
			CollectionAssert.DoesNotContain(listKeys,"X0007 04-03");
			CollectionAssert.DoesNotContain(listKeys,"X0008 04-03");
			CollectionAssert.DoesNotContain(listKeys,"~GRP~ 04-03");
		}

		[TestMethod]
		public void AllNoNotesOption_ImpliesSameDayOption() {
			List<string> listAll=Keys(Run(false,false,true));
			CollectionAssert.AreEqual(listAll,Keys(Run(true,false,true)));
			foreach(string key in Keys(Run(true,false,false))) {
				CollectionAssert.Contains(listAll,key);
			}
		}

		[TestMethod]
		public void AllNoNotesOption_Columns() {
			DataTable table=Run(false,false,true);
			Assert.IsTrue(table.Columns.Contains("Incomplete"),"The new option turns on the Incomplete column.");
			Assert.IsTrue(table.Columns.Contains("HasNoNote"),"The new option turns on the No Note column.");
			Assert.IsFalse(table.Columns.Contains("HasUnsignedNote"));
			Assert.AreEqual("X",Row(table,"X0004")["Incomplete"].ToString());
			Assert.AreEqual("",Row(table,"X0004")["HasNoNote"].ToString(),"An incomplete note is still a note.");
			foreach(string procCode in new[] { "X0002","X0003","X0005","X0006","X0009","X0013","X0014","~GRP~" }) {
				Assert.AreEqual("X",Row(table,procCode)["HasNoNote"].ToString(),procCode);
				Assert.AreEqual("",Row(table,procCode)["Incomplete"].ToString(),procCode);
			}
		}

		[TestMethod]
		public void AllNoNotesOption_WithUnsigned_AddsUnsignedNotesAndLeavesUnsignedBlankForNoNote() {
			DataTable table=Run(false,true,true);
			List<string> listKeys=Keys(table);
			CollectionAssert.Contains(listKeys,"X0012 04-04");
			CollectionAssert.Contains(listKeys,"X0002 04-01");
			Assert.AreEqual("X",Row(table,"X0012")["HasUnsignedNote"].ToString());
			Assert.AreEqual("",Row(table,"X0012")["HasNoNote"].ToString());
			Assert.AreEqual("",Row(table,"X0002")["HasUnsignedNote"].ToString(),"The Unsigned column is blank for rows with no note.");
		}

		[TestMethod]
		public void AllNoNotesOption_ExcludedCodesStillApply() {
			CollectionAssert.DoesNotContain(Keys(Run(false,false,true)),"X0099 04-04");
			CollectionAssert.Contains(Keys(Run(false,false,true,showExcludedCodes:true)),"X0099 04-04");
		}

		[TestMethod]
		public void AllNoNotesOption_NeverListsUncompletedProcedures() {
			CollectionAssert.DoesNotContain(Keys(Run(false,true,true,showExcludedCodes:true)),"X0011 04-04");
		}

		[TestMethod]
		public void AllNoNotesOption_GroupByPatient() {
			DataTable table=Run(false,false,true,groupBy:RpProcNote.ProcNoteGroupBy.Patient);
			Assert.AreEqual(2,table.Rows.Count);
			Assert.IsTrue(table.Select().All(x => x["HasNoNote"].ToString()=="X"));
		}
	}
}
