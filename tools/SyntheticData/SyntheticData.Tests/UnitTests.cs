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
	[TestClass]
	public class RngTests {
		[TestMethod]
		public void SameSeedAndStream_GiveSameSequence() {
			Rng a=Rng.Derive(42,"family:3");
			Rng b=Rng.Derive(42,"family:3");
			for(int i=0;i<1000;i++) {
				Assert.AreEqual(a.NextULong(),b.NextULong());
			}
		}

		[TestMethod]
		public void DifferentStreams_Differ() {
			Assert.AreNotEqual(Rng.Derive(42,"family:3").NextULong(),Rng.Derive(42,"family:4").NextULong());
			Assert.AreNotEqual(Rng.Derive(42,"family:3").NextULong(),Rng.Derive(43,"family:3").NextULong());
		}

		[TestMethod]
		public void Sequence_IsPinned() {
			//The generator's output depends on these exact numbers; a change here changes every synthetic database.
			Rng rng=new Rng(0);
			Assert.AreEqual(0xE220A8397B1DCDAFUL,rng.NextULong());
			Assert.AreEqual(0x6E789E6AA1B965F4UL,rng.NextULong());
		}

		[TestMethod]
		public void Ranges_StayInBounds() {
			Rng rng=new Rng(7);
			for(int i=0;i<10000;i++) {
				int value=rng.Between(-3,5);
				Assert.IsTrue(value>=-3 && value<=5);
				double d=rng.NextDouble();
				Assert.IsTrue(d>=0 && d<1);
				decimal money=rng.Money(10,40);
				Assert.IsTrue(money>=10 && money<=40);
				Assert.AreEqual(money,Math.Round(money,2));
			}
		}
	}

	[TestClass]
	public class LocalOnlyTests {
		[TestMethod]
		public void OnlyThisMachineIsAllowed() {
			foreach(string host in new[] { "localhost","127.0.0.1","::1","[::1]","LOCALHOST" }) {
				Assert.IsTrue(LocalOnly.IsLocalHost(host),host);
			}
			foreach(string host in new[] { "fox-db","192.168.1.10","10.0.0.5","example.com","127.0.0.2","","0.0.0.0",null }) {
				Assert.IsFalse(LocalOnly.IsLocalHost(host),host??"(null)");
			}
		}

		[TestMethod]
		public void OpeningARemoteServer_IsRefusedBeforeConnecting() {
			ServerSettings settings=new ServerSettings { Server="fox-db" };
			TestUtil.AssertThrows<RefusedException>(() => Db.Open(settings));
		}

		[TestMethod]
		public void OnlySynthDatabasesAreWritable() {
			foreach(string name in new[] { "synth_253","synth","synth_test_a","synth2" }) {
				Assert.IsTrue(LocalOnly.IsWritableDatabaseName(name),name);
			}
			foreach(string name in new[] { "opendental","fox-db","od_253_schema","opendental_dev","synth-253","Synth_253","synth_253;drop","" }) {
				Assert.IsFalse(LocalOnly.IsWritableDatabaseName(name),name);
			}
		}
	}

	[TestClass]
	public class NameBankTests {
		[TestMethod]
		public void GeneratedNames_AreNeverCommonRealNames() {
			Rng rng=new Rng(11);
			for(int i=0;i<20000;i++) {
				string surname=NameBank.Surname(rng);
				string given=NameBank.GivenName(rng);
				Assert.IsFalse(NameBank.SetCommonRealNames.Contains(surname),surname);
				Assert.IsFalse(NameBank.SetCommonRealNames.Contains(given),given);
			}
		}

		[TestMethod]
		public void ContactDetails_UseReservedRanges() {
			Rng rng=new Rng(5);
			for(int i=0;i<1000;i++) {
				StringAssert.Matches(NameBank.Phone(rng),new System.Text.RegularExpressions.Regex(@"^\(\d{3}\)555-01\d{2}$"));
			}
			for(long num=1;num<50;num++) {
				string email=NameBank.Email("Ael","Quorvale",num);
				Assert.IsTrue(email.EndsWith("@example.com") || email.EndsWith("@example.net") || email.EndsWith("@example.org"),email);
			}
		}
	}

	[TestClass]
	public class SqlLiteralTests {
		private static ColumnInfo Column(string dataType,bool nullable=false,long? maxLength=null,string def=null) {
			return new ColumnInfo { Name="C",DataType=dataType,ColumnType=dataType,IsNullable=nullable,MaxLength=maxLength,Default=def };
		}

		[TestMethod]
		public void Strings_AreEscaped() {
			Assert.AreEqual(@"'O\'Brien \\ x\n'",SqlLiteral.Format("O'Brien \\ x\n",Column("varchar",maxLength:255),"t"));
		}

		[TestMethod]
		public void Values_AreFormattedInvariantly() {
			Assert.AreEqual("1234.5",SqlLiteral.Format(1234.5m,Column("double"),"t"));
			Assert.AreEqual("'2026-09-30'",SqlLiteral.Format(new DateTime(2026,9,30,14,5,0),Column("date"),"t"));
			Assert.AreEqual("'2026-09-30 14:05:00'",SqlLiteral.Format(new DateTime(2026,9,30,14,5,0),Column("datetime"),"t"));
			Assert.AreEqual("'08:30:00'",SqlLiteral.Format(new TimeSpan(8,30,0),Column("time"),"t"));
			Assert.AreEqual("1",SqlLiteral.Format(true,Column("tinyint"),"t"));
			Assert.AreEqual("0x0AFF",SqlLiteral.Format(new byte[] { 10,255 },Column("mediumblob"),"t"));
		}

		[TestMethod]
		public void MissingValues_FollowOpenDentalConventions() {
			DateTime stamp=new DateTime(2026,9,30,18,0,0);
			Assert.AreEqual("'2026-09-30 18:00:00'",SqlLiteral.Missing(Column("timestamp",def:"current_timestamp()"),stamp));
			Assert.AreEqual("DEFAULT",SqlLiteral.Missing(Column("double",def:"-1"),stamp));
			Assert.AreEqual("'0001-01-01'",SqlLiteral.Missing(Column("date",def:"'0000-00-00'"),stamp));
			Assert.AreEqual("''",SqlLiteral.Missing(Column("text"),stamp));
			Assert.AreEqual("0",SqlLiteral.Missing(Column("bigint"),stamp));
		}

		[TestMethod]
		public void TooLongText_AndNullIntoNotNull_Fail() {
			TestUtil.AssertThrows<InvalidOperationException>(() => SqlLiteral.Format("abcdef",Column("varchar",maxLength:5),"t"));
			TestUtil.AssertThrows<InvalidOperationException>(() => SqlLiteral.Format(null,Column("varchar"),"t"));
			Assert.AreEqual("NULL",SqlLiteral.Format(null,Column("varchar",nullable:true),"t"));
		}
	}

	[TestClass]
	public class SchemaBuilderTests {
		[TestMethod]
		public void DumpStatements_KeepOnlyCreateTable_AndResetCounters() {
			string dump=string.Join("\n",new[] {
				"/*M!999999\\- enable the sandbox mode */ ",
				"-- MariaDB dump",
				"/*!40101 SET NAMES utf8mb4 */;",
				"DROP TABLE IF EXISTS `account`;",
				"CREATE TABLE `account` (",
				"  `AccountNum` bigint(20) NOT NULL AUTO_INCREMENT,",
				"  PRIMARY KEY (`AccountNum`)",
				") ENGINE=MyISAM AUTO_INCREMENT=15 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;",
				"/*!40101 SET character_set_client = @saved_cs_client */;",
			});
			List<string> list=SchemaBuilder.ReadDumpStatements(dump);
			Assert.AreEqual(1,list.Count);
			StringAssert.StartsWith(list[0],"CREATE TABLE `account`");
			Assert.IsFalse(list[0].Contains("AUTO_INCREMENT=15"));
			StringAssert.Contains(list[0],"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
		}

		[TestMethod]
		public void RealDump_Has422Tables() {
			if(!File.Exists(SchemaBuilder.DefaultSchemaSqlPath)) {
				Assert.Inconclusive("The 25.3.48 schema dump is not on this machine.");
			}
			List<string> list=SchemaBuilder.ReadDumpStatements(File.ReadAllText(SchemaBuilder.DefaultSchemaSqlPath));
			Assert.AreEqual(SchemaBuilder.ExpectedTableCount,list.Count);
			Assert.IsFalse(list.Any(x => System.Text.RegularExpressions.Regex.IsMatch(x,@"AUTO_INCREMENT=\d")));
		}
	}

	[TestClass]
	public class AgingTests {
		[TestMethod]
		public void CreditsPayOldestChargesFirst() {
			DateTime asOf=new DateTime(2026,9,30);
			Person person=new Person();
			List<LedgerEntry> list=new List<LedgerEntry> {
				new LedgerEntry(asOf.AddDays(-100),100,person),
				new LedgerEntry(asOf.AddDays(-45),50,person),
				new LedgerEntry(asOf.AddDays(-10),30,person),
				new LedgerEntry(asOf.AddDays(-5),-120,person),
				new LedgerEntry(asOf.AddDays(5),999,person),//after the as-of date: ignored
			};
			AgingResult result=Aging.Compute(list,asOf);
			Assert.AreEqual(0m,result.BalOver90);
			Assert.AreEqual(30m,result.Bal31To60);
			Assert.AreEqual(30m,result.Bal0To30);
			Assert.AreEqual(60m,result.Total);
		}

		[TestMethod]
		public void CreditBalance_ShowsAsNegativeCurrentBucket() {
			DateTime asOf=new DateTime(2026,9,30);
			Person person=new Person();
			AgingResult result=Aging.Compute(new[] { new LedgerEntry(asOf.AddDays(-70),40,person),new LedgerEntry(asOf.AddDays(-2),-100,person) },asOf);
			Assert.AreEqual(-60m,result.Bal0To30);
			Assert.AreEqual(0m,result.Bal61To90);
			Assert.AreEqual(-60m,result.Total);
		}
	}

	[TestClass]
	public class GenerationModelTests {
		[TestMethod]
		public void SizePresets() {
			Assert.AreEqual(40,new GenerationSettings { Size=DataSize.Small }.FamilyCount);
			Assert.AreEqual(600,new GenerationSettings { Size=DataSize.Medium }.FamilyCount);
			Assert.AreEqual(4000,new GenerationSettings { Size=DataSize.Large }.FamilyCount);
			Assert.AreEqual(3,new GenerationSettings { Size=DataSize.Large }.YearCount);
			Assert.AreEqual(77,new GenerationSettings { Size=DataSize.Large,Families=77,Years=2 }.FamilyCount);
			Assert.AreEqual(DataSize.Medium,GenerationSettings.ParseSize("Medium"));
			TestUtil.AssertThrows<ArgumentException>(() => GenerationSettings.ParseSize("huge"));
		}

		[TestMethod]
		public void ProcedureCodes_AreInventedZCodes() {
			List<ProcCodeDef> list=Catalog.CreateProcCodes();
			Assert.IsTrue(list.All(x => System.Text.RegularExpressions.Regex.IsMatch(x.Code,"^Z\\d{4}$")),"Only invented Z codes; no CDT codes.");
			Assert.AreEqual(list.Count,list.Select(x => x.Code).Distinct().Count());
			List<CovCatDef> listCovCats=Catalog.CreateCovCats();
			Assert.IsTrue(list.All(x => Catalog.CovCatFor(listCovCats,x.Code)!=null),"Every code falls in a coverage category.");
		}

		[TestMethod]
		public void SlotBook_NeverDoubleBooks_AndKeepsLunchFree() {
			SlotBook book=new SlotBook();
			DateTime day=new DateTime(2026,3,2);
			List<(DateTime start,int slots)> list=new List<(DateTime,int)>();
			for(int i=0;i<40;i++) {
				DateTime? start=book.TryBook(1,day,7,(i*13)%SlotBook.SlotsPerDay);
				if(start.HasValue) {
					list.Add((start.Value,7));
				}
			}
			Assert.IsTrue(list.Count>=8,list.Count.ToString());
			for(int i=0;i<list.Count;i++) {
				DateTime end=list[i].start.AddMinutes(list[i].slots*5);
				Assert.IsTrue(end<=day.AddHours(12) || list[i].start>=day.AddHours(13),"No appointment over lunch.");
				for(int j=i+1;j<list.Count;j++) {
					DateTime endJ=list[j].start.AddMinutes(list[j].slots*5);
					Assert.IsTrue(end<=list[j].start || endJ<=list[i].start,"Overlap");
				}
			}
		}
	}

	[TestClass]
	public class ValidatorUnitTests {
		[TestMethod]
		public void TotalsFile_ParsesIntoUniquelyNamedQueries() {
			List<(string name,string sql)> list=Validator.ReadQueries(Validator.ReadTotalsSql());
			Assert.IsTrue(list.Count>=60,list.Count.ToString());
			Assert.AreEqual(list.Count,list.Select(x => x.name).Distinct().Count());
			Assert.IsTrue(list.All(x => x.sql.StartsWith("SELECT",StringComparison.OrdinalIgnoreCase)),"Read-only queries only.");
			Assert.IsTrue(list.Any(x => x.name=="aging.ledger_total"));
			Assert.IsTrue(list.Count(x => x.name.StartsWith("check."))>=10);
		}

		[TestMethod]
		public void AsOf_IsReadFromGenerationInfo() {
			Assert.AreEqual(new DateTime(2026,9,30),Validator.AsOfFromGenerationInfo("seed=1;size=small;asOf=2026-09-30;tool=SyntheticData 1"));
			Assert.IsNull(Validator.AsOfFromGenerationInfo(null));
		}
	}

	[TestClass]
	public class ReportComparerTests {
		private static ValidationReport Report() {
			ValidationReport report=new ValidationReport { Database="synth" };
			TableReport patient=new TableReport { Name="patient",Rows=3,ChecksumTable="111" };
			patient.Columns["PatNum"]="10";
			patient.Columns["LName"]="20";
			patient.Columns["SecurityHash"]="30";
			patient.Columns["_portal_active_email"]="40";
			report.Tables.Add(patient);
			TableReport guardian=new TableReport { Name="guardian",Rows=2,ChecksumTable="222" };
			guardian.Columns["Relationship"]="50";
			report.Tables.Add(guardian);
			report.Totals["patients.portal_active_email_nonnull"]="2";
			report.Totals["production.gross"]="100.00";
			return report;
		}

		private static AllowedDifferences Adoption() {
			string path=Path.Combine(Path.GetDirectoryName(Validator.ReadTotalsSqlPath()),"..","allowed-differences.adoption.json");
			return AllowedDifferences.Load(path);
		}

		[TestMethod]
		public void IdenticalReports_HaveNoDifferences() {
			Assert.AreEqual(0,ReportComparer.Compare(Report(),Report(),null).Count);
		}

		[TestMethod]
		public void AdoptionChanges_AreAllowed_OtherChangesAreNot() {
			ValidationReport after=Report();
			TableReport patient=after.Tables[0];
			patient.Columns.Remove("_portal_active_email");
			patient.Columns["SecurityHash"]="31";
			patient.ChecksumTable="112";
			after.Totals["patients.portal_active_email_nonnull"]="error: Unknown column";
			List<Difference> list=ReportComparer.Compare(Report(),after,Adoption());
			Assert.IsTrue(list.Count>=4);
			Assert.IsTrue(list.All(x => x.IsAllowed),ReportComparer.Format(list));
			//A double conversion of guardian relationships must be caught.
			after.Tables[1].Columns["Relationship"]="51";
			after.Tables[1].ChecksumTable="223";
			after.Totals["production.gross"]="101.00";
			list=ReportComparer.Compare(Report(),after,Adoption());
			List<Difference> listUnexpected=list.Where(x => !x.IsAllowed).ToList();
			Assert.IsTrue(listUnexpected.Any(x => x.Kind==DifferenceKind.ColumnChanged && x.Table=="guardian"));
			Assert.IsTrue(listUnexpected.Any(x => x.Kind==DifferenceKind.TotalChanged && x.Name=="production.gross"));
		}

		[TestMethod]
		public void ChecksumTableChange_WithoutColumnChange_IsUnexpected() {
			ValidationReport after=Report();
			after.Tables[1].ChecksumTable="999";
			List<Difference> list=ReportComparer.Compare(Report(),after,Adoption());
			Assert.AreEqual(1,list.Count);
			Assert.IsFalse(list[0].IsAllowed);
			//It says what it means, and where the runbook says what to do.
			StringAssert.Contains(list[0].Note,"every value reads the same");
			StringAssert.Contains(list[0].ToString(),"[note: ");
			StringAssert.Contains(list[0].ToString(),"step 3.3");
			//With a column difference it is explained by it, and has no note.
			after.Tables[1].Columns["Relationship"]="51";
			list=ReportComparer.Compare(Report(),after,Adoption());
			Difference checksum=list.Single(x => x.Kind==DifferenceKind.ChecksumTableChanged);
			Assert.IsTrue(checksum.IsAllowed);
			Assert.IsNull(checksum.Note);
		}

		///<summary>Adoption does not touch updatehistory, so the allowed list has no rule for it (it had a whole-table one).</summary>
		[TestMethod]
		public void UpdateHistoryChange_IsUnexpected() {
			ValidationReport before=Report();
			TableReport history=new TableReport { Name="updatehistory",Rows=4,ChecksumTable="333" };
			history.Columns["ProgramVersion"]="60";
			before.Tables.Add(history);
			ValidationReport after=Report();
			TableReport historyAfter=new TableReport { Name="updatehistory",Rows=5,ChecksumTable="334" };
			historyAfter.Columns["ProgramVersion"]="61";
			after.Tables.Add(historyAfter);
			List<Difference> list=ReportComparer.Compare(before,after,Adoption());
			Assert.IsTrue(list.Where(x => x.Table=="updatehistory").All(x => !x.IsAllowed || x.Kind==DifferenceKind.ChecksumTableChanged),ReportComparer.Format(list));
			Assert.IsTrue(list.Any(x => x.Table=="updatehistory" && x.Kind==DifferenceKind.RowsChanged && !x.IsAllowed));
		}

		///<summary>MariaDB writes a FLOAT as text with 6 significant digits, so the column checksum widens it to DOUBLE, which it writes exactly.</summary>
		[TestMethod]
		public void ColumnChecksum_ReadsFloatsExactly() {
			Assert.AreEqual("CAST(`FontSize` AS DOUBLE)",Validator.ExactText(new ColumnInfo { Name="FontSize",DataType="float" }));
			Assert.AreEqual("`ClaimFee`",Validator.ExactText(new ColumnInfo { Name="ClaimFee",DataType="double" }));
			Assert.AreEqual("`LName`",Validator.ExactText(new ColumnInfo { Name="LName",DataType="varchar" }));
		}
	}

	[TestClass]
	public class CoverageListTests {
		///<summary>Coverage must name every 24.3-25.3 entry of docs/specs/schema-changes.json, and nothing else except the two documented
		///extras.</summary>
		[TestMethod]
		public void CoverageList_MatchesSchemaChangesJson() {
			string path=TestUtil.FindRepoFile(Path.Combine("docs","specs","schema-changes.json"));
			if(path==null) {
				Assert.Inconclusive("docs/specs/schema-changes.json not found.");
			}
			JArray array=JArray.Parse(File.ReadAllText(path));
			HashSet<string> setTables=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> setColumns=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach(JObject entry in array.Cast<JObject>().Where(x => (string)x["range"]=="24.3-25.3")) {
				string table=(string)entry["table"];
				if((string)entry["change"]=="add-table") {
					setTables.Add(table);
				}
				else {
					setColumns.Add(table+"."+(string)entry["column"]);
				}
			}
			HashSet<string> setCoverageTables=new HashSet<string>(Coverage.NewTables,StringComparer.OrdinalIgnoreCase);
			HashSet<string> setCoverageColumns=new HashSet<string>(Coverage.NewOrChangedColumns,StringComparer.OrdinalIgnoreCase);
			Assert.AreEqual(14,setTables.Count);
			CollectionAssert.AreEquivalent(setTables.ToList(),setCoverageTables.Where(x => x!="deletedobject").ToList());
			CollectionAssert.AreEquivalent(setColumns.ToList(),setCoverageColumns.Where(x => x!="patient._portal_active_email").ToList());
		}

		///<summary>The widened-column list must match the 24.3-25.3 "alter" entries of schema-changes.json, with the old length taken from
		///each entry's note ("Previous type: varchar(N)", or the varchar(N) the office still has for a DRIFT entry), and the columns the
		///reference schema has not widened must be exactly the DRIFT entries.</summary>
		[TestMethod]
		public void WidenedColumns_MatchAlterEntries() {
			string path=TestUtil.FindRepoFile(Path.Combine("docs","specs","schema-changes.json"));
			if(path==null) {
				Assert.Inconclusive("docs/specs/schema-changes.json not found.");
			}
			JArray array=JArray.Parse(File.ReadAllText(path));
			Dictionary<string,int> dictExpected=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> setDrift=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach(JObject entry in array.Cast<JObject>().Where(x => (string)x["range"]=="24.3-25.3" && (string)x["change"]=="alter")) {
				string item=(string)entry["table"]+"."+(string)entry["column"];
				string notes=(string)entry["notes"]??"";
				Match match=Regex.Match(notes,@"varchar\((\d+)\)",RegexOptions.IgnoreCase);
				Assert.IsTrue(match.Success,item+": no old varchar width in the notes: "+notes);
				dictExpected[item]=int.Parse(match.Groups[1].Value);
				if(notes.StartsWith("DRIFT",StringComparison.OrdinalIgnoreCase)) {
					setDrift.Add(item);
				}
			}
			Assert.AreEqual(10,dictExpected.Count);
			CollectionAssert.AreEquivalent(dictExpected.Keys.ToList(),Coverage.DictWidenedOldLength.Keys.ToList());
			foreach(KeyValuePair<string,int> pair in dictExpected) {
				Assert.AreEqual(pair.Value,Coverage.DictWidenedOldLength[pair.Key],pair.Key);
				Assert.IsTrue(Coverage.NewOrChangedColumns.Contains(pair.Key),pair.Key+" is not in NewOrChangedColumns.");
			}
			CollectionAssert.AreEquivalent(setDrift.ToList(),Coverage.DictWideningNotInReference.Keys.ToList());
			Assert.AreEqual(26,Coverage.RequiredMaxLength("insplan.GroupNum"));
			Assert.AreEqual(255,Coverage.RequiredMaxLength("claimpayment.Note"));
		}

		///<summary>The generator's long values must actually need the new widths, and the claimpayment note must fit the reference schema.</summary>
		[TestMethod]
		public void LongValues_ExceedOldWidths() {
			for(int planIndex=0;planIndex<3;planIndex++) {
				foreach(bool isPpo in new[] { true,false }) {
					string groupNum=PracticeGenerator.LongGroupNumber(999999,isPpo,planIndex);
					Assert.IsTrue(groupNum.Length>25 && groupNum.Length<=50,groupNum);
					groupNum=PracticeGenerator.LongGroupNumber(100000,isPpo,planIndex);
					Assert.IsTrue(groupNum.Length>25 && groupNum.Length<=50,groupNum);
				}
			}
			string pickList=EFormJson.JoinPickList(PracticeGenerator.ArrayLastVisitAnswers);
			Assert.IsTrue(pickList.Length>255,"pick list "+pickList.Length);
			Assert.IsTrue(PracticeGenerator.LastVisitFollowUpTrigger.Length>255,"trigger "+PracticeGenerator.LastVisitFollowUpTrigger.Length);
			Assert.IsFalse(PracticeGenerator.ArrayLastVisitAnswers.Any(x => x.Contains("|")));
			Assert.IsTrue(FamilySimulator.LongBounceReport("a@example.com").Length>255);
			Assert.AreEqual(PracticeWideGenerator.ClaimPaymentNoteReferenceWidth,
				PracticeWideGenerator.LongEftNote("EFT trace 12345678","Synthetic Mutual Dental").Length);
			Assert.AreEqual(255,PracticeWideGenerator.LongEftNote("EFT trace 1","X").Length);
		}
	}

	///<summary>Stored formats that Open Dental 25.3.48 itself produces: benefit tooth ranges, eForm pick lists, conditional values and the
	///JSON and Ins Pick values of the 25.1-25.2 eForm field types. DatabaseTests and the check.* totals verify the generated rows.</summary>
	[TestClass]
	public class StoredFormatTests {
		private static readonly Regex _regexToothRange=new Regex(
			"^([1-9]|[12][0-9]|3[0-2]|5[1-9]|[67][0-9]|8[0-2]|[A-T]S?)(,([1-9]|[12][0-9]|3[0-2]|5[1-9]|[67][0-9]|8[0-2]|[A-T]S?))*$");

		[TestMethod]
		public void ToothRanges_AreStoredExpandedWithoutHyphens() {
			Assert.AreEqual("4,5,6,7,8,9,10,11,12,13",PracticeGenerator.ToothRangeForDb("4-13"));
			Assert.AreEqual("1,16,17,32",PracticeGenerator.ToothRangeForDb("1,16,17,32"));
			Assert.AreEqual("2,3,4,15,A,T",PracticeGenerator.ToothRangeForDb("2-4, 15,A,T"));
			foreach(string toothRange in new[] { "4,5,6,7,8,9,10,11,12,13","1,16,17,32","A,AS,T,51,82" }) {
				Assert.IsTrue(PracticeGenerator.IsStoredToothRange(toothRange),toothRange);
				Assert.IsTrue(_regexToothRange.IsMatch(toothRange),toothRange);
			}
			foreach(string toothRange in new[] { "4-13","","2, 3","33","0","U","a",",2","2,",null }) {
				Assert.IsFalse(PracticeGenerator.IsStoredToothRange(toothRange),toothRange??"null");
			}
			TestUtil.AssertThrows<ArgumentException>(() => PracticeGenerator.ToothRangeForDb("13-4"));
		}

		///<summary>The generator refuses a tooth range on anything but an Exclusions row (BenefitType 4), and any range that is not in the
		///stored form. The generated rows themselves are checked by DatabaseTests.Data_UsesThe25_3StoredFormats.</summary>
		[TestMethod]
		public void ToothRanges_OnlyOnExclusions() {
			World world=new World();
			foreach(int benefitType in new[] { OdValues.BenCoInsurance,OdValues.BenDeductible,OdValues.BenLimitations,OdValues.BenWaitingPeriod }) {
				TestUtil.AssertThrows<InvalidOperationException>(() => PracticeGenerator.AddBenefit(world,1,0,benefitType,-1,-1,OdValues.TimeNone,
					OdValues.QtyAgeLimit,16,OdValues.CovNone,toothRange:"2,3,14,15",treatArea:OdValues.AreaTooth));
			}
			TestUtil.AssertThrows<InvalidOperationException>(() => PracticeGenerator.AddBenefit(world,1,0,OdValues.BenExclusions,-1,-1,OdValues.TimeNone,
				OdValues.QtyNone,0,OdValues.CovNone,toothRange:"4-13",treatArea:OdValues.AreaTooth));
			Assert.AreEqual(4,OdValues.BenExclusions);
		}

		private static IEnumerable<PracticeGenerator.EFormFieldSpec> AllFields() {
			return PracticeGenerator.ArrayRegistrationFields.Concat(PracticeGenerator.ArrayMedicalFields);
		}

		[TestMethod]
		public void PickLists_ArePipeDelimited_OneDbItemPerVisibleItem() {
			int radioButtons=0;
			foreach(PracticeGenerator.EFormFieldSpec field in AllFields()) {
				string pickListDb=PracticeGenerator.PickListDb(field.FieldType,field.PickList);
				if(field.FieldType==OdValues.EfRadioButtons) {
					radioButtons++;
					string[] arrayVis=EFormJson.SplitPickList(field.PickList);
					string[] arrayDb=EFormJson.SplitPickList(pickListDb);
					Assert.IsTrue(arrayVis.Length>=2,field.Label);
					Assert.AreEqual(arrayVis.Length,arrayDb.Length,field.Label);
				}
				else if(field.FieldType==OdValues.EfInsPick) {
					CollectionAssert.AreEqual(new[] { "None","Existing","New" },EFormJson.SplitPickList(field.PickList),"Ins Pick shows three options.");
					Assert.AreEqual("",pickListDb,"Ins Pick does not use PickListDb.");
				}
				else {
					Assert.AreEqual("",field.PickList,field.Label);
					Assert.AreEqual("",pickListDb,field.Label);
				}
			}
			Assert.AreEqual(4,radioButtons);
			Assert.AreEqual("M|F|U",PracticeGenerator.PickListDb(OdValues.EfRadioButtons,"Male|Female|Unknown"));
			Assert.AreEqual("Yes|No",PracticeGenerator.PickListDb(OdValues.EfRadioButtons,"Yes|No"));
		}

		///<summary>Each conditional value (pipe-delimited when there are several) matches an answer of its parent field, so the child can show.</summary>
		[TestMethod]
		public void ConditionalValues_MatchTheParentsAnswers() {
			int conditions=0;
			foreach(PracticeGenerator.EFormFieldSpec[] arrayFields in new[] { PracticeGenerator.ArrayRegistrationFields,PracticeGenerator.ArrayMedicalFields }) {
				foreach(PracticeGenerator.EFormFieldSpec field in arrayFields.Where(x => x.ConditionalParent!="")) {
					conditions++;
					PracticeGenerator.EFormFieldSpec parent=arrayFields.Single(x => x.Label==field.ConditionalParent);
					string[] arrayAnswers=parent.FieldType==OdValues.EfCheckBox ? new[] { "X" } : EFormJson.SplitPickList(parent.PickList);
					foreach(string value in EFormJson.SplitPickList(field.ConditionalValue)) {
						CollectionAssert.Contains(arrayAnswers,value,field.Label);
					}
				}
			}
			Assert.AreEqual(6,conditions);
			Assert.AreEqual(PracticeGenerator.ArrayLastVisitAnswers.Length-1,EFormJson.SplitPickList(PracticeGenerator.LastVisitFollowUpTrigger).Length);
		}

		///<summary>The JSON shapes deserialize into Open Dental's own eForm classes.</summary>
		[TestMethod]
		public void MedicationList_AndPatientList_UseTheDocumentedJson() {
			string medLabel=EFormJson.ValueLabel(OdValues.EfMedicationList,"Current medications");
			OpenDentBusiness.EFormMedListLayout layout=Newtonsoft.Json.JsonConvert.DeserializeObject<OpenDentBusiness.EFormMedListLayout>(medLabel);
			Assert.AreEqual("Current medications",layout.Title);
			Assert.IsTrue(layout.ImportCol1);
			List<OpenDentBusiness.EFormMed> listMeds=Newtonsoft.Json.JsonConvert.DeserializeObject<List<OpenDentBusiness.EFormMed>>(
				EFormJson.MedicationList(new[] { "Ibuprofen","Lisinopril" }));
			CollectionAssert.AreEqual(new[] { "Ibuprofen","Lisinopril" },listMeds.Select(x => x.MedName).ToList());
			Assert.AreEqual("[]",EFormJson.MedicationList(new string[0]));
			JObject patList=JObject.Parse(EFormJson.ValueLabel(OdValues.EfPatientList,"Subscriber"));
			Assert.AreEqual("Subscriber",(string)patList["Label"]);
			Assert.AreEqual("Gender",EFormJson.ValueLabel(OdValues.EfRadioButtons,"Gender"));
		}

		[TestMethod]
		public void InsPickValues_FollowTheDbDocs() {
			Regex regex=new Regex("^(|New|InsSubNum:[0-9]+|InsPendingNum:[0-9]+)$");
			foreach(string value in new[] { "",EFormJson.InsPickNew,EFormJson.InsPickExistingSub(42),EFormJson.InsPickExistingPending(7) }) {
				Assert.IsTrue(regex.IsMatch(value),value);
			}
			Assert.AreEqual("InsSubNum:42",EFormJson.InsPickExistingSub(42));
			Assert.AreEqual("InsPendingNum:7",EFormJson.InsPickExistingPending(7));
		}
	}

	public static class TestUtil {
		///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
		public static void AssertThrows<T>(Action action) where T : Exception {
			try {
				action();
			}
			catch(T) {
				return;
			}
			catch(Exception ex) {
				Assert.Fail("Expected "+typeof(T).Name+" but got "+ex.GetType().Name+": "+ex.Message);
			}
			Assert.Fail("Expected "+typeof(T).Name+" but nothing was thrown.");
		}

		public static void AssertThrows<T>(Func<object> func) where T : Exception {
			AssertThrows<T>(() => { func(); });
		}

		public static string FindRepoFile(string relativePath) {
			for(DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);dir!=null;dir=dir.Parent) {
				string candidate=Path.Combine(dir.FullName,relativePath);
				if(File.Exists(candidate)) {
					return candidate;
				}
			}
			return null;
		}
	}
}
