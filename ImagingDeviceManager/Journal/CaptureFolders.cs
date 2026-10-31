// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using ImagingDeviceManager.Config;

namespace ImagingDeviceManager.Journal {
	///<summary>The capture folders under %ProgramData%\OpenPMS-Dental\Imaging (design 6.1 and 7.12): Capture\journal, Capture\quarantine
	///and Capture itself for the capture log. They are created only through LocalCaptureConfig.EnsureImagingFolder, which gives the local
	///Users group Modify, because every Windows user of the computer must be able to write and recover them.</summary>
	internal sealed class CaptureFolders {
		public const string SubfolderCapture="Capture";
		public const string SubfolderJournal=@"Capture\journal";
		public const string SubfolderQuarantine=@"Capture\quarantine";
		///<summary>The Imaging folder, or null when it cannot be determined.</summary>
		private readonly string _folderImaging;
		///<summary>False for tests: a folder standing in for the Imaging folder, whose ACL is never changed.</summary>
		private readonly bool _isDefault;
		///<summary>For tests that are not the default: the steps that add and check for the Users rule, or null to change no ACL.</summary>
		private readonly Func<string,string> _funcGrantUsersModify;
		private readonly Func<string,bool> _funcHasUsersModify;

		private CaptureFolders(string folderImaging,bool isDefault,Func<string,string> funcGrantUsersModify=null,
			Func<string,bool> funcHasUsersModify=null)
		{
			_folderImaging=string.IsNullOrWhiteSpace(folderImaging) ? null : folderImaging;
			_isDefault=isDefault;
			_funcGrantUsersModify=funcGrantUsersModify;
			_funcHasUsersModify=funcHasUsersModify;
		}

		///<summary>The folders under %ProgramData%\OpenPMS-Dental\Imaging, the folder of capture.ini.</summary>
		public static CaptureFolders CreateDefault() {
			string pathConfig=LocalCaptureConfig.DefaultPath;
			return new CaptureFolders(pathConfig==null ? null : Path.GetDirectoryName(pathConfig),true);
		}

		///<summary>For tests: folderImaging stands in for the Imaging folder, and no ACL is changed.</summary>
		public static CaptureFolders CreateForFolder(string folderImaging) {
			return new CaptureFolders(folderImaging,false);
		}

		///<summary>For tests: as CreateForFolder, with stand-ins for the steps that add and check for the Users rule.</summary>
		public static CaptureFolders CreateForFolder(string folderImaging,Func<string,string> funcGrantUsersModify,
			Func<string,bool> funcHasUsersModify)
		{
			return new CaptureFolders(folderImaging,false,funcGrantUsersModify,funcHasUsersModify);
		}

		///<summary>The full path of a capture subfolder, whether or not it exists. Null when the Imaging folder is unknown.</summary>
		public string GetPath(string subfolder) {
			if(_folderImaging==null) {
				return null;
			}
			return Path.Combine(_folderImaging,subfolder);
		}

		///<summary>Creates the subfolder if it is missing (LocalCaptureConfig.EnsureImagingFolder). Returns false when it is unusable, with
		///error. Returns true when it is usable; error is then "" or a warning to log (other Windows users may not be able to change its
		///files). Never throws.</summary>
		public bool Ensure(string subfolder,out string path,out string error) {
			path=GetPath(subfolder);
			if(path==null) {
				error="The capture folder could not be determined.";
				return false;
			}
			try {
				if(_isDefault) {
					return LocalCaptureConfig.EnsureImagingFolder(subfolder,out error);
				}
				return LocalCaptureConfig.EnsureImagingFolder(_folderImaging,subfolder,_funcGrantUsersModify,_funcHasUsersModify,out error);
			}
			catch(Exception ex) {
				error=ex.Message;
				return false;
			}
		}
	}
}
