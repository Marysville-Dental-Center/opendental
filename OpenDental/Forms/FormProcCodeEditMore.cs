using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenDentBusiness;
using OpenDental.UI;
using System.Linq;

namespace OpenDental {
	public partial class FormProcCodeEditMore:FormODBase {
		private List<Fee> _listFees;
		private ProcedureCode _procedureCode;
		private List<FeeSched> _listFeeScheds;
		private bool _isFeeChanged=false;

		public FormProcCodeEditMore(ProcedureCode procedureCode) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_procedureCode=procedureCode;
		}

		private void FormProcCodeEditMore_Load(object sender,EventArgs e) {
			_listFeeScheds=FeeScheds.GetDeepCopy(true);//js not sure why this is being used at all.  Looks like it's supposed to show all fee scheds.
			FillAndSortListFees();
			FillComboFilters();
			FillGrid();
		}

		///<summary></summary>
		private void FillAndSortListFees() {
			List<long> listClinicNums=Clinics.GetForUserod(Security.CurUser,true).Select(x => x.ClinicNum).ToList();
			_listFees=Fees.GetFeesForCode(_procedureCode.CodeNum,listClinicNums);//already sorted
		}

		///<summary>Provider and Clinic filters (Open Dental 25.1). Both start at All, which is the full list. The clinic picker lists only the clinics this
		///user may access and hides itself when clinics are off.</summary>
		private void FillComboFilters() {
			comboProvider.IncludeAll=true;
			comboProvider.Items.Clear();
			comboProvider.Items.AddProvNone(Lan.g(this,"Default (none)"));
			comboProvider.Items.AddProvsAbbr(MoreFeesFilters.GetProvidersForFilter(Providers.GetDeepCopy(),_listFees));
			comboProvider.IsAllSelected=true;
			comboClinic.IsAllSelected=true;
		}

		///<summary>MoreFeesFilters.ALL, 0 for fees with no provider, or the ProvNum.</summary>
		private long GetProvNumFilter() {
			if(comboProvider.IsAllSelected) {
				return MoreFeesFilters.ALL;
			}
			return comboProvider.GetSelectedProvNum();
		}

		///<summary>MoreFeesFilters.ALL, 0 for the default (HQ) fees, or the ClinicNum. Always ALL when clinics are off.</summary>
		private long GetClinicNumFilter() {
			if(!PrefC.HasClinicsEnabled || comboClinic.IsAllSelected || comboClinic.IsNothingSelected) {
				return MoreFeesFilters.ALL;
			}
			if(comboClinic.IsUnassignedSelected) {
				return 0;
			}
			return comboClinic.ClinicNumSelected;
		}

		private void comboFilter_SelectionChangeCommitted(object sender,EventArgs e) {
			FillGrid();
		}

		private void FillGrid() {
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col;
			if(!PrefC.HasClinicsEnabled) {
				col=new GridColumn(Lan.g("TableProcCodeEditMore","Schedule"),200);
				gridMain.Columns.Add(col);
				col=new GridColumn(Lan.g("TableProcCodeEditMore","Provider"),135);
				gridMain.Columns.Add(col);
			}
			else {//Using clinics.
				col=new GridColumn(Lan.g("TableProcCodeEditMore","Schedule"),130);
				gridMain.Columns.Add(col);
				col=new GridColumn(Lan.g("TableProcCodeEditMore","Clinic"),130);
				gridMain.Columns.Add(col);
				col=new GridColumn(Lan.g("TableProcCodeEditMore","Provider"),75);
				gridMain.Columns.Add(col);
			}
			col=new GridColumn(Lan.g("TableProcCodeEditMore","Amount"),100,HorizontalAlignment.Right);
			gridMain.Columns.Add(col);
			//The fee in effect today (24.4 effective dates) can have started on a later date than the schedule's first fee. Blank means always.
			col=new GridColumn(Lan.g("TableProcCodeEditMore","Effective"),75,HorizontalAlignment.Center);
			gridMain.Columns.Add(col);
			gridMain.ListGridRows.Clear();
			GridRow row;
			long lastFeeSched=0;
			long provNumFilter=GetProvNumFilter();
			long clinicNumFilter=GetClinicNumFilter();
			bool isDefaultFeeShown=MoreFeesFilters.IsDefaultFeeShown(provNumFilter,clinicNumFilter);
			List<Fee> listFeesShowing=MoreFeesFilters.Filter(_listFees,provNumFilter,clinicNumFilter);
			for(int i=0;i<listFeesShowing.Count;i++) {
				row=new GridRow();
				if(listFeesShowing[i].FeeSched!=lastFeeSched) {
					row.Cells.Add(FeeScheds.GetDescription(listFeesShowing[i].FeeSched));
					row.Bold=true;
					lastFeeSched=listFeesShowing[i].FeeSched;
					row.ColorBackG=Color.LightBlue;
					//FeeSched change, but not with a default fee. Insert placeholder row, unless the filters leave default fees out.
					if(isDefaultFeeShown && (listFeesShowing[i].ClinicNum!=0 || listFeesShowing[i].ProvNum!=0)) {
						if(PrefC.HasClinicsEnabled) {
							row.Cells.Add("");
						}
						row.Cells.Add("");
						row.Cells.Add("");
						row.Cells.Add("");
						Fee fee=new Fee();
						fee.FeeSched=listFeesShowing[i].FeeSched;
						row.Tag=fee;
						gridMain.ListGridRows.Add(row);
						//Now that we have a placeholder for the default fee (none was found), go about adding the next row (non-default fee).
						row=new GridRow();
						row.Cells.Add("");
					}
				}
				else {
					row.Cells.Add("");
				}
				row.Tag=listFeesShowing[i];
				if(PrefC.HasClinicsEnabled) { //Using clinics
					row.Cells.Add(Clinics.GetAbbr(listFeesShowing[i].ClinicNum)); //Returns "" if invalid clinicnum (ie. 0)
				}
				row.Cells.Add(Providers.GetAbbr(listFeesShowing[i].ProvNum)); //Returns "" if invalid provnum (ie. 0)
				row.Cells.Add(listFeesShowing[i].Amount.ToString("n"));
				row.Cells.Add(listFeesShowing[i].DateEffective.Year<1880 ? "" : listFeesShowing[i].DateEffective.ToShortDateString());
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
		}

		private void gridMain_CellDoubleClick(object sender,UI.ODGridClickEventArgs e) {
			Fee fee=(Fee)gridMain.ListGridRows[e.Row].Tag;
			using FormFeeEdit formFeeEdit=new FormFeeEdit();
			if(fee.FeeNum==0) {
				formFeeEdit.IsNew=true;
				fee.CodeNum=_procedureCode.CodeNum;
				Fees.Insert(fee);//Pre-insert the fee before opening the edit window.
			}
			formFeeEdit.FeeCur=fee;
			formFeeEdit.ShowDialog();
			if(formFeeEdit.DialogResult==DialogResult.OK) {
				//FormFE could have manipulated the fee.  Refresh our local cache and grids to reflect the changes.
				FillAndSortListFees();
				FillGrid();
				_isFeeChanged=true;
			}
		}

	}
}