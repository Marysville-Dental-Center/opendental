using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>What Reset Code Groups does with one existing code group.</summary>
	public enum EnumCodeGroupResetAction {
		///<summary>0 - No benefit uses it: deleted.</summary>
		Delete,
		///<summary>1 - Benefits use it and a default group matches it: the benefits are moved to the new default group, then it is deleted.</summary>
		Repoint,
		///<summary>2 - Benefits use it and no default group matches it: kept as a hidden (retired) group so the benefits keep working. Only when the
		///user accepts.</summary>
		KeepRetired,
	}

	///<summary>One existing code group in a reset plan.</summary>
	[Serializable]
	public class CodeGroupResetItem {
		public CodeGroup CodeGroupOld;
		///<summary>How many benefit rows use the group.</summary>
		public int CountBenefits;
		public EnumCodeGroupResetAction Action;
		///<summary>Index into CodeGroupResetPlan.ListCodeGroupsNew of the default group that matches this group (same fixed type, or failing that the
		///same name); -1 when none does.</summary>
		public int IndexDefault=-1;
		///<summary>True when the matching default group has no default codes for the chosen code system and takes this group's ProcCodes instead
		///(see CodeGroupResets.KeepCodesForEmptyDefaults).</summary>
		public bool IsProcCodesKept;
	}

	///<summary>What Reset Code Groups will do, worked out before anything is changed so that the user can be told.</summary>
	[Serializable]
	public class CodeGroupResetPlan {
		public EnumDentalCodeSystem CodeSystem;
		///<summary>The default groups that will be inserted, in ItemOrder.</summary>
		public List<CodeGroup> ListCodeGroupsNew=new List<CodeGroup>();
		///<summary>One per existing code group.</summary>
		public List<CodeGroupResetItem> ListItems=new List<CodeGroupResetItem>();

		///<summary>Groups that benefits use but that no default group matches. The reset only runs when the user accepts that they are kept as hidden
		///groups.</summary>
		public List<CodeGroupResetItem> GetUnmatched() {
			return ListItems.FindAll(x => x.Action==EnumCodeGroupResetAction.KeepRetired);
		}

		public int GetCountBenefitsRepointed() {
			return ListItems.Where(x => x.Action==EnumCodeGroupResetAction.Repoint).Sum(x => x.CountBenefits);
		}

		///<summary>Groups the reset deletes, used or not. Custom groups that no benefit uses are among them.</summary>
		public int GetCountDeleted() {
			return ListItems.Count(x => x.Action!=EnumCodeGroupResetAction.KeepRetired);
		}

		///<summary>Existing groups whose codes are carried into a default group that has no default codes for the code system.</summary>
		public List<CodeGroupResetItem> GetCodesKept() {
			return ListItems.FindAll(x => x.IsProcCodesKept);
		}

		///<summary>Default groups that will be created with no codes: no default codes for the code system and no existing group to take them from.</summary>
		public List<CodeGroup> GetDefaultsWithoutCodes() {
			return ListCodeGroupsNew.FindAll(x => string.IsNullOrWhiteSpace(x.ProcCodes));
		}
	}

	///<summary>What a reset did.</summary>
	[Serializable]
	public class CodeGroupResetResult {
		public EnumDentalCodeSystem CodeSystem;
		///<summary>False when nothing was changed because groups in use had no default match and the user had not accepted keeping them.</summary>
		public bool IsDone;
		public int CountCreated;
		public int CountDeleted;
		public int CountKeptRetired;
		public int CountBenefitsRepointed;
		///<summary>Default groups that took the codes of the existing group they replace because they have no default codes for the code system.</summary>
		public int CountProcCodesKept;
	}

	///<summary>Reset Code Groups in Procedure Code Tools (Open Dental 25.3.54 and 25.4): deletes the office's code groups and creates the default set
	///for the code system the user picks (CodeGroupDefaults). Fork rules, so that no benefit is orphaned: a benefit that uses a group is moved to the
	///new default group with the same CodeGroupFixed or, failing that, the same name; a group in use that matches no default group is kept as a
	///hidden (retired) group, which benefits and estimates keep using, and only when the user accepts (doKeepUnmatched); a default group with no
	///default codes for the code system (most CDA groups) keeps the codes of the existing group it replaces (KeepCodesForEmptyDefaults).</summary>
	public class CodeGroupResets {
		///<summary>Works out the reset without touching the database. dictBenefitCounts: benefit rows per CodeGroupNum (see GetBenefitCounts).</summary>
		public static CodeGroupResetPlan CreatePlan(EnumDentalCodeSystem codeSystem,List<CodeGroup> listCodeGroupsExisting,
			Dictionary<long,int> dictBenefitCounts)
		{
			Meth.NoCheckMiddleTierRole();
			CodeGroupResetPlan codeGroupResetPlan=new CodeGroupResetPlan();
			codeGroupResetPlan.CodeSystem=codeSystem;
			codeGroupResetPlan.ListCodeGroupsNew=CodeGroupDefaults.GetDefaults(codeSystem);
			List<CodeGroup> listCodeGroupsOrdered=listCodeGroupsExisting.OrderBy(x => x.ItemOrder).ThenBy(x => x.CodeGroupNum).ToList();
			for(int i=0;i<listCodeGroupsOrdered.Count;i++) {
				CodeGroupResetItem codeGroupResetItem=new CodeGroupResetItem();
				codeGroupResetItem.CodeGroupOld=listCodeGroupsOrdered[i];
				if(dictBenefitCounts!=null && dictBenefitCounts.TryGetValue(listCodeGroupsOrdered[i].CodeGroupNum,out int countBenefits)) {
					codeGroupResetItem.CountBenefits=countBenefits;
				}
				codeGroupResetItem.IndexDefault=FindDefaultMatch(listCodeGroupsOrdered[i],codeGroupResetPlan.ListCodeGroupsNew);
				if(codeGroupResetItem.CountBenefits==0) {
					codeGroupResetItem.Action=EnumCodeGroupResetAction.Delete;
				}
				else if(codeGroupResetItem.IndexDefault>=0) {
					codeGroupResetItem.Action=EnumCodeGroupResetAction.Repoint;
				}
				else {
					codeGroupResetItem.Action=EnumCodeGroupResetAction.KeepRetired;
				}
				codeGroupResetPlan.ListItems.Add(codeGroupResetItem);
			}
			KeepCodesForEmptyDefaults(codeGroupResetPlan);
			return codeGroupResetPlan;
		}

		///<summary>Fork rule, so that benefits moved to a default group still match procedures (INFERRED, see docs/port-notes/B-PROC.md): a default
		///group with no default codes for the code system (the CDA list has codes only for Fluoride and Sealant) takes the ProcCodes of an existing
		///group that matches it, used by benefits or not, so frequency limitations, Insurance History and the sheets' date-last fields keep finding
		///the office's procedures. When several existing groups match, the one with the most benefits wins, then the first in ItemOrder. Existing
		///groups without codes are passed over; a default with no source stays empty and GetPlanSummary names it.</summary>
		private static void KeepCodesForEmptyDefaults(CodeGroupResetPlan codeGroupResetPlan) {
			for(int i=0;i<codeGroupResetPlan.ListCodeGroupsNew.Count;i++) {
				CodeGroup codeGroupNew=codeGroupResetPlan.ListCodeGroupsNew[i];
				if(!string.IsNullOrWhiteSpace(codeGroupNew.ProcCodes)) {
					continue;
				}
				int indexDefault=i;
				//ListItems is in ItemOrder and OrderByDescending is a stable sort, so a tie goes to the first group in ItemOrder.
				CodeGroupResetItem codeGroupResetItemSource=codeGroupResetPlan.ListItems
					.Where(x => x.IndexDefault==indexDefault && !string.IsNullOrWhiteSpace(x.CodeGroupOld.ProcCodes))
					.OrderByDescending(x => x.CountBenefits)
					.FirstOrDefault();
				if(codeGroupResetItemSource==null) {
					continue;
				}
				codeGroupNew.ProcCodes=codeGroupResetItemSource.CodeGroupOld.ProcCodes;
				codeGroupResetItemSource.IsProcCodesKept=true;
				CodeGroupDefaults.ApplyHistoryDefaults(codeGroupNew);//With codes now, a history category's group gets its Insurance History row (26.1).
			}
		}

		///<summary>The index of the default group that replaces the group passed in: the one with the same CodeGroupFixed when the group has a fixed
		///type, otherwise (or when no default has that type) the one with the same name, compared trimmed and without regard to case. -1 when none.</summary>
		public static int FindDefaultMatch(CodeGroup codeGroup,List<CodeGroup> listCodeGroupsDefault) {
			Meth.NoCheckMiddleTierRole();
			if(codeGroup==null) {
				return -1;
			}
			if(codeGroup.CodeGroupFixed!=EnumCodeGroupFixed.None) {
				int index=listCodeGroupsDefault.FindIndex(x => x.CodeGroupFixed==codeGroup.CodeGroupFixed);
				if(index>=0) {
					return index;
				}
			}
			string groupName=(codeGroup.GroupName??"").Trim();
			if(groupName=="") {
				return -1;
			}
			return listCodeGroupsDefault.FindIndex(x => string.Equals((x.GroupName??"").Trim(),groupName,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>Benefit rows per CodeGroupNum, for every group that a benefit uses.</summary>
		public static Dictionary<long,int> GetBenefitCounts() {
			Meth.NoCheckMiddleTierRole();
			DataTable table=GetTableBenefitCounts();
			Dictionary<long,int> dictBenefitCounts=new Dictionary<long,int>();
			for(int i=0;i<table.Rows.Count;i++) {
				dictBenefitCounts[PIn.Long(table.Rows[i]["CodeGroupNum"].ToString())]=PIn.Int(table.Rows[i]["CountBenefits"].ToString());
			}
			return dictBenefitCounts;
		}

		///<summary>Columns CodeGroupNum and CountBenefits.</summary>
		public static DataTable GetTableBenefitCounts() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod());
			}
			string command="SELECT CodeGroupNum,COUNT(*) CountBenefits FROM benefit WHERE CodeGroupNum!=0 GROUP BY CodeGroupNum";
			return Db.GetTable(command);
		}

		///<summary>The plan for the code groups and benefits in the database now.</summary>
		public static CodeGroupResetPlan CreatePlanFromDb(EnumDentalCodeSystem codeSystem) {
			Meth.NoCheckMiddleTierRole();
			return CreatePlan(codeSystem,CodeGroups.GetManyFromDb(),GetBenefitCounts());
		}

		///<summary>Runs the reset against the database as it is now (the plan is worked out again here). When groups in use have no default match and
		///doKeepUnmatched is false, nothing is changed and the result's IsDone is false. Order: insert the defaults, move benefits, retire the kept
		///groups, delete the rest, so that an interruption never leaves a benefit pointing to a deleted group; running the tool again finishes the
		///job. Makes a ProcCodeEdit log entry. The caller refreshes the code group cache and sends the InvalidType.CodeGroups signal.</summary>
		public static CodeGroupResetResult Reset(EnumDentalCodeSystem codeSystem,bool doKeepUnmatched) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<CodeGroupResetResult>(MethodBase.GetCurrentMethod(),codeSystem,doKeepUnmatched);
			}
			CodeGroupResetPlan codeGroupResetPlan=CreatePlanFromDb(codeSystem);
			CodeGroupResetResult codeGroupResetResult=new CodeGroupResetResult();
			codeGroupResetResult.CodeSystem=codeSystem;
			List<CodeGroupResetItem> listCodeGroupResetItemsUnmatched=codeGroupResetPlan.GetUnmatched();
			if(listCodeGroupResetItemsUnmatched.Count>0 && !doKeepUnmatched) {
				return codeGroupResetResult;
			}
			for(int i=0;i<codeGroupResetPlan.ListCodeGroupsNew.Count;i++) {
				Crud.CodeGroupCrud.Insert(codeGroupResetPlan.ListCodeGroupsNew[i]);
				codeGroupResetResult.CountCreated++;
			}
			for(int i=0;i<codeGroupResetPlan.ListItems.Count;i++) {
				CodeGroupResetItem codeGroupResetItem=codeGroupResetPlan.ListItems[i];
				if(codeGroupResetItem.Action!=EnumCodeGroupResetAction.Repoint) {
					continue;
				}
				long codeGroupNumNew=codeGroupResetPlan.ListCodeGroupsNew[codeGroupResetItem.IndexDefault].CodeGroupNum;
				string command="UPDATE benefit SET CodeGroupNum="+POut.Long(codeGroupNumNew)+" "
					+"WHERE CodeGroupNum="+POut.Long(codeGroupResetItem.CodeGroupOld.CodeGroupNum);
				codeGroupResetResult.CountBenefitsRepointed+=(int)Db.NonQ(command);
			}
			int itemOrder=codeGroupResetPlan.ListCodeGroupsNew.Count;
			for(int i=0;i<listCodeGroupResetItemsUnmatched.Count;i++) {
				CodeGroup codeGroup=listCodeGroupResetItemsUnmatched[i].CodeGroupOld;
				codeGroup.IsHidden=true;
				codeGroup.ItemOrder=itemOrder++;
				Crud.CodeGroupCrud.Update(codeGroup);
				codeGroupResetResult.CountKeptRetired++;
			}
			List<long> listCodeGroupNumsDelete=codeGroupResetPlan.ListItems
				.Where(x => x.Action!=EnumCodeGroupResetAction.KeepRetired)
				.Select(x => x.CodeGroupOld.CodeGroupNum)
				.ToList();
			if(listCodeGroupNumsDelete.Count>0) {
				Crud.CodeGroupCrud.DeleteMany(listCodeGroupNumsDelete);
			}
			codeGroupResetResult.CountDeleted=listCodeGroupNumsDelete.Count;
			codeGroupResetResult.CountProcCodesKept=codeGroupResetPlan.GetCodesKept().Count;
			codeGroupResetResult.IsDone=true;
			SecurityLogs.MakeLogEntry(EnumPermType.ProcCodeEdit,0,GetLogText(codeGroupResetResult));
			return codeGroupResetResult;
		}

		///<summary>The warning shown before the reset: what is deleted, created and moved.</summary>
		public static string GetPlanSummary(CodeGroupResetPlan codeGroupResetPlan) {
			Meth.NoCheckMiddleTierRole();
			List<string> listLines=new List<string>();
			listLines.Add(Lans.g("CodeGroups","All code groups, including custom ones, will be deleted:")+" "+codeGroupResetPlan.GetCountDeleted());
			listLines.Add(Lans.g("CodeGroups","Default code groups that will be created:")+" "+codeGroupResetPlan.ListCodeGroupsNew.Count
				+" ("+codeGroupResetPlan.CodeSystem.ToString()+")");
			listLines.Add(Lans.g("CodeGroups","Benefits that will be moved to the matching new group:")+" "+codeGroupResetPlan.GetCountBenefitsRepointed());
			List<CodeGroupResetItem> listCodeGroupResetItemsUnmatched=codeGroupResetPlan.GetUnmatched();
			if(listCodeGroupResetItemsUnmatched.Count>0) {
				listLines.Add("");
				listLines.Add(Lans.g("CodeGroups","These code groups are used by benefits and no default group matches them. They will be kept as hidden code groups so "
					+"that those benefits keep working:"));
				for(int i=0;i<listCodeGroupResetItemsUnmatched.Count;i++) {
					listLines.Add("  "+listCodeGroupResetItemsUnmatched[i].CodeGroupOld.GroupName+" ("+listCodeGroupResetItemsUnmatched[i].CountBenefits+" "
						+Lans.g("CodeGroups","benefits")+")");
				}
			}
			string codeSystemName=codeGroupResetPlan.CodeSystem.ToString();
			List<CodeGroupResetItem> listCodeGroupResetItemsCodesKept=codeGroupResetPlan.GetCodesKept();
			if(listCodeGroupResetItemsCodesKept.Count>0) {
				listLines.Add("");
				listLines.Add(Lans.g("CodeGroups","These code groups have no default codes for")+" "+codeSystemName+": "
					+Lans.g("CodeGroups","your current codes are kept:"));
				for(int i=0;i<listCodeGroupResetItemsCodesKept.Count;i++) {
					CodeGroupResetItem codeGroupResetItem=listCodeGroupResetItemsCodesKept[i];
					listLines.Add("  "+codeGroupResetPlan.ListCodeGroupsNew[codeGroupResetItem.IndexDefault].GroupName+": "
						+Lans.g("CodeGroups","codes of")+" "+codeGroupResetItem.CodeGroupOld.GroupName+" ("+codeGroupResetItem.CountBenefits+" "
						+Lans.g("CodeGroups","benefits")+")");
				}
			}
			List<CodeGroup> listCodeGroupsWithoutCodes=codeGroupResetPlan.GetDefaultsWithoutCodes();
			if(listCodeGroupsWithoutCodes.Count>0) {
				listLines.Add("");
				listLines.Add(Lans.g("CodeGroups","These default code groups have no default codes for")+" "+codeSystemName+" "
					+Lans.g("CodeGroups","and will be created empty. Add their codes in Setup, Code Groups:")+" "
					+string.Join(", ",listCodeGroupsWithoutCodes.Select(x => x.GroupName)));
			}
			return string.Join("\r\n",listLines);
		}

		///<summary>The ProcCodeEdit log text: the code system and the counts.</summary>
		public static string GetLogText(CodeGroupResetResult codeGroupResetResult) {
			Meth.NoCheckMiddleTierRole();
			string logText=Lans.g("CodeGroups","Code groups reset to the default")+" "+codeGroupResetResult.CodeSystem.ToString()+" "+Lans.g("CodeGroups","code groups")+": "
				+codeGroupResetResult.CountCreated+" "+Lans.g("CodeGroups","created")+", "
				+codeGroupResetResult.CountDeleted+" "+Lans.g("CodeGroups","deleted")+", "
				+codeGroupResetResult.CountKeptRetired+" "+Lans.g("CodeGroups","kept as hidden")+", "
				+codeGroupResetResult.CountBenefitsRepointed+" "+Lans.g("CodeGroups","benefits moved to the new groups");
			if(codeGroupResetResult.CountProcCodesKept>0) {
				logText+=", "+codeGroupResetResult.CountProcCodesKept+" "+Lans.g("CodeGroups","groups with no default codes kept their current codes");
			}
			return logText+".";
		}
	}
}
