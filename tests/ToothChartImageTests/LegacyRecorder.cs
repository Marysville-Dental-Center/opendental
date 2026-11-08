// Records today's tooth chart (DirectX 9, OpenGL and Simple 2D) for every synthetic scene: the "before" pictures of the WPF chart track (package
// TC-0 of docs/specs/wpf-tooth-chart-plan.md, launch check L-1). Opt-in: it runs only with OPMS_RECORD_LEGACY_CHART=1, on the desktop of an
// interactive session, because the DirectX and OpenGL charts need a visible window and a graphics device. No database: the scenes set their state
// through ToothChartRelay only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using SparksToothChart;

namespace ToothChartImageTests {
	///<summary>The recorder of the legacy chart pictures and the byte-for-byte check of a re-recording.
	///
	///Record (the lead's run, L-1; from the repository root, after a build of OpenDental.Local.slnf):
	///  set OPMS_RECORD_LEGACY_CHART=1
	///  vstest.console.exe tests\ToothChartImageTests\bin\Debug\ToothChartImageTests.dll /Platform:x86 /Tests:RecordLegacyChart
	///The pictures go to tests\ToothChartImageTests\Baselines\legacy\&lt;mode&gt;\&lt;width&gt;x&lt;height&gt;\&lt;scene&gt;.png, each with a .txt
	///next to it, plus manifest.txt and index.html for the whole folder. DirectX uses the first standard device format that works, with
	///multisampling turned off so that the same scene gives the same pixels (see FindDirectXFormat); each .txt names both formats and says whether
	///a second picture of the same state was identical. Optional: OPMS_RECORD_LEGACY_CHART_OUT (another folder),
	///OPMS_RECORD_LEGACY_CHART_ENGINES (for example "DirectX"), OPMS_RECORD_LEGACY_CHART_SCENES (scene names, for a quick look) and
	///OPMS_RECORD_LEGACY_CHART_DIRECTX_FORMAT (a device format string instead of the first standard format that works).
	///
	///Check a re-recording (after the SharpDX cut, on the same PC): record into a scratch folder with OPMS_RECORD_LEGACY_CHART_OUT, then
	///  set OPMS_COMPARE_LEGACY_CHART=&lt;that folder&gt;
	///  vstest.console.exe ... /Tests:CompareLegacyRecording
	///which fails unless every PNG there has the same bytes as its baseline.</summary>
	[TestClass]
	public class LegacyRecorder {
		public const string EnvRecord="OPMS_RECORD_LEGACY_CHART";
		public const string EnvOut="OPMS_RECORD_LEGACY_CHART_OUT";
		public const string EnvEngines="OPMS_RECORD_LEGACY_CHART_ENGINES";
		public const string EnvScenes="OPMS_RECORD_LEGACY_CHART_SCENES";
		public const string EnvDirectXFormat="OPMS_RECORD_LEGACY_CHART_DIRECTX_FORMAT";
		public const string EnvCompare="OPMS_COMPARE_LEGACY_CHART";
		///<summary>The modes in the order they are recorded.</summary>
		public static readonly DrawingMode[] ArrayEngines={ DrawingMode.DirectX,DrawingMode.OpenGL,DrawingMode.Simple2D };

		[TestMethod]
		public void RecordLegacyChart() {
			if(Environment.GetEnvironmentVariable(EnvRecord)!="1") {
				Assert.Inconclusive("The legacy chart recorder runs only with "+EnvRecord+"=1, on an interactive desktop (TC-0, launch check L-1).");
			}
			if(!Environment.UserInteractive) {
				Assert.Inconclusive("The legacy chart recorder needs an interactive desktop session: the DirectX and OpenGL charts draw in a visible window.");
			}
			string folderOut=Environment.GetEnvironmentVariable(EnvOut);
			if(string.IsNullOrWhiteSpace(folderOut)) {
				folderOut=TestPaths.BaselinesLegacy;
			}
			List<DrawingMode> listEngines=ParseEngines(Environment.GetEnvironmentVariable(EnvEngines));
			List<ChartScene> listScenes=ChartScenes.GetAll();
			string scenesWanted=Environment.GetEnvironmentVariable(EnvScenes);
			bool isAllScenes=string.IsNullOrWhiteSpace(scenesWanted);
			if(!isAllScenes) {
				List<string> listNames=scenesWanted.Split(',').Select(x => x.Trim()).Where(x => x!="").ToList();
				listScenes=listNames.Select(x => ChartScenes.Get(x)).ToList();
			}
			LegacyRecording legacyRecording=StaRunner.Run(() => LegacyRecording.Record(folderOut,listEngines,listScenes,isAllScenes,
				Environment.GetEnvironmentVariable(EnvDirectXFormat)));
			Console.WriteLine(legacyRecording.GetSummary());
			Assert.AreEqual(0,legacyRecording.ListFailures.Count,"Some pictures could not be made:\r\n"+string.Join("\r\n",legacyRecording.ListFailures));
		}

		[TestMethod]
		public void CompareLegacyRecording() {
			string folderNew=Environment.GetEnvironmentVariable(EnvCompare);
			if(string.IsNullOrWhiteSpace(folderNew)) {
				Assert.Inconclusive("Set "+EnvCompare+" to a folder recorded with "+EnvOut+" to compare it with Baselines\\legacy byte for byte.");
			}
			List<string> listPaths=Directory.GetFiles(folderNew,"*.png",SearchOption.AllDirectories).OrderBy(x => x,StringComparer.Ordinal).ToList();
			Assert.IsTrue(listPaths.Count>0,"No pictures in "+folderNew);
			List<string> listDifferent=new List<string>();
			int countIdentical=0;
			foreach(string path in listPaths) {
				string pathRelative=path.Substring(folderNew.TrimEnd('\\').Length+1);
				string pathBaseline=Path.Combine(TestPaths.BaselinesLegacy,pathRelative);
				if(!File.Exists(pathBaseline)) {
					listDifferent.Add(pathRelative+": no baseline");
					continue;
				}
				if(ChartImageCompare.AreFilesIdentical(path,pathBaseline)) {
					countIdentical++;
					continue;
				}
				using Bitmap bitmapNew=new Bitmap(path);
				using Bitmap bitmapBaseline=new Bitmap(pathBaseline);
				listDifferent.Add(pathRelative+": the bytes differ. "+ChartImageCompare.Compare(bitmapBaseline,bitmapNew));
			}
			Console.WriteLine(countIdentical+" of "+listPaths.Count+" pictures are byte-identical to Baselines\\legacy.");
			Assert.AreEqual(0,listDifferent.Count,listDifferent.Count+" pictures differ:\r\n"+string.Join("\r\n",listDifferent));
		}

		///<summary>"DirectX,OpenGL" gives those modes in recording order; empty gives all three.</summary>
		public static List<DrawingMode> ParseEngines(string engines) {
			if(string.IsNullOrWhiteSpace(engines)) {
				return ArrayEngines.ToList();
			}
			List<DrawingMode> listEngines=engines.Split(',').Select(x => (DrawingMode)Enum.Parse(typeof(DrawingMode),x.Trim(),true)).ToList();
			return ArrayEngines.Where(x => listEngines.Contains(x)).ToList();
		}
	}

	///<summary>One run of the recorder.</summary>
	public class LegacyRecording {
		public List<string> ListWritten=new List<string>();
		public List<string> ListFailures=new List<string>();
		public List<string> ListNotes=new List<string>();
		private string _folderOut;
		private string _environmentText;
		private string _directXFormat;
		///<summary>The format the standard search found, before multisampling was turned off.</summary>
		private string _directXFormatSearched;
		private Form _formHost;

		///<summary>Draws every scene in every mode at every size of the scene, writes a PNG and a .txt for each, then rewrites manifest.txt and
		///index.html from all the .txt files in folderOut. When isAllScenes, the old files of the recorded modes are deleted first. Runs on an STA
		///thread.</summary>
		public static LegacyRecording Record(string folderOut,List<DrawingMode> listEngines,List<ChartScene> listScenes,bool isAllScenes,
			string directXFormatGiven)
		{
			LegacyRecording legacyRecording=new LegacyRecording();
			legacyRecording._folderOut=folderOut;
			legacyRecording.Run(listEngines,listScenes,isAllScenes,directXFormatGiven);
			return legacyRecording;
		}

		private void Run(List<DrawingMode> listEngines,List<ChartScene> listScenes,bool isAllScenes,string directXFormatGiven) {
			Directory.CreateDirectory(_folderOut);
			if(isAllScenes) {
				foreach(DrawingMode engine in listEngines) {
					string folderEngine=Path.Combine(_folderOut,engine.ToString());
					if(Directory.Exists(folderEngine)) {
						Directory.Delete(folderEngine,true);
					}
				}
			}
			_environmentText=ReadEnvironment();
			using(_formHost=CreateHostForm()) {
				_formHost.Show();
				_formHost.Activate();
				Application.DoEvents();
				if(!_formHost.Visible || !IsWindowVisible(_formHost.Handle)) {
					throw new AssertInconclusiveException("The recorder's window is not visible: no interactive desktop.");
				}
				if(listEngines.Contains(DrawingMode.DirectX)) {
					_directXFormat=FindDirectXFormat(directXFormatGiven);
				}
				foreach(DrawingMode engine in listEngines) {
					RecordEngine(engine,listScenes);
				}
				_formHost.Close();
			}
			WriteManifestAndIndex(_folderOut);
		}

		private void RecordEngine(DrawingMode engine,List<ChartScene> listScenes) {
			//Chart scenes: one wrapper per size, reused from scene to scene as the Chart module reuses its chart. Standard sizes first, then the
			//extra sizes in scene order.
			List<ChartScene> listChartScenes=listScenes.Where(x => x.Kind==ChartSceneKind.Chart).ToList();
			List<ChartSize> listSizes=new List<ChartSize>();
			foreach(ChartSize chartSize in ChartScenes.GetStandardSizes(ChartSceneKind.Chart).Concat(listChartScenes.SelectMany(x => x.ListSizesExtra))) {
				if(!listSizes.Contains(chartSize)) {
					listSizes.Add(chartSize);
				}
			}
			foreach(ChartSize chartSize in listSizes) {
				List<ChartScene> listForSize=listChartScenes.Where(x => ChartScenes.GetSizes(x).Contains(chartSize)).ToList();
				if(listForSize.Count==0) {
					continue;
				}
				ToothChartWrapper wrapper=CreateWrapper(engine,chartSize.Size);
				try {
					ToothChartRelay relay=new ToothChartRelay();
					relay.SetToothChartWrapper(wrapper);
					foreach(ChartScene scene in listForSize) {
						RecordScene(wrapper,relay,engine,scene,chartSize);
					}
				}
				finally {
					DisposeWrapper(wrapper);
				}
			}
			//Perio scenes: DirectX only, as FormPerioGraphical forces DirectX. Each gets a new wrapper, because ResetTeeth keeps perio measures.
			if(engine!=DrawingMode.DirectX) {
				return;
			}
			foreach(ChartScene scene in listScenes.Where(x => x.Kind==ChartSceneKind.Perio)) {
				foreach(ChartSize chartSize in ChartScenes.GetSizes(scene)) {
					ToothChartWrapper wrapper=CreateWrapper(engine,chartSize.Size);
					try {
						ToothChartRelay relay=new ToothChartRelay();
						relay.SetToothChartWrapper(wrapper);
						RecordScene(wrapper,relay,engine,scene,chartSize);
					}
					finally {
						DisposeWrapper(wrapper);
					}
				}
			}
		}

		private void RecordScene(ToothChartWrapper wrapper,ToothChartRelay relay,DrawingMode engine,ChartScene scene,ChartSize chartSize) {
			string folder=Path.Combine(_folderOut,engine.ToString(),chartSize.FolderName);
			Directory.CreateDirectory(folder);
			string pathPng=Path.Combine(folder,scene.Name+".png");
			string pathText=Path.Combine(folder,scene.Name+".txt");
			string pathRelative=engine+"/"+chartSize.FolderName+"/"+scene.Name+".png";
			List<string> listLines=new List<string>();
			listLines.Add("Scene: "+scene.Name);
			listLines.Add("Group: "+scene.Group);
			listLines.Add("Description: "+scene.Description);
			listLines.Add("Size: "+chartSize.FolderName);
			listLines.Add("Colours: "+GetColorSetText(chartSize.ColorSet));
			listLines.Add("Requested mode: "+engine);
			listLines.Add("Started mode: "+wrapper.DrawMode);
			listLines.Add("Device or pixel format: "+GetDeviceOrPixelFormat(wrapper));
			if(wrapper.DrawMode==DrawingMode.DirectX) {
				listLines.Add("DirectX standard search: "+_directXFormatSearched+(_directXFormatSearched!=_directXFormat?" (multisampling turned off: "
					+"with it a draw is not repeatable on every driver)":""));
			}
			try {
				_formHost.Activate();
				Application.DoEvents();
				ChartScenes.Prepare(relay,scene,chartSize.ColorSet);
				using Bitmap bitmap=relay.GetBitmap();
				if(bitmap==null) {
					throw new Exception("GetBitmap returned nothing.");
				}
				BitmapFacts bitmapFacts=ChartImageCompare.DescribeBitmap(bitmap);
				if(wrapper.DrawMode==DrawingMode.OpenGL) {
					listLines.Add("OpenGL: "+GetOpenGLStrings());
				}
				PngWriter.Save(bitmap,pathPng);
				//A second picture of the same state: a recording is only worth comparing with a later one if the same state gives the same pixels.
				string pixelsSecond;
				using(Bitmap bitmapSecond=relay.GetBitmap()) {
					pixelsSecond=ChartImageCompare.DescribeBitmap(bitmapSecond).PixelsSha256;
				}
				bool isRepeatable=pixelsSecond==bitmapFacts.PixelsSha256;
				listLines.Add("Bitmap: "+bitmapFacts.Size.Width+"x"+bitmapFacts.Size.Height+" "+bitmapFacts.PixelFormat);
				listLines.Add("Alpha: "+bitmapFacts.GetAlphaText());
				listLines.Add("Colours in the picture: "+bitmapFacts.CountColors+(bitmapFacts.IsBlank?" (blank)":""));
				listLines.Add("Pixels SHA-256: "+bitmapFacts.PixelsSha256);
				listLines.Add("Repeatable: "+(isRepeatable?"yes (a second GetBitmap gave the same pixels)":"no (a second GetBitmap gave other pixels)"));
				if(bitmapFacts.IsBlank) {
					ListNotes.Add(pathRelative+" is blank (one colour).");
				}
				if(!isRepeatable) {
					ListNotes.Add(pathRelative+" is not repeatable: a second GetBitmap gave other pixels.");
				}
				if(wrapper.DrawMode!=engine) {
					ListNotes.Add(pathRelative+" was drawn by "+wrapper.DrawMode+": "+engine+" did not start.");
				}
				ListWritten.Add(pathRelative);
			}
			catch(Exception ex) {
				listLines.Add("Failed: "+ex.GetType().Name+": "+ex.Message.Replace("\r"," ").Replace("\n"," "));
				ListFailures.Add(pathRelative+": "+ex.GetType().Name+": "+ex.Message);
				if(File.Exists(pathPng)) {
					File.Delete(pathPng);
				}
			}
			listLines.AddRange(_environmentText.Split(new[] { "\r\n" },StringSplitOptions.RemoveEmptyEntries));
			File.WriteAllLines(pathText,listLines,new UTF8Encoding(false));
		}

		#region Wrappers
		private static Form CreateHostForm() {
			Form form=new Form();
			form.Text="Tooth chart recorder (TC-0)";
			form.FormBorderStyle=FormBorderStyle.None;
			form.ShowInTaskbar=false;
			form.StartPosition=FormStartPosition.Manual;
			Rectangle rectangleWork=System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
			form.Location=new Point(rectangleWork.Left+20,rectangleWork.Top+20);
			form.ClientSize=ChartScenes.SizeModule.Size;
			form.TopMost=true;
			form.BackColor=Color.Black;
			return form;
		}

		///<summary>A wrapper on the visible host form, set up as a new computer has it (ComputerPrefs.GetForLocalComputer: no hardware flag, pixel format
		///0) and with the DirectX device format the recorder found, then started in engine. The fallback may start another mode: the .txt says which.</summary>
		private ToothChartWrapper CreateWrapper(DrawingMode engine,Size size) {
			_formHost.ClientSize=size;
			ToothChartWrapper wrapper=new ToothChartWrapper();
			wrapper.Location=new Point(0,0);
			wrapper.Size=size;
			wrapper.UseHardware=false;
			wrapper.PreferredPixelFormatNumber=0;
			if(engine==DrawingMode.DirectX) {
				wrapper.DeviceFormat=new ToothChartDirectX.DirectXDeviceFormat(_directXFormat??"invalid");
			}
			_formHost.Controls.Add(wrapper);
			wrapper.DrawMode=engine;
			Application.DoEvents();
			return wrapper;
		}

		private void DisposeWrapper(ToothChartWrapper wrapper) {
			_formHost.Controls.Remove(wrapper);
			wrapper.Dispose();
			Application.DoEvents();
		}

		///<summary>The DirectX device format to record with: the given one, or the first standard format that starts and gives a picture (the search
		///FormOpenDental makes for a computer with no format saved, FormGraphics.GetPreferredDirectXFormat) with multisampling turned off. With
		///multisampling a draw is not repeatable on every driver: on the build PC (2026-11-08) every GetBitmap of the same scene with four samples gave
		///other pixels along the edges, so a re-recording could never be compared byte for byte. Without it the same scene gives the same pixels.
		///Null when DirectX cannot start.</summary>
		private string FindDirectXFormat(string directXFormatGiven) {
			if(!string.IsNullOrWhiteSpace(directXFormatGiven)) {
				ListNotes.Add("DirectX device format given: "+directXFormatGiven);
				_directXFormatSearched="not searched (given)";
				return directXFormatGiven;
			}
			if(!ToothChartWrapper.IsDirectXRuntimeAvailable()) {
				ListNotes.Add("DirectX cannot start: "+ToothChartWrapper.D3DX_DLL+" is missing.");
				_directXFormatSearched="none ("+ToothChartWrapper.D3DX_DLL+" is missing)";
				return null;
			}
			foreach(ToothChartDirectX.DirectXDeviceFormat directXDeviceFormat in ToothChartDirectX.GetStandardDeviceFormats()) {
				if(!TestDirectXFormat(directXDeviceFormat.ToString())) {
					continue;
				}
				_directXFormatSearched=directXDeviceFormat.ToString();
				directXDeviceFormat.MultiSampleCount=0;
				if(directXDeviceFormat.ToString()!=_directXFormatSearched && !TestDirectXFormat(directXDeviceFormat.ToString())) {
					ListNotes.Add("DirectX device format found: "+_directXFormatSearched+"; it does not start without multisampling, so it is used as it is.");
					return _directXFormatSearched;
				}
				ListNotes.Add("DirectX device format found: "+_directXFormatSearched+"; recorded without multisampling: "+directXDeviceFormat);
				return directXDeviceFormat.ToString();
			}
			ListNotes.Add("No standard DirectX device format works on this computer.");
			_directXFormatSearched="none (no standard format starts)";
			return null;
		}

		///<summary>FormGraphics.TestDirectXFormat: an invisible wrapper on the host form starts DirectX with the format and returns a picture.</summary>
		private bool TestDirectXFormat(string directXFormat) {
			using ToothChartWrapper wrapperTest=new ToothChartWrapper();
			wrapperTest.Visible=false;
			_formHost.Controls.Add(wrapperTest);
			try {
				wrapperTest.DeviceFormat=new ToothChartDirectX.DirectXDeviceFormat(directXFormat);
				wrapperTest.DrawMode=DrawingMode.DirectX;
				if(wrapperTest.DrawMode!=DrawingMode.DirectX) {
					return false;
				}
				using Bitmap bitmap=wrapperTest.GetBitmap();
				return bitmap!=null;
			}
			catch {
				return false;
			}
			finally {
				_formHost.Controls.Remove(wrapperTest);
			}
		}
		#endregion Wrappers

		#region Facts
		private static string GetColorSetText(ChartColorSet chartColorSet) {
			switch(chartColorSet) {
				case ChartColorSet.Module:
					return "module (Chart Graphic Colors 10-13)";
				case ChartColorSet.Printout:
					return "printout (Chart Graphic Colors 14 and 15)";
				default:
					return "perio (white, black and the perio colours)";
			}
		}

		private string GetDeviceOrPixelFormat(ToothChartWrapper wrapper) {
			switch(wrapper.DrawMode) {
				case DrawingMode.DirectX:
					return "DirectX device format "+(wrapper.DeviceFormat==null?"none":wrapper.DeviceFormat.ToString());
				case DrawingMode.OpenGL:
					return "OpenGL pixel format "+wrapper.PreferredPixelFormatNumber+" ("+DescribePixelFormat(wrapper)+")";
				default:
					return "GDI+ (no device)";
			}
		}

		///<summary>The OpenGL pixel format of the engine control: colour, depth and stencil bits, double buffering and acceleration.</summary>
		private static string DescribePixelFormat(ToothChartWrapper wrapper) {
			if(wrapper.Controls.Count==0) {
				return "no engine control";
			}
			IntPtr hwnd=wrapper.Controls[0].Handle;
			IntPtr hdc=GetDC(hwnd);
			try {
				PIXELFORMATDESCRIPTOR pfd=new PIXELFORMATDESCRIPTOR();
				pfd.nSize=(ushort)Marshal.SizeOf(typeof(PIXELFORMATDESCRIPTOR));
				pfd.nVersion=1;
				if(DescribePixelFormat(hdc,wrapper.PreferredPixelFormatNumber,(uint)pfd.nSize,ref pfd)==0) {
					return "not described";
				}
				const uint PFD_DOUBLEBUFFER=0x1;
				const uint PFD_GENERIC_FORMAT=0x40;
				const uint PFD_GENERIC_ACCELERATED=0x1000;
				string acceleration="accelerated";
				if((pfd.dwFlags&PFD_GENERIC_FORMAT)!=0) {
					acceleration=(pfd.dwFlags&PFD_GENERIC_ACCELERATED)!=0?"generic accelerated":"generic (software)";
				}
				return pfd.cColorBits+"-bit colour, "+pfd.cAlphaBits+"-bit alpha, "+pfd.cDepthBits+"-bit depth, "+pfd.cStencilBits+"-bit stencil, "
					+((pfd.dwFlags&PFD_DOUBLEBUFFER)!=0?"double buffered":"single buffered")+", "+acceleration;
			}
			finally {
				ReleaseDC(hwnd,hdc);
			}
		}

		///<summary>The OpenGL vendor, renderer and version of the current context (the chart's, after a picture).</summary>
		private static string GetOpenGLStrings() {
			const uint GL_VENDOR=0x1F00;
			const uint GL_RENDERER=0x1F01;
			const uint GL_VERSION=0x1F02;
			try {
				return Marshal.PtrToStringAnsi(glGetString(GL_VENDOR))+"; "+Marshal.PtrToStringAnsi(glGetString(GL_RENDERER))+"; "
					+Marshal.PtrToStringAnsi(glGetString(GL_VERSION));
			}
			catch(Exception ex) {
				return "unknown ("+ex.GetType().Name+")";
			}
		}

		///<summary>The Windows build, the display adapters with their drivers and the screen DPI: the lines every .txt ends with.</summary>
		private static string ReadEnvironment() {
			StringBuilder stringBuilder=new StringBuilder();
			string windows=Environment.OSVersion.VersionString;
			try {
				using RegistryKey registryKeyBase=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
				using RegistryKey registryKey=registryKeyBase.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
				if(registryKey!=null) {
					windows="build "+registryKey.GetValue("CurrentBuild")+"."+registryKey.GetValue("UBR")+" ("+registryKey.GetValue("DisplayVersion")+")";
				}
			}
			catch(Exception ex) {
				ex.ToString();//the version string above is enough
			}
			stringBuilder.Append("Windows: "+windows+"\r\n");
			try {
				using ManagementObjectSearcher managementObjectSearcher=new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController");
				List<string> listAdapters=new List<string>();
				foreach(ManagementBaseObject managementBaseObject in managementObjectSearcher.Get()) {
					listAdapters.Add(managementBaseObject["Name"]+", driver "+managementBaseObject["DriverVersion"]);
				}
				foreach(string adapter in listAdapters.OrderBy(x => x,StringComparer.Ordinal)) {
					stringBuilder.Append("Adapter: "+adapter+"\r\n");
				}
			}
			catch(Exception ex) {
				stringBuilder.Append("Adapter: unknown ("+ex.GetType().Name+")\r\n");
			}
			using(Graphics g=Graphics.FromHwnd(IntPtr.Zero)) {
				stringBuilder.Append("Screen DPI seen by the test host: "+g.DpiX+"\r\n");
			}
			return stringBuilder.ToString();
		}
		#endregion Facts

		#region Manifest and index
		///<summary>Reads the "Key: value" lines of a recording's .txt.</summary>
		public static Dictionary<string,string> ReadSidecar(string pathText) {
			Dictionary<string,string> dict=new Dictionary<string,string>();
			foreach(string line in File.ReadAllLines(pathText)) {
				int index=line.IndexOf(": ",StringComparison.Ordinal);
				if(index<=0) {
					continue;
				}
				string key=line.Substring(0,index);
				string value=line.Substring(index+2);
				if(dict.ContainsKey(key)) {
					dict[key]+="; "+value;
				}
				else {
					dict[key]=value;
				}
			}
			return dict;
		}

		///<summary>Rewrites manifest.txt (one tab-separated line per picture) and index.html (the pictures side by side, one row per scene and size)
		///from every .txt under folder, so a run that records one mode keeps the others in both.</summary>
		public static void WriteManifestAndIndex(string folder) {
			Dictionary<string,Dictionary<string,string>> dictSidecars=new Dictionary<string,Dictionary<string,string>>();
			foreach(DrawingMode engine in LegacyRecorder.ArrayEngines) {
				string folderEngine=Path.Combine(folder,engine.ToString());
				if(!Directory.Exists(folderEngine)) {
					continue;
				}
				foreach(string pathText in Directory.GetFiles(folderEngine,"*.txt",SearchOption.AllDirectories)) {
					string pathRelative=pathText.Substring(folder.TrimEnd('\\').Length+1).Replace('\\','/');
					dictSidecars[pathRelative.Substring(0,pathRelative.Length-4)+".png"]=ReadSidecar(pathText);
				}
			}
			WriteManifest(folder,dictSidecars);
			WriteIndex(folder,dictSidecars);
		}

		private static void WriteManifest(string folder,Dictionary<string,Dictionary<string,string>> dictSidecars) {
			string[] arrayColumns={ "Scene","Group","Requested mode","Started mode","Size","Colours","Bitmap","Alpha","Colours in the picture",
				"Pixels SHA-256","Repeatable","Failed" };
			List<string> listLines=new List<string>();
			listLines.Add("# Today's tooth chart, recorded by ToothChartImageTests.LegacyRecorder (package TC-0, docs/specs/wpf-tooth-chart-plan.md).");
			listLines.Add("# One line per picture, from the .txt next to it, which also names the device or pixel format, the adapters, the driver and the "
				+"Windows build.");
			listLines.Add("File\t"+string.Join("\t",arrayColumns));
			foreach(string pathPng in dictSidecars.Keys.OrderBy(x => x,StringComparer.Ordinal)) {
				Dictionary<string,string> dict=dictSidecars[pathPng];
				listLines.Add(pathPng+"\t"+string.Join("\t",arrayColumns.Select(x => dict.TryGetValue(x,out string value)?value:"")));
			}
			File.WriteAllLines(Path.Combine(folder,"manifest.txt"),listLines,new UTF8Encoding(false));
		}

		private static void WriteIndex(string folder,Dictionary<string,Dictionary<string,string>> dictSidecars) {
			StringBuilder sb=new StringBuilder();
			sb.Append("<!doctype html>\r\n<html lang=\"en\">\r\n<head>\r\n<meta charset=\"utf-8\">\r\n");
			sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\r\n");
			sb.Append("<title>Tooth chart recordings</title>\r\n<style>\r\n");
			sb.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:16px;background:#fff;color:#111}\r\n");
			sb.Append("table{border-collapse:collapse}td,th{border:1px solid #bbb;padding:4px;vertical-align:top;font-size:12px;text-align:left}\r\n");
			sb.Append("img{display:block;max-width:100%;height:auto}.cap{color:#555}.name{font-weight:bold}.warn{color:#b00}\r\n");
			sb.Append("</style>\r\n</head>\r\n<body>\r\n<h1>Today's tooth chart, as recorded</h1>\r\n");
			sb.Append("<p>The pictures of the DirectX, OpenGL and Simple 2D charts for every synthetic scene (package TC-0 of the WPF tooth chart plan). "
				+"They are the \"before\" column of the owner galleries and the target of the parity probes. Under each picture: the mode that really "
				+"drew it, the pixel format of the returned bitmap and its alpha. Perio is drawn by DirectX only. DirectX was recorded without "
				+"multisampling, so its edges are harder than on a computer that uses four samples: with them the same scene gave other pixels on "
				+"every draw, and a later recording could not be compared.</p>\r\n");
			List<ChartScene> listScenes=ChartScenes.GetAll();
			sb.Append("<p><label>Group: <select id=\"group\"><option value=\"\">all</option>");
			foreach(string group in listScenes.Select(x => x.Group).Distinct()) {
				sb.Append("<option>"+WebUtility.HtmlEncode(group)+"</option>");
			}
			sb.Append("</select></label></p>\r\n<table>\r\n<thead><tr><th>Scene</th><th>Size and colours</th>");
			foreach(DrawingMode engine in LegacyRecorder.ArrayEngines) {
				sb.Append("<th>"+engine+"</th>");
			}
			sb.Append("</tr></thead>\r\n");
			foreach(ChartScene scene in listScenes) {
				List<ChartSize> listSizes=ChartScenes.GetSizes(scene);
				sb.Append("<tbody data-group=\""+WebUtility.HtmlEncode(scene.Group)+"\" id=\""+scene.Name+"\">\r\n");
				for(int i=0;i<listSizes.Count;i++) {
					sb.Append("<tr>");
					if(i==0) {
						sb.Append("<td rowspan=\""+listSizes.Count+"\"><div class=\"name\">"+scene.Name+"</div><div class=\"cap\">"
							+WebUtility.HtmlEncode(scene.Group)+"</div><div>"+WebUtility.HtmlEncode(scene.Description)+"</div></td>");
					}
					sb.Append("<td>"+listSizes[i].FolderName+"<div class=\"cap\">"+WebUtility.HtmlEncode(GetColorSetText(listSizes[i].ColorSet))+"</div></td>");
					foreach(DrawingMode engine in LegacyRecorder.ArrayEngines) {
						string pathPng=engine+"/"+listSizes[i].FolderName+"/"+scene.Name+".png";
						if(!dictSidecars.TryGetValue(pathPng,out Dictionary<string,string> dict)) {
							sb.Append("<td class=\"cap\">"+(scene.Kind==ChartSceneKind.Perio && engine!=DrawingMode.DirectX?"DirectX only":"not recorded")+"</td>");
							continue;
						}
						sb.Append("<td>");
						if(dict.ContainsKey("Failed")) {
							sb.Append("<div class=\"warn\">Failed: "+WebUtility.HtmlEncode(dict["Failed"])+"</div>");
						}
						else {
							sb.Append("<img loading=\"lazy\" src=\""+pathPng+"\" width=\""+listSizes[i].Size.Width+"\" height=\""+listSizes[i].Size.Height
								+"\" alt=\""+scene.Name+" in "+engine+"\">");
						}
						string started=dict.TryGetValue("Started mode",out string value)?value:"?";
						string bitmap=dict.TryGetValue("Bitmap",out value)?value:"";
						string alpha=dict.TryGetValue("Alpha",out value)?value:"";
						bool isOtherMode=started!=engine.ToString();
						sb.Append("<div class=\""+(isOtherMode?"warn":"cap")+"\">drawn by "+WebUtility.HtmlEncode(started)+"; "+WebUtility.HtmlEncode(bitmap)+"; "
							+WebUtility.HtmlEncode(alpha)+"</div></td>");
					}
					sb.Append("</tr>\r\n");
				}
				sb.Append("</tbody>\r\n");
			}
			sb.Append("</table>\r\n<script>\r\n");
			sb.Append("document.getElementById('group').addEventListener('change',function(){var g=this.value;"
				+"document.querySelectorAll('tbody[data-group]').forEach(function(b){b.style.display=(!g||b.getAttribute('data-group')===g)?'':'none';});});\r\n");
			sb.Append("</script>\r\n</body>\r\n</html>\r\n");
			File.WriteAllText(Path.Combine(folder,"index.html"),sb.ToString(),new UTF8Encoding(false));
		}
		#endregion Manifest and index

		///<summary>What happened: counts, notes and failures.</summary>
		public string GetSummary() {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(ListWritten.Count+" pictures written to "+_folderOut+", "+ListFailures.Count+" failed.\r\n");
			foreach(string note in ListNotes) {
				stringBuilder.Append("Note: "+note+"\r\n");
			}
			foreach(string failure in ListFailures) {
				stringBuilder.Append("Failed: "+failure+"\r\n");
			}
			return stringBuilder.ToString();
		}

		#region Native
		[StructLayout(LayoutKind.Sequential)]
		private struct PIXELFORMATDESCRIPTOR {
			public ushort nSize;
			public ushort nVersion;
			public uint dwFlags;
			public byte iPixelType;
			public byte cColorBits;
			public byte cRedBits;
			public byte cRedShift;
			public byte cGreenBits;
			public byte cGreenShift;
			public byte cBlueBits;
			public byte cBlueShift;
			public byte cAlphaBits;
			public byte cAlphaShift;
			public byte cAccumBits;
			public byte cAccumRedBits;
			public byte cAccumGreenBits;
			public byte cAccumBlueBits;
			public byte cAccumAlphaBits;
			public byte cDepthBits;
			public byte cStencilBits;
			public byte cAuxBuffers;
			public byte iLayerType;
			public byte bReserved;
			public uint dwLayerMask;
			public uint dwVisibleMask;
			public uint dwDamageMask;
		}

		[DllImport("gdi32.dll",SetLastError=true)]
		private static extern int DescribePixelFormat(IntPtr hdc,int iPixelFormat,uint nBytes,ref PIXELFORMATDESCRIPTOR ppfd);

		[DllImport("user32.dll")]
		private static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern int ReleaseDC(IntPtr hWnd,IntPtr hDC);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("opengl32.dll")]
		private static extern IntPtr glGetString(uint name);
		#endregion Native
	}
}
