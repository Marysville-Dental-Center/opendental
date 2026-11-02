// Guards for the rename of the internal code names: rules that a rename could break without a compiler error.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace NoHqTests {
	///<summary>Rules that the rename to Open PMS Dental must keep and that the compiler does not check: the Middle Tier's namespace rule, the WPF
	///pack URIs, and that the program's temporary folder never empties anything else. Reads the business layer, the sources and the built
	///WpfControlsOD.dll; needs no database or network connection.</summary>
	[TestClass]
	public class RenameGuardTests {
		///<summary>The Middle Tier sends "namespace.Class.Method" (Meth, from DeclaringType.Namespace) and the server takes the first part of the
		///name as the assembly name (DtoProcessor), and parameter types are resolved the same way (DtoObject). So every table type, CRUD type and
		///data-interface type of the business layer needs a namespace whose first part is its assembly's name. A namespace that differs (for
		///example a dotted or shortened root after a rename) breaks every Middle Tier call of that type.</summary>
		[TestMethod]
		public void BusinessLayer_TableCrudAndDataInterfaceTypes_HaveTheAssemblyNameAsTheirRootNamespace() {
			Assembly assembly=typeof(Patient).Assembly;
			string assemblyName=assembly.GetName().Name;
			List<Type> listTypes=GetLoadableTypes(assembly).Where(x => !x.IsNested).ToList();
			List<Type> listTableTypes=listTypes.Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(TableBase))).ToList();
			List<Type> listCrudTypes=listTypes.Where(x => x.IsClass && x.Name.EndsWith("Crud") && x.Namespace!=null
				&& (x.Namespace=="Crud" || x.Namespace.EndsWith(".Crud"))).ToList();
			//Data-interface types: the classes declared in the business layer's source files that send Middle Tier calls.
			HashSet<string> hashSetDataInterfaceNames=GetClassNamesThatCallMeth(assemblyName);
			List<Type> listDataInterfaceTypes=listTypes.Where(x => x.IsClass && hashSetDataInterfaceNames.Contains(x.Name)).ToList();
			Assert.IsTrue(listTableTypes.Count>300,"Only "+listTableTypes.Count+" table types found.");
			Assert.IsTrue(listCrudTypes.Count>300,"Only "+listCrudTypes.Count+" CRUD types found.");
			Assert.IsTrue(listDataInterfaceTypes.Count>300,"Only "+listDataInterfaceTypes.Count+" data-interface types found.");
			CollectionAssert.Contains(listTableTypes,typeof(Patient));
			CollectionAssert.Contains(listCrudTypes,typeof(OpenPmsDentalBusiness.Crud.PatientCrud));
			CollectionAssert.Contains(listDataInterfaceTypes,typeof(Patients));
			CollectionAssert.Contains(listDataInterfaceTypes,typeof(Security));
			List<string> listWrong=listTableTypes.Concat(listCrudTypes).Concat(listDataInterfaceTypes).Distinct()
				.Where(x => !HasAssemblyNameAsRootNamespace(x,assemblyName))
				.Select(x => x.FullName)
				.OrderBy(x => x)
				.ToList();
			Assert.AreEqual(0,listWrong.Count,"Namespaces that do not start with "+assemblyName+" (the Middle Tier cannot reach them): "
				+string.Join(", ",listWrong));
		}

		///<summary>Security.LogInWeb's Middle Tier name is built the same way, so the server finds it under the assembly's name.</summary>
		[TestMethod]
		public void BusinessLayer_LogInWebMethodName_StartsWithTheAssemblyName() {
			string assemblyName=typeof(Security).Assembly.GetName().Name;
			Assert.AreEqual(assemblyName+".Security.LogInWeb",Security.LogInWebMethodName);
		}

		///<summary>WpfControlsOD loads its images and its TabControl theme through pack URIs that name the assembly as text
		///("/WPFControlsOD;component/Resources/..."). Checks that every such URI names the built assembly (pack URIs ignore case), and that what
		///they load is in the assembly's resources: the TabControl theme, every Resource item of the project and every BitmapFileName the
		///windows use. A rename of the assembly without these strings would show blank buttons and an unstyled TabControl.</summary>
		[TestMethod]
		public void WpfControls_PackUrisResolve() {
			string root=RemovedAssembliesTests.GetRepositoryRoot();
			string folderProject=Path.Combine(root,"WpfControlsOD");
			Assert.IsTrue(Directory.Exists(folderProject),folderProject+" is missing.");
			string dirTests=Path.GetDirectoryName(typeof(RenameGuardTests).Assembly.Location);//NoHqTests\bin\Debug
			string pathDll=Path.GetFullPath(Path.Combine(dirTests,"..","..","..","OpenPmsDental","bin",Path.GetFileName(dirTests),"WpfControlsOD.dll"));
			if(!File.Exists(pathDll)) {
				Assert.Inconclusive(pathDll+" is not built.");
			}
			//From a copy of the bytes, so the file stays unlocked. The reflection-only context holds one copy of an assembly, so reuse one.
			string fullName=AssemblyName.GetAssemblyName(pathDll).FullName;
			Assembly assembly=AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().FirstOrDefault(x => x.FullName==fullName)
				??Assembly.ReflectionOnlyLoad(File.ReadAllBytes(pathDll));
			string assemblyName=assembly.GetName().Name;
			HashSet<string> hashSetResources=GetWpfResourceKeys(assembly);
			Assert.IsTrue(hashSetResources.Count>0,"No WPF resources in "+pathDll);
			//The URIs in the source.
			List<string> listPrefixes=new List<string>();
			List<string> listProblems=new List<string>();
			Regex regexUri=new Regex(@"""(?:pack://application:,,,)?/?(?<asm>[A-Za-z0-9_.]+);component/(?<path>[^""]*)""");
			foreach(string pathSource in GetSourceFiles(folderProject,".cs")) {
				string text=File.ReadAllText(pathSource);
				foreach(Match match in regexUri.Matches(StripLineComments(text))) {
					string relative=pathSource.Substring(root.Length+1);
					if(!string.Equals(match.Groups["asm"].Value,assemblyName,StringComparison.OrdinalIgnoreCase)) {
						listProblems.Add(relative+": "+match.Value+" names "+match.Groups["asm"].Value+", not "+assemblyName);
						continue;
					}
					string path=match.Groups["path"].Value;
					if(path.EndsWith("/")) {
						listPrefixes.Add(path);//Completed at run time, for example with a BitmapFileName.
						continue;
					}
					if(!hashSetResources.Contains(ToResourceKey(path))) {
						listProblems.Add(relative+": "+match.Value+" is not in the resources of "+assemblyName);
					}
				}
			}
			Assert.IsTrue(listPrefixes.Count>=2,"Expected the image URIs of Button and PictureBox.");
			Assert.IsTrue(hashSetResources.Contains("themes/tabcontroldict.baml"),"The TabControl theme is not in the resources.");
			//Everything loaded through a prefix: the project's Resource items and the BitmapFileName values of the windows.
			List<string> listFiles=new List<string>();
			string csproj=File.ReadAllText(Path.Combine(folderProject,"WpfControlsOD.csproj"));
			foreach(Match match in Regex.Matches(csproj,@"<Resource Include=""Resources\\(?<file>[^""]+)""")) {
				listFiles.Add(match.Groups["file"].Value);
			}
			foreach(string folder in new[] { folderProject,Path.Combine(root,"OpenPmsDental") }) {
				foreach(string pathXaml in GetSourceFiles(folder,".xaml")) {
					foreach(Match match in Regex.Matches(File.ReadAllText(pathXaml),@"\bBitmapFileName=""(?<file>[^""]+)""")) {
						listFiles.Add(match.Groups["file"].Value);
					}
				}
			}
			Assert.IsTrue(listFiles.Count>=5,"Expected the Resource items and the BitmapFileName values.");
			foreach(string prefix in listPrefixes.Distinct()) {
				foreach(string file in listFiles.Distinct(StringComparer.OrdinalIgnoreCase)) {
					if(!hashSetResources.Contains(ToResourceKey(prefix+file))) {
						listProblems.Add(prefix+file+" is not in the resources of "+assemblyName);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		///<summary>PrefC.GetTempFolderPath creates the program's temporary folder and touches nothing else. Open Dental emptied the user's whole
		///%TEMP% the first time it created the folder, which a renamed folder (or a new dev database) would have set off.</summary>
		[TestMethod]
		public void TempFolder_CreatingItLeavesOtherTempFilesAlone() {
			string folderTemp=Path.Combine(Path.GetTempPath(),"renameguard_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folderTemp);
			string pathOther=Path.Combine(folderTemp,"another program's file.txt");
			File.WriteAllText(pathOther,"keep");
			string tmpOld=Environment.GetEnvironmentVariable("TMP");
			string tempOld=Environment.GetEnvironmentVariable("TEMP");
			try {
				Environment.SetEnvironmentVariable("TMP",folderTemp);
				Environment.SetEnvironmentVariable("TEMP",folderTemp);
				Assert.AreEqual(folderTemp.TrimEnd('\\'),Path.GetTempPath().TrimEnd('\\'),"The test could not point %TEMP% at its own folder.");
				string pathFolder=PrefC.GetTempFolderPath();
				Assert.AreEqual(Path.Combine(folderTemp,ProgramNames.TempFolderName),pathFolder.TrimEnd('\\'));
				Assert.IsTrue(Directory.Exists(pathFolder));
				Assert.IsTrue(File.Exists(pathOther),"Creating the program's temporary folder deleted another file in %TEMP%.");
				Assert.AreEqual(pathFolder,PrefC.GetTempFolderPath(),"A second call returns the same folder.");
				Assert.IsTrue(File.Exists(pathOther));
			}
			finally {
				Environment.SetEnvironmentVariable("TMP",tmpOld);
				Environment.SetEnvironmentVariable("TEMP",tempOld);
				try {
					Directory.Delete(folderTemp,true);
				}
				catch(IOException) {
				}
			}
		}

		#region Helpers
		private static bool HasAssemblyNameAsRootNamespace(Type type,string assemblyName) {
			return type.Namespace==assemblyName || (type.Namespace??"").StartsWith(assemblyName+".",StringComparison.Ordinal);
		}

		private static Type[] GetLoadableTypes(Assembly assembly) {
			try {
				return assembly.GetTypes();
			}
			catch(ReflectionTypeLoadException ex) {
				return ex.Types.Where(x => x!=null).ToArray();
			}
		}

		///<summary>The simple names of the classes declared in the source files of the business layer's project folder (named like the assembly)
		///that send Middle Tier calls: through Meth.Get..., or straight through RemotingClient.Process... (Security.LogInWeb).</summary>
		private static HashSet<string> GetClassNamesThatCallMeth(string assemblyName) {
			string folder=Path.Combine(RemovedAssembliesTests.GetRepositoryRoot(),assemblyName);
			Assert.IsTrue(Directory.Exists(folder),"The business layer's project folder is not named like its assembly: "+folder);
			HashSet<string> hashSetNames=new HashSet<string>();
			Regex regexClass=new Regex(@"^\s*(?:public|internal)?\s*(?:static\s+|partial\s+|sealed\s+)*class\s+(?<name>\w+)",RegexOptions.Multiline);
			foreach(string path in GetSourceFiles(folder,".cs")) {
				string text=File.ReadAllText(path);
				if(!Regex.IsMatch(text,@"\bMeth\.Get\w*[(<]|\bRemotingClient\.Process\w*[(<]")) {
					continue;
				}
				foreach(Match match in regexClass.Matches(text)) {
					hashSetNames.Add(match.Groups["name"].Value);
				}
			}
			return hashSetNames;
		}

		///<summary>The keys of the assembly's WPF resources (AssemblyName.g.resources): lower-case paths, with .xaml compiled to .baml.</summary>
		private static HashSet<string> GetWpfResourceKeys(Assembly assembly) {
			HashSet<string> hashSetKeys=new HashSet<string>();
			string resourceName=assembly.GetName().Name+".g.resources";
			using(Stream stream=assembly.GetManifestResourceStream(resourceName)) {
				Assert.IsNotNull(stream,resourceName+" is missing.");
				using(ResourceReader resourceReader=new ResourceReader(stream)) {
					IDictionaryEnumerator enumerator=resourceReader.GetEnumerator();
					while(enumerator.MoveNext()) {
						hashSetKeys.Add(((string)enumerator.Key).ToLowerInvariant());
					}
				}
			}
			return hashSetKeys;
		}

		///<summary>The resource key a pack URI path loads: lower case, and a .xaml page is stored compiled as .baml.</summary>
		private static string ToResourceKey(string path) {
			string key=Uri.UnescapeDataString(path).Replace('\\','/').TrimStart('/').ToLowerInvariant();
			if(key.EndsWith(".xaml")) {
				key=key.Substring(0,key.Length-5)+".baml";
			}
			return key;
		}

		private static List<string> GetSourceFiles(string folder,string extension) {
			string sep=Path.DirectorySeparatorChar.ToString();
			return Directory.GetFiles(folder,"*"+extension,SearchOption.AllDirectories)
				.Where(x => x.EndsWith(extension,StringComparison.OrdinalIgnoreCase))
				.Where(x => !x.Contains(sep+"bin"+sep) && !x.Contains(sep+"obj"+sep))
				.ToList();
		}

		///<summary>The text with //-comments blanked out (a commented-out URI loads nothing), keeping line breaks. A "//" inside a string literal
		///such as "pack://application" is kept.</summary>
		private static string StripLineComments(string text) {
			return Regex.Replace(text,@"^(?<code>(?:[^""/\r\n]|""[^""\r\n]*""|/(?!/))*)//[^\r\n]*",m => m.Groups["code"].Value,RegexOptions.Multiline);
		}
		#endregion Helpers
	}
}
