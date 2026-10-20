// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntheticData.Infrastructure {
	public class ColumnInfo {
		public string Name;
		public int Ordinal;
		///<summary>Lower-case base type, for example bigint, varchar, date, timestamp.</summary>
		public string DataType;
		///<summary>Full type, for example bigint(20) or varchar(255).</summary>
		public string ColumnType;
		public bool IsNullable;
		///<summary>Default as information_schema shows it (MariaDB quotes string literals), or null when there is none.</summary>
		public string Default;
		public long? MaxLength;
		public bool IsPrimaryKey;
		public bool IsGenerated;
		public bool IsAutoIncrement;

		public bool IsText {
			get { return DataType.Contains("char") || DataType.Contains("text"); }
		}

		public bool IsBinary {
			get { return DataType.Contains("blob") || DataType.Contains("binary"); }
		}

		public bool IsInteger {
			get { return DataType.EndsWith("int"); }
		}

		public bool IsDecimal {
			get { return DataType=="double" || DataType=="float" || DataType=="decimal"; }
		}

		public bool IsDateOnly {
			get { return DataType=="date"; }
		}

		public bool IsDateTime {
			get { return DataType=="datetime" || DataType=="timestamp"; }
		}

		public bool IsTime {
			get { return DataType=="time"; }
		}

		///<summary>True when the column has a real default value that is neither NULL nor a zero date.</summary>
		public bool HasUsableDefault {
			get {
				if(Default==null || Default.Equals("NULL",StringComparison.OrdinalIgnoreCase)) {
					return false;
				}
				if(Default.Contains("0000-00-00") || Default.StartsWith("current_timestamp",StringComparison.OrdinalIgnoreCase)) {
					return false;
				}
				return true;
			}
		}
	}

	public class TableInfo {
		public string Name;
		public List<ColumnInfo> ListColumns=new List<ColumnInfo>();
		private Dictionary<string,ColumnInfo> _dictColumns;

		public ColumnInfo PrimaryKey {
			get { return ListColumns.FirstOrDefault(x => x.IsPrimaryKey); }
		}

		public ColumnInfo GetColumn(string name) {
			if(_dictColumns==null) {
				_dictColumns=ListColumns.ToDictionary(x => x.Name,StringComparer.OrdinalIgnoreCase);
			}
			_dictColumns.TryGetValue(name,out ColumnInfo column);
			return column;
		}

		public bool HasColumn(string name) {
			return GetColumn(name)!=null;
		}
	}

	///<summary>The tables and columns of one database, read from information_schema.</summary>
	public class SchemaInfo {
		public string Database;
		public SortedDictionary<string,TableInfo> DictTables=new SortedDictionary<string,TableInfo>(StringComparer.OrdinalIgnoreCase);

		public static SchemaInfo Load(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			SchemaInfo schema=new SchemaInfo { Database=database };
			List<object[]> listRows=db.Rows("SELECT TABLE_NAME,COLUMN_NAME,ORDINAL_POSITION,COLUMN_DEFAULT,IS_NULLABLE,DATA_TYPE,COLUMN_TYPE,"
				+"CHARACTER_MAXIMUM_LENGTH,COLUMN_KEY,EXTRA "
				+"FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"+database+"' ORDER BY TABLE_NAME,ORDINAL_POSITION");
			foreach(object[] row in listRows) {
				string tableName=Convert.ToString(row[0]).ToLowerInvariant();
				if(!schema.DictTables.TryGetValue(tableName,out TableInfo table)) {
					table=new TableInfo { Name=tableName };
					schema.DictTables[tableName]=table;
				}
				string extra=Convert.ToString(row[9]??"").ToLowerInvariant();
				table.ListColumns.Add(new ColumnInfo {
					Name=Convert.ToString(row[1]),
					Ordinal=Convert.ToInt32(row[2]),
					Default=row[3]==null ? null : Convert.ToString(row[3]),
					IsNullable=Convert.ToString(row[4])=="YES",
					DataType=Convert.ToString(row[5]).ToLowerInvariant(),
					ColumnType=Convert.ToString(row[6]).ToLowerInvariant(),
					MaxLength=row[7]==null ? (long?)null : Convert.ToInt64(row[7]),
					IsPrimaryKey=Convert.ToString(row[8])=="PRI",
					IsGenerated=extra.Contains("generated"),
					IsAutoIncrement=extra.Contains("auto_increment"),
				});
			}
			return schema;
		}

		public TableInfo GetTable(string name) {
			DictTables.TryGetValue(name,out TableInfo table);
			return table;
		}

		public int ColumnCount {
			get { return DictTables.Values.Sum(x => x.ListColumns.Count); }
		}

		///<summary>Differences in tables, columns, types, nullability and defaults between two schemas. Empty when they match.</summary>
		public static List<string> Diff(SchemaInfo expected,SchemaInfo actual) {
			List<string> listDiffs=new List<string>();
			foreach(TableInfo tableExpected in expected.DictTables.Values) {
				TableInfo tableActual=actual.GetTable(tableExpected.Name);
				if(tableActual==null) {
					listDiffs.Add("missing table "+tableExpected.Name);
					continue;
				}
				foreach(ColumnInfo columnExpected in tableExpected.ListColumns) {
					ColumnInfo columnActual=tableActual.GetColumn(columnExpected.Name);
					string where=tableExpected.Name+"."+columnExpected.Name;
					if(columnActual==null) {
						listDiffs.Add("missing column "+where);
						continue;
					}
					if(columnActual.ColumnType!=columnExpected.ColumnType) {
						listDiffs.Add(where+" type "+columnActual.ColumnType+" (expected "+columnExpected.ColumnType+")");
					}
					if(columnActual.IsNullable!=columnExpected.IsNullable) {
						listDiffs.Add(where+" nullable "+columnActual.IsNullable+" (expected "+columnExpected.IsNullable+")");
					}
					if(columnActual.Default!=columnExpected.Default) {
						listDiffs.Add(where+" default "+(columnActual.Default??"(none)")+" (expected "+(columnExpected.Default??"(none)")+")");
					}
					if(columnActual.Ordinal!=columnExpected.Ordinal) {
						listDiffs.Add(where+" position "+columnActual.Ordinal+" (expected "+columnExpected.Ordinal+")");
					}
				}
				foreach(ColumnInfo columnActual in tableActual.ListColumns) {
					if(!tableExpected.HasColumn(columnActual.Name)) {
						listDiffs.Add("extra column "+tableExpected.Name+"."+columnActual.Name);
					}
				}
			}
			foreach(TableInfo tableActual in actual.DictTables.Values) {
				if(expected.GetTable(tableActual.Name)==null) {
					listDiffs.Add("extra table "+tableActual.Name);
				}
			}
			return listDiffs;
		}
	}
}
