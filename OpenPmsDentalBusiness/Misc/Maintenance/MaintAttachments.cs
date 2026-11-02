// Routine maintenance (docs/specs/routine-maintenance.md, "Result fields and validation"): where attachments are stored.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenPmsDentalBusiness.FileIO;

namespace OpenPmsDentalBusiness {
	///<summary>Attachments of maintenance results live in the images folder (A to Z folder, or its cloud equivalent) under
	///Maintenance\&lt;MaintItemNum&gt;\, named yyyyMMdd-HHmmss_&lt;original name&gt;. The recorded value holds the file name only. Files are written
	///and read through the FileAtoZ helpers, so local and cloud storage both work; when images are stored in the database there is no folder and
	///attachments are not available.</summary>
	public static class MaintAttachments {
		///<summary>The folder under the images folder.</summary>
		public const string FOLDER="Maintenance";
		///<summary>Longest original file name kept in the stored name.</summary>
		private const int MAX_NAME_LENGTH=150;

		///<summary>False when the office stores images in the database, so there is no folder to put attachments in.</summary>
		public static bool IsAvailable() {
			return PrefC.AtoZfolderUsed!=DataStorageType.InDatabase;
		}

		///<summary>The item's folder relative to the images folder: Maintenance\&lt;MaintItemNum&gt;.</summary>
		public static string GetFolderRelative(long maintItemNum) {
			return FileAtoZ.CombinePaths(FOLDER,maintItemNum.ToString(CultureInfo.InvariantCulture));
		}

		///<summary>The item's folder in the images folder. Null when attachments are not available.</summary>
		public static string GetFolder(long maintItemNum) {
			string pathAtoZ=FileAtoZ.GetPreferredAtoZpath();
			if(!IsAvailable() || string.IsNullOrEmpty(pathAtoZ)) {
				return null;
			}
			return FileAtoZ.CombinePaths(pathAtoZ,GetFolderRelative(maintItemNum));
		}

		///<summary>The full path of a stored attachment. Null when attachments are not available.</summary>
		public static string GetFilePath(long maintItemNum,string fileName) {
			string folder=GetFolder(maintItemNum);
			if(folder==null || string.IsNullOrEmpty(fileName)) {
				return null;
			}
			return FileAtoZ.CombinePaths(folder,fileName);
		}

		///<summary>The stored name of a file picked at dateTime: yyyyMMdd-HHmmss_ and the original name, with characters a file name cannot hold
		///replaced by '_' and the name shortened to 150 characters (keeping the extension).</summary>
		public static string MakeFileName(string pathOriginal,DateTime dateTime) {
			//The last part of the path, without Path.GetFileName, which throws on characters a path cannot hold.
			string name=pathOriginal??"";
			int indexSeparator=Math.Max(name.LastIndexOf('\\'),name.LastIndexOf('/'));
			if(indexSeparator>=0) {
				name=name.Substring(indexSeparator+1);
			}
			char[] arrayInvalid=Path.GetInvalidFileNameChars();
			name=new string(name.Select(x => arrayInvalid.Contains(x) ? '_' : x).ToArray()).Trim();
			if(name=="") {
				name="attachment";
			}
			if(name.Length>MAX_NAME_LENGTH) {
				string extension=Path.GetExtension(name);
				if(extension.Length>20) {
					extension="";
				}
				name=Path.GetFileNameWithoutExtension(name);
				name=name.Substring(0,Math.Max(1,MAX_NAME_LENGTH-extension.Length))+extension;
			}
			return dateTime.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+"_"+name;
		}
	}
}
