// Tests for the QuickBooks Online connect step: the redirect rules, the pasted-address parser, the state check and the loopback listener.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness.Qbo;

namespace VendorDirectTests.Qbo {
	[TestClass]
	public class QboAuthorizationTests {
		#region Redirect rules
		[TestMethod]
		public void Production_NeedsHttpsAndARealHostName() {
			Assert.AreEqual("",QboRedirectRules.Validate(QboEnvironment.Production,"https://office.example/qbo/callback"));
			Assert.AreEqual(QboRedirectRules.MessageProductionHttps,QboRedirectRules.Validate(QboEnvironment.Production,"http://localhost:8765/callback"));
			Assert.AreEqual(QboRedirectRules.MessageProductionHttps,QboRedirectRules.Validate(QboEnvironment.Production,"https://localhost/callback"));
			Assert.AreEqual(QboRedirectRules.MessageProductionHttps,QboRedirectRules.Validate(QboEnvironment.Production,"https://127.0.0.1/callback"));
			Assert.AreEqual(QboRedirectRules.MessageProductionHttps,QboRedirectRules.Validate(QboEnvironment.Production,"https://10.0.0.5/callback"));
			Assert.AreEqual(QboRedirectRules.MessageProductionHttps,QboRedirectRules.Validate(QboEnvironment.Production,"http://office.example/callback"));
			Assert.AreNotEqual("",QboRedirectRules.Validate(QboEnvironment.Production,""));
			Assert.AreNotEqual("",QboRedirectRules.Validate(QboEnvironment.Production,"/qbo/callback"));
			Assert.AreNotEqual("",QboRedirectRules.Validate(QboEnvironment.Production,"https://office.example/qbo#x"));
		}

		[TestMethod]
		public void Sandbox_AllowsLocalhostOrHttps() {
			Assert.AreEqual("",QboRedirectRules.Validate(QboEnvironment.Sandbox,"http://localhost:8765/callback"));
			Assert.AreEqual("",QboRedirectRules.Validate(QboEnvironment.Sandbox,"http://127.0.0.1:8765/callback"));
			Assert.AreEqual("",QboRedirectRules.Validate(QboEnvironment.Sandbox,"https://office.example/qbo/callback"));
			Assert.AreNotEqual("",QboRedirectRules.Validate(QboEnvironment.Sandbox,"http://office.example/qbo/callback"));
			Assert.AreNotEqual("",QboRedirectRules.Validate(QboEnvironment.Sandbox,"ftp://localhost/callback"));
		}

		[TestMethod]
		public void RelayPort_BlankOrAValidPort() {
			Assert.AreEqual("",QboRedirectRules.ValidateRelayPort(""));
			Assert.AreEqual("",QboRedirectRules.ValidateRelayPort("8765"));
			Assert.AreNotEqual("",QboRedirectRules.ValidateRelayPort("0"));
			Assert.AreNotEqual("",QboRedirectRules.ValidateRelayPort("70000"));
			Assert.AreNotEqual("",QboRedirectRules.ValidateRelayPort("-1"));
		}

		[TestMethod]
		public void AuthRequest_PicksLoopbackRelayOrPaste() {
			QboAuthRequest requestSandbox=QboAuthRequest.Create("id","http://localhost:8765/callback",QboEnvironment.Sandbox,0);
			Assert.IsTrue(requestSandbox.UsesLoopback);
			Assert.IsFalse(requestSandbox.UsesRelay);
			Assert.AreEqual(8765,requestSandbox.LoopbackPort);
			Assert.AreEqual("/callback",requestSandbox.LoopbackPath);
			Assert.AreEqual("localhost",requestSandbox.LoopbackHost);
			Assert.AreEqual(43,requestSandbox.State.Length);
			StringAssert.Contains(requestSandbox.Url,"state="+Uri.EscapeDataString(requestSandbox.State));
			QboAuthRequest requestRelay=QboAuthRequest.Create("id","https://office.example/qbo/callback",QboEnvironment.Production,9100);
			Assert.IsTrue(requestRelay.UsesLoopback);
			Assert.IsTrue(requestRelay.UsesRelay);
			Assert.AreEqual(9100,requestRelay.LoopbackPort);
			Assert.AreEqual(QboAuthRequest.RelayPath,requestRelay.LoopbackPath);
			Assert.AreEqual("https://office.example/qbo/callback",requestRelay.RedirectUri,"Intuit always gets the registered address.");
			QboAuthRequest requestPaste=QboAuthRequest.Create("id","https://office.example/qbo/callback",QboEnvironment.Production,0);
			Assert.IsFalse(requestPaste.UsesLoopback);
			Assert.IsNull(requestPaste.CreateLoopbackListener());
			Assert.AreNotEqual(requestSandbox.State,QboAuthRequest.Create("id","http://localhost:8765/callback",QboEnvironment.Sandbox,0).State);
		}
		#endregion

		#region Pasted address
		[TestMethod]
		public void Parse_FullAddress() {
			QboAuthorizationResult result=QboRedirectParser.Parse(
				"https://office.example/qbo/callback?code=AB11&state=st-1&realmId=4620816365165566530");
			Assert.AreEqual("AB11",result.Code);
			Assert.AreEqual("st-1",result.State);
			Assert.AreEqual("4620816365165566530",result.RealmId);
			Assert.IsFalse(result.IsEnteredSeparately);
		}

		[TestMethod]
		public void Parse_QueryOnlyAndUrlEncoded() {
			QboAuthorizationResult result=QboRedirectParser.Parse("  ?code=AB%2B11%3D&state=st%2D1&realmId=123  ");
			Assert.AreEqual("AB+11=",result.Code);
			Assert.AreEqual("st-1",result.State);
			Assert.AreEqual("123",result.RealmId);
			result=QboRedirectParser.Parse("code=XY&realmId=9&state=s");
			Assert.AreEqual("XY",result.Code);
		}

		[TestMethod]
		public void Parse_BlankOrABareCode_IsNull() {
			Assert.IsNull(QboRedirectParser.Parse(""));
			Assert.IsNull(QboRedirectParser.Parse("   "));
			Assert.IsNull(QboRedirectParser.Parse("AB11CODEONLY"),"A bare code has no realm ID; the separate boxes cover that.");
		}

		[TestMethod]
		public void Validate_MissingRealmId_Fails() {
			QboAuthRequest request=QboAuthRequest.Create("id","https://office.example/cb",QboEnvironment.Production,0,"st-1");
			QboAuthorizationResult result=QboRedirectParser.Parse("https://office.example/cb?code=AB11&state=st-1");
			QboAuthException ex=AssertEx.Throws<QboAuthException>(() => result.Validate(request));
			StringAssert.Contains(ex.Message,"realmId");
			result=QboRedirectParser.Parse("https://office.example/cb?code=AB11&state=st-1&realmId=12%2F..%2F34");
			AssertEx.Throws<QboAuthException>(() => result.Validate(request));
		}

		[TestMethod]
		public void Validate_AccessDenied_ShowsTheError() {
			QboAuthRequest request=QboAuthRequest.Create("id","https://office.example/cb",QboEnvironment.Production,0,"st-1");
			QboAuthorizationResult result=QboRedirectParser.Parse("https://office.example/cb?error=access_denied&state=st-1");
			QboAuthException ex=AssertEx.Throws<QboAuthException>(() => result.Validate(request));
			StringAssert.Contains(ex.Message,"access_denied");
			Assert.AreEqual("access_denied",ex.OAuthError);
		}

		[TestMethod]
		public void Validate_StateFromAnotherAttempt_IsRefused() {
			QboAuthRequest request=QboAuthRequest.Create("id","https://office.example/cb",QboEnvironment.Production,0,"st-current");
			QboAuthorizationResult result=QboRedirectParser.Parse("https://office.example/cb?code=AB11&state=st-earlier&realmId=123");
			QboAuthException ex=AssertEx.Throws<QboAuthException>(() => result.Validate(request));
			Assert.AreEqual(QboAuthException.MessageStateMismatch,ex.Message);
			result=QboRedirectParser.Parse("https://office.example/cb?code=AB11&realmId=123");
			AssertEx.Throws<QboAuthException>(() => result.Validate(request));//A pasted address without state is refused too.
			QboRedirectParser.Parse("https://office.example/cb?code=AB11&state=st-current&realmId=123").Validate(request);
		}

		[TestMethod]
		public void Validate_SeparateBoxes_SkipTheStateCheck() {
			QboAuthRequest request=QboAuthRequest.Create("id","https://office.example/cb",QboEnvironment.Production,0,"st-current");
			QboAuthorizationResult result=QboAuthorizationResult.FromSeparateValues(" AB11 "," 123 ");
			Assert.IsTrue(result.IsEnteredSeparately);
			Assert.AreEqual("AB11",result.Code);
			result.Validate(request);
		}
		#endregion

		[TestMethod]
		public void Loopback_CatchesTheRedirectAndPassesTheStateCheck() {
			int port=GetFreePort();
			QboAuthRequest request=QboAuthRequest.Create("id","http://127.0.0.1:"+port+"/callback",QboEnvironment.Sandbox,0);
			QboAuthorizationResult result=null;
			using(LoopbackListener listener=request.CreateLoopbackListener()) {
				listener.Start();
				Assert.AreEqual(request.RedirectUri,listener.RedirectUri);
				AssertEx.Run(async () => {
					Task<OAuthRedirectResult> taskWait=listener.WaitForRedirectAsync(TimeSpan.FromSeconds(20));
					using(HttpClient httpClient=new HttpClient()) {
						HttpResponseMessage response=await httpClient.GetAsync(request.RedirectUri+"?code=AB11&state="+Uri.EscapeDataString(request.State)
							+"&realmId=4620816365165566530");
						Assert.AreEqual(HttpStatusCode.OK,response.StatusCode);
					}
					result=QboAuthorizationResult.FromRedirect(await taskWait);
				});
			}
			result.Validate(request);
			Assert.AreEqual("4620816365165566530",result.RealmId);
		}

		private static int GetFreePort() {
			TcpListener tcpListener=new TcpListener(IPAddress.Loopback,0);
			tcpListener.Start();
			int port=((IPEndPoint)tcpListener.LocalEndpoint).Port;
			tcpListener.Stop();
			return port;
		}
	}
}
