using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

//The Open Dental 24.4 to 25.3.48 schema, re-implemented from Open Dental's published database documentation (the full and diff documents for 24.4,
//25.1, 25.2 and 25.3) and the schema-only dump of an Open Dental 25.3.48 database. Specs: docs/specs/schema-changes-24.3-to-26.1.md and
//docs/specs/port-backlog.md (package M1-01). Each step is idempotent: on a database made by our 24.3.63 convert scripts it brings the schema to
//25.3.48; on a database Open Dental already converted to 25.3.48 it changes nothing, and none of its data migrations run.
namespace OpenDentBusiness.ForkMigrations {
	///<summary>Open Dental 24.4: chart letter columns on document, fee effective dates, payment plan sheets, BetterDiagnostics layer (data
	///compatibility only), fee schedule notes and Query Monitor filters.</summary>
	[ForkMigration(2026101901,"Open Dental 24.4 schema")]
	public class OdParity24_4Schema:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.AddColumnIfMissing("document","ChartLetterStatus","tinyint(4) NOT NULL","PrintHeading");
			context.AddColumnIfMissing("document","UserNum","bigint(20) NOT NULL","ChartLetterStatus");
			context.AddColumnIfMissing("document","ChartLetterHash","varchar(255) NOT NULL","UserNum");
			context.AddColumnIfMissing("fee","DateEffective","date NOT NULL DEFAULT '0001-01-01'","SecDateTEdit");
			context.EnsureIndex("fee","FeeSchedCodeClinicProvDate","`FeeSched`,`CodeNum`,`ClinicNum`,`ProvNum`,`DateEffective`");
			context.AddColumnIfMissing("payplan","SheetDefNum","bigint(20) NOT NULL","SecurityHash");
			context.AddColumnIfMissing("payplantemplate","SheetDefNum","bigint(20) NOT NULL","IsHidden");
			context.AddColumnIfMissing("imagedraw","BetterDiagLayer","tinyint(4) NOT NULL","PearlLayer");
			context.CreateTableIfMissing(@"CREATE TABLE `feeschednote` (
  `FeeSchedNoteNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `FeeSchedNum` bigint(20) NOT NULL,
  `ClinicNums` text NOT NULL,
  `Note` text NOT NULL,
  `DateEntry` date NOT NULL DEFAULT '0001-01-01',
  `SecUserNumEntry` bigint(20) NOT NULL,
  `SecDateEntry` date NOT NULL DEFAULT '0001-01-01',
  `SecDateTEdit` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`FeeSchedNoteNum`),
  KEY `FeeSchedNum` (`FeeSchedNum`),
  KEY `SecUserNumEntry` (`SecUserNumEntry`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `queryfilter` (
  `QueryFilterNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `GroupName` varchar(255) NOT NULL,
  `FilterText` varchar(255) NOT NULL,
  PRIMARY KEY (`QueryFilterNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
		}
	}

	///<summary>Open Dental 25.1: new columns, and the data migrations that only a database coming from 24.x needs:
	///- guardian.Relationship renumbering (None became 0; Father moved from 0 to 6 and Sitter from 6 to 23),
	///- codegroup.ShowInFrequency taken from the old meaning of IsHidden,
	///- payplancharge.IsDownPayment set on the charges 24.3 recognized as the down payment by their note.
	///The renumbering runs only while codegroup.ShowInFrequency is missing, and that column is added right after it, so a database Open Dental
	///already converted (which has the column) is never renumbered.</summary>
	[ForkMigration(2026101902,"Open Dental 25.1 schema")]
	public class OdParity25_1Schema:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			if(!context.ColumnExists("codegroup","ShowInFrequency")) {
				//Pre-25.1 database. One statement, so a row is never moved twice.
				context.ExecuteData("UPDATE guardian SET Relationship=CASE Relationship WHEN 6 THEN 23 WHEN 0 THEN 6 END WHERE Relationship IN (0,6)");
				//The marker column. Before 25.1, IsHidden meant "not a row in the frequency grid"; from 25.1 that is ShowInFrequency and IsHidden means
				//the group is retired. No group was retired before 25.1.
				context.AddColumnIfMissing("codegroup","ShowInFrequency","tinyint(4) NOT NULL","ShowInAgeLimit");
				context.ExecuteData("UPDATE codegroup SET ShowInFrequency=(IsHidden=0),IsHidden=0");
			}
			context.AddColumnIfMissing("computerpref","CreditCardTerminalId","varchar(255) NOT NULL","VideoRectangle");
			context.AddColumnIfMissing("creditcard","CardHolderName","varchar(255) NOT NULL","Nickname");
			context.AddColumnIfMissing("eformfield","AutoImport","tinyint(4) NOT NULL","SpaceToRight");
			context.AddColumnIfMissing("eformfield","PrefillFromGuar","tinyint(4) NOT NULL","AutoImport");
			context.AddColumnIfMissing("eformfielddef","AutoImport","tinyint(4) NOT NULL","SpaceToRight");
			context.AddColumnIfMissing("eformfielddef","PrefillFromGuar","tinyint(4) NOT NULL","AutoImport");
			context.AddColumnIfMissing("insverify","SecurityHash","varchar(255) NOT NULL","SecDateTEdit");
			context.AddColumnIfMissing("insverifyhist","SecurityHash","varchar(255) NOT NULL","SecDateTEdit");
			context.AddColumnIfMissing("medication","IsHidden","tinyint(4) NOT NULL","RxCui");
			if(context.AddColumnIfMissing("payplancharge","IsDownPayment","tinyint(4) NOT NULL","IsOffset")) {
				//24.3 finds down payments by the charge note (PayPlanEdit.CreateRowForPayPlanListDT) and writes the note through
				//Lans.g("PayPlanEdit","Down payment"), so a translated note is matched too. Only debit charges (ChargeType 0) are down payments.
				string command="UPDATE payplancharge SET IsDownPayment=1 WHERE ChargeType=0 AND (Note LIKE '%down payment%' OR Note LIKE '%downpayment%'";
				if(context.TableExists("languageforeign")) {
					command+=" OR TRIM(Note) IN (SELECT Translation FROM languageforeign WHERE ClassType='PayPlanEdit' AND English='Down payment' "
						+"AND Translation!='')";
				}
				command+=")";
				context.ExecuteData(command);
			}
		}
	}

	///<summary>Open Dental 25.2: appointment mirroring columns, eForm translation columns, preauthorization EOBs, XConnect, the signing user on sheets,
	///the statement option, pending insurance, branding (data compatibility only), the task category rename and two widened columns. Back-fills,
	///only when this step adds the column: statement.ShowTransSinceBalZero from the BillingShowTransSinceBalZero preference, and the English copies
	///of eForm labels and pick lists (forms made before 25.2 were never translated, so the English text is the text).</summary>
	[ForkMigration(2026101903,"Open Dental 25.2 schema")]
	public class OdParity25_2Schema:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.AddColumnIfMissing("appointment","IsMirrored","tinyint(4) NOT NULL","ItemOrderPlanned");
			context.AddColumnIfMissing("apptview","ShowMirroredAppts","tinyint(4) NOT NULL","OnlyScheduledProvDays");
			context.AddColumnIfMissing("histappointment","IsMirrored","tinyint(4) NOT NULL","ItemOrderPlanned");
			context.EnsureColumnDefinition("histappointment","ProcDescript","text NOT NULL");
			context.CreateTableIfMissing(@"CREATE TABLE `branding` (
  `BrandingNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `BrandingType` tinyint(4) NOT NULL,
  `ClinicNum` bigint(20) NOT NULL,
  `ValueString` text NOT NULL,
  `DateTimeUpdated` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  PRIMARY KEY (`BrandingNum`),
  KEY `ClinicNum` (`ClinicNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.AddColumnIfMissing("clearinghouse","EnableXConnect","tinyint(4) NOT NULL DEFAULT 0","LocationID");
			context.AddColumnIfMissing("eclipboardsheetdef","SheetDefNumsConsidered","varchar(255) NOT NULL","Frequency");
			if(context.AddColumnIfMissing("eformfield","ValueLabelEnglish","text NOT NULL","PrefillFromGuar")) {
				context.ExecuteData("UPDATE eformfield SET ValueLabelEnglish=ValueLabel");
			}
			//Created as text, the 25.3 type (25.2 made it varchar(255) and 25.3 widened it).
			if(context.AddColumnIfMissing("eformfield","PickListVisEnglish","text NOT NULL","ValueLabelEnglish")) {
				context.ExecuteData("UPDATE eformfield SET PickListVisEnglish=PickListVis");
			}
			context.AddColumnIfMissing("eobattach","ClaimNumPreAuth","bigint(20) NOT NULL","RawBase64");
			context.EnsureIndex("eobattach","ClaimNumPreAuth","`ClaimNumPreAuth`");
			context.CreateTableIfMissing(@"CREATE TABLE `inspending` (
  `InsPendingNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `PatNum` bigint(20) NOT NULL,
  `PatNumSubscriber` bigint(20) NOT NULL,
  `Ordinal` tinyint(3) unsigned NOT NULL,
  `Relationship` tinyint(4) NOT NULL,
  `GroupNum` varchar(255) NOT NULL,
  `GroupName` varchar(255) NOT NULL,
  `Employer` varchar(255) NOT NULL,
  `SubscriberID` varchar(255) NOT NULL,
  `Phone` varchar(255) NOT NULL,
  `CarrierName` varchar(255) NOT NULL,
  PRIMARY KEY (`InsPendingNum`),
  KEY `PatNum` (`PatNum`),
  KEY `PatNumSubscriber` (`PatNumSubscriber`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.EnsureColumnDefinition("insplan","GroupNum","varchar(50) DEFAULT NULL");
			context.AddColumnIfMissing("inssub","SecurityHash","varchar(255) NOT NULL","SecDateTEdit");
			context.AddColumnIfMissing("sheetfield","UserSigned","bigint(20) NOT NULL","IsSigProvRestricted");
			context.EnsureIndex("sheetfield","UserSigned","`UserSigned`");
			if(!context.ColumnExists("statement","ShowTransSinceBalZero")) {
				//Adding the column with the preference's value as its default fills every existing row in the same ALTER, so the back-fill cannot be
				//half done. The default is removed right after, as in Open Dental's schema.
				string valuePref=context.GetPrefRaw("BillingShowTransSinceBalZero");
				string valueDefault=(valuePref=="1" || string.Equals(valuePref,"true",StringComparison.OrdinalIgnoreCase)) ? "1" : "0";
				context.AddColumnIfMissing("statement","ShowTransSinceBalZero","tinyint(4) NOT NULL DEFAULT "+valueDefault,"LimitedCustomFamily");
			}
			context.EnsureColumnDefinition("statement","ShowTransSinceBalZero","tinyint(4) NOT NULL");
			//TriageCategory was an HQ field; 25.2 renamed it to Category for every office. Same type and position, and the key keeps its old name.
			foreach(string table in new[] { "task","taskhist" }) {
				context.RenameColumnIfNeeded(table,"TriageCategory","Category","bigint(20) NOT NULL DEFAULT 0");
				context.EnsureColumnDefinition(table,"Category","bigint(20) NOT NULL DEFAULT 0");
				context.EnsureIndex(table,"TriageCategory","`Category`");
			}
		}
	}

	///<summary>Open Dental 25.3: accounting review flags and payee, tooth ranges on benefits, Other Benefits code groups, widened eForm and e-mail
	///columns, and the tables and columns of features this program leaves out but whose data must load: intra-office chat, the dental-school
	///overhaul, Web Sched ASAP and the HQ triage position.</summary>
	[ForkMigration(2026101904,"Open Dental 25.3 schema")]
	public class OdParity25_3Schema:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.AddColumnIfMissing("asapcomm","UserNum","bigint(20) NOT NULL","EmailTemplateType");
			context.AddColumnIfMissing("benefit","ToothRange","varchar(255) DEFAULT NULL","TreatArea");
			context.AddColumnIfMissing("codegroup","ShowInOther","tinyint(4) NOT NULL","ShowInFrequency");
			foreach(string table in new[] { "eformfield","eformfielddef" }) {
				context.EnsureColumnDefinition(table,"PickListVis","text NOT NULL");
				context.EnsureColumnDefinition(table,"PickListDb","text NOT NULL");
				context.EnsureColumnDefinition(table,"ConditionalValue","text NOT NULL");
			}
			context.EnsureColumnDefinition("eformfield","PickListVisEnglish","text NOT NULL");
			context.EnsureColumnDefinition("emailmessage","FailReason","text NOT NULL");
			//claimpayment.Note: the 25.3 documentation says text, but Open Dental 25.3.48 databases still have varchar(255). Left alone here; the 26.1
			//step widens it.
			context.AddColumnIfMissing("journalentry","Payee","varchar(255) NOT NULL","SecDateTEdit");
			context.AddColumnIfMissing("journalentry","Notes","text NOT NULL","Payee");
			context.AddColumnIfMissing("journalentry","NeedsReview","tinyint(4) NOT NULL","Notes");
			context.AddColumnIfMissing("transaction","NeedsReview","tinyint(4) NOT NULL","TransactionInvoiceNum");
			context.AddColumnIfMissing("task","TriagePosition","int(11) NOT NULL","Category");
			context.AddColumnIfMissing("taskhist","TriagePosition","int(11) NOT NULL","Category");
			//Intra-office chat. The feature is excluded; the tables exist so the data loads.
			context.CreateTableIfMissing(@"CREATE TABLE `chat` (
  `ChatNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) NOT NULL,
  PRIMARY KEY (`ChatNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `chatattach` (
  `ChatAttachNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ChatMsgNum` bigint(20) NOT NULL,
  `FileName` varchar(255) NOT NULL,
  `Thumbnail` mediumblob NOT NULL,
  `FileData` mediumblob NOT NULL,
  PRIMARY KEY (`ChatAttachNum`),
  KEY `ChatMsgNum` (`ChatMsgNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `chatmsg` (
  `ChatMsgNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ChatNum` bigint(20) NOT NULL,
  `UserNum` bigint(20) NOT NULL,
  `DateTimeSent` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `Message` text NOT NULL,
  `SeqCount` bigint(20) unsigned NOT NULL,
  `Quote` bigint(20) NOT NULL,
  `EventType` tinyint(4) NOT NULL,
  `IsImportant` tinyint(4) NOT NULL,
  PRIMARY KEY (`ChatMsgNum`),
  UNIQUE KEY `SeqCount` (`SeqCount`),
  KEY `UserNum` (`UserNum`),
  KEY `ChatNum` (`ChatNum`),
  KEY `DateTimeSent` (`DateTimeSent`),
  KEY `chatnum_seqcount` (`ChatNum`,`SeqCount`),
  KEY `Quote` (`Quote`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `chatreaction` (
  `ChatReactionNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ChatMsgNum` bigint(20) NOT NULL,
  `UserNum` bigint(20) NOT NULL,
  `EmojiName` varchar(255) NOT NULL,
  PRIMARY KEY (`ChatReactionNum`),
  KEY `ChatMsgNum` (`ChatMsgNum`),
  KEY `UserNum` (`UserNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `chatuserattach` (
  `ChatUserAttachNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `UserNum` bigint(20) NOT NULL,
  `ChatNum` bigint(20) NOT NULL,
  `IsRead` tinyint(4) NOT NULL,
  `DateTimeRemoved` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `IsMute` tinyint(4) NOT NULL,
  PRIMARY KEY (`ChatUserAttachNum`),
  KEY `UserNum` (`UserNum`),
  KEY `ChatNum` (`ChatNum`),
  KEY `usernum_chatnum` (`UserNum`,`ChatNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `chatuserod` (
  `ChatUserodNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `UserNum` bigint(20) NOT NULL,
  `UserStatus` tinyint(4) NOT NULL,
  `DateTimeStatusReset` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `Photo` text NOT NULL,
  `PhotoCrop` varchar(255) NOT NULL,
  `OpenBackground` tinyint(4) NOT NULL,
  `CloseKeepRunning` tinyint(4) NOT NULL,
  `MuteNotifications` tinyint(4) NOT NULL,
  `DismissNotifySecs` int(11) NOT NULL,
  `MuteImportantNotifications` tinyint(4) NOT NULL,
  `DismissImportantNotifySecs` int(11) NOT NULL,
  PRIMARY KEY (`ChatUserodNum`),
  KEY `UserNum` (`UserNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `sequencecounter` (
  `CounterNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `CounterName` varchar(255) NOT NULL,
  `CounterVal` bigint(20) NOT NULL,
  PRIMARY KEY (`CounterNum`),
  KEY `CounterVal` (`CounterVal`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			//Dental-school overhaul (schema in 25.3, feature in 25.4). The features are excluded; the tables and columns exist so the data loads.
			context.AddColumnIfMissing("reqneeded","SchoolCourseDefNum","bigint(20) NOT NULL","SchoolClassNum");
			context.EnsureIndex("reqneeded","SchoolCourseDefNum","`SchoolCourseDefNum`");
			context.AddColumnIfMissing("reqstudent","ProcNum","bigint(20) NOT NULL","DateCompleted");
			context.EnsureIndex("reqstudent","ProcNum","`ProcNum`");
			context.AddColumnIfMissing("schoolcourse","DateStart","date NOT NULL DEFAULT '0001-01-01'","Descript");
			context.AddColumnIfMissing("schoolcourse","DateEnd","date NOT NULL DEFAULT '0001-01-01'","DateStart");
			context.AddColumnIfMissing("schoolcourse","SchoolClassNum","bigint(20) NOT NULL DEFAULT 0","DateEnd");
			context.AddColumnIfMissing("schoolcourse","GradingScaleNum","bigint(20) NOT NULL","SchoolClassNum");
			context.EnsureIndex("schoolcourse","SchoolClassNum","`SchoolClassNum`");
			context.EnsureIndex("schoolcourse","GradingScaleNum","`GradingScaleNum`");
			context.CreateTableIfMissing(@"CREATE TABLE `schoolapproval` (
  `SchoolApprovalNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ProvNum` bigint(20) NOT NULL,
  `SignOffStatus` tinyint(4) NOT NULL,
  `InstructorNum` bigint(20) NOT NULL,
  `AptNum` bigint(20) NOT NULL,
  `ProcNum` bigint(20) NOT NULL,
  `TreatPlanNum` bigint(20) NOT NULL,
  `PerioExamNum` bigint(20) NOT NULL,
  `AllergyNum` bigint(20) NOT NULL,
  `DiseaseNum` bigint(20) NOT NULL,
  `DocNum` bigint(20) NOT NULL,
  `MountNum` bigint(20) NOT NULL,
  `SecDateEntry` date NOT NULL DEFAULT '0001-01-01',
  `SecDateTEdit` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`SchoolApprovalNum`),
  KEY `ProvNum` (`ProvNum`),
  KEY `InstructorNum` (`InstructorNum`),
  KEY `AptNum` (`AptNum`),
  KEY `ProcNum` (`ProcNum`),
  KEY `TreatPlanNum` (`TreatPlanNum`),
  KEY `PerioExamNum` (`PerioExamNum`),
  KEY `AllergyNum` (`AllergyNum`),
  KEY `DiseaseNum` (`DiseaseNum`),
  KEY `DocNum` (`DocNum`),
  KEY `MountNum` (`MountNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `schoolcoursedef` (
  `SchoolCourseDefNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `CourseID` varchar(255) NOT NULL,
  `Descript` varchar(255) NOT NULL,
  PRIMARY KEY (`SchoolCourseDefNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `schoolcoursesched` (
  `SchoolCourseSchedNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `SchoolCourseDefNum` bigint(20) NOT NULL,
  `SchoolCourseNum` bigint(20) NOT NULL,
  `TimeStart` time NOT NULL DEFAULT '00:00:00',
  `TimeEnd` time NOT NULL DEFAULT '00:00:00',
  `DayOfTheWeek` tinyint(4) NOT NULL,
  `DateOverride` date NOT NULL DEFAULT '0001-01-01',
  `IsOverride` tinyint(4) NOT NULL,
  `IsCanceled` tinyint(4) NOT NULL,
  PRIMARY KEY (`SchoolCourseSchedNum`),
  KEY `SchoolCourseDefNum` (`SchoolCourseDefNum`),
  KEY `SchoolCourseNum` (`SchoolCourseNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
		}
	}

	///<summary>Closes the key gap on columns that already existed in 24.3: keys an Open Dental 25.3.48 database has and a database made by our
	///24.3.63 convert scripts lacks (found by comparing the two schemas). The definitions come from the 25.3.48 reference snapshot.</summary>
	[ForkMigration(2026101905,"Open Dental 25.3.48 keys on existing columns")]
	public class OdParity25_3Keys:ForkMigrationStep {
		///<summary>Table and key name.</summary>
		public static readonly string[][] ArrayKeys=new string[][] {
			new[] { "carecreditwebresponse","ProcessingStatus" },
			new[] { "claim","ClaimIdentifier" },
			new[] { "claim","ClaimStatus" },
			new[] { "claimproc","indexUnfinalizeCovering" },
			new[] { "document","FileName" },
			new[] { "eservicelog","LogGuid" },
			new[] { "patfield","FieldName" },
			new[] { "recurringcharge","DateTimeCharge" },
			new[] { "rxnorm","Description" },
		};

		public override void Apply(ForkMigrationContext context) {
			SchemaSnapshot reference=SchemaVerifier.GetReference25_3_48();
			foreach(string[] arrayKey in ArrayKeys) {
				SchemaIndex index=reference.GetTable(arrayKey[0]).GetIndex(arrayKey[1]);
				context.EnsureIndex(arrayKey[0],index.Name,index.Columns,index.Kind=="UNIQUE KEY");
			}
		}
	}

	///<summary>Brings column definitions that already existed in 24.3 to their Open Dental 25.3.48 form where a database made by our 24.3.63 convert
	///scripts differs (found by comparing the two schemas): missing date defaults, carrier columns that are NOT NULL in 25.3.48 (NULLs become empty
	///strings first), and the equipment table, which old databases created in latin1. The definitions come from the 25.3.48 reference snapshot, so a
	///database that already matches is left alone.</summary>
	[ForkMigration(2026101906,"Open Dental 25.3.48 definitions of existing columns")]
	public class OdParity25_3ColumnDefinitions:ForkMigrationStep {
		///<summary>Table and column.</summary>
		public static readonly string[][] ArrayColumns=new string[][] {
			new[] { "appointment","DateTimeArrived" },
			new[] { "appointment","DateTimeSeated" },
			new[] { "appointment","DateTimeDismissed" },
			new[] { "carrier","CarrierName" },
			new[] { "carrier","Address" },
			new[] { "carrier","Address2" },
			new[] { "carrier","City" },
			new[] { "carrier","State" },
			new[] { "carrier","Zip" },
			new[] { "carrier","Phone" },
			new[] { "carrier","ElectID" },
			new[] { "carrier","CDAnetVersion" },
			new[] { "document","DateCreated" },
			new[] { "eobattach","DateTCreated" },
			new[] { "popup","DateTimeEntry" },
			new[] { "procedurelog","DateTP" },
			new[] { "registrationkey","DateStarted" },
			new[] { "registrationkey","DateDisabled" },
			new[] { "registrationkey","DateEnded" },
			new[] { "statement","DateSent" },
			new[] { "statement","DateRangeFrom" },
			new[] { "statement","DateRangeTo" },
			new[] { "supplyneeded","DateAdded" },
			new[] { "supplyorder","DatePlaced" },
			new[] { "task","DateTimeFinished" },
		};

		public override void Apply(ForkMigrationContext context) {
			SchemaSnapshot reference=SchemaVerifier.GetReference25_3_48();
			SchemaTable tableEquipment=reference.GetTable("equipment");
			context.EnsureTableCharset("equipment",tableEquipment.Charset,tableEquipment.Collation);
			//Converting from latin1 makes MariaDB widen text columns to mediumtext so they still hold as many characters. Put them back to the
			//25.3.48 types. A value longer than 64 KB in UTF-8 would be cut; equipment descriptions, locations and notes are short.
			foreach(SchemaColumn column in tableEquipment.Columns) {
				context.EnsureColumnDefinition("equipment",column.Name,column.Definition);
			}
			foreach(string[] arrayColumn in ArrayColumns) {
				SchemaColumn column=reference.GetTable(arrayColumn[0]).GetColumn(arrayColumn[1]);
				string sqlNullReplacement=null;
				if(column.Definition.Contains("NOT NULL") && column.Definition.StartsWith("varchar")) {
					sqlNullReplacement="''";
				}
				context.EnsureColumnDefinition(arrayColumn[0],column.Name,column.Definition,sqlNullReplacement);
			}
		}
	}
}
