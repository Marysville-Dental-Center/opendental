// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>What the fork migration's inventory tracking steps (InventoryLedgerStep 2026110211 and InventoryTablesStep 2026110212, package INV,
	///docs/specs/inventory-tracking.md) change when adoption brings an Open Dental 25.3.48 database to this program, worked out before adoption and
	///read-only. They change no Open Dental table: they create the five fork tables (SchemaVerifier.ArrayForkTables) and, in the step that creates
	///supplyledger, write one Opening row per supply whose LevelOnHand rounded to 2 decimals is not 0, at the supply's price, without a lot, user,
	///procedure or order item, with the reason "Opening balance". migration-preview prints the plan, and adopt's row check (AdoptionRowCheck) holds
	///the adopted copy's ledger to it and the other four fork tables to empty. Reads supply.</summary>
	public static class InventoryPlan {
		public const string TABLE_LEDGER="supplyledger";

		///<summary>Level of the step that writes the rows, for messages.</summary>
		public static long StepLevel {
			get { return new InventoryLedgerStep().Level; }
		}

		public class OpeningRow {
			public long SupplyNum;
			public double Quantity;
			public double UnitCost;
		}

		public class Plan {
			///<summary>True when the database already has the ledger: the steps then change nothing.</summary>
			public bool HasLedger;
			public bool HasSupplyTable;
			///<summary>The Opening rows the step writes, in SupplyNum order.</summary>
			public List<OpeningRow> ListOpening=new List<OpeningRow>();

			///<summary>What migration-preview prints for the steps.</summary>
			public List<string> Lines() {
				string step="Steps "+StepLevel+" and "+new InventoryTablesStep().Level+" (inventory tracking)";
				if(HasLedger) {
					return new List<string> { step+" change nothing: the database already has the inventory tables." };
				}
				string text=step+" create the fork tables "+string.Join(", ",SchemaVerifier.ArrayForkTables)+" and change no Open Dental table; ";
				if(ListOpening.Count==0) {
					text+="no supply has stock (supply.LevelOnHand), so the stock ledger starts empty.";
				}
				else {
					text+="the stock ledger gets "+ListOpening.Count+" opening balance row(s), one per supply with stock (SupplyNum "
						+string.Join(", ",ListOpening.Take(20).Select(x => x.SupplyNum.ToString(CultureInfo.InvariantCulture)))+(ListOpening.Count>20 ? ", ..." : "")
						+"), LevelOnHand rounded to 2 decimals at the supply's price; supply itself is not changed.";
				}
				return new List<string> { text+" Tracking stays off until the office turns it on; adopt's row check (step 4) requires exactly this." };
			}
		}

		///<summary>The plan for a database on this server, read with plain SQL.</summary>
		public static Plan Make(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			Plan plan=new Plan();
			plan.HasLedger=Migration26_1Plan.HasTable(db,database,TABLE_LEDGER);
			plan.HasSupplyTable=Migration26_1Plan.HasTable(db,database,"supply");
			if(plan.HasLedger || !plan.HasSupplyTable) {
				return plan;
			}
			plan.ListOpening=db.Rows("SELECT SupplyNum,ROUND(LevelOnHand,2),Price FROM `"+database+"`.supply WHERE ROUND(LevelOnHand,2)<>0 ORDER BY SupplyNum")
				.Select(x => new OpeningRow {
					SupplyNum=Convert.ToInt64(x[0],CultureInfo.InvariantCulture),
					Quantity=Math.Round(Convert.ToDouble(x[1],CultureInfo.InvariantCulture),2),
					UnitCost=Convert.ToDouble(x[2],CultureInfo.InvariantCulture),
				}).ToList();
			return plan;
		}
	}
}
