// The log on lockout rule shared by Open Dental passwords and domain credentials (Userods.IsLockedOut and ApplyLogOnAttempt, factored out of
// CheckUserAndPassword so the domain change-user prompt counts failures the same way).
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace SignatureTests {
	[TestClass]
	public class LockoutTests {
		private static readonly DateTime _dateTimeNow=new DateTime(2026,10,19,9,0,0);

		[TestMethod]
		public void IsLockedOut_FiveRecentFailures() {
			Assert.IsTrue(Userods.IsLockedOut(new Userod { FailedAttempts=5,DateTFail=_dateTimeNow.AddMinutes(-4) },_dateTimeNow));
			Assert.IsTrue(Userods.IsLockedOut(new Userod { FailedAttempts=9,DateTFail=_dateTimeNow },_dateTimeNow));
		}

		[TestMethod]
		public void IsLockedOut_NotWithFewerFailures_OrOldFailures_OrNoFailure() {
			Assert.IsFalse(Userods.IsLockedOut(new Userod { FailedAttempts=4,DateTFail=_dateTimeNow.AddMinutes(-1) },_dateTimeNow));
			Assert.IsFalse(Userods.IsLockedOut(new Userod { FailedAttempts=5,DateTFail=_dateTimeNow.AddMinutes(-6) },_dateTimeNow));
			Assert.IsFalse(Userods.IsLockedOut(new Userod { FailedAttempts=5,DateTFail=DateTime.MinValue },_dateTimeNow));
		}

		[TestMethod]
		public void ApplyLogOnAttempt_FailureCountsAndStampsTheTime() {
			Userod userod=new Userod { FailedAttempts=2,DateTFail=_dateTimeNow.AddMinutes(-1) };
			Userods.ApplyLogOnAttempt(userod,false,_dateTimeNow);
			Assert.AreEqual(3,userod.FailedAttempts);
			Assert.AreEqual(_dateTimeNow,userod.DateTFail);
		}

		[TestMethod]
		public void ApplyLogOnAttempt_OldFailuresAreForgottenFirst() {
			Userod userodFail=new Userod { FailedAttempts=4,DateTFail=_dateTimeNow.AddMinutes(-10) };
			Userods.ApplyLogOnAttempt(userodFail,false,_dateTimeNow);
			Assert.AreEqual(1,userodFail.FailedAttempts,"Counting starts over after 5 minutes.");
			Userod userodGood=new Userod { FailedAttempts=4,DateTFail=_dateTimeNow.AddMinutes(-10) };
			Userods.ApplyLogOnAttempt(userodGood,true,_dateTimeNow);
			Assert.AreEqual(0,userodGood.FailedAttempts);
			Assert.AreEqual(DateTime.MinValue,userodGood.DateTFail);
		}

		[TestMethod]
		public void ApplyLogOnAttempt_RecentFailuresStayAfterAGoodAttempt() {
			//Same as CheckUserAndPassword in 24.3: a good log on within 5 minutes leaves the count alone.
			Userod userod=new Userod { FailedAttempts=3,DateTFail=_dateTimeNow.AddMinutes(-1) };
			Userods.ApplyLogOnAttempt(userod,true,_dateTimeNow);
			Assert.AreEqual(3,userod.FailedAttempts);
		}

		[TestMethod]
		public void ApplyLogOnAttempt_CountDoesNotWrapAround() {
			Userod userod=new Userod { FailedAttempts=byte.MaxValue,DateTFail=_dateTimeNow };
			Userods.ApplyLogOnAttempt(userod,false,_dateTimeNow);
			Assert.AreEqual(byte.MaxValue,userod.FailedAttempts);
		}
	}
}
