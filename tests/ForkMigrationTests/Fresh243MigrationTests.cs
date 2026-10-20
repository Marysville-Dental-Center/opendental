// Upgrades a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) with the fork migration runner and checks it against Open Dental 25.3.48.
// Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace ForkMigrationTests {
	[TestClass]
	public partial class Fresh243MigrationTests {
		private const string DB_NAME="forkmig_fresh243";
		private static string _skipReason;
		private static ForkMigrationResult _resultFirstRun;
		private static SchemaSnapshot _snapshotBefore;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.CreateFresh24_3(DB_NAME);
			_snapshotBefore=SchemaSnapshot.FromDatabase(DB_NAME);
			SeedSynthetic24_3Data();
			_resultFirstRun=ForkMigrationRunner.Run();
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//The next run drops it.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
		}

		///<summary>Rows a 24.3 office could have, chosen to exercise every data migration of the 25.1 and 25.2 steps. All made up.</summary>
		private static void SeedSynthetic24_3Data() {
			using(MySqlConnection connection=TestDb.OpenServer()) {
				TestDb.Exec(connection,"USE `"+DB_NAME+"`");
				//24.3 numbering: 0=Father, 1=Mother, 6=Sitter, 22=Grandchild.
				TestDb.Exec(connection,"INSERT INTO guardian (PatNumChild,PatNumGuardian,Relationship,IsGuardian) VALUES (101,201,0,1),(102,202,1,1),"
					+"(103,203,6,0),(104,204,22,0)");
				TestDb.Exec(connection,"INSERT INTO codegroup (GroupName,ProcCodes,ItemOrder,CodeGroupFixed,IsHidden,ShowInAgeLimit) VALUES "
					+"('ForkMigShown','D0001',90,0,0,0),('ForkMigHidden','D0002',91,0,1,1)");
				//Payment plan charges: a down payment debit, a translated down payment debit, an ordinary debit, and a credit whose note mentions it.
				TestDb.Exec(connection,"INSERT INTO languageforeign (ClassType,English,Culture,Translation,Comments) VALUES "
					+"('PayPlanEdit','Down payment','es-MX','Enganche','')");
				TestDb.Exec(connection,"INSERT INTO payplancharge (PayPlanNum,Guarantor,PatNum,ChargeDate,Principal,Interest,Note,ProvNum,ClinicNum,"
					+"ChargeType,ProcNum,SecDateTEntry,StatementNum,FKey,LinkType,IsOffset) VALUES "
					+"(501,1,1,'2024-01-01',100,0,'Down payment',0,0,0,0,NOW(),0,0,0,0),"
					+"(501,1,1,'2024-01-01',100,0,'Enganche',0,0,0,0,NOW(),0,0,0,0),"
					+"(501,1,1,'2024-02-01',50,0,'',0,0,0,0,NOW(),0,0,0,0),"
					+"(501,1,1,'2024-01-01',100,0,'Down payment',0,0,1,0,NOW(),0,0,0,0)");
				TestDb.Exec(connection,"UPDATE preference SET ValueString='1' WHERE PrefName='BillingShowTransSinceBalZero'");
				TestDb.Exec(connection,"INSERT INTO statement (PatNum,DateSent,DateRangeFrom,DateRangeTo,Note,NoteBold,Mode_,HidePayment,SinglePatient,"
					+"Intermingled,IsSent,DocNum,IsReceipt,IsInvoice,IsInvoiceCopy,EmailSubject,EmailBody,SuperFamily,IsBalValid,InsEst,BalTotal,"
					+"StatementType,ShortGUID,StatementShortURL,StatementURL,SmsSendStatus,LimitedCustomFamily) VALUES "
					+"(1,'2024-03-01','2024-01-01','2024-03-01','','',0,0,0,0,1,0,0,0,0,'','',0,1,0,0,'NotSet','','','',0,0)");
				TestDb.Exec(connection,"INSERT INTO eformfield (EFormNum,PatNum,FieldType,DbLink,ValueLabel,ValueString,ItemOrder,PickListVis,PickListDb,"
					+"IsHorizStacking,IsTextWrap,Width,FontScale,IsRequired,ConditionalParent,ConditionalValue,LabelAlign,SpaceBelow,ReportableName,"
					+"IsLocked,Border,IsWidthPercentage,MinWidth,WidthLabel,SpaceToRight) VALUES "
					+"(1,1,4,'','Marital status','',0,'Married|Single','Married|Single',0,0,0,100,0,'','',0,-1,'',0,0,0,0,0,-1)");
				TestDb.Exec(connection,"INSERT INTO task (TaskListNum,DateTask,KeyNum,Descript,TaskStatus,IsRepeating,DateType,FromNum,ObjectType,"
					+"DateTimeEntry,UserNum,DateTimeFinished,PriorityDefNum,ReminderGroupId,ReminderType,ReminderFrequency,DateTimeOriginal,"
					+"DescriptOverride,IsReadOnly,TriageCategory) VALUES (1,'0001-01-01',0,'Synthetic task',0,0,0,0,0,NOW(),1,'0001-01-01',0,'',0,0,NOW(),"
					+"'',0,777)");
			}
		}

		[TestMethod]
		public void Migrated_MatchesEmbeddedReference() {
			SchemaVerifyResult result=SchemaVerifier.VerifyDatabase(DB_NAME);
			Assert.IsTrue(result.IsMatch,result.GetReport());
			//The only allowed differences on a 24.3 database are tables Open Dental 25.3.48 does not have and no code uses.
			foreach(SchemaDifference difference in result.ListAllowed) {
				Assert.AreEqual(SchemaDifferenceKind.ExtraTable,difference.Kind,difference.ToString());
			}
		}

		[TestMethod]
		public void Migrated_MatchesOd253SchemaDatabase() {
			if(!TestDb.DatabaseExists(TestDb.DbNameOd253Schema)) {
				Assert.Inconclusive("The reference database "+TestDb.DbNameOd253Schema+" is not loaded on this server.");
			}
			SchemaVerifyResult result=SchemaVerifier.Compare(SchemaSnapshot.FromDatabase(TestDb.DbNameOd253Schema),SchemaSnapshot.FromDatabase(DB_NAME),
				SchemaVerifier.DocumentedExceptions25_3_48);
			Assert.IsTrue(result.IsMatch,result.GetReport());
			List<string> listAllowedOther=result.ListAllowed.Where(x => x.Kind!=SchemaDifferenceKind.ExtraTable).Select(x => x.Table+"."+x.Item).ToList();
			CollectionAssert.AreEquivalent(new[] { "patient."+SchemaVerifier.PortalActiveEmailColumn,"patient."+SchemaVerifier.PortalActiveEmailIndex },
				listAllowedOther,result.GetReport());
		}

		[TestMethod]
		public void Before_DiffersFromReference() {
			//Sanity check: the database really was at 24.3 before the runner, so the match above is the runner's work.
			SchemaVerifyResult result=SchemaVerifier.Compare(SchemaVerifier.GetReference25_3_48(),_snapshotBefore,
				SchemaVerifier.DocumentedExceptions25_3_48);
			Assert.IsFalse(result.IsMatch);
			Assert.IsTrue(result.ListMismatches.Any(x => x.Kind==SchemaDifferenceKind.MissingTable && x.Table=="feeschednote"));
			Assert.IsTrue(result.ListMismatches.Any(x => x.Kind==SchemaDifferenceKind.MissingIndex && x.Table=="claim" && x.Item=="ClaimIdentifier"));
		}

		[TestMethod]
		public void FirstRun_RanEveryStepAndRecordedTheMarkers() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			CollectionAssert.AreEqual(listSteps.Select(x => x.Level).ToList(),_resultFirstRun.ListStepsRun.Select(x => x.Level).ToList());
			Assert.AreEqual(0,ForkMigrationRunner.GetPendingSteps().Count);
			Assert.AreEqual(listSteps.Max(x => x.Level).ToString(),PrefRaw(ForkMigrationRunner.PrefNameSchemaLevel));
			Assert.AreEqual("",PrefRaw(ForkMigrationRunner.PrefNameInProgress));
			Assert.IsTrue(_resultFirstRun.ListChanges.Count>0);
		}

		[TestMethod]
		public void SecondRun_ChangesNothing() {
			Dictionary<string,string> dictBefore=TestDb.ChecksumAllTables(DB_NAME,"preference");
			SchemaSnapshot snapshotBefore=SchemaSnapshot.FromDatabase(DB_NAME);
			ForkMigrationResult resultSkip=ForkMigrationRunner.Run();
			Assert.AreEqual(0,resultSkip.ListStepsRun.Count,"Every step already finished.");
			ForkMigrationResult resultAll=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true });
			Assert.AreEqual(ForkMigrationRunner.GetSteps().Count,resultAll.ListStepsRun.Count);
			Assert.AreEqual(0,resultAll.ListChanges.Count,string.Join("\r\n",resultAll.ListChanges));
			CollectionAssert.AreEquivalent(dictBefore.ToList(),TestDb.ChecksumAllTables(DB_NAME,"preference").ToList());
			Assert.IsTrue(SchemaVerifier.Compare(snapshotBefore,SchemaSnapshot.FromDatabase(DB_NAME)).ListDifferences.Count==0);
		}

		[TestMethod]
		public void InterruptedStep_BlocksTheRunner() {
			DataCore.NonQ("UPDATE preference SET ValueString='2026101903' WHERE PrefName='"+ForkMigrationRunner.PrefNameInProgress+"'");
			try {
				Exception ex=null;
				try {
					ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true });
				}
				catch(Exception e) {
					ex=e;
				}
				Assert.IsNotNull(ex,"The runner must refuse to run after an interrupted step.");
				StringAssert.Contains(ex.Message,"2026101903");
			}
			finally {
				DataCore.NonQ("UPDATE preference SET ValueString='' WHERE PrefName='"+ForkMigrationRunner.PrefNameInProgress+"'");
			}
		}

		[TestMethod]
		public void Guardian_RenumberedTo25_1() {
			Dictionary<long,int> dict=Table("SELECT PatNumChild,Relationship FROM guardian WHERE PatNumChild BETWEEN 101 AND 104").Rows.Cast<DataRow>()
				.ToDictionary(x => PIn.Long(x[0].ToString()),x => PIn.Int(x[1].ToString()));
			Assert.AreEqual((int)GuardianRelationship.Father,dict[101]);
			Assert.AreEqual(6,dict[101]);
			Assert.AreEqual((int)GuardianRelationship.Mother,dict[102]);
			Assert.AreEqual((int)GuardianRelationship.Sitter,dict[103]);
			Assert.AreEqual(23,dict[103]);
			Assert.AreEqual((int)GuardianRelationship.Grandchild,dict[104]);
			Assert.AreEqual(GuardianRelationship.Father,Guardians.Refresh(101)[0].Relationship,"The CRUD layer reads the new numbers.");
		}

		[TestMethod]
		public void CodeGroup_ShowInFrequencyTakenFromOldIsHidden() {
			DataTable table=Table("SELECT GroupName,IsHidden,ShowInFrequency,ShowInOther FROM codegroup WHERE GroupName LIKE 'ForkMig%' ORDER BY GroupName");
			Assert.AreEqual("ForkMigHidden",table.Rows[0]["GroupName"].ToString());
			Assert.AreEqual("0",table.Rows[0]["ShowInFrequency"].ToString(),"Hidden in 24.3 means not in the frequency grid.");
			Assert.AreEqual("0",table.Rows[0]["IsHidden"].ToString(),"No group was retired before 25.1.");
			Assert.AreEqual("1",table.Rows[1]["ShowInFrequency"].ToString());
			Assert.AreEqual("0",table.Rows[1]["IsHidden"].ToString());
			Assert.AreEqual("0",table.Rows[1]["ShowInOther"].ToString());
		}

		[TestMethod]
		public void PayPlanCharge_DownPaymentsFlagged() {
			DataTable table=Table("SELECT Note,ChargeType,IsDownPayment FROM payplancharge WHERE PayPlanNum=501 ORDER BY PayPlanChargeNum");
			Assert.AreEqual("1",table.Rows[0]["IsDownPayment"].ToString(),"English note");
			Assert.AreEqual("1",table.Rows[1]["IsDownPayment"].ToString(),"Translated note");
			Assert.AreEqual("0",table.Rows[2]["IsDownPayment"].ToString(),"Ordinary charge");
			Assert.AreEqual("0",table.Rows[3]["IsDownPayment"].ToString(),"Credits are never down payments");
		}

		[TestMethod]
		public void Statement_ShowTransSinceBalZeroFromPreference() {
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ShowTransSinceBalZero FROM statement WHERE PatNum=1 AND DateSent='2024-03-01'"));
			//The default used for the back-fill is gone again, as in Open Dental's schema.
			Assert.AreEqual("tinyint(4) NOT NULL",SchemaTable.FromDatabase(DB_NAME,"statement").GetColumn("ShowTransSinceBalZero").Definition);
		}

		[TestMethod]
		public void EFormField_EnglishCopiesFilled() {
			DataTable table=Table("SELECT ValueLabelEnglish,PickListVisEnglish FROM eformfield WHERE ValueLabel='Marital status'");
			Assert.AreEqual("Marital status",table.Rows[0]["ValueLabelEnglish"].ToString());
			Assert.AreEqual("Married|Single",table.Rows[0]["PickListVisEnglish"].ToString());
		}

		[TestMethod]
		public void Task_CategoryKeptThroughRename() {
			Assert.AreEqual("777",DataCore.GetScalar("SELECT Category FROM task WHERE Descript='Synthetic task'"));
			OpenDentBusiness.Task task=OpenDentBusiness.Crud.TaskCrud.SelectOne("SELECT * FROM task WHERE Descript='Synthetic task'");
			Assert.AreEqual(777,task.Category);
		}

		private static string PrefRaw(string prefName) {
			return DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
		}

		private static DataTable Table(string command) {
			return DataCore.GetTable(command);
		}
	}
}
