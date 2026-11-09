// The owner's answers of 2026-11-02 for the connection settings: the tool removes only a client certificate it imported itself, a save sets
// FreeDentalConfig.xml's rights back to the program folder's, a silent setup whose connection step fails ends with exit code 10, and the
// start-up's sql_mode stop names the option-file fix.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.RegularExpressions;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalDbSettings;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class OwnerAnswersTests {
		private const string THUMB_A="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
		private const string THUMB_B="BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
		private const string THUMB_C="CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC";
		private string _folder;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
		}

		[TestCleanup]
		public void CleanUp() {
			TestHelpers.DeleteFolder(_folder);
		}

		private static void Plan(string recorded,string current,string newThumbprint,bool isImported,bool wasInStore,out string toRecord,out string toRemove) {
			DbSettingsRunner.PlanImportedCertificate(recorded,current,newThumbprint,isImported,wasInStore,out toRecord,out toRemove);
		}

		#region The certificate the tool replaces (answer b)
		[TestMethod]
		public void Plan_RemovesOnlyTheRecordedCertificate_WhenTheSettingsStopUsingIt() {
			//The tool imported A; a new .pfx (B, not in the store before) replaces it: A goes, B is recorded.
			Plan(THUMB_A,THUMB_A,THUMB_B,true,false,out string toRecord,out string toRemove);
			Assert.AreEqual(THUMB_B,toRecord);
			Assert.AreEqual(THUMB_A,toRemove);
			//The password replaces it: A goes, nothing is recorded.
			Plan(THUMB_A,THUMB_A,"",false,false,out toRecord,out toRemove);
			Assert.AreEqual("",toRecord);
			Assert.AreEqual(THUMB_A,toRemove);
			//A certificate named by thumbprint replaces it: A goes, C was not imported, so it is not recorded and never removed later.
			Plan(THUMB_A,THUMB_A,THUMB_C,false,false,out toRecord,out toRemove);
			Assert.AreEqual("",toRecord);
			Assert.AreEqual(THUMB_A,toRemove);
		}

		[TestMethod]
		public void Plan_NeverRemovesACertificateTheToolDidNotImport() {
			//Nothing recorded (a certificate named by thumbprint, or put there by hand): nothing is removed, whatever replaces it.
			foreach(string newThumbprint in new[] { THUMB_B,"" }) {
				Plan("",THUMB_A,newThumbprint,true,false,out string toRecord,out string toRemove);
				Assert.AreEqual("",toRemove,newThumbprint);
			}
			//A .pfx whose certificate the store already had: the tool did not import it, so it is not recorded.
			Plan("",THUMB_A,THUMB_B,true,true,out string toRecordB,out string toRemoveB);
			Assert.AreEqual("",toRecordB);
			Assert.AreEqual("",toRemoveB);
			//A record of a certificate the settings no longer used (they were changed another way): it is not the one being replaced.
			Plan(THUMB_C,THUMB_A,THUMB_B,true,false,out string toRecordC,out string toRemoveC);
			Assert.AreEqual(THUMB_B,toRecordC);
			Assert.AreEqual("",toRemoveC);
		}

		[TestMethod]
		public void Plan_KeepingOrReimportingTheToolsCertificate_KeepsItAndItsRecord() {
			//The same settings saved again, or its .pfx imported again (the store has it, from the tool): nothing goes, the record stays.
			Plan(THUMB_A,THUMB_A,THUMB_A,false,false,out string toRecord,out string toRemove);
			Assert.AreEqual(THUMB_A,toRecord);
			Assert.AreEqual("",toRemove);
			Plan(THUMB_A,THUMB_A,THUMB_A,true,true,out toRecord,out toRemove);
			Assert.AreEqual(THUMB_A,toRecord);
			Assert.AreEqual("",toRemove);
			//Thumbprints are compared as the store does: spaces and case do not matter.
			Plan(THUMB_A.ToLowerInvariant(),"aa aa"+THUMB_A.Substring(4),THUMB_B,true,false,out toRecord,out toRemove);
			Assert.AreEqual(THUMB_A,toRemove);
		}

		[TestMethod]
		public void Record_IsWrittenProtected_ReadBack_AndDeleted() {
			string caCopy=DbConnectionConfig.GetOfficeCaCopyPath(Path.Combine(_folder,"program"));
			string pathRecord=ImportedCertificateRecord.GetPath(caCopy);
			Assert.AreEqual(Path.Combine(Path.GetDirectoryName(caCopy),ProgramNames.ImportedClientCertFileName),pathRecord,"Next to the office CA copy.");
			Assert.AreEqual("",ImportedCertificateRecord.Read(pathRecord),"No record: none.");
			ImportedCertificateRecord.Write(pathRecord,"aa:"+THUMB_A.Substring(2).ToLowerInvariant());
			Assert.AreEqual(THUMB_A,ImportedCertificateRecord.Read(pathRecord));
			FileSecurity fileSecurity=File.GetAccessControl(pathRecord);
			Assert.IsTrue(fileSecurity.AreAccessRulesProtected,"Nothing inherited.");
			SecurityIdentifier securityIdentifierUsers=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);
			foreach(FileSystemAccessRule fileSystemAccessRule in fileSecurity.GetAccessRules(true,true,typeof(SecurityIdentifier))) {
				if(fileSystemAccessRule.IdentityReference.Equals(securityIdentifierUsers)) {
					Assert.AreEqual(FileSystemRights.Read,fileSystemAccessRule.FileSystemRights & ~FileSystemRights.Synchronize,"Users may only read it.");
				}
			}
			ImportedCertificateRecord.Write(pathRecord,"");
			Assert.IsFalse(File.Exists(pathRecord));
			Assert.AreEqual("",ImportedCertificateRecord.Read(pathRecord));
		}

		[TestMethod]
		public void Record_ThatIsNotAThumbprint_ReadsAsNone() {
			string caCopy=DbConnectionConfig.GetOfficeCaCopyPath(Path.Combine(_folder,"program"));
			string pathRecord=ImportedCertificateRecord.GetPath(caCopy);
			ImportedCertificateRecord.Write(pathRecord,THUMB_A);
			File.WriteAllText(pathRecord,"not a thumbprint");
			Assert.AreEqual("",ImportedCertificateRecord.Read(pathRecord));
			File.WriteAllText(pathRecord,THUMB_A.Substring(0,39));
			Assert.AreEqual("",ImportedCertificateRecord.Read(pathRecord),"Not 40 hex digits.");
		}

		[TestMethod]
		public void Apply_PasswordMode_RemovesNoCertificate_AndLeavesNoRecord() {
			string config=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","opendental","User","root","Password","");
			string caCopy=DbConnectionConfig.GetOfficeCaCopyPath(Path.Combine(_folder,"program"));
			DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(new DbSettingsRequest(),config,caCopy);
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResult.ExitCode,dbSettingsResult.Message);
			Assert.IsFalse(File.Exists(ImportedCertificateRecord.GetPath(caCopy)));
			Assert.IsFalse(dbSettingsResult.Message.Contains("could not be removed"),dbSettingsResult.Message);
		}

		///<summary>RemoveImported takes the certificate out of the store and deletes its private key, so no key Users may read is left. The tool
		///works on LocalMachine\My, which needs administrator rights; the test uses a store of the current user with a name of its own
		///(ClientCertificateTestStore, never CurrentUser\My) and a synthetic certificate. ClientCertificateKeyTests does the same for each kind of
		///key (CAPI, CNG RSA, CNG ECDSA).</summary>
		[TestMethod]
		public void RemoveImported_TakesTheCertificateAndItsPrivateKeyOutOfTheStore() {
			string subject="CN=synthetic-dbsec-remove-"+Guid.NewGuid().ToString("N");
			using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore())
			using(X509Certificate2 x509Certificate2Ephemeral=TestHelpers.NewCertificate(subject,isCa:false,isClientAuth:true))
			{
				TestKey testKey=clientCertificateTestStore.AddCertificate(x509Certificate2Ephemeral);
				string thumbprint=testKey.Thumbprint;
				Assert.IsTrue(testKey.Exists(),"The persisted key is there before.");
				Assert.AreEqual("",ClientCertificateStore.RemoveImported(thumbprint,StoreLocation.CurrentUser,clientCertificateTestStore.Name));
				Assert.AreEqual(0,clientCertificateTestStore.GetThumbprints().Count,"The certificate is gone.");
				Assert.IsFalse(testKey.Exists(),"Its private key is deleted too.");
				Assert.AreEqual("",ClientCertificateStore.RemoveImported(thumbprint,StoreLocation.CurrentUser,clientCertificateTestStore.Name),
					"A certificate that is not there: nothing.");
				Assert.AreEqual("",ClientCertificateStore.RemoveImported("",StoreLocation.CurrentUser,clientCertificateTestStore.Name));
			}
		}
		#endregion The certificate the tool replaces (answer b)

		#region The rights of FreeDentalConfig.xml (answer e)
		[TestMethod]
		public void Apply_SetsTheConfigFilesRightsBackToTheFolders() {
			string config=TestHelpers.WriteConfig(_folder,"ComputerName","localhost","Database","opendental","User","root","Password","");
			//As v0.0's setup left it: an explicit Users modify right.
			SecurityIdentifier securityIdentifierUsers=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);
			FileSecurity fileSecurityBefore=File.GetAccessControl(config);
			fileSecurityBefore.AddAccessRule(new FileSystemAccessRule(securityIdentifierUsers,FileSystemRights.Modify,AccessControlType.Allow));
			File.SetAccessControl(config,fileSecurityBefore);
			Assert.IsTrue(File.GetAccessControl(config).GetAccessRules(true,false,typeof(SecurityIdentifier)).Count>0,"The explicit rule is there before.");
			DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(new DbSettingsRequest() { Database="opendental_test" },config,
				DbConnectionConfig.GetOfficeCaCopyPath(Path.Combine(_folder,"program")));
			Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResult.ExitCode,dbSettingsResult.Message);
			FileSecurity fileSecurityAfter=File.GetAccessControl(config);
			Assert.AreEqual(0,fileSecurityAfter.GetAccessRules(true,false,typeof(SecurityIdentifier)).Count,"No explicit rule is left.");
			Assert.IsFalse(fileSecurityAfter.AreAccessRulesProtected,"The folder's rules are inherited.");
			Assert.IsTrue(fileSecurityAfter.GetAccessRules(false,true,typeof(SecurityIdentifier)).Count>0,"The file has the folder's rules.");
			Assert.AreEqual("opendental_test",DbConnectionConfig.ReadFile(config).Database,"And the settings are saved.");
		}
		#endregion The rights of FreeDentalConfig.xml (answer e)

		#region Setup's exit code (answer c)
		[TestMethod]
		public void Setup_ASilentInstallWhoseConnectionStepFails_EndsWithExitCode10_AndTheGuideSaysSo() {
			string iss=TestHelpers.ReadRepoFile("installer","OpenPmsDental.iss");
			StringAssert.Contains(iss,"DbSettingsFailedExitCode = 10;");
			Match match=Regex.Match(iss,@"(?ms)^function GetCustomSetupExitCode: Integer;\r?\nbegin\r?\n(.*?)^end;");
			Assert.IsTrue(match.Success,"Setup sets its own exit code.");
			StringAssert.Contains(match.Groups[1].Value,"Result := DbSettingsFailedExitCode;");
			Match matchReport=Regex.Match(iss,@"(?ms)^procedure ReportDbSettingsFailure\(const ToolExit: Integer\);\r?\nbegin\r?\n(.*?)^end;");
			Assert.IsTrue(matchReport.Success);
			StringAssert.Contains(matchReport.Groups[1].Value,"if WizardSilent then");
			StringAssert.Contains(matchReport.Groups[1].Value,"DbSettingsFailed := True;");
			//The upgrade's /check in a silent install counts too.
			Match matchSetUp=Regex.Match(iss,@"(?ms)^procedure SetUpConnection;\r?\n(.*?)^end;");
			Assert.IsTrue(matchSetUp.Success);
			string check=matchSetUp.Groups[1].Value.Substring(matchSetUp.Groups[1].Value.IndexOf("RunDbSettings('/check', False);"));
			StringAssert.Contains(check,"DbSettingsFailed := True;");
			//Documented in the guide, the spec and setup's own notes.
			StringAssert.Contains(TestHelpers.ReadRepoFile("docs","db-connection-security.md"),"**10** means a silent setup");
			StringAssert.Contains(TestHelpers.ReadRepoFile("docs","specs","db-connection-security.md"),"exit code 10");
			StringAssert.Contains(iss,"; Exit code: Inno Setup's own (0 done, 1 to 8 setup did not finish), and 10 for a silent install");
		}
		#endregion Setup's exit code (answer c)

		#region The sql_mode stop (answer d)
		[TestMethod]
		public void SqlModeStop_NamesTheOptionFileFix_OfTheGuide() {
			string message=MiscData.GetSetSqlModeFailedMessage();
			StringAssert.Contains(message,"sql_mode=\"\"");
			StringAssert.Contains(message,"[mariadb]");
			StringAssert.Contains(message,"docs/db-connection-security.md, section 4");
			//The guide's option file has exactly that line.
			StringAssert.Contains(TestHelpers.ReadRepoFile("docs","db-connection-security.md"),"\nsql_mode=\"\"");
			//The start-up still stops: it shows the message and returns false.
			string source=TestHelpers.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDental.cs");
			int index=source.IndexOf("MiscData.SetSqlMode();");
			Assert.IsTrue(index>0);
			string afterCall=source.Substring(index,Math.Min(800,source.Length-index));
			Assert.IsTrue(Regex.IsMatch(afterCall,@"MessageBox\.Show\(MiscData\.GetSetSqlModeFailedMessage\(\)\);\s*return false;"),
				"The start-up shows the message and stops.");
		}
		#endregion The sql_mode stop (answer d)
	}
}
