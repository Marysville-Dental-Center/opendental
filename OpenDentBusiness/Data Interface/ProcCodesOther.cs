// Other Procedure Codes (Canada, Open Dental 25.4 and 26.1; backlog package M2-CAN): imports the CDHA, DAC and RAMQ code systems, and updates
// existing codes of those systems and of CDA, from a code file the office supplies. No code text ships with this program.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>The code systems of the Other Procedure Codes window. Not stored in the database.</summary>
	public enum EnumOtherCodeSystem {
		///<summary>Canadian Dental Hygienists Association codes, for independent hygiene practices.</summary>
		[Description("CDHA")]
		Cdha,
		///<summary>Denturist Association of Canada codes, English descriptions.</summary>
		[Description("DAC (English)")]
		DacEnglish,
		///<summary>Denturist Association of Canada codes, French descriptions.</summary>
		[Description("DAC (French)")]
		DacFrench,
		///<summary>Régie de l'assurance maladie du Québec codes. Stored with a trailing R, which claims leave out.</summary>
		[Description("RAMQ")]
		Ramq,
		///<summary>Canadian Dental Association codes. Only updates existing codes (Advanced Options); the CDA Codes option of Procedure Code Tools
		///adds missing ones.</summary>
		[Description("CDA (Advanced Options only)")]
		Cda,
	}

	///<summary>The Advanced Options of the Other Procedure Codes window: fields of existing codes that an import may change.</summary>
	[Flags]
	public enum EnumOtherCodeUpdates {
		None=0,
		Descriptions=1,
		Abbreviations=2,
		TreatArea=4,
		PaintType=8,
	}

	///<summary>One code read from a code file. The fields left null were blank in the file.</summary>
	public class OtherCodeRow {
		///<summary>1-based line number, for messages.</summary>
		public int LineNumber;
		///<summary>The code as the file has it, before any suffix.</summary>
		public string ProcCode="";
		public string Descript="";
		public string AbbrDesc="";
		public TreatmentArea? TreatArea;
		public ToothPaintingType? PaintType;
		public double? CanadaTimeUnits;
	}

	///<summary>A change to a code that is already in the database. New is a copy of Old with the changed fields.</summary>
	[Serializable]
	public class OtherCodeChange {
		public ProcedureCode ProcedureCodeOld;
		public ProcedureCode ProcedureCodeNew;
	}

	///<summary>What an Other Procedure Codes import will do, worked out before anything is changed.</summary>
	[Serializable]
	public class OtherCodeImportPlan {
		public EnumOtherCodeSystem CodeSystem;
		public EnumOtherCodeUpdates Updates;
		///<summary>The procedure code category new codes go in. Blank for CDA, which adds no codes.</summary>
		public string CategoryName="";
		///<summary>New codes. ProcCat is set when the plan is applied.</summary>
		public List<ProcedureCode> ListAdds=new List<ProcedureCode>();
		public List<OtherCodeChange> ListChanges=new List<OtherCodeChange>();
		///<summary>Codes in the file that are already in the database and that the chosen Advanced Options leave as they are.</summary>
		public int CountSkipped;
		///<summary>CDA only: codes in the file that are not in the database. CDA never adds codes here.</summary>
		public int CountNotFound;
		///<summary>Rows that could not be used, one message each.</summary>
		public List<string> ListErrors=new List<string>();

		public bool HasWork() {
			return ListAdds.Count>0 || ListChanges.Count>0;
		}
	}

	///<summary>What an import did.</summary>
	[Serializable]
	public class OtherCodeImportResult {
		public int CountAdded;
		public int CountUpdated;
		public int CountSkipped;
		public int CountNotFound;
		public int CountRefused;
		public bool IsCategoryCreated;
		public long DefNumCategory;
	}

	///<summary>Other Procedure Codes (Procedure Code Tools, Canada). Open Dental downloads these code lists from its own servers; this program reads a
	///code file that the office supplies, because the code sets belong to their associations. The file is comma or tab separated text (a
	///spreadsheet's CSV or Unicode Text export): code, description, abbreviation, treatment area, paint type, time units. A header row is optional;
	///with one, the columns are found by name. Lines starting with # are comments.
	///Rules: each code system is imported into a procedure code category named after it; codes that already exist are skipped unless an Advanced
	///Option asks to update a field; codes are never deleted; RAMQ codes are stored with a trailing R; CDA only updates existing codes. Advanced
	///Options need the confirmation phrase (26.1).</summary>
	public class ProcCodesOther {
		///<summary>RAMQ codes are stored with this suffix so they never clash with CDA codes. Claims leave it out.</summary>
		public const string RAMQ_SUFFIX="R";
		///<summary>Open Dental 26.1 asks for a password before Advanced Options change existing codes in bulk. Its password is printed in its public
		///manual, so it is a speed bump against accidental mass edits, not security. This program asks the user to type this phrase instead; the
		///window shows it. INFERRED.</summary>
		public const string CONFIRMATION_PHRASE="UPDATE CODES";
		///<summary>procedurecode.Descript is varchar(255).</summary>
		public const int DESCRIPT_MAX_LENGTH=255;
		///<summary>procedurecode.AbbrDesc is varchar(50).</summary>
		public const int ABBR_MAX_LENGTH=50;
		private const string COLUMN_CODE="Code";
		private const string COLUMN_DESCRIPT="Descript";
		private const string COLUMN_ABBR="AbbrDesc";
		private const string COLUMN_TREAT_AREA="TreatArea";
		private const string COLUMN_PAINT_TYPE="PaintType";
		private const string COLUMN_TIME_UNITS="TimeUnits";
		///<summary>The column order of a file without a header row.</summary>
		private static readonly string[] _arrayColumnsDefault=new string[] { COLUMN_CODE,COLUMN_DESCRIPT,COLUMN_ABBR,COLUMN_TREAT_AREA,COLUMN_PAINT_TYPE,
			COLUMN_TIME_UNITS };

		#region Code systems
		///<summary>The name shown in the window and in log entries, for example "DAC (French)".</summary>
		public static string GetCodeSystemName(EnumOtherCodeSystem codeSystem) {
			Meth.NoCheckMiddleTierRole();
			if(codeSystem==EnumOtherCodeSystem.Cda) {
				return "CDA";
			}
			return codeSystem.GetDescription();
		}

		///<summary>The procedure code category that new codes of this system go in: the system's name. DAC English and French share "DAC", since they
		///are the same codes in two languages. Blank for CDA, which adds no codes. INFERRED (the category names are not public).</summary>
		public static string GetCategoryName(EnumOtherCodeSystem codeSystem) {
			Meth.NoCheckMiddleTierRole();
			switch(codeSystem) {
				case EnumOtherCodeSystem.Cdha:
					return "CDHA";
				case EnumOtherCodeSystem.DacEnglish:
				case EnumOtherCodeSystem.DacFrench:
					return "DAC";
				case EnumOtherCodeSystem.Ramq:
					return "RAMQ";
				default:
					return "";
			}
		}

		///<summary>False for CDA: the CDA Codes option of Procedure Code Tools adds CDA codes; here CDA is used only with Advanced Options.</summary>
		public static bool CanAddCodes(EnumOtherCodeSystem codeSystem) {
			Meth.NoCheckMiddleTierRole();
			return codeSystem!=EnumOtherCodeSystem.Cda;
		}

		///<summary>"" when the import can run with these choices, otherwise why not.</summary>
		public static string GetChoiceError(EnumOtherCodeSystem codeSystem,EnumOtherCodeUpdates updates) {
			Meth.NoCheckMiddleTierRole();
			if(!CanAddCodes(codeSystem) && updates==EnumOtherCodeUpdates.None) {
				return Lans.g("ProcCodesOther","CDA codes are only updated here. Choose at least one Advanced Option, or add missing CDA codes with the CDA "
					+"Codes option of Procedure Code Tools.");
			}
			return "";
		}

		///<summary>The code as it is stored: RAMQ codes get a trailing R, unless the file already has it (a list exported from this program).</summary>
		public static string GetStoredCode(EnumOtherCodeSystem codeSystem,string fileCode) {
			Meth.NoCheckMiddleTierRole();
			string code=(fileCode??"").Trim();
			if(codeSystem!=EnumOtherCodeSystem.Ramq || code=="" || code.EndsWith(RAMQ_SUFFIX,StringComparison.Ordinal)) {
				return code;
			}
			return code+RAMQ_SUFFIX;
		}

		///<summary>True when the code's category is the RAMQ category (named "RAMQ", any case).</summary>
		public static bool IsRamqCode(ProcedureCode procedureCode,List<Def> listDefsProcCodeCats=null) {
			Meth.NoCheckMiddleTierRole();
			if(procedureCode==null || procedureCode.ProcCat==0) {
				return false;
			}
			Def def=Defs.GetDef(DefCat.ProcCodeCats,procedureCode.ProcCat,listDefsProcCodeCats);
			return def!=null && string.Equals((def.ItemName??"").Trim(),GetCategoryName(EnumOtherCodeSystem.Ramq),StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The code a claim shows for a procedure. RAMQ codes leave out the trailing R that keeps them apart from CDA codes. Only when the
		///code sent is the procedure's own code and the code is in the RAMQ category; anything else (an alternate or medical code, a code sent that
		///the user typed) is returned unchanged.</summary>
		public static string GetCodeForClaim(string codeSent,ProcedureCode procedureCode,List<Def> listDefsProcCodeCats=null) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(codeSent) || procedureCode==null || codeSent!=procedureCode.ProcCode
				|| codeSent.Length<=RAMQ_SUFFIX.Length || !codeSent.EndsWith(RAMQ_SUFFIX,StringComparison.Ordinal))
			{
				return codeSent;
			}
			if(!IsRamqCode(procedureCode,listDefsProcCodeCats)) {
				return codeSent;
			}
			return codeSent.Substring(0,codeSent.Length-RAMQ_SUFFIX.Length);
		}
		#endregion Code systems

		#region Confirmation
		///<summary>26.1: Advanced Options change existing codes in bulk, so they need the confirmation phrase. A plain import does not.</summary>
		public static bool RequiresConfirmation(EnumOtherCodeUpdates updates) {
			Meth.NoCheckMiddleTierRole();
			return updates!=EnumOtherCodeUpdates.None;
		}

		///<summary>True when the typed text is the confirmation phrase, ignoring case and spaces around it.</summary>
		public static bool IsConfirmationPhrase(string typed) {
			Meth.NoCheckMiddleTierRole();
			return string.Equals((typed??"").Trim(),CONFIRMATION_PHRASE,StringComparison.OrdinalIgnoreCase);
		}
		#endregion Confirmation

		#region Parse
		///<summary>The text of a code file: UTF-16 or UTF-8 when the file starts with their byte order mark, UTF-8 when the bytes are valid UTF-8,
		///otherwise the computer's ANSI code page (spreadsheet programs often use it for CSV exports).</summary>
		public static string DecodeFileBytes(byte[] arrayBytes) {
			Meth.NoCheckMiddleTierRole();
			if(arrayBytes==null || arrayBytes.Length==0) {
				return "";
			}
			if(arrayBytes.Length>=3 && arrayBytes[0]==0xEF && arrayBytes[1]==0xBB && arrayBytes[2]==0xBF) {
				return Encoding.UTF8.GetString(arrayBytes,3,arrayBytes.Length-3);
			}
			if(arrayBytes.Length>=2 && arrayBytes[0]==0xFF && arrayBytes[1]==0xFE) {
				return Encoding.Unicode.GetString(arrayBytes,2,arrayBytes.Length-2);
			}
			if(arrayBytes.Length>=2 && arrayBytes[0]==0xFE && arrayBytes[1]==0xFF) {
				return Encoding.BigEndianUnicode.GetString(arrayBytes,2,arrayBytes.Length-2);
			}
			try {
				return new UTF8Encoding(false,true).GetString(arrayBytes);
			}
			catch(ArgumentException) {
				return Encoding.Default.GetString(arrayBytes);
			}
		}

		///<summary>Reads the rows of a code file. Rows that can't be read are left out, with one message each in listErrors.</summary>
		public static List<OtherCodeRow> Parse(string text,out List<string> listErrors) {
			Meth.NoCheckMiddleTierRole();
			listErrors=new List<string>();
			List<OtherCodeRow> listRows=new List<OtherCodeRow>();
			string[] arrayLines=(text??"").Replace("\r\n","\n").Replace('\r','\n').Split('\n');
			char delimiter='\0';
			Dictionary<string,int> dictColumns=null;
			for(int i=0;i<arrayLines.Length;i++) {
				string line=arrayLines[i];
				if(line.Trim()=="" || line.TrimStart().StartsWith("#")) {
					continue;
				}
				if(delimiter=='\0') {
					delimiter=line.Contains("\t") ? '\t' : ',';
				}
				List<string> listCells=SplitLine(line,delimiter);
				if(dictColumns==null) {
					dictColumns=GetColumnsFromHeader(listCells);
					if(dictColumns!=null) {
						if(!dictColumns.ContainsKey(COLUMN_CODE)) {
							listErrors.Add(GetLinePrefix(i+1)+Lans.g("ProcCodesOther","the header row has no Code column."));
							return new List<OtherCodeRow>();
						}
						continue;//The header row itself.
					}
					dictColumns=new Dictionary<string,int>();
					for(int c=0;c<_arrayColumnsDefault.Length;c++) {
						dictColumns[_arrayColumnsDefault[c]]=c;
					}
				}
				OtherCodeRow otherCodeRow=new OtherCodeRow();
				otherCodeRow.LineNumber=i+1;
				string error=FillRow(otherCodeRow,listCells,dictColumns);
				if(error!="") {
					listErrors.Add(GetLinePrefix(i+1)+error);
					continue;
				}
				listRows.Add(otherCodeRow);
			}
			return listRows;
		}

		///<summary>Splits one line. A field that starts with a double quote (spaces before it ignored) ends at the closing quote; "" inside it is one
		///quote, as spreadsheet programs write a cell that holds the delimiter. A quote anywhere else is kept as text.</summary>
		public static List<string> SplitLine(string line,char delimiter) {
			Meth.NoCheckMiddleTierRole();
			List<string> listCells=new List<string>();
			int i=0;
			while(true) {
				int start=i;
				while(i<line.Length && line[i]==' ') {
					i++;
				}
				StringBuilder stringBuilder=new StringBuilder();
				if(i<line.Length && line[i]=='"') {
					i++;
					while(i<line.Length) {
						if(line[i]=='"') {
							if(i+1<line.Length && line[i+1]=='"') {
								stringBuilder.Append('"');
								i+=2;
								continue;
							}
							i++;
							break;
						}
						stringBuilder.Append(line[i]);
						i++;
					}
					//Anything between the closing quote and the delimiter is kept.
					while(i<line.Length && line[i]!=delimiter) {
						stringBuilder.Append(line[i]);
						i++;
					}
				}
				else {
					i=start;
					while(i<line.Length && line[i]!=delimiter) {
						stringBuilder.Append(line[i]);
						i++;
					}
				}
				listCells.Add(stringBuilder.ToString().Trim());
				if(i>=line.Length) {
					break;
				}
				i++;//The delimiter.
			}
			return listCells;
		}

		///<summary>Null when the cells are not a header row (the first cell is not a name for the code column). Otherwise the column index per known
		///column; columns with other names are ignored.</summary>
		private static Dictionary<string,int> GetColumnsFromHeader(List<string> listCells) {
			if(listCells.Count==0 || GetColumnForHeader(listCells[0])!=COLUMN_CODE) {
				return null;
			}
			Dictionary<string,int> dictColumns=new Dictionary<string,int>();
			for(int i=0;i<listCells.Count;i++) {
				string column=GetColumnForHeader(listCells[i]);
				if(column!="" && !dictColumns.ContainsKey(column)) {
					dictColumns[column]=i;
				}
			}
			return dictColumns;
		}

		///<summary>The column a header names, ignoring case, spaces and punctuation. "" for a header this import does not read.</summary>
		private static string GetColumnForHeader(string header) {
			string name=new string((header??"").Where(x => char.IsLetter(x)).ToArray()).ToLowerInvariant();
			switch(name) {
				case "code":
				case "proccode":
				case "procedurecode":
				case "codes":
					return COLUMN_CODE;
				case "description":
				case "descript":
				case "desc":
					return COLUMN_DESCRIPT;
				case "abbreviation":
				case "abbrdesc":
				case "abbr":
					return COLUMN_ABBR;
				case "treatmentarea":
				case "treatarea":
				case "area":
					return COLUMN_TREAT_AREA;
				case "painttype":
				case "paint":
					return COLUMN_PAINT_TYPE;
				case "timeunits":
				case "canadatimeunits":
				case "units":
				case "unitsoftime":
					return COLUMN_TIME_UNITS;
				default:
					return "";
			}
		}

		private static string GetLinePrefix(int lineNumber) {
			return Lans.g("ProcCodesOther","Line")+" "+lineNumber+": ";
		}

		///<summary>Returns "" or why the row can't be used.</summary>
		private static string FillRow(OtherCodeRow otherCodeRow,List<string> listCells,Dictionary<string,int> dictColumns) {
			otherCodeRow.ProcCode=GetCell(listCells,dictColumns,COLUMN_CODE);
			if(otherCodeRow.ProcCode=="") {
				return Lans.g("ProcCodesOther","no code.");
			}
			otherCodeRow.Descript=GetCell(listCells,dictColumns,COLUMN_DESCRIPT);
			if(otherCodeRow.Descript.Length>DESCRIPT_MAX_LENGTH) {
				return Lans.g("ProcCodesOther","the description is longer than")+" "+DESCRIPT_MAX_LENGTH+" "+Lans.g("ProcCodesOther","characters.");
			}
			otherCodeRow.AbbrDesc=GetCell(listCells,dictColumns,COLUMN_ABBR);
			if(otherCodeRow.AbbrDesc.Length>ABBR_MAX_LENGTH) {
				return Lans.g("ProcCodesOther","the abbreviation is longer than")+" "+ABBR_MAX_LENGTH+" "+Lans.g("ProcCodesOther","characters.");
			}
			string value=GetCell(listCells,dictColumns,COLUMN_TREAT_AREA);
			if(value!="") {
				if(!TryParseTreatArea(value,out TreatmentArea treatmentArea)) {
					return Lans.g("ProcCodesOther","invalid treatment area:")+" "+value;
				}
				otherCodeRow.TreatArea=treatmentArea;
			}
			value=GetCell(listCells,dictColumns,COLUMN_PAINT_TYPE);
			if(value!="") {
				if(!TryParseEnum(value,out ToothPaintingType toothPaintingType)) {
					return Lans.g("ProcCodesOther","invalid paint type:")+" "+value;
				}
				otherCodeRow.PaintType=toothPaintingType;
			}
			value=GetCell(listCells,dictColumns,COLUMN_TIME_UNITS);
			if(value!="") {
				if(!TryParseTimeUnits(value,out double timeUnits)) {
					return Lans.g("ProcCodesOther","invalid time units:")+" "+value;
				}
				otherCodeRow.CanadaTimeUnits=timeUnits;
			}
			return "";
		}

		private static string GetCell(List<string> listCells,Dictionary<string,int> dictColumns,string column) {
			if(!dictColumns.TryGetValue(column,out int index) || index>=listCells.Count) {
				return "";
			}
			return listCells[index];
		}

		///<summary>The number or the name of a TreatmentArea, ignoring case and spaces; "Surface" and "Range" are accepted for Surf and ToothRange.
		///</summary>
		public static bool TryParseTreatArea(string value,out TreatmentArea treatmentArea) {
			Meth.NoCheckMiddleTierRole();
			string name=new string((value??"").Where(x => char.IsLetterOrDigit(x)).ToArray()).ToLowerInvariant();
			if(name=="surface") {
				treatmentArea=TreatmentArea.Surf;
				return true;
			}
			if(name=="range") {
				treatmentArea=TreatmentArea.ToothRange;
				return true;
			}
			return TryParseEnum(value,out treatmentArea);
		}

		///<summary>The number or the name of a value, ignoring case and spaces.</summary>
		private static bool TryParseEnum<T>(string value,out T result) where T:struct {
			result=default(T);
			string name=new string((value??"").Where(x => char.IsLetterOrDigit(x)).ToArray());
			if(name=="") {
				return false;
			}
			if(int.TryParse(name,NumberStyles.Integer,CultureInfo.InvariantCulture,out int number)) {
				if(!Enum.IsDefined(typeof(T),number)) {
					return false;
				}
				result=(T)Enum.ToObject(typeof(T),number);
				return true;
			}
			foreach(T member in Enum.GetValues(typeof(T))) {
				if(string.Equals(member.ToString(),name,StringComparison.OrdinalIgnoreCase)) {
					result=member;
					return true;
				}
			}
			return false;
		}

		///<summary>A number of 0 or more, with a period or (French Canadian files) a comma as the decimal separator.</summary>
		private static bool TryParseTimeUnits(string value,out double timeUnits) {
			string text=value.Trim().Replace(',','.');
			if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out timeUnits) || timeUnits<0 || double.IsInfinity(timeUnits)) {
				timeUnits=0;
				return false;
			}
			return true;
		}
		#endregion Parse

		#region Plan
		///<summary>Works out what the import will do, without changing anything. listProcedureCodesExisting is every code in the database. Rows whose
		///code breaks the procedure code rules (comma, hyphen, more than 15 characters with any suffix), rows listed twice and new codes without a
		///description are refused; listParseErrors are carried into the plan's errors.</summary>
		public static OtherCodeImportPlan CreatePlan(EnumOtherCodeSystem codeSystem,EnumOtherCodeUpdates updates,List<OtherCodeRow> listRows,
			List<ProcedureCode> listProcedureCodesExisting,List<string> listParseErrors=null)
		{
			Meth.NoCheckMiddleTierRole();
			OtherCodeImportPlan otherCodeImportPlan=new OtherCodeImportPlan();
			otherCodeImportPlan.CodeSystem=codeSystem;
			otherCodeImportPlan.Updates=updates;
			otherCodeImportPlan.CategoryName=GetCategoryName(codeSystem);
			if(listParseErrors!=null) {
				otherCodeImportPlan.ListErrors.AddRange(listParseErrors);
			}
			Dictionary<string,ProcedureCode> dictProcedureCodes=new Dictionary<string,ProcedureCode>(StringComparer.Ordinal);
			for(int i=0;i<listProcedureCodesExisting.Count;i++) {
				if(!dictProcedureCodes.ContainsKey(listProcedureCodesExisting[i].ProcCode)) {
					dictProcedureCodes[listProcedureCodesExisting[i].ProcCode]=listProcedureCodesExisting[i];
				}
			}
			HashSet<string> hashSetCodesSeen=new HashSet<string>(StringComparer.Ordinal);
			for(int i=0;i<listRows.Count;i++) {
				OtherCodeRow otherCodeRow=listRows[i];
				string code=GetStoredCode(codeSystem,otherCodeRow.ProcCode);
				string error=ProcedureCodes.GetProcCodeTextError(code);
				if(error!="") {
					otherCodeImportPlan.ListErrors.Add(GetLinePrefix(otherCodeRow.LineNumber)+error);
					continue;
				}
				if(!hashSetCodesSeen.Add(code)) {
					otherCodeImportPlan.ListErrors.Add(GetLinePrefix(otherCodeRow.LineNumber)+code+" "
						+Lans.g("ProcCodesOther","is listed more than once. Only the first row is used."));
					continue;
				}
				if(!dictProcedureCodes.TryGetValue(code,out ProcedureCode procedureCodeExisting)) {
					if(!CanAddCodes(codeSystem)) {
						otherCodeImportPlan.CountNotFound++;
						continue;
					}
					if(otherCodeRow.Descript=="") {
						otherCodeImportPlan.ListErrors.Add(GetLinePrefix(otherCodeRow.LineNumber)+code+" "+Lans.g("ProcCodesOther","has no description."));
						continue;
					}
					otherCodeImportPlan.ListAdds.Add(CreateProcedureCode(code,otherCodeRow));
					continue;
				}
				ProcedureCode procedureCodeNew=procedureCodeExisting.Copy();
				bool isChanged=false;
				if(updates.HasFlag(EnumOtherCodeUpdates.Descriptions) && otherCodeRow.Descript!="" && otherCodeRow.Descript!=procedureCodeExisting.Descript) {
					procedureCodeNew.Descript=otherCodeRow.Descript;
					isChanged=true;
				}
				if(updates.HasFlag(EnumOtherCodeUpdates.Abbreviations) && otherCodeRow.AbbrDesc!="" && otherCodeRow.AbbrDesc!=procedureCodeExisting.AbbrDesc) {
					procedureCodeNew.AbbrDesc=otherCodeRow.AbbrDesc;
					isChanged=true;
				}
				if(updates.HasFlag(EnumOtherCodeUpdates.TreatArea) && otherCodeRow.TreatArea.HasValue
					&& otherCodeRow.TreatArea.Value!=procedureCodeExisting.TreatArea)
				{
					procedureCodeNew.TreatArea=otherCodeRow.TreatArea.Value;
					isChanged=true;
				}
				if(updates.HasFlag(EnumOtherCodeUpdates.PaintType) && otherCodeRow.PaintType.HasValue
					&& otherCodeRow.PaintType.Value!=procedureCodeExisting.PaintType)
				{
					procedureCodeNew.PaintType=otherCodeRow.PaintType.Value;
					isChanged=true;
				}
				if(!isChanged) {
					otherCodeImportPlan.CountSkipped++;
					continue;
				}
				OtherCodeChange otherCodeChange=new OtherCodeChange();
				otherCodeChange.ProcedureCodeOld=procedureCodeExisting.Copy();
				otherCodeChange.ProcedureCodeNew=procedureCodeNew;
				otherCodeImportPlan.ListChanges.Add(otherCodeChange);
			}
			return otherCodeImportPlan;
		}

		///<summary>CreatePlan with every code in the database.</summary>
		public static OtherCodeImportPlan CreatePlanFromDb(EnumOtherCodeSystem codeSystem,EnumOtherCodeUpdates updates,List<OtherCodeRow> listRows,
			List<string> listParseErrors=null)
		{
			Meth.NoCheckMiddleTierRole();
			return CreatePlan(codeSystem,updates,listRows,ProcedureCodes.GetAllCodes(),listParseErrors);
		}

		///<summary>A new code from a row. Blank fields get the defaults of a new code: abbreviation the code itself, no treatment area, no paint
		///type, no time units, no default provider. INFERRED.</summary>
		private static ProcedureCode CreateProcedureCode(string code,OtherCodeRow otherCodeRow) {
			ProcedureCode procedureCode=new ProcedureCode();
			procedureCode.ProcCode=code;
			procedureCode.Descript=otherCodeRow.Descript;
			procedureCode.AbbrDesc=otherCodeRow.AbbrDesc=="" ? code : otherCodeRow.AbbrDesc;
			procedureCode.TreatArea=otherCodeRow.TreatArea??TreatmentArea.None;
			procedureCode.PaintType=otherCodeRow.PaintType??ToothPaintingType.None;
			procedureCode.CanadaTimeUnits=otherCodeRow.CanadaTimeUnits??0;
			procedureCode.ProvNumDefault=0;
			return procedureCode;
		}
		#endregion Plan

		#region Apply
		///<summary>Saves the plan: creates the system's procedure code category when the plan adds codes and none has that name (hidden ones count),
		///adds the codes that are still missing, saves the changes, and logs each added and changed code and one entry for the run. Never deletes
		///anything.</summary>
		public static OtherCodeImportResult Apply(OtherCodeImportPlan otherCodeImportPlan) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<OtherCodeImportResult>(MethodBase.GetCurrentMethod(),otherCodeImportPlan);
			}
			OtherCodeImportResult otherCodeImportResult=new OtherCodeImportResult();
			otherCodeImportResult.CountSkipped=otherCodeImportPlan.CountSkipped;
			otherCodeImportResult.CountNotFound=otherCodeImportPlan.CountNotFound;
			otherCodeImportResult.CountRefused=otherCodeImportPlan.ListErrors.Count;
			string systemName=GetCodeSystemName(otherCodeImportPlan.CodeSystem);
			if(otherCodeImportPlan.ListAdds.Count>0) {
				//Read from the database rather than the cache: the server of a middle tier may have an old cache.
				List<Def> listDefs=Crud.DefCrud.SelectMany("SELECT * FROM definition WHERE Category="+POut.Int((int)DefCat.ProcCodeCats)+" ORDER BY ItemOrder");
				Def def=listDefs.FirstOrDefault(x => x.ItemName==otherCodeImportPlan.CategoryName);
				if(def==null) {
					def=new Def();
					def.Category=DefCat.ProcCodeCats;
					def.ItemName=otherCodeImportPlan.CategoryName;
					def.ItemOrder=listDefs.Count==0 ? 0 : listDefs.Max(x => x.ItemOrder)+1;
					Defs.Insert(def);
					SecurityLogs.MakeLogEntry(EnumPermType.DefEdit,0,Lans.g("Defintions","Definition created:")+" "+def.ItemName+" "
						+Lans.g("Defintions","with category:")+" "+def.Category.GetDescription());
					otherCodeImportResult.IsCategoryCreated=true;
				}
				otherCodeImportResult.DefNumCategory=def.DefNum;
				HashSet<string> hashSetCodes=new HashSet<string>(Db.GetListString("SELECT ProcCode FROM procedurecode"),StringComparer.Ordinal);
				for(int i=0;i<otherCodeImportPlan.ListAdds.Count;i++) {
					ProcedureCode procedureCode=otherCodeImportPlan.ListAdds[i];
					if(!hashSetCodes.Add(procedureCode.ProcCode)) {
						otherCodeImportResult.CountSkipped++;//Added by someone else since the plan was made.
						continue;
					}
					procedureCode.ProcCat=def.DefNum;
					ProcedureCodes.Insert(procedureCode);
					SecurityLogs.MakeLogEntry(EnumPermType.ProcCodeEdit,0,"Code "+procedureCode.ProcCode+" "
						+Lans.g("ProcCodesOther","added by the Other Procedure Codes import")+" ("+systemName+").",procedureCode.CodeNum,DateTime.MinValue);
					otherCodeImportResult.CountAdded++;
				}
			}
			for(int i=0;i<otherCodeImportPlan.ListChanges.Count;i++) {
				OtherCodeChange otherCodeChange=otherCodeImportPlan.ListChanges[i];
				//Writes only the fields that differ between the two, so a change made meanwhile to any other field is kept.
				if(!ProcedureCodes.Update(otherCodeChange.ProcedureCodeNew,otherCodeChange.ProcedureCodeOld)) {
					continue;
				}
				SecurityLogs.MakeLogEntry(EnumPermType.ProcCodeEdit,0,GetChangeLogText(otherCodeChange,systemName),
					otherCodeChange.ProcedureCodeNew.CodeNum,DateTime.MinValue);
				otherCodeImportResult.CountUpdated++;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.ProcCodeEdit,0,GetLogText(otherCodeImportPlan,otherCodeImportResult));
			return otherCodeImportResult;
		}

		///<summary>One log entry per changed code: each changed field, old and new value.</summary>
		public static string GetChangeLogText(OtherCodeChange otherCodeChange,string systemName) {
			Meth.NoCheckMiddleTierRole();
			ProcedureCode procedureCodeOld=otherCodeChange.ProcedureCodeOld;
			ProcedureCode procedureCodeNew=otherCodeChange.ProcedureCodeNew;
			List<string> listChanges=new List<string>();
			if(procedureCodeOld.Descript!=procedureCodeNew.Descript) {
				listChanges.Add(Lans.g("ProcCodesOther","description changed from")+" '"+procedureCodeOld.Descript+"' "+Lans.g("ProcCodesOther","to")+" '"
					+procedureCodeNew.Descript+"'");
			}
			if(procedureCodeOld.AbbrDesc!=procedureCodeNew.AbbrDesc) {
				listChanges.Add(Lans.g("ProcCodesOther","abbreviation changed from")+" '"+procedureCodeOld.AbbrDesc+"' "+Lans.g("ProcCodesOther","to")+" '"
					+procedureCodeNew.AbbrDesc+"'");
			}
			if(procedureCodeOld.TreatArea!=procedureCodeNew.TreatArea) {
				listChanges.Add(Lans.g("ProcCodesOther","treatment area changed from")+" '"+procedureCodeOld.TreatArea+"' "+Lans.g("ProcCodesOther","to")+" '"
					+procedureCodeNew.TreatArea+"'");
			}
			if(procedureCodeOld.PaintType!=procedureCodeNew.PaintType) {
				listChanges.Add(Lans.g("ProcCodesOther","paint type changed from")+" '"+procedureCodeOld.PaintType+"' "+Lans.g("ProcCodesOther","to")+" '"
					+procedureCodeNew.PaintType+"'");
			}
			return "Code "+procedureCodeNew.ProcCode+" "+string.Join(", ",listChanges)+" ("+Lans.g("ProcCodesOther","Other Procedure Codes import")+", "
				+systemName+").";
		}

		///<summary>The run's log entry and the message shown afterwards: the code system, the counts and the Advanced Options used.</summary>
		public static string GetLogText(OtherCodeImportPlan otherCodeImportPlan,OtherCodeImportResult otherCodeImportResult) {
			Meth.NoCheckMiddleTierRole();
			string text=Lans.g("ProcCodesOther","Other Procedure Codes import")+", "+GetCodeSystemName(otherCodeImportPlan.CodeSystem)+": "
				+otherCodeImportResult.CountAdded+" "+Lans.g("ProcCodesOther","added")+", "
				+otherCodeImportResult.CountUpdated+" "+Lans.g("ProcCodesOther","updated")+", "
				+otherCodeImportResult.CountSkipped+" "+Lans.g("ProcCodesOther","skipped");
			if(otherCodeImportResult.CountNotFound>0) {
				text+=", "+otherCodeImportResult.CountNotFound+" "+Lans.g("ProcCodesOther","not found");
			}
			if(otherCodeImportResult.CountRefused>0) {
				text+=", "+otherCodeImportResult.CountRefused+" "+Lans.g("ProcCodesOther","refused");
			}
			text+=".";
			if(otherCodeImportPlan.Updates!=EnumOtherCodeUpdates.None) {
				text+=" "+Lans.g("ProcCodesOther","Advanced Options:")+" "+GetUpdatesText(otherCodeImportPlan.Updates)+".";
			}
			if(otherCodeImportResult.IsCategoryCreated) {
				text+=" "+Lans.g("ProcCodesOther","New category:")+" "+otherCodeImportPlan.CategoryName+".";
			}
			return text;
		}

		///<summary>The Advanced Options, for example "Update descriptions, Update paint type".</summary>
		public static string GetUpdatesText(EnumOtherCodeUpdates updates) {
			Meth.NoCheckMiddleTierRole();
			List<string> listTexts=new List<string>();
			if(updates.HasFlag(EnumOtherCodeUpdates.Descriptions)) {
				listTexts.Add(Lans.g("ProcCodesOther","Update descriptions"));
			}
			if(updates.HasFlag(EnumOtherCodeUpdates.Abbreviations)) {
				listTexts.Add(Lans.g("ProcCodesOther","Update abbreviations"));
			}
			if(updates.HasFlag(EnumOtherCodeUpdates.TreatArea)) {
				listTexts.Add(Lans.g("ProcCodesOther","Update treatment area"));
			}
			if(updates.HasFlag(EnumOtherCodeUpdates.PaintType)) {
				listTexts.Add(Lans.g("ProcCodesOther","Update paint type"));
			}
			return string.Join(", ",listTexts);
		}

		///<summary>What the plan will do, shown before anything is saved. Lists the first 20 refused rows.</summary>
		public static string GetPlanSummary(OtherCodeImportPlan otherCodeImportPlan) {
			Meth.NoCheckMiddleTierRole();
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lans.g("ProcCodesOther","Code system:")+" "+GetCodeSystemName(otherCodeImportPlan.CodeSystem));
			if(otherCodeImportPlan.ListAdds.Count>0) {
				stringBuilder.AppendLine(Lans.g("ProcCodesOther","Codes to add:")+" "+otherCodeImportPlan.ListAdds.Count+" ("
					+Lans.g("ProcCodesOther","category")+" "+otherCodeImportPlan.CategoryName+")");
			}
			else {
				stringBuilder.AppendLine(Lans.g("ProcCodesOther","Codes to add:")+" 0");
			}
			stringBuilder.AppendLine(Lans.g("ProcCodesOther","Codes to update:")+" "+otherCodeImportPlan.ListChanges.Count);
			stringBuilder.AppendLine(Lans.g("ProcCodesOther","Codes skipped (already present):")+" "+otherCodeImportPlan.CountSkipped);
			if(otherCodeImportPlan.CountNotFound>0) {
				stringBuilder.AppendLine(Lans.g("ProcCodesOther","Codes not in the database (CDA codes are not added here):")+" "
					+otherCodeImportPlan.CountNotFound);
			}
			if(otherCodeImportPlan.Updates!=EnumOtherCodeUpdates.None) {
				stringBuilder.AppendLine(Lans.g("ProcCodesOther","Advanced Options:")+" "+GetUpdatesText(otherCodeImportPlan.Updates));
			}
			if(otherCodeImportPlan.ListErrors.Count>0) {
				stringBuilder.AppendLine(Lans.g("ProcCodesOther","Rows refused:")+" "+otherCodeImportPlan.ListErrors.Count);
				for(int i=0;i<otherCodeImportPlan.ListErrors.Count && i<20;i++) {
					stringBuilder.AppendLine("   "+otherCodeImportPlan.ListErrors[i]);
				}
				if(otherCodeImportPlan.ListErrors.Count>20) {
					stringBuilder.AppendLine("   ...");
				}
			}
			return stringBuilder.ToString();
		}
		#endregion Apply
	}
}
