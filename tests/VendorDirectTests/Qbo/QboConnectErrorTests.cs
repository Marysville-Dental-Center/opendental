// Tests that the Connect step of the QuickBooks Online setup window keeps the messages written for the office (wrong keys, expired code, offline)
// when the token exchange runs behind UI.ProgressWin, which rethrows an exception's InnerException whenever it has one.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using CodeBase;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness.Qbo;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.Qbo {
	[TestClass]
	public class QboConnectErrorTests {
		private const string RedirectUri="http://localhost:8765/callback";

		///<summary>What UI.ProgressWin.ShowDialog does with an exception from ActionMain (WpfControlsOD/UI/Progress/ProgressWin.cs): it rethrows the
		///InnerException when there is one, otherwise the exception itself. Returns what would reach the window's catch, or null.</summary>
		private static Exception RunLikeProgressWin(Action actionMain) {
			try {
				actionMain();
				return null;
			}
			catch(Exception ex) {
				return ex.InnerException??ex;
			}
		}

		///<summary>The connect path as the setup window runs it: the exchange inside RunForWindow, inside ProgressWin. Returns the exception the window
		///shows, after checking that nothing escaped through ProgressWin.</summary>
		private static Exception Connect(QboTestKit kit) {
			Exception exConnect=null;
			Exception exEscaped=RunLikeProgressWin(() => {
				exConnect=QuickBooksOnline.RunForWindow(() => QuickBooksOnline.RedeemCode(kit.QboOAuth,"auth-code",RedirectUri));
			});
			Assert.IsNull(exEscaped,"Nothing may escape through ProgressWin.");
			return exConnect;
		}

		[TestMethod]
		public void Connect_InvalidClient_ShowsMessageInvalidClient() {
			QboTestKit kit=new QboTestKit();
			kit.HandlerOAuth.Enqueue(HttpStatusCode.Unauthorized,QboTestKit.OAuthErrorJson("invalid_client"));
			Exception ex=Connect(kit);
			Assert.IsInstanceOfType(ex,typeof(QboAuthException));
			Assert.IsInstanceOfType(ex.InnerException,typeof(TokenRejectedException),"The cause is kept for the log.");
			Assert.AreEqual(QboAuthException.MessageInvalidClient,QuickBooksOnline.GetMessageForUser(ex));
		}

		[TestMethod]
		public void Connect_QboAuthExceptionWithInnerTokenRejected_ComesOutWithMessageInvalidClient() {
			QboAuthException exThrown=new QboAuthException(QboAuthException.MessageInvalidClient,needsReconnect:false,oAuthError:"invalid_client",
				innerException:new TokenRejectedException("invalid_client","Client authentication failed.",401));
			Exception exConnect=null;
			Exception exEscaped=RunLikeProgressWin(() => {
				exConnect=QuickBooksOnline.RunForWindow(() => throw exThrown);
			});
			Assert.IsNull(exEscaped);
			Assert.AreSame(exThrown,exConnect);
			Assert.AreEqual(QboAuthException.MessageInvalidClient,QuickBooksOnline.GetMessageForUser(exConnect));
			//Without RunForWindow, ProgressWin hands the window the raw TokenRejectedException, which has no message for the office.
			Exception exUnwrapped=RunLikeProgressWin(() => throw exThrown);
			Assert.IsInstanceOfType(exUnwrapped,typeof(TokenRejectedException));
			Assert.AreEqual("",QuickBooksOnline.GetMessageForUser(exUnwrapped));
		}

		[TestMethod]
		public void Connect_InvalidGrant_ShowsTheExpiredCodeMessage() {
			QboTestKit kit=new QboTestKit();
			kit.HandlerOAuth.Enqueue(HttpStatusCode.BadRequest,QboTestKit.OAuthErrorJson("invalid_grant"));
			Exception ex=Connect(kit);
			Assert.IsInstanceOfType(ex,typeof(QboAuthException));
			string message=QuickBooksOnline.GetMessageForUser(ex);
			StringAssert.Contains(message,"invalid_grant");
			StringAssert.Contains(message,"Click Connect again.");
		}

		[TestMethod]
		public void Connect_Offline_ShowsMessageUnreachable() {
			QboTestKit kit=new QboTestKit();
			kit.HandlerOAuth.EnqueueNetworkFailure();
			Exception ex=Connect(kit);
			Assert.IsInstanceOfType(ex,typeof(QboApiException));
			Assert.AreEqual(QboApiException.MessageUnreachable,QuickBooksOnline.GetMessageForUser(ex));
		}

		[TestMethod]
		public void Connect_Success_ReturnsNoException() {
			QboTestKit kit=new QboTestKit();
			kit.HandlerOAuth.Enqueue(HttpStatusCode.OK,QboTestKit.TokenJson("at-1","rt-2"));
			QboTokenResult tokenResult=null;
			Exception ex=QuickBooksOnline.RunForWindow(() => tokenResult=QuickBooksOnline.RedeemCode(kit.QboOAuth,"auth-code",RedirectUri));
			Assert.IsNull(ex);
			Assert.AreEqual("rt-2",tokenResult.RefreshToken);
		}

		[TestMethod]
		public void GetMessageForUser_OnlyODExceptionsCarryAMessageForTheOffice() {
			Assert.AreEqual("Shown.",QuickBooksOnline.GetMessageForUser(new ODException("Shown.")));
			Assert.AreEqual("",QuickBooksOnline.GetMessageForUser(new HttpRequestException("Raw.")));
			Assert.AreEqual("",QuickBooksOnline.GetMessageForUser(new IOException("Raw.")));
		}

		[TestMethod]
		public void SetupWindow_CatchesInsideEveryProgressWinAction() {
			string pathForm=Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),"OpenDental","Forms","FormQuickBooksOnlineSetup.cs");
			string source=File.ReadAllText(pathForm);
			int countProgressWins=CountOccurrences(source,"new UI.ProgressWin()");
			Assert.IsTrue(countProgressWins>=3,"Load, Connect and Disconnect each run behind a progress window.");
			Assert.AreEqual(countProgressWins,CountOccurrences(source,"=QuickBooksOnline.RunForWindow("),
				"Every ProgressWin action has to catch inside RunForWindow, or ProgressWin swaps the message for the inner exception.");		}

		private static int CountOccurrences(string text,string value) {
			int count=0;
			int index=text.IndexOf(value,StringComparison.Ordinal);
			while(index>=0) {
				count++;
				index=text.IndexOf(value,index+value.Length,StringComparison.Ordinal);
			}
			return count;
		}
	}
}
