// The removed EHR.dll hooks stay removed (docs/specs/ehr-rebuild-plan.md, package EHR-0). This source scan replaced NoHqTests'
// Ehr_ReflectionHooksAreGone, whose three assertions (an empty code list, empty CCD and CCR stylesheets) the open rebuilds of EHR-2 and EHR-3 make
// false on purpose. What must stay true: no code names EHR.dll or one of its types as a string, no project file, configuration file or resource
// names EHR.dll outside comments, no EHR file loads an assembly from a file, and no "#if EHRTEST" block is left (TC-2 deleted the last one, the
// dead block of ControlChart.cs).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EhrTests {
	[TestClass]
	public class NoEhrDllTests {
		///<summary>The Chart module, which held the last, dead "#if EHRTEST" block until TC-2 deleted it.</summary>
		private const string PATH_CONTROL_CHART=@"OpenPmsDental\Main Modules\ControlChart.cs";
		private static List<string> _listProgramSourceFiles;

		private static List<string> ProgramSourceFiles() {
			if(_listProgramSourceFiles==null) {
				_listProgramSourceFiles=TestPaths.ProgramSourceFiles();
			}
			return _listProgramSourceFiles;
		}

		///<summary>The EHR files: the program's and the business library's files whose name has "Ehr" in it, and the files the hooks were in.</summary>
		private static List<string> EhrFiles() {
			List<string> listFiles=ProgramSourceFiles().Where(x => {
				string relative=TestPaths.Relative(x);
				return (relative.StartsWith(@"OpenPmsDental\",StringComparison.OrdinalIgnoreCase)
					|| relative.StartsWith(@"OpenPmsDentalBusiness\",StringComparison.OrdinalIgnoreCase))
					&& Path.GetFileName(x).IndexOf("Ehr",StringComparison.OrdinalIgnoreCase)>=0;
			}).ToList();
			foreach(string relative in new[] { @"OpenPmsDental\Forms\FormShowFeatures.cs",PATH_CONTROL_CHART,@"OpenPmsDental\Main Modules\FormOpenDentalThreads.cs" }) {
				listFiles.Add(TestPaths.Combine(relative));
			}
			return listFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		[TestMethod]
		public void TheScan_SeesTheProgram() {
			//So a scan that found nothing cannot pass by reading nothing.
			List<string> listFiles=ProgramSourceFiles();
			Assert.IsTrue(listFiles.Count>3000,"C# files of the program: "+listFiles.Count);
			Assert.IsTrue(listFiles.Any(x => TestPaths.Relative(x).Equals(@"OpenPmsDental\Forms\FormEHR.cs",StringComparison.OrdinalIgnoreCase)));
			Assert.IsFalse(listFiles.Any(x => TestPaths.Relative(x).StartsWith(@"tests\",StringComparison.OrdinalIgnoreCase)),"Tests name what they check.");
			List<string> listEhrFiles=EhrFiles();
			Assert.IsTrue(listEhrFiles.Count>100,"EHR files: "+listEhrFiles.Count);
			Assert.IsTrue(listEhrFiles.All(x => File.Exists(x)));
			foreach(string relative in new[] { @"OpenPmsDentalBusiness\Data Interface\EhrCodes.cs",@"OpenPmsDentalBusiness\Data Interface\EhrSummaryCcds.cs",
				@"OpenPmsDental\Forms\FormEHR.cs",@"OpenPmsDental\Forms\FormEhrSetup.cs" })
			{
				Assert.IsTrue(listEhrFiles.Any(x => TestPaths.Relative(x).Equals(relative,StringComparison.OrdinalIgnoreCase)),relative);
			}
		}

		[TestMethod]
		public void TheReader_SeparatesCommentsFromStrings() {
			string code="string a=\"http://example.test/EHR.dll\";//EHR.dll in a comment\r\n"
				+"/* \"EHR.EhrCodeList\" in a block comment */ string b=@\"C:\\x\\\"\"EHR.dll\"\"\";\r\n"
				+"char c='\"'; string d=\"EHR. Then\"; string e=$\"{f}EHR.FormEhrMeasures\";";
			CSharpSource cSharpSource=CSharpSource.Read(code);
			CollectionAssert.AreEqual(new[] { "http://example.test/EHR.dll","C:\\x\\\"EHR.dll\"","EHR. Then","{f}EHR.FormEhrMeasures" },
				cSharpSource.ListStringLiterals);
			Assert.IsFalse(cSharpSource.CodeWithoutComments.Contains("in a comment"));
			Assert.IsFalse(cSharpSource.CodeWithoutComments.Contains("block comment"));
			Assert.AreEqual(cSharpSource.CodeWithoutComments.Length,cSharpSource.CodeWithoutCommentsOrLiterals.Length);
			Assert.AreEqual("void M() { string s=\"}\"; if(x) { y(); } }",
				CSharpSource.GetBlock("class C { void M() { string s=\"}\"; if(x) { y(); } } void N() { }","void M()"),"Braces in strings are not counted.");
		}

		[TestMethod]
		public void NoCode_NamesEhrDll() {
			//Comments may tell the history ("lived in Open Dental's proprietary EHR.dll"); a string literal is what a reflection hook needs.
			List<string> listProblems=new List<string>();
			Regex regex=new Regex(@"\bEHR\.(dll\b|[A-Z]\w*)",RegexOptions.IgnoreCase);
			foreach(string path in ProgramSourceFiles()) {
				string source=File.ReadAllText(path);
				if(source.IndexOf("EHR.",StringComparison.OrdinalIgnoreCase)<0) {
					continue;
				}
				CSharpSource cSharpSource=CSharpSource.Read(source);
				foreach(string literal in cSharpSource.ListStringLiterals) {
					Match match=regex.Match(literal);
					if(match.Success && (match.Value.EndsWith("dll",StringComparison.OrdinalIgnoreCase) || char.IsUpper(match.Value[4]))) {
						listProblems.Add(TestPaths.Relative(path)+": \""+literal+"\"");
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,"Strings that name EHR.dll or a type in it:\r\n"+string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void NoProjectFileConfigOrResource_NamesEhrDll() {
			//The plan's "no EHR.dll outside comments" for the project files, the configuration files and the resources too: a reference, a hint
			//path, a copy step or a resource string could load or ship it again. XML comments (and a .resx file's translator notes) may tell the
			//history, as this project's own header does. The scanner's own text is C# in tests\EhrTests, which these extensions do not reach.
			List<string> listFiles=TestPaths.RepositoryFiles("*.csproj","*.config","*.resx");
			//So a scan that found nothing cannot pass by reading nothing.
			Assert.IsTrue(listFiles.Count(x => x.EndsWith(".csproj",StringComparison.OrdinalIgnoreCase))>50,"Project files: "+listFiles.Count);
			Assert.IsTrue(listFiles.Count(x => x.EndsWith(".config",StringComparison.OrdinalIgnoreCase))>10,"Configuration files.");
			Assert.IsTrue(listFiles.Count(x => x.EndsWith(".resx",StringComparison.OrdinalIgnoreCase))>500,"Resources.");
			foreach(string relative in new[] { @"OpenPmsDental\OpenPmsDental.csproj",@"OpenPmsDentalBusiness\OpenPmsDentalBusiness.csproj",
				@"OpenPmsDental\app.config",@"OpenPmsDental\Forms\FormEhrTimeSynch.resx",@"tests\EhrTests\EhrTests.csproj" })
			{
				Assert.IsTrue(listFiles.Any(x => TestPaths.Relative(x).Equals(relative,StringComparison.OrdinalIgnoreCase)),relative);
			}
			Assert.IsFalse(listFiles.Any(x => Regex.IsMatch(TestPaths.Relative(x),@"(^|\\)(bin|obj)\\",RegexOptions.IgnoreCase)),"No build output.");
			Regex regexDll=new Regex(@"\bEHR\.dll\b",RegexOptions.IgnoreCase);
			Regex regexReference=new Regex(@"\bInclude\s*=\s*""EHR(""|,)",RegexOptions.IgnoreCase);
			List<string> listProblems=new List<string>();
			foreach(string path in listFiles) {
				string text=File.ReadAllText(path);
				if(text.IndexOf("EHR",StringComparison.OrdinalIgnoreCase)<0) {
					continue;
				}
				string textWithoutComments=XmlText.WithoutComments(text);
				foreach(Regex regex in new[] { regexDll,regexReference }) {
					Match match=regex.Match(textWithoutComments);
					if(match.Success) {
						listProblems.Add(TestPaths.Relative(path)+": "+match.Value);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,"Project files, configuration files or resources that name EHR.dll:\r\n"+string.Join("\r\n",listProblems));
			//This project's header names EHR.dll in a comment, so the comment rule is what lets it pass.
			string project=TestPaths.ReadSource(@"tests\EhrTests\EhrTests.csproj");
			StringAssert.Contains(project,"EHR.dll");
			Assert.IsFalse(regexDll.IsMatch(XmlText.WithoutComments(project)));
		}

		[TestMethod]
		public void TheXmlReader_DropsCommentsOnly() {
			string xml="<Project><!-- EHR.dll in a comment\r\n over two lines --><ItemGroup><Reference Include=\"EHR\" /><None Include=\"x\\EHR.dll\" />"
				+"</ItemGroup><comment/><data name=\"a\"><value>Uses EHR.dll</value><comment>EHR.dll translator note</comment></data></Project>";
			string text=XmlText.WithoutComments(xml);
			Assert.IsFalse(text.Contains("in a comment") || text.Contains("two lines"),"An XML comment.");
			Assert.IsFalse(text.Contains("translator note"),"A .resx comment element.");
			StringAssert.Contains(text,"<Reference Include=\"EHR\" />");
			StringAssert.Contains(text,"<None Include=\"x\\EHR.dll\" />");
			StringAssert.Contains(text,"<value>Uses EHR.dll</value>","An empty comment element does not run on to a later one.");
			Assert.AreEqual("",XmlText.WithoutComments(null));
		}

		[TestMethod]
		public void NoEhrFile_LoadsAnAssemblyFromAFile() {
			List<string> listProblems=new List<string>();
			foreach(string path in EhrFiles()) {
				string code=CSharpSource.Read(File.ReadAllText(path)).CodeWithoutComments;
				foreach(string call in new[] { "Assembly.LoadFile(","Assembly.LoadFrom(","Assembly.UnsafeLoadFrom(","Assembly.Load(","AppDomain.CurrentDomain.Load(" }) {
					if(code.Contains(call)) {
						listProblems.Add(TestPaths.Relative(path)+": "+call);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
		}

		[TestMethod]
		public void EhrTest_IsNowhere() {
			List<string> listProblems=new List<string>();
			Regex regex=new Regex(@"^\s*#\s*(if|elif)\b.*\bEHRTEST\b",RegexOptions.Multiline);
			foreach(string path in ProgramSourceFiles()) {
				string source=File.ReadAllText(path);
				if(source.IndexOf("EHRTEST",StringComparison.Ordinal)<0) {
					continue;
				}
				string relative=TestPaths.Relative(path);
				if(regex.IsMatch(source)) {
					listProblems.Add(relative);
				}
			}
			Assert.AreEqual(0,listProblems.Count,"#if EHRTEST is back: "+string.Join("; ",listProblems));
			//Nothing defines it either.
			foreach(string pathProject in Directory.GetFiles(TestPaths.Combine("OpenPmsDental"),"*.csproj")) {
				Assert.IsFalse(File.ReadAllText(pathProject).Contains("EHRTEST"),pathProject);
			}
		}

		[TestMethod]
		public void TheCodeListThread_IsGone_AndItsEnumValueKeepsItsNumber() {
			string threads=CSharpSource.Read(TestPaths.ReadSource(@"OpenPmsDental\Main Modules\FormOpenDentalThreads.cs")).CodeWithoutComments;
			Assert.IsFalse(threads.Contains("case FormODThreadNames.EhrCodeList:"),"The \"do not kill\" case of the removed code list thread.");
			Type typeThreadNames=typeof(OpenPmsDental.FormOpenDental).GetNestedType("FormODThreadNames");
			Assert.IsNotNull(typeThreadNames);
			FieldInfo fieldInfo=typeThreadNames.GetField("EhrCodeList");
			Assert.IsNotNull(fieldInfo,"The value stays, so the later values keep their numbers.");
			Assert.IsNotNull(fieldInfo.GetCustomAttribute<ObsoleteAttribute>(),"Marked [Obsolete] like CacheFillForFees.");
			Assert.IsNotNull(typeThreadNames.GetField("CacheFillForFees").GetCustomAttribute<ObsoleteAttribute>());
			Assert.AreEqual(7,(int)fieldInfo.GetValue(null));
			Assert.AreEqual(8,(int)typeThreadNames.GetField("EnableAdditionalFeatures").GetValue(null));
		}
	}
}
