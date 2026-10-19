// QuickBooks Online (direct): the token carrier the deposit and setup windows pass around. Replaces the type of the same name in the prebuilt
// Bridges.dll, re-implemented from how this program calls it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenDentBusiness.Qbo {
	///<summary>Kept as a thin carrier for the callers' sake. The real token state lives in QboTokenManager: access tokens in memory only, the
	///refresh token in the database. RequestException is set instead of throwing, the error style the callers rely on.</summary>
	public class QuickBooksOnlineToken {
		///<summary>The current access token, or "". Never written anywhere.</summary>
		public string AccessToken { get; set; }
		///<summary>Not used by the direct integration: the refresh token stays in the database. Kept for compatibility.</summary>
		public string RefreshToken { get; set; }
		///<summary>The failure, or null on success.</summary>
		public Exception RequestException { get; internal set; }

		public QuickBooksOnlineToken(string accessToken,string refreshToken) {
			AccessToken=accessToken??"";
			RefreshToken=refreshToken??"";
		}

		internal static QuickBooksOnlineToken FromException(Exception ex) {
			QuickBooksOnlineToken token=new QuickBooksOnlineToken("","");
			token.RequestException=ex;
			return token;
		}
	}
}
