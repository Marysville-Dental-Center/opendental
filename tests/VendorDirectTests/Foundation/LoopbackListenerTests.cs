// Tests for the vendor-direct loopback redirect listener. Connects to 127.0.0.1 only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VendorDirectTests.Foundation {
	[TestClass]
	public class LoopbackListenerTests {
		///<summary>Sends raw request text to 127.0.0.1:port and returns the whole response.</summary>
		private static async Task<string> SendRawAsync(int port,string requestText) {
			using(TcpClient tcpClient=new TcpClient()) {
				await tcpClient.ConnectAsync("127.0.0.1",port);
				NetworkStream stream=tcpClient.GetStream();
				byte[] bytes=Encoding.ASCII.GetBytes(requestText);
				await stream.WriteAsync(bytes,0,bytes.Length);
				using(StreamReader reader=new StreamReader(stream,Encoding.UTF8)) {
					return await reader.ReadToEndAsync();
				}
			}
		}

		private static string Get(string target,int port,string host=null) {
			return "GET "+target+" HTTP/1.1\r\nHost: "+(host??("127.0.0.1:"+port))+"\r\nConnection: close\r\n\r\n";
		}

		[TestMethod]
		public void Listener_ReturnsCodeAndStateOnExpectedPath() {
			AssertEx.Run(async () => {
				using(LoopbackListener listener=new LoopbackListener(0,"/callback")) {
					listener.Start();
					Assert.IsTrue(listener.Port>0);
					Assert.AreEqual("http://127.0.0.1:"+listener.Port+"/callback",listener.RedirectUri);
					Task<OAuthRedirectResult> task=listener.WaitForRedirectAsync(TimeSpan.FromSeconds(20));
					string response=await SendRawAsync(listener.Port,Get("/callback?code=abc%2F1&state=s1&realmId=99",listener.Port));
					OAuthRedirectResult result=await task;
					Assert.AreEqual("abc/1",result.Code);
					Assert.AreEqual("s1",result.State);
					Assert.AreEqual("99",result.Get("realmId"));
					StringAssert.StartsWith(response,"HTTP/1.1 200 OK");
					StringAssert.Contains(response,OAuthResponsePage.MessageSuccess);
					StringAssert.Contains(response,"Cache-Control: no-store");
					Assert.IsFalse(response.Contains("<script"),"The page must not run scripts.");
					Assert.IsFalse(response.Contains("src="),"The page must not load anything.");
				}
			});
		}

		[TestMethod]
		public void Listener_IgnoresOtherPathsMethodsAndHosts() {
			AssertEx.Run(async () => {
				using(LoopbackListener listener=new LoopbackListener(0,"/cb/nonce123")) {
					listener.Start();
					Task<OAuthRedirectResult> task=listener.WaitForRedirectAsync(TimeSpan.FromSeconds(20));
					StringAssert.StartsWith(await SendRawAsync(listener.Port,Get("/favicon.ico",listener.Port)),"HTTP/1.1 404");
					StringAssert.StartsWith(await SendRawAsync(listener.Port,Get("/cb?code=wrongpath",listener.Port)),"HTTP/1.1 404");
					StringAssert.StartsWith(await SendRawAsync(listener.Port,
						"POST /cb/nonce123?code=post HTTP/1.1\r\nHost: 127.0.0.1:"+listener.Port+"\r\nContent-Length: 0\r\n\r\n"),"HTTP/1.1 404");
					//DNS rebinding: a page on another name that resolves to 127.0.0.1.
					StringAssert.StartsWith(await SendRawAsync(listener.Port,Get("/cb/nonce123?code=rebind",listener.Port,"evil.example:"+listener.Port)),
						"HTTP/1.1 404");
					Assert.IsFalse(task.IsCompleted,"Only the real redirect ends the wait.");
					await SendRawAsync(listener.Port,Get("/cb/nonce123?code=real&state=s",listener.Port,"localhost:"+listener.Port));
					OAuthRedirectResult result=await task;
					Assert.AreEqual("real",result.Code);
				}
			});
		}

		[TestMethod]
		public void Listener_ErrorRedirectGetsErrorPage() {
			AssertEx.Run(async () => {
				using(LoopbackListener listener=new LoopbackListener()) {
					listener.Start();
					Task<OAuthRedirectResult> task=listener.WaitForRedirectAsync(TimeSpan.FromSeconds(20));
					string response=await SendRawAsync(listener.Port,Get("/?error=access_denied&state=s",listener.Port));
					OAuthRedirectResult result=await task;
					Assert.AreEqual("access_denied",result.Error);
					Assert.IsFalse(result.HasCode);
					StringAssert.Contains(response,OAuthResponsePage.MessageError);
				}
			});
		}

		[TestMethod]
		public void Listener_OversizedRequestIsRefusedAndWaitContinues() {
			AssertEx.Run(async () => {
				using(LoopbackListener listener=new LoopbackListener(0,"/cb")) {
					listener.Start();
					Task<OAuthRedirectResult> task=listener.WaitForRedirectAsync(TimeSpan.FromSeconds(20));
					string huge="GET /cb?code="+new string('a',LoopbackListener.MaxRequestBytes+10)+" HTTP/1.1\r\nHost: 127.0.0.1:"+listener.Port+"\r\n\r\n";
					try {
						await SendRawAsync(listener.Port,huge);
					}
					catch(IOException) {
						//The listener may close the socket before reading everything.
					}
					catch(SocketException) {
					}
					Assert.IsFalse(task.IsCompleted);
					await SendRawAsync(listener.Port,Get("/cb?code=ok",listener.Port));
					Assert.AreEqual("ok",(await task).Code);
				}
			});
		}

		[TestMethod]
		public void Listener_TimesOutAndReleasesPort() {
			AssertEx.Run(async () => {
				int port;
				using(LoopbackListener listener=new LoopbackListener()) {
					listener.Start();
					port=listener.Port;
					await AssertEx.ThrowsAsync<TimeoutException>(() => listener.WaitForRedirectAsync(TimeSpan.FromMilliseconds(200)));
				}
				//The port is free again: a fixed-port listener can bind it.
				using(LoopbackListener listenerFixed=new LoopbackListener(port,"/")) {
					listenerFixed.Start();
					Assert.AreEqual(port,listenerFixed.Port);
				}
			});
		}

		[TestMethod]
		public void Listener_CancelThrowsOperationCanceled() {
			AssertEx.Run(async () => {
				using(LoopbackListener listener=new LoopbackListener())
				using(CancellationTokenSource cts=new CancellationTokenSource()) {
					listener.Start();
					Task<OAuthRedirectResult> task=listener.WaitForRedirectAsync(TimeSpan.FromSeconds(20),cts.Token);
					cts.Cancel();
					await AssertEx.ThrowsAsync<OperationCanceledException>(() => task);
				}
			});
		}

		[TestMethod]
		public void Listener_LocalhostAlsoListensOnIPv6WhenAvailable() {
			using(LoopbackListener listener=new LoopbackListener(0,"/cb","localhost")) {
				listener.Start();
				Assert.AreEqual("http://localhost:"+listener.Port+"/cb",listener.RedirectUri);
			}
		}

		[TestMethod]
		public void Listener_RejectsBadArguments() {
			AssertEx.Throws<ArgumentException>(() => new LoopbackListener(0,"callback"));
			AssertEx.Throws<ArgumentException>(() => new LoopbackListener(0,"/cb?x=1"));
			AssertEx.Throws<ArgumentException>(() => new LoopbackListener(0,"/",redirectHost:"0.0.0.0"));
			AssertEx.Throws<ArgumentOutOfRangeException>(() => new LoopbackListener(70000));
			using(LoopbackListener listener=new LoopbackListener()) {
				AssertEx.Throws<InvalidOperationException>(() => { string uri=listener.RedirectUri; });
			}
		}
	}
}
