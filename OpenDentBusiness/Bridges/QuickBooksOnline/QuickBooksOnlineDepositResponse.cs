// QuickBooks Online (direct): the result of sending a deposit. Replaces the type of the same name in the prebuilt Bridges.dll, re-implemented
// from how this program calls it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenDentBusiness.Qbo {
	///<summary>What came back from creating a deposit. RequestException is set instead of throwing.</summary>
	public class QuickBooksOnlineDepositResponse {
		///<summary>The failure, or null on success.</summary>
		public Exception RequestException { get; internal set; }
		///<summary>The QuickBooks Online Id of the deposit created (or of the original one, when a retry with the same request ID was answered from
		///Intuit's record of it). "" on failure.</summary>
		public string DepositId { get; internal set; }
		///<summary>True when a class was sent and at least one line came back without it.</summary>
		internal bool IsClassRefMissing;

		public QuickBooksOnlineDepositResponse() {
			DepositId="";
		}

		///<summary>True when a class was sent but QuickBooks Online didn't apply it to every line, which usually means class tracking is off.</summary>
		public bool IsClassRefNull() {
			return IsClassRefMissing;
		}

		internal static QuickBooksOnlineDepositResponse FromException(Exception ex) {
			QuickBooksOnlineDepositResponse response=new QuickBooksOnlineDepositResponse();
			response.RequestException=ex;
			return response;
		}
	}
}
