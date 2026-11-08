// Reading Open Dental's published 26.2 database documents (OpenDentalDocumentation26-2.xml, OpenDentalDiffDocumentation26-2.xml) as data, and
// comparing a schema with them the way M2-01 compared the 26.1 reference (docs/port-notes/M2-01.md). The documents are not in the repository:
// the checks run when P262_OD_DOCS_DIR (or M201_OD_DOCS_DIR) names a folder that holds them, and are inconclusive otherwise.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness.ForkMigrations;

namespace P262SchemaTests {
	public static class OdDocs {
		public const string FULL_26_2="OpenDentalDocumentation26-2.xml";
		public const string DIFF_26_2="OpenDentalDiffDocumentation26-2.xml";

		///<summary>The folder holding the published documents, or null.</summary>
		public static string GetDocsDir() {
			foreach(string variable in new[] { "P262_OD_DOCS_DIR","M201_OD_DOCS_DIR" }) {
				string dir=Environment.GetEnvironmentVariable(variable);
				if(!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir,FULL_26_2)) && File.Exists(Path.Combine(dir,DIFF_26_2))) {
					return dir;
				}
			}
			return null;
		}

		///<summary>Loads a published document, or marks the test inconclusive when the documents are not available.</summary>
		public static XDocument Load(string fileName) {
			string dir=GetDocsDir();
			if(dir==null) {
				Assert.Inconclusive("Set P262_OD_DOCS_DIR to a folder with "+FULL_26_2+" and "+DIFF_26_2+" (Open Dental's published database documents) to "
					+"run this check.");
			}
			return XDocument.Load(Path.Combine(dir,fileName));
		}

		///<summary>A column type the way Open Dental's documentation tool prints it (DocumentationBuilder/Form1.cs, GetXElementForColumn): unsigned
		///tinyint(3), tinyint(1), smallint(5) and mediumint(8) lose their width, other unsigned types lose " unsigned".</summary>
		public static string ToDocType(string definition) {
			Match match=Regex.Match(definition,@"^([a-z]+(?:\([^)]*\))?(?: unsigned)?)");
			string sqlType=match.Groups[1].Value;
			if(sqlType=="tinyint(3) unsigned" || sqlType=="tinyint(1) unsigned") {
				return "tinyint";
			}
			if(sqlType=="smallint(5) unsigned") {
				return "smallint";
			}
			if(sqlType=="mediumint(8) unsigned") {
				return "mediumint";
			}
			if(sqlType.EndsWith(" unsigned")) {
				return sqlType.Substring(0,sqlType.Length-" unsigned".Length);
			}
			return sqlType;
		}

		///<summary>Every difference between the full 26.2 document and the schema, over the tables the document lists: a table missing from the schema,
		///a different column count, name or type at a position, and every schema table the document does not list (setTablesIgnored aside, for
		///this program's own tables and the documented legacy and HQ-only exceptions). Empty when they match exactly.</summary>
		public static List<string> CompareWithFullDocument(XDocument doc,SchemaSnapshot snapshot,ISet<string> setTablesIgnored=null) {
			List<string> listProblems=new List<string>();
			HashSet<string> setDocTables=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach(XElement elementTable in doc.Root.Elements("table")) {
				string tableName=elementTable.Attribute("name").Value;
				setDocTables.Add(tableName);
				SchemaTable table=snapshot.GetTable(tableName);
				if(table==null) {
					listProblems.Add("No table "+tableName);
					continue;
				}
				List<XElement> listColumns=elementTable.Elements("column").OrderBy(x => int.Parse(x.Attribute("order").Value)).ToList();
				if(listColumns.Count!=table.Columns.Count) {
					listProblems.Add(tableName+": "+listColumns.Count+" columns published, "+table.Columns.Count+" in the schema");
					continue;
				}
				for(int i=0;i<listColumns.Count;i++) {
					string name=listColumns[i].Attribute("name").Value;
					string type=listColumns[i].Attribute("type").Value;
					if(!string.Equals(name,table.Columns[i].Name,StringComparison.Ordinal)) {
						listProblems.Add(tableName+" column "+(i+1)+": published "+name+", schema "+table.Columns[i].Name);
						continue;
					}
					if(type!=ToDocType(table.Columns[i].Definition)) {
						listProblems.Add(tableName+"."+name+": published "+type+", schema "+table.Columns[i].Definition);
					}
				}
			}
			foreach(SchemaTable table in snapshot.Tables) {
				if(!setDocTables.Contains(table.Name) && (setTablesIgnored==null || !setTablesIgnored.Contains(table.Name))) {
					listProblems.Add("Table "+table.Name+" is not in the published document");
				}
			}
			return listProblems;
		}

		///<summary>One enum value of the diff document: the enumeration as the document names it (trailing punctuation removed), the member, and its
		///number, read from the leading digits of the description, or null when the description has none.</summary>
		public class EnumValue {
			public string EnumName;
			public string Member;
			public int? Number;
			///<summary>The member's position in its Enumeration element, from 0.</summary>
			public int Ordinal;
		}

		///<summary>Every EnumValue of the document, once per enumeration and member (the diff lists AlertType and EnumPermType under two columns each).
		///</summary>
		public static List<EnumValue> GetEnumValues(XDocument doc,string elementName) {
			List<EnumValue> listValues=new List<EnumValue>();
			foreach(XElement elementEnum in doc.Root.Elements(elementName).Elements("column").Elements("Enumeration")) {
				string enumName=elementEnum.Attribute("name").Value.Trim().TrimEnd(',');
				int ordinal=0;
				foreach(XElement elementValue in elementEnum.Elements("EnumValue")) {
					string member=elementValue.Attribute("name").Value;
					Match match=Regex.Match(elementValue.Value.Trim(),@"^(-?\d+)");
					if(!listValues.Any(x => x.EnumName==enumName && x.Member==member)) {
						listValues.Add(new EnumValue { EnumName=enumName,Member=member,Number=match.Success ? int.Parse(match.Groups[1].Value) : (int?)null,
							Ordinal=ordinal });
					}
					ordinal++;
				}
			}
			return listValues;
		}

		///<summary>The names of the enumerations the document lists (trailing punctuation removed), including ones without values.</summary>
		public static List<string> GetEnumNames(XDocument doc,string elementName) {
			return doc.Root.Elements(elementName).Elements("column").Elements("Enumeration").Select(x => x.Attribute("name").Value.Trim().TrimEnd(','))
				.Distinct().ToList();
		}
	}
}
