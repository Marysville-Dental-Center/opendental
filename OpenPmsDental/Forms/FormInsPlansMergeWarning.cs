// P262-CLAIMS (Open Dental 26.2, release note "When combining insurance plans, there is a new detailed warning for mismatched fields"): lists every
// field of the plans being combined that does not match the plan they are combined into, before anything is changed. The layout and wording are
// this program's own (INFERRED); see docs/port-notes/P262-CLAIMS.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental{
	///<summary>The detailed warning of Combine Insurance Plans. DialogResult OK means the user chose to combine anyway.</summary>
	public partial class FormInsPlansMergeWarning:FormODBase {
		private List<InsPlanMismatch> _listInsPlanMismatches;
		///<summary>How each plan is described in the grid, by PlanNum.</summary>
		private Dictionary<long,string> _dictPlanDescriptions;
		private string _planDescriptionKept;

		///<summary>listInsPlanMismatches: the mismatches of the other plans against the kept one (InsPlanMergeMismatches). dictPlanDescriptions: how
		///each plan is described, by PlanNum.</summary>
		public FormInsPlansMergeWarning(string planDescriptionKept,List<InsPlanMismatch> listInsPlanMismatches,Dictionary<long,string> dictPlanDescriptions){
			InitializeComponent();// Required for Windows Form Designer support
			InitializeLayoutManager();
			_planDescriptionKept=planDescriptionKept;
			_listInsPlanMismatches=listInsPlanMismatches??new List<InsPlanMismatch>();
			_dictPlanDescriptions=dictPlanDescriptions??new Dictionary<long,string>();
			Lan.F(this);
		}

		private void FormInsPlansMergeWarning_Load(object sender,EventArgs e) {
			int countPlans=_listInsPlanMismatches.Select(x => x.PlanNumOther).Distinct().Count();
			labelWarning.Text=Lan.g(this,"Selected plan")+": "+_planDescriptionKept+"\r\n"
				+countPlans+" "+Lan.g(this,"of the other plans have fields that do not match it. When the plans are combined, every plan takes the "
				+"selected plan's values and benefits, so the values in the last column are lost. Combined plans cannot be separated again. Do not "
				+"combine insurance plans from more than one workstation at the same time.");
			FillGrid();
		}

		private void FillGrid() {
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Plan Combined"),300));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Field"),200));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"Selected Plan (kept)"),210));
			gridMain.Columns.Add(new GridColumn(Lan.g(gridMain.TranslationName,"This Plan (lost)"),210){ IsWidthDynamic=true });
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listInsPlanMismatches.Count;i++) {
				InsPlanMismatch insPlanMismatch=_listInsPlanMismatches[i];
				GridRow row=new GridRow();
				if(!_dictPlanDescriptions.TryGetValue(insPlanMismatch.PlanNumOther,out string planDescription)) {
					planDescription=Lan.g(this,"PlanNum")+" "+insPlanMismatch.PlanNumOther;
				}
				row.Cells.Add(planDescription);
				row.Cells.Add(Lan.g(this,insPlanMismatch.Label));
				row.Cells.Add(insPlanMismatch.ValueKept);
				row.Cells.Add(insPlanMismatch.ValueOther);
				row.Tag=insPlanMismatch;
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
		}

		private void butCombine_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.OK;
		}

		private void butCancel_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.Cancel;
		}
	}
}
