// The recurring charge run's part of the failure e-mail (Open Dental 25.4, item v25_4-account-recurringcharge-fail-email, package M2-RECUR in
// docs/specs/port-backlog.md). The rules are in RecurringChargeFailEmails.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;

namespace OpenPmsDentalBusiness {
	public partial class RecurringChargerator {
		///<summary>Failure e-mails sent in this run.</summary>
		public int FailEmailsSent { get; private set; }
		///<summary>Declined charges in this run whose failure e-mail could not be sent (no address, opted out, send error, and so on).</summary>
		public int FailEmailsNotSent { get; private set; }
		///<summary>Declined charges in this run whose card was already e-mailed within the quiet period.</summary>
		public int FailEmailsSkipped { get; private set; }
		///<summary>Sends and saves one failure e-mail, and throws when sending fails. Null uses EmailMessages.SendEmail. Tests set it.</summary>
		public Action<EmailMessage,EmailAddress> ActionSendFailEmail;

		///<summary>Called by SendCharges for each card after it was processed and before its recurringcharge row is saved, so the line this adds to the
		///row's ErrorMsg is saved with it. Sends the failure e-mail when the charge was declined and the office turned the e-mail on. Never throws.</summary>
		public RecurringChargeFailEmailResult SendFailEmailIfNeeded(RecurringChargeData chargeData) {
			RecurringChargeFailEmailResult result=RecurringChargeFailEmails.SendIfNeeded(chargeData,_nowDateTime,ActionSendFailEmail);
			switch(result.Status) {
				case RecurringChargeFailEmailStatus.Sent:
					FailEmailsSent++;
					break;
				case RecurringChargeFailEmailStatus.NotSent:
					FailEmailsNotSent++;
					//Information, not Error: the manual run shows every Error in a message box, and the history window has the reason.
					_log.WriteLine(Lans.g(_lanThis,"Failure e-mail not sent")+": "+result.Reason+"\r\n  "+Lans.g(_lanThis,"Patient:")+" "+chargeData.PatName,
						LogLevel.Information);
					break;
				case RecurringChargeFailEmailStatus.SkippedQuietPeriod:
					FailEmailsSkipped++;
					break;
			}
			return result;
		}

		///<summary>The start of each message PaySimple.PaySimpleApi.ToSaleResponse throws when PaySimple answered a charge with something other than
		///approved. M2RECURTests runs charges through ToSaleResponse to keep these in step with it.</summary>
		private static readonly string[] _arrayPaySimpleDeclineStarts=new string[] {
			"Payment was declined by PaySimple.",
			"Payment was not approved by PaySimple.",
		};

		///<summary>For SendPaySimple's catch, which marks every exception declined (Open Dental's behaviour, kept for "inactivate declined cards").
		///True only when PaySimple answered the charge and did not approve it: the plain Exception that PaySimpleApi.ToSaleResponse throws, also when
		///the charge was found by its OrderId after a lost answer. False for everything else, so none of it e-mails the patient: any ODException
		///(PaySimpleResultUnknownException, whose charge may have gone through; missing credentials or PayType, a disabled program, rejected
		///credentials), PaySimple's error objects (PaySimpleException), HTTP and network errors, and errors after the card was charged.</summary>
		public static bool IsPaySimpleDecline(Exception ex) {
			if(ex==null || ex is ODException || ex.GetType()!=typeof(Exception) || ex.InnerException!=null) {
				return false;
			}
			string message=ex.Message??"";
			return _arrayPaySimpleDeclineStarts.Any(x => message.StartsWith(x,StringComparison.Ordinal));
		}

		///<summary>For SendEdgeExpress and the XWeb gateway path (ProcessCardXWebDTG), which mark every answer other than an approval declined (Open
		///Dental's behaviour, kept for "inactivate declined cards"). EdgeExpress and X-Web share these response codes. True only for an answer about
		///the card itself: Declined (001), AutoDecline (009) and InvalidExpirationDate (010). False for everything else, so none of it e-mails the
		///patient: the office's setup and credential errors (800-812, among them 805 AuthenticationError and the 806-808 merchant mode errors), a
		///duplicate (813: the card was already charged), the settlement, maintenance and outage answers (818-821), a partial approval (007: the card
		///was charged), Pending, and a code this program does not know (Undefined).</summary>
		public static bool IsEdgeExpressCardDecline(XWebResponseCodes xWebResponseCode) {
			return xWebResponseCode==XWebResponseCodes.Declined
				|| xWebResponseCode==XWebResponseCodes.AutoDecline
				|| xWebResponseCode==XWebResponseCodes.InvalidExpirationDate;
		}

		///<summary>For ProcessCardXWebDTG's catch, which marks an exception declined when its message contains "decline". The gateway's answer other
		///than an approval arrives as the ODException XWebs throws for it (ErrorCodes.XWebDTGFailed, "DTG failed (code): description"). True only
		///when that code is a card decline (IsEdgeExpressCardDecline). False for any other exception, whatever its text says.</summary>
		public static bool IsXWebDtgCardDecline(Exception ex) {
			ODException odException=ex as ODException;
			if(odException==null || odException.ErrorCodeAsEnum!=ODException.ErrorCodes.XWebDTGFailed) {
				return false;
			}
			Match match=Regex.Match(odException.Message??"",@"^DTG failed \(([0-9]{1,4})\)");
			if(!match.Success) {
				return false;
			}
			int responseCode=int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);
			return IsEdgeExpressCardDecline(XWebResponse.ConvertResponseCode(responseCode));
		}

		///<summary>For ProcessCardXChargeClientProgram, which marks every result other than SUCCESS declined: also "Transaction not completed", any
		///other result, and a result file with no RESULT line. resultText is the result file's lines as the charge's note keeps them. True only when
		///the RESULT line itself says declined (it contains "DECLINE", ignoring case). INFERRED: X-Charge's result values are not published.</summary>
		public static bool IsXChargeClientDecline(string resultText) {
			if(string.IsNullOrEmpty(resultText)) {
				return false;
			}
			string[] arrayLines=resultText.Split(new[] { "\r\n","\n" },StringSplitOptions.None);
			string lineResult=arrayLines.FirstOrDefault(x => x.StartsWith("RESULT=",StringComparison.Ordinal));
			if(lineResult==null) {
				return false;
			}
			return lineResult.Substring("RESULT=".Length).IndexOf("DECLINE",StringComparison.OrdinalIgnoreCase)>=0;
		}

		///<summary>For SendPayConnectV2, which marks every error answer PayConnect sent declined. True only when PayConnect turned the card down: its
		///own error (PayConnectErrorKind.Vendor) with an HTTP 4xx status other than 408 (timeout), 409 (conflict, which may be a duplicate of a charge
		///that went through) and 429 (too many requests); or a lost answer that PayConnect's transaction list shows as declined (Outcome Reconciled,
		///a status that says declined). False for a rejected API key or Secret (401, 403), a validation error (400 VALIDATION), any 5xx, missing
		///settings and no answer, so none of them e-mails the patient. INFERRED: which statuses PayConnect uses for a decline is not published.</summary>
		public static bool IsPayConnect2Decline(PayConnect2.PayConnect2Response response) {
			PayConnect2.ErrorResponse errorResponse=response?.ErrorResponse;
			if(response==null || response.ResponseType!=PayConnect2.ResponseType.Error || errorResponse==null
				|| errorResponse.Kind!=PayConnect2.PayConnectErrorKind.Vendor)
			{
				return false;
			}
			if(response.Outcome==PayConnect2.PayConnectRequestOutcome.Reconciled) {
				return (errorResponse.VendorCode??"").IndexOf("declin",StringComparison.OrdinalIgnoreCase)>=0;
			}
			if(response.Outcome!=PayConnect2.PayConnectRequestOutcome.Response) {
				return false;
			}
			int httpStatus=(errorResponse.HttpStatus>0) ? errorResponse.HttpStatus : (int)response.httpStatusCode;
			return httpStatus>=400 && httpStatus<500 && httpStatus!=408 && httpStatus!=409 && httpStatus!=429;
		}

		///<summary>Called by ClearStats at the start of a run.</summary>
		private void ClearFailEmailStats() {
			FailEmailsSent=0;
			FailEmailsNotSent=0;
			FailEmailsSkipped=0;
		}

		///<summary>The failure e-mail counts of the last run, for the summary shown when the run is done. Blank when the e-mail is turned off.</summary>
		public string GetFailEmailSummary() {
			if(!RecurringChargeFailEmails.IsEnabled()) {
				return "";
			}
			List<string> listLines=new List<string>();
			listLines.Add(Lans.g(_lanThis,"Failure e-mails sent")+": "+FailEmailsSent);
			listLines.Add(Lans.g(_lanThis,"Failure e-mails not sent")+": "+FailEmailsNotSent);
			if(FailEmailsSkipped>0) {
				listLines.Add(Lans.g(_lanThis,"Not e-mailed again (card e-mailed in the last")+" "+RecurringChargeFailEmails.QUIET_PERIOD_DAYS+" "
					+Lans.g(_lanThis,"days")+"): "+FailEmailsSkipped);
			}
			return string.Join("\r\n",listLines);
		}
	}
}
