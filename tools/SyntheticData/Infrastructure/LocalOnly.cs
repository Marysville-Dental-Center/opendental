// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace SyntheticData.Infrastructure {
	///<summary>Thrown when a request would leave this machine or touch a database the tool must not write to.</summary>
	public class RefusedException : Exception {
		public RefusedException(string message) : base(message) {
		}
	}

	///<summary>The tool may only ever talk to a MariaDB/MySQL server on this machine. There is deliberately no override: the office
	///server and every other host are refused, whatever the command line says.</summary>
	public static class LocalOnly {
		private static readonly HashSet<string> _setLocalHosts=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"localhost","127.0.0.1","::1","[::1]",
		};

		///<summary>Prefix every database the generator writes must start with, so it can never fill or drop a real database by mistake.</summary>
		public const string WritableDatabasePrefix="synth";

		public static bool IsLocalHost(string host) {
			return host!=null && _setLocalHosts.Contains(host.Trim());
		}

		public static void EnsureLocalHost(string host) {
			if(!IsLocalHost(host)) {
				throw new RefusedException("Server '"+host+"' is not this machine. SyntheticData only connects to localhost, 127.0.0.1 or ::1.");
			}
		}

		///<summary>True for names the generator may create, fill or drop: they start with "synth" and contain only lower-case letters,
		///digits and underscores.</summary>
		public static bool IsWritableDatabaseName(string database) {
			return database!=null && Regex.IsMatch(database,"^"+WritableDatabasePrefix+"[a-z0-9_]{0,59}$");
		}

		public static void EnsureWritableDatabaseName(string database) {
			if(!IsWritableDatabaseName(database)) {
				throw new RefusedException("Database '"+database+"' cannot be written. Target databases must be named synth..."
					+" (lower-case letters, digits and underscores), for example synth_253.");
			}
		}

		///<summary>Database names that are only ever read: letters, digits, underscores and hyphens.</summary>
		public static void EnsureSafeIdentifier(string database) {
			if(database==null || !Regex.IsMatch(database,"^[A-Za-z0-9_-]{1,64}$")) {
				throw new RefusedException("'"+database+"' is not a valid database name.");
			}
		}
	}

	///<summary>Blocks outbound HTTP(S) made through the .NET web stack. The tool has no reason to make any; this makes sure a
	///dependency cannot either. The MySQL connection uses raw sockets and is checked by LocalOnly instead.</summary>
	public static class NetworkGuard {
		private static readonly object _lock=new object();
		private static readonly List<string> _listBlocked=new List<string>();

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
			lock(_lock) {
				_listBlocked.Add(uri?.GetLeftPart(UriPartial.Path)??"(unknown)");
			}
			Console.Error.WriteLine("  [network guard] blocked outbound request to "+uri);
		}

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
