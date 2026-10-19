// Vendor-direct integrations: the PDMP (LogiCoy) and Bamboo program link buttons, moved out of ProgramL unchanged.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text;
using CodeBase;
using OpenDental;
using OpenDentBusiness;
using WpfControls.UI;

namespace WpfControls {
	///<summary>Owned by the PDMP package (docs/specs/vendor-direct/README.md, 3.2). The foundation package moved this branch here from
	///ProgramL.Execute without changing what it does, so the PDMP package can change it without editing ProgramL.</summary>
	public static class PdmpLaunch {
		///<summary>Runs the PDMP or Bamboo program link for the patient.</summary>
		public static void Execute(Program program,Patient patient) {
			PDMP pDMP=null;
			OpenDental.UI.ProgressWin progressWin=new OpenDental.UI.ProgressWin();
			Provider provider = Providers.GetProv(Security.CurUser.ProvNum);
			progressWin.ActionMain=() => pDMP=PDMP.SendData(program,patient,provider);
			progressWin.StartingMessage=Lans.g("PDMP","Fetching data...");
			try {
				progressWin.ShowDialog();
				bool hasUrl=!string.IsNullOrWhiteSpace(pDMP.Url);
				if(program.ProgName==ProgramName.PDMP.ToString()) {
					if(hasUrl) {//Logicoy errors throw exceptions.
						RxPats.CreatePdmpAccessLog(patient,Security.CurUser,program);
						FormLauncher formLauncher=new FormLauncher(EnumFormName.FormWebBrowser);
						formLauncher.SetField("UrlBrowseTo",pDMP.Url);
						formLauncher.Show();
					}
					else {
						throw new ApplicationException(Lans.g("PDMP","Unable to get report URL."));
					}
				}
				else {
					bool showCancel=true;
					StringBuilder stringBuilder=new StringBuilder();
					if(!string.IsNullOrWhiteSpace(pDMP.Message)) {
						stringBuilder.AppendLine(pDMP.Message);
						if(!hasUrl) {
							stringBuilder.Append(Lans.g("PDMP","No report URL retrieved from Bamboo."));
							showCancel=false;
						}
					}
					bool isOK=false;
					if(showCancel){
						isOK=MsgBox.Show(MsgBoxButtons.OKCancel,stringBuilder.ToString(),Lans.g("PDMP","Bamboo"));
					}
					else{
						MsgBox.Show(stringBuilder.ToString());
						isOK=true;
					}
					if(isOK && hasUrl) {
						RxPats.CreatePdmpAccessLog(patient,Security.CurUser,program);
						FormLauncher formLauncher=new FormLauncher(EnumFormName.FormWebBrowser);
						formLauncher.SetField("UrlBrowseTo",pDMP.Url);
						formLauncher.SetField("IsUrlSingleUse",true);
						formLauncher.Show();
					}
				}
			}
			catch(ApplicationException appEx) {
				MsgBox.Show(Lans.g("PDMP","An error occurred while loading ")+program.ProgName+"\n"+appEx.Message);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lans.g("PDMP","An error occurred while loading ")+program.ProgName+". \n"+ex.Message,ex);
			}
		}
	}
}
