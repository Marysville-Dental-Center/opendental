// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Design 13 (guard tests), in the manner of NoHqTests.RemovedAssembliesTests: the proprietary imaging binaries the capture track
	///removed stay out. The commercial TWAIN library and its codecs, the old Video window's native camera DLL and its Media Foundation wrappers,
	///and the prebuilt ImagingDeviceManager.dll that stood in for the in-repo project are neither in "Required dlls" nor named by a project
	///item, and every project reference to ImagingDeviceManager is the in-repo project. No build, database or device is needed.</summary>
	[TestClass]
	public class RemovedImagingBinariesTests {
		///<summary>The files the capture track deleted from "Required dlls".</summary>
		private static readonly string[] _arrayRemovedFiles={ "Eztwain4.dll","EZT4Jpeg.dll","EZT4Pdf.dll","VideoCamLib.dll","MediaFoundation.dll",
			"MediaFoundation.Extension.dll","ImagingDeviceManager.dll" };
		///<summary>The items that name a file to reference, copy or ship. A comment is not an item: the files are parsed as XML.</summary>
		private static readonly string[] _arrayItemNames={ "Reference","HintPath","Content","None" };
		///<summary>Folders never searched for project files: build output, NuGet packages, test results, and every folder whose name starts with
		///'.' (.git, .vs, and the worktrees under .claude, which are other checkouts).</summary>
		private static readonly string[] _arrayFoldersSkipped={ "bin","obj","packages","TestResults","node_modules" };
		private const string ProjectFileName="xImagingDeviceManager.csproj";

		[TestMethod]
		public void RemovedBinaries_AreNotInRequiredDlls() {
			string pathRequiredDlls=Path.Combine(GetRepositoryRoot(),"Required dlls");
			Assert.IsTrue(Directory.Exists(pathRequiredDlls),"Missing "+pathRequiredDlls);
			List<string> listFound=_arrayRemovedFiles.Where(x => File.Exists(Path.Combine(pathRequiredDlls,x))).ToList();
			Assert.AreEqual(0,listFound.Count,"Back in \"Required dlls\": "+string.Join(", ",listFound));
		}

		[TestMethod]
		public void RemovedBinaries_AreNotNamedByAProjectItem() {
			string root=GetRepositoryRoot();
			List<string> listViolations=new List<string>();
			List<string> listPaths=GetProjectFiles(root).ToList();
			Assert.IsTrue(listPaths.Any(x => x.EndsWith(ProjectFileName,StringComparison.OrdinalIgnoreCase)),"The project files were not found under "+root);
			foreach(string path in listPaths) {
				XDocument xDocument=LoadProject(path);
				foreach(XElement xElement in xDocument.Descendants().Where(x => _arrayItemNames.Contains(x.Name.LocalName))) {
					//An item's file is in its Include (or Update) attribute; a HintPath's in its text. A Reference's Include is an assembly name.
					List<string> listValues=xElement.Attributes().Where(x => x.Name.LocalName=="Include" || x.Name.LocalName=="Update")
						.Select(x => x.Value).ToList();
					if(xElement.Name.LocalName=="HintPath") {
						listValues.Add(xElement.Value);
					}
					foreach(string value in listValues) {
						string fileRemoved=GetRemovedFileNamed(value,xElement.Name.LocalName=="Reference");
						if(fileRemoved!=null) {
							listViolations.Add(path.Substring(root.Length).TrimStart('\\')+": <"+xElement.Name.LocalName+"> names "+fileRemoved+" ("+value.Trim()+")");
						}
					}
				}
			}
			Assert.AreEqual(0,listViolations.Count,"Removed imaging binaries are named again: "+string.Join("; ",listViolations));
		}

		[TestMethod]
		public void ImagingDeviceManagerReferences_AreTheInRepoProject() {
			string root=GetRepositoryRoot();
			string pathExpected=Path.GetFullPath(Path.Combine(root,"ImagingDeviceManager",ProjectFileName));
			Assert.IsTrue(File.Exists(pathExpected),"Missing "+pathExpected);
			List<string> listViolations=new List<string>();
			int countReferences=0;
			foreach(string path in GetProjectFiles(root)) {
				XDocument xDocument=LoadProject(path);
				foreach(XElement xElement in xDocument.Descendants().Where(x => x.Name.LocalName=="ProjectReference")) {
					string include=((string)xElement.Attribute("Include")??"").Trim();
					if(!string.Equals(Path.GetFileName(include.Replace('/','\\')),ProjectFileName,StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					countReferences++;
					string pathResolved=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),include.Replace('/','\\')));
					if(!string.Equals(pathResolved,pathExpected,StringComparison.OrdinalIgnoreCase)) {
						listViolations.Add(path.Substring(root.Length).TrimStart('\\')+": "+include+" resolves to "+pathResolved);
					}
				}
			}
			//OpenPmsDental, ImagingCapture and the two test projects reference it today; a project dropping its reference is not this test's concern.
			Assert.IsTrue(countReferences>=1,"No project references "+ProjectFileName+".");
			Assert.AreEqual(0,listViolations.Count,"A reference to ImagingDeviceManager is not the in-repo project (a missing project would be swapped "
				+"for a prebuilt DLL by Directory.Build.targets): "+string.Join("; ",listViolations));
		}

		[TestMethod]
		public void GetRemovedFileNamed_MatchesFileNamesAndAssemblyNamesOnly() {
			Assert.AreEqual("VideoCamLib.dll",GetRemovedFileNamed(@"..\Required dlls\videocamlib.DLL",false));
			Assert.AreEqual("Eztwain4.dll",GetRemovedFileNamed("$(ODRequiredDllsDir)Eztwain4.dll",false));
			Assert.AreEqual("ImagingDeviceManager.dll",GetRemovedFileNamed("ImagingDeviceManager, Version=1.0.0.0, Culture=neutral",true));
			Assert.AreEqual("MediaFoundation.Extension.dll",GetRemovedFileNamed("MediaFoundation.Extension",true));
			Assert.IsNull(GetRemovedFileNamed("ImagingDeviceManager",false),"Without .dll an item is not the file (a folder, a Content item's name).");
			Assert.IsNull(GetRemovedFileNamed(@"..\ImagingDeviceManager\xImagingDeviceManager.csproj",false));
			Assert.IsNull(GetRemovedFileNamed(@"..\ImagingDeviceManager\EZTwain.cs",false));
			Assert.IsNull(GetRemovedFileNamed("ImagingDeviceManagerTests",true));
			Assert.IsNull(GetRemovedFileNamed("",true));
		}

		///<summary>The removed file value names, or null. value is an item's Include or a HintPath: its last path part is compared with each file
		///name. For a Reference (isAssemblyName), the assembly name before the first comma is compared with each file name without .dll too.</summary>
		private static string GetRemovedFileNamed(string value,bool isAssemblyName) {
			string trimmed=(value??"").Trim();
			if(trimmed=="") {
				return null;
			}
			string fileName=trimmed.Substring(trimmed.LastIndexOfAny(new[] { '\\','/',')' })+1);//after the folder or an MSBuild property
			string assemblyName=trimmed.Split(',')[0].Trim();
			foreach(string fileRemoved in _arrayRemovedFiles) {
				if(string.Equals(fileName,fileRemoved,StringComparison.OrdinalIgnoreCase)) {
					return fileRemoved;
				}
				if(isAssemblyName && string.Equals(assemblyName,Path.GetFileNameWithoutExtension(fileRemoved),StringComparison.OrdinalIgnoreCase)) {
					return fileRemoved;
				}
			}
			return null;
		}

		private static XDocument LoadProject(string path) {
			try {
				return XDocument.Load(path);
			}
			catch(Exception ex) {
				Assert.Fail("The project file could not be read as XML: "+path+": "+ex.Message);
				return null;
			}
		}

		///<summary>Every *.csproj, *.targets and *.props file under root, outside the skipped folders.</summary>
		private static IEnumerable<string> GetProjectFiles(string folder) {
			foreach(string path in Directory.GetFiles(folder)) {
				string extension=Path.GetExtension(path).ToLowerInvariant();
				if(extension==".csproj" || extension==".targets" || extension==".props") {
					yield return path;
				}
			}
			foreach(string pathSub in Directory.GetDirectories(folder)) {
				string name=Path.GetFileName(pathSub);
				if(name.StartsWith(".") || _arrayFoldersSkipped.Contains(name,StringComparer.OrdinalIgnoreCase)) {
					continue;
				}
				foreach(string path in GetProjectFiles(pathSub)) {
					yield return path;
				}
			}
		}

		///<summary>The repository root (the folder that holds LICENSE and NOTICE), found by walking up from the test assembly.</summary>
		private static string GetRepositoryRoot() {
			foreach(string start in new[] { Path.GetDirectoryName(typeof(RemovedImagingBinariesTests).Assembly.Location),Environment.CurrentDirectory }) {
				DirectoryInfo directoryInfo=new DirectoryInfo(start);
				while(directoryInfo!=null) {
					if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))
						&& Directory.Exists(Path.Combine(directoryInfo.FullName,"ImagingDeviceManager")))
					{
						return directoryInfo.FullName;
					}
					directoryInfo=directoryInfo.Parent;
				}
			}
			Assert.Inconclusive("The repository root (the folder with LICENSE and NOTICE) was not found.");
			return null;
		}
	}
}
