// FreeDentalConfig.xml: reading old-style and new configs, the password rules, writing, and the program's start-up reader.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Xml;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalDbSettings;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class ConfigTests {
		private string _folder;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
		}

		[TestCleanup]
		public void CleanUp() {
			TestHelpers.DeleteFolder(_folder);
		}

		#region DbConnectionConfig
		[TestMethod]
		public void OldStyle_Loopback_TheDeveloperConfigKeepsWorking() {
			string path=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","opendental_test","User","root","Password","",
				"MySQLPassHash","","NoShowOnStartup","True");
			DbConnectionConfig dbConnectionConfig=DbConnectionConfig.ReadFile(path);
			Assert.AreEqual("",dbConnectionConfig.GetPassword());
			dbConnectionConfig.ThrowIfRefused();
			string pathPlain=TestHelpers.WriteConfig(_folder,"ComputerName","127.0.0.1:3306","Database","x","User","root","Password",TestHelpers.SECRET);
			Assert.AreEqual(TestHelpers.SECRET,DbConnectionConfig.ReadFile(pathPlain).GetPassword());
			string pathHash=TestHelpers.WriteConfig(_folder,"ComputerName","::1","Database","x","User","root","Password","",
				"MySQLPassHash",Secrets.TryEncrypt(TestHelpers.SECRET));
			Assert.AreEqual(TestHelpers.SECRET,DbConnectionConfig.ReadFile(pathHash).GetPassword(),"osd1$ is still read for this computer.");
		}

		[TestMethod]
		public void OldStyle_Network_IsRefused_WithTheMessageToRunTheTool() {
			foreach(string[] arrayPassword in new[] { new[] { "Password",TestHelpers.SECRET },new[] { "MySQLPassHash",Secrets.TryEncrypt(TestHelpers.SECRET) } }) {
				string path=TestHelpers.WriteConfig(_folder,"ComputerName",TestHelpers.NETWORK_HOST,"Database","opendental","User","opmsd",
					arrayPassword[0],arrayPassword[1],"SslCa",TestHelpers.WriteCaPem(_folder));
				DbConnectionConfig dbConnectionConfig=DbConnectionConfig.ReadFile(path);
				Assert.IsTrue(dbConnectionConfig.HasLegacyPassword);
				DbConnectionRefusedException ex=RuleTests.AssertRefused(() => dbConnectionConfig.GetPassword());
				StringAssert.Contains(ex.Message,"stored the old way (Password or MySQLPassHash)");
				StringAssert.Contains(ex.Message,TestHelpers.NETWORK_HOST);
				StringAssert.Contains(ex.Message,ProgramNames.DbSettingsExeFileName);
				Assert.IsFalse(ex.Message.Contains(TestHelpers.SECRET));
				RuleTests.AssertRefused(() => dbConnectionConfig.ThrowIfRefused());
			}
		}

		[TestMethod]
		public void NewStyle_Network_DpapiPassword_OrCertificate_PassTheRule() {
			string ca=TestHelpers.WriteCaPem(_folder);
			string path=TestHelpers.WriteConfig(_folder,"ComputerName",TestHelpers.NETWORK_HOST,"Database","opendental","User","opmsd","Password","",
				"MySQLPassHash","","MySQLPassDpapi",MachineSecret.Protect(TestHelpers.SECRET),"SslCa",ca);
			DbConnectionConfig dbConnectionConfig=DbConnectionConfig.ReadFile(path);
			Assert.AreEqual(TestHelpers.SECRET,dbConnectionConfig.GetPassword());
			Assert.IsFalse(dbConnectionConfig.IsCertificateMode);
			dbConnectionConfig.ThrowIfRefused();
			string pathCert=TestHelpers.WriteConfig(_folder,"ComputerName",TestHelpers.NETWORK_ADDRESS,"Database","opendental","User","opmsd",
				"SslCa",ca,"ClientCertThumbprint","01 23 45 67 89 ab cd ef 01 23 45 67 89 ab cd ef 01 23 45 67");
			DbConnectionConfig dbConnectionConfigCert=DbConnectionConfig.ReadFile(pathCert);
			Assert.AreEqual("0123456789ABCDEF0123456789ABCDEF01234567",dbConnectionConfigCert.ClientCertThumbprint,"As certlm.msc shows it, normalized.");
			Assert.IsTrue(dbConnectionConfigCert.IsCertificateMode);
			Assert.AreEqual("",dbConnectionConfigCert.GetPassword());
			dbConnectionConfigCert.ThrowIfRefused();
		}

		[TestMethod]
		public void NewStyle_Network_WithoutCaOrCredential_IsRefused() {
			string path=TestHelpers.WriteConfig(_folder,"ComputerName",TestHelpers.NETWORK_HOST,"Database","opendental","User","opmsd","Password","");
			DbConnectionRefusedException ex=RuleTests.AssertRefused(() => DbConnectionConfig.ReadFile(path).ThrowIfRefused());
			Assert.AreEqual(2,ex.ListMissing.Count,ex.Message);
		}

		[TestMethod]
		public void DpapiValueFromAnotherComputer_IsRefused_NotSentAsAPassword() {
			string path=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","x","User","root","MySQLPassDpapi",MachineSecret.Prefix+"AAAAAAAAAAAA");
			DbConnectionRefusedException ex=RuleTests.AssertRefused(() => DbConnectionConfig.ReadFile(path).GetPassword());
			StringAssert.Contains(ex.Message,"cannot be read");
		}

		[TestMethod]
		public void ReadFile_MissingFile_OrNoElement_IsNull_AndMissingChildrenReadEmpty() {
			Assert.IsNull(DbConnectionConfig.ReadFile(Path.Combine(_folder,"none.xml")));
			string path=Path.Combine(_folder,"FreeDentalConfig.xml");
			File.WriteAllText(path,"<ConnectionSettings><ServerConnection><URI>https://mt.example.test/</URI></ServerConnection></ConnectionSettings>");
			Assert.IsNull(DbConnectionConfig.ReadFile(path));
			File.WriteAllText(path,"<ConnectionSettings><DatabaseConnection><ComputerName>localhost</ComputerName></DatabaseConnection></ConnectionSettings>");
			DbConnectionConfig dbConnectionConfig=DbConnectionConfig.ReadFile(path);
			Assert.AreEqual("",dbConnectionConfig.User);
			Assert.AreEqual("",dbConnectionConfig.GetPassword());
		}

		[TestMethod]
		public void SaveFile_KeepsTheOtherElements_NeverWritesAPasswordInTheOldWays_AndRoundTrips() {
			string ca=TestHelpers.WriteCaPem(_folder);
			string path=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","old","User","root","Password",TestHelpers.SECRET,
				"NoShowOnStartup","True");
			DbConnectionConfig dbConnectionConfig=DbConnectionConfig.ReadFile(path);
			dbConnectionConfig.ServerName=TestHelpers.NETWORK_HOST+":3307";
			dbConnectionConfig.Database="opendental";
			dbConnectionConfig.User="opmsd";
			dbConnectionConfig.SslCa=ca;
			dbConnectionConfig.SetPassword(TestHelpers.SECRET);
			dbConnectionConfig.SaveFile(path);
			string text=File.ReadAllText(path);
			Assert.IsFalse(text.Contains(TestHelpers.SECRET),"No plain text password.");
			Assert.IsFalse(text.Contains(Secrets.Prefix),"No osd1$.");
			StringAssert.Contains(text,"<CompName>SYNTHETIC-PC</CompName>","Elements the tool does not own are kept.");
			StringAssert.Contains(text,"<UseDynamicMode>False</UseDynamicMode>");
			XmlDocument xmlDocument=new XmlDocument();
			xmlDocument.Load(path);
			Assert.AreEqual("",xmlDocument.SelectSingleNode("//DatabaseConnection/Password").InnerText,"Kept empty for older readers.");
			Assert.AreEqual("",xmlDocument.SelectSingleNode("//DatabaseConnection/MySQLPassHash").InnerText);
			Assert.AreEqual("True",xmlDocument.SelectSingleNode("//DatabaseConnection/NoShowOnStartup").InnerText);
			StringAssert.StartsWith(xmlDocument.SelectSingleNode("//DatabaseConnection/MySQLPassDpapi").InnerText,MachineSecret.Prefix);
			Assert.IsNull(xmlDocument.SelectSingleNode("//DatabaseConnection/ClientCertThumbprint"),"Not written when empty.");
			DbConnectionConfig dbConnectionConfigRead=DbConnectionConfig.ReadFile(path);
			Assert.AreEqual(TestHelpers.NETWORK_HOST+":3307",dbConnectionConfigRead.ServerName);
			Assert.AreEqual(ca,dbConnectionConfigRead.SslCa);
			Assert.AreEqual(TestHelpers.SECRET,dbConnectionConfigRead.GetPassword());
			Assert.IsFalse(File.Exists(path+".new"));
		}

		[TestMethod]
		public void SaveFile_SwitchingToCertificate_RemovesThePassword_AndMakesADirectConnection() {
			string path=Path.Combine(_folder,"FreeDentalConfig.xml");
			File.WriteAllText(path,"<ConnectionSettings><ConnectionString>Server=localhost</ConnectionString><DatabaseConnection>"
				+"<MySQLPassDpapi>"+MachineSecret.Protect("x")+"</MySQLPassDpapi></DatabaseConnection></ConnectionSettings>");
			DbConnectionConfig dbConnectionConfig=new DbConnectionConfig() { ServerName=TestHelpers.NETWORK_HOST,Database="opendental",User="opmsd",
				SslCa=TestHelpers.WriteCaPem(_folder),ClientCertThumbprint="0123456789ABCDEF0123456789ABCDEF01234567" };
			dbConnectionConfig.SetPassword("");
			dbConnectionConfig.SaveFile(path);
			XmlDocument xmlDocument=new XmlDocument();
			xmlDocument.Load(path);
			Assert.IsNull(xmlDocument.SelectSingleNode("//MySQLPassDpapi"));
			Assert.IsNull(xmlDocument.SelectSingleNode("//ConnectionString"),"It would win over DatabaseConnection.");
			Assert.AreEqual("0123456789ABCDEF0123456789ABCDEF01234567",xmlDocument.SelectSingleNode("//ClientCertThumbprint").InnerText);
			Assert.AreEqual("MySql",xmlDocument.SelectSingleNode("/ConnectionSettings/DatabaseType").InnerText);
		}

		[TestMethod]
		public void SaveFile_CreatesANewFile() {
			string path=Path.Combine(_folder,"FreeDentalConfig.xml");
			new DbConnectionConfig() { ServerName="localhost",Database="opendental",User="root" }.SaveFile(path);
			DbConnectionConfig dbConnectionConfig=DbConnectionConfig.ReadFile(path);
			Assert.AreEqual("localhost",dbConnectionConfig.ServerName);
			Assert.AreEqual("",dbConnectionConfig.GetPassword());
		}
		#endregion DbConnectionConfig

		#region The program's start-up reader (CentralConnections)
		[TestMethod]
		public void StartUp_Release_DeveloperConfig_Loopback_NoProblem() {
			string path=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","opendental_test","User","root","Password","",
				"MySQLPassHash","","NoShowOnStartup","False");
			ChooseDatabaseInfo chooseDatabaseInfo=CentralConnections.GetChooseDatabaseConnectionSettings(path,isDebug:false);
			Assert.AreEqual("",chooseDatabaseInfo.ConfigProblem);
			Assert.AreEqual("localhost",chooseDatabaseInfo.CentralConnectionCur.ServerName);
			Assert.AreEqual("",chooseDatabaseInfo.CentralConnectionCur.MySqlPassword);
			CollectionAssert.AreEqual(new[] { "SYNTHETIC-PC" },chooseDatabaseInfo.ListAdminCompNames);
		}

		[TestMethod]
		public void StartUp_Release_OldStyleNetworkPassword_IsAProblem_AndNoPasswordIsKept() {
			string path=TestHelpers.WriteConfig(_folder,"ComputerName",TestHelpers.NETWORK_HOST,"Database","opendental","User","opmsd",
				"Password",TestHelpers.SECRET);
			foreach(bool isDebug in new[] { false,true }) {
				ChooseDatabaseInfo chooseDatabaseInfo=CentralConnections.GetChooseDatabaseConnectionSettings(path,isDebug);
				StringAssert.Contains(chooseDatabaseInfo.ConfigProblem,"stored the old way");
				Assert.AreEqual("",chooseDatabaseInfo.CentralConnectionCur.MySqlPassword);
				Assert.IsFalse(chooseDatabaseInfo.ConfigProblem.Contains(TestHelpers.SECRET));
			}
		}

		[TestMethod]
		public void StartUp_ReadsTheCaTheCertificateAndTheDpapiPassword() {
			string ca=TestHelpers.WriteCaPem(_folder);
			string path=TestHelpers.WriteConfig(_folder,"ComputerName",TestHelpers.NETWORK_HOST,"Database","opendental","User","opmsd",
				"MySQLPassDpapi",MachineSecret.Protect(TestHelpers.SECRET),"SslCa",ca,"ClientCertThumbprint","0123456789ABCDEF0123456789ABCDEF01234567");
			ChooseDatabaseInfo chooseDatabaseInfo=CentralConnections.GetChooseDatabaseConnectionSettings(path,isDebug:false);
			Assert.AreEqual("",chooseDatabaseInfo.ConfigProblem);
			Assert.AreEqual(ca,chooseDatabaseInfo.CentralConnectionCur.SslCA);
			Assert.AreEqual("0123456789ABCDEF0123456789ABCDEF01234567",chooseDatabaseInfo.CentralConnectionCur.ClientCertThumbprint);
			Assert.AreEqual(TestHelpers.SECRET,chooseDatabaseInfo.CentralConnectionCur.MySqlPassword);
		}

		[TestMethod]
		public void StartUp_Release_MissingEmptyOrConnectionlessFile_IsAProblemThatNamesTheTool() {
			string path=Path.Combine(_folder,"FreeDentalConfig.xml");
			ChooseDatabaseInfo chooseDatabaseInfoMissing=CentralConnections.GetChooseDatabaseConnectionSettings(path,isDebug:false);
			StringAssert.Contains(chooseDatabaseInfoMissing.ConfigProblem,"is missing");
			StringAssert.Contains(chooseDatabaseInfoMissing.ConfigProblem,ProgramNames.DbSettingsExeFileName);
			Assert.IsFalse(File.Exists(path),"Release never creates the file.");
			File.WriteAllText(path,"");
			StringAssert.Contains(CentralConnections.GetChooseDatabaseConnectionSettings(path,isDebug:false).ConfigProblem,"cannot be read");
			File.WriteAllText(path,"<ConnectionSettings><DatabaseType>MySql</DatabaseType></ConnectionSettings>");
			StringAssert.Contains(CentralConnections.GetChooseDatabaseConnectionSettings(path,isDebug:false).ConfigProblem,"has no database connection");
		}

		///<summary>A FreeDentalConfig.xml with a ConnectionString element and a DatabaseConnection for this computer.</summary>
		private string WriteConfigWithConnectionString(string connectionString) {
			string path=Path.Combine(_folder,"FreeDentalConfig.xml");
			File.WriteAllText(path,"<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<ConnectionSettings>\r\n    <ConnectionString>"
				+System.Security.SecurityElement.Escape(connectionString)+"</ConnectionString>\r\n    <DatabaseConnection>\r\n"
				+"        <ComputerName>localhost</ComputerName>\r\n        <Database>opendental</Database>\r\n        <User>root</User>\r\n"
				+"        <Password></Password>\r\n        <MySQLPassHash></MySQLPassHash>\r\n    </DatabaseConnection>\r\n"
				+"    <DatabaseType>MySql</DatabaseType>\r\n</ConnectionSettings>\r\n");
			return path;
		}

		[TestMethod]
		public void StartUp_ConnectionStringForANetworkServer_IsAProblemInReleaseAndDebug_AndIsNeverUsed() {
			//Its credential would be neither the client certificate nor the password stored with DPAPI (owner decision Q2), even with TLS.
			string ca=TestHelpers.WriteCaPem(_folder);
			string secret=TestHelpers.SECRET.Replace(";","").Replace("=","");
			foreach(string connectionString in new[] { "Server="+TestHelpers.NETWORK_HOST+";SslMode=VerifyFull;SslCa="+ca+";Password="+secret,
				"Server=localhost:3306;SslMode=None;Pwd="+secret,"Server=127.0.0.1,"+TestHelpers.NETWORK_ADDRESS+";Password="+secret })
			{
				string path=WriteConfigWithConnectionString(connectionString);
				foreach(bool isDebug in new[] { false,true }) {
					ChooseDatabaseInfo chooseDatabaseInfo=CentralConnections.GetChooseDatabaseConnectionSettings(path,isDebug);
					StringAssert.Contains(chooseDatabaseInfo.ConfigProblem,"Connection strings are for a database on this computer only",connectionString);
					StringAssert.Contains(chooseDatabaseInfo.ConfigProblem,ProgramNames.DbSettingsExeFileName);
					Assert.IsFalse(chooseDatabaseInfo.ConfigProblem.Contains(secret),"No password in the problem.");
					Assert.AreEqual("",chooseDatabaseInfo.ConnectionString,"The string is never used.");
				}
				Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,DbSettingsRunner.Check(path).ExitCode,"The tool's /check sees it too.");
			}
		}

		[TestMethod]
		public void StartUp_ConnectionStringForThisComputer_IsUsedAsBefore() {
			string connectionString="Server=127.0.0.1;Port=3306;Database=opendental;User ID=root;Password=;SslMode=None";
			string path=WriteConfigWithConnectionString(connectionString);
			ChooseDatabaseInfo chooseDatabaseInfo=CentralConnections.GetChooseDatabaseConnectionSettings(path,isDebug:false);
			Assert.AreEqual("",chooseDatabaseInfo.ConfigProblem);
			Assert.AreEqual(connectionString,chooseDatabaseInfo.ConnectionString);
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,DbSettingsRunner.Check(path).ExitCode);
		}

		[TestMethod]
		public void TryToConnect_RefusesAConnectionStringForANetworkServer_BeforeConnecting() {
			string thumbprintOld=DataConnection.ClientCertificateThumbprint;
			string officeCaPathOld=DataConnection.OfficeCaPath;
			try {
				string ca=TestHelpers.WriteCaPem(_folder);
				DbConnectionRefusedException ex=RuleTests.AssertRefused(() => CentralConnections.TryToConnect(new CentralConnection(),DatabaseType.MySql,
					"Server="+TestHelpers.NETWORK_ADDRESS+";SslMode=VerifyFull;SslCa="+ca+";Password=x"));
				StringAssert.Contains(ex.Message,"Connection strings are for a database on this computer only");
			}
			finally {
				DataConnection.ClientCertificateThumbprint=thumbprintOld;
				DataConnection.OfficeCaPath=officeCaPathOld;
			}
		}

		[TestMethod]
		public void StartUp_Debug_KeepsTheOldDefaults_ForChooseDatabase() {
			string path=Path.Combine(_folder,"FreeDentalConfig.xml");
			ChooseDatabaseInfo chooseDatabaseInfo=CentralConnections.GetChooseDatabaseConnectionSettings(path,isDebug:true);
			Assert.AreEqual("",chooseDatabaseInfo.ConfigProblem);
			Assert.AreEqual("localhost",chooseDatabaseInfo.CentralConnectionCur.ServerName);
			Assert.IsTrue(File.Exists(path),"Debug creates the file, as before.");
		}

		[TestMethod]
		public void CommandLine_NetworkServer_RefusesACommandLinePassword_AndUsesTheStoredOne() {
			string ca=TestHelpers.WriteCaPem(_folder);
			DbConnectionConfig dbConnectionConfig=new DbConnectionConfig() { ServerName=TestHelpers.NETWORK_HOST,Database="opendental",User="opmsd",
				SslCa=ca,ClientCertThumbprint="0123456789ABCDEF0123456789ABCDEF01234567" };
			dbConnectionConfig.SetPassword(TestHelpers.SECRET);
			ChooseDatabaseInfo chooseDatabaseInfo=new ChooseDatabaseInfo();
			ChooseDatabaseInfo.ApplyCommandLineConnection(chooseDatabaseInfo,dbConnectionConfig,TestHelpers.NETWORK_HOST,"opendental2","opmsd",
				"typed-on-the-command-line","");
			StringAssert.Contains(chooseDatabaseInfo.ConfigProblem,"password on the command line is not accepted");
			Assert.AreEqual("",chooseDatabaseInfo.CentralConnectionCur.MySqlPassword);
			Assert.IsFalse(chooseDatabaseInfo.ConfigProblem.Contains("typed-on-the-command-line"));
			ChooseDatabaseInfo chooseDatabaseInfoStored=new ChooseDatabaseInfo();
			ChooseDatabaseInfo.ApplyCommandLineConnection(chooseDatabaseInfoStored,dbConnectionConfig,TestHelpers.NETWORK_HOST,"opendental2","opmsd","","");
			Assert.AreEqual("",chooseDatabaseInfoStored.ConfigProblem);
			Assert.AreEqual(TestHelpers.SECRET,chooseDatabaseInfoStored.CentralConnectionCur.MySqlPassword,"The configured user's stored password.");
			Assert.AreEqual(ca,chooseDatabaseInfoStored.CentralConnectionCur.SslCA);
			Assert.AreEqual("0123456789ABCDEF0123456789ABCDEF01234567",chooseDatabaseInfoStored.CentralConnectionCur.ClientCertThumbprint);
			Assert.AreEqual("opendental2",chooseDatabaseInfoStored.CentralConnectionCur.DatabaseName);
			ChooseDatabaseInfo chooseDatabaseInfoOtherUser=new ChooseDatabaseInfo();
			ChooseDatabaseInfo.ApplyCommandLineConnection(chooseDatabaseInfoOtherUser,dbConnectionConfig,TestHelpers.NETWORK_HOST,"opendental","other","","");
			Assert.AreEqual("",chooseDatabaseInfoOtherUser.CentralConnectionCur.MySqlPassword,"Another user's password is never sent.");
		}

		[TestMethod]
		public void CommandLine_Loopback_TakesTheCommandLinePassword_AsBefore() {
			ChooseDatabaseInfo chooseDatabaseInfo=new ChooseDatabaseInfo();
			ChooseDatabaseInfo.ApplyCommandLineConnection(chooseDatabaseInfo,null,"localhost","opendental","root",TestHelpers.SECRET,"");
			Assert.AreEqual("",chooseDatabaseInfo.ConfigProblem);
			Assert.AreEqual(TestHelpers.SECRET,chooseDatabaseInfo.CentralConnectionCur.MySqlPassword);
			ChooseDatabaseInfo chooseDatabaseInfoHash=new ChooseDatabaseInfo();
			ChooseDatabaseInfo.ApplyCommandLineConnection(chooseDatabaseInfoHash,null,"127.0.0.1","opendental","root","",Secrets.TryEncrypt(TestHelpers.SECRET));
			Assert.AreEqual(TestHelpers.SECRET,chooseDatabaseInfoHash.CentralConnectionCur.MySqlPassword,"The self-update's hash, for this computer.");
		}
		#endregion The program's start-up reader (CentralConnections)
	}
}
