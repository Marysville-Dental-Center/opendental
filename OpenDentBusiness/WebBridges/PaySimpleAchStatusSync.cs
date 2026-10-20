// Vendor-direct integrations: the PaySimple ACH status check, which replaces Open Dental HQ's webhook relay with local polling, and its hourly
// background job.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CodeBase;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness {
	///<summary>What a PaySimple payment status means for a local ACH payment (docs/specs/vendor-direct/paysimple.md, section 2.4).</summary>
	public enum PaySimpleAchOutcome {
		///<summary>Pending or Posted: not settled yet. The payment stays PaySimpleAchPosted.</summary>
		Unchanged,
		///<summary>Settled: funds collected.</summary>
		Settled,
		///<summary>Returned, ReverseNSF, ChargeBack or Failed: the bank returned it, or it never completed.</summary>
		Failed,
		///<summary>Voided: cancelled before the batch. Counts as failed.</summary>
		Voided,
		///<summary>Reversed, ReversePosted or RefundSettled: refunded after settlement. Counts as settled.</summary>
		Refunded,
		///<summary>Authorized, RequiresAction, blank or anything else: not expected for ACH. Left alone and logged.</summary>
		Unrecognized,
	}

	///<summary>Reads and writes the payment rows the ACH status check works on. PaySimpleAchStore is the database version; tests use their own.</summary>
	public interface IPaySimpleAchStore {
		///<summary>Payments to check: see Payments.GetPaySimpleAchToTrack.</summary>
		List<Payment> GetPaymentsToTrack(DateTime dateFrom);
		///<summary>The payment as it is in the database now, or null.</summary>
		Payment GetPayment(long payNum);
		///<summary>Writes only the fields that changed.</summary>
		void UpdatePayment(Payment paymentNew,Payment paymentOld);
		void MakeSecurityLog(Payment payment,string logText);
		void AddAlert(long clinicNum,string description,string itemValue);
	}

	///<summary>The database behind the ACH status check.</summary>
	public class PaySimpleAchStore:IPaySimpleAchStore {
		public List<Payment> GetPaymentsToTrack(DateTime dateFrom) {
			return Payments.GetPaySimpleAchToTrack(dateFrom);
		}

		public Payment GetPayment(long payNum) {
			return Payments.GetPayment(payNum);
		}

		public void UpdatePayment(Payment paymentNew,Payment paymentOld) {
			Payments.Update(paymentNew,paymentOld);
		}

		public void MakeSecurityLog(Payment payment,string logText) {
			try {
				SecurityLogs.MakeLogEntry(EnumPermType.PaymentEdit,payment.PatNum,logText,LogSources.None);
			}
			catch(Exception ex) {
				//No user is logged on (a background thread during log off, for example). The payment note still records the change.
				Logger.WriteLine("PaySimple ACH: security log entry not written for PayNum "+payment.PayNum+": "+ex.Message,"PaySimple");
			}
		}

		public void AddAlert(long clinicNum,string description,string itemValue) {
			AlertItem alertItem=new AlertItem();
			alertItem.Type=AlertType.Generic;
			alertItem.Actions=ActionType.MarkAsRead | ActionType.Delete | ActionType.ShowItemValue;
			alertItem.Severity=SeverityType.Medium;
			alertItem.ClinicNum=clinicNum;
			alertItem.Description=description;
			alertItem.ItemValue=itemValue;
			AlertItems.Insert(alertItem);
		}
	}

	///<summary>The outcome of one ACH status check, for the log and the manual "Check ACH statuses now" message.</summary>
	public class PaySimpleAchSyncResult {
		///<summary>Set when the check didn't run at all (PaySimple disabled, or the hourly check turned off).</summary>
		public string SkipReason="";
		///<summary>Payments whose status PaySimple reported.</summary>
		public int CountChecked;
		public int CountSettled;
		public int CountFailed;
		///<summary>Payments not checked because their clinic has no PaySimple credentials.</summary>
		public int CountSkippedNoCredentials;
		///<summary>One line per problem. Never holds keys or account numbers.</summary>
		public List<string> ListErrors=new List<string>();

		public bool WasSkipped {
			get {
				return SkipReason!="";
			}
		}

		public string GetSummary() {
			if(WasSkipped) {
				return SkipReason;
			}
			string summary=Lans.g("PaySimple","ACH payments checked")+": "+CountChecked+"\r\n"
				+Lans.g("PaySimple","Marked settled")+": "+CountSettled+"\r\n"
				+Lans.g("PaySimple","Marked failed")+": "+CountFailed;
			if(CountSkippedNoCredentials>0) {
				summary+="\r\n"+Lans.g("PaySimple","Skipped (clinic has no PaySimple credentials)")+": "+CountSkippedNoCredentials;
			}
			if(ListErrors.Count>0) {
				summary+="\r\n\r\n"+Lans.g("PaySimple","Problems")+":\r\n"+string.Join("\r\n",ListErrors);
			}
			return summary;
		}
	}

	///<summary>Keeps payment.PaymentStatus of PaySimple ACH payments current by asking PaySimple, replacing the webhook relay Open Dental HQ ran
	///(docs/specs/vendor-direct/paysimple.md, section 3.4). Each Posted payment is looked up with GET /v4/payment/{id}, about one a second; Settled
	///payments from the last 75 days (NACHA's 60-day window for unauthorized-debit returns, plus margin) are matched against one list of returned
	///payments per merchant account. Only PaymentStatus and one appended PayNote line change: never PayAmt, paysplits or deposits. A failure
	///raises an alert so staff correct the account by hand. Only PaySimple hosts are contacted, and no inbound port is opened.</summary>
	public class PaySimpleAchStatusSync {
		///<summary>Settled payments dated this many days back are still checked for late returns.</summary>
		public const int DaysToTrackSettled=75;
		///<summary>The pause between single-payment lookups.</summary>
		public static readonly TimeSpan TimeSpanPace=TimeSpan.FromSeconds(1);
		///<summary>The statuses a settled ACH payment can still move to.</summary>
		public static readonly string[] ArrayLateReturnStatuses={ "Returned","ReverseNSF","ChargeBack" };
		///<summary>The most list pages read per merchant account in one run.</summary>
		private const int MAX_SWEEP_PAGES=50;
		///<summary>Clinics whose "credentials rejected" alert went out, and when (local date). At most one alert per clinic per day.</summary>
		private static readonly Dictionary<long,DateTime> _dictAuthAlertDates=new Dictionary<long,DateTime>();
		private static readonly object _lockRun=new object();
		private readonly IPaySimpleAchStore _store;
		private readonly Func<long,PaySimple.PaySimpleCredentials> _funcGetCredentials;
		private readonly Func<bool> _funcIsProgramEnabled;
		private readonly Func<bool> _funcIsAchStatusCheckOn;
		private readonly Func<DateTime> _funcToday;
		private readonly Action<TimeSpan> _actionPause;
		private readonly Action<string> _actionLog;

		///<summary>Every argument has a production default: the database store, PaySimple.GetCredentials, Programs.IsEnabled,
		///PaySimple.IsAchStatusCheckOn, DateTime.Today, Thread.Sleep and the "PaySimple" log folder. Tests pass their own.</summary>
		public PaySimpleAchStatusSync(IPaySimpleAchStore store=null,Func<long,PaySimple.PaySimpleCredentials> funcGetCredentials=null,
			Func<bool> funcIsProgramEnabled=null,Func<bool> funcIsAchStatusCheckOn=null,Func<DateTime> funcToday=null,Action<TimeSpan> actionPause=null,
			Action<string> actionLog=null)
		{
			_store=store??new PaySimpleAchStore();
			_funcGetCredentials=funcGetCredentials??(x => PaySimple.GetCredentials(x));
			_funcIsProgramEnabled=funcIsProgramEnabled??(() => Programs.IsEnabled(ProgramName.PaySimple));
			_funcIsAchStatusCheckOn=funcIsAchStatusCheckOn??(() => PaySimple.IsAchStatusCheckOn());
			_funcToday=funcToday??(() => DateTime.Today);
			_actionPause=actionPause??(x => Thread.Sleep(x));
			_actionLog=actionLog??(x => Logger.WriteLine(x,"PaySimple"));
		}

		///<summary>The one entry point: checks every tracked ACH payment. isManual runs even when the hourly check is turned off (the background job
		///passes false; the job host's lease already keeps other workstations out). Never throws; problems are in the result.</summary>
		public static PaySimpleAchSyncResult Run(bool isManual) {
			Meth.NoCheckMiddleTierRole();
			return new PaySimpleAchStatusSync().Execute(isManual);
		}

		///<summary>Checks every tracked ACH payment. See Run.</summary>
		public PaySimpleAchSyncResult Execute(bool isManual) {
			PaySimpleAchSyncResult result=new PaySimpleAchSyncResult();
			if(!_funcIsProgramEnabled()) {
				result.SkipReason=Lans.g("PaySimple","PaySimple is not enabled.");
				return result;
			}
			if(!isManual && !_funcIsAchStatusCheckOn()) {
				result.SkipReason=Lans.g("PaySimple","The hourly ACH status check is turned off.");
				return result;
			}
			//One run at a time in this process: a manual run and the hourly job would only duplicate lookups.
			if(!Monitor.TryEnter(_lockRun)) {
				result.SkipReason=Lans.g("PaySimple","An ACH status check is already running.");
				return result;
			}
			try {
				List<Payment> listPayments;
				try {
					listPayments=_store.GetPaymentsToTrack(_funcToday().AddDays(-DaysToTrackSettled));
				}
				catch(Exception ex) {
					AddError(result,"Could not read payments: "+ex.Message);
					return result;
				}
				//Group by merchant account: clinics that share credentials share one list call.
				Dictionary<string,AchGroup> dictGroups=new Dictionary<string,AchGroup>();
				Dictionary<long,PaySimple.PaySimpleCredentials> dictCredentials=new Dictionary<long,PaySimple.PaySimpleCredentials>();
				for(int i=0;i<listPayments.Count;i++) {
					Payment payment=listPayments[i];
					PaySimple.PaySimpleCredentials credentials;
					if(!dictCredentials.TryGetValue(payment.ClinicNum,out credentials)) {
						credentials=_funcGetCredentials(payment.ClinicNum)??new PaySimple.PaySimpleCredentials();
						dictCredentials[payment.ClinicNum]=credentials;
						if(!credentials.IsComplete) {
							_actionLog("PaySimple ACH: ClinicNum "+payment.ClinicNum+" has no PaySimple credentials; its payments are skipped.");
						}
					}
					if(!credentials.IsComplete) {
						result.CountSkippedNoCredentials++;
						continue;
					}
					string groupingKey=credentials.GetGroupingKey();
					AchGroup achGroup;
					if(!dictGroups.TryGetValue(groupingKey,out achGroup)) {
						achGroup=new AchGroup(credentials);
						dictGroups[groupingKey]=achGroup;
					}
					achGroup.ListPayments.Add(payment);
				}
				foreach(AchGroup achGroup in dictGroups.Values) {
					if(!CheckGroup(achGroup,result)) {
						break;//PaySimple is unreachable or busy. Try again next run; statuses stay as they are, which is safe.
					}
				}
				_actionLog("PaySimple ACH: checked "+result.CountChecked+", settled "+result.CountSettled+", failed "+result.CountFailed
					+(result.ListErrors.Count>0 ? ", "+result.ListErrors.Count+" problem(s)" : "")+".");
				return result;
			}
			finally {
				Monitor.Exit(_lockRun);
			}
		}

		///<summary>Checks one payment now (the payment window's "Check ACH status"), whatever its date, if it is a PaySimple ACH payment still
		///Posted or Settled. Never throws; problems are in the result.</summary>
		public PaySimpleAchSyncResult CheckPayment(long payNum) {
			PaySimpleAchSyncResult result=new PaySimpleAchSyncResult();
			if(!_funcIsProgramEnabled()) {
				result.SkipReason=Lans.g("PaySimple","PaySimple is not enabled.");
				return result;
			}
			Payment payment;
			try {
				payment=_store.GetPayment(payNum);
			}
			catch(Exception ex) {
				AddError(result,"Could not read the payment: "+ex.Message);
				return result;
			}
			if(payment==null || string.IsNullOrWhiteSpace(payment.ExternalId)
				|| !payment.PaymentStatus.In(PaymentStatus.PaySimpleAchPosted,PaymentStatus.PaySimpleAchSettled))
			{
				result.SkipReason=Lans.g("PaySimple","This is not a PaySimple ACH payment that is waiting to settle.");
				return result;
			}
			PaySimple.PaySimpleCredentials credentials=_funcGetCredentials(payment.ClinicNum)??new PaySimple.PaySimpleCredentials();
			if(!credentials.IsComplete) {
				result.CountSkippedNoCredentials++;
				return result;
			}
			LookUpAndApply(credentials,payment,result);
			return result;
		}

		///<summary>Checks one merchant account's payments. Returns false if the run should stop (network failure, PaySimple busy or down).</summary>
		private bool CheckGroup(AchGroup achGroup,PaySimpleAchSyncResult result) {
			List<Payment> listPosted=achGroup.ListPayments.FindAll(x => x.PaymentStatus==PaymentStatus.PaySimpleAchPosted);
			List<Payment> listSettled=achGroup.ListPayments.FindAll(x => x.PaymentStatus==PaymentStatus.PaySimpleAchSettled);
			for(int i=0;i<listPosted.Count;i++) {
				if(i>0) {
					_actionPause(TimeSpanPace);
				}
				LookUpResult lookUpResult=LookUpAndApply(achGroup.Credentials,listPosted[i],result);
				if(lookUpResult==LookUpResult.CredentialsRejected) {
					RaiseCredentialsAlert(achGroup,result);
					return true;//The other merchant accounts may still work.
				}
				if(lookUpResult==LookUpResult.StopRun) {
					return false;
				}
			}
			if(listSettled.Count==0) {
				return true;
			}
			//One paged list per merchant account catches returns that arrive after settlement.
			Dictionary<string,Payment> dictSettled=new Dictionary<string,Payment>();
			for(int i=0;i<listSettled.Count;i++) {
				dictSettled[listSettled[i].ExternalId.Trim()]=listSettled[i];
			}
			DateTime dateStart=_funcToday().AddDays(-DaysToTrackSettled);
			for(int page=1;page<=MAX_SWEEP_PAGES;page++) {
				if(page>1 || listPosted.Count>0) {
					_actionPause(TimeSpanPace);
				}
				PaySimple.PaySimplePaymentPage paySimplePaymentPage;
				try {
					paySimplePaymentPage=PaySimple.ListPayments(achGroup.Credentials,ArrayLateReturnStatuses,dateStart,page);
				}
				catch(PaySimpleCredentialsException ex) {
					ex.DoNothing();
					RaiseCredentialsAlert(achGroup,result);
					return true;
				}
				catch(Exception ex) {
					AddError(result,"Late-return check failed: "+DescribeError(ex));
					return !IsStopError(ex);
				}
				for(int i=0;i<paySimplePaymentPage.ListPayments.Count;i++) {
					PaySimple.PaySimplePaymentStatus paySimplePaymentStatus=paySimplePaymentPage.ListPayments[i];
					Payment payment;
					if(!dictSettled.TryGetValue(paySimplePaymentStatus.Id.ToString(CultureInfo.InvariantCulture),out payment)) {
						continue;
					}
					dictSettled.Remove(paySimplePaymentStatus.Id.ToString(CultureInfo.InvariantCulture));
					result.CountChecked++;
					//The list holds lite objects; fetch the full one for the return reason, but don't lose the match if that fails.
					PaySimple.PaySimplePaymentStatus paySimplePaymentStatusFull=paySimplePaymentStatus;
					try {
						_actionPause(TimeSpanPace);
						paySimplePaymentStatusFull=PaySimple.GetPaymentStatus(achGroup.Credentials,payment.ExternalId);
					}
					catch(Exception ex) {
						ex.DoNothing();
					}
					Apply(payment,paySimplePaymentStatusFull,result);
				}
				if(!paySimplePaymentPage.HasMorePages || dictSettled.Count==0) {
					break;
				}
			}
			return true;
		}

		private enum LookUpResult {
			Done,
			CredentialsRejected,
			StopRun,
		}

		///<summary>GET /v4/payment/{ExternalId} for one payment, then Apply.</summary>
		private LookUpResult LookUpAndApply(PaySimple.PaySimpleCredentials credentials,Payment payment,PaySimpleAchSyncResult result) {
			PaySimple.PaySimplePaymentStatus paySimplePaymentStatus;
			try {
				paySimplePaymentStatus=PaySimple.GetPaymentStatus(credentials,payment.ExternalId);
			}
			catch(PaySimpleCredentialsException ex) {
				ex.DoNothing();
				return LookUpResult.CredentialsRejected;
			}
			catch(Exception ex) {
				//A PaySimple error for this payment (unknown ID, or the payment moved to a clinic with another merchant account) leaves it alone.
				AddError(result,"PayNum "+payment.PayNum+": "+DescribeError(ex));
				return IsStopError(ex) ? LookUpResult.StopRun : LookUpResult.Done;
			}
			result.CountChecked++;
			Apply(payment,paySimplePaymentStatus,result);
			return LookUpResult.Done;
		}

		///<summary>Moves the payment to the status PaySimple reports, if it changed and nobody changed the row in the meantime. Idempotent.</summary>
		private void Apply(Payment paymentTracked,PaySimple.PaySimplePaymentStatus paySimplePaymentStatus,PaySimpleAchSyncResult result) {
			PaySimpleAchOutcome outcome=MapStatus(paySimplePaymentStatus.Status);
			if(outcome==PaySimpleAchOutcome.Unrecognized) {
				_actionLog("PaySimple ACH: PayNum "+paymentTracked.PayNum+" has PaySimple status '"+paySimplePaymentStatus.Status+"', left unchanged.");
				return;
			}
			PaymentStatus? paymentStatusNew=GetNewPaymentStatus(outcome);
			if(paymentStatusNew==null || paymentStatusNew.Value==paymentTracked.PaymentStatus) {
				return;
			}
			try {
				//Re-read: continue only if the row still has the status we read, so a user's change or another workstation's run wins.
				Payment payment=_store.GetPayment(paymentTracked.PayNum);
				if(payment==null || payment.PaymentStatus!=paymentTracked.PaymentStatus || payment.ExternalId!=paymentTracked.ExternalId) {
					return;
				}
				Payment paymentOld=payment.Clone();
				payment.PaymentStatus=paymentStatusNew.Value;
				payment.PayNote=AppendNoteLine(payment.PayNote,BuildNoteLine(outcome,paySimplePaymentStatus,_funcToday()));
				_store.UpdatePayment(payment,paymentOld);
				_store.MakeSecurityLog(payment,"PaySimple ACH status check changed the payment of "+payment.PayAmt.ToString("c")
					+" dated "+payment.PayDate.ToShortDateString()+" from "+paymentOld.PaymentStatus+" to "+payment.PaymentStatus+".");
				if(paymentStatusNew.Value==PaymentStatus.PaySimpleAchFailed) {
					result.CountFailed++;
					_store.AddAlert(payment.ClinicNum,Lans.g("PaySimple","PaySimple ACH payment failed. Review the payment."),
						Lans.g("PaySimple","A PaySimple ACH payment did not go through. The payment amount, splits and deposits were not changed;"
							+" correct the account by hand.")+"\r\n"
						+Lans.g("PaySimple","Patient number")+": "+payment.PatNum+"\r\n"
						+Lans.g("PaySimple","Payment date")+": "+payment.PayDate.ToShortDateString()+"\r\n"
						+Lans.g("PaySimple","Amount")+": "+payment.PayAmt.ToString("c")+"\r\n"
						+Lans.g("PaySimple","PaySimple Transaction Number")+": "+payment.ExternalId+"\r\n"
						+BuildNoteLine(outcome,paySimplePaymentStatus,_funcToday()));
				}
				else {
					result.CountSettled++;
				}
			}
			catch(Exception ex) {
				AddError(result,"PayNum "+paymentTracked.PayNum+": could not save the new status: "+ex.Message);
			}
		}

		///<summary>Alerts each of the group's clinics, at most once a day, that PaySimple rejected the credentials.</summary>
		private void RaiseCredentialsAlert(AchGroup achGroup,PaySimpleAchSyncResult result) {
			AddError(result,Lans.g("PaySimple","PaySimple rejected the credentials for")+" "
				+string.Join(", ",achGroup.ListPayments.Select(x => x.ClinicNum).Distinct().Select(x => "ClinicNum "+x)));
			DateTime today=_funcToday().Date;
			List<long> listClinicNums=achGroup.ListPayments.Select(x => x.ClinicNum).Distinct().ToList();
			for(int i=0;i<listClinicNums.Count;i++) {
				lock(_dictAuthAlertDates) {
					DateTime dateLast;
					if(_dictAuthAlertDates.TryGetValue(listClinicNums[i],out dateLast) && dateLast==today) {
						continue;
					}
					_dictAuthAlertDates[listClinicNums[i]]=today;
				}
				try {
					_store.AddAlert(listClinicNums[i],Lans.g("PaySimple","PaySimple credentials rejected; re-enter the key in PaySimple Setup."),
						Lans.g("PaySimple","PaySimple refused the API user name and key while checking ACH payment statuses.")+" "
						+Lans.g("PaySimple","ACH payments stay Posted until the credentials work again."));
				}
				catch(Exception ex) {
					AddError(result,"Could not add the credentials alert: "+ex.Message);
				}
			}
		}

		private void AddError(PaySimpleAchSyncResult result,string error) {
			result.ListErrors.Add(error);
			_actionLog("PaySimple ACH: "+error);
		}

		///<summary>True for errors that mean "PaySimple is unreachable or busy": stop this run and try again next time.</summary>
		private static bool IsStopError(Exception ex) {
			if(ex is PaySimpleException) {
				return false;//PaySimple answered about this one request.
			}
			PaySimpleHttpException paySimpleHttpException=ex as PaySimpleHttpException;
			if(paySimpleHttpException!=null) {
				return paySimpleHttpException.IsTransient;
			}
			if(ex is ODException) {
				return false;
			}
			return true;//Network failures, timeouts.
		}

		///<summary>The first line of an exception message: enough for the log, without PaySimple's raw response.</summary>
		private static string DescribeError(Exception ex) {
			string message=ex.Message??"";
			int indexNewLine=message.IndexOfAny(new[] { '\r','\n' });
			if(indexNewLine>0) {
				message=message.Substring(0,indexNewLine);
			}
			return ex.GetType().Name+": "+message;
		}

		///<summary>Maps a PaySimple payment status to its meaning for an ACH payment. Case-insensitive. Pure.</summary>
		public static PaySimpleAchOutcome MapStatus(string paySimpleStatus) {
			switch((paySimpleStatus??"").Trim().ToLowerInvariant()) {
				case "pending":
				case "posted":
					return PaySimpleAchOutcome.Unchanged;
				case "settled":
					return PaySimpleAchOutcome.Settled;
				case "returned":
				case "reversensf":
				case "chargeback":
				case "failed":
					return PaySimpleAchOutcome.Failed;
				case "voided":
					return PaySimpleAchOutcome.Voided;
				case "reversed":
				case "reverseposted":
				case "refundsettled":
					return PaySimpleAchOutcome.Refunded;
				default:
					return PaySimpleAchOutcome.Unrecognized;
			}
		}

		///<summary>The local status for an outcome: Settled and Refunded give PaySimpleAchSettled; Failed and Voided give PaySimpleAchFailed;
		///Unchanged and Unrecognized give null (leave the payment alone).</summary>
		public static PaymentStatus? GetNewPaymentStatus(PaySimpleAchOutcome outcome) {
			switch(outcome) {
				case PaySimpleAchOutcome.Settled:
				case PaySimpleAchOutcome.Refunded:
					return PaymentStatus.PaySimpleAchSettled;
				case PaySimpleAchOutcome.Failed:
				case PaySimpleAchOutcome.Voided:
					return PaymentStatus.PaySimpleAchFailed;
				default:
					return null;
			}
		}

		///<summary>The PayNote line for a status change, e.g. "PaySimple ACH settled 2026-10-21" or "PaySimple ACH Returned: Insufficient Funds
		///(R01) 2026-10-23".</summary>
		public static string BuildNoteLine(PaySimpleAchOutcome outcome,PaySimple.PaySimplePaymentStatus paySimplePaymentStatus,DateTime today) {
			switch(outcome) {
				case PaySimpleAchOutcome.Settled:
					return "PaySimple ACH settled "+FormatDate(paySimplePaymentStatus.ActualSettledDate,today);
				case PaySimpleAchOutcome.Failed:
					string line="PaySimple ACH "+paySimplePaymentStatus.Status;
					if(!string.IsNullOrWhiteSpace(paySimplePaymentStatus.FailureDescription)) {
						line+=": "+paySimplePaymentStatus.FailureDescription.Trim();
					}
					if(!string.IsNullOrWhiteSpace(paySimplePaymentStatus.FailureCode)) {
						line+=" ("+paySimplePaymentStatus.FailureCode.Trim()+")";
					}
					return line+" "+FormatDate(paySimplePaymentStatus.ReturnDate,today);
				case PaySimpleAchOutcome.Voided:
					return "PaySimple ACH voided at PaySimple "+FormatDate(null,today);
				case PaySimpleAchOutcome.Refunded:
					return "PaySimple ACH refunded at PaySimple "+FormatDate(null,today);
				default:
					return "";
			}
		}

		///<summary>Adds line to the end of the note on its own line.</summary>
		public static string AppendNoteLine(string payNote,string line) {
			if(string.IsNullOrEmpty(line)) {
				return payNote??"";
			}
			if(string.IsNullOrEmpty(payNote)) {
				return line;
			}
			return payNote.TrimEnd('\r','\n')+"\r\n"+line;
		}

		private static string FormatDate(DateTime? date,DateTime today) {
			DateTime dateShown=(date.HasValue && date.Value.Year>1880) ? date.Value : today;
			return dateShown.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
		}

		///<summary>Payments that share one set of PaySimple credentials.</summary>
		private class AchGroup {
			public PaySimple.PaySimpleCredentials Credentials;
			public List<Payment> ListPayments=new List<Payment>();

			public AchGroup(PaySimple.PaySimpleCredentials credentials) {
				Credentials=credentials;
			}
		}
	}

	///<summary>Runs the ACH status check once an hour on one workstation, while PaySimple is enabled and the hourly check is on. Found by the vendor
	///job host (OpenDental/Logic/VendorDirect/VendorJobHost.cs); its lease row is "VendorJobLease.PaySimpleAchStatus".</summary>
	public class PaySimpleAchStatusJob:IVendorBackgroundJob {
		public string Name {
			get {
				return "PaySimpleAchStatus";
			}
		}

		public ProgramName ProgramName {
			get {
				return ProgramName.PaySimple;
			}
		}

		public TimeSpan Interval {
			get {
				return TimeSpan.FromHours(1);
			}
		}

		public bool ShouldRun() {
			return PaySimple.IsAchStatusCheckOn();
		}

		public void Run() {
			PaySimpleAchStatusSync.Run(false);
		}
	}
}
