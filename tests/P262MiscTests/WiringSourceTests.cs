// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace P262MiscTests {
	///<summary>The windows need a screen, so their wiring is checked from the source: where the business logic of the three items is called.</summary>
	[TestClass]
	public class WiringSourceTests {
		private static string Read(params string[] arrayParts) {
			return MiscDb.ReadRepoFile(arrayParts);
		}

		[TestMethod]
		public void ReferralCommlogs_NewOnesGetTheDefaultType_GridShowsType() {
			string source=Read("WpfControlsOD","Frms","FrmReferralEdit.xaml.cs");
			StringAssert.Contains(source,"commlog.CommType=CommlogReferralTypes.GetDefaultCommType();");
			Assert.IsFalse(source.Contains("commlog.CommType=0;"),"Referral commlogs no longer always get 0.");
			StringAssert.Contains(source,"new GridColumn(Lang.g(\"TableCommLog\",\"Type\"),100)");
			Assert.AreEqual(2,Regex.Matches(source,@"row\.Cells\.Add\(GetTypeCell\(").Count,"Anchored and other rows both show the type.");
		}

		[TestMethod]
		public void ReferralCommlogWindow_HasTypeList_SavesIt() {
			StringAssert.Contains(Read("WpfControlsOD","Frms","FrmCommReferral.xaml"),"x:Name=\"listType\"");
			string source=Read("WpfControlsOD","Frms","FrmCommReferral.xaml.cs");
			StringAssert.Contains(source,"CommlogReferralTypes.GetPickList(CommlogCur.CommType,listDefsCat)");
			StringAssert.Contains(source,"CommlogCur.CommType=_listCommTypes[listType.SelectedIndex];");
			Assert.IsTrue(source.IndexOf("FillTypes();")<source.IndexOf("private void FillTypes()"),"Filled on load.");
		}

		[TestMethod]
		public void DefinitionsWindow_HasOptionsForCommlogReferralTypes() {
			string source=Read("OpenPmsDental","Logic","DefL.cs");
			int idx=source.IndexOf("case DefCat.CommLogReferralTypes:");
			Assert.IsTrue(idx>0);
			string strCase=source.Substring(idx,source.IndexOf("break;",idx)-idx);
			StringAssert.Contains(strCase,"EnableColor=true");
			Assert.IsFalse(strCase.Contains("CanDelete=true"),"Like Commlog Types: hidden, never deleted.");
			Assert.IsFalse(strCase.Contains("EnableValue=true"),"The Usage values of Commlog Types automate patient commlogs only.");
			StringAssert.Contains(Read("OpenPmsDentalBusiness","Data Interface","Defs.cs"),
				"case DefCat.CommLogReferralTypes://Open Dental 26.2: the CommType of referral commlogs (ReferralNum set).");
		}

		[TestMethod]
		public void RecallTypesWindow_HasTheSetting() {
			string designer=Read("OpenPmsDental","Forms","FormRecallTypes.designer.cs");
			StringAssert.Contains(designer,"this.Controls.Add(this.checkAutoSwitchProphyPerio);");
			StringAssert.Contains(designer,"checkAutoSwitchProphyPerio_Click");
			string source=Read("OpenPmsDental","Forms","FormRecallTypes.cs");
			StringAssert.Contains(source,"checkAutoSwitchProphyPerio.Checked=RecallProphyPerioSwitch.IsOn();");
			StringAssert.Contains(source,"RecallProphyPerioSwitch.Save(isOnNew)");
			StringAssert.Contains(source,"DataValid.SetInvalid(InvalidType.Prefs);");
			StringAssert.Contains(source,"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,RecallProphyPerioSwitch.GetChangeLogLine(isOnOld,isOnNew));");
		}

		[TestMethod]
		public void RecallSync_CallsTheSwitchBeforeChoosingTheType() {
			string source=Read("OpenPmsDentalBusiness","Data Interface","Recalls.cs");
			int idxSynch=source.IndexOf("public static void Synch(long patNum)");
			int idxSwitch=source.IndexOf("RecallProphyPerioSwitch.SwitchIfNeeded(patNum,recallList);",idxSynch);
			int idxIsPerio=source.IndexOf("bool isPerio=",idxSynch);
			Assert.IsTrue(idxSynch>0 && idxSwitch>idxSynch && idxSwitch<idxIsPerio);
			Assert.AreEqual(1,Regex.Matches(source,"RecallProphyPerioSwitch\\.SwitchIfNeeded").Count,"Only the one-patient sync switches; Synch All does not.");
		}

		[TestMethod]
		public void EFormImportWindow_HasVitalSignsSection() {
			string source=Read("OpenPmsDental","Forms","FormSheetImport.cs");
			StringAssert.Contains(source,"FillRowsVitalSigns();");
			StringAssert.Contains(source,"ImportVitalSignRows();");
			Assert.IsTrue(source.IndexOf("ImportPatFieldRows();")<source.IndexOf("ImportVitalSignRows();"));
			string partial=Read("OpenPmsDental","Forms","FormSheetImport.VitalSigns.cs");
			StringAssert.Contains(partial,"EFormVitalSigns.GetImports(EFormCur,Vitalsigns.GetMostRecent(_patient.PatNum))");
			StringAssert.Contains(partial,"EFormVitalSigns.Import(_patient.PatNum,_dateVitalsTaken,listImports,");
			StringAssert.Contains(Read("OpenPmsDental","OpenPmsDental.csproj"),"<Compile Include=\"Forms\\FormSheetImport.VitalSigns.cs\">");
		}

		[TestMethod]
		public void EForms_SaveCheckAndDefaultLabel() {
			StringAssert.Contains(Read("OpenPmsDentalBusiness","Data Interface","EForms.cs"),"EFormVitalSigns.Validate(listEFormFields[i]);");
			StringAssert.Contains(Read("WpfControlsOD","Logic","EFormL.cs"),"return EFormVitalSigns.GetDefaultLabel(dbLink);");
		}

		[TestMethod]
		public void NoMigrationStepIsNeeded() {
			//Levels 2026110771 to 2026110779 are kept for this package. Nothing needed one: the definition category is Open Dental's (P262-SCHEMA), the
			//office adds its own types, and the switch setting's row is written when the office first changes it.
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			Assert.IsFalse(listSteps.Exists(x => x.Level>=2026110771 && x.Level<=2026110779));
		}
	}
}
