// Vendor-direct integrations: the PDMP (LogiCoy) and Bamboo program link buttons.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text;
using CodeBase;
using OpenDental;
using OpenDentBusiness;
using WpfControls.UI;

namespace WpfControls {
	///<summary>Owned by the PDMP package (docs/specs/vendor-direct/README.md, 3.2). Runs the prescription monitoring check from the patient's chart.
	///Reports open in the WebView2 window. Bamboo asks for the single-use report link only after the user clicks OK, so Cancel sends nothing more to
	///the PMP. The rxpat access-log row is written only when a report actually opens.</summary>
	public static class PdmpLaunch {
		///<summary>Runs the PDMP or Bamboo program link for the patient.</summary>
		public static void Execute(Program program,Patient patient) {
			PDMP pDMP=null;
			Provider provider=Providers.GetProv(Security.CurUser.ProvNum);
			try {
				OpenDental.UI.ProgressWin progressWin=new OpenDental.UI.ProgressWin();
				progressWin.ActionMain=() => pDMP=PDMP.SendData(program,patient,provider);
				progressWin.StartingMessage=Lans.g("PDMP","Fetching data...");
				progressWin.ShowDialog();
				if(progressWin.IsCancelled || pDMP==null) {
					return;
				}
				if(program.ProgName==ProgramName.PDMP.ToString()) {
					if(string.IsNullOrWhiteSpace(pDMP.Url)) {//Logicoy errors throw exceptions.
						throw new ApplicationException(Lans.g("PDMP","No report was returned."));
					}
					OpenReport(program,patient,pDMP.Url,isUrlSingleUse:false);
					return;
				}
				//Bamboo: step 1 is done. Show the scores; ask for the report only if the user wants it.
				StringBuilder stringBuilder=new StringBuilder();
				if(!string.IsNullOrWhiteSpace(pDMP.Message)) {
					stringBuilder.AppendLine(pDMP.Message.TrimEnd());
				}
				if(!string.IsNullOrWhiteSpace(pDMP.Warning)) {
					stringBuilder.AppendLine();
					stringBuilder.AppendLine(pDMP.Warning);
				}
				if(!pDMP.HasBambooReport) {
					if(stringBuilder.Length==0) {
						stringBuilder.AppendLine(Lans.g("PDMP","No report was returned."));
					}
					MsgBox.Show(stringBuilder.ToString().TrimEnd());
					return;
				}
				if(!MsgBox.Show(MsgBoxButtons.OKCancel,stringBuilder.ToString().TrimEnd(),program.ProgDesc)) {
					return;//No ReportRequest is sent and no rxpat row is written.
				}
				string reportLink=null;
				OpenDental.UI.ProgressWin progressWinReport=new OpenDental.UI.ProgressWin();
				progressWinReport.ActionMain=() => reportLink=pDMP.GetBambooReportLink();
				progressWinReport.StartingMessage=Lans.g("PDMP","Opening report...");
				progressWinReport.ShowDialog();
				if(progressWinReport.IsCancelled || string.IsNullOrWhiteSpace(reportLink)) {
					return;
				}
				OpenReport(program,patient,reportLink,isUrlSingleUse:true);
			}
			catch(ApplicationException appEx) {
				MsgBox.Show(Lans.g("PDMP","An error occurred while loading ")+program.ProgName+"\n"+appEx.Message);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lans.g("PDMP","An error occurred while loading ")+program.ProgName+". \n"+ex.Message,ex);
			}
		}

		///<summary>Writes the rxpat access-log row and opens the report in the WebView2 window. A single-use link hides Refresh.</summary>
		private static void OpenReport(Program program,Patient patient,string url,bool isUrlSingleUse) {
			RxPats.CreatePdmpAccessLog(patient,Security.CurUser,program);
			FormLauncher formLauncher=new FormLauncher(EnumFormName.FormWebView);
			formLauncher.SetField("UrlBrowseTo",url);
			if(isUrlSingleUse) {
				formLauncher.SetField("IsUrlSingleUse",true);
			}
			formLauncher.Show();
		}
	}
}
