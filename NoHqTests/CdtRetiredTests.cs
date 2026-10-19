using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace NoHqTests {
	///<summary>Checks that the CDT library is retired: its secrets and SecurityHash functions live in CodeBase (Secrets, TamperHash), the CDT project
	///and every reference to it are gone, and so are the ADA-content and registration-key functions that used it. Procedure codes come from the
	///procedurecode table. None of these tests needs a database or a network connection. The OpenDental.exe checks read the built program.</summary>
	[TestClass]
	public class CdtRetiredTests {
		private const BindingFlags ALL_DECLARED=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance
			|BindingFlags.DeclaredOnly;

		[TestMethod]
		public void CdtProject_IsDeleted() {
			string pathRoot=RemovedAssembliesTests.GetRepositoryRoot();
			Assert.IsFalse(Directory.Exists(Path.Combine(pathRoot,"CDT")),"The CDT folder should have been deleted.");
			Assert.IsFalse(File.Exists(Path.Combine(pathRoot,"Required dlls","CDT.dll")),"Required dlls\\CDT.dll should have been deleted.");
			foreach(string fileName in new[] { "OpenDental.sln","OpenDental.Local.slnf" }) {
				StringAssert.DoesNotMatch(File.ReadAllText(Path.Combine(pathRoot,fileName)),new System.Text.RegularExpressions.Regex("xCDT"),
					fileName+" still lists the CDT project.");
			}
		}

		[TestMethod]
		public void CdtProject_IsNotReferenced() {
			string pathRoot=RemovedAssembliesTests.GetRepositoryRoot();
			List<string> listOffenders=new List<string>();
			foreach(string pathProject in Directory.EnumerateFiles(pathRoot,"*.csproj",SearchOption.AllDirectories)) {
				if(IsUnderIgnoredFolder(pathRoot,pathProject)) {
					continue;
				}
				string text=File.ReadAllText(pathProject);
				if(text.Contains("xCDT.csproj") || text.Contains("CDT.dll") || text.Contains("<Reference Include=\"CDT\"")) {
					listOffenders.Add(pathProject.Substring(pathRoot.Length+1));
				}
			}
			Assert.AreEqual(0,listOffenders.Count,"Still references CDT: "+string.Join(", ",listOffenders));
		}

		[TestMethod]
		public void CdtAssembly_IsNotReferencedByTheBusinessLayers() {
			List<Assembly> listAssemblies=new List<Assembly> {
				typeof(Patient).Assembly,//OpenDentBusiness
				typeof(CodeBase.Secrets).Assembly,//CodeBase
				typeof(DataConnectionBase.DataConnection).Assembly,
			};
			for(int i=0;i<listAssemblies.Count;i++) {
				Assert.IsFalse(listAssemblies[i].GetReferencedAssemblies().Any(x => x.Name=="CDT"),listAssemblies[i].GetName().Name+" still references CDT.");
			}
			Assert.IsNull(Type.GetType("CDT.Class1, CDT",throwOnError:false),"CDT.Class1 should not load.");
		}

		[TestMethod]
		public void CdtAssembly_IsNotReferencedByTheProgram() {
			string pathRoot=RemovedAssembliesTests.GetRepositoryRoot();
			foreach(string pathRelative in new[] { @"OpenDental\bin\Debug\OpenDental.exe",@"WpfControlsOD\bin\Debug\WpfControlsOD.dll" }) {
				string path=Path.Combine(pathRoot,pathRelative);
				if(!File.Exists(path)) {
					Assert.Inconclusive(path+" is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
				}
				List<string> listReferences=RemovedAssembliesTests.GetReferencedAssemblyNames(path);
				Assert.IsFalse(listReferences.Contains("CDT",StringComparer.OrdinalIgnoreCase),Path.GetFileName(path)+" still references CDT.");
			}
		}

		[TestMethod]
		public void AdaCodeSeeding_IsRemoved() {
			AssertNoMembers(typeof(ProcedureCodes),"GetADAcodes","GetMandibularCodes","GetMaxillaryCodes","ResetADAdescriptionsAndAbbrs",
				"SetTreatAreasForADACodes");
			AssertNoMembers(typeof(ClaimForms),"SetDefaultClaimForm");
		}

		[TestMethod]
		public void RegistrationKeyGeneration_IsRemoved() {
			AssertNoMembers(typeof(RegistrationKeys),"Insert");
		}

		[TestMethod]
		public void ArchSurface_DefaultsToUpperOutsideCanada() {
			//There is no built-in list of mandibular codes any more.
			if(System.Globalization.CultureInfo.CurrentCulture.Name.EndsWith("CA")) {
				Assert.Inconclusive("Runs only outside Canada.");
			}
			Assert.AreEqual("U",CodeMapping.GetArchSurfaceFromProcCode(new ProcedureCode { ProcCode="D7940" }));
		}

		[TestMethod]
		public void ClaimFormBackgrounds_AdaAndDc217AreNotDrawn() {
			foreach(string name in new[] { "ADA2006.gif","ADA2012.gif","ADA2012_J430D.gif","ADA2018_J432.gif","ADA2019_J430.gif","ADA2024_J430.gif","DC-217.gif" }) {
				Assert.IsTrue(ClaimFormItems.IsUnshippedBackground(name),name);
			}
			//The CMS-1500 background is our own resource, and other names are the office's own images in the A to Z folder.
			foreach(string name in new[] { "1500_02_12.gif","MyClaimForm.jpg","","ada2024_j430.gif" }) {
				Assert.IsFalse(ClaimFormItems.IsUnshippedBackground(name),name);
			}
		}

		[TestMethod]
		public void OpenDental_AdaUiIsRemoved() {
			Assembly assembly=LoadBuiltAssembly("OpenDental.exe");
			AssertNoMembers(FindType(assembly,"OpenDental.FormProcTools"),"checkDcodes","checkTreatAreas","_listProcedureCodes");
			AssertNoMembers(FindType(assembly,"OpenDental.FormRegistrationKey"),"ValidateKey");
			Type typeFormDatabaseMaintenance=FindType(assembly,"OpenDental.FormDatabaseMaintenance");
			Assert.AreEqual(1,typeFormDatabaseMaintenance.GetMember("SecurityHashesFix",ALL_DECLARED).Length,
				"Database Maintenance should have the Security Hashes tool.");
		}

		#region Helpers
		///<summary>Build output and other git worktrees aren't part of this checkout.</summary>
		private static bool IsUnderIgnoredFolder(string pathRoot,string path) {
			string pathRelative=path.Substring(pathRoot.Length+1);
			string[] arrayParts=pathRelative.Split(Path.DirectorySeparatorChar);
			return arrayParts.Any(x => x.Equals("bin",StringComparison.OrdinalIgnoreCase) || x.Equals("obj",StringComparison.OrdinalIgnoreCase)
				|| x.Equals(".claude",StringComparison.OrdinalIgnoreCase) || x.Equals(".git",StringComparison.OrdinalIgnoreCase)
				|| x.Equals("packages",StringComparison.OrdinalIgnoreCase));
		}

		///<summary>Loads an assembly from OpenDental\bin\Debug (or Release). The test is inconclusive when it has not been built.</summary>
		private static Assembly LoadBuiltAssembly(string fileName) {
			string dirTests=Path.GetDirectoryName(typeof(CdtRetiredTests).Assembly.Location);//NoHqTests\bin\Debug
			string dirConfiguration=Path.GetFileName(dirTests);
			string path=Path.GetFullPath(Path.Combine(dirTests,"..","..","..","OpenDental","bin",dirConfiguration,fileName));
			if(!File.Exists(path)) {
				Assert.Inconclusive(fileName+" is not built: "+path);
			}
			return Assembly.LoadFrom(path);
		}

		private static Type FindType(Assembly assembly,string typeName) {
			Type type=assembly.GetType(typeName);
			Assert.IsNotNull(type,typeName+" was not found in "+assembly.GetName().Name+".");
			return type;
		}

		private static void AssertNoMembers(Type type,params string[] arrayMemberNames) {
			for(int i=0;i<arrayMemberNames.Length;i++) {
				Assert.AreEqual(0,type.GetMember(arrayMemberNames[i],ALL_DECLARED).Length,type.Name+"."+arrayMemberNames[i]+" should be removed.");
			}
		}
		#endregion Helpers
	}
}
