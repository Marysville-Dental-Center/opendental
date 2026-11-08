// P262-CLAIMS (Open Dental 26.2, release note "Option to set the default claim form at a provider level"): provider.ClaimFormNum, "the default
// claim form created for the provider. This takes priority over pref.DefaultClaimForm and insplan.ClaimFormNum" (OpenDentalDiffDocumentation26-2).
// Which provider: the billing provider's first, then the treating provider's (owner decision of 2026-11-07); when it applies is INFERRED. See
// docs/port-notes/P262-CLAIMS.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The claim form a new claim gets: the billing provider's default claim form when that provider has one, otherwise the treating
	///provider's, otherwise the insurance plan's (which itself starts as the practice default, pref DefaultClaimForm, when the plan is created).
	///Owner decision of 2026-11-07.</summary>
	public static class ClaimFormDefaults {
		///<summary>The claim form (claim.ClaimForm) for a claim created now with this billing provider, this treating provider and this plan's claim
		///form. Every place that creates a claim or preauthorization calls it once the claim's billing and treating providers are known. The billing
		///provider's default claim form first, then the treating provider's (owner decision of 2026-11-07), then the plan's. INFERRED: at creation
		///only; changing a provider on a claim later does not change its claim form, and existing claims are not changed.</summary>
		public static long GetClaimFormNumForNewClaim(long provNumBill,long provNumTreat,long claimFormNumPlan) {
			Meth.NoCheckMiddleTierRole();
			return Pick(GetProviderClaimFormNum(provNumBill),GetProviderClaimFormNum(provNumTreat),claimFormNumPlan,x => ClaimForms.GetClaimForm(x)!=null);
		}

		///<summary>The provider's default claim form (provider.ClaimFormNum) from the cache, or 0 for no provider, an unknown one or none set.</summary>
		private static long GetProviderClaimFormNum(long provNum) {
			if(provNum<=0) {
				return 0;
			}
			Provider provider=Providers.GetFirstOrDefault(x => x.ProvNum==provNum);
			return provider?.ClaimFormNum??0;
		}

		///<summary>The billing provider's claim form when it is set (above 0) and still exists, else the treating provider's when it is set and still
		///exists, else the plan's. funcExists tells whether a claim form exists (null: every set claim form counts); a provider whose claim form was
		///deleted is passed over.</summary>
		public static long Pick(long claimFormNumProvBill,long claimFormNumProvTreat,long claimFormNumPlan,Func<long,bool> funcExists) {
			foreach(long claimFormNumProv in new[] { claimFormNumProvBill,claimFormNumProvTreat }) {
				if(claimFormNumProv>0 && (funcExists==null || funcExists(claimFormNumProv))) {
					return claimFormNumProv;
				}
			}
			return claimFormNumPlan;
		}

		///<summary>The abbreviations of the providers whose default claim form this is, in ProvNum order (hidden providers too). Empty when none.
		///</summary>
		public static List<string> GetProviderAbbrsUsing(long claimFormNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<string>>(MethodBase.GetCurrentMethod(),claimFormNum);
			}
			if(claimFormNum<=0) {
				return new List<string>();
			}
			string command="SELECT Abbr FROM provider WHERE ClaimFormNum="+POut.Long(claimFormNum)+" ORDER BY ProvNum";
			return Db.GetListString(command);
		}
	}
}
