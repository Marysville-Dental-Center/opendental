// CareCredit (Synchrony) patient financing: pending-result polling and batch QuickScreen, in place of Open Dental's webhook relay and eConnector.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness.Bridges {
	///<summary>When a pending row is pulled back (carecredit.md 3.5): every minute for the first 15 minutes, every 10 minutes until 4 hours, then
	///hourly until it expires (72 hours for prefills). Batch rows: from the pullback time of day (or 15 minutes after submission, whichever is later),
	///every 15 minutes, and they expire after 24 hours. Pure functions, for tests.</summary>
	public static class CareCreditPollSchedule {
		public static readonly TimeSpan PrefillLifetime=TimeSpan.FromHours(72);
		public static readonly TimeSpan BatchLifetime=TimeSpan.FromHours(24);

		///<summary>When the row's polling started: when it went Pending, else when it was entered.</summary>
		public static DateTime GetStart(CareCreditWebResponse careCreditWebResponse) {
			if(careCreditWebResponse.DateTimePending.Year>1880) {
				return careCreditWebResponse.DateTimePending;
			}
			return careCreditWebResponse.DateTimeEntry;
		}

		///<summary>True if the row has been open longer than its lifetime.</summary>
		public static bool IsExpired(CareCreditWebResponse careCreditWebResponse,DateTime dateTimeNow) {
			TimeSpan lifetime=(careCreditWebResponse.ServiceType==CareCreditServiceType.Batch) ? BatchLifetime : PrefillLifetime;
			return dateTimeNow-GetStart(careCreditWebResponse)>=lifetime;
		}

		///<summary>The wait between pullbacks for a row of this age.</summary>
		public static TimeSpan GetInterval(CareCreditWebResponse careCreditWebResponse,DateTime dateTimeNow) {
			if(careCreditWebResponse.ServiceType==CareCreditServiceType.Batch) {
				return TimeSpan.FromMinutes(15);
			}
			TimeSpan age=dateTimeNow-GetStart(careCreditWebResponse);
			if(age<TimeSpan.FromMinutes(15)) {
				return TimeSpan.FromMinutes(1);
			}
			if(age<TimeSpan.FromHours(4)) {
				return TimeSpan.FromMinutes(10);
			}
			return TimeSpan.FromMinutes(60);
		}

		///<summary>True if the row should be pulled back now. dateTimeLastPoll is DateTime.MinValue if this process hasn't polled it yet.</summary>
		public static bool IsDue(CareCreditWebResponse careCreditWebResponse,DateTime dateTimeLastPoll,DateTime dateTimeNow,TimeSpan pullbackTimeOfDay) {
			if(string.IsNullOrWhiteSpace(careCreditWebResponse.RefNumber)) {
				return false;//Nothing to pull back with.
			}
			if(careCreditWebResponse.ServiceType==CareCreditServiceType.Batch) {
				DateTime dateTimeStart=GetStart(careCreditWebResponse);
				DateTime dateTimeFirst=dateTimeStart.Date.Add(pullbackTimeOfDay);
				if(dateTimeFirst<dateTimeStart.AddMinutes(15)) {
					dateTimeFirst=dateTimeStart.AddMinutes(15);
				}
				if(dateTimeNow<dateTimeFirst) {
					return false;
				}
			}
			return dateTimeNow-dateTimeLastPoll>=GetInterval(careCreditWebResponse,dateTimeNow);
		}
	}

	///<summary>The database and payment actions a pullback result leads to. Production uses the database; tests use fakes.</summary>
	public sealed class CareCreditResultActions {
		///<summary>(patNum, clinicNum, amount, note, merchantFee) => PayNum. Creates an online-pending CareCredit payment.</summary>
		public Func<long,long,double,string,double,long> FuncInsertPayment;
		public Func<long,Payment> FuncGetPayment;
		public Action<Payment,Payment> ActionUpdatePayment;
		public Func<CareCreditWebResponse,CareCreditWebResponse,bool> FuncUpdateRow;
		///<summary>(CareCreditWebResponseNum, DateTimeCompleted) => rows changed. Atomically marks a pending row with no payment as Completed
		///(CareCreditWebResponses.ClaimCompletedForPayment). A payment is created only when this returns 1, so two processes pulling back the same
		///row can't both pay it.</summary>
		public Func<long,DateTime,long> FuncClaimCompletedForPayment;
		///<summary>Reads a row from the database (CareCreditWebResponses.GetOne), or null. Used after a lost claim to pick up the winner's status and
		///payment.</summary>
		public Func<long,CareCreditWebResponse> FuncGetRow;
		///<summary>(status description, patNum, isBatch)</summary>
		public Action<string,long,bool> ActionUpdatePatField;
		public Action<double,long> ActionUpsertPreApprovalAmt;
		public Action<double,long> ActionUpsertAvailableCredit;
		///<summary>Puts an 'x' in front of a closed merchant number wherever it is stored.</summary>
		public Action<string> ActionMarkMerchantClosed;

		public static CareCreditResultActions CreateDb() {
			CareCreditResultActions actions=new CareCreditResultActions();
			actions.FuncInsertPayment=(patNum,clinicNum,amount,note,fee) =>
				Payments.InsertFromCareCredit(patNum,0,clinicNum,amount,note,CreditCardSource.CareCredit,fee);
			actions.FuncGetPayment=Payments.GetPayment;
			actions.ActionUpdatePayment=Payments.Update;
			actions.FuncUpdateRow=CareCreditWebResponses.Update;
			actions.FuncClaimCompletedForPayment=CareCreditWebResponses.ClaimCompletedForPayment;
			actions.FuncGetRow=CareCreditWebResponses.GetOne;
			actions.ActionUpdatePatField=CareCreditWebResponses.UpdateCareCreditPatField;
			actions.ActionUpsertPreApprovalAmt=CareCreditWebResponses.UpsertPreApprovalAmt;
			actions.ActionUpsertAvailableCredit=CareCreditWebResponses.UpsertAvailableCredit;
			actions.ActionMarkMerchantClosed=CareCreditJobs.MarkMerchantClosed;
			return actions;
		}
	}

	///<summary>Background work for CareCredit: pending-result polling and batch QuickScreen. Run by the vendor job host on the one computer chosen in
	///CareCredit Setup (CareCreditPollJob, CareCreditBatchJob), and later by the local service host. Never runs while the program link is disabled
	///or the settings are incomplete, and never on the middle tier server.</summary>
	public static class CareCreditJobs {
		///<summary>When this process last pulled back each row, by CareCreditWebResponseNum.</summary>
		private static readonly ConcurrentDictionary<long,DateTime> _dictLastPoll=new ConcurrentDictionary<long,DateTime>();
		///<summary>Consecutive pullback refusals (HTTP 403/404) per CareCreditWebResponseNum in this process.</summary>
		private static readonly ConcurrentDictionary<long,int> _dictRefusals=new ConcurrentDictionary<long,int>();
		///<summary>A row is given up on only after this many consecutive refusals. Rows made under Open Dental's partner context before migration are
		///refused every time, so they stop quickly; our own rows survive a transient refusal and keep polling until they expire.</summary>
		private const int MAX_REFUSALS=3;
		///<summary>Pause between batch submissions, so a large batch doesn't hit Synchrony's (unpublished) rate limits.</summary>
		public static TimeSpan BatchSubmitDelay=TimeSpan.FromMilliseconds(500);

		#region Polling
		///<summary>Pulls back every pending row that is due, and applies the results: statuses, patient fields, payments. Rows past their lifetime
		///become Expired (prefill) or ExpiredBatch. Returns the number of rows pulled back.
		///funcInclude limits the pass to some rows (null: every pending row). funcKeepGoing is asked before each pullback (null: always); when it
		///returns false the pass stops and the remaining rows wait for the next one. The job host uses it to renew its lease during a long pass.</summary>
		public static int PollPending(DateTime dateTimeNow,Func<CareCreditWebResponse,bool> funcInclude=null,Func<bool> funcKeepGoing=null) {
			Meth.NoCheckMiddleTierRole();
			if(!CareCredit.IsUsable()) {
				return 0;
			}
			CareCreditConfig config=CareCredit.FuncGetConfig();
			TimeSpan pullbackTime=CareCreditSettings.GetPullbackTime();
			List<CareCreditWebResponse> listPending=CareCreditWebResponses.GetAllPending();
			if(funcInclude!=null) {
				listPending=listPending.FindAll(x => funcInclude(x));
			}
			CareCreditResultActions actions=CareCreditResultActions.CreateDb();
			int countPolled=0;
			for(int i=0;i<listPending.Count;i++) {
				CareCreditWebResponse careCreditWebResponse=listPending[i];
				if(CareCreditPollSchedule.IsExpired(careCreditWebResponse,dateTimeNow)) {
					CareCreditWebStatus statusExpired=(careCreditWebResponse.ServiceType==CareCreditServiceType.Batch) ? CareCreditWebStatus.ExpiredBatch
						: CareCreditWebStatus.Expired;
					CareCreditWebResponses.UpdateProcessingWebStatus(careCreditWebResponse,statusExpired);
					_dictLastPoll.TryRemove(careCreditWebResponse.CareCreditWebResponseNum,out _);
					_dictRefusals.TryRemove(careCreditWebResponse.CareCreditWebResponseNum,out _);
					continue;
				}
				DateTime dateTimeLastPoll;
				if(!_dictLastPoll.TryGetValue(careCreditWebResponse.CareCreditWebResponseNum,out dateTimeLastPoll)) {
					dateTimeLastPoll=DateTime.MinValue;
				}
				if(!CareCreditPollSchedule.IsDue(careCreditWebResponse,dateTimeLastPoll,dateTimeNow,pullbackTime)) {
					continue;
				}
				if(funcKeepGoing!=null && !funcKeepGoing()) {
					return countPolled;//Out of time, or the lease went to another workstation. The rest are still due next pass.
				}
				_dictLastPoll[careCreditWebResponse.CareCreditWebResponseNum]=dateTimeNow;
				CareCreditPullbackResult result;
				try {
					result=CareCredit.Pullback(config,careCreditWebResponse);
				}
				catch(CareCredit.CareCreditException ex) {
					if(ex.IsAuthFailure) {
						return countPolled;//Every other row would fail the same way. The setup window shows the error.
					}
					if(ex.ErrorCode==403 || ex.ErrorCode==404) {
						//Refused for this reference, e.g. a row made under Open Dental's partner context before migration. Retry a few times in case the
						//refusal is transient, then stop instead of retrying forever (spec 3.8). Rows that are never given up on still expire normally.
						int countRefusals=_dictRefusals.AddOrUpdate(careCreditWebResponse.CareCreditWebResponseNum,1,(key,count) => count+1);
						if(countRefusals<MAX_REFUSALS) {
							continue;
						}
						_dictRefusals.TryRemove(careCreditWebResponse.CareCreditWebResponseNum,out _);
						_dictLastPoll.TryRemove(careCreditWebResponse.CareCreditWebResponseNum,out _);
						CareCreditWebResponse careCreditWebResponseOld=careCreditWebResponse.Copy();
						careCreditWebResponse.LastResponseStr=CareCreditQSBatchResponse.ToLocalErrorJson(
							"Pullback refused (HTTP "+ex.ErrorCode+"). The transaction may have been made under a different integration.",isInvalidInput:false);
						CareCredit.ApplyStatus(careCreditWebResponse,CareCreditWebStatus.ErrorAcknowledged,dateTimeNow);
						CareCreditWebResponses.Update(careCreditWebResponse,careCreditWebResponseOld);
					}
					else if(ex.ErrorCode==429) {
						return countPolled;//Throttled: stop this cycle.
					}
					continue;
				}
				catch(ODException ex) {
					ex.DoNothing();
					return countPolled;//Network down: try again next cycle.
				}
				countPolled++;
				_dictRefusals.TryRemove(careCreditWebResponse.CareCreditWebResponseNum,out _);//A successful pullback resets the refusal count.
				//A pass can outlive its lease, and a window can pull back its own row, so apply the result to the row as saved now, not to the snapshot
				//from the start of the pass. (ApplyResult's claim is what guarantees one payment; this skips work another process already did.)
				CareCreditWebResponse careCreditWebResponseDb=actions.FuncGetRow(careCreditWebResponse.CareCreditWebResponseNum);
				if(careCreditWebResponseDb==null || !CareCreditWebResponses.ListWebStatusPending.Contains(careCreditWebResponseDb.ProcessingStatus)) {
					_dictLastPoll.TryRemove(careCreditWebResponse.CareCreditWebResponseNum,out _);
					continue;
				}
				careCreditWebResponse=careCreditWebResponseDb;
				ApplyResult(careCreditWebResponse,result,dateTimeNow,actions);
				if(!CareCreditWebResponses.ListWebStatusPending.Contains(careCreditWebResponse.ProcessingStatus)) {
					_dictLastPoll.TryRemove(careCreditWebResponse.CareCreditWebResponseNum,out _);
				}
			}
			return countPolled;
		}

		///<summary>Applies a pullback result to a pending row and saves it: status, then patient fields (QuickScreen and lookups), then payments
		///(purchases and refunds). A completed purchase or refund with no payment (finished on the patient's own device) creates exactly one
		///online-pending payment, refunds as a negative amount; one with a payment completes that payment. Before a payment is created the row is
		///claimed in the database (an UPDATE from pending to Completed WHERE PayNum=0, FuncClaimCompletedForPayment); only the caller whose claim
		///changes the row creates the payment, so neither a repeated pullback nor a second process or window can pay twice.</summary>
		public static void ApplyResult(CareCreditWebResponse careCreditWebResponse,CareCreditPullbackResult result,DateTime dateTimeNow,
			CareCreditResultActions actions)
		{
			CareCreditWebResponse careCreditWebResponseOld=careCreditWebResponse.Copy();
			bool wasPending=CareCreditWebResponses.ListWebStatusPending.Contains(careCreditWebResponseOld.ProcessingStatus);
			CareCredit.ApplyPullbackToRow(careCreditWebResponse,result,dateTimeNow);
			CareCreditWebStatus status=careCreditWebResponse.ProcessingStatus;
			bool isBatch=(careCreditWebResponse.ServiceType==CareCreditServiceType.Batch);
			if(status==CareCreditWebStatus.BatchError && result.IsMerchantClosed && careCreditWebResponse.MerchantNumber!="") {
				actions.ActionMarkMerchantClosed?.Invoke(careCreditWebResponse.MerchantNumber);
			}
			bool isPayment=wasPending && status==CareCreditWebStatus.Completed
				&& careCreditWebResponse.TransType.In(CareCreditTransType.Purchase,CareCreditTransType.Refund);
			if(isPayment && careCreditWebResponse.PayNum==0 && careCreditWebResponse.PatNum!=0) {
				double amount=Math.Abs(careCreditWebResponse.Amount);
				if(careCreditWebResponse.TransType==CareCreditTransType.Refund) {
					amount=-amount;
				}
				//Claim the row before creating the payment. The claim is a compare-and-swap in the database, so when two pollers (or a poller and a
				//window closing) pull back the same completed purchase, exactly one of them gets 1 here. It also saves the completed status first, so a
				//crash between the two can't lead to a second payment next cycle.
				if(actions.FuncClaimCompletedForPayment(careCreditWebResponse.CareCreditWebResponseNum,careCreditWebResponse.DateTimeCompleted)!=1) {
					//Someone else completed this row and made its payment. Leave their row as it is, and show the caller what they saved.
					CareCreditWebResponse careCreditWebResponseDb=actions.FuncGetRow?.Invoke(careCreditWebResponse.CareCreditWebResponseNum);
					if(careCreditWebResponseDb!=null) {
						careCreditWebResponse.ProcessingStatus=careCreditWebResponseDb.ProcessingStatus;
						careCreditWebResponse.PayNum=careCreditWebResponseDb.PayNum;
						careCreditWebResponse.DateTimeCompleted=careCreditWebResponseDb.DateTimeCompleted;
					}
					return;
				}
				actions.FuncUpdateRow(careCreditWebResponse,careCreditWebResponseOld);//The rest of the pullback: response, amount, transaction type.
				careCreditWebResponseOld=careCreditWebResponse.Copy();
				long payNum=actions.FuncInsertPayment(careCreditWebResponse.PatNum,careCreditWebResponse.ClinicNum,amount,
					CareCredit.GetFormattedNote(careCreditWebResponse),CareCredit.GetMerchantFee(careCreditWebResponse));
				careCreditWebResponse.PayNum=payNum;
			}
			else if(isPayment && careCreditWebResponse.PayNum!=0) {
				Payment payment=actions.FuncGetPayment(careCreditWebResponse.PayNum);
				if(payment!=null && !payment.IsCcCompleted) {
					Payment paymentOld=payment.Clone();
					payment.IsCcCompleted=true;
					payment.PaymentSource=CreditCardSource.CareCredit;
					string note=CareCredit.GetFormattedNote(careCreditWebResponse);
					if(!(payment.PayNote??"").Contains(note)) {
						payment.PayNote=string.IsNullOrEmpty(payment.PayNote) ? note : payment.PayNote+"\r\n"+note;
					}
					payment.MerchantFee=CareCredit.GetMerchantFee(careCreditWebResponse);
					actions.ActionUpdatePayment(payment,paymentOld);
				}
			}
			actions.FuncUpdateRow(careCreditWebResponse,careCreditWebResponseOld);
			if(!wasPending || careCreditWebResponse.PatNum==0) {
				return;
			}
			if(status.In(CareCreditWebStatus.PreApproved,CareCreditWebStatus.Declined,CareCreditWebStatus.AccountFound,
				CareCreditWebStatus.AccountNotFoundQS,CareCreditWebStatus.DupQS,CareCreditWebStatus.CallForAuth))
			{
				actions.ActionUpdatePatField(status.GetDescription(),careCreditWebResponse.PatNum,isBatch);
				if(status==CareCreditWebStatus.PreApproved && result.PreApprovalAmount>0) {
					actions.ActionUpsertPreApprovalAmt(result.PreApprovalAmount,careCreditWebResponse.PatNum);
				}
				else if(status==CareCreditWebStatus.AccountFound && result.AvailableCredit>0) {
					actions.ActionUpsertAvailableCredit(result.AvailableCredit,careCreditWebResponse.PatNum);
				}
			}
		}

		///<summary>Pulls back one row now and applies the result as the poller would (statuses, patient fields, payments), for a hosted-page window
		///that just closed. Returns false, without calling Synchrony, for a row that isn't pending or has no reference ID, or while CareCredit isn't
		///usable. Throws CareCredit.CareCreditException for an HTTP error answer and ODException when Synchrony can't be reached. actions defaults to
		///the database.</summary>
		public static bool PullbackNow(CareCreditWebResponse careCreditWebResponse,DateTime dateTimeNow,CareCreditResultActions actions=null) {
			Meth.NoCheckMiddleTierRole();
			if(careCreditWebResponse==null || string.IsNullOrWhiteSpace(careCreditWebResponse.RefNumber)
				|| !CareCreditWebResponses.ListWebStatusPending.Contains(careCreditWebResponse.ProcessingStatus))
			{
				return false;
			}
			if(!CareCredit.IsUsable()) {
				return false;
			}
			CareCreditConfig config=CareCredit.FuncGetConfig();
			_dictLastPoll[careCreditWebResponse.CareCreditWebResponseNum]=dateTimeNow;
			CareCreditPullbackResult result=CareCredit.Pullback(config,careCreditWebResponse);
			ApplyResult(careCreditWebResponse,result,dateTimeNow,actions??CareCreditResultActions.CreateDb());
			if(!CareCreditWebResponses.ListWebStatusPending.Contains(careCreditWebResponse.ProcessingStatus)) {
				_dictLastPoll.TryRemove(careCreditWebResponse.CareCreditWebResponseNum,out _);
			}
			return true;
		}
		#endregion Polling

		#region Rows this session created
		///<summary>Rows this process created with a prefill, by CareCreditWebResponseNum. While no computer is chosen in CareCredit Setup, nothing
		///polls every row, so each session pulls back its own (carecredit.md 3.7).</summary>
		private static readonly ConcurrentDictionary<long,byte> _dictSessionRows=new ConcurrentDictionary<long,byte>();

		///<summary>Remembers a row this process just created, so this session pulls it back while no job computer is set. Its first scheduled
		///pullback is a poll interval after dateTimeNow, not at once (the page has only just opened).</summary>
		public static void TrackSessionRow(long careCreditWebResponseNum,DateTime dateTimeNow) {
			if(careCreditWebResponseNum==0) {
				return;
			}
			_dictSessionRows[careCreditWebResponseNum]=0;
			_dictLastPoll[careCreditWebResponseNum]=dateTimeNow;
		}

		///<summary>True while this session has rows it created that may still be pending.</summary>
		public static bool HasSessionRows {
			get {
				return !_dictSessionRows.IsEmpty;
			}
		}

		public static bool IsSessionRow(long careCreditWebResponseNum) {
			return _dictSessionRows.ContainsKey(careCreditWebResponseNum);
		}

		///<summary>Forgets every row this session created. For tests.</summary>
		public static void ClearSessionRows() {
			foreach(long num in _dictSessionRows.Keys) {
				_dictLastPoll.TryRemove(num,out _);
			}
			_dictSessionRows.Clear();
		}

		///<summary>True when no computer is chosen in CareCredit Setup, so each session has to pull back the rows it created. With a job computer set,
		///CareCreditPollJob on that computer pulls back every row.</summary>
		public static bool IsSessionPollingNeeded() {
			Meth.NoCheckMiddleTierRole();
			return CareCreditSettings.GetJobComputerName()=="";
		}

		///<summary>While no job computer is set, pulls back the due rows this session created (PollPending limited to them) and forgets the ones that
		///are no longer pending. Returns the number pulled back. Does nothing while CareCredit isn't usable. Once a job computer is set, the rows are
		///left to it and forgotten here.</summary>
		public static int PollSessionRows(DateTime dateTimeNow) {
			Meth.NoCheckMiddleTierRole();
			if(_dictSessionRows.IsEmpty) {
				return 0;
			}
			if(!IsSessionPollingNeeded()) {
				_dictSessionRows.Clear();//The job computer's poller pulls back every row.
				return 0;
			}
			if(!CareCredit.IsUsable()) {
				return 0;
			}
			HashSet<long> hashSetStillPending=new HashSet<long>();
			int countPolled=PollPending(dateTimeNow,funcInclude:x => {
				if(!_dictSessionRows.ContainsKey(x.CareCreditWebResponseNum)) {
					return false;
				}
				hashSetStillPending.Add(x.CareCreditWebResponseNum);
				return true;
			});
			foreach(long num in _dictSessionRows.Keys) {
				if(!hashSetStillPending.Contains(num)) {
					_dictSessionRows.TryRemove(num,out _);//Completed, expired, or handled by another window or computer.
				}
			}
			return countPolled;
		}
		#endregion Rows this session created

		#region Batch QuickScreen
		///<summary>Runs today's batch QuickScreen once (compare-and-swap on CareCreditBatchLastRunDate): selects patients for each clinic whose mode
		///isn't Off, records BatchError rows for the ones that can't be sent, submits the rest one by one, and raises one alert per clinic with
		///errors. Returns the number of requests submitted, or -1 if another host already ran today's batch.</summary>
		public static int RunBatch(DateTime dateTimeNow) {
			Meth.NoCheckMiddleTierRole();
			if(!CareCredit.IsUsable()) {
				return 0;
			}
			CareCreditConfig config=CareCredit.FuncGetConfig();
			if(CareCreditConfig.GetPathProblem(config.BatchPath,isRequired:true)!="") {
				return 0;//Batch QuickScreen isn't set up.
			}
			if(!CareCreditSettings.TryClaimBatchDay(dateTimeNow.Date)) {
				return -1;
			}
			List<long> listClinicNums=new List<long> { 0 };
			if(PrefC.HasClinicsEnabled) {
				listClinicNums.AddRange(Clinics.GetDeepCopy(isShort:true).Select(x => x.ClinicNum));
			}
			List<long> listPatNumsExclude=CareCreditWebResponses.GetPatNumsForDateRangeToExclude(dateTimeNow.Date.AddDays(-60),dateTimeNow);
			int countSubmitted=0;
			bool isByProv=CareCredit.IsMerchantNumberByProv;
			for(int i=0;i<listClinicNums.Count;i++) {
				long clinicNum=listClinicNums[i];
				EnumCareCreditBatch mode=CareCreditSettings.GetBatchMode(clinicNum);
				if(mode==EnumCareCreditBatch.Off) {
					continue;
				}
				string merchantNumClinic=isByProv ? "" : CareCredit.GetMerchantNumberByClinic(clinicNum);
				if(!isByProv && !CareCredit.IsMerchantNumValid(merchantNumClinic,allowClosedMerchant:false)) {
					continue;//No usable merchant number for this clinic.
				}
				List<CareCreditBatchCandidate> listCandidates=GetCandidates(clinicNum,dateTimeNow.Date,CareCreditSettings.GetBatchDays(clinicNum),mode,
					isByProv,merchantNumClinic);
				CareCreditBatchPlan plan=CareCreditBatchPlanner.Plan(listCandidates,mode,listPatNumsExclude);
				int countErrors=0;
				for(int e=0;e<plan.ListErrors.Count;e++) {
					InsertBatchError(plan.ListErrors[e],dateTimeNow);
					countErrors++;
				}
				for(int s=0;s<plan.ListSubmissions.Count;s++) {
					if(!SubmitOne(config,plan.ListSubmissions[s],dateTimeNow)) {
						countErrors++;
					}
					else {
						countSubmitted++;
					}
					listPatNumsExclude.Add(plan.ListSubmissions[s].PatNum);
					if(BatchSubmitDelay>TimeSpan.Zero) {
						Thread.Sleep(BatchSubmitDelay);
					}
				}
				if(countErrors>0) {
					InsertBatchErrorAlert(clinicNum,countErrors);
				}
			}
			return countSubmitted;
		}

		///<summary>Builds the batch candidates for one clinic from appointments starting tomorrow through `days` days ahead (scheduled, complete or
		///ASAP; not broken, unscheduled or planned).</summary>
		private static List<CareCreditBatchCandidate> GetCandidates(long clinicNum,DateTime dateToday,int days,EnumCareCreditBatch mode,bool isByProv,
			string merchantNumClinic)
		{
			List<CareCreditBatchCandidate> listCandidates=new List<CareCreditBatchCandidate>();
			DateTime dateFrom=dateToday.AddDays(1);
			DateTime dateTo=dateToday.AddDays(days+1).AddSeconds(-1);
			List<Appointment> listAppointments=Appointments.GetAppointmentsStartingWithinPeriod(dateFrom,dateTo,ApptStatus.Broken,ApptStatus.UnschedList,
				ApptStatus.Planned,ApptStatus.PtNote,ApptStatus.PtNoteCompleted);
			if(PrefC.HasClinicsEnabled) {
				listAppointments=listAppointments.FindAll(x => x.ClinicNum==clinicNum);
			}
			if(listAppointments.Count==0) {
				return listCandidates;
			}
			List<Procedure> listProcedures=Procedures.GetProcsMultApts(listAppointments.Select(x => x.AptNum).ToList());
			Dictionary<long,Patient> dictPatients=new Dictionary<long,Patient>();
			for(int i=0;i<listAppointments.Count;i++) {
				Appointment appointment=listAppointments[i];
				Patient patient=GetPatientCached(dictPatients,appointment.PatNum);
				if(patient==null) {
					continue;
				}
				CareCreditBatchCandidate candidate=new CareCreditBatchCandidate();
				candidate.AptNum=appointment.AptNum;
				candidate.AptDateTime=appointment.AptDateTime;
				candidate.ClinicNum=clinicNum;
				candidate.ProvNum=appointment.ProvNum;
				candidate.Patient=patient;
				candidate.Guarantor=(patient.Guarantor==patient.PatNum) ? patient : GetPatientCached(dictPatients,patient.Guarantor);
				candidate.EstimatedFee=listProcedures.Where(x => x.AptNum==appointment.AptNum).Sum(x => x.ProcFeeTotal);
				if(mode==EnumCareCreditBatch.On) {
					candidate.EstimatedPatientPortion=(double)Appointments.GetEstPatientPortion(appointment);
				}
				listCandidates.Add(candidate);
			}
			SetMerchantNumbers(listCandidates,clinicNum,isByProv,merchantNumClinic);
			return listCandidates;
		}

		///<summary>Sets each candidate's merchant number. By provider: CareCredit.GetMerchantNumberByProv, the same lookup a purchase or QuickScreen
		///started from a window uses (the provider's row for the clinic, else, when that row is missing or blank, the provider's default), read once
		///per provider; also sets ProviderAbbr. Otherwise: merchantNumClinic for every candidate.</summary>
		public static void SetMerchantNumbers(List<CareCreditBatchCandidate> listCandidates,long clinicNum,bool isByProv,string merchantNumClinic) {
			Meth.NoCheckMiddleTierRole();
			Dictionary<long,string> dictMerchantNumsByProv=new Dictionary<long,string>();
			for(int i=0;i<listCandidates.Count;i++) {
				CareCreditBatchCandidate candidate=listCandidates[i];
				if(!isByProv) {
					candidate.MerchantNumber=merchantNumClinic;
					continue;
				}
				string merchantNum;
				if(!dictMerchantNumsByProv.TryGetValue(candidate.ProvNum,out merchantNum)) {
					merchantNum=CareCredit.GetMerchantNumberByProv(candidate.ProvNum,clinicNum);
					dictMerchantNumsByProv[candidate.ProvNum]=merchantNum;
				}
				candidate.ProviderAbbr=Providers.GetAbbr(candidate.ProvNum);
				candidate.MerchantNumber=merchantNum;
			}
		}

		private static Patient GetPatientCached(Dictionary<long,Patient> dictPatients,long patNum) {
			Patient patient;
			if(!dictPatients.TryGetValue(patNum,out patient)) {
				patient=Patients.GetPat(patNum);
				dictPatients[patNum]=patient;
			}
			return patient;
		}

		private static void InsertBatchError(CareCreditBatchError careCreditBatchError,DateTime dateTimeNow) {
			CareCreditWebResponse careCreditWebResponse=new CareCreditWebResponse();
			careCreditWebResponse.PatNum=careCreditBatchError.PatNum;
			careCreditWebResponse.ClinicNum=careCreditBatchError.ClinicNum;
			careCreditWebResponse.MerchantNumber=careCreditBatchError.MerchantNumber??"";
			careCreditWebResponse.RefNumber="";
			careCreditWebResponse.WebToken="";
			careCreditWebResponse.ServiceType=CareCreditServiceType.Batch;
			careCreditWebResponse.TransType=CareCreditTransType.None;
			careCreditWebResponse.LastResponseStr=CareCreditQSBatchResponse.ToLocalErrorJson(careCreditBatchError.Description,careCreditBatchError.IsInvalidInput);
			CareCredit.ApplyStatus(careCreditWebResponse,CareCreditWebStatus.BatchError,dateTimeNow);
			CareCreditWebResponses.Insert(careCreditWebResponse);
		}

		///<summary>Inserts a Created batch row, submits it and marks it Pending with the reference ID. On failure the row becomes a BatchError (with
		///the merchant marked closed if Synchrony says so). Returns false on failure.</summary>
		private static bool SubmitOne(CareCreditConfig config,CareCreditBatchSubmission submission,DateTime dateTimeNow) {
			CareCreditWebResponse careCreditWebResponse=new CareCreditWebResponse();
			careCreditWebResponse.PatNum=submission.PatNum;
			careCreditWebResponse.ClinicNum=submission.ClinicNum;
			careCreditWebResponse.MerchantNumber=submission.Request.MerchantNumber;
			careCreditWebResponse.Amount=submission.Request.EstimatedFeeAmount;
			careCreditWebResponse.RefNumber="";
			careCreditWebResponse.WebToken="";
			careCreditWebResponse.ServiceType=CareCreditServiceType.Batch;
			careCreditWebResponse.TransType=CareCreditTransType.None;
			careCreditWebResponse.ProcessingStatus=CareCreditWebStatus.Created;
			careCreditWebResponse.LastResponseStr="";
			CareCreditWebResponses.Insert(careCreditWebResponse);
			submission.Request.PartnerCode=config.PartnerCode.Trim();
			string refId;
			try {
				refId=VendorHttp.RunSync(() => CareCredit.ApiClient.SubmitBatchAsync(config,submission.Request));
			}
			catch(Exception ex) {
				CareCredit.CareCreditException careCreditException=ex as CareCredit.CareCreditException;
				CareCreditPullbackResult result=CareCreditJson.ReadPullback(careCreditException?.ResponseJson??"",isQuickScreen:true);
				string description=(result.ErrorDescription!="") ? result.ErrorDescription : ex.Message;
				if(careCreditException!=null && careCreditException.IsAuthFailure) {
					CareCreditSettings.SetLastAuthError(careCreditException.Message);
				}
				careCreditWebResponse.LastResponseStr=CareCreditQSBatchResponse.ToLocalErrorJson(description,result.IsInvalidInput||careCreditException?.ErrorCode==400,
					result.IsMerchantClosed);
				CareCredit.ApplyStatus(careCreditWebResponse,CareCreditWebStatus.BatchError,dateTimeNow);
				CareCreditWebResponses.Update(careCreditWebResponse);
				if(result.IsMerchantClosed) {
					MarkMerchantClosed(careCreditWebResponse.MerchantNumber);
				}
				return false;
			}
			if(string.IsNullOrWhiteSpace(refId)) {
				careCreditWebResponse.LastResponseStr=CareCreditQSBatchResponse.ToLocalErrorJson("CareCredit returned no reference ID.",false);
				CareCredit.ApplyStatus(careCreditWebResponse,CareCreditWebStatus.BatchError,dateTimeNow);
				CareCreditWebResponses.Update(careCreditWebResponse);
				return false;
			}
			careCreditWebResponse.RefNumber=refId.Trim();
			CareCredit.ApplyStatus(careCreditWebResponse,CareCreditWebStatus.Pending,dateTimeNow);
			CareCreditWebResponses.Update(careCreditWebResponse);
			return true;
		}

		private static void InsertBatchErrorAlert(long clinicNum,int countErrors) {
			AlertItem alertItem=new AlertItem();
			alertItem.ClinicNum=clinicNum;
			alertItem.Type=AlertType.CareCreditBatchError;
			alertItem.Severity=SeverityType.Medium;
			alertItem.Actions=ActionType.OpenForm|ActionType.MarkAsRead|ActionType.Delete;
			alertItem.FormToOpen=FormType.FormCareCreditTransactions;
			alertItem.Description=Lans.g("CareCredit","CareCredit batch errors")+": "+countErrors;
			alertItem.ItemValue="";
			AlertItems.Insert(alertItem);
		}

		///<summary>Puts an 'x' in front of merchantNumber in every program property and providerclinic row that holds it, so the closed-merchant UI
		///takes over.</summary>
		public static void MarkMerchantClosed(string merchantNumber) {
			string num=(merchantNumber??"").Trim();
			if(num=="" || CareCredit.IsMerchantNumClosed(num)) {
				return;
			}
			long programNum=CareCreditSettings.GetProgramNum();
			bool isChanged=false;
			if(programNum!=0) {
				List<ProgramProperty> listProgramProperties=ProgramProperties.GetWhere(x => x.ProgramNum==programNum
					&& x.PropertyDesc==ProgramProperties.PropertyDescs.CareCredit.CareCreditMerchantNumber && (x.PropertyValue??"").Trim()==num);
				for(int i=0;i<listProgramProperties.Count;i++) {
					isChanged|=ProgramProperties.UpdateProgramPropertyWithValue(listProgramProperties[i],"x"+num);
				}
			}
			List<ProviderClinic> listProviderClinics=ProviderClinics.GetCareCreditRows().FindAll(x => (x.CareCreditMerchantId??"").Trim()==num);
			for(int i=0;i<listProviderClinics.Count;i++) {
				listProviderClinics[i].CareCreditMerchantId="x"+num;
				ProviderClinics.Update(listProviderClinics[i]);
				isChanged=true;
			}
			if(isChanged) {
				Signalods.SetInvalid(InvalidType.Programs,InvalidType.Providers);
			}
		}
		#endregion Batch QuickScreen
	}

	///<summary>One appointment's patient, as the batch sees it.</summary>
	public sealed class CareCreditBatchCandidate {
		public long AptNum;
		public DateTime AptDateTime;
		public long ClinicNum;
		public long ProvNum;
		public string ProviderAbbr="";
		public Patient Patient;
		///<summary>The patient's guarantor (the patient when self-guaranteed), or null.</summary>
		public Patient Guarantor;
		public double EstimatedFee;
		public double EstimatedPatientPortion;
		public string MerchantNumber="";
	}

	///<summary>One request the batch will send.</summary>
	public sealed class CareCreditBatchSubmission {
		///<summary>The screened patient: the appointment's patient, or the guarantor for a minor.</summary>
		public long PatNum;
		public long ClinicNum;
		public long AptNum;
		public CareCreditBatchRequest Request=new CareCreditBatchRequest();
	}

	///<summary>A patient the batch couldn't send, recorded as a BatchError row.</summary>
	public sealed class CareCreditBatchError {
		public long PatNum;
		public long ClinicNum;
		public string MerchantNumber="";
		public string Description="";
		public bool IsInvalidInput;
	}

	public sealed class CareCreditBatchPlan {
		public List<CareCreditBatchSubmission> ListSubmissions=new List<CareCreditBatchSubmission>();
		public List<CareCreditBatchError> ListErrors=new List<CareCreditBatchError>();
	}

	///<summary>Decides who the batch screens (carecredit.md 3.6), with no database access, for tests.</summary>
	public static class CareCreditBatchPlanner {
		///<summary>Rules, in order: a usable merchant number (by-provider mode: the provider's, else a BatchError); in "On (with Bal)" mode an estimated
		///patient portion over $1; minors replaced by an adult guarantor (none: BatchError); patients screened, completed or pending in the last
		///60 days skipped; one request per screened patient (the largest fee); name, street, city, state, ZIP and phone required (BatchError);
		///Lookup Only also needs the last four SSN digits. The fee is rounded to the nearest dollar.</summary>
		public static CareCreditBatchPlan Plan(List<CareCreditBatchCandidate> listCandidates,EnumCareCreditBatch mode,IEnumerable<long> listPatNumsExclude) {
			CareCreditBatchPlan plan=new CareCreditBatchPlan();
			if(mode==EnumCareCreditBatch.Off || listCandidates==null) {
				return plan;
			}
			HashSet<long> hashSetExclude=new HashSet<long>(listPatNumsExclude??Enumerable.Empty<long>());
			HashSet<long> hashSetErrored=new HashSet<long>();
			Dictionary<long,CareCreditBatchSubmission> dictSubmissions=new Dictionary<long,CareCreditBatchSubmission>();
			bool isLookupOnly=(mode==EnumCareCreditBatch.LookupOnly);
			foreach(CareCreditBatchCandidate candidate in listCandidates.OrderBy(x => x.AptDateTime)) {
				if(candidate.Patient==null) {
					continue;
				}
				if(hashSetExclude.Contains(candidate.Patient.PatNum)) {
					continue;
				}
				if(!CareCredit.IsMerchantNumValid(candidate.MerchantNumber,allowClosedMerchant:false)) {
					AddError(plan,hashSetErrored,candidate.Patient.PatNum,candidate,
						"Provider '"+candidate.ProviderAbbr+"' has no usable CareCredit merchant number for this clinic.",false);
					continue;
				}
				if(mode==EnumCareCreditBatch.On && candidate.EstimatedPatientPortion<=1) {
					continue;
				}
				Patient patientScreened=candidate.Patient;
				if(CareCredit.IsPatAMinor(candidate.Patient)) {
					Patient guarantor=candidate.Guarantor;
					if(guarantor==null || guarantor.PatNum==candidate.Patient.PatNum || CareCredit.IsPatAMinor(guarantor)) {
						AddError(plan,hashSetErrored,candidate.Patient.PatNum,candidate,"Minor patient with no adult guarantor.",true);
						continue;
					}
					patientScreened=guarantor;
				}
				if(hashSetExclude.Contains(patientScreened.PatNum) || hashSetErrored.Contains(patientScreened.PatNum)) {
					continue;
				}
				List<string> listMissing=GetMissingDemographics(patientScreened,isLookupOnly);
				if(listMissing.Count>0) {
					AddError(plan,hashSetErrored,patientScreened.PatNum,candidate,"Missing "+string.Join(", ",listMissing)+".",true);
					continue;
				}
				double fee=Math.Round(candidate.EstimatedFee,0,MidpointRounding.AwayFromZero);
				CareCreditBatchSubmission submission;
				if(dictSubmissions.TryGetValue(patientScreened.PatNum,out submission)) {
					if(fee>submission.Request.EstimatedFeeAmount) {
						submission.Request.EstimatedFeeAmount=fee;
					}
					continue;
				}
				submission=new CareCreditBatchSubmission();
				submission.PatNum=patientScreened.PatNum;
				submission.ClinicNum=candidate.ClinicNum;
				submission.AptNum=candidate.AptNum;
				submission.Request.MerchantNumber=candidate.MerchantNumber.Trim();
				submission.Request.IsLookupOnly=isLookupOnly;
				submission.Request.EstimatedFeeAmount=fee;
				submission.Request.Consumer=CareCreditConsumer.FromPatient(patientScreened,includeDateOfBirth:false,includeSsnLast4:isLookupOnly);
				dictSubmissions[patientScreened.PatNum]=submission;
				plan.ListSubmissions.Add(submission);
			}
			return plan;
		}

		private static void AddError(CareCreditBatchPlan plan,HashSet<long> hashSetErrored,long patNum,CareCreditBatchCandidate candidate,string description,
			bool isInvalidInput)
		{
			if(!hashSetErrored.Add(patNum)) {
				return;//One error row per patient per run.
			}
			CareCreditBatchError careCreditBatchError=new CareCreditBatchError();
			careCreditBatchError.PatNum=patNum;
			careCreditBatchError.ClinicNum=candidate.ClinicNum;
			careCreditBatchError.MerchantNumber=candidate.MerchantNumber??"";
			careCreditBatchError.Description=description;
			careCreditBatchError.IsInvalidInput=isInvalidInput;
			plan.ListErrors.Add(careCreditBatchError);
		}

		///<summary>The required fields that are blank: first and last name, street, city, state, ZIP, phone; plus SSN for Lookup Only.</summary>
		public static List<string> GetMissingDemographics(Patient patient,bool isLookupOnly) {
			List<string> listMissing=new List<string>();
			if(string.IsNullOrWhiteSpace(patient.FName)) {
				listMissing.Add("first name");
			}
			if(string.IsNullOrWhiteSpace(patient.LName)) {
				listMissing.Add("last name");
			}
			if(string.IsNullOrWhiteSpace(patient.Address)) {
				listMissing.Add("street");
			}
			if(string.IsNullOrWhiteSpace(patient.City)) {
				listMissing.Add("city");
			}
			if(string.IsNullOrWhiteSpace(patient.State)) {
				listMissing.Add("state");
			}
			if(string.IsNullOrWhiteSpace(patient.Zip)) {
				listMissing.Add("ZIP");
			}
			if(CareCreditConsumer.GetPhoneDigits(patient)=="") {
				listMissing.Add("phone");
			}
			if(isLookupOnly && CareCreditConsumer.GetSsnLast4(patient.SSN)=="") {
				listMissing.Add("SSN");
			}
			return listMissing;
		}
	}

	///<summary>Pulls back pending CareCredit results every minute (each row on its own schedule). Runs only while the program link is enabled, the
	///settings are complete and this is the computer chosen in CareCredit Setup.</summary>
	public sealed class CareCreditPollJob : IVendorBackgroundJob {
		public string Name {
			get {
				return "CareCreditPoll";
			}
		}

		public ProgramName ProgramName {
			get {
				return ProgramName.CareCredit;
			}
		}

		public TimeSpan Interval {
			get {
				return TimeSpan.FromMinutes(1);
			}
		}

		///<summary>How often a running pass renews its lease. Well inside Interval (the lease length), so the lease can't lapse between renewals
		///unless one pullback takes longer than the difference.</summary>
		public static readonly TimeSpan LeaseRenewInterval=TimeSpan.FromSeconds(20);
		///<summary>The lease the host took before Run. Replaced in tests.</summary>
		public IVendorJobLease Lease=new VendorJobLease();
		public Func<DateTime> FuncUtcNow=() => DateTime.UtcNow;

		public bool ShouldRun() {
			return CareCreditSettings.IsThisComputerJobHost() && CareCredit.IsUsable();
		}

		///<summary>The host holds the lease for one Interval only, and a pass at the batch pullback time can take longer. So the pass renews the lease
		///every LeaseRenewInterval and stops if another process has taken it. Payments don't depend on this: ApplyResult claims each row atomically.</summary>
		public void Run() {
			CareCreditJobs.PollPending(CareCredit.FuncNow(),funcKeepGoing:CreateKeepGoing());
		}

		///<summary>Asked before each pullback: true while this process still holds the lease, renewing it once LeaseRenewInterval has passed.</summary>
		public Func<bool> CreateKeepGoing() {
			DateTime dateTimeRenewedUtc=FuncUtcNow();
			return () => {
				if(FuncUtcNow()-dateTimeRenewedUtc<LeaseRenewInterval) {
					return true;
				}
				if(!Lease.TryAcquire(this,Interval)) {
					return false;
				}
				dateTimeRenewedUtc=FuncUtcNow();
				return true;
			};
		}
	}

	///<summary>Submits the day's batch QuickScreen once the process time of day has passed. Same conditions as CareCreditPollJob; the batch itself
	///runs once a day (CareCreditSettings.TryClaimBatchDay).</summary>
	public sealed class CareCreditBatchJob : IVendorBackgroundJob {
		public string Name {
			get {
				return "CareCreditBatch";
			}
		}

		public ProgramName ProgramName {
			get {
				return ProgramName.CareCredit;
			}
		}

		public TimeSpan Interval {
			get {
				return TimeSpan.FromMinutes(5);
			}
		}

		public bool ShouldRun() {
			return CareCreditSettings.IsThisComputerJobHost() && CareCredit.IsUsable() && DateTime.Now.TimeOfDay>=CareCreditSettings.GetProcessTime();
		}

		public void Run() {
			CareCreditJobs.RunBatch(CareCredit.FuncNow());
		}
	}
}
