// The rows, statuses, filter and actions of the Patient Forms window (Open Dental 25.2 "Patient Forms window improvements", backlog package B-EF).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Which forms the Patient Forms window lists. Not stored in a table column: the choice is kept per user in a userodpref row's
	///ValueString (UserOdFkeyType.PatientFormsView).</summary>
	public enum EnumPatientFormsShow {
		///<summary>0 - Sheets, eForms and scanned form documents.</summary>
		Both,
		///<summary>1 - Sheets and scanned form documents, the forms the window listed before eForms.</summary>
		Sheets,
		///<summary>2 - eForms only.</summary>
		EForms,
	}

	///<summary>How the Patient Forms window sorts its rows, kept per user with EnumPatientFormsShow.</summary>
	public enum EnumPatientFormsSort {
		///<summary>0 - Date and time.</summary>
		DateTime,
		///<summary>1 - Description, then date and time.</summary>
		DescriptionDateTime,
	}

	///<summary>The kind of form a row of the Patient Forms window holds.</summary>
	public enum EnumPatientFormRowType {
		Sheet,
		EForm,
		///<summary>A document in an image category that is flagged to show in Patient Forms, usually a scanned paper form.</summary>
		Document,
	}

	///<summary>The toolbar actions of the Patient Forms window, in the order they show.</summary>
	public enum EnumPatientFormAction {
		AddSheet,
		AddEForm,
		PreFill,
		Copy,
		Import,
		Kiosk,
	}

	///<summary>Whether one toolbar action is available for the current selection, and the tooltip to show: what the action does, or why it is
	///disabled. Tooltips are English; the window translates them.</summary>
	public class PatientFormActionState {
		public EnumPatientFormAction Action;
		public bool IsEnabled;
		public string ToolTip;
	}

	///<summary>Logic behind the Patient Forms window: the Status column, the Show filter, which toolbar actions apply to the selected row, the audit
	///entries that record an import, and the per-user view settings. The window itself is OpenDental/Forms/FormPatientForms.cs.
	///eClipboard and Web Forms are not part of this program. Rows they created in an office database still show, with a neutral status.</summary>
	public class PatientFormsList {
		///<summary>Column added to the patient forms table: the text of the Status column.</summary>
		public const string COLUMN_STATUS="status";
		///<summary>Column added to the patient forms table: "1" when the row can be imported into the database (an eForm, or a patient form or medical
		///history sheet), otherwise "0".</summary>
		public const string COLUMN_IMPORTABLE="isImportable";
		public const string STATUS_KIOSK="Kiosk";
		///<summary>A sheet that came in through Open Dental's Web Forms service (sheet.IsWebForm). That service is not part of this program.</summary>
		public const string STATUS_SUBMITTED_ONLINE="Submitted online";
		public const string STATUS_IMPORTED="Imported";
		public const string STATUS_NONE="None";
		///<summary>eform.Status 1 (ShowInEClipboard): Open Dental queued the form for the patient to fill on a device. That service is not part of
		///this program, so the form waits; staff can still fill it here.</summary>
		public const string STATUS_WAITING="Waiting for patient";
		public const string STATUS_FILLED="Filled";

		#region Patient forms table
		///<summary>The patient forms table (Sheets.GetPatientFormsTable) with the Status and importable columns added.</summary>
		public static DataTable GetTable(long patNum) {
			Meth.NoCheckMiddleTierRole();
			DataTable table=Sheets.GetPatientFormsTable(patNum);
			AddStatusColumns(table,GetStatusTable(patNum));
			return table;
		}

		///<summary>One row per sheet and eForm of the patient with what the Status column needs: FormType ("Sheet" or "EForm"), FormNum, IsKiosk,
		///IsWebForm, IsImportable, EFormStatus and IsImported (an import audit entry exists, see IsImportLog).</summary>
		public static DataTable GetStatusTable(long patNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),patNum);
			}
			DataTable table=new DataTable("PatientFormStatus");
			table.Columns.Add("FormType");
			table.Columns.Add("FormNum",typeof(long));
			table.Columns.Add("IsKiosk",typeof(bool));
			table.Columns.Add("IsWebForm",typeof(bool));
			table.Columns.Add("IsImportable",typeof(bool));
			table.Columns.Add("EFormStatus",typeof(int));
			table.Columns.Add("IsImported",typeof(bool));
			string command="SELECT SheetNum,SheetType,ShowInTerminal,IsWebForm FROM sheet WHERE IsDeleted=0 AND PatNum="+POut.Long(patNum);
			DataTable tableSheets=Db.GetTable(command);
			command="SELECT EFormNum,Status FROM eform WHERE PatNum="+POut.Long(patNum);
			DataTable tableEForms=Db.GetTable(command);
			List<long> listSheetNums=tableSheets.Rows.OfType<DataRow>().Select(x => PIn.Long(x["SheetNum"].ToString())).ToList();
			List<long> listEFormNums=tableEForms.Rows.OfType<DataRow>().Select(x => PIn.Long(x["EFormNum"].ToString())).ToList();
			HashSet<long> hashSetSheetNumsImported=new HashSet<long>();
			HashSet<long> hashSetEFormNumsImported=new HashSet<long>();
			List<long> listFKeys=listSheetNums.Concat(listEFormNums).Distinct().Where(x => x!=0).ToList();
			if(listFKeys.Count>0) {
				command="SELECT FKey,PermType,LogText FROM securitylog "
					+"WHERE PermType IN ("+POut.Int((int)EnumPermType.SheetEdit)+","+POut.Int((int)EnumPermType.EFormImport)+") "
					+"AND FKey IN ("+string.Join(",",listFKeys.Select(x => POut.Long(x)))+")";
				DataTable tableLogs=Db.GetTable(command);
				for(int i=0;i<tableLogs.Rows.Count;i++) {
					long fKey=PIn.Long(tableLogs.Rows[i]["FKey"].ToString());
					EnumPermType permType=(EnumPermType)PIn.Int(tableLogs.Rows[i]["PermType"].ToString());
					string logText=tableLogs.Rows[i]["LogText"].ToString();
					if(permType==EnumPermType.SheetEdit && listSheetNums.Contains(fKey) && IsImportLog(permType,fKey,logText)) {
						hashSetSheetNumsImported.Add(fKey);
					}
					if(permType==EnumPermType.EFormImport && listEFormNums.Contains(fKey) && IsImportLog(permType,fKey,logText)) {
						hashSetEFormNumsImported.Add(fKey);
					}
				}
			}
			for(int i=0;i<tableSheets.Rows.Count;i++) {
				DataRow dataRow=table.NewRow();
				long sheetNum=PIn.Long(tableSheets.Rows[i]["SheetNum"].ToString());
				dataRow["FormType"]=EnumPatientFormRowType.Sheet.ToString();
				dataRow["FormNum"]=sheetNum;
				dataRow["IsKiosk"]=PIn.Int(tableSheets.Rows[i]["ShowInTerminal"].ToString())>0;
				dataRow["IsWebForm"]=PIn.Bool(tableSheets.Rows[i]["IsWebForm"].ToString());
				dataRow["IsImportable"]=SheetDefs.IsWebFormAllowed((SheetTypeEnum)PIn.Int(tableSheets.Rows[i]["SheetType"].ToString()));
				dataRow["EFormStatus"]=0;
				dataRow["IsImported"]=hashSetSheetNumsImported.Contains(sheetNum);
				table.Rows.Add(dataRow);
			}
			for(int i=0;i<tableEForms.Rows.Count;i++) {
				DataRow dataRow=table.NewRow();
				long eFormNum=PIn.Long(tableEForms.Rows[i]["EFormNum"].ToString());
				dataRow["FormType"]=EnumPatientFormRowType.EForm.ToString();
				dataRow["FormNum"]=eFormNum;
				dataRow["IsKiosk"]=false;
				dataRow["IsWebForm"]=false;
				dataRow["IsImportable"]=true;
				dataRow["EFormStatus"]=(int)EFormStatusCompat.FromDb(PIn.Int(tableEForms.Rows[i]["Status"].ToString()));
				dataRow["IsImported"]=hashSetEFormNumsImported.Contains(eFormNum);
				table.Rows.Add(dataRow);
			}
			return table;
		}

		///<summary>Adds the Status and importable columns to the patient forms table from GetStatusTable. A row with no status row (a document, or a
		///form deleted in between) gets an empty status. Documents are never importable.</summary>
		public static void AddStatusColumns(DataTable tableForms,DataTable tableStatus) {
			Meth.NoCheckMiddleTierRole();
			if(!tableForms.Columns.Contains(COLUMN_STATUS)) {
				tableForms.Columns.Add(COLUMN_STATUS);
			}
			if(!tableForms.Columns.Contains(COLUMN_IMPORTABLE)) {
				tableForms.Columns.Add(COLUMN_IMPORTABLE);
			}
			Dictionary<string,DataRow> dictStatus=new Dictionary<string,DataRow>();
			if(tableStatus!=null) {
				for(int i=0;i<tableStatus.Rows.Count;i++) {
					dictStatus[tableStatus.Rows[i]["FormType"].ToString()+"#"+tableStatus.Rows[i]["FormNum"].ToString()]=tableStatus.Rows[i];
				}
			}
			for(int i=0;i<tableForms.Rows.Count;i++) {
				DataRow dataRow=tableForms.Rows[i];
				EnumPatientFormRowType rowType=GetRowType(dataRow);
				dataRow[COLUMN_STATUS]="";
				dataRow[COLUMN_IMPORTABLE]="0";
				if(rowType==EnumPatientFormRowType.Document) {
					continue;
				}
				string key=rowType==EnumPatientFormRowType.Sheet
					? EnumPatientFormRowType.Sheet.ToString()+"#"+PIn.Long(dataRow["SheetNum"].ToString())
					: EnumPatientFormRowType.EForm.ToString()+"#"+PIn.Long(dataRow["EFormNum"].ToString());
				if(!dictStatus.TryGetValue(key,out DataRow dataRowStatus)) {
					continue;
				}
				bool isImported=PIn.Bool(dataRowStatus["IsImported"].ToString());
				if(rowType==EnumPatientFormRowType.Sheet) {
					dataRow[COLUMN_STATUS]=GetSheetStatusText(PIn.Bool(dataRowStatus["IsKiosk"].ToString()),PIn.Bool(dataRowStatus["IsWebForm"].ToString()),
						isImported);
				}
				else {
					dataRow[COLUMN_STATUS]=GetEFormStatusText(EFormStatusCompat.FromDb(PIn.Int(dataRowStatus["EFormStatus"].ToString())),isImported);
				}
				dataRow[COLUMN_IMPORTABLE]=PIn.Bool(dataRowStatus["IsImportable"].ToString()) ? "1" : "0";
			}
		}

		///<summary>The kind of form in a row of the patient forms table, tested in the same order the grid always used: document, sheet, eForm.</summary>
		public static EnumPatientFormRowType GetRowType(DataRow dataRow) {
			Meth.NoCheckMiddleTierRole();
			if(PIn.Long(dataRow["DocNum"].ToString())!=0) {
				return EnumPatientFormRowType.Document;
			}
			if(PIn.Long(dataRow["SheetNum"].ToString())!=0) {
				return EnumPatientFormRowType.Sheet;
			}
			return EnumPatientFormRowType.EForm;
		}

		///<summary>True if the Show filter lists this kind of row. Sheets lists sheets and scanned form documents, the list the window had before
		///eForms; eForms lists only eForms; Both lists everything.</summary>
		public static bool IsShown(EnumPatientFormRowType rowType,EnumPatientFormsShow show) {
			Meth.NoCheckMiddleTierRole();
			switch(show) {
				case EnumPatientFormsShow.Sheets:
					return rowType!=EnumPatientFormRowType.EForm;
				case EnumPatientFormsShow.EForms:
					return rowType==EnumPatientFormRowType.EForm;
				default:
					return true;
			}
		}

		///<summary>A copy of the patient forms table with only the rows the Show filter lists. The table passed in is not changed.</summary>
		public static DataTable Filter(DataTable table,EnumPatientFormsShow show) {
			Meth.NoCheckMiddleTierRole();
			DataTable tableFiltered=table.Clone();
			for(int i=0;i<table.Rows.Count;i++) {
				if(IsShown(GetRowType(table.Rows[i]),show)) {
					tableFiltered.ImportRow(table.Rows[i]);
				}
			}
			return tableFiltered;
		}

		///<summary>True if any row of the patient forms table is set to show in the kiosk. The Kiosk action needs one.</summary>
		public static bool HasKioskForms(DataTable table) {
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<table.Rows.Count;i++) {
				if(table.Rows[i]["showInTerminal"].ToString()!="") {
					return true;
				}
			}
			return false;
		}
		#endregion Patient forms table

		#region Status text
		///<summary>Status of a sheet from today's flags: Kiosk (in the kiosk queue), Submitted online (came from a web form) and Imported (an import
		///audit entry exists). Several can apply. Empty when none does.</summary>
		public static string GetSheetStatusText(bool isKiosk,bool isWebForm,bool isImported) {
			Meth.NoCheckMiddleTierRole();
			List<string> listParts=new List<string>();
			if(isKiosk) {
				listParts.Add(STATUS_KIOSK);
			}
			if(isWebForm) {
				listParts.Add(STATUS_SUBMITTED_ONLINE);
			}
			if(isImported) {
				listParts.Add(STATUS_IMPORTED);
			}
			return string.Join(", ",listParts);
		}

		///<summary>Status of an eForm: Waiting for patient (status 1), Filled (status 2), and Imported when an import audit entry exists, because
		///since Open Dental 25.2 the status itself no longer records the import. None when nothing applies.</summary>
		public static string GetEFormStatusText(EnumEFormStatus status,bool isImported) {
			Meth.NoCheckMiddleTierRole();
			List<string> listParts=new List<string>();
			if(status==EnumEFormStatus.ShowInEClipboard) {
				listParts.Add(STATUS_WAITING);
			}
			else if(status==EnumEFormStatus.Filled) {
				listParts.Add(STATUS_FILLED);
			}
			if(isImported) {
				listParts.Add(STATUS_IMPORTED);
			}
			if(listParts.Count==0) {
				return STATUS_NONE;
			}
			return string.Join(", ",listParts);
		}
		#endregion Status text

		#region Import audit entries
		///<summary>The audit entry text (permission SheetEdit, FKey SheetNum) that records a sheet import from the Import window.</summary>
		public static string GetImportLogTextSheet(long sheetNum,string description) {
			Meth.NoCheckMiddleTierRole();
			return "Sheet #"+sheetNum+" ("+description+") imported";
		}

		///<summary>The audit entry text (permission EFormImport, FKey EFormNum) that records an eForm import from the Import window.</summary>
		public static string GetImportLogTextEForm(long eFormNum,string description) {
			Meth.NoCheckMiddleTierRole();
			return "eForm #"+eFormNum+" ("+description+") imported";
		}

		///<summary>True if an audit entry records the import of the form fKey points to. Sheets: permission SheetEdit with the text of
		///GetImportLogTextSheet, because other SheetEdit entries (signatures) also carry the SheetNum. eForms: permission EFormImport with FKey set and
		///"imported" in the text, so the pending-insurance entries the import also makes don't count.</summary>
		public static bool IsImportLog(EnumPermType permType,long fKey,string logText) {
			Meth.NoCheckMiddleTierRole();
			if(fKey==0 || string.IsNullOrEmpty(logText)) {
				return false;
			}
			if(permType==EnumPermType.SheetEdit) {
				return logText.StartsWith("Sheet #"+fKey+" (") && logText.EndsWith(") imported");
			}
			if(permType==EnumPermType.EFormImport) {
				return logText.IndexOf("imported",StringComparison.OrdinalIgnoreCase)>=0
					&& !logText.StartsWith("Pending insurance",StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
		#endregion Import audit entries

		#region Toolbar actions
		///<summary>Which toolbar actions apply, in toolbar order. countSelected is the number of selected rows, rowTypeSelected the kind of the
		///selected row (ignored unless exactly one is selected), isSelectedImportable whether that row can be imported, hasKioskForms whether any form
		///of the patient is set to show in the kiosk. Adding a sheet or an eForm is always available.</summary>
		public static List<PatientFormActionState> GetActionStates(int countSelected,EnumPatientFormRowType rowTypeSelected,bool isSelectedImportable,
			bool hasKioskForms)
		{
			Meth.NoCheckMiddleTierRole();
			List<PatientFormActionState> listStates=new List<PatientFormActionState>();
			listStates.Add(Enabled(EnumPatientFormAction.AddSheet,"Add a sheet for this patient"));
			listStates.Add(Enabled(EnumPatientFormAction.AddEForm,"Add an eForm for this patient"));
			//Pre-Fill and Copy work on one sheet.
			string reasonSheetOnly=GetReasonSheetOnly(countSelected,rowTypeSelected);
			if(reasonSheetOnly=="") {
				listStates.Add(Enabled(EnumPatientFormAction.PreFill,"Make a copy of the selected sheet, but pull new info from the database where possible"));
				listStates.Add(Enabled(EnumPatientFormAction.Copy,"Make a copy of the selected sheet, without its signatures"));
			}
			else {
				listStates.Add(Disabled(EnumPatientFormAction.PreFill,reasonSheetOnly));
				listStates.Add(Disabled(EnumPatientFormAction.Copy,reasonSheetOnly));
			}
			//Import works on one sheet of an importable type, or one eForm.
			if(countSelected==0) {
				listStates.Add(Disabled(EnumPatientFormAction.Import,"Select one completed form first"));
			}
			else if(countSelected>1) {
				listStates.Add(Disabled(EnumPatientFormAction.Import,"Select only one form"));
			}
			else if(rowTypeSelected==EnumPatientFormRowType.Document) {
				listStates.Add(Disabled(EnumPatientFormAction.Import,"Scanned documents (PDFs) can't be imported"));
			}
			else if(rowTypeSelected==EnumPatientFormRowType.Sheet && !isSelectedImportable) {
				listStates.Add(Disabled(EnumPatientFormAction.Import,"Only patient form and medical history sheets can be imported"));
			}
			else {
				listStates.Add(Enabled(EnumPatientFormAction.Import,"Import information from the selected form into the database"));
			}
			if(hasKioskForms) {
				listStates.Add(Enabled(EnumPatientFormAction.Kiosk,"Open the kiosk with this patient's forms"));
			}
			else {
				listStates.Add(Disabled(EnumPatientFormAction.Kiosk,"No forms for this patient are set to show in the kiosk"));
			}
			return listStates;
		}

		private static string GetReasonSheetOnly(int countSelected,EnumPatientFormRowType rowTypeSelected) {
			if(countSelected==0) {
				return "Select one sheet first";
			}
			if(countSelected>1) {
				return "Select only one sheet";
			}
			if(rowTypeSelected==EnumPatientFormRowType.EForm) {
				return "Works with sheets only, not eForms";
			}
			if(rowTypeSelected==EnumPatientFormRowType.Document) {
				return "Works with sheets only, not scanned documents";
			}
			return "";
		}

		private static PatientFormActionState Enabled(EnumPatientFormAction action,string toolTip) {
			return new PatientFormActionState() { Action=action,IsEnabled=true,ToolTip=toolTip };
		}

		private static PatientFormActionState Disabled(EnumPatientFormAction action,string reason) {
			return new PatientFormActionState() { Action=action,IsEnabled=false,ToolTip=reason };
		}
		#endregion Toolbar actions

		#region Per-user view
		///<summary>The ValueString of the user's UserOdFkeyType.PatientFormsView row, like "Show=2;Sort=0".</summary>
		public static string SerializeView(EnumPatientFormsShow show,EnumPatientFormsSort sort) {
			Meth.NoCheckMiddleTierRole();
			return "Show="+(int)show+";Sort="+(int)sort;
		}

		///<summary>Reads a ValueString written by SerializeView. Missing, unknown or unreadable parts give the defaults: Both, sorted by date and
		///time.</summary>
		public static void ParseView(string valueString,out EnumPatientFormsShow show,out EnumPatientFormsSort sort) {
			Meth.NoCheckMiddleTierRole();
			show=EnumPatientFormsShow.Both;
			sort=EnumPatientFormsSort.DateTime;
			if(string.IsNullOrWhiteSpace(valueString)) {
				return;
			}
			string[] arrayParts=valueString.Split(';');
			for(int i=0;i<arrayParts.Length;i++) {
				string[] arrayKeyValue=arrayParts[i].Split('=');
				if(arrayKeyValue.Length!=2 || !int.TryParse(arrayKeyValue[1].Trim(),out int value)) {
					continue;
				}
				string key=arrayKeyValue[0].Trim();
				if(key.Equals("Show",StringComparison.OrdinalIgnoreCase) && Enum.IsDefined(typeof(EnumPatientFormsShow),value)) {
					show=(EnumPatientFormsShow)value;
				}
				if(key.Equals("Sort",StringComparison.OrdinalIgnoreCase) && Enum.IsDefined(typeof(EnumPatientFormsSort),value)) {
					sort=(EnumPatientFormsSort)value;
				}
			}
		}

		///<summary>The user's view settings from the userodpref cache. Defaults when the user has none.</summary>
		public static void GetViewForUser(long userNum,out EnumPatientFormsShow show,out EnumPatientFormsSort sort) {
			Meth.NoCheckMiddleTierRole();
			UserOdPref userOdPref=UserOdPrefs.GetByUserAndFkeyType(userNum,UserOdFkeyType.PatientFormsView).FirstOrDefault();
			ParseView(userOdPref?.ValueString,out show,out sort);
		}

		///<summary>Saves the user's view settings in their userodpref row, adding the row the first time, and refreshes this workstation's userodpref
		///cache so the next save finds the row. Returns true if the database changed, so the caller can tell other workstations to refresh too.
		///</summary>
		public static bool SaveViewForUser(long userNum,EnumPatientFormsShow show,EnumPatientFormsSort sort) {
			Meth.NoCheckMiddleTierRole();
			if(userNum==0) {
				return false;
			}
			UserOdPref userOdPref=UserOdPrefs.GetFirstOrNewByUserAndFkeyType(userNum,UserOdFkeyType.PatientFormsView);
			string valueString=SerializeView(show,sort);
			if(userOdPref.UserOdPrefNum!=0 && userOdPref.ValueString==valueString) {
				return false;
			}
			userOdPref.ValueString=valueString;
			UserOdPrefs.Upsert(userOdPref);
			UserOdPrefs.RefreshCache();
			return true;
		}
		#endregion Per-user view
	}
}
