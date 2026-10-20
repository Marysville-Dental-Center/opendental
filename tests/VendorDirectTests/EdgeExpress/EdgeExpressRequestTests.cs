// Tests for the EdgeExpress library side that need no database: request building and its size bound, the return URL and pay-page navigation
// rules, response codes, the auth key masking and the removal of the compiled-in X-Web test credentials.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.WebTypes.Shared.XWeb;

namespace VendorDirectTests.EdgeExpressTests {
	[TestClass]
	public class EdgeExpressRequestTests {
		private const string XWEB_ID="800000001111";
		private const string TERMINAL_ID="80001111";
		private const string AUTH_KEY="Abcdefghijklmnopqrstuvwxyz012345";

		private static WebPaymentProperties GetCreds() {
			WebPaymentProperties webPaymentProperties=new WebPaymentProperties();
			webPaymentProperties.XWebID=XWEB_ID;
			webPaymentProperties.TerminalID=TERMINAL_ID;
			webPaymentProperties.AuthKey=AUTH_KEY;
			return webPaymentProperties;
		}

		private static Patient GetPatient(string fName,string lName) {
			Patient patient=new Patient();
			patient.PatNum=4242;
			patient.FName=fName;
			patient.LName=lName;
			return patient;
		}

		///<summary>A blocked Open Dental domain, taken from the one shared list so this file names none itself.</summary>
		private static string HqDomain(int index) {
			return CodeBase.VendorDirect.HqHosts.BlockedDomains[index];
		}

		private static string GetElement(string xml,string name) {
			Match match=Regex.Match(xml,"<"+name+">([^<]*)</"+name+">");
			return match.Success ? match.Groups[1].Value : null;
		}

		[TestMethod]
		public void BuildRequestXml_HostPay_CarriesTheReturnUrlAndTheOfficesCredentials() {
			Userod userod=new Userod();
			userod.UserName="frontdesk";
			string xml=EdgeExpress.CNP.BuildRequestXml(GetCreds(),EdgeExpressTransType.CreditSale,GetPatient("Ann","Lee"),25.5,"1234567890",
				doCreateAlias:true,alias:"",transactionID:"",allowDuplicates:false,expDate:"",returnUrl:"https://edgeexpress-return.invalid/PayDone",userod);
			Assert.AreEqual(XWEB_ID,GetElement(xml,"XWEBID"));
			Assert.AreEqual(TERMINAL_ID,GetElement(xml,"XWEBTERMINALID"));
			Assert.AreEqual(AUTH_KEY,GetElement(xml,"XWEBAUTHKEY"));
			Assert.AreEqual("CREDITSALE",GetElement(xml,"TRANSACTIONTYPE"));
			Assert.AreEqual("1234567890",GetElement(xml,"ORDERID"));
			Assert.AreEqual("Ann Lee",GetElement(xml,"CUSTOMERNAME"));
			Assert.AreEqual("PAT4242",GetElement(xml,"INVOICENO"));
			Assert.AreEqual("frontdesk",GetElement(xml,"CLERK"));
			Assert.AreEqual("true",GetElement(xml,"CREATEALIAS"));
			Assert.AreEqual("https://edgeexpress-return.invalid/PayDone",GetElement(xml,"RETURNURL"));
			Assert.AreEqual("_self",GetElement(xml,"RETURNTARGET"));
			StringAssert.Contains(xml,"<CUSTOMIZATION>");
		}

		[TestMethod]
		public void BuildRequestXml_DirectPay_HasNoPageSettings() {
			string xml=EdgeExpress.CNP.BuildRequestXml(GetCreds(),EdgeExpressTransType.QueryPayment,GetPatient("Ann","Lee"),0,"1234567890",
				doCreateAlias:false,alias:"",transactionID:"",allowDuplicates:false,expDate:"",returnUrl:null,userod:null);
			Assert.AreEqual("QUERYPAYMENT",GetElement(xml,"TRANSACTIONTYPE"));
			Assert.AreEqual("1234567890",GetElement(xml,"ORDERID"));
			Assert.IsFalse(xml.Contains("HOSTPAYSETTING"),"DirectPay answers an error when UI parameters are sent.");
			Assert.IsNull(GetElement(xml,"CLERK"));
		}

		[TestMethod]
		public void BuildRequestXml_ClerkIsCutTo20Characters_AndLeftOutForEServices() {
			Userod userod=new Userod();
			userod.UserName="a-very-long-user-name-of-thirty";
			string xml=EdgeExpress.CNP.BuildRequestXml(GetCreds(),EdgeExpressTransType.CreditSale,GetPatient("Ann","Lee"),1,"1",false,"","",false,"",
				"https://edgeexpress-return.invalid/PayDone",userod);
			Assert.AreEqual(20,GetElement(xml,"CLERK").Length);
			userod.EServiceType=EServiceTypes.Broadcaster;
			xml=EdgeExpress.CNP.BuildRequestXml(GetCreds(),EdgeExpressTransType.CreditSale,GetPatient("Ann","Lee"),1,"1",false,"","",false,"",
				"https://edgeexpress-return.invalid/PayDone",userod);
			Assert.IsNull(GetElement(xml,"CLERK"));
		}

		[TestMethod]
		public void BuildRequestXml_MaximumLengthFieldsStayUnderTheGatewayLimit() {
			Userod userod=new Userod();
			userod.UserName=new string('u',20);
			string returnUrl="https://pay.example-dental-office.com/"+new string('r',EdgeExpress.CNP.ReturnUrlMaxLength-38);
			Assert.AreEqual(EdgeExpress.CNP.ReturnUrlMaxLength,returnUrl.Length);
			Assert.AreEqual("",EdgeExpress.CNP.ValidateReturnUrl(returnUrl));
			//A 60-character name (F13) keeps the page customization.
			string xml=EdgeExpress.CNP.BuildRequestXml(GetCreds(),EdgeExpressTransType.CreditSale,GetPatient(new string('F',30),new string('L',29)),
				99999.99,"9999999999",true,new string('a',40),"",false,"",returnUrl,userod);
			Assert.IsTrue(Encoding.ASCII.GetByteCount(xml)<EdgeExpress.CNP.MaxRequestBytes,"Request is "+Encoding.ASCII.GetByteCount(xml)+" bytes.");
			StringAssert.Contains(xml,"<CUSTOMIZATION>");
			//An absurd name is shortened (after the customization is dropped) rather than failing with 801.
			xml=EdgeExpress.CNP.BuildRequestXml(GetCreds(),EdgeExpressTransType.CreditSale,GetPatient(new string('F',600),new string('L',600)),
				99999.99,"9999999999",true,new string('a',40),"",false,"",returnUrl,userod);
			Assert.IsTrue(Encoding.ASCII.GetByteCount(xml)<EdgeExpress.CNP.MaxRequestBytes,"Request is "+Encoding.ASCII.GetByteCount(xml)+" bytes.");
			Assert.IsFalse(xml.Contains("<CUSTOMIZATION>"));
			Assert.AreEqual(30,GetElement(xml,"CUSTOMERNAME").Length);
			Assert.AreEqual(returnUrl,GetElement(xml,"RETURNURL"),"The return URL is never cut.");
		}

		[TestMethod]
		public void ValidateReturnUrl_AcceptsBlankDefaultOfficeAndLoopback_RefusesTheRest() {
			Assert.AreEqual("",EdgeExpress.CNP.ValidateReturnUrl(""));
			Assert.AreEqual("",EdgeExpress.CNP.ValidateReturnUrl("   "));
			Assert.AreEqual("",EdgeExpress.CNP.ValidateReturnUrl(EdgeExpress.CNP.ReturnUrlDefault));
			Assert.AreEqual("",EdgeExpress.CNP.ValidateReturnUrl("https://www.example-dental-office.com/paid"));
			Assert.AreEqual("",EdgeExpress.CNP.ValidateReturnUrl("http://127.0.0.1:5000/edgeexpress/return/abc"));
			Assert.AreNotEqual("",EdgeExpress.CNP.ValidateReturnUrl("http://www.example-dental-office.com/paid"),"Plain http off this computer.");
			Assert.AreNotEqual("",EdgeExpress.CNP.ValidateReturnUrl("/PayDone"),"Relative.");
			Assert.AreNotEqual("",EdgeExpress.CNP.ValidateReturnUrl("ftp://example.com/x"));
			Assert.AreNotEqual("",EdgeExpress.CNP.ValidateReturnUrl("https://www."+HqDomain(3)+"/PortalPayDone.aspx"),"Never an Open Dental host.");
			Assert.AreNotEqual("",EdgeExpress.CNP.ValidateReturnUrl("https://example.com/paid#done"));
			Assert.AreNotEqual("",EdgeExpress.CNP.ValidateReturnUrl("https://example.com/"+new string('x',EdgeExpress.CNP.ReturnUrlMaxLength)));
		}

		[TestMethod]
		public void ParseReturnMode_DefaultsToEmbedded() {
			Assert.AreEqual(EdgeExpressReturnMode.Embedded,EdgeExpress.CNP.ParseReturnMode(""));
			Assert.AreEqual(EdgeExpressReturnMode.Embedded,EdgeExpress.CNP.ParseReturnMode(null));
			Assert.AreEqual(EdgeExpressReturnMode.Embedded,EdgeExpress.CNP.ParseReturnMode("Embedded"));
			Assert.AreEqual(EdgeExpressReturnMode.Embedded,EdgeExpress.CNP.ParseReturnMode("1"));
			Assert.AreEqual(EdgeExpressReturnMode.Loopback,EdgeExpress.CNP.ParseReturnMode("Loopback"));
			Assert.AreEqual(EdgeExpressReturnMode.Loopback,EdgeExpress.CNP.ParseReturnMode(" loopback "));
		}

		[TestMethod]
		public void IsReturnNavigation_MatchesTheReturnPrefixOnly() {
			string returnUrl=EdgeExpress.CNP.ReturnUrlDefault;
			Assert.IsTrue(EdgeExpress.CNP.IsReturnNavigation("https://edgeexpress-return.invalid/PayDone",returnUrl));
			Assert.IsTrue(EdgeExpress.CNP.IsReturnNavigation("https://EDGEEXPRESS-RETURN.invalid/paydone?ORDERID=1",returnUrl));
			Assert.IsFalse(EdgeExpress.CNP.IsReturnNavigation("http://edgeexpress-return.invalid/PayDone",returnUrl),"Scheme differs.");
			Assert.IsFalse(EdgeExpress.CNP.IsReturnNavigation("https://edgeexpress-return.invalid:8443/PayDone",returnUrl),"Port differs.");
			Assert.IsFalse(EdgeExpress.CNP.IsReturnNavigation("https://evil.example/https://edgeexpress-return.invalid/PayDone",returnUrl));
			Assert.IsFalse(EdgeExpress.CNP.IsReturnNavigation("https://edgeexpress-return.invalid/Other",returnUrl));
			Assert.IsFalse(EdgeExpress.CNP.IsReturnNavigation("",returnUrl));
			string returnLoopback="http://127.0.0.1:50123/edgeexpress/return/AbCd";
			Assert.IsTrue(EdgeExpress.CNP.IsReturnNavigation("http://127.0.0.1:50123/edgeexpress/return/AbCd",returnLoopback));
			Assert.IsFalse(EdgeExpress.CNP.IsReturnNavigation("http://127.0.0.1:50124/edgeexpress/return/AbCd",returnLoopback));
		}

		[TestMethod]
		public void ClassifyPayPageNavigation_AllowsOnlyThePayPageHosts() {
			string payPage="https://ee.test.paygateway.com/HostPayService/v1/hostpay/paypage/abc";
			string returnUrl=EdgeExpress.CNP.ReturnUrlDefault;
			Assert.AreEqual(EdgeExpressPayPageNavigation.Allow,EdgeExpress.CNP.ClassifyPayPageNavigation(payPage,payPage,returnUrl));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Allow,EdgeExpress.CNP.ClassifyPayPageNavigation("https://ee.test.paygateway.com/x",payPage,returnUrl));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Allow,EdgeExpress.CNP.ClassifyPayPageNavigation("https://other.paygateway.com/x",payPage,returnUrl));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Allow,EdgeExpress.CNP.ClassifyPayPageNavigation("about:blank",payPage,returnUrl));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Return,EdgeExpress.CNP.ClassifyPayPageNavigation(returnUrl+"?x=1",payPage,returnUrl));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Return,EdgeExpress.CNP.ClassifyPayPageNavigation(returnUrl,payPage,returnUrl,isFrame:true),
				"A return inside a frame is caught too.");
			Assert.AreEqual(EdgeExpressPayPageNavigation.Block,EdgeExpress.CNP.ClassifyPayPageNavigation("https://www.example.com/",payPage,returnUrl));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Block,EdgeExpress.CNP.ClassifyPayPageNavigation("http://ee.test.paygateway.com/x",payPage,returnUrl));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Block,EdgeExpress.CNP.ClassifyPayPageNavigation("https://www."+HqDomain(0)+"/",payPage,returnUrl));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Block,EdgeExpress.CNP.ClassifyPayPageNavigation("javascript:alert(1)",payPage,returnUrl));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Block,EdgeExpress.CNP.ClassifyPayPageNavigation("data:text/html,x",payPage,returnUrl));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Block,EdgeExpress.CNP.ClassifyPayPageNavigation("https://notpaygateway.com/x",payPage,returnUrl));
			//Frames (3-D Secure and the like) may load other https hosts, never an Open Dental one or plain http.
			Assert.AreEqual(EdgeExpressPayPageNavigation.Allow,EdgeExpress.CNP.ClassifyPayPageNavigation("https://acs.example-bank.com/3ds",payPage,returnUrl,true));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Block,EdgeExpress.CNP.ClassifyPayPageNavigation("https://"+HqDomain(0)+"/x",payPage,returnUrl,true));
			Assert.AreEqual(EdgeExpressPayPageNavigation.Block,EdgeExpress.CNP.ClassifyPayPageNavigation("http://example.com/x",payPage,returnUrl,true));
		}

		[TestMethod]
		public void ResponseCodes_SessionTimedOutAndCancelledHaveTheirOwnValues() {
			Assert.AreEqual(XWebResponseCodes.SessionTimedOut,XWebResponse.ConvertResponseCode(6));
			Assert.AreEqual(XWebResponseCodes.TransactionCancelled,XWebResponse.ConvertResponseCode(103));
			Assert.AreEqual(XWebResponseCodes.Pending,XWebResponse.ConvertResponseCode(102));
			Assert.AreEqual(XWebResponseCodes.Undefined,XWebResponse.ConvertResponseCode(104));
		}

		[TestMethod]
		public void GetUserMessage_PointsCredentialErrorsAtSetup() {
			XWebResponse xWebResponse=new XWebResponse();
			xWebResponse.XWebResponseCode=XWebResponseCodes.AuthenticationError;
			StringAssert.Contains(EdgeExpress.CNP.GetUserMessage(xWebResponse),"Setup > Program Links > EdgeExpress");
			xWebResponse.XWebResponseCode=XWebResponseCodes.TestMerchantSetUpError;
			StringAssert.Contains(EdgeExpress.CNP.GetUserMessage(xWebResponse),"test gateway");
			xWebResponse.XWebResponseCode=XWebResponseCodes.SessionTimedOut;
			StringAssert.Contains(EdgeExpress.CNP.GetUserMessage(xWebResponse),"No charge was made");
			xWebResponse.XWebResponseCode=XWebResponseCodes.Pending;
			xWebResponse.TransactionStatus=XWebTransactionStatus.EdgeExpressPending;
			StringAssert.Contains(EdgeExpress.CNP.GetUserMessage(xWebResponse),"keep checking");
			xWebResponse.XWebResponseCode=XWebResponseCodes.Approval;
			xWebResponse.TransactionStatus=XWebTransactionStatus.EdgeExpressCompletePaymentApproved;
			Assert.AreEqual("",EdgeExpress.CNP.GetUserMessage(xWebResponse));
		}

		[TestMethod]
		public void GetRequestLogText_ShowsOnlyHalfTheKey() {
			string text=EdgeExpress.GetRequestLogText(GetCreds(),10);
			Assert.IsFalse(text.Contains(AUTH_KEY));
			StringAssert.Contains(text,AUTH_KEY.Substring(0,16)+new string('X',16));
		}

		[TestMethod]
		public void MaskAuthKeys_HidesBothKeyElements() {
			string xml="<REQUEST><XWEBID>"+XWEB_ID+"</XWEBID><XWEBAUTHKEY>"+AUTH_KEY+"</XWEBAUTHKEY></REQUEST>"
				+"<GatewayRequest><AuthKey>"+AUTH_KEY+"</AuthKey><TerminalID>"+TERMINAL_ID+"</TerminalID></GatewayRequest>";
			string masked=XWebs.MaskAuthKeys(xml);
			Assert.IsFalse(masked.Contains(AUTH_KEY));
			StringAssert.Contains(masked,"<XWEBAUTHKEY>"+new string('X',32)+"</XWEBAUTHKEY>");
			StringAssert.Contains(masked,"<AuthKey>"+new string('X',32)+"</AuthKey>");
			StringAssert.Contains(masked,XWEB_ID);
			StringAssert.Contains(masked,TERMINAL_ID);
		}

		[TestMethod]
		public void InputEvent_NeverSeesTheAuthKey() {
			List<string> listSeen=new List<string>();
			EventHandler<CodeBase.Logger.LoggerEventArgs> eventHandler=(sender,e) => {
				listSeen.Add(sender as string);
				listSeen.Add(e.Data);
			};
			XWebs.InputEvent+=eventHandler;
			XWebs.Mock=new XWebs.MockXWebs();
			XWebs.Mock.ResponseForRequest=(input,url) => "<RESULT><RESPONSECODE>814</RESPONSECODE></RESULT>";
			try {
				string xml=EdgeExpress.CNP.BuildRequestXml(GetCreds(),EdgeExpressTransType.QueryPayment,GetPatient("Ann","Lee"),0,"1",false,"","",false,"",
					null,null);
				Type typeInput=typeof(XWebs).GetNestedType("XWebInputAbs",BindingFlags.NonPublic);
				typeInput.GetMethod("UploadData",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[] { xml,EdgeExpress.CNP.DirectPayUrlTest });
			}
			finally {
				XWebs.InputEvent-=eventHandler;
				XWebs.Mock=null;
			}
			Assert.AreEqual(2,listSeen.Count);
			foreach(string seen in listSeen) {
				Assert.IsNotNull(seen);
				Assert.IsFalse(seen.Contains(AUTH_KEY),"The auth key reached an event handler.");
			}
		}

		[TestMethod]
		public void UploadData_RefusesOpenDentalAndPlainHttpHosts() {
			Type typeInput=typeof(XWebs).GetNestedType("XWebInputAbs",BindingFlags.NonPublic);
			MethodInfo methodUpload=typeInput.GetMethod("UploadData",BindingFlags.Public|BindingFlags.Static);
			XWebs.Mock=new XWebs.MockXWebs();
			XWebs.Mock.ResponseForRequest=(input,url) => "<RESULT/>";
			try {
				foreach(string url in new[] { "https://www."+HqDomain(0)+"/x","http://ee.paygateway.com/x" }) {
					TargetInvocationException ex=AssertEx.Throws<TargetInvocationException>(() => methodUpload.Invoke(null,new object[] { "<REQUEST/>",url }));
					Assert.IsInstanceOfType(ex.InnerException,typeof(CodeBase.ODException),url);
				}
				Assert.AreEqual("<RESULT/>",methodUpload.Invoke(null,new object[] { "<REQUEST/>",EdgeExpress.CNP.HostPayUrlTest }));
			}
			finally {
				XWebs.Mock=null;
			}
		}

		[TestMethod]
		public void XWebs_HasNoCompiledInTestAccount() {
			Assert.IsNull(typeof(XWebs).GetField("UseTestAccountOD"),"The Open Dental test account switch is gone.");
			Assert.IsNull(typeof(XWebs).GetField("UseTestAccountLoopback"),"The X-Web loopback test account switch is gone.");
			string source=File.ReadAllText(Path.Combine(Foundation.FoundationLayoutTests.GetRepositoryRoot(),
				@"OpenDentBusiness\WebTypes\Shared\XWeb\XWebs.cs"));
			//No quoted literal shaped like an X-Web Auth Key (32 letters and digits), XWebID (12 digits) or Terminal ID (8 digits).
			foreach(Match match in Regex.Matches(source,"\"([A-Za-z0-9]{32}|[0-9]{12}|[0-9]{8})\"")) {
				Assert.Fail("Credential-shaped literal in XWebs.cs (SHA-256 "+Sha256Hex(match.Groups[1].Value)+").");
			}
			//The removed test-account values, as SHA-256 hashes only so they aren't committed again: the XWebID, the two Auth Keys and the two
			//Terminal IDs. No quoted literal of any length may hash to one of them.
			HashSet<string> hashSetRemoved=new HashSet<string> {
				"7b4d9acb33f98312722a99452841ae3baedd70cdf50158fabc55cad882b2f8b8",
				"429b587e30fc7fbd66d8524a2f9e8ec6c47d8b580f39b621c8f0132e55329f05",
				"34733ac172b4acdb593833f15347b118d0d911b4420272e3c5a357fb7e31dac9",
				"ac085bc4c38d3993e15a30f4822ae9123e9ad82e6921abb027e91bfa7b502cb4",
				"9939f2be3cc23b3e8644e3af520d5d8034f4220fa6277ca512482a75961db28b",
			};
			foreach(Match match in Regex.Matches(source,"\"([^\"\\\\\\r\\n]*)\"")) {
				Assert.IsFalse(hashSetRemoved.Contains(Sha256Hex(match.Groups[1].Value)),"A removed X-Web test credential is back in XWebs.cs.");
			}
			//GatewayParams has one way to fill the credentials: the office's own.
			Match matchGatewayParams=Regex.Match(source,@"Dictionary<string,string> GatewayParams \{(.*?)ExtraGatewayParams",RegexOptions.Singleline);
			Assert.IsTrue(matchGatewayParams.Success,"GatewayParams not found.");
			string body=matchGatewayParams.Groups[1].Value;
			foreach(string param in new[] { "XWebID","AuthKey","TerminalID" }) {
				Assert.AreEqual(1,Regex.Matches(body,"\""+param+"\"").Count,param+" is set in one place only.");
			}
			StringAssert.Contains(body,"ret.Add(\"XWebID\",_xWebID.ToString());");
			StringAssert.Contains(body,"ret.Add(\"AuthKey\",_authKey.ToString());");
			StringAssert.Contains(body,"ret.Add(\"TerminalID\",_terminalID.ToString());");
			Assert.IsFalse(Regex.IsMatch(body,@"\bif\s*\((?![^)]*_transactionType)"),"No branch besides the office-credential one.");
		}

		private static string Sha256Hex(string value) {
			using(System.Security.Cryptography.SHA256 sha256=System.Security.Cryptography.SHA256.Create()) {
				byte[] arrayHash=sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
				StringBuilder stringBuilder=new StringBuilder();
				for(int i=0;i<arrayHash.Length;i++) {
					stringBuilder.Append(arrayHash[i].ToString("x2"));
				}
				return stringBuilder.ToString();
			}
		}

		[TestMethod]
		public void XWebs_GatewayParamsStillSendTheOfficesCredentials() {
			//X-Charge (X-Web) regression: GatewayParams builds every X-Web request from the office's own credentials.
			Type typeAbs=typeof(XWebs).GetNestedType("XWebInputAbs",BindingFlags.NonPublic);
			Type typeOtkStatus=typeof(XWebs).GetNestedType("XWebInputOtkStatus",BindingFlags.NonPublic);
			object input=Activator.CreateInstance(typeOtkStatus,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,
				new object[] { 1L,"otk123",false },null);
			typeAbs.GetField("_xWebID",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(input,XWEB_ID);
			typeAbs.GetField("_authKey",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(input,AUTH_KEY);
			typeAbs.GetField("_terminalID",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(input,TERMINAL_ID);
			Dictionary<string,string> dictParams=(Dictionary<string,string>)typeAbs.GetProperty("GatewayParams",BindingFlags.Instance|BindingFlags.NonPublic)
				.GetValue(input);
			Assert.AreEqual(XWEB_ID,dictParams["XWebID"]);
			Assert.AreEqual(AUTH_KEY,dictParams["AuthKey"]);
			Assert.AreEqual(TERMINAL_ID,dictParams["TerminalID"]);
			Assert.AreEqual("otk123",dictParams["OTK"]);
		}

		[TestMethod]
		public void Urls_TestAndProductionUseTheSameTrailingSlashRule() {
			Assert.IsTrue(EdgeExpress.CNP.HostPayUrlProduction.EndsWith("/"));
			Assert.IsTrue(EdgeExpress.CNP.HostPayUrlTest.EndsWith("/"));
			Assert.AreEqual(EdgeExpress.CNP.HostPayUrlProduction.Replace("ee.paygateway.com","ee.test.paygateway.com"),EdgeExpress.CNP.HostPayUrlTest);
			Assert.AreEqual(EdgeExpress.CNP.DirectPayUrlProduction.Replace("ee.paygateway.com","ee.test.paygateway.com"),EdgeExpress.CNP.DirectPayUrlTest);
		}
	}
}
