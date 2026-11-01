// V-BRIDGE1, Open Dental 25.1 item v25_1-setup-overjet-bridge: the Overjet program link opens the vendor's web app in the default browser.
// The office signs in on the vendor's own page with its own account; nothing passes through any other server.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;

namespace OpenDentBusiness.VendorDirect.ProgramLinks {
	///<summary>The Overjet program link: its row and the address its button opens.
	///<para>Public sources give the default address and the setup fields only. The names of the patient parameters are not public
	///(partner-gated), so the office enters them in the "Patient URL parameters" property from the vendor's integration guide, using the
	///BridgePatientTokens tokens. While that property is blank the button opens the address as it is, which shows the vendor's sign-in page.</para>
	///</summary>
	public static class OverjetLink {
		public const string ProgDesc="Overjet";
		public const string ButtonText="Overjet";
		///<summary>The default "Path of file to open", from the public Overjet setup page.</summary>
		public const string DefaultPath="https://clinic.overjet.ai/app/bridge_redirect";

		///<summary>programproperty.PropertyDesc values.</summary>
		public static class PropertyDescs {
			///<summary>Our addition: the query string added to the address when a patient is selected, for example name=[FName] (the parameter names
			///come from the vendor). Blank by default, so no patient data is sent until the office sets it up.</summary>
			public const string PatientUrlParameters="Patient URL parameters";
		}

		///<summary>The date format [Birthdate] uses in the URL (ISO 8601). Our choice; the vendor's format is not public.</summary>
		public const string BirthdateFormat="yyyy-MM-dd";

		public const string Note="Opens Overjet in the web browser. With no patient selected, or while the Patient URL parameters property is blank, "
			+"the address opens as it is (the Overjet sign-in page). To open the selected patient, enter the query parameters from Overjet's "
			+"integration guide in Patient URL parameters, using the tokens [PatientID], [PatNum], [ChartNumber], [LName], [FName], [Birthdate] "
			+"(yyyy-MM-dd) and [Gender]. [PatientID] is the PatNum or the ChartNumber, as the PatientNum/ChartNum property says.";

		public static BridgeLinkDefinition GetDefinition() {
			Meth.NoCheckMiddleTierRole();
			BridgeLinkDefinition definition=new BridgeLinkDefinition();
			definition.ProgName=ProgramName.Overjet;
			definition.ProgDesc=ProgDesc;
			definition.Path=DefaultPath;
			definition.Note=Note;
			definition.ButtonText=ButtonText;
			definition.ListToolBars=new List<EnumToolBar> { EnumToolBar.MainToolbar };
			definition.ListProperties=new List<KeyValuePair<string,string>> {
				new KeyValuePair<string,string>(ProgramProperties.PropertyDescs.PatOrChartNum,"0"),
				new KeyValuePair<string,string>(PropertyDescs.PatientUrlParameters,""),
			};
			return definition;
		}

		///<summary>Inserts the Overjet row (Enabled=0), its Main toolbar button and its properties if missing. Returns true if anything was inserted.
		///</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			return BridgeLinkRows.EnsureProgram(GetDefinition());
		}

		///<summary>The address for the button, from the program row (with this computer's path override) and its properties.</summary>
		public static BridgeLaunch GetLaunch(Program program,Patient patient) {
			Meth.NoCheckMiddleTierRole();
			string path=Programs.GetProgramPath(program);
			string parameters=ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.PatientUrlParameters,0);
			string patOrChartNum=ProgramProperties.GetValOrDefault(program.ProgramNum,ProgramProperties.PropertyDescs.PatOrChartNum,0);
			return GetLaunch(path,parameters,patOrChartNum,patient);
		}

		///<summary>The address for the button. No patient, or blank parameters: the path as it is. Otherwise the path plus the parameters with the
		///patient's values, percent-encoded. The path, and the finished address, must be https:// and not on a blocked host.</summary>
		public static BridgeLaunch GetLaunch(string path,string patientUrlParameters,string patOrChartNum,Patient patient) {
			string url=(path??"").Trim();
			BridgeLaunchProblem problem=BridgeLaunch.CheckWebAddress(url);
			if(problem!=BridgeLaunchProblem.None) {
				return BridgeLaunch.Refused(problem,url);
			}
			string query=(patientUrlParameters??"").Trim().TrimStart('?','&');
			if(patient==null || query=="") {
				return new BridgeLaunch { FileName=url };
			}
			query=BridgePatientTokens.Replace(query,patient,BridgePatientTokens.IsChartNumberSelected(patOrChartNum),BirthdateFormat,
				BridgeTokenEncoding.Url);
			string separator="?";
			if(url.Contains("?")) {
				separator=(url.EndsWith("?") || url.EndsWith("&")) ? "" : "&";
			}
			string urlPatient=url+separator+query;
			problem=BridgeLaunch.CheckWebAddress(urlPatient);
			if(problem!=BridgeLaunchProblem.None) {
				return BridgeLaunch.Refused(problem,url);
			}
			return new BridgeLaunch { FileName=urlPatient,HasPatientData=BridgePatientTokens.HasAnyToken(patientUrlParameters) };
		}
	}
}
