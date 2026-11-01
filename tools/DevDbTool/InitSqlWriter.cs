// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DataConnectionBase;
using OpenPmsDentalBusiness;

namespace DevDbTool {
	///<summary>Writes a database as one SQL file, in C# and without MariaDB client tools: a header, the session settings of the seed dump, then for
	///every table in name order its SHOW CREATE TABLE (without the AUTO_INCREMENT counter) and its rows in primary key order as multi-row INSERTs.
	///Values come from the server as text (CAST AS CHAR, HEX for binary columns), so numbers and dates are written exactly as the server prints them.
	///Line ends are LF. The same database always gives the same text. No DROP statement: loaded into a database that already has the tables, the
	///file stops at the first CREATE TABLE instead of replacing anything.</summary>
	public class InitSqlWriter {
		///<summary>An INSERT statement is closed once it is this long, well under the 16 MB max_allowed_packet of a default MariaDB or MySQL server and
		///client.</summary>
		public const int MaxStatementChars=1000000;
		private static readonly string[] _arrayNumericTypes={ "tinyint","smallint","mediumint","int","bigint","decimal","float","double" };
		private static readonly string[] _arrayDateTypes={ "date","datetime","timestamp","time","year" };
		private static readonly string[] _arrayTextTypes={ "char","varchar","tinytext","text","mediumtext","longtext","enum","set" };
		private static readonly string[] _arrayBinaryTypes={ "binary","varbinary","tinyblob","blob","mediumblob","longblob" };

		private readonly string _database;
		public int CountTables;
		public long CountRows;

		public InitSqlWriter(string database) {
			_database=database;
		}

		public string Write() {
			string db=POut.String(_database);
			DataTable tableTables=DataCore.GetTable("SELECT TABLE_NAME,TABLE_TYPE FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+db+"'");
			List<string> listTables=new List<string>();
			foreach(DataRow row in tableTables.Rows) {
				if(row["TABLE_TYPE"].ToString()!="BASE TABLE") {
					throw new ApplicationException("Only tables can be written; "+row["TABLE_NAME"]+" is a "+row["TABLE_TYPE"]+".");
				}
				listTables.Add(row["TABLE_NAME"].ToString());
			}
			listTables.Sort(StringComparer.Ordinal);
			Dictionary<string,List<ColumnInfo>> dictColumns=DataCore.GetTable("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,ORDINAL_POSITION "
				+"FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"+db+"'").Rows.Cast<DataRow>()
				.Select(x => new ColumnInfo {
					TableName=x["TABLE_NAME"].ToString(),
					Name=x["COLUMN_NAME"].ToString(),
					DataType=x["DATA_TYPE"].ToString().ToLowerInvariant(),
					Position=PIn.Int(x["ORDINAL_POSITION"].ToString()),
				})
				.GroupBy(x => x.TableName)
				.ToDictionary(x => x.Key,x => x.OrderBy(y => y.Position).ToList());
			Dictionary<string,List<string>> dictPrimaryKeys=DataCore.GetTable("SELECT TABLE_NAME,COLUMN_NAME,SEQ_IN_INDEX FROM information_schema.STATISTICS "
				+"WHERE TABLE_SCHEMA='"+db+"' AND INDEX_NAME='PRIMARY'").Rows.Cast<DataRow>()
				.GroupBy(x => x["TABLE_NAME"].ToString())
				.ToDictionary(x => x.Key,x => x.OrderBy(y => PIn.Int(y["SEQ_IN_INDEX"].ToString())).Select(y => y["COLUMN_NAME"].ToString()).ToList());
			StringBuilder sb=new StringBuilder();
			WriteHeader(sb,listTables.Count);
			foreach(string tableName in listTables) {
				List<ColumnInfo> listColumns=dictColumns[tableName];
				List<string> listKey;
				if(!dictPrimaryKeys.TryGetValue(tableName,out listKey)) {
					listKey=listColumns.Select(x => x.Name).ToList();//No primary key: every column, so the order is still total.
				}
				sb.Append("/*Table structure for table `").Append(tableName).Append("` */\n\n");
				sb.Append(GetCreateTable(tableName)).Append(";\n\n");
				WriteRows(sb,tableName,listColumns,listKey);
				CountTables++;
			}
			WriteFooter(sb);
			return sb.ToString();
		}

		private void WriteHeader(StringBuilder sb,int countTables) {
			string level=DevDatabase.GetPrefRaw(ForkVersions.PREF_FORK_SCHEMA_LEVEL);
			string odSchema=DevDatabase.GetPrefRaw(ForkVersions.PREF_OD_SCHEMA_COMPAT);
			sb.Append("/*\n");
			sb.Append("Open PMS - Dental: a new database.\n");
			sb.Append("\n");
			sb.Append("Creates the ").Append(countTables).Append(" tables of a database at fork schema level ").Append(level).Append(" (the Open Dental ")
				.Append(odSchema).Append(" schema and the\n");
			sb.Append("fork's), with the rows the program needs to start: preferences, definitions, user groups and permissions, the user Admin,\n");
			sb.Append("program links (all off) and procedure codes (named by their code only). No patients and no office data. Admin has a blank\n");
			sb.Append("password, which the program asks to set at the first logon.\n");
			sb.Append("\n");
			sb.Append("Load it into a new, empty database created with CHARACTER SET utf8mb3 (db/README.md). Nothing is dropped: in a database\n");
			sb.Append("that already has tables, loading stops at the first CREATE TABLE.\n");
			sb.Append("\n");
			sb.Append("Generated from UnitTests/Resources/dump.txt by tools/New-InitSql.ps1 (DevDbTool --export-init-sql). Do not edit it by hand:\n");
			sb.Append("regenerate it after every new fork migration step.\n");
			sb.Append("\n");
			sb.Append("Part of Open PMS - Dental, a fork of Open Dental. Licensed under the GNU General Public License, version 2.\n");
			sb.Append("See LICENSE and NOTICE in the repository root.\n");
			sb.Append("*/\n\n");
			sb.Append("/*!40101 SET NAMES utf8 */;\n\n");
			sb.Append("/*!40101 SET SQL_MODE=''*/;\n\n");
			sb.Append("/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;\n");
			sb.Append("/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;\n");
			sb.Append("/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;\n");
			sb.Append("/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;\n\n");
		}

		private static void WriteFooter(StringBuilder sb) {
			sb.Append("/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;\n");
			sb.Append("/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;\n");
			sb.Append("/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;\n");
			sb.Append("/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;\n");
		}

		///<summary>SHOW CREATE TABLE without the AUTO_INCREMENT counter (the server sets it from the rows), with LF line ends.</summary>
		private string GetCreateTable(string tableName) {
			DataTable table=DataCore.GetTable("SHOW CREATE TABLE `"+_database+"`.`"+tableName+"`");
			string create=table.Rows[0][1].ToString().Replace("\r\n","\n");
			return Regex.Replace(create,@" AUTO_INCREMENT=\d+","");
		}

		private void WriteRows(StringBuilder sb,string tableName,List<ColumnInfo> listColumns,List<string> listKey) {
			string select="SELECT "+string.Join(",",listColumns.Select(x => GetSelectExpression(x)))+" FROM `"+_database+"`.`"+tableName+"` ORDER BY "
				+string.Join(",",listKey.Select(x => "`"+x+"`"));
			DataTable table=DataCore.GetTable(select);
			if(table.Rows.Count==0) {
				return;
			}
			sb.Append("/*Data for the table `").Append(tableName).Append("` */\n\n");
			string insert="INSERT INTO `"+tableName+"` ("+string.Join(",",listColumns.Select(x => "`"+x.Name+"`"))+") VALUES\n";
			int lengthStatementStart=-1;
			for(int i=0;i<table.Rows.Count;i++) {
				if(lengthStatementStart<0) {
					lengthStatementStart=sb.Length;
					sb.Append(insert);
				}
				sb.Append('(');
				for(int c=0;c<listColumns.Count;c++) {
					if(c>0) {
						sb.Append(',');
					}
					AppendValue(sb,listColumns[c],table.Rows[i][c]);
				}
				sb.Append(')');
				CountRows++;
				bool isLast=i==table.Rows.Count-1;
				if(isLast || sb.Length-lengthStatementStart>=MaxStatementChars) {
					sb.Append(";\n");
					lengthStatementStart=-1;
				}
				else {
					sb.Append(",\n");
				}
			}
			sb.Append('\n');
		}

		private static string GetSelectExpression(ColumnInfo column) {
			string name="`"+column.Name+"`";
			if(_arrayNumericTypes.Contains(column.DataType) || _arrayDateTypes.Contains(column.DataType)) {
				return "CAST("+name+" AS CHAR)";
			}
			if(_arrayTextTypes.Contains(column.DataType)) {
				return name;
			}
			if(_arrayBinaryTypes.Contains(column.DataType)) {
				return "HEX("+name+")";
			}
			throw new ApplicationException("Column "+column.TableName+"."+column.Name+" has type "+column.DataType+", which the writer does not handle yet.");
		}

		private static void AppendValue(StringBuilder sb,ColumnInfo column,object value) {
			if(value==null || value==DBNull.Value) {
				sb.Append("NULL");
				return;
			}
			string text=Convert.ToString(value,CultureInfo.InvariantCulture);
			if(_arrayNumericTypes.Contains(column.DataType)) {
				sb.Append(text);
				return;
			}
			if(_arrayBinaryTypes.Contains(column.DataType)) {
				if(text=="") {
					sb.Append("''");
				}
				else {
					sb.Append("0x").Append(text);
				}
				return;
			}
			AppendQuoted(sb,text);
		}

		///<summary>A string literal with the escapes mysqldump uses, for a server without NO_BACKSLASH_ESCAPES (the file sets its own sql_mode).
		///</summary>
		public static void AppendQuoted(StringBuilder sb,string text) {
			sb.Append('\'');
			foreach(char c in text) {
				switch(c) {
					case '\\': sb.Append("\\\\"); break;
					case '\'': sb.Append("\\'"); break;
					case '"': sb.Append("\\\""); break;
					case '\0': sb.Append("\\0"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\u001a': sb.Append("\\Z"); break;
					default: sb.Append(c); break;
				}
			}
			sb.Append('\'');
		}

		private class ColumnInfo {
			public string TableName;
			public string Name;
			public string DataType;
			public int Position;
		}
	}
}
