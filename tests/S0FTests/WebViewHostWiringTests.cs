// S0-F (1): the WebView2 hosts close their browsers with their windows. The EOB and amendment window (FormImages) disposes its PDF viewer in
// FormClosed, while the window still exists, and its LoadPdf stops once the window closed; the Imaging module prints with a browser of its own
// that closes with the control, instead of replacing its viewer with one that nothing closed; ODWebView2 starts once. Checked by the source;
// LibWebTests' PdfViewer smoke test runs the viewer the same way. No window is shown.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace S0FTests {
	[TestClass]
	public class WebViewHostWiringTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(S0FDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		private static string MethodBody(string source,string signature) {
			int index=source.IndexOf(signature,StringComparison.Ordinal);
			Assert.IsTrue(index>=0,"Found: "+signature);
			Match match=new Regex(@"\r?\n\t\t\}").Match(source,index);
			Assert.IsTrue(match.Success);
			return source.Substring(index,match.Index-index);
		}

		[TestMethod]
		public void FormImages_DisposesItsViewerWhenItCloses() {
			string source=Read("OpenPmsDental","Forms","FormImages.cs");
			string body=MethodBody(source,"private void FormImages_FormClosed(object sender,FormClosedEventArgs e) {");
			Assert.IsTrue(Regex.IsMatch(body,@"if\(_odWebView2!=null\) \{\s*_odWebView2\.Dispose\(\);\s*_odWebView2=null;\s*\}"),
				"FormClosed disposes the viewer, while the window still exists.");
			string bodyLoad=MethodBody(source,"private async void LoadPdf(");
			Assert.IsTrue(Regex.IsMatch(bodyLoad,@"^private async void LoadPdf\([^\n]*\n\s*if\(_odWebView2==null \|\| _odWebView2\.IsDisposed\) \{\s*return;"),
				"LoadPdf does nothing once the window closed.");
			Assert.IsTrue(bodyLoad.IndexOf("await _odWebView2.Init();")<bodyLoad.IndexOf("if(_odWebView2==null || _odWebView2FilePath!=filePath) {"),
				"After the viewer started, LoadPdf checks that the window is still open and the file still selected.");
			//The callers dispose the window, which closes the viewer too should FormClosed not have run.
			foreach(string file in new[] { "FormClaimEdit.cs","FormClaimPayBatch.cs","FormClaimPayPreAuth.cs","FormEhrAmendmentEdit.cs" }) {
				Assert.IsTrue(Regex.Matches(Read("OpenPmsDental","Forms",file),@"using FormImages formImages=new FormImages\(\);").Count>0,file);
			}
		}

		[TestMethod]
		public void ImagingModulePrint_HasItsOwnBrowser_ClosedWithTheControl() {
			string source=Read("OpenPmsDental","User Controls","ControlImageDisplay.cs");
			string body=MethodBody(source,"public async void PrintPDF(string filePath) {");
			Assert.IsFalse(body.Contains("_odWebView2=new ODWebView2();"),"The viewer is never replaced by a browser that nothing closes.");
			StringAssert.Contains(body,"_odWebView2Print=new ODWebView2();");
			StringAssert.Contains(body,"_odWebView2Print?.Dispose();");
			StringAssert.Contains(source,"_odWebView2Print.CoreWebView2.PrintAsync(printerSettings)");
			string designer=Read("OpenPmsDental","User Controls","ControlImageDisplay.Designer.cs");
			string bodyDispose=MethodBody(designer,"protected override void Dispose(bool disposing) {");
			StringAssert.Contains(bodyDispose,"_odWebView2Print?.Dispose();","The print browser, in no window, closes with the control.");
		}

		[TestMethod]
		public void ODWebView2_StartsOnce() {
			string source=Read("CodeBase","Controls","ODWebView2.cs");
			string body=MethodBody(source,"public Task Init(Action<CoreWebView2ControllerOptions> actionSetControllerOptions) {");
			Assert.IsTrue(Regex.IsMatch(body,@"if\(_taskInit!=null && !_taskInit\.IsFaulted && !_taskInit\.IsCanceled\) \{\s*return _taskInit;"),
				"A start while one runs, or after one succeeded, gets the same task.");
			StringAssert.Contains(body,"_taskInit=InitOnce(actionSetControllerOptions);");
			StringAssert.Contains(source,"private async Task InitOnce(Action<CoreWebView2ControllerOptions> actionSetControllerOptions) {");
		}
	}
}
