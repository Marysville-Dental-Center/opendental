// PostcardMania (PCM Integrations) DirectMail API v3: what the business layer needs from the vendor.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;

namespace OpenDentBusiness.PostcardMania {
	///<summary>The DirectMail API v3 calls this program makes. Every method runs on the workstation, blocks its thread (call it from a ProgressWin
	///worker, never the UI thread), and throws PostcardManiaException on failure. The production implementation is PostcardManiaApiClient;
	///PostcardManiaApiClientMock is an in-memory fake for tests.</summary>
	public interface IPostcardManiaApiClient {
		///<summary>Logs in (POST /auth/login) and reads the balance (GET /integration/balance).</summary>
		PcmConnectionResult TestConnection(PostcardManiaAccount account);
		///<summary>GET /design. productType is "postcard" for this feature.</summary>
		PcmPagedResult<PcmDesign> GetDesigns(PostcardManiaAccount account,string productType,int page,int perPage);
		///<summary>GET /design/{designID}/edit?mode=embed. The URL carries a short-lived token.</summary>
		PcmDesignEditorResponse GetDesignEditUrl(PostcardManiaAccount account,long designID,bool duplicate);
		///<summary>POST /design/custom. The URL carries a short-lived token.</summary>
		PcmDesignEditorResponse CreateDesign(PostcardManiaAccount account,string name,string size);
		///<summary>POST /recipient/verify.</summary>
		PcmAddressVerificationResponse VerifyAddresses(PostcardManiaAccount account,List<PcmAddress> listAddresses);
		///<summary>POST /order/postcard/pricing. Places nothing.</summary>
		PcmPricingResponse EstimatePostcardOrder(PostcardManiaAccount account,PcmPostcardOrderRequest request);
		///<summary>POST /order/postcard. A purchase. Never retried automatically, except after a 429 (nothing is processed on a 429).</summary>
		PcmOrderResponse PlacePostcardOrder(PostcardManiaAccount account,PcmPostcardOrderRequest request);
		///<summary>GET /order, optionally filtered by extRefNbr.</summary>
		PcmPagedResult<PcmOrder> GetOrders(PostcardManiaAccount account,string extRefNbr,int page,int perPage);
		///<summary>DELETE /order/{orderID}. Only pending orders, before the vendor's daily cutoff.</summary>
		void CancelOrder(PostcardManiaAccount account,long orderID);
		///<summary>GET /batch/{batchID}.</summary>
		PcmBatch GetBatch(PostcardManiaAccount account,long batchID);
		///<summary>Downloads a design's proof image from the URL the API returned, without any credentials. HTTPS only, never an Open Dental host.
		///Returns null if the URL is blank or not allowed.</summary>
		byte[] GetProofImage(string url);
	}
}
