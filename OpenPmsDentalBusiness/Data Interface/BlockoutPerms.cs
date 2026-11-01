// The blockout permission split of Open Dental 25.4 (backlog item v25_4-appts-blockout-permissions, package M2-APPT in
// docs/specs/port-backlog.md): which permission covers each blockout action, and what a user with a given set of blockout permissions may do.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>The blockout permissions of one user. Open Dental 25.4 split the single blockout permission for blockouts whose type has no NS
	///(no scheduling) or DC (no cut/copy/paste) flag into three: Blockout Add (272) for create, paste and copy; Blockout Edit (13, C# name
	///Blockouts) for edit; Blockout Delete (273) for delete, cut and clear. Blockouts whose type has a flag stay under BlockoutsFlagged (266)
	///for every action. Moving or resizing a blockout counts as Edit.</summary>
	public class BlockoutPerms {
		///<summary>Blockout Add (272).</summary>
		public bool HasAdd;
		///<summary>Blockout Edit (13).</summary>
		public bool HasEdit;
		///<summary>Blockout Delete (273).</summary>
		public bool HasDelete;
		///<summary>BlockoutsFlagged (266).</summary>
		public bool HasFlagged;

		public BlockoutPerms(bool hasAdd,bool hasEdit,bool hasDelete,bool hasFlagged) {
			HasAdd=hasAdd;
			HasEdit=hasEdit;
			HasDelete=hasDelete;
			HasFlagged=hasFlagged;
		}

		#region Methods - Static
		///<summary>The current user's blockout permissions, checked without showing any message. None when nobody is logged on.</summary>
		public static BlockoutPerms ForCurrentUser() {
			Meth.NoCheckMiddleTierRole();
			return ForUser(Security.CurUser);
		}

		///<summary>The blockout permissions of the user, checked without showing any message. None for a null user.</summary>
		public static BlockoutPerms ForUser(Userod userod) {
			Meth.NoCheckMiddleTierRole();
			if(userod==null) {
				return new BlockoutPerms(false,false,false,false);
			}
			return new BlockoutPerms(IsAuthorized(EnumPermType.BlockoutAdd,userod),IsAuthorized(EnumPermType.Blockouts,userod),
				IsAuthorized(EnumPermType.BlockoutDelete,userod),IsAuthorized(EnumPermType.BlockoutsFlagged,userod));
		}

		private static bool IsAuthorized(EnumPermType permType,Userod userod) {
			return Security.IsAuthorized(permType,DateTime.MinValue,true,true,userod);
		}

		///<summary>The permission that covers the action for a blockout whose type has no NS or DC flag: Blockout Add for create, paste and copy,
		///Blockout Delete for delete, cut and clear, and Blockout Edit (13) for edit.</summary>
		public static EnumPermType GetPermTypeUnflagged(BlockoutAction blockoutAction) {
			Meth.NoCheckMiddleTierRole();
			switch(blockoutAction) {
				case BlockoutAction.Create:
				case BlockoutAction.Paste:
				case BlockoutAction.Copy:
					return EnumPermType.BlockoutAdd;
				case BlockoutAction.Delete:
				case BlockoutAction.Cut:
				case BlockoutAction.Clear:
					return EnumPermType.BlockoutDelete;
				case BlockoutAction.Edit:
				default:
					return EnumPermType.Blockouts;
			}
		}

		///<summary>BlockoutsFlagged for a flagged blockout type, otherwise the split permission for the action.</summary>
		public static EnumPermType GetPermType(BlockoutAction blockoutAction,bool isFlagged) {
			Meth.NoCheckMiddleTierRole();
			if(isFlagged) {
				return EnumPermType.BlockoutsFlagged;
			}
			return GetPermTypeUnflagged(blockoutAction);
		}

		///<summary>The permission for the action on blockouts of this type (a BlockoutTypes definition; an unknown type counts as unflagged).</summary>
		public static EnumPermType GetPermType(BlockoutAction blockoutAction,long blockoutTypeDefNum) {
			Meth.NoCheckMiddleTierRole();
			return GetPermType(blockoutAction,Schedules.IsBlockoutTypeFlagged(blockoutTypeDefNum));
		}

		///<summary>The permission for the action on this blockout. A schedule that is not a regular blockout, such as a Web Sched ASAP blockout left
		///in an adopted database, has no type, so it uses the split permission for the action.</summary>
		public static EnumPermType GetPermType(BlockoutAction blockoutAction,Schedule schedule) {
			Meth.NoCheckMiddleTierRole();
			if(schedule==null || schedule.SchedType!=ScheduleType.Blockout) {
				return GetPermTypeUnflagged(blockoutAction);
			}
			return GetPermType(blockoutAction,schedule.BlockoutType);
		}

		///<summary>The distinct permissions needed to delete these blockouts, for example when a new or pasted blockout replaces overlapping
		///ones. Empty for an empty or null list.</summary>
		public static List<EnumPermType> GetPermTypesToDelete(List<Schedule> listSchedules) {
			Meth.NoCheckMiddleTierRole();
			if(listSchedules==null) {
				return new List<EnumPermType>();
			}
			return listSchedules.Select(x => GetPermType(BlockoutAction.Delete,x)).Distinct().OrderBy(x => (int)x).ToList();
		}
		#endregion Methods - Static

		#region Methods - Public
		///<summary>Whether this user holds the permission. Only the four blockout permissions are known here; any other is false.</summary>
		public bool Has(EnumPermType permType) {
			switch(permType) {
				case EnumPermType.BlockoutAdd:
					return HasAdd;
				case EnumPermType.Blockouts:
					return HasEdit;
				case EnumPermType.BlockoutDelete:
					return HasDelete;
				case EnumPermType.BlockoutsFlagged:
					return HasFlagged;
				default:
					return false;
			}
		}

		///<summary>Whether the user may take the action on blockouts whose type has no flag.</summary>
		public bool HasUnflagged(BlockoutAction blockoutAction) {
			return Has(GetPermTypeUnflagged(blockoutAction));
		}

		///<summary>Whether the user may take the action on a blockout of this kind.</summary>
		public bool CanDo(BlockoutAction blockoutAction,bool isFlagged) {
			return Has(GetPermType(blockoutAction,isFlagged));
		}

		///<summary>Whether the user may take the action on a blockout of this type.</summary>
		public bool CanDo(BlockoutAction blockoutAction,long blockoutTypeDefNum) {
			return Has(GetPermType(blockoutAction,blockoutTypeDefNum));
		}

		///<summary>Whether the user may take the action on this blockout.</summary>
		public bool CanDo(BlockoutAction blockoutAction,Schedule schedule) {
			return Has(GetPermType(blockoutAction,schedule));
		}

		///<summary>Whether the user may take the action on at least one kind of blockout. Bulk actions (clear, copy and paste of days) are offered
		///then, and act only on the kinds the user may manage.</summary>
		public bool CanDoAny(BlockoutAction blockoutAction) {
			return HasUnflagged(blockoutAction) || HasFlagged;
		}

		///<summary>Changing a blockout's type is an edit: it needs the edit permission of the old type's kind and of the new type's kind, so going
		///between flagged and unflagged needs BlockoutsFlagged plus Blockout Edit.</summary>
		public bool CanChangeType(bool isFlaggedOld,bool isFlaggedNew) {
			return CanDo(BlockoutAction.Edit,isFlaggedOld) && CanDo(BlockoutAction.Edit,isFlaggedNew);
		}

		///<summary>The blockout types the user may pick for the action: unflagged types need the split permission for the action, flagged types
		///need BlockoutsFlagged.</summary>
		public List<Def> FilterTypes(List<Def> listDefsBlockoutTypes,BlockoutAction blockoutAction) {
			return Schedules.FilterBlockoutTypesForUser(listDefsBlockoutTypes,HasUnflagged(blockoutAction),HasFlagged);
		}

		///<summary>The Blockout Cut-Copy-Paste window copies and pastes days (Blockout Add) and clears a day (Blockout Delete). Either of those, or
		///BlockoutsFlagged, opens it.</summary>
		public bool CanOpenCutCopyPasteTool() {
			return CanDoAny(BlockoutAction.Paste) || CanDoAny(BlockoutAction.Clear);
		}

		///<summary>Pasting a day or week with "Replace Existing" first deletes the blockouts already there, of the kinds being pasted. Pasting
		///unflagged blockouts then also needs Blockout Delete; pasting only flagged ones needs only BlockoutsFlagged.</summary>
		public bool CanReplaceOnPaste() {
			if(HasUnflagged(BlockoutAction.Paste)) {
				return HasUnflagged(BlockoutAction.Clear);
			}
			return HasFlagged;
		}
		#endregion Methods - Public
	}
}
