// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ImagingDeviceManager.Journal {
	///<summary>The capture log, Capture\capture-log.txt: what the library logs about device acquisitions (refusals, contract deviations,
	///images drained to the quarantine, journal problems, "may still hold images"). One line per entry with the local time and the Windows
	///user. Above 1 MB the file becomes capture-log.1.txt, replacing the previous one. A warning about the Capture folder (it is usable,
	///but other Windows users may not be able to change its files) is the first line written, once per process. Writing never fails an
	///acquisition: problems go to the debug trace only.</summary>
	internal sealed class CaptureLog {
		public const string FileName="capture-log.txt";
		public const string FileNamePrevious="capture-log.1.txt";
		public const long MaxBytes=1024*1024;
		private readonly CaptureFolders _folders;
		private readonly Func<DateTime> _funcNow;
		private readonly object _lock=new object();
		///<summary>The folder once it was made usable; null until then, and again after a write failed.</summary>
		private string _folder;
		///<summary>The folder's warning has been written to the log.</summary>
		private bool _isFolderWarningLogged;

		public CaptureLog(CaptureFolders folders,Func<DateTime> funcNow) {
			_folders=folders;
			_funcNow=funcNow??(() => DateTime.Now);
		}

		public void Write(string text) {
			Trace.WriteLine("ImagingDeviceManager: "+text);
			lock(_lock) {
				try {
					string warningFolder=null;
					if(_folder==null) {
						string folder;
						string error;
						if(!_folders.Ensure(CaptureFolders.SubfolderCapture,out folder,out error)) {
							Trace.WriteLine("ImagingDeviceManager: the capture log could not be written, its folder is not usable: "+error);
							return;
						}
						_folder=folder;
						if(!string.IsNullOrEmpty(error) && !_isFolderWarningLogged) {
							_isFolderWarningLogged=true;
							warningFolder=error;
						}
					}
					string path=Path.Combine(_folder,FileName);
					FileInfo fileInfo=new FileInfo(path);
					if(fileInfo.Exists && fileInfo.Length>MaxBytes) {
						string pathPrevious=Path.Combine(_folder,FileNamePrevious);
						File.Delete(pathPrevious);
						File.Move(path,pathPrevious);
					}
					string lines=(warningFolder==null ? "" : FormatLine(warningFolder))+FormatLine(text);
					File.AppendAllText(path,lines,new UTF8Encoding(false));
				}
				catch(Exception ex) {
					_folder=null;//Ensure the folder again next time; it may have been removed.
					Trace.WriteLine("ImagingDeviceManager: the capture log could not be written: "+ex.Message);
				}
			}
		}

		///<summary>One line of the log: the local time, the Windows user and the text.</summary>
		private string FormatLine(string text) {
			return _funcNow().ToString("yyyy-MM-dd HH:mm:ss.fff",System.Globalization.CultureInfo.InvariantCulture)+" "
				+CaptureRecord.GetWindowsUser()+" "+CaptureFiles.Clean(text)+"\r\n";
		}
	}
}
