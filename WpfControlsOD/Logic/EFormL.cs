using OpenPmsDental;
using OpenPmsDentalBusiness;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfControls.UI;
using CodeBase;

namespace WpfControls {
	public class EFormL {
		/// <summary>Shows InputBox to user to let them pick a value. Pass in an existingValue to prefill.</summary>
		public static string PickCondValues(List<EFormField> listEFormFields,string condParent,string existingValues) {
			if(condParent==""){
				MsgBox.Show("Please enter a name in the Parent field first.");
				return existingValues;
			}
			List<EFormField> listEFormFieldsParent=listEFormFields.FindAll(x=>x.ValueLabel==condParent);
			if(listEFormFieldsParent.Count==0){
				MsgBox.Show("Parent field name could not be found.");
				return existingValues;
			}
			if(listEFormFieldsParent.Count>1){
				MsgBox.Show("There are duplicate fields with that parent name.");
				return existingValues;
			}
			EFormField eFormFieldParent=listEFormFieldsParent[0];
			if(eFormFieldParent.FieldType==EnumEFormFieldType.RadioButtons || eFormFieldParent.FieldType==EnumEFormFieldType.InsPick){
				//Ins Pick (Open Dental 25.2) offers its three choices; children normally use New.
				List<string> listPick=eFormFieldParent.PickListVis.Split('|').ToList();
				if(eFormFieldParent.FieldType==EnumEFormFieldType.InsPick){
					listPick=EFormInsPick.GetOptions(eFormFieldParent.PickListVis);
				}
				if(eFormFieldParent.FieldType==EnumEFormFieldType.RadioButtons && eFormFieldParent.DbLink!=""){//Ins Pick has no PickListDb
					//parent has a db link, so we might need to use some of those values if the Vis items are empty
					List<string> listPickDb=eFormFieldParent.PickListDb.Split('|').ToList();
					for(int i=0;i<listPick.Count;i++){
						if(listPick[i]==""){
							listPick[i]=listPickDb[i];
						}
					}
				}
				List<string> listExistingValues=existingValues.Split('|').ToList();
				List<int> listIndicesSelected=new List<int>();
				for(int i=0;i<listExistingValues.Count;i++){
					int idxSelected=listPick.IndexOf(listExistingValues[i]);
					if(idxSelected==-1){
						continue;
					}
					listIndicesSelected.Add(idxSelected);
				}
				InputBoxParam inputBoxParam=new InputBoxParam();
				inputBoxParam.InputBoxType_=InputBoxType.ListBoxMulti;
				inputBoxParam.LabelText="To pick multiple values, hold down Ctrl key";
				inputBoxParam.ListSelections=listPick;
				inputBoxParam.ListIndicesSelected=listIndicesSelected;
				InputBox inputBox=new InputBox(inputBoxParam);
				inputBox.ShowDialog();
				if(inputBox.IsDialogCancel){
					return existingValues;
				}
				//yes, this works even if no selected index. ListBoxMulti forces at least one selection
				string retVal="";
				for(int i=0;i<inputBox.SelectedIndices.Count;i++){
					if(i>0){
						retVal+="|";
					}
					retVal+=listPick[inputBox.SelectedIndices[i]];
				}
				return retVal;
			}
			if(eFormFieldParent.FieldType==EnumEFormFieldType.CheckBox){
				List<string> listStrCheck=new List<string>();
				listStrCheck.Add("Checked");
				listStrCheck.Add("Unchecked");
				int idxSelected=listStrCheck.IndexOf(existingValues);//can be -1;
				InputBox inputBox=new InputBox(prompt:"",listSelections:listStrCheck,selectedIndex:idxSelected);
				inputBox.ShowDialog();
				if(inputBox.IsDialogCancel) {
					return existingValues;
				}
				return listStrCheck[inputBox.SelectedIndex];
			}
			if(eFormFieldParent.FieldType==EnumEFormFieldType.DateField){
				List<InputBoxParam> listInputBoxParams=new List<InputBoxParam>();
				InputBoxParam inputBoxParamYounger=new InputBoxParam();
				inputBoxParamYounger.InputBoxType_=InputBoxType.RadioButton;
				inputBoxParamYounger.Text="Younger than";
				listInputBoxParams.Add(inputBoxParamYounger);
				InputBoxParam inputBoxParamOlder=new InputBoxParam();
				inputBoxParamOlder.InputBoxType_=InputBoxType.RadioButton;
				inputBoxParamOlder.Text="Older than";
				listInputBoxParams.Add(inputBoxParamOlder);
				InputBoxParam inputBoxAge=new InputBoxParam();
				inputBoxAge.InputBoxType_=InputBoxType.TextBox;
				inputBoxAge.LabelText="Age";
				listInputBoxParams.Add(inputBoxAge);
				InputBox inputBox=new InputBox(listInputBoxParams);
				//inputBox.SizeInitial=new System.Windows.Size(200,200);
				inputBox.ShowDialog();
				if(inputBox.IsDialogCancel) {
					return existingValues;
				}
				RadioButton radioButtonYounger=(RadioButton)inputBox.ListControls[0];
				RadioButton radioButtonOlder=(RadioButton)inputBox.ListControls[1];
				TextBox textAge=(TextBox)inputBox.ListControls[2];
				string retVal="";
				if(radioButtonYounger.Checked){
					retVal+="<";
				}
				if(radioButtonOlder.Checked){
					retVal+=">";
				}
				retVal+=textAge.Text;
				return retVal;
			}
			return existingValues;
		}

		///<summary>This handles the problem of parent labels that are longer than 255. Also if the parent is a checkBox, then this converts "" or "X" to Checked or Unchecked to make it more user friendly for display. It doesn't alter other types.</summary>
		public static string ConvertCondDbToVis(List<EFormField> listEFormFields,string parent,string dbVal){
			//if(parent==""){//this would have worked, but I would rather roll it into the predicate below so that the same predicate can be used elsewhere
			//	return dbVal;
			//}
			//So the trick here is that the parent value coming from the db won't be >255, but eFormFieldParent.ValueLabel certainly could be
			EFormField eFormFieldParent=listEFormFields.Find(x=>
				x.ValueLabel!=""
				&& x.ValueLabel.Substring(0,Math.Min(x.ValueLabel.Length,255))==parent
				&& x.FieldType.In(EnumEFormFieldType.CheckBox,EnumEFormFieldType.RadioButtons,EnumEFormFieldType.DateField,EnumEFormFieldType.InsPick));
			//note that in the linq above, we did not validate dbVal in any way. We're just trying to find the parent.
			if(eFormFieldParent is null){
				return dbVal;
			}
			if(eFormFieldParent.FieldType!=EnumEFormFieldType.CheckBox) {
				return dbVal;
			}
			if(dbVal==""){
				return "Unchecked";
			}
			if(dbVal=="X"){
				return "Checked";
			}
			return dbVal;
		}

		///<summary>This handles the problem of parent labels that are longer than 255. Also if the parent is a checkBox, then this converts Checked or Unchecked to "" or "X" for storage in db. It doesn't alter other types.</summary>
		public static string ConvertCondVisToDb(List<EFormField> listEFormFields,string parent,string visVal){
			EFormField eFormFieldParent=listEFormFields.Find(x=>
				x.ValueLabel!=""
				&& x.ValueLabel.Substring(0,Math.Min(x.ValueLabel.Length,255))==parent
				&& x.FieldType.In(EnumEFormFieldType.CheckBox,EnumEFormFieldType.RadioButtons,EnumEFormFieldType.DateField,EnumEFormFieldType.InsPick));
			if(eFormFieldParent is null){
				return visVal;
			}
			if(eFormFieldParent.FieldType!=EnumEFormFieldType.CheckBox) {
				return visVal;
			}
			if(visVal=="Unchecked"){
				return "";
			}
			if(visVal=="Checked"){
				return "X";
			}
			return "";
		}

		///<summary>Fills a combo language box on a variety of eForm windows. Sets it visible false if the office has not set up any languages.</summary>
		public static void FillComboLanguage(ComboBox comboLanguage){
			comboLanguage.Items.Clear();
			comboLanguage.Items.Add(Lang.g("EFormLanguage","Default"));
			comboLanguage.SelectedIndex=0;
			List<string> listLangs=LanguagePats.GetLanguagesForCombo();
			if(listLangs.Count==0){
				comboLanguage.Visible=false;
				return;
			}
			for(int i = 0;i<listLangs.Count;i++){
				comboLanguage.Items.Add(listLangs[i]);
			}
		}

		///<summary>Open Dental 25.4: adds the office's patient fields of a compatible type to a field editor's database link combo box, after the
		///built-in links. Call after the editor selected the current link. When the current link is a patient field, it is selected; a missing one
		///(deleted, hidden or of another type) is added as "PatField:Name (missing)" so the link is kept when the field is saved.</summary>
		public static void AddPatFieldDbLinks(ComboBox comboDbLink,EnumEFormFieldType eFormFieldType,string dbLinkCur){
			List<string> listDbLinks=EFormPatFields.GetDbLinksAvailable(eFormFieldType);
			for(int i=0;i<listDbLinks.Count;i++){
				comboDbLink.Items.Add(listDbLinks[i],listDbLinks[i]);
			}
			if(!EFormPatFields.IsPatFieldLink(dbLinkCur)){
				return;
			}
			if(!listDbLinks.Contains(dbLinkCur)){
				comboDbLink.Items.Add(EFormPatFields.GetDisplayText(dbLinkCur,isMissing:true),dbLinkCur);
			}
			for(int i=0;i<comboDbLink.Items.Count;i++){
				if(comboDbLink.Items.GetObjectAt(i) as string==dbLinkCur){
					comboDbLink.SelectedIndex=i;
					break;
				}
			}
		}

		///<summary>The label a field gets when the user picks a database link and the label is still empty.</summary>
		public static string GetDefaultLabelForDbLink(string dbLink){
			return EFormPatFields.GetDefaultLabel(dbLink);
		}

		///<summary>Open Dental 25.4: attach to a label editor's rich text box with DataObject.AddPastingHandler. Pasted RTF (Word, e-mail) or HTML
		///(browsers) keeps basic formatting (see EFormRichPaste). Plain text, and rich text copied inside this program, paste as before.</summary>
		public static void RichTextBox_Pasting(object sender,System.Windows.DataObjectPastingEventArgs e){
			string xaml=null;
			try{
				xaml=EFormRichPaste.GetXamlForPaste(e.DataObject);
			}
			catch(Exception ex){
				ex.DoNothing();//the normal paste runs
				return;
			}
			if(xaml==null){
				return;
			}
			System.Windows.DataObject dataObject=new System.Windows.DataObject();
			dataObject.SetData(System.Windows.DataFormats.Xaml,xaml);
			e.DataObject=dataObject;
			e.FormatToApply=System.Windows.DataFormats.Xaml;
		}
	}
}
