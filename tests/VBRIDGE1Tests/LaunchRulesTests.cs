// The launch rules of the V-BRIDGE1 program links, without a database: the address the Overjet and Insurance Answers Plus buttons open and the
// command line the DTX Studio buttons start. Nothing is started; synthetic patients only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect.ProgramLinks;

namespace VBRIDGE1Tests {
	[TestClass]
	public class LaunchRulesTests {
		private const string DTX_PATH=@"C:\Program Files\DTX Studio Clinic\DTXsync.exe";

		private static Patient MakePatient() {
			Patient patient=new Patient();
			patient.PatNum=42;
			patient.ChartNumber="CH 7";
			patient.LName="Smith & Co";
			patient.FName="Ann";
			patient.Birthdate=new DateTime(1990,2,3);
			patient.Gender=PatientGender.Female;
			return patient;
		}

		private static bool FileExistsAlways(string path) {
			return true;
		}

		///<summary>An https address on a host this program never contacts, built from the one blocked-host list (CodeBase.VendorDirect.HqHosts).</summary>
		private static string BlockedUrl(string subdomain,string pathAndQuery) {
			return "https://"+subdomain+HqHosts.BlockedDomains[0]+pathAndQuery;
		}

		#region Overjet
		[TestMethod]
		public void Overjet_NoPatient_OpensThePathAsItIs() {
			BridgeLaunch bridgeLaunch=OverjetLink.GetLaunch(OverjetLink.DefaultPath,"pid=[PatientID]","0",null);
			Assert.IsTrue(bridgeLaunch.IsReady);
			Assert.AreEqual(OverjetLink.DefaultPath,bridgeLaunch.FileName);
			Assert.AreEqual("",bridgeLaunch.Arguments);
			Assert.IsFalse(bridgeLaunch.HasPatientData);
		}

		[TestMethod]
		public void Overjet_BlankParameters_SendNoPatientData() {
			BridgeLaunch bridgeLaunch=OverjetLink.GetLaunch(OverjetLink.DefaultPath,"  ","0",MakePatient());
			Assert.IsTrue(bridgeLaunch.IsReady);
			Assert.AreEqual(OverjetLink.DefaultPath,bridgeLaunch.FileName,"Partner-gated: no parameter names until the office enters them.");
			Assert.IsFalse(bridgeLaunch.HasPatientData);
		}

		[TestMethod]
		public void Overjet_Parameters_AreFilledAndPercentEncoded() {
			BridgeLaunch bridgeLaunch=OverjetLink.GetLaunch(OverjetLink.DefaultPath,"?pid=[PatientID]&last=[LName]&first=[FName]&dob=[Birthdate]&sex=[Gender]",
				"0",MakePatient());
			Assert.IsTrue(bridgeLaunch.IsReady);
			Assert.AreEqual(OverjetLink.DefaultPath+"?pid=42&last=Smith%20%26%20Co&first=Ann&dob=1990-02-03&sex=F",bridgeLaunch.FileName);
			Assert.IsTrue(bridgeLaunch.HasPatientData);
		}

		[TestMethod]
		public void Overjet_ChartNumberProperty_SelectsTheChartNumber() {
			BridgeLaunch bridgeLaunch=OverjetLink.GetLaunch(OverjetLink.DefaultPath,"pid=[PatientID]&num=[PatNum]","1",MakePatient());
			Assert.AreEqual(OverjetLink.DefaultPath+"?pid=CH%207&num=42",bridgeLaunch.FileName);
		}

		[TestMethod]
		public void Overjet_PathWithQuery_AddsWithAmpersand() {
			Assert.AreEqual("https://app.example.test/x?a=1&pid=42",
				OverjetLink.GetLaunch("https://app.example.test/x?a=1","&pid=[PatNum]","0",MakePatient()).FileName);
			Assert.AreEqual("https://app.example.test/x?pid=42",
				OverjetLink.GetLaunch("https://app.example.test/x?","pid=[PatNum]","0",MakePatient()).FileName);
		}

		[TestMethod]
		public void Overjet_NoBirthdate_SendsBlank() {
			Patient patient=MakePatient();
			patient.Birthdate=DateTime.MinValue;
			Assert.AreEqual(OverjetLink.DefaultPath+"?dob=",OverjetLink.GetLaunch(OverjetLink.DefaultPath,"dob=[Birthdate]","0",patient).FileName);
		}

		[TestMethod]
		public void Overjet_PathMustBeHttpsAndAllowed() {
			Assert.AreEqual(BridgeLaunchProblem.PathBlank,OverjetLink.GetLaunch("","","0",null).Problem);
			Assert.AreEqual(BridgeLaunchProblem.PathNotHttps,OverjetLink.GetLaunch("http://clinic.example.test/app","","0",null).Problem);
			Assert.AreEqual(BridgeLaunchProblem.PathNotHttps,OverjetLink.GetLaunch("http://localhost/app","","0",null).Problem,
				"Plain http is refused even for this computer.");
			Assert.AreEqual(BridgeLaunchProblem.PathNotHttps,OverjetLink.GetLaunch(@"C:\Overjet\app.exe","","0",null).Problem);
			Assert.AreEqual(BridgeLaunchProblem.PathBlocked,OverjetLink.GetLaunch(BlockedUrl("www.","/x"),"","0",null).Problem);
			Assert.AreEqual(BridgeLaunchProblem.PathBlocked,OverjetLink.GetLaunch(BlockedUrl("help.","/x"),"pid=[PatNum]","0",MakePatient()).Problem);
		}
		#endregion Overjet

		#region DTX Studio
		[TestMethod]
		public void Dtx_NoPatient_IsRefusedFirst() {
			BridgeLaunch bridgeLaunch=DtxStudioLink.GetLaunch("","","","","0",null,FileExistsAlways);
			Assert.AreEqual(BridgeLaunchProblem.NoPatient,bridgeLaunch.Problem);
			Assert.AreEqual("Please select a patient first.",BridgeLaunch.Describe(bridgeLaunch.Problem));
		}

		[TestMethod]
		public void Dtx_BlankOrMissingProgram_IsRefusedWithThePath() {
			Assert.AreEqual(BridgeLaunchProblem.PathBlank,DtxStudioLink.GetLaunch(" ","[PatNum]","","","0",MakePatient(),FileExistsAlways).Problem);
			BridgeLaunch bridgeLaunch=DtxStudioLink.GetLaunch(DTX_PATH,"[PatNum]","","","0",MakePatient(),x => false);
			Assert.AreEqual(BridgeLaunchProblem.ProgramNotFound,bridgeLaunch.Problem);
			Assert.AreEqual(DTX_PATH,bridgeLaunch.FileName,"The message shows the path.");
		}

		[TestMethod]
		public void Dtx_BlankPatientArguments_IsPartnerGated() {
			BridgeLaunch bridgeLaunch=DtxStudioLink.GetLaunch(DTX_PATH,"  ","-x","","0",MakePatient(),FileExistsAlways);
			Assert.AreEqual(BridgeLaunchProblem.PatientArgumentsNotSet,bridgeLaunch.Problem);
			StringAssert.Contains(BridgeLaunch.Describe(bridgeLaunch.Problem),"integration guide");
		}

		[TestMethod]
		public void Dtx_Arguments_AreFilledWithDefaultBirthdateFormat() {
			BridgeLaunch bridgeLaunch=DtxStudioLink.GetLaunch(DTX_PATH,"/id [PatientID] /ln \"[LName]\" /fn \"[FName]\" /dob [Birthdate] /sex [Gender]",
				"","","0",MakePatient(),FileExistsAlways);
			Assert.IsTrue(bridgeLaunch.IsReady);
			Assert.AreEqual(DTX_PATH,bridgeLaunch.FileName);
			Assert.AreEqual("/id 42 /ln \"Smith & Co\" /fn \"Ann\" /dob 02/03/1990 /sex F",bridgeLaunch.Arguments);
			Assert.IsTrue(bridgeLaunch.HasPatientData);
		}

		[TestMethod]
		public void Dtx_BirthdateFormatProperty_IsUsedWithInvariantSeparators() {
			BridgeLaunch bridgeLaunch=DtxStudioLink.GetLaunch(DTX_PATH,"[Birthdate]","","dd/MM/yyyy","0",MakePatient(),FileExistsAlways);
			Assert.AreEqual("03/02/1990",bridgeLaunch.Arguments);
		}

		[TestMethod]
		public void Dtx_UnusableBirthdateFormat_IsRefused() {
			Assert.AreEqual(BridgeLaunchProblem.BirthdateFormatInvalid,DtxStudioLink.GetLaunch(DTX_PATH,"[Birthdate]","","%","0",MakePatient(),FileExistsAlways).Problem);
			Assert.AreEqual(BridgeLaunchProblem.BirthdateFormatInvalid,DtxStudioLink.GetLaunch(DTX_PATH,"[Birthdate]","","abc","0",MakePatient(),FileExistsAlways).Problem,
				"A format without any date field would send the same text for every patient.");
			Assert.IsTrue(DtxStudioLink.GetLaunch(DTX_PATH,"[PatNum]","","%","0",MakePatient(),FileExistsAlways).IsReady,
				"The format only matters when the arguments use [Birthdate].");
		}

		[TestMethod]
		public void Dtx_ValuesLoseQuotesAndControlCharacters() {
			Patient patient=MakePatient();
			patient.LName="O\"Neil\r\n";
			patient.FName="\tBo";
			BridgeLaunch bridgeLaunch=DtxStudioLink.GetLaunch(DTX_PATH,"\"[LName]\" \"[FName]\"","","","0",patient,FileExistsAlways);
			Assert.AreEqual("\"ONeil\" \"Bo\"",bridgeLaunch.Arguments);
		}

		[TestMethod]
		public void Dtx_CommandLine_IsAppendedAfterThePatientArguments() {
			BridgeLaunch bridgeLaunch=DtxStudioLink.GetLaunch(DTX_PATH,"[PatientID]"," /extra 1 ","","1",MakePatient(),FileExistsAlways);
			Assert.AreEqual("CH 7 /extra 1",bridgeLaunch.Arguments);
		}

		[TestMethod]
		public void Dtx_GenderCodes() {
			Assert.AreEqual("M",BridgePatientTokens.GetGenderCode(PatientGender.Male));
			Assert.AreEqual("F",BridgePatientTokens.GetGenderCode(PatientGender.Female));
			Assert.AreEqual("O",BridgePatientTokens.GetGenderCode(PatientGender.Other));
			Assert.AreEqual("U",BridgePatientTokens.GetGenderCode(PatientGender.Unknown));
		}
		#endregion DTX Studio

		#region Insurance Answers Plus
		[TestMethod]
		public void Iap_BlankOrDesktopPath_AsksForTheLoginAddress() {
			Assert.AreEqual(BridgeLaunchProblem.PathBlank,IapLink.GetLaunch("").Problem);
			Assert.AreEqual(BridgeLaunchProblem.PathBlank,IapLink.GetLaunch(@"C:\IAPlus\").Problem);
			Assert.AreEqual(BridgeLaunchProblem.PathBlank,IapLink.GetLaunch((Program)null).Problem);
			Assert.AreEqual("Set the IAP login address in Program Links.",IapLink.Describe(BridgeLaunchProblem.PathBlank));
		}

		[TestMethod]
		public void Iap_OnlyHttpsAddressesOpen_AndNothingIsAdded() {
			Assert.AreEqual(BridgeLaunchProblem.PathNotHttps,IapLink.GetLaunch("http://iap.example.test/login").Problem);
			Assert.AreEqual(BridgeLaunchProblem.PathBlocked,IapLink.GetLaunch(BlockedUrl("","/iap")).Problem);
			BridgeLaunch bridgeLaunch=IapLink.GetLaunch(" https://iap.example.test/login ");
			Assert.IsTrue(bridgeLaunch.IsReady);
			Assert.AreEqual("https://iap.example.test/login",bridgeLaunch.FileName);
			Assert.AreEqual("",bridgeLaunch.Arguments);
			Assert.IsFalse(bridgeLaunch.HasPatientData);
		}

		[TestMethod]
		public void Iap_LegacyDesktopPath_IsRecognized() {
			Assert.IsTrue(IapLink.IsLegacyDesktopPath(@"C:\IAPlus\"));
			Assert.IsTrue(IapLink.IsLegacyDesktopPath(@"\\server\iap"));
			Assert.IsTrue(IapLink.IsLegacyDesktopPath("iaplus"));
			Assert.IsFalse(IapLink.IsLegacyDesktopPath(""));
			Assert.IsFalse(IapLink.IsLegacyDesktopPath("https://iap.example.test/login"));
			Assert.IsFalse(IapLink.IsLegacyDesktopPath("http://iap.example.test/login"),"An office's own web address is never cleared.");
		}
		#endregion Insurance Answers Plus

		#region Shared
		[TestMethod]
		public void Tokens_AreReplacedInOnePass() {
			Patient patient=MakePatient();
			patient.LName="[FName]";
			Assert.AreEqual("[FName]-Ann",BridgePatientTokens.Replace("[LName]-[FName]",patient,false,"MM/dd/yyyy",BridgeTokenEncoding.CommandLine));
			Assert.AreEqual("[Unknown]",BridgePatientTokens.Replace("[Unknown]",patient,false,"MM/dd/yyyy",BridgeTokenEncoding.CommandLine));
			Assert.IsFalse(BridgePatientTokens.HasAnyToken("static=1"));
			Assert.IsTrue(BridgePatientTokens.ArrayAll.All(x => BridgePatientTokens.HasAnyToken("a"+x)));
		}

		[TestMethod]
		public void PathSaveRule_AppliesToTheWebLinksOnly() {
			Assert.AreEqual("",BridgeLinkRows.GetPathSaveProblem("Overjet",OverjetLink.DefaultPath));
			Assert.AreEqual("",BridgeLinkRows.GetPathSaveProblem("Overjet",""));
			Assert.AreEqual("",BridgeLinkRows.GetPathSaveProblem("IAP","https://iap.example.test/login"));
			Assert.AreNotEqual("",BridgeLinkRows.GetPathSaveProblem("Overjet","http://clinic.example.test"));
			Assert.AreNotEqual("",BridgeLinkRows.GetPathSaveProblem("IAP",@"C:\IAPlus\"));
			Assert.AreEqual(BridgeLaunch.Describe(BridgeLaunchProblem.PathBlocked),BridgeLinkRows.GetPathSaveProblem("IAP",BlockedUrl("www.","")));
			Assert.AreEqual("",BridgeLinkRows.GetPathSaveProblem("DTXStudio",DTX_PATH));
			Assert.AreEqual("",BridgeLinkRows.GetPathSaveProblem("Dexis","http://anything"),"Other program links are not checked here.");
		}

		[TestMethod]
		public void EveryProblem_HasAMessage() {
			foreach(BridgeLaunchProblem problem in Enum.GetValues(typeof(BridgeLaunchProblem))) {
				string message=BridgeLaunch.Describe(problem);
				if(problem==BridgeLaunchProblem.None) {
					Assert.AreEqual("",message);
					continue;
				}
				Assert.AreNotEqual(problem.ToString(),message,problem.ToString());
				Assert.IsFalse(message.ToLowerInvariant().Contains("open dental"),problem.ToString());
			}
		}
		#endregion Shared
	}
}
