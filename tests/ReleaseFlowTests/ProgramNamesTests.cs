// The program's run-time names in CodeBase.ProgramNames: exact process-name matches, the folders scanned for work saved before a rename, and
// that the sites which used to spell the names out now read them from there.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ReleaseFlowTests {
	[TestClass]
	public class ProgramNamesTests {
		[TestMethod]
		public void ProcessName_MatchesTheWholeNameOnly() {
			Assert.IsTrue(ProgramNames.IsProgramProcessName(ProgramNames.ProcessName));
			Assert.IsTrue(ProgramNames.IsProgramProcessName(ProgramNames.ProcessName.ToUpperInvariant()),"Process names ignore case.");
			Assert.AreEqual(ProgramNames.ProcessName+".exe",ProgramNames.ExeFileName);
			foreach(string name in new[] { ProgramNames.ProcessName+"HL7",ProgramNames.ProcessName+".vshost",ProgramNames.ProcessName+"Service",
				ProgramNames.ProcessName.Substring(0,ProgramNames.ProcessName.Length-1),"",null })
			{
				Assert.IsFalse(ProgramNames.IsProgramProcessName(name),"Matched "+(name??"null"));
			}
		}

		[TestMethod]
		public void LocalAppDataFolders_ListTodaysFolderFirstAndThePreRenameOneOnce() {
			string folderAppData=Path.Combine(Path.GetTempPath(),"LocalAppData");
			List<string> listFolders=ProgramNames.GetLocalAppDataFolders(folderAppData);
			Assert.AreEqual(Path.Combine(folderAppData,ProgramNames.LocalAppDataFolderName),listFolders[0]);
			CollectionAssert.Contains(listFolders,Path.Combine(folderAppData,ProgramNames.LegacyLocalAppDataFolderName));
			Assert.AreEqual(listFolders.Count,listFolders.Distinct(StringComparer.OrdinalIgnoreCase).Count());
			//Before the rename both names are the same folder.
			Assert.AreEqual(ProgramNames.LocalAppDataFolderName==ProgramNames.LegacyLocalAppDataFolderName ? 1 : 2,listFolders.Count);
			Assert.AreEqual("OpenDental",ProgramNames.LegacyLocalAppDataFolderName,"The pre-rename folder never changes.");
			Assert.AreEqual(0,ProgramNames.GetLocalAppDataFolders("").Count);
			Assert.AreEqual(0,ProgramNames.GetLocalAppDataFolders(null).Count);
		}

		[TestMethod]
		public void Values_AreTodaysNames() {
			//The rename changes these values, and only here. Until then they are the names the program has always used.
			Assert.AreEqual("OpenDental",ProgramNames.ProcessName);
			Assert.AreEqual("OpenDentBusiness.dll",ProgramNames.BusinessDllFileName);
			Assert.AreEqual("opendental",ProgramNames.TempFolderName);
			Assert.AreEqual("updatefilecopier",ProgramNames.UpdateFileCopierTempFolderName);
			Assert.AreEqual("OpenDental",ProgramNames.EventLogSource);
			Assert.AreEqual("OpenDentHL7",ProgramNames.EventLogSourceHL7);
			Assert.AreEqual("OpenDentHL7.Main",ProgramNames.EventLogSourceHL7Main);
			Assert.AreEqual("OpenDent",ProgramNames.ServicePrefix);
			Assert.IsTrue(ProgramNames.HL7ServiceName.StartsWith(ProgramNames.ServicePrefix),"The HL7 service must carry the service prefix.");
		}

		///<summary>The HL7 service's fallback name is Open Dental's run-time default, not the installer's name. Existing HL7 setups saved it into
		///HL7Def.HL7ServiceName or the eCW HL7ServiceName property, and the service refuses to start when its name differs from the saved one, so
		///a different value would stop their HL7 feed.</summary>
		[TestMethod]
		public void HL7Service_FallbackNameIsOpenDentalsRunTimeDefault() {
			Assert.AreEqual("OpenDentalHL7",ProgramNames.HL7ServiceFallbackName,"Saved HL7 setups hold this name; it never changes.");
			string code=Regex.Replace(File.ReadAllText(Path.Combine(TestPaths.RepoRoot,"OpenDentHL7","Program.cs")),@"//.*$","",RegexOptions.Multiline);
			StringAssert.Contains(code,"serviceHL7.ServiceName=ProgramNames.HL7ServiceFallbackName;","The service's default name must be the fallback name.");
			Assert.IsFalse(code.Contains("serviceHL7.ServiceName=ProgramNames.HL7ServiceName"),"The installer's name is not the run-time fallback.");
			//The setup windows tell users to type the fallback name.
			foreach(string designer in new[] { "FormHL7DefEdit.designer.cs","FormEClinicalWorks.designer.cs" }) {
				StringAssert.Contains(File.ReadAllText(Path.Combine(TestPaths.RepoRoot,"OpenDental","Forms",designer)),
					"Typically "+ProgramNames.HL7ServiceFallbackName+".",designer);
			}
		}

		///<summary>The sites that spelled the program's names out now read them from ProgramNames. A literal left behind would keep the old name
		///after the rename without a compiler error.</summary>
		[TestMethod]
		public void RuntimeNameSites_UseProgramNames() {
			Dictionary<string,string[]> dictForbidden=new Dictionary<string,string[]> {
				[@"UpdateFileCopier\FormMain.cs"]=new[] { "KillProcess(\"OpenDental\")","\"OpenDental.exe\"","\"opendental\"","\"updatefilecopier\"" },
				[@"UpdateFileCopier\Program.cs"]=new[] { @"@""C:\Program Files\Open Dental""" },
				[@"OpenDental\Main Modules\ProgramEntry.cs"]=new[] { "ProcessName.StartsWith(" },
				[@"OpenDental\Main Modules\FormOpenDental.cs"]=new[] { "ProcessName==\"OpenDental\"","Contains(\"updatefilecopier\")" },
				[@"OpenDentalCloud\Storage\Sftp.cs"]=new[] { "ProcessName.StartsWith(","\"OpenDental\",\"Stash\"" },
				[@"OpenDentBusiness\Cache\PrefC.cs"]=new[] { "\"opendental\"","Directory.GetFiles(Path.GetTempPath())" },
				[@"OpenDentBusiness\Misc\NTPv4.cs"]=new[] { "WriteEntry(\"OpenDental\"" },
				[@"OpenDentBusiness\Misc\WindowsTime.cs"]=new[] { "WriteEntry(\"OpenDental\"" },
				[@"OpenDentHL7\Program.cs"]=new[] { "\"OpenDentHL7","StartsWith(\"OpenDent\")" },
				[@"OpenDentHL7\ProjectInstaller.cs"]=new[] { "\"OpenDentHL7\"" },
				[@"CodeBase\Utilities\ServicesHelper.cs"]=new[] { "StartsWith(\"OpenDent\")" },
				[@"ServiceManager\FormServiceManage.cs"]=new[] { "!=\"OpenDent\"" },
				[@"ServiceManager\FormMain.cs"]=new[] { "FormServiceManage(\"OpenDent\"" },
				[@"CentralManager\Db\CentralConnectionHelper.cs"]=new[] { "Process.Start(\"OpenDental.exe\"" },
				[@"OpenDental\Logic\ChartLetterL.cs"]=new[] { "\"OpenDental\",\"ChartLetters\"" },
				[@"OpenDentBusiness\Remoting\DtoProcessor.cs"]=new[] { "\"OpenDentBusiness.\"" },
				[@"OpenDentBusiness\Remoting\DataTransferObject.cs"]=new[] { "\"OpenDentBusiness.\"" },
				[@"OpenDentBusiness\Db Multi Table\Security.cs"]=new[] { "\"OpenDentBusiness.Security.LogInWeb\"" },
				[@"WpfControlsOD\Bridges\DexisIntegrator.cs"]=new[] { "\"OpenDental.FormOpenDental\"" },
			};
			List<string> listProblems=new List<string>();
			foreach(KeyValuePair<string,string[]> pair in dictForbidden) {
				string path=Path.Combine(TestPaths.RepoRoot,pair.Key);
				Assert.IsTrue(File.Exists(path),path);
				string code=Regex.Replace(File.ReadAllText(path),@"^\s*//.*$","",RegexOptions.Multiline);//Whole-line comments may name them.
				foreach(string forbidden in pair.Value) {
					if(code.Contains(forbidden)) {
						listProblems.Add(pair.Key+": "+forbidden);
					}
				}
			}
			//Every HL7 Event Log write names its source through ProgramNames.
			foreach(string folder in new[] { @"OpenDentBusiness\HL7",@"OpenDentBusiness\HL7old",@"OpenDentHL7" }) {
				foreach(string path in Directory.GetFiles(Path.Combine(TestPaths.RepoRoot,folder),"*.cs",SearchOption.AllDirectories)) {
					//A literal source ("OpenDentHL7", or Open Dental's stray "OpenDentalHL7"); "OpenDentHL7 error ..." messages are not sources.
					Match match=Regex.Match(File.ReadAllText(path),@"EventLog\.WriteEntry\(\s*""OpenDent\w*""\s*,");
					if(match.Success) {
						listProblems.Add(path.Substring(TestPaths.RepoRoot.Length+1)+": "+match.Value);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,"Literal run-time names left: "+string.Join("; ",listProblems));
		}

		///<summary>UpdateFileCopier compiles a linked copy of ProgramNames.cs (it replaces CodeBase.dll while it runs), so the file must stay
		///self-contained.</summary>
		[TestMethod]
		public void ProgramNames_IsLinkedIntoUpdateFileCopierAndSelfContained() {
			string csproj=File.ReadAllText(Path.Combine(TestPaths.RepoRoot,"UpdateFileCopier","UpdateFileCopier.csproj"));
			StringAssert.Contains(csproj,@"<Compile Include=""..\CodeBase\Utilities\ProgramNames.cs"">");
			string source=File.ReadAllText(Path.Combine(TestPaths.RepoRoot,"CodeBase","Utilities","ProgramNames.cs"));
			List<string> listUsings=Regex.Matches(source,@"^using\s+([\w.]+);",RegexOptions.Multiline).Cast<Match>().Select(x => x.Groups[1].Value).ToList();
			Assert.IsTrue(listUsings.All(x => x=="System" || x.StartsWith("System.")),"ProgramNames.cs may use only the framework: "+string.Join(", ",listUsings));
		}
	}
}
