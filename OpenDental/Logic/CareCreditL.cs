using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CodeBase;
using CodeBase.VendorDirect;
using Microsoft.Web.WebView2.Core;
using OpenDentBusiness;
using OpenDentBusiness.Bridges;

namespace OpenDental {
	public class CareCreditL {
		private const string LOG_FOLDER="CareCredit";
		private const string SESSION_POLLER_GROUP="CareCreditSessionPoll";
		///<summary>How often this session's poller looks for due rows. Each row keeps its own schedule (CareCreditPollSchedule).</summary>
		private static readonly TimeSpan SessionPollInterval=TimeSpan.FromMinutes(1);
		private static readonly object _lockSessionPoller=new object();
		private static ODThread _odThreadSessionPoller;

		///<summary>Opens the CareCredit Admin page.</summary>
		public static string LaunchAdminPage(string merchantNum,string userName=null,bool showPage=true) {
			string url=TryLaunchCareCreditPage(CareCreditLandingPage.HM,null,0,0,CareCredit.IsMerchantNumberByProv,serviceType:EnumCareCreditPrefillServiceType.A,userName:userName,merchantNum:merchantNum,doShowPage:showPage);
			return url;
		}

		///<summary>Opens the CareCredit Admin page.</summary>
		public static void LaunchAdminPage(long provNum,long clinicNum,bool isProvOverride,string userName=null) {
			TryLaunchCareCreditPage(CareCreditLandingPage.HM,null,provNum,clinicNum,isProvOverride:isProvOverride,serviceType:EnumCareCreditPrefillServiceType.A,userName:userName);
		}

		///<summary>Opens the CareCredit Credit Application request page. Staff at California locations may not submit applications for patients;
		///there the patient is told to apply directly with CareCredit and nothing is sent.</summary>
		public static void LaunchCreditApplicationPage(Patient patient,long provNum,long clinicNum,double purchaseAmt,double estimatedFeeAmt,
			string practiceMemo=null,string userName=null,string oldRefId=null)
		{
			if(IsApplicationRestrictedShowMessage(clinicNum)) {
				return;
			}
			TryLaunchCareCreditPage(CareCreditLandingPage.AP,patient,provNum,clinicNum,CareCredit.IsMerchantNumberByProv,purchaseAmt:purchaseAmt,
				estimatedFeeAmt:estimatedFeeAmt,noteMemo:practiceMemo,userName:userName,oldRefId:oldRefId);
		}

		///<summary>Opens the CareCredit Lookup request page.</summary>
		public static void LaunchLookupPage(Patient patient,long provNum,long clinicNum,string userName=null,string oldRefId=null) {
			TryLaunchCareCreditPage(CareCreditLandingPage.LU,patient,provNum,clinicNum,CareCredit.IsMerchantNumberByProv,userName:userName,oldRefId:oldRefId,serviceType:EnumCareCreditPrefillServiceType.L);
		}

		///<summary>Returns the Purchase request webpage url.</summary>
		public static string GetPurchasePageUrl(Patient patient,long provNum,long clinicNum,double purchaseAmt,double estimatedFeeAmt,string invoiceNum=null,
			string practiceMemo=null,string promoCode=null,string userName=null,string oldRefId=null,long payNum=0) {
			return TryLaunchCareCreditPage(CareCreditLandingPage.PR,patient,provNum,clinicNum,CareCredit.IsMerchantNumberByProv,purchaseAmt:purchaseAmt,
				estimatedFeeAmt:estimatedFeeAmt,invoiceNumber:invoiceNum,noteMemo:practiceMemo,promoCode:promoCode,userName:userName,oldRefId:oldRefId,payNum:payNum,
				doShowPage:false);
		}

		///<summary>Returns the QuickScreen request webpage url.</summary>
		public static string GetQSPageUrl(Patient patient,long provNum,long clinicNum,double estimatedFeeAmt,string invoiceNum=null,string practiceMemo=null,string userName=null) {
			if(IsApplicationRestrictedShowMessage(clinicNum)) {
				return "";
			}
			CareCreditWebResponse careCreditWebResponseBatchOriginal=CareCreditWebResponses.GetOriginalCompletedBatchForPatNum(patient);
			string refId=careCreditWebResponseBatchOriginal?.RefNumber??null;
			return TryLaunchCareCreditPage(CareCreditLandingPage.QS,patient,provNum,clinicNum,CareCredit.IsMerchantNumberByProv,estimatedFeeAmt:estimatedFeeAmt,
				invoiceNumber:invoiceNum,noteMemo:practiceMemo,userName:userName,oldRefId:refId,doShowPage:false);
		}

		///<summary>Opens the CareCredit quickscreen individual request page (accepting a pre-approval leads to an application, so the California rule
		///applies here too).</summary>
		public static void LaunchQuickScreenIndividualPage(Patient patient,long provNum,long clinicNum,double estimatedFeeAmt,string invoiceNum=null,
			string practiceMemo=null,string userName=null)
		{
			if(IsApplicationRestrictedShowMessage(clinicNum)) {
				return;
			}
			CareCreditWebResponse careCreditWebResponseBatchOriginal=CareCreditWebResponses.GetOriginalCompletedBatchForPatNum(patient);
			string refId=careCreditWebResponseBatchOriginal?.RefNumber??null;
			TryLaunchCareCreditPage(CareCreditLandingPage.QS,patient,provNum,clinicNum,CareCredit.IsMerchantNumberByProv,estimatedFeeAmt:estimatedFeeAmt,
				invoiceNumber:invoiceNum,noteMemo:practiceMemo,userName:userName,oldRefId:refId);
		}

		///<summary>Opens the CareCredit Refund request page.</summary>
		public static void RefundTransaction(CareCreditWebResponse careCreditWebResponse) {
			if(careCreditWebResponse==null) {
				MsgBox.Show("FormPayment","CareCreditWebResponse is null or no longer exists. Request could not be completed.");
				return;
			}
			string practiceMemo=$"Refund PayNum {careCreditWebResponse.PayNum}";
			TryLaunchCareCreditPage(CareCreditLandingPage.RF,Patients.GetPat(careCreditWebResponse.PatNum),0,careCreditWebResponse.ClinicNum,CareCredit.IsMerchantNumberByProv,
				refundAmt:careCreditWebResponse.Amount,invoiceNumber:careCreditWebResponse.PayNum.ToString(),noteMemo:practiceMemo,userName:CareCredit.GetUserName(careCreditWebResponse),
				oldRefId:careCreditWebResponse.RefNumber,merchantNum:careCreditWebResponse.MerchantNumber);
		}

		///<summary>Opens the CareCredit Refund request page.</summary>
		public static void LaunchRefundPage(Patient patient,long provNum,long clinicNum,double refundAmt,string invoiceNum=null,string practiceMemo=null,
			string promoCode=null,string userName=null,string oldRefId=null)
		{
			TryLaunchCareCreditPage(CareCreditLandingPage.RF,patient,provNum,clinicNum,CareCredit.IsMerchantNumberByProv,refundAmt:refundAmt,
				invoiceNumber:invoiceNum,noteMemo:practiceMemo,promoCode:promoCode,userName:userName,oldRefId:oldRefId);
		}

		///<summary>Opens the CareCredit Reports page.</summary>
		public static void LaunchReportsPage(long provNum,long clinicNum,string userName=null) {
			TryLaunchCareCreditPage(CareCreditLandingPage.HM,null,provNum,clinicNum,CareCredit.IsMerchantNumberByProv,userName:userName,serviceType:EnumCareCreditPrefillServiceType.R);
		}

		///<summary>True, after telling the user, when staff may not submit credit applications at this clinic (California).</summary>
		private static bool IsApplicationRestrictedShowMessage(long clinicNum) {
			if(!CareCredit.IsStaffApplicationRestricted(clinicNum)) {
				return false;
			}
			MsgBox.Show("CareCreditL","At this location, staff may not submit CareCredit applications for patients. The patient must apply directly with CareCredit.");
			return true;
		}

		///<summary>Tries to send prefill request with the specified landing page and launches the CareCredit portal. Set doShowPage to false if you don't want the page to be displayed. Displays error messages to user.</summary>
		private static string TryLaunchCareCreditPage(CareCreditLandingPage careCreditLandingPage,Patient patient,long provNum,long clinicNum,bool isProvOverride,
			string invoiceNumber=null,double purchaseAmt=0,double refundAmt=0,string noteMemo=null,string promoCode=null,string userName=null,
			double estimatedFeeAmt=0,string oldRefId=null,EnumCareCreditPrefillServiceType serviceType=EnumCareCreditPrefillServiceType.C,long payNum=0,string merchantNum=null,bool doShowPage=true)
		{
			if(!CareCredit.IsUsable()) {
				//Disabled or not configured: say so, and send nothing.
				MsgBox.Show("CareCreditL",CareCredit.NOT_CONFIGURED_MESSAGE);
				return "";
			}
			if(string.IsNullOrEmpty(merchantNum)) {//No merchant number passed in
				try {
					merchantNum=CareCredit.GetMerchantNumber(clinicNum,provNum,isProvOverride);
				}
				catch(ODException oDException) {
					if(isProvOverride) {
						provNum=TryGetNewProvOrRegisterProv(provNum,clinicNum);
						if(provNum>-1) {//a new provider was selected
							return TryLaunchCareCreditPage(careCreditLandingPage,patient,provNum,clinicNum,isProvOverride,invoiceNumber,purchaseAmt,refundAmt,noteMemo,promoCode,userName,
								estimatedFeeAmt,oldRefId,serviceType,payNum:payNum,doShowPage:doShowPage);//No need to pass merchantNum because a new provider was selected.
						}
					}
					else {
						FriendlyException.Show(oDException.Message,oDException);
					}
					return "";
				}
				catch(Exception ex) {
					FriendlyException.Show("Unexpected Error. Please call support.",ex);
					return "";
				}
			}
			try {
				string pageUrl;
				Cursor cursorOld=Cursor.Current;
				Cursor.Current=Cursors.WaitCursor;
				try {
					pageUrl=CareCredit.SendPrefillReturnPageUrl(careCreditLandingPage,patient,invoiceNumber,purchaseAmt,refundAmt,noteMemo,promoCode,userName,estimatedFeeAmt,
						oldRefId,merchantNum,serviceType,payNum,clinicNum:clinicNum);
				}
				finally {
					Cursor.Current=cursorOld;
				}
				StartSessionPoller();
				if(doShowPage) {
					ShowPage(pageUrl,patient);
				}
				return pageUrl;
			}
			catch(CareCredit.CareCreditException careCreditException) {
				string errorMsg="CareCredit returned ";
				switch(careCreditException.ErrorCode) {
					case 0:
						errorMsg=careCreditException.Message;
						break;
					case 400:
						errorMsg+="an Invalid Input";
						break;
					case 401:
						errorMsg=careCreditException.Message;
						break;
					case 404:
						errorMsg+="a Request URL Not Found";
						break;
					case 422:
						errorMsg+="a status of Training Suspended. Please contact CareCredit for more information.";
						break;
					case 429:
						errorMsg="CareCredit is busy. Try again in a few minutes.";
						break;
					case 500:
						errorMsg+="an Internal Server Error";
						break;
					default:
						errorMsg+="an Unknown Error";
						break;
				}
				FriendlyException.Show(errorMsg,careCreditException);
			}
			catch(ODException oDException) {
				FriendlyException.Show(oDException.Message,oDException);
			}
			catch(Exception ex) {
				FriendlyException.Show("There was an issue with the request.",ex);
			}
			return "";
		}

		///<summary>Shows a CareCredit hosted page in a non-modal window, or in the default browser when WebView2 isn't installed. The result is pulled
		///back when the window closes (PullbackOnClose), and after that by the job computer's poller, or by this session's own poller when no job
		///computer is set (StartSessionPoller). Refuses any address that isn't on the hosted-page host set in CareCredit Setup.</summary>
		public static void ShowPage(string url,Patient patient=null) {
			if(string.IsNullOrEmpty(url)) {
				return;
			}
			if(!CareCredit.IsHostedPageUrl(url)) {
				MsgBox.Show("CareCreditL","The CareCredit page address is not on the hosted-page host set in CareCredit Setup.");
				return;
			}
			if(!IsWebView2Available()) {
				try {
					SystemBrowser.Open(url);
				}
				catch(Exception ex) {
					FriendlyException.Show("Could not open the default browser.",ex);
				}
				return;
			}
			FormCareCreditWeb formCareCreditWeb=new FormCareCreditWeb(url,patient);
			formCareCreditWeb.DoPullbackOnClose=true;
			formCareCreditWeb.Show();
		}

		///<summary>Called when a non-modal CareCredit window closes: pulls back the row for its session on a background thread and applies the result
		///(patient fields, or the payment for a purchase finished in the window). Errors are logged, never shown; the pollers try again later.</summary>
		public static void PullbackOnClose(string sessionId) {
			if(string.IsNullOrEmpty(sessionId) || !CareCredit.IsUsable()) {
				return;
			}
			ODThread odThread=new ODThread((o) => {
				try {
					CareCreditWebResponse careCreditWebResponse=CareCreditWebResponses.GetBySessionId(sessionId);
					CareCreditJobs.PullbackNow(careCreditWebResponse,CareCredit.FuncNow());
				}
				catch(CareCredit.CareCreditException ex) {
					Logger.WriteLine("Pullback when the window closed failed: HTTP "+ex.ErrorCode+": "+ex.Message,LOG_FOLDER);
				}
				catch(ODException ex) {
					Logger.WriteLine("Pullback when the window closed failed: "+ex.Message,LOG_FOLDER);
				}
			});
			odThread.AddExceptionHandler((e) => Logger.WriteLine("Pullback when the window closed failed: "+e.GetType().Name+": "+e.Message,LOG_FOLDER));
			odThread.Name="CareCreditPullbackOnClose";
			odThread.Start();
		}

		///<summary>Starts this session's poller if it isn't running. Once a minute, while no computer is chosen in CareCredit Setup, it pulls back the
		///rows this session created (CareCreditJobs.PollSessionRows), so self-service purchases still become payments and QuickScreen and lookup
		///results still reach the patient. It quits when no such rows are left; the next prefill starts it again.</summary>
		private static void StartSessionPoller() {
			lock(_lockSessionPoller) {
				if(_odThreadSessionPoller!=null && !_odThreadSessionPoller.HasQuit) {
					return;
				}
				ODThread odThread=new ODThread((int)SessionPollInterval.TotalMilliseconds,(o) => {
					if(!CareCreditJobs.HasSessionRows) {
						o.QuitAsync();
						return;
					}
					CareCreditJobs.PollSessionRows(CareCredit.FuncNow());
				});
				odThread.AddExceptionHandler((e) => Logger.WriteLine("Session poll failed: "+e.GetType().Name+": "+e.Message,LOG_FOLDER));
				odThread.GroupName=SESSION_POLLER_GROUP;
				odThread.Name=SESSION_POLLER_GROUP;
				_odThreadSessionPoller=odThread;
				odThread.Start();
			}
		}

		private static bool IsWebView2Available() {
			try {
				return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>Tells the user that the provider has no Merchant Number and lets them select a different provider that has one.
		///Returns the selected provider, or -1 if the user cancels or no provider has a Merchant Number.</summary>
		private static long TryGetNewProvOrRegisterProv(long provNum,long clinicNum) {
			long provNumNew=-1;
			string msg=$"{Lan.g("CareCreditL","Provider")} '{Providers.GetAbbr(provNum)}' "
					+Lan.g("CareCreditL","does not have a usable Merchant Number.\r\nSelect a different provider?");
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,msg,"CareCredit")) {
				return provNumNew;
			}
			List<Provider> listProvsWithOverrides=Providers.GetProvsWithCareCreditOverrides(clinicNum);
			if(listProvsWithOverrides.IsNullOrEmpty()) {
				MsgBox.Show("CareCreditL","No providers with CareCredit Merchant Numbers found.");
				return provNumNew;
			}
			FrmProviderPick frmProvPick=new FrmProviderPick(listProvsWithOverrides);
			frmProvPick.ShowDialog();
			if(!frmProvPick.IsDialogOK) {
				return provNumNew;
			}
			provNumNew=frmProvPick.ProvNumSelected;
			return provNumNew;
		}

		public static void LaunchPurchasePage(Patient patient,double purchaseAmt) {
			TryLaunchCareCreditPage(CareCreditLandingPage.PR, patient, patient.PriProv, patient.ClinicNum, CareCredit.IsMerchantNumberByProv, purchaseAmt:purchaseAmt);
		}
	}
}
