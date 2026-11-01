using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;
using SparksToothChart;

namespace OpenPmsDental{
	///<summary>Relays tooth chart commands to SparksToothChart.ToothChartWrapper, the only tooth chart. Open Dental's closed DirectX 11 chart
	///(Sparks3D.dll, under Open Dental's own licence) was removed on 2026-11-01. The wrapper draws with DirectX 9, OpenGL or Simple 2D and falls
	///back on its own when a mode cannot start. Methods for features only Sparks3D drew (ortho hardware, free text, brackets, retained roots,
	///space maintainers) are kept as no-ops so callers need no branches.</summary>
	public class ToothChartRelay{
		private ToothChartWrapper _toothChartWrapper;

		///<summary>hasHwnd is kept for callers; the wrapper always needs a window handle.</summary>
		public ToothChartRelay(bool hasHwnd=true){
		}

		#region Properties
		public int Bottom{
			get{
				return _toothChartWrapper.Bottom;
			}
		}

		public Color ColorBackgroundMain{
			set{
				_toothChartWrapper.ColorBackground=value;
			}
		}

		public Color ColorDrawing{
			set{
				_toothChartWrapper.ColorDrawing=value;
			}
		}

		public Color ColorText{
			set{
				_toothChartWrapper.ColorText=value;
			}
		}

		public Color ColorTextHighlightBack{
			set{
				_toothChartWrapper.ColorBackHighlight=value;
			}
		}

		public Color ColorTextHighlightFore{
			set{
				_toothChartWrapper.ColorTextHighlight=value;
			}
		}

		public CursorTool CursorTool{
			get{
				return _toothChartWrapper.CursorTool;
			}
			set{
				_toothChartWrapper.CursorTool=value;
			}
		}

		public bool Enabled{
			set{
				_toothChartWrapper.Enabled=value;
			}
		}

		public int Height{
			get{
				return _toothChartWrapper.Height;
			}
		}

		public bool IsPerioMode{
			set{
				_toothChartWrapper.PerioMode=true;
			}
		}

		public List<string> SelectedTeeth{
			get{
				return _toothChartWrapper.SelectedTeeth;
			}
		}

		public int Width{
			get{
				return _toothChartWrapper.Width;
			}
		}
		#endregion Properties

		#region Methods
		public void AddDrawingSegment(ToothInitial drawingSegment){
			_toothChartWrapper.AddDrawingSegment(drawingSegment);
		}

		///<summary>Not supported by SparksToothChart.</summary>
		public void AddOrthoWireBetweenBrackets(string toothIDstart,string toothIDend,Color color){
		}

		///<summary>Not supported by SparksToothChart.</summary>
		public void AddOrthoWireInBracket(string toothID,Color color){
		}

		///<summary>Not supported by SparksToothChart.</summary>
		public void AddOrthoElastic(string toothIDstart,string toothIDend,Color color){
		}

		public void AddPerioMeasure(int intTooth,PerioSequenceType sequenceType,int mb,int b,int db,int ml,int l, int dl){
			_toothChartWrapper.AddPerioMeasure(intTooth,sequenceType,mb,b,db,ml,l,dl);
		}

		///<summary>Not to be confused with SetText. Not supported by SparksToothChart.</summary>
		public void AddText(string text,PointF location,Color color,long toothInitialNum){
		}

		///<summary>toothChartWrapper.SuspendLayout</summary>
		public void BeginUpdate(){
			_toothChartWrapper.SuspendLayout();//badly named
		}

		public void DisposeControl(){
			_toothChartWrapper.Dispose();
		}

		///<summary>toothChartWrapper.ResumeLayout</summary>
		public void EndUpdate(){
			_toothChartWrapper.ResumeLayout();//badly named
		}

		public Bitmap GetBitmap(){
			return _toothChartWrapper.GetBitmap();
		}

		public void MoveTooth(string toothID,float rotate,float tipM,float tipB,float shiftM,float shiftO,float shiftB){
			_toothChartWrapper.MoveTooth(toothID,rotate,tipM,tipB,shiftM,shiftO,shiftB);
		}

		public void ResetTeeth(){
			_toothChartWrapper.ResetTeeth();
		}

		public void SetBigX(string toothID,Color color){
			_toothChartWrapper.SetBigX(toothID,color);
		}

		///<summary>Not supported by SparksToothChart.</summary>
		public void SetBracket(string toothID,Color color){
		}

		public void SetBU(string toothID,Color color){
			_toothChartWrapper.SetBU(toothID,color);
		}

		public void SetCrown(string toothID,Color color){
			_toothChartWrapper.SetCrown(toothID,color);
		}

		public void SetHidden(string toothID){
			_toothChartWrapper.SetHidden(toothID);
		}

		public void SetImplant(string toothID,Color color){
			_toothChartWrapper.SetImplant(toothID,color);
		}

		public void SetMissing(string toothID){
			_toothChartWrapper.SetMissing(toothID);
		}

		public void SetMobility(string toothID,string mobility,Color color){
			_toothChartWrapper.SetMobility(toothID,mobility,color);
		}

		///<summary>Not supported by SparksToothChart.</summary>
		public void SetOrthoMode(bool isOrthoMode){
		}

		public void SetPerioColors(Color colorBleeding,Color colorSuppuration,Color colorProbing,Color colorProbingRed,Color colorGM,Color colorCAL,
			Color colorMGJ,Color colorFurcations,Color colorFurcationsRed,int redLimitProbing,int redLimitFurcations){
			_toothChartWrapper.ColorBleeding=colorBleeding;
			_toothChartWrapper.ColorSuppuration=colorSuppuration;
			_toothChartWrapper.ColorProbing=colorProbing;
			_toothChartWrapper.ColorProbingRed=colorProbingRed;
			_toothChartWrapper.ColorGingivalMargin=colorGM;
			_toothChartWrapper.ColorCAL=colorCAL;
			_toothChartWrapper.ColorMGJ=colorMGJ;
			_toothChartWrapper.ColorFurcations=colorFurcations;
			_toothChartWrapper.ColorFurcationsRed=colorFurcationsRed;
			_toothChartWrapper.RedLimitProbing=redLimitProbing;
			_toothChartWrapper.RedLimitFurcations=redLimitFurcations;
		}

		public void SetPontic(string toothID,Color color){
			_toothChartWrapper.SetPontic(toothID,color);
		}

		public void SetPrimary(string toothID){
			_toothChartWrapper.SetPrimary(toothID);
		}

		public void SetRCT(string toothID,Color color){
			_toothChartWrapper.SetRCT(toothID,color);
		}

		///<summary>Not supported by SparksToothChart.</summary>
		public void SetRetainedRoot(string toothID,Color color){
		}

		public void SetSealant(string toothID,Color color){
			_toothChartWrapper.SetSealant(toothID,color);
		}

		public void SetSelected(string toothID,bool setValue){
			_toothChartWrapper.SetSelected(toothID,setValue);
		}

		///<summary>Not supported by SparksToothChart.</summary>
		public void SetSpaceMaintainer(string toothID,Color color){
		}

		public void SetSurfaceColors(string toothID,string surfaces,Color color){
			_toothChartWrapper.SetSurfaceColors(toothID,surfaces,color);
		}

		public void SetText(string toothID,Color color,string text){
			if(text=="W"){
				_toothChartWrapper.SetWatch(toothID,color);
			}
			//Other text not supported
		}

		public void SetToothChartWrapper(ToothChartWrapper toothChartWrapper){
			_toothChartWrapper=toothChartWrapper;
		}

		public void SetToothNumberingNomenclature(ToothNumberingNomenclature toothNumberingNomenclature){
			_toothChartWrapper.SetToothNumberingNomenclature(toothNumberingNomenclature);
		}

		public void SetVeneer(string toothID,Color color){
			_toothChartWrapper.SetVeneer(toothID,color);
		}
		#endregion Methods

		#region Colors
		private static List<UserOdPref> _listUserOdPrefsToothChartColor;
		
		/// <summary> Checks to see if preferences are up to date. If they are, no database query is made. However if a change occurs for a user, the list needs to update itself.
		private static List<UserOdPref> GetListUserOdPrefs() {
			if(_listUserOdPrefsToothChartColor==null) {
				RefreshToothColorsPrefs();
			}
			return _listUserOdPrefsToothChartColor;
		}

		///<summary>Checks ProvNums only if the logged in user has 'ToothChartUsesDiffColorByProv' user pref on</summary>
		public bool DoesToothColorPrefApply(List<long> listCurProvNums,long procProvNum) {
			//This first check is just being extra careful.
			return HasToothColorUserPref() && !listCurProvNums.Contains(procProvNum);
		}

		///<summary>
		///Returns ProvNums related to the passed in entities in a specific order. Returns the first of the following conditions: 
		///	1. Returns an empty list if the user currently logged in does not have ToothChartUsesDiffColorByProv user pref. 
		///	2. Returns the ProvNum of the user currently logged in if one is set. 
		///	3. Returns all primary providers for any appointment for the patient passed in that is scheduled for today. 
		///	4. Returns the primary provider if none of the previous conditions were met. Can return an empty list.
		///	</summary>
		public List<long> GetPertinentProvNumsForToothColorPref(Userod user,Patient patCur,List<Appointment> listAppts) {
			List<long> listCurProvNums=new List<long>();
			if(!HasToothColorUserPref()) {//We will leave this method if the current user doesn't have the pref
				return new List<long>();
			}
			//First, checks if current user is a provider
			if(user?.ProvNum!=0) {
				return new List<long>() { Security.CurUser.ProvNum };
			}
			if(patCur==null) {
				return new List<long>();
			}
			//Second, grabs the primary provider on today's appointment for the current patient. This list has already been filtered but just in case
			List<long> listApptProvNums=listAppts?.Where(x=>x.AptDateTime.Date==DateTime.Today && x.PatNum==patCur.PatNum)
				.Select(x=>x.ProvNum).ToList();
			if(!listApptProvNums.IsNullOrEmpty()) {
				return listApptProvNums;
			}
			//Finally, return the patient's primary provider. 
			return new List<long>() { patCur.PriProv };
		}

		///<summary>Sets colors for the teeth in the graphical tooth chart.  Pass doUseEcEoForCProcs=true if C procs should be colored like EO/EC.</summary>
		public bool GetToothColors(ProcedureCode curProc,ProcStat procStatus,bool doUseEcEoForCProcs,out Color cDark,out Color cLight) {
			cDark=Color.White;
			cLight=Color.White;
			List<Def> listDefsToothColors=Defs.GetDefsForCategory(DefCat.ChartGraphicColors,true);
			if(curProc.GraphicColor!=Color.FromArgb(0) && procStatus!=ProcStat.TPi) {
				cDark=curProc.GraphicColor;
				cLight=curProc.GraphicColor;
				return true;
			}
			switch(procStatus) {
				case ProcStat.C:					
					if(doUseEcEoForCProcs) {//Whoever is currently viewing the ToothChart wants other Provider's C procs to show as EC
						cDark=listDefsToothColors[3].ItemColor;//same values as ProcStat.EO
						cLight=listDefsToothColors[8].ItemColor;
					}
					else {
						cDark=listDefsToothColors[1].ItemColor;
						cLight=listDefsToothColors[6].ItemColor;
					}
					return true;
				case ProcStat.TP:
					cDark=listDefsToothColors[0].ItemColor;
					cLight=listDefsToothColors[5].ItemColor;
					return true;
				case ProcStat.EC:
					cDark=listDefsToothColors[2].ItemColor;
					cLight=listDefsToothColors[7].ItemColor;
					return true;
				case ProcStat.EO:
					cDark=listDefsToothColors[3].ItemColor;
					cLight=listDefsToothColors[8].ItemColor;
					return true;
				case ProcStat.R:
					cDark=listDefsToothColors[4].ItemColor;
					cLight=listDefsToothColors[9].ItemColor;
					return true;
				case ProcStat.Cn:
					cDark=listDefsToothColors[16].ItemColor;
					cLight=listDefsToothColors[17].ItemColor;
					return true;
				case ProcStat.D://Can happen with invalidated locked procs.
				default:
					cDark=Color.White;
					cLight=Color.White;
					return false;//Don't draw.
			}
		}

		///<summary>Will update list of users with preference if a change is made while OD is running</summary>
		public static void RefreshToothColorsPrefs() {
			_listUserOdPrefsToothChartColor=UserOdPrefs.GetByFkeyType(UserOdFkeyType.ToothChartUsesDiffColorByProv);
		}

		///<summary>Returns true if the currently logged in user has ColorChartUsesDiffByProv preference</summary>
		public bool HasToothColorUserPref(){
			return GetListUserOdPrefs().Select(x => x.UserNum).Contains(Security.CurUser.UserNum);
		}

		#endregion Colors

	}
}
