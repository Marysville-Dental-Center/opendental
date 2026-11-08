// ODOPTS: the windows cannot be opened in a test run (the desktop client is never launched), so these tests read the window sources and check
// that each one calls the shared rules, writes Open Dental's row and makes its log entry; and they check the documents.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ODOPTSTests {
	[TestClass]
	public class OptionWiringTests {
		private static string Form(string name) {
			return ScratchDb.ReadRepoFile("OpenPmsDental","Forms",name+".cs");
		}

		private static string Between(string source,string start,string end) {
			int idxStart=source.IndexOf(start,StringComparison.Ordinal);
			Assert.IsTrue(idxStart>=0,"Not found: "+start);
			int idxEnd=source.IndexOf(end,idxStart+start.Length,StringComparison.Ordinal);
			Assert.IsTrue(idxEnd>idxStart,"Not found after it: "+end);
			return source.Substring(idxStart,idxEnd-idxStart);
		}

		#region PerioRedAttGing
		[TestMethod]
		public void PerioChart_AttGingIsRedAtOrBelowTheThreshold_AndCounted() {
			string contrPerio=ScratchDb.ReadRepoFile("OpenPmsDental","User Controls","ContrPerio.cs");
			Assert.AreEqual(2,Regex.Matches(contrPerio,@"PerioMeasures\.GetRedAttGing\(\)\?\?int\.MinValue").Count,"The cell color and the tooth count.");
			StringAssert.Contains(contrPerio,"bool isRedAtOrBelow=_perioSequenceTypeArray[GetSection(rowIndex)][GetSectionRow(rowIndex)].In(PerioSequenceType.MGJ,"
				+"PerioSequenceType.AttGing);");
			StringAssert.Contains(contrPerio,"bool isAtOrBelow=perioSequenceType.In(PerioSequenceType.MGJ,PerioSequenceType.AttGing);");
			Assert.IsFalse(contrPerio.Contains("redThresh=int.MaxValue;"),"The 24.4 'never red' is gone.");
		}

		[TestMethod]
		public void PerioChart_TheBoxWithTheOtherThresholds_AndThePrintout() {
			string designer=ScratchDb.ReadRepoFile("OpenPmsDental","Forms","FormPerio.designer.cs");
			StringAssert.Contains(designer,"this.groupBox2.Controls.Add(this.updownAttGing);");
			StringAssert.Contains(designer,"this.labelAttGing.Text = \"Att Ging (<=)\";");
			StringAssert.Contains(designer,"this.updownAttGing.MouseDown += new System.Windows.Forms.MouseEventHandler(this.updownRedAttGing_MouseDown);");
			StringAssert.Contains(designer,"this.groupBox2.Size = new System.Drawing.Size(221, 204);");
			string code=Form("FormPerio");
			string handler=Between(code,"private void updownRedAttGing_MouseDown(","#endregion");
			StringAssert.Contains(handler,"Security.IsAuthorized(EnumPermType.Setup)");
			StringAssert.Contains(handler,"PerioMeasures.SetRedAttGing(redAttGing)");
			StringAssert.Contains(handler,"OdOptions.LogChange(EnumPermType.Setup,");
			StringAssert.Contains(handler,"Cache.Refresh(InvalidType.Prefs);");
			string print=Between(code,"private void pd2_PrintPage(","private void updownRed_MouseDown(");
			StringAssert.Contains(print,"\"Teeth with Att Ging less than or equal to\"");
			StringAssert.Contains(print,"contrPerio.CountTeeth(PerioSequenceType.AttGing)");
		}
		#endregion PerioRedAttGing

		#region DomainLoginForSigBoxes
		[TestMethod]
		public void SignatureBoxes_PassIsSigBox_TheTaskPromptDoesNot() {
			foreach((string form,int count) in new[] { ("FormProcEdit",1),("FormProcGroup",2),("FormSheetFillEdit",1),("FormTPsign",1),("FormOrthoChart",1) }) {
				string source=Form(form);
				Assert.AreEqual(count,Regex.Matches(source,@"new FormLogOn\(isSimpleSwitch:true,isSigBox:true\)").Count,form);
				Assert.AreEqual(0,Regex.Matches(source,@"new FormLogOn\(isSimpleSwitch:true\)").Count,form);
			}
			StringAssert.Contains(Form("FormTaskEdit"),"new FormLogOn(isSimpleSwitch:true)","A task's From User is not a signature box.");
			string logOn=Form("FormLogOn");
			StringAssert.Contains(logOn,"checkUseDomainLogin.Visible=_isSimpleSwitch && DomainLogin.IsOfferedInChangeUser(_isSigBox);");
		}

		[TestMethod]
		public void GlobalSecurity_HasTheSwitch_AndLogsIt() {
			string designer=ScratchDb.ReadRepoFile("OpenPmsDental","Forms","FormGlobalSecurity.designer.cs");
			StringAssert.Contains(designer,"this.groupBox1.Controls.Add(this.checkDomainLoginForSigBoxes);");
			string code=Form("FormGlobalSecurity");
			StringAssert.Contains(code,"checkDomainLoginForSigBoxes.Checked=DomainLogin.IsForSigBoxesSettingOn();");
			StringAssert.Contains(code,"DomainLogin.SetForSigBoxesSetting(checkDomainLoginForSigBoxes.Checked)");
			StringAssert.Contains(code,"OdOptions.LogChange(EnumPermType.SecurityAdmin,");
		}
		#endregion DomainLoginForSigBoxes

		#region ClaimUpdateFeeForSecondary
		[TestMethod]
		public void ProcEdit_UpdatesSecondaryClaims_AfterTheClaimprocsAreSaved() {
			string closing=Between(Form("FormProcEdit"),"private void FormProcEdit_FormClosing(","Signalods.SetInvalid(InvalidType.BillingList);");
			int idxCompute=closing.IndexOf("Procedures.ComputeEstimates(_procedure,_patient.PatNum,ref _listClaimProcs,_isQuickAdd,",StringComparison.Ordinal);
			int idxUpdate=closing.IndexOf("ClaimFeeForSecondary.UpdateForProcFeeChange(_procedure,_procedureOld.ProcFee);",StringComparison.Ordinal);
			Assert.IsTrue(idxCompute>0,"ComputeEstimates");
			Assert.IsTrue(idxUpdate>idxCompute,"After ComputeEstimates saved the claimprocs, so their copies cannot undo the new fee.");
		}

		[TestMethod]
		public void ClaimReceivePreferences_HaveTheOption_AndLogIt() {
			string code=ScratchDb.ReadRepoFile("OpenPmsDental","User Controls","Preferences","UserControlAccountClaimReceive.cs");
			StringAssert.Contains(code,"checkClaimUpdateFeeForSecondary.Checked=ClaimFeeForSecondary.IsEnabled();");
			StringAssert.Contains(code,"ClaimFeeForSecondary.SetEnabled(checkClaimUpdateFeeForSecondary.Checked)");
			StringAssert.Contains(code,"OdOptions.LogChange(EnumPermType.Setup,ClaimFeeForSecondary.LABEL,ClaimFeeForSecondary.PREF_NAME,");
			string designer=ScratchDb.ReadRepoFile("OpenPmsDental","User Controls","Preferences","UserControlAccountClaimReceive.Designer.cs");
			StringAssert.Contains(designer,"this.checkClaimUpdateFeeForSecondary.Text = \""+ClaimFeeForSecondary.LABEL+"\";");
		}
		#endregion ClaimUpdateFeeForSecondary

		#region eForm import
		[TestMethod]
		public void EFormSetup_HasBothOptions_AndLogsThem() {
			string xaml=ScratchDb.ReadRepoFile("WpfControlsOD","Frms","FrmEFormSetup.xaml");
			StringAssert.Contains(xaml,"x:Name=\"checkImportAllergiesAsOther\"");
			StringAssert.Contains(xaml,"x:Name=\"checkImportProblemsAsOther\"");
			string code=ScratchDb.ReadRepoFile("WpfControlsOD","Frms","FrmEFormSetup.xaml.cs");
			StringAssert.Contains(code,"EFormImport.SetAllergiesAsOther(isAllergiesAsOther)");
			StringAssert.Contains(code,"EFormImport.SetProblemsAsOther(isProblemsAsOther)");
			Assert.AreEqual(2,Regex.Matches(code,@"OdOptions\.LogChange\(EnumPermType\.Setup,").Count);
		}

		[TestMethod]
		public void ManualImport_ListsUnmatchedEntriesUnticked_WhenTheOptionIsOff() {
			string code=Form("FormSheetImport");
			StringAssert.Contains(code,"if(allergyDef==null && !EFormImport.IsAllergiesAsOther()) {");
			StringAssert.Contains(code,"if(diseaseDef==null && !EFormImport.IsProblemsAsOther()) {");
			Assert.AreEqual(2,Regex.Matches(code,@"importRow\.ImpValDisplay=Lan\.g\(this,""\(no match: tick to add as Other\)""\);\s+importRow\.DoImport=false;").Count);
		}
		#endregion eForm import

		#region InsHistPerioScalingCode
		[TestMethod]
		public void FrequencyPreferences_HaveTheCode_ValidatedAndLogged() {
			string code=ScratchDb.ReadRepoFile("OpenPmsDental","User Controls","Preferences","UserControlTreatPlanFreqLimit.cs");
			StringAssert.Contains(code,"textInsHistPerioScalingCode.Text=InsHistCategories.GetPerioScalingCodeSetting();");
			StringAssert.Contains(code,"string procCodeListed=InsHistCategories.FindProcCode(procCode);");
			StringAssert.Contains(code,"InsHistCategories.SetPerioScalingCodeSetting(procCode)");
			StringAssert.Contains(code,"OdOptions.LogChange(EnumPermType.Setup,\"Insurance History perio scaling procedure code\",");
			StringAssert.Contains(code,"Changed|=UpdateInsHistPerioScalingCode(ref errorMessage);");
		}
		#endregion InsHistPerioScalingCode

		#region StatementAutoSendEmail and StatementAutoSendSMS
		[TestMethod]
		public void StatementSendSetup_FromBillingDefaults_SavesAndLogsBoth() {
			StringAssert.Contains(Form("FormBillingDefaults"),"FrmStatementSendSetup frmStatementSendSetup=new FrmStatementSendSetup();");
			string code=ScratchDb.ReadRepoFile("WpfControlsOD","Frms","FrmStatementSendSetup.xaml.cs");
			StringAssert.Contains(code,"StatementAutoSend.SetModes(modeEmail,modeSms)");
			Assert.AreEqual(2,Regex.Matches(code,@"OdOptions\.LogChange\(EnumPermType\.Setup,").Count);
			StringAssert.Contains(code,"labelTextNeedsProvider.Visible=StatementAutoSend.TextSender==null;");
			StringAssert.Contains(ScratchDb.ReadRepoFile("WpfControlsOD","Frms","FrmStatementSendSetup.xaml"),StatementAutoSend.TEXT_NEEDS_PROVIDER);
		}

		[TestMethod]
		public void Billing_NewStatements_GoThroughTheSendPath() {
			string options=Form("FormBillingOptions");
			StringAssert.Contains(options,"ListStatementNumsCreated=StatementAutoSend.GetCreatedStatementNums(listStatementNumsUnsentBefore,Statements.GetUnsentStatements());");
			string manage=ScratchDb.ReadRepoFile("OpenPmsDental","Main Modules","ControlManage.cs");
			StringAssert.Contains(manage,"formBillingOptions.ListStatementNumsCreated,formBillingOptions.ListStatementNumsEmailCreated);");
			string billing=Form("FormBilling");
			string autoSend=Between(billing,"private void AutoSendCreatedStatements() {","private void ");
			StringAssert.Contains(autoSend,"StatementAutoSend.GetEmailStatementNumsToSend(StatementAutoSend.GetEmailMode(),ListStatementNumsEmailCreated,");
			StringAssert.Contains(autoSend,"SendStatements(listStatementNumsEmail);");
			StringAssert.Contains(autoSend,"StatementAutoSend.SendTexts(StatementAutoSend.GetSmsMode(),ListStatementNumsCreated,");
			//The Send button and the automatic send share one path.
			StringAssert.Contains(Between(billing,"private void butSend_Click(","private void SendStatements("),"SendStatements(gridBill.SelectedTags<DataRow>()");
			StringAssert.Contains(Between(billing,"private void SendStatements(","BillingL.SendStatements(sendStatementsIO);"),
				"sendStatementsIO.ListStatementNumsToSend=listStatementNums;");
		}
		#endregion StatementAutoSendEmail and StatementAutoSendSMS

		#region Documents
		[TestMethod]
		public void PortNotes_NameEveryOption() {
			string odopts=ScratchDb.ReadRepoFile("docs","port-notes","ODOPTS.md");
			string swmap=ScratchDb.ReadRepoFile("docs","port-notes","SWMAP.md");
			string swmapRead=Between(swmap,"**Read by this program (","**Rows older than 24.3");
			string swmapNotBuilt=Between(swmap,"**Settings of features this program built without that option","## Program links");
			foreach(string name in OdOptions.ArrayPrefNames) {
				StringAssert.Contains(odopts,"| `"+name+"` |",name);
				StringAssert.Contains(swmapRead,"`"+name+"`",name+" is read by this program now.");
				Assert.IsFalse(swmapNotBuilt.Contains("`"+name+"`"),name+" is no longer 'built without'.");
			}
			StringAssert.Contains(odopts,"INFERRED");
		}

		[TestMethod]
		public void ReleaseNotes_OneLineEach() {
			string notes=ScratchDb.ReadRepoFile("docs","release-notes","next.md");
			foreach(string name in OdOptions.ArrayPrefNames) {
				Assert.AreEqual(1,Regex.Matches(notes,"`"+name+"`").Count,name);
			}
		}

		[TestMethod]
		public void OwnerQuestions_ListTheInferredMeanings() {
			string questions=ScratchDb.ReadRepoFile("docs","owner-questions.md");
			string section=Between(questions,"## Open Dental 25.3 options built (ODOPTS, 2026-11-07)","\n---");
			foreach(string name in OdOptions.ArrayPrefNames) {
				StringAssert.Contains(section,"`"+name+"`",name);
			}
		}
		#endregion Documents
	}
}
