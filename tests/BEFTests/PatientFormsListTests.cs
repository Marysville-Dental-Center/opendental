// Logic tests for the Patient Forms window improvements (Open Dental 25.2, our design; package B-EF). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BEFTests {
	[TestClass]
	public class PatientFormsListTests {
		///<summary>A patient forms table with the columns Sheets.GetPatientFormsTable makes.</summary>
		private static DataTable FormsTable() {
			DataTable table=new DataTable("");
			foreach(string column in new[] { "date","description","DocNum","EFormNum","imageCat","SheetNum","showInTerminal","time" }) {
				table.Columns.Add(column);
			}
			table.Columns.Add("dateOnly",typeof(DateTime));
			table.Columns.Add("dateTime",typeof(DateTime));
			table.Columns.Add("DateTSheetEdited",typeof(DateTime));
			table.Columns.Add("timeOnly",typeof(TimeSpan));
			return table;
		}

		private static DataRow AddFormRow(DataTable table,long sheetNum,long eFormNum,long docNum,string showInTerminal="") {
			DataRow dataRow=table.NewRow();
			dataRow["SheetNum"]=sheetNum.ToString();
			dataRow["EFormNum"]=eFormNum.ToString();
			dataRow["DocNum"]=docNum.ToString();
			dataRow["showInTerminal"]=showInTerminal;
			dataRow["description"]="Synthetic form";
			dataRow["dateTime"]=new DateTime(2026,10,1,9,30,0);
			table.Rows.Add(dataRow);
			return dataRow;
		}

		private static DataTable StatusTable() {
			DataTable table=new DataTable("PatientFormStatus");
			table.Columns.Add("FormType");
			table.Columns.Add("FormNum",typeof(long));
			table.Columns.Add("IsKiosk",typeof(bool));
			table.Columns.Add("IsWebForm",typeof(bool));
			table.Columns.Add("IsImportable",typeof(bool));
			table.Columns.Add("EFormStatus",typeof(int));
			table.Columns.Add("IsImported",typeof(bool));
			return table;
		}

		private static void AddStatusRow(DataTable table,EnumPatientFormRowType rowType,long formNum,bool isKiosk=false,bool isWebForm=false,
			bool isImportable=true,EnumEFormStatus status=EnumEFormStatus.None,bool isImported=false)
		{
			table.Rows.Add(rowType.ToString(),formNum,isKiosk,isWebForm,isImportable,(int)status,isImported);
		}

		private static PatientFormActionState State(List<PatientFormActionState> listStates,EnumPatientFormAction action) {
			return listStates.Single(x => x.Action==action);
		}

		[TestMethod]
		public void SheetStatus_FromTodaysFlags() {
			Assert.AreEqual("",PatientFormsList.GetSheetStatusText(false,false,false));
			Assert.AreEqual("Kiosk",PatientFormsList.GetSheetStatusText(true,false,false));
			Assert.AreEqual("Submitted online",PatientFormsList.GetSheetStatusText(false,true,false));
			Assert.AreEqual("Imported",PatientFormsList.GetSheetStatusText(false,false,true));
			Assert.AreEqual("Kiosk, Submitted online, Imported",PatientFormsList.GetSheetStatusText(true,true,true));
		}

		[TestMethod]
		public void EFormStatus_NoneWaitingFilledAndImported() {
			Assert.AreEqual("None",PatientFormsList.GetEFormStatusText(EnumEFormStatus.None,false));
			Assert.AreEqual("Waiting for patient",PatientFormsList.GetEFormStatusText(EnumEFormStatus.ShowInEClipboard,false));
			Assert.AreEqual("Filled",PatientFormsList.GetEFormStatusText(EnumEFormStatus.Filled,false));
			Assert.AreEqual("Filled, Imported",PatientFormsList.GetEFormStatusText(EnumEFormStatus.Filled,true));
			Assert.AreEqual("Imported",PatientFormsList.GetEFormStatusText(EnumEFormStatus.None,true));
		}

		[TestMethod]
		public void Statuses_NeverNameTheCutHostedServices() {
			//eClipboard and Web Forms are not part of this program; the window must not show them. Existing rows get neutral words.
			List<string> listTexts=new List<string>();
			foreach(EnumEFormStatus status in Enum.GetValues(typeof(EnumEFormStatus))) {
				listTexts.Add(PatientFormsList.GetEFormStatusText(status,true));
				listTexts.Add(PatientFormsList.GetEFormStatusText(status,false));
			}
			listTexts.Add(PatientFormsList.GetSheetStatusText(true,true,true));
			List<PatientFormActionState> listStates=new List<PatientFormActionState>();
			foreach(EnumPatientFormRowType rowType in Enum.GetValues(typeof(EnumPatientFormRowType))) {
				for(int count=0;count<3;count++) {
					listStates.AddRange(PatientFormsList.GetActionStates(count,rowType,true,true));
					listStates.AddRange(PatientFormsList.GetActionStates(count,rowType,false,false));
				}
			}
			listTexts.AddRange(listStates.Select(x => x.ToolTip));
			foreach(string text in listTexts) {
				Assert.IsFalse(text.IndexOf("clipboard",StringComparison.OrdinalIgnoreCase)>=0,text);
				Assert.IsFalse(text.IndexOf("web form",StringComparison.OrdinalIgnoreCase)>=0,text);
			}
		}

		[TestMethod]
		public void ImportLog_TextsRoundTrip() {
			string textSheet=PatientFormsList.GetImportLogTextSheet(42,"Registration (adult)");
			Assert.AreEqual("Sheet #42 (Registration (adult)) imported",textSheet);
			Assert.IsTrue(PatientFormsList.IsImportLog(EnumPermType.SheetEdit,42,textSheet));
			Assert.IsFalse(PatientFormsList.IsImportLog(EnumPermType.SheetEdit,43,textSheet),"Another sheet's entry.");
			string textEForm=PatientFormsList.GetImportLogTextEForm(7,"Medical History");
			Assert.AreEqual("eForm #7 (Medical History) imported",textEForm,"Same text the import window wrote before B-EF.");
			Assert.IsTrue(PatientFormsList.IsImportLog(EnumPermType.EFormImport,7,textEForm));
		}

		[TestMethod]
		public void ImportLog_OtherEntriesDoNotCount() {
			//Signature entries also carry the SheetNum.
			Assert.IsFalse(PatientFormsList.IsImportLog(EnumPermType.SheetEdit,42,"Signed by Admin, Registration from 10/1/2026"));
			Assert.IsFalse(PatientFormsList.IsImportLog(EnumPermType.SheetEdit,0,"Sheet #0 (x) imported"));
			//Pending insurance made by an import is logged under EFormImport too.
			Assert.IsFalse(PatientFormsList.IsImportLog(EnumPermType.EFormImport,7,"Pending insurance created from eForm #7 (Reg): Synthetic Carrier, imported"));
			Assert.IsFalse(PatientFormsList.IsImportLog(EnumPermType.EFormImport,0,"eForm #7 (Reg) imported"),"No FKey.");
			Assert.IsFalse(PatientFormsList.IsImportLog(EnumPermType.PatientEdit,7,"eForm #7 (Reg) imported"));
			Assert.IsFalse(PatientFormsList.IsImportLog(EnumPermType.EFormImport,7,""));
		}

		[TestMethod]
		public void Actions_InFixedOrder_AddAlwaysAvailable() {
			List<PatientFormActionState> listStates=PatientFormsList.GetActionStates(0,EnumPatientFormRowType.Sheet,false,false);
			CollectionAssert.AreEqual(new[] { EnumPatientFormAction.AddSheet,EnumPatientFormAction.AddEForm,EnumPatientFormAction.PreFill,
				EnumPatientFormAction.Copy,EnumPatientFormAction.Import,EnumPatientFormAction.Kiosk },listStates.Select(x => x.Action).ToArray());
			Assert.IsTrue(State(listStates,EnumPatientFormAction.AddSheet).IsEnabled);
			Assert.IsTrue(State(listStates,EnumPatientFormAction.AddEForm).IsEnabled);
			//Nothing selected: the row actions are disabled and say why.
			Assert.IsFalse(State(listStates,EnumPatientFormAction.PreFill).IsEnabled);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.Copy).IsEnabled);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.Import).IsEnabled);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.Kiosk).IsEnabled);
			Assert.IsTrue(listStates.All(x => !string.IsNullOrWhiteSpace(x.ToolTip)),"Every action has a tooltip.");
		}

		[TestMethod]
		public void Actions_ForSelectedSheet() {
			List<PatientFormActionState> listStates=PatientFormsList.GetActionStates(1,EnumPatientFormRowType.Sheet,true,true);
			Assert.IsTrue(listStates.All(x => x.IsEnabled));
			//A consent sheet can be copied but not imported.
			listStates=PatientFormsList.GetActionStates(1,EnumPatientFormRowType.Sheet,false,true);
			Assert.IsTrue(State(listStates,EnumPatientFormAction.PreFill).IsEnabled);
			Assert.IsTrue(State(listStates,EnumPatientFormAction.Copy).IsEnabled);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.Import).IsEnabled);
			StringAssert.Contains(State(listStates,EnumPatientFormAction.Import).ToolTip,"patient form and medical history");
		}

		[TestMethod]
		public void Actions_ForSelectedEFormAndDocument() {
			List<PatientFormActionState> listStates=PatientFormsList.GetActionStates(1,EnumPatientFormRowType.EForm,true,false);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.PreFill).IsEnabled);
			StringAssert.Contains(State(listStates,EnumPatientFormAction.PreFill).ToolTip,"sheets only");
			Assert.IsFalse(State(listStates,EnumPatientFormAction.Copy).IsEnabled);
			Assert.IsTrue(State(listStates,EnumPatientFormAction.Import).IsEnabled);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.Kiosk).IsEnabled);
			StringAssert.Contains(State(listStates,EnumPatientFormAction.Kiosk).ToolTip,"kiosk");
			listStates=PatientFormsList.GetActionStates(1,EnumPatientFormRowType.Document,false,true);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.PreFill).IsEnabled);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.Copy).IsEnabled);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.Import).IsEnabled);
			StringAssert.Contains(State(listStates,EnumPatientFormAction.Import).ToolTip,"documents");
			Assert.IsTrue(State(listStates,EnumPatientFormAction.Kiosk).IsEnabled,"Kiosk depends on the patient's forms, not on the selection.");
			listStates=PatientFormsList.GetActionStates(2,EnumPatientFormRowType.Sheet,true,true);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.Import).IsEnabled);
			Assert.IsFalse(State(listStates,EnumPatientFormAction.Copy).IsEnabled);
		}

		[TestMethod]
		public void ShowFilter_SheetsKeepDocuments_EFormsOnlyEForms() {
			DataTable table=FormsTable();
			AddFormRow(table,sheetNum:1,eFormNum:0,docNum:0);
			AddFormRow(table,sheetNum:0,eFormNum:2,docNum:0);
			AddFormRow(table,sheetNum:0,eFormNum:0,docNum:3);
			AddFormRow(table,sheetNum:4,eFormNum:0,docNum:0);
			Assert.AreEqual(4,PatientFormsList.Filter(table,EnumPatientFormsShow.Both).Rows.Count);
			DataTable tableSheets=PatientFormsList.Filter(table,EnumPatientFormsShow.Sheets);
			CollectionAssert.AreEqual(new[] { "1","0","4" },tableSheets.Rows.OfType<DataRow>().Select(x => x["SheetNum"].ToString()).ToArray());
			DataTable tableEForms=PatientFormsList.Filter(table,EnumPatientFormsShow.EForms);
			Assert.AreEqual(1,tableEForms.Rows.Count);
			Assert.AreEqual("2",tableEForms.Rows[0]["EFormNum"].ToString());
			Assert.AreEqual(4,table.Rows.Count,"The table passed in is not changed.");
			Assert.AreEqual(table.Columns.Count,tableEForms.Columns.Count,"The filtered table keeps every column.");
		}

		[TestMethod]
		public void RowType_DocumentThenSheetThenEForm() {
			DataTable table=FormsTable();
			Assert.AreEqual(EnumPatientFormRowType.Document,PatientFormsList.GetRowType(AddFormRow(table,0,0,9)));
			Assert.AreEqual(EnumPatientFormRowType.Sheet,PatientFormsList.GetRowType(AddFormRow(table,5,0,0)));
			Assert.AreEqual(EnumPatientFormRowType.EForm,PatientFormsList.GetRowType(AddFormRow(table,0,6,0)));
		}

		[TestMethod]
		public void AddStatusColumns_MergesByFormTypeAndNumber() {
			DataTable table=FormsTable();
			DataRow rowSheet=AddFormRow(table,sheetNum:10,eFormNum:0,docNum:0,showInTerminal:"1");
			DataRow rowEForm=AddFormRow(table,sheetNum:0,eFormNum:10,docNum:0);
			DataRow rowDoc=AddFormRow(table,sheetNum:0,eFormNum:0,docNum:10);
			DataRow rowConsent=AddFormRow(table,sheetNum:11,eFormNum:0,docNum:0);
			DataRow rowMissing=AddFormRow(table,sheetNum:12,eFormNum:0,docNum:0);
			DataTable tableStatus=StatusTable();
			//Sheet 10 and eForm 10 share a number; the merge keeps them apart.
			AddStatusRow(tableStatus,EnumPatientFormRowType.Sheet,10,isKiosk:true,isImported:true);
			AddStatusRow(tableStatus,EnumPatientFormRowType.EForm,10,status:EnumEFormStatus.ShowInEClipboard);
			AddStatusRow(tableStatus,EnumPatientFormRowType.Sheet,11,isImportable:false);
			PatientFormsList.AddStatusColumns(table,tableStatus);
			Assert.AreEqual("Kiosk, Imported",rowSheet[PatientFormsList.COLUMN_STATUS]);
			Assert.AreEqual("1",rowSheet[PatientFormsList.COLUMN_IMPORTABLE]);
			Assert.AreEqual("Waiting for patient",rowEForm[PatientFormsList.COLUMN_STATUS]);
			Assert.AreEqual("1",rowEForm[PatientFormsList.COLUMN_IMPORTABLE]);
			Assert.AreEqual("",rowDoc[PatientFormsList.COLUMN_STATUS]);
			Assert.AreEqual("0",rowDoc[PatientFormsList.COLUMN_IMPORTABLE]);
			Assert.AreEqual("",rowConsent[PatientFormsList.COLUMN_STATUS]);
			Assert.AreEqual("0",rowConsent[PatientFormsList.COLUMN_IMPORTABLE]);
			Assert.AreEqual("",rowMissing[PatientFormsList.COLUMN_STATUS],"Deleted in between: no status.");
			//Running it again (a refresh) does not add the columns twice.
			PatientFormsList.AddStatusColumns(table,tableStatus);
			Assert.AreEqual(1,table.Columns.OfType<DataColumn>().Count(x => x.ColumnName==PatientFormsList.COLUMN_STATUS));
		}

		[TestMethod]
		public void HasKioskForms_LooksAtEveryRow() {
			DataTable table=FormsTable();
			AddFormRow(table,1,0,0);
			Assert.IsFalse(PatientFormsList.HasKioskForms(table));
			AddFormRow(table,2,0,0,showInTerminal:"2");
			Assert.IsTrue(PatientFormsList.HasKioskForms(table));
		}

		[TestMethod]
		public void View_RoundTripsAndToleratesBadValues() {
			foreach(EnumPatientFormsShow show in Enum.GetValues(typeof(EnumPatientFormsShow))) {
				foreach(EnumPatientFormsSort sort in Enum.GetValues(typeof(EnumPatientFormsSort))) {
					PatientFormsList.ParseView(PatientFormsList.SerializeView(show,sort),out EnumPatientFormsShow showRead,out EnumPatientFormsSort sortRead);
					Assert.AreEqual(show,showRead);
					Assert.AreEqual(sort,sortRead);
				}
			}
			Assert.AreEqual("Show=2;Sort=1",PatientFormsList.SerializeView(EnumPatientFormsShow.EForms,EnumPatientFormsSort.DescriptionDateTime));
			foreach(string bad in new[] { null,"","garbage","Show=9;Sort=-1","Show=x;Sort","=;=;" }) {
				PatientFormsList.ParseView(bad,out EnumPatientFormsShow show,out EnumPatientFormsSort sort);
				Assert.AreEqual(EnumPatientFormsShow.Both,show,bad??"null");
				Assert.AreEqual(EnumPatientFormsSort.DateTime,sort,bad??"null");
			}
			PatientFormsList.ParseView(" sort = 1 ; show = 1 ",out EnumPatientFormsShow showSpaced,out EnumPatientFormsSort sortSpaced);
			Assert.AreEqual(EnumPatientFormsShow.Sheets,showSpaced);
			Assert.AreEqual(EnumPatientFormsSort.DescriptionDateTime,sortSpaced);
		}

		[TestMethod]
		public void UserOdFkeyType_PatientFormsView_IsForkOnlyAndFitsTheColumn() {
			int value=(int)UserOdFkeyType.PatientFormsView;
			Assert.AreEqual(114,value);
			Assert.IsTrue(value<=sbyte.MaxValue,"userodpref.FkeyType is a signed tinyint.");
			Assert.AreEqual(1,Enum.GetValues(typeof(UserOdFkeyType)).Cast<int>().Count(x => x==value),"No other member shares the number.");
			Assert.IsTrue(value>(int)UserOdFkeyType.DefaultMapSetting+50,"Fork values stay clear of Open Dental's numbers (28 at 25.3, 29 at 25.4).");
		}
	}
}
