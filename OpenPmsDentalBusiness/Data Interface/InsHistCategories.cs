// Insurance History categories from code groups (Open Dental 26.1, package M2-HIST in docs/specs/port-backlog.md): which rows the Insurance
// History window and the Family module show, which procedures each row reads, the procedure a typed date creates, the eligibility (271) mapping,
// the codes frequency checks take from history, and the rules of the insurance history options in Code Group Edit.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>One row of the Insurance History window and of the insurance history rows in the Family module (Open Dental 26.1): a code group marked
	///Show In History, or one quadrant of a group that is also marked Perio Four Quadrants. Before 26.1 the rows were fixed and their codes came from
	///the ten InsHist*Codes preferences. Built from the code group cache by InsHistCategories.GetAll; not stored.</summary>
	[Serializable]
	public class InsHistCategory {
		///<summary>The code group the row comes from.</summary>
		public long CodeGroupNum;
		///<summary>Blank for a whole-mouth row. UL, UR, LL or LR for one of the four rows of a Perio Four Quadrants group: the history procedures of
		///that row have the quadrant in their Surf.</summary>
		public string Quadrant="";
		///<summary>The row label: the group name, or for a quadrant row the group name and the quadrant (see InsHistCategories.GetDescription).</summary>
		public string Description="";
		///<summary>codegroup.HistProcCode: the one procedure code that entering a date uses.</summary>
		public string HistProcCode="";
		///<summary>codegroup.ProcCodes: the codes and ranges whose procedures count as history for the row.</summary>
		public string ProcCodes="";
		///<summary>codegroup.CodeGroupFixed. Eligibility imports match on it first.</summary>
		public EnumCodeGroupFixed CodeGroupFixed;
		///<summary>codegroup.IsHidden (retired for benefits). A retired group still has its history row while Show In History is checked.</summary>
		public bool IsHidden;

		///<summary>Identifies the row, for example in a grid row's Tag: the CodeGroupNum, plus ":" and the quadrant for a quadrant row.</summary>
		public string Key {
			get {
				if(string.IsNullOrEmpty(Quadrant)) {
					return CodeGroupNum.ToString();
				}
				return CodeGroupNum+":"+Quadrant;
			}
		}

		///<summary>True for one of the four rows of a Perio Four Quadrants group.</summary>
		public bool IsQuadrant {
			get {
				return !string.IsNullOrEmpty(Quadrant);
			}
		}

		///<summary>Whether a procedure with this code and surface is history for the row: the code is in ProcCodes (ranges included) or is the
		///HistProcCode, and for a quadrant row the surface names the quadrant. The HistProcCode always counts, so a date entered in the window is
		///read back even when the group's own list leaves the code out.</summary>
		public bool IsMatch(string procCode,string surf) {
			if(string.IsNullOrWhiteSpace(procCode)) {
				return false;
			}
			procCode=procCode.Trim();
			bool isCodeInRow=ProcCodeRanges.Get(ProcCodes).Contains(procCode)
				|| (!string.IsNullOrWhiteSpace(HistProcCode) && string.Equals(procCode,HistProcCode.Trim(),StringComparison.OrdinalIgnoreCase));
			if(!isCodeInRow) {
				return false;
			}
			if(!IsQuadrant) {
				return true;
			}
			return (surf??"").Contains(Quadrant);//As the 24.3 perio quadrant rows matched.
		}

		public InsHistCategory Copy() {
			return (InsHistCategory)MemberwiseClone();
		}

		public override string ToString() {
			return Description;
		}
	}

	///<summary>What InsHistCategories.InsertOrUpdateProcedure did with a date.</summary>
	public enum InsHistSaveResult {
		///<summary>The date was already on the procedure, or the procedure is complete and the date is not newer.</summary>
		Unchanged,
		///<summary>A new Existing Other procedure with an InsHist claimproc.</summary>
		Inserted,
		///<summary>The date of the Existing Other procedure (and its claimprocs) was changed.</summary>
		Updated,
		///<summary>A new procedure was needed, but the row has no valid History Proc Code. Nothing was saved.</summary>
		InvalidCode,
	}

	///<summary>The Insurance History rows of Open Dental 26.1, taken from code groups (codegroup.ShowInHistory, HistProcCode, IsPerioFourQuads).
	///Replaces the ten fixed rows of 24.3 and their InsHist*Codes preferences, which are kept in the database but no longer read here. The
	///preference-based methods in Procedures and ProcedureCodes stay for older callers.</summary>
	public class InsHistCategories {
		///<summary>The four rows of a Perio Four Quadrants group, in the order they are shown.</summary>
		public static readonly ReadOnlyCollection<string> ListQuadrants=new ReadOnlyCollection<string>(new[] { "UL","UR","LL","LR" });

		#region Perio scaling procedure code (ODOPTS)
		///<summary>Open Dental's preference row (ODOPTS; Open Dental 25.3.48 has it, its meaning is not public): the procedure code Insurance History
		///uses for perio scaling, here the History Proc Code of the rows of every perio scaling code group (CodeGroupFixed SRP). Blank, missing, or a
		///value that names no procedure code: each group's own History Proc Code, the behaviour without the setting (INFERRED). Read and written by
		///name outside PrefName, so the office's value carries over. Like SalesTaxProcCode, a value that is no procedure code but is the CodeNum of one
		///is read too, in case Open Dental stores a CodeNum. docs/port-notes/ODOPTS.md.</summary>
		public const string PREF_PERIO_SCALING_CODE="InsHistPerioScalingCode";

		///<summary>The stored setting, trimmed. Blank when the row is missing or the cache is not loaded.</summary>
		public static string GetPerioScalingCodeSetting() {
			Meth.NoCheckMiddleTierRole();
			return (PrefC.GetRawOrDefault(PREF_PERIO_SCALING_CODE,"")??"").Trim();
		}

		///<summary>The procedure code (as the procedure code list spells it) the setting names, or blank when it is blank or names none.</summary>
		public static string GetPerioScalingProcCode() {
			Meth.NoCheckMiddleTierRole();
			try {
				return GetPerioScalingProcCode(GetPerioScalingCodeSetting(),x => ProcedureCodes.IsValidCode(x) ? ProcedureCodes.GetProcCode(x).ProcCode : "",
					x => ProcedureCodes.GetFirstOrDefault(y => y.CodeNum==x)?.ProcCode??"");
			}
			catch(Exception ex) {
				ex.DoNothing();
				return "";//no procedure code cache (a tool or a test without a database): the behaviour without the setting
			}
		}

		///<summary>Resolves a setting: first as a procedure code (funcGetProcCode returns the code as the list spells it, or blank), then, when it is a
		///number, as the CodeNum of one (funcGetProcCodeByCodeNum). Blank when nothing matches.</summary>
		public static string GetPerioScalingProcCode(string setting,Func<string,string> funcGetProcCode,Func<long,string> funcGetProcCodeByCodeNum) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(setting)) {
				return "";
			}
			string value=setting.Trim();
			string procCode=funcGetProcCode(value)??"";
			if(procCode!="") {
				return procCode;
			}
			if(long.TryParse(value,out long codeNum) && codeNum>0) {
				return funcGetProcCodeByCodeNum(codeNum)??"";
			}
			return "";
		}

		///<summary>Saves the setting as typed (trimmed; blank clears it) and updates this workstation's cache. The caller validates it first
		///(blank, or a procedure code). Returns true if the stored value changed; the caller signals InvalidType.Prefs and makes the log entry.</summary>
		public static bool SetPerioScalingCodeSetting(string procCode) {
			Meth.NoCheckMiddleTierRole();
			return OdOptions.SetString(PREF_PERIO_SCALING_CODE,procCode);
		}
		#endregion Perio scaling procedure code (ODOPTS)

		#region Rows
		///<summary>The rows, from the code group cache: every group marked Show In History, retired (hidden) groups included, in code group order. A Perio
		///Four Quadrants group gives four rows, UL, UR, LL and LR.</summary>
		public static List<InsHistCategory> GetAll() {
			Meth.NoCheckMiddleTierRole();
			return GetAll(CodeGroups.GetDeepCopy());
		}

		///<summary>The rows for the code groups passed in. See GetAll(). The perio scaling rows use InsHistPerioScalingCode when it names a procedure
		///code (GetPerioScalingProcCode).</summary>
		public static List<InsHistCategory> GetAll(List<CodeGroup> listCodeGroups) {
			Meth.NoCheckMiddleTierRole();
			return GetAll(listCodeGroups,GetPerioScalingProcCode());
		}

		///<summary>The rows for the code groups passed in, with perioScalingProcCode as the History Proc Code of every row of a perio scaling group
		///(CodeGroupFixed SRP, ODOPTS): a date entered for such a row creates a procedure with that code, and its procedures count as the row's history.
		///Blank: each group's own History Proc Code, the behaviour without the setting.</summary>
		public static List<InsHistCategory> GetAll(List<CodeGroup> listCodeGroups,string perioScalingProcCode) {
			Meth.NoCheckMiddleTierRole();
			List<InsHistCategory> listInsHistCategories=new List<InsHistCategory>();
			if(listCodeGroups==null) {
				return listInsHistCategories;
			}
			List<CodeGroup> listCodeGroupsHist=listCodeGroups.FindAll(x => x.ShowInHistory).OrderBy(x => x.ItemOrder).ThenBy(x => x.CodeGroupNum).ToList();
			for(int i=0;i<listCodeGroupsHist.Count;i++) {
				if(!listCodeGroupsHist[i].IsPerioFourQuads) {
					listInsHistCategories.Add(CreateCategory(listCodeGroupsHist[i],"",perioScalingProcCode));
					continue;
				}
				for(int q=0;q<ListQuadrants.Count;q++) {
					listInsHistCategories.Add(CreateCategory(listCodeGroupsHist[i],ListQuadrants[q],perioScalingProcCode));
				}
			}
			return listInsHistCategories;
		}

		private static InsHistCategory CreateCategory(CodeGroup codeGroup,string quadrant,string perioScalingProcCode) {
			InsHistCategory insHistCategory=new InsHistCategory();
			insHistCategory.CodeGroupNum=codeGroup.CodeGroupNum;
			insHistCategory.Quadrant=quadrant;
			insHistCategory.Description=GetDescription(codeGroup,quadrant);
			insHistCategory.HistProcCode=(codeGroup.HistProcCode??"").Trim();
			if(codeGroup.CodeGroupFixed==EnumCodeGroupFixed.SRP && !string.IsNullOrWhiteSpace(perioScalingProcCode)) {
				insHistCategory.HistProcCode=perioScalingProcCode.Trim();//ODOPTS: InsHistPerioScalingCode
			}
			insHistCategory.ProcCodes=codeGroup.ProcCodes??"";
			insHistCategory.CodeGroupFixed=codeGroup.CodeGroupFixed;
			insHistCategory.IsHidden=codeGroup.IsHidden;
			return insHistCategory;
		}

		///<summary>The row label. A whole-mouth row shows the group name. A quadrant row of the fixed SRP group shows "Perio Scaling" and the quadrant,
		///as the four 24.3 rows were labeled; a quadrant row of any other group shows its name and the quadrant, so two such groups never share
		///labels.</summary>
		public static string GetDescription(CodeGroup codeGroup,string quadrant) {
			Meth.NoCheckMiddleTierRole();
			if(codeGroup==null) {
				return "";
			}
			if(string.IsNullOrEmpty(quadrant)) {
				return codeGroup.GroupName;
			}
			string name=codeGroup.GroupName;
			if(codeGroup.CodeGroupFixed==EnumCodeGroupFixed.SRP) {
				name=Lans.g("InsHistCategories","Perio Scaling");
			}
			return name+" "+quadrant;
		}

		///<summary>The row with this Key (see InsHistCategory.Key), from the rows passed in or, when null, from the cache. Null if none.</summary>
		public static InsHistCategory GetByKey(string key,List<InsHistCategory> listInsHistCategories=null) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(key)) {
				return null;
			}
			listInsHistCategories=listInsHistCategories??GetAll();
			return listInsHistCategories.FirstOrDefault(x => x.Key==key);
		}
		#endregion Rows

		#region Reading history
		///<summary>The procedures passed in that are history for the row (see InsHistCategory.IsMatch), newest first. Callers pass the procedures they
		///count as history, as before 26.1: EO and C procedures with an InsHist or Received claimproc on the plan. funcGetProcCode turns a CodeNum
		///into its ProcCode; null uses the procedure code cache.</summary>
		public static List<Procedure> GetProcs(List<Procedure> listProcedures,InsHistCategory insHistCategory,Func<long,string> funcGetProcCode=null) {
			Meth.NoCheckMiddleTierRole();
			if(listProcedures==null || insHistCategory==null) {
				return new List<Procedure>();
			}
			if(funcGetProcCode==null) {
				funcGetProcCode=codeNum => ProcedureCodes.GetStringProcCode(codeNum,doThrowIfMissing:false);
			}
			Dictionary<long,string> dictProcCodes=new Dictionary<long,string>();
			List<Procedure> listProceduresMatched=new List<Procedure>();
			for(int i=0;i<listProcedures.Count;i++) {
				if(!dictProcCodes.TryGetValue(listProcedures[i].CodeNum,out string procCode)) {
					procCode=funcGetProcCode(listProcedures[i].CodeNum)??"";
					dictProcCodes[listProcedures[i].CodeNum]=procCode;
				}
				if(insHistCategory.IsMatch(procCode,listProcedures[i].Surf)) {
					listProceduresMatched.Add(listProcedures[i]);
				}
			}
			//Oldest to newest, then reversed: of two procedures on the same day the later one in the list comes first, as 24.3 picked it.
			List<Procedure> listProceduresSorted=listProceduresMatched.OrderBy(x => x.ProcDate).ToList();
			listProceduresSorted.Reverse();
			return listProceduresSorted;
		}

		///<summary>The most recent history procedure of the row, or null. See GetProcs.</summary>
		public static Procedure GetMostRecentProc(List<Procedure> listProcedures,InsHistCategory insHistCategory,Func<long,string> funcGetProcCode=null) {
			Meth.NoCheckMiddleTierRole();
			return GetProcs(listProcedures,insHistCategory,funcGetProcCode).FirstOrDefault();
		}

		///<summary>The CodeNums of the row's codes (ranges expanded) and of its HistProcCode. The quadrant is not part of it.</summary>
		public static List<long> GetCodeNums(InsHistCategory insHistCategory) {
			Meth.NoCheckMiddleTierRole();
			if(insHistCategory==null) {
				return new List<long>();
			}
			List<long> listCodeNums=ProcedureCodes.GetCodeNumsForProcCodes(insHistCategory.ProcCodes);
			ProcedureCode procedureCode=GetHistProcCode(insHistCategory);
			if(procedureCode.CodeNum!=0 && !listCodeNums.Contains(procedureCode.CodeNum)) {
				listCodeNums.Add(procedureCode.CodeNum);
			}
			return listCodeNums;
		}
		#endregion Reading history

		#region Entering history
		///<summary>The procedure code a date entered for the row uses (HistProcCode). A new ProcedureCode with CodeNum 0 when the code is blank or not in
		///the procedure code list.</summary>
		public static ProcedureCode GetHistProcCode(InsHistCategory insHistCategory) {
			Meth.NoCheckMiddleTierRole();
			if(insHistCategory==null || string.IsNullOrWhiteSpace(insHistCategory.HistProcCode)) {
				return new ProcedureCode();
			}
			string procCode=insHistCategory.HistProcCode.Trim();
			if(!ProcedureCodes.IsValidCode(procCode)) {
				return new ProcedureCode();
			}
			return ProcedureCodes.GetProcCode(procCode);
		}

		///<summary>Whether the row can create procedures: its HistProcCode is a code in the procedure code list.</summary>
		public static bool HasValidHistProcCode(InsHistCategory insHistCategory) {
			Meth.NoCheckMiddleTierRole();
			return GetHistProcCode(insHistCategory).CodeNum!=0;
		}

		///<summary>A new Existing Other procedure (not inserted) for the date, with the row's HistProcCode and, for a quadrant row, the quadrant as its
		///surface. Like 24.3: the patient's clinic and site, the primary provider (or the secondary, or the practice default), and the code's default
		///diagnosis, base units and revenue code.</summary>
		public static Procedure CreateProcedure(Patient patient,DateTime date,InsHistCategory insHistCategory) {
			Meth.NoCheckMiddleTierRole();
			ProcedureCode procedureCode=GetHistProcCode(insHistCategory);
			Procedure procedure=new Procedure();
			procedure.CodeNum=procedureCode.CodeNum;
			procedure.PatNum=patient.PatNum;
			procedure.ProcDate=date;
			procedure.DateTP=date;
			procedure.ProcStatus=ProcStat.EO;
			procedure.ProvNum=patient.PriProv;
			procedure.BaseUnits=procedureCode.BaseUnits;
			procedure.RevCode=procedureCode.RevenueCodeDefault;
			if(procedure.ProvNum==0) {
				procedure.ProvNum=patient.SecProv;
			}
			if(procedure.ProvNum==0) {
				procedure.ProvNum=PrefC.GetLong(PrefName.PracticeDefaultProv);
			}
			procedure.ClinicNum=patient.ClinicNum;
			procedure.SiteNum=patient.SiteNum;
			Procedures.SetDiagnosticCodesToDefault(procedure,procedureCode);
			procedure.PlaceService=Clinics.GetPlaceService(procedure.ClinicNum);
			procedure.Surf=insHistCategory.Quadrant??"";
			return procedure;
		}

		///<summary>Saves a date for the row, as 24.3 did for its fixed rows. procedure is the row's most recent history procedure (or null) and
		///listClaimProcsForProc its claimprocs on the plan.
		///- No procedure, or a completed one older than the date: a new Existing Other procedure with an InsHist claimproc, then the recall is
		///recalculated. A date that is not newer than a completed procedure changes nothing.
		///- An Existing Other procedure on another day: its date and its claimprocs' dates move (claimprocs of this plan become InsHist), or it gets an
		///InsHist claimproc when it has none, then the recall is recalculated (INFERRED: 24.3 only did this for new procedures).
		///A new procedure needs a valid HistProcCode; without one nothing is saved and InvalidCode is returned. doSynchRecalls false leaves the recall to
		///the caller, which synchs it once after several dates (InsHistDates.ApplyActions).</summary>
		public static InsHistSaveResult InsertOrUpdateProcedure(Patient patient,InsHistCategory insHistCategory,DateTime date,long planNum,long insSubNum,
			Procedure procedure,List<ClaimProc> listClaimProcsForProc,bool doSynchRecalls=true)
		{
			Meth.NoCheckMiddleTierRole();
			listClaimProcsForProc=listClaimProcsForProc??new List<ClaimProc>();
			if(procedure==null || (procedure.ProcStatus!=ProcStat.EO && date.Date>procedure.ProcDate.Date)) {
				if(!HasValidHistProcCode(insHistCategory)) {
					return InsHistSaveResult.InvalidCode;
				}
				Procedure procedureNew=CreateProcedure(patient,date,insHistCategory);
				Procedures.Insert(procedureNew);
				ClaimProcs.InsertClaimProcForInsHist(procedureNew,planNum,insSubNum);
				if(doSynchRecalls) {
					Recalls.Synch(patient.PatNum);
				}
				return InsHistSaveResult.Inserted;
			}
			if(procedure.ProcStatus!=ProcStat.EO || date.Date==procedure.ProcDate.Date) {
				return InsHistSaveResult.Unchanged;
			}
			Procedure procedureOld=procedure.Copy();
			procedure.ProcDate=date;
			Procedures.Update(procedure,procedureOld);
			if(listClaimProcsForProc.Count>0) {
				ClaimProcs.UpdateClaimProcForInsHist(listClaimProcsForProc.FindAll(x => x.ProcDate!=date || x.Status!=ClaimProcStatus.InsHist),date,insSubNum);
			}
			else {
				ClaimProcs.InsertClaimProcForInsHist(procedure,planNum,insSubNum);
			}
			if(doSynchRecalls) {
				Recalls.Synch(patient.PatNum);
			}
			return InsHistSaveResult.Updated;
		}
		#endregion Entering history

		#region Eligibility (271)
		///<summary>The row an insurance history date from an eligibility response (271) goes to. First the row of a code group whose CodeGroupFixed is
		///the category the response names (codeGroupFixed); otherwise the row whose HistProcCode is the procedure code the response names (procCode).
		///Quadrant rows are never picked, because a response does not name a quadrant. Null when no row matches, with the reason.</summary>
		public static InsHistCategory GetForEligibility(List<InsHistCategory> listInsHistCategories,EnumCodeGroupFixed codeGroupFixed,string procCode,
			out string reason)
		{
			Meth.NoCheckMiddleTierRole();
			reason="";
			listInsHistCategories=listInsHistCategories??new List<InsHistCategory>();
			bool isQuadrantOnly=false;
			if(codeGroupFixed!=EnumCodeGroupFixed.None) {
				List<InsHistCategory> listMatches=listInsHistCategories.FindAll(x => x.CodeGroupFixed==codeGroupFixed);
				InsHistCategory insHistCategory=listMatches.FirstOrDefault(x => !x.IsQuadrant);
				if(insHistCategory!=null) {
					return insHistCategory;
				}
				isQuadrantOnly=listMatches.Count>0;
			}
			procCode=(procCode??"").Trim();
			if(procCode!="") {
				List<InsHistCategory> listMatches=listInsHistCategories.FindAll(x => string.Equals(x.HistProcCode.Trim(),procCode,StringComparison.OrdinalIgnoreCase));
				InsHistCategory insHistCategory=listMatches.FirstOrDefault(x => !x.IsQuadrant);
				if(insHistCategory!=null) {
					return insHistCategory;
				}
				isQuadrantOnly|=listMatches.Count>0;
			}
			if(isQuadrantOnly) {
				reason=Lans.g("InsHistCategories","the matching code group has four quadrant rows and the response names no quadrant");
				return null;
			}
			reason=Lans.g("InsHistCategories","no code group marked Show In History matches");
			return null;
		}
		#endregion Eligibility (271)

		#region Frequency limitations
		///<summary>The code group category of each of the ten InsHist*Codes preferences (the four perio quadrant preferences: SRP). None for any other
		///preference.</summary>
		public static EnumCodeGroupFixed GetCodeGroupFixed(PrefName prefName) {
			Meth.NoCheckMiddleTierRole();
			switch(prefName) {
				case PrefName.InsHistBWCodes:
					return EnumCodeGroupFixed.BW;
				case PrefName.InsHistPanoCodes:
					return EnumCodeGroupFixed.PanoFMX;
				case PrefName.InsHistExamCodes:
					return EnumCodeGroupFixed.Exam;
				case PrefName.InsHistProphyCodes:
					return EnumCodeGroupFixed.Prophy;
				case PrefName.InsHistPerioURCodes:
				case PrefName.InsHistPerioULCodes:
				case PrefName.InsHistPerioLRCodes:
				case PrefName.InsHistPerioLLCodes:
					return EnumCodeGroupFixed.SRP;
				case PrefName.InsHistPerioMaintCodes:
					return EnumCodeGroupFixed.Perio;
				case PrefName.InsHistDebridementCodes:
					return EnumCodeGroupFixed.FMDebride;
				default:
					return EnumCodeGroupFixed.None;
			}
		}

		///<summary>What frequency checks used the obsolete InsHist*Codes preference for, from Open Dental 26.1: the codes of the history rows whose code
		///group has the preference's category (GetCodeGroupFixed), HistProcCode included. Empty when no such row.</summary>
		public static List<long> GetCodeNumsForInsHistPref(PrefName prefName) {
			Meth.NoCheckMiddleTierRole();
			EnumCodeGroupFixed codeGroupFixed=GetCodeGroupFixed(prefName);
			List<long> listCodeNums=new List<long>();
			if(codeGroupFixed==EnumCodeGroupFixed.None) {
				return listCodeNums;
			}
			List<InsHistCategory> listInsHistCategories=GetAll().FindAll(x => x.CodeGroupFixed==codeGroupFixed);
			for(int i=0;i<listInsHistCategories.Count;i++) {
				if(listInsHistCategories.Take(i).Any(x => x.CodeGroupNum==listInsHistCategories[i].CodeGroupNum)) {
					continue;//The other quadrant rows of the same group have the same codes.
				}
				listCodeNums.AddRange(GetCodeNums(listInsHistCategories[i]).Where(x => !listCodeNums.Contains(x)));
			}
			return listCodeNums;
		}
		#endregion Frequency limitations
	}

	///<summary>The rules of the insurance history options in Code Group Edit (Open Dental 26.1): Show In History, History Proc Code and Perio Four
	///Quadrants, plus the log text for changes to them.</summary>
	public class CodeGroupHistoryOptions {
		///<summary>Why the options can not be saved, or "" when they can. History Proc Code is required when Show In History is checked and must be a
		///code in the procedure code list (funcIsValidCode; null uses the cache); Perio Four Quadrants requires Show In History.</summary>
		public static string GetError(bool showInHistory,string histProcCode,bool isPerioFourQuads,Func<string,bool> funcIsValidCode=null) {
			Meth.NoCheckMiddleTierRole();
			histProcCode=(histProcCode??"").Trim();
			if(!showInHistory) {
				if(isPerioFourQuads) {
					return Lans.g("CodeGroupHistoryOptions","Perio Four Quadrants requires Show In History.");
				}
				return "";
			}
			if(histProcCode=="") {
				return Lans.g("CodeGroupHistoryOptions","History Proc Code is required when Show In History is checked.");
			}
			if(histProcCode.Contains(",") || ProcCodeRanges.TryParseRange(histProcCode,out ProcCodeRange _)) {
				return Lans.g("CodeGroupHistoryOptions","History Proc Code must be a single procedure code.");
			}
			funcIsValidCode=funcIsValidCode??ProcedureCodes.IsValidCode;
			if(!funcIsValidCode(histProcCode)) {
				return Lans.g("CodeGroupHistoryOptions","History Proc Code is not a valid procedure code:")+" "+histProcCode;
			}
			return "";
		}

		///<summary>A warning when the History Proc Code is not one of the group's own codes (ranges included), or "". The code still counts as history
		///for the group (InsHistCategory.IsMatch), but frequency limitations only count the group's own codes.</summary>
		public static string GetWarning(bool showInHistory,string histProcCode,string procCodes) {
			Meth.NoCheckMiddleTierRole();
			histProcCode=(histProcCode??"").Trim();
			if(!showInHistory || histProcCode=="") {
				return "";
			}
			if(ProcCodeRanges.Get(procCodes).Contains(histProcCode)) {
				return "";
			}
			return Lans.g("CodeGroupHistoryOptions","The History Proc Code is not in this group's Proc Codes, so frequency limitations that use the group "
				+"will not count the history it creates.");
		}

		///<summary>The values saved for the options: History Proc Code trimmed and cleared, and Perio Four Quadrants cleared, when Show In History is
		///not checked.</summary>
		public static void Apply(CodeGroup codeGroup,bool showInHistory,string histProcCode,bool isPerioFourQuads) {
			Meth.NoCheckMiddleTierRole();
			codeGroup.ShowInHistory=showInHistory;
			codeGroup.HistProcCode=showInHistory ? (histProcCode??"").Trim() : "";
			codeGroup.IsPerioFourQuads=showInHistory && isPerioFourQuads;
		}

		///<summary>The changes to the options between two versions of a code group, for the "Code group ... edited" security log entry. Empty when none
		///changed.</summary>
		public static List<string> GetLogChanges(CodeGroup codeGroupOld,CodeGroup codeGroup) {
			Meth.NoCheckMiddleTierRole();
			List<string> listChanges=new List<string>();
			if(codeGroupOld==null || codeGroup==null) {
				return listChanges;
			}
			if(codeGroupOld.ShowInHistory!=codeGroup.ShowInHistory) {
				listChanges.Add(codeGroup.ShowInHistory ? Lans.g("CodeGroupHistoryOptions","shown in history")
					: Lans.g("CodeGroupHistoryOptions","removed from history"));
			}
			if((codeGroupOld.HistProcCode??"")!=(codeGroup.HistProcCode??"")) {
				listChanges.Add(Lans.g("CodeGroupHistoryOptions","history proc code from")+" '"+codeGroupOld.HistProcCode+"' "
					+Lans.g("CodeGroupHistoryOptions","to")+" '"+codeGroup.HistProcCode+"'");
			}
			if(codeGroupOld.IsPerioFourQuads!=codeGroup.IsPerioFourQuads) {
				listChanges.Add(codeGroup.IsPerioFourQuads ? Lans.g("CodeGroupHistoryOptions","perio four quadrants on")
					: Lans.g("CodeGroupHistoryOptions","perio four quadrants off"));
			}
			return listChanges;
		}
	}
}
