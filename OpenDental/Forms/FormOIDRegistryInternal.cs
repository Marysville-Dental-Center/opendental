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
				labelStatus.ForeColor=System.Drawing.Color.Red;
				labelStatus.Text=Lan.g(this,"There is no OID root stored.  Press Generate Root, or Enter Root to use an OID your organization registered.");
			}
			else if(OIDInternals.IsLegacyRoot(_rootOIDString)) {
				labelStatus.ForeColor=System.Drawing.Color.DarkRed;
				labelStatus.Text=Lan.g(this,"This root was issued by the previous software vendor.  Changing it changes the identifiers that HL7 and CCD "
					+"trading partners may already hold, so tell them before you change it.");
			}
			else {
				labelStatus.Text="";
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
					//recommended value is root+.1 through root+.8 (will grow as the enum is expanded)
					row.Cells.Add(OIDInternals.GetRecommendedValue(_rootOIDString,_listOIDInternals[i].IDType));
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
			string oid=inputBox.StringResult.Trim();
			if(oid!="" && !OIDInternals.IsValidOid(oid)) {
				MsgBox.Show(this,"Not a valid OID.  Use numbers separated by periods, such as 1.3.6.1.4.1.99999.1, with no leading zeros.");
				return;
			}
			if(_listOIDInternals[e.Row].IDType==IdentifierType.Root) {
				_rootOIDString=oid;
			}
			_listOIDInternals[e.Row].IDRoot=oid;
			FillGrid();
		}

		///<summary>Generates a root under the UUID arc (2.25), which needs no registration, and fills the child rows from it.</summary>
		private void butGenerateRoot_Click(object sender,EventArgs e) {
			SetRoot(OIDInternals.GenerateUuidRoot());
		}

		///<summary>Takes a root the organization obtained, such as 1.3.6.1.4.1.&lt;PEN&gt;.1 under an IANA Private Enterprise Number, and fills the
		///child rows from it.</summary>
		private void butEnterRoot_Click(object sender,EventArgs e) {
			InputBoxParam inputBoxParam=new InputBoxParam();
			inputBoxParam.InputBoxType_=InputBoxType.TextBox;
			inputBoxParam.LabelText=Lan.g(this,"Root OID registered by your organization, for example 1.3.6.1.4.1.<PEN>.1");
			inputBoxParam.Text=_rootOIDString;
			InputBox inputBox=new InputBox(inputBoxParam);
			inputBox.ShowDialog();
			if(inputBox.IsDialogCancel) {
				return;
			}
			string root=inputBox.StringResult.Trim();
			if(!OIDInternals.IsValidOid(root)) {
				MsgBox.Show(this,"Not a valid OID.  Use numbers separated by periods, such as 1.3.6.1.4.1.99999.1, with no leading zeros, "
					+"at most 64 characters.");
				return;
			}
			SetRoot(root);
		}

		///<summary>Replaces the root after confirming, and fills the child rows. Nothing is saved until the user presses Save.</summary>
		private void SetRoot(string root) {
			if(root==_rootOIDString) {
				return;
			}
			if(_rootOIDString!=""
				&& !MsgBox.Show(this,MsgBoxButtons.OKCancel,"Replace the current root?  Identifiers already sent to HL7 and CCD trading partners use the "
				+"current root, so tell them about the new one before the next message goes out."))
			{
				return;
			}
			bool overwriteChildren=false;
			if(OIDInternals.HasCustomChildren(_listOIDInternals)) {
				overwriteChildren=MsgBox.Show(this,MsgBoxButtons.YesNo,"Some rows hold values that were set by hand.  Replace them with values under "
					+"the new root?  Choose No to keep them.");
			}
			OIDInternals.ApplyRootToList(_listOIDInternals,root,overwriteChildren);
			_rootOIDString=root;
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