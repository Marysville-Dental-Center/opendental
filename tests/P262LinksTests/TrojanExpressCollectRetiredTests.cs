// Trojan Express Collect (Open Dental 26.2 item 46, "Deprecated Trojan Express Collect bridge"): the bridge's windows and queries stay removed, the
// program row is a discontinued link, and fork migration step 2026110781 disables it and deletes its buttons. The Trojan benefit link stays.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace P262LinksTests {
	[TestClass]
	public class TrojanExpressCollectRetiredTests {
		private static readonly string[] _arrayRemovedForms={ "FormTrojanCollect","FormTrojanCollectSetup","FormTrojanHelp" };

		[TestMethod]
		public void Windows_AreGone_AndNothingOpensThem() {
			string csproj=TestPaths.ReadRepoFile("OpenPmsDental","OpenPmsDental.csproj");
			foreach(string form in _arrayRemovedForms) {
				foreach(string extension in new[] { ".cs",".designer.cs",".resx" }) {
					Assert.IsFalse(File.Exists(TestPaths.GetRepoFile("OpenPmsDental","Forms",form+extension)),form+extension+" should be deleted.");
					Assert.IsFalse(csproj.Contains("Forms\\"+form+extension+"\""),"OpenPmsDental.csproj still lists "+form+extension);
				}
			}
			string programL=TestPaths.ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs");
			Assert.IsFalse(programL.Contains("ProgramName.TrojanExpressCollect"),"No dispatch branch is left; the discontinued check refuses the link first.");
			Assert.IsFalse(programL.Contains("FormTrojanCollect"));
			Assert.IsFalse(TestPaths.ReadRepoFile("WpfControlsOD","Misc","FormLauncher.cs").Contains("FormTrojanCollect"),"EnumFormName (not stored) has no member for it.");
			Assert.IsFalse(TestPaths.ReadRepoFile("OpenPmsDental","UI","FormLauncherHelper.cs").Contains("FormTrojanCollect"));
			string programLinks=TestPaths.ReadRepoFile("OpenPmsDental","Forms","FormProgramLinks.cs");
			Assert.IsFalse(programLinks.Contains("TrojanExpressCollect"),"Program Links opens the row in the standard window, which keeps it disabled.");
			//The Trojan benefit service's own print window stays.
			Assert.IsTrue(File.Exists(TestPaths.GetRepoFile("OpenPmsDental","Bridges","FormPrintTrojan.cs")));
		}

		[TestMethod]
		public void Queries_OfTheCollectBridgeAreGone_BenefitQueriesStay() {
			List<string> listMethods=typeof(TrojanQueries).GetMethods(BindingFlags.Public|BindingFlags.Static).Select(x => x.Name).ToList();
			foreach(string name in new[] { "GetMaxProcedureDate","GetMaxPaymentDate","GetUniqueFileNum" }) {
				CollectionAssert.DoesNotContain(listMethods,name);
			}
			foreach(string name in new[] { "GetPendingDeletionTable","GetPendingDeletionTableTrojan","GetPlanWithTrojanID","UpdatePlan" }) {
				CollectionAssert.Contains(listMethods,name,"The Trojan benefit link uses it.");
			}
		}

		[TestMethod]
		public void ProgramName_Stays_AndTheLinkIsDiscontinued() {
			Assert.IsTrue(Enum.IsDefined(typeof(ProgramName),"TrojanExpressCollect"),"Migrated rows refer to it by name.");
			Assert.IsTrue(ProgramLinksDiscontinued.IsDiscontinued("TrojanExpressCollect"));
			Assert.IsTrue(ProgramLinksDiscontinued.IsDiscontinued(new Program { ProgName="TrojanExpressCollect" }));
			Assert.IsTrue(ProgramLinksDiscontinued.IsDiscontinued("DXCPatientCreditScore"),"The 25.2 retirement stays.");
			Assert.IsFalse(ProgramLinksDiscontinued.IsDiscontinued("Trojan"),"The Trojan benefit link is not retired.");
			Assert.IsFalse(ProgramLinksDiscontinued.IsDiscontinued("Ez3Di"),"Owner decision: the Ez3D-i link stays.");
			Assert.AreEqual("Trojan Express Collect (discontinued)",ProgramLinksDiscontinued.GetLabeledDesc("Trojan Express Collect"));
		}

		[TestMethod]
		public void Step_HasItsDatedLevel_AndPassesValidation() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			ForkMigrationStep step=listSteps.Single(x => x is TrojanExpressCollectRemovedStep);
			Assert.AreEqual(2026110781,step.Level);
			Assert.AreEqual("Disable the discontinued Trojan Express Collect program link",step.Name);
			ForkMigrationRunner.ValidateStep(step);
			Assert.IsTrue(step.GetType().Namespace.EndsWith(".FeatureSteps"),"A feature data step, not an Open Dental schema step.");
			//The package's range is 2026110781 to 2026110789, and only this step uses it.
			Assert.AreEqual(1,listSteps.Count(x => x.Level>=2026110781 && x.Level<=2026110789));
			Assert.IsTrue(step.Level>new OdParity26_2Schema().Level,"After the 26.2 schema step.");
			string readme=TestPaths.ReadRepoFile("OpenPmsDentalBusiness","Misc","ForkMigrations","README.md");
			StringAssert.Contains(readme,"| 2026110781 | `TrojanExpressCollectRemovedStep.cs`");
		}

		[TestMethod]
		public void Plan_PicksOnlyTheLinksRowsAndButtons() {
			List<DxcCreditScoreRemoved.ProgramRow> listPrograms=new List<DxcCreditScoreRemoved.ProgramRow> {
				new DxcCreditScoreRemoved.ProgramRow { ProgramNum=7,ProgName="trojanexpresscollect  ",IsEnabled=true },//As MariaDB compares it.
				new DxcCreditScoreRemoved.ProgramRow { ProgramNum=3,ProgName="Trojan",IsEnabled=true },
				new DxcCreditScoreRemoved.ProgramRow { ProgramNum=4,ProgName="DXCPatientCreditScore",IsEnabled=true },
				new DxcCreditScoreRemoved.ProgramRow { ProgramNum=5,ProgName="TrojanExpressCollect",IsEnabled=false },
			};
			List<DxcCreditScoreRemoved.ToolButItemRow> listButtons=new List<DxcCreditScoreRemoved.ToolButItemRow> {
				new DxcCreditScoreRemoved.ToolButItemRow { ToolButItemNum=12,ProgramNum=5,ToolBar=0 },
				new DxcCreditScoreRemoved.ToolButItemRow { ToolButItemNum=11,ProgramNum=7,ToolBar=0 },
				new DxcCreditScoreRemoved.ToolButItemRow { ToolButItemNum=10,ProgramNum=3,ToolBar=0 },
				new DxcCreditScoreRemoved.ToolButItemRow { ToolButItemNum=9,ProgramNum=4,ToolBar=0 },
			};
			TrojanExpressCollectRemoved.RowPlan plan=TrojanExpressCollectRemoved.Plan(listPrograms,listButtons);
			CollectionAssert.AreEqual(new long[] { 5,7 },plan.ListPrograms.Select(x => x.ProgramNum).ToList());
			CollectionAssert.AreEqual(new long[] { 7 },plan.ListProgramsDisabled.Select(x => x.ProgramNum).ToList());
			CollectionAssert.AreEqual(new long[] { 11,12 },plan.ListButtonsDeleted.Select(x => x.ToolButItemNum).ToList(),"Every button of the link, enabled or not.");
			CollectionAssert.AreEqual(new[] { "Deleted 2 toolbar button(s) of the discontinued Trojan Express Collect program link (ToolButItemNum 11, 12)",
				"Disabled the discontinued Trojan Express Collect program link (ProgramNum 7)" },plan.GetLogLines());
			Assert.IsFalse(TrojanExpressCollectRemoved.Plan(null,null).IsChanging);
		}

		[TestMethod]
		public void Step_DisablesAndRemovesButtons_KeepsTheRowItsTextAndProperties_Idempotent() {
			ScratchDb.RequireDb();
			DataCore.NonQ("INSERT INTO program (ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,IsDisabledByHq,CustErr) "
				+"VALUES (112,'TrojanExpressCollect','Trojan Express Collect',1,'','','','','','','',0,''),"
				+"(113,'Trojan','Trojan',1,'','','','','','','',0,''),"
				+"(114,'DXCPatientCreditScore','DXC Patient Credit Score',1,'','','','','','','',0,'')");
			DataCore.NonQ("INSERT INTO toolbutitem (ToolButItemNum,ProgramNum,ToolBar,ButtonText) VALUES (1,112,0,'Collect'),(2,112,3,'Collect'),(3,113,0,'Trojan'),"
				+"(4,114,0,'Credit')");
			DataCore.NonQ("INSERT INTO programproperty (ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity) VALUES (112,'FolderPath','C:\\\\Synthetic','',0,0,0)");
			ForkMigrationContext context=new ForkMigrationContext(ScratchDb.DbName);
			new TrojanExpressCollectRemovedStep().Apply(context);
			CollectionAssert.AreEqual(new[] { "Deleted 2 toolbar button(s) of the discontinued Trojan Express Collect program link (ToolButItemNum 1, 2)",
				"Disabled the discontinued Trojan Express Collect program link (ProgramNum 112)" },context.ListChanges,string.Join("\r\n",context.ListChanges));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT Enabled FROM program WHERE ProgramNum=112"));
			Assert.AreEqual("Trojan Express Collect",DataCore.GetScalar("SELECT ProgDesc FROM program WHERE ProgramNum=112"),"The row's own text is kept.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM toolbutitem WHERE ProgramNum=112"));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM programproperty WHERE ProgramNum=112"),"Its properties stay.");
			//The Trojan benefit link and the DXC link (its own step's business) are untouched.
			Assert.AreEqual("1",DataCore.GetScalar("SELECT Enabled FROM program WHERE ProgramNum=113"));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT Enabled FROM program WHERE ProgramNum=114"));
			Assert.AreEqual("2",DataCore.GetScalar("SELECT COUNT(*) FROM toolbutitem WHERE ProgramNum IN (113,114)"));
			ForkMigrationContext contextAgain=new ForkMigrationContext(ScratchDb.DbName);
			new TrojanExpressCollectRemovedStep().Apply(contextAgain);
			Assert.AreEqual(0,contextAgain.ListChanges.Count,"A second run changes nothing.");
		}

		///<summary>Open Dental's 19.2.29 conversion gave every database the row with four properties and one Account module button, enabled only where
		///the office had used the old Trojan preferences; db/init/new-database.sql holds that shape (program 112 disabled, programproperty 223 to 226,
		///toolbutitem (82,112,0,'TrojanCollect')). So on a new database, and on an office that never used the link, the step deletes that one button
		///and nothing else. Without a row it changes nothing.</summary>
		[TestMethod]
		public void Step_NoRow_ChangesNothing_RowAsANewDatabaseHasIt_LosesOnlyItsButton() {
			ScratchDb.RequireDb();
			ForkMigrationContext context=new ForkMigrationContext(ScratchDb.DbName);
			new TrojanExpressCollectRemovedStep().Apply(context);
			Assert.AreEqual(0,context.ListChanges.Count,"No row.");
			//The rows as db/init/new-database.sql has them (and Open Dental's 19.2.29 conversion wrote them for an office that never used the link).
			DataCore.NonQ("INSERT INTO program (ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,IsDisabledByHq,CustErr) "
				+"VALUES (112,'TrojanExpressCollect','Trojan Express Collect',0,'','','','','','','',0,'')");
			DataCore.NonQ("INSERT INTO programproperty (ProgramPropertyNum,ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity) VALUES "
				+"(223,112,'FolderPath','','',0,0,0),(224,112,'BillingType','0','',0,0,0),(225,112,'Password','','',0,1,1),(226,112,'PreviousFileNumber','0','',0,0,0)");
			DataCore.NonQ("INSERT INTO toolbutitem (ToolButItemNum,ProgramNum,ToolBar,ButtonText) VALUES (82,112,0,'TrojanCollect')");
			string programBefore=ReadRow("SELECT * FROM program WHERE ProgramNum=112");
			string propertiesBefore=ReadRow("SELECT * FROM programproperty WHERE ProgramNum=112 ORDER BY ProgramPropertyNum");
			ForkMigrationContext contextNew=new ForkMigrationContext(ScratchDb.DbName);
			new TrojanExpressCollectRemovedStep().Apply(contextNew);
			CollectionAssert.AreEqual(new[] { "Deleted 1 toolbar button(s) of the discontinued Trojan Express Collect program link (ToolButItemNum 82)" },
				contextNew.ListChanges,string.Join("\r\n",contextNew.ListChanges));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM toolbutitem"));
			Assert.AreEqual(programBefore,ReadRow("SELECT * FROM program WHERE ProgramNum=112"),"The row stays as it was: disabled, with its text.");
			Assert.AreEqual(propertiesBefore,ReadRow("SELECT * FROM programproperty WHERE ProgramNum=112 ORDER BY ProgramPropertyNum"),"Its four properties stay.");
			Assert.AreEqual("4",DataCore.GetScalar("SELECT COUNT(*) FROM programproperty WHERE ProgramNum=112"));
			ForkMigrationContext contextAgain=new ForkMigrationContext(ScratchDb.DbName);
			new TrojanExpressCollectRemovedStep().Apply(contextAgain);
			Assert.AreEqual(0,contextAgain.ListChanges.Count,"A second run changes nothing.");
		}

		///<summary>Every cell of the rows the query returns, in order, as one string.</summary>
		private static string ReadRow(string command) {
			System.Data.DataTable table=DataCore.GetTable(command);
			return string.Join("\r\n",table.Rows.Cast<System.Data.DataRow>().Select(x => string.Join("|",x.ItemArray.Select(y => Convert.ToString(y,
				System.Globalization.CultureInfo.InvariantCulture)))));
		}

		[TestMethod]
		public void Step_DisabledRowWithAButton_OnlyRemovesTheButton() {
			ScratchDb.RequireDb();
			DataCore.NonQ("INSERT INTO program (ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,IsDisabledByHq,CustErr) "
				+"VALUES (112,'TrojanExpressCollect','Collections',0,'','','','','','','',0,'')");
			DataCore.NonQ("INSERT INTO toolbutitem (ToolButItemNum,ProgramNum,ToolBar,ButtonText) VALUES (5,112,0,'Collect')");
			ForkMigrationContext context=new ForkMigrationContext(ScratchDb.DbName);
			new TrojanExpressCollectRemovedStep().Apply(context);
			CollectionAssert.AreEqual(new[] { "Deleted 1 toolbar button(s) of the discontinued Trojan Express Collect program link (ToolButItemNum 5)" },context.ListChanges);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM toolbutitem"));
			Assert.AreEqual("Collections",DataCore.GetScalar("SELECT ProgDesc FROM program WHERE ProgramNum=112"));
		}
	}
}
