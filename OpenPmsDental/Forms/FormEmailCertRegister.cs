using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>The Certificate button of Edit Email Address (open point O-13 of the .NET 10 plan, owner answer of 2026-11-08: "Yes, load Direct
	///provider's certificate from a file"). Imports the certificate the office's Direct provider (HISP) issued for the address, from its .pfx or
	///.p12 file and password, into this computer's Direct certificate store (NHINDPrivate) with its private key never exportable, and the
	///provider's trust anchor into NHINDAnchors. Open Dental registered the certificate with its own servers here, which are gone. The rules are
	///DirectCertificateImport's (docs/port-notes/DIRECT-CERT.md); this window shows what they found and asks before it replaces or trusts
	///anything.</summary>
	public partial class FormEmailCertRegister:FormODBase {
		///<summary>The address the certificate is for: the Edit Email Address window's user name, which Direct sends from.</summary>
		private string _emailAddress;
		///<summary>For the security log only; 0 for an address not saved yet.</summary>
		private long _emailAddressNum;

		public FormEmailCertRegister(string emailAddress,long emailAddressNum) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_emailAddress=EmailMessages.GetAddressSimple(emailAddress);
			_emailAddressNum=emailAddressNum;
			textEmailAddress.Text=_emailAddress;
		}

		private void FormEmailCertRegister_Load(object sender,EventArgs e) {
			FillInStore();
		}

		///<summary>What this computer has for the address now: its certificates in the Direct store and whether a trust anchor of this computer
		///trusts them.</summary>
		private void FillInStore() {
			List<DirectCertificateDetails> listDetails;
			try {
				listDetails=DirectCertificateImport.GetPrivateCertificates(_emailAddress);
			}
			catch(Exception ex) {
				textInStore.Text=Lan.g(this,"This computer's Direct certificate store could not be read")+": "+ex.Message;
				return;
			}
			if(listDetails.Count==0) {
				textInStore.Text=Lan.g(this,"No certificate for this address or its domain.");
				return;
			}
			string trusted;
			try {
				trusted=EmailMessages.IsSenderTrusted(_emailAddress) ? Lan.g(this,"yes") : Lan.g(this,"no");
			}
			catch(Exception ex) {
				ex.DoNothing();//The anchor store does not exist yet, or the address can't be parsed: not known.
				trusted=Lan.g(this,"unknown");
			}
			textInStore.Text=string.Join("\r\n",listDetails.Select(x => x.DescribeShort()))+"\r\n"
				+Lan.g(this,"Trusted by a trust anchor of this computer")+": "+trusted;
		}

		private void butBrowse_Click(object sender,EventArgs e) {
			if(openFileDialogCert.ShowDialog()==DialogResult.OK) {
				textCertFilePath.Text=openFileDialogCert.FileName;
			}
		}

		///<summary>A changed file or password needs a new check before Import.</summary>
		private void textCertFile_TextChanged(object sender,EventArgs e) {
			textDetails.Text="";
			butImport.Enabled=false;
		}

		private void butCheck_Click(object sender,EventArgs e) {
			Cursor=Cursors.WaitCursor;
			DirectCertificateResult directCertificateResult=DirectCertificateImport.CheckPrivateFile(_emailAddress,textCertFilePath.Text,textPassword.Text,
				DateTime.UtcNow);
			Cursor=Cursors.Default;
			if(directCertificateResult.Error!="") {
				textDetails.Text="";
				butImport.Enabled=false;
				MessageBox.Show(this,directCertificateResult.Error,Text);
				return;
			}
			textDetails.Text=DescribeCheck(directCertificateResult);
			butImport.Enabled=true;
		}

		private string DescribeCheck(DirectCertificateResult directCertificateResult) {
			string text=string.Join("\r\n\r\n",directCertificateResult.ListDetails.Select(x => x.Describe()));
			if(directCertificateResult.ListDetailsReplaced.Count>0) {
				text+="\r\n\r\n"+Lan.g(this,"Replaces, after asking")+":\r\n"+string.Join("\r\n",directCertificateResult.ListDetailsReplaced.Select(x => x.DescribeShort()));
			}
			return text;
		}

		private void butImport_Click(object sender,EventArgs e) {
			Cursor=Cursors.WaitCursor;
			DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(_emailAddress,textCertFilePath.Text,textPassword.Text,
				DateTime.UtcNow);
			Cursor=Cursors.Default;
			if(directCertificateResultCheck.Error!="") {
				butImport.Enabled=false;
				MessageBox.Show(this,directCertificateResultCheck.Error,Text);
				return;
			}
			textDetails.Text=DescribeCheck(directCertificateResultCheck);
			List<string> listThumbprintsReplaceAllowed=new List<string>();
			if(directCertificateResultCheck.ListDetailsReplaced.Count>0) {
				//Only after asking: No imports nothing, so the store never holds two current certificates for the address.
				string question=Lan.g(this,"This computer's Direct certificate store already has an older certificate for this address or its domain")+":\r\n\r\n"
					+string.Join("\r\n",directCertificateResultCheck.ListDetailsReplaced.Select(x => x.DescribeShort()))+"\r\n\r\n"
					+Lan.g(this,"Replace it with the new certificate? It is removed from the store, and so is its private key if this window imported it. Messages "
					+"encrypted to it can then be read here only after importing it again. No imports nothing.");
				if(MessageBox.Show(this,question,Text,MessageBoxButtons.YesNo)!=DialogResult.Yes) {
					return;
				}
				listThumbprintsReplaceAllowed=directCertificateResultCheck.ListDetailsReplaced.Select(x => x.Thumbprint).ToList();
			}
			Cursor=Cursors.WaitCursor;
			DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(_emailAddress,textCertFilePath.Text,textPassword.Text,
				listThumbprintsReplaceAllowed,DateTime.UtcNow);
			Cursor=Cursors.Default;
			if(!directCertificateResult.IsImported) {
				MessageBox.Show(this,directCertificateResult.Error,Text);
				return;
			}
			//No password, address or key in the log.
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,DirectCertificateImport.GetLogText(directCertificateResult,_emailAddressNum,isAnchor:false));
			textPassword.Text="";//Also clears the details and turns Import off (textCertFile_TextChanged).
			FillInStore();
			string message=Lan.g(this,"The certificate was imported into this computer's Direct certificate store.");
			if(directCertificateResult.Warning!="") {
				message+="\r\n\r\n"+directCertificateResult.Warning;
			}
			MessageBox.Show(this,message,Text);
		}

		private void butBrowseAnchor_Click(object sender,EventArgs e) {
			if(openFileDialogAnchor.ShowDialog()==DialogResult.OK) {
				textAnchorFilePath.Text=openFileDialogAnchor.FileName;
			}
		}

		private void butImportAnchor_Click(object sender,EventArgs e) {
			DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckAnchorFile(textAnchorFilePath.Text,DateTime.UtcNow);
			if(directCertificateResultCheck.Error!="") {
				MessageBox.Show(this,directCertificateResultCheck.Error,Text);
				return;
			}
			string question=Lan.g(this,"Add this certificate to this computer's Direct trust anchors? Direct messages from every address whose certificate it "
				+"issued are then trusted.")+"\r\n\r\n"+directCertificateResultCheck.ListDetails[0].Describe();
			if(MessageBox.Show(this,question,Text,MessageBoxButtons.YesNo)!=DialogResult.Yes) {
				return;
			}
			DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportAnchorFile(textAnchorFilePath.Text,DateTime.UtcNow);
			if(!directCertificateResult.IsImported) {
				MessageBox.Show(this,directCertificateResult.Error,Text);
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,DirectCertificateImport.GetLogText(directCertificateResult,_emailAddressNum,isAnchor:true));
			textAnchorFilePath.Text="";
			FillInStore();
			MsgBox.Show(this,"The trust anchor was imported.");
		}

		private void butClose_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.Cancel;
		}

	}
}
