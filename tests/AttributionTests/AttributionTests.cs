// Tests for decoupling Phase 4 attribution (docs/decoupling-plan.md): README.md, NOTICE and the About box.
// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental;

namespace AttributionTests {
	[TestClass]
	public class AttributionTests {
		///<summary>The attribution the owner chose, word for word.</summary>
		private const string ExpectedAttribution="Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under "
			+"the GNU GPL v2. It is not affiliated with or endorsed by Open Dental Software, Inc.";

		#region Helpers
		///<summary>The repository root: the first folder above the test assembly that holds OpenDental.sln and LICENSE.</summary>
		private static string RepoRoot() {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				if(File.Exists(Path.Combine(directoryInfo.FullName,"OpenDental.sln")) && File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE"))) {
					return directoryInfo.FullName;
				}
				directoryInfo=directoryInfo.Parent;
			}
			Assert.Fail("Repository root not found above "+AppDomain.CurrentDomain.BaseDirectory);
			return null;
		}

		private static string ReadRepoFile(params string[] pathParts) {
			string path=Path.Combine(new[] { RepoRoot() }.Concat(pathParts).ToArray());
			Assert.IsTrue(File.Exists(path),"Missing file: "+path);
			return File.ReadAllText(path);
		}

		///<summary>Joins wrapped lines so a sentence split over lines still matches.</summary>
		private static string Unwrap(string text) {
			return string.Join(" ",text.Split(new[] { "\r\n","\n" },StringSplitOptions.None).Select(x => x.Trim()).Where(x => x!=""));
		}

		///<summary>An in-memory assembly with the given version and, when informationalVersion is not null, an AssemblyInformationalVersion.</summary>
		private static Assembly MakeAssembly(Version version,string informationalVersion) {
			AssemblyName assemblyName=new AssemblyName("AttributionTestsDynamic"+Guid.NewGuid().ToString("N"));
			assemblyName.Version=version;
			CustomAttributeBuilder[] arrayAttributes=new CustomAttributeBuilder[0];
			if(informationalVersion!=null) {
				ConstructorInfo constructorInfo=typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) });
				arrayAttributes=new[] { new CustomAttributeBuilder(constructorInfo,new object[] { informationalVersion }) };
			}
			return AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName,AssemblyBuilderAccess.Run,arrayAttributes);
		}
		#endregion Helpers

		#region README and NOTICE
		[TestMethod]
		public void Notice_ExistsAtRootWithAttribution() {
			string notice=ReadRepoFile("NOTICE");
			StringAssert.Contains(Unwrap(notice),ExpectedAttribution);
			StringAssert.StartsWith(notice.TrimStart(),"Open PMS - Dental");
		}

		[TestMethod]
		public void Notice_KeepsOpenDentalCopyrightAndPointsToGplV2() {
			string notice=Unwrap(ReadRepoFile("NOTICE"));
			StringAssert.Contains(notice,"Copyright (C) 2003");
			StringAssert.Contains(notice,"Jordan Sparks");
			StringAssert.Contains(notice,"GNU General Public License, version 2");
			StringAssert.Contains(notice,"LICENSE");
		}

		[TestMethod]
		public void Readme_TopSectionNamesProductAndAttribution() {
			string[] arrayLines=ReadRepoFile("README.md").Split(new[] { "\r\n","\n" },StringSplitOptions.None);
			Assert.AreEqual("# Open PMS - Dental",arrayLines[0].Trim());
			//The attribution comes before the first second-level heading, so it is the first thing a reader sees.
			int indexFirstSection=Array.FindIndex(arrayLines,x => x.StartsWith("## "));
			Assert.IsTrue(indexFirstSection>0,"README.md has no second-level heading.");
			string top=string.Join("\n",arrayLines.Take(indexFirstSection));
			StringAssert.Contains(top,ExpectedAttribution);
			StringAssert.Contains(top,"(NOTICE)");
			StringAssert.Contains(top,"(LICENSE)");
		}

		[TestMethod]
		public void License_IsGplV2() {
			string license=ReadRepoFile("LICENSE");
			StringAssert.Contains(license,"GNU GENERAL PUBLIC LICENSE");
			StringAssert.Contains(license,"Version 2, June 1991");
		}
		#endregion README and NOTICE

		#region About box
		[TestMethod]
		public void AboutBox_AttributionIsExact() {
			Assert.AreEqual(ExpectedAttribution,AboutBoxInfo.Attribution);
		}

		[TestMethod]
		public void AboutBox_ProductNameIsOpenPmsDental() {
			Assert.AreEqual("Open PMS - Dental",AboutBoxInfo.ProductName);
			Assert.AreEqual(CodeBase.VendorDirect.ProductIdentity.ProductName,AboutBoxInfo.ProductName);
		}

		[TestMethod]
		public void ReleaseVersion_UsesInformationalVersionWhenSet() {
			Assembly assembly=MakeAssembly(new Version(24,3,63,0),"0.0");
			Assert.AreEqual("0.0",AboutBoxInfo.GetReleaseVersion(assembly));
			Assert.AreEqual("24.3.63.0",AboutBoxInfo.GetAssemblyVersion(assembly));
		}

		[TestMethod]
		public void ReleaseVersion_TrimsInformationalVersion() {
			Assembly assembly=MakeAssembly(new Version(1,0,0,0)," 1.2.3-beta ");
			Assert.AreEqual("1.2.3-beta",AboutBoxInfo.GetReleaseVersion(assembly));
		}

		[TestMethod]
		public void ReleaseVersion_FallsBackToAssemblyVersionWithoutInformationalVersion() {
			Assembly assembly=MakeAssembly(new Version(24,3,63,0),null);
			Assert.AreEqual("24.3.63.0",AboutBoxInfo.GetReleaseVersion(assembly));
		}

		[TestMethod]
		public void ReleaseVersion_FallsBackToAssemblyVersionWhenInformationalVersionIsBlank() {
			Assembly assembly=MakeAssembly(new Version(25,3,48,0),"   ");
			Assert.AreEqual("25.3.48.0",AboutBoxInfo.GetReleaseVersion(assembly));
		}

		[TestMethod]
		public void ReleaseVersion_NullAssemblyGivesZero() {
			Assert.AreEqual("0.0",AboutBoxInfo.GetReleaseVersion(null));
			Assert.AreEqual("0.0",AboutBoxInfo.GetAssemblyVersion(null));
		}

		[TestMethod]
		public void ReleaseVersion_OfTheProgramMatchesItsAttributes() {
			Assembly assemblyProgram=typeof(AboutBoxInfo).Assembly;
			AssemblyInformationalVersionAttribute attribute=assemblyProgram.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
			string expected=(attribute==null || string.IsNullOrWhiteSpace(attribute.InformationalVersion))
				? assemblyProgram.GetName().Version.ToString()
				: attribute.InformationalVersion.Trim();
			Assert.AreEqual(expected,AboutBoxInfo.GetReleaseVersion(assemblyProgram));
		}

		[TestMethod]
		public void AboutBox_ShowsNameAttributionAndReleaseVersion() {
			string code=ReadRepoFile("OpenDental","Forms","FormAbout.cs");
			StringAssert.Contains(code,"labelProductName.Text=AboutBoxInfo.ProductName;");
			StringAssert.Contains(code,"labelAttribution.Text=AboutBoxInfo.Attribution;");
			StringAssert.Contains(code,"AboutBoxInfo.GetReleaseVersion(assemblyProgram)");
			string designer=ReadRepoFile("OpenDental","Forms","FormAbout.designer.cs");
			StringAssert.Contains(designer,"this.Controls.Add(this.labelProductName);");
			StringAssert.Contains(designer,"this.Controls.Add(this.labelAttribution);");
			//The Open Dental logo is no longer shown.
			Assert.IsFalse(designer.Contains("ODLogo"),"The About box still shows the Open Dental logo.");
		}

		[TestMethod]
		public void AboutBox_KeepsEveryCopyrightLine() {
			string code=ReadRepoFile("OpenDental","Forms","FormAbout.cs");
			StringAssert.Contains(code,"\"Copyright 2003-\"");
			StringAssert.Contains(code,"Jordan Sparks, D.M.D.");
			StringAssert.Contains(code,"\"MySQL - Copyright 1995-\"");
			StringAssert.Contains(code,"\"MariaDB - Copyright 2009-\"");
			StringAssert.Contains(code,"FormLicense");
			string designer=ReadRepoFile("OpenDental","Forms","FormAbout.designer.cs");
			StringAssert.Contains(designer,"All CDT codes are Copyrighted by the ADA.");
			StringAssert.Contains(designer,"this.Controls.Add(this.labelCopyright);");
			StringAssert.Contains(designer,"this.Controls.Add(this.labelMySQLCopyright);");
			StringAssert.Contains(designer,"this.Controls.Add(this.labelMariaDBCopyright);");
			StringAssert.Contains(designer,"this.Controls.Add(this.label9);");
		}

		[TestMethod]
		public void AboutBox_DoesNotRenameWindowTitleOrAssembly() {
			//The rename of window titles, namespaces and assemblies is the final cosmetic step, not this one.
			string designer=ReadRepoFile("OpenDental","Forms","FormAbout.designer.cs");
			StringAssert.Contains(designer,"this.Text = \"About\";");
			Assert.AreEqual("OpenDental",typeof(AboutBoxInfo).Assembly.GetName().Name);
			Assert.AreEqual("OpenDental",typeof(AboutBoxInfo).Namespace);
		}
		#endregion About box
	}
}
