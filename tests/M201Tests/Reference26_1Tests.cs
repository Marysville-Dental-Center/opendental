// Tests of the Open Dental 26.1.70 reference schema (OpenPmsDentalBusiness/Misc/ForkMigrations/Reference/OdSchema-26.1.70.sql), the step levels and
// the table classes that map it. No database. The checks against Open Dental's published database documents run only when the documents are in
// the folder named by M201_OD_DOCS_DIR (they are not in the repository); otherwise they are inconclusive.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace M201Tests {
	[TestClass]
	public class Reference26_1Tests {
		///<summary>What the 26.1.70 reference adds to or changes in the 25.3.48 reference, written out independently of the reference file and the
		///steps: kind, table, column or key, and the expected definition. From the published 25.4 and 26.1 diff documents (names, order, types) and
		///the inferred rest (NOT NULL, defaults, keys), plus the claimpayment.Note drift fix.</summary>
		public static readonly string[][] ArrayExpectedChanges=new string[][] {
			new[] { "MissingTable","deletedobj",null,null },
			new[] { "MissingTable","paysuitepayment",null,null },
			new[] { "MissingTable","paysuitepaymentdetail",null,null },
			new[] { "MissingTable","schoolcourseenrollee",null,null },
			new[] { "MissingTable","schoolcourseinstructor",null,null },
			//25.4 columns
			new[] { "MissingColumn","carrier","PaySuiteTransSup","tinyint(4) NOT NULL" },
			new[] { "MissingColumn","eform","DateTimeSubmitted","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'" },
			new[] { "MissingColumn","evaluation","GradeOverride","float NOT NULL DEFAULT -1" },
			new[] { "MissingColumn","evaluationdef","SchoolCourseDefNum","bigint(20) NOT NULL" },
			new[] { "MissingColumn","inseditlog","OldValueBig","text NOT NULL" },
			new[] { "MissingColumn","inseditlog","NewValueBig","text NOT NULL" },
			new[] { "MissingColumn","inseditpatlog","OldValueBig","text NOT NULL" },
			new[] { "MissingColumn","inseditpatlog","NewValueBig","text NOT NULL" },
			new[] { "MissingColumn","payterminal","CCIntegration","varchar(50) NOT NULL" },
			new[] { "MissingColumn","procedurelog","NoBillIns","tinyint(4) NOT NULL" },
			new[] { "MissingColumn","registrationkey","DateTClinicAccess","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'" },
			new[] { "MissingColumn","schoolcoursedef","GradingScaleNum","bigint(20) NOT NULL" },
			new[] { "MissingColumn","sheet","DateTimeSubmitted","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'" },
			//26.1 columns
			new[] { "MissingColumn","carrier","PreAuthCodes","text NOT NULL" },
			new[] { "MissingColumn","chatuserod","PlayNotificationSound","tinyint(4) NOT NULL" },
			new[] { "MissingColumn","claimproc","Etrans835AttachNum","bigint(20) NOT NULL" },
			new[] { "MissingColumn","codegroup","ShowInHistory","tinyint(4) NOT NULL" },
			new[] { "MissingColumn","codegroup","HistProcCode","varchar(255) NOT NULL" },
			new[] { "MissingColumn","codegroup","IsPerioFourQuads","tinyint(4) NOT NULL" },
			new[] { "MissingColumn","definition","Supp","varchar(255) NOT NULL" },
			new[] { "MissingColumn","dunning","EmailType","tinyint(4) NOT NULL" },
			new[] { "MissingColumn","eformfield","DateTimeSig","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'" },
			new[] { "MissingColumn","employee","Title","bigint(20) NOT NULL" },
			new[] { "MissingColumn","procnote","UserNum2","bigint(20) NOT NULL" },
			new[] { "MissingColumn","procnote","Signature2","text DEFAULT NULL" },
			new[] { "MissingColumn","procnote","SigIsTopaz2","tinyint(3) unsigned NOT NULL" },
			new[] { "MissingColumn","smstomobile","SecUserNumEntry","bigint(20) NOT NULL" },
			new[] { "MissingColumn","statement","IsServiceDateView","tinyint(4) NOT NULL" },
			//Type changes
			new[] { "ColumnDefinition","branding","ValueString","mediumtext NOT NULL" },
			new[] { "ColumnDefinition","claimpayment","Note","text NOT NULL" },
			new[] { "ColumnDefinition","insverify","DateLastAssigned","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'" },
			new[] { "ColumnDefinition","insverifyhist","DateLastAssigned","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'" },
			new[] { "ColumnDefinition","referral","Telephone","varchar(30) DEFAULT ''" },
			//Keys on new columns (inferred)
			new[] { "MissingIndex","claimproc","Etrans835AttachNum","KEY (`Etrans835AttachNum`)" },
			new[] { "MissingIndex","employee","Title","KEY (`Title`)" },
			new[] { "MissingIndex","evaluationdef","SchoolCourseDefNum","KEY (`SchoolCourseDefNum`)" },
			new[] { "MissingIndex","procnote","UserNum2","KEY (`UserNum2`)" },
			new[] { "MissingIndex","schoolcoursedef","GradingScaleNum","KEY (`GradingScaleNum`)" },
			new[] { "MissingIndex","smstomobile","SecUserNumEntry","KEY (`SecUserNumEntry`)" },
		};

		///<summary>The tables whose class M2-01 added or changed.</summary>
		public static readonly string[] ArrayChangedTables=new string[] {
			"branding","carrier","chatuserod","claimpayment","claimproc","codegroup","definition","deletedobj","dunning","eform","eformfield","employee",
			"evaluation","evaluationdef","inseditlog","inseditpatlog","insverify","insverifyhist","paysuitepayment","paysuitepaymentdetail","payterminal",
			"procnote","procedurelog","registrationkey","schoolcoursedef","schoolcourseenrollee","schoolcourseinstructor","sheet","smstomobile",
			"statement",
		};

		[TestMethod]
		public void Reference_Has427MyIsamTablesAndIsTheDefault() {
			SchemaSnapshot reference=SchemaVerifier.GetReference26_1_70();
			Assert.AreEqual(427,reference.Tables.Count,"Open Dental 26.1 has 427 tables: the 422 of 25.3.48 and 5 new ones.");
			Assert.IsTrue(reference.Tables.All(x => x.Engine=="MyISAM" && x.Charset=="utf8mb3" && x.Collation=="utf8mb3_general_ci"));
			Assert.AreSame(reference,SchemaVerifier.GetReference());
			Assert.AreEqual("26.1.70",SchemaVerifier.ReferenceVersion);
			Assert.AreEqual(SchemaVerifier.ReferenceResourceName26_1_70,SchemaVerifier.ReferenceResourceName);
			Assert.AreEqual(422,SchemaVerifier.GetReference25_3_48().Tables.Count,"The 25.3.48 reference is still there, unchanged.");
			StringAssert.DoesNotMatch(SchemaVerifier.ReadReferenceDdl(),new Regex("AUTO_INCREMENT=[0-9]"));
			Assert.IsNull(reference.GetTable("patient").GetColumn(SchemaVerifier.PortalActiveEmailColumn));
			foreach(string table in SchemaVerifier.ArrayHqOnlyTables.Concat(SchemaVerifier.ArrayLegacyTables)) {
				Assert.IsFalse(reference.HasTable(table),table+" is not in Open Dental 26.1.");
			}
		}

		[TestMethod]
		public void Reference_DiffersFrom25_3_48ByExactlyThePublishedChanges() {
			SchemaVerifyResult result=SchemaVerifier.Compare(SchemaVerifier.GetReference26_1_70(),SchemaVerifier.GetReference25_3_48());
			List<string> listActual=result.ListDifferences.Select(x => x.Kind+" "+x.Table+"."+x.Item+" "+(x.Kind==SchemaDifferenceKind.MissingTable ? "" : x.Expected))
				.OrderBy(x => x,StringComparer.Ordinal).ToList();
			List<string> listExpected=ArrayExpectedChanges.Select(x => x[0]+" "+x[1]+"."+x[2]+" "+(x[3]??""))
				.OrderBy(x => x,StringComparer.Ordinal).ToList();
			CollectionAssert.AreEqual(listExpected,listActual,"Actual:\r\n"+string.Join("\r\n",listActual));
			//Summary counts of the spec (docs/specs/schema-changes-24.3-to-26.1.md): 5 tables, 28 columns, 4 type changes plus the drift fix.
			Assert.AreEqual(5,result.ListDifferences.Count(x => x.Kind==SchemaDifferenceKind.MissingTable));
			Assert.AreEqual(28,result.ListDifferences.Count(x => x.Kind==SchemaDifferenceKind.MissingColumn));
			Assert.AreEqual(5,result.ListDifferences.Count(x => x.Kind==SchemaDifferenceKind.ColumnDefinition));
		}

		///<summary>New columns are appended where the published documents put them, after the column named here.</summary>
		[TestMethod]
		public void Reference_NewColumnsSitWhereTheDocumentsPutThem() {
			SchemaSnapshot reference=SchemaVerifier.GetReference26_1_70();
			string[][] arrayAfter=new string[][] {
				new[] { "carrier","PaySuiteTransSup","OrthoInsPayConsolidate" },new[] { "carrier","PreAuthCodes","PaySuiteTransSup" },
				new[] { "claimproc","Etrans835AttachNum","SecurityHash" },new[] { "codegroup","ShowInHistory","ShowInOther" },
				new[] { "codegroup","HistProcCode","ShowInHistory" },new[] { "codegroup","IsPerioFourQuads","HistProcCode" },
				new[] { "procnote","UserNum2","Signature" },new[] { "procnote","Signature2","UserNum2" },new[] { "procnote","SigIsTopaz2","Signature2" },
				new[] { "procedurelog","NoBillIns","DiscountPlanAmt" },new[] { "statement","IsServiceDateView","ShowTransSinceBalZero" },
				new[] { "eformfield","DateTimeSig","PickListVisEnglish" },new[] { "sheet","DateTimeSubmitted","WebFormSheetID" },
			};
			foreach(string[] array in arrayAfter) {
				SchemaTable table=reference.GetTable(array[0]);
				Assert.AreEqual(table.GetColumn(array[2]).Position+1,table.GetColumn(array[1]).Position,array[0]+"."+array[1]);
			}
			//Every new column of an existing table is at the end of it.
			SchemaSnapshot reference25=SchemaVerifier.GetReference25_3_48();
			foreach(SchemaTable table in reference.Tables.Where(x => reference25.HasTable(x.Name))) {
				SchemaTable table25=reference25.GetTable(table.Name);
				for(int i=0;i<table25.Columns.Count;i++) {
					Assert.AreEqual(table25.Columns[i].Name,table.Columns[i].Name,table.Name+" column "+(i+1));
				}
			}
		}

		///<summary>Every table that differs from 25.3.48 has a comment right above it that names each change and says what is inferred.</summary>
		[TestMethod]
		public void Reference_CommentsMarkWhatIsInferred() {
			string ddl=SchemaVerifier.ReadReferenceDdl(SchemaVerifier.ReferenceResourceName26_1_70).Replace("\r\n","\n");
			string[] arrayLines=ddl.Split('\n');
			StringAssert.Contains(ddl,"INFERRED");
			foreach(string[] change in ArrayExpectedChanges) {
				string table=change[1];
				int indexCreate=Array.FindIndex(arrayLines,x => x.StartsWith("CREATE TABLE `"+table+"` ("));
				Assert.IsTrue(indexCreate>0,table);
				List<string> listComment=new List<string>();
				for(int i=indexCreate-1;i>=0 && arrayLines[i].StartsWith("--");i--) {
					listComment.Insert(0,arrayLines[i]);
				}
				string comment=string.Join(" ",listComment);
				StringAssert.Contains(comment,"INFERRED",table);
				if(change[2]!=null && change[0]!="MissingIndex") {
					StringAssert.Contains(comment,change[2],table+": the comment names "+change[2]);
				}
				if(change[0]=="MissingIndex") {
					StringAssert.Contains(comment,"INFERRED key `"+change[2]+"`",table);
				}
			}
			//Unchanged tables carry no comment.
			int indexAccount=Array.FindIndex(arrayLines,x => x.StartsWith("CREATE TABLE `account` ("));
			Assert.IsFalse(arrayLines[indexAccount-1].StartsWith("--") && arrayLines[indexAccount-1].Contains("new column"));
		}

		[TestMethod]
		public void Steps_UseTheAssignedLevelsAndRunAfterThe25_3Steps() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			ForkMigrationStep step25_4=listSteps.Single(x => x is OdParity25_4Schema);
			ForkMigrationStep step26_1=listSteps.Single(x => x is OdParity26_1Schema);
			ForkMigrationStep stepNote=listSteps.Single(x => x is OdParity26_1ClaimPaymentNote);
			Assert.AreEqual(2026102401,step25_4.Level);
			Assert.AreEqual(2026102402,step26_1.Level);
			Assert.AreEqual(2026102403,stepNote.Level);
			//M2-01's range is 2026102401 to 2026102449.
			Assert.IsTrue(listSteps.Where(x => x.GetType().Assembly==typeof(OdParity26_1).Assembly
				&& x.GetType().Namespace==typeof(OdParity26_1).Namespace && x.Level>=2026102401 && x.Level<=2026102449)
				.All(x => x is OdParity25_4Schema || x is OdParity26_1Schema || x is OdParity26_1ClaimPaymentNote));
			long levelLast25_3=new long[] { new OdParity25_3ColumnDefinitions().Level,new OdParity25_3Keys().Level,new OdParity25_3Schema().Level }.Max();
			Assert.IsTrue(step25_4.Level>levelLast25_3 && step26_1.Level>step25_4.Level && stepNote.Level>step26_1.Level);
		}

		///<summary>The allowlist stays as M1 left it: an office database is adopted at 25.3.48 (or our own 24.3.63) and then runs the M1 and M2 steps.
		///A database at any other Open Dental version, 26.1 included, is not adopted.</summary>
		[TestMethod]
		public void Adoption_AllowlistIsUnchanged() {
			CollectionAssert.AreEquivalent(new[] { new Version(24,3,63,0),new Version(25,3,48,0) },ForkVersions.ListAdoptableVersions.ToList());
			Assert.IsFalse(ForkVersions.IsAdoptable(new Version(26,1,70,0)));
		}

		///<summary>Every table class has a table in the 26.1.70 reference with every field as a column, and for the tables M2-01 changed every
		///reference column is mapped, so nothing Open Dental 26.1 writes is lost when the program updates a row.</summary>
		[TestMethod]
		public void TableClasses_MapThe26_1Tables() {
			SchemaSnapshot reference=SchemaVerifier.GetReference26_1_70();
			Dictionary<string,Type> dictTypes=GetTableTypes().ToDictionary(x => CrudTableAttribute.GetTableName(x).ToLowerInvariant(),x => x);
			List<string> listProblems=new List<string>();
			foreach(string tableName in ArrayChangedTables) {
				if(!dictTypes.TryGetValue(tableName,out Type type)) {
					listProblems.Add("No table class for "+tableName);
					continue;
				}
				SchemaTable table=reference.GetTable(tableName);
				HashSet<string> setFields=new HashSet<string>(GetDbFields(type).Select(x => x.Name),StringComparer.OrdinalIgnoreCase);
				foreach(SchemaColumn column in table.Columns) {
					if(!setFields.Contains(column.Name)) {
						listProblems.Add(type.Name+" does not map "+tableName+"."+column.Name);
					}
				}
				foreach(string field in setFields) {
					if(table.GetColumn(field)==null) {
						listProblems.Add(type.Name+"."+field+" has no column in "+tableName);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		///<summary>CRUD special types follow the column types: datetime columns are DateT (time kept), text columns IsText.</summary>
		[TestMethod]
		public void TableClasses_SpecialTypesFollowTheColumnTypes() {
			SchemaSnapshot reference=SchemaVerifier.GetReference26_1_70();
			Dictionary<string,Type> dictTypes=GetTableTypes().ToDictionary(x => CrudTableAttribute.GetTableName(x).ToLowerInvariant(),x => x);
			List<string> listProblems=new List<string>();
			foreach(string[] change in ArrayExpectedChanges.Where(x => x[0]=="MissingColumn" || x[0]=="ColumnDefinition")) {
				FieldInfo field=GetDbFields(dictTypes[change[1]]).Single(x => x.Name==change[2]);
				CrudSpecialColType specialType=field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>()
					.FirstOrDefault()?.SpecialType??CrudSpecialColType.None;
				if(change[3].StartsWith("datetime") && !specialType.HasFlag(CrudSpecialColType.DateT)) {
					listProblems.Add(change[1]+"."+change[2]+" is datetime but not DateT");
				}
				if(Regex.IsMatch(change[3],"^(medium)?text") && !specialType.HasFlag(CrudSpecialColType.IsText)) {
					listProblems.Add(change[1]+"."+change[2]+" is text but not IsText");
				}
			}
			foreach(string table in new[] { "deletedobj","paysuitepaymentdetail" }) {
				foreach(SchemaColumn column in reference.GetTable(table).Columns.Where(x => x.Definition.StartsWith("mediumtext"))) {
					FieldInfo field=GetDbFields(dictTypes[table]).Single(x => x.Name==column.Name);
					if(!(field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>().FirstOrDefault()?.SpecialType
						??CrudSpecialColType.None).HasFlag(CrudSpecialColType.IsText))
					{
						listProblems.Add(table+"."+column.Name+" is mediumtext but not IsText");
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void Enums_HaveThePublished26_1Numbers() {
			Assert.AreEqual(56,(int)DefCat.EmployeeTitles);
			Assert.AreEqual(55,(int)DefCat.OperatoryTypes,"The categories before it keep their numbers.");
			Assert.AreEqual(57,Enum.GetValues(typeof(DefCat)).Length,"Contiguous 0 to 56: the definition cache indexes its arrays by category.");
			CollectionAssert.AreEqual(new[] { "None","LogoImage","MaterialColorPalette","OfficeDescription" },Enum.GetNames(typeof(EnumBrandingType)));
			CollectionAssert.AreEqual(new[] { 0,1,2,3 },Enum.GetValues(typeof(EnumBrandingType)).Cast<int>().ToArray());
			CollectionAssert.AreEqual(new[] { "None","Schedules","LabCases","MedicationPats","PatFields","ToothInitials" },
				Enum.GetNames(typeof(EnumDeletedObjType)));
			CollectionAssert.AreEqual(new[] { 0,1,2,3,4,5 },Enum.GetValues(typeof(EnumDeletedObjType)).Cast<int>().ToArray());
			CollectionAssert.AreEqual(new[] { "None","ExtendedReversal","PlanDetails","Both" },Enum.GetNames(typeof(EnumPaySuiteTransTypes)));
			CollectionAssert.AreEqual(new[] { 0,1,2,3 },Enum.GetValues(typeof(EnumPaySuiteTransTypes)).Cast<int>().ToArray());
			CollectionAssert.AreEqual(new[] { "DoNotProcess","ReadyToProcess","MultiClaimMatches","DiscrepanciesFound","NoDiscrepanciesFound" },
				Enum.GetNames(typeof(EnumReconciliationStatus)));
			CollectionAssert.AreEqual(new[] { 0,1,2,3,4 },Enum.GetValues(typeof(EnumReconciliationStatus)).Cast<int>().ToArray());
			Assert.AreEqual(0,(int)EmailType.Regular);
			Assert.AreEqual(1,(int)EmailType.Html);
			Assert.AreEqual(2,(int)EmailType.RawHtml);
			Assert.AreEqual(0,(int)TrustedEtransTypes.None);
			Assert.AreEqual(1,(int)TrustedEtransTypes.RealTimeEligibility);
			//statement.StatementType holds the name.
			Assert.AreEqual(StmtType.Walkout,(StmtType)Enum.Parse(typeof(StmtType),"Walkout"));
			Assert.AreEqual("NotSet",StmtType.NotSet.ToString());
			Assert.AreEqual("LimitedStatement",StmtType.LimitedStatement.ToString());
		}

		[TestMethod]
		public void Defaults_OfTheNewFields() {
			Assert.AreEqual(-1f,new Evaluation().GradeOverride,"-1 is 'no override'.");
			Assert.AreEqual(-1f,new SchoolCourseEnrollee().GradeNumber);
			Assert.AreEqual(-1f,new SchoolCourseEnrollee().GradeOverride);
			Assert.AreEqual("",new Def().Supp);
			Assert.AreEqual("",new Carrier().PreAuthCodes);
			Assert.AreEqual("",new CodeGroup().HistProcCode);
			Assert.AreEqual("",new ProcNote().Signature2);
			Assert.AreEqual("",new InsEditLog().OldValueBig);
			Assert.AreEqual("",new InsEditPatLog().NewValueBig);
			Assert.AreEqual("",new PayTerminal().CCIntegration);
			Assert.AreEqual("",new DeletedObj().ObjSerialized);
			Assert.AreEqual("",new PaySuitePaymentDetail().DetailsJson);
		}

		#region Published documents (optional)
		///<summary>The folder holding Open Dental's published database documents (OpenDentalDocumentation26-1.xml, OpenDentalDiffDocumentation25-4.xml,
		///OpenDentalDiffDocumentation26-1.xml), or null. They are read as data only.</summary>
		private static string GetDocsDir() {
			string dir=Environment.GetEnvironmentVariable("M201_OD_DOCS_DIR");
			if(string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) {
				return null;
			}
			return dir;
		}

		private static XDocument LoadDoc(string fileName) {
			string dir=GetDocsDir();
			string path=dir==null ? null : Path.Combine(dir,fileName);
			if(path==null || !File.Exists(path)) {
				Assert.Inconclusive("Set M201_OD_DOCS_DIR to a folder with "+fileName+" (Open Dental's published database document) to run this check.");
			}
			return XDocument.Load(path);
		}

		///<summary>A column type the way Open Dental's documentation tool prints it (DocumentationBuilder/Form1.cs, GetXElementForColumn).</summary>
		public static string ToDocType(string definition) {
			Match match=Regex.Match(definition,@"^([a-z]+(?:\([^)]*\))?(?: unsigned)?)");
			string sqlType=match.Groups[1].Value;
			if(sqlType=="tinyint(3) unsigned" || sqlType=="tinyint(1) unsigned") {
				return "tinyint";
			}
			if(sqlType=="smallint(5) unsigned") {
				return "smallint";
			}
			if(sqlType=="mediumint(8) unsigned") {
				return "mediumint";
			}
			if(sqlType.EndsWith(" unsigned")) {
				return sqlType.Substring(0,sqlType.Length-" unsigned".Length);
			}
			return sqlType;
		}

		///<summary>The full 26.1 document lists the same 427 tables, each with the same columns in the same order and of the same type.</summary>
		[TestMethod]
		public void Reference_MatchesThePublished26_1Document() {
			XDocument doc=LoadDoc("OpenDentalDocumentation26-1.xml");
			SchemaSnapshot reference=SchemaVerifier.GetReference26_1_70();
			List<XElement> listTables=doc.Root.Elements("table").ToList();
			Assert.AreEqual(427,listTables.Count);
			List<string> listProblems=new List<string>();
			foreach(XElement elementTable in listTables) {
				string tableName=elementTable.Attribute("name").Value;
				SchemaTable table=reference.GetTable(tableName);
				if(table==null) {
					listProblems.Add("No table "+tableName);
					continue;
				}
				List<XElement> listColumns=elementTable.Elements("column").OrderBy(x => int.Parse(x.Attribute("order").Value)).ToList();
				if(listColumns.Count!=table.Columns.Count) {
					listProblems.Add(tableName+": "+listColumns.Count+" columns published, "+table.Columns.Count+" in the reference");
					continue;
				}
				for(int i=0;i<listColumns.Count;i++) {
					string name=listColumns[i].Attribute("name").Value;
					string type=listColumns[i].Attribute("type").Value;
					if(!string.Equals(name,table.Columns[i].Name,StringComparison.Ordinal)) {
						listProblems.Add(tableName+" column "+(i+1)+": published "+name+", reference "+table.Columns[i].Name);
						continue;
					}
					if(type!=ToDocType(table.Columns[i].Definition)) {
						listProblems.Add(tableName+"."+name+": published "+type+", reference "+table.Columns[i].Definition);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		///<summary>Every column the 25.4 and 26.1 diff documents add is in the 26.1.70 reference with that type, and every retyped column changed from
		///the old published type (in 25.3.48) to the new one.</summary>
		[TestMethod]
		public void Reference_HasEveryChangeOfThePublishedDiffDocuments() {
			XDocument doc25_4=LoadDoc("OpenDentalDiffDocumentation25-4.xml");
			XDocument doc26_1=LoadDoc("OpenDentalDiffDocumentation26-1.xml");
			SchemaSnapshot reference25=SchemaVerifier.GetReference25_3_48();
			SchemaSnapshot reference26=SchemaVerifier.GetReference26_1_70();
			List<string> listProblems=new List<string>();
			int countAdded=0;
			int countRetyped=0;
			foreach(XDocument doc in new[] { doc25_4,doc26_1 }) {
				Dictionary<string,string> dictDeleted=doc.Root.Elements("tableDeletion")
					.SelectMany(x => x.Elements("column").Select(y => new { Key=x.Attribute("name").Value+"."+y.Attribute("name").Value,Type=y.Attribute("type").Value }))
					.ToDictionary(x => x.Key,x => x.Type,StringComparer.OrdinalIgnoreCase);
				foreach(XElement elementTable in doc.Root.Elements("tableAddition")) {
					string tableName=elementTable.Attribute("name").Value;
					foreach(XElement elementColumn in elementTable.Elements("column")) {
						string columnName=elementColumn.Attribute("name").Value;
						string type=elementColumn.Attribute("type").Value;
						SchemaColumn column26=reference26.GetTable(tableName)?.GetColumn(columnName);
						if(column26==null) {
							listProblems.Add("Missing "+tableName+"."+columnName);
							continue;
						}
						SchemaColumn column25=reference25.GetTable(tableName)?.GetColumn(columnName);
						if(column25==null) {
							countAdded++;
							if(ToDocType(column26.Definition)!=type) {
								listProblems.Add(tableName+"."+columnName+": published "+type+", reference "+column26.Definition);
							}
							if(column26.Position!=int.Parse(elementColumn.Attribute("order").Value)+1) {
								listProblems.Add(tableName+"."+columnName+": published at "+elementColumn.Attribute("order").Value+", reference at "
									+(column26.Position-1));
							}
							continue;
						}
						if(dictDeleted.TryGetValue(tableName+"."+columnName,out string typeOld)) {
							countRetyped++;
							if(ToDocType(column25.Definition)!=typeOld || ToDocType(column26.Definition)!=type) {
								listProblems.Add(tableName+"."+columnName+": published "+typeOld+" to "+type+", references "+column25.Definition+" to "
									+column26.Definition);
							}
							continue;
						}
						//Listed for an enum change only: the column is unchanged.
						if(column25.Definition!=column26.Definition) {
							listProblems.Add(tableName+"."+columnName+" changed although the documents list only an enum change");
						}
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			//28 new columns of existing tables, plus every column of the 5 new tables: paysuitepayment 14, paysuitepaymentdetail 2,
			//schoolcourseenrollee 5, schoolcourseinstructor 3, deletedobj 4.
			Assert.AreEqual(28+14+2+5+3+4,countAdded);
			Assert.AreEqual(4,countRetyped,"insverify, insverifyhist, referral and branding.");
		}
		#endregion Published documents (optional)

		public static IEnumerable<Type> GetTableTypes() {
			Type typeTableBase=typeof(TableBase);
			return typeTableBase.Assembly.GetTypes()
				.Where(x => x.IsSubclassOf(typeTableBase) && !x.IsAbstract && !x.Name.StartsWith("WebForms_"))
				.OrderBy(x => x.Name);
		}

		public static IEnumerable<FieldInfo> GetDbFields(Type type) {
			foreach(FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
				CrudColumnAttribute crudColumn=field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>().FirstOrDefault();
				if(crudColumn!=null && crudColumn.IsNotDbColumn) {
					continue;
				}
				yield return field;
			}
		}
	}
}
