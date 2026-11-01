// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using SyntheticData.Generation;
using SyntheticData.Infrastructure;
using SyntheticData.Validation;

namespace SyntheticData.Tests {
	///<summary>End-to-end tests on scratch databases (synth_test_*) on the local server. They generate the small preset twice with the
	///same seed and once with another seed, then check schema, coverage, determinism, consistency and a simulated adoption. Inconclusive
	///when no local server or no schema source is available.</summary>
	[TestClass]
	public class DatabaseTests {
		///<summary>Unique to this run, so test runs from other worktrees at the same time never drop or fill each other's databases.</summary>
		private static readonly string RunTag=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id
			+"_"+new Random().Next(1000,10000);
		private static readonly string DbA="synth_test_"+RunTag+"_a";
		private static readonly string DbB="synth_test_"+RunTag+"_b";
		private static readonly string DbC="synth_test_"+RunTag+"_c";
		private static readonly string DbAdopted="synth_test_"+RunTag+"_adopted";
		private static readonly string DbFill="synth_test_"+RunTag+"_fill";
		private static readonly string DbFloatA="synth_test_"+RunTag+"_floata";
		private static readonly string DbFloatB="synth_test_"+RunTag+"_floatb";
		private static readonly ServerSettings _server=new ServerSettings();
		private static string _skipReason;
		private static ValidationReport _reportA;
		private static ValidationReport _reportB;
		private static ValidationReport _reportC;

		[ClassInitialize]
		public static void ClassInitialize(TestContext context) {
			if(!Db.CanConnect(_server,out string error)) {
				_skipReason="No local MariaDB/MySQL server: "+error;
				return;
			}
			using(Db db=Db.Open(_server)) {
				if(!File.Exists(SchemaBuilder.DefaultSchemaSqlPath) && !db.DatabaseExists(SchemaBuilder.DefaultReferenceDatabase)) {
					_skipReason="Neither the 25.3.48 schema dump nor the od_253_schema database is available.";
					return;
				}
				_reportA=Generate(db,DbA,GenerationSettings.DefaultSeed);
				_reportB=Generate(db,DbB,GenerationSettings.DefaultSeed);
				_reportC=Generate(db,DbC,7);
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			using(Db db=Db.Open(_server)) {
				foreach(string database in new[] { DbA,DbB,DbC,DbAdopted,DbFill,DbFloatA,DbFloatB }) {
					if(db.DatabaseExists(database)) {
						db.Execute("DROP DATABASE `"+database+"`");
					}
				}
			}
		}

		private static ValidationReport Generate(Db db,string database,long seed) {
			GenerationSettings settings=new GenerationSettings { Seed=seed,Size=DataSize.Small };
			SchemaBuilder.Create(db,database,true,SchemaBuilder.DefaultSchemaSqlPath,SchemaBuilder.DefaultReferenceDatabase);
			Generator.Run(db,database,settings,x => { });
			return Validator.Run(db,database,null,null);
		}

		private static void SkipIfNeeded() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
		}

		[TestMethod]
		public void Schema_MatchesTheReference() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server)) {
				SchemaInfo schema=SchemaInfo.Load(db,DbA);
				Assert.AreEqual(SchemaBuilder.ExpectedTableCount,schema.DictTables.Count);
				Assert.IsTrue(schema.GetTable("patient").GetColumn("_portal_active_email").IsGenerated);
				if(db.DatabaseExists(SchemaBuilder.DefaultReferenceDatabase)) {
					List<string> listDiffs=SchemaInfo.Diff(SchemaInfo.Load(db,SchemaBuilder.DefaultReferenceDatabase),schema);
					Assert.AreEqual(0,listDiffs.Count,string.Join("\n",listDiffs));
				}
				//The generator never touches the table definitions: same indexes as the dump.
				long keys=db.ScalarLong("SELECT COUNT(DISTINCT TABLE_NAME,INDEX_NAME) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='"+DbA+"'");
				Assert.IsTrue(keys>422,keys.ToString());
			}
		}

		[TestMethod]
		public void SameSeed_GivesIdenticalReports_OtherSeedDiffers() {
			SkipIfNeeded();
			string jsonA=_reportA.ToJson().Replace(DbA,"X");
			string jsonB=_reportB.ToJson().Replace(DbB,"X");
			Assert.AreEqual(jsonA,jsonB,"Generation must be deterministic.");
			Assert.AreEqual(0,ReportComparer.Compare(_reportA,_reportB,null).Count);
			TableReport patientA=_reportA.Tables.First(x => x.Name=="patient");
			TableReport patientC=_reportC.Tables.First(x => x.Name=="patient");
			Assert.AreNotEqual(patientA.Columns["FName"],patientC.Columns["FName"]);
		}

		[TestMethod]
		public void ConsistencyChecks_AllPass() {
			SkipIfNeeded();
			foreach(ValidationReport report in new[] { _reportA,_reportC }) {
				List<string> listFailed=Validator.FailedChecks(report);
				Assert.AreEqual(0,listFailed.Count,string.Join("; ",listFailed));
				Assert.IsFalse(report.Totals.Any(x => x.Value.StartsWith("error")),string.Join("; ",report.Totals.Where(x => x.Value.StartsWith("error"))));
				Assert.AreEqual(report.Totals["aging.stored.bal_total"],report.Totals["aging.ledger_total"]);
				Assert.AreEqual(report.Totals["income.insurance"],report.Totals["income.insurance_checks"]);
				Assert.AreEqual(report.Totals["accounting.totals.debits"],report.Totals["accounting.totals.credits"]);
			}
		}

		[TestMethod]
		public void Coverage_IsCompleteForAnySeed() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server)) {
				foreach(string database in new[] { DbA,DbC }) {
					Coverage.Result result=Coverage.Check(db,database);
					Assert.IsTrue(result.IsComplete,database+": "+string.Join(", ",result.AllProblems));
					Assert.AreEqual(Coverage.DictWidenedOldLength.Count,result.DictMaxLengths.Count,database);
				}
			}
		}

		[TestMethod]
		public void CoreAndFeatureTables_HoldData() {
			SkipIfNeeded();
			Dictionary<string,long> dictRows=_reportA.Tables.ToDictionary(x => x.Name,x => x.Rows);
			foreach(string table in new[] { "patient","provider","operatory","schedule","scheduleop","appointment","procedurelog","procedurecode","claim",
				"claimproc","claimpayment","payment","paysplit","adjustment","payplan","payplancharge","insplan","inssub","patplan","benefit","carrier",
				"feesched","fee","feeschednote","codegroup","covcat","covspan","statement","deposit","journalentry","transaction","account","task",
				"taskhist","document","imagedraw","eform","eformfield","eformdef","eformfielddef","sheet","sheetfield","medication","medicationpat",
				"guardian","recall","commlog","histappointment","securitylog","inspending","chatmsg","schoolcourse","treatplan","procnote" })
			{
				Assert.IsTrue(dictRows[table]>0,table+" is empty");
			}
		}

		[TestMethod]
		public void Data_IsFictionalAndUsesReservedContactRanges() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server,DbA)) {
				foreach(string sql in new[] { "SELECT LName,FName FROM patient","SELECT LName,FName FROM provider","SELECT LName,FName FROM employee",
					"SELECT LName,FName FROM referral" })
				{
					foreach(object[] row in db.Rows(sql)) {
						Assert.IsFalse(NameBank.SetCommonRealNames.Contains(Convert.ToString(row[0])),Convert.ToString(row[0]));
						Assert.IsFalse(NameBank.SetCommonRealNames.Contains(Convert.ToString(row[1])),Convert.ToString(row[1]));
					}
				}
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM patient WHERE SSN<>''"));
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM patient WHERE Email<>'' AND Email NOT REGEXP '@example\\\\.(com|net|org)$'"));
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM patient WHERE HmPhone<>'' AND HmPhone NOT LIKE '%555-01__'"));
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM procedurecode WHERE ProcCode NOT LIKE 'Z%'"));
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM creditcard WHERE CCNumberMasked NOT LIKE 'XXXXXXXXXXXX%'"));
			}
		}

		[TestMethod]
		public void PortalEmailColumn_IsFilledAndUnique() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server,DbA)) {
				Assert.IsTrue(db.ScalarLong("SELECT COUNT(*) FROM patient WHERE _portal_active_email IS NOT NULL")>0);
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM patient WHERE PatStatus<>0 AND _portal_active_email IS NOT NULL"));
			}
		}

		[TestMethod]
		public void ProcedureFees_FollowTheFeeInEffectOnTheProcedureDate() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server,DbA)) {
				long standard=db.ScalarLong("SELECT FeeSchedNum FROM feesched WHERE Description='Standard'");
				Assert.IsTrue(db.ScalarLong("SELECT COUNT(*) FROM fee WHERE DateEffective>'"+GenerationSettings.DefaultAsOf.ToString("yyyy-MM-dd")+"'")>0,
					"Some fees start after the as-of date.");
				long mismatches=db.ScalarLong("SELECT COUNT(*) FROM procedurelog pl WHERE pl.ProcStatus=2 AND pl.ProcFee<>("
					+"SELECT f.Amount FROM fee f WHERE f.FeeSched="+standard+" AND f.CodeNum=pl.CodeNum AND f.ProvNum IN (0,pl.ProvNum) "
					+"AND f.DateEffective<=pl.ProcDate ORDER BY f.ProvNum DESC,f.DateEffective DESC LIMIT 1)");
				Assert.AreEqual(0,mismatches);
				Assert.IsTrue(db.ScalarLong("SELECT COUNT(DISTINCT ProcFee) FROM procedurelog pl INNER JOIN procedurecode pc ON pc.CodeNum=pl.CodeNum "
					+"WHERE pc.ProcCode='Z1110' AND pl.ProcStatus=2")>=2,"Cleanings before and after the January increase have different fees.");
			}
		}

		///<summary>The generated rows hold only values Open Dental 25.3.48 itself stores: expanded tooth ranges on Exclusions rows only,
		///pipe-delimited pick lists with one PickListDb item per visible item, and the documented MedicationList, Patient List and Ins Pick
		///values.</summary>
		[TestMethod]
		public void Data_UsesThe25_3StoredFormats() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server,DbA)) {
				List<object[]> listRanges=db.Rows("SELECT BenefitType,ToothRange FROM benefit WHERE ToothRange IS NOT NULL AND ToothRange<>''");
				Assert.IsTrue(listRanges.Count>0);
				foreach(object[] row in listRanges) {
					Assert.AreEqual(4L,Convert.ToInt64(row[0]),"ToothRange only on Exclusions: "+row[1]);
					Assert.IsTrue(Regex.IsMatch((string)row[1],@"^\d+(,\d+)*$"),(string)row[1]);
				}
				Assert.IsTrue(listRanges.Any(x => (string)x[1]=="4,5,6,7,8,9,10,11,12,13"),"An entered range 4-13 is stored expanded.");
				List<object[]> listPickLists=db.Rows("SELECT FieldType,PickListVis,PickListDb FROM eformfield WHERE PickListVis<>'' "
					+"UNION ALL SELECT FieldType,PickListVis,PickListDb FROM eformfielddef WHERE PickListVis<>''");
				Assert.IsTrue(listPickLists.Count>0);
				foreach(object[] row in listPickLists) {
					string[] arrayVis=((string)row[1]).Split('|');
					Assert.IsTrue(arrayVis.Length>=2,(string)row[1]);
					if(Convert.ToInt32(row[0])==OdValues.EfInsPick) {
						Assert.AreEqual(3,arrayVis.Length,(string)row[1]);
						Assert.AreEqual("",(string)row[2]);
						continue;
					}
					Assert.AreEqual(arrayVis.Length,((string)row[2]).Split('|').Length,(string)row[1]+" / "+(string)row[2]);
				}
				Assert.IsTrue(db.ScalarLong("SELECT COUNT(*) FROM eformfield WHERE ConditionalValue LIKE '%|%'")>0,"Several trigger values, pipe-delimited.");
				Assert.IsTrue(db.ScalarLong("SELECT COUNT(*) FROM eformfield WHERE FieldType=9 AND ValueString='New'")>0);
				Assert.IsTrue(db.ScalarLong("SELECT COUNT(*) FROM eformfield WHERE FieldType=9 AND ValueString LIKE 'InsSubNum:%'")>0);
				Assert.IsTrue(db.ScalarLong("SELECT COUNT(*) FROM eformfield WHERE FieldType=8 AND ValueString<>''")>0);
				foreach(object[] row in db.Rows("SELECT ValueLabel,ValueString FROM eformfield WHERE FieldType=7")) {
					Assert.IsFalse(string.IsNullOrEmpty((string)JObject.Parse((string)row[0])["Title"]),(string)row[0]);
					Assert.IsNotNull(JArray.Parse((string)row[1]));
				}
				Assert.AreEqual(0,Validator.FailedChecks(_reportA).Count(x => x.StartsWith("check.benefit_") || x.StartsWith("check.eform_")));
			}
		}

		[TestMethod]
		public void GuardianRelationships_UseThe25_1Numbering() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server,DbA)) {
				Assert.IsTrue(db.ScalarLong("SELECT COUNT(*) FROM guardian WHERE Relationship=6")>0,"Fathers are 6 since 25.1.");
				Assert.AreEqual(0,db.ScalarLong("SELECT COUNT(*) FROM guardian WHERE Relationship=0"),"0 is None and never stored.");
			}
		}

		///<summary>Rehearses what M1-Z will do: copy, apply the adoption's allowed changes, validate and compare. Then a forbidden change (a
		///second guardian renumbering) must be caught.</summary>
		[TestMethod]
		public void SimulatedAdoption_OnlyAllowedDifferences() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server)) {
				if(db.DatabaseExists(DbAdopted)) {
					db.Execute("DROP DATABASE `"+DbAdopted+"`");
				}
				db.Execute("CREATE DATABASE `"+DbAdopted+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				foreach(TableInfo table in SchemaInfo.Load(db,DbA).DictTables.Values) {
					db.Execute("CREATE TABLE `"+DbAdopted+"`.`"+table.Name+"` LIKE `"+DbA+"`.`"+table.Name+"`");
					string columns=string.Join(",",table.ListColumns.Where(x => !x.IsGenerated).Select(x => "`"+x.Name+"`"));
					db.Execute("INSERT INTO `"+DbAdopted+"`.`"+table.Name+"` ("+columns+") SELECT "+columns+" FROM `"+DbA+"`.`"+table.Name+"` ORDER BY 1");
				}
				db.Use(DbAdopted);
				db.Execute("ALTER TABLE patient DROP INDEX portal_active_email_uniq, DROP COLUMN _portal_active_email");
				//A rehash that forgets the ON UPDATE timestamp changes SecDateTEdit on every row; the comparison must catch that.
				db.Execute("UPDATE inssub SET SecurityHash=CONCAT('osd1$',SHA2(InsSubNum,256))");
				List<Difference> listCareless=ReportComparer.Compare(_reportA,Validator.Run(db,DbAdopted,null,null),
					AllowedDifferences.Load(Path.Combine(Path.GetDirectoryName(Validator.ReadTotalsSqlPath()),"..","allowed-differences.adoption.json")));
				Assert.IsTrue(listCareless.Any(x => !x.IsAllowed && x.Table=="inssub" && x.Column=="SecDateTEdit"),ReportComparer.Format(listCareless));
				//The correct rehash keeps the audit timestamp.
				db.Execute("UPDATE inssub SET SecurityHash=CONCAT('osd1$',SHA2(InsSubNum+1,256)),SecDateTEdit=SecDateTEdit");
				db.Execute("UPDATE inssub a INNER JOIN `"+DbA+"`.inssub b ON a.InsSubNum=b.InsSubNum SET a.SecDateTEdit=b.SecDateTEdit");
				db.Execute("UPDATE preference SET ValueString='99.0.0.0' WHERE PrefName IN ('DataBaseVersion','ProgramVersion')");
				string allowedPath=Path.Combine(Path.GetDirectoryName(Validator.ReadTotalsSqlPath()),"..","allowed-differences.adoption.json");
				AllowedDifferences allowed=AllowedDifferences.Load(allowedPath);
				ValidationReport adopted=Validator.Run(db,DbAdopted,null,null);
				List<Difference> listDiffs=ReportComparer.Compare(_reportA,adopted,allowed);
				Assert.IsTrue(listDiffs.Any(x => x.Kind==DifferenceKind.ColumnRemoved && x.Column=="_portal_active_email"));
				Assert.IsTrue(listDiffs.All(x => x.IsAllowed),ReportComparer.Format(listDiffs));
				db.Execute("UPDATE guardian SET Relationship=23 WHERE Relationship=6");
				adopted=Validator.Run(db,DbAdopted,null,null);
				listDiffs=ReportComparer.Compare(_reportA,adopted,allowed);
				Assert.IsTrue(listDiffs.Any(x => !x.IsAllowed && x.Table=="guardian" && x.Column=="Relationship"),ReportComparer.Format(listDiffs));
			}
		}

		[TestMethod]
		public void FillRemaining_PutsRowsInEveryOtherTable() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server)) {
				GenerationSettings settings=new GenerationSettings { Families=3,FillRemaining=2 };
				SchemaBuilder.Create(db,DbFill,true,SchemaBuilder.DefaultSchemaSqlPath,SchemaBuilder.DefaultReferenceDatabase);
				Generator.Run(db,DbFill,settings,x => { });
				ValidationReport report=Validator.Run(db,DbFill,null,null);
				List<string> listEmpty=report.Tables.Where(x => x.Rows==0 && !Generator.SetFillerExcludedTables.Contains(x.Name)).Select(x => x.Name).ToList();
				Assert.AreEqual(0,listEmpty.Count,string.Join(", ",listEmpty));
			}
		}

		///<summary>A dump and restore rounds a FLOAT to 6 significant digits (34.285713 to 34.2857). Both print as 34.2857, so a checksum of the printed
		///value cannot tell them apart; the report's column checksum reads FLOAT values widened to DOUBLE and does. DOUBLE values print exactly.</summary>
		[TestMethod]
		public void ColumnChecksums_SeeAFloatRoundedTo6Digits() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server)) {
				foreach((string database,string valueFloat,string valueDouble) item in new[] { (DbFloatA,"34.285713","0.30000000000000004"),
					(DbFloatB,"34.2857","0.3") })
				{
					db.Execute("DROP DATABASE IF EXISTS `"+item.database+"`");
					db.Execute("CREATE DATABASE `"+item.database+"`");
					db.Execute("CREATE TABLE `"+item.database+"`.imagedraw (ImageDrawNum bigint NOT NULL PRIMARY KEY,FontSize float NOT NULL,Amount double NOT NULL,"
						+"Note varchar(20) NOT NULL) ENGINE=MyISAM");
					db.Execute("INSERT INTO `"+item.database+"`.imagedraw VALUES (1,"+item.valueFloat+","+item.valueDouble+",'x'),(2,8.25,1.5,'y')");
				}
				Assert.AreEqual("34.2857",Convert.ToString(db.Scalar("SELECT CAST(FontSize AS CHAR) FROM `"+DbFloatA+"`.imagedraw WHERE ImageDrawNum=1")),
					"MariaDB prints the long value with 6 digits.");
				TableReport ChecksumOf(string database) {
					db.Use(database);//ChecksumTable reads the table of the current database, as Validator.Run does.
					return Validator.ChecksumTable(db,SchemaInfo.Load(db,database).GetTable("imagedraw"));
				}
				TableReport reportA=ChecksumOf(DbFloatA);
				TableReport reportB=ChecksumOf(DbFloatB);
				Assert.AreNotEqual(reportA.Columns["FontSize"],reportB.Columns["FontSize"],"The FLOAT column checksum sees the rounding.");
				Assert.AreNotEqual(reportA.Columns["Amount"],reportB.Columns["Amount"],"The DOUBLE column checksum sees the 17th digit.");
				Assert.AreEqual(reportA.Columns["Note"],reportB.Columns["Note"]);
				db.Execute("UPDATE `"+DbFloatB+"`.imagedraw SET FontSize=34.285713,Amount=0.30000000000000004 WHERE ImageDrawNum=1");
				reportB=ChecksumOf(DbFloatB);
				Assert.AreEqual(reportA.Columns["FontSize"],reportB.Columns["FontSize"]);
				Assert.AreEqual(reportA.Columns["Amount"],reportB.Columns["Amount"]);
				Assert.AreEqual(reportA.ChecksumTable,reportB.ChecksumTable);
			}
		}

		[TestMethod]
		public void GeneratorRefusesToWriteRealDatabases() {
			SkipIfNeeded();
			using(Db db=Db.Open(_server)) {
				TestUtil.AssertThrows<RefusedException>(() => SchemaBuilder.Create(db,"opendental",true,SchemaBuilder.DefaultSchemaSqlPath,null));
				TestUtil.AssertThrows<RefusedException>(() => SchemaBuilder.Create(db,DbA,false,SchemaBuilder.DefaultSchemaSqlPath,null));
				TestUtil.AssertThrows<RefusedException>(() => Generator.Run(db,"od_253_schema",new GenerationSettings(),x => { }));
			}
		}
	}
}
