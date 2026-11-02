// What the manual grants test is told to use: the temporary account and the office database, by name. Nothing else is configurable: the
// server is always the local MariaDB at 127.0.0.1:3306, as root with a blank password, like every database test of this repository.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DbGrantsTests {
	///<summary>The names the harness runs with, from the environment: DBGRANTS_ACCOUNT (the temporary account, always at 'localhost') and
	///DBGRANTS_DATABASE (the office database; its backups are named after it). Without both the harness does not run. The names are checked
	///here; that the account and the databases do not exist yet is checked on the server (GrantRun.Preflight), so the harness never changes
	///or drops anything it did not create.</summary>
	public class GrantSettings {
		public const string ENV_ACCOUNT="DBGRANTS_ACCOUNT";
		public const string ENV_DATABASE="DBGRANTS_DATABASE";
		///<summary>Optional: where the report is written. Default: %TEMP%\dbgrants-report-yyyyMMdd-HHmmss.txt.</summary>
		public const string ENV_REPORT="DBGRANTS_REPORT";
		///<summary>Optional: "1" skips the Open Dental update chain from 21.1 and the adoption, the slowest part (a few minutes).</summary>
		public const string ENV_SKIP_CHAIN="DBGRANTS_SKIP_CHAIN";
		///<summary>The longest office database name: its backup names add "backup_MM_dd_yyyy_NN" (20 characters) and must stay within 64.</summary>
		public const int MAX_DATABASE_LENGTH=40;

		///<summary>The temporary account's user name. The host is always 'localhost'.</summary>
		public string Account;
		///<summary>The office database's name.</summary>
		public string Database;
		public string ReportPath;
		public bool DoRunChain;

		///<summary>The settings from the environment, or null with the reason the harness does not run.</summary>
		public static GrantSettings FromEnvironment(out string reason) {
			Dictionary<string,string> dict=new Dictionary<string,string>();
			foreach(string name in new[] { ENV_ACCOUNT,ENV_DATABASE,ENV_REPORT,ENV_SKIP_CHAIN }) {
				dict[name]=Environment.GetEnvironmentVariable(name);
			}
			return FromValues(dict,out reason);
		}

		///<summary>The settings from these values (names as the ENV_ constants), or null with the reason the harness does not run.</summary>
		public static GrantSettings FromValues(IDictionary<string,string> dictValues,out string reason) {
			string account=Get(dictValues,ENV_ACCOUNT);
			string database=Get(dictValues,ENV_DATABASE);
			if(account=="" || database=="") {
				reason="Manual test: it runs only when told the temporary account's and the office database's names, in "+ENV_ACCOUNT+" and "
					+ENV_DATABASE+" (for example opmsd_granttest and opmsdgrant_od). It creates that account on the local MariaDB at 127.0.0.1:3306 "
					+"as root, and drops it and every database it made at the end.";
				return null;
			}
			reason=GetAccountProblem(account)??GetDatabaseProblem(database);
			if(reason!=null) {
				return null;
			}
			GrantSettings grantSettings=new GrantSettings();
			grantSettings.Account=account;
			grantSettings.Database=database;
			grantSettings.ReportPath=Get(dictValues,ENV_REPORT);
			grantSettings.DoRunChain=Get(dictValues,ENV_SKIP_CHAIN)!="1";
			return grantSettings;
		}

		///<summary>Null when the account name is usable: lower-case letters, digits and underscores, 3 to 32 characters, starting with a letter, and
		///none of MariaDB's own account names.</summary>
		public static string GetAccountProblem(string account) {
			if(!Regex.IsMatch(account,"^[a-z][a-z0-9_]{2,31}$")) {
				return ENV_ACCOUNT+" must be 3 to 32 lower-case letters, digits or underscores, starting with a letter: "+account;
			}
			if(account=="root" || account=="public" || account.StartsWith("mysql") || account.StartsWith("mariadb")) {
				return ENV_ACCOUNT+" names one of MariaDB's own accounts: "+account;
			}
			return null;
		}

		///<summary>Null when the database name is usable: lower-case letters, digits and underscores, 3 to 40 characters, starting with a letter;
		///not a system database, and not named test or starting with test_, whose rights MariaDB's own set-up can give every account through the
		///PUBLIC role (the server guide, section 5), so that refusals would not show.</summary>
		public static string GetDatabaseProblem(string database) {
			if(!Regex.IsMatch(database,"^[a-z][a-z0-9_]{2,"+(MAX_DATABASE_LENGTH-1)+"}$")) {
				return ENV_DATABASE+" must be 3 to "+MAX_DATABASE_LENGTH+" lower-case letters, digits or underscores, starting with a letter: "+database;
			}
			if(Array.IndexOf(new[] { "mysql","sys","information_schema","performance_schema" },database)>=0) {
				return ENV_DATABASE+" names a system database: "+database;
			}
			if(database=="test" || database.StartsWith("test_")) {
				return ENV_DATABASE+" must not be test or start with test_: MariaDB's own set-up can give every account rights on such databases "
					+"through the PUBLIC role, which would hide the refusals this test is for.";
			}
			return null;
		}

		private static string Get(IDictionary<string,string> dictValues,string name) {
			if(!dictValues.TryGetValue(name,out string value) || value==null) {
				return "";
			}
			return value.Trim();
		}
	}
}
