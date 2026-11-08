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
		public void ProcEditAll_UpdatesSecondaryClaims_AfterUpdateMany() {
			string save=Between(Form("FormProcEditAll"),"private void butSave_Click(","#region Security Log Entries");
			int idxUpdateMany=save.IndexOf("ClaimProcs.UpdateMany(listClaimProcsAll);",StringComparison.Ordinal);
			int idxUpdate=save.IndexOf("ClaimFeeForSecondary.UpdateForProcFeeChange(ListProcedures[i],procedureOld.ProcFee);",StringComparison.Ordinal);
			Assert.IsTrue(idxUpdateMany>0,"UpdateMany");
			Assert.IsTrue(idxUpdate>idxUpdateMany,"After UpdateMany wrote the in-memory claimprocs, so they cannot write the old fee billed back.");
			Assert.AreEqual(1,Regex.Matches(save,@"ClaimFeeForSecondary\.UpdateForProcFeeChange\(").Count);
			StringAssert.Contains(save.Substring(idxUpdateMany),"Procedure procedureOld=_listProceduresOld.Find(x => x.ProcNum==ListProcedures[i].ProcNum);");
		}

		[TestMethod]
		public void AppointmentRepricing_UpdatesSecondaryClaims() {
			string procedures=ScratchDb.ReadRepoFile("OpenPmsDentalBusiness","Data Interface","Procedures.cs");
			//Moving an appointment or changing its provider (ControlAppt, PinBoard, Appointments.MoveAppointment).
			string setProviders=Between(procedures,"public static void SetProvidersInAppointment(","public static void SetCanadianLabFeesCompleteForProc(");
			int idxSync=setProviders.IndexOf("Sync(listProcNew,listProcOrig);",StringComparison.Ordinal);
			int idxUpdate=setProviders.IndexOf("ClaimFeeForSecondary.UpdateForProcFeeChange(listProcNew[i],listProcOrig[i].ProcFee);",StringComparison.Ordinal);
			Assert.IsTrue(idxSync>0,"Sync");
			Assert.IsTrue(idxUpdate>idxSync,"After the procedures are saved.");
			//The appointment window's save (Appointments.ApptSaveHelper).
			string helper=Between(procedures,"public static bool UpdateProcsInApptHelper(","public static void UpdateDiscountPlanAmt(");
			int idxUpdateProc=helper.IndexOf("Update(procCur,procOld);//Update fields if needed.",StringComparison.Ordinal);
			int idxUpdateClaims=helper.IndexOf("ClaimFeeForSecondary.UpdateForProcFeeChange(procCur,procOld.ProcFee);",StringComparison.Ordinal);
			Assert.IsTrue(idxUpdateProc>0,"Update");
			Assert.IsTrue(idxUpdateClaims>idxUpdateProc,"After the procedure is saved.");
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

		[TestMethod]
		public void AutoImport_TellsTheUserWhatItDidNotAdd() {
			string code=ScratchDb.ReadRepoFile("WpfControlsOD","Frms","FrmEFormFillEdit.xaml.cs");
			StringAssert.Contains(code,"EFormImport.ImportAutoFields(EFormCur,out List<string> listNotImported);");
			StringAssert.Contains(code,"string notImported=EFormImport.GetNotImportedMessage(listNotImported);");
			string import=ScratchDb.ReadRepoFile("OpenPmsDentalBusiness","EForms","EFormImport.cs");
			StringAssert.Contains(import,"listNotImported.Add(GetAllergyNotImportedText(listStrsInput[a]));");
			StringAssert.Contains(import,"listNotImported.Add(GetProblemNotImportedText(listStrsInput[a]));");
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
			//Always carried over from Open Dental asks until this window is saved here; the first save is recorded and logged.
			StringAssert.Contains(code,"labelAlwaysNotSavedHere.Visible=StatementAutoSend.IsEmailAlwaysNotSavedHere();");
			StringAssert.Contains(code,"if(StatementAutoSend.SetSetupSavedHere()) {");
			StringAssert.Contains(code,"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,StatementAutoSend.GetSetupSavedHereLogText(modeEmail));");
			StringAssert.Contains(ScratchDb.ReadRepoFile("WpfControlsOD","Frms","FrmStatementSendSetup.xaml"),"x:Name=\"labelAlwaysNotSavedHere\"");
		}

		[TestMethod]
		public void Billing_NewStatements_GoThroughTheSendPath() {
			string options=Form("FormBillingOptions");
			StringAssert.Contains(options,"ListStatementNumsCreated=StatementAutoSend.GetCreatedStatementNums(listStatementNumsUnsentBefore,Statements.GetUnsentStatements());");
			string manage=ScratchDb.ReadRepoFile("OpenPmsDental","Main Modules","ControlManage.cs");
			StringAssert.Contains(manage,"formBillingOptions.ListStatementNumsCreated,formBillingOptions.ListStatementNumsEmailCreated);");
			string billing=Form("FormBilling");
			string autoSend=Between(billing,"private void AutoSendCreatedStatements() {","private void ");
			StringAssert.Contains(autoSend,"StatementAutoSend.GetEmailStatementNumsToSend(StatementAutoSend.GetEmailModeToUse(),");
			StringAssert.Contains(autoSend,"StatementAutoSend.GetEmailQuestion(count,isAlwaysNotSavedHere)");
			Assert.IsFalse(autoSend.Contains("StatementAutoSend.GetEmailMode()"),"Billing uses the mode to use, not the stored Always.");
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

		///<summary>The one line of a document that contains marker.</summary>
		private static string Line(string document,string marker) {
			string[] arrayLines=document.Replace("\r\n","\n").Split('\n').Where(x => x.Contains(marker)).ToArray();
			Assert.AreEqual(1,arrayLines.Length,"One line with: "+marker);
			return arrayLines[0];
		}

		[TestMethod]
		public void Documents_StatementSendSetup_NameMessageToPay_AsThePublicSource() {
			//Review of 2026-11-07: Open Dental's window sends Message-to-Pay links; e-mailing the statement is this program's INFERRED substitute.
			string odopts=ScratchDb.ReadRepoFile("docs","port-notes","ODOPTS.md");
			string rowEmail=Line(odopts,"| `StatementAutoSendEmail` |");
			StringAssert.Contains(rowEmail,"`v25_1-eservices-msgtopay-on-statement-creation`");
			StringAssert.Contains(rowEmail,"INFERRED substitute");
			StringAssert.Contains(rowEmail,StatementAutoSend.PREF_SETUP_SAVED);
			StringAssert.Contains(Line(odopts,"| `StatementAutoSendSMS` |"),"Message-to-Pay");
			StringAssert.Contains(odopts,"- **Statement Send Setup is Message-to-Pay in Open Dental.**");
			StringAssert.Contains(ScratchDb.ReadRepoFile("docs","specs","features-25.1.md"),
				"| v25_1-eservices-msgtopay-on-statement-creation | Send Message-to-Pay links automatically when statements are created");
			string rowSwmap=Line(ScratchDb.ReadRepoFile("docs","port-notes","SWMAP.md"),"`StatementAutoSendEmail`, `StatementAutoSendSMS` | 0 never");
			StringAssert.Contains(rowSwmap,"**Message-to-Pay**");
			StringAssert.Contains(rowSwmap,"is **INFERRED**");
			string questions=ScratchDb.ReadRepoFile("docs","owner-questions.md");
			string question6=Between(questions,"- (ODOPTS 6) `StatementAutoSendEmail`","- (ODOPTS 7)");
			StringAssert.Contains(question6,"**the meaning itself was changed, not only the values.**");
			StringAssert.Contains(question6,"(a) does the office use this setting in");
			StringAssert.Contains(question6,"Open Dental for Message-to-Pay, and (b) do you want it to mean \"e-mail the statement when billing creates it\"");
			Assert.IsTrue(question6.IndexOf("(a) does",StringComparison.Ordinal)<question6.IndexOf("(c) which value",StringComparison.Ordinal),
				"Whether the office uses it for Message-to-Pay comes before the values.");
			string runbook=ScratchDb.ReadRepoFile("docs","adoption-runbook.md");
			string rowRunbook=Line(runbook,"| ODOPTS: `StatementAutoSendEmail` |");
			StringAssert.Contains(rowRunbook,"Message-to-Pay");
			StringAssert.Contains(rowRunbook,"Kept as built (owner, 2026-11-07, ODOPTS 6)");
			StringAssert.Contains(rowRunbook,"asks before e-mailing until Statement Send Setup is saved");
			Assert.IsFalse(rowRunbook.Contains("settle owner question ODOPTS 6"),"The runbook does not hold the switch-over for a question the owner answered.");
			StringAssert.Contains(Line(ScratchDb.ReadRepoFile("docs","release-notes","next.md"),"`StatementAutoSendEmail`"),"Message-to-Pay");
		}

		[TestMethod]
		public void Documents_EveryFeePath_AndTheSkippedEFormEntries() {
			string odopts=ScratchDb.ReadRepoFile("docs","port-notes","ODOPTS.md");
			StringAssert.Contains(Line(odopts,"| `ClaimUpdateFeeForSecondary` |"),"Edit All");
			string questions=ScratchDb.ReadRepoFile("docs","owner-questions.md");
			StringAssert.Contains(Between(questions,"- (ODOPTS 3) `ClaimUpdateFeeForSecondary`","- (ODOPTS 4)"),"Edit All");
			StringAssert.Contains(Line(ScratchDb.ReadRepoFile("docs","release-notes","next.md"),"`ClaimUpdateFeeForSecondary`"),"Edit All");
			string runbook=ScratchDb.ReadRepoFile("docs","adoption-runbook.md");
			foreach(string name in new[] { EFormImport.PREF_ALLERGIES_AS_OTHER,EFormImport.PREF_PROBLEMS_AS_OTHER }) {
				string row=Line(runbook,"| ODOPTS: `"+name+"` |");
				StringAssert.Contains(row,"0: a typed allergy or problem that matches no list entry is not auto-imported",name);
				StringAssert.Contains(row,"kept as built (owner, 2026-11-07, ODOPTS 4)",name);
			}
			StringAssert.Contains(Line(odopts,"| `EFormImportAllergiesAsOther` |"),"Allergy not imported (matches no allergy in the list; EFormImportAllergiesAsOther off)");
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
