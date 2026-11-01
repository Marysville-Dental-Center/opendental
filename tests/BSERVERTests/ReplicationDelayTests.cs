// v25_2-server-replication-delay: the rules of the read delay and the signal look-back, without a database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BSERVERTests {
	[TestClass]
	public class ReplicationDelayTests {
		[TestMethod]
		public void PrefNames_AreForkOwnedAndNotInPrefName() {
			foreach(string name in new[] { ReplicationDelay.PREF_READ_DELAY_MS,ReplicationDelay.PREF_SIGNAL_LOOKBACK_MS,RpProcNote.PREF_ALL_NO_NOTES }) {
				StringAssert.StartsWith(name,"Fork");
				Assert.IsFalse(Enum.IsDefined(typeof(PrefName),name),name+" must not be an Open Dental preference name.");
			}
		}

		[TestMethod]
		public void ParseMs_ClampsAndTreatsBadValuesAsZero() {
			Assert.AreEqual(0,ReplicationDelay.ParseMs(null));
			Assert.AreEqual(0,ReplicationDelay.ParseMs(""));
			Assert.AreEqual(0,ReplicationDelay.ParseMs("abc"));
			Assert.AreEqual(0,ReplicationDelay.ParseMs("-5"));
			Assert.AreEqual(1500,ReplicationDelay.ParseMs(" 1500 "));
			Assert.AreEqual(60000,ReplicationDelay.ParseMs("60000"));
			Assert.AreEqual(60000,ReplicationDelay.ParseMs("999999999999"));
		}

		[TestMethod]
		public void IsWithinDelay() {
			Assert.IsFalse(ReplicationDelay.IsWithinDelay(long.MinValue,1000,5000),"No write yet.");
			Assert.IsFalse(ReplicationDelay.IsWithinDelay(1000,1000,0),"Delay off.");
			Assert.IsTrue(ReplicationDelay.IsWithinDelay(1000,1000,5000));
			Assert.IsTrue(ReplicationDelay.IsWithinDelay(1000,5999,5000));
			Assert.IsFalse(ReplicationDelay.IsWithinDelay(1000,6000,5000),"The delay has passed.");
			Assert.IsFalse(ReplicationDelay.IsWithinDelay(1000,500,5000),"A clock that went backwards is not a recent write.");
		}

		[TestMethod]
		public void StreamKey_DependsOnlyOnTheFilters() {
			string keyHigh=SignalLookbackFilter.GetStreamKey(new List<InvalidType> { InvalidType.Print,InvalidType.ShutDownNow },null);
			Assert.AreEqual(keyHigh,SignalLookbackFilter.GetStreamKey(new List<InvalidType> { InvalidType.ShutDownNow,InvalidType.Print },null));
			Assert.AreNotEqual(keyHigh,SignalLookbackFilter.GetStreamKey(null,new List<InvalidType> { InvalidType.Print,InvalidType.ShutDownNow }));
			Assert.AreNotEqual(keyHigh,SignalLookbackFilter.GetStreamKey(null,null));
		}

		[TestMethod]
		public void QuerySince_NoLookback_IsUnchanged() {
			SignalLookbackFilter filter=new SignalLookbackFilter();
			DateTime dateT=new DateTime(2026,5,1,10,0,5);
			Assert.AreEqual(dateT,filter.GetQuerySince("s",dateT,0));
		}

		[TestMethod]
		public void QuerySince_LooksBackButNeverBeforeTheFirstCheck() {
			SignalLookbackFilter filter=new SignalLookbackFilter();
			DateTime dateTStart=new DateTime(2026,5,1,10,0,0);
			//First check with a look-back: nothing from before the workstation started looking back.
			Assert.AreEqual(dateTStart,filter.GetQuerySince("s",dateTStart,30000));
			//Ten seconds later: looks back to the floor, not 30 seconds.
			Assert.AreEqual(dateTStart,filter.GetQuerySince("s",dateTStart.AddSeconds(10),30000));
			//A minute later: the full 30 seconds.
			Assert.AreEqual(dateTStart.AddSeconds(30),filter.GetQuerySince("s",dateTStart.AddSeconds(60),30000));
			//Milliseconds are cut to whole seconds (signalod.SigDateTime has none), rounding the look-back up.
			Assert.AreEqual(dateTStart.AddSeconds(58),filter.GetQuerySince("s",dateTStart.AddSeconds(60),1500));
		}

		[TestMethod]
		public void Filter_DropsSignalsAlreadyReturned_PerStream() {
			SignalLookbackFilter filter=new SignalLookbackFilter();
			DateTime dateT0=new DateTime(2026,5,1,10,0,0);
			Signalod signalA=new Signalod { SignalNum=1,SigDateTime=dateT0.AddSeconds(5),IType=InvalidType.Print };
			Signalod signalB=new Signalod { SignalNum=2,SigDateTime=dateT0.AddSeconds(8),IType=InvalidType.Print };
			DateTime dateTQuery=filter.GetQuerySince("high",dateT0,20000);
			List<Signalod> listFirst=filter.Filter("high",new List<Signalod> { signalA },dateT0,dateTQuery,20000);
			Assert.AreEqual(1,listFirst.Count);
			//The next check looks back over signal A again, and also finds signal B, which reached the replica late.
			dateTQuery=filter.GetQuerySince("high",dateT0.AddSeconds(10),20000);
			List<Signalod> listSecond=filter.Filter("high",new List<Signalod> { signalA,signalB },dateT0.AddSeconds(10),dateTQuery,20000);
			CollectionAssert.AreEqual(new long[] { 2 },listSecond.Select(x => x.SignalNum).ToList(),"A printed signal must not print twice.");
			//Another stream (the appointment refresh) has its own memory: it still gets signal A.
			DateTime dateTQueryAppt=filter.GetQuerySince("appt",dateT0,20000);
			Assert.AreEqual(1,filter.Filter("appt",new List<Signalod> { signalA },dateT0,dateTQueryAppt,20000).Count);
		}

		[TestMethod]
		public void Filter_ForgetsSignalsOlderThanTheWindow_AndNeverChecksThatFarBackAgain() {
			SignalLookbackFilter filter=new SignalLookbackFilter();
			DateTime dateT0=new DateTime(2026,5,1,10,0,0);
			DateTime dateTQuery=filter.GetQuerySince("s",dateT0,5000);
			filter.Filter("s",new List<Signalod> { new Signalod { SignalNum=1,SigDateTime=dateT0.AddSeconds(1) } },dateT0,dateTQuery,5000);
			Assert.AreEqual(1,filter.GetCountSeen("s"));
			dateTQuery=filter.GetQuerySince("s",dateT0.AddSeconds(60),5000);
			Assert.AreEqual(dateT0.AddSeconds(55),dateTQuery);
			filter.Filter("s",new List<Signalod>(),dateT0.AddSeconds(60),dateTQuery,5000);
			Assert.AreEqual(0,filter.GetCountSeen("s"),"Signal 1 is forgotten.");
			//The caller's time moves back (FormOpenDental sets it to the newest signal returned, and a late signal is older than the last check).
			//The check still starts at the forgotten mark, so the forgotten signal 1 cannot come back.
			Assert.AreEqual(dateT0.AddSeconds(55),filter.GetQuerySince("s",dateT0.AddSeconds(5),5000));
			Assert.AreEqual(dateT0.AddSeconds(55),filter.GetQuerySince("s",dateT0.AddSeconds(58),5000));
			Assert.AreEqual(dateT0.AddSeconds(56),filter.GetQuerySince("s",dateT0.AddSeconds(61),5000),"Forward again: the normal look-back.");
		}

		[TestMethod]
		public void Filter_TurningLookbackOffDropsTheStream() {
			SignalLookbackFilter filter=new SignalLookbackFilter();
			DateTime dateT0=new DateTime(2026,5,1,10,0,0);
			DateTime dateTQuery=filter.GetQuerySince("s",dateT0,5000);
			filter.Filter("s",new List<Signalod> { new Signalod { SignalNum=1,SigDateTime=dateT0.AddSeconds(1) } },dateT0,dateTQuery,5000);
			Assert.AreEqual(dateT0.AddSeconds(2),filter.GetQuerySince("s",dateT0.AddSeconds(2),0));
			Assert.AreEqual(0,filter.GetCountSeen("s"));
			List<Signalod> list=new List<Signalod> { new Signalod { SignalNum=1 } };
			Assert.AreSame(list,filter.Filter("s",list,dateT0,dateT0,0),"With no look-back the list is returned as is.");
		}

		///<summary>One caller of Signalods.RefreshTimed against a simulated signalod table, checked the way FormOpenDental.GetAndProcessSignals
		///checks the regular signals: the next check's time is the newest SigDateTime among the signals returned (after the look-back filter), and it
		///stays the same when none came back. A late signal therefore moves that time back.</summary>
		private class SimulatedCaller {
			private readonly SignalLookbackFilter _filter;
			private readonly string _streamKey;
			private readonly int _lookbackMs;
			///<summary>Each signal and the server time it reaches the database this workstation reads.</summary>
			private readonly List<Tuple<Signalod,DateTime>> _listRows=new List<Tuple<Signalod,DateTime>>();
			///<summary>The caller's time (Signalods.DateTRegularPrioritySignalLastRefreshed).</summary>
			public DateTime DateTSince;
			///<summary>The time the last check started after.</summary>
			public DateTime DateTLastQuery;
			public readonly List<long> ListSignalNumsReturned=new List<long>();

			public SimulatedCaller(SignalLookbackFilter filter,string streamKey,int lookbackMs,DateTime dateTStart) {
				_filter=filter;
				_streamKey=streamKey;
				_lookbackMs=lookbackMs;
				DateTSince=dateTStart;
			}

			public void AddSignal(long signalNum,DateTime dateTSig,DateTime dateTArrives) {
				_listRows.Add(Tuple.Create(new Signalod { SignalNum=signalNum,SigDateTime=dateTSig },dateTArrives));
			}

			///<summary>One check at server time dateTNow: the rows that have arrived, after the check's time and before the current second (as the
			///RefreshTimed query), through the filter. Returns the SignalNums let through.</summary>
			public List<long> Poll(DateTime dateTNow) {
				DateTime dateTQuery=_filter.GetQuerySince(_streamKey,DateTSince,_lookbackMs);
				DateTLastQuery=dateTQuery;
				DateTime dateTNowSecond=new DateTime(dateTNow.Ticks-(dateTNow.Ticks%TimeSpan.TicksPerSecond));
				List<Signalod> listRows=_listRows.Where(x => x.Item2<=dateTNow && x.Item1.SigDateTime>dateTQuery && x.Item1.SigDateTime<dateTNowSecond)
					.Select(x => x.Item1).OrderBy(x => x.SigDateTime).ToList();
				List<Signalod> listNew=_filter.Filter(_streamKey,listRows,DateTSince,dateTQuery,_lookbackMs);
				if(listNew.Count>0) {
					DateTSince=listNew.Max(x => x.SigDateTime);
				}
				ListSignalNumsReturned.AddRange(listNew.Select(x => x.SignalNum));
				return listNew.Select(x => x.SignalNum).ToList();
			}

			public void AssertNoSignalReturnedTwice() {
				List<long> listTwice=ListSignalNumsReturned.GroupBy(x => x).Where(x => x.Count()>1).Select(x => x.Key).ToList();
				Assert.AreEqual(0,listTwice.Count,"Signals processed twice: "+string.Join(",",listTwice));
			}
		}

		[TestMethod]
		public void Filter_LateSignalMovesTheCallerBack_ForgottenSignalIsNotReturnedAgain() {
			//5 second look-back. Check 1 returns A@94 and B@100. Check 2 returns only the late C@97 and forgets A; the caller's time moves back to 97.
			//Check 3 would start at 92 and return A again if it reached back into what was forgotten.
			DateTime dateT=new DateTime(2026,5,1,10,0,0);
			SimulatedCaller caller=new SimulatedCaller(new SignalLookbackFilter(),"regular",5000,dateT.AddSeconds(88));
			CollectionAssert.AreEqual(new long[0],caller.Poll(dateT.AddSeconds(89)));
			caller.AddSignal(1,dateT.AddSeconds(94),dateT.AddSeconds(94));//A
			caller.AddSignal(2,dateT.AddSeconds(100),dateT.AddSeconds(100));//B
			CollectionAssert.AreEqual(new long[] { 1,2 },caller.Poll(dateT.AddSeconds(101)));
			Assert.AreEqual(dateT.AddSeconds(100),caller.DateTSince);
			caller.AddSignal(3,dateT.AddSeconds(97),dateT.AddSeconds(101.5));//C, late
			CollectionAssert.AreEqual(new long[] { 3 },caller.Poll(dateT.AddSeconds(102)),"The look-back finds the late signal.");
			Assert.AreEqual(dateT.AddSeconds(95),caller.DateTLastQuery);
			Assert.AreEqual(dateT.AddSeconds(97),caller.DateTSince,"The caller's time moved back.");
			CollectionAssert.AreEqual(new long[0],caller.Poll(dateT.AddSeconds(103)),"A must not come back.");
			Assert.AreEqual(dateT.AddSeconds(95),caller.DateTLastQuery,"The check does not start before the forgotten mark (it would be 92).");
			caller.AssertNoSignalReturnedTwice();
		}

		[TestMethod]
		public void Filter_LateSignalsInAQuietPeriod_DoNotWalkTheCheckBackWindowByWindow() {
			//10 second look-back. A@3 is processed and forgotten; then late signals keep moving the caller's time back.
			DateTime dateT0=new DateTime(2026,5,1,10,0,0);
			SimulatedCaller caller=new SimulatedCaller(new SignalLookbackFilter(),"regular",10000,dateT0);
			caller.Poll(dateT0.AddSeconds(1));
			caller.AddSignal(1,dateT0.AddSeconds(3),dateT0.AddSeconds(3));//A
			CollectionAssert.AreEqual(new long[] { 1 },caller.Poll(dateT0.AddSeconds(4)));
			caller.AddSignal(2,dateT0.AddSeconds(15),dateT0.AddSeconds(15));//B
			CollectionAssert.AreEqual(new long[] { 2 },caller.Poll(dateT0.AddSeconds(16)));
			CollectionAssert.AreEqual(new long[0],caller.Poll(dateT0.AddSeconds(17)));//Starts at 5: A is forgotten.
			caller.AddSignal(3,dateT0.AddSeconds(12),dateT0.AddSeconds(17.5));//C, 5.5 seconds late
			CollectionAssert.AreEqual(new long[] { 3 },caller.Poll(dateT0.AddSeconds(18)));
			Assert.AreEqual(dateT0.AddSeconds(12),caller.DateTSince);
			caller.AddSignal(4,dateT0.AddSeconds(8),dateT0.AddSeconds(18.5));//D, later still
			CollectionAssert.AreEqual(new long[] { 4 },caller.Poll(dateT0.AddSeconds(19)),"Without the mark this check (from 2) would also return A.");
			Assert.AreEqual(dateT0.AddSeconds(8),caller.DateTSince);
			CollectionAssert.AreEqual(new long[0],caller.Poll(dateT0.AddSeconds(20)),"Without the mark this check (from the floor) would return A.");
			Assert.AreEqual(dateT0.AddSeconds(5),caller.DateTLastQuery);
			caller.AssertNoSignalReturnedTwice();
		}

		[TestMethod]
		public void Filter_SimulatedLateSignals_EachReturnedExactlyOnce() {
			//Ten minutes of signals that reach the workstation's database up to 9 seconds late, checked every 2 seconds with a 10 second look-back,
			//the caller's time following FormOpenDental's rule. Every signal must come back once: none missed, none twice.
			Random random=new Random(20261025);
			DateTime dateT0=new DateTime(2026,5,1,10,0,0);
			SimulatedCaller caller=new SimulatedCaller(new SignalLookbackFilter(),"regular",10000,dateT0);
			caller.Poll(dateT0);
			long signalNum=0;
			for(int second=1;second<=600;second++) {
				int countSignals=random.Next(0,4);
				for(int i=0;i<countSignals;i++) {
					DateTime dateTSig=dateT0.AddSeconds(second);
					caller.AddSignal(++signalNum,dateTSig,dateTSig.AddMilliseconds(random.Next(0,9000)));
				}
				if(second%2==0) {
					caller.Poll(dateT0.AddSeconds(second).AddMilliseconds(random.Next(0,1000)));
				}
			}
			for(int second=602;second<=640;second+=2) {
				caller.Poll(dateT0.AddSeconds(second));//Quiet period: the last late signals arrive.
			}
			Assert.IsTrue(signalNum>500);
			caller.AssertNoSignalReturnedTwice();
			CollectionAssert.AreEquivalent(Enumerable.Range(1,(int)signalNum).Select(x => (long)x).ToList(),caller.ListSignalNumsReturned,
				"Every signal less late than the look-back is found.");
		}

		[TestMethod]
		public void QuerySince_ServerClockBackMoreThanAMinute_StartsTheStreamOver() {
			SignalLookbackFilter filter=new SignalLookbackFilter();
			DateTime dateT0=new DateTime(2026,11,1,1,0,0);
			filter.GetQuerySince("high",dateT0,10000);
			DateTime dateTSince=dateT0.AddMinutes(59);
			DateTime dateTQuery=filter.GetQuerySince("high",dateTSince,10000);
			Assert.AreEqual(dateT0.AddMinutes(58).AddSeconds(50),dateTQuery);
			filter.Filter("high",new List<Signalod>(),dateTSince,dateTQuery,10000);
			//Up to a minute before the forgotten mark: a move back the late signals can cause. The check stays at the mark.
			Assert.AreEqual(dateTQuery,filter.GetQuerySince("high",dateT0.AddMinutes(58),10000));
			//Daylight saving time ends: the server's clock is back at 1:00. The stream starts over, as on its first check, instead of finding
			//nothing for an hour.
			Assert.AreEqual(dateT0.AddSeconds(5),filter.GetQuerySince("high",dateT0.AddSeconds(5),10000));
			Assert.AreEqual(dateT0.AddSeconds(5),filter.GetQuerySince("high",dateT0.AddSeconds(11),10000),"The new floor.");
			Assert.AreEqual(dateT0.AddSeconds(10),filter.GetQuerySince("high",dateT0.AddSeconds(20),10000));
		}

		[TestMethod]
		public void QuerySince_TimeNearMinValue_DoesNotThrow() {
			SignalLookbackFilter filter=new SignalLookbackFilter();
			Assert.AreEqual(DateTime.MinValue,filter.GetQuerySince("s",DateTime.MinValue,60000));
			Assert.AreEqual(DateTime.MinValue,filter.GetQuerySince("s",DateTime.MinValue.AddSeconds(30),60000));
		}

		[TestMethod]
		public void Filter_OwnSignalFoundOnlyByTheLookback_IsDropped() {
			//Shutdown all workstations (FormOpenDental.menuItemShutdown_Click): the high-priority time is pushed 5 seconds ahead, then this workstation
			//inserts ShutDownNow. A 10 second look-back reaches back over it, but this workstation must not shut itself down.
			long msNow=5000000;
			SignalLookbackFilter filter=new SignalLookbackFilter(() => msNow);
			DateTime dateTNow=new DateTime(2026,5,1,10,0,0);
			DateTime dateTQuery=filter.GetQuerySince("high",dateTNow.AddSeconds(-60),10000);//Checked since startup, a minute ago.
			filter.Filter("high",new List<Signalod>(),dateTNow.AddSeconds(-60),dateTQuery,10000);
			Signalod signalOwn=new Signalod { SignalNum=50,SigDateTime=dateTNow,IType=InvalidType.ShutDownNow };
			Signalod signalOther=new Signalod { SignalNum=51,SigDateTime=dateTNow.AddSeconds(-1),IType=InvalidType.Print };
			DateTime dateTSince=dateTNow.AddSeconds(5);
			filter.NoteOwnSignal(50);
			dateTQuery=filter.GetQuerySince("high",dateTSince,10000);
			Assert.AreEqual(dateTNow.AddSeconds(-5),dateTQuery,"The look-back reaches back over the signal.");
			List<Signalod> listFirst=filter.Filter("high",new List<Signalod> { signalOther,signalOwn },dateTSince,dateTQuery,10000);
			CollectionAssert.AreEqual(new long[] { 51 },listFirst.Select(x => x.SignalNum).ToList(),"Another workstation's signal is found; its own is not.");
			//The next poll checks since the time the previous one started.
			msNow+=2000;
			dateTSince=dateTNow.AddSeconds(1);
			dateTQuery=filter.GetQuerySince("high",dateTSince,10000);
			Assert.AreEqual(0,filter.Filter("high",new List<Signalod> { signalOther,signalOwn },dateTSince,dateTQuery,10000).Count);
		}

		[TestMethod]
		public void Filter_OwnSignalAfterTheCallersTime_IsReturnedOnceAsIn243() {
			long msNow=0;
			SignalLookbackFilter filter=new SignalLookbackFilter(() => msNow);
			DateTime dateT0=new DateTime(2026,5,1,10,0,0);
			filter.Filter("s",new List<Signalod>(),dateT0,filter.GetQuerySince("s",dateT0,10000),10000);
			filter.NoteOwnSignal(7);
			Signalod signalOwn=new Signalod { SignalNum=7,SigDateTime=dateT0.AddSeconds(3) };
			DateTime dateTSince=dateT0.AddSeconds(2);
			DateTime dateTQuery=filter.GetQuerySince("s",dateTSince,10000);
			CollectionAssert.AreEqual(new long[] { 7 },filter.Filter("s",new List<Signalod> { signalOwn },dateTSince,dateTQuery,10000).Select(x => x.SignalNum).ToList(),
				"24.3 returns a workstation's own signal newer than the check's time (its own cache signals, for example).");
			dateTSince=dateT0.AddSeconds(3);
			dateTQuery=filter.GetQuerySince("s",dateTSince,10000);
			Assert.AreEqual(0,filter.Filter("s",new List<Signalod> { signalOwn },dateTSince,dateTQuery,10000).Count);
		}

		[TestMethod]
		public void OwnSignals_ForgottenAfterTheKeepTimeAndOverTheCap() {
			long msNow=0;
			SignalLookbackFilter filter=new SignalLookbackFilter(() => msNow);
			filter.NoteOwnSignal(0);
			filter.NoteOwnSignal(-3);
			Assert.AreEqual(0,filter.GetCountOwn(),"No SignalNum: nothing to remember.");
			filter.NoteOwnSignal(1);
			filter.NoteOwnSignal(1);
			Assert.AreEqual(1,filter.GetCountOwn());
			msNow+=SignalLookbackFilter.OWN_SIGNAL_KEEP_MS;
			filter.NoteOwnSignal(2);
			Assert.AreEqual(2,filter.GetCountOwn());
			msNow+=1;
			Assert.AreEqual(1,filter.GetCountOwn(),"Signal 1 is older than the keep time.");
			for(int i=0;i<SignalLookbackFilter.OWN_SIGNAL_MAX_COUNT+5;i++) {
				filter.NoteOwnSignal(100+i);
			}
			Assert.AreEqual(SignalLookbackFilter.OWN_SIGNAL_MAX_COUNT,filter.GetCountOwn());
			Assert.IsTrue(SignalLookbackFilter.OWN_SIGNAL_KEEP_MS>ReplicationDelay.MAX_DELAY_MS);
		}

		[TestMethod]
		public void NoteOwnSignal_NeverThrows() {
			SignalLookbackFilter filter=new SignalLookbackFilter(() => { throw new InvalidOperationException("clock"); });
			filter.NoteOwnSignal(1);
			Assert.AreEqual(0,filter.GetCountOwn());
		}
	}
}
