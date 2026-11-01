// v25_4-security-patientselected-audit: the Patient Selected (275) Audit Trail entry, its off-by-default setting and the 60-second repeat rule.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2SHELLTests {
	[TestClass]
	public class PatientSelectedAuditTests {
		private static readonly DateTime _dateTime=new DateTime(2026,10,25,9,0,0);

		[TestInitialize]
		public void TestInitialize() {
			PatientSelectedAudit.ClearRepeatMemory();
		}

		#region Repeat rule (no database)
		[TestMethod]
		public void RepeatRule_SamePatientWithinSixtySeconds_IsLoggedOnce() {
			Assert.IsTrue(PatientSelectedAudit.ShouldLog(1,100,_dateTime));
			Assert.IsFalse(PatientSelectedAudit.ShouldLog(1,100,_dateTime.AddSeconds(1)));
			Assert.IsFalse(PatientSelectedAudit.ShouldLog(1,100,_dateTime.AddSeconds(59.9)));
			Assert.IsTrue(PatientSelectedAudit.ShouldLog(1,100,_dateTime.AddSeconds(60)),"After 60 seconds the selection is logged again.");
		}

		[TestMethod]
		public void RepeatRule_IsPerUserAndPerPatient() {
			Assert.IsTrue(PatientSelectedAudit.ShouldLog(1,100,_dateTime));
			Assert.IsTrue(PatientSelectedAudit.ShouldLog(2,100,_dateTime.AddSeconds(5)),"Another user selecting the same patient is logged.");
			Assert.IsTrue(PatientSelectedAudit.ShouldLog(1,200,_dateTime.AddSeconds(10)),"The same user selecting another patient is logged.");
			Assert.IsFalse(PatientSelectedAudit.ShouldLog(1,100,_dateTime.AddSeconds(20)),"Back to the first patient within 60 seconds: no second entry.");
		}

		[TestMethod]
		public void RepeatRule_SuppressedSelectionDoesNotExtendTheWindow() {
			Assert.IsTrue(PatientSelectedAudit.ShouldLog(1,100,_dateTime));
			Assert.IsFalse(PatientSelectedAudit.ShouldLog(1,100,_dateTime.AddSeconds(50)));
			Assert.IsTrue(PatientSelectedAudit.ShouldLog(1,100,_dateTime.AddSeconds(61)),"The window runs from the last entry written, not the last selection.");
		}

		[TestMethod]
		public void RepeatRule_ClockMovedBack_LogsAgain() {
			Assert.IsTrue(PatientSelectedAudit.ShouldLog(1,100,_dateTime));
			Assert.IsTrue(PatientSelectedAudit.ShouldLog(1,100,_dateTime.AddMinutes(-5)));
		}
		#endregion

		#region Database
		private static void Prepare() {
			string skipReason=ScratchDb.EnsureCreated();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			ScratchDb.Reset("securitylog","securityloghash");
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
		}

		private static List<(long PatNum,long UserNum,string LogText)> GetEntries() {
			return DataCore.GetTable("SELECT PatNum,UserNum,LogText FROM securitylog WHERE PermType="+(int)EnumPermType.PatientSelected+" ORDER BY SecurityLogNum")
				.Select().Select(x => (Convert.ToInt64(x[0]),Convert.ToInt64(x[1]),x[2].ToString())).ToList();
		}

		[TestMethod]
		public void Off_ByDefault_WritesNothing() {
			Prepare();
			Assert.IsFalse(PatientSelectedAudit.IsEnabled(),"No preference row means off.");
			Assert.IsFalse(PatientSelectedAudit.LogIfEnabled(100));
			Assert.AreEqual(0,GetEntries().Count);
			Assert.AreEqual(0,ScratchDb.GetLong("SELECT COUNT(*) FROM preference WHERE PrefName='"+PatientSelectedAudit.PREF_NAME+"'"),
				"Reading the setting never inserts a row.");
		}

		[TestMethod]
		public void SetEnabled_InsertsTheRowOnce_AndReportsChanges() {
			Prepare();
			Assert.IsFalse(PatientSelectedAudit.SetEnabled(false),"Off is already the value a missing row reads as.");
			Assert.AreEqual(0,ScratchDb.GetLong("SELECT COUNT(*) FROM preference WHERE PrefName='"+PatientSelectedAudit.PREF_NAME+"'"));
			Assert.IsTrue(PatientSelectedAudit.SetEnabled(true));
			Assert.IsTrue(PatientSelectedAudit.IsEnabled(),"The writing workstation's cache is updated at once.");
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+PatientSelectedAudit.PREF_NAME+"'"));
			Assert.IsFalse(PatientSelectedAudit.SetEnabled(true));
			Assert.IsTrue(PatientSelectedAudit.SetEnabled(false));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+PatientSelectedAudit.PREF_NAME+"'"));
			Assert.AreEqual(1,ScratchDb.GetLong("SELECT COUNT(*) FROM preference WHERE PrefName='"+PatientSelectedAudit.PREF_NAME+"'"));
		}

		[TestMethod]
		public void On_WritesOneEntryPerSelection_WithThePatient() {
			Prepare();
			PatientSelectedAudit.SetEnabled(true);
			Prefs.RefreshCache();
			Assert.IsTrue(PatientSelectedAudit.IsEnabled());
			Assert.IsTrue(PatientSelectedAudit.LogIfEnabled(100));
			Assert.IsFalse(PatientSelectedAudit.LogIfEnabled(100),"Same user, same patient, within 60 seconds.");
			Assert.IsTrue(PatientSelectedAudit.LogIfEnabled(200));
			Assert.IsFalse(PatientSelectedAudit.LogIfEnabled(0),"No patient (unloading the patient) is not a selection.");
			Security.CurUser=new Userod { UserNum=2,UserName="Synthetic2" };
			Assert.IsTrue(PatientSelectedAudit.LogIfEnabled(100),"Another user.");
			List<(long PatNum,long UserNum,string LogText)> listEntries=GetEntries();
			CollectionAssert.AreEqual(new[] { "100/1","200/1","100/2" },listEntries.Select(x => x.PatNum+"/"+x.UserNum).ToArray());
			Assert.IsTrue(listEntries.All(x => x.LogText==PatientSelectedAudit.LOG_TEXT));
			Assert.AreEqual(3,ScratchDb.GetLong("SELECT COUNT(*) FROM securityloghash"),"Every entry is hashed like other audit entries.");
		}

		[TestMethod]
		public void On_NobodyLoggedOn_WritesNothing() {
			Prepare();
			PatientSelectedAudit.SetEnabled(true);
			Prefs.RefreshCache();
			Security.CurUser=null;
			try {
				Assert.IsFalse(PatientSelectedAudit.LogIfEnabled(100));
			}
			finally {
				Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
			}
			Assert.AreEqual(0,GetEntries().Count);
		}

		[TestMethod]
		public void TurnedOffAgain_StopsWriting() {
			Prepare();
			PatientSelectedAudit.SetEnabled(true);
			Prefs.RefreshCache();
			Assert.IsTrue(PatientSelectedAudit.LogIfEnabled(100));
			PatientSelectedAudit.SetEnabled(false);
			Prefs.RefreshCache();
			Assert.IsFalse(PatientSelectedAudit.LogIfEnabled(300));
			Assert.AreEqual(1,GetEntries().Count);
		}
		#endregion

		[TestMethod]
		public void Setting_IsTheForksOwnRow_NotInPrefName() {
			Assert.AreEqual("ForkAuditTrailPatientSelected",PatientSelectedAudit.PREF_NAME);
			Assert.IsFalse(Enum.IsDefined(typeof(PrefName),PatientSelectedAudit.PREF_NAME),
				"A fork-invented setting stays out of PrefName, which holds only names Open Dental publishes (RegistryTests).");
			Assert.IsFalse(Enum.GetNames(typeof(PrefName)).Any(x => x=="AuditTrailPatientSelected"),"The unprefixed name of the first build is gone.");
		}

		[TestMethod]
		public void Permission_IsAuditOnly() {
			Assert.AreEqual(275,(int)EnumPermType.PatientSelected);
			Assert.IsTrue(GroupPermissions.HasAuditTrail(EnumPermType.PatientSelected),"The entries can be filtered in the Audit Trail.");
		}
	}
}
