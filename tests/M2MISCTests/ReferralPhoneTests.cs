// v25_4-family-referral-phone: the referral's primary phone holds up to 30 characters; 10-digit values keep the office phone format.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
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
