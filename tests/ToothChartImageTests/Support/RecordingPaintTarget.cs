// The recording target of the call-log tests (package TC-2): an IToothChartPaintTarget that writes each call of ToothChartPainter to a CallLog,
// with the same names and arguments as the harness that recorded the callers' code before TC-2 moved it (deleted since).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Drawing;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace ToothChartImageTests {
	public class RecordingPaintTarget:IToothChartPaintTarget {
		private readonly CallLog _callLog;

		public RecordingPaintTarget(CallLog callLog) {
			_callLog=callLog;
		}

		public void AddDrawingSegment(ToothInitial drawingSegment) {
			_callLog.Add("AddDrawingSegment",drawingSegment);
		}

		public void AddOrthoElastic(string toothIDstart,string toothIDend,Color color) {
			_callLog.Add("AddOrthoElastic",toothIDstart,toothIDend,color);
		}

		public void AddOrthoWireBetweenBrackets(string toothIDstart,string toothIDend,Color color) {
			_callLog.Add("AddOrthoWireBetweenBrackets",toothIDstart,toothIDend,color);
		}

		public void AddOrthoWireInBracket(string toothID,Color color) {
			_callLog.Add("AddOrthoWireInBracket",toothID,color);
		}

		public void AddText(string text,PointF location,Color color,long toothInitialNum) {
			_callLog.Add("AddText",text,location,color,toothInitialNum);
		}

		public void MoveTooth(string toothID,float rotate,float tipM,float tipB,float shiftM,float shiftO,float shiftB) {
			_callLog.Add("MoveTooth",toothID,rotate,tipM,tipB,shiftM,shiftO,shiftB);
		}

		public void SetBigX(string toothID,Color color) {
			_callLog.Add("SetBigX",toothID,color);
		}

		public void SetBracket(string toothID,Color color) {
			_callLog.Add("SetBracket",toothID,color);
		}

		public void SetBU(string toothID,Color color) {
			_callLog.Add("SetBU",toothID,color);
		}

		public void SetCrown(string toothID,Color color) {
			_callLog.Add("SetCrown",toothID,color);
		}

		public void SetHidden(string toothID) {
			_callLog.Add("SetHidden",toothID);
		}

		public void SetImplant(string toothID,Color color) {
			_callLog.Add("SetImplant",toothID,color);
		}

		public void SetMissing(string toothID) {
			_callLog.Add("SetMissing",toothID);
		}

		public void SetPontic(string toothID,Color color) {
			_callLog.Add("SetPontic",toothID,color);
		}

		public void SetPrimary(string toothID) {
			_callLog.Add("SetPrimary",toothID);
		}

		public void SetRCT(string toothID,Color color) {
			_callLog.Add("SetRCT",toothID,color);
		}

		public void SetRetainedRoot(string toothID,Color color) {
			_callLog.Add("SetRetainedRoot",toothID,color);
		}

		public void SetSealant(string toothID,Color color) {
			_callLog.Add("SetSealant",toothID,color);
		}

		public void SetSpaceMaintainer(string toothID,Color color) {
			_callLog.Add("SetSpaceMaintainer",toothID,color);
		}

		public void SetSurfaceColors(string toothID,string surfaces,Color color) {
			_callLog.Add("SetSurfaceColors",toothID,surfaces,color);
		}

		public void SetText(string toothID,Color color,string text) {
			_callLog.Add("SetText",toothID,color,text);
		}

		public void SetVeneer(string toothID,Color color) {
			_callLog.Add("SetVeneer",toothID,color);
		}
	}
}
