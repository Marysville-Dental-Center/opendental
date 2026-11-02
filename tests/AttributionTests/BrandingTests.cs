// Tests for the Open PMS - Dental branding (branding/README.md): the window icons, the program icons, the splash, the About box logo, the
// installer images and README.md. Nothing is shown on screen; the windows are built in memory on an STA thread.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;

namespace AttributionTests {
	[TestClass]
	public class BrandingTests {
		///<summary>SHA-256 of Open Dental's three window icons: OpenDentalMain.ico (and WpfControlsOD's Icon.ico), Misc/App.ico, and the icon
		///FormSearchPatNotes had. No file of the repository may hold them.</summary>
		private static readonly string[] _arrayOldIconHashes=new string[] {
			"d60200a0cfe9fed41b7a2aab96eb7a608341343622ec3e4b54fa5f1982d60633",
			"74fd7ee3f3ec1b727ec9cb97b3f00ec651c98f2ffaa7e01a4b7078dc6deac199",
			"6db31ccce04998eaf5517532f12c18bd91401d309ed5b4d1150ee82d8e676235",
		};
		///<summary>The report designer (RdlDesign, fyiReporting's, not shipped) keeps its own icon on these two windows and as its program icon.</summary>
		private static readonly string[] _arrayOwnIconFiles=new string[] {
			@"RdlDesign\MDIChild.resx",
			@"RdlDesign\RdlDesigner.resx",
			@"RdlDesign\ReportDesigner.csproj",
		};
		///<summary>Folders that hold build output, packages or other checkouts (.claude holds worktrees), never this repository's sources.</summary>
		private static readonly string[] _arraySkippedFolders=new string[] { ".git",".claude",".vs","bin","obj","packages","node_modules","TestResults" };
		private static string _repoRoot;
		private const int WM_GETICON=0x007F;
		private const int ICON_SMALL=0;
		private const int ICON_BIG=1;

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hWnd,int msg,IntPtr wParam,IntPtr lParam);

		#region Helpers
		///<summary>The repository root: the first folder above the test assembly that holds LICENSE and NOTICE.</summary>
		private static string RepoRoot() {
			if(_repoRoot!=null) {
				return _repoRoot;
			}
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))) {
					_repoRoot=directoryInfo.FullName;
					return _repoRoot;
				}
				directoryInfo=directoryInfo.Parent;
			}
			Assert.Fail("Repository root not found above "+AppDomain.CurrentDomain.BaseDirectory);
			return null;
		}

		private static string RepoPath(string relativePath) {
			return Path.Combine(RepoRoot(),relativePath.Replace('/','\\'));
		}

		private static string ReadRepoFile(string relativePath) {
			string path=RepoPath(relativePath);
			Assert.IsTrue(File.Exists(path),"Missing file: "+path);
			return File.ReadAllText(path);
		}

		private static byte[] ReadRepoBytes(string relativePath) {
			string path=RepoPath(relativePath);
			Assert.IsTrue(File.Exists(path),"Missing file: "+path);
			return File.ReadAllBytes(path);
		}

		private static string Relative(string path) {
			return path.Substring(RepoRoot().Length).TrimStart('\\');
		}

		///<summary>Every file of the repository's sources that matches the pattern, skipping build output and other checkouts.</summary>
		private static List<string> RepoFiles(string pattern) {
			List<string> listFiles=new List<string>();
			Stack<string> stackFolders=new Stack<string>();
			stackFolders.Push(RepoRoot());
			while(stackFolders.Count>0) {
				string folder=stackFolders.Pop();
				foreach(string subFolder in Directory.GetDirectories(folder)) {
					if(!_arraySkippedFolders.Contains(Path.GetFileName(subFolder),StringComparer.OrdinalIgnoreCase)) {
						stackFolders.Push(subFolder);
					}
				}
				listFiles.AddRange(Directory.GetFiles(folder,pattern));
			}
			return listFiles;
		}

		private static string Sha256(byte[] bytes) {
			using SHA256 sha256=SHA256.Create();
			return string.Concat(sha256.ComputeHash(bytes).Select(x => x.ToString("x2")));
		}

		///<summary>The data entries of a .resx with their decoded bytes, for the entries stored as base64 byte arrays.</summary>
		private static List<(string Name,string Type,byte[] Bytes)> ResxByteEntries(string path) {
			XmlDocument xmlDocument=new XmlDocument();
			xmlDocument.Load(path);
			List<(string,string,byte[])> listEntries=new List<(string,string,byte[])>();
			foreach(XmlElement xmlElement in xmlDocument.SelectNodes("/root/data")) {
				if(xmlElement.GetAttribute("mimetype")!="application/x-microsoft.net.object.bytearray.base64") {
					continue;
				}
				string value=xmlElement.SelectSingleNode("value")?.InnerText ?? "";
				listEntries.Add((xmlElement.GetAttribute("name"),xmlElement.GetAttribute("type"),Convert.FromBase64String(Regex.Replace(value,@"\s",""))));
			}
			return listEntries;
		}

		private static byte[] ResxEntry(string relativePath,string name) {
			List<(string Name,string Type,byte[] Bytes)> listEntries=ResxByteEntries(RepoPath(relativePath)).FindAll(x => x.Name==name);
			Assert.AreEqual(1,listEntries.Count,relativePath+" should hold one "+name);
			return listEntries[0].Bytes;
		}

		///<summary>The images of an .ico file: width, height and bit count of each directory entry (0 in the file means 256).</summary>
		private static List<(int Width,int Height,int BitCount)> IcoImages(byte[] bytes) {
			Assert.IsTrue(bytes.Length>6 && BitConverter.ToUInt16(bytes,0)==0 && BitConverter.ToUInt16(bytes,2)==1,"Not an .ico file.");
			int count=BitConverter.ToUInt16(bytes,4);
			List<(int,int,int)> listImages=new List<(int,int,int)>();
			for(int i=0;i<count;i++) {
				int offset=6+16*i;
				int width=bytes[offset]==0 ? 256 : bytes[offset];
				int height=bytes[offset+1]==0 ? 256 : bytes[offset+1];
				listImages.Add((width,height,BitConverter.ToUInt16(bytes,offset+6)));
			}
			return listImages;
		}

		///<summary>The bytes an Icon was read from (System.Drawing keeps them and Save writes them back unchanged).</summary>
		private static byte[] IconBytes(Icon icon) {
			using MemoryStream memoryStream=new MemoryStream();
			icon.Save(memoryStream);
			return memoryStream.ToArray();
		}

		///<summary>Windows Forms windows are made on a single-threaded apartment thread, as the program does.</summary>
		private static void RunSta(Action action) {
			Exception exception=null;
			Thread thread=new Thread(() => {
				try {
					Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
					Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if(exception!=null) {
				throw new AssertFailedException("Failed on the STA thread: "+exception,exception);
			}
		}
		#endregion Helpers

		#region Window icons
		[TestMethod]
		public void NoFileHoldsAnOpenDentalIcon() {
			List<string> listFound=new List<string>();
			List<string> listResx=RepoFiles("*.resx");
			Assert.IsTrue(listResx.Count>1000,"Only "+listResx.Count+" .resx files found under "+RepoRoot());
			foreach(string path in listResx) {
				foreach((string Name,string Type,byte[] Bytes) entry in ResxByteEntries(path)) {
					if(_arrayOldIconHashes.Contains(Sha256(entry.Bytes))) {
						listFound.Add(Relative(path)+" "+entry.Name);
					}
				}
			}
			foreach(string path in RepoFiles("*.ico")) {
				if(_arrayOldIconHashes.Contains(Sha256(File.ReadAllBytes(path)))) {
					listFound.Add(Relative(path));
				}
			}
			Assert.AreEqual(0,listFound.Count,"Open Dental's icon is still in: "+string.Join(", ",listFound));
		}

		[TestMethod]
		public void EveryEmbeddedWindowIconIsTheCompactIcon() {
			//The forms that do not derive from FormODBase keep an icon of their own: a copy of branding/window.ico.
			string hashWindowIco=Sha256(ReadRepoBytes("branding/window.ico"));
			List<string> listWrong=new List<string>();
			int count=0;
			foreach(string path in RepoFiles("*.resx")) {
				if(_arrayOwnIconFiles.Contains(Relative(path),StringComparer.OrdinalIgnoreCase)) {
					continue;
				}
				foreach((string Name,string Type,byte[] Bytes) entry in ResxByteEntries(path).Where(x => x.Name=="$this.Icon")) {
					count++;
					if(Sha256(entry.Bytes)!=hashWindowIco) {
						listWrong.Add(Relative(path));
					}
				}
			}
			Assert.AreEqual(0,listWrong.Count,"These forms embed an icon other than branding/window.ico: "+string.Join(", ",listWrong));
			//The splash, side programs and test windows; not the hundreds of FormODBase windows.
			Assert.IsTrue(count>0 && count<40,count+" forms embed an icon.");
		}

		[TestMethod]
		public void NoDesignerReadsAnIconItsResxDoesNotHave() {
			List<string> listMissing=new List<string>();
			int count=0;
			foreach(string path in RepoFiles("*.cs")) {
				string code=File.ReadAllText(path);
				if(!code.Contains("GetObject(\"$this.Icon\")")) {
					continue;
				}
				count++;
				string stem=Regex.Replace(Path.GetFileName(path),@"(\.designer)?\.cs$","",RegexOptions.IgnoreCase);
				string pathResx=Path.Combine(Path.GetDirectoryName(path),stem+".resx");
				if(!File.Exists(pathResx) || !ResxByteEntries(pathResx).Any(x => x.Name=="$this.Icon")) {
					listMissing.Add(Relative(path));
				}
			}
			Assert.AreEqual(0,listMissing.Count,"These designers read a $this.Icon their .resx does not have: "+string.Join(", ",listMissing));
			Assert.IsTrue(count>0,"No designer reads an icon at all; the scan found nothing.");
		}

		[TestMethod]
		public void OnlyTheSplashCarriesAWindowIconInTheProgram() {
			//Every FormODBase window gets BrandAssets.AppIcon at run time; the only form of OpenPmsDental.exe with an icon of its own is the splash.
			Assembly assembly=typeof(FormODBase).Assembly;
			List<string> listWithIcon=new List<string>();
			foreach(string name in assembly.GetManifestResourceNames().Where(x => x.EndsWith(".resources"))) {
				using Stream stream=assembly.GetManifestResourceStream(name);
				using ResourceReader resourceReader=new ResourceReader(stream);
				IDictionaryEnumerator enumerator=resourceReader.GetEnumerator();
				while(enumerator.MoveNext()) {
					if((string)enumerator.Key=="$this.Icon") {
						listWithIcon.Add(name);
					}
				}
			}
			CollectionAssert.AreEquivalent(new[] { "OpenPmsDental.FormSplash.resources" },listWithIcon,
				"Forms of OpenPmsDental.exe with an icon of their own: "+string.Join(", ",listWithIcon));
		}

		[TestMethod]
		public void TheProgramEmbedsTheBrandFilesOnce() {
			CollectionAssert.AreEqual(ReadRepoBytes("branding/app.ico"),BrandAssets.GetResourceBytes(BrandAssets.AppIconResourceName));
			CollectionAssert.AreEqual(ReadRepoBytes("branding/logo-256.png"),BrandAssets.GetResourceBytes(BrandAssets.LogoResourceName));
			string[] arrayNames=typeof(BrandAssets).Assembly.GetManifestResourceNames();
			Assert.AreEqual(1,arrayNames.Count(x => x.EndsWith(".ico",StringComparison.OrdinalIgnoreCase)),"Icons embedded: "
				+string.Join(", ",arrayNames.Where(x => x.EndsWith(".ico",StringComparison.OrdinalIgnoreCase))));
			Assert.AreSame(BrandAssets.AppIcon,BrandAssets.AppIcon,"Every window shares one icon.");
			using Bitmap bitmap=BrandAssets.GetLogo();
			Assert.AreEqual(new Size(256,256),bitmap.Size);
		}

		[TestMethod]
		public void FormODBase_SetsTheSharedIconWhenItsHandleIsCreated() {
			string code=ReadRepoFile("OpenPmsDental/UI/BordersDpi/FormODBase.cs");
			StringAssert.Contains(code,"protected override void OnHandleCreated(EventArgs e) {");
			StringAssert.Contains(code,"this.Icon=BrandAssets.AppIcon;");
			RunSta(() => {
				using FormODBase formODBase=new FormODBase();
				using FormODBase formODBase2=new FormODBase();
				Assert.IsFalse(formODBase.IsHandleCreated);
				Assert.AreNotEqual(IntPtr.Zero,formODBase.Handle);
				Assert.AreNotEqual(IntPtr.Zero,formODBase2.Handle);
				Assert.AreSame(BrandAssets.AppIcon,formODBase.Icon);
				Assert.AreSame(BrandAssets.AppIcon,formODBase2.Icon);
				//The large sizes the taskbar and Alt+Tab use are there: the window's icon is the full app.ico, not the compact window.ico.
				CollectionAssert.AreEqual(ReadRepoBytes("branding/app.ico"),IconBytes(formODBase.Icon));
				//Windows has the window's big icon (taskbar, Alt+Tab) and small icon (title bar) at the system sizes.
				IntPtr hIconBig=SendMessage(formODBase.Handle,WM_GETICON,(IntPtr)ICON_BIG,IntPtr.Zero);
				IntPtr hIconSmall=SendMessage(formODBase.Handle,WM_GETICON,(IntPtr)ICON_SMALL,IntPtr.Zero);
				Assert.AreNotEqual(IntPtr.Zero,hIconBig,"The window has no big icon.");
				Assert.AreNotEqual(IntPtr.Zero,hIconSmall,"The window has no small icon.");
				Assert.AreEqual(SystemInformation.IconSize,Icon.FromHandle(hIconBig).Size);
				Assert.AreEqual(SystemInformation.SmallIconSize,Icon.FromHandle(hIconSmall).Size);
				//A real window of the program: the About box, built from its designer (it is never loaded or shown).
				using FormAbout formAbout=new FormAbout();
				Assert.AreNotEqual(IntPtr.Zero,formAbout.Handle);
				Assert.AreSame(BrandAssets.AppIcon,formAbout.Icon);
			});
		}

		[TestMethod]
		public void FormODBase_KeepsAnIconTheFormSetItself() {
			RunSta(() => {
				using Icon icon=new Icon(RepoPath("branding/window.ico"));
				using FormODBase formODBase=new FormODBase();
				formODBase.Icon=icon;
				Assert.AreNotEqual(IntPtr.Zero,formODBase.Handle);
				Assert.AreSame(icon,formODBase.Icon);
			});
		}

		[TestMethod]
		public void WpfFrames_UseTheProgramIcon() {
			//WpfControlsOD's Icon resource is branding/app.ico itself, and FormFrame (the frame of every WPF window) sets it as its window icon.
			StringAssert.Contains(ReadRepoFile("WpfControlsOD/Properties/Resources.resx"),@"<value>..\..\branding\app.ico;System.Drawing.Icon, System.Drawing");
			Assert.IsFalse(ReadRepoFile("WpfControlsOD/WpfControlsOD.csproj").Contains(@"Resources\Icon.ico"));
			Assert.IsFalse(File.Exists(RepoPath("WpfControlsOD/Resources/Icon.ico")));
			Assert.IsFalse(ResxByteEntries(RepoPath("WpfControlsOD/Frame/FormFrame.resx")).Any(x => x.Name=="$this.Icon"));
			StringAssert.Contains(ReadRepoFile("WpfControlsOD/Frame/FormFrame.cs"),"this.Icon=_lazyIconApp.Value;");
			//The built WpfControlsOD.dll carries it. (A FormFrame is not built here: without its UIManager its window procedure is not meant to run.)
			Type typeResources=Type.GetType("WpfControls.Properties.Resources, WpfControlsOD",throwOnError:true);
			using Icon iconResource=(Icon)typeResources.GetProperty("Icon",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).GetValue(null);
			CollectionAssert.AreEqual(ReadRepoBytes("branding/app.ico"),IconBytes(iconResource));
		}

		[TestMethod]
		public void WindowIco_IsSixteenAndThirtyTwoPixelsOnly() {
			List<(int Width,int Height,int BitCount)> listImages=IcoImages(ReadRepoBytes("branding/window.ico"));
			CollectionAssert.AreEqual(new[] { 16,32 },listImages.Select(x => x.Width).ToArray());
			CollectionAssert.AreEqual(new[] { 16,32 },listImages.Select(x => x.Height).ToArray());
			Assert.IsTrue(listImages.All(x => x.BitCount==32),"window.ico must be 32-bit.");
			//app.ico has every size the taskbar, Alt+Tab and the program file need.
			List<(int Width,int Height,int BitCount)> listApp=IcoImages(ReadRepoBytes("branding/app.ico"));
			CollectionAssert.AreEqual(new[] { 16,20,24,32,40,48,64,128,256 },listApp.Select(x => x.Width).ToArray());
		}
		#endregion Window icons

		#region Program files
		[TestMethod]
		public void EveryApplicationIconIsTheBrandIcon() {
			byte[] bytesAppIco=ReadRepoBytes("branding/app.ico");
			List<string> listWrong=new List<string>();
			List<string> listWithIcon=new List<string>();
			foreach(string path in RepoFiles("*.csproj")) {
				if(_arrayOwnIconFiles.Contains(Relative(path),StringComparer.OrdinalIgnoreCase)) {
					continue;
				}
				foreach(Match match in Regex.Matches(File.ReadAllText(path),@"<ApplicationIcon>\s*([^<]*?)\s*</ApplicationIcon>")) {
					if(match.Groups[1].Value=="") {
						continue;
					}
					listWithIcon.Add(Path.GetFileName(path));
					string pathIcon=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),match.Groups[1].Value));
					if(!File.Exists(pathIcon) || !File.ReadAllBytes(pathIcon).SequenceEqual(bytesAppIco)) {
						listWrong.Add(Relative(path)+": "+match.Groups[1].Value);
					}
				}
			}
			Assert.AreEqual(0,listWrong.Count,"ApplicationIcon is not branding/app.ico: "+string.Join(", ",listWrong));
			//The shipped executables (tools/Build-Release.ps1).
			foreach(string csproj in new[] { "OpenPmsDental.csproj","ServiceManager.csproj","UpdateFileCopier.csproj" }) {
				Assert.IsTrue(listWithIcon.Contains(csproj),csproj+" has no ApplicationIcon.");
			}
		}

		[TestMethod]
		public void NoReferenceToTheOldSplashOrLogoRemains() {
			string[] arrayNames=new[] { "splashCanada","splashBenco","bencoLogo","benco_logo","ODLogo","OpenDentalMain","CreateBencoSplash" };
			string[] arrayPatterns=new[] { "*.cs","*.resx","*.csproj","*.xaml","*.iss","*.ps1","*.targets","*.props","*.slnf" };
			string folderTests=RepoPath("tests/AttributionTests")+"\\";
			List<string> listFound=new List<string>();
			foreach(string path in arrayPatterns.SelectMany(x => RepoFiles(x))) {
				if(path.StartsWith(folderTests,StringComparison.OrdinalIgnoreCase)) {
					continue;//these tests name them
				}
				string text=File.ReadAllText(path);
				foreach(string name in arrayNames.Where(x => text.Contains(x))) {
					listFound.Add(Relative(path)+": "+name);
				}
			}
			Assert.AreEqual(0,listFound.Count,"Still referenced: "+string.Join(", ",listFound));
			foreach(string file in new[] { "ODLogo.gif","splash.jpg","splashCanada.jpg","splashBenco.jpg","benco_logo.png","OpenDentalMain.ico" }) {
				Assert.IsFalse(File.Exists(RepoPath("OpenPmsDental/Resources/"+file)),file+" is still in OpenPmsDental/Resources.");
			}
			Assert.IsFalse(File.Exists(RepoPath("OpenPmsDental/Misc/App.ico")),"OpenPmsDental/Misc/App.ico is still there.");
		}
		#endregion Program files

		#region Splash, About box, installer, README
		[TestMethod]
		public void FormSplash_BackgroundIsTheBrandSplash() {
			CollectionAssert.AreEqual(ReadRepoBytes("branding/splash.png"),ResxEntry("OpenPmsDental/Forms/FormSplash.resx","$this.BackgroundImage"));
			StringAssert.Contains(ReadRepoFile("OpenPmsDental/Forms/FormSplash.Designer.cs"),"resources.GetObject(\"$this.BackgroundImage\")");
			//The office's own Splash.jpg in the program folder still takes its place.
			StringAssert.Contains(ReadRepoFile("OpenPmsDental/Forms/FormSplash.cs"),@"\Splash.jpg");
			//The built program carries it.
			using Bitmap bitmap=(Bitmap)new System.ComponentModel.ComponentResourceManager(typeof(FormSplash)).GetObject("$this.BackgroundImage");
			Assert.AreEqual(new Size(500,300),bitmap.Size);
		}

		[TestMethod]
		public void AboutBox_ShowsTheLogoBesideTheTextWithoutCoveringIt() {
			StringAssert.Contains(ReadRepoFile("OpenPmsDental/Forms/FormAbout.cs"),"pictureLogo.Image=BrandAssets.GetLogo();");
			RunSta(() => {
				using FormAbout formAbout=new FormAbout();
				Control controlLogo=formAbout.Controls.Find("pictureLogo",false).Single();
				Assert.AreEqual(new Size(48,48),controlLogo.Size);
				foreach(Control control in formAbout.Controls) {
					if(control==controlLogo || control.Name=="") {
						continue;
					}
					Assert.IsFalse(control.Bounds.IntersectsWith(controlLogo.Bounds),"The logo covers "+control.Name);
				}
				Assert.IsTrue(new Rectangle(Point.Empty,formAbout.ClientSize).Contains(controlLogo.Bounds),"The logo is outside the window.");
			});
		}

		[TestMethod]
		public void InstallerScript_NamesTheBrandingFiles() {
			string script=ReadRepoFile("installer/OpenPmsDental.iss");
			string[] arrayLines=script.Split(new[] { "\r\n","\n" },StringSplitOptions.None).Select(x => x.Trim()).ToArray();
			CollectionAssert.Contains(arrayLines,@"SetupIconFile=..\branding\app.ico");
			CollectionAssert.Contains(arrayLines,@"WizardImageFile=..\branding\installer-wizard.bmp,..\branding\installer-wizard-2x.bmp");
			CollectionAssert.Contains(arrayLines,@"WizardSmallImageFile=..\branding\installer-small.bmp,..\branding\installer-small-2x.bmp");
			CollectionAssert.Contains(arrayLines,@"UninstallDisplayIcon={app}\{#ExeName}");
			foreach(string file in new[] { "app.ico","installer-wizard.bmp","installer-wizard-2x.bmp","installer-small.bmp","installer-small-2x.bmp" }) {
				Assert.IsTrue(File.Exists(RepoPath("branding/"+file)),"Missing branding/"+file);
			}
		}

		[TestMethod]
		public void Readme_ShowsTheLogoAboveTheTitle() {
			string[] arrayLines=ReadRepoFile("README.md").Split(new[] { "\r\n","\n" },StringSplitOptions.None);
			StringAssert.StartsWith(arrayLines[0],"<img src=\"branding/logo-256.png\"");
			StringAssert.Contains(arrayLines[0],"width=\"96\"");
			Assert.AreEqual("# Open PMS - Dental",arrayLines.First(x => x.StartsWith("#")));
		}
		#endregion Splash, About box, installer, README
	}
}
