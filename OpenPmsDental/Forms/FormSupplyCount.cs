// Inventory tracking (docs/specs/inventory-tracking.md, 2.7 and 3.5): a periodic stock count, by scanning and counting, saved line by line and
// posted as count adjustments.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Supply Inventory, Inventory, Stock Count. Shows the office's open count, if there is one, for any user, and saves every counted row
	///at once (InventoryTracking.SaveCountLine), so a closed window, a crash or a power cut loses nothing and the count is resumed where it was.
	///Items are added by scanning or from the supply list; for supplies tracked by lot there is one row per lot. A saved line keeps the program's
	///quantity of the moment it was counted, so procedures completed during the count are not counted twice. Post (Inventory Adjust) writes the
	///differences with the reason and closes the count; Discard (Inventory Adjust) closes it without changing stock.</summary>
	public partial class FormSupplyCount:FormODBase {
		///<summary>The office's open count, or null while none is open: the first counted row starts one.</summary>
		private SupplyCount _supplyCount;
		private List<CountRow> _listCountRows=new List<CountRow>();
		///<summary>True when nothing can be counted here: the count's post runs elsewhere or did not finish, or the count could not be read.</summary>
		private bool _isReadOnly;

		private class CountRow {
			public Supply Supply;
			public long SupplyLotNum;
			public string LotNumber="";
			public DateTime DateExpiry;
			public double QtyProgram;
			public double QtyCounted;
			///<summary>True for a saved line of the open count (SupplyCountLineNum). A row not counted yet is only in this window.</summary>
			public bool IsCounted;
			public long SupplyCountLineNum;
		}

		public FormSupplyCount() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSupplyCount_Load(object sender,EventArgs e) {
			if(!LoadCount()) {
				SetReadOnly();
			}
			else if(_supplyCount!=null && _supplyCount.CountStatus==SupplyCountStatus.Posting) {
				ResumePosting();
			}
			FillGrid();
			textScan.Select();
		}

		///<summary>Reads the open count and its saved lines again (other workstations' too) and merges them into the rows: a saved line updates its
		///row, a row whose line is gone (removed elsewhere, or the count was closed) is no longer counted, and a line this window has no row for
		///gets one after the other rows of its supply. Rows not counted yet stay. Returns false, after a message, when the count could not be read.
		///</summary>
		private bool LoadCount() {
			List<SupplyCountLine> listSupplyCountLines;
			List<Supply> listSupplies;
			try {
				_supplyCount=InventoryTracking.GetOpenCount();
				listSupplyCountLines=_supplyCount==null ? new List<SupplyCountLine>() : InventoryTracking.GetSavedCountLines(_supplyCount.SupplyCountNum);
				listSupplies=Supplies.GetMany(listSupplyCountLines.Select(x => x.SupplyNum).Distinct()
					.Where(x => !_listCountRows.Any(y => y.Supply.SupplyNum==x)).ToList());
			}
			catch(Exception ex) {
				MessageBox.Show(this,Lan.g(this,"The stock count could not be read")+": "+ex.Message);
				return false;
			}
			List<SupplyCountLine> listLinesLeft=new List<SupplyCountLine>(listSupplyCountLines);
			foreach(CountRow countRow in _listCountRows) {
				SupplyCountLine supplyCountLine=listLinesLeft.Find(x => IsRowOfLine(countRow,x));
				if(supplyCountLine==null) {
					countRow.IsCounted=false;
					countRow.QtyCounted=0;
					countRow.SupplyCountLineNum=0;
					continue;
				}
				listLinesLeft.Remove(supplyCountLine);
				ApplyLine(countRow,supplyCountLine);
			}
			foreach(SupplyCountLine supplyCountLine in listLinesLeft) {
				Supply supply=_listCountRows.Select(x => x.Supply).FirstOrDefault(x => x.SupplyNum==supplyCountLine.SupplyNum)
					??listSupplies.Find(x => x.SupplyNum==supplyCountLine.SupplyNum)
					??new Supply { SupplyNum=supplyCountLine.SupplyNum,Descript=Lan.g(this,"(deleted supply)") };
				CountRow countRow=new CountRow { Supply=supply };
				ApplyLine(countRow,supplyCountLine);
				int indexLast=_listCountRows.FindLastIndex(x => x.Supply.SupplyNum==supply.SupplyNum);
				_listCountRows.Insert(indexLast<0 ? _listCountRows.Count : indexLast+1,countRow);
			}
			return true;
		}

		///<summary>True when the row is the saved line's supply and lot: the same lot, or the same lot number for a lot found in the count (also once
		///the line became the lot it names, when the lot was received meanwhile).</summary>
		private static bool IsRowOfLine(CountRow countRow,SupplyCountLine supplyCountLine) {
			if(countRow.Supply.SupplyNum!=supplyCountLine.SupplyNum) {
				return false;
			}
			if(countRow.SupplyLotNum==supplyCountLine.SupplyLotNum) {
				return supplyCountLine.SupplyLotNum!=0 || string.Equals(countRow.LotNumber.Trim(),(supplyCountLine.LotNumber??"").Trim(),StringComparison.OrdinalIgnoreCase);
			}
			return countRow.SupplyLotNum==0 && countRow.LotNumber.Trim()!=""
				&& string.Equals(countRow.LotNumber.Trim(),(supplyCountLine.LotNumber??"").Trim(),StringComparison.OrdinalIgnoreCase);
		}

		private static void ApplyLine(CountRow countRow,SupplyCountLine supplyCountLine) {
			countRow.SupplyLotNum=supplyCountLine.SupplyLotNum;
			countRow.LotNumber=supplyCountLine.LotNumber??"";
			countRow.DateExpiry=supplyCountLine.DateExpiry;
			countRow.QtyProgram=supplyCountLine.QtyProgram;
			countRow.QtyCounted=supplyCountLine.QtyCounted;
			countRow.IsCounted=true;
			countRow.SupplyCountLineNum=supplyCountLine.SupplyCountLineNum;
		}

		///<summary>The open count is Posting: its post runs on another workstation, or did not finish (a crash, a lost connection). Offers to resume
		///it (InventoryTracking.ResumeCount), which reads every line's program quantity again from the ledger; otherwise nothing can be counted here.
		///</summary>
		private void ResumePosting() {
			string description=InventoryTracking.GetOpenCountDescription()??"";
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,description+"\r\n\r\n"+Lan.g(this,"Resume the count? Each line's program quantity is read again "
				+"from the ledger, so posting again writes only what still differs.")))
			{
				SetReadOnly();
				return;
			}
			try {
				InventoryTracking.ResumeCount(_supplyCount.SupplyCountNum);
			}
			catch(Exception ex) {
				MessageBox.Show(this,ex.Message);
				SetReadOnly();
				return;
			}
			if(!LoadCount()) {
				SetReadOnly();
			}
		}

		private void SetReadOnly() {
			_isReadOnly=true;
			textScan.Enabled=false;
			butAdd.Enabled=false;
			butNewLot.Enabled=false;
			butRemove.Enabled=false;
			butPost.Enabled=false;
			butDiscard.Enabled=false;
		}

		private void FillGrid() {
			int indexSelected=gridMain.GetSelectedIndex();
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Supply"),300));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Lot"),120));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Expires"),75));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Program"),65,HorizontalAlignment.Center));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Counted"),65,HorizontalAlignment.Center,isEditable:!_isReadOnly));
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
			string text=_listCountRows.Count(x => x.IsCounted)+" "+Lan.g(this,"of")+" "+_listCountRows.Count+" "+Lan.g(this,"rows counted.")+" ";
			if(_supplyCount==null) {
				text+=Lan.g(this,"Each counted row is saved as soon as it is entered.");
			}
			else {
				text+=Lan.g(this,"Count started by")+" "+Userods.GetName(_supplyCount.UserNum)+" "+Lan.g(this,"on")+" "+_supplyCount.DateTimeEntry.ToShortDateString()+" "
					+_supplyCount.DateTimeEntry.ToShortTimeString()+"; "+Lan.g(this,"every counted row is saved, so it can be closed and resumed.");
			}
			labelCount.Text=text;
		}

		///<summary>Rows are selected in the Supply column, never in the Counted one: an edit box there would take the scanner's digits.</summary>
		private const int COL_SUPPLY=0;
		///<summary>The editable column.</summary>
		private const int COL_COUNTED=4;

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
		///without a lot when there is some, or when the supply is not tracked by lot, or has no such lot. They are saved once they are counted.
		///</summary>
		private void AddRowsForSupply(Supply supply) {
			if(_listCountRows.Any(x => x.Supply.SupplyNum==supply.SupplyNum)) {
				return;
			}
			foreach(SupplyCountLine supplyCountLine in InventoryTracking.GetCountLines(supply.SupplyNum)) {
				_listCountRows.Add(new CountRow { Supply=supply,SupplyLotNum=supplyCountLine.SupplyLotNum,LotNumber=supplyCountLine.LotNumber??"",
					DateExpiry=supplyCountLine.DateExpiry,QtyProgram=supplyCountLine.QtyProgram });
			}
		}

		///<summary>Saves the row at once as a counted line of the count this window shows, or of the office's open count (or a new one) while it
		///shows none (InventoryTracking.SaveCountLine): qty replaces its counted quantity, or with isAdded (a scan) is added to it. The line gets the
		///program's quantity of this moment. Then reads the count again. Returns false when it was not saved (a message says why: another
		///workstation posted or discarded the count; the window then shows the office's count as it is now, and counting the row again saves it).
		///</summary>
		private bool SaveRow(CountRow countRow,double qty,bool isAdded) {
			SupplyCountLine supplyCountLine=new SupplyCountLine();
			//The count this window shows: a line is never moved into another count without a word. None (0): the office's open count, or a new one.
			supplyCountLine.SupplyCountNum=_supplyCount?.SupplyCountNum??0;
			supplyCountLine.SupplyNum=countRow.Supply.SupplyNum;
			supplyCountLine.SupplyLotNum=countRow.SupplyLotNum;
			supplyCountLine.LotNumber=countRow.LotNumber;
			supplyCountLine.DateExpiry=countRow.DateExpiry;
			supplyCountLine.QtyCounted=qty;
			try {
				ApplyLine(countRow,InventoryTracking.SaveCountLine(supplyCountLine,isAdded));
			}
			catch(Exception ex) {
				MessageBox.Show(this,ex.Message);
				LoadCount();
				return false;
			}
			LoadCount();
			return true;
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
			SaveRow(countRow,1,isAdded:true);//One more of it, added to the saved line in the same statement.
			FillGrid();
			SelectRowAndScanBox(_listCountRows.IndexOf(countRow));
		}

		private void gridMain_CellLeave(object sender,ODGridClickEventArgs e) {
			if(e.Col!=COL_COUNTED || e.Row<0 || e.Row>=_listCountRows.Count || _isReadOnly) {
				return;
			}
			CountRow countRow=_listCountRows[e.Row];
			string text=gridMain.ListGridRows[e.Row].Cells[COL_COUNTED].Text.Trim();
			if(text=="") {
				if(countRow.IsCounted) {
					DeleteLine(countRow);
				}
			}
			else if(!double.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out double qty) || qty<0) {
				MsgBox.Show(this,"Please enter a quantity of 0 or more.");
			}
			else if(countRow.IsCounted && Math.Round(qty,2)==Math.Round(countRow.QtyCounted,2)) {
				//Unchanged: the line keeps its snapshot.
			}
			else if(MaterialsUsedL.IsQtyAccepted(this,qty,Math.Max(MaterialsUsedL.QTY_ASK_STOCK,Math.Round(countRow.QtyProgram*10,2)))) {
				SaveRow(countRow,Math.Round(qty,2),isAdded:false);
			}
			FillGrid();
		}

		///<summary>Removes the row's saved line from the open count; the row stays, not counted. Returns false when it could not be removed.</summary>
		private bool DeleteLine(CountRow countRow) {
			try {
				InventoryTracking.DeleteCountLine(countRow.SupplyCountLineNum);
			}
			catch(Exception ex) {
				MessageBox.Show(this,ex.Message);
				LoadCount();
				return false;
			}
			countRow.IsCounted=false;
			countRow.QtyCounted=0;
			countRow.SupplyCountLineNum=0;
			LoadCount();
			return true;
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
				//Saved once it is counted (scan or type its quantity), like every other row.
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
			CountRow countRow=_listCountRows[index];
			if(countRow.IsCounted && !DeleteLine(countRow)) {
				FillGrid();
				return;
			}
			_listCountRows.Remove(countRow);
			FillGrid();
		}

		private void butPost_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.InventoryAdjust)) {
				return;
			}
			if(_supplyCount==null || !_listCountRows.Any(x => x.IsCounted)) {
				MsgBox.Show(this,"Nothing is counted yet.");
				return;
			}
			if(textReason.Text.Trim()=="") {
				MsgBox.Show(this,"Please enter a reason, for example \"Quarterly count\".");
				return;
			}
			int countAdjusted;
			try {
				countAdjusted=InventoryTracking.PostOpenCount(_supplyCount.SupplyCountNum,textReason.Text);
			}
			catch(Exception ex) {
				//Some or all of it may be posted (a failure part-way, or the answer was lost). The count was opened again with each line's program
				//quantity read again from the ledger, so posting again writes only what still differs; read it again here.
				if(!LoadCount()) {
					SetReadOnly();
				}
				FillGrid();
				MessageBox.Show(this,ex.Message+"\r\n\r\n"+Lan.g(this,"Part of the count may have been posted. The program's quantities were read again: posting again writes only what still differs."));
				return;
			}
			_listCountRows.RemoveAll(x => x.IsCounted);
			textReason.Text="";
			LoadCount();
			FillGrid();
			MessageBox.Show(this,Lan.g(this,"Count posted. Rows adjusted")+": "+countAdjusted);
		}

		private void butDiscard_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.InventoryAdjust)) {
				return;
			}
			if(_supplyCount==null) {
				MsgBox.Show(this,"No stock count is open.");
				return;
			}
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,Lan.g(this,"Discard the open stock count with its")+" "+_listCountRows.Count(x => x.IsCounted)+" "
				+Lan.g(this,"counted row(s)? Stock is not changed, and the count cannot be resumed afterwards.")))
			{
				return;
			}
			try {
				InventoryTracking.DiscardOpenCount(_supplyCount.SupplyCountNum);
			}
			catch(Exception ex) {
				MessageBox.Show(this,ex.Message);
			}
			_listCountRows.Clear();
			textReason.Text="";
			LoadCount();
			FillGrid();
		}

		private void butClose_Click(object sender,EventArgs e) {
			//Every counted row is saved: the count stays open and is resumed from here or from Supply Inventory.
			DialogResult=DialogResult.OK;
		}
	}
}
