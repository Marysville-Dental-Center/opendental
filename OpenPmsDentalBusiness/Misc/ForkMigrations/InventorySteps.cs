// Inventory tracking (docs/specs/inventory-tracking.md): the fork tables, the opening balances, the preferences, the alert category links and the
// Inventory Adjust grant. Levels 2026110211 to 2026110215 of the inventory range 2026110211 to 2026110219.
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
			context.ExecuteData("INSERT INTO supplyledger (SupplyNum,SupplyLotNum,LedgerType,Quantity,UnitCost,ProcNum,PatNum,SupplyOrderItemNum,"
				+"DispSupplyNum,UserNum,DateTimeEntry,Reason) "
				+"SELECT SupplyNum,0,"+(int)SupplyLedgerType.Opening+",ROUND(LevelOnHand,2),Price,0,0,0,0,0,NOW(),'"+REASON_OPENING+"' "
				+"FROM supply WHERE ROUND(LevelOnHand,2)<>0 ORDER BY SupplyNum");
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

	///<summary>The two preferences, when missing: InventoryTrackingEnabled off on every database (the office turns it on), InventoryExpiryAlertDays
	///30.</summary>
	[ForkMigration(2026110213,"Inventory tracking: preferences")]
	public class InventoryPrefsStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			AddPrefIfMissing(context,nameof(PrefName.InventoryTrackingEnabled),"0");
			AddPrefIfMissing(context,nameof(PrefName.InventoryExpiryAlertDays),"30");
		}

		private static void AddPrefIfMissing(ForkMigrationContext context,string prefName,string valueString) {
			if(context.GetPrefRaw(prefName)!=null) {
				return;
			}
			context.ExecuteData("INSERT INTO preference (PrefName,ValueString) VALUES('"+POut.String(prefName)+"','"+POut.String(valueString)+"')");
		}
	}

	///<summary>Links the two inventory alert types to the "All" alert category (InternalName OdAllTypes), as every alert type is linked
	///(AlertType's summary), so users subscribed to All see them. Only links that are missing are added.</summary>
	[ForkMigration(2026110214,"Inventory tracking: alert category links")]
	public class InventoryAlertLinksStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			string alertCategoryNum=Db.GetScalar("SELECT AlertCategoryNum FROM alertcategory WHERE InternalName='OdAllTypes' AND IsHQCategory=1 "
				+"ORDER BY AlertCategoryNum LIMIT 1");
			if(string.IsNullOrEmpty(alertCategoryNum)) {
				context.Log("No OdAllTypes alert category; no links added.");
				return;
			}
			foreach(AlertType alertType in new[] { AlertType.InventoryLowStock,AlertType.InventoryExpiring }) {
				string where="WHERE AlertCategoryNum="+POut.Long(PIn.Long(alertCategoryNum))+" AND AlertType="+(int)alertType;
				if(PIn.Long(Db.GetScalar("SELECT COUNT(*) FROM alertcategorylink "+where))>0) {
					continue;
				}
				context.ExecuteData("INSERT INTO alertcategorylink (AlertCategoryNum,AlertType) VALUES("+POut.Long(PIn.Long(alertCategoryNum))+","
					+(int)alertType+")");
			}
		}
	}

	///<summary>Grants Inventory Adjust to every user group that holds Supplier Edit, the people who look after supplies today. Only when no group has
	///Inventory Adjust yet, so a grant the office removed later is never added back.</summary>
	[ForkMigration(2026110215,"Inventory tracking: Inventory Adjust for groups with Supplier Edit")]
	public class InventoryPermissionStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			if(PIn.Long(Db.GetScalar("SELECT COUNT(*) FROM grouppermission WHERE PermType="+(int)EnumPermType.InventoryAdjust))>0) {
				return;
			}
			if(PIn.Long(Db.GetScalar("SELECT COUNT(*) FROM grouppermission WHERE PermType="+(int)EnumPermType.SupplierEdit))==0) {
				return;
			}
			context.ExecuteData("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) "
				+"SELECT '0001-01-01',0,UserGroupNum,"+(int)EnumPermType.InventoryAdjust+",0 FROM grouppermission "
				+"WHERE PermType="+(int)EnumPermType.SupplierEdit+" GROUP BY UserGroupNum ORDER BY UserGroupNum");
		}
	}
}
