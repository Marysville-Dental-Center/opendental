using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenPmsDentalBusiness {
	///<summary>Window sizes remembered per user on this workstation (a fork addition; first used by the Group Note window, package M2-NOTES). Kept in a
	///small text file under the Windows user's local application data, never in the database, because screens differ between workstations. Sizes are
	///stored at 96 dpi (unscaled). A missing, unreadable or locked file only means the window opens at its default size: nothing here throws.</summary>
	public static class LocalWindowSizes {
		///<summary>Tests point this at a scratch file. Null means the default file.</summary>
		public static string PathOverride;

		///<summary>%LocalAppData%\OpenDental\WindowSizes.txt, one line per window and user: name, UserNum, width, height, separated by tabs.</summary>
		public static string GetPath() {
			if(!string.IsNullOrEmpty(PathOverride)) {
				return PathOverride;
			}
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),CodeBase.ProgramNames.LocalAppDataFolderName,"WindowSizes.txt");
		}

		///<summary>The remembered size (width, height at 96 dpi) of the window for the user, or null when none is remembered.</summary>
		public static Tuple<int,int> Get(string windowName,long userNum) {
			Dictionary<string,Tuple<int,int>> dict=ReadAll();
			if(dict.TryGetValue(GetKey(windowName,userNum),out Tuple<int,int> size)) {
				return size;
			}
			return null;
		}

		///<summary>Remembers the size (at 96 dpi) of the window for the user. Sizes that are not positive are ignored. Returns false when the file
		///could not be written.</summary>
		public static bool Set(string windowName,long userNum,int width96,int height96) {
			if(string.IsNullOrWhiteSpace(windowName) || width96<=0 || height96<=0) {
				return false;
			}
			try {
				Dictionary<string,Tuple<int,int>> dict=ReadAll();
				dict[GetKey(windowName,userNum)]=Tuple.Create(width96,height96);
				string path=GetPath();
				string folder=Path.GetDirectoryName(path);
				if(!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) {
					Directory.CreateDirectory(folder);
				}
				StringBuilder stringBuilder=new StringBuilder();
				foreach(KeyValuePair<string,Tuple<int,int>> pair in dict.OrderBy(x => x.Key,StringComparer.Ordinal)) {
					stringBuilder.Append(pair.Key).Append('\t').Append(pair.Value.Item1.ToString(CultureInfo.InvariantCulture)).Append('\t')
						.Append(pair.Value.Item2.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
				}
				File.WriteAllText(path,stringBuilder.ToString(),new UTF8Encoding(false));
				return true;
			}
			catch(Exception) {
				return false;//Only a convenience.
			}
		}

		///<summary>The size to open a window at: the remembered size when there is one, never smaller than the minimum and never larger than the
		///available area (all in the same units). Null when nothing is remembered.</summary>
		public static Tuple<int,int> GetSizeToOpen(Tuple<int,int> sizeRemembered,int widthMin,int heightMin,int widthMax,int heightMax) {
			if(sizeRemembered==null) {
				return null;
			}
			int width=Math.Max(sizeRemembered.Item1,widthMin);
			int height=Math.Max(sizeRemembered.Item2,heightMin);
			if(widthMax>0) {
				width=Math.Min(width,Math.Max(widthMax,widthMin));
			}
			if(heightMax>0) {
				height=Math.Min(height,Math.Max(heightMax,heightMin));
			}
			return Tuple.Create(width,height);
		}

		private static string GetKey(string windowName,long userNum) {
			return (windowName??"").Replace("\t"," ").Trim()+"\t"+userNum.ToString(CultureInfo.InvariantCulture);
		}

		private static Dictionary<string,Tuple<int,int>> ReadAll() {
			Dictionary<string,Tuple<int,int>> dict=new Dictionary<string,Tuple<int,int>>(StringComparer.Ordinal);
			try {
				string path=GetPath();
				if(!File.Exists(path)) {
					return dict;
				}
				foreach(string line in File.ReadAllLines(path)) {
					string[] arrayParts=line.Split('\t');
					if(arrayParts.Length!=4
						|| !long.TryParse(arrayParts[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out long userNum)
						|| !int.TryParse(arrayParts[2],NumberStyles.Integer,CultureInfo.InvariantCulture,out int width)
						|| !int.TryParse(arrayParts[3],NumberStyles.Integer,CultureInfo.InvariantCulture,out int height)
						|| width<=0 || height<=0)
					{
						continue;//Ignore anything malformed.
					}
					dict[GetKey(arrayParts[0],userNum)]=Tuple.Create(width,height);
				}
			}
			catch(Exception) {
				//Unreadable: open at the default size.
			}
			return dict;
		}
	}
}
