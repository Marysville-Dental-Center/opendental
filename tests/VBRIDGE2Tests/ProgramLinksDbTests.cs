// Integration tests for the V-BRIDGE2 program rows, buttons and Clerri keys against a scratch MariaDB database on this computer. The database gets
// the Open Dental 26.1.70 schema from the reference snapshot embedded in OpenPmsDentalBusiness and synthetic rows only. Its name is unique to the run
// (time, process id, random) and it is dropped afterwards. The tests are skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root
// with a blank password, or when VBRIDGE2_TEST_DB=skip. Nothing is launched and no vendor is called.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.VendorDirect;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace VBRIDGE2Tests {
	[TestClass]
	public class ProgramLinksDbTests {
		private const string SERVER="127.0.0.1";
		private const string CONNECTION="Server="+SERVER+";Port=3306;User ID=root;Password=;Connection Timeout=15";
		///<summary>Unique to this run, because several worktrees and test runners share the one local server.</summary>
		private static readonly string _dbName="vbridge2_"+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("VBRIDGE2_TEST_DB")=="skip") {
				_skipReason="VBRIDGE2_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"CREATE DATABASE `"+_dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
				return;
			}
			//The server answers, so from here on a failure is a real failure, not a reason to skip.
			using(MySqlConnection connection=new MySqlConnection(CONNECTION+";Database="+_dbName)) {
				connection.Open();
				foreach(SchemaTable table in SchemaVerifier.GetReference().Tables) {
					Exec(connection,table.ToDdl());
				}
				//The preferences the caches these tests touch read (the provider cache, for Patients.ReplacePatient).
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('EasyHideDentalSchools','1')");
			}
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(SERVER,_dbName,"root","","","",DatabaseType.MySql,true);
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
			Prefs.RefreshCache();
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS `"+_dbName+"`");
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; its name is unique to this run.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			foreach(string table in new[] { "program","programproperty","toolbutitem","provider" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			RefreshCaches();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static void RefreshCaches() {
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			ToolButItems.RefreshCache();
		}

		private static DataTable GetRows(string command) {
			return DataCore.GetTable(command);
		}

		private static void SetEnabled(ProgramName programName,bool isEnabled) {
			DataCore.NonQ("UPDATE program SET Enabled="+(isEnabled ? "1" : "0")+" WHERE ProgName='"+programName+"'");
			RefreshCaches();
		}

		#region Clever One
		[TestMethod]
		public void CleverOne_InsertsADisabledRowWithTheSetupPageDefaults() {
			Assert.IsTrue(CleverOneProgram.EnsureProgram());
			RefreshCaches();
			DataTable table=GetRows("SELECT * FROM program WHERE ProgName='CleverOne'");
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("Clever One by VATECH",table.Rows[0]["ProgDesc"].ToString());
			Assert.AreEqual("0",table.Rows[0]["Enabled"].ToString());
			Assert.AreEqual(@"C:\Program Files\VATECH\CleverOne\Bin\CleverOne.exe",table.Rows[0]["Path"].ToString());
			Assert.AreEqual("",table.Rows[0]["CommandLine"].ToString());
			long programNum=PIn.Long(table.Rows[0]["ProgramNum"].ToString());
			List<ToolButItem> listToolButItems=ToolButItems.GetForProgram(programNum);
			Assert.AreEqual(1,listToolButItems.Count);
			Assert.AreEqual(EnumToolBar.ChartModule,listToolButItems[0].ToolBar);
			Assert.AreEqual("Clever One",listToolButItems[0].ButtonText);
			DataTable tableProps=GetRows("SELECT PropertyDesc,PropertyValue,ClinicNum,IsMasked,IsHighSecurity FROM programproperty WHERE ProgramNum="+programNum);
			Assert.AreEqual(1,tableProps.Rows.Count);
			Assert.AreEqual("Enter 0 to use PatientNum, or 1 to use ChartNum",tableProps.Rows[0]["PropertyDesc"].ToString());
			Assert.AreEqual("0",tableProps.Rows[0]["PropertyValue"].ToString());
			Assert.AreEqual("0",tableProps.Rows[0]["ClinicNum"].ToString());
			Assert.AreEqual("0",tableProps.Rows[0]["IsMasked"].ToString());
			Assert.AreEqual(CleverOneProgram.Note,table.Rows[0]["Note"].ToString());
			Assert.IsFalse(CleverOneProgram.UsesChartNumber(Programs.GetCur(ProgramName.CleverOne)),"The default 0 sends the PatNum.");
		}

		[TestMethod]
		public void CleverOne_SecondRunChangesNothingTheOfficeSet() {
			CleverOneProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.CleverOne);
			DataCore.NonQ("UPDATE program SET Path='D:\\\\Imaging\\\\CleverOne.exe' WHERE ProgramNum="+programNum);
			DataCore.NonQ("DELETE FROM toolbutitem WHERE ProgramNum="+programNum);
			DataCore.NonQ("INSERT INTO toolbutitem (ProgramNum,ToolBar,ButtonText) VALUES ("+programNum+","+(int)EnumToolBar.ImagingModule+",'VATECH')");
			DataCore.NonQ("UPDATE programproperty SET PropertyValue='1' WHERE ProgramNum="+programNum);
			RefreshCaches();
			Assert.IsFalse(CleverOneProgram.EnsureProgram(),"Nothing is missing, so nothing is inserted.");
			RefreshCaches();
			Assert.AreEqual(1,GetRows("SELECT * FROM program WHERE ProgName='CleverOne'").Rows.Count);
			Assert.AreEqual(@"D:\Imaging\CleverOne.exe",Programs.GetCur(ProgramName.CleverOne).Path);
			List<ToolButItem> listToolButItems=ToolButItems.GetForProgram(programNum);
			Assert.AreEqual(1,listToolButItems.Count);
			Assert.AreEqual(EnumToolBar.ImagingModule,listToolButItems[0].ToolBar);
			Assert.AreEqual("1",ProgramProperties.GetPropVal(programNum,CleverOneProgram.PropPatOrChartNum,0));
			Assert.IsTrue(CleverOneProgram.UsesChartNumber(Programs.GetCur(ProgramName.CleverOne)),"The saved 1 sends the ChartNumber.");
		}

		[TestMethod]
		public void CleverOne_RestoresAMissingPropertyOnly() {
			CleverOneProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.CleverOne);
			DataCore.NonQ("DELETE FROM programproperty WHERE ProgramNum="+programNum);
			DataCore.NonQ("DELETE FROM toolbutitem WHERE ProgramNum="+programNum);
			RefreshCaches();
			Assert.IsTrue(CleverOneProgram.EnsureProgram());
			RefreshCaches();
			Assert.AreEqual("0",ProgramProperties.GetPropVal(programNum,CleverOneProgram.PropPatOrChartNum,0));
			Assert.AreEqual(0,ToolButItems.GetForProgram(programNum).Count,"Buttons the office removed stay removed.");
		}

		[TestMethod]
		public void CleverOne_ButtonOnlyAfterTheOfficeEnablesIt() {
			CleverOneProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.CleverOne);
			Assert.IsFalse(ToolButItems.GetForToolBar(EnumToolBar.ChartModule).Any(x => x.ProgramNum==programNum));
			SetEnabled(ProgramName.CleverOne,true);
			Assert.IsTrue(ToolButItems.GetForToolBar(EnumToolBar.ChartModule).Any(x => x.ProgramNum==programNum));
		}
		#endregion Clever One

		#region Clerri
		[TestMethod]
		public void Clerri_InsertsADisabledRowWithAFamilyButtonAndTheKeyRows() {
			Assert.IsTrue(ClerriProgram.EnsureProgram());
			RefreshCaches();
			DataTable table=GetRows("SELECT * FROM program WHERE ProgName='Clerri'");
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("Clerri",table.Rows[0]["ProgDesc"].ToString());
			Assert.AreEqual("0",table.Rows[0]["Enabled"].ToString());
			Assert.AreEqual("",table.Rows[0]["Path"].ToString());
			long programNum=PIn.Long(table.Rows[0]["ProgramNum"].ToString());
			List<ToolButItem> listToolButItems=ToolButItems.GetForProgram(programNum);
			Assert.AreEqual(1,listToolButItems.Count);
			Assert.AreEqual(EnumToolBar.FamilyModule,listToolButItems[0].ToolBar);
			Assert.AreEqual("Clerri",listToolButItems[0].ButtonText);
			Assert.IsTrue(ClerriProgram.IsToolbarButtonShown(programNum));
			DataTable tableProps=GetRows("SELECT PropertyDesc,PropertyValue,ClinicNum,IsMasked,IsHighSecurity FROM programproperty "
				+"WHERE ProgramNum="+programNum+" ORDER BY PropertyDesc");
			Assert.AreEqual(2,tableProps.Rows.Count);
			Assert.AreEqual("Client Key",tableProps.Rows[0]["PropertyDesc"].ToString());
			Assert.AreEqual("",tableProps.Rows[0]["PropertyValue"].ToString());
			Assert.AreEqual("1",tableProps.Rows[0]["IsMasked"].ToString());
			Assert.AreEqual("1",tableProps.Rows[0]["IsHighSecurity"].ToString());
			Assert.AreEqual("Dashboard Address",tableProps.Rows[1]["PropertyDesc"].ToString());
			Assert.AreEqual("",tableProps.Rows[1]["PropertyValue"].ToString());
			Assert.AreEqual("0",tableProps.Rows[1]["IsMasked"].ToString());
			Assert.IsFalse(ClerriProgram.EnsureProgram(),"A second run inserts nothing.");
			Assert.AreEqual(1,GetRows("SELECT * FROM program WHERE ProgName='Clerri'").Rows.Count);
			Assert.AreEqual(2,GetRows("SELECT * FROM programproperty WHERE ProgramNum="+programNum).Rows.Count);
			Assert.AreEqual(1,GetRows("SELECT * FROM toolbutitem WHERE ProgramNum="+programNum).Rows.Count);
		}

		[TestMethod]
		public void Clerri_RaisesTheFlagsOfAPlainKeyRow() {
			ClerriProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.Clerri);
			DataCore.NonQ("UPDATE programproperty SET IsMasked=0,IsHighSecurity=0 WHERE PropertyDesc='Client Key'");
			RefreshCaches();
			Assert.IsTrue(ClerriProgram.EnsureProgram());
			DataTable table=GetRows("SELECT IsMasked,IsHighSecurity FROM programproperty WHERE ProgramNum="+programNum+" AND PropertyDesc='Client Key'");
			Assert.AreEqual("1",table.Rows[0]["IsMasked"].ToString());
			Assert.AreEqual("1",table.Rows[0]["IsHighSecurity"].ToString());
		}

		[TestMethod]
		public void Clerri_ShowToolbarButtonTouchesOnlyTheFamilyButton() {
			ClerriProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.Clerri);
			DataCore.NonQ("INSERT INTO toolbutitem (ProgramNum,ToolBar,ButtonText) VALUES ("+programNum+","+(int)EnumToolBar.MainToolbar+",'Clerri')");
			RefreshCaches();
			ClerriProgram.SetToolbarButtonShown(programNum,false);
			RefreshCaches();
			Assert.IsFalse(ClerriProgram.IsToolbarButtonShown(programNum));
			Assert.AreEqual(1,ToolButItems.GetForProgram(programNum).Count(x => x.ToolBar==EnumToolBar.MainToolbar));
			ClerriProgram.SetToolbarButtonShown(programNum,true);
			RefreshCaches();
			ClerriProgram.SetToolbarButtonShown(programNum,true);
			RefreshCaches();
			Assert.AreEqual(1,ToolButItems.GetForProgram(programNum).Count(x => x.ToolBar==EnumToolBar.FamilyModule),"Never a second button.");
		}

		[TestMethod]
		public void Clerri_ButtonPickedWhileDisabledThenEnabledInTheSameSave_BothSaved() {
			//The review's case: saved disabled with the button removed; the user checks Show Toolbar Button, then Enabled (which locks the box), and saves.
			ClerriProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.Clerri);
			ClerriProgram.SetToolbarButtonShown(programNum,false);
			RefreshCaches();
			Assert.IsFalse(ClerriProgram.IsToolbarButtonShown(programNum));
			ClerriProgram.SaveToolbarButtonAndEnabled(Programs.GetCur(ProgramName.Clerri),isShownChecked:true,isEnabledChecked:true);
			RefreshCaches();
			Assert.IsTrue(Programs.GetCur(ProgramName.Clerri).Enabled);
			Assert.IsTrue(ClerriProgram.IsToolbarButtonShown(programNum),"The button the user picked is saved.");
			Assert.IsTrue(ToolButItems.GetForToolBar(EnumToolBar.FamilyModule).Any(x => x.ProgramNum==programNum),"So Clerri can be launched.");
		}

		[TestMethod]
		public void Clerri_ButtonClearedWhileDisabledThenEnabledInTheSameSave_BothSaved() {
			ClerriProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.Clerri);
			Assert.IsTrue(ClerriProgram.IsToolbarButtonShown(programNum));
			ClerriProgram.SaveToolbarButtonAndEnabled(Programs.GetCur(ProgramName.Clerri),isShownChecked:false,isEnabledChecked:true);
			RefreshCaches();
			Assert.IsTrue(Programs.GetCur(ProgramName.Clerri).Enabled);
			Assert.IsFalse(ClerriProgram.IsToolbarButtonShown(programNum),"The button the user cleared stays off.");
			Assert.IsFalse(ToolButItems.GetForToolBar(EnumToolBar.FamilyModule).Any(x => x.ProgramNum==programNum));
		}

		[TestMethod]
		public void Clerri_ButtonNotChangedWhileSavedEnabled() {
			ClerriProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.Clerri);
			SetEnabled(ProgramName.Clerri,true);
			//Saved as enabled: the box is locked from the start, so a different value is not saved, even together with disabling.
			ClerriProgram.SaveToolbarButtonAndEnabled(Programs.GetCur(ProgramName.Clerri),isShownChecked:false,isEnabledChecked:false);
			RefreshCaches();
			Assert.IsFalse(Programs.GetCur(ProgramName.Clerri).Enabled);
			Assert.IsTrue(ClerriProgram.IsToolbarButtonShown(programNum),"Disable and save first, then change the button.");
			//Now saved as disabled: the change is saved.
			ClerriProgram.SaveToolbarButtonAndEnabled(Programs.GetCur(ProgramName.Clerri),isShownChecked:false,isEnabledChecked:false);
			RefreshCaches();
			Assert.IsFalse(ClerriProgram.IsToolbarButtonShown(programNum));
		}

		[TestMethod]
		public void Clerri_FamilyButtonOnlyAfterTheOfficeEnablesIt() {
			ClerriProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.Clerri);
			Assert.IsFalse(ToolButItems.GetForToolBar(EnumToolBar.FamilyModule).Any(x => x.ProgramNum==programNum));
			SetEnabled(ProgramName.Clerri,true);
			Assert.IsTrue(ToolButItems.GetForToolBar(EnumToolBar.FamilyModule).Any(x => x.ProgramNum==programNum));
		}

		[TestMethod]
		public void Clerri_KeysAreEncryptedAndChosenPerClinic() {
			ClerriProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.Clerri);
			VendorCredentials.WriteSetting(programNum,ClerriProgram.PropDashboardAddress,0,"https://dashboard.example.com/?k=[ClientKey]&p=[PatNum]");
			Assert.IsTrue(VendorCredentials.WriteSecret(programNum,ClerriProgram.PropClientKey,0,"default-key"));
			Assert.IsTrue(VendorCredentials.WriteSecret(programNum,ClerriProgram.PropClientKey,5,"clinic5-key"));
			Assert.IsTrue(VendorCredentials.WriteSecret(programNum,ClerriProgram.PropClientKey,7,""));//A cleared clinic key.
			RefreshCaches();
			Dictionary<long,string> dictExpected=new Dictionary<long,string> { { 0,"default-key" },{ 5,"clinic5-key" },{ 7,"" } };
			DataTable tableKeys=GetRows("SELECT PropertyValue,IsMasked,IsHighSecurity,ClinicNum FROM programproperty WHERE ProgramNum="+programNum
				+" AND PropertyDesc='Client Key'");
			Assert.AreEqual(3,tableKeys.Rows.Count);
			foreach(DataRow row in tableKeys.Rows) {
				long clinicNum=PIn.Long(row["ClinicNum"].ToString());
				string stored=row["PropertyValue"].ToString();
				string expected=dictExpected[clinicNum];
				if(expected=="") {
					Assert.AreEqual("",stored);
				}
				else {
					Assert.IsTrue(Secrets.IsEncrypted(stored),"Clinic "+clinicNum+": stored as osd1$.");
					Assert.AreNotEqual(expected,stored,"No plain text.");
					string plainText;
					Assert.IsTrue(Secrets.Decrypt(stored,out plainText));
					Assert.AreEqual(expected,plainText);
				}
				Assert.AreEqual("1",row["IsMasked"].ToString());
				Assert.AreEqual("1",row["IsHighSecurity"].ToString());
			}
			string error;
			Assert.AreEqual("https://dashboard.example.com/?k=clinic5-key&p=1042",ClerriProgram.GetDashboardUrl(programNum,5,1042,out error));
			Assert.AreEqual("https://dashboard.example.com/?k=default-key&p=1042",ClerriProgram.GetDashboardUrl(programNum,7,1042,out error),
				"A blank clinic key uses the default.");
			Assert.AreEqual("https://dashboard.example.com/?k=default-key&p=",ClerriProgram.GetDashboardUrl(programNum,9,0,out error),
				"A clinic without a row uses the default.");
			Assert.AreEqual("https://dashboard.example.com/?k=default-key&p=3",ClerriProgram.GetDashboardUrl(programNum,0,3,out error));
			Assert.IsTrue(ClerriProgram.HasAnyClientKey(programNum));
			Assert.IsTrue(ClerriProgram.ReadClientKeyExact(programNum,7).Status==SecretReadStatus.Blank);
		}

		[TestMethod]
		public void Clerri_NoKeyOrAnUnreadableKeyIsAnError() {
			ClerriProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.Clerri);
			VendorCredentials.WriteSetting(programNum,ClerriProgram.PropDashboardAddress,0,"https://dashboard.example.com/[ClientKey]");
			RefreshCaches();
			string error;
			Assert.IsNull(ClerriProgram.GetDashboardUrl(programNum,0,1,out error));
			Assert.IsNotNull(error);
			Assert.IsFalse(ClerriProgram.HasAnyClientKey(programNum));
			//An Open Dental ciphertext can't be read here: the office must enter the key again.
			DataCore.NonQ("UPDATE programproperty SET PropertyValue='odv2e$c3ludGhldGlj' WHERE ProgramNum="+programNum+" AND PropertyDesc='Client Key'");
			RefreshCaches();
			Assert.IsNull(ClerriProgram.GetDashboardUrl(programNum,0,1,out error));
			StringAssert.Contains(error,"can't be read");
			Assert.IsFalse(ClerriProgram.HasAnyClientKey(programNum));
		}

		[TestMethod]
		public void Clerri_PlainTextKeyIsAcceptedUntilTheNextSave() {
			ClerriProgram.EnsureProgram();
			RefreshCaches();
			long programNum=Programs.GetProgramNum(ProgramName.Clerri);
			VendorCredentials.WriteSetting(programNum,ClerriProgram.PropDashboardAddress,0,"https://dashboard.example.com/[ClientKey]");
			DataCore.NonQ("UPDATE programproperty SET PropertyValue='typed-key' WHERE ProgramNum="+programNum+" AND PropertyDesc='Client Key'");
			RefreshCaches();
			string error;
			Assert.AreEqual("https://dashboard.example.com/typed-key",ClerriProgram.GetDashboardUrl(programNum,0,0,out error));
			Assert.IsTrue(ClerriProgram.ReadClientKeyExact(programNum,0).NeedsReEncrypt,"The setup window encrypts it on the next save.");
		}

		[TestMethod]
		public void Clerri_BlankAddressIsAnError() {
			ClerriProgram.EnsureProgram();
			RefreshCaches();
			string error;
			Assert.IsNull(ClerriProgram.GetDashboardUrl(Programs.GetProgramNum(ProgramName.Clerri),0,1,out error));
			Assert.IsNotNull(error);
		}
		#endregion Clerri

		#region Trophy (Enhanced)
		[TestMethod]
		public void Trophy_CommandLineUsesTheRealPatientTags() {
			Patient patient=SyntheticPatients.Create();
			string arguments=TrophyEnhancedCommandLine.Build("-D[PatNum] -I[ChartNumber] -N\"[LName], [FName]\" -B[Birthdate_yyyyMMdd] -P\"[TrophyFolder]\"",
				patient,@"F:\tw\O.rvg\O0000012",(text,pat) => Patients.ReplacePatient(text,pat));
			Assert.AreEqual("-D1042 -ICH-77 -N\"O'Testa, Ann \"Annie\"\" -B19800309 -P\"F:\\tw\\O.rvg\\O0000012\"",arguments);
		}
		#endregion Trophy (Enhanced)
	}
}
