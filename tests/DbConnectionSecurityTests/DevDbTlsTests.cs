// O-12 (docs/port-notes/DB-TLS.md): the connection-string choices of DataConnectionBase.DevDbTls for the local test server, without a server,
// and source checks that the program never calls it and that every test and tool connection to the local server goes through it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class DevDbTlsTests {
		private const string LOCAL="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=15";
		private string _folder;
		private string _ca;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
			_ca=TestHelpers.WriteCaPem(_folder,"ca.pem");
		}

		[TestCleanup]
		public void CleanUp() {
			TestHelpers.DeleteFolder(_folder);
		}

		///<summary>Runs action with the environment variable set to value (null removes it), then puts the old value back.</summary>
		private static void WithVariable(string name,string value,Action action) {
			string valueOld=Environment.GetEnvironmentVariable(name);
			Environment.SetEnvironmentVariable(name,value);
			try {
				action();
			}
			finally {
				Environment.SetEnvironmentVariable(name,valueOld);
			}
		}

		[TestMethod]
		public void ParseRequireTls_OneTrueYes_ZeroFalseNoEmpty_AnythingElseThrows() {
			foreach(string value in new[] { "1","true","TRUE"," yes " }) {
				Assert.IsTrue(DevDbTls.ParseRequireTls(value),value);
			}
			foreach(string value in new[] { null,""," ","0","false","No" }) {
				Assert.IsFalse(DevDbTls.ParseRequireTls(value),value??"(null)");
			}
			foreach(string value in new[] { "2","on","required","1;" }) {
				ArgumentException ex=TestHelpers.AssertThrows<ArgumentException>(() => DevDbTls.ParseRequireTls(value));
				StringAssert.Contains(ex.Message,DevDbTls.REQUIRE_TLS_VARIABLE,"A typing error never turns the requirement off.");
			}
		}

		[TestMethod]
		public void FindCaPath_TheVariableWins_ItsFileMustExist_ElseTheDefaultWhenItExists() {
			string pathMissing=Path.Combine(_folder,"missing.pem");
			Assert.AreEqual(_ca,DevDbTls.FindCaPath(_ca,pathMissing));
			Assert.AreEqual(_ca,DevDbTls.FindCaPath(" \""+_ca+"\" ",pathMissing),"Quotes and spaces around the value.");
			ApplicationException ex=TestHelpers.AssertThrows<ApplicationException>(() => DevDbTls.FindCaPath(pathMissing,_ca));
			StringAssert.Contains(ex.Message,DevDbTls.CA_VARIABLE);
			StringAssert.Contains(ex.Message,pathMissing);
			Assert.AreEqual(_ca,DevDbTls.FindCaPath(null,_ca),"No variable: the default file.");
			Assert.AreEqual(_ca,DevDbTls.FindCaPath("  ",_ca));
			Assert.AreEqual("",DevDbTls.FindCaPath(null,pathMissing),"No variable, no default file: no CA.");
		}

		[TestMethod]
		public void GetDefaultCaPath_IsInTheUsersLocalAppData_NeverTheRepository() {
			string path=DevDbTls.GetDefaultCaPath();
			Assert.AreEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenPmsDental","DevDbTls","ca.pem"),path);
			string root=GetRepoRoot();
			Assert.IsFalse(path.StartsWith(root,StringComparison.OrdinalIgnoreCase),path);
		}

		[TestMethod]
		public void IsLocalTestServer_ThisComputerOnPort3306_OrARelayPort_NothingElse() {
			foreach(string host in new[] { "127.0.0.1","localhost","LOCALHOST","::1","[::1]","127.0.0.2" }) {
				Assert.IsTrue(DevDbTls.IsLocalTestServer(host,3306,""),host);
			}
			foreach(string host in new[] { "",null,"db.example.test","192.0.2.10","0.0.0.0","localhost.","127.0.0.1,localhost" }) {
				Assert.IsFalse(DevDbTls.IsLocalTestServer(host,3306,""),host??"(null)");
			}
			Assert.IsFalse(DevDbTls.IsLocalTestServer("127.0.0.1",13306,""),"The local end of an SSH forward to another server.");
			Assert.IsFalse(DevDbTls.IsLocalTestServer("127.0.0.1",3307,"13306"));
			Assert.IsTrue(DevDbTls.IsLocalTestServer("127.0.0.1",13306," 4000 ,13306"),"A test's relay to the local test server.");
			Assert.IsFalse(DevDbTls.IsLocalTestServer("db.example.test",13306,"13306"),"A relay port counts on this computer only.");
		}

		[TestMethod]
		public void IsLocalTestServer_TheProgramsNotation() {
			WithVariable(DevDbTls.RELAY_PORTS_VARIABLE,null,() => {
				foreach(string server in new[] { "127.0.0.1","localhost","127.0.0.1:3306","localhost:3306","[::1]:3306","::1"," 127.0.0.1 " }) {
					Assert.IsTrue(DevDbTls.IsLocalTestServer(server),server);
				}
				foreach(string server in new[] { "","127.0.0.1:13306","127.0.0.1:x","127.0.0.1,localhost","db.example.test","192.0.2.10:3306" }) {
					Assert.IsFalse(DevDbTls.IsLocalTestServer(server),server);
				}
			});
		}

		[TestMethod]
		public void Apply_WithTheCa_VerifyCaAndTheCa_ReplacingAnySslMode_KeepingTheRest() {
			foreach(string connectionString in new[] { LOCAL,LOCAL+";SslMode=None",LOCAL+";SslMode=Required;SslCa=C:\\other.pem",
				"Server=localhost;User ID=root;Password=;Database=dbtls_x;Pooling=false" })
			{
				MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(DevDbTls.Apply(connectionString,_ca,isTlsRequired:false));
				Assert.AreEqual(MySqlSslMode.VerifyCA,builder.SslMode,connectionString);
				Assert.AreEqual(_ca,builder.SslCa,connectionString);
				MySqlConnectionStringBuilder builderOld=new MySqlConnectionStringBuilder(connectionString);
				Assert.AreEqual(builderOld.Server,builder.Server);
				Assert.AreEqual(builderOld.Port,builder.Port);
				Assert.AreEqual(builderOld.UserID,builder.UserID);
				Assert.AreEqual(builderOld.Database,builder.Database);
				Assert.AreEqual(builderOld.Pooling,builder.Pooling);
				Assert.AreEqual(builderOld.ConnectionTimeout,builder.ConnectionTimeout);
			}
			//The same with the requirement: the CA is there.
			Assert.AreEqual(MySqlSslMode.VerifyCA,new MySqlConnectionStringBuilder(DevDbTls.Apply(LOCAL,_ca,isTlsRequired:true)).SslMode);
		}

		[TestMethod]
		public void Apply_WithoutTheCa_TheStringAsItWas_UnlessTlsIsRequired() {
			Assert.AreEqual(LOCAL,DevDbTls.Apply(LOCAL,"",isTlsRequired:false),"Before the owner's script: exactly as before.");
			Assert.AreEqual(LOCAL+";SslMode=None",DevDbTls.Apply(LOCAL+";SslMode=None",null,isTlsRequired:false));
			ApplicationException ex=TestHelpers.AssertThrows<ApplicationException>(() => DevDbTls.Apply(LOCAL,"",isTlsRequired:true));
			StringAssert.Contains(ex.Message,DevDbTls.REQUIRE_TLS_VARIABLE+"=1");
			StringAssert.Contains(ex.Message,DevDbTls.GetDefaultCaPath());
			StringAssert.Contains(ex.Message,DevDbTls.CA_VARIABLE);
			StringAssert.Contains(ex.Message,DevDbTls.ENABLE_SCRIPT);
		}

		[TestMethod]
		public void Apply_AnyOtherServerOrPort_IsLeftAsItWas_EvenWhenTlsIsRequired() {
			foreach(string connectionString in new[] { "Server=127.0.0.1;Port=13306;User ID=root;Password=",
				"Server="+TestHelpers.NETWORK_HOST+";SslMode=VerifyFull;SslCa=C:\\office-ca.pem;Password=x",
				"Server="+TestHelpers.NETWORK_ADDRESS+";Port=3306;Password=x","Server=127.0.0.1,"+TestHelpers.NETWORK_HOST+";Password=x",
				"Server=localhost:3306;Password=x" })
			{
				Assert.AreEqual(connectionString,DevDbTls.Apply(connectionString,_ca,isTlsRequired:true),"The test CA is never offered to it.");
			}
			string relay="Server=127.0.0.1;Port=13306;User ID=root;Password=";
			Assert.AreEqual(MySqlSslMode.VerifyCA,new MySqlConnectionStringBuilder(DevDbTls.Apply(relay,_ca,false,relayPorts:"13306")).SslMode,
				"A test's relay to the local test server.");
		}

		[TestMethod]
		public void GetSslCa_ForSetDb_TheCaForTheLocalTestServerOnly() {
			Assert.AreEqual(_ca,DevDbTls.GetSslCa(_ca,isTlsRequired:false));
			Assert.AreEqual(_ca,DevDbTls.GetSslCa(_ca,isTlsRequired:true));
			Assert.AreEqual("",DevDbTls.GetSslCa("",isTlsRequired:false));
			TestHelpers.AssertThrows<ApplicationException>(() => DevDbTls.GetSslCa("",isTlsRequired:true));
			WithVariable(DevDbTls.CA_VARIABLE,_ca,() => WithVariable(DevDbTls.REQUIRE_TLS_VARIABLE,"1",() => {
				Assert.AreEqual(_ca,DevDbTls.GetSslCa("127.0.0.1"));
				Assert.AreEqual(_ca,DevDbTls.GetSslCa("localhost:3306"));
				Assert.AreEqual("",DevDbTls.GetSslCa(TestHelpers.NETWORK_HOST),"Another server: as before, even when TLS is required.");
				Assert.AreEqual("",DevDbTls.GetSslCa("127.0.0.1:13306"),"An SSH forward's port.");
				Assert.AreEqual(_ca,DevDbTls.GetCaPath());
			}));
			WithVariable(DevDbTls.CA_VARIABLE,Path.Combine(_folder,"missing.pem"),() => {
				TestHelpers.AssertThrows<ApplicationException>(() => DevDbTls.GetSslCa("127.0.0.1"));
				Assert.AreEqual("",DevDbTls.GetSslCa(TestHelpers.NETWORK_HOST),"Never read for another server.");
			});
		}

		[TestMethod]
		public void SetDbWithTheCa_TheProgramsLoopbackStringIsVerifyCa_TheNetworkRuleIsUnchanged() {
			//What the test helpers' DataConnection.SetDb(...,DevDbTls.GetSslCa(server)) builds: the program's own loopback branch with a CA.
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder(
				DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,"127.0.0.1","dbtls_x","root","",DevDbTls.GetSslCa(_ca,false)));
			Assert.AreEqual(MySqlSslMode.VerifyCA,builder.SslMode);
			Assert.AreEqual(_ca,builder.SslCa);
			Assert.AreEqual(0,DbConnectionSecurity.GetMissing(builder.ConnectionString).Count,"This computer: exempt from the rule, as before.");
			//Without a CA: SslMode=none, as before.
			Assert.AreEqual(MySqlSslMode.None,new MySqlConnectionStringBuilder(
				DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,"127.0.0.1","dbtls_x","root",DevDbTls.GetSslCa("",false))).SslMode);
			//A server over the network still gets VerifyFull and needs the office CA and a credential: GetSslCa never gives it the test CA.
			MySqlConnectionStringBuilder builderNetwork=new MySqlConnectionStringBuilder(DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,
				TestHelpers.NETWORK_HOST,"opendental","opmsd","",DevDbTls.GetSslCa(TestHelpers.NETWORK_HOST)));
			Assert.AreEqual(MySqlSslMode.VerifyFull,builderNetwork.SslMode);
			Assert.AreEqual("",builderNetwork.SslCa);
			Assert.IsTrue(DbConnectionSecurity.GetMissing(builderNetwork.ConnectionString).Count>0,"The rule still refuses it.");
		}

		[TestMethod]
		public void GetClientArguments_SslCaAndVerifyServerCert_ForTheLocalTestServer() {
			CollectionAssert.AreEqual(new[] { "--ssl-ca="+_ca,"--ssl-verify-server-cert" },DevDbTls.GetClientArguments(_ca,false).ToArray());
			Assert.AreEqual(0,DevDbTls.GetClientArguments("",false).Count,"No CA: the command line as before.");
			TestHelpers.AssertThrows<ApplicationException>(() => DevDbTls.GetClientArguments("",true));
			WithVariable(DevDbTls.CA_VARIABLE,_ca,() => {
				Assert.AreEqual(2,DevDbTls.GetClientArguments("127.0.0.1",3306).Count);
				Assert.AreEqual(0,DevDbTls.GetClientArguments("127.0.0.1",13306).Count,"An SSH forward's port.");
				Assert.AreEqual(0,DevDbTls.GetClientArguments(TestHelpers.NETWORK_ADDRESS,3306).Count);
			});
		}

		#region Source checks

		///<summary>The repository root: the first folder above the test assembly that holds LICENSE and NOTICE.</summary>
		private static string GetRepoRoot() {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null
				&& !(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))))
			{
				directoryInfo=directoryInfo.Parent;
			}
			Assert.IsNotNull(directoryInfo,"Repository root not found.");
			return directoryInfo.FullName;
		}

		///<summary>The C# files under the folder, without build output and worktrees (the folder names below it decide, not the folders above).
		///</summary>
		private static List<string> GetSourceFiles(string folder) {
			return Directory.EnumerateFiles(folder,"*.cs",SearchOption.AllDirectories)
				.Where(x => !Regex.IsMatch(x.Substring(folder.Length),@"\\(bin|obj|\.git|\.claude|\.vs|packages)\\",RegexOptions.IgnoreCase))
				.ToList();
		}

		[TestMethod]
		public void TheProgramNeverCallsDevDbTls() {
			string root=GetRepoRoot();
			string[] arrayTopNotProgram={ "tests","tools","UnitTests","UnitTestsCore","NoHqTests","TestResults",".git",".claude",".vs","packages","docs" };
			List<string> listCallers=new List<string>();
			foreach(string folder in Directory.GetDirectories(root)) {
				if(arrayTopNotProgram.Contains(Path.GetFileName(folder),StringComparer.OrdinalIgnoreCase)) {
					continue;
				}
				foreach(string path in GetSourceFiles(folder)) {
					if(path.EndsWith(Path.Combine("DataConnectionBase","DevDbTls.cs"),StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					if(File.ReadAllText(path).Contains("DevDbTls")) {
						listCallers.Add(path.Substring(root.Length+1));
					}
				}
			}
			Assert.AreEqual("",string.Join(", ",listCallers),"Only developers, tests and dev tools use the local test server's TLS.");
		}

		[TestMethod]
		public void SyntheticDataAndTheScripts_UseTheSameVariablesFileAndPort() {
			string sourceLocalTls=TestHelpers.ReadRepoFile("tools","SyntheticData","Infrastructure","LocalTls.cs");
			string sourceScripts=TestHelpers.ReadRepoFile("tools","DevDbTls.ps1");
			foreach(string value in new[] { DevDbTls.REQUIRE_TLS_VARIABLE,DevDbTls.CA_VARIABLE }) {
				StringAssert.Contains(sourceLocalTls,"\""+value+"\"");
				StringAssert.Contains(sourceScripts,"$env:"+value);
			}
			StringAssert.Contains(sourceLocalTls,"\""+DevDbTls.RELAY_PORTS_VARIABLE+"\"");
			StringAssert.Contains(sourceLocalTls,"PORT="+DevDbTls.PORT+";");
			StringAssert.Contains(sourceLocalTls,"\"OpenPmsDental\",\"DevDbTls\",\"ca.pem\"");
			StringAssert.Contains(sourceScripts,"'OpenPmsDental\\DevDbTls\\ca.pem'");
			StringAssert.Contains(sourceScripts,"$Port -eq "+DevDbTls.PORT);
			StringAssert.Contains(TestHelpers.ReadRepoFile("tools","Enable-LocalDbTls.ps1"),"'OpenPmsDental\\DevDbTls'",
				"The script writes the CA where DevDbTls looks.");
		}

		///<summary>Marks the characters of C# source that are code, not a comment, a string or a character literal.</summary>
		private static bool[] GetCodeMask(string text) {
			bool[] arrayIsCode=Enumerable.Repeat(true,text.Length).ToArray();
			int i=0;
			while(i<text.Length) {
				char c=text[i];
				char next=i+1<text.Length ? text[i+1] : '\0';
				int end=-1;
				if(c=='/' && next=='/') {
					end=text.IndexOf('\n',i);
					end=end<0 ? text.Length : end;
				}
				else if(c=='/' && next=='*') {
					end=text.IndexOf("*/",i+2,StringComparison.Ordinal);
					end=end<0 ? text.Length : end+2;
				}
				else if(c=='@' && next=='"') {
					int j=i+2;
					while(j<text.Length && !(text[j]=='"' && (j+1>=text.Length || text[j+1]!='"'))) {
						j+=text[j]=='"' ? 2 : 1;
					}
					end=Math.Min(j+1,text.Length);
				}
				else if(c=='"' || (c=='$' && next=='"') || c=='\'') {
					char quote=c=='\'' ? '\'' : '"';
					int j=i+(c=='$' ? 2 : 1);
					while(j<text.Length && text[j]!=quote) {
						j+=text[j]=='\\' ? 2 : 1;
					}
					end=Math.Min(j+1,text.Length);
				}
				if(end<0) {
					i++;
					continue;
				}
				for(int k=i;k<end;k++) {
					arrayIsCode[k]=false;
				}
				i=end;
			}
			return arrayIsCode;
		}

		///<summary>The arguments of the call whose opening parenthesis is at indexOpen, split at the top-level commas.</summary>
		private static List<string> GetArguments(string text,bool[] arrayIsCode,int indexOpen) {
			List<string> listArguments=new List<string>();
			int depth=0;
			int start=indexOpen+1;
			for(int k=indexOpen;k<text.Length;k++) {
				if(!arrayIsCode[k]) {
					continue;
				}
				if("([{".IndexOf(text[k])>=0) {
					depth++;
				}
				else if(")]}".IndexOf(text[k])>=0) {
					depth--;
					if(depth==0) {
						listArguments.Add(text.Substring(start,k-start));
						return listArguments;
					}
				}
				else if(text[k]==',' && depth==1) {
					listArguments.Add(text.Substring(start,k-start));
					start=k+1;
				}
			}
			return listArguments;
		}

		///<summary>Calls of tests and tools that connect without DevDbTls, on purpose: each opens nothing, or is refused before it connects, or sets
		///its TLS options another way. File (from the repository root) and a piece of the call's text.</summary>
		private static readonly string[][] _arrayAllowed={
			//SyntheticData sets the builder's TLS with its own LocalTls (it does not reference DataConnectionBase).
			new[] { @"tools\SyntheticData\Infrastructure\Db.cs","builder.ConnectionString" },
			//The TLS tests themselves set SslMode and SslCa explicitly (VerifyFull, another CA, and SslMode=None on purpose).
			new[] { @"tests\DbConnectionSecurityTests\DevDbTlsServerTests.cs","connectionString" },
			new[] { @"tests\DbConnectionSecurityTests\DevDbTlsServerTests.cs","builder.ConnectionString" },
			new[] { @"tests\DbConnectionSecurityTests\DevDbTlsServerTests.cs","ROOT+\";SslMode=None\"" },
			//A server over the network: refused by the rule before any connection.
			new[] { @"tests\DbConnectionSecurityTests\PreferenceServerTests.cs","PREFERENCE_HOST,\"opendental\",\"ro\"" },
			new[] { @"tests\DbConnectionSecurityTests\PreferenceServerTests.cs","TestHelpers.NETWORK_HOST,\"opendental\",\"opmsd\"" },
			//CentralConnectionBase entries for a server over the network: refused by the rule before any connection, or only serialized.
			new[] { @"tests\DbConnectionSecurityTests\PreferenceServerTests.cs","ServerName=PREFERENCE_HOST" },
			//The program's loopback connection without a CA, on purpose (the test returns before it when the server requires TLS), and the next
			//test's connection with the CA, which it reads into sslCa from DevDbTls.GetSslCa(SERVER) first.
			new[] { @"tests\DbConnectionSecurityTests\LoopbackDatabaseTests.cs","SERVER,_dbName,\"root\",\"\",DatabaseType.MySql" },
		};

		///<summary>The calls the scan reads, each match ending at the call's opening parenthesis or the initializer's opening brace.</summary>
		private static readonly Regex _regexConnectionCall=new Regex(@"new\s+MySqlConnection\s*\(|\.SetDbT?\s*\(|new\s+DataConnection\s*\("
			+@"|new\s+CentralConnection(?:Base)?\s*(?:\(\s*\))?\s*\{|\bMakeABackup\s*\(|new\s+(?:[A-Za-z_][\w.]*\.)?OpenDentalServerMockIIS\s*\(");

		///<summary>The kind of call the scan checks: a connection to a server named in the call, which needs the local test server's CA through
		///DevDbTls. Null for a call of that name that names no server (it takes the thread's own connection), so the scan skips it.</summary>
		private static string GetConnectionKind(string matchValue,List<string> listArguments) {
			if(Regex.IsMatch(matchValue,@"^new\s+MySqlConnection")) {
				//new MySqlConnection(connectionString); new MySqlConnection() sets its string later.
				return listArguments.Count==1 && listArguments[0].Trim()!="" ? "MySqlConnection" : null;
			}
			if(matchValue.StartsWith(".SetDb")) {
				//SetDb(server,database,user,password,userLow,passLow,dbtype[,skipValidation][,sslCa]): the form with a server.
				return listArguments.Count>=7 && Regex.IsMatch(listArguments[6].Trim(),@"(DatabaseType\.MySql|DBtype)$") ? "SetDb" : null;
			}
			if(Regex.IsMatch(matchValue,@"^new\s+DataConnection")) {
				//DataConnection(server,database,user,password,dbtype[,sslCa]), (connectStr,server,database,user,password,dbtype) and the ten-argument
				//form; the forms with fewer arguments take the static or the thread's connection.
				return listArguments.Count>=5 ? "DataConnection" : null;
			}
			if(Regex.IsMatch(matchValue,@"^new\s+CentralConnection")) {
				//new CentralConnectionBase { ServerName=...,... } and new CentralConnection { ServerName=... }: ConnectionStoreBase.SetDb/SetDbT
				//connect with its SslCa.
				return listArguments.Any(x => Regex.IsMatch(x,@"^\s*ServerName\s*=")) ? "CentralConnection" : null;
			}
			if(matchValue.Contains("MakeABackup")) {
				//MakeABackup(serverName:...) or a server as the first argument: the archive tab's backup, to another server too.
				bool hasServer=listArguments.Any(x => Regex.IsMatch(x,@"^\s*serverName\s*:"))
					|| (listArguments.Count>0 && listArguments[0].Trim()!="" && !Regex.IsMatch(listArguments[0],@"^\s*[A-Za-z_]\w*\s*:(?!:)"));
				return hasServer ? "MakeABackup" : null;
			}
			if(matchValue.Contains("OpenDentalServerMockIIS")) {
				//The Middle Tier mock server with a server of its own (server: or the first argument) connects its thread with SetDbT and sslCa.
				bool hasServer=listArguments.Any(x => Regex.IsMatch(x,@"^\s*server\s*:"))
					|| (listArguments.Count>0 && listArguments[0].Trim()!="" && !Regex.IsMatch(listArguments[0],@"^\s*[A-Za-z_]\w*\s*:(?!:)"));
				return hasServer ? "OpenDentalServerMockIIS" : null;
			}
			return null;
		}

		[TestMethod]
		public void EveryTestAndToolConnectionToTheLocalServer_GoesThroughDevDbTls() {
			string root=GetRepoRoot();
			List<string> listMissing=new List<string>();
			Dictionary<string,int> dictCounts=new Dictionary<string,int>();
			foreach(string top in new[] { "tests","tools","UnitTests","UnitTestsCore","NoHqTests" }) {
				foreach(string path in GetSourceFiles(Path.Combine(root,top))) {
					string relative=path.Substring(root.Length+1);
					string text=File.ReadAllText(path);
					bool[] arrayIsCode=GetCodeMask(text);
					foreach(Match match in _regexConnectionCall.Matches(text)) {
						if(!arrayIsCode[match.Index]) {
							continue;
						}
						List<string> listArguments=GetArguments(text,arrayIsCode,match.Index+match.Length-1);
						string kind=GetConnectionKind(match.Value,listArguments);
						if(kind==null) {
							continue;
						}
						string call=string.Join(",",listArguments);
						dictCounts[kind]=(dictCounts.TryGetValue(kind,out int count) ? count : 0)+1;
						bool isThrough=kind=="MySqlConnection" ? listArguments[0].Trim().StartsWith("DevDbTls.Apply(") : call.Contains("DevDbTls.GetSslCa(");
						if(isThrough || _arrayAllowed.Any(x => x[0]==relative && call.Contains(x[1]))) {
							continue;
						}
						int line=text.Substring(0,match.Index).Count(x => x=='\n')+1;
						string callShown=Regex.Replace(call.Trim(),@"\s+"," ");
						listMissing.Add(relative+":"+line+" "+match.Value+callShown.Substring(0,Math.Min(80,callShown.Length)));
					}
				}
			}
			string counts=string.Join(", ",dictCounts.OrderBy(x => x.Key).Select(x => x.Key+" "+x.Value));
			Assert.IsTrue(dictCounts.Values.Sum()>150,"The scan found the helpers' connections ("+counts+").");
			foreach(string kind in new[] { "MySqlConnection","SetDb","DataConnection","CentralConnection","MakeABackup","OpenDentalServerMockIIS" }) {
				Assert.IsTrue(dictCounts.ContainsKey(kind),"The scan found calls of the kind "+kind+" ("+counts+").");
			}
			Assert.AreEqual("",string.Join("\r\n",listMissing),"Connect through DevDbTls (docs/port-notes/DB-TLS.md): "
				+"new MySqlConnection(DevDbTls.Apply(...)); SetDb/SetDbT(...,DevDbTls.GetSslCa(server)); new DataConnection(server,...,"
				+"DevDbTls.GetSslCa(server)); new CentralConnectionBase { ServerName=server,...,SslCa=DevDbTls.GetSslCa(server) }; "
				+"MiscData.MakeABackup(serverName:server,...,sslCa:DevDbTls.GetSslCa(server)); new OpenDentalServerMockIIS(server:server,...,"
				+"sslCa:DevDbTls.GetSslCa(server)).");
		}

		[TestMethod]
		public void TheConnectionScan_FindsEachCallShape_WithAndWithoutTheCa() {
			//The scan's own reading of synthetic calls (no file): which calls name a server, so that a new helper of any of these shapes is checked.
			string source=string.Join("\n",new[] {
				"var a=new MySqlConnection(\"Server=127.0.0.1\");",
				"var b=new MySqlConnection();",
				"new DataConnection().SetDb(Server,db,User,Password,\"\",\"\",DatabaseType.MySql,true);",
				"new DataConnection().SetDb(connectStr,\"\",DatabaseType.MySql);",
				"var c=new DataConnection(SERVER,db,\"root\",\"\",DatabaseType.MySql);",
				"var d=new DataConnection(DatabaseType.MySql);",
				"var e=new DataConnection(\"dbname\");",
				"ConnectionStoreBase.OverrideConnection(ConnectionNames.DentalOffice,new CentralConnectionBase {\n ServerName=TestServer,DatabaseName=x });",
				"var f=new CentralConnectionBase() { DatabaseName=x };",
				"var g=new CentralConnection { ServerName=s };",
				"MiscData.MakeABackup(serverName:Server+\":3306\",user:u,pass:p,doVerify:true);",
				"MiscData.MakeABackup(Server,u,p);",
				"MiscData.MakeABackup(isAutoBackup:false);",
				"MiscData.MakeABackup();",
				"var h=new OpenDentalServerMockIIS(server:Server,db:DbName,user:User);",
				"var i=new OpenPmsDentalBusiness.WebServices.OpenDentalServerMockIIS(TestServer,db,\"root\",\"\",low,pass,DatabaseType.MySql);",
				"var j=new OpenPmsDentalBusiness.WebServices.OpenDentalServerMockIIS(user:u,password:p);",
				"var k=new OpenDentalServerMockIIS();",
			});
			bool[] arrayIsCode=GetCodeMask(source);
			List<string> listKinds=_regexConnectionCall.Matches(source).Cast<Match>()
				.Select(x => GetConnectionKind(x.Value,GetArguments(source,arrayIsCode,x.Index+x.Length-1))??"-")
				.ToList();
			CollectionAssert.AreEqual(new[] {
				"MySqlConnection","-",//new MySqlConnection(string), new MySqlConnection()
				"-","SetDb",//new DataConnection(), .SetDb with a server
				"-","-",//new DataConnection(), .SetDb with a connection string
				"DataConnection","-","-",//new DataConnection(server,...), (DatabaseType), (database)
				"CentralConnection","-","CentralConnection",//with ServerName=, without, CentralConnection with ServerName=
				"MakeABackup","MakeABackup","-","-",//serverName:, a server first, neither, none
				"OpenDentalServerMockIIS","OpenDentalServerMockIIS","-","-",//server:, a server first (full name), user and password only, none
				},listKinds,string.Join(", ",listKinds));
		}

		#endregion Source checks
	}
}
