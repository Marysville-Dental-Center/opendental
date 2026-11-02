using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	/// <summary>
	/// Summary description for FormBasicTemplate.
	/// </summary>
	public partial class FormEmailTemplateEdit : FormODBase {
		///<summary></summary>
		public bool IsNew;
		///<summary></summary>
		public EmailTemplate EmailTemplateCur;
		private List<EmailAttach> _listEmailAttachesDisplayed;
		private List<EmailAttach> _listEmailAttachesOld=new List<EmailAttach>();
		private bool _hasTextChanged;
		private bool _isRaw;

		///<summary>The fully translated HTML text with the master template, as it would show in a web browser.</summary>
		private string _htmlDocument;

		///<summary></summary>
		public FormEmailTemplateEdit()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			webViewHtml.LinkClicked+=webViewHtml_LinkClicked;
		}

		private void FormEmailTemplateEdit_Load(object sender, System.EventArgs e) {
			gridAttachments.ContextMenu=contextMenuAttachments;
			textSubject.Text=EmailTemplateCur.Subject;
			textDescription.Text=EmailTemplateCur.Description;
			if(EmailTemplateCur.EmailTemplateNum==0) {//New email template
				_listEmailAttachesDisplayed=new List<EmailAttach>();
				EmailTemplateCur.BodyText="";
			}
			else {
				_listEmailAttachesDisplayed=EmailAttaches.GetForTemplate(EmailTemplateCur.EmailTemplateNum); 
				for(int i=0;i<_listEmailAttachesDisplayed.Count;i++) {
					_listEmailAttachesOld.Add(_listEmailAttachesDisplayed[i].Copy());
				}
			}
			textBodyText.Text=EmailTemplateCur.BodyText;
			if(EmailMessages.IsHtmlEmail(EmailTemplateCur.TemplateType)) {
				_hasTextChanged=true;
				_isRaw=(EmailTemplateCur.TemplateType==EmailType.RawHtml);
				ChangeView(isHtml:true);
			}
			FillAttachments();
		}

		private void FillAttachments() {
			gridAttachments.BeginUpdate();
			gridAttachments.ListGridRows.Clear();
			gridAttachments.Columns.Clear();
			GridColumn col=new GridColumn("",90); //No name column, since there is only one column.
			col.IsWidthDynamic=true;
			gridAttachments.Columns.Add(col);
			for(int i=0;i<_listEmailAttachesDisplayed.Count;i++) {
				GridRow row=new GridRow();
				row.Cells.Add(_listEmailAttachesDisplayed[i].DisplayedFileName);
				gridAttachments.ListGridRows.Add(row);
			}
			gridAttachments.EndUpdate();
			if(gridAttachments.ListGridRows.Count>0) {
				gridAttachments.SetSelected(0,true);
			}
		}
		
		private void butAttach_Click(object sender,EventArgs e) {
			_listEmailAttachesDisplayed.AddRange(EmailAttachL.PickAttachments(null));
			FillAttachments();
		}

		private void gridAttachments_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			EmailAttach emailAttach=_listEmailAttachesDisplayed[gridAttachments.SelectedIndices[0]];
			FileAtoZ.OpenFile(FileAtoZ.CombinePaths(EmailAttaches.GetAttachPath(),emailAttach.ActualFileName),emailAttach.DisplayedFileName);
		}

		private void butSubjectFields_Click(object sender,EventArgs e) {
			List<MessageReplaceType> listMessageReplaceTypes=new List<MessageReplaceType>();
			listMessageReplaceTypes.Add(MessageReplaceType.Appointment);
			listMessageReplaceTypes.Add(MessageReplaceType.Office);
			listMessageReplaceTypes.Add(MessageReplaceType.Patient);
			listMessageReplaceTypes.Add(MessageReplaceType.User);
			listMessageReplaceTypes.Add(MessageReplaceType.Misc);
			FrmMessageReplacements frmMessageReplacements=new FrmMessageReplacements(listMessageReplaceTypes);
			frmMessageReplacements.IsSelectionMode=true;
			frmMessageReplacements.ShowDialog();
			if(frmMessageReplacements.IsDialogOK) {
				textSubject.SelectedText=frmMessageReplacements.ReplacementTextSelected;
			}
		}

		private void butBodyFields_Click(object sender,EventArgs e) {
			List<MessageReplaceType> listMessageReplaceTypes=new List<MessageReplaceType>();
			listMessageReplaceTypes.Add(MessageReplaceType.Appointment);
			listMessageReplaceTypes.Add(MessageReplaceType.Office);
			listMessageReplaceTypes.Add(MessageReplaceType.Patient);
			listMessageReplaceTypes.Add(MessageReplaceType.User);
			listMessageReplaceTypes.Add(MessageReplaceType.Misc);
			FrmMessageReplacements frmMessageReplacements=new FrmMessageReplacements(listMessageReplaceTypes);
			frmMessageReplacements.IsSelectionMode=true;
			frmMessageReplacements.ShowDialog();
			if(frmMessageReplacements.IsDialogOK) {
				textBodyText.SelectedText=frmMessageReplacements.ReplacementTextSelected;
			}
		}

		private void gridAttachments_MouseDown(object sender,MouseEventArgs e) {
			//A right click also needs to select an items so that the context menu will work properly.
			if(e.Button==MouseButtons.Right) {
				int clickedIndex=gridAttachments.PointToRow(e.Y);
				if(clickedIndex!=-1) {
					gridAttachments.SetSelected(clickedIndex,true);
				}
			}
		}

		private void contextMenuAttachments_Popup(object sender,EventArgs e) {
			menuItemOpen.Enabled=false;
			menuItemRename.Enabled=false;
			menuItemRemove.Enabled=false;
			if(gridAttachments.SelectedIndices.Length > 0) {
				menuItemOpen.Enabled=true;
				menuItemRename.Enabled=true;
				menuItemRemove.Enabled=true;
			}
		}

		private void menuItemOpen_Click(object sender,EventArgs e) {
			EmailAttach emailAttach=_listEmailAttachesDisplayed[gridAttachments.SelectedIndices[0]];
			FileAtoZ.OpenFile(FileAtoZ.CombinePaths(EmailAttaches.GetAttachPath(),emailAttach.ActualFileName),emailAttach.DisplayedFileName);
		}

		private void menuItemRename_Click(object sender,EventArgs e) {
			InputBoxParam inputBoxParam=new InputBoxParam();
			inputBoxParam.InputBoxType_=InputBoxType.TextBox;
			inputBoxParam.LabelText=Lan.g(this,"Filename");
			EmailAttach emailAttach=_listEmailAttachesDisplayed[gridAttachments.SelectedIndices[0]];
			inputBoxParam.Text=emailAttach.DisplayedFileName;
			InputBox inputBox=new InputBox(inputBoxParam);
			inputBox.ShowDialog();
			if(inputBox.IsDialogCancel) {
				return;
			}
			emailAttach.DisplayedFileName=inputBox.StringResult;
			FillAttachments();
		}

		private void menuItemRemove_Click(object sender,EventArgs e) {
			EmailAttach emailAttach=_listEmailAttachesDisplayed[gridAttachments.SelectedIndices[0]];
			_listEmailAttachesDisplayed.Remove(emailAttach);
			FillAttachments();
		}

		private void butEditHtml_Click(object sender,EventArgs e) {
			//get the most current version of the "plain" text from the emailPreview text box.
			using FormEmailEdit formEmailEdit=new FormEmailEdit();
			formEmailEdit.MarkupText=textBodyText.Text;//Copy existing text in case user decided to compose HTML after starting their email.
			formEmailEdit.IsRaw=_isRaw;
			formEmailEdit.ShowDialog();
			if(formEmailEdit.DialogResult!=DialogResult.OK) {
				return;
			}
			textBodyText.Text=formEmailEdit.MarkupText;
			_hasTextChanged=true;
			_isRaw=formEmailEdit.IsRaw;
			ChangeView(isHtml:true);
		}

		private void butEditText_Click(object sender,EventArgs e) {
			_isRaw=false;
			ChangeView(isHtml:false);
		}

		private void textBodyText_KeyDown(object sender,KeyEventArgs e) {
			_hasTextChanged=true;
		}
		///<summary>A link clicked within the email message: the preview never follows it. An http, https or mailto link opens in the default
		///browser or mail program instead; a link without a scheme, such as www.example.com, counts as http (WebView2Policy.GetSystemBrowserUrl).</summary>
		private void webViewHtml_LinkClicked(object sender,CodeBase.Controls.ODWebView2LinkEventArgs e) {
			CodeBase.Controls.WebView2Policy.OpenInSystemBrowser(e.GetSystemBrowserUrl());
		}

		///<summary>Refreshes the email preview pane to show the web browser when viewing HTML and the editable text if not.</summary>
		public void ChangeView(bool isHtml) {
			if(_hasTextChanged) {
				//plain text box changed, grab the new plain text string and translate into html, regardless of if this is currently an html message or not.
				try {
					string htmlText="";
					if(_isRaw) {
						htmlText=textBodyText.Text;
						_htmlDocument=htmlText;
					}
					else {
						//handle aggregation of the full document text with the template ourselves so we can display properly but only save the html string. 
						htmlText=MarkupEdit.TranslateToXhtml(textBodyText.Text,isPreviewOnly:false,hasWikiPageTitles:false,isEmail:true,canAggregate:false);
						_htmlDocument=PrefC.GetString(PrefName.EmailMasterTemplate).Replace("@@@body@@@",htmlText);
					}
					_hasTextChanged=false;
				}
				catch(Exception ex) {
					FriendlyException.Show("Error loading HTML.",ex);
				}
			}
			if(isHtml || _isRaw) {
				try {
					textBodyText.Visible=false;
					webViewHtml.Visible=true;
					//The office's own template: its images come from the email images folder or the web, as they will for the recipient.
					webViewHtml.NavigateToHtml(_htmlDocument,isRemoteContentAllowed:true,listFoldersAllowed:MarkupEdit.GetImageFolders(isEmail:true));
					webViewHtml.BringToFront();
				}
				catch(Exception ex) {
					ex.DoNothing();
					//invalid preview
				}
			}
			else {
				//load the plain text
				webViewHtml.Visible=false;
				textBodyText.Visible=true;
				textBodyText.BringToFront();
			}
		}

		private void butSave_Click(object sender, System.EventArgs e) {
			if(textSubject.Text=="" && textBodyText.Text==""){
				MsgBox.Show(this,"Both the subject and body of the template cannot be left blank.");
				return;
			}
			if(textDescription.Text==""){
				MsgBox.Show(this,"Please enter a description.");
				return;
			}
			EmailTemplateCur.Subject=textSubject.Text;
			EmailTemplateCur.BodyText=textBodyText.Text;//always save as plain text version. We will translate to html on loading. 
			EmailTemplateCur.Description=textDescription.Text;
			if(!webViewHtml.Visible) {
				EmailTemplateCur.TemplateType=EmailType.Regular;
			}
			else {
				EmailTemplateCur.TemplateType=EmailType.Html;
				if(_isRaw) {
					EmailTemplateCur.TemplateType=EmailType.RawHtml;
				}
			}
			if(IsNew){
				EmailTemplates.Insert(EmailTemplateCur);
			}
			else{
				EmailTemplates.Update(EmailTemplateCur);
			}
			for(int i=0;i<_listEmailAttachesDisplayed.Count;i++) {
				_listEmailAttachesDisplayed[i].EmailTemplateNum=EmailTemplateCur.EmailTemplateNum;
			}
			//Sync the email attachments and pass in an emailMessageNum of 0 because we will be providing _listEmailAttachOld.
			EmailAttaches.Sync(0,_listEmailAttachesDisplayed,_listEmailAttachesOld);
			DialogResult=DialogResult.OK;
		}

	}
}