// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262MiscTests {
	///<summary>The rules of the automatic Prophy and Perio switch (Open Dental 26.2 item 41), on objects only: RecallProphyPerioSwitch.Decide.</summary>
	[TestClass]
	public class RecallSwitchDecideTests {
		private const long PROPHY=1;
		private const long PERIO=3;
		private const long CODE_PROPHY=25;//adult prophy
		private const long CODE_CHILD=30;//child prophy, also a Prophy trigger
		private const long CODE_PERIO=90;//perio maintenance
		private const long CODE_BOTH=91;//set up as a trigger of both types
		private const long CODE_OTHER=92;//no trigger
		private static readonly DateTime _date1=new DateTime(2026,3,2);
		private static readonly DateTime _date2=new DateTime(2026,6,8);

		private static List<long> ProphyCodes() {
			return new List<long> { CODE_PROPHY,CODE_CHILD,CODE_BOTH };
		}

		private static List<long> PerioCodes() {
			return new List<long> { CODE_PERIO,CODE_BOTH };
		}

		private static Recall RecallOf(long recallTypeNum,DateTime datePrevious,bool isDisabled=false) {
			return new Recall { RecallNum=7,PatNum=5,RecallTypeNum=recallTypeNum,DatePrevious=datePrevious,IsDisabled=isDisabled };
		}

		private static RecallSwitchProc Proc(long codeNum,DateTime date) {
			return new RecallSwitchProc { CodeNum=codeNum,ProcDate=date };
		}

		private static RecallSwitchDecision Decide(List<Recall> listRecalls,params RecallSwitchProc[] arrayProcs) {
			return RecallProphyPerioSwitch.Decide(PROPHY,PERIO,ProphyCodes(),PerioCodes(),listRecalls,new List<RecallSwitchProc>(arrayProcs));
		}

		[TestMethod]
		public void ProphyPatient_NewPerioProcedure_SwitchesToPerio() {
			Recall recall=RecallOf(PROPHY,_date1);
			RecallSwitchDecision decision=Decide(new List<Recall> { recall },Proc(CODE_PROPHY,_date1),Proc(CODE_PERIO,_date2));
			Assert.AreEqual(EnumRecallSwitchOutcome.Switched,decision.Outcome);
			Assert.AreEqual(PERIO,decision.RecallTypeNumTarget);
			Assert.AreSame(recall,decision.Recall);
			Assert.AreEqual(_date2,decision.DateProc);
			CollectionAssert.AreEqual(new[] { CODE_PERIO },decision.ListCodeNums);
		}

		[TestMethod]
		public void PerioPatient_NewProphyProcedure_SwitchesToProphy() {
			RecallSwitchDecision decision=Decide(new List<Recall> { RecallOf(PERIO,_date1) },Proc(CODE_PERIO,_date1),Proc(CODE_PROPHY,_date2));
			Assert.AreEqual(EnumRecallSwitchOutcome.Switched,decision.Outcome);
			Assert.AreEqual(PROPHY,decision.RecallTypeNumTarget);
		}

		[TestMethod]
		public void PerioPatient_NewChildProphy_SwitchesToProphy() {
			//The child prophy codes are Prophy triggers (the Child Prophy type has none of its own).
			RecallSwitchDecision decision=Decide(new List<Recall> { RecallOf(PERIO,_date1) },Proc(CODE_CHILD,_date2));
			Assert.AreEqual(EnumRecallSwitchOutcome.Switched,decision.Outcome);
			Assert.AreEqual(PROPHY,decision.RecallTypeNumTarget);
		}

		[TestMethod]
		public void SameKindAsRecall_NoChange() {
			Assert.AreEqual(EnumRecallSwitchOutcome.AlreadyThatType,Decide(new List<Recall> { RecallOf(PERIO,_date1) },Proc(CODE_PERIO,_date2)).Outcome);
			Assert.AreEqual(EnumRecallSwitchOutcome.AlreadyThatType,Decide(new List<Recall> { RecallOf(PROPHY,_date1) },Proc(CODE_PROPHY,_date2)).Outcome);
		}

		[TestMethod]
		public void ProcedureNotNewerThanPreviousDate_NoChange() {
			//The sync after the completion already set the previous date: a later sync, or a manual Set Prophy afterwards, changes nothing.
			Assert.AreEqual(EnumRecallSwitchOutcome.NoNewProcedure,Decide(new List<Recall> { RecallOf(PROPHY,_date2) },Proc(CODE_PERIO,_date2)).Outcome);
			//A back-dated perio procedure older than the previous date.
			Assert.AreEqual(EnumRecallSwitchOutcome.NoNewProcedure,
				Decide(new List<Recall> { RecallOf(PROPHY,_date2) },Proc(CODE_PROPHY,_date2),Proc(CODE_PERIO,_date1)).Outcome);
		}

		[TestMethod]
		public void OnlyTheNewestDateDecides() {
			//An older perio procedure does not count once a newer prophy one exists.
			RecallSwitchDecision decision=Decide(new List<Recall> { RecallOf(PERIO,DateTime.MinValue) },Proc(CODE_PERIO,_date1),Proc(CODE_PROPHY,_date2));
			Assert.AreEqual(EnumRecallSwitchOutcome.Switched,decision.Outcome);
			Assert.AreEqual(PROPHY,decision.RecallTypeNumTarget);
		}

		[TestMethod]
		public void BothKindsOnNewestDate_NoChange() {
			RecallSwitchDecision decision=Decide(new List<Recall> { RecallOf(PROPHY,_date1) },Proc(CODE_PROPHY,_date2),Proc(CODE_PERIO,_date2));
			Assert.AreEqual(EnumRecallSwitchOutcome.Ambiguous,decision.Outcome);
			Assert.AreEqual(0,decision.RecallTypeNumTarget);
		}

		[TestMethod]
		public void CodeThatTriggersBothTypes_CountsAsNeither() {
			Assert.AreEqual(EnumRecallSwitchOutcome.Ambiguous,Decide(new List<Recall> { RecallOf(PROPHY,_date1) },Proc(CODE_BOTH,_date2)).Outcome);
			//With a perio code on the same date, the perio code decides.
			RecallSwitchDecision decision=Decide(new List<Recall> { RecallOf(PROPHY,_date1) },Proc(CODE_BOTH,_date2),Proc(CODE_PERIO,_date2));
			Assert.AreEqual(EnumRecallSwitchOutcome.Switched,decision.Outcome);
			Assert.AreEqual(PERIO,decision.RecallTypeNumTarget);
		}

		[TestMethod]
		public void CodesThatTriggerNothing_AreIgnored() {
			Assert.AreEqual(EnumRecallSwitchOutcome.NoNewProcedure,Decide(new List<Recall> { RecallOf(PROPHY,_date1) },Proc(CODE_OTHER,_date2)).Outcome);
			//A newer procedure without a trigger does not hide an older trigger procedure.
			RecallSwitchDecision decision=Decide(new List<Recall> { RecallOf(PROPHY,DateTime.MinValue) },Proc(CODE_PERIO,_date1),Proc(CODE_OTHER,_date2));
			Assert.AreEqual(EnumRecallSwitchOutcome.Switched,decision.Outcome);
			Assert.AreEqual(_date1,decision.DateProc);
		}

		[TestMethod]
		public void DisabledRecall_NeverSwitched() {
			Assert.AreEqual(EnumRecallSwitchOutcome.Disabled,
				Decide(new List<Recall> { RecallOf(PROPHY,_date1,isDisabled:true) },Proc(CODE_PERIO,_date2)).Outcome);
		}

		[TestMethod]
		public void BothSpecialRecalls_NoChange() {
			Assert.AreEqual(EnumRecallSwitchOutcome.TwoSpecialRecalls,
				Decide(new List<Recall> { RecallOf(PROPHY,_date1),RecallOf(PERIO,_date1) },Proc(CODE_PERIO,_date2)).Outcome);
		}

		[TestMethod]
		public void OtherRecallTypes_DoNotCount() {
			//A patient with only a bitewing recall has no Prophy or Perio recall: perio procedures make Synch create a Perio recall.
			Recall recallBw=RecallOf(4,_date2);
			RecallSwitchDecision decision=Decide(new List<Recall> { recallBw },Proc(CODE_PERIO,_date1));
			Assert.AreEqual(EnumRecallSwitchOutcome.PerioCreated,decision.Outcome);
			Assert.IsNull(decision.Recall);
		}

		[TestMethod]
		public void NoRecallYet_PerioCreatedOnlyForPerio() {
			Assert.AreEqual(EnumRecallSwitchOutcome.PerioCreated,Decide(new List<Recall>(),Proc(CODE_PERIO,_date1)).Outcome);
			Assert.AreEqual(EnumRecallSwitchOutcome.AlreadyThatType,Decide(new List<Recall>(),Proc(CODE_PROPHY,_date1)).Outcome,"Synch makes a Prophy recall anyway");
			Assert.AreEqual(EnumRecallSwitchOutcome.NoNewProcedure,Decide(new List<Recall>()).Outcome);
		}

		[TestMethod]
		public void NotSetUp_NoChange() {
			List<Recall> listRecalls=new List<Recall> { RecallOf(PROPHY,_date1) };
			List<RecallSwitchProc> listProcs=new List<RecallSwitchProc> { Proc(CODE_PERIO,_date2) };
			Assert.AreEqual(EnumRecallSwitchOutcome.NotSetUp,RecallProphyPerioSwitch.Decide(PROPHY,0,ProphyCodes(),PerioCodes(),listRecalls,listProcs).Outcome);
			Assert.AreEqual(EnumRecallSwitchOutcome.NotSetUp,RecallProphyPerioSwitch.Decide(0,PERIO,ProphyCodes(),PerioCodes(),listRecalls,listProcs).Outcome);
			Assert.AreEqual(EnumRecallSwitchOutcome.NotSetUp,RecallProphyPerioSwitch.Decide(PROPHY,PROPHY,ProphyCodes(),PerioCodes(),listRecalls,listProcs).Outcome);
			Assert.AreEqual(EnumRecallSwitchOutcome.NotSetUp,
				RecallProphyPerioSwitch.Decide(PROPHY,PERIO,ProphyCodes(),new List<long>(),listRecalls,listProcs).Outcome,"Perio has no trigger");
			Assert.AreEqual(EnumRecallSwitchOutcome.NotSetUp,RecallProphyPerioSwitch.Decide(PROPHY,PERIO,null,null,listRecalls,listProcs).Outcome);
		}

		[TestMethod]
		public void BlankProcDates_AreIgnored() {
			Assert.AreEqual(EnumRecallSwitchOutcome.NoNewProcedure,
				Decide(new List<Recall> { RecallOf(PROPHY,DateTime.MinValue) },Proc(CODE_PERIO,DateTime.MinValue)).Outcome);
		}

		[TestMethod]
		public void NullLists_AreSafe() {
			RecallSwitchDecision decision=RecallProphyPerioSwitch.Decide(PROPHY,PERIO,ProphyCodes(),PerioCodes(),null,null);
			Assert.AreEqual(EnumRecallSwitchOutcome.NoNewProcedure,decision.Outcome);
		}

		[TestMethod]
		public void ChangeLogLine() {
			Assert.AreEqual("",RecallProphyPerioSwitch.GetChangeLogLine(true,true));
			StringAssert.Contains(RecallProphyPerioSwitch.GetChangeLogLine(false,true),"from 'False' to 'True'");
		}
	}
}
