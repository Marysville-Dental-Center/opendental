// Tests that Open Dental's closed binaries removed by package BIN-A stay removed (docs/port-notes/BIN-A.md): the Suni capture driver
// (xImageDeviceManager.dll and its SuniUSB.dll), the VB helper VBbridges.dll and the Sparks3D tooth chart (Sparks3D.dll, Sparks3DXEngine.dll).
// Nothing in the repository may hold, reference, copy, publish or use them. Reads the source and the built assemblies only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using SparksToothChart;

namespace BINATests {
	[TestClass]
	public class ProprietaryBinariesRemovedTests {
		///<summary>The assembly names of the removed binaries.</summary>
		private static readonly string[] _arrayRemovedAssemblies=new[] { "xImageDeviceManager","SuniUSB","VBbridges","Sparks3D","Sparks3DXEngine" };

		[TestMethod]
		public void RequiredDlls_HoldsNoneOfTheRemovedBinaries() {
			string folder=Path.Combine(RepoFiles.Root(),"Required dlls");
			Assert.IsTrue(Directory.Exists(folder));
			List<string> listLeft=new[] { "xImageDeviceManager.dll","SuniUSB.dll","VBbridges.dll" }.Where(x => File.Exists(Path.Combine(folder,x))).ToList();
			Assert.AreEqual(0,listLeft.Count,"Still in Required dlls: "+string.Join(", ",listLeft));
			Assert.IsFalse(Directory.Exists(Path.Combine(folder,"Sparks3D")),@"Required dlls\Sparks3D must stay deleted.");
			List<string> listAnywhere=RepoFiles.Files("*.dll").Where(x => _arrayRemovedAssemblies.Contains(Path.GetFileNameWithoutExtension(x),StringComparer.OrdinalIgnoreCase))
				.ToList();
			Assert.AreEqual(0,listAnywhere.Count,"Still in the repository: "+string.Join(", ",listAnywhere));
		}

		[TestMethod]
		public void Projects_ReferenceNoneOfTheRemovedBinaries() {
			//A Reference, a HintPath, or a file item named after them (Sparks3DInterface.cs, the Sparks3D licence text).
			Regex regex=new Regex(@"<Reference\s+Include=""(xImageDeviceManager|SuniUSB|VBbridges|Sparks3D|Sparks3DXEngine)\b"
				+@"|<HintPath>[^<]*(xImageDeviceManager|SuniUSB|VBbridges|Sparks3D)[^<]*</HintPath>"
				+@"|<(Compile|None|Content|EmbeddedResource)\s+Include=""[^""]*(Sparks3D|SuniUSB|VBbridges|xImageDeviceManager)",RegexOptions.IgnoreCase);
			List<string> listOffenders=new List<string>();
			foreach(string relative in RepoFiles.Files("*.csproj","*.vcxproj","*.props","*.targets","*.sln","*.slnf")) {
				Match match=regex.Match(RepoFiles.Read(relative));
				if(match.Success) {
					listOffenders.Add(relative+" ("+match.Value+")");
				}
			}
			Assert.AreEqual(0,listOffenders.Count,"Still referenced in: "+string.Join(", ",listOffenders));
		}

		[TestMethod]
		public void Source_UsesNoTypeOfTheRemovedBinaries() {
			//Type use: a using directive, a qualified name (namespace dot capital letter, so "Sparks3D.dll" in a comment is not a hit), the Suni
			//capture control, the Sparks3D adapter and its switch, and the Sparks3D licence resource.
			Regex regex=new Regex(@"^\s*using\s+(xImageDeviceManager|VBbridges|Sparks3D)\s*;"
				+@"|\b(xImageDeviceManager|VBbridges|Sparks3D)\.[A-Z]"
				+@"|\bSuniDeviceControl\b|\bSparks3DInterface\b|\bIsSparks3DPresent\b|Resources\.Sparks3D\b",RegexOptions.Multiline);
			List<string> listOffenders=new List<string>();
			foreach(string relative in RepoFiles.Files("*.cs","*.vb","*.xaml")) {
				Match match=regex.Match(RepoFiles.Read(relative));
				if(match.Success) {
					listOffenders.Add(relative+" ("+match.Value.Trim()+")");
				}
			}
			Assert.AreEqual(0,listOffenders.Count,"Still uses a removed binary: "+string.Join(", ",listOffenders));
		}

		[TestMethod]
		public void BuiltLibraries_ReferenceNoneOfTheRemovedBinaries() {
			foreach(Assembly assembly in new[] { typeof(Patient).Assembly,typeof(CodeBase.ODFileUtils).Assembly,typeof(ToothChartWrapper).Assembly }) {
				List<string> listReferences=assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();
				foreach(string removed in _arrayRemovedAssemblies) {
					Assert.IsFalse(listReferences.Contains(removed,StringComparer.OrdinalIgnoreCase),assembly.GetName().Name+" still references "+removed);
				}
			}
		}

		[TestMethod]
		public void BuiltProgram_ReferencesNoneOfTheRemovedBinaries() {
			foreach(string pathRelative in new[] { @"OpenDental\bin\Debug\OpenDental.exe",@"WpfControlsOD\bin\Debug\WpfControlsOD.dll" }) {
				string path=Path.Combine(RepoFiles.Root(),pathRelative);
				if(!File.Exists(path)) {
					Assert.Inconclusive(pathRelative+" is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
				}
				List<string> listReferences=RepoFiles.ReferencedAssemblyNames(path);
				foreach(string removed in _arrayRemovedAssemblies) {
					Assert.IsFalse(listReferences.Contains(removed,StringComparer.OrdinalIgnoreCase),pathRelative+" still references "+removed);
				}
			}
		}

		[TestMethod]
		public void ReleaseChannel_PublishesNoSparks3DFolder() {
			Assert.IsFalse(ForkReleases.ListSpecialDirs.Contains("Sparks3D",StringComparer.OrdinalIgnoreCase),
				"The fork release must not publish a Sparks3D folder.");
			string source=RepoFiles.Read(@"UpdateFileCopier\FormMain.cs");
			Match match=Regex.Match(source,@"_listSpecialDirs=new List<string>\(\)\s*\{(?<items>[^}]*)\}");
			Assert.IsTrue(match.Success,"UpdateFileCopier's special folder list not found.");
			StringAssert.DoesNotMatch(match.Groups["items"].Value,new Regex("Sparks3D",RegexOptions.IgnoreCase),
				"UpdateFileCopier must not copy a Sparks3D folder.");
		}

		[TestMethod]
		public void SuniCapture_IsGoneFromTheOldImagesModuleAndScanningSetup() {
			string controlImagesOld=RepoFiles.Read(@"OpenDental\Main Modules\ControlImagesOld.cs");
			StringAssert.DoesNotMatch(controlImagesOld,new Regex(@"""Capture Image From Device""|ToolBarCapture_Click|KillXRayThread|CaptureXRay"));
			StringAssert.DoesNotMatch(RepoFiles.Read(@"OpenDental\Main Modules\ControlImagesOld.designer.cs"),new Regex("KillXRayThread"));
			string imagingSetup=RepoFiles.Read(@"OpenDental\Forms\FormImagingSetup.cs")+RepoFiles.Read(@"OpenDental\Forms\FormImagingSetup.designer.cs");
			StringAssert.DoesNotMatch(imagingSetup,new Regex(@"groupBoxSuni|upDownExposure|checkBinned|ProgDesc==""Suni"""),
				"The Suni sensor settings were only read by the removed Suni driver.");
		}

		[TestMethod]
		public void LicenceLists_DoNotNameSparks3D() {
			foreach(string relative in new[] { @"OpenDental\Forms\FormLicense.cs",@"OpenDental\Forms\FormRegistrationKey.cs",
				@"OpenDental\Properties\Resources.resx" })
			{
				StringAssert.DoesNotMatch(RepoFiles.Read(relative),new Regex("Sparks3D"),relative);
			}
			Assert.IsFalse(File.Exists(Path.Combine(RepoFiles.Root(),@"OpenDental\Resources\Sparks3D.txt")));
		}

		[TestMethod]
		public void GraphicsWindow_HasNoDirectX11Settings() {
			string graphics=RepoFiles.Read(@"OpenDental\Forms\FormGraphics.cs")+RepoFiles.Read(@"OpenDental\Forms\FormGraphics.designer.cs");
			StringAssert.DoesNotMatch(graphics,new Regex(@"radioDirectX11|Prefs\.UpdateBool\(PrefName\.DirectX11ToothChartUseIfAvail"),
				"The DirectX 11 settings were only read by the removed Sparks3D chart.");
			StringAssert.DoesNotMatch(RepoFiles.Read(@"OpenDental\Main Modules\ControlChart.cs"),
				new Regex(@"PrefName\.DirectX11ToothChartUseIfAvail|LocalComputer\.GraphicsUseDirectX11"),"The chart no longer reads the Sparks3D settings.");
		}
	}
}
