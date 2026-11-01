using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CodeBase;
using System.Xml;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using DataConnectionBase;

namespace OpenDentBusiness {
	///<summary></summary>
	public class BugSubmissions {

		public static IBugSubmissions MockBugSubmissions {
			get;
			set;
		}
		#region Get Methods
		///<summary>The Help, About, Diagnostics text. Gathered locally by LocalDiagnostics (M2-MISC, 25.4); nothing is sent anywhere. Kept so callers
		///built against the old name still work. patNum is the selected patient, or -1 when none.</summary>
		public static string GetDiagnostics(long patNum=-1) {
			Meth.NoCheckMiddleTierRole();
			return LocalDiagnostics.GetDiagnostics(patNum);
		}
		
		#endregion

		#region Misc Methods
		///<summary>Used to send the exception (with stack trace, patient number, server and database names) to Open Dental HQ.
		///Telemetry was removed: nothing is sent anywhere. The exception is only written to the local log. Always returns BugSubmissionResult.None.</summary>
		public static BugSubmissionResult SubmitException(Exception ex,string threadName="",long patNumCur=-1,string moduleName="") {
			Meth.NoCheckMiddleTierRole();
			return SubmitException(ex,out string displayMsg,threadName,patNumCur,moduleName);
		}

		///<summary>Used to send the exception to Open Dental HQ. Telemetry was removed: nothing is sent anywhere, the PrefName.SendUnhandledExceptionsToHQ
		///preference is ignored (treated as false), and the exception is only written to the local log. displayMsg is always null.
		///Always returns BugSubmissionResult.None.</summary>
		public static BugSubmissionResult SubmitException(Exception ex,out string displayMsg,string threadName="",long patNumCur=-1,string moduleName="") {
			Meth.NoCheckMiddleTierRole();
			displayMsg=null;
			if(MockBugSubmissions!=null) {
				return MockBugSubmissions.SubmitException(ex,threadName,patNumCur,moduleName);
			}
			if(ex!=null) {
				//Local logging only. Never let logging problems surface to the caller, which is usually already handling an error.
				ODException.SwallowAnyException(() => Logger.WriteException(ex,"BugSubmissions"));
			}
			return BugSubmissionResult.None;
		}

		/// <summary>
		/// Returns true if currentVersion has a pertinent update version in listFixedVersions.
		/// When true pertinentFixedVersion is set to the specific version that can be used.
		/// </summary>
		public static bool TryMatchPertinentFixedVersion(Version currentVersion,List<Version> listVersionsFixed,out Version versionPertinentFixed) {
			Meth.NoCheckMiddleTierRole();
			versionPertinentFixed=null;
			//Make sure that the current bug submission is at a version that is lower than all fixed versions.  Rare but it has happened.
			if(listVersionsFixed.All(x => x<currentVersion)) {
				//Not a single fixed version is higher than the bug submission that just came in.  Guaranteed to be a new bug.
			}
			//Only versions of the same Major and Minor matter.
			else if(listVersionsFixed.Any(x => x.Major==currentVersion.Major && x.Minor==currentVersion.Minor && x.Build<=currentVersion.Build)) {
				//There are fixed versions, they may or may not be pertinent to the bug submission version.
				//Only care about fixed versions that share the same Major version.  
				//E.g. a fixed version with a higher Major version doesn't necessarily mean this bug is fixed.  
				//subProgVersion = 17.4.32.0  and  listFixedVersions = { 16.8.23.0, 17.4.44.0, 18.1.1.0 }
				//As you can see, the presence of a "fix" being in 18.1.1.0 does not matter to the current bug submission.
				//The inverse can be applied to versions that are less than the current bug submission version.
				//Meaning, just because 16.8.23.0 has been flagged as "fixed", doesn't mean that all major versions above it are fixed as well.
			}
			else {
				//Fix is on more recent major version than what user is on.
				versionPertinentFixed=listVersionsFixed.Find(x => x>currentVersion);
			}
			return (versionPertinentFixed!=null);
		}
		
		///<summary>Removes file names and line numbers to reveal a simplified stack path.</summary>
		public static string SimplifyStackTrace(string stackTrace) {
			return Regex.Replace(stackTrace.ToLower(),@" in [a-z0-9.\\: ]+\n","\n");//Case insensitive.
		}
		#endregion
	}

	public enum BugSubmissionResult {
		///<summary></summary>
		None,
		///<summary>Submitter is not on support or there was an exception in the web method</summary>
		Failed,
		///<summary>Submitter must be on the most recent stable or any beta version.</summary>
		UpdateRequired,
		///<summary>Submitter sucessfully inserted a bugSubmission at HQ</summary>
		SuccessHashFound,
		///<summary>Submitter sucessfully inserted a bugSubmission at HQ and a hash row was also inserted.</summary>
		SuccessHashNeeded,
		///<summary>Submitter sucessfully inserted a bugSubmission at HQ and it was matched to a bug that is NOT currently flagged as fixed.</summary>
		SuccessMatched,
		///<summary>Submitter sucessfully inserted a bugSubmission at HQ and it was matched to a bug that is currently flagged as fixed.</summary>
		SuccessMatchedFixed,
	}

	///<summary>Used to test bug submissions.</summary>
	public interface IBugSubmissions {
		BugSubmissionResult SubmitException(Exception ex,string threadName="",long patNumCur=-1,string moduleName="");
	}
}