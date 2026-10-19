// NewCrop (Ensora) eRx: the office's own partner credentials and account, kept in the eRx program's programproperty rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness {
	///<summary>Which NewCrop environment the credentials belong to. Chosen in eRx Setup; never follows the build configuration.</summary>
	public enum NewCropEnvironment {
		///<summary>0. secure.newcropaccounts.com. The default.</summary>
		Production,
		///<summary>1. preproduction.newcropaccounts.com, with preproduction credentials from Ensora or the reseller.</summary>
		Preproduction,
	}

	///<summary>NewCrop's settings for this office: the partner name, account name and password Ensora (or an authorized reseller) issued, the
	///account and site IDs, and the product name Ensora accepted. They go in every click-through and SOAP call; there are no tokens. Nothing is
	///compiled in: Open Dental's own NewCrop credentials are gone from the source. The password is an osd1$ value in a masked, high-security row.
	///Rows missing from the database are inserted at runtime (InsertMissingProperties), so there is no schema change. Until every value is set,
	///IsConfigured is false and NewCrop is never contacted.</summary>
	public sealed class NewCropConfig {
		public const string PropEnvironment="NewCrop Environment";
		public const string PropPartnerName="NewCrop Partner Name";
		public const string PropAccountName="NewCrop Account Name";
		public const string PropPassword="NewCrop Password";
		public const string PropAccountId="NewCrop Account ID";
		public const string PropSiteId="NewCrop Site ID";
		public const string PropProductName="NewCrop Product Name";
		public const string DefaultSiteId="1";
		///<summary>The prefix of a generated account ID. Ensora or the reseller must accept generated IDs; if they assign one, it is typed in instead.</summary>
		public const string AccountIdPrefix="OPMS";
		///<summary>Random characters after the prefix in a generated account ID.</summary>
		public const int AccountIdRandomLength=10;
		public const string UrlRxEntryProduction="https://secure.newcropaccounts.com/interfacev7/rxentry.aspx";
		public const string UrlRxEntryPreproduction="https://preproduction.newcropaccounts.com/interfaceV7/rxentry.aspx";
		public const string UrlUpdate1Production="https://secure.newcropaccounts.com/v7/WebServices/Update1.asmx";
		public const string UrlUpdate1Preproduction="https://preproduction.newcropaccounts.com/v7/WebServices/Update1.asmx";
		///<summary>The reseller password was stored as plain text in a preference; an office may also have typed one before this version.</summary>
		public const LegacyPolicy PasswordLegacyPolicy=LegacyPolicy.AcceptPlaintext;
		private const string ACCOUNT_ID_CHARS="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

		public NewCropEnvironment Environment=NewCropEnvironment.Production;
		public string PartnerName="";
		public string AccountName="";
		///<summary>The plain password, in memory only. Blank when none is stored or the stored value doesn't decrypt.</summary>
		public string Password="";
		///<summary>How the stored password read. Unreadable means "re-enter it".</summary>
		public SecretReadStatus PasswordStatus=SecretReadStatus.Blank;
		public string AccountId="";
		public string SiteId=DefaultSiteId;
		public string ProductName=ProductIdentity.ProductName;

		///<summary>The hosts NewCrop is reached on, for VendorUrlPolicy checks and the eRx window's navigation guard.</summary>
		public static List<string> GetAllowedHosts() {
			return new List<string> { "secure.newcropaccounts.com","preproduction.newcropaccounts.com" };
		}

		///<summary>Inserts the NewCrop rows on the eRx program if they are missing (password masked and high security), then imports a reseller's
		///old preferences once. Returns true if anything may have changed, so the caller signals InvalidType.Programs. Called when eRx Setup opens.</summary>
		public static bool InsertMissingProperties() {
			Meth.NoCheckMiddleTierRole();
			long programNum=Programs.GetProgramNum(ProgramName.eRx);
			if(programNum==0) {
				return false;
			}
			VendorCredentials.EnsureSetting(programNum,PropEnvironment,0,NewCropEnvironment.Production.ToString());
			VendorCredentials.EnsureSetting(programNum,PropPartnerName,0,"");
			VendorCredentials.EnsureSetting(programNum,PropAccountName,0,"");
			VendorCredentials.EnsureSecret(programNum,PropPassword,0);
			VendorCredentials.EnsureSetting(programNum,PropAccountId,0,"");
			VendorCredentials.EnsureSetting(programNum,PropSiteId,0,DefaultSiteId);
			VendorCredentials.EnsureSetting(programNum,PropProductName,0,ProductIdentity.ProductName);
			ImportLegacyPrefs();
			return true;
		}

		///<summary>A reseller ("distributor") used to put its NewCrop credentials in the NewCropPartnerName, NewCropName and NewCropPassword preferences,
		///as plain text. If the account name preference is set and the new rows are still empty, copies them into the rows (the password encrypted)
		///and blanks the password preference. Runs once: afterwards the rows are not empty. Returns true if it imported. The NewCropAccountId preference
		///is left alone: it is shown in eRx Setup as the previous account ID and is used only if the office copies it in.</summary>
		public static bool ImportLegacyPrefs() {
			Meth.NoCheckMiddleTierRole();
			long programNum=Programs.GetProgramNum(ProgramName.eRx);
			string prefAccountName=PrefC.GetString(PrefName.NewCropName);
			if(programNum==0 || string.IsNullOrWhiteSpace(prefAccountName)) {
				return false;
			}
			ProgramProperty programPropertyAccountName=ProgramProperties.GetOneFromDb(programNum,PropAccountName,0);
			ProgramProperty programPropertyPassword=ProgramProperties.GetOneFromDb(programNum,PropPassword,0);
			if((programPropertyAccountName?.PropertyValue??"")!="" || (programPropertyPassword?.PropertyValue??"")!="") {
				return false;//Already configured here; never overwrite the office's own values.
			}
			string prefPassword=PrefC.GetString(PrefName.NewCropPassword);
			if(prefPassword!="" && !VendorCredentials.WriteSecret(programNum,PropPassword,0,prefPassword)) {
				return false;//Encryption failed: keep the preference rather than lose the password.
			}
			VendorCredentials.WriteSetting(programNum,PropAccountName,0,prefAccountName.Trim());
			ProgramProperty programPropertyPartner=ProgramProperties.GetOneFromDb(programNum,PropPartnerName,0);
			if((programPropertyPartner?.PropertyValue??"")=="") {
				VendorCredentials.WriteSetting(programNum,PropPartnerName,0,PrefC.GetString(PrefName.NewCropPartnerName).Trim());
			}
			if(prefPassword!="") {
				Prefs.UpdateString(PrefName.NewCropPassword,"");
				Signalods.SetInvalid(InvalidType.Prefs);
			}
			return true;
		}

		///<summary>Reads the settings from the cache and decrypts the password. Never throws.</summary>
		public static NewCropConfig Load() {
			Meth.NoCheckMiddleTierRole();
			NewCropConfig newCropConfig=new NewCropConfig();
			long programNum=Programs.GetProgramNum(ProgramName.eRx);
			newCropConfig.Environment=ParseEnvironment(ProgramProperties.GetValOrDefault(programNum,PropEnvironment,0));
			newCropConfig.PartnerName=ProgramProperties.GetValOrDefault(programNum,PropPartnerName,0).Trim();
			newCropConfig.AccountName=ProgramProperties.GetValOrDefault(programNum,PropAccountName,0).Trim();
			SecretReadResult secretReadResult=SecretValue.Read(ProgramProperties.GetValOrDefault(programNum,PropPassword,0),PasswordLegacyPolicy);
			newCropConfig.Password=secretReadResult.PlainText;
			newCropConfig.PasswordStatus=secretReadResult.Status;
			newCropConfig.AccountId=ProgramProperties.GetValOrDefault(programNum,PropAccountId,0).Trim();
			string siteId=ProgramProperties.GetValOrDefault(programNum,PropSiteId,0).Trim();
			newCropConfig.SiteId=(siteId=="") ? DefaultSiteId : siteId;
			string productName=ProgramProperties.GetValOrDefault(programNum,PropProductName,0).Trim();
			newCropConfig.ProductName=(productName=="") ? ProductIdentity.ProductName : productName;
			return newCropConfig;
		}

		///<summary>"Preproduction" (any case) is Preproduction; anything else is Production.</summary>
		public static NewCropEnvironment ParseEnvironment(string value) {
			if((value??"").Trim().Equals(NewCropEnvironment.Preproduction.ToString(),StringComparison.OrdinalIgnoreCase)) {
				return NewCropEnvironment.Preproduction;
			}
			return NewCropEnvironment.Production;
		}

		///<summary>True when every value NewCrop needs is present and the password decrypted. Until then nothing is sent to NewCrop.</summary>
		public bool IsConfigured() {
			return GetMissingItems().Count==0;
		}

		///<summary>The settings that are missing, in words for a message; empty when configured.</summary>
		public List<string> GetMissingItems() {
			List<string> listMissing=new List<string>();
			if(string.IsNullOrWhiteSpace(PartnerName)) {
				listMissing.Add(Lans.g("NewCrop","partner name"));
			}
			if(string.IsNullOrWhiteSpace(AccountName)) {
				listMissing.Add(Lans.g("NewCrop","account name"));
			}
			if(string.IsNullOrEmpty(Password)) {
				listMissing.Add(PasswordStatus==SecretReadStatus.Unreadable
					? Lans.g("NewCrop","password (the stored one can't be read; enter it again)")
					: Lans.g("NewCrop","password"));
			}
			if(string.IsNullOrWhiteSpace(AccountId)) {
				listMissing.Add(Lans.g("NewCrop","account ID"));
			}
			if(string.IsNullOrWhiteSpace(SiteId)) {
				listMissing.Add(Lans.g("NewCrop","site ID"));
			}
			if(string.IsNullOrWhiteSpace(ProductName)) {
				listMissing.Add(Lans.g("NewCrop","product name"));
			}
			return listMissing;
		}

		///<summary>"NewCrop is not configured. Missing: ... See Setup > Program Links > eRx."</summary>
		public string GetNotConfiguredMessage() {
			return Lans.g("NewCrop","NewCrop is not configured.")+" "+Lans.g("NewCrop","Missing:")+" "+string.Join(", ",GetMissingItems())+". "
				+Lans.g("NewCrop","See Setup > Program Links > eRx.");
		}

		///<summary>The click-through page for the environment. The Introspection NewCropRxEntryURL override still wins, for mock servers.</summary>
		public string GetRxEntryUrl() {
			string url=(Environment==NewCropEnvironment.Preproduction) ? UrlRxEntryPreproduction : UrlRxEntryProduction;
			return Introspection.GetOverride(Introspection.IntrospectionEntity.NewCropRxEntryURL,url);
		}

		///<summary>The Update1 SOAP service for the environment. The Introspection NewCropUpdate1URL override still wins, for mock servers.</summary>
		public string GetUpdate1Url() {
			string url=(Environment==NewCropEnvironment.Preproduction) ? UrlUpdate1Preproduction : UrlUpdate1Production;
			return Introspection.GetOverride(Introspection.IntrospectionEntity.NewCropUpdate1URL,url);
		}

		///<summary>Writes the settings. The password is encrypted; if encryption fails nothing is written for it, the old value stays, and false
		///is returned with errorMessage set. Blank Password leaves the stored password alone unless isPasswordCleared. Does not refresh the cache.</summary>
		public bool Save(bool isPasswordCleared,out string errorMessage) {
			Meth.NoCheckMiddleTierRole();
			errorMessage="";
			long programNum=Programs.GetProgramNum(ProgramName.eRx);
			if(programNum==0) {
				errorMessage=Lans.g("NewCrop","The eRx program link is missing from the database.");
				return false;
			}
			if(Password!="" || isPasswordCleared) {
				if(!VendorCredentials.WriteSecret(programNum,PropPassword,0,Password??"")) {
					errorMessage=Lans.g("NewCrop","The NewCrop password could not be encrypted, so it was not saved.");
					return false;
				}
			}
			VendorCredentials.WriteSetting(programNum,PropEnvironment,0,Environment.ToString());
			VendorCredentials.WriteSetting(programNum,PropPartnerName,0,(PartnerName??"").Trim());
			VendorCredentials.WriteSetting(programNum,PropAccountName,0,(AccountName??"").Trim());
			VendorCredentials.WriteSetting(programNum,PropAccountId,0,(AccountId??"").Trim());
			VendorCredentials.WriteSetting(programNum,PropSiteId,0,string.IsNullOrWhiteSpace(SiteId) ? DefaultSiteId : SiteId.Trim());
			VendorCredentials.WriteSetting(programNum,PropProductName,0,string.IsNullOrWhiteSpace(ProductName) ? ProductIdentity.ProductName : ProductName.Trim());
			return true;
		}

		///<summary>A new account ID, unique without any central registry: AccountIdPrefix, a dash and AccountIdRandomLength uppercase letters and
		///digits from a cryptographic RNG. The length and characters must be confirmed with Ensora; an ID Ensora assigns is typed in instead.</summary>
		public static string GenerateAccountId() {
			StringBuilder stringBuilder=new StringBuilder(AccountIdPrefix+"-");
			byte[] arrayBytes=new byte[1];
			using(RNGCryptoServiceProvider rng=new RNGCryptoServiceProvider()) {
				int count=0;
				while(count<AccountIdRandomLength) {
					rng.GetBytes(arrayBytes);
					//252 is the largest multiple of 36 below 256: rejecting the rest keeps every character equally likely.
					if(arrayBytes[0]>=252) {
						continue;
					}
					stringBuilder.Append(ACCOUNT_ID_CHARS[arrayBytes[0]%ACCOUNT_ID_CHARS.Length]);
					count++;
				}
			}
			return stringBuilder.ToString();
		}

		///<summary>The credentials for an Update1 SOAP call.</summary>
		public NewCropWebService.Credentials GetSoapCredentials() {
			NewCropWebService.Credentials credentials=new NewCropWebService.Credentials();
			credentials.PartnerName=PartnerName;
			credentials.Name=AccountName;
			credentials.Password=Password;
			return credentials;
		}

		///<summary>The account for an Update1 SOAP call.</summary>
		public NewCropWebService.AccountRequest GetSoapAccountRequest() {
			NewCropWebService.AccountRequest accountRequest=new NewCropWebService.AccountRequest();
			accountRequest.AccountId=AccountId;
			accountRequest.SiteId=SiteId;
			return accountRequest;
		}

		///<summary>An Update1 client pointed at the configured environment, after checking the address (never an Open Dental host; HTTPS only).</summary>
		public NewCropWebService.Update1 CreateUpdate1Client(int timeoutMs) {
			string url=GetUpdate1Url();
			VendorUrlProblem vendorUrlProblem=VendorUrlPolicy.Check(url);
			if(vendorUrlProblem!=VendorUrlProblem.None) {
				throw new ODException(Lans.g("NewCrop","The NewCrop address was refused:")+" "+VendorUrlPolicy.Describe(vendorUrlProblem));
			}
			NewCropWebService.Update1 update1=new NewCropWebService.Update1();
			update1.Url=url;
			update1.Timeout=timeoutMs;
			update1.UserAgent=ProductIdentity.UserAgent;
			return update1;
		}

		///<summary>Calls GetAccountStatusV2 with the saved settings, for eRx Setup's Test connection. Returns true if NewCrop answered OK; message holds
		///NewCrop's status and message, never the password. Which userType and userId a pure credential check should send must be confirmed in the
		///partner documentation; this sends Staff and the current user's number.</summary>
		public bool TestConnection(out string message) {
			Meth.NoCheckMiddleTierRole();
			if(!IsConfigured()) {
				message=GetNotConfiguredMessage();
				return false;
			}
			try {
				using(NewCropWebService.Update1 update1=CreateUpdate1Client(15000)) {
					string userId=(Security.CurUser==null) ? "0" : POut.Long(Security.CurUser.UserNum);
					NewCropWebService.AccountStatusDetailV2Result result=update1.GetAccountStatusV2(GetSoapCredentials(),GetSoapAccountRequest(),"0","Staff",userId);
					NewCropWebService.Result resultStatus=result?.result;
					if(resultStatus==null) {
						message=Lans.g("NewCrop","NewCrop sent no status.");
						return false;
					}
					message=Lans.g("NewCrop","NewCrop status:")+" "+resultStatus.Status+(string.IsNullOrWhiteSpace(resultStatus.Message) ? "" : ". "+resultStatus.Message);
					return resultStatus.Status==NewCropWebService.StatusType.OK;
				}
			}
			catch(Exception ex) {
				message=Lans.g("NewCrop","NewCrop could not be reached:")+" "+ex.GetType().Name;
				return false;
			}
		}

		#region Provider gate
		///<summary>True if eRx Setup has enabled the provider with this NPI for NewCrop (providererx row, ErxType NewCrop, IsEnabled Enabled). This
		///replaces Open Dental's server-side approval; it also keeps an office from being billed for a prescriber it did not mean to enable, because
		///NewCrop bills per distinct prescriber ID.</summary>
		public static bool IsProviderEnabled(string npi) {
			Meth.NoCheckMiddleTierRole();
			ProviderErx providerErx=ProviderErxs.GetOneForNpiAndOption(NormalizeNpi(npi),ErxOption.NewCrop);
			return providerErx!=null && providerErx.IsEnabled==ErxStatus.Enabled;
		}

		///<summary>Inserts a Disabled NewCrop providererx row for the NPI if none exists, so eRx Setup lists the provider. No network call.
		///Returns the row, or null for an NPI that isn't 10 digits.</summary>
		public static ProviderErx EnsureProviderErx(string npi) {
			Meth.NoCheckMiddleTierRole();
			npi=NormalizeNpi(npi);
			if(npi.Length!=10) {
				return null;
			}
			ProviderErx providerErx=ProviderErxs.GetOneForNpiAndOption(npi,ErxOption.NewCrop);
			if(providerErx==null) {
				//First refresh the cache to verify the row was not added by another workstation within the last signal interval.
				ProviderErxs.RefreshCache();
				providerErx=ProviderErxs.GetOneForNpiAndOption(npi,ErxOption.NewCrop);
			}
			if(providerErx!=null) {
				return providerErx;
			}
			providerErx=new ProviderErx();
			providerErx.PatNum=0;
			providerErx.NationalProviderID=npi;
			providerErx.IsEnabled=ErxStatus.Disabled;
			providerErx.IsIdentifyProofed=false;
			providerErx.IsSentToHq=false;
			providerErx.ErxType=ErxOption.NewCrop;
			providerErx.UserId="";
			providerErx.AccountId="";
			ProviderErxs.Insert(providerErx);
			Signalods.SetInvalid(InvalidType.ProviderErxs);
			ProviderErxs.RefreshCache();
			return providerErx;
		}

		///<summary>Enables or disables the provider with this NPI for NewCrop, inserting the row if needed. The caller checks the SecurityAdmin
		///permission. Returns true if the row changed.</summary>
		public static bool SetProviderEnabled(string npi,bool isEnabled) {
			Meth.NoCheckMiddleTierRole();
			ProviderErx providerErx=EnsureProviderErx(npi);
			if(providerErx==null) {
				return false;
			}
			ProviderErx providerErxOld=providerErx.Clone();
			providerErx.IsEnabled=isEnabled ? ErxStatus.Enabled : ErxStatus.Disabled;
			if(!ProviderErxs.Update(providerErx,providerErxOld)) {
				return false;
			}
			Signalods.SetInvalid(InvalidType.ProviderErxs);
			ProviderErxs.RefreshCache();
			return true;
		}

		///<summary>The NPI with everything but digits removed.</summary>
		public static string NormalizeNpi(string npi) {
			return new string((npi??"").Where(x => x>='0' && x<='9').ToArray());
		}
		#endregion Provider gate
	}
}
