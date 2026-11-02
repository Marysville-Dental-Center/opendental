// Rules without a database: the fork-only enum values and their ranges, LevelOnHand excluded from Update, the security tree and the documents.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace InventoryTests {
	[TestClass]
	public class EnumAndWiringTests {
		private static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(InvDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		[TestMethod]
		public void ForkEnumValues_InTheInventoryRanges() {
			Assert.AreEqual(10050,(int)EnumPermType.InventoryAdjust,"smallint grouppermission.PermType and securitylog.PermType hold it.");
			Assert.AreEqual("Inventory Adjust",EnumPermType.InventoryAdjust.GetDescription());
			Assert.AreEqual(110,(int)AlertType.InventoryLowStock);
			Assert.AreEqual(111,(int)AlertType.InventoryExpiring);
			Assert.AreEqual(110,(int)FormType.FormSupplyInventory);
			//alertitem.Type, alertsub.Type, alertcategorylink.AlertType and alertitem.FormToOpen are tinyint(4): every value must fit.
			Assert.IsTrue(Enum.GetValues(typeof(AlertType)).Cast<int>().All(x => x<=127));
			Assert.IsTrue(Enum.GetValues(typeof(FormType)).Cast<int>().All(x => x<=127));
			Assert.IsFalse(GroupPermissions.IsReserved(EnumPermType.InventoryAdjust));
			Assert.IsTrue(GroupPermissions.HasAuditTrail(EnumPermType.InventoryAdjust));
			Assert.IsFalse(GroupPermissions.PermTakesDates(EnumPermType.InventoryAdjust));
		}

		[TestMethod]
		public void ForkEnumsDocument_ListsTheValues() {
			string text=ReadRepoFile("docs","port-notes","FORK-ENUMS.md");
			StringAssert.Contains(text,"| `EnumPermType` | 10050 | `InventoryAdjust` | Inventory tracking |");
			StringAssert.Contains(text,"| `AlertType` | 110 | `InventoryLowStock` | Inventory tracking |");
			StringAssert.Contains(text,"| `AlertType` | 111 | `InventoryExpiring` | Inventory tracking |");
			StringAssert.Contains(text,"| `FormType` | 110 | `FormSupplyInventory` | Inventory tracking |");
			StringAssert.Contains(text,"| 110-119 | Inventory tracking ");
		}

		[TestMethod]
		public void LevelOnHand_IsNeverWrittenByUpdate() {
			CrudColumnAttribute crudColumn=typeof(Supply).GetField(nameof(Supply.LevelOnHand)).GetCustomAttributes(typeof(CrudColumnAttribute),true)
				.OfType<CrudColumnAttribute>().Single();
			Assert.IsTrue(crudColumn.SpecialType.HasFlag(CrudSpecialColType.ExcludeFromUpdate));
			string crud=ReadRepoFile("OpenPmsDentalBusiness","Crud","SupplyCrud.cs");
			Assert.IsFalse(crud.Contains("LevelOnHand     =  "),"The generated Update must not set LevelOnHand.");
			Assert.IsFalse(crud.Contains("command+=\"LevelOnHand = \""));
		}

		[TestMethod]
		public void LedgerRows_CannotBeDeletedThroughTheCrud() {
			Assert.IsNull(typeof(OpenPmsDentalBusiness.Crud.SupplyLedgerCrud).GetMethod("Delete",BindingFlags.Public|BindingFlags.Static));
		}

		[TestMethod]
		public void SecurityTree_HasInventoryAdjustUnderSupplyInventory() {
			string source=ReadRepoFile("OpenPmsDental","User Controls","UserControlSecurityTree.cs");
			int idxNode=source.IndexOf("SetNode(\"Supply Inventory\")");
			int idxPerm=source.IndexOf("SetNode(EnumPermType.InventoryAdjust)");
			int idxSupplier=source.IndexOf("SetNode(EnumPermType.SupplierEdit)");
			Assert.IsTrue(idxNode>0 && idxPerm>idxNode && idxSupplier>idxPerm);
			Assert.AreEqual(1,source.Split(new[] { "SetNode(EnumPermType.InventoryAdjust)" },StringSplitOptions.None).Length-1);
		}

		[TestMethod]
		public void ForkTables_AreTheInventoryTables() {
			CollectionAssert.AreEquivalent(new[] { "supplyledger","supplylot","supplyproccode","supplysetting","supplyprocpending" },SchemaVerifier.ArrayForkTables);
			Assert.IsTrue(SchemaVerifier.DocumentedExceptions.Count(x => SchemaVerifier.ArrayForkTables.Contains(x.Table)
				&& x.Kind==SchemaDifferenceKind.ExtraTable)==SchemaVerifier.ArrayForkTables.Length);
			foreach(string table in SchemaVerifier.ArrayForkTables) {
				Assert.IsFalse(SchemaVerifier.GetReference().HasTable(table),table+" is not Open Dental schema.");
			}
		}

		[TestMethod]
		public void SpecAndReadme_DescribeTheSteps() {
			string spec=ReadRepoFile("docs","specs","inventory-tracking.md");
			string readme=ReadRepoFile("OpenPmsDentalBusiness","Misc","ForkMigrations","README.md");
			foreach(string level in new[] { "2026110211","2026110212","2026110213","2026110214","2026110215" }) {
				StringAssert.Contains(spec,"| "+level+" |");
			}
			StringAssert.Contains(readme,"2026110211 to 2026110215 | `InventorySteps.cs`");
		}
	}
}
