// The audit text for birthdate additions, changes and removals (feature v26_1-tools-audit-birthdate). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2AUDITTests {
	[TestClass]
	public class BirthdateAuditTextTests {
		private CultureInfo _cultureInfoOld;

		[TestInitialize]
		public void TestInitialize() {
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestCleanup]
		public void TestCleanup() {
			Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
		}

		[TestMethod]
		public void Added_FromBlankToDate() {
			Assert.AreEqual("Patient birthdate changed from '' to '3/4/1985'",Patients.GetBirthdateChangeLogText(DateTime.MinValue,new DateTime(1985,3,4)));
		}

		[TestMethod]
		public void Changed_ShowsOldAndNewDates() {
			Assert.AreEqual("Patient birthdate changed from '3/4/1985' to '4/3/1985'",
				Patients.GetBirthdateChangeLogText(new DateTime(1985,3,4),new DateTime(1985,4,3)));
		}

		[TestMethod]
		public void Removed_FromDateToBlank() {
			Assert.AreEqual("Patient birthdate changed from '12/31/2001' to ''",Patients.GetBirthdateChangeLogText(new DateTime(2001,12,31),DateTime.MinValue));
		}

		[TestMethod]
		public void SameDate_NoText() {
			Assert.AreEqual("",Patients.GetBirthdateChangeLogText(new DateTime(1985,3,4),new DateTime(1985,3,4)));
			//The column is a date: a time of day on either side is not a change.
			Assert.AreEqual("",Patients.GetBirthdateChangeLogText(new DateTime(1985,3,4,0,0,0),new DateTime(1985,3,4,13,45,0)));
		}

		[TestMethod]
		public void BlankOnBothSides_NoText() {
			Assert.AreEqual("",Patients.GetBirthdateChangeLogText(DateTime.MinValue,DateTime.MinValue));
			//Before 1880 counts as blank everywhere in the program.
			Assert.AreEqual("",Patients.GetBirthdateChangeLogText(DateTime.MinValue,new DateTime(1850,1,1)));
			Assert.IsTrue(Patients.IsBirthdateBlank(new DateTime(1879,12,31)));
			Assert.IsFalse(Patients.IsBirthdateBlank(new DateTime(1880,1,1)));
		}

		[TestMethod]
		public void OldBirthdateBefore1880_ShownAsBlank() {
			Assert.AreEqual("Patient birthdate changed from '' to '1/2/1990'",Patients.GetBirthdateChangeLogText(new DateTime(1800,5,5),new DateTime(1990,1,2)));
		}

		[TestMethod]
		public void ShortDateFormat_FollowsTheComputerCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-GB");
			Assert.AreEqual("Patient birthdate changed from '04/03/1985' to '05/03/1985'",
				Patients.GetBirthdateChangeLogText(new DateTime(1985,3,4),new DateTime(1985,3,5)));
		}

		[TestMethod]
		public void Text_MatchesTheOtherPatientFieldEntries() {
			//Same wording as the address, phone and name lines that InsertAddressChangeSecurityLogEntry writes, so the audit trail reads alike and a
			//search for "birthdate" finds every entry.
			StringAssert.StartsWith(Patients.GetBirthdateChangeLogText(DateTime.MinValue,new DateTime(1985,3,4)),"Patient birthdate changed from '");
		}

		[TestMethod]
		public void NullPatient_DoesNothing() {
			//No database: the call returns before it would log.
			Patients.InsertBirthdateChangeSecurityLogEntry(null,null);
			Patient patient=new Patient { PatNum=1,Birthdate=new DateTime(1985,3,4) };
			Patients.InsertBirthdateChangeSecurityLogEntry(patient.Copy(),patient);
		}
	}
}
