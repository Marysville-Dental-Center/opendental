// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using SyntheticData.Infrastructure;

namespace SyntheticData.Validation {
	///<summary>The tables and columns that are new or changed between Open Dental 24.3 and 25.3.48 (docs/specs/schema-changes.json,
	///range 24.3-25.3, plus the office-only patient._portal_active_email and the legacy deletedobject table the extractor missed).
	///"coverage" checks that every one of them holds at least one row with a non-default value, and that every widened column holds a
	///value longer than its old width, so the adoption rehearsal exercises them. The tests keep these lists in step with schema-changes.json.</summary>
	public static class Coverage {
		public static readonly string[] NewTables={
			"feeschednote","queryfilter","branding","inspending","chat","chatmsg","chatattach","chatreaction","chatuserattach","chatuserod",
			"sequencecounter","schoolapproval","schoolcoursedef","schoolcoursesched","deletedobject",
		};

		///<summary>New columns, renamed columns (task/taskhist.Category) and columns whose type changed, as table.column.</summary>
		public static readonly string[] NewOrChangedColumns={
			//24.4
			"document.ChartLetterStatus","document.UserNum","document.ChartLetterHash","fee.DateEffective","payplan.SheetDefNum",
			"payplantemplate.SheetDefNum","imagedraw.BetterDiagLayer",
			//25.1
			"codegroup.ShowInFrequency","computerpref.CreditCardTerminalId","creditcard.CardHolderName","eformfield.AutoImport",
			"eformfield.PrefillFromGuar","eformfielddef.AutoImport","eformfielddef.PrefillFromGuar","insverify.SecurityHash",
			"insverifyhist.SecurityHash","medication.IsHidden","payplancharge.IsDownPayment",
			//25.2
			"appointment.IsMirrored","apptview.ShowMirroredAppts","histappointment.IsMirrored","histappointment.ProcDescript",
			"clearinghouse.EnableXConnect","eclipboardsheetdef.SheetDefNumsConsidered","eformfield.ValueLabelEnglish","eformfield.PickListVisEnglish",
			"eobattach.ClaimNumPreAuth","insplan.GroupNum","inssub.SecurityHash","sheetfield.UserSigned","statement.ShowTransSinceBalZero",
			"task.Category","taskhist.Category",
			//25.3
			"asapcomm.UserNum","benefit.ToothRange","claimpayment.Note","codegroup.ShowInOther","eformfield.PickListVis","eformfield.PickListDb",
			"eformfield.ConditionalValue","eformfielddef.PickListVis","eformfielddef.PickListDb","eformfielddef.ConditionalValue",
			"emailmessage.FailReason","journalentry.Payee","journalentry.Notes","journalentry.NeedsReview","transaction.NeedsReview",
			"reqneeded.SchoolCourseDefNum","reqstudent.ProcNum","schoolcourse.DateStart","schoolcourse.DateEnd","schoolcourse.SchoolClassNum",
			"schoolcourse.GradingScaleNum","task.TriagePosition","taskhist.TriagePosition",
			//Office-specific generated column (dropped by adoption).
			"patient._portal_active_email",
		};

		///<summary>Columns the generator deliberately leaves empty, with the reason. Coverage reports them separately instead of failing.</summary>
		public static readonly Dictionary<string,string> DictKnownEmpty=new Dictionary<string,string>();

		///<summary>Columns widened between 24.3 and 25.3 (the "alter" entries of schema-changes.json), with their old maximum length in
		///characters. A value that merely exists proves nothing about a widening, so coverage requires the longest stored value to exceed the
		///old length: an adoption or fork migration step that redefines one of these columns at the old width would then truncate data,
		///and the rehearsal's checksums would show it.</summary>
		public static readonly Dictionary<string,int> DictWidenedOldLength=new Dictionary<string,int> {
			["histappointment.ProcDescript"]=255,
			["insplan.GroupNum"]=25,
			["claimpayment.Note"]=255,
			["eformfield.PickListVis"]=255,
			["eformfield.PickListDb"]=255,
			["eformfield.ConditionalValue"]=255,
			["eformfielddef.PickListVis"]=255,
			["eformfielddef.PickListDb"]=255,
			["eformfielddef.ConditionalValue"]=255,
			["emailmessage.FailReason"]=255,
		};

		///<summary>Widened columns that the reference schema (the office's 25.3.48 database, fox-db-schema.sql) still has at the old width,
		///so no longer value can be stored before adoption. For these, coverage requires the longest value to fill the old width exactly,
		///which still catches a step that redefines the column narrower.</summary>
		public static readonly Dictionary<string,string> DictWideningNotInReference=new Dictionary<string,string> {
			["claimpayment.Note"]="the office's 25.3.48 database still has varchar(255); Open Dental's 25.3 docs say text (DRIFT in schema-changes.json)",
		};

		///<summary>The shortest longest-value that proves a widened column's new width is in use.</summary>
		public static int RequiredMaxLength(string item) {
			int oldLength=DictWidenedOldLength[item];
			return DictWideningNotInReference.ContainsKey(item) ? oldLength : oldLength+1;
		}

		public class Result {
			public List<string> ListMissingTables=new List<string>();
			public List<string> ListEmptyTables=new List<string>();
			public List<string> ListMissingColumns=new List<string>();
			public List<string> ListUnfilledColumns=new List<string>();
			public List<string> ListKnownEmpty=new List<string>();
			///<summary>Widened columns whose longest value does not need the new width, as "table.column: longest N, needs at least M".</summary>
			public List<string> ListNotWidenedColumns=new List<string>();
			public Dictionary<string,long> DictCounts=new Dictionary<string,long>();
			///<summary>The longest stored value, in characters, of each widened column.</summary>
			public Dictionary<string,long> DictMaxLengths=new Dictionary<string,long>();

			public bool IsComplete {
				get {
					return ListMissingTables.Count==0 && ListEmptyTables.Count==0 && ListMissingColumns.Count==0 && ListUnfilledColumns.Count==0
						&& ListNotWidenedColumns.Count==0;
				}
			}

			public IEnumerable<string> AllProblems {
				get { return ListMissingTables.Concat(ListMissingColumns).Concat(ListEmptyTables).Concat(ListUnfilledColumns).Concat(ListNotWidenedColumns); }
			}
		}

		public static Result Check(Db db,string database) {
			db.Use(database);
			SchemaInfo schema=SchemaInfo.Load(db,database);
			Result result=new Result();
			foreach(string table in NewTables) {
				if(schema.GetTable(table)==null) {
					result.ListMissingTables.Add(table);
					continue;
				}
				long count=db.ScalarLong("SELECT COUNT(*) FROM `"+table+"`");
				result.DictCounts[table]=count;
				if(count==0) {
					result.ListEmptyTables.Add(table);
				}
			}
			foreach(string item in NewOrChangedColumns) {
				string table=item.Split('.')[0];
				string column=item.Split('.')[1];
				TableInfo tableInfo=schema.GetTable(table);
				if(tableInfo==null || !tableInfo.HasColumn(column)) {
					result.ListMissingColumns.Add(item);
					continue;
				}
				long count=db.ScalarLong("SELECT COUNT(*) FROM `"+table+"` WHERE NOT (`"+column+"` IS NULL OR CAST(`"+column+"` AS CHAR) IN "
					+"('','0','0001-01-01','0001-01-01 00:00:00','0000-00-00','0000-00-00 00:00:00'))");
				result.DictCounts[item]=count;
				if(DictWidenedOldLength.ContainsKey(item)) {
					long maxLength=db.ScalarLong("SELECT COALESCE(MAX(CHAR_LENGTH(`"+column+"`)),0) FROM `"+table+"`");
					result.DictMaxLengths[item]=maxLength;
					int required=RequiredMaxLength(item);
					if(maxLength<required) {
						result.ListNotWidenedColumns.Add(item+": longest "+maxLength+", needs at least "+required);
					}
				}
				if(count>0) {
					continue;
				}
				if(DictKnownEmpty.ContainsKey(item)) {
					result.ListKnownEmpty.Add(item);
				}
				else {
					result.ListUnfilledColumns.Add(item);
				}
			}
			return result;
		}

		public static IEnumerable<string> AllItems {
			get { return NewTables.Concat(NewOrChangedColumns); }
		}
	}
}
