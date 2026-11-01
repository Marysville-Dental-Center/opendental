// v25_3-tools-file-monitor: the File Monitor's in-process capture of image folder reads and writes.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.FileIO;

namespace BSHELLTests {
	[TestClass]
	public class FileMonitorTests {
		private readonly List<FileMonitorEntry> _listEntries=new List<FileMonitorEntry>();
		private readonly object _lock=new object();
		private string _folderRoot;

		[TestInitialize]
		public void TestInitialize() {
			_folderRoot=Path.Combine(Path.GetTempPath(),"bshell_filemonitor_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folderRoot);
			FileMonitor.EntryCaptured+=OnEntry;
		}

		[TestCleanup]
		public void TestCleanup() {
			FileMonitor.EntryCaptured-=OnEntry;
			FileMonitor.StopCapture();
			try {
				Directory.Delete(_folderRoot,true);
			}
			catch(Exception) {
				//Temp files only.
			}
		}

		private void OnEntry(FileMonitorEntry entry) {
			lock(_lock) {
				_listEntries.Add(entry);
			}
		}

		private List<FileMonitorEntry> Entries() {
			lock(_lock) {
				return _listEntries.ToList();
			}
		}

		[TestMethod]
		public void NothingCaptured_WhileNobodyListens() {
			Assert.IsFalse(FileMonitor.IsCapturing);
			Assert.IsNull(FileMonitor.Begin(FileMonitorAction.Read,"x"),"Zero cost: no scope at all.");
			Assert.IsNull(FileMonitor.Begin(FileMonitorAction.Read,"folder","file"));
			Assert.IsNull(FileMonitor.BeginTransfer(FileMonitorAction.Copy,"a","b"));
			using(FileMonitorScope scope=FileMonitor.Begin(FileMonitorAction.Write,"x")) {
				//"using" on null is allowed, which is what FileAtoZ relies on.
			}
			Assert.AreEqual(0,Entries().Count);
		}

		[TestMethod]
		public void Capture_RelativePath_Bytes_Duration_Ok() {
			string path=Path.Combine(_folderRoot,"A","AbcPatient1","img.jpg");
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllBytes(path,new byte[1234]);
			FileMonitor.StartCapture(_folderRoot,doReadFileSizes:true);
			using(FileMonitor.Begin(FileMonitorAction.Read,path)) {
				File.ReadAllBytes(path);
			}
			FileMonitorEntry entry=Entries().Single();
			Assert.AreEqual(FileMonitorAction.Read,entry.Action);
			Assert.AreEqual(Path.Combine("A","AbcPatient1","img.jpg"),entry.Path,"Relative to the A to Z root.");
			Assert.AreEqual(1234,entry.Bytes);
			Assert.AreEqual("OK",entry.Result);
			Assert.IsFalse(entry.IsError);
			Assert.IsTrue(entry.DurationMs>=0);
			Assert.IsTrue((DateTime.Now-entry.DateTimeStart).TotalMinutes<1);
		}

		[TestMethod]
		public void Capture_Failure_ShowsTheError() {
			FileMonitor.StartCapture(_folderRoot,doReadFileSizes:true);
			string pathMissing=Path.Combine(_folderRoot,"missing.jpg");
			try {
				using(FileMonitor.Begin(FileMonitorAction.Read,pathMissing)) {
					File.ReadAllBytes(pathMissing);
				}
				Assert.Fail("Should have thrown.");
			}
			catch(FileNotFoundException) {
			}
			FileMonitorEntry entry=Entries().Single();
			Assert.IsTrue(entry.IsError);
			StringAssert.Contains(entry.Result,"missing.jpg");
			Assert.AreEqual(-1,entry.Bytes);
		}

		[TestMethod]
		public void Capture_CaughtExceptionInside_StillOk() {
			FileMonitor.StartCapture(_folderRoot,doReadFileSizes:false);
			using(FileMonitor.Begin(FileMonitorAction.Exists,_folderRoot)) {
				try {
					throw new InvalidOperationException("handled inside");
				}
				catch(InvalidOperationException) {
				}
			}
			Assert.AreEqual("OK",Entries().Single().Result,"Only an exception that leaves the operation marks it failed.");
		}

		[TestMethod]
		public void Capture_TransferRow_AndNoSizesForCloud() {
			string pathFrom=Path.Combine(_folderRoot,"a.txt");
			string pathTo=Path.Combine(_folderRoot,"b.txt");
			File.WriteAllText(pathFrom,"12345");
			FileMonitor.StartCapture(_folderRoot,doReadFileSizes:true);
			using(FileMonitor.BeginTransfer(FileMonitorAction.Copy,pathFrom,pathTo)) {
				File.Copy(pathFrom,pathTo);
			}
			FileMonitorEntry entry=Entries().Single();
			Assert.AreEqual("a.txt -> b.txt",entry.Path);
			Assert.AreEqual(5,entry.Bytes,"The destination's size.");
			FileMonitor.StopCapture();
			FileMonitor.StartCapture(_folderRoot,doReadFileSizes:false);
			using(FileMonitor.Begin(FileMonitorAction.Read,pathTo)) {
			}
			Assert.AreEqual(-1,Entries().Last().Bytes,"Cloud storage: no size lookups.");
		}

		[TestMethod]
		public void StopCapture_StopsRows() {
			FileMonitor.StartCapture(_folderRoot,doReadFileSizes:false);
			using(FileMonitor.Begin(FileMonitorAction.List,_folderRoot)) {
			}
			FileMonitor.StopCapture();
			using(FileMonitor.Begin(FileMonitorAction.List,_folderRoot)) {
			}
			Assert.AreEqual(1,Entries().Count);
			Assert.IsFalse(FileMonitor.IsCapturing);
		}

		[TestMethod]
		public void OtherThreads_AreCapturedToo_InThisProcessOnly() {
			FileMonitor.StartCapture(_folderRoot,doReadFileSizes:false);
			Thread thread=new Thread(() => {
				using(FileMonitor.Begin(FileMonitorAction.Exists,Path.Combine(_folderRoot,"x"))) {
				}
			});
			thread.Start();
			thread.Join();
			Assert.AreEqual(1,Entries().Count);
		}

		[TestMethod]
		public void RelativePath_Cases() {
			Assert.AreEqual(@"A\Abc1\x.jpg",FileMonitor.GetRelativePath(@"\\server\OpenDentImages\A\Abc1\x.jpg",@"\\server\OpenDentImages"));
			Assert.AreEqual(@"A\Abc1\x.jpg",FileMonitor.GetRelativePath(@"\\SERVER\opendentimages\A\Abc1\x.jpg",@"\\server\OpenDentImages\"),"Ignores case.");
			Assert.AreEqual("A/Abc1/x.jpg",FileMonitor.GetRelativePath("/OpenDentImages/A/Abc1/x.jpg","/OpenDentImages"),"Cloud paths use '/'.");
			Assert.AreEqual(@"C:\Temp\x.jpg",FileMonitor.GetRelativePath(@"C:\Temp\x.jpg",@"\\server\OpenDentImages"),"Outside the root: full path.");
			Assert.AreEqual(@"\\server\OpenDentImagesOld\x.jpg",FileMonitor.GetRelativePath(@"\\server\OpenDentImagesOld\x.jpg",@"\\server\OpenDentImages"),
				"A folder that only starts with the root's name is not inside it.");
			Assert.AreEqual("",FileMonitor.GetRelativePath(@"\\server\OpenDentImages\",@"\\server\OpenDentImages"));
			Assert.AreEqual("x",FileMonitor.GetRelativePath("x",""));
			Assert.AreEqual("",FileMonitor.GetRelativePath(null,"root"));
		}

		[TestMethod]
		public void Feed_KeepsTheNewest5000() {
			FileMonitorFeed feed=new FileMonitorFeed();
			int countDropped=0;
			for(int i=0;i<5003;i++) {
				countDropped+=feed.Add(new FileMonitorEntry { Path="p"+i });
			}
			Assert.AreEqual(FileMonitorFeed.MAX_ROWS,feed.Count);
			Assert.AreEqual(5000,FileMonitorFeed.MAX_ROWS);
			Assert.AreEqual(3,countDropped);
			List<FileMonitorEntry> listEntries=feed.GetEntries();
			Assert.AreEqual("p3",listEntries.First().Path,"Oldest kept first.");
			Assert.AreEqual("p5002",listEntries.Last().Path);
			Assert.AreEqual(0,feed.Add(null));
			feed.Clear();
			Assert.AreEqual(0,feed.Count);
			Assert.AreEqual("",FileMonitorFeed.FormatBytes(-1));
			Assert.AreEqual("1,234",FileMonitorFeed.FormatBytes(1234));
			Assert.AreEqual("13:04:05.067",FileMonitorFeed.FormatTime(new DateTime(2026,10,25,13,4,5,67)));
		}

		[TestMethod]
		public void FileAtoZ_And_ImageStore_RaiseRows() {
			ScratchDb.RequireDb();//PrefC.AtoZfolderUsed: local A to Z.
			FileMonitor.StartCapture(_folderRoot,doReadFileSizes:true);
			string folderPat=Path.Combine(_folderRoot,"A","AbcPatient1");
			FileAtoZ.CreateDirectory(folderPat);
			string path=Path.Combine(folderPat,"note.txt");
			FileAtoZ.WriteAllText(path,"hello");
			Assert.AreEqual("hello",FileAtoZ.ReadAllText(path));
			FileAtoZ.WriteAllBytes(path,new byte[] { 1,2,3 });
			FileAtoZ.ReadAllBytes(path);
			Assert.IsTrue(FileAtoZ.Exists(path));
			Assert.IsTrue(FileAtoZ.DirectoryExists(folderPat));
			FileAtoZ.GetFilesInDirectory(folderPat);
			string pathCopy=Path.Combine(folderPat,"copy.txt");
			FileAtoZ.Copy(path,pathCopy,FileAtoZSourceDestination.AtoZToAtoZ);
			string pathMoved=Path.Combine(folderPat,"moved.txt");
			FileAtoZ.Move(pathCopy,pathMoved);
			FileAtoZ.Delete(pathMoved);
			string pathImage=Path.Combine(folderPat,"img.png");
			using(Bitmap bitmap=new Bitmap(4,4)) {
				bitmap.Save(pathImage,ImageFormat.Png);
			}
			using(FileAtoZ.GetImage(pathImage)) {
			}
			Document document=new Document { FileName="img.png" };
			byte[] arrayBytes=ImageStore.GetBytes(document,folderPat);
			using(ImageStore.OpenImage(document,folderPat)) {
			}
			List<FileMonitorEntry> listEntries=Entries();
			CollectionAssert.AreEqual(new List<FileMonitorAction> {
					FileMonitorAction.CreateDirectory,FileMonitorAction.Write,FileMonitorAction.Read,FileMonitorAction.Write,FileMonitorAction.Read,
					FileMonitorAction.Exists,FileMonitorAction.Exists,FileMonitorAction.List,FileMonitorAction.Copy,FileMonitorAction.Move,
					FileMonitorAction.Delete,FileMonitorAction.Read,FileMonitorAction.Read,FileMonitorAction.Read },
				listEntries.Select(x => x.Action).ToList(),string.Join("\r\n",listEntries.Select(x => x.Action+" "+x.Path)));
			Assert.IsTrue(listEntries.All(x => x.Result=="OK"));
			Assert.AreEqual(Path.Combine("A","AbcPatient1","note.txt"),listEntries[1].Path);
			Assert.AreEqual(3,listEntries[4].Bytes,"Read after the 3-byte write.");
			Assert.AreEqual(Path.Combine("A","AbcPatient1","img.png"),listEntries.Last().Path);
			Assert.AreEqual(arrayBytes.Length,listEntries.Last().Bytes);
		}

		[TestMethod]
		public void MenuAndWindow_Wiring() {
			string menus=TestPaths.ReadRepoFile("OpenDental","Main Modules","FormOpenDentalMenus.cs");
			string help=TestPaths.GetMethodBody(menus,"private void LayoutMenuHelp(MenuItemOD menuItemHelp)");
			StringAssert.Contains(help,"menuItemHelp.Add(\"File Monitor\",menuItemFileMonitor_Click);");
			string shell=TestPaths.ReadRepoFile("OpenDental","Main Modules","FormOpenDentalBShell.cs");
			string click=TestPaths.GetMethodBody(shell,"private void menuItemFileMonitor_Click(");
			StringAssert.Contains(click,"Security.IsAuthorized(EnumPermType.Setup)");
			StringAssert.Contains(click,"FormFileMonitor.ShowSingle(this);");
			string window=TestPaths.ReadRepoFile("OpenDental","Forms","FormFileMonitor.cs");
			StringAssert.Contains(window,"DataStorageType.InDatabase","Says so when images are stored in the database.");
			StringAssert.Contains(window,"FileMonitor.StopCapture();");
			Assert.IsFalse(window.Contains("File.Write") || window.Contains("FileAtoZ.Write"),"The feed is never written to disk.");
			string designer=TestPaths.ReadRepoFile("OpenDental","Forms","FormFileMonitor.designer.cs");
			StringAssert.Contains(designer,"this.butStart.Text = \"Start\";","Capture is off when the window opens.");
		}
	}
}
