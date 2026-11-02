// Inventory tracking (docs/specs/inventory-tracking.md): receiving an order, scanning the items against the order.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Supply Orders, Receive. One row per order item (more for an item that arrives in several lots): ordered, received so far, and what
	///arrives now, with lot number and expiry date for supplies tracked by lot. Each barcode scan adds one to the matching item. Post raises stock.
	///</summary>
	public partial class FormSupplyReceive:FormODBase {
		///<summary>Set before showing.</summary>
		public long SupplyOrderNum;
		private List<ReceiveRow> _listReceiveRows=new List<ReceiveRow>();

		private class ReceiveRow {
			public SupplyOrderItem SupplyOrderItem;
			public Supply Supply;
			public bool IsLotTracked;
			public double QtyReceivedBefore;
			public double QtyNow;
			public string LotNumber="";
			public string Expiry="";
		}

		public FormSupplyReceive() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSupplyReceive_Load(object sender,EventArgs e) {
			LoadRows();
			FillGrid();
			textScan.Select();
		}

		private void LoadRows() {
			List<SupplyOrderItem> listSupplyOrderItems=SupplyOrderItems.GetSupplyItemsForOrder(SupplyOrderNum);
			List<Supply> listSupplies=Supplies.GetMany(listSupplyOrderItems.Select(x => x.SupplyNum).Distinct().ToList());
			List<SupplySetting> listSupplySettings=SupplySettings.GetForSupplies(listSupplies.Select(x => x.SupplyNum).ToList());
			List<SupplyLedger> listReceipts=SupplyLedgers.GetReceiptsForOrderItems(listSupplyOrderItems.Select(x => x.SupplyOrderItemNum).ToList());
			_listReceiveRows.Clear();
			foreach(SupplyOrderItem supplyOrderItem in listSupplyOrderItems) {
				ReceiveRow receiveRow=new ReceiveRow();
				receiveRow.SupplyOrderItem=supplyOrderItem;
				receiveRow.Supply=listSupplies.Find(x => x.SupplyNum==supplyOrderItem.SupplyNum)??new Supply { Descript="?" };
				receiveRow.IsLotTracked=listSupplySettings.Find(x => x.SupplyNum==supplyOrderItem.SupplyNum)?.IsLotTracked??false;
				receiveRow.QtyReceivedBefore=Math.Round(listReceipts.Where(x => x.SupplyOrderItemNum==supplyOrderItem.SupplyOrderItemNum).Sum(x => x.Quantity),2);
				_listReceiveRows.Add(receiveRow);
			}
		}

		private void FillGrid() {
			int indexSelected=gridMain.GetSelectedIndex();
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Catalog #"),80));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Description"),250));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Ordered"),55,HorizontalAlignment.Center));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Received"),60,HorizontalAlignment.Center));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Receive Now"),78,HorizontalAlignment.Center,isEditable:true));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Lot"),110,isEditable:true));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Expires"),80,isEditable:true));
			gridMain.ListGridRows.Clear();
			foreach(ReceiveRow receiveRow in _listReceiveRows) {
				GridRow row=new GridRow();
				row.Cells.Add(receiveRow.Supply.CatalogNumber);
				row.Cells.Add(receiveRow.Supply.Descript+(receiveRow.IsLotTracked ? " ("+Lan.g(this,"lot")+")" : ""));
				row.Cells.Add(receiveRow.SupplyOrderItem.Qty.ToString());
				row.Cells.Add(InventoryTracking.FormatQty(receiveRow.QtyReceivedBefore));
				row.Cells.Add(receiveRow.QtyNow==0 ? "" : InventoryTracking.FormatQty(receiveRow.QtyNow));
				row.Cells.Add(receiveRow.LotNumber);
				row.Cells.Add(receiveRow.Expiry);
				row.Tag=receiveRow;
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
			if(indexSelected>=0 && indexSelected<gridMain.ListGridRows.Count) {
				gridMain.SetSelected(new System.Drawing.Point(4,indexSelected));
			}
		}

		private void gridMain_CellLeave(object sender,ODGridClickEventArgs e) {
			if(e.Row<0 || e.Row>=_listReceiveRows.Count) {
				return;
			}
			ReceiveRow receiveRow=_listReceiveRows[e.Row];
			string text=gridMain.ListGridRows[e.Row].Cells[e.Col].Text.Trim();
			if(e.Col==4) {
				if(text=="") {
					receiveRow.QtyNow=0;
				}
				else if(double.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out double qty) && qty>=0) {
					receiveRow.QtyNow=Math.Round(qty,2);
				}
				else {
					MsgBox.Show(this,"Please enter a quantity of 0 or more.");
				}
			}
			else if(e.Col==5) {
				receiveRow.LotNumber=text;
			}
			else if(e.Col==6) {
				receiveRow.Expiry=text;
			}
			FillGrid();
		}

		private void textScan_KeyDown(object sender,KeyEventArgs e) {
			if(e.KeyCode!=Keys.Enter) {
				return;
			}
			e.SuppressKeyPress=true;
			string code=textScan.Text.Trim();
			textScan.Text="";
			if(code=="") {
				return;
			}
			List<long> listSupplyNums=Supplies.GetByScan(code).Select(x => x.SupplyNum).ToList();
			//The selected row when it is that supply (a second lot), otherwise the first row of the supply.
			ReceiveRow receiveRow=null;
			int indexSelected=gridMain.GetSelectedIndex();
			if(indexSelected>=0 && indexSelected<_listReceiveRows.Count && listSupplyNums.Contains(_listReceiveRows[indexSelected].Supply.SupplyNum)) {
				receiveRow=_listReceiveRows[indexSelected];
			}
			if(receiveRow==null) {
				receiveRow=_listReceiveRows.Find(x => listSupplyNums.Contains(x.Supply.SupplyNum));
			}
			if(receiveRow==null) {
				MessageBox.Show(this,(listSupplyNums.Count==0 ? Lan.g(this,"No supply has this barcode or catalog number")
					: Lan.g(this,"This supply is not on the order"))+": "+code);
				return;
			}
			receiveRow.QtyNow=Math.Round(receiveRow.QtyNow+1,2);
			FillGrid();
			int index=_listReceiveRows.IndexOf(receiveRow);
			gridMain.SetSelected(new System.Drawing.Point(receiveRow.IsLotTracked && receiveRow.LotNumber=="" ? 5 : 4,index));
			textScan.Focus();
		}

		private void butAddLotRow_Click(object sender,EventArgs e) {
			int index=gridMain.GetSelectedIndex();
			if(index<0 || index>=_listReceiveRows.Count) {
				MsgBox.Show(this,"Please select an item first.");
				return;
			}
			ReceiveRow receiveRowFrom=_listReceiveRows[index];
			ReceiveRow receiveRow=new ReceiveRow();
			receiveRow.SupplyOrderItem=receiveRowFrom.SupplyOrderItem;
			receiveRow.Supply=receiveRowFrom.Supply;
			receiveRow.IsLotTracked=receiveRowFrom.IsLotTracked;
			receiveRow.QtyReceivedBefore=receiveRowFrom.QtyReceivedBefore;
			_listReceiveRows.Insert(index+1,receiveRow);
			FillGrid();
			gridMain.SetSelected(new System.Drawing.Point(4,index+1));
		}

		private void butPost_Click(object sender,EventArgs e) {
			List<ReceiveRow> listRows=_listReceiveRows.FindAll(x => x.QtyNow>0);
			if(listRows.Count==0) {
				MsgBox.Show(this,"Please enter or scan what arrived first.");
				return;
			}
			List<SupplyReceiveLine> listSupplyReceiveLines=new List<SupplyReceiveLine>();
			foreach(ReceiveRow receiveRow in listRows) {
				if(receiveRow.IsLotTracked && receiveRow.LotNumber=="") {
					MessageBox.Show(this,Lan.g(this,"Please enter the lot number of")+" "+receiveRow.Supply.Descript+".");
					return;
				}
				DateTime dateExpiry=DateTime.MinValue;
				if(receiveRow.Expiry!="" && !DateTime.TryParse(receiveRow.Expiry,CultureInfo.CurrentCulture,DateTimeStyles.None,out dateExpiry)) {
					MessageBox.Show(this,Lan.g(this,"Please enter a valid expiry date for")+" "+receiveRow.Supply.Descript+".");
					return;
				}
				listSupplyReceiveLines.Add(new SupplyReceiveLine { SupplyOrderItemNum=receiveRow.SupplyOrderItem.SupplyOrderItemNum,Quantity=receiveRow.QtyNow,
					LotNumber=receiveRow.LotNumber,DateExpiry=dateExpiry.Date });
			}
			//More than ordered: ask first.
			List<string> listOver=listRows.GroupBy(x => x.SupplyOrderItem.SupplyOrderItemNum)
				.Where(x => x.First().QtyReceivedBefore+x.Sum(y => y.QtyNow)>x.First().SupplyOrderItem.Qty)
				.Select(x => x.First().Supply.Descript).ToList();
			if(listOver.Count>0 && MessageBox.Show(this,Lan.g(this,"More than was ordered")+":\r\n"+string.Join("\r\n",listOver)+"\r\n"+Lan.g(this,"Receive anyway?"),
				"",MessageBoxButtons.OKCancel)!=DialogResult.OK)
			{
				return;
			}
			try {
				InventoryTracking.Receive(listSupplyReceiveLines);
			}
			catch(Exception ex) {
				MessageBox.Show(this,ex.Message);
				return;
			}
			LoadRows();
			FillGrid();
			MsgBox.Show(this,"Received. Stock is updated.");
		}

		private void butClose_Click(object sender,EventArgs e) {
			if(_listReceiveRows.Any(x => x.QtyNow>0) && !MsgBox.Show(this,MsgBoxButtons.OKCancel,"Quantities entered here are not posted. Close anyway?")) {
				return;
			}
			DialogResult=DialogResult.OK;
		}
	}
}
