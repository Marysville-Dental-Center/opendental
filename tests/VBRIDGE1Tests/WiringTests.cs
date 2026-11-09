// The windows and buttons cannot be opened in a test run (the GUI is not launched), so these tests read the sources and check that each one calls the
// rules the other files test, that the IAP desktop import is gone, and that the new names keep the enum's existing numbers.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;
using ProjectFileTests;

namespace VBRIDGE1Tests {
	[TestClass]
	public class WiringTests {
		///<summary>A repository file with its line endings normalized to \n, since they depend on the checkout.</summary>
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts))).Replace("\r\n","\n");
		}

		///<summary>A project file's evaluated items, so a check holds for the classic project files and the SDK-style ones alike.</summary>
		private static ProjectFile Project(params string[] arrayParts) {
			return ProjectFile.Load(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		///<summary>The repository root: the first folder above the test assembly with NOTICE, which must also hold LICENSE. Found by those files
		///rather than by the solution's name, so it survives a rename.</summary>
		private static string GetRepoRoot() {
			string root=Path.GetDirectoryName(TestDb.FindRepoFile("NOTICE"));
			Assert.IsTrue(File.Exists(Path.Combine(root,"LICENSE")),"LICENSE is not next to NOTICE in "+root);
			return root;
		}

		///<summary>The text of one method: from its signature to the first closing brace at the method's own indent (two tabs).</summary>
		private static string MethodBody(string source,string signature) {
			int idxStart=source.IndexOf(signature);
			Assert.IsTrue(idxStart>=0,signature+" not found.");
			int idxEnd=source.IndexOf("\n\t\t}",idxStart);
			Assert.IsTrue(idxEnd>idxStart,signature+" has no closing brace.");
			return source.Substring(idxStart,idxEnd-idxStart);
		}

		[TestMethod]
		public void ProgramName_NewNamesAreAppended_ExistingNumbersKept() {
			string[] arrayNames=Enum.GetNames(typeof(ProgramName));
			int idxPostcardMania=Array.IndexOf(arrayNames,"PostcardMania");
			CollectionAssert.AreEqual(new[] { "Overjet","DTXStudio","DTXStudioAcquire" },arrayNames.Skip(idxPostcardMania+1).Take(3).ToArray());
			Assert.AreEqual((int)ProgramName.ZImage+1,(int)ProgramName.Microsoft365Email,"Existing values keep their numbers.");
			Assert.IsTrue(Enum.IsDefined(typeof(ProgramName),"IAP"),"IAP keeps its name.");
		}

		[TestMethod]
		public void ProgramL_DispatchesTheNewButtons() {
			string source=Read("WpfControlsOD","Logic","ProgramL.cs");
			StringAssert.Contains(source,"program.ProgName==ProgramName.Overjet.ToString()) {\n\t\t\t\tOverjet.SendData(program,patient);");
			StringAssert.Contains(source,"program.ProgName==ProgramName.DTXStudio.ToString() || program.ProgName==ProgramName.DTXStudioAcquire.ToString()) {"
				+"\n\t\t\t\tDTXStudio.SendData(program,patient);");
		}

		[TestMethod]
		public void Bridges_UseTheBusinessRules() {
			StringAssert.Contains(Read("WpfControlsOD","Bridges","Overjet.cs"),"OverjetLink.GetLaunch(program,patient)");
			StringAssert.Contains(Read("WpfControlsOD","Bridges","DTXStudio.cs"),"DtxStudioLink.GetLaunch(program,patient)");
			string iap=Read("WpfControlsOD","Bridges","Iap.cs");
			StringAssert.Contains(iap,"IapLink.GetLaunch(Programs.GetCur(ProgramName.IAP))");
			Assert.IsFalse(iap.Contains("DllImport"),"No calls into the IAP desktop library.");
			Assert.IsFalse(iap.Contains("iaplus10"),"No calls into the IAP desktop library.");
			Project("WpfControlsOD","WpfControlsOD.csproj").AssertCompiles(@"Bridges\Overjet.cs",@"Bridges\DTXStudio.cs",@"Bridges\Iap.cs");
		}

		[TestMethod]
		public void InsurancePlan_IapButtonOpensTheLoginPage_AndTheImportIsGone() {
			string source=Read("OpenPmsDental","Forms","FormInsPlan.cs");
			string body=MethodBody(source,"private void butIapFind_Click(");
			StringAssert.Contains(body,"Iap.OpenLoginPage();");
			foreach(string removed in new[] { "FormIap","Iap.ReadRecord","Iap.ReadField","Iap.CloseDatabase" }) {
				Assert.IsFalse(source.Contains(removed),removed+" is gone with the IAP desktop import.");
			}
			//The button still shows only when the IAP program link is enabled.
			StringAssert.Contains(source,"program=Programs.GetCur(ProgramName.IAP);\n\t\t\tif(program==null || !program.Enabled) {");
			string root=GetRepoRoot();
			foreach(string file in new[] { "FormIap.cs","FormIap.designer.cs","FormIap.resx" }) {
				Assert.IsFalse(File.Exists(Path.Combine(root,"OpenPmsDental","Bridges",file)),file);
			}
			Assert.IsFalse(Read("OpenPmsDental","OpenPmsDental.csproj").Contains("FormIap"),"FormIap is out of the project.");
			//Nor compiled or embedded from a file left anywhere in the folder (an SDK-style project compiles what is on disk).
			Assert.AreEqual(0,Project("OpenPmsDental","OpenPmsDental.csproj").GetItemsMentioning("FormIap").Count,"FormIap is out of the project.");
		}

		[TestMethod]
		public void ProgramLinks_RegistersTheFourSetups() {
			string source=Read("OpenPmsDental","Logic","VendorDirect","BridgeProgramLinkSetups.cs");
			StringAssert.Contains(source,"public abstract class StandardProgramLinkSetup:IProgramLinkSetup");
			string[][] arrayPairs={
				new[] { "ProgramName.Overjet;","OverjetLink.EnsureProgram();" },
				new[] { "ProgramName.DTXStudio;","DtxStudioLink.EnsureProgram(ProgramName.DTXStudio);" },
				new[] { "ProgramName.DTXStudioAcquire;","DtxStudioLink.EnsureProgram(ProgramName.DTXStudioAcquire);" },
				new[] { "ProgramName.IAP;","IapLink.EnsureProgram();" },
			};
			foreach(string[] arrayPair in arrayPairs) {
				StringAssert.Contains(source,"return "+arrayPair[0]);
				StringAssert.Contains(source,"return "+arrayPair[1]);
			}
			StringAssert.Contains(source,"new FormProgramLinkEdit()");
			Project("OpenPmsDental","OpenPmsDental.csproj").AssertCompiles(@"Logic\VendorDirect\BridgeProgramLinkSetups.cs");
			//FormProgramLinks finds them through the registry; it is not edited.
			StringAssert.Contains(Read("OpenPmsDental","Forms","FormProgramLinks.cs"),"VendorDirect.ProgramLinkSetupRegistry.EnsureAllPrograms()");
		}

		[TestMethod]
		public void ProgramLinkEdit_ChecksTheWebAddressBeforeSaving() {
			string body=MethodBody(Read("OpenPmsDental","Forms","FormProgramLinkEdit.cs"),"private void butSave_Click(");
			int idxCheck=body.IndexOf("BridgeLinkRows.GetPathSaveProblem(textProgName.Text,textPath.Text)");
			Assert.IsTrue(idxCheck>=0,"The save checks the address.");
			Assert.IsTrue(idxCheck<body.IndexOf("ProgramCur.Path=textPath.Text"),"Before the path is copied to the program.");
			Assert.IsTrue(idxCheck<body.IndexOf("Programs.Update(ProgramCur)"),"Before anything is saved.");
		}

		[TestMethod]
		public void Definitions_DescribeAndNeverPromote() {
			BridgeLinkDefinition[] arrayDefinitions={ OverjetLink.GetDefinition(),DtxStudioLink.GetDefinition(ProgramName.DTXStudio),
				DtxStudioLink.GetDefinition(ProgramName.DTXStudioAcquire),IapLink.GetDefinition() };
			string[] arrayForbidden={ "www.","sign up","signup","free trial","learn more","apply now","recommended","partner","opendental","open dental" };
			foreach(BridgeLinkDefinition definition in arrayDefinitions) {
				string text=(definition.ProgDesc+" "+definition.Note+" "+definition.ButtonText).ToLowerInvariant();
				foreach(string forbidden in arrayForbidden) {
					Assert.IsFalse(text.Contains(forbidden),definition.ProgName+" says "+forbidden);
				}
			}
			Assert.AreEqual(0,IapLink.GetDefinition().ListToolBars.Count,"No toolbar button for IAP.");
			Assert.AreEqual("",IapLink.GetDefinition().Path,"The office enters the IAP address; none is shipped.");
			Assert.AreNotEqual(DtxStudioLink.GetDefinition(ProgramName.DTXStudio).ButtonText,DtxStudioLink.GetDefinition(ProgramName.DTXStudioAcquire).ButtonText);
			bool isThrown=false;
			try {
				DtxStudioLink.GetDefinition(ProgramName.Dexis);
			}
			catch(ArgumentException) {
				isThrown=true;
			}
			Assert.IsTrue(isThrown,"Only the two DTX Studio names have a definition.");
		}
	}
}
