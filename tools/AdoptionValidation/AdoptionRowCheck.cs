// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.Features;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>Checks, row by row against the original it was copied from, the tables where the report comparison (allowed-differences.adoption.json)
	///can only allow whole-table or whole-column changes: preference, documentmisc, securitylog and securityloghash, displayreport, and
	///updatehistory. Adoption may only:
	///- preference: add its markers and the fork migration's, set DataBaseVersion and ProgramVersion to the sentinel, and blank preferences that held
	///  an address of Open Dental's servers; no other row may change, and no row may go;
	///- documentmisc: delete Open Dental's UpdateFiles and UpdateFilesSegment rows, and nothing else;
	///- securitylog and securityloghash: add adoption's and the rehash's entries; no existing row may change or go;
	///- displayreport: rename the rows that the fork migration's report rows step renames (ReportRows25_3.PlanRenames on the original's rows, what
	///  migration-preview lists), InternalName only and only to this program's name for that report; no other row or column may change, and no row
	///  may be added or go;
	///- updatehistory: nothing.
	///Read-only; it names rows, preferences and report names, never other values. The original must be the unchanged database the copy was made
	///from (runbook step 3.3 showed they were equal, and step 10.2 shows the original did not change).</summary>
	public static class AdoptionRowCheck {
		///<summary>Preferences adoption and the fork migration add.</summary>
		public static readonly string[] ArrayAddedPrefNames={ ForkVersions.PREF_FORK_ADOPTED_FROM,ForkVersions.PREF_OD_SCHEMA_COMPAT,
			ForkVersions.PREF_FORK_SCHEMA_LEVEL,ForkVersions.PREF_FORK_PROGRAM_VERSION,ForkMigrationRunner.PrefNameApplied,ForkMigrationRunner.PrefNameInProgress };
		///<summary>The start of the security log entries adoption and the rehash write.</summary>
		public static readonly string[] ArrayLogTexts={ "Database adopted from Open Dental ",HeadlessAdoption.REHASH_LOG_TEXT };

		public class Result {
			public List<string> ListLines=new List<string>();
			public List<string> ListFailures=new List<string>();
			public List<string> ListPrefsAdded=new List<string>();
			public List<string> ListPrefsSentinel=new List<string>();
			public List<string> ListPrefsBlanked=new List<string>();
			public long CountUpdateFileRowsDeleted;
			public long CountLogEntriesAdded;
			///<summary>The displayreport renames found, each "old name to new name (DisplayReportNum n)", all of them planned.</summary>
			public List<string> ListReportRenames=new List<string>();

			public bool Passed {
				get { return ListFailures.Count==0; }
			}
		}

		public static Result Run(Db db,string original,string adopted) {
			LocalOnly.EnsureSafeIdentifier(original);
			LocalOnly.EnsureSafeIdentifier(adopted);
			Result result=new Result();
			CheckPreferences(db,original,adopted,result);
			CheckDocumentMisc(db,original,adopted,result);
			List<long> listLogAdded=CheckSecurityLog(db,original,adopted,result);
			CheckSecurityLogHash(db,original,adopted,listLogAdded,result);
			CheckDisplayReport(db,original,adopted,result);
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
			foreach(long prefNum in diff.ListOnlySecond) {
				string name=Convert.ToString(db.Scalar("SELECT PrefName FROM `"+adopted+"`.preference WHERE PrefNum="+prefNum.ToString(CultureInfo.InvariantCulture)));
				if(ArrayAddedPrefNames.Contains(name,StringComparer.OrdinalIgnoreCase)) {
					result.ListPrefsAdded.Add(name);
				}
				else {
					result.ListFailures.Add("Preference "+name+" (PrefNum "+prefNum+") was added; adoption adds only "+string.Join(", ",ArrayAddedPrefNames)+".");
				}
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

		///<summary>The fork migration's report rows step renames, on a database Open Dental converted, the rows ReportRows25_3.PlanRenames finds in the
		///original (what migration-preview lists before the day). Nothing else in displayreport may change.</summary>
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
			Dictionary<long,ReportRows25_3.ReportTargetPlan> dictPlanned=ReportRows25_3.PlanRenames(MigrationPreview.ReadRows(db,original))
				.Where(x => x.RowRenamed!=null).ToDictionary(x => x.RowRenamed.DisplayReportNum);
			RowComparer.TableDiff diff=RowComparer.Compare(db,original,adopted,"displayreport");
			if(diff.ListColumnsInOneOnly.Count>0) {
				result.ListFailures.Add("displayreport columns differ: "+string.Join(", ",diff.ListColumnsInOneOnly));
			}
			if(diff.CountOnlyFirst>0 || diff.CountOnlySecond>0) {
				result.ListFailures.Add("displayreport rows were removed or added: "+diff.Describe()+". On an Open Dental 25.3.48 database the fork migration only renames.");
			}
			if(diff.CountChanged>diff.ListChanged.Count) {
				result.ListFailures.Add(diff.CountChanged+" displayreport rows changed; the fork migration renames at most "+ReportRows25_3.ArrayTargets.Length+".");
			}
			int countFailuresBefore=result.ListFailures.Count;
			foreach(RowComparer.ChangedRow changed in diff.ListChanged) {
				if(!dictPlanned.TryGetValue(changed.Key,out ReportRows25_3.ReportTargetPlan plan)) {
					result.ListFailures.Add("displayreport row "+changed.Key+" changed ("+string.Join(", ",changed.ListColumns)+"); the fork migration renames only the rows "
						+"migration-preview lists for "+original+".");
					continue;
				}
				if(changed.ListColumns.Count!=1 || !string.Equals(changed.ListColumns[0],"InternalName",StringComparison.OrdinalIgnoreCase)) {
					result.ListFailures.Add("displayreport row "+changed.Key+": "+string.Join(", ",changed.ListColumns)+" changed; the fork migration changes only InternalName.");
					continue;
				}
				string nameNew=Convert.ToString(db.Scalar("SELECT InternalName FROM `"+adopted+"`.displayreport WHERE DisplayReportNum="
					+changed.Key.ToString(CultureInfo.InvariantCulture)));
				if(nameNew!=plan.Target.InternalName) {
					result.ListFailures.Add("displayreport row "+changed.Key+" was renamed from "+plan.RowRenamed.InternalName+" to "+nameNew+", not to "+plan.Target.InternalName+".");
					continue;
				}
				result.ListReportRenames.Add(plan.RowRenamed.InternalName+" to "+plan.Target.InternalName+" (DisplayReportNum "+changed.Key+")");
			}
			foreach(ReportRows25_3.ReportTargetPlan plan in dictPlanned.Values.Where(x => !diff.ListChanged.Any(y => y.Key==x.RowRenamed.DisplayReportNum))) {
				result.ListFailures.Add("displayreport row "+plan.RowRenamed.DisplayReportNum+" ("+plan.RowRenamed.InternalName+") was not renamed to "+plan.Target.InternalName
					+", though the fork migration renames it.");
			}
			bool isClean=result.ListFailures.Count==countFailuresBefore && diff.CountOnlyFirst==0 && diff.CountOnlySecond==0 && diff.ListColumnsInOneOnly.Count==0
				&& diff.CountChanged==diff.ListChanged.Count;
			if(isClean && dictPlanned.Count==0) {
				result.ListLines.Add("displayreport: identical row by row (migration-preview lists no rename).");
				return;
			}
			result.ListLines.Add("displayreport: "+result.ListReportRenames.Count+" of the "+dictPlanned.Count+" rename(s) migration-preview lists, InternalName only"
				+(result.ListReportRenames.Count==0 ? "" : " ("+string.Join("; ",result.ListReportRenames)+")")
				+(isClean ? "; no other row or column changed, none added or removed." : " (see the failures)."));
		}
	}
}
