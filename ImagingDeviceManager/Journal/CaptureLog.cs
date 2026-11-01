// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace ImagingDeviceManager.Journal {
	///<summary>The capture log, Capture\capture-log.txt: what the library logs about device acquisitions (refusals, contract deviations,
	///images drained to the quarantine, journal problems, "may still hold images"). One line per entry with the local time and the Windows
	///user. Above 1 MB the file becomes capture-log.1.txt, replacing the previous one. A warning about the Capture folder (it is usable,
	///but other Windows users may not be able to change its files) is the first line written, once per process. Writing never fails an
	///acquisition: problems go to the debug trace only.
	///
	///Several instances write the same file in one process (EZTwain's, each TwainCaptureSession's, and CaptureLogFile's for the capture
	///session window), from several threads: every write, the size check and the rollover are under one lock for the process, so no line is
	///lost to a sharing violation and a rollover never deletes the log another writer has just rolled over. Another process (a second Open
	///Dental on a terminal server) is retried a few times when it holds the file.</summary>
	internal sealed class CaptureLog {
		public const string FileName="capture-log.txt";
		public const string FileNamePrevious="capture-log.1.txt";
		public const long MaxBytes=1024*1024;
		///<summary>A writer of another process may hold the file for a moment: tries, and the wait between them.</summary>
		private const int CountWriteAttempts=3;
		private const int WriteRetryMs=20;
		///<summary>For every instance of the process: they write the same file.</summary>
		private static readonly object _lockFile=new object();
		private readonly CaptureFolders _folders;
		private readonly Func<DateTime> _funcNow;
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
			lock(_lockFile) {
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
					RollOver(path,Path.Combine(_folder,FileNamePrevious));
					string lines=(warningFolder==null ? "" : FormatLine(warningFolder))+FormatLine(text);
					Append(path,new UTF8Encoding(false).GetBytes(lines));
				}
				catch(Exception ex) {
					_folder=null;//Ensure the folder again next time; it may have been removed.
					Trace.WriteLine("ImagingDeviceManager: the capture log could not be written: "+ex.Message);
				}
			}
		}

		///<summary>Above MaxBytes the log becomes capture-log.1.txt, replacing the previous one in one step (File.Replace), so there is no moment
		///without either. The size is read right before, under the lock.</summary>
		private static void RollOver(string path,string pathPrevious) {
			FileInfo fileInfo=new FileInfo(path);
			if(!fileInfo.Exists || fileInfo.Length<=MaxBytes) {
				return;
			}
			if(File.Exists(pathPrevious)) {
				File.Replace(path,pathPrevious,null);
			}
			else {
				File.Move(path,pathPrevious);
			}
		}

		///<summary>Appends bytes to path, opened for this write only (other readers allowed); a writer of another process that holds the file is
		///waited for a few times.</summary>
		private static void Append(string path,byte[] bytes) {
			for(int attempt=1;;attempt++) {
				try {
					using(FileStream fileStream=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read)) {
						fileStream.Write(bytes,0,bytes.Length);
					}
					return;
				}
				catch(IOException) when(attempt<CountWriteAttempts) {
					Thread.Sleep(WriteRetryMs);
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
