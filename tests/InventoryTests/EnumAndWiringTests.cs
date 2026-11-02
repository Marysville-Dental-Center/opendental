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
			//The tinyint enums count down from 127; inventory tracking has 119 to 112 (docs/port-notes/FORK-ENUMS.md, "tinyint enums").
			Assert.AreEqual(119,(int)AlertType.InventoryLowStock);
			Assert.AreEqual(118,(int)AlertType.InventoryExpiring);
			Assert.AreEqual(119,(int)FormType.FormSupplyInventory);
			foreach(Enum value in new Enum[] { AlertType.InventoryLowStock,AlertType.InventoryExpiring,FormType.FormSupplyInventory }) {
				Assert.IsTrue(ForkEnums.IsForkOnly(value),value+" is a fork-only value.");
				Assert.IsTrue(Convert.ToInt32(value)>=112 && Convert.ToInt32(value)<=119,value+" is in the inventory range 119-112.");
			}
			Assert.IsTrue(ForkEnums.IsForkOnly(EnumPermType.InventoryAdjust));
			//alertitem.Type, alertsub.Type, alertcategorylink.AlertType and alertitem.FormToOpen are tinyint(4): every value must fit.
			Assert.IsTrue(Enum.GetValues(typeof(AlertType)).Cast<int>().All(x => x<=127));
			Assert.IsTrue(Enum.GetValues(typeof(FormType)).Cast<int>().All(x => x<=127));
			Assert.IsFalse(GroupPermissions.IsReserved(EnumPermType.InventoryAdjust));
			Assert.IsTrue(GroupPermissions.HasAuditTrail(EnumPermType.InventoryAdjust));
			Assert.IsFalse(GroupPermissions.PermTakesDates(EnumPermType.InventoryAdjust));
		}

		[TestMethod]
		public void ForkEnumsDocument_HasTheRangesOfTheValues() {
			string text=ReadRepoFile("docs","port-notes","FORK-ENUMS.md");
			StringAssert.Contains(text,"| 10050-10099 | Inventory tracking ");
			StringAssert.Contains(text,"| 119-112               | Inventory tracking ");
			StringAssert.Contains(text,"`ForkEnums.FIRST_VALUE_TINYINT`");
			string spec=ReadRepoFile("docs","specs","inventory-tracking.md");
			StringAssert.Contains(spec,"`AlertType.InventoryLowStock` 119, `AlertType.InventoryExpiring` 118, `FormType.FormSupplyInventory` 119");
		}

		///<summary>The security tree's Set All and Set None, and the Central Manager's alert list, loop over the defined values, so a fork-only value
		///(10050, 119) is reached and no undefined value is ever written.</summary>
		[TestMethod]
		public void EnumLoops_GoOverTheDefinedValues() {
			string sourceTree=ReadRepoFile("OpenPmsDental","User Controls","UserControlSecurityTree.cs");
			Assert.IsFalse(sourceTree.Contains("(EnumPermType)i"),"No loop casts a counter to a permission.");
			Assert.AreEqual(2,sourceTree.Split(new[] { "Enum.GetValues(typeof(EnumPermType))" },StringSplitOptions.None).Length-1,"Set All and Set None.");
			string sourceCentral=ReadRepoFile("CentralManager","FormCentralUserEdit.cs");
			Assert.IsFalse(sourceCentral.Contains("(AlertType)i") || sourceCentral.Contains("(AlertType)index"),"No loop casts a counter to an alert type.");
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
			Assert.IsFalse(SchemaVerifier.DocumentedExceptions.Any(x => SchemaVerifier.IsForkTable(x.Table)),"Left out, not excepted.");
			foreach(string table in SchemaVerifier.ArrayForkTables) {
				Assert.IsFalse(SchemaVerifier.GetReference().HasTable(table),table+" is not Open Dental schema.");
			}
			//A snapshot with a fork table compares exactly with one without it.
			SchemaSnapshot snapshotOd=SchemaSnapshot.Parse("CREATE TABLE `account` (\n  `AccountNum` bigint(20) NOT NULL AUTO_INCREMENT,\n  PRIMARY KEY (`AccountNum`)\n"
				+") ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;");
			SchemaSnapshot snapshotFork=SchemaSnapshot.Parse(snapshotOd.ToDdl("")+"\n"
				+OpenPmsDentalBusiness.ForkMigrations.FeatureSteps.InventoryTablesStep.CREATE_SUPPLYLOT+";");
			Assert.AreEqual(2,snapshotFork.Tables.Count);
			SchemaVerifyResult result=SchemaVerifier.Compare(snapshotOd,snapshotFork);
			Assert.AreEqual(0,result.ListDifferences.Count,result.GetReport());
			Assert.AreEqual(0,SchemaVerifier.Compare(snapshotFork,snapshotOd).ListDifferences.Count);
		}

		[TestMethod]
		public void SpecAndReadme_DescribeTheSteps() {
			string spec=ReadRepoFile("docs","specs","inventory-tracking.md");
			string readme=ReadRepoFile("OpenPmsDentalBusiness","Misc","ForkMigrations","README.md");
			foreach(string level in new[] { "2026110211","2026110212" }) {
				StringAssert.Contains(spec,"| "+level+" |");
			}
			StringAssert.Contains(readme,"2026110211 and 2026110212 | `InventorySteps.cs`");
		}
	}
}
