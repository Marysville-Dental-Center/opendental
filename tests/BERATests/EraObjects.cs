// In-memory ERA claims for the tests that need no database. Synthetic values only (made-up Z codes and names).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenDentBusiness;

namespace BERATests {
	public static class EraObjects {
		public static Hx835_Adj EraAdj(string groupCode,string reasonCode,decimal amount) {
			return new Hx835_Adj { AdjCode=groupCode,ReasonCode=reasonCode,AdjAmt=amount,AdjustRemarks="",ReasonDescript="",IsDeductible=(groupCode=="PR" && reasonCode=="1") };
		}

		///<summary>A procedure line with its amounts worked out from its adjustments the way X835.ProcessSVC does.</summary>
		public static Hx835_Proc EraProc(string code,decimal fee,decimal insPaid,long procNum,params Hx835_Adj[] arrayAdjs) {
			Hx835_Proc proc=new Hx835_Proc();
			proc.ProcCodeBilled=code;
			proc.ProcCodeAdjudicated=code;
			proc.ProcFee=fee;
			proc.InsPaid=insPaid;
			proc.ProcNum=procNum;
			proc.ListProcAdjustments=arrayAdjs.ToList();
			proc.ListSupplementalInfo=new List<Hx835_Info>();
			proc.ListRemarks=new List<Hx835_Remark>();
			proc.DateServiceStart=new DateTime(2026,9,1);
			proc.DateServiceEnd=new DateTime(2026,9,1);
			for(int i=0;i<arrayAdjs.Length;i++) {
				if(arrayAdjs[i].AdjCode=="CO") {
					proc.WriteoffAmt+=arrayAdjs[i].AdjAmt;
				}
				else if(arrayAdjs[i].AdjCode=="PR") {
					proc.PatRespTotal+=arrayAdjs[i].AdjAmt;
					if(arrayAdjs[i].IsDeductible) {
						proc.DeductibleAmt+=arrayAdjs[i].AdjAmt;
					}
					else {
						proc.PatientPortionAmt+=arrayAdjs[i].AdjAmt;
					}
				}
			}
			proc.AllowedAmt=proc.InsPaid+proc.PreAuthInsEst+proc.PatRespTotal;
			return proc;
		}

		///<summary>An ERA claim (CLP) with the given procedure lines; the claim totals are the sums of the lines, as a payer reports them.</summary>
		public static Hx835_Claim EraClaim(params Hx835_Proc[] arrayProcs) {
			Hx835_Claim claim=new Hx835_Claim();
			claim.ClaimTrackingNumber="SYN1001";
			claim.PayerControlNumber="PCN77";
			claim.CodeClp02="1";
			claim.StatusCodeDescript="Processed as Primary";
			claim.ListClaimAdjustments=new List<Hx835_Adj>();
			claim.ListAdjudicationInfo=new List<Hx835_Info>();
			claim.ListSupplementalInfo=new List<Hx835_Info>();
			claim.ListProcs=arrayProcs.ToList();
			claim.PatientName=new Hx835_Name { Fname="Synthetic",Mname="",Lname="Patient",Suffix="",SubscriberId="SUB123",SubscriberIdTypeDesc="Member ID" };
			claim.SubscriberName=claim.PatientName;
			claim.DateServiceStart=new DateTime(2026,9,1);
			claim.DateServiceEnd=new DateTime(2026,9,1);
			for(int i=0;i<arrayProcs.Length;i++) {
				arrayProcs[i].ClaimPaid=claim;
				claim.ClaimFee+=arrayProcs[i].ProcFee;
				claim.InsPaid+=arrayProcs[i].InsPaid;
				claim.PatientRespAmt+=arrayProcs[i].PatRespTotal;
				claim.PatientDeductAmt+=arrayProcs[i].DeductibleAmt;
				claim.PatientPortionAmt+=arrayProcs[i].PatientPortionAmt;
				claim.WriteoffAmt+=arrayProcs[i].WriteoffAmt;
			}
			claim.AllowedAmt=claim.InsPaid+claim.PreAuthInsEst+claim.PatientRespAmt;
			return claim;
		}

		///<summary>A not-received procedure claimproc.</summary>
		public static ClaimProc NewClaimProc(long claimProcNum,long procNum,string codeSent,double feeBilled,ClaimProcStatus status=ClaimProcStatus.NotReceived) {
			return new ClaimProc { ClaimProcNum=claimProcNum,ProcNum=procNum,CodeSent=codeSent,FeeBilled=feeBilled,Status=status,ClaimNum=500 };
		}
	}
}
