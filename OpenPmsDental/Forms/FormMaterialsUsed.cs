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
	///MaterialsUsedL after procedures are completed, from the Awaiting Materials list, and from the procedure edit window.
	///Every material but an implant is taken soonest-expiring first unless the assistant picks a lot: its row has lot -1, ConfirmMaterials chooses
	///the lots from the stock as it is when OK is pressed, and the grid shows the lots that would be taken now. An implant's row keeps the lot shown,
	///which is recorded with the procedure; its lot number can be typed or scanned (Lot Number).</summary>
	public partial class FormMaterialsUsed:FormODBase {
		///<summary>The procedures, in the order shown. Set before showing.</summary>
		public List<long> ListProcNums=new List<long>();
		///<summary>True to only show the materials (the user may not change confirmed materials, or tracking is off).</summary>
		public bool IsReadOnly;
		private List<Procedure> _listProcedures;
		///<summary>One line per material per procedure. SupplyLotNum -1: soonest-expiring first, chosen when OK is pressed.</summary>
		private List<SupplyUsageLine> _listSupplyUsageLines;
		///<summary>What the procedures held when the panel opened, per supply and lot (the ledger).</summary>
		private List<SupplyUsageLine> _listHeld=new List<SupplyUsageLine>();
		private List<Supply> _listSupplies=new List<Supply>();
		///<summary>The lots of the panel's supplies, empty ones included, with their stock when they were read.</summary>
		private List<SupplyLot> _listSupplyLots=new List<SupplyLot>();
		private List<SupplySetting> _listSupplySettings=new List<SupplySetting>();
		///<summary>Set by ComputePlan: the lots that the lines of lot -1 would take now.</summary>
		private List<SupplyUsageLine> _listAutoPlanned=new List<SupplyUsageLine>();
		///<summary>Set by ComputePlan: copies of the lots with what the panel plans to take taken off.</summary>
		private List<SupplyLot> _listSupplyLotsAfterPlan=new List<SupplyLot>();
		///<summary>The lots whose number the assistant entered here (Lot Number), from the package in hand.</summary>
		private List<long> _listSupplyLotNumsEntered=new List<long>();
		///<summary>The Supply column. Rows are selected in it, never in the Qty column: an edit box there would take the scanner's digits.</summary>
		private const int COL_SUPPLY=1;
		private const int COL_QTY=2;

		public FormMaterialsUsed() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormMaterialsUsed_Load(object sender,EventArgs e) {
			_listProcedures=Procedures.GetManyProc(ListProcNums,false);
			_listProcedures=ListProcNums.Select(x => _listProcedures.Find(y => y.ProcNum==x)).Where(x => x!=null).ToList();
			List<long> listProcNumsAwaiting=SupplyProcPendings.GetForProcs(ListProcNums).Select(x => x.ProcNum).ToList();
			_listHeld=InventoryTracking.GetProcMaterials(ListProcNums);
			//A procedure that awaits materials and holds none yet opens with its code's defaults; any other shows what it holds.
			List<Procedure> listProceduresDefault=_listProcedures.FindAll(x => listProcNumsAwaiting.Contains(x.ProcNum) && !_listHeld.Any(y => y.ProcNum==x.ProcNum));
			LoadSupplies(_listHeld.Select(x => x.SupplyNum));
			_listSupplyUsageLines=new List<SupplyUsageLine>();
			//What a procedure holds: an implant with the lot it was taken from; anything else as one line of lot -1 per supply, which keeps the lots
			//it holds as long as the quantity is not lowered.
			foreach(SupplyUsageLine supplyUsageLine in _listHeld.Where(x => x.Quantity>0)) {
				if(GetSetting(supplyUsageLine.SupplyNum).IsImplant) {
					_listSupplyUsageLines.Add(supplyUsageLine.Copy());
					continue;
				}
				AddToAutoLine(supplyUsageLine.ProcNum,supplyUsageLine.SupplyNum,supplyUsageLine.Quantity);
			}
			List<SupplyUsageLine> listDefaults=InventoryTracking.GetDefaultMaterials(listProceduresDefault,isLotAuto:true);
			LoadSupplies(listDefaults.Select(x => x.SupplyNum));
			foreach(SupplyUsageLine supplyUsageLine in listDefaults) {
				if(supplyUsageLine.SupplyLotNum==-1) {
					AddToAutoLine(supplyUsageLine.ProcNum,supplyUsageLine.SupplyNum,supplyUsageLine.Quantity);
					continue;
				}
				_listSupplyUsageLines.Add(supplyUsageLine);
			}
			if(IsReadOnly) {
				labelReadOnly.Visible=true;
				textScan.Enabled=false;
				butAdd.Enabled=false;
				butRemove.Enabled=false;
				comboLot.Enabled=false;
				butLotNumber.Enabled=false;
				butOK.Visible=false;
				butCancel.Text=Lan.g(this,"Close");
			}
			if(_listProcedures.Count>1) {
				Text=Lan.g(this,"Materials used")+" - "+_listProcedures.Count+" "+Lan.g(this,"procedures");
			}
			FillGrid();
			if(gridMain.ListGridRows.Count>0) {
				gridMain.SetSelected(new System.Drawing.Point(COL_SUPPLY,0));
			}
			FillComboLot();
			//The keyboard-wedge scanner types into the control that has focus.
			textScan.Select();
		}

		///<summary>Adds the quantity to the procedure's line of lot -1 of the supply, or adds that line.</summary>
		private SupplyUsageLine AddToAutoLine(long procNum,long supplyNum,double qty) {
			SupplyUsageLine supplyUsageLine=_listSupplyUsageLines.Find(x => x.ProcNum==procNum && x.SupplyNum==supplyNum && x.SupplyLotNum==-1);
			if(supplyUsageLine==null) {
				supplyUsageLine=new SupplyUsageLine(procNum,supplyNum,-1,0);
				_listSupplyUsageLines.Add(supplyUsageLine);
			}
			supplyUsageLine.Quantity=Math.Round(supplyUsageLine.Quantity+qty,2);
			return supplyUsageLine;
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

		///<summary>The lots the lines of lot -1 would take now, procedure by procedure, from the lots as the panel read them, and the lots with what
		///every row plans to take taken off. The lots are chosen again from the database when OK is pressed.</summary>
		private void ComputePlan() {
			_listSupplyLotsAfterPlan=_listSupplyLots.Select(x => x.Copy()).ToList();
			_listAutoPlanned=InventoryTracking.ResolveAutoLines(_listProcedures.Select(x => x.ProcNum).ToList(),_listSupplyUsageLines,_listHeld,
				_listSupplyLotsAfterPlan);
		}

		private void FillGrid() {
			int indexSelected=gridMain.GetSelectedIndex();
			_listSupplyUsageLines=_listSupplyUsageLines
				.OrderBy(x => _listProcedures.FindIndex(y => y.ProcNum==x.ProcNum))
				.ToList();
			ComputePlan();
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
				SupplySetting supplySetting=GetSetting(supplyUsageLine.SupplyNum);
				GridRow row=new GridRow();
				row.Cells.Add(GetProcText(supplyUsageLine.ProcNum));
				row.Cells.Add((supply?.Descript??"")+(supplySetting.IsImplant ? " ("+Lan.g(this,"implant")+")" : ""));
				row.Cells.Add(InventoryTracking.FormatQty(supplyUsageLine.Quantity));
				List<SupplyUsageLine> listLots;
				if(supplyUsageLine.SupplyLotNum==-1) {
					listLots=_listAutoPlanned.FindAll(x => x.ProcNum==supplyUsageLine.ProcNum && x.SupplyNum==supplyUsageLine.SupplyNum);
				}
				else {
					listLots=new List<SupplyUsageLine> { supplyUsageLine };
				}
				row.Cells.Add(GetLotText(listLots,supplySetting.IsLotTracked));
				row.Cells.Add(GetExpiryText(listLots));
				row.Cells.Add(supply==null ? "" : InventoryTracking.FormatQty(supply.LevelOnHand));
				row.Tag=supplyUsageLine;
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
			if(indexSelected>=0 && indexSelected<gridMain.ListGridRows.Count) {
				gridMain.SetSelected(new System.Drawing.Point(COL_SUPPLY,indexSelected));
			}
		}

		///<summary>The lot numbers of the lines; with the quantity of each when there are several (a quantity split over lots).</summary>
		private string GetLotText(List<SupplyUsageLine> listLots,bool isLotTracked) {
			List<string> listTexts=new List<string>();
			foreach(SupplyUsageLine supplyUsageLine in listLots) {
				SupplyLot supplyLot=_listSupplyLots.Find(x => x.SupplyLotNum==supplyUsageLine.SupplyLotNum);
				string text=supplyLot?.LotNumber??(isLotTracked || listLots.Count>1 ? Lan.g(this,"(no lot)") : "");
				if(listLots.Count>1) {
					text+=" ("+InventoryTracking.FormatQty(supplyUsageLine.Quantity)+")";
				}
				listTexts.Add(text);
			}
			return string.Join(", ",listTexts.Where(x => x!=""));
		}

		///<summary>The earliest expiry date of the lots of the lines, blank when none has one.</summary>
		private string GetExpiryText(List<SupplyUsageLine> listLots) {
			List<SupplyLot> listSupplyLots=_listSupplyLots.FindAll(x => listLots.Any(y => y.SupplyLotNum==x.SupplyLotNum) && x.HasExpiry());
			if(listSupplyLots.Count==0) {
				return "";
			}
			return listSupplyLots.Min(x => x.DateExpiry).ToShortDateString();
		}

		private SupplyUsageLine GetSelectedLine() {
			int index=gridMain.GetSelectedIndex();
			if(index<0 || index>=_listSupplyUsageLines.Count) {
				return null;
			}
			return _listSupplyUsageLines[index];
		}

		///<summary>Selects the line's row in the Supply column (no edit box) and puts the keyboard back in the Scan box once the grid is drawn, for the
		///next scan.</summary>
		private void SelectLineAndScanBox(SupplyUsageLine supplyUsageLine) {
			int index=_listSupplyUsageLines.IndexOf(supplyUsageLine);
			if(index>=0) {
				gridMain.SetSelected(new System.Drawing.Point(COL_SUPPLY,index));
			}
			FillComboLot();
			if(IsHandleCreated) {
				BeginInvoke((Action)(() => textScan.Focus()));
			}
		}

		///<summary>The lots of the selected row's supply, soonest-expiring first, with the row's lot selected. A material other than an implant can
		///be set back to soonest-expiring first. An implant's lot is recorded with the procedure, so "(no lot)" is offered for it only while no lot
		///has stock (or the row has no lot).</summary>
		private void FillComboLot() {
			comboLot.Items.Clear();
			SupplyUsageLine supplyUsageLine=GetSelectedLine();
			if(supplyUsageLine==null) {
				butLotNumber.Enabled=false;
				return;
			}
			SupplySetting supplySetting=GetSetting(supplyUsageLine.SupplyNum);
			List<SupplyLot> listSupplyLotsShown=SupplyLots.SortFifo(_listSupplyLots.FindAll(x => x.SupplyNum==supplyUsageLine.SupplyNum
				&& (Math.Round(x.QtyOnHand,2)>0 || x.SupplyLotNum==supplyUsageLine.SupplyLotNum)));
			if(!supplySetting.IsImplant) {
				comboLot.Items.Add(Lan.g(this,"(soonest-expiring first)"),new SupplyLot { SupplyLotNum=-1 });
			}
			if(!supplySetting.IsImplant || supplyUsageLine.SupplyLotNum==0 || !listSupplyLotsShown.Any(x => Math.Round(x.QtyOnHand,2)>0)) {
				comboLot.Items.Add(Lan.g(this,"(no lot)"),new SupplyLot { SupplyLotNum=0 });
			}
			foreach(SupplyLot supplyLot in listSupplyLotsShown) {
				string text=supplyLot.LotNumber+(supplyLot.HasExpiry() ? ", "+Lan.g(this,"exp")+" "+supplyLot.DateExpiry.ToShortDateString() : "")
					+" ("+InventoryTracking.FormatQty(supplyLot.QtyOnHand)+")";
				comboLot.Items.Add(text,supplyLot);
			}
			comboLot.SetSelectedKey<SupplyLot>(supplyUsageLine.SupplyLotNum,x => x.SupplyLotNum);
			butLotNumber.Enabled=!IsReadOnly && supplySetting.IsLotTracked;
		}

		private void gridMain_CellClick(object sender,ODGridClickEventArgs e) {
			FillComboLot();
		}

		private void gridMain_CellLeave(object sender,ODGridClickEventArgs e) {
			if(e.Col!=COL_QTY || e.Row<0 || e.Row>=_listSupplyUsageLines.Count) {
				return;
			}
			string text=gridMain.ListGridRows[e.Row].Cells[COL_QTY].Text;
			if(!double.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out double qty) || qty<0) {
				MsgBox.Show(this,"Please enter a quantity of 0 or more.");
			}
			else if(MaterialsUsedL.IsQtyAccepted(this,qty,MaterialsUsedL.QTY_ASK_MATERIALS)) {
				_listSupplyUsageLines[e.Row].Quantity=Math.Round(qty,2);
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
			if(supplyUsageLine.SupplyLotNum==-1) {
				//One line of lot -1 per procedure and supply.
				SupplyUsageLine supplyUsageLineAuto=_listSupplyUsageLines.Find(x => x!=supplyUsageLine && x.ProcNum==supplyUsageLine.ProcNum
					&& x.SupplyNum==supplyUsageLine.SupplyNum && x.SupplyLotNum==-1);
				if(supplyUsageLineAuto!=null) {
					supplyUsageLineAuto.Quantity=Math.Round(supplyUsageLineAuto.Quantity+supplyUsageLine.Quantity,2);
					_listSupplyUsageLines.Remove(supplyUsageLine);
					supplyUsageLine=supplyUsageLineAuto;
				}
			}
			FillGrid();
			SelectLineAndScanBox(supplyUsageLine);
		}

		///<summary>The lot number printed on the package, typed or scanned, for the selected row of a supply tracked by lot (an implant that came in
		///as an opening balance has no lot in the program). The lot is found or created; when OK is pressed, what the lot lacks is moved into it from
		///the stock without a lot before it is deducted (ConfirmMaterials).</summary>
		private void butLotNumber_Click(object sender,EventArgs e) {
			SupplyUsageLine supplyUsageLine=GetSelectedLine();
			if(supplyUsageLine==null) {
				MsgBox.Show(this,"Please select a row first.");
				return;
			}
			if(!GetSetting(supplyUsageLine.SupplyNum).IsLotTracked) {
				MsgBox.Show(this,"This supply is not tracked by lot.");
				return;
			}
			InputBox inputBoxNumber=new InputBox(Lan.g(this,"Lot number from the package (type it or scan it)"));
			inputBoxNumber.ShowDialog();
			if(inputBoxNumber.IsDialogCancel || inputBoxNumber.StringResult.Trim()=="") {
				return;
			}
			string lotNumber=inputBoxNumber.StringResult.Trim();
			SupplyLot supplyLot=_listSupplyLots.Find(x => x.SupplyNum==supplyUsageLine.SupplyNum && string.Equals(x.LotNumber.Trim(),lotNumber,StringComparison.OrdinalIgnoreCase));
			if(supplyLot==null) {
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
				try {
					supplyLot=SupplyLots.GetOrInsert(supplyUsageLine.SupplyNum,lotNumber,dateExpiry,0);
				}
				catch(Exception ex) {
					MessageBox.Show(this,ex.Message);
					return;
				}
				if(!_listSupplyLots.Any(x => x.SupplyLotNum==supplyLot.SupplyLotNum)) {
					_listSupplyLots.Add(supplyLot);
				}
			}
			supplyUsageLine.SupplyLotNum=supplyLot.SupplyLotNum;
			if(!_listSupplyLotNumsEntered.Contains(supplyLot.SupplyLotNum)) {
				_listSupplyLotNumsEntered.Add(supplyLot.SupplyLotNum);
			}
			FillGrid();
			SelectLineAndScanBox(supplyUsageLine);
		}

		///<summary>The procedure an added item belongs to: the selected row's, or the first procedure.</summary>
		private long GetProcNumForAdd() {
			SupplyUsageLine supplyUsageLine=GetSelectedLine();
			if(supplyUsageLine!=null) {
				return supplyUsageLine.ProcNum;
			}
			return _listProcedures.FirstOrDefault()?.ProcNum??0;
		}

		///<summary>Adds one of the supply to the procedure. Anything but an implant: one more on the procedure's line of lot -1 (soonest-expiring
		///first when OK is pressed). An implant: one more on a row of the procedure whose lot still has one free after what the panel plans, otherwise
		///a row in the soonest-expiring lot that has one free (without a lot when none has).</summary>
		private void AddSupply(Supply supply,long procNum) {
			if(procNum==0) {
				return;
			}
			LoadSupplies(new[] { supply.SupplyNum });
			SupplyUsageLine supplyUsageLine;
			if(GetSetting(supply.SupplyNum).IsImplant) {
				ComputePlan();
				supplyUsageLine=_listSupplyUsageLines.Find(x => x.ProcNum==procNum && x.SupplyNum==supply.SupplyNum && x.SupplyLotNum>0
					&& Math.Round(_listSupplyLotsAfterPlan.Find(y => y.SupplyLotNum==x.SupplyLotNum)?.QtyOnHand??0,2)>=1);
				if(supplyUsageLine==null) {
					long supplyLotNum=SupplyLots.AllocateFifo(supply.SupplyNum,1,_listSupplyLotsAfterPlan).First().SupplyLotNum;
					supplyUsageLine=_listSupplyUsageLines.Find(x => x.ProcNum==procNum && x.SupplyNum==supply.SupplyNum && x.SupplyLotNum==supplyLotNum);
					if(supplyUsageLine==null) {
						supplyUsageLine=new SupplyUsageLine(procNum,supply.SupplyNum,supplyLotNum,0);
						_listSupplyUsageLines.Add(supplyUsageLine);
					}
				}
				supplyUsageLine.Quantity=Math.Round(supplyUsageLine.Quantity+1,2);
			}
			else {
				supplyUsageLine=AddToAutoLine(procNum,supply.SupplyNum,1);
			}
			FillGrid();
			SelectLineAndScanBox(supplyUsageLine);
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
				textScan.Focus();
				return;
			}
			AddSupply(listSupplies[0],GetProcNumForAdd());
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

		///<summary>What the procedure held when the panel opened of the supply and lot.</summary>
		private double GetQtyHeld(long procNum,long supplyNum,long supplyLotNum) {
			return Math.Round(_listHeld.Where(x => x.ProcNum==procNum && x.SupplyNum==supplyNum && x.SupplyLotNum==supplyLotNum).Sum(x => x.Quantity),2);
		}

		///<summary>The rows of explicit lots that take more from their lot than it holds now (read again from the database): another workstation may
		///have taken the lot since the panel opened, or the row asks for more. For a lot whose number was entered here (Lot Number), the stock
		///without a lot counts too, because ConfirmMaterials moves it into the lot. One text line per lot; empty when none.</summary>
		private string GetLotsShortText(List<SupplyUsageLine> listLines) {
			List<SupplyUsageLine> listExplicit=listLines.FindAll(x => x.SupplyLotNum>0);
			if(listExplicit.Count==0) {
				return "";
			}
			List<SupplyLot> listSupplyLotsNow=SupplyLots.GetMany(listExplicit.Select(x => x.SupplyLotNum).Distinct().ToList());
			List<string> listTexts=new List<string>();
			foreach(IGrouping<long,SupplyUsageLine> grouping in listExplicit.GroupBy(x => x.SupplyLotNum)) {
				SupplyLot supplyLot=listSupplyLotsNow.Find(x => x.SupplyLotNum==grouping.Key);
				if(supplyLot==null) {
					continue;
				}
				double qtyTaken=Math.Round(grouping.Sum(x => x.Quantity-GetQtyHeld(x.ProcNum,x.SupplyNum,x.SupplyLotNum)),2);
				if(qtyTaken<=0) {
					continue;
				}
				double qtyThere=Math.Max(0,Math.Round(supplyLot.QtyOnHand,2));
				if(_listSupplyLotNumsEntered.Contains(supplyLot.SupplyLotNum) && GetSetting(supplyLot.SupplyNum).IsLotTracked) {
					qtyThere+=Math.Max(0,Math.Round(SupplyLedgers.GetSumWithoutLot(supplyLot.SupplyNum),2));
				}
				if(qtyTaken<=qtyThere) {
					continue;
				}
				Supply supply=_listSupplies.Find(x => x.SupplyNum==supplyLot.SupplyNum);
				listTexts.Add((supply?.Descript??"")+", "+Lan.g(this,"lot")+" "+supplyLot.LotNumber+": "+Lan.g(this,"holds")+" "
					+InventoryTracking.FormatQty(supplyLot.QtyOnHand)+", "+Lan.g(this,"taken")+" "+InventoryTracking.FormatQty(qtyTaken));
			}
			return string.Join("\r\n",listTexts);
		}

		private void butOK_Click(object sender,EventArgs e) {
			if(IsReadOnly) {
				DialogResult=DialogResult.Cancel;
				return;
			}
			List<SupplyUsageLine> listLines=_listSupplyUsageLines.FindAll(x => x.Quantity>0);
			//An implant's lot is recorded with the procedure: one taken now without a lot needs the lot number, or a reason for the audit trail.
			string reasonImplantWithoutLot="";
			List<SupplyUsageLine> listImplantsWithoutLot=listLines.FindAll(x => x.SupplyLotNum==0 && GetSetting(x.SupplyNum).IsImplant
				&& Math.Round(x.Quantity-GetQtyHeld(x.ProcNum,x.SupplyNum,0),2)>0);
			if(listImplantsWithoutLot.Count>0) {
				string textImplants=string.Join("\r\n",listImplantsWithoutLot.Select(x => _listSupplies.Find(y => y.SupplyNum==x.SupplyNum)?.Descript??""));
				MessageBox.Show(this,Lan.g(this,"The lot number of an implant is recorded with the procedure. Select the row and enter the lot number from the package with Lot Number, or give a reason to deduct it without a lot number")
					+":\r\n"+textImplants);
				InputBox inputBoxReason=new InputBox(Lan.g(this,"Reason to deduct the implant without a lot number (kept in the audit trail)"));
				inputBoxReason.ShowDialog();
				if(inputBoxReason.IsDialogCancel || inputBoxReason.StringResult.Trim()=="") {
					return;
				}
				reasonImplantWithoutLot=inputBoxReason.StringResult.Trim();
			}
			string textShort;
			try {
				textShort=GetLotsShortText(listLines);
			}
			catch(Exception ex) {
				MessageBox.Show(this,ex.Message);
				return;
			}
			if(textShort!="" && MessageBox.Show(this,Lan.g(this,"These lots hold less than is taken from them")+":\r\n"+textShort+"\r\n"
				+Lan.g(this,"Stock of the supply without a lot is moved into the lot first, as far as there is some. Deduct anyway?"),"",
				MessageBoxButtons.OKCancel)!=DialogResult.OK)
			{
				return;
			}
			List<long> listProcNumsSkipped;
			try {
				listProcNumsSkipped=InventoryTracking.ConfirmMaterials(_listProcedures.Select(x => x.ProcNum).ToList(),listLines,reasonImplantWithoutLot);
			}
			catch(Exception ex) {
				MessageBox.Show(this,Lan.g(this,"The materials could not be saved. Pressing OK again saves only what is still missing; until then the procedures stay on the Awaiting Materials list.")
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
