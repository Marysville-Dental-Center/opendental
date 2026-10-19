// PostcardMania (PCM Integrations) DirectMail API v3: the program link row, its settings and the office's own credentials.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness.PostcardMania {
	///<summary>The credentials and settings one clinic (or the practice, ClinicNum 0) uses to talk to PostcardMania. ApiSecret is the decrypted
	///value: keep this object in memory only, never log or serialize it.</summary>
	public class PostcardManiaAccount {
		///<summary>The clinic this account was asked for.</summary>
		public long ClinicNum;
		///<summary>The clinic whose rows supplied the credentials: ClinicNum itself, or 0 when the clinic uses the default.</summary>
		public long ClinicNumSource;
		public string ApiKey="";
		///<summary>Decrypted. "" if missing or unreadable.</summary>
		public string ApiSecret="";
		///<summary>Sandbox or Production. Must match the key; it only drives the "nothing will be mailed" warning.</summary>
		public string Environment=PostcardManiaProgram.EnvironmentSandbox;
		///<summary>Optional childRefNbr, at most 50 characters.</summary>
		public string ChildRefNbr="";
		///<summary>True when the stored secret could not be decrypted. It must be entered again.</summary>
		public bool IsSecretUnreadable;

		///<summary>True when both the key and the secret are present.</summary>
		public bool IsConfigured {
			get {
				return !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrEmpty(ApiSecret);
			}
		}

		public bool IsSandbox {
			get {
				return !string.Equals(Environment,PostcardManiaProgram.EnvironmentProduction,StringComparison.OrdinalIgnoreCase);
			}
		}

		///<summary>Identifies the account for the token cache without revealing the secret (the secret is only hashed).</summary>
		public string GetCacheKey() {
			return TokenCacheKey(ApiKey,ChildRefNbr,ApiSecret);
		}

		internal static string TokenCacheKey(string apiKey,string childRefNbr,string apiSecret) {
			return CodeBase.VendorDirect.TokenCache.Fingerprint(apiKey??"",childRefNbr??"",CodeBase.VendorDirect.TokenCache.Fingerprint(apiSecret??""));
		}
	}

	///<summary>The PostcardMania program link: its row (inserted at runtime, Enabled=0), its programproperty rows and reading the office's own
	///credentials. Key, secret, environment and child reference are per clinic, with ClinicNum 0 as the default; a clinic uses its own rows only when
	///it has its own API key. The other settings are practice-wide (ClinicNum 0). The secret is stored as osd1$ through SecretValue.
	///Nothing here calls the vendor.</summary>
	public static class PostcardManiaProgram {
		public const string ProgDesc="PostcardMania (PCM Integrations) postcards";
		public const string EnvironmentSandbox="Sandbox";
		public const string EnvironmentProduction="Production";
		///<summary>Where the office signs in to the vendor portal with its own login, for billing and payment problems. Opened without any token.</summary>
		public const string PortalUrl="https://portal.pcmintegrations.com/";

		///<summary>programproperty.PropertyDesc values.</summary>
		public static class PropertyDescs {
			public const string ApiKey="API Key";
			public const string ApiSecret="API Secret";
			public const string Environment="Environment";
			public const string ChildReference="Child Reference";
			public const string DefaultMailClass="Default Mail Class";
			public const string UsePreferredName="Use Preferred Name";
			public const string SendPatientReference="Send Patient Reference";
			public const string BaaOnFile="BAA On File";
			public const string BaaConfirmed="BAA Confirmed";
		}

		///<summary>The per-clinic account rows (key, secret, environment, child reference).</summary>
		public static readonly string[] ArrayAccountDescs={ PropertyDescs.ApiKey,PropertyDescs.ApiSecret,PropertyDescs.Environment,
			PropertyDescs.ChildReference };

		///<summary>Inserts the program row (Enabled=0) if missing, then any missing ClinicNum 0 property rows. Returns true if it inserted anything;
		///the caller refreshes the caches and signals InvalidType.Programs. Never calls the vendor.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			bool isInserted=Programs.InsertIfMissing(ProgramName.PostcardMania,ProgDesc);
			if(isInserted || Programs.GetCur(ProgramName.PostcardMania)==null) {
				Programs.RefreshCache();
			}
			long programNum=Programs.GetProgramNum(ProgramName.PostcardMania);
			if(programNum==0) {
				return isInserted;
			}
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetListForProgramAndClinic(programNum,0);
			Dictionary<string,string> dictDefaults=GetDefaultValues();
			foreach(KeyValuePair<string,string> pair in dictDefaults) {
				if(listProgramProperties.Any(x => x.PropertyDesc==pair.Key)) {
					continue;
				}
				if(pair.Key==PropertyDescs.ApiSecret) {
					VendorCredentials.EnsureSecret(programNum,pair.Key,0);
				}
				else {
					VendorCredentials.EnsureSetting(programNum,pair.Key,0,pair.Value);
				}
				isInserted=true;
			}
			return isInserted;
		}

		///<summary>Every ClinicNum 0 row and its default value.</summary>
		public static Dictionary<string,string> GetDefaultValues() {
			Meth.NoCheckMiddleTierRole();
			return new Dictionary<string,string> {
				{ PropertyDescs.ApiKey,"" },
				{ PropertyDescs.ApiSecret,"" },
				{ PropertyDescs.Environment,EnvironmentSandbox },
				{ PropertyDescs.ChildReference,"" },
				{ PropertyDescs.DefaultMailClass,PcmMailClass.FirstClass },
				{ PropertyDescs.UsePreferredName,"0" },
				{ PropertyDescs.SendPatientReference,"0" },
				{ PropertyDescs.BaaOnFile,"0" },
				{ PropertyDescs.BaaConfirmed,"" },
			};
		}

		///<summary>The program row's key, or 0 if the row doesn't exist yet (nobody has opened Program Links).</summary>
		public static long GetProgramNum() {
			Meth.NoCheckMiddleTierRole();
			Program program=Programs.GetCur(ProgramName.PostcardMania);
			return program?.ProgramNum??0;
		}

		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return Programs.IsEnabled(ProgramName.PostcardMania);
		}

		///<summary>True if the clinic has its own API key row with a value. ClinicNum 0 is always "its own".</summary>
		public static bool HasOwnAccount(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			if(clinicNum==0) {
				return true;
			}
			long programNum=GetProgramNum();
			if(programNum==0) {
				return false;
			}
			return !string.IsNullOrWhiteSpace(GetExactValue(programNum,PropertyDescs.ApiKey,clinicNum));
		}

		///<summary>The account a clinic uses: its own rows if it has its own API key, otherwise the default (ClinicNum 0). Reads the cache; never throws
		///and never calls the vendor. The returned account may not be configured (check IsConfigured).</summary>
		public static PostcardManiaAccount GetAccount(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			PostcardManiaAccount account=new PostcardManiaAccount();
			account.ClinicNum=clinicNum;
			long programNum=GetProgramNum();
			if(programNum==0) {
				return account;
			}
			long clinicNumSource=HasOwnAccount(clinicNum) ? clinicNum : 0;
			account.ClinicNumSource=clinicNumSource;
			account.ApiKey=GetExactValue(programNum,PropertyDescs.ApiKey,clinicNumSource).Trim();
			SecretReadResult secretReadResult=SecretValue.Read(GetExactValue(programNum,PropertyDescs.ApiSecret,clinicNumSource),LegacyPolicy.RejectPlaintext);
			account.ApiSecret=secretReadResult.PlainText;
			account.IsSecretUnreadable=secretReadResult.NeedsReentry;
			string environment=GetExactValue(programNum,PropertyDescs.Environment,clinicNumSource);
			account.Environment=(environment==EnvironmentProduction) ? EnvironmentProduction : EnvironmentSandbox;
			account.ChildRefNbr=GetExactValue(programNum,PropertyDescs.ChildReference,clinicNumSource).Trim();
			return account;
		}

		///<summary>True if the default or any clinic has both a key and a secret.</summary>
		public static bool IsAnyAccountConfigured() {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				return false;
			}
			List<long> listClinicNums=ProgramProperties.GetForProgram(programNum)
				.Where(x => x.PropertyDesc==PropertyDescs.ApiKey && !string.IsNullOrWhiteSpace(x.PropertyValue))
				.Select(x => x.ClinicNum)
				.Distinct()
				.ToList();
			return listClinicNums.Any(x => GetAccount(x).IsConfigured);
		}

		///<summary>Saves one clinic's (or the default's) account. secretPlainText null keeps the stored secret; "" clears it. Returns false, saving
		///nothing, if the secret could not be encrypted. Inserts missing rows. Does not refresh the cache; the caller signals InvalidType.Programs.</summary>
		public static bool SaveAccount(long clinicNum,string apiKey,string secretPlainText,string environment,string childRefNbr) {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				throw new ODException("The PostcardMania program link is missing. Open Setup, Program Links once to create it.");
			}
			if(secretPlainText!=null) {
				//The secret is written first, so an encryption failure (which writes nothing) saves nothing at all.
				if(!VendorCredentials.WriteSecret(programNum,PropertyDescs.ApiSecret,clinicNum,secretPlainText.Trim())) {
					return false;
				}
			}
			else {
				VendorCredentials.EnsureSecret(programNum,PropertyDescs.ApiSecret,clinicNum);
			}
			VendorCredentials.WriteSetting(programNum,PropertyDescs.ApiKey,clinicNum,(apiKey??"").Trim());
			VendorCredentials.WriteSetting(programNum,PropertyDescs.Environment,clinicNum,
				environment==EnvironmentProduction ? EnvironmentProduction : EnvironmentSandbox);
			VendorCredentials.WriteSetting(programNum,PropertyDescs.ChildReference,clinicNum,(childRefNbr??"").Trim());
			return true;
		}

		///<summary>Blanks a clinic's own key, secret and child reference, so it uses the default again. The rows stay (programproperty rows are only
		///deleted deliberately). Never touches ClinicNum 0. Does not refresh the cache.</summary>
		public static void ClearClinicAccount(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			if(clinicNum==0) {
				return;
			}
			long programNum=GetProgramNum();
			if(programNum==0) {
				return;
			}
			if(ProgramProperties.GetOneFromDb(programNum,PropertyDescs.ApiSecret,clinicNum)!=null) {
				VendorCredentials.WriteSecret(programNum,PropertyDescs.ApiSecret,clinicNum,"");
			}
			for(int i=0;i<ArrayAccountDescs.Length;i++) {
				if(ArrayAccountDescs[i]==PropertyDescs.ApiSecret || ArrayAccountDescs[i]==PropertyDescs.Environment) {
					continue;
				}
				if(ProgramProperties.GetOneFromDb(programNum,ArrayAccountDescs[i],clinicNum)!=null) {
					VendorCredentials.WriteSetting(programNum,ArrayAccountDescs[i],clinicNum,"");
				}
			}
		}

		///<summary>A practice-wide setting (ClinicNum 0), or its default if the row is missing.</summary>
		public static string GetSetting(string desc) {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			string defaultValue;
			GetDefaultValues().TryGetValue(desc,out defaultValue);
			if(programNum==0) {
				return defaultValue??"";
			}
			ProgramProperty programProperty=ProgramProperties.GetListForProgramAndClinic(programNum,0).FirstOrDefault(x => x.PropertyDesc==desc);
			if(programProperty==null) {
				return defaultValue??"";
			}
			return programProperty.PropertyValue??"";
		}

		///<summary>Saves a practice-wide setting, inserting the row if needed. Does not refresh the cache.</summary>
		public static void SaveSetting(string desc,string value) {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				throw new ODException("The PostcardMania program link is missing. Open Setup, Program Links once to create it.");
			}
			VendorCredentials.WriteSetting(programNum,desc,0,value);
		}

		///<summary>FirstClass unless the office chose Standard.</summary>
		public static string GetDefaultMailClass() {
			Meth.NoCheckMiddleTierRole();
			return GetSetting(PropertyDescs.DefaultMailClass)==PcmMailClass.Standard ? PcmMailClass.Standard : PcmMailClass.FirstClass;
		}

		public static bool GetUsePreferredName() {
			Meth.NoCheckMiddleTierRole();
			return GetSetting(PropertyDescs.UsePreferredName)=="1";
		}

		public static bool GetSendPatientReference() {
			Meth.NoCheckMiddleTierRole();
			return GetSetting(PropertyDescs.SendPatientReference)=="1";
		}

		///<summary>True once the office recorded that a signed business associate agreement with the vendor is on file. Every call that sends
		///recipient data (check addresses, estimate, send) is blocked until then.</summary>
		public static bool IsBaaOnFile() {
			Meth.NoCheckMiddleTierRole();
			return GetSetting(PropertyDescs.BaaOnFile)=="1";
		}

		///<summary>The value of exactly this clinic's row from the cache (no fallback), or "".</summary>
		private static string GetExactValue(long programNum,string desc,long clinicNum) {
			ProgramProperty programProperty=ProgramProperties.GetListForProgramAndClinic(programNum,clinicNum)
				.Where(x => x.PropertyDesc==desc)
				.OrderBy(x => x.ProgramPropertyNum)
				.FirstOrDefault();
			return programProperty?.PropertyValue??"";
		}
	}
}
