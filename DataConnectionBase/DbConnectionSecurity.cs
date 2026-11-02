// The database connection rule: every connection over the network is encrypted and verifies the server; the same computer is exempt.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using CodeBase;
using MySqlConnector;

namespace DataConnectionBase {
	///<summary>The one place that decides whether a database connection may be made (owner decisions Q3 to Q5 of 2026-11-02,
	///docs/specs/db-connection-security.md). A connection to the same computer (localhost, any loopback address) is exempt. Any other needs
	///SslMode=VerifyFull, SslCa (the office CA certificate file) and a client credential: a certificate from the LocalMachine store by
	///thumbprint, or a password. Every MySqlConnection a shipped binary creates comes from NewMySqlConnection, so the refusal happens before
	///any network connection is attempted. DNS is never asked to decide.</summary>
	public static class DbConnectionSecurity {
		///<summary>Connection strings that passed the check. A query creates a new DataConnection each time, so the check of an unchanged string
		///is one look-up. Never holds a refused string.</summary>
		private static readonly HashSet<string> _hashSetPassed=new HashSet<string>(StringComparer.Ordinal);
		private static readonly object _lockPassed=new object();
		///<summary>The cache is emptied when it grows past this (connection strings change only when the database is switched).</summary>
		private const int MAX_PASSED=256;

		#region Hosts
		///<summary>True when host (one host name or address, without a port) is this computer: "localhost" in any case, or an address that
		///IPAddress.TryParse reads and IPAddress.IsLoopback accepts (127.0.0.0/8, ::1, an IPv4-mapped loopback). Square brackets around an IPv6
		///address are allowed. Never resolves a name: every other name is the network, even one that resolves to this computer.</summary>
		public static bool IsLoopbackHost(string host) {
			if(host==null) {
				return false;
			}
			host=host.Trim();
			if(host.Length>=2 && host[0]=='[' && host[host.Length-1]==']') {
				host=host.Substring(1,host.Length-2);
			}
			if(host=="") {
				return false;
			}
			if(string.Equals(host,"localhost",StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
			IPAddress ipAddress;
			if(!IPAddress.TryParse(host,out ipAddress)) {
				return false;
			}
			if(ipAddress.AddressFamily==AddressFamily.InterNetworkV6 && ipAddress.IsIPv4MappedToIPv6) {
				ipAddress=ipAddress.MapToIPv4();
			}
			return IPAddress.IsLoopback(ipAddress);
		}

		///<summary>Splits a server as the program stores it (FreeDentalConfig.xml ComputerName, a preference) into host and port: "host",
		///"host:port", "[v6]", "[v6]:port", or a bare IPv6 address (more than one colon, kept whole). port is "" when there is none. A value
		///with a comma is a list of hosts and is returned whole as host.</summary>
		public static void SplitServer(string server,out string host,out string port) {
			host=(server??"").Trim();
			port="";
			if(host.Contains(",")) {
				return;
			}
			if(host.StartsWith("[")) {
				int indexEnd=host.IndexOf(']');
				if(indexEnd<0) {
					return;
				}
				string rest=host.Substring(indexEnd+1);
				string inside=host.Substring(1,indexEnd-1);
				if(rest.StartsWith(":")) {
					port=rest.Substring(1);
				}
				else if(rest!="") {
					return;//Not a bracketed address; leave it whole.
				}
				host=inside;
				return;
			}
			int indexColon=host.IndexOf(':');
			if(indexColon<0 || indexColon!=host.LastIndexOf(':')) {
				return;//No port, or a bare IPv6 address.
			}
			port=host.Substring(indexColon+1);
			host=host.Substring(0,indexColon);
		}

		///<summary>The hosts of a MySqlConnector Server value (a comma-separated list), each without a port, empty entries left out.</summary>
		public static List<string> GetHosts(string server) {
			List<string> listHosts=new List<string>();
			foreach(string entry in (server??"").Split(',')) {
				SplitServer(entry,out string host,out string port);
				if(host!="") {
					listHosts.Add(host);
				}
			}
			return listHosts;
		}

		///<summary>True when every host of server is a loopback host (IsLoopbackHost). An empty server names no host on the network (a connection
		///object made before a database is chosen) and counts as this computer.</summary>
		public static bool IsLoopbackServer(string server) {
			List<string> listHosts=GetHosts(server);
			return listHosts.All(x => IsLoopbackHost(x));
		}
		#endregion Hosts

		#region The rule
		///<summary>What a connection to server lacks under the rule, in the order of the refusal message. Empty when the connection may be made:
		///the server is this computer, or every requirement is met. Never contains a secret.</summary>
		public static List<string> GetMissing(string server,MySqlConnectionProtocol protocol,MySqlSslMode sslMode,string sslCa,
			MySqlCertificateStoreLocation certificateStoreLocation,string certificateThumbprint,bool hasPassword,
			MySqlServerRedirectionMode serverRedirectionMode=MySqlServerRedirectionMode.Disabled)
		{
			List<string> listMissing=new List<string>();
			if(IsLoopbackServer(server)) {
				return listMissing;
			}
			if(protocol!=MySqlConnectionProtocol.Sockets) {
				listMissing.Add("a TCP connection (Protocol=Socket; "+protocol+" is allowed only on this computer)");
			}
			if(sslMode!=MySqlSslMode.VerifyFull) {
				listMissing.Add("server verification (SslMode=VerifyFull, now "+sslMode+")");
			}
			if(string.IsNullOrWhiteSpace(sslCa)) {
				listMissing.Add("the office CA certificate file (SslCa)");
			}
			else if(!File.Exists(sslCa)) {
				listMissing.Add("the office CA certificate file "+sslCa+" (the file does not exist)");
			}
			if(serverRedirectionMode!=MySqlServerRedirectionMode.Disabled) {
				listMissing.Add("no server redirection (ServerRedirectionMode=Disabled, now "+serverRedirectionMode+")");
			}
			bool hasThumbprint=!string.IsNullOrWhiteSpace(certificateThumbprint);
			if(hasThumbprint && certificateStoreLocation!=MySqlCertificateStoreLocation.LocalMachine) {
				listMissing.Add("the client certificate in the computer's certificate store (CertificateStoreLocation=LocalMachine)");
			}
			else if(!hasThumbprint && !hasPassword) {
				listMissing.Add("a client certificate (CertificateThumbprint) or a database password stored on this computer");
			}
			return listMissing;
		}

		///<summary>What the connection string lacks under the rule (GetMissing). Throws ArgumentException when MySqlConnector cannot read the
		///string.</summary>
		public static List<string> GetMissing(string connectionString) {
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString??"");
			return GetMissing(builder.Server,builder.ConnectionProtocol,builder.SslMode,builder.SslCa,builder.CertificateStoreLocation,
				builder.CertificateThumbprint,!string.IsNullOrEmpty(builder.Password),builder.ServerRedirectionMode);
		}

		///<summary>Throws DbConnectionRefusedException when the connection string fails the rule. Makes no connection.</summary>
		public static void CheckConnectionString(string connectionString) {
			connectionString=connectionString??"";
			lock(_lockPassed) {
				if(_hashSetPassed.Contains(connectionString)) {
					return;
				}
			}
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString);
			List<string> listMissing=GetMissing(builder.Server,builder.ConnectionProtocol,builder.SslMode,builder.SslCa,
				builder.CertificateStoreLocation,builder.CertificateThumbprint,!string.IsNullOrEmpty(builder.Password),builder.ServerRedirectionMode);
			if(listMissing.Count>0) {
				throw new DbConnectionRefusedException(builder.Server,listMissing);
			}
			lock(_lockPassed) {
				if(_hashSetPassed.Count>=MAX_PASSED) {
					_hashSetPassed.Clear();
				}
				_hashSetPassed.Add(connectionString);
			}
		}

		///<summary>The only way a shipped binary creates a MySqlConnection: checks the string with the rule first (CheckConnectionString), so a
		///refused connection never reaches the network. Throws DbConnectionRefusedException.</summary>
		public static MySqlConnection NewMySqlConnection(string connectionString) {
			CheckConnectionString(connectionString);
			return new MySqlConnection(connectionString);
		}

		///<summary>The check for RdlEngine's connection hook (RdlEngineConfig.CheckConnection): a report may only use MySqlConnector, and its
		///connection string must pass the rule. provider is the report's data provider name; className the class the engine will create.</summary>
		public static void CheckReportConnection(string provider,string className,string connectionString) {
			if(className!=typeof(MySqlConnection).FullName) {
				throw new DbConnectionRefusedException("","A report may connect only through MySqlConnector, not through the data provider "
					+(provider??"")+". "+ToolSentence());
			}
			CheckConnectionString(connectionString);
		}
		#endregion The rule

		#region Messages
		///<summary>The sentence every refusal ends with: what to do.</summary>
		public static string ToolSentence() {
			return "Run "+ProgramNames.DbSettingsExeFileName+" in the program folder as an administrator to set up the connection.";
		}

		///<summary>The message of a refusal by the rule. No secret is ever part of it.</summary>
		public static string GetRefusalMessage(string server,List<string> listMissing) {
			string hosts=string.Join(", ",GetHosts(server));
			return "Open PMS - Dental did not connect to the database server "+hosts+": a connection over the network must be encrypted and the "
				+"server verified, and this computer's connection settings are missing: "+string.Join("; ",listMissing)+". "+ToolSentence();
		}
		#endregion Messages
	}

	///<summary>A database connection refused before it was attempted (DbConnectionSecurity), or connection settings that cannot be used. The
	///message is written for the user and names what is missing; it never contains a secret. An ApplicationException, which the program shows
	///as it is.</summary>
	[Serializable]
	public class DbConnectionRefusedException:ApplicationException {
		///<summary>The server as the settings name it. May be empty.</summary>
		public string Server { get; private set; }
		///<summary>What the rule found missing; empty for a refusal of the settings themselves.</summary>
		public List<string> ListMissing { get; private set; }

		///<summary>A refusal by the rule.</summary>
		public DbConnectionRefusedException(string server,List<string> listMissing)
			:base(DbConnectionSecurity.GetRefusalMessage(server,listMissing))
		{
			Server=server??"";
			ListMissing=listMissing;
		}

		///<summary>A refusal of the connection settings, with its own message.</summary>
		public DbConnectionRefusedException(string server,string message):base(message) {
			Server=server??"";
			ListMissing=new List<string>();
		}

		protected DbConnectionRefusedException(System.Runtime.Serialization.SerializationInfo info,System.Runtime.Serialization.StreamingContext context)
			:base(info,context)
		{
			Server="";
			ListMissing=new List<string>();
		}
	}
}
