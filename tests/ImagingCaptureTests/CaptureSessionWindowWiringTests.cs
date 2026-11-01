// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>The session window's wiring that no unit test can run (the WPF window needs a desktop): checked in its source text, like the
	///vendor-neutrality tests, so that removing it fails a test.</summary>
	[TestClass]
	public class CaptureSessionWindowWiringTests {
		private static string ReadWindowSource() {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				string path=Path.Combine(directoryInfo.FullName,"ImagingCapture","UI","FrmCaptureSession.xaml.cs");
				if(File.Exists(path)) {
					return File.ReadAllText(path);
				}
				directoryInfo=directoryInfo.Parent;
			}
			Assert.Inconclusive("FrmCaptureSession.xaml.cs was not found above "+AppDomain.CurrentDomain.BaseDirectory);
			return null;
		}

		///<summary>The body of the method that starts with signature (to its first line that closes a method at two tabs).</summary>
		private static string GetMethodBody(string source,string signature) {
			int idx=source.IndexOf(signature,StringComparison.Ordinal);
			Assert.IsTrue(idx>=0,"Not found: "+signature);
			Match match=new Regex(@"\n\t\t\}").Match(source,idx);
			Assert.IsTrue(match.Success,"No end for "+signature);
			return source.Substring(idx,match.Index-idx);
		}

		[TestMethod]
		public void TheSessionBeginsWhenTheWindowIsOnTheScreen_NotInLoad() {
			//Design 7.3 step 2: the 2 s in which keys are ignored, and before Start is armed, count from when the banner appears.
			string source=ReadWindowSource();
			StringAssert.DoesNotMatch(GetMethodBody(source,"private void FrmCaptureSession_Load("),new Regex(@"_viewModel\.Begin\("),
				"Load runs before the window is on the screen.");
			StringAssert.Contains(GetMethodBody(source,"private void FrmCaptureSession_Shown("),"BeginSession");
			StringAssert.Contains(GetMethodBody(source,"private void BeginSession("),"_viewModel.Begin();");
		}

		[TestMethod]
		public void AltF4AndTheTaskbarsClose_GoThroughTheEndRule_TheFrameHookIsAttachedAndDropsTheMessage() {
			//Design 7.6: without the hook, Alt+F4 while the source is enabled without safe disable would take the forced-close path unwarned.
			string source=ReadWindowSource();
			StringAssert.Contains(GetMethodBody(source,"private void FrmCaptureSession_Load("),"AttachFrameCloseCommandHook();");
			StringAssert.Contains(GetMethodBody(source,"private void FrmCaptureSession_Shown("),"AttachFrameCloseCommandHook();");
			string attach=GetMethodBody(source,"private void AttachFrameCloseCommandHook(");
			StringAssert.Contains(attach,"new FrameCloseCommandHook(MayOperatorClose,");
			StringAssert.Contains(attach,".AssignHandle(_formFrame.Handle);");
			string wndProc=GetMethodBody(source,"protected override void WndProc(ref System.Windows.Forms.Message m) {");
			StringAssert.Matches(wndProc,new Regex(@"if\(CaptureSessionViewModel\.IsFrameMessageDropped\(m\.Msg,m\.WParam\.ToInt64\(\),_funcMayClose,_actionLog\)\)"
				+@"\s*\{\s*m\.Result=IntPtr\.Zero;\s*return;"));
		}
	}
}
