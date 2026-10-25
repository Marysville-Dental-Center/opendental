// V-EDS: the EDS-specific local checks (rule 2 of v26_1-manage-eds-claim-validation), on synthetic snapshots. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect.Eds;

namespace VEDSTests {
	[TestClass]
	public class EdsNpiTaxonomyTests {
		[TestInitialize]
		public void TestInitialize() {
			EdsTestDb.SetCulture();
		}

		[TestMethod]
		public void Npi_PublishedExampleAndPrefix_AreValid() {
			Assert.IsTrue(EdsClaimChecks.IsNpiCheckDigitValid("1234567893"),"The example number in CMS's check digit description.");
			Assert.IsTrue(EdsClaimChecks.IsNpiCheckDigitValid("808401234567893"),"The optional 80840 prefix.");
			Assert.IsTrue(EdsClaimChecks.IsNpiCheckDigitValid(" 1234567893 "),"Surrounding spaces are ignored.");
			Assert.IsFalse(EdsClaimChecks.IsNpiCheckDigitValid("1234567890"));
			Assert.IsFalse(EdsClaimChecks.IsNpiCheckDigitValid("808401234567890"));
		}

		[TestMethod]
		public void Npi_AgreesWithTheCmsFormulation_ForManyNumbers() {
			for(int i=0;i<500;i++) {
				string nine=(100000000+i*7919).ToString();
				Assert.IsTrue(EdsClaimChecks.IsNpiCheckDigitValid(EdsTestData.MakeNpi(nine)),nine);
				Assert.IsFalse(EdsClaimChecks.IsNpiCheckDigitValid(EdsTestData.MakeBadNpi(nine)),nine);
			}
		}

		[TestMethod]
		public void Npi_WrongShape_IsNotValid() {
			Assert.IsFalse(EdsClaimChecks.IsNpiCheckDigitValid(null));
			Assert.IsFalse(EdsClaimChecks.IsNpiCheckDigitValid(""));
			Assert.IsFalse(EdsClaimChecks.IsNpiCheckDigitValid("123456789"));
			Assert.IsFalse(EdsClaimChecks.IsNpiCheckDigitValid("12345678931"));
			Assert.IsFalse(EdsClaimChecks.IsNpiCheckDigitValid("12345A7893"));
			Assert.IsNull(EdsClaimChecks.GetNpiDigits("80841234567893"));
			Assert.AreEqual("1234567893",EdsClaimChecks.GetNpiDigits("808401234567893"));
		}

		[TestMethod]
		public void Taxonomy_TenCharactersEndingInX() {
			Assert.IsTrue(EdsClaimChecks.IsTaxonomyFormatValid("1223G0001X"));
			Assert.IsTrue(EdsClaimChecks.IsTaxonomyFormatValid("124Q00000X"));
			Assert.IsTrue(EdsClaimChecks.IsTaxonomyFormatValid("1223g0001x"),"Case does not matter.");
			Assert.IsFalse(EdsClaimChecks.IsTaxonomyFormatValid("1223G0001"));
			Assert.IsFalse(EdsClaimChecks.IsTaxonomyFormatValid("1223G00012"));
			Assert.IsFalse(EdsClaimChecks.IsTaxonomyFormatValid("1223-0001X"));
			Assert.IsFalse(EdsClaimChecks.IsTaxonomyFormatValid(""));
			Assert.IsFalse(EdsClaimChecks.IsTaxonomyFormatValid(null));
		}
	}

	[TestClass]
	public class EdsLocalChecksTests {
		[TestInitialize]
		public void TestInitialize() {
			EdsTestDb.SetCulture();
		}

		[TestMethod]
		public void ValidSnapshot_HasNoProblems() {
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetLocalIssues(EdsTestData.MakeValidSnapshot());
			Assert.AreEqual(0,listIssues.Count,string.Join("; ",listIssues.Select(x => x.Message)));
		}

		[TestMethod]
		public void Null_HasNoProblems() {
			Assert.AreEqual(0,EdsClaimChecks.GetLocalIssues(null).Count);
		}

		[TestMethod]
		public void NpiCheckDigit_IsAnErrorOnTheRightProvider() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ProvTreat.Npi=EdsTestData.MakeBadNpi("100000002");
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetLocalIssues(snapshot);
			Assert.AreEqual(1,listIssues.Count);
			Assert.AreEqual(EdsClaimChecks.CHECK_NPI_CHECK_DIGIT,listIssues[0].CheckId);
			Assert.AreEqual(EdsIssueSeverity.Error,listIssues[0].Severity);
			Assert.AreEqual(EdsIssueSource.Local,listIssues[0].Source);
			Assert.AreEqual("Treating provider (TREAT)",listIssues[0].Location);
			StringAssert.Contains(listIssues[0].Message,snapshot.ProvTreat.Npi);
			Assert.AreNotEqual("",listIssues[0].HowToFix);
		}

		[TestMethod]
		public void SameProviderForBoth_IsCheckedOnce() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ProvTreat=snapshot.ProvBill;
			snapshot.ProvBill.Npi=EdsTestData.MakeBadNpi("100000001");
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetLocalIssues(snapshot);
			Assert.AreEqual(1,listIssues.Count);
			Assert.AreEqual("Billing and treating provider (BILL)",listIssues[0].Location);
		}

		[TestMethod]
		public void NpiMissingOrMalformed_IsLeftToTheX12Validator() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ProvBill.Npi="";
			snapshot.ProvTreat.Npi="12345";
			Assert.AreEqual(0,EdsClaimChecks.GetLocalIssues(snapshot).Count,"X837_5010.Validate already reports both.");
			snapshot.IsX12Validated=false;
			List<string> listCheckIds=EdsTestData.CheckIds(EdsClaimChecks.GetLocalIssues(snapshot));
			CollectionAssert.AreEquivalent(new[] { EdsClaimChecks.CHECK_NPI_MISSING,EdsClaimChecks.CHECK_NPI_FORMAT },listCheckIds);
		}

		[TestMethod]
		public void ProviderNotFound_IsAnError() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ProvTreat=new EdsProviderInfo { ProvNum=99 };
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetLocalIssues(snapshot);
			Assert.AreEqual(1,listIssues.Count);
			Assert.AreEqual(EdsClaimChecks.CHECK_PROV_NOT_FOUND,listIssues[0].CheckId);
		}

		[TestMethod]
		public void Taxonomy_MissingOrMalformed_IsAnError() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ProvBill.Taxonomy="";
			snapshot.ProvTreat.Taxonomy="1223G0001Z";
			snapshot.ProvTreat.TaxonomyOverride="1223G0001Z";
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetLocalIssues(snapshot);
			CollectionAssert.AreEquivalent(new[] { EdsClaimChecks.CHECK_TAXONOMY_MISSING,EdsClaimChecks.CHECK_TAXONOMY_FORMAT },
				EdsTestData.CheckIds(listIssues));
			Assert.IsTrue(listIssues.All(x => x.Severity==EdsIssueSeverity.Error));
		}

		[TestMethod]
		public void Taxonomy_OverrideOfWrongLength_IsLeftToTheX12Validator() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ProvBill.Taxonomy="1223G";
			snapshot.ProvBill.TaxonomyOverride="1223G";
			Assert.AreEqual(0,EdsClaimChecks.GetLocalIssues(snapshot).Count);
			snapshot.IsX12Validated=false;
			Assert.AreEqual(EdsClaimChecks.CHECK_TAXONOMY_FORMAT,EdsClaimChecks.GetLocalIssues(snapshot).Single().CheckId);
		}

		[TestMethod]
		public void PayerId_MissingOrTooShort_IsAnError() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.PayerId=" ";
			EdsValidationIssue issue=EdsClaimChecks.GetLocalIssues(snapshot).Single();
			Assert.AreEqual(EdsClaimChecks.CHECK_PAYER_ID_MISSING,issue.CheckId);
			Assert.AreEqual(EdsIssueSeverity.Error,issue.Severity);
			Assert.AreEqual("Carrier (Synthetic Carrier)",issue.Location);
			snapshot.PayerId="ZZ";
			Assert.AreEqual(EdsClaimChecks.CHECK_PAYER_ID_MISSING,EdsClaimChecks.GetLocalIssues(snapshot).Single().CheckId);
		}

		[TestMethod]
		public void PayerNotOnTheEdsList_IsAWarning_OnlyWhenTheListWasDownloaded() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ListEdsPayers.Clear();
			EdsValidationIssue issue=EdsClaimChecks.GetLocalIssues(snapshot).Single();
			Assert.AreEqual(EdsClaimChecks.CHECK_PAYER_NOT_ON_LIST,issue.CheckId);
			Assert.AreEqual(EdsIssueSeverity.Warning,issue.Severity);
			snapshot.IsEdsPayerListLoaded=false;
			Assert.AreEqual(0,EdsClaimChecks.GetLocalIssues(snapshot).Count,"Nothing to compare with.");
		}

		[TestMethod]
		public void PaperOrMedicalPayer_IsAWarning() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ListEdsPayers.Clear();
			snapshot.ListEdsPayers.Add(EdsClaimSnapshot.MakePayerListEntry("ZZ123","Synthetic Carrier",((int)EnumEDSPayerAttributes.ERATP).ToString()));
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetLocalIssues(snapshot);
			CollectionAssert.AreEquivalent(new[] { EdsClaimChecks.CHECK_PAYER_PAPER,EdsClaimChecks.CHECK_PAYER_MEDICAL },EdsTestData.CheckIds(listIssues));
			Assert.IsTrue(listIssues.All(x => x.Severity==EdsIssueSeverity.Warning));
			snapshot.MedType=EnumClaimMedType.Medical;
			Assert.AreEqual(EdsClaimChecks.CHECK_PAYER_PAPER,EdsClaimChecks.GetLocalIssues(snapshot).Single().CheckId,"A medical payer is right for a medical claim.");
		}

		[TestMethod]
		public void PayerAttributes_Unknown_GiveNoWarning() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ListEdsPayers.Clear();
			snapshot.ListEdsPayers.Add(EdsClaimSnapshot.MakePayerListEntry("ZZ123","Synthetic Carrier",""));
			Assert.AreEqual(0,EdsClaimChecks.GetLocalIssues(snapshot).Count);
		}

		[TestMethod]
		public void PayerAttributes_AnyElectronicRow_IsEnough() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ListEdsPayers.Add(EdsClaimSnapshot.MakePayerListEntry("ZZ123","Other name","3"));
			Assert.AreEqual(0,EdsClaimChecks.GetLocalIssues(snapshot).Count);
		}

		[TestMethod]
		public void PayerListEntry_ParsesStoredAttributes() {
			EdsPayerListEntry entry=EdsClaimSnapshot.MakePayerListEntry("ZZ123","Name","0, 6,x,99");
			Assert.IsTrue(entry.AreAttributesKnown);
			CollectionAssert.AreEqual(new[] { EnumEDSPayerAttributes.DefaultClaimTP,EnumEDSPayerAttributes.PayerType },entry.ListAttributes);
			Assert.IsFalse(EdsClaimSnapshot.MakePayerListEntry("ZZ123","Name",null).AreAttributesKnown);
		}

		[TestMethod]
		public void SubscriberId_Missing_IsAnError_WhenNoX12Validation() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.SubscriberId="";
			Assert.AreEqual(0,EdsClaimChecks.GetLocalIssues(snapshot).Count,"X837_5010.Validate reports SubscriberID.");
			snapshot.IsX12Validated=false;
			EdsValidationIssue issue=EdsClaimChecks.GetLocalIssues(snapshot).Single();
			Assert.AreEqual(EdsClaimChecks.CHECK_SUBSCRIBER_ID_MISSING,issue.CheckId);
			Assert.AreEqual(EdsIssueSeverity.Error,issue.Severity);
		}

		[TestMethod]
		public void FutureDates_AreErrors() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.DateService=EdsTestData.DateToday.AddDays(1);
			snapshot.ListProcLines[1].ProcDate=EdsTestData.DateToday.AddDays(2);
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetLocalIssues(snapshot);
			CollectionAssert.AreEquivalent(new[] { EdsClaimChecks.CHECK_DATE_SERVICE_FUTURE,EdsClaimChecks.CHECK_PROC_DATE_FUTURE },
				EdsTestData.CheckIds(listIssues));
			Assert.AreEqual("Procedure line 2 (Z0002)",listIssues.Single(x => x.CheckId==EdsClaimChecks.CHECK_PROC_DATE_FUTURE).Location);
		}

		[TestMethod]
		public void Today_IsNotInTheFuture() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.DateService=EdsTestData.DateToday.AddHours(23);
			snapshot.ListProcLines[0].ProcDate=EdsTestData.DateToday;
			Assert.AreEqual(0,EdsClaimChecks.GetLocalIssues(snapshot).Count);
		}

		[TestMethod]
		public void FutureDates_OnAPreauthorization_AreFine() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ClaimType="PreAuth";
			snapshot.DateService=EdsTestData.DateToday.AddDays(30);
			snapshot.ListProcLines[0].ProcDate=EdsTestData.DateToday.AddDays(30);
			Assert.AreEqual(0,EdsClaimChecks.GetLocalIssues(snapshot).Count);
		}

		[TestMethod]
		public void Errors_ComeBeforeWarnings() {
			EdsClaimSnapshot snapshot=EdsTestData.MakeValidSnapshot();
			snapshot.ListEdsPayers.Clear();//warning
			snapshot.ListProcLines[0].ProcDate=EdsTestData.DateToday.AddDays(5);//error, found later
			List<EdsValidationIssue> listIssues=EdsClaimChecks.GetLocalIssues(snapshot);
			Assert.AreEqual(2,listIssues.Count);
			Assert.AreEqual(EdsIssueSeverity.Error,listIssues[0].Severity);
			Assert.AreEqual(EdsIssueSeverity.Warning,listIssues[1].Severity);
		}
	}
}
