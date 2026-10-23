using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace RegistryTests {
	///<summary>Pins every enum value that Open Dental 24.4 to 25.3 added, renamed or removed (and that package M1-02 owns) to its published number.
	///One table per enum. Numbers from https://www.opendental.com/OpenDentalDocumentation25-3.xml and the 24.4 to 25.3 diff documents.</summary>
	[TestClass]
	public class EnumPinTests {
		private static void AssertPinned<T>(params (string Name,int Number)[] arrayPins) where T:Enum {
			foreach((string name,int number) in arrayPins) {
				Assert.IsTrue(Enum.GetNames(typeof(T)).Contains(name),typeof(T).Name+"."+name+" is missing.");
				Assert.AreEqual(number,Convert.ToInt32(Enum.Parse(typeof(T),name)),typeof(T).Name+"."+name);
				//The number must map back to this name (no other member shares it).
				Assert.AreEqual(name,Enum.GetName(typeof(T),number),typeof(T).Name+" "+number+" maps to another name.");
			}
		}

		private static void AssertNotDefined<T>(params string[] arrayNames) where T:Enum {
			foreach(string name in arrayNames) {
				Assert.IsFalse(Enum.GetNames(typeof(T)).Contains(name),typeof(T).Name+"."+name+" should no longer exist.");
			}
		}

		[TestMethod]
		public void EnumPermType_24_4_To_25_3() {
			AssertPinned<EnumPermType>(
				("Blockouts",13),("AppointmentCreate",25),("AppointmentMove",26),("ImageDelete",44),("ImageEdit",89),("InsWriteOffEdit",119),
				("PayPlanEdit",149),("ProcExistingEdit",163),
				("ClaimProcFieldsBilledToInsEdit",252),("SuperFamilyDisband",263),
				("ImageSignatureCreate",264),("SignedImageEdit",265),("BlockoutsFlagged",266),("PayPlanUnlock",267),("SendAlertsFromHQ",268),
				("TextAllEmployees",269),("ProcTPEditFee",270),("EFormImport",271),("PhoneExtension",272));
			AssertNotDefined<EnumPermType>("ClaimProcFeeBilledToInsEdit");
			Assert.AreEqual(272,Enum.GetValues(typeof(EnumPermType)).Cast<int>().Max(),"25.3 ends at 272. 273 and up belong to 25.4 (M2-02).");
		}

		[TestMethod]
		public void EnumPermType_IsContiguous() {
			//The security tree's Set All and Set None loop over 0..count-1, so the values must have no gaps.
			int[] arrayValues=Enum.GetValues(typeof(EnumPermType)).Cast<int>().OrderBy(x => x).ToArray();
			for(int i=0;i<arrayValues.Length;i++) {
				Assert.AreEqual(i,arrayValues[i]);
			}
		}

		[TestMethod]
		public void AlertType_And_FormType_25_1() {
			AssertPinned<AlertType>(("Pearl",40),("BetterDiagnostics",41),("MessageToPayTag",42),("MassEmailUpload",43),("MassEmailReceipt",44),
				("HQNotification",45));
			AssertPinned<FormType>(("FormEServicesAutoMsging",18),("FrmStatementSendSetup",19),("FormAdvertisingMassEmailUpload",20));
		}

		[TestMethod]
		public void CommItemSource_EtransType_InvalidType() {
			AssertPinned<CommItemSource>(("MsgToPay",9),("MassEmail",10));
			AssertPinned<EtransType>(("AttachmentAck_CA",35),("Claim_XConnect",36),("ClaimAck_XConnect",37));
			AssertPinned<InvalidType>(("WebChatAiAssistants",115),("MassEmail",116));
		}

		[TestMethod]
		public void EnumEFormStatus_RemovedValues() {
			AssertPinned<EnumEFormStatus>(("None",0),("ShowInEClipboard",1),("Filled",2));
			AssertNotDefined<EnumEFormStatus>("Imported","UnmatchedToPatient");
			Assert.IsFalse(Enum.IsDefined(typeof(EnumEFormStatus),3));
			Assert.IsFalse(Enum.IsDefined(typeof(EnumEFormStatus),4));
		}

		[TestMethod]
		public void SheetType_DisplayField_Perio_Pearl_UserOdPref() {
			AssertPinned<SheetTypeEnum>(("None",27),("Check",28));
			AssertPinned<DisplayFieldCategory>(("SuperFamilyGridCols",21),("AccountModuleServiceDateView",22),("ERA",23));
			AssertPinned<PerioSequenceType>(("CAL",7),("AttGing",8));
			AssertPinned<EnumPearlStatus>(("Uploading",4),("ErrorUploading",5),("ErrorProcessing",6),("NoResults",7));
			AssertPinned<UserOdFkeyType>(("ImageSelectorWidth",27),("DefaultMapSetting",28));
		}

		[TestMethod]
		public void Renames_InsBlueBookRuleType_SchedStatus() {
			AssertPinned<InsBlueBookRuleType>(("ManualBlueBookSchedule",4),("ProviderFee",5));
			AssertNotDefined<InsBlueBookRuleType>("UcrFee");
			Assert.AreEqual("Provider Fee",InsBlueBookRuleType.ProviderFee.GetDescription());
			AssertPinned<SchedStatus>(("Open",0),("Override",1),("Holiday",2));
			AssertNotDefined<SchedStatus>("Closed");
		}

		[TestMethod]
		public void ReservedValues_ForCutServices() {
			AssertPinned<ApptReminderType>(("PayPortalMsgToPay",10),("EClipboardWeb",11));
			AssertPinned<SmsMessageSource>(("OptOutReply",28),("EClipboardWeb",29));
			AssertPinned<SessionTokenType>(("PatientPortalVerifyUser",4),("TwoFactorAuth",5));
			AssertPinned<eServiceType>(("PaymentPortal",13),("BetterDiag",14),("MassEmail",15),("Pearl",16));
			AssertPinned<eServiceAction>(("SelectedPatient",105),("WebAppError",117),("BetterDiagPass",118),("BetterDiagFail",119),
				("BetterDiagException",120),("ECCheckinErrorEFormToImaging",121),("MassEmailUpload",122),("MassEmailUploadError",123),
				("MassEmailReceipt",124),("OcrInsuranceCardScannerError",125),("PearlSentImage",126),("AppLoaded",127),
				("VerificationMethodSelected",128),("InsuranceInfoPresented",129),("ApptReasonSelected",130),("ApptConfirmPresented",131),
				("ApptConfirmAttempted",132),("ScheduleAnotherAppt",133));
			AssertNotDefined<eServiceAction>("PayPortalSelectedPatient","PayPortalPaymentPortalError");
			AssertPinned<eServiceCode>(("UrlRedirect",49),("DeveloperPortalApi",50),("DeveloperPortalUI",51),("EmailHqService",52),
				("BetterDiagnostics",53),("EmailMassAccess",54),("WebSchedExistingPatientUI",55),("WebSchedExistingPatientApi",56));
		}

		[TestMethod]
		public void ReservedValues_AreHiddenWhereHqValuesAreHidden() {
			//Alert category setup, for example, removes every value for which GenericTools.IsODHQ is true.
			foreach(AlertType alertType in new[] { AlertType.BetterDiagnostics,AlertType.MessageToPayTag,AlertType.MassEmailUpload,AlertType.MassEmailReceipt,
				AlertType.HQNotification })
			{
				Assert.IsTrue(GenericTools.IsODHQ(alertType),alertType+" must be hidden from alert category setup.");
			}
			Assert.IsFalse(GenericTools.IsODHQ(AlertType.Pearl));
			Assert.IsTrue(ReservedEnumValueAttribute.IsReserved(FormType.FrmStatementSendSetup));
			Assert.IsTrue(ReservedEnumValueAttribute.IsReserved(FormType.FormAdvertisingMassEmailUpload));
			Assert.IsFalse(ReservedEnumValueAttribute.IsReserved(FormType.FormModuleSetup));
			Assert.IsTrue(ReservedEnumValueAttribute.IsReserved(ApptReminderType.EClipboardWeb));
			Assert.IsTrue(ReservedEnumValueAttribute.IsReserved(SmsMessageSource.EClipboardWeb));
			Assert.IsTrue(ReservedEnumValueAttribute.IsReserved(SessionTokenType.TwoFactorAuth));
			Assert.IsTrue(GenericTools.IsODHQ(SchedStatus.Override));
			Assert.IsTrue(GenericTools.IsODHQ(UserOdFkeyType.DefaultMapSetting));
		}

		[TestMethod]
		public void EFormStatusCompat_LoadsRemovedValuesAsFilled() {
			Assert.AreEqual(EnumEFormStatus.None,EFormStatusCompat.FromDb(0));
			Assert.AreEqual(EnumEFormStatus.ShowInEClipboard,EFormStatusCompat.FromDb(1));
			Assert.AreEqual(EnumEFormStatus.Filled,EFormStatusCompat.FromDb(2));
			Assert.AreEqual(EnumEFormStatus.Filled,EFormStatusCompat.FromDb(3));
			Assert.AreEqual(EnumEFormStatus.Filled,EFormStatusCompat.FromDb(4));
			CollectionAssert.AreEqual(new[] { 3,4 },EFormStatusCompat.ArrayRemovedValues);
		}

		///<summary>For the enums this package changed, the code must not keep members that Open Dental 25.3 no longer has (old names of renamed
		///values, removed values). UI-only members that are never stored are listed as exceptions.</summary>
		[TestMethod]
		public void ChangedEnums_HaveNoMembersOutsideThePublishedList() {
			Dictionary<Type,string[]> dictExceptions=new Dictionary<Type,string[]> {
				{ typeof(EnumPermType),new[] { "EditReadOnlyTasks" } },//227, HQ only. The published document skips members that have no summary comment.
				{ typeof(AlertType),new string[0] },
				{ typeof(FormType),new string[0] },
				{ typeof(CommItemSource),new string[0] },
				{ typeof(EtransType),new string[0] },
				{ typeof(InvalidType),new string[0] },
				{ typeof(EnumEFormStatus),new string[0] },
				{ typeof(SheetTypeEnum),new string[0] },
				{ typeof(DisplayFieldCategory),new string[0] },
				{ typeof(InsBlueBookRuleType),new string[0] },
				{ typeof(SchedStatus),new string[0] },
				{ typeof(UserOdFkeyType),new string[0] },
				{ typeof(EnumPearlStatus),new string[0] },
				{ typeof(PerioSequenceType),new string[0] },
				{ typeof(ApptReminderType),new string[0] },
				{ typeof(SmsMessageSource),new string[0] },
				{ typeof(SessionTokenType),new string[0] },
				{ typeof(eServiceType),new string[0] },
				{ typeof(eServiceAction),new string[0] },
				{ typeof(eServiceCode),new string[0] },
			};
			List<string> listProblems=new List<string>();
			foreach(KeyValuePair<Type,string[]> pair in dictExceptions) {
				HashSet<string> setPublished=new HashSet<string>(OdEnumReference.GetEnum(pair.Key.Name).Select(x => x.MemberName));
				Assert.IsTrue(setPublished.Count>0,pair.Key.Name+" is not in the reference data.");
				foreach(string name in Enum.GetNames(pair.Key)) {
					if(!setPublished.Contains(name) && !pair.Value.Contains(name)) {
						listProblems.Add(pair.Key.Name+"."+name);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,"Members not in Open Dental 25.3: "+string.Join(", ",listProblems));
		}
	}
}
