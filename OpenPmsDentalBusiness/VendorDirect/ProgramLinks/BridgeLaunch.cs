// V-BRIDGE1: what a program link button starts (a URL or a program with arguments), the patient tokens the office's templates may use, and the
// reasons a launch is refused. The windows layer (WpfControlsOD/Bridges) shows the message or starts the process; nothing here starts anything.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CodeBase.VendorDirect;

namespace OpenPmsDentalBusiness.VendorDirect.ProgramLinks {
	///<summary>Why a program link button did not start anything.</summary>
	public enum BridgeLaunchProblem {
		///<summary>Ready to start.</summary>
		None,
		///<summary>The link needs a patient and none is selected.</summary>
		NoPatient,
		///<summary>"Path of file to open" is blank.</summary>
		PathBlank,
		///<summary>The address is not a full https:// address.</summary>
		PathNotHttps,
		///<summary>The address is on a host this program never contacts (CodeBase.VendorDirect.HqHosts).</summary>
		PathBlocked,
		///<summary>The program file in "Path of file to open" does not exist on this computer.</summary>
		ProgramNotFound,
		///<summary>The patient arguments template is blank. The vendor's launch syntax is not public; the office enters it from the vendor's
		///integration guide.</summary>
		PatientArgumentsNotSet,
		///<summary>The Birthdate Format property is not a usable date format.</summary>
		BirthdateFormatInvalid,
	}

	///<summary>What a program link button starts: FileName (a URL or a program path) with Arguments, or the Problem that stopped it.</summary>
	public class BridgeLaunch {
		public BridgeLaunchProblem Problem=BridgeLaunchProblem.None;
		///<summary>The URL to open in the default browser, or the program to start.</summary>
		public string FileName="";
		///<summary>Command-line arguments. Always "" for a URL.</summary>
		public string Arguments="";
		///<summary>True when the URL or the arguments carry patient data.</summary>
		public bool HasPatientData;

		public bool IsReady {
			get {
				return Problem==BridgeLaunchProblem.None;
			}
		}

		public static BridgeLaunch Refused(BridgeLaunchProblem problem,string fileName="") {
			return new BridgeLaunch { Problem=problem,FileName=fileName??"" };
		}

		///<summary>The message for a refused launch. Fixed text per problem, so it can be translated. ProgramNotFound is followed by the path.</summary>
		public static string Describe(BridgeLaunchProblem problem) {
			switch(problem) {
				case BridgeLaunchProblem.None:
					return "";
				case BridgeLaunchProblem.NoPatient:
					return "Please select a patient first.";
				case BridgeLaunchProblem.PathBlank:
					return "Enter the address in Setup, Program Links, Path of file to open.";
				case BridgeLaunchProblem.PathNotHttps:
					return "The address in Program Links must be a full address that starts with https://.";
				case BridgeLaunchProblem.PathBlocked:
					return "The address in Program Links belongs to a service this program does not contact.";
				case BridgeLaunchProblem.ProgramNotFound:
					return "The program was not found at this path:";
				case BridgeLaunchProblem.PatientArgumentsNotSet:
					return "The patient arguments for this program link are not set up. Enter them in Setup, Program Links, from the vendor's "
						+"integration guide.";
				case BridgeLaunchProblem.BirthdateFormatInvalid:
					return "The Birthdate Format in Program Links is not a valid date format, for example MM/dd/yyyy.";
				default:
					return problem.ToString();
			}
		}

		///<summary>Checks an address the way every web program link needs it: a full https:// address that is not on a blocked host. Plain http is
		///refused even for this computer. Returns None, PathBlank, PathNotHttps or PathBlocked.</summary>
		public static BridgeLaunchProblem CheckWebAddress(string url) {
			if(string.IsNullOrWhiteSpace(url)) {
				return BridgeLaunchProblem.PathBlank;
			}
			Uri uri;
			if(!Uri.TryCreate(url.Trim(),UriKind.Absolute,out uri) || uri.Scheme!=Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host)) {
				return BridgeLaunchProblem.PathNotHttps;
			}
			switch(VendorUrlPolicy.Check(uri)) {
				case VendorUrlProblem.None:
					return BridgeLaunchProblem.None;
				case VendorUrlProblem.BlockedHost:
					return BridgeLaunchProblem.PathBlocked;
				default:
					return BridgeLaunchProblem.PathNotHttps;
			}
		}
	}

	///<summary>How a token value is written into a template.</summary>
	public enum BridgeTokenEncoding {
		///<summary>Percent-encoded for a URL query string.</summary>
		Url,
		///<summary>For a command line: double quotes and control characters removed. The template supplies any quotes the vendor needs.</summary>
		CommandLine,
	}

	///<summary>The patient tokens a program link template may use. The vendors' parameter and argument names are not public, so the office enters the
	///template from the vendor's own integration guide; this program only fills in the values.</summary>
	public static class BridgePatientTokens {
		///<summary>PatNum, or ChartNumber when the "Enter 0 to use PatientNum, or 1 to use ChartNum" property is 1.</summary>
		public const string PatientID="[PatientID]";
		public const string PatNum="[PatNum]";
		public const string ChartNumber="[ChartNumber]";
		public const string LName="[LName]";
		public const string FName="[FName]";
		///<summary>Blank when the patient has no birthdate.</summary>
		public const string Birthdate="[Birthdate]";
		///<summary>M, F, O (other) or U (unknown).</summary>
		public const string Gender="[Gender]";
		///<summary>Every token, for help text and tests.</summary>
		public static readonly string[] ArrayAll={ PatientID,PatNum,ChartNumber,LName,FName,Birthdate,Gender };

		///<summary>True when the property value selects ChartNumber ("1"); anything else, including blank, selects PatNum.</summary>
		public static bool IsChartNumberSelected(string patOrChartNumValue) {
			return (patOrChartNumValue??"").Trim()=="1";
		}

		///<summary>M, F, O or U. Our choice: the vendors' codes are not public.</summary>
		public static string GetGenderCode(PatientGender patientGender) {
			switch(patientGender) {
				case PatientGender.Male:
					return "M";
				case PatientGender.Female:
					return "F";
				case PatientGender.Other:
					return "O";
				default:
					return "U";
			}
		}

		///<summary>The birthdate in the format (invariant culture), or "" when the patient has none. Throws FormatException for an unusable format.
		///</summary>
		public static string FormatBirthdate(DateTime birthdate,string format) {
			if(birthdate.Year<1880) {
				return "";
			}
			return birthdate.ToString(format,CultureInfo.InvariantCulture);
		}

		///<summary>True if format is a usable custom or standard date format string.</summary>
		public static bool IsValidDateFormat(string format) {
			if(string.IsNullOrWhiteSpace(format)) {
				return false;
			}
			try {
				string text=new DateTime(2001,2,3).ToString(format,CultureInfo.InvariantCulture);
				return text!="" && text!=format;//A format with no date field at all would send the same text for every patient.
			}
			catch(FormatException) {
				return false;
			}
		}

		///<summary>Replaces every token in template with the patient's value, encoded for where it goes, in one pass (a value that looks like a token
		///is never replaced again). Text that isn't a token is left as typed. Throws FormatException if the template uses [Birthdate] and
		///birthdateFormat is unusable; callers check IsValidDateFormat first.</summary>
		public static string Replace(string template,Patient patient,bool isChartNumber,string birthdateFormat,BridgeTokenEncoding encoding) {
			if(string.IsNullOrEmpty(template) || patient==null) {
				return template??"";
			}
			return _regexToken.Replace(template,match => Encode(GetValue(match.Value,patient,isChartNumber,birthdateFormat),encoding));
		}

		private static readonly Regex _regexToken=new Regex(@"\[(PatientID|PatNum|ChartNumber|LName|FName|Birthdate|Gender)\]",RegexOptions.CultureInvariant);

		private static string GetValue(string token,Patient patient,bool isChartNumber,string birthdateFormat) {
			switch(token) {
				case PatientID:
					return isChartNumber ? patient.ChartNumber : patient.PatNum.ToString(CultureInfo.InvariantCulture);
				case PatNum:
					return patient.PatNum.ToString(CultureInfo.InvariantCulture);
				case ChartNumber:
					return patient.ChartNumber;
				case LName:
					return patient.LName;
				case FName:
					return patient.FName;
				case Birthdate:
					return FormatBirthdate(patient.Birthdate,birthdateFormat);
				case Gender:
					return GetGenderCode(patient.Gender);
				default:
					return token;
			}
		}

		///<summary>True if the template uses at least one token.</summary>
		public static bool HasAnyToken(string template) {
			if(string.IsNullOrEmpty(template)) {
				return false;
			}
			for(int i=0;i<ArrayAll.Length;i++) {
				if(template.Contains(ArrayAll[i])) {
					return true;
				}
			}
			return false;
		}

		public static string Encode(string value,BridgeTokenEncoding encoding) {
			string text=value??"";
			if(encoding==BridgeTokenEncoding.Url) {
				return Uri.EscapeDataString(text.Trim());
			}
			StringBuilder stringBuilder=new StringBuilder();
			for(int i=0;i<text.Length;i++) {
				char c=text[i];
				if(c=='"' || char.IsControl(c)) {
					continue;
				}
				stringBuilder.Append(c);
			}
			return stringBuilder.ToString().Trim();
		}
	}
}
