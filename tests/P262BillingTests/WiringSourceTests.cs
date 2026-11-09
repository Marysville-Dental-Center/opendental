// Source checks of the 26.2 billing and statements overhaul (package P262-BILLING) in the Windows Forms code. The windows are never shown in a
// test run, and other 26.2 packages edit some of the same files, so these checks make sure the hooks and the moved settings stay in place.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using ProjectFileTests;

namespace P262BillingTests {
	[TestClass]
	public class WiringSourceTests {
		private static string Read(params string[] arrayPathParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayPathParts)));
		}

		///<summary>The program's project file, evaluated, so a check holds for the classic project file and the SDK-style one alike.</summary>
		private static ProjectFile ProgramProject() {
			return ProjectFile.Load(TestDb.FindRepoFile(Path.Combine("OpenPmsDental","OpenPmsDental.csproj")));
		}

		///<summary>The text of a method, from its signature to the closing brace at two tabs.</summary>
		private static string GetMethodBody(string source,string methodSignatureStart) {
			int start=source.IndexOf(methodSignatureStart,StringComparison.Ordinal);
			Assert.IsTrue(start>=0,"Not found: "+methodSignatureStart);
			int end=source.IndexOf("\n\t\t}",start,StringComparison.Ordinal);
			Assert.IsTrue(end>start);
			return source.Substring(start,end-start);
		}

		private static int CountOf(string text,string value) {
			return Regex.Matches(text,Regex.Escape(value)).Count;
		}

		#region Statement Options window (item 8)
		[TestMethod]
		public void StatementButton_ArrowOpensTheStatementOptionsWindow() {
			string account=Read("OpenPmsDental","Main Modules","ControlAccount.cs");
			string layout=GetMethodBody(account,"public void LayoutToolBar() {");
			int indexButton=layout.IndexOf("new ODToolBarButton(Lan.g(this,\"Statement\"),4,\"\",\"Statement\");",StringComparison.Ordinal);
			Assert.IsTrue(indexButton>0);
			string statementButton=layout.Substring(indexButton,layout.IndexOf("ToolBarMain.Buttons.Add(toolBarButton);",indexButton,StringComparison.Ordinal)-indexButton);
			StringAssert.Contains(statementButton,"toolBarButton.Style=ODToolBarButtonStyle.DropDownButton;");
			Assert.IsFalse(statementButton.Contains("DropDownMenu=contextMenuStatement"),"No dropdown menu: the arrow opens the window.");
			Assert.AreEqual(0,CountOf(account,"DropDownMenu=contextMenuStatement"));
			Assert.AreEqual(1,CountOf(account,"ConstructStatementOptions();"),"Constructor.");
			//The button's click still prints the default statement.
			StringAssert.Contains(account,"ToolBarClick toolBarClick=toolBarButStatement_Click;");
			string partial=Read("OpenPmsDental","Main Modules","ControlAccount.StatementOptions.cs");
			StringAssert.Contains(partial,"ToolBarMain.ButtonDropDownClick+=ToolBarMain_ButtonDropDownClick;");
			StringAssert.Contains(partial,"contextMenuStatement.MenuItems.Cast<MenuItem>()");
			StringAssert.Contains(partial,"using FormStatementChoices formStatementChoices=new FormStatementChoices();");
			StringAssert.Contains(partial,"listMenuItems[formStatementChoices.IndexSelected-1].PerformClick();");
			StringAssert.Contains(partial,"toolBarButStatement_Click();");
			StringAssert.Contains(partial,"this.BeginInvoke(toolBarClick);");
			ProgramProject().AssertCompiles(@"Main Modules\ControlAccount.StatementOptions.cs");
		}

		[TestMethod]
		public void StatementMenuEntries_EachHasItsChoice() {
			string designer=Read("OpenPmsDental","Main Modules","ControlAccount.Designer.cs");
			Match match=Regex.Match(designer,@"this\.contextMenuStatement\.MenuItems\.AddRange\(new System\.Windows\.Forms\.MenuItem\[\] \{(?<items>[^}]*)\}\);");
			Assert.IsTrue(match.Success);
			List<string> listItems=Regex.Matches(match.Groups["items"].Value,@"this\.(\w+)").Cast<Match>().Select(x => x.Groups[1].Value).ToList();
			Assert.AreEqual(7,listItems.Count,"Walkout, Email, Receipt, Invoice, Limited, Limited (Custom), More Options.");
			string partial=Read("OpenPmsDental","Main Modules","ControlAccount.StatementOptions.cs");
			foreach(string item in listItems) {
				Assert.IsTrue(Regex.IsMatch(partial,Regex.Escape(item)+@"\.Tag=StatementChoice\.\w+;"),item);
			}
			//The Service Date View window, added by M2-SDV, too.
			StringAssert.Contains(Read("OpenPmsDental","Main Modules","ControlAccount.ServiceDateView.cs"),
				"contextMenuStatement.MenuItems.Add(new MenuItem(Lan.g(this,\"Service Date View Window\"),menuItemServiceDateViewWindow_Click) { Tag=StatementChoice.ServiceDateView });");
			//Every choice except the button itself is an entry.
			Assert.AreEqual(Enum.GetValues(typeof(StatementChoice)).Length-1,listItems.Count+1);
		}

		[TestMethod]
		public void ToolBar_ArrowWithoutMenuRaisesTheDropDownEvent() {
			string toolBar=Read("OpenPmsDental","UI","ToolBarOD.cs");
			StringAssert.Contains(toolBar,"public event ODToolBarButtonClickEventHandler ButtonDropDownClick=null;");
			Assert.IsTrue(Regex.IsMatch(toolBar,
				@"if\(toolBarButton\.DropDownMenu==null && toolBarButton\.ContextMenuStripDropDown==null\)\{[^\r\n]*\r?\n(\s*[^\r\n]*\r?\n){3}\s*OnButtonDropDownClicked\(toolBarButton\);"),
				"Only a dropdown button without a menu raises it.");
			Assert.AreEqual(1,CountOf(toolBar,"OnButtonDropDownClicked(toolBarButton);"));
		}

		[TestMethod]
		public void StatementOptionsWindow_ReturnsTheRowChosen() {
			string source=Read("OpenPmsDental","Forms","FormStatementChoices.cs");
			StringAssert.Contains(source,"public void AddChoice(string title,string description)");
			StringAssert.Contains(source,"IndexSelected=e.Row;");
			StringAssert.Contains(source,"IndexSelected=gridMain.GetSelectedIndex();");
			string designer=Read("OpenPmsDental","Forms","FormStatementChoices.Designer.cs");
			StringAssert.Contains(designer,"this.Text = \"Statement Options\";");
			StringAssert.Contains(designer,"this.gridMain.CellDoubleClick += new OpenPmsDental.UI.ODGridClickEventHandler(this.gridMain_CellDoubleClick);");
			ProjectFile project=ProgramProject();
			project.AssertCompiles(@"Forms\FormStatementChoices.cs",@"Forms\FormStatementChoices.Designer.cs");
			project.AssertEmbeds(@"Forms\FormStatementChoices.resx");
		}
		#endregion Statement Options window (item 8)

		#region Statement window redesign (item 9)
		[TestMethod]
		public void StatementWindow_EveryControlIsInOneContainer() {
			string designer=Read("OpenPmsDental","Forms","FormStatementOptions.designer.cs");
			string[] arrayControls={ "butSave","checkHidePayment","label3","textNote","butToday","butDatesAll","but90days","but45days","labelEndDate",
				"labelStartDate","textNoteBold","label1","label2","listMode","checkIntermingled","checkSinglePatient","groupDateRange","textDateEnd",
				"checkBoxBillShowTransSinceZero","textDateStart","label4","checkIsSent","butDelete","butPrint","butEmail","butPreview","textDate",
				"checkIsReceipt","groupInvoice","textInvoiceNum","label5","checkIsInvoiceCopy","checkIsInvoice","checkSuperStatement","checkLimited",
				"checkShowLName","checkExcludeTxfr","checkExportCSV","checkServiceDateView","groupStatement","groupContents" };
			foreach(string control in arrayControls) {
				Assert.AreEqual(1,Regex.Matches(designer,@"\.Controls\.Add\(this\."+control+@"\);").Count,control);
			}
			StringAssert.Contains(designer,"this.groupStatement.Controls.Add(this.listMode);");
			StringAssert.Contains(designer,"this.groupStatement.Controls.Add(this.checkIsSent);");
			StringAssert.Contains(designer,"this.groupContents.Controls.Add(this.checkHidePayment);");
			StringAssert.Contains(designer,"this.groupContents.Controls.Add(this.checkExportCSV);");
			StringAssert.Contains(designer,"this.groupDateRange.Controls.Add(this.checkBoxBillShowTransSinceZero);");
		}

		[TestMethod]
		public void StatementWindow_LimitedCustomKeepsTheSameControlsUsable() {
			string source=Read("OpenPmsDental","Forms","FormStatementOptions.cs");
			string helper=GetMethodBody(source,"private void LimitedCustomStatementLayoutHelper() {");
			StringAssert.Contains(helper,"DisableAllExceptInBoxes(butDelete,butPreview,butSave,checkIsSent,checkIntermingled,checkExportCSV,checkShowLName,"
				+"checkExcludeTxfr, checkHidePayment,butPrint,butEmail,textNote,textNoteBold,listMode,label1,label2,label3,label4,textDate);");
			Assert.IsFalse(helper.Contains("this.DisableAllExcept("),"DisableAllExcept alone would disable the boxes.");
			string boxes=GetMethodBody(source,"private void DisableAllExceptInBoxes(");
			StringAssert.Contains(boxes,"DisableAllExcept(listControlsKept.Concat(listBoxes).ToArray());");
			StringAssert.Contains(boxes,"control.Enabled=false;");
		}
		#endregion Statement window redesign (item 9)

		#region Electronic Billing window (item 17)
		[TestMethod]
		public void ElectronicBillingWindow_SavesEveryElectronicBillingSetting() {
			string source=Read("OpenPmsDental","Forms","FormEbillSetup.cs");
			string save=GetMethodBody(source,"private void butSave_Click(");
			foreach(PrefName prefName in EbillSettings.ListPrefNames) {
				StringAssert.Contains(save,"(PrefName."+prefName+",",prefName.ToString());
			}
			StringAssert.Contains(save,"if(Ebills.Sync(_listEbills,_listEbillsOld)) {");
			StringAssert.Contains(save,"EbillSettings.FormatCreditCardChoices(checkMC.Checked,checkV.Checked,checkD.Checked,checkAmEx.Checked)");
			string load=GetMethodBody(source,"private void FormEbillSetup_Load(");
			foreach(PrefName prefName in EbillSettings.ListPrefNames.Where(x => x!=PrefName.BillingElectCreditCardChoices && x!=PrefName.BillingUseElectronic)) {
				StringAssert.Contains(load,"(PrefName."+prefName+")",prefName.ToString());
			}
			StringAssert.Contains(load,"PrefC.GetEnum<BillingUseElectronicEnum>(PrefName.BillingUseElectronic)");
			StringAssert.Contains(load,"EbillSettings.ParseCreditCardChoices(PrefC.GetString(PrefName.BillingElectCreditCardChoices),");
			StringAssert.Contains(load,"_listEbills=Ebills.GetDeepCopy();");
			string designer=Read("OpenPmsDental","Forms","FormEbillSetup.Designer.cs");
			StringAssert.Contains(designer,"this.Text = \"Electronic Billing\";");
			StringAssert.Contains(designer,"this.textPassword.PasswordChar = '*';");
			StringAssert.Contains(designer,"this.comboClinic.IncludeUnassigned = true;");
		}

		[TestMethod]
		public void BillingDefaults_NoLongerHoldsTheElectronicBillingSettings() {
			string source=Read("OpenPmsDental","Forms","FormBillingDefaults.cs");
			foreach(PrefName prefName in EbillSettings.ListPrefNames) {
				Assert.IsFalse(Regex.IsMatch(source,@"PrefName\."+prefName+@"\b"),prefName.ToString());
			}
			Assert.IsFalse(source.Contains("Ebills."),"The ebill rows are the Electronic Billing window's.");
			Assert.IsFalse(source.Contains("IsUserPasswordOnly"));
			StringAssert.Contains(GetMethodBody(source,"private void butEbill_Click("),"using FormEbillSetup formEbillSetup=new FormEbillSetup();");
			string designer=Read("OpenPmsDental","Forms","FormBillingDefaults.Designer.cs");
			Assert.IsFalse(designer.Contains("groupBoxBilling"));
			StringAssert.Contains(designer,"this.butEbill.Click += new System.EventHandler(this.butEbill_Click);");
			//The Billing List's Defaults button opened Billing Defaults limited to electronic billing; it opens the Electronic Billing window now.
			string billing=GetMethodBody(Read("OpenPmsDental","Forms","FormBilling.cs"),"private void butDefaults_Click(");
			StringAssert.Contains(billing,"using FormEbillSetup formEbillSetup=new FormEbillSetup();");
			Assert.IsFalse(billing.Contains("FormBillingDefaults"));
			ProjectFile project=ProgramProject();
			project.AssertCompiles(@"Forms\FormEbillSetup.cs");
			project.AssertEmbeds(@"Forms\FormEbillSetup.resx");
		}
		#endregion Electronic Billing window (item 17)

		#region Billing Defaults (item 18)
		[TestMethod]
		public void BillingDefaults_SavesThePracticeWideSettingsAndTheMessagesPerClinic() {
			string source=Read("OpenPmsDental","Forms","FormBillingDefaults.cs");
			string save=GetMethodBody(source,"private void butSave_Click(");
			foreach(PrefName prefName in new[] { PrefName.BillingDefaultsLastDays,PrefName.BillingDefaultsIntermingle,PrefName.BillingDefaultsSinglePatient,
				PrefName.BillingShowTransSinceBalZero })
			{
				StringAssert.Contains(save,"(PrefName."+prefName+",",prefName.ToString());
			}
			StringAssert.Contains(save,"| StatementInserterBarcode.SetEnabled(checkInserterBarcode.Checked))");
			StringAssert.Contains(save,"BillingDefaultsByClinic.SaveSettings(settings,_dictionarySettingsOld[clinicNum])");
			StringAssert.Contains(save,"DataValid.SetInvalid(InvalidType.ClinicPrefs);");
			//The messages are only written through BillingDefaultsByClinic.
			foreach(PrefName prefName in BillingDefaultsByClinic.ListPrefNames) {
				Assert.IsFalse(Regex.IsMatch(source,@"Prefs\.Update\w+\(PrefName\."+prefName+@"\b"),prefName.ToString());
			}
			string load=GetMethodBody(source,"private void FormBillingDefaults_Load(");
			StringAssert.Contains(load,"ShowSettings(_clinicNumCur);");
			string show=GetMethodBody(source,"private void ShowSettings(");
			StringAssert.Contains(show,"checkUsePracticeDefaults.Visible=(clinicNum>0);");
			StringAssert.Contains(show,"settingsShown=GetSettings(0);");
			//A clinic with settings of its own shows the practice values, as edited in the window, for the messages it has no row for (review fix of
			//p262/billing-2), before the boxes are filled.
			string follow="BillingDefaultsByClinic.FollowPracticeDefaults(settings,_dictionarySettingsOld[clinicNum],GetSettings(0),"
				+"_dictionaryPrefNamesWithRows[clinicNum]);";
			StringAssert.Contains(show,follow);
			Assert.IsTrue(show.IndexOf(follow)<show.IndexOf("textNote.Text=settingsShown.GeneralMessage;"));
			StringAssert.Contains(GetMethodBody(source,"private BillingDefaultsSettings GetSettings("),
				"_dictionaryPrefNamesWithRows[clinicNum]=BillingDefaultsByClinic.GetPrefNamesWithRows(clinicNum);");
			string designer=Read("OpenPmsDental","Forms","FormBillingDefaults.Designer.cs");
			StringAssert.Contains(designer,"this.comboClinic.IncludeUnassigned = true;");
			StringAssert.Contains(designer,"this.checkUsePracticeDefaults.Visible = false;");
			foreach(string control in new[] { "textNote","groupBox3","textInvoiceNote","label4","label11" }) {
				StringAssert.Contains(designer,"this.groupMessages.Controls.Add(this."+control+");",control);
			}
			foreach(string control in new[] { "checkIntermingled","textDays","checkBoxBillShowTransSinceZero","checkInserterBarcode" }) {
				StringAssert.Contains(designer,"this.groupStatements.Controls.Add(this."+control+");",control);
			}
		}

		[TestMethod]
		public void Readers_TakeTheMessagesOfThePatientsClinic() {
			string statements=GetMethodBody(Read("OpenPmsDentalBusiness","Data Interface","Statements.cs"),"public static EmailMessage GetEmailMessageForStatement(");
			StringAssert.Contains(statements,"BillingDefaultsByClinic.GetString(PrefName.BillingEmailSubject,patient.ClinicNum)");
			StringAssert.Contains(statements,"BillingDefaultsByClinic.GetString(PrefName.BillingEmailBodyText,patient.ClinicNum)");
			string emailHtml=GetMethodBody(Read("OpenPmsDentalBusiness","Logic","BillingEmailHtml.cs"),"public static EmailType GetEmailTypeForStatement(");
			StringAssert.Contains(emailHtml,"return GetBillingDefaultsEmailType(clinicNum);");
			StringAssert.Contains(Read("OpenPmsDentalBusiness","Logic","BillingL.cs"),
				"if(BillingDefaultsByClinic.GetBool(PrefName.BillingEmailIncludeAutograph,patient.ClinicNum)) {");
			StringAssert.Contains(Read("OpenPmsDental","Main Modules","ControlAccount.cs"),
				"statement.Note=BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsInvoiceNote,_patient.ClinicNum);");
			//No other code reads a message preference directly, so none misses a clinic's own value. Billing Options keeps the General Message with
			//each clinic's filter defaults (its own per-clinic rows), and BillingEmailHtml reads the practice e-mail type for clinic 0.
			Dictionary<PrefName,string[]> dictionaryAllowed=new Dictionary<PrefName,string[]>() {
				{ PrefName.BillingDefaultsNote,new[] { "FormBillingOptions.cs" } },
				{ PrefName.BillingEmailType,new[] { "BillingEmailHtml.cs" } },
			};
			string root=Path.GetDirectoryName(Path.GetDirectoryName(TestDb.FindRepoFile(Path.Combine("OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj"))));
			List<string> listFiles=Directory.GetFiles(Path.Combine(root,"OpenPmsDental"),"*.cs",SearchOption.AllDirectories)
				.Concat(Directory.GetFiles(Path.Combine(root,"OpenPmsDentalBusiness"),"*.cs",SearchOption.AllDirectories))
				.Where(x => !x.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar)
					&& !x.Contains(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar))
				.ToList();
			foreach(string file in listFiles) {
				string text=File.ReadAllText(file);
				foreach(PrefName prefName in BillingDefaultsByClinic.ListPrefNames) {
					if(!Regex.IsMatch(text,@"PrefC\.\w+\(PrefName\."+prefName+@"\)")) {
						continue;
					}
					string fileName=Path.GetFileName(file);
					Assert.IsTrue(dictionaryAllowed.ContainsKey(prefName) && dictionaryAllowed[prefName].Contains(fileName),fileName+" reads "+prefName);
				}
			}
		}

		[TestMethod]
		public void BillingOptions_ReadsTheClinicRowsAgainAfterBillingDefaults() {
			string defaults=GetMethodBody(Read("OpenPmsDental","Forms","FormBillingOptions.cs"),"private void butDefaults_Click(");
			int indexRefresh=defaults.IndexOf("RefreshClinicPrefs();",StringComparison.Ordinal);
			int indexSetDefaults=defaults.IndexOf("SetDefaults();",StringComparison.Ordinal);
			Assert.IsTrue(indexRefresh>0 && indexRefresh<indexSetDefaults);
		}

		[TestMethod]
		public void BillingOptions_SaveDefaultsWritesTheSelectedClinicOnly() {
			//Review fix: Save Defaults wrote the rows of every clinic in its list, so a clinic whose only row was the General Message from Billing
			//Defaults got frozen copies of the practice filters, and a clinic back on the practice defaults got its General Message row again.
			string source=Read("OpenPmsDental","Forms","FormBillingOptions.cs");
			string save=GetMethodBody(source,"private void butSaveDefault_Click(");
			StringAssert.Contains(save,"if(BillingOptionsClinicDefaults.SaveForClinic(clinicNum,listValues)) {");
			StringAssert.Contains(save,"DataValid.SetInvalid(InvalidType.ClinicPrefs);");
			Assert.IsFalse(source.Contains("ClinicPrefs.Sync("),"No sync of several clinics' rows.");
			Assert.IsFalse(source.Contains("SelectMany("),"No list of every clinic's rows.");
			foreach(PrefName prefName in BillingOptionsClinicDefaults.ListPrefNames) {
				StringAssert.Contains(save,"new KeyValuePair<PrefName,string>(PrefName."+prefName+",",prefName.ToString());
			}
			string refresh=GetMethodBody(source,"private void RefreshClinicPrefs(");
			StringAssert.Contains(refresh,"_dictionaryClinicPrefs=BillingOptionsClinicDefaults.GetFilledDefaults(BillingOptionsClinicDefaults.GetRowsByClinic());");
			Assert.IsFalse(Regex.IsMatch(refresh,@"ClinicPrefs\.(GetWhere|Insert|Update|Upsert|Delete)"),"Reads only through the helper, writes nothing.");
			//A clinic without filter defaults here still shows its own General Message, also when the window opens or Billing Defaults closes.
			string filters=GetMethodBody(source,"private void SetFiltersForClinicNums(");
			StringAssert.Contains(filters,"textNote.Text=GetGeneralMessage(listClinicNums);");
			StringAssert.Contains(GetMethodBody(source,"private void SetDefaults("),"textNote.Text=GetGeneralMessage(comboClinic.ListClinicNumsSelected);");
			StringAssert.Contains(GetMethodBody(source,"private string GetGeneralMessage("),
				"return BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsNote,listClinicNums[0]);");
			//Automated billing reads the same filter preferences (the General Message included), with the practice value for a row a clinic lacks.
			string billing=GetMethodBody(Read("OpenPmsDentalBusiness","Logic","BillingL.cs"),"public ClinicBillingOptions(long clinicNum) {");
			StringAssert.Contains(billing,"listBillingPrefs.FindAll(x => !listExistingClinicPrefs.Contains(x))");
			StringAssert.Contains(billing,"PrefName.BillingDefaultsNote");
		}
		#endregion Billing Defaults (item 18)

		#region Migration
		[TestMethod]
		public void NoMigrationStep_InThisPackagesLevels() {
			//The package needs no new row: the settings keep their preference and ebill rows, and a clinic's rows are written when the office saves
			//them. Levels 2026110721 to 2026110729 stay free.
			string folder=Path.GetDirectoryName(TestDb.FindRepoFile(Path.Combine("OpenPmsDentalBusiness","Misc","ForkMigrations","README.md")));
			foreach(string file in Directory.GetFiles(folder,"*.cs",SearchOption.AllDirectories)) {
				Assert.IsFalse(Regex.IsMatch(File.ReadAllText(file),@"\[ForkMigration\(20261107(2[1-9])\b"),Path.GetFileName(file));
			}
		}
		#endregion Migration
	}
}
