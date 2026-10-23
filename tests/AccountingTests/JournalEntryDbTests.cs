// Accounting transactions against a scratch database with the Open Dental 25.3.48 schema (the reference embedded for the fork migration runner),
// filled with synthetic rows only (backlog package A-ACCT). Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace AccountingTests {
	[TestClass]
	public class JournalEntryDbTests {
		private const string SERVER="127.0.0.1";
		private const string USER="root";
		private const string PASSWORD="";
		///<summary>Every scratch database of this fixture starts with this, so the tests can never drop anything else.</summary>
		private const string PREFIX="accttest_";
		private static readonly string DB_NAME=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		private static string _skipReason;
		private static Account _accountChecking;
		private static Account _accountExpense;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("ACCOUNTING_TEST_DB")=="skip") {
				_skipReason="ACCOUNTING_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					connection.Close();
				}
			}
			catch(Exception ex) {
				_skipReason="No local MariaDB at "+SERVER+":3306 as "+USER+" with a blank password: "+ex.GetBaseException().Message;
				return;
			}
			DropStale();
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+DB_NAME+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				Exec(connection,"USE `"+DB_NAME+"`");
				//The tables this package reads and writes, exactly as Open Dental 25.3.48 defines them.
				foreach(SchemaTable table in SchemaVerifier.GetReference25_3_48().Tables
					.Where(x => x.Name.In("account","journalentry","transaction","preference","reconcile","userod")))
				{
					Exec(connection,table.ToDdl());
				}
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('DataBaseVersion','25.3.48.0')");
			}
			Connect();
			_accountChecking=InsertAccount("Checking (synthetic)",AccountType.Asset);
			_accountExpense=InsertAccount("Supplies (synthetic)",AccountType.Expense);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("ACCOUNTING_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(DB_NAME);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			Connect();
		}

		[TestMethod]
		public void NextCheckNumber_PerAccount() {
			DateTime date=new DateTime(2025,3,1);
			//Accounts of their own, so entries other tests add cannot change the answer.
			Account accountChecking=InsertAccount("Check run checking (synthetic)",AccountType.Asset);
			Account accountSavings=InsertAccount("Check run savings (synthetic)",AccountType.Asset);
			Account accountEmpty=InsertAccount("Check run petty cash (synthetic)",AccountType.Asset);
			InsertCheck(accountChecking,_accountExpense,date,"1001",10);
			InsertCheck(accountChecking,_accountExpense,date,"1005",11);
			InsertCheck(accountChecking,_accountExpense,date,"DEP",12);
			InsertCheck(accountChecking,_accountExpense,date,"AW",13);
			long transactionNumNewest=InsertCheck(accountChecking,_accountExpense,date," 1010 ",14);
			InsertCheck(accountSavings,_accountExpense,date,"2000",15);
			//An entry of another account with a far higher number does not count for the checking account.
			InsertTransaction(new[] {
				Je(_accountExpense,date,debit:16,checkNumber:"9000"),
				Je(accountSavings,date,credit:16,checkNumber:"")
			});
			Assert.AreEqual("1011",JournalEntries.GetNextCheckNumber(accountChecking.AccountNum));
			Assert.AreEqual("2001",JournalEntries.GetNextCheckNumber(accountSavings.AccountNum));
			Assert.AreEqual("1001",JournalEntries.GetNextCheckNumber(accountEmpty.AccountNum));
			//Editing the transaction that holds the highest number: its own entries are not counted.
			Assert.AreEqual("1006",JournalEntries.GetNextCheckNumber(accountChecking.AccountNum,transactionNumNewest));
		}

		[TestMethod]
		public void PayeeNotesNeedsReview_RoundTrip() {
			DateTime date=new DateTime(2025,4,2);
			string notesLong=string.Join("\r\n",Enumerable.Range(1,500).Select(x => "Line "+x+" of the synthetic notes, with 'quotes' and \\ backslash."));
			Assert.IsTrue(notesLong.Length>20000,"Notes longer than the old Memo habits, to prove the text column.");
			string payeeLong=new string('P',255);
			long transactionNum=InsertTransaction(new[] {
				Je(_accountChecking,date,credit:42.5,checkNumber:"3001",memo:"Supplies",payee:payeeLong,notes:notesLong,needsReview:true),
				Je(_accountExpense,date,debit:42.5,checkNumber:"3001",memo:"Supplies",payee:payeeLong,notes:notesLong,needsReview:true)
			});
			List<JournalEntry> list=JournalEntries.GetForTrans(transactionNum);
			Assert.AreEqual(2,list.Count);
			foreach(JournalEntry journalEntry in list) {
				Assert.AreEqual(payeeLong,journalEntry.Payee);
				Assert.AreEqual(notesLong,journalEntry.Notes);
				Assert.IsTrue(journalEntry.NeedsReview);
				Assert.AreEqual("Supplies",journalEntry.Memo);
			}
			//Transaction History reads the account's entries with the new fields.
			List<JournalEntry> listForAccount=JournalEntries.GetForAccount(_accountChecking,date,date);
			JournalEntry journalEntryChecking=listForAccount.Single(x => x.TransactionNum==transactionNum);
			Assert.AreEqual(payeeLong,journalEntryChecking.Payee);
			Assert.IsTrue(journalEntryChecking.NeedsReview);
			Assert.IsTrue(JournalEntries.MatchesFindText(journalEntryChecking,"line 250 of"));
			//The starting balance row is never flagged.
			Assert.IsFalse(listForAccount[0].NeedsReview);
			Assert.AreEqual(0,listForAccount[0].TransactionNum);
		}

		[TestMethod]
		public void UpdateList_SavesPayeeNotesAndNeedsReviewWithoutLosingMemo() {
			DateTime date=new DateTime(2025,5,3);
			long transactionNum=InsertTransaction(new[] {
				Je(_accountChecking,date,credit:5,checkNumber:"4001",memo:"Memo stays"),
				Je(_accountExpense,date,debit:5,checkNumber:"4001",memo:"Memo stays")
			});
			//Each field alone must be enough for UpdateList to write the entry; before 25.3 parity only the older fields were compared.
			AssertFieldSaved(transactionNum,x => x.Payee="Payee only",x => x.Payee=="Payee only");
			AssertFieldSaved(transactionNum,x => x.Notes="Notes only",x => x.Notes=="Notes only");
			AssertFieldSaved(transactionNum,x => x.NeedsReview=true,x => x.NeedsReview);
			AssertFieldSaved(transactionNum,x => x.NeedsReview=false,x => !x.NeedsReview);
			List<JournalEntry> list=JournalEntries.GetForTrans(transactionNum);
			Assert.IsTrue(list.TrueForAll(x => x.Memo=="Memo stays" && x.Payee=="Payee only" && x.Notes=="Notes only" && x.CheckNumber=="4001"));
		}

		[TestMethod]
		public void UpdateList_NeedsReviewWrittenOnEveryEntry() {
			DateTime date=new DateTime(2025,6,4);
			long transactionNum=InsertTransaction(new[] {
				Je(_accountChecking,date,credit:30,checkNumber:"5001"),
				Je(_accountExpense,date,debit:20,checkNumber:"5001"),
				Je(_accountExpense,date,debit:10,checkNumber:"5001",needsReview:true)
			});
			List<JournalEntry> listOld=JournalEntries.GetForTrans(transactionNum);
			Assert.IsTrue(JournalEntries.IsAnyNeedsReview(listOld),"The window loads the box checked when any entry has the flag.");
			List<JournalEntry> listNew=listOld.Select(x => x.Copy()).ToList();
			JournalEntries.SetNeedsReview(listNew,true);
			JournalEntries.UpdateList(listOld,listNew);
			Assert.IsTrue(JournalEntries.GetForTrans(transactionNum).TrueForAll(x => x.NeedsReview));
			listOld=JournalEntries.GetForTrans(transactionNum);
			listNew=listOld.Select(x => x.Copy()).ToList();
			JournalEntries.SetNeedsReview(listNew,false);
			JournalEntries.UpdateList(listOld,listNew);
			Assert.IsTrue(JournalEntries.GetForTrans(transactionNum).TrueForAll(x => !x.NeedsReview));
		}

		[TestMethod]
		public void Transaction_NeedsReviewAlwaysWrittenAsZero() {
			Transaction transaction=new Transaction { UserNum=Security.CurUser.UserNum,NeedsReview=true };
			Transactions.Insert(transaction);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT NeedsReview FROM transaction WHERE TransactionNum="+transaction.TransactionNum));
			//A row that Open Dental flagged before adoption is cleared on the next save, and the flag is never shown from it.
			DataCore.NonQ("UPDATE transaction SET NeedsReview=1 WHERE TransactionNum="+transaction.TransactionNum);
			Transaction transactionLoaded=Transactions.GetTrans(transaction.TransactionNum);
			Transactions.Update(transactionLoaded);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT NeedsReview FROM transaction WHERE TransactionNum="+transaction.TransactionNum));
		}

		[TestMethod]
		public void Transaction_DeleteRemovesEntriesWithNewFields() {
			DateTime date=new DateTime(2025,7,5);
			long transactionNum=InsertTransaction(new[] {
				Je(_accountChecking,date,credit:1,payee:"Gone",notes:"Gone",needsReview:true),
				Je(_accountExpense,date,debit:1,payee:"Gone",notes:"Gone",needsReview:true)
			});
			Transactions.Delete(Transactions.GetTrans(transactionNum));
			Assert.AreEqual(0,JournalEntries.GetForTrans(transactionNum).Count);
		}

		private static void AssertFieldSaved(long transactionNum,Action<JournalEntry> actionChange,Func<JournalEntry,bool> funcIsSaved) {
			List<JournalEntry> listOld=JournalEntries.GetForTrans(transactionNum);
			List<JournalEntry> listNew=listOld.Select(x => x.Copy()).ToList();
			listNew.ForEach(actionChange);
			JournalEntries.UpdateList(listOld,listNew);
			List<JournalEntry> listLoaded=JournalEntries.GetForTrans(transactionNum);
			Assert.IsTrue(listLoaded.TrueForAll(x => funcIsSaved(x)),"Change was not saved.");
		}

		///<summary>A two-entry check from an asset account to an expense account, the shape the simple view of Edit Transaction saves.</summary>
		private static long InsertCheck(Account accountFrom,Account accountTo,DateTime date,string checkNumber,double amount) {
			return InsertTransaction(new[] {
				Je(accountFrom,date,credit:amount,checkNumber:checkNumber,memo:"Synthetic check"),
				Je(accountTo,date,debit:amount,checkNumber:checkNumber,memo:"Synthetic check")
			});
		}

		private static long InsertTransaction(JournalEntry[] arrayJournalEntries) {
			Transaction transaction=new Transaction { UserNum=Security.CurUser.UserNum };
			Transactions.Insert(transaction);
			foreach(JournalEntry journalEntry in arrayJournalEntries) {
				journalEntry.TransactionNum=transaction.TransactionNum;
				JournalEntries.Insert(journalEntry);
			}
			return transaction.TransactionNum;
		}

		private static JournalEntry Je(Account account,DateTime date,double debit=0,double credit=0,string checkNumber="",string memo="",
			string payee="",string notes="",bool needsReview=false)
		{
			return new JournalEntry {
				AccountNum=account.AccountNum,
				DateDisplayed=date,
				DebitAmt=debit,
				CreditAmt=credit,
				CheckNumber=checkNumber,
				Memo=memo,
				Splits="",
				Payee=payee,
				Notes=notes,
				NeedsReview=needsReview,
			};
		}

		private static Account InsertAccount(string description,AccountType accountType) {
			Account account=new Account { Description=description,AcctType=accountType,BankNumber="" };
			OpenDentBusiness.Crud.AccountCrud.Insert(account);
			return account;
		}

		private static void Connect() {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(SERVER,DB_NAME,USER,PASSWORD,"","",DatabaseType.MySql,true);
			Security.CurUser=new Userod { UserNum=7,UserName="synthetic" };
			Cache.Refresh(InvalidType.Prefs);
		}

		///<summary>Drops this fixture's scratch databases that runs started more than 12 hours ago left behind.</summary>
		private static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'accttest\\_%'",connection))
			using(MySqlDataReader reader=command.ExecuteReader()) {
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
				}
			}
			foreach(string name in listNames) {
				Match match=regex.Match(name);
				DateTime dateStarted;
				if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out dateStarted))
				{
					continue;
				}
				if(DateTime.UtcNow-dateStarted<TimeSpan.FromHours(12)) {
					continue;
				}
				try {
					Drop(name);
				}
				catch(Exception) {
					//Another run may be dropping it at the same time.
				}
			}
		}

		private static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX);
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		private static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection("Server="+SERVER+";Port=3306;User ID="+USER+";Password="+PASSWORD+";Connection Timeout=5");
			connection.Open();
			return connection;
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}
	}
}
