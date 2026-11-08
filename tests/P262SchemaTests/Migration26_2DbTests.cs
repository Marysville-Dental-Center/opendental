// The Open Dental 26.2 step (OdParity26_2Schema, 2026110701) on scratch databases with synthetic rows:
// - a database at the 26.1.70 reference (the schema the 25.4 and 26.1 steps leave) gets exactly the 26.2.0 reference; existing rows keep their
//   values and the new columns hold their "nothing" value; a second run changes nothing;
// - a table that already has some of the 26.2 columns is finished, not broken;
// - a database already at 26.2.0 (as Open Dental 26.2 would leave it) is not changed, and its 26.2 values are kept;
// - an office-shaped Open Dental 25.3.48 database adopted with the real runner (the full chain of fork steps) matches the 26.2.0 reference and,
//   when the documents are available, Open Dental's published 26.2 document exactly.
// Skipped (inconclusive) when the local MariaDB does not answer. No office data is used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace P262SchemaTests {
	[TestClass]
	public class Migration26_2DbTests {
		private static readonly string DB_OD261=ScratchDb.Name("od261");
		private static readonly string DB_OD262=ScratchDb.Name("od262");
		private static readonly string DB_PART=ScratchDb.Name("part");
		private static readonly string DB_OD253=ScratchDb.Name("od253");
		private static string _skipReason;
		//26.1.70 database
		private static Dictionary<string,string> _dictChecksumsBefore;
		private static Dictionary<string,string> _dictChecksumsAfter;
		private static ForkMigrationResult _resultFirst;
		private static ForkMigrationResult _resultSecond;
		private static Dictionary<string,string> _dictChecksumsAfterSecond;
		private static SchemaSnapshot _snapshotAfterFirst;
		private static SchemaSnapshot _snapshotAfterSecond;
		private static DateTime _dateTimeServerBefore;
		private static DateTime _dateTimeServerAfter;
		//26.2.0 database
		private static ForkMigrationResult _resultOd262;
		private static Dictionary<string,string> _dictChecksumsOd262Before;
		private static Dictionary<string,string> _dictChecksumsOd262After;
		private static SchemaVerifyResult _resultSchemaOd262;
		//Partly converted database
		private static ForkMigrationResult _resultPart;
		//25.3.48 database, adopted
		private static AdoptionResult _adoptionResult;
		private static ForkDbState _forkDbStateAdopted;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropStale();
			Security.CurUser=Security.CurUser??new Userod { UserNum=1,UserName="Synthetic" };
			//A database at 26.1.70, with rows in the tables 26.2 changes and in one it does not.
			ScratchDb.CreateFromSnapshot(DB_OD261,SchemaVerifier.GetReference26_1_70());
			SeedSynthetic26_1Data(DB_OD261);
			_dictChecksumsBefore=ScratchDb.ChecksumAllTables(DB_OD261);
			ScratchDb.Connect(DB_OD261);
			_dateTimeServerBefore=DateTime.Parse(ScratchDb.Scalar(DB_OD261,"SELECT DATE_FORMAT(NOW(),'%Y-%m-%d %H:%i:%s')"));
			_resultFirst=RunStep();
			_dateTimeServerAfter=DateTime.Parse(ScratchDb.Scalar(DB_OD261,"SELECT DATE_FORMAT(NOW(),'%Y-%m-%d %H:%i:%s')"));
			_snapshotAfterFirst=SchemaSnapshot.FromDatabase(DB_OD261);
			_dictChecksumsAfter=ScratchDb.ChecksumAllTables(DB_OD261);
			_resultSecond=RunStep();
			_snapshotAfterSecond=SchemaSnapshot.FromDatabase(DB_OD261);
			_dictChecksumsAfterSecond=ScratchDb.ChecksumAllTables(DB_OD261);
			//A database Open Dental already converted to 26.2.
			ScratchDb.CreateFromSnapshot(DB_OD262,SchemaVerifier.GetReference26_2_0());
			SeedSynthetic26_2Data(DB_OD262);
			_dictChecksumsOd262Before=ScratchDb.ChecksumAllTables(DB_OD262);
			ScratchDb.Connect(DB_OD262);
			SchemaSnapshot snapshotOd262Before=SchemaSnapshot.FromDatabase(DB_OD262);
			_resultOd262=RunStep();
			_resultSchemaOd262=SchemaVerifier.Compare(snapshotOd262Before,SchemaSnapshot.FromDatabase(DB_OD262));
			_dictChecksumsOd262After=ScratchDb.ChecksumAllTables(DB_OD262);
			//A database where provider already has ClaimFormNum without its key, and apisubscription the first two of its four new columns.
			SchemaSnapshot snapshotPart=new SchemaSnapshot();
			snapshotPart.AddTable(SchemaTable.Parse(SchemaVerifier.GetReference26_1_70().GetTable("preference").ToDdl()));
			SchemaTable tableProvider=SchemaTable.Parse(SchemaVerifier.GetReference26_1_70().GetTable("provider").ToDdl());
			tableProvider.Columns.Add(new SchemaColumn { Name="ClaimFormNum",Definition="bigint(20) NOT NULL",Position=tableProvider.Columns.Count+1 });
			snapshotPart.AddTable(tableProvider);
			SchemaTable tableApiSubscription=SchemaTable.Parse(SchemaVerifier.GetReference26_1_70().GetTable("apisubscription").ToDdl());
			tableApiSubscription.Columns.Add(new SchemaColumn { Name="DateTimeLastFailure",Definition="datetime NOT NULL DEFAULT '0001-01-01 00:00:00'",
				Position=tableApiSubscription.Columns.Count+1 });
			tableApiSubscription.Columns.Add(new SchemaColumn { Name="FailureReason",Definition="text NOT NULL",Position=tableApiSubscription.Columns.Count+1 });
			snapshotPart.AddTable(tableApiSubscription);
			foreach(string table in new[] { "insbluebook","labcase","patient" }) {
				snapshotPart.AddTable(SchemaTable.Parse(SchemaVerifier.GetReference26_1_70().GetTable(table).ToDdl()));
			}
			ScratchDb.CreateFromSnapshot(DB_PART,snapshotPart);
			ScratchDb.Exec(DB_PART,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')",
				"INSERT INTO provider (Abbr,FeeSched,Specialty,SchoolClassNum,AnesthProvType,TaxonomyCodeOverride,EcwID,StateRxID,StateWhereLicensed,"
					+"EmailAddressNum,ProvNumBillingOverride,CustomID,SchedNote,WebSchedDescript,WebSchedImageLocation,PreferredName,ClaimFormNum) VALUES "
					+"('SYN1',1,0,0,0,'','','','',0,0,'','','','','',7)");
			ScratchDb.Connect(DB_PART);
			_resultPart=RunStep();
			//The full chain: an office copy at 25.3.48, adopted as the adoption tool adopts it.
			ScratchDb.CreateFromSnapshot(DB_OD253,SchemaVerifier.GetReference25_3_48());
			ScratchDb.Exec(DB_OD253,
				"INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','25.3.48.0'),('ProgramVersion','25.3.48.0'),"
					+"('CorruptedDatabase','0'),('UpdateInProgressOnComputerName',''),('RandomPrimaryKeys','0'),('DxIcdVersion','9')",
				"INSERT INTO patient (LName,FName,Email,PatStatus) VALUES ('Synthetic','Patient','patient@example.invalid',0)",
				"INSERT INTO provider (Abbr,FeeSched,Specialty,SchoolClassNum,AnesthProvType,TaxonomyCodeOverride,EcwID,StateRxID,StateWhereLicensed,"
					+"EmailAddressNum,ProvNumBillingOverride,CustomID,SchedNote,WebSchedDescript,WebSchedImageLocation,PreferredName) VALUES "
					+"('SYN1',1,0,0,0,'','','','',0,0,'','','','','')");
			ScratchDb.Connect(DB_OD253);
			_adoptionResult=Adoption.Run();
			_forkDbStateAdopted=ForkVersions.ReadState();
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			foreach(string name in new[] { DB_OD261,DB_OD262,DB_PART,DB_OD253 }) {
				ScratchDb.DropQuietly(name);
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
		}

		///<summary>Runs only the 26.2 step on the connected database through the real runner.</summary>
		private static ForkMigrationResult RunStep() {
			return ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true,ListSteps=new List<ForkMigrationStep> { new OdParity26_2Schema() } });
		}

		///<summary>Rows an office could have at 26.1 in the tables 26.2 changes, plus a patient and a lab case. All made up. Columns left out get the
		///server's implicit default (blank sql_mode), as Open Dental's own inserts rely on.</summary>
		private static void SeedSynthetic26_1Data(string dbName) {
			ScratchDb.Exec(dbName,
				"INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','26.1.70.0'),('ProgramVersion','26.1.70.0'),('RandomPrimaryKeys','0')",
				"INSERT INTO patient (LName,FName,Email,PatStatus) VALUES ('Synthetic','Patient','patient@example.invalid',0)",
				"INSERT INTO provider (Abbr,LName,FeeSched,Specialty,SchoolClassNum,AnesthProvType,TaxonomyCodeOverride,EcwID,StateRxID,StateWhereLicensed,"
					+"EmailAddressNum,ProvNumBillingOverride,CustomID,SchedNote,WebSchedDescript,WebSchedImageLocation,PreferredName,DateTStamp) VALUES "
					+"('SYN1','Synthetic',1,0,0,0,'','','','',0,0,'','','','','Doc One','2025-01-02 03:04:05'),"
					+"('SYN2','Synthetic',1,0,0,0,'','','','',0,0,'','','','','Doc Two','2025-01-02 03:04:06')",
				"INSERT INTO apisubscription (EndPointUrl,Workstation,CustomerKey,WatchTable,PollingSeconds,UiEventType,DateTimeStart,DateTimeStop,Note) "
					+"VALUES ('https://example.invalid/hook','','SYNKEY','Patient',60,'','2026-01-01 08:00:00','0001-01-01 00:00:00','Synthetic')",
				"INSERT INTO insbluebook (ProcCodeNum,CarrierNum,PlanNum,GroupNum,InsPayAmt,AllowedOverride,DateTEntry,ProcNum,ProcDate,ClaimType,ClaimNum) "
					+"VALUES (1,2,3,'G1',80,100,'2026-02-01 09:00:00',4,'2026-01-31','P',5),(6,2,3,'G1',40,-1,'2026-02-02 09:00:00',7,'2026-01-30','S',8)",
				"INSERT INTO labcase (PatNum,LaboratoryNum,AptNum,PlannedAptNum,ProvNum,Instructions,LabFee,InvoiceNum) VALUES (1,1,0,0,1,'Synthetic',50,'INV-1')"
			);
		}

		///<summary>Rows in 26.2 form, with values in every new column and new table, that a second conversion would damage. All made up.</summary>
		private static void SeedSynthetic26_2Data(string dbName) {
			ScratchDb.Exec(dbName,
				"INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','26.2.0.0'),('ProgramVersion','26.2.0.0'),('RandomPrimaryKeys','0')",
				"INSERT INTO provider (Abbr,FeeSched,Specialty,SchoolClassNum,AnesthProvType,TaxonomyCodeOverride,EcwID,StateRxID,StateWhereLicensed,"
					+"EmailAddressNum,ProvNumBillingOverride,CustomID,SchedNote,WebSchedDescript,WebSchedImageLocation,PreferredName,ClaimFormNum,"
					+"GlidewellAcctNum,DateTStamp) VALUES ('SYN1',1,0,0,0,'','','','',0,0,'','','','','',12,1,'2026-03-04 05:06:07')",
				"INSERT INTO apisubscription (EndPointUrl,Workstation,CustomerKey,WatchTable,PollingSeconds,UiEventType,Note,DateTimeLastFailure,"
					+"FailureReason,SubsequentFailures,DateTimeNextRetry) VALUES ('https://example.invalid/hook','','SYNKEY','Patient',60,'','',"
					+"'2026-03-01 10:00:00','Synthetic timeout',3,'2026-03-01 10:30:00')",
				"INSERT INTO insbluebook (ProcCodeNum,CarrierNum,PlanNum,GroupNum,InsPayAmt,AllowedOverride,ProcNum,ClaimType,ClaimNum,DateTStamp) VALUES "
					+"(1,2,3,'G1',80,100,4,'P',5,'2026-03-02 11:00:00')",
				"INSERT INTO glidewellacct (AccountNumber,ApiKey,DateTimeExpires) VALUES ('00-0000001','synthetic-encrypted-key','2026-04-01 00:00:00')",
				"INSERT INTO glidewellmsg (LabCaseNum,OrderStatus,VendorRxId,PatNum,GlidewellAcctNum,RawJsonResponse,RawJsonRequest,SentITero,SentMedit) "
					+"VALUES (1,4,'SYN-RX-1',1,1,'{\"status\":\"Shipped\"}','{\"OrderId\":1}',1,0)",
				"INSERT INTO labcaseproduct (LabCaseNum,PatNum,GlidewellProduct,Material,Shade,ToothRange,Notes) VALUES (1,1,3,'Zirconia','A1','8-9','Synthetic')"
			);
		}

		[TestMethod]
		public void Od261_StepBringsTheSchemaExactlyTo26_2() {
			SchemaVerifyResult result=SchemaVerifier.Compare(SchemaVerifier.GetReference26_2_0(),_snapshotAfterFirst);
			Assert.AreEqual(0,result.ListDifferences.Count,result.GetReport());
			Assert.AreEqual(1,_resultFirst.ListStepsChanged.Count);
			//One ALTER per changed table (provider's two columns and two keys together) and one CREATE per new table.
			List<string> listChanges=_resultFirst.ListChanges;
			Assert.AreEqual(6,listChanges.Count,string.Join("\r\n",listChanges));
			Assert.AreEqual(1,listChanges.Count(x => x.StartsWith("2026110701: ALTER TABLE `provider` ADD COLUMN `ClaimFormNum`")
				&& x.Contains("ADD COLUMN `GlidewellAcctNum`") && x.Contains("ADD INDEX `ClaimFormNum`") && x.Contains("ADD INDEX `GlidewellAcctNum`")));
			Assert.AreEqual(1,listChanges.Count(x => x.StartsWith("2026110701: ALTER TABLE `apisubscription`")));
			Assert.AreEqual(1,listChanges.Count(x => x.StartsWith("2026110701: ALTER TABLE `insbluebook`")));
			foreach(string table in new[] { "glidewellacct","glidewellmsg","labcaseproduct" }) {
				Assert.AreEqual(1,listChanges.Count(x => x.StartsWith("2026110701: CREATE TABLE `"+table+"`")),table);
			}
		}

		[TestMethod]
		public void Od261_SecondRunChangesNothing() {
			Assert.AreEqual(1,_resultSecond.ListStepsRun.Count);
			Assert.AreEqual(0,_resultSecond.ListChanges.Count,string.Join("\r\n",_resultSecond.ListChanges));
			Assert.AreEqual(0,SchemaVerifier.Compare(_snapshotAfterFirst,_snapshotAfterSecond).ListDifferences.Count);
			CollectionAssert.AreEquivalent(_dictChecksumsAfter.Where(x => x.Key!="preference").ToList(),
				_dictChecksumsAfterSecond.Where(x => x.Key!="preference").ToList());
		}

		///<summary>Existing rows keep every value; the new columns hold 0, empty or 0001-01-01; insbluebook.DateTStamp holds the time of the ALTER in
		///every row; provider.DateTStamp does not move; the new tables are empty; tables 26.2 does not change are byte-identical.</summary>
		[TestMethod]
		public void Od261_RowsKeepTheirValuesAndTheNewColumnsHoldNothing() {
			Assert.AreEqual("SYN1|Doc One|0|0|2025-01-02 03:04:05,SYN2|Doc Two|0|0|2025-01-02 03:04:06",ScratchDb.Scalar(DB_OD261,
				"SELECT GROUP_CONCAT(CONCAT(Abbr,'|',PreferredName,'|',ClaimFormNum,'|',GlidewellAcctNum,'|',DATE_FORMAT(DateTStamp,'%Y-%m-%d %H:%i:%s')) "
				+"ORDER BY ProvNum) FROM provider"),"The provider rows were not edited: ALTER TABLE keeps their DateTStamp.");
			Assert.AreEqual("https://example.invalid/hook|SYNKEY|60|Synthetic|0001-01-01 00:00:00||0|0001-01-01 00:00:00",ScratchDb.Scalar(DB_OD261,
				"SELECT CONCAT(EndPointUrl,'|',CustomerKey,'|',PollingSeconds,'|',Note,'|',DATE_FORMAT(DateTimeLastFailure,'%Y-%m-%d %H:%i:%s'),'|',"
				+"FailureReason,'|',SubsequentFailures,'|',DATE_FORMAT(DateTimeNextRetry,'%Y-%m-%d %H:%i:%s')) FROM apisubscription"));
			Assert.AreEqual("2026-02-01 09:00:00|80|100|5,2026-02-02 09:00:00|40|-1|8",ScratchDb.Scalar(DB_OD261,
				"SELECT GROUP_CONCAT(CONCAT(DATE_FORMAT(DateTEntry,'%Y-%m-%d %H:%i:%s'),'|',InsPayAmt,'|',AllowedOverride,'|',ClaimNum) ORDER BY InsBlueBookNum) "
				+"FROM insbluebook"));
			//The ALTER fills the new timestamp of every existing row with its own time (INFERRED: the documents do not say what Open Dental writes).
			Assert.AreEqual("1",ScratchDb.Scalar(DB_OD261,"SELECT COUNT(DISTINCT DateTStamp) FROM insbluebook"));
			DateTime dateTStamp=DateTime.Parse(ScratchDb.Scalar(DB_OD261,"SELECT DATE_FORMAT(MIN(DateTStamp),'%Y-%m-%d %H:%i:%s') FROM insbluebook"));
			Assert.IsTrue(dateTStamp>=_dateTimeServerBefore && dateTStamp<=_dateTimeServerAfter,dateTStamp+" is not the time of the step.");
			foreach(string table in new[] { "glidewellacct","glidewellmsg","labcaseproduct" }) {
				Assert.AreEqual("0",ScratchDb.Scalar(DB_OD261,"SELECT COUNT(*) FROM `"+table+"`"),table);
			}
			HashSet<string> setChanged=new HashSet<string>(new[] { "apisubscription","insbluebook","provider","preference" },StringComparer.OrdinalIgnoreCase);
			List<string> listChanged=_dictChecksumsBefore.Keys.Where(x => !setChanged.Contains(x) && _dictChecksumsAfter[x]!=_dictChecksumsBefore[x]).ToList();
			Assert.AreEqual(0,listChanged.Count,"Changed outside 26.2: "+string.Join(", ",listChanged));
			Assert.AreEqual(_dictChecksumsBefore.Count+3,_dictChecksumsAfter.Count,"Three new tables.");
		}

		///<summary>A table that already has some of the 26.2 columns gets only the missing ones and the missing keys, and its values stay.</summary>
		[TestMethod]
		public void Part_TablesWithSomeColumnsAreFinished() {
			SchemaSnapshot snapshot=SchemaSnapshot.FromDatabase(DB_PART);
			SchemaSnapshot reference=SchemaVerifier.GetReference26_2_0();
			foreach(string table in new[] { "provider","apisubscription","insbluebook","glidewellacct","glidewellmsg","labcaseproduct" }) {
				SchemaSnapshot snapshotExpected=new SchemaSnapshot();
				snapshotExpected.AddTable(reference.GetTable(table));
				SchemaSnapshot snapshotActual=new SchemaSnapshot();
				snapshotActual.AddTable(snapshot.GetTable(table));
				SchemaVerifyResult result=SchemaVerifier.Compare(snapshotExpected,snapshotActual);
				Assert.AreEqual(0,result.ListDifferences.Count,table+": "+result.GetReport());
			}
			Assert.AreEqual("7|0",ScratchDb.Scalar(DB_PART,"SELECT CONCAT(ClaimFormNum,'|',GlidewellAcctNum) FROM provider"),"The existing value stays.");
			string changeProvider=_resultPart.ListChanges.Single(x => x.Contains("ALTER TABLE `provider`"));
			Assert.IsFalse(changeProvider.Contains("ADD COLUMN `ClaimFormNum`"),changeProvider);
			StringAssert.Contains(changeProvider,"ADD INDEX `ClaimFormNum`");
			string changeApi=_resultPart.ListChanges.Single(x => x.Contains("ALTER TABLE `apisubscription`"));
			Assert.IsFalse(changeApi.Contains("`DateTimeLastFailure` datetime"),changeApi);
			StringAssert.Contains(changeApi,"ADD COLUMN `SubsequentFailures`");
		}

		///<summary>A database Open Dental already brought to 26.2: the step finds nothing to do, and the 26.2 values are kept.</summary>
		[TestMethod]
		public void Od262_StepChangesNothing() {
			Assert.AreEqual(0,_resultOd262.ListChanges.Count,string.Join("\r\n",_resultOd262.ListChanges));
			Assert.AreEqual(0,_resultSchemaOd262.ListDifferences.Count,_resultSchemaOd262.GetReport());
			CollectionAssert.AreEquivalent(_dictChecksumsOd262Before.Where(x => x.Key!="preference").ToList(),
				_dictChecksumsOd262After.Where(x => x.Key!="preference").ToList());
			Assert.AreEqual("12|1|2026-03-04 05:06:07",ScratchDb.Scalar(DB_OD262,
				"SELECT CONCAT(ClaimFormNum,'|',GlidewellAcctNum,'|',DATE_FORMAT(DateTStamp,'%Y-%m-%d %H:%i:%s')) FROM provider"));
			Assert.AreEqual("Synthetic timeout|3",ScratchDb.Scalar(DB_OD262,"SELECT CONCAT(FailureReason,'|',SubsequentFailures) FROM apisubscription"));
			Assert.AreEqual("2026-03-02 11:00:00",ScratchDb.Scalar(DB_OD262,"SELECT DATE_FORMAT(DateTStamp,'%Y-%m-%d %H:%i:%s') FROM insbluebook"));
			Assert.AreEqual("1",ScratchDb.Scalar(DB_OD262,"SELECT COUNT(*) FROM glidewellmsg WHERE OrderStatus=4 AND SentITero=1"));
		}

		///<summary>The business layer reads 26.2 rows written by Open Dental through the generated CRUD.</summary>
		[TestMethod]
		public void Od262_CrudReadsThe26_2Values() {
			ScratchDb.Connect(DB_OD262);
			Provider provider=OpenPmsDentalBusiness.Crud.ProviderCrud.SelectOne(1);
			Assert.AreEqual(12,provider.ClaimFormNum);
			Assert.AreEqual(1,provider.GlidewellAcctNum);
			ApiSubscription apiSubscription=OpenPmsDentalBusiness.Crud.ApiSubscriptionCrud.SelectOne(1);
			Assert.AreEqual(new DateTime(2026,3,1,10,0,0),apiSubscription.DateTimeLastFailure);
			Assert.AreEqual("Synthetic timeout",apiSubscription.FailureReason);
			Assert.AreEqual(3,apiSubscription.SubsequentFailures);
			Assert.AreEqual(new DateTime(2026,3,1,10,30,0),apiSubscription.DateTimeNextRetry);
			Assert.AreEqual(new DateTime(2026,3,2,11,0,0),OpenPmsDentalBusiness.Crud.InsBlueBookCrud.SelectOne(1).DateTStamp);
			GlidewellAcct glidewellAcct=OpenPmsDentalBusiness.Crud.GlidewellAcctCrud.SelectOne(1);
			Assert.AreEqual("00-0000001",glidewellAcct.AccountNumber);
			Assert.AreEqual(new DateTime(2026,4,1),glidewellAcct.DateTimeExpires);
			GlidewellMsg glidewellMsg=OpenPmsDentalBusiness.Crud.GlidewellMsgCrud.SelectOne(1);
			Assert.AreEqual(EnumGlidewellOrderStatus.Shipped,glidewellMsg.OrderStatus);
			Assert.AreEqual("{\"OrderId\":1}",glidewellMsg.RawJsonRequest);
			Assert.IsTrue(glidewellMsg.SentITero);
			Assert.IsFalse(glidewellMsg.SentMedit);
			LabCaseProduct labCaseProduct=OpenPmsDentalBusiness.Crud.LabCaseProductCrud.SelectOne(1);
			Assert.AreEqual(3,(int)labCaseProduct.GlidewellProduct,"A product number this program has no name for loads as it is.");
			Assert.AreEqual("8-9",labCaseProduct.ToothRange);
			//Written back unchanged.
			OpenPmsDentalBusiness.Crud.LabCaseProductCrud.Update(labCaseProduct);
			Assert.AreEqual("3",ScratchDb.Scalar(DB_OD262,"SELECT GlidewellProduct FROM labcaseproduct"));
		}

		///<summary>The full chain: an Open Dental 25.3.48 office copy adopted with every fork step matches the 26.2.0 reference exactly (this program's
		///own tables aside) and is marked OdSchemaCompat 26.2.0.</summary>
		[TestMethod]
		public void FullChain_Od253AdoptedMatchesThe26_2ReferenceExactly() {
			Assert.AreEqual(new Version(25,3,48,0),_adoptionResult.VersionSource);
			Assert.AreEqual("26.2.0",_forkDbStateAdopted.OdSchemaCompat);
			ScratchDb.Connect(DB_OD253);
			Assert.IsTrue(ForkMigrationRunner.GetAppliedLevels().Contains(2026110701),"Step 2026110701 ran.");
			Assert.AreEqual(0,ForkMigrationRunner.GetPendingSteps().Count);
			Assert.AreEqual(ForkMigrationRunner.GetSteps().Max(x => x.Level).ToString(),_forkDbStateAdopted.ForkSchemaLevel);
			Assert.IsTrue(_adoptionResult.ListLog.Any(x => x.Contains("26.2.0")),string.Join("\r\n",_adoptionResult.ListLog));
			SchemaVerifyResult result=SchemaVerifier.VerifyDatabase(DB_OD253);
			Assert.IsTrue(result.IsMatch,result.GetReport());
			Assert.IsTrue(result.ListDifferences.All(x => x.Kind==SchemaDifferenceKind.ExtraTable && ForkTables.IsForkTable(x.Table)),result.GetReport());
			Assert.AreEqual(0,ForkTables.VerifyDatabase(DB_OD253).ListDifferences.Count);
			Assert.AreEqual("0|0",ScratchDb.Scalar(DB_OD253,"SELECT CONCAT(ClaimFormNum,'|',GlidewellAcctNum) FROM provider"));
		}

		///<summary>The full chain against Open Dental's published 26.2 document itself: the adopted database has the document's 430 tables, each with
		///the published columns in the published order and of the published types. Only this program's own tables are extra.</summary>
		[TestMethod]
		public void FullChain_Od253AdoptedMatchesThePublished26_2Document() {
			XDocument doc=OdDocs.Load(OdDocs.FULL_26_2);
			SchemaSnapshot snapshot=SchemaSnapshot.FromDatabase(DB_OD253);
			HashSet<string> setForkTables=new HashSet<string>(ForkTables.GetTableNames(),StringComparer.OrdinalIgnoreCase);
			List<string> listProblems=OdDocs.CompareWithFullDocument(doc,snapshot,setForkTables);
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			Assert.AreEqual(430+setForkTables.Count,snapshot.Tables.Count);
		}
	}
}
