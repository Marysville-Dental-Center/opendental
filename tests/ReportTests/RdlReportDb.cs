// One scratch database for the RDL report tests (package RDL-SRC, docs/port-notes/RDL-SRC.md): a fresh 24.3.63 database built like
// tools/New-DevDatabase.ps1 (the seed dump and the frozen convert chain), brought up to date by the fork migration runner, with a few synthetic
// rows for the reports that load ODR.dll. Built the first time an RDL test class starts, dropped when the run ends. Skipped (inconclusive) when
// the local MariaDB does not answer. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace ReportTests {
	[TestClass]
	public class RdlReportDb {
		public static readonly string DbName=TestDb.ScratchName("rdlsrc");
		///<summary>The synthetic patient, procedure code and practice title the report pages must show.</summary>
		public const string PatLName="Rdlsynthetic";
		public const string PatFName="Ann";
		public const string ProcCode="ZRDL1";
		public const string ProcDescript="Synthetic RDL procedure";
		public const string PracticeTitle="Synthetic RDL Practice";
		private static readonly object _lock=new object();
		private static bool _isBuildTried;
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		private static string _skipReason;

		///<summary>Builds and seeds the database once per run.</summary>
		public static void EnsureBuilt() {
			lock(_lock) {
				if(_isBuildTried) {
					return;
				}
				_isBuildTried=true;
				UseUsEnglish();
				_skipReason=TestDb.GetSkipReason();
				if(_skipReason!=null) {
					return;
				}
				try {
					TestDb.DropStaleScratchDatabases();
					TestDb.CreateFresh24_3(DbName);
					ForkMigrationRunner.Run();
					Seed();
					Security.CurUser=new Userod { UserNum=1,UserName="synthetic" };
					Cache.Refresh(InvalidType.Prefs);
				}
				catch(Exception ex) {
					_skipReason="Could not build the scratch database "+DbName+": "+ex.GetBaseException().Message;
				}
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isBuildTried || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				if(TestDb.DatabaseExists(DbName)) {
					TestDb.Drop(DbName);
				}
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		///<summary>Call from [TestInitialize] of a database test: skips the test when there is no database, otherwise connects to it.</summary>
		public static void Begin() {
			UseUsEnglish();
			EnsureBuilt();
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DbName);
		}

		///<summary>One patient with a treatment-planned procedure in 2025 (the report shows it), a completed one and an older planned one (it does
		///not), and a small chart of accounts with 2006 and 2007 journal entries for the accounting reports, which are fixed to the year 2007.
		///2007 balances: Checking 6,500.00 (5,000 capital + 300 loan + 400 fees in 2006, + 1,000 fees - 200 supplies in 2007), Loan 300.00, Capital
		///5,000.00, retained earnings 400.00 (2006 income), net income 2007 800.00.</summary>
		private static void Seed() {
			using(MySqlConnection connection=TestDb.OpenServer()) {
				TestDb.Exec(connection,"USE `"+DbName+"`");
				//Blank sql_mode, as the program runs: the rows only name the columns the reports read.
				TestDb.Exec(connection,"SET SESSION sql_mode=''");
				TestDb.Exec(connection,"UPDATE preference SET ValueString='"+PracticeTitle+"' WHERE PrefName='PracticeTitle'");
				TestDb.Exec(connection,"INSERT INTO patient (LName,FName,PatStatus) VALUES ('"+PatLName+"','"+PatFName+"',0)");
				TestDb.Exec(connection,"SET @pat=LAST_INSERT_ID()");
				TestDb.Exec(connection,"INSERT INTO procedurecode (ProcCode,Descript,AbbrDesc) VALUES ('"+ProcCode+"','"+ProcDescript+"','SynRdl')");
				TestDb.Exec(connection,"SET @code=LAST_INSERT_ID()");
				TestDb.Exec(connection,"INSERT INTO procedurelog (PatNum,CodeNum,ProcDate,ProcStatus,ProcFee,ToothNum) VALUES "
					+"(@pat,@code,'2025-03-14',1,123.45,'8'),"//treatment planned, in the date range: shown
					+"(@pat,@code,'2025-03-15',2,99.99,'9'),"//complete: not shown
					+"(@pat,@code,'2019-01-02',1,77.77,'10')");//treatment planned, before the date range: not shown
				//Only the synthetic accounts, so the totals are exact.
				TestDb.Exec(connection,"DELETE FROM journalentry");
				TestDb.Exec(connection,"DELETE FROM account");
				TestDb.Exec(connection,"INSERT INTO account (Description,AcctType) VALUES ('Synthetic Checking',0)");
				TestDb.Exec(connection,"SET @chk=LAST_INSERT_ID()");
				TestDb.Exec(connection,"INSERT INTO account (Description,AcctType) VALUES ('Synthetic Loan',1)");
				TestDb.Exec(connection,"SET @loan=LAST_INSERT_ID()");
				TestDb.Exec(connection,"INSERT INTO account (Description,AcctType) VALUES ('Synthetic Capital',2)");
				TestDb.Exec(connection,"SET @cap=LAST_INSERT_ID()");
				TestDb.Exec(connection,"INSERT INTO account (Description,AcctType) VALUES ('Synthetic Fees',3)");
				TestDb.Exec(connection,"SET @fees=LAST_INSERT_ID()");
				TestDb.Exec(connection,"INSERT INTO account (Description,AcctType) VALUES ('Synthetic Supplies',4)");
				TestDb.Exec(connection,"SET @sup=LAST_INSERT_ID()");
				TestDb.Exec(connection,"INSERT INTO journalentry (AccountNum,DateDisplayed,DebitAmt,CreditAmt,Memo,Splits,CheckNumber) VALUES "
					+"(@chk,'2006-06-01',5000,0,'Synthetic capital','',''),(@cap,'2006-06-01',0,5000,'Synthetic capital','',''),"
					+"(@chk,'2006-07-01',300,0,'Synthetic loan','',''),(@loan,'2006-07-01',0,300,'Synthetic loan','',''),"
					+"(@chk,'2006-08-01',400,0,'Synthetic 2006 fees','',''),(@fees,'2006-08-01',0,400,'Synthetic 2006 fees','',''),"
					+"(@chk,'2007-03-01',1000,0,'Synthetic 2007 fees','',''),(@fees,'2007-03-01',0,1000,'Synthetic 2007 fees','',''),"
					+"(@sup,'2007-04-01',200,0,'Synthetic supplies','','101'),(@chk,'2007-04-01',0,200,'Synthetic supplies','','101')");
			}
		}

		///<summary>Dates and amounts are checked as an en-US workstation shows them.</summary>
		public static void UseUsEnglish() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
		}
	}
}
