// The release manifest (also compiled into UpdateFileCopier) and the publish/pull decision.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ReleaseFlowTests {
	[TestClass]
	public class ReleaseManifestTests {
		private static ReleaseManifest Sample() {
			ReleaseManifest releaseManifest=new ReleaseManifest();
			releaseManifest.ReleaseVersion="0.0";
			releaseManifest.BuildId=new string('a',64);
			releaseManifest.CompatVersion="24.3.63.0";
			releaseManifest.PublishedUtc=new DateTime(2026,10,19,12,30,5,DateTimeKind.Utc);
			releaseManifest.PublishedBy="SERVER1";
			releaseManifest.ArchiveSha256=new string('b',64);
			releaseManifest.ListFiles.Add(new ReleaseManifestFile { RelativePath="OpenDental.exe",Size=3,Sha256=ReleaseManifest.ComputeSha256(new byte[] { 1,2,3 }) });
			releaseManifest.ListFiles.Add(new ReleaseManifestFile { RelativePath="Sparks3D\\a b.dll",Size=0,Sha256=ReleaseManifest.ComputeSha256(new byte[0]) });
			return releaseManifest;
		}

		[TestMethod]
		public void Manifest_RoundTrips_AndNamesOurProduct() {
			ReleaseManifest releaseManifest=Sample();
			string text=releaseManifest.Serialize();
			StringAssert.Contains(text,"Product=Open PMS - Dental\n");
			StringAssert.Contains(text,"ReleaseVersion=0.0\n");
			ReleaseManifest releaseManifestParsed=ReleaseManifest.Parse(text);
			Assert.IsTrue(releaseManifestParsed.IsOurProduct());
			Assert.AreEqual(text,releaseManifestParsed.Serialize());
			Assert.AreEqual(releaseManifest.PublishedUtc,releaseManifestParsed.PublishedUtc);
			Assert.AreEqual(2,releaseManifestParsed.ListFiles.Count);
			Assert.IsFalse(releaseManifest.Serialize(includeArchiveHash:false).Contains("ArchiveSha256"),"The copy inside the archive has no archive hash.");
		}

		[TestMethod]
		public void Manifest_ForAnotherProduct_IsNotOurs() {
			string text=Sample().Serialize().Replace("Product=Open PMS - Dental","Product=Open Dental");
			ReleaseManifest releaseManifest=ReleaseManifest.Parse(text);
			Assert.IsFalse(releaseManifest.IsOurProduct());
			string folder=TestPaths.NewTempFolder("product");
			Assert.AreEqual(1,releaseManifest.VerifyFolder(folder).Count);
		}

		[TestMethod]
		public void Manifest_RejectsMalformedAndUnsafeInput() {
			string text=Sample().Serialize();
			AssertFormat(text.Replace("Format=1","Format=2"));
			AssertFormat(text.Replace("Format=1\n",""));
			AssertFormat(text.Replace("BuildId="+new string('a',64),"BuildId="));
			AssertFormat(text+"File=zz|1|x.dll\n");
			AssertFormat(text+"File="+new string('c',64)+"|1|..\\evil.dll\n");
			AssertFormat(text+"File="+new string('c',64)+"|1|C:\\Windows\\evil.dll\n");
			AssertFormat(text+"File="+new string('c',64)+"|1|\\evil.dll\n");
			AssertFormat(text+"File="+new string('c',64)+"|1|OpenDental.exe\n");//Listed twice.
			AssertFormat(text+"Unknown=1\n");
			AssertFormat(null);
		}

		private static void AssertFormat(string text) {
			try {
				ReleaseManifest.Parse(text);
				Assert.Fail("Expected FormatException for: "+text);
			}
			catch(FormatException) {
			}
		}

		[TestMethod]
		public void SafeRelativePaths() {
			Assert.IsTrue(ReleaseManifest.IsSafeRelativePath("OpenDental.exe"));
			Assert.IsTrue(ReleaseManifest.IsSafeRelativePath("Sparks3D\\x.dll"));
			Assert.IsTrue(ReleaseManifest.IsSafeRelativePath("Sparks3D/x.dll"));
			Assert.IsFalse(ReleaseManifest.IsSafeRelativePath("..\\x.dll"));
			Assert.IsFalse(ReleaseManifest.IsSafeRelativePath("a\\..\\..\\x.dll"));
			Assert.IsFalse(ReleaseManifest.IsSafeRelativePath("C:x.dll"));
			Assert.IsFalse(ReleaseManifest.IsSafeRelativePath("/x.dll"));
			Assert.IsFalse(ReleaseManifest.IsSafeRelativePath("a\\\\x.dll"));
			Assert.IsFalse(ReleaseManifest.IsSafeRelativePath(""));
			Assert.IsFalse(ReleaseManifest.IsSafeRelativePath("x?.dll"));
		}

		[TestMethod]
		public void VerifyFolder_FindsMissingTamperedAndExtraFiles() {
			string folder=TestPaths.NewTempFolder("verify");
			File.WriteAllBytes(Path.Combine(folder,"OpenDental.exe"),new byte[] { 1,2,3 });
			Directory.CreateDirectory(Path.Combine(folder,"Sparks3D"));
			File.WriteAllBytes(Path.Combine(folder,"Sparks3D","a b.dll"),new byte[0]);
			File.WriteAllText(Path.Combine(folder,ReleaseManifest.FILE_NAME),"ignored");
			ReleaseManifest releaseManifest=Sample();
			Assert.AreEqual(0,releaseManifest.VerifyFolder(folder).Count,string.Join("; ",releaseManifest.VerifyFolder(folder)));
			File.WriteAllBytes(Path.Combine(folder,"OpenDental.exe"),new byte[] { 1,2,4 });
			StringAssert.Contains(releaseManifest.VerifyFolder(folder).Single(),"Wrong SHA-256");
			File.WriteAllBytes(Path.Combine(folder,"OpenDental.exe"),new byte[] { 1,2,3,4 });
			StringAssert.Contains(releaseManifest.VerifyFolder(folder).Single(),"Wrong size");
			File.Delete(Path.Combine(folder,"OpenDental.exe"));
			StringAssert.Contains(releaseManifest.VerifyFolder(folder).Single(),"Missing");
			File.WriteAllBytes(Path.Combine(folder,"OpenDental.exe"),new byte[] { 1,2,3 });
			File.WriteAllText(Path.Combine(folder,"extra.dll"),"x");
			StringAssert.Contains(releaseManifest.VerifyFolder(folder).Single(),"Not in the manifest");
		}

		[TestMethod]
		public void Sha256_KnownValue() {
			Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",ReleaseManifest.ComputeSha256(Encoding.ASCII.GetBytes("abc")));
		}
	}

	[TestClass]
	public class ReleaseDecisionTests {
		private static ReleaseManifest Published(string release,string buildId) {
			ReleaseManifest releaseManifest=new ReleaseManifest();
			releaseManifest.ReleaseVersion=release;
			releaseManifest.BuildId=buildId;
			return releaseManifest;
		}

		private static ReleaseIdentity Running(string release,string buildId) {
			return new ReleaseIdentity { ReleaseVersion=release,BuildId=buildId };
		}

		[TestMethod]
		public void UpdateServer_PublishesItsBuild_UnlessANewerReleaseIsPublished() {
			Assert.AreEqual(ReleaseAction.Publish,ForkReleases.Decide(null,Running("0.0","x"),true,false).Action,"Nothing published yet.");
			Assert.AreEqual(ReleaseAction.Publish,ForkReleases.Decide(Published("0.0","old"),Running("0.0","new"),true,false).Action,"A rebuild of 0.0.");
			Assert.AreEqual(ReleaseAction.Publish,ForkReleases.Decide(Published("0.0","old"),Running("0.1","new"),true,false).Action);
			Assert.AreEqual(ReleaseAction.Pull,ForkReleases.Decide(Published("0.1","new"),Running("0.0","old"),true,false).Action);
			Assert.AreEqual(ReleaseAction.None,ForkReleases.Decide(Published("0.0","same"),Running("0.0","same"),true,false).Action);
		}

		[TestMethod]
		public void Workstation_PullsThePublishedRelease_AndIsNeverOfferedADowngrade() {
			Assert.AreEqual(ReleaseAction.None,ForkReleases.Decide(null,Running("0.0","x"),false,false).Action);
			Assert.AreEqual(ReleaseAction.None,ForkReleases.Decide(Published("0.0","same"),Running("0.0","same"),false,false).Action);
			Assert.AreEqual(ReleaseAction.Pull,ForkReleases.Decide(Published("0.0","new"),Running("0.0","old"),false,false).Action);
			Assert.AreEqual(ReleaseAction.Pull,ForkReleases.Decide(Published("0.2","new"),Running("0.1","old"),false,false).Action);
			ReleaseDecision releaseDecision=ForkReleases.Decide(Published("0.1","old"),Running("0.2","new"),false,false,"SERVER1");
			Assert.AreEqual(ReleaseAction.RefuseNewerThanPublished,releaseDecision.Action);
			StringAssert.Contains(releaseDecision.Message,"SERVER1");
			Assert.IsFalse(releaseDecision.Message.ToLowerInvariant().Contains("downgrade"));
		}

		[TestMethod]
		public void DebugBuilds_NeitherPublishNorPull() {
			Assert.AreEqual(ReleaseAction.None,ForkReleases.Decide(null,Running("0.0","x"),true,true).Action);
			Assert.AreEqual(ReleaseAction.None,ForkReleases.Decide(Published("0.1","new"),Running("0.0","old"),false,true).Action);
		}

		[TestMethod]
		public void ProgramVersionMarker_RoundTrips() {
			ReleaseIdentity releaseIdentity=Running("0.0","abc123");
			Assert.AreEqual("0.0+abc123",ForkReleases.FormatProgramVersion(releaseIdentity));
			ReleaseIdentity releaseIdentityParsed=ForkReleases.ParseProgramVersion("0.0+abc123");
			Assert.AreEqual("0.0",releaseIdentityParsed.ReleaseVersion);
			Assert.AreEqual("abc123",releaseIdentityParsed.BuildId);
			Assert.IsNull(ForkReleases.ParseProgramVersion(""));
			Assert.IsNull(ForkReleases.ParseProgramVersion("24.3.63.0"));
		}

		[TestMethod]
		public void PublishFilter_KeepsLocalSettingsLocal() {
			Assert.IsTrue(ForkReleases.ShouldPublishFile("OpenDental.exe"));
			Assert.IsTrue(ForkReleases.ShouldPublishFile("UpdateFileCopier.exe"));
			Assert.IsFalse(ForkReleases.ShouldPublishFile("FreeDentalConfig.xml"));
			Assert.IsFalse(ForkReleases.ShouldPublishFile("openlog_2026.txt"));
			Assert.IsFalse(ForkReleases.ShouldPublishFile("Plugin__local.dll"));
			Assert.IsFalse(ForkReleases.ShouldPublishFile(ReleaseManifest.FILE_NAME));
			Assert.IsFalse(ForkReleases.ShouldPublishFile("Manifest.txt"),"Open Dental's manifest is never shipped.");
		}

		[TestMethod]
		public void BuildId_ChangesWithAnyOwnAssembly() {
			string folder=TestPaths.NewTempFolder("buildid");
			File.WriteAllText(Path.Combine(folder,"OpenDental.exe"),"one");
			File.WriteAllText(Path.Combine(folder,"OpenDentBusiness.dll"),"two");
			File.WriteAllText(Path.Combine(folder,"ThirdParty.dll"),"three");
			string buildId=ForkReleases.ComputeBuildId(folder);
			Assert.IsTrue(ReleaseManifest.IsSha256Hex(buildId));
			File.WriteAllText(Path.Combine(folder,"ThirdParty.dll"),"changed");
			Assert.AreEqual(buildId,ForkReleases.ComputeBuildId(folder),"Third-party files are not part of the build id.");
			File.WriteAllText(Path.Combine(folder,"OpenDentBusiness.dll"),"changed");
			Assert.AreNotEqual(buildId,ForkReleases.ComputeBuildId(folder));
		}

		[TestMethod]
		public void BuildIdFiles_AreBuiltNextToOpenDentalExe() {
			string folder=Path.Combine(TestPaths.RepoRoot,"OpenDental","bin","Debug");
			if(!File.Exists(Path.Combine(folder,"OpenDental.exe"))) {
				Assert.Inconclusive("OpenDental is not built.");
			}
			List<string> listMissing=new List<string>();
			foreach(string fileName in new[] { "OpenDental.exe","OpenDentBusiness.dll","CodeBase.dll","DataConnectionBase.dll","WpfControlsOD.dll",
				"UpdateFileCopier.exe" })
			{
				Assert.IsTrue(ForkReleases.ListBuildIdFiles.Contains(fileName),fileName);
				if(!File.Exists(Path.Combine(folder,fileName))) {
					listMissing.Add(fileName);
				}
			}
			Assert.AreEqual(0,listMissing.Count,"Missing from the build output: "+string.Join(", ",listMissing));
		}
	}
}
