// Rules for Payee, Notes, Needs Review and the next check number that need no database (backlog package A-ACCT).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace AccountingTests {
	[TestClass]
	public class JournalEntryRulesTests {
		#region Next check number
		[TestMethod]
		public void NextCheckNumber_NoNumbers_StartsAt1001() {
			Assert.AreEqual("1001",JournalEntries.CalcNextCheckNumber(new List<string>()));
			Assert.AreEqual("1001",JournalEntries.CalcNextCheckNumber(null));
			Assert.AreEqual("1001",JournalEntries.CalcNextCheckNumber(new List<string> { "",null,"   " }));
		}

		[TestMethod]
		public void NextCheckNumber_NonNumericIgnored() {
			Assert.AreEqual("1001",JournalEntries.CalcNextCheckNumber(new List<string> { "DEP","AW","Adj","D" }));
			Assert.AreEqual("8",JournalEntries.CalcNextCheckNumber(new List<string> { "DEP","7","AW" }));
		}

		[TestMethod]
		public void NextCheckNumber_PartlyNumericIgnored() {
			Assert.AreEqual("1001",JournalEntries.CalcNextCheckNumber(new List<string> { "12a","1.5","-3","+4","1 2","#55" }));
			Assert.AreEqual("21",JournalEntries.CalcNextCheckNumber(new List<string> { "20","9999x" }));
		}

		[TestMethod]
		public void NextCheckNumber_ComparesNumbersNotText() {
			//As text "9" sorts after "10"; as numbers 10 is the highest.
			Assert.AreEqual("11",JournalEntries.CalcNextCheckNumber(new List<string> { "9","10","2" }));
		}

		[TestMethod]
		public void NextCheckNumber_SurroundingSpacesAllowed() {
			Assert.AreEqual("1011",JournalEntries.CalcNextCheckNumber(new List<string> { " 1010 ","1005" }));
		}

		[TestMethod]
		public void NextCheckNumber_KeepsLeadingZeros() {
			Assert.AreEqual("000124",JournalEntries.CalcNextCheckNumber(new List<string> { "000123","45" }));
			Assert.AreEqual("0100",JournalEntries.CalcNextCheckNumber(new List<string> { "0099" }));
			//The same number written two ways: the wider spelling sets the width.
			Assert.AreEqual("0006",JournalEntries.CalcNextCheckNumber(new List<string> { "5","0005" }));
		}

		[TestMethod]
		public void NextCheckNumber_TooLargeIgnored() {
			Assert.AreEqual("1001",JournalEntries.CalcNextCheckNumber(new List<string> { "99999999999999999999999" }));
			Assert.AreEqual("1001",JournalEntries.CalcNextCheckNumber(new List<string> { long.MaxValue.ToString() }));
			Assert.AreEqual("31",JournalEntries.CalcNextCheckNumber(new List<string> { "99999999999999999999999","30" }));
		}
		#endregion Next check number

		#region Find Text
		[TestMethod]
		public void FindText_SearchesPayeeAndNotes() {
			JournalEntry journalEntry=Entry("Office supplies","1001","Payee Co","Paid early\r\nInvoice 77");
			Assert.IsTrue(JournalEntries.MatchesFindText(journalEntry,"payee co"));
			Assert.IsTrue(JournalEntries.MatchesFindText(journalEntry,"INVOICE 77"));
			Assert.IsTrue(JournalEntries.MatchesFindText(journalEntry,"supplies"));
			Assert.IsTrue(JournalEntries.MatchesFindText(journalEntry,"1001"));
			Assert.IsTrue(JournalEntries.MatchesFindText(journalEntry,"Checking"));//splits
			Assert.IsFalse(JournalEntries.MatchesFindText(journalEntry,"rent"));
		}

		[TestMethod]
		public void FindText_EmptyMatchesAllAndNullFieldsAreSafe() {
			JournalEntry journalEntry=new JournalEntry { Memo=null,CheckNumber=null,Splits=null,Payee=null,Notes=null };
			Assert.IsTrue(JournalEntries.MatchesFindText(journalEntry,""));
			Assert.IsTrue(JournalEntries.MatchesFindText(journalEntry,null));
			Assert.IsFalse(JournalEntries.MatchesFindText(journalEntry,"x"));
		}
		#endregion Find Text

		#region Shared fields (simple view and "same for all")
		[TestMethod]
		public void SharedFields_SameOnlyWhenMemoPayeeAndNotesAgree() {
			List<JournalEntry> list=new List<JournalEntry> { Entry("M","","P","N"),Entry("M","","P","N") };
			Assert.IsTrue(JournalEntries.AreSharedFieldsSame(list));
			list[1].Payee="Other";
			Assert.IsFalse(JournalEntries.AreSharedFieldsSame(list),"A different payee needs the compound view.");
			list[1].Payee="P";
			list[1].Notes="Other";
			Assert.IsFalse(JournalEntries.AreSharedFieldsSame(list),"Different notes need the compound view.");
			list[1].Notes="N";
			list[1].Memo="Other";
			Assert.IsFalse(JournalEntries.AreSharedFieldsSame(list),"Different memos still need the compound view.");
		}

		[TestMethod]
		public void SharedFields_NullEqualsEmptyAndShortListsAreSame() {
			Assert.IsTrue(JournalEntries.AreSharedFieldsSame(new List<JournalEntry> { Entry("M","",null,null),Entry("M","","","") }));
			Assert.IsTrue(JournalEntries.AreSharedFieldsSame(new List<JournalEntry>()));
			Assert.IsTrue(JournalEntries.AreSharedFieldsSame(null));
			Assert.IsTrue(JournalEntries.AreSharedFieldsSame(new List<JournalEntry> { Entry("M","","P","N") }));
		}

		[TestMethod]
		public void SharedFields_CopyLeavesAmountsAndCheckNumbers() {
			JournalEntry source=Entry("Memo new","1","Payee new","Notes new");
			List<JournalEntry> list=new List<JournalEntry> { Entry("a","2","b","c"),Entry("d","3","e","f") };
			list[0].DebitAmt=12.5;
			JournalEntries.CopySharedFields(source,list);
			foreach(JournalEntry journalEntry in list) {
				Assert.AreEqual("Memo new",journalEntry.Memo);
				Assert.AreEqual("Payee new",journalEntry.Payee);
				Assert.AreEqual("Notes new",journalEntry.Notes);
			}
			Assert.AreEqual("2",list[0].CheckNumber);
			Assert.AreEqual("3",list[1].CheckNumber);
			Assert.AreEqual(12.5,list[0].DebitAmt);
		}
		#endregion Shared fields

		#region Needs Review
		[TestMethod]
		public void NeedsReview_AnyEntryFlagsTheTransaction() {
			List<JournalEntry> list=new List<JournalEntry> { Entry("","","",""),Entry("","","","") };
			Assert.IsFalse(JournalEntries.IsAnyNeedsReview(list));
			list[1].NeedsReview=true;
			Assert.IsTrue(JournalEntries.IsAnyNeedsReview(list));
			Assert.IsFalse(JournalEntries.IsAnyNeedsReview(null));
		}

		[TestMethod]
		public void NeedsReview_SetWritesEveryEntry() {
			List<JournalEntry> list=new List<JournalEntry> { Entry("","","",""),Entry("","","",""),Entry("","","","") };
			list[0].NeedsReview=true;
			JournalEntries.SetNeedsReview(list,true);
			Assert.IsTrue(list.TrueForAll(x => x.NeedsReview));
			JournalEntries.SetNeedsReview(list,false);
			Assert.IsTrue(list.TrueForAll(x => !x.NeedsReview));
		}
		#endregion Needs Review

		#region Audit text
		[TestMethod]
		public void Audit_NoChangeGivesEmptyText() {
			List<JournalEntry> listOld=new List<JournalEntry> { Entry("m","1","Payee","n") };
			List<JournalEntry> listNew=new List<JournalEntry> { Entry("changed memo","2","Payee","changed notes") };
			Assert.AreEqual("",JournalEntries.GetAuditChangeText(listOld,listNew));
		}

		[TestMethod]
		public void Audit_PayeeChangeShowsOldAndNew() {
			List<JournalEntry> listOld=new List<JournalEntry> { Entry("m","","Old Payee",""),Entry("m","","Old Payee","") };
			List<JournalEntry> listNew=new List<JournalEntry> { Entry("m","","New Payee",""),Entry("m","","New Payee","") };
			string text=JournalEntries.GetAuditChangeText(listOld,listNew);
			StringAssert.Contains(text,"'Old Payee' -> 'New Payee'");
			Assert.IsFalse(text.Contains("Needs Review"),"Needs Review did not change: "+text);
		}

		[TestMethod]
		public void Audit_NeedsReviewChangeShowsOffToOn() {
			List<JournalEntry> listOld=new List<JournalEntry> { Entry("m","","P",""),Entry("m","","P","") };
			List<JournalEntry> listNew=new List<JournalEntry> { Entry("m","","P",""),Entry("m","","P","") };
			JournalEntries.SetNeedsReview(listNew,true);
			StringAssert.Contains(JournalEntries.GetAuditChangeText(listOld,listNew),"off -> on");
			StringAssert.Contains(JournalEntries.GetAuditChangeText(listNew,listOld),"on -> off");
		}

		[TestMethod]
		public void Audit_NewTransactionWithPayee() {
			List<JournalEntry> listNew=new List<JournalEntry> { Entry("m","","Payee Co",""),Entry("m","","Payee Co","") };
			StringAssert.Contains(JournalEntries.GetAuditChangeText(new List<JournalEntry>(),listNew),"'' -> 'Payee Co'");
			Assert.AreEqual("",JournalEntries.GetAuditChangeText(new List<JournalEntry>(),new List<JournalEntry> { Entry("m","","","") }));
		}

		[TestMethod]
		public void PayeeSummary_DistinctNonEmptyInOrder() {
			List<JournalEntry> list=new List<JournalEntry> { Entry("","","B",""),Entry("","","",""),Entry("","","A",""),Entry("","","B","") };
			Assert.AreEqual("B, A",JournalEntries.GetPayeeSummary(list));
			Assert.AreEqual("",JournalEntries.GetPayeeSummary(null));
		}
		#endregion Audit text

		[TestMethod]
		public void NewJournalEntry_DefaultsMatchTheNotNullColumns() {
			//journalentry.Payee and Notes are NOT NULL without a default in Open Dental 25.3.48, so a new entry must never carry null.
			JournalEntry journalEntry=new JournalEntry();
			Assert.AreEqual("",journalEntry.Payee);
			Assert.AreEqual("",journalEntry.Notes);
			Assert.IsFalse(journalEntry.NeedsReview);
			Assert.AreEqual(255,JournalEntries.PAYEE_MAX_LENGTH);
			Assert.AreEqual(65535,JournalEntries.NOTES_MAX_LENGTH);
		}

		private static JournalEntry Entry(string memo,string checkNumber,string payee,string notes) {
			return new JournalEntry { Memo=memo,CheckNumber=checkNumber,Payee=payee,Notes=notes,Splits="Checking" };
		}
	}
}
