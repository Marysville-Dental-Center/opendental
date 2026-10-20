// Vendor-direct integrations: the HTTP client both PDMP bridges use (Bamboo PMP Gateway with mutual TLS and digest headers, LogiCoy with Basic auth).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using CodeBase;
using CodeBase.VendorDirect;

namespace OpenDentBusiness {
	///<summary>Why a PDMP call failed. Each kind has one user message (PdmpException.Message); none carries a response body or patient data.</summary>
	public enum PdmpErrorKind {
		///<summary>Setup is incomplete (URL, credentials or certificate missing).</summary>
		NotConfigured,
		///<summary>The URL failed the vendor URL policy (not HTTPS, or a host this program never contacts).</summary>
		UrlRefused,
		///<summary>No network, DNS failure, refused connection or timeout.</summary>
		Unreachable,
		///<summary>The TLS handshake failed or the server answered 403: the client certificate was rejected or has expired.</summary>
		CertificateRejected,
		///<summary>HTTP 401.</summary>
		Unauthorized,
		///<summary>HTTP 5xx.</summary>
		ServiceUnavailable,
		///<summary>Any other non-success HTTP status without a readable vendor error.</summary>
		HttpError,
		///<summary>The body was not the XML we expected.</summary>
		MalformedResponse,
		///<summary>The vendor said no (no unique patient, permissions). A business outcome, not a fault.</summary>
		Disallowed,
		///<summary>The vendor returned an Error element.</summary>
		VendorError,
	}

	///<summary>A PDMP call that failed. Derives from ApplicationException so the launcher shows the message in a plain box. The message is safe to show
	///and to log: it never holds a response body, a header or patient data. It never has an InnerException: ProgressWin rethrows the inner exception
	///in place of the outer one, and the inner one's message could echo a URL. The underlying exception's type name is kept in CauseType.</summary>
	public class PdmpException:ApplicationException {
		public PdmpErrorKind Kind { get; }
		///<summary>The HTTP status, or 0 when there was no response.</summary>
		public int HttpStatus { get; }
		///<summary>The type name of the exception that caused this one, for diagnostics, or "".</summary>
		public string CauseType { get; }

		public PdmpException(PdmpErrorKind kind,string message,int httpStatus=0,string causeType="") : base(message) {
			Kind=kind;
			HttpStatus=httpStatus;
			CauseType=causeType??"";
		}
	}

	///<summary>The one HTTP path for both PDMP bridges: an XML POST over HTTPS through CodeBase.VendorDirect.VendorHttp (never an Open Dental host,
	///HTTPS only, no automatic retries for POSTs), a 30-second timeout, UTF-8 bodies, an optional client certificate, and a fixed error mapping
	///(pdmp.md section 3.6). Runs on the client workstation only; nothing here touches the database.</summary>
	public static class PdmpHttp {
		public const string VendorName="PDMP";
		private const string MEDIA_TYPE_XML="application/xml";
		///<summary>Per-request timeout.</summary>
		public static readonly TimeSpan Timeout=TimeSpan.FromSeconds(30);

		#region Messages
		public static string MessageUnreachable {
			get { return Lans.g("PDMP","The prescription monitoring service could not be reached. Use your state PMP website."); }
		}

		public static string MessageCertificateRejected {
			get { return Lans.g("PDMP","The client certificate was rejected or has expired. Check Setup."); }
		}

		public static string MessageUnauthorized {
			get { return Lans.g("PDMP","The username or password was rejected."); }
		}

		public static string MessageClock {
			get { return Lans.g("PDMP","If they are correct, check that this computer's date and time are set correctly."); }
		}

		public static string MessageUnavailable {
			get { return Lans.g("PDMP","The service is temporarily unavailable."); }
		}

		public static string MessageMalformed {
			get { return Lans.g("PDMP","Unexpected response from the service."); }
		}

		public static string MessageSearchPortal {
			get { return Lans.g("PDMP","Search for this patient on your state PMP website."); }
		}
		#endregion Messages

		///<summary>POSTs the XML body to url and deserializes the answer as T. dictHeaders are added as-is (Authorization, X-Auth-*). clientCertificate
		///is presented for mutual TLS when not null. httpMessageHandler replaces the network for tests. A 4xx answer whose body is a T (the vendor's
		///own error document) is returned so the caller can read its Error items. isDigestAuth adds the clock hint to the 401 message.
		///Throws PdmpException.</summary>
		public static T PostXml<T>(string url,string body,Dictionary<string,string> dictHeaders,X509Certificate2 clientCertificate,
			HttpMessageHandler httpMessageHandler=null,bool isDigestAuth=false)
		{
			Meth.NoCheckMiddleTierRole();
			Uri uri=CheckUrl(url);
			return VendorHttp.RunSync(() => PostXmlAsync<T>(uri,body,dictHeaders,clientCertificate,httpMessageHandler,isDigestAuth));
		}

		private static async Task<T> PostXmlAsync<T>(Uri uri,string body,Dictionary<string,string> dictHeaders,X509Certificate2 clientCertificate,
			HttpMessageHandler httpMessageHandler,bool isDigestAuth)
		{
			using(HttpClient httpClient=CreateClient(clientCertificate,httpMessageHandler))
			using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,uri)) {
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_XML));
				if(dictHeaders!=null) {
					foreach(KeyValuePair<string,string> header in dictHeaders) {
						request.Headers.TryAddWithoutValidation(header.Key,header.Value);
					}
				}
				request.Content=new StringContent(body??"",new UTF8Encoding(false),MEDIA_TYPE_XML);
				VendorHttp.SetEndpointName(request,"POST "+uri.Host);//The host only: paths can carry tokens.
				HttpResponseMessage response;
				try {
					response=await httpClient.SendAsync(request).ConfigureAwait(false);
				}
				catch(Exception ex) {
					throw MapSendException(ex);
				}
				using(response) {
					string responseBody=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					int status=(int)response.StatusCode;
					if(response.IsSuccessStatusCode) {
						try {
							return Deserialize<T>(responseBody);
						}
						catch(Exception ex) {
							throw new PdmpException(PdmpErrorKind.MalformedResponse,MessageMalformed,status,ex.GetType().Name);
						}
					}
					if(status==401) {
						throw new PdmpException(PdmpErrorKind.Unauthorized,MessageUnauthorized+(isDigestAuth ? " "+MessageClock : ""),status);
					}
					if(status==403) {
						throw new PdmpException(PdmpErrorKind.CertificateRejected,MessageCertificateRejected,status);
					}
					if(status>=500) {
						throw new PdmpException(PdmpErrorKind.ServiceUnavailable,MessageUnavailable,status);
					}
					//A 4xx can carry the vendor's own error document. Hand it back so the caller shows the vendor's Error or Disallowed fields.
					try {
						return Deserialize<T>(responseBody);
					}
					catch(Exception ex) {
						ex.DoNothing();
					}
					throw new PdmpException(PdmpErrorKind.HttpError,Lans.g("PDMP","The service returned an error.")+" (HTTP "+status+")",status);
				}
			}
		}

		///<summary>Sends a bodyless GET to the origin of url (scheme, host and port) with the client certificate, to prove that the host resolves and the TLS
		///handshake succeeds. Any HTTP status counts as reachable. Sends no patient data and no credentials besides the certificate.
		///Returns "" when reachable, else the user message.</summary>
		public static string CheckReachable(string url,X509Certificate2 clientCertificate,HttpMessageHandler httpMessageHandler=null) {
			Meth.NoCheckMiddleTierRole();
			Uri uri;
			try {
				uri=CheckUrl(url);
			}
			catch(PdmpException ex) {
				return ex.Message;
			}
			Uri uriOrigin=new Uri(uri.GetLeftPart(UriPartial.Authority)+"/");
			try {
				VendorHttp.RunSync(async () => {
					using(HttpClient httpClient=CreateClient(clientCertificate,httpMessageHandler))
					using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Get,uriOrigin)) {
						VendorHttp.SetEndpointName(request,"GET "+uriOrigin.Host);
						VendorHttp.SetNoRetry(request);
						using(HttpResponseMessage response=await httpClient.SendAsync(request).ConfigureAwait(false)) {
							return (int)response.StatusCode;
						}
					}
				});
			}
			catch(Exception ex) {
				return MapSendException(ex).Message;
			}
			return "";
		}

		///<summary>Throws PdmpException unless url is an absolute HTTPS URL (plain http only to this computer) on a host this program may contact.</summary>
		public static Uri CheckUrl(string url) {
			VendorUrlProblem problem=VendorUrlPolicy.Check(url);
			if(problem==VendorUrlProblem.NotAbsolute && string.IsNullOrWhiteSpace(url)) {
				throw new PdmpException(PdmpErrorKind.NotConfigured,Lans.g("PDMP","The service address is not set."));
			}
			if(problem!=VendorUrlProblem.None) {
				throw new PdmpException(PdmpErrorKind.UrlRefused,Lans.g("PDMP","The service address was refused.")+" "+VendorUrlPolicy.Describe(problem));
			}
			return new Uri(url.Trim(),UriKind.Absolute);
		}

		private static HttpClient CreateClient(X509Certificate2 clientCertificate,HttpMessageHandler httpMessageHandler) {
			VendorHttpOptions options=new VendorHttpOptions();
			options.VendorName=VendorName;
			options.Timeout=Timeout;
			options.MaxGetRetries=0;//Interactive: fail fast, the user clicks again.
			options.ClientCertificate=clientCertificate;
			//A new client per call: the certificate can differ per clinic and per click, and VendorHttp.GetClient would keep the first one.
			//disposeHandler is left to HttpClient; a test handler survives because the policy handler wraps it without owning the test's reference.
			return VendorHttp.CreateClient(options,httpMessageHandler==null ? null : new NonDisposingHandler(httpMessageHandler));
		}

		///<summary>Maps a send failure to a PdmpException without exposing the URL or any body.</summary>
		public static PdmpException MapSendException(Exception ex) {
			if(ex is PdmpException pdmpException) {
				return pdmpException;
			}
			if(ex is AggregateException aggregateException && aggregateException.InnerExceptions.Count==1) {
				return MapSendException(aggregateException.InnerException);
			}
			if(ex is VendorHttpException vendorHttpException) {
				if(vendorHttpException.Failure==VendorHttpFailure.UrlRefused) {
					return new PdmpException(PdmpErrorKind.UrlRefused,Lans.g("PDMP","The service address was refused.")+" "
						+VendorUrlPolicy.Describe(vendorHttpException.UrlProblem));
				}
				return new PdmpException(PdmpErrorKind.Unreachable,MessageUnreachable,0,ex.GetType().Name);
			}
			if(ex is TaskCanceledException || ex is OperationCanceledException || ex is TimeoutException) {
				return new PdmpException(PdmpErrorKind.Unreachable,MessageUnreachable,0,ex.GetType().Name);
			}
			if(IsTlsFailure(ex)) {
				return new PdmpException(PdmpErrorKind.CertificateRejected,MessageCertificateRejected,0,ex.GetType().Name);
			}
			return new PdmpException(PdmpErrorKind.Unreachable,MessageUnreachable,0,ex.GetType().Name);
		}

		///<summary>True if any exception in the chain is a TLS or certificate failure (WebException TrustFailure or SecureChannelFailure, or an
		///AuthenticationException).</summary>
		internal static bool IsTlsFailure(Exception ex) {
			for(Exception exCur=ex;exCur!=null;exCur=exCur.InnerException) {
				if(exCur is AuthenticationException) {
					return true;
				}
				if(exCur is WebException webException
					&& (webException.Status==WebExceptionStatus.TrustFailure || webException.Status==WebExceptionStatus.SecureChannelFailure))
				{
					return true;
				}
			}
			return false;
		}

		#region XML
		///<summary>Serializes a request object as UTF-8 XML.</summary>
		public static string Serialize<T>(T request) {
			Meth.NoCheckMiddleTierRole();
			using(MemoryStream memoryStream=new MemoryStream()) {
				XmlSerializer xmlSerializer=new XmlSerializer(request.GetType());
				using(XmlWriter xmlWriter=XmlWriter.Create(memoryStream,new XmlWriterSettings { Encoding=new UTF8Encoding(false),Indent=true })) {
					xmlSerializer.Serialize(xmlWriter,request);
				}
				byte[] arrayBytes=memoryStream.ToArray();
				return Encoding.UTF8.GetString(arrayBytes,0,arrayBytes.Length);
			}
		}

		///<summary>Deserializes a response. If the first attempt fails, strips xsi:type attributes from Pmp nodes and tries again: responses have carried
		///them, and stripping them is what worked in testing. Throws on anything that isn't the expected document.</summary>
		public static T Deserialize<T>(string strXml) {
			Meth.NoCheckMiddleTierRole();
			XmlDocument xmlDocument=new XmlDocument();
			xmlDocument.XmlResolver=null;//No external entities.
			using(XmlReader xmlReaderSafe=XmlReader.Create(new StringReader(strXml??""),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null })) {
				xmlDocument.Load(xmlReaderSafe);
			}
			XmlSerializer xmlSerializer=new XmlSerializer(typeof(T));
			try {
				using(XmlReader xmlReader=new XmlNodeReader(xmlDocument)) {
					return (T)xmlSerializer.Deserialize(xmlReader);
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
				StripAttributeFromNode(xmlDocument.ChildNodes,"Pmp","xsi:type");
				using(XmlReader xmlReaderStripped=new XmlNodeReader(xmlDocument)) {
					return (T)xmlSerializer.Deserialize(xmlReaderStripped);
				}
			}
		}

		///<summary>Strips the named attributes from every node called nodeName in nodeList and its descendants.</summary>
		private static void StripAttributeFromNode(XmlNodeList nodeList,string nodeName,params string[] arrayAttributeNames) {
			if(nodeList==null) {
				return;
			}
			foreach(XmlNode xmlNode in nodeList) {
				StripAttributeFromNode(xmlNode.ChildNodes,nodeName,arrayAttributeNames);
				if(xmlNode.Name!=nodeName || xmlNode.Attributes==null) {
					continue;
				}
				for(int i=xmlNode.Attributes.Count-1;i>=0;i--) {
					if(xmlNode.Attributes[i].Name.In(arrayAttributeNames)) {
						xmlNode.Attributes.RemoveAt(i);
					}
				}
			}
		}
		#endregion XML

		///<summary>Lets a test's handler be reused across HttpClients: disposing the client doesn't dispose the handler the test still holds.</summary>
		private sealed class NonDisposingHandler:DelegatingHandler {
			public NonDisposingHandler(HttpMessageHandler innerHandler) : base(innerHandler) {
			}

			protected override void Dispose(bool disposing) {
				//Leave InnerHandler alone; its owner disposes it.
			}
		}
	}
}
