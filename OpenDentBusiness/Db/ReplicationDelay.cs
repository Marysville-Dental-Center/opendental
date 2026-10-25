// B-SERVER (v25_2-server-replication-delay): read and write delays for offices that use a read-only server or replication.
// Open Dental 25.2's release notes say only that new preferences set a read/write delay when replication is used. Names, units and behavior are
// not public, so everything below is our own design (docs/specs/features-25.2.md, docs/port-notes/B-SERVER.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Two global settings, kept in fork-owned preference rows that are not in the PrefName enum (a missing row reads as 0, so an adopted
	///Open Dental database needs no migration):
	///<para>Read delay after write (ForkReplicationReadDelayMs). With replication, data written on the main server reaches a replica after a lag.
	///For this many milliseconds after this workstation wrote to the database, reads that would go to the read-only server
	///(ReportsComplex.RunFuncOnReadOnlyServer, which also fills the caches) run on the main server instead, so users do not see stale data right
	///after saving.</para>
	///<para>Write delay for signals (ForkReplicationSignalLookbackMs). When a workstation checks for cache-refresh signals (signalod), it looks back
	///this many milliseconds before its last check, so a signal that reaches its replica late (daisy-chain replication) is not missed. Signals it
	///already processed are filtered out, so nothing is processed twice.</para>
	///<para>Both range from 0 to 60000 ms. 0 (the default) keeps the 24.3 behavior. Both are ignored when no read-only server is configured and no
	///replication servers are defined.</para></summary>
	public static class ReplicationDelay {
		public const string PREF_READ_DELAY_MS="ForkReplicationReadDelayMs";
		public const string PREF_SIGNAL_LOOKBACK_MS="ForkReplicationSignalLookbackMs";
		///<summary>Largest value either setting accepts, in milliseconds.</summary>
		public const int MAX_DELAY_MS=60000;
		///<summary>No write noted yet.</summary>
		private const long NO_WRITE=long.MinValue;

		///<summary>Milliseconds from a monotonic clock (not the wall clock, which can jump). Tests replace it.</summary>
		public static Func<long> FuncGetNowMs=() => (long)(Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency*1000d);
		///<summary>FuncGetNowMs at the end of this process's last database write, or NO_WRITE.</summary>
		private static long _msLastWrite=NO_WRITE;
		private static readonly SignalLookbackFilter _signalLookbackFilter=new SignalLookbackFilter();

		///<summary>The per-stream filter that keeps looked-back signals from being processed twice.</summary>
		public static SignalLookbackFilter SignalLookback {
			get {
				return _signalLookbackFilter;
			}
		}

		#region Settings
		///<summary>Clamps to 0 to MAX_DELAY_MS.</summary>
		public static int ClampMs(long ms) {
			if(ms<0) {
				return 0;
			}
			if(ms>MAX_DELAY_MS) {
				return MAX_DELAY_MS;
			}
			return (int)ms;
		}

		///<summary>Parses a stored value. Blank or unreadable values are 0; others are clamped.</summary>
		public static int ParseMs(string valueString) {
			if(!long.TryParse((valueString??"").Trim(),out long ms)) {
				return 0;
			}
			return ClampMs(ms);
		}

		///<summary>The stored read delay, as set, whether or not it is in effect. Never throws.</summary>
		public static int GetReadDelayMs() {
			return ParseMs(PrefC.GetRawOrDefault(PREF_READ_DELAY_MS,"0"));
		}

		///<summary>The stored write delay for signals, as set, whether or not it is in effect. Never throws.</summary>
		public static int GetSignalLookbackMs() {
			return ParseMs(PrefC.GetRawOrDefault(PREF_SIGNAL_LOOKBACK_MS,"0"));
		}

		///<summary>Stores both settings (clamped), inserting a row the first time its value differs from the current one (a missing row is 0, so
		///saving 0 on a database without the rows writes nothing). Returns true if either changed. The caller signals InvalidType.Prefs so other
		///workstations reload.</summary>
		public static bool SaveSettings(int readDelayMs,int signalLookbackMs) {
			Meth.NoCheckMiddleTierRole();
			bool isChanged=false;
			if(ClampMs(readDelayMs)!=GetReadDelayMs()) {
				isChanged|=PrefC.UpsertRaw(PREF_READ_DELAY_MS,POut.Int(ClampMs(readDelayMs)));
			}
			if(ClampMs(signalLookbackMs)!=GetSignalLookbackMs()) {
				isChanged|=PrefC.UpsertRaw(PREF_SIGNAL_LOOKBACK_MS,POut.Int(ClampMs(signalLookbackMs)));
			}
			return isChanged;
		}

		///<summary>True when Preferences, Server Connections has a read-only server (direct or Middle Tier). Never throws.</summary>
		public static bool IsReadOnlyServerConfigured() {
			return PrefC.GetRawOrDefault(nameof(PrefName.ReadOnlyServerCompName),"").Trim()!=""
				|| PrefC.GetRawOrDefault(nameof(PrefName.ReadOnlyServerURI),"").Trim()!="";
		}

		///<summary>True when the Replication Setup window lists at least one server. Never throws.</summary>
		public static bool HasReplicationServers() {
			try {
				return ReplicationServers.GetCount()>0;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}
		#endregion Settings

		#region Read delay
		///<summary>Called by Db after every write this process sends to the database. Only stores a timestamp, so it costs nothing.</summary>
		public static void NoteWrite() {
			Interlocked.Exchange(ref _msLastWrite,FuncGetNowMs());
		}

		///<summary>Forgets the last write, as if this process had never written. For tests.</summary>
		public static void ClearLastWrite() {
			Interlocked.Exchange(ref _msLastWrite,NO_WRITE);
		}

		///<summary>True when a write noted at msLastWrite is less than delayMs old at msNow.</summary>
		public static bool IsWithinDelay(long msLastWrite,long msNow,int delayMs) {
			if(delayMs<=0 || msLastWrite==NO_WRITE) {
				return false;
			}
			long msElapsed=msNow-msLastWrite;
			return msElapsed>=0 && msElapsed<delayMs;
		}

		///<summary>True when a read meant for the read-only server should run on the main server instead: a read-only server is configured, the
		///read delay is set, and this process wrote to the database less than that long ago. Never throws (false on any error), because it guards
		///every read-only server call.</summary>
		public static bool IsReadFromPrimaryRequired() {
			try {
				long msLastWrite=Interlocked.Read(ref _msLastWrite);
				if(msLastWrite==NO_WRITE) {
					return false;
				}
				int delayMs=GetReadDelayMs();
				if(delayMs<=0 || !IsReadOnlyServerConfigured()) {
					return false;
				}
				return IsWithinDelay(msLastWrite,FuncGetNowMs(),delayMs);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}
		#endregion Read delay

		#region Write delay for signals
		///<summary>The look-back in effect for signal checks: the stored value when a read-only server or replication servers are configured,
		///otherwise 0. Never throws.</summary>
		public static int GetEffectiveSignalLookbackMs() {
			try {
				int lookbackMs=GetSignalLookbackMs();
				if(lookbackMs<=0) {
					return 0;
				}
				if(!IsReadOnlyServerConfigured() && !HasReplicationServers()) {
					return 0;
				}
				return lookbackMs;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return 0;
			}
		}
		#endregion Write delay for signals
	}

	///<summary>Look-back for signal checks. Each caller of Signalods.RefreshTimed polls its own stream of signals, told apart by its type filters (the
	///high-priority types, every other type, the appointment types). Per stream this keeps the time of the first check made with a look-back (the
	///floor, so a workstation never processes signals from before it started looking back) and the signals already returned, so looked-back
	///signals are never processed twice. Thread-safe.</summary>
	public class SignalLookbackFilter {
		private readonly object _lock=new object();
		private readonly Dictionary<string,StreamState> _dictStreams=new Dictionary<string,StreamState>();

		private class StreamState {
			public DateTime DateTFloor;
			///<summary>SignalNum to SigDateTime of the signals already returned by a look-back check.</summary>
			public Dictionary<long,DateTime> DictSeen=new Dictionary<long,DateTime>();
		}

		///<summary>The key of the stream a RefreshTimed call belongs to, from its type filters.</summary>
		public static string GetStreamKey(List<InvalidType> listInvalidTypes,List<InvalidType> listInvalidTypesExclude) {
			string include=listInvalidTypes==null ? "" : string.Join(",",listInvalidTypes.Select(x => (int)x).OrderBy(x => x));
			string exclude=listInvalidTypesExclude==null ? "" : string.Join(",",listInvalidTypesExclude.Select(x => (int)x).OrderBy(x => x));
			return "in:"+include+"|out:"+exclude;
		}

		///<summary>The time to check for signals after. With no look-back this is dateTSince, and the stream's state is dropped. Otherwise it is
		///dateTSince less the look-back, cut to whole seconds (signalod.SigDateTime has no fractions), but never earlier than the stream's floor.</summary>
		public DateTime GetQuerySince(string streamKey,DateTime dateTSince,int lookbackMs) {
			lock(_lock) {
				if(lookbackMs<=0) {
					_dictStreams.Remove(streamKey);
					return dateTSince;
				}
				if(!_dictStreams.TryGetValue(streamKey,out StreamState streamState)) {
					streamState=new StreamState();
					streamState.DateTFloor=dateTSince;
					_dictStreams[streamKey]=streamState;
				}
				if(dateTSince<streamState.DateTFloor) {
					streamState.DateTFloor=dateTSince;//The caller moved back on purpose; follow it.
				}
				DateTime dateTQuery=dateTSince.AddMilliseconds(-lookbackMs);
				dateTQuery=new DateTime(dateTQuery.Ticks-(dateTQuery.Ticks%TimeSpan.TicksPerSecond),dateTQuery.Kind);
				if(dateTQuery<streamState.DateTFloor) {
					dateTQuery=streamState.DateTFloor;
				}
				return dateTQuery;
			}
		}

		///<summary>Removes the signals this stream already returned and remembers the rest. Forgets signals at or before dateTQuery, which a check
		///after dateTQuery can never return again. With no look-back the list is returned as is.</summary>
		public List<Signalod> Filter(string streamKey,List<Signalod> listSignalods,DateTime dateTQuery,int lookbackMs) {
			if(lookbackMs<=0 || listSignalods==null) {
				return listSignalods;
			}
			lock(_lock) {
				if(!_dictStreams.TryGetValue(streamKey,out StreamState streamState)) {
					return listSignalods;
				}
				List<Signalod> listNew=new List<Signalod>();
				for(int i=0;i<listSignalods.Count;i++) {
					if(streamState.DictSeen.ContainsKey(listSignalods[i].SignalNum)) {
						continue;
					}
					streamState.DictSeen[listSignalods[i].SignalNum]=listSignalods[i].SigDateTime;
					listNew.Add(listSignalods[i]);
				}
				List<long> listSignalNumsOld=streamState.DictSeen.Where(x => x.Value<=dateTQuery).Select(x => x.Key).ToList();
				for(int i=0;i<listSignalNumsOld.Count;i++) {
					streamState.DictSeen.Remove(listSignalNumsOld[i]);
				}
				return listNew;
			}
		}

		///<summary>Number of signals the stream remembers. For tests.</summary>
		public int GetCountSeen(string streamKey) {
			lock(_lock) {
				if(!_dictStreams.TryGetValue(streamKey,out StreamState streamState)) {
					return 0;
				}
				return streamState.DictSeen.Count;
			}
		}

		///<summary>Drops every stream. For tests.</summary>
		public void Clear() {
			lock(_lock) {
				_dictStreams.Clear();
			}
		}
	}
}
