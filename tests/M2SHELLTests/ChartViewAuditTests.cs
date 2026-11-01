// v26_1-misc-audit-chartviews: Audit Trail entries for adding, editing, deleting and reordering a Chart View.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2SHELLTests {
	[TestClass]
	public class ChartViewAuditTests {
		private static ChartView NewView() {
			return new ChartView {
				ChartViewNum=7,
				Description="Hygiene",
				ItemOrder=2,
				ProcStatuses=ChartViewProcStat.TP|ChartViewProcStat.C,
				ObjectTypes=ChartViewObjs.Appointments|ChartViewObjs.CommLog,
				ShowProcNotes=true,
				IsAudit=false,
				SelectedTeethOnly=false,
				OrionStatusFlags=OrionStatus.None,
				DatesShowing=ChartViewDates.All,
				IsTpCharting=true,
			};
		}

		private static List<DisplayField> NewColumns() {
			return new List<DisplayField> {
				new DisplayField { InternalName="Date",Description="",ColumnWidth=67 },
				new DisplayField { InternalName="Th",Description="Tooth",ColumnWidth=27 },
			};
		}

		[TestMethod]
		public void Texts_AddDeleteMove() {
			ChartView chartView=NewView();
			Assert.AreEqual("Chart view 'Hygiene' added.",ChartViews.GetAddedLogText(chartView));
			Assert.AreEqual("Chart view 'Hygiene' deleted.",ChartViews.GetDeletedLogText(chartView));
			Assert.AreEqual("Chart view 'Hygiene' moved from position 3 to 2.",ChartViews.GetMovedLogText(chartView,2,1),"Positions are shown 1-based.");
		}

		[TestMethod]
		public void EditChanges_NothingChanged_IsEmpty() {
			Assert.AreEqual(0,ChartViews.GetEditChanges(NewView(),NewView(),NewColumns(),NewColumns()).Count);
			ChartView chartViewMoved=NewView();
			chartViewMoved.ItemOrder=0;
			Assert.AreEqual(0,ChartViews.GetEditChanges(NewView(),chartViewMoved,null,null).Count,"Moves have their own entry.");
		}

		[TestMethod]
		public void EditChanges_ListsEveryChangedSetting() {
			ChartView chartViewOld=NewView();
			ChartView chartViewNew=NewView();
			chartViewNew.Description="Hygiene recall";
			chartViewNew.ProcStatuses=ChartViewProcStat.TP|ChartViewProcStat.C|ChartViewProcStat.EC;
			chartViewNew.ObjectTypes=ChartViewObjs.None;
			chartViewNew.DatesShowing=ChartViewDates.ThisYear;
			chartViewNew.ShowProcNotes=false;
			chartViewNew.IsAudit=true;
			chartViewNew.SelectedTeethOnly=true;
			chartViewNew.IsTpCharting=false;
			chartViewNew.OrionStatusFlags=OrionStatus.TP|OrionStatus.W;
			List<DisplayField> listColumnsNew=NewColumns();
			listColumnsNew[1].ColumnWidth=40;
			List<string> listChanges=ChartViews.GetEditChanges(chartViewOld,chartViewNew,NewColumns(),listColumnsNew);
			CollectionAssert.AreEqual(new[] {
				"description 'Hygiene' to 'Hygiene recall'",
				"procedure statuses TP, C to TP, C, EC",
				"show Appointments, CommLog to none",
				"dates All to ThisYear",
				"procedure notes on to off",
				"audit mode off to on",
				"selected teeth only off to on",
				"treatment plan charting on to off",
				"Orion statuses none to TP, W",
				"columns Date 67, Th (Tooth) 27 to Date 67, Th (Tooth) 40",
			},listChanges);
			Assert.AreEqual("Chart view 'Hygiene recall' edited: description 'Hygiene' to 'Hygiene recall'; dates All to ThisYear.",
				ChartViews.GetEditedLogText(chartViewNew,new List<string> { listChanges[0],listChanges[3] }));
		}

		[TestMethod]
		public void FlagText_SplitsCombinedValues() {
			Assert.AreEqual("TP, C, EC, EO, R, D, Cn",ChartViews.GetFlagText(ChartViewProcStat.All),"All is shown as its parts.");
			Assert.AreEqual("none",ChartViews.GetFlagText(ChartViewProcStat.None));
			Assert.AreEqual("default",ChartViews.GetColumnsText(new List<DisplayField>()),"A view with no columns of its own uses the default columns.");
		}

		[TestMethod]
		public void ColumnOrderChange_IsAChange() {
			List<DisplayField> listColumnsNew=NewColumns();
			listColumnsNew.Reverse();
			List<string> listChanges=ChartViews.GetEditChanges(NewView(),NewView(),NewColumns(),listColumnsNew);
			Assert.AreEqual(1,listChanges.Count);
			StringAssert.StartsWith(listChanges[0],"columns ");
		}

		[TestMethod]
		public void Permission_IsInTheAuditTrailFilter() {
			Assert.AreEqual(262,(int)EnumPermType.ChartViewsEdit);
			Assert.IsTrue(GroupPermissions.HasAuditTrail(EnumPermType.ChartViewsEdit),
				"ChartViewsEdit writes audit entries (add, edit, delete, move), so it belongs in the audit trail filter.");
		}

		#region Database
		private static void Prepare() {
			string skipReason=ScratchDb.EnsureCreated();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			ScratchDb.Reset("securitylog","securityloghash","chartview");
		}

		[TestMethod]
		public void Log_WritesEntriesUnderChartViewsEdit_WithTheView() {
			Prepare();
			ChartView chartView=NewView();
			chartView.ChartViewNum=0;
			ChartViews.Insert(chartView);
			ChartViews.LogAdded(chartView);
			ChartView chartViewOld=chartView.Copy();
			chartView.IsAudit=true;
			ChartViews.Update(chartView);
			Assert.IsTrue(ChartViews.LogEdited(chartViewOld,chartView,NewColumns(),NewColumns()));
			Assert.IsFalse(ChartViews.LogEdited(chartView,chartView.Copy(),NewColumns(),NewColumns()),"Saving without a change writes nothing.");
			ChartViews.LogMoved(chartView,1,0);
			ChartViews.LogMoved(chartView,1,1);//Not a move.
			ChartViews.Delete(chartView.ChartViewNum);
			ChartViews.LogDeleted(chartView);
			List<string> listTexts=DataCore.GetTable("SELECT PermType,FKey,LogText FROM securitylog ORDER BY SecurityLogNum").Select()
				.Select(x => x[0]+"|"+x[1]+"|"+x[2]).ToList();
			string prefix=(int)EnumPermType.ChartViewsEdit+"|"+chartView.ChartViewNum+"|";
			CollectionAssert.AreEqual(new[] {
				prefix+"Chart view 'Hygiene' added.",
				prefix+"Chart view 'Hygiene' edited: audit mode off to on.",
				prefix+"Chart view 'Hygiene' moved from position 2 to 1.",
				prefix+"Chart view 'Hygiene' deleted.",
			},listTexts);
		}
		#endregion
	}
}
