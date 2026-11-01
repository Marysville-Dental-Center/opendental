// v24_4-manage-era-mismatch-match: a claim whose fee or date of service differs from the ERA can still be chosen; the warning names the value
// the ERA expects. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using static BERATests.EraObjects;

namespace BERATests {
	[TestClass]
	public class EraClaimMismatchTests {
		[TestInitialize]
		public void SetCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestMethod]
		public void ClaimFee_MatchAndMismatch() {
			Hx835_Claim claimPaid=EraClaim(EraProc("Z0120",60,48,1001),EraProc("Z0220",40,0,1002));
			Assert.IsFalse(EraClaimMismatch.IsClaimFeeMismatch(new Claim { ClaimFee=100 },claimPaid));
			Assert.IsTrue(EraClaimMismatch.IsClaimFeeMismatch(new Claim { ClaimFee=95.5 },claimPaid));
		}

		[TestMethod]
		public void ClaimFee_ReversalComparedByAbsoluteValue() {
			Hx835_Claim claimPaid=EraClaim(EraProc("Z0120",60,48,1001));
			claimPaid.ClaimFee=-60;
			claimPaid.IsReversal=true;
			Assert.AreEqual(60d,EraClaimMismatch.GetEraClaimFee(claimPaid));
			Assert.IsFalse(EraClaimMismatch.IsClaimFeeMismatch(new Claim { ClaimFee=60 },claimPaid));
		}

		[TestMethod]
		public void DateOfService_InsideRangeMatches_OutsideMismatches() {
			Hx835_Claim claimPaid=EraClaim(EraProc("Z0120",60,48,1001));
			claimPaid.DateServiceStart=new DateTime(2026,9,1);
			claimPaid.DateServiceEnd=new DateTime(2026,9,5);
			Assert.IsFalse(EraClaimMismatch.IsDateOfServiceMismatch(new Claim { DateService=new DateTime(2026,9,1),ClaimType="P" },claimPaid));
			Assert.IsFalse(EraClaimMismatch.IsDateOfServiceMismatch(new Claim { DateService=new DateTime(2026,9,5,14,0,0),ClaimType="P" },claimPaid));
			Assert.IsTrue(EraClaimMismatch.IsDateOfServiceMismatch(new Claim { DateService=new DateTime(2026,8,31),ClaimType="P" },claimPaid));
			Assert.IsTrue(EraClaimMismatch.IsDateOfServiceMismatch(new Claim { DateService=new DateTime(2026,9,6),ClaimType="P" },claimPaid));
		}

		[TestMethod]
		public void DateOfService_PreauthWith1900IsNeverAMismatch() {
			Hx835_Claim claimPaid=EraClaim(EraProc("Z0120",60,0,1001));
			claimPaid.DateServiceStart=new DateTime(1900,1,1);
			claimPaid.DateServiceEnd=new DateTime(1900,1,1);
			Assert.IsFalse(EraClaimMismatch.IsDateOfServiceMismatch(new Claim { DateService=new DateTime(2026,9,1),ClaimType="PreAuth" },claimPaid));
			Assert.IsTrue(EraClaimMismatch.IsDateOfServiceMismatch(new Claim { DateService=new DateTime(2026,9,1),ClaimType="P" },claimPaid));
		}

		[TestMethod]
		public void Messages_NameTheExpectedValues() {
			string message=EraClaimMismatch.GetClaimFeeMismatchMessage(95.5,100);
			StringAssert.Contains(message,"Claim fee on claim does not match ERA.");
			StringAssert.Contains(message,"Claim fee on claim: 95.50");
			StringAssert.Contains(message,"Expected from ERA: 100.00");
			StringAssert.EndsWith(message,"Continue anyway?");
			message=EraClaimMismatch.GetDateOfServiceMismatchMessage(new DateTime(2026,8,31),new DateTime(2026,9,1),new DateTime(2026,9,5));
			StringAssert.Contains(message,"Date of service on claim: 8/31/2026");
			StringAssert.Contains(message,"Expected from ERA: 9/1/2026 to 9/5/2026");
			StringAssert.EndsWith(message,"Continue anyway?");
			message=EraClaimMismatch.GetDateOfServiceMismatchMessage(new DateTime(2026,8,31),new DateTime(2026,9,1),new DateTime(2026,9,1));
			StringAssert.Contains(message,"Expected from ERA: 9/1/2026\r\n");
			message=EraClaimMismatch.GetSplitClaimFeeMismatchMessage(40);
			StringAssert.Contains(message,"Expected from ERA: 40.00");
			StringAssert.EndsWith(message,"Continue anyway?");
		}
	}
}
