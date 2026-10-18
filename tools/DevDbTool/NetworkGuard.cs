// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net;

namespace DevDbTool {
	///<summary>Blocks and records outbound HTTP(S) traffic made through the .NET web stack (WebRequest, SOAP proxies, WebClient and
	///HttpClient, which all fall back to WebRequest.DefaultWebProxy). Building a dev database must never contact Open Dental's servers
	///or anyone else's, so every request is routed to a dead local proxy and its destination is reported at the end of the run.
	///The MySQL connection uses raw sockets and is unaffected.</summary>
	public static class NetworkGuard {
		private static readonly object _lock=new object();
		private static readonly List<string> _listBlocked=new List<string>();

		///<summary>Destinations of every outbound request that was attempted and blocked, in order.</summary>
		public static List<string> ListBlocked {
			get {
				lock(_lock) {
					return new List<string>(_listBlocked);
				}
			}
		}

		public static void Install() {
			WebRequest.DefaultWebProxy=new BlockingProxy();
		}

		private static void Record(Uri uri) {
			string where=uri?.GetLeftPart(UriPartial.Path)??"(unknown)";
			lock(_lock) {
				_listBlocked.Add(where);
			}
			Console.Error.WriteLine("  [network guard] blocked outbound request to "+where);
		}

		///<summary>Sends every request to a local port nothing listens on, so it fails fast without leaving the machine.</summary>
		private class BlockingProxy : IWebProxy {
			private static readonly Uri _uriDeadEnd=new Uri("http://127.0.0.1:9/");

			public ICredentials Credentials { get; set; }

			public Uri GetProxy(Uri destination) {
				Record(destination);
				return _uriDeadEnd;
			}

			public bool IsBypassed(Uri host) {
				return false;
			}
		}
	}
}
