using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace OpenPmsDental {
	public partial class FrmBencoSetup:FrmODBase {
		private Program _program;
		private List<ToolButItem> _listToolButItems;

		public FrmBencoSetup() {
			InitializeComponent();
			_program=Programs.GetCur(ProgramName.BencoPracticeManagement);
			_listToolButItems=ToolButItems.GetForProgram(_program.ProgramNum); //Initially only set up for Main Toolbar
			Load+=FrmBencoSetup_Load;
			PreviewKeyDown+=FrmBencoSetup_PreviewKeyDown;
		}

		private void FrmBencoSetup_Load(object sender,EventArgs e) {
			checkEnable.Checked=_program.Enabled;
			textProgDesc.Text=_program.ProgDesc;
			textPath.Text=_program.Path;
			textButText.Text=_listToolButItems.FirstOrDefault()?.ButtonText??"Benco";
			FillToolBars();
		}

		private void FillToolBars() {
			listToolBars.Items.Clear();
			listToolBars.Items.AddEnums<EnumToolBar>();
			for(int i=0;i<listToolBars.Items.Count;i++) {
				if(_listToolButItems.Any(x => x.ToolBar==(EnumToolBar)listToolBars.Items.GetObjectAt(i))) {
					listToolBars.SetSelected(i);
				}
			}
		}

		//The Benco branding switch (UpdateBencoSettings) was removed (decision D7, ground rule 7): enabling this program link used to rename the
		//main window title and the SoftwareName preference to Benco's reseller branding, and disabling it set them back. The program link itself
		//(path, description, toolbar buttons) is unchanged. A database that still holds the Benco values shows the product name instead
		//(OpenDental.AboutBoxInfo.GetDisplayName); the rows are left as they are.

		private void FrmBencoSetup_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butSave.IsAltKey(Key.S,e)) {
				butSave_Click(this,new EventArgs());
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			//Program
			_program.Enabled=checkEnable.Checked.Value;
			_program.ProgDesc=textProgDesc.Text;
			_program.Path=textPath.Text;
			Programs.Update(_program);
			//Toolbar button
			ToolButItems.DeleteAllForProgram(_program.ProgramNum);
			List<EnumToolBar> listToolBarsAvails=listToolBars.GetListSelected<EnumToolBar>();
			for(int i=0;i<listToolBarsAvails.Count;++i) {
				ToolButItem newBut=new ToolButItem();
				newBut.ProgramNum=_program.ProgramNum;
				newBut.ToolBar=listToolBarsAvails[i];
				newBut.ButtonText=textButText.Text;
				ToolButItems.Insert(newBut);
			}
			MsgBox.Show(this,"You will need to restart Open Dental for these changes to take effect.");
			IsDialogOK=true;
		}

	}
}