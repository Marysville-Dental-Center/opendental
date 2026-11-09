// The four callers' chart code as it is since TC-2 moved their painting into ToothChartPainter (package TC-2): the painter calls each caller
// makes between the reset of its chart and the end of the refill, in the caller's order, with the inputs the caller passes. The tests cannot
// open the program's windows, so these drivers stand in for them; CallerSourceTests pins the same calls in the same order in each caller.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Data;
using System.Linq;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace ToothChartImageTests {
	public static class PainterCallers {
		///<summary>ControlChart.FillToothChart after the reset (the selection it restores is no painting call), its DrawProcGraphics and
		///DrawOrthoHardware, then for IsPrintRecolor the two recolours of menuItemChartSave.</summary>
		public static CallLog RunChartModule(ChartModuleScene scene) {
			CallLog callLog=new CallLog();
			RecordingPaintTarget target=new RecordingPaintTarget(callLog);
			Patient patient=CallLogFixtures.GetPatient(scene.PatNum);
			List<Appointment> listAppointments=CallLogFixtures.GetAppointments(scene.PatNum,scene.HasTodaysAppointment);
			List<ToothInitial> listToothInitials=ToothInitials.GetPatientData(scene.PatNum);
			DataTable tableProgNotes=CallLogFixtures.GetProgNotes(scene.PatNum);
			List<OrthoHardware> listOrthoHardwaresPatient=OrthoHardwares.GetPatientData(scene.PatNum);
			List<DataRow> listDataRowsProcsOrig=CallLogFixtures.GetProcRowsOrig(tableProgNotes);
			ToothChartPainter.PaintPrimaryTeeth(target,listToothInitials);
			DataTable table=tableProgNotes.Copy();
			List<long> listProcNumsTreatPlans=null;
			if(scene.FuncProcNumsTreatPlans!=null) {
				TreatPlan treatPlan=new TreatPlan { ListProcTPs=scene.FuncProcNumsTreatPlans().Select(x => new ProcTP { ProcNumOrig=x }).ToList() };
				listProcNumsTreatPlans=new[] { treatPlan }.SelectMany(x => x.ListProcTPs).Select(x => x.ProcNumOrig).ToList();
			}
			List<DataRow> listDataRowsProcsForGraphical=ToothChartPainter.GetChartModuleRows(table,listProcNumsTreatPlans,listDataRowsProcsOrig,scene.DateLimit,
				out List<DataRow> listDataRowsProcsSkipped);
			List<ToothInitial> listToothInitialsCopy=ToothChartPainter.GetChartModuleToothInitials(listToothInitials,listDataRowsProcsSkipped,
				listDataRowsProcsForGraphical,scene.DateLimit);
			ToothChartPainter.PaintToothInitials(target,listToothInitialsCopy);
			ToothChartPainter.PaintProcedures(target,ToothChartPaintProc.FromDataRows(listDataRowsProcsForGraphical),listToothInitialsCopy,
				ToothChartPaintOptions.ForChartModule(patient,listAppointments,false));
			if(scene.IsOrthoGraphics) {
				List<OrthoHardware> listOrthoHardwares=ToothChartPainter.GetChartModuleOrthoHardwares(listOrthoHardwaresPatient,scene.DateOrthoExam,
					scene.IsShowHidden);
				ToothChartPainter.PaintOrthoHardware(target,listOrthoHardwares);
			}
			if(scene.IsPrintRecolor) {
				ToothChartPainter.PaintProcedures(target,ToothChartPaintProc.FromDataRows(listDataRowsProcsForGraphical),listToothInitialsCopy,
					ToothChartPaintOptions.ForChartModule(patient,listAppointments,true));
				ToothChartPainter.PaintProcedures(target,ToothChartPaintProc.FromDataRows(listDataRowsProcsForGraphical),listToothInitialsCopy,
					ToothChartPaintOptions.ForChartModule(patient,listAppointments,false));
			}
			return callLog;
		}

		///<summary>ControlTreat.PrepImageForPrinting after the reset.</summary>
		public static CallLog RunTreatPlan(TreatPlanScene scene) {
			CallLog callLog=new CallLog();
			RecordingPaintTarget target=new RecordingPaintTarget(callLog);
			Patient patientCur=CallLogFixtures.GetPatient(scene.PatNum);
			List<ToothInitial> listToothInitials=ToothInitials.GetPatientData(patientCur.PatNum);
			ToothChartPainter.PaintPrimaryTeeth(target,listToothInitials);
			ToothChartPainter.PaintToothInitials(target,listToothInitials);
			List<Procedure> listProceduresFiltered=scene.FuncProceduresFiltered(patientCur.PatNum);//ComputeProcListFiltered
			ToothChartPainter.PaintProcedures(target,ToothChartPaintProc.FromProcedures(listProceduresFiltered),listToothInitials,
				ToothChartPaintOptions.ForTreatPlanPrintout());
			ToothChartPainter.PaintOrthoHardware(target,OrthoHardwares.GetPatientData(patientCur.PatNum));
			return callLog;
		}

		///<summary>SheetPrinting.GetToothChartHelper after the reset.</summary>
		public static CallLog RunSheet(SheetScene scene) {
			CallLog callLog=new CallLog();
			RecordingPaintTarget target=new RecordingPaintTarget(callLog);
			long patNum=scene.PatNum;
			Patient patCur=scene.PatNum==0 ? null : CallLogFixtures.GetPatient(scene.PatNum);
			TreatPlan treatPlan=scene.FuncTreatPlan?.Invoke(scene.PatNum);
			List<Procedure> listProceduresFilteredOverride=scene.FuncProceduresOverride?.Invoke(scene.PatNum);
			List<Appointment> listAppts=CallLogFixtures.GetAppointments(scene.PatNum,scene.HasTodaysAppointment);
			List<ToothInitial> toothInitialList=patNum==0?new List<ToothInitial>():ToothInitials.GetPatientData(patNum);
			ToothChartPainter.PaintPrimaryTeeth(target,toothInitialList);
			ToothChartPainter.PaintToothInitials(target,toothInitialList);
			List<Procedure> listProceduresFiltered=new List<Procedure>();
			if(listProceduresFilteredOverride!=null) {
				listProceduresFiltered=listProceduresFilteredOverride;
			}
			else if(patNum>0) {
				List<Procedure> listProceduresAll=Procedures.Refresh(patNum);
				listProceduresFiltered=OpenPmsDentalBusiness.SheetPrinting.FilterProceduresForToothChart(listProceduresAll,treatPlan,scene.ShowCompleted);
			}
			listProceduresFiltered.Sort(OpenPmsDentalBusiness.SheetPrinting.CompareProcListFiltered);
			ToothChartPainter.PaintProcedures(target,ToothChartPaintProc.FromProcedures(listProceduresFiltered),toothInitialList,
				ToothChartPaintOptions.ForSheet(scene.IsInPatientDashboard,patCur,listAppts));
			List<OrthoHardware> listOrthoHardwares=OrthoHardwares.GetPatientData(patNum);
			ToothChartPainter.PaintOrthoHardware(target,listOrthoHardwares);
			return callLog;
		}

		///<summary>FormToothChartingBig.FillToothChart after the reset (the selection it restores is no painting call).</summary>
		public static CallLog RunBigChart(BigChartScene scene) {
			CallLog callLog=new CallLog();
			RecordingPaintTarget target=new RecordingPaintTarget(callLog);
			List<ToothInitial> listToothInitials=ToothInitials.GetPatientData(scene.PatNum);
			List<DataRow> listDataRowsProcs=CallLogFixtures.GetProcRowsOrig(CallLogFixtures.GetProgNotes(scene.PatNum));
			List<Appointment> listAppointments=CallLogFixtures.GetAppointments(scene.PatNum,scene.HasTodaysAppointment);
			Patient patientCur=CallLogFixtures.GetPatient(scene.PatNum);
			List<OrthoHardware> listOrthoHardwares=OrthoHardwares.GetPatientData(scene.PatNum);
			ToothChartPainter.PaintPrimaryTeeth(target,listToothInitials);
			ToothChartPainter.PaintToothInitials(target,listToothInitials);
			ToothChartPainter.PaintProcedures(target,ToothChartPaintProc.FromDataRows(listDataRowsProcs),listToothInitials,
				ToothChartPaintOptions.ForBigChart(patientCur,listAppointments));
			ToothChartPainter.PaintOrthoHardware(target,listOrthoHardwares);
			return callLog;
		}
	}
}
