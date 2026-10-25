// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImagingCapture.Camera {
	///<summary>What one baseline recording showed.</summary>
	public sealed class BaselineResult {
		///<summary>Controls that kept one value in every reading, with that value.</summary>
		public IReadOnlyDictionary<ControlId,int> StableValues { get; }
		///<summary>Controls whose value changed during the baseline. They are never used for a key or a level.</summary>
		public IReadOnlyList<ControlId> Drifting { get; }
		///<summary>Controls that could be read in some readings but not in all. Not used.</summary>
		public IReadOnlyList<ControlId> Unreadable { get; }
		public int SampleCount { get; }
		///<summary>"" when the baseline can be used; otherwise why not (too few readings).</summary>
		public string Problem { get; }

		internal BaselineResult(Dictionary<ControlId,int> dictStableValues,List<ControlId> listDrifting,List<ControlId> listUnreadable,int sampleCount,string problem) {
			StableValues=dictStableValues;
			Drifting=listDrifting;
			Unreadable=listUnreadable;
			SampleCount=sampleCount;
			Problem=problem??"";
		}

		public bool IsUsable {
			get {
				return Problem=="";
			}
		}
	}

	///<summary>Where a level candidate came from.</summary>
	public enum LevelCandidateSource {
		///<summary>Bits that differed between Baseline A (held) and Baseline B (resting).</summary>
		BaselineB=0,
		///<summary>Bits that changed during the level step.</summary>
		LevelStep=1,
	}

	///<summary>A level input Learn offers. Binding's active level is the resting (put down) state, so PausePreview pauses the preview
	///while the handpiece rests.</summary>
	public sealed class LevelCandidate {
		public ControlBinding Binding { get; }
		public LevelCandidateSource Source { get; }

		public LevelCandidate(ControlBinding binding,LevelCandidateSource source) {
			Binding=binding??throw new ArgumentNullException(nameof(binding));
			Source=source;
		}

		public override string ToString() {
			return Binding.ToString();
		}
	}

	///<summary>One press seen during key capture.</summary>
	public sealed class KeyPress {
		///<summary>The first reading that showed the change.</summary>
		public long StartMs { get; }
		///<summary>The first reading back at the baseline.</summary>
		public long EndMs { get; }
		///<summary>The baseline bits that changed at any time during the press.</summary>
		public int ChangedBits { get; }

		public KeyPress(long startMs,long endMs,int changedBits) {
			StartMs=startMs;
			EndMs=endMs;
			ChangedBits=changedBits;
		}

		///<summary>EndMs-StartMs. At most one poll interval longer than the press itself, never shorter.</summary>
		public int DurationMs {
			get {
				return (int)Math.Min(int.MaxValue,Math.Max(0,EndMs-StartMs));
			}
		}
	}

	///<summary>A control that changed with every press and passed the mask rule.</summary>
	public sealed class KeyCandidate {
		///<summary>The trigger binding: the active level is the pressed state.</summary>
		public ControlBinding Binding { get; }
		public IReadOnlyList<KeyPress> Presses { get; }

		public KeyCandidate(ControlBinding binding,IReadOnlyList<KeyPress> listPresses) {
			Binding=binding??throw new ArgumentNullException(nameof(binding));
			Presses=listPresses??new List<KeyPress>();
		}

		public int LongestPressMs {
			get {
				return Presses.Count==0 ? 0 : Presses.Max(x => x.DurationMs);
			}
		}

		public override string ToString() {
			return Binding.ToString();
		}
	}

	///<summary>What the key step found.</summary>
	public sealed class KeyLearnResult {
		private KeyCandidate _keyCandidateChosen;

		///<summary>Every control that changed with every press and passed the mask rule. Single-bit masks first, then in ControlId
		///order.</summary>
		public IReadOnlyList<KeyCandidate> Candidates { get; }
		///<summary>Still-pin samples during the key step.</summary>
		public int StillSampleCount { get; }
		///<summary>True when the still pin sent exactly one sample per press: [Trigger.Still] is turned on.</summary>
		public bool IsStillPinKey { get; }
		///<summary>Why controls were not taken, and anything the user should check. For the Learn window.</summary>
		public IReadOnlyList<string> Notes { get; }

		internal KeyLearnResult(List<KeyCandidate> listCandidates,int stillSampleCount,bool isStillPinKey,List<string> listNotes) {
			Candidates=listCandidates;
			StillSampleCount=stillSampleCount;
			IsStillPinKey=isStillPinKey;
			Notes=listNotes;
			_keyCandidateChosen=listCandidates.FirstOrDefault();
		}

		///<summary>The control trigger to use: the first candidate, or the one picked with LearnAnalyzer.ChooseKey. Null when no
		///control changed with every press.</summary>
		public KeyCandidate Chosen {
			get {
				return _keyCandidateChosen;
			}
			internal set {
				_keyCandidateChosen=value;
			}
		}

		///<summary>True when a control trigger or the still pin was found.</summary>
		public bool IsKeyFound {
			get {
				return Chosen!=null || IsStillPinKey;
			}
		}

		///<summary>The learned DebounceMs: twice the longest press of the chosen control, at least 200 ms (design 8.4). Null without a
		///control trigger, since only a control shows how long a press lasts; the 300 ms fallback then applies.</summary>
		public int? DebounceMs {
			get {
				if(Chosen==null) {
					return null;
				}
				return CameraConfig.GetLearnedDebounceMs(Chosen.LongestPressMs);
			}
		}
	}

	///<summary>What the level step found.</summary>
	public sealed class LevelLearnResult {
		///<summary>Single-bit masks first, then those Baseline B also offered, then in ControlId order.</summary>
		public IReadOnlyList<LevelCandidate> Candidates { get; }
		public IReadOnlyList<string> Notes { get; }

		internal LevelLearnResult(List<LevelCandidate> listCandidates,List<string> listNotes) {
			Candidates=listCandidates;
			Notes=listNotes;
		}

		///<summary>The first candidate, or null.</summary>
		public LevelCandidate Chosen {
			get {
				return Candidates.FirstOrDefault();
			}
		}
	}

	///<summary>Live progress of the key step, for "Presses seen: 2".</summary>
	public struct KeyCaptureProgress {
		///<summary>The most completed presses (change and return) seen on any one usable control.</summary>
		public int PressesSeen { get; }
		///<summary>True while a usable control is away from its baseline (a key is held).</summary>
		public bool IsKeyDown { get; }

		public KeyCaptureProgress(int pressesSeen,bool isKeyDown) {
			PressesSeen=pressesSeen;
			IsKeyDown=isKeyDown;
		}
	}

	///<summary>The analysis behind the Learn tool (design 8.5). Pure: it works on recorded ControlSamples and still-sample times and
	///never touches a camera, so the window (which records with ControlSampler at PollMs) and the tests share it. It finds
	///whatever standard control changes when the user presses a key, on any camera, and nothing on a camera that has none. It
	///knows nothing about any particular camera.
	///
	///Steps, in order:
	///1. Baseline A (SetBaselineA): BaselineMs held in the hand, no keys. Every readable control is recorded; a control whose
	///   value changes (drifts) is excluded from everything after.
	///2. Baseline B (SetBaselineB, optional; or SkipBaselineB): BaselineMs in the resting position. Bits that differ between A and
	///   B are excluded from key masks and offered as level inputs: a value that changes when the handpiece is put down or picked
	///   up, without a key press, must not be taken as a key. A control that drifts or cannot be read in B is excluded too.
	///3. Key capture (SetKeyCapture): the user presses and releases the key PressCount times. For each usable control, a press is a
	///   run of readings whose bits (outside the level bits) differ from Baseline A. A control is a candidate when it shows exactly
	///   PressCount completed presses and the mask rule holds: the same set of bits changed on every press (usually one bit), or,
	///   when the sets differ, exactly one bit changed on every press. The active level follows from the observed change: High
	///   when the mask bits were 0 at the baseline and became 1, Low for the reverse; bits that move in opposite directions are
	///   rejected. DebounceMs is twice the longest press, at least 200 ms. The still pin counts as a key when it sent exactly one
	///   sample per press.
	///4. Level step (SetLevelCapture, optional): the user puts the handpiece down and picks it up. A control whose bits (outside the
	///   key mask) changed by the same rule is offered as a level input, active in the changed (put down) state.
	///BuildConfig then gives the CameraConfig update. Learn is the only source of trigger and level values.</summary>
	public sealed class LearnAnalyzer {
		#region Constants
		///<summary>The length of each baseline recording (design 8.5). The window records for this long.</summary>
		public const int BaselineMs=3000;
		///<summary>Presses asked for in the key step (design 8.5).</summary>
		public const int PressCount=3;
		#endregion Constants

		#region Fields
		private Dictionary<ControlId,int> _dictLevelBits=new Dictionary<ControlId,int>();
		private List<LevelCandidate> _listBaselineLevelCandidates=new List<LevelCandidate>();
		private List<ControlId> _listExcludedByBaselineB=new List<ControlId>();
		#endregion Fields

		#region Properties
		///<summary>Null until SetBaselineA.</summary>
		public BaselineResult BaselineA { get; private set; }
		///<summary>Null when skipped, not recorded yet, or unusable.</summary>
		public BaselineResult BaselineB { get; private set; }
		///<summary>Null until SetKeyCapture.</summary>
		public KeyLearnResult KeyResult { get; private set; }
		///<summary>Null until SetLevelCapture.</summary>
		public LevelLearnResult LevelResult { get; private set; }

		///<summary>Per control, the bits that differed between Baseline A and Baseline B. Empty without Baseline B.</summary>
		public IReadOnlyDictionary<ControlId,int> LevelBits {
			get {
				return _dictLevelBits;
			}
		}

		///<summary>The level inputs Baseline B offers: one per differing bit, active in the resting state.</summary>
		public IReadOnlyList<LevelCandidate> BaselineLevelCandidates {
			get {
				return _listBaselineLevelCandidates;
			}
		}
		#endregion Properties

		#region Baselines
		///<summary>Analyzes Baseline A and forgets every later step.</summary>
		public BaselineResult SetBaselineA(IEnumerable<ControlSample> samples) {
			BaselineA=AnalyzeBaseline(samples);
			ClearBaselineB();
			KeyResult=null;
			LevelResult=null;
			return BaselineA;
		}

		///<summary>Analyzes Baseline B against Baseline A and forgets the key and level steps. An unusable recording is not kept: the
		///result's Problem says why, and the user records again or skips. Throws InvalidOperationException without a usable
		///Baseline A.</summary>
		public BaselineResult SetBaselineB(IEnumerable<ControlSample> samples) {
			RequireBaselineA();
			BaselineResult baselineResult=AnalyzeBaseline(samples);
			ClearBaselineB();
			KeyResult=null;
			LevelResult=null;
			if(!baselineResult.IsUsable) {
				return baselineResult;
			}
			BaselineB=baselineResult;
			foreach(ControlId control in BaselineA.StableValues.Keys.OrderBy(x => x.SortKey)) {
				int valueB;
				if(!baselineResult.StableValues.TryGetValue(control,out valueB)) {
					_listExcludedByBaselineB.Add(control);
					continue;
				}
				int valueA=BaselineA.StableValues[control];
				int bitsDiffering=valueA^valueB;
				if(bitsDiffering==0) {
					continue;
				}
				_dictLevelBits[control]=bitsDiffering;
				for(int bit=0;bit<32;bit++) {
					int mask=1<<bit;
					if((bitsDiffering & mask)==0) {
						continue;
					}
					ControlLevel activeLevel=(valueB & mask)!=0 ? ControlLevel.High : ControlLevel.Low;
					_listBaselineLevelCandidates.Add(new LevelCandidate(new ControlBinding(control,mask,activeLevel),LevelCandidateSource.BaselineB));
				}
			}
			return baselineResult;
		}

		///<summary>Goes on without Baseline B and forgets the key and level steps.</summary>
		public void SkipBaselineB() {
			ClearBaselineB();
			KeyResult=null;
			LevelResult=null;
		}

		private void ClearBaselineB() {
			BaselineB=null;
			_dictLevelBits=new Dictionary<ControlId,int>();
			_listBaselineLevelCandidates=new List<LevelCandidate>();
			_listExcludedByBaselineB=new List<ControlId>();
		}

		///<summary>The controls Learn can use: steady in Baseline A and, when Baseline B was recorded, steady in it too. ControlId
		///order.</summary>
		public List<ControlId> GetUsableControls() {
			if(BaselineA==null) {
				return new List<ControlId>();
			}
			return BaselineA.StableValues.Keys.Where(x => !_listExcludedByBaselineB.Contains(x)).OrderBy(x => x.SortKey).ToList();
		}

		///<summary>Sorts the readings by time and classifies every control seen as stable, drifting or unreadable.</summary>
		internal static BaselineResult AnalyzeBaseline(IEnumerable<ControlSample> samples) {
			List<ControlSample> listSamples=SortSamples(samples);
			Dictionary<ControlId,int> dictStableValues=new Dictionary<ControlId,int>();
			List<ControlId> listDrifting=new List<ControlId>();
			List<ControlId> listUnreadable=new List<ControlId>();
			if(listSamples.Count<2) {
				return new BaselineResult(dictStableValues,listDrifting,listUnreadable,listSamples.Count,
					"The baseline needs at least two readings, and "+listSamples.Count+" were taken. Record it again.");
			}
			List<ControlId> listControls=listSamples.SelectMany(x => x.Values.Keys).Distinct().OrderBy(x => x.SortKey).ToList();
			foreach(ControlId control in listControls) {
				List<int> listValues=new List<int>();
				foreach(ControlSample sample in listSamples) {
					int value;
					if(sample.TryGetValue(control,out value)) {
						listValues.Add(value);
					}
				}
				if(listValues.Count<listSamples.Count) {
					listUnreadable.Add(control);
				}
				else if(listValues.Distinct().Count()>1) {
					listDrifting.Add(control);
				}
				else {
					dictStableValues[control]=listValues[0];
				}
			}
			return new BaselineResult(dictStableValues,listDrifting,listUnreadable,listSamples.Count,"");
		}
		#endregion Baselines

		#region Key capture
		///<summary>Live progress of a key recording. Needs Baseline A; gives zeros without it.</summary>
		public KeyCaptureProgress GetKeyCaptureProgress(IEnumerable<ControlSample> samples) {
			if(BaselineA==null || !BaselineA.IsUsable) {
				return new KeyCaptureProgress(0,false);
			}
			List<ControlSample> listSamples=SortSamples(samples);
			//Per control: completed presses, and whether it is away from its baseline now.
			List<Tuple<int,bool>> listCounts=new List<Tuple<int,bool>>();
			foreach(ControlId control in GetUsableControls()) {
				int bitsAllowed=GetKeyBitsAllowed(control);
				if(bitsAllowed==0) {
					continue;
				}
				List<Episode> listEpisodes=FindEpisodes(control,BaselineA.StableValues[control],bitsAllowed,listSamples);
				listCounts.Add(Tuple.Create(listEpisodes.Count(x => x.IsComplete),listEpisodes.Any(x => !x.IsComplete)));
			}
			int pressesSeen=listCounts.Count==0 ? 0 : listCounts.Max(x => x.Item1);
			//Only the controls with the most presses count for "key down", so a control that changed once and stayed changed (one
			//that settles late) does not keep the step waiting for a release.
			bool isKeyDown=listCounts.Any(x => x.Item1==pressesSeen && x.Item2);
			return new KeyCaptureProgress(pressesSeen,isKeyDown);
		}

		///<summary>Analyzes the key recording and the still-sample times taken during it, and forgets the level step. Throws
		///InvalidOperationException without a usable Baseline A.</summary>
		public KeyLearnResult SetKeyCapture(IEnumerable<ControlSample> samples,IEnumerable<long> stillSampleTimesMs) {
			RequireBaselineA();
			LevelResult=null;
			List<ControlSample> listSamples=SortSamples(samples);
			List<KeyCandidate> listCandidates=new List<KeyCandidate>();
			List<string> listNotes=new List<string>();
			foreach(ControlId control in GetUsableControls()) {
				int baseline=BaselineA.StableValues[control];
				int bitsAllowed=GetKeyBitsAllowed(control);
				if(bitsAllowed==0) {
					continue;
				}
				List<Episode> listEpisodes=FindEpisodes(control,baseline,bitsAllowed,listSamples);
				if(listEpisodes.Count==0) {
					continue;
				}
				if(listEpisodes.Any(x => !x.IsComplete)) {
					listNotes.Add(control+" had not returned to its resting value when the recording ended. Release the key before stopping.");
					continue;
				}
				if(listEpisodes.Count!=PressCount) {
					listNotes.Add(control+" changed "+listEpisodes.Count+" times, and "+PressCount+" presses were asked for. It is not used.");
					continue;
				}
				ControlBinding binding;
				string reason;
				if(!TryGetBinding(control,baseline,listEpisodes,out binding,out reason)) {
					listNotes.Add(control+" changed with every press, but "+reason+" It is not used.");
					continue;
				}
				List<KeyPress> listPresses=listEpisodes.Select(x => new KeyPress(x.StartMs,x.EndMs,x.BitsUnion)).ToList();
				listCandidates.Add(new KeyCandidate(binding,listPresses));
			}
			listCandidates=listCandidates.OrderBy(x => x.Binding.IsSingleBit ? 0 : 1).ThenBy(x => x.Binding.Control.SortKey).ToList();
			if(listCandidates.Count>1) {
				listNotes.Add("Several controls changed with every press ("+string.Join("; ",listCandidates.Select(x => x.Binding.Control.ToString()))+"). "
					+listCandidates[0].Binding.Control+" is used. Check it on the test page.");
			}
			foreach(KeyValuePair<ControlId,int> keyValuePair in _dictLevelBits.OrderBy(x => x.Key.SortKey)) {
				if(!BaselineA.StableValues.ContainsKey(keyValuePair.Key)) {
					continue;
				}
				if(FindEpisodes(keyValuePair.Key,BaselineA.StableValues[keyValuePair.Key],keyValuePair.Value,listSamples).Count>0) {
					listNotes.Add("The bits of "+keyValuePair.Key+" that change between held and resting changed during the key step. They are never used as a key.");
				}
			}
			int stillSampleCount=(stillSampleTimesMs??Enumerable.Empty<long>()).Count();
			bool isStillPinKey=stillSampleCount==PressCount;
			if(stillSampleCount>0 && !isStillPinKey) {
				listNotes.Add("The still pin sent "+stillSampleCount+" samples for "+PressCount+" presses, so it is not used as a trigger.");
			}
			if(listCandidates.Count==0 && !isStillPinKey) {
				listNotes.Add("No key was found: no usable control changed with every press, and the still pin did not respond once per press. "
					+"On this camera, use the on-screen Capture button or Space.");
			}
			KeyResult=new KeyLearnResult(listCandidates,stillSampleCount,isStillPinKey,listNotes);
			return KeyResult;
		}

		///<summary>Uses another of KeyResult's candidates as the control trigger. False when it is not one of them.</summary>
		public bool ChooseKey(KeyCandidate keyCandidate) {
			if(KeyResult==null || keyCandidate==null || !KeyResult.Candidates.Contains(keyCandidate)) {
				return false;
			}
			KeyResult.Chosen=keyCandidate;
			return true;
		}

		///<summary>The bits of a control that may form a key: all except the level bits from Baseline B.</summary>
		private int GetKeyBitsAllowed(ControlId control) {
			int bitsLevel;
			if(_dictLevelBits.TryGetValue(control,out bitsLevel)) {
				return ~bitsLevel;
			}
			return ~0;
		}
		#endregion Key capture

		#region Level step
		///<summary>Analyzes the level recording (put the handpiece down, pick it up). Bits of the chosen key's mask are left out.
		///Throws InvalidOperationException without a usable Baseline A.</summary>
		public LevelLearnResult SetLevelCapture(IEnumerable<ControlSample> samples) {
			RequireBaselineA();
			List<ControlSample> listSamples=SortSamples(samples);
			ControlBinding bindingKey=KeyResult?.Chosen?.Binding;
			List<LevelCandidate> listCandidates=new List<LevelCandidate>();
			List<string> listNotes=new List<string>();
			foreach(ControlId control in GetUsableControls()) {
				int baseline=BaselineA.StableValues[control];
				int bitsAllowed=bindingKey!=null && bindingKey.Control==control ? ~bindingKey.Mask : ~0;
				List<Episode> listEpisodes=FindEpisodes(control,baseline,bitsAllowed,listSamples);
				if(listEpisodes.Count==0) {
					continue;
				}
				ControlBinding binding;
				string reason;
				if(!TryGetBinding(control,baseline,listEpisodes,out binding,out reason)) {
					listNotes.Add(control+" changed, but "+reason+" It is not offered.");
					continue;
				}
				listCandidates.Add(new LevelCandidate(binding,LevelCandidateSource.LevelStep));
			}
			listCandidates=listCandidates
				.OrderBy(x => x.Binding.IsSingleBit ? 0 : 1)
				.ThenBy(x => _listBaselineLevelCandidates.Any(y => y.Binding.Control==x.Binding.Control && (y.Binding.Mask & x.Binding.Mask)!=0) ? 0 : 1)
				.ThenBy(x => x.Binding.Control.SortKey)
				.ToList();
			if(listCandidates.Count==0) {
				listNotes.Add("No control changed when the handpiece was put down and picked up, so no level input is offered.");
			}
			LevelResult=new LevelLearnResult(listCandidates,listNotes);
			return LevelResult;
		}
		#endregion Level step

		#region Result
		///<summary>The CameraConfig update: a copy of current with every trigger and level value replaced by what Learn found, since
		///Learn is their only source and earlier values may belong to another camera. [Trigger.Still] is on when the still pin
		///responded once per press. [Trigger.ControlPoll] gets the chosen key, or is turned off and emptied. DebounceMs gets the
		///learned value, or is emptied (300 ms then applies). The levels are replaced by [Level.1] from levelAccepted (one of
		///BaselineLevelCandidates or LevelResult.Candidates the user accepted), or by none. [Camera] and PollMs are kept. Write the
		///result with CameraConfig.Write. Throws InvalidOperationException before the key step.</summary>
		public CameraConfig BuildConfig(CameraConfig current,LevelCandidate levelAccepted) {
			RequireBaselineA();
			if(KeyResult==null) {
				throw new InvalidOperationException("Learn is not finished: the key step comes first.");
			}
			CameraConfig cameraConfig=(current??new CameraConfig()).Clone();
			cameraConfig.StillEnabled=KeyResult.IsStillPinKey;
			cameraConfig.ControlPoll=new ControlInputConfig();
			if(KeyResult.Chosen!=null) {
				cameraConfig.ControlPoll.Enabled=true;
				cameraConfig.ControlPoll.SetBinding(KeyResult.Chosen.Binding);
			}
			cameraConfig.DebounceMs=KeyResult.DebounceMs;
			cameraConfig.Levels=new List<LevelInputConfig>();
			if(levelAccepted!=null) {
				LevelInputConfig levelInputConfig=new LevelInputConfig(1);
				levelInputConfig.Enabled=true;
				levelInputConfig.SetBinding(levelAccepted.Binding);
				levelInputConfig.Action=LevelAction.PausePreview;
				cameraConfig.Levels.Add(levelInputConfig);
			}
			return cameraConfig;
		}
		#endregion Result

		#region Helpers
		private void RequireBaselineA() {
			if(BaselineA==null || !BaselineA.IsUsable) {
				throw new InvalidOperationException("Record Baseline A first.");
			}
		}

		private static List<ControlSample> SortSamples(IEnumerable<ControlSample> samples) {
			return (samples??Enumerable.Empty<ControlSample>()).Where(x => x!=null).OrderBy(x => x.TimeMs).ToList();
		}

		///<summary>A run of readings in which some allowed bit differs from the baseline.</summary>
		private sealed class Episode {
			public long StartMs;
			///<summary>The first reading back at the baseline, or -1 when the recording ended first.</summary>
			public long EndMs=-1;
			///<summary>Every allowed bit that differed in any reading of the run.</summary>
			public int BitsUnion;
			///<summary>The differing bits of each reading in the run.</summary>
			public readonly HashSet<int> HashSetPatterns=new HashSet<int>();

			public bool IsComplete {
				get {
					return EndMs>=0;
				}
			}
		}

		///<summary>The runs of readings in which (value ^ baseline) &amp; bitsAllowed is not 0. Readings without this control are
		///skipped.</summary>
		private static List<Episode> FindEpisodes(ControlId control,int baseline,int bitsAllowed,List<ControlSample> listSamples) {
			List<Episode> listEpisodes=new List<Episode>();
			Episode episode=null;
			foreach(ControlSample sample in listSamples) {
				int value;
				if(!sample.TryGetValue(control,out value)) {
					continue;
				}
				int bitsChanged=(value^baseline) & bitsAllowed;
				if(bitsChanged!=0) {
					if(episode==null) {
						episode=new Episode();
						episode.StartMs=sample.TimeMs;
						listEpisodes.Add(episode);
					}
					episode.BitsUnion|=bitsChanged;
					episode.HashSetPatterns.Add(bitsChanged);
				}
				else if(episode!=null) {
					episode.EndMs=sample.TimeMs;
					episode=null;
				}
			}
			return listEpisodes;
		}

		///<summary>The mask rule and the active level (design 8.5). The mask is the set of bits that changed, when it was the same on
		///every episode; otherwise the one bit that changed on every episode, when there is exactly one. Every mask bit must have
		///changed together in some reading of each episode. The active level is the changed state: High when the mask bits are 0 at
		///the baseline, Low when they are all 1; a mix is refused.</summary>
		private static bool TryGetBinding(ControlId control,int baseline,List<Episode> listEpisodes,out ControlBinding binding,out string reason) {
			binding=null;
			reason="";
			int mask;
			if(listEpisodes.All(x => x.BitsUnion==listEpisodes[0].BitsUnion)) {
				mask=listEpisodes[0].BitsUnion;
			}
			else {
				int bitsCommon=listEpisodes.Aggregate(~0,(bits,episode) => bits & episode.BitsUnion);
				if(!ControlBinding.IsOneBit(bitsCommon)) {
					reason=bitsCommon==0
						? "no bit changed every time."
						: "different bits changed each time and more than one bit changed every time.";
					return false;
				}
				mask=bitsCommon;
			}
			if(listEpisodes.Any(x => !x.HashSetPatterns.Any(y => (y & mask)==mask))) {
				reason="its bits "+ControlValueFormat.ToHex(mask)+" did not change together.";
				return false;
			}
			int bitsAtBaseline=baseline & mask;
			ControlLevel activeLevel;
			if(bitsAtBaseline==0) {
				activeLevel=ControlLevel.High;
			}
			else if(bitsAtBaseline==mask) {
				activeLevel=ControlLevel.Low;
			}
			else {
				reason="its bits "+ControlValueFormat.ToHex(mask)+" changed in opposite directions.";
				return false;
			}
			binding=new ControlBinding(control,mask,activeLevel);
			return true;
		}
		#endregion Helpers
	}
}
