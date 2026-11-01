// B-ERA: automatic split of ERA claims that carry denial CARCs (Open Dental 25.1, "Auto split ERAs with CARC denial codes listed above"), and the
// per-claim no-auto-process check after a split (25.2). Rules: docs/specs/features-25.1.md (v25_1-manage-era-autosplit-carc),
// docs/specs/features-25.2.md (v25_2-manage-era-carc-logic) and docs/port-notes/B-ERA.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>What automatic ERA processing does with an ERA claim that carries listed CARCs (EraNoAutoProcessCarcCodes).</summary>
	public enum EraAutoSplitDecision {
		///<summary>No listed CARC on the claim: it is processed as usual.</summary>
		NoListedCarcs,
		///<summary>The auto split option is off: the claim is not processed, as in 24.3.</summary>
		OptionOff,
		///<summary>A listed CARC is on a claim-level adjustment: the claim is not split and not processed.</summary>
		ClaimLevelCarc,
		///<summary>Every procedure line carries a listed CARC: the claim is not split and not processed.</summary>
		AllProcsDenied,
		///<summary>A preauthorization, a reversal, or a claim the payer reported in several CLP segments: never split automatically.</summary>
		NotEligible,
		///<summary>A procedure line with a listed CARC still has an insurance payment, so it is not a plain denial: not split.</summary>
		DeniedProcPaid,
		///<summary>Some, not all, procedure lines carry a listed CARC and nothing else blocks: split them off, then process the rest.</summary>
		Split,
	}

	///<summary>The outcome of EraAutoSplit.GetPlan for one ERA claim.</summary>
	public class EraAutoSplitPlan {
		public EraAutoSplitDecision Decision=EraAutoSplitDecision.NoListedCarcs;
		///<summary>Every listed entry found on the claim.</summary>
		public List<EraCarcMatch> ListMatches=new List<EraCarcMatch>();
		///<summary>The procedure lines to move to a new claim. Only filled when Decision is Split.</summary>
		public List<Hx835_Proc> ListProcsToSplit=new List<Hx835_Proc>();

		///<summary>Why the claim was not split, for the automatic processing message. Blank for NoListedCarcs, OptionOff and Split.</summary>
		public string GetSkipReason() {
			switch(Decision) {
				case EraAutoSplitDecision.ClaimLevelCarc:
					return Lans.g("X835","Auto split was not possible: a listed code is on the claim itself, not on a procedure.");
				case EraAutoSplitDecision.AllProcsDenied:
					return Lans.g("X835","Auto split was not possible: every procedure on the claim carries a listed code.");
				case EraAutoSplitDecision.NotEligible:
					return Lans.g("X835","Auto split was not possible: the claim is a preauthorization, a reversal, or was split by the payer.");
				case EraAutoSplitDecision.DeniedProcPaid:
					return Lans.g("X835","Auto split was not possible: a procedure that carries a listed code still has an insurance payment.");
				default:
					return "";
			}
		}
	}

	///<summary>Automatic split of denied procedures during automatic ERA processing. Off by default. When it is on and only some procedure lines of an
	///ERA claim carry a listed CARC, those procedures move to a new, unsent claim with the same steps as the manual Split Claim button
	///(Claims.InsertSplitClaimForEra), and the rest of the claim is processed if it passes every other automatic processing check on its own.</summary>
	public static class EraAutoSplit {
		///<summary>Fork-owned preference row, not in the PrefName enum: Open Dental's own name for this 25.1 preference is not published (see
		///docs/port-notes/B-ERA.md). The row is written the first time the office saves the setting; a missing row reads as off.</summary>
		public const string PREF_NAME="ForkEraAutoSplitCarcDenials";

		///<summary>True when the office turned on "Auto split ERAs with CARC denial codes listed above". Reads the preference cache. Never throws.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			try {
				if(Prefs.DictIsNull() || !Prefs.GetContainsKey(PREF_NAME)) {
					return false;
				}
				return PIn.Bool(Prefs.GetOne(PREF_NAME).ValueString);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>Stores the setting, inserting the row if needed, and updates this workstation's cache. Returns true if it changed. The caller
		///signals InvalidType.Prefs so other workstations reload.</summary>
		public static bool SetEnabled(bool isEnabled) {
			Meth.NoCheckMiddleTierRole();
			if(IsEnabled()==isEnabled) {
				return false;//A missing row already reads as off.
			}
			string valueString=POut.Bool(isEnabled);
			UpsertPref(valueString);
			Prefs.UpdateValueForKey(new Pref { PrefName=PREF_NAME,ValueString=valueString });
			return true;
		}

		///<summary>Writes the preference row straight to the table, inserting it when it is missing.</summary>
		public static void UpsertPref(string valueString) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),valueString);
				return;
			}
			string count=Db.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(PREF_NAME)+"'");
			if(PIn.Long(count)==0) {
				Db.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('"+POut.String(PREF_NAME)+"','"+POut.String(valueString)+"')");
				return;
			}
			Db.NonQ("UPDATE preference SET ValueString='"+POut.String(valueString)+"' WHERE PrefName='"+POut.String(PREF_NAME)+"'");
		}

		///<summary>Decides from the ERA alone what automatic processing does with a claim that carries listed CARCs. The checks that need the
		///database (claim already received, claimprocs that match) are made by SplitDeniedProcsIfNeeded.</summary>
		public static EraAutoSplitPlan GetPlan(Hx835_Claim claimPaid,List<EraCarcRule> listRules,bool isOptionOn) {
			Meth.NoCheckMiddleTierRole();
			EraAutoSplitPlan plan=new EraAutoSplitPlan();
			plan.ListMatches=EraCarcRules.GetMatches(listRules,claimPaid);
			if(plan.ListMatches.Count==0) {
				plan.Decision=EraAutoSplitDecision.NoListedCarcs;
				return plan;
			}
			if(!isOptionOn) {
				plan.Decision=EraAutoSplitDecision.OptionOff;
				return plan;
			}
			if(plan.ListMatches.Any(x => x.IsClaimLevel)) {
				plan.Decision=EraAutoSplitDecision.ClaimLevelCarc;
				return plan;
			}
			if(claimPaid.IsPreauth || claimPaid.IsReversal || claimPaid.IsSplitClaim) {
				plan.Decision=EraAutoSplitDecision.NotEligible;
				return plan;
			}
			List<Hx835_Proc> listProcsDenied=claimPaid.ListProcs.FindAll(x => plan.ListMatches.Any(y => y.Proc==x));
			if(listProcsDenied.Count>=claimPaid.ListProcs.Count) {
				plan.Decision=EraAutoSplitDecision.AllProcsDenied;
				return plan;
			}
			if(listProcsDenied.Any(x => x.InsPaid!=0)) {
				plan.Decision=EraAutoSplitDecision.DeniedProcPaid;
				return plan;
			}
			plan.Decision=EraAutoSplitDecision.Split;
			plan.ListProcsToSplit=listProcsDenied;
			return plan;
		}

		///<summary>A copy of the ERA claim without the given procedure lines, with their amounts taken out of the claim totals (charge, payment,
		///patient responsibility, deductible, write-off, allowed). Automatic processing posts this copy to the claim that keeps the other procedures,
		///so the given lines are not looked for on it.</summary>
		public static Hx835_Claim GetRemainingClaim(Hx835_Claim claimPaid,List<Hx835_Proc> listProcsToRemove) {
			Meth.NoCheckMiddleTierRole();
			Hx835_Claim claimRemaining=claimPaid.Copy();
			claimRemaining.ListProcs=new List<Hx835_Proc>();
			for(int i=0;i<claimPaid.ListProcs.Count;i++) {
				Hx835_Proc proc=claimPaid.ListProcs[i];
				if(!listProcsToRemove.Contains(proc)) {
					Hx835_Proc procCopy=proc.Copy();
					procCopy.ClaimPaid=claimRemaining;
					claimRemaining.ListProcs.Add(procCopy);
					continue;
				}
				claimRemaining.ClaimFee-=proc.ProcFee;
				claimRemaining.InsPaid-=proc.InsPaid;
				claimRemaining.PreAuthInsEst-=proc.PreAuthInsEst;
				claimRemaining.PatientRespAmt-=proc.PatRespTotal;
				claimRemaining.PatientPortionAmt-=proc.PatientPortionAmt;
				claimRemaining.PatientDeductAmt-=proc.DeductibleAmt;
				claimRemaining.WriteoffAmt-=proc.WriteoffAmt;
				claimRemaining.AllowedAmt-=proc.AllowedAmt;
			}
			return claimRemaining;
		}

		///<summary>The not-received procedure claimprocs of the claim for the given ERA procedure lines, one distinct claimproc per line, matched by
		///ProcNum first and then by code sent and fee billed (as Hx835_Claim.GetPaymentsForClaimProcs matches). Null when any line has no match.</summary>
		public static List<ClaimProc> MatchClaimProcs(List<Hx835_Proc> listProcs,List<ClaimProc> listClaimProcs) {
			Meth.NoCheckMiddleTierRole();
			List<ClaimProc> listCandidates=listClaimProcs.FindAll(x => x.ProcNum!=0 && x.Status==ClaimProcStatus.NotReceived);
			List<ClaimProc> listMatched=new List<ClaimProc>();
			for(int i=0;i<listProcs.Count;i++) {
				Hx835_Proc proc=listProcs[i];
				ClaimProc claimProc=null;
				if(proc.ProcNum!=0) {
					claimProc=listCandidates.FirstOrDefault(x => x.ProcNum==proc.ProcNum && !listMatched.Contains(x));
				}
				if(claimProc==null) {
					claimProc=listCandidates.FirstOrDefault(x => !listMatched.Contains(x) && x.CodeSent==proc.ProcCodeBilled && (decimal)x.FeeBilled==proc.ProcFee);
				}
				if(claimProc==null) {
					return null;
				}
				listMatched.Add(claimProc);
			}
			return listMatched;
		}

		///<summary>True when every procedure line of the ERA claim finds a claimproc and every procedure claimproc finds a line, which is what automatic
		///processing needs to post an initial payment (Etranss.TryImportEraClaimData).</summary>
		public static bool DoAllProcsMatch(Hx835_Claim claimPaid,List<ClaimProc> listClaimProcs) {
			Meth.NoCheckMiddleTierRole();
			List<ClaimProc> listClaimProcsProcs=listClaimProcs.FindAll(x => x.ProcNum!=0 && x.Status==ClaimProcStatus.NotReceived).Select(x => x.Copy()).ToList();
			if(listClaimProcsProcs.Count==0) {
				return false;
			}
			List<Hx835_Proc> listProcsUnassigned=new List<Hx835_Proc>(claimPaid.ListProcs);
			List<List<Hx835_Proc>> listProcsForClaimProcs=Hx835_Claim.GetPaymentsForClaimProcs(listClaimProcsProcs,listProcsUnassigned);
			return listProcsUnassigned.Count==0 && listProcsForClaimProcs.All(x => x.Count>0);
		}

		///<summary>True when automatic payment entry (Etranss.TryImportEraClaimData) would need a By Total row to post this ERA claim, and so would refuse
		///it. Repeats the By Total arithmetic of TryImportEraClaimData for an original payment on which every line matches a procedure (check that first
		///with DoAllProcsMatch): the claim's deductible, allowed amount and insurance payment, less the sums of its lines, must all round to zero, and so
		///must the write-off when canWriteOff (CanAutoPostWriteOff) is true; a negative write-off difference counts as zero, as there. An adjustment on
		///the claim itself that is not listed, such as OA-23, makes the claim payment differ from the sum of the lines and needs a By Total row.
		///Not for supplemental payments, reversals or claims the payer split (the auto split never handles those).</summary>
		public static bool WouldNeedByTotalPayment(Hx835_Claim claimPaid,bool canWriteOff) {
			Meth.NoCheckMiddleTierRole();
			decimal dedApplied=claimPaid.PatientDeductAmt-claimPaid.ListProcs.Sum(x => x.DeductibleAmt);
			decimal allowed=claimPaid.AllowedAmt-claimPaid.ListProcs.Sum(x => x.AllowedAmt);
			decimal insPaid=claimPaid.InsPaid-claimPaid.ListProcs.Sum(x => x.InsPaid);
			decimal writeOff=0;
			if(canWriteOff) {
				writeOff=Math.Max(0m,claimPaid.WriteoffAmt-claimPaid.ListProcs.Sum(x => x.WriteoffAmt));
			}
			return new List<decimal> { dedApplied,allowed,insPaid,writeOff }.Any(x => Math.Round(x,2,MidpointRounding.AwayFromZero)!=0);
		}

		///<summary>True when automatic payment entry posts the ERA's write-offs for this claim (preference EraAutoPostWriteOff), as decided in
		///Etranss.TryImportEraClaimData for an original (not supplemental) payment. Reads the preference cache.</summary>
		public static bool CanAutoPostWriteOff(Hx835_Claim claimPaid,Claim claim) {
			Meth.NoCheckMiddleTierRole();
			EnumEraAutoPostWriteOff eraAutoPostWriteOff=PrefC.GetEnum<EnumEraAutoPostWriteOff>(PrefName.EraAutoPostWriteOff);
			if(eraAutoPostWriteOff==EnumEraAutoPostWriteOff.Always) {
				return true;
			}
			if(eraAutoPostWriteOff==EnumEraAutoPostWriteOff.PriFromERA) {
				return claimPaid.CodeClp02.In("1","19");//"Processed as Primary", "Processed as Primary, Forwarded to Additional Payer(s)"
			}
			if(eraAutoPostWriteOff==EnumEraAutoPostWriteOff.PriFromPlan) {
				return claim.ClaimType=="P";
			}
			return false;
		}

		///<summary>Why a claim with a split plan can't be split, from its database state. Blank when it can be.</summary>
		public static string GetClaimSkipReason(Claim claim,List<ClaimProc> listClaimProcsForClaim) {
			Meth.NoCheckMiddleTierRole();
			if(claim.ClaimType=="PreAuth" || claim.ClaimType=="Cap") {
				return Lans.g("X835","Auto split was not possible: the claim is a preauthorization or capitation claim.");
			}
			if(claim.ClaimStatus=="R" || listClaimProcsForClaim.Any(x => x.ProcNum!=0 && x.Status!=ClaimProcStatus.NotReceived)) {
				return Lans.g("X835","Auto split was not possible: the claim already has received procedures.");
			}
			return "";
		}

		///<summary>Hook for Etranss.TryAutoProcessEraEob, called for each matched ERA claim just before CanClaimBeAutoProcessed. When the auto split
		///option is on and the claim qualifies, moves the procedures that carry listed CARCs to a new unsent claim and returns a copy of the ERA claim
		///without those lines; automatic processing then checks and posts that copy to the remaining claim. Otherwise returns claimPaid unchanged and
		///changes nothing, and CanClaimBeAutoProcessed reports the listed CARCs as in 24.3 (with the reason the claim was not split).
		///A split happens only when the remaining claim passes every other automatic processing check, every remaining line matches a procedure and
		///its payment needs no By Total row (WouldNeedByTotalPayment), so a split claim is always followed by its payment. Updates the given lists in place: listClaimProcsForClaim loses the moved claimprocs,
		///and listClaimProcsAll gets their new ClaimNum (so payment finalization leaves them out). Records the moved procedures in
		///automationResult.DictSplitClaimNumsByProcNum (see KeepNextInsuranceClaimsOnHold). Adds a line to automationResult.ListAutoSplitNotes,
		///puts the same line at the top of the etrans note, and writes a ClaimEdit security log entry for the original claim.</summary>
		public static Hx835_Claim SplitDeniedProcsIfNeeded(bool isFullyAutomatic,X835 x835,Hx835_Claim claimPaid,Claim claim,Patient patient,
			InsPlan insPlan,List<PayPlan> listPayPlans,List<ClaimProc> listClaimProcsAll,List<Hx835_ShortClaimProc> listShortClaimProcsAll,
			List<ClaimProc> listClaimProcsForClaim,List<Etrans835Attach> listAttaches,EraAutomationResult automationResult)
		{
			Meth.NoCheckMiddleTierRole();
			if(x835?.EtransSource==null || claimPaid==null || claim==null || patient==null || insPlan==null || automationResult==null || !IsEnabled()) {
				return claimPaid;
			}
			EraAutoSplitPlan plan=GetPlan(claimPaid,EraCarcRules.GetRulesFromPref(),isOptionOn:true);
			if(plan.Decision!=EraAutoSplitDecision.Split) {
				return claimPaid;//CanClaimBeAutoProcessed explains why, from the same plan.
			}
			string skipReason=GetClaimSkipReason(claim,listClaimProcsForClaim);
			if(skipReason!="") {
				automationResult.SetAutoSplitSkipReason(claimPaid,skipReason);
				return claimPaid;
			}
			List<ClaimProc> listClaimProcsToSplit=MatchClaimProcs(plan.ListProcsToSplit,listClaimProcsForClaim);
			if(listClaimProcsToSplit==null) {
				automationResult.SetAutoSplitSkipReason(claimPaid,
					Lans.g("X835","Auto split was not possible: a procedure that carries a listed code could not be matched to a procedure on the claim."));
				return claimPaid;
			}
			List<ClaimProc> listClaimProcsRemaining=listClaimProcsForClaim.FindAll(x => !listClaimProcsToSplit.Contains(x));
			Hx835_Claim claimPaidRemaining=GetRemainingClaim(claimPaid,plan.ListProcsToSplit);
			if(!DoAllProcsMatch(claimPaidRemaining,listClaimProcsRemaining)) {
				automationResult.SetAutoSplitSkipReason(claimPaid,
					Lans.g("X835","Auto split was not possible: the procedures left on the claim do not all match the ERA."));
				return claimPaid;
			}
			//Automatic payment entry refuses a claim that would need a By Total row (TryImportEraClaimData), for example when an adjustment on the claim
			//itself that is not listed makes the claim payment differ from the sum of the lines. Check it here, so that a split is never left without
			//its payment.
			if(WouldNeedByTotalPayment(claimPaidRemaining,CanAutoPostWriteOff(claimPaidRemaining,claim))) {
				automationResult.SetAutoSplitSkipReason(claimPaid,
					Lans.g("X835","Auto split was not possible: the rest of the claim would need a By Total payment."));
				return claimPaid;
			}
			//The remaining claim must pass the other automatic processing checks on its own, or nothing is split. A scratch result keeps those checks'
			//messages out of this result; when they fail, CanClaimBeAutoProcessed reports them for the whole claim right after this.
			List<Hx835_ShortClaimProc> listShortClaimProcsRemaining=listClaimProcsRemaining.Select(x => new Hx835_ShortClaimProc(x)).ToList();
			if(!new EraAutomationResult().CanClaimBeAutoProcessed(isFullyAutomatic,patient,insPlan,claimPaidRemaining,listPayPlans,listShortClaimProcsAll,
				listShortClaimProcsRemaining,listAttaches))
			{
				return claimPaid;
			}
			//Point of no return. InsertSplitClaimForEra changes the database right away, like the manual Split Claim button.
			long claimNumOriginal=claim.ClaimNum;
			List<ClaimProc> listClaimProcsBefore=new List<ClaimProc>(listClaimProcsForClaim);
			Claim claimSplit=Claims.InsertSplitClaimForEra(claim,listClaimProcsToSplit,listClaimProcsBefore,x835.EtransSource.EtransNum,claimPaid.ClpSegmentIndex,
				isUnsent:true,out _,patient);
			List<long> listClaimProcNumsSplit=listClaimProcsToSplit.Select(x => x.ClaimProcNum).ToList();
			for(int i=0;i<listClaimProcsToSplit.Count;i++) {
				automationResult.DictSplitClaimNumsByProcNum[listClaimProcsToSplit[i].ProcNum]=claimSplit.ClaimNum;//KeepNextInsuranceClaimsOnHold
			}
			listClaimProcsForClaim.RemoveAll(x => listClaimProcNumsSplit.Contains(x.ClaimProcNum));
			for(int i=0;i<listClaimProcsAll.Count;i++) {
				if(!listClaimProcNumsSplit.Contains(listClaimProcsAll[i].ClaimProcNum)) {
					continue;
				}
				listClaimProcsAll[i].ClaimNum=claimSplit.ClaimNum;
				listClaimProcsAll[i].PayPlanNum=0;
			}
			//The split attach is not added to listAttaches: the rest of this automatic run works on the remaining claim only, and the ERA window reloads
			//the attaches from the database.
			string procCodes=string.Join(", ",plan.ListProcsToSplit.Select(x => x.ProcCodeBilled));
			string matches=EraCarcRules.GetDescription(plan.ListMatches);
			string note=Lans.g("X835","Auto split:")+" "+patient.GetNameFL()+", "+Lans.g("X835","claim")+" #"+claimNumOriginal+": "+procCodes+" "
				+Lans.g("X835","moved to new unsent claim")+" #"+claimSplit.ClaimNum+" ("+matches+").";
			automationResult.ListAutoSplitNotes.Add(note);
			string noteOld=x835.EtransSource.Note??"";
			x835.EtransSource.Note=note+(noteOld.Trim()=="" ? "" : "\r\n"+noteOld);
			SecurityLogs.MakeLogEntry(EnumPermType.ClaimEdit,claim.PatNum,
				Lans.g("X835","Claim split automatically during ERA processing. Procedures")+" "+procCodes+" "+Lans.g("X835","moved to new claim")+" "
				+claimSplit.ClaimNum+" "+Lans.g("X835","(status Unsent) because of")+" "+matches+".",
				claimNumOriginal,LogSources.None,DateTime.MinValue);
			return claimPaidRemaining;
		}

		///<summary>Combined review (2026-11-01). Hook for Etranss.TryAutoProcessEraEob, called with the claims of the next insurance that automatic
		///processing is about to set to Waiting to Send after the claims it received. Takes out of the list every claim that holds a procedure an auto
		///split moved to a new unsent claim during this run (automationResult.DictSplitClaimNumsByProcNum): the prior insurance has not adjudicated that
		///procedure, so the claim would go out with a line the prior payer never paid or denied. Such a claim is left as it is (normally Hold Until Prior
		///Received), with a line in automationResult.ListAutoSplitNotes and on top of the etrans note. Its estimates were already recalculated when the
		///remaining claim was received (Etranss.ReceiveEraPayment), and the usual next-insurance lookup releases it once the split claim is received.
		///</summary>
		public static void KeepNextInsuranceClaimsOnHold(X835 x835,List<Claim> listClaimsNext,List<Patient> listPatients,EraAutomationResult automationResult) {
			Meth.NoCheckMiddleTierRole();
			if(listClaimsNext.IsNullOrEmpty() || automationResult==null || automationResult.DictSplitClaimNumsByProcNum.Count==0) {
				return;
			}
			List<ClaimProc> listClaimProcsNext=ClaimProcs.RefreshForClaims(listClaimsNext.Select(x => x.ClaimNum).ToList());
			List<Claim> listClaimsHeld=new List<Claim>();
			for(int i=0;i<listClaimsNext.Count;i++) {
				Claim claim=listClaimsNext[i];
				List<long> listClaimNumsSplit=listClaimProcsNext
					.FindAll(x => x.ClaimNum==claim.ClaimNum && automationResult.DictSplitClaimNumsByProcNum.ContainsKey(x.ProcNum))
					.Select(x => automationResult.DictSplitClaimNumsByProcNum[x.ProcNum]).Distinct().OrderBy(x => x).ToList();
				if(listClaimNumsSplit.Count==0) {
					continue;
				}
				listClaimsHeld.Add(claim);
				string patientName=listPatients?.FirstOrDefault(x => x.PatNum==claim.PatNum)?.GetNameFL()??"";
				string note=Lans.g("X835","Auto split:")+" "+(patientName=="" ? "" : patientName+", ")+Lans.g("X835","claim")+" #"+claim.ClaimNum+" "
					+Lans.g("X835","of the next insurance left on hold: procedures moved to claim")+" #"+string.Join(", #",listClaimNumsSplit)+".";
				automationResult.ListAutoSplitNotes.Add(note);
				if(x835?.EtransSource!=null) {
					string noteOld=x835.EtransSource.Note??"";
					x835.EtransSource.Note=note+(noteOld.Trim()=="" ? "" : "\r\n"+noteOld);
				}
			}
			listClaimsNext.RemoveAll(x => listClaimsHeld.Contains(x));
		}
	}

	///<summary>B-ERA additions to the automatic processing result: notes for the claims that were split, and why a claim with listed CARCs was not.</summary>
	public partial class EraAutomationResult {
		///<summary>One line per claim that automatic processing split (EraAutoSplit), naming the original and the new ClaimNum.</summary>
		public List<string> ListAutoSplitNotes=new List<string>();
		///<summary>Combined review (2026-11-01): the procedures EraAutoSplit moved to a new unsent claim during this run, keyed by ProcNum, with the
		///ClaimNum of that claim. The claims of the next insurance that hold one of them stay on hold (EraAutoSplit.KeepNextInsuranceClaimsOnHold).
		///</summary>
		public Dictionary<long,long> DictSplitClaimNumsByProcNum=new Dictionary<long,long>();
		///<summary>Why EraAutoSplit did not split a claim, when the reason comes from the database (keyed by the ERA claim object).</summary>
		private Dictionary<Hx835_Claim,string> _dictAutoSplitSkipReasons=new Dictionary<Hx835_Claim,string>();

		public void SetAutoSplitSkipReason(Hx835_Claim claimPaid,string reason) {
			_dictAutoSplitSkipReasons[claimPaid]=reason;
		}

		///<summary>Why the claim was not split automatically, for the message that reports its listed CARCs. Blank when the option is off.</summary>
		public string GetAutoSplitSkipReason(Hx835_Claim claimPaid) {
			if(_dictAutoSplitSkipReasons.TryGetValue(claimPaid,out string reason)) {
				return reason;
			}
			if(!EraAutoSplit.IsEnabled()) {
				return "";
			}
			return EraAutoSplit.GetPlan(claimPaid,EraCarcRules.GetRulesFromPref(),isOptionOn:true).GetSkipReason();
		}

		///<summary>Adds the auto split notes of the results to the automatic processing message.</summary>
		public static void AppendAutoSplitNotes(StringBuilder stringBuilder,List<EraAutomationResult> listAutomationResults) {
			List<string> listNotes=listAutomationResults.SelectMany(x => x.ListAutoSplitNotes).ToList();
			if(listNotes.Count==0) {
				return;
			}
			stringBuilder.AppendLine(Lans.g("X835","Claims Split Automatically:")+$" {listNotes.Count}");
			for(int i=0;i<listNotes.Count;i++) {
				stringBuilder.AppendLine(listNotes[i]);
			}
		}

		///<summary>CreateEtransNote, with the auto split notes on top of the existing note.</summary>
		public static string CreateEtransNote(X835Status status,string note,int countProcessedClaimsWithNameMismatches,List<string> listAutoSplitNotes) {
			string noteWithSplits=note??"";
			if(listAutoSplitNotes!=null && listAutoSplitNotes.Count>0) {
				string notes=string.Join("\r\n",listAutoSplitNotes);
				//The automatic run may already have put the same lines on top of the etrans note.
				if(!noteWithSplits.StartsWith(notes)) {
					noteWithSplits=notes+(noteWithSplits.Trim()=="" ? "" : "\r\n"+noteWithSplits);
				}
			}
			return CreateEtransNote(status,noteWithSplits,countProcessedClaimsWithNameMismatches);
		}
	}
}
