// Vendor follow-up from public documentation (next/vendorpub-1): fork migration step 2026110801 gives the OrthoComposite rows the 26.2 integration
// inserted with blank Patient arguments OrthoComposite's public arguments once, and never changes an office's own value or note.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace P262LinksTests {
	[TestClass]
	public class OrthoCompositeArgumentsStepTests {
		private const string PROGRAM_COLUMNS="ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,IsDisabledByHq,CustErr";
		private const string PROPERTY_COLUMNS="ProgramPropertyNum,ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity";

		[TestMethod]
		public void Step_HasItsDatedLevel_AndPassesValidation() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			ForkMigrationStep step=listSteps.Single(x => x is OrthoCompositePatientArgumentsStep);
			Assert.AreEqual(2026110801,step.Level);
			Assert.AreEqual("Fill the blank OrthoComposite patient arguments with its public arguments",step.Name);
			ForkMigrationRunner.ValidateStep(step);
			ForkMigrationRunner.ValidateLevelsUnique(listSteps);
			Assert.IsTrue(step.GetType().Namespace.EndsWith(".FeatureSteps"),"A feature data step, not an Open Dental schema step.");
			Assert.IsTrue(step.Level>listSteps.Single(x => x is TrojanExpressCollectRemovedStep).Level,"After the 26.2 program link step.");
			string readme=TestPaths.ReadRepoFile("OpenPmsDentalBusiness","Misc","ForkMigrations","README.md");
			StringAssert.Contains(readme,"| 2026110801 | `OrthoCompositePatientArgumentsStep.cs`");
		}

		[TestMethod]
		public void Step_WritesTheLinksDefaultAndNote_AndKnowsTheOldNote() {
			Assert.AreEqual("OrthoComposite",OrthoCompositePatientArguments.PROG_NAME);
			Assert.AreEqual(ProgramName.OrthoComposite.ToString(),OrthoCompositePatientArguments.PROG_NAME);
			Assert.AreNotEqual(OrthoCompositeLink.Note,OrthoCompositePatientArguments.NoteBefore);
			StringAssert.StartsWith(OrthoCompositePatientArguments.NoteBefore,"Starts OrthoComposite for the selected patient. OrthoComposite finds the patient");
			StringAssert.Contains(OrthoCompositePatientArguments.NoteBefore,"Before the first use with a patient, enter the arguments from OrthoComposite's instructions");
			Assert.IsTrue(OrthoCompositePatientArguments.IsOrthoCompositeProgName("orthocomposite  "),"As MariaDB compares it.");
			Assert.IsFalse(OrthoCompositePatientArguments.IsOrthoCompositeProgName("OrthoCompositeX"));
			Assert.IsFalse(OrthoCompositePatientArguments.IsOrthoCompositeProgName(null));
		}

		[TestMethod]
		public void Step_NoRow_ChangesNothing() {
			ScratchDb.RequireDb();
			//Another link's blank "Patient arguments" row is not OrthoComposite's.
			DataCore.NonQ("INSERT INTO program ("+PROGRAM_COLUMNS+") VALUES (40,'AlliedstarConnect','Allied Star Connect',0,'','','"
				+POut.String(OrthoCompositePatientArguments.NoteBefore)+"','','','','',0,'')");
			DataCore.NonQ("INSERT INTO programproperty ("+PROPERTY_COLUMNS+") VALUES (400,40,'Patient arguments','','',0,0,0)");
			ForkMigrationContext context=new ForkMigrationContext(ScratchDb.DbName);
			new OrthoCompositePatientArgumentsStep().Apply(context);
			Assert.AreEqual(0,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
			Assert.AreEqual("",DataCore.GetScalar("SELECT PropertyValue FROM programproperty WHERE ProgramPropertyNum=400"));
			Assert.AreEqual(OrthoCompositePatientArguments.NoteBefore,DataCore.GetScalar("SELECT Note FROM program WHERE ProgramNum=40"));
		}

		[TestMethod]
		public void Step_FillsOnlyBlankValues_ReplacesOnlyTheOldNote_Idempotent() {
			ScratchDb.RequireDb();
			//Row 51: as p262/links-1 inserted it (blank value, the old note). Row 52: the office cleared the note and typed white space only. Row 53:
			//the office's own arguments and note. Each also has a blank clinic row, which the button does not read.
			DataCore.NonQ("INSERT INTO program ("+PROGRAM_COLUMNS+") VALUES "
				+"(51,'OrthoComposite','OrthoComposite',0,'','','"+POut.String(OrthoCompositePatientArguments.NoteBefore)+"','','','','',0,''),"
				+"(52,'orthocomposite ','Ortho imaging',1,'OrthoComposite.exe','','','','','','',0,''),"
				+"(53,'OrthoComposite','OrthoComposite',1,'','','Our own note','','','','',0,'')");
			DataCore.NonQ("INSERT INTO programproperty ("+PROPERTY_COLUMNS+") VALUES "
				+"(501,51,'Patient arguments','','',0,0,0),(502,51,'Patient arguments','','',7,0,0),"
				+"(511,52,'Patient arguments','  ','',0,0,0),"
				+"(521,53,'Patient arguments','/last [LName]','',0,0,0),(522,53,'Patient arguments','','',7,0,0)");
			DataCore.NonQ("UPDATE programproperty SET PropertyValue=NULL WHERE ProgramPropertyNum=502");
			ForkMigrationContext context=new ForkMigrationContext(ScratchDb.DbName);
			new OrthoCompositePatientArgumentsStep().Apply(context);
			CollectionAssert.AreEqual(new[] {
				"Set the blank Patient arguments of the OrthoComposite program link (ProgramNum 51, ProgramPropertyNum 501) to OrthoComposite's public arguments",
				"Replaced the built-in note of the OrthoComposite program link (ProgramNum 51)",
				"Set the blank Patient arguments of the OrthoComposite program link (ProgramNum 52, ProgramPropertyNum 511) to OrthoComposite's public arguments",
			},context.ListChanges,string.Join("\r\n",context.ListChanges));
			string defaultArguments="--lastname \"[LName]\" --firstname \"[FName]\" --dob \"[Birthdate]\"";
			Assert.AreEqual(defaultArguments,DataCore.GetScalar("SELECT PropertyValue FROM programproperty WHERE ProgramPropertyNum=501"));
			Assert.AreEqual(defaultArguments,DataCore.GetScalar("SELECT PropertyValue FROM programproperty WHERE ProgramPropertyNum=511"));
			Assert.AreEqual("/last [LName]",DataCore.GetScalar("SELECT PropertyValue FROM programproperty WHERE ProgramPropertyNum=521"),"The office's own value.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM programproperty WHERE ProgramPropertyNum IN (502,522) AND PropertyValue<>''"),
				"Clinic rows are not read by the button and stay.");
			Assert.AreEqual(OrthoCompositeLink.Note,DataCore.GetScalar("SELECT Note FROM program WHERE ProgramNum=51"));
			Assert.AreEqual("",DataCore.GetScalar("SELECT Note FROM program WHERE ProgramNum=52"),"A note the office cleared stays clear.");
			Assert.AreEqual("Our own note",DataCore.GetScalar("SELECT Note FROM program WHERE ProgramNum=53"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM programproperty WHERE ProgramNum IN (51,52,53) AND PropertyDesc<>'Patient arguments'"),
				"No row is added.");
			//The button now reads the default for row 51.
			ScratchDb.RefreshCaches();
			Assert.AreEqual(defaultArguments,ProgramProperties.GetValOrDefault(51,OrthoCompositeLink.PropertyDescs.PatientArguments,0));
			ForkMigrationContext contextAgain=new ForkMigrationContext(ScratchDb.DbName);
			new OrthoCompositePatientArgumentsStep().Apply(contextAgain);
			Assert.AreEqual(0,contextAgain.ListChanges.Count,"A second run changes nothing.");
			//Once the step ran, a value the office clears is its own choice: the step is not run again (its level is applied), and EnsureProgram
			//never refills an existing property.
			DataCore.NonQ("UPDATE programproperty SET PropertyValue='' WHERE ProgramPropertyNum=501");
			DataCore.NonQ("DELETE FROM program WHERE ProgramNum IN (52,53)");
			ScratchDb.RefreshCaches();
			Assert.IsFalse(OrthoCompositeLink.EnsureProgram());
			Assert.AreEqual("",DataCore.GetScalar("SELECT PropertyValue FROM programproperty WHERE ProgramPropertyNum=501"));
		}

		///<summary>A row without the property (removed by hand) gets it with the default from EnsureProgram, not from the step.</summary>
		[TestMethod]
		public void RowWithoutTheProperty_GetsItWithTheDefaultFromProgramLinks() {
			ScratchDb.RequireDb();
			DataCore.NonQ("INSERT INTO program ("+PROGRAM_COLUMNS+") VALUES (61,'OrthoComposite','OrthoComposite',0,'','','Kept','','','','',0,'')");
			ForkMigrationContext context=new ForkMigrationContext(ScratchDb.DbName);
			new OrthoCompositePatientArgumentsStep().Apply(context);
			Assert.AreEqual(0,context.ListChanges.Count);
			ScratchDb.RefreshCaches();
			Assert.IsTrue(OrthoCompositeLink.EnsureProgram(),"The missing property is added.");
			ScratchDb.RefreshCaches();
			Assert.AreEqual(OrthoCompositeLink.DefaultPatientArguments,ProgramProperties.GetValOrDefault(61,OrthoCompositeLink.PropertyDescs.PatientArguments,0));
			Assert.AreEqual("Kept",DataCore.GetScalar("SELECT Note FROM program WHERE ProgramNum=61"),"An existing row keeps its settings.");
		}
	}
}
