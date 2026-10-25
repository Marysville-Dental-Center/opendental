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
			List<Signalod> listFirst=filter.Filter("high",new List<Signalod> { signalA },dateTQuery,20000);
			Assert.AreEqual(1,listFirst.Count);
			//The next check looks back over signal A again, and also finds signal B, which reached the replica late.
			dateTQuery=filter.GetQuerySince("high",dateT0.AddSeconds(10),20000);
			List<Signalod> listSecond=filter.Filter("high",new List<Signalod> { signalA,signalB },dateTQuery,20000);
			CollectionAssert.AreEqual(new long[] { 2 },listSecond.Select(x => x.SignalNum).ToList(),"A printed signal must not print twice.");
			//Another stream (the appointment refresh) has its own memory: it still gets signal A.
			DateTime dateTQueryAppt=filter.GetQuerySince("appt",dateT0,20000);
			Assert.AreEqual(1,filter.Filter("appt",new List<Signalod> { signalA },dateTQueryAppt,20000).Count);
		}

		[TestMethod]
		public void Filter_ForgetsSignalsOlderThanTheWindow() {
			SignalLookbackFilter filter=new SignalLookbackFilter();
			DateTime dateT0=new DateTime(2026,5,1,10,0,0);
			DateTime dateTQuery=filter.GetQuerySince("s",dateT0,5000);
			filter.Filter("s",new List<Signalod> { new Signalod { SignalNum=1,SigDateTime=dateT0.AddSeconds(1) } },dateTQuery,5000);
			Assert.AreEqual(1,filter.GetCountSeen("s"));
			dateTQuery=filter.GetQuerySince("s",dateT0.AddSeconds(60),5000);
			filter.Filter("s",new List<Signalod>(),dateTQuery,5000);
			Assert.AreEqual(0,filter.GetCountSeen("s"),"A signal before the look-back window can never be returned again.");
		}

		[TestMethod]
		public void Filter_TurningLookbackOffDropsTheStream() {
			SignalLookbackFilter filter=new SignalLookbackFilter();
			DateTime dateT0=new DateTime(2026,5,1,10,0,0);
			DateTime dateTQuery=filter.GetQuerySince("s",dateT0,5000);
			filter.Filter("s",new List<Signalod> { new Signalod { SignalNum=1,SigDateTime=dateT0.AddSeconds(1) } },dateTQuery,5000);
			Assert.AreEqual(dateT0.AddSeconds(2),filter.GetQuerySince("s",dateT0.AddSeconds(2),0));
			Assert.AreEqual(0,filter.GetCountSeen("s"));
			List<Signalod> list=new List<Signalod> { new Signalod { SignalNum=1 } };
			Assert.AreSame(list,filter.Filter("s",list,dateT0,0),"With no look-back the list is returned as is.");
		}
	}
}
