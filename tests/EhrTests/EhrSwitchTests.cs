// Turning EHR on again (docs/specs/ehr-rebuild-plan.md, section 3.1; owner decision E-2): Show Features and the Setup Wizard call the same
// confirmation, Cancel unchecks the box, and saving no longer refuses. The click behaviour runs here with fakes for the message boxes; the windows
// themselves are checked in their sources, since no window of the program is created in tests.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;

namespace EhrTests {
	[TestClass]
	public class EhrSwitchTests {
		private const string PATH_SHOW_FEATURES=@"OpenPmsDental\Forms\FormShowFeatures.cs";
		private const string PATH_WIZARD=@"OpenPmsDental\User Controls\SetupWizControls\UserControlSetupWizFeatures.cs";
		private const string PATH_WIZARD_DESIGNER=@"OpenPmsDental\User Controls\SetupWizControls\UserControlSetupWizFeatures.Designer.cs";
		private const string PATH_WIZARD_RESX=@"OpenPmsDental\User Controls\SetupWizControls\UserControlSetupWizFeatures.resx";
		private const string RESTART="You will need to restart the program for the change to take effect.";

		#region The confirmation
		[TestMethod]
		public void Confirmation_NamesTheChanges_TheTools_TheMissingCertification_AndTheRestart() {
			string text=FormShowFeatures.GetEhrOnConfirmationText();
			foreach(string part in new[] { "prescriptions","patient race","required fields","the Chart","EHR tools","not certified EHR technology",
				"cannot be used to attest or submit to any program","restart" })
			{
				StringAssert.Contains(text,part);
			}
			Assert.IsFalse(Regex.IsMatch(text,"password",RegexOptions.IgnoreCase),"No password (E-2).");
		}

		[TestMethod]
		public void CheckingTheBox_AsksOnce_AndCancelUnchecksIt() {
			List<string> listQuestions=new List<string>();
			List<string> listMessages=new List<string>();
			bool isChecked=FormShowFeatures.HandleEhrClick(true,(text) => { listQuestions.Add(text); return false; },(text) => listMessages.Add(text));
			Assert.IsFalse(isChecked,"Cancel unchecks the box.");
			Assert.AreEqual(1,listQuestions.Count,"One confirmation.");
			Assert.AreEqual(FormShowFeatures.GetEhrOnConfirmationText(),listQuestions[0]);
			Assert.AreEqual(0,listMessages.Count,"No restart message after Cancel.");
		}

		[TestMethod]
		public void CheckingTheBox_OK_KeepsIt_AndShowsTheRestartMessage() {
			List<string> listQuestions=new List<string>();
			List<string> listMessages=new List<string>();
			bool isChecked=FormShowFeatures.HandleEhrClick(true,(text) => { listQuestions.Add(text); return true; },(text) => listMessages.Add(text));
			Assert.IsTrue(isChecked,"Saving no longer refuses: the box stays checked.");
			Assert.AreEqual(1,listQuestions.Count);
			CollectionAssert.AreEqual(new[] { RESTART },listMessages);
		}

		[TestMethod]
		public void UncheckingTheBox_AsksNothing_AndShowsTheRestartMessage() {
			List<string> listQuestions=new List<string>();
			List<string> listMessages=new List<string>();
			bool isChecked=FormShowFeatures.HandleEhrClick(false,(text) => { listQuestions.Add(text); return true; },(text) => listMessages.Add(text));
			Assert.IsFalse(isChecked);
			Assert.AreEqual(0,listQuestions.Count);
			CollectionAssert.AreEqual(new[] { RESTART },listMessages);
		}
		#endregion The confirmation

		#region Both windows
		[TestMethod]
		public void BothWindows_CallTheSameConfirmation() {
			string showFeatures=TestPaths.ReadSource(PATH_SHOW_FEATURES);
			string wizard=TestPaths.ReadSource(PATH_WIZARD);
			StringAssert.Contains(CSharpSource.GetBlock(showFeatures,"private void checkEhr_Click("),"checkEhr.Checked=HandleEhrClick(checkEhr.Checked);");
			StringAssert.Contains(CSharpSource.GetBlock(wizard,"private void checkEhr_Click("),"checkEhr.Checked=FormShowFeatures.HandleEhrClick(checkEhr.Checked);");
			//Both boxes are wired to their handlers.
			StringAssert.Contains(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormShowFeatures.Designer.cs"),
				"this.checkEhr.Click += new System.EventHandler(this.checkEhr_Click);");
			StringAssert.Contains(TestPaths.ReadSource(PATH_WIZARD_DESIGNER),"this.checkEhr.Click += new System.EventHandler(this.checkEhr_Click);");
			//The shared method asks through ConfirmEhrOn, the one public confirmation, and with no password.
			string handle=CSharpSource.GetBlock(showFeatures,"public static bool HandleEhrClick(");
			StringAssert.Contains(handle,"if(isChecked && !ConfirmEhrOn(funcConfirm)) {");
			StringAssert.Contains(CSharpSource.GetBlock(showFeatures,"public static bool ConfirmEhrOn("),"return funcConfirm(GetEhrOnConfirmationText());");
			foreach(string forbidden in new[] { "GetPasswordFromUser","InputBox","abracadabra" }) {
				Assert.IsFalse(handle.Contains(forbidden),forbidden);
				Assert.IsFalse(CSharpSource.GetBlock(wizard,"private void checkEhr_Click(").Contains(forbidden),forbidden);
			}
			Assert.IsTrue(typeof(FormShowFeatures).GetMethod("ConfirmEhrOn").IsStatic && typeof(FormShowFeatures).GetMethod("ConfirmEhrOn").IsPublic);
		}

		[TestMethod]
		public void Saving_NoLongerRefuses_InEitherWindow() {
			string showFeatures=CSharpSource.Read(TestPaths.ReadSource(PATH_SHOW_FEATURES)).CodeWithoutComments;
			Assert.IsFalse(showFeatures.Contains("The EHR feature is not available in this build."),"The refusal is gone.");
			Assert.IsFalse(CSharpSource.GetBlock(TestPaths.ReadSource(PATH_SHOW_FEATURES),"private void checkEhr_Click(").Contains("checkEhr.Checked=false"));
			StringAssert.Contains(CSharpSource.GetBlock(TestPaths.ReadSource(PATH_SHOW_FEATURES),"private void butSave_Click("),
				"Prefs.UpdateBool(PrefName.ShowFeatureEhr,checkEhr.Checked)");
			StringAssert.Contains(CSharpSource.GetBlock(TestPaths.ReadSource(PATH_WIZARD),"private void ControlDone("),
				"Prefs.UpdateBool(PrefName.ShowFeatureEhr,checkEhr.Checked)");
			//Open Dental asked for a password for clinics only.
			StringAssert.Contains(CSharpSource.GetBlock(TestPaths.ReadSource(PATH_SHOW_FEATURES),"private void checkEnableClinics_Click("),"GetPasswordFromUser()");
		}

		[TestMethod]
		public void WizardExplanation_MakesNoCertificationClaim() {
			XDocument xDocument=XDocument.Parse(TestPaths.ReadSource(PATH_WIZARD_RESX));
			XElement xElement=xDocument.Root.Elements("data").Single(x => (string)x.Attribute("name")=="labelInfoEHR.Tag");
			string text=xElement.Element("value").Value;
			foreach(string claim in new[] { "certified software","2014 edition","EHR Contract","contact Open Dental","certification body","qualified" }) {
				Assert.IsFalse(text.IndexOf(claim,StringComparison.OrdinalIgnoreCase)>=0,"The wizard still says: "+claim);
			}
			StringAssert.Contains(text,"not certified EHR technology");
			StringAssert.Contains(text,"cannot be used to attest or submit to any program");
		}
		#endregion Both windows
	}
}
