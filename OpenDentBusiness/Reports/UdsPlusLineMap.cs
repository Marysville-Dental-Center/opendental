// UDS+ report (Open Dental 25.4, package M2-UDS in docs/specs/port-backlog.md): the office's list of UDS dental service lines and the procedure
// codes counted on each. Stored as JSON in the UdsPlusDentalLineCodes preference. No codes ship with this program.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness {
	///<summary>One UDS dental service line, as the office enters it in the report's setup.</summary>
	public class UdsPlusLine {
		///<summary>The line as HRSA's UDS manual numbers it for the reporting year, for example "27". Required and unique.</summary>
		public string Line="";
		///<summary>The service the line counts, for example "Emergency services".</summary>
		public string Description="";
		///<summary>The procedure codes counted on the line, separated by commas, semicolons or spaces. A token is one of:
		///<para>a code, matched exactly: D1234;</para>
		///<para>a range of two codes of the same length joined by a hyphen, matched by codes of that length that sort between them: D1000-D1999;</para>
		///<para>a prefix ending in an asterisk, matched by every code that starts with it: D1*.</para>
		///Matching ignores case. A token with a hyphen whose two sides differ in length, such as a custom code D1234-L, is an exact code.</summary>
		public string Codes="";

		public UdsPlusLine Copy() {
			return (UdsPlusLine)MemberwiseClone();
		}
	}

	///<summary>The office's UDS dental service lines (preference UdsPlusDentalLineCodes). HRSA revises the lines each year, so they are data the office
	///maintains, not code. Stored as {"Lines":[{"Line":"..","Description":"..","Codes":".."},...]}; a bare array of lines is read too.</summary>
	public class UdsPlusLineMap {
		public List<UdsPlusLine> ListLines=new List<UdsPlusLine>();

		///<summary>Parsed code tokens, per line, built on first use.</summary>
		[JsonIgnore]
		private List<List<CodeRule>> _listRulesByLine;
		///<summary>Lines per procedure code already looked up.</summary>
		[JsonIgnore]
		private Dictionary<string,List<UdsPlusLine>> _dictLinesByCode;

		///<summary>Reads the preference value. A blank value is an empty map. Returns false, with the reason, when the text is not valid JSON in the
		///expected shape.</summary>
		public static bool TryParse(string json,out UdsPlusLineMap udsPlusLineMap,out string error) {
			udsPlusLineMap=new UdsPlusLineMap();
			error="";
			if(string.IsNullOrWhiteSpace(json)) {
				return true;
			}
			JToken jToken;
			try {
				jToken=JToken.Parse(json);
			}
			catch(Exception ex) {
				error="The UDS line codes setting is not valid JSON: "+ex.Message;
				return false;
			}
			JArray jArray=null;
			if(jToken is JArray) {
				jArray=(JArray)jToken;
			}
			else if(jToken is JObject) {
				JToken jTokenLines=((JObject)jToken).GetValue("Lines",StringComparison.OrdinalIgnoreCase);
				if(jTokenLines==null || jTokenLines.Type==JTokenType.Null) {
					return true;
				}
				jArray=jTokenLines as JArray;
			}
			if(jArray==null) {
				error="The UDS line codes setting must hold a list of lines.";
				return false;
			}
			foreach(JToken jTokenLine in jArray) {
				JObject jObject=jTokenLine as JObject;
				if(jObject==null) {
					error="Each UDS line must be an object with Line, Description and Codes.";
					return false;
				}
				UdsPlusLine udsPlusLine=new UdsPlusLine();
				udsPlusLine.Line=GetString(jObject,"Line");
				udsPlusLine.Description=GetString(jObject,"Description");
				udsPlusLine.Codes=GetString(jObject,"Codes");
				udsPlusLineMap.ListLines.Add(udsPlusLine);
			}
			return true;
		}

		///<summary>Like TryParse, but throws an ApplicationException with the reason.</summary>
		public static UdsPlusLineMap Parse(string json) {
			if(!TryParse(json,out UdsPlusLineMap udsPlusLineMap,out string error)) {
				throw new ApplicationException(error);
			}
			return udsPlusLineMap;
		}

		private static string GetString(JObject jObject,string name) {
			JToken jToken=jObject.GetValue(name,StringComparison.OrdinalIgnoreCase);
			if(jToken==null || jToken.Type==JTokenType.Null) {
				return "";
			}
			return jToken.ToString().Trim();
		}

		///<summary>The preference value. An empty map is a blank string, the shipped default.</summary>
		public string ToJson() {
			if(ListLines.Count==0) {
				return "";
			}
			JArray jArray=new JArray();
			foreach(UdsPlusLine udsPlusLine in ListLines) {
				jArray.Add(new JObject(
					new JProperty("Line",(udsPlusLine.Line??"").Trim()),
					new JProperty("Description",(udsPlusLine.Description??"").Trim()),
					new JProperty("Codes",(udsPlusLine.Codes??"").Trim())));
			}
			return new JObject(new JProperty("Lines",jArray)).ToString(Formatting.None);
		}

		///<summary>Problems that keep the map from being saved: a line without a number, a line number used twice, and code tokens that cannot be read.
		///Empty when the map is fine. A line without codes is allowed; it counts nothing.</summary>
		public List<string> Validate() {
			List<string> listErrors=new List<string>();
			HashSet<string> hashSetLines=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for(int i=0;i<ListLines.Count;i++) {
				string line=(ListLines[i].Line??"").Trim();
				string name=string.IsNullOrEmpty(line) ? "Row "+(i+1) : "Line "+line;
				if(string.IsNullOrEmpty(line)) {
					listErrors.Add(name+": the UDS line is blank.");
				}
				else if(!hashSetLines.Add(line)) {
					listErrors.Add(name+": this line is listed more than once.");
				}
				List<string> listCodeErrors=new List<string>();
				ParseCodes(ListLines[i].Codes,listCodeErrors);
				listErrors.AddRange(listCodeErrors.Select(x => name+": "+x));
			}
			return listErrors;
		}

		///<summary>The lines that count this procedure code, in setup order. Empty when none does.</summary>
		public List<UdsPlusLine> GetLinesForCode(string procCode) {
			string code=NormalizeCode(procCode);
			if(_dictLinesByCode==null) {
				_dictLinesByCode=new Dictionary<string,List<UdsPlusLine>>();
				_listRulesByLine=ListLines.Select(x => ParseCodes(x.Codes,null)).ToList();
			}
			if(_dictLinesByCode.TryGetValue(code,out List<UdsPlusLine> listLines)) {
				return listLines;
			}
			listLines=new List<UdsPlusLine>();
			if(code!="") {
				for(int i=0;i<ListLines.Count;i++) {
					if(_listRulesByLine[i].Any(x => x.IsMatch(code))) {
						listLines.Add(ListLines[i]);
					}
				}
			}
			_dictLinesByCode[code]=listLines;
			return listLines;
		}

		///<summary>True if the codes text of a line counts this procedure code.</summary>
		public static bool CodesMatch(string codes,string procCode) {
			string code=NormalizeCode(procCode);
			return code!="" && ParseCodes(codes,null).Any(x => x.IsMatch(code));
		}

		///<summary>Trimmed and upper case, as codes are compared.</summary>
		public static string NormalizeCode(string code) {
			return (code??"").Trim().ToUpperInvariant();
		}

		///<summary>Reads the codes text of a line. Tokens that cannot be read are skipped and described in listErrors when it is not null.</summary>
		private static List<CodeRule> ParseCodes(string codes,List<string> listErrors) {
			List<CodeRule> listCodeRules=new List<CodeRule>();
			string[] arrayTokens=(codes??"").Split(new[] { ',',';',' ','\t','\r','\n' },StringSplitOptions.RemoveEmptyEntries);
			foreach(string tokenRaw in arrayTokens) {
				string token=NormalizeCode(tokenRaw);
				if(token.EndsWith("*")) {
					string prefix=token.TrimEnd('*');
					if(prefix=="" || prefix.Contains("*")) {
						listErrors?.Add("'"+tokenRaw+"' needs the start of a code before the asterisk.");
						continue;
					}
					listCodeRules.Add(new CodeRule { Start=prefix,IsPrefix=true });
					continue;
				}
				if(token.Contains("*")) {
					listErrors?.Add("'"+tokenRaw+"': an asterisk is only allowed at the end.");
					continue;
				}
				string[] arrayParts=token.Split('-');
				if(arrayParts.Length==2 && arrayParts[0]!="" && arrayParts[1]!="" && arrayParts[0].Length==arrayParts[1].Length) {
					if(string.CompareOrdinal(arrayParts[0],arrayParts[1])>0) {
						listErrors?.Add("'"+tokenRaw+"': the range starts after it ends.");
						continue;
					}
					listCodeRules.Add(new CodeRule { Start=arrayParts[0],End=arrayParts[1] });
					continue;
				}
				listCodeRules.Add(new CodeRule { Start=token });
			}
			return listCodeRules;
		}

		///<summary>The dental service lines of HRSA's UDS Table 6A, names only and without codes, as a starting point for the setup. HRSA renumbers and
		///renames lines from year to year: the office checks them against the manual for the reporting year and enters the codes itself.</summary>
		public static List<UdsPlusLine> GetTable6ALineTemplate() {
			return new List<UdsPlusLine> {
				new UdsPlusLine { Line="27",Description="Emergency services" },
				new UdsPlusLine { Line="28",Description="Oral exams" },
				new UdsPlusLine { Line="29",Description="Prophylaxis, adult or child" },
				new UdsPlusLine { Line="30",Description="Sealants" },
				new UdsPlusLine { Line="31",Description="Fluoride treatment, adult or child" },
				new UdsPlusLine { Line="32",Description="Restorative services" },
				new UdsPlusLine { Line="33",Description="Oral surgery (extractions and other surgical procedures)" },
				new UdsPlusLine { Line="34",Description="Rehabilitative services (endodontics, periodontics, prosthodontics, orthodontics)" },
			};
		}

		///<summary>One code token: an exact code (Start only), a range (Start and End, same length) or a prefix.</summary>
		private class CodeRule {
			public string Start;
			public string End;
			public bool IsPrefix;

			///<summary>code is already normalized.</summary>
			public bool IsMatch(string code) {
				if(IsPrefix) {
					return code.StartsWith(Start,StringComparison.Ordinal);
				}
				if(End==null) {
					return code==Start;
				}
				return code.Length==Start.Length && string.CompareOrdinal(code,Start)>=0 && string.CompareOrdinal(code,End)<=0;
			}
		}
	}
}
