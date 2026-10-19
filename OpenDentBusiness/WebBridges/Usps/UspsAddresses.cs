// USPS address validation: the program link row, its settings and the office's USPS app credentials.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness {
	///<summary>Which USPS host the office's app talks to. The Testing Environment for Mailers (TEM) uses the same consumer key and secret.</summary>
	public enum UspsEnvironment {
		Production,
		Testing,
	}

	///<summary>The settings one address check needs, read from the program link. The secret is plain text in memory only.</summary>
	public sealed class UspsConfig {
		public string ConsumerKey="";
		public string ConsumerSecret="";
		public UspsEnvironment Environment=UspsEnvironment.Production;
		///<summary>A SHA-256 fingerprint of the stored settings (the encrypted secret, never the plain one). A changed setting gives a new access token.</summary>
		public string Fingerprint="";

		///<summary>True if the key and secret are both present.</summary>
		public bool IsComplete {
			get {
				return !string.IsNullOrWhiteSpace(ConsumerKey) && !string.IsNullOrEmpty(ConsumerSecret);
			}
		}
	}

	///<summary>The USPSAddresses program link: a program row inserted at runtime with Enabled=0 (no ConvertDatabases step) and three practice-wide
	///programproperty rows. The consumer secret is stored as osd1$ (masked, high security). Access tokens are never stored.
	///Verification runs only when the program is enabled, the key and secret are set, and the AddressVerifyWithUSPS preference is on.</summary>
	public static class UspsAddresses {
		///<summary>The program row's description, shown in Setup > Program Links.</summary>
		public const string ProgDesc="USPS address validation";
		///<summary>The USPS app's Consumer Key (OAuth client_id). Plain text.</summary>
		public const string PropConsumerKey="Consumer Key";
		///<summary>The USPS app's Consumer Secret (OAuth client_secret). osd1$, masked, high security.</summary>
		public const string PropConsumerSecret="Consumer Secret";
		///<summary>"Production" (default) or "Testing".</summary>
		public const string PropEnvironment="Environment";
		///<summary>How long address checks are skipped, in this process, after USPS answers 429 (over the quota). Every call is billed.</summary>
		public static readonly TimeSpan QuotaCooldown=TimeSpan.FromMinutes(2);
		private static long _ticksQuotaCooldownUntilUtc;

		///<summary>Inserts the program row (Enabled=0) and its property rows if they are missing. Returns true if anything was inserted.
		///Never calls USPS. The caller refreshes the caches and signals InvalidType.Programs.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			bool isInserted=Programs.InsertIfMissing(ProgramName.USPSAddresses,ProgDesc);
			long programNum=Programs.GetProgramNum(ProgramName.USPSAddresses);
			if(programNum==0) {
				Programs.RefreshCache();
				programNum=Programs.GetProgramNum(ProgramName.USPSAddresses);
			}
			if(programNum==0) {
				return isInserted;
			}
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetForProgram(programNum);
			if(!listProgramProperties.Any(x => x.PropertyDesc==PropConsumerKey && x.ClinicNum==0)) {
				VendorCredentials.EnsureSetting(programNum,PropConsumerKey);
				isInserted=true;
			}
			ProgramProperty programPropertySecret=listProgramProperties.FirstOrDefault(x => x.PropertyDesc==PropConsumerSecret && x.ClinicNum==0);
			if(programPropertySecret==null || !programPropertySecret.IsMasked || !programPropertySecret.IsHighSecurity) {
				VendorCredentials.EnsureSecret(programNum,PropConsumerSecret);
				isInserted=true;
			}
			if(!listProgramProperties.Any(x => x.PropertyDesc==PropEnvironment && x.ClinicNum==0)) {
				VendorCredentials.EnsureSetting(programNum,PropEnvironment,0,UspsEnvironment.Production.ToString());
				isInserted=true;
			}
			return isInserted;
		}

		///<summary>The program link's settings from the cache, with the secret decrypted. Missing rows read as blank. Never throws.</summary>
		public static UspsConfig GetConfig() {
			Meth.NoCheckMiddleTierRole();
			UspsConfig uspsConfig=new UspsConfig();
			long programNum=Programs.GetProgramNum(ProgramName.USPSAddresses);
			if(programNum==0) {
				return uspsConfig;
			}
			uspsConfig.ConsumerKey=ProgramProperties.GetValOrDefault(programNum,PropConsumerKey,0).Trim();
			//USPS was never configured locally under Open Dental, so a plain-text value can only have been typed into the generic property editor.
			//Accept it, and encrypt it on the next save from the setup window.
			uspsConfig.ConsumerSecret=VendorCredentials.ReadSecret(programNum,PropConsumerSecret,0,LegacyPolicy.AcceptPlaintext).PlainText.Trim();
			uspsConfig.Environment=ParseEnvironment(ProgramProperties.GetValOrDefault(programNum,PropEnvironment,0));
			uspsConfig.Fingerprint=VendorCredentials.GetFingerprint(programNum,0,PropConsumerKey,PropConsumerSecret,PropEnvironment);
			return uspsConfig;
		}

		///<summary>"Testing" (any case) is the test environment; anything else, blank included, is production.</summary>
		public static UspsEnvironment ParseEnvironment(string value) {
			Meth.NoCheckMiddleTierRole();
			if((value??"").Trim().Equals(UspsEnvironment.Testing.ToString(),StringComparison.OrdinalIgnoreCase)) {
				return UspsEnvironment.Testing;
			}
			return UspsEnvironment.Production;
		}

		///<summary>True if the program link is enabled and has a consumer key and a readable secret. Reads the cache; never calls USPS.</summary>
		public static bool IsConfigured() {
			Meth.NoCheckMiddleTierRole();
			if(!Programs.IsEnabled(ProgramName.USPSAddresses)) {
				return false;
			}
			return GetConfig().IsComplete;
		}

		///<summary>True if IsConfigured and USPS hasn't answered "over the quota" in this process within QuotaCooldown. While cooling down, saves skip
		///verification quietly.</summary>
		public static bool IsReadyToVerify() {
			Meth.NoCheckMiddleTierRole();
			return IsConfigured() && !IsQuotaCoolingDown(DateTime.UtcNow);
		}

		///<summary>Starts the quota cool-down from utcNow.</summary>
		public static void StartQuotaCooldown(DateTime utcNow) {
			Meth.NoCheckMiddleTierRole();
			System.Threading.Interlocked.Exchange(ref _ticksQuotaCooldownUntilUtc,utcNow.Add(QuotaCooldown).Ticks);
		}

		///<summary>True while the quota cool-down runs.</summary>
		public static bool IsQuotaCoolingDown(DateTime utcNow) {
			Meth.NoCheckMiddleTierRole();
			return utcNow.Ticks<System.Threading.Interlocked.Read(ref _ticksQuotaCooldownUntilUtc);
		}

		///<summary>Ends the quota cool-down, e.g. after a successful Test connection.</summary>
		public static void ClearQuotaCooldown() {
			Meth.NoCheckMiddleTierRole();
			System.Threading.Interlocked.Exchange(ref _ticksQuotaCooldownUntilUtc,0);
		}
	}
}
