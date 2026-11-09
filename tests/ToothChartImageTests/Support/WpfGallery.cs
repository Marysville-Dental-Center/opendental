// The owner gallery of the WPF chart (docs/specs/wpf-tooth-chart-plan.md, section 8): every scene with today's DirectX, OpenGL and Simple 2D pictures
// (TC-0's recording), the WPF picture and its difference from the last approved WPF picture, and the render times. Gallery G0 is TC-1's.
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
		public bool HasBaseline;
	}

	public static class WpfGallery {
		///<summary>The gallery's legacy columns, in order.</summary>
		private static readonly DrawingMode[] _arrayEngines={ DrawingMode.DirectX,DrawingMode.OpenGL,DrawingMode.Simple2D };

		///<summary>Copies TC-0's legacy pictures of every scene and size into the gallery (legacy\&lt;mode&gt;\&lt;size&gt;\, legacy-smoothed\&lt;size&gt;\),
		///so the folder stands alone, and writes index.html and timings.txt.</summary>
		public static void Write(string folder,string galleryName,string galleryNote,List<WpfGalleryPicture> listPictures,WpfTimingReport timingReportCold,
			string commit)
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
			File.WriteAllText(Path.Combine(folder,"index.html"),GetIndexHtml(folder,galleryName,galleryNote,listPictures,timingReportCold,commit),new UTF8Encoding(false));
		}

		private static void CopyIfExists(string pathFrom,string pathTo) {
			if(!File.Exists(pathFrom)) {
				return;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(pathTo));
			File.Copy(pathFrom,pathTo,true);
		}

		private static string GetIndexHtml(string folder,string galleryName,string galleryNote,List<WpfGalleryPicture> listPictures,WpfTimingReport timingReport,
			string commit)
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
				+"from the last approved WPF picture (different pixels in red over grey).</p>\r\n");
			if(timingReport!=null) {
				AppendTimings(sb,timingReport);
			}
			int countWithBaseline=listPictures.Count(x => x.HasBaseline);
			int countDifferent=listPictures.Count(x => x.Comparison!=null && !x.Comparison.IsPass);
			sb.Append("<p>"+listPictures.Count+" WPF pictures; "+countWithBaseline+" have an approved baseline"+(countWithBaseline>0?", "+countDifferent
				+" differ from it beyond the tolerance":"")+".</p>\r\n");
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
					else {
						AppendImageCell(sb,folder,picture.PathWpf,chartSize,"missing");
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

		private static void AppendImageCell(StringBuilder sb,string folder,string pathRelative,ChartSize chartSize,string textMissing) {
			if(!File.Exists(Path.Combine(folder,pathRelative.Replace('/','\\')))) {
				sb.Append("<td class=\"cap\">"+WebUtility.HtmlEncode(textMissing)+"</td>");
				return;
			}
			sb.Append("<td><img loading=\"lazy\" src=\""+pathRelative+"\" width=\""+chartSize.Size.Width+"\" height=\""+chartSize.Size.Height+"\" alt=\""
				+WebUtility.HtmlEncode(pathRelative)+"\"></td>");
		}

		///<summary>The render times, with O-19's limits: at 150% (615x461) a refill plus render within 100 ms, and a selection-only change within 20 ms.</summary>
		private static void AppendTimings(StringBuilder sb,WpfTimingReport timingReport) {
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
