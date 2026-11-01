// Global Payments V2 (Open Dental 25.4): recurring charges on cards saved through Global Payments V2. A partial of RecurringChargerator
// (OpenDentBusiness/Data Interface/RecurringCharges.cs), which calls IsGlobalPaymentsV2Charge and SendGlobalPaymentsV2 from SendCharges and
// GetGlobalPaymentsV2PayType from CreatePayment. The Recurring Charges window calls GetGlobalPaymentsV2InDoubt and
// BuildGlobalPaymentsV2InDoubtPrompt before a run, and fills SetRecurringChargeNumsRetryInDoubt only when the user answers Yes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness.VendorDirect.GlobalPayments;

namespace OpenDentBusiness {
	public partial class RecurringChargerator {
		///<summary>The Global Payments V2 recurring charge rows in doubt (GlobalPaymentsV2.GetRecurringChargeInDoubt) that a person has said may be charged
		///again, after checking Global Payments' records: the RecurringChargeNum of each confirmed attempt. Only the Recurring Charges window fills it,
		///from its own Yes/No prompt (default No), for one run. A card whose attempt in doubt is not in this set is not charged, whatever Force Duplicates
		///or the clinic's "Recurring charges force duplicates by default" say, and automated runs never retry one. Keyed on the attempt, not the card: if
		///the retry itself ends in doubt, that new attempt needs a new confirmation.</summary>
		public HashSet<long> SetRecurringChargeNumsRetryInDoubt=new HashSet<long>();

		///<summary>True for a card saved through Global Payments V2 (CCSource MeetInTheCloudTerminal with a stored payment method) while the program link
		///is enabled. Such cards are charged by SendGlobalPaymentsV2, never by another processor.</summary>
		private bool IsGlobalPaymentsV2Charge(RecurringChargeData chargeData) {
			return IsGlobalPaymentsV2Card(chargeData) && GlobalPaymentsV2.IsEnabled();
		}

		///<summary>True for a card saved through Global Payments V2 (CCSource MeetInTheCloudTerminal with a stored payment method).</summary>
		private static bool IsGlobalPaymentsV2Card(RecurringChargeData chargeData) {
			return chargeData!=null
				&& chargeData.RecurringCharge!=null
				&& chargeData.CCSource==CreditCardSource.MeetInTheCloudTerminal
				&& !string.IsNullOrWhiteSpace(chargeData.XChargeToken);
		}

		///<summary>A Global Payments V2 card about to be charged whose earlier recurring charge has an unknown outcome.</summary>
		public class GlobalPaymentsV2InDoubt {
			///<summary>The charge about to be sent.</summary>
			public RecurringChargeData RecurringChargeData;
			///<summary>The earlier attempt whose outcome is unknown (GlobalPaymentsV2.GetRecurringChargeInDoubt).</summary>
			public RecurringCharge RecurringChargeInDoubt;

			///<summary>The earlier attempt's Global Payments reference, to look it up in Global Payments' records, or "".</summary>
			public string Reference {
				get {
					return GlobalPaymentsV2.GetReferenceFromInDoubtTag(RecurringChargeInDoubt?.ErrorMsg);
				}
			}
		}

		///<summary>The Global Payments V2 cards in listRecurringChargeData whose earlier recurring charge has an unknown outcome, each with that attempt, in
		///the list's order. Empty when Global Payments V2 is not enabled. One query for all the cards (GlobalPaymentsV2.GetRecurringChargesInDoubt).
		///</summary>
		public static List<GlobalPaymentsV2InDoubt> GetGlobalPaymentsV2InDoubt(List<RecurringChargeData> listRecurringChargeData) {
			List<GlobalPaymentsV2InDoubt> listInDoubt=new List<GlobalPaymentsV2InDoubt>();
			if(listRecurringChargeData==null || !GlobalPaymentsV2.IsEnabled()) {
				return listInDoubt;
			}
			List<RecurringChargeData> listV2=listRecurringChargeData.FindAll(x => IsGlobalPaymentsV2Card(x));
			if(listV2.Count==0) {
				return listInDoubt;
			}
			Dictionary<long,RecurringCharge> dictInDoubt=GlobalPaymentsV2.GetRecurringChargesInDoubt(
				listV2.Select(x => x.RecurringCharge.CreditCardNum).Distinct().ToList()).ToDictionary(x => x.CreditCardNum);
			for(int i=0;i<listV2.Count;i++) {
				if(dictInDoubt.TryGetValue(listV2[i].RecurringCharge.CreditCardNum,out RecurringCharge recurringChargeInDoubt)) {
					listInDoubt.Add(new GlobalPaymentsV2InDoubt { RecurringChargeData=listV2[i],RecurringChargeInDoubt=recurringChargeInDoubt });
				}
			}
			return listInDoubt;
		}

		///<summary>The Recurring Charges window's question before a run that includes Global Payments V2 cards whose earlier charge has an unknown
		///outcome: patient, date, amount and reference of each earlier attempt (at most 20 listed), then whether to charge them again. Never the card
		///token.</summary>
		public static string BuildGlobalPaymentsV2InDoubtPrompt(List<GlobalPaymentsV2InDoubt> listInDoubt) {
			const int countListedMax=20;
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lans.g(_lanThis,"Global Payments V2: an earlier recurring charge on these cards has an unknown outcome. Global Payments may "
				+"already have taken the money."));
			stringBuilder.AppendLine();
			for(int i=0;i<listInDoubt.Count && i<countListedMax;i++) {
				GlobalPaymentsV2InDoubt inDoubt=listInDoubt[i];
				string reference=inDoubt.Reference;
				stringBuilder.AppendLine(inDoubt.RecurringChargeData.PatName+": "+inDoubt.RecurringChargeInDoubt.DateTimeCharge.ToString()+", "
					+inDoubt.RecurringChargeInDoubt.ChargeAmt.ToString("c")+(reference=="" ? "" : ", "+Lans.g(_lanThis,"reference")+" "+reference));
			}
			if(listInDoubt.Count>countListedMax) {
				stringBuilder.AppendLine(Lans.g(_lanThis,"and")+" "+(listInDoubt.Count-countListedMax)+" "+Lans.g(_lanThis,"more"));
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(Lans.g(_lanThis,"Have you checked Global Payments' records and found that these charges were not taken?"));
			stringBuilder.AppendLine(Lans.g(_lanThis,"Yes charges these cards again. No leaves them out of this run (the other selected cards are still charged)."));
			stringBuilder.Append(Lans.g(_lanThis,"Charge these cards again?"));
			return stringBuilder.ToString();
		}

		///<summary>The payment type of a recurring Global Payments V2 payment when neither the RecurringChargesPayTypeCC preference nor the card sets one:
		///the clinic's own setting, else Headquarters', exactly as the payment window reads it (GlobalPaymentsV2.GetConfig). The setup window writes a
		///clinic row only when the clinic's value differs from Headquarters', so the clinic's own row alone is not enough.</summary>
		private static long GetGlobalPaymentsV2PayType(long clinicNum) {
			return GlobalPaymentsV2.GetConfig(clinicNum).PaymentTypeDefNum;
		}

		///<summary>Charges one recurring charge on a card saved through Global Payments V2 (Transactions API, card not present, the stored payment method).
		///Never throws: every problem marks this card and the batch carries on.
		///- An earlier attempt on this card whose outcome is unknown (GlobalPaymentsV2.GetRecurringChargeInDoubt) blocks the card in every later run,
		///  automated or manual, until Global Payments gives a definite answer on the card again. Only a manual run in which a person confirmed that
		///  attempt in the Recurring Charges window's own prompt (SetRecurringChargeNumsRetryInDoubt) retries it; Force Duplicates never does, since the
		///  window pre-checks it from the clinic's setting and offices check it for unrelated reasons. Within 24 hours of that attempt the retry reuses
		///  its reference and idempotency key, so Global Payments can return the first answer instead of charging again.
		///- The same card is not charged the same amount twice on one day unless duplicates are forced (the window's box, or for automated runs the
		///  clinic's "Recurring charges force duplicates by default").
		///- The request is sent once and never retried. An approved or declined charge gets a payment (a declined one for $0, as the other processors
		///  do); a charge whose outcome is unknown gets none and is tagged in doubt (GlobalPaymentsV2.RecurringInDoubtTag).</summary>
		private void SendGlobalPaymentsV2(RecurringChargeData chargeData,bool forceDuplicates) {
			_progCur=Programs.GetCur(ProgramName.MeetInTheCloud);
			RecurringCharge recurringCharge=chargeData.RecurringCharge;
			GlobalPaymentsV2Config config=GlobalPaymentsV2.GetConfig(recurringCharge.ClinicNum);
			bool doForceDuplicates=forceDuplicates || (!_isManual && config.ForceDuplicates);
			Patient patient;
			try {
				patient=Patients.GetPat(recurringCharge.PatNum);
			}
			catch(Exception ex) {
				MarkFailed(chargeData,Lans.g(_lanThis,"Global Payments V2: the card was not charged.")+" "+ex.Message);
				return;
			}
			if(patient==null) {
				MarkFailed(chargeData,Lans.g(_lanThis,"Unable to find patient")+" "+recurringCharge.PatNum);
				return;
			}
			if(!config.IsComplete) {
				MarkFailed(chargeData,Lans.g(_lanThis,"Global Payments V2 is not set up for this clinic. Enter the App ID and App Key in Setup > Program Links >")
					+" "+GlobalPaymentsV2.DisplayName+".");
				return;
			}
			double amount=recurringCharge.ChargeAmt;
			RecurringCharge recurringChargeInDoubt;
			bool isDuplicate;
			try {
				recurringChargeInDoubt=GlobalPaymentsV2.GetRecurringChargeInDoubt(recurringCharge.CreditCardNum);
				isDuplicate=!doForceDuplicates && GlobalPaymentsV2.HasSameRecurringChargeOnDate(recurringCharge.CreditCardNum,amount,_nowDateTime.Date,
					recurringCharge.RecurringChargeNum);
			}
			catch(Exception ex) {
				//Nothing was sent.
				MarkFailed(chargeData,Lans.g(_lanThis,"Global Payments V2: this card's earlier charges could not be checked, so it was not charged.")+" "
					+ex.Message);
				return;
			}
			string referenceInDoubt=GlobalPaymentsV2.GetReferenceFromInDoubtTag(recurringChargeInDoubt?.ErrorMsg);
			//Retrying an attempt in doubt needs a person who has checked Global Payments' records and said so for this attempt: never an automated run,
			//never Force Duplicates (pre-checked from the clinic's default), never a confirmation of an earlier attempt.
			bool canRetryInDoubt=_isManual && recurringChargeInDoubt!=null && SetRecurringChargeNumsRetryInDoubt!=null
				&& SetRecurringChargeNumsRetryInDoubt.Contains(recurringChargeInDoubt.RecurringChargeNum);
			if(recurringChargeInDoubt!=null && !canRetryInDoubt) {
				//No in-doubt tag here: nothing was sent, and the earlier attempt stays the one to check.
				MarkFailed(chargeData,Lans.g(_lanThis,"Global Payments V2: an earlier charge on this card has an unknown outcome, so it was not charged again.")
					+" "+Lans.g(_lanThis,"Earlier charge:")+" "+recurringChargeInDoubt.DateTimeCharge.ToString()+", "
					+recurringChargeInDoubt.ChargeAmt.ToString("c")+(referenceInDoubt=="" ? "" : ", "+Lans.g(_lanThis,"reference")+" "+referenceInDoubt)+". "
					+Lans.g(_lanThis,"Check Global Payments' records. If that charge was not taken, send this card again from the Recurring Charges window and "
					+"answer Yes when it asks whether to charge cards with an unknown outcome again."));
				return;
			}
			if(canRetryInDoubt) {
				//One confirmation, one retry: if this retry also ends in doubt, its own attempt needs a new confirmation.
				SetRecurringChargeNumsRetryInDoubt.Remove(recurringChargeInDoubt.RecurringChargeNum);
			}
			if(isDuplicate) {
				MarkFailed(chargeData,Lans.g(_lanThis,"Global Payments V2: this card was already charged the same amount today, so it was not charged again. "
					+"Check Force Duplicates to charge it anyway."),LogLevel.Information);
				return;
			}
			string reference=GlobalPaymentsV2.GetRecurringReference(recurringCharge.RecurringChargeNum,_nowDateTime);
			if(recurringChargeInDoubt!=null && referenceInDoubt!=""
				&& GlobalPaymentsV2.ToMinorUnits((decimal)recurringChargeInDoubt.ChargeAmt)==GlobalPaymentsV2.ToMinorUnits((decimal)amount)
				&& recurringChargeInDoubt.DateTimeCharge>_nowDateTime.AddHours(-24))
			{
				//The same request again: if the first one reached Global Payments, its answer comes back instead of a second charge.
				reference=referenceInDoubt;
			}
			CreditCard creditCard=new CreditCard();
			creditCard.CreditCardNum=recurringCharge.CreditCardNum;
			creditCard.XChargeToken=chargeData.XChargeToken;
			creditCard.CCNumberMasked=chargeData.CCNumberMasked;
			creditCard.CCSource=chargeData.CCSource;
			GlobalPaymentsV2Result result;
			try {
				GpV2Transactions gpV2Transactions=new GpV2Transactions(GlobalPaymentsV2.GetClient());
				result=VendorHttp.RunSync(() => gpV2Transactions.RunSavedCardAsync(config,creditCard,GpV2TransType.Sale,(decimal)amount,reference,
					isRecurring:true,CancellationToken.None));
			}
			catch(Exception ex) {
				//Not a Global Payments answer (those become results): something in this program. The request may have been sent.
				result=new GlobalPaymentsV2Result();
				result.TransType=GpV2TransType.Sale;
				result.Amount=(decimal)amount;
				result.IsSandbox=config.UseSandbox;
				result.Outcome=GpV2Outcome.Unknown;
				result.Message=ex.Message;
			}
			string note=Lans.g(_lanThis,_isManual ? "Made from manually ran recurring charge" : "Made from automated recurring charge")+"\r\n"
				+GlobalPaymentsV2.BuildPayNote(result);
			try {
				SecurityLogs.MakeLogEntry(EnumPermType.PaymentCreate,patient.PatNum,GlobalPaymentsV2.BuildLogText(result)+" ("
					+Lans.g(_lanThis,"recurring charge")+")");
			}
			catch(Exception ex) {
				ex.DoNothing();//The charge is recorded below either way.
			}
			switch(result.Outcome) {
				case GpV2Outcome.Approved:
					recurringCharge.ChargeStatus=RecurringChargeStatus.ChargeSuccessful;
					Success++;
					try {
						string receipt=GlobalPaymentsV2.BuildReceipt(result,CreditCardUtils.AddClinicToReceipt(recurringCharge.ClinicNum),
							patient.GetNameFLnoPref(),_nowDateTime);
						CreatePayment(patient,chargeData,note,(double)result.Amount,receipt,CreditCardSource.MeetInTheCloudTerminal,externalId:result.TransactionId);
					}
					catch(Exception ex) {
						if(recurringCharge.PayNum>0) {
							//The payment row was saved, so the charge is on the account; only part of what follows it failed.
							recurringCharge.ErrorMsg=StringTools.AppendLine(recurringCharge.ErrorMsg,Lans.g(_lanThis,"Global Payments V2: the payment was saved, but:")
								+" "+ex.Message);
							return;
						}
						//Charged, but nothing on the account says so: never charge this card again automatically.
						recurringCharge.ChargeStatus=RecurringChargeStatus.NotYetCharged;
						Success--;
						MarkFailed(chargeData,Lans.g(_lanThis,"Global Payments V2 approved this charge (transaction")+" "+result.TransactionId+"), "
							+Lans.g(_lanThis,"but the payment could not be saved. Enter the payment by hand.")+" "+ex.Message+" "
							+GlobalPaymentsV2.BuildRecurringInDoubtTag(reference));
					}
					return;
				case GpV2Outcome.Declined:
					MarkDeclined(chargeData,Lans.g(_lanThis,"Response from Global Payments V2:")+" "+GlobalPaymentsV2.GetOutcomeText(result.Outcome)
						+(result.Message=="" ? "" : " ("+result.Message+")"),LogLevel.Information);
					try {
						//A declined charge still gets a $0 payment, so there is a record that the charge was tried (as for the other processors).
						CreatePayment(patient,chargeData,note,0,"",CreditCardSource.MeetInTheCloudTerminal);
					}
					catch(Exception ex) {
						recurringCharge.ErrorMsg=StringTools.AppendLine(recurringCharge.ErrorMsg,ex.Message);//No money moved.
					}
					return;
				case GpV2Outcome.Failed:
					//Global Payments refused the request: no money moved.
					MarkFailed(chargeData,Lans.g(_lanThis,"Error from Global Payments V2:")+" "+result.Message);
					return;
				default:
					MarkFailed(chargeData,Lans.g(_lanThis,"Error from Global Payments V2:")+" "+result.Message+" "
						+Lans.g(_lanThis,"Check the card processor's records before charging this card again. It is not charged again until then.")+" "
						+GlobalPaymentsV2.BuildRecurringInDoubtTag(reference));
					return;
			}
		}
	}
}
