// Inventory tracking (docs/specs/inventory-tracking.md): the seven fork-only tables, the steps that create them and the opening balances. Levels
// 2026110211 to 2026110213 of the inventory range 2026110211 to 2026110219. The preferences, the alert category links and the Inventory Adjust
// grant are written when the office turns tracking on (InventoryTracking.SaveSettings), not here, so adopting an Open Dental database changes no
// Open Dental table for this feature.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	///<summary>The inventory tracking tables, exactly as SHOW CREATE TABLE prints them on MariaDB 10.11. Not Open Dental schema: the schema
	///verifier allows them as extra tables and ForkTables.VerifyDatabase checks them. The steps below create them from these definitions. Of the
	///seven, only supplyledger gets rows on adoption (the opening balances, InventoryLedgerStep); the adoption tools hold it to those rows
	///(tools/AdoptionValidation, InventoryPlan) and the other six to empty, so an adopted database has no open stock count.</summary>
	public class InventoryTables:ForkTableSet {
		public const string TABLE_LEDGER="supplyledger";
		public const string TABLE_LOT="supplylot";
		public const string TABLE_PROC_CODE="supplyproccode";
		public const string TABLE_SETTING="supplysetting";
		public const string TABLE_PROC_PENDING="supplyprocpending";
		public const string TABLE_COUNT="supplycount";
		public const string TABLE_COUNT_LINE="supplycountline";

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
  `SupplyCountLineNum` bigint(20) NOT NULL DEFAULT 0,
  PRIMARY KEY (`SupplyLedgerNum`),
  KEY `SupplyNum` (`SupplyNum`),
  KEY `SupplyLotNum` (`SupplyLotNum`),
  KEY `ProcNum` (`ProcNum`),
  KEY `SupplyOrderItemNum` (`SupplyOrderItemNum`),
  KEY `DateTimeEntry` (`DateTimeEntry`),
  KEY `SupplyCountLineNum` (`SupplyCountLineNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

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

		///<summary>Stock counts (owner decision INV 11): at most one is Open or Posting; the key on CountStatus finds it.</summary>
		public const string CREATE_SUPPLYCOUNT=@"CREATE TABLE `supplycount` (
  `SupplyCountNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `CountStatus` tinyint(4) NOT NULL,
  `UserNum` bigint(20) NOT NULL,
  `DateTimeEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `UserNumClosed` bigint(20) NOT NULL,
  `DateTimeClosed` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  `Reason` varchar(255) NOT NULL,
  PRIMARY KEY (`SupplyCountNum`),
  KEY `CountStatus` (`CountStatus`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		///<summary>The counted lines of the stock counts. The unique key makes a supply and lot counted again replace its line (INSERT ... ON
		///DUPLICATE KEY UPDATE, SupplyCountLines.Upsert); 3 bigints and a varchar(255) in utf8mb3 fit MyISAM's 1000-byte key limit.</summary>
		public const string CREATE_SUPPLYCOUNTLINE=@"CREATE TABLE `supplycountline` (
  `SupplyCountLineNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `SupplyCountNum` bigint(20) NOT NULL,
  `SupplyNum` bigint(20) NOT NULL,
  `SupplyLotNum` bigint(20) NOT NULL,
  `LotNumber` varchar(255) NOT NULL,
  `DateExpiry` date NOT NULL DEFAULT '0001-01-01',
  `QtyProgram` double NOT NULL,
  `QtyCounted` double NOT NULL,
  `QtyPosted` double NOT NULL,
  `UserNum` bigint(20) NOT NULL,
  `DateTimeEntry` datetime NOT NULL DEFAULT '0001-01-01 00:00:00',
  PRIMARY KEY (`SupplyCountLineNum`),
  UNIQUE KEY `SupplyCountNum` (`SupplyCountNum`,`SupplyNum`,`SupplyLotNum`,`LotNumber`),
  KEY `SupplyNum` (`SupplyNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";

		public override string Feature {
			get { return "Inventory tracking"; }
		}

		public override string Levels {
			get { return "2026110211 to 2026110213"; }
		}

		public override string[] ArrayCreateTables {
			get { return new[] { CREATE_SUPPLYLEDGER,CREATE_SUPPLYLOT,CREATE_SUPPLYPROCCODE,CREATE_SUPPLYSETTING,CREATE_SUPPLYPROCPENDING,
				CREATE_SUPPLYCOUNT,CREATE_SUPPLYCOUNTLINE }; }
		}
	}

	///<summary>The stock ledger, and in the same step the opening balances: an Opening row for every supply whose LevelOnHand is not 0 (rounded to 2
	///decimals, at the supply's price), so supply.LevelOnHand equals the sum of its ledger rows from the start. The opening rows are written only
	///when this step created the table (README rule 4), never again. supply itself is not changed.</summary>
	[ForkMigration(2026110211,"Inventory tracking: stock ledger and opening balances")]
	public class InventoryLedgerStep:ForkMigrationStep {
		///<summary>The reason written on the opening rows.</summary>
		public const string REASON_OPENING="Opening balance";

		public override void Apply(ForkMigrationContext context) {
			if(!context.CreateTableIfMissing(InventoryTables.CREATE_SUPPLYLEDGER)) {
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

	///<summary>Lots, default materials of procedure codes, supply settings and procedures awaiting materials. Creates the empty tables; writes no
	///row.</summary>
	[ForkMigration(2026110212,"Inventory tracking: lots, default materials, supply settings, awaiting materials")]
	public class InventoryTablesStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(InventoryTables.CREATE_SUPPLYLOT);
			context.CreateTableIfMissing(InventoryTables.CREATE_SUPPLYPROCCODE);
			context.CreateTableIfMissing(InventoryTables.CREATE_SUPPLYSETTING);
			context.CreateTableIfMissing(InventoryTables.CREATE_SUPPLYPROCPENDING);
		}
	}

	///<summary>Stock counts saved while they are counted (owner decision INV 11, 2026-11-02): the counts and their counted lines. Creates the two
	///empty tables; writes no row, so a database migrated or adopted has no open count. A stock ledger that an earlier build created (2026110211
	///without the column) gets supplyledger.SupplyCountLineNum, 0 on every row it has, and its key: the count line a CountAdjust row was posted
	///from, so a post that failed or did not finish is opened again knowing which lines it adjusted. 2026110211 creates the ledger with them.
	///</summary>
	[ForkMigration(2026110213,"Inventory tracking: stock counts saved line by line")]
	public class InventoryCountsStep:ForkMigrationStep {
		public const string COLUMN_LEDGER_COUNT_LINE="SupplyCountLineNum";

		public override void Apply(ForkMigrationContext context) {
			context.CreateTableIfMissing(InventoryTables.CREATE_SUPPLYCOUNT);
			context.CreateTableIfMissing(InventoryTables.CREATE_SUPPLYCOUNTLINE);
			if(!context.TableExists(InventoryTables.TABLE_LEDGER)) {
				return;//No ledger yet: 2026110211, which runs first, creates it with the column.
			}
			context.AddColumnIfMissing(InventoryTables.TABLE_LEDGER,COLUMN_LEDGER_COUNT_LINE,"bigint(20) NOT NULL DEFAULT 0","Reason");
			context.EnsureIndex(InventoryTables.TABLE_LEDGER,COLUMN_LEDGER_COUNT_LINE,"`"+COLUMN_LEDGER_COUNT_LINE+"`");
		}
	}
}
