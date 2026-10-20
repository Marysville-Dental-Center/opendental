// QuickBooks Online (direct): the errors the integration reports.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;

namespace OpenDentBusiness.Qbo {
	///<summary>The connection to QuickBooks Online can't be used: not set up, from another installation, or refused by Intuit (revoked, expired,
	///rotated away, or the wrong keys). The message says what the office should do. It never carries a token, code or secret.</summary>
	public class QboAuthException : ODException {
		///<summary>The message for a connection that has to be made again.</summary>
		public const string MessageReconnect="QuickBooks Online needs to be reconnected in Accounting > Setup > QuickBooks Online.";
		///<summary>The message when nothing is set up.</summary>
		public const string MessageNotConnected="QuickBooks Online is not connected. Connect it in Accounting > Setup > QuickBooks Online.";
		///<summary>The message for a saved connection that this installation can't read (migrated from another installation).</summary>
		public const string MessageLegacy="The saved QuickBooks Online connection is from another installation and cannot be used. "
			+"Connect it again in Accounting > Setup > QuickBooks Online.";
		///<summary>The message when Intuit refuses the client ID or secret.</summary>
		public const string MessageInvalidClient="Intuit rejected the client ID or secret. Check that the keys match the selected environment.";
		///<summary>The message when the redirect does not belong to the connection attempt in progress.</summary>
		public const string MessageStateMismatch="This address is from a different connection attempt. Click Connect again.";

		///<summary>True when the office must connect again (as opposed to fixing the setup values).</summary>
		public bool NeedsReconnect { get; }
		///<summary>The OAuth error code from Intuit, when there was one (invalid_grant, invalid_client, access_denied), or "".</summary>
		public string OAuthError { get; }

		public QboAuthException(string message,bool needsReconnect=true,string oAuthError="",Exception innerException=null)
			: base(message,innerException)
		{
			NeedsReconnect=needsReconnect;
			OAuthError=oAuthError??"";
		}
	}

	///<summary>A QuickBooks Online Accounting API call failed. The message is neutral and safe to show; it never carries the request body (amounts,
	///memo) or a token.</summary>
	public class QboApiException : ODException {
		///<summary>The message for a network failure.</summary>
		public const string MessageUnreachable="QuickBooks Online could not be reached.";
		///<summary>The message for HTTP 429.</summary>
		public const string MessageThrottled="QuickBooks Online is busy. Try again in a minute.";

		///<summary>The HTTP status, or 0 for a network failure.</summary>
		public int HttpStatus { get; }
		///<summary>The Fault type ("ValidationFault", "AuthenticationFault"...), or "".</summary>
		public string FaultType { get; }
		///<summary>The first Fault error code, or "".</summary>
		public string Code { get; }
		///<summary>Intuit's intuit_tid response header, for support, or "".</summary>
		public string IntuitTid { get; }

		public QboApiException(string message,int httpStatus=0,string faultType="",string code="",string intuitTid="",Exception innerException=null)
			: base(message,innerException)
		{
			HttpStatus=httpStatus;
			FaultType=faultType??"";
			Code=code??"";
			IntuitTid=intuitTid??"";
		}

		///<summary>True for a failure where no answer came back (network, timeout).</summary>
		public bool IsNetworkFailure {
			get {
				return HttpStatus==0;
			}
		}
	}
}
