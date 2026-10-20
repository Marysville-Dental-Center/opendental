// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using SyntheticData.Infrastructure;

namespace SyntheticData.Validation {
	///<summary>Records row counts, CHECKSUM TABLE values, per-column checksums and business totals for one database. Read-only.</summary>
	public static class Validator {
		public const string TotalsFileName="business-totals.sql";

		public static ValidationReport Run(Db db,string database,DateTime? asOf,Action<string> log) {
			LocalOnly.EnsureSafeIdentifier(database);
			db.Use(database);
			SchemaInfo schema=SchemaInfo.Load(db,database);
			ValidationReport report=new ValidationReport {
				Database=database,
				ServerVersion=db.ServerVersion(),
				TableCount=schema.DictTables.Count,
				ColumnCount=schema.ColumnCount,
			};
			report.GenerationInfo=ReadGenerationInfo(db,schema);
			DateTime dateAsOf=asOf??AsOfFromGenerationInfo(report.GenerationInfo)??Generation.GenerationSettings.DefaultAsOf;
			report.AsOf=dateAsOf.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
			log?.Invoke("Checksums for "+schema.DictTables.Count+" tables");
			foreach(TableInfo table in schema.DictTables.Values) {
				report.Tables.Add(ChecksumTable(db,table));
			}
			report.TotalRows=report.Tables.Sum(x => x.Rows);
			log?.Invoke("Business totals as of "+report.AsOf);
			foreach(KeyValuePair<string,string> pair in RunTotals(db,dateAsOf)) {
				report.Totals[pair.Key]=pair.Value;
			}
			return report;
		}

		public static TableReport ChecksumTable(Db db,TableInfo table) {
			TableReport tableReport=new TableReport { Name=table.Name };
			List<object[]> listChecksum=db.Rows("CHECKSUM TABLE `"+table.Name+"` EXTENDED");
			tableReport.ChecksumTable=Convert.ToString(listChecksum[0][1],CultureInfo.InvariantCulture);
			ColumnInfo columnKey=table.PrimaryKey;
			string key=columnKey==null ? "''" : "`"+columnKey.Name+"`";
			StringBuilder stringBuilder=new StringBuilder("SELECT COUNT(*)");
			foreach(ColumnInfo column in table.ListColumns) {
				stringBuilder.Append(",IFNULL(SUM(CRC32(CONCAT_WS(CHAR(31),"+key+",IFNULL(`"+column.Name+"`,CHAR(0))))),0)");
			}
			stringBuilder.Append(" FROM `"+table.Name+"`");
			object[] row=db.Rows(stringBuilder.ToString())[0];
			tableReport.Rows=Convert.ToInt64(row[0]);
			for(int i=0;i<table.ListColumns.Count;i++) {
				tableReport.Columns[table.ListColumns[i].Name]=Convert.ToString(row[i+1],CultureInfo.InvariantCulture);
			}
			return tableReport;
		}

		///<summary>Runs every named query of business-totals.sql. A failed query is recorded as "error: ...".</summary>
		public static SortedDictionary<string,string> RunTotals(Db db,DateTime asOf) {
			SortedDictionary<string,string> dictTotals=new SortedDictionary<string,string>(StringComparer.Ordinal);
			db.Execute("SET @AsOf='"+asOf.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+"'");
			foreach((string name,string sql) query in ReadQueries(ReadTotalsSql())) {
				try {
					foreach(object[] row in db.Rows(query.sql)) {
						string key=Convert.ToString(row[0],CultureInfo.InvariantCulture);
						dictTotals[key=="" ? query.name : query.name+"."+key]=FormatValue(row[1]);
					}
				}
				catch(Exception ex) {
					dictTotals[query.name]="error: "+ex.Message;
				}
			}
			return dictTotals;
		}

		public static string FormatValue(object value) {
			if(value==null) {
				return "0";
			}
			switch(value) {
				case string text:
					return text;
				case double _:
				case float _:
				case decimal _:
					return Convert.ToDecimal(value,CultureInfo.InvariantCulture).ToString("0.00",CultureInfo.InvariantCulture);
				default:
					return Convert.ToString(value,CultureInfo.InvariantCulture);
			}
		}

		public static string ReadTotalsSql() {
			return File.ReadAllText(ReadTotalsSqlPath(),Encoding.UTF8);
		}

		///<summary>sql\business-totals.sql next to the program, or (when run from another project's output folder, as the tests are) in the
		///source folder above it.</summary>
		public static string ReadTotalsSqlPath() {
			string directory=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			for(DirectoryInfo dir=new DirectoryInfo(directory);dir!=null;dir=dir.Parent) {
				foreach(string candidate in new[] { Path.Combine(dir.FullName,"sql",TotalsFileName),
					Path.Combine(dir.FullName,"tools","SyntheticData","sql",TotalsFileName) })
				{
					if(File.Exists(candidate)) {
						return candidate;
					}
				}
			}
			throw new FileNotFoundException("sql\\"+TotalsFileName+" was not found next to the program.");
		}

		///<summary>Splits the file on "-- name:" lines. Comment lines are dropped; the statement is everything up to the next name.</summary>
		public static List<(string name,string sql)> ReadQueries(string text) {
			List<(string,string)> listQueries=new List<(string,string)>();
			string name=null;
			StringBuilder current=new StringBuilder();
			void Finish() {
				if(name!=null) {
					string sql=current.ToString().Trim().TrimEnd(';').Trim();
					if(sql!="") {
						listQueries.Add((name,sql));
					}
				}
				current.Clear();
			}
			foreach(string rawLine in text.Replace("\r\n","\n").Split('\n')) {
				Match match=Regex.Match(rawLine,@"^--\s*name:\s*(\S+)\s*$");
				if(match.Success) {
					Finish();
					name=match.Groups[1].Value;
					continue;
				}
				if(rawLine.TrimStart().StartsWith("--")) {
					continue;
				}
				current.Append(rawLine).Append('\n');
			}
			Finish();
			return listQueries;
		}

		///<summary>Totals named check.* whose value is not 0.</summary>
		public static List<string> FailedChecks(ValidationReport report) {
			return report.Totals.Where(x => x.Key.StartsWith("check.") && x.Value!="0").Select(x => x.Key+" = "+x.Value).ToList();
		}

		private static string ReadGenerationInfo(Db db,SchemaInfo schema) {
			if(schema.GetTable("preference")==null) {
				return null;
			}
			return Convert.ToString(db.Scalar("SELECT ValueString FROM preference WHERE PrefName='SyntheticDataInfo' LIMIT 1"));
		}

		public static DateTime? AsOfFromGenerationInfo(string info) {
			Match match=Regex.Match(info??"",@"asOf=(\d{4}-\d{2}-\d{2})");
			if(!match.Success) {
				return null;
			}
			return DateTime.ParseExact(match.Groups[1].Value,"yyyy-MM-dd",CultureInfo.InvariantCulture);
		}
	}
}
