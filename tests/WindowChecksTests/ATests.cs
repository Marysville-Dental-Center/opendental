// Group A of the checklist: start-up, About and build stamp. The starts are out of process (the real Debug and Release exes with their command
// line); About is reached in process through the real menu item; A3, A8 and A9 read the files.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowChecksTests {
	[TestClass]
	public class ATests {
		public static string DebugExe {
			get {
				return Path.Combine(WinChkEnv.DebugFolder,"OpenPmsDental.exe");
			}
		}

		///<summary>Starts the program out of process for patient #1 and waits for its main window. A "database will now be updated" prompt is
		///cancelled and fails the check; any other message box is a failure too, with its text. Zero when the window never came.</summary>
		public static IntPtr StartProgram(string exePath,string module,CheckResult checkResult,out ProgramProcess programProcess,
			Func<IntPtr,string,bool> dialogAction=null)
		{
			ProgramProcess programProcessStarted=ProgramProcess.Start(exePath,ProgramProcess.ProgramArgs(module));
			programProcess=programProcessStarted;
			IntPtr hwndMain=programProcessStarted.WaitForWindow((hwnd,className,title) => className!="#32770" && title.Contains("Gatetest"),
				TimeSpan.FromSeconds(180),(hwnd,text) => {
					if(dialogAction!=null && dialogAction(hwnd,text)) {
						return true;
					}
					checkResult.Fail("Unexpected message box at the start: "+Native.GetText(hwnd)+": "+text);
					if(text.Contains("database will now be updated")) {
						Native.CancelDialog(hwnd);
					}
					return false;
				});
			checkResult.Note(Path.GetFileName(Path.GetDirectoryName(exePath))+" start to the main window: "
				+programProcessStarted.StopwatchStart.Elapsed.TotalSeconds.ToString("0.0")+" s");
			if(hwndMain==IntPtr.Zero) {
				checkResult.Fail("The main window with patient #1 did not show within 180 s"+(programProcessStarted.HasExited ? " (the process ended)" : "")
					+"; windows: "+string.Join(", ",programProcessStarted.GetWindows().Select(x => Native.GetClass(x)+" '"+Native.GetText(x)+"'")));
				return IntPtr.Zero;
			}
			IntPtr hwndChoose=programProcessStarted.FindWindow("Choose Database");
			checkResult.Expect(hwndChoose==IntPtr.Zero,"No Choose Database window");
			System.Threading.Thread.Sleep(2500);//the module's first paint
			return hwndMain;
		}

		///<summary>The module bar of the main window: its widest-left child that is tall and about 50 pixels wide.</summary>
		public static IntPtr FindModuleBar(IntPtr hwndMain) {
			Rectangle rectangleMain=Native.GetRect(hwndMain);
			return Native.GetChildren(hwndMain).Where(x => Native.IsWindowVisible(x)).Select(x => new { Hwnd=x,Rect=Native.GetRect(x) })
				.Where(x => x.Rect.Width>=40 && x.Rect.Width<=80 && x.Rect.Height>300 && x.Rect.Left-rectangleMain.Left<40)
				.OrderBy(x => x.Rect.Left).Select(x => x.Hwnd).FirstOrDefault();
		}

		///<summary>Clicks a module button by window messages: index 0 Appts, 1 Family, 2 Account, 3 Tx Plan, 4 Chart, 5 Imaging, 6 Manage.</summary>
		public static bool ClickModule(IntPtr hwndMain,int index) {
			IntPtr hwndModuleBar=FindModuleBar(hwndMain);
			if(hwndModuleBar==IntPtr.Zero) {
				return false;
			}
			Native.PostClick(hwndModuleBar,25,29+58*index);
			System.Threading.Thread.Sleep(3000);
			return true;
		}

		[TestMethod]
		public void A1_DebugStart() {
			WinChkFixture.Begin("A");
			CheckResult checkResult=new CheckResult("A1","Debug start");
			IntPtr hwndMain=StartProgram(DebugExe,"",checkResult,out ProgramProcess programProcess);
			using(programProcess) {
				if(hwndMain!=IntPtr.Zero) {
					checkResult.Expect(!programProcess.ListDialogsSeen.Any(x => x.Contains("database will now be updated")),
						"No 'The database will now be updated' prompt");
					checkResult.Expect(Native.GetWindowIcon(hwndMain)!=IntPtr.Zero,"The main window has its icon");
					ShotInfo shotInfo=Shot.Window(hwndMain,"A1-main.png");
					checkResult.AddShot(shotInfo,"The main window");
					if(shotInfo.IsDrawn) {
						int colorsModuleBar=Shot.CountColorsIn("A1-main.png",new Rectangle(10,60,46,400));
						int colorsToolbar=Shot.CountColorsIn("A1-main.png",new Rectangle(60,60,640,45));
						checkResult.Expect(colorsModuleBar>40,"The module bar's icons are drawn ("+colorsModuleBar+" colours in its area)");
						checkResult.Expect(colorsToolbar>40,"The toolbar's icons are drawn ("+colorsToolbar+" colours in its area)");
					}
					checkResult.Note("Title: "+Native.GetText(hwndMain));
					Exits.CloseProgram(programProcess,hwndMain,"A1",checkResult);
				}
			}
			//The splash: a start with a command line never shows it, so it is shown in process as the program shows it.
			using(InProcHost inProcHost=InProcHost.Start("")) {
				CheckResult checkResultSplash=inProcHost.Run("A1SPLASH","Splash");
				Merge(checkResult,checkResultSplash);
			}
			checkResult.ForPerson("compare A1-main.png and A1-splash.png with stage 0's pictures (looks only)");
			WinChkFixture.Finish(checkResult);
		}

		public static void Merge(CheckResult checkResult,CheckResult checkResultOther) {
			checkResult.ListVerified.AddRange(checkResultOther.ListVerified);
			checkResult.ListFailures.AddRange(checkResultOther.ListFailures);
			checkResult.ListShots.AddRange(checkResultOther.ListShots);
			checkResult.ListNotes.AddRange(checkResultOther.ListNotes);
			checkResult.ListForPerson.AddRange(checkResultOther.ListForPerson);
		}

		[TestMethod]
		public void A2_HelpAbout() {
			WinChkFixture.Begin("A");
			using InProcHost inProcHost=InProcHost.Start("chart");
			WinChkFixture.Finish(inProcHost.Run("A2","Help > About"));
		}

		[TestMethod]
		public void A3_BuildStampOfTheDebugExe() {
			WinChkFixture.Begin("A");
			CheckResult checkResult=new CheckResult("A3","Build stamp of the Debug exe");
			string[] arrayMetadata=AssemblyProbe.Run(WinChkEnv.DebugFolder,x => x.GetMetadata("OpenPmsDental.exe"));
			File.WriteAllLines(Path.Combine(WinChkEnv.OutFolder,"A3-build-stamp.txt"),arrayMetadata);
			string stamp=arrayMetadata.FirstOrDefault(x => x.StartsWith("\"ForkBuildUtc\""))??"";
			checkResult.Note("AssemblyMetadata: "+string.Join("; ",arrayMetadata));
			Match match=Regex.Match(stamp,"^\"ForkBuildUtc\" = \"(\\d{4}-\\d\\d-\\d\\dT\\d\\d:\\d\\d:\\d\\dZ)\"$");
			if(checkResult.Expect(match.Success,"\"ForkBuildUtc\" = \"<yyyy-MM-ddTHH:mm:ssZ>\": "+stamp,stamp)) {
				DateTime dateBuild=DateTime.ParseExact(match.Groups[1].Value,"yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture,
					DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal);
				DateTime dateWrite=File.GetLastWriteTimeUtc(DebugExe);
				double minutes=(dateWrite-dateBuild).TotalMinutes;
				checkResult.Expect(minutes>=0 && minutes<=5,"The stamp is "+minutes.ToString("0.0")+" minutes before the exe's LastWriteTimeUtc ("
					+dateWrite.ToString("s")+"Z)");
			}
			checkResult.Note("Read with a reflection-only load in a throwaway AppDomain (no PowerShell window); text in A3-build-stamp.txt instead of a picture");
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void A4_Exit() {
			WinChkFixture.Begin("A");
			CheckResult checkResult=new CheckResult("A4","Exit");
			string folderExitLog=Path.Combine(WinChkEnv.DebugFolder,"Logger","Exit");
			string folderExitLogDocuments=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Logger","Exit");
			int countBefore=CountFiles(folderExitLog);
			int countBeforeDocuments=CountFiles(folderExitLogDocuments);
			IntPtr hwndMain=StartProgram(DebugExe,"chart",checkResult,out ProgramProcess programProcess);
			using(programProcess) {
				if(hwndMain!=IntPtr.Zero) {
					Exits.CloseProgram(programProcess,hwndMain,"A4",checkResult);
				}
			}
			checkResult.Expect(CountFiles(folderExitLog)==countBefore,"No new file in <dbg>\\Logger\\Exit ("+countBefore+" before)");
			checkResult.Expect(CountFiles(folderExitLogDocuments)==countBeforeDocuments,"No new file in Documents\\Logger\\Exit");
			checkResult.Note("Every out-of-process run of the program in this run is closed by WM_CLOSE and timed; all exits are listed in exits.txt "
				+"and the report");
			WinChkFixture.Finish(checkResult);
		}

		private static int CountFiles(string folder) {
			return Directory.Exists(folder) ? Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Length : 0;
		}

		[TestMethod]
		public void A5_StartWithNoDpi() {
			WinChkFixture.Begin("A");
			CheckResult checkResult=new CheckResult("A5","Start with NoDpi.txt");
			StartWithFlagFile("NoDpi.txt",checkResult,(programProcess,hwndMain) => {
				checkResult.AddShot(Shot.Window(hwndMain,"A5-nodpi-chart.png"),"The Chart with NoDpi.txt");
				checkResult.Expect(ClickModule(hwndMain,0),"The Appts module button is clicked (window messages)");
				checkResult.Expect(!programProcess.HasExited && programProcess.GetWindows().All(x => !Native.IsDialogClass(x)),"Appointments opens without a message");
				checkResult.AddShot(Shot.Window(hwndMain,"A5-nodpi-appts.png"),"Appointments with NoDpi.txt");
			});
			checkResult.ForPerson("at 100% the pictures should look as A1 (the flag only changes scaling above 100%)");
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void A6_StartWithNoCustomBorders() {
			WinChkFixture.Begin("A");
			CheckResult checkResult=new CheckResult("A6","Start with NoCustomBorders.txt");
			StartWithFlagFile("NoCustomBorders.txt",checkResult,(programProcess,hwndMain) => {
				int style=Native.GetWindowLong(hwndMain,Native.GWL_STYLE);
				checkResult.Expect((style&Native.WS_CAPTION)==Native.WS_CAPTION,"The main window has the standard Windows title bar (WS_CAPTION)",
					"style 0x"+style.ToString("X8"));
				checkResult.AddShot(Shot.Window(hwndMain,"A6-nocustomborders.png"),"The Chart with standard borders");
				checkResult.Expect(ClickModule(hwndMain,0),"The Appts module button is clicked (window messages)");
				checkResult.Expect(!programProcess.HasExited && programProcess.GetWindows().All(x => !Native.IsDialogClass(x)),"Appointments opens without a message");
			},() => {
				//One dialog: Help > About, in process (the program's menus open no drop-down by UI Automation while the screen is locked), with the
				//flag file still beside the exe so the in-process start sets the same borders flag ProgramEntry sets.
				using InProcHost inProcHost=InProcHost.Start("chart");
				Merge(checkResult,inProcHost.Run("A6ABOUT","Help > About with NoCustomBorders.txt"));
			});
			WinChkFixture.Finish(checkResult);
		}

		///<summary>Creates the empty flag file beside the Debug exe, starts the program, runs the action, exits, and deletes the file again.</summary>
		private static void StartWithFlagFile(string fileName,CheckResult checkResult,Action<ProgramProcess,IntPtr> action,Action actionInProcess=null) {
			string path=Path.Combine(WinChkEnv.DebugFolder,fileName);
			bool existed=File.Exists(path);
			Assert.IsFalse(existed,fileName+" is already there; it is never left behind by these checks.");
			File.WriteAllText(path,"");
			try {
				IntPtr hwndMain=StartProgram(DebugExe,"chart",checkResult,out ProgramProcess programProcess);
				using(programProcess) {
					if(hwndMain!=IntPtr.Zero) {
						action(programProcess,hwndMain);
						Exits.CloseProgram(programProcess,hwndMain,checkResult.Id,checkResult);
					}
				}
				actionInProcess?.Invoke();
			}
			finally {
				File.Delete(path);
			}
			checkResult.Expect(!File.Exists(path),fileName+" deleted again");
		}

		[TestMethod]
		public void A7_ReleaseStart() {
			WinChkFixture.Begin("A");
			CheckResult checkResult=new CheckResult("A7","Release start");
			string folderRelease=WinChkEnv.ReleaseFolder;
			string exeRelease=Path.Combine(folderRelease,"OpenPmsDental.exe");
			if(!File.Exists(exeRelease)) {
				checkResult.Fail("No Release build at "+exeRelease);
				WinChkFixture.Finish(checkResult);
				return;
			}
			List<string> listBefore=Directory.GetFiles(folderRelease,"*",SearchOption.AllDirectories).Select(x => x.Substring(folderRelease.Length+1)).OrderBy(x => x).ToList();
			File.WriteAllLines(Path.Combine(WinChkEnv.OutFolder,"release-files-before.txt"),listBefore);
			DateTime dateWriteUtc=File.GetLastWriteTimeUtc(exeRelease);
			string promptText="";
			IntPtr hwndMain=StartProgram(exeRelease,"chart",checkResult,out ProgramProcess programProcess,(hwnd,text) => {
				//The publish offer: "No release is published yet." on a database without a release, "This build is newer than the published one" on
				//one where an older build is published. Either way Cancel runs it on this workstation only.
				if(!text.Contains("Publish this build")) {
					return false;
				}
				promptText=Native.GetText(hwnd)+": "+text;
				checkResult.AddShot(Shot.Window(hwnd,"A7-release-prompt.png"),"The release prompt");
				checkResult.Expect(Native.GetDialogButtons(hwnd).Contains("Cancel"),"The prompt has Cancel ("+string.Join("/",Native.GetDialogButtons(hwnd))+")");
				checkResult.Expect(Native.ClickDialogButton(hwnd,"Cancel"),"Cancel pressed (BM_CLICK); OK, which publishes, is never pressed");
				return true;
			});
			using(programProcess) {
				checkResult.Note("Prompt: "+promptText.Replace("\r"," ").Replace("\n"," "));
				if(checkResult.Expect(promptText!="","The publish prompt showed")) {
					checkResult.Expect(promptText.Contains("Publish this build") && promptText.Contains("Click Cancel to run it on this workstation only."),
						"It names 'Publish this build' and 'Click Cancel to run it on this workstation only.'");
					if(!promptText.Contains("No release is published yet.")) {
						string published=ScratchDb.Rows("SELECT DocMiscNum,DateCreated,FileName,DocMiscType,LENGTH(RawBase64) FROM documentmisc");
						checkResult.ForPerson("the prompt is not the checklist's 'No release is published yet.': the copy of the test database already holds a "
							+"published release (documentmisc rows: "+(published.Length>400 ? published.Substring(0,400)+"..." : published)+"), an older build of "
							+"this fork; the lead decides whether opendental_dev should keep it");
					}
					Match match=Regex.Match(promptText,"Publish this build \\(release [^(]*\\(build ([0-9a-f]{12}), built (\\d{4}-\\d\\d-\\d\\d \\d\\d:\\d\\d) UTC");
					if(checkResult.Expect(match.Success,"It names the build (12 hex) and the built time (yyyy-MM-dd HH:mm UTC)")) {
						DateTime dateBuilt=DateTime.ParseExact(match.Groups[2].Value,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture);
						double minutes=Math.Abs((dateWriteUtc-dateBuilt).TotalMinutes);
						checkResult.Expect(minutes<=3,"The built time "+match.Groups[2].Value+" matches the exe's LastWriteTimeUtc "+dateWriteUtc.ToString("yyyy-MM-dd HH:mm:ss")
							+" ("+minutes.ToString("0.0")+" min)");
					}
				}
				if(hwndMain!=IntPtr.Zero) {
					checkResult.AddShot(Shot.Window(hwndMain,"A7-release-main.png"),"The Release build's main window (Chart)");
					checkResult.Expect(ClickModule(hwndMain,5),"The Imaging module button is clicked (window messages)");
					checkResult.Expect(!programProcess.HasExited && programProcess.GetWindows().All(x => !Native.IsDialogClass(x)),"Imaging opens without a message");
					checkResult.AddShot(Shot.Window(hwndMain,"A7-release-images.png"),"The Release build's Imaging module");
					Exits.CloseProgram(programProcess,hwndMain,"A7",checkResult);
				}
			}
			//The File menu and Help > About of the Release build, in process (an AppDomain on the Release folder); its publish prompt is cancelled.
			using(InProcHost inProcHost=InProcHost.Start("chart",folderRelease,new[] { "Publish this build" })) {
				checkResult.Note("In-process Release start: prompt cancelled: "+(inProcHost.StartPrompts=="" ? "none" : "yes"));
				Merge(checkResult,inProcHost.Run("A7RELEASE","Release: File menu, Chart, Imaging, About"));
			}
			List<string> listAfter=Directory.GetFiles(folderRelease,"*",SearchOption.AllDirectories).Select(x => x.Substring(folderRelease.Length+1)).OrderBy(x => x).ToList();
			List<string> listNew=listAfter.Except(listBefore).ToList();
			checkResult.Note("Files the Release run added: "+(listNew.Count==0 ? "none" : string.Join(", ",listNew))+" (removed at Z)");
			checkResult.Note("The connection comes from the command line, so no FreeDentalConfig.xml is copied into the Release folder");
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void A8_ReleaseXmlSerializersPairing() {
			WinChkFixture.Begin("A");
			CheckResult checkResult=new CheckResult("A8","Release XmlSerializers pairing");
			string folderRelease=WinChkEnv.ReleaseFolder;
			List<string> listLines=new List<string>();
			foreach(string[] arrayPair in new[] { new[] { "OpenPmsDental.exe","OpenPmsDental.XmlSerializers.dll" },
				new[] { "OpenPmsDentalBusiness.dll","OpenPmsDentalBusiness.XmlSerializers.dll" } })
			{
				string mvid=AssemblyProbe.Run(folderRelease,x => x.GetMvid(arrayPair[0]));
				string serializerVersion=AssemblyProbe.Run(folderRelease,x => x.GetSerializerVersion(arrayPair[1]));
				listLines.Add(arrayPair[0]+" MVID "+mvid);
				listLines.Add(arrayPair[1]+" "+serializerVersion);
				checkResult.Expect(serializerVersion.Contains("ParentAssemblyId = "+mvid+","),arrayPair[1]+"'s ParentAssemblyId is "+arrayPair[0]+"'s MVID "+mvid
					+" followed by a comma",serializerVersion);
			}
			File.WriteAllLines(Path.Combine(WinChkEnv.OutFolder,"A8-xmlserializers.txt"),listLines);
			checkResult.Note(string.Join("; ",listLines));
			WinChkFixture.Finish(checkResult);
		}

		[TestMethod]
		public void A9_ElevationManifestsAndTheCopier() {
			WinChkFixture.Begin("A");
			CheckResult checkResult=new CheckResult("A9","Elevation manifests and the copier beside the program");
			foreach(string[] arrayFolder in new[] { new[] { WinChkEnv.DebugFolder,"Debug","A9-debug-folder.png" },new[] { WinChkEnv.ReleaseFolder,"Release","A9-release-folder.png" } }) {
				string folder=arrayFolder[0];
				foreach(string exe in new[] { "UpdateFileCopier.exe","OpenPmsDentalDbSettings.exe","ServiceManager.exe","OpenPmsDental.exe" }) {
					//The manifests keep Visual Studio's commented-out examples of every level: only the element outside comments counts.
					string manifest=Regex.Replace(Native.ReadManifest(Path.Combine(folder,exe)),"<!--.*?-->","",RegexOptions.Singleline);
					Match match=Regex.Match(manifest,"requestedExecutionLevel[^>]*level=\"(\\w+)\"");
					string level=match.Success ? match.Groups[1].Value : "(none)";
					bool isShield=level=="requireAdministrator" || level=="highestAvailable";
					bool wantShield=exe!="OpenPmsDental.exe";
					checkResult.Expect(isShield==wantShield,arrayFolder[1]+" "+exe+": manifest level "+level+(wantShield ? " (the shell draws the UAC shield)" : " (no shield)"));
				}
				foreach(string exe in new[] { "ServiceManager.exe","OpenPmsDentalDbSettings.exe" }) {
					int countIcons=Native.ExtractIconEx(Path.Combine(folder,exe),-1,null,null,0);
					checkResult.Expect(countIcons>0,arrayFolder[1]+" "+exe+" has its own icon ("+countIcons+")");
				}
				string copierOwn=Path.Combine(WinChkEnv.RepoRoot,"UpdateFileCopier","bin",arrayFolder[1],"UpdateFileCopier.exe");
				FileInfo fileInfoBeside=new FileInfo(Path.Combine(folder,"UpdateFileCopier.exe"));
				FileInfo fileInfoOwn=new FileInfo(copierOwn);
				checkResult.Expect(fileInfoBeside.Exists && fileInfoOwn.Exists && fileInfoBeside.Length==fileInfoOwn.Length
					&& fileInfoBeside.LastWriteTimeUtc==fileInfoOwn.LastWriteTimeUtc,arrayFolder[1]+" UpdateFileCopier.exe has the size and date of its own "
					+"bin\\"+arrayFolder[1]+" output ("+(fileInfoBeside.Exists ? fileInfoBeside.Length+" bytes, "+fileInfoBeside.LastWriteTime.ToString("s") : "missing")+")");
				checkResult.AddShot(DrawFolder(folder,arrayFolder[2]),"The shell's icons of "+arrayFolder[1]+"'s exes");
			}
			checkResult.Note("No Explorer window: the manifests are read from the files, and the shell's own icons (SHGetFileInfo, with overlays) are drawn "
				+"into A9-*-folder.png");
			checkResult.ForPerson("look at the shields in A9-debug-folder.png and A9-release-folder.png");
			WinChkFixture.Finish(checkResult);
		}

		///<summary>Draws each exe's shell icon (as Explorer gets it, UAC shield included) with its name, size and date.</summary>
		private static ShotInfo DrawFolder(string folder,string fileName) {
			string[] arrayExes=Directory.GetFiles(folder,"*.exe").OrderBy(x => x).ToArray();
			using Bitmap bitmap=new Bitmap(760,Math.Max(60,arrayExes.Length*40+20));
			using(Graphics g=Graphics.FromImage(bitmap)) {
				g.Clear(Color.White);
				using Font font=new Font("Segoe UI",9f);
				for(int i=0;i<arrayExes.Length;i++) {
					Native.SHFILEINFO shFileInfo=new Native.SHFILEINFO();
					Native.SHGetFileInfo(arrayExes[i],0,ref shFileInfo,(uint)System.Runtime.InteropServices.Marshal.SizeOf(shFileInfo),
						Native.SHGFI_ICON|Native.SHGFI_LARGEICON|Native.SHGFI_ADDOVERLAYS);
					if(shFileInfo.hIcon!=IntPtr.Zero) {
						using(Icon icon=Icon.FromHandle(shFileInfo.hIcon)) {
							g.DrawIcon(icon,10,10+i*40);
						}
						Native.DestroyIcon(shFileInfo.hIcon);
					}
					FileInfo fileInfo=new FileInfo(arrayExes[i]);
					g.DrawString(fileInfo.Name+"   "+fileInfo.Length.ToString("N0")+" bytes   "+fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),font,Brushes.Black,52,18+i*40);
				}
			}
			return Shot.Bitmap(bitmap,fileName);
		}
	}
}
