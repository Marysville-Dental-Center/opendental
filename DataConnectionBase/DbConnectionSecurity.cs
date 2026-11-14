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
	///docs/specs/db-connection-security.md). The rule is for the program's own database account: the main connection of FreeDentalConfig.xml
	///and everything that reuses its settings. A connection to the same computer (localhost, any loopback address) is exempt. Any other needs
	///SslMode=VerifyFull, SslCa (the office CA certificate file, this computer's own once one is configured) and a client credential: a
	///certificate from the LocalMachine store by thumbprint, or a password. Every MySqlConnection a shipped binary creates comes from
	///NewMySqlConnection, so the refusal happens before any network connection is attempted. DNS is never asked to decide.
	///SslMode=VerifyFull and SslCa are what the program's string says; for a server over the network MySqlConnector gets SslMode=Required without
	///SslCa and the program's own check of the server's certificate in the TLS handshake, before any login (GetDriverConnectionString,
	///OfficeCaServerVerifier; DB-SEC 3 of the spec, decided as (c)).
	///A read-only or reporting server of its own, named in the database preferences, connects as it did before (owner decision of 2026-11-02,
	///spec section 5.1): only the exact strings DataConnection.BuildPreferenceServerConnectionString built in this process pass without the rule
	///(IsPreferenceServerConnection), never a string because of what it contains.
	///Two notations for a server have their own loopback tests: IsLoopbackConnectorServer reads a connection string's Server value exactly as
	///MySqlConnector does; IsLoopbackServer reads the program's own notation (ComputerName, "host:port", "[v6]:port").</summary>
	public static class DbConnectionSecurity {
		///<summary>Connection strings that passed the check. A query creates a new DataConnection each time, so the check of an unchanged string
		///is one look-up. Never holds a refused string. Emptied when this computer's office CA changes (ClearPassed).</summary>
		private static readonly HashSet<string> _hashSetPassed=new HashSet<string>(StringComparer.Ordinal);
		private static readonly object _lockPassed=new object();
		///<summary>The cache is emptied when it grows past this (connection strings change only when the database is switched).</summary>
		private const int MAX_PASSED=256;
		///<summary>The connection strings of read-only and reporting servers of their own, exactly as DataConnection.BuildPreferenceServerConnectionString
		///built them in this process (AllowPreferenceServerConnection, internal to DataConnectionBase). Nothing else adds to it: a connection string
		///from a file, a preference, the command line or another program never gets in by its content. The main connection's strings come from
		///BuildSimpleConnectionString, which writes SslMode=VerifyFull for every server over the network, and never match one.</summary>
		private static readonly HashSet<string> _hashSetPreferenceServer=new HashSet<string>(StringComparer.Ordinal);
		private static readonly object _lockPreferenceServer=new object();
		///<summary>Emptied when it grows past this; such strings change only when the preferences change.</summary>
		private const int MAX_PREFERENCE_SERVER=256;

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
				//A CA named anywhere else (a connection string, a caller's own CA) must never become what the program's own account trusts (Q5).
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

		///<summary>Throws DbConnectionRefusedException when the connection string fails the rule. Makes no connection. A string that
		///DataConnection.BuildPreferenceServerConnectionString built in this process (a read-only or reporting server of its own) passes as it did
		///before this rule (IsPreferenceServerConnection).</summary>
		public static void CheckConnectionString(string connectionString) {
			connectionString=connectionString??"";
			lock(_lockPassed) {
				if(_hashSetPassed.Contains(connectionString)) {
					return;
				}
			}
			if(IsPreferenceServerConnection(connectionString)) {
				return;
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
		///refused connection never reaches the network, then creates the connection the driver gets for it (CreateConnection: for a server over
		///the network with a CA, the program verifies the server itself in the TLS handshake). Throws DbConnectionRefusedException.</summary>
		public static MySqlConnection NewMySqlConnection(string connectionString) {
			CheckConnectionString(connectionString);
			return CreateConnection(connectionString);
		}

		///<summary>NewMySqlConnection for a connection of its own, outside the pool (Pooling=false), as DataConnectionCancelable keeps for a user
		///query that can be cancelled. The string is checked as it is, then only Pooling is changed, so the connection of a read-only or reporting
		///server of its own keeps its way through (IsPreferenceServerConnection) and every other connection is checked by the rule.</summary>
		public static MySqlConnection NewMySqlConnectionWithoutPooling(string connectionString) {
			CheckConnectionString(connectionString);
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString??"");
			builder.Pooling=false;
			return CreateConnection(builder.ConnectionString);
		}
		#endregion The rule

		#region The server's certificate (DB-SEC 3)
		///<summary>The driver strings of GetDriverConnectionString, by the program's string. Emptied when it grows past MAX_PASSED.</summary>
		private static readonly Dictionary<string,DriverConnection> _dictDriverConnections=new Dictionary<string,DriverConnection>(StringComparer.Ordinal);
		private static readonly object _lockDriverConnections=new object();
		///<summary>Which check each driver string that the program verifies itself was first given: "VerifyFull|<CA path>" or "VerifyCA|<CA path>".
		///MySqlConnector pools sessions by connection string, and a pooled session was verified once, by the check of the connection that made it.
		///Two program strings that differ only in SslMode or SslCa give the same driver string, so the second check gets Pooling=false instead of
		///sharing the first one's pool. Never emptied (one entry per server, database and account); past MAX_DRIVER_POOLS a new driver string gets
		///Pooling=false.</summary>
		private static readonly Dictionary<string,string> _dictPoolChecks=new Dictionary<string,string>(StringComparer.Ordinal);
		private const int MAX_DRIVER_POOLS=1024;
		///<summary>The program's string of each connection CreateConnection made with a driver string of its own, for GetConnectionString.</summary>
		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MySqlConnection,string> _tableProgramStrings
			=new System.Runtime.CompilerServices.ConditionalWeakTable<MySqlConnection,string>();

		///<summary>What GetDriverConnectionString decided for a program string: the driver string, and the check when the program verifies the
		///server itself (CaPath "" otherwise).</summary>
		private class DriverConnection {
			public string DriverString;
			public string CaPath="";
			public string Server="";
			public bool IsHostNameChecked;
		}

		///<summary>The connection string MySqlConnector gets for connectionString, the program's string (checked by the rule, or a read-only or
		///reporting server of its own). verifier is null when the string goes to the driver as it is: a server on this computer
		///(IsLoopbackConnectorServer, exempt from the rule, as before), or a string without SslCa or without SslMode VerifyCA or VerifyFull (a
		///read-only or reporting server without a CA preference, SslMode=none, as before).
		///For any other string, a server over the network with a CA (the program's own account under the rule: VerifyFull with the office CA; a
		///read-only or reporting server with its CA preference: VerifyCA), the driver never gets SslCa: MySqlConnector 2.6.2 with SslCa logs in
		///before it decides on the certificate and accepts a server that proves the account's stored hash with any certificate, and it ignores the
		///connection's own certificate callback while SslCa is set (docs/port-notes/S2-2.md section 3.6). The driver string has no SslCa and
		///SslMode=Required (SkipCertificateRevocationCheck, which the driver allows only with VerifyFull, is left out), and verifier is the check
		///the connection must use as its RemoteCertificateValidationCallback: the chain to the CA of SslCa, under VerifyFull also the host name
		///and revocation (OfficeCaServerVerifier). The driver passes that callback to the TLS handshake, which fails when it returns false, so
		///nothing of the login is sent to a server it refuses, and the session never carries the policy errors that MySqlConnector's stored-hash
		///check (ValidateFingerprint) needs. Everything else of the string (the client certificate from the store, the password, the timeouts)
		///goes to the driver unchanged. A driver string whose check differs from the one its pool was made with gets Pooling=false (_dictPoolChecks).
		///Throws ArgumentException when MySqlConnector cannot read the string.</summary>
		public static string GetDriverConnectionString(string connectionString,out OfficeCaServerVerifier verifier) {
			connectionString=connectionString??"";
			DriverConnection driverConnection;
			lock(_lockDriverConnections) {
				_dictDriverConnections.TryGetValue(connectionString,out driverConnection);
			}
			if(driverConnection==null) {
				driverConnection=GetDriverConnection(connectionString);
				lock(_lockDriverConnections) {
					if(_dictDriverConnections.Count>=MAX_PASSED) {
						_dictDriverConnections.Clear();
					}
					_dictDriverConnections[connectionString]=driverConnection;
				}
			}
			verifier=null;
			if(driverConnection.CaPath!="") {
				verifier=new OfficeCaServerVerifier(driverConnection.CaPath,driverConnection.Server,driverConnection.IsHostNameChecked);
			}
			return driverConnection.DriverString;
		}

		private static DriverConnection GetDriverConnection(string connectionString) {
			DriverConnection driverConnection=new DriverConnection();
			driverConnection.DriverString=connectionString;
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString);
			if(IsLoopbackConnectorServer(builder.Server)
				|| string.IsNullOrWhiteSpace(builder.SslCa)
				|| (builder.SslMode!=MySqlSslMode.VerifyCA && builder.SslMode!=MySqlSslMode.VerifyFull))
			{
				return driverConnection;
			}
			driverConnection.CaPath=builder.SslCa;
			driverConnection.Server=builder.Server;
			driverConnection.IsHostNameChecked=builder.SslMode==MySqlSslMode.VerifyFull;
			builder.Remove("SslCa");
			builder.Remove("SkipCertificateRevocationCheck");
			builder.SslMode=MySqlSslMode.Required;
			if(!string.IsNullOrEmpty(builder.SslCa) || builder.SkipCertificateRevocationCheck) {
				throw new ArgumentException("The connection string's CA could not be taken off for the driver.");
			}
			string check=(driverConnection.IsHostNameChecked ? "VerifyFull|" : "VerifyCA|")+GetFullPathQuietly(driverConnection.CaPath).ToLowerInvariant();
			string driverString=builder.ConnectionString;
			lock(_lockDriverConnections) {
				string checkPool;
				if(_dictPoolChecks.TryGetValue(driverString,out checkPool)) {
					if(checkPool!=check) {
						builder.Pooling=false;
					}
				}
				else if(_dictPoolChecks.Count>=MAX_DRIVER_POOLS) {
					builder.Pooling=false;
				}
				else {
					_dictPoolChecks.Add(driverString,check);
				}
			}
			driverConnection.DriverString=builder.ConnectionString;
			return driverConnection;
		}

		///<summary>The MySqlConnection for connectionString, already checked by the rule (NewMySqlConnection): the driver string of
		///GetDriverConnectionString, with its check as the connection's RemoteCertificateValidationCallback when the program verifies the server
		///itself. MySqlConnection.Clone and CloneWith keep the callback.</summary>
		private static MySqlConnection CreateConnection(string connectionString) {
			OfficeCaServerVerifier verifier;
			string driverString=GetDriverConnectionString(connectionString,out verifier);
			MySqlConnection mySqlConnection=new MySqlConnection(driverString);
			if(verifier!=null) {
				mySqlConnection.RemoteCertificateValidationCallback=verifier.ValidateServerCertificate;
				_tableProgramStrings.Add(mySqlConnection,connectionString??"");
			}
			return mySqlConnection;
		}

		///<summary>The program's connection string of a connection that NewMySqlConnection made: the string it was given, also when the driver got
		///another one (GetDriverConnectionString). For any other connection its ConnectionString. DataConnection.GetCurrentConnectionString reads
		///this, because its string seeds other connections (child threads, ODR, reports, the Connection Lost retry) that go through the rule
		///again.</summary>
		public static string GetConnectionString(MySqlConnection mySqlConnection) {
			if(mySqlConnection==null) {
				return "";
			}
			string connectionString;
			if(_tableProgramStrings.TryGetValue(mySqlConnection,out connectionString)) {
				return connectionString;
			}
			return mySqlConnection.ConnectionString;
		}
		#endregion The server's certificate (DB-SEC 3)

		#region Read-only and reporting servers of their own
		///<summary>Lets exactly this connection string through without the rule: a read-only or reporting server of its own, named in the
		///database preferences (owner decision of 2026-11-02, docs/specs/db-connection-security.md section 5.1). Only
		///DataConnection.BuildPreferenceServerConnectionString calls it, with the string it built (the old SslMode behaviour, the server's own
		///user, password and CA). Throws DbConnectionRefusedException for a string with a client certificate of any kind, which that builder never
		///writes: this computer's certificate belongs to the program's own account.</summary>
		internal static void AllowPreferenceServerConnection(string connectionString) {
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(connectionString??"");
			if(!string.IsNullOrEmpty(builder.CertificateThumbprint) || builder.CertificateStoreLocation!=MySqlCertificateStoreLocation.None
				|| !string.IsNullOrEmpty(builder.CertificateFile) || !string.IsNullOrEmpty(builder.SslCert) || !string.IsNullOrEmpty(builder.SslKey))
			{
				throw new DbConnectionRefusedException(builder.Server,"A read-only or reporting server connection never carries a client certificate: "
					+"this computer's certificate is for the program's own database account only.");
			}
			lock(_lockPreferenceServer) {
				if(_hashSetPreferenceServer.Count>=MAX_PREFERENCE_SERVER && !_hashSetPreferenceServer.Contains(connectionString)) {
					_hashSetPreferenceServer.Clear();
				}
				_hashSetPreferenceServer.Add(connectionString);
			}
		}

		///<summary>True when connectionString is, character for character, one that DataConnection.BuildPreferenceServerConnectionString built in
		///this process for a read-only or reporting server of its own. Such a connection is made as before this rule (SslMode=None, or VerifyCA
		///with the server's own CA) and never carries this computer's client certificate or the password stored on this computer.</summary>
		public static bool IsPreferenceServerConnection(string connectionString) {
			if(string.IsNullOrEmpty(connectionString)) {
				return false;
			}
			lock(_lockPreferenceServer) {
				return _hashSetPreferenceServer.Contains(connectionString);
			}
		}
		#endregion Read-only and reporting servers of their own

		#region Reports
		///<summary>The check for RdlEngine's connection hook (RdlEngineConfig.CheckConnection): a report may only use MySqlConnector, and its
		///connection string must pass the rule (or be the connection of a read-only or reporting server of its own, IsPreferenceServerConnection).
		///provider is the report's data provider name; className the class the engine will create.</summary>
		public static void CheckReportConnection(string provider,string className,string connectionString) {
			if(className!=typeof(MySqlConnection).FullName) {
				throw new DbConnectionRefusedException("","A report may connect only through MySqlConnector, not through the data provider "
					+(provider??"")+". "+ToolSentence());
			}
			CheckConnectionString(connectionString);
		}

		///<summary>For RdlEngine's connection factory hook (RdlEngineConfig.CreateConnection), called after CheckReportConnection: the report's
		///MySqlConnector connection, made by NewMySqlConnection, so a server over the network is verified by the program before the login as every
		///other connection is (GetDriverConnectionString). Null for any other class, which CheckReportConnection has already refused.</summary>
		public static System.Data.IDbConnection NewReportConnection(string provider,string className,string connectionString) {
			if(className!=typeof(MySqlConnection).FullName) {
				return null;
			}
			return NewMySqlConnection(connectionString);
		}
		#endregion Reports

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
