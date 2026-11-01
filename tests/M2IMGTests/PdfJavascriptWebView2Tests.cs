// Shows a made-up PDF that carries a script in the WebView2 wrapper the Imaging module uses (CodeBase ODWebView2), in a small off-screen window,
// and checks from screen captures whether the script ran. This is the check the 26.1 spec asks for: the setting must reach the browser's PDF
// viewer, block the script when off, let it run when on, and leave the viewer working either way. Skipped (inconclusive) when the WebView2
// Runtime is missing, when no window can be shown, or when M2IMG_SKIP_WEBVIEW2=1. Licensed under the GNU General Public License, version 2 or
// later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CodeBase.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Core;

namespace M2IMGTests {
	[TestClass]
	public class PdfJavascriptWebView2Tests {
		///<summary>Sampled pixels darker than this (R+G+B) count as black.</summary>
		private const int DARK=150;
		///<summary>Sampled black pixels that the bar at the top of the page alone gives at least (several hundred in practice); the viewer's toolbar
		///icons give a few dozen at most.</summary>
		private const int MIN_DARK_PAGE=100;

		///<summary>A one-page PDF (300 by 300 points): a black bar across the top that is ordinary page content, and below it a text field whose
		///appearance is a black square. The document's open action hides the field with JavaScript, so the square disappears only if the script
		///runs, while the bar shows that the page was drawn.</summary>
		public static byte[] BuildPdfWithScript() {
			List<string> listObjects=new List<string>();
			listObjects.Add("<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [6 0 R] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 8 0 R >> >> >>"
				+" /OpenAction 4 0 R >>");
			listObjects.Add("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
			listObjects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Contents 5 0 R /Annots [6 0 R] >>");
			listObjects.Add("<< /Type /Action /S /JavaScript /JS (this.getField\\('t'\\).display=display.hidden;) >>");
			string content="0 g 20 250 260 30 re f\n";
			listObjects.Add("<< /Length "+content.Length+" >>\nstream\n"+content+"endstream");
			listObjects.Add("<< /Type /Annot /Subtype /Widget /FT /Tx /T (t) /Rect [20 20 280 230] /F 4 /P 3 0 R /DA (/Helv 12 Tf 0 g)"
				+" /MK << /BG [0] >> /AP << /N 7 0 R >> >>");
			string appearance="0 g 0 0 260 210 re f\n";
			listObjects.Add("<< /Type /XObject /Subtype /Form /BBox [0 0 260 210] /Resources << >> /Length "+appearance.Length+" >>\nstream\n"
				+appearance+"endstream");
			listObjects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("%PDF-1.4\n");
			List<int> listOffsets=new List<int>();
			for(int i=0;i<listObjects.Count;i++) {
				listOffsets.Add(stringBuilder.Length);
				stringBuilder.Append((i+1)+" 0 obj\n"+listObjects[i]+"\nendobj\n");
			}
			int offsetXref=stringBuilder.Length;
			stringBuilder.Append("xref\n0 "+(listObjects.Count+1)+"\n0000000000 65535 f \n");
			for(int i=0;i<listOffsets.Count;i++) {
				stringBuilder.Append(listOffsets[i].ToString("D10")+" 00000 n \n");
			}
			stringBuilder.Append("trailer\n<< /Size "+(listObjects.Count+1)+" /Root 1 0 R >>\nstartxref\n"+offsetXref+"\n%%EOF\n");
			return Encoding.ASCII.GetBytes(stringBuilder.ToString());
		}

		[TestMethod]
		public void BuildPdfWithScript_HasTheScriptAndAValidCrossReference() {
			string pdf=Encoding.ASCII.GetString(BuildPdfWithScript());
			StringAssert.Contains(pdf,"/S /JavaScript");
			StringAssert.Contains(pdf,"/OpenAction 4 0 R");
			//Every cross-reference offset points at its object.
			int idxXref=pdf.IndexOf("xref\n0 9\n");
			Assert.IsTrue(idxXref>0);
			string[] arrayLines=pdf.Substring(idxXref).Split('\n');
			for(int i=1;i<=8;i++) {
				int offset=int.Parse(arrayLines[2+i].Substring(0,10));
				Assert.IsTrue(pdf.Substring(offset).StartsWith(i+" 0 obj\n"),"Object "+i);
			}
		}

		///<summary>How many sampled pixels of the captured viewer are black.</summary>
		private static int CountDark(Bitmap bitmap) {
			int countDark=0;
			for(int x=0;x<bitmap.Width;x+=4) {
				for(int y=0;y<bitmap.Height;y+=4) {
					Color color=bitmap.GetPixel(x,y);
					if(color.R+color.G+color.B<DARK) {
						countDark++;
					}
				}
			}
			return countDark;
		}

		private static async Task<int> CaptureDark(ODWebView2 odWebView2) {
			using MemoryStream memoryStream=new MemoryStream();
			await odWebView2.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,memoryStream);
			memoryStream.Position=0;
			using Bitmap bitmap=new Bitmap(memoryStream);
			return CountDark(bitmap);
		}

		///<summary>Navigates like ControlImageDisplay.LoadPdf (a plain file path through ODWebView2Navigate), waits until the page is drawn and the
		///picture stops changing, and returns the black count of the last capture (0 if the page was never drawn).</summary>
		private static async Task<int> ShowPdf(ODWebView2 odWebView2,string pathPdf) {
			TaskCompletionSource<bool> taskCompletionSource=new TaskCompletionSource<bool>();
			EventHandler<CoreWebView2NavigationCompletedEventArgs> eventHandler=(sender,e) => taskCompletionSource.TrySetResult(true);
			odWebView2.NavigationCompleted+=eventHandler;
			odWebView2.ODWebView2Navigate(pathPdf);
			await Task.WhenAny(taskCompletionSource.Task,Task.Delay(20000));
			odWebView2.NavigationCompleted-=eventHandler;
			DateTime dateTimeStart=DateTime.Now;
			int countDarkLast=-1;
			int countDark=0;
			while(DateTime.Now-dateTimeStart<TimeSpan.FromSeconds(20)) {
				await Task.Delay(500);
				countDark=await CaptureDark(odWebView2);
				bool isDrawn=countDark>=MIN_DARK_PAGE;
				if(isDrawn && countDark==countDarkLast && DateTime.Now-dateTimeStart>TimeSpan.FromSeconds(2)) {
					break;
				}
				countDarkLast=countDark;
			}
			return countDark;
		}

		[TestMethod]
		public void PdfViewer_RunsTheScriptOnlyWhenAllowed() {
			if(Environment.GetEnvironmentVariable("M2IMG_SKIP_WEBVIEW2")=="1") {
				Assert.Inconclusive("M2IMG_SKIP_WEBVIEW2=1");
			}
			if(!ODWebView2.IsRuntimeAvailable()) {
				Assert.Inconclusive("The Microsoft WebView2 Runtime is not installed.");
			}
			string folder=Path.Combine(Path.GetTempPath(),"m2img_webview2_"+DateTime.Now.ToString("yyMMddHHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8));
			Directory.CreateDirectory(folder);
			List<int> listDark=new List<int>();
			string browserVersion="";
			string error=null;
			try {
				Thread thread=new Thread(() => {
					using Form form=new Form();
					form.StartPosition=FormStartPosition.Manual;
					form.Location=new Point(-6000,-6000);//Off screen; WebView2 still draws, so the capture works.
					form.Size=new Size(600,600);
					form.ShowInTaskbar=false;
					form.FormBorderStyle=FormBorderStyle.None;
					form.Load+=async (sender,e) => {
						try {
							CoreWebView2Environment coreWebView2Environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(folder,"data"));
							browserVersion=coreWebView2Environment.BrowserVersionString;
							//1: one viewer reused for every PDF, as in the Imaging module, with the setting applied before each navigation.
							ODWebView2 odWebView2=new ODWebView2();
							odWebView2.Dock=DockStyle.Fill;
							odWebView2.DoBlockNavigation=true;
							form.Controls.Add(odWebView2);
							await odWebView2.EnsureCoreWebView2Async(coreWebView2Environment);
							bool[] arrayIsAllowed=new bool[] { false,true,false };
							for(int i=0;i<arrayIsAllowed.Length;i++) {
								string pathPdf=Path.Combine(folder,"script"+i+".pdf");
								File.WriteAllBytes(pathPdf,BuildPdfWithScript());
								odWebView2.SetScriptEnabled(arrayIsAllowed[i]);
								listDark.Add(await ShowPdf(odWebView2,pathPdf));
							}
							form.Controls.Remove(odWebView2);
							odWebView2.Dispose();
							//2: the setting given before the viewer is initialized, which then applies it.
							ODWebView2 odWebView2New=new ODWebView2();
							odWebView2New.Dock=DockStyle.Fill;
							odWebView2New.SetScriptEnabled(false);
							form.Controls.Add(odWebView2New);
							await odWebView2New.EnsureCoreWebView2Async(coreWebView2Environment);
							string pathPdfNew=Path.Combine(folder,"script_new.pdf");
							File.WriteAllBytes(pathPdfNew,BuildPdfWithScript());
							listDark.Add(await ShowPdf(odWebView2New,pathPdfNew));
						}
						catch(Exception ex) {
							error=ex.GetType().Name+": "+ex.Message;
						}
						form.Close();
					};
					Application.Run(form);
				});
				thread.SetApartmentState(ApartmentState.STA);
				thread.IsBackground=true;
				thread.Start();
				if(!thread.Join(TimeSpan.FromMinutes(3))) {
					Assert.Inconclusive("The WebView2 window did not finish within 3 minutes.");
				}
			}
			finally {
				DeleteFolder(folder);
			}
			if(error!=null) {
				Assert.Inconclusive("WebView2 could not be used here: "+error);
			}
			string results="WebView2 Runtime "+browserVersion+", black samples per PDF: "+string.Join(", ",listDark);
			Console.WriteLine(results);
			Assert.AreEqual(4,listDark.Count,results);
			if(listDark.TrueForAll(x => x<MIN_DARK_PAGE)) {
				Assert.Inconclusive("The PDF viewer drew nothing in this session (no desktop?). "+results);
			}
			int countDarkBlocked=listDark[0];
			int countDarkAllowed=listDark[1];
			Assert.IsTrue(countDarkBlocked>=MIN_DARK_PAGE,"Scripts off: the viewer still shows the PDF. "+results);
			Assert.IsTrue(countDarkAllowed>=MIN_DARK_PAGE,"Scripts on: the viewer shows the PDF. "+results);
			//The field is seven times the bar's size, so with the field hidden far fewer pixels are black.
			Assert.IsTrue(countDarkBlocked>3*countDarkAllowed,"Scripts on: the script hid the black field; scripts off: it stayed. "+results);
			Assert.IsTrue(listDark[2]>3*countDarkAllowed,"Turning scripts off again takes effect on the next PDF. "+results);
			Assert.IsTrue(listDark[3]>3*countDarkAllowed,"Off when set before the viewer is initialized. "+results);
		}

		///<summary>The browser processes let go of the data folder shortly after the window closes.</summary>
		private static void DeleteFolder(string folder) {
			for(int i=0;i<20;i++) {
				try {
					if(Directory.Exists(folder)) {
						Directory.Delete(folder,true);
					}
					return;
				}
				catch(Exception) {
					Thread.Sleep(500);
				}
			}
		}
	}
}
