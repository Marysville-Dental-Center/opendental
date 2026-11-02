using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness{
	///<summary></summary>
	public class Supplies {
		///<summary>Gets all Supplies, ordered by category and itemOrder.  Use listCategories=null to indicate all not hidden.  Use listSupplierNums=null to indicate all suppliers.</summary>
		public static List<Supply> GetList(List<long> listSupplierNums,bool showHidden,string textFind,List<long> listCategories) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Supply>>(MethodBase.GetCurrentMethod(),listSupplierNums,showHidden,textFind,listCategories);
			}
			string command="SELECT supply.* "
				+"FROM supply,definition "
				+"WHERE definition.DefNum=supply.Category ";
			if(listSupplierNums!=null){
				command+="AND SupplierNum IN ("+String.Join(",",listSupplierNums.ConvertAll(x=>x.ToString()))+") ";
			}
			if(!showHidden){
				command+="AND supply.IsHidden=0 ";
			}
			if(textFind!=""){
				command+="AND (supply.Descript LIKE '%"+POut.String(textFind)+"%' "
					+"OR supply.CatalogNumber LIKE '%"+POut.String(textFind)+"%' "
					+"OR supply.BarCodeOrID LIKE '%"+POut.String(textFind)+"%' "//Inventory tracking: a scan in the search box finds the supply.
					+")";
			}
			if(listCategories!=null && listCategories.Count>0){
				command+="AND supply.Category IN ("+String.Join(",",listCategories.ConvertAll(x=>x.ToString()))+") ";
			}
			command+="ORDER BY definition.ItemOrder,supply.ItemOrder";
			return Crud.SupplyCrud.SelectMany(command);
		}

		///<Summary>Gets one supply from the database.  Used for display in SupplyOrderItemEdit window.</Summary>
		public static Supply GetSupply(long supplyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<Supply>(MethodBase.GetCurrentMethod(),supplyNum);
			}
			return Crud.SupplyCrud.SelectOne(supplyNum);
		}

		///<summary>Supplies by key, in no particular order. Hidden supplies included.</summary>
		public static List<Supply> GetMany(List<long> listSupplyNums) {
			if(listSupplyNums.IsNullOrEmpty()) {
				return new List<Supply>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Supply>>(MethodBase.GetCurrentMethod(),listSupplyNums);
			}
			return Crud.SupplyCrud.SelectMany("SELECT * FROM supply WHERE SupplyNum IN("+string.Join(",",listSupplyNums.Select(x => POut.Long(x)))+")");
		}

		///<summary>Inventory tracking: the supplies a barcode scan names. A scan is matched against BarCodeOrID (exactly, ignoring case and blanks
		///around it); when no supply has that barcode, against CatalogNumber. Supplies that are not hidden come first. Empty for a blank code.</summary>
		public static List<Supply> GetByScan(string code) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Supply>>(MethodBase.GetCurrentMethod(),code);
			}
			code=(code??"").Trim();
			if(code=="") {
				return new List<Supply>();
			}
			List<Supply> listSupplies=Crud.SupplyCrud.SelectMany("SELECT * FROM supply WHERE TRIM(BarCodeOrID)='"+POut.String(code)+"' "
				+"ORDER BY IsHidden,SupplyNum");
			if(listSupplies.Count==0) {
				listSupplies=Crud.SupplyCrud.SelectMany("SELECT * FROM supply WHERE TRIM(CatalogNumber)='"+POut.String(code)+"' ORDER BY IsHidden,SupplyNum");
			}
			return listSupplies;
		}

		///<summary>Inserts the supply with LevelOnHand 0. A typed LevelOnHand becomes an opening stock ledger row (inventory tracking), so the stock
		///always equals the sum of the ledger.</summary>
		public static long Insert(Supply supply){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				supply.SupplyNum=Meth.GetLong(MethodBase.GetCurrentMethod(),supply);
				return supply.SupplyNum;
			}
			double qtyOpening=Math.Round(supply.LevelOnHand,2);
			supply.LevelOnHand=0;
			Crud.SupplyCrud.Insert(supply);
			if(qtyOpening!=0 && InventoryTracking.IsInstalled()) {
				SupplyLedger supplyLedger=new SupplyLedger();
				supplyLedger.SupplyNum=supply.SupplyNum;
				supplyLedger.LedgerType=SupplyLedgerType.Opening;
				supplyLedger.Quantity=qtyOpening;
				supplyLedger.UnitCost=supply.Price;
				supplyLedger.Reason=InventoryTracking.REASON_OPENING;
				SupplyLedgers.InsertMany(new List<SupplyLedger> { supplyLedger });
				supply.LevelOnHand=(float)qtyOpening;
			}
			return supply.SupplyNum;
		}

		///<summary>Never writes LevelOnHand (ExcludeFromUpdate): stock changes only through the stock ledger.</summary>
		public static void Update(Supply supply) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supply);
				return;
			}
			Crud.SupplyCrud.Update(supply);
		}

		///<summary>Surround with try-catch.  Handles ItemOrders below this supply.</summary>
		public static void DeleteObject(Supply supply){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supply);
				return;
			}
			//validate that not already in use.
			string command="SELECT COUNT(*) FROM supplyorderitem WHERE SupplyNum="+POut.Long(supply.SupplyNum);
			int count=PIn.Int(Db.GetCount(command));
			if(count>0){
				throw new ApplicationException(Lans.g("Supplies","Supply is already in use on an order. Not allowed to delete."));
			}
			if(InventoryTracking.IsInstalled()) {
				//Inventory tracking: a supply with receipts, uses, counts or dispensing keeps its history and is hidden instead (throws).
				SupplyLedgers.DeleteForSupplyWithoutHistory(supply.SupplyNum);
				SupplyLots.DeleteForSupply(supply.SupplyNum);
				SupplySettings.DeleteForSupply(supply.SupplyNum);
				SupplyProcCodes.DeleteForSupply(supply.SupplyNum);
			}
			Crud.SupplyCrud.Delete(supply.SupplyNum);
			command="UPDATE supply SET ItemOrder=(ItemOrder-1) WHERE Category="+POut.Long(supply.Category)+" AND ItemOrder>"+POut.Int(supply.ItemOrder);
			Db.NonQ(command);
		}

		///<summary>Uses single query to subtract a count, typically 1, from each supply in the list.</summary>
		public static void OrderSubtract(List<long> listSupplyNums,int countMove){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listSupplyNums,countMove);
				return;
			}
			if(listSupplyNums.Count==0){
				return;
			}
			string command="UPDATE supply SET ItemOrder=(ItemOrder-"+countMove.ToString()+") WHERE SupplyNum IN("
				+String.Join(",",listSupplyNums.ConvertAll(x=>x.ToString()))+")";
			Db.NonQ(command);

		}

		///<summary>Uses single query to add a count, typically 1, to each supply in the list.</summary>
		public static void OrderAdd(List<long> listSupplyNums,int countMove){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listSupplyNums,countMove);
				return;
			}
			if(listSupplyNums.Count==0){
				return;
			}
			string command="UPDATE supply SET ItemOrder=(ItemOrder+"+countMove.ToString()+") WHERE SupplyNum IN("
				+String.Join(",",listSupplyNums.ConvertAll(x=>x.ToString()))+")";
			Db.NonQ(command);
		}

		///<summary>Uses single query to add one to each supply.ItemOrder that has an itemOrder >= than specified.</summary>
		public static void OrderAddOneGreater(int itemOrder,long category,long supplyNumExclude){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),itemOrder,category,supplyNumExclude);
				return;
			}
			string command="UPDATE supply SET ItemOrder=(ItemOrder+1) "
				+"WHERE ItemOrder >= "+POut.Int(itemOrder)
				+" AND Category="+POut.Long(category)
				+" AND SupplyNum !="+POut.Long(supplyNumExclude);
			Db.NonQ(command);
		}

		///<summary>Gets the last ItemOrder for a category.  -1 if none in that category yet.</summary>
		public static int GetLastItemOrder(long category){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetInt(MethodBase.GetCurrentMethod(),category);
			}
			string command="SELECT MAX(ItemOrder) FROM supply "
				+"WHERE Category="+POut.Long(category);
			string result=Db.GetScalar(command);
			if(result==""){
				return -1;
			}
			return PIn.Int(result);
		}
	}
}