// V-BRIDGE2 (Open Dental 25.4, v25_4-bridges-trophy-enhanced-tags): the arguments the Trophy (Enhanced) program link passes to the imaging program.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text.RegularExpressions;
using CodeBase;

namespace OpenPmsDentalBusiness.VendorDirect.ProgramLinks {
	///<summary>Builds the Trophy (Enhanced) command line. A blank Command line in the program link keeps the 24.3 arguments, -P followed by the patient
	///folder and -N followed by "last, first". A filled one is used instead, with the patient replacement tags (Patients.ReplacePatient) and the
	///fork's [TrophyFolder] tag, which holds the full patient folder the bridge found or created. With a blank Storage Path and a filled Command
	///line the bridge skips the folder steps, as for the newer Carestream setups that look the patient up by an argument such as -D[PatNum];
	///[TrophyFolder] is then blank. Pure functions: no database, no file system.</summary>
	public static class TrophyEnhancedCommandLine {
		///<summary>The fork's tag for the full patient folder (Storage Path plus the patient's folder).</summary>
		public const string TagTrophyFolder="[TrophyFolder]";
		///<summary>The Storage Path program property of the Trophy (Enhanced) link.</summary>
		public const string PropStoragePath="Storage Path";
		///<summary>The help shown on the Command line box of the Trophy (Enhanced) link in Program Links.</summary>
		public const string CommandLineHint="Blank: the bridge sends -P and the patient folder, then -N and the patient's last and first name.\r\n"
			+"Filled: the bridge sends this text instead. Patient tags such as [PatNum], [ChartNumber], [LName] and [FName] are replaced, and "
			+"[TrophyFolder] is replaced with the patient's folder under the Storage Path.\r\n"
			+"With a blank Storage Path the folder is not looked up and [TrophyFolder] is blank, for example -D[PatNum].";

		///<summary>True if the program link's Command line replaces the built-in arguments.</summary>
		public static bool UsesCommandLine(string commandLine) {
			Meth.NoCheckMiddleTierRole();
			return !string.IsNullOrWhiteSpace(commandLine);
		}

		///<summary>True if the bridge skips the patient folder steps: a Command line is set and the Storage Path is blank. With a blank Command line a
		///blank Storage Path is still an error, as in 24.3.</summary>
		public static bool SkipsFolders(string commandLine,string storagePath) {
			Meth.NoCheckMiddleTierRole();
			return UsesCommandLine(commandLine) && string.IsNullOrWhiteSpace(storagePath);
		}

		///<summary>The 24.3 arguments: -P and the folder, -N and "last, first", no spaces after -P or -N, quotes removed from the names.</summary>
		public static string GetDefaultArguments(string patFolder,Patient patient) {
			Meth.NoCheckMiddleTierRole();
			//A space between the last and first name, so CareStream reads a first and last name. Quotes are not needed around these arguments.
			return "-P"+patFolder+" -N"+Tidy(patient.LName)+", "+Tidy(patient.FName);
		}

		///<summary>The arguments to pass. commandLine is the program link's Command line; blank gives GetDefaultArguments. Otherwise the patient tags
		///are replaced with funcReplacePatient (Patients.ReplacePatient in the program; a test can pass its own), then [TrophyFolder] (any case)
		///with patFolder ("" when the folder steps were skipped). The result is trimmed.</summary>
		public static string Build(string commandLine,Patient patient,string patFolder,Func<string,Patient,string> funcReplacePatient) {
			Meth.NoCheckMiddleTierRole();
			if(patient==null) {
				throw new ArgumentNullException(nameof(patient));
			}
			if(!UsesCommandLine(commandLine)) {
				return GetDefaultArguments(patFolder,patient);
			}
			if(funcReplacePatient==null) {
				throw new ArgumentNullException(nameof(funcReplacePatient));
			}
			string arguments=funcReplacePatient(commandLine,patient)??"";
			arguments=Regex.Replace(arguments,Regex.Escape(TagTrophyFolder),(patFolder??"").Replace("$","$$"),RegexOptions.IgnoreCase);
			return arguments.Trim();
		}

		///<summary>Removes double and single quotes, as the 24.3 bridge did for the names it sends.</summary>
		public static string Tidy(string str) {
			Meth.NoCheckMiddleTierRole();
			string retVal=(str??"").Replace("\"","");
			retVal=retVal.Replace("'","");
			return retVal;
		}
	}
}
