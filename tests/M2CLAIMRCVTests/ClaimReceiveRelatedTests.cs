// ClaimReceiveRelated (no database): the related-claims prompt's wording, the force-status preference it reads, when the next insurance's estimates
// are recalculated, and the 26.1 text for claim status H.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CLAIMRCVTests {
	[TestClass]
	public class ClaimReceiveRelatedTests {
		[TestMethod]
		public void NextInsuranceWording_NamesNoOrdinal() {
			Assert.AreEqual("There is at least one unsent claim for the next insurance on the received procedures.",ClaimReceiveRelated.GetMessage(true));
			Assert.AreEqual("Outstanding claims for the next insurance",ClaimReceiveRelated.GetTitle(true));
			Assert.AreEqual("Send claim(s) now",ClaimReceiveRelated.GetSendText(true));
			Assert.AreEqual("Print claim(s) now",ClaimReceiveRelated.GetPrintText(true));
			foreach(string text in new[] { ClaimReceiveRelated.GetMessage(true),ClaimReceiveRelated.GetTitle(true),ClaimReceiveRelated.GetSendText(true),
				ClaimReceiveRelated.GetPrintText(true) })
			{
				StringAssert.DoesNotMatch(text,new System.Text.RegularExpressions.Regex("(?i)secondary|primary|tertiary"),"The prompt serves every ordinal.");
			}
		}

		[TestMethod]
		public void MedicalToPrimaryWording_IsThe24_3Text() {
			//The 24.3 prompt built these from "primary"; the texts are unchanged, so existing translations still match.
			Assert.AreEqual("There is at least one unsent "+"primary"+" claim for the received procedures.",ClaimReceiveRelated.GetMessage(false));
			Assert.AreEqual("Outstanding "+"primary"+" claims",ClaimReceiveRelated.GetTitle(false));
			Assert.AreEqual("Send "+"primary"+" claim(s) now",ClaimReceiveRelated.GetSendText(false));
			Assert.AreEqual("Print "+"primary"+" claim(s) now",ClaimReceiveRelated.GetPrintText(false));
		}

		[TestMethod]
		public void ForceStatusPreference_KeepsItsMeaningForEveryOrdinal() {
			Assert.AreEqual(PrefName.ClaimPrimaryRecievedForceSecondaryStatus,ClaimReceiveRelated.GetForceStatusPrefName(true));
			Assert.AreEqual(PrefName.ClaimMedReceivedForcePrimaryStatus,ClaimReceiveRelated.GetForceStatusPrefName(false));
		}

		[TestMethod]
		public void RecalcNextInsurance_AfterAnyClaimButAPreAuth() {
			foreach(string claimType in new[] { "P","S","Other","Cap" }) {
				Assert.IsTrue(ClaimReceiveRelated.IsRecalcNextInsuranceNeeded(new Claim { ClaimType=claimType },false,true),claimType+" claims recalculate (26.1).");
				Assert.IsFalse(ClaimReceiveRelated.IsRecalcNextInsuranceNeeded(new Claim { ClaimType=claimType },false,false),"Preference off.");
				Assert.IsFalse(ClaimReceiveRelated.IsRecalcNextInsuranceNeeded(new Claim { ClaimType=claimType },true,true),
					"Received with no payment: Claims.ReceiveAsNoPaymentIfNeeded already recalculated.");
			}
			Assert.IsFalse(ClaimReceiveRelated.IsRecalcNextInsuranceNeeded(new Claim { ClaimType="PreAuth" },false,true));
			Assert.IsFalse(ClaimReceiveRelated.IsRecalcNextInsuranceNeeded(null,false,true));
		}

		[TestMethod]
		public void ClaimStatusH_ReadsHoldUntilPriorReceived() {
			Assert.AreEqual("Hold Until Prior Received",ClaimReceiveRelated.GetClaimStatusText(ClaimStatus.HoldUntilPriReceived));
			Assert.AreEqual("H",ClaimStatus.HoldUntilPriReceived.GetDescription(useShortVersionIfAvailable:true),"The stored value stays H.");
			foreach(ClaimStatus claimStatus in Enum.GetValues(typeof(ClaimStatus)).Cast<ClaimStatus>().Where(x => x!=ClaimStatus.HoldUntilPriReceived)) {
				Assert.AreEqual(claimStatus.GetDescription(),ClaimReceiveRelated.GetClaimStatusText(claimStatus),"Other statuses keep their text.");
			}
		}
	}
}
