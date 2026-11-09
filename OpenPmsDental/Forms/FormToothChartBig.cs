using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using SparksToothChart;

namespace OpenPmsDental {
	/// <summary></summary>
	public partial class FormToothChartingBig:FormODBase {
		private bool _showBySelectedTeeth;
		public List<ToothInitial> ListToothInitials;
		private ToothChartRelay _toothChartRelay;
		public List<DataRow> ListDataRowsProcs;
		public List<Appointment> ListAppointments;
		public Patient PatientCur;
		public List<OrthoHardware> ListOrthoHardwares;
		public bool IsOrthoMode;

		///<summary></summary>
		public FormToothChartingBig(bool showBySelectedTeeth)
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_showBySelectedTeeth=showBySelectedTeeth;
		}

		private void FormToothChartingBig_Load(object sender,EventArgs e) {
			this.Text=Lan.g(this,"Big Tooth Chart")+" - "+PatientCur.GetNameLF()+" - "+PatientCur.PatNum;
			_toothChartRelay= new ToothChartRelay();
			_toothChartRelay.SetToothChartWrapper(toothChartWrapper);
			toothChartWrapper.Visible=true;
			ToothChartRelay.ApplyComputerSettings(toothChartWrapper,ToothChartSettingsUse.ModeByName);
			FillToothChart();
			//toothChart.Refresh();
		}

		private void FormToothChartingBig_ResizeEnd(object sender,EventArgs e) {
			FillToothChart();
			//toothChart.Refresh();
		}

		///<summary>This is, of course, called when module refreshed.  But it's also called when user sets missing teeth or tooth movements.  In that case, the Progress notes are not refreshed, so it's a little faster.  This also fills in the movement amounts.</summary>
		private void FillToothChart(){
			Cursor=Cursors.WaitCursor;
			_toothChartRelay.BeginUpdate();
			_toothChartRelay.SetOrthoMode(IsOrthoMode);
			ToothNumberingNomenclature toothNumberingNomenclature=(ToothNumberingNomenclature)PrefC.GetInt(PrefName.UseInternationalToothNumbers);
			if(IsOrthoMode && toothNumberingNomenclature==ToothNumberingNomenclature.Universal){
				_toothChartRelay.SetToothNumberingNomenclature(ToothNumberingNomenclature.Palmer);
			}
			else{
				_toothChartRelay.SetToothNumberingNomenclature(toothNumberingNomenclature);
			}
			List<Def> listDefs=Defs.GetDefsForCategory(DefCat.ChartGraphicColors);
			_toothChartRelay.ColorBackgroundMain=listDefs[10].ItemColor;
			_toothChartRelay.ColorText=listDefs[11].ItemColor;
			_toothChartRelay.ColorTextHighlightFore=listDefs[12].ItemColor;
			_toothChartRelay.ColorTextHighlightBack=listDefs[13].ItemColor;
			//remember which teeth were selected
			List<string> listSelectedTeeth=new List<string>(toothChartWrapper.SelectedTeeth);
			//ArrayList selectedTeeth=new ArrayList();//integers 1-32
			//for(int i=0;i<toothChart.SelectedTeeth.Length;i++) {
			//	selectedTeeth.Add(Tooth.ToInt(toothChart.SelectedTeeth[i]));
			//}
			_toothChartRelay.ResetTeeth();
			//if(PatCur==null) {
				//toothChart.ResumeLayout();
				//FillMovementsAndHidden();
				//Cursor=Cursors.Default;
				//return;
			//}
			if(_showBySelectedTeeth) {
				for(int i=0;i<listSelectedTeeth.Count;i++) {
					_toothChartRelay.SetSelected(listSelectedTeeth[i],true);
				}
			}
			//first, primary.  That way, you can still set a primary tooth missing afterwards.
			ToothChartPainter.PaintPrimaryTeeth(_toothChartRelay,ListToothInitials);
			ToothChartPainter.PaintToothInitials(_toothChartRelay,ListToothInitials);
			ToothChartPainter.PaintProcedures(_toothChartRelay,ToothChartPaintProc.FromDataRows(ListDataRowsProcs),ListToothInitials,
				ToothChartPaintOptions.ForBigChart(PatientCur,ListAppointments));
			ToothChartPainter.PaintOrthoHardware(_toothChartRelay,ListOrthoHardwares);
			_toothChartRelay.EndUpdate();
			//FillMovementsAndHidden();
			Cursor=Cursors.Default;
		}

		private void FormToothChartingBig_FormClosed(object sender,FormClosedEventArgs e) {
			//This helps ensure that the tooth chart wrapper is properly disposed of.
			//This step is necessary so that graphics memory does not fill up.
			Dispose();
		}
	}
}