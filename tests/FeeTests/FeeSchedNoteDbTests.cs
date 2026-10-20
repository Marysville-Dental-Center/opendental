// Fee schedule notes against a scratch database: insert, update, order, clinic filter, and cleanup with the fee schedule. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace FeeTests {
	[TestClass]
	public class FeeSchedNoteDbTests {
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.EnsureDatabase();
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.EnsureDatabase();
		}

		private static FeeSchedNote InsertNote(long feeSchedNum,string dateEntry,string clinicNums,string note) {
			FeeSchedNote feeSchedNote=new FeeSchedNote();
			feeSchedNote.FeeSchedNum=feeSchedNum;
			feeSchedNote.DateEntry=DateTime.Parse(dateEntry);
			feeSchedNote.ClinicNums=clinicNums;
			feeSchedNote.Note=note;
			FeeSchedNotes.Insert(feeSchedNote);
			return feeSchedNote;
		}

		[TestMethod]
		public void Notes_InsertSetsEntryFields_UpdateKeepsThem() {
			FeeSched feeSched=FeeDbTests.NewFeeSched();
			FeeSchedNote feeSchedNote=InsertNote(feeSched.FeeSchedNum,"2026-01-15","","Raised fictional fees 3%.\r\nSecond line.");
			FeeSchedNote feeSchedNoteDb=FeeSchedNotes.GetOne(feeSchedNote.FeeSchedNoteNum);
			Assert.AreEqual(Security.CurUser.UserNum,feeSchedNoteDb.SecUserNumEntry);
			Assert.AreEqual(DateTime.Today,feeSchedNoteDb.SecDateEntry.Date);
			Assert.AreEqual(new DateTime(2026,1,15),feeSchedNoteDb.DateEntry);
			Assert.AreEqual("Raised fictional fees 3%.\r\nSecond line.",feeSchedNoteDb.Note);
			long userNumEntry=feeSchedNoteDb.SecUserNumEntry;
			feeSchedNoteDb.Note="Edited";
			feeSchedNoteDb.DateEntry=new DateTime(2026,2,1);
			feeSchedNoteDb.SecUserNumEntry=userNumEntry+999;//never written by an update
			FeeSchedNotes.Update(feeSchedNoteDb);
			FeeSchedNote feeSchedNoteEdited=FeeSchedNotes.GetOne(feeSchedNote.FeeSchedNoteNum);
			Assert.AreEqual("Edited",feeSchedNoteEdited.Note);
			Assert.AreEqual(new DateTime(2026,2,1),feeSchedNoteEdited.DateEntry);
			Assert.AreEqual(userNumEntry,feeSchedNoteEdited.SecUserNumEntry);
			FeeSchedNotes.MakeLogEntry(feeSchedNoteEdited,"edited");
			FeeSchedNotes.Delete(feeSchedNote.FeeSchedNoteNum);
			Assert.IsNull(FeeSchedNotes.GetOne(feeSchedNote.FeeSchedNoteNum));
		}

		[TestMethod]
		public void Notes_NewestFirstAndClinicFilter() {
			FeeSched feeSched=FeeDbTests.NewFeeSched();
			FeeSched feeSchedOther=FeeDbTests.NewFeeSched();
			Clinic clinicA=FeeDbTests.NewClinic();
			Clinic clinicB=FeeDbTests.NewClinic();
			FeeSchedNote feeSchedNoteAll=InsertNote(feeSched.FeeSchedNum,"2026-01-01","","All clinics");
			FeeSchedNote feeSchedNoteA=InsertNote(feeSched.FeeSchedNum,"2026-03-01",clinicA.ClinicNum.ToString(),"Clinic A");
			FeeSchedNote feeSchedNoteB=InsertNote(feeSched.FeeSchedNum,"2026-02-01",clinicB.ClinicNum.ToString(),"Clinic B");
			InsertNote(feeSchedOther.FeeSchedNum,"2026-04-01","","Other schedule");
			CollectionAssert.AreEqual(new[] { feeSchedNoteA.FeeSchedNoteNum,feeSchedNoteB.FeeSchedNoteNum,feeSchedNoteAll.FeeSchedNoteNum },
				FeeSchedNotes.GetForFeeSched(feeSched.FeeSchedNum).Select(x => x.FeeSchedNoteNum).ToArray());
			CollectionAssert.AreEqual(new[] { feeSchedNoteA.FeeSchedNoteNum,feeSchedNoteAll.FeeSchedNoteNum },
				FeeSchedNotes.GetForFeeSchedAndClinic(feeSched.FeeSchedNum,clinicA.ClinicNum).Select(x => x.FeeSchedNoteNum).ToArray());
		}

		[TestMethod]
		public void Notes_DeletedWithTheirFeeSchedule() {
			FeeSched feeSchedKept=FeeDbTests.NewFeeSched();
			FeeSched feeSchedUnused=FeeDbTests.NewFeeSched(FeeScheduleType.OutNetwork);//not used by any plan: Cleanup deletes it
			FeeSchedNote feeSchedNoteKept=InsertNote(feeSchedKept.FeeSchedNum,"2026-01-01","","Kept");
			FeeSchedNote feeSchedNoteGone=InsertNote(feeSchedUnused.FeeSchedNum,"2026-01-01","","Goes with its schedule");
			FeeScheds.CleanupAllowedScheds();
			FeeScheds.RefreshCache();
			Assert.IsNull(FeeSchedNotes.GetOne(feeSchedNoteGone.FeeSchedNoteNum));
			Assert.IsNotNull(FeeSchedNotes.GetOne(feeSchedNoteKept.FeeSchedNoteNum));
			Assert.AreEqual(1,FeeSchedNotes.DeleteForFeeSched(feeSchedKept.FeeSchedNum));
			Assert.AreEqual(0,FeeSchedNotes.DeleteOrphans());
		}
	}
}
