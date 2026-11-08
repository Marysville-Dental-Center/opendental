// S0-F (1): the PDF viewer of the EOB and amendment window (FormImages), as the window uses it: ODWebView2 in a window of its own, started
// twice at once (a second PDF shown while the viewer still starts), showing a PDF file, and disposed in the window's FormClosed. Checks that the
// two starts are one (the same task, one environment, no error: a second environment made WebView2 throw an ArgumentException that reached the
// user as "the runtime could not start"), that a start after success does nothing, and that once the window closed the viewer's browser process
// ends: nothing of the viewer is left running for the program's exit. The test's own profiles, under its temp folder; no network. Inconclusive
// only when the WebView2 Runtime is missing or does not start here, as the other smoke tests. What it cannot show is the program's own exit,
// which needs the program: that stays a launch check.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeBase.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Core;

namespace LibWebTests {
	public partial class WebView2SmokeTests {
		///<summary>What the PDF viewer test saw.</summary>
		private class PdfViewerResults {
			public string BrowserVersion="";
			public int CountEnvironments;
			public bool IsSameTask;
			public string ErrorInit;
			public bool IsInitialized;
			public bool IsInitAfterSuccessDone;
			public int CountEnvironmentsAfterSuccess;
			public bool IsPdfShown;
			public int BrowserProcessId;
			public bool IsDisposedInFormClosed;
			public bool IsBrowserProcessGone;
			public int SecondsUntilGone=-1;
		}

		///<summary>A one-page PDF with a correct cross-reference table.</summary>
		private static void WritePdf(string path) {
			string[] arrayObjects={
				"<< /Type /Catalog /Pages 2 0 R >>",
				"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
				"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
				"<< /Length 39 >>\nstream\nBT /F1 18 Tf 20 100 Td (S0-F EOB) Tj ET\nendstream",
				"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
			};
			StringBuilder stringBuilder=new StringBuilder("%PDF-1.4\n");
			int[] arrayOffsets=new int[arrayObjects.Length];
			for(int i=0;i<arrayObjects.Length;i++) {
				arrayOffsets[i]=stringBuilder.Length;
				stringBuilder.Append((i+1)+" 0 obj\n"+arrayObjects[i]+"\nendobj\n");
			}
			int offsetXref=stringBuilder.Length;
			stringBuilder.Append("xref\n0 "+(arrayObjects.Length+1)+"\n0000000000 65535 f \n");
			for(int i=0;i<arrayOffsets.Length;i++) {
				stringBuilder.Append(arrayOffsets[i].ToString("0000000000")+" 00000 n \n");
			}
			stringBuilder.Append("trailer\n<< /Size "+(arrayObjects.Length+1)+" /Root 1 0 R >>\nstartxref\n"+offsetXref+"\n%%EOF\n");
			File.WriteAllText(path,stringBuilder.ToString(),Encoding.ASCII);
		}

		private static bool IsProcessGone(int processId) {
			try {
				using Process process=Process.GetProcessById(processId);
				return process.HasExited;
			}
			catch(ArgumentException) {
				return true;//No such process any more.
			}
			catch(InvalidOperationException) {
				return true;
			}
		}

		private static async Task RunPdfViewer(Form form,OffScreenRun offScreenRun,string folder,PdfViewerResults results) {
			CoreWebView2Environment coreWebView2EnvironmentRuntime=await CreateEnvironmentAsync(Path.Combine(folder,"runtime"));
			results.BrowserVersion=coreWebView2EnvironmentRuntime.BrowserVersionString;
			await StartRuntime(form,coreWebView2EnvironmentRuntime,offScreenRun);
			string pathPdf=Path.Combine(folder,"eob.pdf");
			WritePdf(pathPdf);
			//The viewer's own profile, so its browser process is its own and ends when the viewer is gone.
			Func<Task<CoreWebView2Environment>> funcCreateEnvironmentOld=ODWebView2.FuncCreateEnvironment;
			ODWebView2.FuncCreateEnvironment=() => {
				results.CountEnvironments++;
				return CreateEnvironmentAsync(Path.Combine(folder,"viewer"));
			};
			try {
				//The EOB window, off screen like the test's own window.
				Form formViewer=new Form();
				formViewer.StartPosition=FormStartPosition.Manual;
				formViewer.Location=new Point(-6000,-6000);
				formViewer.Size=new Size(500,400);
				formViewer.ShowInTaskbar=false;
				formViewer.FormBorderStyle=FormBorderStyle.None;
				ODWebView2 odWebView2=new ODWebView2();
				odWebView2.DoBlockNavigation=true;
				odWebView2.Dock=DockStyle.Fill;
				formViewer.Controls.Add(odWebView2);
				//As FormImages_FormClosed does.
				formViewer.FormClosed+=(sender,e) => {
					odWebView2.Dispose();
					results.IsDisposedInFormClosed=true;
				};
				formViewer.Show(form);
				//Two PDFs while the viewer starts (LoadPdf twice): one start.
				Task taskFirst=odWebView2.Init();
				Task taskSecond=odWebView2.Init();
				results.IsSameTask=ReferenceEquals(taskFirst,taskSecond);
				try {
					await taskFirst;
					await taskSecond;
				}
				catch(Exception ex) {
					results.ErrorInit=ex.ToString();
				}
				results.IsInitialized=odWebView2.CoreWebView2!=null;
				if(!results.IsInitialized) {
					formViewer.Close();
					return;
				}
				Task taskAfter=odWebView2.Init();
				results.IsInitAfterSuccessDone=taskAfter.IsCompleted && !taskAfter.IsFaulted;
				await taskAfter;
				results.CountEnvironmentsAfterSuccess=results.CountEnvironments;
				results.BrowserProcessId=(int)odWebView2.CoreWebView2.BrowserProcessId;
				bool isNavigated=false;
				odWebView2.NavigationCompleted+=(sender,e) => {
					isNavigated=true;
					results.IsPdfShown=e.IsSuccess;
				};
				odWebView2.SetScriptEnabled(false);
				odWebView2.ODWebView2Navigate(pathPdf);
				await WaitFor(() => isNavigated,20000);
				await Task.Delay(1000);//the PDF viewer draws
				formViewer.Close();//a window shown with Show is disposed by Close
				DateTime dateTimeClosed=DateTime.Now;
				results.IsBrowserProcessGone=await WaitFor(() => IsProcessGone(results.BrowserProcessId),30000);
				if(results.IsBrowserProcessGone) {
					results.SecondsUntilGone=(int)(DateTime.Now-dateTimeClosed).TotalSeconds;
				}
			}
			finally {
				ODWebView2.FuncCreateEnvironment=funcCreateEnvironmentOld;
			}
		}

		[TestMethod]
		public void PdfViewer_StartsOnce_AndLeavesNoBrowserProcessOnceItsWindowCloses() {
			SkipWithoutRuntime();
			string folder=TestPaths.NewTempFolder("pdfviewer");
			PdfViewerResults results=new PdfViewerResults();
			OffScreenRun offScreenRun;
			try {
				offScreenRun=RunOffScreen((form,run) => RunPdfViewer(form,run,folder,results));
			}
			finally {
				TestPaths.DeleteFolder(folder);
			}
			offScreenRun.AssertNoError();
			string details="WebView2 Runtime "+results.BrowserVersion+"; browser process "+results.BrowserProcessId+"; gone after "+results.SecondsUntilGone+" s";
			Console.WriteLine(details);
			Assert.IsTrue(results.IsSameTask,"A start while the viewer starts gets the same task. "+details);
			Assert.IsNull(results.ErrorInit,"Two starts at once: no error. "+details);
			Assert.IsTrue(results.IsInitialized,details);
			Assert.AreEqual(1,results.CountEnvironmentsAfterSuccess,"One environment for both starts and the one after. "+details);
			Assert.IsTrue(results.IsInitAfterSuccessDone,"A start after success does nothing. "+details);
			Assert.IsTrue(results.IsPdfShown,"The PDF showed. "+details);
			Assert.IsTrue(results.IsDisposedInFormClosed);
			Assert.IsTrue(results.BrowserProcessId>0,details);
			Assert.IsTrue(results.IsBrowserProcessGone,"Once the window closed, the viewer's browser process ends: nothing is left for the exit. "+details);
		}
	}
}
