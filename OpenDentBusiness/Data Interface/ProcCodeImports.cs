using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>One code read from a code file.</summary>
	[Serializable]
	public class ProcCodeImportRow {
		///<summary>1-based line (text files) or element (XML files) number, for messages.</summary>
		public int LineNumber;
		public string ProcCode="";
		public string Descript="";
		public string AbbrDesc="";
		///<summary>The procedure code category name. Blank when the file gives none.</summary>
		public string ProcCatDescript="";
		///<summary>Blank when the file gives none.</summary>
		public string ProcTime="";
		public TreatmentArea TreatArea;
		public ToothPaintingType PaintType;
		public bool NoBillIns;
		public bool IsProsth;
		public bool IsHygiene;
		public double CanadaTimeUnits;
		///<summary>The code set removed this code: its Deleted column says so, or its category is "Obsolete".</summary>
		public bool IsDeleted;
	}

	///<summary>A code the import adds, with the category it goes in (ProcedureCode.ProcCatDescript is not serialized, so it travels separately).</summary>
	[Serializable]
	public class ProcCodeImportAdd {
		public ProcedureCode ProcedureCode;
		public string CategoryName;
	}

	///<summary>A change to a code that is already in the database.</summary>
	[Serializable]
	public class ProcCodeImportChange {
		public long CodeNum;
		public string ProcCode;
		///<summary>Blank when the description does not change.</summary>
		public string DescriptNew="";
		public string DescriptOld="";
		///<summary>Blank when the abbreviation does not change. Only filled in for codes that have none.</summary>
		public string AbbrDescNew="";
		public bool IsMoveToObsolete;
	}

	///<summary>What a code import will do, worked out before anything is changed.</summary>
	[Serializable]
	public class ProcCodeImportPlan {
		public EnumDentalCodeSystem CodeSystem;
		public List<ProcCodeImportAdd> ListAdds=new List<ProcCodeImportAdd>();
		public List<ProcCodeImportChange> ListChanges=new List<ProcCodeImportChange>();
		///<summary>Codes in the file that are already in the database and need no change (CDT), or that are left alone because CDA imports never
		///change existing codes.</summary>
		public int CountUnchanged;
		///<summary>Rows skipped by a CDT import because they are not D codes (D followed by four digits).</summary>
		public int CountNotInCodeSystem;
		///<summary>Rows marked deleted for codes that are not in the database: nothing to do.</summary>
		public int CountDeletedNotPresent;

		public int GetCountDescriptions() {
			return ListChanges.Count(x => x.DescriptNew!="");
		}

		public int GetCountAbbrs() {
			return ListChanges.Count(x => x.AbbrDescNew!="");
		}

		public int GetCountObsolete() {
			return ListChanges.Count(x => x.IsMoveToObsolete);
		}

		public bool HasWork() {
			return ListAdds.Count>0 || ListChanges.Count>0;
		}
	}

	///<summary>What an import did.</summary>
	[Serializable]
	public class ProcCodeImportResult {
		public int CountAdded;
		public int CountDescriptionsUpdated;
		public int CountAbbrsFilled;
		public int CountMovedToObsolete;
	}

	///<summary>Procedure Code Tools: the D Codes option (CDT, Open Dental 25.3 adds the 2026 set) and the CDA Codes option (Canada: the 2025 set
	///from 24.3.32, the 2026 set from 25.3.41 and 25.4). Open Dental ships the code text; this program never does (ADA and CDA code descriptions
	///are licensed). The office supplies the licensed code file and this class reads it:
	///- CDT: adds missing D codes, replaces the description of existing codes with the file's, fills in abbreviations that are blank, and moves the
	///  codes the file marks deleted to the hidden "Obsolete" category. Nothing is ever deleted.
	///- CDA: adds only missing codes and never changes existing ones (Open Dental's public Canadian Procedure Code Tools page).
	///File formats (see docs/port-notes/B-PROC.md):
	///- XML: a procedure code list in the format Procedure Codes, Export writes (ArrayOfProcedureCode), optionally with ProcCatDescript elements.
	///- Text: one code per line, tab or comma separated. With a header row the columns are found by name (ProcCode or Code, Descript, Description or
	///  Nomenclature, AbbrDesc, Category, TreatArea, PaintType, ProcTime, NoBillIns, IsProsth, IsHygiene, CanadaTimeUnits, Deleted). Without one
	///  they are in the order of Open Dental's old Canadian procedurecodes.txt (ProcCode, Descript, TreatArea, NoBillIns, IsProsth, IsHygiene,
	///  PaintType, ProcCatDescript, ProcTime, AbbrDesc, CanadaTimeUnits), plus an optional Deleted column. Lines that start with # are comments.</summary>
	public class ProcCodeImports {
		public const string CATEGORY_OBSOLETE="Obsolete";
		///<summary>INFERRED: the category of a new code when the file names none and no code of the same block is in the database.</summary>
		public const string CATEGORY_IMPORTED="Imported Codes";
		private static readonly Regex _regexCdt=new Regex(@"^D\d{4}$",RegexOptions.Compiled);

		#region Parse
		///<summary>The text of a code file: UTF-16 when it starts with a UTF-16 byte order mark (a spreadsheet's "Unicode Text" export), UTF-8 (with or
		///without a byte order mark) when the bytes are valid UTF-8, otherwise the computer's ANSI code page, which spreadsheet programs often use for
		///text exports.</summary>
		public static string DecodeFileBytes(byte[] arrayBytes) {
			Meth.NoCheckMiddleTierRole();
			if(arrayBytes==null || arrayBytes.Length==0) {
				return "";
			}
			if(arrayBytes.Length>=2 && arrayBytes[0]==0xFF && arrayBytes[1]==0xFE) {
				return Encoding.Unicode.GetString(arrayBytes,2,arrayBytes.Length-2);
			}
			if(arrayBytes.Length>=2 && arrayBytes[0]==0xFE && arrayBytes[1]==0xFF) {
				return Encoding.BigEndianUnicode.GetString(arrayBytes,2,arrayBytes.Length-2);
			}
			try {
				return new UTF8Encoding(false,true).GetString(arrayBytes).TrimStart('﻿');
			}
			catch(DecoderFallbackException) {
				return Encoding.Default.GetString(arrayBytes);
			}
		}

		///<summary>Reads a code file. Rows with errors are left out and described in listErrors. Later duplicates of a code are left out too.</summary>
		public static List<ProcCodeImportRow> Parse(string fileText,out List<string> listErrors) {
			Meth.NoCheckMiddleTierRole();
			listErrors=new List<string>();
			List<ProcCodeImportRow> listRows;
			string text=(fileText??"").TrimStart('﻿');
			if(text.TrimStart().StartsWith("<")) {
				listRows=ParseXml(text,listErrors);
			}
			else {
				listRows=ParseText(text,listErrors);
			}
			List<ProcCodeImportRow> listRowsUnique=new List<ProcCodeImportRow>();
			HashSet<string> hashSetCodes=new HashSet<string>();
			for(int i=0;i<listRows.Count;i++) {
				if(!hashSetCodes.Add(listRows[i].ProcCode)) {
					listErrors.Add(Lans.g("ProcCodeImports","Line")+" "+listRows[i].LineNumber+": "+Lans.g("ProcCodeImports","code listed twice, skipped:")+" "
						+listRows[i].ProcCode);
					continue;
				}
				listRowsUnique.Add(listRows[i]);
			}
			return listRowsUnique;
		}

		private static List<ProcCodeImportRow> ParseXml(string text,List<string> listErrors) {
			List<ProcCodeImportRow> listRows=new List<ProcCodeImportRow>();
			List<ProcedureCode> listProcedureCodes;
			XmlDocument xmlDocument=new XmlDocument();
			try {
				XmlSerializer xmlSerializer=new XmlSerializer(typeof(List<ProcedureCode>));
				using(StringReader stringReader=new StringReader(text)) {
					listProcedureCodes=(List<ProcedureCode>)xmlSerializer.Deserialize(stringReader);
				}
				xmlDocument.LoadXml(text);
			}
			catch(Exception ex) {
				listErrors.Add(Lans.g("ProcCodeImports","The XML file could not be read:")+" "+ex.GetBaseException().Message);
				return listRows;
			}
			//ProcCatDescript is not serialized by XmlSerializer. Read it from the elements, the way the N Codes resource carries it.
			List<string> listCategoryNames=new List<string>();
			XmlElement xmlElementRoot=xmlDocument.DocumentElement;
			if(xmlElementRoot!=null) {
				foreach(XmlNode xmlNode in xmlElementRoot.ChildNodes) {
					if(xmlNode.NodeType!=XmlNodeType.Element) {
						continue;
					}
					XmlNode xmlNodeCat=xmlNode.SelectSingleNode("ProcCatDescript");
					listCategoryNames.Add(xmlNodeCat==null ? "" : xmlNodeCat.InnerText);
				}
			}
			for(int i=0;i<listProcedureCodes.Count;i++) {
				ProcedureCode procedureCode=listProcedureCodes[i];
				ProcCodeImportRow procCodeImportRow=new ProcCodeImportRow();
				procCodeImportRow.LineNumber=i+1;
				procCodeImportRow.ProcCode=(procedureCode.ProcCode??"").Trim();
				procCodeImportRow.Descript=(procedureCode.Descript??"").Trim();
				procCodeImportRow.AbbrDesc=(procedureCode.AbbrDesc??"").Trim();
				procCodeImportRow.ProcCatDescript=i<listCategoryNames.Count ? listCategoryNames[i].Trim() : "";
				procCodeImportRow.ProcTime=(procedureCode.ProcTime??"").Trim();
				procCodeImportRow.TreatArea=procedureCode.TreatArea;
				procCodeImportRow.PaintType=procedureCode.PaintType;
				procCodeImportRow.NoBillIns=procedureCode.NoBillIns;
				procCodeImportRow.IsProsth=procedureCode.IsProsth;
				procCodeImportRow.IsHygiene=procedureCode.IsHygiene;
				procCodeImportRow.CanadaTimeUnits=procedureCode.CanadaTimeUnits;
				procCodeImportRow.IsDeleted=IsObsoleteName(procCodeImportRow.ProcCatDescript);
				string error=Validate(procCodeImportRow);
				if(error!="") {
					listErrors.Add(Lans.g("ProcCodeImports","Code")+" "+(i+1)+": "+error);
					continue;
				}
				listRows.Add(procCodeImportRow);
			}
			return listRows;
		}

		///<summary>The column a header name stands for, or "" when it is not one this import reads. Names are compared without case, spaces,
		///underscores or dashes.</summary>
		public static string GetColumnForHeader(string header) {
			Meth.NoCheckMiddleTierRole();
			string name=Regex.Replace((header??"").ToLowerInvariant(),@"[\s_\-]","");
			switch(name) {
				case "proccode":
				case "code":
				case "procedurecode":
				case "cdtcode":
				case "cdacode":
					return "ProcCode";
				case "descript":
				case "description":
				case "nomenclature":
					return "Descript";
				case "abbrdesc":
				case "abbreviation":
				case "abbr":
					return "AbbrDesc";
				case "proccatdescript":
				case "category":
				case "proccat":
					return "ProcCatDescript";
				case "treatarea":
				case "treatmentarea":
					return "TreatArea";
				case "painttype":
					return "PaintType";
				case "proctime":
				case "time":
					return "ProcTime";
				case "nobillins":
					return "NoBillIns";
				case "isprosth":
					return "IsProsth";
				case "ishygiene":
					return "IsHygiene";
				case "canadatimeunits":
				case "timeunits":
					return "CanadaTimeUnits";
				case "deleted":
				case "isdeleted":
				case "status":
					return "Deleted";
				default:
					return "";
			}
		}

		///<summary>The columns of the old Canadian procedurecodes.txt, then Deleted.</summary>
		private static readonly string[] _arrayColumnsPositional=new string[] {
			"ProcCode","Descript","TreatArea","NoBillIns","IsProsth","IsHygiene","PaintType","ProcCatDescript","ProcTime","AbbrDesc","CanadaTimeUnits","Deleted"
		};

		private static List<ProcCodeImportRow> ParseText(string text,List<string> listErrors) {
			List<ProcCodeImportRow> listRows=new List<ProcCodeImportRow>();
			string[] arrayLines=text.Replace("\r\n","\n").Replace("\r","\n").Split('\n');
			string[] arrayColumns=null;
			for(int i=0;i<arrayLines.Length;i++) {
				string line=arrayLines[i];
				if(line.Trim()=="" || line.TrimStart().StartsWith("#")) {
					continue;
				}
				List<string> listFields=SplitLine(line);
				if(arrayColumns==null) {
					//The first data line decides: a header row names the columns, otherwise they are positional.
					List<string> listColumnsHeader=listFields.Select(x => GetColumnForHeader(x)).ToList();
					if(listColumnsHeader.Contains("ProcCode")) {
						arrayColumns=listColumnsHeader.ToArray();
						continue;
					}
					arrayColumns=_arrayColumnsPositional;
				}
				ProcCodeImportRow procCodeImportRow=new ProcCodeImportRow();
				procCodeImportRow.LineNumber=i+1;
				string error="";
				for(int f=0;f<listFields.Count && f<arrayColumns.Length;f++) {
					error=SetField(procCodeImportRow,arrayColumns[f],listFields[f].Trim());
					if(error!="") {
						break;
					}
				}
				if(error=="") {
					if(IsObsoleteName(procCodeImportRow.ProcCatDescript)) {
						procCodeImportRow.IsDeleted=true;
					}
					error=Validate(procCodeImportRow);
				}
				if(error!="") {
					listErrors.Add(Lans.g("ProcCodeImports","Line")+" "+(i+1)+": "+error);
					continue;
				}
				listRows.Add(procCodeImportRow);
			}
			return listRows;
		}

		///<summary>Splits on tabs when the line has any, otherwise on commas. With either delimiter a field may be in double quotes, as spreadsheet
		///programs write a cell that holds the delimiter or a quote in both their tab and comma separated exports: a field that starts with a quote
		///(spaces before it are ignored) ends at the closing quote, and "" inside it is one quote. A quote anywhere else in a field is kept as text.</summary>
		public static List<string> SplitLine(string line) {
			Meth.NoCheckMiddleTierRole();
			char delimiter=',';
			if(line.Contains("\t")) {
				delimiter='\t';
			}
			List<string> listFields=new List<string>();
			StringBuilder stringBuilder=new StringBuilder();
			bool isInQuotes=false;
			bool isFieldQuoted=false;//The current field started with a quote. Text after its closing quote is kept as is.
			for(int i=0;i<line.Length;i++) {
				char c=line[i];
				if(isInQuotes) {
					if(c=='"') {
						if(i+1<line.Length && line[i+1]=='"') {
							stringBuilder.Append('"');
							i++;
						}
						else {
							isInQuotes=false;
						}
					}
					else {
						stringBuilder.Append(c);
					}
					continue;
				}
				if(c==delimiter) {
					listFields.Add(stringBuilder.ToString());
					stringBuilder.Clear();
					isFieldQuoted=false;
				}
				else if(c=='"' && !isFieldQuoted && stringBuilder.ToString().Trim()=="") {
					stringBuilder.Clear();
					isInQuotes=true;
					isFieldQuoted=true;
				}
				else {
					stringBuilder.Append(c);
				}
			}
			listFields.Add(stringBuilder.ToString());
			return listFields;
		}

		///<summary>Returns "" or the error.</summary>
		private static string SetField(ProcCodeImportRow procCodeImportRow,string column,string value) {
			switch(column) {
				case "ProcCode":
					procCodeImportRow.ProcCode=value;
					return "";
				case "Descript":
					procCodeImportRow.Descript=value;
					return "";
				case "AbbrDesc":
					procCodeImportRow.AbbrDesc=value;
					return "";
				case "ProcCatDescript":
					procCodeImportRow.ProcCatDescript=value;
					return "";
				case "ProcTime":
					procCodeImportRow.ProcTime=value;
					return "";
				case "TreatArea":
					if(!TryParseEnum(value,out TreatmentArea treatmentArea)) {
						return Lans.g("ProcCodeImports","invalid treatment area:")+" "+value;
					}
					procCodeImportRow.TreatArea=treatmentArea;
					return "";
				case "PaintType":
					if(!TryParseEnum(value,out ToothPaintingType toothPaintingType)) {
						return Lans.g("ProcCodeImports","invalid paint type:")+" "+value;
					}
					procCodeImportRow.PaintType=toothPaintingType;
					return "";
				case "NoBillIns":
					procCodeImportRow.NoBillIns=ParseBool(value);
					return "";
				case "IsProsth":
					procCodeImportRow.IsProsth=ParseBool(value);
					return "";
				case "IsHygiene":
					procCodeImportRow.IsHygiene=ParseBool(value);
					return "";
				case "CanadaTimeUnits":
					if(value=="") {
						return "";
					}
					if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double canadaTimeUnits) || canadaTimeUnits<0) {
						return Lans.g("ProcCodeImports","invalid time units:")+" "+value;
					}
					procCodeImportRow.CanadaTimeUnits=canadaTimeUnits;
					return "";
				case "Deleted":
					string valueLower=value.ToLowerInvariant();
					procCodeImportRow.IsDeleted=ParseBool(value) || valueLower=="deleted" || valueLower=="delete" || valueLower=="removed" || valueLower=="d";
					return "";
				default:
					return "";//A column this import does not read.
			}
		}

		///<summary>Blank gives the enum's 0 value. Accepts the number or the name, without regard to case.</summary>
		private static bool TryParseEnum<T>(string value,out T result) where T:struct {
			result=default(T);
			if(value=="") {
				return true;
			}
			if(int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int number)) {
				if(!Enum.IsDefined(typeof(T),number)) {
					return false;
				}
				result=(T)Enum.ToObject(typeof(T),number);
				return true;
			}
			if(Enum.TryParse(value,true,out T resultParsed) && Enum.IsDefined(typeof(T),resultParsed)) {
				result=resultParsed;
				return true;
			}
			return false;
		}

		public static bool ParseBool(string value) {
			Meth.NoCheckMiddleTierRole();
			string valueLower=(value??"").Trim().ToLowerInvariant();
			return valueLower=="1" || valueLower=="true" || valueLower=="yes" || valueLower=="y" || valueLower=="x";
		}

		private static bool IsObsoleteName(string categoryName) {
			return string.Equals((categoryName??"").Trim(),CATEGORY_OBSOLETE,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Returns "" or the error. Lengths follow the procedurecode columns.</summary>
		private static string Validate(ProcCodeImportRow procCodeImportRow) {
			if(procCodeImportRow.ProcCode=="") {
				return Lans.g("ProcCodeImports","no procedure code");
			}
			if(procCodeImportRow.ProcCode.Length>15) {
				return Lans.g("ProcCodeImports","procedure code longer than 15 characters:")+" "+procCodeImportRow.ProcCode;
			}
			//26.1 (package M2-CAN): no comma or hyphen in a code, the rule every other code import and Procedure Codes, New follow.
			string errorCodeText=ProcedureCodes.GetProcCodeTextError(procCodeImportRow.ProcCode);
			if(errorCodeText!="") {
				return errorCodeText;
			}
			if(procCodeImportRow.Descript.Length>255) {
				return Lans.g("ProcCodeImports","description longer than 255 characters for")+" "+procCodeImportRow.ProcCode;
			}
			if(procCodeImportRow.AbbrDesc.Length>50) {
				return Lans.g("ProcCodeImports","abbreviation longer than 50 characters for")+" "+procCodeImportRow.ProcCode;
			}
			if(procCodeImportRow.ProcTime!="" && (procCodeImportRow.ProcTime.Length>24 || !Regex.IsMatch(procCodeImportRow.ProcTime,@"^[/Xx]+$"))) {
				return Lans.g("ProcCodeImports","invalid time pattern for")+" "+procCodeImportRow.ProcCode+": "+procCodeImportRow.ProcTime;
			}
			return "";
		}
		#endregion Parse

		#region Plan
		///<summary>True for a CDT code: D followed by four digits.</summary>
		public static bool IsCdtCode(string procCode) {
			Meth.NoCheckMiddleTierRole();
			return procCode!=null && _regexCdt.IsMatch(procCode);
		}

		///<summary>True from October 30 through December 31, when the next year's codes are out but not valid on claims until January 1. Procedure
		///Code Tools warns before a D code import in that window, as Open Dental does.</summary>
		public static bool IsYearEndWindow(DateTime date) {
			Meth.NoCheckMiddleTierRole();
			return date.Date>=new DateTime(date.Year,10,30) && date.Date<=new DateTime(date.Year,12,31);
		}

		///<summary>Works out the import without touching the database. listProcedureCodesExisting: every procedure code. listDefsProcCodeCats: every
		///procedure code category, hidden ones included.</summary>
		public static ProcCodeImportPlan CreatePlan(List<ProcCodeImportRow> listRows,List<ProcedureCode> listProcedureCodesExisting,
			List<Def> listDefsProcCodeCats,EnumDentalCodeSystem codeSystem)
		{
			Meth.NoCheckMiddleTierRole();
			ProcCodeImportPlan procCodeImportPlan=new ProcCodeImportPlan();
			procCodeImportPlan.CodeSystem=codeSystem;
			Dictionary<string,ProcedureCode> dictProcedureCodes=new Dictionary<string,ProcedureCode>();
			for(int i=0;i<listProcedureCodesExisting.Count;i++) {
				if(!dictProcedureCodes.ContainsKey(listProcedureCodesExisting[i].ProcCode)) {
					dictProcedureCodes[listProcedureCodesExisting[i].ProcCode]=listProcedureCodesExisting[i];
				}
			}
			long defNumObsolete=listDefsProcCodeCats.FirstOrDefault(x => x.ItemName==CATEGORY_OBSOLETE)?.DefNum??0;
			for(int i=0;i<listRows.Count;i++) {
				ProcCodeImportRow procCodeImportRow=listRows[i];
				if(codeSystem==EnumDentalCodeSystem.CDT && !IsCdtCode(procCodeImportRow.ProcCode)) {
					procCodeImportPlan.CountNotInCodeSystem++;
					continue;
				}
				dictProcedureCodes.TryGetValue(procCodeImportRow.ProcCode,out ProcedureCode procedureCodeExisting);
				if(procedureCodeExisting==null) {
					if(procCodeImportRow.IsDeleted) {
						procCodeImportPlan.CountDeletedNotPresent++;
						continue;
					}
					ProcCodeImportAdd procCodeImportAdd=new ProcCodeImportAdd();
					procCodeImportAdd.ProcedureCode=ToProcedureCode(procCodeImportRow);
					procCodeImportAdd.CategoryName=procCodeImportRow.ProcCatDescript;
					if(procCodeImportAdd.CategoryName=="") {
						procCodeImportAdd.CategoryName=GetCategoryForNewCode(procCodeImportRow.ProcCode,listProcedureCodesExisting,listDefsProcCodeCats);
					}
					procCodeImportPlan.ListAdds.Add(procCodeImportAdd);
					continue;
				}
				if(codeSystem==EnumDentalCodeSystem.CDA) {
					procCodeImportPlan.CountUnchanged++;//CDA codes that exist are never changed.
					continue;
				}
				ProcCodeImportChange procCodeImportChange=new ProcCodeImportChange();
				procCodeImportChange.CodeNum=procedureCodeExisting.CodeNum;
				procCodeImportChange.ProcCode=procedureCodeExisting.ProcCode;
				procCodeImportChange.DescriptOld=procedureCodeExisting.Descript??"";
				if(procCodeImportRow.IsDeleted) {
					procCodeImportChange.IsMoveToObsolete=(defNumObsolete==0 || procedureCodeExisting.ProcCat!=defNumObsolete);
				}
				else {
					if(procCodeImportRow.Descript!="" && procCodeImportRow.Descript!=(procedureCodeExisting.Descript??"")) {
						procCodeImportChange.DescriptNew=procCodeImportRow.Descript;
					}
					if(procCodeImportRow.AbbrDesc!="" && string.IsNullOrWhiteSpace(procedureCodeExisting.AbbrDesc)) {
						procCodeImportChange.AbbrDescNew=procCodeImportRow.AbbrDesc;
					}
				}
				if(!procCodeImportChange.IsMoveToObsolete && procCodeImportChange.DescriptNew=="" && procCodeImportChange.AbbrDescNew=="") {
					procCodeImportPlan.CountUnchanged++;
					continue;
				}
				procCodeImportPlan.ListChanges.Add(procCodeImportChange);
			}
			return procCodeImportPlan;
		}

		///<summary>What the import will do, for the confirmation message. listErrors: the rows the file reader left out (at most 20 are listed).</summary>
		public static string GetPlanSummary(ProcCodeImportPlan procCodeImportPlan,List<string> listErrors) {
			Meth.NoCheckMiddleTierRole();
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lans.g("ProcCodeImports","Codes to add:")+" "+procCodeImportPlan.ListAdds.Count);
			if(procCodeImportPlan.CodeSystem==EnumDentalCodeSystem.CDT) {
				stringBuilder.AppendLine(Lans.g("ProcCodeImports","Descriptions to update:")+" "+procCodeImportPlan.GetCountDescriptions());
				stringBuilder.AppendLine(Lans.g("ProcCodeImports","Abbreviations to fill in:")+" "+procCodeImportPlan.GetCountAbbrs());
				stringBuilder.AppendLine(Lans.g("ProcCodeImports","Codes to move to the Obsolete category:")+" "+procCodeImportPlan.GetCountObsolete());
				stringBuilder.AppendLine(Lans.g("ProcCodeImports","Codes already up to date:")+" "+procCodeImportPlan.CountUnchanged);
				if(procCodeImportPlan.CountNotInCodeSystem>0) {
					stringBuilder.AppendLine(Lans.g("ProcCodeImports","Rows skipped because they are not D codes:")+" "+procCodeImportPlan.CountNotInCodeSystem);
				}
			}
			else {
				stringBuilder.AppendLine(Lans.g("ProcCodeImports","Codes already in the database, left unchanged:")+" "+procCodeImportPlan.CountUnchanged);
			}
			if(procCodeImportPlan.CountDeletedNotPresent>0) {
				stringBuilder.AppendLine(Lans.g("ProcCodeImports","Deleted codes that are not in the database:")+" "+procCodeImportPlan.CountDeletedNotPresent);
			}
			if(listErrors!=null && listErrors.Count>0) {
				stringBuilder.AppendLine();
				stringBuilder.AppendLine(Lans.g("ProcCodeImports","Rows that could not be read and are skipped:")+" "+listErrors.Count);
				for(int i=0;i<listErrors.Count && i<20;i++) {
					stringBuilder.AppendLine(listErrors[i]);
				}
				if(listErrors.Count>20) {
					stringBuilder.AppendLine("...");
				}
			}
			return stringBuilder.ToString();
		}

		private static ProcedureCode ToProcedureCode(ProcCodeImportRow procCodeImportRow) {
			ProcedureCode procedureCode=new ProcedureCode();
			procedureCode.ProcCode=procCodeImportRow.ProcCode;
			procedureCode.Descript=procCodeImportRow.Descript;
			procedureCode.AbbrDesc=procCodeImportRow.AbbrDesc;
			if(procCodeImportRow.ProcTime!="") {
				procedureCode.ProcTime=procCodeImportRow.ProcTime.ToUpperInvariant();
			}
			procedureCode.TreatArea=procCodeImportRow.TreatArea;
			procedureCode.PaintType=procCodeImportRow.PaintType;
			procedureCode.NoBillIns=procCodeImportRow.NoBillIns;
			procedureCode.IsProsth=procCodeImportRow.IsProsth;
			procedureCode.IsHygiene=procCodeImportRow.IsHygiene;
			procedureCode.CanadaTimeUnits=procCodeImportRow.CanadaTimeUnits;
			procedureCode.ProvNumDefault=0;//Imported codes never name a provider.
			procedureCode.DrugNDC="";
			procedureCode.RevenueCodeDefault="";
			procedureCode.DefaultClaimNote="";
			procedureCode.DefaultTPNote="";
			procedureCode.TaxCode="";
			procedureCode.PaintText="";
			procedureCode.DiagnosticCodes="";
			return procedureCode;
		}

		///<summary>INFERRED: the category of a new code when the file names none: the visible category that most codes of the same block already use
		///(the code without its last two characters, so D2741 goes with the D27xx codes), otherwise "Imported Codes". Nothing about the code's meaning is
		///shipped; the office's own categories decide.</summary>
		public static string GetCategoryForNewCode(string procCode,List<ProcedureCode> listProcedureCodesExisting,List<Def> listDefsProcCodeCats) {
			Meth.NoCheckMiddleTierRole();
			if(procCode!=null && procCode.Length>=4) {
				string block=procCode.Substring(0,procCode.Length-2);
				Dictionary<long,Def> dictDefsVisible=listDefsProcCodeCats.Where(x => !x.IsHidden).GroupBy(x => x.DefNum).ToDictionary(x => x.Key,x => x.First());
				var category=listProcedureCodesExisting
					.Where(x => x.ProcCode!=null && x.ProcCode.Length==procCode.Length && x.ProcCode.StartsWith(block) && dictDefsVisible.ContainsKey(x.ProcCat))
					.GroupBy(x => x.ProcCat)
					.Select(x => new { DefNum=x.Key,Count=x.Count() })
					.OrderByDescending(x => x.Count)
					.ThenBy(x => dictDefsVisible[x.DefNum].ItemOrder)
					.FirstOrDefault();
				if(category!=null) {
					return dictDefsVisible[category.DefNum].ItemName;
				}
			}
			return CATEGORY_IMPORTED;
		}
		#endregion Plan

		#region Apply
		///<summary>Reads the procedure codes and categories from the database and works out the plan.</summary>
		public static ProcCodeImportPlan CreatePlanFromDb(List<ProcCodeImportRow> listRows,EnumDentalCodeSystem codeSystem) {
			Meth.NoCheckMiddleTierRole();
			return CreatePlan(listRows,ProcedureCodes.GetAllCodes(),Defs.GetDefsForCategory(DefCat.ProcCodeCats),codeSystem);
		}

		///<summary>Saves the plan: inserts the new codes (creating categories by name as needed), updates descriptions and abbreviations, and moves
		///codes to the hidden Obsolete category (created if missing). Never deletes a code. Makes ProcCodeEdit log entries for every code and one for the
		///run. The caller refreshes the procedure code and definition caches and sends the signals.</summary>
		public static ProcCodeImportResult Apply(ProcCodeImportPlan procCodeImportPlan) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<ProcCodeImportResult>(MethodBase.GetCurrentMethod(),procCodeImportPlan);
			}
			ProcCodeImportResult procCodeImportResult=new ProcCodeImportResult();
			string source=procCodeImportPlan.CodeSystem==EnumDentalCodeSystem.CDA ? "CDA" : "CDT";
			for(int i=0;i<procCodeImportPlan.ListAdds.Count;i++) {
				ProcedureCode procedureCode=procCodeImportPlan.ListAdds[i].ProcedureCode;
				if(ProcedureCodeExistsInDb(procedureCode.ProcCode)) {
					continue;//Added by someone else since the plan was made.
				}
				procedureCode.ProcCat=GetOrCreateCategory(procCodeImportPlan.ListAdds[i].CategoryName,isHidden:false);
				Crud.ProcedureCodeCrud.Insert(procedureCode);
				SecurityLogs.MakeLogEntry(EnumPermType.ProcCodeEdit,0,"Code "+procedureCode.ProcCode+" added from the "+source+" code import.",
					procedureCode.CodeNum,DateTime.MinValue);
				procCodeImportResult.CountAdded++;
			}
			List<ProcCodeImportChange> listProcCodeImportChangesObsolete=procCodeImportPlan.ListChanges.FindAll(x => x.IsMoveToObsolete);
			if(listProcCodeImportChangesObsolete.Count>0) {
				long defNumObsolete=GetOrCreateCategory(CATEGORY_OBSOLETE,isHidden:true);
				string command="UPDATE procedurecode SET ProcCat="+POut.Long(defNumObsolete)+" "
					+"WHERE CodeNum IN("+string.Join(",",listProcCodeImportChangesObsolete.Select(x => POut.Long(x.CodeNum)))+")";
				Db.NonQ(command);
				for(int i=0;i<listProcCodeImportChangesObsolete.Count;i++) {
					SecurityLogs.MakeLogEntry(EnumPermType.ProcCodeEdit,0,"Code "+listProcCodeImportChangesObsolete[i].ProcCode
						+" moved to the "+CATEGORY_OBSOLETE+" category by the "+source+" code import.",listProcCodeImportChangesObsolete[i].CodeNum,DateTime.MinValue);
				}
				procCodeImportResult.CountMovedToObsolete=listProcCodeImportChangesObsolete.Count;
			}
			for(int i=0;i<procCodeImportPlan.ListChanges.Count;i++) {
				ProcCodeImportChange procCodeImportChange=procCodeImportPlan.ListChanges[i];
				List<string> listSets=new List<string>();
				if(procCodeImportChange.DescriptNew!="") {
					listSets.Add("Descript='"+POut.String(procCodeImportChange.DescriptNew)+"'");
				}
				if(procCodeImportChange.AbbrDescNew!="") {
					listSets.Add("AbbrDesc='"+POut.String(procCodeImportChange.AbbrDescNew)+"'");
				}
				if(listSets.Count==0) {
					continue;
				}
				string command="UPDATE procedurecode SET "+string.Join(",",listSets)+" WHERE CodeNum="+POut.Long(procCodeImportChange.CodeNum);
				Db.NonQ(command);
				if(procCodeImportChange.DescriptNew!="") {
					procCodeImportResult.CountDescriptionsUpdated++;
					SecurityLogs.MakeLogEntry(EnumPermType.ProcCodeEdit,0,"Code "+procCodeImportChange.ProcCode+" description changed by the "+source
						+" code import from '"+procCodeImportChange.DescriptOld+"'.",procCodeImportChange.CodeNum,DateTime.MinValue);
				}
				if(procCodeImportChange.AbbrDescNew!="") {
					procCodeImportResult.CountAbbrsFilled++;
				}
			}
			SecurityLogs.MakeLogEntry(EnumPermType.ProcCodeEdit,0,GetLogText(procCodeImportPlan.CodeSystem,procCodeImportResult));
			return procCodeImportResult;
		}

		///<summary>The log text of a run: the code system and the counts.</summary>
		public static string GetLogText(EnumDentalCodeSystem codeSystem,ProcCodeImportResult procCodeImportResult) {
			Meth.NoCheckMiddleTierRole();
			return (codeSystem==EnumDentalCodeSystem.CDA ? "CDA" : "CDT")+" code import from a user-supplied file: "
				+procCodeImportResult.CountAdded+" added, "
				+procCodeImportResult.CountDescriptionsUpdated+" descriptions updated, "
				+procCodeImportResult.CountAbbrsFilled+" abbreviations filled in, "
				+procCodeImportResult.CountMovedToObsolete+" moved to "+CATEGORY_OBSOLETE+".";
		}

		private static bool ProcedureCodeExistsInDb(string procCode) {
			string command="SELECT COUNT(*) FROM procedurecode WHERE ProcCode='"+POut.String(procCode)+"'";
			return Db.GetCount(command)!="0";
		}

		///<summary>The DefNum of the procedure code category with exactly this name (hidden ones included), inserting it when there is none. The
		///Obsolete category is made hidden if it is not already, as T Codes does.</summary>
		private static long GetOrCreateCategory(string categoryName,bool isHidden) {
			if(string.IsNullOrWhiteSpace(categoryName)) {
				categoryName=CATEGORY_IMPORTED;
			}
			categoryName=categoryName.Trim();
			List<Def> listDefs=Defs.GetDefsForCategory(DefCat.ProcCodeCats);
			Def def=listDefs.FirstOrDefault(x => x.ItemName==categoryName);
			if(def!=null) {
				if(isHidden && !def.IsHidden) {
					def.IsHidden=true;
					Defs.Update(def);
					Defs.RefreshCache();
					SecurityLogs.MakeLogEntry(EnumPermType.DefEdit,0,Lans.g("Defintions","Definition edited:")+" "+def.ItemName+" "
						+Lans.g("Defintions","with category:")+" "+def.Category.GetDescription());
				}
				return def.DefNum;
			}
			def=new Def();
			def.Category=DefCat.ProcCodeCats;
			def.ItemName=categoryName;
			def.ItemOrder=listDefs.Count;
			def.IsHidden=isHidden;
			Defs.Insert(def);
			Defs.RefreshCache();
			SecurityLogs.MakeLogEntry(EnumPermType.DefEdit,0,Lans.g("Defintions","Definition created:")+" "+def.ItemName+" "
				+Lans.g("Defintions","with category:")+" "+def.Category.GetDescription());
			return def.DefNum;
		}
		#endregion Apply
	}
}
