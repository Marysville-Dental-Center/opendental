// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdoptionValidation.Tests {
	///<summary>The model of an Open Dental client's startup version checks (ClassConvertDatabase.Convert and PrefL.CheckProgramVersion in the 24.3.63
	///source). No database.</summary>
	[TestClass]
	public class OdStartupCheckTests {
		private static OdDatabaseState Adopted() {
			return new OdDatabaseState { DataBaseVersion="99.0.0.0",ProgramVersion="99.0.0.0" };
		}

		private static OdDatabaseState OfficeCopy(long countSegments=3) {
			return new OdDatabaseState { DataBaseVersion="25.3.48.0",ProgramVersion="25.3.48.0",CountUpdateSegmentsJoined=countSegments,
				CountUpdateFileRows=countSegments+1,UpdateWebsitePath="https://updates.invalid/" };
		}

		private static OdClient Client(string version,bool isUpdateServer=true) {
			return new OdClient { Version=new Version(version),IsUpdateServer=isUpdateServer };
		}

		[TestMethod]
		public void AdoptedDatabase_EveryReleaseClientExitsWithoutTouchingIt() {
			foreach((string name,OdClient client,bool isClassicView) scenario in OdStartupCheck.StandardScenarios().Where(x => !x.client.IsDebugBuild)) {
				OdDatabaseState state=Adopted();
				state.IsClassicUpdateView=scenario.isClassicView;
				OdStartupResult result=OdStartupCheck.Evaluate(scenario.client,state);
				Assert.IsTrue(result.IsRefused,scenario.name+": "+result.Describe()+"\r\n"+string.Join("\r\n",result.ListTrace));
				Assert.IsFalse(result.RunsConversion,scenario.name);
				Assert.IsFalse(result.WritesDatabase,scenario.name);
				Assert.IsFalse(result.InstallsServices,scenario.name);
				Assert.IsFalse(result.CopiesBinariesFromDatabase,scenario.name);
			}
		}

		[TestMethod]
		public void AdoptedDatabase_OfficeClientSkipsConversionAndFindsNoUpdateFiles() {
			OdStartupResult result=OdStartupCheck.Evaluate(Client("25.3.48.0"),Adopted());
			Assert.AreEqual(OdStartupOutcome.Exits,result.Outcome);
			Assert.AreEqual(312,result.ExitCode);
			Assert.IsFalse(result.TriesSetup,"UpdateWebsitePath is blank after adoption, so no Setup.exe is downloaded.");
			Assert.IsTrue(result.ListTrace.Any(x => x.Contains("conversion is skipped")));
			Assert.IsTrue(result.ListTrace.Any(x => x.Contains("no UpdateFilesSegment rows")));
		}

		[TestMethod]
		public void UnadoptedOfficeCopy_SameVersionProceeds_NewerUpdateServerConvertsAndInstallsServices() {
			Assert.AreEqual(OdStartupOutcome.Proceeds,OdStartupCheck.Evaluate(Client("25.3.48.0"),OfficeCopy()).Outcome);
			OdStartupResult result=OdStartupCheck.Evaluate(Client("25.4.30.0"),OfficeCopy());
			Assert.AreEqual(OdStartupOutcome.PublishesUpdateAndProceeds,result.Outcome);
			Assert.IsTrue(result.RunsConversion && result.WritesDatabase && result.InstallsServices);
			Assert.IsFalse(result.IsRefused);
		}

		[TestMethod]
		public void UnadoptedOfficeCopy_OlderClientCopiesOpenDentalBinariesFromTheDatabase() {
			//Risk R1: a fork build numbered 24.3.x on the office database would replace itself with Open Dental's binaries.
			OdStartupResult result=OdStartupCheck.Evaluate(Client("24.3.63.0"),OfficeCopy());
			Assert.IsTrue(result.CopiesBinariesFromDatabase);
			Assert.IsFalse(result.IsRefused);
		}

		[TestMethod]
		public void SentinelWithUpdateFilesLeft_IsNotARefusal() {
			//Why adoption deletes the update files: with the sentinel alone, a client would still unpack whatever the rows hold.
			OdDatabaseState state=Adopted();
			state.CountUpdateSegmentsJoined=2;
			OdStartupResult result=OdStartupCheck.Evaluate(Client("25.3.48.0"),state);
			Assert.IsTrue(result.CopiesBinariesFromDatabase);
			Assert.IsFalse(result.IsRefused);
		}

		[TestMethod]
		public void UpdateAddressLeft_ClientTriesToDownloadSetupButDoesNotTouchTheDatabase() {
			OdDatabaseState state=Adopted();
			state.UpdateWebsitePath="https://updates.invalid/";
			OdStartupResult result=OdStartupCheck.Evaluate(Client("25.3.48.0"),state);
			Assert.IsTrue(result.TriesSetup);
			Assert.IsTrue(result.IsRefused);
		}

		[TestMethod]
		public void ClassicView_LaunchesSetupFromAtoZ_AndExits() {
			OdDatabaseState state=Adopted();
			state.IsClassicUpdateView=true;
			OdStartupResult result=OdStartupCheck.Evaluate(new OdClient { Version=new Version(25,3,48,0),HasSetupInAtoZ=true },state);
			Assert.AreEqual(OdStartupOutcome.Exits,result.Outcome);
			Assert.IsTrue(result.TriesSetup);
			Assert.IsFalse(result.WritesDatabase);
		}

		[TestMethod]
		public void CorruptedDatabase_Exit201() {
			OdDatabaseState state=OfficeCopy();
			state.IsCorrupted=true;
			OdStartupResult result=OdStartupCheck.Evaluate(Client("25.3.48.0"),state);
			Assert.AreEqual(201,result.ExitCode);
			Assert.IsTrue(result.IsRefused);
		}

		[TestMethod]
		public void DebugBuild_SkipsTheVersionCheck_DocumentedLimit() {
			//Open Dental's Debug builds skip CheckProgramVersion. They are not shipped; the rehearsal reports this case separately.
			OdStartupResult result=OdStartupCheck.Evaluate(new OdClient { Version=new Version(25,3,48,0),IsDebugBuild=true },Adopted());
			Assert.AreEqual(OdStartupOutcome.Proceeds,result.Outcome);
			Assert.IsFalse(result.RunsConversion);
		}

		[TestMethod]
		public void UnreadableVersion_Exits() {
			OdStartupResult result=OdStartupCheck.Evaluate(Client("25.3.48.0"),new OdDatabaseState { DataBaseVersion="",ProgramVersion="x" });
			Assert.AreEqual(OdStartupOutcome.Exits,result.Outcome);
		}

		[TestMethod]
		public void StandardScenarios_CoverTheOfficeVersionAndLaterReleases() {
			Assert.IsTrue(OdStartupCheck.StandardScenarios().Any(x => x.client.Version==new Version(25,3,48,0) && !x.client.IsUpdateServer));
			Assert.IsTrue(OdStartupCheck.StandardScenarios().Any(x => x.client.Version.Major==26));
			Assert.AreEqual(1,OdStartupCheck.StandardScenarios().Count(x => x.client.IsDebugBuild));
		}
	}
}
