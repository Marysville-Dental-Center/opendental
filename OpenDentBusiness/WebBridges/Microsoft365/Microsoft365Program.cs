// Vendor-direct Microsoft 365 email: the program link row and the office's app registration settings, kept in programproperty rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness.Microsoft365 {
	///<summary>How an interactive sign-in happens.</summary>
	public enum Microsoft365SignInMethod {
		///<summary>The default browser, with MSAL's loopback listener on http://localhost and PKCE.</summary>
		SystemBrowser,
		///<summary>Device code: the user types a code at Microsoft's verification page. For locked-down terminal servers only; the app registration
		///needs "Allow public client flows = Yes".</summary>
		DeviceCode,
	}

	///<summary>The office's Microsoft Entra app registration, as entered in Setup > Program Links > Microsoft 365 email. All values are public
	///identifiers: there is no client secret (a public client must not have one).</summary>
	public sealed class Microsoft365Config {
		///<summary>Application (client) ID, a GUID.</summary>
		public string ClientId="";
		///<summary>Directory (tenant) ID: a GUID (recommended), a verified domain, "organizations", or "common"/"consumers" with personal accounts.</summary>
		public string TenantId="";
		///<summary>Default http://localhost.</summary>
		public string RedirectUri=Microsoft365Program.DefaultRedirectUri;
		public Microsoft365SignInMethod SignInMethod=Microsoft365SignInMethod.SystemBrowser;
		public bool AllowPersonalAccounts;

		///<summary>True if the client ID and tenant are both filled in.</summary>
		public bool IsComplete {
			get {
				return !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(TenantId);
			}
		}

		///<summary>The redirect URI to give MSAL: the stored one, or http://localhost when blank.</summary>
		public string GetRedirectUriOrDefault() {
			return string.IsNullOrWhiteSpace(RedirectUri) ? Microsoft365Program.DefaultRedirectUri : RedirectUri.Trim();
		}

		///<summary>Identifies this configuration in session keys: a new client ID or tenant gives new sessions.</summary>
		public string GetKey() {
			return (ClientId??"").Trim().ToLowerInvariant()+"|"+(TenantId??"").Trim().ToLowerInvariant();
		}

		public Microsoft365Config Copy() {
			return (Microsoft365Config)MemberwiseClone();
		}
	}

	///<summary>The Microsoft365Email program link: its row (inserted at runtime, Enabled=0), its five settings, and their validation.
	///No schema change and no ConvertDatabases step. All rows use ClinicNum 0. See docs/specs/vendor-direct/microsoft-365.md, section 3.3.</summary>
	public static class Microsoft365Program {
		public const string ProgDesc="Microsoft 365 email (Microsoft Graph)";
		public const string PropertyClientId="Application (client) ID";
		public const string PropertyTenantId="Directory (tenant) ID";
		public const string PropertyRedirectUri="Redirect URI";
		public const string PropertySignInMethod="Sign-in Method";
		public const string PropertyAllowPersonalAccounts="Allow personal accounts";
		public const string DefaultRedirectUri="http://localhost";
		public const string TenantOrganizations="organizations";
		public const string TenantCommon="common";
		public const string TenantConsumers="consumers";

		///<summary>Every property this program keeps, with its default value.</summary>
		public static readonly IReadOnlyList<KeyValuePair<string,string>> ListPropertyDefaults=new List<KeyValuePair<string,string>> {
			new KeyValuePair<string,string>(PropertyClientId,""),
			new KeyValuePair<string,string>(PropertyTenantId,""),
			new KeyValuePair<string,string>(PropertyRedirectUri,DefaultRedirectUri),
			new KeyValuePair<string,string>(PropertySignInMethod,Microsoft365SignInMethod.SystemBrowser.ToString()),
			new KeyValuePair<string,string>(PropertyAllowPersonalAccounts,"0"),
		};

		///<summary>Inserts the program row (Enabled=0) and any missing property rows. Returns true if it inserted anything. Never calls Microsoft.
		///Called by Setup > Program Links each time it opens.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			bool isInserted=Programs.InsertIfMissing(ProgramName.Microsoft365Email,ProgDesc);
			if(isInserted || Programs.GetProgramNum(ProgramName.Microsoft365Email)==0) {
				Programs.RefreshCache();
			}
			long programNum=Programs.GetProgramNum(ProgramName.Microsoft365Email);
			if(programNum==0) {
				return isInserted;
			}
			Func<bool> funcIsAnyMissing=() => {
				List<ProgramProperty> listProgramProperties=ProgramProperties.GetForProgram(programNum);
				return ListPropertyDefaults.Any(x => !listProgramProperties.Any(y => y.PropertyDesc==x.Key && y.ClinicNum==0));
			};
			if(!funcIsAnyMissing()) {
				return isInserted;
			}
			ProgramProperties.RefreshCache();//The cache may just be stale (another workstation inserted the rows).
			if(!funcIsAnyMissing()) {
				return isInserted;
			}
			List<ProgramProperty> listProgramPropertiesCur=ProgramProperties.GetForProgram(programNum);
			for(int i=0;i<ListPropertyDefaults.Count;i++) {
				if(listProgramPropertiesCur.Any(x => x.PropertyDesc==ListPropertyDefaults[i].Key && x.ClinicNum==0)) {
					continue;
				}
				VendorCredentials.EnsureSetting(programNum,ListPropertyDefaults[i].Key,0,ListPropertyDefaults[i].Value);
			}
			ProgramProperties.RefreshCache();
			return true;
		}

		///<summary>The program row's key, or 0 if the row hasn't been inserted yet.</summary>
		public static long GetProgramNum() {
			Meth.NoCheckMiddleTierRole();
			return Programs.GetProgramNum(ProgramName.Microsoft365Email);
		}

		///<summary>True if the office turned the program link on.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return Programs.IsEnabled(ProgramName.Microsoft365Email);
		}

		///<summary>The settings from the cache. Missing rows read as their defaults; never throws.</summary>
		public static Microsoft365Config GetConfig() {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				return new Microsoft365Config();
			}
			Func<string,string> funcGet=desc => ProgramProperties.GetValOrDefault(programNum,desc,0)??"";
			return ConfigFromValues(funcGet(PropertyClientId),funcGet(PropertyTenantId),funcGet(PropertyRedirectUri),funcGet(PropertySignInMethod),
				funcGet(PropertyAllowPersonalAccounts));
		}

		///<summary>Builds a configuration from stored property values. Unknown sign-in methods read as SystemBrowser.</summary>
		public static Microsoft365Config ConfigFromValues(string clientId,string tenantId,string redirectUri,string signInMethod,string allowPersonal) {
			Microsoft365Config config=new Microsoft365Config();
			config.ClientId=(clientId??"").Trim();
			config.TenantId=(tenantId??"").Trim();
			config.RedirectUri=string.IsNullOrWhiteSpace(redirectUri) ? DefaultRedirectUri : redirectUri.Trim();
			Microsoft365SignInMethod method;
			if(!Enum.TryParse(signInMethod??"",true,out method) || !Enum.IsDefined(typeof(Microsoft365SignInMethod),method)) {
				method=Microsoft365SignInMethod.SystemBrowser;
			}
			config.SignInMethod=method;
			config.AllowPersonalAccounts=(allowPersonal??"").Trim()=="1";
			return config;
		}

		///<summary>Stores the settings, inserting rows as needed. Does not refresh the cache; the caller signals InvalidType.Programs.</summary>
		public static void SaveConfig(Microsoft365Config config) {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				throw new ODException("The Microsoft 365 email program link is missing. Close and reopen Program Links.");
			}
			VendorCredentials.WriteSetting(programNum,PropertyClientId,0,(config.ClientId??"").Trim());
			VendorCredentials.WriteSetting(programNum,PropertyTenantId,0,(config.TenantId??"").Trim());
			VendorCredentials.WriteSetting(programNum,PropertyRedirectUri,0,config.GetRedirectUriOrDefault());
			VendorCredentials.WriteSetting(programNum,PropertySignInMethod,0,config.SignInMethod.ToString());
			VendorCredentials.WriteSetting(programNum,PropertyAllowPersonalAccounts,0,config.AllowPersonalAccounts ? "1" : "0");
		}

		///<summary>True for a GUID, the form Entra shows the Application (client) ID in.</summary>
		public static bool IsValidClientId(string clientId) {
			Guid guid;
			return Guid.TryParse((clientId??"").Trim(),out guid) && guid!=Guid.Empty;
		}

		///<summary>True for "common" or "consumers", the authorities that let personal Microsoft accounts sign in.</summary>
		public static bool IsPersonalTenant(string tenantId) {
			string tenant=(tenantId??"").Trim();
			return tenant.Equals(TenantCommon,StringComparison.OrdinalIgnoreCase) || tenant.Equals(TenantConsumers,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>True for a tenant GUID, a domain name (contoso.com, contoso.onmicrosoft.com), or one of organizations, common, consumers.</summary>
		public static bool IsValidTenant(string tenantId) {
			string tenant=(tenantId??"").Trim();
			if(tenant=="") {
				return false;
			}
			Guid guid;
			if(Guid.TryParse(tenant,out guid)) {
				return guid!=Guid.Empty;
			}
			if(tenant.Equals(TenantOrganizations,StringComparison.OrdinalIgnoreCase) || IsPersonalTenant(tenant)) {
				return true;
			}
			return Regex.IsMatch(tenant,@"^(?=.{4,253}$)([A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$");
		}

		///<summary>True for http://localhost, optionally with a port and a path (the forms Entra accepts for a desktop loopback redirect).
		///https, other hosts, a query or a fragment are refused.</summary>
		public static bool IsValidRedirectUri(string redirectUri) {
			string value=(redirectUri??"").Trim();
			Uri uri;
			if(!Uri.TryCreate(value,UriKind.Absolute,out uri)) {
				return false;
			}
			if(uri.Scheme!=Uri.UriSchemeHttp || !uri.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			if(uri.Query!="" || uri.Fragment!="" || uri.UserInfo!="") {
				return false;
			}
			return Regex.IsMatch(value,@"^http://localhost(:\d{1,5})?(/[A-Za-z0-9\-._~/]*)?$",RegexOptions.IgnoreCase);
		}

		///<summary>Checks a configuration before it is saved. Returns one message per problem; empty if it is fine. When isEnabled is true the client
		///ID and tenant are required; otherwise only filled-in values are checked.</summary>
		public static List<string> Validate(Microsoft365Config config,bool isEnabled) {
			List<string> listErrors=new List<string>();
			string clientId=(config.ClientId??"").Trim();
			string tenantId=(config.TenantId??"").Trim();
			if(clientId=="") {
				if(isEnabled) {
					listErrors.Add("Enter the Application (client) ID.");
				}
			}
			else if(!IsValidClientId(clientId)) {
				listErrors.Add("The Application (client) ID must be a GUID, as shown on the app registration's Overview page.");
			}
			if(tenantId=="") {
				if(isEnabled) {
					listErrors.Add("Enter the Directory (tenant) ID.");
				}
			}
			else if(!IsValidTenant(tenantId)) {
				listErrors.Add("The Directory (tenant) ID must be a GUID, a verified domain, or organizations.");
			}
			else if(IsPersonalTenant(tenantId) && !config.AllowPersonalAccounts) {
				listErrors.Add("The Directory (tenant) ID common or consumers lets personal Microsoft accounts sign in. Tick Allow personal accounts to "
					+"use it, or enter the office's tenant ID.");
			}
			else if(config.AllowPersonalAccounts && !IsPersonalTenant(tenantId)) {
				listErrors.Add("Personal Microsoft accounts can sign in only with the Directory (tenant) ID common or consumers.");
			}
			if(!string.IsNullOrWhiteSpace(config.RedirectUri) && !IsValidRedirectUri(config.RedirectUri)) {
				listErrors.Add("The Redirect URI must be http://localhost, optionally with a port, matching the app registration.");
			}
			return listErrors;
		}

		///<summary>Throws Microsoft365Exception (NotConfigured) unless the program is on and its client ID and tenant are valid. No network call.
		///Returns the configuration.</summary>
		public static Microsoft365Config GetConfigOrThrow() {
			Meth.NoCheckMiddleTierRole();
			if(!IsEnabled()) {
				throw new Microsoft365Exception(Microsoft365ErrorKind.NotConfigured,Microsoft365Errors.MessageTurnedOff);
			}
			Microsoft365Config config=GetConfig();
			if(!config.IsComplete || Validate(config,true).Count>0) {
				throw new Microsoft365Exception(Microsoft365ErrorKind.NotConfigured,Microsoft365Errors.MessageNotConfigured);
			}
			return config;
		}
	}
}
