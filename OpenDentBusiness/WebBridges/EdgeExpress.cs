using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json;
using OpenDentBusiness.WebTypes.Shared.XWeb;
using WebServiceSerializer;

namespace OpenDentBusiness {
	public class EdgeExpress {
		///<summary>The program property names this integration added for the vendor-direct pay page (docs/specs/vendor-direct/edgeexpress.md 3.5).
		///The older names are in ProgramProperties.PropertyDescs.EdgeExpress. Missing rows mean the default; the setup window inserts them.</summary>
		public static class PropertyDescs {
			///<summary>Per clinic. Where the hosted pay page sends the browser after the card is submitted. Blank means CNP.ReturnUrlDefault.
			///Ignored in Loopback mode, where the address is made per session.</summary>
			public const string ReturnUrl="EdgeExpressReturnUrl";
			///<summary>Per clinic. "Embedded" (default) or "Loopback". See EdgeExpressReturnMode.</summary>
			public const string ReturnMode="EdgeExpressReturnMode";
			///<summary>Practice level (ClinicNum 0). "1" sends card-not-present requests to the card processor's test gateway.</summary>
			public const string UseTestGateway="EdgeExpressUseTestGateway";
		}

		public static List<EdgeExpressTransType> ListTerminalTransTypes=new List<EdgeExpressTransType>() {
			EdgeExpressTransType.CreditSale,EdgeExpressTransType.CreditReturn,EdgeExpressTransType.DebitSale,EdgeExpressTransType.DebitReturn,
			EdgeExpressTransType.CreditOnlineCapture,EdgeExpressTransType.CreditAuth,EdgeExpressTransType.CreditVoid
		};

		public static List<EdgeExpressTransType> ListWebTransTypes=new List<EdgeExpressTransType>() {
			EdgeExpressTransType.CreditSale,EdgeExpressTransType.CreditReturn,EdgeExpressTransType.CreditOnlineCapture,
			EdgeExpressTransType.CreditAuth,EdgeExpressTransType.CreditVoid
		};

		#region Private methods
		///<summary>Adds the base params for an EdgeExpress request.</summary>
		private static void WriteEdgeExpressBaseRequest(long clinicNum,EdgeExpressTransType edgeExpressTransactionType,XmlWriter xmlWriter,bool isWebPayment=true,double amount=0) {
			WebPaymentProperties xwebProperties=GetCredsForRequest(clinicNum,isWebPayment,amount);
			WriteEdgeExpressBaseRequest(xwebProperties,edgeExpressTransactionType,xmlWriter);
		}

		///<summary>Reads the clinic's credentials (the Auth Key decrypted) and checks that a request may be sent. Throws ODException.</summary>
		private static WebPaymentProperties GetCredsForRequest(long clinicNum,bool isWebPayment,double amount) {
			ProgramProperties.GetXWebCreds(clinicNum,out WebPaymentProperties xwebProperties);
			Program progEdge=Programs.GetCur(ProgramName.EdgeExpress);
			Program progXWeb=Programs.GetCur(ProgramName.Xcharge);
			if((progEdge==null || !progEdge.Enabled) && (progXWeb==null || !progXWeb.Enabled)) {
				throw new ODException("Both XWeb and EdgeExpress are not enabled. Please enable one of these programs.",ODException.ErrorCodes.XWebProgramProperties);
			}
			if(isWebPayment && !xwebProperties.IsPaymentsAllowed) {
				throw new ODException("Clinic or Practice has online payments disabled",ODException.ErrorCodes.XWebProgramProperties);
			}
			ODException.SwallowAnyException(() => Logger.LogVerbose(GetRequestLogText(xwebProperties,amount),subDirectory:"EdgeExpress"));
			return xwebProperties;
		}

		///<summary>Starts the REQUEST element and writes the merchant credentials and the transaction type.</summary>
		private static void WriteEdgeExpressBaseRequest(WebPaymentProperties xwebProperties,EdgeExpressTransType edgeExpressTransactionType,XmlWriter xmlWriter) {
			xmlWriter.WriteStartElement("REQUEST");
			xmlWriter.WriteElementString("XWEBID",xwebProperties.XWebID);
			xmlWriter.WriteElementString("XWEBTERMINALID",xwebProperties.TerminalID);
			xmlWriter.WriteElementString("XWEBAUTHKEY",xwebProperties.AuthKey);
			xmlWriter.WriteElementString("TRANSACTIONTYPE",edgeExpressTransactionType.ToString().ToUpper());
		}

		public static string GetRequestLogText(WebPaymentProperties xwebProperties,double amount) {
			string terminalID="NULL";
			string authKey="NULL";
			if(xwebProperties!=null) {
				if(xwebProperties.TerminalID!=null) {
					terminalID=xwebProperties.TerminalID;
				}
				if(xwebProperties.AuthKey!=null) {
					//Only display the first half of the AuthKey.
					int countVisibleChars=(xwebProperties.AuthKey.Length / 2);
					authKey=xwebProperties.AuthKey.Substring(0,countVisibleChars) + new string('X',xwebProperties.AuthKey.Length-countVisibleChars);
				}
			}
			return $"TerminalID:{terminalID}  AuthKey:{authKey}  Amount:{amount:C}";
		}

			///<summary> Removes { } [ ] < > ‘ ’ and trailing white space from passed in string </summary>
			public static string CleanString(string str) {
				if(string.IsNullOrEmpty(str)) {
					return "";
				}
				string	reg=Regex.Replace(str,"[{,},\\[,\\],<,>,‘,’]","");
				return reg.TrimEnd();
			}
		#endregion Private methods

		#region Helper classes
		///<summary>Represents a response from the EdgeExpress RCM program.</summary>
		public class RcmResponse {
			public string DUPLICATECARD;
			public DateTime DATE_TIME;
			public string HOSTRESPONSECODE;
			public string HOSTRESPONSEDESCRIPTION;
			///<summary>Only included if the transaction failed.</summary>
			public string RESPONSE="-1";
			///<summary>Only included if the transaction succeeded.</summary>
			public string RESULT="-1";
			public string RESULTMSG;
			public string APPROVEDAMOUNT;
			public string BATCHNO;
			public string BATCHAMOUNT;
			public string APPROVALCODE;
			public string ACCOUNT;
			public string CARDCODERESPONSE;
			public string CARDTYPE;
			public string CARDBRAND;
			public string CARDBRANDSHORT;
			public string LANGUAGE;
			public string ALIAS;
			public string ENTRYTYPE;
			public string RECEIPTTEXT;
			public string EXPMONTH;
			public string EXPYEAR;
			public string TRANSACTIONID;
			///<summary>Not a part of the response from EdgeExpress.</summary>
			public EdgeExpressTransType TransType;
			///<summary>Whether or not the tranaction was successful.</summary>
			public bool IsSuccess => RESULT=="0";

			///<summary>Returns a string that can be used as the note for a payment.</summary>
			public string GetPayNote() {
				StringBuilder strbNote=new StringBuilder();
				strbNote.AppendLine(Lans.g("EdgeExpress","TRANSACTIONTYPE:")+" "+TransType);
				foreach(FieldInfo fi in GetType().GetFields().Where(x => x.IsPublic).OrderByDescending(x => x.Name==nameof(RESULTMSG))) {
					if(fi.Name.In(nameof(RECEIPTTEXT),nameof(ALIAS),nameof(TransType))
						|| (fi.Name.In(nameof(RESPONSE),nameof(RESULT)) && fi.GetValue(this).ToString()=="-1")) 
					{
						continue;
					}
					object value=fi.GetValue(this);
					if(value==null || (value.GetType()==typeof(string) && string.IsNullOrEmpty(value.ToString()))
						|| (value.GetType()==typeof(DateTime) && ((DateTime)value).Year < 1880)) 
					{
						continue;
					}
					strbNote.AppendLine(fi.Name+": "+value);
				}
				return strbNote.ToString();
			}
		}

		#endregion Helper classes
		#region RCM Methods
		///<summary>This class uses the locally installed RCM client to send transactions to EdgeExpress via a physical payment terminal.</summary>
		public class RCM {
			///<summary>The URL to send the request to the RCM program running on the local machine. localsystem.paygateway.com resolves to 127.0.0.1.
			///</summary>
			private const string _edgeExpressRCMURL="https://localsystem.paygateway.com:21113/RcmService.svc/Initialize";

			///<summary>Checks the process list of the local machine to see if there is a program called "RCM" currently running.</summary>
			public static bool IsRCMRunning {
				get {
					bool isRCMRunning=false;
					try {
						isRCMRunning=Process.GetProcesses().Any(x => x.ProcessName.ToLower().Contains("rcm"));
					}
					catch(Exception ex) {
						ex.DoNothing();
					}
					return isRCMRunning;
				}
			}

			///<summary>Sends the request to EdgeExpress RCM and returns the response.</summary>
			public static RcmResponse SendEdgeExpressRequest(Patient patient,long clinicNum,EdgeExpressTransType edgeExpressTransactionType,bool isWebPayment,double amount = 0,
				bool doPromptForSignature = false,bool doCreateToken = false,string aliasToken = "",string transactionId = "",decimal cashBackAmt = 0,string expDate = "") {
				RcmResponse rcmResponse=null;
				StringBuilder strBldXml=new StringBuilder();
				XmlWriter xmlWriter=XmlWriter.Create(strBldXml);
				WriteEdgeExpressBaseRequest(clinicNum,edgeExpressTransactionType,xmlWriter,isWebPayment,amount);
				AddOtherParamsEdgeExpress(xmlWriter,edgeExpressTransactionType,patient,amount,doPromptForSignature,doCreateToken,aliasToken,transactionId,
					cashBackAmt,expDate);
				string url=$"{_edgeExpressRCMURL}?xl2Parameters={strBldXml}";
				string response;
				using WebClient client=new WebClient();
				response=client.DownloadString(url);
				//Response will look like: 
				//{"Description":"OK","IsSuccessful":true,"RcmResponse":"{\"RESPONSE\":{\"RESPONSE\":\"3\",\"RESULTMSG\":\"Transaction Cancelled\"}}","XmlRcmResponse":null}
				var jsonResponse=new {
					Description="",
					IsSuccessful=false,
					RcmResponse="",
				};
				jsonResponse=JsonConvert.DeserializeAnonymousType(response,jsonResponse);
				if(!jsonResponse.IsSuccessful) {
					throw new ODException("Error from EdgeExpress: "+jsonResponse.Description);
				}
				var innerResponse=new {
					RESPONSE=new RcmResponse(),
				};
				innerResponse=JsonConvert.DeserializeAnonymousType(jsonResponse.RcmResponse,innerResponse);
				rcmResponse=innerResponse.RESPONSE;
				rcmResponse.TransType=edgeExpressTransactionType;
				//},startingMessage:"Processing credit card transaction...");
				return rcmResponse;
			}

			///<summary>Adds additional parameters to the specified transaction type. For EdgeExpress RCM only.</summary>
			private static void AddOtherParamsEdgeExpress(XmlWriter xmlWriter,EdgeExpressTransType edgeExpressTransactionType,Patient patient,
				double amount,bool doPromptForSignature,bool doCreateToken,string aliasToken,string transactionId,decimal cashBackAmt,string expDate) {
				if(edgeExpressTransactionType.In(EdgeExpressTransType.CreditSale,EdgeExpressTransType.CreditReturn,EdgeExpressTransType.CreditAuth,
					EdgeExpressTransType.CreditOnlineCapture,EdgeExpressTransType.DebitSale,EdgeExpressTransType.DebitReturn)) {
					xmlWriter.WriteElementString("AMOUNT",amount.ToString());
				}
				//EdgeExpress only Accepts CLERK Id's up to 20 characters long. Payment will fail if UserName is over 20 characters long.
				if(edgeExpressTransactionType.In(EdgeExpressTransType.CreditSale,EdgeExpressTransType.CreditReturn,EdgeExpressTransType.CreditAuth)) {
					xmlWriter.WriteElementString("RECEIPTLINEITEMS","Pat"+patient.PatNum);
					xmlWriter.WriteElementString("PROMPTSIGNATURE",doPromptForSignature ? "True" : "False");
					xmlWriter.WriteElementString("CREATEALIAS",doCreateToken ? "TRUE" : "FALSE");
					if(!string.IsNullOrEmpty(aliasToken)) {
						xmlWriter.WriteElementString("ALIAS",aliasToken);
					}
					xmlWriter.WriteElementString("INVOICENO",$"PAT{patient.PatNum}");
					xmlWriter.WriteElementString("CLERK",StringTools.Truncate(Security.CurUser.UserName,20));
				}
				if(edgeExpressTransactionType.In(EdgeExpressTransType.AliasUpdate,EdgeExpressTransType.AliasDelete)) {
					xmlWriter.WriteElementString("ALIAS",aliasToken);
				}
				if(edgeExpressTransactionType.In(EdgeExpressTransType.AliasUpdate)) {
					xmlWriter.WriteElementString("EXPDATE",expDate);
				}
				if(edgeExpressTransactionType.In(EdgeExpressTransType.CreditVoid,EdgeExpressTransType.CreditReturn,EdgeExpressTransType.CreditOnlineCapture)) {
					xmlWriter.WriteElementString("TRANSACTIONID",transactionId);
				}
				if(edgeExpressTransactionType.In(EdgeExpressTransType.CreditOnlineCapture)) {
					xmlWriter.WriteElementString("INVOICENO",$"PAT{patient.PatNum}");
					xmlWriter.WriteElementString("CLERK",StringTools.Truncate(Security.CurUser.UserName,20));
				}
				if(edgeExpressTransactionType.In(EdgeExpressTransType.DebitSale)) {
					xmlWriter.WriteElementString("CASHBACKAMOUNT",cashBackAmt.ToString());
					xmlWriter.WriteElementString("INVOICENO",$"PAT{patient.PatNum}");
					xmlWriter.WriteElementString("CLERK",StringTools.Truncate(Security.CurUser.UserName,20));
				}
				if(edgeExpressTransactionType.In(EdgeExpressTransType.DebitReturn)) {
					xmlWriter.WriteElementString("INVOICENO",$"PAT{patient.PatNum}");
					xmlWriter.WriteElementString("CLERK",StringTools.Truncate(Security.CurUser.UserName,20));
				}
				//Include the patient first and last name with every transaction type if they are available.
				if(!string.IsNullOrWhiteSpace(patient.FName)) {
					xmlWriter.WriteElementString("CUSTOMERFIRSTNAME",CleanString(patient.FName));
				}
				if(!string.IsNullOrWhiteSpace(patient.LName)) {
					xmlWriter.WriteElementString("CUSTOMERLASTNAME",CleanString(patient.LName));
				}
				xmlWriter.WriteEndElement();//REQUEST
				xmlWriter.Flush();
			}

			///<summary>Creates a credit card alias.</summary>
			public static RcmResponse CreateAlias(Patient patient,long clinicNum,bool isWebPayment) {
				return SendEdgeExpressRequest(patient,clinicNum,EdgeExpressTransType.AliasCreate,isWebPayment,doCreateToken: true);
			}

			///<summary>Updates the expiration date for the alias.</summary>
			public static RcmResponse UpdateAlias(Patient patient,long clinicNum,string aliasToken,DateTime expDate,bool isWebPayment) {
				return SendEdgeExpressRequest(patient,clinicNum,EdgeExpressTransType.AliasUpdate,isWebPayment,aliasToken: aliasToken,expDate: expDate.ToString("MMyy"));
			}

			///<summary>Deletes the alias.</summary>
			public static RcmResponse DeleteAlias(Patient patient,long clinicNum,string aliasToken,bool isWebPayment) {
				return SendEdgeExpressRequest(patient,clinicNum,EdgeExpressTransType.AliasDelete,isWebPayment,aliasToken: aliasToken);
			}

			///<summary>For credit only, not debit. Voids the transaction.</summary>
			public static RcmResponse VoidTransaction(Patient patient,long clinicNum,string transactionId,bool isWebPayment) {
				return SendEdgeExpressRequest(patient,clinicNum,EdgeExpressTransType.CreditVoid,isWebPayment,transactionId: transactionId);
			}
		}
		#endregion RCM Methods
		#region Card Not Present API

		///<summary>This class makes calls to the EdgeExpress Card Not Present API.</summary>
		public class CNP {
			///<summary>Where the hosted payment page sends the browser after the card is submitted, when the clinic has no return URL of its own. The
			///.invalid top level domain is reserved (RFC 2606, RFC 6761) and never resolves, so nothing is contacted: the pay-page window cancels the
			///navigation before any request goes out and closes (FormEdgeExpressPayPage, or FormWebBrowser.UrlCloseOn). The caller then asks
			///EdgeExpress for the result, so nothing the return carries is ever read.</summary>
			public const string ReturnUrlDefault="https://edgeexpress-return.invalid/PayDone";
			///<summary>The path of a loopback return address. A per-session random nonce follows it.</summary>
			public const string ReturnPathPrefix="/edgeexpress/return/";
			///<summary>Longest return URL the setup window accepts, so the pay-page request stays under the gateway's size limit.</summary>
			public const int ReturnUrlMaxLength=200;
			///<summary>The gateway refuses requests of this many bytes or more (response code 801).</summary>
			public const int MaxRequestBytes=XWebs.XWebInputAbs.MaxRequestBytes;
			///<summary>How long a pay-page session is treated as open: the pay-page window closes after this, and HpfExpiration is set to now plus this.
			///Open Dental used 10 minutes; a public third-party walkthrough says the page itself expires after 5. Confirm with sandbox test T5
			///(docs/specs/vendor-direct/edgeexpress.md section 6) and adjust.</summary>
			public static readonly TimeSpan PayPageSessionLifetime=TimeSpan.FromMinutes(10);
			///<summary>How long past HpfExpiration a session that is still pending is kept pending before it is marked expired.</summary>
			public static readonly TimeSpan PendingGrace=TimeSpan.FromMinutes(5);
			///<summary>Reconciliation looks at pending sessions whose HpfExpiration is no older than this.</summary>
			public static readonly TimeSpan ReconcileWindow=TimeSpan.FromMinutes(30);
			///<summary>After the pay-page window closes, the result is asked for once and then up to this many more times while it is still pending.
			///</summary>
			public static int RepollCount=3;
			///<summary>The wait between those re-polls.</summary>
			public static TimeSpan RepollDelay=TimeSpan.FromSeconds(3);
			public const string HostPayUrlProduction="https://ee.paygateway.com/HostPayService/v1/hostpay/transactions/";
			public const string HostPayUrlTest="https://ee.test.paygateway.com/HostPayService/v1/hostpay/transactions/";
			public const string DirectPayUrlProduction="https://ee.paygateway.com/HostPayService/v1/directpay/express";
			public const string DirectPayUrlTest="https://ee.test.paygateway.com/HostPayService/v1/directpay/express";
			///<summary>Besides the host of PAYPAGEURL itself, the hosts the pay-page window may navigate to at the top level.</summary>
			public static readonly string[] ArrayPayPageHostPatterns=new string[] { "*.paygateway.com" };
			///<summary>The page the loopback listener answers the return with. Self-contained: no scripts, images or branding.</summary>
			public const string ReturnPageHtml="<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Payment step finished</title>"
				+"<style>body{font-family:sans-serif;margin:3em;}</style></head>"
				+"<body><p>Payment step finished. You can close this tab and return to the program.</p></body></html>";

			///<summary>True if card-not-present requests go to the card processor's test gateway: a debug build, the UseXWebTestGateway node in the
			///local connection config, or the practice-level "Use test gateway" setting in the EdgeExpress setup window.</summary>
			public static bool IsTestGateway() {
				if(ODBuild.IsDebug() || XWebs.UseXWebTestGateway) {
					return true;
				}
				long programNum=Programs.GetProgramNum(ProgramName.EdgeExpress);
				if(programNum==0) {
					return false;
				}
				return ProgramProperties.GetValOrDefault(programNum,PropertyDescs.UseTestGateway,0).Trim()=="1";
			}

			///<summary>Url to setup a payment page and get a OTK.</summary>
			public static string GetHostPayUrl() {
				string edgeExpressHostPayUrl=IsTestGateway() ? HostPayUrlTest : HostPayUrlProduction;
				return Introspection.GetOverride(Introspection.IntrospectionEntity.EdgeExpressHostPay,edgeExpressHostPayUrl);
			}

			///<summary>Url for transactions that do not require a payment page.</summary>
			public static string GetDirectPayUrl() {
				string edgeExpressDirectPayUrl=IsTestGateway() ? DirectPayUrlTest : DirectPayUrlProduction;
				return Introspection.GetOverride(Introspection.IntrospectionEntity.EdgeExpressDirectPay,edgeExpressDirectPayUrl);
			}

			///<summary>The return URL for the clinic's pay pages in Embedded mode: the clinic's EdgeExpressReturnUrl (else the practice's), or
			///ReturnUrlDefault when that is blank or not usable.</summary>
			public static string GetReturnUrl(long clinicNum) {
				long programNum=Programs.GetProgramNum(ProgramName.EdgeExpress);
				if(programNum==0) {
					return ReturnUrlDefault;
				}
				string returnUrl=ProgramProperties.GetValOrDefault(programNum,PropertyDescs.ReturnUrl,clinicNum).Trim();
				if(returnUrl=="" || ValidateReturnUrl(returnUrl)!="") {
					return ReturnUrlDefault;
				}
				return returnUrl;
			}

			///<summary>Returns "" if returnUrl may be saved as a clinic's return URL (blank means the built-in default), otherwise the reason it can't.
			///It must be a full https address (plain http only to this computer), never an Open Dental host, with no fragment.</summary>
			public static string ValidateReturnUrl(string returnUrl) {
				if(string.IsNullOrWhiteSpace(returnUrl)) {
					return "";
				}
				string returnUrlTrimmed=returnUrl.Trim();
				if(returnUrlTrimmed.Length>ReturnUrlMaxLength) {
					return "The return URL can be at most "+ReturnUrlMaxLength+" characters.";
				}
				Uri uri;
				if(!Uri.TryCreate(returnUrlTrimmed,UriKind.Absolute,out uri) || (uri.Scheme!=Uri.UriSchemeHttps && uri.Scheme!=Uri.UriSchemeHttp)) {
					return "The return URL must be a full https address.";
				}
				VendorUrlProblem vendorUrlProblem=VendorUrlPolicy.Check(uri);
				if(vendorUrlProblem!=VendorUrlProblem.None) {
					return VendorUrlPolicy.Describe(vendorUrlProblem);
				}
				if(uri.Fragment!="") {
					return "The return URL can't contain '#'.";
				}
				return "";
			}

			///<summary>The clinic's return mode (else the practice's). Missing or unreadable means Embedded.</summary>
			public static EdgeExpressReturnMode GetReturnMode(long clinicNum) {
				long programNum=Programs.GetProgramNum(ProgramName.EdgeExpress);
				if(programNum==0) {
					return EdgeExpressReturnMode.Embedded;
				}
				return ParseReturnMode(ProgramProperties.GetValOrDefault(programNum,PropertyDescs.ReturnMode,clinicNum));
			}

			///<summary>Reads a stored return mode. Anything but "Loopback" (any case) is Embedded.</summary>
			public static EdgeExpressReturnMode ParseReturnMode(string value) {
				if(string.Equals((value??"").Trim(),EdgeExpressReturnMode.Loopback.ToString(),StringComparison.OrdinalIgnoreCase)) {
					return EdgeExpressReturnMode.Loopback;
				}
				return EdgeExpressReturnMode.Embedded;
			}

			///<summary>The clinic whose credentials a new session for this patient uses, by the same rules the requests below follow: 0 without
			///clinics; else clinicNum when it is set; else the current clinic when doUseCurrentClinicNum; else the patient's clinic. Callers use it to
			///read the return settings of the clinic before they create the session.</summary>
			public static long ResolveClinicNum(long patNum,long clinicNum=-1,bool doUseCurrentClinicNum=false) {
				if(!PrefC.HasClinicsEnabled) {
					return 0;
				}
				if(clinicNum>0) {
					return clinicNum;
				}
				if(doUseCurrentClinicNum) {
					return Clinics.ClinicNum;
				}
				return Patients.GetLim(patNum).ClinicNum;
			}

			///<summary>Starts the one-shot loopback listener for Loopback mode on 127.0.0.1 (or [::1] if IPv4 can't bind), port chosen by the
			///operating system, path ReturnPathPrefix plus a 128-bit random nonce. It also accepts a POST return and answers with ReturnPageHtml.
			///Put its RedirectUri in the session request as the return URL. Returns null if it can't bind; the caller then uses Embedded mode.</summary>
			public static LoopbackListener StartReturnListener() {
				string path=ReturnPathPrefix+Base64Url.RandomString(16);
				string[] arrayHosts=new string[] { "127.0.0.1","[::1]" };
				for(int i=0;i<arrayHosts.Length;i++) {
					LoopbackListener loopbackListener=new LoopbackListener(0,path,arrayHosts[i]);
					loopbackListener.AcceptPost=true;
					loopbackListener.ResponseHtml=ReturnPageHtml;
					try {
						loopbackListener.Start();
						return loopbackListener;
					}
					catch(SocketException ex) {
						loopbackListener.Dispose();
						ODException.SwallowAnyException(() => Logger.WriteLine("Loopback return listener could not bind "+arrayHosts[i]+": "+ex.Message,"EdgeExpress"));
					}
				}
				return null;
			}

			///<summary>True if uri is a navigation to the return URL: same scheme, host and port, and a path that starts with the return URL's path
			///(letter case ignored, compared as parsed absolute URIs).</summary>
			public static bool IsReturnNavigation(string uri,string returnUrl) {
				if(string.IsNullOrWhiteSpace(uri) || string.IsNullOrWhiteSpace(returnUrl)) {
					return false;
				}
				Uri uriNavigation;
				Uri uriReturn;
				if(!Uri.TryCreate(uri.Trim(),UriKind.Absolute,out uriNavigation) || !Uri.TryCreate(returnUrl.Trim(),UriKind.Absolute,out uriReturn)) {
					return false;
				}
				if(!string.Equals(uriNavigation.Scheme,uriReturn.Scheme,StringComparison.OrdinalIgnoreCase)
					|| !string.Equals(uriNavigation.Host,uriReturn.Host,StringComparison.OrdinalIgnoreCase)
					|| uriNavigation.Port!=uriReturn.Port)
				{
					return false;
				}
				return uriNavigation.AbsolutePath.StartsWith(uriReturn.AbsolutePath,StringComparison.OrdinalIgnoreCase);
			}

			///<summary>What the pay-page window does with a navigation. Return: the card processor is done; cancel it and close. Allow: the pay page
			///itself (its host, or another *.paygateway.com host) or about:blank. A frame may also load any other https page that isn't an Open Dental
			///host, since it can't take over the window. Block: everything else, including plain http, data: and javascript: addresses.</summary>
			public static EdgeExpressPayPageNavigation ClassifyPayPageNavigation(string uri,string payPageUrl,string returnUrl,bool isFrame=false) {
				if(IsReturnNavigation(uri,returnUrl)) {
					return EdgeExpressPayPageNavigation.Return;
				}
				if(string.IsNullOrWhiteSpace(uri)) {
					return EdgeExpressPayPageNavigation.Block;
				}
				if(uri.Equals("about:blank",StringComparison.OrdinalIgnoreCase) || (isFrame && uri.Equals("about:srcdoc",StringComparison.OrdinalIgnoreCase))) {
					return EdgeExpressPayPageNavigation.Allow;
				}
				Uri uriNavigation;
				if(!Uri.TryCreate(uri.Trim(),UriKind.Absolute,out uriNavigation) || VendorUrlPolicy.Check(uriNavigation)!=VendorUrlProblem.None) {
					return EdgeExpressPayPageNavigation.Block;
				}
				if(isFrame) {
					return EdgeExpressPayPageNavigation.Allow;
				}
				Uri uriPayPage;
				if(Uri.TryCreate((payPageUrl??"").Trim(),UriKind.Absolute,out uriPayPage)
					&& string.Equals(uriPayPage.Host,uriNavigation.Host,StringComparison.OrdinalIgnoreCase))
				{
					return EdgeExpressPayPageNavigation.Allow;
				}
				if(uriNavigation.Scheme==Uri.UriSchemeHttps && ArrayPayPageHostPatterns.Any(x => VendorUrlPolicy.HostMatches(uriNavigation.Host,x))) {
					return EdgeExpressPayPageNavigation.Allow;
				}
				return EdgeExpressPayPageNavigation.Block;
			}

			///<summary>A message for the user about a result that isn't a plain approval or decline, or "" if there is nothing special to say.
			///Pending (still unknown) results get the "will keep checking" message.</summary>
			public static string GetUserMessage(XWebResponse xWebResponse) {
				if(xWebResponse==null) {
					return "";
				}
				switch(xWebResponse.XWebResponseCode) {
					case XWebResponseCodes.AuthenticationError:
						return Lans.g("EdgeExpress","The card processor rejected the X-Web ID, Terminal ID or Auth Key for this clinic. "
							+"Check them in Setup > Program Links > EdgeExpress.");
					case XWebResponseCodes.ProductionMerchantSetUpError:
					case XWebResponseCodes.TestMerchantSetUpError:
					case XWebResponseCodes.DevelopmentMerchantSetUpError:
						return Lans.g("EdgeExpress","The card processor says these credentials are for a different environment (test or production). "
							+"Check the credentials and the test gateway setting in Setup > Program Links > EdgeExpress.");
					case XWebResponseCodes.DuplicateTransactionError:
						return Lans.g("EdgeExpress","The card processor refused this as a duplicate of a recent transaction. No second charge was made.");
					case XWebResponseCodes.MerchantLockedForSettlement:
					case XWebResponseCodes.MerchantLockedForMaintenance:
					case XWebResponseCodes.TemporaryServiceOutage:
						return Lans.g("EdgeExpress","The card processor is temporarily unavailable. Try again in a minute.");
					case XWebResponseCodes.MaxRequestDataExceededError:
						return Lans.g("EdgeExpress","The request to the card processor was too large. No charge was made.");
					case XWebResponseCodes.ExpiredWithoutApproval:
					case XWebResponseCodes.SessionTimedOut:
						return Lans.g("EdgeExpress","The card entry page expired. No charge was made.");
					case XWebResponseCodes.TransactionCancelled:
						return Lans.g("EdgeExpress","Card entry was cancelled. No charge was made.");
				}
				if(xWebResponse.TransactionStatus==XWebTransactionStatus.EdgeExpressPending) {
					return Lans.g("EdgeExpress","Payment status unknown. The program will keep checking with the card processor.");
				}
				return "";
			}

			///<summary>Sends a web request to the XWeb EdgeExpress API. By default the clinic will be set to the Patient's clinic, however passing in
			///doUseCurrentClinicNum as true will set it to the Clinics.ClinicNum. isClinicNumExact uses clinicNum as given (the clinic a session was
			///created with). isHostPay creates a pay-page session (HostPay), which carries the UI parameters and the return URL (returnUrl, or the
			///clinic's GetReturnUrl when blank); otherwise the request goes to DirectPay.</summary>
			private static XWebResponse SendEdgeExpressRequest(long patNum,EdgeExpressTransType edgeExpressTransactionType,bool isHostPay,bool isWebPayment,
				double amount=0,string orderId="",bool doCreateAlias=false,string alias="",string transactionID="",bool allowDuplicates=false,
				string expDate="",bool doUseCurrentClinicNum=false,long clinicNum=-1,string returnUrl="",bool isClinicNumExact=false)
			{
				Patient pat=Patients.GetLim(patNum);
				if(patNum!=pat.PatNum) {
					throw new ODException("Patient not found for PatNum: "+patNum.ToString(),ODException.ErrorCodes.XWebProgramProperties);
				}
				if(!isClinicNumExact) {
					if(PrefC.HasClinicsEnabled) {
						if(clinicNum<=0) {
							clinicNum=pat.ClinicNum;
							if(doUseCurrentClinicNum) {
								clinicNum=Clinics.ClinicNum;
							}
						}
					}
					else {
						clinicNum=0;
					}
				}
				WebPaymentProperties xwebProperties=GetCredsForRequest(clinicNum,isWebPayment,amount);
				orderId=string.IsNullOrEmpty(orderId) ? XWebResponses.CreateOrderId() : orderId;
				string returnUrlForPage=null;
				//Only host pay calls can display a payment page, direct pay calls will return an error if we send UI parameters.
				if(isHostPay) {
					returnUrlForPage=string.IsNullOrWhiteSpace(returnUrl) ? GetReturnUrl(clinicNum) : returnUrl.Trim();
				}
				string requestXml=BuildRequestXml(xwebProperties,edgeExpressTransactionType,pat,amount,orderId,doCreateAlias,alias,transactionID,
					allowDuplicates,expDate,returnUrlForPage,Security.CurUser);
				string url=isHostPay ? GetHostPayUrl() : GetDirectPayUrl();
				string result=XWebs.XWebInputAbs.UploadData(requestXml,url);
				XWebResponse xResponse=CreateEdgeExpressXWebResponse(result,edgeExpressTransactionType,isDirectPay:!isHostPay);
				xResponse.OrderId=orderId;
				xResponse.PatNum=patNum;
				xResponse.ProvNum=pat.PriProv;
				xResponse.ClinicNum=clinicNum;
				xResponse.DateTUpdate=DateTime.Now;
				xResponse.TransactionType=edgeExpressTransactionType.ToString();
				return xResponse;
			}

			///<summary>Builds the XML for a Card Not Present request. returnUrl is null for DirectPay; for HostPay it is the return URL and the UI
			///parameters are added. If the request would reach MaxRequestBytes, the optional page customization is left out, and then the customer name
			///is shortened, so a long patient name can't push it over the gateway's limit. userod is the clerk (Security.CurUser), or null.</summary>
			public static string BuildRequestXml(WebPaymentProperties xwebProperties,EdgeExpressTransType edgeExpressTransactionType,Patient pat,
				double amount,string orderId,bool doCreateAlias,string alias,string transactionID,bool allowDuplicates,string expDate,string returnUrl,
				Userod userod)
			{
				string customerName=pat.GetNameFLnoPref();
				string requestXml=WriteRequestXml(xwebProperties,edgeExpressTransactionType,pat.PatNum,customerName,amount,orderId,doCreateAlias,alias,
					transactionID,allowDuplicates,expDate,returnUrl,userod,doIncludeCustomization:true);
				if(Encoding.ASCII.GetByteCount(requestXml)<MaxRequestBytes) {
					return requestXml;
				}
				requestXml=WriteRequestXml(xwebProperties,edgeExpressTransactionType,pat.PatNum,customerName,amount,orderId,doCreateAlias,alias,
					transactionID,allowDuplicates,expDate,returnUrl,userod,doIncludeCustomization:false);
				if(Encoding.ASCII.GetByteCount(requestXml)<MaxRequestBytes) {
					return requestXml;
				}
				return WriteRequestXml(xwebProperties,edgeExpressTransactionType,pat.PatNum,StringTools.Truncate(customerName,30),amount,orderId,
					doCreateAlias,alias,transactionID,allowDuplicates,expDate,returnUrl,userod,doIncludeCustomization:false);
			}

			private static string WriteRequestXml(WebPaymentProperties xwebProperties,EdgeExpressTransType edgeExpressTransactionType,long patNum,
				string customerName,double amount,string orderId,bool doCreateAlias,string alias,string transactionID,bool allowDuplicates,string expDate,
				string returnUrl,Userod userod,bool doIncludeCustomization)
			{
				StringBuilder strBldXml=new StringBuilder();
				using(XmlWriter xmlWriter=XmlWriter.Create(strBldXml)) {
					WriteEdgeExpressBaseRequest(xwebProperties,edgeExpressTransactionType,xmlWriter);
					AddAdditionalTransactionParams(xmlWriter,edgeExpressTransactionType,doCreateAlias,amount,patNum,customerName,alias,transactionID,orderId,
						allowDuplicates,expDate,userod);
					if(returnUrl!=null) {
						AddUIParams(xmlWriter,returnUrl,doIncludeCustomization);
					}
					xmlWriter.WriteEndElement();//REQUEST
				}
				return strBldXml.ToString();
			}

			///<summary>Adds additional parameters to the specified transaction type. For EdgeExpress Card Not Present only.</summary>
			private static void AddAdditionalTransactionParams(XmlWriter xmlWriter,EdgeExpressTransType edgeExpressTransactionType,bool doCreateAlias,
				double amount,long patNum,string customerName,string alias,string transactionID,string orderId,bool allowDuplicates,string expDate,
				Userod userod)
			{
				//EdgeExpress CNP only Accepts CLERK Id's up to 20 characters long. Payment will fail if UserName is over 20 characters long. The CNP API guide(2019) states that it accepts up to 50, but this is not true according to testing.
				bool hasClerk=(userod!=null && userod.EServiceType==EServiceTypes.None);//Not an eService, a valid user is logged in.
				switch(edgeExpressTransactionType) {
					case EdgeExpressTransType.CreditSale:
					case EdgeExpressTransType.CreditAuth:
						xmlWriter.WriteElementString("ORDERID",orderId);
						xmlWriter.WriteElementString("AMOUNT",amount.ToString());
						xmlWriter.WriteElementString("CUSTOMERNAME",customerName);
						if(doCreateAlias) {
							xmlWriter.WriteElementString("CREATEALIAS","true");
						}
						if(!alias.IsNullOrEmpty()) {
							xmlWriter.WriteElementString("ALIAS",alias);
						}
						xmlWriter.WriteElementString("ALLOWDUPLICATES",allowDuplicates.ToString());
						xmlWriter.WriteElementString("INVOICENO",$"PAT{patNum}");
						if(hasClerk) {
							xmlWriter.WriteElementString("CLERK",StringTools.Truncate(userod.UserName,20));
						}
						break;
					case EdgeExpressTransType.CreditReturn:
						xmlWriter.WriteElementString("TRANSACTIONID",transactionID);
						xmlWriter.WriteElementString("AMOUNT",amount.ToString());
						if(doCreateAlias) {
							xmlWriter.WriteElementString("CREATEALIAS","true");
						}
						if(!alias.IsNullOrEmpty()) {
							xmlWriter.WriteElementString("ALIAS",alias);
						}
						xmlWriter.WriteElementString("INVOICENO",$"PAT{patNum}");
						if(hasClerk) {
							xmlWriter.WriteElementString("CLERK",StringTools.Truncate(userod.UserName,20));
						}
						break;
					case EdgeExpressTransType.CreditVoid:
						xmlWriter.WriteElementString("TRANSACTIONID",transactionID);
						xmlWriter.WriteElementString("AMOUNT",amount.ToString());
						if(!alias.IsNullOrEmpty()) {
							xmlWriter.WriteElementString("ALIAS",alias);
						}
						xmlWriter.WriteElementString("INVOICENO",$"PAT{patNum}");
						break;
					case EdgeExpressTransType.CreditOnlineCapture://Force
						xmlWriter.WriteElementString("AMOUNT",amount.ToString());
						xmlWriter.WriteElementString("TRANSACTIONID",transactionID);
						xmlWriter.WriteElementString("INVOICENO",$"PAT{patNum}");
						if(hasClerk) {
							xmlWriter.WriteElementString("CLERK",StringTools.Truncate(userod.UserName,20));
						}
						break;
					case EdgeExpressTransType.QueryPayment:
						xmlWriter.WriteElementString("ORDERID",orderId);
						break;
					case EdgeExpressTransType.AliasUpdate:
						xmlWriter.WriteElementString("ALIAS",alias);
						xmlWriter.WriteElementString("EXPDATE",expDate);
						break;
					case EdgeExpressTransType.AliasDelete:
						xmlWriter.WriteElementString("ALIAS",alias);
						break;
					case EdgeExpressTransType.DebitSale:
					case EdgeExpressTransType.DebitReturn:
						if(hasClerk) {
							xmlWriter.WriteElementString("CLERK",StringTools.Truncate(userod.UserName,20));
						}
						break;
				}
			}

			private static void AddUIParams(XmlWriter xmlWriter,string returnUrl,bool doIncludeCustomization=true) {
				xmlWriter.WriteStartElement("HOSTPAYSETTING");
				xmlWriter.WriteStartElement("POSDEVICE");
				xmlWriter.WriteElementString("TYPE","KEYED");
				xmlWriter.WriteEndElement();//POSDEVICE
				xmlWriter.WriteStartElement("RETURNOPTION");
				xmlWriter.WriteElementString("RETURNURL",returnUrl);
				xmlWriter.WriteElementString("RETURNTARGET","_self");//without this line, the top level window will be redirected to RETURNURL
				xmlWriter.WriteEndElement();//RETURNOPTION
				xmlWriter.WriteElementString("DISABLEFRAMING","false");//allows us to put the page in an iframe.
				if(!doIncludeCustomization) {
					xmlWriter.WriteEndElement();//HOSTPAYSETTING
					return;
				}
				xmlWriter.WriteStartElement("CUSTOMIZATION");
				xmlWriter.WriteStartElement("PAGE");
				xmlWriter.WriteStartElement("BILLINGFIRSTNAME");
				xmlWriter.WriteElementString("VISIBLE","false");//They've already entered their name
				xmlWriter.WriteEndElement();//BILLINGFIRSTNAME
				xmlWriter.WriteStartElement("BILLINGMIDDLENAME");
				xmlWriter.WriteElementString("VISIBLE","false");//They've already entered their name
				xmlWriter.WriteEndElement();//BILLINGMIDDLENAME
				xmlWriter.WriteStartElement("BILLINGLASTNAME");
				xmlWriter.WriteElementString("VISIBLE","false");//They've already entered their name
				xmlWriter.WriteEndElement();//BILLINGLASTNAME
				xmlWriter.WriteStartElement("BILLINGCOMPANY");
				xmlWriter.WriteElementString("VISIBLE","false");
				xmlWriter.WriteEndElement();//BILLINGCOMPANY
				xmlWriter.WriteStartElement("BILLINGCUSTOMERTITLE");
				xmlWriter.WriteElementString("VISIBLE","false");
				xmlWriter.WriteEndElement();//BILLINGCUSTOMERTITLE
				xmlWriter.WriteStartElement("ORDERINFO");
				xmlWriter.WriteElementString("LABEL","Payment Information");
				xmlWriter.WriteEndElement();//ORDERINFO
				xmlWriter.WriteEndElement();//PAGE
				xmlWriter.WriteEndElement();//CUSTOMIZATION
				xmlWriter.WriteEndElement();//HOSTPAYSETTING
			}

			///<summary>Sets the expiration and inserts the response to the db.</summary>
			private static void FinishEdgeExpressUrlRequest(XWebResponse response) {
				response.HpfExpiration=DateTime.Now.Add(PayPageSessionLifetime);
				XWebResponses.Insert(response);
			}

			///<summary>Converts the XML string result from the EdgeExpress API to an XWebResponse. Throws exceptions.</summary>
			private static XWebResponse CreateEdgeExpressXWebResponse(string result,EdgeExpressTransType edgeExpressTransactionType,bool isDirectPay) {
				XWebResponse xResponse=new XWebResponse();
				if(edgeExpressTransactionType.In(EdgeExpressTransType.CreditSale,EdgeExpressTransType.CreditAuth)) {
					//The fields in the response depend on which URL is used to make the payment.
					if(isDirectPay) {
						xResponse=ConvertEdgeExpressResponse(result);
					}
					else {
						xResponse.OTK=WebSerializer.DeserializeNode(result,"SESSIONTOKEN");
						xResponse.HpfUrl=WebSerializer.DeserializeNode(result,"PAYPAGEURL");
						xResponse.TransactionStatus=XWebTransactionStatus.EdgeExpressPending;
					}
				}
				else if(edgeExpressTransactionType==EdgeExpressTransType.QueryPayment) {
					string responseCode=WebSerializer.DeserializeNode(result,"RESPONSECODE");
					XWebResponseCodes responseCodeEnum=PIn.Enum<XWebResponseCodes>(responseCode,defaultEnumOption:XWebResponseCodes.Undefined);
					if(responseCodeEnum==XWebResponseCodes.InvalidReferenceError) {
						//XWeb gives this code before the patient completes the transaction. They also give this code when the OrderId doesn't exist.
						xResponse.XWebResponseCode=XWebResponseCodes.Pending;
					}
					else {
						xResponse=ConvertEdgeExpressResponse(result);
					}
				}
				else if(edgeExpressTransactionType.In(EdgeExpressTransType.CreditVoid,
					EdgeExpressTransType.CreditReturn,
					EdgeExpressTransType.CreditOnlineCapture)
				) {
					string responseCode=WebSerializer.DeserializeNode(result,"RESPONSECODE");
					XWebResponseCodes responseCodeEnum=PIn.Enum<XWebResponseCodes>(responseCode,defaultEnumOption:XWebResponseCodes.Undefined);
					if(responseCodeEnum==XWebResponseCodes.InvalidReferenceError) {
						string transaction=WebSerializer.DeserializeNode(result,"TRANSACTIONID");
						string desc=WebSerializer.DeserializeNode(result,"RESPONSEDESCRIPTION");
						throw new ODException("Unable to process transaction: "+transaction+Environment.NewLine+desc);
					}
					xResponse=ConvertEdgeExpressResponse(result);
				}
				else if(edgeExpressTransactionType==EdgeExpressTransType.AliasDelete) {
					xResponse.XWebResponseCode=PIn.Enum<XWebResponseCodes>(WebSerializer.DeserializeNode(result,"RESPONSECODE"));
					xResponse.ResponseDescription=WebSerializer.DeserializeNode(result,"RESPONSEDESCRIPTION");
					if(xResponse.XWebResponseCode==XWebResponseCodes.AliasSuccess) {
						xResponse.TransactionStatus=XWebTransactionStatus.EdgeExpressAliasDeleted;//Not Tracked by eConnector.
					}
					else {
						xResponse.TransactionStatus=XWebTransactionStatus.EdgeExpressPending;
					}
				}
				else if(edgeExpressTransactionType==EdgeExpressTransType.AliasUpdate) {
					xResponse.XWebResponseCode=PIn.Enum<XWebResponseCodes>(WebSerializer.DeserializeNode(result,"RESPONSECODE"));
					xResponse.ResponseDescription=WebSerializer.DeserializeNode(result,"RESPONSEDESCRIPTION");
					if(xResponse.XWebResponseCode==XWebResponseCodes.AliasSuccess) {
						xResponse.TransactionStatus=XWebTransactionStatus.EdgeExpressAliasUpdated;//Not Tracked by eConnector.
					}
					else {
						xResponse.TransactionStatus=XWebTransactionStatus.EdgeExpressPending;
					}
				}
				return xResponse;
			}

			///<summary>Converts the XML string result from the EdgeExpress API for a QueryPayment to an XWebResponse.</summary>
			private static XWebResponse ConvertEdgeExpressResponse(string result) {
				EdgeExpressResponse eeResponse;
				using(StringReader sr = new StringReader(result)) {
					//XWeb's xml references this class as RESULT but OD wants to deserialize to a class called EdgeExpressResponse. 
					//We must explicitly specific the XmlRoot node name here to make that conversion.	
					eeResponse=(EdgeExpressResponse)new XmlSerializer(typeof(EdgeExpressResponse),new XmlRootAttribute("RESULT")).Deserialize(sr);
				}
				//if we don't get a valid exp date back in response, set AccountExpirationDate to the default "0001-01-01"
				DateTime expDate = DateTime.MinValue;
				if(eeResponse.EXPYEAR >= 0 && eeResponse.EXPMONTH > 0) {
					expDate=new DateTime(2000+eeResponse.EXPYEAR,eeResponse.EXPMONTH,1);
				}
				XWebResponse xResponse=new XWebResponse {
					AccountExpirationDate=expDate,
					Alias=eeResponse.ALIAS,
					Amount=(double)eeResponse.APPROVEDAMOUNT,
					ApprovalCode=eeResponse.APPROVALCODE,
					BatchNum=eeResponse.BATCHNO,
					CardBrand=eeResponse.CARDBRAND,
					CardCodeResponse=eeResponse.CARDCODERESPONSE,
					CardType=eeResponse.CARDTYPE,
					CCSource=CreditCardSource.EdgeExpressCNP,
					MaskedAcctNum=eeResponse.MASKEDCARDNUMBER,
					ProcessorResponse=eeResponse.PROCESSORRESPONSE,
					ReceiptID=eeResponse.RECEIPTID,
					ResponseCode=eeResponse.RESPONSECODE,
					ResponseDescription=eeResponse.RESPONSEDESCRIPTION,
					TransactionID=eeResponse.TRANSACTIONID,
					TransactionType=eeResponse.TRANSACTIONTYPE,
					XWebResponseCode=XWebResponse.ConvertResponseCode(eeResponse.RESPONSECODE),
				};
				return xResponse;
			}

			///<summary>Creates and returns the EdgeExpress URL and validation OTK which can be used to make a payment for an unspecified credit card.
			///</summary>
			public static XWebResponse GetUrlForPaymentPage(long patNum,string payNote,double amount,bool createAlias,
				CreditCardSource creditCardSource,bool isWebPayment,string alias="",bool allowDuplicates=false,string email="",string logGuid="",
				long paymentNum=0,long clinicNum=-1,string returnUrl="")
			{
				Meth.NoCheckMiddleTierRole();
				if(!creditCardSource.In(CreditCardSource.XWeb, CreditCardSource.XWebPortalLogin, CreditCardSource.XWebPaymentPortal, CreditCardSource.XWebPaymentPortalGuest, CreditCardSource.EdgeExpressCNP, CreditCardSource.EdgeExpressPaymentPortal, CreditCardSource.EdgeExpressPaymentPortalGuest, CreditCardSource.XWebPaymentPortal, CreditCardSource.XWebPaymentPortalGuest))
				{
					throw new ODException("Invalid CreditCardSource: "+creditCardSource.ToString(),ODException.ErrorCodes.OtkArgsInvalid);
				}
				//Validate the amount.
				if(amount<0.00 || amount>99999.99) {
					throw new ODException("Invalid Amount",ODException.ErrorCodes.OtkArgsInvalid);
				}
				try {
					XWebResponse response=SendEdgeExpressRequest(
						patNum,
						EdgeExpressTransType.CreditSale,
						isHostPay:true,
						isWebPayment,
						amount,
						doCreateAlias:createAlias,
						alias:alias,
						allowDuplicates:allowDuplicates,
						clinicNum:clinicNum,
						returnUrl:returnUrl
					);
					response.Amount=amount;
					response.PayNote=payNote;
					response.CCSource=creditCardSource;
					response.EmailResponse=email;
					response.LogGuid=logGuid;
					response.PaymentNum = paymentNum;
					FinishEdgeExpressUrlRequest(response);
					return response;
				}
				catch(Exception ex) {
					throw new ODException("Error creating payment request.",ex);
				}
			}

			///<summary>Makes a payment using the Direct Pay URL. Does not display a webpage and an alias is required.</summary>
			public static XWebResponse ProcessPaymentDirect(long patNum,string payNote,double amount,CreditCardSource ccSource,
				bool isWebPayment,string alias,bool allowDuplicates,List<CreditCardSource> listCreditCardSourcesEdgeExpress,
				long clinicNum=-1) 
			{
				Meth.NoCheckMiddleTierRole();
				if(!listCreditCardSourcesEdgeExpress.Contains(ccSource)) {
					throw new ODException("Invalid CreditCardSource: "+ccSource.ToString(),ODException.ErrorCodes.OtkArgsInvalid);
				}
				//Validate the amount.
				if(amount<0.00 || amount>99999.99) {
					throw new ODException("Invalid Amount",ODException.ErrorCodes.OtkArgsInvalid);
				}
				try {
					XWebResponse response=SendEdgeExpressRequest(patNum,EdgeExpressTransType.CreditSale,isHostPay:false,isWebPayment,amount,			doCreateAlias:false,alias:alias,allowDuplicates:allowDuplicates,clinicNum:clinicNum);
					response.Amount=amount;
					response.PayNote=payNote;
					response.CCSource=ccSource;
					FinishEdgeExpressUrlRequest(response);
					return response;
				}
				catch(Exception ex) {
					throw new ODException("Error creating payment request.",ex);
				}
			}

			///<summary>Creates and returns the EdgeExpress URL and validation OTK which can be used to create a credit card alias. Uses CreditAuth transaction type.
			///clinicNum picks the clinic whose credentials are used (see ResolveClinicNum). returnUrl is the session's return URL (the loopback
			///listener's address in Loopback mode); blank uses the clinic's GetReturnUrl.</summary>
			public static XWebResponse GetUrlForCreditCardAlias(long patNum,CreditCardSource ccSource,bool isWebPayment,double amount=0,
				bool doCreateAlias=true,bool doUseCurrentClinicNum=false,long clinicNum=-1,string returnUrl="")
			{
				Meth.NoCheckMiddleTierRole();
				try {
					XWebResponse response=SendEdgeExpressRequest(patNum,EdgeExpressTransType.CreditAuth,isHostPay:true,isWebPayment,amount,doCreateAlias:doCreateAlias,
						doUseCurrentClinicNum:doUseCurrentClinicNum,clinicNum:clinicNum,returnUrl:returnUrl);
					response.CCSource=ccSource;
					FinishEdgeExpressUrlRequest(response);
					return response;
				}
				catch(Exception ex) {
					throw new ODException("Error creating token request.",ex);
				}
			}

			///<summary>Tells EdgeExpress to delete their saved copy of the credit card that matches the supplied token.</summary>
			public static XWebResponse UpdateAlias(long patNum,string alias,DateTime expDate) {
				Meth.NoCheckMiddleTierRole();
				try {
					XWebResponse response=SendEdgeExpressRequest(patNum,EdgeExpressTransType.AliasUpdate,isHostPay:false,false,alias:alias,expDate:expDate.ToString("MMyy"));
					FinishEdgeExpressUrlRequest(response);
					return response;
				}
				catch(Exception ex) {
					throw new ODException("Error updating card. "+ex.Message,ex);
				}
			}

			///<summary>Tells EdgeExpress to delete their saved copy of the credit card that matches the supplied token.</summary>
			public static XWebResponse DeleteAlias(long patNum,string alias) {
				Meth.NoCheckMiddleTierRole();
				try {
					XWebResponse response=SendEdgeExpressRequest(patNum,EdgeExpressTransType.AliasDelete,isHostPay:false,false,alias:alias);
					FinishEdgeExpressUrlRequest(response);
					return response;
				}
				catch(Exception ex) {
					throw new ODException("Error deleting card. "+ex.Message,ex);
				}
			}

			public static XWebResponse VoidTransaction(long patNum,string transactionID,double amount,bool isWebPayment) {
				Meth.NoCheckMiddleTierRole();
				try {
					XWebResponse response=SendEdgeExpressRequest(patNum,EdgeExpressTransType.CreditVoid,isHostPay:false,isWebPayment,amount:amount,
						doCreateAlias:false,transactionID:transactionID);
					FinishEdgeExpressUrlRequest(response);
					return response;
				}
				catch(Exception ex) {
					throw new ODException("Error creating void request. "+ex.Message,ex);
				}
			}

			public static XWebResponse ReturnTransaction(long patNum,string transactionID,double amount,bool isWebPayment) {
				Meth.NoCheckMiddleTierRole();
				try {
					XWebResponse response=SendEdgeExpressRequest(patNum,EdgeExpressTransType.CreditReturn,isHostPay:false,isWebPayment,amount:amount,
						doCreateAlias:false,transactionID:transactionID);
					FinishEdgeExpressUrlRequest(response);
					return response;
				}
				catch(Exception ex) {
					throw new ODException("Error creating return request.",ex);
				}
			}

			public static XWebResponse ForceTransaction(long patNum,string transactionID,double amount,bool isWebPayment) {
				Meth.NoCheckMiddleTierRole();
				try {
					XWebResponse response=SendEdgeExpressRequest(patNum,EdgeExpressTransType.CreditOnlineCapture,isHostPay:false,isWebPayment,amount:amount,
						doCreateAlias:false,transactionID:transactionID);
					FinishEdgeExpressUrlRequest(response);
					return response;
				}
				catch(Exception ex) {
					throw new ODException("Error creating return request.",ex);
				}
			}

			///<summary>Makes a web request to EdgeExpress to get the status for the OTK passed in.  Throws exceptions.</summary>
			public static XWebResponse GetOtkStatus(long patNum,string orderId,bool isWebPayment,long clinicNum=-1) {
				Meth.NoCheckMiddleTierRole();
				try {
					return SendEdgeExpressRequest(patNum,EdgeExpressTransType.QueryPayment,isHostPay:false,isWebPayment,orderId:orderId,clinicNum:clinicNum);
				}
				catch(Exception ex) {
					throw new ODException("Error querying transaction information.",ex);
				}
			}

			///<summary>Asks EdgeExpress for the result of a pay-page session (QUERYPAYMENT for its OrderId), with the credentials of the clinic the
			///session was created with: the row's ClinicNum, used as is. A query under any other clinic's merchant account answers 814, and the
			///payment would look pending even though the card was charged. Throws exceptions.</summary>
			public static XWebResponse QueryPayment(XWebResponse xWebResponse) {
				Meth.NoCheckMiddleTierRole();
				try {
					return SendEdgeExpressRequest(xWebResponse.PatNum,EdgeExpressTransType.QueryPayment,isHostPay:false,isWebPayment:false,
						orderId:xWebResponse.OrderId,clinicNum:xWebResponse.ClinicNum,isClinicNumExact:true);
				}
				catch(Exception ex) {
					throw new ODException("Error querying transaction information.",ex);
				}
			}

			public static string BuildReceiptString(XWebResponse response,bool doShowSignatureLine,bool isVoidingRefund=false) {

				if(Enum.TryParse<EdgeExpressTransType>(response.TransactionType,out EdgeExpressTransType transType)) {
					return BuildReceiptString(transType,response.TransactionID,response.MaskedAcctNum,
						response.ApprovalCode,response.PayNote,(decimal)response.Amount,doShowSignatureLine,response.ClinicNum,isVoidingRefund:isVoidingRefund);
				}
				else {
					return "Error creating receipt string.";
				}
			}

			public static string BuildReceiptString(EdgeExpressTransType transType,string transactionID,string cardNumber,
				string approvalCode,string statusDescription,decimal amount,bool doShowSignatureLine,long clinicNum,bool isVoidingRefund=false)
			{
				string result="";
				cardNumber=cardNumber??"";
				int xmin=0;
				int xleft=xmin;
				int xright=15;
				int xmax=37;
				result+=Environment.NewLine;
				result+=CreditCardUtils.AddClinicToReceipt(clinicNum);
				//Print body
				result+="Date".PadRight(xright-xleft,'.')+DateTime.Now.ToString()+Environment.NewLine;
				result+=Environment.NewLine;
				result+="Trans Type".PadRight(xright-xleft,'.')+transType+Environment.NewLine;
				result+=Environment.NewLine;
				//TODO add a decline line
				result+="Transaction ID".PadRight(xright-xleft,'.')+transactionID+Environment.NewLine;
				result+="Account".PadRight(xright-xleft,'.');
				for(int i = 0;i<cardNumber.Length-4;i++) {
					result+="*";
				}
				if(cardNumber.Length>=4) {
					result+=cardNumber.Substring(cardNumber.Length-4)+Environment.NewLine;//last 4 digits of card number only.
				}
				result+="Card Type".PadRight(xright-xleft,'.')+CreditCardUtils.GetCardType(cardNumber)+Environment.NewLine;
				//result+="Entry".PadRight(xright-xleft,'.')+(String.IsNullOrEmpty(magData) ? "Manual" : "Swiped")+Environment.NewLine;
				result+="Approval Code".PadRight(xright-xleft,'.')+approvalCode+Environment.NewLine;
				result+="Result".PadRight(xright-xleft,'.')+statusDescription+Environment.NewLine;
				result+=Environment.NewLine+Environment.NewLine+Environment.NewLine;
				if(transType.In(EdgeExpressTransType.CreditReturn,EdgeExpressTransType.CreditVoid) && !isVoidingRefund) {
					result+="Total Amt".PadRight(xright-xleft,'.')+(amount*-1).ToString("c")+Environment.NewLine;
				}
				else {
					result+="Total Amt".PadRight(xright-xleft,'.')+amount.ToString("c")+Environment.NewLine;
				}
				result+=Environment.NewLine+Environment.NewLine+Environment.NewLine;
				result+="I agree to pay the above total amount according to my card issuer/bank agreement."+Environment.NewLine;
				result+=Environment.NewLine+Environment.NewLine+Environment.NewLine+Environment.NewLine+Environment.NewLine;
				if(doShowSignatureLine) {
					result+="Signature X".PadRight(xmax-xleft,'_');
				}
				else {
					result+="Electronically signed";
				}
				return result;
			}


			///<summary>Similar to XWebs.ProcessOutstandingTransactions but only runs for a single XWebResponse. Used in the event that they don't have a running eConnector.
			///Handles the creation of CreditCards if an alias is returned. The result is always asked for with the credentials of the clinic the session
			///was created with (xWebResponseOld.ClinicNum), whatever clinic existingPayment has.</summary>
			public static XWebResponse ProcessTransaction(XWebResponse xWebResponseOld,Payment existingPayment=null,bool doCreatePayment=true) {
				XWebResponse xWebResponseUpdate=xWebResponseOld;
				XWebTransactionStatus transactionStatusOld=xWebResponseOld.TransactionStatus;
				try {
					//Get the OTK status from the gateway, under the merchant account the session was created with.
					XWebResponse xWebResponseNew=QueryPayment(xWebResponseOld);
					if(xWebResponseNew.XWebResponseCode==XWebResponseCodes.Pending || xWebResponseNew.XWebResponseCode==XWebResponseCodes.Undefined) { //No new status to report. Try again next time.
						if(DateTime.Now>xWebResponseOld.HpfExpiration.Add(PendingGrace)) {
							//EdgeExpress will return 814 "Invalid Reference Error" indefinitely if the patient doesn't finish the transaction.
							xWebResponseUpdate.TransactionStatus=XWebTransactionStatus.EdgeExpressExpired;
						}
						return xWebResponseUpdate;
					}
					//We got this far so we at least got a valid gateway response. This is now the response that we will update in the db.
					xWebResponseUpdate=xWebResponseNew;
					//ResponseCode came over as an int from XML. We need to convert that the XWebResponseCode enum here. Note we are using the otkOutput.ResponseCode (not the otkOld.ResponseCode).
					xWebResponseUpdate.XWebResponseCode=XWebResponse.ConvertResponseCode(xWebResponseUpdate.ResponseCode);
					//Save existing fields from the old db version to the new db version.						
					xWebResponseUpdate.SetPersistentFields(
						xWebResponseOld.XWebResponseNum,
						xWebResponseOld.TransactionType,
						xWebResponseOld.PatNum,
						xWebResponseOld.ProvNum,
						xWebResponseOld.ClinicNum,
						xWebResponseOld.Amount,
						xWebResponseOld.OTK,
						xWebResponseOld.HpfUrl,
						xWebResponseOld.HpfExpiration,
						xWebResponseOld.DebugError,
						xWebResponseOld.PayNote,
						xWebResponseOld.CCSource);
					//The credit card's ExpDate will only be valid when we actually completed a payment.
					if(!string.IsNullOrEmpty(xWebResponseUpdate.ExpDate)) {
						xWebResponseUpdate.AccountExpirationDate=XWebResponse.ConvertExpDate(xWebResponseUpdate.ExpDate);
					}
					switch(xWebResponseUpdate.XWebResponseCode) {
						case XWebResponseCodes.Approval:
							if(!string.IsNullOrEmpty(xWebResponseUpdate.Alias)) {
								//Create a new credit card with this alias.
								CreditCards.InsertFromXWeb(xWebResponseUpdate);
							}
							//If we already have a payment, just update the note and insert a paysplit.
							if(existingPayment!=null) {
								xWebResponseUpdate.PaymentNum=existingPayment.PayNum;
							}
							//Insert Payment, PaySplit, and set FK.
							else if(doCreatePayment) {
								xWebResponseUpdate.PaymentNum=Payments.InsertFromXWeb(
								//todo: create a formatted receipt to show the web user after the payment has been accepted
								xWebResponseUpdate.PatNum,xWebResponseUpdate.ProvNum,xWebResponseUpdate.ClinicNum,xWebResponseUpdate.Amount,
								xWebResponseUpdate.GetFormattedNote(true),//If we ever allow returns or voids from Patient Portal, we will need to change this argument.
								"",xWebResponseUpdate.CCSource);
							}
							xWebResponseUpdate.TransactionStatus=XWebTransactionStatus.EdgeExpressCompletePaymentApproved;
							break;
						case XWebResponseCodes.AliasSuccess:
						case XWebResponseCodes.ZeroDollarAuthApproval:
							//Create a new credit card with this alias.
							CreditCards.InsertFromXWeb(xWebResponseUpdate);
							xWebResponseUpdate.TransactionStatus=XWebTransactionStatus.EdgeExpressCompleteAliasCreated;
							break;
						case XWebResponseCodes.ExpiredWithoutApproval:
						case XWebResponseCodes.SessionTimedOut://006: the page timed out before a card was submitted.
						case XWebResponseCodes.TransactionCancelled://103: cancelled on the page.
							//No charge was made. Just let the XWebResponseCode get updated so we don't check next time.
							xWebResponseUpdate.TransactionStatus=XWebTransactionStatus.EdgeExpressExpired;
							break;
						case XWebResponseCodes.Declined:
							break;
						case XWebResponseCodes.PartialApproval:
						case XWebResponseCodes.OtkSuccess:
						case XWebResponseCodes.Undefined:
						case XWebResponseCodes.Pending:
						default:
							throw new ODException("Unsupported XWebResponseCode for GetOtkStatus: "+xWebResponseUpdate.XWebResponseCode.ToString());
					}
				}
				catch(Exception e) {
					if(transactionStatusOld==XWebTransactionStatus.EdgeExpressPending) {
						//No answer, or one that settles nothing (a temporary outage, 820; a credential error, 805): the card may still have been charged, so
						//the session stays pending and reconciliation asks again. Only officially stop polling once it is well past when the HPF expired.
						if(DateTime.Now>xWebResponseOld.HpfExpiration.Add(PendingGrace)) {
							xWebResponseUpdate.TransactionStatus=XWebTransactionStatus.EdgeExpressMonitoringError;
						}
						else {
							xWebResponseUpdate.TransactionStatus=XWebTransactionStatus.EdgeExpressPending;
						}
					}
					//Hold this error for troubleshooting later.
					xWebResponseUpdate.DebugError=e.Message;
				}
				finally { //Always update, even if there were no changes. This is to update the value of DateTUpdate.
					xWebResponseUpdate.DateTUpdate=DateTime.Now;
					XWebResponses.Update(xWebResponseUpdate);
				}
				return xWebResponseUpdate;
			}

			///<summary>ProcessTransaction, then while the result is still pending (102, or 814 before the card is submitted), asks again up to
			///RepollCount more times, RepollDelay apart. This catches a card submitted just before the user closed the pay-page window. Call it on a
			///background thread (the pay-page helper shows a progress window). actionWait defaults to Thread.Sleep.</summary>
			public static XWebResponse ProcessTransactionWithRepoll(XWebResponse xWebResponseOld,Payment existingPayment=null,bool doCreatePayment=true,
				Action<TimeSpan> actionWait=null)
			{
				Meth.NoCheckMiddleTierRole();
				if(actionWait==null) {
					actionWait=x => Thread.Sleep(x);
				}
				XWebResponse xWebResponse=ProcessTransaction(xWebResponseOld,existingPayment,doCreatePayment);
				for(int i=0;i<RepollCount && xWebResponse.TransactionStatus==XWebTransactionStatus.EdgeExpressPending;i++) {
					actionWait(RepollDelay);
					xWebResponse=ProcessTransaction(xWebResponse,existingPayment,doCreatePayment);
				}
				return xWebResponse;
			}

			///<summary>Local reconciliation of pay-page sessions left pending, which Open Dental's eConnector used to do. Looks at EdgeExpressPending
			///CreditSale and CreditAuth rows whose session ended (HpfExpiration passed, so no pay-page window still has them) within ReconcileWindow.
			///Each row is claimed first (XWebResponses.TryClaim), so only one workstation handles it, and re-read after the claim. The result is asked
			///for with the row's clinic. An approval found this late is never posted automatically: the row is marked approved (and a card on file is
			///saved if an alias came back), no payment is created, and an alert asks staff to attach or void it. Rows with no result past the grace
			///period are marked expired, as before. dateTimeNow is for tests.</summary>
			public static EdgeExpressReconcileResult ReconcilePending(DateTime? dateTimeNow=null) {
				Meth.NoCheckMiddleTierRole();
				DateTime now=dateTimeNow??DateTime.Now;
				EdgeExpressReconcileResult reconcileResult=new EdgeExpressReconcileResult();
				List<XWebResponse> listXWebResponses=XWebResponses.GetPendingEdgeExpress(now.Subtract(ReconcileWindow));
				for(int i=0;i<listXWebResponses.Count;i++) {
					XWebResponse xWebResponse=listXWebResponses[i];
					if(xWebResponse.HpfExpiration>now) {
						reconcileResult.CountStillOpen++;//A pay-page window may still have it; that window asks for the result itself.
						continue;
					}
					if(!XWebResponses.TryClaim(xWebResponse.XWebResponseNum,xWebResponse.DateTUpdate)) {
						reconcileResult.CountClaimedElsewhere++;
						continue;
					}
					XWebResponse xWebResponseClaimed=XWebResponses.GetOne(xWebResponse.XWebResponseNum);
					if(xWebResponseClaimed==null || xWebResponseClaimed.TransactionStatus!=XWebTransactionStatus.EdgeExpressPending) {
						reconcileResult.CountClaimedElsewhere++;
						continue;
					}
					long payNumOld=xWebResponseClaimed.PaymentNum;
					XWebResponse xWebResponseProcessed;
					try {
						xWebResponseProcessed=ProcessTransaction(xWebResponseClaimed,existingPayment:null,doCreatePayment:false);
					}
					catch(Exception ex) {
						//ProcessTransaction catches the vendor's errors itself; this is the database write or the card-on-file insert failing.
						reconcileResult.CountFailed++;
						ODException.SwallowAnyException(() => Logger.WriteLine("Reconciling XWebResponseNum "+xWebResponse.XWebResponseNum+" failed: "
							+ex.Message,"EdgeExpress"));
						continue;
					}
					reconcileResult.CountChecked++;
					switch(xWebResponseProcessed.TransactionStatus) {
						case XWebTransactionStatus.EdgeExpressCompletePaymentApproved:
						case XWebTransactionStatus.EdgeExpressCompleteAliasCreated:
							reconcileResult.CountApproved++;
							InsertLateResultAlert(xWebResponseProcessed,payNumOld);
							break;
						case XWebTransactionStatus.EdgeExpressExpired:
						case XWebTransactionStatus.EdgeExpressMonitoringError:
							reconcileResult.CountExpired++;
							break;
						case XWebTransactionStatus.EdgeExpressPending:
							break;
						default:
							reconcileResult.CountDeclined++;
							break;
					}
				}
				return reconcileResult;
			}

			///<summary>Raises the alert for an approval (or a saved card) that reconciliation found after the pay-page window closed.</summary>
			private static void InsertLateResultAlert(XWebResponse xWebResponse,long payNumOld) {
				Patient patient=Patients.GetLim(xWebResponse.PatNum);
				string patientText=patient.GetNameFLnoPref()+" (patient "+POut.Long(xWebResponse.PatNum)+")";
				AlertItem alertItem=new AlertItem();
				alertItem.ClinicNum=xWebResponse.ClinicNum;
				alertItem.Type=AlertType.Generic;
				alertItem.FormToOpen=FormType.None;
				alertItem.Actions=ActionType.MarkAsRead|ActionType.Delete|ActionType.ShowItemValue;
				if(xWebResponse.TransactionStatus==XWebTransactionStatus.EdgeExpressCompletePaymentApproved) {
					alertItem.Severity=SeverityType.High;
					alertItem.Description=Lans.g("EdgeExpress","Card payment of")+" "+xWebResponse.Amount.ToString("c")+" "+Lans.g("EdgeExpress","for")+" "
						+patientText+" "+Lans.g("EdgeExpress","was approved after the payment window closed. No payment was entered. Attach it or void it.");
				}
				else {
					alertItem.Severity=SeverityType.Normal;
					alertItem.Description=Lans.g("EdgeExpress","A card was saved on file for")+" "+patientText+" "
						+Lans.g("EdgeExpress","after the card entry window closed. Check the patient's cards on file.");
				}
				StringBuilder stringBuilder=new StringBuilder();
				stringBuilder.AppendLine(Lans.g("EdgeExpress","Transaction type:")+" "+xWebResponse.TransactionType);
				stringBuilder.AppendLine(Lans.g("EdgeExpress","Order ID:")+" "+xWebResponse.OrderId);
				stringBuilder.AppendLine(Lans.g("EdgeExpress","Transaction ID:")+" "+xWebResponse.TransactionID);
				stringBuilder.AppendLine(Lans.g("EdgeExpress","Approval code:")+" "+xWebResponse.ApprovalCode);
				stringBuilder.AppendLine(Lans.g("EdgeExpress","Card:")+" "+xWebResponse.MaskedAcctNum);
				stringBuilder.AppendLine(Lans.g("EdgeExpress","Amount:")+" "+xWebResponse.Amount.ToString("c"));
				if(payNumOld!=0) {
					stringBuilder.AppendLine(Lans.g("EdgeExpress","The payment window had payment number")+" "+POut.Long(payNumOld)+" "
						+Lans.g("EdgeExpress","open. It may have been cancelled since."));
				}
				alertItem.ItemValue=stringBuilder.ToString();
				AlertItems.Insert(alertItem);
			}
		}
		#endregion Card Not Present API
	}

	public enum EdgeExpressApiType {
		Terminal,
		Web,
	}

	///<summary>The type of transaction for the EdgeExpress API.</summary>
	public enum EdgeExpressTransType {
		[Description("Purchase")]
		CreditSale,
		[Description("Return")]
		CreditReturn,
		///<summary>Queries that status of a payment using its OTK (session token).</summary>
		QueryPayment,
		[Description("Debit Purchase")]
		DebitSale,
		[Description("Debit Return")]
		DebitReturn,
		[Description("Force")]
		CreditOnlineCapture,
		[Description("Pre-Authorization")]
		CreditAuth,
		[Description("Void")]
		CreditVoid,
		AliasCreate,
		AliasUpdate,
		AliasDelete,
	}

	///<summary>How the program notices that the card processor's hosted pay page is done (docs/specs/vendor-direct/edgeexpress.md 3.2). Stored per
	///clinic in the EdgeExpressReturnMode program property by name.</summary>
	public enum EdgeExpressReturnMode {
		///<summary>The pay page opens in a window inside the program, which catches the navigation to the return URL (the clinic's, or the built-in
		///.invalid address) and closes before anything is loaded from it. The default.</summary>
		Embedded,
		///<summary>A one-shot listener on 127.0.0.1 catches the return, so the page also works in the system browser (when WebView2 is missing).
		///The embedded window still catches it first when it is used.</summary>
		Loopback,
	}

	///<summary>What the pay-page window does with a navigation. See EdgeExpress.CNP.ClassifyPayPageNavigation.</summary>
	public enum EdgeExpressPayPageNavigation {
		///<summary>Let it load.</summary>
		Allow,
		///<summary>The return URL: the card processor is done. Cancel it and close the window.</summary>
		Return,
		///<summary>Cancel it.</summary>
		Block,
	}

	///<summary>What one EdgeExpress.CNP.ReconcilePending run did.</summary>
	public class EdgeExpressReconcileResult {
		///<summary>Rows asked about.</summary>
		public int CountChecked;
		///<summary>Approvals or saved cards found late; each raised an alert.</summary>
		public int CountApproved;
		///<summary>Rows marked expired (or past their grace period).</summary>
		public int CountExpired;
		///<summary>Rows that finished with a decline or another final answer.</summary>
		public int CountDeclined;
		///<summary>Rows skipped because their session hadn't ended yet.</summary>
		public int CountStillOpen;
		///<summary>Rows another workstation or window touched first.</summary>
		public int CountClaimedElsewhere;
		///<summary>Rows whose update failed.</summary>
		public int CountFailed;
	}

	///<summary>The vendor job that runs EdgeExpress.CNP.ReconcilePending every 2 minutes, on one workstation at a time, while the EdgeExpress
	///program link is enabled and pending pay-page sessions exist. Found by the vendor job host (OpenDental.VendorDirect.VendorJobHost); it replaces
	///the eConnector's polling of pending hosted-pay transactions.</summary>
	public class EdgeExpressReconcileJob:VendorDirect.IVendorBackgroundJob {
		public string Name {
			get {
				return "EdgeExpressReconcile";
			}
		}

		public ProgramName ProgramName {
			get {
				return ProgramName.EdgeExpress;
			}
		}

		public TimeSpan Interval {
			get {
				return TimeSpan.FromMinutes(2);
			}
		}

		///<summary>Only when a pending session might need it: one COUNT query, no vendor call.</summary>
		public bool ShouldRun() {
			return XWebResponses.CountPendingEdgeExpress(DateTime.Now.Subtract(EdgeExpress.CNP.ReconcileWindow))>0;
		}

		public void Run() {
			EdgeExpressReconcileResult reconcileResult=EdgeExpress.CNP.ReconcilePending();
			if(reconcileResult.CountChecked>0 || reconcileResult.CountFailed>0) {
				ODException.SwallowAnyException(() => Logger.WriteLine("Reconciled "+reconcileResult.CountChecked+" pending pay-page session(s): "
					+reconcileResult.CountApproved+" approved late, "+reconcileResult.CountExpired+" expired, "+reconcileResult.CountDeclined+" declined, "
					+reconcileResult.CountFailed+" failed.","EdgeExpress"));
			}
		}
	}
}
