// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using MySqlConnector;

namespace SyntheticData.Infrastructure {
	///<summary>TLS to the local test database server (O-12, docs/port-notes/DB-TLS.md), by the rules of DataConnectionBase.DevDbTls, which this
	///tool does not reference (it depends on nothing of the program): the CA certificate that tools/Enable-LocalDbTls.ps1 writes to
	///%LOCALAPPDATA%\OpenPmsDental\DevDbTls\ca.pem (or the file OPMS_DB_TLS_CA names) verifies the server (SslMode=VerifyCA) on localhost,
	///127.0.0.1 or ::1, port 3306 or a port of OPMS_DB_TLS_RELAY_PORTS (a test's TCP relay to it). Any other port (the local end of an SSH forward
	///to the office's server, runbook step 0.0) keeps SslMode=None, as before: the test CA would not verify that server. Without the CA the
	///local test server is reached as before, unless OPMS_DB_REQUIRE_TLS=1, which makes that an error. tests/DbConnectionSecurityTests checks
	///that this class and DevDbTls name the same variables and file.</summary>
	public static class LocalTls {
		public const string REQUIRE_TLS_VARIABLE="OPMS_DB_REQUIRE_TLS";
		public const string CA_VARIABLE="OPMS_DB_TLS_CA";
		public const string RELAY_PORTS_VARIABLE="OPMS_DB_TLS_RELAY_PORTS";
		public const int PORT=3306;

		public static string GetDefaultCaPath() {
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenPmsDental","DevDbTls","ca.pem");
		}

		///<summary>1, true or yes: true; unset, empty, 0, false or no: false; anything else throws ArgumentException.</summary>
		public static bool ParseRequireTls(string value) {
			switch((value??"").Trim().ToLowerInvariant()) {
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

		///<summary>The CA file OPMS_DB_TLS_CA names (it must exist), else the default path when it exists, else "".</summary>
		public static string FindCaPath(string caVariable,string defaultPath) {
			if(!string.IsNullOrWhiteSpace(caVariable)) {
				string path=caVariable.Trim().Trim('"');
				if(!File.Exists(path)) {
					throw new RefusedException(CA_VARIABLE+" names the CA certificate file "+path+", which does not exist.");
				}
				return Path.GetFullPath(path);
			}
			if(!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath)) {
				return defaultPath;
			}
			return "";
		}

		public static bool IsLocalTestServer(string host,int port,string relayPorts) {
			if(!LocalOnly.IsLocalHost(host)) {
				return false;
			}
			if(port==PORT) {
				return true;
			}
			return (relayPorts??"").Split(',').Any(x => x.Trim()==port.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}

		///<summary>Sets the builder's TLS options for this server: VerifyCA with the CA for the local test server, else SslMode=None (as before).
		///</summary>
		public static void Apply(MySqlConnectionStringBuilder builder,string host,int port) {
			Apply(builder,host,port,FindCaPath(Environment.GetEnvironmentVariable(CA_VARIABLE),GetDefaultCaPath()),
				ParseRequireTls(Environment.GetEnvironmentVariable(REQUIRE_TLS_VARIABLE)),Environment.GetEnvironmentVariable(RELAY_PORTS_VARIABLE));
		}

		///<summary>Apply with the CA path, the requirement and the relay ports given (for the tests).</summary>
		public static void Apply(MySqlConnectionStringBuilder builder,string host,int port,string caPath,bool isTlsRequired,string relayPorts) {
			if(!IsLocalTestServer(host,port,relayPorts)) {
				builder.SslMode=MySqlSslMode.None;
				return;
			}
			if(string.IsNullOrEmpty(caPath)) {
				if(isTlsRequired) {
					throw new RefusedException("The local test database must be reached with TLS ("+REQUIRE_TLS_VARIABLE+"=1), but its CA certificate is not at "
						+GetDefaultCaPath()+" and "+CA_VARIABLE+" names no other file. The owner turns on TLS once with tools\\Enable-LocalDbTls.ps1.");
				}
				builder.SslMode=MySqlSslMode.None;
				return;
			}
			builder.SslMode=MySqlSslMode.VerifyCA;
			builder.SslCa=caPath;
		}
	}
}
