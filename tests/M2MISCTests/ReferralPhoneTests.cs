// v25_4-family-referral-phone: the referral's primary phone holds up to 30 characters; 10-digit values keep the office phone format.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2MISCTests {
	[TestClass]
	public class ReferralPhoneTests {
		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();//the office phone format applies to en-US (and Canada)
		}

		[TestMethod]
		public void Display_TenDigits_InTheOfficeFormat_OtherValuesAsStored() {
			Assert.AreEqual("(503)555-0142",ReferralPhones.GetDisplay("5035550142"));
			Assert.AreEqual("",ReferralPhones.GetDisplay(""));
			Assert.AreEqual("",ReferralPhones.GetDisplay(null));
			Assert.AreEqual("(503)555-0142 ext. 12",ReferralPhones.GetDisplay("(503)555-0142 ext. 12"));
			Assert.AreEqual("+44 20 7946 0958",ReferralPhones.GetDisplay("+44 20 7946 0958"));
			Assert.AreEqual("15035550142",ReferralPhones.GetDisplay("15035550142"),"Only exactly 10 digits are reformatted.");
		}

		[TestMethod]
		public void Storage_PlainTenDigitNumbers_StayTenDigits() {
			Assert.AreEqual("5035550142",ReferralPhones.GetStorage("(503)555-0142"));
			Assert.AreEqual("5035550142",ReferralPhones.GetStorage(" (503) 555-0142 "));
			Assert.AreEqual("5035550142",ReferralPhones.GetStorage("503.555.0142"));
			Assert.AreEqual("5035550142",ReferralPhones.GetStorage("5035550142"));
			Assert.AreEqual("",ReferralPhones.GetStorage("   "));
			Assert.AreEqual("",ReferralPhones.GetStorage(null));
		}

		[TestMethod]
		public void Storage_ExtensionsAndLongNumbers_AreKeptAsTyped() {
			//Before 25.4 these were refused ("Invalid phone") or cut to 10 digits.
			Assert.AreEqual("(503)555-0142 x12",ReferralPhones.GetStorage("(503)555-0142 x12"));
			Assert.AreEqual("1(503)555-0142",ReferralPhones.GetStorage("1(503)555-0142"));
			Assert.AreEqual("+44 20 7946 0958",ReferralPhones.GetStorage("+44 20 7946 0958"));
			Assert.AreEqual("555-0142",ReferralPhones.GetStorage("555-0142"),"Fewer than 10 digits is no longer an error.");
		}

		[TestMethod]
		public void Error_OnlyWhenLongerThanTheColumn() {
			Assert.AreEqual(30,ReferralPhones.MAX_LENGTH);
			Assert.AreEqual("",ReferralPhones.GetError(""));
			Assert.AreEqual("",ReferralPhones.GetError("555"));
			Assert.AreEqual("",ReferralPhones.GetError(new string('1',30)));
			Assert.AreEqual("",ReferralPhones.GetError(" "+new string('1',30)+" "),"Trimmed first.");
			Assert.AreNotEqual("",ReferralPhones.GetError(new string('1',31)));
		}

		[TestMethod]
		public void FromPatientPhone_KeepsLongerHomePhones() {
			Assert.AreEqual("5035550142",ReferralPhones.FromPatientPhone("(503)555-0142"));
			Assert.AreEqual("(503)555-0142 x7",ReferralPhones.FromPatientPhone("(503)555-0142 x7"));
			Assert.AreEqual("",ReferralPhones.FromPatientPhone(""));
			Assert.AreEqual("",ReferralPhones.FromPatientPhone(new string('2',31)));
		}

		///<summary>Referral slips and referral letters print referral.phone by the same rule as the Referral window. Before, only a value of exactly
		///10 characters was printed, so a phone with an extension, a leading 1 or an international number printed blank.</summary>
		[TestMethod]
		public void Sheets_ReferralPhone_PrintsEveryStoredValue() {
			TestDb.ResetOrSkip();
			Dictionary<string,string> dictStoredToPrinted=new Dictionary<string,string> {
				{ "5035550142","(503)555-0142" },
				{ "(503)555-0142 x12","(503)555-0142 x12" },
				{ "1(503)555-0142","1(503)555-0142" },
				{ "+44 20 7946 0958","+44 20 7946 0958" },
				{ "555-0142","555-0142" },
				{ "","" },
			};
			Patient patient=new Patient { PatNum=900001,LName="Synthetic",FName="Patient" };
			foreach(KeyValuePair<string,string> pair in dictStoredToPrinted) {
				Referral referral=new Referral { LName="Synthetic",FName="Referrer",Telephone=pair.Key,Phone2="555-0199",Note="",DisplayNote="" };
				long referralNum=Referrals.Insert(referral);
				Referrals.RefreshCache();
				foreach(SheetTypeEnum sheetType in new[] { SheetTypeEnum.ReferralSlip,SheetTypeEnum.ReferralLetter }) {
					Sheet sheet=new Sheet {
						SheetType=sheetType,
						PatNum=patient.PatNum,
						SheetFields=new List<SheetField> {
							new SheetField { FieldType=SheetFieldType.OutputText,FieldName="referral.phone",FieldValue="" },
							new SheetField { FieldType=SheetFieldType.OutputText,FieldName="referral.phone2",FieldValue="" },
						},
						Parameters=new List<SheetParameter> {
							new SheetParameter(true,"PatNum") { ParamValue=patient.PatNum },
							new SheetParameter(true,"ReferralNum") { ParamValue=referralNum },
						},
					};
					Assert.AreEqual("",SheetFiller.FillFields(sheet,patient:patient));
					Assert.AreEqual(pair.Value,sheet.SheetFields[0].FieldValue,sheetType+" referral.phone for '"+pair.Key+"'");
					Assert.AreEqual("555-0199",sheet.SheetFields[1].FieldValue,sheetType+" referral.phone2");
				}
				Assert.AreEqual(pair.Value,Referrals.GetPhone(referralNum),"Family module referral list for '"+pair.Key+"'");
			}
		}

		///<summary>A patient who is also a referral gets the same referral phone whichever path writes it: Edit Referral when the patient is added
		///as a referral, saving the patient (Patients.Update keeps the referral in step) and merging patients. 24.3 used FormatNumbersExactTen in
		///all three, which cut an extension and turned an international number into the wrong 10 digits.</summary>
		[TestMethod]
		public void PatientReferral_EveryCopyOfTheHomePhoneUsesTheSameRule() {
			Assert.AreEqual("1(503)555-0142",ReferralPhones.FromPatientPhone("1(503)555-0142"));
			Assert.AreEqual("+44 20 7946 0958",ReferralPhones.FromPatientPhone("+44 20 7946 0958"));
			string patients=TestDb.ReadRepoFile("OpenDentBusiness","Data Interface","Patients.cs");
			StringAssert.Contains(patients,"referral.Telephone=ReferralPhones.FromPatientPhone(patientTo.HmPhone);");
			StringAssert.Contains(patients,"referral.Telephone=ReferralPhones.FromPatientPhone(patient.HmPhone);");
			Assert.IsFalse(Regex.IsMatch(patients,@"\.Telephone=TelephoneNumbers\.FormatNumbersExactTen"),"No 10-digit copy of the home phone is left.");
			StringAssert.Contains(TestDb.ReadRepoFile("WpfControlsOD","Frms","FrmReferralEdit.xaml.cs"),
				"ReferralCur.Telephone=ReferralPhones.FromPatientPhone(patient.HmPhone);");
			string sheetFiller=TestDb.ReadRepoFile("OpenDentBusiness","SheetFramework","SheetFiller.cs");
			Assert.IsFalse(sheetFiller.Contains("refer.Telephone.Length==10"),"Sheets print every stored referral phone.");
		}

		[TestMethod]
		public void Database_ThirtyCharacterPhone_RoundTrips() {
			TestDb.ResetOrSkip();
			string telephone=ReferralPhones.GetStorage("+1 (503) 555-0142 ext. 123456");
			Assert.AreEqual(29,telephone.Length);
			Referral referral=new Referral { LName="Synthetic",FName="Referrer",Telephone=telephone,Phone2="",Note="",DisplayNote="" };
			long referralNum=Referrals.Insert(referral);
			Referral referralDb=OpenDentBusiness.Crud.ReferralCrud.SelectOne(referralNum);
			Assert.AreEqual(telephone,referralDb.Telephone,"referral.Telephone is varchar(30) since 25.4 (M2-01).");
			string telephoneMax=new string('9',30);
			referralDb.Telephone=telephoneMax;
			Referrals.Update(referralDb);
			Assert.AreEqual(telephoneMax,OpenDentBusiness.Crud.ReferralCrud.SelectOne(referralNum).Telephone);
		}
	}
}
