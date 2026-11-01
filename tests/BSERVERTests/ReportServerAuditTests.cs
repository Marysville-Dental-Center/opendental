// v24_4-setup-reportserver-audit: the Audit Trail entry Report Setup writes when the Report Server settings change.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BSERVERTests {
	[TestClass]
	public class ReportServerAuditTests {
		private static ReportServerSettings Direct(string serverName,string database,string user,string password,string sslCa) {
			Secrets.Encrypt(password,out string passHash);
			return new ReportServerSettings { ServerName=serverName,Database=database,MySqlUser=user,MySqlPassHash=passHash,SslCa=sslCa };
		}

		private static ReportServerSettings MiddleTier(string uri) {
			return new ReportServerSettings { URI=uri };
		}

		[TestMethod]
		public void Mode() {
			Assert.AreEqual(ReportServerMode.Off,new ReportServerSettings().Mode);
			Assert.AreEqual(ReportServerMode.Direct,Direct("rptsrv","opendental","rpt","pw","").Mode);
			Assert.AreEqual(ReportServerMode.MiddleTier,MiddleTier("https://mt.example.test/OpenDentalServer/ServiceMain.asmx").Mode);
		}

		[TestMethod]
		public void NothingChanged_IsBlank() {
			Assert.AreEqual("",ReportServerAudit.BuildLogEntry(Direct("rptsrv","opendental","rpt","pw","C:\\ca.pem"),
				Direct("rptsrv","opendental","rpt","pw","C:\\ca.pem")));
		}

		[TestMethod]
		public void SslCaChange_DoesNotReplaceTheOtherLines() {
			//In 24.3 the SSL CA line was assigned with = instead of +=, so it replaced the server, database and user lines before it.
			string logEntry=ReportServerAudit.BuildLogEntry(Direct("rptsrv1","db1","user1","pw","C:\\old.pem"),Direct("rptsrv2","db2","user2","pw","C:\\new.pem"));
			string[] arrayLines=logEntry.Split('\n');
			CollectionAssert.AreEqual(new[] {
				"Server Name changed from rptsrv1 to rptsrv2",
				"Database changed from db1 to db2",
				"MySQL User changed from user1 to user2",
				"SSL CA certificate (.pem) path changed from C:\\old.pem to C:\\new.pem",
			},arrayLines);
		}

		[TestMethod]
		public void SwitchToMiddleTier_IsLogged() {
			string logEntry=ReportServerAudit.BuildLogEntry(Direct("rptsrv","opendental","rpt","pw",""),MiddleTier("https://mt.example.test/s.asmx"));
			StringAssert.StartsWith(logEntry,"Report Server changed from Direct Connection to Middle Tier\n");
			StringAssert.Contains(logEntry,"Server Name changed from rptsrv to (blank)");
			StringAssert.Contains(logEntry,"Middle Tier URI changed from (blank) to https://mt.example.test/s.asmx");
		}

		[TestMethod]
		public void TurnedOff_IsLogged() {
			string logEntry=ReportServerAudit.BuildLogEntry(MiddleTier("https://mt.example.test/s.asmx"),new ReportServerSettings());
			StringAssert.StartsWith(logEntry,"Report Server changed from Middle Tier to Off");
		}

		[TestMethod]
		public void PasswordChange_IsLoggedWithoutThePassword() {
			ReportServerSettings settingsOld=Direct("rptsrv","opendental","rpt","OldSecret1","");
			ReportServerSettings settingsNew=Direct("rptsrv","opendental","rpt","NewSecret2","");
			string logEntry=ReportServerAudit.BuildLogEntry(settingsOld,settingsNew);
			Assert.AreEqual("MySQL Password changed",logEntry);
			foreach(string secret in new[] { "OldSecret1","NewSecret2",settingsOld.MySqlPassHash,settingsNew.MySqlPassHash }) {
				Assert.IsFalse(logEntry.Contains(secret),"The log must never contain a password or its stored form.");
			}
		}
	}
}
