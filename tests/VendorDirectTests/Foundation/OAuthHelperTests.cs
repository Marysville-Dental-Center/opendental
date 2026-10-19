// Tests for the vendor-direct OAuth helpers: PKCE, state and pasted redirects.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VendorDirectTests.Foundation {
	[TestClass]
	public class OAuthHelperTests {
		[TestMethod]
		public void Pkce_Rfc7636AppendixB_Vector() {
			//RFC 7636, Appendix B.
			Pkce pkce=Pkce.FromVerifier("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");
			Assert.AreEqual("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",pkce.Challenge);
			Assert.AreEqual("S256",Pkce.ChallengeMethod);
		}

		[TestMethod]
		public void Pkce_Create_GivesValidUniqueVerifiers() {
			Pkce pkce1=Pkce.Create();
			Pkce pkce2=Pkce.Create();
			Assert.IsTrue(Pkce.IsValidVerifier(pkce1.Verifier));
			Assert.AreEqual(43,pkce1.Verifier.Length);
			Assert.AreNotEqual(pkce1.Verifier,pkce2.Verifier);
			Assert.AreEqual(Pkce.ComputeChallenge(pkce1.Verifier),pkce1.Challenge);
			Assert.IsFalse(pkce1.Challenge.Contains("="));
		}

		[TestMethod]
		public void Pkce_IsValidVerifier_EnforcesLengthAndAlphabet() {
			Assert.IsFalse(Pkce.IsValidVerifier(null));
			Assert.IsFalse(Pkce.IsValidVerifier(new string('a',42)));
			Assert.IsTrue(Pkce.IsValidVerifier(new string('a',43)));
			Assert.IsTrue(Pkce.IsValidVerifier(new string('~',128)));
			Assert.IsFalse(Pkce.IsValidVerifier(new string('a',129)));
			Assert.IsFalse(Pkce.IsValidVerifier(new string('a',42)+"+"));
			AssertEx.Throws<ArgumentException>(() => Pkce.FromVerifier("short"));
		}

		[TestMethod]
		public void Base64Url_RoundTrips() {
			byte[] bytes={ 0xfb,0xff,0xfe,0x00,0x10 };
			string encoded=Base64Url.Encode(bytes);
			Assert.AreEqual("-__-ABA",encoded);
			CollectionAssert.AreEqual(bytes,Base64Url.Decode(encoded));
		}

		[TestMethod]
		public void OAuthState_NewIsRandomAndMatchesOnlyItself() {
			string state=OAuthState.New();
			Assert.AreEqual(43,state.Length);
			Assert.AreNotEqual(state,OAuthState.New());
			Assert.IsTrue(OAuthState.Matches(state,state));
			Assert.IsFalse(OAuthState.Matches(state,state+"x"));
			Assert.IsFalse(OAuthState.Matches(state,state.Substring(1)));
			Assert.IsFalse(OAuthState.Matches(state,""));
			Assert.IsFalse(OAuthState.Matches("",""));
			Assert.IsFalse(OAuthState.Matches(state,null));
		}

		[TestMethod]
		public void RedirectParser_FullUrl() {
			OAuthRedirectResult result=OAuthRedirectParser.Parse("  https://office.example/qbo/callback?code=AB%2BC&state=xyz&realmId=4620816365&extra=a+b  ");
			Assert.IsNotNull(result);
			Assert.AreEqual("AB+C",result.Code);
			Assert.AreEqual("xyz",result.State);
			Assert.AreEqual("4620816365",result.Get("realmId"));
			Assert.AreEqual("a b",result.Get("extra"));
			Assert.IsTrue(result.HasCode);
			Assert.IsTrue(result.VerifyState("xyz"));
			Assert.IsFalse(result.VerifyState("abc"));
		}

		[TestMethod]
		public void RedirectParser_QueryStringAndFragment() {
			Assert.AreEqual("c1",OAuthRedirectParser.Parse("?code=c1&state=s").Code);
			Assert.AreEqual("c2",OAuthRedirectParser.Parse("code=c2&state=s").Code);
			Assert.AreEqual("c3",OAuthRedirectParser.Parse("#code=c3").Code);
			Assert.AreEqual("c4",OAuthRedirectParser.Parse("http://127.0.0.1:5000/cb#code=c4&state=s").Code);
		}

		[TestMethod]
		public void RedirectParser_BareCode() {
			OAuthRedirectResult result=OAuthRedirectParser.Parse("sl.AbCdEf-123_x");
			Assert.AreEqual("sl.AbCdEf-123_x",result.Code);
			Assert.IsNull(result.State);
			Assert.IsFalse(result.VerifyState("anything"),"A bare code carries no state.");
		}

		[TestMethod]
		public void RedirectParser_ErrorAndJunk() {
			OAuthRedirectResult result=OAuthRedirectParser.Parse("https://x.example/cb?error=access_denied&error_description=User+said+no&state=s");
			Assert.IsFalse(result.HasCode);
			Assert.AreEqual("access_denied",result.Error);
			Assert.AreEqual("User said no",result.ErrorDescription);
			Assert.IsNull(OAuthRedirectParser.Parse(""));
			Assert.IsNull(OAuthRedirectParser.Parse("   "));
			Assert.IsNull(OAuthRedirectParser.Parse("two words"));
		}

		[TestMethod]
		public void RedirectParser_FirstValueWinsForRepeatedNames() {
			Dictionary<string,string> dict=OAuthRedirectParser.ParseQuery("state=first&state=second&&=empty&flag");
			Assert.AreEqual("first",dict["state"]);
			Assert.AreEqual("",dict["flag"]);
			Assert.IsFalse(dict.ContainsKey(""));
		}

		[TestMethod]
		public void BuildAuthorizeUrl_EscapesAndRefusesBlockedHosts() {
			string url=OAuthTokenClient.BuildAuthorizeUrl(new Uri("https://auth.example.com/authorize?prompt=consent"),new[] {
				new KeyValuePair<string,string>("client_id","id 1"),
				new KeyValuePair<string,string>("redirect_uri","http://127.0.0.1:5000/cb"),
				new KeyValuePair<string,string>("skipped",null),
			});
			Assert.AreEqual("https://auth.example.com/authorize?prompt=consent&client_id=id%201&redirect_uri=http%3A%2F%2F127.0.0.1%3A5000%2Fcb",url);
			AssertEx.Throws<ArgumentException>(() => OAuthTokenClient.BuildAuthorizeUrl(new Uri("https://login."+HqHosts.BlockedDomains[0]+"/a"),
				new KeyValuePair<string,string>[0]));
			AssertEx.Throws<ArgumentException>(() => OAuthTokenClient.BuildAuthorizeUrl(new Uri("http://auth.example.com/a"),
				new KeyValuePair<string,string>[0]));
		}
	}
}
