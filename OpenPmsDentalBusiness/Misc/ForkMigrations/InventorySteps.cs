// Inventory tracking (docs/specs/inventory-tracking.md): the fork tables and the opening balances. Levels 2026110211 and 2026110212 of the
// inventory range 2026110211 to 2026110219. The preferences, the alert category links and the Inventory Adjust grant are written when the office
// turns tracking on (InventoryTracking.SaveSettings), not here, so adopting an Open Dental database changes no Open Dental table for this feature.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	///<summary>The stock ledger, and in the same step the opening balances: an Opening row for every supply whose LevelOnHand is not 0 (rounded to 2
	///decimals, at the supply's price), so supply.LevelOnHand equals the sum of its ledger rows from the start. The opening rows are written only
	///when this step created the table (README rule 4), never again. supply itself is not changed.</summary>
	[ForkMigration(2026110211,"Inventory tracking: stock ledger and opening balances")]
	public class InventoryLedgerStep:ForkMigrationStep {
		///<summary>The table as SHOW CREATE TABLE prints it on MariaDB 10.11.</summary>
		public const string CREATE_SUPPLYLEDGER=@"CREATE TABLE `supplyledger` (
  `SupplyLedgerNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `SupplyNum` bigint(20) NOT NULL,
  `SupplyLotNum` bigint(20) NOT NULL,
  `LedgerType` tinyint(4) NOT NULL,
  `Quantity` double NOT NULL,
  `UnitCost` double NOT NULL,
  `ProcNum` bigint(20) NOT NULL,
  `PatNum` bigint(20) NOT NULL,
  `SupplyOrderItemNum` bigint(20) NOT NULL,
  `DispSupplyNum` bigint(20) NOT NULL,
  `UserNum` bigint(20) NOT NULL,
  `DateTimeEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `Reason` varchar(255) NOT NULL,
  PRIMARY KEY (`SupplyLedgerNum`),
  KEY `SupplyNum` (`SupplyNum`),
  KEY `SupplyLotNum` (`SupplyLotNum`),
  KEY `ProcNum` (`ProcNum`),
  KEY `SupplyOrderItemNum` (`SupplyOrderItemNum`),
  KEY `DateTimeEntry` (`DateTimeEntry`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		///<summary>The reason written on the opening rows.</summary>
		public const string REASON_OPENING="Opening balance";

		public override void Apply(ForkMigrationContext context) {
			if(!context.CreateTableIfMissing(CREATE_SUPPLYLEDGER)) {
				return;
			}
			string where="WHERE ROUND(LevelOnHand,2)<>0";
			if(PIn.Long(Db.GetScalar("SELECT COUNT(*) FROM supply "+where))==0) {
				return;
			}
			//DateTimeEntry 0001-01-01: the stock from before tracking has no time of its own, and the same database always migrates to the same rows
			//(the adoption rehearsal compares two runs). It also sorts the opening rows first.
			context.ExecuteData("INSERT INTO supplyledger (SupplyNum,SupplyLotNum,LedgerType,Quantity,UnitCost,ProcNum,PatNum,SupplyOrderItemNum,"
				+"DispSupplyNum,UserNum,DateTimeEntry,Reason) "
				+"SELECT SupplyNum,0,"+(int)SupplyLedgerType.Opening+",ROUND(LevelOnHand,2),Price,0,0,0,0,0,'0001-01-01 00:00:00','"+REASON_OPENING+"' "
				+"FROM supply "+where+" ORDER BY SupplyNum");
		}
	}

	///<summary>Lots, default materials of procedure codes, supply settings and procedures awaiting materials. Schema only.</summary>
	[ForkMigration(2026110212,"Inventory tracking: lots, default materials, supply settings, awaiting materials")]
	public class InventoryTablesStep:ForkMigrationStep {
		public const string CREATE_SUPPLYLOT=@"CREATE TABLE `supplylot` (
  `SupplyLotNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `SupplyNum` bigint(20) NOT NULL,
  `LotNumber` varchar(255) NOT NULL,
  `DateExpiry` date NOT NULL DEFAULT '0001-01-01',
  `QtyOnHand` double NOT NULL,
  `SupplyOrderItemNum` bigint(20) NOT NULL,
  `DateTimeEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  PRIMARY KEY (`SupplyLotNum`),
  KEY `SupplyNum` (`SupplyNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_SUPPLYPROCCODE=@"CREATE TABLE `supplyproccode` (
  `SupplyProcCodeNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `CodeNum` bigint(20) NOT NULL,
  `SupplyNum` bigint(20) NOT NULL,
  `Quantity` double NOT NULL,
  `ItemOrder` int(11) NOT NULL,
  PRIMARY KEY (`SupplyProcCodeNum`),
  KEY `CodeNum` (`CodeNum`),
  KEY `SupplyNum` (`SupplyNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_SUPPLYSETTING=@"CREATE TABLE `supplysetting` (
  `SupplySettingNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `SupplyNum` bigint(20) NOT NULL,
  `ReorderLevel` double NOT NULL,
  `IsLotTracked` tinyint(4) NOT NULL,
  `IsImplant` tinyint(4) NOT NULL,
  PRIMARY KEY (`SupplySettingNum`),
  UNIQUE KEY `SupplyNum` (`SupplyNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public const string CREATE_SUPPLYPROCPENDING=@"CREATE TABLE `supplyprocpending` (
  `SupplyProcPendingNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `ProcNum` bigint(20) NOT NULL,
  `PatNum` bigint(20) NOT NULL,
  `UserNum` bigint(20) NOT NULL,
  `DateTimeEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  PRIMARY KEY (`SupplyProcPendingNum`),
  UNIQUE KEY `ProcNum` (`ProcNum`),
  KEY `PatNum` (`PatNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(CREATE_SUPPLYLOT);
			context.CreateTableIfMissing(CREATE_SUPPLYPROCCODE);
			context.CreateTableIfMissing(CREATE_SUPPLYSETTING);
			context.CreateTableIfMissing(CREATE_SUPPLYPROCPENDING);
		}
	}
}
