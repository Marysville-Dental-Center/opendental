// Global Payments V2 (Open Dental 25.4): recurring charges on cards saved through Global Payments V2. A partial of RecurringChargerator
// (OpenDentBusiness/Data Interface/RecurringCharges.cs), which calls IsGlobalPaymentsV2Charge and SendGlobalPaymentsV2 from SendCharges.
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

		///<summary>Charges one recurring charge on a card saved through Global Payments V2 (Transactions API, card not present, the stored payment method).
		///The same card is not charged the same amount twice on one day unless duplicates are forced (the window's box, or for automated runs the clinic's
		///"Recurring charges force duplicates by default"). The request carries an idempotency key per charge row and day, and is never retried.
		///An approved or declined charge gets a payment (a declined one for $0, as the other processors do); a charge whose outcome is unknown gets none
		///and is marked failed, so it is never charged again automatically.</summary>
		private void SendGlobalPaymentsV2(RecurringChargeData chargeData,bool forceDuplicates) {
			_progCur=Programs.GetCur(ProgramName.MeetInTheCloud);
			RecurringCharge recurringCharge=chargeData.RecurringCharge;
			GlobalPaymentsV2Config config=GlobalPaymentsV2.GetConfig(recurringCharge.ClinicNum);
			bool doForceDuplicates=forceDuplicates || (!_isManual && config.ForceDuplicates);
			Patient patient=Patients.GetPat(recurringCharge.PatNum);
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
			if(!doForceDuplicates && GlobalPaymentsV2.HasSameRecurringChargeOnDate(recurringCharge.CreditCardNum,amount,_nowDateTime.Date,
				recurringCharge.RecurringChargeNum))
			{
				MarkFailed(chargeData,Lans.g(_lanThis,"Global Payments V2: this card was already charged the same amount today, so it was not charged again. "
					+"Check Force Duplicates to charge it anyway."),LogLevel.Information);
				return;
			}
			CreditCard creditCard=new CreditCard();
			creditCard.CreditCardNum=recurringCharge.CreditCardNum;
			creditCard.XChargeToken=chargeData.XChargeToken;
			creditCard.CCNumberMasked=chargeData.CCNumberMasked;
			creditCard.CCSource=chargeData.CCSource;
			string reference=GlobalPaymentsV2.GetRecurringReference(recurringCharge.RecurringChargeNum,_nowDateTime);
			GpV2Transactions gpV2Transactions=new GpV2Transactions(GlobalPaymentsV2.GetClient());
			GlobalPaymentsV2Result result=VendorHttp.RunSync(() => gpV2Transactions.RunSavedCardAsync(config,creditCard,GpV2TransType.Sale,(decimal)amount,
				reference,isRecurring:true,CancellationToken.None));
			string note=Lans.g(_lanThis,_isManual ? "Made from manually ran recurring charge" : "Made from automated recurring charge")+"\r\n"
				+GlobalPaymentsV2.BuildPayNote(result);
			SecurityLogs.MakeLogEntry(EnumPermType.PaymentCreate,patient.PatNum,GlobalPaymentsV2.BuildLogText(result)+" ("
				+Lans.g(_lanThis,"recurring charge")+")");
			switch(result.Outcome) {
				case GpV2Outcome.Approved:
					recurringCharge.ChargeStatus=RecurringChargeStatus.ChargeSuccessful;
					Success++;
					string receipt=GlobalPaymentsV2.BuildReceipt(result,CreditCardUtils.AddClinicToReceipt(recurringCharge.ClinicNum),patient.GetNameFLnoPref(),
						_nowDateTime);
					CreatePayment(patient,chargeData,note,(double)result.Amount,receipt,CreditCardSource.MeetInTheCloudTerminal,externalId:result.TransactionId);
					return;
				case GpV2Outcome.Declined:
					MarkDeclined(chargeData,Lans.g(_lanThis,"Response from Global Payments V2:")+" "+GlobalPaymentsV2.GetOutcomeText(result.Outcome)
						+(result.Message=="" ? "" : " ("+result.Message+")"),LogLevel.Information);
					//A declined charge still gets a $0 payment, so there is a record that the charge was tried (as for the other processors).
					CreatePayment(patient,chargeData,note,0,"",CreditCardSource.MeetInTheCloudTerminal);
					return;
				case GpV2Outcome.Failed:
					MarkFailed(chargeData,Lans.g(_lanThis,"Error from Global Payments V2:")+" "+result.Message);
					return;
				default:
					MarkFailed(chargeData,Lans.g(_lanThis,"Error from Global Payments V2:")+" "+result.Message+" "
						+Lans.g(_lanThis,"Check the card processor's records before charging this card again."));
					return;
			}
		}
	}
}
