// M2-HIST: moves the ten InsHist*Codes preferences (the fixed Insurance History rows of 24.3 and 25.3) into code groups marked Show In History
// (Open Dental 26.1). The preference rows are kept.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using OpenDentBusiness.ForkMigrations.Seeds26_1;

namespace OpenDentBusiness.ForkMigrations.FeatureSteps {
	//A feature package's data step, not one of the Open Dental schema steps: it lives in its own namespace, like XConnectEnableStep.
	///<summary>From Open Dental 26.1 the Insurance History rows come from code groups (codegroup.ShowInHistory, HistProcCode, IsPerioFourQuads, added
	///by M2-01's 26.1 schema step at level 2026102402). Open Dental's own conversion is not public; this is the fork's rule from the 26.1 spec
	///(INFERRED), see InsHistCodeGroupsMigration.Plan.
	///<para>Runs only on a database that comes from before Open Dental 25.4 (Seed26_1Step.IsFromBefore25_4: 24.3, or an adopted 25.3 database), only
	///on its first run (a RunAll rerun of an applied level skips it), and only while no code group is marked Show In History yet, so a database whose
	///history rows are already set up is never changed. The preferences are read, never changed or deleted.</para>
	///Level 2027004701 (range 2027004701 to 2027004799, package M2-HIST).</summary>
	[ForkMigration(2027004701,"Insurance History rows from the InsHist preferences to code groups (26.1)")]
	public class InsHistCodeGroupsStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			foreach(string column in new[] { "ShowInHistory","HistProcCode","IsPerioFourQuads" }) {
				if(!context.ColumnExists("codegroup",column)) {
					throw new ApplicationException("codegroup."+column+" is missing; the Open Dental 26.1 schema step must run first.");
				}
			}
			if(!Seed26_1Step.IsFromBefore25_4(context)) {
				return;
			}
			if(ForkMigrationRunner.GetAppliedLevels().Contains(Level)) {
				return;
			}
			if(PIn.Long(Db.GetScalar("SELECT COUNT(*) FROM codegroup WHERE ShowInHistory=1"))>0) {
				return;
			}
			Dictionary<string,string> dictPrefs=new Dictionary<string,string>();
			string prefNames=string.Join(",",InsHistCodeGroupsMigration.ListPrefNames.Select(x => "'"+POut.String(x.ToString())+"'"));
			DataTable tablePrefs=Db.GetTable("SELECT PrefName,ValueString FROM preference WHERE PrefName IN ("+prefNames+")");
			for(int i=0;i<tablePrefs.Rows.Count;i++) {
				dictPrefs[tablePrefs.Rows[i]["PrefName"].ToString()]=tablePrefs.Rows[i]["ValueString"].ToString();
			}
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
			List<string> listProcCodes=Db.GetTable("SELECT ProcCode FROM procedurecode").Select().Select(x => x["ProcCode"].ToString()).ToList();
			List<CodeGroup> listCodeGroupsChanged=InsHistCodeGroupsMigration.Plan(dictPrefs,listCodeGroups,listProcCodes,out List<string> listNotes);
			for(int i=0;i<listCodeGroupsChanged.Count;i++) {
				CodeGroup codeGroup=listCodeGroupsChanged[i];
				if(codeGroup.IsNew) {
					context.ExecuteData("INSERT INTO codegroup (GroupName,ProcCodes,ItemOrder,CodeGroupFixed,IsHidden,ShowInAgeLimit,ShowInFrequency,"
						+"ShowInOther,ShowInHistory,HistProcCode,IsPerioFourQuads) VALUES ("
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
				context.ExecuteData("UPDATE codegroup SET "
					+"ProcCodes='"+POut.String(codeGroup.ProcCodes)+"',"
					+"ShowInHistory="+POut.Bool(codeGroup.ShowInHistory)+","
					+"HistProcCode='"+POut.String(codeGroup.HistProcCode)+"',"
					+"IsPerioFourQuads="+POut.Bool(codeGroup.IsPerioFourQuads)+" "
					+"WHERE CodeGroupNum="+POut.Long(codeGroup.CodeGroupNum));
			}
			for(int i=0;i<listNotes.Count;i++) {
				context.Log(listNotes[i]);
			}
		}
	}

	///<summary>The fork's rule for moving the ten InsHist*Codes preferences into code groups (Open Dental 26.1; INFERRED, Open Dental's own
	///conversion is not public). For each history category whose preference (or, for the perio quadrants, any of the four) has a value:
	///1. The code group whose CodeGroupFixed matches the category (Exam, Prophy, BW, PanoFMX, Perio for perio maintenance, FMDebride for full mouth
	///debridement, SRP for the four perio quadrants), a group in use before a retired one, lowest ItemOrder first. If there is none, a new group named
	///after the 24.3 row, with that CodeGroupFixed, after every other group, in no benefit grid.
	///2. ShowInHistory=1, and HistProcCode is the first code of the preference that is in the procedure code list (a range gives its first code
	///in the list); when none is, the first code as written, so the setting is not lost and Code Group Edit asks for a valid one.
	///3. ProcCodes gets every code and range of the preference it does not hold yet, appended in the order written.
	///4. The four perio quadrant preferences share one group with IsPerioFourQuads=1. Its HistProcCode comes from the UR preference, or else from
	///the first of UL, LR and LL that has a value; when the quadrants had different codes, every code goes into ProcCodes and a note is logged.
	///The preferences are not changed. Pure: the step reads the rows and writes what Plan returns.</summary>
	public static class InsHistCodeGroupsMigration {
		///<summary>The ten preferences, in the order of the 24.3 Insurance History window.</summary>
		public static readonly List<PrefName> ListPrefNames=new List<PrefName> { PrefName.InsHistBWCodes,PrefName.InsHistPanoCodes,
			PrefName.InsHistExamCodes,PrefName.InsHistProphyCodes,PrefName.InsHistPerioURCodes,PrefName.InsHistPerioULCodes,PrefName.InsHistPerioLRCodes,
			PrefName.InsHistPerioLLCodes,PrefName.InsHistPerioMaintCodes,PrefName.InsHistDebridementCodes };

		///<summary>One history category of 24.3: its preferences, the code group category it moves to and the name of a group created for it.</summary>
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

		///<summary>The code groups to update (IsNew false) or insert (IsNew true), with their new values. dictPrefs holds the preference values by name,
		///listCodeGroups the code groups as they are, listProcCodes every ProcCode in the procedure code table. listNotes gets one line per decision
		///worth logging.</summary>
		public static List<CodeGroup> Plan(Dictionary<string,string> dictPrefs,List<CodeGroup> listCodeGroups,List<string> listProcCodes,
			out List<string> listNotes)
		{
			listNotes=new List<string>();
			List<CodeGroup> listCodeGroupsChanged=new List<CodeGroup>();
			List<string> listProcCodesSorted=(listProcCodes??new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x,StringComparer.OrdinalIgnoreCase).ToList();
			Dictionary<string,string> dictProcCodes=listProcCodesSorted.ToDictionary(x => x,x => x,StringComparer.OrdinalIgnoreCase);
			List<CodeGroup> listCodeGroupsAll=(listCodeGroups??new List<CodeGroup>()).Select(x => x.Copy()).ToList();
			int itemOrderNext=listCodeGroupsAll.Count==0 ? 0 : listCodeGroupsAll.Max(x => x.ItemOrder)+1;
			for(int c=0;c<_listCategories.Count;c++) {
				Category category=_listCategories[c];
				List<string> listValues=category.ListPrefNames.Select(x => GetPref(dictPrefs,x)).ToList();
				if(listValues.All(x => x=="")) {
					continue;//Not set up in 24.3: the row showed "Not Set". No history row from 26.1 on.
				}
				CodeGroup codeGroup=listCodeGroupsAll.Where(x => x.CodeGroupFixed==category.CodeGroupFixed)
					.OrderBy(x => x.IsHidden).ThenBy(x => x.ItemOrder).ThenBy(x => x.CodeGroupNum).FirstOrDefault();
				if(codeGroup!=null && listCodeGroupsChanged.Contains(codeGroup)) {
					codeGroup=null;//Two categories never share a group.
				}
				if(codeGroup==null) {
					codeGroup=new CodeGroup();
					codeGroup.IsNew=true;
					codeGroup.GroupName=category.GroupName;
					codeGroup.ProcCodes="";
					codeGroup.ItemOrder=itemOrderNext++;
					codeGroup.CodeGroupFixed=category.CodeGroupFixed;
					codeGroup.HistProcCode="";
					listCodeGroupsAll.Add(codeGroup);
					listNotes.Add("Insurance History: created code group '"+category.GroupName+"' for "+string.Join(", ",category.ListPrefNames));
				}
				codeGroup.ProcCodes=MergeProcCodes(codeGroup.ProcCodes,listValues);
				codeGroup.HistProcCode=listValues.Where(x => x!="").Select(x => GetFirstCode(x,listProcCodesSorted,dictProcCodes)).FirstOrDefault(x => x!="")??"";
				codeGroup.ShowInHistory=true;
				codeGroup.IsPerioFourQuads=category.IsPerioFourQuads;
				if(category.IsPerioFourQuads) {
					List<string> listHistCodes=listValues.Where(x => x!="").Select(x => GetFirstCode(x,listProcCodesSorted,dictProcCodes)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
					if(listHistCodes.Count>1) {
						listNotes.Add("Insurance History: the perio quadrant preferences had different codes ("+string.Join(", ",listHistCodes)+"); '"
							+codeGroup.GroupName+"' uses "+codeGroup.HistProcCode+" for all four quadrants and counts every code as history.");
					}
				}
				listCodeGroupsChanged.Add(codeGroup);
			}
			return listCodeGroupsChanged;
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
