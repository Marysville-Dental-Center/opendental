// Inventory tracking (docs/specs/inventory-tracking.md): completed procedures whose materials nobody has confirmed yet.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Supply Inventory, Inventory, Awaiting Materials. Procedures completed while tracking was on whose materials were not confirmed: the
	///Materials used window was cancelled, or the procedure was completed without a user (another workstation's background work, an interface).
	///Materials opens the panel for the selected procedures (one panel for several); No Materials confirms them with nothing used.</summary>
	public partial class FormSupplyProcPendings:FormODBase {
		private DataTable _table;

		public FormSupplyProcPendings() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormSupplyProcPendings_Load(object sender,EventArgs e) {
			FillGrid();
		}

		private void FillGrid() {
			_table=SupplyProcPendings.GetTableAll();
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Completed"),120));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Patient"),160));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Proc Date"),75));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Code"),60));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Tooth"),45));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Description"),180));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"User"),90));
			gridMain.ListGridRows.Clear();
			foreach(DataRow dataRow in _table.Rows) {
				GridRow row=new GridRow();
				row.Cells.Add(PIn.DateT(dataRow["DateTimeEntry"].ToString()).ToString("g"));
				row.Cells.Add(dataRow["LName"].ToString()+", "+dataRow["FName"].ToString());
				row.Cells.Add(PIn.Date(dataRow["ProcDate"].ToString()).ToShortDateString());
				row.Cells.Add(dataRow["ProcCode"].ToString());
				row.Cells.Add(Tooth.Display(dataRow["ToothNum"].ToString()));
				row.Cells.Add(dataRow["Descript"].ToString());
				long userNum=PIn.Long(dataRow["UserNum"].ToString());
				row.Cells.Add(userNum==0 ? Lan.g(this,"(no user)") : Userods.GetName(userNum));
				row.Tag=PIn.Long(dataRow["ProcNum"].ToString());
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
		}

		private List<long> GetSelectedProcNums() {
			return gridMain.SelectedIndices.Select(x => (long)gridMain.ListGridRows[x].Tag).ToList();
		}

		private void butMaterials_Click(object sender,EventArgs e) {
			List<long> listProcNums=GetSelectedProcNums();
			if(listProcNums.Count==0) {
				MsgBox.Show(this,"Please select one or more procedures first.");
				return;
			}
			MaterialsUsedL.ShowForAwaiting(this,listProcNums);
			FillGrid();
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			MaterialsUsedL.ShowForAwaiting(this,new List<long> { (long)gridMain.ListGridRows[e.Row].Tag });
			FillGrid();
		}

		private void butNoMaterials_Click(object sender,EventArgs e) {
			List<long> listProcNums=GetSelectedProcNums();
			if(listProcNums.Count==0) {
				MsgBox.Show(this,"Please select one or more procedures first.");
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Confirm that the selected procedures used no materials? Nothing is deducted.")) {
				return;
			}
			InventoryTracking.ConfirmMaterials(listProcNums,new List<SupplyUsageLine>());
			FillGrid();
		}

		private void butClose_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.OK;
		}
	}
}
