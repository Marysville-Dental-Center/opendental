// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using SyntheticData.Infrastructure;

namespace SyntheticData.Generation {
	public partial class FamilySimulator {
		private static readonly DateTime _minDate=new DateTime(1,1,1);
		private static readonly DateTime _dateShowTransSinceZero=new DateTime(2025,7,1);
		private readonly Dictionary<long,decimal> _dictPayPlanDue=new Dictionary<long,decimal>();
		private bool _hasPrepayment;
		private bool _hasBouncedEmail;

		#region Visits and insurance
		private void CompleteVisit(Person person,List<ProcRecord> listProcs,long aptNum,DateTime date) {
			foreach(ProcRecord proc in listProcs) {
				proc.Status=OdValues.ProcComplete;
				proc.AptNum=aptNum;
				proc.DateBillable=date;
				if(proc.DateTP.Year<1900) {
					proc.DateTP=date;
				}
				if(!_listProcs.Contains(proc)) {
					_listProcs.Add(proc);
				}
				_listLedger.Add(new LedgerEntry(date,proc.Fee,person));
				if(_world.ListSampleCompletedProcs.Count<40) {
					_world.ListSampleCompletedProcs.Add((proc.ProcNum,person.PatNum,proc.ProvNum,date));
				}
			}
			if(person.ListCoverages.Count==0 && person.AgeOn(date)>=65) {
				foreach(ProcRecord proc in listProcs.Where(x => x.Fee>0)) {
					AddAdjustment(person,date,-Math.Round(proc.Fee*0.10m,2),"Senior Discount",proc,"Senior discount 10%");
				}
			}
			else if(_rng.Chance(0.03)) {
				ProcRecord proc=listProcs.OrderByDescending(x => x.Fee).First();
				AddAdjustment(person,date,-Math.Round(proc.Fee*_rng.Pick(new[] { 0.10m,0.15m,0.20m }),2),"Courtesy Discount",proc,"Courtesy discount approved by the dentist");
			}
			ProcessInsurance(person,listProcs,date);
			List<ProcRecord> listNoBill=listProcs.Where(x => x.Code.NoBillIns).ToList();
			if(listNoBill.Count>0) {
				CreateInvoice(person,listNoBill,date);
			}
			if(!_isShowcase) {
				PayAtVisit(person,listProcs,date);
			}
			if(_rng.Chance(0.6)) {
				ProcRecord procMain=listProcs.OrderByDescending(x => x.Fee).First();
				ProviderInfo provider=_world.AllProviders.First(x => x.ProvNum==procMain.ProvNum);
				_sink.Insert("procnote",new Row {
					["ProcNoteNum"]=_sink.NextId("procnote"),["PatNum"]=person.PatNum,["ProcNum"]=procMain.ProcNum,
					["EntryDateTime"]=date.AddHours(_rng.Between(9,16)).AddMinutes(_rng.Next(60)),["UserNum"]=provider.UserNum,
					["Note"]=_rng.Pick(new[] { "Patient tolerated the visit well. No complications.","Reviewed home care. Next visit as planned.",
						"Local anesthetic used. Patient comfortable throughout.","Discussed findings and treatment options with the patient." }),
					["SigIsTopaz"]=false,["Signature"]="",
				});
			}
			if(_rng.Chance(0.12)) {
				AddSecurityLog(OdValues.PermProcComplCreate,_world.AllProviders.First(x => x.ProvNum==listProcs[0].ProvNum).UserNum,date.AddHours(15),
					person.PatNum,listProcs[0].ProcNum,listProcs[0].Code.Code+" set complete, fee "+listProcs[0].Fee.ToString("0.00"));
			}
			AddImagesForVisit(person,listProcs,date);
		}

		private void ProcessInsurance(Person person,List<ProcRecord> listProcs,DateTime date) {
			List<InsCoverage> listCoverages=person.ListCoverages.OrderBy(x => x.Ordinal).ToList();
			if(listCoverages.Count==0) {
				return;
			}
			foreach(ProcRecord proc in listProcs.Where(x => x.Code.NoBillIns)) {
				foreach(InsCoverage coverage in listCoverages) {
					WriteClaimProc(new Row {
						["ClaimProcNum"]=_sink.NextId("claimproc"),["ProcNum"]=proc.ProcNum,["ClaimNum"]=0L,["PatNum"]=person.PatNum,["ProvNum"]=proc.ProvNum,
						["Status"]=OdValues.CpEstimate,["NoBillIns"]=true,["PlanNum"]=coverage.Plan.PlanNum,["InsSubNum"]=coverage.InsSubNum,
						["ProcDate"]=date,["DateEntry"]=date,["SecDateEntry"]=date,
					});
				}
			}
			List<ProcRecord> listBillable=listProcs.Where(x => !x.Code.NoBillIns && x.Fee>0).ToList();
			if(listBillable.Count==0) {
				return;
			}
			InsCoverage primary=listCoverages[0];
			(string status,DateTime dateSent,DateTime? dateReceived) timing=ClaimTiming(date);
			Dictionary<ProcRecord,decimal> dictPrimaryPortion=MakeClaim(person,primary,listBillable,date,timing,isSecondary:false,dictPrimaryPortion:null,
				planNum2:listCoverages.Count>1 ? listCoverages[1] : null);
			if(listCoverages.Count>1) {
				(string status,DateTime dateSent,DateTime? dateReceived) timingSecondary;
				if(timing.dateReceived.HasValue) {
					timingSecondary=ClaimTiming(timing.dateReceived.Value.AddDays(_rng.Between(1,3)));
				}
				else {
					//Held until the primary insurance pays.
					timingSecondary=("H",_minDate,null);
				}
				MakeClaim(person,listCoverages[1],listBillable,date,timingSecondary,isSecondary:true,dictPrimaryPortion:dictPrimaryPortion,planNum2:primary);
			}
		}

		///<summary>Claim status and dates for a visit: unsent for the last couple of days, then sent, then received (paid on a Friday)
		///10 to 35 days after sending, if that is on or before the as-of date.</summary>
		private (string,DateTime,DateTime?) ClaimTiming(DateTime date) {
			int daysOld=(AsOf-date).Days;
			if(daysOld<2) {
				return (_rng.Chance(0.5) ? "U" : "W",_minDate,null);
			}
			DateTime dateSent=date.AddDays(_rng.Between(0,2));
			if(dateSent>AsOf) {
				dateSent=AsOf;
			}
			DateTime received=dateSent.AddDays(_rng.Between(10,35));
			received=received.AddDays(((int)DayOfWeek.Friday-(int)received.DayOfWeek+7)%7);
			if(received<=AsOf) {
				return ("R",dateSent,received);
			}
			return ("S",dateSent,null);
		}

		///<summary>Writes a claim and its claimprocs, updates the procedures and the family ledger, and returns what this insurance paid
		///or is expected to pay per procedure.</summary>
		private Dictionary<ProcRecord,decimal> MakeClaim(Person person,InsCoverage coverage,List<ProcRecord> listProcs,DateTime date,
			(string status,DateTime dateSent,DateTime? dateReceived) timing,bool isSecondary,Dictionary<ProcRecord,decimal> dictPrimaryPortion,InsCoverage planNum2)
		{
			Dictionary<ProcRecord,decimal> dictPortion=new Dictionary<ProcRecord,decimal>();
			long claimNum=_sink.NextId("claim");
			bool isReceived=timing.dateReceived.HasValue;
			ClaimPaymentBatch batch=null;
			if(isReceived) {
				batch=ClaimPaymentFor(coverage.Plan,timing.dateReceived.Value);
			}
			decimal claimFee=0, claimEst=0, claimPaid=0, claimDed=0, claimWriteOff=0;
			string reasonUnderPaid="";
			int lineNumber=1;
			foreach(ProcRecord proc in listProcs) {
				(decimal allowed,decimal writeOff,decimal ded,decimal insEst,int percent) estimate=Estimate(person,coverage,proc,date,isSecondary);
				if(isSecondary) {
					decimal left=proc.Fee-proc.WriteOff-(dictPrimaryPortion!=null && dictPrimaryPortion.TryGetValue(proc,out decimal primaryPortion) ? primaryPortion : 0);
					estimate.insEst=Math.Max(0,Math.Min(estimate.insEst,left));
					estimate.writeOff=0;
				}
				decimal paid=0;
				string remarks="";
				if(isReceived) {
					double roll=_rng.NextDouble();
					if(roll<0.05 && estimate.insEst>0) {
						paid=0;
						remarks="Denied: frequency limitation reached.";
						reasonUnderPaid="One or more procedures denied.";
					}
					else if(roll<0.14) {
						paid=Math.Round(estimate.insEst*0.9m,2);
						remarks=paid<estimate.insEst ? "Paid at the carrier's allowed amount." : "";
					}
					else {
						paid=estimate.insEst;
					}
				}
				UpdateAccumulators(person,coverage,proc,date,estimate.ded,isReceived ? paid : estimate.insEst);
				long claimProcNum=_sink.NextId("claimproc");
				WriteClaimProc(new Row {
					["ClaimProcNum"]=claimProcNum,["ProcNum"]=proc.ProcNum,["ClaimNum"]=claimNum,["PatNum"]=person.PatNum,["ProvNum"]=proc.ProvNum,
					["FeeBilled"]=proc.Fee,["InsPayEst"]=estimate.insEst,["DedApplied"]=estimate.ded,
					["Status"]=isReceived ? OdValues.CpReceived : OdValues.CpNotReceived,["InsPayAmt"]=paid,["Remarks"]=remarks,
					["ClaimPaymentNum"]=batch?.ClaimPaymentNum??0,["PlanNum"]=coverage.Plan.PlanNum,["DateCP"]=isReceived ? timing.dateReceived.Value : date,
					["WriteOff"]=estimate.writeOff,["CodeSent"]=proc.Code.Code,["Percentage"]=estimate.percent,["BaseEst"]=Math.Round((estimate.allowed-estimate.ded)*estimate.percent/100m,2),
					["ProcDate"]=date,["DateEntry"]=date,["LineNumber"]=lineNumber++,["DedEst"]=estimate.ded,["InsEstTotal"]=estimate.insEst,
					["WriteOffEst"]=estimate.writeOff,["InsSubNum"]=coverage.InsSubNum,["SecUserNumEntry"]=_rng.Pick(_world.ListFrontDeskUsers),
					["SecDateEntry"]=date,["DateInsFinalized"]=isReceived ? timing.dateReceived.Value : _minDate,
					["PaidOtherIns"]=isSecondary && dictPrimaryPortion!=null && dictPrimaryPortion.TryGetValue(proc,out decimal paidOther) ? paidOther : 0m,
					["SecurityHash"]=date.Year>=2025 ? World.PseudoHash("claimproc"+claimProcNum) : "",
				});
				proc.WriteOff+=estimate.writeOff;
				if(estimate.writeOff!=0) {
					_listLedger.Add(new LedgerEntry(date,-estimate.writeOff,person));
				}
				if(isReceived) {
					proc.InsPaid+=paid;
					if(paid!=0) {
						_listLedger.Add(new LedgerEntry(timing.dateReceived.Value,-paid,person));
					}
					batch.Amount+=paid;
					proc.DateBillable=Max(proc.DateBillable,timing.dateReceived.Value);
					dictPortion[proc]=paid;
				}
				else {
					proc.InsEstOutstanding+=estimate.insEst;
					person.OwnInsEst+=estimate.insEst;
					proc.DateBillable=Max(proc.DateBillable,date.AddDays(30));
					dictPortion[proc]=estimate.insEst;
				}
				proc.HasCompletedClaim=true;
				claimFee+=proc.Fee;
				claimEst+=estimate.insEst;
				claimPaid+=paid;
				claimDed+=estimate.ded;
				claimWriteOff+=estimate.writeOff;
			}
			_listInsTimeline.Add((date,timing.dateReceived,claimEst));
			//A supplemental payment now and then, a few weeks after the claim was paid.
			if(isReceived && !isSecondary && _rng.Chance(0.03)) {
				DateTime dateSupp=timing.dateReceived.Value.AddDays(21);
				ProcRecord proc=listProcs.FirstOrDefault(x => x.PatientRemainder>=20);
				if(proc!=null && dateSupp<=AsOf) {
					decimal amount=Math.Min(proc.PatientRemainder,_rng.Money(10,40));
					ClaimPaymentBatch batchSupp=ClaimPaymentFor(coverage.Plan,dateSupp);
					WriteClaimProc(new Row {
						["ClaimProcNum"]=_sink.NextId("claimproc"),["ProcNum"]=proc.ProcNum,["ClaimNum"]=claimNum,["PatNum"]=person.PatNum,["ProvNum"]=proc.ProvNum,
						["Status"]=OdValues.CpSupplemental,["InsPayAmt"]=amount,["ClaimPaymentNum"]=batchSupp.ClaimPaymentNum,["PlanNum"]=coverage.Plan.PlanNum,
						["DateCP"]=dateSupp,["ProcDate"]=date,["DateEntry"]=dateSupp,["InsSubNum"]=coverage.InsSubNum,["Remarks"]="Supplemental payment",
						["DateSuppReceived"]=dateSupp,["SecDateEntry"]=dateSupp,
					});
					batchSupp.Amount+=amount;
					proc.InsPaid+=amount;
					_listLedger.Add(new LedgerEntry(dateSupp,-amount,person));
					claimPaid+=amount;
				}
			}
			ProviderInfo provTreat=_world.AllProviders.First(x => x.ProvNum==listProcs[0].ProvNum);
			if(provTreat.IsHygienist) {
				provTreat=person.Dentist;
			}
			DateTime dateEntry=date.AddDays(timing.status=="U" ? 0 : 1);
			_sink.Insert("claim",new Row {
				["ClaimNum"]=claimNum,["PatNum"]=person.PatNum,["DateService"]=date,["DateSent"]=timing.dateSent,["ClaimStatus"]=timing.status,
				["DateReceived"]=timing.dateReceived??_minDate,["PlanNum"]=coverage.Plan.PlanNum,["ProvTreat"]=provTreat.ProvNum,["ClaimFee"]=claimFee,
				["InsPayEst"]=claimEst,["InsPayAmt"]=claimPaid,["DedApplied"]=claimDed,["PreAuthString"]="",["IsProsthesis"]="N",["ReasonUnderPaid"]=reasonUnderPaid,
				["ClaimNote"]="",["ClaimType"]=isSecondary ? "S" : "P",["ProvBill"]=provTreat.ProvNum,["RefNumString"]="",["PlaceService"]=0,
				["AccidentRelated"]="",["AccidentST"]="",["PatRelat"]=coverage.Relationship,["PlanNum2"]=planNum2?.Plan.PlanNum??0,
				["PatRelat2"]=planNum2?.Relationship??0,["WriteOff"]=claimWriteOff,["ClinicNum"]=_world.ClinicNum,["ClaimForm"]=0L,
				["AttachedFlags"]="Mail",["AttachmentID"]="",["InsSubNum"]=coverage.InsSubNum,["InsSubNum2"]=planNum2?.InsSubNum??0,
				["ClaimIdentifier"]=person.PatNum+"/"+claimNum,["OrigRefNum"]="",["SecUserNumEntry"]=_rng.Pick(_world.ListFrontDeskUsers),
				["SecDateEntry"]=dateEntry,["SecDateTEdit"]=(timing.dateReceived??dateEntry).AddHours(15),["DateSentOrig"]=timing.dateSent,
				["SecurityHash"]=date.Year>=2025 ? World.PseudoHash("claim"+claimNum) : "",["Narrative"]="",
			});
			if(isReceived && _rng.Chance(0.05)) {
				_world.ListEobClaimPayments.Add(batch.ClaimPaymentNum);
			}
			return dictPortion;
		}

		private ClaimPaymentBatch ClaimPaymentFor(InsPlanInfo plan,DateTime checkDate) {
			if(!_world.DictClaimPayments.TryGetValue((plan.CarrierNum,checkDate),out ClaimPaymentBatch batch)) {
				batch=new ClaimPaymentBatch {
					ClaimPaymentNum=_sink.NextId("claimpayment"),
					CarrierNum=plan.CarrierNum,
					CarrierName=plan.CarrierName,
					CheckDate=checkDate,
					IsEft=plan.UsesEft,
				};
				_world.DictClaimPayments[(plan.CarrierNum,checkDate)]=batch;
			}
			return batch;
		}

		///<summary>Insurance estimate for one procedure: PPO allowed fee and write-off, exclusions (implants, third-molar surgery by tooth
		///range), crown waiting period, age limits, frequencies, deductible and annual maximum.</summary>
		private (decimal allowed,decimal writeOff,decimal ded,decimal insEst,int percent) Estimate(Person person,InsCoverage coverage,ProcRecord proc,DateTime date,bool isSecondary) {
			InsPlanInfo plan=coverage.Plan;
			decimal fee=proc.Fee;
			decimal allowed=fee;
			if(plan.IsPpo && !isSecondary) {
				decimal ppo=_world.GetFee(plan.FeeSched,proc.Code.CodeNum,0,date)??fee;
				allowed=Math.Min(fee,ppo);
			}
			decimal writeOff=plan.IsPpo && !isSecondary ? fee-allowed : 0;
			CovCatDef covCat=Catalog.CovCatFor(_world.ListCovCats,proc.Code.Code);
			int percent=covCat==null ? 0 : plan.DictPercentByCovCat[covCat.CovCatNum];
			string code=proc.Code.Code;
			int age=person.AgeOn(date);
			bool isExcluded=(plan.ExcludesImplants && (code=="Z6010" || code=="Z6065"))
				|| (plan.ExcludesThirdMolarSurgery && code=="Z7210" && new[] { "1","16","17","32" }.Contains(proc.ToothNum))
				|| (plan.CrownWaitMonths>0 && covCat?.Description=="Crowns" && coverage.DateEffective.AddMonths(plan.CrownWaitMonths)>date);
			if(isExcluded) {
				return (fee,0,0,0,0);
			}
			if((code=="Z1206" && age>=14) || (code=="Z1351" && age>=16)) {
				percent=0;
			}
			string group=code=="Z0272" || code=="Z0274" ? "BW"
				: code=="Z0120" || code=="Z0140" || code=="Z0150" || code=="Z0180" ? "Exam"
				: code=="Z1110" || code=="Z1120" ? "Prophy"
				: null;
			int limit=group=="BW" ? 1 : 2;
			if(group!=null) {
				person.DictFreqUsed.TryGetValue((group,plan.PlanNum,date.Year),out int used);
				if(used>=limit) {
					percent=0;
				}
			}
			decimal ded=0;
			bool isDeductibleCategory=covCat!=null && covCat.Description!="Diagnostic" && covCat.Description!="X-Ray" && covCat.Description!="Preventive";
			if(isDeductibleCategory && percent>0) {
				person.DictDedUsed.TryGetValue((plan.PlanNum,date.Year),out decimal dedUsed);
				ded=Math.Max(0,Math.Min(plan.Deductible-dedUsed,allowed));
			}
			decimal insEst=Math.Round((allowed-ded)*percent/100m,2);
			if(covCat?.Description!="Orthodontics") {
				person.DictInsUsed.TryGetValue((plan.PlanNum,date.Year),out decimal insUsed);
				insEst=Math.Max(0,Math.Min(insEst,plan.AnnualMax-insUsed));
			}
			else {
				insEst=Math.Min(insEst,1500);
			}
			return (allowed,writeOff,ded,insEst,percent);
		}

		private void UpdateAccumulators(Person person,InsCoverage coverage,ProcRecord proc,DateTime date,decimal ded,decimal insAmount) {
			long planNum=coverage.Plan.PlanNum;
			person.DictDedUsed.TryGetValue((planNum,date.Year),out decimal dedUsed);
			person.DictDedUsed[(planNum,date.Year)]=dedUsed+ded;
			person.DictInsUsed.TryGetValue((planNum,date.Year),out decimal insUsed);
			person.DictInsUsed[(planNum,date.Year)]=insUsed+insAmount;
			string code=proc.Code.Code;
			string group=code=="Z0272" || code=="Z0274" ? "BW" : code=="Z0120" || code=="Z0140" || code=="Z0150" || code=="Z0180" ? "Exam"
				: code=="Z1110" || code=="Z1120" ? "Prophy" : null;
			if(group!=null) {
				person.DictFreqUsed.TryGetValue((group,planNum,date.Year),out int used);
				person.DictFreqUsed[(group,planNum,date.Year)]=used+1;
			}
		}

		///<summary>Preauthorization for major treatment. Received ones sometimes have an EOB attached (25.2: eobattach.ClaimNumPreAuth).</summary>
		private void CreatePreauth(Person person,List<ProcRecord> listProcs,DateTime date) {
			InsCoverage coverage=person.ListCoverages.OrderBy(x => x.Ordinal).First();
			long claimNum=_sink.NextId("claim");
			DateTime dateSent=date.AddDays(1);
			if(dateSent>AsOf) {
				dateSent=AsOf;
			}
			DateTime dateReceived=dateSent.AddDays(_rng.Between(14,30));
			bool isReceived=dateReceived<=AsOf;
			decimal total=0, totalEst=0;
			foreach(ProcRecord proc in listProcs) {
				(decimal allowed,decimal writeOff,decimal ded,decimal insEst,int percent) estimate=Estimate(person,coverage,proc,date,false);
				WriteClaimProc(new Row {
					["ClaimProcNum"]=_sink.NextId("claimproc"),["ProcNum"]=proc.ProcNum,["ClaimNum"]=claimNum,["PatNum"]=person.PatNum,["ProvNum"]=proc.ProvNum,
					["FeeBilled"]=proc.Fee,["InsPayEst"]=isReceived ? estimate.insEst : 0m,["Status"]=OdValues.CpPreauth,["PlanNum"]=coverage.Plan.PlanNum,
					["DateCP"]=isReceived ? dateReceived : date,["WriteOff"]=0m,["CodeSent"]=proc.Code.Code,["Percentage"]=estimate.percent,["ProcDate"]=date,
					["DateEntry"]=date,["InsSubNum"]=coverage.InsSubNum,["SecDateEntry"]=date,
				});
				total+=proc.Fee;
				totalEst+=isReceived ? estimate.insEst : 0;
			}
			_sink.Insert("claim",new Row {
				["ClaimNum"]=claimNum,["PatNum"]=person.PatNum,["DateService"]=_minDate,["DateSent"]=dateSent,["ClaimStatus"]=isReceived ? "R" : "S",
				["DateReceived"]=isReceived ? dateReceived : _minDate,["PlanNum"]=coverage.Plan.PlanNum,["ProvTreat"]=person.Dentist.ProvNum,["ClaimFee"]=total,
				["InsPayEst"]=totalEst,["PreAuthString"]=isReceived ? "PA"+_rng.Between(100000,999999) : "",["IsProsthesis"]="I",["ReasonUnderPaid"]="",
				["ClaimNote"]="",["ClaimType"]="PreAuth",["ProvBill"]=person.Dentist.ProvNum,["RefNumString"]="",["AccidentRelated"]="",["AccidentST"]="",
				["PatRelat"]=coverage.Relationship,["ClinicNum"]=_world.ClinicNum,["AttachedFlags"]="Mail",["AttachmentID"]="",["InsSubNum"]=coverage.InsSubNum,
				["ClaimIdentifier"]=person.PatNum+"/"+claimNum,["OrigRefNum"]="",["SecUserNumEntry"]=_rng.Pick(_world.ListFrontDeskUsers),["SecDateEntry"]=date,
				["SecDateTEdit"]=(isReceived ? dateReceived : date).AddHours(15),["DateSentOrig"]=dateSent,["SecurityHash"]="",["Narrative"]="Treatment plan attached.",
			});
			if(isReceived && Chance(0.6)) {
				_world.ListPreauthClaims.Add((claimNum,person.PatNum,coverage.Plan.PlanNum,dateReceived));
			}
		}

		///<summary>Writes a claimproc, filling the "no override" columns with -1 the way Open Dental does.</summary>
		private void WriteClaimProc(Row row) {
			foreach(string column in new[] { "AllowedOverride","DedEstOverride","InsEstTotalOverride","PaidOtherInsOverride","WriteOffEstOverride" }) {
				if(!row.ContainsKey(column)) {
					row[column]=-1m;
				}
			}
			if(!row.ContainsKey("PaidOtherIns")) {
				row["PaidOtherIns"]=-1m;
			}
			if(!row.ContainsKey("EstimateNote")) {
				row["EstimateNote"]="";
			}
			if(!row.ContainsKey("ClinicNum")) {
				row["ClinicNum"]=_world.ClinicNum;
			}
			_sink.Insert("claimproc",row);
		}

		///<summary>Insurance estimates for treatment still planned on the as-of date.</summary>
		private void WriteEstimates(ProcRecord proc) {
			foreach(InsCoverage coverage in proc.Patient.ListCoverages.OrderBy(x => x.Ordinal)) {
				(decimal allowed,decimal writeOff,decimal ded,decimal insEst,int percent) estimate=Estimate(proc.Patient,coverage,proc,AsOf,coverage.Ordinal>1);
				WriteClaimProc(new Row {
					["ClaimProcNum"]=_sink.NextId("claimproc"),["ProcNum"]=proc.ProcNum,["ClaimNum"]=0L,["PatNum"]=proc.Patient.PatNum,["ProvNum"]=proc.ProvNum,
					["Status"]=OdValues.CpEstimate,["NoBillIns"]=proc.Code.NoBillIns,["PlanNum"]=coverage.Plan.PlanNum,["InsSubNum"]=coverage.InsSubNum,
					["Percentage"]=estimate.percent,["BaseEst"]=Math.Round((estimate.allowed-estimate.ded)*estimate.percent/100m,2),["DedEst"]=estimate.ded,
					["InsEstTotal"]=estimate.insEst,["WriteOffEst"]=estimate.writeOff,["ProcDate"]=proc.ProcDate,["DateEntry"]=proc.DateTP,
					["SecDateEntry"]=proc.DateTP,["EstimateNote"]=estimate.percent==0 && estimate.insEst==0 ? "Not covered" : "",
				});
			}
		}
		#endregion Visits and insurance

		#region Payments and adjustments
		private void PayAtVisit(Person person,List<ProcRecord> listProcs,DateTime date) {
			decimal due=listProcs.Sum(x => Math.Max(0,x.PatientRemainder));
			if(due<1) {
				return;
			}
			double fullChance=_payerKind==PayerKind.Prompt ? 0.72 : _payerKind==PayerKind.Slow ? 0.12 : 0.04;
			double partialChance=_payerKind==PayerKind.Prompt ? 0.12 : _payerKind==PayerKind.Slow ? 0.25 : 0.08;
			double roll=_rng.NextDouble();
			decimal amount;
			if(roll<fullChance) {
				amount=due;
			}
			else if(roll<fullChance+partialChance) {
				amount=Math.Round(due*_rng.Pick(new[] { 0.25m,0.4m,0.5m }),0);
			}
			else {
				return;
			}
			List<(ProcRecord,decimal)> listSplits=Allocate(listProcs,amount);
			if(listSplits.Count==0) {
				return;
			}
			long payNum=CreatePayment(person,date,listSplits,"Paid at checkout");
			if(_rng.Chance(0.15)) {
				WriteStatementRow(person,date,OdValues.StmtInPerson,isReceipt:true,isInvoice:false,balTotal:0,insEst:0,singlePatient:true);
			}
			if(payNum%7==0 && _rng.Chance(0.5)) {
				AddCommlog(person,date.AddHours(16),"Financial",OdValues.CommModeEmail,OdValues.CommSent,"Receipt e-mailed for today's payment.");
			}
		}

		private static List<(ProcRecord,decimal)> Allocate(List<ProcRecord> listProcs,decimal amount) {
			List<(ProcRecord,decimal)> listSplits=new List<(ProcRecord,decimal)>();
			foreach(ProcRecord proc in listProcs) {
				decimal remainder=proc.PatientRemainder;
				if(amount<=0) {
					break;
				}
				if(remainder<=0) {
					continue;
				}
				decimal split=Math.Min(remainder,amount);
				listSplits.Add((proc,split));
				amount-=split;
			}
			return listSplits;
		}

		///<summary>A payment with one split per procedure (or one unearned split when the list is empty and unearned is given).</summary>
		private long CreatePayment(Person person,DateTime date,List<(ProcRecord proc,decimal amount)> listSplits,string note,decimal unearned=0,
			long payPlanNum=0,List<(long chargeNum,decimal amount,int debitType)> listPayPlanSplits=null)
		{
			string payTypeName=_rng.PickWeighted(new List<(string,double)> { ("Credit Card",0.5),("Check",0.28),("Cash",0.17),("Financing",0.05) });
			decimal total=listSplits.Sum(x => x.amount)+unearned+(listPayPlanSplits?.Sum(x => x.amount)??0);
			long payNum=_sink.NextId("payment");
			DepositBatch deposit=_world.DepositFor(date);
			long userNum=_rng.Pick(_world.ListFrontDeskUsers);
			DateTime entry=date.AddHours(_rng.Between(9,16)).AddMinutes(_rng.Next(60));
			Person payer=_guarantor.Status==OdValues.PatNonPatient || _rng.Chance(0.7) ? _guarantor : person;
			_sink.Insert("payment",new Row {
				["PayNum"]=payNum,["PayType"]=_world.Def(OdValues.DefPaymentTypes,payTypeName),["PayDate"]=date,["PayAmt"]=total,
				["CheckNum"]=payTypeName=="Check" ? _rng.Between(1001,9999).ToString() : "",["BankBranch"]="",["PayNote"]=note,
				["IsSplit"]=listSplits.Count+(listPayPlanSplits?.Count??0)+(unearned>0 ? 1 : 0)>1,["PatNum"]=payer.PatNum,["ClinicNum"]=_world.ClinicNum,
				["DateEntry"]=date,["DepositNum"]=deposit?.DepositNum??0,["Receipt"]="",["IsRecurringCC"]=false,["SecUserNumEntry"]=userNum,
				["SecDateTEdit"]=entry,["PaymentSource"]=0,["ProcessStatus"]=0,["ExternalId"]="",["PaymentStatus"]=0,
				["MerchantFee"]=payTypeName=="Credit Card" ? (double)Math.Round(total*0.029m,2) : 0.0,
			});
			if(deposit!=null) {
				deposit.PatientAmount+=total;
			}
			foreach((ProcRecord proc,decimal amount) split in listSplits) {
				WritePaySplit(split.proc.Patient,payNum,date,split.amount,split.proc.ProvNum,split.proc.ProcNum,0,0,0,0,userNum,entry);
				split.proc.PatPaid+=split.amount;
				_listLedger.Add(new LedgerEntry(date,-split.amount,split.proc.Patient));
			}
			if(unearned>0) {
				WritePaySplit(person,payNum,date,unearned,person.Dentist.ProvNum,0,_world.Def(OdValues.DefPaySplitUnearnedType,"Prepayment"),0,0,0,userNum,entry);
				_listLedger.Add(new LedgerEntry(date,-unearned,person));
			}
			if(listPayPlanSplits!=null) {
				foreach((long chargeNum,decimal amount,int debitType) split in listPayPlanSplits) {
					WritePaySplit(person,payNum,date,split.amount,person.Dentist.ProvNum,0,0,payPlanNum,split.chargeNum,split.debitType,userNum,entry);
					_listLedger.Add(new LedgerEntry(date,-split.amount,person));
				}
			}
			if(_rng.Chance(0.1)) {
				AddSecurityLog(OdValues.PermPaymentCreate,userNum,entry,payer.PatNum,payNum,"Payment of "+total.ToString("0.00")+" ("+payTypeName+")");
			}
			return payNum;
		}

		private void WritePaySplit(Person person,long payNum,DateTime date,decimal amount,long provNum,long procNum,long unearnedType,long payPlanNum,long payPlanChargeNum,
			int payPlanDebitType,long userNum,DateTime entry)
		{
			long splitNum=_sink.NextId("paysplit");
			_sink.Insert("paysplit",new Row {
				["SplitNum"]=splitNum,["SplitAmt"]=amount,["PatNum"]=person.PatNum,["ProcDate"]=date,["PayNum"]=payNum,["IsDiscount"]=false,
				["DiscountType"]=0,["ProvNum"]=unearnedType!=0 ? 0 : provNum,["PayPlanNum"]=payPlanNum,["DatePay"]=date,["ProcNum"]=procNum,["DateEntry"]=date,
				["UnearnedType"]=unearnedType,["ClinicNum"]=_world.ClinicNum,["SecUserNumEntry"]=userNum,["SecDateTEdit"]=entry,["FSplitNum"]=0L,
				["AdjNum"]=0L,["PayPlanChargeNum"]=payPlanChargeNum,["PayPlanDebitType"]=payPlanDebitType,
				["SecurityHash"]=date.Year>=2025 ? World.PseudoHash("paysplit"+splitNum) : "",
			});
		}

		private long AddAdjustment(Person person,DateTime date,decimal amount,string adjTypeName,ProcRecord proc,string note,long statementNum=0) {
			long adjNum=_sink.NextId("adjustment");
			_sink.Insert("adjustment",new Row {
				["AdjNum"]=adjNum,["AdjDate"]=date,["AdjAmt"]=amount,["PatNum"]=person.PatNum,["AdjType"]=_world.Def(OdValues.DefAdjTypes,adjTypeName),
				["ProvNum"]=proc?.ProvNum??person.Dentist.ProvNum,["AdjNote"]=note,["ProcDate"]=proc?.ProcDate??date,["ProcNum"]=proc?.ProcNum??0,
				["DateEntry"]=date,["ClinicNum"]=_world.ClinicNum,["StatementNum"]=statementNum,["SecUserNumEntry"]=_rng.Pick(_world.ListFrontDeskUsers),
				["SecDateTEdit"]=date.AddHours(15),["TaxTransID"]=0L,
			});
			if(proc!=null) {
				proc.AdjTotal+=amount;
			}
			_listLedger.Add(new LedgerEntry(date,amount,person));
			if(_rng.Chance(0.1)) {
				AddSecurityLog(OdValues.PermAdjustmentCreate,_rng.Pick(_world.ListFrontDeskUsers),date.AddHours(15),person.PatNum,adjNum,
					adjTypeName+" of "+amount.ToString("0.00"));
			}
			return adjNum;
		}

		///<summary>Invoice for procedures insurance never pays for (25.1: the invoice's InsEst holds its adjustment total).</summary>
		private void CreateInvoice(Person person,List<ProcRecord> listProcs,DateTime date) {
			long statementNum=_sink.NextId("statement");
			decimal adjTotal=0;
			foreach(ProcRecord proc in listProcs) {
				proc.StatementNum=statementNum;
				if(_rng.Chance(0.5)) {
					decimal amount=-Math.Round(proc.Fee*0.1m,2);
					AddAdjustment(person,date,amount,"Courtesy Discount",proc,"Invoice discount",statementNum);
					adjTotal+=amount;
				}
			}
			WriteStatementRow(person,date,OdValues.StmtInPerson,isReceipt:false,isInvoice:true,balTotal:listProcs.Sum(x => x.Fee)+adjTotal,insEst:adjTotal,
				singlePatient:true,statementNum:statementNum);
		}

		///<summary>Payment plans for large balances (24.4 sheet choice, 25.1 down-payment flag and lock), some of them dynamic.</summary>
		private void CreatePaymentPlans() {
			if(_payerKind==PayerKind.NonPayer) {
				return;
			}
			decimal threshold=_isShowcase ? 200 : 600;
			ProcRecord proc=_listProcs.Where(x => x.Status==OdValues.ProcComplete && x.PatientRemainder>=threshold && x.DateBillable<=AsOf.AddDays(-5))
				.OrderByDescending(x => x.Fee).ThenByDescending(x => x.ProcDate).FirstOrDefault();
			if(proc==null || !Chance(0.4)) {
				return;
			}
			Person person=proc.Patient;
			DateTime datePlan=proc.DateBillable;
			decimal principal=Math.Round(proc.PatientRemainder,2);
			bool isOrtho=proc.Code.Code=="Z8080";
			bool isDynamic=!isOrtho && datePlan>=new DateTime(2024,1,1) && _rng.Chance(0.25) && !_isShowcase;
			int count=isOrtho ? 24 : _rng.Pick(new[] { 6,10,12 });
			double apr=!isOrtho && _rng.Chance(0.3) ? 9.9 : 0;
			decimal downPayment=Chance(0.4) ? Math.Round(principal*0.2m,0) : 0;
			decimal financed=principal-downPayment;
			decimal payment=Math.Round(financed/count,2);
			long payPlanNum=_sink.NextId("payplan");
			proc.PayPlanCredit+=principal;
			_listLedger.Add(new LedgerEntry(datePlan,-principal,person));
			if(isDynamic) {
				_sink.Insert("payplanlink",new Row { ["PayPlanLinkNum"]=_sink.NextId("payplanlink"),["PayPlanNum"]=payPlanNum,["LinkType"]=OdValues.PplProcedure,
					["FKey"]=proc.ProcNum,["AmountOverride"]=principal,["SecDateTEntry"]=datePlan.AddHours(11) });
			}
			else {
				WritePayPlanCharge(payPlanNum,person,datePlan,principal,0,OdValues.PpcCredit,proc.ProcNum,"Treatment on plan: "+proc.Describe(),false);
			}
			List<(long chargeNum,DateTime date,decimal principal,decimal interest)> listDebits=new List<(long,DateTime,decimal,decimal)>();
			if(downPayment>0) {
				long chargeNum=WritePayPlanCharge(payPlanNum,person,datePlan,downPayment,0,OdValues.PpcDebit,0,"Down payment",true);
				listDebits.Add((chargeNum,datePlan,downPayment,0));
			}
			decimal balance=financed;
			for(int i=1;i<=count;i++) {
				DateTime dateCharge=datePlan.AddMonths(i);
				decimal principalPart=i==count ? balance : Math.Min(payment,balance);
				decimal interest=apr>0 ? Math.Round(balance*(decimal)apr/1200m,2) : 0;
				balance-=principalPart;
				if(isDynamic && dateCharge>AsOf) {
					break;//dynamic plans create charges as they come due
				}
				long chargeNum=WritePayPlanCharge(payPlanNum,person,dateCharge,principalPart,interest,OdValues.PpcDebit,0,"",false);
				listDebits.Add((chargeNum,dateCharge,principalPart,interest));
			}
			decimal due=0, paid=0;
			bool allPaid=true;
			foreach((long chargeNum,DateTime date,decimal principal,decimal interest) debit in listDebits.Where(x => x.date<=AsOf)) {
				_listLedger.Add(new LedgerEntry(debit.date,debit.principal+debit.interest,person));
				due+=debit.principal+debit.interest;
				bool pays=_payerKind==PayerKind.Prompt ? _rng.Chance(0.95) : _rng.Chance(0.7);
				DateTime datePay=debit.date.AddDays(_rng.Between(0,6));
				if(!pays || datePay>AsOf) {
					allPaid=false;
					continue;
				}
				List<(long,decimal,int)> listSplits=new List<(long,decimal,int)> { (debit.chargeNum,debit.principal,OdValues.PpdPrincipal) };
				if(debit.interest>0) {
					listSplits.Add((debit.chargeNum,debit.interest,OdValues.PpdInterest));
				}
				CreatePayment(person,datePay,new List<(ProcRecord,decimal)>(),"Payment plan installment",0,payPlanNum,listSplits);
				paid+=debit.principal+debit.interest;
			}
			bool isClosed=allPaid && listDebits.Count>0 && listDebits.Max(x => x.date)<=AsOf && !isDynamic;
			_dictPayPlanDue.TryGetValue(_guarantor.PatNum,out decimal dueSoFar);
			_dictPayPlanDue[_guarantor.PatNum]=dueSoFar+(due-paid);
			_sink.Insert("payplan",new Row {
				["PayPlanNum"]=payPlanNum,["PatNum"]=person.PatNum,["Guarantor"]=_guarantor.PatNum,["PayPlanDate"]=datePlan,["APR"]=apr,
				["Note"]=isOrtho ? "Orthodontic treatment plan" : "Agreed at checkout",["PlanNum"]=0L,["CompletedAmt"]=principal,["InsSubNum"]=0L,
				["PaySchedule"]=0,["NumberOfPayments"]=count,["PayAmt"]=payment,["DownPayment"]=downPayment,["IsClosed"]=isClosed,["Signature"]="",
				["SigIsTopaz"]=false,["PlanCategory"]=isOrtho ? _world.Def(OdValues.DefPayPlanCategories,"Orthodontics") : _world.Def(OdValues.DefPayPlanCategories,"Major Treatment"),
				["IsDynamic"]=isDynamic,["ChargeFrequency"]=OdValues.PayFreqMonthly,["DatePayPlanStart"]=datePlan.AddMonths(1),["IsLocked"]=_rng.Chance(0.3),
				["DateInterestStart"]=apr>0 ? datePlan.AddMonths(1) : _minDate,["DynamicPayPlanTPOption"]=isDynamic ? 1 : 0,["MobileAppDeviceNum"]=0L,
				["SecurityHash"]=datePlan.Year>=2025 ? World.PseudoHash("payplan"+payPlanNum) : "",
				//24.4: plans can name the sheet used to print them.
				["SheetDefNum"]=Chance(0.4) ? _world.SheetDefPayPlan : 0L,
			});
			if(_rng.Chance(0.3)) {
				AddCreditCard(_guarantor,payPlanNum,payment);
			}
			if(_rng.Chance(0.3)) {
				AddSecurityLog(OdValues.PermPayPlanEdit,_rng.Pick(_world.ListFrontDeskUsers),datePlan.AddHours(15),person.PatNum,payPlanNum,"Payment plan created");
			}
		}

		private long WritePayPlanCharge(long payPlanNum,Person person,DateTime date,decimal principal,decimal interest,int chargeType,long procNum,string note,bool isDownPayment) {
			long chargeNum=_sink.NextId("payplancharge");
			_sink.Insert("payplancharge",new Row {
				["PayPlanChargeNum"]=chargeNum,["PayPlanNum"]=payPlanNum,["Guarantor"]=_guarantor.PatNum,["PatNum"]=person.PatNum,["ChargeDate"]=date,
				["Principal"]=principal,["Interest"]=interest,["Note"]=note,["ProvNum"]=person.Dentist.ProvNum,["ClinicNum"]=_world.ClinicNum,
				["ChargeType"]=chargeType,["ProcNum"]=procNum,["SecDateTEntry"]=date.AddHours(11),["StatementNum"]=0L,["FKey"]=0L,["LinkType"]=0,
				["IsOffset"]=false,["IsDownPayment"]=isDownPayment,
			});
			return chargeNum;
		}

		///<summary>Payments after the insurance paid or after a statement, by the family's payment habits; a few prepayments.</summary>
		private void FollowUpPayments() {
			SortedDictionary<DateTime,List<(ProcRecord,decimal)>> dictByDate=new SortedDictionary<DateTime,List<(ProcRecord,decimal)>>();
			foreach(ProcRecord proc in _listProcs.Where(x => x.Status==OdValues.ProcComplete).OrderBy(x => x.ProcDate).ThenBy(x => x.ProcNum)) {
				decimal remainder=Math.Round(proc.PatientRemainder,2);
				if(remainder<0.01m) {
					continue;
				}
				DateTime datePay;
				switch(_payerKind) {
					case PayerKind.Prompt:
						datePay=proc.DateBillable.AddDays(_rng.Between(7,40));
						break;
					case PayerKind.Slow:
						datePay=proc.DateBillable.AddDays(_rng.Between(40,130));
						break;
					default:
						if(!_rng.Chance(0.15)) {
							continue;
						}
						datePay=proc.DateBillable.AddDays(_rng.Between(60,200));
						break;
				}
				if(datePay>AsOf) {
					continue;
				}
				if(!dictByDate.TryGetValue(datePay,out List<(ProcRecord,decimal)> list)) {
					list=new List<(ProcRecord,decimal)>();
					dictByDate[datePay]=list;
				}
				list.Add((proc,remainder));
			}
			foreach(KeyValuePair<DateTime,List<(ProcRecord,decimal)>> pair in dictByDate) {
				CreatePayment(pair.Value[0].Item1.Patient,pair.Key,pair.Value,"Payment on account");
			}
			Person active=_listMembers.FirstOrDefault(x => x.Status==OdValues.PatPatient);
			if(active!=null && _rng.Chance(0.025)) {
				DateTime date=AsOf.AddDays(-_rng.Between(5,300));
				if(date>=_world.Settings.HistoryStart) {
					CreatePayment(active,date,new List<(ProcRecord,decimal)>(),"Prepayment toward upcoming treatment",_rng.Money(100,400));
					_hasPrepayment=true;
				}
			}
		}

		///<summary>Monthly finance charges on balances over 90 days for families that do not pay; such families end in collections.</summary>
		private void FinanceCharges() {
			if(_payerKind!=PayerKind.NonPayer) {
				return;
			}
			DateTime start=_world.Settings.HistoryStart;
			for(DateTime monthEnd=new DateTime(start.Year,start.Month,1).AddMonths(2).AddDays(-1);monthEnd<=AsOf;monthEnd=monthEnd.AddDays(1).AddMonths(1).AddDays(-1)) {
				AgingResult aging=Aging.Compute(_listLedger,monthEnd);
				if(aging.BalOver90<25) {
					continue;
				}
				decimal charge=Math.Max(1,Math.Round(aging.BalOver90*0.015m,2));
				AddAdjustment(_guarantor,monthEnd,charge,"Finance Charge",null,"Finance charge on balance over 90 days");
			}
			if(Aging.Compute(_listLedger,AsOf).BalOver90>100) {
				_billingType=_world.Def(OdValues.DefBillingTypes,"Collections");
				AddCommlog(_guarantor,AsOf.AddDays(-_rng.Between(1,30)).AddHours(11),"Financial",OdValues.CommModeMail,OdValues.CommSent,
					"Final notice mailed; account referred to collections.");
			}
		}
		#endregion Payments and adjustments

		#region Writing
		private void WriteProcedures() {
			Dictionary<Person,List<ProcRecord>> dictTp=new Dictionary<Person,List<ProcRecord>>();
			foreach(ProcRecord proc in _listProcs) {
				bool isComplete=proc.Status==OdValues.ProcComplete;
				DateTime dateEntry=isComplete ? proc.ProcDate : proc.DateTP.Year>1900 ? proc.DateTP : proc.ProcDate;
				_sink.Insert("procedurelog",new Row {
					["ProcNum"]=proc.ProcNum,["PatNum"]=proc.Patient.PatNum,["AptNum"]=proc.AptNum,["ProcDate"]=proc.ProcDate,["ProcFee"]=proc.Fee,
					["Surf"]=proc.Surf,["ToothNum"]=proc.ToothNum,["ToothRange"]="",["Priority"]=0L,["ProcStatus"]=proc.Status,["ProvNum"]=proc.ProvNum,
					["Dx"]=0L,["PlannedAptNum"]=proc.PlannedAptNum,["PlaceService"]=0,["Prosthesis"]=proc.Code.IsProsth && isComplete ? "I" : "",
					["ClaimNote"]="",["DateEntryC"]=isComplete ? proc.ProcDate : _minDate,["ClinicNum"]=_world.ClinicNum,["MedicalCode"]="",
					["DiagnosticCode"]="",["CodeNum"]=proc.Code.CodeNum,["UnitQty"]=1,["BaseUnits"]=0,["DateTP"]=proc.DateTP,["CanadianTypeCodes"]="",
					["ProcTime"]=TimeSpan.Zero,["ProcTimeEnd"]=TimeSpan.Zero,["DateTStamp"]=dateEntry.AddHours(17),["StatementNum"]=proc.StatementNum,
					["BillingNote"]="",["SnomedBodySite"]="",["DiagnosticCode2"]="",["DiagnosticCode3"]="",["DiagnosticCode4"]="",["IcdVersion"]=10,
					["SecUserNumEntry"]=_world.AllProviders.FirstOrDefault(x => x.ProvNum==proc.ProvNum)?.UserNum??_world.UserNumAdmin,
					["SecDateEntry"]=dateEntry.AddHours(9),["DateComplete"]=isComplete ? proc.ProcDate : _minDate,
				});
				if(proc.Status==OdValues.ProcTP) {
					if(proc.Patient.ListCoverages.Count>0) {
						WriteEstimates(proc);
					}
					if(!dictTp.TryGetValue(proc.Patient,out List<ProcRecord> list)) {
						list=new List<ProcRecord>();
						dictTp[proc.Patient]=list;
					}
					list.Add(proc);
				}
			}
			foreach(KeyValuePair<Person,List<ProcRecord>> pair in dictTp) {
				long treatPlanNum=_sink.NextId("treatplan");
				DateTime dateTP=pair.Value.Min(x => x.DateTP.Year>1900 ? x.DateTP : AsOf);
				_sink.Insert("treatplan",new Row {
					["TreatPlanNum"]=treatPlanNum,["PatNum"]=pair.Key.PatNum,["DateTP"]=dateTP,["Heading"]="Active Treatment Plan",["Note"]="",
					["Signature"]="",["SigIsTopaz"]=false,["ResponsParty"]=0L,["DocNum"]=0L,["TPStatus"]=OdValues.TPActive,["SecUserNumEntry"]=pair.Key.Dentist.UserNum,
					["SecDateEntry"]=dateTP,["SecDateTEdit"]=dateTP.AddHours(14),["UserNumPresenter"]=pair.Key.Dentist.UserNum,["TPType"]=0,["SignaturePractice"]="",
					["SignatureText"]="",["SignaturePracticeText"]="",
				});
				foreach(ProcRecord proc in pair.Value) {
					_sink.Insert("treatplanattach",new Row { ["TreatPlanAttachNum"]=_sink.NextId("treatplanattach"),["TreatPlanNum"]=treatPlanNum,
						["ProcNum"]=proc.ProcNum,["Priority"]=0L });
				}
			}
		}

		///<summary>Monthly statements while the family owes money (25.2 ShowTransSinceBalZero, 25.3 API mode), plus the family's share of
		///the monthly A/R snapshot.</summary>
		private void WriteStatements() {
			DateTime start=_world.Settings.HistoryStart;
			List<LedgerEntry> listSorted=_listLedger.OrderBy(x => x.Date).ToList();
			DateTime datePrevious=_minDate;
			for(DateTime month=new DateTime(start.Year,start.Month,1).AddMonths(1);month<=AsOf;month=month.AddMonths(1)) {
				decimal balance=0;
				foreach(LedgerEntry entry in listSorted) {
					if(entry.Date>=month) {
						break;
					}
					balance+=entry.Amount;
				}
				decimal insEst=_listInsTimeline.Where(x => x.dateClaim<month && (x.dateReceived==null || x.dateReceived.Value>=month)).Sum(x => x.estimate);
				_world.AddDashboardAr(month,balance,insEst);
				if(balance<0.01m) {
					continue;
				}
				DateTime dateSent=month.AddDays(_rng.Between(0,3));
				//The showcase family's first statement went out through the API (StatementMode 4, 25.3).
				int mode=_isShowcase && datePrevious==_minDate ? OdValues.StmtApi : _statementMode;
				WriteStatementRow(_guarantor,dateSent,mode,isReceipt:false,isInvoice:false,balTotal:balance,insEst:insEst,singlePatient:false,
					dateRangeFrom:datePrevious);
				datePrevious=dateSent;
			}
		}

		private void WriteStatementRow(Person person,DateTime date,int mode,bool isReceipt,bool isInvoice,decimal balTotal,decimal insEst,bool singlePatient,
			long statementNum=0,DateTime? dateRangeFrom=null)
		{
			if(statementNum==0) {
				statementNum=_sink.NextId("statement");
			}
			bool isEmail=mode==OdValues.StmtEmail;
			_sink.Insert("statement",new Row {
				["StatementNum"]=statementNum,["PatNum"]=person.PatNum,["DateSent"]=date,["DateRangeFrom"]=dateRangeFrom??date,["DateRangeTo"]=date,
				["Note"]="",["NoteBold"]=!isReceipt && !isInvoice && balTotal>500 ? "Please call our office about your balance." : "",["Mode_"]=mode,
				["HidePayment"]=false,["SinglePatient"]=singlePatient,["Intermingled"]=!singlePatient,["IsSent"]=true,["DocNum"]=0L,["DateTStamp"]=date.AddHours(18),
				["IsReceipt"]=isReceipt,["IsInvoice"]=isInvoice,["IsInvoiceCopy"]=false,["EmailSubject"]=isEmail ? "Your statement from Fictional Family Dental" : "",
				["EmailBody"]=isEmail ? "Your statement is attached. Thank you." : "",["SuperFamily"]=0L,["IsBalValid"]=!isReceipt,["InsEst"]=insEst,
				["BalTotal"]=balTotal,["StatementType"]="NotSet",["ShortGUID"]="",["StatementShortURL"]="",["StatementURL"]="",["SmsSendStatus"]=0,
				["LimitedCustomFamily"]=false,
				["ShowTransSinceBalZero"]=!isReceipt && !isInvoice && date>=_dateShowTransSinceZero && (statementNum%3==0 || _isShowcase),
			});
			if(isEmail && !isReceipt && !isInvoice && person.Email!="" && Chance(0.25)) {
				//The e-mail that carried the statement. A few bounced; FailReason became text in 25.3.
				bool failed=_rng.Chance(0.08) || (_isShowcase && !_hasBouncedEmail);
				_hasBouncedEmail|=failed;
				DateTime sent=date.AddHours(19).AddMinutes(_rng.Next(60));
				_sink.Insert("emailmessage",new Row {
					["EmailMessageNum"]=_sink.NextId("emailmessage"),["PatNum"]=person.PatNum,["ToAddress"]=person.Email,["FromAddress"]="billing@example.com",
					["Subject"]="Your statement from Fictional Family Dental",["BodyText"]="Your statement is attached. Thank you.",["MsgDateTime"]=sent,
					["SentOrReceived"]=failed ? 17 : 1,["RecipientAddress"]=person.Email,["RawEmailIn"]="",["ProvNumWebMail"]=0L,["PatNumSubj"]=person.PatNum,
					["CcAddress"]="",["BccAddress"]="",["HideIn"]=0,["AptNum"]=0L,["UserNum"]=_world.UserNumAdmin,["HtmlType"]=0,["SecDateTEntry"]=sent,
					["SecDateTEdit"]=sent,["MsgType"]="Statement",["FailReason"]=failed ? "Recipient mailbox unavailable (550). Synthetic bounce." : "",
				});
			}
		}

		private void WritePatients() {
			AgingResult aging=Aging.Compute(_listLedger,AsOf);
			decimal familyInsEst=_listMembers.Sum(x => x.OwnInsEst);
			_dictPayPlanDue.TryGetValue(_guarantor.PatNum,out decimal payPlanDue);
			foreach(Person person in _listMembers) {
				decimal ownBalance=_listLedger.Where(x => x.Patient==person).Sum(x => x.Amount);
				bool isGuarantor=person.IsGuarantor;
				DateTime secDateEntry=person.DateFirstVisit.Year>1900 ? person.DateFirstVisit.AddDays(-_rng.Between(1,10)) : AsOf.AddDays(-_rng.Between(1,20));
				_world.ListPatients.Add((person.PatNum,_guarantor.PatNum,person.Status,person.Birthdate));
				_sink.Insert("patient",new Row {
					["PatNum"]=person.PatNum,["LName"]=person.LName,["FName"]=person.FName,["MiddleI"]=person.MiddleI,["Preferred"]=person.Preferred,
					["PatStatus"]=person.Status,["Gender"]=person.Gender,["Position"]=person.Position,["Birthdate"]=person.Birthdate,["SSN"]="",
					["Address"]=_familyAddress,["Address2"]="",["City"]=_familyCity,["State"]="WA",["Zip"]=_familyZip,["HmPhone"]=_familyPhone,
					["WkPhone"]="",["WirelessPhone"]=person.WirelessPhone,["Guarantor"]=_guarantor.PatNum,["CreditType"]="",["Email"]=person.Email,
					["Salutation"]="",["EstBalance"]=Math.Round(ownBalance-person.OwnInsEst,2),["PriProv"]=person.Dentist.ProvNum,
					["SecProv"]=person.Hygienist.ProvNum,["FeeSched"]=0L,["BillingType"]=_billingType,["ImageFolder"]=(person.LName+person.FName+person.PatNum),
					["AddrNote"]="",["FamFinUrgNote"]=isGuarantor && _payerKind==PayerKind.NonPayer ? "Collections: call before scheduling." : "",
					["MedUrgNote"]=_rng.Chance(0.05) ? "Premedicate before treatment" : "",["ApptModNote"]="",["StudentStatus"]="",["SchoolName"]="",
					["ChartNumber"]="",["MedicaidID"]="",
					["Bal_0_30"]=isGuarantor ? aging.Bal0To30 : 0m,["Bal_31_60"]=isGuarantor ? aging.Bal31To60 : 0m,["Bal_61_90"]=isGuarantor ? aging.Bal61To90 : 0m,
					["BalOver90"]=isGuarantor ? aging.BalOver90 : 0m,["InsEst"]=isGuarantor ? familyInsEst : 0m,["BalTotal"]=isGuarantor ? aging.Total : 0m,
					["EmployerNum"]=person.ListCoverages.FirstOrDefault()?.Plan.EmployerNum??0,["EmploymentNote"]="",["County"]="",
					["DateFirstVisit"]=person.DateFirstVisit.Year>1900 && person.DateFirstVisit<=AsOf ? person.DateFirstVisit : _minDate,
					["ClinicNum"]=_world.ClinicNum,["HasIns"]=person.ListCoverages.Count>0 ? "I" : "",["TrophyFolder"]="",["PlannedIsDone"]=false,
					["Premed"]=false,["Ward"]="",["PreferConfirmMethod"]=person.WirelessPhone!="" ? 8 : 0,["PreferContactMethod"]=person.Email!="" ? 5 : 2,
					["PreferRecallMethod"]=person.WirelessPhone!="" ? 8 : 0,["SchedDayOfWeek"]=0,["Language"]="",["Title"]="",
					["PayPlanDue"]=isGuarantor ? payPlanDue : 0m,["SiteNum"]=0L,["DateTStamp"]=AsOf.AddHours(18),["ResponsParty"]=0L,
					["SuperFamily"]=0L,["TxtMsgOk"]=person.WirelessPhone!="" ? 1 : 0,["SmokingSnoMed"]="",["Country"]="",
					["DateTimeDeceased"]=person.DateTimeDeceased??_minDate,["BillingCycleDay"]=1,["SecUserNumEntry"]=_rng.Pick(_world.ListFrontDeskUsers),
					["SecDateEntry"]=secDateEntry,["HasSuperBilling"]=false,["PatNumCloneFrom"]=0L,["DiscountPlanNum"]=0L,["HasSignedTil"]=false,
					["ShortCodeOptIn"]=0,["SecurityHash"]=secDateEntry.Year>=2025 ? World.PseudoHash("patient"+person.PatNum) : "",
				});
				if(_rng.Chance(0.2)) {
					AddSecurityLog(OdValues.PermPatientEdit,_rng.Pick(_world.ListFrontDeskUsers),secDateEntry.AddHours(10),person.PatNum,person.PatNum,
						"Patient information edited");
				}
			}
		}
		#endregion Writing

		private static DateTime Max(DateTime a,DateTime b) {
			return a>b ? a : b;
		}
	}
}
