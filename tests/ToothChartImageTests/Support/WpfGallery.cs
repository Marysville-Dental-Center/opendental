// The owner gallery of the WPF chart (docs/specs/wpf-tooth-chart-plan.md, section 8): every scene with today's DirectX, OpenGL and Simple 2D pictures
// (TC-0's recording), the WPF picture (marked equal or not to the branch's baseline) and its difference from the last approved WPF picture, and the
// render times. Gallery G0 is TC-1's.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using OpenPmsDentalBusiness;

namespace ToothChartImageTests {
	///<summary>One WPF picture of the gallery and how it compares with the baseline.</summary>
	public class WpfGalleryPicture {
		public ChartScene Scene;
		public ChartSize ChartSize;
		///<summary>Relative to the gallery folder, for example "wpf/410x307/empty-universal.png".</summary>
		public string PathWpf;
		///<summary>Relative path of the difference image, or null when the picture matches its baseline or has none.</summary>
		public string PathDiff;
		///<summary>Null when there is no approved baseline.</summary>
		public ChartImageCompareResult Comparison;
		///<summary>True when the approved set (OPMS_CHART_GALLERY_APPROVED, from main) has this scene and size.</summary>
		public bool HasBaseline;
		///<summary>The pixel SHA-256 of the fresh WPF picture (ChartImageCompare.Sha256 of GetPixelsArgb).</summary>
		public string PixelsSha256;
		///<summary>The Pixels SHA-256 of this branch's baseline of the scene (the .txt in Baselines\wpf-net48 that the branch merges), or null when
		///it has none. Equal to PixelsSha256 when the owner's approval of the picture approves the very file being merged.</summary>
		public string PixelsSha256Merged;
		///<summary>Null when the WPF chart drew the picture. Otherwise why it did not (the engine did not start, or the render failed and GetBitmap
		///returned the Simple 2D fallback): no picture is saved, and the gallery marks the cell as failed.</summary>
		public string Failure;
		///<summary>TC-3: the parity probes of this picture against TC-0's DirectX picture of the scene (ParityProbes), in words, or null when there is
		///no DirectX picture. IsParityOk is false when a probe differs that no allowance covers.</summary>
		public string ParityText;
		public bool IsParityOk=true;
	}

	///<summary>From G1 on: one live-view capture of the gallery (O-34): the live view as the program draws it on screen, in software at 1x.</summary>
	public class WpfGalleryLiveCapture {
		public ChartScene Scene;
		public ChartSize ChartSize;
		///<summary>Relative to the gallery folder, for example "live/410x307/restored-adult.png".</summary>
		public string PathLive;
		///<summary>The parity probes of the capture against the renderer's picture of the same data, and how many of them differ.</summary>
		public int CountProbes;
		public int CountProbesFailed;
		///<summary>The capture against the renderer's picture by the baseline tolerances (mostly the 3D layer's unsmoothed edges differ).</summary>
		public ChartImageCompareResult Comparison;
	}

	public static class WpfGallery {
		///<summary>The gallery's legacy columns, in order.</summary>
		private static readonly DrawingMode[] _arrayEngines={ DrawingMode.DirectX,DrawingMode.OpenGL,DrawingMode.Simple2D };

		///<summary>The marker file of a gallery folder, written by PrepareFolder: WriteGallery empties only a folder that holds it (or a gallery's
		///index.html).</summary>
		public const string MarkerFileName="tooth-chart-gallery.txt";

		///<summary>Makes folder an empty gallery folder with its marker file. A folder that does not exist is made and an empty one is used. A folder
		///that holds a gallery (the marker file, or the index.html of a gallery made before the marker) is emptied. Any other folder is refused with
		///an InvalidOperationException and left as it is, whatever -Out or OPMS_CHART_GALLERY named: "-Out ." in a checkout deletes nothing.</summary>
		public static void PrepareFolder(string folder) {
			string folderFull=Path.GetFullPath(folder);
			if(Directory.Exists(folderFull) && Directory.EnumerateFileSystemEntries(folderFull).Any()) {
				if(!IsGalleryFolder(folderFull)) {
					throw new InvalidOperationException("Not a gallery folder, so it is not emptied: "+folderFull+". It holds neither "+MarkerFileName
						+" nor a gallery's index.html. Name a new or empty folder.");
				}
				Directory.Delete(folderFull,true);
			}
			Directory.CreateDirectory(folderFull);
			File.WriteAllText(Path.Combine(folderFull,MarkerFileName),"A tooth chart gallery (tools\\New-ToothChartGallery.ps1, ToothChartImageTests.WpfImageTests."
				+"WriteGallery). The next gallery written to this folder empties it first.\r\n",new UTF8Encoding(false));
		}

		///<summary>True when the folder holds the marker file, or an index.html that a gallery wrote (a gallery made before the marker file).</summary>
		public static bool IsGalleryFolder(string folder) {
			if(File.Exists(Path.Combine(folder,MarkerFileName))) {
				return true;
			}
			string pathIndex=Path.Combine(folder,"index.html");
			if(!File.Exists(pathIndex)) {
				return false;
			}
			string html=File.ReadAllText(pathIndex);
			return html.StartsWith("<!doctype html>",StringComparison.Ordinal) && html.Contains("<title>Tooth chart gallery ");
		}

		///<summary>Copies TC-0's legacy pictures of every scene and size into the gallery (legacy\&lt;mode&gt;\&lt;size&gt;\, legacy-smoothed\&lt;size&gt;\),
		///so the folder stands alone, and writes index.html and timings.txt. approvedFrom says where the approved WPF pictures came from.
		///listLiveCaptures (from G1 on) are the live view's captures, already saved under folder\live; null when none could be made.</summary>
		public static void Write(string folder,string galleryName,string galleryNote,List<WpfGalleryPicture> listPictures,WpfTimingReport timingReportCold,
			string commit,string approvedFrom,List<WpfGalleryLiveCapture> listLiveCaptures=null)
		{
			Directory.CreateDirectory(folder);
			foreach(ChartScene scene in ChartScenes.GetAll()) {
				foreach(ChartSize chartSize in ChartScenes.GetSizes(scene)) {
					foreach(DrawingMode engine in _arrayEngines) {
						CopyIfExists(Path.Combine(TestPaths.BaselinesLegacy,engine.ToString(),chartSize.FolderName,scene.Name+".png"),
							Path.Combine(folder,"legacy",engine.ToString(),chartSize.FolderName,scene.Name+".png"));
					}
					CopyIfExists(Path.Combine(TestPaths.BaselinesLegacySmoothed,chartSize.FolderName,scene.Name+".png"),
						Path.Combine(folder,"legacy-smoothed",chartSize.FolderName,scene.Name+".png"));
				}
			}
			if(timingReportCold!=null) {
				File.WriteAllText(Path.Combine(folder,"timings.txt"),timingReportCold.GetReport(),new UTF8Encoding(false));
			}
			File.WriteAllText(Path.Combine(folder,"index.html"),GetIndexHtml(folder,galleryName,galleryNote,listPictures,timingReportCold,commit,approvedFrom,
				listLiveCaptures,galleryName!="G0"),new UTF8Encoding(false));
		}

		private static void CopyIfExists(string pathFrom,string pathTo) {
			if(!File.Exists(pathFrom)) {
				return;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(pathTo));
			File.Copy(pathFrom,pathTo,true);
		}

		///<summary>The gallery's index.html. The pictures' files are looked for under folder. isLiveSection (from G1 on) adds the section of the live
		///view's captures, listLiveCaptures, or a note when there are none.</summary>
		public static string GetIndexHtml(string folder,string galleryName,string galleryNote,List<WpfGalleryPicture> listPictures,WpfTimingReport timingReport,
			string commit,string approvedFrom,List<WpfGalleryLiveCapture> listLiveCaptures=null,bool isLiveSection=false)
		{
			StringBuilder sb=new StringBuilder();
			sb.Append("<!doctype html>\r\n<html lang=\"en\">\r\n<head>\r\n<meta charset=\"utf-8\">\r\n");
			sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\r\n");
			sb.Append("<title>Tooth chart gallery "+WebUtility.HtmlEncode(galleryName)+"</title>\r\n<style>\r\n");
			sb.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:16px;background:#fff;color:#111}\r\n");
			sb.Append("table{border-collapse:collapse}td,th{border:1px solid #bbb;padding:4px;vertical-align:top;font-size:12px;text-align:left}\r\n");
			sb.Append("img{display:block;max-width:100%;height:auto}.cap{color:#555}.name{font-weight:bold}.warn{color:#b00}.ok{color:#070}\r\n");
			sb.Append(".times td{font-size:13px}.num{text-align:right;font-variant-numeric:tabular-nums}\r\n");
			sb.Append("</style>\r\n</head>\r\n<body>\r\n");
			sb.Append("<h1>Tooth chart gallery "+WebUtility.HtmlEncode(galleryName)+"</h1>\r\n");
			sb.Append("<p>"+WebUtility.HtmlEncode(galleryNote)+"</p>\r\n");
			sb.Append("<p class=\"cap\">Made "+DateTime.Now.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)+(string.IsNullOrEmpty(commit)?"":" at commit "
				+WebUtility.HtmlEncode(commit))+". Every scene is synthetic: no patient data and no database. Columns: today's DirectX, OpenGL and Simple 2D "
				+"pictures (recorded in TC-0; DirectX without multisampling, a few smoothed ones beside it), the WPF chart's picture, and its difference "
				+"from the last approved WPF picture (different pixels in red over grey). Approved WPF pictures: "+WebUtility.HtmlEncode(approvedFrom)+".</p>\r\n");
			if(timingReport!=null) {
				AppendTimings(sb,timingReport);
			}
			if(isLiveSection) {
				AppendLiveCaptures(sb,folder,listLiveCaptures);
			}
			int countWithBaseline=listPictures.Count(x => x.HasBaseline);
			int countDifferent=listPictures.Count(x => x.Comparison!=null && !x.Comparison.IsPass);
			int countFailed=listPictures.Count(x => x.Failure!=null);
			int countDrawn=listPictures.Count-countFailed;
			int countSameAsMerged=listPictures.Count(x => x.Failure==null && x.PixelsSha256Merged!=null && x.PixelsSha256Merged==x.PixelsSha256);
			sb.Append("<p>"+countDrawn+" WPF pictures; "+countWithBaseline+" have an approved baseline"+(countWithBaseline>0?", "
				+countDifferent+" differ from it beyond the tolerance":"")+".</p>\r\n");
			int countParity=listPictures.Count(x => x.ParityText!=null);
			if(countParity>0) {
				int countParityOk=listPictures.Count(x => x.ParityText!=null && x.IsParityOk);
				sb.Append("<p class=\""+(countParityOk==countParity?"ok":"warn")+"\">Parity with DirectX (TC-3, plan section 7): "+countParityOk+" of the "+countParity
					+" WPF pictures with a DirectX picture agree with it at every probe (each tooth's facial and occlusal surface centres, the X, build-ups, "
					+"implants and root canals: hue within 12 degrees, lightness within 20 points; the known differences of plan 5.1 allowed). Each WPF "
					+"picture says so under it.</p>\r\n");
			}
			List<WpfGalleryPicture> listChanged=listPictures.Where(x => x.Comparison!=null && x.Comparison.CountDifferent>0).ToList();
			if(listChanged.Count>0) {
				sb.Append("<p class=\"warn\"><b>"+listChanged.Count+" WPF pictures differ from the approved ones</b> (their baselines are re-recorded in this "
					+"branch; the last column shows each difference): "+string.Join(", ",listChanged.Select(x => "<a href=\"#"+x.Scene.Name+"\">"+x.ChartSize.FolderName+"/"
					+x.Scene.Name+"</a>"))+".</p>\r\n");
			}
			sb.Append("<p class=\""+(countSameAsMerged==countDrawn?"ok":"warn")+"\">"+countSameAsMerged+" of the "+countDrawn+" WPF pictures have exactly the pixels "
				+"of this branch's baselines (Baselines/wpf-net48, the files the branch merges, by their .txt's Pixels SHA-256), so approving a picture here "
				+"approves that file. Each WPF picture says whether it does.</p>\r\n");
			if(countFailed>0) {
				sb.Append("<p class=\"warn\"><b>"+countFailed+" scenes failed</b>: the WPF chart did not draw them, so their cells show why instead of a "
					+"picture.</p>\r\n");
			}
			List<ChartScene> listScenes=ChartScenes.GetAll();
			sb.Append("<p><label>Group: <select id=\"group\"><option value=\"\">all</option>");
			foreach(string group in listScenes.Select(x => x.Group).Distinct()) {
				sb.Append("<option>"+WebUtility.HtmlEncode(group)+"</option>");
			}
			sb.Append("</select></label></p>\r\n<table>\r\n<thead><tr><th>Scene</th><th>Size and colours</th><th>DirectX</th><th>DirectX, smoothed</th>"
				+"<th>OpenGL</th><th>Simple 2D</th><th>WPF</th><th>Difference from the approved WPF picture</th></tr></thead>\r\n");
			foreach(ChartScene scene in listScenes) {
				List<ChartSize> listSizes=ChartScenes.GetSizes(scene);
				sb.Append("<tbody data-group=\""+WebUtility.HtmlEncode(scene.Group)+"\" id=\""+scene.Name+"\">\r\n");
				for(int i=0;i<listSizes.Count;i++) {
					ChartSize chartSize=listSizes[i];
					sb.Append("<tr>");
					if(i==0) {
						sb.Append("<td rowspan=\""+listSizes.Count+"\"><div class=\"name\">"+scene.Name+"</div><div class=\"cap\">"+WebUtility.HtmlEncode(scene.Group)
							+"</div><div>"+WebUtility.HtmlEncode(scene.Description)+"</div></td>");
					}
					sb.Append("<td>"+chartSize.FolderName+"<div class=\"cap\">"+chartSize.ColorSet+" colours</div></td>");
					AppendImageCell(sb,folder,"legacy/DirectX/"+chartSize.FolderName+"/"+scene.Name+".png",chartSize,"not recorded");
					AppendImageCell(sb,folder,"legacy-smoothed/"+chartSize.FolderName+"/"+scene.Name+".png",chartSize,"");
					AppendImageCell(sb,folder,"legacy/OpenGL/"+chartSize.FolderName+"/"+scene.Name+".png",chartSize,scene.Kind==ChartSceneKind.Perio?"DirectX only":"not recorded");
					AppendImageCell(sb,folder,"legacy/Simple2D/"+chartSize.FolderName+"/"+scene.Name+".png",chartSize,scene.Kind==ChartSceneKind.Perio?"DirectX only":"not recorded");
					WpfGalleryPicture picture=listPictures.FirstOrDefault(x => x.Scene.Name==scene.Name && x.ChartSize.FolderName==chartSize.FolderName);
					if(picture==null) {
						sb.Append("<td class=\"cap\">"+(scene.Kind==ChartSceneKind.Perio?"Perio on the WPF chart comes with TC-7 (gallery G3).":"not drawn")+"</td><td></td>");
					}
					else if(picture.Failure!=null) {
						sb.Append("<td class=\"warn\">failed: "+WebUtility.HtmlEncode(picture.Failure)+"</td><td class=\"cap\">not compared</td>");
					}
					else {
						AppendImageCell(sb,folder,picture.PathWpf,chartSize,"missing",GetMergedCaption(picture)+(picture.ParityText==null?""
							:"<div class=\""+(picture.IsParityOk?"ok":"warn")+"\">"+WebUtility.HtmlEncode(picture.ParityText)+"</div>"));
						if(!picture.HasBaseline) {
							sb.Append("<td class=\"cap\">no approved WPF picture yet</td>");
						}
						else if(picture.PathDiff==null) {
							sb.Append("<td class=\"ok\">same as approved ("+WebUtility.HtmlEncode(picture.Comparison.ToString())+")</td>");
						}
						else {
							sb.Append("<td><img loading=\"lazy\" src=\""+picture.PathDiff+"\" width=\""+chartSize.Size.Width+"\" height=\""+chartSize.Size.Height
								+"\" alt=\"difference\"><div class=\""+(picture.Comparison.IsPass?"cap":"warn")+"\">"+WebUtility.HtmlEncode(picture.Comparison.ToString())
								+"</div></td>");
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
			return sb.ToString();
		}

		///<summary>A cell with the picture, or textMissing when the file is not in the gallery. captionHtml, when given, goes under the picture.</summary>
		private static void AppendImageCell(StringBuilder sb,string folder,string pathRelative,ChartSize chartSize,string textMissing,string captionHtml=null) {
			if(!File.Exists(Path.Combine(folder,pathRelative.Replace('/','\\')))) {
				sb.Append("<td class=\"cap\">"+WebUtility.HtmlEncode(textMissing)+"</td>");
				return;
			}
			sb.Append("<td><img loading=\"lazy\" src=\""+pathRelative+"\" width=\""+chartSize.Size.Width+"\" height=\""+chartSize.Size.Height+"\" alt=\""
				+WebUtility.HtmlEncode(pathRelative)+"\">"+(captionHtml??"")+"</td>");
		}

		///<summary>TC-3's table: refill plus the screen's update with the dashboard hidden and visible, a selection-only change and one pen move, for
		///DirectX, the live view (as the program draws it, in software, O-34: the UI thread's work and WPF's frame; and without the 3D layer cache)
		///and the renderer's picture (the "Draw in software" switch), at 410x307 and 615x461.</summary>
		private static void AppendOnScreenTimings(StringBuilder sb,WpfTimingReport timingReport) {
			sb.Append("<h2>On-screen times against DirectX (TC-3)</h2>\r\n<p>Measured in a fresh process that draws every WPF window in software, as "
				+"OpenPmsDental.exe does (it sets RenderOptions.ProcessRenderMode to SoftwareOnly for the whole process: owner question O-34), on shown "
				+"windows: a refill (the Chart module's Set calls) and the screen's update; with the Patient Dashboard visible the refill also makes the "
				+"dashboard's image (GetBitmap, the renderer's picture); a selection-only change toggles one tooth; a pen move adds 4 pixels to a stroke. "
				+"Median of "+WpfTimings.CountRepeats+" runs. The live view's screen update is the UI thread's work and then WPF's frame, which WPF's "
				+"render thread draws in software at 1x; the frame is timed until WPF has presented it, on a window on the display that the user cannot "
				+"see. The live view keeps its 3D layer in a bitmap cache, so a frame that changes only the 2D layer or the pen stroke does not draw "
				+"the 3D layer again; the row without the cache is the live view as TC-3 first built it.</p>\r\n");
			sb.Append("<table class=\"times\"><thead><tr><th>Size</th><th>Engine</th><th>Refill, dashboard hidden</th><th>Refill, dashboard visible</th>"
				+"<th>Selection only</th><th>One pen move</th></tr></thead><tbody>\r\n");
			foreach(string size in new[] { "410x307","615x461" }) {
				double Get(string suffix) {
					return timingReport.Get(size+suffix);
				}
				AppendTimesRow(sb,size,"DirectX (today)",Get(".directx.refill"),Get(".directx.refill.dashboard"),Get(".directx.selection"),double.NaN);
				AppendTimesRow(sb,size,"WPF live view as the program draws it (software, 1x), until WPF's frame is presented",Get(".live.shown.refill"),
					Get(".live.refill.dashboard")+Get(".live.frame.refill"),Get(".live.shown.selection"),Get(".live.shown.pen"),true);
				AppendTimesRow(sb,size,"  of that, the UI thread's work",Get(".live.shown.refill")-Get(".live.frame.refill"),Get(".live.refill.dashboard"),
					Get(".live.shown.selection")-Get(".live.frame.selection"),Get(".live.shown.pen")-Get(".live.frame.pen"));
				AppendTimesRow(sb,size,"  of that, WPF's frame in software",Get(".live.frame.refill"),Get(".live.frame.refill"),Get(".live.frame.selection"),
					Get(".live.frame.pen"));
				AppendTimesRow(sb,size,"WPF live view without the 3D layer cache (TC-3 as first built), software",Get(".live.nocache.shown.refill"),
					Get(".live.refill.dashboard")+Get(".live.nocache.frame.refill"),Get(".live.nocache.shown.selection"),Get(".live.nocache.shown.pen"));
				AppendTimesRow(sb,size,"WPF drawn in software: the renderer's smoothed picture (the \"Draw in software\" switch)",Get(".software.refill"),
					Get(".software.refill.dashboard"),Get(".software.selection"),double.NaN);
			}
			sb.Append("</tbody></table>\r\n");
			List<string> listNotes=new List<string>();
			foreach(string size in new[] { "410x307","615x461" }) {
				double fullFrame=timingReport.Get(size+".live.fullframe");
				double idle=timingReport.Get(size+".live.frame.idle");
				double offscreen=timingReport.Get(size+".live.offscreen.frame.refill");
				listNotes.Add(size+": one full frame of the live view by WPF's software rasterizer at 1x (the 3D layer drawn again) "+FormatMs(fullFrame)
					+"; waiting for a frame with nothing changed "+FormatMs(idle)+"; a refill's frame on a window off every display "+FormatMs(offscreen)+".");
			}
			sb.Append("<p class=\"cap\">"+WebUtility.HtmlEncode(string.Join(" ",listNotes))+"</p>\r\n");
		}

		private static void AppendTimesRow(StringBuilder sb,string size,string engine,double refill,double refillDashboard,double selection,double pen,
			bool isBold=false)
		{
			sb.Append("<tr><td>"+size+"</td><td>"+(isBold?"<b>":"")+WebUtility.HtmlEncode(engine)+(isBold?"</b>":"")+"</td>");
			foreach(double value in new[] { refill,refillDashboard,selection,pen }) {
				sb.Append("<td class=\"num\">"+(isBold?"<b>":"")+WebUtility.HtmlEncode(FormatMs(value))+(isBold?"</b>":"")+"</td>");
			}
			sb.Append("</tr>\r\n");
		}

		private static string FormatMs(double value) {
			return double.IsNaN(value)?"not measured":value.ToString("0.0",CultureInfo.InvariantCulture)+" ms";
		}

		///<summary>From G1 on: the live view as the program draws it (O-34), next to DirectX and the renderer's picture of the same scene, each at 1x and
		///a part of it enlarged 3 times without smoothing, so the edges can be compared.</summary>
		private static void AppendLiveCaptures(StringBuilder sb,string folder,List<WpfGalleryLiveCapture> listLiveCaptures) {
			sb.Append("<h2>The live view as the program draws it (O-34)</h2>\r\n");
			sb.Append("<p>OpenPmsDental.exe sets RenderOptions.ProcessRenderMode to SoftwareOnly for the whole process (ProgramEntry.cs, a line from "
				+"upstream), and a window cannot turn hardware rendering back on, so the live view is drawn by WPF's software rasterizer at 1x with no "
				+"edge smoothing, not by the graphics card that O-19 (b) assumed. These captures are the live view of a shown chart, read back with "
				+"WPF's software rasterizer, which is what the screen shows in such a process. Next to them: today's DirectX picture (unsmoothed and, "
				+"where recorded, smoothed) and the renderer's picture (3x supersampled and reduced: GetBitmap, prints, and the screen with \"Draw in "
				+"software\"). The second row of each scene enlarges the upper right molars 3 times without smoothing.</p>\r\n");
			if(listLiveCaptures==null || listLiveCaptures.Count==0) {
				sb.Append("<p class=\"warn\">No live-view captures: no interactive desktop session to show a chart on.</p>\r\n");
				return;
			}
			sb.Append("<table><thead><tr><th>Scene</th><th>DirectX</th><th>DirectX, smoothed</th><th>WPF renderer (GetBitmap)</th>"
				+"<th>WPF live view (software, 1x)</th></tr></thead><tbody>\r\n");
			foreach(WpfGalleryLiveCapture capture in listLiveCaptures) {
				ChartSize chartSize=capture.ChartSize;
				string[] arrayPaths={ "legacy/DirectX/"+chartSize.FolderName+"/"+capture.Scene.Name+".png","legacy-smoothed/"+chartSize.FolderName+"/"+capture.Scene.Name+".png",
					"wpf/"+chartSize.FolderName+"/"+capture.Scene.Name+".png",capture.PathLive };
				sb.Append("<tr><td rowspan=\"2\"><div class=\"name\">"+capture.Scene.Name+"</div><div class=\"cap\">"+chartSize.FolderName+"</div><div>"
					+WebUtility.HtmlEncode(capture.Scene.Description)+"</div><div class=\"cap\">Live view against the renderer's picture: "
					+(capture.CountProbes-capture.CountProbesFailed)+" of "+capture.CountProbes+" parity probes agree; "
					+WebUtility.HtmlEncode(capture.Comparison==null?"":capture.Comparison.ToString().TrimEnd('.'))+".</div></td>");
				foreach(string path in arrayPaths) {
					AppendImageCell(sb,folder,path,chartSize,"not recorded");
				}
				sb.Append("</tr>\r\n<tr>");
				foreach(string path in arrayPaths) {
					AppendZoomCell(sb,folder,path,chartSize);
				}
				sb.Append("</tr>\r\n");
			}
			sb.Append("</tbody></table>\r\n");
		}

		///<summary>A cell with a part of the picture (the upper right molars: the left quarter of the upper half) enlarged 3 times without smoothing.</summary>
		private static void AppendZoomCell(StringBuilder sb,string folder,string pathRelative,ChartSize chartSize) {
			if(!File.Exists(Path.Combine(folder,pathRelative.Replace('/','\\')))) {
				sb.Append("<td></td>");
				return;
			}
			int zoom=3;
			int widthCrop=chartSize.Size.Width/4;
			int heightCrop=chartSize.Size.Height/2;
			sb.Append("<td><div style=\"width:"+widthCrop*zoom+"px;height:"+heightCrop*zoom+"px;max-width:100%;overflow:hidden\"><img loading=\"lazy\" src=\""
				+pathRelative+"\" alt=\"enlarged\" style=\"width:"+chartSize.Size.Width*zoom+"px;height:"+chartSize.Size.Height*zoom
				+"px;max-width:none;image-rendering:pixelated\"></div></td>");
		}

		///<summary>Under a WPF picture: whether its pixels are exactly those of this branch's baseline (the file being merged).</summary>
		private static string GetMergedCaption(WpfGalleryPicture picture) {
			if(picture.PixelsSha256Merged==null) {
				return "<div class=\"warn\">no baseline in this branch</div>";
			}
			if(picture.PixelsSha256Merged==picture.PixelsSha256) {
				return "<div class=\"ok\">the pixels of this branch's baseline</div>";
			}
			return "<div class=\"warn\">other pixels than this branch's baseline</div>";
		}

		///<summary>The render times, with O-19's limits: at 150% (615x461) a refill plus render within 100 ms, and a selection-only change within 20 ms.
		///From G1 on, first TC-3's on-screen times against DirectX, when measured.</summary>
		private static void AppendTimings(StringBuilder sb,WpfTimingReport timingReport) {
			if(!double.IsNaN(timingReport.Get("410x307.live.refill")) || !double.IsNaN(timingReport.Get("410x307.directx.refill"))) {
				AppendOnScreenTimings(sb,timingReport);
			}
			sb.Append("<h2>Render times for O-19</h2>\r\n");
			double refill150=timingReport.Get("615x461.refill");
			double selection150=timingReport.Get("615x461.selection");
			bool isRefillOk=refill150<=100;
			bool isSelectionOk=selection150<=20;
			sb.Append("<p>The limits proposed for showing the software render on screen (O-19 (a)): at 150% (615x461) a refill plus render of the Chart "
				+"module's chart within 100 ms <b class=\""+(isRefillOk?"ok":"warn")+"\">("+refill150.ToString("0.0",CultureInfo.InvariantCulture)+" ms, "
				+(isRefillOk?"within":"over")+")</b>, and a selection-only change within 20 ms <b class=\""+(isSelectionOk?"ok":"warn")+"\">("
				+selection150.ToString("0.0",CultureInfo.InvariantCulture)+" ms, "+(isSelectionOk?"within":"over")+")</b>. Measured in a fresh process, so the "
				+"first values are cold; warm values are the median of "+WpfTimings.CountRepeats+" runs.</p>\r\n");
			sb.Append("<ul>");
			foreach(string line in timingReport.ListMachine) {
				sb.Append("<li>"+WebUtility.HtmlEncode(line)+"</li>");
			}
			sb.Append("</ul>\r\n<table class=\"times\"><thead><tr><th>Value</th><th>Measured</th><th>What</th></tr></thead><tbody>\r\n");
			foreach(WpfTiming wpfTiming in timingReport.ListTimings) {
				sb.Append("<tr><td>"+WebUtility.HtmlEncode(wpfTiming.Key)+"</td><td class=\"num\">"+wpfTiming.Value.ToString("0.0",CultureInfo.InvariantCulture)+" "
					+WebUtility.HtmlEncode(wpfTiming.Unit)+"</td><td>"+WebUtility.HtmlEncode(wpfTiming.Text)+"</td></tr>\r\n");
			}
			sb.Append("</tbody></table>\r\n");
			foreach(string note in timingReport.ListNotes) {
				sb.Append("<p class=\"warn\">"+WebUtility.HtmlEncode(note)+"</p>\r\n");
			}
		}
	}
}
