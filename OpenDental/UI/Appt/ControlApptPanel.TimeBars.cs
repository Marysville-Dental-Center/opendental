// Package B-APPT (docs/specs/port-backlog.md): drawing for the two time bar preferences of Preferences, Appts, Appearance.
// - v25_2-appts-provider-timebars-op-background: provider time on the operatory background (hooked from DrawMainBackground).
// - v25_3-appts-timebar-secondary-color: secondary provider color in an empty appointment time bar (hooked from DrawOneAppt).
// The rules are in OpenDentBusiness/Misc/ApptModule/ApptTimeBars.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental.UI {
	public partial class ControlApptPanel {
		///<summary>With the preference on, paints the scheduled provider time of one operatory column in a light tint of each provider's color,
		///over the generic open color that was just painted. Overlapping providers split the column width. Hygienists use their own color.
		///Printing in less-color or grayscale mode keeps the old output. Holidays, blockouts, time bars and appointments are drawn as before,
		///so the operatory time bars on the left of the column still show.</summary>
		private void DrawOpBackgroundProvTime(Graphics g,List<Schedule> listSchedulesForOp,float xColumn,float widthColumn) {
			if(!ApptTimeBars.IsProvTimeOnOpBackground()) {
				return;
			}
			if(_isPrinting && PrintColorBehavior.In(ApptPrintColorBehavior.LessColor,ApptPrintColorBehavior.Grayscale)) {
				return;
			}
			List<ApptOpBackgroundSegment> listSegments=ApptTimeBars.GetOpBackgroundSegments(listSchedulesForOp);
			for(int i=0;i<listSegments.Count;i++) {
				ApptOpBackgroundSegment segment=listSegments[i];
				float widthShare=widthColumn/segment.Count;
				float yTop=TimeToYForBackground(segment.TimeStart);
				float yBottom=TimeToYForBackground(segment.TimeStop);
				using SolidBrush solidBrush=new SolidBrush(ApptTimeBars.TintTowardWhite(Providers.GetColor(segment.ProvNum)));
				g.FillRectangle(solidBrush,xColumn+widthShare*segment.Index,yTop,widthShare,yBottom-yTop);
			}
		}

		///<summary>The same y position DrawMainBackground computes for a schedule's start and stop.</summary>
		private float TimeToYForBackground(TimeSpan timeSpan) {
			return timeSpan.Hours*_heightLine*_rowsPerHr+timeSpan.Minutes*_heightLine/_minPerRow;
		}

		///<summary>With the preference on, fills the whole time bar on the left of an appointment with the secondary provider's color when neither
		///time pattern has provider time. Drawn before the pattern loops, so their row lines still show on top. Grayscale printing desaturates
		///the color, as for the provider time itself.</summary>
		private void DrawTimeBarSecondaryNoProvTime(Graphics g,DataRow dataRow,GraphicsPath graphicsPathLeftSide) {
			if(!ApptTimeBars.IsSecondaryColorNoProvTime()) {
				return;
			}
			long provNumSecondary=ApptTimeBars.GetSecondaryProvNumForEmptyTimeBar(
				pattern:dataRow["Pattern"].ToString(),
				patternSecondary:dataRow.Table.Columns.Contains("PatternSecondary") ? dataRow["PatternSecondary"].ToString() : "",
				provNum:PIn.Long(dataRow["ProvNum"].ToString()),
				provHyg:PIn.Long(dataRow["ProvHyg"].ToString()),
				isHygiene:dataRow["IsHygiene"].ToString()=="1");
			if(provNumSecondary==0) {
				return;
			}
			Color color=Providers.GetColor(provNumSecondary);
			if(_isPrinting && PrintColorBehavior==ApptPrintColorBehavior.Grayscale) {
				color=DesaturateColor(color);
			}
			using SolidBrush solidBrush=new SolidBrush(color);
			g.FillPath(solidBrush,graphicsPathLeftSide);
		}
	}
}
