// Vendor-direct integrations: choosing the office's PDMP (Bamboo) client certificate.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Imports the client certificate issued for this software (.pfx/.p12 and its password), points at one in the Windows certificate store
	///by thumbprint, or removes it, for one program and clinic (docs/specs/vendor-direct/pdmp.md, section 3.3). The file is checked before it is saved:
	///it opens with the password, has a private key, is inside its dates and allows client authentication. It is stored as osd1$ in masked,
	///high-security rows; every change writes a security log entry. Editing needs ManageHighSecurityProgProperties.</summary>
	public partial class FormPdmpClientCertificate:FormODBase {
		private readonly Program _program;
		private readonly long _clinicNum;
		private byte[] _arrayFileBytes;

		///<summary>For the designer only.</summary>
		public FormPdmpClientCertificate() : this(null,0) {
		}

		public FormPdmpClientCertificate(Program program,long clinicNum) {
			_program=program;
			_clinicNum=clinicNum;
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormPdmpClientCertificate_Load(object sender,EventArgs e) {
			if(_program==null) {
				DialogResult=DialogResult.Cancel;
				return;
			}
			string clinicText=Lan.g(this,"Default");
			if(_clinicNum!=0) {
				clinicText=Clinics.GetAbbr(_clinicNum);
			}
			labelClinic.Text=Lan.g(this,"Clinic")+": "+clinicText;
			labelClinic.Visible=PrefC.HasClinicsEnabled;
			FillCurrent();
		}

		private void FillCurrent() {
			ProgramProperties.RefreshCache();
			PdmpProperty pdmpProperty=PdmpProperty.LoadBambooForSetup(_program,_clinicNum);
			textCurrent.Text=PdmpProperty.GetCertificateStatusText(pdmpProperty,DateTime.UtcNow);
		}

		private bool IsAuthorized() {
			return Security.IsAuthorized(EnumPermType.ManageHighSecurityProgProperties);
		}

		private void butBrowse_Click(object sender,EventArgs e) {
			using OpenFileDialog openFileDialog=new OpenFileDialog();
			openFileDialog.Filter=Lan.g(this,"Certificate files")+" (*.pfx;*.p12)|*.pfx;*.p12|"+Lan.g(this,"All files")+" (*.*)|*.*";
			if(openFileDialog.ShowDialog()!=DialogResult.OK) {
				return;
			}
			try {
				FileInfo fileInfo=new FileInfo(openFileDialog.FileName);
				if(fileInfo.Length>1024*1024) {
					MsgBox.Show(this,"That file is too large to be a certificate file.");
					return;
				}
				_arrayFileBytes=File.ReadAllBytes(openFileDialog.FileName);
				textFile.Text=openFileDialog.FileName;
				textPassword.Select();
			}
			catch(Exception ex) {
				_arrayFileBytes=null;
				textFile.Text="";
				MsgBox.Show(Lan.g(this,"The file could not be read.")+"\r\n"+ex.Message);
			}
		}

		private void butImport_Click(object sender,EventArgs e) {
			if(_arrayFileBytes==null || _arrayFileBytes.Length==0) {
				MsgBox.Show(this,"Choose the certificate file first.");
				return;
			}
			if(!IsAuthorized()) {
				return;
			}
			try {
				string description=PdmpCertificate.SaveImported(_program.ProgramNum,_clinicNum,_arrayFileBytes,textPassword.Text);
				DataValid.SetInvalid(InvalidType.Programs);
				MsgBox.Show(Lan.g(this,"Certificate imported.")+"\r\n"+description);
			}
			catch(PdmpException ex) {
				MsgBox.Show(ex.Message);
				return;
			}
			finally {
				textPassword.Text="";
			}
			_arrayFileBytes=null;
			textFile.Text="";
			FillCurrent();
		}

		private void butStore_Click(object sender,EventArgs e) {
			if(!IsAuthorized()) {
				return;
			}
			List<X509Certificate2> listCertificates=PdmpCertificate.ListStoreCertificates(DateTime.UtcNow);
			try {
				if(listCertificates.Count==0) {
					MsgBox.Show(this,"No certificate for client authentication with a private key was found in this computer's certificate store.");
					return;
				}
				List<string> listDisplays=listCertificates.Select(x => PdmpCertificate.Describe(x)).ToList();
				InputBox inputBox=new InputBox(Lan.g(this,"Choose the client certificate issued for this software"),listDisplays);
				inputBox.ShowDialog();
				if(inputBox.IsDialogCancel || inputBox.SelectedIndex<0) {
					return;
				}
				PdmpCertificate.SaveThumbprint(_program.ProgramNum,_clinicNum,listCertificates[inputBox.SelectedIndex].Thumbprint);
				DataValid.SetInvalid(InvalidType.Programs);
			}
			finally {
				listCertificates.ForEach(x => x.Dispose());
			}
			FillCurrent();
		}

		private void butRemove_Click(object sender,EventArgs e) {
			if(!IsAuthorized()) {
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Remove the client certificate? The integration can't be used until a certificate is set again.")) {
				return;
			}
			PdmpCertificate.Remove(_program.ProgramNum,_clinicNum);
			DataValid.SetInvalid(InvalidType.Programs);
			FillCurrent();
		}

		private void butClose_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.OK;
			Close();
		}
	}
}
