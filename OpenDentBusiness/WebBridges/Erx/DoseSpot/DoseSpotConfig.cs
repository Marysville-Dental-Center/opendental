// DoseSpot eRx: the office's own settings, kept in the eRx program's programproperty rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness {
	///<summary>Which DoseSpot environment the office's credentials belong to. Chosen in eRx Setup; never follows the build configuration.</summary>
	public enum DoseSpotEnvironment {
		///<summary>0. my.dosespot.com. The default.</summary>
		Production,
		///<summary>1. my.staging.dosespot.com, with staging credentials from DoseSpot's integration specialist.</summary>
		Staging,
	}

	///<summary>The DoseSpot settings of one office: the environment and subscription key (practice-wide), and a clinic ID and clinic key per
	///clinic (ClinicNum 0 when clinics are off, or for Headquarters). Every value is issued by DoseSpot, or by whoever holds the DoseSpot contract
	///for the office; nothing is compiled in. The subscription key and the clinic keys are secrets: stored as osd1$ values in rows marked
	///IsMasked and IsHighSecurity. Missing rows are inserted at runtime (EnsureProperties), so there is no schema change. DoseSpot user IDs stay
	///where they always were: userodpref rows (Security > User Edit).</summary>
	public static class DoseSpotConfig {
		///<summary>Practice-wide (ClinicNum 0 only). Production or Staging.</summary>
		public const string PropEnvironment="DoseSpot Environment";
		///<summary>Practice-wide (ClinicNum 0 only), secret. Sent as a header on every DoseSpot call.</summary>
		public const string PropSubscriptionKey="DoseSpot Subscription Key";
		public const string UrlApiProduction="https://my.dosespot.com/webapi/v2";
		public const string UrlApiStaging="https://my.staging.dosespot.com/webapi/v2";
		public const string UrlSsoProduction="https://my.dosespot.com/LoginSingleSignOn.aspx";
		public const string UrlSsoStaging="https://my.staging.dosespot.com/LoginSingleSignOn.aspx?b=2";
		public const string HostProduction="my.dosespot.com";
		public const string HostStaging="my.staging.dosespot.com";
		///<summary>Open Dental stored clinic keys as plain text, and offices typed them. Used as they are and encrypted on the next save.</summary>
		public const LegacyPolicy ClinicKeyLegacyPolicy=LegacyPolicy.AcceptPlaintext;

		///<summary>The only hosts the DoseSpot client and the eRx window may reach.</summary>
		public static List<string> GetAllowedHosts() {
			return new List<string> { HostProduction,HostStaging };
		}

		///<summary>The eRx program's key, or 0 if the row is missing.</summary>
		public static long GetProgramNum() {
			Meth.NoCheckMiddleTierRole();
			return Programs.GetProgramNum(ProgramName.eRx);
		}

		///<summary>Inserts the practice-wide DoseSpot rows if they are missing, and raises IsMasked and IsHighSecurity on every existing clinic key
		///row (Open Dental wrote them unmasked). Returns true if anything may have changed, so the caller signals InvalidType.Programs.
		///Called when eRx Setup opens.</summary>
		public static bool EnsureProperties() {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				return false;
			}
			VendorCredentials.EnsureSetting(programNum,PropEnvironment,0,DoseSpotEnvironment.Production.ToString());
			VendorCredentials.EnsureSecret(programNum,PropSubscriptionKey,0);
			List<ProgramProperty> listProgramPropertiesKeys=ProgramProperties.GetWhere(x => x.ProgramNum==programNum
				&& x.PropertyDesc==Erx.PropertyDescs.ClinicKey && (!x.IsMasked || !x.IsHighSecurity));
			for(int i=0;i<listProgramPropertiesKeys.Count;i++) {
				VendorCredentials.EnsureSecret(programNum,Erx.PropertyDescs.ClinicKey,listProgramPropertiesKeys[i].ClinicNum);
			}
			return true;
		}

		///<summary>The environment from the practice-wide row. Production when the row is missing or unreadable.</summary>
		public static DoseSpotEnvironment GetEnvironment() {
			Meth.NoCheckMiddleTierRole();
			return ParseEnvironment(ProgramProperties.GetValOrDefault(GetProgramNum(),PropEnvironment,0));
		}

		///<summary>"Staging" (any case) is Staging; anything else is Production.</summary>
		public static DoseSpotEnvironment ParseEnvironment(string value) {
			if((value??"").Trim().Equals(DoseSpotEnvironment.Staging.ToString(),StringComparison.OrdinalIgnoreCase)) {
				return DoseSpotEnvironment.Staging;
			}
			return DoseSpotEnvironment.Production;
		}

		///<summary>The REST API base URL for the environment. The Introspection DoseSpotURL override still wins, for mock servers.</summary>
		public static string GetApiBaseUrl(DoseSpotEnvironment doseSpotEnvironment) {
			string url=(doseSpotEnvironment==DoseSpotEnvironment.Staging) ? UrlApiStaging : UrlApiProduction;
			return Introspection.GetOverride(Introspection.IntrospectionEntity.DoseSpotURL,url).TrimEnd('/');
		}

		///<summary>The single sign-on page for the environment. The Introspection DoseSpotSingleSignOnURL override still wins, for mock servers.</summary>
		public static string GetSsoBaseUrl(DoseSpotEnvironment doseSpotEnvironment) {
			string url=(doseSpotEnvironment==DoseSpotEnvironment.Staging) ? UrlSsoStaging : UrlSsoProduction;
			return Introspection.GetOverride(Introspection.IntrospectionEntity.DoseSpotSingleSignOnURL,url);
		}

		///<summary>The practice-wide subscription key. Clinic rows are ignored: the key is never per clinic.</summary>
		public static SecretReadResult ReadSubscriptionKey() {
			Meth.NoCheckMiddleTierRole();
			ProgramProperty programProperty=ProgramProperties.GetWhere(x => x.ProgramNum==GetProgramNum() && x.PropertyDesc==PropSubscriptionKey
				&& x.ClinicNum==0).OrderBy(x => x.ProgramPropertyNum).FirstOrDefault();
			return SecretValue.Read(programProperty?.PropertyValue,LegacyPolicy.AcceptPlaintext);
		}

		///<summary>Reads a stored clinic key (osd1$, or Open Dental's plain text). An undecryptable osd1$ value reads as blank: not configured.</summary>
		public static SecretReadResult ReadClinicKey(string stored) {
			return SecretValue.Read(stored,ClinicKeyLegacyPolicy);
		}

		///<summary>What is missing for DoseSpot at this clinic, in words the office can act on; empty when the clinic is ready. Never contacts
		///DoseSpot. The user ID is checked separately (DoseSpot.GetUserID), because it belongs to the user, not the clinic.</summary>
		public static List<string> GetMissingClinicItems(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			List<string> listMissing=new List<string>();
			long programNum=GetProgramNum();
			if(!ReadSubscriptionKey().HasValue) {
				listMissing.Add(Lans.g("DoseSpot","the DoseSpot subscription key (Setup > Program Links > eRx)"));
			}
			string clinicId=ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.ClinicNum==clinicNum && x.PropertyDesc==Erx.PropertyDescs.ClinicID)
				.Select(x => x.PropertyValue).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))??"";
			string clinicKeyStored=ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.ClinicNum==clinicNum
				&& x.PropertyDesc==Erx.PropertyDescs.ClinicKey).Select(x => x.PropertyValue).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))??"";
			string clinicName=GetClinicName(clinicNum);
			if(clinicId.Trim()=="") {
				listMissing.Add(Lans.g("DoseSpot","the DoseSpot clinic ID for")+" "+clinicName+" "+Lans.g("DoseSpot","(Setup > Program Links > eRx)"));
			}
			if(!ReadClinicKey(clinicKeyStored).HasValue) {
				listMissing.Add(Lans.g("DoseSpot","the DoseSpot clinic key for")+" "+clinicName+" "+Lans.g("DoseSpot","(Setup > Program Links > eRx)"));
			}
			return listMissing;
		}

		///<summary>The clinic's abbreviation, or "Headquarters" for ClinicNum 0.</summary>
		public static string GetClinicName(long clinicNum) {
			if(clinicNum==0) {
				return Lans.g("DoseSpot","Headquarters");
			}
			string abbr=Clinics.GetAbbr(clinicNum);
			return string.IsNullOrWhiteSpace(abbr) ? Lans.g("DoseSpot","clinic")+" "+POut.Long(clinicNum) : abbr;
		}
	}
}
