// The release archive after the move from DotNetZip to System.IO.Compression (package BIN-B): the layout publishing writes, the round trip
// through extraction, and the refusal of entries that would leave the target folder. Synthetic files only; no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ReleaseFlowTests {
	[TestClass]
	public class ReleaseArchiveTests {
		///<summary>A fake installation folder: two program files, a special sub folder, and a local settings file that is never published.</summary>
		private static string MakeInstallFolder() {
			string folder=TestPaths.NewTempFolder("archive_install");
			File.WriteAllText(Path.Combine(folder,"OpenDental.exe"),"synthetic program "+Guid.NewGuid());
			byte[] arrayBig=new byte[300000];
			new Random(7).NextBytes(arrayBig);
			File.WriteAllBytes(Path.Combine(folder,"Big.dll"),arrayBig);
			File.WriteAllText(Path.Combine(folder,"FreeDentalConfig.xml"),"<local/>");
			Directory.CreateDirectory(Path.Combine(folder,"OpenDentalReplicationService"));
			File.WriteAllText(Path.Combine(folder,"OpenDentalReplicationService","shader.dat"),"3d");
			return folder;
		}

		private static ReleaseManifest Build(string folder,out byte[] arrayArchive) {
			ReleaseIdentity releaseIdentity=new ReleaseIdentity { ReleaseVersion="0.0",BuildId=ForkReleases.ComputeBuildId(folder) };
			return ForkReleases.BuildRelease(folder,releaseIdentity,"SERVER1",new DateTime(2026,11,1,8,0,0,DateTimeKind.Utc),out arrayArchive);
		}

		///<summary>An archive with the given entries (name and content) followed by the manifest's archive copy.</summary>
		private static byte[] MakeArchive(ReleaseManifest releaseManifest,params KeyValuePair<string,byte[]>[] arrayEntries) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(ZipArchive zipArchive=new ZipArchive(memoryStream,ZipArchiveMode.Create,leaveOpen:true)) {
					foreach(KeyValuePair<string,byte[]> keyValuePair in arrayEntries) {
						using(Stream stream=zipArchive.CreateEntry(keyValuePair.Key).Open()) {
							stream.Write(keyValuePair.Value,0,keyValuePair.Value.Length);
						}
					}
					using(Stream stream=zipArchive.CreateEntry(ReleaseManifest.FILE_NAME).Open()) {
						byte[] arrayManifest=Encoding.UTF8.GetBytes(releaseManifest.Serialize(includeArchiveHash:false));
						stream.Write(arrayManifest,0,arrayManifest.Length);
					}
				}
				return memoryStream.ToArray();
			}
		}

		private static KeyValuePair<string,byte[]> Entry(string name,string content) {
			return new KeyValuePair<string,byte[]>(name,Encoding.UTF8.GetBytes(content));
		}

		private static ReleaseManifest ManifestListing(params KeyValuePair<string,byte[]>[] arrayFiles) {
			ReleaseManifest releaseManifest=new ReleaseManifest { ReleaseVersion="0.0",BuildId=new string('a',64),CompatVersion="24.3.63.0",
				PublishedUtc=new DateTime(2026,11,1,8,0,0,DateTimeKind.Utc),PublishedBy="SERVER1" };
			foreach(KeyValuePair<string,byte[]> keyValuePair in arrayFiles) {
				releaseManifest.ListFiles.Add(new ReleaseManifestFile { RelativePath=keyValuePair.Key,Size=keyValuePair.Value.LongLength,
					Sha256=ReleaseManifest.ComputeSha256(keyValuePair.Value) });
			}
			return releaseManifest;
		}

		[TestMethod]
		public void BuildRelease_ArchiveLayout_FilesInOrderThenTheManifest() {
			string folder=MakeInstallFolder();
			ReleaseManifest releaseManifest=Build(folder,out byte[] arrayArchive);
			Assert.AreEqual(ReleaseManifest.ComputeSha256(arrayArchive),releaseManifest.ArchiveSha256);
			using(ZipArchive zipArchive=new ZipArchive(new MemoryStream(arrayArchive),ZipArchiveMode.Read)) {
				//The same layout DotNetZip wrote: every published file under its relative path with '/' separators, in GetReleaseFiles order, no
				//folder entries, and the manifest at the root last.
				List<string> listExpected=ForkReleases.GetReleaseFiles(folder).Select(x => x.Replace('\\','/')).ToList();
				listExpected.Add(ReleaseManifest.FILE_NAME);
				CollectionAssert.AreEqual(listExpected,zipArchive.Entries.Select(x => x.FullName).ToList());
				CollectionAssert.AreEqual(new[] { "Big.dll","OpenDental.exe","OpenDentalReplicationService/shader.dat",ReleaseManifest.FILE_NAME },
					zipArchive.Entries.Select(x => x.FullName).ToArray());
				foreach(ReleaseManifestFile releaseManifestFile in releaseManifest.ListFiles) {
					ZipArchiveEntry zipArchiveEntry=zipArchive.GetEntry(releaseManifestFile.RelativePath.Replace('\\','/'));
					using(MemoryStream memoryStream=new MemoryStream()) {
						using(Stream stream=zipArchiveEntry.Open()) {
							stream.CopyTo(memoryStream);
						}
						CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(folder,releaseManifestFile.RelativePath)),memoryStream.ToArray());
					}
				}
				using(StreamReader streamReader=new StreamReader(zipArchive.GetEntry(ReleaseManifest.FILE_NAME).Open(),Encoding.UTF8)) {
					Assert.AreEqual(releaseManifest.Serialize(includeArchiveHash:false),streamReader.ReadToEnd(),"The archive holds the manifest without its hash.");
				}
			}
		}

		[TestMethod]
		public void BuildThenExtract_RoundTripsEveryFile() {
			string folder=MakeInstallFolder();
			ReleaseManifest releaseManifest=Build(folder,out byte[] arrayArchive);
			string folderDest=Path.Combine(TestPaths.NewTempFolder("archive_pull"),"ForkRelease");
			List<string> listProblems=ForkReleases.ExtractArchive(arrayArchive,folderDest,releaseManifest);
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
			Assert.AreEqual(0,releaseManifest.VerifyFolder(folderDest).Count,"Every file has its listed size and SHA-256 and nothing else is there.");
			CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(folder,"Big.dll")),File.ReadAllBytes(Path.Combine(folderDest,"Big.dll")));
			Assert.AreEqual("3d",File.ReadAllText(Path.Combine(folderDest,"OpenDentalReplicationService","shader.dat")));
			Assert.IsFalse(File.Exists(Path.Combine(folderDest,"FreeDentalConfig.xml")));
			Assert.AreEqual(releaseManifest.Serialize(),File.ReadAllText(Path.Combine(folderDest,ReleaseManifest.FILE_NAME)),
				"The pulled folder keeps the published manifest for UpdateFileCopier.");
			Assert.AreEqual(releaseManifest.BuildId,ForkReleases.ComputeBuildId(folderDest));
		}

		[TestMethod]
		public void Extract_RefusesEveryPathOutsideTheFolder() {
			string folderParent=TestPaths.NewTempFolder("archive_slip");
			string folderDest=Path.Combine(folderParent,"ForkRelease");
			KeyValuePair<string,byte[]> entryGood=Entry("OpenDental.exe","good");
			string[] arrayHostile={ "../evil1.dll","..\\evil2.dll","Sparks3D/../../evil3.dll","/evil4.dll","\\evil5.dll","C:/evil6.dll",
				"C:evil7.dll","OpenDental.exe:stream","\\\\server\\share\\evil8.dll" };
			List<KeyValuePair<string,byte[]>> listEntries=new List<KeyValuePair<string,byte[]>> { entryGood };
			listEntries.AddRange(arrayHostile.Select(x => Entry(x,"hostile")));
			byte[] arrayArchive=MakeArchive(ManifestListing(entryGood),listEntries.ToArray());
			List<string> listProblems=ForkReleases.ExtractArchive(arrayArchive,folderDest,ManifestListing(entryGood));
			foreach(string hostile in arrayHostile) {
				Assert.IsTrue(listProblems.Any(x => x.StartsWith("Unsafe path in the release archive") && x.EndsWith(hostile)),
					"Not refused: "+hostile+". Problems: "+string.Join("; ",listProblems));
			}
			Assert.AreEqual(arrayHostile.Length,listProblems.Count,string.Join("; ",listProblems));
			Assert.AreEqual(0,Directory.GetFiles(folderParent,"evil*",SearchOption.AllDirectories).Length,"Nothing was written outside the folder.");
			Assert.IsFalse(File.Exists(@"C:\evil6.dll") || File.Exists(@"C:\evil4.dll") || File.Exists(@"C:\evil5.dll"));
			Assert.AreEqual("good",File.ReadAllText(Path.Combine(folderDest,"OpenDental.exe")),"The listed file is still extracted; DownloadAndVerify "
				+"then fails because problems were found.");
		}

		[TestMethod]
		public void Extract_RefusesUnlistedDuplicateAndOversizedEntries() {
			string folderDest=Path.Combine(TestPaths.NewTempFolder("archive_bad"),"ForkRelease");
			KeyValuePair<string,byte[]> entryA=Entry("A.dll","aaaa");
			KeyValuePair<string,byte[]> entryB=Entry("B.dll","bb");
			ReleaseManifest releaseManifest=ManifestListing(entryA,entryB);
			byte[] arrayArchive=MakeArchive(releaseManifest,entryA,Entry("A.dll","second copy"),Entry("B.dll","much longer than listed"),
				Entry("Extra.dll","not listed"));
			List<string> listProblems=ForkReleases.ExtractArchive(arrayArchive,folderDest,releaseManifest);
			CollectionAssert.AreEquivalent(new[] { "In the release archive twice: A.dll","Larger in the release archive than the manifest says: B.dll",
				"Not in the manifest: Extra.dll" },listProblems);
			Assert.AreEqual("aaaa",File.ReadAllText(Path.Combine(folderDest,"A.dll")),"The second copy of a name is never written.");
			Assert.IsFalse(File.Exists(Path.Combine(folderDest,"B.dll")),"An entry that inflates beyond its listed size is removed.");
			Assert.IsFalse(File.Exists(Path.Combine(folderDest,"Extra.dll")));
		}

		[TestMethod]
		public void Extract_ArchiveManifestMustMatchThePublishedOne() {
			string folderDest=Path.Combine(TestPaths.NewTempFolder("archive_manifest"),"ForkRelease");
			KeyValuePair<string,byte[]> entryA=Entry("A.dll","aaaa");
			ReleaseManifest releaseManifestInside=ManifestListing(entryA);
			ReleaseManifest releaseManifestPublished=ManifestListing(entryA);
			releaseManifestPublished.PublishedBy="SOMEONE ELSE";
			List<string> listProblems=ForkReleases.ExtractArchive(MakeArchive(releaseManifestInside,entryA),folderDest,releaseManifestPublished);
			CollectionAssert.AreEqual(new[] { "The manifest inside the release archive differs from the published manifest." },listProblems);
		}

		[TestMethod]
		public void Extract_NotAZipArchive_IsAProblemNotACrash() {
			string folderDest=Path.Combine(TestPaths.NewTempFolder("archive_notzip"),"ForkRelease");
			List<string> listProblems=ForkReleases.ExtractArchive(Encoding.UTF8.GetBytes("this is not a zip archive"),folderDest,ManifestListing());
			Assert.AreEqual(1,listProblems.Count);
			StringAssert.StartsWith(listProblems[0],"The release archive is not a readable zip archive");
		}
	}
}
