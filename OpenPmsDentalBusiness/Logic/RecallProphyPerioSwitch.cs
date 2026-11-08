// Open Dental 26.2 (release notes, Miscellaneous): "New preference to automatically switch patients' prophy or perio recall based on completed
// procedures". Package P262-MISC; the rules are this program's own and are listed in docs/port-notes/P262-MISC.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The setting "Automatically switch between the Prophy and Perio recall based on completed procedures" (Setup, Recall Types). Off by
	///default. When it is on, Recalls.Synch switches a patient's special recall the way the Set Perio and Set Prophy buttons of the Recalls for
	///Patient window do, when a newly completed procedure says so:
	///<para>1. Only the special Prophy and Perio recall types take part (Setup, Recall Types). Both must be set, be different types, and have at least
	///one trigger; otherwise nothing is switched. The Prophy type's triggers are prophy procedures (they include the child prophy codes, since the
	///Child Prophy type has no triggers of its own), the Perio type's triggers are perio procedures. A code that triggers both types counts as
	///neither.</para>
	///<para>2. Completed procedures are those with status C, EC or EO, the statuses that set a recall's previous date.</para>
	///<para>3. Only a procedure newer than the patient's current Prophy or Perio recall's previous date can switch it, so the switch happens once, on
	///the sync that follows the completion. A procedure dated on or before that date (a back-dated entry, history entered later), a deleted procedure,
	///or a sync with nothing new changes nothing, and a type chosen by hand with Set Perio or Set Prophy stays until the next trigger procedure is
	///completed.</para>
	///<para>4. The procedures of the newest date decide: perio procedures only, the recall becomes Perio; prophy procedures only, it becomes Prophy.
	///Both kinds on that date (or only codes that trigger both types) changes nothing.</para>
	///<para>5. A patient with no Prophy or Perio recall yet gets a Perio recall instead of a Prophy recall when the deciding procedures are perio
	///procedures. A disabled recall is never switched, and neither is a patient who has both a Prophy and a Perio recall (the Recalls for Patient
	///window warns about that).</para>
	///<para>6. Switching changes the recall type and sets the interval to the new type's default interval, as Set Perio and Set Prophy do; the sync then
	///sets the previous and due dates as usual. Each switch is written to the audit trail (RecallEdit).</para>
	///<para>Not done by Synch All (Setup, Recall Types, Sync): it brings every patient's dates up to date without switching anyone, so turning the
	///setting on never switches patients for procedures completed before.</para>
	///Open Dental's preference name is not public, so the value lives in a preference row that is not in PrefName (the PayPlanGlobalLock pattern):
	///the row is inserted the first time the office changes the setting, and a database without it reads as off. No migration step is needed.</summary>
	public class RecallProphyPerioSwitch {
		///<summary>This program's name for the preference row (INFERRED: Open Dental's name is not public).</summary>
		public const string PREF_NAME="RecallSwitchProphyPerioAuto";

		#region Setting
		///<summary>True when the office turned the setting on. A missing row, or a cache that is not loaded yet, reads as off. Never throws.</summary>
		public static bool IsOn() {
			Meth.NoCheckMiddleTierRole();
			try {
				if(Prefs.DictIsNull() || !Prefs.GetContainsKey(PREF_NAME)) {
					return false;
				}
				return PIn.Bool(Prefs.GetOne(PREF_NAME).ValueString);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>Stores the setting, inserting the row if needed, and updates this workstation's cache. Returns true if the value changed. The caller
		///signals InvalidType.Prefs so other workstations reload, and writes the audit entry (GetChangeLogLine).</summary>
		public static bool Save(bool isOn) {
			Meth.NoCheckMiddleTierRole();
			if(IsOn()==isOn) {
				return false;
			}
			string value=POut.Bool(isOn);
			Upsert(value);
			Prefs.UpdateValueForKey(new Pref { PrefName=PREF_NAME,ValueString=value });
			return true;
		}

		///<summary>Writes the preference row straight to the table, inserting it when it is missing.</summary>
		public static void Upsert(string value) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),value);
				return;
			}
			string count=Db.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(PREF_NAME)+"'");
			if(PIn.Long(count)==0) {
				Db.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('"+POut.String(PREF_NAME)+"','"+POut.String(value)+"')");
				return;
			}
			Db.NonQ("UPDATE preference SET ValueString='"+POut.String(value)+"' WHERE PrefName='"+POut.String(PREF_NAME)+"'");
		}

		///<summary>The audit line for the Recall Types window, or "" when unchanged.</summary>
		public static string GetChangeLogLine(bool isOnOld,bool isOnNew) {
			if(isOnOld==isOnNew) {
				return "";
			}
			return "Automatically switch between Prophy and Perio recall changed from '"+isOnOld+"' to '"+isOnNew+"'";
		}
		#endregion Setting

		#region Decision
		///<summary>What the rules say for one patient. Pure: reads nothing and changes nothing. listCodeNumsProphy and listCodeNumsPerio are the trigger
		///codes of the special Prophy and Perio types; listRecallsPat is every recall of the patient; listProcsCompleted holds the patient's procedures
		///with status C, EC or EO (the caller filters the status; procedures with other codes are ignored here).</summary>
		public static RecallSwitchDecision Decide(long recallTypeNumProphy,long recallTypeNumPerio,List<long> listCodeNumsProphy,List<long> listCodeNumsPerio,
			List<Recall> listRecallsPat,List<RecallSwitchProc> listProcsCompleted)
		{
			RecallSwitchDecision decision=new RecallSwitchDecision();
			decision.RecallTypeNumProphy=recallTypeNumProphy;
			decision.RecallTypeNumPerio=recallTypeNumPerio;
			listCodeNumsProphy=listCodeNumsProphy??new List<long>();
			listCodeNumsPerio=listCodeNumsPerio??new List<long>();
			if(recallTypeNumProphy==0 || recallTypeNumPerio==0 || recallTypeNumProphy==recallTypeNumPerio
				|| listCodeNumsProphy.Count==0 || listCodeNumsPerio.Count==0)
			{
				decision.Outcome=EnumRecallSwitchOutcome.NotSetUp;
				return decision;
			}
			List<Recall> listRecallsSpecial=(listRecallsPat??new List<Recall>())
				.FindAll(x => x.RecallTypeNum==recallTypeNumProphy || x.RecallTypeNum==recallTypeNumPerio);
			if(listRecallsSpecial.Count>1) {
				decision.Outcome=EnumRecallSwitchOutcome.TwoSpecialRecalls;
				return decision;
			}
			Recall recall=listRecallsSpecial.FirstOrDefault();
			decision.Recall=recall;
			if(recall!=null && recall.IsDisabled) {
				decision.Outcome=EnumRecallSwitchOutcome.Disabled;
				return decision;
			}
			DateTime dateStored=DateTime.MinValue;
			if(recall!=null && recall.DatePrevious.Year>1880) {
				dateStored=recall.DatePrevious.Date;
			}
			List<RecallSwitchProc> listProcsTrigger=(listProcsCompleted??new List<RecallSwitchProc>())
				.FindAll(x => x.ProcDate.Year>1880 && (listCodeNumsProphy.Contains(x.CodeNum) || listCodeNumsPerio.Contains(x.CodeNum)));
			if(listProcsTrigger.Count==0) {
				decision.Outcome=EnumRecallSwitchOutcome.NoNewProcedure;
				return decision;
			}
			DateTime dateNewest=listProcsTrigger.Max(x => x.ProcDate.Date);
			if(dateNewest<=dateStored) {
				decision.Outcome=EnumRecallSwitchOutcome.NoNewProcedure;
				return decision;
			}
			decision.DateProc=dateNewest;
			List<long> listCodeNumsOnDate=listProcsTrigger.FindAll(x => x.ProcDate.Date==dateNewest).Select(x => x.CodeNum).Distinct().ToList();
			List<long> listCodeNumsProphyOnDate=listCodeNumsOnDate.FindAll(x => listCodeNumsProphy.Contains(x) && !listCodeNumsPerio.Contains(x));
			List<long> listCodeNumsPerioOnDate=listCodeNumsOnDate.FindAll(x => listCodeNumsPerio.Contains(x) && !listCodeNumsProphy.Contains(x));
			if((listCodeNumsProphyOnDate.Count>0)==(listCodeNumsPerioOnDate.Count>0)) {//both kinds, or only codes that trigger both types
				decision.ListCodeNums=listCodeNumsOnDate;
				decision.Outcome=EnumRecallSwitchOutcome.Ambiguous;
				return decision;
			}
			if(listCodeNumsPerioOnDate.Count>0) {
				decision.RecallTypeNumTarget=recallTypeNumPerio;
				decision.ListCodeNums=listCodeNumsPerioOnDate;
			}
			else {
				decision.RecallTypeNumTarget=recallTypeNumProphy;
				decision.ListCodeNums=listCodeNumsProphyOnDate;
			}
			if(recall==null) {
				//Recalls.Synch makes a Prophy recall for a patient without one, so only Perio needs a decision.
				decision.Outcome=decision.RecallTypeNumTarget==recallTypeNumPerio ? EnumRecallSwitchOutcome.PerioCreated : EnumRecallSwitchOutcome.AlreadyThatType;
				return decision;
			}
			if(recall.RecallTypeNum==decision.RecallTypeNumTarget) {
				decision.Outcome=EnumRecallSwitchOutcome.AlreadyThatType;
				return decision;
			}
			decision.Outcome=EnumRecallSwitchOutcome.Switched;
			return decision;
		}
		#endregion Decision

		#region Apply
		///<summary>Called by Recalls.Synch (on the server side of the middle tier) with the patient's recalls, before it works out which special type the
		///patient has. When the setting is on and the rules say so, switches the patient's Prophy or Perio recall in place (the object in the list and its
		///row) and logs it. Returns the decision; Synch makes a Perio recall instead of a Prophy recall when the outcome is PerioCreated. Off returns
		///Outcome Off without reading anything.</summary>
		public static RecallSwitchDecision SwitchIfNeeded(long patNum,List<Recall> listRecallsPat) {
			Meth.NoCheckMiddleTierRole();
			if(!IsOn()) {
				return new RecallSwitchDecision { Outcome=EnumRecallSwitchOutcome.Off };
			}
			long recallTypeNumProphy=PrefC.GetLong(PrefName.RecallTypeSpecialProphy);
			long recallTypeNumPerio=PrefC.GetLong(PrefName.RecallTypeSpecialPerio);
			List<long> listCodeNumsProphy=GetTriggerCodeNums(recallTypeNumProphy);
			List<long> listCodeNumsPerio=GetTriggerCodeNums(recallTypeNumPerio);
			List<RecallSwitchProc> listProcs=new List<RecallSwitchProc>();
			if(listCodeNumsProphy.Count>0 && listCodeNumsPerio.Count>0) {
				listProcs=GetCompletedProcs(patNum,listCodeNumsProphy.Union(listCodeNumsPerio).ToList());
			}
			RecallSwitchDecision decision=Decide(recallTypeNumProphy,recallTypeNumPerio,listCodeNumsProphy,listCodeNumsPerio,listRecallsPat,listProcs);
			if(decision.Outcome!=EnumRecallSwitchOutcome.Switched) {
				return decision;
			}
			Recall recallOld=decision.Recall.Copy();
			decision.Recall.RecallTypeNum=decision.RecallTypeNumTarget;
			decision.Recall.RecallInterval=RecallTypes.GetInterval(decision.RecallTypeNumTarget);
			Recalls.Update(decision.Recall,recallOld);
			SecurityLogs.MakeLogEntry(EnumPermType.RecallEdit,patNum,GetLogText(decision));
			return decision;
		}

		///<summary>The trigger codes of a recall type, from the cache. Empty for 0.</summary>
		public static List<long> GetTriggerCodeNums(long recallTypeNum) {
			Meth.NoCheckMiddleTierRole();
			if(recallTypeNum==0) {
				return new List<long>();
			}
			return RecallTriggers.GetForType(recallTypeNum).Select(x => x.CodeNum).Distinct().ToList();
		}

		///<summary>The patient's procedures with one of these codes and status C, EC or EO.</summary>
		public static List<RecallSwitchProc> GetCompletedProcs(long patNum,List<long> listCodeNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<RecallSwitchProc>>(MethodBase.GetCurrentMethod(),patNum,listCodeNums);
			}
			List<RecallSwitchProc> listProcs=new List<RecallSwitchProc>();
			if(listCodeNums.IsNullOrEmpty()) {
				return listProcs;
			}
			string command="SELECT CodeNum,ProcDate FROM procedurelog "
				+"WHERE PatNum="+POut.Long(patNum)+" "
				+"AND CodeNum IN ("+string.Join(",",listCodeNums.Select(x => POut.Long(x)))+") "
				+"AND ProcStatus IN ("+POut.Int((int)ProcStat.C)+","+POut.Int((int)ProcStat.EC)+","+POut.Int((int)ProcStat.EO)+")";
			DataTable table=Db.GetTable(command);
			for(int i=0;i<table.Rows.Count;i++) {
				RecallSwitchProc recallSwitchProc=new RecallSwitchProc();
				recallSwitchProc.CodeNum=PIn.Long(table.Rows[i]["CodeNum"].ToString());
				recallSwitchProc.ProcDate=PIn.Date(table.Rows[i]["ProcDate"].ToString());
				listProcs.Add(recallSwitchProc);
			}
			return listProcs;
		}

		///<summary>The audit text of a switch, or of a Perio recall made instead of a Prophy recall.</summary>
		public static string GetLogText(RecallSwitchDecision decision) {
			string codes=string.Join(", ",decision.ListCodeNums.Select(x => ProcedureCodes.GetStringProcCode(x)));
			string strWhen=" ("+codes+" completed on "+decision.DateProc.ToShortDateString()+").";
			if(decision.Outcome==EnumRecallSwitchOutcome.PerioCreated) {
				return "Perio recall added instead of Prophy by Recall Sync, automatic Prophy and Perio switch"+strWhen;
			}
			string strFrom=decision.RecallTypeNumTarget==decision.RecallTypeNumPerio ? "Prophy" : "Perio";
			string strTo=decision.RecallTypeNumTarget==decision.RecallTypeNumPerio ? "Perio" : "Prophy";
			return "Recall changed from "+strFrom+" to "+strTo+" by Recall Sync, automatic Prophy and Perio switch"+strWhen;
		}
		#endregion Apply
	}

	///<summary>One completed procedure, as the switch rules need it.</summary>
	[Serializable]
	public class RecallSwitchProc {
		public long CodeNum;
		public DateTime ProcDate;
	}

	///<summary>What RecallProphyPerioSwitch decided for one patient.</summary>
	public class RecallSwitchDecision {
		public EnumRecallSwitchOutcome Outcome;
		///<summary>The patient's Prophy or Perio recall. Null when the patient has none (or two).</summary>
		public Recall Recall;
		public long RecallTypeNumProphy;
		public long RecallTypeNumPerio;
		///<summary>The type the deciding procedures point to. 0 when they point to neither.</summary>
		public long RecallTypeNumTarget;
		///<summary>The date of the deciding procedures. MinValue when there are none.</summary>
		public DateTime DateProc=DateTime.MinValue;
		///<summary>The deciding procedure codes (of the target kind), or every trigger code of the date when Ambiguous.</summary>
		public List<long> ListCodeNums=new List<long>();
	}

	///<summary>Outcomes of the automatic Prophy and Perio switch. Only Switched and PerioCreated change anything.</summary>
	public enum EnumRecallSwitchOutcome {
		///<summary>The setting is off.</summary>
		Off,
		///<summary>The special Prophy and Perio types are not both set, are the same type, or one has no trigger.</summary>
		NotSetUp,
		///<summary>The patient has both a Prophy and a Perio recall.</summary>
		TwoSpecialRecalls,
		///<summary>The patient's Prophy or Perio recall is disabled.</summary>
		Disabled,
		///<summary>No completed trigger procedure is newer than the recall's previous date.</summary>
		NoNewProcedure,
		///<summary>The newest date has both prophy and perio procedures, or only codes that trigger both types.</summary>
		Ambiguous,
		///<summary>The recall already has the type the procedures point to (or the patient has no recall and they point to Prophy).</summary>
		AlreadyThatType,
		///<summary>The patient's recall was switched.</summary>
		Switched,
		///<summary>The patient has no Prophy or Perio recall yet and the procedures point to Perio, so Synch makes a Perio recall.</summary>
		PerioCreated,
	}
}
