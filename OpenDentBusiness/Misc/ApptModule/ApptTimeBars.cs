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

namespace OpenDentBusiness {
	///<summary>Rules and settings for the two preferences. The drawing is in ControlApptPanel.TimeBars.cs.
	///<para>Open Dental's own preference names for these two settings are not in any public source, so they are not registered in PrefName (see
	///tests/RegistryTests/PrefNameTests.cs: a guessed name would keep the office's real setting from carrying over). Until the owner confirms
	///the real names, each setting is kept in a fork-owned preference row whose name starts with "Fork". The row is created the first time the
	///setting is turned on; a missing row reads as off, so no migration step adds rows to an Open Dental database. Once the registry owner adds
	///the confirmed name to PrefName (and PREF_NAME_PROPOSED_* is corrected if it differs), the setting is read from and saved to that
	///preference instead, with no other change.</para></summary>
	public static class ApptTimeBars {
		///<summary>Bool, off when missing. When on, each operatory column paints its scheduled provider time in a light tint of the provider's
		///color instead of the generic open color.</summary>
		public const string PREF_PROV_TIME_ON_OP_BACKGROUND="ForkApptProvTimeBarsOnOpBackground";
		///<summary>Bool, off when missing. When on, an appointment whose two time patterns hold no provider time shows the secondary provider's
		///color over its whole time bar.</summary>
		public const string PREF_SECONDARY_COLOR_NO_PROV_TIME="ForkApptTimeBarSecondaryColorNoProvTime";
		///<summary>The name docs/specs/features-25.2.md proposes for Open Dental's 25.2 preference. Not confirmed, so not in PrefName.</summary>
		public const string PREF_NAME_PROPOSED_OP_BACKGROUND="ApptProvTimeBarsOnOpBackground";
		///<summary>The name docs/specs/features-25.3.md proposes for Open Dental's 25.3 preference. Not confirmed, so not in PrefName.</summary>
		public const string PREF_NAME_PROPOSED_SECONDARY_COLOR="ApptTimeBarSecondaryColorNoProvTime";
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

		///<summary>The preference row that holds a setting: the Open Dental name once it is registered in PrefName, otherwise the fork-owned
		///name. Any other name is returned unchanged.</summary>
		public static string GetPrefNameInUse(string prefNameFork) {
			Meth.NoCheckMiddleTierRole();
			string prefNameProposed=null;
			if(prefNameFork==PREF_PROV_TIME_ON_OP_BACKGROUND) {
				prefNameProposed=PREF_NAME_PROPOSED_OP_BACKGROUND;
			}
			else if(prefNameFork==PREF_SECONDARY_COLOR_NO_PROV_TIME) {
				prefNameProposed=PREF_NAME_PROPOSED_SECONDARY_COLOR;
			}
			if(prefNameProposed!=null && Enum.IsDefined(typeof(PrefName),prefNameProposed)) {
				return prefNameProposed;
			}
			return prefNameFork;
		}

		///<summary>Reads one of the two settings (pass the fork-owned name) from the cache. A missing row, an empty cache or an unreadable value
		///reads as false.</summary>
		public static bool GetBoolPref(string prefNameFork) {
			Meth.NoCheckMiddleTierRole();
			string prefName=GetPrefNameInUse(prefNameFork);
			try {
				if(!PrefC.ContainsKey(prefName)) {
					return false;
				}
				return PIn.Bool(PrefC.GetRaw(prefName));
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>Saves one of the two settings (pass the fork-owned name) and updates the local cache. Inserts the row the first time it is
		///turned on; a missing row already reads as off, so turning off a setting that was never saved writes nothing. Returns true if the
		///stored value changed, so the caller sends the Prefs signal.</summary>
		public static bool UpdateBoolPref(string prefNameFork,bool value) {
			Meth.NoCheckMiddleTierRole();
			string prefName=GetPrefNameInUse(prefNameFork);
			if(GetBoolPref(prefNameFork)==value && (PrefC.ContainsKey(prefName) || !value)) {
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
			if(!prefName.In(PREF_PROV_TIME_ON_OP_BACKGROUND,PREF_SECONDARY_COLOR_NO_PROV_TIME,PREF_NAME_PROPOSED_OP_BACKGROUND,PREF_NAME_PROPOSED_SECONDARY_COLOR)) {
				throw new ArgumentException("Not a time bar preference: "+prefName);
			}
			string count=Db.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
			if(PIn.Long(count)==0) {
				Db.NonQ("INSERT INTO preference (PrefName,ValueString,Comments) VALUES ('"+POut.String(prefName)+"','"+POut.String(valueString)+"','')");
				return;
			}
			Db.NonQ("UPDATE preference SET ValueString='"+POut.String(valueString)+"' WHERE PrefName='"+POut.String(prefName)+"'");
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
