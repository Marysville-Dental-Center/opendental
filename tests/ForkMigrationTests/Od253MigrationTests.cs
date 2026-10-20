// Runs the fork migration on a database shaped like the office's Open Dental 25.3.48 database: the reference schema plus the office-specific
// patient._portal_active_email column, filled with synthetic rows in 25.3 form. Nothing may change, and no data migration may run again.
// No office data is used. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace ForkMigrationTests {
	[TestClass]
	public class Od253MigrationTests {
		private static readonly string DB_NAME=TestDb.ScratchName("od253");
		private static string _skipReason;
		private static Dictionary<string,string> _dictChecksumsBefore;
		private static SchemaSnapshot _snapshotBefore;
		private static ForkMigrationResult _result;
		private static SchemaVerifyResult _resultSchemaAfterRun;
		private static Dictionary<string,string> _dictChecksumsAfter;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.Recreate(DB_NAME);
			using(MySqlConnection connection=TestDb.OpenServer()) {
				TestDb.Exec(connection,"USE `"+DB_NAME+"`");
				foreach(SchemaTable table in SchemaVerifier.GetReference25_3_48().Tables) {
					TestDb.Exec(connection,table.ToDdl());
				}
				//The office customization, as the office database has it.
				TestDb.Exec(connection,"ALTER TABLE patient ADD COLUMN `_portal_active_email` varchar(100) GENERATED ALWAYS AS (case when `PatStatus` = 0 "
					+"and `Email` <> '' then `Email` else NULL end) VIRTUAL, ADD UNIQUE KEY `portal_active_email_uniq` (`_portal_active_email`)");
				SeedSynthetic25_3Data(connection);
			}
			TestDb.Connect(DB_NAME);
			_snapshotBefore=SchemaSnapshot.FromDatabase(DB_NAME);
			_dictChecksumsBefore=TestDb.ChecksumAllTables(DB_NAME,"preference");
			//A database without fork markers: the adoption tool runs every step.
			_result=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true });
			_resultSchemaAfterRun=SchemaVerifier.Compare(_snapshotBefore,SchemaSnapshot.FromDatabase(DB_NAME));
			_dictChecksumsAfter=TestDb.ChecksumAllTables(DB_NAME,"preference");
		}

		///<summary>Rows in Open Dental 25.3 form that a second conversion would damage. All made up.</summary>
		private static void SeedSynthetic25_3Data(MySqlConnection connection) {
			TestDb.Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','25.3.48.0'),('ProgramVersion','25.3.48.0'),"
				+"('BillingShowTransSinceBalZero','1'),('RandomPrimaryKeys','0')");
			//25.1 numbering: 6=Father, 23=Sitter, 1=Mother.
			TestDb.Exec(connection,"INSERT INTO guardian (PatNumChild,PatNumGuardian,Relationship,IsGuardian) VALUES (11,21,6,1),(12,22,23,0),(13,23,1,1)");
			//A retired code group that is also out of the frequency grid, and one in the grid.
			TestDb.Exec(connection,"INSERT INTO codegroup (GroupName,ProcCodes,ItemOrder,CodeGroupFixed,IsHidden,ShowInAgeLimit,ShowInFrequency,ShowInOther) "
				+"VALUES ('Retired','D0001',0,0,1,0,0,0),('Shown','D0002',1,0,0,0,1,1)");
			//A charge whose note mentions a down payment but that Open Dental did not flag.
			TestDb.Exec(connection,"INSERT INTO payplancharge (PayPlanNum,Guarantor,PatNum,ChargeDate,Principal,Interest,Note,ProvNum,ClinicNum,ChargeType,"
				+"ProcNum,SecDateTEntry,StatementNum,FKey,LinkType,IsOffset,IsDownPayment) VALUES "
				+"(7,1,1,'2025-01-01',100,0,'Down payment',0,0,0,0,'2025-01-01 10:00:00',0,0,0,0,0)");
			//A statement saved with the option off although the preference is on.
			TestDb.Exec(connection,"INSERT INTO statement (PatNum,DateSent,DateRangeFrom,DateRangeTo,Note,NoteBold,Mode_,HidePayment,SinglePatient,"
				+"Intermingled,IsSent,DocNum,DateTStamp,IsReceipt,IsInvoice,IsInvoiceCopy,EmailSubject,EmailBody,SuperFamily,IsBalValid,InsEst,BalTotal,"
				+"StatementType,ShortGUID,StatementShortURL,StatementURL,SmsSendStatus,LimitedCustomFamily,ShowTransSinceBalZero) VALUES "
				+"(1,'2025-03-01','2025-01-01','2025-03-01','','',4,0,0,0,1,0,'2025-03-01 10:00:00',0,0,0,'','',0,1,0,0,'NotSet','','','',0,0,0)");
			//An eForm field whose English copies are empty.
			TestDb.Exec(connection,"INSERT INTO eformfield (EFormNum,PatNum,FieldType,DbLink,ValueLabel,ValueString,ItemOrder,PickListVis,PickListDb,"
				+"IsHorizStacking,IsTextWrap,Width,FontScale,IsRequired,ConditionalParent,ConditionalValue,LabelAlign,SpaceBelow,ReportableName,IsLocked,"
				+"Border,IsWidthPercentage,MinWidth,WidthLabel,SpaceToRight,AutoImport,PrefillFromGuar,ValueLabelEnglish,PickListVisEnglish) VALUES "
				+"(1,1,9,'','Insurance','',0,'A|B','A|B',0,0,0,100,0,'','',0,-1,'',0,0,0,0,0,-1,0,0,'','')");
			TestDb.Exec(connection,"INSERT INTO task (TaskListNum,DateTask,KeyNum,Descript,TaskStatus,IsRepeating,DateType,FromNum,ObjectType,DateTimeEntry,"
				+"UserNum,DateTimeFinished,PriorityDefNum,ReminderGroupId,ReminderType,ReminderFrequency,DateTimeOriginal,SecDateTEdit,DescriptOverride,"
				+"IsReadOnly,Category,TriagePosition) VALUES (1,'0001-01-01',0,'Synthetic',0,0,0,0,0,'2025-01-01 10:00:00',1,'0001-01-01 00:00:00',0,'',0,0,"
				+"'2025-01-01 10:00:00','2025-01-01 10:00:00','',0,5,0)");
			TestDb.Exec(connection,"INSERT INTO patient (LName,FName,Email,PatStatus) VALUES ('Synthetic','Pat','pat@example.invalid',0)");
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
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
		}

		[TestMethod]
		public void Migration_ChangesNoSchemaAndNoData() {
			Assert.AreEqual(0,_result.ListChanges.Count,string.Join("\r\n",_result.ListChanges));
			Assert.AreEqual(0,_resultSchemaAfterRun.ListDifferences.Count,_resultSchemaAfterRun.GetReport());
			//Byte-identical tables: no renumbering, no back-fill, nothing. Only the runner's own preference rows were added.
			CollectionAssert.AreEquivalent(_dictChecksumsBefore.ToList(),_dictChecksumsAfter.ToList());
			Assert.AreEqual(ForkMigrationRunner.GetSteps().Max(x => x.Level).ToString(),
				DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+ForkMigrationRunner.PrefNameSchemaLevel+"'"));
		}

		[TestMethod]
		public void Verifier_AllowsOnlyTheOfficeCustomization() {
			SchemaVerifyResult result=SchemaVerifier.VerifyDatabase(DB_NAME);
			Assert.IsTrue(result.IsMatch,result.GetReport());
			CollectionAssert.AreEquivalent(new[] { "patient."+SchemaVerifier.PortalActiveEmailColumn,"patient."+SchemaVerifier.PortalActiveEmailIndex },
				result.ListDifferences.Select(x => x.Table+"."+x.Item).ToList(),result.GetReport());
			Assert.IsTrue(result.ListDifferences.All(x => x.IsExcepted));
		}

		[TestMethod]
		public void Verifier_WithoutExceptionsFlagsTheOfficeCustomization() {
			SchemaVerifyResult result=SchemaVerifier.Compare(SchemaVerifier.GetReference25_3_48(),SchemaSnapshot.FromDatabase(DB_NAME));
			Assert.IsFalse(result.IsMatch);
			Assert.AreEqual(2,result.ListMismatches.Count,result.GetReport());
		}

		///<summary>What the adoption step does with the office customization, using the context helpers: afterwards the database is exactly Open Dental
		///25.3.48. The customization is put back at the end, so the other tests do not depend on the order tests run in.</summary>
		[TestMethod]
		public void DroppingTheOfficeCustomization_GivesAnExactMatch() {
			ForkMigrationContext context=new ForkMigrationContext(DB_NAME);
			try {
				Assert.IsTrue(context.DropIndexIfExists("patient",SchemaVerifier.PortalActiveEmailIndex));
				Assert.IsTrue(context.DropColumnIfExists("patient",SchemaVerifier.PortalActiveEmailColumn));
				Assert.IsFalse(context.DropColumnIfExists("patient",SchemaVerifier.PortalActiveEmailColumn),"Idempotent.");
				SchemaVerifyResult result=SchemaVerifier.VerifyDatabase(DB_NAME);
				Assert.AreEqual(0,result.ListDifferences.Count,result.GetReport());
				//Family members can share an e-mail address again.
				DataCore.NonQ("INSERT INTO patient (LName,FName,Email,PatStatus) VALUES ('Synthetic','Kid','pat@example.invalid',0)");
			}
			finally {
				DataCore.NonQ("DELETE FROM patient WHERE LName='Synthetic' AND FName='Kid'");
				if(!context.ColumnExists("patient",SchemaVerifier.PortalActiveEmailColumn)) {
					DataCore.NonQ("ALTER TABLE patient ADD COLUMN `_portal_active_email` varchar(100) GENERATED ALWAYS AS (case when `PatStatus` = 0 "
						+"and `Email` <> '' then `Email` else NULL end) VIRTUAL, ADD UNIQUE KEY `portal_active_email_uniq` (`_portal_active_email`)");
				}
			}
		}
	}
}
