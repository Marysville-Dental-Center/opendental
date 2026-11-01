// B-SERVER (v24_4-setup-reportserver-audit): the Audit Trail entry Report Setup writes when the Report Server settings change.
// Open Dental added this entry in 24.4 and backported it to 24.3.35, so our 24.3.63 code already wrote it. This class fixes two gaps in that
// code: the SSL CA line replaced the lines before it, and a switch between Direct Connection and Middle Tier was not logged as such.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>How the reporting server is reached.</summary>
	public enum ReportServerMode {
		///<summary>No separate reporting server.</summary>
		Off,
		///<summary>Direct connection to a MySQL/MariaDB server.</summary>
		Direct,
		///<summary>Through a Middle Tier URI.</summary>
		MiddleTier,
	}

	///<summary>The Report Server preferences (ReportingServer*) at one moment, for comparing before and after a save.</summary>
	public class ReportServerSettings {
		public string ServerName="";
		public string Database="";
		public string MySqlUser="";
		///<summary>The stored (encrypted) password. Only compared, never logged.</summary>
		public string MySqlPassHash="";
		public string SslCa="";
		public string URI="";

		///<summary>Middle Tier when a URI is set, Direct when a server name is set, otherwise Off. Same rule Report Setup uses to fill its options.</summary>
		public ReportServerMode Mode {
			get {
				if(!string.IsNullOrEmpty(URI)) {
					return ReportServerMode.MiddleTier;
				}
				if(!string.IsNullOrEmpty(ServerName)) {
					return ReportServerMode.Direct;
				}
				return ReportServerMode.Off;
			}
		}

		///<summary>The current values from the preference cache. Missing preferences read as blank.</summary>
		public static ReportServerSettings FromPrefCache() {
			ReportServerSettings reportServerSettings=new ReportServerSettings();
			reportServerSettings.ServerName=PrefC.GetStringSilent(PrefName.ReportingServerCompName);
			reportServerSettings.Database=PrefC.GetStringSilent(PrefName.ReportingServerDbName);
			reportServerSettings.MySqlUser=PrefC.GetStringSilent(PrefName.ReportingServerMySqlUser);
			reportServerSettings.MySqlPassHash=PrefC.GetStringSilent(PrefName.ReportingServerMySqlPassHash);
			reportServerSettings.SslCa=PrefC.GetStringSilent(PrefName.ReportingServerSslCa);
			reportServerSettings.URI=PrefC.GetStringSilent(PrefName.ReportingServerURI);
			return reportServerSettings;
		}
	}

	public static class ReportServerAudit {
		///<summary>Display name of a mode in the log entry.</summary>
		public static string GetModeText(ReportServerMode reportServerMode) {
			switch(reportServerMode) {
				case ReportServerMode.Direct:
					return "Direct Connection";
				case ReportServerMode.MiddleTier:
					return "Middle Tier";
				default:
					return "Off";
			}
		}

		///<summary>One line per change, in a fixed order: the mode, then server name, database, MySQL user, password, SSL CA path and Middle Tier URI.
		///The password line says only that it changed; the password and its stored form never appear. Blank when nothing changed.</summary>
		public static string BuildLogEntry(ReportServerSettings settingsOld,ReportServerSettings settingsNew) {
			List<string> listLines=new List<string>();
			if(settingsOld.Mode!=settingsNew.Mode) {
				listLines.Add("Report Server changed from "+GetModeText(settingsOld.Mode)+" to "+GetModeText(settingsNew.Mode));
			}
			AddChange(listLines,"Server Name",settingsOld.ServerName,settingsNew.ServerName);
			AddChange(listLines,"Database",settingsOld.Database,settingsNew.Database);
			AddChange(listLines,"MySQL User",settingsOld.MySqlUser,settingsNew.MySqlUser);
			if((settingsOld.MySqlPassHash??"")!=(settingsNew.MySqlPassHash??"")) {
				listLines.Add("MySQL Password changed");
			}
			AddChange(listLines,"SSL CA certificate (.pem) path",settingsOld.SslCa,settingsNew.SslCa);
			AddChange(listLines,"Middle Tier URI",settingsOld.URI,settingsNew.URI);
			return string.Join("\n",listLines);
		}

		private static void AddChange(List<string> listLines,string fieldName,string valueOld,string valueNew) {
			valueOld=valueOld??"";
			valueNew=valueNew??"";
			if(valueOld==valueNew) {
				return;
			}
			listLines.Add(fieldName+" changed from "+GetValueText(valueOld)+" to "+GetValueText(valueNew));
		}

		private static string GetValueText(string value) {
			if(value=="") {
				return "(blank)";
			}
			return value;
		}
	}
}
