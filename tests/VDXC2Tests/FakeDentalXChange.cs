// Stand-ins for DentalXChange's two attachment services, so the V-DXC2 tests never reach the network:
//  - RecordingApiRequest replaces APIRequest.Inst, which every XConnect call goes through, and answers with canned JSON.
//  - FakeDeaWebRequestCreate is registered with WebRequest for the partner service's hosts, so the SOAP client's HTTP request is answered
//    in memory with a canned SOAP response. Unless a test has turned it on, it refuses every request instead of passing it on.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
using OpenDentBusiness;
using OpenDentBusiness.WebBridges;

namespace VDXC2Tests {
	///<summary>Records every XConnect request and answers from DictResponses (endpoint to JSON). Unknown endpoints throw.</summary>
	public class RecordingApiRequest:IApiRequest {
		public List<ApiCall> ListCalls=new List<ApiCall>();
		public Dictionary<string,string> DictResponses=new Dictionary<string,string>();

		public T SendRequest<T>(string urlEndpoint,HttpMethod method,AuthenticationHeaderValue authHeaderVal,string body,HttpContentType contentType,
			HttpClient clientOverride,List<string> queryParameters,JsonSerializerSettings deserializeSettings)
		{
			ApiCall apiCall=new ApiCall();
			apiCall.Endpoint=urlEndpoint;
			apiCall.Method=method;
			apiCall.Body=body;
			apiCall.ListQueryParameters=queryParameters==null ? new List<string>() : queryParameters.ToList();
			if(clientOverride!=null) {
				apiCall.BaseAddress=clientOverride.BaseAddress;
				apiCall.ApiKey=GetHeader(clientOverride,"API-Key");
				apiCall.Username=GetHeader(clientOverride,"username");
			}
			ListCalls.Add(apiCall);
			string json;
			if(!DictResponses.TryGetValue(urlEndpoint,out json)) {
				throw new InvalidOperationException("Unexpected XConnect endpoint in a test: "+urlEndpoint);
			}
			return JsonConvert.DeserializeObject<T>(json,deserializeSettings);
		}

		private static string GetHeader(HttpClient httpClient,string name) {
			IEnumerable<string> values;
			if(httpClient.DefaultRequestHeaders.TryGetValues(name,out values)) {
				return values.FirstOrDefault();
			}
			return null;
		}

		public T SendRequest<T,U>(string urlEndpoint,HttpMethod method,AuthenticationHeaderValue authHeaderVal,U body,HttpContentType contentType,
			HttpClient clientOverride,List<string> queryParameters,JsonSerializerSettings deserializeSettings)
		{
			throw new NotSupportedException();
		}

		public Task<T> GetRequestAsync<T>(string urlEndpoint,HttpClient clientOverride) {
			throw new NotSupportedException();
		}

		public Task<T> PostRequestAsync<T>(string urlEndpoint,string body,HttpContentType contentType,HttpClient clientOverride) {
			throw new NotSupportedException();
		}

		public Task<T> SendRequestAsync<T,U>(string urlEndpoint,HttpMethod method,AuthenticationHeaderValue authHeaderVal,U body,HttpContentType contentType,
			HttpClient clientOverride,List<string> queryParameters,JsonSerializerSettings deserializeSettings)
		{
			throw new NotSupportedException();
		}
	}

	public class ApiCall {
		public string Endpoint;
		public HttpMethod Method;
		public string Body;
		public List<string> ListQueryParameters;
		public Uri BaseAddress;
		public string ApiKey;
		public string Username;
	}

	///<summary>One SOAP request the partner service client sent: the address, the operation (first element of the SOAP body) and the whole request.
	///</summary>
	public class SoapCall {
		public Uri Uri;
		public string Operation;
		public XmlDocument XmlDocumentRequest;
	}

	///<summary>Answers the DentalXChange partner service (DEA) in memory. Registered once per test run for both of its hosts; while IsOn is false it
	///refuses every request, so nothing can reach DentalXChange by accident.</summary>
	public class FakeDeaWebRequestCreate:IWebRequestCreate {
		public static readonly FakeDeaWebRequestCreate Inst=new FakeDeaWebRequestCreate();
		private static bool _isRegistered;
		public bool IsOn;
		public List<SoapCall> ListCalls=new List<SoapCall>();
		///<summary>Operation name to the object returned in the SOAP response's "return" element.</summary>
		public Dictionary<string,object> DictReturns=new Dictionary<string,object>();

		public static void EnsureRegistered() {
			if(_isRegistered) {
				return;
			}
			WebRequest.RegisterPrefix("https://prelive2.dentalxchange.com/dea/",Inst);//Debug builds
			WebRequest.RegisterPrefix("https://webservices.dentalxchange.com/dea/",Inst);//Release builds
			_isRegistered=true;
		}

		public void Reset() {
			IsOn=false;
			ListCalls.Clear();
			DictReturns.Clear();
		}

		public WebRequest Create(Uri uri) {
			if(!IsOn) {
				throw new WebException("Test: the DentalXChange partner service is not answered by this test.");
			}
			return new FakeWebRequest(uri,this);
		}

		///<summary>Called with the request body when the client asks for the response.</summary>
		public byte[] Answer(Uri uri,byte[] arrayBytesRequest) {
			XmlDocument xmlDocument=new XmlDocument();
			xmlDocument.LoadXml(Encoding.UTF8.GetString(arrayBytesRequest).TrimStart('﻿'));
			XmlNode xmlNodeBody=xmlDocument.DocumentElement.ChildNodes.Cast<XmlNode>().First(x => x.LocalName=="Body");
			string operation=xmlNodeBody.ChildNodes.Cast<XmlNode>().First(x => x.NodeType==XmlNodeType.Element).LocalName;
			ListCalls.Add(new SoapCall { Uri=uri,Operation=operation,XmlDocumentRequest=xmlDocument });
			object returnValue;
			if(!DictReturns.TryGetValue(operation,out returnValue)) {
				throw new WebException("Test: unexpected partner service operation "+operation);
			}
			return Encoding.UTF8.GetBytes(BuildEnvelope(operation,returnValue));
		}

		///<summary>A SOAP 1.1 document/literal wrapped response: operationResponse in the dxci.dea namespace, holding an unqualified "return" element
		///serialized with the generated proxy's own type mappings.</summary>
		public static string BuildEnvelope(string operation,object returnValue) {
			XmlSerializer xmlSerializer=new XmlSerializer(returnValue.GetType(),new XmlRootAttribute("return") { Namespace="" });
			StringBuilder stringBuilder=new StringBuilder();
			XmlWriterSettings xmlWriterSettings=new XmlWriterSettings { OmitXmlDeclaration=true };
			using(XmlWriter xmlWriter=XmlWriter.Create(stringBuilder,xmlWriterSettings)) {
				xmlSerializer.Serialize(xmlWriter,returnValue);
			}
			return "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
				+"<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>"
				+"<ns2:"+operation+"Response xmlns:ns2=\"dxci.dea\">"+stringBuilder.ToString()+"</ns2:"+operation+"Response>"
				+"</soap:Body></soap:Envelope>";
		}
	}

	public class FakeWebRequest:WebRequest {
		private readonly Uri _uri;
		private readonly FakeDeaWebRequestCreate _fakeDeaWebRequestCreate;
		private readonly MemoryStream _memoryStreamRequest=new MemoryStream();
		private WebHeaderCollection _webHeaderCollection=new WebHeaderCollection();

		public FakeWebRequest(Uri uri,FakeDeaWebRequestCreate fakeDeaWebRequestCreate) {
			_uri=uri;
			_fakeDeaWebRequestCreate=fakeDeaWebRequestCreate;
		}

		public override string Method { get; set; }="GET";
		public override Uri RequestUri => _uri;
		public override WebHeaderCollection Headers { get => _webHeaderCollection; set => _webHeaderCollection=value; }
		public override string ContentType { get; set; }
		public override long ContentLength { get; set; }
		public override int Timeout { get; set; }
		public override ICredentials Credentials { get; set; }
		public override bool PreAuthenticate { get; set; }
		public override string ConnectionGroupName { get; set; }
		public override IWebProxy Proxy { get; set; }
		public override bool UseDefaultCredentials { get; set; }

		public override Stream GetRequestStream() {
			return _memoryStreamRequest;
		}

		public override WebResponse GetResponse() {
			byte[] arrayBytesResponse=_fakeDeaWebRequestCreate.Answer(_uri,_memoryStreamRequest.ToArray());
			return new FakeWebResponse(_uri,arrayBytesResponse);
		}

		public override void Abort() {
		}
	}

	public class FakeWebResponse:WebResponse {
		private readonly Uri _uri;
		private readonly byte[] _arrayBytes;
		private readonly WebHeaderCollection _webHeaderCollection=new WebHeaderCollection();

		public FakeWebResponse(Uri uri,byte[] arrayBytes) {
			_uri=uri;
			_arrayBytes=arrayBytes;
		}

		public override string ContentType { get => "text/xml; charset=utf-8"; set { } }
		public override long ContentLength { get => _arrayBytes.Length; set { } }
		public override Uri ResponseUri => _uri;
		public override WebHeaderCollection Headers => _webHeaderCollection;

		public override Stream GetResponseStream() {
			return new MemoryStream(_arrayBytes);
		}

		public override void Close() {
		}
	}
}
