// The Corresponding Source of a release: tools\Build-Release.ps1 writes OpenPmsDental-<version>-source.zip, the repository at the release
// commit with every submodule at its pinned commit, so the source of PDFsharp, MigraDoc and WeCantSpell.Hunspell as built is published with each
// release; the setup's pages, NOTICE, the notices and the program's licence text say where it is. -WriteSourceArchive writes only the archive
// (git archive, offline); nothing is built.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PdfLibraryTests {
	[TestClass]
	public class SourceArchiveTests {
		private static string ReleaseVersion() {
			Match match=Regex.Match(File.ReadAllText(SourceBuild.Combine("SharedAssemblyInfo.cs")),@"AssemblyInformationalVersion\(""([^""]+)""\)");
			Assert.IsTrue(match.Success);
			return match.Groups[1].Value;
		}

		///<summary>The files (blobs) of the tree of the commit, as git ls-tree names them, relative to the repository of the folder.</summary>
		private static List<string> ListCommitFiles(string folder,string commit) {
			SourceBuild.ProcessResult processResult=SourceBuild.Git("ls-tree -r "+commit,folder);
			Assert.AreEqual(0,processResult.ExitCode,processResult.Output);
			return processResult.Output.Split(new[] { "\n" },StringSplitOptions.RemoveEmptyEntries).Select(x => x.TrimEnd('\r'))
				.Where(x => Regex.IsMatch(x,@"^\d+ blob [0-9a-f]{40}\t")).Select(x => x.Substring(x.IndexOf('\t')+1)).ToList();
		}

		private static string ReadEntry(ZipArchive zipArchive,string name) {
			ZipArchiveEntry zipArchiveEntry=zipArchive.GetEntry(name);
			Assert.IsNotNull(zipArchiveEntry,"Not in the source archive: "+name);
			using(StreamReader streamReader=new StreamReader(zipArchiveEntry.Open(),Encoding.UTF8)) {
				return streamReader.ReadToEnd();
			}
		}

		[TestMethod]
		public void SourceArchive_HoldsTheRepository_AndEverySubmoduleAtItsPin() {
			string version=ReleaseVersion();
			string folder=Path.Combine(Path.GetTempPath(),"PdfLibraryTests_source_"+Guid.NewGuid().ToString("N"));
			try {
				SourceBuild.ProcessResult processResult=SourceBuild.BuildRelease("-WriteSourceArchive","\""+folder+"\"");
				Assert.AreEqual(0,processResult.ExitCode,processResult.Output);
				string top="OpenPmsDental-"+version+"-source";
				string pathArchive=Path.Combine(folder,top+".zip");
				CollectionAssert.Contains(processResult.Lines,"Wrote "+pathArchive,processResult.Output);
				string commitHead=SourceBuild.Git("rev-parse HEAD").Output.Trim();
				using(ZipArchive zipArchive=ZipFile.OpenRead(pathArchive)) {
					List<string> listEntries=zipArchive.Entries.Select(x => x.FullName).ToList();
					Assert.IsTrue(listEntries.All(x => x.StartsWith(top+"/")),"One top folder, "+top);
					HashSet<string> hashSetFiles=new HashSet<string>(listEntries.Where(x => !x.EndsWith("/")).Select(x => x.Substring(top.Length+1)));
					//The repository at the release commit: every file of HEAD, the submodules' own files besides.
					List<string> listRepository=ListCommitFiles(SourceBuild.RepoRoot,"HEAD");
					List<string> listMissing=listRepository.Where(x => !hashSetFiles.Contains(x)).ToList();
					Assert.AreEqual(0,listMissing.Count,"Not in the archive: "+string.Join(", ",listMissing.Take(20)));
					foreach(string name in new[] { ".gitmodules","LICENSE","NOTICE","THIRD-PARTY-NOTICES.txt","LICENSE-AGPL-3.0.txt","LICENSE-LGPL-2.1.txt",
						"tools/Build-Release.ps1","third-party/build/PDFsharp.props","third-party/build/SourceBuild.targets",SourceBuild.REPLACEMENT_FILE,
						"third-party/build/WeCantSpell.Hunspell/WeCantSpell.Hunspell.csproj","docs/third-party-source-builds.md" })
					{
						Assert.IsTrue(hashSetFiles.Contains(name),"Not in the archive: "+name);
					}
					//Every submodule at the commit the release commit pins, whole.
					foreach((string path,string commit) in new[] { (SourceBuild.PDFSHARP_PATH,SourceBuild.PDFSHARP_COMMIT),
						(SourceBuild.WECANTSPELL_PATH,SourceBuild.WECANTSPELL_COMMIT) })
					{
						List<string> listSubmodule=ListCommitFiles(SourceBuild.Combine(path),commit);
						Assert.IsTrue(listSubmodule.Count>100,path+": "+listSubmodule.Count+" files.");
						List<string> listSubMissing=listSubmodule.Where(x => !hashSetFiles.Contains(path+"/"+x)).ToList();
						Assert.AreEqual(0,listSubMissing.Count,path+", not in the archive: "+string.Join(", ",listSubMissing.Take(20)));
						Assert.AreEqual(listSubmodule.Count,hashSetFiles.Count(x => x.StartsWith(path+"/")),path+": nothing else under it.");
					}
					foreach(string name in new[] { "third-party/PDFsharp/LICENSE","third-party/PDFsharp/src/StrongnameKey.snk",
						"third-party/PDFsharp/"+SourceBuild.REPLACED_FILE,"third-party/PDFsharp/src/foundation/src/PDFsharp/src/PdfSharp-gdi/PdfSharp-gdi.csproj",
						"third-party/WeCantSpell.Hunspell/license.txt","third-party/WeCantSpell.Hunspell/WeCantSpell.Hunspell/WordList.cs" })
					{
						Assert.IsTrue(hashSetFiles.Contains(name),"Not in the archive: "+name);
					}
					//A file of the archive is the committed file (line ends aside).
					string replaced=ReadEntry(zipArchive,top+"/third-party/PDFsharp/"+SourceBuild.REPLACED_FILE);
					SourceBuild.ProcessResult processResultBlob=SourceBuild.Git("show "+SourceBuild.PDFSHARP_COMMIT+":"+SourceBuild.REPLACED_FILE,
						SourceBuild.Combine(SourceBuild.PDFSHARP_PATH));
					Assert.AreEqual(processResultBlob.Output.Replace("\r\n","\n").Trim('﻿','\n'),replaced.Replace("\r\n","\n").Trim('﻿','\n'));
					//What the archive holds, in its own words.
					string commits=ReadEntry(zipArchive,top+"/SOURCE-COMMITS.txt");
					StringAssert.Contains(commits,"Repository: commit "+commitHead);
					StringAssert.Contains(commits,SourceBuild.PDFSHARP_PATH+": commit "+SourceBuild.PDFSHARP_COMMIT+", "+SourceBuild.PDFSHARP_URL);
					StringAssert.Contains(commits,SourceBuild.WECANTSPELL_PATH+": commit "+SourceBuild.WECANTSPELL_COMMIT+", "+SourceBuild.WECANTSPELL_URL);
				}
			}
			finally {
				if(Directory.Exists(folder)) {
					Directory.Delete(folder,true);
				}
			}
		}

		[TestMethod]
		public void BuildRelease_ChecksTheSubmodulesFirst_AndPublishesTheArchiveWithTheSetup() {
			string script=File.ReadAllText(SourceBuild.Combine("tools/Build-Release.ps1"));
			int indexSubmodules=script.IndexOf("#region Submodules");
			int indexBuild=script.IndexOf("#region Build");
			int indexInstaller=script.IndexOf("#region Installer");
			int indexArchive=script.IndexOf("#region Source archive");
			int indexCopy=script.IndexOf("#region Copy");
			Assert.IsTrue(indexSubmodules>0 && indexSubmodules<indexBuild && indexBuild<indexInstaller && indexInstaller<indexArchive && indexArchive<indexCopy,
				"The submodules are checked before the build; the archive is written after the setup and copied with it.");
			StringAssert.Contains(script.Substring(indexSubmodules,indexBuild-indexSubmodules),"Assert-SourceArchivePossible");
			string regionArchive=script.Substring(indexArchive,indexCopy-indexArchive);
			StringAssert.Contains(regionArchive,"$sourceArchivePath=Write-SourceArchive $OutDir $releaseVersion");
			StringAssert.Contains(regionArchive,"$sumLines.Insert(");
			StringAssert.Contains(script.Substring(indexCopy),"Copy-Item -LiteralPath $sourceArchivePath");
			StringAssert.Contains(script,"$SourceArchiveName='OpenPmsDental-{0}-source'");
			//No source archive from a checkout whose submodules are not at their pins.
			StringAssert.Contains(script,"but the release commit pins");
		}

		[TestMethod]
		public void LicenceInformation_SaysWhereTheSourceIs() {
			string version=ReleaseVersion();
			StringAssert.Contains(File.ReadAllText(SourceBuild.Combine("installer/setup-info.txt")),"OpenPmsDental-@VERSION@-source.zip");
			StringAssert.Contains(File.ReadAllText(SourceBuild.Combine("installer/setup-license.txt")),
				"The Corresponding Source of this program as installed, including PDFsharp, MigraDoc and WeCantSpell.Hunspell as built for this setup, is the "
				+"source archive OpenPmsDental-@VERSION@-source.zip published with this release");
			string notice=Regex.Replace(File.ReadAllText(SourceBuild.Combine("NOTICE")),@"\s+"," ");
			StringAssert.Contains(notice,"Where the source is: the complete source of each release, the Corresponding Source of everything its setup installs, is "
				+"the source archive OpenPmsDental-<version>-source.zip published with the release");
			StringAssert.Contains(notice,"The PDFsharp built here is a modified version (GNU AGPL version 3, section 5a)");
			string notices=Regex.Replace(File.ReadAllText(SourceBuild.Combine("THIRD-PARTY-NOTICES.txt")),@"\s+"," ");
			StringAssert.Contains(notices,"The source archive of each release, OpenPmsDental-<version>-source.zip next to the setup");
			StringAssert.Contains(notices,"Modified version (GNU AGPL version 3, section 5a). One file of empira's source, "+SourceBuild.REPLACED_FILE+", is replaced by "
				+SourceBuild.REPLACEMENT_FILE+", changed by the Open PMS - Dental contributors on 2026-11-02.");
			string program=OpenPmsDental.LicenseList.GetProgramLicenseText();
			StringAssert.Contains(program,"is the source archive OpenPmsDental-"+version+"-source.zip published with the release");
			StringAssert.Contains(program,"source of PDFsharp, MigraDoc and WeCantSpell.Hunspell as built");
			Dictionary<string,string> dictTexts=OpenPmsDental.LicenseList.GetEntries().ToDictionary(x => x.Name,x => x.Text);
			StringAssert.Contains(dictTexts["PDFsharp"],SourceBuild.PDFSHARP_COMMIT,"Help > About > View Licenses: PDFsharp built from the pinned source.");
			StringAssert.Contains(dictTexts["PDFsharp"],SourceBuild.REPLACED_FILE);
			StringAssert.Contains(dictTexts["MigraDoc"],SourceBuild.PDFSHARP_COMMIT);
			StringAssert.Contains(dictTexts["WeCantSpell.Hunspell"],SourceBuild.WECANTSPELL_COMMIT);
		}
	}
}
