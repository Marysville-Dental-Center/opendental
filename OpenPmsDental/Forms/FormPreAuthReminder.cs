// The Chart module's preauthorization reminder window (Open Dental 26.1, feature v26_1-lists-carrier-preauth-flags, package M2-CARRIER in
// docs/specs/port-backlog.md): lists the charted procedures that the patient's carriers want preauthorized, with "Create Preauth Now" and "Later".
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>DialogResult OK means "Create Preauth Now"; Cancel means "Later".</summary>
	public partial class FormPreAuthReminder:FormODBase {
		private List<PreAuthNeed> _listPreAuthNeeds;

		public FormPreAuthReminder(List<PreAuthNeed> listPreAuthNeeds) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_listPreAuthNeeds=listPreAuthNeeds??new List<PreAuthNeed>();
		}

		private void FormPreAuthReminder_Load(object sender,EventArgs e) {
			List<string> listCarrierNames=_listPreAuthNeeds.Select(x => x.CarrierName).Distinct().ToList();
			labelExplain.Text=Lan.g(this,"The patient's insurance carrier wants a preauthorization for these procedures:")+" "+string.Join(", ",listCarrierNames)+".";
			FillGrid();
		}

		private void FillGrid() {
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Code"),70));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Tth"),40));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Surf"),45));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Description"),200));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Carrier"),160));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Plan"),60));
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listPreAuthNeeds.Count;i++) {
				PreAuthNeed preAuthNeed=_listPreAuthNeeds[i];
				GridRow row=new GridRow();
				row.Cells.Add(preAuthNeed.ProcCode);
				row.Cells.Add(Tooth.Display(preAuthNeed.ToothNum));
				row.Cells.Add(preAuthNeed.Surf);
				row.Cells.Add(ProcedureCodes.GetProcCode(preAuthNeed.CodeNum).Descript);
				row.Cells.Add(preAuthNeed.CarrierName);
				row.Cells.Add(GetPlanText(preAuthNeed.Ordinal));
				row.Tag=preAuthNeed;
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
		}

		private string GetPlanText(byte ordinal) {
			switch(ordinal) {
				case 1:
					return Lan.g(this,"Primary");
				case 2:
					return Lan.g(this,"Secondary");
				default:
					return ordinal.ToString();
			}
		}

		private void butCreate_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.OK;
		}

		private void butLater_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.Cancel;
		}
	}
}
