// Low-stock and expiring alerts through the alert system: raised once, kept once when refreshed again or by two workstations, removed when they
// no longer apply, and linked to the "All" alert category.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace InventoryTests {
	[TestClass]
	public class AlertTests {
		[TestInitialize]
		public void TestInitialize() {
			InvDb.Begin();
		}

		private static List<AlertItem> GetAlerts(AlertType alertType,long fKey) {
			return AlertItems.RefreshForType(alertType).FindAll(x => x.FKey==fKey);
		}

		[TestMethod]
		public void LowStock_RaisedAtOrBelowTheReorderLevel_OnceOnly_AndRemovedWhenRestocked() {
			Supply supply=InvData.Supply(onHand:6,reorderLevel:5);
			InventoryTracking.RefreshLowStockAlerts(new List<long> { supply.SupplyNum });
			Assert.AreEqual(0,GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum).Count,"6 is above 5.");
			InventoryTracking.Adjust(supply.SupplyNum,0,-1,"Used");//5: at the level. Every movement refreshes the alert of its supply.
			List<AlertItem> listAlertItems=GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum);
			Assert.AreEqual(1,listAlertItems.Count);
			Assert.AreEqual(-1,listAlertItems[0].ClinicNum);
			Assert.AreEqual(FormType.FormSupplyInventory,listAlertItems[0].FormToOpen);
			Assert.AreEqual(ActionType.MarkAsRead|ActionType.OpenForm,listAlertItems[0].Actions);
			StringAssert.Contains(listAlertItems[0].Description,supply.Descript);
			InventoryTracking.Adjust(supply.SupplyNum,0,-1,"Used");
			InventoryTracking.RefreshLowStockAlerts(new List<long> { supply.SupplyNum });
			InventoryTracking.RefreshAllAlerts();
			Assert.AreEqual(1,GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum).Count,"Still one alert.");
			Assert.AreEqual(listAlertItems[0].AlertItemNum,GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum)[0].AlertItemNum,"The same row, so a read mark stays.");
			InventoryTracking.Adjust(supply.SupplyNum,0,10,"Restocked");
			Assert.AreEqual(0,GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum).Count);
		}

		[TestMethod]
		public void LowStock_NoAlertWithoutAReorderLevel_OrForAHiddenSupply() {
			Supply supply=InvData.Supply(onHand:0);
			Supply supplyHidden=InvData.Supply(onHand:0,reorderLevel:3);
			Supply supplyHiddenDb=Supplies.GetSupply(supplyHidden.SupplyNum);
			supplyHiddenDb.IsHidden=true;
			Supplies.Update(supplyHiddenDb);
			InventoryTracking.RefreshAllAlerts();
			Assert.AreEqual(0,GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum).Count);
			Assert.AreEqual(0,GetAlerts(AlertType.InventoryLowStock,supplyHidden.SupplyNum).Count);
		}

		///<summary>Two workstations that both saw no alert and both add one: the INSERT ... WHERE NOT EXISTS adds only the first. Copies that exist
		///anyway (rows written by an older build) are cleaned up by the next refresh.</summary>
		[TestMethod]
		public void Alerts_NeverDuplicated() {
			Supply supply=InvData.Supply(onHand:1,reorderLevel:2);
			AlertItems.DeleteFor(AlertType.InventoryLowStock,new List<long> { supply.SupplyNum });
			Assert.IsTrue(InventoryTracking.InsertAlertIfMissing(AlertType.InventoryLowStock,supply.SupplyNum,"Low stock: first"));
			Assert.IsFalse(InventoryTracking.InsertAlertIfMissing(AlertType.InventoryLowStock,supply.SupplyNum,"Low stock: second"));
			Assert.AreEqual(1,GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum).Count);
			AlertItems.Insert(new AlertItem { Type=AlertType.InventoryLowStock,FKey=supply.SupplyNum,ClinicNum=-1,Description="copy",ItemValue="" });
			Assert.AreEqual(2,GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum).Count);
			InventoryTracking.RefreshLowStockAlerts(new List<long> { supply.SupplyNum });
			List<AlertItem> listAlertItems=GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum);
			Assert.AreEqual(1,listAlertItems.Count);
			StringAssert.Contains(listAlertItems[0].Description,supply.Descript,"The text is brought up to date.");
		}

		///<summary>With random primary keys (replication), the alert's INSERT ... SELECT takes its key from ReplicationServers.GetKey, as the generated
		///CRUD does, never the next AUTO_INCREMENT value, which two replicated servers would hand out twice.</summary>
		[TestMethod]
		public void Alerts_WithRandomKeys_TakeAReplicationKey_NotTheNextAutoIncrement() {
			Supply supply=InvData.Supply(onHand:0,reorderLevel:2);
			Supply supplyAuto=InvData.Supply(onHand:0,reorderLevel:2);
			AlertItems.DeleteFor(AlertType.InventoryLowStock,new List<long> { supply.SupplyNum,supplyAuto.SupplyNum });
			Assert.IsFalse(PrefC.RandomKeys,"The test database starts without random keys.");
			Prefs.UpdateBool(PrefName.RandomPrimaryKeys,true);
			Cache.Refresh(InvalidType.Prefs);
			long alertItemNumRandom;
			long autoIncrementBefore;
			try {
				Assert.IsTrue(PrefC.RandomKeys);
				autoIncrementBefore=PIn.Long(DataCore.GetScalar("SELECT COALESCE(MAX(AlertItemNum),0)+1 FROM alertitem"));
				Assert.IsTrue(InventoryTracking.InsertAlertIfMissing(AlertType.InventoryLowStock,supply.SupplyNum,"Low stock: random key"));
				Assert.IsFalse(InventoryTracking.InsertAlertIfMissing(AlertType.InventoryLowStock,supply.SupplyNum,"Low stock: again"),"Still only once.");
				List<AlertItem> listAlertItems=GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum);
				Assert.AreEqual(1,listAlertItems.Count);
				alertItemNumRandom=listAlertItems[0].AlertItemNum;
			}
			finally {
				Prefs.UpdateBool(PrefName.RandomPrimaryKeys,false);
				Cache.Refresh(InvalidType.Prefs);
			}
			Assert.AreNotEqual(autoIncrementBefore,alertItemNumRandom,"Not the next AUTO_INCREMENT value.");
			Assert.IsTrue(alertItemNumRandom>10000,"In the range ReplicationServers.GetKey gives (above 10000 without a server range).");
			//Without random keys the database numbers the row, as before.
			Assert.IsTrue(InventoryTracking.InsertAlertIfMissing(AlertType.InventoryLowStock,supplyAuto.SupplyNum,"Low stock: auto"));
			Assert.AreEqual(1,GetAlerts(AlertType.InventoryLowStock,supplyAuto.SupplyNum).Count);
			string source=System.IO.File.ReadAllText(InvDb.FindRepoFile(System.IO.Path.Combine("OpenPmsDentalBusiness","Db Multi Table","InventoryTracking.cs")));
			StringAssert.Contains(source,"POut.Long(ReplicationServers.GetKey(\"alertitem\",\"AlertItemNum\"))");
		}

		[TestMethod]
		public void Expiring_RaisedWithinTheLeadDays_TextChangesWhenExpired_RemovedWhenUsedUp() {
			Supply supply=InvData.Supply(isLotTracked:true);
			int n=InvData.Next();
			SupplyLot supplyLotSoon=InvData.Lot(supply,"EXP-SOON-"+n,DateTime.Today.AddDays(10),2);
			SupplyLot supplyLotLate=InvData.Lot(supply,"EXP-LATE-"+n,DateTime.Today.AddDays(60),2);
			SupplyLot supplyLotGone=InvData.Lot(supply,"EXP-GONE-"+n,DateTime.Today.AddDays(-3),1);
			SupplyLot supplyLotNoDate=InvData.Lot(supply,"EXP-NONE-"+n,DateTime.MinValue,1);
			Assert.AreEqual(30,InventoryTracking.GetExpiryAlertDays());
			InventoryTracking.RefreshAllAlerts();
			InventoryTracking.RefreshAllAlerts();
			List<AlertItem> listSoon=GetAlerts(AlertType.InventoryExpiring,supplyLotSoon.SupplyLotNum);
			Assert.AreEqual(1,listSoon.Count);
			StringAssert.StartsWith(listSoon[0].Description,"Expires ");
			StringAssert.Contains(listSoon[0].Description,"lot EXP-SOON-"+n);
			Assert.AreEqual(0,GetAlerts(AlertType.InventoryExpiring,supplyLotLate.SupplyLotNum).Count,"60 days is beyond the 30 lead days.");
			Assert.AreEqual(0,GetAlerts(AlertType.InventoryExpiring,supplyLotNoDate.SupplyLotNum).Count);
			List<AlertItem> listGone=GetAlerts(AlertType.InventoryExpiring,supplyLotGone.SupplyLotNum);
			Assert.AreEqual(1,listGone.Count);
			StringAssert.StartsWith(listGone[0].Description,"Expired ");
			InventoryTracking.Adjust(supply.SupplyNum,supplyLotGone.SupplyLotNum,-1,"Thrown away");
			InventoryTracking.RefreshAllAlerts();
			Assert.AreEqual(0,GetAlerts(AlertType.InventoryExpiring,supplyLotGone.SupplyLotNum).Count);
			Assert.AreEqual(1,GetAlerts(AlertType.InventoryExpiring,supplyLotSoon.SupplyLotNum).Count);
		}

		[TestMethod]
		public void TurningTrackingOff_RemovesTheAlerts_AndTheThrottledRefreshDoesNothing() {
			Supply supply=InvData.Supply(onHand:0,reorderLevel:1);
			InventoryTracking.RefreshLowStockAlerts(new List<long> { supply.SupplyNum });
			Assert.AreEqual(1,GetAlerts(AlertType.InventoryLowStock,supply.SupplyNum).Count);
			PrefC.UpsertRaw(InventoryTracking.PREF_ENABLED,"0");//The shared helper for fork-owned preference rows.
			Cache.Refresh(InvalidType.Prefs);
			try {
				InventoryTracking.DeleteAllAlerts();
				InventoryTracking.RefreshAlertsIfDue();
				Assert.AreEqual(0,AlertItems.RefreshForType(AlertType.InventoryLowStock).Count);
			}
			finally {
				PrefC.UpsertRaw(InventoryTracking.PREF_ENABLED,"1");
				Cache.Refresh(InvalidType.Prefs);
			}
		}

		[TestMethod]
		public void AlertTypes_AreLinkedToTheAllCategory() {
			long alertCategoryNum=PIn.Long(DataCore.GetScalar("SELECT AlertCategoryNum FROM alertcategory WHERE InternalName='OdAllTypes' AND IsHQCategory=1"));
			Assert.AreNotEqual(0,alertCategoryNum);
			foreach(AlertType alertType in new[] { AlertType.InventoryLowStock,AlertType.InventoryExpiring }) {
				Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM alertcategorylink WHERE AlertCategoryNum="+alertCategoryNum+" AND AlertType="
					+(int)alertType),alertType.ToString());
				Assert.AreEqual((int)alertType,PIn.Int(DataCore.GetScalar("SELECT AlertType FROM alertcategorylink WHERE AlertCategoryNum="+alertCategoryNum
					+" AND AlertType="+(int)alertType)),"The tinyint column holds the value.");
			}
		}
	}
}
