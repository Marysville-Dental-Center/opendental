using System;
using System.Collections.Generic;
using System.Data;
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
	///space maintainers) are kept as no-ops so callers need no branches. Since TC-2 the callers' painting code is ToothChartPainter, which draws
	///on the relay as its IToothChartPaintTarget, and the callers' chart settings are the relay's settings helpers.</summary>
	public class ToothChartRelay:IToothChartPaintTarget{
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

		///<summary>Sets the wrapper's perio mode to the value given. Turning it on is only allowed in DirectX (the wrapper throws otherwise).</summary>
		public bool IsPerioMode{
			set{
				_toothChartWrapper.PerioMode=value;
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

		///<summary>Also passes the wrapper's TextMoved on as this relay's TextMoved.</summary>
		public void SetToothChartWrapper(ToothChartWrapper toothChartWrapper){
			if(_toothChartWrapper!=null) {
				_toothChartWrapper.TextMoved-=ToothChartWrapper_TextMoved;
			}
			_toothChartWrapper=toothChartWrapper;
			if(_toothChartWrapper!=null) {
				_toothChartWrapper.TextMoved+=ToothChartWrapper_TextMoved;
			}
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
		internal static List<UserOdPref> GetListUserOdPrefs() {
			if(_listUserOdPrefsToothChartColor==null) {
				RefreshToothColorsPrefs();
			}
			return _listUserOdPrefsToothChartColor;
		}

		///<summary>Checks ProvNums only if the logged in user has 'ToothChartUsesDiffColorByProv' user pref on (ToothChartPainter.DoesToothColorPrefApply).</summary>
		public bool DoesToothColorPrefApply(List<long> listCurProvNums,long procProvNum) {
			return ToothChartPainter.DoesToothColorPrefApply(listCurProvNums,procProvNum);
		}

		///<summary>The ProvNums whose completed work keeps its own colour (ToothChartPainter.GetPertinentProvNumsForToothColorPref).</summary>
		public List<long> GetPertinentProvNumsForToothColorPref(Userod user,Patient patCur,List<Appointment> listAppts) {
			return ToothChartPainter.GetPertinentProvNumsForToothColorPref(user,patCur,listAppts);
		}

		///<summary>Sets colors for the teeth in the graphical tooth chart (ToothChartPainter.GetToothColors).</summary>
		public bool GetToothColors(ProcedureCode curProc,ProcStat procStatus,bool doUseEcEoForCProcs,out Color cDark,out Color cLight) {
			return ToothChartPainter.GetToothColors(curProc,procStatus,doUseEcEoForCProcs,out cDark,out cLight);
		}

		///<summary>Will update list of users with preference if a change is made while OD is running</summary>
		public static void RefreshToothColorsPrefs() {
			_listUserOdPrefsToothChartColor=UserOdPrefs.GetByFkeyType(UserOdFkeyType.ToothChartUsesDiffColorByProv);
		}

		///<summary>Returns true if the currently logged in user has ColorChartUsesDiffByProv preference (ToothChartPainter.HasToothColorUserPref).</summary>
		public bool HasToothColorUserPref(){
			return ToothChartPainter.HasToothColorUserPref();
		}

		#endregion Colors

		#region Settings helpers (TC-2)
		///<summary>Applies the local computer's chart settings (computerpref: GraphicsUseHardware, PreferredPixelFormatNum, DirectXFormat and the
		///stored mode GraphicsSimple) to a wrapper and starts its engine. One helper for the copies each caller had before TC-2 (ControlChart twice,
		///ControlTreat, SheetPrinting, FormSheetDefEdit and FormToothChartingBig), with the same values; toothChartSettingsUse keeps the small
		///differences between them. The engine starts last, so it starts with the other settings. Except for StartupMode, the pixel format the
		///engine chose is then saved for the computer with the same ComputerPrefs.Update as before. In DEBUG builds the developer switch
		///(IsWpfDeveloperSwitchOn) starts the WPF chart instead.</summary>
		public static void ApplyComputerSettings(ToothChartWrapper toothChartWrapper,ToothChartSettingsUse toothChartSettingsUse) {
			if(toothChartSettingsUse!=ToothChartSettingsUse.StartupMode) {
				toothChartWrapper.UseHardware=ComputerPrefs.LocalComputer.GraphicsUseHardware;
				toothChartWrapper.PreferredPixelFormatNumber=ComputerPrefs.LocalComputer.PreferredPixelFormatNum;
			}
			if(IsWpfDeveloperSwitchOn()) {
				toothChartWrapper.Engine=ToothChartEngine.Wpf3D;
			}
			else if(toothChartSettingsUse==ToothChartSettingsUse.ModeByName) {
				//Must be last preference set, last so that all settings are caried through in the reinitialization this line triggers.
				if(ComputerPrefs.LocalComputer.GraphicsSimple==DrawingMode.Simple2D) {
					toothChartWrapper.DrawMode=DrawingMode.Simple2D;
				}
				else if(ComputerPrefs.LocalComputer.GraphicsSimple==DrawingMode.DirectX) {
					toothChartWrapper.DeviceFormat=new ToothChartDirectX.DirectXDeviceFormat(ComputerPrefs.LocalComputer.DirectXFormat);
					toothChartWrapper.DrawMode=DrawingMode.DirectX;
				}
				else{
					toothChartWrapper.DrawMode=DrawingMode.OpenGL;
				}
			}
			else {
				toothChartWrapper.DeviceFormat=new ToothChartDirectX.DirectXDeviceFormat(ComputerPrefs.LocalComputer.DirectXFormat);
				//Must be last setting set for preferences, because this is the line where the device pixel format is recreated.
				//When DirectX cannot start (no d3dx9_43.dll from the legacy DirectX runtime), the wrapper falls back to OpenGL, then Simple 2D.
				toothChartWrapper.DrawMode=ComputerPrefs.LocalComputer.GraphicsSimple;
			}
			if(toothChartSettingsUse==ToothChartSettingsUse.StartupMode) {
				return;
			}
			//The preferred pixel format number changes to the selected pixel format number after a context is chosen.
			ComputerPrefs.LocalComputer.PreferredPixelFormatNum=toothChartWrapper.PreferredPixelFormatNumber;
			ComputerPrefs.Update(ComputerPrefs.LocalComputer);
		}

		///<summary>At start-up (FormOpenDental): a computer saved as DirectX with no DirectX format yet gets the first standard format that works
		///(ToothChartWrapper.GetPreferredDirectXFormat, which can take minutes, controlHost hosting its test charts), or the next mode, OpenGL, when
		///none works, and the computer's settings are saved. Returns true when they were saved, so the caller restarts its chart
		///(controlChart.InitializeOnStartup). Any failure leaves them unsaved and returns false.</summary>
		public static bool ChooseStartupDirectXFormat(Control controlHost) {
			//Choose a default DirectX format when no DirectX format has been specified and running in DirectX tooth chart mode.
			if(ComputerPrefs.LocalComputer.GraphicsSimple==DrawingMode.DirectX && ComputerPrefs.LocalComputer.DirectXFormat=="") {
				try {
					ComputerPrefs.LocalComputer.DirectXFormat=ToothChartWrapper.GetPreferredDirectXFormat(controlHost);
					if(ComputerPrefs.LocalComputer.DirectXFormat=="invalid") {
						//No valid local DirectX format could be found, for example because d3dx9_43.dll from the legacy DirectX runtime is missing.
						//Use the wrapper's next mode, OpenGL; the wrapper itself falls back to Simple 2D if OpenGL cannot start either.
						ComputerPrefs.LocalComputer.GraphicsSimple=SparksToothChart.ToothChartWrapper.GetFallbackDrawMode(DrawingMode.DirectX);
					}
					ComputerPrefs.Update(ComputerPrefs.LocalComputer);
					return true;
				}
				catch(Exception) {
					//The tooth chart will default to Simple2D mode if the above code fails for any reason.  This will at least get the user into the program.
				}
			}
			return false;
		}

		///<summary>The gate of the graphical perio chart (FormPerio's Graphical button), moved from FormPerio: today it draws only in DirectX, which
		///must be chosen in Setup > Graphics and installed. Returns false with the message to show, in English (the caller shows it through MsgBox
		///as before, which translates it). In DEBUG builds the developer switch opens it on the WPF chart.</summary>
		public static bool CanOpenGraphicalPerio(out string message) {
			message="";
			if(IsWpfDeveloperSwitchOn()) {
				return true;
			}
			if(ComputerPrefs.LocalComputer.GraphicsSimple!=DrawingMode.DirectX) {
				message="In the Graphics setup window, you must first select DirectX.";
				return false;
			}
			if(!SparksToothChart.ToothChartWrapper.IsDirectXRuntimeAvailable()) {
				//The graphical perio chart draws only in DirectX, and DirectX needs d3dx9_43.dll, which a clean Windows does not have.
				message="The graphical perio chart needs Microsoft's DirectX End-User Runtime (d3dx9_43.dll), which is not installed on this computer.";
				return false;
			}
			return true;
		}

		///<summary>Starts the graphical perio chart's engine (FormPerioGraphical), moved from FormPerioGraphical: DirectX with the computer's DirectX
		///format, whatever chart the computer uses elsewhere. In DEBUG builds the developer switch starts the WPF chart, whose perio mode follows in
		///TC-7 (until then turning perio mode on throws, and the window says so and closes).</summary>
		public static void ApplyGraphicalPerioMode(ToothChartWrapper toothChartWrapper) {
			if(IsWpfDeveloperSwitchOn()) {
				toothChartWrapper.Engine=ToothChartEngine.Wpf3D;
				return;
			}
			toothChartWrapper.DeviceFormat=new ToothChartDirectX.DirectXDeviceFormat(ComputerPrefs.LocalComputer.DirectXFormat);//Must be set before draw mode
			//The perio chart draws only in DirectX. If DirectX cannot start, the wrapper falls back and IsPerioMode below says so.
			toothChartWrapper.DrawMode=DrawingMode.DirectX;
		}

		///<summary>The developer switch of the WPF chart (TC-2; TC-8 removes it): in DEBUG builds only, the environment variable OPMS_TOOTHCHART_WPF=1
		///makes ApplyComputerSettings, ApplyGraphicalPerioMode and CanOpenGraphicalPerio choose the WPF chart (ToothChartEngine.Wpf3D). A Release
		///build compiles no switch and always gets false.</summary>
		public static bool IsWpfDeveloperSwitchOn() {
#if DEBUG
			return Environment.GetEnvironmentVariable("OPMS_TOOTHCHART_WPF")=="1";
#else
			return false;
#endif
		}
		#endregion Settings helpers (TC-2)

		#region Free text (TC-2)
		///<summary>The wrapper's TextMoved, passed on: a free-text label (ToothInitialType.Text) was dragged with CursorTool.MoveText to a point in the
		///chart's 410×307 space. No engine raises it yet (TC-5 does); the Chart module saves the move (MoveTextLabel).</summary>
		public event ToothChartTextMovedEventHandler TextMoved;

		private void ToothChartWrapper_TextMoved(object sender,ToothChartTextMovedEventArgs e) {
			TextMoved?.Invoke(this,e);
		}

		///<summary>Moves a free-text label to location, a point in the chart's 410×307 space: clamped to 0..410 and 0..307 and rounded to whole
		///numbers, then written with ToothInitial.SetText, the label's text unchanged. Whole numbers matter: SetText writes the point in the
		///workstation's number format, and GetTextPoint reads only digits and a dot, so a decimal comma (de-DE) or a minus sign would move the
		///label to 0,0. The caller saves the label.</summary>
		public static void MoveTextLabel(ToothInitial toothInitial,PointF location) {
			float x=RoundToWhole(location.X,410);
			float y=RoundToWhole(location.Y,307);
			string text="";
			int indexSemicolon=(toothInitial.DrawText==null) ? -1 : toothInitial.DrawText.IndexOf(';');
			if(indexSemicolon>=0) {
				text=toothInitial.DrawText.Substring(indexSemicolon+1);//everything after the point, a semicolon in the text included
			}
			toothInitial.SetText(new PointF(x,y),text);
		}

		///<summary>value clamped to 0..max and rounded to a whole number (halves away from zero); 0 for a value that is not a number.</summary>
		private static float RoundToWhole(float value,int max) {
			if(float.IsNaN(value)) {
				return 0;
			}
			double clamped=Math.Min(Math.Max(value,0),max);
			return (float)Math.Round(clamped,MidpointRounding.AwayFromZero);
		}
		#endregion Free text (TC-2)
	}

	///<summary>How a caller applied the local computer's chart settings before TC-2 (ToothChartRelay.ApplyComputerSettings). The copies differed a
	///little, and each keeps its own way.</summary>
	public enum ToothChartSettingsUse {
		///<summary>Hardware flag and pixel format, the DirectX format, then the stored mode as it is; the chosen pixel format is saved
		///(ControlChart.InitializeLocalData, ControlTreat's printout, SheetPrinting's sheets and dashboard).</summary>
		StoredMode,
		///<summary>As StoredMode, but the mode is chosen by name: Simple 2D, DirectX with its format, otherwise OpenGL; the DirectX format is set
		///only for DirectX (FormSheetDefEdit's preview, FormToothChartingBig).</summary>
		ModeByName,
		///<summary>Only the DirectX format and the stored mode, and nothing is saved (ControlChart.InitializeOnStartup).</summary>
		StartupMode,
	}

	///<summary>The chart settings FormOpenDental compares after a refresh, to redraw the Chart module only when one of them changed
	///(NeedsRedraw("ChartModule")). Moved from FormOpenDental (TC-2) with the same keys and comparisons; FormOpenDental keeps its task-list
	///cache.</summary>
	public class ToothChartSettingsSnapshot {
		private Dictionary<string,object> _dictionaryChartPrefsCache=new Dictionary<string,object>();

		///<summary>Takes the settings as they are now, replacing the last snapshot.</summary>
		public void Take() {
			_dictionaryChartPrefsCache.Clear();
			//Chart Drawing Prefs
			_dictionaryChartPrefsCache.Add(PrefName.UseInternationalToothNumbers.ToString(),PrefC.GetInt(PrefName.UseInternationalToothNumbers));
			_dictionaryChartPrefsCache.Add("GraphicsUseHardware",ComputerPrefs.LocalComputer.GraphicsUseHardware);
			_dictionaryChartPrefsCache.Add("PreferredPixelFormatNum",ComputerPrefs.LocalComputer.PreferredPixelFormatNum);
			_dictionaryChartPrefsCache.Add("GraphicsSimple",ComputerPrefs.LocalComputer.GraphicsSimple);
			_dictionaryChartPrefsCache.Add(PrefName.ShowFeatureEhr.ToString(),PrefC.GetBool(PrefName.ShowFeatureEhr));
			_dictionaryChartPrefsCache.Add("DirectXFormat",ComputerPrefs.LocalComputer.DirectXFormat);
			_dictionaryChartPrefsCache.Add(PrefName.OrthoShowInChart.ToString(),PrefC.GetBool(PrefName.OrthoShowInChart));
		}

		///<summary>True when nothing was taken yet or a setting differs from the last snapshot. Throws when a setting cannot be read or compared (a
		///snapshot cut short by an error); FormOpenDental then redraws.</summary>
		public bool HasChanged() {
			return _dictionaryChartPrefsCache.Count==0
				|| PrefC.GetInt(PrefName.UseInternationalToothNumbers)!=(int)_dictionaryChartPrefsCache["UseInternationalToothNumbers"]
				|| ComputerPrefs.LocalComputer.GraphicsUseHardware!=(bool)_dictionaryChartPrefsCache["GraphicsUseHardware"]
				|| ComputerPrefs.LocalComputer.PreferredPixelFormatNum!=(int)_dictionaryChartPrefsCache["PreferredPixelFormatNum"]
				|| ComputerPrefs.LocalComputer.GraphicsSimple!=(DrawingMode)_dictionaryChartPrefsCache["GraphicsSimple"]
				|| PrefC.GetBool(PrefName.ShowFeatureEhr)!=(bool)_dictionaryChartPrefsCache["ShowFeatureEhr"]
				|| ComputerPrefs.LocalComputer.DirectXFormat!=(string)_dictionaryChartPrefsCache["DirectXFormat"]
				|| PrefC.GetBool(PrefName.OrthoShowInChart)!=(bool)_dictionaryChartPrefsCache["OrthoShowInChart"];
		}
	}

	///<summary>What ToothChartPainter draws on: the relay, which passes each call to the chart, or a test's recording target. Only the calls the
	///painter makes.</summary>
	public interface IToothChartPaintTarget {
		void AddDrawingSegment(ToothInitial drawingSegment);
		void AddOrthoElastic(string toothIDstart,string toothIDend,Color color);
		void AddOrthoWireBetweenBrackets(string toothIDstart,string toothIDend,Color color);
		void AddOrthoWireInBracket(string toothID,Color color);
		void AddText(string text,PointF location,Color color,long toothInitialNum);
		void MoveTooth(string toothID,float rotate,float tipM,float tipB,float shiftM,float shiftO,float shiftB);
		void SetBigX(string toothID,Color color);
		void SetBracket(string toothID,Color color);
		void SetBU(string toothID,Color color);
		void SetCrown(string toothID,Color color);
		void SetHidden(string toothID);
		void SetImplant(string toothID,Color color);
		void SetMissing(string toothID);
		void SetPontic(string toothID,Color color);
		void SetPrimary(string toothID);
		void SetRCT(string toothID,Color color);
		void SetRetainedRoot(string toothID,Color color);
		void SetSealant(string toothID,Color color);
		void SetSpaceMaintainer(string toothID,Color color);
		void SetSurfaceColors(string toothID,string surfaces,Color color);
		void SetText(string toothID,Color color,string text);
		void SetVeneer(string toothID,Color color);
	}

	///<summary>The painting code of the tooth chart's callers (TC-2): the procedures, the tooth initials and the orthodontic hardware, drawn on an
	///IToothChartPaintTarget. Moved from ControlChart, ControlTreat, SheetPrinting and FormToothChartingBig, so that the callers hold no chart code
	///and tests can log what each caller paints (tests/ToothChartImageTests, CallLogTests).</summary>
	public static class ToothChartPainter {
		#region Chart module (ControlChart)
		///<summary>ControlChart.FillToothChart: the progress-notes rows the chart draws for the slider date. listProcNumsTreatPlans is null unless
		///"Treatment Plans" is checked; then only completed work, conditions, referrals and the selected plans' procedures are drawn. Otherwise the
		///rows are those of listDataRowsProcsOrig (the procedures the progress notes show). Rows the slider date has not reached go to
		///listDataRowsProcsSkipped (ShouldRowShowGraphical).</summary>
		public static List<DataRow> GetChartModuleRows(DataTable table,List<long> listProcNumsTreatPlans,List<DataRow> listDataRowsProcsOrig,DateTime dateLimit,
			out List<DataRow> listDataRowsProcsSkipped)
		{
			List<DataRow> listDataRowsProcsForGraphical;
			if(listProcNumsTreatPlans!=null) {
				//filter list of DataRows to only include completed work and work for the selected treatment plans
				listDataRowsProcsForGraphical=new List<DataRow>();
				listDataRowsProcsSkipped=new List<DataRow>();
				for(int i=0;i<table.Rows.Count;i++) {
					//If proc status is anything except TP and TPi
					if(new[] { ProcStat.C,ProcStat.Cn,ProcStat.EC,ProcStat.EO,ProcStat.R }.Contains((ProcStat)PIn.Long(table.Rows[i]["ProcStatus"].ToString()))
						|| listProcNumsTreatPlans.Contains(PIn.Long(table.Rows[i]["ProcNum"].ToString())))
					{
						if(!ShouldRowShowGraphical(table.Rows[i],dateLimit,listDataRowsProcsSkipped)) {
							continue;
						}
						listDataRowsProcsForGraphical.Add(table.Rows[i]);
					}
				}
				return listDataRowsProcsForGraphical;
			}
			//put list back to the original list of DataRows
			List<DataRow> listDataRowsSkipped=new List<DataRow>();
			listDataRowsProcsSkipped=listDataRowsSkipped;//the lambda below cannot use the out parameter
			List<string> listProcNumStrsOrig=listDataRowsProcsOrig.Select(x => x["ProcNum"].ToString()).ToList();
			listDataRowsProcsForGraphical=table.Select().Where(x => listProcNumStrsOrig.Contains(x["ProcNum"].ToString())
				&& ShouldRowShowGraphical(x,dateLimit,listDataRowsSkipped)).ToList();
			return listDataRowsProcsForGraphical;
		}

		///<summary>ControlChart.DrawOrthoHardware: the hardware of the exam shown, hidden pieces only when "Show Hidden" is checked.</summary>
		public static List<OrthoHardware> GetChartModuleOrthoHardwares(List<OrthoHardware> listOrthoHardwares,DateTime dateExam,bool isShowHidden) {
			return listOrthoHardwares.FindAll(x=>x.DateExam==dateExam && (!x.IsHidden || isShowHidden));
		}


		///<summary>ControlChart.FillToothChart: the primary teeth, before the selection is restored and before the other tooth initials.</summary>
		public static void PaintChartModulePrimaryTeeth(IToothChartPaintTarget target,List<ToothInitial> listToothInitials) {
			//primary teeth need to be set before resetting selected teeth, because some of them might be primary.
			//primary teeth also need to be set before initial list so that we can set a primary tooth missing.
			for(int i=0;i<listToothInitials.Count;i++) {
				if(listToothInitials[i].InitialType==ToothInitialType.Primary) {
					target.SetPrimary(listToothInitials[i].ToothNum);
				}
			}
		}

		///<summary>ControlChart.FillToothChart: a copy of the tooth initials for the slider date. A missing tooth whose extraction the slider has not reached yet (skipped, or completed after the slider date and drawn as planned) is not missing.</summary>
		public static List<ToothInitial> GetChartModuleToothInitials(List<ToothInitial> listToothInitials,List<DataRow> listDataRowsProcsSkipped,
			List<DataRow> listDataRowsProcsForGraphical,DateTime dateLimit)
		{
			List<ToothInitial> listToothInitialsCopy=listToothInitials.Select(x => x.Copy()).ToList();
			for(int i=0;i<listDataRowsProcsSkipped.Count();i++) {
				ProcStat procStat=(ProcStat)PIn.Long(listDataRowsProcsSkipped[i]["ProcStatus"].ToString());
				if(!procStat.In(ProcStat.C,ProcStat.EO,ProcStat.EC)) {
					continue;
				}
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(listDataRowsProcsSkipped[i]["ProcCode"].ToString());
				if(procedureCode.PaintType!=ToothPaintingType.Extraction) {
					continue;
				}
				listToothInitialsCopy.RemoveAll(x => x.InitialType==ToothInitialType.Missing && x.ToothNum==listDataRowsProcsSkipped[i]["ToothNum"].ToString());
			}
			//Also remove any extractions for TP procs that were set to TP by the ShouldRowShowGraphical
			for(int i=0;i<listDataRowsProcsForGraphical.Count();i++) {
				DateTime dateTP;
				DateTime dateComplete;
				ProcStat procStat=(ProcStat)PIn.Int(listDataRowsProcsForGraphical[i]["ProcStatus"].ToString());
				if(!procStat.In(ProcStat.TP)) {
					continue;
				}
				if(!DateTime.TryParse(listDataRowsProcsForGraphical[i]["DateTP"].ToString(),out dateTP)) {
					continue;
				}
				if(!DateTime.TryParse(listDataRowsProcsForGraphical[i]["DateEntryC"].ToString(),out dateComplete)) {
					continue;
				}
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(listDataRowsProcsForGraphical[i]["ProcCode"].ToString());
				if(procedureCode.PaintType==ToothPaintingType.Extraction 
						&& dateLimit<dateComplete && dateLimit>=dateTP) 
				{//Procedure is C and the slider date is after or equal to the TP date, but before the completion date
					listToothInitialsCopy.RemoveAll(x => x.InitialType==ToothInitialType.Missing && x.ToothNum==listDataRowsProcsForGraphical[i]["ToothNum"].ToString());//Pretend the row is TP for the tooth chart
				}
			}
			return listToothInitialsCopy;
		}

		///<summary>ControlChart.FillToothChart: the tooth initials of the slider date, without the primary teeth.</summary>
		public static void PaintChartModuleToothInitials(IToothChartPaintTarget target,List<ToothInitial> listToothInitialsCopy) {
			for(int i=0;i<listToothInitialsCopy.Count;i++) {
				switch(listToothInitialsCopy[i].InitialType) {
					case ToothInitialType.Missing:
						target.SetMissing(listToothInitialsCopy[i].ToothNum);
						break;
					case ToothInitialType.Hidden:
						target.SetHidden(listToothInitialsCopy[i].ToothNum);
						break;
					//case ToothInitialType.Primary:
					//	break;
					case ToothInitialType.Rotate:
						target.MoveTooth(listToothInitialsCopy[i].ToothNum,listToothInitialsCopy[i].Movement,0,0,0,0,0);
						break;
					case ToothInitialType.TipM:
						target.MoveTooth(listToothInitialsCopy[i].ToothNum,0,listToothInitialsCopy[i].Movement,0,0,0,0);
						break;
					case ToothInitialType.TipB:
						target.MoveTooth(listToothInitialsCopy[i].ToothNum,0,0,listToothInitialsCopy[i].Movement,0,0,0);
						break;
					case ToothInitialType.ShiftM:
						target.MoveTooth(listToothInitialsCopy[i].ToothNum,0,0,0,listToothInitialsCopy[i].Movement,0,0);
						break;
					case ToothInitialType.ShiftO:
						target.MoveTooth(listToothInitialsCopy[i].ToothNum,0,0,0,0,listToothInitialsCopy[i].Movement,0);
						break;
					case ToothInitialType.ShiftB:
						target.MoveTooth(listToothInitialsCopy[i].ToothNum,0,0,0,0,0,listToothInitialsCopy[i].Movement);
						break;
					case ToothInitialType.Drawing:
						target.AddDrawingSegment(listToothInitialsCopy[i].Copy());
						break;
					case ToothInitialType.Text:
						target.AddText(listToothInitialsCopy[i].GetTextString(),listToothInitialsCopy[i].GetTextPoint(),listToothInitialsCopy[i].ColorDraw,listToothInitialsCopy[i].ToothInitialNum);
						break;
				}
			}
		}

		///<summary>ControlChart.DrawProcGraphics: the procedures of the progress notes. isPrinting turns the per-provider colours off.</summary>
		public static void PaintChartModuleProcs(IToothChartPaintTarget target,List<DataRow> listDataRowsProcsForGraphical,List<ToothInitial> listToothInitialsCopy,
			Patient patient,List<Appointment> listAppointments,bool isPrinting)
		{
			//this requires: ProcStatus, ProcCode, ToothNum, HideGraphics, Surf, and ToothRange.  All need to be raw database values.
			string[] stringArrayTeeth;
			List<long> listProvNums=GetPertinentProvNumsForToothColorPref(Security.CurUser,patient,listAppointments);
			for(int i=0;i<listDataRowsProcsForGraphical.Count;i++) {
				if(listDataRowsProcsForGraphical[i]["HideGraphics"].ToString()=="1") {
					continue;
				}
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(listDataRowsProcsForGraphical[i]["ProcCode"].ToString());
				if(procedureCode.PaintType==ToothPaintingType.None || procedureCode.TreatArea==TreatmentArea.Mouth) {
					continue;
				}
				if(procedureCode.PaintType==ToothPaintingType.Extraction && (
					PIn.Long(listDataRowsProcsForGraphical[i]["ProcStatus"].ToString())==(int)ProcStat.C
					|| PIn.Long(listDataRowsProcsForGraphical[i]["ProcStatus"].ToString())==(int)ProcStat.EC
					|| PIn.Long(listDataRowsProcsForGraphical[i]["ProcStatus"].ToString())==(int)ProcStat.EO
					)) {
					continue;//prevents the red X. Missing teeth already handled.
				}
				ProcStat procStat=(ProcStat)PIn.Long(listDataRowsProcsForGraphical[i]["ProcStatus"].ToString());
				long provNumForProc=PIn.Long(listDataRowsProcsForGraphical[i]["ProvNum"].ToString());
				bool applyColorPref=DoesToothColorPrefApply(listProvNums,provNumForProc) && !isPrinting;
				GetToothColors(procedureCode,procStat,applyColorPref,out Color colorDark,out Color colorLight);
				switch(procedureCode.PaintType) {
					case ToothPaintingType.BridgeDark:
						if(ToothInitials.ToothIsMissingOrHidden(listToothInitialsCopy,listDataRowsProcsForGraphical[i]["ToothNum"].ToString())){
							target.SetPontic(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						}
						else{
							target.SetCrown(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						}
						break;
					case ToothPaintingType.BridgeLight:
						if(ToothInitials.ToothIsMissingOrHidden(listToothInitialsCopy,listDataRowsProcsForGraphical[i]["ToothNum"].ToString())) {
							target.SetPontic(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorLight);
						}
						else {
							target.SetCrown(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorLight);
						}
						break;
					case ToothPaintingType.CrownDark:
						target.SetCrown(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.CrownLight:
						target.SetCrown(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorLight);
						break;
					case ToothPaintingType.DentureDark:
						if(listDataRowsProcsForGraphical[i]["Surf"].ToString()=="U") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+2).ToString();
							}
						}
						else if(listDataRowsProcsForGraphical[i]["Surf"].ToString()=="L") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+18).ToString();
							}
						}
						else {
							stringArrayTeeth=listDataRowsProcsForGraphical[i]["ToothRange"].ToString().Split(new char[] {','});
						}
						for(int t=0;t<stringArrayTeeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(listToothInitialsCopy,stringArrayTeeth[t])) {
								target.SetPontic(stringArrayTeeth[t],colorDark);
								continue;
							}
							target.SetCrown(stringArrayTeeth[t],colorDark);
						}
						break;
					case ToothPaintingType.DentureLight:
						if(listDataRowsProcsForGraphical[i]["Surf"].ToString()=="U") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+2).ToString();
							}
						}
						else if(listDataRowsProcsForGraphical[i]["Surf"].ToString()=="L") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+18).ToString();
							}
						}
						else {
							stringArrayTeeth=listDataRowsProcsForGraphical[i]["ToothRange"].ToString().Split(new char[] { ',' });
						}
						for(int t=0;t<stringArrayTeeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(listToothInitialsCopy,stringArrayTeeth[t])) {
								target.SetPontic(stringArrayTeeth[t],colorLight);
								continue;
							}
							target.SetCrown(stringArrayTeeth[t],colorLight);
						}
						break;
					case ToothPaintingType.Extraction:
						target.SetBigX(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.FillingDark:
						target.SetSurfaceColors(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),listDataRowsProcsForGraphical[i]["Surf"].ToString(),colorDark);
						break;
					case ToothPaintingType.FillingLight:
						target.SetSurfaceColors(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),listDataRowsProcsForGraphical[i]["Surf"].ToString(),colorLight);
						break;
					case ToothPaintingType.Implant:
						target.SetImplant(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.PostBU:
						target.SetBU(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.RCT:
						target.SetRCT(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.RetainedRoot:
						target.SetRetainedRoot(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.Sealant:
						target.SetSealant(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.SpaceMaintainer:
						if(procedureCode.TreatArea==TreatmentArea.Tooth && listDataRowsProcsForGraphical[i]["ToothNum"].ToString()!=""){
							target.SetSpaceMaintainer(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
							break;
						}
						if(procedureCode.TreatArea==TreatmentArea.ToothRange && listDataRowsProcsForGraphical[i]["ToothRange"].ToString()!="") {
							stringArrayTeeth=listDataRowsProcsForGraphical[i]["ToothRange"].ToString().Split(',');
							for(int t=0;t<stringArrayTeeth.Length;t++) {
								target.SetSpaceMaintainer(stringArrayTeeth[t],colorDark);
							}
							break;
						}
						if(procedureCode.AreaAlsoToothRange==true && listDataRowsProcsForGraphical[i]["ToothRange"].ToString()!="") {
							stringArrayTeeth=listDataRowsProcsForGraphical[i]["ToothRange"].ToString().Split(',');
							for(int t=0;t<stringArrayTeeth.Length;t++) {
								target.SetSpaceMaintainer(stringArrayTeeth[t],colorDark);
							}
							break;
						}
						if(procedureCode.TreatArea==TreatmentArea.Quad){
							stringArrayTeeth=new string[0];
							if(listDataRowsProcsForGraphical[i]["Surf"].ToString()=="UR") {
								stringArrayTeeth=new string[] {"4","5","6","7","8"};
							}
							if(listDataRowsProcsForGraphical[i]["Surf"].ToString()=="UL") {
								stringArrayTeeth=new string[] {"9","10","11","12","13"};
							}
							if(listDataRowsProcsForGraphical[i]["Surf"].ToString()=="LL") {
								stringArrayTeeth=new string[] {"20","21","22","23","24"};
							}
							if(listDataRowsProcsForGraphical[i]["Surf"].ToString()=="LR") {
								stringArrayTeeth=new string[] {"25","26","27","28","29"};
							}							
							for(int t=0;t<stringArrayTeeth.Length;t++) {//could still be length 0
								target.SetSpaceMaintainer(stringArrayTeeth[t],colorDark);
							}
						}
						break;
					case ToothPaintingType.Text:
						target.SetText(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark,procedureCode.PaintText);
						break;
					case ToothPaintingType.Veneer:
						target.SetVeneer(listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorLight);
						break;
				}
			}
		}

		///<summary>ControlChart.DrawOrthoHardware: the hardware of the exam shown, from GetChartModuleOrthoHardwares.</summary>
		public static void PaintChartModuleOrthoHardware(IToothChartPaintTarget target,List<OrthoHardware> listOrthoHardwares) {
			List<OrthoHardwareSpec> listOrthoHardwareSpecs=OrthoHardwareSpecs.GetDeepCopy();
			List<OrthoHardwares.OrthoWire> listOrthoWires=new List<OrthoHardwares.OrthoWire>();//also used for elastics
			for(int i=0;i<listOrthoHardwares.Count;i++){
				OrthoHardwareSpec orthoHardwareSpec=listOrthoHardwareSpecs.Find(x=>x.OrthoHardwareSpecNum==listOrthoHardwares[i].OrthoHardwareSpecNum);
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Bracket){
					target.SetBracket(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor);
				}
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Wire){
					listOrthoWires.AddRange(OrthoHardwares.GetWires(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor));
				}
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Elastic){
					listOrthoWires.AddRange(OrthoHardwares.GetElastics(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor));
				}
			}
			for(int i=0;i<listOrthoWires.Count;i++){
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.BetweenBrackets){
					target.AddOrthoWireBetweenBrackets(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.InBracket){
					target.AddOrthoWireInBracket(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.Elastic){
					target.AddOrthoElastic(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
			}
		}

		///<summary>Returns true if rowCur represents a valid procedure that should be shown on the tooth chart based on the current track bar date. Returns false if rowCur does not have a valid DateTP, DateEntryC, and ProcDate set. Will also return false if rowCur should not be drawn on the tooth chart (based on the track bar's currently selected date / status of rowCur). If the row needs to be skipped, it gets added to listDataRowsProcsSkipped. Moved from ControlChart.</summary>
		public static bool ShouldRowShowGraphical(DataRow dataRow,DateTime dateLimit,List<DataRow> listDataRowsProcsSkipped) {
			DateTime dateTP;
			DateTime dateComplete;
			DateTime dateScheduled;
			ProcStat procStat=(ProcStat)PIn.Int(dataRow["ProcStatus"].ToString());
			if(!DateTime.TryParse(dataRow["DateTP"].ToString(),out dateTP)) {
				return false;
			}
			if(!DateTime.TryParse(dataRow["DateEntryC"].ToString(),out dateComplete)) {
				return false;
			}
			if(!DateTime.TryParse(dataRow["aptDateTime"].ToString(),out dateScheduled)) {
				return false;
			}
			if(dateLimit<dateTP) {//slider date is before the TP date
				listDataRowsProcsSkipped.Add(dataRow);
				return false;//Skip the proc
			}
			if(!procStat.In(ProcStat.C,ProcStat.TP) && dateLimit<dateComplete) {//slider date is before the completion date and the procedure is not C or TP
				listDataRowsProcsSkipped.Add(dataRow);
				return false;//Skip the proc
			}
			if(procStat==ProcStat.C && dateLimit<dateComplete && dateLimit>=dateTP) {//Procedure is C and the slider date is after or equal to the TP date, but before the completion date
				dataRow["ProcStatus"]=POut.Int((int)ProcStat.TP);//Pretend the row is TP for the tooth chart
			}
			return true;
		}

		#endregion Chart module (ControlChart)

		#region Treatment Plan printout (ControlTreat)
		///<summary>ControlTreat.PrepImageForPrinting: the primary teeth, then the other tooth initials.</summary>
		public static void PaintTreatPlanToothInitials(IToothChartPaintTarget target,List<ToothInitial> listToothInitials) {
			//first, primary.  That way, you can still set a primary tooth missing afterwards.
			for(int i=0;i<listToothInitials.Count;i++) {
				if(listToothInitials[i].InitialType==ToothInitialType.Primary) {
					target.SetPrimary(listToothInitials[i].ToothNum);
				}
			}
			for(int i=0;i<listToothInitials.Count;i++) {
				switch(listToothInitials[i].InitialType) {
					case ToothInitialType.Missing:
						target.SetMissing(listToothInitials[i].ToothNum);
						break;
					case ToothInitialType.Hidden:
						target.SetHidden(listToothInitials[i].ToothNum);
						break;
					case ToothInitialType.Rotate:
						target.MoveTooth(listToothInitials[i].ToothNum,listToothInitials[i].Movement,0,0,0,0,0);
						break;
					case ToothInitialType.TipM:
						target.MoveTooth(listToothInitials[i].ToothNum,0,listToothInitials[i].Movement,0,0,0,0);
						break;
					case ToothInitialType.TipB:
						target.MoveTooth(listToothInitials[i].ToothNum,0,0,listToothInitials[i].Movement,0,0,0);
						break;
					case ToothInitialType.ShiftM:
						target.MoveTooth(listToothInitials[i].ToothNum,0,0,0,listToothInitials[i].Movement,0,0);
						break;
					case ToothInitialType.ShiftO:
						target.MoveTooth(listToothInitials[i].ToothNum,0,0,0,0,listToothInitials[i].Movement,0);
						break;
					case ToothInitialType.ShiftB:
						target.MoveTooth(listToothInitials[i].ToothNum,0,0,0,0,0,listToothInitials[i].Movement);
						break;
					case ToothInitialType.Drawing:
						target.AddDrawingSegment(listToothInitials[i].Copy());
						break;
					case ToothInitialType.Text:
						target.AddText(listToothInitials[i].GetTextString(), listToothInitials[i].GetTextPoint(), listToothInitials[i].ColorDraw, listToothInitials[i].ToothInitialNum);
						break;
				}
			}
		}

		///<summary>ControlTreat.DrawProcsGraphics: the printout's procedures, in its own colours (no planned-inactive exception, no per-provider colours) and without deleted or unknown statuses.</summary>
		public static void PaintTreatPlanProcs(IToothChartPaintTarget target,List<Procedure> listProceduresFiltered,List<ToothInitial> listToothInitials) {
			Procedure procedure;
			string[] stringArrayTeeth;
			System.Drawing.Color cLight=System.Drawing.Color.White;
			System.Drawing.Color cDark=System.Drawing.Color.White;
			List<Def> listDefs=Defs.GetDefsForCategory(DefCat.ChartGraphicColors,true);
			for(int i=0;i<listProceduresFiltered.Count;i++) {
				procedure=listProceduresFiltered[i];
				//if(proc.ProcStatus!=procStat) {
				//  continue;
				//}
				if(procedure.HideGraphics) {
					continue;
				}
				if(ProcedureCodes.GetProcCode(procedure.CodeNum).PaintType==ToothPaintingType.Extraction && (
					procedure.ProcStatus==ProcStat.C
					|| procedure.ProcStatus==ProcStat.EC
					|| procedure.ProcStatus==ProcStat.EO
					)) {
					continue;//prevents the red X. Missing teeth already handled.
				}
				if(ProcedureCodes.GetProcCode(procedure.CodeNum).GraphicColor==System.Drawing.Color.FromArgb(0)) {
					switch(procedure.ProcStatus) {
						case ProcStat.C:
							cDark=listDefs[1].ItemColor;
							cLight=listDefs[6].ItemColor;
							break;
						case ProcStat.TP:
							cDark=listDefs[0].ItemColor;
							cLight=listDefs[5].ItemColor;
							break;
						case ProcStat.EC:
							cDark=listDefs[2].ItemColor;
							cLight=listDefs[7].ItemColor;
							break;
						case ProcStat.EO:
							cDark=listDefs[3].ItemColor;
							cLight=listDefs[8].ItemColor;
							break;
						case ProcStat.R:
							cDark=listDefs[4].ItemColor;
							cLight=listDefs[9].ItemColor;
							break;
						case ProcStat.Cn:
							cDark=listDefs[16].ItemColor;
							cLight=listDefs[17].ItemColor;
							break;
						case ProcStat.D:  //Don't think this can ever happen, but skip anyway.
						default:
							continue;//Don't draw.
					}
				}
				else {
					cDark=ProcedureCodes.GetProcCode(procedure.CodeNum).GraphicColor;
					cLight=ProcedureCodes.GetProcCode(procedure.CodeNum).GraphicColor;
				}
				switch(ProcedureCodes.GetProcCode(procedure.CodeNum).PaintType) {
					case ToothPaintingType.BridgeDark:
						if(ToothInitials.ToothIsMissingOrHidden(listToothInitials,procedure.ToothNum)) {
							target.SetPontic(procedure.ToothNum,cDark);
						}
						else {
							target.SetCrown(procedure.ToothNum,cDark);
						}
						break;
					case ToothPaintingType.BridgeLight:
						if(ToothInitials.ToothIsMissingOrHidden(listToothInitials,procedure.ToothNum)) {
							target.SetPontic(procedure.ToothNum,cLight);
						}
						else {
							target.SetCrown(procedure.ToothNum,cLight);
						}
						break;
					case ToothPaintingType.CrownDark:
						target.SetCrown(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.CrownLight:
						target.SetCrown(procedure.ToothNum,cLight);
						break;
					case ToothPaintingType.DentureDark:
						if(procedure.Surf=="U") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+2).ToString();
							}
						}
						else if(procedure.Surf=="L") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+18).ToString();
							}
						}
						else {
							stringArrayTeeth=procedure.ToothRange.Split(new char[] { ',' });
						}
						for(int t=0;t<stringArrayTeeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(listToothInitials,stringArrayTeeth[t])) {
								target.SetPontic(stringArrayTeeth[t],cDark);
							}
							else {
								target.SetCrown(stringArrayTeeth[t],cDark);
							}
						}
						break;
					case ToothPaintingType.DentureLight:
						if(procedure.Surf=="U") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+2).ToString();
							}
						}
						else if(procedure.Surf=="L") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+18).ToString();
							}
						}
						else {
							stringArrayTeeth=procedure.ToothRange.Split(new char[] { ',' });
						}
						for(int t=0;t<stringArrayTeeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(listToothInitials,stringArrayTeeth[t])) {
								target.SetPontic(stringArrayTeeth[t],cLight);
							}
							else {
								target.SetCrown(stringArrayTeeth[t],cLight);
							}
						}
						break;
					case ToothPaintingType.Extraction:
						target.SetBigX(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.FillingDark:
						target.SetSurfaceColors(procedure.ToothNum,procedure.Surf,cDark);
						break;
					case ToothPaintingType.FillingLight:
						target.SetSurfaceColors(procedure.ToothNum,procedure.Surf,cLight);
						break;
					case ToothPaintingType.Implant:
						target.SetImplant(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.PostBU:
						target.SetBU(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.RCT:
						target.SetRCT(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.RetainedRoot:
						target.SetRetainedRoot(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.Sealant:
						target.SetSealant(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.SpaceMaintainer:
						if(ProcedureCodes.GetProcCode(procedure.CodeNum).TreatArea==TreatmentArea.Tooth && procedure.ToothNum!=""){
							target.SetSpaceMaintainer(procedure.ToothNum,cDark);
						}
						else if((ProcedureCodes.GetProcCode(procedure.CodeNum).TreatArea==TreatmentArea.ToothRange && procedure.ToothRange!="") 
							|| (ProcedureCodes.GetProcCode(procedure.CodeNum).AreaAlsoToothRange && procedure.ToothRange!=""))
						{
							stringArrayTeeth=procedure.ToothRange.Split(',');
							for(int t=0;t<stringArrayTeeth.Length;t++) {
								target.SetSpaceMaintainer(stringArrayTeeth[t],cDark);
							}
						}
						else if(ProcedureCodes.GetProcCode(procedure.CodeNum).TreatArea==TreatmentArea.Quad){
							stringArrayTeeth=new string[0];
							if(procedure.Surf=="UR") {
								stringArrayTeeth=new string[] {"4","5","6","7","8"};
							}
							if(procedure.Surf=="UL") {
								stringArrayTeeth=new string[] {"9","10","11","12","13"};
							}
							if(procedure.Surf=="LL") {
								stringArrayTeeth=new string[] {"20","21","22","23","24"};
							}
							if(procedure.Surf=="LR") {
								stringArrayTeeth=new string[] {"25","26","27","28","29"};
							}							
							for(int t=0;t<stringArrayTeeth.Length;t++) {//could still be length 0
								target.SetSpaceMaintainer(stringArrayTeeth[t],cDark);
							}
						}
						break;
					case ToothPaintingType.Text:
						target.SetText(procedure.ToothNum,cDark,ProcedureCodes.GetProcCode(procedure.CodeNum).PaintText);
						break;
					case ToothPaintingType.Veneer:
						target.SetVeneer(procedure.ToothNum,cLight);
						break;
				}
			}
		}

		///<summary>ControlTreat.DrawOrthoHardware: all of the patient's hardware.</summary>
		public static void PaintTreatPlanOrthoHardware(IToothChartPaintTarget target,List<OrthoHardware> listOrthoHardwares) {
			List<OrthoHardwareSpec> listOrthoHardwareSpecs=OrthoHardwareSpecs.GetDeepCopy();
			List<OrthoHardwares.OrthoWire> listOrthoWires=new List<OrthoHardwares.OrthoWire>();//also used for elastics
			for(int i=0;i<listOrthoHardwares.Count;i++){
				OrthoHardwareSpec orthoHardwareSpec=listOrthoHardwareSpecs.Find(x=>x.OrthoHardwareSpecNum==listOrthoHardwares[i].OrthoHardwareSpecNum);
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Bracket){
					target.SetBracket(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor);
				}
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Wire){
					listOrthoWires.AddRange(OrthoHardwares.GetWires(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor));
				}
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Elastic){
					listOrthoWires.AddRange(OrthoHardwares.GetElastics(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor));
				}
			}
			for(int i=0;i<listOrthoWires.Count;i++){
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.BetweenBrackets){
					target.AddOrthoWireBetweenBrackets(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.InBracket){
					target.AddOrthoWireInBracket(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.Elastic){
					target.AddOrthoElastic(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
			}
		}

		#endregion Treatment Plan printout (ControlTreat)

		#region Sheets and the Patient Dashboard (SheetPrinting)
		///<summary>SheetPrinting.GetToothChartHelper: the primary teeth, then the other tooth initials.</summary>
		public static void PaintSheetToothInitials(IToothChartPaintTarget target,List<ToothInitial> toothInitialList) {
			//first, primary.  That way, you can still set a primary tooth missing afterwards.
			for(int i=0;i<toothInitialList.Count;i++) {
				if(toothInitialList[i].InitialType==ToothInitialType.Primary) {
					target.SetPrimary(toothInitialList[i].ToothNum);
				}
			}
			for(int i=0;i<toothInitialList.Count;i++) {
				switch(toothInitialList[i].InitialType) {
					case ToothInitialType.Missing:
						target.SetMissing(toothInitialList[i].ToothNum);
						break;
					case ToothInitialType.Hidden:
						target.SetHidden(toothInitialList[i].ToothNum);
						break;
					case ToothInitialType.Rotate:
						target.MoveTooth(toothInitialList[i].ToothNum,toothInitialList[i].Movement,0,0,0,0,0);
						break;
					case ToothInitialType.TipM:
						target.MoveTooth(toothInitialList[i].ToothNum,0,toothInitialList[i].Movement,0,0,0,0);
						break;
					case ToothInitialType.TipB:
						target.MoveTooth(toothInitialList[i].ToothNum,0,0,toothInitialList[i].Movement,0,0,0);
						break;
					case ToothInitialType.ShiftM:
						target.MoveTooth(toothInitialList[i].ToothNum,0,0,0,toothInitialList[i].Movement,0,0);
						break;
					case ToothInitialType.ShiftO:
						target.MoveTooth(toothInitialList[i].ToothNum,0,0,0,0,toothInitialList[i].Movement,0);
						break;
					case ToothInitialType.ShiftB:
						target.MoveTooth(toothInitialList[i].ToothNum,0,0,0,0,0,toothInitialList[i].Movement);
						break;
					case ToothInitialType.Drawing:
						target.AddDrawingSegment(toothInitialList[i].Copy());
						break;
					case ToothInitialType.Text:
						target.AddText(toothInitialList[i].GetTextString(), toothInitialList[i].GetTextPoint(), toothInitialList[i].ColorDraw, toothInitialList[i].ToothInitialNum);
						break;
				}
			}
		}

		///<summary>SheetPrinting.DrawProcsGraphics: the procedures of a sheet or the Patient Dashboard. The per-provider colours apply only in the dashboard.</summary>
		public static void PaintSheetProcs(IToothChartPaintTarget target,List<Procedure> procList,List<ToothInitial> toothInitialList,bool isInPatientDashboard,
			Patient patCur=null,List<Appointment> listAppts=null)
		{
			Procedure proc;
			string[] teeth;
			List<long> listCurProvNums=GetPertinentProvNumsForToothColorPref(Security.CurUser,patCur,listAppts);
			for(int i=0;i<procList.Count;i++) {
				proc=procList[i];
				//if(proc.ProcStatus!=procStat) {
				//  continue;
				//}
				if(proc.HideGraphics) {
					continue;
				}
				if(ProcedureCodes.GetProcCode(proc.CodeNum).PaintType==ToothPaintingType.Extraction && (
					proc.ProcStatus==ProcStat.C
					|| proc.ProcStatus==ProcStat.EC
					|| proc.ProcStatus==ProcStat.EO
					)) {
					continue;//prevents the red X. Missing teeth already handled.
				}
				ProcedureCode procCode = ProcedureCodes.GetProcCode(proc.CodeNum);
				bool doApplyColorPref=isInPatientDashboard && DoesToothColorPrefApply(listCurProvNums,proc.ProvNum);
				GetToothColors(procCode,proc.ProcStatus,doApplyColorPref,out Color cDark,out Color cLight);
				switch(ProcedureCodes.GetProcCode(proc.CodeNum).PaintType) {
					case ToothPaintingType.BridgeDark:
						if(ToothInitials.ToothIsMissingOrHidden(toothInitialList,proc.ToothNum)) {
							target.SetPontic(proc.ToothNum,cDark);
						}
						else {
							target.SetCrown(proc.ToothNum,cDark);
						}
						break;
					case ToothPaintingType.BridgeLight:
						if(ToothInitials.ToothIsMissingOrHidden(toothInitialList,proc.ToothNum)) {
							target.SetPontic(proc.ToothNum,cLight);
						}
						else {
							target.SetCrown(proc.ToothNum,cLight);
						}
						break;
					case ToothPaintingType.CrownDark:
						target.SetCrown(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.CrownLight:
						target.SetCrown(proc.ToothNum,cLight);
						break;
					case ToothPaintingType.DentureDark:
						if(proc.Surf=="U") {
							teeth=new string[14];
							for(int t=0;t<14;t++) {
								teeth[t]=(t+2).ToString();
							}
						}
						else if(proc.Surf=="L") {
							teeth=new string[14];
							for(int t=0;t<14;t++) {
								teeth[t]=(t+18).ToString();
							}
						}
						else {
							teeth=proc.ToothRange.Split(new char[] { ',' });
						}
						for(int t=0;t<teeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(toothInitialList,teeth[t])) {
								target.SetPontic(teeth[t],cDark);
							}
							else {
								target.SetCrown(teeth[t],cDark);
							}
						}
						break;
					case ToothPaintingType.DentureLight:
						if(proc.Surf=="U") {
							teeth=new string[14];
							for(int t=0;t<14;t++) {
								teeth[t]=(t+2).ToString();
							}
						}
						else if(proc.Surf=="L") {
							teeth=new string[14];
							for(int t=0;t<14;t++) {
								teeth[t]=(t+18).ToString();
							}
						}
						else {
							teeth=proc.ToothRange.Split(new char[] { ',' });
						}
						for(int t=0;t<teeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(toothInitialList,teeth[t])) {
								target.SetPontic(teeth[t],cLight);
							}
							else {
								target.SetCrown(teeth[t],cLight);
							}
						}
						break;
					case ToothPaintingType.Extraction:
						target.SetBigX(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.FillingDark:
						target.SetSurfaceColors(proc.ToothNum,proc.Surf,cDark);
						break;
					case ToothPaintingType.FillingLight:
						target.SetSurfaceColors(proc.ToothNum,proc.Surf,cLight);
						break;
					case ToothPaintingType.Implant:
						target.SetImplant(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.PostBU:
						target.SetBU(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.RCT:
						target.SetRCT(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.RetainedRoot:
						target.SetRetainedRoot(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.Sealant:
						target.SetSealant(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.SpaceMaintainer:
						if(ProcedureCodes.GetProcCode(proc.CodeNum).TreatArea==TreatmentArea.Tooth && proc.ToothNum!=""){
							target.SetSpaceMaintainer(proc.ToothNum,cDark);
						}
						else if((ProcedureCodes.GetProcCode(proc.CodeNum).TreatArea==TreatmentArea.ToothRange && proc.ToothRange!="") 
							|| (ProcedureCodes.GetProcCode(proc.CodeNum).AreaAlsoToothRange && proc.ToothRange!=""))
						{
							teeth=proc.ToothRange.Split(',');
							for(int t=0;t<teeth.Length;t++) {
								target.SetSpaceMaintainer(teeth[t],cDark);
							}
						}
						else if(ProcedureCodes.GetProcCode(proc.CodeNum).TreatArea==TreatmentArea.Quad){
							teeth=new string[0];
							if(proc.Surf=="UR") {
								teeth=new string[] {"4","5","6","7","8"};
							}
							if(proc.Surf=="UL") {
								teeth=new string[] {"9","10","11","12","13"};
							}
							if(proc.Surf=="LL") {
								teeth=new string[] {"20","21","22","23","24"};
							}
							if(proc.Surf=="LR") {
								teeth=new string[] {"25","26","27","28","29"};
							}							
							for(int t=0;t<teeth.Length;t++) {//could still be length 0
								target.SetSpaceMaintainer(teeth[t],cDark);
							}
						}
						break;
					case ToothPaintingType.Text:
						target.SetText(proc.ToothNum,cDark,ProcedureCodes.GetProcCode(proc.CodeNum).PaintText);
						break;
					case ToothPaintingType.Veneer:
						target.SetVeneer(proc.ToothNum,cLight);
						break;
				}
			}
		}

		///<summary>SheetPrinting.DrawOrthoHardware: all of the patient's hardware.</summary>
		public static void PaintSheetOrthoHardware(IToothChartPaintTarget target,List<OrthoHardware> listOrthoHardwares) {
			List<OrthoHardwareSpec> listOrthoHardwareSpecs=OrthoHardwareSpecs.GetDeepCopy();
			List<OrthoHardwares.OrthoWire> listOrthoWires=new List<OrthoHardwares.OrthoWire>();//also used for elastics
			for(int i=0;i<listOrthoHardwares.Count;i++){
				OrthoHardwareSpec orthoHardwareSpec=listOrthoHardwareSpecs.Find(x=>x.OrthoHardwareSpecNum==listOrthoHardwares[i].OrthoHardwareSpecNum);
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Bracket){
					target.SetBracket(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor);
				}
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Wire){
					listOrthoWires.AddRange(OrthoHardwares.GetWires(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor));
				}
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Elastic){
					listOrthoWires.AddRange(OrthoHardwares.GetElastics(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor));
				}
			}
			for(int i=0;i<listOrthoWires.Count;i++){
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.BetweenBrackets){
					target.AddOrthoWireBetweenBrackets(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.InBracket){
					target.AddOrthoWireInBracket(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.Elastic){
					target.AddOrthoElastic(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
			}
		}

		#endregion Sheets and the Patient Dashboard (SheetPrinting)

		#region Big chart (FormToothChartingBig)
		///<summary>FormToothChartingBig.FillToothChart: the primary teeth, then the other tooth initials.</summary>
		public static void PaintBigChartToothInitials(IToothChartPaintTarget target,List<ToothInitial> listToothInitials) {
			//first, primary.  That way, you can still set a primary tooth missing afterwards.
			for(int i=0;i<listToothInitials.Count;i++) {
				if(listToothInitials[i].InitialType==ToothInitialType.Primary) {
					target.SetPrimary(listToothInitials[i].ToothNum);
				}
			}
			for(int i=0;i<listToothInitials.Count;i++) {
				switch(listToothInitials[i].InitialType) {
					case ToothInitialType.Missing:
						target.SetMissing(listToothInitials[i].ToothNum);
						break;
					case ToothInitialType.Hidden:
						target.SetHidden(listToothInitials[i].ToothNum);
						break;
					//case ToothInitialType.Primary:
					//	break;
					case ToothInitialType.Rotate:
						target.MoveTooth(listToothInitials[i].ToothNum,listToothInitials[i].Movement,0,0,0,0,0);
						break;
					case ToothInitialType.TipM:
						target.MoveTooth(listToothInitials[i].ToothNum,0,listToothInitials[i].Movement,0,0,0,0);
						break;
					case ToothInitialType.TipB:
						target.MoveTooth(listToothInitials[i].ToothNum,0,0,listToothInitials[i].Movement,0,0,0);
						break;
					case ToothInitialType.ShiftM:
						target.MoveTooth(listToothInitials[i].ToothNum,0,0,0,listToothInitials[i].Movement,0,0);
						break;
					case ToothInitialType.ShiftO:
						target.MoveTooth(listToothInitials[i].ToothNum,0,0,0,0,listToothInitials[i].Movement,0);
						break;
					case ToothInitialType.ShiftB:
						target.MoveTooth(listToothInitials[i].ToothNum,0,0,0,0,0,listToothInitials[i].Movement);
						break;
					case ToothInitialType.Drawing:
						target.AddDrawingSegment(listToothInitials[i].Copy());
						break;
					case ToothInitialType.Text:
						target.AddText(listToothInitials[i].GetTextString(), listToothInitials[i].GetTextPoint(), listToothInitials[i].ColorDraw, listToothInitials[i].ToothInitialNum);
						break;
				}
			}
		}

		///<summary>FormToothChartingBig.DrawProcGraphics: the rows the Chart module hands the big chart.</summary>
		public static void PaintBigChartProcs(IToothChartPaintTarget target,List<DataRow> listDataRowsProcs,List<ToothInitial> listToothInitials,Patient patient,
			List<Appointment> listAppointments)
		{
			//this requires: ProcStatus, ProcCode, ToothNum, HideGraphics, Surf, and ToothRange.  All need to be raw database values.
			string[] stringArrayTeeth;
			List<long> listProvNums=GetPertinentProvNumsForToothColorPref(Security.CurUser,patient,listAppointments);
			for(int i=0;i<listDataRowsProcs.Count;i++) {
				if(listDataRowsProcs[i]["HideGraphics"].ToString()=="1") {
					continue;
				}
				if(ProcedureCodes.GetProcCode(listDataRowsProcs[i]["ProcCode"].ToString()).PaintType==ToothPaintingType.Extraction && (
					PIn.Long(listDataRowsProcs[i]["ProcStatus"].ToString())==(int)ProcStat.C
					|| PIn.Long(listDataRowsProcs[i]["ProcStatus"].ToString())==(int)ProcStat.EC
					|| PIn.Long(listDataRowsProcs[i]["ProcStatus"].ToString())==(int)ProcStat.EO
					)) {
					continue;//prevents the red X. Missing teeth already handled.
				}
				ProcedureCode procedureCode = ProcedureCodes.GetProcCode(listDataRowsProcs[i]["ProcCode"].ToString());
				ProcStat procStat = (ProcStat)PIn.Long(listDataRowsProcs[i]["ProcStatus"].ToString());
				long provNum=PIn.Long(listDataRowsProcs[i]["ProvNum"].ToString());
				bool doApplyColorPref=DoesToothColorPrefApply(listProvNums,provNum);
				GetToothColors(procedureCode,procStat,doApplyColorPref,out Color cDark,out Color cLight);
				switch(ProcedureCodes.GetProcCode(listDataRowsProcs[i]["ProcCode"].ToString()).PaintType) {
					case ToothPaintingType.BridgeDark:
						if(ToothInitials.ToothIsMissingOrHidden(listToothInitials,listDataRowsProcs[i]["ToothNum"].ToString())) {
							target.SetPontic(listDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						}
						else {
							target.SetCrown(listDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						}
						break;
					case ToothPaintingType.BridgeLight:
						if(ToothInitials.ToothIsMissingOrHidden(listToothInitials,listDataRowsProcs[i]["ToothNum"].ToString())) {
							target.SetPontic(listDataRowsProcs[i]["ToothNum"].ToString(),cLight);
						}
						else {
							target.SetCrown(listDataRowsProcs[i]["ToothNum"].ToString(),cLight);
						}
						break;
					case ToothPaintingType.CrownDark:
						target.SetCrown(listDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.CrownLight:
						target.SetCrown(listDataRowsProcs[i]["ToothNum"].ToString(),cLight);
						break;
					case ToothPaintingType.DentureDark:
						if(listDataRowsProcs[i]["Surf"].ToString()=="U") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+2).ToString();
							}
						}
						else if(listDataRowsProcs[i]["Surf"].ToString()=="L") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+18).ToString();
							}
						}
						else {
							stringArrayTeeth=listDataRowsProcs[i]["ToothRange"].ToString().Split(new char[] { ',' });
						}
						for(int t=0;t<stringArrayTeeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(listToothInitials,stringArrayTeeth[t])) {
								target.SetPontic(stringArrayTeeth[t],cDark);
							}
							else {
								target.SetCrown(stringArrayTeeth[t],cDark);
							}
						}
						break;
					case ToothPaintingType.DentureLight:
						if(listDataRowsProcs[i]["Surf"].ToString()=="U") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+2).ToString();
							}
						}
						else if(listDataRowsProcs[i]["Surf"].ToString()=="L") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+18).ToString();
							}
						}
						else {
							stringArrayTeeth=listDataRowsProcs[i]["ToothRange"].ToString().Split(new char[] { ',' });
						}
						for(int t=0;t<stringArrayTeeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(listToothInitials,stringArrayTeeth[t])) {
								target.SetPontic(stringArrayTeeth[t],cLight);
							}
							else {
								target.SetCrown(stringArrayTeeth[t],cLight);
							}
						}
						break;
					case ToothPaintingType.Extraction:
						target.SetBigX(listDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.FillingDark:
						target.SetSurfaceColors(listDataRowsProcs[i]["ToothNum"].ToString(),listDataRowsProcs[i]["Surf"].ToString(),cDark);
						break;
					case ToothPaintingType.FillingLight:
						target.SetSurfaceColors(listDataRowsProcs[i]["ToothNum"].ToString(),listDataRowsProcs[i]["Surf"].ToString(),cLight);
						break;
					case ToothPaintingType.Implant:
						target.SetImplant(listDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.PostBU:
						target.SetBU(listDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.RCT:
						target.SetRCT(listDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.RetainedRoot:
						target.SetRetainedRoot(listDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.Sealant:
						target.SetSealant(listDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.SpaceMaintainer:
						if(procedureCode.TreatArea==TreatmentArea.Tooth && listDataRowsProcs[i]["ToothNum"].ToString()!=""){
							target.SetSpaceMaintainer(listDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						}
						else if((procedureCode.TreatArea==TreatmentArea.ToothRange && listDataRowsProcs[i]["ToothRange"].ToString()!="") 
							|| (procedureCode.AreaAlsoToothRange==true && listDataRowsProcs[i]["ToothRange"].ToString()!=""))
						{
							stringArrayTeeth=listDataRowsProcs[i]["ToothRange"].ToString().Split(',');
							for(int t=0;t<stringArrayTeeth.Length;t++) {
								target.SetSpaceMaintainer(stringArrayTeeth[t],cDark);
							}
						}
						else if(procedureCode.TreatArea==TreatmentArea.Quad){
							stringArrayTeeth=new string[0];
							if(listDataRowsProcs[i]["Surf"].ToString()=="UR") {
								stringArrayTeeth=new string[] {"4","5","6","7","8"};
							}
							if(listDataRowsProcs[i]["Surf"].ToString()=="UL") {
								stringArrayTeeth=new string[] {"9","10","11","12","13"};
							}
							if(listDataRowsProcs[i]["Surf"].ToString()=="LL") {
								stringArrayTeeth=new string[] {"20","21","22","23","24"};
							}
							if(listDataRowsProcs[i]["Surf"].ToString()=="LR") {
								stringArrayTeeth=new string[] {"25","26","27","28","29"};
							}							
							for(int t=0;t<stringArrayTeeth.Length;t++) {//could still be length 0
								target.SetSpaceMaintainer(stringArrayTeeth[t],cDark);
							}
						}
						break;
					case ToothPaintingType.Text:
						target.SetText(listDataRowsProcs[i]["ToothNum"].ToString(),cDark,ProcedureCodes.GetProcCode(listDataRowsProcs[i]["ProcCode"].ToString()).PaintText);
						break;
					case ToothPaintingType.Veneer:
						target.SetVeneer(listDataRowsProcs[i]["ToothNum"].ToString(),cLight);
						break;
				}
			}
		}

		///<summary>FormToothChartingBig.DrawOrthoHardware: the hardware the Chart module hands the big chart.</summary>
		public static void PaintBigChartOrthoHardware(IToothChartPaintTarget target,List<OrthoHardware> listOrthoHardwares) {
			List<OrthoHardwareSpec> listOrthoHardwareSpecs=OrthoHardwareSpecs.GetDeepCopy();
			List<OrthoHardwares.OrthoWire> listOrthoWires=new List<OrthoHardwares.OrthoWire>();//also used for elastics
			for(int i=0;i<listOrthoHardwares.Count;i++){
				OrthoHardwareSpec orthoHardwareSpec=listOrthoHardwareSpecs.Find(x=>x.OrthoHardwareSpecNum==listOrthoHardwares[i].OrthoHardwareSpecNum);
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Bracket){
					target.SetBracket(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor);
				}
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Wire){
					listOrthoWires.AddRange(OrthoHardwares.GetWires(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor));
				}
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Elastic){
					listOrthoWires.AddRange(OrthoHardwares.GetElastics(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor));
				}
			}
			for(int i=0;i<listOrthoWires.Count;i++){
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.BetweenBrackets){
					target.AddOrthoWireBetweenBrackets(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.InBracket){
					target.AddOrthoWireInBracket(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.Elastic){
					target.AddOrthoElastic(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
			}
		}
		#endregion Big chart (FormToothChartingBig)

		#region Colours (moved from ToothChartRelay, whose methods forward here)
		///<summary>Checks ProvNums only if the logged in user has 'ToothChartUsesDiffColorByProv' user pref on</summary>
		public static bool DoesToothColorPrefApply(List<long> listCurProvNums,long procProvNum) {
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
		public static List<long> GetPertinentProvNumsForToothColorPref(Userod user,Patient patCur,List<Appointment> listAppts) {
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
		public static bool GetToothColors(ProcedureCode curProc,ProcStat procStatus,bool doUseEcEoForCProcs,out Color cDark,out Color cLight) {
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

		///<summary>Returns true if the currently logged in user has ColorChartUsesDiffByProv preference</summary>
		public static bool HasToothColorUserPref(){
			return ToothChartRelay.GetListUserOdPrefs().Select(x => x.UserNum).Contains(Security.CurUser.UserNum);
		}
		#endregion Colours (moved from ToothChartRelay, whose methods forward here)
	}
}
