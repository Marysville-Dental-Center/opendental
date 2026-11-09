// TEMPORARY (package TC-2): the chart code of the four callers as it was before TC-2's commit (1), so that their call logs could be recorded
// from today's code. Every block between a "HARNESS-BEGIN <file>" and a "HARNESS-END" line is the program's code copied line for line (TC-2's
// commit (0) checked each block against its program file with a source test, which commit (1) deleted with the code it moved); around the
// blocks are only the fields the copied code reads, named as in the callers, and the stand-ins for the controls it reads (check boxes, the
// plans grid, the exam combo).
// The copied code runs against the ToothChartRelay below, which logs each draw call and asks the real relay for the colours. TC-2 deletes
// this file after commit (2): the logs it recorded stay in CallLogs\ and every painter is compared with them.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using CodeBase;
using OpenPmsDentalBusiness;

namespace ToothChartImageTests.Harness {
	///<summary>Stands in for OpenPmsDental.ToothChartRelay under the same name, so the copied code compiles unchanged. Draw calls go to the log;
	///the colour questions go to a real relay, which has no chart.</summary>
	public class ToothChartRelay {
		private readonly CallLog _callLog;
		private readonly OpenPmsDental.ToothChartRelay _toothChartRelayColors=new OpenPmsDental.ToothChartRelay();

		public ToothChartRelay(CallLog callLog) {
			_callLog=callLog;
		}

		public bool DoesToothColorPrefApply(List<long> listCurProvNums,long procProvNum) {
			return _toothChartRelayColors.DoesToothColorPrefApply(listCurProvNums,procProvNum);
		}

		public List<long> GetPertinentProvNumsForToothColorPref(Userod user,Patient patCur,List<Appointment> listAppts) {
			return _toothChartRelayColors.GetPertinentProvNumsForToothColorPref(user,patCur,listAppts);
		}

		public bool GetToothColors(ProcedureCode curProc,ProcStat procStatus,bool doUseEcEoForCProcs,out Color cDark,out Color cLight) {
			return _toothChartRelayColors.GetToothColors(curProc,procStatus,doUseEcEoForCProcs,out cDark,out cLight);
		}

		///<summary>Not a draw call of the painting code: the selection is the callers' own and is not logged.</summary>
		public void SetSelected(string toothID,bool setValue) {
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

	///<summary>Stands in for a check box the copied code reads.</summary>
	public class LegacyCheckBox {
		public bool Checked;
	}

	///<summary>Stands in for the Chart module's plans grid: only its selected rows are read.</summary>
	public class LegacyGrid {
		public int[] SelectedIndices=new int[0];
	}

	///<summary>Stands in for the Chart module's ortho exam date combo.</summary>
	public class LegacyComboDate {
		public DateTime DateSelected;

		public T GetSelected<T>() {
			return (T)(object)DateSelected;
		}
	}

	///<summary>Stands in for the Chart module's PatientData (Pd): the members the copied code reads.</summary>
	public class LegacyPatientData {
		public Patient Patient;
		public List<Appointment> ListAppointments;
		public List<ToothInitial> ListToothInitials;
		public DataTable TableProgNotes;
		public List<OrthoHardware> ListOrthoHardwares;
	}

	///<summary>ControlChart's chart code: FillToothChart(bool,DateTime) from the primary teeth to the ortho hardware, DrawProcGraphics,
	///DrawOrthoHardware and ShouldRowShowGraphical.</summary>
	public class LegacyControlChart {
		private ToothChartRelay _toothChartRelay;
		private LegacyPatientData Pd=new LegacyPatientData();
		private LegacyCheckBox checkShowTeeth=new LegacyCheckBox();
		private LegacyCheckBox checkTreatPlans=new LegacyCheckBox();
		private LegacyCheckBox checkOrthoGraphics=new LegacyCheckBox();
		private LegacyCheckBox checkShowHidden=new LegacyCheckBox();
		private LegacyGrid gridTreatPlans=new LegacyGrid();
		private List<TreatPlan> _listTreatPlans=new List<TreatPlan>();
		private LegacyComboDate comboOrthoDate=new LegacyComboDate();
		private List<DataRow> _listDataRowsProcsForGraphical;
		private List<DataRow> _listDataRowsProcsSkipped;
		private List<DataRow> _listDataRowsProcsOrig;
		private List<ToothInitial> _listToothInitialsCopy;

		///<summary>Runs one scene: the refill after the reset, then, for IsPrintRecolor, the print recolour of menuItemChartSave.</summary>
		public static CallLog Run(ChartModuleScene scene) {
			CallLog callLog=new CallLog();
			LegacyControlChart legacyControlChart=new LegacyControlChart();
			legacyControlChart._toothChartRelay=new ToothChartRelay(callLog);
			legacyControlChart.Pd.Patient=CallLogFixtures.GetPatient(scene.PatNum);
			legacyControlChart.Pd.ListAppointments=CallLogFixtures.GetAppointments(scene.PatNum,scene.HasTodaysAppointment);
			legacyControlChart.Pd.ListToothInitials=ToothInitials.GetPatientData(scene.PatNum);
			legacyControlChart.Pd.TableProgNotes=CallLogFixtures.GetProgNotes(scene.PatNum);
			legacyControlChart.Pd.ListOrthoHardwares=OrthoHardwares.GetPatientData(scene.PatNum);
			legacyControlChart._listDataRowsProcsOrig=CallLogFixtures.GetProcRowsOrig(legacyControlChart.Pd.TableProgNotes);
			if(scene.FuncProcNumsTreatPlans!=null) {
				legacyControlChart.checkTreatPlans.Checked=true;
				TreatPlan treatPlan=new TreatPlan { ListProcTPs=scene.FuncProcNumsTreatPlans().Select(x => new ProcTP { ProcNumOrig=x }).ToList() };
				legacyControlChart._listTreatPlans.Add(treatPlan);
				legacyControlChart.gridTreatPlans.SelectedIndices=new[] { 0 };
			}
			legacyControlChart.checkOrthoGraphics.Checked=scene.IsOrthoGraphics;
			legacyControlChart.checkShowHidden.Checked=scene.IsShowHidden;
			legacyControlChart.comboOrthoDate.DateSelected=scene.DateOrthoExam;
			legacyControlChart.FillToothChartAfterReset(false,scene.DateLimit,new List<string>());
			if(scene.IsPrintRecolor) {
				legacyControlChart.DrawProcGraphics(true);
				legacyControlChart.DrawProcGraphics(false);
			}
			return callLog;
		}

		private void FillOrthoDateIfNeeded() {
		}

		private void FillToothChartAfterReset(bool retainSelection,DateTime dateLimit,List<string> listTeethSelected) {
			//HARNESS-BEGIN OpenPmsDental\Main Modules\ControlChart.cs
			//primary teeth need to be set before resetting selected teeth, because some of them might be primary.
			//primary teeth also need to be set before initial list so that we can set a primary tooth missing.
			for(int i=0;i<Pd.ListToothInitials.Count;i++) {
				if(Pd.ListToothInitials[i].InitialType==ToothInitialType.Primary) {
					_toothChartRelay.SetPrimary(Pd.ListToothInitials[i].ToothNum);
				}
			}
			if(checkShowTeeth.Checked || retainSelection) {
				for(int i=0;i<listTeethSelected.Count;i++) {
					_toothChartRelay.SetSelected(listTeethSelected[i],true);
				}
			}
			DataTable table=Pd.TableProgNotes.Copy();
			if(checkTreatPlans.Checked) {
				//filter list of DataRows to only include completed work and work for the selected treatment plans
				List<long> listProcNumsAll=gridTreatPlans.SelectedIndices.SelectMany(x => _listTreatPlans[x].ListProcTPs).Select(x => x.ProcNumOrig).ToList();
				_listDataRowsProcsForGraphical=new List<DataRow>();
				_listDataRowsProcsSkipped=new List<DataRow>();
				for(int i=0;i<table.Rows.Count;i++) {
					//If proc status is anything except TP and TPi
					if(new[] { ProcStat.C,ProcStat.Cn,ProcStat.EC,ProcStat.EO,ProcStat.R }.Contains((ProcStat)PIn.Long(table.Rows[i]["ProcStatus"].ToString()))
						|| listProcNumsAll.Contains(PIn.Long(table.Rows[i]["ProcNum"].ToString())))
					{
						if(!ShouldRowShowGraphical(table.Rows[i],dateLimit)) {
							continue;
						}
						_listDataRowsProcsForGraphical.Add(table.Rows[i]);
					}
				}
			}
			else {
				//put list back to the original list of DataRows
				_listDataRowsProcsSkipped=new List<DataRow>();
				List<string> listProcNumStrsOrig=_listDataRowsProcsOrig.Select(x => x["ProcNum"].ToString()).ToList();
				_listDataRowsProcsForGraphical=table.Select().Where(x => listProcNumStrsOrig.Contains(x["ProcNum"].ToString()) && ShouldRowShowGraphical(x,dateLimit)).ToList();
			}
			_listToothInitialsCopy=Pd.ListToothInitials.Select(x => x.Copy()).ToList();
			for(int i=0;i<_listDataRowsProcsSkipped.Count();i++) {
				ProcStat procStat=(ProcStat)PIn.Long(_listDataRowsProcsSkipped[i]["ProcStatus"].ToString());
				if(!procStat.In(ProcStat.C,ProcStat.EO,ProcStat.EC)) {
					continue;
				}
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(_listDataRowsProcsSkipped[i]["ProcCode"].ToString());
				if(procedureCode.PaintType!=ToothPaintingType.Extraction) {
					continue;
				}
				_listToothInitialsCopy.RemoveAll(x => x.InitialType==ToothInitialType.Missing && x.ToothNum==_listDataRowsProcsSkipped[i]["ToothNum"].ToString());
			}
			//Also remove any extractions for TP procs that were set to TP by the ShouldRowShowGraphical
			for(int i=0;i<_listDataRowsProcsForGraphical.Count();i++) {
				DateTime dateTP;
				DateTime dateComplete;
				ProcStat procStat=(ProcStat)PIn.Int(_listDataRowsProcsForGraphical[i]["ProcStatus"].ToString());
				if(!procStat.In(ProcStat.TP)) {
					continue;
				}
				if(!DateTime.TryParse(_listDataRowsProcsForGraphical[i]["DateTP"].ToString(),out dateTP)) {
					continue;
				}
				if(!DateTime.TryParse(_listDataRowsProcsForGraphical[i]["DateEntryC"].ToString(),out dateComplete)) {
					continue;
				}
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(_listDataRowsProcsForGraphical[i]["ProcCode"].ToString());
				if(procedureCode.PaintType==ToothPaintingType.Extraction 
						&& dateLimit<dateComplete && dateLimit>=dateTP) 
				{//Procedure is C and the slider date is after or equal to the TP date, but before the completion date
					_listToothInitialsCopy.RemoveAll(x => x.InitialType==ToothInitialType.Missing && x.ToothNum==_listDataRowsProcsForGraphical[i]["ToothNum"].ToString());//Pretend the row is TP for the tooth chart
				}
			}
			for(int i=0;i<_listToothInitialsCopy.Count;i++) {
				switch(_listToothInitialsCopy[i].InitialType) {
					case ToothInitialType.Missing:
						_toothChartRelay.SetMissing(_listToothInitialsCopy[i].ToothNum);
						break;
					case ToothInitialType.Hidden:
						_toothChartRelay.SetHidden(_listToothInitialsCopy[i].ToothNum);
						break;
					//case ToothInitialType.Primary:
					//	break;
					case ToothInitialType.Rotate:
						_toothChartRelay.MoveTooth(_listToothInitialsCopy[i].ToothNum,_listToothInitialsCopy[i].Movement,0,0,0,0,0);
						break;
					case ToothInitialType.TipM:
						_toothChartRelay.MoveTooth(_listToothInitialsCopy[i].ToothNum,0,_listToothInitialsCopy[i].Movement,0,0,0,0);
						break;
					case ToothInitialType.TipB:
						_toothChartRelay.MoveTooth(_listToothInitialsCopy[i].ToothNum,0,0,_listToothInitialsCopy[i].Movement,0,0,0);
						break;
					case ToothInitialType.ShiftM:
						_toothChartRelay.MoveTooth(_listToothInitialsCopy[i].ToothNum,0,0,0,_listToothInitialsCopy[i].Movement,0,0);
						break;
					case ToothInitialType.ShiftO:
						_toothChartRelay.MoveTooth(_listToothInitialsCopy[i].ToothNum,0,0,0,0,_listToothInitialsCopy[i].Movement,0);
						break;
					case ToothInitialType.ShiftB:
						_toothChartRelay.MoveTooth(_listToothInitialsCopy[i].ToothNum,0,0,0,0,0,_listToothInitialsCopy[i].Movement);
						break;
					case ToothInitialType.Drawing:
						_toothChartRelay.AddDrawingSegment(_listToothInitialsCopy[i].Copy());
						break;
					case ToothInitialType.Text:
						_toothChartRelay.AddText(_listToothInitialsCopy[i].GetTextString(),_listToothInitialsCopy[i].GetTextPoint(),_listToothInitialsCopy[i].ColorDraw,_listToothInitialsCopy[i].ToothInitialNum);
						break;
				}
			}
			DrawProcGraphics();
			DrawOrthoHardware();
			//HARNESS-END
		}

		//HARNESS-BEGIN OpenPmsDental\Main Modules\ControlChart.cs
		private void DrawProcGraphics(bool isPrinting=false) {
			//this requires: ProcStatus, ProcCode, ToothNum, HideGraphics, Surf, and ToothRange.  All need to be raw database values.
			string[] stringArrayTeeth;
			List<long> listProvNums=_toothChartRelay.GetPertinentProvNumsForToothColorPref(Security.CurUser,Pd.Patient,Pd.ListAppointments);
			for(int i=0;i<_listDataRowsProcsForGraphical.Count;i++) {
				if(_listDataRowsProcsForGraphical[i]["HideGraphics"].ToString()=="1") {
					continue;
				}
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(_listDataRowsProcsForGraphical[i]["ProcCode"].ToString());
				if(procedureCode.PaintType==ToothPaintingType.None || procedureCode.TreatArea==TreatmentArea.Mouth) {
					continue;
				}
				if(procedureCode.PaintType==ToothPaintingType.Extraction && (
					PIn.Long(_listDataRowsProcsForGraphical[i]["ProcStatus"].ToString())==(int)ProcStat.C
					|| PIn.Long(_listDataRowsProcsForGraphical[i]["ProcStatus"].ToString())==(int)ProcStat.EC
					|| PIn.Long(_listDataRowsProcsForGraphical[i]["ProcStatus"].ToString())==(int)ProcStat.EO
					)) {
					continue;//prevents the red X. Missing teeth already handled.
				}
				ProcStat procStat=(ProcStat)PIn.Long(_listDataRowsProcsForGraphical[i]["ProcStatus"].ToString());
				long provNumForProc=PIn.Long(_listDataRowsProcsForGraphical[i]["ProvNum"].ToString());
				bool applyColorPref=_toothChartRelay.DoesToothColorPrefApply(listProvNums,provNumForProc) && !isPrinting;
				_toothChartRelay.GetToothColors(procedureCode,procStat,applyColorPref,out Color colorDark,out Color colorLight);
				switch(procedureCode.PaintType) {
					case ToothPaintingType.BridgeDark:
						if(ToothInitials.ToothIsMissingOrHidden(_listToothInitialsCopy,_listDataRowsProcsForGraphical[i]["ToothNum"].ToString())){
							_toothChartRelay.SetPontic(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						}
						else{
							_toothChartRelay.SetCrown(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						}
						break;
					case ToothPaintingType.BridgeLight:
						if(ToothInitials.ToothIsMissingOrHidden(_listToothInitialsCopy,_listDataRowsProcsForGraphical[i]["ToothNum"].ToString())) {
							_toothChartRelay.SetPontic(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorLight);
						}
						else {
							_toothChartRelay.SetCrown(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorLight);
						}
						break;
					case ToothPaintingType.CrownDark:
						_toothChartRelay.SetCrown(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.CrownLight:
						_toothChartRelay.SetCrown(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorLight);
						break;
					case ToothPaintingType.DentureDark:
						if(_listDataRowsProcsForGraphical[i]["Surf"].ToString()=="U") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+2).ToString();
							}
						}
						else if(_listDataRowsProcsForGraphical[i]["Surf"].ToString()=="L") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+18).ToString();
							}
						}
						else {
							stringArrayTeeth=_listDataRowsProcsForGraphical[i]["ToothRange"].ToString().Split(new char[] {','});
						}
						for(int t=0;t<stringArrayTeeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(_listToothInitialsCopy,stringArrayTeeth[t])) {
								_toothChartRelay.SetPontic(stringArrayTeeth[t],colorDark);
								continue;
							}
							_toothChartRelay.SetCrown(stringArrayTeeth[t],colorDark);
						}
						break;
					case ToothPaintingType.DentureLight:
						if(_listDataRowsProcsForGraphical[i]["Surf"].ToString()=="U") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+2).ToString();
							}
						}
						else if(_listDataRowsProcsForGraphical[i]["Surf"].ToString()=="L") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+18).ToString();
							}
						}
						else {
							stringArrayTeeth=_listDataRowsProcsForGraphical[i]["ToothRange"].ToString().Split(new char[] { ',' });
						}
						for(int t=0;t<stringArrayTeeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(_listToothInitialsCopy,stringArrayTeeth[t])) {
								_toothChartRelay.SetPontic(stringArrayTeeth[t],colorLight);
								continue;
							}
							_toothChartRelay.SetCrown(stringArrayTeeth[t],colorLight);
						}
						break;
					case ToothPaintingType.Extraction:
						_toothChartRelay.SetBigX(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.FillingDark:
						_toothChartRelay.SetSurfaceColors(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),_listDataRowsProcsForGraphical[i]["Surf"].ToString(),colorDark);
						break;
					case ToothPaintingType.FillingLight:
						_toothChartRelay.SetSurfaceColors(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),_listDataRowsProcsForGraphical[i]["Surf"].ToString(),colorLight);
						break;
					case ToothPaintingType.Implant:
						_toothChartRelay.SetImplant(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.PostBU:
						_toothChartRelay.SetBU(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.RCT:
						_toothChartRelay.SetRCT(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.RetainedRoot:
						_toothChartRelay.SetRetainedRoot(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.Sealant:
						_toothChartRelay.SetSealant(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
						break;
					case ToothPaintingType.SpaceMaintainer:
						if(procedureCode.TreatArea==TreatmentArea.Tooth && _listDataRowsProcsForGraphical[i]["ToothNum"].ToString()!=""){
							_toothChartRelay.SetSpaceMaintainer(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark);
							break;
						}
						if(procedureCode.TreatArea==TreatmentArea.ToothRange && _listDataRowsProcsForGraphical[i]["ToothRange"].ToString()!="") {
							stringArrayTeeth=_listDataRowsProcsForGraphical[i]["ToothRange"].ToString().Split(',');
							for(int t=0;t<stringArrayTeeth.Length;t++) {
								_toothChartRelay.SetSpaceMaintainer(stringArrayTeeth[t],colorDark);
							}
							break;
						}
						if(procedureCode.AreaAlsoToothRange==true && _listDataRowsProcsForGraphical[i]["ToothRange"].ToString()!="") {
							stringArrayTeeth=_listDataRowsProcsForGraphical[i]["ToothRange"].ToString().Split(',');
							for(int t=0;t<stringArrayTeeth.Length;t++) {
								_toothChartRelay.SetSpaceMaintainer(stringArrayTeeth[t],colorDark);
							}
							break;
						}
						if(procedureCode.TreatArea==TreatmentArea.Quad){
							stringArrayTeeth=new string[0];
							if(_listDataRowsProcsForGraphical[i]["Surf"].ToString()=="UR") {
								stringArrayTeeth=new string[] {"4","5","6","7","8"};
							}
							if(_listDataRowsProcsForGraphical[i]["Surf"].ToString()=="UL") {
								stringArrayTeeth=new string[] {"9","10","11","12","13"};
							}
							if(_listDataRowsProcsForGraphical[i]["Surf"].ToString()=="LL") {
								stringArrayTeeth=new string[] {"20","21","22","23","24"};
							}
							if(_listDataRowsProcsForGraphical[i]["Surf"].ToString()=="LR") {
								stringArrayTeeth=new string[] {"25","26","27","28","29"};
							}							
							for(int t=0;t<stringArrayTeeth.Length;t++) {//could still be length 0
								_toothChartRelay.SetSpaceMaintainer(stringArrayTeeth[t],colorDark);
							}
						}
						break;
					case ToothPaintingType.Text:
						_toothChartRelay.SetText(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorDark,procedureCode.PaintText);
						break;
					case ToothPaintingType.Veneer:
						_toothChartRelay.SetVeneer(_listDataRowsProcsForGraphical[i]["ToothNum"].ToString(),colorLight);
						break;
				}
			}
		}
		//HARNESS-END

		//HARNESS-BEGIN OpenPmsDental\Main Modules\ControlChart.cs
		private void DrawOrthoHardware(){
			if(!checkOrthoGraphics.Checked){
				return;
			}
			List<OrthoHardwareSpec> listOrthoHardwareSpecs=OrthoHardwareSpecs.GetDeepCopy();
			List<OrthoHardwares.OrthoWire> listOrthoWires=new List<OrthoHardwares.OrthoWire>();//also used for elastics
			FillOrthoDateIfNeeded();
			List<OrthoHardware> listOrthoHardwares=Pd.ListOrthoHardwares.FindAll(x=>x.DateExam==comboOrthoDate.GetSelected<DateTime>() && (!x.IsHidden || checkShowHidden.Checked));
			for(int i=0;i<listOrthoHardwares.Count;i++){
				OrthoHardwareSpec orthoHardwareSpec=listOrthoHardwareSpecs.Find(x=>x.OrthoHardwareSpecNum==listOrthoHardwares[i].OrthoHardwareSpecNum);
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Bracket){
					_toothChartRelay.SetBracket(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor);
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
					_toothChartRelay.AddOrthoWireBetweenBrackets(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.InBracket){
					_toothChartRelay.AddOrthoWireInBracket(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.Elastic){
					_toothChartRelay.AddOrthoElastic(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
			}
		}
		//HARNESS-END

		//HARNESS-BEGIN OpenPmsDental\Main Modules\ControlChart.cs
		///<summary>Returns true if rowCur represents a valid procedure that should be shown on the tooth chart based on the current track bar date. Returns false if rowCur does not have a valid DateTP, DateEntryC, and ProcDate set. Will also return false if rowCur should not be drawn on the tooth chart (based on the track bar's currently selected date / status of rowCur). If the row needs to be skipped, it gets added to _listProcsSkipped so make sure to manage it correctly before calling this method.</summary>
		private bool ShouldRowShowGraphical(DataRow dataRow,DateTime dateLimit) {
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
				_listDataRowsProcsSkipped.Add(dataRow);
				return false;//Skip the proc
			}
			if(!procStat.In(ProcStat.C,ProcStat.TP) && dateLimit<dateComplete) {//slider date is before the completion date and the procedure is not C or TP
				_listDataRowsProcsSkipped.Add(dataRow);
				return false;//Skip the proc
			}
			if(procStat==ProcStat.C && dateLimit<dateComplete && dateLimit>=dateTP) {//Procedure is C and the slider date is after or equal to the TP date, but before the completion date
				dataRow["ProcStatus"]=POut.Int((int)ProcStat.TP);//Pretend the row is TP for the tooth chart
			}
			return true;
		}
		//HARNESS-END
	}

	///<summary>ControlTreat's chart code for the printout: PrepImageForPrinting from the tooth initials to the ortho hardware, DrawProcsGraphics
	///and DrawOrthoHardware.</summary>
	public class LegacyControlTreat {
		private ToothChartRelay _toothChartRelay;
		private Patient PatientCur;
		private List<ToothInitial> _listToothInitials;
		private List<Procedure> _listProceduresFiltered;
		private Func<long,List<Procedure>> _funcProceduresFiltered;

		public static CallLog Run(TreatPlanScene scene) {
			CallLog callLog=new CallLog();
			LegacyControlTreat legacyControlTreat=new LegacyControlTreat();
			legacyControlTreat._toothChartRelay=new ToothChartRelay(callLog);
			legacyControlTreat.PatientCur=CallLogFixtures.GetPatient(scene.PatNum);
			legacyControlTreat._funcProceduresFiltered=scene.FuncProceduresFiltered;
			legacyControlTreat.PrepImageAfterReset();
			return callLog;
		}

		///<summary>Stands in for ControlTreat.ComputeProcListFiltered, which reads the plans grid: the scene gives the list.</summary>
		private void ComputeProcListFiltered() {
			_listProceduresFiltered=_funcProceduresFiltered(PatientCur.PatNum);
		}

		private void PrepImageAfterReset() {
			//HARNESS-BEGIN OpenPmsDental\Main Modules\ControlTreat.cs
			_listToothInitials=ToothInitials.GetPatientData(PatientCur.PatNum);
			//first, primary.  That way, you can still set a primary tooth missing afterwards.
			for(int i=0;i<_listToothInitials.Count;i++) {
				if(_listToothInitials[i].InitialType==ToothInitialType.Primary) {
					_toothChartRelay.SetPrimary(_listToothInitials[i].ToothNum);
				}
			}
			for(int i=0;i<_listToothInitials.Count;i++) {
				switch(_listToothInitials[i].InitialType) {
					case ToothInitialType.Missing:
						_toothChartRelay.SetMissing(_listToothInitials[i].ToothNum);
						break;
					case ToothInitialType.Hidden:
						_toothChartRelay.SetHidden(_listToothInitials[i].ToothNum);
						break;
					case ToothInitialType.Rotate:
						_toothChartRelay.MoveTooth(_listToothInitials[i].ToothNum,_listToothInitials[i].Movement,0,0,0,0,0);
						break;
					case ToothInitialType.TipM:
						_toothChartRelay.MoveTooth(_listToothInitials[i].ToothNum,0,_listToothInitials[i].Movement,0,0,0,0);
						break;
					case ToothInitialType.TipB:
						_toothChartRelay.MoveTooth(_listToothInitials[i].ToothNum,0,0,_listToothInitials[i].Movement,0,0,0);
						break;
					case ToothInitialType.ShiftM:
						_toothChartRelay.MoveTooth(_listToothInitials[i].ToothNum,0,0,0,_listToothInitials[i].Movement,0,0);
						break;
					case ToothInitialType.ShiftO:
						_toothChartRelay.MoveTooth(_listToothInitials[i].ToothNum,0,0,0,0,_listToothInitials[i].Movement,0);
						break;
					case ToothInitialType.ShiftB:
						_toothChartRelay.MoveTooth(_listToothInitials[i].ToothNum,0,0,0,0,0,_listToothInitials[i].Movement);
						break;
					case ToothInitialType.Drawing:
						_toothChartRelay.AddDrawingSegment(_listToothInitials[i].Copy());
						break;
					case ToothInitialType.Text:
						_toothChartRelay.AddText(_listToothInitials[i].GetTextString(), _listToothInitials[i].GetTextPoint(), _listToothInitials[i].ColorDraw, _listToothInitials[i].ToothInitialNum);
						break;
				}
			}
			ComputeProcListFiltered();
			DrawProcsGraphics();
			DrawOrthoHardware();
			//HARNESS-END
		}

		//HARNESS-BEGIN OpenPmsDental\Main Modules\ControlTreat.cs
		private void DrawProcsGraphics() {
			Procedure procedure;
			string[] stringArrayTeeth;
			System.Drawing.Color cLight=System.Drawing.Color.White;
			System.Drawing.Color cDark=System.Drawing.Color.White;
			List<Def> listDefs=Defs.GetDefsForCategory(DefCat.ChartGraphicColors,true);
			for(int i=0;i<_listProceduresFiltered.Count;i++) {
				procedure=_listProceduresFiltered[i];
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
						if(ToothInitials.ToothIsMissingOrHidden(_listToothInitials,procedure.ToothNum)) {
							_toothChartRelay.SetPontic(procedure.ToothNum,cDark);
						}
						else {
							_toothChartRelay.SetCrown(procedure.ToothNum,cDark);
						}
						break;
					case ToothPaintingType.BridgeLight:
						if(ToothInitials.ToothIsMissingOrHidden(_listToothInitials,procedure.ToothNum)) {
							_toothChartRelay.SetPontic(procedure.ToothNum,cLight);
						}
						else {
							_toothChartRelay.SetCrown(procedure.ToothNum,cLight);
						}
						break;
					case ToothPaintingType.CrownDark:
						_toothChartRelay.SetCrown(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.CrownLight:
						_toothChartRelay.SetCrown(procedure.ToothNum,cLight);
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
							if(ToothInitials.ToothIsMissingOrHidden(_listToothInitials,stringArrayTeeth[t])) {
								_toothChartRelay.SetPontic(stringArrayTeeth[t],cDark);
							}
							else {
								_toothChartRelay.SetCrown(stringArrayTeeth[t],cDark);
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
							if(ToothInitials.ToothIsMissingOrHidden(_listToothInitials,stringArrayTeeth[t])) {
								_toothChartRelay.SetPontic(stringArrayTeeth[t],cLight);
							}
							else {
								_toothChartRelay.SetCrown(stringArrayTeeth[t],cLight);
							}
						}
						break;
					case ToothPaintingType.Extraction:
						_toothChartRelay.SetBigX(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.FillingDark:
						_toothChartRelay.SetSurfaceColors(procedure.ToothNum,procedure.Surf,cDark);
						break;
					case ToothPaintingType.FillingLight:
						_toothChartRelay.SetSurfaceColors(procedure.ToothNum,procedure.Surf,cLight);
						break;
					case ToothPaintingType.Implant:
						_toothChartRelay.SetImplant(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.PostBU:
						_toothChartRelay.SetBU(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.RCT:
						_toothChartRelay.SetRCT(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.RetainedRoot:
						_toothChartRelay.SetRetainedRoot(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.Sealant:
						_toothChartRelay.SetSealant(procedure.ToothNum,cDark);
						break;
					case ToothPaintingType.SpaceMaintainer:
						if(ProcedureCodes.GetProcCode(procedure.CodeNum).TreatArea==TreatmentArea.Tooth && procedure.ToothNum!=""){
							_toothChartRelay.SetSpaceMaintainer(procedure.ToothNum,cDark);
						}
						else if((ProcedureCodes.GetProcCode(procedure.CodeNum).TreatArea==TreatmentArea.ToothRange && procedure.ToothRange!="") 
							|| (ProcedureCodes.GetProcCode(procedure.CodeNum).AreaAlsoToothRange && procedure.ToothRange!=""))
						{
							stringArrayTeeth=procedure.ToothRange.Split(',');
							for(int t=0;t<stringArrayTeeth.Length;t++) {
								_toothChartRelay.SetSpaceMaintainer(stringArrayTeeth[t],cDark);
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
								_toothChartRelay.SetSpaceMaintainer(stringArrayTeeth[t],cDark);
							}
						}
						break;
					case ToothPaintingType.Text:
						_toothChartRelay.SetText(procedure.ToothNum,cDark,ProcedureCodes.GetProcCode(procedure.CodeNum).PaintText);
						break;
					case ToothPaintingType.Veneer:
						_toothChartRelay.SetVeneer(procedure.ToothNum,cLight);
						break;
				}
			}
		}
		//HARNESS-END

		//HARNESS-BEGIN OpenPmsDental\Main Modules\ControlTreat.cs
		private void DrawOrthoHardware(){
			List<OrthoHardware> listOrthoHardwares=OrthoHardwares.GetPatientData(PatientCur.PatNum);
			List<OrthoHardwareSpec> listOrthoHardwareSpecs=OrthoHardwareSpecs.GetDeepCopy();
			List<OrthoHardwares.OrthoWire> listOrthoWires=new List<OrthoHardwares.OrthoWire>();//also used for elastics
			for(int i=0;i<listOrthoHardwares.Count;i++){
				OrthoHardwareSpec orthoHardwareSpec=listOrthoHardwareSpecs.Find(x=>x.OrthoHardwareSpecNum==listOrthoHardwares[i].OrthoHardwareSpecNum);
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Bracket){
					_toothChartRelay.SetBracket(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor);
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
					_toothChartRelay.AddOrthoWireBetweenBrackets(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.InBracket){
					_toothChartRelay.AddOrthoWireInBracket(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.Elastic){
					_toothChartRelay.AddOrthoElastic(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
			}
		}
		//HARNESS-END
	}

	///<summary>SheetPrinting's chart code: DrawProcsGraphics, DrawOrthoHardware and GetToothChartHelper from the tooth initials to the ortho
	///hardware.</summary>
	public class LegacySheetPrinting {
		public static CallLog Run(SheetScene scene) {
			CallLog callLog=new CallLog();
			ToothChartRelay toothChartRelay=new ToothChartRelay(callLog);
			Patient patCur=scene.PatNum==0 ? null : CallLogFixtures.GetPatient(scene.PatNum);
			GetToothChartAfterReset(scene.PatNum,scene.ShowCompleted,scene.FuncTreatPlan?.Invoke(scene.PatNum),scene.FuncProceduresOverride?.Invoke(scene.PatNum),
				scene.IsInPatientDashboard,patCur,CallLogFixtures.GetAppointments(scene.PatNum,scene.HasTodaysAppointment),toothChartRelay);
			return callLog;
		}

		//HARNESS-BEGIN OpenPmsDental\SheetFramework\SheetPrinting.cs
		public static void DrawProcsGraphics(List<Procedure> procList,ToothChartRelay toothChartRelay,List<ToothInitial> toothInitialList,bool isInPatientDashboard
			,Patient patCur=null,List<Appointment> listAppts=null) 
		{
			//this method must also stay in OD.exe
			Procedure proc;
			string[] teeth;
			List<long> listCurProvNums=toothChartRelay.GetPertinentProvNumsForToothColorPref(Security.CurUser,patCur,listAppts);
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
				bool doApplyColorPref=isInPatientDashboard && toothChartRelay.DoesToothColorPrefApply(listCurProvNums,proc.ProvNum);
				toothChartRelay.GetToothColors(procCode,proc.ProcStatus,doApplyColorPref,out Color cDark,out Color cLight);
				switch(ProcedureCodes.GetProcCode(proc.CodeNum).PaintType) {
					case ToothPaintingType.BridgeDark:
						if(ToothInitials.ToothIsMissingOrHidden(toothInitialList,proc.ToothNum)) {
							toothChartRelay.SetPontic(proc.ToothNum,cDark);
						}
						else {
							toothChartRelay.SetCrown(proc.ToothNum,cDark);
						}
						break;
					case ToothPaintingType.BridgeLight:
						if(ToothInitials.ToothIsMissingOrHidden(toothInitialList,proc.ToothNum)) {
							toothChartRelay.SetPontic(proc.ToothNum,cLight);
						}
						else {
							toothChartRelay.SetCrown(proc.ToothNum,cLight);
						}
						break;
					case ToothPaintingType.CrownDark:
						toothChartRelay.SetCrown(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.CrownLight:
						toothChartRelay.SetCrown(proc.ToothNum,cLight);
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
								toothChartRelay.SetPontic(teeth[t],cDark);
							}
							else {
								toothChartRelay.SetCrown(teeth[t],cDark);
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
								toothChartRelay.SetPontic(teeth[t],cLight);
							}
							else {
								toothChartRelay.SetCrown(teeth[t],cLight);
							}
						}
						break;
					case ToothPaintingType.Extraction:
						toothChartRelay.SetBigX(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.FillingDark:
						toothChartRelay.SetSurfaceColors(proc.ToothNum,proc.Surf,cDark);
						break;
					case ToothPaintingType.FillingLight:
						toothChartRelay.SetSurfaceColors(proc.ToothNum,proc.Surf,cLight);
						break;
					case ToothPaintingType.Implant:
						toothChartRelay.SetImplant(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.PostBU:
						toothChartRelay.SetBU(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.RCT:
						toothChartRelay.SetRCT(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.RetainedRoot:
						toothChartRelay.SetRetainedRoot(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.Sealant:
						toothChartRelay.SetSealant(proc.ToothNum,cDark);
						break;
					case ToothPaintingType.SpaceMaintainer:
						if(ProcedureCodes.GetProcCode(proc.CodeNum).TreatArea==TreatmentArea.Tooth && proc.ToothNum!=""){
							toothChartRelay.SetSpaceMaintainer(proc.ToothNum,cDark);
						}
						else if((ProcedureCodes.GetProcCode(proc.CodeNum).TreatArea==TreatmentArea.ToothRange && proc.ToothRange!="") 
							|| (ProcedureCodes.GetProcCode(proc.CodeNum).AreaAlsoToothRange && proc.ToothRange!=""))
						{
							teeth=proc.ToothRange.Split(',');
							for(int t=0;t<teeth.Length;t++) {
								toothChartRelay.SetSpaceMaintainer(teeth[t],cDark);
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
								toothChartRelay.SetSpaceMaintainer(teeth[t],cDark);
							}
						}
						break;
					case ToothPaintingType.Text:
						toothChartRelay.SetText(proc.ToothNum,cDark,ProcedureCodes.GetProcCode(proc.CodeNum).PaintText);
						break;
					case ToothPaintingType.Veneer:
						toothChartRelay.SetVeneer(proc.ToothNum,cLight);
						break;
				}
			}
		}
		//HARNESS-END

		//HARNESS-BEGIN OpenPmsDental\SheetFramework\SheetPrinting.cs
		public static void DrawOrthoHardware(List<OrthoHardware> listOrthoHardwares,ToothChartRelay toothChartRelay){
			List<OrthoHardwareSpec> listOrthoHardwareSpecs=OrthoHardwareSpecs.GetDeepCopy();
			List<OrthoHardwares.OrthoWire> listOrthoWires=new List<OrthoHardwares.OrthoWire>();//also used for elastics
			for(int i=0;i<listOrthoHardwares.Count;i++){
				OrthoHardwareSpec orthoHardwareSpec=listOrthoHardwareSpecs.Find(x=>x.OrthoHardwareSpecNum==listOrthoHardwares[i].OrthoHardwareSpecNum);
				if(listOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Bracket){
					toothChartRelay.SetBracket(listOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor);
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
					toothChartRelay.AddOrthoWireBetweenBrackets(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.InBracket){
					toothChartRelay.AddOrthoWireInBracket(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.Elastic){
					toothChartRelay.AddOrthoElastic(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
			}
		}
		//HARNESS-END

		private static void GetToothChartAfterReset(long patNum,bool showCompleted,TreatPlan treatPlan,List<Procedure> listProceduresFilteredOverride,
			bool isInPatientDashboard,Patient patCur,List<Appointment> listAppts,ToothChartRelay toothChartRelay)
		{
			//HARNESS-BEGIN OpenPmsDental\SheetFramework\SheetPrinting.cs
			List<ToothInitial> toothInitialList=patNum==0?new List<ToothInitial>():ToothInitials.GetPatientData(patNum);
			//first, primary.  That way, you can still set a primary tooth missing afterwards.
			for(int i=0;i<toothInitialList.Count;i++) {
				if(toothInitialList[i].InitialType==ToothInitialType.Primary) {
					toothChartRelay.SetPrimary(toothInitialList[i].ToothNum);
				}
			}
			for(int i=0;i<toothInitialList.Count;i++) {
				switch(toothInitialList[i].InitialType) {
					case ToothInitialType.Missing:
						toothChartRelay.SetMissing(toothInitialList[i].ToothNum);
						break;
					case ToothInitialType.Hidden:
						toothChartRelay.SetHidden(toothInitialList[i].ToothNum);
						break;
					case ToothInitialType.Rotate:
						toothChartRelay.MoveTooth(toothInitialList[i].ToothNum,toothInitialList[i].Movement,0,0,0,0,0);
						break;
					case ToothInitialType.TipM:
						toothChartRelay.MoveTooth(toothInitialList[i].ToothNum,0,toothInitialList[i].Movement,0,0,0,0);
						break;
					case ToothInitialType.TipB:
						toothChartRelay.MoveTooth(toothInitialList[i].ToothNum,0,0,toothInitialList[i].Movement,0,0,0);
						break;
					case ToothInitialType.ShiftM:
						toothChartRelay.MoveTooth(toothInitialList[i].ToothNum,0,0,0,toothInitialList[i].Movement,0,0);
						break;
					case ToothInitialType.ShiftO:
						toothChartRelay.MoveTooth(toothInitialList[i].ToothNum,0,0,0,0,toothInitialList[i].Movement,0);
						break;
					case ToothInitialType.ShiftB:
						toothChartRelay.MoveTooth(toothInitialList[i].ToothNum,0,0,0,0,0,toothInitialList[i].Movement);
						break;
					case ToothInitialType.Drawing:
						toothChartRelay.AddDrawingSegment(toothInitialList[i].Copy());
						break;
					case ToothInitialType.Text:
						toothChartRelay.AddText(toothInitialList[i].GetTextString(), toothInitialList[i].GetTextPoint(), toothInitialList[i].ColorDraw, toothInitialList[i].ToothInitialNum);
						break;
				}
			}
			//We passed in a list of procs we want to see on the toothchart.  Use that.
			List<Procedure> listProceduresFiltered=new List<Procedure>();
			if(listProceduresFilteredOverride!=null) {
				listProceduresFiltered=listProceduresFilteredOverride;
			}
			else if(patNum>0) {//Custom list of procedures was not passed in, go get all for the patient like we always have.
				List<Procedure> listProceduresAll=Procedures.Refresh(patNum);
				listProceduresFiltered=OpenPmsDentalBusiness.SheetPrinting.FilterProceduresForToothChart(listProceduresAll,treatPlan,showCompleted);
			}
			listProceduresFiltered.Sort(OpenPmsDentalBusiness.SheetPrinting.CompareProcListFiltered);
			//Draw tooth chart
			DrawProcsGraphics(listProceduresFiltered,toothChartRelay,toothInitialList,isInPatientDashboard,patCur,listAppts);
			List<OrthoHardware> listOrthoHardwares=OrthoHardwares.GetPatientData(patNum);
			DrawOrthoHardware(listOrthoHardwares,toothChartRelay);
			//HARNESS-END
		}
	}

	///<summary>FormToothChartingBig's chart code: FillToothChart from the selection to the ortho hardware, DrawProcGraphics and
	///DrawOrthoHardware.</summary>
	public class LegacyFormToothChartBig {
		private ToothChartRelay _toothChartRelay;
		private bool _showBySelectedTeeth;
		public List<ToothInitial> ListToothInitials;
		public List<DataRow> ListDataRowsProcs;
		public List<Appointment> ListAppointments;
		public Patient PatientCur;
		public List<OrthoHardware> ListOrthoHardwares;

		///<summary>The Chart module hands the big chart its rows, its tooth initials and its hardware; the scene gives every procedure row, every
		///initial and all of the patient's hardware.</summary>
		public static CallLog Run(BigChartScene scene) {
			CallLog callLog=new CallLog();
			LegacyFormToothChartBig legacyFormToothChartBig=new LegacyFormToothChartBig();
			legacyFormToothChartBig._toothChartRelay=new ToothChartRelay(callLog);
			legacyFormToothChartBig.ListToothInitials=ToothInitials.GetPatientData(scene.PatNum);
			legacyFormToothChartBig.ListDataRowsProcs=CallLogFixtures.GetProcRowsOrig(CallLogFixtures.GetProgNotes(scene.PatNum));
			legacyFormToothChartBig.ListAppointments=CallLogFixtures.GetAppointments(scene.PatNum,scene.HasTodaysAppointment);
			legacyFormToothChartBig.PatientCur=CallLogFixtures.GetPatient(scene.PatNum);
			legacyFormToothChartBig.ListOrthoHardwares=OrthoHardwares.GetPatientData(scene.PatNum);
			legacyFormToothChartBig.FillToothChartAfterReset(new List<string>());
			return callLog;
		}

		private void FillToothChartAfterReset(List<string> listSelectedTeeth) {
			//HARNESS-BEGIN OpenPmsDental\Forms\FormToothChartBig.cs
			if(_showBySelectedTeeth) {
				for(int i=0;i<listSelectedTeeth.Count;i++) {
					_toothChartRelay.SetSelected(listSelectedTeeth[i],true);
				}
			}
			//first, primary.  That way, you can still set a primary tooth missing afterwards.
			for(int i=0;i<ListToothInitials.Count;i++) {
				if(ListToothInitials[i].InitialType==ToothInitialType.Primary) {
					_toothChartRelay.SetPrimary(ListToothInitials[i].ToothNum);
				}
			}
			for(int i=0;i<ListToothInitials.Count;i++) {
				switch(ListToothInitials[i].InitialType) {
					case ToothInitialType.Missing:
						_toothChartRelay.SetMissing(ListToothInitials[i].ToothNum);
						break;
					case ToothInitialType.Hidden:
						_toothChartRelay.SetHidden(ListToothInitials[i].ToothNum);
						break;
					//case ToothInitialType.Primary:
					//	break;
					case ToothInitialType.Rotate:
						_toothChartRelay.MoveTooth(ListToothInitials[i].ToothNum,ListToothInitials[i].Movement,0,0,0,0,0);
						break;
					case ToothInitialType.TipM:
						_toothChartRelay.MoveTooth(ListToothInitials[i].ToothNum,0,ListToothInitials[i].Movement,0,0,0,0);
						break;
					case ToothInitialType.TipB:
						_toothChartRelay.MoveTooth(ListToothInitials[i].ToothNum,0,0,ListToothInitials[i].Movement,0,0,0);
						break;
					case ToothInitialType.ShiftM:
						_toothChartRelay.MoveTooth(ListToothInitials[i].ToothNum,0,0,0,ListToothInitials[i].Movement,0,0);
						break;
					case ToothInitialType.ShiftO:
						_toothChartRelay.MoveTooth(ListToothInitials[i].ToothNum,0,0,0,0,ListToothInitials[i].Movement,0);
						break;
					case ToothInitialType.ShiftB:
						_toothChartRelay.MoveTooth(ListToothInitials[i].ToothNum,0,0,0,0,0,ListToothInitials[i].Movement);
						break;
					case ToothInitialType.Drawing:
						_toothChartRelay.AddDrawingSegment(ListToothInitials[i].Copy());
						break;
					case ToothInitialType.Text:
						_toothChartRelay.AddText(ListToothInitials[i].GetTextString(), ListToothInitials[i].GetTextPoint(), ListToothInitials[i].ColorDraw, ListToothInitials[i].ToothInitialNum);
						break;
				}
			}
			DrawProcGraphics();
			DrawOrthoHardware();
			//HARNESS-END
		}

		//HARNESS-BEGIN OpenPmsDental\Forms\FormToothChartBig.cs
		private void DrawProcGraphics() {
			//this requires: ProcStatus, ProcCode, ToothNum, HideGraphics, Surf, and ToothRange.  All need to be raw database values.
			string[] stringArrayTeeth;
			List<long> listProvNums=_toothChartRelay.GetPertinentProvNumsForToothColorPref(Security.CurUser,PatientCur,ListAppointments);
			for(int i=0;i<ListDataRowsProcs.Count;i++) {
				if(ListDataRowsProcs[i]["HideGraphics"].ToString()=="1") {
					continue;
				}
				if(ProcedureCodes.GetProcCode(ListDataRowsProcs[i]["ProcCode"].ToString()).PaintType==ToothPaintingType.Extraction && (
					PIn.Long(ListDataRowsProcs[i]["ProcStatus"].ToString())==(int)ProcStat.C
					|| PIn.Long(ListDataRowsProcs[i]["ProcStatus"].ToString())==(int)ProcStat.EC
					|| PIn.Long(ListDataRowsProcs[i]["ProcStatus"].ToString())==(int)ProcStat.EO
					)) {
					continue;//prevents the red X. Missing teeth already handled.
				}
				ProcedureCode procedureCode = ProcedureCodes.GetProcCode(ListDataRowsProcs[i]["ProcCode"].ToString());
				ProcStat procStat = (ProcStat)PIn.Long(ListDataRowsProcs[i]["ProcStatus"].ToString());
				long provNum=PIn.Long(ListDataRowsProcs[i]["ProvNum"].ToString());
				bool doApplyColorPref=_toothChartRelay.DoesToothColorPrefApply(listProvNums,provNum);
				_toothChartRelay.GetToothColors(procedureCode,procStat,doApplyColorPref,out Color cDark,out Color cLight);
				switch(ProcedureCodes.GetProcCode(ListDataRowsProcs[i]["ProcCode"].ToString()).PaintType) {
					case ToothPaintingType.BridgeDark:
						if(ToothInitials.ToothIsMissingOrHidden(ListToothInitials,ListDataRowsProcs[i]["ToothNum"].ToString())) {
							_toothChartRelay.SetPontic(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						}
						else {
							_toothChartRelay.SetCrown(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						}
						break;
					case ToothPaintingType.BridgeLight:
						if(ToothInitials.ToothIsMissingOrHidden(ListToothInitials,ListDataRowsProcs[i]["ToothNum"].ToString())) {
							_toothChartRelay.SetPontic(ListDataRowsProcs[i]["ToothNum"].ToString(),cLight);
						}
						else {
							_toothChartRelay.SetCrown(ListDataRowsProcs[i]["ToothNum"].ToString(),cLight);
						}
						break;
					case ToothPaintingType.CrownDark:
						_toothChartRelay.SetCrown(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.CrownLight:
						_toothChartRelay.SetCrown(ListDataRowsProcs[i]["ToothNum"].ToString(),cLight);
						break;
					case ToothPaintingType.DentureDark:
						if(ListDataRowsProcs[i]["Surf"].ToString()=="U") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+2).ToString();
							}
						}
						else if(ListDataRowsProcs[i]["Surf"].ToString()=="L") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+18).ToString();
							}
						}
						else {
							stringArrayTeeth=ListDataRowsProcs[i]["ToothRange"].ToString().Split(new char[] { ',' });
						}
						for(int t=0;t<stringArrayTeeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(ListToothInitials,stringArrayTeeth[t])) {
								_toothChartRelay.SetPontic(stringArrayTeeth[t],cDark);
							}
							else {
								_toothChartRelay.SetCrown(stringArrayTeeth[t],cDark);
							}
						}
						break;
					case ToothPaintingType.DentureLight:
						if(ListDataRowsProcs[i]["Surf"].ToString()=="U") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+2).ToString();
							}
						}
						else if(ListDataRowsProcs[i]["Surf"].ToString()=="L") {
							stringArrayTeeth=new string[14];
							for(int t=0;t<14;t++) {
								stringArrayTeeth[t]=(t+18).ToString();
							}
						}
						else {
							stringArrayTeeth=ListDataRowsProcs[i]["ToothRange"].ToString().Split(new char[] { ',' });
						}
						for(int t=0;t<stringArrayTeeth.Length;t++) {
							if(ToothInitials.ToothIsMissingOrHidden(ListToothInitials,stringArrayTeeth[t])) {
								_toothChartRelay.SetPontic(stringArrayTeeth[t],cLight);
							}
							else {
								_toothChartRelay.SetCrown(stringArrayTeeth[t],cLight);
							}
						}
						break;
					case ToothPaintingType.Extraction:
						_toothChartRelay.SetBigX(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.FillingDark:
						_toothChartRelay.SetSurfaceColors(ListDataRowsProcs[i]["ToothNum"].ToString(),ListDataRowsProcs[i]["Surf"].ToString(),cDark);
						break;
					case ToothPaintingType.FillingLight:
						_toothChartRelay.SetSurfaceColors(ListDataRowsProcs[i]["ToothNum"].ToString(),ListDataRowsProcs[i]["Surf"].ToString(),cLight);
						break;
					case ToothPaintingType.Implant:
						_toothChartRelay.SetImplant(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.PostBU:
						_toothChartRelay.SetBU(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.RCT:
						_toothChartRelay.SetRCT(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.RetainedRoot:
						_toothChartRelay.SetRetainedRoot(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.Sealant:
						_toothChartRelay.SetSealant(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						break;
					case ToothPaintingType.SpaceMaintainer:
						if(procedureCode.TreatArea==TreatmentArea.Tooth && ListDataRowsProcs[i]["ToothNum"].ToString()!=""){
							_toothChartRelay.SetSpaceMaintainer(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark);
						}
						else if((procedureCode.TreatArea==TreatmentArea.ToothRange && ListDataRowsProcs[i]["ToothRange"].ToString()!="") 
							|| (procedureCode.AreaAlsoToothRange==true && ListDataRowsProcs[i]["ToothRange"].ToString()!=""))
						{
							stringArrayTeeth=ListDataRowsProcs[i]["ToothRange"].ToString().Split(',');
							for(int t=0;t<stringArrayTeeth.Length;t++) {
								_toothChartRelay.SetSpaceMaintainer(stringArrayTeeth[t],cDark);
							}
						}
						else if(procedureCode.TreatArea==TreatmentArea.Quad){
							stringArrayTeeth=new string[0];
							if(ListDataRowsProcs[i]["Surf"].ToString()=="UR") {
								stringArrayTeeth=new string[] {"4","5","6","7","8"};
							}
							if(ListDataRowsProcs[i]["Surf"].ToString()=="UL") {
								stringArrayTeeth=new string[] {"9","10","11","12","13"};
							}
							if(ListDataRowsProcs[i]["Surf"].ToString()=="LL") {
								stringArrayTeeth=new string[] {"20","21","22","23","24"};
							}
							if(ListDataRowsProcs[i]["Surf"].ToString()=="LR") {
								stringArrayTeeth=new string[] {"25","26","27","28","29"};
							}							
							for(int t=0;t<stringArrayTeeth.Length;t++) {//could still be length 0
								_toothChartRelay.SetSpaceMaintainer(stringArrayTeeth[t],cDark);
							}
						}
						break;
					case ToothPaintingType.Text:
						_toothChartRelay.SetText(ListDataRowsProcs[i]["ToothNum"].ToString(),cDark,ProcedureCodes.GetProcCode(ListDataRowsProcs[i]["ProcCode"].ToString()).PaintText);
						break;
					case ToothPaintingType.Veneer:
						_toothChartRelay.SetVeneer(ListDataRowsProcs[i]["ToothNum"].ToString(),cLight);
						break;
				}
			}
		}
		//HARNESS-END

		//HARNESS-BEGIN OpenPmsDental\Forms\FormToothChartBig.cs
		private void DrawOrthoHardware(){
			List<OrthoHardwareSpec> listOrthoHardwareSpecs=OrthoHardwareSpecs.GetDeepCopy();
			List<OrthoHardwares.OrthoWire> listOrthoWires=new List<OrthoHardwares.OrthoWire>();//also used for elastics
			for(int i=0;i<ListOrthoHardwares.Count;i++){
				OrthoHardwareSpec orthoHardwareSpec=listOrthoHardwareSpecs.Find(x=>x.OrthoHardwareSpecNum==ListOrthoHardwares[i].OrthoHardwareSpecNum);
				if(ListOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Bracket){
					_toothChartRelay.SetBracket(ListOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor);
				}
				if(ListOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Wire){
					listOrthoWires.AddRange(OrthoHardwares.GetWires(ListOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor));
				}
				if(ListOrthoHardwares[i].OrthoHardwareType==EnumOrthoHardwareType.Elastic){
					listOrthoWires.AddRange(OrthoHardwares.GetElastics(ListOrthoHardwares[i].ToothRange,orthoHardwareSpec.ItemColor));
				}
			}
			for(int i=0;i<listOrthoWires.Count;i++){
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.BetweenBrackets){
					_toothChartRelay.AddOrthoWireBetweenBrackets(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.InBracket){
					_toothChartRelay.AddOrthoWireInBracket(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ColorDraw);
				}
				if(listOrthoWires[i].OrthoWireType==OrthoHardwares.EnumOrthoWireType.Elastic){
					_toothChartRelay.AddOrthoElastic(listOrthoWires[i].ToothIDstart,listOrthoWires[i].ToothIDend,listOrthoWires[i].ColorDraw);
				}
			}
		}
		//HARNESS-END
	}
}
