// Vendor follow-up from public documentation (next/vendorpub-1): the Partner ID of the Medit Link program link. Medit's public article on its
// Open Dental integration documents a PartnerID key in the program link's arguments that assigns a partner (a lab) to every case created through
// the integration. Built from that article only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenPmsDentalBusiness.VendorDirect.ProgramLinks {
	///<summary>The arguments of the Medit Link program link (ProgName MeditLink, the row Open Dental's 21.1 conversion created in every database) and
	///its "Partner ID" property. Medit Link gets "--commandUuid" with Medit's command id and, for a patient, "--commandParam" with the program row's
	///Command line (a JSON object whose quotes are written \") with the patient filled in. With a Partner ID, the key PartnerID is added as the last
	///key of that object before the patient is filled in, exactly as Medit's article shows ("..., \"PartnerID\": \"xxxxx\"}"), so every case created
	///from the Chart is assigned to that partner in Medit Link. A blank Partner ID sends exactly what the link sent before.</summary>
	public static class MeditLinkPartner {
		///<summary>Medit's command id for the Open Dental integration, sent with --commandUuid.</summary>
		public const string CommandUuid="E02698AC-C1AE-4699-9307-F4A58DED47FE";
		///<summary>The key Medit's article documents, spelled as the article spells it.</summary>
		public const string PartnerIdKey="PartnerID";

		///<summary>programproperty.PropertyDesc values.</summary>
		public static class PropertyDescs {
			///<summary>Our addition: the Medit partner ID of the office's lab, or blank (nothing added, as before).</summary>
			public const string PartnerId="Partner ID";
		}

		///<summary>INFERRED: Medit's IDs are digits (its table lists 7307395); letters, hyphens and underscores are allowed too, nothing that needs
		///escaping on a command line or in JSON.</summary>
		private static readonly Regex _regexPartnerId=new Regex(@"^[A-Za-z0-9_-]+$",RegexOptions.CultureInvariant);

		///<summary>Inserts the blank ClinicNum 0 "Partner ID" row of the Medit Link program row if the row exists and has none. The program row itself
		///is never inserted or changed (Open Dental's conversion made it). Returns true if the property was inserted; the caller refreshes the
		///program property cache (ProgramLinkSetupRegistry does). Never calls Medit.</summary>
		public static bool EnsureProperty() {
			Meth.NoCheckMiddleTierRole();
			Program program=Programs.GetCur(ProgramName.MeditLink);
			if(program==null) {
				return false;
			}
			if(ProgramProperties.GetListForProgramAndClinic(program.ProgramNum,0).Any(x => x.PropertyDesc==PropertyDescs.PartnerId)) {
				return false;
			}
			ProgramProperties.InsertIfMissing(program.ProgramNum,PropertyDescs.PartnerId,0,"",false,false);
			return true;
		}

		///<summary>The program row's Partner ID (ClinicNum 0), trimmed; blank when there is no row.</summary>
		public static string GetPartnerId(Program program) {
			Meth.NoCheckMiddleTierRole();
			if(program==null) {
				return "";
			}
			return ProgramProperties.GetValOrDefault(program.ProgramNum,PropertyDescs.PartnerId,0).Trim();
		}

		///<summary>Why the Partner ID can't be added to this Command line, for the user, or "" when it can (or is blank).</summary>
		public static string GetPartnerIdProblem(string commandLine,string partnerId) {
			Meth.NoCheckMiddleTierRole();
			string id=(partnerId??"").Trim();
			if(id=="") {
				return "";
			}
			if(!_regexPartnerId.IsMatch(id)) {
				return Lans.g("MeditLink","The Partner ID of the Medit Link program link may only hold letters, digits, hyphens and underscores.");
			}
			string text=(commandLine??"").TrimEnd();
			if(text.IndexOf(PartnerIdKey,StringComparison.OrdinalIgnoreCase)>=0) {
				return Lans.g("MeditLink","The Medit Link program link has a Partner ID and its Command line already holds a PartnerID. Remove one of them "
					+"in Setup, Program Links, Medit Link.");
			}
			if(!text.EndsWith("}")) {
				return Lans.g("MeditLink","The Partner ID of the Medit Link program link can only be added to a Command line that ends with } (the format "
					+"Medit gives). Check the Command line, or clear the Partner ID, in Setup, Program Links, Medit Link.");
			}
			return "";
		}

		///<summary>The Command line with ", \"PartnerID\": \"id\"" added before its closing "}", the last key of its JSON object as Medit's article
		///shows. The Command line unchanged when the Partner ID is blank. Call GetPartnerIdProblem first; a Command line it refuses is returned
		///unchanged.</summary>
		public static string AddPartnerId(string commandLine,string partnerId) {
			Meth.NoCheckMiddleTierRole();
			string text=commandLine??"";
			string id=(partnerId??"").Trim();
			if(id=="" || GetPartnerIdProblem(text,id)!="") {
				return text;
			}
			string before=text.TrimEnd();
			before=before.Substring(0,before.Length-1).TrimEnd();
			string separator=before.EndsWith("{") ? "" : ", ";//An empty object gets the key alone.
			return before+separator+"\\\""+PartnerIdKey+"\\\": \\\""+id+"\\\"}";
		}

		///<summary>The arguments for the button: "--commandUuid" and Medit's command id, and with a patient "--commandParam" and the Command line with
		///the Partner ID added (AddPartnerId) and the patient filled in (funcReplacePatient, Patients.ReplacePatient in the program). Each part is
		///in double quotes, as the link always sent them. Returns null, with problem set, when the Partner ID can't be added (GetPartnerIdProblem):
		///nothing may be started then. Without a patient there is no command parameter, so the Partner ID is not used.</summary>
		public static string GetArguments(string commandLine,string partnerId,Patient patient,Func<string,Patient,string> funcReplacePatient,
			out string problem)
		{
			Meth.NoCheckMiddleTierRole();
			problem="";
			string arguments="\"--commandUuid\" \""+CommandUuid+"\"";
			if(patient==null) {
				return arguments;
			}
			problem=GetPartnerIdProblem(commandLine,partnerId);
			if(problem!="") {
				return null;
			}
			string commandParam=AddPartnerId(commandLine??"",partnerId);
			if(funcReplacePatient!=null) {
				commandParam=funcReplacePatient(commandParam,patient);
			}
			return arguments+" \"--commandParam\" \""+commandParam+"\"";
		}

		///<summary>GetArguments for a program row: its Command line and Partner ID, the patient filled in by Patients.ReplacePatient.</summary>
		public static string GetArguments(Program program,Patient patient,out string problem) {
			Meth.NoCheckMiddleTierRole();
			return GetArguments(program?.CommandLine,GetPartnerId(program),patient,(x,y) => Patients.ReplacePatient(x,y),out problem);
		}
	}
}
