using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace NoHqTests {
	///<summary>Checks the "no HQ" stand-ins that replaced Open Dental's HQ web services, the removed gates and the removed telemetry.
	///None of these tests needs a database or a network connection.</summary>
	[TestClass]
	public class NoHqTests {

		[TestInitialize]
		public void SetUp() {
			WebServiceMainHQProxy.MockWebServiceMainHQ=null;
			CustomerUpdatesProxy.MockCustomerUpdates=null;
			SheetsSynchProxy.MockSheetSynchService=null;
			BugSubmissions.MockBugSubmissions=null;
		}

		#region WebServiceMainHQ
		[TestMethod]
		public void WebServiceMainHQ_FactoryReturnsNoHq() {
			Assert.IsInstanceOfType(WebServiceMainHQProxy.GetWebServiceMainHQInstance(),typeof(WebServiceMainHQNoHq));
			//The URL argument is ignored. Nothing ever connects to it.
			Assert.IsInstanceOfType(WebServiceMainHQProxy.GetWebServiceMainHQInstance("https://example.invalid/WebServiceMainHQ.asmx"),
				typeof(WebServiceMainHQNoHq));
			Assert.IsFalse(WebServiceMainHQProxy.IsHqAvailable);
		}

		[TestMethod]
		public void WebServiceMainHQ_EveryDataMethodThrowsHqNotAvailable() {
			IWebServiceMainHQ webService=WebServiceMainHQProxy.GetWebServiceMainHQInstance();
			List<MethodInfo> listMethods=typeof(IWebServiceMainHQ).GetMethods()
				.Where(x => x.ReturnType==typeof(string) && !x.IsSpecialName)
				.ToList();
			Assert.IsTrue(listMethods.Count>80,"Expected the full IWebServiceMainHQ surface.");
			foreach(MethodInfo method in listMethods) {
				AssertThrowsHqNotAvailable(() => method.Invoke(webService,GetDefaultArgs(method)),method.Name);
			}
		}

		[TestMethod]
		public void WebServiceMainHQ_NoClinicIsSignedUpForAnyEService() {
			List<long> listClinicNums=new List<long> { 0,1,2 };
			Assert.AreEqual(0,WebServiceMainHQProxy.GetWebServiceMainHQInstance().GetEServiceClinicsAllowed(listClinicNums,eServiceCode.IntegratedTexting).Count);
			Assert.AreEqual(0,WebServiceMainHQProxy.GetEServiceClinicsAllowed(listClinicNums,eServiceCode.WebSched).Count);
			Assert.IsFalse(WebServiceMainHQProxy.IsEServiceClinicAllowed(0,eServiceCode.ConfirmationRequest));
		}

		[TestMethod]
		public void WebServiceMainHQ_UploadsAreDropped() {
			IWebServiceMainHQ webService=WebServiceMainHQProxy.GetWebServiceMainHQInstance();
			webService.SetEConnectorStatsAsync("<Request/>");//Must not throw.
			webService.ProcessEConnectorFailoverAsync("<Request/>");//Must not throw.
		}

		[TestMethod]
		public void WebServiceMainHQ_VendorMethodsSayDirectIntegrationIsNeeded() {
			IWebServiceMainHQ webService=WebServiceMainHQProxy.GetWebServiceMainHQInstance();
			List<Action> listVendorCalls=new List<Action> {
				() => webService.GetCareCreditOAuthToken(""),
				() => webService.GetPostcardManiaSSO(""),
				() => webService.ProcessUSPSAddressValidationRequest(""),
				() => webService.GetGoogleAccessToken(""),
				() => webService.BuildOAuthUrl("","Dropbox"),
				() => webService.SmsSend(""),
				() => webService.GetPaySimpleWebHookUrl(),
			};
			foreach(Action action in listVendorCalls) {
				HqNotAvailableException ex=AssertThrowsHqNotAvailable(action,"vendor call");
				StringAssert.Contains(ex.Message,"direct vendor integration");
			}
		}

		[TestMethod]
		public void WebServiceMainHQ_HelpersFailWithoutTouchingTheNetwork() {
			Assert.AreEqual(DateTime.MinValue,WebServiceMainHQProxy.GetCareCreditBatchProcessTime());
			Assert.AreEqual(DateTime.MinValue,WebServiceMainHQProxy.GetCareCreditBatchPullbackTime());
			AssertThrowsHqNotAvailable(() => WebServiceMainHQProxy.ProcessUSPSAddressValidationRequest(new Address()),"USPS");
			AssertThrowsHqNotAvailable(() => WebServiceMainHQProxy.GetEServiceSetupFull(SignupPortalPermission.ReadOnly),"GetEServiceSetupFull");
		}

		[TestMethod]
		public void WebServiceMainHQ_MockTakesPriority() {
			FakeWebServiceMainHQ mock=new FakeWebServiceMainHQ();
			WebServiceMainHQProxy.MockWebServiceMainHQ=mock;
			Assert.AreSame(mock,WebServiceMainHQProxy.GetWebServiceMainHQInstance());
			Assert.IsTrue(WebServiceMainHQProxy.IsHqAvailable);
		}
		#endregion WebServiceMainHQ

		#region CustomerUpdates and SheetsSynch
		[TestMethod]
		public void CustomerUpdates_FactoryReturnsNoHqAndEveryMethodThrows() {
			ICustomerUpdates customerUpdates=CustomerUpdatesProxy.GetWebServiceInstance();
			Assert.IsInstanceOfType(customerUpdates,typeof(CustomerUpdatesNoHq));
			foreach(MethodInfo method in typeof(ICustomerUpdates).GetMethods()) {
				AssertThrowsHqNotAvailable(() => method.Invoke(customerUpdates,GetDefaultArgs(method)),method.Name);
			}
			AssertThrowsHqNotAvailable(() => CustomerUpdatesProxy.SendAndReceiveUpdateRequestXml(),"SendAndReceiveUpdateRequestXml");
		}

		[TestMethod]
		public void SheetsSynch_FactoryReturnsNoHqAndEveryMethodThrows() {
			ISheetsSynch sheetsSynch=SheetsSynchProxy.GetWebServiceInstance();
			Assert.IsInstanceOfType(sheetsSynch,typeof(SheetsSynchNoHq));
			foreach(MethodInfo method in typeof(ISheetsSynch).GetMethods()) {
				AssertThrowsHqNotAvailable(() => method.Invoke(sheetsSynch,GetDefaultArgs(method)),method.Name);
			}
		}
		#endregion CustomerUpdates and SheetsSynch

		#region Gates and telemetry
		[TestMethod]
		public void BugSubmissions_SubmitException_SendsNothing() {
			BugSubmissionResult result=BugSubmissions.SubmitException(new Exception("NoHqTests"),out string displayMsg,"NoHqTests",patNumCur:5);
			Assert.AreEqual(BugSubmissionResult.None,result);
			Assert.IsNull(displayMsg);
			Assert.AreEqual(BugSubmissionResult.None,BugSubmissions.SubmitException(null));
		}

		[TestMethod]
		public void Programs_IsEnabledByHq_IgnoresHqFlags() {
			OpenDentBusiness.Program program=new OpenDentBusiness.Program() {
				ProgName=ProgramName.PDMP.ToString(),
				IsDisabledByHq=true,
				CustErr="Disabled by HQ",
			};
			Assert.IsTrue(Programs.IsEnabledByHq(program,out string err));
			Assert.AreEqual("",err);
			Assert.IsFalse(Programs.IsEnabledByHq((OpenDentBusiness.Program)null,out err));
		}

		[TestMethod]
		public void HqProgram_IsNeverFilled() {
			Assert.IsFalse(HqProgram.IsInitialized());
			Assert.AreEqual(0,HqProgram.GetAll().Count);
		}

		[TestMethod]
		public void PrefC_IsODHQ_IsAlwaysFalse() {
			Assert.IsFalse(PrefC.IsODHQ);
		}
		#endregion Gates and telemetry

		#region EHR
		[TestMethod]
		public void Ehr_ReflectionHooksAreGone() {
			Assert.AreEqual(0,EhrCodes.Listt.Count);
			Assert.AreEqual("",EhrSummaryCcds.GetEhrResource("CCD"));
			Assert.AreEqual("",EhrSummaryCcds.GetEhrResource("CCR"));
		}
		#endregion EHR

		[TestMethod]
		public void HqNotAvailableException_IsAnODException() {
			Assert.IsInstanceOfType(new HqNotAvailableException(),typeof(ODException));
			StringAssert.Contains(HqNotAvailableException.ForVendor("CareCredit").Message,"CareCredit");
		}

		#region Helpers
		///<summary>Runs the action and asserts that it throws HqNotAvailableException, unwrapping reflection's TargetInvocationException.</summary>
		private static HqNotAvailableException AssertThrowsHqNotAvailable(Action action,string name) {
			try {
				action();
			}
			catch(TargetInvocationException ex) when(ex.InnerException is HqNotAvailableException) {
				return (HqNotAvailableException)ex.InnerException;
			}
			catch(HqNotAvailableException ex) {
				return ex;
			}
			catch(Exception ex) {
				Assert.Fail(name+" threw "+ex.GetType().Name+" instead of HqNotAvailableException: "+ex.Message);
			}
			Assert.Fail(name+" did not throw HqNotAvailableException.");
			return null;
		}

		private static object[] GetDefaultArgs(MethodInfo method) {
			return method.GetParameters()
				.Select(x => x.ParameterType.IsValueType ? Activator.CreateInstance(x.ParameterType) : (x.ParameterType==typeof(string) ? "" : null))
				.ToArray();
		}
		#endregion Helpers
	}

	///<summary>Minimal test double. Derives from the no-HQ implementation so it does not need every interface member, and is only used to check
	///that a mock set by a unit test takes priority over the default.</summary>
	public class FakeWebServiceMainHQ:WebServiceMainHQNoHq {
	}
}
