using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenDentBusiness.ForkMigrations {
	///<summary>A database schema as a set of tables, read from CREATE TABLE statements. The same parser reads a mysqldump file (the reference schema)
	///and the output of SHOW CREATE TABLE (a live database), so both sides of a comparison are normalized the same way. Only what decides schema
	///equality is kept: table options (engine, charset, collation, and any other option except AUTO_INCREMENT), columns in order with their full
	///definitions, and keys. Row counts and AUTO_INCREMENT values are dropped on purpose.</summary>
	public class SchemaSnapshot {
		private readonly Dictionary<string,SchemaTable> _dictTables=new Dictionary<string,SchemaTable>(StringComparer.OrdinalIgnoreCase);

		///<summary>Tables sorted by name.</summary>
		public List<SchemaTable> Tables {
			get {
				return _dictTables.Values.OrderBy(x => x.Name,StringComparer.OrdinalIgnoreCase).ToList();
			}
		}

		///<summary>Null if there is no such table. Table names are compared without case, as MariaDB does on Windows.</summary>
		public SchemaTable GetTable(string tableName) {
			SchemaTable table;
			_dictTables.TryGetValue(tableName,out table);
			return table;
		}

		public bool HasTable(string tableName) {
			return _dictTables.ContainsKey(tableName);
		}

		public void AddTable(SchemaTable table) {
			if(_dictTables.ContainsKey(table.Name)) {
				throw new ApplicationException("Table "+table.Name+" is defined twice.");
			}
			_dictTables.Add(table.Name,table);
		}

		public bool RemoveTable(string tableName) {
			return _dictTables.Remove(tableName);
		}

		///<summary>Reads every CREATE TABLE statement in the text: a mysqldump file, a file written by ToDdl, or SHOW CREATE TABLE output. Everything
		///outside CREATE TABLE statements (comments, SET, DROP, INSERT) is ignored.</summary>
		public static SchemaSnapshot Parse(string ddl) {
			SchemaSnapshot snapshot=new SchemaSnapshot();
			foreach(string statement in SplitCreateTableStatements(ddl)) {
				snapshot.AddTable(SchemaTable.Parse(statement));
			}
			return snapshot;
		}

		///<summary>Reads the schema of a database through the current connection: SHOW CREATE TABLE for every base table. databaseName defaults to
		///the connected database. Views are ignored.</summary>
		public static SchemaSnapshot FromDatabase(string databaseName=null) {
			if(string.IsNullOrEmpty(databaseName)) {
				databaseName=Db.GetScalar("SELECT DATABASE()");
			}
			SchemaSnapshot snapshot=new SchemaSnapshot();
			string command="SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+POut.String(databaseName)+"' "
				+"AND TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME";
			DataTable tableNames=Db.GetTable(command);
			foreach(DataRow row in tableNames.Rows) {
				snapshot.AddTable(SchemaTable.FromDatabase(databaseName,row[0].ToString()));
			}
			return snapshot;
		}

		///<summary>Writes the snapshot as CREATE TABLE statements, one per table, sorted by table name, without AUTO_INCREMENT values. Parse(ToDdl())
		///gives back an equal snapshot, and the text runs as is on MariaDB.</summary>
		public string ToDdl(string header=null) {
			StringBuilder stringBuilder=new StringBuilder();
			if(!string.IsNullOrEmpty(header)) {
				foreach(string line in header.Replace("\r\n","\n").Split('\n')) {
					stringBuilder.Append("-- "+line+"\n");
				}
				stringBuilder.Append("\n");
			}
			foreach(SchemaTable table in Tables) {
				stringBuilder.Append(table.ToDdl());
				stringBuilder.Append("\n\n");
			}
			return stringBuilder.ToString();
		}

		///<summary>Splits text into CREATE TABLE statements. A statement runs from a line starting with CREATE TABLE to the first following line that
		///starts with ")" (the table options line). Column definitions never start a line with ")", in mysqldump or in SHOW CREATE TABLE output.</summary>
		private static List<string> SplitCreateTableStatements(string ddl) {
			List<string> listStatements=new List<string>();
			string[] arrayLines=ddl.Replace("\r\n","\n").Split('\n');
			StringBuilder current=null;
			foreach(string line in arrayLines) {
				if(current==null) {
					if(line.StartsWith("CREATE TABLE ",StringComparison.OrdinalIgnoreCase)) {
						current=new StringBuilder();
						current.Append(line+"\n");
					}
					continue;
				}
				current.Append(line+"\n");
				if(line.StartsWith(")")) {
					listStatements.Add(current.ToString());
					current=null;
				}
			}
			if(current!=null) {
				throw new ApplicationException("Unterminated CREATE TABLE statement: "+current.ToString().Split('\n')[0]);
			}
			return listStatements;
		}
	}

	///<summary>One table of a SchemaSnapshot.</summary>
	public class SchemaTable {
		public string Name;
		///<summary>In table order.</summary>
		public List<SchemaColumn> Columns=new List<SchemaColumn>();
		///<summary>Primary key, unique, plain, fulltext and spatial keys, in the order the server lists them. Compared by name, not by order.</summary>
		public List<SchemaIndex> Indexes=new List<SchemaIndex>();
		///<summary>Lines that are neither columns nor keys, for example CONSTRAINT or CHECK lines. MyISAM tables have none.</summary>
		public List<string> OtherDefinitions=new List<string>();
		public string Engine="";
		public string Charset="";
		public string Collation="";
		///<summary>Table options other than ENGINE, CHARSET, COLLATE and AUTO_INCREMENT, normalized and sorted, for example "ROW_FORMAT=DYNAMIC".
		///Empty for nearly every table.</summary>
		public string OtherOptions="";

		public SchemaColumn GetColumn(string columnName) {
			return Columns.FirstOrDefault(x => string.Equals(x.Name,columnName,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>Index names are compared without case, as MariaDB does.</summary>
		public SchemaIndex GetIndex(string indexName) {
			return Indexes.FirstOrDefault(x => string.Equals(x.Name,indexName,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>Reads SHOW CREATE TABLE for one table. Returns null if the table does not exist.</summary>
		public static SchemaTable FromDatabase(string databaseName,string tableName) {
			string command="SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+POut.String(databaseName)+"' "
				+"AND TABLE_NAME='"+POut.String(tableName)+"'";
			if(Db.GetScalar(command)=="0") {
				return null;
			}
			DataTable table=Db.GetTable("SHOW CREATE TABLE `"+databaseName+"`.`"+tableName+"`");
			return Parse(table.Rows[0][1].ToString());
		}

		///<summary>Parses one CREATE TABLE statement.</summary>
		public static SchemaTable Parse(string statement) {
			string[] arrayLines=statement.Replace("\r\n","\n").Split('\n').Where(x => x.Trim()!="").ToArray();
			Match matchName=Regex.Match(arrayLines[0],@"^CREATE TABLE (?:IF NOT EXISTS )?`([^`]+)`\s*\(\s*$",RegexOptions.IgnoreCase);
			if(!matchName.Success) {
				throw new ApplicationException("Not a CREATE TABLE line: "+arrayLines[0]);
			}
			SchemaTable schemaTable=new SchemaTable();
			schemaTable.Name=matchName.Groups[1].Value;
			string lineOptions=null;
			for(int i=1;i<arrayLines.Length;i++) {
				string line=arrayLines[i].Trim();
				if(line.StartsWith(")")) {
					lineOptions=line;
					break;
				}
				if(line.EndsWith(",")) {
					line=line.Substring(0,line.Length-1).TrimEnd();
				}
				if(line.StartsWith("`")) {
					int indexEnd=line.IndexOf('`',1);
					SchemaColumn column=new SchemaColumn();
					column.Name=line.Substring(1,indexEnd-1);
					column.Definition=SchemaColumn.NormalizeDefinition(line.Substring(indexEnd+1).Trim());
					column.Position=schemaTable.Columns.Count+1;
					schemaTable.Columns.Add(column);
					continue;
				}
				SchemaIndex index=SchemaIndex.TryParse(line);
				if(index!=null) {
					schemaTable.Indexes.Add(index);
					continue;
				}
				schemaTable.OtherDefinitions.Add(line);
			}
			if(lineOptions==null) {
				throw new ApplicationException("CREATE TABLE `"+schemaTable.Name+"` has no closing line.");
			}
			schemaTable.ParseOptions(lineOptions);
			return schemaTable;
		}

		///<summary>Reads ") ENGINE=MyISAM AUTO_INCREMENT=15 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;" and the like.</summary>
		private void ParseOptions(string lineOptions) {
			string text=lineOptions.Trim().TrimStart(')').Trim().TrimEnd(';').Trim();
			List<string> listOther=new List<string>();
			//Options are KEY=VALUE pairs; a value may be quoted. "DEFAULT CHARSET" and "DEFAULT COLLATE" carry an optional DEFAULT word.
			MatchCollection matches=Regex.Matches(text,@"([A-Z_ ]+?)\s*=\s*('(?:[^'\\]|\\.|'')*'|\S+)",RegexOptions.IgnoreCase);
			foreach(Match match in matches) {
				string key=Regex.Replace(match.Groups[1].Value.Trim(),@"\s+"," ").ToUpperInvariant();
				if(key.StartsWith("DEFAULT ")) {
					key=key.Substring("DEFAULT ".Length);
				}
				string value=match.Groups[2].Value;
				switch(key) {
					case "ENGINE":
						Engine=value;
						break;
					case "CHARSET":
					case "CHARACTER SET":
						Charset=NormalizeCharset(value);
						break;
					case "COLLATE":
						Collation=NormalizeCollation(value);
						break;
					case "AUTO_INCREMENT":
						break;//Data, not schema.
					default:
						listOther.Add(key+"="+value);
						break;
				}
			}
			if(Collation=="" && Charset!="") {
				Collation=DefaultCollation(Charset);
			}
			OtherOptions=string.Join(" ",listOther.OrderBy(x => x,StringComparer.Ordinal));
		}

		///<summary>MariaDB 10.6 and later print utf8mb3 where older servers printed utf8.</summary>
		public static string NormalizeCharset(string charset) {
			if(string.Equals(charset,"utf8",StringComparison.OrdinalIgnoreCase)) {
				return "utf8mb3";
			}
			return charset.ToLowerInvariant();
		}

		public static string NormalizeCollation(string collation) {
			string lower=collation.ToLowerInvariant();
			if(lower.StartsWith("utf8_")) {
				return "utf8mb3_"+lower.Substring(5);
			}
			return lower;
		}

		///<summary>The server's default collation for a charset, used when a CREATE TABLE statement names only the charset.</summary>
		public static string DefaultCollation(string charset) {
			switch(charset) {
				case "utf8mb3":
					return "utf8mb3_general_ci";
				case "utf8mb4":
					return "utf8mb4_general_ci";
				case "latin1":
					return "latin1_swedish_ci";
				default:
					return charset+"_general_ci";
			}
		}

		///<summary>The table as a CREATE TABLE statement in the server's own format.</summary>
		public string ToDdl() {
			List<string> listLines=new List<string>();
			foreach(SchemaColumn column in Columns) {
				listLines.Add("  `"+column.Name+"` "+column.Definition);
			}
			foreach(SchemaIndex index in Indexes) {
				listLines.Add("  "+index.ToDdl());
			}
			foreach(string other in OtherDefinitions) {
				listLines.Add("  "+other);
			}
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("CREATE TABLE `"+Name+"` (\n");
			stringBuilder.Append(string.Join(",\n",listLines));
			stringBuilder.Append("\n) ENGINE="+Engine);
			if(Charset!="") {
				stringBuilder.Append(" DEFAULT CHARSET="+Charset);
			}
			if(Collation!="") {
				stringBuilder.Append(" COLLATE="+Collation);
			}
			if(OtherOptions!="") {
				stringBuilder.Append(" "+OtherOptions);
			}
			stringBuilder.Append(";");
			return stringBuilder.ToString();
		}
	}

	///<summary>One column of a SchemaTable.</summary>
	public class SchemaColumn {
		public string Name;
		///<summary>Everything after the column name, as the server prints it: type, charset and collation when they differ from the table's,
		///NULL or NOT NULL, default, ON UPDATE, AUTO_INCREMENT, generated expression, comment.</summary>
		public string Definition;
		///<summary>1-based position in the table.</summary>
		public int Position;

		///<summary>Removes formatting differences that do not change the column: repeated spaces, and the spellings of CURRENT_TIMESTAMP.</summary>
		public static string NormalizeDefinition(string definition) {
			string text=Regex.Replace(definition.Trim(),@"\s+"," ");
			text=Regex.Replace(text,@"\bCURRENT_TIMESTAMP(\(\))?",match => "current_timestamp()",RegexOptions.IgnoreCase);
			return text;
		}

		public override string ToString() {
			return "`"+Name+"` "+Definition;
		}
	}

	///<summary>One key of a SchemaTable.</summary>
	public class SchemaIndex {
		///<summary>PRIMARY for the primary key.</summary>
		public string Name;
		///<summary>PRIMARY KEY, UNIQUE KEY, KEY, FULLTEXT KEY or SPATIAL KEY.</summary>
		public string Kind;
		///<summary>The column list as printed, without the outer parentheses, for example "`ClaimIdentifier`(30)" or "`PatNum`,`DocCategory`".</summary>
		public string Columns;
		///<summary>Anything printed after the column list, for example "USING BTREE" or a COMMENT. Usually empty.</summary>
		public string Options;

		public bool IsPrimary {
			get {
				return Kind=="PRIMARY KEY";
			}
		}

		///<summary>Everything except the name, for comparing two keys with the same name.</summary>
		public string Definition {
			get {
				return Kind+" ("+Columns+")"+(Options=="" ? "" : " "+Options);
			}
		}

		///<summary>Returns null if the line is not a key.</summary>
		public static SchemaIndex TryParse(string line) {
			Match match=Regex.Match(line,@"^(PRIMARY KEY|UNIQUE KEY|UNIQUE INDEX|UNIQUE|KEY|INDEX|FULLTEXT KEY|FULLTEXT INDEX|FULLTEXT|SPATIAL KEY|SPATIAL INDEX|SPATIAL)"
				+@"\s*(?:`([^`]+)`\s*)?\((.*)\)\s*(.*)$",RegexOptions.IgnoreCase);
			if(!match.Success) {
				return null;
			}
			SchemaIndex index=new SchemaIndex();
			string kind=match.Groups[1].Value.ToUpperInvariant();
			if(kind.StartsWith("PRIMARY")) {
				index.Kind="PRIMARY KEY";
				index.Name="PRIMARY";
			}
			else {
				if(kind.StartsWith("UNIQUE")) {
					index.Kind="UNIQUE KEY";
				}
				else if(kind.StartsWith("FULLTEXT")) {
					index.Kind="FULLTEXT KEY";
				}
				else if(kind.StartsWith("SPATIAL")) {
					index.Kind="SPATIAL KEY";
				}
				else {
					index.Kind="KEY";
				}
				index.Name=match.Groups[2].Value;
			}
			index.Columns=Regex.Replace(match.Groups[3].Value.Trim(),@"\s*,\s*",",");
			index.Options=Regex.Replace(match.Groups[4].Value.Trim(),@"\s+"," ");
			return index;
		}

		public string ToDdl() {
			if(IsPrimary) {
				return "PRIMARY KEY ("+Columns+")"+(Options=="" ? "" : " "+Options);
			}
			return Kind+" `"+Name+"` ("+Columns+")"+(Options=="" ? "" : " "+Options);
		}

		public override string ToString() {
			return ToDdl();
		}
	}
}
