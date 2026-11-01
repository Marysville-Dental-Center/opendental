// Procedure code text rules (Open Dental 26.1, backlog package M2-CAN, feature v26_1-lists-proccode-no-hyphens).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;

namespace OpenDentBusiness {
	public partial class ProcedureCodes {
		///<summary>procedurecode.ProcCode is varchar(15).</summary>
		public const int PROC_CODE_MAX_LENGTH=15;
		///<summary>The characters a procedure code may not contain: code groups and carrier preauthorization lists (carrier.PreAuthCodes) store lists
		///of codes separated by commas, with hyphen ranges ("D0120,D0140-D0180"), so a code that holds one of them cannot be read back from such a
		///list. Comma, hyphen-minus and the non-breaking hyphen (U+2011), as Open Dental's 26.1 database document lists them.</summary>
		public static readonly char[] ArrayProcCodeCharsNotAllowed=new char[] { ',','-','‑' };

		///<summary>Returns "" when the text can be used as a new procedure code, otherwise the reason it can't, ready to show. Checks only the text
		///(blank, length, separators), not whether the code already exists. Codes added before 26.1 are never changed by this rule; Database
		///Maintenance lists them (ProcedurecodeCodeHasSeparator).</summary>
		public static string GetProcCodeTextError(string procCode) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(procCode)) {
				return Lans.g("ProcedureCodes","Code not allowed to be blank.");
			}
			if(procCode.Length>PROC_CODE_MAX_LENGTH) {
				return Lans.g("ProcedureCodes","Codes can't be longer than")+" "+PROC_CODE_MAX_LENGTH+" "+Lans.g("ProcedureCodes","characters:")+" "+procCode;
			}
			if(procCode.IndexOfAny(ArrayProcCodeCharsNotAllowed)>=0) {
				return Lans.g("ProcedureCodes","Codes can't contain commas or hyphens:")+" "+procCode;
			}
			return "";
		}

		///<summary>True when GetProcCodeTextError has nothing to say about the text.</summary>
		public static bool IsProcCodeTextAllowed(string procCode) {
			Meth.NoCheckMiddleTierRole();
			return GetProcCodeTextError(procCode)=="";
		}

		///<summary>For code imports: returns the codes whose text is allowed, in the same order, and puts one message per refused code in
		///listErrors.</summary>
		public static List<ProcedureCode> GetCodesWithAllowedText(List<ProcedureCode> listProcedureCodes,out List<string> listErrors) {
			Meth.NoCheckMiddleTierRole();
			listErrors=new List<string>();
			List<ProcedureCode> listProcedureCodesAllowed=new List<ProcedureCode>();
			if(listProcedureCodes==null) {
				return listProcedureCodesAllowed;
			}
			for(int i=0;i<listProcedureCodes.Count;i++) {
				string error=GetProcCodeTextError(listProcedureCodes[i].ProcCode);
				if(error!="") {
					listErrors.Add(error);
					continue;
				}
				listProcedureCodesAllowed.Add(listProcedureCodes[i]);
			}
			return listProcedureCodesAllowed;
		}

		///<summary>The codes in the list that break the 26.1 rule (comma or hyphen of either kind). Used by Database Maintenance.</summary>
		public static List<ProcedureCode> GetCodesWithSeparators(List<ProcedureCode> listProcedureCodes) {
			Meth.NoCheckMiddleTierRole();
			if(listProcedureCodes==null) {
				return new List<ProcedureCode>();
			}
			return listProcedureCodes.FindAll(x => x.ProcCode!=null && x.ProcCode.IndexOfAny(ArrayProcCodeCharsNotAllowed)>=0);
		}
	}
}
