// Treatment Plan module: the Add Procs toolbar button (Open Dental 25.3, "easier adding of procedures to the active treatment plan", backlog
// package B-TP). Kept in its own partial file so ControlTreat.cs only gets the toolbar button, its click case and its enabled state.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental {
	public partial class ControlTreat {
		///<summary>Add Procs: pick codes (code search, typed code or the Chart's quick buttons), enter teeth and areas, and create them as treatment
		///planned procedures on the Active plan in one step. Only the Active plan can be targeted; a patient with no Active plan gets one.</summary>
		private void ToolBarMainAddProcs_Click() {
			if(PatientCur==null) {
				MsgBox.Show(this,"Please select a patient.");
				return;
			}
			TreatPlan treatPlanTarget=GetAddProcsTarget();
			//With no Active plan at all (no plans, or only Saved ones), the Active plan is created; otherwise it has to be the selected plan.
			if(treatPlanTarget==null && _listTreatPlans!=null && _listTreatPlans.Any(x => TreatPlans.CanAddProcsTo(x))) {
				MsgBox.Show(this,"Select the Active treatment plan first. Procedures can only be added to the Active plan.");
				return;
			}
			//Adding procedures is Chart entry made reachable from here, so it needs the Chart module permission (INFERRED, see docs/port-notes/B-TP.md).
			if(!Security.IsAuthorized(EnumPermType.ChartModule)) {
				return;
			}
			UpdateTPNoteIfNeeded();
			using FormTreatPlanAddProcs formTreatPlanAddProcs=new FormTreatPlanAddProcs(PatientCur,treatPlanTarget);
			formTreatPlanAddProcs.ShowDialog();
			if(formTreatPlanAddProcs.DialogResult!=DialogResult.OK) {
				return;
			}
			ProcAddResult procAddResult;
			Cursor=Cursors.WaitCursor;
			try {
				procAddResult=ProcAddHelper.AddToActivePlan(PatientCur.PatNum,treatPlanTarget?.TreatPlanNum??0,formTreatPlanAddProcs.ListProcAddEntries,
					formTreatPlanAddProcs.PriorityNum,DateTime.Today);
			}
			catch(ODException ex) {
				Cursor=Cursors.Default;
				MessageBox.Show(ex.Message);
				ModuleSelected(PatientCur.PatNum);
				return;
			}
			Cursor=Cursors.Default;
			ApplyAutoCodesToAddedProcs(procAddResult);
			RefreshAfterAddProcs(procAddResult);
		}

		///<summary>The plan selected in the plans grid when it can take new procedures (the Active plan). Null when nothing or another plan is
		///selected.</summary>
		private TreatPlan GetAddProcsTarget() {
			if(_listTreatPlans==null || gridPlans.SelectedIndices.Length==0 || gridPlans.SelectedIndices[0]>=_listTreatPlans.Count) {
				return null;
			}
			TreatPlan treatPlan=_listTreatPlans[gridPlans.SelectedIndices[0]];
			if(!TreatPlans.CanAddProcsTo(treatPlan)) {
				return null;
			}
			return treatPlan;
		}

		///<summary>As Chart entry does right after creating a procedure: when the auto codes recommend another code, offer it. If the office requires
		///auto codes and the user declines, the procedure opens for editing, and cancelling that removes it.</summary>
		private void ApplyAutoCodesToAddedProcs(ProcAddResult procAddResult) {
			PatientData patientData=procAddResult.PatientData;
			for(int i=0;i<procAddResult.ListProcedures.Count;i++) {
				Procedure procedure=procAddResult.ListProcedures[i];
				List<ClaimProc> listClaimProcs=procAddResult.GetClaimProcs(procedure.ProcNum);
				long codeNumRecommended=ProcAddHelper.GetRecommendedCodeNum(procedure,patientData.Patient,listClaimProcs);
				if(codeNumRecommended==procedure.CodeNum) {
					continue;
				}
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(procedure.CodeNum);
				FrmAutoCodeLessIntrusive frmAutoCodeLessIntrusive=new FrmAutoCodeLessIntrusive(patientData.Patient,procedure,procedureCode,codeNumRecommended,
					patientData.ListPatPlans,patientData.ListInsSubs,patientData.ListInsPlans,patientData.ListBenefits,listClaimProcs);
				if(frmAutoCodeLessIntrusive.ShowDialog() || !PrefC.GetBool(PrefName.ProcEditRequireAutoCodes)) {
					continue;
				}
				using FormProcEdit formProcEdit=new FormProcEdit(procedure,patientData.Patient,patientData.Family,listToothInitials:_listToothInitials);
				formProcEdit.ListClaimProcHists=patientData.ListClaimProcHists;
				formProcEdit.ShowDialog();
				if(formProcEdit.DialogResult==DialogResult.OK) {
					continue;
				}
				try {
					Procedures.Delete(procedure.ProcNum,true);//also deletes the claimprocs
				}
				catch(Exception ex) {
					MessageBox.Show(ex.Message);
				}
			}
		}

		///<summary>Refreshes the module (which audits the plans and recomputes the estimates shown), selects the Active plan and the new rows.</summary>
		private void RefreshAfterAddProcs(ProcAddResult procAddResult) {
			ModuleSelected(PatientCur.PatNum);
			int idxPlan=_listTreatPlans.FindIndex(x => x.TreatPlanNum==procAddResult.TreatPlanActive.TreatPlanNum);
			if(idxPlan<0) {
				return;
			}
			gridPlans.SetAll(false);
			gridPlans.SetSelected(idxPlan,true);
			FillMain();
			List<long> listProcNums=procAddResult.ListProcedures.Select(x => x.ProcNum).ToList();
			for(int i=0;i<gridMain.ListGridRows.Count;i++) {
				if(gridMain.ListGridRows[i].Tag is ProcTP procTP && listProcNums.Contains(procTP.ProcNumOrig)) {
					gridMain.SetSelected(i,true);
				}
			}
		}
	}
}
