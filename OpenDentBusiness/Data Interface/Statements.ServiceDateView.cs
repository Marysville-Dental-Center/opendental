// Statements in Service Date View (package M2-SDV in docs/specs/port-backlog.md; Open Dental 26.1 item v26_1-account-service-date-view and the
// statement.IsServiceDateView column). Single and batch statements can be sent with their main grid in service-date layout. Only the main
// grid changes: aging, balance totals, amount due and the dunning text come from the same data as a statement in the regular view.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace OpenDentBusiness {
	public partial class Statements {
		///<summary>Name of the table in a statement DataSet that holds the service-date main grid once it has been built, so redrawing a sheet does
		///not run the query again. Its name does not start with "account", so nothing that reads the account tables sees it.</summary>
		public const string TABLE_NAME_SERVICE_DATE_VIEW="servicedateview";
		///<summary>Bool column of the service-date main grid table: the sheet grid draws the row bold (totals).</summary>
		public const string COLUMN_IS_BOLD="paramIsBold";

		///<summary>Whether the statement may be shown in Service Date View. Limited statements, limited custom statements and super-family
		///statements always use the regular view in this first release (INFERRED scope cut of the 26.1 spec).</summary>
		public static bool CanUseServiceDateView(Statement statement) {
			Meth.NoCheckMiddleTierRole();
			if(statement==null) {
				return false;
			}
			if(statement.StatementType==StmtType.LimitedStatement) {
				return false;
			}
			if(statement.SuperFamily!=0) {
				return false;
			}
			if(statement.LimitedCustomFamily!=EnumLimitedCustomFamily.None) {
				return false;
			}
			return true;
		}

		///<summary>True when the statement's main grid is built in service-date layout: the statement's own setting (statement.IsServiceDateView),
		///so a saved statement prints again in the view it was sent in, and a statement type that supports it.</summary>
		public static bool IsShownInServiceDateView(Statement statement) {
			Meth.NoCheckMiddleTierRole();
			return statement!=null && statement.IsServiceDateView && CanUseServiceDateView(statement);
		}

		///<summary>Sets statement.IsServiceDateView on a new statement: the requested value, but never on a statement type that cannot use it.</summary>
		public static void SetServiceDateView(Statement statement,bool isServiceDateView) {
			Meth.NoCheckMiddleTierRole();
			if(statement==null) {
				return;
			}
			statement.IsServiceDateView=isServiceDateView && CanUseServiceDateView(statement);
		}

		///<summary>The default of the Service Date View box in Billing Options (preference BillingDefaultServiceDateView).</summary>
		public static bool GetBillingDefaultServiceDateView() {
			Meth.NoCheckMiddleTierRole();
			return PrefC.GetBoolSilent(PrefName.BillingDefaultServiceDateView,false);
		}

		///<summary>The statement's main grid in service-date layout, with the columns of the regular account tables of the DataSet (so the
		///Statement Main Grid display fields, the CSV export and the sheet code work unchanged) plus the bool column paramIsBold. One block per
		///account table of the DataSet: "account" holds the single patient or the intermingled family, "account"+PatNum one patient of a statement
		///that is not intermingled; every row of a patient's block carries that PatNum, so the sheet splits the grid by patient as it does for a
		///regular statement. The DataSet must come from AccountModules.GetAccount or GetStatementDataSet for this statement. The result is kept in
		///the DataSet (table servicedateview) and a copy is returned.</summary>
		public static DataTable GetServiceDateViewMainTable(DataSet dataSet,Statement statement) {
			Meth.NoCheckMiddleTierRole();
			List<DataTable> listAccountTables=dataSet.Tables.OfType<DataTable>().Where(x => x.TableName.StartsWith("account")).ToList();
			string key=GetServiceDateViewCacheKey(statement);
			DataTable tableCached=dataSet.Tables[TABLE_NAME_SERVICE_DATE_VIEW];
			if(tableCached!=null && tableCached.ExtendedProperties["key"] as string==key) {
				return tableCached.Copy();
			}
			Family family=Patients.GetFamily(statement.PatNum);
			DataTable table;
			if(listAccountTables.Count>0) {
				table=listAccountTables[0].Clone();
				table.TableName=TABLE_NAME_SERVICE_DATE_VIEW;
			}
			else {
				table=new DataTable(TABLE_NAME_SERVICE_DATE_VIEW);
			}
			AddMissingColumns(table);
			Dictionary<long,string> dictPatNames=GetPatientNamesFromAccountTables(listAccountTables);
			foreach(DataTable tableAccount in listAccountTables) {
				long patNumBlock;
				bool isFamily;
				bool isPerPatient;
				if(tableAccount.TableName=="account") {
					isPerPatient=false;
					if(statement.SinglePatient || !statement.Intermingled) {
						isFamily=false;
						patNumBlock=GetPatNumForGetAccount(statement);
					}
					else {
						isFamily=true;
						patNumBlock=family.Guarantor.PatNum;
					}
				}
				else {
					isPerPatient=true;
					isFamily=false;
					patNumBlock=PIn.Long(tableAccount.TableName.Substring("account".Length));
				}
				ServiceDateViewOptions options=GetServiceDateViewOptions(statement,isFamily,tableAccount);
				List<ServiceDateViewRow> listRows=RpServiceDateView.GetRows(patNumBlock,isFamily,options);
				if(!listRows.Any(x => x.RowKind==ServiceDateViewRowKind.Transaction || x.RowKind==ServiceDateViewRowKind.BalanceForward)) {
					AddNoActivityRow(table,patNumBlock,family,dictPatNames);
					continue;
				}
				foreach(ServiceDateViewRow row in listRows) {
					long patNumRow=row.PatNum;
					if(isPerPatient || patNumRow==0) {
						patNumRow=patNumBlock;
					}
					table.Rows.Add(MakeStatementRow(table,row,patNumRow,statement,family,dictPatNames));
				}
			}
			DataTable tableStore=table.Copy();
			tableStore.TableName=TABLE_NAME_SERVICE_DATE_VIEW;
			tableStore.ExtendedProperties["key"]=key;
			if(tableCached!=null) {
				dataSet.Tables.Remove(tableCached);
			}
			dataSet.Tables.Add(tableStore);
			return table;
		}

		///<summary>The builder options for one block of a statement: receipts list today's (the range's) patient payments with the procedures they
		///pay, invoices list their own charges, other statements show the services dated within the statement's range after a balance forward.
		///Statements never show the insurance estimate per procedure; the Balance column shows each service's remaining balance.</summary>
		public static ServiceDateViewOptions GetServiceDateViewOptions(Statement statement,bool isFamily,DataTable tableAccount) {
			Meth.NoCheckMiddleTierRole();
			ServiceDateViewOptions options=new ServiceDateViewOptions();
			options.IsFamily=isFamily;
			options.IsDetailed=false;
			if(statement.IsInvoice) {
				options.InvoiceItems=GetInvoiceItems(tableAccount);
				return options;
			}
			options.DateFrom=statement.DateRangeFrom;
			options.DateTo=statement.DateRangeTo;
			options.IsReceipt=statement.IsReceipt;
			return options;
		}

		///<summary>The charges listed in an invoice's account table: its procedures, adjustments and payment plan charges.</summary>
		public static ServiceDateViewInvoiceItems GetInvoiceItems(DataTable tableAccount) {
			Meth.NoCheckMiddleTierRole();
			ServiceDateViewInvoiceItems invoiceItems=new ServiceDateViewInvoiceItems();
			if(tableAccount==null) {
				return invoiceItems;
			}
			foreach(DataRow dataRow in tableAccount.Rows) {
				long procNum=PIn.Long(dataRow["ProcNum"].ToString());
				long adjNum=PIn.Long(dataRow["AdjNum"].ToString());
				long payPlanChargeNum=PIn.Long(dataRow["PayPlanChargeNum"].ToString());
				if(procNum!=0) {
					invoiceItems.ListProcNums.Add(procNum);
				}
				else if(adjNum!=0) {
					invoiceItems.ListAdjNums.Add(adjNum);
				}
				else if(payPlanChargeNum!=0) {
					invoiceItems.ListPayPlanChargeNums.Add(payPlanChargeNum);
				}
			}
			return invoiceItems;
		}

		///<summary>The patient-facing description of a service-date row on a statement. Procedures use the same text as the regular statement;
		///other rows keep the Service Date View report's text.</summary>
		public static string GetServiceDateViewDescription(ServiceDateViewRow row) {
			Meth.NoCheckMiddleTierRole();
			switch(row.RowKind) {
				case ServiceDateViewRowKind.UnallocatedHeading:
					return Lans.g("ServiceDateView",ServiceDateViews.REFERENCE_UNALLOCATED);
				case ServiceDateViewRowKind.DayTotal:
					return "  "+Lans.g("ServiceDateView",ServiceDateViews.REFERENCE_DAY_TOTAL.Trim());
				case ServiceDateViewRowKind.OverallTotal:
					return Lans.g("ServiceDateView",row.Reference);
				case ServiceDateViewRowKind.BalanceForward:
					return Lans.g("AccountModule","Balance Forward");
			}
			if(!row.IsProcedure || row.CodeNum==0 || ProcedureCodes.GetProcCode(row.CodeNum).CodeNum!=row.CodeNum) {
				return row.Reference;
			}
			string description=Procedures.ConvertProcToString(row.CodeNum,row.Surf,row.ToothNum,true);
			if(!string.IsNullOrEmpty(row.ToothRange)) {
				description+=" #"+Tooth.DisplayRange(row.ToothRange);
			}
			return description;
		}

		private static string GetServiceDateViewCacheKey(Statement statement) {
			return string.Join("|",statement.StatementNum,statement.PatNum,statement.IsServiceDateView,statement.SinglePatient,statement.Intermingled,
				statement.IsReceipt,statement.IsInvoice,statement.DateRangeFrom.Ticks,statement.DateRangeTo.Ticks,statement.StatementType,statement.SuperFamily,
				statement.LimitedCustomFamily,CultureInfo.CurrentCulture.Name);
		}

		///<summary>Adds the columns the service-date table needs when the DataSet had no account table to copy them from.</summary>
		private static void AddMissingColumns(DataTable table) {
			foreach(string columnName in new[] { "AbbrDesc","balance","charges","ClaimPaymentNum","clinic","ClinicNum","colorText","credits","date",
				"description","InvoiceNum","IsTransfer","patient","paymentsOnObj","PayPlanChargeNum","ProcCode","ProcNum","ProcNumLab","procsOnObj",
				"adjustsOnObj","prov","signed","ToothNum","ToothRange","tth","SuperFamily" })
			{
				if(!table.Columns.Contains(columnName)) {
					table.Columns.Add(columnName);
				}
			}
			foreach(string columnName in new[] { "AdjNum","ClaimNum","PatNum","PayNum","PayPlanNum","StatementNum" }) {
				if(!table.Columns.Contains(columnName)) {
					table.Columns.Add(columnName,typeof(long));
				}
			}
			foreach(string columnName in new[] { "balanceDouble","chargesDouble","creditsDouble" }) {
				if(!table.Columns.Contains(columnName)) {
					table.Columns.Add(columnName,typeof(decimal));
				}
			}
			foreach(string columnName in new[] { "DateTime","dateTimeSort" }) {
				if(!table.Columns.Contains(columnName)) {
					table.Columns.Add(columnName,typeof(DateTime));
				}
			}
			if(!table.Columns.Contains(COLUMN_IS_BOLD)) {
				table.Columns.Add(COLUMN_IS_BOLD,typeof(bool));
			}
		}

		///<summary>The patient text the regular statement uses for each patient (it follows the "Include patient last name" option).</summary>
		private static Dictionary<long,string> GetPatientNamesFromAccountTables(List<DataTable> listAccountTables) {
			Dictionary<long,string> dictPatNames=new Dictionary<long,string>();
			foreach(DataTable tableAccount in listAccountTables) {
				foreach(DataRow dataRow in tableAccount.Rows) {
					long patNum=PIn.Long(dataRow["PatNum"].ToString());
					string patient=dataRow["patient"].ToString();
					if(patNum!=0 && patient!="" && !dictPatNames.ContainsKey(patNum)) {
						dictPatNames[patNum]=patient;
					}
				}
			}
			return dictPatNames;
		}

		private static string GetPatientName(long patNum,string patientNameReport,Family family,Dictionary<long,string> dictPatNames) {
			if(dictPatNames.TryGetValue(patNum,out string name)) {
				return name;
			}
			if(family!=null && family.GetPatient(patNum)!=null) {
				return family.GetNameInFamFirst(patNum);
			}
			return patientNameReport??"";
		}

		private static DataRow MakeStatementRow(DataTable table,ServiceDateViewRow row,long patNumRow,Statement statement,Family family,
			Dictionary<long,string> dictPatNames)
		{
			DataRow dataRow=table.NewRow();
			foreach(DataColumn dataColumn in table.Columns) {//Same defaults as an empty account row.
				if(dataColumn.DataType==typeof(long)) {
					dataRow[dataColumn]=0L;
				}
				else if(dataColumn.DataType==typeof(decimal)) {
					dataRow[dataColumn]=0m;
				}
				else if(dataColumn.DataType==typeof(DateTime)) {
					dataRow[dataColumn]=DateTime.MinValue;
				}
				else if(dataColumn.DataType==typeof(bool)) {
					dataRow[dataColumn]=false;
				}
				else {
					dataRow[dataColumn]="";
				}
			}
			bool isTransaction=row.RowKind==ServiceDateViewRowKind.Transaction;
			long priKey=row.GetPriKey();
			if(isTransaction && ServiceDateViews.IsAdjustmentType(row.TranType)) {
				dataRow["AdjNum"]=priKey;
			}
			if(isTransaction && ServiceDateViews.IsInsuranceType(row.TranType)) {
				dataRow["ClaimNum"]=row.ParentNum;
				dataRow["ClaimPaymentNum"]=row.TranType.StartsWith("InsPay") ? "1" : "0";
			}
			else {
				dataRow["ClaimPaymentNum"]="0";
			}
			if(isTransaction && ServiceDateViews.IsPatPayType(row.TranType)) {
				dataRow["PayNum"]=row.ParentNum;
			}
			if(isTransaction && ServiceDateViews.IsPayPlanType(row.TranType)) {
				dataRow["PayPlanNum"]=row.ParentNum;
			}
			dataRow["PayPlanChargeNum"]=(isTransaction && ServiceDateViews.IsPayPlanChargeType(row.TranType)) ? priKey.ToString() : "0";
			dataRow["ProcNum"]=row.IsProcedure ? row.ProcNum.ToString() : "0";
			dataRow["ProcNumLab"]="0";
			dataRow["SuperFamily"]="0";
			dataRow["IsTransfer"]="0";
			//Amounts
			bool showsAmounts=row.IsTotal || (isTransaction && (row.IsProcedure || row.Charge!=0));
			dataRow["chargesDouble"]=row.Charge;
			dataRow["charges"]=showsAmounts ? row.Charge.ToString("n") : "";
			showsAmounts=row.IsTotal || (isTransaction && row.Credit!=0);
			dataRow["creditsDouble"]=row.Credit;
			dataRow["credits"]=showsAmounts ? row.Credit.ToString("n") : "";
			dataRow["balanceDouble"]=row.ShowsBalance ? row.Balance : 0m;
			dataRow["balance"]=row.ShowsBalance ? row.Balance.ToString("n") : "";
			//Dates: procedures and day totals show the date of service, other transactions their own date.
			DateTime dateShown=DateTime.MinValue;
			if(row.IsProcedure || row.RowKind==ServiceDateViewRowKind.DayTotal) {
				dateShown=row.DateService;
			}
			else if(isTransaction) {
				dateShown=row.DateTrans.Year>1880 ? row.DateTrans : row.DateService;
			}
			dataRow["date"]=dateShown.Year>1880 ? dateShown.ToShortDateString() : "";
			dataRow["DateTime"]=row.DateService;
			dataRow["dateTimeSort"]=row.DateService;
			dataRow["description"]=GetServiceDateViewDescription(row);
			dataRow["patient"]=isTransaction ? GetPatientName(row.PatNum,row.PatientName,family,dictPatNames) : "";
			dataRow["PatNum"]=patNumRow;
			dataRow["prov"]=isTransaction ? row.ProvAbbr : "";
			dataRow["ClinicNum"]=row.ClinicNum.ToString();
			if(row.IsProcedure) {
				dataRow["ProcCode"]=row.ProcCode;
				dataRow["ToothNum"]=row.ToothNum;
				dataRow["ToothRange"]=row.ToothRange;
				dataRow["tth"]=Tooth.Display(row.ToothNum);
			}
			if(CultureInfo.CurrentCulture.Name.EndsWith("CA") && statement.IsReceipt && row.IsProcedure) {//Canadian receipts, as the regular view.
				dataRow["description"]="";
				dataRow["ProcCode"]="";
				dataRow["tth"]="";
			}
			dataRow[COLUMN_IS_BOLD]=row.IsTotal;
			return dataRow;
		}

		private static void AddNoActivityRow(DataTable table,long patNum,Family family,Dictionary<long,string> dictPatNames) {
			ServiceDateViewRow row=new ServiceDateViewRow();
			row.RowKind=ServiceDateViewRowKind.Transaction;
			row.PatNum=patNum;
			DataRow dataRow=MakeStatementRow(table,row,patNum,new Statement(),family,dictPatNames);
			dataRow["date"]=DateTime.Today.ToShortDateString();
			dataRow["DateTime"]=DateTime.Today;
			dataRow["description"]=Lans.g("Statements","No Account Activity");
			dataRow["patient"]=GetPatientName(patNum,"",family,dictPatNames);
			dataRow["charges"]="";
			dataRow["credits"]="";
			table.Rows.Add(dataRow);
		}
	}
}
