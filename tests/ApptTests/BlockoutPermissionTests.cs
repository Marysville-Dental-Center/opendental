// Unit tests for the flagged-blockout permission rules (backlog item v25_1-appt-blockouts-flagged-permission): a blockout type with the NS or DC
// flag needs BlockoutsFlagged (266), others need Blockouts (13), and changing between kinds needs both. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ApptTests {
	[TestClass]
	public class BlockoutPermissionTests {
		[TestMethod]
		public void PermNumbers_MatchOpenDental() {
			Assert.AreEqual(13,(int)EnumPermType.Blockouts);
			Assert.AreEqual(266,(int)EnumPermType.BlockoutsFlagged);
		}

		[TestMethod]
		public void Flagged_FromItemValue() {
			Assert.IsFalse(Schedules.IsBlockoutTypeFlagged((string)null));
			Assert.IsFalse(Schedules.IsBlockoutTypeFlagged(""));
			Assert.IsFalse(Schedules.IsBlockoutTypeFlagged("XY"));
			Assert.IsTrue(Schedules.IsBlockoutTypeFlagged("NS"));
			Assert.IsTrue(Schedules.IsBlockoutTypeFlagged("DC"));
			Assert.IsTrue(Schedules.IsBlockoutTypeFlagged("NS,DC"));
		}

		[TestMethod]
		public void PermType_ForKind() {
			Assert.AreEqual(EnumPermType.BlockoutsFlagged,Schedules.GetBlockoutPermType(true));
			Assert.AreEqual(EnumPermType.Blockouts,Schedules.GetBlockoutPermType(false));
		}

		[TestMethod]
		public void PermType_NonBlockoutSchedulesUseBlockouts() {
			Assert.AreEqual(EnumPermType.Blockouts,Schedules.GetBlockoutPermType((Schedule)null));
			Assert.AreEqual(EnumPermType.Blockouts,Schedules.GetBlockoutPermType(new Schedule { SchedType=ScheduleType.WebSchedASAP,BlockoutType=0 }));
		}

		[TestMethod]
		public void Manage_PermissionMatrix() {
			//isFlagged, hasBlockouts, hasBlockoutsFlagged -> allowed
			Assert.IsFalse(Schedules.CanManageBlockoutType(false,false,false));
			Assert.IsTrue(Schedules.CanManageBlockoutType(false,true,false));
			Assert.IsFalse(Schedules.CanManageBlockoutType(false,false,true));
			Assert.IsTrue(Schedules.CanManageBlockoutType(false,true,true));
			Assert.IsFalse(Schedules.CanManageBlockoutType(true,false,false));
			Assert.IsFalse(Schedules.CanManageBlockoutType(true,true,false));
			Assert.IsTrue(Schedules.CanManageBlockoutType(true,false,true));
			Assert.IsTrue(Schedules.CanManageBlockoutType(true,true,true));
		}

		[TestMethod]
		public void ChangeType_BetweenKindsNeedsBoth() {
			Assert.IsTrue(Schedules.CanChangeBlockoutType(false,false,true,false));
			Assert.IsTrue(Schedules.CanChangeBlockoutType(true,true,false,true));
			Assert.IsFalse(Schedules.CanChangeBlockoutType(false,true,true,false));
			Assert.IsFalse(Schedules.CanChangeBlockoutType(true,false,false,true));
			Assert.IsTrue(Schedules.CanChangeBlockoutType(true,false,true,true));
		}

		private static List<Def> GetDefs() {
			return new List<Def> {
				new Def { DefNum=1,Category=DefCat.BlockoutTypes,ItemName="Lunch",ItemValue="" },
				new Def { DefNum=2,Category=DefCat.BlockoutTypes,ItemName="Meeting",ItemValue="NS" },
				new Def { DefNum=3,Category=DefCat.BlockoutTypes,ItemName="Hold",ItemValue="DC" },
				new Def { DefNum=4,Category=DefCat.BlockoutTypes,ItemName="Closed",ItemValue="NS,DC" },
			};
		}

		private static string Filter(bool hasBlockouts,bool hasBlockoutsFlagged) {
			return string.Join(",",Schedules.FilterBlockoutTypesForUser(GetDefs(),hasBlockouts,hasBlockoutsFlagged).Select(x => x.DefNum));
		}

		[TestMethod]
		public void Filter_TypesOfferedToTheUser() {
			Assert.AreEqual("1",Filter(true,false));
			Assert.AreEqual("2,3,4",Filter(false,true));
			Assert.AreEqual("1,2,3,4",Filter(true,true));
			Assert.AreEqual("",Filter(false,false));
			Assert.AreEqual(0,Schedules.FilterBlockoutTypesForUser(null,true,true).Count);
		}

		[TestMethod]
		public void Included_BothOrNeitherNeedNoLookup() {
			Assert.IsTrue(Schedules.IsBlockoutIncluded(12345,true,true));
			Assert.IsFalse(Schedules.IsBlockoutIncluded(12345,false,false));
		}
	}
}
