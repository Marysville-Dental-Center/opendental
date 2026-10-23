// Domain (Active Directory) login in the change-user prompts (Open Dental 25.2, feature v25_2-security-domain-login-change-user).
// The directory is a fake; the Open Dental users live in a scratch database with synthetic rows.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace SignatureTests {
	///<summary>A directory that knows one account and password, and records every call.</summary>
	public class FakeDomainDirectory:IDomainDirectory {
		public string Account="jsmith";
		public string Password="Correct-Horse-7";
		public string Guid="5f0c2a7e-1111-4c3b-9d2e-000000000001";
		public bool DoThrow;
		public List<string> ListCalls=new List<string>();

		public bool ValidateCredentials(string ldapPath,string accountName,string password) {
			ListCalls.Add("validate:"+ldapPath+":"+accountName);
			if(DoThrow) {
				throw new InvalidOperationException("server down");
			}
			return string.Equals(accountName,Account,StringComparison.OrdinalIgnoreCase) && password==Password;
		}

		public string GetDomainObjectGuid(string ldapPath) {
			ListCalls.Add("guid:"+ldapPath);
			if(DoThrow) {
				throw new InvalidOperationException("server down");
			}
			return Guid;
		}
	}

	[TestClass]
	public class DomainLoginParsingTests {
		[TestMethod]
		public void NormalizeAccountName_StripsDomainPrefixAndUpnSuffix() {
			Assert.AreEqual("jsmith",DomainLogin.NormalizeAccountName("jsmith"));
			Assert.AreEqual("jsmith",DomainLogin.NormalizeAccountName(@"EXAMPLE\jsmith"));
			Assert.AreEqual("jsmith",DomainLogin.NormalizeAccountName("jsmith@example.local"));
			Assert.AreEqual("jsmith",DomainLogin.NormalizeAccountName("  jsmith  "));
			Assert.AreEqual("",DomainLogin.NormalizeAccountName(null));
			Assert.AreEqual("",DomainLogin.NormalizeAccountName(@"EXAMPLE\"));
		}

		[TestMethod]
		public void BuildDomainUser_UsesTheFormatThatUserEditStores() {
			Assert.AreEqual(@"abc-123\jsmith",DomainLogin.BuildDomainUser("abc-123","jsmith"));
		}

		[TestMethod]
		public void TryGetPrincipalContextArgs_DcComponentsBecomeTheDomainName() {
			Assert.IsTrue(DomainLogin.TryGetPrincipalContextArgs("LDAP://DC=example,DC=local",out string name,out string container));
			Assert.AreEqual("example.local",name);
			Assert.AreEqual("DC=example,DC=local",container);
		}

		[TestMethod]
		public void TryGetPrincipalContextArgs_ServerAndContainer() {
			Assert.IsTrue(DomainLogin.TryGetPrincipalContextArgs("ldap://dc1.example.local/OU=Staff,DC=example,DC=local",out string name,out string container));
			Assert.AreEqual("dc1.example.local",name);
			Assert.AreEqual("OU=Staff,DC=example,DC=local",container);
		}

		[TestMethod]
		public void TryGetPrincipalContextArgs_ServerOnly() {
			Assert.IsTrue(DomainLogin.TryGetPrincipalContextArgs("LDAP://example.local",out string name,out string container));
			Assert.AreEqual("example.local",name);
			Assert.AreEqual("",container);
		}

		[TestMethod]
		public void TryGetPrincipalContextArgs_RefusesPathsWithoutADomain() {
			//Without a domain name the check could fall back to the local machine, which the spec forbids.
			Assert.IsFalse(DomainLogin.TryGetPrincipalContextArgs("",out _,out _));
			Assert.IsFalse(DomainLogin.TryGetPrincipalContextArgs(null,out _,out _));
			Assert.IsFalse(DomainLogin.TryGetPrincipalContextArgs("LDAP://",out _,out _));
			Assert.IsFalse(DomainLogin.TryGetPrincipalContextArgs("LDAP://OU=Staff",out _,out _));
			Assert.IsFalse(DomainLogin.TryGetPrincipalContextArgs("GC://DC=example,DC=local",out _,out _));
		}

		[TestMethod]
		public void SettingsUsable_NeedsEnabledAndAPath() {
			Assert.IsFalse(new DomainLoginSettings { IsEnabled=false,LdapPath="LDAP://DC=example,DC=local" }.IsUsable);
			Assert.IsFalse(new DomainLoginSettings { IsEnabled=true,LdapPath="  " }.IsUsable);
			Assert.IsTrue(new DomainLoginSettings { IsEnabled=true,LdapPath="LDAP://DC=example,DC=local" }.IsUsable);
		}

		[TestMethod]
		public void SwitchLogText_SaysWhenTheDomainCheckedTheCredentials() {
			Userod userodFrom=new Userod { UserName="assistant1" };
			Userod userodTo=new Userod { UserName="drjones" };
			string textDomain=DomainLogin.GetSwitchLogText("Procedure note",userodFrom,userodTo,true);
			StringAssert.Contains(textDomain,"assistant1");
			StringAssert.Contains(textDomain,"drjones");
			StringAssert.Contains(textDomain,"(domain authentication)");
			Assert.IsFalse(DomainLogin.GetSwitchLogText("Procedure note",userodFrom,userodTo,false).Contains("domain"));
		}
	}

	[TestClass]
	public class DomainLoginSwitchTests {
		private const string GUID="5f0c2a7e-1111-4c3b-9d2e-000000000001";
		private const string PATH="LDAP://DC=example,DC=local";
		private FakeDomainDirectory _directory;
		private DomainLoginSettings _settings;

		[TestInitialize]
		public void TestInitialize() {
			ScratchDb.RequireDb();
			_directory=new FakeDomainDirectory();
			_settings=new DomainLoginSettings { IsEnabled=true,LdapPath=PATH,DomainObjectGuid=GUID };
		}

		private static string DomainUser(string account) {
			return GUID+"\\"+account;
		}

		[TestMethod]
		public void NotEnabled_NeverCallsTheDirectory() {
			ScratchDb.InsertUser("drjones",provNum:3,domainUser:DomainUser("jsmith"));
			_settings.IsEnabled=false;
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith",_directory.Password,_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.NotEnabled,result.Status);
			Assert.AreEqual(0,_directory.ListCalls.Count);
		}

		[TestMethod]
		public void BlankPassword_IsRefusedWithoutAnAnonymousBind() {
			ScratchDb.InsertUser("drjones",provNum:3,domainUser:DomainUser("jsmith"));
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith","",_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.InvalidCredentials,result.Status);
			Assert.AreEqual(0,_directory.ListCalls.Count);
		}

		[TestMethod]
		public void GoodCredentials_OneLinkedUser() {
			long userNum=ScratchDb.InsertUser("drjones",provNum:3,domainUser:DomainUser("jsmith"));
			ScratchDb.InsertUser("other",domainUser:DomainUser("someoneelse"));
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch(@"EXAMPLE\JSmith",_directory.Password,_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.Success,result.Status,result.Message);
			Assert.AreEqual(1,result.ListUserods.Count);
			Assert.AreEqual(userNum,result.ListUserods[0].UserNum);
			Assert.AreEqual(DomainUser("JSmith"),result.DomainUser);
			CollectionAssert.Contains(_directory.ListCalls,"validate:"+PATH+":JSmith");
			Assert.AreEqual("",result.DomainObjectGuidDiscovered,"The guid preference was already set.");
		}

		[TestMethod]
		public void GoodCredentials_SeveralLinkedUsers_ForAPickList_HiddenUsersLeftOut() {
			ScratchDb.InsertUser("zeta",provNum:4,domainUser:DomainUser("jsmith"));
			ScratchDb.InsertUser("alpha",provNum:5,domainUser:DomainUser("jsmith"));
			ScratchDb.InsertUser("hiddenone",provNum:6,domainUser:DomainUser("jsmith"),isHidden:true);
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith",_directory.Password,_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.Success,result.Status,result.Message);
			CollectionAssert.AreEqual(new[] { "alpha","zeta" },result.ListUserods.Select(x => x.UserName).ToArray());
		}

		[TestMethod]
		public void GoodCredentials_NoLinkedUser() {
			ScratchDb.InsertUser("drjones",provNum:3,domainUser:DomainUser("someoneelse"));
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith",_directory.Password,_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.NoLinkedUser,result.Status);
			Assert.AreEqual(0,result.ListUserods.Count);
		}

		[TestMethod]
		public void LinkUsesTheDomainGuid_NotJustTheAccountName() {
			//Same account name under another domain's guid must not match.
			ScratchDb.InsertUser("drjones",provNum:3,domainUser:"99999999-0000-0000-0000-000000000000\\jsmith");
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith",_directory.Password,_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.NoLinkedUser,result.Status);
		}

		[TestMethod]
		public void WrongPassword_CountsTowardTheLockout() {
			long userNum=ScratchDb.InsertUser("drjones",provNum:3,domainUser:DomainUser("jsmith"));
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith","wrong",_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.InvalidCredentials,result.Status);
			Userod userod=Userods.GetUserNoCache(userNum);
			Assert.AreEqual(1,userod.FailedAttempts);
			Assert.IsTrue(userod.DateTFail.Year>1880);
		}

		[TestMethod]
		public void FiveFailures_LockTheAccount_AndTheDirectoryIsNotAskedAgain() {
			long userNum=ScratchDb.InsertUser("drjones",provNum:3,domainUser:DomainUser("jsmith"));
			for(int i=0;i<5;i++) {
				Assert.AreEqual(DomainSwitchStatus.InvalidCredentials,DomainLogin.AuthenticateForSwitch("jsmith","wrong",_settings,_directory).Status);
			}
			Assert.AreEqual(5,Userods.GetUserNoCache(userNum).FailedAttempts);
			_directory.ListCalls.Clear();
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith",_directory.Password,_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.LockedOut,result.Status);
			Assert.AreEqual(0,_directory.ListCalls.Count,"A locked account is refused before the domain is asked.");
		}

		[TestMethod]
		public void OldFailures_AreForgottenOnAGoodLogOn() {
			DateTime dateTimeNow=MiscData.GetNowDateTime();
			long userNum=ScratchDb.InsertUser("drjones",provNum:3,domainUser:DomainUser("jsmith"),failedAttempts:5,dateTFail:dateTimeNow.AddMinutes(-10));
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith",_directory.Password,_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.Success,result.Status,result.Message);
			Userod userod=Userods.GetUserNoCache(userNum);
			Assert.AreEqual(0,userod.FailedAttempts);
			Assert.AreEqual(1,userod.DateTFail.Year);
		}

		[TestMethod]
		public void OnlyTheUnlockedUsersAreOffered() {
			DateTime dateTimeNow=MiscData.GetNowDateTime();
			ScratchDb.InsertUser("locked",provNum:3,domainUser:DomainUser("jsmith"),failedAttempts:5,dateTFail:dateTimeNow.AddMinutes(-1));
			long userNumOpen=ScratchDb.InsertUser("open",provNum:4,domainUser:DomainUser("jsmith"));
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith",_directory.Password,_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.Success,result.Status,result.Message);
			CollectionAssert.AreEqual(new[] { userNumOpen },result.ListUserods.Select(x => x.UserNum).ToArray());
		}

		[TestMethod]
		public void MissingGuidPreference_IsReadFromTheDirectory_ForTheCallerToSave() {
			ScratchDb.InsertUser("drjones",provNum:3,domainUser:DomainUser("jsmith"));
			_settings.DomainObjectGuid="";
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith",_directory.Password,_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.Success,result.Status,result.Message);
			Assert.AreEqual(GUID,result.DomainObjectGuidDiscovered);
			CollectionAssert.Contains(_directory.ListCalls,"guid:"+PATH);
		}

		[TestMethod]
		public void DirectoryDown_IsAnErrorNotALogOn() {
			long userNum=ScratchDb.InsertUser("drjones",provNum:3,domainUser:DomainUser("jsmith"));
			_directory.DoThrow=true;
			DomainSwitchResult result=DomainLogin.AuthenticateForSwitch("jsmith",_directory.Password,_settings,_directory);
			Assert.AreEqual(DomainSwitchStatus.DirectoryError,result.Status);
			Assert.AreEqual(0,result.ListUserods.Count);
			Assert.AreEqual(0,Userods.GetUserNoCache(userNum).FailedAttempts,"An unreachable domain is not a failed password.");
		}

		[TestMethod]
		public void ThePasswordIsNeverStored() {
			ScratchDb.InsertUser("drjones",provNum:3,domainUser:DomainUser("jsmith"));
			DomainLogin.AuthenticateForSwitch("jsmith",_directory.Password,_settings,_directory);
			DomainLogin.AuthenticateForSwitch("jsmith","Wrong-Secret-9",_settings,_directory);
			DataTable table=DataCore.GetTable("SELECT * FROM userod");
			foreach(DataRow row in table.Rows) {
				foreach(DataColumn column in table.Columns) {
					string value=row[column].ToString();
					Assert.IsFalse(value.Contains(_directory.Password),"userod."+column.ColumnName+" holds the domain password.");
					Assert.IsFalse(value.Contains("Wrong-Secret-9"),"userod."+column.ColumnName+" holds a typed password.");
				}
			}
		}
	}
}
