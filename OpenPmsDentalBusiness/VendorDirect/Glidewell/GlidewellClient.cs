// Open Dental 26.2 (P262-GLIDEWELL): the one place where this program would talk to Glidewell. Glidewell's interface for practice management
// programs is not publicly documented (no address, sign-in scheme, paths or field formats are published), so this build has no transport:
// the default client is GlidewellClientBlocked, which never opens a connection. A transport built later from Glidewell's partner documentation
// implements IGlidewellClient and calls Glidewell directly from the workstation with the office's own account credentials, never through any
// other company's server. Tests use a fake.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness.VendorDirect.Glidewell {
	///<summary>The scanner a lab case is sent for. Not stored as a number: it becomes glidewellmsg.SentITero or SentMedit and the request's
	///VendorName.</summary>
	public enum GlidewellScanner {
		///<summary>An iTero scanner. VendorName "iTero".</summary>
		ITero,
		///<summary>A Medit scanner. VendorName "Medit".</summary>
		Medit,
	}

	///<summary>The office's credentials for one call: the account number Glidewell bills and the decrypted API key of that account. Kept in memory
	///only for the call; never logged or serialized.</summary>
	public sealed class GlidewellCredentials {
		public string AccountNumber { get; }
		public string ApiKey { get; }

		public GlidewellCredentials(string accountNumber,string apiKey) {
			AccountNumber=(accountNumber??"").Trim();
			ApiKey=apiKey??"";
		}

		public override string ToString() {
			//Never the key.
			return "Glidewell account "+AccountNumber;
		}
	}

	public enum GlidewellCallOutcome {
		///<summary>Glidewell answered with success; ResponseJson holds the body.</summary>
		Success,
		///<summary>The call was made and failed (refused, an error answer, no connection, a timeout or an unreadable answer).</summary>
		Failed,
		///<summary>No call was made: this build cannot reach Glidewell.</summary>
		Blocked,
	}

	///<summary>What one call to Glidewell gave.</summary>
	public sealed class GlidewellCallResult {
		public GlidewellCallOutcome Outcome { get; }
		///<summary>The body Glidewell answered with (JSON), or "".</summary>
		public string ResponseJson { get; }
		///<summary>Why the call failed or was not made, for the user. "" on success.</summary>
		public string ErrorMessage { get; }

		private GlidewellCallResult(GlidewellCallOutcome outcome,string responseJson,string errorMessage) {
			Outcome=outcome;
			ResponseJson=responseJson??"";
			ErrorMessage=errorMessage??"";
		}

		public bool IsSuccess {
			get {
				return Outcome==GlidewellCallOutcome.Success;
			}
		}

		public static GlidewellCallResult Success(string responseJson) {
			return new GlidewellCallResult(GlidewellCallOutcome.Success,responseJson,"");
		}

		public static GlidewellCallResult Failed(string errorMessage,string responseJson="") {
			return new GlidewellCallResult(GlidewellCallOutcome.Failed,responseJson,errorMessage);
		}

		public static GlidewellCallResult Blocked(string reason) {
			return new GlidewellCallResult(GlidewellCallOutcome.Blocked,"",reason);
		}
	}

	///<summary>Talks to Glidewell. Implementations call Glidewell directly from the workstation with the credentials given, and never throw for a
	///failed call: they return GlidewellCallResult.Failed. GlidewellLabCases still catches anything thrown and treats it as a failure.</summary>
	public interface IGlidewellClient {
		///<summary>False when this client cannot reach Glidewell at all; nothing is sent then and UnavailableReason says why.</summary>
		bool IsAvailable { get; }

		///<summary>Why IsAvailable is false, for the user. "" when available.</summary>
		string UnavailableReason { get; }

		///<summary>Registers a lab case with Glidewell (the "Register" call the 26.2 documents mention): requestJson is the body built by
		///GlidewellRegisterRequest.ToJson. On success the body is Glidewell's register response (it carries the VendorRxId).</summary>
		GlidewellCallResult Register(GlidewellCredentials credentials,string requestJson);

		///<summary>Asks Glidewell for the current state of a lab case registered earlier. On success the body is Glidewell's answer (stored as the most
		///recent response).</summary>
		GlidewellCallResult GetStatus(GlidewellCredentials credentials,GlidewellMsg glidewellMsg);
	}

	///<summary>The client of this build: Glidewell's interface is not public, so nothing is sent. Never opens a connection.</summary>
	public sealed class GlidewellClientBlocked:IGlidewellClient {
		public const string REASON="This version cannot send lab cases to Glidewell yet: Glidewell's interface for practice management programs is "
			+"not publicly documented, and it will be added once that documentation is available. Lab case products are saved, and a lab case sent "
			+"earlier keeps its stored status.";

		public bool IsAvailable {
			get {
				return false;
			}
		}

		public string UnavailableReason {
			get {
				return Lans.g("Glidewell",REASON);
			}
		}

		public GlidewellCallResult Register(GlidewellCredentials credentials,string requestJson) {
			return GlidewellCallResult.Blocked(UnavailableReason);
		}

		public GlidewellCallResult GetStatus(GlidewellCredentials credentials,GlidewellMsg glidewellMsg) {
			return GlidewellCallResult.Blocked(UnavailableReason);
		}
	}

	///<summary>The client the program uses. GlidewellClientBlocked unless a transport (or a test fake) is set.</summary>
	public static class GlidewellClients {
		private static IGlidewellClient _client=new GlidewellClientBlocked();

		///<summary>Never null: setting null restores GlidewellClientBlocked.</summary>
		public static IGlidewellClient Current {
			get {
				return _client;
			}
			set {
				_client=value??new GlidewellClientBlocked();
			}
		}
	}
}
