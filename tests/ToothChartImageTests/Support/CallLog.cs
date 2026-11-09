// The call log of the tooth chart's painting code (package TC-2, docs/specs/wpf-tooth-chart-plan.md, section 4): one line for each draw call that
// a caller's chart code makes on the chart, in order, with its arguments written the same way whatever the workstation's culture. The logs of
// the callers' code as it was before TC-2 are committed in CallLogs\ and every later painter must give the same lines.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using OpenPmsDentalBusiness;

namespace ToothChartImageTests {
	///<summary>The draw calls of one scene, one line each. Colours are written as ARGB hex, floats with the round-trip format of the invariant
	///culture, strings quoted with their control characters escaped, so a log reads the same on every workstation.</summary>
	public class CallLog {
		private readonly List<string> _listLines=new List<string>();

		///<summary>Adds one call: its name and its arguments, written by Format.</summary>
		public void Add(string name,params object[] arrayArgs) {
			StringBuilder stringBuilder=new StringBuilder(name);
			for(int i=0;i<arrayArgs.Length;i++) {
				stringBuilder.Append(' ').Append(Format(arrayArgs[i]));
			}
			_listLines.Add(stringBuilder.ToString());
		}

		///<summary>How many calls the log holds.</summary>
		public int Count {
			get {
				return _listLines.Count;
			}
		}

		///<summary>The lines, without the header.</summary>
		public List<string> ListLines {
			get {
				return new List<string>(_listLines);
			}
		}

		///<summary>The log as it is stored: a header line that names the caller and the scene, then one line per call, each ending in a line feed.</summary>
		public string ToText(string caller,string scene) {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("# Tooth chart call log (TC-2): caller ").Append(caller).Append(", scene ").Append(scene).Append(", ")
				.Append(_listLines.Count.ToString(CultureInfo.InvariantCulture)).Append(" calls\n");
			for(int i=0;i<_listLines.Count;i++) {
				stringBuilder.Append(_listLines[i]).Append('\n');
			}
			return stringBuilder.ToString();
		}

		///<summary>One argument in the log's own notation.</summary>
		public static string Format(object value) {
			if(value==null) {
				return "null";
			}
			if(value is Color) {
				return "#"+((Color)value).ToArgb().ToString("X8",CultureInfo.InvariantCulture);
			}
			if(value is float) {
				return ((float)value).ToString("R",CultureInfo.InvariantCulture);
			}
			if(value is double) {
				return ((double)value).ToString("R",CultureInfo.InvariantCulture);
			}
			if(value is PointF) {
				PointF pointF=(PointF)value;
				return "("+Format(pointF.X)+","+Format(pointF.Y)+")";
			}
			if(value is string) {
				return Quote((string)value);
			}
			if(value is bool) {
				return (bool)value ? "true" : "false";
			}
			if(value is int || value is long) {
				return Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
			}
			if(value is Enum) {
				return value.ToString();
			}
			if(value is ToothInitial) {
				ToothInitial toothInitial=(ToothInitial)value;
				return "{"+toothInitial.InitialType+" tooth="+Format(toothInitial.ToothNum)+" move="+Format(toothInitial.Movement)
					+" segment="+Format(toothInitial.DrawingSegment)+" color="+Format(toothInitial.ColorDraw)+" text="+Format(toothInitial.DrawText)+"}";
			}
			throw new ArgumentException("The call log cannot write a "+value.GetType().FullName);
		}

		private static string Quote(string text) {
			StringBuilder stringBuilder=new StringBuilder("\"");
			foreach(char c in text) {
				if(c=='"' || c=='\\') {
					stringBuilder.Append('\\').Append(c);
				}
				else if(c<' ') {
					stringBuilder.Append("\\u").Append(((int)c).ToString("X4",CultureInfo.InvariantCulture));
				}
				else {
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.Append('"').ToString();
		}
	}

	///<summary>The committed call logs: CallLogs\(caller)\(scene).txt in this project's folder. OPMS_RECORD_CALL_LOGS=1 writes them instead of
	///comparing, for a deliberate re-record (never in a normal run).</summary>
	public static class CallLogFiles {
		public const string EnvRecord="OPMS_RECORD_CALL_LOGS";

		public static bool IsRecording {
			get {
				return Environment.GetEnvironmentVariable(EnvRecord)=="1";
			}
		}

		public static string Folder {
			get {
				return Path.Combine(TestPaths.ProjectFolder,"CallLogs");
			}
		}

		public static string GetPath(string caller,string scene) {
			return Path.Combine(Folder,caller,scene+".txt");
		}

		///<summary>The committed log of a scene with line feeds only (git may check text files out with CRLF), or null when there is none.</summary>
		public static string Read(string caller,string scene) {
			string path=GetPath(caller,scene);
			if(!File.Exists(path)) {
				return null;
			}
			return File.ReadAllText(path,new UTF8Encoding(false)).Replace("\r\n","\n");
		}

		public static void Write(string caller,string scene,string text) {
			string path=GetPath(caller,scene);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path,text,new UTF8Encoding(false));
		}
	}
}
