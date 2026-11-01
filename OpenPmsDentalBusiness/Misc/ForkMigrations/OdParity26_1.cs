// The Open Dental 25.3.48 to 26.1.70 schema (backlog package M2-01).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

//Re-implemented from Open Dental's published database documents only: the diff documents for 25.4 and 26.1, checked against the full 26.1
//document. No Open Dental program or database was used. The documents publish names, column order and types; NULL/NOT NULL, defaults and keys
//are inferred from the conventions of the 25.3.48 schema and marked INFERRED in Reference/OdSchema-26.1.70.sql, which these steps must match.
//Specs: docs/specs/schema-changes-24.3-to-26.1.md (Part 2) and docs/specs/port-backlog.md (M2-01).
//Each step is idempotent: on a database at 25.3.48 (an adopted office copy, or a 24.3.63 database after the 25.3 steps) it brings the schema to
//26.1.70; on a database that already has a change it leaves it alone, and the data migration that belongs to a change runs only in the
//statement that makes the change.
namespace OpenPmsDentalBusiness.ForkMigrations {
	///<summary>Open Dental 25.4: PaySuite (Canadian claim payments; tables only), "date submitted" on eForms and sheets, the dental-school overhaul
	///(schema only; the features are excluded), long insurance edit log values, datetime assignment on the insurance verification list, the
	///processor of a payment terminal, the per-procedure "Do Not Bill Insurance" flag, longer referral phone numbers, and HQ clinic access (schema
	///only). Data migration: existing evaluation rows get GradeOverride -1 ("no override") in the ALTER that adds the column.</summary>
	[ForkMigration(2026102401,"Open Dental 25.4 schema")]
	public class OdParity25_4Schema:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			OdParity26_1.AlterTable(context,"carrier",new[] {
				new OdColumn("PaySuiteTransSup","tinyint(4) NOT NULL","OrthoInsPayConsolidate"),
			});
			OdParity26_1.AlterTable(context,"eform",new[] {
				new OdColumn("DateTimeSubmitted","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'","SaveImageCategory"),
			});
			//The column default fills every existing row with -1 in the same statement, so the back-fill cannot be half done. It stays the column's
			//default, as in the reference.
			OdParity26_1.AlterTable(context,"evaluation",new[] {
				new OdColumn("GradeOverride","float NOT NULL DEFAULT -1","Notes"),
			});
			OdParity26_1.AlterTable(context,"evaluationdef",new[] {
				new OdColumn("SchoolCourseDefNum","bigint(20) NOT NULL","GradingScaleNum"),
			},new[] { new OdKey("SchoolCourseDefNum","`SchoolCourseDefNum`") });
			foreach(string table in new[] { "inseditlog","inseditpatlog" }) {
				OdParity26_1.AlterTable(context,table,new[] {
					new OdColumn("OldValueBig","text NOT NULL","Description"),
					new OdColumn("NewValueBig","text NOT NULL","OldValueBig"),
				});
			}
			//date to datetime keeps every value (midnight) and the DateLastAssigned key on insverify.
			context.EnsureColumnDefinition("insverify","DateLastAssigned","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'");
			context.EnsureColumnDefinition("insverifyhist","DateLastAssigned","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'");
			OdParity26_1.AlterTable(context,"payterminal",new[] {
				new OdColumn("CCIntegration","varchar(50) NOT NULL","TerminalID"),
			});
			OdParity26_1.AlterTable(context,"procedurelog",new[] {
				new OdColumn("NoBillIns","tinyint(4) NOT NULL","DiscountPlanAmt"),
			});
			context.EnsureColumnDefinition("referral","Telephone","varchar(30) DEFAULT ''");
			OdParity26_1.AlterTable(context,"registrationkey",new[] {
				new OdColumn("DateTClinicAccess","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'","BackupPassCode"),
			});
			OdParity26_1.AlterTable(context,"schoolcoursedef",new[] {
				new OdColumn("GradingScaleNum","bigint(20) NOT NULL","Descript"),
			},new[] { new OdKey("GradingScaleNum","`GradingScaleNum`") });
			OdParity26_1.AlterTable(context,"sheet",new[] {
				new OdColumn("DateTimeSubmitted","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'","WebFormSheetID"),
			});
			context.CreateTableIfMissing(@"CREATE TABLE `paysuitepayment` (
  `PaySuitePaymentNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `PaymentId` varchar(255) NOT NULL,
  `ProviderId` varchar(255) NOT NULL,
  `PaymentMethod` varchar(255) NOT NULL,
  `PaymentReference` varchar(255) NOT NULL,
  `PaymentAmount` double NOT NULL,
  `PaymentDate` date NOT NULL DEFAULT '0001-01-01',
  `PaymentStatus` varchar(255) NOT NULL,
  `ReversalReasonCode` varchar(255) NOT NULL,
  `AssociatedPaymentId` varchar(255) NOT NULL,
  `PaySuitePaymentDetailNum` bigint(20) NOT NULL,
  `HasUnresolvedClaimPayment` tinyint(4) NOT NULL,
  `ReconciliationStatus` tinyint(4) NOT NULL,
  `ClaimPaymentNum` bigint(20) NOT NULL,
  PRIMARY KEY (`PaySuitePaymentNum`),
  KEY `PaySuitePaymentDetailNum` (`PaySuitePaymentDetailNum`),
  KEY `ClaimPaymentNum` (`ClaimPaymentNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `paysuitepaymentdetail` (
  `PaySuitePaymentDetailNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `DetailsJson` mediumtext NOT NULL,
  PRIMARY KEY (`PaySuitePaymentDetailNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			//Dental-school overhaul. The features are excluded; the tables exist so the data loads.
			context.CreateTableIfMissing(@"CREATE TABLE `schoolcourseenrollee` (
  `SchoolCourseEnrolleeNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `SchoolCourseNum` bigint(20) NOT NULL,
  `StudentNum` bigint(20) NOT NULL,
  `GradeNumber` float NOT NULL DEFAULT -1,
  `GradeOverride` float NOT NULL DEFAULT -1,
  PRIMARY KEY (`SchoolCourseEnrolleeNum`),
  KEY `SchoolCourseNum` (`SchoolCourseNum`),
  KEY `StudentNum` (`StudentNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `schoolcourseinstructor` (
  `SchoolCourseInstructorNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `SchoolCourseNum` bigint(20) NOT NULL,
  `InstructorNum` bigint(20) NOT NULL,
  PRIMARY KEY (`SchoolCourseInstructorNum`),
  KEY `SchoolCourseNum` (`SchoolCourseNum`),
  KEY `InstructorNum` (`InstructorNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
		}
	}

	///<summary>Open Dental 26.1: logo images in the branding table (schema only), carrier preauthorization codes, the chat notification sound (schema
	///only), the ERA claim a claimproc was paid from, Insurance History options on code groups, the extra definition value, deleted-row snapshots
	///(schema only; their writers belong to the excluded REST API), the dunning e-mail format, the eForm signature time, employee titles, the second
	///signature on procedure notes, the sending user of a text (schema only) and service date view on statements. No data migration.</summary>
	[ForkMigration(2026102402,"Open Dental 26.1 schema")]
	public class OdParity26_1Schema:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.EnsureColumnDefinition("branding","ValueString","mediumtext NOT NULL");
			OdParity26_1.AlterTable(context,"carrier",new[] {
				new OdColumn("PreAuthCodes","text NOT NULL","PaySuiteTransSup"),
			});
			OdParity26_1.AlterTable(context,"chatuserod",new[] {
				new OdColumn("PlayNotificationSound","tinyint(4) NOT NULL","DismissImportantNotifySecs"),
			});
			//claimproc is one of the largest tables: the column and its key go in one ALTER, so MyISAM copies the table once.
			OdParity26_1.AlterTable(context,"claimproc",new[] {
				new OdColumn("Etrans835AttachNum","bigint(20) NOT NULL","SecurityHash"),
			},new[] { new OdKey("Etrans835AttachNum","`Etrans835AttachNum`") });
			OdParity26_1.AlterTable(context,"codegroup",new[] {
				new OdColumn("ShowInHistory","tinyint(4) NOT NULL","ShowInOther"),
				new OdColumn("HistProcCode","varchar(255) NOT NULL","ShowInHistory"),
				new OdColumn("IsPerioFourQuads","tinyint(4) NOT NULL","HistProcCode"),
			});
			OdParity26_1.AlterTable(context,"definition",new[] {
				new OdColumn("Supp","varchar(255) NOT NULL","IsHidden"),
			});
			context.CreateTableIfMissing(@"CREATE TABLE `deletedobj` (
  `DeletedObjNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `DateTEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `ObjType` tinyint(4) NOT NULL,
  `ObjSerialized` mediumtext NOT NULL,
  PRIMARY KEY (`DeletedObjNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			OdParity26_1.AlterTable(context,"dunning",new[] {
				new OdColumn("EmailType","tinyint(4) NOT NULL","IsSuperFamily"),
			});
			OdParity26_1.AlterTable(context,"eformfield",new[] {
				new OdColumn("DateTimeSig","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'","PickListVisEnglish"),
			});
			OdParity26_1.AlterTable(context,"employee",new[] {
				new OdColumn("Title","bigint(20) NOT NULL","ReportsTo"),
			},new[] { new OdKey("Title","`Title`") });
			//procnote can be large too: three columns and a key in one ALTER.
			OdParity26_1.AlterTable(context,"procnote",new[] {
				new OdColumn("UserNum2","bigint(20) NOT NULL","Signature"),
				new OdColumn("Signature2","text DEFAULT NULL","UserNum2"),
				new OdColumn("SigIsTopaz2","tinyint(3) unsigned NOT NULL","Signature2"),
			},new[] { new OdKey("UserNum2","`UserNum2`") });
			OdParity26_1.AlterTable(context,"smstomobile",new[] {
				new OdColumn("SecUserNumEntry","bigint(20) NOT NULL","SecDateTEdit"),
			},new[] { new OdKey("SecUserNumEntry","`SecUserNumEntry`") });
			OdParity26_1.AlterTable(context,"statement",new[] {
				new OdColumn("IsServiceDateView","tinyint(4) NOT NULL","ShowTransSinceBalZero"),
			});
		}
	}

	///<summary>claimpayment.Note becomes text NOT NULL. The published 25.3 and 26.1 documents both say text, but Open Dental 25.3.48 databases
	///still have varchar(255) DEFAULT '' (backlog risk R2, "Drift"). A NULL note becomes an empty string first; every other value is kept as it is.
	///On a database that already has text NOT NULL nothing happens.</summary>
	[ForkMigration(2026102403,"Open Dental 26.1: claimpayment.Note as text")]
	public class OdParity26_1ClaimPaymentNote:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.EnsureColumnDefinition("claimpayment","Note","text NOT NULL","''");
		}
	}

	///<summary>A column a 26.1 step adds: its definition as SHOW CREATE TABLE prints it, and the column it goes after.</summary>
	public class OdColumn {
		public string Name;
		public string Definition;
		public string After;

		public OdColumn(string name,string definition,string after) {
			Name=name;
			Definition=definition;
			After=after;
		}
	}

	///<summary>A plain key a 26.1 step adds: its name and its column list as SHOW CREATE TABLE prints it, without the outer parentheses.</summary>
	public class OdKey {
		public string Name;
		public string Columns;

		public OdKey(string name,string columns) {
			Name=name;
			Columns=columns;
		}
	}

	///<summary>Helpers of the 25.4 and 26.1 steps.</summary>
	public static class OdParity26_1 {
		///<summary>Adds the columns and keys of one table that are missing, all in one ALTER TABLE, so a MyISAM table is copied once however many
		///columns it gains (claimproc, procedurelog and procnote are among the largest tables of an office database). Columns are added in the given
		///order after the named column; a column may go after one added earlier in the same list. Keys that exist with other columns are replaced
		///afterwards by ForkMigrationContext.EnsureIndex. Returns the names of the columns that were added.</summary>
		public static List<string> AlterTable(ForkMigrationContext context,string table,OdColumn[] arrayColumns,OdKey[] arrayKeys=null) {
			arrayKeys=arrayKeys??new OdKey[0];
			if(!context.TableExists(table)) {
				throw new ApplicationException("Table "+table+" does not exist.");
			}
			List<string> listClauses=new List<string>();
			List<string> listAdded=new List<string>();
			foreach(OdColumn column in arrayColumns) {
				if(context.ColumnExists(table,column.Name)) {
					continue;
				}
				listClauses.Add("ADD COLUMN `"+column.Name+"` "+column.Definition+(column.After==null ? "" : " AFTER `"+column.After+"`"));
				listAdded.Add(column.Name);
			}
			foreach(OdKey key in arrayKeys) {
				if(context.IndexExists(table,key.Name)) {
					continue;
				}
				listClauses.Add("ADD INDEX `"+key.Name+"` ("+key.Columns+")");
			}
			if(listClauses.Count>0) {
				context.Execute("ALTER TABLE `"+table+"` "+string.Join(", ",listClauses),table);
			}
			foreach(OdKey key in arrayKeys) {
				context.EnsureIndex(table,key.Name,key.Columns);
			}
			return listAdded;
		}
	}
}
