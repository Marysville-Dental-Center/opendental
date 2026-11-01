// M2-MISC (Open Dental 25.4, "Perio Chart direction radio buttons improved"): the Current Direction buttons of the Perio Chart.
// - A direction chosen with the buttons applies at once to the next keystroke from the current cell.
// - It holds while entry stays in the same row section; auto-advance into another section lets the advance sequence (Maxillary First or
//   Facials First) set the direction again.
// - The arrow keys never change the direction.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDental {
	public partial class ContrPerio {
		///<summary>The section (0 to 3, see GetSection) of the cell the user was on when they chose a direction with the Current Direction buttons, or
		///-1 when no such choice applies.</summary>
		private int _sectionDirectionManual=-1;
		///<summary>The direction the user chose with the Current Direction buttons.</summary>
		private EnumCurrentDirection _enumCurrentDirectionManual;

		///<summary>The user chose a direction with the Current Direction buttons. It applies to the next keystroke from the current cell, and holds
		///while entry stays in this cell's section. Raises DirectionChangedLeft or DirectionChangedRight so the window's buttons and Forward/Reverse
		///label follow.</summary>
		public void SetDirectionManual(EnumCurrentDirection enumCurrentDirection) {
			EnumCurrentDirection_=enumCurrentDirection;
			_enumCurrentDirectionManual=enumCurrentDirection;
			_sectionDirectionManual=-1;
			if(ListColRowsSelected!=null && ListColRowsSelected.Count>0) {
				_sectionDirectionManual=GetSection(ListColRowsSelected[0].Row);
			}
			RaiseDirectionChanged();
		}

		///<summary>True while a direction chosen with the Current Direction buttons applies in the section: the choice was made in that section and
		///the direction has not changed since.</summary>
		public bool IsDirectionManual(int section) {
			return PerioDirectionRules.IsManualInSection(_sectionDirectionManual,section,_enumCurrentDirectionManual,EnumCurrentDirection_);
		}

		///<summary>Raises the event for the current direction.</summary>
		private void RaiseDirectionChanged() {
			if(EnumCurrentDirection_==EnumCurrentDirection.Left) {
				DirectionChangedLeft?.Invoke(this,new EventArgs());
			}
			else {
				DirectionChangedRight?.Invoke(this,new EventArgs());
			}
		}

		///<summary>Sets the direction and raises its event, only if it changes.</summary>
		private void SetDirectionIfChanged(EnumCurrentDirection enumCurrentDirection) {
			if(EnumCurrentDirection_==enumCurrentDirection) {
				return;
			}
			EnumCurrentDirection_=enumCurrentDirection;
			RaiseDirectionChanged();
		}

		///<summary>Runs after AdvanceCell moved the cursor from a cell in sectionFrom. When it crossed into another section, a direction chosen with
		///the buttons stops applying; with Facials First (and when the move may set the direction, so not for the arrow keys) the direction becomes
		///the path's direction at the new cell. Maxillary First already set the direction while crossing (TryAutoAdvanceLeft/Right).</summary>
		private void AfterAdvanceDirection(int sectionFrom,bool setDirection) {
			if(ListColRowsSelected==null || ListColRowsSelected.Count==0) {
				return;
			}
			int sectionTo=GetSection(ListColRowsSelected[0].Row);
			if(sectionTo==sectionFrom) {
				return;
			}
			_sectionDirectionManual=-1;
			if(!setDirection || EnumAdvanceSequence_!=EnumAdvanceSequence.FacialsFirst) {
				return;
			}
			PerioCell perioCell=GetCurrentCell();
			EnumCurrentDirection? enumCurrentDirectionPath=PerioDirectionRules.GetPathDirection(EnumAdvanceSequence_,perioCell.ToothNum,perioCell.IsFacial);
			if(enumCurrentDirectionPath!=null) {
				SetDirectionIfChanged(enumCurrentDirectionPath.Value);
			}
		}
	}

	///<summary>The rules of the Perio Chart's Current Direction, without the control, so they can be tested on their own.</summary>
	public static class PerioDirectionRules {
		///<summary>True if a direction chosen with the buttons in sectionManual (-1 for none) still applies in section: same section, and the
		///current direction is still the chosen one.</summary>
		public static bool IsManualInSection(int sectionManual,int section,EnumCurrentDirection enumCurrentDirectionManual,
			EnumCurrentDirection enumCurrentDirectionCurrent)
		{
			return sectionManual!=-1 && sectionManual==section && enumCurrentDirectionManual==enumCurrentDirectionCurrent;
		}

		///<summary>The direction entry goes in with Facials First: the direction chosen with the buttons while it applies, otherwise the path's.</summary>
		public static EnumCurrentDirection GetForwardDirection(EnumCurrentDirection enumCurrentDirectionPath,EnumCurrentDirection enumCurrentDirectionCurrent,
			bool isManual)
		{
			return isManual ? enumCurrentDirectionCurrent : enumCurrentDirectionPath;
		}

		///<summary>True if the cursor moves against the advance path: entry in the reverse direction, or Backspace while going forward (isReverse).
		///Backspace in the reverse direction moves along the path.</summary>
		public static bool IsAgainstPath(EnumCurrentDirection enumCurrentDirectionPath,EnumCurrentDirection enumCurrentDirectionForward,bool isReverse) {
			bool isReverseChosen=enumCurrentDirectionForward!=enumCurrentDirectionPath;
			return isReverseChosen!=isReverse;
		}

		///<summary>The path's direction at a tooth and side, or null when the path does not have the position.</summary>
		public static EnumCurrentDirection? GetPathDirection(EnumAdvanceSequence enumAdvanceSequence,int toothNum,bool isFacial) {
			PerioAdvancePos perioAdvancePos=PerioAdvancePos.GetPath(enumAdvanceSequence).FirstOrDefault(x => x.ToothNum==toothNum && x.IsFacial==isFacial);
			return perioAdvancePos?.EnumCurrentDirection2;
		}

		///<summary>"Forward" when the current direction is the path's at the position, "Reverse" when not, or "" when the path has no such position.
		///Not translated here.</summary>
		public static string GetForwardReverseText(EnumAdvanceSequence enumAdvanceSequence,int toothNum,bool isFacial,
			EnumCurrentDirection enumCurrentDirectionCurrent)
		{
			EnumCurrentDirection? enumCurrentDirectionPath=GetPathDirection(enumAdvanceSequence,toothNum,isFacial);
			if(enumCurrentDirectionPath==null) {
				return "";
			}
			return (enumCurrentDirectionPath.Value==enumCurrentDirectionCurrent) ? "Forward" : "Reverse";
		}
	}
}
