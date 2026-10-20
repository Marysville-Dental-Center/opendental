// The windows cannot be opened in a test run (the GUI is not launched), so these tests read the window sources and check that each one calls the
// shared rules. The rules themselves are tested in the other files.
using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SignatureTests {
	[TestClass]
	public class WiringTests {
		private static string Form(string name) {
			return TestPaths.ReadRepoFile("OpenDental","Forms",name+".cs");
		}

		[TestMethod]
		public void LogOn_SimpleSwitchOffersDomainLogin() {
			string source=Form("FormLogOn");
			StringAssert.Contains(source,"checkUseDomainLogin.Visible=_isSimpleSwitch && DomainLogin.IsEnabled()");
			StringAssert.Contains(source,"DomainLogin.AuthenticateForSwitch(");
			StringAssert.Contains(source,"IsDomainAuthenticated=true");
			StringAssert.Contains(TestPaths.ReadRepoFile("OpenDental","Forms","FormLogOn.designer.cs"),"Use Windows (domain) login");
		}

		[TestMethod]
		public void ChangeUserPrompts_LogDomainAuthentication() {
			foreach(string form in new[] { "FormProcEdit","FormProcGroup","FormOrthoChart" }) {
				string source=Form(form);
				StringAssert.Contains(source,".IsDomainAuthenticated",form);
				StringAssert.Contains(source,"DomainLogin.GetSwitchLogText(",form);
			}
		}

		[TestMethod]
		public void FillSheet_ChangeUserButtonAndUserSigned() {
			string source=Form("FormSheetFillEdit");
			StringAssert.Contains(source,"SheetSignatureSigners.CanChangeUser(sheetField,SheetCur.SheetType,IsInTerminal)");
			StringAssert.Contains(source,"new FormLogOn(isSimpleSwitch:true)");
			StringAssert.Contains(source,"sheetField.UserSigned=SheetSignatureSigners.GetUserSignedToSave(");
			StringAssert.Contains(source,"SheetSignatureSigners.GetSignedLogText(");
			Assert.AreEqual(2,Regex.Matches(source,@"LogSignedAfterSwitch\(listSignedAfterSwitch\);").Count,"Logged after both sig box syncs.");
			StringAssert.Contains(source,"SheetSignatureSigners.GetSignedByText(");
		}

		[TestMethod]
		public void SignTreatmentPlan_HasChangeUser() {
			string source=Form("FormTPsign");
			StringAssert.Contains(source,"SheetSignatureSigners.IsChangeUserBox(sheetField)");
			StringAssert.Contains(source,"signatureBoxWrapper.UserSig=userod");
		}

		[TestMethod]
		public void PrintedSheets_ShowWhoSigned() {
			string source=TestPaths.ReadRepoFile("OpenDentBusiness","SheetFramework","SheetDrawingJob.cs");
			StringAssert.Contains(source,"SheetSignatureSigners.GetSignedByText(field.UserSigned,Userods.GetName)");
		}

		[TestMethod]
		public void OrthoChart_WritesTheLocalSwitchValue() {
			string source=Form("FormOrthoChart");
			StringAssert.Contains(source,"PatientNotes.SetUserNumOrthoLocked(_patient.PatNum,PatientNotes.ORTHO_LOCK_LOCAL_SWITCH)");
			StringAssert.Contains(source,"PatientNotes.GetOrthoLockedByText(");
		}

		[TestMethod]
		public void TpFeePermission_EnforcedInAllThreeWindows() {
			string sourceProcEdit=Form("FormProcEdit");
			StringAssert.Contains(sourceProcEdit,"Security.IsAuthorized(EnumPermType.ProcTPEditFee,true)");
			StringAssert.Contains(sourceProcEdit,"textProcFee.ReadOnly=!canEdit");
			StringAssert.Contains(sourceProcEdit,"SecurityLogs.MakeLogEntry(EnumPermType.ProcTPEditFee");
			//Called on load and on every status change.
			Assert.IsTrue(Regex.Matches(sourceProcEdit,@"SetTpFeeEditable\(\);").Count>=2);
			string sourceEditAll=Form("FormProcEditAll");
			StringAssert.Contains(sourceEditAll,"Security.IsAuthorized(EnumPermType.ProcTPEditFee,true)");
			StringAssert.Contains(sourceEditAll,"SecurityLogs.MakeLogEntry(EnumPermType.ProcTPEditFee");
			string sourceTpEdit=Form("FormProcTPEdit");
			StringAssert.Contains(sourceTpEdit,"Security.IsAuthorized(EnumPermType.ProcTPEditFee,true)");
			StringAssert.Contains(sourceTpEdit,"textFeeAmt.ReadOnly=true");
		}
	}
}
