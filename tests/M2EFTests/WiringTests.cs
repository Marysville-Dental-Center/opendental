// Checks that the windows (WpfControlsOD and OpenPmsDental.exe, which these tests do not load) call the M2-EF business rules. They read the source.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace M2EFTests {
	[TestClass]
	public class WiringTests {
		private static string Read(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		///<summary>The program's project file, evaluated, so a check holds for the classic project file and the SDK-style one alike.</summary>
		private static ProjectFile ProgramProject() {
			return ProjectFile.Load(TestDb.FindRepoFile(@"OpenPmsDental\OpenPmsDental.csproj"));
		}

		[TestMethod]
		public void FieldEditors_OfferPatientFields() {
			StringAssert.Contains(Read(@"WpfControlsOD\Frms\FrmEFormTextBoxEdit.xaml.cs"),
				"EFormL.AddPatFieldDbLinks(comboDbLink,EnumEFormFieldType.TextField,EFormFieldCur.DbLink);");
			StringAssert.Contains(Read(@"WpfControlsOD\Frms\FrmEFormDateFieldEdit.xaml.cs"),
				"EFormL.AddPatFieldDbLinks(comboDbLink,EnumEFormFieldType.DateField,EFormFieldCur.DbLink);");
			StringAssert.Contains(Read(@"WpfControlsOD\Frms\FrmEFormCheckBoxEdit.xaml.cs"),
				"EFormL.AddPatFieldDbLinks(comboDbLink,EnumEFormFieldType.CheckBox,EFormFieldCur.DbLink);");
			StringAssert.Contains(Read(@"WpfControlsOD\Frms\FrmEFormRadioButtonsEdit.xaml.cs"),
				"EFormL.AddPatFieldDbLinks(comboDbLink,EnumEFormFieldType.RadioButtons,EFormFieldCur.DbLink);");
			string sourceEFormL=Read(@"WpfControlsOD\Logic\EFormL.cs");
			StringAssert.Contains(sourceEFormL,"EFormPatFields.GetDbLinksAvailable(eFormFieldType)");
			StringAssert.Contains(sourceEFormL,"EFormPatFields.GetDisplayText(dbLinkCur,isMissing:true),dbLinkCur");
		}

		[TestMethod]
		public void FillAndImport_UsePatientFields() {
			StringAssert.Contains(Read(@"OpenPmsDentalBusiness\EForms\EFormFiller.cs"),"EFormPatFields.FillFields(eForm);");
			StringAssert.Contains(Read(@"OpenPmsDentalBusiness\Data Interface\EForms.cs"),"EFormPatFields.Validate(listEFormFields[i]);");
			StringAssert.Contains(Read(@"OpenPmsDentalBusiness\Data Interface\PatFields.cs"),"EFormPatFields.UpdateDbLinksForRename(patFieldNameNew,patFieldNameOld);");
			string sourceImport=Read(@"OpenPmsDental\Forms\FormSheetImport.cs");
			StringAssert.Contains(sourceImport,"FillRowsPatFields();");
			StringAssert.Contains(sourceImport,"ImportPatFieldRows();");
			string sourceImportPatFields=Read(@"OpenPmsDental\Forms\FormSheetImport.PatFields.cs");
			StringAssert.Contains(sourceImportPatFields,"EFormPatFields.GetImports(EFormCur)");
			StringAssert.Contains(sourceImportPatFields,"importRow.DoImport=eFormPatFieldImport.IsChange() && !eFormPatFieldImport.IsClearing();");
			StringAssert.Contains(sourceImportPatFields,"EFormPatFields.Import(_patient.PatNum,listImports,");
			ProgramProject().AssertCompiles(@"Forms\FormSheetImport.PatFields.cs");
		}

		[TestMethod]
		public void LabelEditor_PastesRichText() {
			string source=Read(@"WpfControlsOD\Frms\FrmEFormLabelEdit.xaml.cs");
			StringAssert.Contains(source,"DataObject.AddPastingHandler(textLabel.richTextBox,EFormL.RichTextBox_Pasting);");
			StringAssert.Contains(source,"DataObject.AddPastingHandler(textLabelTranslated.richTextBox,EFormL.RichTextBox_Pasting);");
			string sourceEFormL=Read(@"WpfControlsOD\Logic\EFormL.cs");
			StringAssert.Contains(sourceEFormL,"xaml=EFormRichPaste.GetXamlForPaste(e.DataObject);");
			StringAssert.Contains(sourceEFormL,"e.FormatToApply=System.Windows.DataFormats.Xaml;");
		}

		[TestMethod]
		public void EFormSetup_ImportsSeveralFiles() {
			string source=Read(@"WpfControlsOD\Frms\FrmEFormDefs.xaml.cs");
			StringAssert.Contains(source,"openFileDialog.Multiselect=true;");
			StringAssert.Contains(source,"EFormDefImport.ImportFiles(openFileDialog.FileNames.ToList());");
			StringAssert.Contains(source,"MsgBox.Show(eFormDefImportResult.GetSummary());");
		}

		[TestMethod]
		public void FillEForm_SignatureTimeSubmittedAndDeleteAudit() {
			string source=Read(@"WpfControlsOD\Frms\FrmEFormFillEdit.xaml.cs");
			StringAssert.Contains(source,"EFormFields.SetDateTimeSigs(EFormCur.ListEFormFields,_listEFormFieldsOld,() => MiscData.GetNowDateTime());");
			StringAssert.Contains(source,"EForms.MakeDeleteLogEntry(EFormCur);");
			StringAssert.Contains(source,"FormSubmittedDates.TryParse(textDateSubmitted.Text,EFormCur.DateTimeSubmitted,out DateTime dateTimeSubmitted)");
			StringAssert.Contains(source,"EFormCur.DateTimeSubmitted=FormSubmittedDates.GetToSave(");
			StringAssert.Contains(Read(@"WpfControlsOD\Frms\FrmEFormFillEdit.xaml"),"x:Name=\"textDateSubmitted\"");
			string sourceFill=Read(@"WpfControlsOD\UI\Controls\UserControls\CtrlEFormFill.xaml.cs");
			StringAssert.Contains(sourceFill,"AddSigCaption(stackPanel2,eFormField);");
			StringAssert.Contains(sourceFill,"yPos+=PrintSigCaption(g,stackPanel2,eFormField,yPos+signatureBoxWrapper.ActualHeight);");
		}

		[TestMethod]
		public void FillSheet_AndPatientForms_ShowDateSubmitted() {
			string source=Read(@"OpenPmsDental\Forms\FormSheetFillEdit.cs");
			StringAssert.Contains(source,"LoadDateSubmitted();");
			StringAssert.Contains(source,"if(!TrySetDateSubmitted()) {");
			StringAssert.Contains(Read(@"OpenPmsDental\Forms\FormSheetFillEdit.Submitted.cs"),
				"SheetCur.DateTimeSubmitted=FormSubmittedDates.GetToSave(dateTimeSubmitted,SheetCur.IsNew,IsAnySignatureEntered(),DateTime.Now);");
			ProgramProject().AssertCompiles(@"Forms\FormSheetFillEdit.Submitted.cs");
			string sourceForms=Read(@"OpenPmsDental\Forms\FormPatientForms.cs");
			StringAssert.Contains(sourceForms,"new GridColumn(Lan.g(this,\"Submitted\"),115,GridSortingStrategy.DateParse)");
			StringAssert.Contains(sourceForms,"FormSubmittedDates.GetDisplayForRow(tableSubmitted,");
		}
	}
}
