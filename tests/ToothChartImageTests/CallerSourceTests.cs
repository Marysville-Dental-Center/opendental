// Source tests of the tooth chart's callers (package TC-2, docs/specs/wpf-tooth-chart-plan.md, sections 4 and 7): the callers hold no chart
// code of their own any more. Their painting is ToothChartPainter's, in the order PainterCallers (the call-log drivers) follows; their chart
// settings are the relay's helpers; no program file outside the relay chooses an engine by its DrawingMode value or names the old engines'
// libraries and types, except FormGraphics until TC-8 and the licence texts for good; the WPF developer switch exists only in DEBUG builds.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;

namespace ToothChartImageTests {
	[TestClass]
	public class CallerSourceTests {
		private const string CONTROL_CHART=@"OpenPmsDental\Main Modules\ControlChart.cs";
		private const string CONTROL_TREAT=@"OpenPmsDental\Main Modules\ControlTreat.cs";
		private const string SHEET_PRINTING=@"OpenPmsDental\SheetFramework\SheetPrinting.cs";
		private const string FORM_BIG=@"OpenPmsDental\Forms\FormToothChartBig.cs";
		private const string FORM_SHEET_DEF_EDIT=@"OpenPmsDental\Forms\FormSheetDefEdit.cs";
		private const string FORM_PERIO=@"OpenPmsDental\Forms\FormPerio.cs";
		private const string FORM_PERIO_GRAPHICAL=@"OpenPmsDental\Forms\FormPerioGraphical.cs";
		private const string FORM_GRAPHICS=@"OpenPmsDental\Forms\FormGraphics.cs";
		private const string FORM_OPEN_DENTAL=@"OpenPmsDental\Main Modules\FormOpenDental.cs";
		private const string RELAY=@"WpfControlsOD\UI\ToothChart\ToothChartRelay.cs";
		///<summary>The licence window and its resources name SharpDX and Tao for good (their notices), until TC-9b removes those entries.</summary>
		private static readonly string[] _arrayLicenceFiles=new[] { @"OpenPmsDental\Forms\FormLicense.cs",@"OpenPmsDental\Properties\Resources.Designer.cs" };

		#region The painting code is the painter's
		[TestMethod]
		public void ChartModule_PaintsThroughThePainter_InTheDriversOrder() {
			string code=SourceText.ReadCode(CONTROL_CHART);
			string fill=SourceText.GetMethod(code,"private void FillToothChart(bool retainSelection,DateTime dateLimit) {");
			SourceText.AssertInOrder(fill,"ControlChart.FillToothChart",
				"_toothChartRelay.ResetTeeth();",
				"ToothChartPainter.PaintChartModulePrimaryTeeth(_toothChartRelay,Pd.ListToothInitials);",
				"_toothChartRelay.SetSelected(listTeethSelected[i],true);",
				"DataTable table=Pd.TableProgNotes.Copy();",
				"if(checkTreatPlans.Checked) {",
				"listProcNumsTreatPlans=gridTreatPlans.SelectedIndices.SelectMany(x => _listTreatPlans[x].ListProcTPs).Select(x => x.ProcNumOrig).ToList();",
				"_listDataRowsProcsForGraphical=ToothChartPainter.GetChartModuleRows(table,listProcNumsTreatPlans,_listDataRowsProcsOrig,dateLimit,",
				"out _listDataRowsProcsSkipped);",
				"_listToothInitialsCopy=ToothChartPainter.GetChartModuleToothInitials(Pd.ListToothInitials,_listDataRowsProcsSkipped,_listDataRowsProcsForGraphical,",
				"ToothChartPainter.PaintChartModuleToothInitials(_toothChartRelay,_listToothInitialsCopy);",
				"DrawProcGraphics();",
				"DrawOrthoHardware();",
				"_toothChartRelay.EndUpdate();");
			StringAssert.Contains(SourceText.GetMethod(code,"private void DrawProcGraphics(bool isPrinting=false) {"),
				"ToothChartPainter.PaintChartModuleProcs(_toothChartRelay,_listDataRowsProcsForGraphical,_listToothInitialsCopy,Pd.Patient,Pd.ListAppointments,isPrinting);");
			SourceText.AssertInOrder(SourceText.GetMethod(code,"private void DrawOrthoHardware(){"),"ControlChart.DrawOrthoHardware",
				"if(!checkOrthoGraphics.Checked){",
				"return;",
				"FillOrthoDateIfNeeded();",
				"ToothChartPainter.GetChartModuleOrthoHardwares(Pd.ListOrthoHardwares,comboOrthoDate.GetSelected<DateTime>(),",
				"checkShowHidden.Checked);",
				"ToothChartPainter.PaintChartModuleOrthoHardware(_toothChartRelay,listOrthoHardwares);");
			//The print recolour of Save to Images: printing colours, the picture, then the screen's colours again.
			SourceText.AssertInOrder(SourceText.GetMethod(code,"private void menuItemChartSave_Click(object sender,EventArgs e) {"),"menuItemChartSave_Click",
				"if(_toothChartRelay.HasToothColorUserPref()) {","DrawProcGraphics(true);","bitmapChart=_toothChartRelay.GetBitmap();","DrawProcGraphics(false);");
			Assert.IsFalse(code.Contains("bool ShouldRowShowGraphical("),"The slider's row rule moved to the painter.");
		}

		[TestMethod]
		public void TreatPlan_PaintsThroughThePainter_InTheDriversOrder() {
			string code=SourceText.ReadCode(CONTROL_TREAT);
			SourceText.AssertInOrder(SourceText.GetMethod(code,"private void PrepImageForPrinting(){"),"ControlTreat.PrepImageForPrinting",
				"ToothChartRelay.ApplyComputerSettings(_toothChartWrapper,ToothChartSettingsUse.StoredMode);",
				"_toothChartRelay.ResetTeeth();",
				"_listToothInitials=ToothInitials.GetPatientData(PatientCur.PatNum);",
				"ToothChartPainter.PaintTreatPlanToothInitials(_toothChartRelay,_listToothInitials);",
				"ComputeProcListFiltered();",
				"ToothChartPainter.PaintTreatPlanProcs(_toothChartRelay,_listProceduresFiltered,_listToothInitials);",
				"ToothChartPainter.PaintTreatPlanOrthoHardware(_toothChartRelay,OrthoHardwares.GetPatientData(PatientCur.PatNum));",
				"_toothChartRelay.EndUpdate();");
		}

		[TestMethod]
		public void Sheet_PaintsThroughThePainter_InTheDriversOrder() {
			string code=SourceText.ReadCode(SHEET_PRINTING);
			SourceText.AssertInOrder(SourceText.GetMethod(code,"public static Image GetToothChartHelper("),"SheetPrinting.GetToothChartHelper",
				"ToothChartRelay.ApplyComputerSettings(toothChartWrapper,ToothChartSettingsUse.StoredMode);",
				"toothChartRelay.ResetTeeth();",
				"List<ToothInitial> toothInitialList=patNum==0?new List<ToothInitial>():ToothInitials.GetPatientData(patNum);",
				"ToothChartPainter.PaintSheetToothInitials(toothChartRelay,toothInitialList);",
				"if(listProceduresFilteredOverride!=null) {",
				"listProceduresFiltered=listProceduresFilteredOverride;",
				"else if(patNum>0) {",
				"List<Procedure> listProceduresAll=Procedures.Refresh(patNum);",
				"listProceduresFiltered=OpenPmsDentalBusiness.SheetPrinting.FilterProceduresForToothChart(listProceduresAll,treatPlan,showCompleted);",
				"listProceduresFiltered.Sort(OpenPmsDentalBusiness.SheetPrinting.CompareProcListFiltered);",
				"ToothChartPainter.PaintSheetProcs(toothChartRelay,listProceduresFiltered,toothInitialList,isInPatientDashboard,patCur,listAppts);",
				"List<OrthoHardware> listOrthoHardwares=OrthoHardwares.GetPatientData(patNum);",
				"ToothChartPainter.PaintSheetOrthoHardware(toothChartRelay,listOrthoHardwares);",
				"toothChartRelay.EndUpdate();");
		}

		[TestMethod]
		public void BigChart_PaintsThroughThePainter_InTheDriversOrder() {
			string code=SourceText.ReadCode(FORM_BIG);
			StringAssert.Contains(SourceText.GetMethod(code,"private void FormToothChartingBig_Load(object sender,EventArgs e) {"),
				"ToothChartRelay.ApplyComputerSettings(toothChartWrapper,ToothChartSettingsUse.ModeByName);");
			SourceText.AssertInOrder(SourceText.GetMethod(code,"private void FillToothChart(){"),"FormToothChartingBig.FillToothChart",
				"_toothChartRelay.ResetTeeth();",
				"_toothChartRelay.SetSelected(listSelectedTeeth[i],true);",
				"ToothChartPainter.PaintBigChartToothInitials(_toothChartRelay,ListToothInitials);",
				"ToothChartPainter.PaintBigChartProcs(_toothChartRelay,ListDataRowsProcs,ListToothInitials,PatientCur,ListAppointments);",
				"ToothChartPainter.PaintBigChartOrthoHardware(_toothChartRelay,ListOrthoHardwares);",
				"_toothChartRelay.EndUpdate();");
		}

		[TestMethod]
		public void Callers_HaveNoPaintSwitchesOrToothInitialLoopsOrHardwareLoops() {
			foreach(string relative in new[] { CONTROL_CHART,CONTROL_TREAT,SHEET_PRINTING,FORM_BIG }) {
				string code=SourceText.ReadCode(relative);
				StringAssert.DoesNotMatch(code,new Regex(@"case\s+ToothPaintingType\."),relative+" paints procedures itself.");
				StringAssert.DoesNotMatch(code,new Regex(@"case\s+ToothInitialType\.(Missing|Rotate|Drawing|Text)\s*:"),relative+" draws tooth initials itself.");
				StringAssert.DoesNotMatch(code,new Regex(@"OrthoHardwares\.Get(Wires|Elastics)\("),relative+" draws ortho hardware itself.");
				StringAssert.DoesNotMatch(code,new Regex(@"\.GetToothColors\("),relative+" picks procedure colours itself.");
			}
		}

		[TestMethod]
		public void Relay_IsThePaintersTarget() {
			Assert.IsTrue(typeof(IToothChartPaintTarget).IsAssignableFrom(typeof(ToothChartRelay)));
		}
		#endregion The painting code is the painter's

		#region Settings and engines
		[TestMethod]
		public void EightSettingsSites_CallTheRelayOrTheWrapper() {
			//Six copies of the computer's chart settings, the graphical perio chart's forced DirectX and Setup > Graphics' format test.
			Dictionary<string,string[]> dictSites=new Dictionary<string,string[]> {
				{ CONTROL_CHART,new[] { "ToothChartRelay.ApplyComputerSettings(toothChartWrapper,ToothChartSettingsUse.StoredMode);",
					"ToothChartRelay.ApplyComputerSettings(toothChartWrapper,ToothChartSettingsUse.StartupMode);" } },
				{ CONTROL_TREAT,new[] { "ToothChartRelay.ApplyComputerSettings(_toothChartWrapper,ToothChartSettingsUse.StoredMode);" } },
				{ SHEET_PRINTING,new[] { "ToothChartRelay.ApplyComputerSettings(toothChartWrapper,ToothChartSettingsUse.StoredMode);" } },
				{ FORM_SHEET_DEF_EDIT,new[] { "ToothChartRelay.ApplyComputerSettings(toothChartWrapper,ToothChartSettingsUse.ModeByName);" } },
				{ FORM_BIG,new[] { "ToothChartRelay.ApplyComputerSettings(toothChartWrapper,ToothChartSettingsUse.ModeByName);" } },
				{ FORM_PERIO_GRAPHICAL,new[] { "ToothChartRelay.ApplyGraphicalPerioMode(toothChartWrapper);" } },
				{ FORM_GRAPHICS,new[] { "ToothChartWrapper.TestDirectXFormat(this,_directXFormatSelected)" } },
			};
			int countSites=0;
			foreach(KeyValuePair<string,string[]> pair in dictSites) {
				string code=SourceText.ReadCode(pair.Key);
				foreach(string call in pair.Value) {
					Assert.AreEqual(1,Regex.Matches(code,Regex.Escape(call)).Count,pair.Key+": "+call);
					countSites++;
				}
			}
			Assert.AreEqual(8,countSites);
			int countCallsInProgram=SourceText.GetCsFiles("OpenPmsDental").Sum(x => Regex.Matches(SourceText.ReadCode(x),
				@"ToothChartRelay\.ApplyComputerSettings\(|ToothChartRelay\.ApplyGraphicalPerioMode\(|ToothChartWrapper\.TestDirectXFormat\(").Count);
			Assert.AreEqual(8,countCallsInProgram,"No other program file applies chart settings.");
			//Nothing else in the program sets a chart's engine or its settings: only the wrapper's designer defaults remain.
			Regex regexSet=new Regex(@"toothChartWrapper\w*\.(DrawMode|DeviceFormat|UseHardware|PreferredPixelFormatNumber|Engine)\s*=(?!=)\s*(?<value>[^;]*);",
				RegexOptions.IgnoreCase);
			string[] arrayDesignerDefaults=new[] { "null","OpenPmsDentalBusiness.DrawingMode.Simple2D","0","false" };
			List<string> listProblems=new List<string>();
			foreach(string relative in SourceText.GetCsFiles("OpenPmsDental")) {
				if(relative.EndsWith(".designer.cs",StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				foreach(Match match in regexSet.Matches(SourceText.ReadCode(relative))) {
					if(!arrayDesignerDefaults.Contains(match.Groups["value"].Value.Trim())) {
						listProblems.Add(relative+": "+match.Value);
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,"Chart settings set outside the relay: "+string.Join("; ",listProblems));
		}

		[TestMethod]
		public void Program_ChoosesNoEngineByItsDrawingModeValue_ExceptFormGraphicsUntilTC8() {
			Regex regex=new Regex(@"[!=]=\s*\(?\s*(OpenPmsDentalBusiness\.)?DrawingMode\b|\bDrawingMode\.\w+\s*[!=]=|\bcase\s+(OpenPmsDentalBusiness\.)?DrawingMode\.|switch\s*\([^)]*GraphicsSimple");
			List<string> listProblems=new List<string>();
			foreach(string relative in SourceText.GetCsFiles("OpenPmsDental")) {
				if(relative.Equals(FORM_GRAPHICS,StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				foreach(Match match in regex.Matches(SourceText.ReadCode(relative))) {
					listProblems.Add(relative+": "+match.Value);
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
			//The checks moved into the relay's helpers, where TC-8 changes them.
			string relay=SourceText.ReadCode(RELAY);
			StringAssert.Contains(relay,"if(ComputerPrefs.LocalComputer.GraphicsSimple==DrawingMode.DirectX && ComputerPrefs.LocalComputer.DirectXFormat==\"\") {");
			StringAssert.Contains(relay,"if(ComputerPrefs.LocalComputer.GraphicsSimple!=DrawingMode.DirectX) {");
			StringAssert.Contains(relay,"|| ComputerPrefs.LocalComputer.GraphicsSimple!=(DrawingMode)_dictionaryChartPrefsCache[\"GraphicsSimple\"]");
		}

		[TestMethod]
		public void Program_NamesNoOldEngineLibraryOrType_ExceptFormGraphicsUntilTC8AndTheLicenceTexts() {
			Regex regex=new Regex(@"\bSharpDX\b|\bTao\b|\bToothChartDirectX\b|\bToothChartOpenGL\b|\bOpenGLWinFormsControl\b");
			List<string> listAllowed=new List<string>(_arrayLicenceFiles) { FORM_GRAPHICS };
			List<string> listProblems=new List<string>();
			foreach(string relative in SourceText.GetCsFiles("OpenPmsDental")) {
				if(listAllowed.Contains(relative,StringComparer.OrdinalIgnoreCase)) {
					continue;
				}
				foreach(Match match in regex.Matches(SourceText.ReadCode(relative))) {
					listProblems.Add(relative+": "+match.Value);
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
			string chart=SourceText.Read(CONTROL_CHART);
			StringAssert.Contains(chart,"catch(Exception ex) when(ToothChartWrapper.IsGraphicsDeviceException(ex)) {");
			StringAssert.DoesNotMatch(chart,new Regex(@"#\s*if\s+EHRTEST|using\s+EHR\s*;"),"The dead EHRTEST block is gone.");
		}

		[TestMethod]
		public void DeveloperSwitch_IsCompiledOnlyInDebugBuilds() {
			List<string> listFound=new List<string>();
			foreach(string folder in new[] { "OpenPmsDental","WpfControlsOD","SparksToothChart","OpenPmsDentalBusiness" }) {
				foreach(string relative in SourceText.GetCsFiles(folder)) {
					string code=SourceText.ReadCode(relative);
					int index=code.IndexOf("OPMS_TOOTHCHART_WPF",StringComparison.Ordinal);
					while(index>=0) {
						listFound.Add(relative);
						Assert.AreEqual("#if DEBUG",GetOpenDirective(code,index),relative+": OPMS_TOOTHCHART_WPF outside #if DEBUG.");
						index=code.IndexOf("OPMS_TOOTHCHART_WPF",index+1,StringComparison.Ordinal);
					}
				}
			}
			CollectionAssert.AreEqual(new[] { RELAY },listFound.ToArray(),"Read once, by the relay's IsWpfDeveloperSwitchOn.");
			string relay=SourceText.ReadCode(RELAY);
			SourceText.AssertInOrder(SourceText.GetMethod(relay,"public static bool IsWpfDeveloperSwitchOn() {"),"IsWpfDeveloperSwitchOn",
				"#if DEBUG","return Environment.GetEnvironmentVariable(\"OPMS_TOOTHCHART_WPF\")==\"1\";","#else","return false;","#endif");
			//The settings helpers and the perio helpers honour it.
			foreach(string signature in new[] { "public static void ApplyComputerSettings(","public static bool CanOpenGraphicalPerio(",
				"public static void ApplyGraphicalPerioMode(" })
			{
				StringAssert.Contains(SourceText.GetMethod(relay,signature),"IsWpfDeveloperSwitchOn()",signature);
			}
		}

		///<summary>The preprocessor block the text at index is in: the last #if, #else or #elif line before it that its #endif has not closed, or "".</summary>
		private static string GetOpenDirective(string code,int index) {
			Stack<string> stack=new Stack<string>();
			foreach(Match match in new Regex(@"^[ \t]*#(if|elif|else|endif)\b[^\n]*",RegexOptions.Multiline).Matches(code.Substring(0,index))) {
				string directive=match.Value.Trim();
				if(directive.StartsWith("#if")) {
					stack.Push(directive);
				}
				else if(directive.StartsWith("#endif")) {
					stack.Pop();
				}
				else {
					stack.Pop();
					stack.Push(directive);
				}
			}
			return stack.Count==0 ? "" : stack.Peek();
		}

		[TestMethod]
		public void MainWindow_UsesTheRelaysStartupSearchAndSnapshot() {
			string code=SourceText.ReadCode(FORM_OPEN_DENTAL);
			SourceText.AssertInOrder(code,"FormOpenDental start-up","if(ToothChartRelay.ChooseStartupDirectXFormat(this)) {","try {",
				"controlChart.InitializeOnStartup();","catch(Exception) {");
			StringAssert.Contains(code,"private ToothChartSettingsSnapshot _toothChartSettingsSnapshot=new ToothChartSettingsSnapshot();");
			SourceText.AssertInOrder(code,"FormOpenDental refresh","_dictionaryTaskListPrefsCache.Clear();","_toothChartSettingsSnapshot.Take();",
				"_dictionaryTaskListPrefsCache.Add(\"TaskDock\"");
			SourceText.AssertInOrder(SourceText.GetMethod(code,"private bool NeedsRedraw(string section) {"),"NeedsRedraw",
				"case \"ChartModule\":","if(_toothChartSettingsSnapshot.HasChanged()) {","return true;","case \"TaskLists\":","catch {","return true;");
			Assert.IsFalse(code.Contains("_dictionaryChartPrefsCache"));
			StringAssert.Contains(SourceText.ReadCode(FORM_PERIO),"if(!ToothChartRelay.CanOpenGraphicalPerio(out string message)) {");
		}
		#endregion Settings and engines
	}
}
