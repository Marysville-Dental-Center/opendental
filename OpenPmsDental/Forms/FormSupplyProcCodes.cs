// Inventory tracking (docs/specs/inventory-tracking.md): the default materials of procedure codes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Supply Inventory, Inventory, Default Materials. Needs Setup. Left: the procedure codes that have default materials; right: the
	///materials of the selected code and their default quantities. Every change is saved right away.</summary>
	public partial class FormSupplyProcCodes:FormODBase {
		private List<SupplyProcCode> _listSupplyProcCodes;
		private List<long> _listCodeNums=new List<long>();
		private List<Supply> _listSupplies=new List<Supply>();

		public FormSupplyProcCodes() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSupplyProcCodes_Load(object sender,EventArgs e) {
			RefreshData();
			FillGridCodes();
			if(gridCodes.ListGridRows.Count>0) {
				gridCodes.SetSelected(0,true);
			}
			FillGridMaterials();
		}

		private void RefreshData() {
			_listSupplyProcCodes=SupplyProcCodes.GetAll();
			_listCodeNums=_listCodeNums.Union(_listSupplyProcCodes.Select(x => x.CodeNum)).Distinct().ToList();
			_listSupplies=Supplies.GetMany(_listSupplyProcCodes.Select(x => x.SupplyNum).Distinct().ToList());
		}

		private long GetCodeNumSelected() {
			int index=gridCodes.GetSelectedIndex();
			if(index<0 || index>=gridCodes.ListGridRows.Count) {
				return 0;
			}
			return (long)gridCodes.ListGridRows[index].Tag;
		}

		private void FillGridCodes() {
			long codeNumSelected=GetCodeNumSelected();
			List<ProcedureCode> listProcedureCodes=_listCodeNums.Select(x => ProcedureCodes.GetProcCode(x)).OrderBy(x => x.ProcCode).ToList();
			gridCodes.BeginUpdate();
			gridCodes.Columns.Clear();
			gridCodes.Columns.Add(new GridColumn(Lan.g(this,"Code"),70));
			gridCodes.Columns.Add(new GridColumn(Lan.g(this,"Description"),190));
			gridCodes.Columns.Add(new GridColumn(Lan.g(this,"Items"),45,HorizontalAlignment.Center));
			gridCodes.ListGridRows.Clear();
			foreach(ProcedureCode procedureCode in listProcedureCodes) {
				GridRow row=new GridRow();
				row.Cells.Add(procedureCode.ProcCode);
				row.Cells.Add(procedureCode.Descript);
				row.Cells.Add(_listSupplyProcCodes.Count(x => x.CodeNum==procedureCode.CodeNum).ToString());
				row.Tag=procedureCode.CodeNum;
				gridCodes.ListGridRows.Add(row);
			}
			gridCodes.EndUpdate();
			int index=gridCodes.ListGridRows.FindIndex(x => (long)x.Tag==codeNumSelected);
			if(index>=0) {
				gridCodes.SetSelected(index,true);
			}
		}

		private List<SupplyProcCode> GetMaterialsSelected() {
			long codeNum=GetCodeNumSelected();
			return _listSupplyProcCodes.FindAll(x => x.CodeNum==codeNum).OrderBy(x => x.ItemOrder).ToList();
		}

		private void FillGridMaterials() {
			List<SupplyProcCode> listMaterials=GetMaterialsSelected();
			gridMaterials.BeginUpdate();
			gridMaterials.Columns.Clear();
			gridMaterials.Columns.Add(new GridColumn(Lan.g(this,"Catalog #"),80));
			gridMaterials.Columns.Add(new GridColumn(Lan.g(this,"Supply"),250));
			gridMaterials.Columns.Add(new GridColumn(Lan.g(this,"Default Qty"),75,HorizontalAlignment.Center,isEditable:true));
			gridMaterials.ListGridRows.Clear();
			foreach(SupplyProcCode supplyProcCode in listMaterials) {
				Supply supply=_listSupplies.Find(x => x.SupplyNum==supplyProcCode.SupplyNum);
				GridRow row=new GridRow();
				row.Cells.Add(supply?.CatalogNumber??"");
				row.Cells.Add(supply?.Descript??"");
				row.Cells.Add(InventoryTracking.FormatQty(supplyProcCode.Quantity));
				row.Tag=supplyProcCode;
				gridMaterials.ListGridRows.Add(row);
			}
			gridMaterials.EndUpdate();
			gridMaterials.Title=Lan.g(this,"Default Materials")+(GetCodeNumSelected()==0 ? "" : " - "+ProcedureCodes.GetStringProcCode(GetCodeNumSelected()));
		}

		///<summary>Saves the materials of the selected code and refreshes both grids.</summary>
		private void SaveMaterials(List<SupplyProcCode> listMaterials) {
			long codeNum=GetCodeNumSelected();
			if(codeNum==0) {
				return;
			}
			SupplyProcCodes.SyncForCode(codeNum,listMaterials);
			RefreshData();
			FillGridCodes();
			FillGridMaterials();
		}

		private void gridCodes_CellClick(object sender,ODGridClickEventArgs e) {
			FillGridMaterials();
		}

		private void butAddCode_Click(object sender,EventArgs e) {
			using FormProcCodes formProcCodes=new FormProcCodes();
			formProcCodes.IsSelectionMode=true;
			formProcCodes.CanAllowMultipleSelections=true;
			formProcCodes.ShowDialog();
			if(formProcCodes.DialogResult!=DialogResult.OK) {
				return;
			}
			List<long> listCodeNumsNew=formProcCodes.ListProcedureCodesSelected.Select(x => x.CodeNum).ToList();
			_listCodeNums=_listCodeNums.Union(listCodeNumsNew).ToList();
			FillGridCodes();
			int index=gridCodes.ListGridRows.FindIndex(x => listCodeNumsNew.Contains((long)x.Tag));
			if(index>=0) {
				gridCodes.SetSelected(index,true);
			}
			FillGridMaterials();
		}

		private void butAddSupply_Click(object sender,EventArgs e) {
			if(GetCodeNumSelected()==0) {
				MsgBox.Show(this,"Please select or add a procedure code first.");
				return;
			}
			using FormSupplies formSupplies=new FormSupplies();
			formSupplies.IsSelectMode=true;
			formSupplies.SupplierNumSelected=0;//Every supplier.
			if(formSupplies.ShowDialog()!=DialogResult.OK) {
				return;
			}
			List<SupplyProcCode> listMaterials=GetMaterialsSelected();
			foreach(Supply supply in formSupplies.ListSuppliesSelected) {
				if(listMaterials.Any(x => x.SupplyNum==supply.SupplyNum)) {
					continue;
				}
				listMaterials.Add(new SupplyProcCode { SupplyNum=supply.SupplyNum,Quantity=1 });
			}
			SaveMaterials(listMaterials);
		}

		private void butRemoveSupply_Click(object sender,EventArgs e) {
			int index=gridMaterials.GetSelectedIndex();
			if(index<0 || index>=gridMaterials.ListGridRows.Count) {
				MsgBox.Show(this,"Please select a material first.");
				return;
			}
			List<SupplyProcCode> listMaterials=GetMaterialsSelected();
			listMaterials.RemoveAt(index);
			SaveMaterials(listMaterials);
		}

		private void gridMaterials_CellLeave(object sender,ODGridClickEventArgs e) {
			if(e.Col!=2) {
				return;
			}
			List<SupplyProcCode> listMaterials=GetMaterialsSelected();
			if(e.Row<0 || e.Row>=listMaterials.Count) {
				return;
			}
			string text=gridMaterials.ListGridRows[e.Row].Cells[2].Text.Trim();
			if(!double.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out double qty) || qty<=0) {
				MsgBox.Show(this,"Please enter a quantity above 0.");
				FillGridMaterials();
				return;
			}
			if(Math.Round(qty,2)==listMaterials[e.Row].Quantity) {
				return;
			}
			listMaterials[e.Row].Quantity=Math.Round(qty,2);
			SaveMaterials(listMaterials);
		}

		private void butClose_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.OK;
		}
	}
}
