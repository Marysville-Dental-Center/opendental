// PostcardMania (PCM Integrations) DirectMail API v3: the one exception the client throws.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CodeBase;

namespace OpenDentBusiness.PostcardMania {
	///<summary>Why a PostcardMania call failed. The forms choose their message and next step from this.</summary>
	public enum PostcardManiaErrorKind {
		///<summary>Anything not covered below.</summary>
		Other,
		///<summary>No connection, DNS or TLS failure, or a refused URL. Nothing was sent or saved.</summary>
		Network,
		///<summary>No answer in time on a call that can't be repeated blindly. Placing an order: look it up by its reference before retrying.</summary>
		Timeout,
		///<summary>The API key or secret was rejected (401 or 404 on login, or 401 twice).</summary>
		CredentialsRejected,
		///<summary>Not configured: no API key or secret for this clinic or the default.</summary>
		NotConfigured,
		///<summary>404 on a resource, e.g. a design that no longer exists.</summary>
		NotFound,
		///<summary>409: an order with this reference already exists.</summary>
		Conflict,
		///<summary>400 or 422: the request was refused; Messages say why.</summary>
		Validation,
		///<summary>429 after the allowed retries. Nothing was processed or charged.</summary>
		RateLimited,
		///<summary>5xx. For an order, the outcome is unknown until it is looked up.</summary>
		Server,
	}

	///<summary>A failed PostcardMania call. Messages holds the vendor's error.data[].message values (with the property when given).
	///Never carries an inner exception, because ProgressWin rethrows the innermost exception and the friendly message would be lost.
	///Never contains a token, a secret or patient data.</summary>
	public class PostcardManiaException:ODException {
		public PostcardManiaErrorKind Kind { get; }
		///<summary>0 when there was no HTTP response.</summary>
		public HttpStatusCode HttpStatusCode { get; }
		public List<string> Messages { get; }
		///<summary>Seconds from a 429's Retry-After, or 0.</summary>
		public int RetryAfterSeconds { get; }

		public PostcardManiaException(PostcardManiaErrorKind kind,string message,HttpStatusCode httpStatusCode=0,List<string> listMessages=null,
			int retryAfterSeconds=0) : base(BuildMessage(message,listMessages))
		{
			Kind=kind;
			HttpStatusCode=httpStatusCode;
			Messages=listMessages??new List<string>();
			RetryAfterSeconds=retryAfterSeconds;
		}

		///<summary>True when an order may or may not have been placed: a timeout, a network failure after sending, or a 5xx.</summary>
		public bool IsOutcomeUnknown {
			get {
				return Kind==PostcardManiaErrorKind.Timeout || Kind==PostcardManiaErrorKind.Server || Kind==PostcardManiaErrorKind.Network;
			}
		}

		private static string BuildMessage(string message,List<string> listMessages) {
			if(listMessages==null || listMessages.Count==0) {
				return message;
			}
			return message+"\r\n"+string.Join("\r\n",listMessages.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => "- "+x));
		}
	}
}
