// Inventory tracking (docs/specs/inventory-tracking.md): a periodic stock count, by scanning and counting, posted as count adjustments.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Supply Inventory, Inventory, Stock Count. Items are added by scanning or from the supply list; for supplies tracked by lot there is
	///one row per lot. Each row keeps the quantity the program had when it was added, so procedures completed during the count are not counted
	///twice. Post (Inventory Adjust) writes the differences with the reason. The count is kept only while the window is open.</summary>
	public partial class FormSupplyCount:FormODBase {
		private List<CountRow> _listCountRows=new List<CountRow>();

		private class CountRow {
			public Supply Supply;
			public long SupplyLotNum;
			public string LotNumber="";
			public DateTime DateExpiry;
			public double QtyProgram;
			public double QtyCounted;
			public bool IsCounted;
		}

		public FormSupplyCount() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSupplyCount_Load(object sender,EventArgs e) {
			FillGrid();
			textScan.Select();
		}

		private void FillGrid() {
			int indexSelected=gridMain.GetSelectedIndex();
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Supply"),260));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Lot"),110));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Expires"),75));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Program"),65,HorizontalAlignment.Center));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Counted"),65,HorizontalAlignment.Center,isEditable:true));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Difference"),70,HorizontalAlignment.Center));
			gridMain.ListGridRows.Clear();
			foreach(CountRow countRow in _listCountRows) {
				GridRow row=new GridRow();
				row.Cells.Add(countRow.Supply.Descript);
				row.Cells.Add(countRow.LotNumber=="" ? Lan.g(this,"(no lot)") : countRow.LotNumber);
				row.Cells.Add(countRow.DateExpiry.Year>1880 ? countRow.DateExpiry.ToShortDateString() : "");
				row.Cells.Add(InventoryTracking.FormatQty(countRow.QtyProgram));
				row.Cells.Add(countRow.IsCounted ? InventoryTracking.FormatQty(countRow.QtyCounted) : "");
				double qtyDiff=Math.Round(countRow.QtyCounted-countRow.QtyProgram,2);
				row.Cells.Add(!countRow.IsCounted || qtyDiff==0 ? "" : (qtyDiff>0 ? "+" : "")+InventoryTracking.FormatQty(qtyDiff));
				row.Tag=countRow;
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
			if(indexSelected>=0 && indexSelected<gridMain.ListGridRows.Count) {
				gridMain.SetSelected(new System.Drawing.Point(COL_SUPPLY,indexSelected));
			}
			labelCount.Text=_listCountRows.Count(x => x.IsCounted)+" "+Lan.g(this,"of")+" "+_listCountRows.Count+" "+Lan.g(this,"rows counted");
		}

		///<summary>Rows are selected in the Supply column, never in the Counted one: an edit box there would take the scanner's digits.</summary>
		private const int COL_SUPPLY=0;

		///<summary>Selects the row in the Supply column (no edit box) and puts the keyboard back in the Scan box once the grid is drawn.</summary>
		private void SelectRowAndScanBox(int index) {
			if(index>=0 && index<gridMain.ListGridRows.Count) {
				gridMain.SetSelected(new System.Drawing.Point(COL_SUPPLY,index));
			}
			if(IsHandleCreated) {
				BeginInvoke((Action)(() => textScan.Focus()));
			}
		}

		///<summary>Adds the rows of the supply that are not in the grid yet, with the quantities the program has now, from the ledger
		///(InventoryTracking.GetCountLines): one row per lot whose stock is not 0, negative lots too, so the count can correct them, and one for stock
		///without a lot when there is some, or when the supply is not tracked by lot, or has no such lot.</summary>
		private void AddRowsForSupply(Supply supply) {
			if(_listCountRows.Any(x => x.Supply.SupplyNum==supply.SupplyNum)) {
				return;
			}
			foreach(SupplyCountLine supplyCountLine in InventoryTracking.GetCountLines(supply.SupplyNum)) {
				_listCountRows.Add(new CountRow { Supply=supply,SupplyLotNum=supplyCountLine.SupplyLotNum,LotNumber=supplyCountLine.LotNumber??"",
					DateExpiry=supplyCountLine.DateExpiry,QtyProgram=supplyCountLine.QtyProgram });
			}
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
			List<Supply> listSupplies=Supplies.GetByScan(code);
			if(listSupplies.Count==0) {
				MessageBox.Show(this,Lan.g(this,"No supply has this barcode or catalog number")+": "+code);
				return;
			}
			Supply supply=listSupplies[0];
			AddRowsForSupply(supply);
			//The selected row when it is that supply (the lot being counted), otherwise its first row that the program has stock in (a lot below zero
			//is counted by selecting its row), otherwise its first row.
			CountRow countRow=null;
			int indexSelected=gridMain.GetSelectedIndex();
			if(indexSelected>=0 && indexSelected<_listCountRows.Count && _listCountRows[indexSelected].Supply.SupplyNum==supply.SupplyNum) {
				countRow=_listCountRows[indexSelected];
			}
			countRow=countRow??_listCountRows.FirstOrDefault(x => x.Supply.SupplyNum==supply.SupplyNum && x.QtyProgram>0)
				??_listCountRows.First(x => x.Supply.SupplyNum==supply.SupplyNum);
			countRow.QtyCounted=Math.Round((countRow.IsCounted ? countRow.QtyCounted : 0)+1,2);
			countRow.IsCounted=true;
			FillGrid();
			SelectRowAndScanBox(_listCountRows.IndexOf(countRow));
		}

		private void gridMain_CellLeave(object sender,ODGridClickEventArgs e) {
			if(e.Col!=4 || e.Row<0 || e.Row>=_listCountRows.Count) {
				return;
			}
			CountRow countRow=_listCountRows[e.Row];
			string text=gridMain.ListGridRows[e.Row].Cells[4].Text.Trim();
			if(text=="") {
				countRow.IsCounted=false;
				countRow.QtyCounted=0;
			}
			else if(!double.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out double qty) || qty<0) {
				MsgBox.Show(this,"Please enter a quantity of 0 or more.");
			}
			else if(MaterialsUsedL.IsQtyAccepted(this,qty,Math.Max(MaterialsUsedL.QTY_ASK_STOCK,Math.Round(countRow.QtyProgram*10,2)))) {
				countRow.QtyCounted=Math.Round(qty,2);
				countRow.IsCounted=true;
			}
			FillGrid();
		}

		private void butAdd_Click(object sender,EventArgs e) {
			using FormSupplies formSupplies=new FormSupplies();
			formSupplies.IsSelectMode=true;
			formSupplies.SupplierNumSelected=0;//Every supplier.
			if(formSupplies.ShowDialog()!=DialogResult.OK) {
				return;
			}
			foreach(Supply supply in formSupplies.ListSuppliesSelected) {
				AddRowsForSupply(supply);
			}
			FillGrid();
		}

		private void butNewLot_Click(object sender,EventArgs e) {
			int index=gridMain.GetSelectedIndex();
			if(index<0 || index>=_listCountRows.Count) {
				MsgBox.Show(this,"Please select a row of the supply first.");
				return;
			}
			Supply supply=_listCountRows[index].Supply;
			InputBox inputBoxNumber=new InputBox(Lan.g(this,"Lot number found in the count"));
			inputBoxNumber.ShowDialog();
			if(inputBoxNumber.IsDialogCancel || inputBoxNumber.StringResult.Trim()=="") {
				return;
			}
			string lotNumber=inputBoxNumber.StringResult.Trim();
			CountRow countRow=_listCountRows.Find(x => x.Supply.SupplyNum==supply.SupplyNum && string.Equals(x.LotNumber,lotNumber,StringComparison.OrdinalIgnoreCase));
			if(countRow==null) {
				SupplyLot supplyLot=SupplyLots.GetForSupply(supply.SupplyNum,isEmptyIncluded:true)
					.Find(x => string.Equals(x.LotNumber.Trim(),lotNumber,StringComparison.OrdinalIgnoreCase));
				countRow=new CountRow { Supply=supply,LotNumber=lotNumber };
				if(supplyLot!=null) {
					countRow.SupplyLotNum=supplyLot.SupplyLotNum;
					countRow.DateExpiry=supplyLot.DateExpiry;
					countRow.QtyProgram=InventoryTracking.GetQtyProgram(supply.SupplyNum,supplyLot.SupplyLotNum);//From the ledger, not the counter.
				}
				else {
					InputBox inputBoxExpiry=new InputBox(Lan.g(this,"Expiry date (blank for none)"));
					inputBoxExpiry.ShowDialog();
					if(inputBoxExpiry.IsDialogCancel) {
						return;
					}
					DateTime dateExpiry=DateTime.MinValue;
					if(inputBoxExpiry.StringResult.Trim()!="" && !DateTime.TryParse(inputBoxExpiry.StringResult,CultureInfo.CurrentCulture,DateTimeStyles.None,out dateExpiry)) {
						MsgBox.Show(this,"Please enter a valid date.");
						return;
					}
					countRow.DateExpiry=dateExpiry.Date;
				}
				_listCountRows.Insert(index+1,countRow);
			}
			FillGrid();
			SelectRowAndScanBox(_listCountRows.IndexOf(countRow));
		}

		private void butRemove_Click(object sender,EventArgs e) {
			int index=gridMain.GetSelectedIndex();
			if(index<0 || index>=_listCountRows.Count) {
				MsgBox.Show(this,"Please select a row first.");
				return;
			}
			_listCountRows.RemoveAt(index);
			FillGrid();
		}

		private void butPost_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.InventoryAdjust)) {
				return;
			}
			List<CountRow> listCounted=_listCountRows.FindAll(x => x.IsCounted);
			if(listCounted.Count==0) {
				MsgBox.Show(this,"Nothing is counted yet.");
				return;
			}
			if(textReason.Text.Trim()=="") {
				MsgBox.Show(this,"Please enter a reason, for example \"Quarterly count\".");
				return;
			}
			List<SupplyCountLine> listSupplyCountLines=listCounted.Select(x => new SupplyCountLine { SupplyNum=x.Supply.SupplyNum,SupplyLotNum=x.SupplyLotNum,
				LotNumber=x.LotNumber,DateExpiry=x.DateExpiry,QtyProgram=x.QtyProgram,QtyCounted=x.QtyCounted }).ToList();
			int countAdjusted;
			try {
				countAdjusted=InventoryTracking.PostCount(listSupplyCountLines,textReason.Text);
			}
			catch(Exception ex) {
				//Some or all of it may be posted (a failure part-way, or the answer was lost). Read the program's quantity of each counted row again, so
				//posting again writes only the difference that is still left.
				ReloadQtyProgramAfterFailure(listCounted);
				FillGrid();
				MessageBox.Show(this,ex.Message+"\r\n\r\n"+Lan.g(this,"Part of the count may have been posted. The program's quantities were read again: posting again writes only what still differs."));
				return;
			}
			_listCountRows.RemoveAll(x => x.IsCounted);
			FillGrid();
			MessageBox.Show(this,Lan.g(this,"Count posted. Rows adjusted")+": "+countAdjusted);
		}

		///<summary>After a failed post: each counted row's program quantity read again from the ledger (InventoryTracking.GetQtyProgram), never from
		///the lot counter, which the failed post may have left at its old value; a lot found in the count that the post created is found by its number.
		///When even that fails, Post is turned off until the window is opened again.</summary>
		private void ReloadQtyProgramAfterFailure(List<CountRow> listCountRows) {
			try {
				foreach(CountRow countRow in listCountRows) {
					if(countRow.SupplyLotNum==0 && countRow.LotNumber!="") {
						SupplyLot supplyLotFound=SupplyLots.GetForSupply(countRow.Supply.SupplyNum,isEmptyIncluded:true)
							.Find(x => string.Equals(x.LotNumber.Trim(),countRow.LotNumber.Trim(),StringComparison.OrdinalIgnoreCase));
						if(supplyLotFound!=null) {
							countRow.SupplyLotNum=supplyLotFound.SupplyLotNum;
						}
					}
					if(countRow.SupplyLotNum!=0 || countRow.LotNumber=="") {
						countRow.QtyProgram=InventoryTracking.GetQtyProgram(countRow.Supply.SupplyNum,countRow.SupplyLotNum);
					}
				}
			}
			catch(Exception) {
				_listCountRows.Clear();
				butPost.Enabled=false;
				butAdd.Enabled=false;
				butNewLot.Enabled=false;
				textScan.Enabled=false;
				MsgBox.Show(this,"The stock could not be read again. Close this window and check the supplies in Stock before counting again.");
			}
		}

		private void butClose_Click(object sender,EventArgs e) {
			if(_listCountRows.Any(x => x.IsCounted) && !MsgBox.Show(this,MsgBoxButtons.OKCancel,"The count is not posted and will be lost. Close anyway?")) {
				return;
			}
			DialogResult=DialogResult.OK;
		}
	}
}
