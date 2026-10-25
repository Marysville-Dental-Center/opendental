// Carriers flag procedures that need a preauthorization (Open Dental 26.1, feature v26_1-lists-carrier-preauth-flags, package M2-CARRIER in
// docs/specs/port-backlog.md). The list lives in carrier.PreAuthCodes (M2-01's column). This class holds the rules: how the list is typed, checked,
// shown and merged, and which treatment-planned procedures still need a preauthorization. The Chart, Treatment Plan and Appointments modules call it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Rules for carrier.PreAuthCodes: procedure codes and code ranges a carrier wants preauthorized. The stored text has commas between
	///entries, a plain hyphen inside a range and no spaces, for example "D0100-D0999,D2140-D2161,D2750". Ranges and codes are matched with the shared
	///parser of code groups (ProcCodeRanges): a range holds every code with the same letter prefix and a numeric part of the same length between its
	///ends, and a single code matches whole, without regard to case.</summary>
	public class CarrierPreAuths {
		///<summary>U+2011, the non-breaking hyphen. Edit Carrier shows ranges with it so a range never wraps in the middle; it is stored as '-'.</summary>
		public const char NonBreakingHyphen='‑';

		#region Code list text
		///<summary>The stored list as Edit Carrier shows it: every plain hyphen becomes a non-breaking hyphen. Null gives "".</summary>
		public static string ToDisplay(string preAuthCodes) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(preAuthCodes)) {
				return "";
			}
			return preAuthCodes.Replace('-',NonBreakingHyphen);
		}

		///<summary>Text from Edit Carrier back to the stored form: non-breaking hyphens, and the other dash characters a paste can bring (en dash, em
		///dash, minus sign), become plain hyphens. Nothing else changes. Null gives "".</summary>
		public static string FromDisplay(string text) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(text)) {
				return "";
			}
			return text.Replace(NonBreakingHyphen,'-').Replace('‐','-').Replace('–','-').Replace('—','-').Replace('−','-');
		}

		///<summary>Checks the text typed into Edit Carrier (display or stored form) before it is saved. Refuses spaces inside the list, empty entries
		///(two commas in a row, or a comma at the start or end) and ranges whose start is after their end. Whitespace around the whole text is
		///ignored, a repeated entry is dropped (first one kept, without regard to case). Codes that are not in the procedure code list are allowed but
		///listed, so the window can warn; ranges are not looked up, because their ends do not have to exist. Pass funcCodeExists to look codes up
		///somewhere other than the procedure code cache (tests).</summary>
		public static PreAuthCodesCheck Check(string text,Func<string,bool> funcCodeExists=null) {
			Meth.NoCheckMiddleTierRole();
			PreAuthCodesCheck preAuthCodesCheck=new PreAuthCodesCheck();
			string codes=FromDisplay(text).Trim();
			if(codes=="") {
				return preAuthCodesCheck;
			}
			if(codes.Any(x => char.IsWhiteSpace(x))) {
				preAuthCodesCheck.Error=Lans.g("CarrierPreAuths","Spaces are not allowed in the preauthorization list. Separate codes and ranges with commas only.");
				return preAuthCodesCheck;
			}
			if(funcCodeExists==null) {
				funcCodeExists=DoesProcCodeExist;
			}
			string[] arrayEntries=codes.Split(',');
			List<string> listEntries=new List<string>();
			HashSet<string> hashSetSeen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for(int i=0;i<arrayEntries.Length;i++) {
				string entry=arrayEntries[i];
				if(entry=="") {
					preAuthCodesCheck.Error=Lans.g("CarrierPreAuths","The preauthorization list has an empty entry: two commas in a row, or a comma at the start or end.");
					return preAuthCodesCheck;
				}
				if(ProcCodeRanges.TryParseRange(entry,out ProcCodeRange procCodeRange)) {
					if(procCodeRange.Start>procCodeRange.End) {
						preAuthCodesCheck.Error=Lans.g("CarrierPreAuths","The start of this range is after its end:")+" "+ToDisplay(entry);
						return preAuthCodesCheck;
					}
				}
				else if(!funcCodeExists(entry) && !preAuthCodesCheck.ListUnknownCodes.Contains(entry,StringComparer.OrdinalIgnoreCase)) {
					preAuthCodesCheck.ListUnknownCodes.Add(entry);
				}
				if(hashSetSeen.Add(entry)) {
					listEntries.Add(entry);
				}
			}
			preAuthCodesCheck.Codes=string.Join(",",listEntries);
			return preAuthCodesCheck;
		}

		///<summary>Whether the code is in the procedure code list (cache), without regard to case.</summary>
		private static bool DoesProcCodeExist(string procCode) {
			if(ProcedureCodes.IsValidCode(procCode)) {
				return true;
			}
			return ProcedureCodes.GetWhereFromList(x => string.Equals(x.ProcCode,procCode,StringComparison.OrdinalIgnoreCase)).Count>0;
		}

		///<summary>The union of two stored lists for Carrier Combine: the entries of codesKept in their order, then each entry of the other lists that
		///is not there yet (compared without regard to case), in order. Blank lists add nothing.</summary>
		public static string Merge(string codesKept,IEnumerable<string> listCodesOther) {
			Meth.NoCheckMiddleTierRole();
			List<string> listEntries=new List<string>();
			HashSet<string> hashSetSeen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			List<string> listLists=new List<string> { codesKept };
			if(listCodesOther!=null) {
				listLists.AddRange(listCodesOther);
			}
			for(int i=0;i<listLists.Count;i++) {
				if(string.IsNullOrWhiteSpace(listLists[i])) {
					continue;
				}
				foreach(string entryRaw in listLists[i].Split(',')) {
					string entry=entryRaw.Trim();
					if(entry!="" && hashSetSeen.Add(entry)) {
						listEntries.Add(entry);
					}
				}
			}
			return string.Join(",",listEntries);
		}
		#endregion Code list text

		#region Matching
		///<summary>True when the carrier's list holds the procedure code (as a code or inside a range). False for a null carrier or a blank code.</summary>
		public static bool DoesCarrierRequire(Carrier carrier,string procCode) {
			Meth.NoCheckMiddleTierRole();
			if(carrier==null || string.IsNullOrWhiteSpace(carrier.PreAuthCodes) || string.IsNullOrWhiteSpace(procCode)) {
				return false;
			}
			return ProcCodeRanges.Get(carrier.PreAuthCodes).Contains(procCode);
		}

		///<summary>True when at least one carrier in the cache has a preauthorization list. Every caller checks this first, so an office that does not
		///use the feature runs no extra query.</summary>
		public static bool IsInUse() {
			Meth.NoCheckMiddleTierRole();
			return Carriers.GetFirstOrDefault(x => !string.IsNullOrWhiteSpace(x.PreAuthCodes))!=null;
		}

		///<summary>The treatment-planned procedures that still need a preauthorization, one entry per procedure and insurance plan. A procedure needs
		///one when (1) its status is TP or TPi and it is not a Canadian lab procedure, (2) one of the patient's plans (patplan) belongs to a carrier
		///whose list holds the procedure code, (3) no preauth claimproc (status Preauth, sent or not) exists for that procedure and that plan, and
		///(4) the procedure's estimate for that plan is not marked Do Not Bill Ins. Entries are ordered by procedure, then by the plan's ordinal.
		///Pure: the lists passed in are all it reads, besides funcGetCarrier and funcGetProcCode.</summary>
		public static List<PreAuthNeed> GetNeeds(List<Procedure> listProcedures,List<PatPlan> listPatPlans,List<InsSub> listInsSubs,
			List<InsPlan> listInsPlans,List<ClaimProc> listClaimProcs,Func<long,Carrier> funcGetCarrier,Func<long,string> funcGetProcCode)
		{
			Meth.NoCheckMiddleTierRole();
			List<PreAuthNeed> listPreAuthNeeds=new List<PreAuthNeed>();
			if(listProcedures.IsNullOrEmpty() || listPatPlans.IsNullOrEmpty() || listInsSubs.IsNullOrEmpty() || listInsPlans.IsNullOrEmpty()) {
				return listPreAuthNeeds;
			}
			listClaimProcs=listClaimProcs??new List<ClaimProc>();
			HashSet<long> hashSetProcNums=new HashSet<long>();
			for(int i=0;i<listProcedures.Count;i++) {
				Procedure procedure=listProcedures[i];
				if(procedure==null || !hashSetProcNums.Add(procedure.ProcNum)) {
					continue;
				}
				if(!procedure.ProcStatus.In(ProcStat.TP,ProcStat.TPi) || procedure.ProcNumLab!=0) {
					continue;
				}
				string procCode=funcGetProcCode(procedure.CodeNum);
				if(string.IsNullOrWhiteSpace(procCode)) {
					continue;
				}
				List<PatPlan> listPatPlansPat=listPatPlans.FindAll(x => x.PatNum==procedure.PatNum).OrderBy(x => x.Ordinal).ToList();
				for(int p=0;p<listPatPlansPat.Count;p++) {
					InsSub insSub=listInsSubs.Find(x => x.InsSubNum==listPatPlansPat[p].InsSubNum);
					if(insSub==null) {
						continue;
					}
					InsPlan insPlan=listInsPlans.Find(x => x.PlanNum==insSub.PlanNum);
					if(insPlan==null) {
						continue;
					}
					if(listPreAuthNeeds.Any(x => x.ProcNum==procedure.ProcNum && x.PlanNum==insPlan.PlanNum)) {
						continue;//The same plan through a second subscription.
					}
					Carrier carrier=funcGetCarrier(insPlan.CarrierNum);
					if(!DoesCarrierRequire(carrier,procCode)) {
						continue;
					}
					if(listClaimProcs.Any(x => x.ProcNum==procedure.ProcNum && x.PlanNum==insPlan.PlanNum && x.Status==ClaimProcStatus.Preauth)) {
						continue;//A preauthorization exists, sent or not.
					}
					if(listClaimProcs.Any(x => x.ProcNum==procedure.ProcNum && x.PlanNum==insPlan.PlanNum && x.Status==ClaimProcStatus.Estimate && x.NoBillIns)) {
						continue;//Not billed to this plan.
					}
					PreAuthNeed preAuthNeed=new PreAuthNeed();
					preAuthNeed.ProcNum=procedure.ProcNum;
					preAuthNeed.PatNum=procedure.PatNum;
					preAuthNeed.AptNum=procedure.AptNum;
					preAuthNeed.PlannedAptNum=procedure.PlannedAptNum;
					preAuthNeed.CodeNum=procedure.CodeNum;
					preAuthNeed.ProcCode=procCode;
					preAuthNeed.ToothNum=procedure.ToothNum??"";
					preAuthNeed.Surf=procedure.Surf??"";
					preAuthNeed.PatPlanNum=listPatPlansPat[p].PatPlanNum;
					preAuthNeed.Ordinal=listPatPlansPat[p].Ordinal;
					preAuthNeed.InsSubNum=insSub.InsSubNum;
					preAuthNeed.PlanNum=insPlan.PlanNum;
					preAuthNeed.CarrierNum=carrier.CarrierNum;
					preAuthNeed.CarrierName=carrier.CarrierName??"";
					listPreAuthNeeds.Add(preAuthNeed);
				}
			}
			return listPreAuthNeeds;
		}

		///<summary>GetNeeds with the carrier and procedure code caches.</summary>
		public static List<PreAuthNeed> GetNeeds(List<Procedure> listProcedures,List<PatPlan> listPatPlans,List<InsSub> listInsSubs,
			List<InsPlan> listInsPlans,List<ClaimProc> listClaimProcs)
		{
			Meth.NoCheckMiddleTierRole();
			return GetNeeds(listProcedures,listPatPlans,listInsSubs,listInsPlans,listClaimProcs,
				x => Carriers.GetCarrier(x),
				x => ProcedureCodes.GetStringProcCode(x,doThrowIfMissing:false));
		}
		#endregion Matching

		#region Database
		///<summary>The procedures passed in (by ProcNum) that still need a preauthorization, read fresh from the database: the procedures, the
		///patients' plans and the preauth and estimate claimprocs. Empty when no carrier has a list.</summary>
		public static List<PreAuthNeed> GetNeedsForProcNums(List<long> listProcNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<PreAuthNeed>>(MethodBase.GetCurrentMethod(),listProcNums);
			}
			if(listProcNums.IsNullOrEmpty() || !IsInUse()) {
				return new List<PreAuthNeed>();
			}
			string command="SELECT ProcNum,PatNum,AptNum,PlannedAptNum,CodeNum,ProcStatus,ProcNumLab,ToothNum,Surf FROM procedurelog "
				+"WHERE ProcNum IN("+string.Join(",",listProcNums.Distinct().Select(x => POut.Long(x)))+") "
				+"AND ProcStatus IN("+POut.Int((int)ProcStat.TP)+","+POut.Int((int)ProcStat.TPi)+")";
			return GetNeedsForProcedureTable(Db.GetTable(command));
		}

		///<summary>The procedures attached to the appointments passed in (by AptNum, or by PlannedAptNum when isPlanned) that still need a
		///preauthorization. Empty when no carrier has a list. Used for the PreAuthFlag appointment element and bubble field.</summary>
		public static List<PreAuthNeed> GetNeedsForAppointments(List<long> listAptNums,bool isPlanned) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<PreAuthNeed>>(MethodBase.GetCurrentMethod(),listAptNums,isPlanned);
			}
			listAptNums=(listAptNums??new List<long>()).FindAll(x => x>0).Distinct().ToList();
			if(listAptNums.Count==0 || !IsInUse()) {
				return new List<PreAuthNeed>();
			}
			string columnApt=isPlanned ? "PlannedAptNum" : "AptNum";
			string command="SELECT ProcNum,PatNum,AptNum,PlannedAptNum,CodeNum,ProcStatus,ProcNumLab,ToothNum,Surf FROM procedurelog "
				+"WHERE "+columnApt+" IN("+string.Join(",",listAptNums.Select(x => POut.Long(x)))+") "
				+"AND ProcStatus IN("+POut.Int((int)ProcStat.TP)+","+POut.Int((int)ProcStat.TPi)+")";
			return GetNeedsForProcedureTable(Db.GetTable(command));
		}

		///<summary>The preauth claimprocs, and the estimates marked Do Not Bill Ins, of the procedures passed in: all GetNeeds reads from claimproc.</summary>
		public static List<ClaimProc> GetClaimProcsForNeeds(List<long> listProcNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<ClaimProc>>(MethodBase.GetCurrentMethod(),listProcNums);
			}
			List<ClaimProc> listClaimProcs=new List<ClaimProc>();
			if(listProcNums.IsNullOrEmpty()) {
				return listClaimProcs;
			}
			string command="SELECT ProcNum,PlanNum,InsSubNum,Status,NoBillIns FROM claimproc "
				+"WHERE ProcNum IN("+string.Join(",",listProcNums.Distinct().Select(x => POut.Long(x)))+") "
				+"AND (Status="+POut.Int((int)ClaimProcStatus.Preauth)+" OR (Status="+POut.Int((int)ClaimProcStatus.Estimate)+" AND NoBillIns=1))";
			DataTable table=Db.GetTable(command);
			for(int i=0;i<table.Rows.Count;i++) {
				ClaimProc claimProc=new ClaimProc();
				claimProc.ProcNum=PIn.Long(table.Rows[i]["ProcNum"].ToString());
				claimProc.PlanNum=PIn.Long(table.Rows[i]["PlanNum"].ToString());
				claimProc.InsSubNum=PIn.Long(table.Rows[i]["InsSubNum"].ToString());
				claimProc.Status=(ClaimProcStatus)PIn.Int(table.Rows[i]["Status"].ToString());
				claimProc.NoBillIns=PIn.Bool(table.Rows[i]["NoBillIns"].ToString());
				listClaimProcs.Add(claimProc);
			}
			return listClaimProcs;
		}

		///<summary>Reads the patients' plans and the claimprocs for the procedure rows and applies GetNeeds. The table has the columns of the two
		///procedurelog queries above.</summary>
		private static List<PreAuthNeed> GetNeedsForProcedureTable(DataTable tableProcs) {
			List<Procedure> listProcedures=new List<Procedure>();
			for(int i=0;i<tableProcs.Rows.Count;i++) {
				Procedure procedure=new Procedure();
				procedure.ProcNum=PIn.Long(tableProcs.Rows[i]["ProcNum"].ToString());
				procedure.PatNum=PIn.Long(tableProcs.Rows[i]["PatNum"].ToString());
				procedure.AptNum=PIn.Long(tableProcs.Rows[i]["AptNum"].ToString());
				procedure.PlannedAptNum=PIn.Long(tableProcs.Rows[i]["PlannedAptNum"].ToString());
				procedure.CodeNum=PIn.Long(tableProcs.Rows[i]["CodeNum"].ToString());
				procedure.ProcStatus=(ProcStat)PIn.Int(tableProcs.Rows[i]["ProcStatus"].ToString());
				procedure.ProcNumLab=PIn.Long(tableProcs.Rows[i]["ProcNumLab"].ToString());
				procedure.ToothNum=PIn.String(tableProcs.Rows[i]["ToothNum"].ToString());
				procedure.Surf=PIn.String(tableProcs.Rows[i]["Surf"].ToString());
				listProcedures.Add(procedure);
			}
			if(listProcedures.Count==0) {
				return new List<PreAuthNeed>();
			}
			List<long> listPatNums=listProcedures.Select(x => x.PatNum).Distinct().ToList();
			string command="SELECT patplan.PatPlanNum,patplan.PatNum,patplan.Ordinal,patplan.InsSubNum,inssub.PlanNum,insplan.CarrierNum "
				+"FROM patplan "
				+"INNER JOIN inssub ON inssub.InsSubNum=patplan.InsSubNum "
				+"INNER JOIN insplan ON insplan.PlanNum=inssub.PlanNum "
				+"WHERE patplan.PatNum IN("+string.Join(",",listPatNums.Select(x => POut.Long(x)))+")";
			DataTable tablePlans=Db.GetTable(command);
			List<PatPlan> listPatPlans=new List<PatPlan>();
			List<InsSub> listInsSubs=new List<InsSub>();
			List<InsPlan> listInsPlans=new List<InsPlan>();
			for(int i=0;i<tablePlans.Rows.Count;i++) {
				PatPlan patPlan=new PatPlan();
				patPlan.PatPlanNum=PIn.Long(tablePlans.Rows[i]["PatPlanNum"].ToString());
				patPlan.PatNum=PIn.Long(tablePlans.Rows[i]["PatNum"].ToString());
				patPlan.Ordinal=PIn.Byte(tablePlans.Rows[i]["Ordinal"].ToString());
				patPlan.InsSubNum=PIn.Long(tablePlans.Rows[i]["InsSubNum"].ToString());
				listPatPlans.Add(patPlan);
				if(!listInsSubs.Any(x => x.InsSubNum==patPlan.InsSubNum)) {
					InsSub insSub=new InsSub();
					insSub.InsSubNum=patPlan.InsSubNum;
					insSub.PlanNum=PIn.Long(tablePlans.Rows[i]["PlanNum"].ToString());
					listInsSubs.Add(insSub);
				}
				long planNum=PIn.Long(tablePlans.Rows[i]["PlanNum"].ToString());
				if(!listInsPlans.Any(x => x.PlanNum==planNum)) {
					InsPlan insPlan=new InsPlan();
					insPlan.PlanNum=planNum;
					insPlan.CarrierNum=PIn.Long(tablePlans.Rows[i]["CarrierNum"].ToString());
					listInsPlans.Add(insPlan);
				}
			}
			if(listPatPlans.Count==0) {
				return new List<PreAuthNeed>();
			}
			List<ClaimProc> listClaimProcs=GetClaimProcsForNeeds(listProcedures.Select(x => x.ProcNum).ToList());
			return GetNeeds(listProcedures,listPatPlans,listInsSubs,listInsPlans,listClaimProcs);
		}

		///<summary>Carrier Combine (FormCarrierCombine): the carrier that is kept takes the union of its own list and the lists of the carriers combined
		///into it (INFERRED rule). Called from Carriers.Combine before the other carriers are deleted. Logs the change in the insurance edit log like
		///any other carrier field. Does nothing when no list changes.</summary>
		public static void CombineCodes(long carrierNumKept,List<long> listCarrierNumsCombined,long userNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),carrierNumKept,listCarrierNumsCombined,userNum);
				return;
			}
			if(listCarrierNumsCombined.IsNullOrEmpty()) {
				return;
			}
			string command="SELECT PreAuthCodes FROM carrier WHERE CarrierNum IN("+string.Join(",",listCarrierNumsCombined.Select(x => POut.Long(x)))+") "
				+"AND PreAuthCodes!='' ORDER BY CarrierNum";
			List<string> listCodesOther=Db.GetListString(command);
			if(listCodesOther.Count==0) {
				return;
			}
			Carrier carrierKept=Carriers.GetCarrierDB(carrierNumKept);
			if(carrierKept==null) {
				return;
			}
			string codesMerged=Merge(carrierKept.PreAuthCodes,listCodesOther);
			if(codesMerged==(carrierKept.PreAuthCodes??"")) {
				return;
			}
			Carrier carrierOld=carrierKept.Copy();
			carrierKept.PreAuthCodes=codesMerged;
			Crud.CarrierCrud.Update(carrierKept,carrierOld);
			InsEditLogs.MakeLogEntry(carrierKept,carrierOld,InsEditLogType.Carrier,userNum);
		}
		#endregion Database
	}

	///<summary>The result of CarrierPreAuths.Check.</summary>
	public class PreAuthCodesCheck {
		///<summary>Why the text can not be saved. Empty when it can.</summary>
		public string Error="";
		///<summary>The text to store (plain hyphens, no repeated entries). Empty when the list is empty or when Error is set.</summary>
		public string Codes="";
		///<summary>Single codes that are not in the procedure code list. Saving is allowed; the window warns.</summary>
		public List<string> ListUnknownCodes=new List<string>();

		public bool IsValid {
			get {
				return Error=="";
			}
		}
	}

	///<summary>One treatment-planned procedure that still needs a preauthorization from one insurance plan. Not a database table.</summary>
	[Serializable]
	public class PreAuthNeed {
		public long ProcNum;
		public long PatNum;
		public long AptNum;
		public long PlannedAptNum;
		public long CodeNum;
		public string ProcCode="";
		public string ToothNum="";
		public string Surf="";
		public long PatPlanNum;
		///<summary>The plan's priority for the patient: 1 primary, 2 secondary, and so on.</summary>
		public byte Ordinal;
		public long InsSubNum;
		public long PlanNum;
		public long CarrierNum;
		public string CarrierName="";
	}
}
