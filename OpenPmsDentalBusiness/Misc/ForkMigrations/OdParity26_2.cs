// The Open Dental 26.1 to 26.2 schema (package P262-SCHEMA, the 26.2 schema foundation).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

//Re-implemented from Open Dental's published database documents only: OpenDentalDiffDocumentation26-2.xml (the changes since 26.1), checked
//against the full OpenDentalDocumentation26-2.xml. No Open Dental program or database was used. The documents publish names, column order and
//types; NULL/NOT NULL, defaults, keys and the engine of the new tables are inferred from the conventions of the 25.3.48 schema and marked INFERRED
//in Reference/OdSchema-26.2.0.sql, which this step must match (docs/port-notes/P262-SCHEMA.md).
//The step is idempotent: on a database at 26.1.70 (an adopted office copy or a 24.3.63 database after the earlier steps) it brings the schema to
//26.2.0; on a database that already has a change (an Open Dental 26.2 database) it leaves it alone.
namespace OpenPmsDentalBusiness.ForkMigrations {
	///<summary>Open Dental 26.2: failure tracking of API subscriptions (schema only; this program has no API event sender), the last-change time of
	///insurance blue book rows, the default claim form and the default Glidewell account of a provider, and the three tables of the Glidewell lab
	///case integration (glidewellacct, glidewellmsg, labcaseproduct; schema only here, the feature is a separate package). No data migration: the
	///new columns hold their "nothing" value (0, empty, 0001-01-01) in existing rows, except insbluebook.DateTStamp, which MariaDB fills with the
	///time of the ALTER TABLE, as for any row written then (INFERRED: the documents do not say what Open Dental puts there).
	///Levels 2026110701 to 2026110709 are this package's; 2026110702 to 2026110709 are kept for later 26.2 schema corrections.</summary>
	[ForkMigration(2026110701,"Open Dental 26.2 schema")]
	public class OdParity26_2Schema:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			OdParity26_1.AlterTable(context,"apisubscription",new[] {
				new OdColumn("DateTimeLastFailure","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'","Note"),
				new OdColumn("FailureReason","text NOT NULL","DateTimeLastFailure"),
				new OdColumn("SubsequentFailures","int(11) NOT NULL","FailureReason"),
				new OdColumn("DateTimeNextRetry","datetime NOT NULL DEFAULT '0001-01-01 00:00:00'","SubsequentFailures"),
			});
			OdParity26_1.AlterTable(context,"insbluebook",new[] {
				new OdColumn("DateTStamp","timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()","ClaimNum"),
			});
			//Two columns and two keys in one ALTER, so MyISAM copies the table once.
			OdParity26_1.AlterTable(context,"provider",new[] {
				new OdColumn("ClaimFormNum","bigint(20) NOT NULL","PreferredName"),
				new OdColumn("GlidewellAcctNum","bigint(20) NOT NULL","ClaimFormNum"),
			},new[] { new OdKey("ClaimFormNum","`ClaimFormNum`"),new OdKey("GlidewellAcctNum","`GlidewellAcctNum`") });
			//Glidewell lab cases. The feature is a separate package; the tables exist so that the data of an Open Dental 26.2 database loads.
			context.CreateTableIfMissing(@"CREATE TABLE `glidewellacct` (
  `GlidewellAcctNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `AccountNumber` varchar(255) NOT NULL,
  `ApiKey` varchar(255) NOT NULL,
  `DateTimeExpires` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  PRIMARY KEY (`GlidewellAcctNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `glidewellmsg` (
  `GlidewellMsgNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `LabCaseNum` bigint(20) NOT NULL,
  `OrderStatus` tinyint(4) NOT NULL,
  `VendorRxId` varchar(255) NOT NULL,
  `PatNum` bigint(20) NOT NULL,
  `GlidewellAcctNum` bigint(20) NOT NULL,
  `RawJsonResponse` text NOT NULL,
  `RawJsonRequest` text NOT NULL,
  `SentITero` tinyint(4) NOT NULL,
  `SentMedit` tinyint(4) NOT NULL,
  PRIMARY KEY (`GlidewellMsgNum`),
  KEY `LabCaseNum` (`LabCaseNum`),
  KEY `PatNum` (`PatNum`),
  KEY `GlidewellAcctNum` (`GlidewellAcctNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
			context.CreateTableIfMissing(@"CREATE TABLE `labcaseproduct` (
  `LabCaseProductNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `LabCaseNum` bigint(20) NOT NULL,
  `PatNum` bigint(20) NOT NULL,
  `GlidewellProduct` tinyint(4) NOT NULL,
  `Material` varchar(255) NOT NULL,
  `Shade` varchar(255) NOT NULL,
  `ToothRange` varchar(255) NOT NULL,
  `Notes` text NOT NULL,
  PRIMARY KEY (`LabCaseProductNum`),
  KEY `LabCaseNum` (`LabCaseNum`),
  KEY `PatNum` (`PatNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
		}
	}
}
