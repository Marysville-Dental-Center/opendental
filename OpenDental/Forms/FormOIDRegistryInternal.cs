using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using OpenDentBusiness;
using OpenDental.UI;

namespace OpenDental {
	public partial class FormOIDRegistryInternal:FormODBase {
		private List<OIDInternal> _listOIDInternals;
		///<summary>Used for refence to construct recommended values.</summary>
		private string _rootOIDString;

		public FormOIDRegistryInternal() {
			InitializeComponent();
			InitializeLayoutManager();
		}

		private void FormReminders_Load(object sender,EventArgs e) {
			_listOIDInternals=OIDInternals.GetAll();
			_listOIDInternals.Sort(sortOIDsByIDType);
			_rootOIDString=OIDInternals.GetForType(IdentifierType.Root).IDRoot;
			FillGrid();
		}

		private void FillGrid() {
			if(_rootOIDString=="") {
				labelRetrieveStatus.ForeColor=System.Drawing.Color.Red;
				labelRetrieveStatus.Text="There is no OID root stored.  Double click the Root row to enter your organization's root OID.";
			}
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col;
			col=new GridColumn("Type",100);
			gridMain.Columns.Add(col);
			col=new GridColumn("Recommended Value",220);
			gridMain.Columns.Add(col);
			col=new GridColumn("Actual Value",220);
			gridMain.Columns.Add(col);
			gridMain.ListGridRows.Clear();
			GridRow row;
			for(int i=0;i<_listOIDInternals.Count;i++) {
				row=new GridRow();
				row.Cells.Add(_listOIDInternals[i].IDType.ToString());
				if(_listOIDInternals[i].IDType==IdentifierType.Root) {
					row.Cells.Add("Your organization's root OID");
				}
				else {
					//recommended value is root+.1 through root+.4 (will grow as the enum is expanded)
					row.Cells.Add(_rootOIDString+"."+i.ToString());//adds the .1, .2, .3, and .4 (etc...) to the root
				}
				row.Cells.Add(_listOIDInternals[i].IDRoot);
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			InputBoxParam inputBoxParam=new InputBoxParam();
			inputBoxParam.InputBoxType_=InputBoxType.TextBox;
			inputBoxParam.LabelText="Actual OID";
			inputBoxParam.Text=_listOIDInternals[e.Row].IDRoot;
			InputBox inputBox=new InputBox(inputBoxParam);
			inputBox.ShowDialog();
			if(inputBox.IsDialogCancel) {
				return;
			}
			if(e.Row==0) {
				_rootOIDString=inputBox.StringResult;
			}
			_listOIDInternals[e.Row].IDRoot=inputBox.StringResult;
			FillGrid();
		}

		private int sortOIDsByIDType(OIDInternal a,OIDInternal b) {
			if(a.IDType>b.IDType) {
				return 1;
			}
			if(a.IDType<b.IDType) {
				return -1;
			}
			return 0;//should never happen.
		}

		private void butSave_Click(object sender,EventArgs e) {
			for(int i=0;i<_listOIDInternals.Count;i++) {
				OIDInternals.Update(_listOIDInternals[i]);
			}
			DialogResult=DialogResult.OK;
		}

	}
}