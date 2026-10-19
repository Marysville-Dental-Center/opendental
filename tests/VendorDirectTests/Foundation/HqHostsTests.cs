// Tests for the blocked-host list and the vendor URL policy. This file names the blocked hosts on purpose; tools/Test-NoHqHosts.ps1 allows it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VendorDirectTests.Foundation {
	[TestClass]
	public class HqHostsTests {
		[TestMethod]
		public void BlockedDomains_AreTheFiveOpenDentalDomains() {
			CollectionAssert.AreEquivalent(new[] { "opendental.com","open-dent.com","opendentalsoft.com","patientviewer.com","opendental.cloud" },
				HqHosts.BlockedDomains.ToArray());
		}

		[TestMethod]
		public void IsBlocked_DomainsAndSubdomains() {
			Assert.IsTrue(HqHosts.IsBlocked("https://opendental.com/"));
			Assert.IsTrue(HqHosts.IsBlocked("https://www.opendental.com/manual"));
			Assert.IsTrue(HqHosts.IsBlocked("https://API.OpenDental.com:443/x"));
			Assert.IsTrue(HqHosts.IsBlocked("http://emailhosting.opendental.cloud"));
			Assert.IsTrue(HqHosts.IsBlocked("https://a.b.patientviewer.com"));
			Assert.IsTrue(HqHosts.IsBlocked("https://open-dent.com"));
			Assert.IsTrue(HqHosts.IsBlocked("https://opendentalsoft.com."),"A trailing dot is the same host.");
			Assert.IsTrue(HqHosts.IsBlocked("https://user:pw@opendental.com/"),"User info doesn't hide the host.");
			Assert.IsTrue(HqHosts.IsBlockedHost(" WWW.OPENDENTAL.COM "));
		}

		[TestMethod]
		public void IsBlocked_LookalikesAreNotBlocked() {
			Assert.IsFalse(HqHosts.IsBlocked("https://notopendental.com"));
			Assert.IsFalse(HqHosts.IsBlocked("https://opendental.com.example.org"));
			Assert.IsFalse(HqHosts.IsBlocked("https://opendental.com@vendor.example/"),"Here the host is vendor.example.");
			Assert.IsFalse(HqHosts.IsBlocked("https://api.dentalxchange.com"));
			Assert.IsFalse(HqHosts.IsBlocked("not a url"));
			Assert.IsFalse(HqHosts.IsBlocked((string)null));
			Assert.IsFalse(HqHosts.IsBlocked(new Uri("/relative",UriKind.Relative)));
		}

		[TestMethod]
		public void UrlPolicy_HttpsOnlyExceptLoopback() {
			Assert.AreEqual(VendorUrlProblem.None,VendorUrlPolicy.Check("https://api.vendor.example/v1"));
			Assert.AreEqual(VendorUrlProblem.NotHttps,VendorUrlPolicy.Check("http://api.vendor.example/v1"));
			Assert.AreEqual(VendorUrlProblem.None,VendorUrlPolicy.Check("http://127.0.0.1:8765/callback"));
			Assert.AreEqual(VendorUrlProblem.None,VendorUrlPolicy.Check("http://localhost/callback"));
			Assert.AreEqual(VendorUrlProblem.None,VendorUrlPolicy.Check("http://[::1]:5000/"));
			Assert.AreEqual(VendorUrlProblem.NotAbsolute,VendorUrlPolicy.Check("ftp://files.vendor.example/"));
			Assert.AreEqual(VendorUrlProblem.NotAbsolute,VendorUrlPolicy.Check("/relative"));
			Assert.AreEqual(VendorUrlProblem.NotAbsolute,VendorUrlPolicy.Check(""));
			Assert.AreEqual(VendorUrlProblem.BlockedHost,VendorUrlPolicy.Check("https://www.opendental.com/"));
			Assert.AreEqual(VendorUrlProblem.BlockedHost,VendorUrlPolicy.Check("http://opendental.com/"),"Blocked wins over the http check.");
		}

		[TestMethod]
		public void UrlPolicy_AllowList() {
			string[] arrayAllowed={ "*.dentalxchange.com","api.paysimple.com" };
			Assert.AreEqual(VendorUrlProblem.None,VendorUrlPolicy.Check("https://api.dentalxchange.com/payments",arrayAllowed));
			Assert.AreEqual(VendorUrlProblem.None,VendorUrlPolicy.Check("https://dentalxchange.com/",arrayAllowed));
			Assert.AreEqual(VendorUrlProblem.None,VendorUrlPolicy.Check("https://API.PAYSIMPLE.COM/v4",arrayAllowed));
			Assert.AreEqual(VendorUrlProblem.HostNotAllowed,VendorUrlPolicy.Check("https://sandbox-api.paysimple.com/v4",arrayAllowed));
			Assert.AreEqual(VendorUrlProblem.HostNotAllowed,VendorUrlPolicy.Check("https://evil-dentalxchange.com/",arrayAllowed));
			Assert.AreEqual(VendorUrlProblem.None,VendorUrlPolicy.Check("http://127.0.0.1:9000/return",arrayAllowed),"Loopback returns are always allowed.");
			Assert.IsTrue(VendorUrlPolicy.HostMatches("a.b.example.com","*.example.com"));
			Assert.IsFalse(VendorUrlPolicy.HostMatches("example.com.evil","*.example.com"));
		}

		[TestMethod]
		public void UrlPolicy_DescribeNamesNoVendor() {
			foreach(VendorUrlProblem problem in Enum.GetValues(typeof(VendorUrlProblem))) {
				string text=VendorUrlPolicy.Describe(problem);
				Assert.IsFalse(text.IndexOf("open dental",StringComparison.OrdinalIgnoreCase)>=0);
			}
		}

		[TestMethod]
		public void SystemBrowser_RefusesBlockedAndPlainHttpWithoutStartingAnything() {
			AssertEx.Throws<ArgumentException>(() => SystemBrowser.Open("https://www.opendental.com/"));
			AssertEx.Throws<ArgumentException>(() => SystemBrowser.Open("http://vendor.example/"));
			AssertEx.Throws<ArgumentException>(() => SystemBrowser.Open("file:///C:/Windows/System32/calc.exe"));
		}
	}
}
