using System;
using System.Linq;
using System.Net;
using CodeBase;
using Newtonsoft.Json;
using OpenDentBusiness;

namespace OpenDental.Bridges {
	///<summary></summary>
	public class Oryx {

		///<summary>Makes an API call to get an Oryx URL to launch that is specific to the current user and patient.</summary>
		public static void SendData(Program progOryx,Patient pat) {
			string clientUrl="";
			try {
				if(!progOryx.Enabled) {
					MsgBox.Show("Oryx","Oryx must be enabled in Program Links.");
					return;
				}
				clientUrl=OpenDentBusiness.ProgramProperties.GetPropVal(progOryx.ProgramNum,ProgramProperties.ClientUrl);
				if(clientUrl=="") {
					MsgBox.Show("Oryx","Enter the Client URL in the Oryx program link.");
					return;
				}
				if(!clientUrl.ToLower().StartsWith("http")) {
					clientUrl="https://"+clientUrl;
				}
				UserOdPref userNamePref=UserOdPrefs.GetByUserFkeyAndFkeyType(Security.CurUser.UserNum,progOryx.ProgramNum,UserOdFkeyType.ProgramUserName)
					.FirstOrDefault();
				UserOdPref passwordPref=UserOdPrefs.GetByUserFkeyAndFkeyType(Security.CurUser.UserNum,progOryx.ProgramNum,UserOdFkeyType.ProgramPassword)
					.FirstOrDefault();
				if((userNamePref==null || userNamePref.ValueString=="") && (passwordPref==null || passwordPref.ValueString=="")) {
					//User hasn't entered credentials yet. Launch the office's Oryx page where the user can then log in.
					ODFileUtils.ProcessStart(clientUrl);
					return;
				}
				string apiUrl=clientUrl.TrimEnd('/')+"/api/auth/opendental/v1/login";
				string passwordPlain;
				if(!CDT.Class1.Decrypt(passwordPref.ValueString,out passwordPlain)) {
					MsgBox.Show("Oryx","Unable to decrypt password");
					return;
				}
				var content=new {
					username=userNamePref.ValueString,
					password=passwordPlain,
					patientId=(pat!=null ? pat.PatNum.ToString() : ""),
				};
				string contentJson=JsonConvert.SerializeObject(content);
				string responseStr;
				using(WebClient client=new WebClient()) {
					client.Headers[HttpRequestHeader.ContentType]="application/json";
					responseStr=client.UploadString(apiUrl,"POST",contentJson);
				}
				var response=new {
					success=false,
					redirectUrl="",
					errorMessage="",
				};
				response=JsonConvert.DeserializeAnonymousType(responseStr,response);
				if(!response.success) {
					MsgBox.Show(Lans.g("Orxy","Error message from Oryx:")+" "+response.errorMessage);
					return;
				}
				ODFileUtils.ProcessStart(response.redirectUrl);
			}
			catch(Exception ex) {
				string errorMessage="Unable to launch Oryx.";
				if(ex is NotSupportedException && ex.Message=="The given path's format is not supported.") {
					//Oryx has asked us to give a more helpful error message when this happens.
					errorMessage+=" This is likely because the Client URL is invalid.\r\nClient URL: "+clientUrl;
				}
				FriendlyException.Show(Lans.g("Oryx",errorMessage),ex);
			}
		}

		public static void menuItemUserSettingsClick(object sender,EventArgs e) {
			FormLauncher formLauncher=new FormLauncher(EnumFormName.FormOryxUserSettings);
			formLauncher.ShowDialog();
		}

		public class ProgramProperties {
			public static string ClientUrl="Client URL";
		}
	}
}







