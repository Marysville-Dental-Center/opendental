// v25_3-misc-pem-path-label and its 25.4 twin v25_4-setup-pem-path-label: the SSL certificate-authority file path field has the same
// vendor-neutral label in Choose Database, Report Setup and Preferences > Server Connections, and its text box is named textSslCaPem. The windows
// are not opened (the GUI is never launched in tests); the designer files are checked instead.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BSERVERTests {
	[TestClass]
	public class PemPathLabelTests {
		public const string LABEL="SSL CA certificate (.pem) path";

		private static readonly string[][] _arrayDesignerFiles=new[] {
			new[] { "OpenDental","Forms","FormChooseDatabase.Designer.cs" },
			new[] { "OpenDental","Forms","FormReportSetup.designer.cs" },
			new[] { "OpenDental","User Controls","Preferences","UserControlServerConnections.Designer.cs" },
		};

		private static string ReadRepoFile(string[] arrayParts) {
			string path=TestPaths.GetRepoFile(arrayParts);
			Assert.IsTrue(File.Exists(path),path);
			return File.ReadAllText(path);
		}

		[TestMethod]
		public void EveryWindow_UsesTheVendorNeutralLabel() {
			foreach(string[] arrayParts in _arrayDesignerFiles) {
				string text=ReadRepoFile(arrayParts);
				StringAssert.Contains(text,".Text = \""+LABEL+"\";",string.Join("/",arrayParts));
				Assert.IsFalse(Regex.IsMatch(text,"Sky ?SQL",RegexOptions.IgnoreCase),string.Join("/",arrayParts)+" still names SkySQL.");
			}
		}

		[TestMethod]
		public void EveryWindow_NamesTheTextBoxTextSslCaPem() {
			foreach(string[] arrayParts in _arrayDesignerFiles) {
				string text=ReadRepoFile(arrayParts);
				StringAssert.Contains(text,"this.textSslCaPem = new System.Windows.Forms.TextBox();",string.Join("/",arrayParts));
				Assert.IsFalse(Regex.IsMatch(text,@"\btext(PEM|SkySQL)\b"),string.Join("/",arrayParts)+" still has the old text box name.");
			}
		}

		[TestMethod]
		public void CodeBehind_SavesTheSameStoredValues() {
			//The stored values do not change: the connection's SslCA, ReportingServerSslCa and ReadOnlyServerSslCa.
			StringAssert.Contains(ReadRepoFile(new[] { "OpenDental","Forms","FormChooseDatabase.cs" }),"CentralConnectionCur.SslCA=textSslCaPem.Text;");
			StringAssert.Contains(ReadRepoFile(new[] { "OpenDental","Forms","FormReportSetup.cs" }),
				"Prefs.UpdateString(PrefName.ReportingServerSslCa,textSslCaPem.Text);");
			StringAssert.Contains(ReadRepoFile(new[] { "OpenDental","User Controls","Preferences","UserControlServerConnections.cs" }),
				"Prefs.UpdateString(PrefName.ReadOnlyServerSslCa,textSslCaPem.Text);");
		}
	}
}
