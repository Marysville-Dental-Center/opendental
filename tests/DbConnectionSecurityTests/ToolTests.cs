// The connection settings tool: its switches, its secrets file, the CA and .pfx checks, and saving the settings (no certificate store writes).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Xml;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalDbSettings;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class ToolTests {
		private string _folder;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
		}

		[TestCleanup]
		public void CleanUp() {
			TestHelpers.DeleteFolder(_folder);
		}

		#region Switches
		[TestMethod]
		public void Args_TheInstallersSilentCall() {
			DbSettingsArgs dbSettingsArgs=DbSettingsArgs.Parse(new[] { "/server="+TestHelpers.NETWORK_HOST+":3307","/database=opendental","/user=opmsd",
				@"/ca=C:\Temp\office ca.pem",@"/PFX=""C:\Temp\client.pfx""",@"/secretsfile=C:\Temp\s.txt","/silent","/TEST",@"/log=C:\Temp\l.txt" });
			Assert.AreEqual(TestHelpers.NETWORK_HOST+":3307",dbSettingsArgs.Server);
			Assert.AreEqual("opendental",dbSettingsArgs.Database);
			Assert.AreEqual("opmsd",dbSettingsArgs.User);
			Assert.AreEqual(@"C:\Temp\office ca.pem",dbSettingsArgs.CaFile);
			Assert.AreEqual(@"C:\Temp\client.pfx",dbSettingsArgs.PfxFile,"Quotes and case of the name do not matter.");
			Assert.AreEqual(DbSettingsAuth.Certificate,dbSettingsArgs.Auth,"/pfx means certificate mode.");
			Assert.AreEqual(@"C:\Temp\s.txt",dbSettingsArgs.SecretsFile);
			Assert.IsTrue(dbSettingsArgs.IsSilent && dbSettingsArgs.IsTest);
			Assert.IsTrue(dbSettingsArgs.HasChanges);
		}

		[TestMethod]
		public void Args_NoSwitches_TheWindow_AndCheck_AndThumbprintNormalized() {
			DbSettingsArgs dbSettingsArgs=DbSettingsArgs.Parse(new string[0]);
			Assert.IsFalse(dbSettingsArgs.IsSilent||dbSettingsArgs.IsCheck||dbSettingsArgs.HasChanges);
			Assert.IsTrue(DbSettingsArgs.Parse(new[] { "-check" }).IsCheck);
			Assert.IsTrue(DbSettingsArgs.Parse(new[] { "/?" }).IsHelp);
			DbSettingsArgs dbSettingsArgsThumbprint=DbSettingsArgs.Parse(new[] { "/thumbprint=\u200e01 23 45 67 89 ab cd ef 01 23 45 67 89 ab cd ef 01 23 45 67" });
			Assert.AreEqual("0123456789ABCDEF0123456789ABCDEF01234567",dbSettingsArgsThumbprint.Thumbprint);
			Assert.AreEqual(DbSettingsAuth.Certificate,dbSettingsArgsThumbprint.Auth);
			Assert.AreEqual(DbSettingsAuth.Password,DbSettingsArgs.Parse(new[] { "/auth=Password" }).Auth);
			StringAssert.Contains(DbSettingsArgs.Usage(),"Exit codes: 0 done");
		}

		[TestMethod]
		public void Args_RefuseSecretsOnTheCommandLine_WithoutRepeatingThem() {
			foreach(string name in new[] { "password","PASSWORD","dbpassword","pfxpassword","pwd","pass" }) {
				DbSettingsException ex=TestHelpers.AssertThrows<DbSettingsException>(() => DbSettingsArgs.Parse(new[] { "/"+name+"="+TestHelpers.SECRET }));
				Assert.AreEqual(DbSettingsRunner.EXIT_BAD_ARGS,ex.ExitCode);
				StringAssert.Contains(ex.Message,"not accepted on the command line");
				Assert.IsFalse(ex.Message.Contains(TestHelpers.SECRET),name);
			}
			DbSettingsException exBare=TestHelpers.AssertThrows<DbSettingsException>(() => DbSettingsArgs.Parse(new[] { "/silent",TestHelpers.SECRET }));
			Assert.AreEqual("Argument 2 is not a switch. Switches start with /.",exBare.Message,"A stray value is never repeated.");
		}

		[TestMethod]
		public void Args_RefuseBadSwitchesAndCombinations() {
			foreach(string[] arrayArgs in new[] {
				new[] { "/unknown=1" },
				new[] { "/server=a","/server=b" },
				new[] { "/server=" },
				new[] { "/silent=yes" },
				new[] { "/pfx=a.pfx","/thumbprint=0123456789ABCDEF0123456789ABCDEF01234567" },
				new[] { "/auth=password","/pfx=a.pfx" },
				new[] { "/auth=both" },
				new[] { "/thumbprint=1234" },
				new[] { "/check","/server=a" },
				new[] { "/check","/silent" },
				new[] { "/test" },
			})
			{
				DbSettingsException ex=TestHelpers.AssertThrows<DbSettingsException>(() => DbSettingsArgs.Parse(arrayArgs));
				Assert.AreEqual(DbSettingsRunner.EXIT_BAD_ARGS,ex.ExitCode,string.Join(" ",arrayArgs));
			}
			foreach(string[] arrayArgs in new[] { new[] { "/server=a;b" },new[] { "/server=a b" },new[] { "/server=a,b" },new[] { "/database=x.y" },
				new[] { "/user=a;b" } })
			{
				DbSettingsException ex=TestHelpers.AssertThrows<DbSettingsException>(() => DbSettingsArgs.Parse(arrayArgs));
				Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,ex.ExitCode,string.Join(" ",arrayArgs));
			}
		}
		#endregion Switches

		#region Secrets file
		[TestMethod]
		public void Secrets_ParseTheTwoLines_KeepingEveryCharacterOfTheValue() {
			DbSettingsSecrets dbSettingsSecrets=DbSettingsSecrets.Parse("\uFEFF# synthetic\r\nPfxPassword= a=b;c \r\n\r\ndbpassword="+TestHelpers.SECRET+"\n");
			Assert.AreEqual(" a=b;c ",dbSettingsSecrets.PfxPassword);
			Assert.AreEqual(TestHelpers.SECRET,dbSettingsSecrets.DbPassword);
			DbSettingsSecrets dbSettingsSecretsOne=DbSettingsSecrets.Parse("DbPassword=");
			Assert.AreEqual("",dbSettingsSecretsOne.DbPassword,"An empty password is a password.");
			Assert.IsNull(dbSettingsSecretsOne.PfxPassword);
		}

		[TestMethod]
		public void Secrets_RefuseOtherLines_WithoutRepeatingValues() {
			foreach(string text in new[] { TestHelpers.SECRET,"Other="+TestHelpers.SECRET,"DbPassword=a\nDbPassword="+TestHelpers.SECRET,"="+TestHelpers.SECRET }) {
				DbSettingsException ex=TestHelpers.AssertThrows<DbSettingsException>(() => DbSettingsSecrets.Parse(text));
				Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,ex.ExitCode);
				Assert.IsFalse(ex.Message.Contains(TestHelpers.SECRET),text);
			}
		}

		[TestMethod]
		public void Secrets_ReadFile_ReadsUtf8_AndNeverChangesTheFile() {
			string path=Path.Combine(_folder,"secrets.txt");
			File.WriteAllText(path,"DbPassword=pässwörd\r\n",new UTF8Encoding(true));
			DateTime dateTimeWritten=File.GetLastWriteTimeUtc(path);
			Assert.AreEqual("pässwörd",DbSettingsSecrets.ReadFile(path).DbPassword);
			Assert.IsTrue(File.Exists(path),"The caller deletes it.");
			Assert.AreEqual(dateTimeWritten,File.GetLastWriteTimeUtc(path));
			Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,
				TestHelpers.AssertThrows<DbSettingsException>(() => DbSettingsSecrets.ReadFile(Path.Combine(_folder,"none.txt"))).ExitCode);
			File.WriteAllBytes(path,new byte[70000]);
			StringAssert.Contains(TestHelpers.AssertThrows<DbSettingsException>(() => DbSettingsSecrets.ReadFile(path)).Message,"too large");
		}
		#endregion Secrets file

		#region The office CA and the client certificate
		[TestMethod]
		public void OfficeCa_PemOrDer_ACaOnly() {
			string pem=TestHelpers.WriteCaPem(_folder);
			List<X509Certificate2> listCertificates=OfficeCaFile.Load(pem);
			Assert.AreEqual(1,listCertificates.Count);
			StringAssert.Contains(OfficeCaFile.Describe(listCertificates),"CN=Synthetic Office CA");
			string der=Path.Combine(_folder,"office-ca.cer");
			File.WriteAllBytes(der,listCertificates[0].RawData);
			Assert.AreEqual(listCertificates[0].Thumbprint,OfficeCaFile.Load(der)[0].Thumbprint);
			string leaf=Path.Combine(_folder,"server.pem");
			using(X509Certificate2 x509Certificate2=TestHelpers.NewCertificate("CN="+TestHelpers.NETWORK_HOST,isCa:false)) {
				File.WriteAllText(leaf,OfficeCaFile.ToPem(new List<X509Certificate2> { new X509Certificate2(x509Certificate2.RawData) }));
			}
			StringAssert.Contains(TestHelpers.AssertThrows<DbSettingsException>(() => OfficeCaFile.Load(leaf)).Message,"not a certificate authority");
			string garbage=Path.Combine(_folder,"garbage.pem");
			File.WriteAllText(garbage,"not a certificate");
			Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,TestHelpers.AssertThrows<DbSettingsException>(() => OfficeCaFile.Load(garbage)).ExitCode);
			string withKey=Path.Combine(_folder,"with-key.pfx");
			using(X509Certificate2 x509Certificate2=TestHelpers.NewCertificate("CN=Synthetic Office CA",isCa:true)) {
				File.WriteAllBytes(withKey,x509Certificate2.Export(X509ContentType.Pfx,""));
			}
			StringAssert.Contains(TestHelpers.AssertThrows<DbSettingsException>(() => OfficeCaFile.Load(withKey)).Message,"private key");
		}

		[TestMethod]
		public void OfficeCa_WriteCopy_IsPemThatLoadsAgain() {
			List<X509Certificate2> listCertificates=OfficeCaFile.Load(TestHelpers.WriteCaPem(_folder));
			string pathCopy=DbConnectionConfig.GetOfficeCaCopyPath(Path.Combine(_folder,"program"));
			OfficeCaFile.WriteCopy(listCertificates,pathCopy);
			OfficeCaFile.WriteCopy(listCertificates,pathCopy);//a second time replaces it
			StringAssert.StartsWith(File.ReadAllText(pathCopy),"-----BEGIN CERTIFICATE-----");
			Assert.AreEqual(listCertificates[0].Thumbprint,OfficeCaFile.Load(pathCopy)[0].Thumbprint);
			Assert.IsFalse(File.Exists(pathCopy+".new"));
		}

		[TestMethod]
		public void OfficeCa_CopyPath_IsInTheProgramFolder_NeverProgramData() {
			string folderProgram=Path.Combine(_folder,"Open PMS - Dental");
			Assert.AreEqual(Path.Combine(folderProgram,"ConnectionSettings","office-ca.pem"),DbConnectionConfig.GetOfficeCaCopyPath(folderProgram));
			string folderProgramData=Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
			Assert.IsFalse(DbConnectionConfig.GetOfficeCaCopyPath(folderProgram).StartsWith(folderProgramData,StringComparison.OrdinalIgnoreCase));
		}

		[TestMethod]
		public void OfficeCa_WriteCopy_ProtectsTheFolderAndTheFile_UsersMayOnlyRead() {
			List<X509Certificate2> listCertificates=OfficeCaFile.Load(TestHelpers.WriteCaPem(_folder));
			string pathCopy=DbConnectionConfig.GetOfficeCaCopyPath(Path.Combine(_folder,"program"));
			OfficeCaFile.WriteCopy(listCertificates,pathCopy);
			OfficeCaFile.WriteCopy(listCertificates,pathCopy);//File.Replace keeps the old file's DACL; the protected one is set again
			AssertProtected(File.GetAccessControl(pathCopy).GetAccessRules(true,true,typeof(SecurityIdentifier)),
				File.GetAccessControl(pathCopy).AreAccessRulesProtected,pathCopy);
			string folder=Path.GetDirectoryName(pathCopy);
			AssertProtected(Directory.GetAccessControl(folder).GetAccessRules(true,true,typeof(SecurityIdentifier)),
				Directory.GetAccessControl(folder).AreAccessRulesProtected,folder);
			SecurityIdentifier securityIdentifierOwner=(SecurityIdentifier)File.GetAccessControl(pathCopy).GetOwner(typeof(SecurityIdentifier));
			if(OfficeCaFile.IsElevated()) {
				Assert.IsTrue(securityIdentifierOwner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid),"Owner Administrators.");
			}
		}

		///<summary>Nothing inherited; SYSTEM and Administrators full control; Users read only; no Everyone or Authenticated Users. Not elevated
		///(the tests), the account running them may have full control too; elevated (the tool), nobody else.</summary>
		private static void AssertProtected(AuthorizationRuleCollection authorizationRuleCollection,bool isProtected,string path) {
			Assert.IsTrue(isProtected,"Nothing inherited: "+path);
			SecurityIdentifier securityIdentifierUser;
			using(WindowsIdentity windowsIdentity=WindowsIdentity.GetCurrent()) {
				securityIdentifierUser=windowsIdentity.User;
			}
			FileSystemRights fileSystemRightsWrite=FileSystemRights.WriteData|FileSystemRights.AppendData|FileSystemRights.WriteAttributes
				|FileSystemRights.WriteExtendedAttributes|FileSystemRights.Delete|FileSystemRights.DeleteSubdirectoriesAndFiles
				|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership;
			bool hasSystem=false;
			bool hasAdministrators=false;
			bool hasUsers=false;
			foreach(FileSystemAccessRule fileSystemAccessRule in authorizationRuleCollection) {
				SecurityIdentifier securityIdentifier=(SecurityIdentifier)fileSystemAccessRule.IdentityReference;
				Assert.IsFalse(fileSystemAccessRule.IsInherited,path);
				Assert.AreEqual(AccessControlType.Allow,fileSystemAccessRule.AccessControlType,path);
				if(securityIdentifier.IsWellKnown(WellKnownSidType.LocalSystemSid)) {
					hasSystem=fileSystemAccessRule.FileSystemRights.HasFlag(FileSystemRights.FullControl);
				}
				else if(securityIdentifier.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid)) {
					hasAdministrators=fileSystemAccessRule.FileSystemRights.HasFlag(FileSystemRights.FullControl);
				}
				else if(securityIdentifier.IsWellKnown(WellKnownSidType.BuiltinUsersSid)) {
					hasUsers=true;
					Assert.AreEqual((FileSystemRights)0,fileSystemAccessRule.FileSystemRights & fileSystemRightsWrite,"Users may only read: "+path);
					Assert.IsTrue(fileSystemAccessRule.FileSystemRights.HasFlag(FileSystemRights.ReadData),"Users may read: "+path);
				}
				else {
					Assert.IsFalse(OfficeCaFile.IsElevated(),"Elevated, nobody else has a rule: "+securityIdentifier.Value);
					Assert.AreEqual(securityIdentifierUser,securityIdentifier,"Only the account running the tests: "+path);
				}
			}
			Assert.IsTrue(hasSystem && hasAdministrators && hasUsers,"SYSTEM, Administrators and Users: "+path);
		}

		[TestMethod]
		public void OfficeCa_WriteCopy_RefusesALinkOrAFileForTheFolder_AndWritesNothing() {
			List<X509Certificate2> listCertificates=OfficeCaFile.Load(TestHelpers.WriteCaPem(_folder));
			//A junction, as a user could plant one to send the administrator's write elsewhere. Junctions need no privilege to make.
			string folderProgram=Path.Combine(_folder,"program");
			string folderElsewhere=Path.Combine(_folder,"elsewhere");
			Directory.CreateDirectory(folderProgram);
			Directory.CreateDirectory(folderElsewhere);
			string pathCopy=DbConnectionConfig.GetOfficeCaCopyPath(folderProgram);
			MakeJunction(Path.GetDirectoryName(pathCopy),folderElsewhere);
			DbSettingsException ex=TestHelpers.AssertThrows<DbSettingsException>(() => OfficeCaFile.WriteCopy(listCertificates,pathCopy));
			Assert.AreEqual(DbSettingsRunner.EXIT_WRITE_FAILED,ex.ExitCode);
			StringAssert.Contains(ex.Message,"is a link");
			Assert.AreEqual(0,Directory.GetFileSystemEntries(folderElsewhere).Length,"Nothing written through the link.");
			Directory.Delete(Path.GetDirectoryName(pathCopy));//removes the junction only
			//A file where the folder belongs, and a folder where the file belongs.
			File.WriteAllText(Path.GetDirectoryName(pathCopy),"synthetic");
			StringAssert.Contains(TestHelpers.AssertThrows<DbSettingsException>(() => OfficeCaFile.WriteCopy(listCertificates,pathCopy)).Message,"is a file");
			File.Delete(Path.GetDirectoryName(pathCopy));
			Directory.CreateDirectory(pathCopy);
			StringAssert.Contains(TestHelpers.AssertThrows<DbSettingsException>(() => OfficeCaFile.WriteCopy(listCertificates,pathCopy)).Message,"is a folder");
		}

		private static void MakeJunction(string pathLink,string pathTarget) {
			System.Diagnostics.ProcessStartInfo processStartInfo=new System.Diagnostics.ProcessStartInfo(
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe"),
				"/c mklink /J \""+pathLink+"\" \""+pathTarget+"\"");
			processStartInfo.UseShellExecute=false;
			processStartInfo.CreateNoWindow=true;
			processStartInfo.RedirectStandardOutput=true;
			using(System.Diagnostics.Process process=System.Diagnostics.Process.Start(processStartInfo)) {
				process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				Assert.AreEqual(0,process.ExitCode,"mklink /J");
			}
			Assert.IsTrue((File.GetAttributes(pathLink) & FileAttributes.ReparsePoint)!=0,"The junction exists.");
		}

		[TestMethod]
		public void Pfx_CheckedWithoutStoringItsKey_PasswordAndUse() {
			string pfx=TestHelpers.WritePfx(_folder,TestHelpers.SECRET);
			using(X509Certificate2 x509Certificate2=ClientCertificateStore.LoadPfxForCheck(pfx,TestHelpers.SECRET)) {
				Assert.IsTrue(x509Certificate2.HasPrivateKey);
				StringAssert.Contains(ClientCertificateStore.Describe(x509Certificate2),"CN=synthetic-workstations");
			}
			DbSettingsException exWrong=TestHelpers.AssertThrows<DbSettingsException>(() => ClientCertificateStore.LoadPfxForCheck(pfx,"wrong"));
			Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,exWrong.ExitCode);
			StringAssert.Contains(exWrong.Message,"password is wrong");
			string pfxServer=TestHelpers.WritePfx(_folder,"x",isClientAuth:false,isServerAuthOnly:true,fileName:"server.pfx");
			StringAssert.Contains(TestHelpers.AssertThrows<DbSettingsException>(() => ClientCertificateStore.LoadPfxForCheck(pfxServer,"x")).Message,
				"not for client authentication");
			Assert.IsNull(ClientCertificateStore.FindInStore("00000000000000000000000000000000000000AA"),"Not in LocalMachine\\My.");
		}
		#endregion The office CA and the client certificate

		#region Saving
		[TestMethod]
		public void Apply_Loopback_PasswordMode_WritesTheFile() {
			string config=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","opendental","User","root","Password","",
				"MySQLPassHash","","NoShowOnStartup","False");
			DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(new DbSettingsRequest() { Database="opendental_test" },config,
				Path.Combine(_folder,"ca-copy.pem"));
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResult.ExitCode,dbSettingsResult.Message);
			DbConnectionConfig dbConnectionConfig=DbConnectionConfig.ReadFile(config);
			Assert.AreEqual("localhost",dbConnectionConfig.ServerName);
			Assert.AreEqual("opendental_test",dbConnectionConfig.Database);
			Assert.AreEqual("",dbConnectionConfig.PasswordDpapi,"A blank password stays blank.");
			Assert.AreEqual("False",dbConnectionConfig.NoShowOnStartup);
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,DbSettingsRunner.Check(config).ExitCode);
		}

		[TestMethod]
		public void Apply_Loopback_OldStylePassword_MovesIntoDpapi() {
			string config=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","opendental","User","root","Password",TestHelpers.SECRET);
			DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(new DbSettingsRequest(),config,Path.Combine(_folder,"ca-copy.pem"));
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResult.ExitCode,dbSettingsResult.Message);
			Assert.IsFalse(File.ReadAllText(config).Contains(TestHelpers.SECRET));
			DbConnectionConfig dbConnectionConfig=DbConnectionConfig.ReadFile(config);
			Assert.IsFalse(dbConnectionConfig.HasLegacyPassword);
			Assert.AreEqual(TestHelpers.SECRET,dbConnectionConfig.GetPassword());
		}

		[TestMethod]
		public void Apply_Network_PasswordMode_CopiesTheCa_AndStoresThePasswordWithDpapi() {
			string config=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","opendental","User","root","Password","");
			string caCopy=DbConnectionConfig.GetOfficeCaCopyPath(Path.Combine(_folder,"program"));
			DbSettingsRequest dbSettingsRequest=new DbSettingsRequest() { Server=TestHelpers.NETWORK_HOST,Database="opendental",User="opmsd",
				CaFile=TestHelpers.WriteCaPem(_folder,"picked-ca.pem"),Auth=DbSettingsAuth.Password,DbPassword=TestHelpers.SECRET };
			DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(dbSettingsRequest,config,caCopy);
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResult.ExitCode,dbSettingsResult.Message);
			Assert.IsFalse(dbSettingsResult.Message.Contains(TestHelpers.SECRET));
			Assert.IsTrue(File.Exists(caCopy));
			string text=File.ReadAllText(config);
			Assert.IsFalse(text.Contains(TestHelpers.SECRET));
			StringAssert.Contains(text,"<CompName>SYNTHETIC-PC</CompName>");
			DbConnectionConfig dbConnectionConfig=DbConnectionConfig.ReadFile(config);
			Assert.AreEqual(caCopy,dbConnectionConfig.SslCa,"SslCa points to the tool's own copy.");
			Assert.AreEqual(TestHelpers.SECRET,dbConnectionConfig.GetPassword());
			Assert.IsFalse(dbConnectionConfig.IsCertificateMode);
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,DbSettingsRunner.Check(config).ExitCode,"It passes the rule as the program applies it.");
			//The same user again without a password keeps the stored one.
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,DbSettingsRunner.Apply(new DbSettingsRequest() { Database="opendental2" },config,caCopy).ExitCode);
			Assert.AreEqual(TestHelpers.SECRET,DbConnectionConfig.ReadFile(config).GetPassword());
		}

		[TestMethod]
		public void Apply_Network_RefusesWithoutCaOrCredential_AndChangesNothing() {
			string config=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","opendental","User","root","Password","");
			string before=File.ReadAllText(config);
			string caCopy=Path.Combine(_folder,"ca-copy.pem");
			DbSettingsResult dbSettingsResultNoCa=DbSettingsRunner.Apply(new DbSettingsRequest() { Server=TestHelpers.NETWORK_HOST,
				Auth=DbSettingsAuth.Password,DbPassword="x" },config,caCopy);
			Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,dbSettingsResultNoCa.ExitCode);
			StringAssert.Contains(dbSettingsResultNoCa.Message,"office CA certificate");
			DbSettingsResult dbSettingsResultNoPassword=DbSettingsRunner.Apply(new DbSettingsRequest() { Server=TestHelpers.NETWORK_HOST,
				CaFile=TestHelpers.WriteCaPem(_folder),Auth=DbSettingsAuth.Password,User="other" },config,caCopy);
			Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,dbSettingsResultNoPassword.ExitCode);
			StringAssert.Contains(dbSettingsResultNoPassword.Message,"certificate or a database password");
			DbSettingsResult dbSettingsResultBadCa=DbSettingsRunner.Apply(new DbSettingsRequest() { Server=TestHelpers.NETWORK_HOST,
				CaFile=Path.Combine(_folder,"missing.pem"),DbPassword="x" },config,caCopy);
			Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,dbSettingsResultBadCa.ExitCode);
			DbSettingsResult dbSettingsResultNoCert=DbSettingsRunner.Apply(new DbSettingsRequest() { Server=TestHelpers.NETWORK_HOST,
				CaFile=TestHelpers.WriteCaPem(_folder),Thumbprint="00000000000000000000000000000000000000AA" },config,caCopy);
			Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,dbSettingsResultNoCert.ExitCode);
			StringAssert.Contains(dbSettingsResultNoCert.Message,"not in the computer's certificate store");
			DbSettingsResult dbSettingsResultPfxWrong=DbSettingsRunner.Apply(new DbSettingsRequest() { Server=TestHelpers.NETWORK_HOST,
				CaFile=TestHelpers.WriteCaPem(_folder),PfxFile=TestHelpers.WritePfx(_folder,"right"),PfxPassword="wrong" },config,caCopy);
			Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,dbSettingsResultPfxWrong.ExitCode,"Checked before anything is imported.");
			Assert.AreEqual(before,File.ReadAllText(config),"Nothing was written.");
			Assert.IsFalse(File.Exists(caCopy),"The CA was not copied.");
		}

		[TestMethod]
		public void RootUser_OverTheNetwork_IsWarnedAbout_NeverRefused() {
			//Owner decision of 2026-11-02: the office's own account (opmsd in the guide), not root. A warning, the settings are still saved.
			foreach(string user in new[] { "root","ROOT"," root " }) {
				StringAssert.Contains(DbSettingsRunner.GetRootUserWarning(TestHelpers.NETWORK_HOST,user),"its own account",user);
			}
			StringAssert.Contains(DbSettingsRunner.GetRootUserWarning(TestHelpers.NETWORK_ADDRESS+":3307","root"),"docs/db-connection-security.md");
			Assert.AreEqual("",DbSettingsRunner.GetRootUserWarning(TestHelpers.NETWORK_HOST,"opmsd"));
			Assert.AreEqual("",DbSettingsRunner.GetRootUserWarning(TestHelpers.NETWORK_HOST,"rootless"));
			foreach(string server in new[] { "localhost","127.0.0.1:3306","[::1]","" }) {
				Assert.AreEqual("",DbSettingsRunner.GetRootUserWarning(server,"root"),"Same computer: the developer default, no warning.");
			}
			string config=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","opendental","User","root","Password","");
			DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(new DbSettingsRequest() { Server=TestHelpers.NETWORK_HOST,User="root",
				CaFile=TestHelpers.WriteCaPem(_folder),Auth=DbSettingsAuth.Password,DbPassword=TestHelpers.SECRET },config,Path.Combine(_folder,"ca-copy.pem"));
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResult.ExitCode,dbSettingsResult.Message);
			StringAssert.Contains(dbSettingsResult.Message,"Warning: the database user root");
			Assert.IsFalse(dbSettingsResult.Message.Contains(TestHelpers.SECRET));
			Assert.AreEqual("root",DbConnectionConfig.ReadFile(config).User,"Saved as asked.");
			DbSettingsResult dbSettingsResultLocal=DbSettingsRunner.Apply(new DbSettingsRequest() { Server="localhost",User="root",
				Auth=DbSettingsAuth.Password,DbPassword="" },config,Path.Combine(_folder,"ca-copy.pem"));
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResultLocal.ExitCode,dbSettingsResultLocal.Message);
			Assert.IsFalse(dbSettingsResultLocal.Message.Contains("Warning"));
		}

		[TestMethod]
		public void Apply_Network_OldStylePassword_CannotBeKept() {
			string config=TestHelpers.WriteConfig(_folder,"ComputerName",TestHelpers.NETWORK_HOST,"Database","opendental","User","opmsd",
				"Password",TestHelpers.SECRET);
			DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(new DbSettingsRequest() { CaFile=TestHelpers.WriteCaPem(_folder) },config,
				Path.Combine(_folder,"ca-copy.pem"));
			Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,dbSettingsResult.ExitCode);
			StringAssert.Contains(dbSettingsResult.Message,"stored the old way");
			Assert.IsFalse(dbSettingsResult.Message.Contains(TestHelpers.SECRET));
			Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,DbSettingsRunner.Check(config).ExitCode);
		}

		[TestMethod]
		public void Apply_FromTheWindow_MessagesNameTheWindowsFields_NotTheSecretsFile() {
			string config=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","opendental","User","root","Password","");
			string caCopy=Path.Combine(_folder,"ca-copy.pem");
			string ca=TestHelpers.WriteCaPem(_folder);
			string pfx=TestHelpers.WritePfx(_folder,"right");
			foreach(bool isWindow in new[] { true,false }) {
				DbSettingsResult dbSettingsResultPassword=DbSettingsRunner.Apply(new DbSettingsRequest() { Server=TestHelpers.NETWORK_HOST,CaFile=ca,
					Auth=DbSettingsAuth.Password,User="other",IsWindow=isWindow },config,caCopy);
				Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,dbSettingsResultPassword.ExitCode);
				DbSettingsResult dbSettingsResultPfx=DbSettingsRunner.Apply(new DbSettingsRequest() { Server=TestHelpers.NETWORK_HOST,CaFile=ca,
					PfxFile=pfx,IsWindow=isWindow },config,caCopy);
				Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,dbSettingsResultPfx.ExitCode);
				DbSettingsResult dbSettingsResultCa=DbSettingsRunner.Apply(new DbSettingsRequest() { Server=TestHelpers.NETWORK_HOST,
					Auth=DbSettingsAuth.Password,DbPassword="x",IsWindow=isWindow },config,caCopy);
				Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,dbSettingsResultCa.ExitCode);
				foreach(DbSettingsResult dbSettingsResult in new[] { dbSettingsResultPassword,dbSettingsResultPfx,dbSettingsResultCa }) {
					Assert.AreEqual(!isWindow,dbSettingsResult.Message.Contains("secrets file") || dbSettingsResult.Message.Contains("/"),
						dbSettingsResult.Message);
				}
				if(isWindow) {
					StringAssert.Contains(dbSettingsResultPassword.Message,"Password field");
					StringAssert.Contains(dbSettingsResultPfx.Message,"Import .pfx...");
					StringAssert.Contains(dbSettingsResultCa.Message,"Browse...");
				}
			}
			Assert.IsFalse(File.Exists(caCopy),"Nothing was saved.");
		}

		[TestMethod]
		public void Check_AConnectionStringElement_ComesFirst_AndOnlyForThisComputer() {
			string path=Path.Combine(_folder,"FreeDentalConfig.xml");
			File.WriteAllText(path,"<ConnectionSettings><ConnectionString>Server="+TestHelpers.NETWORK_HOST+";SslMode=VerifyFull;Password=x"
				+"</ConnectionString></ConnectionSettings>");
			DbSettingsResult dbSettingsResult=DbSettingsRunner.Check(path);
			Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,dbSettingsResult.ExitCode);
			StringAssert.Contains(dbSettingsResult.Message,"Connection strings are for a database on this computer only");
			File.WriteAllText(path,"<ConnectionSettings><ConnectionString>Server=localhost;Password=x</ConnectionString></ConnectionSettings>");
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,DbSettingsRunner.Check(path).ExitCode,"The program uses it, and it is this computer.");
			//Saving with the tool removes the element (DbConnectionConfig.WriteTo).
			File.WriteAllText(path,"<ConnectionSettings><ConnectionString>Server="+TestHelpers.NETWORK_HOST+";Password=x</ConnectionString>"
				+"<DatabaseConnection><ComputerName>localhost</ComputerName><Database>opendental</Database><User>root</User></DatabaseConnection>"
				+"</ConnectionSettings>");
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,DbSettingsRunner.Apply(new DbSettingsRequest(),path,Path.Combine(_folder,"ca-copy.pem")).ExitCode);
			Assert.IsFalse(File.ReadAllText(path).Contains("ConnectionString>"));
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,DbSettingsRunner.Check(path).ExitCode);
		}

		[TestMethod]
		public void Check_MissingFile_OrNoConnection_IsRefused() {
			Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,DbSettingsRunner.Check(Path.Combine(_folder,"none.xml")).ExitCode);
			string path=Path.Combine(_folder,"FreeDentalConfig.xml");
			File.WriteAllText(path,"<ConnectionSettings />");
			Assert.AreEqual(DbSettingsRunner.EXIT_REFUSED,DbSettingsRunner.Check(path).ExitCode);
		}

		[TestMethod]
		public void RequestFromArgs_TakesTheSecretsFromTheFile_AndAppendLogWritesTheResult() {
			DbSettingsArgs dbSettingsArgs=DbSettingsArgs.Parse(new[] { "/auth=password","/user=opmsd" });
			DbSettingsRequest dbSettingsRequest=DbSettingsRunner.RequestFromArgs(dbSettingsArgs,DbSettingsSecrets.Parse("DbPassword=x"));
			Assert.AreEqual("x",dbSettingsRequest.DbPassword);
			Assert.AreEqual(DbSettingsAuth.Password,dbSettingsRequest.Auth);
			Assert.IsNull(dbSettingsRequest.Server,"Not given: the current value is kept.");
			string log=Path.Combine(_folder,"dbsettings.log");
			DbSettingsRunner.AppendLog(log,new DbSettingsResult(DbSettingsRunner.EXIT_REFUSED,"synthetic message"));
			StringAssert.Contains(File.ReadAllText(log),ProgramNames.DbSettingsExeFileName+" exit=5 synthetic message");
		}
		#endregion Saving
	}
}
