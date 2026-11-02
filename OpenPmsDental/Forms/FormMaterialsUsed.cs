// Inventory tracking (docs/specs/inventory-tracking.md): the Materials used panel.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>The materials of one or more completed procedures, for the assistant to confirm or change: quantities, lots, items added from the
	///supply list or by scanning, items removed. OK deducts what is shown (only the difference to what the procedures already hold). Opened by
	///MaterialsUsedL after procedures are completed, from the Awaiting Materials list, and from the procedure edit window.</summary>
	public partial class FormMaterialsUsed:FormODBase {
		///<summary>The procedures, in the order shown. Set before showing.</summary>
		public List<long> ListProcNums=new List<long>();
		///<summary>True to only show the materials (the user may not change confirmed materials).</summary>
		public bool IsReadOnly;
		private List<Procedure> _listProcedures;
		private List<SupplyUsageLine> _listSupplyUsageLines;
		private List<Supply> _listSupplies=new List<Supply>();
		private List<SupplyLot> _listSupplyLots=new List<SupplyLot>();
		private List<SupplySetting> _listSupplySettings=new List<SupplySetting>();

		public FormMaterialsUsed() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormMaterialsUsed_Load(object sender,EventArgs e) {
			_listProcedures=Procedures.GetManyProc(ListProcNums,false);
			_listProcedures=ListProcNums.Select(x => _listProcedures.Find(y => y.ProcNum==x)).Where(x => x!=null).ToList();
			List<long> listProcNumsAwaiting=SupplyProcPendings.GetForProcs(ListProcNums).Select(x => x.ProcNum).ToList();
			List<SupplyUsageLine> listHeld=InventoryTracking.GetProcMaterials(ListProcNums);
			//A procedure that awaits materials and holds none yet opens with its code's defaults; any other shows what it holds.
			List<Procedure> listProceduresDefault=_listProcedures.FindAll(x => listProcNumsAwaiting.Contains(x.ProcNum) && !listHeld.Any(y => y.ProcNum==x.ProcNum));
			_listSupplyUsageLines=listHeld.FindAll(x => !listProceduresDefault.Any(y => y.ProcNum==x.ProcNum));
			_listSupplyUsageLines.AddRange(InventoryTracking.GetDefaultMaterials(listProceduresDefault));
			LoadSupplies(_listSupplyUsageLines.Select(x => x.SupplyNum));
			if(IsReadOnly) {
				labelReadOnly.Visible=true;
				textScan.Enabled=false;
				butAdd.Enabled=false;
				butRemove.Enabled=false;
				comboLot.Enabled=false;
				butOK.Visible=false;
				butCancel.Text=Lan.g(this,"Close");
			}
			if(_listProcedures.Count>1) {
				Text=Lan.g(this,"Materials used")+" - "+_listProcedures.Count+" "+Lan.g(this,"procedures");
			}
			FillGrid();
			if(gridMain.ListGridRows.Count>0) {
				gridMain.SetSelected(new System.Drawing.Point(2,0));
			}
			FillComboLot();
		}

		///<summary>Adds the supplies, their lots and settings to the local lists, read from the database now.</summary>
		private void LoadSupplies(IEnumerable<long> supplyNums) {
			List<long> listSupplyNums=supplyNums.Distinct().Where(x => !_listSupplies.Any(y => y.SupplyNum==x)).ToList();
			if(listSupplyNums.Count==0) {
				return;
			}
			_listSupplies.AddRange(Supplies.GetMany(listSupplyNums));
			_listSupplyLots.AddRange(SupplyLots.GetForSupplies(listSupplyNums,isEmptyIncluded:true));
			_listSupplySettings.AddRange(SupplySettings.GetForSupplies(listSupplyNums));
		}

		private SupplySetting GetSetting(long supplyNum) {
			return _listSupplySettings.Find(x => x.SupplyNum==supplyNum)??SupplySettings.CreateDefault(supplyNum);
		}

		private string GetProcText(long procNum) {
			Procedure procedure=_listProcedures.Find(x => x.ProcNum==procNum);
			if(procedure==null) {
				return "";
			}
			ProcedureCode procedureCode=ProcedureCodes.GetProcCode(procedure.CodeNum);
			string text=procedure.ProcDate.ToShortDateString()+" "+procedureCode.ProcCode;
			if(procedure.ToothNum!="") {
				text+=" #"+Tooth.Display(procedure.ToothNum);
			}
			return text+" "+procedureCode.AbbrDesc;
		}

		private void FillGrid() {
			int indexSelected=gridMain.GetSelectedIndex();
			_listSupplyUsageLines=_listSupplyUsageLines
				.OrderBy(x => _listProcedures.FindIndex(y => y.ProcNum==x.ProcNum))
				.ToList();
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Procedure"),200));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Supply"),230));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Qty"),50,HorizontalAlignment.Center,isEditable:!IsReadOnly));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Lot"),120));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Expires"),72));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"On Hand"),60,HorizontalAlignment.Center));
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listSupplyUsageLines.Count;i++) {
				SupplyUsageLine supplyUsageLine=_listSupplyUsageLines[i];
				Supply supply=_listSupplies.Find(x => x.SupplyNum==supplyUsageLine.SupplyNum);
				SupplyLot supplyLot=_listSupplyLots.Find(x => x.SupplyLotNum==supplyUsageLine.SupplyLotNum);
				GridRow row=new GridRow();
				row.Cells.Add(GetProcText(supplyUsageLine.ProcNum));
				row.Cells.Add((supply?.Descript??"")+(GetSetting(supplyUsageLine.SupplyNum).IsImplant ? " ("+Lan.g(this,"implant")+")" : ""));
				row.Cells.Add(InventoryTracking.FormatQty(supplyUsageLine.Quantity));
				if(supplyLot!=null) {
					row.Cells.Add(supplyLot.LotNumber);
					row.Cells.Add(supplyLot.HasExpiry() ? supplyLot.DateExpiry.ToShortDateString() : "");
				}
				else {
					row.Cells.Add(GetSetting(supplyUsageLine.SupplyNum).IsLotTracked ? Lan.g(this,"(no lot)") : "");
					row.Cells.Add("");
				}
				row.Cells.Add(supply==null ? "" : InventoryTracking.FormatQty(supply.LevelOnHand));
				row.Tag=supplyUsageLine;
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
			if(indexSelected>=0 && indexSelected<gridMain.ListGridRows.Count) {
				gridMain.SetSelected(new System.Drawing.Point(2,indexSelected));
			}
		}

		private SupplyUsageLine GetSelectedLine() {
			int index=gridMain.GetSelectedIndex();
			if(index<0 || index>=_listSupplyUsageLines.Count) {
				return null;
			}
			return _listSupplyUsageLines[index];
		}

		///<summary>The lots of the selected row's supply, soonest-expiring first, with the row's lot selected.</summary>
		private void FillComboLot() {
			comboLot.Items.Clear();
			SupplyUsageLine supplyUsageLine=GetSelectedLine();
			if(supplyUsageLine==null) {
				return;
			}
			comboLot.Items.Add(Lan.g(this,"(no lot)"),new SupplyLot { SupplyLotNum=0 });
			foreach(SupplyLot supplyLot in SupplyLots.SortFifo(_listSupplyLots.FindAll(x => x.SupplyNum==supplyUsageLine.SupplyNum
				&& (Math.Round(x.QtyOnHand,2)>0 || x.SupplyLotNum==supplyUsageLine.SupplyLotNum))))
			{
				string text=supplyLot.LotNumber+(supplyLot.HasExpiry() ? ", "+Lan.g(this,"exp")+" "+supplyLot.DateExpiry.ToShortDateString() : "")
					+" ("+InventoryTracking.FormatQty(supplyLot.QtyOnHand)+")";
				comboLot.Items.Add(text,supplyLot);
			}
			comboLot.SetSelectedKey<SupplyLot>(supplyUsageLine.SupplyLotNum,x => x.SupplyLotNum);
		}

		private void gridMain_CellClick(object sender,ODGridClickEventArgs e) {
			FillComboLot();
		}

		private void gridMain_CellLeave(object sender,ODGridClickEventArgs e) {
			if(e.Col!=2 || e.Row<0 || e.Row>=_listSupplyUsageLines.Count) {
				return;
			}
			string text=gridMain.ListGridRows[e.Row].Cells[2].Text;
			if(double.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out double qty) && qty>=0) {
				_listSupplyUsageLines[e.Row].Quantity=Math.Round(qty,2);
			}
			else {
				MsgBox.Show(this,"Please enter a quantity of 0 or more.");
			}
			FillGrid();
		}

		private void comboLot_SelectionChangeCommitted(object sender,EventArgs e) {
			SupplyUsageLine supplyUsageLine=GetSelectedLine();
			SupplyLot supplyLot=comboLot.GetSelected<SupplyLot>();
			if(supplyUsageLine==null || supplyLot==null) {
				return;
			}
			supplyUsageLine.SupplyLotNum=supplyLot.SupplyLotNum;
			FillGrid();
		}

		///<summary>The procedure an added item belongs to: the selected row's, or the first procedure.</summary>
		private long GetProcNumForAdd() {
			SupplyUsageLine supplyUsageLine=GetSelectedLine();
			if(supplyUsageLine!=null) {
				return supplyUsageLine.ProcNum;
			}
			return _listProcedures.FirstOrDefault()?.ProcNum??0;
		}

		///<summary>Adds one of the supply to the procedure: one more on its row, or a new row in the lot that expires first.</summary>
		private void AddSupply(Supply supply,long procNum) {
			if(procNum==0) {
				return;
			}
			LoadSupplies(new[] { supply.SupplyNum });
			SupplyUsageLine supplyUsageLine=_listSupplyUsageLines.Find(x => x.ProcNum==procNum && x.SupplyNum==supply.SupplyNum);
			if(supplyUsageLine!=null) {
				supplyUsageLine.Quantity=Math.Round(supplyUsageLine.Quantity+1,2);
			}
			else {
				long supplyLotNum=SupplyLots.AllocateFifo(supply.SupplyNum,1,_listSupplyLots).First().SupplyLotNum;
				supplyUsageLine=new SupplyUsageLine(procNum,supply.SupplyNum,supplyLotNum,1);
				_listSupplyUsageLines.Add(supplyUsageLine);
			}
			FillGrid();
			int index=_listSupplyUsageLines.IndexOf(supplyUsageLine);
			if(index>=0) {
				gridMain.SetSelected(new System.Drawing.Point(2,index));
			}
			FillComboLot();
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
			AddSupply(listSupplies[0],GetProcNumForAdd());
			textScan.Focus();
		}

		private void butAdd_Click(object sender,EventArgs e) {
			using FormSupplies formSupplies=new FormSupplies();
			formSupplies.IsSelectMode=true;
			formSupplies.SupplierNumSelected=0;//Every supplier.
			if(formSupplies.ShowDialog()!=DialogResult.OK) {
				return;
			}
			long procNum=GetProcNumForAdd();
			foreach(Supply supply in formSupplies.ListSuppliesSelected) {
				AddSupply(supply,procNum);
			}
		}

		private void butRemove_Click(object sender,EventArgs e) {
			SupplyUsageLine supplyUsageLine=GetSelectedLine();
			if(supplyUsageLine==null) {
				MsgBox.Show(this,"Please select a row first.");
				return;
			}
			_listSupplyUsageLines.Remove(supplyUsageLine);
			FillGrid();
			FillComboLot();
		}

		private void butOK_Click(object sender,EventArgs e) {
			if(IsReadOnly) {
				DialogResult=DialogResult.Cancel;
				return;
			}
			List<long> listProcNumsSkipped;
			try {
				listProcNumsSkipped=InventoryTracking.ConfirmMaterials(_listProcedures.Select(x => x.ProcNum).ToList(),
					_listSupplyUsageLines.FindAll(x => x.Quantity>0));
			}
			catch(Exception ex) {
				MessageBox.Show(this,Lan.g(this,"The materials could not be saved. Nothing is lost: the procedures stay on the Awaiting Materials list.")
					+"\r\n"+ex.Message);
				return;
			}
			if(listProcNumsSkipped.Count>0) {
				MessageBox.Show(this,Lan.g(this,"Not deducted, because the procedure is no longer complete")+":\r\n"
					+string.Join("\r\n",listProcNumsSkipped.Select(x => GetProcText(x))));
			}
			DialogResult=DialogResult.OK;
		}

		private void butCancel_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.Cancel;
		}
	}
}
