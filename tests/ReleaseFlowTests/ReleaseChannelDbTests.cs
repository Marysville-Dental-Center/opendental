// Publishing a release into a scratch database and pulling it back, with the SHA-256 checks. Synthetic files only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ReleaseFlowTests {
	[TestClass]
	public class ReleaseChannelDbTests {
		private const string DB_NAME="releaseflow_release";

		[ClassCleanup]
		public static void ClassCleanup() {
			ScratchDb.Drop(DB_NAME);
		}

		///<summary>A fake installation folder: program files, a special sub folder, and local files that must not be published.</summary>
		private static string MakeInstallFolder(byte[] arrayBigFile=null) {
			string folder=TestPaths.NewTempFolder("install");
			File.WriteAllText(Path.Combine(folder,"OpenDental.exe"),"synthetic program "+Guid.NewGuid());
			File.WriteAllText(Path.Combine(folder,"OpenDentBusiness.dll"),"synthetic business layer");
			File.WriteAllText(Path.Combine(folder,"UpdateFileCopier.exe"),"synthetic copier");
			File.WriteAllText(Path.Combine(folder,"FreeDentalConfig.xml"),"<local/>");
			File.WriteAllText(Path.Combine(folder,"openlog_1.txt"),"log");
			File.WriteAllText(Path.Combine(folder,"Manifest.txt"),"24.3.63");
			Directory.CreateDirectory(Path.Combine(folder,"Sparks3D"));
			File.WriteAllText(Path.Combine(folder,"Sparks3D","shader.dat"),"3d");
			Directory.CreateDirectory(Path.Combine(folder,"NotShipped"));
			File.WriteAllText(Path.Combine(folder,"NotShipped","x.txt"),"x");
			if(arrayBigFile!=null) {
				File.WriteAllBytes(Path.Combine(folder,"Big.dll"),arrayBigFile);
			}
			return folder;
		}

		private static ReleaseIdentity Identity(string folder,string release="0.0") {
			return new ReleaseIdentity { ReleaseVersion=release,BuildId=ForkReleases.ComputeBuildId(folder) };
		}

		private static void CreateDb() {
			ScratchDb.Create(DB_NAME,is253:false);
			ScratchDb.SetPref(DB_NAME,"DataBaseVersion","24.3.63.0");
			ScratchDb.SetPref(DB_NAME,"ProgramVersion","24.3.63.0");
			//Open Dental update rows that must never be read or used.
			ScratchDb.Exec(DB_NAME,"INSERT INTO documentmisc (DateCreated,FileName,DocMiscType,RawBase64) VALUES "
				+"('2026-01-01','UpdateFiles.zip',0,'AAAA'),('2026-01-01','0001',1,'"+Convert.ToBase64String(Encoding.UTF8.GetBytes("not ours"))+"')");
		}

		[TestMethod]
		public void PublishThenPull_RoundTripsEveryFile() {
			CreateDb();
			byte[] arrayBig=new byte[2*ForkReleases.SEGMENT_BYTES+1000];
			new Random(42).NextBytes(arrayBig);//Incompressible, so the archive needs several segments.
			string folderInstall=MakeInstallFolder(arrayBig);
			ReleaseIdentity releaseIdentity=Identity(folderInstall);
			ReleaseManifest releaseManifest=ForkReleases.Publish(folderInstall,releaseIdentity,"SERVER1",new DateTime(2026,10,19,9,0,0,DateTimeKind.Utc));
			CollectionAssert.AreEquivalent(new[] { "Big.dll","OpenDental.exe","OpenDentBusiness.dll","Sparks3D\\shader.dat","UpdateFileCopier.exe" },
				releaseManifest.ListFiles.Select(x => x.RelativePath).ToArray(),"Local settings, logs, Open Dental's Manifest.txt and other folders stay local.");
			Assert.IsTrue(int.Parse(ScratchDb.Scalar(DB_NAME,"SELECT COUNT(*) FROM documentmisc WHERE DocMiscType=101"))>=3);
			Assert.AreEqual("1",ScratchDb.Scalar(DB_NAME,"SELECT COUNT(*) FROM documentmisc WHERE DocMiscType=100"));
			Assert.AreEqual(ForkReleases.FormatProgramVersion(releaseIdentity),ScratchDb.GetPref(DB_NAME,"ForkProgramVersion"));
			Assert.AreEqual("24.3.63.0",ScratchDb.GetPref(DB_NAME,"ProgramVersion"),"Publishing never touches Open Dental's version preferences.");
			ReleaseManifest releaseManifestPublished=ForkReleases.GetPublishedManifest(out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual(releaseIdentity.BuildId,releaseManifestPublished.BuildId);
			Assert.AreEqual(ReleaseManifest.PRODUCT_NAME,releaseManifestPublished.Product);
			Assert.AreEqual("24.3.63.0",releaseManifestPublished.CompatVersion);
			string folderPull=Path.Combine(TestPaths.NewTempFolder("pull"),"ForkRelease");
			ReleaseDownloadResult releaseDownloadResult=ForkReleases.DownloadAndVerify(folderPull);
			Assert.IsTrue(releaseDownloadResult.IsOk,string.Join("; ",releaseDownloadResult.ListProblems));
			CollectionAssert.AreEqual(arrayBig,File.ReadAllBytes(Path.Combine(folderPull,"Big.dll")));
			Assert.AreEqual("3d",File.ReadAllText(Path.Combine(folderPull,"Sparks3D","shader.dat")));
			Assert.IsTrue(File.Exists(Path.Combine(folderPull,ReleaseManifest.FILE_NAME)),"UpdateFileCopier checks this manifest again.");
			Assert.IsFalse(File.Exists(Path.Combine(folderPull,"FreeDentalConfig.xml")));
			Assert.AreEqual(0,ReleaseManifest.Parse(File.ReadAllText(Path.Combine(folderPull,ReleaseManifest.FILE_NAME))).VerifyFolder(folderPull).Count);
			Assert.AreEqual(releaseIdentity.BuildId,ForkReleases.ComputeBuildId(folderPull),"A pulled installation has the published build id.");
			//Open Dental's rows are untouched.
			Assert.AreEqual("1",ScratchDb.Scalar(DB_NAME,"SELECT COUNT(*) FROM documentmisc WHERE DocMiscType=0"));
			Assert.AreEqual("1",ScratchDb.Scalar(DB_NAME,"SELECT COUNT(*) FROM documentmisc WHERE DocMiscType=1"));
		}

		[TestMethod]
		public void OpenDentalUpdateRows_AreNeverUsed() {
			CreateDb();
			Assert.IsNull(ForkReleases.GetPublishedManifest(out string error),"Only fork rows count as a release.");
			ReleaseDownloadResult releaseDownloadResult=ForkReleases.DownloadAndVerify(Path.Combine(TestPaths.NewTempFolder("none"),"ForkRelease"));
			Assert.IsFalse(releaseDownloadResult.IsOk);
			StringAssert.Contains(releaseDownloadResult.ListProblems.Single(),"No release");
		}

		[TestMethod]
		public void TamperedSegment_FailsTheArchiveHash() {
			CreateDb();
			string folderInstall=MakeInstallFolder();
			ForkReleases.Publish(folderInstall,Identity(folderInstall),"SERVER1");
			string segment=ScratchDb.Scalar(DB_NAME,"SELECT RawBase64 FROM documentmisc WHERE DocMiscType=101 ORDER BY FileName LIMIT 1");
			byte[] arrayBytes=Convert.FromBase64String(segment);
			arrayBytes[arrayBytes.Length/2]^=0xFF;
			ScratchDb.Exec(DB_NAME,"UPDATE documentmisc SET RawBase64='"+Convert.ToBase64String(arrayBytes)+"' WHERE DocMiscType=101 AND FileName='0001'");
			string folderPull=Path.Combine(TestPaths.NewTempFolder("tamper"),"ForkRelease");
			ReleaseDownloadResult releaseDownloadResult=ForkReleases.DownloadAndVerify(folderPull);
			Assert.IsFalse(releaseDownloadResult.IsOk);
			StringAssert.Contains(releaseDownloadResult.ListProblems.Single(),"SHA-256");
			Assert.IsFalse(Directory.Exists(folderPull) && Directory.GetFiles(folderPull).Length>0,"Nothing is extracted from a bad archive.");
		}

		[TestMethod]
		public void ManifestForAnotherProduct_IsNotARelease() {
			CreateDb();
			string folderInstall=MakeInstallFolder();
			ForkReleases.Publish(folderInstall,Identity(folderInstall),"SERVER1");
			string manifest=Encoding.UTF8.GetString(Convert.FromBase64String(
				ScratchDb.Scalar(DB_NAME,"SELECT RawBase64 FROM documentmisc WHERE DocMiscType=100")));
			string manifestOther=manifest.Replace("Product="+ReleaseManifest.PRODUCT_NAME,"Product=Some Other Program");
			ScratchDb.Exec(DB_NAME,"UPDATE documentmisc SET RawBase64='"+Convert.ToBase64String(Encoding.UTF8.GetBytes(manifestOther))+"' WHERE DocMiscType=100");
			Assert.IsNull(ForkReleases.GetPublishedManifest(out string error));
			StringAssert.Contains(error,"another product");
			Assert.IsFalse(ForkReleases.DownloadAndVerify(Path.Combine(TestPaths.NewTempFolder("other"),"ForkRelease")).IsOk);
		}

		[TestMethod]
		public void ManifestFileHash_Tampered_IsCaughtAfterExtraction() {
			CreateDb();
			string folderInstall=MakeInstallFolder();
			ForkReleases.Publish(folderInstall,Identity(folderInstall),"SERVER1");
			//Change one file hash in the published manifest; the archive's own copy then differs, and the file check fails too.
			string manifest=Encoding.UTF8.GetString(Convert.FromBase64String(
				ScratchDb.Scalar(DB_NAME,"SELECT RawBase64 FROM documentmisc WHERE DocMiscType=100")));
			ReleaseManifest releaseManifest=ReleaseManifest.Parse(manifest);
			releaseManifest.ListFiles.First(x => x.RelativePath=="OpenDental.exe").Sha256=new string('0',64);
			ScratchDb.Exec(DB_NAME,"UPDATE documentmisc SET RawBase64='"+Convert.ToBase64String(Encoding.UTF8.GetBytes(releaseManifest.Serialize()))
				+"' WHERE DocMiscType=100");
			ReleaseDownloadResult releaseDownloadResult=ForkReleases.DownloadAndVerify(Path.Combine(TestPaths.NewTempFolder("hash"),"ForkRelease"));
			Assert.IsFalse(releaseDownloadResult.IsOk);
		}

		[TestMethod]
		public void Republish_ReplacesThePreviousRelease() {
			CreateDb();
			string folderInstall=MakeInstallFolder();
			ForkReleases.Publish(folderInstall,Identity(folderInstall),"SERVER1");
			File.WriteAllText(Path.Combine(folderInstall,"OpenDental.exe"),"rebuilt");
			ReleaseIdentity releaseIdentity=Identity(folderInstall);
			ForkReleases.Publish(folderInstall,releaseIdentity,"SERVER1");
			Assert.AreEqual("1",ScratchDb.Scalar(DB_NAME,"SELECT COUNT(*) FROM documentmisc WHERE DocMiscType=100"));
			Assert.AreEqual(releaseIdentity.BuildId,ForkReleases.GetPublishedManifest(out string _).BuildId);
			string folderPull=Path.Combine(TestPaths.NewTempFolder("republish"),"ForkRelease");
			Assert.IsTrue(ForkReleases.DownloadAndVerify(folderPull).IsOk);
			Assert.AreEqual("rebuilt",File.ReadAllText(Path.Combine(folderPull,"OpenDental.exe")));
		}
	}
}
