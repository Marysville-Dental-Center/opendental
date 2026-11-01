// v25_4-misc-diagnostics-info: the local Diagnostics text of Help, About (OpenDentBusiness.LocalDiagnostics).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2MISCTests {
	[TestClass]
	public class DiagnosticsTests {
		#region No database
		[TestMethod]
		public void ClockDifference_SaysWhichClockIsAheadAndByHowMuch() {
			DateTime dateTimeWorkstation=new DateTime(2026,10,25,9,0,0);
			Assert.AreEqual("none",LocalDiagnostics.GetClockDifferenceText(dateTimeWorkstation,dateTimeWorkstation));
			Assert.AreEqual("none",LocalDiagnostics.GetClockDifferenceText(dateTimeWorkstation,dateTimeWorkstation.AddMilliseconds(400)));
			Assert.AreEqual("+1 second (server ahead)",LocalDiagnostics.GetClockDifferenceText(dateTimeWorkstation,dateTimeWorkstation.AddSeconds(1)));
			Assert.AreEqual("-3 seconds (server behind)",LocalDiagnostics.GetClockDifferenceText(dateTimeWorkstation,dateTimeWorkstation.AddSeconds(-3)));
			Assert.AreEqual("+1 minute 5 seconds (server ahead)",
				LocalDiagnostics.GetClockDifferenceText(dateTimeWorkstation,dateTimeWorkstation.AddSeconds(65)));
			Assert.AreEqual("-2 hours (server behind)",LocalDiagnostics.GetClockDifferenceText(dateTimeWorkstation,dateTimeWorkstation.AddHours(-2)));
			Assert.AreEqual("+1 day 1 hour 1 minute 1 second (server ahead)",
				LocalDiagnostics.GetClockDifferenceText(dateTimeWorkstation,dateTimeWorkstation.AddSeconds(86400+3600+61)));
		}

		[TestMethod]
		public void FormatBytes_UsesBinaryUnitsWithOneDecimal() {
			Assert.AreEqual("0 bytes",LocalDiagnostics.FormatBytes(0));
			Assert.AreEqual("1023 bytes",LocalDiagnostics.FormatBytes(1023));
			Assert.AreEqual("1.0 KB",LocalDiagnostics.FormatBytes(1024));
			Assert.AreEqual("1.5 MB",LocalDiagnostics.FormatBytes(1024*1024*3/2));
			Assert.AreEqual("16.0 GB",LocalDiagnostics.FormatBytes(16L*1024*1024*1024));
			Assert.AreEqual("2.0 TB",LocalDiagnostics.FormatBytes(2L*1024*1024*1024*1024));
			Assert.AreEqual("unknown",LocalDiagnostics.FormatBytes(-1));
		}

		[TestMethod]
		public void NetFrameworkName_FollowsTheReleaseNumbers() {
			Assert.AreEqual("4.8.1 or later",LocalDiagnostics.GetNetFrameworkName(533320));
			Assert.AreEqual("4.8",LocalDiagnostics.GetNetFrameworkName(528040));
			Assert.AreEqual("4.8",LocalDiagnostics.GetNetFrameworkName(528372));
			Assert.AreEqual("4.7.2",LocalDiagnostics.GetNetFrameworkName(461808));
			Assert.AreEqual("older than 4.7",LocalDiagnostics.GetNetFrameworkName(394802));
		}

		[TestMethod]
		public void TimeZoneText_HasTheUtcOffset() {
			string text=LocalDiagnostics.GetTimeZoneText(TimeZoneInfo.Utc,new DateTime(2026,1,15,12,0,0));
			StringAssert.EndsWith(text,"(UTC+00:00)");
		}

		[TestMethod]
		public void Preferences_NeverIncludePasswordsHashesOrRegistrationKeys() {
			List<PrefName> listPrefNames=LocalDiagnostics.GetPrefNamesShown();
			Assert.IsTrue(listPrefNames.Count>10);
			CollectionAssert.DoesNotContain(listPrefNames,PrefName.ReportingServerMySqlPassHash);
			CollectionAssert.DoesNotContain(listPrefNames,PrefName.ReadOnlyServerMySqlPassHash);
			CollectionAssert.DoesNotContain(listPrefNames,PrefName.RegistrationKey);
			CollectionAssert.DoesNotContain(listPrefNames,PrefName.RegistrationKeyIsDisabled);
			foreach(PrefName prefName in listPrefNames) {
				Assert.IsFalse(LocalDiagnostics.IsSecretPrefName(prefName.ToString()),prefName+" looks like a secret");
			}
			//Every preference whose name says it holds a password, hash or key is recognized, so it can never be listed by mistake.
			List<string> listSecretNames=Enum.GetNames(typeof(PrefName)).Where(x => x.ToLowerInvariant().Contains("password")
				|| x.ToLowerInvariant().Contains("passhash")).ToList();
			Assert.IsTrue(listSecretNames.Count>0);
			foreach(string name in listSecretNames) {
				Assert.IsTrue(LocalDiagnostics.IsSecretPrefName(name),name);
			}
			Assert.IsTrue(LocalDiagnostics.IsSecretPrefName("RegistrationKey"));
			Assert.IsFalse(LocalDiagnostics.IsSecretPrefName("ProgramVersion"));
		}

		[TestMethod]
		public void DiagnosticsText_OneFailingLineDoesNotStopTheRest() {
			LocalDiagnostics.DiagnosticsText diagnosticsText=new LocalDiagnostics.DiagnosticsText();
			diagnosticsText.AddSection("Section");
			diagnosticsText.AddLine("First",() => throw new InvalidOperationException("boom"));
			diagnosticsText.AddLine("Second",() => "fine");
			diagnosticsText.AddLine("Third",() => null);
			string[] arrayLines=diagnosticsText.ToString().Split(new[] { Environment.NewLine },StringSplitOptions.None);
			Assert.AreEqual(LocalDiagnostics.SEPARATOR,arrayLines[0]);
			Assert.AreEqual("Section",arrayLines[1]);
			Assert.AreEqual("  First: ERROR: boom",arrayLines[2]);
			Assert.AreEqual("  Second: fine",arrayLines[3]);
			Assert.AreEqual("  Third: ",arrayLines[4]);
		}
		#endregion No database

		#region Database
		[TestMethod]
		public void GetDiagnostics_HasEveryNewSection_FromThisDatabase() {
			TestDb.ResetOrSkip();
			string text=LocalDiagnostics.GetDiagnostics(patNum:42);
			foreach(string section in new[] { "Program","Date and time","Computer","Database","Session","Preferences" }) {
				StringAssert.Contains(text,LocalDiagnostics.SEPARATOR+Environment.NewLine+section+Environment.NewLine,"Section "+section);
			}
			//Date and time on both clocks, with the difference.
			StringAssert.Contains(text,"  Workstation: ");
			StringAssert.Contains(text,"  Workstation time zone: ");
			Assert.IsFalse(GetLine(text,"Database server").Contains("ERROR"),GetLine(text,"Database server"));
			Assert.IsFalse(GetLine(text,"Difference (server minus workstation)").Contains("ERROR"));
			//The server is local, so its clock is the workstation's, give or take the second NOW() is truncated to.
			string difference=GetValue(text,"Difference (server minus workstation)");
			Assert.IsTrue(difference=="none" || difference.StartsWith("+1 second") || difference.StartsWith("-1 second")
				|| difference.StartsWith("+2 seconds") || difference.StartsWith("-2 seconds"),difference);
			//Hardware and program details.
			foreach(string label in new[] { "Computer name","OS architecture","CPU cores","Memory total","Memory free","Screens","Free disk, program drive",
				"Program path",".NET runtime","Process" })
			{
				Assert.IsFalse(string.IsNullOrWhiteSpace(GetValue(text,label)),label+" is blank");
				Assert.IsFalse(GetValue(text,label).StartsWith("ERROR"),label+": "+GetValue(text,label));
			}
			Assert.AreEqual("(images are not stored in a folder)",GetValue(text,"Free disk, image folder drive"));
			StringAssert.Contains(GetValue(text,"Server version"),"MariaDB");
			Assert.AreEqual(TestDb.DbName,GetValue(text,"Database name"));
			Assert.AreEqual("42",GetValue(text,"PatNum"));
			Assert.AreEqual("24.3.63.0",GetValue(text,"ProgramVersion"));
		}

		[TestMethod]
		public void GetDiagnostics_NeverShowsPasswordsOrHashes() {
			TestDb.ResetOrSkip();
			const string secret="SyntheticSecretValue_7f3a";
			TestDb.SetPref(nameof(PrefName.ReportingServerMySqlPassHash),secret);
			TestDb.SetPref(nameof(PrefName.ReadOnlyServerMySqlPassHash),secret);
			TestDb.SetPref(nameof(PrefName.RegistrationKey),secret);
			TestDb.RefreshCaches();
			string text=LocalDiagnostics.GetDiagnostics();
			Assert.IsFalse(text.Contains(secret),"A password hash or key was shown.");
			Assert.IsFalse(text.Contains("PassHash"));
			Assert.IsFalse(text.Contains("RegistrationKey"));
			Assert.IsFalse(text.ToLowerInvariant().Contains("password"));
			Assert.AreEqual("none selected",GetValue(text,"PatNum"));
		}

		[TestMethod]
		public void BugSubmissionsGetDiagnostics_ReturnsTheLocalText() {
			TestDb.ResetOrSkip();
			string text=BugSubmissions.GetDiagnostics(7);
			StringAssert.Contains(text,LocalDiagnostics.SEPARATOR+Environment.NewLine+"Date and time"+Environment.NewLine);
			Assert.AreEqual("7",GetValue(text,"PatNum"));
		}

		[TestMethod]
		public void ServerInfo_IsOneRowWithTheServerClock() {
			TestDb.ResetOrSkip();
			System.Data.DataTable table=LocalDiagnostics.GetServerInfo();
			Assert.AreEqual(1,table.Rows.Count);
			DateTime dateTimeServer=LocalDiagnostics.ParseServerDateTime(table.Rows[0]["ServerNow"].ToString());
			Assert.IsTrue(Math.Abs((dateTimeServer-DateTime.Now).TotalMinutes)<5);
			Assert.IsFalse(string.IsNullOrEmpty(table.Rows[0]["StorageEngine"].ToString()));
		}
		#endregion Database

		private static string GetLine(string text,string label) {
			string line=text.Split(new[] { Environment.NewLine },StringSplitOptions.None).FirstOrDefault(x => x.StartsWith("  "+label+": "));
			Assert.IsNotNull(line,"No line "+label);
			return line;
		}

		private static string GetValue(string text,string label) {
			return GetLine(text,label).Substring(("  "+label+": ").Length);
		}
	}
}
