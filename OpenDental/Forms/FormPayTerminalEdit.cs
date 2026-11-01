using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormPayTerminalEdit:FormODBase {
		private PayTerminal _payTerminalCur;
		private bool _isNew;
		///<summary>For a new terminal: the clinic to preselect, or -1 for the picker's default.</summary>
		private long _clinicNumNew=-1;

		///<summary>ccIntegration is the integration a new terminal belongs to (payterminal.CCIntegration, e.g. PayTerminals.CCIntegrationMeetInTheCloud);
		///blank makes a PayConnect terminal, as before 25.4. An existing terminal keeps its own. clinicNumNew preselects a new terminal's clinic.</summary>
		public FormPayTerminalEdit(PayTerminal payTerminalCur=null,string ccIntegration="",long clinicNumNew=-1) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			if(payTerminalCur!=null) {
				_payTerminalCur=payTerminalCur;
				_isNew=false;
			}
			else {
				_payTerminalCur=new PayTerminal();
				_payTerminalCur.CCIntegration=ccIntegration??"";
				_clinicNumNew=clinicNumNew;
				_isNew=true;
			}
		}
		private void FormPayTerminalEdit_Load(object sender,EventArgs e) {
			if(!_isNew) {
				comboClinic.ClinicNumSelected=_payTerminalCur.ClinicNum;
				textName.Text=_payTerminalCur.Name;
				textId.Text=_payTerminalCur.TerminalID;
			}
			else if(_clinicNumNew>=0) {
				comboClinic.ClinicNumSelected=_clinicNumNew;
			}
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(_isNew) {
				DialogResult=DialogResult.Cancel;
				Close();
				return;
			}
			if(MsgBox.Show(MsgBoxButtons.YesNo,Lans.g(this,"Are you sure you would like to delete this payment terminal?"))) {
				PayTerminals.Delete(_payTerminalCur.PayTerminalNum);
				Close();
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(textId.Text.Trim().IsNullOrEmpty()) {
				MsgBox.Show(this,"Terminal ID is required.");
				return;
			}
			_payTerminalCur.Name=textName.Text;
			_payTerminalCur.TerminalID=textId.Text.Trim();
			_payTerminalCur.ClinicNum=comboClinic.ClinicNumSelected;
			if(_isNew) {
				PayTerminals.Insert(_payTerminalCur);
			}
			else {
				PayTerminals.Update(_payTerminalCur);
			}
			DialogResult=DialogResult.OK;
		}

	}
}