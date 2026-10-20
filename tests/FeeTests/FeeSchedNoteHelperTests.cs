// Fee schedule note helpers that need no database: the clinic list, the clinic filter, sorting and the grid summary.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace FeeTests {
	[TestClass]
	public class FeeSchedNoteHelperTests {
		private static FeeSchedNote Note(long num,string clinicNums,string dateEntry,string note="x") {
			return new FeeSchedNote { FeeSchedNoteNum=num,FeeSchedNum=1,ClinicNums=clinicNums,DateEntry=DateTime.Parse(dateEntry),Note=note };
		}

		[TestMethod]
		public void ClinicNums_ParseAndFormat() {
			CollectionAssert.AreEqual(new long[0],FeeSchedNotes.ParseClinicNums("").ToArray());
			CollectionAssert.AreEqual(new long[0],FeeSchedNotes.ParseClinicNums(null).ToArray());
			CollectionAssert.AreEqual(new long[] { 3,1 },FeeSchedNotes.ParseClinicNums(" 3, 1,,abc,0,3").ToArray());
			Assert.AreEqual("1,3,12",FeeSchedNotes.ClinicNumsToString(new List<long> { 12,3,1,3,0 }));
			Assert.AreEqual("",FeeSchedNotes.ClinicNumsToString(new List<long>()));
			Assert.AreEqual("",FeeSchedNotes.ClinicNumsToString(null));
		}

		[TestMethod]
		public void ClinicFilter_ShowsMatchingAndAllClinicNotes() {
			List<FeeSchedNote> listNotes=new List<FeeSchedNote> {
				Note(1,"","2026-01-01"),
				Note(2,"2","2026-02-01"),
				Note(3,"2,5","2026-03-01"),
				Note(4,"5","2026-04-01"),
			};
			CollectionAssert.AreEqual(new long[] { 1,2,3 },FeeSchedNotes.FilterForClinic(listNotes,2).Select(x => x.FeeSchedNoteNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 1,3,4 },FeeSchedNotes.FilterForClinic(listNotes,5).Select(x => x.FeeSchedNoteNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 1 },FeeSchedNotes.FilterForClinic(listNotes,9).Select(x => x.FeeSchedNoteNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 1,2,3,4 },FeeSchedNotes.FilterForClinic(listNotes,-1).Select(x => x.FeeSchedNoteNum).ToArray());
			Assert.IsTrue(FeeSchedNotes.AppliesToClinic(listNotes[0],77));
			Assert.IsFalse(FeeSchedNotes.AppliesToClinic(listNotes[1],5));
		}

		[TestMethod]
		public void SortForDisplay_NewestDateFirst() {
			List<FeeSchedNote> listNotes=new List<FeeSchedNote> {
				Note(1,"","2026-01-01"),
				Note(2,"","2026-03-01"),
				Note(3,"","2026-03-01"),
				Note(4,"","2025-12-31"),
			};
			CollectionAssert.AreEqual(new long[] { 3,2,1,4 },FeeSchedNotes.SortForDisplay(listNotes).Select(x => x.FeeSchedNoteNum).ToArray());
		}

		[TestMethod]
		public void NoteSummary_FirstLineAndCut() {
			Assert.AreEqual("Raised D-codes 3%",FeeSchedNotes.GetNoteSummary(Note(1,"","2026-01-01","Raised D-codes 3%")));
			Assert.AreEqual("Raised D-codes 3%...",FeeSchedNotes.GetNoteSummary(Note(1,"","2026-01-01","Raised D-codes 3%\r\nsecond line")));
			Assert.AreEqual("abcde...",FeeSchedNotes.GetNoteSummary(Note(1,"","2026-01-01","abcdefgh"),5));
			Assert.AreEqual("",FeeSchedNotes.GetNoteSummary(Note(1,"","2026-01-01","")));
			Assert.AreEqual("trailing",FeeSchedNotes.GetNoteSummary(Note(1,"","2026-01-01","trailing  \r\n")));
		}
	}
}
