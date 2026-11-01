// Unit tests for the 25.4 blockout permission split (backlog item v25_4-appts-blockout-permissions): which permission covers each blockout
// action, and the full Add/Edit/Delete/Flagged permission matrix. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2APPTTests {
	[TestClass]
	public class BlockoutPermsTests {
		///<summary>The spec's table: the actions each split permission covers for blockouts without the NS or DC flag.</summary>
		private static readonly Dictionary<BlockoutAction,EnumPermType> _dictSpecTable=new Dictionary<BlockoutAction,EnumPermType> {
			{ BlockoutAction.Create,EnumPermType.BlockoutAdd },
			{ BlockoutAction.Paste,EnumPermType.BlockoutAdd },
			{ BlockoutAction.Copy,EnumPermType.BlockoutAdd },
			{ BlockoutAction.Edit,EnumPermType.Blockouts },
			{ BlockoutAction.Delete,EnumPermType.BlockoutDelete },
			{ BlockoutAction.Cut,EnumPermType.BlockoutDelete },
			{ BlockoutAction.Clear,EnumPermType.BlockoutDelete },
		};

		private static IEnumerable<BlockoutAction> AllActions() {
			return Enum.GetValues(typeof(BlockoutAction)).Cast<BlockoutAction>();
		}

		///<summary>All 16 combinations of the four blockout permissions.</summary>
		private static IEnumerable<BlockoutPerms> AllCombinations() {
			for(int i=0;i<16;i++) {
				yield return new BlockoutPerms((i&1)!=0,(i&2)!=0,(i&4)!=0,(i&8)!=0);
			}
		}

		private static string Describe(BlockoutPerms blockoutPerms) {
			return "Add="+blockoutPerms.HasAdd+" Edit="+blockoutPerms.HasEdit+" Delete="+blockoutPerms.HasDelete+" Flagged="+blockoutPerms.HasFlagged;
		}

		[TestMethod]
		public void PermNumbers_MatchThe25_4Documents() {
			Assert.AreEqual(13,(int)EnumPermType.Blockouts,"Blockout Edit keeps 13.");
			Assert.AreEqual(272,(int)EnumPermType.BlockoutAdd);
			Assert.AreEqual(273,(int)EnumPermType.BlockoutDelete);
			Assert.AreEqual(266,(int)EnumPermType.BlockoutsFlagged);
		}

		[TestMethod]
		public void Unflagged_EveryActionMapsAsTheSpecTableSays() {
			CollectionAssert.AreEquivalent(AllActions().ToList(),_dictSpecTable.Keys.ToList(),"Every BlockoutAction is in the table.");
			foreach(BlockoutAction blockoutAction in AllActions()) {
				Assert.AreEqual(_dictSpecTable[blockoutAction],BlockoutPerms.GetPermTypeUnflagged(blockoutAction),blockoutAction.ToString());
				Assert.AreEqual(_dictSpecTable[blockoutAction],BlockoutPerms.GetPermType(blockoutAction,false),blockoutAction.ToString());
			}
		}

		[TestMethod]
		public void Flagged_EveryActionStaysUnderBlockoutsFlagged() {
			foreach(BlockoutAction blockoutAction in AllActions()) {
				Assert.AreEqual(EnumPermType.BlockoutsFlagged,BlockoutPerms.GetPermType(blockoutAction,true),blockoutAction.ToString());
			}
		}

		[TestMethod]
		public void NonBlockoutSchedules_UseTheSplitPermission() {
			//A Web Sched ASAP blockout left in an adopted database has no type; deleting it needs Blockout Delete (24.3 used Blockouts).
			Schedule scheduleAsap=new Schedule { SchedType=ScheduleType.WebSchedASAP,BlockoutType=0 };
			Assert.AreEqual(EnumPermType.BlockoutDelete,BlockoutPerms.GetPermType(BlockoutAction.Delete,scheduleAsap));
			Assert.AreEqual(EnumPermType.BlockoutAdd,BlockoutPerms.GetPermType(BlockoutAction.Create,(Schedule)null));
			Assert.AreEqual(EnumPermType.Blockouts,BlockoutPerms.GetPermType(BlockoutAction.Edit,scheduleAsap));
		}

		[TestMethod]
		public void Matrix_CanDoFollowsTheOnePermissionForEachActionAndKind() {
			foreach(BlockoutPerms blockoutPerms in AllCombinations()) {
				foreach(BlockoutAction blockoutAction in AllActions()) {
					bool expectedUnflagged;
					switch(_dictSpecTable[blockoutAction]) {
						case EnumPermType.BlockoutAdd:
							expectedUnflagged=blockoutPerms.HasAdd;
							break;
						case EnumPermType.BlockoutDelete:
							expectedUnflagged=blockoutPerms.HasDelete;
							break;
						default:
							expectedUnflagged=blockoutPerms.HasEdit;
							break;
					}
					string message=Describe(blockoutPerms)+" "+blockoutAction;
					Assert.AreEqual(expectedUnflagged,blockoutPerms.CanDo(blockoutAction,false),message+" unflagged");
					Assert.AreEqual(expectedUnflagged,blockoutPerms.HasUnflagged(blockoutAction),message+" HasUnflagged");
					Assert.AreEqual(blockoutPerms.HasFlagged,blockoutPerms.CanDo(blockoutAction,true),message+" flagged");
					Assert.AreEqual(expectedUnflagged || blockoutPerms.HasFlagged,blockoutPerms.CanDoAny(blockoutAction),message+" any kind");
				}
			}
		}

		[TestMethod]
		public void Matrix_SinglePermissionGivesOnlyItsOwnActions() {
			BlockoutPerms blockoutPermsAdd=new BlockoutPerms(true,false,false,false);
			BlockoutPerms blockoutPermsEdit=new BlockoutPerms(false,true,false,false);
			BlockoutPerms blockoutPermsDelete=new BlockoutPerms(false,false,true,false);
			Assert.AreEqual("Copy,Paste,Create",string.Join(",",AllActions().Where(x => blockoutPermsAdd.CanDo(x,false))));
			Assert.AreEqual("Edit",string.Join(",",AllActions().Where(x => blockoutPermsEdit.CanDo(x,false))));
			Assert.AreEqual("Cut,Delete,Clear",string.Join(",",AllActions().Where(x => blockoutPermsDelete.CanDo(x,false))));
			//None of the split permissions touches flagged blockouts.
			Assert.IsFalse(AllActions().Any(x => blockoutPermsAdd.CanDo(x,true) || blockoutPermsEdit.CanDo(x,true) || blockoutPermsDelete.CanDo(x,true)));
		}

		[TestMethod]
		public void Has_OnlyTheFourBlockoutPermissions() {
			BlockoutPerms blockoutPerms=new BlockoutPerms(true,true,true,true);
			Assert.IsTrue(blockoutPerms.Has(EnumPermType.BlockoutAdd));
			Assert.IsTrue(blockoutPerms.Has(EnumPermType.Blockouts));
			Assert.IsTrue(blockoutPerms.Has(EnumPermType.BlockoutDelete));
			Assert.IsTrue(blockoutPerms.Has(EnumPermType.BlockoutsFlagged));
			Assert.IsFalse(blockoutPerms.Has(EnumPermType.Schedules));
			Assert.IsFalse(blockoutPerms.Has(EnumPermType.Setup));
		}

		[TestMethod]
		public void CutCopyPasteTool_OpensWithAddDeleteOrFlagged() {
			foreach(BlockoutPerms blockoutPerms in AllCombinations()) {
				bool expected=blockoutPerms.HasAdd || blockoutPerms.HasDelete || blockoutPerms.HasFlagged;
				Assert.AreEqual(expected,blockoutPerms.CanOpenCutCopyPasteTool(),Describe(blockoutPerms));
			}
			Assert.IsFalse(new BlockoutPerms(false,true,false,false).CanOpenCutCopyPasteTool(),"Blockout Edit alone does nothing in the tool.");
		}

		[TestMethod]
		public void CutCopyPasteTool_ReplaceExistingAlsoNeedsDelete() {
			//add, edit, delete, flagged -> may paste with Replace Existing
			Assert.IsFalse(new BlockoutPerms(true,false,false,false).CanReplaceOnPaste(),"Add without Delete.");
			Assert.IsTrue(new BlockoutPerms(true,false,true,false).CanReplaceOnPaste(),"Add and Delete.");
			Assert.IsFalse(new BlockoutPerms(true,false,false,true).CanReplaceOnPaste(),"Unflagged pasted blockouts would land on unflagged ones it may not delete.");
			Assert.IsTrue(new BlockoutPerms(false,false,false,true).CanReplaceOnPaste(),"Flagged only: pastes and replaces flagged blockouts.");
			Assert.IsTrue(new BlockoutPerms(false,false,true,true).CanReplaceOnPaste());
			Assert.IsFalse(new BlockoutPerms(false,false,true,false).CanReplaceOnPaste(),"Delete alone cannot paste.");
			Assert.IsFalse(new BlockoutPerms(false,false,false,false).CanReplaceOnPaste());
		}

		[TestMethod]
		public void ChangeType_IsAnEditOfBothKinds() {
			BlockoutPerms blockoutPermsEdit=new BlockoutPerms(false,true,false,false);
			BlockoutPerms blockoutPermsFlagged=new BlockoutPerms(false,false,false,true);
			BlockoutPerms blockoutPermsBoth=new BlockoutPerms(false,true,false,true);
			BlockoutPerms blockoutPermsAddDeleteFlagged=new BlockoutPerms(true,false,true,true);
			Assert.IsTrue(blockoutPermsEdit.CanChangeType(false,false));
			Assert.IsFalse(blockoutPermsEdit.CanChangeType(false,true),"Unflagged to flagged needs BlockoutsFlagged too.");
			Assert.IsTrue(blockoutPermsFlagged.CanChangeType(true,true));
			Assert.IsFalse(blockoutPermsFlagged.CanChangeType(true,false),"Flagged to unflagged needs Blockout Edit too.");
			Assert.IsTrue(blockoutPermsBoth.CanChangeType(false,true));
			Assert.IsTrue(blockoutPermsBoth.CanChangeType(true,false));
			Assert.IsFalse(blockoutPermsAddDeleteFlagged.CanChangeType(true,false),"Add and Delete are not Edit.");
		}

		private static List<Def> GetDefs() {
			return new List<Def> {
				new Def { DefNum=1,Category=DefCat.BlockoutTypes,ItemName="Lunch",ItemValue="" },
				new Def { DefNum=2,Category=DefCat.BlockoutTypes,ItemName="Meeting",ItemValue="NS" },
				new Def { DefNum=3,Category=DefCat.BlockoutTypes,ItemName="Hold",ItemValue="DC" },
			};
		}

		private static string Filter(BlockoutPerms blockoutPerms,BlockoutAction blockoutAction) {
			return string.Join(",",blockoutPerms.FilterTypes(GetDefs(),blockoutAction).Select(x => x.DefNum));
		}

		[TestMethod]
		public void FilterTypes_FollowsTheActionsPermission() {
			BlockoutPerms blockoutPermsAdd=new BlockoutPerms(true,false,false,false);
			BlockoutPerms blockoutPermsEdit=new BlockoutPerms(false,true,false,false);
			BlockoutPerms blockoutPermsFlagged=new BlockoutPerms(false,false,false,true);
			Assert.AreEqual("1",Filter(blockoutPermsAdd,BlockoutAction.Create),"New blockout: unflagged types with Blockout Add.");
			Assert.AreEqual("",Filter(blockoutPermsAdd,BlockoutAction.Edit),"Blockout Add does not allow editing.");
			Assert.AreEqual("1",Filter(blockoutPermsEdit,BlockoutAction.Edit));
			Assert.AreEqual("",Filter(blockoutPermsEdit,BlockoutAction.Create),"Blockout Edit does not allow adding.");
			Assert.AreEqual("2,3",Filter(blockoutPermsFlagged,BlockoutAction.Create));
			Assert.AreEqual("2,3",Filter(blockoutPermsFlagged,BlockoutAction.Edit));
			Assert.AreEqual("1,2,3",Filter(new BlockoutPerms(true,false,false,true),BlockoutAction.Create));
			Assert.AreEqual(0,blockoutPermsAdd.FilterTypes(null,BlockoutAction.Create).Count);
		}

		[TestMethod]
		public void ForUser_NullUserHasNothing() {
			BlockoutPerms blockoutPerms=BlockoutPerms.ForUser(null);
			Assert.IsFalse(blockoutPerms.HasAdd || blockoutPerms.HasEdit || blockoutPerms.HasDelete || blockoutPerms.HasFlagged);
		}

		[TestMethod]
		public void GetPermTypesToDelete_EmptyForNoBlockouts() {
			Assert.AreEqual(0,BlockoutPerms.GetPermTypesToDelete(null).Count);
			Assert.AreEqual(0,BlockoutPerms.GetPermTypesToDelete(new List<Schedule>()).Count);
			//Two Web Sched ASAP blockouts (no type, so no definition lookup) need Blockout Delete once.
			List<Schedule> listSchedules=new List<Schedule> {
				new Schedule { SchedType=ScheduleType.WebSchedASAP },
				new Schedule { SchedType=ScheduleType.WebSchedASAP },
			};
			CollectionAssert.AreEqual(new List<EnumPermType> { EnumPermType.BlockoutDelete },BlockoutPerms.GetPermTypesToDelete(listSchedules));
		}
	}
}
