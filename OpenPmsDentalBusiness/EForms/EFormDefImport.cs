// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace OpenPmsDentalBusiness {
	///<summary>Open Dental 25.4: Setup, eForms, Import accepts several exported eForm files at once. Files are imported in name order; a file that
	///fails validation is skipped and listed; a description that is already used gets " (2)" (or the next free number) appended; one EFormEdit Audit
	///Trail entry is written per imported form. A single file goes through the same path.</summary>
	public class EFormDefImport {
		///<summary>Imports the files: reads and validates each one, inserts the eForm definition and its fields, and logs it. Refreshes the eForm
		///definition caches when anything was imported. Never throws for a bad file.</summary>
		public static EFormDefImportResult ImportFiles(List<string> listFilePaths) {
			EFormDefImportResult result=new EFormDefImportResult();
			List<string> listFilePathsSorted=SortByFileName(listFilePaths);
			List<string> listDescriptionsTaken=EFormDefs.GetDeepCopy().Select(x => x.Description??"").ToList();
			for(int i=0;i<listFilePathsSorted.Count;i++) {
				string fileName=Path.GetFileName(listFilePathsSorted[i]);
				EFormDef eFormDef=ReadFile(listFilePathsSorted[i],out string error);
				if(eFormDef==null) {
					result.ListFailures.Add(fileName+": "+error);
					continue;
				}
				eFormDef.Description=GetUniqueDescription(eFormDef.Description,listDescriptionsTaken);
				listDescriptionsTaken.Add(eFormDef.Description);
				try {
					Insert(eFormDef);
				}
				catch(Exception ex) {
					result.ListFailures.Add(fileName+": "+ex.Message);
					continue;
				}
				SecurityLogs.MakeLogEntry(EnumPermType.EFormEdit,0,"eForm "+eFormDef.Description+" imported from file "+fileName);
				result.ListEFormDefsImported.Add(eFormDef);
			}
			if(result.ListEFormDefsImported.Count>0) {
				EFormDefs.RefreshCache();
				EFormFieldDefs.RefreshCache();
			}
			return result;
		}

		///<summary>The paths in file name order (ignoring case), then by full path.</summary>
		public static List<string> SortByFileName(List<string> listFilePaths) {
			return listFilePaths.Where(x => !string.IsNullOrWhiteSpace(x))
				.OrderBy(x => Path.GetFileName(x),StringComparer.OrdinalIgnoreCase)
				.ThenBy(x => x,StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		///<summary>Reads one exported file. Returns null and the reason when the file is missing, is not an exported eForm, or fails validation.</summary>
		public static EFormDef ReadFile(string filePath,out string error) {
			if(!File.Exists(filePath)) {
				error="file not found";
				return null;
			}
			string xml;
			try {
				xml=File.ReadAllText(filePath);
			}
			catch(Exception ex) {
				error="could not be read ("+ex.Message+")";
				return null;
			}
			return Deserialize(xml,out error);
		}

		///<summary>Reads the XML that FrmEFormDefs exports. Returns null and the reason when it is not an exported eForm or fails validation.</summary>
		public static EFormDef Deserialize(string xml,out string error) {
			EFormDef eFormDef;
			try {
				XmlSerializer xmlSerializer=new XmlSerializer(typeof(EFormDef));
				using StringReader stringReader=new StringReader(xml??"");
				using XmlReader xmlReader=XmlReader.Create(stringReader,new XmlReaderSettings() { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null });
				eFormDef=(EFormDef)xmlSerializer.Deserialize(xmlReader);
			}
			catch(Exception) {
				error="invalid file format";
				return null;
			}
			error=Validate(eFormDef);
			if(error!="") {
				return null;
			}
			return eFormDef;
		}

		///<summary>Empty when the definition can be imported. It needs a description, a known form type, and fields of known types (INFERRED: Open
		///Dental does not publish what its import checks).</summary>
		public static string Validate(EFormDef eFormDef) {
			if(eFormDef==null) {
				return "invalid file format";
			}
			if(string.IsNullOrWhiteSpace(eFormDef.Description)) {
				return "the eForm has no description";
			}
			if(!Enum.IsDefined(typeof(EnumEFormType),eFormDef.FormType)) {
				return "unknown form type "+(int)eFormDef.FormType;
			}
			if(eFormDef.ListEFormFieldDefs==null) {
				eFormDef.ListEFormFieldDefs=new List<EFormFieldDef>();
			}
			for(int i=0;i<eFormDef.ListEFormFieldDefs.Count;i++) {
				if(eFormDef.ListEFormFieldDefs[i]==null) {
					return "field "+(i+1)+" is empty";
				}
				if(!Enum.IsDefined(typeof(EnumEFormFieldType),eFormDef.ListEFormFieldDefs[i].FieldType)) {
					return "field "+(i+1)+" has an unknown type "+(int)eFormDef.ListEFormFieldDefs[i].FieldType;
				}
			}
			return "";
		}

		///<summary>The description itself when no other eForm uses it (ignoring case); otherwise the description with " (2)", " (3)" and so on,
		///whichever is free first.</summary>
		public static string GetUniqueDescription(string description,List<string> listDescriptionsTaken) {
			description=description.Trim();
			if(!listDescriptionsTaken.Exists(x => string.Equals(x,description,StringComparison.OrdinalIgnoreCase))) {
				return description;
			}
			for(int n=2;;n++) {
				string descriptionNumbered=description+" ("+n+")";
				if(!listDescriptionsTaken.Exists(x => string.Equals(x,descriptionNumbered,StringComparison.OrdinalIgnoreCase))) {
					return descriptionNumbered;
				}
			}
		}

		///<summary>Inserts the definition and its fields as new rows, the way the single import always did.</summary>
		private static void Insert(EFormDef eFormDef) {
			eFormDef.DateTCreated=DateTime.Now;
			EFormDefs.Insert(eFormDef);
			for(int i=0;i<eFormDef.ListEFormFieldDefs.Count;i++) {
				eFormDef.ListEFormFieldDefs[i].EFormDefNum=eFormDef.EFormDefNum;
				eFormDef.ListEFormFieldDefs[i].ItemOrder=i;
				EFormFieldDefs.Insert(eFormDef.ListEFormFieldDefs[i]);//ignores any existing PK when inserting
			}
		}
	}

	///<summary>What a multiple import did.</summary>
	public class EFormDefImportResult {
		///<summary>The definitions that were inserted, in import order.</summary>
		public List<EFormDef> ListEFormDefsImported=new List<EFormDef>();
		///<summary>One line per file that was skipped: the file name and why.</summary>
		public List<string> ListFailures=new List<string>();

		///<summary>The message shown at the end: imported and failed counts, then the failed files.</summary>
		public string GetSummary() {
			string summary=Lans.g("EForms","Imported")+": "+ListEFormDefsImported.Count+"\r\n"+Lans.g("EForms","Failed")+": "+ListFailures.Count;
			if(ListFailures.Count>0) {
				summary+="\r\n\r\n"+string.Join("\r\n",ListFailures);
			}
			return summary;
		}
	}
}
