// Global Payments V2 (Open Dental 25.4): recurring charges on cards saved through Global Payments V2. A partial of RecurringChargerator
// (OpenDentBusiness/Data Interface/RecurringCharges.cs), which calls IsGlobalPaymentsV2Charge and SendGlobalPaymentsV2 from SendCharges and
// GetGlobalPaymentsV2PayType from CreatePayment.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Threading;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness.VendorDirect.GlobalPayments;

namespace OpenDentBusiness {
	public partial class RecurringChargerator {
		///<summary>True for a card saved through Global Payments V2 (CCSource MeetInTheCloudTerminal with a stored payment method) while the program link
		///is enabled. Such cards are charged by SendGlobalPaymentsV2, never by another processor.</summary>
		private bool IsGlobalPaymentsV2Charge(RecurringChargeData chargeData) {
			return chargeData.CCSource==CreditCardSource.MeetInTheCloudTerminal
				&& !string.IsNullOrWhiteSpace(chargeData.XChargeToken)
				&& GlobalPaymentsV2.IsEnabled();
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
		///  automated or manual, until Global Payments gives a definite answer on the card again. Only a manual run with Force Duplicates checked
		///  retries it, and within 24 hours of that attempt the retry reuses its reference and idempotency key, so Global Payments can return the first
		///  answer instead of charging again.
		///- The same card is not charged the same amount twice on one day unless duplicates are forced (the window's box, or for automated runs the
		///  clinic's "Recurring charges force duplicates by default").
		///- The request is sent once and never retried. An approved or declined charge gets a payment (a declined one for $0, as the other processors
		///  do); a charge whose outcome is unknown gets none and is tagged in doubt (GlobalPaymentsV2.RecurringInDoubtTag).</summary>
		private void SendGlobalPaymentsV2(RecurringChargeData chargeData,bool forceDuplicates) {
			_progCur=Programs.GetCur(ProgramName.MeetInTheCloud);
			RecurringCharge recurringCharge=chargeData.RecurringCharge;
			GlobalPaymentsV2Config config=GlobalPaymentsV2.GetConfig(recurringCharge.ClinicNum);
			bool doForceDuplicates=forceDuplicates || (!_isManual && config.ForceDuplicates);
			//Retrying an attempt in doubt needs a person who has checked Global Payments' records: never an automated run, never a clinic default.
			bool canRetryInDoubt=_isManual && forceDuplicates;
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
			if(recurringChargeInDoubt!=null && !canRetryInDoubt) {
				//No in-doubt tag here: nothing was sent, and the earlier attempt stays the one to check.
				MarkFailed(chargeData,Lans.g(_lanThis,"Global Payments V2: an earlier charge on this card has an unknown outcome, so it was not charged again.")
					+" "+Lans.g(_lanThis,"Earlier charge:")+" "+recurringChargeInDoubt.DateTimeCharge.ToString()+", "
					+recurringChargeInDoubt.ChargeAmt.ToString("c")+(referenceInDoubt=="" ? "" : ", "+Lans.g(_lanThis,"reference")+" "+referenceInDoubt)+". "
					+Lans.g(_lanThis,"Check Global Payments' records. If that charge was not taken, run this card from the Recurring Charges window with Force "
					+"Duplicates checked."));
				return;
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
