// OrthoComposite (Open Dental 26.2 item 45): the row definition from the public setup page, the order of the launch checks and the three tokens.
// No database, nothing is launched.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace P262LinksTests {
	[TestClass]
	public class OrthoCompositeLinkTests {
		private const string PATH=@"C:\Programs\OrthoComposite\OrthoComposite.exe";

		private static BridgeLaunch Launch(string path,string arguments,string commandLine,Patient patient,bool fileExists=true) {
			return OrthoCompositeLink.GetLaunch(path,arguments,commandLine,patient,x => fileExists);
		}

		[TestMethod]
		public void Definition_HasThePublicPageDefaults() {
			BridgeLinkDefinition definition=OrthoCompositeLink.GetDefinition();
			Assert.AreEqual(ProgramName.OrthoComposite,definition.ProgName);
			Assert.AreEqual("OrthoComposite",definition.ProgDesc);
			Assert.AreEqual("",definition.Path,"The public page gives no default path (a Microsoft Store program).");
			CollectionAssert.AreEqual(new[] { EnumToolBar.ChartModule },definition.ListToolBars);
			Assert.AreEqual("OrthoComposite",definition.ButtonText,"The public page's Text on button default.");
			Assert.AreEqual(1,definition.ListProperties.Count);
			Assert.AreEqual("Patient arguments",definition.ListProperties[0].Key);
			Assert.AreEqual("",definition.ListProperties[0].Value,"Partner-gated: the office enters it from OrthoComposite's instructions.");
		}

		[TestMethod]
		public void Launch_ChecksInOrder() {
			Patient patient=SyntheticPatients.Create();
			Assert.AreEqual(BridgeLaunchProblem.ProgramPathBlank,Launch("",null,"",patient).Problem);
			Assert.AreEqual(BridgeLaunchProblem.ProgramNotFound,Launch(PATH,"",null,patient,fileExists:false).Problem);
			Assert.AreEqual(PATH,Launch(PATH,"","",patient,fileExists:false).FileName);
			Assert.AreEqual(BridgeLaunchProblem.PatientArgumentsNotSet,Launch(PATH,"  ","",patient).Problem);
			BridgeLaunch ready=Launch(PATH,"/last \"[LName]\" /first \"[FName]\" /dob [Birthdate]","",patient);
			Assert.IsTrue(ready.IsReady);
			Assert.AreEqual(PATH,ready.FileName);
			Assert.AreEqual("/last \"N\u00fa\u00f1ez Nico\" /first \"Zo\u00e9\" /dob 03/09/1980",ready.Arguments,"Accented letters are kept; double quotes in values removed.");
			Assert.IsTrue(ready.HasPatientData);
		}

		[TestMethod]
		public void Launch_WithoutAPatient_StartsWithTheCommandLineOnly() {
			BridgeLaunch launch=Launch(PATH,"","  --fullscreen ",null);
			Assert.IsTrue(launch.IsReady,"No patient arguments are needed without a patient.");
			Assert.AreEqual("--fullscreen",launch.Arguments);
			Assert.IsFalse(launch.HasPatientData);
			Assert.AreEqual("",Launch(PATH,"[LName]",null,null).Arguments);
		}

		[TestMethod]
		public void Launch_CommandLineFollowsThePatientArguments() {
			Assert.AreEqual("Zo\u00e9 --fullscreen",Launch(PATH,"[FName]","--fullscreen",SyntheticPatients.Create()).Arguments);
		}

		[TestMethod]
		public void Launch_AliasOrShellTargetIsLeftToWindows() {
			//An app execution alias (what a Microsoft Store program installs) or a shell: target is not a file this program can check.
			foreach(string path in new[] { "OrthoComposite.exe",@"shell:AppsFolder\Example.OrthoComposite_abc!App" }) {
				BridgeLaunch launch=Launch(path,"[FName]","",SyntheticPatients.Create(),fileExists:false);
				Assert.IsTrue(launch.IsReady,path);
				Assert.AreEqual(path,launch.FileName);
			}
			Assert.IsTrue(OrthoCompositeLink.IsFileSystemPath(@"\\server\apps\OrthoComposite.exe"));
			Assert.IsTrue(OrthoCompositeLink.IsFileSystemPath(PATH));
			Assert.IsFalse(OrthoCompositeLink.IsFileSystemPath("OrthoComposite.exe"));
			Assert.IsFalse(OrthoCompositeLink.IsFileSystemPath(@"shell:AppsFolder\X!App"));
		}

		[TestMethod]
		public void Tokens_OnlyNameAndBirthdateAreFilledIn() {
			Patient patient=SyntheticPatients.Create();
			string filled=OrthoCompositeLink.FillArguments("[PatNum] [ChartNumber] [PatientID] [Gender] [LName] [FName] [Birthdate] [fname]",patient);
			Assert.AreEqual("[PatNum] [ChartNumber] [PatientID] [Gender] N\u00fa\u00f1ez Nico Zo\u00e9 03/09/1980 [fname]",filled,
				"Other bridges' tokens are sent as typed, so no other patient data can reach OrthoComposite; tokens are case sensitive.");
			Assert.IsFalse(filled.Contains("1042") || filled.Contains("CH-77"));
			CollectionAssert.AreEqual(new[] { "[FName]","[LName]","[Birthdate]" },OrthoCompositeLink.ArrayTokens);
			Assert.IsTrue(OrthoCompositeLink.HasAnyToken("x [Birthdate]"));
			Assert.IsFalse(OrthoCompositeLink.HasAnyToken("[PatNum]"));
			Assert.IsFalse(Launch(PATH,"--open","",patient).HasPatientData,"A template without tokens sends no patient data.");
		}

		[TestMethod]
		public void Tokens_OnePass() {
			Patient patient=SyntheticPatients.Create();
			patient.FName="[LName]";
			Assert.AreEqual("[LName] N\u00fa\u00f1ez Nico",OrthoCompositeLink.FillArguments("[FName] [LName]",patient),"A value that looks like a token is not replaced again.");
		}

		[TestMethod]
		public void Birthdate_IsMMddyyyy_WhateverTheRegionalSettings_BlankWithoutOne() {
			CultureInfo cultureOld=Thread.CurrentThread.CurrentCulture;
			try {
				Thread.CurrentThread.CurrentCulture=new CultureInfo("de-DE");
				Assert.AreEqual("03/09/1980",OrthoCompositeLink.FillArguments("[Birthdate]",SyntheticPatients.Create()));
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureOld;
			}
			Patient patient=SyntheticPatients.Create();
			patient.Birthdate=DateTime.MinValue;
			Assert.AreEqual("\"\"",OrthoCompositeLink.FillArguments("\"[Birthdate]\"",patient));
		}

		[TestMethod]
		public void ControlCharactersAreRemoved() {
			Patient patient=SyntheticPatients.Create();
			patient.LName="Smith\r\n--evil\t";
			Assert.AreEqual("Smith--evil",OrthoCompositeLink.FillArguments("[LName]",patient));
		}

		[TestMethod]
		public void Note_DescribesWithoutPromotion() {
			string note=OrthoCompositeLink.Note;
			foreach(string token in OrthoCompositeLink.ArrayTokens) {
				StringAssert.Contains(note,token);
			}
			StringAssert.Contains(note,"No other patient data is sent.");
			foreach(string word in new[] { "http","www.","Microsoft Store","best","leading","partner","sign up","free trial","!" }) {
				Assert.IsFalse(note.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0,"The note says "+word);
			}
		}
	}
}
