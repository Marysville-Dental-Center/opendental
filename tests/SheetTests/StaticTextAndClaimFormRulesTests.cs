// Secondary-insurance static text fields and hospitalization dates, rules that need no database (Open Dental 24.4; backlog package A-SHEET).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace SheetTests {
	[TestClass]
	public class StaticTextAndClaimFormRulesTests {
		///<summary>Each new secondary field and the primary field it mirrors.</summary>
		private static readonly Dictionary<string,EnumStaticTextField> _dictSecondaryToPrimary=new Dictionary<string,EnumStaticTextField> {
			{"ins2FeeSchedule",EnumStaticTextField.insFeeSchedule},
			{"ins2PlanNote",EnumStaticTextField.insPlanNote},
			{"ins2SubBirthDate",EnumStaticTextField.insSubBirthDate},
			{"ins2SubNote",EnumStaticTextField.insSubNote},
			{"ins2Type",EnumStaticTextField.insType},
			{"subscriber2ID",EnumStaticTextField.subscriberID},
		};

		[TestInitialize]
		public void TestInitialize() {
			SheetDb.UseUsEnglish();
		}

		[TestMethod]
		public void SecondaryStaticText_TokensExistWithPrimaryDependencies() {
			foreach(KeyValuePair<string,EnumStaticTextField> pair in _dictSecondaryToPrimary) {
				Assert.IsTrue(Enum.TryParse(pair.Key,false,out EnumStaticTextField enumSecondary),"Missing token ["+pair.Key+"]");
				Assert.AreEqual(pair.Key,enumSecondary.ToString(),"Token names are case sensitive in saved sheets.");
				Assert.AreEqual(StaticTextData.GetStaticTextDependencies(new List<EnumStaticTextField> { pair.Value }),
					StaticTextData.GetStaticTextDependencies(new List<EnumStaticTextField> { enumSecondary }),pair.Key);
				Assert.IsFalse(SheetFields.IsStaticTextFieldObsolete(enumSecondary),"Offered in the static text picker: "+pair.Key);
			}
		}

		[TestMethod]
		public void StaticText_EveryTokenDeclaresItsDependencies() {
			//Filling a sheet throws for a token without a dependency entry.
			List<EnumStaticTextField> listAll=Enum.GetValues(typeof(EnumStaticTextField)).Cast<EnumStaticTextField>().ToList();
			StaticTextData.GetStaticTextDependencies(listAll);
		}

		[TestMethod]
		public void StaticText_TokensFoundInSheetText() {
			Sheet sheet=new Sheet { SheetFields=new List<SheetField> {
				new SheetField { FieldType=SheetFieldType.StaticText,FieldValue="Secondary: [ins2Type] [ins2FeeSchedule] [subscriber2ID] [ins2SubBirthDate]" },
				new SheetField { FieldType=SheetFieldType.StaticText,FieldValue="[ins2PlanNote]\r\n[ins2SubNote] [notAToken]" },
			}};
			List<EnumStaticTextField> listFound=SheetFiller.GetAllStaticTextFieldsForSheet(sheet);
			CollectionAssert.AreEquivalent(_dictSecondaryToPrimary.Keys.ToList(),listFound.Select(x => x.ToString()).ToList());
		}

		[TestMethod]
		public void InsTypeDescription_SharedByPrimaryAndSecondary() {
			Assert.AreEqual("PPO Percentage",SheetFiller.GetInsTypeDescription("p"));
			Assert.AreEqual("Medicaid or Flat Copay",SheetFiller.GetInsTypeDescription("f"));
			Assert.AreEqual("Capitation",SheetFiller.GetInsTypeDescription("c"));
			Assert.AreEqual("Category Percentage",SheetFiller.GetInsTypeDescription(""));
			Assert.AreEqual("",SheetFiller.GetInsTypeDescription(null));
		}

		[TestMethod]
		public void HospitalizationDates_FromAdmitAndDischarge() {
			Patient patient=new Patient { PatNum=5,AdmitDate=new DateTime(2025,3,1) };
			EhrPatient ehrPatient=new EhrPatient { PatNum=5,DischargeDate=new DateTime(2025,3,5,14,30,0) };
			Claim claimMedical=new Claim { MedType=EnumClaimMedType.Medical };
			EhrPatients.GetHospitalizationDates(claimMedical,patient,ehrPatient,out DateTime dateFrom,out DateTime dateTo);
			Assert.AreEqual(new DateTime(2025,3,1),dateFrom);
			Assert.AreEqual(new DateTime(2025,3,5),dateTo,"The discharge time of day is dropped.");
			//Same day counts.
			ehrPatient.DischargeDate=new DateTime(2025,3,1,9,0,0);
			EhrPatients.GetHospitalizationDates(claimMedical,patient,ehrPatient,out dateFrom,out dateTo);
			Assert.AreEqual(new DateTime(2025,3,1),dateTo);
			//A discharge before the admit date is an earlier stay: To stays blank.
			ehrPatient.DischargeDate=new DateTime(2025,2,20);
			EhrPatients.GetHospitalizationDates(claimMedical,patient,ehrPatient,out dateFrom,out dateTo);
			Assert.AreEqual(new DateTime(2025,3,1),dateFrom);
			Assert.AreEqual(DateTime.MinValue,dateTo);
			//No EHR record.
			EhrPatients.GetHospitalizationDates(claimMedical,patient,null,out dateFrom,out dateTo);
			Assert.AreEqual(new DateTime(2025,3,1),dateFrom);
			Assert.AreEqual(DateTime.MinValue,dateTo);
		}

		[TestMethod]
		public void HospitalizationDates_BlankUnlessMedicalWithAdmitDate() {
			EhrPatient ehrPatient=new EhrPatient { DischargeDate=new DateTime(2025,3,5) };
			Patient patient=new Patient { AdmitDate=new DateTime(2025,3,1) };
			foreach(EnumClaimMedType medType in new[] { EnumClaimMedType.Dental,EnumClaimMedType.Institutional }) {
				EhrPatients.GetHospitalizationDates(new Claim { MedType=medType },patient,ehrPatient,out DateTime dateFrom,out DateTime dateTo);
				Assert.AreEqual(DateTime.MinValue,dateFrom,medType.ToString());
				Assert.AreEqual(DateTime.MinValue,dateTo,medType.ToString());
			}
			//No admit date: nothing prints, not even the discharge date.
			EhrPatients.GetHospitalizationDates(new Claim { MedType=EnumClaimMedType.Medical },new Patient(),ehrPatient,out DateTime dateFrom2,out DateTime dateTo2);
			Assert.AreEqual(DateTime.MinValue,dateFrom2);
			Assert.AreEqual(DateTime.MinValue,dateTo2);
			EhrPatients.GetHospitalizationDates(null,patient,ehrPatient,out dateFrom2,out dateTo2);
			Assert.AreEqual(DateTime.MinValue,dateFrom2);
		}

		[TestMethod]
		public void ClaimForm1500_HasBox18Items() {
			ClaimForm claimFormFile=ClaimForms.DeserializeClaimForm(TestDb.FindRepoFile(@"OpenDentBusiness\Resources\ClaimForms\ClaimForm1500_02_12.xml"),"");
			ClaimForm claimFormEmbedded=ClaimForms.GetInternalClaims().Single(x => x.Description=="1500_02_12");
			foreach(ClaimForm claimForm in new[] { claimFormFile,claimFormEmbedded }) {
				ClaimFormItem itemFrom=claimForm.Items.Single(x => x.FieldName=="HospitalizationDateFrom");
				ClaimFormItem itemTo=claimForm.Items.Single(x => x.FieldName=="HospitalizationDateTo");
				ClaimFormItem itemReferringNpi=claimForm.Items.Single(x => x.FieldName=="ReferringProvNPI");
				//Box 18 is on the row of box 17 (referring provider), right of 17b.
				Assert.AreEqual(itemReferringNpi.YPos,itemFrom.YPos);
				Assert.AreEqual(itemReferringNpi.YPos,itemTo.YPos);
				Assert.IsTrue(itemFrom.XPos>=itemReferringNpi.XPos+itemReferringNpi.Width,"Right of 17b.");
				Assert.IsTrue(itemTo.XPos>=itemFrom.XPos+itemFrom.Width,"To is right of From.");
				Assert.IsTrue(itemTo.XPos+itemTo.Width<=claimForm.Width);
				//Same date format as the service dates in box 24A.
				Assert.AreEqual("MM    dd    yy",itemFrom.FormatString);
				Assert.AreEqual("MM    dd    yy",itemTo.FormatString);
				Assert.AreEqual("12    31    25",new DateTime(2025,12,31).ToString(itemFrom.FormatString));
			}
		}
	}
}
