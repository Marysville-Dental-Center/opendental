// The provider row rule of package EHR-1 (docs/specs/ehr-rebuild-plan.md, section 5; owner answer E-4): this open build has no licence keys, a
// provider "reports EHR" when an ehrprovkey row has their last and first name, and the key text is optional and never checked. On the scratch
// database of Support/ScratchDb.cs (synthetic providers with made-up names): blank names, a name no provider has, the two-digit year and an empty
// key, the names compared as the measure queries compare them, the radiology alert preference, the EHR window's note, and HasProvHadKey through
// the Middle Tier mock. Source checks: the windows no longer ask for a valid key, and the two deliberate removals (the dead butPatList14 handler
// and the quarterly-key stub, E-5) are gone.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace EhrTests {
	[TestClass]
	public class ProvKeyRuleTests {
		private const string PASSWORD="ehr1-test-password";

		[TestCleanup]
		public void Cleanup() {
			ScratchDb.End();
		}

		private static EhrProvKey NewRow(string lName,string fName,int yearValue,string provKey) {
			EhrProvKey ehrProvKey=new EhrProvKey();
			ehrProvKey.LName=lName;
			ehrProvKey.FName=fName;
			ehrProvKey.YearValue=yearValue;
			ehrProvKey.ProvKey=provKey;
			ehrProvKey.IsNew=true;
			return ehrProvKey;
		}

		#region The rule (database)
		[TestMethod]
		public void GetSaveError_BlankNames_AreRefused() {
			ScratchDb.Begin();
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			foreach(string[] arrayNames in new[] { new[] { "",provider.FName },new[] { provider.LName,"" },new[] { "  ",provider.FName },
				new[] { provider.LName,"\t" },new[] { null,provider.FName },new[] { provider.LName,null },new[] { "","" } })
			{
				string error=EhrProvKeys.GetSaveError(arrayNames[0],arrayNames[1],26);
				StringAssert.Contains(error,"last name and first name","'"+arrayNames[0]+"' '"+arrayNames[1]+"'");
			}
			Assert.AreEqual("",EhrProvKeys.GetSaveError(provider.LName,provider.FName,26));
		}

		[TestMethod]
		public void GetSaveError_ANameNoProviderHas_IsRefused_AndTheMessageNamesIt() {
			ScratchDb.Begin();
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			string fNameOther=ScratchDb.UniqueName("Ehrother");
			string error=EhrProvKeys.GetSaveError(provider.LName,fNameOther,26);
			StringAssert.Contains(error,"No provider has the last name '"+provider.LName+"' and the first name '"+fNameOther+"'");
			StringAssert.Contains(error,"exactly as they are in the provider's record");
			string lNameOther=ScratchDb.UniqueName("Ehrnobody");
			StringAssert.Contains(EhrProvKeys.GetSaveError(lNameOther,provider.FName,26),"'"+lNameOther+"'");
			//The first name of one provider and the last name of another are not a provider.
			Provider providerSecond=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			Assert.AreNotEqual("",EhrProvKeys.GetSaveError(provider.LName,providerSecond.FName,26));
			Assert.AreEqual("",EhrProvKeys.GetSaveError(providerSecond.LName,providerSecond.FName,26));
			//A name with an apostrophe is escaped, not refused.
			Provider providerApostrophe=ScratchDb.MakeProvider("O'"+ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			Assert.AreEqual("",EhrProvKeys.GetSaveError(providerApostrophe.LName,providerApostrophe.FName,26));
		}

		[TestMethod]
		public void GetSaveError_TheYear_HasTwoDigits() {
			ScratchDb.Begin();
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			foreach(int year in new[] { 0,9,12,26,99 }) {
				Assert.AreEqual("",EhrProvKeys.GetSaveError(provider.LName,provider.FName,year),year.ToString());
			}
			foreach(int year in new[] { -1,100,2026,int.MinValue,int.MaxValue }) {
				StringAssert.Contains(EhrProvKeys.GetSaveError(provider.LName,provider.FName,year),"two digits",year.ToString());
			}
			//The window keeps its own checks of the year box (blank, and the 0 to 99 of its ValidNum) before the rule.
			string save=CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrProviderKeyEdit.cs"),"private void butSave_Click(");
			StringAssert.Contains(save,"if(textYear.Text==\"\"){");
			StringAssert.Contains(save,"if(!textYear.IsValid()) {");
			string designer=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrProviderKeyEdit.designer.cs");
			StringAssert.Contains(designer,"this.textYear.MaxVal = 99;");
			StringAssert.Contains(designer,"this.textYear.MinVal = 0;");
		}

		[TestMethod]
		public void GetSaveError_ComparesTheNames_AsTheMeasureQueriesDo() {
			//EhrMeasures.GetTable finds a provider's rows with "pv.LName=epk.LName AND pv.FName=epk.FName" in the database. A row the rule accepts must
			//be a row that join finds, and a row the rule refuses must be one it would not find.
			ScratchDb.Begin();
			StringAssert.Contains(TestPaths.ReadSource(@"OpenPmsDentalBusiness\Data Interface\EhrMeasures.cs"),
				"FROM provider pv, ehrprovkey epk WHERE pv.LName=epk.LName AND pv.FName=epk.FName ");
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			List<string[]> listVariants=new List<string[]> {
				new[] { provider.LName,provider.FName },
				new[] { provider.LName.ToUpperInvariant(),provider.FName.ToLowerInvariant() },
				new[] { provider.LName+" ",provider.FName },
				new[] { provider.LName+"x",provider.FName },
				new[] { provider.FName,provider.LName },
			};
			foreach(string[] arrayNames in listVariants) {
				DataCore.NonQ("DELETE FROM ehrprovkey");
				EhrProvKeys.Insert(NewRow(arrayNames[0],arrayNames[1],26,""));
				string countJoin=DataCore.GetScalar("SELECT COUNT(*) FROM provider pv, ehrprovkey epk WHERE pv.LName=epk.LName AND pv.FName=epk.FName "
					+"AND pv.ProvNum="+POut.Long(provider.ProvNum));
				bool isAccepted=EhrProvKeys.GetSaveError(arrayNames[0],arrayNames[1],26)=="";
				Assert.AreEqual(countJoin!="0",isAccepted,"'"+arrayNames[0]+"' '"+arrayNames[1]+"': the join found "+countJoin);
			}
			DataCore.NonQ("DELETE FROM ehrprovkey");
		}

		[TestMethod]
		public void SaveChecked_AnEmptyKey_IsAccepted_AndAnyKeyIsStoredAsTyped() {
			ScratchDb.Begin();
			Userod userod=ScratchDb.MakeUser(PASSWORD,EnumPermType.Setup);
			ScratchDb.LogOn(userod,PASSWORD);
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			foreach(string provKey in new[] { "",null," any text, never checked ","NOT-A-KEY" }) {
				EhrProvKey ehrProvKey=NewRow(provider.LName,provider.FName,26,provKey);
				Assert.AreEqual("",EhrProvKeys.SaveChecked(ehrProvKey,null,out bool isPrefChanged),"'"+provKey+"'");
				Assert.IsFalse(ehrProvKey.IsNew,"A saved row is not inserted twice.");
				EhrProvKey ehrProvKeyDb=EhrProvKeys.GetAllKeys().Single(x => x.EhrProvKeyNum==ehrProvKey.EhrProvKeyNum);
				Assert.AreEqual(provKey??"",ehrProvKeyDb.ProvKey,"Stored as typed.");
				Assert.AreEqual(provider.LName,ehrProvKeyDb.LName);
				Assert.AreEqual(provider.FName,ehrProvKeyDb.FName);
				Assert.AreEqual(26,ehrProvKeyDb.YearValue);
			}
			//A refused row is not written.
			string lNameNobody=ScratchDb.UniqueName("Ehrnobody");
			Assert.AreNotEqual("",EhrProvKeys.SaveChecked(NewRow(lNameNobody,provider.FName,26,""),null,out bool _));
			Assert.AreEqual(0,EhrProvKeys.GetKeysByFLName(lNameNobody,provider.FName).Count);
		}

		[TestMethod]
		public void SaveChecked_AChangedRow_IsUpdated() {
			ScratchDb.Begin();
			Userod userod=ScratchDb.MakeUser(PASSWORD,EnumPermType.Setup);
			ScratchDb.LogOn(userod,PASSWORD);
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			Provider providerSecond=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			EhrProvKey ehrProvKey=NewRow(provider.LName,provider.FName,25,"");
			Assert.AreEqual("",EhrProvKeys.SaveChecked(ehrProvKey,null,out bool _));
			EhrProvKey ehrProvKeyOld=ehrProvKey.Copy();
			ehrProvKey.LName=providerSecond.LName;
			ehrProvKey.FName=providerSecond.FName;
			ehrProvKey.YearValue=26;
			Assert.AreEqual("",EhrProvKeys.SaveChecked(ehrProvKey,ehrProvKeyOld,out bool _));
			Assert.AreEqual(0,EhrProvKeys.GetKeysByFLName(provider.LName,provider.FName).Count);
			EhrProvKey ehrProvKeyDb=EhrProvKeys.GetKeysByFLName(providerSecond.LName,providerSecond.FName).Single();
			Assert.AreEqual(ehrProvKey.EhrProvKeyNum,ehrProvKeyDb.EhrProvKeyNum,"Updated, not inserted.");
			Assert.AreEqual(26,ehrProvKeyDb.YearValue);
			//A change to a name no provider has is refused and leaves the row as it was.
			EhrProvKey ehrProvKeyBad=ehrProvKey.Copy();
			ehrProvKeyBad.FName=ScratchDb.UniqueName("Ehrnobody");
			Assert.AreNotEqual("",EhrProvKeys.SaveChecked(ehrProvKeyBad,ehrProvKey,out bool _));
			Assert.AreEqual(providerSecond.FName,EhrProvKeys.GetAllKeys().Single(x => x.EhrProvKeyNum==ehrProvKey.EhrProvKeyNum).FName);
		}

		[TestMethod]
		public void RadiologyAlert_TheFirstRowTurnsItOn_DeletingTheLastTurnsItOff() {
			//As Open Dental did (FormEhrProviderKeyEdit before EHR-1), now in SaveChecked and DeleteChecked, with the change in the security log entry.
			ScratchDb.Begin();
			Userod userod=ScratchDb.MakeUser(PASSWORD,EnumPermType.Setup);
			ScratchDb.LogOn(userod,PASSWORD);
			DataCore.NonQ("DELETE FROM ehrprovkey");
			Prefs.UpdateBool(PrefName.IsAlertRadiologyProcsEnabled,false);
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			try {
				long securityLogNum=ScratchDb.GetLastSecurityLogNum();
				EhrProvKey ehrProvKeyFirst=NewRow(provider.LName,provider.FName,25,"");
				Assert.AreEqual("",EhrProvKeys.SaveChecked(ehrProvKeyFirst,null,out bool isPrefChangedFirst));
				Assert.IsTrue(isPrefChangedFirst,"The first row turns the alert on.");
				Assert.IsTrue(PrefC.GetBool(PrefName.IsAlertRadiologyProcsEnabled));
				StringAssert.Contains(ScratchDb.GetSecurityLogsAfter(securityLogNum).Single().LogText,"(IsAlertRadiologyProcsEnabled) was turned on.");
				securityLogNum=ScratchDb.GetLastSecurityLogNum();
				EhrProvKey ehrProvKeySecond=NewRow(provider.LName,provider.FName,26,"");
				Assert.AreEqual("",EhrProvKeys.SaveChecked(ehrProvKeySecond,null,out bool isPrefChangedSecond));
				Assert.IsFalse(isPrefChangedSecond);
				Assert.IsFalse(ScratchDb.GetSecurityLogsAfter(securityLogNum).Single().LogText.Contains("radiology"));
				Assert.AreEqual("",EhrProvKeys.DeleteChecked(ehrProvKeyFirst,out bool isPrefChangedDelete));
				Assert.IsFalse(isPrefChangedDelete,"A row is left.");
				Assert.IsTrue(PrefC.GetBool(PrefName.IsAlertRadiologyProcsEnabled));
				securityLogNum=ScratchDb.GetLastSecurityLogNum();
				Assert.AreEqual("",EhrProvKeys.DeleteChecked(ehrProvKeySecond,out bool isPrefChangedLast));
				Assert.IsTrue(isPrefChangedLast,"Deleting the last row turns the alert off.");
				Assert.IsFalse(PrefC.GetBool(PrefName.IsAlertRadiologyProcsEnabled));
				StringAssert.Contains(ScratchDb.GetSecurityLogsAfter(securityLogNum).Single().LogText,"(IsAlertRadiologyProcsEnabled) was turned off.");
				//The window signals Prefs exactly when the preference changed.
				string source=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrProviderKeyEdit.cs");
				foreach(string signature in new[] { "private void butSave_Click(","private void butDelete_Click(" }) {
					string block=CSharpSource.GetBlock(source,signature);
					Assert.IsTrue(Regex.IsMatch(block,@"if\(isPrefChanged\)\s*\{\s*DataValid\.SetInvalid\(InvalidType\.Prefs\);"),
						signature+": the Prefs signal is inside if(isPrefChanged), whatever the file's line ends.");
					Assert.AreEqual(1,Regex.Matches(block,@"DataValid\.SetInvalid\(").Count,signature);
					Assert.IsFalse(block.Contains("Prefs.UpdateBool"),signature+": the preference is written by EhrProvKeys only.");
				}
			}
			finally {
				Prefs.UpdateBool(PrefName.IsAlertRadiologyProcsEnabled,false);
				DataCore.NonQ("DELETE FROM ehrprovkey");
			}
		}
		#endregion The rule (database)

		#region The EHR window's note
		[TestMethod]
		public void ReportingNote_SaysNotMarked_OnlyWhenNoRowHasTheNames() {
			ScratchDb.Begin();
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			Assert.AreEqual(" (not marked for EHR reporting)",EhrProvKeys.GetReportingNote(provider.LName,provider.FName));
			EhrProvKeys.Insert(NewRow(provider.LName,provider.FName,7,""));
			Assert.AreEqual("",EhrProvKeys.GetReportingNote(provider.LName,provider.FName),"A row of any year, with no key text.");
			Assert.AreEqual(" (not marked for EHR reporting)",EhrProvKeys.GetReportingNote(provider.LName,ScratchDb.UniqueName("Ehrother")));
			Assert.AreEqual(" (not marked for EHR reporting)",EhrProvKeys.GetReportingNote("",""),"Blank names have no row.");
			//Both labels of the EHR window use it; the old "(no ehr provider key entered)" is gone.
			string source=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEHR.cs");
			string shown=CSharpSource.GetBlock(source,"private void FormEHR_Shown(");
			StringAssert.Contains(shown,"labelProvPat.Text=_provider.GetLongDesc()+EhrProvKeys.GetReportingNote(_provider.LName,_provider.FName);");
			StringAssert.Contains(shown,"labelProvUser.Text=Providers.GetLongDesc(provider.ProvNum)+EhrProvKeys.GetReportingNote(provider.LName,provider.FName);");
			//(A commented-out message box of Open Dental's below the labels still quotes the old words; comments tell the history.)
			Assert.IsFalse(CSharpSource.Read(source).CodeWithoutComments.IndexOf("no ehr provider key entered",StringComparison.OrdinalIgnoreCase)>=0);
			Assert.IsFalse(Regex.IsMatch(CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDentalBusiness\Data Interface\EhrProvKeys.cs"),
				"public static string GetReportingNote("),@"\.ProvKey\b"),"The note never reads the key text.");
		}
		#endregion The EHR window's note

		#region Middle Tier
		[TestMethod]
		public void HasProvHadKey_ThroughTheMiddleTierMock() {
			//HasProvHadKey (the test the CQM windows will use, EHR-5) and GetSaveError run their queries through Db, which refuses SQL on a Middle Tier
			//client, so right answers here came back from DtoProcessor on the mock server's thread: arguments and results serialized both ways.
			ScratchDb.Begin();
			Userod userod=ScratchDb.MakeUser(PASSWORD,EnumPermType.Setup);
			Provider provider=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			Provider providerApostrophe=ScratchDb.MakeProvider("O'"+ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			Provider providerNoRow=ScratchDb.MakeProvider(ScratchDb.UniqueName("Ehrlast"),ScratchDb.UniqueName("Ehrfirst"));
			EhrProvKeys.Insert(NewRow(provider.LName,provider.FName,99,""));
			EhrProvKeys.Insert(NewRow(providerApostrophe.LName,providerApostrophe.FName,3,"text"));
			string fNameNobody=ScratchDb.UniqueName("Ehrnobody");
			string errorDirect=EhrProvKeys.GetSaveError(provider.LName,fNameNobody,26);
			ScratchDb.LogOn(userod,PASSWORD);
			ScratchDb.UseMockMiddleTier();
			try {
				Assert.AreEqual(MiddleTierRole.ClientMT,RemotingClient.MiddleTierRole);
				Assert.IsTrue(EhrProvKeys.HasProvHadKey(provider.LName,provider.FName),"A row of any year.");
				Assert.IsTrue(EhrProvKeys.HasProvHadKey(providerApostrophe.LName,providerApostrophe.FName),"An apostrophe crosses the Middle Tier.");
				Assert.IsFalse(EhrProvKeys.HasProvHadKey(providerNoRow.LName,providerNoRow.FName));
				Assert.IsFalse(EhrProvKeys.HasProvHadKey(provider.LName,fNameNobody));
				Assert.AreEqual("",EhrProvKeys.GetSaveError(provider.LName,provider.FName,26));
				Assert.AreEqual(errorDirect,EhrProvKeys.GetSaveError(provider.LName,fNameNobody,26));
				Assert.AreEqual("",EhrProvKeys.GetReportingNote(provider.LName,provider.FName));
				Assert.AreEqual(" (not marked for EHR reporting)",EhrProvKeys.GetReportingNote(providerNoRow.LName,providerNoRow.FName));
				//Saving through the Middle Tier: every call SaveChecked makes is a Middle Tier call of its own.
				long securityLogNum;
				ScratchDb.UseDirectConnection();
				securityLogNum=ScratchDb.GetLastSecurityLogNum();
				ScratchDb.UseMockMiddleTier();
				EhrProvKey ehrProvKey=NewRow(providerNoRow.LName,providerNoRow.FName,26,"");
				Assert.AreEqual("",EhrProvKeys.SaveChecked(ehrProvKey,null,out bool _));
				Assert.IsTrue(EhrProvKeys.HasProvHadKey(providerNoRow.LName,providerNoRow.FName));
				Assert.AreEqual(MiddleTierRole.ClientMT,RemotingClient.MiddleTierRole,"The mock puts the client's role back after each call.");
				ScratchDb.UseDirectConnection();
				SecurityLog securityLog=ScratchDb.GetSecurityLogsAfter(securityLogNum).Single();
				Assert.AreEqual(EnumPermType.Setup,securityLog.PermType);
				Assert.AreEqual(userod.UserNum,securityLog.UserNum);
				StringAssert.Contains(securityLog.LogText,"'"+providerNoRow.LName+", "+providerNoRow.FName+"', year 26");
			}
			finally {
				ScratchDb.UseDirectConnection();
			}
			Assert.IsTrue(EhrProvKeys.HasProvHadKey(provider.LName,provider.FName),"The same answer directly.");
		}
		#endregion Middle Tier

		#region Pure and source checks
		[TestMethod]
		public void GetLogText_GivesTheNamesAndYear_AndThePreferenceChange_NeverTheKey() {
			EhrProvKey ehrProvKeyOld=NewRow("Ehrlast","Ehrfirst",5,"SECRET-LOOKING-TEXT");
			EhrProvKey ehrProvKeyNew=NewRow("Ehrlast","Ehrother",26,"SECRET-LOOKING-TEXT");
			Assert.AreEqual("EHR reporting row added: 'Ehrlast, Ehrfirst', year 05.",EhrProvKeys.GetLogText(null,ehrProvKeyOld,null));
			Assert.AreEqual("EHR reporting row added: 'Ehrlast, Ehrfirst', year 05. The radiology procedures alert (IsAlertRadiologyProcsEnabled) was "
				+"turned on.",EhrProvKeys.GetLogText(null,ehrProvKeyOld,true));
			Assert.AreEqual("EHR reporting row changed from 'Ehrlast, Ehrfirst', year 05 to 'Ehrlast, Ehrother', year 26.",
				EhrProvKeys.GetLogText(ehrProvKeyOld,ehrProvKeyNew,null));
			Assert.AreEqual("EHR reporting row saved, names and year unchanged: 'Ehrlast, Ehrfirst', year 05.",
				EhrProvKeys.GetLogText(ehrProvKeyOld,ehrProvKeyOld.Copy(),null));
			Assert.AreEqual("EHR reporting row deleted: 'Ehrlast, Ehrother', year 26. The radiology procedures alert (IsAlertRadiologyProcsEnabled) was "
				+"turned off.",EhrProvKeys.GetLogText(ehrProvKeyNew,null,false));
			foreach(string text in new[] { EhrProvKeys.GetLogText(null,ehrProvKeyOld,true),EhrProvKeys.GetLogText(ehrProvKeyOld,ehrProvKeyNew,null),
				EhrProvKeys.GetLogText(ehrProvKeyNew,null,false) })
			{
				Assert.IsFalse(text.Contains("SECRET"),"The key text is never logged: "+text);
			}
		}

		[TestMethod]
		public void TheKeyWindow_NoLongerAsksForAValidKey_AndSaysTheKeyIsOptional() {
			string source=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrProviderKeyEdit.cs");
			string code=CSharpSource.Read(source).CodeWithoutComments;
			Assert.IsFalse(code.Contains("ProvKeyIsValid"),"No key is checked.");
			Assert.IsFalse(code.Contains("Invalid provider key"));
			StringAssert.Contains(CSharpSource.GetBlock(source,"private void butSave_Click("),
				"string error=EhrProvKeys.SaveChecked(ehrProvKey,_keyCur,out bool isPrefChanged);");
			StringAssert.Contains(CSharpSource.GetBlock(source,"private void butSave_Click("),"ehrProvKey.ProvKey=textKey.Text;");
			string designer=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrProviderKeyEdit.designer.cs");
			StringAssert.Contains(designer,"this.labelKeyNote.Text = \"optional, not checked\";");
			StringAssert.Contains(designer,"this.Controls.Add(this.labelKeyNote);");
			//The explanation no longer sends offices to Open Dental's customer service for a key; it is set in the designer, not read from the .resx.
			StringAssert.Contains(designer,"The key is optional and is not checked: this program has no license keys.");
			Assert.IsFalse(designer.Contains("resources.GetString(\"label2.Text\")"));
			Assert.IsFalse(designer.IndexOf("customer service",StringComparison.OrdinalIgnoreCase)>=0);
			//The rule's own text: no key in it.
			string rule=CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDentalBusiness\Data Interface\EhrProvKeys.cs"),"public static string GetSaveError(");
			Assert.IsFalse(Regex.IsMatch(rule,@"\.ProvKey\b|\bprovKey\b"),"GetSaveError never reads a key.");
			StringAssert.Contains(rule,"public static string GetSaveError(string lName,string fName,int yearValue) {","No key among its arguments.");
			StringAssert.Contains(rule,"return Meth.GetString(MethodBase.GetCurrentMethod(),lName,fName,yearValue);");
		}

		[TestMethod]
		public void TheEhrWindow_LosesOnlyTheTwoDeliberateRemovals() {
			string source=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEHR.cs");
			string designer=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEHR.Designer.cs");
			//The dead butPatList14 handler (no button in the designer) and the quarterly-key stub with no caller (E-5).
			Assert.IsFalse(source.Contains("butPatList14"));
			Assert.IsFalse(designer.Contains("butPatList14"));
			Assert.IsFalse(source.Contains("QuarterlyKeyIsValid"));
			List<string> listFilesQuarterly=TestPaths.ProgramSourceFiles().Where(x => File.ReadAllText(x).Contains("QuarterlyKeyIsValid")).ToList();
			Assert.AreEqual(0,listFilesQuarterly.Count,string.Join("\r\n",listFilesQuarterly));
			//The table and its type stay, unread (E-5).
			StringAssert.Contains(TestPaths.ReadSource(@"OpenPmsDentalBusiness\TableTypes\EhrQuarterlyKey.cs"),"public class EhrQuarterlyKey");
			//The patient lists button keeps its handler.
			StringAssert.Contains(source,"private void butPatList_Click(");
			StringAssert.Contains(designer,"this.butPatList.Click += new System.EventHandler(this.butPatList_Click);");
			//ProvKeyIsValid stays false, and only the two CQM windows still ask it, until EHR-5 (counsel's E-10).
			StringAssert.Contains(CSharpSource.GetBlock(source,"public static bool ProvKeyIsValid("),"return false;");
			List<string> listCallers=TestPaths.ProgramSourceFiles()
				.Where(x => CSharpSource.Read(File.ReadAllText(x)).CodeWithoutComments.Contains("FormEHR.ProvKeyIsValid("))
				.Select(x => Path.GetFileName(x)).OrderBy(x => x).ToList();
			CollectionAssert.AreEqual(new[] { "FormEhrQualityMeasures.cs","FormEhrQualityMeasures2014.cs" },listCallers,string.Join(", ",listCallers));
		}
		#endregion Pure and source checks
	}
}
