// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Threading;
using ImagingDeviceManager.Journal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>CaptureLog, Capture\capture-log.txt: several instances write it in one process (the library's, each capture session worker's and
	///the capture session window's), from several threads, and no line is lost. Temp folders only.</summary>
	[TestClass]
	public class CaptureLogTests {
		private string _folder;

		[TestInitialize]
		public void Setup() {
			_folder=Path.Combine(Path.GetTempPath(),"CaptureLogTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				Directory.Delete(_folder,true);
			}
			catch {
			}
		}

		[TestMethod]
		public void TwoInstancesOnTwoThreads_EveryLineIsInTheFile() {
			CaptureFolders folders=CaptureFolders.CreateForFolder(_folder);
			CaptureLog logWorker=new CaptureLog(folders,null);//a TwainCaptureSession worker's
			CaptureLog logWindow=new CaptureLog(folders,null);//CaptureLogFile's, for the capture session window
			const int countLines=500;
			using(ManualResetEvent eventStart=new ManualResetEvent(false)) {
				Thread threadWorker=new Thread(() => {
					eventStart.WaitOne();
					for(int i=0;i<countLines;i++) {
						logWorker.Write("worker line "+i);
					}
				});
				Thread threadWindow=new Thread(() => {
					eventStart.WaitOne();
					for(int i=0;i<countLines;i++) {
						logWindow.Write("window line "+i);
					}
				});
				threadWorker.Start();
				threadWindow.Start();
				eventStart.Set();
				Assert.IsTrue(threadWorker.Join(60000),"worker");
				Assert.IsTrue(threadWindow.Join(60000),"window");
			}
			string[] arrayLines=File.ReadAllLines(Path.Combine(folders.GetPath(CaptureFolders.SubfolderCapture),CaptureLog.FileName));
			Assert.AreEqual(countLines,arrayLines.Count(x => x.Contains(" worker line ")),"No line of the worker is lost to a sharing violation.");
			Assert.AreEqual(countLines,arrayLines.Count(x => x.Contains(" window line ")),"No line of the window is lost.");
		}

		[TestMethod]
		public void AboveTheLimit_TheLogRollsOverOnce_ThePreviousReplaced() {
			CaptureFolders folders=CaptureFolders.CreateForFolder(_folder);
			string folderCapture=folders.GetPath(CaptureFolders.SubfolderCapture);
			Directory.CreateDirectory(folderCapture);
			string path=Path.Combine(folderCapture,CaptureLog.FileName);
			string pathPrevious=Path.Combine(folderCapture,CaptureLog.FileNamePrevious);
			File.WriteAllText(pathPrevious,"older\r\n");
			File.WriteAllText(path,new string('x',(int)CaptureLog.MaxBytes+10)+"\r\n");
			new CaptureLog(folders,null).Write("first after the limit");
			new CaptureLog(folders,null).Write("second");
			StringAssert.StartsWith(File.ReadAllText(pathPrevious),"xxxx","The full log became the previous one, replacing the older.");
			string[] arrayLines=File.ReadAllLines(path);
			Assert.IsTrue(new FileInfo(path).Length<10000,"Rolled over once: the second writer does not roll the new log over again.");
			Assert.AreEqual(1,arrayLines.Count(x => x.EndsWith("first after the limit")));
			Assert.AreEqual(1,arrayLines.Count(x => x.EndsWith(" second")));
		}

		[TestMethod]
		public void AboveTheLimit_ThePreviousLogIsHeldOpen_TheLineIsStillWritten_ALaterWriteRollsOver() {
			CaptureFolders folders=CaptureFolders.CreateForFolder(_folder);
			string folderCapture=folders.GetPath(CaptureFolders.SubfolderCapture);
			Directory.CreateDirectory(folderCapture);
			string path=Path.Combine(folderCapture,CaptureLog.FileName);
			string pathPrevious=Path.Combine(folderCapture,CaptureLog.FileNamePrevious);
			File.WriteAllText(pathPrevious,"older\r\n");
			File.WriteAllText(path,new string('x',(int)CaptureLog.MaxBytes+10)+"\r\n");
			CaptureLog captureLog=new CaptureLog(folders,null);
			//A log viewer a technician runs while troubleshooting: it reads capture-log.1.txt and lets nobody replace or delete it.
			using(new FileStream(pathPrevious,FileMode.Open,FileAccess.Read,FileShare.Read)) {
				captureLog.Write("written while the previous log is held");
			}
			StringAssert.EndsWith(File.ReadAllText(path),"written while the previous log is held\r\n","Not lost: appended to the full log.");
			Assert.AreEqual("older\r\n",File.ReadAllText(pathPrevious));
			captureLog.Write("after the viewer closed");
			StringAssert.StartsWith(File.ReadAllText(pathPrevious),"xxxx","The later write rolled the log over.");
			StringAssert.Contains(File.ReadAllText(pathPrevious),"written while the previous log is held");
			string[] arrayLines=File.ReadAllLines(path);
			Assert.AreEqual(1,arrayLines.Length,string.Join("\r\n",arrayLines));
			StringAssert.EndsWith(arrayLines[0]," after the viewer closed");
		}
	}
}
