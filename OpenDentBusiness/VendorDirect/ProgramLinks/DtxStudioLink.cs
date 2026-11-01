// V-BRIDGE1, Open Dental 25.2 item v25_2-bridges-dtx-studio: the DTX Studio and DTX Studio Acquire program links start DTXsync.exe on this
// computer with the selected patient.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenDentBusiness.VendorDirect.ProgramLinks {
	///<summary>The DTX Studio and DTX Studio Acquire program links: their rows and the command line their buttons start.
	///<para>Public sources give the setup fields and defaults (path, Birthdate Format, toolbars) only. DTXsync.exe's command-line syntax is not
	///public (partner-gated), so the office enters it in the "Patient arguments" property from the vendor's integration guide, using the
	///BridgePatientTokens tokens. Until then the button explains what is missing and starts nothing. The program's "Command line" field holds the
	///additional properties, appended after the patient arguments.</para></summary>
	public static class DtxStudioLink {
		public const string ProgDescStudio="DTX Studio";
		public const string ProgDescAcquire="DTX Studio Acquire";
		public const string ButtonTextStudio="DTX Studio";
		///<summary>Open Dental's Acquire page also says "DTX Studio"; we default to a different text so the two buttons can be told apart.</summary>
		public const string ButtonTextAcquire="DTX Studio Acquire";
		public const string DefaultPath=@"C:\Program Files\DTX Studio Clinic\DTXsync.exe";
		public const string DefaultBirthdateFormat="MM/dd/yyyy";

		///<summary>programproperty.PropertyDesc values.</summary>
		public static class PropertyDescs {
			///<summary>From the public setup pages. The format of [Birthdate].</summary>
			public const string BirthdateFormat="Birthdate Format";
			///<summary>Our addition: the arguments DTXsync.exe gets for the selected patient, with tokens. Blank by default.</summary>
			public const string PatientArguments="Patient arguments";
		}

		private const string NOTE_TOKENS="using the tokens [PatientID], [PatNum], [ChartNumber], [LName], [FName], [Birthdate] (in the Birthdate Format) "
			+"and [Gender] (M, F, O or U). Put quotes around a token whose value can contain spaces, for example \"[LName]\". [PatientID] is the "
			+"PatNum or the ChartNumber, as the PatientNum/ChartNum property says. The Command line field is added after the patient arguments.";

		public const string NoteStudio="Opens the selected patient in DTX Studio, which adds the patient if needed. Before the first use, enter the "
			+"DTXsync.exe arguments from the DTX Studio integration guide in the Patient arguments property, "+NOTE_TOKENS;

		public const string NoteAcquire="Opens the selected patient in DTX Studio with acquisition ready. A patient that DTX Studio does not have yet is "
			+"sent anyway, and DTX Studio decides what to do. Before the first use, enter the DTXsync.exe arguments for acquisition from the DTX Studio "
			+"integration guide in the Patient arguments property, "+NOTE_TOKENS;

		public static BridgeLinkDefinition GetDefinition(ProgramName programName) {
			Meth.NoCheckMiddleTierRole();
			bool isAcquire;
			if(programName==ProgramName.DTXStudio) {
				isAcquire=false;
			}
			else if(programName==ProgramName.DTXStudioAcquire) {
				isAcquire=true;
			}
			else {
				throw new ArgumentException("Not a DTX Studio program link: "+programName,nameof(programName));
			}
			BridgeLinkDefinition definition=new BridgeLinkDefinition();
			definition.ProgName=programName;
			definition.ProgDesc=isAcquire ? ProgDescAcquire : ProgDescStudio;
			definition.Path=DefaultPath;
			definition.Note=isAcquire ? NoteAcquire : NoteStudio;
			definition.ButtonText=isAcquire ? ButtonTextAcquire : ButtonTextStudio;
			definition.ListToolBars=new List<EnumToolBar> { EnumToolBar.ImagingModule,EnumToolBar.MainToolbar };
			definition.ListProperties=new List<KeyValuePair<string,string>> {
				new KeyValuePair<string,string>(PropertyDescs.BirthdateFormat,DefaultBirthdateFormat),
				new KeyValuePair<string,string>(ProgramProperties.PropertyDescs.PatOrChartNum,"0"),
				new KeyValuePair<string,string>(PropertyDescs.PatientArguments,""),
			};
			return definition;
		}

		///<summary>True for the two DTX Studio program rows.</summary>
		public static bool IsDtxStudio(Program program) {
			Meth.NoCheckMiddleTierRole();
			return program!=null
				&& (program.ProgName==ProgramName.DTXStudio.ToString() || program.ProgName==ProgramName.DTXStudioAcquire.ToString());
		}

		///<summary>Inserts the row (Enabled=0), its Imaging and Main toolbar buttons and its properties if missing. Returns true if anything was
		///inserted.</summary>
		public static bool EnsureProgram(ProgramName programName) {
			Meth.NoCheckMiddleTierRole();
			return BridgeLinkRows.EnsureProgram(GetDefinition(programName));
		}

		///<summary>The program and arguments for the button, from the program row (with this computer's path override) and its properties.</summary>
		public static BridgeLaunch GetLaunch(Program program,Patient patient) {
			Meth.NoCheckMiddleTierRole();
			string path=Programs.GetProgramPath(program);
			string patientArguments=ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.PatientArguments,0);
			string birthdateFormat=ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.BirthdateFormat,0);
			string patOrChartNum=ProgramProperties.GetValOrDefault(program.ProgramNum,ProgramProperties.PropertyDescs.PatOrChartNum,0);
			return GetLaunch(path,patientArguments,program.CommandLine,birthdateFormat,patOrChartNum,patient,File.Exists);
		}

		///<summary>The program and arguments for the button. Refused, in this order, when there is no patient, the path is blank, the program file is
		///missing (funcFileExists), the patient arguments are blank, or the Birthdate Format is unusable. A blank Birthdate Format means
		///MM/dd/yyyy.</summary>
		public static BridgeLaunch GetLaunch(string path,string patientArguments,string commandLine,string birthdateFormat,string patOrChartNum,
			Patient patient,Func<string,bool> funcFileExists)
		{
			string fileName=(path??"").Trim();
			if(patient==null) {
				return BridgeLaunch.Refused(BridgeLaunchProblem.NoPatient,fileName);
			}
			if(fileName=="") {
				return BridgeLaunch.Refused(BridgeLaunchProblem.PathBlank);
			}
			if(funcFileExists==null || !funcFileExists(fileName)) {
				return BridgeLaunch.Refused(BridgeLaunchProblem.ProgramNotFound,fileName);
			}
			string template=(patientArguments??"").Trim();
			if(template=="") {
				return BridgeLaunch.Refused(BridgeLaunchProblem.PatientArgumentsNotSet,fileName);
			}
			string format=string.IsNullOrWhiteSpace(birthdateFormat) ? DefaultBirthdateFormat : birthdateFormat.Trim();
			if(template.Contains(BridgePatientTokens.Birthdate) && !BridgePatientTokens.IsValidDateFormat(format)) {
				return BridgeLaunch.Refused(BridgeLaunchProblem.BirthdateFormatInvalid,fileName);
			}
			string arguments=BridgePatientTokens.Replace(template,patient,BridgePatientTokens.IsChartNumberSelected(patOrChartNum),format,
				BridgeTokenEncoding.CommandLine);
			string additional=(commandLine??"").Trim();
			if(additional!="") {
				arguments+=" "+additional;
			}
			return new BridgeLaunch { FileName=fileName,Arguments=arguments,HasPatientData=BridgePatientTokens.HasAnyToken(template) };
		}
	}
}
