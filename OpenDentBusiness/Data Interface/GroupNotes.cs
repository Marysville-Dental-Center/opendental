using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Rules for flexible group notes (Open Dental 25.1). A group note is a procedure with status EC and the code "~GRP~" that holds one note.
	///Its procedures are linked through procgroupitem. Since 25.1 the attached procedures can have any status, date, provider or clinic, and a group
	///note can have no procedures at all. The window and the Chart module call these rules. They live here so that Procedures.cs is not touched.</summary>
	public static class GroupNotes {
		///<summary>Statuses that count as complete when choosing the permission for a group note, the same set as
		///ProcGroupItems.GetCountCompletedProcsForGroup uses by default.</summary>
		private static readonly ProcStat[] _arrayProcStatsComplete=new ProcStat[] { ProcStat.C,ProcStat.EO,ProcStat.EC };

		#region Attaching
		///<summary>Returns null when the procedure can be attached to a group note, otherwise the reason it can't (not yet translated).
		///Kept from 24.3: no group note in a group note, no locked procedures, no deleted or invalidated procedures, and no completed procedures dated
		///in the future unless future transactions are allowed. Dropped in 25.1: the procedure doesn't have to be complete, and the procedures don't have
		///to share a date, clinic or provider.</summary>
		public static string GetAttachBlockReason(Procedure procedure,string procCode,bool isFutureTransDatesAllowed,DateTime dateToday) {
			if(procedure==null || procedure.ProcNum==0) {
				return "You may only attach a group note to procedures.";
			}
			if(procCode==ProcedureCodes.GroupProcCode) {
				return "You cannot attach a group note to another group note.";
			}
			if(procedure.IsLocked) {
				return "Locked procedures cannot be attached to a group note.";
			}
			if(procedure.ProcStatus==ProcStat.D) {
				return "Deleted or invalidated procedures cannot be attached to a group note.";
			}
			if(procedure.ProcStatus==ProcStat.C && procedure.ProcDate.Date>dateToday.Date && !isFutureTransDatesAllowed) {
				return "Completed procedures cannot be set complete for days in the future.";
			}
			return null;
		}

		///<summary>Same as the other overload, with the procedure code from the cache, the preference and today's date.</summary>
		public static string GetAttachBlockReason(Procedure procedure) {
			if(procedure==null) {
				return GetAttachBlockReason(null,"",false,DateTime.Today);
			}
			return GetAttachBlockReason(procedure,ProcedureCodes.GetStringProcCode(procedure.CodeNum,doThrowIfMissing:false),
				PrefC.GetBool(PrefName.FutureTransDatesAllowed),DateTime.Today);
		}
		#endregion Attaching

		#region Defaults for a new group note
		///<summary>The latest date of the attached procedures, or today when there are none.</summary>
		public static DateTime GetDefaultProcDate(List<Procedure> listProceduresAttached,DateTime dateToday) {
			if(listProceduresAttached.IsNullOrEmpty()) {
				return dateToday.Date;
			}
			return listProceduresAttached.Max(x => x.ProcDate.Date);
		}

		///<summary>The provider of the attached procedures when they all share one. Otherwise the user's provider, otherwise the practice default
		///provider.</summary>
		public static long GetDefaultProvNum(List<Procedure> listProceduresAttached,long provNumUser,long provNumPracticeDefault) {
			if(!listProceduresAttached.IsNullOrEmpty()) {
				List<long> listProvNums=listProceduresAttached.Select(x => x.ProvNum).Distinct().ToList();
				if(listProvNums.Count==1 && listProvNums[0]!=0) {
					return listProvNums[0];
				}
			}
			if(provNumUser!=0) {
				return provNumUser;
			}
			return provNumPracticeDefault;
		}

		///<summary>Defaults the same way as the provider: the clinic of the attached procedures when they all share one, otherwise the clinic the user is
		///working in. Always 0 when clinics are off.</summary>
		public static long GetDefaultClinicNum(List<Procedure> listProceduresAttached,long clinicNumCurrent,bool hasClinicsEnabled) {
			if(!hasClinicsEnabled) {
				return 0;
			}
			if(!listProceduresAttached.IsNullOrEmpty()) {
				List<long> listClinicNums=listProceduresAttached.Select(x => x.ClinicNum).Distinct().ToList();
				if(listClinicNums.Count==1) {
					return listClinicNums[0];
				}
			}
			return clinicNumCurrent;
		}
		#endregion Defaults for a new group note

		#region Permissions
		///<summary>True when the group note is treated like a completed note for security: at least one attached procedure is complete (C, EO or EC),
		///or nothing is attached.</summary>
		public static bool IsTreatedAsComplete(List<ProcStat> listProcStatsAttached) {
			if(listProcStatsAttached.IsNullOrEmpty()) {
				return true;
			}
			return listProcStatsAttached.Any(x => _arrayProcStatsComplete.Contains(x));
		}

		///<summary>The permission to edit, sign or delete the group note's note. As in 24.3: Edit Note on Completed Procedure when a completed procedure
		///is attached, otherwise TP Procedure Delete. A group note with nothing attached is treated as complete.</summary>
		public static EnumPermType GetNotePermission(List<ProcStat> listProcStatsAttached) {
			if(IsTreatedAsComplete(listProcStatsAttached)) {
				return EnumPermType.ProcCompleteNote;
			}
			return EnumPermType.ProcDelete;
		}

		///<summary>The permission to change the group note's date, provider or attached procedures: Edit Completed Procedure when the note is treated as
		///complete, otherwise TP Procedure Delete.</summary>
		public static EnumPermType GetStructurePermission(List<ProcStat> listProcStatsAttached) {
			if(IsTreatedAsComplete(listProcStatsAttached)) {
				return EnumPermType.ProcCompleteEdit;
			}
			return EnumPermType.ProcDelete;
		}

		///<summary>The date the permission's date limit is checked against: the group note's own date when it is treated as complete, otherwise its
		///entry date (as 24.3 does for group notes on non-complete procedures).</summary>
		public static DateTime GetPermissionDate(Procedure procedureGroup,List<ProcStat> listProcStatsAttached) {
			if(IsTreatedAsComplete(listProcStatsAttached)) {
				return procedureGroup.ProcDate;
			}
			return procedureGroup.DateEntryC;
		}

		///<summary>The permission logged when the group note is deleted.</summary>
		public static EnumPermType GetDeleteLogPermission(List<ProcStat> listProcStatsAttached) {
			if(IsTreatedAsComplete(listProcStatsAttached)) {
				return EnumPermType.ProcCompleteStatusEdit;
			}
			return EnumPermType.ProcDelete;
		}
		#endregion Permissions

		#region Signature
		///<summary>The data a group note's signature is bound to. Unchanged from 24.3 so existing signatures stay valid: procedure date, entry date,
		///user, note, then every ProcGroupItemNum in the order of listProcGroupItems, which must come from ProcGroupItems.GetForGroup (ordered by
		///ProcNum). Changing the date, the note, the user or the attached procedures changes the key, so the signature must be cleared.</summary>
		public static string GetSignatureKey(Procedure procedureGroup,List<ProcGroupItem> listProcGroupItems) {
			string keyData=procedureGroup.ProcDate.ToShortDateString();
			keyData+=procedureGroup.DateEntryC.ToShortDateString();
			keyData+=procedureGroup.UserNum.ToString();
			keyData+=procedureGroup.Note;
			if(listProcGroupItems!=null) {
				for(int i=0;i<listProcGroupItems.Count;i++) {
					keyData+=listProcGroupItems[i].ProcGroupItemNum.ToString();
				}
			}
			keyData=keyData.Replace("\r\n","\n");//All newlines must be the same, a mix of \r\n and \n can invalidate the signature.
			return keyData;
		}
		#endregion Signature

		#region Progress notes
		///<summary>Whether a group note shows when the progress notes are filtered by tooth. A group note with procedures attached shows when one of
		///them is visible (as in 24.3). A group note with nothing attached always passes this check; the date range still applies.</summary>
		public static bool IsVisibleWithToothFilter(long groupNum,List<ProcGroupItem> listProcGroupItems,List<long> listProcNumsVisible) {
			List<long> listProcNumsAttached=(listProcGroupItems??new List<ProcGroupItem>()).FindAll(x => x.GroupNum==groupNum).Select(x => x.ProcNum).ToList();
			if(listProcNumsAttached.Count==0) {
				return true;
			}
			HashSet<long> hashSetVisible=new HashSet<long>(listProcNumsVisible??new List<long>());
			return listProcNumsAttached.Any(x => hashSetVisible.Contains(x));
		}

		///<summary>Moves each group note row so that it shows after the last row of the procedures attached to it. A group note that already shows
		///after them stays where its own date puts it, and a group note with none of its procedures in the list sorts by its own date. The rows need
		///the ProcNum and ProcCode columns. Rows that are not procedures are never moved.</summary>
		public static void PlaceAfterAttachedProcs(List<DataRow> listDataRows,List<ProcGroupItem> listProcGroupItems) {
			if(listDataRows.IsNullOrEmpty() || listProcGroupItems.IsNullOrEmpty()) {
				return;
			}
			Dictionary<long,List<long>> dictAttached=listProcGroupItems.GroupBy(x => x.GroupNum)
				.ToDictionary(x => x.Key,x => x.Select(y => y.ProcNum).Distinct().ToList());
			List<DataRow> listDataRowsGroup=listDataRows.FindAll(x => IsGroupNoteRow(x) && dictAttached.ContainsKey(GetProcNum(x)));
			for(int i=0;i<listDataRowsGroup.Count;i++) {
				DataRow dataRowGroup=listDataRowsGroup[i];
				HashSet<long> hashSetAttached=new HashSet<long>(dictAttached[GetProcNum(dataRowGroup)]);
				int idxLastAttached=-1;
				for(int r=0;r<listDataRows.Count;r++) {
					if(listDataRows[r]==dataRowGroup) {
						continue;
					}
					long procNum=GetProcNum(listDataRows[r]);
					if(procNum!=0 && hashSetAttached.Contains(procNum)) {
						idxLastAttached=r;
					}
				}
				if(idxLastAttached==-1) {
					continue;//None of its procedures are in the list. It sorts by its own date.
				}
				int idxGroup=listDataRows.IndexOf(dataRowGroup);
				if(idxGroup>idxLastAttached) {
					continue;//Already after its procedures.
				}
				listDataRows.RemoveAt(idxGroup);
				//Removing the group note row shifted the last attached row down by one, so this index is right after it.
				listDataRows.Insert(idxLastAttached,dataRowGroup);
			}
		}

		private static bool IsGroupNoteRow(DataRow dataRow) {
			return GetProcNum(dataRow)!=0 && dataRow["ProcCode"].ToString()==ProcedureCodes.GroupProcCode;
		}

		private static long GetProcNum(DataRow dataRow) {
			return PIn.Long(dataRow["ProcNum"].ToString());
		}
		#endregion Progress notes

		#region Audit
		///<summary>Text for the Proc Edit log entry on the group note when its date, provider or attached procedures change. Empty when nothing of that
		///changed. The note itself is not logged here, as in 24.3.</summary>
		public static string GetEditLogText(Procedure procedureGroupOld,Procedure procedureGroup,List<Procedure> listProceduresAdded,
			List<Procedure> listProceduresRemoved,Func<long,string> funcGetProcCode,Func<long,string> funcGetProvAbbr)
		{
			List<string> listParts=new List<string>();
			if(procedureGroupOld.ProcDate.Date!=procedureGroup.ProcDate.Date) {
				listParts.Add(Lans.g("GroupNotes","Date changed from")+" "+procedureGroupOld.ProcDate.ToShortDateString()+" "
					+Lans.g("GroupNotes","to")+" "+procedureGroup.ProcDate.ToShortDateString());
			}
			if(procedureGroupOld.ProvNum!=procedureGroup.ProvNum) {
				listParts.Add(Lans.g("GroupNotes","Provider changed from")+" "+funcGetProvAbbr(procedureGroupOld.ProvNum)+" "
					+Lans.g("GroupNotes","to")+" "+funcGetProvAbbr(procedureGroup.ProvNum));
			}
			if(!listProceduresAdded.IsNullOrEmpty()) {
				listParts.Add(Lans.g("GroupNotes","Procedures added")+": "+string.Join(", ",listProceduresAdded.Select(x => DescribeProc(x,funcGetProcCode))));
			}
			if(!listProceduresRemoved.IsNullOrEmpty()) {
				listParts.Add(Lans.g("GroupNotes","Procedures removed")+": "+string.Join(", ",listProceduresRemoved.Select(x => DescribeProc(x,funcGetProcCode))));
			}
			if(listParts.Count==0) {
				return "";
			}
			return Lans.g("GroupNotes","Group note")+" "+procedureGroup.ProcDate.ToShortDateString()+". "+string.Join(". ",listParts)+".";
		}

		private static string DescribeProc(Procedure procedure,Func<long,string> funcGetProcCode) {
			return funcGetProcCode(procedure.CodeNum)+" ("+procedure.ProcStatus.ToString()+", "+procedure.ProcDate.ToShortDateString()+")";
		}
		#endregion Audit
	}
}
