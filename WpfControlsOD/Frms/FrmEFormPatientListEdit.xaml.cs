using System;
using System.Collections.Generic;
using System.Windows.Input;
using OpenDentBusiness;
using WpfControls;
using WpfControls.UI;

namespace OpenDental {
	///<summary>Edits a Patient List field (Open Dental 25.2) in the eForm designer. The label is stored in an EFormPatListLayout serialized as json into
	///ValueLabel. Not patient facing.</summary>
	public partial class FrmEFormPatientListEdit:FrmODBase {
		///<summary>This is the object being edited.</summary>
		public EFormField EFormFieldCur;
		///<summary>All the siblings.</summary>
		public List<EFormField> ListEFormFields;
		///<summary>The language showing in the parent form, or empty string for the default language.</summary>
		public string LanguageShowing="";
		///<summary>True if a translation was saved, so the parent signals other workstations.</summary>
		public bool IsChangedLanCache;
		private EFormPatListLayout _eFormPatListLayout;

		public FrmEFormPatientListEdit() {
			InitializeComponent();
			Load+=FrmEFormPatientListEdit_Load;
			PreviewKeyDown+=FrmEFormPatientListEdit_PreviewKeyDown;
		}

		private void FrmEFormPatientListEdit_Load(object sender,EventArgs e) {
			Lang.F(this);
			_eFormPatListLayout=EFormPatListLayout.Deserialize(EFormFieldCur.ValueLabel);
			if(LanguageShowing=="") {
				groupLanguage.Visible=false;
			}
			else {
				textLanguage.Text=LanguageShowing;
				textLabelTranslated.Text=LanguagePats.TranslateEFormField(EFormFieldCur.EFormFieldDefNum,LanguageShowing,_eFormPatListLayout.Label);
			}
			textLabel.Text=_eFormPatListLayout.Label;
			checkIsRequired.Checked=EFormFieldCur.IsRequired;
			checkBorder.Checked=EFormFieldCur.Border==EnumEFormBorder.ThreeD;
			textVIntFontScale.Value=EFormFieldCur.FontScale;
			int spaceBelowDefault=PrefC.GetInt(PrefName.EformsSpaceBelowEachField);
			labelSpaceDefault.Text=Lang.g(this,"leave blank to use the default value of ")+spaceBelowDefault.ToString();
			if(EFormFieldCur.SpaceBelow==-1) {
				textSpaceBelow.Text="";
			}
			else {
				textSpaceBelow.Text=EFormFieldCur.SpaceBelow.ToString();
			}
			textCondParent.Text=EFormFieldCur.ConditionalParent;
			textCondValue.Text=EFormL.ConvertCondDbToVis(ListEFormFields,EFormFieldCur.ConditionalParent,EFormFieldCur.ConditionalValue);
		}

		private void butDelete_Click(object sender,EventArgs e) {
			EFormFieldCur.IsDeleted=true;
			IsDialogOK=true;
		}

		private void butPickParent_Click(object sender,EventArgs e) {
			FrmEFormFieldPicker frmEFormFieldPicker=new FrmEFormFieldPicker();
			frmEFormFieldPicker.ListEFormFields=ListEFormFields;
			int idx=ListEFormFields.IndexOf(EFormFieldCur);
			frmEFormFieldPicker.ListSelectedIndices.Add(idx);
			frmEFormFieldPicker.ShowDialog();
			if(frmEFormFieldPicker.IsDialogCancel) {
				return;
			}
			textCondParent.Text=frmEFormFieldPicker.ParentSelected;
		}

		private void butPickValue_Click(object sender,EventArgs e) {
			textCondValue.Text=EFormL.PickCondValues(ListEFormFields,textCondParent.Text,textCondValue.Text);
		}

		private void FrmEFormPatientListEdit_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butSave.IsAltKey(Key.S,e)) {
				butSave_Click(this,new EventArgs());
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(!textVIntFontScale.IsValid()) {
				MsgBox.Show(this,"Please fix entry errors first.");
				return;
			}
			int spaceBelow=-1;
			if(textSpaceBelow.Text!="") {
				try {
					spaceBelow=Convert.ToInt32(textSpaceBelow.Text);
				}
				catch {
					MsgBox.Show(this,"Please fix error in Space Below first.");
					return;
				}
				if(spaceBelow<0 || spaceBelow>200) {
					MsgBox.Show(this,"Space Below value is invalid.");
					return;
				}
			}
			if(textLabel.Text.Trim()=="") {
				MsgBox.Show(this,"Please enter a label.");
				return;
			}
			//end of validation
			if(LanguageShowing!="") {
				IsChangedLanCache=LanguagePats.SaveTranslationEFormField(EFormFieldCur.EFormFieldDefNum,LanguageShowing,textLabelTranslated.Text);
				if(IsChangedLanCache) {
					LanguagePats.RefreshCache();
				}
			}
			_eFormPatListLayout.Label=textLabel.Text;
			EFormFieldCur.ValueLabel=_eFormPatListLayout.Serialize();
			EFormFieldCur.IsRequired=checkIsRequired.Checked==true;
			EFormFieldCur.Border=checkBorder.Checked==true ? EnumEFormBorder.ThreeD : EnumEFormBorder.None;
			EFormFieldCur.FontScale=textVIntFontScale.Value;
			EFormFieldCur.SpaceBelow=spaceBelow;
			EFormFieldCur.ConditionalParent=textCondParent.Text;
			EFormFieldCur.ConditionalValue=EFormL.ConvertCondVisToDb(ListEFormFields,textCondParent.Text,textCondValue.Text);
			//not saved to db here. That happens when clicking Save in parent window.
			IsDialogOK=true;
		}
	}
}
