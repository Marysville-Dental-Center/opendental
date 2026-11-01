// v25_2-bridges-dxc-credit-score-removed: the DentalXChange Patient Credit Score bridge is gone and its program row is disabled.
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.FeatureSteps;

namespace BSHELLTests {
	[TestClass]
	public class DxcCreditScoreRemovedTests {
		[TestMethod]
		public void BridgeFile_IsGone_AndNothingCallsIt() {
			Assert.IsFalse(File.Exists(TestPaths.GetRepoFile("WpfControlsOD","Bridges","DentalXChange.cs")));
			string csproj=TestPaths.ReadRepoFile("WpfControlsOD","WpfControlsOD.csproj");
			Assert.IsFalse(csproj.Contains("Bridges\\DentalXChange.cs"));
			string programL=TestPaths.ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs");
			Assert.IsFalse(programL.Contains("DentalXChange.SendData"),"The dispatch branch is removed.");
			Assert.IsFalse(programL.Contains("ProgramName.DXCPatientCreditScore"),"No branch for the program name is left.");
			StringAssert.Contains(programL,"if(ProgramLinksDiscontinued.IsDiscontinued(program)) {");
			//The discontinued check comes before any dispatch, so an old button cannot reach the generic launcher either.
			Assert.IsTrue(programL.IndexOf("ProgramLinksDiscontinued.IsDiscontinued(program)")<programL.IndexOf("program.PluginDllName!=\"\""));
		}

		[TestMethod]
		public void ProgramName_StaysForMigratedRows() {
			Assert.IsTrue(Enum.IsDefined(typeof(ProgramName),"DXCPatientCreditScore"));
			Assert.IsTrue(ProgramLinksDiscontinued.IsDiscontinued("DXCPatientCreditScore"));
			Assert.IsTrue(ProgramLinksDiscontinued.IsDiscontinued(new Program { ProgName="DXCPatientCreditScore" }));
			Assert.IsFalse(ProgramLinksDiscontinued.IsDiscontinued("Dolphin"));
			Assert.IsFalse(ProgramLinksDiscontinued.IsDiscontinued(""));
			Assert.IsFalse(ProgramLinksDiscontinued.IsDiscontinued((Program)null));
		}

		[TestMethod]
		public void Label_AddedOnce() {
			Assert.AreEqual("DXC Patient Credit Score (discontinued)",ProgramLinksDiscontinued.GetLabeledDesc("DXC Patient Credit Score"));
			Assert.AreEqual("DXC Patient Credit Score (discontinued)",ProgramLinksDiscontinued.GetLabeledDesc("DXC Patient Credit Score (discontinued)"));
			Assert.AreEqual("(discontinued)",ProgramLinksDiscontinued.GetLabeledDesc(""));
		}

		[TestMethod]
		public void ProgramLinkEdit_CannotEnableIt() {
			string source=TestPaths.ReadRepoFile("OpenDental","Forms","FormProgramLinkEdit.cs");
			string load=TestPaths.GetMethodBody(source,"private void FormProgramLinkEdit_Load(");
			StringAssert.Contains(load,"checkEnabled.Enabled=false;");
			StringAssert.Contains(load,"listToolBars.Enabled=false;");
			string save=TestPaths.GetMethodBody(source,"private void butSave_Click(");
			int indexEnabled=save.IndexOf("ProgramCur.Enabled=checkEnabled.Checked;");
			int indexForceOff=save.IndexOf("ProgramCur.Enabled=false;");
			Assert.IsTrue(indexEnabled>0 && indexForceOff>indexEnabled,"Saved as disabled whatever the box says.");
			string list=TestPaths.ReadRepoFile("OpenDental","Forms","FormProgramLinks.cs");
			StringAssert.Contains(list,"ProgramLinksDiscontinued.GetLabeledDesc(_listPrograms[i].ProgDesc)","Program Links labels it (discontinued).");
		}

		[TestMethod]
		public void Step_HasItsDatedLevel_AndPassesValidation() {
			ForkMigrationStep step=ForkMigrationRunner.GetSteps().Single(x => x is DxcCreditScoreRemovedStep);
			Assert.AreEqual(2026110101,step.Level,"Renumbered on 2026-11-01 from the retired package range level 2027002301.");
			ForkMigrationRunner.ValidateStep(step);
			Assert.IsTrue(ForkMigrationRunner.GetSteps().Where(x => !x.GetType().Namespace.EndsWith(".FeatureSteps") && !x.GetType().Namespace.EndsWith(".Features"))
				.All(x => x.Level<step.Level),"Runs after every M1 and M2-01/M2-02 step, as before the renumbering.");
		}

		[TestMethod]
		public void Step_DisablesLabelsAndRemovesButtons_Idempotent() {
			ScratchDb.RequireDb();
			DataCore.NonQ("INSERT INTO program (ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath) "
				+"VALUES (1,'DXCPatientCreditScore','DXC Patient Credit Score',1,'','','','','','',''),"
				+"(2,'Dolphin','Dolphin Imaging',1,'','','','','','','')");
			DataCore.NonQ("INSERT INTO toolbutitem (ToolButItemNum,ProgramNum,ToolBar,ButtonText) VALUES (1,1,0,'Credit'),(2,1,3,'Credit'),(3,2,0,'Dolphin')");
			ForkMigrationContext context=new ForkMigrationContext(ScratchDb.DbName);
			new DxcCreditScoreRemovedStep().Apply(context);
			Assert.AreEqual(2,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT Enabled FROM program WHERE ProgramNum=1"));
			Assert.AreEqual("DXC Patient Credit Score",DataCore.GetScalar("SELECT ProgDesc FROM program WHERE ProgramNum=1"),"The row's own text is kept.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM toolbutitem WHERE ProgramNum=1"));
			//Other program links are untouched.
			Assert.AreEqual("1",DataCore.GetScalar("SELECT Enabled FROM program WHERE ProgramNum=2"));
			Assert.AreEqual("Dolphin Imaging",DataCore.GetScalar("SELECT ProgDesc FROM program WHERE ProgramNum=2"));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM toolbutitem WHERE ProgramNum=2"));
			//The row stays, because migrated data refers to it by ProgName.
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM program WHERE ProgName='DXCPatientCreditScore'"));
			ForkMigrationContext contextAgain=new ForkMigrationContext(ScratchDb.DbName);
			new DxcCreditScoreRemovedStep().Apply(contextAgain);
			Assert.AreEqual(0,contextAgain.ListChanges.Count,"A second run changes nothing.");
		}

		[TestMethod]
		public void Step_DatabaseWithoutTheRow_OrAlreadyCleanedUp_ChangesNothing() {
			ScratchDb.RequireDb();
			DataCore.NonQ("INSERT INTO program (ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath) "
				+"VALUES (2,'Dolphin','Dolphin Imaging',1,'','','','','','','')");
			ForkMigrationContext context=new ForkMigrationContext(ScratchDb.DbName);
			new DxcCreditScoreRemovedStep().Apply(context);
			Assert.AreEqual(0,context.ListChanges.Count);
			//What Open Dental's 25.2 conversion most likely left: the row disabled, no buttons. An adopted database stays as it is.
			DataCore.NonQ("INSERT INTO program (ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath) "
				+"VALUES (1,'DXCPatientCreditScore','DXC Patient Credit Score',0,'','','','','','','')");
			ForkMigrationContext contextCleaned=new ForkMigrationContext(ScratchDb.DbName);
			new DxcCreditScoreRemovedStep().Apply(contextCleaned);
			Assert.AreEqual(0,contextCleaned.ListChanges.Count);
		}

		[TestMethod]
		public void Step_AlreadyDisabledButWithAButton_OnlyRemovesTheButton() {
			ScratchDb.RequireDb();
			DataCore.NonQ("INSERT INTO program (ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath) "
				+"VALUES (1,'DXCPatientCreditScore','Credit check (discontinued)',0,'','','','','','','')");
			DataCore.NonQ("INSERT INTO toolbutitem (ToolButItemNum,ProgramNum,ToolBar,ButtonText) VALUES (1,1,0,'Credit')");
			ForkMigrationContext context=new ForkMigrationContext(ScratchDb.DbName);
			new DxcCreditScoreRemovedStep().Apply(context);
			Assert.AreEqual(1,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
			StringAssert.StartsWith(context.ListChanges[0],"DELETE FROM toolbutitem");
		}
	}
}
