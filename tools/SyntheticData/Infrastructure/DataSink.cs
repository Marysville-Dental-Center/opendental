// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SyntheticData.Infrastructure {
	///<summary>Column values of one row. Columns left out get the column's default, or the zero value Open Dental itself writes
	///('' for text, 0 for numbers, 0001-01-01 for dates).</summary>
	public class Row : Dictionary<string,object> {
		public Row() : base(StringComparer.OrdinalIgnoreCase) {
		}
	}

	///<summary>Formats C# values as SQL literals for a given column.</summary>
	public static class SqlLiteral {
		public static string Quote(string text) {
			StringBuilder stringBuilder=new StringBuilder(text.Length+2);
			stringBuilder.Append('\'');
			foreach(char c in text) {
				switch(c) {
					case '\\': stringBuilder.Append("\\\\"); break;
					case '\'': stringBuilder.Append("\\'"); break;
					case '\0': stringBuilder.Append("\\0"); break;
					case '\n': stringBuilder.Append("\\n"); break;
					case '\r': stringBuilder.Append("\\r"); break;
					case '\x1a': stringBuilder.Append("\\Z"); break;
					default: stringBuilder.Append(c); break;
				}
			}
			stringBuilder.Append('\'');
			return stringBuilder.ToString();
		}

		public static string Date(DateTime date) {
			return "'"+date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+"'";
		}

		public static string DateTimeValue(DateTime dateTime) {
			return "'"+dateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)+"'";
		}

		public static string Format(object value,ColumnInfo column,string tableName) {
			string where=tableName+"."+column.Name;
			if(value==null) {
				if(!column.IsNullable) {
					throw new InvalidOperationException(where+" is NOT NULL but the generator gave null.");
				}
				return "NULL";
			}
			switch(value) {
				case string text:
					if(column.MaxLength.HasValue && text.Length>column.MaxLength.Value) {
						throw new InvalidOperationException(where+" holds at most "+column.MaxLength+" characters; the generator gave "+text.Length+".");
					}
					return Quote(text);
				case bool flag:
					return flag ? "1" : "0";
				case Enum enumValue:
					return Convert.ToInt64(enumValue).ToString(CultureInfo.InvariantCulture);
				case decimal amount:
					return amount.ToString(CultureInfo.InvariantCulture);
				case double number:
					return number.ToString("R",CultureInfo.InvariantCulture);
				case float number:
					return number.ToString("R",CultureInfo.InvariantCulture);
				case DateTime dateTime:
					if(column.IsDateOnly) {
						return Date(dateTime);
					}
					if(column.IsTime) {
						return "'"+dateTime.ToString("HH:mm:ss",CultureInfo.InvariantCulture)+"'";
					}
					return DateTimeValue(dateTime);
				case TimeSpan timeSpan:
					return "'"+((int)timeSpan.TotalHours).ToString("00",CultureInfo.InvariantCulture)+":"+timeSpan.Minutes.ToString("00",CultureInfo.InvariantCulture)
						+":"+timeSpan.Seconds.ToString("00",CultureInfo.InvariantCulture)+"'";
				case byte[] bytes:
					if(bytes.Length==0) {
						return "''";
					}
					StringBuilder stringBuilder=new StringBuilder(bytes.Length*2+2);
					stringBuilder.Append("0x");
					foreach(byte b in bytes) {
						stringBuilder.Append(b.ToString("X2"));
					}
					return stringBuilder.ToString();
				case sbyte _:
				case byte _:
				case short _:
				case ushort _:
				case int _:
				case uint _:
				case long _:
				case ulong _:
					return Convert.ToString(value,CultureInfo.InvariantCulture);
				default:
					throw new InvalidOperationException(where+": unsupported value type "+value.GetType().Name+".");
			}
		}

		///<summary>The value written for a column the generator did not set.</summary>
		public static string Missing(ColumnInfo column,DateTime timestampValue) {
			if(column.DataType=="timestamp") {
				//Never let the server fill current_timestamp(): the data would differ on every run.
				return DateTimeValue(timestampValue);
			}
			if(column.HasUsableDefault) {
				return "DEFAULT";
			}
			if(column.IsInteger || column.IsDecimal) {
				return "0";
			}
			if(column.IsDateOnly) {
				return "'0001-01-01'";
			}
			if(column.IsDateTime) {
				return "'0001-01-01 00:00:00'";
			}
			if(column.IsTime) {
				return "'00:00:00'";
			}
			return "''";
		}
	}

	///<summary>Buffers rows per table and writes them with multi-row INSERT statements. Hands out primary keys, so rows can refer to
	///each other before anything is written and the same seed always gives the same keys. Every column name is checked against the
	///target schema, so a generator that drifts from the 25.3.48 schema fails loudly.</summary>
	public sealed class DataSink {
		private const int MaxStatementBytes=1<<20;
		private readonly Db _db;
		private readonly SchemaInfo _schema;
		private readonly DateTime _timestampValue;
		private readonly Dictionary<string,TableBuffer> _dictBuffers=new Dictionary<string,TableBuffer>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string,long> _dictNextIds=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
		private readonly SortedDictionary<string,long> _dictRowCounts=new SortedDictionary<string,long>(StringComparer.OrdinalIgnoreCase);

		public DataSink(Db db,SchemaInfo schema,DateTime timestampValue) {
			_db=db;
			_schema=schema;
			_timestampValue=timestampValue;
		}

		public SchemaInfo Schema {
			get { return _schema; }
		}

		///<summary>Rows written (or buffered) per table by this sink.</summary>
		public IReadOnlyDictionary<string,long> RowCounts {
			get { return _dictRowCounts; }
		}

		///<summary>The next primary key for the table: one more than the largest key already in the table or handed out.</summary>
		public long NextId(string table) {
			if(!_dictNextIds.TryGetValue(table,out long next)) {
				TableInfo tableInfo=RequireTable(table);
				ColumnInfo columnKey=tableInfo.PrimaryKey;
				if(columnKey==null) {
					throw new InvalidOperationException("Table "+table+" has no primary key.");
				}
				next=_db.ScalarLong("SELECT IFNULL(MAX(`"+columnKey.Name+"`),0) FROM `"+tableInfo.Name+"`")+1;
			}
			_dictNextIds[table]=next+1;
			return next;
		}

		public bool HasTable(string table) {
			return _schema.GetTable(table)!=null;
		}

		///<summary>Adds a row. The primary key must be set by the caller (from NextId) unless the table has none.</summary>
		public void Insert(string table,Row row) {
			if(!_dictBuffers.TryGetValue(table,out TableBuffer buffer)) {
				buffer=new TableBuffer(RequireTable(table));
				_dictBuffers[table]=buffer;
			}
			foreach(string columnName in row.Keys) {
				ColumnInfo column=buffer.TableInfo.GetColumn(columnName);
				if(column==null) {
					throw new InvalidOperationException("Column "+table+"."+columnName+" does not exist in the target schema.");
				}
				if(column.IsGenerated) {
					throw new InvalidOperationException("Column "+table+"."+columnName+" is generated and cannot be written.");
				}
			}
			ColumnInfo columnPrimaryKey=buffer.TableInfo.PrimaryKey;
			if(columnPrimaryKey!=null && columnPrimaryKey.IsAutoIncrement && !row.ContainsKey(columnPrimaryKey.Name)) {
				throw new InvalidOperationException("Row for "+table+" has no "+columnPrimaryKey.Name+"; take one from NextId.");
			}
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append('(');
			for(int i=0;i<buffer.ListColumns.Count;i++) {
				ColumnInfo column=buffer.ListColumns[i];
				if(i>0) {
					stringBuilder.Append(',');
				}
				if(row.TryGetValue(column.Name,out object value)) {
					stringBuilder.Append(SqlLiteral.Format(value,column,table));
				}
				else {
					stringBuilder.Append(SqlLiteral.Missing(column,_timestampValue));
				}
			}
			stringBuilder.Append(')');
			string tuple=stringBuilder.ToString();
			if(buffer.Bytes+tuple.Length>MaxStatementBytes) {
				Flush(buffer);
			}
			buffer.ListTuples.Add(tuple);
			buffer.Bytes+=tuple.Length+1;
			_dictRowCounts.TryGetValue(buffer.TableInfo.Name,out long count);
			_dictRowCounts[buffer.TableInfo.Name]=count+1;
		}

		///<summary>Writes every buffered row.</summary>
		public void FlushAll() {
			foreach(TableBuffer buffer in _dictBuffers.Values.OrderBy(x => x.TableInfo.Name,StringComparer.Ordinal)) {
				Flush(buffer);
			}
		}

		private void Flush(TableBuffer buffer) {
			if(buffer.ListTuples.Count==0) {
				return;
			}
			StringBuilder stringBuilder=new StringBuilder(buffer.Bytes+buffer.Header.Length+16);
			stringBuilder.Append(buffer.Header);
			for(int i=0;i<buffer.ListTuples.Count;i++) {
				if(i>0) {
					stringBuilder.Append(',');
				}
				stringBuilder.Append(buffer.ListTuples[i]);
			}
			_db.Execute(stringBuilder.ToString());
			buffer.ListTuples.Clear();
			buffer.Bytes=0;
		}

		private TableInfo RequireTable(string table) {
			TableInfo tableInfo=_schema.GetTable(table);
			if(tableInfo==null) {
				throw new InvalidOperationException("Table "+table+" does not exist in the target schema.");
			}
			return tableInfo;
		}

		private class TableBuffer {
			public readonly TableInfo TableInfo;
			public readonly List<ColumnInfo> ListColumns;
			public readonly string Header;
			public readonly List<string> ListTuples=new List<string>();
			public int Bytes;

			public TableBuffer(TableInfo tableInfo) {
				TableInfo=tableInfo;
				ListColumns=tableInfo.ListColumns.Where(x => !x.IsGenerated).ToList();
				Header="INSERT INTO `"+tableInfo.Name+"` ("+string.Join(",",ListColumns.Select(x => "`"+x.Name+"`"))+") VALUES ";
			}
		}
	}
}
