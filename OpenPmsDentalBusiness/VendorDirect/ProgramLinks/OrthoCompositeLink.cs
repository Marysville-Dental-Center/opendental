// P262-LINKS (Open Dental 26.2, item 45 "New imaging bridge to OrthoComposite"): the OrthoComposite program link, a local launch of the
// OrthoComposite imaging program (from the Microsoft Store) with the selected patient's name and birthdate. Built from the public setup page
// (bridgeorthocomposite.html) and, since next/vendorpub-1, OrthoComposite's public Microsoft Store listing, which gives its command arguments.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace OpenPmsDentalBusiness.VendorDirect.ProgramLinks {
	///<summary>The OrthoComposite program link (ProgName OrthoComposite, INFERRED: Open Dental's name is not public). The row is inserted at runtime
	///with Enabled=0 the first time Setup > Program Links opens, with the setup the public page lists: no default path (OrthoComposite comes from
	///the Microsoft Store, so the office enters where it is), a Chart module button "OrthoComposite", and the "Patient arguments" property.
	///<para>Public: OrthoComposite finds the patient by first name, last name and birthdate; the birthdate is MM/DD/YYYY; names may hold accented
	///letters (Open Dental's page). OrthoComposite takes them as the command arguments --lastname, --firstname and --dob and opens its patient list
	///with the search filled in (its Microsoft Store listing). So the "Patient arguments" property holds DefaultPatientArguments, with the tokens
	///[LName], [FName] and [Birthdate]; only those three are filled in, so no other patient data can reach the program. The office may change the
	///value; a value it cleared makes the button say what is missing and start nothing for a patient. Rows inserted before the default shipped got
	///it from fork migration step 2026110801 (OrthoCompositePatientArgumentsStep) where they were still blank.</para></summary>
	public static class OrthoCompositeLink {
		///<summary>The row's description and the default text on the button (the public page's "Text on button" default).</summary>
		public const string ProgDesc="OrthoComposite";
		public const string DefaultButtonText="OrthoComposite";
		///<summary>The toolbar the button is added to when the row is first inserted (the public page's default).</summary>
		public const EnumToolBar DefaultToolBar=EnumToolBar.ChartModule;
		///<summary>The birthdate format the public page gives (MM/DD/YYYY), written with the invariant culture so the separator is always "/".</summary>
		public const string BirthdateFormat="MM/dd/yyyy";

		///<summary>programproperty.PropertyDesc values.</summary>
		public static class PropertyDescs {
			///<summary>Our addition: the arguments OrthoComposite gets for the selected patient, with tokens. DefaultPatientArguments by default.</summary>
			public const string PatientArguments="Patient arguments";
		}

		///<summary>The Patient arguments value a new row gets: OrthoComposite's public command arguments (its Microsoft Store listing), each value
		///quoted because names may hold spaces. The birthdate is MM/dd/yyyy (Open Dental's page).</summary>
		public const string DefaultPatientArguments="--lastname \"[LName]\" --firstname \"[FName]\" --dob \"[Birthdate]\"";

		///<summary>The three tokens a Patient arguments template may use. Any other text, other bridges' tokens included, is sent as typed.</summary>
		public const string TokenFName="[FName]";
		public const string TokenLName="[LName]";
		public const string TokenBirthdate="[Birthdate]";
		public static readonly string[] ArrayTokens={ TokenFName,TokenLName,TokenBirthdate };

		///<summary>The Notes text in Program Links. Describes, never promotes. Fork migration step 2026110801 writes it over the text of p262/links-1
		///(OrthoCompositePatientArguments.NoteBefore) only.</summary>
		public const string Note="Starts OrthoComposite for the selected patient with its patient search filled in: the Patient arguments property gives "
			+"OrthoComposite the patient's last name, first name and birthdate with its own arguments --lastname, --firstname and --dob, using the "
			+"tokens [LName], [FName] and [Birthdate] (MM/dd/yyyy). No other patient data is sent. Enter where OrthoComposite is in Path of file to "
			+"open; a name without a folder, or a shell: target, is passed to Windows unchecked. The Command line field is added after the patient "
			+"arguments. Without a patient, OrthoComposite starts with the Command line only.";

		///<summary>The row this program inserts: the public page's defaults (no path, Chart module button "OrthoComposite") and the public arguments.
		///</summary>
		public static BridgeLinkDefinition GetDefinition() {
			Meth.NoCheckMiddleTierRole();
			BridgeLinkDefinition definition=new BridgeLinkDefinition();
			definition.ProgName=ProgramName.OrthoComposite;
			definition.ProgDesc=ProgDesc;
			definition.Path="";
			definition.Note=Note;
			definition.ButtonText=DefaultButtonText;
			definition.ListToolBars=new List<EnumToolBar> { DefaultToolBar };
			definition.ListProperties=new List<KeyValuePair<string,string>> {
				new KeyValuePair<string,string>(PropertyDescs.PatientArguments,DefaultPatientArguments),
			};
			return definition;
		}

		///<summary>Inserts the row (Enabled=0), its Chart module button and the Patient arguments property if they are missing (BridgeLinkRows). A
		///row that exists keeps its settings. Returns true if anything was inserted.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			return BridgeLinkRows.EnsureProgram(GetDefinition());
		}

		///<summary>True when the path names a file on a drive or the network ("C:\..." or "\\server\..."), which must then exist. A name without a
		///folder or a "shell:" target is passed to Windows unchecked; whether Windows finds it is not known here. Neither public source names
		///OrthoComposite's executable or any app execution alias, so no such name is suggested to the office.</summary>
		public static bool IsFileSystemPath(string path) {
			Meth.NoCheckMiddleTierRole();
			string text=(path??"").Trim();
			if(text.StartsWith(@"\\")) {
				return true;
			}
			return text.Length>=2 && char.IsLetter(text[0]) && text[1]==':';
		}

		///<summary>Replaces [FName], [LName] and [Birthdate] (MM/dd/yyyy, blank without a birthdate) in one pass, for a command line: double quotes and
		///control characters are removed from the values, accented letters are kept, and a value does not end in a backslash (in a quoted token,
		///"value\" would escape the closing quote on a Windows command line). Nothing else in the template is replaced.</summary>
		public static string FillArguments(string template,Patient patient) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(template) || patient==null) {
				return template??"";
			}
			return _regexToken.Replace(template,match => EncodeValue(GetValue(match.Value,patient)));
		}

		private static readonly Regex _regexToken=new Regex(@"\[(FName|LName|Birthdate)\]",RegexOptions.CultureInvariant);

		///<summary>The value as BridgePatientTokens encodes it for a command line, without backslashes or white space at its end.</summary>
		private static string EncodeValue(string value) {
			string text=BridgePatientTokens.Encode(value,BridgeTokenEncoding.CommandLine);
			int length=text.Length;
			while(length>0 && (text[length-1]=='\\' || char.IsWhiteSpace(text[length-1]))) {
				length--;
			}
			return text.Substring(0,length);
		}

		private static string GetValue(string token,Patient patient) {
			switch(token) {
				case TokenFName:
					return patient.FName;
				case TokenLName:
					return patient.LName;
				case TokenBirthdate:
					return BridgePatientTokens.FormatBirthdate(patient.Birthdate,BirthdateFormat);
				default:
					return token;
			}
		}

		///<summary>True if the template uses at least one of the three tokens.</summary>
		public static bool HasAnyToken(string template) {
			Meth.NoCheckMiddleTierRole();
			return !string.IsNullOrEmpty(template) && _regexToken.IsMatch(template);
		}

		///<summary>The program and arguments for the button, from the program row (with this computer's path override) and its properties.</summary>
		public static BridgeLaunch GetLaunch(Program program,Patient patient) {
			Meth.NoCheckMiddleTierRole();
			string path=Programs.GetProgramPath(program);
			string patientArguments=ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.PatientArguments,0);
			return GetLaunch(path,patientArguments,program.CommandLine,patient,File.Exists);
		}

		///<summary>The program and arguments for the button. Refused, in this order, when the path is blank, a drive or network path names no file
		///(funcFileExists), or there is a patient and the Patient arguments are blank. Without a patient the program starts with the Command line
		///only. The Command line is added after the patient arguments.</summary>
		public static BridgeLaunch GetLaunch(string path,string patientArguments,string commandLine,Patient patient,Func<string,bool> funcFileExists) {
			Meth.NoCheckMiddleTierRole();
			string fileName=(path??"").Trim();
			if(fileName=="") {
				return BridgeLaunch.Refused(BridgeLaunchProblem.ProgramPathBlank);
			}
			if(IsFileSystemPath(fileName) && (funcFileExists==null || !funcFileExists(fileName))) {
				return BridgeLaunch.Refused(BridgeLaunchProblem.ProgramNotFound,fileName);
			}
			string additional=(commandLine??"").Trim();
			if(patient==null) {
				return new BridgeLaunch { FileName=fileName,Arguments=additional };
			}
			string template=(patientArguments??"").Trim();
			if(template=="") {
				return BridgeLaunch.Refused(BridgeLaunchProblem.PatientArgumentsNotSet,fileName);
			}
			string arguments=FillArguments(template,patient);
			if(additional!="") {
				arguments+=" "+additional;
			}
			return new BridgeLaunch { FileName=fileName,Arguments=arguments,HasPatientData=HasAnyToken(template) };
		}
	}
}
