// CareCredit (Synchrony) patient financing: the calls this program makes to Synchrony's Patient Data Service.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Threading;
using System.Threading.Tasks;
using CodeBase.VendorDirect;

namespace OpenDentBusiness.Bridges {
	///<summary>Token, prefill, pullback and batch submit. CareCreditApiClient talks HTTP to the office's configured Synchrony hosts; tests swap in a
	///fake through CareCredit.ApiClient. Every method throws CareCredit.CareCreditException for an HTTP error answer (ErrorCode is the status;
	///IsAuthFailure for refused credentials) and HttpRequestException for a network failure.</summary>
	public interface ICareCreditApiClient {
		///<summary>A new access token (client-credentials grant). Not cached: Test connection uses this to prove the credentials work.</summary>
		Task<OAuthAccessToken> RequestTokenAsync(CareCreditConfig config,CancellationToken cancellationToken=default(CancellationToken));

		///<summary>Sends the prefill and returns the reference ID and hosted-page URL.</summary>
		Task<CareCreditPrefillResult> PrefillAsync(CareCreditConfig config,CareCreditPrefillRequest request,
			CancellationToken cancellationToken=default(CancellationToken));

		///<summary>Gets the outcome for a reference ID.</summary>
		Task<CareCreditPullbackResult> PullbackAsync(CareCreditConfig config,string referenceId,string merchantNumber,bool isQuickScreen,
			CancellationToken cancellationToken=default(CancellationToken));

		///<summary>Submits one batch QuickScreen (or lookup) request and returns its reference ID.</summary>
		Task<string> SubmitBatchAsync(CareCreditConfig config,CareCreditBatchRequest request,CancellationToken cancellationToken=default(CancellationToken));
	}
}
