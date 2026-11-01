// CodeBase.ODZip, the System.IO.Compression replacement for DotNetZip: round trips, the entry names DotNetZip used, and safe extraction.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BINBTests {
	[TestClass]
	public class ODZipTests {
		private static KeyValuePair<string,byte[]> Entry(string name,string content) {
			return new KeyValuePair<string,byte[]>(name,Encoding.UTF8.GetBytes(content));
		}

		///<summary>An archive written straight with ZipArchive, so hostile names are stored exactly as given.</summary>
		private static byte[] RawArchive(params KeyValuePair<string,byte[]>[] arrayEntries) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(ZipArchive zipArchive=new ZipArchive(memoryStream,ZipArchiveMode.Create,leaveOpen:true)) {
					foreach(KeyValuePair<string,byte[]> keyValuePair in arrayEntries) {
						using(Stream stream=zipArchive.CreateEntry(keyValuePair.Key).Open()) {
							stream.Write(keyValuePair.Value,0,keyValuePair.Value.Length);
						}
					}
				}
				return memoryStream.ToArray();
			}
		}

		[TestMethod]
		public void CreateThenExtractAll_RoundTripsFilesAndFolders() {
			byte[] arrayBig=new byte[200000];
			new Random(3).NextBytes(arrayBig);
			byte[] arrayArchive=ODZip.CreateArchive(new[] { Entry("a.txt","first"),new KeyValuePair<string,byte[]>("sub\\big.bin",arrayBig),
				Entry("sub/deeper/c.txt","third") });
			using(ZipArchive zipArchive=new ZipArchive(new MemoryStream(arrayArchive),ZipArchiveMode.Read)) {
				CollectionAssert.AreEqual(new[] { "a.txt","sub/big.bin","sub/deeper/c.txt" },zipArchive.Entries.Select(x => x.FullName).ToArray(),
					"Entries keep their order and use '/' separators.");
			}
			string folder=TestPaths.NewTempFolder("roundtrip");
			List<string> listPaths=ODZip.ExtractAll(new MemoryStream(arrayArchive),folder,overwrite:false);
			Assert.AreEqual(3,listPaths.Count);
			Assert.AreEqual("first",File.ReadAllText(Path.Combine(folder,"a.txt")));
			CollectionAssert.AreEqual(arrayBig,File.ReadAllBytes(Path.Combine(folder,"sub","big.bin")));
			Assert.AreEqual("third",File.ReadAllText(Path.Combine(folder,"sub","deeper","c.txt")));
		}

		[TestMethod]
		public void CreateArchiveFile_WritesAReadableZipFile() {
			string folder=TestPaths.NewTempFolder("file");
			string zipPath=Path.Combine(folder,"claims.zip");
			ODZip.CreateArchiveFile(zipPath,new[] { new KeyValuePair<string,byte[]>("claims.xml",ODZip.GetDefaultEncodingBytes("<x>caf\u00e9</x>")) });
			using(ZipArchive zipArchive=new ZipArchive(File.OpenRead(zipPath),ZipArchiveMode.Read)) {
				ZipArchiveEntry zipArchiveEntry=zipArchive.Entries.Single();
				Assert.AreEqual("claims.xml",zipArchiveEntry.FullName);
				using(MemoryStream memoryStream=new MemoryStream()) {
					using(Stream stream=zipArchiveEntry.Open()) {
						stream.CopyTo(memoryStream);
					}
					CollectionAssert.AreEqual(Encoding.Default.GetBytes("<x>caf\u00e9</x>"),memoryStream.ToArray(),
						"Text is stored in the default code page, as DotNetZip stored a string entry.");
				}
			}
		}

		[TestMethod]
		public void GetEntryNameWithFolders_IsThePathWithoutItsRoot() {
			Assert.AreEqual("Exports/claims/x.txt",ODZip.GetEntryNameWithFolders(@"C:\Exports\claims\x.txt"));
			Assert.AreEqual("x.txt",ODZip.GetEntryNameWithFolders(@"D:\x.txt"));
		}

		[TestMethod]
		public void GetSafeExtractPath_RefusesEveryPathOutsideTheFolder() {
			string folder=TestPaths.NewTempFolder("safe");
			Assert.AreEqual(Path.Combine(folder,"a.txt"),ODZip.GetSafeExtractPath(folder,"a.txt"));
			Assert.AreEqual(Path.Combine(folder,"sub","a.txt"),ODZip.GetSafeExtractPath(folder,"sub/a.txt"));
			Assert.AreEqual(Path.Combine(folder,"sub","a.txt"),ODZip.GetSafeExtractPath(folder,"sub\\a.txt"));
			Assert.AreEqual(Path.Combine(folder,"a.txt"),ODZip.GetSafeExtractPath(folder,"sub/../a.txt"),"A '..' that stays inside is allowed.");
			string[] arrayUnsafe={ "","  ","../a.txt","..\\a.txt","sub/../../a.txt","/a.txt","\\a.txt","C:/a.txt","C:a.txt","a.txt:stream",
				"\\\\server\\share\\a.txt",".","sub/..","a<b>.txt" };
			foreach(string name in arrayUnsafe) {
				Assert.IsNull(ODZip.GetSafeExtractPath(folder,name),"Not refused: '"+name+"'");
			}
			//A sibling folder whose name starts with the target's name is outside it.
			Assert.IsNull(ODZip.GetSafeExtractPath(folder,"..\\"+Path.GetFileName(folder)+"x\\a.txt"));
		}

		[TestMethod]
		public void ExtractAll_RefusesATraversalEntry_BeforeWritingAnything() {
			string folderParent=TestPaths.NewTempFolder("slip");
			string folder=Path.Combine(folderParent,"target");
			byte[] arrayArchive=RawArchive(Entry("good.txt","good"),Entry("../evil.txt","evil"),Entry("sub/ok.txt","ok"));
			IOException ioException=null;
			try {
				ODZip.ExtractAll(new MemoryStream(arrayArchive),folder,overwrite:true);
			}
			catch(IOException ex) {
				ioException=ex;
			}
			Assert.IsNotNull(ioException,"An archive with a path outside the folder must be refused.");
			StringAssert.Contains(ioException.Message,"../evil.txt");
			Assert.IsFalse(File.Exists(Path.Combine(folderParent,"evil.txt")));
			Assert.IsFalse(Directory.Exists(folder) && Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Length>0,
				"Nothing is extracted from an archive with an unsafe path.");
		}

		[TestMethod]
		public void ExtractEntry_RefusesUnsafePathsAndKeepsExistingFilesUnlessAsked() {
			string folderParent=TestPaths.NewTempFolder("entry");
			string folder=Path.Combine(folderParent,"target");
			Directory.CreateDirectory(folder);
			byte[] arrayArchive=RawArchive(Entry("..\\evil.txt","evil"),Entry("medu.txt","new"));
			File.WriteAllText(Path.Combine(folder,"medu.txt"),"old");
			using(ZipArchive zipArchive=new ZipArchive(new MemoryStream(arrayArchive),ZipArchiveMode.Read)) {
				Expect.Throws<IOException>(() => ODZip.ExtractEntry(zipArchive.Entries[0],folder,overwrite:true));
				Assert.IsFalse(File.Exists(Path.Combine(folderParent,"evil.txt")));
				Expect.Throws<IOException>(() => ODZip.ExtractEntry(zipArchive.Entries[1],folder,overwrite:false),
					"An existing file is not overwritten unless asked, as DotNetZip's ExtractAll threw.");
				Assert.AreEqual("old",File.ReadAllText(Path.Combine(folder,"medu.txt")));
				Assert.AreEqual(Path.Combine(folder,"medu.txt"),ODZip.ExtractEntry(zipArchive.Entries[1],folder,overwrite:true));
				Assert.AreEqual("new",File.ReadAllText(Path.Combine(folder,"medu.txt")));
			}
		}

		[TestMethod]
		public void ReadEntry_RefusesAnEntryThatInflatesBeyondTheLimit() {
			byte[] arrayArchive=ODZip.CreateArchive(new[] { new KeyValuePair<string,byte[]>("zeros.bin",new byte[1000000]) });
			Assert.IsTrue(arrayArchive.Length<20000,"A million zeros compress well; this is the shape of a zip bomb.");
			using(ZipArchive zipArchive=new ZipArchive(new MemoryStream(arrayArchive),ZipArchiveMode.Read)) {
				Expect.Throws<ODZip.EntryTooLargeException>(() => ODZip.ReadEntry(zipArchive.Entries[0],999999));
				Assert.AreEqual(1000000,ODZip.ReadEntry(zipArchive.Entries[0],1000000).Length);
			}
		}
	}
}
