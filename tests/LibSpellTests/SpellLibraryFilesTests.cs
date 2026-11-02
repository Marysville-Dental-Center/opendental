// NHunspell 0.9.6 and its native Hunspell 1.2 (Hunspellx86.dll, Hunspellx64.dll) are gone from the repository, the projects, the sources and the
// build output; WeCantSpell.Hunspell 7.0.1 (netstandard2.0, managed) is the one spell checker library, used only through WpfControls.SpellCheckL,
// which both boxes call.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WeCantSpell.Hunspell;

namespace LibSpellTests {
	[TestClass]
	public class SpellLibraryFilesTests {
		private static readonly string[] _arrayRemovedFiles=new string[] { "NHunspell.dll","Hunspellx86.dll","Hunspellx64.dll" };
		///<summary>SHA-256 of lib/netstandard2.0/WeCantSpell.Hunspell.dll of the NuGet package WeCantSpell.Hunspell 7.0.1.</summary>
		private const string SHA256_WECANTSPELL="e2b117024fa6ee39de757b73f536cbb86214d263f8e00645d4307a48bd729eea";

		private static string Sha256(string path) {
			using(SHA256 sha256=SHA256.Create()) {
				return string.Concat(sha256.ComputeHash(File.ReadAllBytes(path)).Select(x => x.ToString("x2")));
			}
		}

		[TestMethod]
		public void RequiredDlls_HoldWeCantSpell701_AndNoneOfNHunspell() {
			string folder=RepoPaths.Combine("Required dlls");
			List<string> listLeft=_arrayRemovedFiles.Where(x => File.Exists(Path.Combine(folder,x))).ToList();
			Assert.AreEqual(0,listLeft.Count,"Back in Required dlls: "+string.Join(", ",listLeft));
			string path=Path.Combine(folder,"WeCantSpell.Hunspell.dll");
			Assert.IsTrue(File.Exists(path));
			Assert.AreEqual(SHA256_WECANTSPELL,Sha256(path),"The netstandard2.0 file of the package, unchanged.");
			Assert.AreEqual(new Version(7,0,0,0),AssemblyName.GetAssemblyName(path).Version,"Assembly version (no strong name, so no redirect).");
			Assert.AreEqual("7.0.1.0",FileVersionInfo.GetVersionInfo(path).FileVersion);
			//The assembly the tests load is that file, a netstandard2.0 build that needs no native file.
			Assembly assembly=typeof(WordList).Assembly;
			Assert.AreEqual(SHA256_WECANTSPELL,Sha256(assembly.Location));
			Assert.AreEqual(".NETStandard,Version=v2.0",assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName);
			Assert.AreEqual(0,assembly.GetName().GetPublicKeyToken().Length);
			CollectionAssert.AreEquivalent(new[] { "netstandard","System.Memory","System.Buffers","System.Runtime.CompilerServices.Unsafe" },
				assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray());
		}

		[TestMethod]
		public void Projects_NameNoNHunspellFile_AndOnlyWpfControlsReferencesWeCantSpell() {
			List<string> listProblems=new List<string>();
			List<string> listUsers=new List<string>();
			foreach(string path in RepoPaths.Files(".csproj").Concat(RepoPaths.Files(".vcxproj")).Concat(RepoPaths.Files(".targets"))
				.Concat(RepoPaths.Files(".props")))
			{
				XDocument xDocument=XDocument.Load(path);
				foreach(XElement xElement in xDocument.Descendants()) {
					string local=xElement.Name.LocalName;
					List<string> listValues=new List<string>();
					if(local=="Reference" || local=="None" || local=="Content") {
						listValues.Add((string)xElement.Attribute("Include")??"");
					}
					if(local=="HintPath") {
						listValues.Add(xElement.Value);
					}
					foreach(string value in listValues) {
						string fileName=Path.GetFileName(value.Split(',')[0].Trim().Replace('/','\\'));
						if(fileName.StartsWith("NHunspell",StringComparison.OrdinalIgnoreCase)
							|| fileName.StartsWith("Hunspellx",StringComparison.OrdinalIgnoreCase))
						{
							listProblems.Add(RepoPaths.Relative(path)+": <"+local+"> "+value.Trim());
						}
						if(local=="HintPath" && fileName.Equals("WeCantSpell.Hunspell.dll",StringComparison.OrdinalIgnoreCase)) {
							listUsers.Add(RepoPaths.Relative(path));
						}
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,"NHunspell is named again: "+string.Join("; ",listProblems));
			//The program code reaches the library only through WpfControlsOD; this test project reads the dictionary with it too.
			CollectionAssert.AreEquivalent(new[] { @"WpfControlsOD\WpfControlsOD.csproj",@"tests\LibSpellTests\LibSpellTests.csproj" },listUsers.ToArray());
		}

		[TestMethod]
		public void Sources_UseNoNHunspell_AndOnlySpellCheckLReadsADictionary() {
			//A using directive, a constructed NHunspell object or a qualified type name (NHunspell.Hunspell; not the file name NHunspell.dll in a
			//list of denied files).
			Regex regexNHunspell=new Regex(@"^\s*using\s+NHunspell\s*;|\bnew\s+Hunspell\s*\(|\bNHunspell\.[A-Z]",RegexOptions.Multiline);
			//A using directive, a qualified type name (WeCantSpell.Hunspell.WordList; not the file name WeCantSpell.Hunspell.dll in a list) or a
			//dictionary read.
			Regex regexWeCantSpell=new Regex(@"^\s*using\s+WeCantSpell\b|\bWeCantSpell\.Hunspell\.[A-Z]|\bWordList\.Create",RegexOptions.Multiline);
			List<string> listProblems=new List<string>();
			List<string> listUsers=new List<string>();
			List<string> listSources=RepoPaths.Files(".cs");
			Assert.IsTrue(listSources.Count>1000,"The sources were not found under "+RepoPaths.Root);
			foreach(string path in listSources) {
				string relative=RepoPaths.Relative(path);
				if(relative.StartsWith(@"tests\LibSpellTests\",StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				string text=File.ReadAllText(path);
				if(text.IndexOf("Hunspell",StringComparison.Ordinal)<0 && text.IndexOf("WordList",StringComparison.Ordinal)<0
					&& text.IndexOf("WeCantSpell",StringComparison.Ordinal)<0)
				{
					continue;//most files: neither regex can match
				}
				if(regexNHunspell.IsMatch(text)) {
					listProblems.Add(relative);
				}
				if(regexWeCantSpell.IsMatch(text)) {
					listUsers.Add(relative);
				}
			}
			Assert.AreEqual(0,listProblems.Count,"Source uses NHunspell: "+string.Join("; ",listProblems));
			CollectionAssert.AreEqual(new[] { @"WpfControlsOD\Logic\SpellCheckL.cs" },listUsers.ToArray(),"Only SpellCheckL reads a dictionary.");
		}

		[TestMethod]
		public void Boxes_SpellCheckThroughSpellCheckL() {
			string odTextBox=RepoPaths.ReadText("OpenPmsDental","User Controls","ODtextBox.cs");
			string textRich=RepoPaths.ReadText("WpfControlsOD","UI","Controls","TextRich.xaml.cs");
			foreach(string source in new[] { odTextBox,textRich }) {
				//Created: the dictionary read starts on the thread pool; never a dictionary of the box's own.
				StringAssert.Contains(source,"SpellCheckL.ReadInBackground();");
				StringAssert.Contains(source,"SpellCheckL.Suggest(");
				StringAssert.Contains(source,"SpellCheckL.AddCustomWord(");
				Assert.IsFalse(source.Contains("HunspellGlobal"));
				Assert.IsFalse(source.Contains("DictCustoms.Insert("),"Adding a word goes through SpellCheckL.AddCustomWord.");
				Assert.IsFalse(source.Contains("Required dlls"),"No native file is copied at run time.");
			}
			StringAssert.Contains(odTextBox,"SpellCheckL.IsInDictionary(");
			StringAssert.Contains(odTextBox,"SpellCheckL.IsCustomWord(");
			StringAssert.Contains(textRich,"SpellCheckL.GetMisspelledWords(run,dictCorrect)");
		}

		[TestMethod]
		public void BuildOutput_HoldsWeCantSpell_AndNoNHunspellFile() {
			List<string> listFolders=new List<string> { AppDomain.CurrentDomain.BaseDirectory };
			foreach(string relative in new[] { @"OpenPmsDental\bin\Debug",@"WpfControlsOD\bin\Debug" }) {
				string folder=RepoPaths.Combine(relative);
				if(Directory.Exists(folder)) {
					listFolders.Add(folder);
				}
			}
			List<string> listProblems=new List<string>();
			foreach(string folder in listFolders) {
				listProblems.AddRange(_arrayRemovedFiles.Where(x => File.Exists(Path.Combine(folder,x))).Select(x => Path.Combine(folder,x)+" is there."));
				string path=Path.Combine(folder,"WeCantSpell.Hunspell.dll");
				if(!File.Exists(path)) {
					listProblems.Add(path+" is missing.");
				}
				else if(Sha256(path)!=SHA256_WECANTSPELL) {
					listProblems.Add(path+" is not the file in Required dlls.");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}
	}
}
