// "Create Preauth Now" from the Chart module's preauthorization reminder (Open Dental 26.1, feature v26_1-lists-carrier-preauth-flags, package
// M2-CARRIER in docs/specs/port-backlog.md). Builds the preauthorization claim the same way the Treatment Plan module's PreAuthorization button
// does (ControlTreat.ToolBarMainPreAuth_Click), for procedures and a plan that are already known, then opens it in Edit Claim. ControlTreat.cs is
// not changed: its toolbar method depends on its grids, so the steps are repeated here.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	public class PreAuthL {
		///<summary>Creates one preauthorization per insurance plan named in the reminder, in plan order (primary first), and opens each in Edit Claim.
		///Returns true when at least one was saved, so the caller refreshes. Stops at the first plan whose checks fail or whose claim is cancelled.</summary>
		public static bool CreatePreAuths(Patient patient,Family family,List<PreAuthNeed> listPreAuthNeeds) {
			if(patient==null || listPreAuthNeeds.IsNullOrEmpty()) {
				return false;
			}
			bool isAnySaved=false;
			List<IGrouping<long,PreAuthNeed>> listGroups=listPreAuthNeeds
				.OrderBy(x => x.Ordinal)
				.GroupBy(x => x.InsSubNum)
				.ToList();
			for(int i=0;i<listGroups.Count;i++) {
				List<long> listProcNums=listGroups[i].Select(x => x.ProcNum).Distinct().ToList();
				PreAuthNeed preAuthNeed=listGroups[i].First();
				if(!CreatePreAuth(patient,family,listProcNums,preAuthNeed.PlanNum,preAuthNeed.InsSubNum)) {
					break;
				}
				isAnySaved=true;
			}
			return isAnySaved;
		}

		///<summary>Creates a preauthorization claim for the procedures and the patient's plan (PlanNum and InsSubNum), with a preauth claimproc for
		///each procedure, then opens Edit Claim as a new claim (cancel deletes it). The checks are the ones of the Treatment Plan module's
		///PreAuthorization button. Returns true when the claim was saved.</summary>
		public static bool CreatePreAuth(Patient patient,Family family,List<long> listProcNums,long planNum,long insSubNum) {
			if(!Security.IsAuthorized(EnumPermType.ClaimView)) {
				return false;
			}
			if(PrefC.GetLong(PrefName.ClearinghouseDefaultDent)==0) {
				MsgBox.Show(Lan.g("ContrTreat","No default dental clearinghouse defined."));
				return false;
			}
			if(PrefC.GetBool(PrefName.ShowFeatureMedicalInsurance) && PrefC.GetLong(PrefName.ClearinghouseDefaultMed)==0) {
				MsgBox.Show(Lan.g("ContrTreat","No default medical clearinghouse defined."));
				return false;
			}
			List<PatPlan> listPatPlans=PatPlans.Refresh(patient.PatNum);
			List<InsSub> listInsSubs=InsSubs.RefreshForFam(family);
			List<InsPlan> listInsPlans=InsPlans.RefreshForSubList(listInsSubs);
			List<Benefit> listBenefits=Benefits.Refresh(listPatPlans,listInsSubs);
			List<ClaimProc> listClaimProcs=ClaimProcs.Refresh(patient.PatNum);
			PatPlan patPlan=listPatPlans.Find(x => x.InsSubNum==insSubNum);
			InsSub insSub=listInsSubs.Find(x => x.InsSubNum==insSubNum);
			InsPlan insPlan=InsPlans.GetPlan(planNum,listInsPlans);
			if(patPlan==null || insSub==null || insPlan==null || insSub.PlanNum!=insPlan.PlanNum) {
				MsgBox.Show(Lan.g("PreAuthL","The insurance plan is no longer attached to the patient."));
				return false;
			}
			List<Procedure> listProcedures=Procedures.GetManyProc(listProcNums,false)
				.FindAll(x => x.ProcStatus.In(ProcStat.TP,ProcStat.TPi) && x.ProcNumLab==0);//Labs are attached through their parent procedure.
			if(listProcedures.Count==0) {
				MsgBox.Show(Lan.g("PreAuthL","The procedures are no longer treatment planned."));
				return false;
			}
			if(listProcedures.Any(x => Procedures.NoBillIns(x,listClaimProcs,insPlan.PlanNum))) {
				MsgBox.Show(Lan.g("ContrTreat","Not allowed to send procedures to insurance that are marked 'Do not bill to ins'."));
				return false;
			}
			if(CultureInfo.CurrentCulture.Name.EndsWith("CA") && listProcedures.Count>7) {//Canada: seven procedures per preauthorization.
				listProcedures=listProcedures.Take(7).ToList();
				MsgBox.Show(Lan.g("ContrTreat","Only the first 7 procedures will be selected.  You will need to create another preauth for the remaining procedures."));
			}
			long procClinicNum=listProcedures[0].ClinicNum;
			PlaceOfService placeOfService=listProcedures[0].PlaceService;
			for(int i=1;i<listProcedures.Count;i++) {
				if(PrefC.HasClinicsEnabled && procClinicNum!=listProcedures[i].ClinicNum) {
					MsgBox.Show(Lan.g("ContrTreat","All procedures do not have the same clinic."));
					return false;
				}
				if(!PrefC.GetBool(PrefName.EasyHidePublicHealth) && listProcedures[i].PlaceService!=placeOfService) {
					MsgBox.Show(Lan.g("ContrTreat","All procedures do not have the same place of service."));
					return false;
				}
			}
			switch(PIn.Enum<ClaimZeroDollarProcBehavior>(PrefC.GetInt(PrefName.ClaimZeroDollarProcBehavior))) {
				case ClaimZeroDollarProcBehavior.Warn:
					if(listProcedures.Any(x => CompareDouble.IsZero(x.ProcFee))
						&& !MsgBox.Show(MsgBoxButtons.OKCancel,Lan.g("ContrTreat","You are about to make a claim that will include a $0 procedure.  Continue?")))
					{
						return false;
					}
					break;
				case ClaimZeroDollarProcBehavior.Block:
					if(listProcedures.Any(x => CompareDouble.IsZero(x.ProcFee))) {
						MsgBox.Show(Lan.g("ContrTreat","You can't make a claim for a $0 procedure."));
						return false;
					}
					break;
			}
			Claim claim=new Claim();
			claim.PatNum=patient.PatNum;
			claim.ClaimNote="";
			claim.ClaimStatus="W";
			claim.DateSent=DateTime.Today;
			claim.DateSentOrig=DateTime.MinValue;
			claim.PlanNum=insPlan.PlanNum;
			claim.InsSubNum=insSub.InsSubNum;
			claim.ClaimForm=insPlan.ClaimFormNum;
			claim.MedType=EnumClaimMedType.Dental;
			if(insPlan.IsMedical) {
				claim.MedType=PrefC.GetBool(PrefName.ClaimMedTypeIsInstWhenInsPlanIsMedical) ? EnumClaimMedType.Institutional : EnumClaimMedType.Medical;
			}
			claim.ProvTreat=0;
			for(int i=0;i<listProcedures.Count;i++) {
				if(claim.ProvTreat==0) {//At least one provider is set.
					claim.ProvTreat=listProcedures[i].ProvNum;
				}
				if(!Providers.GetIsSec(listProcedures[i].ProvNum)) {
					claim.ProvTreat=listProcedures[i].ProvNum;
				}
				if(!claim.IsOrtho && PrefC.GetBool(PrefName.OrthoClaimMarkAsOrtho)) {
					CovCat covCatOrtho=CovCats.GetFirstOrDefault(x => x.EbenefitCat==EbenefitCategory.Orthodontics,true);
					string procCode=ProcedureCodes.GetStringProcCode(listProcedures[i].CodeNum,doThrowIfMissing:false);
					if(covCatOrtho!=null && CovSpans.IsCodeInSpans(procCode,CovSpans.GetWhere(x => x.CovCatNum==covCatOrtho.CovCatNum))) {
						claim.IsOrtho=true;
					}
				}
			}
			claim.ClinicNum=procClinicNum;
			if(Providers.GetIsSec(claim.ProvTreat)) {
				claim.ProvTreat=patient.PriProv;
			}
			claim.ProvBill=Providers.GetBillingProvNum(claim.ProvTreat,claim.ClinicNum);
			Provider provider=Providers.GetProv(claim.ProvTreat);
			if(provider!=null && provider.ProvNumBillingOverride!=0) {
				claim.ProvBill=provider.ProvNumBillingOverride;
			}
			claim.EmployRelated=YN.No;
			claim.ClaimType="PreAuth";
			claim.AttachedFlags="Mail";
			claim.PatRelat=patPlan.Relationship;
			claim.PlaceService=placeOfService;
			claim=Claims.GetClaim(Claims.Insert(claim));
			List<ClaimProc> listClaimProcsNew=new List<ClaimProc>();
			List<long> listCodeNumsNoted=new List<long>();//Codes whose default claim note is already on the claim.
			for(int i=0;i<listProcedures.Count;i++) {
				ClaimProc claimProcExisting=ClaimProcs.GetEstimate(listClaimProcs,listProcedures[i].ProcNum,insPlan.PlanNum,insSub.InsSubNum);
				double insPayEst=0;
				if(claimProcExisting!=null) {
					insPayEst=claimProcExisting.InsPayEst;
				}
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(listProcedures[i].CodeNum);
				ClaimProc claimProc=new ClaimProc();
				ClaimProcs.CreateEst(claimProc,listProcedures[i],insPlan,insSub,0,insPayEst,false,true);//Preauth estimate.
				claimProc.ClaimNum=claim.ClaimNum;
				claimProc.NoBillIns=(claimProcExisting!=null) ? claimProcExisting.NoBillIns : false;
				if(CultureInfo.CurrentCulture.Name.EndsWith("CA") && claimProcExisting!=null) {
					claimProc.Percentage=claimProcExisting.Percentage;
				}
				claimProc.FeeBilled=listProcedures[i].ProcFee;
				if(insPlan.UseAltCode && procedureCode.AlternateCode1!="") {
					claimProc.CodeSent=procedureCode.AlternateCode1;
				}
				else if(insPlan.IsMedical && listProcedures[i].MedicalCode!="") {
					claimProc.CodeSent=listProcedures[i].MedicalCode;
				}
				else {
					claimProc.CodeSent=ProcedureCodes.GetStringProcCode(listProcedures[i].CodeNum);
					if(claimProc.CodeSent.Length>5 && claimProc.CodeSent.Substring(0,1)=="D") {
						claimProc.CodeSent=claimProc.CodeSent.Substring(0,5);
					}
					if(CultureInfo.CurrentCulture.Name.EndsWith("CA") && claimProc.CodeSent.Length>5) {
						claimProc.CodeSent=claimProc.CodeSent.Substring(0,5);
					}
				}
				listClaimProcsNew.Add(claimProc);
				if(!listCodeNumsNoted.Contains(procedureCode.CodeNum)) {
					if(claim.ClaimNote.Length>0 && !string.IsNullOrEmpty(procedureCode.DefaultClaimNote)) {
						claim.ClaimNote+="\n";
					}
					claim.ClaimNote+=procedureCode.DefaultClaimNote;
					listCodeNumsNoted.Add(procedureCode.CodeNum);
				}
			}
			for(int i=0;i<listClaimProcsNew.Count;i++) {
				listClaimProcsNew[i].LineNumber=(byte)(i+1);
				ClaimProcs.Insert(listClaimProcsNew[i]);
			}
			Claims.CalculateAndUpdate(Procedures.Refresh(patient.PatNum),listInsPlans,claim,listPatPlans,listBenefits,patient,listInsSubs);
			using FormClaimEdit formClaimEdit=new FormClaimEdit(claim,patient,family);
			formClaimEdit.IsNew=true;//Cancel deletes the claim.
			formClaimEdit.ShowDialog();
			return formClaimEdit.DialogResult==System.Windows.Forms.DialogResult.OK;
		}
	}
}
