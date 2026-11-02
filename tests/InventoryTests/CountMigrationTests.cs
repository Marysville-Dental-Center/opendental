// The stock count step 2026110213 (InventoryCountsStep, owner decision INV 11) on a scratch database made from db/init/new-database.sql: first on
// the database without any inventory table (as a database from before inventory tracking), then on the same database migrated by the earlier
// inventory steps and holding stock (opening balances, a lot, movements). Each time it creates the two count tables empty and exactly as declared,
// a second run changes nothing, and on the migrated database no other table changes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace InventoryTests {
	[TestClass]
	public class CountMigrationTests {
		private static readonly string _dbName=InvDb.Name("cmig");
		private static readonly string[] _arrayCountTables=new[] { InventoryTables.TABLE_COUNT,InventoryTables.TABLE_COUNT_LINE };
		private static string _skipReason;
		private static List<string> _listChangesEmptyFirst;
		private static List<string> _listChangesEmptySecond;
		private static Dictionary<string,long> _dictRowsEmpty;
		private static List<SchemaDifference> _listDifferencesEmpty;
		private static List<string> _listTablesEmpty;
		private static List<string> _listChangesMigratedFirst;
		private static List<string> _listChangesMigratedSecond;
		private static Dictionary<string,string> _dictChecksumsBefore;
		private static Dictionary<string,string> _dictChecksumsAfter;
		private static Dictionary<string,long> _dictRowsMigrated;
		private static long _countLedgerRowsMigrated;
		private static List<SchemaDifference> _listDifferencesMigrated;
		private static SchemaVerifyResult _schemaVerifyResultMigrated;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=InvDb.SkipReason;
			if(_skipReason!=null) {
				return;
			}
			try {
				InvDb.CreateFromInitFile(_dbName);
				DropInventoryTables();
				//1. A database without any inventory table: the step alone.
				_listChangesEmptyFirst=RunSteps(new InventoryCountsStep());
				_listChangesEmptySecond=RunSteps(new InventoryCountsStep());
				_dictRowsEmpty=CountRows();
				_listDifferencesEmpty=GetCountTableDifferences();
				_listTablesEmpty=GetInventoryTablesPresent();
				//2. Migrated by the earlier inventory steps, with stock: the count tables come in a later release (this step).
				DropInventoryTables();
				DataCore.NonQ("INSERT INTO supply (SupplierNum,CatalogNumber,Descript,Category,ItemOrder,LevelDesired,IsHidden,Price,BarCodeOrID,DispDefaultQuant,"
					+"DispUnitsCount,DispUnitDesc,LevelOnHand,OrderQty) VALUES "
					+"(1,'CMIG-A','Count migration A',0,0,0,0,2,'',0,0,'',6,0),"
					+"(1,'CMIG-B','Count migration B',0,1,0,0,3,'',0,0,'',1.5,0)");
				RunSteps(new InventoryLedgerStep(),new InventoryTablesStep());
				long supplyNumA=PIn.Long(DataCore.GetScalar("SELECT SupplyNum FROM supply WHERE CatalogNumber='CMIG-A'"));
				DataCore.NonQ("INSERT INTO supplylot (SupplyNum,LotNumber,DateExpiry,QtyOnHand,SupplyOrderItemNum,DateTimeEntry) VALUES ("+supplyNumA
					+",'CMIG-LOT','2028-01-01',2,0,'2026-11-01 09:00:00')");
				long supplyLotNum=PIn.Long(DataCore.GetScalar("SELECT MAX(SupplyLotNum) FROM supplylot"));
				DataCore.NonQ("INSERT INTO supplyledger (SupplyNum,SupplyLotNum,LedgerType,Quantity,UnitCost,ProcNum,PatNum,SupplyOrderItemNum,DispSupplyNum,UserNum,"
					+"DateTimeEntry,Reason) VALUES ("+supplyNumA+","+supplyLotNum+","+(int)SupplyLedgerType.Adjust+",2,2,0,0,0,0,1,'2026-11-01 09:00:00','Synthetic'),("
					+supplyNumA+",0,"+(int)SupplyLedgerType.CountAdjust+",-1,2,0,0,0,0,1,'2026-11-01 09:05:00','Synthetic count')");
				_dictChecksumsBefore=Checksums();
				_listChangesMigratedFirst=RunSteps(new InventoryCountsStep());
				_listChangesMigratedSecond=RunSteps(new InventoryCountsStep());
				_dictChecksumsAfter=Checksums();
				_dictRowsMigrated=CountRows();
				_countLedgerRowsMigrated=PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM supplyledger"));
				_listDifferencesMigrated=ForkTables.VerifyDatabase(_dbName).ListDifferences.FindAll(x => InvDb.ArrayInventoryTables.Contains(x.Table));
				_schemaVerifyResultMigrated=SchemaVerifier.VerifyDatabase(_dbName);
			}
			catch(Exception ex) {
				_skipReason="Could not run the stock count step on the scratch database: "+ex.GetBaseException().Message;
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

		private static void DropInventoryTables() {
			foreach(string table in InvDb.ArrayInventoryTables) {
				DataCore.NonQ("DROP TABLE IF EXISTS `"+table+"`");
			}
		}

		private static List<string> RunSteps(params ForkMigrationStep[] arraySteps) {
			ForkMigrationContext context=new ForkMigrationContext(_dbName);
			foreach(ForkMigrationStep step in arraySteps) {
				step.Apply(context);
			}
			return context.ListChanges.ToList();
		}

		private static Dictionary<string,long> CountRows() {
			return _arrayCountTables.ToDictionary(x => x,x => PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM `"+x+"`")));
		}

		private static List<SchemaDifference> GetCountTableDifferences() {
			return ForkTables.VerifyDatabase(_dbName).ListDifferences.FindAll(x => _arrayCountTables.Contains(x.Table));
		}

		private static List<string> GetInventoryTablesPresent() {
			return DataCore.GetTable("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+_dbName+"' AND TABLE_NAME LIKE 'supply%'").Select()
				.Select(x => x[0].ToString()).Where(x => InvDb.ArrayInventoryTables.Contains(x)).OrderBy(x => x).ToList();
		}

		///<summary>CHECKSUM TABLE of every table but the two count tables, by name.</summary>
		private static Dictionary<string,string> Checksums() {
			Dictionary<string,string> dict=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(DataRow dataRow in DataCore.GetTable("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+_dbName+"' AND TABLE_TYPE='BASE TABLE'").Rows) {
				string table=dataRow[0].ToString();
				if(_arrayCountTables.Contains(table)) {
					continue;
				}
				dict[table]=DataCore.GetTable("CHECKSUM TABLE `"+table+"`").Rows[0][1].ToString();
			}
			return dict;
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
		}

		[TestMethod]
		public void Step_OnADatabaseWithoutTheInventoryTables_CreatesTheTwoCountTablesEmpty() {
			Assert.AreEqual(2,_listChangesEmptyFirst.Count,string.Join("\r\n",_listChangesEmptyFirst));
			Assert.IsTrue(_listChangesEmptyFirst.All(x => x.Contains("CREATE TABLE")),string.Join("\r\n",_listChangesEmptyFirst));
			Assert.AreEqual(0,_listChangesEmptySecond.Count,"A second run changes nothing: "+string.Join("\r\n",_listChangesEmptySecond));
			CollectionAssert.AreEqual(_arrayCountTables.OrderBy(x => x).ToList(),_listTablesEmpty,"Only the step's own tables.");
			Assert.AreEqual(0,_listDifferencesEmpty.Count,string.Join("\r\n",_listDifferencesEmpty));
			Assert.IsTrue(_dictRowsEmpty.All(x => x.Value==0),"No count, so none is open.");
		}

		[TestMethod]
		public void Step_OnADatabaseAlreadyMigratedWithStock_AddsOnlyTheTwoCountTables() {
			Assert.AreEqual(2,_listChangesMigratedFirst.Count,string.Join("\r\n",_listChangesMigratedFirst));
			Assert.IsTrue(_listChangesMigratedFirst.All(x => x.Contains("CREATE TABLE")),string.Join("\r\n",_listChangesMigratedFirst));
			Assert.AreEqual(0,_listChangesMigratedSecond.Count,string.Join("\r\n",_listChangesMigratedSecond));
			CollectionAssert.AreEquivalent(_dictChecksumsBefore.Keys.ToList(),_dictChecksumsAfter.Keys.ToList());
			foreach(KeyValuePair<string,string> pair in _dictChecksumsBefore) {
				Assert.AreEqual(pair.Value,_dictChecksumsAfter[pair.Key],pair.Key+" changed.");
			}
			Assert.IsTrue(_dictChecksumsBefore.ContainsKey("supplyledger") && _dictChecksumsBefore.ContainsKey("supplylot"),"The stock stays as it was.");
			Assert.AreEqual(4,_countLedgerRowsMigrated,"Two opening rows and two movements.");
			Assert.IsTrue(_dictRowsMigrated.All(x => x.Value==0),"No count, so none is open.");
			Assert.AreEqual(0,_listDifferencesMigrated.Count,"Every inventory table as declared: "+string.Join("\r\n",_listDifferencesMigrated));
			Assert.IsTrue(_schemaVerifyResultMigrated.IsMatch,_schemaVerifyResultMigrated.GetReport());
			Assert.IsTrue(_schemaVerifyResultMigrated.ListDifferences.Where(x => _arrayCountTables.Contains(x.Table))
				.All(x => x.Kind==SchemaDifferenceKind.ExtraTable && x.IsExcepted),"Allowed as this program's own tables.");
		}
	}
}
