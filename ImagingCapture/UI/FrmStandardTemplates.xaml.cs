// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingCapture.Templates;
using OpenDental;
using OpenDentBusiness;

namespace ImagingCapture.UI {
	///<summary>"Add standard templates" (design 9.4 option a), opened from FormImagingDevices: the standard FMX and 4BW mount definitions
	///(StandardMountTemplates) with the X-ray category the user picks. It shows what will be added and what is left alone because a mount with
	///the name exists, and adds on the user's word. Needs the Setup permission; each added template is logged. All decisions are in
	///StandardTemplateInstaller and StandardTemplatePlanner; this window shows them.</summary>
	public partial class FrmStandardTemplates:FrmODBase {
		private readonly StandardTemplateInstaller _installer;
		private ImageCategoryRule _imageCategoryRule;
		///<summary>The category of each comboCategory item; 0 is none chosen.</summary>
		private List<long> _listCategoryDefNums=new List<long>();
		private bool _isAdded;

		public FrmStandardTemplates(StandardTemplateInstaller installer) {
			InitializeComponent();
			_installer=installer??throw new ArgumentNullException(nameof(installer));
			Load+=FrmStandardTemplates_Load;
			comboCategory.SelectionChangeCommitted+=comboCategory_SelectionChangeCommitted;
		}

		///<summary>Opens the window, modal. Needs Setup (the refusal is shown). Returns true when templates were added.</summary>
		public static bool ShowModal() {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return false;
			}
			FrmStandardTemplates frmStandardTemplates=new FrmStandardTemplates(new StandardTemplateInstaller(new OdStandardTemplateStore()));
			frmStandardTemplates.ShowDialog();
			return frmStandardTemplates._isAdded;
		}

		#region Methods - Event Handlers
		private void FrmStandardTemplates_Load(object sender,EventArgs e) {
			Lang.F(this);
			labelIntro.Text=StandardTemplateTexts.Intro;
			_imageCategoryRule=new ImageCategoryRule();
			comboCategory.Items.Clear();
			_listCategoryDefNums=new List<long>();
			comboCategory.Items.Add(Lang.g(this,"(choose)"));
			_listCategoryDefNums.Add(0);
			foreach(ImageCategoryItem item in _imageCategoryRule.GetPickList(0)) {
				comboCategory.Items.Add(item.Name);
				_listCategoryDefNums.Add(item.DefNum);
			}
			comboCategory.SelectedIndex=0;
			RefreshView();
		}

		private void comboCategory_SelectionChangeCommitted(object sender,EventArgs e) {
			RefreshView();
		}

		private void butAdd_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			long categoryDefNum=GetCategoryDefNum();
			if(categoryDefNum==0) {
				MsgBox.Show(Lang.g(this,"Choose the X-ray category the templates save into."));
				return;
			}
			StandardTemplatePlan plan;
			try {
				plan=_installer.GetPlan(categoryDefNum);
			}
			catch(Exception ex) {
				MsgBox.Show(Lang.g(this,"The mount templates could not be read:")+" "+ex.Message);
				return;
			}
			if(plan.ListToAdd.Count==0) {
				MsgBox.Show(Lang.g(this,"Every standard template exists already; nothing is added."));
				return;
			}
			string categoryName=_imageCategoryRule.Find(categoryDefNum)?.Name??"";
			string question=Lang.g(this,"Add these mount templates, saving into")+" \""+categoryName+"\"?\r\n\r\n"
				+string.Join("\r\n",plan.ListToAdd.ConvertAll(x => x.Template.Description));
			string warning=_imageCategoryRule.GetWarning(categoryDefNum,"X-ray","X-rays");
			if(warning!="") {
				question+="\r\n\r\n"+warning;
			}
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,question)) {
				return;
			}
			StandardTemplateResult result=_installer.Install(categoryDefNum,categoryName);
			if(result.ListAdded.Count>0) {
				_isAdded=true;
			}
			string resultText=StandardTemplateTexts.GetResultText(result);
			RefreshView();
			textPlan.Text=resultText+"\r\n\r\n"+textPlan.Text;
			MsgBox.Show(resultText);
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}
		#endregion Methods - Event Handlers

		private long GetCategoryDefNum() {
			int idx=comboCategory.SelectedIndex;
			return idx>=0 && idx<_listCategoryDefNums.Count ? _listCategoryDefNums[idx] : 0;
		}

		private void RefreshView() {
			long categoryDefNum=GetCategoryDefNum();
			labelCategoryWarning.Text=categoryDefNum==0 ? "" : _imageCategoryRule.GetWarning(categoryDefNum,"X-ray","X-rays");
			try {
				textPlan.Text=StandardTemplateTexts.GetPlanText(_installer.GetPlan(categoryDefNum));
			}
			catch(Exception ex) {
				textPlan.Text=Lang.g(this,"The mount templates could not be read:")+" "+ex.Message;
			}
			butAdd.IsEnabled=categoryDefNum!=0;
		}
	}
}
