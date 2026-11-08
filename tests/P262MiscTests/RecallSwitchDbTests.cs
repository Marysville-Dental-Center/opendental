// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace P262MiscTests {
	///<summary>The automatic Prophy and Perio switch (Open Dental 26.2 item 41) through Recalls.Synch on the scratch database: the setting row, both
	///directions, and the edge cases (setting off, manual choice, back-dated and planned procedures, EO, new patient, disabled recall, inactive patient,
	///both kinds on one date, types not set up).</summary>
	[TestClass]
	public class RecallSwitchDbTests {
		private const string CODE_PERIO="D4910";
		private const string CODE_PROPHY="D1110";
		private const string CODE_CHILD_PROPHY="D1120";
		private readonly string _codeProphy=CODE_PROPHY;
		private long _recallTypeProphy;
		private long _recallTypePerio;
		private static readonly DateTime _date1=DateTime.Today.AddDays(-200);
		private static readonly DateTime _date2=DateTime.Today.AddDays(-10);

		[TestInitialize]
		public void Init() {
			MiscDb.Begin();
			RecallTypes.RefreshCache();
			_recallTypeProphy=RecallTypes.ProphyType;
			_recallTypePerio=RecallTypes.PerioType;
			Assert.AreNotEqual(0,_recallTypeProphy,"The new database has a Prophy special type.");
			Assert.AreNotEqual(0,_recallTypePerio,"The new database has a Perio special type.");
			//The office's triggers: prophy codes on the Prophy type, the perio maintenance code on the Perio type (the new database has no procedure codes,
			//so its Prophy triggers point to codes that are not there yet, and its Perio type has none).
			RecallTriggers.SetForType(_recallTypeProphy,new List<RecallTrigger> {
				new RecallTrigger { CodeNum=ProcedureCodeT.CreateProcCode(CODE_PROPHY).CodeNum },
				new RecallTrigger { CodeNum=ProcedureCodeT.CreateProcCode(CODE_CHILD_PROPHY).CodeNum } });
			RecallTriggers.SetForType(_recallTypePerio,new List<RecallTrigger> { new RecallTrigger { CodeNum=ProcedureCodeT.CreateProcCode(CODE_PERIO).CodeNum } });
			RecallTriggers.RefreshCache();
			RecallProphyPerioSwitch.Save(true);
		}

		private static Patient NewPatient(PatientStatus patientStatus=PatientStatus.Patient) {
			return PatientT.CreatePatient(suffix:Guid.NewGuid().ToString("N").Substring(0,8),lName:"Recall",fName:"Switch",patStatus:patientStatus);
		}

		///<summary>Adds a procedure with the status and date, then syncs the patient's recalls as the program does after a completion.</summary>
		private static void AddProc(Patient patient,string procCode,DateTime date,ProcStat procStat=ProcStat.C) {
			ProcedureT.CreateProcedure(patient,procCode,procStat,"",50,procDate:date);
			Recalls.Synch(patient.PatNum);
		}

		private List<Recall> SpecialRecalls(long patNum) {
			return Recalls.GetList(patNum).FindAll(x => x.RecallTypeNum==_recallTypeProphy || x.RecallTypeNum==_recallTypePerio);
		}

		private static List<string> RecallLogs(long patNum) {
			DataTable table=DataCore.GetTable("SELECT LogText FROM securitylog WHERE PatNum="+POut.Long(patNum)+" AND PermType="
				+POut.Int((int)EnumPermType.RecallEdit)+" ORDER BY SecurityLogNum");
			return table.Select().Select(x => x[0].ToString()).ToList();
		}

		[TestMethod]
		public void NewDatabase_HasNoSettingRow_SoTheSettingIsOff() {
			Assert.AreEqual("0",MiscDb.CountRecallSwitchRowsAtStart,"No migration step writes the row; the office turns the setting on.");
		}

		[TestMethod]
		public void Setting_MissingRowReadsOff_SaveInsertsThenUpdatesOneRow() {
			DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+RecallProphyPerioSwitch.PREF_NAME+"'");
			Prefs.RefreshCache();
			Assert.IsFalse(RecallProphyPerioSwitch.IsOn(),"No row: off.");
			Assert.IsFalse(RecallProphyPerioSwitch.Save(false),"Saving off when off changes nothing.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+RecallProphyPerioSwitch.PREF_NAME+"'"));
			Assert.IsTrue(RecallProphyPerioSwitch.Save(true));
			Assert.IsTrue(RecallProphyPerioSwitch.IsOn());
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+RecallProphyPerioSwitch.PREF_NAME+"'"));
			Assert.IsTrue(RecallProphyPerioSwitch.Save(false));
			Prefs.RefreshCache();
			Assert.IsFalse(RecallProphyPerioSwitch.IsOn());
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+RecallProphyPerioSwitch.PREF_NAME+"'"));
			Assert.IsFalse(Enum.GetNames(typeof(PrefName)).Contains(RecallProphyPerioSwitch.PREF_NAME),"Open Dental's name is not public: not in PrefName.");
		}

		[TestMethod]
		public void Off_PerioProcedureKeepsProphyRecall() {
			RecallProphyPerioSwitch.Save(false);
			Patient patient=NewPatient();
			AddProc(patient,_codeProphy,_date1);
			AddProc(patient,CODE_PERIO,_date2);
			List<Recall> listRecalls=SpecialRecalls(patient.PatNum);
			Assert.AreEqual(1,listRecalls.Count);
			Assert.AreEqual(_recallTypeProphy,listRecalls[0].RecallTypeNum,"Off: the 24.3 behavior, the perio code only moves the prophy dates.");
			Assert.AreEqual(_date2,listRecalls[0].DatePrevious);
		}

		[TestMethod]
		public void On_ProphyToPerio_ThenPerioToProphy() {
			Patient patient=NewPatient();
			AddProc(patient,_codeProphy,_date1);
			Recall recall=SpecialRecalls(patient.PatNum).Single();
			Assert.AreEqual(_recallTypeProphy,recall.RecallTypeNum);
			AddProc(patient,CODE_PERIO,_date2);
			Recall recallPerio=SpecialRecalls(patient.PatNum).Single();
			Assert.AreEqual(recall.RecallNum,recallPerio.RecallNum,"The same recall row is switched.");
			Assert.AreEqual(_recallTypePerio,recallPerio.RecallTypeNum);
			Interval intervalPerio=RecallTypes.GetInterval(_recallTypePerio);
			Assert.AreEqual(intervalPerio,recallPerio.RecallInterval,"The Perio type's default interval.");
			Assert.AreEqual(_date2,recallPerio.DatePrevious);
			Assert.AreEqual(_date2+intervalPerio,recallPerio.DateDueCalc);
			Assert.AreEqual(_date2+intervalPerio,recallPerio.DateDue);
			Assert.IsTrue(RecallLogs(patient.PatNum).Exists(x => x.StartsWith("Recall changed from Prophy to Perio by Recall Sync") && x.Contains(CODE_PERIO)));
			DateTime date3=DateTime.Today.AddDays(-1);
			AddProc(patient,_codeProphy,date3);
			Recall recallProphy=SpecialRecalls(patient.PatNum).Single();
			Assert.AreEqual(_recallTypeProphy,recallProphy.RecallTypeNum);
			Assert.AreEqual(RecallTypes.GetInterval(_recallTypeProphy),recallProphy.RecallInterval);
			Assert.AreEqual(date3,recallProphy.DatePrevious);
			Assert.IsTrue(RecallLogs(patient.PatNum).Exists(x => x.StartsWith("Recall changed from Perio to Prophy by Recall Sync")));
		}

		[TestMethod]
		public void ManualChoiceAfterSwitch_StaysUntilNextTriggerProcedure() {
			Patient patient=NewPatient();
			AddProc(patient,_codeProphy,_date1);
			AddProc(patient,CODE_PERIO,_date2);
			Recall recall=SpecialRecalls(patient.PatNum).Single();
			Assert.AreEqual(_recallTypePerio,recall.RecallTypeNum);
			//What Set Prophy in the Recalls for Patient window does.
			recall.RecallTypeNum=_recallTypeProphy;
			recall.RecallInterval=RecallTypes.GetInterval(_recallTypeProphy);
			Recalls.Update(recall);
			Recalls.Synch(patient.PatNum);
			Recalls.Synch(patient.PatNum);
			Assert.AreEqual(_recallTypeProphy,SpecialRecalls(patient.PatNum).Single().RecallTypeNum,"Nothing new was completed: the choice stays.");
		}

		[TestMethod]
		public void BackdatedPerioProcedure_NoSwitch() {
			Patient patient=NewPatient();
			AddProc(patient,_codeProphy,_date2);
			AddProc(patient,CODE_PERIO,_date1);//entered later, dated before the last prophy
			Recall recall=SpecialRecalls(patient.PatNum).Single();
			Assert.AreEqual(_recallTypeProphy,recall.RecallTypeNum);
			Assert.AreEqual(_date2,recall.DatePrevious);
		}

		[TestMethod]
		public void PlannedPerioProcedure_NoSwitch() {
			Patient patient=NewPatient();
			AddProc(patient,_codeProphy,_date1);
			AddProc(patient,CODE_PERIO,_date2,ProcStat.TP);
			Assert.AreEqual(_recallTypeProphy,SpecialRecalls(patient.PatNum).Single().RecallTypeNum,"Only completed procedures count.");
		}

		[TestMethod]
		public void ExistingOtherProviderPerio_Switches() {
			//EO and EC set a recall's previous date, so they count as completed.
			Patient patient=NewPatient();
			AddProc(patient,_codeProphy,_date1);
			AddProc(patient,CODE_PERIO,_date2,ProcStat.EO);
			Assert.AreEqual(_recallTypePerio,SpecialRecalls(patient.PatNum).Single().RecallTypeNum);
		}

		[TestMethod]
		public void NewPatient_FirstTriggerIsPerio_GetsPerioRecall() {
			Patient patient=NewPatient();
			AddProc(patient,CODE_PERIO,_date2);
			List<Recall> listRecalls=SpecialRecalls(patient.PatNum);
			Assert.AreEqual(1,listRecalls.Count,"No Prophy recall is made besides it.");
			Assert.AreEqual(_recallTypePerio,listRecalls[0].RecallTypeNum);
			Assert.AreEqual(_date2,listRecalls[0].DatePrevious);
			Assert.AreEqual(RecallTypes.GetInterval(_recallTypePerio),listRecalls[0].RecallInterval);
			Assert.IsTrue(RecallLogs(patient.PatNum).Exists(x => x.StartsWith("Perio recall added instead of Prophy by Recall Sync")));
		}

		[TestMethod]
		public void NewPatient_Off_FirstTriggerIsPerio_GetsProphyRecall() {
			RecallProphyPerioSwitch.Save(false);
			Patient patient=NewPatient();
			AddProc(patient,CODE_PERIO,_date2);
			Assert.AreEqual(_recallTypeProphy,SpecialRecalls(patient.PatNum).Single().RecallTypeNum);
		}

		[TestMethod]
		public void DisabledRecall_NoSwitch() {
			Patient patient=NewPatient();
			AddProc(patient,_codeProphy,_date1);
			Recall recall=SpecialRecalls(patient.PatNum).Single();
			recall.IsDisabled=true;
			Recalls.Update(recall);
			AddProc(patient,CODE_PERIO,_date2);
			Recall recallAfter=SpecialRecalls(patient.PatNum).Single();
			Assert.AreEqual(_recallTypeProphy,recallAfter.RecallTypeNum);
			Assert.IsTrue(recallAfter.IsDisabled);
		}

		[TestMethod]
		public void InactivePatient_NoSwitch() {
			Patient patient=NewPatient();
			AddProc(patient,_codeProphy,_date1);
			Patient patientOld=patient.Copy();
			patient.PatStatus=PatientStatus.Inactive;
			Patients.Update(patient,patientOld);
			AddProc(patient,CODE_PERIO,_date2);
			Assert.AreEqual(_recallTypeProphy,SpecialRecalls(patient.PatNum).Single().RecallTypeNum,"Recall Sync skips patients who are not active.");
		}

		[TestMethod]
		public void BothKindsSameDate_NoSwitch() {
			Patient patient=NewPatient();
			AddProc(patient,_codeProphy,_date1);
			ProcedureT.CreateProcedure(patient,_codeProphy,ProcStat.C,"",50,procDate:_date2);
			AddProc(patient,CODE_PERIO,_date2);
			Recall recall=SpecialRecalls(patient.PatNum).Single();
			Assert.AreEqual(_recallTypeProphy,recall.RecallTypeNum);
			Assert.AreEqual(_date2,recall.DatePrevious,"The dates still move as usual.");
		}

		[TestMethod]
		public void PerioTypeWithoutTriggers_NoSwitchNoError() {
			List<RecallTrigger> listTriggersPerio=RecallTriggers.GetForType(_recallTypePerio);
			try {
				RecallTriggers.SetForType(_recallTypePerio,new List<RecallTrigger>());
				RecallTriggers.RefreshCache();
				Patient patient=NewPatient();
				AddProc(patient,_codeProphy,_date1);
				AddProc(patient,CODE_PERIO,_date2);
				Assert.AreEqual(_recallTypeProphy,SpecialRecalls(patient.PatNum).Single().RecallTypeNum);
			}
			finally {
				RecallTriggers.SetForType(_recallTypePerio,listTriggersPerio.Select(x => new RecallTrigger { RecallTypeNum=_recallTypePerio,CodeNum=x.CodeNum }).ToList());
				RecallTriggers.RefreshCache();
			}
		}

		[TestMethod]
		public void SwitchIfNeeded_Off_ReadsNothing() {
			RecallProphyPerioSwitch.Save(false);
			RecallSwitchDecision decision=RecallProphyPerioSwitch.SwitchIfNeeded(0,null);
			Assert.AreEqual(EnumRecallSwitchOutcome.Off,decision.Outcome);
		}
	}
}
