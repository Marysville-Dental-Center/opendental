// Inventory tracking (docs/specs/inventory-tracking.md): the stock of one supply, its lots and its ledger, and manual adjustments.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Opened from the supply edit window. Lots with their stock and expiry, every ledger row of the supply, and Adjust (Inventory Adjust).
	///</summary>
	public partial class FormSupplyStock:FormODBase {
		///<summary>Set before showing.</summary>
		public long SupplyNum;
		private Supply _supply;
		private List<SupplyLot> _listSupplyLots;

		public FormSupplyStock() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSupplyStock_Load(object sender,EventArgs e) {
			FillAll();
		}

		private void FillAll() {
			_supply=Supplies.GetSupply(SupplyNum);
			if(_supply==null) {
				MsgBox.Show(this,"The supply was deleted by another user.");
				DialogResult=DialogResult.Cancel;
				return;
			}
			labelSupply.Text=_supply.Descript+"   "+Lan.g(this,"On hand")+": "+InventoryTracking.FormatQty(_supply.LevelOnHand)
				+"   ("+Lan.g(this,"without a lot")+": "+InventoryTracking.FormatQty(SupplyLedgers.GetSumWithoutLot(SupplyNum))+")";
			_listSupplyLots=SupplyLots.GetForSupply(SupplyNum,isEmptyIncluded:checkShowEmptyLots.Checked);
			gridLots.BeginUpdate();
			gridLots.Columns.Clear();
			gridLots.Columns.Add(new GridColumn(Lan.g(this,"Lot"),140));
			gridLots.Columns.Add(new GridColumn(Lan.g(this,"Expires"),80));
			gridLots.Columns.Add(new GridColumn(Lan.g(this,"On Hand"),60,HorizontalAlignment.Center));
			gridLots.ListGridRows.Clear();
			foreach(SupplyLot supplyLot in _listSupplyLots) {
				GridRow row=new GridRow();
				row.Cells.Add(supplyLot.LotNumber);
				row.Cells.Add(supplyLot.HasExpiry() ? supplyLot.DateExpiry.ToShortDateString() : "");
				row.Cells.Add(InventoryTracking.FormatQty(supplyLot.QtyOnHand));
				row.Tag=supplyLot;
				gridLots.ListGridRows.Add(row);
			}
			gridLots.EndUpdate();
			List<SupplyLedger> listSupplyLedgers=SupplyLedgers.GetForSupply(SupplyNum);
			List<SupplyLot> listSupplyLotsAll=SupplyLots.GetForSupply(SupplyNum,isEmptyIncluded:true);
			gridLedger.BeginUpdate();
			gridLedger.Columns.Clear();
			gridLedger.Columns.Add(new GridColumn(Lan.g(this,"Date"),120));
			gridLedger.Columns.Add(new GridColumn(Lan.g(this,"Type"),70));
			gridLedger.Columns.Add(new GridColumn(Lan.g(this,"Qty"),55,HorizontalAlignment.Right));
			gridLedger.Columns.Add(new GridColumn(Lan.g(this,"Lot"),100));
			gridLedger.Columns.Add(new GridColumn(Lan.g(this,"Patient / Order"),130));
			gridLedger.Columns.Add(new GridColumn(Lan.g(this,"User"),80));
			gridLedger.Columns.Add(new GridColumn(Lan.g(this,"Reason"),170));
			gridLedger.ListGridRows.Clear();
			Dictionary<long,string> dictPatNames=Patients.GetPatientNames(listSupplyLedgers.Select(x => x.PatNum).Where(x => x!=0).Distinct().ToList());
			foreach(SupplyLedger supplyLedger in listSupplyLedgers) {
				GridRow row=new GridRow();
				row.Cells.Add(supplyLedger.DateTimeEntry.ToString("g"));
				row.Cells.Add(Lan.g("enumSupplyLedgerType",supplyLedger.LedgerType.GetDescription()));
				row.Cells.Add((supplyLedger.Quantity>0 ? "+" : "")+InventoryTracking.FormatQty(supplyLedger.Quantity));
				row.Cells.Add(listSupplyLotsAll.Find(x => x.SupplyLotNum==supplyLedger.SupplyLotNum)?.LotNumber??"");
				if(supplyLedger.PatNum!=0) {
					row.Cells.Add(dictPatNames.TryGetValue(supplyLedger.PatNum,out string name) ? name : supplyLedger.PatNum.ToString());
				}
				else if(supplyLedger.SupplyOrderItemNum!=0) {
					row.Cells.Add(Lan.g(this,"Order item")+" "+supplyLedger.SupplyOrderItemNum);
				}
				else {
					row.Cells.Add("");
				}
				row.Cells.Add(supplyLedger.UserNum==0 ? "" : Userods.GetName(supplyLedger.UserNum));
				row.Cells.Add(supplyLedger.Reason);
				gridLedger.ListGridRows.Add(row);
			}
			gridLedger.EndUpdate();
			gridLedger.ScrollToEnd();
		}

		private void checkShowEmptyLots_Click(object sender,EventArgs e) {
			FillAll();
		}

		private void gridLots_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			//Correct a lot number or expiry date typed wrong.
			SupplyLot supplyLot=(SupplyLot)gridLots.ListGridRows[e.Row].Tag;
			InputBox inputBoxNumber=new InputBox(Lan.g(this,"Lot number"),supplyLot.LotNumber);
			inputBoxNumber.ShowDialog();
			if(inputBoxNumber.IsDialogCancel || inputBoxNumber.StringResult.Trim()=="") {
				return;
			}
			InputBox inputBoxExpiry=new InputBox(Lan.g(this,"Expiry date (blank for none)"),supplyLot.HasExpiry() ? supplyLot.DateExpiry.ToShortDateString() : "");
			inputBoxExpiry.ShowDialog();
			if(inputBoxExpiry.IsDialogCancel) {
				return;
			}
			DateTime dateExpiry=DateTime.MinValue;
			if(inputBoxExpiry.StringResult.Trim()!="" && !DateTime.TryParse(inputBoxExpiry.StringResult,CultureInfo.CurrentCulture,DateTimeStyles.None,out dateExpiry)) {
				MsgBox.Show(this,"Please enter a valid date.");
				return;
			}
			supplyLot.LotNumber=inputBoxNumber.StringResult.Trim();
			supplyLot.DateExpiry=dateExpiry.Date;
			SupplyLots.UpdateNumberAndExpiry(supplyLot);
			SecurityLogs.MakeLogEntry(EnumPermType.InventoryAdjust,0,"Lot edited: "+_supply.Descript+", lot "+supplyLot.LotNumber+", expires "
				+(supplyLot.HasExpiry() ? supplyLot.DateExpiry.ToShortDateString() : "(none)"));
			FillAll();
		}

		private void butAdjust_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.InventoryAdjust)) {
				return;
			}
			using FormSupplyAdjust formSupplyAdjust=new FormSupplyAdjust();
			formSupplyAdjust.SupplyNum=SupplyNum;
			formSupplyAdjust.SupplyLotNumSelected=gridLots.SelectedTag<SupplyLot>()?.SupplyLotNum??0;
			if(formSupplyAdjust.ShowDialog()==DialogResult.OK) {
				FillAll();
			}
		}

		private void butClose_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.OK;
		}
	}
}
