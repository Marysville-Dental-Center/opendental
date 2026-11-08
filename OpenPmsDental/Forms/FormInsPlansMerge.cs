using System;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental{
///<summary></summary>
	public partial class FormInsPlansMerge:FormODBase {
		///<summary>After closing this form, if OK, then this will contain the Plan that the others will be merged into.</summary>
		public InsPlan InsPlanToMergeTo;
		///<summary>This list must be set before loading the form.  All of the PlanNums will be 0.</summary>
		public InsPlan[] InsPlanArrayAll;

		///<summary></summary>
		public FormInsPlansMerge(){
			InitializeComponent();// Required for Windows Form Designer support
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormInsPlansMerge_Load(object sender, System.EventArgs e) {
			FillGrid();
		}

		///<summary></summary>
		private void FillGrid(){
			Cursor=Cursors.WaitCursor;
			int indexSelected=0;
			if(gridMain.SelectedIndices.Length>0) {
				indexSelected=gridMain.GetSelectedIndex();
			}
			//ListAll: Set externally before loading.
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col=new GridColumn("Employer",100);
			gridMain.Columns.Add(col);
			col=new GridColumn("Carrier",100);
			gridMain.Columns.Add(col);
			col=new GridColumn("Phone",82);
			gridMain.Columns.Add(col);
			col=new GridColumn("Address",100);
			gridMain.Columns.Add(col);
			col=new GridColumn("City",80);
			gridMain.Columns.Add(col);
			col=new GridColumn("ST",25);
			gridMain.Columns.Add(col);
			col=new GridColumn("Zip",50);
			gridMain.Columns.Add(col);
			col=new GridColumn("Group#",70);
			gridMain.Columns.Add(col);
			col=new GridColumn("Group Name",90);
			gridMain.Columns.Add(col);
			col=new GridColumn("Subs",40);
			gridMain.Columns.Add(col);
			col=new GridColumn("Plan Note",180);
			gridMain.Columns.Add(col);
			//TrojanID and PlanNote not shown
			gridMain.ListGridRows.Clear();
			GridRow row;
			Carrier carrier;
			for(int i=0;i<InsPlanArrayAll.Length;i++) {
				row=new GridRow();
				row.Cells.Add(Employers.GetName(InsPlanArrayAll[i].EmployerNum));
				carrier=Carriers.GetCarrier(InsPlanArrayAll[i].CarrierNum);
				row.Cells.Add(carrier.CarrierName);
				row.Cells.Add(carrier.Phone);
				row.Cells.Add(carrier.Address);
				row.Cells.Add(carrier.City);
				row.Cells.Add(carrier.State);
				row.Cells.Add(carrier.Zip);
				row.Cells.Add(InsPlanArrayAll[i].GroupNum);
				row.Cells.Add(InsPlanArrayAll[i].GroupName);
				row.Cells.Add(InsPlanArrayAll[i].NumberSubscribers.ToString());
				row.Cells.Add(InsPlanArrayAll[i].PlanNote);
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
			gridMain.SetSelected(indexSelected,true);
			Cursor=Cursors.Default;
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e){
			InsPlan insPlan=InsPlanArrayAll[e.Row].Copy();
			using FormInsPlan formInsPlan=new FormInsPlan(insPlan,null,null);
			//FormIP.IsForAll=true;
			//FormIP.IsReadOnly=true;
			formInsPlan.ShowDialog();
			if(formInsPlan.DialogResult==DialogResult.OK) {
				FillGrid();
			}
		}

		private void butOK_Click(object sender, System.EventArgs e) {
			if(gridMain.GetSelectedIndex()==-1) {
				MsgBox.Show(this,"Please select a plan first.");
				return;
			}
			InsPlan insPlanSelected=InsPlanArrayAll[gridMain.GetSelectedIndex()];
			//Open Dental 26.2: a detailed warning that lists every field of the other plans that does not match the selected plan.
			List<InsPlanMismatch> listInsPlanMismatches=GetMismatches(insPlanSelected);
			if(listInsPlanMismatches.Count>0) {
				Dictionary<long,string> dictPlanDescriptions=InsPlanArrayAll.GroupBy(x => x.PlanNum)
					.ToDictionary(x => x.Key,x => InsPlanMergeMismatches.GetPlanDescription(x.First()));
				using FormInsPlansMergeWarning formInsPlansMergeWarning=new FormInsPlansMergeWarning(
					InsPlanMergeMismatches.GetPlanDescription(insPlanSelected),listInsPlanMismatches,dictPlanDescriptions);
				formInsPlansMergeWarning.ShowDialog();
				if(formInsPlansMergeWarning.DialogResult!=DialogResult.OK) {
					return;
				}
			}
			else if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Once insurance plans have been merged, it is not possible to unmerge them. Do not merge insurance plans from multiple copies of the program at the same time. Continue?")) {
				return;
			}
			InsPlanToMergeTo=insPlanSelected.Copy();
			DialogResult=DialogResult.OK;
		}

		///<summary>The fields and benefits of the other plans that do not match the selected plan.</summary>
		private List<InsPlanMismatch> GetMismatches(InsPlan insPlanSelected) {
			List<InsPlan> listInsPlansOther=InsPlanArrayAll.Where(x => x.PlanNum!=insPlanSelected.PlanNum).ToList();
			List<InsPlanMismatch> listInsPlanMismatches=InsPlanMergeMismatches.GetMismatches(insPlanSelected,listInsPlansOther);
			Dictionary<long,List<Benefit>> dictBenefits=InsPlanMergeMismatches.GetBenefitsForPlans(InsPlanArrayAll.Select(x => x.PlanNum).Distinct().ToList());
			listInsPlanMismatches.AddRange(InsPlanMergeMismatches.GetBenefitMismatches(insPlanSelected,listInsPlansOther,dictBenefits));
			//Plan by plan, in the order of the list, each plan's fields in the order of the window and its benefits last.
			List<long> listPlanNumsOrder=listInsPlansOther.Select(x => x.PlanNum).ToList();
			return listInsPlanMismatches.Select((x,i) => new { Mismatch=x,Index=i })
				.OrderBy(x => listPlanNumsOrder.IndexOf(x.Mismatch.PlanNumOther))
				.ThenBy(x => x.Index)
				.Select(x => x.Mismatch)
				.ToList();
		}

	}
}