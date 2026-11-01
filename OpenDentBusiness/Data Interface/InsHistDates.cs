using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Insurance History with two dates per category (Open Dental 25.1) that can be edited or removed one by one (25.2). A date is an Existing
	///Other (EO) or Completed (C) procedure with one of the category's codes and an InsHist or Received claimproc on the subscriber's plan, the same
	///procedures the single date of 24.3 came from. Dates from completed procedures are read-only; only EO procedures are changed or deleted.
	///The categories are still the ten InsHist*Codes preferences, and a new date still uses the first code of the category's preference.</summary>
	public class InsHistDates {
		///<summary>How many dates the Insurance History window and the Family module show per category.</summary>
		public const int DATES_PER_CATEGORY=2;

		///<summary>The name of a category as the Insurance History window shows it, also used in the security log.</summary>
		public static string GetCategoryName(PrefName prefName) {
			Meth.NoCheckMiddleTierRole();
			switch(prefName) {
				case PrefName.InsHistBWCodes:
					return Lans.g("InsHistDates","Bitewing");
				case PrefName.InsHistPanoCodes:
					return Lans.g("InsHistDates","FMX/Pano");
				case PrefName.InsHistExamCodes:
					return Lans.g("InsHistDates","Exam");
				case PrefName.InsHistProphyCodes:
					return Lans.g("InsHistDates","Prophy");
				case PrefName.InsHistPerioURCodes:
					return Lans.g("InsHistDates","Perio Scaling UR");
				case PrefName.InsHistPerioULCodes:
					return Lans.g("InsHistDates","Perio Scaling UL");
				case PrefName.InsHistPerioLRCodes:
					return Lans.g("InsHistDates","Perio Scaling LR");
				case PrefName.InsHistPerioLLCodes:
					return Lans.g("InsHistDates","Perio Scaling LL");
				case PrefName.InsHistPerioMaintCodes:
					return Lans.g("InsHistDates","Perio Maintenance");
				case PrefName.InsHistDebridementCodes:
					return Lans.g("InsHistDates","Full Mouth Debridement");
				default:
					return prefName.ToString();
			}
		}

		///<summary>The quadrant a perio scaling category is limited to (matched against procedurelog.Surf), or "" for the other categories. Same rule as
		///Procedures.GetMostRecentInsHistProc.</summary>
		public static string GetQuadrant(PrefName prefName) {
			Meth.NoCheckMiddleTierRole();
			switch(prefName) {
				case PrefName.InsHistPerioURCodes:
					return "UR";
				case PrefName.InsHistPerioULCodes:
					return "UL";
				case PrefName.InsHistPerioLRCodes:
					return "LR";
				case PrefName.InsHistPerioLLCodes:
					return "LL";
				default:
					return "";
			}
		}

		///<summary>The patient's EO and C procedures that have an InsHist or Received claimproc on the inssub, the procedures Insurance History is
		///built from. listClaimProcs gets those claimprocs.</summary>
		public static List<Procedure> GetProcsForInsSub(long patNum,long insSubNum,out List<ClaimProc> listClaimProcs) {
			Meth.NoCheckMiddleTierRole();
			List<Procedure> listProcedures=Procedures.GetProcsByStatusForPat(patNum,ProcStat.EO,ProcStat.C);
			List<ClaimProc> listClaimProcsAll=ClaimProcs.GetForProcs(listProcedures.Select(x => x.ProcNum).ToList());
			listClaimProcs=listClaimProcsAll.FindAll(x => x.InsSubNum==insSubNum && x.Status.In(ClaimProcStatus.InsHist,ClaimProcStatus.Received));
			List<long> listProcNums=listClaimProcs.Select(x => x.ProcNum).Distinct().ToList();
			return listProcedures.FindAll(x => listProcNums.Contains(x.ProcNum));
		}

		///<summary>The procedures behind the dates of one category, most recent first, at most DATES_PER_CATEGORY. listProcedures are the
		///procedures of one inssub (GetProcsForInsSub). A completed procedure comes before an EO procedure on the same day.</summary>
		public static List<Procedure> GetMostRecentProcs(List<Procedure> listProcedures,List<long> listCodeNums,PrefName prefName) {
			Meth.NoCheckMiddleTierRole();
			string quadrant=GetQuadrant(prefName);
			return listProcedures
				.FindAll(x => listCodeNums.Contains(x.CodeNum) && (quadrant=="" || (x.Surf??"").Contains(quadrant)))
				.OrderByDescending(x => x.ProcDate.Date)
				.ThenBy(x => x.ProcStatus==ProcStat.C ? 0 : 1)
				.ThenByDescending(x => x.ProcNum)
				.Take(DATES_PER_CATEGORY)
				.ToList();
		}

		///<summary>The dates of one category for the Family module's insurance grid: the most recent date, then the previous one, for example
		///"3/1/2026, 9/15/2025". Empty when the category has no history.</summary>
		public static string GetDatesText(List<Procedure> listProcedures,List<long> listCodeNums,PrefName prefName) {
			Meth.NoCheckMiddleTierRole();
			return string.Join(", ",GetMostRecentProcs(listProcedures,listCodeNums,prefName).Select(x => x.ProcDate.ToShortDateString()));
		}

		///<summary>What saving one category of the Insurance History window does. Returns an empty list when nothing changed. error is set, and
		///nothing is returned, when the category can not be saved: two dates on the same day. Boxes the user did not change are never checked, so
		///history that already holds two procedures on one day does not block the window.</summary>
		public static List<InsHistAction> GetActions(PrefName prefName,List<InsHistBox> listBoxes,out string error) {
			Meth.NoCheckMiddleTierRole();
			error="";
			List<InsHistAction> listActions=new List<InsHistAction>();
			if(!listBoxes.Any(x => x.IsChanged())) {
				return listActions;
			}
			List<DateTime> listDates=listBoxes.Select(x => x.GetDateSaved()).Where(x => x!=DateTime.MinValue).ToList();
			if(listDates.Count!=listDates.Distinct().Count()) {
				error=GetCategoryName(prefName)+": "+Lans.g("InsHistDates","the two dates can't be the same.");
				return listActions;
			}
			for(int i=0;i<listBoxes.Count;i++) {
				InsHistBox insHistBox=listBoxes[i];
				if(!insHistBox.IsChanged()) {
					continue;
				}
				InsHistAction insHistAction=new InsHistAction();
				insHistAction.PrefName=prefName;
				insHistAction.Procedure=insHistBox.ProcedureLoaded;
				insHistAction.DateNew=insHistBox.DateEntered.Date;
				if(insHistBox.ProcedureLoaded==null) {
					insHistAction.ActionType=InsHistActionType.Insert;
				}
				else if(insHistBox.DateEntered==DateTime.MinValue) {
					insHistAction.ActionType=InsHistActionType.Remove;
				}
				else {
					insHistAction.ActionType=InsHistActionType.ChangeDate;
				}
				listActions.Add(insHistAction);
			}
			return listActions;
		}

		///<summary>The permission an action needs, and the date to check it with: ProcExistingEdit to change the date of an EO procedure and
		///ProcDelete, with its date restrictions, to remove one. Returns false for an action that needs no permission: a new date, as in 24.3.</summary>
		public static bool TryGetPermission(InsHistAction insHistAction,out EnumPermType enumPermType,out DateTime dateForPermCheck) {
			Meth.NoCheckMiddleTierRole();
			enumPermType=EnumPermType.None;
			dateForPermCheck=DateTime.MinValue;
			if(insHistAction.Procedure==null) {
				return false;
			}
			if(insHistAction.ActionType==InsHistActionType.ChangeDate) {
				enumPermType=EnumPermType.ProcExistingEdit;
			}
			else if(insHistAction.ActionType==InsHistActionType.Remove) {
				enumPermType=EnumPermType.ProcDelete;
			}
			else {
				return false;
			}
			dateForPermCheck=Procedures.GetDateForPermCheck(insHistAction.Procedure);
			return true;
		}

		///<summary>Saves the actions of the Insurance History window for the inssub. A new date creates an EO procedure with the first code of the
		///category and an InsHist claimproc; a changed date moves the EO procedure and its claimprocs; a removed date deletes the EO procedure, which
		///never touches completed procedures, claims or adjustments (Procedures.Delete refuses a procedure that is attached to any). listClaimProcs
		///are the claimprocs of the loaded procedures (GetProcsForInsSub). Edits are logged under ProcExistingEdit and removals under ProcDelete,
		///with the category. Recalls are synched once at the end. Returns a message for each action that could not be saved.</summary>
		public static List<string> ApplyActions(Patient patient,InsSub insSub,List<InsHistAction> listActions,List<ClaimProc> listClaimProcs) {
			Meth.NoCheckMiddleTierRole();
			List<string> listMessages=new List<string>();
			bool isChanged=false;
			for(int i=0;i<listActions.Count;i++) {
				InsHistAction insHistAction=listActions[i];
				string category=GetCategoryName(insHistAction.PrefName);
				if(insHistAction.ActionType==InsHistActionType.Insert) {
					if(ProcedureCodes.GetByInsHistPref(insHistAction.PrefName).CodeNum==0) {
						listMessages.Add(category+": "+Lans.g("InsHistDates","the preference for this category has no valid procedure code. "
							+"Check the Insurance History codes in the Treatment Plan module preferences."));
						continue;
					}
					Procedures.InsertOrUpdateInsHistProcedure(patient,insHistAction.PrefName,insHistAction.DateNew,insSub.PlanNum,insSub.InsSubNum,null,
						new List<ClaimProc>());
					isChanged=true;
					continue;
				}
				Procedure procedure=insHistAction.Procedure;
				string procCode=ProcedureCodes.GetStringProcCode(procedure.CodeNum,doThrowIfMissing:false);
				DateTime dateOld=procedure.ProcDate;
				if(insHistAction.ActionType==InsHistActionType.ChangeDate) {
					if(procedure.ProcStatus!=ProcStat.EO) {
						continue;//Completed procedures are never edited here.
					}
					List<ClaimProc> listClaimProcsForProc=listClaimProcs.FindAll(x => x.ProcNum==procedure.ProcNum);
					Procedures.InsertOrUpdateInsHistProcedure(patient,insHistAction.PrefName,insHistAction.DateNew,insSub.PlanNum,insSub.InsSubNum,
						procedure,listClaimProcsForProc);
					SecurityLogs.MakeLogEntry(EnumPermType.ProcExistingEdit,patient.PatNum,Lans.g("InsHistDates","Insurance History")+", "+category+": "
						+procCode+" "+Lans.g("InsHistDates","date changed from")+" "+dateOld.ToShortDateString()+" "+Lans.g("InsHistDates","to")+" "
						+insHistAction.DateNew.ToShortDateString());
					isChanged=true;
					continue;
				}
				//Remove
				if(procedure.ProcStatus!=ProcStat.EO) {
					continue;//Completed procedures are never deleted here.
				}
				try {
					Procedures.Delete(procedure.ProcNum);
				}
				catch(Exception ex) {
					listMessages.Add(category+": "+Lans.g("InsHistDates","could not remove")+" "+dateOld.ToShortDateString()+". "+ex.Message);
					continue;
				}
				SecurityLogs.MakeLogEntry(EnumPermType.ProcDelete,patient.PatNum,Lans.g("InsHistDates","Insurance History")+", "+category+": "
					+procCode+" "+Lans.g("InsHistDates","on")+" "+dateOld.ToShortDateString()+" "+Lans.g("InsHistDates","removed"));
				isChanged=true;
			}
			if(isChanged) {
				Recalls.Synch(patient.PatNum);//Removing or moving a date changes recall due dates, as adding one does.
			}
			return listMessages;
		}

		///<summary>Notes for dates that were saved but that the category does not show after saving, because DATES_PER_CATEGORY more recent
		///procedures are on file (for example a new EO date older than two completed procedures). listProcedures are the inssub's procedures after
		///saving (GetProcsForInsSub).</summary>
		public static List<string> GetNotesForHiddenDates(List<InsHistAction> listActions,List<Procedure> listProcedures) {
			Meth.NoCheckMiddleTierRole();
			List<string> listNotes=new List<string>();
			List<InsHistAction> listActionsDated=listActions.FindAll(x => x.ActionType.In(InsHistActionType.Insert,InsHistActionType.ChangeDate));
			for(int i=0;i<listActionsDated.Count;i++) {
				InsHistAction insHistAction=listActionsDated[i];
				List<long> listCodeNums=ProcedureCodes.GetCodeNumsForInsHistPref(insHistAction.PrefName);
				List<Procedure> listProceduresShown=GetMostRecentProcs(listProcedures,listCodeNums,insHistAction.PrefName);
				if(listProceduresShown.Any(x => x.ProcDate.Date==insHistAction.DateNew)) {
					continue;
				}
				listNotes.Add(GetCategoryName(insHistAction.PrefName)+": "+insHistAction.DateNew.ToShortDateString()+" "
					+Lans.g("InsHistDates","was saved but isn't shown, because more recent procedures are on file."));
			}
			return listNotes;
		}
	}

	///<summary>One date box of the Insurance History window: the procedure it showed when the window loaded, and the date it holds now.</summary>
	public class InsHistBox {
		///<summary>The EO or C procedure the box showed when the window loaded. Null for an empty box.</summary>
		public Procedure ProcedureLoaded;
		///<summary>The date in the box now. MinValue when the box is blank.</summary>
		public DateTime DateEntered=DateTime.MinValue;

		///<summary>The date the box showed when the window loaded. MinValue for an empty box, and for a procedure without a valid date (shown blank,
		///as 24.3 showed it as "No History").</summary>
		public DateTime GetDateLoaded() {
			if(ProcedureLoaded==null || ProcedureLoaded.ProcDate.Year<1880) {
				return DateTime.MinValue;
			}
			return ProcedureLoaded.ProcDate.Date;
		}

		///<summary>Dates from completed procedures (and any status other than EO) can't be changed here; they are edited in the Chart.</summary>
		public bool IsReadOnly() {
			return ProcedureLoaded!=null && ProcedureLoaded.ProcStatus!=ProcStat.EO;
		}

		///<summary>True when the user changed the date of a box that can be changed.</summary>
		public bool IsChanged() {
			return !IsReadOnly() && DateEntered.Date!=GetDateLoaded();
		}

		///<summary>The date this box stands for once the window is saved. MinValue for a box that ends up blank.</summary>
		public DateTime GetDateSaved() {
			if(IsReadOnly()) {
				return GetDateLoaded();
			}
			return DateEntered.Date;
		}
	}

	public enum InsHistActionType {
		///<summary>A date entered in an empty box: a new EO procedure and InsHist claimproc.</summary>
		Insert,
		///<summary>A new date for an EO procedure.</summary>
		ChangeDate,
		///<summary>An EO procedure whose date was removed: the procedure is deleted.</summary>
		Remove,
	}

	///<summary>One change that saving the Insurance History window makes.</summary>
	public class InsHistAction {
		public PrefName PrefName;
		public InsHistActionType ActionType;
		///<summary>The EO procedure that is changed or removed. Null for Insert.</summary>
		public Procedure Procedure;
		///<summary>The new date for Insert and ChangeDate. MinValue for Remove.</summary>
		public DateTime DateNew;
	}
}
