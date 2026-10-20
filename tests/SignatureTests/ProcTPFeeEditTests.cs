// Permission "Treatment Planned Procedure Edit Fee" (25.2, v25_2-security-tp-proc-edit-fee; EnumPermType.ProcTPEditFee = 270).
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace SignatureTests {
	[TestClass]
	public class ProcTPFeeEditTests {
		[TestMethod]
		public void PermissionIsThePublished270() {
			Assert.AreEqual(270,(int)EnumPermType.ProcTPEditFee);
		}

		[TestMethod]
		public void PermissionMatrix_EveryStatus() {
			foreach(ProcStat procStat in Enum.GetValues(typeof(ProcStat)).Cast<ProcStat>()) {
				bool isTp=procStat==ProcStat.TP || procStat==ProcStat.TPi;
				Assert.AreEqual(isTp,ProcTPFeeEdit.IsTreatPlanStatus(procStat),procStat.ToString());
				Assert.IsTrue(ProcTPFeeEdit.CanEditFee(procStat,true),"With the permission: "+procStat);
				Assert.AreEqual(!isTp,ProcTPFeeEdit.CanEditFee(procStat,false),"Without the permission: "+procStat);
			}
		}

		[TestMethod]
		public void LogOnlyRealChangesOnTreatmentPlannedProcedures() {
			Assert.IsTrue(ProcTPFeeEdit.ShouldLog(ProcStat.TP,50,60));
			Assert.IsTrue(ProcTPFeeEdit.ShouldLog(ProcStat.TPi,60,0));
			Assert.IsFalse(ProcTPFeeEdit.ShouldLog(ProcStat.TP,50,50.0000000001),"Rounding noise is not a change.");
			Assert.IsFalse(ProcTPFeeEdit.ShouldLog(ProcStat.C,50,60));
			Assert.IsFalse(ProcTPFeeEdit.ShouldLog(ProcStat.EO,50,60));
		}

		[TestMethod]
		public void LogText_NamesTheCodeAndBothFees() {
			string text=ProcTPFeeEdit.GetLogText("D0120",50,62.5);
			StringAssert.Contains(text,"D0120");
			StringAssert.Contains(text,(50.0).ToString("F"));
			StringAssert.Contains(text,(62.5).ToString("F"));
			Assert.IsTrue(text.IndexOf((50.0).ToString("F"))<text.IndexOf((62.5).ToString("F")),"Old fee first.");
		}

		[TestMethod]
		public void Tooltip_NamesThePermission() {
			StringAssert.Contains(ProcTPFeeEdit.GetNoPermissionTip(),"Treatment Planned Procedure Edit Fee");
		}
	}

	[TestClass]
	public class OrthoChartLockTests {
		[TestMethod]
		public void LockValues_MatchThePublishedDescription() {
			Assert.AreEqual(0,PatientNotes.ORTHO_LOCK_NONE);
			Assert.AreEqual(-1,PatientNotes.ORTHO_LOCK_SAME_USER_TAKEOVER);
			Assert.AreEqual(-5,PatientNotes.ORTHO_LOCK_LOCAL_SWITCH);
		}

		[TestMethod]
		public void LocalSwitch_IsAHeldLock() {
			Assert.IsTrue(PatientNotes.IsOrthoLockHeld(-5));
			Assert.IsTrue(PatientNotes.IsOrthoLockHeld(-1));
			Assert.IsTrue(PatientNotes.IsOrthoLockHeld(12));
			Assert.IsFalse(PatientNotes.IsOrthoLockHeld(0));
		}

		[TestMethod]
		public void LockedByText_NeverShowsABlankName() {
			Func<long,string> getName=x => x==12 ? "drjones" : "";
			Assert.AreEqual("drjones",PatientNotes.GetOrthoLockedByText(12,getName));
			Assert.AreEqual("Nobody",PatientNotes.GetOrthoLockedByText(0,getName));
			Assert.AreNotEqual("",PatientNotes.GetOrthoLockedByText(-5,getName));
			Assert.AreNotEqual("",PatientNotes.GetOrthoLockedByText(-1,getName));
		}
	}

	[TestClass]
	public class OrthoChartLockDbTests {
		[TestInitialize]
		public void TestInitialize() {
			ScratchDb.RequireDb();
		}

		[TestMethod]
		public void LocalSwitchValue_IsStoredAndReadBack() {
			DataCore.NonQ("INSERT INTO patientnote (PatNum,ICEName,ICEPhone,Consent,UserNumOrthoLocked,Pronoun) VALUES (31,'','',0,0,0)");
			PatientNotes.SetUserNumOrthoLocked(31,12);
			Assert.AreEqual(12,PatientNotes.GetUserNumOrthoLocked(31));
			PatientNotes.SetUserNumOrthoLocked(31,PatientNotes.ORTHO_LOCK_LOCAL_SWITCH);
			long userNumLocked=PatientNotes.GetUserNumOrthoLocked(31);
			Assert.AreEqual(-5,userNumLocked);
			Assert.IsTrue(PatientNotes.IsOrthoLockHeld(userNumLocked),"Another workstation opening the chart now is locked out.");
			PatientNotes.SetUserNumOrthoLocked(31,PatientNotes.ORTHO_LOCK_NONE);
			Assert.IsFalse(PatientNotes.IsOrthoLockHeld(PatientNotes.GetUserNumOrthoLocked(31)));
		}
	}
}
