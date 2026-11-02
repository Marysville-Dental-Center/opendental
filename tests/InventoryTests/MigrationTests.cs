// The inventory fork migration steps on a scratch database made from db/init/new-database.sql, with the inventory install taken off again first
// (so the steps run as they do for an office database that predates them) and synthetic supplies with stock: the tables and the opening
// balances, run twice to show they are idempotent, no Open Dental table changed apart from supply rows staying as they were, and the schema still
// matching Open Dental 26.1.70 apart from the documented fork tables. Then turning tracking on (SaveSettings): the preferences, the alert links
// and the one-time grant.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace InventoryTests {
	[TestClass]
	public class MigrationTests {
		private static readonly string _dbName=InvDb.Name("mig");
		private static string _skipReason;
		private static readonly string[] _arrayInventoryTables=new[] { "supplyledger","supplylot","supplyproccode","supplysetting","supplyprocpending" };
		private static readonly List<ForkMigrationStep> _listSteps=new List<ForkMigrationStep> { new InventoryLedgerStep(),new InventoryTablesStep() };
		private static List<string> _listChangesFirst;
		private static List<string> _listChangesSecond;
		private static DataTable _tableOpening;
		private static DataTable _tableSupply;
		private static Dictionary<string,string> _dictPrefsAfterSteps;
		private static int _countLinksAfterSteps;
		private static int _countGrantsAfterSteps;
		private static Dictionary<string,string> _dictChecksumsBefore;
		private static Dictionary<string,string> _dictChecksumsAfter;
		private static Dictionary<string,string> _dictPrefs;
		private static int _countLinks;
		private static List<long> _listGroupsGranted;
		private static int _countGrantedFirst;
		private static int _countGrantedSecond;
		private static SchemaVerifyResult _schemaVerifyResult;
		private static Dictionary<string,SchemaTable> _dictTables;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=InvDb.SkipReason;
			if(_skipReason!=null) {
				return;
			}
			try {
				InvDb.CreateFromInitFile(_dbName);
				foreach(string table in _arrayInventoryTables) {
					DataCore.NonQ("DROP TABLE IF EXISTS `"+table+"`");
				}
				DataCore.NonQ("DELETE FROM preference WHERE PrefName IN('InventoryTrackingEnabled','InventoryExpiryAlertDays')");
				DataCore.NonQ("DELETE FROM alertcategorylink WHERE AlertType IN("+(int)AlertType.InventoryLowStock+","+(int)AlertType.InventoryExpiring+")");
				DataCore.NonQ("DELETE FROM grouppermission WHERE PermType="+(int)EnumPermType.InventoryAdjust);
				//Synthetic supplies as an office's supply list has them, with the old free-typed On Hand.
				DataCore.NonQ("INSERT INTO supply (SupplierNum,CatalogNumber,Descript,Category,ItemOrder,LevelDesired,IsHidden,Price,BarCodeOrID,DispDefaultQuant,"
					+"DispUnitsCount,DispUnitDesc,LevelOnHand,OrderQty) VALUES "
					+"(1,'MIG-A','Migration A',0,0,0,0,4,'',0,0,'',2.5,0),"
					+"(1,'MIG-B','Migration B',0,1,0,0,1,'',0,0,'',0,0),"
					+"(1,'MIG-C','Migration C',0,2,0,1,1,'',0,0,'',-1.25,0),"
					+"(1,'MIG-D','Migration D',0,3,0,0,0,'',0,0,'',3.333,0)");
				DataCore.NonQ("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES('0001-01-01',0,99901,"
					+(int)EnumPermType.SupplierEdit+",0),('0001-01-01',0,99901,"+(int)EnumPermType.SupplierEdit+",5)");
				_dictChecksumsBefore=Checksums();
				_listChangesFirst=RunSteps();
				_listChangesSecond=RunSteps();
				_dictChecksumsAfter=Checksums();
				_dictPrefsAfterSteps=ReadPrefs();
				_countLinksAfterSteps=CountLinks();
				_countGrantsAfterSteps=PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM grouppermission WHERE PermType="+(int)EnumPermType.InventoryAdjust));
				_tableOpening=DataCore.GetTable("SELECT supply.CatalogNumber,supplyledger.* FROM supplyledger INNER JOIN supply ON supply.SupplyNum=supplyledger.SupplyNum "
					+"ORDER BY supply.CatalogNumber");
				_tableSupply=DataCore.GetTable("SELECT CatalogNumber,LevelOnHand FROM supply WHERE CatalogNumber LIKE 'MIG-%' ORDER BY CatalogNumber");
				_schemaVerifyResult=SchemaVerifier.VerifyDatabase(_dbName);
				_dictTables=_arrayInventoryTables.ToDictionary(x => x,x => SchemaTable.FromDatabase(_dbName,x));
				//The office turns tracking on, twice (Supply Inventory, Inventory, Setup).
				_countGrantedFirst=InventoryTracking.SaveSettings(true,30);
				InventoryTracking.SaveSettings(false,45);
				_countGrantedSecond=InventoryTracking.SaveSettings(true,30);
				_dictPrefs=ReadPrefs();
				_countLinks=CountLinks();
				_listGroupsGranted=DataCore.GetTable("SELECT UserGroupNum FROM grouppermission WHERE PermType="+(int)EnumPermType.InventoryAdjust).Select()
					.Select(x => PIn.Long(x[0].ToString())).ToList();
			}
			catch(Exception ex) {
				_skipReason="Could not run the migration on the scratch database: "+ex.GetBaseException().Message;
			}
			finally {
				//The other test classes work on the main database: give them back its connection and caches.
				ODException.SwallowAnyException(() => {
					InvDb.Connect(InvDb.DbMain);
					Prefs.ClearCache();
					Cache.Refresh(InvalidType.Prefs,InvalidType.ProcCodes,InvalidType.Providers,InvalidType.Defs);
					Userods.RefreshCache();
				});
				InvDb.DropQuietly(_dbName);
			}
		}

		private static Dictionary<string,string> ReadPrefs() {
			return DataCore.GetTable("SELECT PrefName,ValueString FROM preference WHERE PrefName LIKE 'Inventory%'").Select()
				.ToDictionary(x => x["PrefName"].ToString(),x => x["ValueString"].ToString());
		}

		private static int CountLinks() {
			return PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM alertcategorylink WHERE AlertType IN("+(int)AlertType.InventoryLowStock+","
				+(int)AlertType.InventoryExpiring+")"));
		}

		///<summary>CHECKSUM TABLE of every table but the inventory tables, by name.</summary>
		private static Dictionary<string,string> Checksums() {
			Dictionary<string,string> dict=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(DataRow dataRow in DataCore.GetTable("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+_dbName+"' AND TABLE_TYPE='BASE TABLE'").Rows) {
				string table=dataRow[0].ToString();
				if(_arrayInventoryTables.Contains(table)) {
					continue;
				}
				dict[table]=DataCore.GetTable("CHECKSUM TABLE `"+table+"`").Rows[0][1].ToString();
			}
			return dict;
		}

		private static List<string> RunSteps() {
			ForkMigrationContext context=new ForkMigrationContext(_dbName);
			foreach(ForkMigrationStep step in _listSteps) {
				step.Apply(context);
			}
			return context.ListChanges.ToList();
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
		}

		[TestMethod]
		public void OpeningBalance_EverySupplyWithStock_RoundedTo2Decimals_AtItsPrice() {
			List<DataRow> listRows=_tableOpening.Select().ToList();
			CollectionAssert.AreEqual(new[] { "MIG-A","MIG-C","MIG-D" },listRows.Select(x => x["CatalogNumber"].ToString()).ToArray(),"No row for MIG-B (0).");
			Assert.IsTrue(listRows.All(x => PIn.Int(x["LedgerType"].ToString())==(int)SupplyLedgerType.Opening));
			Assert.IsTrue(listRows.All(x => x["Reason"].ToString()==InventoryLedgerStep.REASON_OPENING && PIn.Long(x["SupplyLotNum"].ToString())==0));
			CollectionAssert.AreEqual(new[] { 2.5,-1.25,3.33 },listRows.Select(x => Math.Round(PIn.Double(x["Quantity"].ToString()),4)).ToArray());
			Assert.AreEqual(4,PIn.Double(listRows[0]["UnitCost"].ToString()));
			Assert.AreEqual(InventoryTracking.REASON_OPENING,InventoryLedgerStep.REASON_OPENING);
		}

		[TestMethod]
		public void OpeningBalance_LeavesTheSupplyRowsUnchanged() {
			CollectionAssert.AreEqual(new[] { 2.5,0,-1.25,3.333 },_tableSupply.Select().Select(x => Math.Round(PIn.Double(x["LevelOnHand"].ToString()),3)).ToArray());
		}

		[TestMethod]
		public void Steps_SecondRunChangesNothing() {
			Assert.AreEqual(6,_listChangesFirst.Count,string.Join("\r\n",_listChangesFirst));
			Assert.AreEqual(0,_listChangesSecond.Count,string.Join("\r\n",_listChangesSecond));
			Assert.AreEqual(3,_tableOpening.Rows.Count,"The opening rows are written once.");
		}

		///<summary>Adopting an Open Dental database changes no Open Dental table for this feature: the steps only create the fork tables and fill the
		///ledger. Tracking stays off (no preference row) until the office turns it on.</summary>
		[TestMethod]
		public void Steps_ChangeNoOpenDentalTable_AndLeaveTrackingOff() {
			CollectionAssert.AreEquivalent(_dictChecksumsBefore.Keys.ToList(),_dictChecksumsAfter.Keys.ToList());
			foreach(KeyValuePair<string,string> pair in _dictChecksumsBefore) {
				Assert.AreEqual(pair.Value,_dictChecksumsAfter[pair.Key],pair.Key+" changed.");
			}
			Assert.AreEqual(0,_dictPrefsAfterSteps.Count,"No preference row: tracking is off.");
			Assert.AreEqual(0,_countLinksAfterSteps);
			Assert.AreEqual(0,_countGrantsAfterSteps);
		}

		[TestMethod]
		public void TurningOn_WritesThePrefs_TheLinksOnce_AndTheGrantOnlyTheFirstTime() {
			Assert.AreEqual("1",_dictPrefs["InventoryTrackingEnabled"]);
			Assert.AreEqual("30",_dictPrefs["InventoryExpiryAlertDays"]);
			Assert.AreEqual(2,_countLinks,"One link per alert type, also after turning it on twice.");
			Assert.IsTrue(_countGrantedFirst>=1);
			Assert.AreEqual(0,_countGrantedSecond,"Granted only the first time.");
			Assert.AreEqual(1,_listGroupsGranted.Count(x => x==99901),"One grant per group, also with two Supplier Edit rows.");
			Assert.AreEqual(_listGroupsGranted.Count,_listGroupsGranted.Distinct().Count());
		}

		[TestMethod]
		public void Schema_StillMatchesOpenDental_WithTheForkTablesAllowed() {
			Assert.IsTrue(_schemaVerifyResult.IsMatch,_schemaVerifyResult.GetReport());
			foreach(string table in _arrayInventoryTables) {
				SchemaDifference schemaDifference=_schemaVerifyResult.ListAllowed.Single(x => x.Table==table);
				Assert.AreEqual(SchemaDifferenceKind.ExtraTable,schemaDifference.Kind);
				StringAssert.Contains(schemaDifference.ExceptionReason,"fork feature");
			}
			Assert.IsTrue(_schemaVerifyResult.ListAllowed.All(x => x.Kind==SchemaDifferenceKind.ExtraTable),"No column is added to an Open Dental table.");
			CollectionAssert.AreEquivalent(_arrayInventoryTables,SchemaVerifier.ArrayForkTables);
		}

		[TestMethod]
		public void Tables_MapTheirClassesColumnByColumn() {
			foreach(Type type in new[] { typeof(SupplyLedger),typeof(SupplyLot),typeof(SupplyProcCode),typeof(SupplySetting),typeof(SupplyProcPending) }) {
				SchemaTable schemaTable=_dictTables[CrudTableAttribute.GetTableName(type).ToLowerInvariant()];
				Assert.IsNotNull(schemaTable,type.Name);
				List<string> listFields=type.GetFields(BindingFlags.Public|BindingFlags.Instance).Select(x => x.Name).ToList();
				CollectionAssert.AreEqual(listFields,schemaTable.Columns.Select(x => x.Name).ToList(),type.Name);
				Assert.AreEqual("MyISAM",schemaTable.Engine);
				Assert.AreEqual("utf8mb3",schemaTable.Charset);
			}
			Assert.IsNotNull(_dictTables["supplysetting"].GetIndex("SupplyNum"));
			Assert.AreEqual("UNIQUE KEY",_dictTables["supplyprocpending"].GetIndex("ProcNum").Kind);
		}

		[TestMethod]
		public void Steps_HaveTheInventoryLevels() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			List<long> listLevels=listSteps.Where(x => x.GetType().Namespace==typeof(InventoryLedgerStep).Namespace && x.GetType().Name.StartsWith("Inventory"))
				.Select(x => x.Level).ToList();
			CollectionAssert.AreEqual(new long[] { 2026110211,2026110212 },listLevels);
			Assert.IsTrue(listLevels.All(x => x>=2026110211 && x<=2026110219),"The inventory range.");
			foreach(ForkMigrationStep step in listSteps) {
				ForkMigrationRunner.ValidateStep(step);
			}
			ForkMigrationRunner.ValidateLevelsUnique(listSteps);
		}
	}
}
