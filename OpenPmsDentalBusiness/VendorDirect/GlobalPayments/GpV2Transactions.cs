// Global Payments V2: one transaction from start to finish - a terminal sale or refund (start the device command, then query its result until the
// terminal finishes), a sale or refund on a saved card, or a void. Runs off the UI thread; the payment window shows the status and can stop waiting.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;

namespace OpenPmsDentalBusiness.VendorDirect.GlobalPayments {
	///<summary>Runs Global Payments V2 transactions. A request that moves money is sent once and never retried; when its outcome isn't known the
	///result says so (GlobalPaymentsV2Result.IsInDoubt) and the user is told to check the terminal or the merchant portal.</summary>
	public class GpV2Transactions {
		///<summary>How long to wait for a terminal. Global Payments' Cloud Integration page says results usually arrive within seconds and can take up to
		///about 50 seconds while the cardholder works the terminal; this allows for a slow cardholder (INFERRED, like PayConnect's 190 seconds).</summary>
		public static readonly TimeSpan TerminalWaitDefault=TimeSpan.FromSeconds(180);
		///<summary>How often the result is queried while the terminal works.</summary>
		public static readonly TimeSpan PollIntervalDefault=TimeSpan.FromSeconds(2);
		private readonly GpV2Client _gpV2Client;
		private readonly Func<DateTime> _funcUtcNow;
		//System.Threading.Tasks.Task is written out: OpenDentBusiness.Task (the task list) would be found first.
		private readonly Func<TimeSpan,CancellationToken,System.Threading.Tasks.Task> _funcDelay;
		public TimeSpan TerminalWait=TerminalWaitDefault;
		public TimeSpan PollInterval=PollIntervalDefault;

		///<summary>funcUtcNow and funcDelay are for tests (a fake clock that the delay advances).</summary>
		public GpV2Transactions(GpV2Client gpV2Client,Func<DateTime> funcUtcNow=null,
			Func<TimeSpan,CancellationToken,System.Threading.Tasks.Task> funcDelay=null)
		{
			_gpV2Client=gpV2Client??throw new ArgumentNullException(nameof(gpV2Client));
			_funcUtcNow=funcUtcNow??(() => DateTime.UtcNow);
			if(funcDelay==null) {
				funcDelay=(timeSpan,ct) => System.Threading.Tasks.Task.Delay(timeSpan,ct);
			}
			_funcDelay=funcDelay;
		}

		///<summary>A sale or refund on a terminal. actionStatus receives short status lines for the window (may be called from a pool thread).
		///cancellationToken stops the waiting, not the terminal: after a cancel the result is queried once more, and if the terminal still hasn't finished
		///the outcome is Cancelled. Never throws for Global Payments problems; they become the result's outcome and message.</summary>
		public async Task<GlobalPaymentsV2Result> RunTerminalAsync(GlobalPaymentsV2Config config,PayTerminal payTerminal,GpV2TransType transType,
			decimal amount,Action<string> actionStatus,CancellationToken cancellationToken)
		{
			GlobalPaymentsV2Result result=new GlobalPaymentsV2Result();
			result.TransType=transType;
			result.Amount=amount;
			result.IsSandbox=config.UseSandbox;
			result.TerminalId=(payTerminal?.TerminalID??"").Trim();
			result.TerminalName=(payTerminal?.Name??"").Trim();
			if(transType==GpV2TransType.Void) {
				throw new ArgumentException("A void doesn't use a terminal.");
			}
			if(result.TerminalId=="") {
				result.Outcome=GpV2Outcome.Failed;
				result.Message=Lans.g("GlobalPayments","Choose a terminal.");
				return result;
			}
			if(amount<=0) {
				result.Outcome=GpV2Outcome.Failed;
				result.Message=Lans.g("GlobalPayments","The amount must be more than zero.");
				return result;
			}
			actionStatus?.Invoke(Lans.g("GlobalPayments","Sending to the terminal..."));
			GpV2DeviceAnswer answer;
			try {
				//Not cancellable once sent: the request must either reach Global Payments or fail on its own.
				answer=await _gpV2Client.StartDeviceTransactionAsync(config,result.TerminalId,transType,amount,CancellationToken.None).ConfigureAwait(false);
			}
			catch(GpV2Exception ex) {
				result.Outcome=(ex.Kind==GpV2ErrorKind.Offline) ? GpV2Outcome.Unknown : GpV2Outcome.Failed;
				result.Message=ex.Message;
				return result;
			}
			result.DeviceCommandId=answer.CommandId;
			if(!answer.IsPending) {
				Finish(result,answer);
				return result;
			}
			if(result.DeviceCommandId=="") {
				result.Outcome=GpV2Outcome.Unknown;
				result.Message=Lans.g("GlobalPayments","Global Payments accepted the command but sent no command ID to follow it. Check the terminal.");
				return result;
			}
			actionStatus?.Invoke(Lans.g("GlobalPayments","Waiting for the card on the terminal..."));
			DateTime dateTimeDeadlineUtc=_funcUtcNow().Add(TerminalWait);
			string lastError="";
			while(true) {
				if(cancellationToken.IsCancellationRequested) {
					//One last look: the card may have been charged just before the user stopped waiting.
					GpV2DeviceAnswer answerLast=await TryGetResultAsync(config,result,CancellationToken.None).ConfigureAwait(false);
					if(answerLast!=null && !answerLast.IsPending) {
						Finish(result,answerLast);
						return result;
					}
					result.Outcome=GpV2Outcome.Cancelled;
					result.Message=Lans.g("GlobalPayments","Stopped waiting. The terminal may still finish this transaction: check the terminal before trying again.");
					return result;
				}
				if(_funcUtcNow()>=dateTimeDeadlineUtc) {
					result.Outcome=GpV2Outcome.TimedOut;
					result.Message=Lans.g("GlobalPayments","The terminal did not finish in time. Check the terminal and the merchant portal before trying again.")
						+(lastError=="" ? "" : " "+lastError);
					return result;
				}
				try {
					await _funcDelay(PollInterval,cancellationToken).ConfigureAwait(false);
				}
				catch(OperationCanceledException) {
					continue;//Handled at the top of the loop.
				}
				try {
					GpV2DeviceAnswer answerPoll=await _gpV2Client.GetDeviceCommandResultAsync(config,result.TerminalId,result.DeviceCommandId,
						CancellationToken.None).ConfigureAwait(false);
					lastError="";
					if(!answerPoll.IsPending) {
						Finish(result,answerPoll);
						return result;
					}
				}
				catch(GpV2Exception ex) {
					if(ex.Kind==GpV2ErrorKind.Credentials || ex.Kind==GpV2ErrorKind.NotConfigured) {
						result.Outcome=GpV2Outcome.Unknown;
						result.Message=ex.Message+" "+Lans.g("GlobalPayments","The terminal may still finish this transaction: check the terminal.");
						return result;
					}
					//A short network problem while waiting doesn't lose the transaction: keep asking until the deadline.
					lastError=ex.Message;
				}
			}
		}

		private async Task<GpV2DeviceAnswer> TryGetResultAsync(GlobalPaymentsV2Config config,GlobalPaymentsV2Result result,CancellationToken cancellationToken) {
			try {
				return await _gpV2Client.GetDeviceCommandResultAsync(config,result.TerminalId,result.DeviceCommandId,cancellationToken).ConfigureAwait(false);
			}
			catch(GpV2Exception ex) {
				ex.DoNothing();
				return null;
			}
		}

		///<summary>Reads a finished device answer into result.</summary>
		private static void Finish(GlobalPaymentsV2Result result,GpV2DeviceAnswer answer) {
			if(result.DeviceCommandId=="") {
				result.DeviceCommandId=answer.CommandId;
			}
			GpV2Json.ReadTransaction(result,GpV2Json.GetDeviceTransaction(answer.JObjectAnswer),answer.JObjectAnswer);
			if(result.Message=="" && !result.IsApproved) {
				result.Message=GpV2Json.GetFirstString(answer.JObjectAnswer,"action.result_code","status");
			}
		}

		///<summary>A sale or refund on a saved card. Never throws for Global Payments problems.</summary>
		public async Task<GlobalPaymentsV2Result> RunSavedCardAsync(GlobalPaymentsV2Config config,CreditCard creditCard,GpV2TransType transType,
			decimal amount,string reference,bool isRecurring,CancellationToken cancellationToken)
		{
			GlobalPaymentsV2Result result;
			try {
				result=await _gpV2Client.ChargeSavedCardAsync(config,transType,creditCard?.XChargeToken,amount,reference,isRecurring,cancellationToken)
					.ConfigureAwait(false);
			}
			catch(GpV2Exception ex) {
				result=new GlobalPaymentsV2Result();
				result.TransType=transType;
				result.Amount=amount;
				result.IsSandbox=config.UseSandbox;
				result.Outcome=(ex.Kind==GpV2ErrorKind.Offline) ? GpV2Outcome.Unknown : GpV2Outcome.Failed;
				result.Message=ex.Message;
			}
			//The saved card's last four digits, when Global Payments didn't repeat them.
			if(result.CardLast4=="" && creditCard!=null) {
				result.CardLast4=GpV2Json.GetLast4(creditCard.CCNumberMasked);
			}
			//The token is already stored on the card; never hand it back to be saved again.
			if(creditCard!=null && result.PaymentMethodId==creditCard.XChargeToken) {
				result.PaymentMethodId="";
			}
			return result;
		}

		///<summary>A void of transactionId (amount: the original amount, for the note). Never throws for Global Payments problems.</summary>
		public async Task<GlobalPaymentsV2Result> RunVoidAsync(GlobalPaymentsV2Config config,string transactionId,decimal amount,
			CancellationToken cancellationToken)
		{
			GlobalPaymentsV2Result result;
			try {
				result=await _gpV2Client.ReverseAsync(config,transactionId,null,cancellationToken).ConfigureAwait(false);
				if(result.Amount<=0) {
					result.Amount=amount;
				}
			}
			catch(GpV2Exception ex) {
				result=new GlobalPaymentsV2Result();
				result.TransType=GpV2TransType.Void;
				result.Amount=amount;
				result.IsSandbox=config.UseSandbox;
				result.TransactionId=transactionId??"";
				result.Outcome=(ex.Kind==GpV2ErrorKind.Offline) ? GpV2Outcome.Unknown : GpV2Outcome.Failed;
				result.Message=ex.Message;
			}
			return result;
		}
	}
}
