// Synthetic rows for the inventory tracking tests: supplies, lots, codes with default materials, patients, procedures and orders. Each test makes
// its own, so the tests do not depend on each other's rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenPmsDentalBusiness;

namespace InventoryTests {
	public static class InvData {
		private static int _counter;

		///<summary>A unique number for names and codes in this run.</summary>
		public static int Next() {
			return Interlocked.Increment(ref _counter);
		}

		///<summary>A supply through Supplies.Insert (a typed on hand becomes an opening ledger row) with its tracking settings.</summary>
		public static Supply Supply(double onHand=0,double price=2.5,bool isLotTracked=false,bool isImplant=false,double reorderLevel=0,string barcode="") {
			int n=Next();
			Supply supply=new Supply();
			supply.SupplierNum=1;
			supply.Category=0;
			supply.CatalogNumber="CAT-"+n;
			supply.Descript="Synthetic supply "+n;
			supply.Price=price;
			supply.BarCodeOrID=barcode;
			supply.DispUnitDesc="";
			supply.LevelOnHand=(float)onHand;
			Supplies.Insert(supply);
			if(isLotTracked || isImplant || reorderLevel!=0) {
				SupplySettings.Upsert(new SupplySetting { SupplyNum=supply.SupplyNum,IsLotTracked=isLotTracked,IsImplant=isImplant,ReorderLevel=reorderLevel });
			}
			return supply;
		}

		///<summary>LevelOnHand as stored in the database now.</summary>
		public static double OnHand(long supplyNum) {
			return Math.Round(Supplies.GetSupply(supplyNum).LevelOnHand,2);
		}

		///<summary>QtyOnHand of the lot as stored in the database now.</summary>
		public static double LotQty(long supplyLotNum) {
			return Math.Round(SupplyLots.GetMany(new List<long> { supplyLotNum }).Single().QtyOnHand,2);
		}

		///<summary>A lot with stock received through a manual adjustment.</summary>
		public static SupplyLot Lot(Supply supply,string lotNumber,DateTime dateExpiry,double qty) {
			SupplyLot supplyLot=SupplyLots.GetOrInsert(supply.SupplyNum,lotNumber,dateExpiry,0);
			if(qty!=0) {
				InventoryTracking.Adjust(supply.SupplyNum,supplyLot.SupplyLotNum,qty,"Synthetic stock");
			}
			return supplyLot;
		}

		///<summary>A synthetic procedure code (Z codes), in the cache.</summary>
		public static long Code(params (Supply Supply,double Qty)[] arrayMaterials) {
			int n=Next();
			long codeNum=OpenPmsDentalBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode="ZI"+n.ToString("000"),Descript="Inventory test "+n,
				AbbrDesc="Inv"+n,ProcTime="/X/",TreatArea=TreatmentArea.Mouth });
			Cache.Refresh(InvalidType.ProcCodes);
			if(arrayMaterials.Length>0) {
				SupplyProcCodes.SyncForCode(codeNum,arrayMaterials.Select(x => new SupplyProcCode { SupplyNum=x.Supply.SupplyNum,Quantity=x.Qty }).ToList());
			}
			return codeNum;
		}

		///<summary>A synthetic patient who is the guarantor of the family.</summary>
		public static long Patient() {
			int n=Next();
			long patNum=DataCore.NonQ("INSERT INTO patient (LName,FName,PriProv,ClinicNum) VALUES ('Inventory','Synthetic"+n+"',1,0)",true,"PatNum","patient");
			DataCore.NonQ("UPDATE patient SET Guarantor=PatNum WHERE PatNum="+POut.Long(patNum));
			return patNum;
		}

		///<summary>A procedure inserted with the status (Procedures.Insert, as the Chart module does).</summary>
		public static Procedure Proc(long patNum,long codeNum,ProcStat procStat=ProcStat.TP) {
			Procedure procedure=new Procedure();
			procedure.PatNum=patNum;
			procedure.CodeNum=codeNum;
			procedure.ProcDate=DateTime.Today;
			procedure.DateTP=DateTime.Today;
			procedure.ProvNum=1;
			procedure.ProcFee=100;
			procedure.ProcStatus=procStat;
			Procedures.Insert(procedure);
			return Procedures.GetOneProc(procedure.ProcNum,false);
		}

		///<summary>Changes the status through Procedures.Update, as the program's windows do. Returns the procedure as saved.</summary>
		public static Procedure SetStatus(Procedure procedure,ProcStat procStat) {
			Procedure procedureOld=Procedures.GetOneProc(procedure.ProcNum,false);
			Procedure procedureNew=procedureOld.Copy();
			procedureNew.ProcStatus=procStat;
			Procedures.Update(procedureNew,procedureOld);
			return Procedures.GetOneProc(procedure.ProcNum,false);
		}

		///<summary>An order of the supplies with the quantities, at the prices.</summary>
		public static List<SupplyOrderItem> Order(params (Supply Supply,int Qty,double Price)[] arrayItems) {
			SupplyOrder supplyOrder=new SupplyOrder();
			supplyOrder.SupplierNum=1;
			supplyOrder.DatePlaced=DateTime.Today;
			supplyOrder.Note="";
			supplyOrder.UserNum=1;
			SupplyOrders.Insert(supplyOrder);
			List<SupplyOrderItem> listSupplyOrderItems=new List<SupplyOrderItem>();
			foreach((Supply supply,int qty,double price) in arrayItems) {
				SupplyOrderItem supplyOrderItem=new SupplyOrderItem();
				supplyOrderItem.SupplyOrderNum=supplyOrder.SupplyOrderNum;
				supplyOrderItem.SupplyNum=supply.SupplyNum;
				supplyOrderItem.Qty=qty;
				supplyOrderItem.Price=price;
				SupplyOrderItems.Insert(supplyOrderItem);
				listSupplyOrderItems.Add(supplyOrderItem);
			}
			return listSupplyOrderItems;
		}

		///<summary>Runs the action and returns the exception of type T it threw; fails when it threw none (MSTest v1 has no Assert.ThrowsException).
		///</summary>
		public static T Throws<T>(Action action) where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				return ex;
			}
			Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail("Expected "+typeof(T).Name+".");
			return null;
		}

		///<summary>The number of security log entries of the permission whose text contains the fragment.</summary>
		public static int CountLogs(EnumPermType permType,string textFragment) {
			return (int)PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+(int)permType+" AND LogText LIKE '%"
				+POut.String(textFragment)+"%'"));
		}
	}
}
