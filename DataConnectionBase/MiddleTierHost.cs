// The Middle Tier's host context: what the libraries read from System.Web before stage 0 of the .NET 10 move, now given by the host.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace DataConnectionBase {
	///<summary>What the Middle Tier's host knows and the libraries need (docs/specs/net10-plan.md, stage 0, package S0-4): the application's
	///folder for the whole process, and for the request a thread is handling, the application name and the client's address. The libraries read
	///these from System.Web before, which .NET 10 does not have: HostingEnvironment.ApplicationPhysicalPath and ApplicationVirtualPath, and
	///HttpContext.Current.Request.UserHostAddress and HttpContext.Current.Server.MapPath(null).
	///The host fills it through DtoProcessor.ProcessDto(dtoString,contentRoot,applicationName,clientIp), which the ASMX code-behind
	///(OpenPmsDentalServer/ServiceMain.asmx.cs) calls once for each request with the values it reads from System.Web, so the values are the ones
	///the libraries read before. That form sets the request's values on its thread and clears them when the request ends, also when it fails.
	///With no host (the program, the mock server OpenDentalServerMockIIS, the tests) nothing is set, and every value is what System.Web gave
	///outside a hosted request: null, and no request on any thread.
	///It lives here, not in the business library, because ConnectionStoreBase reads the content root too and cannot see the business
	///library.</summary>
	public static class MiddleTierHost {
		///<summary>The application's folder, one value for the whole process. Null until a host sets it.</summary>
		private static volatile string _contentRoot;
		///<summary>True while this thread handles a request (between BeginRequest and EndRequest).</summary>
		[ThreadStatic]
		private static bool _isInRequestT;
		///<summary>The content root given with this thread's request.</summary>
		[ThreadStatic]
		private static string _contentRootT;
		///<summary>The application name given with this thread's request.</summary>
		[ThreadStatic]
		private static string _applicationNameT;
		///<summary>The client address given with this thread's request.</summary>
		[ThreadStatic]
		private static string _clientIpT;

		///<summary>The physical folder of the Middle Tier application, as HostingEnvironment.ApplicationPhysicalPath gives it on the ASMX host (the
		///IIS application's folder, with a trailing backslash). One value for the whole process, seen by every thread, as HostingEnvironment's
		///was: a thread of a request and any other thread of the server alike. Null until a host sets it: in the program, the mock server and the
		///tests, where HostingEnvironment gave null too. BeginRequest sets it; a host can also set it before its first request.</summary>
		public static string ContentRoot {
			get {
				return _contentRoot;
			}
			set {
				_contentRoot=value;
			}
		}

		///<summary>True while this thread handles a Middle Tier request of a host, that is, where HttpContext.Current was not null before. False on
		///every other thread: threads a request starts, the mock server's thread, the program.</summary>
		public static bool IsInRequest {
			get {
				return _isInRequestT;
			}
		}

		///<summary>The virtual path of the application for this thread's request, as HostingEnvironment.ApplicationVirtualPath gives it on the ASMX
		///host: /OpenDentalServer, or another name when one server hosts several applications, or / for an application at the site root. Null
		///outside a request, as HostingEnvironment gave null with no host. Only code that runs for a request reads it (DtoProcessor and
		///Userods.LoadDatabaseInfoFromFile, which only DtoProcessor calls).</summary>
		public static string ApplicationName {
			get {
				return _applicationNameT;
			}
		}

		///<summary>The client's address for this thread's request, as HttpContext.Current.Request.UserHostAddress gives it. Null outside a
		///request.</summary>
		public static string ClientIp {
			get {
				return _clientIpT;
			}
		}

		///<summary>The folder of this thread's request, for code that called HttpContext.Current.Server.MapPath(null) before: the request's page,
		///ServiceMain.asmx, is in the application's folder, so this is the content root given with the request. Throws
		///InvalidOperationException on a thread that is not handling a request, where HttpContext.Current was null and the call threw a
		///NullReferenceException; a Middle Tier client gets either as a plain Exception (RemotingClient.ThrowExceptionForDto).</summary>
		public static string GetRequestContentRoot() {
			if(!_isInRequestT) {
				throw new InvalidOperationException("This thread is not handling a Middle Tier request, so it has no request folder.");
			}
			return _contentRootT;
		}

		///<summary>Called by the host when this thread starts handling a request, before any business code runs for it. Sets the content root of
		///the process and this thread's request values. EndRequest must follow in a finally block on the same thread. Requests do not nest.
		///</summary>
		public static void BeginRequest(string contentRoot,string applicationName,string clientIp) {
			_contentRoot=contentRoot;
			_contentRootT=contentRoot;
			_applicationNameT=applicationName;
			_clientIpT=clientIp;
			_isInRequestT=true;
		}

		///<summary>Called by the host in a finally block when this thread has finished a request. Clears this thread's request values, so a pooled
		///thread never carries one request's application name or client address into the next. The content root of the process stays, as
		///HostingEnvironment's did.</summary>
		public static void EndRequest() {
			_isInRequestT=false;
			_contentRootT=null;
			_applicationNameT=null;
			_clientIpT=null;
		}
	}
}
