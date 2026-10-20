// QuickBooks Online (direct): the office's settings and connection, kept in the QuickBooksOnline program's programproperty rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness.Qbo {
	///<summary>Which Intuit key set the office entered. It picks the Accounting API host and the redirect rules.</summary>
	public enum QboEnvironment {
		///<summary>Production keys: quickbooks.api.intuit.com. The redirect URI must be HTTPS and not localhost or an IP address.</summary>
		Production,
		///<summary>Development keys: sandbox-quickbooks.api.intuit.com. http://localhost redirects are allowed.</summary>
		Sandbox,
	}

	///<summary>A snapshot of the QuickBooksOnline program's rows (ClinicNum 0, no computer name). Missing rows read as blank, so the deposit window works
	///before anyone has opened Setup. Secrets are osd1$ (SecretValue): the client secret, the refresh token and the realm ID. The refresh token is never
	///held here, only whether a usable one is stored; QboTokenManager reads it from the database when it refreshes. Access tokens are never stored.</summary>
	public sealed class QboSettings {
		#region Property descriptions
		///<summary>"Name,Id|Name,Id" of the accounts deposits may go to. Existing row.</summary>
		public const string PropDepositAccounts="Deposit Accounts";
		///<summary>"Name,Id|Name,Id" of the income accounts. Existing row.</summary>
		public const string PropIncomeAccounts="Income Accounts";
		///<summary>"Name,Id|Name,Id" of the classes. Existing row.</summary>
		public const string PropClassRefs="Class Refs";
		///<summary>osd1$, masked, high security. Rotated by Intuit about once a day. Existing row.</summary>
		public const string PropRefreshToken="Refresh Token";
		///<summary>osd1$, masked, high security (the company ID; not secret, kept encrypted as before). Existing row.</summary>
		public const string PropRealmId="Realm ID";
		///<summary>The office's Intuit app client ID (a public identifier). New row.</summary>
		public const string PropClientId="Client ID";
		///<summary>osd1$, masked, high security. New row.</summary>
		public const string PropClientSecret="Client Secret";
		///<summary>"Production" (default) or "Sandbox". New row.</summary>
		public const string PropEnvironment="Environment";
		///<summary>Exactly as registered in the Intuit developer portal for the key set in use. New row.</summary>
		public const string PropRedirectUri="Redirect URI";
		///<summary>Blank, or the port an office-hosted relay page forwards the redirect to. New row.</summary>
		public const string PropRelayPort="Relay Port";
		///<summary>UTC ISO 8601: when the refresh token stops working unless used. New row.</summary>
		public const string PropRefreshTokenExpires="Refresh Token Expires";
		///<summary>The connected company's name, for display only. New row.</summary>
		public const string PropCompanyName="Company Name";
		#endregion

		///<summary>The QuickBooksOnline program row, or 0 if it is missing.</summary>
		public long ProgramNum;
		public bool IsEnabled;
		public QboEnvironment Environment=QboEnvironment.Production;
		public string ClientId="";
		///<summary>The plain client secret, in memory only. "" when blank or unreadable.</summary>
		public string ClientSecret="";
		///<summary>How the stored client secret read.</summary>
		public SecretReadStatus ClientSecretStatus=SecretReadStatus.Blank;
		public string RedirectUri="";
		///<summary>0 when not set.</summary>
		public int RelayPort;
		///<summary>The plain realm ID (company ID), or "".</summary>
		public string RealmId="";
		public SecretReadStatus RealmIdStatus=SecretReadStatus.Blank;
		///<summary>How the stored refresh token read. The value itself is not kept.</summary>
		public SecretReadStatus RefreshTokenStatus=SecretReadStatus.Blank;
		///<summary>True if a refresh token this installation can use is stored.</summary>
		public bool HasRefreshToken;
		public DateTime? RefreshTokenExpiresUtc;
		public string CompanyName="";
		public string DepositAccounts="";
		public string IncomeAccounts="";
		public string ClassRefs="";
		///<summary>The stored (osd1$) client secret and realm ID, for the token cache fingerprint only.</summary>
		private string _storedClientSecret="";
		private string _storedRealmId="";

		///<summary>True when the app keys and a usable connection (refresh token and realm ID) are all present. Says nothing about whether Intuit
		///still accepts them.</summary>
		public bool IsConnected {
			get {
				return ClientId!="" && ClientSecret!="" && HasRefreshToken && RealmId!="";
			}
		}

		///<summary>True when a saved connection or secret exists but can't be used here: encrypted by another installation, or a plain-text token from
		///before this integration. The setup window says so instead of looking empty.</summary>
		public bool HasUnreadableLegacyValues {
			get {
				return IsUnusable(RefreshTokenStatus,HasRefreshToken) || RealmIdStatus==SecretReadStatus.Unreadable
					|| ClientSecretStatus==SecretReadStatus.Unreadable;
			}
		}

		private static bool IsUnusable(SecretReadStatus status,bool hasValue) {
			return status==SecretReadStatus.Unreadable || (status==SecretReadStatus.Legacy && !hasValue);
		}

		///<summary>Why Send QBO can't run, for a message box. "" when IsConnected.</summary>
		public string GetNotConnectedMessage() {
			if(IsConnected) {
				return "";
			}
			if(HasUnreadableLegacyValues) {
				return QboAuthException.MessageLegacy;
			}
			return QboAuthException.MessageNotConnected;
		}

		///<summary>Identifies this configuration for the in-memory token cache: a changed client ID, secret, environment or company gets a fresh entry.
		///Built from the stored (encrypted) values, never the plain secret.</summary>
		public string GetFingerprint() {
			return TokenCache.Fingerprint(POut.Long(ProgramNum),ClientId,Environment.ToString(),_storedClientSecret,_storedRealmId);
		}

		///<summary>The program's settings from the cache. Never throws for missing rows.</summary>
		public static QboSettings Load() {
			Meth.NoCheckMiddleTierRole();
			Program program=Programs.GetCur(ProgramName.QuickBooksOnline);
			if(program==null) {
				return new QboSettings();
			}
			long programNum=program.ProgramNum;
			QboSettings settings=FromValues(desc => ProgramProperties.GetValOrDefault(programNum,desc,0));
			settings.ProgramNum=programNum;
			settings.IsEnabled=program.Enabled;
			return settings;
		}

		///<summary>Builds settings from stored values (desc to stored PropertyValue; null or "" when the row is missing). Used by Load and by tests.</summary>
		public static QboSettings FromValues(Func<string,string> funcGetStored) {
			Meth.NoCheckMiddleTierRole();
			Func<string,string> funcGet=desc => (funcGetStored(desc)??"");
			QboSettings settings=new QboSettings();
			settings.Environment=ParseEnvironment(funcGet(PropEnvironment));
			settings.ClientId=funcGet(PropClientId).Trim();
			settings._storedClientSecret=funcGet(PropClientSecret);
			SecretReadResult resultSecret=SecretValue.Read(settings._storedClientSecret,LegacyPolicy.AcceptPlaintext);
			settings.ClientSecretStatus=resultSecret.Status;
			settings.ClientSecret=resultSecret.PlainText;
			settings.RedirectUri=funcGet(PropRedirectUri).Trim();
			settings.RelayPort=ParsePort(funcGet(PropRelayPort));
			//The realm ID is a company ID, not a secret: a plain value is accepted. Open Dental's ciphertexts are not readable here.
			settings._storedRealmId=funcGet(PropRealmId);
			SecretReadResult resultRealm=SecretValue.Read(settings._storedRealmId,LegacyPolicy.AcceptPlaintext);
			settings.RealmIdStatus=resultRealm.Status;
			settings.RealmId=resultRealm.PlainText.Trim();
			//Refresh tokens issued under Open Dental's app are never sent, whatever their form.
			SecretReadResult resultRefresh=SecretValue.Read(funcGet(PropRefreshToken),LegacyPolicy.RejectPlaintext);
			settings.RefreshTokenStatus=resultRefresh.Status;
			settings.HasRefreshToken=resultRefresh.HasValue;
			settings.RefreshTokenExpiresUtc=ParseUtc(funcGet(PropRefreshTokenExpires));
			settings.CompanyName=funcGet(PropCompanyName);
			settings.DepositAccounts=funcGet(PropDepositAccounts);
			settings.IncomeAccounts=funcGet(PropIncomeAccounts);
			settings.ClassRefs=funcGet(PropClassRefs);
			return settings;
		}

		public static QboEnvironment ParseEnvironment(string value) {
			if(string.Equals((value??"").Trim(),nameof(QboEnvironment.Sandbox),StringComparison.OrdinalIgnoreCase)) {
				return QboEnvironment.Sandbox;
			}
			return QboEnvironment.Production;
		}

		///<summary>1-65535, or 0 for blank or invalid text.</summary>
		public static int ParsePort(string value) {
			int port;
			if(int.TryParse((value??"").Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out port) && port>0 && port<=65535) {
				return port;
			}
			return 0;
		}

		public static DateTime? ParseUtc(string value) {
			DateTime dateTime;
			if(DateTime.TryParse(value??"",CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out dateTime)) {
				return DateTime.SpecifyKind(dateTime,DateTimeKind.Utc);
			}
			return null;
		}

		public static string FormatUtc(DateTime utc) {
			return DateTime.SpecifyKind(utc,DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ",CultureInfo.InvariantCulture);
		}

		#region Writing
		///<summary>Inserts any missing rows (blank, or "Production" for Environment); secret rows masked and high security. Idempotent. Returns true if
		///a row was missing from the cache, so the caller refreshes the cache and signals InvalidType.Programs. Never calls Intuit.</summary>
		public static bool EnsurePropertyRows() {
			Meth.NoCheckMiddleTierRole();
			Program program=Programs.GetCur(ProgramName.QuickBooksOnline);
			if(program==null) {
				return false;
			}
			long programNum=program.ProgramNum;
			bool isAnyMissing=false;
			List<string> listSettings=new List<string> { PropDepositAccounts,PropIncomeAccounts,PropClassRefs,PropClientId,PropEnvironment,PropRedirectUri,
				PropRelayPort,PropRefreshTokenExpires,PropCompanyName };
			for(int i=0;i<listSettings.Count;i++) {
				ProgramProperty programProperty=ProgramProperties.GetPropForProgByDesc(programNum,listSettings[i]);
				if(programProperty!=null) {
					continue;
				}
				isAnyMissing=true;
				string defaultValue=(listSettings[i]==PropEnvironment) ? nameof(QboEnvironment.Production) : "";
				VendorCredentials.EnsureSetting(programNum,listSettings[i],0,defaultValue);
			}
			List<string> listSecrets=new List<string> { PropRefreshToken,PropRealmId,PropClientSecret };
			for(int i=0;i<listSecrets.Count;i++) {
				ProgramProperty programProperty=ProgramProperties.GetPropForProgByDesc(programNum,listSecrets[i]);
				if(programProperty!=null && programProperty.IsMasked && programProperty.IsHighSecurity) {
					continue;
				}
				isAnyMissing=true;
				VendorCredentials.EnsureSecret(programNum,listSecrets[i]);
			}
			return isAnyMissing;
		}

		///<summary>Saves the app keys and redirect settings. clientSecretNew is null to keep the stored secret. Returns false, saving nothing, if the
		///secret could not be encrypted. Does not touch the connection; the caller clears it first when the environment or client ID changes.
		///The caller signals InvalidType.Programs.</summary>
		public static bool SaveClientSettings(QboEnvironment environment,string clientId,string redirectUri,int relayPort,string clientSecretNew) {
			Meth.NoCheckMiddleTierRole();
			long programNum=Programs.GetProgramNum(ProgramName.QuickBooksOnline);
			string storedSecret=null;
			if(clientSecretNew!=null && !SecretValue.Protect(clientSecretNew.Trim(),out storedSecret)) {
				return false;
			}
			VendorCredentials.WriteSetting(programNum,PropEnvironment,0,environment.ToString());
			VendorCredentials.WriteSetting(programNum,PropClientId,0,(clientId??"").Trim());
			VendorCredentials.WriteSetting(programNum,PropRedirectUri,0,(redirectUri??"").Trim());
			VendorCredentials.WriteSetting(programNum,PropRelayPort,0,(relayPort>0) ? relayPort.ToString(CultureInfo.InvariantCulture) : "");
			if(clientSecretNew!=null) {
				return VendorCredentials.WriteSecret(programNum,PropClientSecret,0,clientSecretNew.Trim());
			}
			return true;
		}

		///<summary>Saves a new connection: the encrypted refresh token and realm ID, the refresh token expiry and the company name. clearEntityLists blanks
		///the deposit accounts, income accounts and classes (another company was connected). Returns false, saving nothing, if encryption failed.
		///The caller signals InvalidType.Programs.</summary>
		public static bool SaveConnection(string refreshToken,DateTime? refreshTokenExpiresUtc,string realmId,string companyName,bool clearEntityLists) {
			Meth.NoCheckMiddleTierRole();
			string storedTest;
			if(!SecretValue.Protect(refreshToken,out storedTest) || !SecretValue.Protect(realmId,out storedTest)) {
				return false;
			}
			long programNum=Programs.GetProgramNum(ProgramName.QuickBooksOnline);
			if(!VendorCredentials.WriteSecret(programNum,PropRefreshToken,0,refreshToken)
				|| !VendorCredentials.WriteSecret(programNum,PropRealmId,0,realmId))
			{
				return false;
			}
			VendorCredentials.WriteSetting(programNum,PropRefreshTokenExpires,0,refreshTokenExpiresUtc.HasValue ? FormatUtc(refreshTokenExpiresUtc.Value) : "");
			VendorCredentials.WriteSetting(programNum,PropCompanyName,0,companyName??"");
			if(clearEntityLists) {
				SaveEntityLists("","","");
			}
			return true;
		}

		///<summary>Blanks the refresh token, realm ID, expiry and company name. The account and class lists stay. The caller signals InvalidType.Programs.</summary>
		public static void ClearConnection() {
			Meth.NoCheckMiddleTierRole();
			long programNum=Programs.GetProgramNum(ProgramName.QuickBooksOnline);
			VendorCredentials.WriteSecret(programNum,PropRefreshToken,0,"");
			VendorCredentials.WriteSecret(programNum,PropRealmId,0,"");
			VendorCredentials.WriteSetting(programNum,PropRefreshTokenExpires,0,"");
			VendorCredentials.WriteSetting(programNum,PropCompanyName,0,"");
		}

		///<summary>Saves the three "Name,Id|Name,Id" lists. The caller signals InvalidType.Programs.</summary>
		public static void SaveEntityLists(string depositAccounts,string incomeAccounts,string classRefs) {
			Meth.NoCheckMiddleTierRole();
			long programNum=Programs.GetProgramNum(ProgramName.QuickBooksOnline);
			VendorCredentials.WriteSetting(programNum,PropDepositAccounts,0,depositAccounts??"");
			VendorCredentials.WriteSetting(programNum,PropIncomeAccounts,0,incomeAccounts??"");
			VendorCredentials.WriteSetting(programNum,PropClassRefs,0,classRefs??"");
		}
		#endregion
	}
}
