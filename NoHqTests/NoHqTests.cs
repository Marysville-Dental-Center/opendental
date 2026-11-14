using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace NoHqTests {
	///<summary>Checks the "no HQ" stand-ins that replaced Open Dental's HQ web services, the removed gates and the removed telemetry.
	///None of these tests needs a database or a network connection.</summary>
	[TestClass]
	public class NoHqTests {

		[TestInitialize]
		public void SetUp() {
			WebServiceMainHQProxy.MockWebServiceMainHQ=null;
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
			Assert.IsTrue(listMethods.Count>0,"Expected the remaining hosted texting members of IWebServiceMainHQ.");
			foreach(MethodInfo method in listMethods) {
				AssertThrowsHqNotAvailable(() => method.Invoke(webService,GetDefaultArgs(method)),method.Name);
			}
		}

		[TestMethod]
		public void WebServiceMainHQ_VendorMembersAreRemoved() {
			//Each vendor integration Open Dental brokered through HQ now talks to its vendor directly (Google, QuickBooks Online, PayConnect, PaySimple,
			//USPS, PostcardMania, CareCredit) or was removed (Dropbox, the Canadian carrier update). Their HQ members were deleted with their last caller.
			string[] arrayRemovedMembers=new string[] { "BuildOAuthUrl",
				"GetDropboxAccessToken","GetDropboxAuthorizationUrl",
				"GetGoogleAccessToken","GetGoogleAuthorizationUrl","GetGoogleAuthorizationUrlLoopbackIpAddressFlow",
				"GetCareCreditOAuthToken","GetCareCreditWebToken","LogCareCreditTransaction","GetCareCreditBatchTimes",
				"GetAdvertisingPostcardsAccounts","GetPostcardManiaSSO","ManageAdvertisingPostcardsAccount","UploadPostcardManiaPatientList",
				"InsertPaySimpleACHId","GetPaySimpleWebHookUrl",
				"ProcessUSPSAddressValidationRequest",
				"CanadaCarrierUpdate" };
			AssertMembersRemoved(new List<Type> { typeof(IWebServiceMainHQ),typeof(WebServiceMainHQNoHq),typeof(WebServiceMainHQProxy) },arrayRemovedMembers);
			string[] arrayRemovedProxyHelpers=new string[] { "GetAdvertisingPostcardsAccountData","GetAdvertisingPostcardsSSO",
				"ManageAdvertisingPostcardsAccount","UploadAdvertisingPostcardsPatientList","GetCareCreditBatchProcessTime","GetCareCreditBatchPullbackTime",
				"GetSerializedProgramProperties","ProcessUSPSAddressValidationRequest" };
			AssertMembersRemoved(new List<Type> { typeof(WebServiceMainHQProxy) },arrayRemovedProxyHelpers);
			Assert.IsNull(typeof(WebServiceMainHQProxy).GetNestedType("ClinicProgramPropertyContainer",BindingFlags.Public|BindingFlags.NonPublic),
				"WebServiceMainHQProxy.ClinicProgramPropertyContainer carried CareCredit merchant numbers to HQ and should have been removed.");
			Assert.IsNull(typeof(CareCreditWebResponses).GetField("MERCHANT_ID_PROG_PROPERTY",BindingFlags.Public|BindingFlags.Static),
				"CareCreditWebResponses.MERCHANT_ID_PROG_PROPERTY only named the merchant numbers sent to HQ and should have been removed.");
		}

		[TestMethod]
		public void WebServiceMainHQ_ProxyNoLongerUsesBridgesTypes() {
			//The PostcardMania helpers were the last reason for "using Bridges;" here. Bridges.dll itself is deleted (vendor-direct package Z2).
			string source=File.ReadAllText(Path.Combine(FindRepoRoot(),"OpenPmsDentalBusiness","WebServices","WebServiceMainHQProxy.cs"));
			Assert.IsFalse(source.Contains("using Bridges;"),"WebServiceMainHQProxy.cs should not import Bridges.dll types.");
			Assert.IsFalse(source.Contains("PostcardMania"),"WebServiceMainHQProxy.cs should not name PostcardMania types.");
		}

		[TestMethod]
		public void WebServiceMainHQ_NoProductCodeCallsHq() {
			//Every HQ-brokered vendor flow was replaced, so only the factory itself (and unit tests) may still ask for the HQ web service.
			string root=FindRepoRoot();
			List<string> listOffenders=new List<string>();
			foreach(string folder in new[] { "OpenPmsDentalBusiness","OpenPmsDental","WpfControlsOD","CodeBase" }) {
				foreach(string path in Directory.EnumerateFiles(Path.Combine(root,folder),"*.cs",SearchOption.AllDirectories)) {
					string relativePath=path.Substring(root.Length+1);
					if(relativePath.Contains(@"\obj\") || relativePath.Contains(@"\bin\") || relativePath.Contains(@"\Web References\")
						|| relativePath.StartsWith(@"OpenPmsDentalBusiness\WebServices\",StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					if(File.ReadAllText(path).Contains("GetWebServiceMainHQInstance(")) {
						listOffenders.Add(relativePath);
					}
				}
			}
			Assert.AreEqual(0,listOffenders.Count,"Product code calls the HQ web service: "+string.Join(", ",listOffenders));
		}

		[TestMethod]
		public void WebServiceMainHQ_MockTakesPriority() {
			FakeWebServiceMainHQ mock=new FakeWebServiceMainHQ();
			WebServiceMainHQProxy.MockWebServiceMainHQ=mock;
			Assert.AreSame(mock,WebServiceMainHQProxy.GetWebServiceMainHQInstance());
			Assert.IsTrue(WebServiceMainHQProxy.IsHqAvailable);
		}
		#endregion WebServiceMainHQ

		#region CustomerUpdates
		///<summary>Open Dental's "Customer Updates" web service is gone with its stand-in (owner, DIRECT-CERT-FOLLOWUP): every member had been
		///deleted with its last caller (the Canadian network certificates come from the office's own files, eRx access is decided locally, the
		///OID root is generated locally, the Direct certificate is imported from the Direct provider's file, O-13), which left ICustomerUpdates,
		///CustomerUpdatesNoHq and CustomerUpdatesProxy empty. Their file and every reference to them are deleted too.</summary>
		[TestMethod]
		public void CustomerUpdates_TheEmptyShellIsRemoved() {
			string[] arrayRemovedTypes=new string[] { "ICustomerUpdates","CustomerUpdatesNoHq","CustomerUpdatesProxy" };
			TypeLookup.AssertRemoved(typeof(WebServiceMainHQProxy),arrayRemovedTypes,"was the empty shell of the Customer Updates web service and "
				+"should have been removed.");
			string root=FindRepoRoot();
			Assert.IsFalse(File.Exists(Path.Combine(root,"OpenPmsDentalBusiness","WebServices","CustomerUpdatesProxy.cs")),
				"CustomerUpdatesProxy.cs should have been deleted.");
			//No code names the types any more. The tests that check a source file does not name them (eRx, OID, Canada, QuickBooks Online, the
			//Direct certificate window) and this file are left out.
			List<string> listOffenders=new List<string>();
			foreach(string folder in new[] { "OpenPmsDentalBusiness","OpenPmsDental","WpfControlsOD","CodeBase","OpenPmsDentalServer","UnitTests" }) {
				string pathFolder=Path.Combine(root,folder);
				Assert.IsTrue(Directory.Exists(pathFolder),pathFolder+" is not there, so the scan would check nothing in it.");
				foreach(string path in Directory.EnumerateFiles(pathFolder,"*.cs",SearchOption.AllDirectories)) {
					string relativePath=path.Substring(root.Length+1);
					if(relativePath.Contains(@"\obj\") || relativePath.Contains(@"\bin\")) {
						continue;
					}
					string text=File.ReadAllText(path);
					if(arrayRemovedTypes.Any(x => Regex.IsMatch(text,@"\b"+x+@"\b"))) {
						listOffenders.Add(relativePath);
					}
				}
			}
			Assert.AreEqual(0,listOffenders.Count,"Code still names the removed Customer Updates types: "+string.Join(", ",listOffenders));
		}
		#endregion CustomerUpdates

		#region Gates and telemetry
		[TestMethod]
		public void BugSubmissions_SubmitException_SendsNothing() {
			BugSubmissionResult result=BugSubmissions.SubmitException(new Exception("NoHqTests"),out string displayMsg,"NoHqTests",patNumCur:5);
			Assert.AreEqual(BugSubmissionResult.None,result);
			Assert.IsNull(displayMsg);
			Assert.AreEqual(BugSubmissionResult.None,BugSubmissions.SubmitException(null));
		}

		[TestMethod]
		public void Programs_IsEnabledByHq_IsRemoved() {
			//Programs.IsEnabledByHq was HQ's kill switch for program links. It became an always-true shim, then was inlined and removed.
			Assert.IsNull(typeof(Programs).GetMethod("IsEnabledByHq",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static));
		}

		[TestMethod]
		public void HqProgram_IsNeverFilled() {
			Assert.IsFalse(HqProgram.IsInitialized());
			Assert.AreEqual(0,HqProgram.GetAll().Count);
		}

		[TestMethod]
		public void PrefC_IsODHQ_IsRemoved() {
			//PrefC.IsODHQ turned on Open Dental HQ's staff tools. It was pinned to false, then inlined and removed.
			Assert.IsNull(typeof(PrefC).GetProperty("IsODHQ",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static));
		}
		#endregion Gates and telemetry

		//The EHR.dll hooks: tests/EhrTests/NoEhrDllTests.cs (package EHR-0 of docs/specs/ehr-rebuild-plan.md). Its source scan replaced
		//Ehr_ReflectionHooksAreGone, whose assertions (an empty code list, empty CCD and CCR stylesheets) the open rebuilds EHR-2 and EHR-3 make false.

		#region Vendor neutrality
		[TestMethod]
		public void ToolButItems_OnlyEnabledProgramLinksGetButtons() {
			//Open Dental also showed buttons for disabled partner program links that have a "Disable Advertising" property (an advertisement
			//until the office turned it off). Only enabled program links get toolbar buttons now.
			List<OpenPmsDentalBusiness.Program> listPrograms=new List<OpenPmsDentalBusiness.Program> {
				new OpenPmsDentalBusiness.Program() { ProgramNum=1,ProgName=ProgramName.Apteryx.ToString(),ProgDesc="Apteryx",Enabled=true },
				new OpenPmsDentalBusiness.Program() { ProgramNum=2,ProgName=ProgramName.Pearl.ToString(),ProgDesc="Pearl",Enabled=false },
				new OpenPmsDentalBusiness.Program() { ProgramNum=3,ProgName=ProgramName.CareCredit.ToString(),ProgDesc="CareCredit",Enabled=false },
			};
			List<ProgramProperty> listProgramProperties=new List<ProgramProperty> {
				new ProgramProperty() { ProgramPropertyNum=1,ProgramNum=2,PropertyDesc=ProgramProperties.PropertyDescs.DisableAdvertising,PropertyValue="0" },
				new ProgramProperty() { ProgramPropertyNum=2,ProgramNum=3,PropertyDesc=ProgramProperties.PropertyDescs.DisableAdvertising,PropertyValue="0" },
				new ProgramProperty() { ProgramPropertyNum=3,ProgramNum=3,PropertyDesc=ProgramProperties.PropertyDescs.DisableAdvertisingHQ,PropertyValue="0" },
			};
			List<ToolButItem> listToolButItems=new List<ToolButItem>();
			foreach(OpenPmsDentalBusiness.Program program in listPrograms) {
				listToolButItems.Add(new ToolButItem() { ToolButItemNum=program.ProgramNum,ProgramNum=program.ProgramNum,ToolBar=EnumToolBar.MainToolbar,ButtonText=program.ProgDesc });
			}
			try {
				Programs.FillCacheFromTable(OpenPmsDentalBusiness.Crud.ProgramCrud.ListToTable(listPrograms));
				ProgramProperties.FillCacheFromTable(OpenPmsDentalBusiness.Crud.ProgramPropertyCrud.ListToTable(listProgramProperties));
				ToolButItems.FillCacheFromTable(OpenPmsDentalBusiness.Crud.ToolButItemCrud.ListToTable(listToolButItems));
				List<ToolButItem> listToolButItemsShown=ToolButItems.GetForToolBar(EnumToolBar.MainToolbar);
				CollectionAssert.AreEqual(new List<long> { 1 },listToolButItemsShown.Select(x => x.ProgramNum).ToList());
			}
			finally {
				Programs.FillCacheFromTable(OpenPmsDentalBusiness.Crud.ProgramCrud.ListToTable(new List<OpenPmsDentalBusiness.Program>()));
				ProgramProperties.FillCacheFromTable(OpenPmsDentalBusiness.Crud.ProgramPropertyCrud.ListToTable(new List<ProgramProperty>()));
				ToolButItems.FillCacheFromTable(OpenPmsDentalBusiness.Crud.ToolButItemCrud.ListToTable(new List<ToolButItem>()));
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
			string pathControlChart=Path.Combine(FindRepoRoot(),"OpenPmsDental","Main Modules","ControlChart.cs");
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
		///<summary>The repository root (the folder with LICENSE and NOTICE), from this project's one locator.</summary>
		private static string FindRepoRoot() {
			return RemovedAssembliesTests.GetRepositoryRoot();
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

		///<summary>Asserts that none of the types declares a method with any of the names.</summary>
		private static void AssertMembersRemoved(List<Type> listTypes,string[] arrayNames) {
			for(int i=0;i<listTypes.Count;i++) {
				List<string> listMethodNames=listTypes[i]
					.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)
					.Select(x => x.Name)
					.ToList();
				for(int j=0;j<arrayNames.Length;j++) {
					Assert.IsFalse(listMethodNames.Contains(arrayNames[j]),listTypes[i].Name+"."+arrayNames[j]+" should have been removed.");
				}
			}
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
