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
	///already processed are filtered out, so nothing is processed twice (unless the server clock goes back, as in 24.3), and its own signals are
	///never returned by the look-back (see SignalLookbackFilter).</para>
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

		///<summary>Called by Signalods.Insert after this process inserted a single signal. Remembers it as this workstation's own, so the look-back
		///never returns it (see SignalLookbackFilter). A Middle Tier server inserts for its clients, which remember their own, so it remembers
		///nothing. Never throws.</summary>
		public static void NoteOwnSignal(long signalNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ServerMT) {
				return;
			}
			_signalLookbackFilter.NoteOwnSignal(signalNum);
		}
		#endregion Write delay for signals
	}

	///<summary>Look-back for signal checks. Each caller of Signalods.RefreshTimed polls its own stream of signals, told apart by its type filters (the
	///high-priority types, every other type, the appointment types). Per stream this keeps the time of the first check made with a look-back (the
	///floor, so a workstation never processes signals from before it started looking back), the signals already returned, and the time through
	///which it has forgotten them (the forgotten mark: a later check never starts before it, so a forgotten signal cannot come back even when the
	///caller's time moves back). So looked-back signals are never processed twice. It also remembers the signals this workstation inserted: they
	///reach the server it reads at once, so they are never late, and the look-back never returns one the 24.3 check would not have returned (the
	///workstation that sends "Shutdown all workstations" skips its own signal, as in 24.3). Thread-safe.</summary>
	public class SignalLookbackFilter {
		///<summary>How long this workstation's own signals are remembered, in milliseconds of the monotonic clock: ten times the longest look-back,
		///so a slow or paused signal timer still recognizes them.</summary>
		public const long OWN_SIGNAL_KEEP_MS=10L*ReplicationDelay.MAX_DELAY_MS;
		///<summary>At most this many own signals are remembered; the oldest are forgotten first.</summary>
		public const int OWN_SIGNAL_MAX_COUNT=10000;
		///<summary>A caller's time more than this many milliseconds before its stream's forgotten mark means the server clock went back or the
		///caller started over, and the stream starts over too (see GetQuerySince). Moves back caused by late signals stay well inside it.</summary>
		public const int RESTART_GAP_MS=ReplicationDelay.MAX_DELAY_MS;
		private readonly object _lock=new object();
		private readonly Dictionary<string,StreamState> _dictStreams=new Dictionary<string,StreamState>();
		private readonly Func<long> _funcGetNowMs;
		///<summary>SignalNums this workstation inserted, and the same in the order noted (monotonic time, SignalNum) for forgetting them.</summary>
		private readonly HashSet<long> _hashSetOwnSignalNums=new HashSet<long>();
		private readonly Queue<KeyValuePair<long,long>> _queueOwnSignals=new Queue<KeyValuePair<long,long>>();

		private class StreamState {
			public DateTime DateTFloor;
			///<summary>The latest time this stream forgot signals through (every signal at or before it was dropped from DictSeen). Only moves
			///forward. MinValue until the first Filter.</summary>
			public DateTime DateTForgotten=DateTime.MinValue;
			///<summary>SignalNum to SigDateTime of the signals already returned by a look-back check.</summary>
			public Dictionary<long,DateTime> DictSeen=new Dictionary<long,DateTime>();
		}

		///<summary>Uses ReplicationDelay.FuncGetNowMs as its clock.</summary>
		public SignalLookbackFilter() : this(() => ReplicationDelay.FuncGetNowMs()) {
		}

		///<summary>funcGetNowMs is the monotonic clock, in milliseconds, used to forget this workstation's own signals. For tests.</summary>
		public SignalLookbackFilter(Func<long> funcGetNowMs) {
			_funcGetNowMs=funcGetNowMs;
		}

		///<summary>The key of the stream a RefreshTimed call belongs to, from its type filters.</summary>
		public static string GetStreamKey(List<InvalidType> listInvalidTypes,List<InvalidType> listInvalidTypesExclude) {
			string include=listInvalidTypes==null ? "" : string.Join(",",listInvalidTypes.Select(x => (int)x).OrderBy(x => x));
			string exclude=listInvalidTypesExclude==null ? "" : string.Join(",",listInvalidTypesExclude.Select(x => (int)x).OrderBy(x => x));
			return "in:"+include+"|out:"+exclude;
		}

		///<summary>The time to check for signals after. With no look-back this is dateTSince, and the stream's state is dropped. Otherwise it is
		///dateTSince less the look-back, cut to whole seconds (signalod.SigDateTime has no fractions), but never earlier than the stream's floor or
		///its forgotten mark.
		///<para>The caller's time can move back: FormOpenDental sets the next regular check's time to the newest signal returned, and a late signal is
		///older than the last check. The forgotten mark keeps such a check from returning a signal a second time, and it loses no late signal: every
		///signal at or before the mark was already outside an earlier check's look-back window.</para>
		///<para>When the caller's time is more than RESTART_GAP_MS before the mark, the server clock went back (daylight saving time ended) or the
		///caller started over. The stream then starts over, as on its first check, so it does not stay blind until the clock catches up. As in 24.3,
		///the checks after a clock change can return signals already processed.</para></summary>
		public DateTime GetQuerySince(string streamKey,DateTime dateTSince,int lookbackMs) {
			lock(_lock) {
				if(lookbackMs<=0) {
					_dictStreams.Remove(streamKey);
					return dateTSince;
				}
				if(_dictStreams.TryGetValue(streamKey,out StreamState streamState) && streamState.DateTForgotten!=DateTime.MinValue
					&& dateTSince<SubtractMs(streamState.DateTForgotten,RESTART_GAP_MS))
				{
					streamState=null;//Start over.
				}
				if(streamState==null) {
					streamState=new StreamState();
					streamState.DateTFloor=dateTSince;
					_dictStreams[streamKey]=streamState;
				}
				if(dateTSince<streamState.DateTFloor) {
					streamState.DateTFloor=dateTSince;//The caller moved back on purpose; follow it.
				}
				DateTime dateTQuery=SubtractMs(dateTSince,lookbackMs);
				dateTQuery=new DateTime(dateTQuery.Ticks-(dateTQuery.Ticks%TimeSpan.TicksPerSecond),dateTQuery.Kind);
				if(dateTQuery<streamState.DateTFloor) {
					dateTQuery=streamState.DateTFloor;
				}
				if(dateTQuery<streamState.DateTForgotten) {
					dateTQuery=streamState.DateTForgotten;//Never reach back into what this stream already forgot.
				}
				return dateTQuery;
			}
		}

		///<summary>dateT less ms milliseconds, or MinValue when that would be before it.</summary>
		private static DateTime SubtractMs(DateTime dateT,long ms) {
			long ticks=ms*TimeSpan.TicksPerMillisecond;
			if(dateT.Ticks<ticks) {
				return DateTime.MinValue;
			}
			return new DateTime(dateT.Ticks-ticks,dateT.Kind);
		}

		///<summary>Removes the signals this stream already returned, and this workstation's own signals that only the look-back returned (at or
		///before dateTSince, the caller's time, so the 24.3 check would not have returned them), and remembers the rest. Then forgets the signals at
		///or before dateTQuery (the time GetQuerySince returned) and moves the stream's forgotten mark there; GetQuerySince never starts a later check
		///before the mark, so a forgotten signal is never returned again. With no look-back the list is returned as is.</summary>
		public List<Signalod> Filter(string streamKey,List<Signalod> listSignalods,DateTime dateTSince,DateTime dateTQuery,int lookbackMs) {
			if(lookbackMs<=0 || listSignalods==null) {
				return listSignalods;
			}
			lock(_lock) {
				if(!_dictStreams.TryGetValue(streamKey,out StreamState streamState)) {
					return listSignalods;
				}
				ForgetOldOwnSignals();
				List<Signalod> listNew=new List<Signalod>();
				for(int i=0;i<listSignalods.Count;i++) {
					if(streamState.DictSeen.ContainsKey(listSignalods[i].SignalNum)) {
						continue;
					}
					if(listSignalods[i].SigDateTime<=dateTSince && _hashSetOwnSignalNums.Contains(listSignalods[i].SignalNum)) {
						continue;//Own signal, never late, returned only by the look-back.
					}
					streamState.DictSeen[listSignalods[i].SignalNum]=listSignalods[i].SigDateTime;
					listNew.Add(listSignalods[i]);
				}
				List<long> listSignalNumsOld=streamState.DictSeen.Where(x => x.Value<=dateTQuery).Select(x => x.Key).ToList();
				for(int i=0;i<listSignalNumsOld.Count;i++) {
					streamState.DictSeen.Remove(listSignalNumsOld[i]);
				}
				if(dateTQuery>streamState.DateTForgotten) {
					streamState.DateTForgotten=dateTQuery;
				}
				return listNew;
			}
		}

		///<summary>Remembers a signal this workstation inserted. Never throws.</summary>
		public void NoteOwnSignal(long signalNum) {
			if(signalNum<=0) {
				return;
			}
			try {
				long msNow=_funcGetNowMs();
				lock(_lock) {
					ForgetOldOwnSignals();
					if(_hashSetOwnSignalNums.Add(signalNum)) {
						_queueOwnSignals.Enqueue(new KeyValuePair<long,long>(msNow,signalNum));
					}
					while(_queueOwnSignals.Count>OWN_SIGNAL_MAX_COUNT) {
						_hashSetOwnSignalNums.Remove(_queueOwnSignals.Dequeue().Value);
					}
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		///<summary>Forgets own signals noted more than OWN_SIGNAL_KEEP_MS ago. Call inside the lock. Never throws.</summary>
		private void ForgetOldOwnSignals() {
			if(_queueOwnSignals.Count==0) {
				return;
			}
			long msNow;
			try {
				msNow=_funcGetNowMs();
			}
			catch(Exception ex) {
				ex.DoNothing();
				return;
			}
			while(_queueOwnSignals.Count>0) {
				if(msNow-_queueOwnSignals.Peek().Key<=OWN_SIGNAL_KEEP_MS) {
					break;
				}
				_hashSetOwnSignalNums.Remove(_queueOwnSignals.Dequeue().Value);
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

		///<summary>Number of own signals remembered. For tests.</summary>
		public int GetCountOwn() {
			lock(_lock) {
				ForgetOldOwnSignals();
				return _hashSetOwnSignalNums.Count;
			}
		}

		///<summary>Drops every stream and every own signal. For tests.</summary>
		public void Clear() {
			lock(_lock) {
				_dictStreams.Clear();
				_hashSetOwnSignalNums.Clear();
				_queueOwnSignals.Clear();
			}
		}
	}
}
