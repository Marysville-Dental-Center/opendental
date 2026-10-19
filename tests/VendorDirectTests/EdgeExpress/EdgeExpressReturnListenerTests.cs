// Tests for the EdgeExpress loopback return listener (Loopback mode, docs/specs/vendor-direct/edgeexpress.md 3.4). Connects to 127.0.0.1 only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace VendorDirectTests.EdgeExpressTests {
	[TestClass]
	public class EdgeExpressReturnListenerTests {
		///<summary>Sends raw request text to 127.0.0.1:port and returns the whole response ("" if the connection was reset).</summary>
		private static async Task<string> SendRawAsync(int port,string requestText) {
			using(TcpClient tcpClient=new TcpClient()) {
				await tcpClient.ConnectAsync("127.0.0.1",port);
				NetworkStream stream=tcpClient.GetStream();
				byte[] bytes=Encoding.ASCII.GetBytes(requestText);
				await stream.WriteAsync(bytes,0,bytes.Length);
				try {
					using(StreamReader reader=new StreamReader(stream,Encoding.UTF8)) {
						return await reader.ReadToEndAsync();
					}
				}
				catch(IOException) {
					return "";
				}
			}
		}

		private static string GetPath(string redirectUri) {
			return new Uri(redirectUri).AbsolutePath;
		}

		[TestMethod]
		public void StartReturnListener_UsesLoopbackAndAPerSessionNonce() {
			using(LoopbackListener listener1=EdgeExpress.CNP.StartReturnListener())
			using(LoopbackListener listener2=EdgeExpress.CNP.StartReturnListener()) {
				Assert.IsNotNull(listener1);
				Assert.IsNotNull(listener2);
				StringAssert.StartsWith(listener1.RedirectUri,"http://127.0.0.1:"+listener1.Port+EdgeExpress.CNP.ReturnPathPrefix);
				string nonce=GetPath(listener1.RedirectUri).Substring(EdgeExpress.CNP.ReturnPathPrefix.Length);
				Assert.AreEqual(22,nonce.Length,"128 random bits, base64url.");
				Assert.AreNotEqual(GetPath(listener1.RedirectUri),GetPath(listener2.RedirectUri));
				Assert.AreNotEqual(listener1.Port,listener2.Port);
				Assert.AreEqual("",EdgeExpress.CNP.ValidateReturnUrl(listener1.RedirectUri),"The gateway gets a return URL the program itself accepts.");
				Assert.IsTrue(listener1.AcceptPost);
			}
		}

		[TestMethod]
		public void ReturnListener_WrongNonceGets404AndKeepsWaiting_ThenAPostReturnEndsIt() {
			AssertEx.Run(async () => {
				using(LoopbackListener listener=EdgeExpress.CNP.StartReturnListener()) {
					Task<OAuthRedirectResult> task=listener.WaitForRedirectAsync(TimeSpan.FromSeconds(20));
					string host="127.0.0.1:"+listener.Port;
					string response=await SendRawAsync(listener.Port,"GET "+EdgeExpress.CNP.ReturnPathPrefix+"wrongnonce HTTP/1.1\r\nHost: "+host
						+"\r\nConnection: close\r\n\r\n");
					StringAssert.StartsWith(response,"HTTP/1.1 404");
					response=await SendRawAsync(listener.Port,"GET /edgeexpress/return HTTP/1.1\r\nHost: "+host+"\r\nConnection: close\r\n\r\n");
					StringAssert.StartsWith(response,"HTTP/1.1 404");
					Assert.IsFalse(task.IsCompleted,"A stray request never ends the wait.");
					//The gateway's return may be a form post. Its body is read and thrown away; the program asks for the result itself.
					string body="RESPONSECODE=000&TRANSACTIONID=123&"+new string('x',8000);
					response=await SendRawAsync(listener.Port,"POST "+GetPath(listener.RedirectUri)+"?ORDERID=55 HTTP/1.1\r\nHost: "+host
						+"\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\n\r\n"+body);
					OAuthRedirectResult result=await task;
					Assert.AreEqual("55",result.Get("ORDERID"),"Only the query string is parsed.");
					Assert.IsNull(result.Get("RESPONSECODE"),"The body is never parsed.");
					StringAssert.StartsWith(response,"HTTP/1.1 200 OK");
					StringAssert.Contains(response,"Payment step finished. You can close this tab and return to the program.");
					Assert.IsFalse(response.Contains("<script"),"The page must not run scripts.");
					Assert.IsFalse(response.Contains("src="),"The page must not load anything.");
					Assert.IsFalse(response.Contains("Sign-in"),"Not the OAuth page.");
				}
			});
		}

		[TestMethod]
		public void ReturnListener_GetReturnWorks_AndABodyOver16KbStillEndsTheWait() {
			AssertEx.Run(async () => {
				using(LoopbackListener listener=EdgeExpress.CNP.StartReturnListener()) {
					Task<OAuthRedirectResult> task=listener.WaitForRedirectAsync(TimeSpan.FromSeconds(20));
					string response=await SendRawAsync(listener.Port,"GET "+GetPath(listener.RedirectUri)+" HTTP/1.1\r\nHost: localhost:"+listener.Port
						+"\r\nConnection: close\r\n\r\n");
					await task;
					StringAssert.StartsWith(response,"HTTP/1.1 200 OK");
				}
				using(LoopbackListener listener=EdgeExpress.CNP.StartReturnListener()) {
					Task<OAuthRedirectResult> task=listener.WaitForRedirectAsync(TimeSpan.FromSeconds(20));
					string body=new string('y',20*1024);
					//The listener reads at most 16 KB in all and leaves the rest unread; the browser may see the connection reset, which is harmless.
					await SendRawAsync(listener.Port,"POST "+GetPath(listener.RedirectUri)+" HTTP/1.1\r\nHost: 127.0.0.1:"+listener.Port
						+"\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\n\r\n"+body);
					OAuthRedirectResult result=await task;
					Assert.IsNotNull(result);
				}
			});
		}

		[TestMethod]
		public void ReturnListener_IsReleasedAfterUse() {
			AssertEx.Run(async () => {
				int port;
				using(LoopbackListener listener=EdgeExpress.CNP.StartReturnListener()) {
					port=listener.Port;
					Task<OAuthRedirectResult> task=listener.WaitForRedirectAsync(TimeSpan.FromSeconds(20));
					await SendRawAsync(listener.Port,"GET "+GetPath(listener.RedirectUri)+" HTTP/1.1\r\nHost: 127.0.0.1:"+listener.Port
						+"\r\nConnection: close\r\n\r\n");
					await task;
				}
				using(TcpClient tcpClient=new TcpClient()) {
					await AssertEx.ThrowsAsync<SocketException>(() => tcpClient.ConnectAsync("127.0.0.1",port));
				}
			});
		}
	}
}
