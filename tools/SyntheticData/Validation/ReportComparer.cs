// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SyntheticData.Validation {
	public enum DifferenceKind {
		TableAdded,
		TableRemoved,
		RowsChanged,
		ColumnAdded,
		ColumnRemoved,
		ColumnChanged,
		ChecksumTableChanged,
		TotalAdded,
		TotalRemoved,
		TotalChanged,
	}

	public class Difference {
		public DifferenceKind Kind;
		public string Table;
		public string Column;
		///<summary>Total name, for total differences.</summary>
		public string Name;
		public string Before;
		public string After;
		public bool IsAllowed;
		public string Reason;
		///<summary>What an unexpected difference means, when the comparer can tell (a CHECKSUM TABLE change with nothing else).</summary>
		public string Note;

		public override string ToString() {
			string where=Name??(Column==null ? Table : Table+"."+Column);
			string values=Before==null && After==null ? "" : " ("+(Before??"-")+" -> "+(After??"-")+")";
			return Kind+" "+where+values+(IsAllowed ? "  [allowed: "+Reason+"]" : Note==null ? "" : "  [note: "+Note+"]");
		}
	}

	///<summary>A difference that is expected, for example from adoption. Table, Column and Name accept * wildcards.</summary>
	public class AllowedRule {
		public string Kind;
		public string Table;
		public string Column;
		public string Name;
		public string Reason;

		public bool Matches(Difference difference) {
			if(!string.IsNullOrEmpty(Kind) && Kind!="*" && !string.Equals(Kind,difference.Kind.ToString(),StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			return Like(Table,difference.Table) && Like(Column,difference.Column) && Like(Name,difference.Name);
		}

		private static bool Like(string pattern,string value) {
			if(string.IsNullOrEmpty(pattern) || pattern=="*") {
				return true;
			}
			if(value==null) {
				return false;
			}
			string regex="^"+Regex.Escape(pattern).Replace("\\*",".*")+"$";
			return Regex.IsMatch(value,regex,RegexOptions.IgnoreCase);
		}
	}

	public class AllowedDifferences {
		public string Description;
		public List<AllowedRule> Rules=new List<AllowedRule>();

		public static AllowedDifferences Load(string path) {
			return JsonConvert.DeserializeObject<AllowedDifferences>(File.ReadAllText(path,Encoding.UTF8));
		}
	}

	///<summary>Compares two validation reports, for example before and after adoption, and sorts the differences into allowed and
	///unexpected ones.</summary>
	public static class ReportComparer {
		public const string NoteChecksumTableOnly="row count and every column checksum are equal, so every value reads the same, but the stored rows "
			+"differ (a row or date format the copy changed, or a value the column checksums cannot see); see the runbook, step 3.3";

		public static List<Difference> Compare(ValidationReport before,ValidationReport after,AllowedDifferences allowed) {
			List<Difference> listDiffs=new List<Difference>();
			Dictionary<string,TableReport> dictBefore=before.Tables.ToDictionary(x => x.Name,StringComparer.OrdinalIgnoreCase);
			Dictionary<string,TableReport> dictAfter=after.Tables.ToDictionary(x => x.Name,StringComparer.OrdinalIgnoreCase);
			foreach(TableReport tableBefore in before.Tables) {
				if(!dictAfter.TryGetValue(tableBefore.Name,out TableReport tableAfter)) {
					listDiffs.Add(new Difference { Kind=DifferenceKind.TableRemoved,Table=tableBefore.Name,Before=tableBefore.Rows+" rows" });
					continue;
				}
				List<Difference> listTable=new List<Difference>();
				if(tableBefore.Rows!=tableAfter.Rows) {
					listTable.Add(new Difference { Kind=DifferenceKind.RowsChanged,Table=tableBefore.Name,Before=tableBefore.Rows.ToString(),After=tableAfter.Rows.ToString() });
				}
				foreach(KeyValuePair<string,string> column in tableBefore.Columns) {
					if(!tableAfter.Columns.TryGetValue(column.Key,out string checksumAfter)) {
						listTable.Add(new Difference { Kind=DifferenceKind.ColumnRemoved,Table=tableBefore.Name,Column=column.Key });
					}
					else if(checksumAfter!=column.Value) {
						listTable.Add(new Difference { Kind=DifferenceKind.ColumnChanged,Table=tableBefore.Name,Column=column.Key,Before=column.Value,After=checksumAfter });
					}
				}
				foreach(string column in tableAfter.Columns.Keys.Where(x => !tableBefore.Columns.ContainsKey(x))) {
					listTable.Add(new Difference { Kind=DifferenceKind.ColumnAdded,Table=tableBefore.Name,Column=column });
				}
				if(tableBefore.ChecksumTable!=tableAfter.ChecksumTable) {
					Difference difference=new Difference { Kind=DifferenceKind.ChecksumTableChanged,Table=tableBefore.Name,Before=tableBefore.ChecksumTable,
						After=tableAfter.ChecksumTable };
					if(listTable.Count>0) {
						//CHECKSUM TABLE follows the row format; the column-level differences above already explain it.
						difference.IsAllowed=true;
						difference.Reason="explained by the differences listed for this table";
					}
					else {
						//Unexpected, and never allowed by a rule: the stored bytes differ while every value reads the same.
						difference.Note=NoteChecksumTableOnly;
					}
					listTable.Add(difference);
				}
				listDiffs.AddRange(listTable);
			}
			foreach(TableReport tableAfter in after.Tables.Where(x => !dictBefore.ContainsKey(x.Name))) {
				listDiffs.Add(new Difference { Kind=DifferenceKind.TableAdded,Table=tableAfter.Name,After=tableAfter.Rows+" rows" });
			}
			foreach(KeyValuePair<string,string> total in before.Totals) {
				if(!after.Totals.TryGetValue(total.Key,out string valueAfter)) {
					listDiffs.Add(new Difference { Kind=DifferenceKind.TotalRemoved,Name=total.Key,Before=total.Value });
				}
				else if(valueAfter!=total.Value) {
					listDiffs.Add(new Difference { Kind=DifferenceKind.TotalChanged,Name=total.Key,Before=total.Value,After=valueAfter });
				}
			}
			foreach(KeyValuePair<string,string> total in after.Totals.Where(x => !before.Totals.ContainsKey(x.Key))) {
				listDiffs.Add(new Difference { Kind=DifferenceKind.TotalAdded,Name=total.Key,After=total.Value });
			}
			if(allowed!=null) {
				foreach(Difference difference in listDiffs.Where(x => !x.IsAllowed)) {
					AllowedRule rule=allowed.Rules.FirstOrDefault(x => x.Matches(difference));
					if(rule!=null) {
						difference.IsAllowed=true;
						difference.Reason=rule.Reason;
					}
				}
			}
			return listDiffs;
		}

		public static string Format(List<Difference> listDiffs) {
			StringBuilder stringBuilder=new StringBuilder();
			List<Difference> listUnexpected=listDiffs.Where(x => !x.IsAllowed).ToList();
			List<Difference> listAllowed=listDiffs.Where(x => x.IsAllowed).ToList();
			stringBuilder.AppendLine("Unexpected differences: "+listUnexpected.Count);
			foreach(Difference difference in listUnexpected) {
				stringBuilder.AppendLine("  "+difference);
			}
			stringBuilder.AppendLine("Allowed differences: "+listAllowed.Count);
			foreach(Difference difference in listAllowed) {
				stringBuilder.AppendLine("  "+difference);
			}
			return stringBuilder.ToString();
		}
	}
}
