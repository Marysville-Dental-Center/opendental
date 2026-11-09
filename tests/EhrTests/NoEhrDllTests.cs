// The removed EHR.dll hooks stay removed (docs/specs/ehr-rebuild-plan.md, package EHR-0). This source scan replaced NoHqTests'
// Ehr_ReflectionHooksAreGone, whose three assertions (an empty code list, empty CCD and CCR stylesheets) the open rebuilds of EHR-2 and EHR-3 make
// false on purpose. What must stay true: no code names EHR.dll or one of its types as a string, no EHR file loads an assembly from a file, and the
// dead "#if EHRTEST" block of ControlChart.cs is the only one (TC-2 deletes it there).
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
		///<summary>The one file that may still hold the dead "#if EHRTEST" block, until TC-2 deletes it.</summary>
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
		public void EhrTest_IsOnlyInControlChart() {
			List<string> listProblems=new List<string>();
			Regex regex=new Regex(@"^\s*#\s*(if|elif)\b.*\bEHRTEST\b",RegexOptions.Multiline);
			foreach(string path in ProgramSourceFiles()) {
				string source=File.ReadAllText(path);
				if(source.IndexOf("EHRTEST",StringComparison.Ordinal)<0) {
					continue;
				}
				string relative=TestPaths.Relative(path);
				if(regex.IsMatch(source) && !relative.Equals(PATH_CONTROL_CHART,StringComparison.OrdinalIgnoreCase)) {
					listProblems.Add(relative);
				}
			}
			Assert.AreEqual(0,listProblems.Count,"#if EHRTEST outside ControlChart.cs: "+string.Join("; ",listProblems));
			//Nothing defines it, so the block in ControlChart.cs is dead until TC-2 deletes it.
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
