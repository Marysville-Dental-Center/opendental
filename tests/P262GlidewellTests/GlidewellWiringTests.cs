// How the Glidewell lab cases are wired into the program, read from the sources (OpenPmsDental.exe is not loaded or launched by tests): the
// program name, the Edit Lab Case window, the Program Links registration, the project files, the vendor-direct rule (no other company's server,
// no network code until Glidewell's interface is documented) and vendor neutrality. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262GlidewellTests {
	[TestClass]
	public class GlidewellWiringTests {
		///<summary>Every source file of the package, relative to the repository root.</summary>
		private static readonly string[] _arrayPackageFiles={
			@"OpenPmsDentalBusiness\VendorDirect\Glidewell\GlidewellClient.cs",
			@"OpenPmsDentalBusiness\VendorDirect\Glidewell\GlidewellJson.cs",
			@"OpenPmsDentalBusiness\VendorDirect\Glidewell\GlidewellLabCases.cs",
			@"OpenPmsDentalBusiness\VendorDirect\Glidewell\GlidewellProgram.cs",
			@"OpenPmsDentalBusiness\Data Interface\GlidewellAccts.cs",
			@"OpenPmsDentalBusiness\Data Interface\GlidewellMsgs.cs",
			@"OpenPmsDentalBusiness\Data Interface\LabCaseProducts.cs",
			@"OpenPmsDental\Forms\FormGlidewellSetup.cs",
			@"OpenPmsDental\Forms\FormGlidewellSetup.Designer.cs",
			@"OpenPmsDental\Forms\FormGlidewellAcctEdit.cs",
			@"OpenPmsDental\Forms\FormGlidewellAcctEdit.Designer.cs",
			@"OpenPmsDental\Forms\FormLabCaseProductEdit.cs",
			@"OpenPmsDental\Forms\FormLabCaseProductEdit.Designer.cs",
			@"OpenPmsDental\Forms\FormLabCaseEdit.Glidewell.cs",
		};

		///<summary>The repository root: the first folder above this assembly that holds LICENSE and NOTICE.</summary>
		private static string GetRepositoryRoot() {
			string path=Path.GetDirectoryName(typeof(GlidewellWiringTests).Assembly.Location);
			while(path!=null) {
				if(File.Exists(Path.Combine(path,"LICENSE")) && File.Exists(Path.Combine(path,"NOTICE"))) {
					return path;
				}
				path=Path.GetDirectoryName(path);
			}
			throw new InvalidOperationException("Repository root not found.");
		}

		private static string Read(string relativePath) {
			return File.ReadAllText(Path.Combine(GetRepositoryRoot(),relativePath));
		}

		[TestMethod]
		public void ProgramName_GlidewellIsAppended_ExistingNumbersKept() {
			string[] arrayNames=Enum.GetNames(typeof(ProgramName));
			Assert.AreEqual("Glidewell",arrayNames.Last());
			Assert.AreEqual((int)ProgramName.MeetInTheCloud+1,(int)ProgramName.Glidewell);
			Assert.AreEqual((int)ProgramName.ZImage+1,(int)ProgramName.Microsoft365Email,"Earlier values keep their numbers.");
		}

		[TestMethod]
		public void PackageFiles_AreInTheProjects() {
			string csprojBusiness=Read(@"OpenPmsDentalBusiness\OpenPmsDentalBusiness.csproj");
			StringAssert.Contains(csprojBusiness,@"<Compile Include=""VendorDirect\**\*.cs"" />","The Glidewell folder is compiled through the wildcard.");
			foreach(string name in new[] { "GlidewellAccts","GlidewellMsgs","LabCaseProducts" }) {
				StringAssert.Contains(csprojBusiness,@"<Compile Include=""Data Interface\"+name+@".cs"" />");
			}
			string csproj=Read(@"OpenPmsDental\OpenPmsDental.csproj");
			foreach(string form in new[] { "FormGlidewellSetup","FormGlidewellAcctEdit","FormLabCaseProductEdit" }) {
				StringAssert.Contains(csproj,@"<Compile Include=""Forms\"+form+@".cs"">");
				StringAssert.Contains(csproj,@"<Compile Include=""Forms\"+form+@".Designer.cs"">");
				StringAssert.Contains(csproj,@"<EmbeddedResource Include=""Forms\"+form+@".resx"">");
			}
			StringAssert.Contains(csproj,@"<Compile Include=""Forms\FormLabCaseEdit.Glidewell.cs"">");
			foreach(string file in _arrayPackageFiles) {
				Assert.IsTrue(File.Exists(Path.Combine(GetRepositoryRoot(),file)),file);
			}
		}

		[TestMethod]
		public void ProgramLinks_RegistersTheSetupWindow() {
			string source=Read(@"OpenPmsDental\Forms\FormGlidewellSetup.cs");
			StringAssert.Contains(source,"public class GlidewellProgramLinkSetup:IProgramLinkSetup {");
			StringAssert.Contains(source,"return ProgramName.Glidewell;");
			StringAssert.Contains(source,"return GlidewellProgram.EnsureProgram();");
		}

		[TestMethod]
		public void LabCaseWindow_ShowsGlidewellOnlyWhenTurnedOn() {
			string designer=Read(@"OpenPmsDental\Forms\FormLabCaseEdit.designer.cs");
			StringAssert.Contains(designer,"this.groupGlidewell.Visible = false;","Hidden until the program link is on.");
			StringAssert.Contains(designer,"this.Controls.Add(this.groupGlidewell);");
			string partial=Read(@"OpenPmsDental\Forms\FormLabCaseEdit.Glidewell.cs").Replace("\r\n","\n");
			StringAssert.Contains(partial,"if(!GlidewellProgram.IsEnabled()) {\n\t\t\t\tgroupGlidewell.Visible=false;\n\t\t\t\treturn;");
			StringAssert.Contains(partial,"if(!_isGlidewellShown) {","Products are saved only while the group is in use.");
			string form=Read(@"OpenPmsDental\Forms\FormLabCaseEdit.cs");
			StringAssert.Contains(form,"LoadGlidewell(patient);");
			StringAssert.Contains(form,"SaveGlidewellProducts();");
			StringAssert.Contains(form,"if(IsSentToGlidewell()) {","A new lab case that was sent is not deleted on Cancel.");
		}

		[TestMethod]
		public void VendorDirect_NoOtherServerAndNoNetworkCodeYet() {
			Regex regexUrl=new Regex(@"(?i)\b(https?|ftp)://");
			Regex regexNetwork=new Regex(@"\b(HttpClient|WebRequest|WebClient|HttpWebRequest|TcpClient|Socket|APIRequest)\b");
			List<string> listProblems=new List<string>();
			foreach(string file in _arrayPackageFiles) {
				string source=Read(file);
				if(regexUrl.IsMatch(source)) {
					listProblems.Add(file+": a web address (the office's own setup holds every address; none is compiled in).");
				}
				if(source.IndexOf("opendental.com",StringComparison.OrdinalIgnoreCase)>=0) {
					listProblems.Add(file+": names Open Dental's servers.");
				}
				if(regexNetwork.IsMatch(source)) {
					listProblems.Add(file+": network code; Glidewell's interface is not publicly documented, so nothing may be sent yet.");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void VendorNeutral_NoPromotion() {
			Regex regexPromotion=new Regex(@"(?i)\b(sign up|signup|recommended|partner program|logos?|buy now|free trial)\b|www\.");
			List<string> listProblems=new List<string>();
			foreach(string file in _arrayPackageFiles) {
				string source=Read(file);
				foreach(Match match in regexPromotion.Matches(source)) {
					listProblems.Add(file+": "+match.Value);
				}
			}
			//Texts say what is sent, so the office knows what leaves the practice.
			StringAssert.Contains(Read(@"OpenPmsDental\Forms\FormGlidewellSetup.Designer.cs"),"Sent: the patient");
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void ApiKey_NeedsHighSecurityPermission() {
			string source=Read(@"OpenPmsDental\Forms\FormGlidewellAcctEdit.cs");
			StringAssert.Contains(source,"Security.IsAuthorized(EnumPermType.ManageHighSecurityProgProperties,true)");
			StringAssert.Contains(source,"GlidewellAccts.SetApiKey(");
			StringAssert.Contains(Read(@"OpenPmsDental\Forms\FormGlidewellAcctEdit.Designer.cs"),"this.textApiKey.UseSystemPasswordChar = true;");
		}
	}
}
