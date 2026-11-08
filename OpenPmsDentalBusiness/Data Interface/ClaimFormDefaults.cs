// P262-CLAIMS (Open Dental 26.2, release note "Option to set the default claim form at a provider level"): provider.ClaimFormNum, "the default
// claim form created for the provider. This takes priority over pref.DefaultClaimForm and insplan.ClaimFormNum" (OpenDentalDiffDocumentation26-2).
// Which provider, and when, is INFERRED; see docs/port-notes/P262-CLAIMS.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The claim form a new claim gets: the treating provider's default claim form when the provider has one, otherwise the insurance plan's
	///(which itself starts as the practice default, pref DefaultClaimForm, when the plan is created).</summary>
	public static class ClaimFormDefaults {
		///<summary>The claim form (claim.ClaimForm) for a claim created now for this treating provider and this plan's claim form. Every place that
		///creates a claim or preauthorization calls it once the claim's treating provider is known. INFERRED: the treating provider, at creation;
		///changing the provider on a claim later does not change its claim form, and existing claims are not changed.</summary>
		public static long GetClaimFormNumForNewClaim(long provNumTreat,long claimFormNumPlan) {
			Meth.NoCheckMiddleTierRole();
			Provider provider=null;
			if(provNumTreat>0) {
				provider=Providers.GetFirstOrDefault(x => x.ProvNum==provNumTreat);
			}
			return Pick(provider?.ClaimFormNum??0,claimFormNumPlan,x => ClaimForms.GetClaimForm(x)!=null);
		}

		///<summary>The provider's claim form when it is set (above 0) and still exists, otherwise the plan's. funcExists tells whether a claim form
		///exists; a provider whose claim form was deleted falls back to the plan's.</summary>
		public static long Pick(long claimFormNumProv,long claimFormNumPlan,Func<long,bool> funcExists) {
			if(claimFormNumProv>0 && (funcExists==null || funcExists(claimFormNumProv))) {
				return claimFormNumProv;
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
