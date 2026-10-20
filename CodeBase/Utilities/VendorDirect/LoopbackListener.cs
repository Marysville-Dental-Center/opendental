// Vendor-direct integrations: a one-shot loopback HTTP listener that catches an OAuth (or hosted payment page) redirect.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBase.VendorDirect {
	///<summary>Catches one browser redirect on this computer. Binds 127.0.0.1 (and ::1 too when the redirect host is "localhost") with a TcpListener,
	///so no http.sys URL reservation is needed. Accepts exactly one GET on the expected path (or POST, when AcceptPost is set); any other path, method
	///or Host header gets 404 and the wait goes on. Reads at most 16 KB per request and never logs or stores it. The caller checks the returned state.
	///Use one instance per sign-in: Start, put RedirectUri on the authorization URL, open the browser, then await WaitForRedirectAsync.</summary>
	public sealed class LoopbackListener : IDisposable {
		///<summary>Default time to wait for the browser: 5 minutes.</summary>
		public static readonly TimeSpan DefaultTimeout=TimeSpan.FromMinutes(5);
		///<summary>Largest request read, headers included.</summary>
		public const int MaxRequestBytes=16*1024;
		///<summary>How long one connection may take to send its request.</summary>
		private static readonly TimeSpan _timeSpanPerConnection=TimeSpan.FromSeconds(10);

		private readonly string _redirectHost;
		private readonly string _path;
		private readonly int _portRequested;
		private TcpListener _tcpListenerV4;
		private TcpListener _tcpListenerV6;
		private int _isDisposed;

		///<summary>port 0 lets the operating system pick a free port (Google, EdgeExpress). Pass a fixed port for vendors that match the registered
		///redirect exactly (QuickBooks Online sandbox and relay). path must start with '/'; it may carry a per-session nonce.
		///redirectHost is "127.0.0.1" (default), "localhost" or "[::1]", and is used as written in RedirectUri.</summary>
		public LoopbackListener(int port=0,string path="/",string redirectHost="127.0.0.1") {
			if(port<0 || port>65535) {
				throw new ArgumentOutOfRangeException(nameof(port));
			}
			if(string.IsNullOrEmpty(path) || path[0]!='/' || path.IndexOfAny(new[] { '?','#',' ' })>=0) {
				throw new ArgumentException("The path must start with '/' and have no query or fragment.",nameof(path));
			}
			if(redirectHost!="127.0.0.1" && redirectHost!="localhost" && redirectHost!="[::1]") {
				throw new ArgumentException("The redirect host must be 127.0.0.1, localhost or [::1].",nameof(redirectHost));
			}
			_portRequested=port;
			_path=path;
			_redirectHost=redirectHost;
		}

		///<summary>The bound port. 0 until Start.</summary>
		public int Port { get; private set; }

		///<summary>False (default): only a GET ends the wait, as OAuth redirects are. True: a POST on the expected path ends it too, for a hosted payment
		///page whose return may be a form post. The body is read (Content-Length only, within MaxRequestBytes in all) and discarded unread; only the
		///query string is returned. Set before WaitForRedirectAsync.</summary>
		public bool AcceptPost { get; set; }

		///<summary>The page sent back for the expected path. Null (default) sends the sign-in page (OAuthResponsePage). Must be self-contained: no
		///scripts or external assets. Set before WaitForRedirectAsync.</summary>
		public string ResponseHtml { get; set; }

		///<summary>"http://127.0.0.1:{port}{path}", the redirect_uri to send. Valid after Start.</summary>
		public string RedirectUri {
			get {
				if(Port==0) {
					throw new InvalidOperationException("Call Start first.");
				}
				return "http://"+_redirectHost+":"+Port+_path;
			}
		}

		///<summary>Binds the socket. Throws SocketException if a fixed port is in use.</summary>
		public void Start() {
			if(Port!=0) {
				return;
			}
			if(_redirectHost=="[::1]") {
				_tcpListenerV6=new TcpListener(IPAddress.IPv6Loopback,_portRequested);
				_tcpListenerV6.Start();
				Port=((IPEndPoint)_tcpListenerV6.LocalEndpoint).Port;
				return;
			}
			_tcpListenerV4=new TcpListener(IPAddress.Loopback,_portRequested);
			_tcpListenerV4.Start();
			Port=((IPEndPoint)_tcpListenerV4.LocalEndpoint).Port;
			if(_redirectHost=="localhost" && Socket.OSSupportsIPv6) {
				//Browsers may resolve localhost to ::1 first. Listen there on the same port too, if we can.
				try {
					_tcpListenerV6=new TcpListener(IPAddress.IPv6Loopback,Port);
					_tcpListenerV6.Start();
				}
				catch(SocketException) {
					_tcpListenerV6=null;
				}
			}
		}

		///<summary>Waits for the redirect and returns its parameters. Throws TimeoutException after timeout (default 5 minutes), and
		///OperationCanceledException when cancellationToken is cancelled (wire it to the progress window's Cancel). The sockets are released on
		///every path, so the listener can't be reused afterwards.</summary>
		public async Task<OAuthRedirectResult> WaitForRedirectAsync(TimeSpan? timeout=null,CancellationToken cancellationToken=default(CancellationToken)) {
			if(Port==0) {
				throw new InvalidOperationException("Call Start first.");
			}
			using(CancellationTokenSource ctsTimeout=new CancellationTokenSource(timeout ?? DefaultTimeout))
			using(CancellationTokenSource ctsLinked=CancellationTokenSource.CreateLinkedTokenSource(ctsTimeout.Token,cancellationToken))
			using(ctsLinked.Token.Register(StopListeners)) {
				Task<TcpClient> taskAcceptV4=null;
				Task<TcpClient> taskAcceptV6=null;
				try {
					taskAcceptV4=_tcpListenerV4?.AcceptTcpClientAsync();
					taskAcceptV6=_tcpListenerV6?.AcceptTcpClientAsync();
					while(true) {
						List<Task<TcpClient>> listTasks=new List<Task<TcpClient>>();
						if(taskAcceptV4!=null) {
							listTasks.Add(taskAcceptV4);
						}
						if(taskAcceptV6!=null) {
							listTasks.Add(taskAcceptV6);
						}
						Task<TcpClient> taskDone=await Task.WhenAny(listTasks).ConfigureAwait(false);
						ctsLinked.Token.ThrowIfCancellationRequested();
						TcpClient tcpClient=await taskDone.ConfigureAwait(false);
						if(taskDone==taskAcceptV4) {
							taskAcceptV4=_tcpListenerV4.AcceptTcpClientAsync();
						}
						else {
							taskAcceptV6=_tcpListenerV6.AcceptTcpClientAsync();
						}
						OAuthRedirectResult result=await HandleConnectionAsync(tcpClient,ctsLinked.Token).ConfigureAwait(false);
						if(result!=null) {
							return result;
						}
					}
				}
				catch(Exception ex) when(ex is ObjectDisposedException || ex is SocketException || ex is OperationCanceledException
					|| ex is InvalidOperationException)
				{
					if(cancellationToken.IsCancellationRequested) {
						throw new OperationCanceledException("Sign-in was cancelled.",ex,cancellationToken);
					}
					if(ctsTimeout.IsCancellationRequested) {
						throw new TimeoutException("Timed out waiting for the browser to finish signing in.");
					}
					throw;
				}
				finally {
					StopListeners();
					ObserveAndRelease(taskAcceptV4);
					ObserveAndRelease(taskAcceptV6);
				}
			}
		}

		///<summary>An accept still pending when the wait ends faults once the listener stops. Observe it, and close a connection it may have taken.</summary>
		private static void ObserveAndRelease(Task<TcpClient> taskAccept) {
			if(taskAccept==null) {
				return;
			}
			taskAccept.ContinueWith(x => {
				if(x.Status==TaskStatus.RanToCompletion) {
					x.Result?.Close();
				}
				else {
					_=x.Exception;
				}
			},TaskScheduler.Default);
		}

		///<summary>Reads one request. Returns the redirect result for a GET on the expected path, or null after answering 404.</summary>
		private async Task<OAuthRedirectResult> HandleConnectionAsync(TcpClient tcpClient,CancellationToken cancellationToken) {
			using(tcpClient)
			using(CancellationTokenSource ctsConnection=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
			using(ctsConnection.Token.Register(() => tcpClient.Close())) {
				ctsConnection.CancelAfter(_timeSpanPerConnection);
				NetworkStream stream;
				RequestHead requestHead;
				try {
					stream=tcpClient.GetStream();
					requestHead=await ReadRequestHeadAsync(stream).ConfigureAwait(false);
				}
				catch(Exception ex) when(ex is IOException || ex is ObjectDisposedException || ex is SocketException || ex is InvalidOperationException) {
					cancellationToken.ThrowIfCancellationRequested();
					return null;//This connection was slow or broken. Keep waiting for the real redirect.
				}
				string query;
				long contentLength;
				if(requestHead==null || !TryParseRequest(requestHead.Text,out query,out contentLength)) {
					await WriteResponseSafeAsync(stream,"404 Not Found","").ConfigureAwait(false);
					return null;
				}
				if(contentLength>0) {
					//A POST return: read the body so the browser gets a clean answer, but never look at it.
					await DiscardBodySafeAsync(stream,contentLength-requestHead.CountBodyBytesRead,MaxRequestBytes-requestHead.CountBytesRead)
						.ConfigureAwait(false);
				}
				OAuthRedirectResult result=new OAuthRedirectResult(OAuthRedirectParser.ParseQuery(query));
				bool isError=!result.HasCode;
				await WriteResponseSafeAsync(stream,"200 OK",ResponseHtml??OAuthResponsePage.GetHtml(isError)).ConfigureAwait(false);
				return result;
			}
		}

		///<summary>The request line and headers, and how much was read with them.</summary>
		private sealed class RequestHead {
			///<summary>Everything before the blank line that ends the headers.</summary>
			public string Text;
			///<summary>All bytes read so far, headers included.</summary>
			public int CountBytesRead;
			///<summary>Bytes read past the blank line: the start of a body.</summary>
			public int CountBodyBytesRead;
		}

		///<summary>Reads up to the blank line that ends the headers. Returns null if the request is larger than MaxRequestBytes or ends early.</summary>
		private static async Task<RequestHead> ReadRequestHeadAsync(NetworkStream stream) {
			byte[] buffer=new byte[MaxRequestBytes];
			int countRead=0;
			while(countRead<buffer.Length) {
				int count=await stream.ReadAsync(buffer,countRead,buffer.Length-countRead).ConfigureAwait(false);
				if(count==0) {
					return null;
				}
				countRead+=count;
				string text=Encoding.ASCII.GetString(buffer,0,countRead);
				int indexEnd=text.IndexOf("\r\n\r\n",StringComparison.Ordinal);
				if(indexEnd>=0) {
					RequestHead requestHead=new RequestHead();
					requestHead.Text=text.Substring(0,indexEnd);
					requestHead.CountBytesRead=countRead;
					requestHead.CountBodyBytesRead=countRead-(indexEnd+4);//ASCII: one byte per character.
					return requestHead;
				}
			}
			return null;
		}

		///<summary>Reads and throws away up to countRemaining body bytes, but never more than countAllowed. A body larger than that is left unread;
		///the connection is closed after the answer anyway.</summary>
		private static async Task DiscardBodySafeAsync(NetworkStream stream,long countRemaining,int countAllowed) {
			long countToRead=Math.Min(countRemaining,countAllowed);
			byte[] buffer=new byte[4096];
			try {
				while(countToRead>0) {
					int count=await stream.ReadAsync(buffer,0,(int)Math.Min(buffer.Length,countToRead)).ConfigureAwait(false);
					if(count==0) {
						return;
					}
					countToRead-=count;
				}
			}
			catch(Exception ex) when(ex is IOException || ex is ObjectDisposedException || ex is SocketException) {
				//The browser went away. The return was still received.
			}
		}

		///<summary>True if the request is "GET {path}[?query] HTTP/1.x" (or POST, when AcceptPost is set) with a loopback Host header for our port.
		///Sets query to the part after '?', and contentLength to a POST's Content-Length (0 for a GET or when none is given).</summary>
		private bool TryParseRequest(string requestHead,out string query,out long contentLength) {
			query="";
			contentLength=0;
			string[] arrayLines=requestHead.Split(new[] { "\r\n" },StringSplitOptions.None);
			string[] arrayRequestLine=arrayLines[0].Split(' ');
			if(arrayRequestLine.Length!=3 || !arrayRequestLine[2].StartsWith("HTTP/1.",StringComparison.Ordinal)) {
				return false;
			}
			bool isPost=(arrayRequestLine[0]=="POST");
			if(arrayRequestLine[0]!="GET" && !(isPost && AcceptPost)) {
				return false;
			}
			string target=arrayRequestLine[1];
			int indexQuery=target.IndexOf('?');
			string path=(indexQuery<0) ? target : target.Substring(0,indexQuery);
			if(path!=_path) {
				return false;
			}
			string host=null;
			for(int i=1;i<arrayLines.Length;i++) {
				int indexColon=arrayLines[i].IndexOf(':');
				if(indexColon<=0) {
					continue;
				}
				string headerName=arrayLines[i].Substring(0,indexColon).Trim();
				if(host==null && headerName.Equals("Host",StringComparison.OrdinalIgnoreCase)) {
					host=arrayLines[i].Substring(indexColon+1).Trim().ToLowerInvariant();
				}
				else if(isPost && headerName.Equals("Content-Length",StringComparison.OrdinalIgnoreCase)) {
					long length;
					if(long.TryParse(arrayLines[i].Substring(indexColon+1).Trim(),out length) && length>0) {
						contentLength=length;
					}
				}
			}
			//Refuse a Host header that isn't this computer (a web page reaching the port through DNS rebinding).
			string portSuffix=":"+Port;
			if(host!="127.0.0.1"+portSuffix && host!="localhost"+portSuffix && host!="[::1]"+portSuffix) {
				return false;
			}
			if(indexQuery>=0) {
				query=target.Substring(indexQuery+1);
				int indexFragment=query.IndexOf('#');
				if(indexFragment>=0) {
					query=query.Substring(0,indexFragment);
				}
			}
			return true;
		}

		private static async Task WriteResponseSafeAsync(NetworkStream stream,string status,string html) {
			byte[] bytesBody=Encoding.UTF8.GetBytes(html);
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("HTTP/1.1 ").Append(status).Append("\r\n");
			if(bytesBody.Length>0) {
				stringBuilder.Append("Content-Type: text/html; charset=utf-8\r\n");
			}
			stringBuilder.Append("Content-Length: ").Append(bytesBody.Length).Append("\r\n");
			stringBuilder.Append("Cache-Control: no-store\r\n");
			stringBuilder.Append("Pragma: no-cache\r\n");
			stringBuilder.Append("X-Content-Type-Options: nosniff\r\n");
			stringBuilder.Append("Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'\r\n");
			stringBuilder.Append("Referrer-Policy: no-referrer\r\n");
			stringBuilder.Append("Connection: close\r\n\r\n");
			byte[] bytesHead=Encoding.ASCII.GetBytes(stringBuilder.ToString());
			try {
				await stream.WriteAsync(bytesHead,0,bytesHead.Length).ConfigureAwait(false);
				if(bytesBody.Length>0) {
					await stream.WriteAsync(bytesBody,0,bytesBody.Length).ConfigureAwait(false);
				}
				await stream.FlushAsync().ConfigureAwait(false);
			}
			catch(Exception ex) when(ex is IOException || ex is ObjectDisposedException || ex is SocketException) {
				//The browser went away. The redirect was still received.
			}
		}

		private void StopListeners() {
			try {
				_tcpListenerV4?.Stop();
			}
			catch(SocketException) {
			}
			try {
				_tcpListenerV6?.Stop();
			}
			catch(SocketException) {
			}
		}

		public void Dispose() {
			if(Interlocked.Exchange(ref _isDisposed,1)==1) {
				return;
			}
			StopListeners();
		}
	}
}
