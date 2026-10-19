// Vendor-direct integrations: the hosts this program must never contact, and the URL checks every vendor call goes through.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;

namespace CodeBase.VendorDirect {
	///<summary>The single home of the Open Dental Software, Inc. host list. Vendor integrations talk to the vendor directly; any URL on these domains
	///(or their subdomains) is refused, whether it comes from code, a setup screen or a vendor's response. tools/Test-NoHqHosts.ps1 allows this
	///file by name.</summary>
	public static class HqHosts {
		private static readonly ReadOnlyCollection<string> _listBlockedDomains=new ReadOnlyCollection<string>(new[] {
			"opendental.com",
			"open-dent.com",
			"opendentalsoft.com",
			"patientviewer.com",
			"opendental.cloud",
		});

		///<summary>The blocked registrable domains, lower case. Each also blocks every subdomain.</summary>
		public static IReadOnlyList<string> BlockedDomains {
			get {
				return _listBlockedDomains;
			}
		}

		///<summary>True if host is one of the blocked domains or a subdomain of one. Ignores letter case, surrounding whitespace and a trailing dot.</summary>
		public static bool IsBlockedHost(string host) {
			string hostNormal=NormalizeHost(host);
			if(hostNormal=="") {
				return false;
			}
			for(int i=0;i<_listBlockedDomains.Count;i++) {
				string domain=_listBlockedDomains[i];
				if(hostNormal==domain || hostNormal.EndsWith("."+domain,StringComparison.Ordinal)) {
					return true;
				}
			}
			return false;
		}

		///<summary>True if the absolute URI points at a blocked host. Checks both the host as written and its IDN (punycode) form.
		///A relative or null URI returns false: there is no host to check.</summary>
		public static bool IsBlocked(Uri uri) {
			if(uri==null || !uri.IsAbsoluteUri) {
				return false;
			}
			if(IsBlockedHost(uri.Host)) {
				return true;
			}
			string idnHost;
			try {
				idnHost=uri.IdnHost;
			}
			catch(Exception) {//Malformed international names: treat as blocked rather than guess.
				return true;
			}
			return IsBlockedHost(idnHost);
		}

		///<summary>True if url is an absolute URL on a blocked host. Text that doesn't parse as an absolute URL returns false.</summary>
		public static bool IsBlocked(string url) {
			if(string.IsNullOrWhiteSpace(url)) {
				return false;
			}
			Uri uri;
			if(!Uri.TryCreate(url.Trim(),UriKind.Absolute,out uri)) {
				return false;
			}
			return IsBlocked(uri);
		}

		internal static string NormalizeHost(string host) {
			if(string.IsNullOrWhiteSpace(host)) {
				return "";
			}
			string hostNormal=host.Trim().ToLowerInvariant().TrimEnd('.');
			if(hostNormal.StartsWith("[") && hostNormal.EndsWith("]")) {
				hostNormal=hostNormal.Substring(1,hostNormal.Length-2);
			}
			return hostNormal;
		}
	}

	///<summary>Why VendorUrlPolicy refused a URL.</summary>
	public enum VendorUrlProblem {
		///<summary>The URL is allowed.</summary>
		None,
		///<summary>Blank, relative, or not http/https.</summary>
		NotAbsolute,
		///<summary>An Open Dental Software, Inc. host (HqHosts).</summary>
		BlockedHost,
		///<summary>Plain http to anything other than this computer.</summary>
		NotHttps,
		///<summary>Not on the integration's host allow-list.</summary>
		HostNotAllowed,
	}

	///<summary>The URL rules for vendor integrations: HTTPS only (plain http only to this computer), never an Open Dental host, and, when the
	///integration has one, only hosts on its allow-list. Used by VendorHttp, by setup screens before they save a URL, before a URL a vendor handed
	///back is opened, and by WebView2 navigation guards.</summary>
	public static class VendorUrlPolicy {
		///<summary>True for localhost, 127.0.0.0/8 and ::1.</summary>
		public static bool IsLoopbackHost(string host) {
			string hostNormal=HqHosts.NormalizeHost(host);
			if(hostNormal=="localhost") {
				return true;
			}
			IPAddress ipAddress;
			if(IPAddress.TryParse(hostNormal,out ipAddress)) {
				return IPAddress.IsLoopback(ipAddress);
			}
			return false;
		}

		///<summary>Matches a host against one allow-list pattern. "*.example.com" matches example.com and every subdomain of it. Anything else must
		///match exactly. Letter case is ignored.</summary>
		public static bool HostMatches(string host,string pattern) {
			string hostNormal=HqHosts.NormalizeHost(host);
			string patternNormal=HqHosts.NormalizeHost(pattern);
			if(hostNormal=="" || patternNormal=="") {
				return false;
			}
			if(patternNormal.StartsWith("*.")) {
				string domain=patternNormal.Substring(2);
				return hostNormal==domain || hostNormal.EndsWith("."+domain,StringComparison.Ordinal);
			}
			return hostNormal==patternNormal;
		}

		///<summary>Checks uri against the rules. listAllowedHostPatterns may be null or empty, meaning any host that isn't blocked.</summary>
		public static VendorUrlProblem Check(Uri uri,IEnumerable<string> listAllowedHostPatterns=null) {
			if(uri==null || !uri.IsAbsoluteUri || (uri.Scheme!=Uri.UriSchemeHttps && uri.Scheme!=Uri.UriSchemeHttp) || string.IsNullOrEmpty(uri.Host)) {
				return VendorUrlProblem.NotAbsolute;
			}
			if(HqHosts.IsBlocked(uri)) {
				return VendorUrlProblem.BlockedHost;
			}
			bool isLoopback=IsLoopbackHost(uri.Host);
			if(uri.Scheme==Uri.UriSchemeHttp && !isLoopback) {
				return VendorUrlProblem.NotHttps;
			}
			List<string> listPatterns=(listAllowedHostPatterns==null) ? new List<string>() : listAllowedHostPatterns.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
			if(listPatterns.Count>0 && !isLoopback && !listPatterns.Any(x => HostMatches(uri.Host,x))) {
				return VendorUrlProblem.HostNotAllowed;
			}
			return VendorUrlProblem.None;
		}

		///<summary>Checks a URL as text, for setup screens. Blank or unparsable text gives NotAbsolute.</summary>
		public static VendorUrlProblem Check(string url,IEnumerable<string> listAllowedHostPatterns=null) {
			if(string.IsNullOrWhiteSpace(url)) {
				return VendorUrlProblem.NotAbsolute;
			}
			Uri uri;
			if(!Uri.TryCreate(url.Trim(),UriKind.Absolute,out uri)) {
				return VendorUrlProblem.NotAbsolute;
			}
			return Check(uri,listAllowedHostPatterns);
		}

		///<summary>True if Check finds no problem.</summary>
		public static bool IsAllowed(Uri uri,IEnumerable<string> listAllowedHostPatterns=null) {
			return Check(uri,listAllowedHostPatterns)==VendorUrlProblem.None;
		}

		///<summary>A short message for a setup screen. Names no vendor.</summary>
		public static string Describe(VendorUrlProblem problem) {
			switch(problem) {
				case VendorUrlProblem.None:
					return "";
				case VendorUrlProblem.NotAbsolute:
					return "Enter a full address that starts with https://.";
				case VendorUrlProblem.BlockedHost:
					return "This address belongs to a service this program does not contact.";
				case VendorUrlProblem.NotHttps:
					return "The address must start with https://.";
				case VendorUrlProblem.HostNotAllowed:
					return "This address is not one this integration uses.";
				default:
					return problem.ToString();
			}
		}
	}
}
