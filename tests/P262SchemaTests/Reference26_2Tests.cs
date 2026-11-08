// Tests of the Open Dental 26.2.0 reference schema (OpenPmsDentalBusiness/Misc/ForkMigrations/Reference/OdSchema-26.2.0.sql), the 26.2 step's level,
// the table classes that map the new and changed tables and the 26.2 enum values. No database. The checks against Open Dental's published
// documents run only when the documents are in the folder named by P262_OD_DOCS_DIR (they are not in the repository); otherwise they are
// inconclusive.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace P262SchemaTests {
	[TestClass]
	public class Reference26_2Tests {
		///<summary>What the 26.2.0 reference adds to the 26.1.70 reference, written out independently of the reference file and the step: kind, table,
		///column or key, and the expected definition. From the published 26.2 diff document (names, order, types) and the inferred rest (NOT NULL,
		///defaults, keys).</summary>
		public static readonly string[][] ArrayExpectedChanges=new string[][] {
			new[] { "MissingTable","glidewellacct",null,null },
			new[] { "MissingTable","glidewellmsg",null,null },
			new[] { "MissingTable","labcaseproduct",null,null },
			new[] { "MissingColumn","apisubscription","DateTimeLastFailure","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'" },
			new[] { "MissingColumn","apisubscription","FailureReason","text NOT NULL" },
			new[] { "MissingColumn","apisubscription","SubsequentFailures","int(11) NOT NULL" },
			new[] { "MissingColumn","apisubscription","DateTimeNextRetry","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'" },
			new[] { "MissingColumn","insbluebook","DateTStamp","timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()" },
			new[] { "MissingColumn","provider","ClaimFormNum","bigint(20) NOT NULL" },
			new[] { "MissingColumn","provider","GlidewellAcctNum","bigint(20) NOT NULL" },
			new[] { "MissingIndex","provider","ClaimFormNum","KEY (`ClaimFormNum`)" },
			new[] { "MissingIndex","provider","GlidewellAcctNum","KEY (`GlidewellAcctNum`)" },
		};

		///<summary>The DDL of the three new tables, written out independently of the reference file and the step.</summary>
		public static readonly Dictionary<string,string[]> DictNewTables=new Dictionary<string,string[]> {
			{ "glidewellacct",new[] { "`GlidewellAcctNum` bigint(20) NOT NULL AUTO_INCREMENT","`AccountNumber` varchar(255) NOT NULL",
				"`ApiKey` varchar(255) NOT NULL","`DateTimeExpires` datetime NOT NULL DEFAULT '0001-01-01 00:00:00'","PRIMARY KEY (`GlidewellAcctNum`)" } },
			{ "glidewellmsg",new[] { "`GlidewellMsgNum` bigint(20) NOT NULL AUTO_INCREMENT","`LabCaseNum` bigint(20) NOT NULL",
				"`OrderStatus` tinyint(4) NOT NULL","`VendorRxId` varchar(255) NOT NULL","`PatNum` bigint(20) NOT NULL","`GlidewellAcctNum` bigint(20) NOT NULL",
				"`RawJsonResponse` text NOT NULL","`RawJsonRequest` text NOT NULL","`SentITero` tinyint(4) NOT NULL","`SentMedit` tinyint(4) NOT NULL",
				"PRIMARY KEY (`GlidewellMsgNum`)","KEY `LabCaseNum` (`LabCaseNum`)","KEY `PatNum` (`PatNum`)","KEY `GlidewellAcctNum` (`GlidewellAcctNum`)" } },
			{ "labcaseproduct",new[] { "`LabCaseProductNum` bigint(20) NOT NULL AUTO_INCREMENT","`LabCaseNum` bigint(20) NOT NULL",
				"`PatNum` bigint(20) NOT NULL","`GlidewellProduct` tinyint(4) NOT NULL","`Material` varchar(255) NOT NULL","`Shade` varchar(255) NOT NULL",
				"`ToothRange` varchar(255) NOT NULL","`Notes` text NOT NULL","PRIMARY KEY (`LabCaseProductNum`)","KEY `LabCaseNum` (`LabCaseNum`)",
				"KEY `PatNum` (`PatNum`)" } },
		};

		///<summary>The tables whose class this package added or changed.</summary>
		public static readonly string[] ArrayChangedTables=new string[] { "apisubscription","glidewellacct","glidewellmsg","insbluebook","labcaseproduct",
			"provider" };

		///<summary>Every enum value the 26.2 diff document lists, with Open Dental's number: enum, member, number. EnumGlidewellOrderStatus has no
		///published numbers; its numbers are the published order from 0 (INFERRED).</summary>
		public static readonly (string EnumName,string Member,int Number)[] ArrayEnumValues=new[] {
			("AlertType","MariaDbFullBackupError",52),("AlertType","MariaDbIncrementalBackupError",53),("AlertType","EConnectorCritical",54),
			("EnumAutomationTrigger","DoNotBillInsuranceCheck",11),
			("AutoCondField","CarriersMatch",15),
			("DefCat","CommLogReferralTypes",57),
			("DefLinkType","User",6),
			("EnumGlidewellOrderStatus","Initialized",0),("EnumGlidewellOrderStatus","Received",1),("EnumGlidewellOrderStatus","InProgress",2),
			("EnumGlidewellOrderStatus","OnHold",3),("EnumGlidewellOrderStatus","Shipped",4),("EnumGlidewellOrderStatus","Cancelled",5),
			("EnumGlidewellOrderStatus","CreditPending",6),("EnumGlidewellOrderStatus","Credited",7),
			("EnumPermType","AdvancedTextingAccountEdit",277),
			("ToothPaintingType","TextBelow",18),
			("TsiFKeyType","PayPlanLink",6),
			("UserOdFkeyType","PerioShowCurrentProbingOnly",31),
		};

		public static Type FindEnum(string enumName) {
			return typeof(TableBase).Assembly.GetTypes().SingleOrDefault(x => x.IsEnum && x.Name==enumName && x.Namespace=="OpenPmsDentalBusiness");
		}

		[TestMethod]
		public void Reference_Has430MyIsamTablesAndIsTheDefault() {
			SchemaSnapshot reference=SchemaVerifier.GetReference26_2_0();
			Assert.AreEqual(430,reference.Tables.Count,"Open Dental 26.2 has 430 tables: the 427 of 26.1 and 3 new ones.");
			Assert.IsTrue(reference.Tables.All(x => x.Engine=="MyISAM" && x.Charset=="utf8mb3" && x.Collation=="utf8mb3_general_ci"));
			Assert.AreSame(reference,SchemaVerifier.GetReference());
			Assert.AreEqual("26.2.0",SchemaVerifier.ReferenceVersion);
			Assert.AreEqual(SchemaVerifier.ReferenceResourceName26_2_0,SchemaVerifier.ReferenceResourceName);
			StringAssert.StartsWith(SchemaVerifier.ReadReferenceDdl(),"-- Open Dental 26.2.0 database schema");
			Assert.AreEqual(427,SchemaVerifier.GetReference26_1_70().Tables.Count,"The 26.1.70 reference is still there, unchanged.");
			Assert.AreEqual(422,SchemaVerifier.GetReference25_3_48().Tables.Count,"The 25.3.48 reference is still there, unchanged.");
			StringAssert.DoesNotMatch(SchemaVerifier.ReadReferenceDdl(),new Regex("AUTO_INCREMENT=[0-9]"));
			foreach(string table in SchemaVerifier.ArrayHqOnlyTables.Concat(SchemaVerifier.ArrayLegacyTables).Concat(ForkTables.GetTableNames())) {
				Assert.IsFalse(reference.HasTable(table),table+" is not in Open Dental 26.2.");
			}
			//The documented exceptions are those of 26.1.70: 26.2 drops no table.
			CollectionAssert.AreEqual(SchemaVerifier.DocumentedExceptions26_1_70.Select(x => x.Kind+" "+x.Table+" "+x.Item).ToList(),
				SchemaVerifier.DocumentedExceptions.Select(x => x.Kind+" "+x.Table+" "+x.Item).ToList());
		}

		[TestMethod]
		public void Reference_DiffersFrom26_1_70ByExactlyThePublishedChanges() {
			SchemaVerifyResult result=SchemaVerifier.Compare(SchemaVerifier.GetReference26_2_0(),SchemaVerifier.GetReference26_1_70());
			List<string> listActual=result.ListDifferences.Select(x => x.Kind+" "+x.Table+"."+x.Item+" "+(x.Kind==SchemaDifferenceKind.MissingTable ? "" : x.Expected))
				.OrderBy(x => x,StringComparer.Ordinal).ToList();
			List<string> listExpected=ArrayExpectedChanges.Select(x => x[0]+" "+x[1]+"."+x[2]+" "+(x[3]??""))
				.OrderBy(x => x,StringComparer.Ordinal).ToList();
			CollectionAssert.AreEqual(listExpected,listActual,"Actual:\r\n"+string.Join("\r\n",listActual));
			//No column of 26.1 changed type, and no table or column was dropped.
			Assert.AreEqual(0,result.ListDifferences.Count(x => x.Kind==SchemaDifferenceKind.ColumnDefinition || x.Kind==SchemaDifferenceKind.ExtraColumn
				|| x.Kind==SchemaDifferenceKind.ExtraTable || x.Kind==SchemaDifferenceKind.ColumnOrder));
		}

		[TestMethod]
		public void Reference_NewTablesAreExactlyAsInferred() {
			SchemaSnapshot reference=SchemaVerifier.GetReference26_2_0();
			foreach(KeyValuePair<string,string[]> pair in DictNewTables) {
				string ddl=reference.GetTable(pair.Key).ToDdl().Replace("\r\n","\n");
				string expected="CREATE TABLE `"+pair.Key+"` (\n  "+string.Join(",\n  ",pair.Value)+"\n) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";
				Assert.AreEqual(expected,ddl.TrimEnd(';','\n'),pair.Key);
			}
		}

		///<summary>New columns are appended to their tables in the published order; every column of 26.1.70 keeps its position.</summary>
		[TestMethod]
		public void Reference_NewColumnsSitAtTheEndInThePublishedOrder() {
			SchemaSnapshot reference=SchemaVerifier.GetReference26_2_0();
			SchemaSnapshot reference26_1=SchemaVerifier.GetReference26_1_70();
			foreach(SchemaTable table26_1 in reference26_1.Tables) {
				SchemaTable table=reference.GetTable(table26_1.Name);
				for(int i=0;i<table26_1.Columns.Count;i++) {
					Assert.AreEqual(table26_1.Columns[i].Name,table.Columns[i].Name,table.Name+" column "+(i+1));
					Assert.AreEqual(table26_1.Columns[i].Definition,table.Columns[i].Definition,table.Name+"."+table.Columns[i].Name);
				}
			}
			CollectionAssert.AreEqual(new[] { "DateTimeLastFailure","FailureReason","SubsequentFailures","DateTimeNextRetry" },
				reference.GetTable("apisubscription").Columns.Skip(10).Select(x => x.Name).ToArray());
			Assert.AreEqual(13,reference.GetTable("insbluebook").GetColumn("DateTStamp").Position,"Published at order 12, counted from 0.");
			Assert.AreEqual(47,reference.GetTable("provider").GetColumn("ClaimFormNum").Position,"Published at order 46.");
			Assert.AreEqual(48,reference.GetTable("provider").GetColumn("GlidewellAcctNum").Position,"Published at order 47.");
		}

		///<summary>Every table 26.2 changes or adds has a comment right above it that names each change and says what is inferred; the comments of the
		///25.4 and 26.1 changes are kept.</summary>
		[TestMethod]
		public void Reference_CommentsMarkWhatIsInferred() {
			string ddl=SchemaVerifier.ReadReferenceDdl(SchemaVerifier.ReferenceResourceName26_2_0).Replace("\r\n","\n");
			string[] arrayLines=ddl.Split('\n');
			foreach(string[] change in ArrayExpectedChanges) {
				string comment=GetCommentAbove(arrayLines,change[1]);
				StringAssert.Contains(comment,"26.2:",change[1]);
				StringAssert.Contains(comment,"INFERRED",change[1]);
				if(change[0]=="MissingColumn") {
					StringAssert.Contains(comment,change[2],change[1]+": the comment names "+change[2]);
				}
				if(change[0]=="MissingIndex") {
					StringAssert.Contains(comment,"INFERRED key `"+change[2]+"`",change[1]);
				}
			}
			StringAssert.Contains(GetCommentAbove(arrayLines,"glidewellmsg"),"INFERRED key `GlidewellAcctNum`");
			StringAssert.Contains(GetCommentAbove(arrayLines,"labcaseproduct"),"INFERRED key `LabCaseNum`");
			//The 26.1 comments stay (for example the new 26.1 table deletedobj).
			StringAssert.Contains(GetCommentAbove(arrayLines,"deletedobj"),"26.1: new table");
			//The 26.1.70 file and this one differ only in the header and the 26.2 tables.
			string ddl26_1=SchemaVerifier.ReadReferenceDdl(SchemaVerifier.ReferenceResourceName26_1_70).Replace("\r\n","\n");
			Assert.AreEqual(GetCommentAbove(ddl26_1.Split('\n'),"smstomobile"),GetCommentAbove(arrayLines,"smstomobile"));
		}

		private static string GetCommentAbove(string[] arrayLines,string table) {
			int indexCreate=Array.FindIndex(arrayLines,x => x.StartsWith("CREATE TABLE `"+table+"` ("));
			Assert.IsTrue(indexCreate>0,table);
			List<string> listComment=new List<string>();
			for(int i=indexCreate-1;i>=0 && arrayLines[i].StartsWith("--");i--) {
				listComment.Insert(0,arrayLines[i]);
			}
			return string.Join(" ",listComment);
		}

		[TestMethod]
		public void Step_UsesThePackageRangeAndRunsAfterThe26_1Steps() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			ForkMigrationStep step=listSteps.Single(x => x is OdParity26_2Schema);
			Assert.AreEqual(2026110701,step.Level);
			//This package's range is 2026110701 to 2026110709, and only its own step uses it.
			Assert.IsTrue(listSteps.Where(x => x.Level>=2026110701 && x.Level<=2026110709).All(x => x is OdParity26_2Schema));
			long levelLast26_1=new long[] { new OdParity25_4Schema().Level,new OdParity26_1Schema().Level,new OdParity26_1ClaimPaymentNote().Level }.Max();
			Assert.IsTrue(step.Level>levelLast26_1);
			Assert.IsTrue(listSteps.IndexOf(step)>listSteps.IndexOf(listSteps.Single(x => x is OdParity26_1ClaimPaymentNote)),
				"The runner runs the 26.2 step after the 26.1 steps.");
			Assert.AreEqual("Open Dental 26.2 schema",step.Name);
		}

		///<summary>Every table class of the new and changed tables maps every column of the 26.2.0 reference, and every field is a column, so nothing
		///Open Dental 26.2 writes is lost when the program updates a row. CRUD special types follow the column types.</summary>
		[TestMethod]
		public void TableClasses_MapThe26_2Tables() {
			SchemaSnapshot reference=SchemaVerifier.GetReference26_2_0();
			Dictionary<string,Type> dictTypes=GetTableTypes().ToDictionary(x => CrudTableAttribute.GetTableName(x).ToLowerInvariant(),x => x);
			List<string> listProblems=new List<string>();
			foreach(string tableName in ArrayChangedTables) {
				if(!dictTypes.TryGetValue(tableName,out Type type)) {
					listProblems.Add("No table class for "+tableName);
					continue;
				}
				SchemaTable table=reference.GetTable(tableName);
				List<FieldInfo> listFields=GetDbFields(type).ToList();
				CollectionAssert.AreEquivalent(table.Columns.Select(x => x.Name).ToList(),listFields.Select(x => x.Name).ToList(),
					type.Name+": the fields are exactly the columns.");
				if(DictNewTables.ContainsKey(tableName)) {
					CollectionAssert.AreEqual(table.Columns.Select(x => x.Name).ToList(),listFields.Select(x => x.Name).ToList(),
						type.Name+": the fields of a new table are declared in the table's order.");
				}
				foreach(FieldInfo field in listFields) {
					string definition=table.GetColumn(field.Name).Definition;
					CrudSpecialColType specialType=GetSpecialType(field);
					if(definition.StartsWith("datetime") && !specialType.HasFlag(CrudSpecialColType.DateT) && !specialType.HasFlag(CrudSpecialColType.DateTEntry)) {
						listProblems.Add(tableName+"."+field.Name+" is datetime but not DateT");
					}
					if(definition.StartsWith("timestamp") && !specialType.HasFlag(CrudSpecialColType.TimeStamp)) {
						listProblems.Add(tableName+"."+field.Name+" is timestamp but not TimeStamp");
					}
					if(Regex.IsMatch(definition,"^(medium)?text") && !specialType.HasFlag(CrudSpecialColType.IsText)) {
						listProblems.Add(tableName+"."+field.Name+" is text but not IsText");
					}
					if(DictNewTables.ContainsKey(tableName) && definition.StartsWith("tinyint(4)") && field.FieldType!=typeof(bool) && !field.FieldType.IsEnum) {
						listProblems.Add(tableName+"."+field.Name+" is tinyint(4) but neither bool nor an enum");
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			Assert.AreEqual(typeof(EnumGlidewellOrderStatus),typeof(GlidewellMsg).GetField(nameof(GlidewellMsg.OrderStatus)).FieldType);
			Assert.AreEqual(typeof(EnumGlidewellProductName),typeof(LabCaseProduct).GetField(nameof(LabCaseProduct.GlidewellProduct)).FieldType);
		}

		[TestMethod]
		public void TableClasses_NewObjectsHoldTheColumnDefaults() {
			Assert.AreEqual("",new ApiSubscription().FailureReason);
			Assert.AreEqual(DateTime.MinValue,new ApiSubscription().DateTimeNextRetry);
			Assert.AreEqual(0,new Provider().ClaimFormNum);
			Assert.AreEqual(0,new Provider().GlidewellAcctNum);
			GlidewellAcct glidewellAcct=new GlidewellAcct();
			Assert.AreEqual("",glidewellAcct.AccountNumber);
			Assert.AreEqual("",glidewellAcct.ApiKey);
			GlidewellMsg glidewellMsg=new GlidewellMsg();
			Assert.AreEqual(EnumGlidewellOrderStatus.Initialized,glidewellMsg.OrderStatus);
			Assert.AreEqual("",glidewellMsg.VendorRxId+glidewellMsg.RawJsonRequest+glidewellMsg.RawJsonResponse);
			LabCaseProduct labCaseProduct=new LabCaseProduct();
			Assert.AreEqual("",labCaseProduct.Material+labCaseProduct.Shade+labCaseProduct.ToothRange+labCaseProduct.Notes);
			Assert.AreEqual(0,(int)labCaseProduct.GlidewellProduct);
		}

		///<summary>Every 26.2 enum value with Open Dental's number; the number maps back to that member only.</summary>
		[TestMethod]
		public void Enums_HaveThePublished26_2Numbers() {
			List<string> listProblems=new List<string>();
			foreach((string enumName,string member,int number) in ArrayEnumValues) {
				Type type=FindEnum(enumName);
				if(type==null) {
					listProblems.Add(enumName+" not found");
					continue;
				}
				if(!Enum.GetNames(type).Contains(member)) {
					listProblems.Add(enumName+"."+member+" missing");
					continue;
				}
				int value=Convert.ToInt32(Enum.Parse(type,member));
				if(value!=number) {
					listProblems.Add(enumName+"."+member+" is "+value+", published "+number);
				}
				if(Enum.GetName(type,Enum.ToObject(type,number))!=member) {
					listProblems.Add(enumName+" "+number+" maps to "+Enum.GetName(type,Enum.ToObject(type,number)));
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			CollectionAssert.AreEqual(new[] { "Initialized","Received","InProgress","OnHold","Shipped","Cancelled","CreditPending","Credited" },
				Enum.GetNames(typeof(EnumGlidewellOrderStatus)));
			Assert.AreEqual(0,Enum.GetValues(typeof(EnumGlidewellProductName)).Length,"Open Dental publishes no values of EnumGlidewellProductName.");
			//The members before the new ones keep their numbers.
			Assert.AreEqual(51,(int)AlertType.EServiceListenerError);
			Assert.AreEqual(276,(int)EnumPermType.LogScraperHQ);
			Assert.AreEqual(56,(int)DefCat.EmployeeTitles);
			Assert.AreEqual(29,(int)UserOdFkeyType.DefaultTestingTeamJM);
			Assert.IsFalse(Enum.IsDefined(typeof(UserOdFkeyType),30),"Open Dental's documents have no 30.");
			Assert.AreEqual(5,(int)TsiFKeyType.Procedure);
			Assert.AreEqual(17,(int)ToothPaintingType.SpaceMaintainer);
		}

		///<summary>No 26.2 value takes a fork-only value or one of the earlier values (docs/port-notes/FORK-ENUMS.md): each is below the fork-only
		///region of its column type, and the fork-only values keep their numbers.</summary>
		[TestMethod]
		public void Enums_NoCollisionWithForkOnlyValues() {
			foreach((string enumName,string member,int number) in ArrayEnumValues) {
				//EnumPermType is stored in smallint(6) columns (fork-only from 10000); every other enum here in tinyint columns (fork-only 100 to 127).
				int firstForkValue=enumName==nameof(EnumPermType) ? ForkEnums.FIRST_VALUE : ForkEnums.FIRST_VALUE_TINYINT;
				Assert.IsTrue(number<firstForkValue,enumName+"."+member+" is in the fork-only region (from "+firstForkValue+").");
				Assert.IsFalse(ForkEnums.ListEarlierValues.Any(x => x.EnumType.Name==enumName && x.Value==number),enumName+" "+number+" is an earlier fork value.");
				Type type=FindEnum(enumName);
				Assert.IsFalse(ForkEnums.IsForkOnly((Enum)Enum.ToObject(type,number)),enumName+"."+member+" counts as fork-only.");
			}
			Assert.AreEqual(127,(int)AlertType.MaintenanceDueSoon);
			Assert.AreEqual(126,(int)AlertType.MaintenanceOverdue);
			Assert.AreEqual(119,(int)AlertType.InventoryLowStock);
			Assert.AreEqual(118,(int)AlertType.InventoryExpiring);
			Assert.AreEqual(114,(int)UserOdFkeyType.PatientFormsView);
			Assert.AreEqual(10000,(int)EnumPermType.MaintenanceSetup);
			Assert.AreEqual(10100,(int)EnumPermType.SshHostKeys);
			//DefCat stays contiguous: the definition cache sizes its arrays by the number of categories and indexes them by category.
			int[] arrayDefCats=Enum.GetValues(typeof(DefCat)).Cast<int>().OrderBy(x => x).ToArray();
			CollectionAssert.AreEqual(Enumerable.Range(0,58).ToArray(),arrayDefCats);
			//AutoCondField stays contiguous: the condition window fills its list by index.
			CollectionAssert.AreEqual(Enumerable.Range(0,16).ToArray(),Enum.GetValues(typeof(AutoCondField)).Cast<int>().OrderBy(x => x).ToArray());
		}

		///<summary>The values that belong to Open Dental HQ or to services this program does not have are hidden where such values are hidden: the
		///alert types from alert category setup, the HQ permission from the security tree and the audit trail filter.</summary>
		[TestMethod]
		public void Enums_HqAndServiceValuesAreReserved() {
			foreach(AlertType alertType in new[] { AlertType.MariaDbFullBackupError,AlertType.MariaDbIncrementalBackupError,AlertType.EConnectorCritical }) {
				Assert.IsTrue(ReservedEnumValueAttribute.IsReserved(alertType),alertType.ToString());
				Assert.IsTrue(GenericTools.IsODHQ(alertType),alertType+" is hidden like the HQ values.");
				Assert.AreNotEqual(alertType.ToString(),alertType.GetDescription(),alertType+" has a description.");
			}
			Assert.IsTrue(GroupPermissions.IsReserved(EnumPermType.AdvancedTextingAccountEdit));
			Assert.IsFalse(GroupPermissions.HasAuditTrail(EnumPermType.AdvancedTextingAccountEdit));
			Assert.AreEqual("Advanced Texting Account Edit",EnumPermType.AdvancedTextingAccountEdit.GetDescription());
			Assert.IsTrue(ReservedEnumValueAttribute.IsReserved(DefLinkType.User));
			//The values the 26.2 feature packages use are not reserved.
			Assert.IsFalse(GenericTools.IsODHQ(DefCat.CommLogReferralTypes));
			Assert.AreEqual("Commlog Referral Types",DefCat.CommLogReferralTypes.GetDescription());
			Assert.IsFalse(GenericTools.IsODHQ(UserOdFkeyType.PerioShowCurrentProbingOnly));
		}

		#region Published documents (optional)
		///<summary>The full 26.2 document lists the same 430 tables as the reference, each with the same columns in the same order and of the same type.
		///</summary>
		[TestMethod]
		public void Reference_MatchesThePublished26_2Document() {
			XDocument doc=OdDocs.Load(OdDocs.FULL_26_2);
			Assert.AreEqual("26.2.0.0",doc.Root.Attribute("version").Value);
			Assert.AreEqual(430,doc.Root.Elements("table").Count());
			List<string> listProblems=OdDocs.CompareWithFullDocument(doc,SchemaVerifier.GetReference26_2_0());
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		///<summary>Every column the 26.2 diff document adds is in the 26.2.0 reference with that type and position and is not in 26.1.70; every column it
		///lists only for an enum change is unchanged; it deletes nothing.</summary>
		[TestMethod]
		public void Reference_HasEveryChangeOfThePublished26_2DiffDocument() {
			XDocument doc=OdDocs.Load(OdDocs.DIFF_26_2);
			Assert.AreEqual("26.2.0.0",doc.Root.Attribute("version").Value);
			Assert.AreEqual(0,doc.Root.Elements("tableDeletion").Count(),"26.2 deletes no table or column.");
			SchemaSnapshot reference26_1=SchemaVerifier.GetReference26_1_70();
			SchemaSnapshot reference26_2=SchemaVerifier.GetReference26_2_0();
			List<string> listProblems=new List<string>();
			int countColumns=0;
			int countAdded=0;
			List<XElement> listTables=doc.Root.Elements("tableAddition").ToList();
			Assert.AreEqual(17,listTables.Count,"17 tables");
			foreach(XElement elementTable in listTables) {
				string tableName=elementTable.Attribute("name").Value;
				foreach(XElement elementColumn in elementTable.Elements("column")) {
					countColumns++;
					string columnName=elementColumn.Attribute("name").Value;
					string type=elementColumn.Attribute("type").Value;
					SchemaColumn column26_2=reference26_2.GetTable(tableName)?.GetColumn(columnName);
					if(column26_2==null) {
						listProblems.Add("Missing "+tableName+"."+columnName);
						continue;
					}
					if(OdDocs.ToDocType(column26_2.Definition)!=type) {
						listProblems.Add(tableName+"."+columnName+": published "+type+", reference "+column26_2.Definition);
					}
					if(column26_2.Position!=int.Parse(elementColumn.Attribute("order").Value)+1) {
						listProblems.Add(tableName+"."+columnName+": published at "+elementColumn.Attribute("order").Value+", reference at "+(column26_2.Position-1));
					}
					SchemaColumn column26_1=reference26_1.GetTable(tableName)?.GetColumn(columnName);
					if(column26_1==null) {
						countAdded++;
						continue;
					}
					//Listed for an enum change only: the column is unchanged.
					if(elementColumn.Element("Enumeration")==null) {
						listProblems.Add(tableName+"."+columnName+" exists in 26.1.70 but the document lists no enum change for it");
					}
					if(column26_1.Definition!=column26_2.Definition) {
						listProblems.Add(tableName+"."+columnName+" changed although the document lists only an enum change");
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			Assert.AreEqual(40,countColumns,"40 columns");
			//7 new columns of existing tables plus every column of the 3 new tables: glidewellacct 4, glidewellmsg 10, labcaseproduct 8.
			Assert.AreEqual(7+4+10+8,countAdded);
		}

		///<summary>Every enum value of the 26.2 diff document is in the code with its published number (EnumGlidewellOrderStatus, published without
		///numbers: its order from 0), and this test's own list is exactly the document's.</summary>
		[TestMethod]
		public void Enums_MatchThePublished26_2DiffDocument() {
			XDocument doc=OdDocs.Load(OdDocs.DIFF_26_2);
			Assert.AreEqual(23,doc.Root.Elements("tableAddition").Elements("column").Elements("Enumeration").Elements("EnumValue").Count(),"23 enum values");
			Assert.AreEqual(13,doc.Root.Elements("tableAddition").Elements("column").Elements("Enumeration").Count(),"in 13 enumerations");
			List<OdDocs.EnumValue> listValues=OdDocs.GetEnumValues(doc,"tableAddition");
			List<string> listProblems=new List<string>();
			foreach(string enumName in OdDocs.GetEnumNames(doc,"tableAddition")) {
				if(FindEnum(enumName)==null) {
					listProblems.Add("No enum "+enumName);
				}
			}
			foreach(OdDocs.EnumValue value in listValues) {
				int number=value.Number??value.Ordinal;
				if(value.Number==null && value.EnumName!="EnumGlidewellOrderStatus") {
					listProblems.Add(value.EnumName+"."+value.Member+" has no published number");
				}
				Type type=FindEnum(value.EnumName);
				if(type==null) {
					continue;
				}
				if(!Enum.GetNames(type).Contains(value.Member)) {
					listProblems.Add(value.EnumName+"."+value.Member+" missing");
					continue;
				}
				if(Convert.ToInt32(Enum.Parse(type,value.Member))!=number) {
					listProblems.Add(value.EnumName+"."+value.Member+" is "+Convert.ToInt32(Enum.Parse(type,value.Member))+", published "+number);
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			CollectionAssert.AreEquivalent(listValues.Select(x => x.EnumName+"."+x.Member+"="+(x.Number??x.Ordinal)).ToList(),
				ArrayEnumValues.Select(x => x.EnumName+"."+x.Member+"="+x.Number).ToList());
		}
		#endregion Published documents (optional)

		public static IEnumerable<Type> GetTableTypes() {
			Type typeTableBase=typeof(TableBase);
			return typeTableBase.Assembly.GetTypes()
				.Where(x => x.IsSubclassOf(typeTableBase) && !x.IsAbstract && !x.Name.StartsWith("WebForms_"))
				.OrderBy(x => x.Name);
		}

		///<summary>The public instance fields that are database columns (not IsNotDbColumn), in declaration order.</summary>
		public static IEnumerable<FieldInfo> GetDbFields(Type type) {
			foreach(FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.MetadataToken)) {
				CrudColumnAttribute crudColumn=field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>().FirstOrDefault();
				if(crudColumn!=null && crudColumn.IsNotDbColumn) {
					continue;
				}
				yield return field;
			}
		}

		public static CrudSpecialColType GetSpecialType(FieldInfo field) {
			return field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>().FirstOrDefault()?.SpecialType
				??CrudSpecialColType.None;
		}
	}
}
