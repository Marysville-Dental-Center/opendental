using System;
using System.Collections.Generic;
using System.Windows.Input;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	///<summary>The Existing popup of an eForm Ins Pick field (Open Dental 25.2): the family's current insurance and pending insurance, read live. After OK,
	///ValueSelected holds "InsSubNum:####" or "InsPendingNum:####".</summary>
	public partial class FrmEFormInsPickExisting:FrmODBase {
		private long _patNumForm;
		private List<EFormInsPickChoice> _listChoices;
		///<summary>Set before showing to preselect. After OK, the picked value.</summary>
		public string ValueSelected="";

		public FrmEFormInsPickExisting(long patNumForm) {
			InitializeComponent();
			_patNumForm=patNumForm;
			Load+=FrmEFormInsPickExisting_Load;
			listIns.MouseDoubleClick+=listIns_MouseDoubleClick;
			PreviewKeyDown+=FrmEFormInsPickExisting_PreviewKeyDown;
		}

		private void FrmEFormInsPickExisting_Load(object sender,EventArgs e) {
			Lang.F(this);
			_listChoices=EFormInsPick.GetExistingChoices(_patNumForm);
			listIns.Items.Clear();
			listIns.Items.AddList(_listChoices,x => x.Display);
			int idx=_listChoices.FindIndex(x => x.Value==ValueSelected);
			if(idx>=0) {
				listIns.SetSelected(idx);
			}
			if(_listChoices.Count==0) {
				labelHead.Text=Lang.g(this,"No one in your family has insurance on file. Pick New instead.");
			}
		}

		private void listIns_MouseDoubleClick(object sender,MouseButtonEventArgs e) {
			if(listIns.SelectedIndex==-1) {
				return;
			}
			ValueSelected=listIns.GetSelected<EFormInsPickChoice>().Value;
			IsDialogOK=true;
		}

		private void FrmEFormInsPickExisting_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butOK.IsAltKey(Key.O,e)) {
				butOK_Click(this,new EventArgs());
			}
		}

		private void butOK_Click(object sender,EventArgs e) {
			if(listIns.SelectedIndex==-1) {
				MsgBox.Show(this,"Please select an insurance first.");
				return;
			}
			ValueSelected=listIns.GetSelected<EFormInsPickChoice>().Value;
			IsDialogOK=true;
		}

		private void butCancel_Click(object sender,EventArgs e) {
			IsDialogOK=false;
		}
	}
}
