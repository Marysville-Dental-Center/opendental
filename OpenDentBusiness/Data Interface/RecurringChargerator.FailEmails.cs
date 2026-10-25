// The recurring charge run's part of the failure e-mail (Open Dental 25.4, item v25_4-account-recurringcharge-fail-email, package M2-RECUR in
// docs/specs/port-backlog.md). The rules are in RecurringChargeFailEmails.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness {
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
