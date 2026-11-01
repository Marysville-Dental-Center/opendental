// The Treatment Plan module's preauthorization flag (Open Dental 26.1, feature v26_1-lists-carrier-preauth-flags, package M2-CARRIER in
// docs/specs/port-backlog.md): a procedure that one of the patient's carriers wants preauthorized (carrier.PreAuthCodes) is marked until a
// preauthorization exists. ControlTreat.cs only gains one call at the end of FillMainDisplay.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class ControlTreat {
		///<summary>Called at the end of FillMainDisplay, before EndUpdate. In a current (Active or Inactive) treatment plan, puts "[PreAuth]" in front of
		///the description of every procedure that still needs a preauthorization, in red (INFERRED presentation: the 26.1 announcement only says the
		///procedure is marked). Uses the Code column when Description is not shown. Saved treatment plans are not marked: they are a record of what was
		///presented. Runs no query unless a carrier has a preauthorization list.</summary>
		private void FillMainDisplayPreAuthFlags() {
			if(PatientCur==null || _listTreatPlans==null || gridPlans.SelectedIndices.Length==0 || gridPlans.SelectedIndices[0]>=_listTreatPlans.Count) {
				return;
			}
			if(_listPatPlans.IsNullOrEmpty() || !CarrierPreAuths.IsInUse()) {
				return;
			}
			if(!_listTreatPlans[gridPlans.SelectedIndices[0]].TPStatus.In(TreatPlanStatus.Active,TreatPlanStatus.Inactive)) {
				return;
			}
			List<long> listProcNums=new List<long>();
			for(int i=0;i<gridMain.ListGridRows.Count;i++) {
				if(gridMain.ListGridRows[i].Tag is ProcTP procTP && procTP.ProcNumOrig>0) {
					listProcNums.Add(procTP.ProcNumOrig);
				}
			}
			if(listProcNums.Count==0) {
				return;
			}
			int idxColumn=GetPreAuthFlagColumn();
			if(idxColumn<0) {
				return;
			}
			List<Procedure> listProcedures=(_listProcedures??new List<Procedure>()).FindAll(x => listProcNums.Contains(x.ProcNum));
			List<long> listProcNumsMissing=listProcNums.FindAll(x => !listProcedures.Any(y => y.ProcNum==x));
			if(listProcNumsMissing.Count>0) {
				listProcedures.AddRange(Procedures.GetManyProc(listProcNumsMissing,false));
			}
			List<PreAuthNeed> listPreAuthNeeds=CarrierPreAuths.GetNeeds(listProcedures,_listPatPlans,_listInsSubs,_listInsPlans,
				CarrierPreAuths.GetClaimProcsForNeeds(listProcNums));
			if(listPreAuthNeeds.Count==0) {
				return;
			}
			string marker=Lan.g(this,"[PreAuth]")+" ";
			for(int i=0;i<gridMain.ListGridRows.Count;i++) {
				if(!(gridMain.ListGridRows[i].Tag is ProcTP procTP) || !listPreAuthNeeds.Any(x => x.ProcNum==procTP.ProcNumOrig)) {
					continue;
				}
				if(idxColumn>=gridMain.ListGridRows[i].Cells.Count) {
					continue;
				}
				GridCell gridCell=gridMain.ListGridRows[i].Cells[idxColumn];
				gridCell.Text=marker+gridCell.Text;
				gridCell.ColorText=Color.Red;
			}
		}

		///<summary>The index of the grid column that carries the flag: Description, or Code when Description is not shown. -1 when neither is.</summary>
		private int GetPreAuthFlagColumn() {
			List<DisplayField> listDisplayFields=DisplayFields.GetForCategory(DisplayFieldCategory.TreatmentPlanModule);
			foreach(string internalName in new[] { "Description","Code" }) {
				DisplayField displayField=listDisplayFields.FirstOrDefault(x => x.InternalName==internalName);
				if(displayField==null) {
					continue;
				}
				//The same heading text FillMainDisplay gives the column (M2-TERM builds the headers with DisplayFields26_1.GetColumnText).
				string heading=DisplayFields26_1.GetColumnText(displayField);
				int idxColumn=gridMain.Columns.GetIndex(heading);
				if(idxColumn>=0) {
					return idxColumn;
				}
			}
			return -1;
		}
	}
}
