// Routine maintenance (docs/specs/routine-maintenance.md): the five fork-only tables and the steps that create them. Levels 2026110201 to
// 2026110205; 2026110206 to 2026110209 stay free for this feature.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	///<summary>The routine maintenance tables, exactly as SHOW CREATE TABLE prints them on MariaDB 10.11. Not Open Dental schema: the schema
	///verifier allows them as extra tables and ForkTables.VerifyDatabase checks them. The steps below create them empty and write no row, so on an
	///adopted database they change nothing else.</summary>
	public class MaintenanceTables:ForkTableSet {
		public const string TABLE_ITEM="maintitem";
		public const string TABLE_STEP="maintstep";
		public const string TABLE_FIELD="maintfield";
		public const string TABLE_RECORD="maintrecord";
		public const string TABLE_RECORD_VALUE="maintrecordvalue";

		public const string CREATE_ITEM=@"CREATE TABLE `maintitem` (
  `MaintItemNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ItemName` varchar(255) NOT NULL,
  `Description` text NOT NULL,
  `EquipmentNum` bigint(20) NOT NULL,
  `ScheduleType` tinyint(4) NOT NULL,
  `IntervalCount` int(11) NOT NULL,
  `IntervalUnit` tinyint(4) NOT NULL,
  `Weekdays` int(11) NOT NULL,
  `DateAnchor` date NOT NULL DEFAULT '0001-01-01',
  `LeadDays` int(11) NOT NULL,
  `UserNumAssigned` bigint(20) NOT NULL,
  `UserGroupNumAssigned` bigint(20) NOT NULL,
  `IsHidden` tinyint(4) NOT NULL,
  `SecUserNumEntry` bigint(20) NOT NULL,
  `SecDateTEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `SecDateTEdit` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`MaintItemNum`),
  KEY `EquipmentNum` (`EquipmentNum`),
  KEY `UserNumAssigned` (`UserNumAssigned`),
  KEY `UserGroupNumAssigned` (`UserGroupNumAssigned`),
  KEY `SecUserNumEntry` (`SecUserNumEntry`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_STEP=@"CREATE TABLE `maintstep` (
  `MaintStepNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `MaintItemNum` bigint(20) NOT NULL,
  `ItemOrder` int(11) NOT NULL,
  `Description` varchar(255) NOT NULL,
  PRIMARY KEY (`MaintStepNum`),
  KEY `MaintItemNum` (`MaintItemNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_FIELD=@"CREATE TABLE `maintfield` (
  `MaintFieldNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `MaintItemNum` bigint(20) NOT NULL,
  `ItemOrder` int(11) NOT NULL,
  `FieldName` varchar(255) NOT NULL,
  `FieldType` tinyint(4) NOT NULL,
  `Unit` varchar(255) NOT NULL,
  `HasMin` tinyint(4) NOT NULL,
  `ValueMin` double NOT NULL,
  `HasMax` tinyint(4) NOT NULL,
  `ValueMax` double NOT NULL,
  `IsRequired` tinyint(4) NOT NULL,
  PRIMARY KEY (`MaintFieldNum`),
  KEY `MaintItemNum` (`MaintItemNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_RECORD=@"CREATE TABLE `maintrecord` (
  `MaintRecordNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `MaintItemNum` bigint(20) NOT NULL,
  `DateDone` date NOT NULL DEFAULT '0001-01-01',
  `DateDue` date NOT NULL DEFAULT '0001-01-01',
  `UserNum` bigint(20) NOT NULL,
  `Note` text NOT NULL,
  `IsFlagged` tinyint(4) NOT NULL,
  `SecDateTEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  PRIMARY KEY (`MaintRecordNum`),
  KEY `MaintItemNum` (`MaintItemNum`),
  KEY `UserNum` (`UserNum`),
  KEY `DateDone` (`DateDone`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_RECORD_VALUE=@"CREATE TABLE `maintrecordvalue` (
  `MaintRecordValueNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `MaintRecordNum` bigint(20) NOT NULL,
  `MaintStepNum` bigint(20) NOT NULL,
  `MaintFieldNum` bigint(20) NOT NULL,
  `ItemOrder` int(11) NOT NULL,
  `FieldType` tinyint(4) NOT NULL,
  `Label` varchar(255) NOT NULL,
  `Unit` varchar(255) NOT NULL,
  `ValueText` text NOT NULL,
  `IsFlagged` tinyint(4) NOT NULL,
  PRIMARY KEY (`MaintRecordValueNum`),
  KEY `MaintRecordNum` (`MaintRecordNum`),
  KEY `MaintStepNum` (`MaintStepNum`),
  KEY `MaintFieldNum` (`MaintFieldNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public override string Feature {
			get { return "Routine maintenance"; }
		}

		public override string Levels {
			get { return "2026110201 to 2026110205"; }
		}

		public override string[] ArrayCreateTables {
			get { return new[] { CREATE_ITEM,CREATE_STEP,CREATE_FIELD,CREATE_RECORD,CREATE_RECORD_VALUE }; }
		}
	}

	///<summary>Routine maintenance: the maintenance items the office defines. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110201,"Routine maintenance: add the maintitem table")]
	public class MaintItemTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(MaintenanceTables.CREATE_ITEM);
		}
	}

	///<summary>Routine maintenance: the checklist steps of each item. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110202,"Routine maintenance: add the maintstep table")]
	public class MaintStepTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(MaintenanceTables.CREATE_STEP);
		}
	}

	///<summary>Routine maintenance: the result fields of each item. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110203,"Routine maintenance: add the maintfield table")]
	public class MaintFieldTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(MaintenanceTables.CREATE_FIELD);
		}
	}

	///<summary>Routine maintenance: each recorded completion. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110204,"Routine maintenance: add the maintrecord table")]
	public class MaintRecordTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(MaintenanceTables.CREATE_RECORD);
		}
	}

	///<summary>Routine maintenance: the checklist ticks and results of each completion. Creates the empty table; writes no row.</summary>
	[ForkMigration(2026110205,"Routine maintenance: add the maintrecordvalue table")]
	public class MaintRecordValueTableStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(MaintenanceTables.CREATE_RECORD_VALUE);
		}
	}
}
