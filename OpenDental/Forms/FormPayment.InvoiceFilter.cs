using System.Collections.Generic;
using System.Linq;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Open Dental 26.1 (backlog package M2-PAYW): the Invoice filter on Outstanding Charges. "All" shows every charge; picking an invoice
	///shows only the procedures, adjustments and payment plan charges on it, combined with the other filters, so the Pay button and manual splits
	///work on just those charges. Changing the filter keeps the current splits. The Treat Plan tab is not filtered by invoice (invoices hold
	///completed work). The rules are in PaymentInvoiceFilter.</summary>
	public partial class FormPayment {
		///<summary>The family's invoices with their members, loaded once per set of patients (the family, or the super family when it is shown).</summary>
		private List<InvoiceInfo> _listInvoiceInfosFilter=null;
		private string _invoiceFilterPatNumsKey="";

		///<summary>Fills the Invoice drop-down: "All", then every invoice that still has a balance, newest first. Keeps the current choice when
		///doPreserveValues is set.</summary>
		private void FillFilterInvoices(bool doPreserveValues) {
			long statementNumSelected=0;
			InvoiceInfo invoiceInfoSelected=GetInvoiceFilterSelected();
			if(doPreserveValues && invoiceInfoSelected!=null) {
				statementNumSelected=invoiceInfoSelected.StatementNum;
			}
			List<long> listPatNums=GetFamilyOrSuperFamily().GetPatNums();
			string patNumsKey=string.Join(",",listPatNums.OrderBy(x => x));
			if(_listInvoiceInfosFilter==null || patNumsKey!=_invoiceFilterPatNumsKey) {
				_listInvoiceInfosFilter=PaymentInvoiceFilter.GetInvoicesForPats(listPatNums);
				_invoiceFilterPatNumsKey=patNumsKey;
			}
			List<InvoiceInfo> listInvoiceInfosShow=PaymentInvoiceFilter.GetInvoicesToShow(_listInvoiceInfosFilter,_listAccountEntriesCharges,
				_payment.PayNum,statementNumSelected);
			comboInvoiceOutstandingFilter.Items.Clear();
			comboInvoiceOutstandingFilter.Items.Add(Lan.g(this,"All"));
			int indexSelect=0;
			for(int i=0;i<listInvoiceInfosShow.Count;i++) {
				comboInvoiceOutstandingFilter.Items.Add(PaymentInvoiceFilter.GetDisplayText(listInvoiceInfosShow[i]),listInvoiceInfosShow[i]);
				if(listInvoiceInfosShow[i].StatementNum==statementNumSelected) {
					indexSelect=comboInvoiceOutstandingFilter.Items.Count-1;
				}
			}
			comboInvoiceOutstandingFilter.SelectedIndex=indexSelect;
			comboInvoiceOutstandingFilter.Enabled=(listInvoiceInfosShow.Count>0);
		}

		///<summary>The picked invoice, or null for "All".</summary>
		private InvoiceInfo GetInvoiceFilterSelected() {
			if(comboInvoiceOutstandingFilter.SelectedIndex<=0) {
				return null;
			}
			return comboInvoiceOutstandingFilter.GetSelected<InvoiceInfo>();
		}

		///<summary>Called by FillGridCharges: limits the outstanding charges to the picked invoice.</summary>
		private List<AccountEntry> FilterAccountEntriesByInvoice(List<AccountEntry> listAccountEntries) {
			return PaymentInvoiceFilter.Filter(listAccountEntries,GetInvoiceFilterSelected());
		}
	}
}
