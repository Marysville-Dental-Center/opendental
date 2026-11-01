// M2-MISC (Open Dental 26.1): the automation condition "procedure clinic differs from patient clinic" (AutoCondField 14,
// PatientProcedureClinicMismatch). It works only with the ProcedureComplete trigger, has no comparison and no compare text.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Evaluates AutoCondField.PatientProcedureClinicMismatch and the save rule that ties it to the ProcedureComplete trigger.</summary>
	public static class AutomationClinicMismatches {
		///<summary>The only trigger the condition works with.</summary>
		public const EnumAutomationTrigger TRIGGER=EnumAutomationTrigger.ProcedureComplete;

		///<summary>True if the trigger can carry the condition (ProcedureComplete only).</summary>
		public static bool IsTriggerAllowed(EnumAutomationTrigger automationTrigger) {
			return automationTrigger==TRIGGER;
		}

		///<summary>True if any of the procedures' clinics differs from the patient's clinic. A patient with no clinic (0) is a mismatch against any
		///procedure with a clinic. Always false with clinics turned off, or with no procedures.</summary>
		public static bool IsMismatch(IEnumerable<long> listProcClinicNums,long patClinicNum,bool hasClinicsEnabled) {
			if(!hasClinicsEnabled || listProcClinicNums==null) {
				return false;
			}
			return listProcClinicNums.Any(x => x!=patClinicNum);
		}

		///<summary>An error message for saving an automation with this trigger and these conditions, or "" when it can be saved. Saving is refused
		///while the automation has the clinic mismatch condition and its trigger is not ProcedureComplete.</summary>
		public static string GetSaveError(EnumAutomationTrigger automationTrigger,List<AutomationCondition> listAutomationConditions) {
			if(IsTriggerAllowed(automationTrigger) || listAutomationConditions==null) {
				return "";
			}
			if(!listAutomationConditions.Any(x => x.CompareField==AutoCondField.PatientProcedureClinicMismatch)) {
				return "";
			}
			return "The condition \""+AutoCondField.PatientProcedureClinicMismatch.GetDescription()+"\" only works with the "
				+TRIGGER+" trigger. Change the trigger or delete the condition.";
		}

		///<summary>The condition for one automation that fired. The completed procedures are the ones passed as triggerObj (a List of Procedure or
		///one Procedure) when the caller has them; otherwise the patient's procedures set complete today with one of the trigger's codes. Only
		///procedures whose code is one of the automation's codes count. False for any trigger other than ProcedureComplete.</summary>
		public static bool IsConditionMet<T>(Automation automation,EnumAutomationTrigger automationTrigger,List<string> listProcCodes,long patNum,
			T triggerObj)
		{
			Meth.NoCheckMiddleTierRole();
			if(!IsTriggerAllowed(automationTrigger) || !PrefC.HasClinicsEnabled) {
				return false;
			}
			Patient patient=Patients.GetPat(patNum);
			if(patient==null) {
				return false;
			}
			List<Procedure> listProcedures=GetProceduresForTrigger(triggerObj,patNum,listProcCodes);
			List<string> listAutomationCodes=GetAutomationCodes(automation);
			if(listAutomationCodes.Count>0) {
				listProcedures=listProcedures.FindAll(x => listAutomationCodes.Contains(ProcedureCodes.GetStringProcCode(x.CodeNum)));
			}
			return IsMismatch(listProcedures.Select(x => x.ClinicNum),patient.ClinicNum,hasClinicsEnabled:true);
		}

		///<summary>The procedure codes an automation fires on (Automation.ProcCodes, comma separated), trimmed. Empty when none.</summary>
		public static List<string> GetAutomationCodes(Automation automation) {
			if(automation==null || string.IsNullOrWhiteSpace(automation.ProcCodes)) {
				return new List<string>();
			}
			return automation.ProcCodes.Split(new[] { ',' },StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x!="").ToList();
		}

		///<summary>The completed procedures that fired the trigger. triggerObj may be a List of Procedure or one Procedure; then only its complete
		///ones are used. Otherwise, the patient's procedures set complete today (DateComplete) with one of the codes.</summary>
		public static List<Procedure> GetProceduresForTrigger(object triggerObj,long patNum,List<string> listProcCodes) {
			Meth.NoCheckMiddleTierRole();
			if(triggerObj is List<Procedure> listProceduresPassed) {
				return listProceduresPassed.FindAll(x => x!=null && x.ProcStatus==ProcStat.C);
			}
			if(triggerObj is Procedure procedurePassed) {
				return new List<Procedure>() { procedurePassed }.FindAll(x => x.ProcStatus==ProcStat.C);
			}
			if(listProcCodes==null || listProcCodes.Count==0) {
				return new List<Procedure>();
			}
			List<long> listCodeNums=listProcCodes.Select(x => ProcedureCodes.GetCodeNum(x)).Where(x => x>0).Distinct().ToList();
			return GetCompletedToday(patNum,listCodeNums,DateTime.Today);
		}

		///<summary>The patient's complete procedures with one of the codes whose DateComplete is the given date. Procedures.Insert and Update set
		///DateComplete to today when a procedure is set complete.</summary>
		public static List<Procedure> GetCompletedToday(long patNum,List<long> listCodeNums,DateTime dateComplete) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Procedure>>(MethodBase.GetCurrentMethod(),patNum,listCodeNums,dateComplete);
			}
			if(listCodeNums==null || listCodeNums.Count==0) {
				return new List<Procedure>();
			}
			string command="SELECT * FROM procedurelog "
				+"WHERE PatNum="+POut.Long(patNum)+" "
				+"AND ProcStatus="+POut.Int((int)ProcStat.C)+" "
				+"AND DateComplete="+POut.Date(dateComplete)+" "
				+"AND CodeNum IN("+string.Join(",",listCodeNums.Select(x => POut.Long(x)))+")";
			return Crud.ProcedureCrud.SelectMany(command);
		}
	}
}
