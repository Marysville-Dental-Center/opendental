// Pins every enum value Open Dental 25.4 and 26.1 added, renamed or renumbered to the number in the published database documents.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M202Tests {
	[TestClass]
	public class EnumRegistry26_1Tests {
		///<summary>Published names that this fork spells differently. The stored number is what matters; each difference says why.</summary>
		private static readonly Dictionary<string,string> _dictForkNames=new Dictionary<string,string> {
			//25.4 renamed 13 BlockoutEdit. The C# name stays Blockouts so existing callers (and packages built at the same time) keep compiling.
			{"EnumPermType.BlockoutEdit","Blockouts"},
			//Dental school is out of scope (owner decision): the 25.4 names are kept as values only, under the 24.3 names.
			{"EnumPermType.SchoolAdminStudentEdit","AdminDentalStudents"},
			{"EnumPermType.SchoolAdminInstructorEdit","AdminDentalInstructors"},
			{"EnumPermType.SchoolAdminAcesss","AdminDentalEvaluations"},
		};

		private static string GetCodeName(OdEnumChange change) {
			string key=change.EnumName+"."+change.PublishedMember;
			return _dictForkNames.TryGetValue(key,out string name) ? name : change.PublishedMember;
		}

		[TestMethod]
		public void DataFile_HasEveryPackageEnum() {
			List<OdEnumChange> listChanges=OdEnumChanges.GetAll();
			Assert.IsTrue(listChanges.Count>40);
			foreach(string enumName in new[] { "EnumPermType","AlertType","FormType","CreditCardSource","LogSources","ErxStatus","UserOdFkeyType","eServiceType",
				"eServiceAction","eServiceCode","AutoCondField","EnumImageAnnotVendor","SheetFieldType","EServiceTypes","DefCat" })
			{
				Assert.IsTrue(listChanges.Any(x => x.EnumName==enumName),enumName);
			}
		}

		[TestMethod]
		public void EveryPublishedChange_IsDefinedWithItsNumber() {
			List<string> listProblems=new List<string>();
			foreach(OdEnumChange change in OdEnumChanges.GetAll().Where(x => x.Status==OdEnumChanges.STATUS_PUBLISHED || x.Status==OdEnumChanges.STATUS_ORDER)) {
				Type type=OdEnumChanges.FindCodeEnum(change.EnumName);
				if(type==null) {
					listProblems.Add(change+": enum not found");
					continue;
				}
				string codeName=GetCodeName(change);
				if(!Enum.GetNames(type).Contains(codeName)) {
					listProblems.Add(change+": member "+codeName+" missing");
					continue;
				}
				long value=Convert.ToInt64(Enum.Parse(type,codeName));
				if(value!=change.Number.Value) {
					listProblems.Add(change+": is "+value);
					continue;
				}
				//The number maps back to this member: no other member shares it.
				if(Enum.GetName(type,Enum.ToObject(type,value))!=codeName) {
					listProblems.Add(change+": number also used by "+Enum.GetName(type,Enum.ToObject(type,value)));
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join(Environment.NewLine,listProblems));
		}

		[TestMethod]
		public void OtherPackageValues_MatchOnceTheyExist() {
			foreach(OdEnumChange change in OdEnumChanges.GetAll().Where(x => x.Status==OdEnumChanges.STATUS_OTHER_PACKAGE)) {
				Type type=OdEnumChanges.FindCodeEnum(change.EnumName);
				if(type==null || !Enum.GetNames(type).Contains(change.PublishedMember)) {
					continue;//Not merged yet (package M2-01).
				}
				Assert.AreEqual((long)change.Number.Value,Convert.ToInt64(Enum.Parse(type,change.PublishedMember)),change.ToString());
			}
			//The setup code uses this constant until DefCat.EmployeeTitles exists, and keeps working after.
			Assert.AreEqual(56,(int)DefCats26_1.EmployeeTitles);
			if(DefCats26_1.IsEmployeeTitlesDefined()) {
				Assert.AreEqual("EmployeeTitles",Enum.GetName(typeof(DefCat),56));
			}
		}

		[TestMethod]
		public void EnumPermType_EndsAt276WithoutGaps() {
			int[] arrayValues=Enum.GetValues(typeof(EnumPermType)).Cast<int>().OrderBy(x => x).ToArray();
			for(int i=0;i<arrayValues.Length;i++) {
				Assert.AreEqual(i,arrayValues[i],"The security tree's Set All and Set None loop over 0..count-1.");
			}
			Assert.AreEqual(276,arrayValues.Max());
		}

		///<summary>The 272 conflict: Open Dental's 25.3 documents print 272 PhoneExtension, the 25.4 and 26.1 documents print 272 BlockoutAdd and 274
		///PhoneExtension. This program follows 26.1.</summary>
		[TestMethod]
		public void Permission272_FollowsThe25_4And26_1Documents() {
			string path25_3=TestPaths.GetRepoFile("tests","RegistryTests","Data","OdEnums25_3.txt");
			Assert.IsTrue(File.ReadAllLines(path25_3).Any(x => x.StartsWith("EnumPermType|PhoneExtension|272|")),"The 25.3 reference prints 272 PhoneExtension.");
			OdEnumChange changePhone=OdEnumChanges.GetAll().Single(x => x.EnumName=="EnumPermType" && x.PublishedMember=="PhoneExtension");
			Assert.AreEqual(274,changePhone.Number);
			StringAssert.Contains(changePhone.Source,"full26-1");
			Assert.AreEqual(272,(int)EnumPermType.BlockoutAdd);
			Assert.AreEqual(273,(int)EnumPermType.BlockoutDelete);
			Assert.AreEqual(274,(int)EnumPermType.PhoneExtension);
			Assert.AreEqual(275,(int)EnumPermType.PatientSelected);
			Assert.AreEqual(276,(int)EnumPermType.LogScraperHQ);
			Assert.AreEqual(272,OpenPmsDentalBusiness.ForkMigrations.Seeds26_1.Seed26_1.PERM_PHONE_EXTENSION_25_3);
		}

		[TestMethod]
		public void DentalSchoolPermissions_KeepTheirNumbersOnly() {
			Assert.AreEqual(77,(int)EnumPermType.AdminDentalStudents);
			Assert.AreEqual(78,(int)EnumPermType.AdminDentalInstructors);
			Assert.AreEqual(81,(int)EnumPermType.AdminDentalEvaluations);
			//Values only: the 24.3 wording stays, because the dental school features are not built.
			Assert.AreEqual("Student Edit",EnumPermType.AdminDentalStudents.GetDescription());
			Assert.AreEqual("Instructor Edit",EnumPermType.AdminDentalInstructors.GetDescription());
			Assert.AreEqual("Admin Evaluation Edit",EnumPermType.AdminDentalEvaluations.GetDescription());
		}

		[TestMethod]
		public void ErxStatus6_IsPending() {
			Assert.AreEqual(6,(int)ErxStatus.Pending);
			Assert.IsFalse(Enum.GetNames(typeof(ErxStatus)).Contains("PendingPodio"));
		}

		[TestMethod]
		public void CreditCardSource_25_4Values() {
			Assert.IsFalse(Enum.IsDefined(typeof(CreditCardSource),24),"The documents list no 24.");
			List<CreditCardSource> listOnline=CreditCards.GetCreditCardSourcesForOnlinePayments();
			Assert.IsFalse(listOnline.Contains(CreditCardSource.MeetInTheCloudTerminal),"A terminal in the office is not an online payment.");
			Assert.IsTrue(listOnline.Contains(CreditCardSource.MeetInTheCloudPaymentPortal));
			Assert.IsTrue(listOnline.Contains(CreditCardSource.MeetInTheCloudPaymentPortalGuest));
		}

		[TestMethod]
		public void ReservedValues_AreHiddenWhereHqValuesAreHidden() {
			//Alert category setup removes every alert type for which GenericTools.IsODHQ is true.
			foreach(AlertType alertType in new[] { AlertType.TreatmentPlanImagesFolderInaccesible,AlertType.PaymentPlanImagesFolderInaccesible,
				AlertType.CannotCreateOrAccessPatientFolder,AlertType.DefaultEmailNotSet,AlertType.PaySuite,AlertType.EServiceListenerError })
			{
				Assert.IsTrue(GenericTools.IsODHQ(alertType),alertType+" must be hidden from alert category setup.");
				Assert.IsTrue(Attribute.IsDefined(typeof(AlertType).GetField(alertType.ToString()),typeof(System.ComponentModel.DescriptionAttribute)),
					alertType+" needs a [Description].");
			}
			Assert.IsTrue(ReservedEnumValueAttribute.IsReserved(FormType.FrmPaySuiteReconciliation));
			Assert.IsTrue(ReservedEnumValueAttribute.IsReserved(LogSources.ODTouch));
			Assert.IsTrue(ReservedEnumValueAttribute.IsReserved(LogSources.eClipboard));
			Assert.IsFalse(ReservedEnumValueAttribute.IsReserved(LogSources.Era835Import));
			Assert.IsTrue(GenericTools.IsODHQ(UserOdFkeyType.DefaultTestingTeamJM));
		}

		[TestMethod]
		public void MobileStaticText_IsAfterSigBoxPractice() {
			Assert.AreEqual((int)SheetFieldType.SigBoxPractice+1,(int)SheetFieldType.MobileStaticText);
			Assert.AreEqual(17,(int)SheetFieldType.MobileStaticText);
		}

		[TestMethod]
		public void EServiceTypes_FollowThePublishedOrder() {
			CollectionAssert.AreEqual(new[] { "None","EConnector","Broadcaster","BroadcastMonitor","ServiceMainHQ","OpenDentalService","OpenDentalAPIService",
				"SecureEmailService","ODXam","EClipboard","OpenDentalLogScraper","OpenDental32Bit" },Enum.GetNames(typeof(EServiceTypes)));
		}

		[TestMethod]
		public void AutoCondField14_HasADescription() {
			Assert.AreEqual(14,(int)AutoCondField.PatientProcedureClinicMismatch);
			Assert.AreNotEqual(AutoCondField.PatientProcedureClinicMismatch.ToString(),AutoCondField.PatientProcedureClinicMismatch.GetDescription());
			//The condition editor lists values by index, so the values must stay contiguous.
			int[] arrayValues=Enum.GetValues(typeof(AutoCondField)).Cast<int>().ToArray();
			CollectionAssert.AreEqual(Enumerable.Range(0,arrayValues.Length).ToArray(),arrayValues);
		}

		[TestMethod]
		public void NotAddedValues_AreDocumented() {
			//ProcStat 0 is printed as "Not used": adding a None member would put it into every procedure status list.
			OdEnumChange change=OdEnumChanges.GetAll().Single(x => x.EnumName=="ProcStat");
			Assert.AreEqual(OdEnumChanges.STATUS_NOT_ADDED,change.Status);
			Assert.IsFalse(Enum.IsDefined(typeof(ProcStat),0));
		}
	}
}
