// The connection settings tool's command-line switches (docs/specs/db-connection-security.md section 4.2).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using DataConnectionBase;

namespace OpenPmsDentalDbSettings {
	///<summary>How the client authenticates: the switches' choice, or Unchanged to keep the mode of the current settings.</summary>
	public enum DbSettingsAuth {
		Unchanged,
		Certificate,
		Password,
	}

	///<summary>A failure of the tool with its exit code (DbSettingsRunner.EXIT_*). The message is for the user and the log, and never contains a
	///secret: the values of switches named like a password are never repeated.</summary>
	public class DbSettingsException:Exception {
		public int ExitCode { get; private set; }

		public DbSettingsException(int exitCode,string message):base(message) {
			ExitCode=exitCode;
		}
	}

	///<summary>The parsed switches. Names are case-insensitive, "/name=value" or "-name=value"; the value may be in double quotes. A value
	///switch that is not given is null. Secrets are never accepted on the command line: they come from /secretsfile or /prompt.</summary>
	public class DbSettingsArgs {
		public string Server;
		public string Database;
		public string User;
		///<summary>The office CA certificate file to copy in.</summary>
		public string CaFile;
		///<summary>A client certificate (.pfx) to import.</summary>
		public string PfxFile;
		///<summary>A client certificate already in LocalMachine\My, normalized (upper-case hex).</summary>
		public string Thumbprint;
		public DbSettingsAuth Auth=DbSettingsAuth.Unchanged;
		///<summary>A file with the lines PfxPassword=... and DbPassword=... (DbSettingsSecrets). Read, never written or deleted.</summary>
		public string SecretsFile;
		///<summary>A file the result line is appended to.</summary>
		public string LogFile;
		public bool IsSilent;
		public bool IsPrompt;
		public bool IsCheck;
		public bool IsTest;
		public bool IsHelp;

		private static readonly string[] _arrayValueSwitches={ "server","database","user","ca","pfx","thumbprint","auth","secretsfile","log" };
		private static readonly string[] _arrayFlagSwitches={ "silent","prompt","check","test","help","?" };
		///<summary>Names that would put a secret on the command line. Refused, and their values are never repeated.</summary>
		private static readonly string[] _arraySecretSwitches={ "password","pass","pwd","dbpassword","dbpass","pfxpassword","pfxpass","secret" };

		///<summary>True when any switch that changes the settings was given.</summary>
		public bool HasChanges {
			get {
				return Server!=null || Database!=null || User!=null || CaFile!=null || PfxFile!=null || Thumbprint!=null || Auth!=DbSettingsAuth.Unchanged;
			}
		}

		///<summary>The usage text the tool shows for /help.</summary>
		public static string Usage() {
			return "OpenPmsDentalDbSettings.exe [switches]\r\n"
				+"Without /silent the window opens, filled from FreeDentalConfig.xml and the switches.\r\n\r\n"
				+"/server=<host[:port]>  /database=<name>  /user=<name>\r\n"
				+"/ca=<office CA certificate file>\r\n"
				+"/pfx=<client certificate .pfx to import>  or  /thumbprint=<certificate in LocalMachine\\My>\r\n"
				+"/auth=certificate|password\r\n"
				+"/secretsfile=<file with PfxPassword=... and DbPassword=... lines; the caller deletes it>\r\n"
				+"/prompt  ask for missing secrets\r\n"
				+"/silent  no window; /test  test the connection after saving (with /silent)\r\n"
				+"/check  only check the current settings against the rule (exit code 0 or 5)\r\n"
				+"/log=<file>  append the result\r\n\r\n"
				+"Exit codes: 0 done, 1 bad switches, 2 invalid value, 3 writing failed, 4 connection test failed, 5 refused by the rule, 6 cancelled.";
		}

		///<summary>Parses the switches. Throws DbSettingsException (exit code 1) for an unknown, repeated or secret switch, a bad value or a bad
		///combination.</summary>
		public static DbSettingsArgs Parse(string[] arrayArgs) {
			DbSettingsArgs dbSettingsArgs=new DbSettingsArgs();
			HashSet<string> hashSetSeen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string[] arrayArgsAll=arrayArgs??new string[0];
			for(int i=0;i<arrayArgsAll.Length;i++) {
				string arg=arrayArgsAll[i];
				if(string.IsNullOrWhiteSpace(arg)) {
					continue;
				}
				string text=arg.Trim();
				if(text[0]!='/' && text[0]!='-') {
					//Never repeated: it could be a secret typed in the wrong place.
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"Argument "+(i+1)+" is not a switch. Switches start with /.");
				}
				text=text.Substring(1);
				string name=text;
				string value=null;
				int indexEquals=text.IndexOf('=');
				if(indexEquals>=0) {
					name=text.Substring(0,indexEquals);
					value=Unquote(text.Substring(indexEquals+1));
				}
				name=name.Trim().ToLowerInvariant();
				if(_arraySecretSwitches.Contains(name)) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"Secrets are not accepted on the command line (/"+name
						+"). Put them in a file named by /secretsfile=, or use /prompt.");
				}
				bool isValueSwitch=_arrayValueSwitches.Contains(name);
				bool isFlagSwitch=_arrayFlagSwitches.Contains(name);
				if(!isValueSwitch && !isFlagSwitch) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"Unknown switch: /"+name+".");
				}
				if(!hashSetSeen.Add(name)) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"The switch /"+name+" is given twice.");
				}
				if(isFlagSwitch) {
					if(value!=null) {
						throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"The switch /"+name+" takes no value.");
					}
					SetFlag(dbSettingsArgs,name);
					continue;
				}
				if(string.IsNullOrWhiteSpace(value)) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"The switch /"+name+" needs a value: /"+name+"=...");
				}
				SetValue(dbSettingsArgs,name,value.Trim());
			}
			CheckCombination(dbSettingsArgs);
			return dbSettingsArgs;
		}

		private static void SetFlag(DbSettingsArgs dbSettingsArgs,string name) {
			switch(name) {
				case "silent":
					dbSettingsArgs.IsSilent=true;
					break;
				case "prompt":
					dbSettingsArgs.IsPrompt=true;
					break;
				case "check":
					dbSettingsArgs.IsCheck=true;
					break;
				case "test":
					dbSettingsArgs.IsTest=true;
					break;
				default://help, ?
					dbSettingsArgs.IsHelp=true;
					break;
			}
		}

		private static void SetValue(DbSettingsArgs dbSettingsArgs,string name,string value) {
			switch(name) {
				case "server":
					CheckValue("The server",value,isDatabase:false,isServer:true);
					dbSettingsArgs.Server=value;
					break;
				case "database":
					CheckValue("The database",value,isDatabase:true,isServer:false);
					dbSettingsArgs.Database=value;
					break;
				case "user":
					CheckValue("The user",value,isDatabase:false,isServer:false);
					dbSettingsArgs.User=value;
					break;
				case "ca":
					dbSettingsArgs.CaFile=value;
					break;
				case "pfx":
					dbSettingsArgs.PfxFile=value;
					break;
				case "thumbprint":
					string thumbprint=DbConnectionConfig.NormalizeThumbprint(value);
					if(thumbprint.Length!=40) {
						throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"The thumbprint must be 40 hexadecimal digits (SHA-1).");
					}
					dbSettingsArgs.Thumbprint=thumbprint;
					break;
				case "auth":
					if(value.Equals("certificate",StringComparison.OrdinalIgnoreCase) || value.Equals("cert",StringComparison.OrdinalIgnoreCase)) {
						dbSettingsArgs.Auth=DbSettingsAuth.Certificate;
					}
					else if(value.Equals("password",StringComparison.OrdinalIgnoreCase)) {
						dbSettingsArgs.Auth=DbSettingsAuth.Password;
					}
					else {
						throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"/auth must be certificate or password.");
					}
					break;
				case "secretsfile":
					dbSettingsArgs.SecretsFile=value;
					break;
				case "log":
					dbSettingsArgs.LogFile=value;
					break;
			}
		}

		private static void CheckCombination(DbSettingsArgs dbSettingsArgs) {
			if(dbSettingsArgs.PfxFile!=null && dbSettingsArgs.Thumbprint!=null) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"Give /pfx or /thumbprint, not both.");
			}
			if((dbSettingsArgs.PfxFile!=null || dbSettingsArgs.Thumbprint!=null)) {
				if(dbSettingsArgs.Auth==DbSettingsAuth.Password) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"/auth=password does not go with /pfx or /thumbprint.");
				}
				dbSettingsArgs.Auth=DbSettingsAuth.Certificate;
			}
			if(dbSettingsArgs.IsCheck && (dbSettingsArgs.HasChanges || dbSettingsArgs.IsSilent || dbSettingsArgs.IsPrompt || dbSettingsArgs.IsTest
				|| dbSettingsArgs.SecretsFile!=null))
			{
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"/check changes nothing and takes only /log.");
			}
			if(dbSettingsArgs.IsTest && !dbSettingsArgs.IsSilent) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_ARGS,"/test goes with /silent (the window has its own Test connection button).");
			}
		}

		///<summary>Returns when value may go into the connection settings, as the installer checks it; throws DbSettingsException otherwise.
		///</summary>
		public static void CheckValue(string caption,string value,bool isDatabase,bool isServer) {
			if(string.IsNullOrEmpty(value)) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,caption+" is empty.");
			}
			foreach(char c in value) {
				if(c<32 || c==';' || c==' ' || c=='"' || (isServer && c==',')) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,caption+" may not contain spaces, semicolons, quotes or control characters"
						+(isServer ? " (or a list of servers)." : "."));
				}
				if(isDatabase && !(char.IsLetterOrDigit(c) && c<128 || c=='_' || c=='$' || c=='-')) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,caption+" may contain only letters, digits, _, $ and -.");
				}
			}
		}

		///<summary>The value without one pair of surrounding double quotes.</summary>
		private static string Unquote(string value) {
			if(value.Length>=2 && value[0]=='"' && value[value.Length-1]=='"') {
				return value.Substring(1,value.Length-2);
			}
			return value;
		}
	}
}
