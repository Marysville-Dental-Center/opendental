// v25_2-server-replication-delay with a stand-in replica: two scratch databases on the local MariaDB, the "main server" and a "replica" that holds
// older data. The read-only server connection is pointed at the replica, so the tests see which database a read actually went to.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BSERVERTests {
	[TestClass]
	public class ReplicationDelayDbTests {
		private const string PROBE="BServerProbe";
		private static string _dbPrimary;
		private static string _dbReplica;
		private static string _skipReason;
		private static Func<long> _funcGetNowMsOriginal;
		private static Func<bool> _funcHasReadOnlyServerOriginal;
		///<summary>The fake monotonic clock, in milliseconds.</summary>
		private static long _msNow;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ServerTestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			_dbPrimary=ServerTestDb.ScratchName("main");
			_dbReplica=ServerTestDb.ScratchName("replica");
			ServerTestDb.Create(_dbPrimary,"preference","replicationserver","signalod");
			ServerTestDb.Create(_dbReplica,"preference");
			ServerTestDb.Seed(_dbReplica,"INSERT INTO preference (PrefName,ValueString) VALUES ('"+PROBE+"','replica')");
			_funcGetNowMsOriginal=ReplicationDelay.FuncGetNowMs;
			_funcHasReadOnlyServerOriginal=PrefC.HasReadOnlyServer;
			ReplicationDelay.FuncGetNowMs=() => _msNow;
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			ReplicationDelay.FuncGetNowMs=_funcGetNowMsOriginal;
			PrefC.HasReadOnlyServer=_funcHasReadOnlyServerOriginal;
			ReplicationDelay.ClearLastWrite();
			ReplicationDelay.SignalLookback.Clear();
			ConnectionStoreBase.ClearConnectionDictionary();
			ServerTestDb.Drop(_dbPrimary);
			ServerTestDb.Drop(_dbReplica);
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ServerTestDb.Connect(_dbPrimary);
			PrefC.HasReadOnlyServer=_funcHasReadOnlyServerOriginal;
			//Every test starts from an office with no read-only server, no replication servers, no delays and no signals.
			ServerTestDb.Seed(_dbPrimary,
				"DELETE FROM preference",
				"INSERT INTO preference (PrefName,ValueString) VALUES ('"+PROBE+"','primary'),('ReadOnlyServerCompName',''),('ReadOnlyServerURI',''),"
					+"('ReadOnlyServerDbName',''),('ReadOnlyServerMySqlUser',''),('ReadOnlyServerMySqlPassHash',''),('ReadOnlyServerSslCa','')",
				"DELETE FROM replicationserver",
				"DELETE FROM signalod");
			Prefs.RefreshCache();
			ReplicationServers.RefreshCache();
			ConnectionStore.OverrideConnection(ConnectionNames.DentalOfficeReadOnlyServer,
				new CentralConnection { ServerName=ServerTestDb.SERVER,DatabaseName=_dbReplica,MySqlUser="root",MySqlPassword="" });
			_msNow=1000000;
			ReplicationDelay.ClearLastWrite();
			ReplicationDelay.SignalLookback.Clear();
		}

		///<summary>Which database a read meant for the read-only server went to.</summary>
		private static string ReadProbeThroughReadOnlyServer() {
			return ReportsComplex.RunFuncOnReadOnlyServer(() => DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+PROBE+"'"));
		}

		private static void SetReadOnlyServerConfigured() {
			ServerTestDb.Seed(_dbPrimary,"UPDATE preference SET ValueString='"+ServerTestDb.SERVER+"' WHERE PrefName='ReadOnlyServerCompName'");
			Prefs.RefreshCache();
		}

		[TestMethod]
		public void Settings_MissingRowsReadAsZero_SaveInsertsOnceThenUpdates() {
			Assert.AreEqual(0,ReplicationDelay.GetReadDelayMs());
			Assert.AreEqual(0,ReplicationDelay.GetSignalLookbackMs());
			Assert.IsFalse(ReplicationDelay.SaveSettings(0,0),"Saving the defaults changes nothing.");
			Assert.AreEqual("0",ServerTestDb.Scalar(_dbPrimary,"SELECT COUNT(*) FROM preference WHERE PrefName LIKE 'ForkReplication%'"),
				"An office that never sets the delays gets no rows.");
			Assert.IsTrue(ReplicationDelay.SaveSettings(1500,3000));
			Assert.AreEqual("1500",ServerTestDb.Scalar(_dbPrimary,"SELECT ValueString FROM preference WHERE PrefName='"+ReplicationDelay.PREF_READ_DELAY_MS+"'"));
			Assert.AreEqual("3000",ServerTestDb.Scalar(_dbPrimary,"SELECT ValueString FROM preference WHERE PrefName='"+ReplicationDelay.PREF_SIGNAL_LOOKBACK_MS+"'"));
			Assert.IsFalse(ReplicationDelay.SaveSettings(1500,3000),"Saving the same values changes nothing.");
			Assert.IsTrue(ReplicationDelay.SaveSettings(70000,-1));
			Prefs.RefreshCache();//What another workstation reads after the Prefs signal.
			Assert.AreEqual(ReplicationDelay.MAX_DELAY_MS,ReplicationDelay.GetReadDelayMs());
			Assert.AreEqual(0,ReplicationDelay.GetSignalLookbackMs());
			Assert.AreEqual("1",ServerTestDb.Scalar(_dbPrimary,"SELECT COUNT(*) FROM preference WHERE PrefName='"+ReplicationDelay.PREF_READ_DELAY_MS+"'"));
		}

		[TestMethod]
		public void RawPrefs_UpsertRawInsertsTheRowAndUpdatesTheCache() {
			Assert.AreEqual("dflt",PrefC.GetRawOrDefault("ForkBServerTestPref","dflt"));
			Assert.IsTrue(PrefC.UpsertRaw("ForkBServerTestPref","a"));
			Assert.AreEqual("a",PrefC.GetRawOrDefault("ForkBServerTestPref","dflt"));
			Assert.IsFalse(PrefC.UpsertRaw("ForkBServerTestPref","a"));
			Assert.IsTrue(PrefC.UpsertRaw("ForkBServerTestPref","b"));
			Assert.AreEqual("b",ServerTestDb.Scalar(_dbPrimary,"SELECT ValueString FROM preference WHERE PrefName='ForkBServerTestPref'"));
			Assert.AreEqual("1",ServerTestDb.Scalar(_dbPrimary,"SELECT COUNT(*) FROM preference WHERE PrefName='ForkBServerTestPref'"));
		}

		[TestMethod]
		public void AllNoNotesDefault_RoundTrip() {
			Assert.IsFalse(RpProcNote.GetAllNoNotesDefault(),"A database without the row reads as off.");
			Assert.IsFalse(RpProcNote.SetAllNoNotesDefault(false),"Saving the default changes nothing.");
			Assert.IsNull(ServerTestDb.Scalar(_dbPrimary,"SELECT ValueString FROM preference WHERE PrefName='"+RpProcNote.PREF_ALL_NO_NOTES+"'"));
			Assert.IsTrue(RpProcNote.SetAllNoNotesDefault(true));
			Prefs.RefreshCache();
			Assert.IsTrue(RpProcNote.GetAllNoNotesDefault());
			Assert.IsFalse(RpProcNote.SetAllNoNotesDefault(true));
		}

		[TestMethod]
		public void ReadDelayOff_ReadsGoToTheReplica() {
			SetReadOnlyServerConfigured();
			PrefC.UpsertRaw("ForkBServerTestPref","written");//A write, with the delay off.
			Assert.AreEqual("replica",ReadProbeThroughReadOnlyServer());
		}

		[TestMethod]
		public void ReadDelay_ReadsGoToTheMainServerForTheDelayAfterAWrite() {
			SetReadOnlyServerConfigured();
			Assert.AreEqual("replica",ReadProbeThroughReadOnlyServer());
			ReplicationDelay.SaveSettings(5000,0);//Saving is itself a write, at _msNow.
			Assert.AreEqual("primary",ReadProbeThroughReadOnlyServer(),"Right after the write the replica may not have it yet.");
			_msNow+=4999;
			Assert.AreEqual("primary",ReadProbeThroughReadOnlyServer());
			_msNow+=1;
			Assert.AreEqual("replica",ReadProbeThroughReadOnlyServer(),"After the delay, reads go back to the replica.");
			PrefC.UpsertRaw("ForkBServerTestPref","written");//Any write starts the delay again.
			Assert.AreEqual("primary",ReadProbeThroughReadOnlyServer());
		}

		[TestMethod]
		public void ReadDelay_IgnoredWithoutReadOnlyServer() {
			ReplicationDelay.SaveSettings(5000,0);
			Assert.IsFalse(ReplicationDelay.IsReadOnlyServerConfigured());
			Assert.IsFalse(ReplicationDelay.IsReadFromPrimaryRequired());
		}

		[TestMethod]
		public void ReadDelay_CacheFilledRightAfterAWriteComesFromTheMainServer() {
			SetReadOnlyServerConfigured();
			ReplicationDelay.SaveSettings(5000,0);
			PrefC.HasReadOnlyServer=() => true;//As the program sets it once the read-only server may be used for cache fills.
			try {
				Prefs.RefreshCache();
				Assert.AreEqual("primary",PrefC.GetRawOrDefault(PROBE,""),"The preference cache refilled right after a save must not be stale.");
				_msNow+=5000;
				Prefs.RefreshCache();
				Assert.AreEqual("replica",PrefC.GetRawOrDefault(PROBE,""),"After the delay, cache fills use the read-only server again.");
			}
			finally {
				PrefC.HasReadOnlyServer=_funcHasReadOnlyServerOriginal;
				Prefs.RefreshCache();
			}
		}

		///<summary>Inserts a signal straight into the main database with the given server time.</summary>
		private static void InsertSignal(long signalNum,DateTime dateTSig,InvalidType invalidType=InvalidType.Print) {
			ServerTestDb.Seed(_dbPrimary,"INSERT INTO signalod (SignalNum,IType,SigDateTime,FKeyType,DateViewing) VALUES ("+signalNum+","
				+(int)invalidType+",'"+dateTSig.ToString("yyyy-MM-dd HH:mm:ss")+"','Undefined','0001-01-01')");
		}

		private static DateTime GetServerNow() {
			return PIn.DateT(DataCore.GetScalar("SELECT NOW()"));
		}

		///<summary>A workstation checks at now-60s, then at now-5s. Between the checks a signal from now-15s reaches its database late.</summary>
		private static List<long> RunLateSignalScenario(out List<long> listThirdCheck) {
			List<InvalidType> listTypes=new List<InvalidType> { InvalidType.Print };
			DateTime dateTNow=GetServerNow();
			List<Signalod> listFirst=Signalods.RefreshTimed(dateTNow.AddSeconds(-60),listTypes);
			Assert.AreEqual(0,listFirst.Count);
			InsertSignal(1,dateTNow.AddSeconds(-15));//Late: older than the next check's starting point.
			List<Signalod> listSecond=Signalods.RefreshTimed(dateTNow.AddSeconds(-5),listTypes);
			listThirdCheck=Signalods.RefreshTimed(dateTNow.AddSeconds(-5),listTypes).Select(x => x.SignalNum).ToList();
			return listSecond.Select(x => x.SignalNum).ToList();
		}

		[TestMethod]
		public void SignalWriteDelayOff_LateSignalIsMissed() {
			ServerTestDb.Seed(_dbPrimary,"INSERT INTO replicationserver (ReplicationServerNum,Descript,ServerId) VALUES (1,'Synthetic server',1)");
			ReplicationServers.RefreshCache();
			List<long> listSecond=RunLateSignalScenario(out List<long> listThird);
			Assert.AreEqual(0,listSecond.Count,"24.3 behavior: the late signal is never seen.");
		}

		[TestMethod]
		public void SignalWriteDelay_LateSignalIsFoundOnce() {
			ServerTestDb.Seed(_dbPrimary,"INSERT INTO replicationserver (ReplicationServerNum,Descript,ServerId) VALUES (1,'Synthetic server',1)");
			ReplicationServers.RefreshCache();
			ReplicationDelay.SaveSettings(0,30000);
			Assert.AreEqual(30000,ReplicationDelay.GetEffectiveSignalLookbackMs());
			List<long> listSecond=RunLateSignalScenario(out List<long> listThird);
			CollectionAssert.AreEqual(new long[] { 1 },listSecond,"The look-back finds the late signal.");
			Assert.AreEqual(0,listThird.Count,"A signal already processed is not returned again.");
		}

		[TestMethod]
		public void SignalWriteDelay_IgnoredWithoutReplicationOrReadOnlyServer() {
			ReplicationDelay.SaveSettings(0,30000);
			Assert.AreEqual(0,ReplicationDelay.GetEffectiveSignalLookbackMs());
			List<long> listSecond=RunLateSignalScenario(out List<long> listThird);
			Assert.AreEqual(0,listSecond.Count);
		}

		[TestMethod]
		public void SignalWriteDelay_ShutdownAllWorkstations_SenderDoesNotShutItselfDown() {
			ServerTestDb.Seed(_dbPrimary,"INSERT INTO replicationserver (ReplicationServerNum,Descript,ServerId) VALUES (1,'Synthetic server',1)",
				"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
			Prefs.RefreshCache();
			ReplicationServers.RefreshCache();
			ReplicationDelay.SaveSettings(0,10000);
			Assert.AreEqual(10000,ReplicationDelay.GetEffectiveSignalLookbackMs());
			List<InvalidType> listTypesHigh=new List<InvalidType> { InvalidType.ShutDownNow,InvalidType.ActiveInstance,InvalidType.Print };
			DateTime dateTNow=GetServerNow();
			//The workstation has checked the high-priority signals since it started, a minute ago.
			Assert.AreEqual(0,Signalods.RefreshTimed(dateTNow.AddSeconds(-60),listTypesHigh).Count);
			//Shutdown all workstations, as FormOpenDental.menuItemShutdown_Click does it: skip the next 5 seconds, then insert the signal.
			DateTime dateTSkip=dateTNow.AddSeconds(5);
			long signalNumOwn=Signalods.Insert(new Signalod { IType=InvalidType.ShutDownNow });
			Assert.IsTrue(signalNumOwn>0);
			//RefreshTimed reads only signals older than the server's current second. Date the row a second back so this check can already see it.
			ServerTestDb.Seed(_dbPrimary,"UPDATE signalod SET SigDateTime='"+dateTNow.AddSeconds(-1).ToString("yyyy-MM-dd HH:mm:ss")+"' "
				+"WHERE SignalNum="+signalNumOwn);
			//Another workstation's signal from the same moment, to show that the look-back reaches back over the shutdown signal.
			long signalNumOther=signalNumOwn+1000;
			InsertSignal(signalNumOther,dateTNow.AddSeconds(-2));
			List<Signalod> listFirst=Signalods.RefreshTimed(dateTSkip,listTypesHigh);
			CollectionAssert.AreEqual(new long[] { signalNumOther },listFirst.Select(x => x.SignalNum).ToList(),
				"The look-back finds the other workstation's signal, but never this workstation's own shutdown signal.");
			//The next poll checks since the time the previous poll started.
			List<Signalod> listSecond=Signalods.RefreshTimed(dateTNow,listTypesHigh);
			Assert.IsFalse(listSecond.Exists(x => x.IType==InvalidType.ShutDownNow),"The workstation that sent the shutdown must not shut down.");
			Assert.AreEqual(0,listSecond.Count);
		}

		[TestMethod]
		public void SignalWriteDelay_OtherWorkstationsShutdownSignal_IsFoundOnce() {
			ServerTestDb.Seed(_dbPrimary,"INSERT INTO replicationserver (ReplicationServerNum,Descript,ServerId) VALUES (1,'Synthetic server',1)");
			ReplicationServers.RefreshCache();
			ReplicationDelay.SaveSettings(0,10000);
			List<InvalidType> listTypesHigh=new List<InvalidType> { InvalidType.ShutDownNow,InvalidType.ActiveInstance,InvalidType.Print };
			DateTime dateTNow=GetServerNow();
			Assert.AreEqual(0,Signalods.RefreshTimed(dateTNow.AddSeconds(-60),listTypesHigh).Count);
			InsertSignal(77,dateTNow.AddSeconds(-3),InvalidType.ShutDownNow);//Reached this database late.
			CollectionAssert.AreEqual(new long[] { 77 },Signalods.RefreshTimed(dateTNow,listTypesHigh).Select(x => x.SignalNum).ToList());
			Assert.AreEqual(0,Signalods.RefreshTimed(dateTNow.AddSeconds(1),listTypesHigh).Count);
		}

		[TestMethod]
		public void SignalWriteDelay_AppliesWithReadOnlyServerToo() {
			SetReadOnlyServerConfigured();
			ReplicationDelay.SaveSettings(0,30000);
			Assert.AreEqual(30000,ReplicationDelay.GetEffectiveSignalLookbackMs());
		}
	}
}
