// P262-LINKS (Open Dental 26.2, item 44 "New bridge for Alliedstar Connect"): the Allied Star Connect program link. It writes the selected patient to
// a .json info file, then starts Alliedstar's Connect program on this computer. Built from the public setup page (bridgeasconnect.html) only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace OpenPmsDentalBusiness.VendorDirect.ProgramLinks {
	///<summary>A program link that hands the patient over in a file before it starts the program: the file to write (or, without a patient, the stale
	///file to remove) besides what BridgeLaunch says.</summary>
	public class BridgeFileLaunch:BridgeLaunch {
		///<summary>The full path of the hand-off file. Blank when the launch is refused before the path is known.</summary>
		public string HandOffFilePath="";
		///<summary>The text to write to HandOffFilePath before the program starts. Null when there is no patient: then a hand-off file left from an
		///earlier launch is removed instead, so the program can never open the previous patient.</summary>
		public string HandOffText;

		public static BridgeFileLaunch RefusedFile(BridgeLaunchProblem problem,string fileName,string handOffFilePath="") {
			return new BridgeFileLaunch { Problem=problem,FileName=fileName??"",HandOffFilePath=handOffFilePath??"" };
		}
	}

	///<summary>The Allied Star Connect program link (ProgName AlliedstarConnect, INFERRED: Open Dental's name is not public). The row is inserted at
	///runtime with Enabled=0 the first time Setup > Program Links opens, with the setup the public page lists: the program path
	///C:\Program Files\Alliedstar\Connect\Connect.exe, the properties "Enter 0 to use PatientNum, or 1 to use ChartNum" (0) and "Info file path"
	///(C:\ProgramData\Alliedstar\Connect\info.json), and a Chart module button. Its button shows only once the office enables the link.
	///<para>Public: the patient goes to Connect through the .json file at the Info file path; the folders of that path must already exist (the link
	///does not create them); of several email addresses, the first one of the comma-separated list is sent. Not public, so INFERRED and to be
	///confirmed with Alliedstar: the names of the fields in the file and the formats of their values (BuildInfoJson), that Connect is started with no
	///arguments and reads the file itself, and what happens without a patient (the stale file is removed and Connect starts without one).</para>
	///</summary>
	public static class AlliedstarConnectLink {
		///<summary>The row's description, as Open Dental's program bridge list names the link.</summary>
		public const string ProgDesc="Allied Star Connect";
		///<summary>The default "Path of file to open", from the public setup page.</summary>
		public const string DefaultPath=@"C:\Program Files\Alliedstar\Connect\Connect.exe";
		///<summary>The default Info file path, from the public setup page.</summary>
		public const string DefaultInfoFilePath=@"C:\ProgramData\Alliedstar\Connect\info.json";
		///<summary>The default text on the button. The public page calls the text optional and gives none (INFERRED).</summary>
		public const string DefaultButtonText="Allied Star Connect";
		///<summary>The toolbar the button is added to when the row is first inserted (the public page's default).</summary>
		public const EnumToolBar DefaultToolBar=EnumToolBar.ChartModule;

		///<summary>programproperty.PropertyDesc values.</summary>
		public static class PropertyDescs {
			///<summary>0 (default) sends the PatNum as the patient's id, 1 the ChartNumber.</summary>
			public const string PatOrChartNum=ProgramProperties.PropertyDescs.PatOrChartNum;
			///<summary>The full path of the .json file the patient is written to.</summary>
			public const string InfoFilePath="Info file path";
		}

		///<summary>The names of the fields in the info file (INFERRED: Alliedstar has not published them).</summary>
		public static class JsonFields {
			public const string PatientId="PatientId";
			public const string LastName="LastName";
			public const string FirstName="FirstName";
			public const string Birthdate="Birthdate";
			public const string Gender="Gender";
			public const string Email="Email";
			///<summary>Every field, in the order they are written.</summary>
			public static readonly string[] ArrayAll={ PatientId,LastName,FirstName,Birthdate,Gender,Email };
		}

		///<summary>The Notes text in Program Links: what the button does and what the office has to set up. Describes, never promotes.</summary>
		public const string Note="Writes the selected patient (id, name, birthdate, gender and the first email address) to the .json file in the Info file "
			+"path, then starts Connect. The folders of the Info file path must already exist; this program does not create them. Without a patient, "
			+"an old info file is removed and Connect starts without a patient. The layout of the info file is this program's own, because Alliedstar "
			+"has not published one; if Connect does not open the patient, ask Alliedstar which file layout it reads.";

		///<summary>The row this program inserts: the public page's defaults.</summary>
		public static BridgeLinkDefinition GetDefinition() {
			Meth.NoCheckMiddleTierRole();
			BridgeLinkDefinition definition=new BridgeLinkDefinition();
			definition.ProgName=ProgramName.AlliedstarConnect;
			definition.ProgDesc=ProgDesc;
			definition.Path=DefaultPath;
			definition.Note=Note;
			definition.ButtonText=DefaultButtonText;
			definition.ListToolBars=new List<EnumToolBar> { DefaultToolBar };
			definition.ListProperties=new List<KeyValuePair<string,string>> {
				new KeyValuePair<string,string>(PropertyDescs.PatOrChartNum,"0"),
				new KeyValuePair<string,string>(PropertyDescs.InfoFilePath,DefaultInfoFilePath),
			};
			return definition;
		}

		///<summary>Inserts the row (Enabled=0), its Chart module button and its two properties if they are missing (BridgeLinkRows). A row that exists
		///keeps its settings. Returns true if anything was inserted.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			return BridgeLinkRows.EnsureProgram(GetDefinition());
		}

		///<summary>The Info file path to use: the property's value, or the default when it is blank.</summary>
		public static string GetInfoFilePath(string propertyValue) {
			Meth.NoCheckMiddleTierRole();
			string path=(propertyValue??"").Trim();
			return path=="" ? DefaultInfoFilePath : path;
		}

		///<summary>The Info file path of the program row (its ClinicNum 0 property, or the default).</summary>
		public static string GetInfoFilePath(Program program) {
			Meth.NoCheckMiddleTierRole();
			if(program==null) {
				return DefaultInfoFilePath;
			}
			return GetInfoFilePath(ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.InfoFilePath,0));
		}

		///<summary>True when path is the full path of a file: a drive or network path, with a folder and a file name, and no character a path can't
		///hold.</summary>
		public static bool IsFullFilePath(string path) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(path)) {
				return false;
			}
			string text=path.Trim();
			if(text.IndexOfAny(Path.GetInvalidPathChars())>=0 || text.IndexOf('?')>=0 || text.IndexOf('*')>=0) {
				return false;
			}
			bool isDrive=text.Length>=3 && char.IsLetter(text[0]) && text[1]==':' && (text[2]=='\\' || text[2]=='/');
			bool isNetwork=text.StartsWith(@"\\") && text.Length>2;
			if(!isDrive && !isNetwork) {
				return false;
			}
			if(text.IndexOf(':',2)>=0) {//A second colon (an alternate data stream, or a mistyped path).
				return false;
			}
			try {
				string fileName=Path.GetFileName(text);
				string folder=Path.GetDirectoryName(text);
				return !string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(folder);
			}
			catch(ArgumentException) {
				return false;
			}
			catch(PathTooLongException) {
				return false;
			}
		}

		///<summary>The first address of a comma-separated list of email addresses (the public page: the bridge uses the first one), trimmed. "" when
		///there is none.</summary>
		public static string GetFirstEmail(string email) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(email)) {
				return "";
			}
			return email.Split(',').Select(x => x.Trim()).FirstOrDefault(x => x!="")??"";
		}

		///<summary>Male, Female, Other or Unknown (INFERRED).</summary>
		public static string GetGenderText(PatientGender patientGender) {
			Meth.NoCheckMiddleTierRole();
			switch(patientGender) {
				case PatientGender.Male:
					return "Male";
				case PatientGender.Female:
					return "Female";
				case PatientGender.Other:
					return "Other";
				default:
					return "Unknown";
			}
		}

		///<summary>The info file for the patient: one JSON object with the fields of JsonFields, in that order, every value a string. PatientId is the
		///PatNum, or the ChartNumber when useChartNumber; Birthdate is yyyy-MM-dd (blank without a birthdate); Email is the first address of the
		///patient's list. Names are written as they are, accented letters included. No other patient data (no address, phone or SSN) is written.
		///The field names and formats are INFERRED.</summary>
		public static string BuildInfoJson(Patient patient,bool useChartNumber) {
			Meth.NoCheckMiddleTierRole();
			if(patient==null) {
				throw new ArgumentNullException(nameof(patient));
			}
			string patientId=useChartNumber ? (patient.ChartNumber??"") : patient.PatNum.ToString(CultureInfo.InvariantCulture);
			string birthdate=patient.Birthdate.Year<1880 ? "" : patient.Birthdate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
			using(StringWriter stringWriter=new StringWriter(CultureInfo.InvariantCulture)) {
				using(JsonTextWriter jsonTextWriter=new JsonTextWriter(stringWriter)) {
					jsonTextWriter.Formatting=Formatting.Indented;
					jsonTextWriter.WriteStartObject();
					WriteField(jsonTextWriter,JsonFields.PatientId,patientId);
					WriteField(jsonTextWriter,JsonFields.LastName,patient.LName);
					WriteField(jsonTextWriter,JsonFields.FirstName,patient.FName);
					WriteField(jsonTextWriter,JsonFields.Birthdate,birthdate);
					WriteField(jsonTextWriter,JsonFields.Gender,GetGenderText(patient.Gender));
					WriteField(jsonTextWriter,JsonFields.Email,GetFirstEmail(patient.Email));
					jsonTextWriter.WriteEndObject();
				}
				return stringWriter.ToString();
			}
		}

		private static void WriteField(JsonTextWriter jsonTextWriter,string name,string value) {
			jsonTextWriter.WritePropertyName(name);
			jsonTextWriter.WriteValue((value??"").Trim());
		}

		///<summary>What the button does, from the program row (with this computer's path override) and its properties.</summary>
		public static BridgeFileLaunch GetLaunch(Program program,Patient patient) {
			Meth.NoCheckMiddleTierRole();
			string path=Programs.GetProgramPath(program);
			string infoFilePath=ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.InfoFilePath,0);
			string patOrChartNum=ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.PatOrChartNum,0);
			return GetLaunch(path,infoFilePath,patOrChartNum,program.CommandLine,patient,File.Exists,Directory.Exists);
		}

		///<summary>What the button does. Refused, in this order, when the program path is blank, the program file is missing, the Info file path
		///(blank means the default) is not the full path of a file, or, with a patient, its folder does not exist (the link never creates it). With a
		///patient, HandOffText is the info file; without one it is null and an old info file is to be removed. Connect is started with the
		///program's Command line as typed (blank by default; no patient data is put on the command line).</summary>
		public static BridgeFileLaunch GetLaunch(string path,string infoFilePath,string patOrChartNum,string commandLine,Patient patient,
			Func<string,bool> funcFileExists,Func<string,bool> funcDirectoryExists)
		{
			Meth.NoCheckMiddleTierRole();
			string fileName=(path??"").Trim();
			if(fileName=="") {
				return BridgeFileLaunch.RefusedFile(BridgeLaunchProblem.ProgramPathBlank,"");
			}
			if(funcFileExists==null || !funcFileExists(fileName)) {
				return BridgeFileLaunch.RefusedFile(BridgeLaunchProblem.ProgramNotFound,fileName);
			}
			string handOffFilePath=GetInfoFilePath(infoFilePath);
			if(!IsFullFilePath(handOffFilePath)) {
				return BridgeFileLaunch.RefusedFile(BridgeLaunchProblem.InfoFilePathInvalid,fileName,handOffFilePath);
			}
			BridgeFileLaunch bridgeFileLaunch=new BridgeFileLaunch();
			bridgeFileLaunch.FileName=fileName;
			bridgeFileLaunch.Arguments=(commandLine??"").Trim();
			bridgeFileLaunch.HandOffFilePath=handOffFilePath;
			if(patient==null) {
				return bridgeFileLaunch;
			}
			string folder=Path.GetDirectoryName(handOffFilePath);
			if(funcDirectoryExists==null || !funcDirectoryExists(folder)) {
				return BridgeFileLaunch.RefusedFile(BridgeLaunchProblem.InfoFolderMissing,fileName,handOffFilePath);
			}
			bridgeFileLaunch.HandOffText=BuildInfoJson(patient,BridgePatientTokens.IsChartNumberSelected(patOrChartNum));
			bridgeFileLaunch.HasPatientData=true;
			return bridgeFileLaunch;
		}
	}
}
