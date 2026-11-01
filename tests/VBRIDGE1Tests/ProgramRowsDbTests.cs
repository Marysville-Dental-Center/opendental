// The program link rows V-BRIDGE1 inserts at runtime (when Setup > Program Links opens), on a scratch database: disabled, with the public defaults,
// once only, without touching a row the office already has; and the Insurance Answers Plus desktop-path cleanup. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace VBRIDGE1Tests {
	[TestClass]
	public class ProgramRowsDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BridgeDb.Begin();
		}

		private static string GetProperty(List<ProgramProperty> listProgramProperties,string desc) {
			List<ProgramProperty> listMatches=listProgramProperties.FindAll(x => x.PropertyDesc==desc && x.ClinicNum==0);
			Assert.AreEqual(1,listMatches.Count,"One ClinicNum 0 row for "+desc);
			return listMatches[0].PropertyValue;
		}

		[TestMethod]
		public void Overjet_InsertsADisabledRowWithThePublicDefaults_Once() {
			BridgeDb.DeleteProgram(ProgramName.Overjet);
			Assert.IsTrue(OverjetLink.EnsureProgram());
			List<Program> listPrograms=BridgeDb.GetProgramsFromDb(ProgramName.Overjet);
			Assert.AreEqual(1,listPrograms.Count);
			Program program=listPrograms[0];
			Assert.IsFalse(program.Enabled,"Off by default.");
			Assert.AreEqual("Overjet",program.ProgDesc);
			Assert.AreEqual("https://clinic.overjet.ai/app/bridge_redirect",program.Path);
			Assert.AreEqual("",program.CommandLine);
			Assert.AreEqual(OverjetLink.Note,program.Note);
			List<ToolButItem> listToolButItems=BridgeDb.GetToolButItemsFromDb(program.ProgramNum);
			Assert.AreEqual(1,listToolButItems.Count);
			Assert.AreEqual(EnumToolBar.MainToolbar,listToolButItems[0].ToolBar);
			Assert.AreEqual("Overjet",listToolButItems[0].ButtonText);
			List<ProgramProperty> listProgramProperties=BridgeDb.GetPropertiesFromDb(program.ProgramNum);
			Assert.AreEqual("0",GetProperty(listProgramProperties,ProgramProperties.PropertyDescs.PatOrChartNum));
			Assert.AreEqual("",GetProperty(listProgramProperties,OverjetLink.PropertyDescs.PatientUrlParameters));
			Assert.IsTrue(listProgramProperties.All(x => !x.IsMasked && !x.IsHighSecurity),"No credentials: the office signs in on the vendor's page.");
			//A second call (Program Links opened again, or another workstation) inserts nothing.
			BridgeDb.RefreshCaches();
			Assert.IsFalse(OverjetLink.EnsureProgram());
			Assert.AreEqual(1,BridgeDb.GetProgramsFromDb(ProgramName.Overjet).Count);
			Assert.AreEqual(1,BridgeDb.GetToolButItemsFromDb(program.ProgramNum).Count);
			Assert.AreEqual(2,BridgeDb.GetPropertiesFromDb(program.ProgramNum).Count);
		}

		[TestMethod]
		public void Overjet_ButtonIsHiddenUntilTheOfficeEnablesIt() {
			BridgeDb.DeleteProgram(ProgramName.Overjet);
			OverjetLink.EnsureProgram();
			BridgeDb.RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.Overjet);
			Assert.IsFalse(ToolButItems.GetForToolBar(EnumToolBar.MainToolbar).Any(x => x.ProgramNum==programNum),"Disabled: no button.");
			DataCore.NonQ("UPDATE program SET Enabled=1 WHERE ProgramNum="+POut.Long(programNum));
			BridgeDb.RefreshCaches();
			Assert.IsTrue(ToolButItems.GetForToolBar(EnumToolBar.MainToolbar).Any(x => x.ProgramNum==programNum),"Enabled: the button shows.");
		}

		[TestMethod]
		public void DtxStudio_InsertsBothDisabledRows() {
			foreach(ProgramName programName in new[] { ProgramName.DTXStudio,ProgramName.DTXStudioAcquire }) {
				BridgeDb.DeleteProgram(programName);
				Assert.IsTrue(DtxStudioLink.EnsureProgram(programName),programName.ToString());
				List<Program> listPrograms=BridgeDb.GetProgramsFromDb(programName);
				Assert.AreEqual(1,listPrograms.Count);
				Program program=listPrograms[0];
				Assert.IsFalse(program.Enabled);
				Assert.AreEqual(@"C:\Program Files\DTX Studio Clinic\DTXsync.exe",program.Path);
				bool isAcquire=programName==ProgramName.DTXStudioAcquire;
				Assert.AreEqual(isAcquire ? "DTX Studio Acquire" : "DTX Studio",program.ProgDesc);
				CollectionAssert.AreEqual(new[] { EnumToolBar.ImagingModule,EnumToolBar.MainToolbar },
					BridgeDb.GetToolButItemsFromDb(program.ProgramNum).Select(x => x.ToolBar).ToArray());
				Assert.IsTrue(BridgeDb.GetToolButItemsFromDb(program.ProgramNum).All(x => x.ButtonText==(isAcquire ? "DTX Studio Acquire" : "DTX Studio")));
				List<ProgramProperty> listProgramProperties=BridgeDb.GetPropertiesFromDb(program.ProgramNum);
				Assert.AreEqual("MM/dd/yyyy",GetProperty(listProgramProperties,DtxStudioLink.PropertyDescs.BirthdateFormat));
				Assert.AreEqual("0",GetProperty(listProgramProperties,ProgramProperties.PropertyDescs.PatOrChartNum));
				Assert.AreEqual("",GetProperty(listProgramProperties,DtxStudioLink.PropertyDescs.PatientArguments));
				BridgeDb.RefreshCaches();
				Assert.IsFalse(DtxStudioLink.EnsureProgram(programName));
			}
		}

		[TestMethod]
		public void ExistingRow_KeepsTheOfficeSettings_AndGetsOnlyMissingProperties() {
			BridgeDb.DeleteProgram(ProgramName.DTXStudio);
			//A row as an office database might already have it: enabled, its own path, its own toolbar and an existing Birthdate Format.
			Program program=new Program();
			program.ProgName=ProgramName.DTXStudio.ToString();
			program.ProgDesc="Office description";
			program.Enabled=true;
			program.Path=@"D:\DTX\DTXsync.exe";
			program.CommandLine="/office";
			program.Note="office note";
			program.PluginDllName="";
			program.ButtonImage="";
			program.FileTemplate="";
			program.FilePath="";
			program.CustErr="";
			long programNum=Programs.Insert(program);
			ToolButItems.Insert(new ToolButItem { ProgramNum=programNum,ToolBar=EnumToolBar.ChartModule,ButtonText="Office DTX" });
			ProgramProperties.InsertIfMissing(programNum,DtxStudioLink.PropertyDescs.BirthdateFormat,0,"dd/MM/yyyy",false,false);
			BridgeDb.RefreshCaches();
			Assert.IsTrue(DtxStudioLink.EnsureProgram(ProgramName.DTXStudio),"The two properties the row lacks are added.");
			Program programAfter=BridgeDb.GetProgramsFromDb(ProgramName.DTXStudio).Single();
			Assert.IsTrue(programAfter.Enabled);
			Assert.AreEqual("Office description",programAfter.ProgDesc);
			Assert.AreEqual(@"D:\DTX\DTXsync.exe",programAfter.Path);
			Assert.AreEqual("/office",programAfter.CommandLine);
			Assert.AreEqual("office note",programAfter.Note);
			List<ToolButItem> listToolButItems=BridgeDb.GetToolButItemsFromDb(programNum);
			Assert.AreEqual(1,listToolButItems.Count,"The office's toolbars stay as they are.");
			Assert.AreEqual(EnumToolBar.ChartModule,listToolButItems[0].ToolBar);
			List<ProgramProperty> listProgramProperties=BridgeDb.GetPropertiesFromDb(programNum);
			Assert.AreEqual("dd/MM/yyyy",GetProperty(listProgramProperties,DtxStudioLink.PropertyDescs.BirthdateFormat));
			Assert.AreEqual("0",GetProperty(listProgramProperties,ProgramProperties.PropertyDescs.PatOrChartNum));
			Assert.AreEqual("",GetProperty(listProgramProperties,DtxStudioLink.PropertyDescs.PatientArguments));
		}

		[TestMethod]
		public void Iap_DesktopPath_IsBlankedAndDisabled_Once() {
			//Every database has the IAP row; the 24.3 seed has it with the desktop folder. Make sure it's enabled, to see it switched off.
			Assert.AreEqual(1,BridgeDb.GetProgramsFromDb(ProgramName.IAP).Count,"The IAP row comes from the early conversions.");
			DataCore.NonQ("UPDATE program SET Enabled=1,Path='"+POut.String(@"C:\IAPlus\")+"',Note='No buttons are available.' WHERE ProgName='IAP'");
			BridgeDb.RefreshCaches();
			Assert.IsTrue(IapLink.EnsureProgram());
			Program program=BridgeDb.GetProgramsFromDb(ProgramName.IAP).Single();
			Assert.IsFalse(program.Enabled,"Hidden until the office enters the web address.");
			Assert.AreEqual("",program.Path);
			Assert.AreEqual(IapLink.Note,program.Note);
			Assert.AreEqual(0,BridgeDb.GetToolButItemsFromDb(program.ProgramNum).Count,"The IAP button lives on the Insurance Plan window.");
			BridgeDb.RefreshCaches();
			Assert.IsFalse(IapLink.EnsureProgram(),"Nothing left to change.");
		}

		[TestMethod]
		public void Iap_WebAddress_IsLeftAlone_AndOpensAsItIs() {
			DataCore.NonQ("UPDATE program SET Enabled=1,Path='https://iap.example.test/login',Note='office note' WHERE ProgName='IAP'");
			BridgeDb.RefreshCaches();
			Assert.IsFalse(IapLink.EnsureProgram());
			Program program=BridgeDb.GetProgramsFromDb(ProgramName.IAP).Single();
			Assert.IsTrue(program.Enabled);
			Assert.AreEqual("https://iap.example.test/login",program.Path);
			Assert.AreEqual("office note",program.Note);
			BridgeLaunch bridgeLaunch=IapLink.GetLaunch(Programs.GetCur(ProgramName.IAP));
			Assert.IsTrue(bridgeLaunch.IsReady);
			Assert.AreEqual("https://iap.example.test/login",bridgeLaunch.FileName);
			//Plain http is the office's own entry: kept, but not opened.
			DataCore.NonQ("UPDATE program SET Path='http://iap.example.test/login' WHERE ProgName='IAP'");
			BridgeDb.RefreshCaches();
			Assert.IsFalse(IapLink.EnsureProgram());
			Assert.AreEqual(BridgeLaunchProblem.PathNotHttps,IapLink.GetLaunch(Programs.GetCur(ProgramName.IAP)).Problem);
		}

		[TestMethod]
		public void Overjet_LaunchReadsTheRowAndItsProperties() {
			BridgeDb.DeleteProgram(ProgramName.Overjet);
			OverjetLink.EnsureProgram();
			BridgeDb.RefreshCaches();
			Program program=Programs.GetCur(ProgramName.Overjet);
			Patient patient=new Patient { PatNum=7,ChartNumber="A1",LName="Test",FName="Pat" };
			Assert.AreEqual(OverjetLink.DefaultPath,OverjetLink.GetLaunch(program,patient).FileName,"Blank parameters: no patient data.");
			BridgeDb.SetProperty(program.ProgramNum,OverjetLink.PropertyDescs.PatientUrlParameters,"id=[PatientID]");
			Assert.AreEqual(OverjetLink.DefaultPath+"?id=7",OverjetLink.GetLaunch(program,patient).FileName);
			BridgeDb.SetProperty(program.ProgramNum,ProgramProperties.PropertyDescs.PatOrChartNum,"1");
			Assert.AreEqual(OverjetLink.DefaultPath+"?id=A1",OverjetLink.GetLaunch(program,patient).FileName);
		}

		[TestMethod]
		public void DtxStudio_LaunchReadsTheRowAndItsProperties() {
			BridgeDb.DeleteProgram(ProgramName.DTXStudioAcquire);
			DtxStudioLink.EnsureProgram(ProgramName.DTXStudioAcquire);
			string pathExe=Path.GetTempFileName();//Stands in for DTXsync.exe; nothing is started.
			try {
				DataCore.NonQ("UPDATE program SET Path='"+POut.String(pathExe)+"',CommandLine='/acquire' WHERE ProgName='DTXStudioAcquire'");
				BridgeDb.RefreshCaches();
				Program program=Programs.GetCur(ProgramName.DTXStudioAcquire);
				Patient patient=new Patient { PatNum=8,LName="Test",FName="Pat",Birthdate=new System.DateTime(2000,12,31) };
				Assert.AreEqual(BridgeLaunchProblem.PatientArgumentsNotSet,DtxStudioLink.GetLaunch(program,patient).Problem);
				BridgeDb.SetProperty(program.ProgramNum,DtxStudioLink.PropertyDescs.PatientArguments,"[PatientID] [Birthdate]");
				BridgeDb.SetProperty(program.ProgramNum,DtxStudioLink.PropertyDescs.BirthdateFormat,"yyyyMMdd");
				BridgeLaunch bridgeLaunch=DtxStudioLink.GetLaunch(program,patient);
				Assert.IsTrue(bridgeLaunch.IsReady);
				Assert.AreEqual(pathExe,bridgeLaunch.FileName);
				Assert.AreEqual("8 20001231 /acquire",bridgeLaunch.Arguments);
			}
			finally {
				File.Delete(pathExe);
			}
		}
	}
}
