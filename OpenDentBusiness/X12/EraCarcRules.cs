// B-ERA: the entries of the preference "Don't auto-process claims w/ these CARCs" (EraNoAutoProcessCarcCodes) and how they match the adjustments
// on an ERA claim. Open Dental 25.2 says only that it added "more logic" to this preference; the rules below are this fork's design
// (docs/specs/features-25.2.md, v25_2-manage-era-carc-logic, and docs/port-notes/B-ERA.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenDentBusiness {
	///<summary>One entry of EraNoAutoProcessCarcCodes. A bare entry such as 97 matches that Claim Adjustment Reason Code under any group code, as
	///every entry did in 24.3. A qualified entry such as CO-97 matches only that group code and reason code pair.</summary>
	public class EraCarcRule {
		///<summary>CO, PR, OA, PI or CR, upper case. Blank for a bare entry, which matches any group code.</summary>
		public string GroupCode="";
		///<summary>The Claim Adjustment Reason Code, upper case.</summary>
		public string ReasonCode="";

		///<summary>True when the adjustment carries this entry's reason code (and group code, for a qualified entry) and a non-zero amount.
		///An adjustment of $0.00 never matches: some payers echo informational codes with no money attached.</summary>
		public bool IsMatch(Hx835_Adj adj) {
			if(adj==null || string.IsNullOrWhiteSpace(adj.ReasonCode)) {
				return false;
			}
			if(adj.AdjAmt==0) {
				return false;
			}
			if(!string.Equals(adj.ReasonCode.Trim(),ReasonCode,StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			if(GroupCode=="") {
				return true;
			}
			return string.Equals((adj.AdjCode??"").Trim(),GroupCode,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The entry as the office would type it, for example 97 or CO-97.</summary>
		public override string ToString() {
			if(GroupCode=="") {
				return ReasonCode;
			}
			return GroupCode+"-"+ReasonCode;
		}
	}

	///<summary>A listed entry found on an ERA claim: the adjustment it matched, and the procedure line it is on (null for a claim-level
	///adjustment).</summary>
	public class EraCarcMatch {
		public EraCarcRule Rule;
		public Hx835_Adj Adj;
		///<summary>Null when the adjustment is a claim-level (CLP loop) adjustment.</summary>
		public Hx835_Proc Proc;

		public bool IsClaimLevel {
			get {
				return Proc==null;
			}
		}

		///<summary>For example "CO-97 on procedure Z0220" or "PR-16 on the claim". Names the adjustment the payer sent, so the office can see which
		///entry of the preference held the claim back.</summary>
		public string GetDescription() {
			string code=(Adj.AdjCode??"").Trim();
			code=(code=="" ? "" : code+"-")+(Adj.ReasonCode??"").Trim();
			if(IsClaimLevel) {
				return code+" "+Lans.g("X835","on the claim");
			}
			return code+" "+Lans.g("X835","on procedure")+" "+Proc.ProcCodeBilled;
		}
	}

	///<summary>Parsing, validation and matching of the EraNoAutoProcessCarcCodes entries.</summary>
	public static class EraCarcRules {
		///<summary>The group codes an entry may be qualified with.</summary>
		public static readonly string[] ArrayGroupCodes=new[] { "CO","PR","OA","PI","CR" };
		private static readonly Regex _regexQualified=new Regex(@"^(CO|PR|OA|PI|CR)\s*-\s*(\S+)$");
		private static readonly Regex _regexValid=new Regex(@"^((CO|PR|OA|PI|CR)-)?[A-Z0-9]{1,5}$");

		///<summary>The text of the tooltip on the preference's text box.</summary>
		public static string GetSyntaxHelp() {
			return Lans.g("X835","Comma separated Claim Adjustment Reason Codes, for example 97,16,B7. A bare code holds back a claim that carries it "
				+"under any group code. Put a group code (CO, PR, OA, PI or CR) and a dash in front to hold back only that pair, for example CO-97. "
				+"Adjustments of $0.00 never hold a claim back.");
		}

		///<summary>The entries of a preference value, upper case and without duplicates. Blank entries are skipped. An entry that does not look like
		///GROUP-CODE is kept whole as a bare code, so any value 24.3 accepted keeps matching what it matched before.</summary>
		public static List<EraCarcRule> Parse(string prefValue) {
			List<EraCarcRule> listRules=new List<EraCarcRule>();
			if(string.IsNullOrWhiteSpace(prefValue)) {
				return listRules;
			}
			string[] arrayEntries=prefValue.Split(',');
			for(int i=0;i<arrayEntries.Length;i++) {
				string entry=arrayEntries[i].Trim().ToUpperInvariant();
				if(entry=="") {
					continue;
				}
				EraCarcRule rule=new EraCarcRule();
				Match match=_regexQualified.Match(entry);
				if(match.Success) {
					rule.GroupCode=match.Groups[1].Value;
					rule.ReasonCode=match.Groups[2].Value;
				}
				else {
					rule.ReasonCode=entry;
				}
				if(listRules.Any(x => x.GroupCode==rule.GroupCode && x.ReasonCode==rule.ReasonCode)) {
					continue;
				}
				listRules.Add(rule);
			}
			return listRules;
		}

		///<summary>The entries of the EraNoAutoProcessCarcCodes preference.</summary>
		public static List<EraCarcRule> GetRulesFromPref() {
			return Parse(PrefC.GetString(PrefName.EraNoAutoProcessCarcCodes));
		}

		///<summary>Checks a value before it is saved: every entry must be 1 to 5 letters or digits, optionally preceded by CO, PR, OA, PI or CR and a
		///dash. Case and spaces around entries do not matter. A blank value is valid (nothing is held back). Returns false and the reason when an
		///entry is not valid.</summary>
		public static bool IsValid(string prefValue,out string error) {
			error="";
			if(string.IsNullOrWhiteSpace(prefValue)) {
				return true;
			}
			string[] arrayEntries=prefValue.Split(',');
			for(int i=0;i<arrayEntries.Length;i++) {
				string entry=Regex.Replace(arrayEntries[i].Trim().ToUpperInvariant(),@"\s*-\s*","-");
				if(entry=="") {
					continue;
				}
				if(!_regexValid.IsMatch(entry)) {
					error=Lans.g("X835","Not a valid Claim Adjustment Reason Code entry:")+" "+arrayEntries[i].Trim()+"\r\n"
						+Lans.g("X835","Use 1 to 5 letters or digits, optionally preceded by CO, PR, OA, PI or CR and a dash, for example 97 or CO-97.");
					return false;
				}
			}
			return true;
		}

		///<summary>Every listed entry found on the ERA claim: claim-level adjustments first, then each procedure line's adjustments in order. Each
		///adjustment is reported once, with the first entry that matches it.</summary>
		public static List<EraCarcMatch> GetMatches(List<EraCarcRule> listRules,Hx835_Claim claimPaid) {
			List<EraCarcMatch> listMatches=new List<EraCarcMatch>();
			if(listRules==null || listRules.Count==0 || claimPaid==null) {
				return listMatches;
			}
			List<Hx835_Adj> listClaimAdjs=claimPaid.ListClaimAdjustments??new List<Hx835_Adj>();
			for(int i=0;i<listClaimAdjs.Count;i++) {
				EraCarcRule rule=listRules.FirstOrDefault(x => x.IsMatch(listClaimAdjs[i]));
				if(rule!=null) {
					listMatches.Add(new EraCarcMatch { Rule=rule,Adj=listClaimAdjs[i],Proc=null });
				}
			}
			List<Hx835_Proc> listProcs=claimPaid.ListProcs??new List<Hx835_Proc>();
			for(int p=0;p<listProcs.Count;p++) {
				List<Hx835_Adj> listProcAdjs=listProcs[p].ListProcAdjustments??new List<Hx835_Adj>();
				for(int i=0;i<listProcAdjs.Count;i++) {
					EraCarcRule rule=listRules.FirstOrDefault(x => x.IsMatch(listProcAdjs[i]));
					if(rule!=null) {
						listMatches.Add(new EraCarcMatch { Rule=rule,Adj=listProcAdjs[i],Proc=listProcs[p] });
					}
				}
			}
			return listMatches;
		}

		///<summary>The descriptions of the matches, without repeats, comma separated. Blank when there are none.</summary>
		public static string GetDescription(List<EraCarcMatch> listMatches) {
			if(listMatches==null || listMatches.Count==0) {
				return "";
			}
			return string.Join(", ",listMatches.Select(x => x.GetDescription()).Distinct());
		}
	}
}
