// The PreAuthFlag appointment element and appointment bubble field (Open Dental 26.1, feature v26_1-lists-carrier-preauth-flags, package M2-CARRIER
// in docs/specs/port-backlog.md). The data comes from Appointments.FillPreAuthFlags (preAuthFlag, preAuthCodes, preAuthColor columns). ControlApptPanel.cs
// only gains one case in DrawElement and one in the bubble that call these methods.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Data;
using System.Drawing;
using OpenDentBusiness;

namespace OpenDental.UI {
	public partial class ControlApptPanel {
		///<summary>Draws the "PreAuth" tag of the PreAuthFlag element and returns the point where the next element draws, like DrawElement. The tag
		///shows when the appointment holds a TP or TPi procedure that one of the patient's carriers wants preauthorized and that has no
		///preauthorization yet. When that carrier has an appointment text back color, the tag is highlighted with it (INFERRED reading of "in the
		///carrier's appointment text color"); the text uses the element's color. In the upper and lower right corners it is a boxed tag like the
		///other flag letters.</summary>
		private PointF DrawElementPreAuthFlag(Graphics g,PointF pointDraw,ApptViewStackBehavior stackBehavior,ApptViewAlignment align,DataRow dataRoww,
			ApptViewItem apptViewItem,float widthAppt,float heightAppt,bool isNote)
		{
			string text=GetPreAuthValue(dataRoww,Appointments.PreAuthFlagColumn);
			if(isNote || text=="") {
				return pointDraw;//The next element draws where this one would have.
			}
			Color colorText=apptViewItem.ElementColor;
			Color colorBack=GetPreAuthCarrierColor(dataRoww);
			if(_isPrinting && PrintColorBehavior==ApptPrintColorBehavior.Grayscale) {
				colorText=DesaturateColor(colorText);
				if(!colorBack.IsEmpty) {
					colorBack=DesaturateColor(colorBack);
				}
			}
			StringFormat stringFormat=new StringFormat();
			stringFormat.Alignment=StringAlignment.Near;
			using SolidBrush brushText=new SolidBrush(colorText);
			float widthText=Math.Max(5,g.MeasureString(text,_font).Width);
			if(align==ApptViewAlignment.Main) {
				if(pointDraw.Y+_heightLine>heightAppt) {
					stringFormat.Dispose();
					return pointDraw;
				}
				float widthAvailable=widthAppt-WidthProvOnAppt-1;
				RectangleF rectangleText=new RectangleF(pointDraw.X,pointDraw.Y,Math.Min(widthText,widthAvailable),_heightLine);
				if(!colorBack.IsEmpty) {
					using SolidBrush brushBack=new SolidBrush(colorBack);
					g.FillRectangle(brushBack,rectangleText.X,rectangleText.Y-1,rectangleText.Width,rectangleText.Height);
				}
				g.DrawString(text,_font,brushText,rectangleText,stringFormat);
				stringFormat.Dispose();
				return new PointF(pointDraw.X,pointDraw.Y+_heightLine);
			}
			//UR and LR: a one-line box. pointDraw is the upper right (UR) or lower right (LR) corner of the element.
			bool isUpperRight=(align==ApptViewAlignment.UR);
			bool isVertical=(stackBehavior==ApptViewStackBehavior.Vertical);
			if(isVertical && isUpperRight && pointDraw.Y+_heightLine+1>heightAppt) {
				stringFormat.Dispose();
				return pointDraw;
			}
			if(!isVertical && pointDraw.X-widthText<=0) {
				stringFormat.Dispose();
				return new PointF(pointDraw.X-widthText,pointDraw.Y);
			}
			float yTop=isUpperRight ? pointDraw.Y : pointDraw.Y-_heightLine;
			PointF pointUpperLeft=new PointF(pointDraw.X-(int)widthText,yTop);
			RectangleF rectangleLayout=new RectangleF(pointUpperLeft,new SizeF(widthText,_heightLine+1));
			rectangleLayout.Y+=2;
			RectangleF rectangleBack=new RectangleF(pointUpperLeft.X,pointUpperLeft.Y+1,widthText,_heightLine);
			if(colorBack.IsEmpty) {
				g.FillRectangle(_brushWhite,rectangleBack);
				g.DrawString(text,_font,brushText,rectangleLayout,stringFormat);
			}
			else {
				using SolidBrush brushBack=new SolidBrush(colorBack);
				g.FillRectangle(brushBack,rectangleBack);
				g.DrawString(text,_font,_brushBlack,rectangleLayout,stringFormat);
			}
			g.DrawRectangle(_penBlack,rectangleBack.X,rectangleBack.Y,rectangleBack.Width,rectangleBack.Height);
			stringFormat.Dispose();
			if(isVertical) {
				return new PointF(pointDraw.X,isUpperRight ? pointDraw.Y+_heightLine : pointDraw.Y-_heightLine);
			}
			return new PointF(pointDraw.X-(int)widthText-3,pointDraw.Y);
		}

		///<summary>Draws the PreAuthFlag bubble line, for example "PreAuth needed: D2740,D2950" in red, highlighted with the carrier's appointment text
		///back color when it has one. Returns the new y. Draws nothing when no procedure needs a preauthorization.</summary>
		private float DrawBubblePreAuthFlag(Graphics g,Font font,DataRow dataRow,float x,float y,int widthBubble) {
			if(GetPreAuthValue(dataRow,Appointments.PreAuthFlagColumn)=="") {
				return y;
			}
			string text=Lan.g(this,"PreAuth needed:")+" "+GetPreAuthValue(dataRow,Appointments.PreAuthCodesColumn);
			float height=g.MeasureString(text,font,widthBubble-(int)x).Height;
			Color colorBack=GetPreAuthCarrierColor(dataRow);
			if(!colorBack.IsEmpty) {
				using SolidBrush brushBack=new SolidBrush(colorBack);
				g.FillRectangle(brushBack,x-2,y+1,widthBubble-(int)x+2,height);
			}
			g.DrawString(text,font,Brushes.Red,new RectangleF(x,y,widthBubble-(int)x,height));
			return y+height;
		}

		///<summary>The value of one of the preauthorization columns, or "" when the table does not have it (an empty view returns the table early).</summary>
		private static string GetPreAuthValue(DataRow dataRow,string columnName) {
			if(dataRow==null || dataRow.Table==null || !dataRow.Table.Columns.Contains(columnName)) {
				return "";
			}
			return dataRow[columnName].ToString();
		}

		///<summary>The carrier's appointment text back color for the tag, or Color.Empty when it has none.</summary>
		private static Color GetPreAuthCarrierColor(DataRow dataRow) {
			string argb=GetPreAuthValue(dataRow,Appointments.PreAuthColorColumn);
			if(argb=="") {
				return Color.Empty;
			}
			return Color.FromArgb(PIn.Int(argb,false));
		}
	}
}
