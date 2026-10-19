using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace NoHqTests {
	///<summary>Checks that Open Dental's hosted eServices stay removed: the eConnector, Web Sched, eClipboard, the patient portal, Mobile Web, ODTouch,
	///Integrated Texting, hosted Secure Email and Mass Email, the payment portal, Message-to-Pay, Web Forms and automated messaging.
	///The tables of these services stay in the database, so their table types and Crud classes must stay too.
	///Needs no database or network connection.</summary>
	[TestClass]
	public class EServicesRemovedTests {
		///<summary>Data interfaces and web types that only served the hosted eServices.</summary>
		private static readonly string[] _arrayRemovedTypes=new string[] {
			"OpenDentBusiness.ApptReminderRules",
			"OpenDentBusiness.ApptReminderSents",
			"OpenDentBusiness.AsapComms",
			"OpenDentBusiness.AutoCommExcludeDates",
			"OpenDentBusiness.ConfirmationRequests",
			"OpenDentBusiness.EClipboardImageCaptures",
			"OpenDentBusiness.EClipboardSheetDefs",
			"OpenDentBusiness.EmailHostingTemplates",
			"OpenDentBusiness.EmailSecures",
			"OpenDentBusiness.ERoutings",
			"OpenDentBusiness.EServiceLogs",
			"OpenDentBusiness.EServiceSignals",
			"OpenDentBusiness.MobileAppDevices",
			"OpenDentBusiness.MobileNotifications",
			"OpenDentBusiness.MsgToPaySents",
			"OpenDentBusiness.PatientPortalInvites",
			"OpenDentBusiness.Promotions",
			"OpenDentBusiness.SessionTokens",
			"OpenDentBusiness.SmsBlockPhones",
			"OpenDentBusiness.SmsFromMobiles",
			"OpenDentBusiness.SmsPhones",
			"OpenDentBusiness.SmsToMobiles",
			"OpenDentBusiness.WebSchedRecalls",
			"OpenDentBusiness.SheetsSynchProxy",
			"OpenDentBusiness.WebTypes.WebForms.WebForms_Sheets",
		};

		///<summary>Table types and Crud classes of eService tables. The tables are not dropped, so these must stay.</summary>
		private static readonly string[] _arrayKeptTypes=new string[] {
			"OpenDentBusiness.ApptReminderRule",
			"OpenDentBusiness.EClipboardSheetDef",
			"OpenDentBusiness.EmailSecure",
			"OpenDentBusiness.EServiceSignal",
			"OpenDentBusiness.MobileAppDevice",
			"OpenDentBusiness.SmsPhone",
			"OpenDentBusiness.SmsToMobile",
			"OpenDentBusiness.WebSchedRecall",
			"OpenDentBusiness.Crud.ApptReminderRuleCrud",
			"OpenDentBusiness.Crud.EClipboardSheetDefCrud",
			"OpenDentBusiness.Crud.EServiceSignalCrud",
			"OpenDentBusiness.Crud.MobileAppDeviceCrud",
			"OpenDentBusiness.Crud.SmsPhoneCrud",
			"OpenDentBusiness.Crud.WebSchedRecallCrud",
		};

		///<summary>Calls to Open Dental HQ for eService sign-ups, hosted email, short URLs and mobile settings.</summary>
		private static readonly string[] _arrayRemovedProxyMethods=new string[] {
			"SetEConnectorOn","BuildWebSchedNewPatApptURLs","GetEServiceSetupFull","CreateHostedEmailCredentials","GetEServiceSetupLite",
			"GetEServiceClinicsAllowed","IsEServiceClinicAllowed","IsEServiceAllowed","GetSignups","IsEServiceActive","GetShortGUIDs",
			"GetMobileSettings","GetMobileSettings2FA","UpsertMobileSettings","ProcessOCRIIDRequest",
		};

		///<summary>Windows of the hosted eServices, as file name prefixes in OpenDental and WpfControlsOD.</summary>
		private static readonly string[] _arrayRemovedWindowPrefixes=new string[] {
			"FormEServices","FormWebSched","FormEClipboard","FrmEClipboard","FormWebForms","FormWebFormSetup","FormMassEmail","FormEmailHosting",
			"FormSecureEmailSetup","FormSmsTextMessaging","FormTxtMsgMany","FrmShortCodeOptIn","FormPatientPortal","FormMessageToPayEdit",
			"FormEservicesPaymentPortal","FormODTouchSecurityEdit","FormMobileBrandingProfileEdit","FormERouting","FrmERouting",
		};

		[TestMethod]
		public void EServices_DataInterfacesAreRemoved() {
			Assembly assembly=typeof(Patient).Assembly;
			for(int i=0;i<_arrayRemovedTypes.Length;i++) {
				Assert.IsNull(assembly.GetType(_arrayRemovedTypes[i]),_arrayRemovedTypes[i]+" should have been removed.");
			}
		}

		[TestMethod]
		public void EServices_TableTypesAndCrudAreKept() {
			Assembly assembly=typeof(Patient).Assembly;
			for(int i=0;i<_arrayKeptTypes.Length;i++) {
				Assert.IsNotNull(assembly.GetType(_arrayKeptTypes[i]),_arrayKeptTypes[i]+" must stay, because its table stays in the database.");
			}
		}

		[TestMethod]
		public void EServices_HqProxyMethodsAreRemoved() {
			List<string> listMethodNames=typeof(WebServiceMainHQProxy)
				.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)
				.Select(x => x.Name)
				.ToList();
			for(int i=0;i<_arrayRemovedProxyMethods.Length;i++) {
				Assert.IsFalse(listMethodNames.Contains(_arrayRemovedProxyMethods[i]),"WebServiceMainHQProxy."+_arrayRemovedProxyMethods[i]+" should have been removed.");
			}
		}

		[TestMethod]
		public void EServices_WindowsAreRemoved() {
			string pathRoot=RemovedAssembliesTests.GetRepositoryRoot();
			List<string> listFiles=new List<string>();
			listFiles.AddRange(GetSourceFiles(Path.Combine(pathRoot,"OpenDental")));
			listFiles.AddRange(GetSourceFiles(Path.Combine(pathRoot,"WpfControlsOD")));
			for(int i=0;i<_arrayRemovedWindowPrefixes.Length;i++) {
				string fileFound=listFiles.FirstOrDefault(x => Path.GetFileName(x).StartsWith(_arrayRemovedWindowPrefixes[i],StringComparison.OrdinalIgnoreCase));
				Assert.IsNull(fileFound,fileFound+" should have been removed.");
			}
		}

		///<summary>The eConnector and the local API service were installed by PrefL. The local Open Dental Service stays.</summary>
		[TestMethod]
		public void EServices_EConnectorInstallIsRemoved() {
			string path=Path.Combine(RemovedAssembliesTests.GetRepositoryRoot(),"OpenDental","Logic","PrefL.cs");
			string text=File.ReadAllText(path);
			Assert.IsFalse(text.Contains("UpgradeOrInstallEConnector"),"PrefL.UpgradeOrInstallEConnector should have been removed.");
			Assert.IsFalse(text.Contains("TryInstallEConnector"),"PrefL.TryInstallEConnector should have been removed.");
			Assert.IsFalse(text.Contains("TryInstallOpenDentalApiService"),"PrefL.TryInstallOpenDentalApiService should have been removed.");
			Assert.IsTrue(text.Contains("TryInstallOpenDentalService"),"PrefL.TryInstallOpenDentalService must stay.");
		}

		///<summary>Source files (.cs and .xaml) below the folder, skipping build output.</summary>
		private static List<string> GetSourceFiles(string pathFolder) {
			string sep=Path.DirectorySeparatorChar.ToString();
			return Directory.GetFiles(pathFolder,"*.*",SearchOption.AllDirectories)
				.Where(x => x.EndsWith(".cs",StringComparison.OrdinalIgnoreCase) || x.EndsWith(".xaml",StringComparison.OrdinalIgnoreCase))
				.Where(x => !x.Contains(sep+"bin"+sep) && !x.Contains(sep+"obj"+sep))
				.ToList();
		}
	}
}
