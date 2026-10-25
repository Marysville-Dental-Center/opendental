// Unit tests for the two time bar preferences (items v25_2-appts-provider-timebars-op-background and v25_3-appts-timebar-secondary-color):
// the secondary provider of an empty time bar, the background tint, and how overlapping providers share an operatory column. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BAPPTTests {
	[TestClass]
	public class TimeBarRuleTests {
		#region Secondary color when no provider time
		[TestMethod]
		public void HasProvTime_LooksForAnX() {
			Assert.IsFalse(ApptTimeBars.HasProvTime(null));
			Assert.IsFalse(ApptTimeBars.HasProvTime(""));
			Assert.IsFalse(ApptTimeBars.HasProvTime("//////"));
			Assert.IsTrue(ApptTimeBars.HasProvTime("//XX//"));
			Assert.IsTrue(ApptTimeBars.HasProvTime("x"));
		}

		[TestMethod]
		public void EmptyBar_NotHygiene_UsesTheHygienist() {
			Assert.AreEqual(7,ApptTimeBars.GetSecondaryProvNumForEmptyTimeBar("//////","//////",provNum:3,provHyg:7,isHygiene:false));
		}

		[TestMethod]
		public void EmptyBar_Hygiene_UsesTheDentist() {
			Assert.AreEqual(3,ApptTimeBars.GetSecondaryProvNumForEmptyTimeBar("//////","",provNum:3,provHyg:7,isHygiene:true));
		}

		[TestMethod]
		public void AnyProvTime_LeavesTheBarAsBefore() {
			Assert.AreEqual(0,ApptTimeBars.GetSecondaryProvNumForEmptyTimeBar("/XX///","//////",3,7,false),"X in the main pattern.");
			Assert.AreEqual(0,ApptTimeBars.GetSecondaryProvNumForEmptyTimeBar("//////","///XX/",3,7,false),"X only in PatternSecondary.");
			Assert.AreEqual(0,ApptTimeBars.GetSecondaryProvNumForEmptyTimeBar("XXXX","XX",3,7,true));
		}

		[TestMethod]
		public void NoSecondaryProvider_LeavesTheBarAsBefore() {
			Assert.AreEqual(0,ApptTimeBars.GetSecondaryProvNumForEmptyTimeBar("//////","//////",provNum:3,provHyg:0,isHygiene:false));
			Assert.AreEqual(0,ApptTimeBars.GetSecondaryProvNumForEmptyTimeBar("//////",null,provNum:0,provHyg:7,isHygiene:true));
		}
		#endregion Secondary color when no provider time

		#region Tint
		[TestMethod]
		public void Tint_LightensSeventyPercentTowardWhite() {
			Assert.AreEqual(Color.FromArgb(255,179,179,179).ToArgb(),ApptTimeBars.TintTowardWhite(Color.Black).ToArgb());
			Assert.AreEqual(Color.FromArgb(255,209,239,179).ToArgb(),ApptTimeBars.TintTowardWhite(Color.FromArgb(100,200,0)).ToArgb());
			Assert.AreEqual(Color.White.ToArgb(),ApptTimeBars.TintTowardWhite(Color.White).ToArgb());
		}

		[TestMethod]
		public void Tint_FractionIsClampedAndOpaque() {
			Color color=Color.FromArgb(40,10,20,30);
			Assert.AreEqual(Color.FromArgb(255,10,20,30).ToArgb(),ApptTimeBars.TintTowardWhite(color,0).ToArgb());
			Assert.AreEqual(Color.White.ToArgb(),ApptTimeBars.TintTowardWhite(color,1).ToArgb());
			Assert.AreEqual(Color.White.ToArgb(),ApptTimeBars.TintTowardWhite(color,5).ToArgb());
			Assert.AreEqual(Color.FromArgb(255,10,20,30).ToArgb(),ApptTimeBars.TintTowardWhite(color,-1).ToArgb());
		}
		#endregion Tint

		#region Operatory background segments
		private static Schedule Prov(long provNum,double hourStart,double hourStop,ScheduleType schedType=ScheduleType.Provider) {
			return new Schedule { ProvNum=provNum,SchedType=schedType,StartTime=TimeSpan.FromHours(hourStart),StopTime=TimeSpan.FromHours(hourStop) };
		}

		private static string Describe(List<Schedule> listSchedules) {
			return string.Join("; ",ApptTimeBars.GetOpBackgroundSegments(listSchedules).Select(x => x.ToString()));
		}

		[TestMethod]
		public void Segments_OneProviderFillsTheColumn() {
			Assert.AreEqual("5 08:00-12:00 1/1",Describe(new List<Schedule> { Prov(5,8,12) }));
		}

		[TestMethod]
		public void Segments_SeparateTimesDoNotSplit() {
			Assert.AreEqual("5 08:00-12:00 1/1; 6 13:00-17:00 1/1",Describe(new List<Schedule> { Prov(6,13,17),Prov(5,8,12) }));
		}

		[TestMethod]
		public void Segments_OverlapSplitsTheWidth() {
			Assert.AreEqual("5 08:00-10:00 1/1; 5 10:00-12:00 1/2; 6 10:00-12:00 2/2; 6 12:00-14:00 1/1",
				Describe(new List<Schedule> { Prov(5,8,12),Prov(6,10,14) }));
		}

		[TestMethod]
		public void Segments_ProvidersOrderedByFirstStartThenProvNum() {
			Assert.AreEqual("3 09:00-10:00 1/2; 9 09:00-10:00 2/2",Describe(new List<Schedule> { Prov(9,9,10),Prov(3,9,10) }));
			Assert.AreEqual("9 08:00-09:00 1/1; 9 09:00-10:00 1/2; 3 09:00-10:00 2/2",Describe(new List<Schedule> { Prov(3,9,10),Prov(9,8,10) }));
		}

		[TestMethod]
		public void Segments_AdjacentPiecesMerge_AndTheSameProviderNeverSplits() {
			Assert.AreEqual("5 08:00-12:00 1/1",Describe(new List<Schedule> { Prov(5,8,10),Prov(5,10,12) }));
			Assert.AreEqual("5 08:00-12:00 1/1",Describe(new List<Schedule> { Prov(5,8,12),Prov(5,9,11) }));
		}

		[TestMethod]
		public void Segments_IgnoreRowsThatAreNotProviderTime() {
			List<Schedule> listSchedules=new List<Schedule> {
				null,
				Prov(0,8,9),//no provider
				Prov(5,12,12),//empty
				Prov(5,13,12),//backwards
				Prov(5,8,9,ScheduleType.Blockout),
				Prov(5,8,9,ScheduleType.Practice),
				Prov(5,24,25),//starts at midnight or later, skipped as the day view skips it
			};
			Assert.AreEqual("",Describe(listSchedules));
			Assert.AreEqual(0,ApptTimeBars.GetOpBackgroundSegments(null).Count);
		}
		#endregion Operatory background segments

		#region Preference names
		[TestMethod]
		public void PrefNames_AreForkOwnedAndNotGuessedOpenDentalNames() {
			//Open Dental's own names are not public, so nothing is registered in PrefName (tests/RegistryTests/PrefNameTests.cs).
			foreach(string prefName in new[] { ApptTimeBars.PREF_PROV_TIME_ON_OP_BACKGROUND,ApptTimeBars.PREF_SECONDARY_COLOR_NO_PROV_TIME }) {
				StringAssert.StartsWith(prefName,"Fork");
				Assert.IsFalse(Enum.IsDefined(typeof(PrefName),prefName),prefName);
			}
			Assert.AreEqual("ApptProvTimeBarsOnOpBackground",ApptTimeBars.PREF_NAME_PROPOSED_OP_BACKGROUND);
			Assert.AreEqual("ApptTimeBarSecondaryColorNoProvTime",ApptTimeBars.PREF_NAME_PROPOSED_SECONDARY_COLOR);
			Assert.IsFalse(Enum.IsDefined(typeof(PrefName),ApptTimeBars.PREF_NAME_PROPOSED_OP_BACKGROUND));
			Assert.IsFalse(Enum.IsDefined(typeof(PrefName),ApptTimeBars.PREF_NAME_PROPOSED_SECONDARY_COLOR));
		}

		[TestMethod]
		public void PrefNameInUse_IsTheForkNameUntilTheOpenDentalNameIsRegistered() {
			//When the registry owner adds the confirmed name to PrefName, GetPrefNameInUse switches to it; until then the fork row is used.
			Assert.AreEqual(ApptTimeBars.PREF_PROV_TIME_ON_OP_BACKGROUND,ApptTimeBars.GetPrefNameInUse(ApptTimeBars.PREF_PROV_TIME_ON_OP_BACKGROUND));
			Assert.AreEqual(ApptTimeBars.PREF_SECONDARY_COLOR_NO_PROV_TIME,ApptTimeBars.GetPrefNameInUse(ApptTimeBars.PREF_SECONDARY_COLOR_NO_PROV_TIME));
			Assert.AreEqual("SomethingElse",ApptTimeBars.GetPrefNameInUse("SomethingElse"));
		}
		#endregion Preference names
	}
}
