// The Open Dental 26.1 preference that allows JavaScript in PDFs shown in the Imaging module: the rule that reads it, and the source checks
// that every PDF navigation of the Imaging viewers applies it and that Preferences > Imaging > General edits it. The windows cannot be opened
// in a test run (the GUI is not launched), so their sources are read. Licensed under the GNU General Public License, version 2 or later.
// See LICENSE in the repository root.
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2IMGTests {
	[TestClass]
	public class PdfJavascriptPrefTests {
		private const string HOOK="SetScriptEnabled(ImageHelper.IsPdfJavascriptAllowed());";

		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		private static int Count(string source,string text) {
			return Regex.Matches(source,Regex.Escape(text)).Count;
		}

		///<summary>The text of one method: from its signature to the first closing brace at the method's own indent (two tabs).</summary>
		private static string MethodBody(string source,string signature) {
			int idxStart=source.IndexOf(signature);
			Assert.IsTrue(idxStart>=0,signature+" not found.");
			int idxEnd=source.IndexOf("\n\t\t}",idxStart);
			Assert.IsTrue(idxEnd>idxStart,signature+" has no closing brace.");
			return source.Substring(idxStart,idxEnd-idxStart);
		}

		private static void AssertBefore(string body,string before,string after) {
			int idxBefore=body.IndexOf(before);
			int idxAfter=body.IndexOf(after);
			Assert.IsTrue(idxBefore>=0,before+" not found.");
			Assert.IsTrue(idxAfter>=0,after+" not found.");
			Assert.IsTrue(idxBefore<idxAfter,before+" must come before "+after);
		}

		///<summary>Fills the preference cache from a made-up table instead of a database. Null leaves the preference out.</summary>
		private static void FillPrefCache(string valueImagingAllowPdfJavascript) {
			DataTable table=new DataTable();
			table.Columns.Add("PrefNum");
			table.Columns.Add("PrefName");
			table.Columns.Add("ValueString");
			table.Rows.Add("1",nameof(PrefName.PdfLaunchWindow),"0");
			if(valueImagingAllowPdfJavascript!=null) {
				table.Rows.Add("2",nameof(PrefName.ImagingAllowPdfJavascript),valueImagingAllowPdfJavascript);
			}
			Prefs.FillCacheFromTable(table);
		}

		[TestMethod]
		public void IsPdfJavascriptAllowed_FollowsThePreference() {
			FillPrefCache("0");
			Assert.IsFalse(ImageHelper.IsPdfJavascriptAllowed(),"The 26.1 default '0' keeps scripts off.");
			FillPrefCache("1");
			Assert.IsTrue(ImageHelper.IsPdfJavascriptAllowed());
		}

		[TestMethod]
		public void IsPdfJavascriptAllowed_OffWhenThePreferenceIsMissing() {
			FillPrefCache(null);
			Assert.IsFalse(ImageHelper.IsPdfJavascriptAllowed(),"A database without the row (not yet migrated) must not run PDF scripts.");
		}

		[TestMethod]
		public void Seed_DefaultIsOff() {
			Assert.AreEqual("0",OpenPmsDentalBusiness.ForkMigrations.Seeds26_1.Seed26_1.ListPrefDefaults.Single(x => x.PrefName==PrefName.ImagingAllowPdfJavascript).ValueString);
		}

		[TestMethod]
		public void ImageDisplay_EveryPdfNavigationAppliesThePreference() {
			string source=Read("OpenPmsDental","User Controls","ControlImageDisplay.cs");
			AssertBefore(MethodBody(source,"private async void LoadPdf("),"_odWebView2."+HOOK,"_odWebView2.ODWebView2Navigate(_odWebView2FilePath);");
			AssertBefore(MethodBody(source,"public async void PrintPDF("),"_odWebView2."+HOOK,"_odWebView2.ODWebView2Navigate(filePath);");
			Assert.AreEqual(Count(source,"ODWebView2Navigate("),Count(source,HOOK),"Every navigation of the PDF viewer reads the preference first.");
			Assert.AreEqual(0,Count(source,"CoreWebView2.Navigate("),"No navigation that skips the preference.");
		}

		[TestMethod]
		public void OldImagingModule_EveryPdfNavigationAppliesThePreference() {
			//Preferences > Imaging > General can still switch to the pre-2020 Imaging module, which shows PDFs in its own WebView2.
			string source=Read("OpenPmsDental","Main Modules","ControlImagesOld.cs");
			AssertBefore(MethodBody(source,"private async void LoadPdf("),"_odWebView2."+HOOK,"_odWebView2.CoreWebView2.Navigate(_odWebView2FilePath);");
			Assert.AreEqual(Count(source,"_odWebView2.CoreWebView2.Navigate(")+Count(source,"_odWebView2.ODWebView2Navigate("),Count(source,HOOK));
		}

		[TestMethod]
		public void WebView2Wrapper_AppliesTheSettingOnNavigationAndOnInitialization() {
			string source=Read("CodeBase","Controls","ODWebView2.cs");
			string body=MethodBody(source,"public void SetScriptEnabled(bool isScriptEnabled)");
			StringAssert.Contains(body,"CoreWebView2.Settings.IsScriptEnabled=isScriptEnabled;");
			StringAssert.Contains(MethodBody(source,"public void CoreWebView2_InitializationCompleted("),"CoreWebView2.Settings.IsScriptEnabled=_isScriptEnabled.Value;");
		}

		[TestMethod]
		public void PreferencesWindow_EditsThePreference() {
			string source=Read("OpenPmsDental","User Controls","Preferences","UserControlImagingGeneral.cs");
			StringAssert.Contains(MethodBody(source,"public void FillImagingGeneral()"),
				"checkImagingAllowPdfJavascript.Checked=PrefC.GetBool(PrefName.ImagingAllowPdfJavascript);");
			StringAssert.Contains(MethodBody(source,"public bool SaveImagingGeneral()"),
				"Changed|=Prefs.UpdateBool(PrefName.ImagingAllowPdfJavascript,checkImagingAllowPdfJavascript.Checked);");
		}

		[TestMethod]
		public void PreferencesWindow_CheckboxMatchesItsHoverHelpAndFitsTheGroup() {
			string designer=Read("OpenPmsDental","User Controls","Preferences","UserControlImagingGeneral.Designer.cs");
			XElement xElementPrefInf=XDocument.Load(TestDb.FindRepoFile(Path.Combine("OpenPmsDental","Resources","PrefInfos.xml"))).Root.Elements("PrefInf")
				.Single(x => (string)x.Element("PrefName")=="ImagingAllowPdfJavascript");
			Assert.AreEqual("checkImagingAllowPdfJavascript",(string)xElementPrefInf.Element("ControlName"),"The hover help is found by control name.");
			Assert.AreEqual("ImagingGeneral",(string)xElementPrefInf.Element("Category"));
			StringAssert.Contains(designer,"this.checkImagingAllowPdfJavascript.Name = \"checkImagingAllowPdfJavascript\";");
			StringAssert.Contains(designer,"this.checkImagingAllowPdfJavascript.Text = \""+(string)xElementPrefInf.Element("ControlText")+"\";");
			StringAssert.Contains(designer,"this.groupBoxFunctionality.Controls.Add(this.checkImagingAllowPdfJavascript);");
			//The checkbox sits under the last one in the Functionality group, the group grew by one row, and the group below moved down with it.
			int yCheck=GetInt(designer,@"this\.checkImagingAllowPdfJavascript\.Location = new System\.Drawing\.Point\(\d+, (\d+)\);");
			int yLast=GetInt(designer,@"this\.checkImagingOrderDescending\.Location = new System\.Drawing\.Point\(\d+, (\d+)\);");
			int heightGroup=GetInt(designer,@"this\.groupBoxFunctionality\.Size = new System\.Drawing\.Size\(\d+, (\d+)\);");
			int yGroup=GetInt(designer,@"this\.groupBoxFunctionality\.Location = new System\.Drawing\.Point\(\d+, (\d+)\);");
			int yGroupBelow=GetInt(designer,@"this\.groupBoxOD2\.Location = new System\.Drawing\.Point\(\d+, (\d+)\);");
			Assert.IsTrue(yCheck>yLast+17,"Below the sort checkbox.");
			Assert.IsTrue(yCheck+17<heightGroup,"Inside the group.");
			Assert.IsTrue(yGroup+heightGroup<yGroupBelow,"The measurement scale group starts below the Functionality group.");
		}

		private static int GetInt(string source,string pattern) {
			Match match=Regex.Match(source,pattern);
			Assert.IsTrue(match.Success,pattern+" not found.");
			return int.Parse(match.Groups[1].Value);
		}
	}
}
