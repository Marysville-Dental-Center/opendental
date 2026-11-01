// B-SHELL (v25_3-tools-file-monitor): the File Monitor's capture of reads and writes to the image (A to Z) folder.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>What a File Monitor row did to the image folder.</summary>
	public enum FileMonitorAction {
		Read,
		Write,
		Delete,
		Copy,
		Move,
		CreateDirectory,
		Exists,
		List,
	}

	///<summary>One row of the File Monitor feed.</summary>
	public class FileMonitorEntry {
		///<summary>Local time the operation started.</summary>
		public DateTime DateTimeStart;
		public FileMonitorAction Action;
		///<summary>Relative to the A to Z root when the path is inside it, otherwise the full path. Can name a patient folder.</summary>
		public string Path="";
		///<summary>Size of the file read or written, or -1 when it is not known (cloud storage, folders, failed operations).</summary>
		public long Bytes=-1;
		public double DurationMs;
		///<summary>"OK", or the error.</summary>
		public string Result="OK";

		public bool IsError {
			get { return Result!="OK"; }
		}
	}

	///<summary>Captures file operations against the image folder for the File Monitor window (Help, File Monitor). Capture is in-process only: the
	///window sees only what this program instance does. FileAtoZ and ImageStore call Begin at the top of each operation:
	///<code>using FileMonitorScope fileMonitorScope=FileMonitor.Begin(FileMonitorAction.Read,fileFullPath);</code>
	///While nobody captures, Begin only reads one static bool and returns null, so the operation costs nothing extra.
	///Nothing is ever written to disk here; paths can contain patient folder names.</summary>
	public static class FileMonitor {
		private static volatile bool _isCapturing;
		private static string _atoZRoot="";
		private static volatile bool _doReadFileSizes;
		private static readonly object _lock=new object();
		///<summary>The innermost open scope on this thread. Lets the first-chance exception handler attach the error message to the right row.</summary>
		[ThreadStatic]
		private static FileMonitorScope _scopeCurrent;

		///<summary>Raised on the thread that did the operation, after it finished. Handlers must be quick and must not touch UI controls directly.</summary>
		public static event Action<FileMonitorEntry> EntryCaptured;

		public static bool IsCapturing {
			get { return _isCapturing; }
		}

		///<summary>The A to Z root that paths are shown relative to. Set by StartCapture.</summary>
		public static string AtoZRoot {
			get { return _atoZRoot; }
		}

		///<summary>False for cloud storage, where the size of a file is not cheap to read. Set by StartCapture.</summary>
		internal static bool DoReadFileSizes {
			get { return _doReadFileSizes; }
		}

		///<summary>Turns capture on. atoZRoot is the current A to Z path (FileAtoZ.GetPreferredAtoZpath), used only to shorten paths. With
		///doReadFileSizes, reads and writes show the file's size, read from the file system after the operation; pass false for cloud storage.</summary>
		public static void StartCapture(string atoZRoot,bool doReadFileSizes) {
			lock(_lock) {
				_atoZRoot=atoZRoot??"";
				_doReadFileSizes=doReadFileSizes;
				if(_isCapturing) {
					return;
				}
				AppDomain.CurrentDomain.FirstChanceException+=FirstChanceException;
				_isCapturing=true;
			}
		}

		public static void StopCapture() {
			lock(_lock) {
				if(!_isCapturing) {
					return;
				}
				_isCapturing=false;
				AppDomain.CurrentDomain.FirstChanceException-=FirstChanceException;
			}
		}

		///<summary>Starts timing one operation. Returns null while nobody captures; "using" on null is allowed.</summary>
		public static FileMonitorScope Begin(FileMonitorAction action,string path) {
			if(!_isCapturing) {
				return null;
			}
			return new FileMonitorScope(action,path);
		}

		///<summary>Same as Begin(action,path), for callers that hold a folder and a file name. They are combined only while capturing.</summary>
		public static FileMonitorScope Begin(FileMonitorAction action,string folder,string fileName) {
			if(!_isCapturing) {
				return null;
			}
			string path;
			try {
				path=ODFileUtils.CombinePaths(folder??"",fileName??"");
			}
			catch(Exception ex) {
				ex.DoNothing();
				path=(folder??"")+"\\"+(fileName??"");
			}
			return new FileMonitorScope(action,path);
		}

		///<summary>For Copy and Move: the row shows "from -> to" and the size of the destination file.</summary>
		public static FileMonitorScope BeginTransfer(FileMonitorAction action,string pathFrom,string pathTo) {
			if(!_isCapturing) {
				return null;
			}
			return new FileMonitorScope(action,pathFrom,pathTo);
		}

		///<summary>Shortens a path to be relative to the A to Z root when it lies inside it. Case-insensitive; both slash kinds count.</summary>
		public static string GetRelativePath(string path,string atoZRoot) {
			if(string.IsNullOrEmpty(path)) {
				return "";
			}
			if(string.IsNullOrWhiteSpace(atoZRoot)) {
				return path;
			}
			string root=atoZRoot.TrimEnd('\\','/');
			if(root=="") {
				return path;
			}
			if(path.Length>root.Length && path.StartsWith(root,StringComparison.OrdinalIgnoreCase)) {
				char next=path[root.Length];
				if(next=='\\' || next=='/') {
					return path.Substring(root.Length+1);
				}
			}
			if(string.Equals(path.TrimEnd('\\','/'),root,StringComparison.OrdinalIgnoreCase)) {
				return "";
			}
			return path;
		}

		internal static void PushScope(FileMonitorScope scope) {
			scope.ScopeOuter=_scopeCurrent;
			_scopeCurrent=scope;
		}

		internal static void PopScope(FileMonitorScope scope) {
			if(_scopeCurrent==scope) {
				_scopeCurrent=scope.ScopeOuter;
			}
		}

		///<summary>Publishes a finished row. Never throws: a broken listener must not break file access.</summary>
		internal static void Publish(FileMonitorEntry entry) {
			try {
				EntryCaptured?.Invoke(entry);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		private static void FirstChanceException(object sender,System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) {
			try {
				FileMonitorScope scope=_scopeCurrent;
				if(scope!=null && e.Exception!=null) {
					scope.LastExceptionMessage=e.Exception.Message;
				}
			}
			catch {
				//Never throw from a first-chance handler.
			}
		}
	}

	///<summary>Times one file operation for the File Monitor. Dispose (the end of the "using") publishes the row. Whether the operation failed is
	///read from the exception that is unwinding through the "using" at that moment, so a caller needs only the one line at the top.</summary>
	public sealed class FileMonitorScope:IDisposable {
		private readonly Stopwatch _stopwatch;
		private readonly FileMonitorEntry _entry;
		private readonly string _pathFull;
		private bool _isDisposed;
		internal FileMonitorScope ScopeOuter;
		///<summary>The message of the last exception thrown on this thread while the scope was open.</summary>
		internal string LastExceptionMessage;

		internal FileMonitorScope(FileMonitorAction action,string path) : this(action,path,null) {
		}

		///<summary>pathTo is set for Copy and Move only.</summary>
		internal FileMonitorScope(FileMonitorAction action,string path,string pathTo) {
			_entry=new FileMonitorEntry();
			_entry.DateTimeStart=DateTime.Now;
			_entry.Action=action;
			_entry.Path=FileMonitor.GetRelativePath(path??"",FileMonitor.AtoZRoot);
			_pathFull=path??"";
			if(pathTo!=null) {
				_entry.Path+=" -> "+FileMonitor.GetRelativePath(pathTo,FileMonitor.AtoZRoot);
				_pathFull=pathTo;//The size shown is the destination's.
			}
			FileMonitor.PushScope(this);
			_stopwatch=Stopwatch.StartNew();
		}

		///<summary>The size in bytes, when the caller knows it. Otherwise Dispose reads it from the file for reads, writes, copies and moves on local
		///storage.</summary>
		public void SetBytes(long bytes) {
			_entry.Bytes=bytes;
		}

		public void Dispose() {
			if(_isDisposed) {
				return;
			}
			_isDisposed=true;
			_stopwatch.Stop();
			FileMonitor.PopScope(this);
			try {
				_entry.DurationMs=_stopwatch.Elapsed.TotalMilliseconds;
				//Non-zero while an exception unwinds through the "using" that owns this scope.
				bool isFailing=Marshal.GetExceptionPointers()!=IntPtr.Zero;
				if(isFailing) {
					_entry.Result=string.IsNullOrWhiteSpace(LastExceptionMessage) ? "Error" : LastExceptionMessage;
				}
				else if(_entry.Bytes<0 && HasFileSize(_entry.Action) && FileMonitor.DoReadFileSizes) {
					_entry.Bytes=GetFileSize(_pathFull);
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			FileMonitor.Publish(_entry);
		}

		///<summary>Actions whose row shows the size of the file.</summary>
		public static bool HasFileSize(FileMonitorAction action) {
			return action==FileMonitorAction.Read || action==FileMonitorAction.Write || action==FileMonitorAction.Copy || action==FileMonitorAction.Move;
		}

		private static long GetFileSize(string path) {
			try {
				FileInfo fileInfo=new FileInfo(path);
				if(fileInfo.Exists) {
					return fileInfo.Length;
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			return -1;
		}
	}

	///<summary>The rows the File Monitor window shows: the newest MaxRows, oldest first. Not thread safe; the window fills it on its UI thread.</summary>
	public class FileMonitorFeed {
		///<summary>Rows kept, from the 25.3 description of the File Monitor (our number; Open Dental's is not public).</summary>
		public const int MAX_ROWS=5000;
		private readonly Queue<FileMonitorEntry> _queueEntries=new Queue<FileMonitorEntry>();
		private readonly int _maxRows;

		public FileMonitorFeed() : this(MAX_ROWS) {
		}

		public FileMonitorFeed(int maxRows) {
			_maxRows=Math.Max(1,maxRows);
		}

		public int Count {
			get { return _queueEntries.Count; }
		}

		///<summary>Adds a row, dropping the oldest ones past the limit. Returns how many were dropped.</summary>
		public int Add(FileMonitorEntry entry) {
			if(entry==null) {
				return 0;
			}
			_queueEntries.Enqueue(entry);
			int countDropped=0;
			while(_queueEntries.Count>_maxRows) {
				_queueEntries.Dequeue();
				countDropped++;
			}
			return countDropped;
		}

		public void Clear() {
			_queueEntries.Clear();
		}

		///<summary>Oldest first.</summary>
		public List<FileMonitorEntry> GetEntries() {
			return _queueEntries.ToList();
		}

		///<summary>Bytes column text: blank when unknown.</summary>
		public static string FormatBytes(long bytes) {
			if(bytes<0) {
				return "";
			}
			return bytes.ToString("N0");
		}

		///<summary>Time column text, with milliseconds.</summary>
		public static string FormatTime(DateTime dateTime) {
			return dateTime.ToString("HH:mm:ss.fff");
		}

		///<summary>Duration column text in milliseconds.</summary>
		public static string FormatDuration(double durationMs) {
			return durationMs.ToString("0.0");
		}
	}
}
