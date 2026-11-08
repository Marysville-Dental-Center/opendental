// SWMAP on adopted databases: two synthetic Open Dental 25.3.48 databases (the reference schema, made-up rows) adopted as the adoption tool adopts the
// office copy. The first has the payment plan lock on (payment plans included in the global lock date) and the Recession wording on, with
// other made-up values and the four V-BRIDGE1 program links; the second has the opposite values. Every setting row comes
// through adoption unchanged, and this program reads each one with Open Dental's meaning. A third database holds only a preference table with
// rows of release v0.0, which the settings step moves. No office value is used. Skipped (inconclusive) without the local MariaDB.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace SWMAPTests {
	[TestClass]
	public class AdoptedOd253Tests {
		private static readonly string DB_LOCK_ON=ScratchDb.Name("lockon");
		private static readonly string DB_LOCK_OFF=ScratchDb.Name("lockoff");
		private static readonly string DB_V0=ScratchDb.Name("v0");
		private static string _skipReason;
		private static List<(string prefName,string value)> _listPrefsBeforeOn;
		private static List<(string prefName,string value)> _listPrefsAfterOn;
		private static List<(string prefName,string value)> _listPrefsBeforeOff;
		private static List<(string prefName,string value)> _listPrefsAfterOff;
		private static AdoptionResult _adoptionResultOn;
		///<summary>A date inside the made-up global lock date (2025-12-31).</summary>
		private static readonly DateTime _dateLocked=new DateTime(2025,6,1);
		public const string SALES_TAX_CODE="Z9901";

		///<summary>The settings this program reads under Open Dental's names, with made-up values: payment plans included in the global lock,
		///Recession on.</summary>
		public static readonly (string prefName,string value)[] ArrayPrefsLockOn={
			("PayPlansIncludeInGlobalLock","1"),
			("PerioRecessionInsteadOfGM","1"),
			("ApptProvbarShowOnOps","1"),
			("ApptUseSecProvTimeIfEmpty","0"),
			("ClaimCheckForMedicalEOB","1"),
			("EraAutoProcessSplitByCarcCodes","1"),
			("BlurModuleOnLoad","1"),
			("ReplicationWriteReadDelayEnabled","1"),
			("ReplicationWriteReadDelayMS","1500"),
			("SalesTaxProcCode",SALES_TAX_CODE),
			//ODOPTS, made up.
			("PerioRedAttGing","1"),
			("DomainLoginForSigBoxes","0"),
			("ClaimUpdateFeeForSecondary","1"),
			("EFormImportAllergiesAsOther","1"),
			("EFormImportProblemsAsOther","0"),
			("InsHistPerioScalingCode",SALES_TAX_CODE),
			("StatementAutoSendEmail","1"),
			("StatementAutoSendSMS","0"),
		};

		///<summary>The opposite values. The read delay is switched off but keeps its milliseconds.</summary>
		public static readonly (string prefName,string value)[] ArrayPrefsLockOff={
			("PayPlansIncludeInGlobalLock","0"),
			("PerioRecessionInsteadOfGM","0"),
			("ApptProvbarShowOnOps","0"),
			("ApptUseSecProvTimeIfEmpty","1"),
			("ClaimCheckForMedicalEOB","0"),
			("EraAutoProcessSplitByCarcCodes","0"),
			("BlurModuleOnLoad","0"),
			("ReplicationWriteReadDelayEnabled","0"),
			("ReplicationWriteReadDelayMS","1500"),
			//ODOPTS, made up; no InsHistPerioScalingCode row.
			("PerioRedAttGing","2"),
			("DomainLoginForSigBoxes","1"),
			("ClaimUpdateFeeForSecondary","0"),
			("EFormImportAllergiesAsOther","0"),
			("EFormImportProblemsAsOther","1"),
			("StatementAutoSendEmail","0"),
			("StatementAutoSendSMS","1"),
		};

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			SetCulture();
			ScratchDb.DropStale();
			Security.CurUser=Security.CurUser??new Userod { UserNum=1,UserName="Synthetic" };
			CreateOd253(DB_LOCK_ON,ArrayPrefsLockOn,hasProgramLinks:true);
			_listPrefsBeforeOn=ScratchDb.GetPrefRows(DB_LOCK_ON);
			ScratchDb.Connect(DB_LOCK_ON);
			_adoptionResultOn=Adoption.Run();
			_listPrefsAfterOn=ScratchDb.GetPrefRows(DB_LOCK_ON);
			CreateOd253(DB_LOCK_OFF,ArrayPrefsLockOff,hasProgramLinks:false);
			_listPrefsBeforeOff=ScratchDb.GetPrefRows(DB_LOCK_OFF);
			ScratchDb.Connect(DB_LOCK_OFF);
			Adoption.Run();
			_listPrefsAfterOff=ScratchDb.GetPrefRows(DB_LOCK_OFF);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropQuietly(DB_LOCK_ON);
			ScratchDb.DropQuietly(DB_LOCK_OFF);
			ScratchDb.DropQuietly(DB_V0);
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			SetCulture();
		}

		///<summary>Lans.g returns the English text without touching the database only in en-US.</summary>
		private static void SetCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
		}

		///<summary>An Open Dental 25.3.48 database as an office copy looks to adoption: the reference schema, the version preferences, a made-up
		///global lock date that includes administrators, the given settings under Open Dental's names, every other name of the office database that is
		///not in PrefName with a made-up value, and optionally the four V-BRIDGE1 program links as Open Dental may have them.</summary>
		private static void CreateOd253(string database,(string prefName,string value)[] arrayPrefs,bool hasProgramLinks) {
			ScratchDb.CreateFromSnapshot(database,SchemaVerifier.GetReference25_3_48());
			List<(string prefName,string value)> listPrefs=new List<(string,string)> {
				("DataBaseVersion","25.3.48.0"),("ProgramVersion","25.3.48.0"),("CorruptedDatabase","0"),("UpdateInProgressOnComputerName",""),
				("RandomPrimaryKeys","0"),("DxIcdVersion","9"),
				("SecurityLockDate","2025-12-31"),("SecurityLockDays","0"),("SecurityLockIncludesAdmin","1"),
			};
			listPrefs.AddRange(arrayPrefs);
			//The names this program leaves alone, with made-up values (a web address on a test host, so adoption has no reason to blank it).
			foreach(string name in OdNames.ArrayLegacy.Concat(OdNames.ArrayDropped).Concat(OdNames.ArrayNotBuilt)) {
				listPrefs.Add((name,name=="CloudApiServiceUrl" ? "https://cloud.example.test/api" : "synthetic-"+name.Length));
			}
			ScratchDb.Exec(database,"INSERT INTO preference (PrefName,ValueString) VALUES "
				+string.Join(",",listPrefs.Select(x => "('"+POut.String(x.prefName)+"','"+POut.String(x.value)+"')")));
			ScratchDb.Exec(database,"INSERT INTO procedurecode (ProcCode,Descript,AbbrDesc) VALUES ('"+SALES_TAX_CODE+"','Synthetic sales tax','SalesTax')");
			if(!hasProgramLinks) {
				return;
			}
			ScratchDb.Exec(database,
				"INSERT INTO program (ProgramNum,ProgName,ProgDesc,Enabled,Path,CommandLine,Note) VALUES "
					+"(501,'Overjet','Overjet',1,'https://overjet.example.test/bridge','','Synthetic office note'),"
					+"(502,'DTXStudio','DTX Studio',1,'C:\\\\Synthetic\\\\DTXsync.exe','--synthetic','Synthetic office note'),"
					+"(503,'DTXStudioAcquire','DTX Studio Acquire',0,'C:\\\\Synthetic\\\\DTXsync.exe','','Synthetic office note'),"
					+"(504,'IAP','Insurance Answers Plus',1,'https://iap.example.test/login','','Synthetic office note')",
				"INSERT INTO toolbutitem (ProgramNum,ToolBar,ButtonText) VALUES (501,7,'Overjet office'),(502,3,'DTX office')",
				"INSERT INTO programproperty (ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity) VALUES "
					+"(502,'"+DtxStudioLink.PropertyDescs.BirthdateFormat+"','yyyyMMdd','',0,0,0)");
		}

		private static void ConnectAndRefresh(string database) {
			ScratchDb.Connect(database);
			Prefs.RefreshCache();
		}

		#region Adoption keeps every setting row
		[TestMethod]
		public void Adoption_KeepsEverySettingRow_UnderOpenDentalsName() {
			foreach((List<(string prefName,string value)> before,List<(string prefName,string value)> after,(string prefName,string value)[] arrayPrefs) run in new[] {
				(_listPrefsBeforeOn,_listPrefsAfterOn,ArrayPrefsLockOn),(_listPrefsBeforeOff,_listPrefsAfterOff,ArrayPrefsLockOff) })
			{
				foreach(string name in OdNames.ArrayRead.Concat(OdNames.ArrayLegacy).Concat(OdNames.ArrayDropped).Concat(OdNames.ArrayNotBuilt)) {
					List<string> listBefore=run.before.Where(x => x.prefName==name).Select(x => x.value).ToList();
					List<string> listAfter=run.after.Where(x => x.prefName==name).Select(x => x.value).ToList();
					CollectionAssert.AreEqual(listBefore,listAfter,name+": adoption keeps the row as it was.");
				}
				foreach((string prefName,string value) pref in run.arrayPrefs) {
					Assert.AreEqual(1,run.after.Count(x => x.prefName==pref.prefName && x.value==pref.value),pref.prefName);
				}
				//No row of release v0.0 appears.
				foreach(OdSettingRename rename in OdSettingNames.ListRenames) {
					Assert.IsFalse(run.after.Any(x => x.prefName==rename.PrefNameV0),rename.PrefNameV0);
				}
			}
		}

		[TestMethod]
		public void Adoption_RanTheSettingsStep_WhichChangedNothing() {
			ScratchDb.Connect(DB_LOCK_ON);
			string applied=ScratchDb.Scalar(DB_LOCK_ON,"SELECT ValueString FROM preference WHERE PrefName='ForkMigrationsApplied'");
			CollectionAssert.Contains(applied.Split(','),"2026110731");
			Assert.IsFalse(_adoptionResultOn.ListLog.Any(x => x.Contains("setting(s) of release v0.0")),string.Join("\r\n",_adoptionResultOn.ListLog));
			Assert.IsTrue(ForkVersions.ReadState().IsAdoptionRecordFinished);
		}
		#endregion Adoption keeps every setting row

		#region Adopted values, read with Open Dental's meaning
		[TestMethod]
		public void LockOn_PaymentPlansStayLocked_AndRecessionIsOn() {
			ConnectAndRefresh(DB_LOCK_ON);
			Assert.IsFalse(PayPlanGlobalLock.IsOn(),"Payment plans are included in the global lock date, so 'not affected' is not ticked.");
			Assert.IsTrue(Security.IsGlobalDateLock(EnumPermType.PayPlanEdit,_dateLocked,suppressMsgBox:true),"A payment plan inside the lock is locked.");
			Assert.IsFalse(Security.IsGlobalDateLock(EnumPermType.PayPlanEdit,new DateTime(2026,1,15),suppressMsgBox:true),"After the lock date: open.");
			Assert.IsTrue(PerioMeasures.GetUseRecessionTerm());
			Assert.AreEqual(PerioMeasures.TermRecession,PerioMeasures.GetGingMarginTerm(PerioMeasures.GetUseRecessionTerm(),hasHyperplasia:false));
		}

		[TestMethod]
		public void LockOn_TheOtherSettings_ReadAsStored() {
			ConnectAndRefresh(DB_LOCK_ON);
			Assert.IsTrue(ApptTimeBars.IsProvTimeOnOpBackground());
			Assert.IsFalse(ApptTimeBars.IsSecondaryColorNoProvTime());
			Assert.IsTrue(ClaimAttaches.IsMedicalEobPromptEnabled());
			Assert.IsTrue(EraAutoSplit.IsEnabled());
			Assert.IsTrue(ModuleSwitchBlur.IsEnabled());
			Assert.AreEqual(1500,ReplicationDelay.GetReadDelayMs());
			Assert.AreEqual(SALES_TAX_CODE,SalesTaxProcs.GetProcCodeSetting());
			Assert.AreEqual(0,ReplicationDelay.GetSignalLookbackMs(),"This program's own look-back has no Open Dental row: its default.");
			//ODOPTS: the eight options read the adopted rows as they are.
			Assert.AreEqual(1,PerioMeasures.GetRedAttGing());
			Assert.IsFalse(DomainLogin.IsForSigBoxesSettingOn());
			Assert.IsTrue(ClaimFeeForSecondary.IsEnabled());
			Assert.IsTrue(EFormImport.IsAllergiesAsOther());
			Assert.IsFalse(EFormImport.IsProblemsAsOther());
			Assert.AreEqual(SALES_TAX_CODE,InsHistCategories.GetPerioScalingCodeSetting());
			Assert.AreEqual(StatementAutoSendMode.Ask,StatementAutoSend.GetEmailMode());
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetSmsMode());
		}

		[TestMethod]
		public void LockOff_PaymentPlansAreNotAffected_AndRecessionIsOff() {
			ConnectAndRefresh(DB_LOCK_OFF);
			Assert.IsTrue(PayPlanGlobalLock.IsOn(),"Payment plans are not included in the global lock date, so 'not affected' is ticked.");
			Assert.IsFalse(Security.IsGlobalDateLock(EnumPermType.PayPlanEdit,_dateLocked,suppressMsgBox:true),"A payment plan inside the lock stays open.");
			Assert.IsTrue(Security.IsGlobalDateLock(EnumPermType.PaymentEdit,_dateLocked,suppressMsgBox:true),"Payments stay locked.");
			Assert.IsFalse(PerioMeasures.GetUseRecessionTerm());
			Assert.AreEqual(PerioMeasures.TermGingMarg,PerioMeasures.GetGingMarginTerm(PerioMeasures.GetUseRecessionTerm(),hasHyperplasia:false));
		}

		[TestMethod]
		public void LockOff_TheOtherSettings_ReadAsStored() {
			ConnectAndRefresh(DB_LOCK_OFF);
			Assert.IsFalse(ApptTimeBars.IsProvTimeOnOpBackground());
			Assert.IsTrue(ApptTimeBars.IsSecondaryColorNoProvTime());
			Assert.IsFalse(ClaimAttaches.IsMedicalEobPromptEnabled());
			Assert.IsFalse(EraAutoSplit.IsEnabled());
			Assert.IsFalse(ModuleSwitchBlur.IsEnabled());
			Assert.AreEqual(0,ReplicationDelay.GetReadDelayMs(),"Enabled 0: no delay, although the milliseconds are kept.");
			Assert.AreEqual("",SalesTaxProcs.GetProcCodeSetting(),"No row: not set.");
			//ODOPTS
			Assert.AreEqual(2,PerioMeasures.GetRedAttGing());
			Assert.IsTrue(DomainLogin.IsForSigBoxesSettingOn());
			Assert.IsFalse(ClaimFeeForSecondary.IsEnabled());
			Assert.IsFalse(EFormImport.IsAllergiesAsOther());
			Assert.IsTrue(EFormImport.IsProblemsAsOther());
			Assert.AreEqual("",InsHistCategories.GetPerioScalingCodeSetting(),"No row: blank.");
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetEmailMode());
			Assert.AreEqual(StatementAutoSendMode.Ask,StatementAutoSend.GetSmsMode());
		}

		[TestMethod]
		public void Saving_WritesOpenDentalsRows_WithOpenDentalsMeaning() {
			ConnectAndRefresh(DB_LOCK_OFF);
			try {
				Assert.IsTrue(PayPlanGlobalLock.Save(false));
				Assert.AreEqual("1",ScratchDb.Scalar(DB_LOCK_OFF,"SELECT ValueString FROM preference WHERE PrefName='PayPlansIncludeInGlobalLock'"),
					"Unticking 'not affected' stores Open Dental's 1: included in the lock.");
				Assert.IsTrue(ReplicationDelay.SaveSettings(800,0));
				Assert.AreEqual("1",ScratchDb.Scalar(DB_LOCK_OFF,"SELECT ValueString FROM preference WHERE PrefName='ReplicationWriteReadDelayEnabled'"));
				Assert.AreEqual("800",ScratchDb.Scalar(DB_LOCK_OFF,"SELECT ValueString FROM preference WHERE PrefName='ReplicationWriteReadDelayMS'"));
				Assert.IsTrue(ReplicationDelay.SaveSettings(0,0));
				Assert.AreEqual("0",ScratchDb.Scalar(DB_LOCK_OFF,"SELECT ValueString FROM preference WHERE PrefName='ReplicationWriteReadDelayEnabled'"));
				Assert.AreEqual("800",ScratchDb.Scalar(DB_LOCK_OFF,"SELECT ValueString FROM preference WHERE PrefName='ReplicationWriteReadDelayMS'"),
					"Turning the delay off keeps the milliseconds.");
				Assert.AreEqual(0,ReplicationDelay.GetReadDelayMs());
			}
			finally {
				//Back to the values the other tests of this database read.
				ScratchDb.Exec(DB_LOCK_OFF,"UPDATE preference SET ValueString='0' WHERE PrefName='PayPlansIncludeInGlobalLock'",
					"UPDATE preference SET ValueString='1500' WHERE PrefName='ReplicationWriteReadDelayMS'",
					"UPDATE preference SET ValueString='0' WHERE PrefName='ReplicationWriteReadDelayEnabled'");
				Prefs.RefreshCache();
			}
		}
		#endregion Adopted values, read with Open Dental's meaning

		#region Program links (V-BRIDGE1)
		[TestMethod]
		public void ProgramLinks_ReuseTheOfficeRows_NoSecondLink() {
			ConnectAndRefresh(DB_LOCK_ON);
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
			ToolButItems.RefreshCache();
			OverjetLink.EnsureProgram();
			DtxStudioLink.EnsureProgram(ProgramName.DTXStudio);
			DtxStudioLink.EnsureProgram(ProgramName.DTXStudioAcquire);
			IapLink.EnsureProgram();
			foreach((string progName,long programNum,string enabled,string path) row in new[] {
				("Overjet",501L,"1","https://overjet.example.test/bridge"),
				("DTXStudio",502L,"1",@"C:\Synthetic\DTXsync.exe"),
				("DTXStudioAcquire",503L,"0",@"C:\Synthetic\DTXsync.exe"),
				("IAP",504L,"1","https://iap.example.test/login") })
			{
				Assert.AreEqual("1",ScratchDb.Scalar(DB_LOCK_ON,"SELECT COUNT(*) FROM program WHERE ProgName='"+row.progName+"'"),row.progName+": no second link.");
				Assert.AreEqual(row.programNum.ToString(),ScratchDb.Scalar(DB_LOCK_ON,"SELECT ProgramNum FROM program WHERE ProgName='"+row.progName+"'"));
				Assert.AreEqual(row.enabled,ScratchDb.Scalar(DB_LOCK_ON,"SELECT Enabled FROM program WHERE ProgramNum="+row.programNum),row.progName);
				Assert.AreEqual(row.path,ScratchDb.Scalar(DB_LOCK_ON,"SELECT Path FROM program WHERE ProgramNum="+row.programNum),row.progName);
				Assert.AreEqual("Synthetic office note",ScratchDb.Scalar(DB_LOCK_ON,"SELECT Note FROM program WHERE ProgramNum="+row.programNum),row.progName);
			}
			Assert.AreEqual("--synthetic",ScratchDb.Scalar(DB_LOCK_ON,"SELECT CommandLine FROM program WHERE ProgramNum=502"));
			//The office's toolbar buttons only: no button is added to a row that already exists.
			Assert.AreEqual("2",ScratchDb.Scalar(DB_LOCK_ON,"SELECT COUNT(*) FROM toolbutitem WHERE ProgramNum IN (501,502,503,504)"));
			//The office's own property keeps its value; only missing properties are added.
			Assert.AreEqual("yyyyMMdd",ScratchDb.Scalar(DB_LOCK_ON,"SELECT PropertyValue FROM programproperty WHERE ProgramNum=502 AND PropertyDesc='"
				+DtxStudioLink.PropertyDescs.BirthdateFormat+"'"));
			Assert.AreEqual("1",ScratchDb.Scalar(DB_LOCK_ON,"SELECT COUNT(*) FROM programproperty WHERE ProgramNum=502 AND PropertyDesc='"
				+DtxStudioLink.PropertyDescs.BirthdateFormat+"'"));
			Assert.AreEqual("1",ScratchDb.Scalar(DB_LOCK_ON,"SELECT COUNT(*) FROM programproperty WHERE ProgramNum=502 AND PropertyDesc='"
				+DtxStudioLink.PropertyDescs.PatientArguments+"'"),"The missing property is added to the office's row.");
			Assert.AreEqual(501,Programs.GetCur(ProgramName.Overjet).ProgramNum,"The program finds the office's row by its name.");
		}
		#endregion Program links (V-BRIDGE1)

		#region A database release v0.0 used
		[TestMethod]
		public void V0Rows_AreMovedByTheStep_ThenNothingIsLeftToDo() {
			ScratchDb.CreateFromSnapshot(DB_V0,SchemaVerifier.GetReference(),"preference");
			ScratchDb.Exec(DB_V0,"INSERT INTO preference (PrefName,ValueString) VALUES "
				//Rows v0.0 wrote under its own names, made up.
				+"('ForkApptProvTimeBarsOnOpBackground','1'),('ForkApptTimeBarSecondaryColorNoProvTime','0'),"
				+"('ForkClaimMedReceivedPromptForMedicalEob','1'),('ForkEraAutoSplitCarcDenials','1'),('SecurityLockExcludesPayPlans','1'),"
				+"('PerioUseRecessionTerm','1'),('ForkReplicationReadDelayMs','2000'),('ForkModuleSwitchBlur','0'),"
				//Open Dental rows already there (a database v0.0 adopted): the v0.0 row wins.
				+"('PerioRecessionInsteadOfGM','0'),('PayPlansIncludeInGlobalLock','1'),('ReplicationWriteReadDelayMS','100'),"
				//This program's own rows stay.
				+"('ForkReplicationSignalLookbackMs','3000'),('ForkWikiReadOnlyAutoExport','1')");
			ScratchDb.Connect(DB_V0);
			ForkMigrationResult result=ForkMigrationRunner.Run(new ForkMigrationOptions { ListSteps=new List<ForkMigrationStep> { new OdSettingNamesStep() } });
			Assert.AreEqual(1,result.ListStepsChanged.Count,string.Join("\r\n",result.ListChanges));
			Dictionary<string,string> dictRows=ScratchDb.GetPrefRows(DB_V0).ToDictionary(x => x.prefName,x => x.value);
			foreach(OdSettingRename rename in OdSettingNames.ListRenames) {
				Assert.IsFalse(dictRows.ContainsKey(rename.PrefNameV0),rename.PrefNameV0+" is gone.");
			}
			Assert.AreEqual("1",dictRows["ApptProvbarShowOnOps"]);
			Assert.IsFalse(dictRows.ContainsKey("ApptUseSecProvTimeIfEmpty"),"Off, like a missing row: not inserted.");
			Assert.AreEqual("1",dictRows["ClaimCheckForMedicalEOB"]);
			Assert.AreEqual("1",dictRows["EraAutoProcessSplitByCarcCodes"]);
			Assert.AreEqual("0",dictRows["PayPlansIncludeInGlobalLock"],"v0.0 'not affected' on: Open Dental's 'included' off.");
			Assert.AreEqual("1",dictRows["PerioRecessionInsteadOfGM"]);
			Assert.AreEqual("1",dictRows["ReplicationWriteReadDelayEnabled"]);
			Assert.AreEqual("2000",dictRows["ReplicationWriteReadDelayMS"]);
			Assert.IsFalse(dictRows.ContainsKey("BlurModuleOnLoad"));
			Assert.AreEqual("3000",dictRows["ForkReplicationSignalLookbackMs"]);
			Assert.AreEqual("1",dictRows["ForkWikiReadOnlyAutoExport"]);
			//The program reads the values v0.0 used.
			Prefs.RefreshCache();
			Assert.IsTrue(ApptTimeBars.IsProvTimeOnOpBackground());
			Assert.IsFalse(ApptTimeBars.IsSecondaryColorNoProvTime());
			Assert.IsTrue(ClaimAttaches.IsMedicalEobPromptEnabled());
			Assert.IsTrue(EraAutoSplit.IsEnabled());
			Assert.IsTrue(PayPlanGlobalLock.IsOn());
			Assert.IsTrue(PerioMeasures.GetUseRecessionTerm());
			Assert.AreEqual(2000,ReplicationDelay.GetReadDelayMs());
			Assert.AreEqual(3000,ReplicationDelay.GetSignalLookbackMs());
			Assert.IsFalse(ModuleSwitchBlur.IsEnabled());
			//Run again (as a rerun of every step would): nothing left to do.
			List<(string prefName,string value)> listBefore=ScratchDb.GetPrefRows(DB_V0).Where(x => !x.prefName.StartsWith("ForkMigration")
				&& x.prefName!="ForkSchemaLevel").ToList();
			ForkMigrationResult resultAgain=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true,
				ListSteps=new List<ForkMigrationStep> { new OdSettingNamesStep() } });
			Assert.AreEqual(0,resultAgain.ListStepsChanged.Count,string.Join("\r\n",resultAgain.ListChanges));
			CollectionAssert.AreEqual(listBefore,ScratchDb.GetPrefRows(DB_V0).Where(x => !x.prefName.StartsWith("ForkMigration")
				&& x.prefName!="ForkSchemaLevel").ToList());
		}
		#endregion A database release v0.0 used
	}
}
