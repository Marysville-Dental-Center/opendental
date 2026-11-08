// Allied Star Connect (Open Dental 26.2 item 44): the row definition from the public setup page, the order of the launch checks, the info file
// and the Info file path rules. No database, no file is written, nothing is launched.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace P262LinksTests {
	[TestClass]
	public class AlliedstarConnectLinkTests {
		private const string PATH=@"C:\Program Files\Alliedstar\Connect\Connect.exe";
		private const string INFO=@"C:\ProgramData\Alliedstar\Connect\info.json";

		///<summary>Parses the info file with dates left as text, as the file holds them.</summary>
		private static JObject Parse(string json) {
			using(JsonTextReader reader=new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling=DateParseHandling.None }) {
				return JObject.Load(reader);
			}
		}

		private static BridgeFileLaunch Launch(string path,string info,string patOrChart,string commandLine,Patient patient,bool fileExists=true,
			bool folderExists=true)
		{
			return AlliedstarConnectLink.GetLaunch(path,info,patOrChart,commandLine,patient,x => fileExists,x => folderExists);
		}

		[TestMethod]
		public void Definition_HasThePublicPageDefaults() {
			BridgeLinkDefinition definition=AlliedstarConnectLink.GetDefinition();
			Assert.AreEqual(ProgramName.AlliedstarConnect,definition.ProgName);
			Assert.AreEqual("Allied Star Connect",definition.ProgDesc);
			Assert.AreEqual(PATH,definition.Path);
			Assert.AreEqual("",definition.CommandLine);
			CollectionAssert.AreEqual(new[] { EnumToolBar.ChartModule },definition.ListToolBars,"The public page's default toolbar.");
			Assert.AreEqual("Allied Star Connect",definition.ButtonText);
			CollectionAssert.AreEqual(new[] { "Enter 0 to use PatientNum, or 1 to use ChartNum","Info file path" },definition.ListProperties.Select(x => x.Key).ToList());
			CollectionAssert.AreEqual(new[] { "0",INFO },definition.ListProperties.Select(x => x.Value).ToList());
			Assert.AreEqual(AlliedstarConnectLink.Note,definition.Note);
		}

		[TestMethod]
		public void Launch_ChecksInOrder() {
			Patient patient=SyntheticPatients.Create();
			Assert.AreEqual(BridgeLaunchProblem.ProgramPathBlank,Launch("  ",INFO,"0","",patient).Problem);
			Assert.AreEqual(BridgeLaunchProblem.ProgramNotFound,Launch(PATH,"not a path","0","",patient,fileExists:false).Problem,"The program before the info path.");
			Assert.AreEqual(PATH,Launch(PATH,INFO,"0","",patient,fileExists:false).FileName,"The message shows the path.");
			Assert.AreEqual(BridgeLaunchProblem.InfoFilePathInvalid,Launch(PATH,"info.json","0","",patient,folderExists:false).Problem);
			Assert.AreEqual(BridgeLaunchProblem.InfoFolderMissing,Launch(PATH,INFO,"0","",patient,folderExists:false).Problem);
			Assert.AreEqual(INFO,Launch(PATH,INFO,"0","",patient,folderExists:false).HandOffFilePath,"The message shows the folder.");
			BridgeFileLaunch ready=Launch(PATH,INFO,"0","",patient);
			Assert.IsTrue(ready.IsReady);
			Assert.AreEqual(PATH,ready.FileName);
			Assert.AreEqual("",ready.Arguments,"No patient data on the command line.");
			Assert.AreEqual(INFO,ready.HandOffFilePath);
			Assert.IsTrue(ready.HasPatientData);
			Assert.IsNotNull(ready.HandOffText);
		}

		[TestMethod]
		public void Launch_BlankInfoFilePathMeansTheDefault() {
			BridgeFileLaunch launch=Launch(PATH,"","0","",SyntheticPatients.Create());
			Assert.IsTrue(launch.IsReady);
			Assert.AreEqual(INFO,launch.HandOffFilePath);
			Assert.AreEqual(INFO,AlliedstarConnectLink.GetInfoFilePath("   "));
			Assert.AreEqual(@"D:\Scans\in.json",AlliedstarConnectLink.GetInfoFilePath(@" D:\Scans\in.json "));
		}

		[TestMethod]
		public void Launch_WithoutAPatient_RemovesTheOldFileAndStartsConnect() {
			//The folder is not needed: there is nothing to write, and a missing folder holds no stale file.
			BridgeFileLaunch launch=Launch(PATH,INFO,"0","",null,folderExists:false);
			Assert.IsTrue(launch.IsReady);
			Assert.IsNull(launch.HandOffText,"Null means: remove the old info file, so Connect can never open the previous patient.");
			Assert.AreEqual(INFO,launch.HandOffFilePath);
			Assert.IsFalse(launch.HasPatientData);
			Assert.AreEqual(BridgeLaunchProblem.InfoFilePathInvalid,Launch(PATH,"relative\\info.json","0","",null).Problem);
		}

		[TestMethod]
		public void Launch_CommandLineIsPassedAsTyped() {
			Assert.AreEqual("--minimized [PatNum]",Launch(PATH,INFO,"0","  --minimized [PatNum] ",SyntheticPatients.Create()).Arguments,
				"No token is replaced on the command line: the patient goes through the info file only.");
		}

		[TestMethod]
		public void InfoJson_HasTheFieldsInOrder_AndNothingElse() {
			Patient patient=SyntheticPatients.Create();
			JObject json=Parse(AlliedstarConnectLink.BuildInfoJson(patient,false));
			CollectionAssert.AreEqual(AlliedstarConnectLink.JsonFields.ArrayAll,json.Properties().Select(x => x.Name).ToList());
			CollectionAssert.AreEqual(new[] { "PatientId","LastName","FirstName","Birthdate","Gender","Email" },AlliedstarConnectLink.JsonFields.ArrayAll);
			Assert.AreEqual("1042",(string)json["PatientId"]);
			Assert.AreEqual(SyntheticPatients.LName,(string)json["LastName"],"Names are written as they are, accents and quotes included (JSON escapes them).");
			Assert.AreEqual(SyntheticPatients.FName,(string)json["FirstName"]);
			Assert.AreEqual("1980-03-09",(string)json["Birthdate"]);
			Assert.AreEqual("Female",(string)json["Gender"]);
			Assert.AreEqual("first@example.com",(string)json["Email"],"The first address of the comma-separated list (public page).");
			Assert.IsTrue(json.Properties().All(x => x.Value.Type==JTokenType.String));
			string text=AlliedstarConnectLink.BuildInfoJson(patient,false);
			foreach(string notSent in new[] { patient.SSN,patient.Address,patient.Zip,patient.HmPhone,patient.WirelessPhone,"second@example.com" }) {
				Assert.IsFalse(text.Contains(notSent),notSent+" must not be in the info file.");
			}
		}

		[TestMethod]
		public void InfoJson_ChartNumberAndBlankValues() {
			Patient patient=SyntheticPatients.Create();
			Assert.AreEqual("CH-77",(string)Parse(AlliedstarConnectLink.BuildInfoJson(patient,true))["PatientId"]);
			Assert.AreEqual("CH-77",(string)Parse(Launch(PATH,INFO,"1","",patient).HandOffText)["PatientId"],"Property value 1 sends the ChartNumber.");
			Assert.AreEqual("1042",(string)Parse(Launch(PATH,INFO,"","",patient).HandOffText)["PatientId"],"Blank sends the PatNum.");
			patient.Birthdate=DateTime.MinValue;
			patient.Email="";
			patient.Gender=PatientGender.Unknown;
			patient.ChartNumber=null;
			JObject json=Parse(AlliedstarConnectLink.BuildInfoJson(patient,true));
			Assert.AreEqual("",(string)json["Birthdate"]);
			Assert.AreEqual("",(string)json["Email"]);
			Assert.AreEqual("Unknown",(string)json["Gender"]);
			Assert.AreEqual("",(string)json["PatientId"]);
		}

		[TestMethod]
		public void InfoJson_BirthdateIgnoresRegionalSettings() {
			CultureInfo cultureOld=Thread.CurrentThread.CurrentCulture;
			try {
				Thread.CurrentThread.CurrentCulture=new CultureInfo("ar-SA");
				Assert.AreEqual("1980-03-09",(string)Parse(AlliedstarConnectLink.BuildInfoJson(SyntheticPatients.Create(),false))["Birthdate"]);
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureOld;
			}
		}

		[TestMethod]
		public void FirstEmail_IsTheFirstOfTheCommaSeparatedList() {
			Assert.AreEqual("",AlliedstarConnectLink.GetFirstEmail(null));
			Assert.AreEqual("",AlliedstarConnectLink.GetFirstEmail(" , "));
			Assert.AreEqual("a@example.com",AlliedstarConnectLink.GetFirstEmail("a@example.com"));
			Assert.AreEqual("b@example.com",AlliedstarConnectLink.GetFirstEmail(" ,b@example.com,c@example.com"));
		}

		[TestMethod]
		public void GenderText() {
			Assert.AreEqual("Male",AlliedstarConnectLink.GetGenderText(PatientGender.Male));
			Assert.AreEqual("Female",AlliedstarConnectLink.GetGenderText(PatientGender.Female));
			Assert.AreEqual("Other",AlliedstarConnectLink.GetGenderText(PatientGender.Other));
			Assert.AreEqual("Unknown",AlliedstarConnectLink.GetGenderText(PatientGender.Unknown));
		}

		[TestMethod]
		public void FullFilePath_Rules() {
			foreach(string good in new[] { INFO,@"D:\info.json",@"\\server\share\Connect\info.json",@"C:/ProgramData/Alliedstar/Connect/info.json" }) {
				Assert.IsTrue(AlliedstarConnectLink.IsFullFilePath(good),good);
			}
			foreach(string bad in new[] { null,"","   ","info.json",@"Connect\info.json",@"C:\",@"C:info.json",@"\\",@"C:\data\info.json:stream",
				"C:\\data\\in|fo.json","C:\\data\\info?.json","C:\\data\\*.json",@"\info.json","https://example.com/info.json" })
			{
				Assert.IsFalse(AlliedstarConnectLink.IsFullFilePath(bad),bad??"null");
			}
		}

		[TestMethod]
		public void Note_DescribesWithoutPromotion() {
			string note=AlliedstarConnectLink.Note;
			StringAssert.Contains(note,"must already exist");
			StringAssert.Contains(note,"this program does not create them");
			StringAssert.Contains(note,"ask Alliedstar");
			foreach(string word in new[] { "http","www.","best","leading","partner","sign up","free trial","!" }) {
				Assert.IsFalse(note.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0,"The note says "+word);
			}
		}

		[TestMethod]
		public void EveryProblemHasAMessage() {
			foreach(BridgeLaunchProblem problem in Enum.GetValues(typeof(BridgeLaunchProblem)).Cast<BridgeLaunchProblem>().Where(x => x!=BridgeLaunchProblem.None)) {
				string message=BridgeLaunch.Describe(problem);
				Assert.IsFalse(string.IsNullOrWhiteSpace(message),problem.ToString());
				Assert.AreNotEqual(problem.ToString(),message,"A fixed text, not the enum name: "+problem);
			}
			List<string> listNew=new List<string> { BridgeLaunch.Describe(BridgeLaunchProblem.ProgramPathBlank),BridgeLaunch.Describe(BridgeLaunchProblem.InfoFilePathInvalid),
				BridgeLaunch.Describe(BridgeLaunchProblem.InfoFolderMissing) };
			Assert.IsTrue(listNew.All(x => !x.Contains("Alliedstar") && !x.Contains("OrthoComposite")),"The messages name no vendor.");
		}
	}
}
