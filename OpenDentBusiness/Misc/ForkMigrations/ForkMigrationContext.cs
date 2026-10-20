using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace OpenDentBusiness.ForkMigrations {
	///<summary>What a fork migration step uses to change the database. Every helper checks the current schema first (through SHOW CREATE TABLE,
	///parsed the same way the schema verifier parses it) and changes nothing when the database is already in the requested state, so steps built
	///from these helpers are idempotent. Each helper returns true when it changed something, which is how a step knows that it just created a column
	///and may run the data migration that belongs to it. Every change is logged in ListChanges.</summary>
	public class ForkMigrationContext {
		private readonly Dictionary<string,SchemaTable> _dictTableCache=new Dictionary<string,SchemaTable>(StringComparer.OrdinalIgnoreCase);

		///<summary>The connected database.</summary>
		public string DatabaseName { get; }
		///<summary>The step that is running, for the log.</summary>
		public ForkMigrationStep StepCur { get; internal set; }
		///<summary>Every statement that changed the schema or data, prefixed with the step level.</summary>
		public List<string> ListChanges { get; }=new List<string>();

		public ForkMigrationContext(string databaseName) {
			DatabaseName=databaseName;
		}

		#region Reading the schema
		///<summary>The table as SHOW CREATE TABLE describes it now, or null if it does not exist. Cached until the next change to that table.</summary>
		public SchemaTable GetTable(string tableName) {
			SchemaTable table;
			if(!_dictTableCache.TryGetValue(tableName,out table)) {
				table=SchemaTable.FromDatabase(DatabaseName,tableName);
				_dictTableCache[tableName]=table;
			}
			return table;
		}

		public bool TableExists(string tableName) {
			return GetTable(tableName)!=null;
		}

		public bool ColumnExists(string tableName,string columnName) {
			SchemaTable table=GetTable(tableName);
			return table!=null && table.GetColumn(columnName)!=null;
		}

		public bool IndexExists(string tableName,string indexName) {
			SchemaTable table=GetTable(tableName);
			return table!=null && table.GetIndex(indexName)!=null;
		}

		///<summary>A preference's ValueString, read straight from the table. Null if there is no such row.</summary>
		public string GetPrefRaw(string prefName) {
			DataTable table=Db.GetTable("SELECT ValueString FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
			if(table.Rows.Count==0) {
				return null;
			}
			return table.Rows[0][0].ToString();
		}
		#endregion Reading the schema

		#region Changing the schema
		///<summary>Runs a full CREATE TABLE statement if the table does not exist. The statement must name the table in backticks.</summary>
		public bool CreateTableIfMissing(string createTableStatement) {
			SchemaTable tableNew=SchemaTable.Parse(createTableStatement);
			if(TableExists(tableNew.Name)) {
				return false;
			}
			Execute(createTableStatement,tableNew.Name);
			return true;
		}

		///<summary>Adds a column if it is missing. definition is everything after the column name, written the way SHOW CREATE TABLE prints it, for
		///example "tinyint(4) NOT NULL". afterColumn puts it after that column (null adds it at the end). Returns true if the column was added.</summary>
		public bool AddColumnIfMissing(string tableName,string columnName,string definition,string afterColumn=null) {
			RequireTable(tableName);
			if(ColumnExists(tableName,columnName)) {
				return false;
			}
			string command="ALTER TABLE `"+tableName+"` ADD COLUMN `"+columnName+"` "+definition;
			if(afterColumn!=null) {
				command+=" AFTER `"+afterColumn+"`";
			}
			Execute(command,tableName);
			return true;
		}

		///<summary>Changes a column's definition when it differs from definition (written the way SHOW CREATE TABLE prints it). When the new definition is
		///NOT NULL and the column holds NULLs, sqlNullReplacement (an SQL literal such as '') is written into those rows first. Returns true if the column
		///changed. Throws if the column does not exist.</summary>
		public bool EnsureColumnDefinition(string tableName,string columnName,string definition,string sqlNullReplacement=null) {
			RequireTable(tableName);
			SchemaColumn column=GetTable(tableName).GetColumn(columnName);
			if(column==null) {
				throw new ApplicationException("Column "+tableName+"."+columnName+" does not exist.");
			}
			if(column.Definition==SchemaColumn.NormalizeDefinition(definition)) {
				return false;
			}
			if(sqlNullReplacement!=null) {
				ExecuteData("UPDATE `"+tableName+"` SET `"+columnName+"`="+sqlNullReplacement+" WHERE `"+columnName+"` IS NULL");
			}
			Execute("ALTER TABLE `"+tableName+"` MODIFY COLUMN `"+columnName+"` "+definition,tableName);
			return true;
		}

		///<summary>Renames a column (keeping its data and position) when the old name exists and the new one does not. Keys on the column keep their
		///names. Returns true if the column was renamed.</summary>
		public bool RenameColumnIfNeeded(string tableName,string columnNameOld,string columnNameNew,string definition) {
			RequireTable(tableName);
			SchemaTable table=GetTable(tableName);
			if(table.GetColumn(columnNameNew)!=null || table.GetColumn(columnNameOld)==null) {
				return false;
			}
			Execute("ALTER TABLE `"+tableName+"` CHANGE COLUMN `"+columnNameOld+"` `"+columnNameNew+"` "+definition,tableName);
			return true;
		}

		///<summary>Adds a key if no key of that name exists. columns is the column list as SHOW CREATE TABLE prints it, without the outer parentheses,
		///for example "`ClaimIdentifier`(30)". If a key of that name exists with other columns, it is replaced. Returns true if a key was added or
		///replaced.</summary>
		public bool EnsureIndex(string tableName,string indexName,string columns,bool isUnique=false) {
			RequireTable(tableName);
			string kind=isUnique ? "UNIQUE KEY" : "KEY";
			SchemaIndex index=GetTable(tableName).GetIndex(indexName);
			if(index!=null && index.Kind==kind && index.Columns==columns && index.Name==indexName) {
				return false;
			}
			string command="ALTER TABLE `"+tableName+"` ";
			if(index!=null) {
				command+="DROP INDEX `"+index.Name+"`, ";
			}
			command+="ADD "+(isUnique ? "UNIQUE " : "")+"INDEX `"+indexName+"` ("+columns+")";
			Execute(command,tableName);
			return true;
		}

		///<summary>Drops a key if it exists. Returns true if it was dropped.</summary>
		public bool DropIndexIfExists(string tableName,string indexName) {
			if(!IndexExists(tableName,indexName)) {
				return false;
			}
			Execute("ALTER TABLE `"+tableName+"` DROP INDEX `"+GetTable(tableName).GetIndex(indexName).Name+"`",tableName);
			return true;
		}

		///<summary>Drops a column if it exists. Returns true if it was dropped. Only for columns that are not Open Dental schema, such as the office
		///customization the adoption step removes; Open Dental itself never drops columns in the 24.3 to 26.1 range.</summary>
		public bool DropColumnIfExists(string tableName,string columnName) {
			if(!ColumnExists(tableName,columnName)) {
				return false;
			}
			Execute("ALTER TABLE `"+tableName+"` DROP COLUMN `"+columnName+"`",tableName);
			return true;
		}

		///<summary>Converts a table and its text columns to the charset and collation if the table's default differs. Returns true if it converted.</summary>
		public bool EnsureTableCharset(string tableName,string charset,string collation) {
			RequireTable(tableName);
			SchemaTable table=GetTable(tableName);
			if(table.Charset==charset && table.Collation==collation) {
				return false;
			}
			Execute("ALTER TABLE `"+tableName+"` CONVERT TO CHARACTER SET "+charset+" COLLATE "+collation,tableName);
			return true;
		}
		#endregion Changing the schema

		#region Running statements
		///<summary>Runs a schema statement on tableName, logs it and forgets the cached definition of that table.</summary>
		public void Execute(string command,string tableName) {
			Db.NonQ(command);
			Log(command);
			if(tableName!=null) {
				_dictTableCache.Remove(tableName);
			}
		}

		///<summary>Runs a data statement (UPDATE, INSERT, DELETE), logs it with the number of rows it changed and returns that number.</summary>
		public long ExecuteData(string command) {
			long rows=Db.NonQ(command);
			Log(command+" -- "+rows+" row(s)");
			return rows;
		}

		public void Log(string message) {
			ListChanges.Add((StepCur==null ? "" : StepCur.Level+": ")+message);
		}
		#endregion Running statements

		private void RequireTable(string tableName) {
			if(!TableExists(tableName)) {
				throw new ApplicationException("Table "+tableName+" does not exist.");
			}
		}
	}
}
