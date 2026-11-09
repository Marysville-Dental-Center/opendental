// TLS to the local development and test database server (O-12, docs/port-notes/DB-TLS.md). For developers and tests only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MySqlConnector;

namespace DataConnectionBase {
	///<summary>TLS to the local development and test database server, MariaDB on 127.0.0.1:3306, for the development set-up, the test helpers and
	///the dev tools only (O-12 of docs/specs/net10-plan.md, docs/port-notes/DB-TLS.md). The program never calls this class (a test checks it),
	///and the connection rule of docs/specs/db-connection-security.md does not change: a connection to this computer stays exempt, and the
	///strings this class makes are what the program's own loopback strings already are with a CA (DataConnection.BuildSimpleConnectionString,
	///SslMode=VerifyCA with SslCa).
	///tools/Enable-LocalDbTls.ps1, run once by the owner as administrator, turns on TLS in that server and writes its test CA certificate (the
	///public part only) to GetDefaultCaPath(); OPMS_DB_TLS_CA names another file. While the CA is there, Apply, GetSslCa and GetClientArguments
	///give connections to the local test server that are encrypted and verify the server against it. While it is absent they give what callers
	///did before (no TLS options), so nothing breaks before the script has run. OPMS_DB_REQUIRE_TLS=1, which the gates set, makes a missing CA
	///an error instead, so a run that must be encrypted can never fall back to an unencrypted connection.
	///Only the local test server gets the CA: this computer (localhost, a loopback address) on port 3306, or on a port that OPMS_DB_TLS_RELAY_PORTS
	///names (a test's TCP relay to it). Any other server or port, such as the local end of an SSH forward to another server, is left as the
	///caller has it, because the test CA would not verify that server.</summary>
	public static class DevDbTls {
		///<summary>1 (or true, yes): a missing CA is an error. 0, false, no or unset: connect as before while there is no CA.</summary>
		public const string REQUIRE_TLS_VARIABLE="OPMS_DB_REQUIRE_TLS";
		///<summary>The CA certificate file (PEM) of the local test server, instead of GetDefaultCaPath(). The file must exist.</summary>
		public const string CA_VARIABLE="OPMS_DB_TLS_CA";
		///<summary>Comma-separated local ports that relay to 127.0.0.1:3306 (a test's TCP relay), which count as the local test server.</summary>
		public const string RELAY_PORTS_VARIABLE="OPMS_DB_TLS_RELAY_PORTS";
		///<summary>The port of the local test server.</summary>
		public const int PORT=3306;
		///<summary>The script that turns on TLS in the local MariaDB service and writes the CA certificate.</summary>
		public const string ENABLE_SCRIPT=@"tools\Enable-LocalDbTls.ps1";

		///<summary>Where tools/Enable-LocalDbTls.ps1 writes the CA certificate: %LOCALAPPDATA%\OpenPmsDental\DevDbTls\ca.pem of the account that
		///runs the dev tools and tests, shared by every worktree of that account. Never in the repository.</summary>
		public static string GetDefaultCaPath() {
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenPmsDental","DevDbTls","ca.pem");
		}

		///<summary>True when OPMS_DB_REQUIRE_TLS asks for TLS (ParseRequireTls).</summary>
		public static bool IsTlsRequired() {
			return ParseRequireTls(Environment.GetEnvironmentVariable(REQUIRE_TLS_VARIABLE));
		}

		///<summary>1, true or yes (any case, spaces trimmed): true. Unset, empty, 0, false or no: false. Throws ArgumentException for anything else,
		///so a typing error in a gate never turns the requirement off.</summary>
		public static bool ParseRequireTls(string value) {
			string valueTrimmed=(value??"").Trim().ToLowerInvariant();
			switch(valueTrimmed) {
				case "":
				case "0":
				case "false":
				case "no":
					return false;
				case "1":
				case "true":
				case "yes":
					return true;
				default:
					throw new ArgumentException(REQUIRE_TLS_VARIABLE+" is '"+value+"'; use 1 to require TLS to the local test database, or 0.");
			}
		}

		///<summary>The local test server's CA certificate file, or "" when there is none: the file OPMS_DB_TLS_CA names, else GetDefaultCaPath()
		///when it exists. Throws ApplicationException when OPMS_DB_TLS_CA names a file that does not exist.</summary>
		public static string GetCaPath() {
			return FindCaPath(Environment.GetEnvironmentVariable(CA_VARIABLE),GetDefaultCaPath());
		}

		///<summary>GetCaPath with the variable's value and the default path given (for the tests).</summary>
		public static string FindCaPath(string caVariable,string defaultPath) {
			if(!string.IsNullOrWhiteSpace(caVariable)) {
				string path=caVariable.Trim().Trim('"');
				if(!File.Exists(path)) {
					throw new ApplicationException(CA_VARIABLE+" names the CA certificate file "+path+", which does not exist.");
				}
				return Path.GetFullPath(path);
			}
			if(!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath)) {
				return defaultPath;
			}
			return "";
		}

		///<summary>True when host (one host name or address, without a port) on port is the local test server: this computer
		///(DbConnectionSecurity.IsLoopbackHost: localhost, a loopback address) on port 3306 or a port of OPMS_DB_TLS_RELAY_PORTS. Never asks DNS.
		///</summary>
		public static bool IsLocalTestServer(string host,int port) {
			return IsLocalTestServer(host,port,Environment.GetEnvironmentVariable(RELAY_PORTS_VARIABLE));
		}

		///<summary>IsLocalTestServer with the relay ports given (for the tests).</summary>
		public static bool IsLocalTestServer(string host,int port,string relayPorts) {
			if(string.IsNullOrWhiteSpace(host) || !DbConnectionSecurity.IsLoopbackHost(host.Trim())) {
				return false;
			}
			if(port==PORT) {
				return true;
			}
			return (relayPorts??"").Split(',').Any(x => x.Trim()==port.ToString(CultureInfo.InvariantCulture));
		}

		///<summary>True when server, in the program's notation (ComputerName, DataConnection.SetDb's server: "host", "host:port", "[v6]:port"; 3306
		///when there is no port), is the local test server. A list of servers never is.</summary>
		public static bool IsLocalTestServer(string server) {
			if(string.IsNullOrWhiteSpace(server) || server.Contains(",")) {
				return false;
			}
			DbConnectionSecurity.SplitServer(server.Trim(),out string host,out string port);
			int portNum=PORT;
			if(port!="" && !int.TryParse(port,NumberStyles.None,CultureInfo.InvariantCulture,out portNum)) {
				return false;
			}
			return IsLocalTestServer(host,portNum);
		}

		///<summary>The sslCa argument for DataConnection.SetDb, SetDbT and the DataConnection constructor, for server in the program's notation:
		///for the local test server the CA file, so that BuildSimpleConnectionString writes SslMode=VerifyCA with it; "" while there is no CA
		///(SslMode=none, as before) and for any other server. Throws ApplicationException when TLS is required (OPMS_DB_REQUIRE_TLS), server is
		///the local test server and there is no CA.</summary>
		public static string GetSslCa(string server) {
			if(!IsLocalTestServer(server)) {
				return "";
			}
			return GetSslCa(GetCaPath(),IsTlsRequired());
		}

		///<summary>The CA for the local test server with the CA path and the requirement given (for the tests).</summary>
		public static string GetSslCa(string caPath,bool isTlsRequired) {
			if(!string.IsNullOrEmpty(caPath)) {
				return caPath;
			}
			if(isTlsRequired) {
				throw new ApplicationException(GetMissingMessage());
			}
			return "";
		}

		///<summary>A MySqlConnector connection string: for the local test server made to connect with TLS and verify the server against the CA,
		///SslMode=VerifyCA and SslCa=the CA file, replacing any SslMode or SslCa it had. Unchanged while there is no CA, and for any other server
		///or port. Throws ApplicationException when TLS is required, the string is for the local test server and there is no CA.</summary>
		public static string Apply(string connectionString) {
			return Apply(connectionString,GetCaPath(),IsTlsRequired(),Environment.GetEnvironmentVariable(RELAY_PORTS_VARIABLE));
		}

		///<summary>Apply with the CA path, the requirement and the relay ports given (for the tests).</summary>
		public static string Apply(string connectionString,string caPath,bool isTlsRequired,string relayPorts="") {
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString);
			//One host only: MySqlConnector's Server value is a comma-separated list, and a list never counts as the local test server.
			if(builder.Server.Contains(",") || !IsLocalTestServer(builder.Server,(int)builder.Port,relayPorts)) {
				return connectionString;
			}
			if(string.IsNullOrEmpty(caPath)) {
				if(isTlsRequired) {
					throw new ApplicationException(GetMissingMessage());
				}
				return connectionString;
			}
			builder.SslMode=MySqlSslMode.VerifyCA;
			builder.SslCa=caPath;
			return builder.ConnectionString;
		}

		///<summary>The TLS options for MariaDB's command-line programs (mariadb.exe, mariadb-dump.exe) connecting to host on port: for the local test
		///server --ssl-ca with the CA file and --ssl-verify-server-cert; none while there is no CA, and for any other server or port. Throws
		///ApplicationException when TLS is required, it is the local test server and there is no CA. The caller quotes the values.</summary>
		public static List<string> GetClientArguments(string host,int port) {
			if(!IsLocalTestServer(host,port)) {
				return new List<string>();
			}
			return GetClientArguments(GetCaPath(),IsTlsRequired());
		}

		///<summary>The command-line TLS options for the local test server with the CA path and the requirement given (for the tests).</summary>
		public static List<string> GetClientArguments(string caPath,bool isTlsRequired) {
			List<string> listArguments=new List<string>();
			if(string.IsNullOrEmpty(caPath)) {
				if(isTlsRequired) {
					throw new ApplicationException(GetMissingMessage());
				}
				return listArguments;
			}
			listArguments.Add("--ssl-ca="+caPath);
			listArguments.Add("--ssl-verify-server-cert");
			return listArguments;
		}

		///<summary>Why a connection to the local test server that must be encrypted is refused when there is no CA.</summary>
		public static string GetMissingMessage() {
			return "The local test database must be reached with TLS ("+REQUIRE_TLS_VARIABLE+"=1), but its CA certificate is not at "+GetDefaultCaPath()
				+" and "+CA_VARIABLE+" names no other file. The owner turns on TLS once with "+ENABLE_SCRIPT+" (docs/port-notes/DB-TLS.md).";
		}
	}
}
