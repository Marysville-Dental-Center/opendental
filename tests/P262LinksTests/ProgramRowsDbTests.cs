// The Allied Star Connect and OrthoComposite program rows against the scratch database: inserted once, disabled, with the public defaults; an office's
// own row and settings kept; the toolbar button only once the link is enabled; the launches read the saved properties. Nothing is launched.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace P262LinksTests {
	[TestClass]
	public class ProgramRowsDbTests {
		private static void SetEnabled(ProgramName programName,bool isEnabled) {
			DataCore.NonQ("UPDATE program SET Enabled="+(isEnabled ? "1" : "0")+" WHERE ProgName='"+programName+"'");
			ScratchDb.RefreshCaches();
		}

		private static DataTable GetProperties(long programNum) {
			return DataCore.GetTable("SELECT PropertyDesc,PropertyValue,ClinicNum,IsMasked,IsHighSecurity FROM programproperty WHERE ProgramNum="+programNum
				+" ORDER BY ProgramPropertyNum");
		}

		[TestMethod]
		public void AlliedstarConnect_InsertsADisabledRowWithThePublicDefaults_Once() {
			ScratchDb.RequireDb();
			Assert.IsTrue(AlliedstarConnectLink.EnsureProgram());
			ScratchDb.RefreshCaches();
			DataTable table=DataCore.GetTable("SELECT * FROM program WHERE ProgName='AlliedstarConnect'");
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("Allied Star Connect",table.Rows[0]["ProgDesc"].ToString());
			Assert.AreEqual("0",table.Rows[0]["Enabled"].ToString());
			Assert.AreEqual(@"C:\Program Files\Alliedstar\Connect\Connect.exe",table.Rows[0]["Path"].ToString());
			Assert.AreEqual(AlliedstarConnectLink.Note,table.Rows[0]["Note"].ToString());
			long programNum=PIn.Long(table.Rows[0]["ProgramNum"].ToString());
			List<ToolButItem> listButtons=ToolButItems.GetForProgram(programNum);
			Assert.AreEqual(1,listButtons.Count);
			Assert.AreEqual(EnumToolBar.ChartModule,listButtons[0].ToolBar);
			Assert.AreEqual("Allied Star Connect",listButtons[0].ButtonText);
			DataTable tableProps=GetProperties(programNum);
			CollectionAssert.AreEqual(new[] { "Enter 0 to use PatientNum, or 1 to use ChartNum","Info file path" },
				tableProps.Rows.Cast<DataRow>().Select(x => x["PropertyDesc"].ToString()).ToList());
			CollectionAssert.AreEqual(new[] { "0",@"C:\ProgramData\Alliedstar\Connect\info.json" },tableProps.Rows.Cast<DataRow>().Select(x => x["PropertyValue"].ToString()).ToList());
			Assert.IsTrue(tableProps.Rows.Cast<DataRow>().All(x => x["ClinicNum"].ToString()=="0" && x["IsMasked"].ToString()=="0" && x["IsHighSecurity"].ToString()=="0"),
				"No credentials.");
			Assert.IsFalse(AlliedstarConnectLink.EnsureProgram(),"A second run inserts nothing.");
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM program WHERE ProgName='AlliedstarConnect'"));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM toolbutitem WHERE ProgramNum="+programNum));
		}

		[TestMethod]
		public void OrthoComposite_InsertsADisabledRowWithThePublicDefaults_Once() {
			ScratchDb.RequireDb();
			Assert.IsTrue(OrthoCompositeLink.EnsureProgram());
			ScratchDb.RefreshCaches();
			DataTable table=DataCore.GetTable("SELECT * FROM program WHERE ProgName='OrthoComposite'");
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("OrthoComposite",table.Rows[0]["ProgDesc"].ToString());
			Assert.AreEqual("0",table.Rows[0]["Enabled"].ToString());
			Assert.AreEqual("",table.Rows[0]["Path"].ToString());
			long programNum=PIn.Long(table.Rows[0]["ProgramNum"].ToString());
			List<ToolButItem> listButtons=ToolButItems.GetForProgram(programNum);
			Assert.AreEqual(1,listButtons.Count);
			Assert.AreEqual(EnumToolBar.ChartModule,listButtons[0].ToolBar);
			Assert.AreEqual("OrthoComposite",listButtons[0].ButtonText);
			DataTable tableProps=GetProperties(programNum);
			Assert.AreEqual(1,tableProps.Rows.Count);
			Assert.AreEqual("Patient arguments",tableProps.Rows[0]["PropertyDesc"].ToString());
			//next/vendorpub-1: OrthoComposite's public arguments (its Microsoft Store listing).
			Assert.AreEqual("--lastname \"[LName]\" --firstname \"[FName]\" --dob \"[Birthdate]\"",tableProps.Rows[0]["PropertyValue"].ToString());
			Assert.AreEqual(OrthoCompositeLink.Note,DataCore.GetScalar("SELECT Note FROM program WHERE ProgramNum="+programNum));
			Assert.IsFalse(OrthoCompositeLink.EnsureProgram());
		}

		[TestMethod]
		public void ButtonsShowOnlyOnceTheOfficeEnablesTheLink() {
			ScratchDb.RequireDb();
			AlliedstarConnectLink.EnsureProgram();
			OrthoCompositeLink.EnsureProgram();
			ScratchDb.RefreshCaches();
			long numAllied=Programs.GetProgramNum(ProgramName.AlliedstarConnect);
			long numOrtho=Programs.GetProgramNum(ProgramName.OrthoComposite);
			List<long> listShown=ToolButItems.GetForToolBar(EnumToolBar.ChartModule).Select(x => x.ProgramNum).ToList();
			CollectionAssert.DoesNotContain(listShown,numAllied);
			CollectionAssert.DoesNotContain(listShown,numOrtho);
			SetEnabled(ProgramName.AlliedstarConnect,true);
			SetEnabled(ProgramName.OrthoComposite,true);
			listShown=ToolButItems.GetForToolBar(EnumToolBar.ChartModule).Select(x => x.ProgramNum).ToList();
			CollectionAssert.Contains(listShown,numAllied);
			CollectionAssert.Contains(listShown,numOrtho);
		}

		[TestMethod]
		public void AnExistingRowKeepsItsSettings_OnlyMissingPropertiesAreAdded() {
			ScratchDb.RequireDb();
			//A row the office already has (for example from Open Dental, if its ProgName matches): enabled, its own path, no button, no properties.
			DataCore.NonQ("INSERT INTO program (ProgName,ProgDesc,Enabled,Path,CommandLine,Note,PluginDllName,ButtonImage,FileTemplate,FilePath,IsDisabledByHq,CustErr) "
				+"VALUES ('AlliedstarConnect','Scanner cases',1,'E:\\\\Connect\\\\Connect.exe','','','','','','',0,'')");
			ScratchDb.RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.AlliedstarConnect);
			Assert.IsTrue(AlliedstarConnectLink.EnsureProgram(),"The two properties are added.");
			ScratchDb.RefreshCaches();
			DataTable table=DataCore.GetTable("SELECT ProgDesc,Enabled,Path FROM program WHERE ProgramNum="+programNum);
			Assert.AreEqual("Scanner cases",table.Rows[0]["ProgDesc"].ToString());
			Assert.AreEqual("1",table.Rows[0]["Enabled"].ToString());
			Assert.AreEqual(@"E:\Connect\Connect.exe",table.Rows[0]["Path"].ToString());
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM toolbutitem WHERE ProgramNum="+programNum),"Its toolbars are the office's.");
			Assert.AreEqual(2,GetProperties(programNum).Rows.Count);
			//The office's own Info file path is kept on the next run.
			DataCore.NonQ("UPDATE programproperty SET PropertyValue='D:\\\\Exchange\\\\patient.json' WHERE ProgramNum="+programNum+" AND PropertyDesc='Info file path'");
			ScratchDb.RefreshCaches();
			Assert.IsFalse(AlliedstarConnectLink.EnsureProgram());
			ScratchDb.RefreshCaches();
			Assert.AreEqual(@"D:\Exchange\patient.json",AlliedstarConnectLink.GetInfoFilePath(Programs.GetCur(ProgramName.AlliedstarConnect)));
		}

		[TestMethod]
		public void Launches_ReadTheSavedProperties() {
			ScratchDb.RequireDb();
			AlliedstarConnectLink.EnsureProgram();
			OrthoCompositeLink.EnsureProgram();
			ScratchDb.RefreshCaches();
			Program programAllied=Programs.GetCur(ProgramName.AlliedstarConnect);
			Program programOrtho=Programs.GetCur(ProgramName.OrthoComposite);
			Patient patient=SyntheticPatients.Create();
			//The default program path does not exist on a test computer, so the program check refuses; the saved values are what the launch reads.
			Assert.AreEqual(@"C:\ProgramData\Alliedstar\Connect\info.json",AlliedstarConnectLink.GetInfoFilePath(programAllied));
			DataCore.NonQ("UPDATE programproperty SET PropertyValue='1' WHERE ProgramNum="+programAllied.ProgramNum
				+" AND PropertyDesc='Enter 0 to use PatientNum, or 1 to use ChartNum'");
			DataCore.NonQ("UPDATE programproperty SET PropertyValue='--dob [Birthdate]' WHERE ProgramNum="+programOrtho.ProgramNum
				+" AND PropertyDesc='Patient arguments'");
			DataCore.NonQ("UPDATE program SET Path='ExampleImaging.exe' WHERE ProgramNum="+programOrtho.ProgramNum);
			ScratchDb.RefreshCaches();
			programOrtho=Programs.GetCur(ProgramName.OrthoComposite);
			BridgeLaunch launchOrtho=OrthoCompositeLink.GetLaunch(programOrtho,patient);
			Assert.IsTrue(launchOrtho.IsReady,"A name without a folder is passed to Windows unchecked.");
			Assert.AreEqual("ExampleImaging.exe",launchOrtho.FileName);
			Assert.AreEqual("--dob 03/09/1980",launchOrtho.Arguments);
			BridgeFileLaunch launchAllied=AlliedstarConnectLink.GetLaunch(programAllied,patient);
			if(System.IO.File.Exists(@"C:\Program Files\Alliedstar\Connect\Connect.exe")) {
				Assert.Inconclusive("Connect is installed on this computer; the refusal below is not tested.");
			}
			Assert.AreEqual(BridgeLaunchProblem.ProgramNotFound,launchAllied.Problem);
			Assert.AreEqual("1",ProgramProperties.GetValOrDefault(programAllied.ProgramNum,AlliedstarConnectLink.PropertyDescs.PatOrChartNum,0));
		}
	}
}
