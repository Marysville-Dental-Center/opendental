// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.Features;
using OpenDentBusiness.ForkMigrations.Seeds26_1;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>Checks, row by row against the original it was copied from, the tables and columns where the report comparison
	///(allowed-differences.adoption.json) can only allow whole-table or whole-column changes: preference, documentmisc, securitylog and
	///securityloghash, displayreport, grouppermission and updatehistory, the columns whose values the Open Dental 25.4 and 26.1 steps change, and the
	///tables and columns those steps add. Adoption may only:
	///- preference: add its markers and the fork migration's, add the 25.4 and 26.1 preferences the original lacks with their defaults (what
	///  migration-preview lists), set DataBaseVersion and ProgramVersion to the sentinel, and blank preferences that held an address of Open Dental's
	///  servers; no other row may change, and no row may go;
	///- documentmisc: delete Open Dental's UpdateFiles and UpdateFilesSegment rows, and nothing else;
	///- securitylog and securityloghash: add adoption's and the rehash's entries; no existing row may change or go;
	///- displayreport: rename the rows that the fork migration's report rows step renames (ReportRows25_3.PlanRenames on the original's rows, what
	///  migration-preview lists), InternalName only and only to this program's name for that report; and what the UDS+ report row step plans
	///  (UdsPlusRowPlan): add the UDS+ row with the planned values and move the later Public Health rows down one place (ItemOrder only), or rename
	///  the one row under another name with a UDS+ description (InternalName only); no other row or column may change, no other row may be added, and
	///  no row may go;
	///- grouppermission: the 25.4 blockout permission split that migration-preview lists (Migration26_1Plan): the Open Dental 25.3 PhoneExtension
	///  grants move from 272 to 274 (PermType only), and each planned BlockoutAdd and BlockoutDelete grant is added as a copy of its Blockouts grant;
	///  and the UDS+ step's report permissions: one Reports grant for the added row (FKey its DisplayReportNum, no date limit) per planned user group;
	///  no other row may change, be added or go;
	///- claimpayment.Note: only NULL notes become empty strings, as many as migration-preview counts; insverify and insverifyhist.DateLastAssigned: every
	///  value is the original date at midnight;
	///- the tables the 25.4 and 26.1 steps add stay empty, and the columns they add hold the column's default in every row;
	///- updatehistory: nothing.
	///Read-only; it names rows, preferences, permissions, columns and report names, never other values. The original must be the unchanged database the
	///copy was made from (runbook step 3.3 showed they were equal, and step 10.2 shows the original did not change).</summary>
	public static class AdoptionRowCheck {
		///<summary>Preferences adoption and the fork migration add.</summary>
		public static readonly string[] ArrayAddedPrefNames={ ForkVersions.PREF_FORK_ADOPTED_FROM,ForkVersions.PREF_OD_SCHEMA_COMPAT,
			ForkVersions.PREF_FORK_SCHEMA_LEVEL,ForkVersions.PREF_FORK_PROGRAM_VERSION,ForkMigrationRunner.PrefNameApplied,ForkMigrationRunner.PrefNameInProgress };
		///<summary>The tables allowed-differences.adoption.json may allow whole ("Kind": "*"): this check compares each row by row.</summary>
		public static readonly string[] ArrayWholeTables={ "preference","documentmisc","securitylog","securityloghash","displayreport","grouppermission" };
		///<summary>The start of the security log entries adoption and the rehash write.</summary>
		public static readonly string[] ArrayLogTexts={ "Database adopted from Open Dental ",HeadlessAdoption.REHASH_LOG_TEXT };

		public class Result {
			public List<string> ListLines=new List<string>();
			public List<string> ListFailures=new List<string>();
			public List<string> ListPrefsAdded=new List<string>();
			///<summary>The 25.4 and 26.1 preferences the seed step added, each with its default, all of them planned.</summary>
			public List<string> ListPrefsSeeded=new List<string>();
			public List<string> ListPrefsSentinel=new List<string>();
			public List<string> ListPrefsBlanked=new List<string>();
			public long CountUpdateFileRowsDeleted;
			public long CountLogEntriesAdded;
			///<summary>The displayreport renames found, each "old name to new name (DisplayReportNum n)", all of them planned.</summary>
			public List<string> ListReportRenames=new List<string>();
			///<summary>The plan made from the original (what migration-preview lists for it).</summary>
			public Migration26_1Plan.Plan Plan26_1;
			///<summary>The UDS+ report row step's plan made from the original (what migration-preview lists for it).</summary>
			public UdsPlusRowPlan.Plan PlanUdsPlus;
			///<summary>The DisplayReportNum of the UDS+ row the step added, when it was planned and found with the planned values; 0 otherwise.</summary>
			public long DisplayReportNumAdded;
			///<summary>Public Health rows the UDS+ step moved down one place, all of them planned.</summary>
			public int CountReportRowsMoved;
			///<summary>Reports grants the UDS+ step added for its row, all of them planned.</summary>
			public int CountReportPermsCopied;
			///<summary>Open Dental 25.3 PhoneExtension grants moved from 272 to 274, all of them planned.</summary>
			public int CountPermsRenumbered;
			///<summary>Planned grants found, per permission ("BlockoutAdd" to the number of rows added).</summary>
			public SortedDictionary<string,int> DictPermsGranted=new SortedDictionary<string,int>(StringComparer.Ordinal);
			///<summary>claimpayment notes that were NULL and are now empty.</summary>
			public long CountNotesBlanked;
			///<summary>The new columns checked (table.column), each holding its default in every row.</summary>
			public List<string> ListColumnsAddedChecked=new List<string>();
			///<summary>The new tables, each empty.</summary>
			public List<string> ListTablesAdded=new List<string>();

			public bool Passed {
				get { return ListFailures.Count==0; }
			}
		}

		public static Result Run(Db db,string original,string adopted) {
			LocalOnly.EnsureSafeIdentifier(original);
			LocalOnly.EnsureSafeIdentifier(adopted);
			Result result=new Result();
			result.Plan26_1=Migration26_1Plan.Make(db,original);
			List<ReportRows25_3.DisplayReportRow> listRowsOriginal=MigrationPreview.ReadRows(db,original);
			result.PlanUdsPlus=UdsPlusRowPlan.Make(db,original,listRowsOriginal,ReportRows25_3.PlanRenames(listRowsOriginal),result.Plan26_1.IsFromBefore25_4);
			CheckPreferences(db,original,adopted,result);
			CheckDocumentMisc(db,original,adopted,result);
			List<long> listLogAdded=CheckSecurityLog(db,original,adopted,result);
			CheckSecurityLogHash(db,original,adopted,listLogAdded,result);
			CheckDisplayReport(db,original,adopted,result);
			CheckGroupPermission(db,original,adopted,result);
			CheckValueChangingColumns(db,original,adopted,result);
			CheckNewTablesAndColumns(db,original,adopted,result);
			RowComparer.TableDiff diffHistory=RowComparer.Compare(db,original,adopted,"updatehistory");
			if(diffHistory.IsIdentical) {
				result.ListLines.Add("updatehistory: identical row by row.");
			}
			else {
				result.ListFailures.Add("updatehistory changed: "+diffHistory.Describe());
			}
			return result;
		}

		private static void CheckPreferences(Db db,string original,string adopted,Result result) {
			RowComparer.TableDiff diff=RowComparer.Compare(db,original,adopted,"preference");
			if(diff.ListColumnsInOneOnly.Count>0) {
				result.ListFailures.Add("preference columns differ: "+string.Join(", ",diff.ListColumnsInOneOnly));
			}
			if(diff.CountOnlyFirst>0) {
				result.ListFailures.Add(diff.CountOnlyFirst+" preference row(s) of "+original+" are missing (PrefNum "+string.Join(", ",diff.ListOnlyFirst.Take(20))+").");
			}
			if(diff.CountOnlySecond>MaxPrefRows || diff.CountChanged>MaxPrefRows) {
				result.ListFailures.Add("Too many preference rows added ("+diff.CountOnlySecond+") or changed ("+diff.CountChanged+").");
				return;
			}
			//The 25.4 and 26.1 preferences the original lacks, which the seed step inserts with their defaults (migration-preview lists them).
			Dictionary<string,string> dictSeedDefaults=Seed26_1.ListPrefDefaults
				.Where(x => result.Plan26_1.ListPrefsInserted.Contains(x.PrefName.ToString(),StringComparer.OrdinalIgnoreCase))
				.ToDictionary(x => x.PrefName.ToString(),x => x.ValueString,StringComparer.OrdinalIgnoreCase);
			foreach(long prefNum in diff.ListOnlySecond) {
				object[] rowAdded=db.Rows("SELECT PrefName,ValueString FROM `"+adopted+"`.preference WHERE PrefNum="+prefNum.ToString(CultureInfo.InvariantCulture))[0];
				string name=Convert.ToString(rowAdded[0]);
				if(ArrayAddedPrefNames.Contains(name,StringComparer.OrdinalIgnoreCase)) {
					result.ListPrefsAdded.Add(name);
				}
				else if(dictSeedDefaults.TryGetValue(name,out string valueDefault)) {
					if(string.Equals(Convert.ToString(rowAdded[1]),valueDefault,StringComparison.Ordinal)
						&& !result.ListPrefsSeeded.Contains(name,StringComparer.OrdinalIgnoreCase))
					{
						result.ListPrefsSeeded.Add(name);
					}
					else {
						result.ListFailures.Add("Preference "+name+" (PrefNum "+prefNum+") was added with a value other than its default, or twice; the fork migration's "
							+"seed step inserts it once, with its default.");
					}
				}
				else {
					result.ListFailures.Add("Preference "+name+" (PrefNum "+prefNum+") was added; adoption adds only "+string.Join(", ",ArrayAddedPrefNames)
						+" and the 25.4 and 26.1 preferences migration-preview lists for "+original+".");
				}
			}
			foreach(string name in dictSeedDefaults.Keys.Where(x => !result.ListPrefsSeeded.Contains(x,StringComparer.OrdinalIgnoreCase))) {
				result.ListFailures.Add("Preference "+name+" was not added, though migration-preview lists it for "+original+".");
			}
			foreach(RowComparer.ChangedRow changed in diff.ListChanged) {
				object[] rowOld=db.Rows("SELECT PrefName,ValueString FROM `"+original+"`.preference WHERE PrefNum="+changed.Key.ToString(CultureInfo.InvariantCulture))[0];
				object[] rowNew=db.Rows("SELECT PrefName,ValueString FROM `"+adopted+"`.preference WHERE PrefNum="+changed.Key.ToString(CultureInfo.InvariantCulture))[0];
				string name=Convert.ToString(rowOld[0]);
				string valueOld=Convert.ToString(rowOld[1]);
				string valueNew=Convert.ToString(rowNew[1]);
				if(changed.ListColumns.Count!=1 || !string.Equals(changed.ListColumns[0],"ValueString",StringComparison.OrdinalIgnoreCase)) {
					result.ListFailures.Add("Preference "+name+" (PrefNum "+changed.Key+"): "+string.Join(", ",changed.ListColumns)+" changed; adoption only changes ValueString.");
					continue;
				}
				if((name=="DataBaseVersion" || name=="ProgramVersion") && valueNew==ForkVersions.SentinelVersion.ToString(4)) {
					result.ListPrefsSentinel.Add(name);
					continue;
				}
				if(ArrayAddedPrefNames.Contains(name,StringComparer.OrdinalIgnoreCase)) {
					//A marker the original already had would have made adopt refuse it; listed rather than failed, so the check never hides it.
					result.ListPrefsAdded.Add(name+" (changed)");
					continue;
				}
				bool isHqPref=Adoption.ListHqUrlPrefNames.Contains(name,StringComparer.OrdinalIgnoreCase) || Adoption.IsHqWebAddress(valueOld);
				if(valueNew=="" && isHqPref) {
					result.ListPrefsBlanked.Add(name);
					continue;
				}
				result.ListFailures.Add("Preference "+name+" (PrefNum "+changed.Key+") changed; adoption only sets the version sentinels and blanks addresses of "
					+"Open Dental's servers.");
			}
			result.ListLines.Add("preference: "+result.ListPrefsAdded.Count+" added ("+string.Join(", ",result.ListPrefsAdded)+"); "
				+result.ListPrefsSeeded.Count+" of the "+dictSeedDefaults.Count+" 25.4 and 26.1 preference(s) migration-preview lists added with their defaults"
				+(result.ListPrefsSeeded.Count==0 ? "" : " ("+string.Join(", ",result.ListPrefsSeeded)+")")+"; "
				+result.ListPrefsSentinel.Count+" set to "+ForkVersions.SentinelVersion.ToString(4)+" ("+string.Join(", ",result.ListPrefsSentinel)+"); "
				+result.ListPrefsBlanked.Count+" blanked ("+string.Join(", ",result.ListPrefsBlanked)+"); no other row changed or removed"
				+(diff.CountOnlyFirst>0 || result.ListFailures.Count>0 ? " (see the failures)" : "")+".");
		}

		///<summary>More added or changed preference rows than this is not an adoption: the check stops listing them.</summary>
		private const int MaxPrefRows=RowComparer.MaxListed;

		private static void CheckDocumentMisc(Db db,string original,string adopted,Result result) {
			RowComparer.TableDiff diff=RowComparer.Compare(db,original,adopted,"documentmisc");
			if(diff.CountChanged>0 || diff.CountOnlySecond>0 || diff.ListColumnsInOneOnly.Count>0) {
				result.ListFailures.Add("documentmisc rows were added or changed: "+diff.Describe());
			}
			string updateTypes=((int)DocumentMiscType.UpdateFiles).ToString(CultureInfo.InvariantCulture)+","
				+((int)DocumentMiscType.UpdateFilesSegment).ToString(CultureInfo.InvariantCulture);
			long countOtherDeleted=db.ScalarLong("SELECT COUNT(*) FROM `"+original+"`.documentmisc a LEFT JOIN `"+adopted+"`.documentmisc b ON a.DocMiscNum=b.DocMiscNum "
				+"WHERE b.DocMiscNum IS NULL AND a.DocMiscType NOT IN ("+updateTypes+")");
			long countUpdateLeft=db.ScalarLong("SELECT COUNT(*) FROM `"+adopted+"`.documentmisc WHERE DocMiscType IN ("+updateTypes+")");
			result.CountUpdateFileRowsDeleted=diff.CountOnlyFirst-countOtherDeleted;
			if(countOtherDeleted>0) {
				result.ListFailures.Add(countOtherDeleted+" documentmisc row(s) that are not Open Dental update files were deleted.");
			}
			if(countUpdateLeft>0) {
				result.ListFailures.Add(countUpdateLeft+" Open Dental update-file row(s) are still in documentmisc.");
			}
			result.ListLines.Add("documentmisc: "+result.CountUpdateFileRowsDeleted+" Open Dental update-file row(s) deleted, none left; "+countOtherDeleted
				+" other row(s) deleted; "+diff.CountOnlySecond+" added; "+diff.CountChanged+" changed.");
		}

		///<summary>Returns the SecurityLogNum of every row the copy has and the original does not.</summary>
		private static List<long> CheckSecurityLog(Db db,string original,string adopted,Result result) {
			RowComparer.TableDiff diff=RowComparer.Compare(db,original,adopted,"securitylog");
			if(diff.CountOnlyFirst>0 || diff.CountChanged>0 || diff.ListColumnsInOneOnly.Count>0) {
				result.ListFailures.Add("Existing securitylog rows were removed or changed: "+diff.Describe());
			}
			result.CountLogEntriesAdded=diff.CountOnlySecond;
			if(diff.CountOnlySecond>RowComparer.MaxListed) {
				result.ListFailures.Add(diff.CountOnlySecond+" securitylog rows were added; adoption and the rehash add one each per run.");
				return diff.ListOnlySecond;
			}
			List<string> listKinds=new List<string>();
			foreach(long securityLogNum in diff.ListOnlySecond) {
				string logText=Convert.ToString(db.Scalar("SELECT LogText FROM `"+adopted+"`.securitylog WHERE SecurityLogNum="
					+securityLogNum.ToString(CultureInfo.InvariantCulture)))??"";
				string kind=logText.StartsWith(ArrayLogTexts[0],StringComparison.Ordinal) ? "adoption"
					: logText.StartsWith(ArrayLogTexts[1],StringComparison.Ordinal) ? "rehash" : null;
				if(kind==null) {
					//The text is not printed: it could name a patient.
					result.ListFailures.Add("securitylog row "+securityLogNum+" was added and is neither adoption's nor the rehash's entry.");
					continue;
				}
				listKinds.Add(kind);
			}
			result.ListLines.Add("securitylog: "+diff.CountOnlySecond+" entr"+(diff.CountOnlySecond==1 ? "y" : "ies")+" added ("
				+string.Join(", ",listKinds.GroupBy(x => x).Select(x => x.Count()+" "+x.Key))+"); no existing row changed or removed.");
			return diff.ListOnlySecond;
		}

		private static void CheckSecurityLogHash(Db db,string original,string adopted,List<long> listLogAdded,Result result) {
			RowComparer.TableDiff diff=RowComparer.Compare(db,original,adopted,"securityloghash");
			if(diff.CountOnlyFirst>0 || diff.CountChanged>0 || diff.ListColumnsInOneOnly.Count>0) {
				result.ListFailures.Add("Existing securityloghash rows were removed or changed: "+diff.Describe());
			}
			long countOther=0;
			if(diff.CountOnlySecond>0) {
				string inAdded=listLogAdded.Count==0 ? "0" : string.Join(",",listLogAdded.Select(x => x.ToString(CultureInfo.InvariantCulture)));
				string inNew=string.Join(",",diff.ListOnlySecond.Select(x => x.ToString(CultureInfo.InvariantCulture)));
				countOther=db.ScalarLong("SELECT COUNT(*) FROM `"+adopted+"`.securityloghash WHERE SecurityLogHashNum IN ("+inNew+") AND SecurityLogNum NOT IN ("+inAdded+")");
				if(diff.CountOnlySecond>RowComparer.MaxListed) {
					countOther=Math.Max(countOther,diff.CountOnlySecond-RowComparer.MaxListed);
				}
			}
			if(countOther>0) {
				result.ListFailures.Add(countOther+" securityloghash row(s) were added for security log entries adoption did not write.");
			}
			result.ListLines.Add("securityloghash: "+diff.CountOnlySecond+" added, each for an added security log entry"+(countOther>0 ? " except "+countOther : "")
				+"; no existing row changed or removed.");
		}

		///<summary>What the fork migration may change in one existing displayreport row: its InternalName (a planned rename), its ItemOrder (a Public Health
		///row the UDS+ step moves down one place), or both. Null means unchanged.</summary>
		private class ReportRowExpected {
			public string InternalNameOld;
			public string InternalNameNew;
			public int? ItemOrderNew;

			public List<string> Columns() {
				List<string> listColumns=new List<string>();
				if(InternalNameNew!=null) {
					listColumns.Add("InternalName");
				}
				if(ItemOrderNew!=null) {
					listColumns.Add("ItemOrder");
				}
				return listColumns;
			}
		}

		///<summary>The fork migration's report rows step renames, on a database Open Dental converted, the rows ReportRows25_3.PlanRenames finds in the
		///original (what migration-preview lists before the day); its UDS+ report row step adds the UDS+ row and moves the later Public Health rows down
		///one place, or renames a row under another name, as UdsPlusRowPlan plans it from the original. Nothing else in displayreport may change.
		///</summary>
		private static void CheckDisplayReport(Db db,string original,string adopted,Result result) {
			bool hasOriginal=MigrationPreview.HasTable(db,original);
			bool hasAdopted=MigrationPreview.HasTable(db,adopted);
			if(!hasOriginal || !hasAdopted) {
				if(hasOriginal || hasAdopted) {
					result.ListFailures.Add("displayreport is missing from "+(hasOriginal ? adopted : original)+".");
				}
				result.ListLines.Add("displayreport: no such table"+(hasOriginal==hasAdopted ? " in either database." : " in one of them (see the failures)."));
				return;
			}
			List<ReportRows25_3.ReportTargetPlan> listPlanned=ReportRows25_3.PlanRenames(MigrationPreview.ReadRows(db,original)).Where(x => x.RowRenamed!=null).ToList();
			ReportRowUdsPlus.RowPlan planUds=result.PlanUdsPlus.Rows;
			//Every planned change of an existing row, by DisplayReportNum.
			Dictionary<long,ReportRowExpected> dictExpected=new Dictionary<long,ReportRowExpected>();
			ReportRowExpected GetExpected(ReportRows25_3.DisplayReportRow row) {
				if(!dictExpected.TryGetValue(row.DisplayReportNum,out ReportRowExpected expected)) {
					expected=new ReportRowExpected { InternalNameOld=row.InternalName };
					dictExpected[row.DisplayReportNum]=expected;
				}
				return expected;
			}
			foreach(ReportRows25_3.ReportTargetPlan plan in listPlanned) {
				GetExpected(plan.RowRenamed).InternalNameNew=plan.Target.InternalName;
			}
			if(planUds.RowRenamed!=null) {
				GetExpected(planUds.RowRenamed).InternalNameNew=DisplayReports.ReportNames.UdsPlus;
			}
			foreach(ReportRows25_3.DisplayReportRow row in planUds.ListRowsMoved) {
				GetExpected(row).ItemOrderNew=row.ItemOrder+1;
			}
			int countRenamesPlanned=dictExpected.Values.Count(x => x.InternalNameNew!=null);
			int countAddedPlanned=planUds.IsAdded ? 1 : 0;
			RowComparer.TableDiff diff=RowComparer.Compare(db,original,adopted,"displayreport");
			if(diff.ListColumnsInOneOnly.Count>0) {
				result.ListFailures.Add("displayreport columns differ: "+string.Join(", ",diff.ListColumnsInOneOnly));
			}
			if(diff.CountOnlyFirst>0 || diff.CountOnlySecond!=countAddedPlanned) {
				result.ListFailures.Add("displayreport rows were removed or added: "+diff.Describe()+". On an Open Dental 25.3.48 database the fork migration only renames, "
					+"and adds "+(planUds.IsAdded ? "the one UDS+ report row migration-preview lists." : "no row (migration-preview lists no UDS+ report row to add)."));
			}
			else if(planUds.IsAdded) {
				CheckUdsPlusRowAdded(db,adopted,diff.ListOnlySecond[0],planUds,result);
			}
			if(diff.CountChanged>diff.ListChanged.Count) {
				result.ListFailures.Add(diff.CountChanged+" displayreport rows changed; the fork migration changes "+dictExpected.Count+" on this database.");
			}
			int countFailuresBefore=result.ListFailures.Count;
			foreach(RowComparer.ChangedRow changed in diff.ListChanged) {
				if(!dictExpected.TryGetValue(changed.Key,out ReportRowExpected expected)) {
					result.ListFailures.Add("displayreport row "+changed.Key+" changed ("+string.Join(", ",changed.ListColumns)+"); the fork migration renames only the rows "
						+"migration-preview lists for "+original+", and moves only the Public Health rows it lists.");
					continue;
				}
				List<string> listColumnsExpected=expected.Columns();
				if(changed.ListColumns.Count!=listColumnsExpected.Count
					|| !changed.ListColumns.All(x => listColumnsExpected.Contains(x,StringComparer.OrdinalIgnoreCase)))
				{
					result.ListFailures.Add("displayreport row "+changed.Key+": "+string.Join(", ",changed.ListColumns)+" changed; the fork migration changes only "
						+string.Join(" and ",listColumnsExpected)+" there.");
					continue;
				}
				object[] rowNew=db.Rows("SELECT InternalName,ItemOrder FROM `"+adopted+"`.displayreport WHERE DisplayReportNum="
					+changed.Key.ToString(CultureInfo.InvariantCulture))[0];
				string nameNew=Convert.ToString(rowNew[0],CultureInfo.InvariantCulture);
				int itemOrderNew=Convert.ToInt32(rowNew[1],CultureInfo.InvariantCulture);
				bool isAsPlanned=true;
				if(expected.InternalNameNew!=null && nameNew!=expected.InternalNameNew) {
					result.ListFailures.Add("displayreport row "+changed.Key+" was renamed from "+expected.InternalNameOld+" to "+nameNew+", not to "+expected.InternalNameNew+".");
					isAsPlanned=false;
				}
				if(expected.ItemOrderNew!=null && itemOrderNew!=expected.ItemOrderNew.Value) {
					result.ListFailures.Add("displayreport row "+changed.Key+" has ItemOrder "+itemOrderNew+", not "+expected.ItemOrderNew.Value+" (one place down, below the "
						+"UDS+ report row).");
					isAsPlanned=false;
				}
				if(!isAsPlanned) {
					continue;
				}
				if(expected.InternalNameNew!=null) {
					result.ListReportRenames.Add(expected.InternalNameOld+" to "+expected.InternalNameNew+" (DisplayReportNum "+changed.Key+")");
				}
				if(expected.ItemOrderNew!=null) {
					result.CountReportRowsMoved++;
				}
			}
			foreach(KeyValuePair<long,ReportRowExpected> pair in dictExpected.Where(x => !diff.ListChanged.Any(y => y.Key==x.Key))) {
				if(pair.Value.InternalNameNew!=null) {
					result.ListFailures.Add("displayreport row "+pair.Key+" ("+pair.Value.InternalNameOld+") was not renamed to "+pair.Value.InternalNameNew
						+", though the fork migration renames it.");
				}
				if(pair.Value.ItemOrderNew!=null) {
					result.ListFailures.Add("displayreport row "+pair.Key+" ("+pair.Value.InternalNameOld+") was not moved down to ItemOrder "+pair.Value.ItemOrderNew.Value
						+", though the fork migration moves it below the UDS+ report row.");
				}
			}
			bool isClean=result.ListFailures.Count==countFailuresBefore && diff.CountOnlyFirst==0 && diff.CountOnlySecond==countAddedPlanned
				&& diff.ListColumnsInOneOnly.Count==0 && diff.CountChanged==diff.ListChanged.Count && (!planUds.IsAdded || result.DisplayReportNumAdded>0);
			if(isClean && dictExpected.Count==0 && countAddedPlanned==0) {
				result.ListLines.Add("displayreport: identical row by row (migration-preview lists no change).");
				return;
			}
			result.ListLines.Add("displayreport: "+result.ListReportRenames.Count+" of the "+countRenamesPlanned+" rename(s) migration-preview lists, InternalName only"
				+(result.ListReportRenames.Count==0 ? "" : " ("+string.Join("; ",result.ListReportRenames)+")")
				+(!planUds.IsAdded ? "" : "; the UDS+ report row "+(result.DisplayReportNumAdded>0 ? "added as migration-preview lists ("+DisplayReports.ReportNames.UdsPlus
					+", DisplayReportNum "+result.DisplayReportNumAdded+", ItemOrder "+planUds.ItemOrder+")" : "not added as migration-preview lists")
					+", "+result.CountReportRowsMoved+" of the "+planUds.ListRowsMoved.Count+" later Public Health row(s) moved down one place, ItemOrder only")
				+(isClean ? "; no other row or column changed, "+(planUds.IsAdded ? "no other row added" : "none added")+", none removed." : " (see the failures)."));
		}

		///<summary>The row the UDS+ step added must hold exactly the planned values. Sets DisplayReportNumAdded when it does.</summary>
		private static void CheckUdsPlusRowAdded(Db db,string adopted,long displayReportNum,ReportRowUdsPlus.RowPlan planUds,Result result) {
			object[] row=db.Rows("SELECT InternalName,ItemOrder,Description,Category,IsHidden,IsVisibleInSubMenu FROM `"+adopted+"`.displayreport WHERE DisplayReportNum="
				+displayReportNum.ToString(CultureInfo.InvariantCulture))[0];
			List<string> listWrong=new List<string>();
			void Compare(string column,object value,string expected) {
				string actual=value is bool isTrue ? (isTrue ? "1" : "0") : Convert.ToString(value,CultureInfo.InvariantCulture);
				if(actual!=expected) {
					listWrong.Add(column+" "+actual+" (planned: "+expected+")");
				}
			}
			Compare("InternalName",row[0],DisplayReports.ReportNames.UdsPlus);
			Compare("ItemOrder",row[1],planUds.ItemOrder.ToString(CultureInfo.InvariantCulture));
			Compare("Description",row[2],ReportRowUdsPlus.Description);
			Compare("Category",row[3],((int)DisplayReportCategory.PublicHealth).ToString(CultureInfo.InvariantCulture));
			Compare("IsHidden",row[4],planUds.IsHidden ? "1" : "0");
			Compare("IsVisibleInSubMenu",row[5],"0");
			if(listWrong.Count>0) {
				result.ListFailures.Add("displayreport row "+displayReportNum+" was added with "+string.Join(", ",listWrong)+"; the fork migration adds only the UDS+ "
					+"report row migration-preview lists.");
				return;
			}
			result.DisplayReportNumAdded=displayReportNum;
		}

		///<summary>The 25.4 blockout permission split of the seed step (Seed26_1Step), as migration-preview plans it from the original
		///(Migration26_1Plan): the Open Dental 25.3 PhoneExtension rows (272) move to 274, PermType only, and each planned BlockoutAdd and BlockoutDelete
		///grant is one added row, a copy of its Blockouts row (user group, FKey, date limits) with the new PermType. Nothing else in grouppermission may
		///change, be added or go.</summary>
		private static void CheckGroupPermission(Db db,string original,string adopted,Result result) {
			bool hasOriginal=Migration26_1Plan.HasTable(db,original,"grouppermission");
			bool hasAdopted=Migration26_1Plan.HasTable(db,adopted,"grouppermission");
			if(!hasOriginal || !hasAdopted) {
				if(hasOriginal || hasAdopted) {
					result.ListFailures.Add("grouppermission is missing from "+(hasOriginal ? adopted : original)+".");
				}
				result.ListLines.Add("grouppermission: no such table"+(hasOriginal==hasAdopted ? " in either database." : " in one of them (see the failures)."));
				return;
			}
			Migration26_1Plan.Plan plan=result.Plan26_1;
			int countFailuresBefore=result.ListFailures.Count;
			RowComparer.TableDiff diff=RowComparer.Compare(db,original,adopted,"grouppermission");
			if(diff.ListColumnsInOneOnly.Count>0) {
				result.ListFailures.Add("grouppermission columns differ: "+string.Join(", ",diff.ListColumnsInOneOnly));
			}
			if(diff.CountOnlyFirst>0) {
				result.ListFailures.Add(diff.CountOnlyFirst+" grouppermission row(s) of "+original+" are missing (GroupPermNum "+string.Join(", ",diff.ListOnlyFirst.Take(20))+").");
			}
			if(diff.CountChanged>diff.ListChanged.Count) {
				result.ListFailures.Add(diff.CountChanged+" grouppermission rows changed; the fork migration moves "+plan.ListRenumbered.Count+".");
			}
			HashSet<long> setRenumbered=new HashSet<long>(plan.ListRenumbered.Select(x => x.GroupPermNum));
			foreach(RowComparer.ChangedRow changed in diff.ListChanged) {
				if(!setRenumbered.Contains(changed.Key)) {
					result.ListFailures.Add("grouppermission row "+changed.Key+" changed ("+string.Join(", ",changed.ListColumns)+"); the fork migration moves only the Open "
						+"Dental 25.3 PhoneExtension grants migration-preview lists for "+original+".");
					continue;
				}
				int permTypeNew=Convert.ToInt32(db.Scalar("SELECT PermType FROM `"+adopted+"`.grouppermission WHERE GroupPermNum="
					+changed.Key.ToString(CultureInfo.InvariantCulture)),CultureInfo.InvariantCulture);
				if(changed.ListColumns.Count!=1 || !string.Equals(changed.ListColumns[0],"PermType",StringComparison.OrdinalIgnoreCase)
					|| permTypeNew!=(int)EnumPermType.PhoneExtension)
				{
					result.ListFailures.Add("grouppermission row "+changed.Key+": "+string.Join(", ",changed.ListColumns)+" changed (PermType now "+permTypeNew+"); the fork "
						+"migration changes only PermType, from "+Seed26_1.PERM_PHONE_EXTENSION_25_3+" to "+(int)EnumPermType.PhoneExtension+".");
					continue;
				}
				result.CountPermsRenumbered++;
			}
			foreach(Migration26_1Plan.GroupPermRow row in plan.ListRenumbered.Where(x => !diff.ListChanged.Any(y => y.Key==x.GroupPermNum))) {
				result.ListFailures.Add("grouppermission row "+row.GroupPermNum+" (Open Dental 25.3 PhoneExtension, "+Seed26_1.PERM_PHONE_EXTENSION_25_3+") was not moved to "
					+(int)EnumPermType.PhoneExtension+", though the fork migration moves it.");
			}
			//The added rows must be exactly the planned grants.
			List<(string describe,string permName)> listExpected=plan.ListGrants.Where(x => !x.IsAlreadyConfigured)
				.SelectMany(x => x.ListSources.Select(y => (y.Describe((int)x.PermTypeNew),x.PermTypeNew.ToString()))).ToList();
			//The UDS+ report row step's grants: Reports for the row it added (FKey its DisplayReportNum, no date limit), to each planned user group that
			//has no such grant yet. When the row was not found as planned, CheckDisplayReport has failed already and none of them is expected.
			List<long> listGroupsUds=new List<long>();
			if(result.PlanUdsPlus!=null && result.PlanUdsPlus.Rows.IsAdded && result.DisplayReportNumAdded>0) {
				HashSet<long> setGroupsAlready=new HashSet<long>(UdsPlusRowPlan.ReadReportGroups(db,original,result.DisplayReportNumAdded));
				listGroupsUds=result.PlanUdsPlus.ListUserGroupsGranted.Where(x => !setGroupsAlready.Contains(x)).ToList();
				listExpected.AddRange(listGroupsUds.Select(x => (new Migration26_1Plan.GroupPermRow { UserGroupNum=x,FKey=result.DisplayReportNumAdded,
					NewerDate=DateTime.MinValue,NewerDays=0 }.Describe((int)EnumPermType.Reports),PermNameUdsPlusReports)));
			}
			int countExpected=listExpected.Count;
			if(diff.CountOnlySecond>diff.ListOnlySecond.Count) {
				result.ListFailures.Add(diff.CountOnlySecond+" grouppermission rows were added; the fork migration adds "+countExpected+".");
			}
			else {
				if(diff.CountOnlySecond>0) {
					string inAdded=string.Join(",",diff.ListOnlySecond.Select(x => x.ToString(CultureInfo.InvariantCulture)));
					foreach(Migration26_1Plan.GroupPermRow row in db.Rows("SELECT GroupPermNum,UserGroupNum,PermType,FKey,NewerDate,NewerDays FROM `"+adopted
						+"`.grouppermission WHERE GroupPermNum IN ("+inAdded+") ORDER BY GroupPermNum").Select(Migration26_1Plan.ToGroupPermRow))
					{
						string describe=row.Describe(row.PermType);
						int index=listExpected.FindIndex(x => x.describe==describe);
						if(index<0) {
							result.ListFailures.Add("grouppermission row "+row.GroupPermNum+" ("+describe+") was added; the fork migration adds only the BlockoutAdd and "
								+"BlockoutDelete grants and the UDS+ report's Reports grants migration-preview lists for "+original+".");
							continue;
						}
						string permName=listExpected[index].permName;
						if(permName==PermNameUdsPlusReports) {
							result.CountReportPermsCopied++;
						}
						else {
							result.DictPermsGranted[permName]=(result.DictPermsGranted.TryGetValue(permName,out int countSoFar) ? countSoFar : 0)+1;
						}
						listExpected.RemoveAt(index);
					}
				}
				foreach((string describe,string permName) expected in listExpected) {
					result.ListFailures.Add("No grouppermission row was added for the planned grant ("+expected.describe+").");
				}
			}
			bool isClean=result.ListFailures.Count==countFailuresBefore;
			if(isClean && plan.ListRenumbered.Count==0 && countExpected==0) {
				result.ListLines.Add("grouppermission: identical row by row (migration-preview lists no permission change).");
				return;
			}
			result.ListLines.Add("grouppermission: "+result.CountPermsRenumbered+" of the "+plan.ListRenumbered.Count+" Open Dental 25.3 PhoneExtension grant(s) moved from "
				+Seed26_1.PERM_PHONE_EXTENSION_25_3+" to "+(int)EnumPermType.PhoneExtension+"; "
				+string.Join(", ",plan.ListGrants.Select(x => x.PermTypeNew+" granted to "+(result.DictPermsGranted.TryGetValue(x.PermTypeNew.ToString(),out int count) ? count : 0)
					+" of "+x.CountGranted+" user group(s)"+(x.IsAlreadyConfigured ? " (already configured)" : "")))
				+", as migration-preview lists, each a copy of the group's "+EnumPermType.Blockouts+" grant"
				+(result.PlanUdsPlus==null || !result.PlanUdsPlus.Rows.IsAdded ? "" : "; "+EnumPermType.Reports+" for the UDS+ report row granted to "
					+result.CountReportPermsCopied+" of the "+listGroupsUds.Count+" user group(s) that may run the FQHC Dental Sealant Measure, as migration-preview lists")
				+(isClean ? "; no other row changed, added or removed." : " (see the failures)."));
		}

		///<summary>The name under which the expected grants list the UDS+ report row step's Reports grants (not a permission the seed step grants).</summary>
		private const string PermNameUdsPlusReports="UdsPlusReports";

		///<summary>The columns whose stored values the 25.4 and 26.1 steps change with their definition (Migration26_1Plan.ArrayValueChangingColumns):
		///claimpayment.Note only from NULL to an empty string, as many as migration-preview counts; insverify and insverifyhist.DateLastAssigned only from a
		///date to that date at midnight.</summary>
		private static void CheckValueChangingColumns(Db db,string original,string adopted,Result result) {
			List<string> listParts=new List<string>();
			foreach(string item in Migration26_1Plan.ArrayValueChangingColumns) {
				string table=item.Split('.')[0];
				string column=item.Split('.')[1];
				bool hasOriginal=ColumnExists(db,original,table,column);
				bool hasAdopted=ColumnExists(db,adopted,table,column);
				if(!hasOriginal || !hasAdopted) {
					if(hasOriginal || hasAdopted) {
						result.ListFailures.Add(item+" is missing from "+(hasOriginal ? adopted : original)+".");
					}
					listParts.Add(item+": not in "+(hasOriginal==hasAdopted ? "either database" : "one of them"));
					continue;
				}
				string key=RowComparer.KeyColumn(db,original,table);
				string join="FROM `"+original+"`.`"+table+"` a INNER JOIN `"+adopted+"`.`"+table+"` b ON a.`"+key+"`=b.`"+key+"`";
				string a="a.`"+column+"`";
				string b="b.`"+column+"`";
				if(item=="claimpayment.Note") {
					long countOther=db.ScalarLong("SELECT COUNT(*) "+join+" WHERE NOT (BINARY "+a+" <=> BINARY "+b+") AND NOT ("+a+" IS NULL AND BINARY "+b+"='')");
					result.CountNotesBlanked=db.ScalarLong("SELECT COUNT(*) "+join+" WHERE "+a+" IS NULL AND BINARY "+b+"=''");
					long countNullLeft=db.ScalarLong("SELECT COUNT(*) FROM `"+adopted+"`.`"+table+"` WHERE `"+column+"` IS NULL");
					if(countOther>0) {
						result.ListFailures.Add(countOther+" claimpayment note(s) changed; the fork migration only makes a NULL note empty.");
					}
					if(countNullLeft>0) {
						result.ListFailures.Add(countNullLeft+" claimpayment note(s) are still NULL; the 26.1 column is NOT NULL.");
					}
					if(result.CountNotesBlanked!=result.Plan26_1.CountClaimPaymentNotesNull) {
						result.ListFailures.Add(result.CountNotesBlanked+" NULL claimpayment note(s) became empty; migration-preview counts "
							+result.Plan26_1.CountClaimPaymentNotesNull+" in "+original+".");
					}
					listParts.Add(item+": "+result.CountNotesBlanked+" of the "+result.Plan26_1.CountClaimPaymentNotesNull+" NULL note(s) migration-preview counts now empty, "
						+(countOther==0 ? "every other note unchanged" : countOther+" other note(s) changed (see the failures)"));
					continue;
				}
				long countRows=db.ScalarLong("SELECT COUNT(*) "+join);
				long countMoved=db.ScalarLong("SELECT COUNT(*) "+join+" WHERE NOT (CAST("+a+" AS DATETIME) <=> CAST("+b+" AS DATETIME))");
				if(countMoved>0) {
					result.ListFailures.Add(countMoved+" "+item+" value(s) are not the original date at midnight.");
				}
				listParts.Add(item+": "+(countMoved==0 ? "the original date at midnight in all "+countRows+" row(s)" : countMoved+" of "+countRows+" row(s) differ (see the failures)"));
			}
			result.ListLines.Add("Changed columns: "+string.Join("; ",listParts)+".");
		}

		///<summary>The tables the copy has and the original lacks must be empty, and every column the copy's tables have and the original's lack must hold
		///the column's default in every row: the 25.4 and 26.1 schema steps add them and write nothing into them (evaluation.GradeOverride's default is
		///-1, the "no override" back-fill).</summary>
		private static void CheckNewTablesAndColumns(Db db,string original,string adopted,Result result) {
			int countFailuresBefore=result.ListFailures.Count;
			HashSet<string> setTablesOriginal=new HashSet<string>(TableNames(db,original),StringComparer.OrdinalIgnoreCase);
			int countTablesWithRows=0;
			foreach(string table in TableNames(db,adopted).Where(x => !setTablesOriginal.Contains(x))) {
				LocalOnly.EnsureSafeIdentifier(table);
				result.ListTablesAdded.Add(table);
				long countRows=db.ScalarLong("SELECT COUNT(*) FROM `"+adopted+"`.`"+table+"`");
				if(countRows>0) {
					countTablesWithRows++;
					result.ListFailures.Add("The new table "+table+" holds "+countRows+" row(s); the fork migration only creates it.");
				}
			}
			HashSet<string> setColumnsOriginal=new HashSet<string>(db.Rows("SELECT TABLE_NAME,COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA="
				+SqlLiteral.Quote(original)).Select(x => Convert.ToString(x[0])+"."+Convert.ToString(x[1])),StringComparer.OrdinalIgnoreCase);
			List<object[]> listColumnsAdopted=db.Rows("SELECT TABLE_NAME,COLUMN_NAME,COLUMN_DEFAULT,IS_NULLABLE,DATA_TYPE,EXTRA FROM information_schema.COLUMNS "
				+"WHERE TABLE_SCHEMA="+SqlLiteral.Quote(adopted)+" ORDER BY TABLE_NAME,ORDINAL_POSITION");
			foreach(object[] row in listColumnsAdopted) {
				string table=Convert.ToString(row[0]);
				string column=Convert.ToString(row[1]);
				if(!setTablesOriginal.Contains(table) || setColumnsOriginal.Contains(table+"."+column)) {
					continue;
				}
				if(Convert.ToString(row[5]??"").IndexOf("generated",StringComparison.OrdinalIgnoreCase)>=0) {
					result.ListFailures.Add("The new column "+table+"."+column+" is generated; the fork migration adds no generated column.");
					continue;
				}
				LocalOnly.EnsureSafeIdentifier(table);
				LocalOnly.EnsureSafeIdentifier(column);
				string dataType=Convert.ToString(row[4]).ToLowerInvariant();
				string expression=DefaultExpression(row[2]==null ? null : Convert.ToString(row[2],CultureInfo.InvariantCulture),
					string.Equals(Convert.ToString(row[3]),"YES",StringComparison.OrdinalIgnoreCase),dataType);
				if(expression==null) {
					result.ListFailures.Add("The new column "+table+"."+column+" has a default this check cannot compare ("+(row[2]??"none")+").");
					continue;
				}
				string value=IsTextType(dataType) ? "BINARY `"+column+"`" : "`"+column+"`";
				string expected=IsTextType(dataType) && expression!="NULL" ? "BINARY "+expression : expression;
				long countOther=db.ScalarLong("SELECT COUNT(*) FROM `"+adopted+"`.`"+table+"` WHERE NOT ("+value+" <=> "+expected+")");
				if(countOther>0) {
					result.ListFailures.Add(countOther+" row(s) of "+table+" hold a value other than the default ("+expression+") in the new column "+column+".");
					continue;
				}
				result.ListColumnsAddedChecked.Add(table+"."+column);
			}
			bool isClean=result.ListFailures.Count==countFailuresBefore;
			result.ListLines.Add("New tables and columns: "+result.ListTablesAdded.Count+" new table(s), "
				+(countTablesWithRows==0 ? "all empty" : countTablesWithRows+" not empty")
				+(result.ListTablesAdded.Count==0 ? "" : " ("+string.Join(", ",result.ListTablesAdded)+")")+"; "+result.ListColumnsAddedChecked.Count
				+" new column(s) in "+result.ListColumnsAddedChecked.Select(x => x.Split('.')[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count()
				+" table(s) hold their default in every row"+(isClean ? "." : " (see the failures)."));
		}

		///<summary>The SQL value every existing row of a column added by ALTER TABLE ... ADD COLUMN holds, from information_schema.COLUMNS: its declared
		///default (a number, a quoted literal or NULL, as MariaDB 10.11 prints COLUMN_DEFAULT), or, for a NOT NULL column without one, the implicit
		///default of its type ('' for text, 0 for numbers). Null when that cannot be told (an expression such as current_timestamp(), or a NOT NULL date
		///without a default).</summary>
		public static string DefaultExpression(string columnDefault,bool isNullable,string dataType) {
			if(columnDefault==null) {
				if(isNullable) {
					return "NULL";
				}
				if(IsTextType(dataType)) {
					return "''";
				}
				return IsNumericType(dataType) ? "0" : null;
			}
			string text=columnDefault.Trim();
			if(string.Equals(text,"NULL",StringComparison.OrdinalIgnoreCase)) {
				return "NULL";
			}
			if(System.Text.RegularExpressions.Regex.IsMatch(text,@"^-?[0-9]+(\.[0-9]+)?$")
				|| System.Text.RegularExpressions.Regex.IsMatch(text,@"^'([^'\\]|'')*'$"))
			{
				return text;
			}
			return null;
		}

		private static bool IsTextType(string dataType) {
			return new[] { "char","varchar","tinytext","text","mediumtext","longtext" }.Contains(dataType);
		}

		private static bool IsNumericType(string dataType) {
			return new[] { "tinyint","smallint","mediumint","int","bigint","float","double","decimal" }.Contains(dataType);
		}

		private static bool ColumnExists(Db db,string database,string table,string column) {
			return db.ScalarLong("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)+" AND TABLE_NAME="
				+SqlLiteral.Quote(table)+" AND COLUMN_NAME="+SqlLiteral.Quote(column))>0;
		}

		private static List<string> TableNames(Db db,string database) {
			return db.Rows("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)+" AND TABLE_TYPE='BASE TABLE' "
				+"ORDER BY TABLE_NAME").Select(x => Convert.ToString(x[0])).ToList();
		}
	}
}
