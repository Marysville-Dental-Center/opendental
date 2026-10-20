// CareCredit (Synchrony) patient financing: the enums callers used from the prebuilt bridge, re-implemented in-repo.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.ComponentModel;

namespace OpenDentBusiness.Bridges {
	///<summary>The hosted page a prefill request opens. The names are the codes this program has always passed. The values Synchrony expects come
	///from its implementation guide; CareCreditApiClient sends the name as it is.</summary>
	public enum CareCreditLandingPage {
		///<summary>Home page. Refined by EnumCareCreditPrefillServiceType: admin, reports, lookup or default.</summary>
		HM,
		///<summary>Credit application.</summary>
		AP,
		///<summary>Cardholder lookup.</summary>
		LU,
		///<summary>Purchase.</summary>
		PR,
		///<summary>Prequalification (QuickScreen).</summary>
		QS,
		///<summary>Refund.</summary>
		RF,
	}

	///<summary>Refines the HM (home) landing page. C is the default.</summary>
	public enum EnumCareCreditPrefillServiceType {
		///<summary>Admin: the merchant's promotional financing terms.</summary>
		A,
		///<summary>Default.</summary>
		C,
		///<summary>Lookup.</summary>
		L,
		///<summary>Reports.</summary>
		R,
	}

	///<summary>Batch QuickScreen mode per clinic. Stored as an int in the QSBatchEnabled program property. Off=0 and On=1 are certain (ConvertDatabases6
	///and the setup default "1"). NoBalance=2 and LookupOnly=3 follow the declaration order; confirm them against an office's migrated data
	///(docs/specs/vendor-direct/carecredit.md, open question 6).</summary>
	public enum EnumCareCreditBatch {
		///<summary>0. No batch.</summary>
		[Description("Off")]
		Off=0,
		///<summary>1. Screens patients with an estimated patient portion over $1.</summary>
		[Description("On (with Bal)")]
		On=1,
		///<summary>2. Screens every patient with an upcoming appointment.</summary>
		[Description("On (no Bal)")]
		NoBalance=2,
		///<summary>3. Only checks cardholder status and available credit. Needs the last four digits of the SSN.</summary>
		[Description("Lookup Only")]
		LookupOnly=3,
	}

	///<summary>Which Synchrony environment the office's credentials belong to. Stored by name in CareCreditEnvironment.</summary>
	public enum CareCreditEnvironment {
		///<summary>Sandbox, with anonymized mock data only (Synchrony Developer Agreement).</summary>
		Sandbox,
		///<summary>Production, after Synchrony approved the app.</summary>
		Production,
	}

	///<summary>Where the client ID and secret go in the token request. From Synchrony's implementation guide. Stored by name.</summary>
	public enum CareCreditClientAuth {
		///<summary>HTTP Basic authentication header (client_secret_basic).</summary>
		[Description("Basic header")]
		BasicHeader,
		///<summary>Form fields client_id and client_secret (client_secret_post).</summary>
		[Description("Form body")]
		FormBody,
	}
}
