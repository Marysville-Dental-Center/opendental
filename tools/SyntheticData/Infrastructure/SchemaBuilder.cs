// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SyntheticData.Infrastructure {
	///<summary>Creates an empty copy of the Open Dental 25.3.48 schema: either from the schema-only dump (fox-db-schema.sql, the default)
	///or by copying the table definitions of the local reference database od_253_schema. Table AUTO_INCREMENT counters are reset.</summary>
	public static class SchemaBuilder {
		public const string DefaultSchemaSqlPath=@"D:\Dev\opendental-schema\fox-db-schema.sql";
		public const string DefaultReferenceDatabase="od_253_schema";
		///<summary>Number of tables in the Open Dental 25.3.48 schema.</summary>
		public const int ExpectedTableCount=422;

		private static readonly Regex _regexAutoIncrement=new Regex(@"\s+AUTO_INCREMENT=\d+",RegexOptions.Compiled);

		///<summary>Drops (with force) and creates the target database, then creates every table. Returns where the definitions came from.</summary>
		public static string Create(Db db,string database,bool force,string schemaSqlPath,string referenceDatabase) {
			LocalOnly.EnsureWritableDatabaseName(database);
			if(db.DatabaseExists(database)) {
				if(!force) {
					throw new RefusedException("Database "+database+" already exists. Use --force to drop and recreate it.");
				}
				db.Execute("DROP DATABASE `"+database+"`");
			}
			List<string> listStatements;
			string source;
			if(!string.IsNullOrEmpty(schemaSqlPath) && File.Exists(schemaSqlPath)) {
				listStatements=ReadDumpStatements(File.ReadAllText(schemaSqlPath,Encoding.UTF8));
				source=schemaSqlPath;
			}
			else if(!string.IsNullOrEmpty(referenceDatabase) && db.DatabaseExists(referenceDatabase)) {
				listStatements=ReadReferenceStatements(db,referenceDatabase);
				source="database "+referenceDatabase;
			}
			else {
				throw new RefusedException("No schema source: neither the file '"+schemaSqlPath+"' nor the local database '"+referenceDatabase+"' exists.");
			}
			db.Execute("CREATE DATABASE `"+database+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			db.Use(database);
			foreach(string statement in listStatements) {
				db.Execute(statement);
			}
			return source;
		}

		///<summary>The DROP TABLE and CREATE TABLE statements of a mysqldump/mariadb-dump file, with AUTO_INCREMENT counters removed.
		///Version-conditional comments (SET statements) and the sandbox-mode line are skipped.</summary>
		public static List<string> ReadDumpStatements(string dumpText) {
			List<string> listStatements=new List<string>();
			StringBuilder current=new StringBuilder();
			foreach(string rawLine in dumpText.Replace("\r\n","\n").Split('\n')) {
				string line=rawLine.TrimEnd();
				if(current.Length==0) {
					if(line.Length==0 || line.StartsWith("--") || line.StartsWith("/*")) {
						continue;
					}
					if(!line.StartsWith("CREATE TABLE",StringComparison.OrdinalIgnoreCase)) {
						//DROP TABLE IF EXISTS and anything else outside a CREATE TABLE is not needed in a new, empty database.
						continue;
					}
				}
				current.Append(line).Append('\n');
				if(line.EndsWith(";")) {
					string statement=current.ToString().Trim();
					statement=statement.Substring(0,statement.Length-1);
					listStatements.Add(_regexAutoIncrement.Replace(statement,""));
					current.Clear();
				}
			}
			if(current.Length>0) {
				throw new InvalidDataException("The schema dump ends inside a CREATE TABLE statement.");
			}
			return listStatements;
		}

		private static List<string> ReadReferenceStatements(Db db,string referenceDatabase) {
			List<string> listStatements=new List<string>();
			List<object[]> listTables=db.Rows("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+referenceDatabase
				+"' AND TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME");
			foreach(object[] row in listTables) {
				string table=Convert.ToString(row[0]);
				List<object[]> listCreate=db.Rows("SHOW CREATE TABLE `"+referenceDatabase+"`.`"+table+"`");
				listStatements.Add(_regexAutoIncrement.Replace(Convert.ToString(listCreate[0][1]),""));
			}
			return listStatements;
		}
	}
}
