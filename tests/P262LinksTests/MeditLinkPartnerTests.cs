// Vendor follow-up from public documentation (next/vendorpub-1): the Partner ID of the Medit Link program link. Medit's public article on its Open
// Dental integration adds a PartnerID key as the last key of the JSON object in the link's arguments, which assigns that partner (a lab) to every
// case created through the integration. The arguments with and without it, the refusals, the property row and the wiring. Nothing is launched.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace P262LinksTests {
	[TestClass]
	public class MeditLinkPartnerTests {
		///<summary>The Command line Open Dental's 21.1 conversion gave the Medit Link row (ConvertDatabases7, the same in db/init/new-database.sql).
		///</summary>
		private const string COMMAND_LINE=@"{\""NameFL\"": \""[NameFL]\"", \""PatNum\"": \""[PatNum]\"", \""PatientGenderMF\"": \""[PatientGenderMF]\"", \""Birthdate_yyyyMMdd\"": \""[Birthdate_yyyyMMdd]\""}";
		///<summary>Medit's article, the Optional command line arguments for Open Dental 22.1 or later with a partner; "xxxxx" is the partner's ID.</summary>
		private const string ARTICLE_WITH_PARTNER=@"{\""NameFL\"": \""[NameFL]\"", \""PatNum\"": \""[PatNum]\"", \""PatientGenderMF\"": \""[PatientGenderMF]\"", \""Birthdate_yyyyMMdd\"": \""[Birthdate_yyyyMMdd]\"", \""PartnerID\"": \""xxxxx\""}";
		private const string UUID_PART="\"--commandUuid\" \"E02698AC-C1AE-4699-9307-F4A58DED47FE\"";
		///<summary>Glidewell Dental's PartnerID in Medit's table of supported partners.</summary>
		private const string GLIDEWELL="7307395";

		///<summary>A stand-in for Patients.ReplacePatient: only the four tokens of the Command line, no database.</summary>
		private static string Replace(string text,Patient patient) {
			return text.Replace("[NameFL]","Zoe Nunez").Replace("[PatNum]",patient.PatNum.ToString()).Replace("[PatientGenderMF]","F")
				.Replace("[Birthdate_yyyyMMdd]","19800309");
		}

		private static string Arguments(string commandLine,string partnerId,Patient patient,out string problem) {
			return MeditLinkPartner.GetArguments(commandLine,partnerId,patient,Replace,out problem);
		}

		[TestMethod]
		public void WithoutPartnerId_TheArgumentsAreAsBefore() {
			Patient patient=SyntheticPatients.Create();
			foreach(string partnerId in new[] { "",null,"   " }) {
				string arguments=Arguments(COMMAND_LINE,partnerId,patient,out string problem);
				Assert.AreEqual("",problem);
				Assert.AreEqual(UUID_PART+" \"--commandParam\" \"{\\\"NameFL\\\": \\\"Zoe Nunez\\\", \\\"PatNum\\\": \\\"1042\\\", \\\"PatientGenderMF\\\": \\\"F\\\", "
					+"\\\"Birthdate_yyyyMMdd\\\": \\\"19800309\\\"}\"",arguments,"What the link sent before next/vendorpub-1.");
			}
			//An office's own Command line is not checked when there is no Partner ID.
			Assert.AreEqual(UUID_PART+" \"--commandParam\" \"anything 1042\"",Arguments("anything [PatNum]","",patient,out string problemOwn));
			Assert.AreEqual("",problemOwn);
			Assert.AreEqual("E02698AC-C1AE-4699-9307-F4A58DED47FE",MeditLinkPartner.CommandUuid);
		}

		[TestMethod]
		public void WithPartnerId_TheKeyIsAddedExactlyAsMeditsArticleShows() {
			Assert.AreEqual(ARTICLE_WITH_PARTNER.Replace("xxxxx",GLIDEWELL),MeditLinkPartner.AddPartnerId(COMMAND_LINE,GLIDEWELL));
			Assert.AreEqual(ARTICLE_WITH_PARTNER.Replace("xxxxx",GLIDEWELL),MeditLinkPartner.AddPartnerId(COMMAND_LINE+"  ",GLIDEWELL+" "),
				"Trailing spaces of the Command line and the Partner ID are not kept.");
			Assert.AreEqual(COMMAND_LINE,MeditLinkPartner.AddPartnerId(COMMAND_LINE,""),"Blank: unchanged.");
			Assert.AreEqual(@"{\""PartnerID\"": \""42\""}",MeditLinkPartner.AddPartnerId("{ }","42"),"An empty object gets the key alone.");
			string arguments=Arguments(COMMAND_LINE,GLIDEWELL,SyntheticPatients.Create(),out string problem);
			Assert.AreEqual("",problem);
			Assert.AreEqual(UUID_PART+" \"--commandParam\" \"{\\\"NameFL\\\": \\\"Zoe Nunez\\\", \\\"PatNum\\\": \\\"1042\\\", \\\"PatientGenderMF\\\": \\\"F\\\", "
				+"\\\"Birthdate_yyyyMMdd\\\": \\\"19800309\\\", \\\"PartnerID\\\": \\\"7307395\\\"}\"",arguments);
			Assert.AreEqual("PartnerID",MeditLinkPartner.PartnerIdKey,"Spelled as the article spells it.");
		}

		[TestMethod]
		public void WithoutAPatient_OnlyTheCommandId_ThePartnerIdIsNotUsed() {
			foreach(string partnerId in new[] { "",GLIDEWELL,"not valid;" }) {
				Assert.AreEqual(UUID_PART,Arguments(COMMAND_LINE,partnerId,null,out string problem));
				Assert.AreEqual("",problem,"Nothing to refuse without a command parameter.");
			}
			Assert.AreEqual(UUID_PART,Arguments("no json",GLIDEWELL,null,out string problemNoJson));
		}

		[TestMethod]
		public void PartnerIdThatCantBeAdded_IsRefused_NothingStarts() {
			Patient patient=SyntheticPatients.Create();
			Dictionary<string,string> dictCases=new Dictionary<string,string> {
				//Partner ID, Command line => what the message says
				{ "73 07|"+COMMAND_LINE,"letters, digits, hyphens and underscores" },
				{ "7307395\"|"+COMMAND_LINE,"letters, digits, hyphens and underscores" },
				{ "a;b|"+COMMAND_LINE,"letters, digits, hyphens and underscores" },
				{ GLIDEWELL+"|"+ARTICLE_WITH_PARTNER,"already holds a PartnerID" },
				{ GLIDEWELL+"|"+COMMAND_LINE.Replace("PatNum","partnerid"),"already holds a PartnerID" },
				{ GLIDEWELL+"|/patient [PatNum]","ends with }" },
				{ GLIDEWELL+"|","ends with }" },
			};
			foreach(KeyValuePair<string,string> pair in dictCases) {
				string partnerId=pair.Key.Substring(0,pair.Key.IndexOf('|'));
				string commandLine=pair.Key.Substring(pair.Key.IndexOf('|')+1);
				Assert.IsNull(Arguments(commandLine,partnerId,patient,out string problem),pair.Key);
				StringAssert.Contains(problem,pair.Value,pair.Key);
				Assert.AreEqual(problem,MeditLinkPartner.GetPartnerIdProblem(commandLine,partnerId));
				Assert.AreEqual(commandLine,MeditLinkPartner.AddPartnerId(commandLine,partnerId),"A refused Command line is returned unchanged.");
			}
			foreach(string partnerId in new[] { GLIDEWELL,"Lab-1","lab_2","X" }) {
				Assert.AreEqual("",MeditLinkPartner.GetPartnerIdProblem(COMMAND_LINE,partnerId),partnerId);
			}
		}

		[TestMethod]
		public void Property_IsAddedBlankToTheExistingRowOnly_TheRowIsNotChanged() {
			ScratchDb.RequireDb();
			Assert.IsFalse(MeditLinkPartner.EnsureProperty(),"No Medit Link row: nothing is inserted (Open Dental's conversion creates the row).");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM program"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM programproperty"));
			//The row as db/init/new-database.sql has it (ProgramNum 127, from Open Dental's 21.1 conversion).
			DataCore.NonQ("INSERT INTO program (ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,IsDisabledByHq,CustErr) "
				+"VALUES (127,'MeditLink','Medit Link from www.meditlink.com',0,'"+POut.String(@"C:\Program Files\Medit\Medit Link\Medit Link\Medit_Link.exe")+"','"
				+POut.String(COMMAND_LINE)+"','','','','','',0,'')");
			ScratchDb.RefreshCaches();
			DataTable tableBefore=DataCore.GetTable("SELECT * FROM program WHERE ProgramNum=127");
			Assert.IsTrue(MeditLinkPartner.EnsureProperty());
			ScratchDb.RefreshCaches();
			Assert.IsFalse(MeditLinkPartner.EnsureProperty(),"Once.");
			DataTable tableProps=DataCore.GetTable("SELECT PropertyDesc,PropertyValue,ClinicNum,IsMasked,IsHighSecurity FROM programproperty WHERE ProgramNum=127");
			Assert.AreEqual(1,tableProps.Rows.Count);
			Assert.AreEqual("Partner ID",tableProps.Rows[0]["PropertyDesc"].ToString());
			Assert.AreEqual("",tableProps.Rows[0]["PropertyValue"].ToString(),"Blank: nothing is added to the arguments.");
			Assert.AreEqual("0",tableProps.Rows[0]["ClinicNum"].ToString());
			Assert.AreEqual("0",tableProps.Rows[0]["IsMasked"].ToString());
			DataTable tableAfter=DataCore.GetTable("SELECT * FROM program WHERE ProgramNum=127");
			foreach(DataColumn column in tableBefore.Columns) {
				Assert.AreEqual(tableBefore.Rows[0][column.ColumnName].ToString(),tableAfter.Rows[0][column.ColumnName].ToString(),"program."+column.ColumnName);
			}
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM toolbutitem"),"No button is added.");
			//The office's value is kept and read by the button, with the real patient token replacement.
			DataCore.NonQ("UPDATE programproperty SET PropertyValue=' "+GLIDEWELL+" ' WHERE ProgramNum=127");
			ScratchDb.RefreshCaches();
			Assert.IsFalse(MeditLinkPartner.EnsureProperty());
			Program program=Programs.GetCur(ProgramName.MeditLink);
			Assert.AreEqual(GLIDEWELL,MeditLinkPartner.GetPartnerId(program));
			Patient patient=SyntheticPatients.Create();
			patient.Gender=PatientGender.Female;
			patient.Birthdate=new DateTime(1980,3,9);
			string arguments=MeditLinkPartner.GetArguments(program,patient,out string problem);
			Assert.AreEqual("",problem);
			StringAssert.StartsWith(arguments,UUID_PART+" \"--commandParam\" \"{\\\"NameFL\\\": \\\"");
			StringAssert.EndsWith(arguments,"\\\"PatNum\\\": \\\"1042\\\", \\\"PatientGenderMF\\\": \\\"F\\\", \\\"Birthdate_yyyyMMdd\\\": \\\"19800309\\\", "
				+"\\\"PartnerID\\\": \\\"7307395\\\"}\"");
			Assert.AreEqual("",MeditLinkPartner.GetPartnerId(null));
		}

		[TestMethod]
		public void Wiring_TheButtonUsesTheRules_ProgramLinksAddsTheProperty() {
			string bridge=TestPaths.ReadRepoFile("WpfControlsOD","Bridges","MeditLink.cs");
			StringAssert.Contains(bridge,"MeditLinkPartner.GetArguments(program,patient,out string problem)");
			int indexRefused=bridge.IndexOf("if(commandArgs==null) {",StringComparison.Ordinal);
			int indexStart=bridge.IndexOf("Process.Start(path,commandArgs);",StringComparison.Ordinal);
			Assert.IsTrue(indexRefused>0 && indexStart>indexRefused,"A refused Partner ID stops the launch.");
			StringAssert.Contains(bridge.Substring(indexRefused,indexStart-indexRefused),"return;");
			Assert.IsFalse(bridge.Contains("E02698AC"),"The command id lives in MeditLinkPartner only.");
			StringAssert.Contains(TestPaths.ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs"),"MeditLink.SendData(");
			string setup=TestPaths.ReadRepoFile("OpenPmsDental","Logic","VendorDirect","MeditLinkProgramLinkSetup.cs");
			StringAssert.Contains(setup,"public class MeditLinkProgramLinkSetup:StandardProgramLinkSetup");
			StringAssert.Contains(setup,"return ProgramName.MeditLink;");
			StringAssert.Contains(setup,"return MeditLinkPartner.EnsureProperty();");
			StringAssert.Contains(TestPaths.ReadRepoFile("OpenPmsDental","OpenPmsDental.csproj"),"<Compile Include=\"Logic\\VendorDirect\\MeditLinkProgramLinkSetup.cs\" />");
			Regex regexUrl=new Regex(@"https?://|www\.",RegexOptions.IgnoreCase);
			foreach(string[] arrayPath in new[] {
				new[] { "OpenPmsDentalBusiness","VendorDirect","ProgramLinks","MeditLinkPartner.cs" },
				new[] { "OpenPmsDental","Logic","VendorDirect","MeditLinkProgramLinkSetup.cs" },
				new[] { "WpfControlsOD","Bridges","MeditLink.cs" },
				new[] { "OpenPmsDentalBusiness","Misc","ForkMigrations","OrthoCompositePatientArgumentsStep.cs" } })
			{
				string text=TestPaths.ReadRepoFile(arrayPath);
				Assert.IsFalse(regexUrl.IsMatch(text),string.Join("\\",arrayPath)+" names a web address.");
				Assert.IsTrue(text.All(x => x<128),string.Join("\\",arrayPath)+" should be plain ASCII.");
			}
		}
	}
}
