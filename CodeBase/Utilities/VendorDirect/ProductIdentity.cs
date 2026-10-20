// Vendor-direct integrations: the neutral product identity sent to vendors, and the system browser launcher.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;

namespace CodeBase.VendorDirect {
	///<summary>The product name and version this program gives vendors: the User-Agent header, and sender or product fields that vendors ask for
	///(the PDMP sender software, the eRx product name). Phase 4 branding sets the final values here and nowhere else.</summary>
	public static class ProductIdentity {
		///<summary>The product name shown to vendors where a display name is wanted.</summary>
		public const string ProductName="Open PMS - Dental";
		///<summary>The product name as an HTTP token (no spaces), for User-Agent.</summary>
		public const string ProductToken="OpenPMS-Dental";

		///<summary>The program version, from this assembly (all of the program's assemblies share one version).</summary>
		public static string Version {
			get {
				Version version=typeof(ProductIdentity).Assembly.GetName().Version;
				return (version==null) ? "0.0" : version.ToString();
			}
		}

		///<summary>"OpenPMS-Dental/24.3.63.0". Names no company.</summary>
		public static string UserAgent {
			get {
				return ProductToken+"/"+Version;
			}
		}
	}

	///<summary>Opens a vendor's sign-in or hosted page in the user's default browser.</summary>
	public static class SystemBrowser {
		///<summary>Opens url with the shell (UseShellExecute=true). Refuses anything VendorUrlPolicy refuses (an Open Dental host, plain http to
		///another computer, a relative address): throws ArgumentException without starting anything.</summary>
		public static void Open(string url) {
			VendorUrlProblem problem=VendorUrlPolicy.Check(url);
			if(problem!=VendorUrlProblem.None) {
				throw new ArgumentException(VendorUrlPolicy.Describe(problem),nameof(url));
			}
			ProcessStartInfo processStartInfo=new ProcessStartInfo(url.Trim());
			processStartInfo.UseShellExecute=true;
			using(Process.Start(processStartInfo)) {
			}
		}
	}

	///<summary>The page the loopback listener returns to the browser after a sign-in. Self-contained: no scripts, images, external styles or
	///branding. Microsoft 365 (MSAL's own listener) shows the same text.</summary>
	public static class OAuthResponsePage {
		public const string TitleSuccess="Sign-in finished";
		public const string MessageSuccess="Sign-in finished. You can close this tab and return to the program.";
		public const string TitleError="Sign-in not finished";
		public const string MessageError="Sign-in was not finished. You can close this tab and return to the program to try again.";

		///<summary>The HTML for the success page, or for the error page when isError is true.</summary>
		public static string GetHtml(bool isError) {
			string title=isError ? TitleError : TitleSuccess;
			string message=isError ? MessageError : MessageSuccess;
			return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>"+title+"</title>"
				+"<style>body{font-family:sans-serif;margin:3em;}</style></head>"
				+"<body><p>"+message+"</p></body></html>";
		}
	}
}
