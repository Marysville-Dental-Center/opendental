// M2-HIST: moves the ten InsHist*Codes preferences (the fixed Insurance History rows of 24.3 and 25.3) into code groups marked Show In History
// (Open Dental 26.1), each a new code group used only for Insurance History; the existing code groups are never changed (owner decision of
// 2026-11-07, switch-over question 4). The preference rows are kept.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using OpenPmsDentalBusiness.ForkMigrations.Seeds26_1;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	//A feature package's data step, not one of the Open Dental schema steps: it lives in its own namespace, like XConnectEnableStep.
	///<summary>From Open Dental 26.1 the Insurance History rows come from code groups (codegroup.ShowInHistory, HistProcCode, IsPerioFourQuads, added
	///by M2-01's 26.1 schema step at level 2026102402). Open Dental's own conversion is not public; this is the fork's rule from the 26.1 spec
	///(INFERRED), see InsHistCodeGroupsMigration.
	///<para>Runs only on a database that comes from before Open Dental 25.4 (Seed26_1Step.IsFromBefore25_4: 24.3, or an adopted 25.3 database), only
	///on its first run (a RunAll rerun of an applied level skips it), and only while no code group is marked Show In History yet, so a database whose
	///history rows are already set up is never changed. The preferences are read, never changed or deleted. Adoption runs it on an Open Dental
	///25.3.48 database: AdoptionValidation's migration-preview prints the same plan (InsHistCodeGroupsMigration.CreatePlan) and adopt's row check holds
	///the adopted copy's codegroup rows to it.</para>
	///Level 2026110104 (package M2-HIST; renumbered on 2026-11-01 from the retired package range level 2027004701). Its rule changed on 2026-11-07
	///(owner decision, switch-over question 4): it now only adds code groups used for Insurance History and changes no existing one. No office
	///database ran the earlier rule (v0.0 was never adopted; the owner reinstalls rather than upgrades it), so the level stays.</summary>
	[ForkMigration(2026110104,"Insurance History rows from the InsHist preferences to code groups (26.1)")]
	public class InsHistCodeGroupsStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			foreach(string column in new[] { "ShowInHistory","HistProcCode","IsPerioFourQuads" }) {
				if(!context.ColumnExists("codegroup",column)) {
					throw new ApplicationException("codegroup."+column+" is missing; the Open Dental 26.1 schema step must run first.");
				}
			}
			bool isFromBefore25_4=Seed26_1Step.IsFromBefore25_4(context);
			bool isLevelApplied=ForkMigrationRunner.GetAppliedLevels().Contains(Level);
			if(!isFromBefore25_4 || isLevelApplied) {
				return;//Nothing to read.
			}
			InsHistCodeGroupsMigration.MigrationPlan plan=InsHistCodeGroupsMigration.CreatePlan(isFromBefore25_4,isLevelApplied,
				InsHistCodeGroupsMigration.ReadPrefs(),InsHistCodeGroupsMigration.ReadCodeGroups(),InsHistCodeGroupsMigration.ReadProcCodes());
			foreach(string change in InsHistCodeGroupsMigration.Run(plan)) {
				context.Log(change);
			}
		}
	}

	///<summary>The fork's rule for moving the ten InsHist*Codes preferences into code groups (Open Dental 26.1; INFERRED, Open Dental's own
	///conversion is not public), as the owner decided on 2026-11-07 (switch-over question 4): the existing code groups, the frequency and age limit
	///groups among them, stay exactly as they are, and the codes of the old Insurance History settings go into code groups used only for Insurance
	///History. For each history category whose preference (or, for the perio quadrants, any of the four) has a value:
	///1. A new code group named after the 24.3 row with " History" ("Bitewing History", "Exam History", ...; a number is added when a group of that
	///name exists), after every other group, ShowInHistory=1 and nothing else: not in the frequency, age limit or other benefit grids, no
	///CodeGroupFixed (None), so no frequency limitation, age limit, sheet field or code group reset takes it for a category's group.
	///2. HistProcCode is the first code of the preference that is in the procedure code list (a range gives its first code in the list); when none
	///is, the first code as written, so the setting is not lost and Code Group Edit asks for a valid one.
	///3. ProcCodes holds every code and range of the preference, in the order written.
	///4. The four perio quadrant preferences share one group with IsPerioFourQuads=1. Its HistProcCode comes from the UR preference, or else from
	///the first of UL, LR and LL that has a value; when the quadrants had different codes, every code goes into ProcCodes and a note is logged.
	///An eligibility response that names a category goes to such a group through its History Proc Code (InsHistCategories.GetForEligibility).
	///The preferences and the existing code groups are not changed. Plan and CreatePlan are pure; the step reads the rows (ReadPrefs,
	///ReadCodeGroups, ReadProcCodes) and writes what CreatePlan returns (Run).</summary>
	public static class InsHistCodeGroupsMigration {
		///<summary>The ten preferences, in the order of the 24.3 Insurance History window.</summary>
		public static readonly List<PrefName> ListPrefNames=new List<PrefName> { PrefName.InsHistBWCodes,PrefName.InsHistPanoCodes,
			PrefName.InsHistExamCodes,PrefName.InsHistProphyCodes,PrefName.InsHistPerioURCodes,PrefName.InsHistPerioULCodes,PrefName.InsHistPerioLRCodes,
			PrefName.InsHistPerioLLCodes,PrefName.InsHistPerioMaintCodes,PrefName.InsHistDebridementCodes };

		///<summary>One history category of 24.3: its preferences, its code group category (for reference only: the group created for it has
		///CodeGroupFixed None) and its 24.3 row label, which names the group created for it.</summary>
		private class Category {
			public List<PrefName> ListPrefNames;
			public EnumCodeGroupFixed CodeGroupFixed;
			public string GroupName;
			public bool IsPerioFourQuads;

			public Category(EnumCodeGroupFixed codeGroupFixed,string groupName,params PrefName[] arrayPrefNames) {
				CodeGroupFixed=codeGroupFixed;
				GroupName=groupName;
				ListPrefNames=arrayPrefNames.ToList();
				IsPerioFourQuads=arrayPrefNames.Length>1;
			}
		}

		///<summary>In the order of the 24.3 window. The group names are the 24.3 row labels.</summary>
		private static readonly List<Category> _listCategories=new List<Category> {
			new Category(EnumCodeGroupFixed.BW,"Bitewing",PrefName.InsHistBWCodes),
			new Category(EnumCodeGroupFixed.PanoFMX,"Pano/FMX",PrefName.InsHistPanoCodes),
			new Category(EnumCodeGroupFixed.Exam,"Exam",PrefName.InsHistExamCodes),
			new Category(EnumCodeGroupFixed.Prophy,"Prophy",PrefName.InsHistProphyCodes),
			//UR first: its code is the history code of the whole group.
			new Category(EnumCodeGroupFixed.SRP,"Perio Scaling",PrefName.InsHistPerioURCodes,PrefName.InsHistPerioULCodes,PrefName.InsHistPerioLRCodes,
				PrefName.InsHistPerioLLCodes),
			new Category(EnumCodeGroupFixed.Perio,"Perio Maintenance",PrefName.InsHistPerioMaintCodes),
			new Category(EnumCodeGroupFixed.FMDebride,"Full Mouth Debridement",PrefName.InsHistDebridementCodes),
		};

		///<summary>What the step does on one database: whether it runs (the step's gates) and, when it does, the code groups it updates and adds.
		///Shared by the step (Run), AdoptionValidation's migration-preview and adopt's row check, so the three always agree.</summary>
		[Serializable]
		public class MigrationPlan {
			///<summary>The database comes from before Open Dental 25.4 (Seed26_1Step.IsFromBefore25_4).</summary>
			public bool IsFromBefore25_4;
			///<summary>The step's level is already applied: a RunAll rerun changes nothing.</summary>
			public bool IsLevelApplied;
			///<summary>Code groups already marked Show In History when the step reads them.</summary>
			public int CountShownInHistory;
			///<summary>The code groups as the step read them, in ItemOrder and CodeGroupNum order.</summary>
			public List<CodeGroup> ListCodeGroupsBefore=new List<CodeGroup>();
			///<summary>The code groups the step inserts (IsNew true), with their values; empty when it does not run. Since 2026-11-07 it never updates an
			///existing group (IsNew false), so ListUpdated is always empty.</summary>
			public List<CodeGroup> ListCodeGroupsChanged=new List<CodeGroup>();
			///<summary>The decisions worth logging, after the changes.</summary>
			public List<string> ListNotes=new List<string>();

			///<summary>True when the step's gates let it run. It then writes ListCodeGroupsChanged, which is empty when no preference has a value.</summary>
			public bool IsRun {
				get { return IsFromBefore25_4 && !IsLevelApplied && CountShownInHistory==0; }
			}

			///<summary>The existing code groups the step updates.</summary>
			public List<CodeGroup> ListUpdated {
				get { return ListCodeGroupsChanged.FindAll(x => !x.IsNew); }
			}

			///<summary>The code groups the step adds, in the order it inserts them.</summary>
			public List<CodeGroup> ListAdded {
				get { return ListCodeGroupsChanged.FindAll(x => x.IsNew); }
			}

			///<summary>The group as it was before the step, for one the step updates. Null for an added group.</summary>
			public CodeGroup GetBefore(CodeGroup codeGroupChanged) {
				if(codeGroupChanged==null || codeGroupChanged.IsNew) {
					return null;
				}
				return ListCodeGroupsBefore.FirstOrDefault(x => x.CodeGroupNum==codeGroupChanged.CodeGroupNum);
			}

			///<summary>Why the step changes nothing, or "" when it changes something.</summary>
			public string GetReasonUnchanged() {
				if(!IsFromBefore25_4) {
					return "this database does not come from before Open Dental 25.4";
				}
				if(IsLevelApplied) {
					return "it already ran on this database";
				}
				if(CountShownInHistory>0) {
					return CountShownInHistory+" code group(s) are already marked Show In History";
				}
				if(ListCodeGroupsChanged.Count==0) {
					return "none of the ten InsHist*Codes preferences has a value";
				}
				return "";
			}

			///<summary>What the step writes to the fork migration log, in order: one line per updated and added group, then the notes. Empty when it
			///changes nothing.</summary>
			public List<string> GetLogLines() {
				List<string> listLines=new List<string>();
				foreach(CodeGroup codeGroup in ListCodeGroupsChanged) {
					listLines.Add(codeGroup.IsNew ? DescribeAdd(codeGroup) : DescribeUpdate(GetBefore(codeGroup),codeGroup));
				}
				listLines.AddRange(ListNotes);
				return listLines;
			}
		}

		///<summary>The plan for a database: the step's gates (isFromBefore25_4, isLevelApplied, and no group already marked Show In History) and, when
		///they let it run, Plan on the rows. dictPrefs holds the preference values by name, listCodeGroups the code groups as they are (on an Open Dental
		///25.3.48 database without the 26.1 columns: ShowInHistory false, HistProcCode "", IsPerioFourQuads false), listProcCodes every ProcCode in the
		///procedure code table.</summary>
		public static MigrationPlan CreatePlan(bool isFromBefore25_4,bool isLevelApplied,Dictionary<string,string> dictPrefs,List<CodeGroup> listCodeGroups,
			List<string> listProcCodes)
		{
			MigrationPlan plan=new MigrationPlan();
			plan.IsFromBefore25_4=isFromBefore25_4;
			plan.IsLevelApplied=isLevelApplied;
			plan.ListCodeGroupsBefore=(listCodeGroups??new List<CodeGroup>()).OrderBy(x => x.ItemOrder).ThenBy(x => x.CodeGroupNum).Select(x => x.Copy()).ToList();
			plan.CountShownInHistory=plan.ListCodeGroupsBefore.Count(x => x.ShowInHistory);
			if(!plan.IsRun) {
				return plan;
			}
			plan.ListCodeGroupsChanged=Plan(dictPrefs,plan.ListCodeGroupsBefore,listProcCodes,out List<string> listNotes);
			plan.ListNotes=listNotes;
			return plan;
		}

		///<summary>The ten preferences of the connected database that exist, by name (values as stored).</summary>
		public static Dictionary<string,string> ReadPrefs() {
			Dictionary<string,string> dictPrefs=new Dictionary<string,string>();
			string prefNames=string.Join(",",ListPrefNames.Select(x => "'"+POut.String(x.ToString())+"'"));
			DataTable tablePrefs=Db.GetTable("SELECT PrefName,ValueString FROM preference WHERE PrefName IN ("+prefNames+")");
			for(int i=0;i<tablePrefs.Rows.Count;i++) {
				dictPrefs[tablePrefs.Rows[i]["PrefName"].ToString()]=tablePrefs.Rows[i]["ValueString"].ToString();
			}
			return dictPrefs;
		}

		///<summary>The code groups of the connected database with the columns the plan uses, in ItemOrder and CodeGroupNum order. The 26.1 columns must
		///exist (the step checks).</summary>
		public static List<CodeGroup> ReadCodeGroups() {
			List<CodeGroup> listCodeGroups=new List<CodeGroup>();
			DataTable tableCodeGroups=Db.GetTable("SELECT CodeGroupNum,GroupName,ProcCodes,ItemOrder,CodeGroupFixed,IsHidden,ShowInHistory,HistProcCode,"
				+"IsPerioFourQuads FROM codegroup ORDER BY ItemOrder,CodeGroupNum");
			for(int i=0;i<tableCodeGroups.Rows.Count;i++) {
				DataRow row=tableCodeGroups.Rows[i];
				CodeGroup codeGroup=new CodeGroup();
				codeGroup.CodeGroupNum=PIn.Long(row["CodeGroupNum"].ToString());
				codeGroup.GroupName=row["GroupName"].ToString();
				codeGroup.ProcCodes=row["ProcCodes"].ToString();
				codeGroup.ItemOrder=PIn.Int(row["ItemOrder"].ToString());
				codeGroup.CodeGroupFixed=(EnumCodeGroupFixed)PIn.Int(row["CodeGroupFixed"].ToString());
				codeGroup.IsHidden=PIn.Bool(row["IsHidden"].ToString());
				codeGroup.ShowInHistory=PIn.Bool(row["ShowInHistory"].ToString());
				codeGroup.HistProcCode=row["HistProcCode"].ToString();
				codeGroup.IsPerioFourQuads=PIn.Bool(row["IsPerioFourQuads"].ToString());
				listCodeGroups.Add(codeGroup);
			}
			return listCodeGroups;
		}

		///<summary>Every ProcCode in the procedure code table of the connected database.</summary>
		public static List<string> ReadProcCodes() {
			return Db.GetTable("SELECT ProcCode FROM procedurecode").Select().Select(x => x["ProcCode"].ToString()).ToList();
		}

		///<summary>Writes the plan with plain INSERT statements and returns the lines for the fork migration log (MigrationPlan.GetLogLines). An added
		///group is in no benefit grid (ShowInAgeLimit, ShowInFrequency and ShowInOther 0) and not hidden. A plan made before 2026-11-07 could also
		///update an existing group (its ProcCodes and the three history columns); Plan no longer makes one, and the UPDATE is kept only for such
		///a plan.</summary>
		public static List<string> Run(MigrationPlan plan) {
			if(plan==null || !plan.IsRun) {
				return new List<string>();
			}
			for(int i=0;i<plan.ListCodeGroupsChanged.Count;i++) {
				CodeGroup codeGroup=plan.ListCodeGroupsChanged[i];
				if(codeGroup.IsNew) {
					Db.NonQ("INSERT INTO codegroup (GroupName,ProcCodes,ItemOrder,CodeGroupFixed,IsHidden,ShowInAgeLimit,ShowInFrequency,ShowInOther,ShowInHistory,"
						+"HistProcCode,IsPerioFourQuads) VALUES ("
						+"'"+POut.String(codeGroup.GroupName)+"',"
						+"'"+POut.String(codeGroup.ProcCodes)+"',"
						+POut.Int(codeGroup.ItemOrder)+","
						+POut.Int((int)codeGroup.CodeGroupFixed)+","
						+"0,0,0,0,"
						+POut.Bool(codeGroup.ShowInHistory)+","
						+"'"+POut.String(codeGroup.HistProcCode)+"',"
						+POut.Bool(codeGroup.IsPerioFourQuads)+")");
					continue;
				}
				Db.NonQ("UPDATE codegroup SET "
					+"ProcCodes='"+POut.String(codeGroup.ProcCodes)+"',"
					+"ShowInHistory="+POut.Bool(codeGroup.ShowInHistory)+","
					+"HistProcCode='"+POut.String(codeGroup.HistProcCode)+"',"
					+"IsPerioFourQuads="+POut.Bool(codeGroup.IsPerioFourQuads)+" "
					+"WHERE CodeGroupNum="+POut.Long(codeGroup.CodeGroupNum));
			}
			return plan.GetLogLines();
		}

		///<summary>The log line for an existing group the step marks, for example "Code group 'BWs' (CodeGroupNum 3) shown in Insurance History,
		///History Proc Code Z0272". Names Perio Four Quadrants, and the codes added to ProcCodes.</summary>
		public static string DescribeUpdate(CodeGroup codeGroupBefore,CodeGroup codeGroupAfter) {
			string line="Code group '"+codeGroupAfter.GroupName+"' (CodeGroupNum "+codeGroupAfter.CodeGroupNum+") shown in Insurance History"
				+(codeGroupAfter.IsPerioFourQuads ? " with four quadrant rows" : "")+", History Proc Code "+codeGroupAfter.HistProcCode;
			List<string> listAdded=GetEntriesAdded(codeGroupBefore?.ProcCodes,codeGroupAfter.ProcCodes);
			if(listAdded.Count>0) {
				line+=", codes added: "+string.Join(",",listAdded);
			}
			return line;
		}

		///<summary>The log line for a group the step adds, for example "Added code group 'Exam History' (ItemOrder 13), used only for Insurance History
		///(not in frequency or age limits), History Proc Code Z0120, codes Z0120,Z0150".</summary>
		public static string DescribeAdd(CodeGroup codeGroup) {
			return "Added code group '"+codeGroup.GroupName+"' (ItemOrder "+codeGroup.ItemOrder+"), used only for Insurance History"
				+(codeGroup.IsPerioFourQuads ? " with four quadrant rows" : "")+" (not in frequency or age limits), History Proc Code "+codeGroup.HistProcCode
				+", codes "+codeGroup.ProcCodes;
		}

		///<summary>The note that closes the step's log when it added groups: the existing code groups were not changed.</summary>
		public const string NOTE_EXISTING_UNCHANGED="Insurance History: the existing code groups, the frequency and age limit groups among them, were not "
			+"changed; the codes of the old Insurance History settings are only in the added groups.";

		///<summary>The suffix of the name of a group the step adds, after the 24.3 row label.</summary>
		public const string GROUP_NAME_SUFFIX=" History";

		///<summary>The entries (codes and ranges) of procCodesAfter that procCodesBefore does not have, in order.</summary>
		public static List<string> GetEntriesAdded(string procCodesBefore,string procCodesAfter) {
			List<string> listBefore=ProcCodeRanges.Parse(procCodesBefore??"").ListEntries;
			return ProcCodeRanges.Parse(procCodesAfter??"").ListEntries.Where(x => !listBefore.Contains(x,StringComparer.OrdinalIgnoreCase)).ToList();
		}

		///<summary>The code groups to insert (IsNew true), with their values: one for each history category whose preference has a value (see the
		///class summary). The existing code groups are never changed, so none is returned with IsNew false. dictPrefs holds the preference values by
		///name, listCodeGroups the code groups as they are (only their names and ItemOrder are read), listProcCodes every ProcCode in the procedure
		///code table. listNotes gets one line per decision worth logging.</summary>
		public static List<CodeGroup> Plan(Dictionary<string,string> dictPrefs,List<CodeGroup> listCodeGroups,List<string> listProcCodes,
			out List<string> listNotes)
		{
			listNotes=new List<string>();
			List<CodeGroup> listCodeGroupsAdded=new List<CodeGroup>();
			List<string> listProcCodesSorted=(listProcCodes??new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x,StringComparer.OrdinalIgnoreCase).ToList();
			Dictionary<string,string> dictProcCodes=listProcCodesSorted.ToDictionary(x => x,x => x,StringComparer.OrdinalIgnoreCase);
			List<CodeGroup> listCodeGroupsExisting=listCodeGroups??new List<CodeGroup>();
			HashSet<string> setGroupNames=new HashSet<string>(listCodeGroupsExisting.Select(x => (x.GroupName??"").Trim()),StringComparer.OrdinalIgnoreCase);
			int itemOrderNext=listCodeGroupsExisting.Count==0 ? 0 : listCodeGroupsExisting.Max(x => x.ItemOrder)+1;
			for(int c=0;c<_listCategories.Count;c++) {
				Category category=_listCategories[c];
				List<string> listValues=category.ListPrefNames.Select(x => GetPref(dictPrefs,x)).ToList();
				if(listValues.All(x => x=="")) {
					continue;//Not set up in 24.3: the row showed "Not Set". No history row from 26.1 on.
				}
				CodeGroup codeGroup=new CodeGroup();
				codeGroup.IsNew=true;
				codeGroup.GroupName=GetUniqueGroupName(category.GroupName+GROUP_NAME_SUFFIX,setGroupNames);
				setGroupNames.Add(codeGroup.GroupName);
				codeGroup.ProcCodes=MergeProcCodes("",listValues);
				codeGroup.ItemOrder=itemOrderNext++;
				codeGroup.CodeGroupFixed=EnumCodeGroupFixed.None;//Used only for Insurance History: never a category's group.
				codeGroup.HistProcCode=listValues.Where(x => x!="").Select(x => GetFirstCode(x,listProcCodesSorted,dictProcCodes)).FirstOrDefault(x => x!="")??"";
				codeGroup.ShowInHistory=true;
				codeGroup.ShowInAgeLimit=false;
				codeGroup.ShowInFrequency=false;
				codeGroup.ShowInOther=false;
				codeGroup.IsPerioFourQuads=category.IsPerioFourQuads;
				if(category.IsPerioFourQuads) {
					List<string> listHistCodes=listValues.Where(x => x!="").Select(x => GetFirstCode(x,listProcCodesSorted,dictProcCodes)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
					if(listHistCodes.Count>1) {
						listNotes.Add("Insurance History: the perio quadrant preferences had different codes ("+string.Join(", ",listHistCodes)+"); '"
							+codeGroup.GroupName+"' uses "+codeGroup.HistProcCode+" for all four quadrants and counts every code as history.");
					}
				}
				listCodeGroupsAdded.Add(codeGroup);
			}
			if(listCodeGroupsAdded.Count>0) {
				listNotes.Add(NOTE_EXISTING_UNCHANGED);
			}
			return listCodeGroupsAdded;
		}

		///<summary>name, or name with " 2", " 3" and so on when a code group of that name exists (ignoring case).</summary>
		public static string GetUniqueGroupName(string name,HashSet<string> setGroupNames) {
			if(setGroupNames==null || !setGroupNames.Contains(name)) {
				return name;
			}
			for(int i=2;;i++) {
				string nameNumbered=name+" "+i;
				if(!setGroupNames.Contains(nameNumbered)) {
					return nameNumbered;
				}
			}
		}

		private static string GetPref(Dictionary<string,string> dictPrefs,PrefName prefName) {
			if(dictPrefs==null || !dictPrefs.TryGetValue(prefName.ToString(),out string value) || value==null) {
				return "";
			}
			return value.Trim();
		}

		///<summary>The first code of a preference value that is in the procedure code list, written as the list has it. A range gives the first code of
		///the list inside it. When no code is in the list, the first code as written (the start of a range).</summary>
		public static string GetFirstCode(string value,List<string> listProcCodesSorted,Dictionary<string,string> dictProcCodes) {
			ProcCodeRanges procCodeRanges=ProcCodeRanges.Parse(value);
			for(int i=0;i<procCodeRanges.ListEntries.Count;i++) {
				string entry=procCodeRanges.ListEntries[i];
				if(ProcCodeRanges.TryParseRange(entry,out ProcCodeRange procCodeRange)) {
					string procCodeInRange=listProcCodesSorted.FirstOrDefault(x => procCodeRange.Contains(x));
					if(procCodeInRange!=null) {
						return procCodeInRange;
					}
					continue;
				}
				if(dictProcCodes.TryGetValue(entry,out string procCode)) {
					return procCode;
				}
			}
			if(procCodeRanges.ListEntries.Count==0) {
				return "";
			}
			string first=procCodeRanges.ListEntries[0];
			if(ProcCodeRanges.TryParseRange(first,out ProcCodeRange _)) {
				return first.Substring(0,first.IndexOf('-'));
			}
			return first;
		}

		///<summary>The group's codes with every code and range of the values appended that the group does not hold yet (a code inside one of its ranges
		///counts as held). The group's own text is kept as it is.</summary>
		public static string MergeProcCodes(string procCodes,List<string> listValues) {
			procCodes=procCodes??"";
			string merged=procCodes.Trim().TrimEnd(',');
			bool isAppended=false;
			for(int v=0;v<listValues.Count;v++) {
				ProcCodeRanges procCodeRangesValue=ProcCodeRanges.Parse(listValues[v]);
				for(int i=0;i<procCodeRangesValue.ListEntries.Count;i++) {
					string entry=procCodeRangesValue.ListEntries[i];
					ProcCodeRanges procCodeRangesMerged=ProcCodeRanges.Parse(merged);
					if(procCodeRangesMerged.ListEntries.Contains(entry,StringComparer.OrdinalIgnoreCase)) {
						continue;
					}
					if(!ProcCodeRanges.TryParseRange(entry,out ProcCodeRange _) && procCodeRangesMerged.Contains(entry)) {
						continue;
					}
					merged=merged=="" ? entry : merged+","+entry;
					isAppended=true;
				}
			}
			if(!isAppended) {
				return procCodes;
			}
			return merged;
		}
	}
}
