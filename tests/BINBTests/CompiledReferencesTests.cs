// The built assemblies name none of the removed assemblies, the Word letter merge is late bound, and AvaTax stays disabled without its SDK.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BINBTests {
	[TestClass]
	public class CompiledReferencesTests {
		private static readonly string[] _arrayRemovedAssemblies={ "SigPlusNET","Interop.Word","Interop.Microsoft.Office.Core","Interop.VBIDE","stdole",
			"AxInterop.AcroPDFLib","Interop.AcroPDFLib","Interop.Acrobat","Interop.AFORMAUTLib","zxing","zxing.presentation","MigraDoc.RtfRendering",
			"Tao.FreeGlut","Avalara.AvaTax.RestClient.net45","RS232","Ionic.Zip","installutil" };

		private static List<string> GetRemovedReferences(AssemblyName[] arrayAssemblyNames) {
			return arrayAssemblyNames.Select(x => x.Name).Where(x => _arrayRemovedAssemblies.Contains(x,StringComparer.OrdinalIgnoreCase)).ToList();
		}

		[TestMethod]
		public void BuiltAssemblies_ReferenceNoneOfTheRemovedAssemblies() {
			Dictionary<string,AssemblyName[]> dictionaryReferences=new Dictionary<string,AssemblyName[]> {
				{ "OpenDentBusiness",typeof(LetterMerges).Assembly.GetReferencedAssemblies() },
				{ "CodeBase",typeof(ODZip).Assembly.GetReferencedAssemblies() },
				{ "PasswordVaultWrapper",typeof(PasswordVaultWrapper.WindowsPasswordVaultWrapper).Assembly.GetReferencedAssemblies() },
			};
			//The program itself, when this checkout has built it. Its public references are read without running any of its code.
			foreach(string relative in new[] { @"OpenDental\bin\Debug\OpenDental.exe",@"ServiceManager\bin\Debug\ServiceManager.exe",
				@"SparksToothChart\bin\Debug\SparksToothChart.dll" })
			{
				string path=Path.Combine(TestPaths.RepoRoot,relative);
				if(File.Exists(path)) {
					dictionaryReferences.Add(relative,Assembly.ReflectionOnlyLoadFrom(path).GetReferencedAssemblies());
				}
			}
			List<string> listViolations=new List<string>();
			foreach(KeyValuePair<string,AssemblyName[]> keyValuePair in dictionaryReferences) {
				listViolations.AddRange(GetRemovedReferences(keyValuePair.Value).Select(x => keyValuePair.Key+" references "+x));
			}
			Assert.AreEqual(0,listViolations.Count,string.Join("; ",listViolations));
		}

		[TestMethod]
		public void WordLetterMerge_IsLateBound() {
			Assert.AreEqual(typeof(object),typeof(LetterMerges).GetMethod("GetWordApp").ReturnType,"Word's automation object is used as dynamic.");
			//The values of Word's own enumerations, from Word's object model.
			Assert.AreEqual(0,LetterMerges.WORD_MAIL_MERGE_TO_NEW_DOCUMENT);
			Assert.AreEqual(1,LetterMerges.WORD_MAIL_MERGE_TO_PRINTER);
			Assert.AreEqual(1,LetterMerges.WORD_WINDOW_STATE_MAXIMIZE);
			Assert.AreEqual(2,LetterMerges.WORD_WINDOW_STATE_MINIMIZE);
			Regex regexTyped=new Regex(@"(\bWord\.(_?Application|_?Document|MailMerge)\s+\w+\s*[;=]|\bWord\.Wd\w+\.|new\s+Word\.)");
			List<string> listViolations=new List<string>();
			foreach(string folder in new[] { "OpenDental","OpenDentBusiness","CodeBase","WpfControlsOD" }) {
				foreach(string path in TestPaths.GetFiles(Path.Combine(TestPaths.RepoRoot,folder),".cs")) {
					string[] arrayLines=File.ReadAllLines(path);
					for(int i=0;i<arrayLines.Length;i++) {
						if(arrayLines[i].TrimStart().StartsWith("//")) {
							continue;
						}
						if(regexTyped.IsMatch(arrayLines[i])) {
							listViolations.Add(path.Substring(TestPaths.RepoRoot.Length)+"("+(i+1)+"): "+arrayLines[i].Trim());
						}
					}
				}
			}
			Assert.AreEqual(0,listViolations.Count,"Word interop types are used again: "+string.Join("; ",listViolations));
		}

		[TestMethod]
		public void AvaTax_StaysDisabledWithoutItsSdk() {
			Assert.IsFalse(AvaTax.IsEnabled());
			Assert.IsFalse(AvaTax.IsTaxable(1));
			//A Procedure's constructor reads preferences, which needs a database; the disabled checks never look at their arguments.
			Assert.IsFalse(AvaTax.DoSendProcToAvalara(null));
			Assert.IsFalse(AvaTax.PingAvaTax());
			Assert.AreEqual(0m,AvaTax.GetEstimate(1,1,100));
			Assert.IsFalse(AvaTax.DidUpdateAdjustment(null,null));
			Assert.IsFalse(AvaTax.DoCreateReturnAdjustment(null,null,null));
		}
	}
}
