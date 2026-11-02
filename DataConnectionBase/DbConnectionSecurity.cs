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
	///SslMode=VerifyFull, SslCa (the office CA certificate file, this computer's own once one is configured) and a client credential: a
	///certificate from the LocalMachine store by thumbprint, or a password. Every MySqlConnection a shipped binary creates comes from
	///NewMySqlConnection, so the refusal happens before any network connection is attempted. DNS is never asked to decide.
	///Two notations for a server have their own loopback tests: IsLoopbackConnectorServer reads a connection string's Server value exactly as
	///MySqlConnector does; IsLoopbackServer reads the program's own notation (ComputerName, "host:port", "[v6]:port").</summary>
	public static class DbConnectionSecurity {
		///<summary>Connection strings that passed the check. A query creates a new DataConnection each time, so the check of an unchanged string
		///is one look-up. Never holds a refused string. Emptied when this computer's office CA changes (ClearPassed).</summary>
		private static readonly HashSet<string> _hashSetPassed=new HashSet<string>(StringComparer.Ordinal);
		private static readonly object _lockPassed=new object();
		///<summary>The cache is emptied when it grows past this (connection strings change only when the database is switched).</summary>
		private const int MAX_PASSED=256;

		#region Hosts of a connection string (MySqlConnector's reading)
		///<summary>True when entry, one comma-separated entry of a connection string's Server value exactly as MySqlConnector receives it (no
		///trimming, no port or bracket removal), is this computer: "localhost" in any case with nothing around it, or a string that
		///IPAddress.TryParse reads as it is and IPAddress.IsLoopback accepts (after MapToIPv4 for an IPv4-mapped address). MySqlConnector hands
		///every entry unchanged to Dns.GetHostAddresses, which answers by itself only for what IPAddress.TryParse reads and sends every other
		///entry to the system's name resolution. So "localhost:3306", "127.0.0.1:3306", "[127.0.0.1]" and " localhost" are names for the
		///network, while "[::1]" (and "[::1]:3306", which TryParse reads as ::1) is this computer. Never resolves a name.</summary>
		public static bool IsLoopbackConnectorHost(string entry) {
			if(string.IsNullOrEmpty(entry)) {
				return false;
			}
			if(string.Equals(entry,"localhost",StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
			IPAddress ipAddress;
			if(!IPAddress.TryParse(entry,out ipAddress)) {
				return false;
			}
			if(ipAddress.AddressFamily==AddressFamily.InterNetworkV6 && ipAddress.IsIPv4MappedToIPv6) {
				ipAddress=ipAddress.MapToIPv4();
			}
			return IPAddress.IsLoopback(ipAddress);
		}

		///<summary>True when every entry of server, a connection string's Server value as MySqlConnectionStringBuilder.Server gives it, is this
		///computer (IsLoopbackConnectorHost). The value is split at commas only, as MySqlConnector splits it; an empty entry in a list counts as
		///the network. An empty value names no host on the network (a connection object made before a database is chosen) and counts as this
		///computer.</summary>
		public static bool IsLoopbackConnectorServer(string server) {
			if(string.IsNullOrEmpty(server)) {
				return true;
			}
			return server.Split(',').All(x => IsLoopbackConnectorHost(x));
		}
		#endregion Hosts of a connection string (MySqlConnector's reading)

		#region Hosts in the program's notation
		///<summary>True when host (one host name or address, without a port, in the program's notation) is this computer: "localhost" in any case,
		///or an address that IPAddress.TryParse reads and IPAddress.IsLoopback accepts (127.0.0.0/8, ::1, an IPv4-mapped loopback). Square
		///brackets around an address are allowed, as the program's notation has them. Never resolves a name: every other name is the network,
		///even one that resolves to this computer. For a connection string's Server value use IsLoopbackConnectorServer.</summary>
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

		///<summary>The hosts of a server in the program's notation, or a comma-separated list of them, each without a port (SplitServer), empty
		///entries left out.</summary>
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

		///<summary>Turns a server in the program's notation (SplitServer), or a comma-separated list of them, into what MySqlConnector takes:
		///hosts, the Server value (the hosts without ports or brackets, comma-separated, empty entries left out), and the one port ("" when no
		///entry names one). Returns false when the entries name different ports: MySqlConnector has one Port for all hosts.</summary>
		public static bool SplitServerList(string server,out string hosts,out string port) {
			List<string> listHosts=new List<string>();
			port="";
			foreach(string entry in (server??"").Split(',')) {
				SplitServer(entry,out string host,out string portEntry);
				if(host=="") {
					continue;
				}
				listHosts.Add(host);
				if(portEntry=="") {
					continue;
				}
				if(port!="" && port!=portEntry) {
					hosts=string.Join(",",listHosts);
					return false;
				}
				port=portEntry;
			}
			hosts=string.Join(",",listHosts);
			return true;
		}

		///<summary>True when every host of server, in the program's notation (ComputerName, "host:port", "[v6]:port", a preference, the command
		///line), is a loopback host (IsLoopbackHost). An empty server names no host on the network (a connection object made before a database is
		///chosen) and counts as this computer. A connection string's Server value is read differently: IsLoopbackConnectorServer.</summary>
		public static bool IsLoopbackServer(string server) {
			List<string> listHosts=GetHosts(server);
			return listHosts.All(x => IsLoopbackHost(x));
		}
		#endregion Hosts in the program's notation

		#region Paths
		///<summary>True when the two paths name the same file (full paths, case-insensitive, as Windows compares them). False when either is
		///empty.</summary>
		public static bool IsSamePath(string path1,string path2) {
			if(string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2)) {
				return false;
			}
			return string.Equals(GetFullPathQuietly(path1),GetFullPathQuietly(path2),StringComparison.OrdinalIgnoreCase);
		}

		private static string GetFullPathQuietly(string path) {
			path=path.Trim();
			try {
				return Path.GetFullPath(path).TrimEnd('\\');
			}
			catch(Exception ex) when(ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException
				|| ex is System.Security.SecurityException)
			{
				return path;
			}
		}
		#endregion Paths

		#region The rule
		///<summary>What a connection to server, a connection string's Server value as MySqlConnector reads it (IsLoopbackConnectorServer), lacks
		///under the rule, in the order of the refusal message. Empty when the connection may be made: the server is this computer, or every
		///requirement is met. officeCaPath is this computer's office CA (DataConnection.OfficeCaPath); when it is set, SslCa must be that file.
		///Never contains a secret.</summary>
		public static List<string> GetMissing(string server,MySqlConnectionProtocol protocol,MySqlSslMode sslMode,string sslCa,
			MySqlCertificateStoreLocation certificateStoreLocation,string certificateThumbprint,bool hasPassword,
			MySqlServerRedirectionMode serverRedirectionMode=MySqlServerRedirectionMode.Disabled,string officeCaPath="")
		{
			List<string> listMissing=new List<string>();
			if(IsLoopbackConnectorServer(server)) {
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
			else if(!string.IsNullOrWhiteSpace(officeCaPath) && !IsSamePath(sslCa,officeCaPath)) {
				//A CA named anywhere else (a database preference, a connection string) must never become what this computer trusts (Q5).
				listMissing.Add("this computer's office CA certificate file (SslCa="+officeCaPath+", now "+sslCa+")");
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

		///<summary>What the connection string lacks under the rule (GetMissing), with this computer's office CA (DataConnection.OfficeCaPath).
		///Throws ArgumentException when MySqlConnector cannot read the string.</summary>
		public static List<string> GetMissing(string connectionString) {
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString??"");
			return GetMissing(builder);
		}

		private static List<string> GetMissing(MySqlConnectionStringBuilder builder) {
			return GetMissing(builder.Server,builder.ConnectionProtocol,builder.SslMode,builder.SslCa,builder.CertificateStoreLocation,
				builder.CertificateThumbprint,!string.IsNullOrEmpty(builder.Password),builder.ServerRedirectionMode,DataConnection.OfficeCaPath);
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
			List<string> listMissing=GetMissing(builder);
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

		///<summary>Forgets every connection string that passed, so each is checked again. DataConnection.OfficeCaPath calls it when this
		///computer's office CA changes.</summary>
		public static void ClearPassed() {
			lock(_lockPassed) {
				_hashSetPassed.Clear();
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

		///<summary>The message of a refusal by the rule. server is a connection string's Server value or a server in the program's notation; its
		///entries are named as they are. No secret is ever part of it.</summary>
		public static string GetRefusalMessage(string server,List<string> listMissing) {
			string hosts=GetServerDisplay(server);
			string message="Open PMS - Dental did not connect to the database server "+hosts+": a connection over the network must be encrypted and "
				+"the server verified, and this computer's connection settings are missing: "+string.Join("; ",listMissing)+". ";
			if(IsLoopbackServer(server) && !IsLoopbackConnectorServer(server)) {
				//"localhost:3306" or "[127.0.0.1]" in a connection string: the program's notation, but not what MySqlConnector reads as this computer.
				message+="MySqlConnector takes each entry of Server as a host name exactly as it is, so a port or square brackets in it make a name on "
					+"the network; give the port with Port=. ";
			}
			return message+ToolSentence();
		}

		///<summary>The entries of server, trimmed, joined by ", ", empty ones left out.</summary>
		public static string GetServerDisplay(string server) {
			return string.Join(", ",(server??"").Split(',').Select(x => x.Trim()).Where(x => x!=""));
		}

		///<summary>The refusal of a connection string that names a server over the network (owner decision Q2: such a server's credential is the
		///client certificate or the password stored with DPAPI, never one in a connection string). "" when every host of its Server value is this
		///computer (IsLoopbackConnectorServer). A string MySqlConnector cannot read is refused too; its own parse message is left out, because it
		///could quote part of the string.</summary>
		public static string GetConnectionStringProblem(string connectionString) {
			if(string.IsNullOrWhiteSpace(connectionString)) {
				return "";
			}
			MySqlConnectionStringBuilder builder;
			try {
				builder=new MySqlConnectionStringBuilder(connectionString);
			}
			catch(Exception ex) when(ex is ArgumentException || ex is FormatException || ex is InvalidOperationException) {
				return "The connection string (ConnectionString in "+ProgramNames.ConfigFileName+") cannot be read as a MySqlConnector connection "
					+"string. "+ToolSentence();
			}
			if(IsLoopbackConnectorServer(builder.Server)) {
				return "";
			}
			return "The connection string (ConnectionString in "+ProgramNames.ConfigFileName+") names the database server "
				+GetServerDisplay(builder.Server)+" over the network. Connection strings are for a database on this computer only: a server over the "
				+"network is set up with the office CA certificate and the client certificate or a database password stored on this computer. "
				+ToolSentence();
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
