using System;
using System.Collections.Generic;
using System.IO;
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

		#region Hosted email
		[TestMethod]
		public void HostedEmail_SignupsFromOpenDentalAreIgnored() {
			//Secure Email and Mass Email were hosted by Open Dental. These must answer "no" without reading the prefs a migrated database still has.
			foreach(long clinicNum in new List<long> { 0,1 }) {
				Assert.IsFalse(Clinics.IsSecureEmailSignedUp(clinicNum));
				Assert.IsFalse(Clinics.IsSecureEmailEnabled(clinicNum));
				Assert.IsFalse(Clinics.IsMassEmailSignedUp(clinicNum));
				Assert.IsFalse(Clinics.IsMassEmailEnabled(clinicNum));
			}
			Assert.IsFalse(EmailSecures.IsSecureEmailReleased());
		}
		#endregion Hosted email

		#region EHR
		[TestMethod]
		public void Ehr_ReflectionHooksAreGone() {
			Assert.AreEqual(0,EhrCodes.Listt.Count);
			Assert.AreEqual("",EhrSummaryCcds.GetEhrResource("CCD"));
			Assert.AreEqual("",EhrSummaryCcds.GetEhrResource("CCR"));
		}
		#endregion EHR

		#region Vendor neutrality
		[TestMethod]
		public void ToolButItems_OnlyEnabledProgramLinksGetButtons() {
			//Open Dental also showed buttons for disabled partner program links that have a "Disable Advertising" property (an advertisement
			//until the office turned it off). Only enabled program links get toolbar buttons now.
			List<OpenDentBusiness.Program> listPrograms=new List<OpenDentBusiness.Program> {
				new OpenDentBusiness.Program() { ProgramNum=1,ProgName=ProgramName.Apteryx.ToString(),ProgDesc="Apteryx",Enabled=true },
				new OpenDentBusiness.Program() { ProgramNum=2,ProgName=ProgramName.Pearl.ToString(),ProgDesc="Pearl",Enabled=false },
				new OpenDentBusiness.Program() { ProgramNum=3,ProgName=ProgramName.CareCredit.ToString(),ProgDesc="CareCredit",Enabled=false },
			};
			List<ProgramProperty> listProgramProperties=new List<ProgramProperty> {
				new ProgramProperty() { ProgramPropertyNum=1,ProgramNum=2,PropertyDesc=ProgramProperties.PropertyDescs.DisableAdvertising,PropertyValue="0" },
				new ProgramProperty() { ProgramPropertyNum=2,ProgramNum=3,PropertyDesc=ProgramProperties.PropertyDescs.DisableAdvertising,PropertyValue="0" },
				new ProgramProperty() { ProgramPropertyNum=3,ProgramNum=3,PropertyDesc=ProgramProperties.PropertyDescs.DisableAdvertisingHQ,PropertyValue="0" },
			};
			List<ToolButItem> listToolButItems=new List<ToolButItem>();
			foreach(OpenDentBusiness.Program program in listPrograms) {
				listToolButItems.Add(new ToolButItem() { ToolButItemNum=program.ProgramNum,ProgramNum=program.ProgramNum,ToolBar=EnumToolBar.MainToolbar,ButtonText=program.ProgDesc });
			}
			try {
				Programs.FillCacheFromTable(OpenDentBusiness.Crud.ProgramCrud.ListToTable(listPrograms));
				ProgramProperties.FillCacheFromTable(OpenDentBusiness.Crud.ProgramPropertyCrud.ListToTable(listProgramProperties));
				ToolButItems.FillCacheFromTable(OpenDentBusiness.Crud.ToolButItemCrud.ListToTable(listToolButItems));
				List<ToolButItem> listToolButItemsShown=ToolButItems.GetForToolBar(EnumToolBar.MainToolbar);
				CollectionAssert.AreEqual(new List<long> { 1 },listToolButItemsShown.Select(x => x.ProgramNum).ToList());
			}
			finally {
				Programs.FillCacheFromTable(OpenDentBusiness.Crud.ProgramCrud.ListToTable(new List<OpenDentBusiness.Program>()));
				ProgramProperties.FillCacheFromTable(OpenDentBusiness.Crud.ProgramPropertyCrud.ListToTable(new List<ProgramProperty>()));
				ToolButItems.FillCacheFromTable(OpenDentBusiness.Crud.ToolButItemCrud.ListToTable(new List<ToolButItem>()));
			}
		}

		[TestMethod]
		public void ProgramProperties_AdvertisingMechanismIsGone() {
			Assert.IsNull(typeof(ProgramProperties).GetMethod("IsAdvertisingDisabled",BindingFlags.Public|BindingFlags.Static));
			Assert.IsNull(typeof(ProgramProperties).GetMethod("IsAdvertisingBridge",BindingFlags.Public|BindingFlags.Static));
			//The stored "Disable Advertising" rows stay in the database for schema compatibility, so they are no longer deletable.
			foreach(string propertyDesc in new List<string> { ProgramProperties.PropertyDescs.DisableAdvertising,ProgramProperties.PropertyDescs.DisableAdvertisingHQ }) {
				Exception exception=null;
				try {
					ProgramProperties.Delete(new ProgramProperty() { ProgramPropertyNum=1,PropertyDesc=propertyDesc });
				}
				catch(Exception ex) {
					exception=ex;
				}
				Assert.IsNotNull(exception,propertyDesc+" should not be deletable.");
				StringAssert.Contains(exception.Message,"Not allowed to delete");
			}
		}
		[TestMethod]
		public void ControlChart_ErxToolbarButtonIsNullChecked() {
			//The Chart module only adds the eRx toolbar button once the eRx program link is enabled, and ODToolBarButtonCollection's
			//string indexer returns null for a missing button. ControlChart is WinForms (not referenced here), so this checks the source:
			//every member access on Buttons["eRx"] must sit inside an "if(ToolBarMain.Buttons["eRx"]!=null)" block.
			string pathControlChart=Path.Combine(FindRepoRoot(),"OpenDental","Main Modules","ControlChart.cs");
			string[] arrayLines=File.ReadAllLines(pathControlChart);
			int countAccesses=0;
			for(int i=0;i<arrayLines.Length;i++) {
				string line=arrayLines[i].Trim();
				if(!line.StartsWith("ToolBarMain.Buttons[\"eRx\"].")) {
					continue;
				}
				countAccesses++;
				string linePrevious=(i==0) ? "" : arrayLines[i-1].Trim();
				Assert.IsTrue(linePrevious.StartsWith("if(ToolBarMain.Buttons[\"eRx\"]!=null)"),
					"ControlChart.cs line "+(i+1)+" uses the eRx toolbar button without a null check: "+line);
			}
			Assert.IsTrue(countAccesses>0,"Expected to find eRx toolbar button accesses in ControlChart.cs.");
		}
		#endregion Vendor neutrality

		[TestMethod]
		public void HqNotAvailableException_IsAnODException() {
			Assert.IsInstanceOfType(new HqNotAvailableException(),typeof(ODException));
			StringAssert.Contains(HqNotAvailableException.ForVendor("CareCredit").Message,"CareCredit");
		}

		#region Helpers
		///<summary>Walks up from the test assembly's folder to the folder that holds OpenDental.sln.</summary>
		private static string FindRepoRoot() {
			DirectoryInfo directoryInfo=new DirectoryInfo(Path.GetDirectoryName(typeof(NoHqTests).Assembly.Location));
			while(directoryInfo!=null && !File.Exists(Path.Combine(directoryInfo.FullName,"OpenDental.sln"))) {
				directoryInfo=directoryInfo.Parent;
			}
			Assert.IsNotNull(directoryInfo,"Could not find the repository root (OpenDental.sln).");
			return directoryInfo.FullName;
		}

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
