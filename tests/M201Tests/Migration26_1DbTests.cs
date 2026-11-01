// The Open Dental 25.4 and 26.1 steps on scratch databases with synthetic rows:
// - an office-shaped Open Dental 25.3.48 database (the 25.3.48 reference plus the office customization) is adopted with the real runner: every M1
//   and M2 step runs, the schema ends exactly at the 26.1.70 reference, OdSchemaCompat says 26.1.70, the data migrations of the new columns run
//   once and every other table is byte-identical;
// - a database already at 26.1.70 (as Open Dental would leave it) is not changed at all.
// Skipped (inconclusive) when the local MariaDB does not answer. No office data is used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace M201Tests {
	[TestClass]
	public partial class Migration26_1DbTests {
		private static readonly string DB_OD253=ScratchDb.Name("od253");
		private static readonly string DB_OD261=ScratchDb.Name("od261");
		private static string _skipReason;
		private static Dictionary<string,string> _dictChecksumsBefore;
		private static Dictionary<string,string> _dictRowCountsBefore;
		private static Dictionary<string,string> _dictChecksumsAfter;
		private static Dictionary<string,string> _dictRowCountsAfter;
		private static AdoptionResult _adoptionResult;
		private static ForkDbState _forkDbStateAfter;
		private static Dictionary<string,string> _dictChecksumsOd261Before;
		private static ForkMigrationResult _resultOd261;
		private static SchemaVerifyResult _resultSchemaOd261;
		private static Dictionary<string,string> _dictChecksumsOd261After;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropStale();
			Security.CurUser=Security.CurUser??new Userod { UserNum=1,UserName="Synthetic" };
			//The office copy at 25.3.48, adopted as the adoption tool adopts it.
			ScratchDb.CreateFromSnapshot(DB_OD253,SchemaVerifier.GetReference25_3_48());
			ScratchDb.Exec(DB_OD253,"ALTER TABLE patient ADD COLUMN `_portal_active_email` varchar(100) GENERATED ALWAYS AS (case when `PatStatus` = 0 "
				+"and `Email` <> '' then `Email` else NULL end) VIRTUAL, ADD UNIQUE KEY `portal_active_email_uniq` (`_portal_active_email`)");
			SeedSynthetic25_3Data();
			_dictChecksumsBefore=ScratchDb.ChecksumAllTables(DB_OD253);
			_dictRowCountsBefore=RowCounts(DB_OD253);
			ScratchDb.Connect(DB_OD253);
			_adoptionResult=Adoption.Run();
			_forkDbStateAfter=ForkVersions.ReadState();
			_dictChecksumsAfter=ScratchDb.ChecksumAllTables(DB_OD253);
			_dictRowCountsAfter=RowCounts(DB_OD253);
			//A database Open Dental already converted to 26.1.70.
			ScratchDb.CreateFromSnapshot(DB_OD261,SchemaVerifier.GetReference26_1_70());
			SeedSynthetic26_1Data();
			_dictChecksumsOd261Before=ScratchDb.ChecksumAllTables(DB_OD261);
			ScratchDb.Connect(DB_OD261);
			SchemaSnapshot snapshotOd261Before=SchemaSnapshot.FromDatabase(DB_OD261);
			_resultOd261=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true });
			_resultSchemaOd261=SchemaVerifier.Compare(snapshotOd261Before,SchemaSnapshot.FromDatabase(DB_OD261));
			_dictChecksumsOd261After=ScratchDb.ChecksumAllTables(DB_OD261);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropQuietly(DB_OD253);
			ScratchDb.DropQuietly(DB_OD261);
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_OD253);
		}

		///<summary>Rows an office could have at 25.3.48 in the tables 25.4 and 26.1 change, plus a few in tables they do not change. All made up.
		///Columns left out get the server's implicit default (blank sql_mode), as Open Dental's own inserts rely on.</summary>
		private static void SeedSynthetic25_3Data() {
			ScratchDb.Exec(DB_OD253,
				"INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','25.3.48.0'),('ProgramVersion','25.3.48.0'),"
					+"('CorruptedDatabase','0'),('UpdateInProgressOnComputerName',''),('RandomPrimaryKeys','0'),('DxIcdVersion','9')",
				"INSERT INTO patient (LName,FName,Email,PatStatus) VALUES ('Synthetic','Parent','family@example.invalid',0)",
				"INSERT INTO evaluation (InstructNum,StudentNum,SchoolCourseNum,EvalTitle,DateEval,GradingScaleNum,OverallGradeShowing,OverallGradeNumber,"
					+"Notes) VALUES (1,2,3,'Synthetic eval A','2025-02-03',1,'B',3,''),(1,4,3,'Synthetic eval B','2025-02-04',1,'A',4,'')",
				"INSERT INTO evaluationdef (SchoolCourseNum,EvalTitle,GradingScaleNum) VALUES (3,'Synthetic template',1)",
				"INSERT INTO schoolcoursedef (CourseID,Descript) VALUES ('SYN101','Synthetic course')",
				"INSERT INTO claimpayment (CheckDate,CheckAmt,CheckNum,BankBranch,Note,ClinicNum,DepositNum,CarrierName,DateIssued,IsPartial,PayType,"
					+"SecUserNumEntry,SecDateEntry,PayGroup,SecDateTEdit) VALUES "
					+"('2025-04-01',10,'1001','',NULL,0,0,'Synthetic Carrier','2025-03-30',0,0,1,'2025-04-01',0,'2025-04-01 12:00:00'),"
					+"('2025-04-02',20,'1002','','Synthetic check note',0,0,'Synthetic Carrier','2025-03-31',0,0,1,'2025-04-02',0,'2025-04-02 12:00:00'),"
					+"('2025-04-03',30,'1003','',REPEAT('n',255),0,0,'Synthetic Carrier','2025-04-01',0,0,1,'2025-04-03',0,'2025-04-03 12:00:00')",
				"INSERT INTO insverify (DateLastVerified,UserNum,VerifyType,FKey,DefNum,Note,DateLastAssigned,DateTimeEntry,HoursAvailableForVerification,"
					+"SecurityHash) VALUES ('2025-05-01',1,1,11,0,'','2025-06-01','2025-05-01 08:00:00',0,'')",
				"INSERT INTO insverifyhist (InsVerifyNum,DateLastVerified,UserNum,VerifyType,FKey,DefNum,Note,DateLastAssigned,DateTimeEntry,"
					+"HoursAvailableForVerification,VerifyUserNum,SecurityHash) VALUES (1,'2025-05-01',1,1,11,0,'','2025-06-02','2025-05-01 08:00:00',0,1,'')",
				"INSERT INTO referral (LName,FName,Telephone,Specialty,PatNum,Slip,IsDoctor,IsTrustedDirect,IsPreferred,BusinessName,DisplayNote) VALUES "
					+"('Synthetic','Referrer','5035550100',0,0,0,1,0,0,'','')",
				"INSERT INTO branding (BrandingType,ClinicNum,ValueString,DateTimeUpdated) VALUES (2,0,'{\"primary\":\"#123456\"}','2025-01-01 00:00:00')",
				"INSERT INTO procnote (PatNum,ProcNum,EntryDateTime,UserNum,Note,SigIsTopaz,Signature) VALUES "
					+"(1,1,'2025-01-02 09:00:00',1,'Synthetic note','0','sig-one')",
				"INSERT INTO carrier (CarrierName,Address,Address2,City,State,Zip,Phone,ElectID,CDAnetVersion,TIN) VALUES "
					+"('Synthetic Carrier','1 Main St','','Springfield','OR','97000','5035550101','00000','','')",
				"INSERT INTO codegroup (GroupName,ProcCodes,ItemOrder,CodeGroupFixed,IsHidden,ShowInAgeLimit,ShowInFrequency,ShowInOther) VALUES "
					+"('Synthetic group','D0001',0,0,0,0,1,0)",
				"INSERT INTO definition (Category,ItemOrder,ItemName,ItemValue,ItemColor,IsHidden) VALUES (1,0,'Synthetic adjustment','',0,0)",
				"INSERT INTO employee (LName,FName,MiddleI,IsHidden,ClockStatus,PhoneExt,PayrollID,WirelessPhone,EmailWork,EmailPersonal,IsFurloughed,"
					+"IsWorkingHome,ReportsTo) VALUES ('Synthetic','Employee','',0,'',0,'','','','',0,0,0)",
				"INSERT INTO dunning (DunMessage,BillingType,AgeAccount,InsIsPending,MessageBold,EmailSubject,EmailBody,DaysInAdvance,ClinicNum,"
					+"IsSuperFamily) VALUES ('Synthetic dunning',0,30,0,'','','',0,0,0)",
				"INSERT INTO eform (FormType,PatNum,DateTimeShown,Description,DateTEdited,MaxWidth,EFormDefNum,Status,RevID,ShowLabelsBold,"
					+"SpaceBelowEachField,SpaceToRightEachField,SaveImageCategory) VALUES (0,1,'2025-01-03 10:00:00','Synthetic form',"
					+"'2025-01-03 10:05:00',450,0,2,1,0,-1,-1,0)",
				"INSERT INTO eformfield (EFormNum,PatNum,FieldType,DbLink,ValueLabel,ValueString,ItemOrder) VALUES (1,1,0,'','Synthetic label','',0)",
				"INSERT INTO sheet (SheetType,PatNum,DateTimeSheet,FontSize,FontName,Width,Height,IsLandscape,InternalNote,Description) VALUES "
					+"(0,1,'2025-01-04 11:00:00',10,'Arial',850,1100,0,'','Synthetic sheet')",
				"INSERT INTO statement (PatNum,DateSent,DateRangeFrom,DateRangeTo,Note,NoteBold,Mode_,IsSent,StatementType,ShowTransSinceBalZero) VALUES "
					+"(1,'2025-03-01','2025-01-01','2025-03-01','','',0,1,'NotSet',1)",
				"INSERT INTO smstomobile (PatNum,GuidMessage,MsgText,DateTimeSent) VALUES (1,'00000000-synthetic','Synthetic text','2025-01-05 12:00:00')",
				"INSERT INTO payterminal (Name,ClinicNum,TerminalID) VALUES ('Front desk',0,'SYN-0001')",
				"INSERT INTO procedurelog (PatNum,ProcDate,ProcFee,ProcStatus,CodeNum,DiscountPlanAmt) VALUES (1,'2025-01-02',100,2,1,0)",
				"INSERT INTO claimproc (ProcNum,ClaimNum,PatNum,ProvNum,FeeBilled,InsPayEst,Status,PlanNum,ProcDate,SecurityHash) VALUES "
					+"(1,0,1,1,100,80,6,1,'2025-01-02','')",
				"INSERT INTO chatuserod (UserNum,UserStatus,Photo,PhotoCrop) VALUES (1,0,'','')",
				"INSERT INTO registrationkey (PatNum,RegKey,Note,BackupPassCode) VALUES (1,'','','')",
				"INSERT INTO inseditlog (FKey,LogType,FieldName,OldValue,NewValue,UserNum,ParentKey,Description) VALUES "
					+"(1,1,'CarrierName','Old Carrier','Synthetic Carrier',1,0,'Synthetic')",
				"INSERT INTO inseditpatlog (FKey,LogType,FieldName,OldValue,NewValue,UserNum,ParentKey,Description) VALUES "
					+"(1,1,'SubscNote','','Synthetic note',1,0,'Synthetic')",
				//Tables 25.4 and 26.1 do not change.
				"INSERT INTO guardian (PatNumChild,PatNumGuardian,Relationship,IsGuardian) VALUES (11,21,6,1),(12,22,23,0)",
				"INSERT INTO task (TaskListNum,DateTask,Descript,UserNum,Category) VALUES (1,'0001-01-01','Synthetic task',1,5)"
			);
		}

		///<summary>Rows in 26.1 form, with values in every new column, that a second conversion would damage. All made up.</summary>
		private static void SeedSynthetic26_1Data() {
			ScratchDb.Exec(DB_OD261,
				"INSERT INTO preference (PrefName,ValueString) VALUES ('DataBaseVersion','26.1.70.0'),('ProgramVersion','26.1.70.0'),('RandomPrimaryKeys','0')",
				"INSERT INTO evaluation (InstructNum,StudentNum,SchoolCourseNum,EvalTitle,DateEval,GradingScaleNum,OverallGradeShowing,OverallGradeNumber,"
					+"Notes,GradeOverride) VALUES (1,2,3,'Synthetic eval','2026-02-03',1,'B',3,'',4.5)",
				"INSERT INTO claimpayment (CheckDate,CheckAmt,CheckNum,BankBranch,Note,ClinicNum,DepositNum,CarrierName,DateIssued,IsPartial,PayType,"
					+"SecUserNumEntry,SecDateEntry,PayGroup) VALUES ('2026-04-01',10,'2001','',REPEAT('t',600),0,0,'Synthetic','2026-03-30',0,0,1,"
					+"'2026-04-01',0)",
				"INSERT INTO insverify (DateLastVerified,UserNum,VerifyType,FKey,DefNum,Note,DateLastAssigned,DateTimeEntry,HoursAvailableForVerification,"
					+"SecurityHash) VALUES ('2026-05-01',1,1,11,0,'','2026-06-01 13:45:00','2026-05-01 08:00:00',0,'')",
				"INSERT INTO referral (LName,FName,Telephone,Specialty,PatNum,Slip,IsDoctor,IsTrustedDirect,IsPreferred,BusinessName,DisplayNote) VALUES "
					+"('Synthetic','Referrer','+1 (503) 555-0100 x12',0,0,0,1,0,0,'','')",
				"INSERT INTO procnote (PatNum,ProcNum,EntryDateTime,UserNum,Note,SigIsTopaz,Signature,UserNum2,Signature2,SigIsTopaz2) VALUES "
					+"(1,1,'2026-01-02 09:00:00',1,'Synthetic note','0','sig-one',2,'sig-two',1)",
				"INSERT INTO carrier (CarrierName,Address,Address2,City,State,Zip,Phone,ElectID,CDAnetVersion,TIN,PaySuiteTransSup,PreAuthCodes) VALUES "
					+"('Synthetic Carrier','1 Main St','','Springfield','OR','97000','5035550101','00000','','',3,'D2740,D2950-D2954')",
				"INSERT INTO definition (Category,ItemOrder,ItemName,ItemValue,ItemColor,IsHidden,Supp) VALUES (56,0,'Synthetic title','',0,0,'x')",
				"INSERT INTO employee (LName,FName,MiddleI,IsHidden,ClockStatus,PhoneExt,PayrollID,WirelessPhone,EmailWork,EmailPersonal,IsFurloughed,"
					+"IsWorkingHome,ReportsTo,Title) VALUES ('Synthetic','Employee','',0,'',0,'','','','',0,0,0,1)",
				"INSERT INTO deletedobj (DateTEntry,ObjType,ObjSerialized) VALUES ('2026-01-06 07:00:00',1,'258|2026-01-06|08:00:00')",
				"INSERT INTO paysuitepayment (PaymentId,PaymentAmount,PaymentDate,PaymentStatus,ReconciliationStatus,ClaimPaymentNum) VALUES "
					+"('SYN-1',25,'2026-02-01','P',4,1)"
			);
		}

		///<summary>SELECT COUNT(*) of every table, keyed by table name.</summary>
		private static Dictionary<string,string> RowCounts(string name) {
			Dictionary<string,string> dict=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(string table in ScratchDb.ChecksumAllTables(name).Keys) {
				dict[table]=ScratchDb.Scalar(name,"SELECT COUNT(*) FROM `"+table+"`");
			}
			return dict;
		}

		///<summary>Tables the 25.4 and 26.1 steps change or add: every table where the two references differ.</summary>
		public static HashSet<string> GetTablesChangedBy26_1() {
			SchemaVerifyResult result=SchemaVerifier.Compare(SchemaVerifier.GetReference26_1_70(),SchemaVerifier.GetReference25_3_48());
			return new HashSet<string>(result.ListDifferences.Select(x => x.Table),StringComparer.OrdinalIgnoreCase);
		}

		[TestMethod]
		public void Od253_AdoptionRunsEveryM1AndM2StepAndMarks26_1_70() {
			Assert.AreEqual(new Version(25,3,48,0),_adoptionResult.VersionSource);
			Assert.IsTrue(_adoptionResult.WasPortalEmailColumnDropped);
			Assert.AreEqual("26.1.70",_forkDbStateAfter.OdSchemaCompat);
			Assert.AreEqual(ForkMigrationRunner.GetSteps().Max(x => x.Level).ToString(),_forkDbStateAfter.ForkSchemaLevel);
			Assert.AreEqual(0,ForkMigrationRunner.GetPendingSteps().Count);
			HashSet<long> setApplied=ForkMigrationRunner.GetAppliedLevels();
			foreach(long level in new long[] { 2026101901,2026101902,2026101903,2026101904,2026101905,2026101906,2026101907,2026102401,2026102402,
				2026102403 })
			{
				Assert.IsTrue(setApplied.Contains(level),"Step "+level+" ran.");
			}
			Assert.IsTrue(_forkDbStateAfter.IsAdoptionRecordFinished);
			StringAssert.StartsWith(_forkDbStateAfter.ForkAdoptedFrom,"source=25.3.48.0;");
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(_forkDbStateAfter);
			Assert.AreEqual(ForkGuardAction.ProceedFork,forkGuardResult.Action,forkGuardResult.Message);
			Assert.IsFalse(forkGuardResult.HasPendingMigrations);
			Assert.IsTrue(_adoptionResult.ListLog.Any(x => x.Contains("26.1.70")),string.Join("\r\n",_adoptionResult.ListLog));
		}

		[TestMethod]
		public void Od253_MatchesThe26_1ReferenceExactly() {
			SchemaVerifyResult result=SchemaVerifier.VerifyDatabase(DB_OD253);
			Assert.IsTrue(result.IsMatch,result.GetReport());
			Assert.AreEqual(0,result.ListDifferences.Count,"Adoption dropped the office customization, so nothing is left to allow: "+result.GetReport());
		}

		[TestMethod]
		public void Od253_MatchesTheLoadedOd261SchemaDatabase() {
			if(!ScratchDb.DatabaseExists(ScratchDb.DbNameOd261Schema)) {
				Assert.Inconclusive("The reference database "+ScratchDb.DbNameOd261Schema+" is not loaded (tools/New-OdReferenceDatabase.ps1).");
			}
			SchemaVerifyResult result=SchemaVerifier.Compare(SchemaSnapshot.FromDatabase(ScratchDb.DbNameOd261Schema),SchemaSnapshot.FromDatabase(DB_OD253));
			Assert.AreEqual(0,result.ListDifferences.Count,result.GetReport());
		}

		///<summary>The loaded od_261_schema database is the reference as the server prints it: loading the file and reading it back changes nothing,
		///so the reference is written exactly the way SHOW CREATE TABLE prints it.</summary>
		[TestMethod]
		public void Od261SchemaDatabase_EqualsTheReferenceFile() {
			if(!ScratchDb.DatabaseExists(ScratchDb.DbNameOd261Schema)) {
				Assert.Inconclusive("The reference database "+ScratchDb.DbNameOd261Schema+" is not loaded (tools/New-OdReferenceDatabase.ps1).");
			}
			SchemaSnapshot snapshotLoaded=SchemaSnapshot.FromDatabase(ScratchDb.DbNameOd261Schema);
			SchemaVerifyResult result=SchemaVerifier.Compare(SchemaVerifier.GetReference26_1_70(),snapshotLoaded);
			Assert.AreEqual(0,result.ListDifferences.Count,result.GetReport());
			Assert.AreEqual("0",ScratchDb.Scalar(ScratchDb.DbNameOd261Schema,
				"SELECT COALESCE(SUM(TABLE_ROWS),0) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+ScratchDb.DbNameOd261Schema+"'"),"No rows.");
		}

		[TestMethod]
		public void Od253_DataMigrationsOfTheNewColumns() {
			//evaluation.GradeOverride: every existing row is "no override".
			Assert.AreEqual("-1,-1",DataCore.GetScalar("SELECT GROUP_CONCAT(GradeOverride ORDER BY EvaluationNum) FROM evaluation"));
			//claimpayment.Note: NULL became empty, the rest is kept, the 255-character note too.
			DataTable table=DataCore.GetTable("SELECT Note FROM claimpayment ORDER BY ClaimPaymentNum");
			Assert.AreEqual("",table.Rows[0]["Note"].ToString());
			Assert.IsFalse(table.Rows[0].IsNull("Note"));
			Assert.AreEqual("Synthetic check note",table.Rows[1]["Note"].ToString());
			Assert.AreEqual(new string('n',255),table.Rows[2]["Note"].ToString());
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM claimpayment WHERE Note IS NULL"));
			//Making a NULL note empty is not an edit of the payment: its ON UPDATE audit timestamp stays (the adoption rehearsal compares them all).
			Assert.AreEqual("2025-04-01 12:00:00|2025-04-02 12:00:00|2025-04-03 12:00:00",ScratchDb.Scalar(DB_OD253,"SELECT GROUP_CONCAT(DATE_FORMAT("
				+"SecDateTEdit,'%Y-%m-%d %H:%i:%s') ORDER BY ClaimPaymentNum SEPARATOR '|') FROM claimpayment"));
			//Dates became datetimes at midnight; the key stays.
			Assert.AreEqual("2025-06-01 00:00:00",DataCore.GetScalar("SELECT DATE_FORMAT(DateLastAssigned,'%Y-%m-%d %H:%i:%s') FROM insverify"));
			Assert.AreEqual("2025-06-02 00:00:00",DataCore.GetScalar("SELECT DATE_FORMAT(DateLastAssigned,'%Y-%m-%d %H:%i:%s') FROM insverifyhist"));
			Assert.IsNotNull(SchemaTable.FromDatabase(DB_OD253,"insverify").GetIndex("DateLastAssigned"));
			Assert.AreEqual(new DateTime(2025,6,1),OpenDentBusiness.Crud.InsVerifyCrud.SelectOne(1).DateLastAssigned);
			//Kept as they were.
			Assert.AreEqual("5035550100",DataCore.GetScalar("SELECT Telephone FROM referral"));
			Assert.AreEqual("{\"primary\":\"#123456\"}",DataCore.GetScalar("SELECT ValueString FROM branding"));
			Assert.AreEqual("Synthetic note|sig-one|1",DataCore.GetScalar("SELECT CONCAT(Note,'|',Signature,'|',UserNum) FROM procnote"));
			//The new columns of existing rows hold the "nothing" value.
			Assert.AreEqual("0|NULL|0",DataCore.GetScalar("SELECT CONCAT(UserNum2,'|',COALESCE(Signature2,'NULL'),'|',SigIsTopaz2) FROM procnote"));
			Assert.AreEqual("0|",DataCore.GetScalar("SELECT CONCAT(PaySuiteTransSup,'|',PreAuthCodes) FROM carrier"));
			Assert.AreEqual("0||0",DataCore.GetScalar("SELECT CONCAT(ShowInHistory,'|',HistProcCode,'|',IsPerioFourQuads) FROM codegroup"));
			Assert.AreEqual("",DataCore.GetScalar("SELECT Supp FROM definition"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT Title FROM employee"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT EmailType FROM dunning"));
			Assert.AreEqual("0001-01-01 00:00:00",DataCore.GetScalar("SELECT DATE_FORMAT(DateTimeSubmitted,'%Y-%m-%d %H:%i:%s') FROM eform"));
			Assert.AreEqual("0001-01-01 00:00:00",DataCore.GetScalar("SELECT DATE_FORMAT(DateTimeSubmitted,'%Y-%m-%d %H:%i:%s') FROM sheet"));
			Assert.AreEqual("0001-01-01 00:00:00",DataCore.GetScalar("SELECT DATE_FORMAT(DateTimeSig,'%Y-%m-%d %H:%i:%s') FROM eformfield"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT IsServiceDateView FROM statement"));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ShowTransSinceBalZero FROM statement"),"The 25.2 back-fill did not run again.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT SecUserNumEntry FROM smstomobile"));
			Assert.AreEqual("",DataCore.GetScalar("SELECT CCIntegration FROM payterminal"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT NoBillIns FROM procedurelog"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT Etrans835AttachNum FROM claimproc"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT PlayNotificationSound FROM chatuserod"));
			Assert.AreEqual("0001-01-01 00:00:00",DataCore.GetScalar("SELECT DATE_FORMAT(DateTClinicAccess,'%Y-%m-%d %H:%i:%s') FROM registrationkey"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT GradingScaleNum FROM schoolcoursedef"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT SchoolCourseDefNum FROM evaluationdef"));
			Assert.AreEqual("|",DataCore.GetScalar("SELECT CONCAT(OldValueBig,'|',NewValueBig) FROM inseditlog"));
			Assert.AreEqual("|",DataCore.GetScalar("SELECT CONCAT(OldValueBig,'|',NewValueBig) FROM inseditpatlog"));
		}

		///<summary>Row counts are unchanged everywhere except the rows adoption itself writes, and every table 25.4 and 26.1 do not change is
		///byte-identical: no data migration of an earlier version ran again.</summary>
		[TestMethod]
		public void Od253_OtherTablesAreUntouched() {
			//Written by adoption: the fork markers and preferences, its security log entry and that entry's hash. patient lost the office
			//customization column.
			HashSet<string> setAdoption=new HashSet<string>(new[] { "preference","securitylog","securityloghash","patient" },
				StringComparer.OrdinalIgnoreCase);
			foreach(KeyValuePair<string,string> pair in _dictRowCountsBefore.Where(x => !setAdoption.Contains(x.Key))) {
				Assert.AreEqual(pair.Value,_dictRowCountsAfter[pair.Key],"Row count of "+pair.Key);
			}
			Assert.AreEqual("1",_dictRowCountsAfter["patient"]);
			HashSet<string> setTables26_1=GetTablesChangedBy26_1();
			List<string> listChanged=_dictChecksumsBefore.Keys.Where(x => !setTables26_1.Contains(x) && !setAdoption.Contains(x))
				.Where(x => _dictChecksumsAfter[x]!=_dictChecksumsBefore[x]).ToList();
			Assert.AreEqual(0,listChanged.Count,"Changed outside 25.4/26.1: "+string.Join(", ",listChanged));
			Assert.AreEqual("6,23",DataCore.GetScalar("SELECT GROUP_CONCAT(Relationship ORDER BY PatNumChild) FROM guardian"));
			//The 5 new tables exist and are empty.
			foreach(string table in new[] { "deletedobj","paysuitepayment","paysuitepaymentdetail","schoolcourseenrollee","schoolcourseinstructor" }) {
				Assert.AreEqual("0",_dictRowCountsAfter[table],table);
			}
		}

		[TestMethod]
		public void Od253_SecondRunChangesNothing() {
			Dictionary<string,string> dictBefore=ScratchDb.ChecksumAllTables(DB_OD253);
			SchemaSnapshot snapshotBefore=SchemaSnapshot.FromDatabase(DB_OD253);
			ForkMigrationResult result=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true });
			Assert.AreEqual(ForkMigrationRunner.GetSteps().Count,result.ListStepsRun.Count);
			Assert.AreEqual(0,result.ListChanges.Count,string.Join("\r\n",result.ListChanges));
			Dictionary<string,string> dictAfter=ScratchDb.ChecksumAllTables(DB_OD253);
			dictBefore.Remove("preference");
			dictAfter.Remove("preference");
			CollectionAssert.AreEquivalent(dictBefore.ToList(),dictAfter.ToList());
			Assert.AreEqual(0,SchemaVerifier.Compare(snapshotBefore,SchemaSnapshot.FromDatabase(DB_OD253)).ListDifferences.Count);
		}

		///<summary>A database Open Dental already brought to 26.1.70: every step finds nothing to do, and the 26.1 values (a grade override, a long
		///claim payment note, an assignment time, a long phone number, a second signature) are kept.</summary>
		[TestMethod]
		public void Od261_StepsChangeNothing() {
			Assert.AreEqual(ForkMigrationRunner.GetSteps().Count,_resultOd261.ListStepsRun.Count);
			//Seed26_1 (package M2-02) inserts the fork's own 25.4 and 26.1 preference rows on every database that lacks them: Open Dental publishes
			//no preference names, so an Open Dental 26.1 database has none of them. They live in the preference table, which is left out below.
			string prefixSeed26_1=new OpenDentBusiness.ForkMigrations.Seeds26_1.Seed26_1Step().Level+": Inserted preference ";
			List<string> listChanges=_resultOd261.ListChanges.Where(x => !x.StartsWith(prefixSeed26_1)).ToList();
			Assert.AreEqual(0,listChanges.Count,string.Join("\r\n",listChanges));
			Assert.AreEqual(0,_resultSchemaOd261.ListDifferences.Count,_resultSchemaOd261.GetReport());
			Dictionary<string,string> dictBefore=_dictChecksumsOd261Before.Where(x => x.Key!="preference").ToDictionary(x => x.Key,x => x.Value);
			Dictionary<string,string> dictAfter=_dictChecksumsOd261After.Where(x => x.Key!="preference").ToDictionary(x => x.Key,x => x.Value);
			CollectionAssert.AreEquivalent(dictBefore.ToList(),dictAfter.ToList());
			Assert.AreEqual("4.5",ScratchDb.Scalar(DB_OD261,"SELECT GradeOverride FROM evaluation"));
			Assert.AreEqual("600",ScratchDb.Scalar(DB_OD261,"SELECT CHAR_LENGTH(Note) FROM claimpayment"));
			Assert.AreEqual("2026-06-01 13:45:00",ScratchDb.Scalar(DB_OD261,"SELECT DATE_FORMAT(DateLastAssigned,'%Y-%m-%d %H:%i:%s') FROM insverify"));
			Assert.AreEqual("+1 (503) 555-0100 x12",ScratchDb.Scalar(DB_OD261,"SELECT Telephone FROM referral"));
			Assert.AreEqual("2|sig-two|1",ScratchDb.Scalar(DB_OD261,"SELECT CONCAT(UserNum2,'|',Signature2,'|',SigIsTopaz2) FROM procnote"));
		}

		///<summary>The business layer reads 26.1 rows written by Open Dental: the new values come back through the regenerated CRUD.</summary>
		[TestMethod]
		public void Od261_CrudReadsThe26_1Values() {
			ScratchDb.Connect(DB_OD261);
			Carrier carrier=OpenDentBusiness.Crud.CarrierCrud.SelectOne(1);
			Assert.AreEqual(EnumPaySuiteTransTypes.Both,carrier.PaySuiteTransSup);
			Assert.AreEqual("D2740,D2950-D2954",carrier.PreAuthCodes);
			Def def=OpenDentBusiness.Crud.DefCrud.SelectOne(1);
			Assert.AreEqual(DefCat.EmployeeTitles,def.Category);
			Assert.AreEqual("x",def.Supp);
			Assert.AreEqual(1,OpenDentBusiness.Crud.EmployeeCrud.SelectOne(1).Title);
			Assert.AreEqual(4.5f,OpenDentBusiness.Crud.EvaluationCrud.SelectOne(1).GradeOverride);
			Assert.AreEqual(new DateTime(2026,6,1,13,45,0),OpenDentBusiness.Crud.InsVerifyCrud.SelectOne(1).DateLastAssigned,"The time of day is kept.");
			ProcNote procNote=OpenDentBusiness.Crud.ProcNoteCrud.SelectOne(1);
			Assert.AreEqual(2,procNote.UserNum2);
			Assert.AreEqual("sig-two",procNote.Signature2);
			Assert.IsTrue(procNote.SigIsTopaz2);
			DeletedObj deletedObj=OpenDentBusiness.Crud.DeletedObjCrud.SelectOne(1);
			Assert.AreEqual(EnumDeletedObjType.Schedules,deletedObj.ObjType);
			Assert.AreEqual(new DateTime(2026,1,6,7,0,0),deletedObj.DateTEntry);
			PaySuitePayment paySuitePayment=OpenDentBusiness.Crud.PaySuitePaymentCrud.SelectOne(1);
			Assert.AreEqual(EnumReconciliationStatus.NoDiscrepanciesFound,paySuitePayment.ReconciliationStatus);
			Assert.AreEqual(new DateTime(2026,2,1),paySuitePayment.PaymentDate);
			Assert.AreEqual(600,OpenDentBusiness.Crud.ClaimPaymentCrud.SelectOne(1).Note.Length);
		}
	}
}
