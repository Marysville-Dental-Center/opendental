using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace NoHqTests {
	///<summary>Checks that Open Dental HQ's staff tools and the HQ-only plumbing stay removed: Job Manager, phones and call center maps, web chat,
	///triage, resellers, customer management, the OpenAI chat, remote support, HQ billing, the HQ database connections and the unused HQ web
	///service members. The tables of these tools stay in the database, so their table types and Crud classes must stay too.
	///Needs no database or network connection.</summary>
	[TestClass]
	public class HqToolsRemovedTests {
		///<summary>Data interfaces and helpers that only served Open Dental HQ's staff tools, relative to the business layer's namespace.</summary>
		private static readonly string[] _arrayRemovedTypes=new string[] {
			"Jobs",
			"JobLinks",
			"JobPermissions",
			"JobTeams",
			"JobTeamUsers",
			"Bugs",
			"BugSubmissionHashes",
			"FeatureRequests",
			"Phones",
			"PhoneEmpDefaults",
			"PhoneComps",
			"PhoneConfs",
			"MapAreas",
			"VoiceMails",
			"TriageMetrics",
			"WebChatSessions",
			"WebChatMessages",
			"WebChatMisc",
			"OpenAiClient",
			"PeerInfos",
			"CloudBillings",
			"APIBillings",
			"SmsBillings",
			"EServiceMetrics",
			"EServiceBillings",
			"EServiceInvoiceLineItem",
			"SiteLinks",
			"TaskTakens",
			"EhrQuarterlyKeys",
			"WebServiceMainHQMockDemo",
			"WebServiceMainHQ.WebServiceMainHQ",
			"customerUpdates.Service1",
			"localhost.Service1",
		};

		///<summary>Table types and Crud classes of HQ-only tables, relative to the business layer's namespace. The tables are not dropped, so these
		///must stay.</summary>
		private static readonly string[] _arrayKeptTypes=new string[] {
			"Job",
			"JobLink",
			"Bug",
			"BugSubmission",
			"Phone",
			"PhoneEmpDefault",
			"WebChatSession",
			"SiteLink",
			"TaskTaken",
			"Reseller",
			"RegistrationKey",
			"EServiceBilling",
			"EServiceCodeLink",
			"Crud.EServiceBillingCrud",
			"Crud.JobCrud",
			"Crud.BugCrud",
			"Crud.PhoneCrud",
			"Crud.WebChatSessionCrud",
			"Crud.SiteLinkCrud",
			"Crud.TaskTakenCrud",
		};

		///<summary>Windows used only by Open Dental HQ staff, as file name prefixes in OpenPmsDental and WpfControlsOD.</summary>
		private static readonly string[] _arrayRemovedWindowPrefixes=new string[] {
			"FormBugSubmission","FormCustomerManagement","FormRegistrationKeyEdit","FormFeatureRequest","FormRequestEdit","FormUpdateInstallMsg",
			"FormResellers","FormResellerEdit","FormResellerServiceEdit","FormWebChat","FormAiChatSession","FormNewCropBilling","FormErxAccess",
			"FormEhrProvKeysCustomer","FormEhrProvKeyEditCust","FormEhrQuarterlyKeyEdit","FormPhoneNumbersManage","FormTimeCardBenefitsRp",
			"FormGraphEmployee","FormMapAreaEdit","FrmSiteLinkEdit","FormPrepaymentTool","FormReferenceSelect","FormReferenceEdit",
			"FormReferenceEntryEdit","FormJob","FormPhone","FormMap","FormVoiceMails",
		};

		///<summary>DataAction helpers that ran code against Open Dental HQ's own databases.</summary>
		private static readonly string[] _arrayRemovedDataActionMethods=new string[] {
			"RunBugsHQ","RunCustomers","RunManualPublisherHQ","RunWebChat","RunTriageHQ","RunRemoteSupport","RunEServices","RunHeadmaster",
			"GetBugsHQ","GetCustomers","GetHqConnection","GetHqConnections","ClearDictHqCentralConnections",
		};

		[TestMethod]
		public void HqTools_DataInterfacesAreRemoved() {
			TypeLookup.AssertRemoved(typeof(Patient),_arrayRemovedTypes,"should have been removed.");
		}

		[TestMethod]
		public void HqTools_TableTypesAndCrudAreKept() {
			TypeLookup.AssertKept(typeof(Patient),_arrayKeptTypes,"must stay, because its table stays in the database.");
		}

		[TestMethod]
		public void HqTools_FoldersAndProgramsAreRemoved() {
			string pathRoot=RemovedAssembliesTests.GetRepositoryRoot();
			foreach(string folder in new[] { Path.Combine("OpenPmsDental","InternalTools"),Path.Combine("WpfControlsOD","InternalTools"),"JobManagerDashboard","WebCamOD" }) {
				Assert.IsFalse(Directory.Exists(Path.Combine(pathRoot,folder)),folder+" should have been removed.");
			}
			//Only table types and Crud classes may remain in OpenPmsDentalBusiness\InternalTools.
			string pathBusinessTools=Path.Combine(pathRoot,"OpenPmsDentalBusiness","InternalTools");
			if(Directory.Exists(pathBusinessTools)) {
				string sep=Path.DirectorySeparatorChar.ToString();
				List<string> listFiles=Directory.GetFiles(pathBusinessTools,"*.cs",SearchOption.AllDirectories)
					.Where(x => !x.Contains(sep+"TableTypes"+sep) && !x.Contains(sep+"Crud"+sep) && !Path.GetFileName(x).EndsWith("Crud.cs"))
					.Where(x => !Path.GetFileName(x).In("EServiceBilling.cs","EServiceCodeLink.cs"))//Table types kept next to their old data interfaces.
					.ToList();
				Assert.AreEqual(0,listFiles.Count,"Unexpected files in OpenPmsDentalBusiness\\InternalTools: "+string.Join(", ",listFiles));
			}
			string[] arrayPathSolutions=Directory.GetFiles(pathRoot,"*.sln");//By extension, so the check survives a rename of the solution.
			Assert.AreEqual(1,arrayPathSolutions.Length,"Expected one solution in "+pathRoot);
			Assert.IsFalse(File.ReadAllText(arrayPathSolutions[0]).Contains("WebCamOD"),Path.GetFileName(arrayPathSolutions[0])+" still lists WebCamOD.");
		}

		[TestMethod]
		public void HqTools_WindowsAreRemoved() {
			string pathRoot=RemovedAssembliesTests.GetRepositoryRoot();
			string sep=Path.DirectorySeparatorChar.ToString();
			List<string> listFiles=new List<string>();
			foreach(string folder in new[] { "OpenPmsDental","WpfControlsOD" }) {
				listFiles.AddRange(Directory.GetFiles(Path.Combine(pathRoot,folder),"*.*",SearchOption.AllDirectories)
					.Where(x => x.EndsWith(".cs",StringComparison.OrdinalIgnoreCase) || x.EndsWith(".xaml",StringComparison.OrdinalIgnoreCase))
					.Where(x => !x.Contains(sep+"bin"+sep) && !x.Contains(sep+"obj"+sep)));
			}
			for(int i=0;i<_arrayRemovedWindowPrefixes.Length;i++) {
				string fileFound=listFiles.FirstOrDefault(x => Path.GetFileName(x).StartsWith(_arrayRemovedWindowPrefixes[i],StringComparison.OrdinalIgnoreCase));
				Assert.IsNull(fileFound,fileFound+" should have been removed.");
			}
		}

		[TestMethod]
		public void HqTools_DataActionHqHelpersAreRemoved() {
			List<string> listMethodNames=typeof(DataAction).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Select(x => x.Name).ToList();
			for(int i=0;i<_arrayRemovedDataActionMethods.Length;i++) {
				Assert.IsFalse(listMethodNames.Contains(_arrayRemovedDataActionMethods[i]),"DataAction."+_arrayRemovedDataActionMethods[i]+" should have been removed.");
			}
			//The dental office helpers stay.
			Assert.IsTrue(listMethodNames.Contains("RunPractice"));
			Assert.IsTrue(listMethodNames.Contains("RunMiddleTierMock"));
		}

		[TestMethod]
		public void HqTools_ConnectionNamesKeepOnlyTheDentalOffice() {
			CollectionAssert.AreEquivalent(new[] { "None","DentalOffice","DentalOfficeReportServer","DentalOffice_19_1","DentalOfficeReadOnlyServer" },
				Enum.GetNames(typeof(ConnectionNames)));
			//A ConnectionStore config file may name a connection by number, so the kept values keep their original numbers.
			Assert.AreEqual(4,(int)ConnectionNames.DentalOffice);
			Assert.AreEqual(5,(int)ConnectionNames.DentalOfficeReportServer);
			Assert.AreEqual(13,(int)ConnectionNames.DentalOffice_19_1);
			Assert.AreEqual(18,(int)ConnectionNames.DentalOfficeReadOnlyServer);
			Assert.IsNull(typeof(ConnectionStoreBase).GetField("GetTriageHQ",BindingFlags.Public|BindingFlags.Static));
		}

		[TestMethod]
		public void HqTools_HostedServiceMembersOfTheHqWebServiceAreRemoved() {
			List<string> listMembers=typeof(IWebServiceMainHQ).GetMethods().Select(x => x.Name).ToList();
			foreach(string name in new[] { "EServiceSetup","EnableAdditionalFeatures","SubmitUnhandledException","GetManualPage","CreateNewHelpKey",
				"ValidateVersion","GetEServiceClinicsAllowed","SetEConnectorStatsAsync","TestConnection","get_Url" })
			{
				Assert.IsFalse(listMembers.Contains(name),"IWebServiceMainHQ."+name+" should have been removed.");
			}
			//The HQ-brokered vendor members were deleted once each integration talked to its vendor directly. NoHqTests checks that they stay gone.
			List<string> listCustomerUpdates=typeof(ICustomerUpdates).GetMethods().Select(x => x.Name).ToList();
			foreach(string name in new[] { "RequestUpdate","FeatureRequestGetList","RequestCodeSystems","RequestFeeSched","RequestIsDevKey","DatabaseIntegrityGetList" }) {
				Assert.IsFalse(listCustomerUpdates.Contains(name),"ICustomerUpdates."+name+" should have been removed.");
			}
			Assert.IsNull(typeof(CustomerUpdatesProxy).GetMethod("SendAndReceiveUpdateRequestXml"));
			Assert.IsNull(typeof(WebServiceMainHQProxy).GetMethod("CanReachWebService"));
		}

		[TestMethod]
		public void HqTools_BugSubmissionsKeepsOnlyLocalLogging() {
			List<string> listMethodNames=typeof(BugSubmissions).GetMethods(BindingFlags.Public|BindingFlags.Static).Select(x => x.Name).ToList();
			foreach(string name in new[] { "GetAll","GetOne","Insert","TryInsertBugSubmission","Update","UpdateMany","Delete","GetFilterPhrases" }) {
				Assert.IsFalse(listMethodNames.Contains(name),"BugSubmissions."+name+" read or wrote Open Dental HQ's bugs database and should have been removed.");
			}
			Assert.IsTrue(listMethodNames.Contains("SubmitException"));
		}
	}
}
