// OrthoComposite (Open Dental 26.2 item 45): the row definition from the public setup page, the public arguments from OrthoComposite's Microsoft
// Store listing (next/vendorpub-1), the order of the launch checks and the three tokens. No database, nothing is launched.
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
			Assert.AreEqual(OrthoCompositeLink.DefaultPatientArguments,definition.ListProperties[0].Value,"Public since next/vendorpub-1.");
			Assert.AreEqual(OrthoCompositeLink.Note,definition.Note);
		}

		///<summary>OrthoComposite's Microsoft Store listing: the command arguments --lastname, --firstname and --dob open its patient list with the search
		///filled in. Open Dental's page: the birthdate is MM/DD/YYYY.</summary>
		[TestMethod]
		public void DefaultArguments_AreTheStoreListingArguments() {
			Assert.AreEqual("--lastname \"[LName]\" --firstname \"[FName]\" --dob \"[Birthdate]\"",OrthoCompositeLink.DefaultPatientArguments);
			BridgeLaunch launch=Launch(PATH,OrthoCompositeLink.DefaultPatientArguments,"",SyntheticPatients.Create());
			Assert.IsTrue(launch.IsReady);
			Assert.IsTrue(launch.HasPatientData);
			Assert.AreEqual("--lastname \"N\u00fa\u00f1ez Nico\" --firstname \"Zo\u00e9\" --dob \"03/09/1980\"",launch.Arguments,
				"Each value quoted, double quotes removed from it, accented letters kept.");
			CultureInfo cultureOld=Thread.CurrentThread.CurrentCulture;
			try {
				Thread.CurrentThread.CurrentCulture=new CultureInfo("de-DE");
				StringAssert.EndsWith(Launch(PATH,OrthoCompositeLink.DefaultPatientArguments,"",SyntheticPatients.Create()).Arguments,"--dob \"03/09/1980\"",
					"MM/dd/yyyy with \"/\" whatever the regional settings.");
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureOld;
			}
			Patient patient=SyntheticPatients.Create();
			patient.Birthdate=DateTime.MinValue;
			StringAssert.EndsWith(Launch(PATH,OrthoCompositeLink.DefaultPatientArguments,"--fullscreen",patient).Arguments,"--dob \"\" --fullscreen",
				"No birthdate: an empty value; the Command line follows.");
		}

		///<summary>In a quoted token, a value ending in a backslash would escape the closing quote ("Smith\" reads as Smith" and the rest).</summary>
		[TestMethod]
		public void Values_DoNotEndInABackslash() {
			Patient patient=SyntheticPatients.Create();
			patient.LName="Smith\\";
			patient.FName="Ann \\ \\";
			Assert.AreEqual("--lastname \"Smith\" --firstname \"Ann\"",OrthoCompositeLink.FillArguments("--lastname \"[LName]\" --firstname \"[FName]\"",patient));
			patient.LName="O\\Neil";
			Assert.AreEqual("\"O\\Neil\"",OrthoCompositeLink.FillArguments("\"[LName]\"",patient),"A backslash inside a value is kept.");
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
		public void Launch_NameWithoutAFolderOrShellTargetIsPassedToWindowsUnchecked() {
			//A name without a folder or a shell: target is not a file this program can check. The names are made up: neither public source names
			//OrthoComposite's executable or an app execution alias.
			foreach(string path in new[] { "ExampleImaging.exe",@"shell:AppsFolder\Example.Imaging_abc!App" }) {
				BridgeLaunch launch=Launch(path,"[FName]","",SyntheticPatients.Create(),fileExists:false);
				Assert.IsTrue(launch.IsReady,path);
				Assert.AreEqual(path,launch.FileName);
			}
			Assert.IsTrue(OrthoCompositeLink.IsFileSystemPath(@"\\server\apps\OrthoComposite.exe"));
			Assert.IsTrue(OrthoCompositeLink.IsFileSystemPath(PATH));
			Assert.IsFalse(OrthoCompositeLink.IsFileSystemPath("ExampleImaging.exe"));
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
			foreach(string argument in new[] { "--lastname","--firstname","--dob" }) {
				StringAssert.Contains(note,argument);
			}
			foreach(string word in new[] { "http","www.","Microsoft Store","best","leading","partner","sign up","free trial","!" }) {
				Assert.IsFalse(note.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0,"The note says "+word);
			}
		}

		///<summary>Nothing invented: neither OrthoComposite's Store listing nor Open Dental's page names an executable or an app execution alias, so the
		///Note (written into new rows, and over the old built-in Note by step 2026110801) suggests no program name, only what the path check does.
		///</summary>
		[TestMethod]
		public void Note_SuggestsNoProgramName() {
			string note=OrthoCompositeLink.Note;
			StringAssert.Contains(note,"Enter where OrthoComposite is in Path of file to open; a name without a folder, or a shell: target, is passed "
				+"to Windows unchecked.");
			foreach(string word in new[] { ".exe","alias","installed as an app","for example" }) {
				Assert.IsFalse(note.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0,"The note says "+word);
			}
		}
	}
}
