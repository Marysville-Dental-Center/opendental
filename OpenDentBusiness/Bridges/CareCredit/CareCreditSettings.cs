// CareCredit (Synchrony) patient financing: the office's settings and credentials, kept in the CareCredit program's programproperty rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness.Bridges {
	///<summary>Reads and writes the CareCredit program's settings. Missing rows are inserted at runtime (EnsureRows), so there is no schema change and
	///no ConvertDatabases step. The client secret is stored as osd1$ through SecretValue, in a masked, high-security row. Nothing here contacts
	///Synchrony. Endpoint paths, the session parameter and the token request style come from Synchrony's gated implementation guide, so they are
	///settings the office enters, not values in this source.</summary>
	public static class CareCreditSettings {
		#region Property names
		//New rows (practice-wide, ClinicNum 0). The existing names stay in ProgramProperties.PropertyDescs.CareCredit.
		public const string ClientId="CareCreditClientId";
		public const string ClientSecret="CareCreditClientSecret";
		public const string Environment="CareCreditEnvironment";
		public const string ApiBaseUrl="CareCreditApiBaseUrl";
		public const string IhsBaseUrl="CareCreditIhsBaseUrl";
		public const string TokenPath="CareCreditTokenPath";
		public const string PrefillPath="CareCreditPrefillPath";
		public const string PullbackPath="CareCreditPullbackPath";
		public const string BatchPath="CareCreditBatchPath";
		public const string SessionParam="CareCreditSessionParam";
		public const string ClientAuth="CareCreditClientAuth";
		public const string TokenScope="CareCreditTokenScope";
		public const string JobComputerName="CareCreditJobComputerName";
		public const string BatchLastRunDate="CareCreditBatchLastRunDate";
		public const string LastAuthError="CareCreditLastAuthError";
		///<summary>Fingerprint of the connection settings at the last passing Test connection. The program can't be enabled, and the background jobs
		///don't run, unless it matches the saved settings.</summary>
		public const string VerifiedConfig="CareCreditVerifiedConfig";
		#endregion Property names

		///<summary>Open Dental Software's own partner code, seeded by ConvertDatabases8. Sending it would attribute this office's traffic to Open
		///Dental, so it counts as "not set" and can't be saved.</summary>
		public const string PARTNER_CODE_REFUSED="1065";
		///<summary>Placeholder for the pullback path: replaced by the escaped reference ID.</summary>
		public const string REF_ID_PLACEHOLDER="{refId}";
		///<summary>Default batch process time of day (the time Open Dental's manual gives).</summary>
		public static readonly TimeSpan DefaultProcessTime=new TimeSpan(7,20,0);
		///<summary>Default first batch pullback time of day.</summary>
		public static readonly TimeSpan DefaultPullbackTime=new TimeSpan(7,50,0);
		///<summary>Prefs hold a DateTime; only the time of day is used. Times are stored on this date so midnight isn't mistaken for "never set".</summary>
		private static readonly DateTime _datePrefBase=new DateTime(2000,1,1);

		///<summary>Rows that the background jobs and Test connection write. A setup window saving its whole list must reload these from the database
		///first, so it doesn't put back a stale value.</summary>
		public static List<string> GetJobManagedDescs() {
			return new List<string> { BatchLastRunDate,LastAuthError,ProgramProperties.PropertyDescs.CareCredit.CareCreditOAuthToken };
		}

		///<summary>Inserts the practice-wide rows this integration reads, if missing (ClinicNum 0). Existing values are never overwritten. Returns true
		///if anything was inserted; the caller refreshes the program property cache. The clinic rows (merchant number, batch mode, payment type, batch
		///days) are added by the setup window as before.</summary>
		public static bool EnsureRows(long programNum) {
			Meth.NoCheckMiddleTierRole();
			if(programNum==0) {
				return false;
			}
			List<ProgramProperty> listBefore=ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.ClinicNum==0);
			Dictionary<string,string> dictDefaults=new Dictionary<string,string> {
				{ ClientId,"" },
				{ Environment,CareCreditEnvironment.Sandbox.ToString() },
				{ ApiBaseUrl,"" },
				{ IhsBaseUrl,"" },
				{ TokenPath,"" },
				{ PrefillPath,"" },
				{ PullbackPath,"" },
				{ BatchPath,"" },
				{ SessionParam,"" },
				{ ClientAuth,CareCreditClientAuth.BasicHeader.ToString() },
				{ TokenScope,"" },
				{ JobComputerName,"" },
				{ BatchLastRunDate,"" },
				{ LastAuthError,"" },
				{ VerifiedConfig,"" },
				{ ProgramProperties.PropertyDescs.CareCredit.CareCreditPartnerCode,"" },
				{ ProgramProperties.PropertyDescs.CareCredit.CareCreditIsMerchantNumberByProv,"0" },
			};
			bool isInserted=false;
			foreach(KeyValuePair<string,string> pair in dictDefaults) {
				if(listBefore.Any(x => x.PropertyDesc==pair.Key)) {
					continue;
				}
				VendorCredentials.EnsureSetting(programNum,pair.Key,0,pair.Value);
				isInserted=true;
			}
			if(!listBefore.Any(x => x.PropertyDesc==ClientSecret && x.IsMasked && x.IsHighSecurity)) {
				VendorCredentials.EnsureSecret(programNum,ClientSecret,0);
				isInserted=true;
			}
			return isInserted;
		}

		///<summary>The CareCredit program's ProgramNum, or 0.</summary>
		public static long GetProgramNum() {
			Meth.NoCheckMiddleTierRole();
			try {
				return Programs.GetProgramNum(ProgramName.CareCredit);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return 0;
			}
		}

		///<summary>A practice-wide value from the cache, or "". Never throws.</summary>
		public static string GetPracticeValue(string desc) {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				return "";
			}
			return ProgramProperties.GetValOrDefault(programNum,desc,0);
		}

		///<summary>The saved connection settings, read from the cache, with the client secret decrypted. Never throws and never calls Synchrony.
		///Introspection overrides (CareCreditApiURL, CareCreditIHSApiURL) replace the base URLs for tests against a mock server.</summary>
		public static CareCreditConfig Load() {
			Meth.NoCheckMiddleTierRole();
			CareCreditConfig config=new CareCreditConfig();
			long programNum=GetProgramNum();
			if(programNum==0) {
				return config;
			}
			config.ClientId=ProgramProperties.GetValOrDefault(programNum,ClientId,0).Trim();
			config.ClientSecretStored=ProgramProperties.GetValOrDefault(programNum,ClientSecret,0);
			SecretReadResult secretReadResult=SecretValue.Read(config.ClientSecretStored,LegacyPolicy.RejectPlaintext);
			config.ClientSecret=secretReadResult.PlainText;
			config.IsClientSecretUnreadable=secretReadResult.NeedsReentry;
			config.Environment=ParseEnvironment(ProgramProperties.GetValOrDefault(programNum,Environment,0));
			config.ApiBaseUrl=ProgramProperties.GetValOrDefault(programNum,ApiBaseUrl,0).Trim();
			config.IhsBaseUrl=ProgramProperties.GetValOrDefault(programNum,IhsBaseUrl,0).Trim();
			config.TokenPath=ProgramProperties.GetValOrDefault(programNum,TokenPath,0).Trim();
			config.PrefillPath=ProgramProperties.GetValOrDefault(programNum,PrefillPath,0).Trim();
			config.PullbackPath=ProgramProperties.GetValOrDefault(programNum,PullbackPath,0).Trim();
			config.BatchPath=ProgramProperties.GetValOrDefault(programNum,BatchPath,0).Trim();
			config.SessionParam=ProgramProperties.GetValOrDefault(programNum,SessionParam,0).Trim();
			config.ClientAuth=ParseClientAuth(ProgramProperties.GetValOrDefault(programNum,ClientAuth,0));
			config.Scope=ProgramProperties.GetValOrDefault(programNum,TokenScope,0).Trim();
			config.PartnerCode=ProgramProperties.GetValOrDefault(programNum,ProgramProperties.PropertyDescs.CareCredit.CareCreditPartnerCode,0).Trim();
			config.VerifiedFingerprint=ProgramProperties.GetValOrDefault(programNum,VerifiedConfig,0);
			string urlOverride;
			if(TryGetIntrospectionOverride(Introspection.IntrospectionEntity.CareCreditApiURL,out urlOverride)) {
				config.ApiBaseUrl=urlOverride;
			}
			if(TryGetIntrospectionOverride(Introspection.IntrospectionEntity.CareCreditIHSApiURL,out urlOverride)) {
				config.IhsBaseUrl=urlOverride;
			}
			return config;
		}

		private static bool TryGetIntrospectionOverride(Introspection.IntrospectionEntity entity,out string value) {
			value="";
			try {
				return Introspection.TryGetOverride(entity,out value) && !string.IsNullOrWhiteSpace(value);
			}
			catch(Exception ex) {
				ex.DoNothing();
				value="";
				return false;
			}
		}

		public static CareCreditEnvironment ParseEnvironment(string value) {
			CareCreditEnvironment environment;
			if(Enum.TryParse(value??"",true,out environment) && Enum.IsDefined(typeof(CareCreditEnvironment),environment)) {
				return environment;
			}
			return CareCreditEnvironment.Sandbox;
		}

		public static CareCreditClientAuth ParseClientAuth(string value) {
			CareCreditClientAuth clientAuth;
			if(Enum.TryParse(value??"",true,out clientAuth) && Enum.IsDefined(typeof(CareCreditClientAuth),clientAuth)) {
				return clientAuth;
			}
			return CareCreditClientAuth.BasicHeader;
		}

		///<summary>The computer chosen to run batch QuickScreen and result polling, or "".</summary>
		public static string GetJobComputerName() {
			return GetPracticeValue(JobComputerName).Trim();
		}

		///<summary>True if this computer is the one chosen to run the background tasks.</summary>
		public static bool IsThisComputerJobHost() {
			string name=GetJobComputerName();
			return name!="" && string.Equals(name,ODEnvironment.MachineName,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Time of day to submit batch QuickScreen. Default 07:20.</summary>
		public static TimeSpan GetProcessTime() {
			return GetTimeOfDayPref(PrefName.CareCreditBatchProcessDateTime,DefaultProcessTime);
		}

		///<summary>Time of day of the first batch pullback. Default 07:50.</summary>
		public static TimeSpan GetPullbackTime() {
			return GetTimeOfDayPref(PrefName.CareCreditBatchPullbackDateTime,DefaultPullbackTime);
		}

		private static TimeSpan GetTimeOfDayPref(PrefName prefName,TimeSpan timeDefault) {
			try {
				DateTime dateTime=PrefC.GetDateT(prefName);
				if(dateTime.Year<1880) {
					return timeDefault;//Never set: Open Dental's HQ used to set it.
				}
				return dateTime.TimeOfDay;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return timeDefault;
			}
		}

		///<summary>The pref value for a time of day.</summary>
		public static DateTime GetPrefValueForTime(TimeSpan timeOfDay) {
			return _datePrefBase.Add(new TimeSpan(timeOfDay.Hours,timeOfDay.Minutes,0));
		}

		///<summary>Parses "HH:mm" (24-hour, or the culture's short time). Returns false for anything else.</summary>
		public static bool TryParseTimeOfDay(string text,out TimeSpan timeOfDay) {
			timeOfDay=TimeSpan.Zero;
			if(string.IsNullOrWhiteSpace(text)) {
				return false;
			}
			DateTime dateTime;
			string[] arrayFormats={ "H:mm","HH:mm","h:mm tt","hh:mm tt" };
			if(DateTime.TryParseExact(text.Trim(),arrayFormats,CultureInfo.InvariantCulture,DateTimeStyles.None,out dateTime)
				|| DateTime.TryParse(text.Trim(),CultureInfo.CurrentCulture,DateTimeStyles.NoCurrentDateDefault,out dateTime))
			{
				timeOfDay=new TimeSpan(dateTime.Hour,dateTime.Minute,0);
				return true;
			}
			return false;
		}

		///<summary>Records the last authorization failure for the setup window, or clears it with "". Never throws.</summary>
		public static void SetLastAuthError(string message) {
			Meth.NoCheckMiddleTierRole();
			try {
				long programNum=GetProgramNum();
				if(programNum==0) {
					return;
				}
				string value=(message??"");
				if(value.Length>250) {
					value=value.Substring(0,250);
				}
				if(ProgramProperties.GetValOrDefault(programNum,LastAuthError,0)==value) {
					return;
				}
				VendorCredentials.WriteSetting(programNum,LastAuthError,0,value);
				Signalods.SetInvalid(InvalidType.Programs);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		///<summary>Claims today's batch run with a compare-and-swap on CareCreditBatchLastRunDate, so two hosts can't both run it. Returns true if
		///this caller claimed it. The schedule prefs keep their time of day.</summary>
		public static bool TryClaimBatchDay(DateTime date) {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				return false;
			}
			ProgramProperty programProperty=ProgramProperties.InsertIfMissing(programNum,BatchLastRunDate,0,"",false,false);
			string today=date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
			if(programProperty.PropertyValue==today) {
				return false;
			}
			return ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,programProperty.PropertyValue,today);
		}

		///<summary>The batch mode for a clinic (its row, else the practice row). Off when unreadable.</summary>
		public static EnumCareCreditBatch GetBatchMode(long clinicNum) {
			long programNum=GetProgramNum();
			if(programNum==0) {
				return EnumCareCreditBatch.Off;
			}
			string value=ProgramProperties.GetValOrDefault(programNum,ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchEnabled,clinicNum);
			int intValue;
			if(!int.TryParse(value,out intValue) || !Enum.IsDefined(typeof(EnumCareCreditBatch),intValue)) {
				return EnumCareCreditBatch.Off;
			}
			return (EnumCareCreditBatch)intValue;
		}

		///<summary>Days ahead to screen for a clinic: 2, 3 or 4 (default 2).</summary>
		public static int GetBatchDays(long clinicNum) {
			long programNum=GetProgramNum();
			int days=0;
			if(programNum!=0) {
				int.TryParse(ProgramProperties.GetValOrDefault(programNum,ProgramProperties.PropertyDescs.CareCredit.CareCreditQSBatchDays,clinicNum),out days);
			}
			if(days<2 || days>4) {
				return 2;
			}
			return days;
		}
	}

	///<summary>A snapshot of the connection settings. Built by CareCreditSettings.Load, or by the setup window from what the user typed.</summary>
	public sealed class CareCreditConfig {
		public string ClientId="";
		///<summary>Plain text, in memory only.</summary>
		public string ClientSecret="";
		///<summary>The stored osd1$ form, used in fingerprints so the plain secret never is.</summary>
		public string ClientSecretStored="";
		///<summary>True if a stored secret couldn't be decrypted (or was plain text from another program).</summary>
		public bool IsClientSecretUnreadable;
		public CareCreditEnvironment Environment=CareCreditEnvironment.Sandbox;
		public string ApiBaseUrl="";
		public string IhsBaseUrl="";
		public string TokenPath="";
		public string PrefillPath="";
		public string PullbackPath="";
		public string BatchPath="";
		public string SessionParam="";
		public CareCreditClientAuth ClientAuth=CareCreditClientAuth.BasicHeader;
		public string Scope="";
		public string PartnerCode="";
		///<summary>The saved fingerprint of the last passing Test connection.</summary>
		public string VerifiedFingerprint="";

		///<summary>What is missing or refused, as short messages for the setup window. Empty when the connection settings are complete.
		///Never calls Synchrony.</summary>
		public List<string> GetProblems() {
			List<string> listProblems=new List<string>();
			if(string.IsNullOrWhiteSpace(ClientId)) {
				listProblems.Add("Client ID is missing.");
			}
			if(IsClientSecretUnreadable) {
				listProblems.Add("The saved client secret can't be read. Enter it again.");
			}
			else if(string.IsNullOrEmpty(ClientSecret)) {
				listProblems.Add("Client secret is missing.");
			}
			AddUrlProblem(listProblems,"API base URL",ApiBaseUrl);
			AddUrlProblem(listProblems,"Hosted-page base URL",IhsBaseUrl);
			AddPathProblem(listProblems,"Token path",TokenPath,isRequired:true);
			AddPathProblem(listProblems,"Prefill path",PrefillPath,isRequired:true);
			AddPathProblem(listProblems,"Pullback path",PullbackPath,isRequired:true);
			AddPathProblem(listProblems,"Batch path",BatchPath,isRequired:false);
			string partnerCodeProblem=GetPartnerCodeProblem(PartnerCode);
			if(partnerCodeProblem!="") {
				listProblems.Add(partnerCodeProblem);
			}
			return listProblems;
		}

		///<summary>True when GetProblems finds nothing.</summary>
		public bool IsComplete {
			get {
				return GetProblems().Count==0;
			}
		}

		///<summary>True when the settings are complete and match the last passing Test connection.</summary>
		public bool IsVerified {
			get {
				return IsComplete && VerifiedFingerprint!="" && VerifiedFingerprint==GetVerificationFingerprint();
			}
		}

		///<summary>"" if the code can be saved; otherwise why not. Open Dental's code 1065 is refused.</summary>
		public static string GetPartnerCodeProblem(string partnerCode) {
			string code=(partnerCode??"").Trim();
			if(code=="") {
				return "Partner code is missing.";
			}
			if(code==CareCreditSettings.PARTNER_CODE_REFUSED) {
				return "This partner code was issued to a different software vendor. Enter the partner code issued to this office.";
			}
			return "";
		}

		///<summary>"" if the URL can be saved; otherwise why not. HTTPS only, never an Open Dental host.</summary>
		public static string GetUrlProblem(string url) {
			VendorUrlProblem problem=VendorUrlPolicy.Check((url??"").Trim());
			if(problem==VendorUrlProblem.None) {
				return "";
			}
			return VendorUrlPolicy.Describe(problem);
		}

		///<summary>"" if the path can be used under the API base URL; otherwise why not. A path is relative: no scheme, no host, no backslashes or
		///spaces. Blank is allowed when not required.</summary>
		public static string GetPathProblem(string path,bool isRequired) {
			string pathTrim=(path??"").Trim();
			if(pathTrim=="") {
				return isRequired ? "is missing." : "";
			}
			if(pathTrim.Contains("://") || pathTrim.StartsWith("//") || pathTrim.Contains("\\") || pathTrim.Any(x => char.IsWhiteSpace(x))
				|| pathTrim.Contains("?") || pathTrim.Contains("#"))
			{
				return "must be a path under the API base URL, such as /v1/example.";
			}
			return "";
		}

		private static void AddUrlProblem(List<string> listProblems,string label,string url) {
			if(string.IsNullOrWhiteSpace(url)) {
				listProblems.Add(label+" is missing.");
				return;
			}
			string problem=GetUrlProblem(url);
			if(problem!="") {
				listProblems.Add(label+": "+problem);
			}
		}

		private static void AddPathProblem(List<string> listProblems,string label,string path,bool isRequired) {
			string problem=GetPathProblem(path,isRequired);
			if(problem!="") {
				listProblems.Add(label+" "+problem);
			}
		}

		///<summary>The absolute URL of a path under the API base URL. Throws ArgumentException for a bad base or path.</summary>
		public Uri GetEndpoint(string path) {
			string pathProblem=GetPathProblem(path,isRequired:true);
			if(pathProblem!="") {
				throw new ArgumentException("CareCredit endpoint path "+pathProblem);
			}
			string urlProblem=GetUrlProblem(ApiBaseUrl);
			if(urlProblem!="") {
				throw new ArgumentException("CareCredit API base URL: "+urlProblem);
			}
			Uri uriBase=new Uri(ApiBaseUrl.Trim());
			string left=uriBase.GetLeftPart(UriPartial.Path).TrimEnd('/');
			return new Uri(left+"/"+path.Trim().TrimStart('/'));
		}

		///<summary>The host of the API base URL, or "".</summary>
		public string GetApiHost() {
			return GetHost(ApiBaseUrl);
		}

		///<summary>The host of the hosted-page base URL, or "".</summary>
		public string GetIhsHost() {
			return GetHost(IhsBaseUrl);
		}

		private static string GetHost(string url) {
			Uri uri;
			if(string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(),UriKind.Absolute,out uri)) {
				return "";
			}
			return uri.Host.ToLowerInvariant();
		}

		///<summary>True if url may be opened as a CareCredit hosted page: it passes the URL policy and its host is the hosted-page host.</summary>
		public bool IsHostedPageUrl(string url) {
			string ihsHost=GetIhsHost();
			if(ihsHost=="" || GetUrlProblem(url)!="") {
				return false;
			}
			return GetHost(url)==ihsHost;
		}

		///<summary>The settings that decide where the client secret and patient data are sent, or how the secret is presented: the API and hosted-page
		///base URLs, the endpoint paths, the client authentication style and the scope. Returns one line per setting whose value differs between
		///configSaved and configNew (old and new values; none of them is a secret), or an empty list.</summary>
		public static List<string> GetDestinationChanges(CareCreditConfig configSaved,CareCreditConfig configNew) {
			configSaved=configSaved??new CareCreditConfig();
			configNew=configNew??new CareCreditConfig();
			List<string> listChanges=new List<string>();
			AddChange(listChanges,"API base URL",configSaved.ApiBaseUrl,configNew.ApiBaseUrl);
			AddChange(listChanges,"Hosted-page base URL",configSaved.IhsBaseUrl,configNew.IhsBaseUrl);
			AddChange(listChanges,"Token path",configSaved.TokenPath,configNew.TokenPath);
			AddChange(listChanges,"Prefill path",configSaved.PrefillPath,configNew.PrefillPath);
			AddChange(listChanges,"Pullback path",configSaved.PullbackPath,configNew.PullbackPath);
			AddChange(listChanges,"Batch path",configSaved.BatchPath,configNew.BatchPath);
			AddChange(listChanges,"Client authentication",configSaved.ClientAuth.ToString(),configNew.ClientAuth.ToString());
			AddChange(listChanges,"Scope",configSaved.Scope,configNew.Scope);
			return listChanges;
		}

		private static void AddChange(List<string> listChanges,string label,string valueOld,string valueNew) {
			string old=(valueOld??"").Trim();
			string cur=(valueNew??"").Trim();
			if(string.Equals(old,cur,StringComparison.Ordinal)) {
				return;
			}
			listChanges.Add(label+": '"+old+"' to '"+cur+"'");
		}

		///<summary>Keys the in-memory token cache: a changed client, secret, environment or token endpoint gets a new token.</summary>
		public string GetTokenFingerprint() {
			return TokenCache.Fingerprint("CareCredit",ClientId,ClientSecretStored,Environment.ToString(),ApiBaseUrl,TokenPath,ClientAuth.ToString(),Scope);
		}

		///<summary>Fingerprint of every connection setting. Saved after a passing Test connection; any later change needs a new test.</summary>
		public string GetVerificationFingerprint() {
			return TokenCache.Fingerprint("CareCreditVerified",ClientId,ClientSecretStored,Environment.ToString(),ApiBaseUrl,IhsBaseUrl,TokenPath,
				PrefillPath,PullbackPath,BatchPath,SessionParam,ClientAuth.ToString(),Scope,PartnerCode);
		}
	}
}
