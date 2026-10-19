// Vendor-direct Microsoft 365 email: the result and data types of the in-repo MicrosoftApiConnector (replacing the prebuilt Bridges.dll types).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenDentBusiness.Microsoft365 {
	///<summary>The result of MicrosoftApiConnector.GetAccessToken and SignOutUser. Same members the callers used with Bridges.dll.
	///A blank AccessToken with a blank ErrorMessage means the user cancelled (or, for sign-out, that it worked).</summary>
	public class MicrosoftTokenHelper {
		///<summary>Not a bearer token any more: the account marker stored in emailaddress.AccessToken, MSAL's HomeAccountId.Identifier
		///("objectId.tenantId", about 73 characters). Non-blank means signed in. "" = cancelled, failed or signed out.</summary>
		public string AccessToken="";
		///<summary>The mailbox's MSAL token cache, encrypted ("osd1$..."), stored in emailaddress.RefreshToken.</summary>
		public string AccountInfo="";
		///<summary>The signed-in account's mail address, or its user principal name when it has no mail attribute.</summary>
		public string EmailAddress="";
		///<summary>A neutral message to show, or "".</summary>
		public string ErrorMessage="";
		///<summary>Why it failed, when ErrorMessage is set.</summary>
		public Microsoft365ErrorKind ErrorKind=Microsoft365ErrorKind.General;
	}

	///<summary>The fields of GET /me that the program uses.</summary>
	public class MicrosoftProfile {
		///<summary>The user's object ID.</summary>
		public string Id="";
		///<summary>The primary SMTP address, or "" for accounts without one.</summary>
		public string Mail="";
		public string UserPrincipalName="";
		public string DisplayName="";

		///<summary>Mail, or the user principal name when Mail is blank.</summary>
		public string GetAddress() {
			return string.IsNullOrWhiteSpace(Mail) ? (UserPrincipalName??"") : Mail;
		}

		///<summary>True if emailUsername is blank, or equals Mail or UserPrincipalName ignoring case.</summary>
		public bool Matches(string emailUsername) {
			string username=(emailUsername??"").Trim();
			if(username=="") {
				return true;
			}
			return username.Equals((Mail??"").Trim(),StringComparison.OrdinalIgnoreCase)
				|| username.Equals((UserPrincipalName??"").Trim(),StringComparison.OrdinalIgnoreCase);
		}
	}

	///<summary>One message in an inbox listing. Replaces Microsoft.Graph.Message in RetrieveMessages; callers use Id only. Id is an immutable ID
	///(it survives moving the message to another folder) and is case-sensitive.</summary>
	public class MicrosoftMessageRef {
		public string Id="";
		public DateTime ReceivedDateTime;
	}

	///<summary>What a device-code sign-in shows the user.</summary>
	public class Microsoft365DeviceCode {
		///<summary>The code to type at the verification page.</summary>
		public string UserCode="";
		///<summary>Microsoft's verification page.</summary>
		public string VerificationUrl="";
		///<summary>Microsoft's own instruction text.</summary>
		public string Message="";
		public DateTimeOffset ExpiresOn;
	}

	///<summary>The outcome of an interactive sign-in (Microsoft365Session.SignInInteractiveAsync).</summary>
	public class Microsoft365SignInResult {
		///<summary>MSAL's HomeAccountId.Identifier, stored in emailaddress.AccessToken.</summary>
		public string AccountMarker="";
		///<summary>The encrypted MSAL cache (osd1$), stored in emailaddress.RefreshToken.</summary>
		public string StoredCache="";
		public MicrosoftProfile Profile=new MicrosoftProfile();
	}
}
