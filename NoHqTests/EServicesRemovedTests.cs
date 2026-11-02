using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace NoHqTests {
	///<summary>Checks that Open Dental's hosted eServices stay removed: the eConnector, Web Sched, eClipboard, the patient portal, Mobile Web, ODTouch,
	///Integrated Texting, hosted Secure Email and Mass Email, the payment portal, Message-to-Pay, Web Forms and automated messaging.
	///The tables of these services stay in the database, so their table types and Crud classes must stay too.
	///Needs no database or network connection.</summary>
	[TestClass]
	public class EServicesRemovedTests {
		///<summary>Data interfaces and web types that only served the hosted eServices, relative to the business layer's namespace.</summary>
		private static readonly string[] _arrayRemovedTypes=new string[] {
			"ApptReminderRules",
			"ApptReminderSents",
			"AsapComms",
			"AutoCommExcludeDates",
			"ConfirmationRequests",
			"EClipboardImageCaptures",
			"EClipboardSheetDefs",
			"EmailHostingTemplates",
			"EmailSecures",
			"ERoutings",
			"EServiceLogs",
			"EServiceSignals",
			"MobileAppDevices",
			"MobileNotifications",
			"MsgToPaySents",
			"PatientPortalInvites",
			"Promotions",
			"SessionTokens",
			"SmsBlockPhones",
			"SmsFromMobiles",
			"SmsPhones",
			"SmsToMobiles",
			"WebSchedRecalls",
			"SheetsSynchProxy",
			"WebTypes.WebForms.WebForms_Sheets",
		};

		///<summary>Table types and Crud classes of eService tables, relative to the business layer's namespace. The tables are not dropped, so
		///these must stay.</summary>
		private static readonly string[] _arrayKeptTypes=new string[] {
			"ApptReminderRule",
			"EClipboardSheetDef",
			"EmailSecure",
			"EServiceSignal",
			"MobileAppDevice",
			"SmsPhone",
			"SmsToMobile",
			"WebSchedRecall",
			"Crud.ApptReminderRuleCrud",
			"Crud.EClipboardSheetDefCrud",
			"Crud.EServiceSignalCrud",
			"Crud.MobileAppDeviceCrud",
			"Crud.SmsPhoneCrud",
			"Crud.WebSchedRecallCrud",
		};

		///<summary>Calls to Open Dental HQ for eService sign-ups, hosted email, short URLs and mobile settings.</summary>
		private static readonly string[] _arrayRemovedProxyMethods=new string[] {
			"SetEConnectorOn","BuildWebSchedNewPatApptURLs","GetEServiceSetupFull","CreateHostedEmailCredentials","GetEServiceSetupLite",
			"GetEServiceClinicsAllowed","IsEServiceClinicAllowed","IsEServiceAllowed","GetSignups","IsEServiceActive","GetShortGUIDs",
			"GetMobileSettings","GetMobileSettings2FA","UpsertMobileSettings","ProcessOCRIIDRequest",
		};

		///<summary>Windows of the hosted eServices, as file name prefixes in OpenPmsDental and WpfControlsOD.</summary>
		private static readonly string[] _arrayRemovedWindowPrefixes=new string[] {
			"FormEServices","FormWebSched","FormEClipboard","FrmEClipboard","FormWebForms","FormWebFormSetup","FormMassEmail","FormEmailHosting",
			"FormSecureEmailSetup","FormSmsTextMessaging","FormTxtMsgMany","FrmShortCodeOptIn","FormPatientPortal","FormMessageToPayEdit",
			"FormEservicesPaymentPortal","FormODTouchSecurityEdit","FormMobileBrandingProfileEdit","FormERouting","FrmERouting",
		};

		[TestMethod]
		public void EServices_DataInterfacesAreRemoved() {
			TypeLookup.AssertRemoved(typeof(Patient),_arrayRemovedTypes,"should have been removed.");
		}

		[TestMethod]
		public void EServices_TableTypesAndCrudAreKept() {
			TypeLookup.AssertKept(typeof(Patient),_arrayKeptTypes,"must stay, because its table stays in the database.");
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
			listFiles.AddRange(GetSourceFiles(Path.Combine(pathRoot,"OpenPmsDental")));
			listFiles.AddRange(GetSourceFiles(Path.Combine(pathRoot,"WpfControlsOD")));
			for(int i=0;i<_arrayRemovedWindowPrefixes.Length;i++) {
				string fileFound=listFiles.FirstOrDefault(x => Path.GetFileName(x).StartsWith(_arrayRemovedWindowPrefixes[i],StringComparison.OrdinalIgnoreCase));
				Assert.IsNull(fileFound,fileFound+" should have been removed.");
			}
		}

		///<summary>The eConnector and the local API service were installed by PrefL. The local Open Dental Service stays.</summary>
		[TestMethod]
		public void EServices_EConnectorInstallIsRemoved() {
			string path=Path.Combine(RemovedAssembliesTests.GetRepositoryRoot(),"OpenPmsDental","Logic","PrefL.cs");
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
