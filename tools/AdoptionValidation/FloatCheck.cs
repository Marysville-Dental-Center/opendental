// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>Finds the FLOAT and DOUBLE values that a logical backup (mariadb-dump) would not restore exactly. MariaDB writes a FLOAT as text with 6
	///significant digits, so a value with more (Open Dental computes some, imagedraw.FontSize for one) comes back rounded: 34.285713 as 34.2857. Every
	///program reads FLOAT through the same 6-digit text, so it shows the same value before and after, but the stored bits differ and CHECKSUM TABLE
	///sees it. A DOUBLE is written with enough digits to come back exactly; it is checked too, in case a server behaves otherwise. A value counts when
	///it differs from itself written as text and read back: c &lt;&gt; CAST(CAST(c AS CHAR) AS FLOAT). Read-only; one pass over each table that has
	///such columns; names columns and counts, never values. The runbook's copies (step 3.1's folder copy, step 3.2's server-side copy) are exact either
	///way; this tells the owner what a logical backup of the office database, such as a nightly dump, would round.</summary>
	public static class FloatCheck {
		public class ColumnCount {
			public string Table;
			public string Column;
			public string DataType;
			public long Count;

			public override string ToString() {
				return Table+"."+Column+" ("+DataType+"): "+Count.ToString("N0",CultureInfo.InvariantCulture)+" value(s)";
			}
		}

		public class Result {
			public int ColumnsChecked;
			public int TablesChecked;
			///<summary>Only the columns with at least one value a dump would round.</summary>
			public List<ColumnCount> ListColumns=new List<ColumnCount>();

			public long Total {
				get { return ListColumns.Sum(x => x.Count); }
			}

			public string Describe() {
				return ColumnsChecked+" FLOAT and DOUBLE column(s) in "+TablesChecked+" table(s) checked; "+(Total==0
					? "no value that a logical backup (mariadb-dump) would round"
					: Total.ToString("N0",CultureInfo.InvariantCulture)+" value(s) in "+ListColumns.Count+" column(s) that a logical backup (mariadb-dump) would round: "
						+string.Join("; ",ListColumns));
			}
		}

		///<summary>The SQL expression that is 1 when a dump would not restore the value exactly. Public for the tests.</summary>
		public static string RoundedExpression(string column,string dataType) {
			string type=dataType=="float" ? "FLOAT" : "DOUBLE";
			return "IFNULL(SUM(`"+column+"`<>CAST(CAST(`"+column+"` AS CHAR) AS "+type+")),0)";
		}

		public static Result Run(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			if(!db.DatabaseExists(database)) {
				throw new RefusedException("Database "+database+" does not exist on this server.");
			}
			Result result=new Result();
			HashSet<string> setBaseTables=new HashSet<string>(db.Rows("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)
				+" AND TABLE_TYPE='BASE TABLE'").Select(x => Convert.ToString(x[0])),StringComparer.Ordinal);
			List<object[]> listRows=db.Rows("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)
				+" AND DATA_TYPE IN ('float','double') AND EXTRA NOT LIKE '%GENERATED%' ORDER BY TABLE_NAME,ORDINAL_POSITION")
				.Where(x => setBaseTables.Contains(Convert.ToString(x[0]))).ToList();
			foreach(IGrouping<string,object[]> table in listRows.GroupBy(x => Convert.ToString(x[0]))) {
				List<(string column,string type)> listColumns=table.Select(x => (Convert.ToString(x[1]),Convert.ToString(x[2]).ToLowerInvariant())).ToList();
				if(table.Key.Contains("`") || listColumns.Any(x => x.column.Contains("`"))) {
					throw new ApplicationException("The table or column name in "+table.Key+" cannot be checked.");
				}
				object[] row=db.Rows("SELECT "+string.Join(",",listColumns.Select(x => RoundedExpression(x.column,x.type)))+" FROM `"+database+"`.`"+table.Key+"`")[0];
				for(int i=0;i<listColumns.Count;i++) {
					long count=Convert.ToInt64(row[i],CultureInfo.InvariantCulture);
					if(count>0) {
						result.ListColumns.Add(new ColumnCount { Table=table.Key,Column=listColumns[i].column,DataType=listColumns[i].type,Count=count });
					}
				}
				result.TablesChecked++;
				result.ColumnsChecked+=listColumns.Count;
			}
			return result;
		}
	}
}
