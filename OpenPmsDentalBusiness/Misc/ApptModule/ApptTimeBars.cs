// Package B-APPT (docs/specs/port-backlog.md): the two time bar preferences of Preferences, Appts, Appearance.
// - v25_2-appts-provider-timebars-op-background: provider time shown on the operatory background.
// - v25_3-appts-timebar-secondary-color: the secondary provider's color in an appointment's time bar when neither pattern has provider time.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Rules and settings for the two preferences. The drawing is in ControlApptPanel.TimeBars.cs.
	///<para>Each setting is Open Dental's own preference row (names confirmed by the setting names of the office's Open Dental 25.3.48 database,
	///owner question Switch-over 1, 2026-11-07; docs/port-notes/SWMAP.md), read and written by name outside PrefName, so the office's value carries
	///over as it is. The row is created the first time the setting is turned on; a missing row (a database from 24.3, a new database) reads as off.
	///Release v0.0 kept them in the rows ForkApptProvTimeBarsOnOpBackground and ForkApptTimeBarSecondaryColorNoProvTime; the fork migration step
	///OdSettingNamesStep moves those.</para></summary>
	public static class ApptTimeBars {
		///<summary>Bool, off when missing. When on, each operatory column paints its scheduled provider time in a light tint of the provider's
		///color instead of the generic open color. Open Dental 25.2's name.</summary>
		public const string PREF_PROV_TIME_ON_OP_BACKGROUND="ApptProvbarShowOnOps";
		///<summary>Bool, off when missing. When on, an appointment whose two time patterns hold no provider time shows the secondary provider's
		///color over its whole time bar. Open Dental 25.3's name.</summary>
		public const string PREF_SECONDARY_COLOR_NO_PROV_TIME="ApptUseSecProvTimeIfEmpty";
		///<summary>How far a provider color is lightened toward white for the operatory background, so appointment text stays readable.</summary>
		public const double TINT_FRACTION=0.7;

		#region Preferences
		///<summary>True if provider time is shown on the operatory background.</summary>
		public static bool IsProvTimeOnOpBackground() {
			return GetBoolPref(PREF_PROV_TIME_ON_OP_BACKGROUND);
		}

		///<summary>True if the secondary provider's color fills a time bar that has no provider time.</summary>
		public static bool IsSecondaryColorNoProvTime() {
			return GetBoolPref(PREF_SECONDARY_COLOR_NO_PROV_TIME);
		}

		///<summary>Reads one of the two settings (PREF_PROV_TIME_ON_OP_BACKGROUND or PREF_SECONDARY_COLOR_NO_PROV_TIME) from the cache. A missing
		///row, an empty cache or an unreadable value reads as false.</summary>
		public static bool GetBoolPref(string prefName) {
			Meth.NoCheckMiddleTierRole();
			try {
				//B-SERVER's shared helper for fork-owned preference rows: null when the cache is empty or has no such row.
				string valueString=PrefC.GetRawOrDefault(prefName,null);
				if(valueString==null) {
					return false;
				}
				return PIn.Bool(valueString);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>Saves one of the two settings and updates the local cache. Inserts the row the first time it is turned on; a missing row already
		///reads as off, so turning off a setting that was never saved writes nothing. Returns true if the stored value changed, so the caller sends
		///the Prefs signal.</summary>
		public static bool UpdateBoolPref(string prefName,bool value) {
			Meth.NoCheckMiddleTierRole();
			if(GetBoolPref(prefName)==value && (PrefC.ContainsKey(prefName) || !value)) {
				return false;
			}
			UpsertPrefRaw(prefName,POut.Bool(value));
			Pref pref=new Pref();
			pref.PrefName=prefName;
			pref.ValueString=POut.Bool(value);
			Prefs.UpdateValueForKey(pref);
			return true;
		}

		///<summary>Writes a preference row by name, inserting it when it does not exist. Only for the names of the two settings.</summary>
		public static void UpsertPrefRaw(string prefName,string valueString) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),prefName,valueString);
				return;
			}
			if(!prefName.In(PREF_PROV_TIME_ON_OP_BACKGROUND,PREF_SECONDARY_COLOR_NO_PROV_TIME)) {
				throw new ArgumentException("Not a time bar preference: "+prefName);
			}
			PrefC.UpsertRawNoCache(prefName,valueString);//B-SERVER's shared helper for fork-owned preference rows.
		}
		#endregion Preferences

		#region Secondary color when no provider time
		///<summary>True if a time pattern marks any provider time (an X).</summary>
		public static bool HasProvTime(string pattern) {
			return !string.IsNullOrEmpty(pattern) && pattern.IndexOf("X",StringComparison.OrdinalIgnoreCase)>=0;
		}

		///<summary>The provider whose color fills the whole time bar of an appointment, or 0 to draw the bar as before. Only when neither the
		///main pattern nor PatternSecondary has an X. The secondary provider is the hygienist for an appointment that is not flagged hygiene,
		///otherwise the dentist; with no secondary provider the bar stays as before. Length and production are never affected.</summary>
		public static long GetSecondaryProvNumForEmptyTimeBar(string pattern,string patternSecondary,long provNum,long provHyg,bool isHygiene) {
			if(HasProvTime(pattern) || HasProvTime(patternSecondary)) {
				return 0;
			}
			if(isHygiene) {
				return provNum;
			}
			return provHyg;
		}
		#endregion Secondary color when no provider time

		#region Provider time on the operatory background
		///<summary>The color lightened toward white by fraction (0 keeps it, 1 is white). Always opaque.</summary>
		public static Color TintTowardWhite(Color color,double fraction=TINT_FRACTION) {
			double fractionClamped=Math.Max(0,Math.Min(1,fraction));
			return Color.FromArgb(255,TintChannel(color.R,fractionClamped),TintChannel(color.G,fractionClamped),TintChannel(color.B,fractionClamped));
		}

		private static int TintChannel(byte channel,double fraction) {
			//decimal, so a 0.7 fraction of 255 is exactly 178.5 and rounds up, not 178.49999...
			return (int)Math.Round(channel+(255-channel)*(decimal)fraction,MidpointRounding.AwayFromZero);
		}

		///<summary>Splits the provider schedules of one operatory column (one day) into the pieces to tint. Where providers overlap in the same
		///time, each gets an equal share of the column width: Index of Count from the left, in the order the providers first start (ties by
		///ProvNum). Pieces next to each other with the same provider and share are merged. Only provider schedules with a provider, a stop time
		///after the start time and a start before midnight count.</summary>
		public static List<ApptOpBackgroundSegment> GetOpBackgroundSegments(List<Schedule> listSchedules) {
			List<ApptOpBackgroundSegment> listSegments=new List<ApptOpBackgroundSegment>();
			if(listSchedules==null) {
				return listSegments;
			}
			List<Schedule> listSchedulesValid=listSchedules.FindAll(x => x!=null
				&& x.SchedType==ScheduleType.Provider
				&& x.ProvNum!=0
				&& x.StopTime>x.StartTime
				&& x.StartTime<TimeSpan.FromHours(24));
			if(listSchedulesValid.Count==0) {
				return listSegments;
			}
			List<long> listProvNumsOrdered=listSchedulesValid
				.GroupBy(x => x.ProvNum)
				.Select(x => new { ProvNum=x.Key,TimeFirst=x.Min(y => y.StartTime) })
				.OrderBy(x => x.TimeFirst).ThenBy(x => x.ProvNum)
				.Select(x => x.ProvNum).ToList();
			List<TimeSpan> listBoundaries=listSchedulesValid.SelectMany(x => new[] { x.StartTime,x.StopTime }).Distinct().OrderBy(x => x).ToList();
			for(int b=0;b<listBoundaries.Count-1;b++) {
				TimeSpan timeStart=listBoundaries[b];
				TimeSpan timeStop=listBoundaries[b+1];
				List<long> listProvNumsHere=listProvNumsOrdered.FindAll(p => listSchedulesValid.Any(x => x.ProvNum==p && x.StartTime<timeStop && x.StopTime>timeStart));
				for(int i=0;i<listProvNumsHere.Count;i++) {
					ApptOpBackgroundSegment segmentPrev=listSegments.LastOrDefault(x => x.ProvNum==listProvNumsHere[i] && x.TimeStop==timeStart);
					if(segmentPrev!=null && segmentPrev.Index==i && segmentPrev.Count==listProvNumsHere.Count) {
						segmentPrev.TimeStop=timeStop;
						continue;
					}
					ApptOpBackgroundSegment segment=new ApptOpBackgroundSegment();
					segment.ProvNum=listProvNumsHere[i];
					segment.TimeStart=timeStart;
					segment.TimeStop=timeStop;
					segment.Index=i;
					segment.Count=listProvNumsHere.Count;
					listSegments.Add(segment);
				}
			}
			return listSegments;
		}
		#endregion Provider time on the operatory background
	}

	///<summary>One piece of an operatory column to tint in a provider's color: from TimeStart to TimeStop, in share Index (0-based, from the left)
	///of Count equal shares of the column width.</summary>
	public class ApptOpBackgroundSegment {
		public long ProvNum;
		public TimeSpan TimeStart;
		public TimeSpan TimeStop;
		public int Index;
		public int Count;

		public override string ToString() {
			return ProvNum+" "+TimeStart.ToString(@"hh\:mm")+"-"+TimeStop.ToString(@"hh\:mm")+" "+(Index+1)+"/"+Count;
		}
	}
}
