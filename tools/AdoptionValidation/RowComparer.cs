// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>Compares one table of two databases on the same server row by row, joined on the primary key. The server does the work, so no row
	///leaves it and the tables may have millions of rows. Read-only. Text is compared byte for byte (BINARY), so a change of case or of trailing
	///spaces counts, and FLOAT and DOUBLE values are compared exactly, not as they print.</summary>
	public static class RowComparer {
		///<summary>Keys listed per kind of difference; the counts are always complete.</summary>
		public const int MaxListed=200;

		public class ChangedRow {
			public long Key;
			public List<string> ListColumns=new List<string>();

			public override string ToString() {
				return Key.ToString(CultureInfo.InvariantCulture)+" ("+string.Join(", ",ListColumns)+")";
			}
		}

		public class TableDiff {
			public string Table;
			public string DatabaseFirst;
			public string DatabaseSecond;
			public string KeyColumn;
			public List<string> ListColumnsCompared=new List<string>();
			public List<string> ListColumnsExcluded=new List<string>();
			///<summary>Columns that only one of the two tables has (generated columns are left out on both sides).</summary>
			public List<string> ListColumnsInOneOnly=new List<string>();
			public long CountOnlyFirst;
			public List<long> ListOnlyFirst=new List<long>();
			public long CountOnlySecond;
			public List<long> ListOnlySecond=new List<long>();
			public long CountChanged;
			public List<ChangedRow> ListChanged=new List<ChangedRow>();

			public bool IsIdentical {
				get { return CountOnlyFirst==0 && CountOnlySecond==0 && CountChanged==0 && ListColumnsInOneOnly.Count==0; }
			}

			public string Describe() {
				if(IsIdentical) {
					return Table+": identical row by row"+(ListColumnsExcluded.Count==0 ? "" : " apart from "+string.Join(", ",ListColumnsExcluded));
				}
				List<string> listParts=new List<string>();
				if(ListColumnsInOneOnly.Count>0) {
					listParts.Add("columns in one table only: "+string.Join(", ",ListColumnsInOneOnly));
				}
				if(CountOnlyFirst>0) {
					listParts.Add(CountOnlyFirst+" row(s) only in "+DatabaseFirst+" ("+KeyColumn+" "+Keys(ListOnlyFirst,CountOnlyFirst)+")");
				}
				if(CountOnlySecond>0) {
					listParts.Add(CountOnlySecond+" row(s) only in "+DatabaseSecond+" ("+KeyColumn+" "+Keys(ListOnlySecond,CountOnlySecond)+")");
				}
				if(CountChanged>0) {
					listParts.Add(CountChanged+" row(s) changed ("+KeyColumn+" "+string.Join("; ",ListChanged.Take(20))+(CountChanged>20 ? "; ..." : "")+")");
				}
				return Table+": "+string.Join("; ",listParts);
			}

			private static string Keys(List<long> listKeys,long count) {
				return string.Join(", ",listKeys.Take(20).Select(x => x.ToString(CultureInfo.InvariantCulture)))+(count>20 ? ", ..." : "");
			}
		}

		///<summary>Compares databaseFirst.table with databaseSecond.table. The table must have a single-column integer primary key, as every Open Dental
		///table has. excludedColumns are left out of the comparison (for example a time that differs by design).</summary>
		public static TableDiff Compare(Db db,string databaseFirst,string databaseSecond,string table,IEnumerable<string> excludedColumns=null) {
			LocalOnly.EnsureSafeIdentifier(databaseFirst);
			LocalOnly.EnsureSafeIdentifier(databaseSecond);
			LocalOnly.EnsureSafeIdentifier(table);
			TableDiff diff=new TableDiff { Table=table,DatabaseFirst=databaseFirst,DatabaseSecond=databaseSecond };
			HashSet<string> setExcluded=new HashSet<string>(excludedColumns??new string[0],StringComparer.OrdinalIgnoreCase);
			List<(string name,string type)> listFirst=StoredColumns(db,databaseFirst,table);
			List<(string name,string type)> listSecond=StoredColumns(db,databaseSecond,table);
			if(listFirst.Count==0 || listSecond.Count==0) {
				throw new ApplicationException("Table "+table+" is missing from "+(listFirst.Count==0 ? databaseFirst : databaseSecond)+".");
			}
			diff.ListColumnsInOneOnly.AddRange(listFirst.Select(x => x.name).Except(listSecond.Select(x => x.name),StringComparer.OrdinalIgnoreCase)
				.Concat(listSecond.Select(x => x.name).Except(listFirst.Select(x => x.name),StringComparer.OrdinalIgnoreCase)));
			List<string> listKey=db.Rows("SELECT COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA="+SqlLiteral.Quote(databaseFirst)
				+" AND TABLE_NAME="+SqlLiteral.Quote(table)+" AND INDEX_NAME='PRIMARY' ORDER BY SEQ_IN_INDEX").Select(x => Convert.ToString(x[0])).ToList();
			if(listKey.Count!=1) {
				throw new ApplicationException("Table "+table+" has no single-column primary key; it cannot be compared row by row.");
			}
			diff.KeyColumn=listKey[0];
			List<(string name,string type)> listCompared=listFirst.Where(x => listSecond.Any(y => string.Equals(x.name,y.name,StringComparison.OrdinalIgnoreCase))
				&& !string.Equals(x.name,diff.KeyColumn,StringComparison.OrdinalIgnoreCase)).ToList();
			diff.ListColumnsExcluded.AddRange(listCompared.Where(x => setExcluded.Contains(x.name)).Select(x => x.name));
			listCompared=listCompared.Where(x => !setExcluded.Contains(x.name)).ToList();
			diff.ListColumnsCompared.AddRange(listCompared.Select(x => x.name));
			string from="`"+databaseFirst+"`.`"+table+"` a";
			string join="`"+databaseSecond+"`.`"+table+"` b ON a.`"+diff.KeyColumn+"`=b.`"+diff.KeyColumn+"`";
			string key="`"+diff.KeyColumn+"`";
			diff.CountOnlyFirst=db.ScalarLong("SELECT COUNT(*) FROM "+from+" LEFT JOIN "+join+" WHERE b."+key+" IS NULL");
			if(diff.CountOnlyFirst>0) {
				diff.ListOnlyFirst=db.Rows("SELECT a."+key+" FROM "+from+" LEFT JOIN "+join+" WHERE b."+key+" IS NULL ORDER BY a."+key+" LIMIT "+MaxListed)
					.Select(x => Convert.ToInt64(x[0],CultureInfo.InvariantCulture)).ToList();
			}
			string fromSecond="`"+databaseSecond+"`.`"+table+"` b";
			string joinFirst="`"+databaseFirst+"`.`"+table+"` a ON a."+key+"=b."+key;
			diff.CountOnlySecond=db.ScalarLong("SELECT COUNT(*) FROM "+fromSecond+" LEFT JOIN "+joinFirst+" WHERE a."+key+" IS NULL");
			if(diff.CountOnlySecond>0) {
				diff.ListOnlySecond=db.Rows("SELECT b."+key+" FROM "+fromSecond+" LEFT JOIN "+joinFirst+" WHERE a."+key+" IS NULL ORDER BY b."+key+" LIMIT "+MaxListed)
					.Select(x => Convert.ToInt64(x[0],CultureInfo.InvariantCulture)).ToList();
			}
			if(listCompared.Count==0) {
				return diff;
			}
			List<string> listEqual=listCompared.Select(x => Equal(x.name,x.type)).ToList();
			string whereChanged="NOT ("+string.Join(" AND ",listEqual)+")";
			diff.CountChanged=db.ScalarLong("SELECT COUNT(*) FROM "+from+" INNER JOIN "+join+" WHERE "+whereChanged);
			if(diff.CountChanged>0) {
				foreach(object[] row in db.Rows("SELECT a."+key+","+string.Join(",",listEqual.Select(x => "NOT ("+x+")"))+" FROM "+from+" INNER JOIN "+join
					+" WHERE "+whereChanged+" ORDER BY a."+key+" LIMIT "+MaxListed))
				{
					ChangedRow changedRow=new ChangedRow { Key=Convert.ToInt64(row[0],CultureInfo.InvariantCulture) };
					for(int i=0;i<listCompared.Count;i++) {
						if(Convert.ToInt64(row[i+1],CultureInfo.InvariantCulture)!=0) {
							changedRow.ListColumns.Add(listCompared[i].name);
						}
					}
					diff.ListChanged.Add(changedRow);
				}
			}
			return diff;
		}

		///<summary>NULL-safe equality of one column of a and b: byte for byte for text, exact for numbers and dates.</summary>
		private static string Equal(string column,string dataType) {
			string a="a.`"+column+"`";
			string b="b.`"+column+"`";
			bool isText=dataType.Contains("char") || dataType.Contains("text") || dataType=="enum" || dataType=="set";
			return isText ? "(BINARY "+a+" <=> BINARY "+b+")" : "("+a+" <=> "+b+")";
		}

		///<summary>The stored (not generated) columns of a table and their lower-case data types, in order; empty when the table does not exist.
		///</summary>
		private static List<(string name,string type)> StoredColumns(Db db,string database,string table) {
			return db.Rows("SELECT COLUMN_NAME,DATA_TYPE,EXTRA FROM information_schema.COLUMNS WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)+" AND TABLE_NAME="
				+SqlLiteral.Quote(table)+" ORDER BY ORDINAL_POSITION")
				.Where(x => !Convert.ToString(x[2]??"").ToLowerInvariant().Contains("generated"))
				.Select(x => (Convert.ToString(x[0]),Convert.ToString(x[1]).ToLowerInvariant())).ToList();
		}
	}
}
