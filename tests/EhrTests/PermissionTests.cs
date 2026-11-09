// The permissions of package EHR-1 once the key gate is gone (docs/specs/ehr-rebuild-plan.md, section 5.1; owner answers E-4 and E-6): saving and
// deleting a provider row needs Setup and writes a Setup security log entry; the EHR setup window hides Provider Keys and the Code System Importer
// without Setup and keeps Emergency Now for everyone; SNOMED's One-Time Tools show for Setup whenever the snomed table has rows; Map to ICD9 needs
// ProblemDefEdit, writes a ProblemDefEdit entry with the count and signals Diseases. On the scratch database of Support/ScratchDb.cs with
// synthetic users, providers, problem definitions and SNOMED rows (made-up codes starting with TEST); the map's ICD-9 code is taken from the
// engine's own list at run time, never written here. Windows are checked in their source; none is created or shown.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace EhrTests {
	[TestClass]
	public class PermissionTests {
		private const string PASSWORD="ehr1-test-password";

		[TestCleanup]
		public void Cleanup() {
			ScratchDb.End();
		}

		private static EhrProvKey NewRow(Provider provider,int yearValue) {
			EhrProvKey ehrProvKey=new EhrProvKey();
			ehrProvKey.LName=provider.LName;
			ehrProvKey.FName=provider.FName;
			ehrProvKey.YearValue=yearValue;
			ehrProvKey.ProvKey="";
			ehrProvKey.IsNew=true;
			return ehrProvKey;
		}

		#region Provider rows (database)
		[TestMethod]
		public void SaveAndDelete_WithoutSetup_AreRefused_AndWriteNothing() {
			ScratchDb.Begin();
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			Userod userodSetup=ScratchDb.MakeUser(PASSWORD,EnumPermType.Setup);
			//Other permissions, ProblemDefEdit and EhrKeyAdd among them, are not Setup.
			Userod userodNoSetup=ScratchDb.MakeUser(PASSWORD,EnumPermType.ProblemDefEdit,EnumPermType.EhrKeyAdd,EnumPermType.EhrMeasureEventEdit);
			ScratchDb.LogOn(userodSetup,PASSWORD);
			EhrProvKey ehrProvKeySaved=NewRow(provider,25);
			Assert.AreEqual("",EhrProvKeys.SaveChecked(ehrProvKeySaved,null,out bool _));
			ScratchDb.LogOn(userodNoSetup,PASSWORD);
			long securityLogNum=ScratchDb.GetLastSecurityLogNum();
			string errorSave=EhrProvKeys.SaveChecked(NewRow(provider,26),null,out bool isPrefChangedSave);
			StringAssert.Contains(errorSave,"Not authorized");
			StringAssert.Contains(errorSave,GroupPermissions.GetDesc(EnumPermType.Setup));
			Assert.IsFalse(isPrefChangedSave);
			EhrProvKey ehrProvKeyChanged=ehrProvKeySaved.Copy();
			ehrProvKeyChanged.YearValue=27;
			StringAssert.Contains(EhrProvKeys.SaveChecked(ehrProvKeyChanged,ehrProvKeySaved,out bool _),"Not authorized");
			string errorDelete=EhrProvKeys.DeleteChecked(ehrProvKeySaved,out bool isPrefChangedDelete);
			StringAssert.Contains(errorDelete,"Not authorized");
			Assert.IsFalse(isPrefChangedDelete);
			List<EhrProvKey> listRows=EhrProvKeys.GetKeysByFLName(provider.LName,provider.FName);
			Assert.AreEqual(1,listRows.Count,"Nothing added, nothing deleted.");
			Assert.AreEqual(25,listRows[0].YearValue,"Nothing changed.");
			Assert.AreEqual(0,ScratchDb.GetSecurityLogsAfter(securityLogNum).Count,"Nothing logged.");
			//No user at all is refused too.
			Security.CurUser=null;
			StringAssert.Contains(EhrProvKeys.SaveChecked(NewRow(provider,26),null,out bool _),"Not authorized");
			StringAssert.Contains(EhrProvKeys.DeleteChecked(ehrProvKeySaved,out bool _),"Not authorized");
			Assert.AreEqual(1,EhrProvKeys.GetKeysByFLName(provider.LName,provider.FName).Count);
		}

		[TestMethod]
		public void SaveChangeAndDelete_WithSetup_WriteTheSetupSecurityLogEntry() {
			ScratchDb.Begin();
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			Userod userodSetup=ScratchDb.MakeUser(PASSWORD,EnumPermType.Setup);
			ScratchDb.LogOn(userodSetup,PASSWORD);
			string describe="'"+provider.LName+", "+provider.FName+"', year ";
			long securityLogNum=ScratchDb.GetLastSecurityLogNum();
			EhrProvKey ehrProvKey=NewRow(provider,25);
			Assert.AreEqual("",EhrProvKeys.SaveChecked(ehrProvKey,null,out bool _));
			SecurityLog securityLogAdd=ScratchDb.GetSecurityLogsAfter(securityLogNum).Single();
			Assert.AreEqual(EnumPermType.Setup,securityLogAdd.PermType);
			Assert.AreEqual(userodSetup.UserNum,securityLogAdd.UserNum);
			Assert.AreEqual(0,securityLogAdd.PatNum);
			StringAssert.StartsWith(securityLogAdd.LogText,"EHR reporting row added: "+describe+"25.");
			securityLogNum=ScratchDb.GetLastSecurityLogNum();
			EhrProvKey ehrProvKeyOld=ehrProvKey.Copy();
			ehrProvKey.YearValue=26;
			Assert.AreEqual("",EhrProvKeys.SaveChecked(ehrProvKey,ehrProvKeyOld,out bool _));
			SecurityLog securityLogChange=ScratchDb.GetSecurityLogsAfter(securityLogNum).Single();
			Assert.AreEqual(EnumPermType.Setup,securityLogChange.PermType);
			StringAssert.StartsWith(securityLogChange.LogText,"EHR reporting row changed from "+describe+"25 to "+describe+"26.");
			securityLogNum=ScratchDb.GetLastSecurityLogNum();
			Assert.AreEqual("",EhrProvKeys.DeleteChecked(ehrProvKey,out bool _));
			SecurityLog securityLogDelete=ScratchDb.GetSecurityLogsAfter(securityLogNum).Single();
			Assert.AreEqual(EnumPermType.Setup,securityLogDelete.PermType);
			StringAssert.StartsWith(securityLogDelete.LogText,"EHR reporting row deleted: "+describe+"26.");
			Assert.AreEqual(0,EhrProvKeys.GetKeysByFLName(provider.LName,provider.FName).Count);
		}

		[TestMethod]
		public void TheKeyWindow_ChecksSetupWithTheStandardMessage_BeforeSavingOrDeleting() {
			string source=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrProviderKeyEdit.cs");
			string save=CSharpSource.GetBlock(source,"private void butSave_Click(");
			int indexAuthorized=save.IndexOf("if(!Security.IsAuthorized(EnumPermType.Setup)) {",StringComparison.Ordinal);
			int indexSave=save.IndexOf("EhrProvKeys.SaveChecked(",StringComparison.Ordinal);
			Assert.IsTrue(indexAuthorized>=0 && indexAuthorized<indexSave,indexAuthorized+" "+indexSave);
			string delete=CSharpSource.GetBlock(source,"private void butDelete_Click(");
			indexAuthorized=delete.IndexOf("if(!Security.IsAuthorized(EnumPermType.Setup)) {",StringComparison.Ordinal);
			int indexConfirm=delete.IndexOf("MsgBox.Show(this,MsgBoxButtons.OKCancel,\"Delete?\")",StringComparison.Ordinal);
			int indexDelete=delete.IndexOf("EhrProvKeys.DeleteChecked(_keyCur,out bool isPrefChanged);",StringComparison.Ordinal);
			Assert.IsTrue(indexAuthorized>=0 && indexAuthorized<indexConfirm && indexConfirm<indexDelete,indexAuthorized+" "+indexConfirm+" "+indexDelete);
			//The window writes the table only through the checked methods.
			string code=CSharpSource.Read(source).CodeWithoutComments;
			foreach(string call in new[] { "EhrProvKeys.Insert(","EhrProvKeys.Update(","EhrProvKeys.Delete(","SecurityLogs.MakeLogEntry(" }) {
				Assert.IsFalse(code.Contains(call),call);
			}
			//Both checked methods begin with the Setup check, before anything is read or written.
			string business=TestPaths.ReadSource(@"OpenPmsDentalBusiness\Data Interface\EhrProvKeys.cs");
			foreach(string signature in new[] { "public static string SaveChecked(","public static string DeleteChecked(" }) {
				string block=CSharpSource.GetBlock(business,signature);
				int indexCheck=block.IndexOf("string errorAuthorization=GetSetupError();",StringComparison.Ordinal);
				int indexFirstWrite=new[] { block.IndexOf("Insert(",StringComparison.Ordinal),block.IndexOf("Delete(",StringComparison.Ordinal),
					block.IndexOf("GetSaveError(",StringComparison.Ordinal) }.Where(x => x>=0).Min();
				Assert.IsTrue(indexCheck>=0 && indexCheck<indexFirstWrite,signature+" "+indexCheck+" "+indexFirstWrite);
				StringAssert.Contains(block,"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,GetLogText(",signature);
			}
			StringAssert.Contains(CSharpSource.GetBlock(business,"private static string GetSetupError("),
				"Security.IsAuthorized(EnumPermType.Setup,DateTime.MinValue,actionNotAuthorized:x => error=x)");
		}
		#endregion Provider rows (database)

		#region The EHR setup window
		[TestMethod]
		public void TheSetupWindow_HidesProviderKeysAndTheImporterWithoutSetup_EmergencyNowStays() {
			string source=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrSetup.cs");
			string load=CSharpSource.GetBlock(source,"private void FormEhrSetup_Load(");
			string blockNoSetup=CSharpSource.GetBlock(load,"if(!Security.IsAuthorized(EnumPermType.Setup,true)) {");
			StringAssert.Contains(blockNoSetup,"butProviderKeys.Visible=false;");
			StringAssert.Contains(blockNoSetup,"butCodeImport.Visible=false;");
			//Emergency Now and its panel stay for every user, and nothing else in the window hides them.
			foreach(string name in new[] { "butEmergencyNow","panelEmergencyNow" }) {
				Assert.IsFalse(Regex.IsMatch(CSharpSource.Read(source).CodeWithoutComments,@"\b"+name+@"\.(Visible|Enabled)\s*=\s*false"),name);
				Assert.IsFalse(Regex.IsMatch(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrSetup.designer.cs"),
					@"this\."+name+@"\.(Visible|Enabled) = false"),name);
			}
			//The two buttons are hidden nowhere else, so a user with Setup sees them; the importer is in the Code Systems group, which stays.
			Assert.AreEqual(1,Regex.Matches(CSharpSource.Read(source).CodeWithoutComments,@"\bbutProviderKeys\.Visible\b").Count);
			Assert.AreEqual(1,Regex.Matches(CSharpSource.Read(source).CodeWithoutComments,@"\bbutCodeImport\.Visible\b").Count);
			Assert.IsFalse(source.Contains("groupCodeSystems.Visible"));
			//Every user may still open the window, for Emergency Now (section 5.1): the Setup menu's EHR item checks nothing.
			string menu=CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDental\Main Modules\FormOpenDental.cs"),"private void menuItemEHR_Click(");
			Assert.IsFalse(menu.Contains("IsAuthorized"),menu);
			StringAssert.Contains(menu,"new FormEhrSetup()");
		}

		[TestMethod]
		public void EhrKeyAdd_StaysUnused_AndHidden() {
			//Open Dental HQ's permission ("Only used at OD HQ"): no window checks it, and the security tree hides it (section 5.1).
			List<string> listFiles=TestPaths.ProgramSourceFiles()
				.Where(x => Regex.IsMatch(CSharpSource.Read(System.IO.File.ReadAllText(x)).CodeWithoutComments,@"IsAuthorized\(\s*EnumPermType\.EhrKeyAdd\b"))
				.ToList();
			Assert.AreEqual(0,listFiles.Count,string.Join("\r\n",listFiles));
			StringAssert.Contains(TestPaths.ReadSource(@"OpenPmsDental\User Controls\UserControlSecurityTree.cs"),"EnumPermType.EhrKeyAdd");
		}
		#endregion The EHR setup window

		#region SNOMED
		[TestMethod]
		public void SnomedTools_ShowOnlyWithSetup_AndSnomedRows() {
			Assert.IsFalse(FormSnomeds.IsSnomedToolsVisible(false,0));
			Assert.IsFalse(FormSnomeds.IsSnomedToolsVisible(false,1));
			Assert.IsFalse(FormSnomeds.IsSnomedToolsVisible(true,0));
			Assert.IsTrue(FormSnomeds.IsSnomedToolsVisible(true,1));
			Assert.IsTrue(FormSnomeds.IsSnomedToolsVisible(true,400000));
			//The window asks the rule with the user's Setup permission and the count of the snomed table, and no longer reads provider keys.
			string load=CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormSnomeds.cs"),"private void FormSnomeds_Load(");
			StringAssert.Contains(load,"groupBox1.Visible=IsSnomedToolsVisible(Security.IsAuthorized(EnumPermType.Setup,true),Snomeds.GetCodeCount());");
			foreach(string gone in new[] { "ProvKeyIsValid","EhrProvKeys","groupBox1.Visible=true" }) {
				Assert.IsFalse(load.Contains(gone),gone);
			}
			StringAssert.Contains(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormSnomeds.designer.cs"),"this.groupBox1.Visible = false;");
			//With the data: synthetic rows (made-up codes), counted as the window counts them.
			ScratchDb.Begin();
			Userod userodSetup=ScratchDb.MakeUser(PASSWORD,EnumPermType.Setup);
			Userod userodNoSetup=ScratchDb.MakeUser(PASSWORD,EnumPermType.ProblemDefEdit);
			DataCore.NonQ("DELETE FROM snomed WHERE SnomedCode LIKE 'TEST%'");
			long countBefore=Snomeds.GetCodeCount();
			List<long> listSnomedNums=new List<long>();
			try {
				foreach(string code in new[] { "TEST0001","TEST0002" }) {
					Snomed snomed=new Snomed();
					snomed.SnomedCode=code;
					snomed.Description="Synthetic concept for a test";
					listSnomedNums.Add(Snomeds.Insert(snomed));
				}
				Assert.AreEqual(countBefore+2,Snomeds.GetCodeCount());
				ScratchDb.LogOn(userodSetup,PASSWORD);
				Assert.IsTrue(FormSnomeds.IsSnomedToolsVisible(Security.IsAuthorized(EnumPermType.Setup,true),Snomeds.GetCodeCount()));
				ScratchDb.LogOn(userodNoSetup,PASSWORD);
				Assert.IsFalse(FormSnomeds.IsSnomedToolsVisible(Security.IsAuthorized(EnumPermType.Setup,true),Snomeds.GetCodeCount()),
					"ProblemDefEdit is not Setup.");
			}
			finally {
				foreach(long snomedNum in listSnomedNums) {
					Snomeds.Delete(snomedNum);
				}
			}
			Assert.AreEqual(countBefore,Snomeds.GetCodeCount());
			if(countBefore==0) {
				ScratchDb.LogOn(userodSetup,PASSWORD);
				Assert.IsFalse(FormSnomeds.IsSnomedToolsVisible(Security.IsAuthorized(EnumPermType.Setup,true),Snomeds.GetCodeCount()),
					"No rows: hidden even with Setup.");
			}
		}

		[TestMethod]
		public void MapToIcd9_WithoutProblemDefEdit_IsRefused_AndChangesNothing() {
			ScratchDb.Begin();
			KeyValuePair<string,string> pairMap=Snomeds.GetICD9toSNOMEDDictionary().First();//From the engine's own list, at run time.
			DiseaseDef diseaseDef=MakeDiseaseDef(pairMap.Key);
			Userod userodNoProblemDefEdit=ScratchDb.MakeUser(PASSWORD,EnumPermType.Setup);
			ScratchDb.LogOn(userodNoProblemDefEdit,PASSWORD);
			long securityLogNum=ScratchDb.GetLastSecurityLogNum();
			List<InvalidType> listSignals=new List<InvalidType>();
			EventHandler<ValidEventArgs> handler=(sender,e) => listSignals.AddRange(e.ITypes);
			DataValid.EventInvalid+=handler;
			try {
				int count=FormSnomeds.MapIcd9ToSnomed(out string error);
				Assert.AreEqual(-1,count);
				StringAssert.Contains(error,"Not authorized");
				StringAssert.Contains(error,GroupPermissions.GetDesc(EnumPermType.ProblemDefEdit));
			}
			finally {
				DataValid.EventInvalid-=handler;
			}
			Assert.AreEqual("",GetSnomedCode(diseaseDef.DiseaseDefNum),"Nothing mapped.");
			Assert.AreEqual(0,ScratchDb.GetSecurityLogsAfter(securityLogNum).Count,"Nothing logged.");
			Assert.AreEqual(0,listSignals.Count,"Nothing signalled.");
		}

		[TestMethod]
		public void MapToIcd9_WithProblemDefEdit_Maps_LogsTheCount_AndSignalsDiseases() {
			ScratchDb.Begin();
			Dictionary<string,string> dictionaryMap=Snomeds.GetICD9toSNOMEDDictionary();//From the engine's own list, at run time.
			KeyValuePair<string,string> pairMap=dictionaryMap.First();
			DiseaseDef diseaseDefMapped=MakeDiseaseDef(pairMap.Key);
			DiseaseDef diseaseDefUnmapped=MakeDiseaseDef("TEST999");
			Assert.IsFalse(dictionaryMap.ContainsKey("TEST999"));
			Userod userod=ScratchDb.MakeUser(PASSWORD,EnumPermType.ProblemDefEdit);
			ScratchDb.LogOn(userod,PASSWORD);
			long securityLogNum=ScratchDb.GetLastSecurityLogNum();
			List<InvalidType> listSignals=new List<InvalidType>();
			EventHandler<ValidEventArgs> handler=(sender,e) => listSignals.AddRange(e.ITypes);
			DataValid.EventInvalid+=handler;
			try {
				int count=FormSnomeds.MapIcd9ToSnomed(out string error);
				Assert.AreEqual("",error);
				Assert.IsTrue(count>=1,"At least the synthetic problem: "+count);
				Assert.AreEqual(pairMap.Value,GetSnomedCode(diseaseDefMapped.DiseaseDefNum),"Mapped from the engine's list.");
				Assert.AreEqual("",GetSnomedCode(diseaseDefUnmapped.DiseaseDefNum),"An ICD-9 code the list does not have is left alone.");
				SecurityLog securityLog=ScratchDb.GetSecurityLogsAfter(securityLogNum).Single();
				Assert.AreEqual(EnumPermType.ProblemDefEdit,securityLog.PermType);
				Assert.AreEqual(userod.UserNum,securityLog.UserNum);
				StringAssert.Contains(securityLog.LogText,"SNOMED CT codes added to "+count+" problem definitions");
				CollectionAssert.AreEqual(new[] { InvalidType.Diseases },listSignals.ToArray());
				//A second run has nothing left to map: the entry says 0, and nothing is signalled.
				listSignals.Clear();
				securityLogNum=ScratchDb.GetLastSecurityLogNum();
				Assert.AreEqual(0,FormSnomeds.MapIcd9ToSnomed(out string errorSecond));
				Assert.AreEqual("",errorSecond);
				StringAssert.Contains(ScratchDb.GetSecurityLogsAfter(securityLogNum).Single().LogText,"SNOMED CT codes added to 0 problem definitions");
				Assert.AreEqual(0,listSignals.Count);
			}
			finally {
				DataValid.EventInvalid-=handler;
			}
		}

		[TestMethod]
		public void TheMapButton_ChecksProblemDefEdit_BeforeItsConfirmation() {
			string source=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormSnomeds.cs");
			string button=CSharpSource.GetBlock(source,"private void butMapToSnomed_Click(");
			int indexAuthorized=button.IndexOf("if(!Security.IsAuthorized(EnumPermType.ProblemDefEdit)) {",StringComparison.Ordinal);
			int indexConfirm=button.IndexOf("MsgBox.Show(this,MsgBoxButtons.OKCancel,",StringComparison.Ordinal);
			int indexMap=button.IndexOf("int changeCount=MapIcd9ToSnomed(out string error);",StringComparison.Ordinal);
			Assert.IsTrue(indexAuthorized>=0 && indexAuthorized<indexConfirm && indexConfirm<indexMap,indexAuthorized+" "+indexConfirm+" "+indexMap);
			Assert.IsFalse(button.Contains("DiseaseDefs.Update("),"The button changes nothing itself.");
			string map=CSharpSource.GetBlock(source,"public static int MapIcd9ToSnomed(");
			int indexCheck=map.IndexOf("Security.IsAuthorized(EnumPermType.ProblemDefEdit,DateTime.MinValue,actionNotAuthorized:",StringComparison.Ordinal);
			int indexUpdate=map.IndexOf("DiseaseDefs.Update(",StringComparison.Ordinal);
			Assert.IsTrue(indexCheck>=0 && indexCheck<indexUpdate,indexCheck+" "+indexUpdate);
			StringAssert.Contains(map,"SecurityLogs.MakeLogEntry(EnumPermType.ProblemDefEdit,0,");
			StringAssert.Contains(map,"DataValid.SetInvalid(InvalidType.Diseases);");
			//The same permission the problem windows use.
			StringAssert.Contains(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormDiseaseDefs.cs"),"if(!Security.IsAuthorized(EnumPermType.ProblemDefEdit)) {");
		}

		///<summary>A problem definition with the ICD-9 code and no SNOMED CT code, in the database.</summary>
		private static DiseaseDef MakeDiseaseDef(string icd9Code) {
			DiseaseDef diseaseDef=new DiseaseDef();
			diseaseDef.DiseaseName=ScratchDb.UniqueName("Ehrproblem");
			diseaseDef.ICD9Code=icd9Code;
			diseaseDef.Icd10Code="";
			diseaseDef.SnomedCode="";
			diseaseDef.ItemOrder=999;
			DiseaseDefs.Insert(diseaseDef);
			return diseaseDef;
		}

		private static string GetSnomedCode(long diseaseDefNum) {
			return DataCore.GetScalar("SELECT SnomedCode FROM diseasedef WHERE DiseaseDefNum="+POut.Long(diseaseDefNum));
		}
		#endregion SNOMED
	}
}
