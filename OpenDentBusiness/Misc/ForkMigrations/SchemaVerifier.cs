using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenDentBusiness.ForkMigrations {
	///<summary>Compares a database schema with the Open Dental schema it must match exactly. The reference is a snapshot of Open Dental 25.3.48
	///(Reference/OdSchema-25.3.48.sql, embedded in this assembly), built from the schema-only dump of the office database. Tables, columns (type,
	///NULL, default, extra, charset, position), keys, engine and charset must match, except for the documented exceptions in
	///DocumentedExceptions25_3_48, each of which says why it is allowed.</summary>
	public static class SchemaVerifier {
		///<summary>The Open Dental version the embedded reference describes.</summary>
		public const string ReferenceVersion="25.3.48";
		///<summary>Name of the embedded reference snapshot (its LogicalName in OpenDentBusiness.csproj).</summary>
		public const string ReferenceResourceName="OdSchema-25.3.48.sql";

		///<summary>The office-specific generated column in patient that is not Open Dental schema. Adoption drops it with its unique key.</summary>
		public const string PortalActiveEmailColumn="_portal_active_email";
		///<summary>The unique key on patient._portal_active_email.</summary>
		public const string PortalActiveEmailIndex="portal_active_email_uniq";

		///<summary>The 54 tables that our 24.3 code declares but that exist only in Open Dental's own HQ database. An office database never has them
		///and Open Dental 25.3 does not either (docs/specs/schema-changes-24.3-to-26.1.md, "HQ-internal tables"). A database made by the 24.3
		///convert scripts can have a few of them.</summary>
		public static readonly string[] ArrayHqOnlyTables=new string[] {
			"address","apibilling","bugsubmission","bugsubmissionhash","chatuser","child","childparent","childparentlink","childroom","childroomlog",
			"cloudbilling","commloghist","ehrcode","eservicebilling","eservicecodelink","faq","job","jobactivelink","jobcontrol","joblink","joblog",
			"jobnote","jobnotification","jobpermission","jobproject","jobquote","jobreview","jobsprint","jobsprintlink","jobteam","jobteamuser",
			"maparea","mapareacontainer","phone","phonecomp","phoneconf","phoneempdefault","phoneempsubgroup","phonegraph","phonemetric",
			"plannedappt","reseller","resellerservice","sitelink","smsbilling","tasktaken","triagemetric","voicemail","webchataiassistant",
			"webchatmessage","webchatnote","webchatpref","webchatsession","webchatsurvey",
		};

		///<summary>Tables from very old Open Dental versions that databases created long ago still carry (our development databases start from a
		///21.1 seed dump that has them). No code reads them, Open Dental 25.3.48 does not have them, and they are left alone because they may hold
		///data.</summary>
		public static readonly string[] ArrayLegacyTables=new string[] {
			"anestheticdata","anestheticrecord","anesthmedsgiven","anesthmedsintake","anesthmedsinventory","anesthmedsinventoryadj",
			"anesthmedsuppliers","anesthscore","anesthvsdata","files","instructor",
		};

		private static SchemaSnapshot _reference25_3_48;
		private static readonly object _lockReference=new object();

		///<summary>The documented differences that do not count as a mismatch against Open Dental 25.3.48.</summary>
		public static List<SchemaException> DocumentedExceptions25_3_48 {
			get {
				List<SchemaException> listExceptions=new List<SchemaException>();
				listExceptions.Add(new SchemaException(null,"patient",PortalActiveEmailColumn,
					"Office-specific customization in the office database, not Open Dental schema (a generated column). The adoption step drops it."));
				listExceptions.Add(new SchemaException(null,"patient",PortalActiveEmailIndex,
					"Unique key on the office-specific patient._portal_active_email column. The adoption step drops it with the column."));
				foreach(string table in ArrayHqOnlyTables) {
					listExceptions.Add(new SchemaException(SchemaDifferenceKind.ExtraTable,table,null,
						"Open Dental HQ-only table declared by the 24.3 code. Never part of an office database; ignored when present."));
				}
				foreach(string table in ArrayLegacyTables) {
					listExceptions.Add(new SchemaException(SchemaDifferenceKind.ExtraTable,table,null,
						"Legacy table from old Open Dental versions that some long-lived databases still carry. No code uses it; left in place."));
				}
				return listExceptions;
			}
		}

		///<summary>The Open Dental 25.3.48 reference snapshot embedded in this assembly. Cached.</summary>
		public static SchemaSnapshot GetReference25_3_48() {
			lock(_lockReference) {
				if(_reference25_3_48==null) {
					_reference25_3_48=SchemaSnapshot.Parse(ReadReferenceDdl());
				}
				return _reference25_3_48;
			}
		}

		///<summary>The text of the embedded reference snapshot.</summary>
		public static string ReadReferenceDdl() {
			Assembly assembly=typeof(SchemaVerifier).Assembly;
			using(Stream stream=assembly.GetManifestResourceStream(ReferenceResourceName)) {
				if(stream==null) {
					throw new ApplicationException("The reference schema "+ReferenceResourceName+" is not embedded in "+assembly.GetName().Name+".");
				}
				using(StreamReader reader=new StreamReader(stream,Encoding.UTF8)) {
					return reader.ReadToEnd();
				}
			}
		}

		///<summary>Builds the reference snapshot from the schema-only dump of an Open Dental 25.3.48 database (fox-db-schema.sql). Removes what is
		///not Open Dental schema (patient._portal_active_email and its key) and writes the result with a header. This is how
		///Reference/OdSchema-25.3.48.sql was made; a test checks that the committed file still equals this output when the dump is available.</summary>
		public static string BuildReferenceDdlFromDump(string dumpText) {
			SchemaSnapshot snapshot=SchemaSnapshot.Parse(dumpText);
			SchemaTable tablePatient=snapshot.GetTable("patient");
			if(tablePatient!=null) {
				tablePatient.Columns.RemoveAll(x => x.Name==PortalActiveEmailColumn);
				tablePatient.Indexes.RemoveAll(x => x.Name==PortalActiveEmailIndex);
				for(int i=0;i<tablePatient.Columns.Count;i++) {
					tablePatient.Columns[i].Position=i+1;
				}
			}
			string header="Open Dental "+ReferenceVersion+" database schema: the reference for SchemaVerifier (Open PMS - Dental fork).\n"
				+"Generated by SchemaVerifier.BuildReferenceDdlFromDump from a schema-only dump of an Open Dental "+ReferenceVersion+" database\n"
				+"(no rows). Normalized: one CREATE TABLE per table, sorted by name, AUTO_INCREMENT values removed. Removed because it is not Open\n"
				+"Dental schema: the office-specific column patient."+PortalActiveEmailColumn+" and its key "+PortalActiveEmailIndex+".\n"
				+"Do not edit by hand; regenerate it.";
			return snapshot.ToDdl(header);
		}

		///<summary>Compares the connected database with the Open Dental 25.3.48 reference, allowing the documented exceptions.</summary>
		public static SchemaVerifyResult VerifyDatabase(string databaseName=null) {
			return Compare(GetReference25_3_48(),SchemaSnapshot.FromDatabase(databaseName),DocumentedExceptions25_3_48);
		}

		///<summary>Lists every difference between expected and actual. A difference that matches an exception is kept in the result with the reason,
		///but does not make IsMatch false.</summary>
		public static SchemaVerifyResult Compare(SchemaSnapshot expected,SchemaSnapshot actual,IEnumerable<SchemaException> exceptions=null) {
			List<SchemaException> listExceptions=(exceptions??new List<SchemaException>()).ToList();
			SchemaVerifyResult result=new SchemaVerifyResult();
			foreach(SchemaTable tableExpected in expected.Tables) {
				SchemaTable tableActual=actual.GetTable(tableExpected.Name);
				if(tableActual==null) {
					result.Add(SchemaDifferenceKind.MissingTable,tableExpected.Name,null,"table exists","missing",listExceptions);
					continue;
				}
				CompareTables(tableExpected,tableActual,result,listExceptions);
			}
			foreach(SchemaTable tableActual in actual.Tables) {
				if(!expected.HasTable(tableActual.Name)) {
					result.Add(SchemaDifferenceKind.ExtraTable,tableActual.Name,null,"no table","table exists",listExceptions);
				}
			}
			result.CountTablesExpected=expected.Tables.Count;
			result.CountTablesActual=actual.Tables.Count;
			return result;
		}

		private static void CompareTables(SchemaTable expected,SchemaTable actual,SchemaVerifyResult result,List<SchemaException> listExceptions) {
			string table=expected.Name;
			if(!string.Equals(expected.Engine,actual.Engine,StringComparison.OrdinalIgnoreCase)) {
				result.Add(SchemaDifferenceKind.TableOptions,table,"ENGINE",expected.Engine,actual.Engine,listExceptions);
			}
			if(expected.Charset!=actual.Charset) {
				result.Add(SchemaDifferenceKind.TableOptions,table,"CHARSET",expected.Charset,actual.Charset,listExceptions);
			}
			if(expected.Collation!=actual.Collation) {
				result.Add(SchemaDifferenceKind.TableOptions,table,"COLLATE",expected.Collation,actual.Collation,listExceptions);
			}
			if(expected.OtherOptions!=actual.OtherOptions) {
				result.Add(SchemaDifferenceKind.TableOptions,table,"options",expected.OtherOptions,actual.OtherOptions,listExceptions);
			}
			//Columns
			foreach(SchemaColumn columnExpected in expected.Columns) {
				SchemaColumn columnActual=actual.GetColumn(columnExpected.Name);
				if(columnActual==null) {
					result.Add(SchemaDifferenceKind.MissingColumn,table,columnExpected.Name,columnExpected.Definition,"missing",listExceptions);
					continue;
				}
				if(columnActual.Name!=columnExpected.Name) {
					result.Add(SchemaDifferenceKind.ColumnDefinition,table,columnExpected.Name,"name "+columnExpected.Name,"name "+columnActual.Name,
						listExceptions);
				}
				if(columnActual.Definition!=columnExpected.Definition) {
					result.Add(SchemaDifferenceKind.ColumnDefinition,table,columnExpected.Name,columnExpected.Definition,columnActual.Definition,
						listExceptions);
				}
			}
			foreach(SchemaColumn columnActual in actual.Columns) {
				if(expected.GetColumn(columnActual.Name)==null) {
					result.Add(SchemaDifferenceKind.ExtraColumn,table,columnActual.Name,"no column",columnActual.Definition,listExceptions);
				}
			}
			//Column order, over the columns both sides have, so a missing or extra column is reported once and not again as an order difference.
			List<string> listOrderExpected=expected.Columns.Where(x => actual.GetColumn(x.Name)!=null).Select(x => x.Name.ToLowerInvariant()).ToList();
			List<string> listOrderActual=actual.Columns.Where(x => expected.GetColumn(x.Name)!=null).Select(x => x.Name.ToLowerInvariant()).ToList();
			if(!listOrderExpected.SequenceEqual(listOrderActual)) {
				result.Add(SchemaDifferenceKind.ColumnOrder,table,null,string.Join(",",listOrderExpected),string.Join(",",listOrderActual),
					listExceptions);
			}
			//Keys
			foreach(SchemaIndex indexExpected in expected.Indexes) {
				SchemaIndex indexActual=actual.GetIndex(indexExpected.Name);
				if(indexActual==null) {
					result.Add(SchemaDifferenceKind.MissingIndex,table,indexExpected.Name,indexExpected.Definition,"missing",listExceptions);
					continue;
				}
				if(indexActual.Name!=indexExpected.Name || indexActual.Definition!=indexExpected.Definition) {
					result.Add(SchemaDifferenceKind.IndexDefinition,table,indexExpected.Name,indexExpected.ToDdl(),indexActual.ToDdl(),listExceptions);
				}
			}
			foreach(SchemaIndex indexActual in actual.Indexes) {
				if(expected.GetIndex(indexActual.Name)==null) {
					result.Add(SchemaDifferenceKind.ExtraIndex,table,indexActual.Name,"no key",indexActual.Definition,listExceptions);
				}
			}
			string otherExpected=string.Join("\n",expected.OtherDefinitions.OrderBy(x => x,StringComparer.Ordinal));
			string otherActual=string.Join("\n",actual.OtherDefinitions.OrderBy(x => x,StringComparer.Ordinal));
			if(otherExpected!=otherActual) {
				result.Add(SchemaDifferenceKind.TableOptions,table,"constraints",otherExpected,otherActual,listExceptions);
			}
		}
	}

	public enum SchemaDifferenceKind {
		MissingTable,
		ExtraTable,
		TableOptions,
		MissingColumn,
		ExtraColumn,
		ColumnDefinition,
		ColumnOrder,
		MissingIndex,
		ExtraIndex,
		IndexDefinition,
	}

	///<summary>A documented, allowed difference. Matches differences on Table (and Item, a column or key name, when given) of the given kind, or of any
	///kind when Kind is null.</summary>
	public class SchemaException {
		public SchemaDifferenceKind? Kind;
		public string Table;
		public string Item;
		public string Reason;

		public SchemaException(SchemaDifferenceKind? kind,string table,string item,string reason) {
			Kind=kind;
			Table=table;
			Item=item;
			Reason=reason;
		}

		public bool Matches(SchemaDifference difference) {
			if(Kind!=null && Kind.Value!=difference.Kind) {
				return false;
			}
			if(!string.Equals(Table,difference.Table,StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			if(Item==null) {
				return true;
			}
			return string.Equals(Item,difference.Item,StringComparison.OrdinalIgnoreCase);
		}
	}

	///<summary>One difference between the expected and the actual schema.</summary>
	public class SchemaDifference {
		public SchemaDifferenceKind Kind;
		public string Table;
		///<summary>Column or key name, or the table option. Null for table-level differences.</summary>
		public string Item;
		public string Expected;
		public string Actual;
		///<summary>Set when a documented exception allows this difference.</summary>
		public string ExceptionReason;

		public bool IsExcepted {
			get {
				return ExceptionReason!=null;
			}
		}

		public override string ToString() {
			string text=Kind+" "+Table+(Item==null ? "" : "."+Item)+": expected ["+Expected+"], actual ["+Actual+"]";
			if(IsExcepted) {
				text+=" (allowed: "+ExceptionReason+")";
			}
			return text;
		}
	}

	///<summary>The outcome of SchemaVerifier.Compare.</summary>
	public class SchemaVerifyResult {
		public List<SchemaDifference> ListDifferences=new List<SchemaDifference>();
		public int CountTablesExpected;
		public int CountTablesActual;

		///<summary>True when every difference is a documented exception.</summary>
		public bool IsMatch {
			get {
				return ListDifferences.All(x => x.IsExcepted);
			}
		}

		public List<SchemaDifference> ListMismatches {
			get {
				return ListDifferences.Where(x => !x.IsExcepted).ToList();
			}
		}

		public List<SchemaDifference> ListAllowed {
			get {
				return ListDifferences.Where(x => x.IsExcepted).ToList();
			}
		}

		internal void Add(SchemaDifferenceKind kind,string table,string item,string expected,string actual,List<SchemaException> listExceptions) {
			SchemaDifference difference=new SchemaDifference {
				Kind=kind,
				Table=table,
				Item=item,
				Expected=expected,
				Actual=actual,
			};
			SchemaException exception=listExceptions.FirstOrDefault(x => x.Matches(difference));
			if(exception!=null) {
				difference.ExceptionReason=exception.Reason;
			}
			ListDifferences.Add(difference);
		}

		///<summary>A readable report: mismatches first, then allowed differences.</summary>
		public string GetReport() {
			StringBuilder stringBuilder=new StringBuilder();
			List<SchemaDifference> listMismatches=ListMismatches;
			stringBuilder.AppendLine(IsMatch ? "Schema matches." : "Schema does not match: "+listMismatches.Count+" difference(s).");
			foreach(SchemaDifference difference in listMismatches) {
				stringBuilder.AppendLine("  "+difference.ToString());
			}
			List<SchemaDifference> listAllowed=ListAllowed;
			if(listAllowed.Count>0) {
				stringBuilder.AppendLine("Allowed differences: "+listAllowed.Count);
				foreach(SchemaDifference difference in listAllowed) {
					stringBuilder.AppendLine("  "+difference.ToString());
				}
			}
			return stringBuilder.ToString();
		}
	}
}
