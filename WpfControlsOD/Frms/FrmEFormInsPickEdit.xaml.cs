using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using OpenDentBusiness;
using WpfControls;
using WpfControls.UI;

namespace OpenDental {
	///<summary>Edits an Ins Pick field (Open Dental 25.2) in the eForm designer: label, insurance slot (the ins1PlanFromList or ins2PlanFromList link),
	///the three option labels (PickListVis), Auto Import, layout and conditional logic. There is no Required box: an empty answer means None, which
	///is always a valid answer. Not patient facing.</summary>
	public partial class FrmEFormInsPickEdit:FrmODBase {
		///<summary>This is the object being edited.</summary>
		public EFormField EFormFieldCur;
		///<summary>All the siblings.</summary>
		public List<EFormField> ListEFormFields;
		///<summary>The language showing in the parent form, or empty string for the default language.</summary>
		public string LanguageShowing="";
		///<summary>True if a translation was saved, so the parent signals other workstations.</summary>
		public bool IsChangedLanCache;
		private List<string> _listDbLinks;

		public FrmEFormInsPickEdit() {
			InitializeComponent();
			Load+=FrmEFormInsPickEdit_Load;
			PreviewKeyDown+=FrmEFormInsPickEdit_PreviewKeyDown;
			comboSlot.SelectionChangeCommitted+=comboSlot_SelectionChangeCommitted;
		}

		private void FrmEFormInsPickEdit_Load(object sender,EventArgs e) {
			Lang.F(this);
			List<string> listOptions=EFormInsPick.GetOptions(EFormFieldCur.PickListVis);
			if(LanguageShowing=="") {
				groupLanguage.Visible=false;
			}
			else {
				textLanguage.Text=LanguageShowing;
				string strTranslations=LanguagePats.TranslateEFormField(EFormFieldCur.EFormFieldDefNum,LanguageShowing,
					EFormFieldCur.ValueLabel+"|"+string.Join("|",listOptions));
				List<string> listTranslations=strTranslations.Split('|').ToList();
				if(listTranslations.Count!=4) {
					listTranslations=new List<string>() { EFormFieldCur.ValueLabel }.Concat(listOptions).ToList();
				}
				textLabelTranslated.Text=listTranslations[0];
				textOptionNoneTranslated.Text=listTranslations[1];
				textOptionNewTranslated.Text=listTranslations[2];
				textOptionExistingTranslated.Text=listTranslations[3];
			}
			_listDbLinks=EFormFieldsAvailable.GetList_InsPick();
			comboSlot.Items.Add(Lang.g(this,"None (ordinal 0)"));
			comboSlot.Items.Add(Lang.g(this,"Insurance 1")+" ("+EFormInsPick.DBLINK_INS1+")");
			comboSlot.Items.Add(Lang.g(this,"Insurance 2")+" ("+EFormInsPick.DBLINK_INS2+")");
			int idxSlot=_listDbLinks.IndexOf(EFormFieldCur.DbLink);
			comboSlot.SelectedIndex=idxSlot<0 ? 0 : idxSlot;
			textLabel.Text=EFormFieldCur.ValueLabel;
			textOptionNone.Text=listOptions[0];
			textOptionNew.Text=listOptions[1];
			textOptionExisting.Text=listOptions[2];
			checkAutoImport.Checked=EFormFieldCur.AutoImport;
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
			SetAutoImportEnabled();
		}

		private void comboSlot_SelectionChangeCommitted(object sender,EventArgs e) {
			SetAutoImportEnabled();
		}

		///<summary>Only the ins1PlanFromList and ins2PlanFromList links may Auto Import.</summary>
		private void SetAutoImportEnabled() {
			bool isAllowed=comboSlot.SelectedIndex>0;
			checkAutoImport.IsEnabled=isAllowed;
			if(!isAllowed) {
				checkAutoImport.Checked=false;
			}
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

		private void FrmEFormInsPickEdit_PreviewKeyDown(object sender,KeyEventArgs e) {
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
				MsgBox.Show(this,"Please enter a label. Child fields name it as their conditional parent.");
				return;
			}
			List<string> listOptions=new List<string>() { textOptionNone.Text.Trim(),textOptionNew.Text.Trim(),textOptionExisting.Text.Trim() };
			if(listOptions.Any(x => x=="" || x.Contains("|"))) {
				MsgBox.Show(this,"Each of the three choices needs a label, without the | character.");
				return;
			}
			if(textLabel.Text.Contains("|")) {
				MsgBox.Show(this,"The label can't contain the | character.");
				return;
			}
			string dbLinkNew=_listDbLinks[comboSlot.SelectedIndex];
			if(dbLinkNew=="None") {
				dbLinkNew="";
			}
			if(dbLinkNew!="" && ListEFormFields.Exists(x => x!=EFormFieldCur && x.FieldType==EnumEFormFieldType.InsPick && x.DbLink==dbLinkNew)) {
				MsgBox.Show(this,"Another Ins Pick field on this form already asks about that insurance.");
				return;
			}
			//end of validation
			if(LanguageShowing!="") {
				string strTranslations=string.Join("|",new List<string>() { textLabelTranslated.Text,textOptionNoneTranslated.Text,textOptionNewTranslated.Text,
					textOptionExistingTranslated.Text });
				if(strTranslations.Replace("|","").Trim()=="") {
					strTranslations="";//deletes the translation
				}
				IsChangedLanCache=LanguagePats.SaveTranslationEFormField(EFormFieldCur.EFormFieldDefNum,LanguageShowing,strTranslations);
				if(IsChangedLanCache) {
					LanguagePats.RefreshCache();
				}
			}
			string labelOld=EFormFieldCur.ValueLabel;
			List<string> listOptionsOld=EFormInsPick.GetOptions(EFormFieldCur.PickListVis);
			EFormFieldCur.ValueLabel=textLabel.Text;
			EFormFieldCur.DbLink=dbLinkNew;
			EFormFieldCur.PickListVis=string.Join("|",listOptions);
			EFormFieldCur.PickListDb="";//not used by Ins Pick
			EFormFieldCur.AutoImport=checkAutoImport.Checked==true && dbLinkNew!="";
			EFormFieldCur.IsRequired=false;
			EFormFieldCur.Border=checkBorder.Checked==true ? EnumEFormBorder.ThreeD : EnumEFormBorder.None;
			EFormFieldCur.FontScale=textVIntFontScale.Value;
			EFormFieldCur.SpaceBelow=spaceBelow;
			EFormFieldCur.ConditionalParent=textCondParent.Text;
			EFormFieldCur.ConditionalValue=EFormL.ConvertCondVisToDb(ListEFormFields,textCondParent.Text,textCondValue.Text);
			//Keep the children attached when the label changes: they name this field as their parent.
			string labelOldTrunc=labelOld.Substring(0,Math.Min(labelOld.Length,255));
			string labelNewTrunc=EFormFieldCur.ValueLabel.Substring(0,Math.Min(EFormFieldCur.ValueLabel.Length,255));
			for(int i=0;i<ListEFormFields.Count;i++) {
				if(ListEFormFields[i]==EFormFieldCur || labelOldTrunc=="" || ListEFormFields[i].ConditionalParent!=labelOldTrunc) {
					continue;
				}
				ListEFormFields[i].ConditionalParent=labelNewTrunc;
				//Children compare the option text, so a renamed option is renamed in their condition too.
				List<string> listCondValues=ListEFormFields[i].ConditionalValue.Split('|').ToList();
				for(int c=0;c<listCondValues.Count;c++) {
					int idxOption=listOptionsOld.IndexOf(listCondValues[c]);
					if(idxOption>=0) {
						listCondValues[c]=listOptions[idxOption];
					}
				}
				ListEFormFields[i].ConditionalValue=string.Join("|",listCondValues);
			}
			//not saved to db here. That happens when clicking Save in parent window.
			IsDialogOK=true;
		}
	}
}
