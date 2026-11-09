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
	///<summary>What the recorder does with the smoothed DirectX pictures of the gallery (LegacyRecorder.ListSmoothed).</summary>
	public enum SmoothedMode {
		///<summary>The default: the baseline pictures, then the smoothed ones.</summary>
		Also,
		///<summary>The baseline pictures only; the smoothed folder is left as it is.</summary>
		None,
		///<summary>The smoothed pictures only; the baseline pictures are left as they are, and the manifest and index are rewritten.</summary>
		Only,
	}

	///<summary>One smoothed DirectX picture of the gallery: a scene at one of its sizes.</summary>
	public class SmoothedPicture {
		public string SceneName;
		public ChartSize ChartSize;

		public SmoothedPicture(string sceneName,ChartSize chartSize) {
			SceneName=sceneName;
			ChartSize=chartSize;
		}

		///<summary>The picture's path in the smoothed folder, for example "410x307/restored-adult.png".</summary>
		public string PathRelative {
			get {
				return ChartSize.FolderName+"/"+SceneName+".png";
			}
		}
	}

	///<summary>The recorder of the legacy chart pictures and the check of a re-recording.
	///
	///Record (the lead's run, L-1; from the repository root, after a build of OpenDental.Local.slnf through the build lane) with the lane's vstest
	///of Visual Studio Build Tools 2026, which
	///  "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -version [18.0,19.0) -find Common7\IDE\Extensions\TestPlatform\vstest.console.exe
	///finds:
	///  set OPMS_RECORD_LEGACY_CHART=1
	///  "&lt;that vstest.console.exe&gt;" tests\ToothChartImageTests\bin\Debug\ToothChartImageTests.dll /Platform:x86 /Tests:RecordLegacyChart /logger:console;verbosity=detailed
	///(or through the lane itself: odbuild.ps1 -Root . -TestOnly -Test tests\ToothChartImageTests\bin\Debug\ToothChartImageTests.dll
	///-TestFilter Name=RecordLegacyChart, which prints only the result lines, not the recorder's summary).
	///The pictures go to tests\ToothChartImageTests\Baselines\legacy\&lt;mode&gt;\&lt;width&gt;x&lt;height&gt;\&lt;scene&gt;.png, each with a .txt
	///next to it, plus manifest.txt and index.html for the whole folder. DirectX uses the first standard device format that works, with
	///multisampling turned off so that the same scene gives the same pixels (see FindDirectXFormat); each .txt names both formats, the adapter that
	///drew the picture and whether a second picture of the same state was identical. Then a few DirectX pictures for the gallery only (ListSmoothed)
	///are drawn with the standard format as it is, multisampled as the program draws them, into Baselines\legacy-smoothed\&lt;width&gt;x&lt;height&gt;\
	///(the output folder's name plus "-smoothed"); they are never compared. Optional: OPMS_RECORD_LEGACY_CHART_OUT (another folder),
	///OPMS_RECORD_LEGACY_CHART_ENGINES (for example "DirectX"), OPMS_RECORD_LEGACY_CHART_SCENES (scene names, for a quick look),
	///OPMS_RECORD_LEGACY_CHART_DIRECTX_FORMAT (a device format string instead of the first standard format that works; then there was no search,
	///so no smoothed pictures are drawn and their folder is left as it is) and OPMS_RECORD_LEGACY_CHART_SMOOTHED ("0": no smoothed pictures;
	///"only": the smoothed pictures alone).
	///
	///Check a re-recording (after a change of the chart's code, on the same PC): record into a scratch folder with OPMS_RECORD_LEGACY_CHART_OUT
	///(and OPMS_RECORD_LEGACY_CHART_SMOOTHED=0), then
	///  set OPMS_COMPARE_LEGACY_CHART=&lt;that folder&gt;
	///  "&lt;the same vstest.console.exe&gt;" tests\ToothChartImageTests\bin\Debug\ToothChartImageTests.dll /Platform:x86 /Tests:CompareLegacyRecording /logger:console;verbosity=detailed
	///which fails unless every baseline picture of the compared modes and scenes (OPMS_RECORD_LEGACY_CHART_ENGINES and _SCENES, all when unset) is
	///in that folder, has the same bytes as its baseline, and its .txt has the same "Pixels SHA-256". The PNG bytes also depend on the framework's
	///DeflateStream (PngWriter), so a check across the x64 or .NET 10 switch (TC-10, S3b-5) sets OPMS_COMPARE_LEGACY_CHART_PIXELS_ONLY=1: the decoded
	///pixels and the .txt hashes are compared, not the file bytes.</summary>
	[TestClass]
	public class LegacyRecorder {
		public const string EnvRecord="OPMS_RECORD_LEGACY_CHART";
		public const string EnvOut="OPMS_RECORD_LEGACY_CHART_OUT";
		public const string EnvEngines="OPMS_RECORD_LEGACY_CHART_ENGINES";
		public const string EnvScenes="OPMS_RECORD_LEGACY_CHART_SCENES";
		public const string EnvDirectXFormat="OPMS_RECORD_LEGACY_CHART_DIRECTX_FORMAT";
		public const string EnvSmoothed="OPMS_RECORD_LEGACY_CHART_SMOOTHED";
		public const string EnvCompare="OPMS_COMPARE_LEGACY_CHART";
		public const string EnvComparePixelsOnly="OPMS_COMPARE_LEGACY_CHART_PIXELS_ONLY";
		///<summary>The modes in the order they are recorded.</summary>
		public static readonly DrawingMode[] ArrayEngines={ DrawingMode.DirectX,DrawingMode.OpenGL,DrawingMode.Simple2D };
		///<summary>The smoothed DirectX pictures of the gallery (owner answer of 2026-11-08): the real look of today's chart on a computer whose
		///standard format multisamples, next to the unsmoothed baseline of the same scene and size. Whole mouths at both chart sizes, a bridge, a
		///denture, a root canal with post and crown, the empty chart, the big window and the perio chart.</summary>
		public static readonly List<SmoothedPicture> ListSmoothed=new List<SmoothedPicture> {
			new SmoothedPicture("empty-universal",ChartScenes.SizeModule),
			new SmoothedPicture("restored-adult",ChartScenes.SizeModule),
			new SmoothedPicture("restored-adult",ChartScenes.SizePrintout),
			new SmoothedPicture("treatment-plan",ChartScenes.SizeModule),
			new SmoothedPicture("treatment-plan",ChartScenes.SizePrintout),
			new SmoothedPicture("bridge-anterior",ChartScenes.SizeModule),
			new SmoothedPicture("denture-upper-dark",ChartScenes.SizeModule),
			new SmoothedPicture("rct-with-post-and-crown",ChartScenes.SizeModule),
			new SmoothedPicture("size-big-window",ChartScenes.SizeBigWindow),
			new SmoothedPicture("perio-full-exam",ChartScenes.SizePerio),
		};

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
			List<string> listNames=ParseSceneNames(Environment.GetEnvironmentVariable(EnvScenes));
			bool isAllScenes=listNames==null;
			if(!isAllScenes) {
				listScenes=listNames.Select(x => ChartScenes.Get(x)).ToList();
			}
			SmoothedMode smoothedMode=ParseSmoothed(Environment.GetEnvironmentVariable(EnvSmoothed));
			LegacyRecording legacyRecording=StaRunner.Run(() => LegacyRecording.Record(folderOut,listEngines,listScenes,isAllScenes,
				Environment.GetEnvironmentVariable(EnvDirectXFormat),smoothedMode));
			Console.WriteLine(legacyRecording.GetSummary());
			Assert.AreEqual(0,legacyRecording.ListFailures.Count,"Some pictures could not be made:\r\n"+string.Join("\r\n",legacyRecording.ListFailures));
		}

		[TestMethod]
		public void CompareLegacyRecording() {
			string folderNew=Environment.GetEnvironmentVariable(EnvCompare);
			if(string.IsNullOrWhiteSpace(folderNew)) {
				Assert.Inconclusive("Set "+EnvCompare+" to a folder recorded with "+EnvOut+" to compare it with Baselines\\legacy.");
			}
			List<DrawingMode> listEngines=ParseEngines(Environment.GetEnvironmentVariable(EnvEngines));
			List<string> listNames=ParseSceneNames(Environment.GetEnvironmentVariable(EnvScenes));
			listNames?.ForEach(x => ChartScenes.Get(x));//a misspelt name throws here, rather than comparing nothing
			bool isPixelsOnly=Environment.GetEnvironmentVariable(EnvComparePixelsOnly)=="1";
			LegacyComparison legacyComparison=LegacyComparison.Compare(TestPaths.BaselinesLegacy,folderNew,listEngines,listNames,isPixelsOnly);
			Console.WriteLine(legacyComparison.GetSummary());
			Assert.IsTrue(legacyComparison.IsPass,legacyComparison.GetSummary());
		}

		///<summary>"DirectX,OpenGL" gives those modes in recording order; empty gives all three.</summary>
		public static List<DrawingMode> ParseEngines(string engines) {
			if(string.IsNullOrWhiteSpace(engines)) {
				return ArrayEngines.ToList();
			}
			List<DrawingMode> listEngines=engines.Split(',').Select(x => (DrawingMode)Enum.Parse(typeof(DrawingMode),x.Trim(),true)).ToList();
			return ArrayEngines.Where(x => listEngines.Contains(x)).ToList();
		}

		///<summary>"a, b" gives the scene names a and b; empty gives null, which means every scene.</summary>
		public static List<string> ParseSceneNames(string scenes) {
			if(string.IsNullOrWhiteSpace(scenes)) {
				return null;
			}
			return scenes.Split(',').Select(x => x.Trim()).Where(x => x!="").ToList();
		}

		///<summary>"0" or "no" gives None, "only" gives Only, anything else (unset, "1") gives Also.</summary>
		public static SmoothedMode ParseSmoothed(string smoothed) {
			string value=(smoothed??"").Trim().ToLowerInvariant();
			if(value=="0" || value=="no") {
				return SmoothedMode.None;
			}
			if(value=="only") {
				return SmoothedMode.Only;
			}
			return SmoothedMode.Also;
		}

		///<summary>The folder of the smoothed gallery pictures of a recording folder: its path plus "-smoothed", so Baselines\legacy gives
		///Baselines\legacy-smoothed, beside it and never inside it.</summary>
		public static string GetSmoothedFolder(string folderOut) {
			return folderOut.TrimEnd('\\','/')+"-smoothed";
		}
	}

	///<summary>One run of the recorder.</summary>
	public class LegacyRecording {
		public List<string> ListWritten=new List<string>();
		public List<string> ListWrittenSmoothed=new List<string>();
		public List<string> ListFailures=new List<string>();
		public List<string> ListNotes=new List<string>();
		private string _folderOut;
		private string _environmentText;
		private string _directXFormat;
		///<summary>The format the standard search found, before multisampling was turned off; or why there is none.</summary>
		private string _directXFormatSearched;
		///<summary>True when the DirectX format was given (OPMS_RECORD_LEGACY_CHART_DIRECTX_FORMAT) rather than searched.</summary>
		private bool _isDirectXFormatGiven;
		private Form _formHost;

		///<summary>Draws every scene in every mode at every size of the scene, writes a PNG and a .txt for each, then the smoothed DirectX pictures
		///(per smoothedMode), then rewrites manifest.txt and index.html from all the .txt files in folderOut and its smoothed folder. When
		///isAllScenes, the old files of the recorded modes are deleted before the first picture, once the recorder's window is known to be visible,
		///and the old smoothed pictures once CanRecordSmoothed says they will be drawn again. Runs on an STA thread.</summary>
		public static LegacyRecording Record(string folderOut,List<DrawingMode> listEngines,List<ChartScene> listScenes,bool isAllScenes,
			string directXFormatGiven,SmoothedMode smoothedMode=SmoothedMode.Also)
		{
			LegacyRecording legacyRecording=new LegacyRecording();
			legacyRecording._folderOut=folderOut;
			legacyRecording.Run(listEngines,listScenes,isAllScenes,directXFormatGiven,smoothedMode);
			return legacyRecording;
		}

		private void Run(List<DrawingMode> listEngines,List<ChartScene> listScenes,bool isAllScenes,string directXFormatGiven,SmoothedMode smoothedMode) {
			Directory.CreateDirectory(_folderOut);
			_environmentText=ReadEnvironment();
			using(_formHost=CreateHostForm()) {
				_formHost.Show();
				_formHost.Activate();
				Application.DoEvents();
				if(!_formHost.Visible || !IsWindowVisible(_formHost.Handle)) {
					//Checked before anything is deleted, so a run without a desktop leaves the recording as it is.
					throw new AssertInconclusiveException("The recorder's window is not visible: no interactive desktop.");
				}
				if(isAllScenes && smoothedMode!=SmoothedMode.Only) {
					foreach(DrawingMode engine in listEngines) {
						string folderEngine=Path.Combine(_folderOut,engine.ToString());
						if(Directory.Exists(folderEngine)) {
							Directory.Delete(folderEngine,true);
						}
					}
				}
				bool isDirectX=listEngines.Contains(DrawingMode.DirectX);
				if(isDirectX) {
					_directXFormat=FindDirectXFormat(directXFormatGiven);
				}
				if(smoothedMode!=SmoothedMode.Only) {
					foreach(DrawingMode engine in listEngines) {
						RecordEngine(engine,listScenes);
					}
				}
				if(smoothedMode!=SmoothedMode.None) {
					if(isDirectX) {
						RecordSmoothed(listScenes,isAllScenes);
					}
					else {
						ListNotes.Add("No smoothed pictures: they are DirectX pictures, and DirectX is not recorded in this run.");
					}
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
				ToothChartWrapper wrapper=CreateWrapper(engine,chartSize.Size,_directXFormat);
				try {
					ToothChartRelay relay=new ToothChartRelay();
					relay.SetToothChartWrapper(wrapper);
					foreach(ChartScene scene in listForSize) {
						RecordScene(wrapper,relay,engine,scene,chartSize,false);
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
					ToothChartWrapper wrapper=CreateWrapper(engine,chartSize.Size,_directXFormat);
					try {
						ToothChartRelay relay=new ToothChartRelay();
						relay.SetToothChartWrapper(wrapper);
						RecordScene(wrapper,relay,engine,scene,chartSize,false);
					}
					finally {
						DisposeWrapper(wrapper);
					}
				}
			}
		}

		///<summary>The smoothed DirectX pictures of LegacyRecorder.ListSmoothed whose scene is in listScenes, each on a new wrapper with the standard
		///search's format as it is (multisampled), as the program saves it for a computer with no format saved. Only when CanRecordSmoothed says so;
		///otherwise a note says why and the smoothed folder is left as it is. The folder is emptied (when isAllScenes) only after that check.</summary>
		private void RecordSmoothed(List<ChartScene> listScenes,bool isAllScenes) {
			if(!CanRecordSmoothed(_isDirectXFormatGiven,_directXFormatSearched,_directXFormat,out string reasonSkip)) {
				ListNotes.Add("No smoothed pictures, and the smoothed folder is left as it is: "+reasonSkip);
				return;
			}
			string folderSmoothed=LegacyRecorder.GetSmoothedFolder(_folderOut);
			if(isAllScenes && Directory.Exists(folderSmoothed)) {
				Directory.Delete(folderSmoothed,true);
			}
			foreach(SmoothedPicture smoothedPicture in LegacyRecorder.ListSmoothed) {
				ChartScene scene=listScenes.FirstOrDefault(x => x.Name==smoothedPicture.SceneName);
				if(scene==null) {
					continue;
				}
				ToothChartWrapper wrapper=CreateWrapper(DrawingMode.DirectX,smoothedPicture.ChartSize.Size,_directXFormatSearched);
				try {
					ToothChartRelay relay=new ToothChartRelay();
					relay.SetToothChartWrapper(wrapper);
					RecordScene(wrapper,relay,DrawingMode.DirectX,scene,smoothedPicture.ChartSize,true);
				}
				finally {
					DisposeWrapper(wrapper);
				}
			}
		}

		///<summary>Whether this run can draw the smoothed pictures, decided from the DirectX formats alone (no device): formatUsed is the baseline's
		///format (null when DirectX cannot start), formatSearched the standard search's own and isFormatGiven whether formatUsed came from
		///OPMS_RECORD_LEGACY_CHART_DIRECTX_FORMAT. They are drawn only with a device format the standard search found that differs from the
		///baseline's, which is the multisampled format the program saves for a computer with no format saved. Not when DirectX cannot start, not when
		///the format was given (there was no search: formatSearched then names no format, and a smoothed picture drawn with it would be a fallback's
		///under "DirectX, smoothed"), not when formatSearched is no device format, and not when the search's format is the baseline's (it does not
		///multisample, or does not start without multisampling): the pictures would be the baseline's. reasonSkip says why not, or is null.</summary>
		public static bool CanRecordSmoothed(bool isFormatGiven,string formatSearched,string formatUsed,out string reasonSkip) {
			reasonSkip=null;
			if(formatUsed==null) {
				reasonSkip="DirectX cannot start.";
			}
			else if(isFormatGiven) {
				reasonSkip="the DirectX format was given ("+LegacyRecorder.EnvDirectXFormat+"="+formatUsed+"), so there is no standard search's format to draw "
					+"them with.";
			}
			else if(formatSearched==null || formatSearched.IndexOf(';')<0) {
				reasonSkip="the standard search found no device format ("+(formatSearched??"none")+").";
			}
			else if(formatSearched==formatUsed) {
				reasonSkip="the DirectX format used ("+formatUsed+") is the standard search's own, so they would be the baseline's.";
			}
			return reasonSkip==null;
		}

		///<summary>Draws the scene and writes its PNG and .txt: into &lt;folderOut&gt;\&lt;engine&gt;\&lt;size&gt;, or, for a smoothed picture,
		///into the smoothed folder's &lt;size&gt;. A smoothed picture that DirectX did not draw is a failure, and no PNG is left for it.</summary>
		private void RecordScene(ToothChartWrapper wrapper,ToothChartRelay relay,DrawingMode engine,ChartScene scene,ChartSize chartSize,bool isSmoothed) {
			string folder;
			string pathRelative;
			if(isSmoothed) {
				string folderSmoothed=LegacyRecorder.GetSmoothedFolder(_folderOut);
				folder=Path.Combine(folderSmoothed,chartSize.FolderName);
				pathRelative=Path.GetFileName(folderSmoothed)+"/"+chartSize.FolderName+"/"+scene.Name+".png";
			}
			else {
				folder=Path.Combine(_folderOut,engine.ToString(),chartSize.FolderName);
				pathRelative=engine+"/"+chartSize.FolderName+"/"+scene.Name+".png";
			}
			Directory.CreateDirectory(folder);
			string pathPng=Path.Combine(folder,scene.Name+".png");
			string pathText=Path.Combine(folder,scene.Name+".txt");
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
				if(isSmoothed) {
					listLines.Add("DirectX standard search: "+_directXFormatSearched+" (used as it is, multisampled, as the program saves it for this computer)");
					listLines.Add("Smoothed: yes, for the gallery only. Never compared: with multisampling a second draw of the same scene can give other pixels "
						+"along the edges. The baseline of this scene and size, without multisampling, is in the recording folder.");
				}
				else {
					listLines.Add("DirectX standard search: "+_directXFormatSearched+(!_isDirectXFormatGiven && _directXFormatSearched!=_directXFormat
						?" (multisampling turned off: with it a draw is not repeatable on every driver)":""));
				}
			}
			try {
				if(isSmoothed && wrapper.DrawMode!=DrawingMode.DirectX) {
					//The catch below fails the run and deletes the PNG: a fallback's picture must never stand under "DirectX, smoothed" in the gallery.
					throw new Exception("DirectX did not start with "+_directXFormatSearched+" (the chart fell back to "+wrapper.DrawMode
						+"), and a smoothed picture is DirectX's only.");
				}
				_formHost.Activate();
				Application.DoEvents();
				ChartScenes.Prepare(relay,scene,chartSize.ColorSet);
				using Bitmap bitmap=relay.GetBitmap();
				if(bitmap==null) {
					throw new Exception("GetBitmap returned nothing.");
				}
				BitmapFacts bitmapFacts=ChartImageCompare.DescribeBitmap(bitmap);
				if(wrapper.DrawMode==DrawingMode.DirectX) {
					listLines.Add("DirectX adapter: "+GetDirectXAdapter(wrapper));
				}
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
				if(!isRepeatable && !isSmoothed) {
					ListNotes.Add(pathRelative+" is not repeatable: a second GetBitmap gave other pixels.");
				}
				if(wrapper.DrawMode!=engine) {
					ListNotes.Add(pathRelative+" was drawn by "+wrapper.DrawMode+": "+engine+" did not start.");
				}
				if(isSmoothed) {
					ListWrittenSmoothed.Add(pathRelative+(isRepeatable?" (repeatable)":" (not repeatable)"));
				}
				else {
					ListWritten.Add(pathRelative);
				}
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
		///0) and with the given DirectX device format, then started in engine. The fallback may start another mode: the .txt says which.</summary>
		private ToothChartWrapper CreateWrapper(DrawingMode engine,Size size,string directXFormat) {
			_formHost.ClientSize=size;
			ToothChartWrapper wrapper=new ToothChartWrapper();
			wrapper.Location=new Point(0,0);
			wrapper.Size=size;
			wrapper.UseHardware=false;
			wrapper.PreferredPixelFormatNumber=0;
			if(engine==DrawingMode.DirectX) {
				wrapper.DeviceFormat=new ToothChartDirectX.DirectXDeviceFormat(directXFormat??"invalid");
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
		///FormOpenDental makes for a computer with no format saved, ToothChartWrapper.GetPreferredDirectXFormat, formerly in FormGraphics) with multisampling turned off. With
		///multisampling a draw is not repeatable on every driver: on the build PC (2026-11-08) every GetBitmap of the same scene with four samples gave
		///other pixels along the edges, so a re-recording could never be compared byte for byte. Without it the same scene gives the same pixels.
		///Null when DirectX cannot start.</summary>
		private string FindDirectXFormat(string directXFormatGiven) {
			if(!string.IsNullOrWhiteSpace(directXFormatGiven)) {
				ListNotes.Add("DirectX device format given: "+directXFormatGiven);
				_isDirectXFormatGiven=true;
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

		///<summary>ToothChartWrapper.TestDirectXFormat (formerly in FormGraphics): an invisible wrapper on the host form starts DirectX with the format and returns a picture.</summary>
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

		///<summary>The adapter that drew the DirectX picture, read from the chart's own device as the engine created it
		///(ToothChartDirectX.DirectXDeviceFormat.CreateDevice): the device's adapter index and device type (IDirect3DDevice9::GetCreationParameters),
		///then that adapter's description, driver version and file, display and PCI ids (IDirect3D9::GetAdapterIdentifier, on the device's own
		///IDirect3D9). For example "0 (Hardware device): NVIDIA RTX A1000, driver 32.0.15.9671 (nvldumd.dll), display \\.\DISPLAY1, PCI 10DE:25B0".
		///The driver file is the user-mode driver of the host's bitness: the x86 test host records the 32-bit one (nvldumd.dll on that adapter), a
		///64-bit host the 64-bit one (nvldumdx.dll). Read through reflection, because this project does not reference SharpDX (see WrapperTests).</summary>
		public static string GetDirectXAdapter(ToothChartWrapper wrapper) {
			ToothChartDirectX toothChartDirectX=wrapper.Controls.OfType<ToothChartDirectX>().FirstOrDefault();
			if(toothChartDirectX==null) {
				return "unknown (no DirectX control)";
			}
			object device=typeof(ToothChartDirectX).GetField("device").GetValue(toothChartDirectX);
			if(device==null) {
				return "unknown (no device)";
			}
			try {
				object creationParameters=device.GetType().GetProperty("CreationParameters").GetValue(device);
				Type typeCreation=creationParameters.GetType();
				int adapterIndex=(int)typeCreation.GetField("AdapterOrdinal").GetValue(creationParameters);
				string deviceType=typeCreation.GetField("DeviceType").GetValue(creationParameters).ToString();
				object direct3D=device.GetType().GetProperty("Direct3D").GetValue(device);//AddRef'd by GetDirect3D; released below
				try {
					object adapterDetails=direct3D.GetType().GetMethod("GetAdapterIdentifier",new[] { typeof(int) }).Invoke(direct3D,new object[] { adapterIndex });
					Type typeDetails=adapterDetails.GetType();
					string description=(string)typeDetails.GetField("Description").GetValue(adapterDetails);
					string driver=(string)typeDetails.GetField("Driver").GetValue(adapterDetails);
					string deviceName=(string)typeDetails.GetField("DeviceName").GetValue(adapterDetails);
					int vendorId=(int)typeDetails.GetField("VendorId").GetValue(adapterDetails);
					int deviceId=(int)typeDetails.GetField("DeviceId").GetValue(adapterDetails);
					object driverVersion=typeDetails.GetProperty("DriverVersion").GetValue(adapterDetails);
					return adapterIndex+" ("+deviceType+" device): "+description.Trim()+", driver "+driverVersion+" ("+driver.Trim()+"), display "+deviceName.Trim()
						+", PCI "+vendorId.ToString("X4")+":"+deviceId.ToString("X4");
				}
				finally {
					(direct3D as IDisposable)?.Dispose();
				}
			}
			catch(Exception ex) {
				Exception exInner=ex.InnerException??ex;
				return "unknown ("+exInner.GetType().Name+": "+exInner.Message.Replace("\r"," ").Replace("\n"," ")+")";
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

		///<summary>The Windows build, the display adapters with their drivers and the screen DPI: the lines every .txt ends with. The adapter that drew
		///a DirectX picture is named on its own line (GetDirectXAdapter); these lines list every adapter of the computer.</summary>
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
		///from every .txt under folder, so a run that records one mode keeps the others in both. The smoothed pictures of the folder's smoothed folder
		///(LegacyRecorder.GetSmoothedFolder) get a column of the index next to DirectX, when there are any; the manifest lists the compared pictures
		///only.</summary>
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
			//Smoothed pictures, keyed "<size>/<scene>.png" as in their folder.
			Dictionary<string,Dictionary<string,string>> dictSmoothed=new Dictionary<string,Dictionary<string,string>>();
			string folderSmoothed=LegacyRecorder.GetSmoothedFolder(folder);
			if(Directory.Exists(folderSmoothed)) {
				foreach(string pathText in Directory.GetFiles(folderSmoothed,"*.txt",SearchOption.AllDirectories)) {
					string pathRelative=pathText.Substring(folderSmoothed.Length+1).Replace('\\','/');
					dictSmoothed[pathRelative.Substring(0,pathRelative.Length-4)+".png"]=ReadSidecar(pathText);
				}
			}
			WriteManifest(folder,dictSidecars);
			WriteIndex(folder,dictSidecars,Path.GetFileName(folderSmoothed),dictSmoothed);
		}

		private static void WriteManifest(string folder,Dictionary<string,Dictionary<string,string>> dictSidecars) {
			string[] arrayColumns={ "Scene","Group","Requested mode","Started mode","Size","Colours","Bitmap","Alpha","Colours in the picture",
				"Pixels SHA-256","Repeatable","Device or pixel format","DirectX adapter","Failed" };
			List<string> listLines=new List<string>();
			listLines.Add("# Today's tooth chart, recorded by ToothChartImageTests.LegacyRecorder (package TC-0, docs/specs/wpf-tooth-chart-plan.md).");
			listLines.Add("# One line per picture, from the .txt next to it, which also names the OpenGL renderer, every adapter of the computer with its "
				+"driver, and the Windows build. \"DirectX adapter\" is the adapter that drew a DirectX picture.");
			listLines.Add("File\t"+string.Join("\t",arrayColumns));
			foreach(string pathPng in dictSidecars.Keys.OrderBy(x => x,StringComparer.Ordinal)) {
				Dictionary<string,string> dict=dictSidecars[pathPng];
				listLines.Add(pathPng+"\t"+string.Join("\t",arrayColumns.Select(x => dict.TryGetValue(x,out string value)?value:"")));
			}
			File.WriteAllLines(Path.Combine(folder,"manifest.txt"),listLines,new UTF8Encoding(false));
		}

		private static void WriteIndex(string folder,Dictionary<string,Dictionary<string,string>> dictSidecars,string folderNameSmoothed,
			Dictionary<string,Dictionary<string,string>> dictSmoothed)
		{
			bool hasSmoothed=dictSmoothed.Count>0;
			StringBuilder sb=new StringBuilder();
			sb.Append("<!doctype html>\r\n<html lang=\"en\">\r\n<head>\r\n<meta charset=\"utf-8\">\r\n");
			sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\r\n");
			sb.Append("<title>Tooth chart recordings</title>\r\n<style>\r\n");
			sb.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:16px;background:#fff;color:#111}\r\n");
			sb.Append("table{border-collapse:collapse}td,th{border:1px solid #bbb;padding:4px;vertical-align:top;font-size:12px;text-align:left}\r\n");
			sb.Append("img{display:block;max-width:100%;height:auto}.cap{color:#555}.name{font-weight:bold}.warn{color:#b00}.smooth{color:#05a}\r\n");
			sb.Append("</style>\r\n</head>\r\n<body>\r\n<h1>Today's tooth chart, as recorded</h1>\r\n");
			sb.Append("<p>The pictures of the DirectX, OpenGL and Simple 2D charts for every synthetic scene (package TC-0 of the WPF tooth chart plan). "
				+"They are the \"before\" column of the owner galleries and the target of the parity probes. Under each picture: the mode that really "
				+"drew it, the pixel format of the returned bitmap and its alpha, and for DirectX the adapter that drew it. Perio is drawn by DirectX "
				+"only. DirectX was recorded without multisampling, so its edges are harder than on a computer that uses four samples: with them the "
				+"same scene gave other pixels on every draw, and a later recording could not be compared.</p>\r\n");
			if(hasSmoothed) {
				sb.Append("<p class=\"smooth\">The column \"DirectX, smoothed\" shows a few scenes as the program draws them on this computer: with the "
					+"standard search's own format, multisampled. They are for looking only (folder "+WebUtility.HtmlEncode(folderNameSmoothed)
					+"), never compared; the unsmoothed picture to their left is the baseline.</p>\r\n");
			}
			List<ChartScene> listScenes=ChartScenes.GetAll();
			sb.Append("<p><label>Group: <select id=\"group\"><option value=\"\">all</option>");
			foreach(string group in listScenes.Select(x => x.Group).Distinct()) {
				sb.Append("<option>"+WebUtility.HtmlEncode(group)+"</option>");
			}
			sb.Append("</select></label></p>\r\n<table>\r\n<thead><tr><th>Scene</th><th>Size and colours</th>");
			foreach(DrawingMode engine in LegacyRecorder.ArrayEngines) {
				sb.Append("<th>"+engine+"</th>");
				if(engine==DrawingMode.DirectX && hasSmoothed) {
					sb.Append("<th>DirectX, smoothed</th>");
				}
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
						AppendIndexCell(sb,scene,listSizes[i],engine,pathPng,dictSidecars.TryGetValue(pathPng,out Dictionary<string,string> dict)?dict:null,false);
						if(engine==DrawingMode.DirectX && hasSmoothed) {
							string pathSmoothed=listSizes[i].FolderName+"/"+scene.Name+".png";
							if(dictSmoothed.TryGetValue(pathSmoothed,out Dictionary<string,string> dictSmooth)) {
								AppendIndexCell(sb,scene,listSizes[i],engine,"../"+folderNameSmoothed+"/"+pathSmoothed,dictSmooth,true);
							}
							else {
								sb.Append("<td class=\"cap\"></td>");
							}
						}
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

		///<summary>One picture cell of the index: the picture (or why there is none) and what drew it.</summary>
		private static void AppendIndexCell(StringBuilder sb,ChartScene scene,ChartSize chartSize,DrawingMode engine,string src,Dictionary<string,string> dict,
			bool isSmoothed)
		{
			if(dict==null) {
				sb.Append("<td class=\"cap\">"+(scene.Kind==ChartSceneKind.Perio && engine!=DrawingMode.DirectX?"DirectX only":"not recorded")+"</td>");
				return;
			}
			sb.Append("<td>");
			if(dict.ContainsKey("Failed")) {
				sb.Append("<div class=\"warn\">Failed: "+WebUtility.HtmlEncode(dict["Failed"])+"</div>");
			}
			else {
				sb.Append("<img loading=\"lazy\" src=\""+src+"\" width=\""+chartSize.Size.Width+"\" height=\""+chartSize.Size.Height
					+"\" alt=\""+scene.Name+" in "+engine+(isSmoothed?", smoothed":"")+"\">");
			}
			string started=dict.TryGetValue("Started mode",out string value)?value:"?";
			string bitmap=dict.TryGetValue("Bitmap",out value)?value:"";
			string alpha=dict.TryGetValue("Alpha",out value)?value:"";
			bool isOtherMode=started!=engine.ToString();
			if(isSmoothed) {
				string format=dict.TryGetValue("Device or pixel format",out value)?value:"";
				sb.Append("<div class=\"smooth\">smoothed, for the gallery only (never compared): "+WebUtility.HtmlEncode(format)+"</div>");
			}
			sb.Append("<div class=\""+(isOtherMode?"warn":"cap")+"\">drawn by "+WebUtility.HtmlEncode(started)+"; "+WebUtility.HtmlEncode(bitmap)+"; "
				+WebUtility.HtmlEncode(alpha)+"</div>");
			if(dict.TryGetValue("DirectX adapter",out value)) {
				sb.Append("<div class=\"cap\">adapter "+WebUtility.HtmlEncode(value)+"</div>");
			}
			sb.Append("</td>");
		}
		#endregion Manifest and index

		///<summary>What happened: counts, notes and failures.</summary>
		public string GetSummary() {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(ListWritten.Count+" pictures written to "+_folderOut+", "+ListWrittenSmoothed.Count+" smoothed pictures to "
				+LegacyRecorder.GetSmoothedFolder(_folderOut)+", "+ListFailures.Count+" failed.\r\n");
			foreach(DrawingMode engine in LegacyRecorder.ArrayEngines) {
				foreach(IGrouping<string,string> grouping in ListWritten.Where(x => x.StartsWith(engine+"/")).GroupBy(x => x.Split('/')[1]).OrderBy(x => x.Key)) {
					stringBuilder.Append("  "+engine+" "+grouping.Key+": "+grouping.Count()+"\r\n");
				}
			}
			foreach(string smoothed in ListWrittenSmoothed) {
				stringBuilder.Append("Smoothed: "+smoothed+"\r\n");
			}
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

	///<summary>The check of a re-recording against the legacy baseline (LegacyRecorder.CompareLegacyRecording). Every baseline picture of the compared
	///modes and scenes must be in the new recording, and every new picture must have a baseline. A picture matches when the "Pixels SHA-256" lines of
	///both .txt files are equal and, by default, both PNG files have the same bytes; in pixels-only mode the decoded pixels are compared instead of the
	///bytes, because the PNG bytes depend on the framework's DeflateStream (PngWriter). Only the mode folders (DirectX, OpenGL, Simple2D) are read:
	///the smoothed gallery pictures beside the recording are never compared.</summary>
	public class LegacyComparison {
		///<summary>The baseline folder the new recording was compared with, as Compare was given it.</summary>
		public string FolderBaseline;
		public bool IsPixelsOnly;
		public List<DrawingMode> ListEngines;
		///<summary>Null means every scene.</summary>
		public List<string> ListSceneNames;
		///<summary>Paths relative to the recording folder, "DirectX/410x307/empty-universal.png", that match.</summary>
		public List<string> ListMatching=new List<string>();
		///<summary>Baseline pictures of the compared modes and scenes that the new recording does not have.</summary>
		public List<string> ListMissing=new List<string>();
		///<summary>New pictures with no baseline.</summary>
		public List<string> ListNoBaseline=new List<string>();
		///<summary>Pictures that do not match, each with the reason.</summary>
		public List<string> ListDifferent=new List<string>();

		///<summary>True when at least one picture was compared, every one matches, none is missing and none lacks a baseline.</summary>
		public bool IsPass {
			get {
				return ListMatching.Count>0 && ListMissing.Count==0 && ListNoBaseline.Count==0 && ListDifferent.Count==0;
			}
		}

		public static LegacyComparison Compare(string folderBaseline,string folderNew,List<DrawingMode> listEngines,List<string> listSceneNames,
			bool isPixelsOnly)
		{
			LegacyComparison legacyComparison=new LegacyComparison();
			legacyComparison.FolderBaseline=folderBaseline;
			legacyComparison.IsPixelsOnly=isPixelsOnly;
			legacyComparison.ListEngines=listEngines;
			legacyComparison.ListSceneNames=listSceneNames;
			List<string> listBaseline=GetPictures(folderBaseline,listEngines,listSceneNames);
			List<string> listNew=GetPictures(folderNew,listEngines,listSceneNames);
			legacyComparison.ListMissing.AddRange(listBaseline.Except(listNew));
			legacyComparison.ListNoBaseline.AddRange(listNew.Except(listBaseline));
			foreach(string pathRelative in listNew.Intersect(listBaseline)) {
				string reason=ComparePicture(Path.Combine(folderBaseline,pathRelative),Path.Combine(folderNew,pathRelative),isPixelsOnly);
				if(reason==null) {
					legacyComparison.ListMatching.Add(pathRelative);
				}
				else {
					legacyComparison.ListDifferent.Add(pathRelative+": "+reason);
				}
			}
			return legacyComparison;
		}

		///<summary>The PNG files under folder\&lt;mode&gt; for the given modes, and only of the given scenes unless listSceneNames is null, as
		///"&lt;mode&gt;/&lt;size&gt;/&lt;scene&gt;.png" in ordinal order.</summary>
		public static List<string> GetPictures(string folder,List<DrawingMode> listEngines,List<string> listSceneNames) {
			List<string> listPaths=new List<string>();
			foreach(DrawingMode engine in listEngines) {
				string folderEngine=Path.Combine(folder,engine.ToString());
				if(!Directory.Exists(folderEngine)) {
					continue;
				}
				foreach(string path in Directory.GetFiles(folderEngine,"*.png",SearchOption.AllDirectories)) {
					if(listSceneNames!=null && !listSceneNames.Contains(Path.GetFileNameWithoutExtension(path))) {
						continue;
					}
					listPaths.Add(path.Substring(folder.TrimEnd('\\','/').Length+1).Replace('\\','/'));
				}
			}
			return listPaths.OrderBy(x => x,StringComparer.Ordinal).ToList();
		}

		///<summary>Null when the new picture matches its baseline, otherwise why not. The .txt files' "Pixels SHA-256" lines must be equal; then the
		///file bytes (or, in pixels-only mode, the decoded pixels).</summary>
		public static string ComparePicture(string pathBaseline,string pathNew,bool isPixelsOnly) {
			string hashBaseline=ReadPixelsSha256(Path.ChangeExtension(pathBaseline,".txt"));
			string hashNew=ReadPixelsSha256(Path.ChangeExtension(pathNew,".txt"));
			List<string> listReasons=new List<string>();
			if(hashBaseline==null || hashNew==null) {
				listReasons.Add("no \"Pixels SHA-256\" in the "+(hashBaseline==null?"baseline's":"new")+" .txt");
			}
			else if(hashBaseline!=hashNew) {
				listReasons.Add("the .txt files' pixel SHA-256 differ (baseline "+hashBaseline+", new "+hashNew+")");
			}
			bool isSame;
			if(isPixelsOnly) {
				isSame=GetPixelsSha256(pathBaseline)==GetPixelsSha256(pathNew);
			}
			else {
				isSame=ChartImageCompare.AreFilesIdentical(pathBaseline,pathNew);
			}
			if(!isSame) {
				using Bitmap bitmapBaseline=new Bitmap(pathBaseline);
				using Bitmap bitmapNew=new Bitmap(pathNew);
				listReasons.Add((isPixelsOnly?"the pixels differ. ":"the bytes differ. ")+ChartImageCompare.Compare(bitmapBaseline,bitmapNew));
			}
			if(listReasons.Count==0) {
				return null;
			}
			return string.Join("; ",listReasons);
		}

		///<summary>The "Pixels SHA-256" of a recording's .txt, or null when there is no .txt or no such line.</summary>
		public static string ReadPixelsSha256(string pathText) {
			if(!File.Exists(pathText)) {
				return null;
			}
			return LegacyRecording.ReadSidecar(pathText).TryGetValue("Pixels SHA-256",out string value)?value:null;
		}

		///<summary>The SHA-256 of a PNG's decoded pixels as 32-bit ARGB, the hash the recorder writes in "Pixels SHA-256".</summary>
		public static string GetPixelsSha256(string pathPng) {
			using Bitmap bitmap=new Bitmap(pathPng);
			return ChartImageCompare.Sha256(ChartImageCompare.GetPixelsArgb(bitmap));
		}

		public string GetSummary() {
			StringBuilder stringBuilder=new StringBuilder();
			int countCompared=ListMatching.Count+ListDifferent.Count;
			stringBuilder.Append("Compared "+countCompared+" pictures of "+string.Join(", ",ListEngines)+" ("
				+(ListSceneNames==null?"every scene":"scenes "+string.Join(", ",ListSceneNames))+") with "+GetBaselineName()+", "
				+(IsPixelsOnly?"by their decoded pixels":"byte for byte")+" and by the \"Pixels SHA-256\" of their .txt: "+ListMatching.Count+" match, "
				+ListDifferent.Count+" differ, "+ListMissing.Count+" baseline pictures are missing from the new recording, "+ListNoBaseline.Count
				+" new pictures have no baseline.\r\n");
			foreach(DrawingMode engine in ListEngines) {
				foreach(IGrouping<string,string> grouping in ListMatching.Where(x => x.StartsWith(engine+"/")).GroupBy(x => x.Split('/')[1]).OrderBy(x => x.Key)) {
					stringBuilder.Append("  "+engine+" "+grouping.Key+": "+grouping.Count()+" match\r\n");
				}
			}
			foreach(string path in ListMissing) {
				stringBuilder.Append("Missing: "+path+"\r\n");
			}
			foreach(string path in ListNoBaseline) {
				stringBuilder.Append("No baseline: "+path+"\r\n");
			}
			foreach(string different in ListDifferent) {
				stringBuilder.Append("Different: "+different+"\r\n");
			}
			if(countCompared==0) {
				stringBuilder.Append("No pictures were compared.\r\n");
			}
			return stringBuilder.ToString();
		}

		///<summary>The baseline folder that was compared with, as Compare was given it: relative to the repository root when it is inside it (for
		///example "tests\ToothChartImageTests\Baselines\legacy"), otherwise the path as given.</summary>
		private string GetBaselineName() {
			if(string.IsNullOrWhiteSpace(FolderBaseline)) {
				return "no baseline folder";
			}
			string folder=FolderBaseline.TrimEnd('\\','/');
			string repoRoot;
			try {
				repoRoot=TestPaths.RepoRoot;
			}
			catch(InvalidOperationException) {
				return folder;
			}
			if(folder.StartsWith(repoRoot+"\\",StringComparison.OrdinalIgnoreCase)) {
				return folder.Substring(repoRoot.Length+1);
			}
			return folder;
		}
	}
}
