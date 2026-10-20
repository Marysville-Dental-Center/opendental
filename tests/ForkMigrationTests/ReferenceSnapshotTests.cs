// Tests of the embedded Open Dental 25.3.48 reference schema and of how it lines up with the table classes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace ForkMigrationTests {
	[TestClass]
	public class ReferenceSnapshotTests {
		///<summary>The schema-only dump of the office's Open Dental 25.3.48 database. It is not in the repository; the tests that need it are skipped
		///where it is missing. Override with FORKMIGRATION_REFERENCE_DUMP.</summary>
		private static string DumpPath {
			get {
				string path=Environment.GetEnvironmentVariable("FORKMIGRATION_REFERENCE_DUMP");
				if(string.IsNullOrEmpty(path)) {
					path=@"D:\Dev\opendental-schema\fox-db-schema.sql";
				}
				return path;
			}
		}

		[TestMethod]
		public void Reference_Loads422TablesWithoutOfficeCustomization() {
			SchemaSnapshot reference=SchemaVerifier.GetReference25_3_48();
			Assert.AreEqual(422,reference.Tables.Count,"Open Dental 25.3.48 has 422 tables.");
			SchemaTable patient=reference.GetTable("patient");
			Assert.IsNotNull(patient);
			Assert.IsNull(patient.GetColumn(SchemaVerifier.PortalActiveEmailColumn),"The office-specific column is not Open Dental schema.");
			Assert.IsNull(patient.GetIndex(SchemaVerifier.PortalActiveEmailIndex));
			Assert.IsTrue(reference.Tables.All(x => x.Engine=="MyISAM"),"Every Open Dental table is MyISAM.");
			string ddl=SchemaVerifier.ReadReferenceDdl();
			StringAssert.DoesNotMatch(ddl,new System.Text.RegularExpressions.Regex("AUTO_INCREMENT=[0-9]"),"No row counters in the reference.");
		}

		[TestMethod]
		public void Reference_HasTheNewTablesAndKeepsClaimPaymentNoteAsVarchar() {
			SchemaSnapshot reference=SchemaVerifier.GetReference25_3_48();
			foreach(string table in new[] { "feeschednote","queryfilter","branding","inspending","chat","chatmsg","chatattach","chatreaction",
				"chatuserattach","chatuserod","sequencecounter","schoolapproval","schoolcoursedef","schoolcoursesched","deletedobject" })
			{
				Assert.IsTrue(reference.HasTable(table),table);
			}
			Assert.AreEqual("varchar(255) DEFAULT ''",reference.GetTable("claimpayment").GetColumn("Note").Definition,
				"25.3.48 still has varchar(255); the 26.1 step widens it.");
			Assert.AreEqual("KEY (`Category`)",reference.GetTable("task").GetIndex("TriageCategory").Definition,
				"The renamed task column keeps its old key name.");
			foreach(string table in SchemaVerifier.ArrayHqOnlyTables.Concat(SchemaVerifier.ArrayLegacyTables)) {
				Assert.IsFalse(reference.HasTable(table),table+" is not in Open Dental 25.3.48.");
			}
		}

		///<summary>The committed reference must be exactly what BuildReferenceDdlFromDump makes from the dump. Set FORKMIGRATION_WRITE_REFERENCE=1 to
		///rewrite the committed file from the dump instead.</summary>
		[TestMethod]
		public void Reference_EqualsSnapshotBuiltFromDump() {
			if(!File.Exists(DumpPath)) {
				Assert.Inconclusive("The schema-only dump is not on this computer: "+DumpPath);
			}
			string built=SchemaVerifier.BuildReferenceDdlFromDump(File.ReadAllText(DumpPath,Encoding.UTF8));
			string pathReference=TestDb.FindRepoFile(Path.Combine("OpenDentBusiness","Misc","ForkMigrations","Reference","OdSchema-25.3.48.sql"));
			if(Environment.GetEnvironmentVariable("FORKMIGRATION_WRITE_REFERENCE")=="1") {
				File.WriteAllText(pathReference,built,new UTF8Encoding(false));
				Assert.Inconclusive("Rewrote "+pathReference+". Rebuild so the new file is embedded.");
			}
			//Line endings depend on the checkout (core.autocrlf), so they are not compared.
			Assert.AreEqual(built,NormalizeLineEndings(File.ReadAllText(pathReference,Encoding.UTF8)),"The committed reference differs from the dump. Regenerate it.");
			Assert.AreEqual(built,NormalizeLineEndings(SchemaVerifier.ReadReferenceDdl()),"The embedded reference is stale. Rebuild OpenDentBusiness.");
		}

		private static string NormalizeLineEndings(string text) {
			return text.Replace("\r\n","\n");
		}

		///<summary>The dump with the office customization put back must equal the reference except for exactly that column and key.</summary>
		[TestMethod]
		public void Dump_DiffersFromReferenceOnlyByOfficeCustomization() {
			if(!File.Exists(DumpPath)) {
				Assert.Inconclusive("The schema-only dump is not on this computer: "+DumpPath);
			}
			SchemaSnapshot dump=SchemaSnapshot.Parse(File.ReadAllText(DumpPath,Encoding.UTF8));
			SchemaVerifyResult result=SchemaVerifier.Compare(SchemaVerifier.GetReference25_3_48(),dump,SchemaVerifier.DocumentedExceptions25_3_48);
			Assert.IsTrue(result.IsMatch,result.GetReport());
			CollectionAssert.AreEquivalent(new[] { "patient."+SchemaVerifier.PortalActiveEmailColumn,"patient."+SchemaVerifier.PortalActiveEmailIndex },
				result.ListDifferences.Select(x => x.Table+"."+x.Item).ToList(),result.GetReport());
		}

		///<summary>Every table class (except the HQ-only ones) has a table in Open Dental 25.3.48, and every database field of the class is a column of
		///it. This is what lets the program read and write an Open Dental 25.3.48 database.</summary>
		[TestMethod]
		public void TableClasses_MapToReferenceColumns() {
			SchemaSnapshot reference=SchemaVerifier.GetReference25_3_48();
			List<string> listProblems=new List<string>();
			foreach(Type type in GetTableTypes()) {
				string tableName=CrudTableAttribute.GetTableName(type).ToLowerInvariant();
				SchemaTable table=reference.GetTable(tableName);
				if(table==null) {
					if(!SchemaVerifier.ArrayHqOnlyTables.Contains(tableName)) {
						listProblems.Add("No table "+tableName+" for "+type.Name);
					}
					continue;
				}
				foreach(FieldInfo field in GetDbFields(type)) {
					if(table.GetColumn(field.Name)==null) {
						listProblems.Add("No column "+tableName+"."+field.Name+" for "+type.Name+"."+field.Name);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		///<summary>For the tables this package added or changed, every reference column is mapped by the class, so nothing written by Open Dental is
		///lost on update. The two chat attachment blobs are the documented exception (no binary column type in the CRUD generator; chat is
		///excluded).</summary>
		[TestMethod]
		public void ChangedTables_ClassesMapEveryReferenceColumn() {
			SchemaSnapshot reference=SchemaVerifier.GetReference25_3_48();
			Dictionary<string,Type> dictTypes=GetTableTypes().ToDictionary(x => CrudTableAttribute.GetTableName(x).ToLowerInvariant(),x => x);
			List<string> listProblems=new List<string>();
			foreach(string tableName in ChangedTables) {
				Type type=dictTypes[tableName];
				HashSet<string> setFields=new HashSet<string>(GetDbFields(type).Select(x => x.Name),StringComparer.OrdinalIgnoreCase);
				foreach(SchemaColumn column in reference.GetTable(tableName).Columns) {
					if(tableName=="chatattach" && (column.Name=="Thumbnail" || column.Name=="FileData")) {
						continue;
					}
					if(!setFields.Contains(column.Name)) {
						listProblems.Add(type.Name+" does not map "+tableName+"."+column.Name);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		///<summary>Tables whose class this package added or changed.</summary>
		public static readonly string[] ChangedTables=new string[] {
			"appointment","apptview","asapcomm","benefit","branding","chat","chatattach","chatmsg","chatreaction","chatuserattach","chatuserod",
			"claimpayment","clearinghouse","codegroup","computerpref","creditcard","document","eclipboardsheetdef","eformfield","eformfielddef",
			"emailmessage","eobattach","fee","feeschednote","guardian","histappointment","imagedraw","inspending","insplan","inssub","insverify",
			"insverifyhist","journalentry","medication","payplan","payplancharge","payplantemplate","queryfilter","reqneeded","reqstudent",
			"schoolapproval","schoolcourse","schoolcoursedef","schoolcoursesched","sequencecounter","sheetfield","statement","task","taskhist",
			"transaction",
		};

		public static IEnumerable<Type> GetTableTypes() {
			Type typeTableBase=typeof(TableBase);
			return typeTableBase.Assembly.GetTypes()
				.Where(x => x.IsSubclassOf(typeTableBase) && !x.IsAbstract && !x.Name.StartsWith("WebForms_"))
				.OrderBy(x => x.Name);
		}

		public static IEnumerable<FieldInfo> GetDbFields(Type type) {
			foreach(FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
				CrudColumnAttribute crudColumn=field.GetCustomAttributes(typeof(CrudColumnAttribute),true).OfType<CrudColumnAttribute>().FirstOrDefault();
				if(crudColumn!=null && crudColumn.IsNotDbColumn) {
					continue;
				}
				yield return field;
			}
		}
	}
}
