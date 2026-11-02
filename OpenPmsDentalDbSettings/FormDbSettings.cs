// The window of the connection settings tool.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using CodeBase;
using DataConnectionBase;

namespace OpenPmsDentalDbSettings {
	///<summary>Server, database, user, the office CA certificate, and the client certificate or the database password; Test connection, Save,
	///Cancel (docs/specs/db-connection-security.md section 4.1). Filled from FreeDentalConfig.xml and then from the switches the tool was started
	///with (the installer passes its page's values).</summary>
	public class FormDbSettings:Form {
		public const string TITLE="Open PMS - Dental connection settings";
		private readonly string _configPath;
		private readonly string _caCopyPath;
		private readonly DbSettingsRequest _dbSettingsRequestStart;
		private DbConnectionConfig _dbConnectionConfigCurrent=new DbConnectionConfig();
		private string _pfxPathPending;
		private string _pfxPasswordPending;
		private TextBox _textServer;
		private TextBox _textDatabase;
		private TextBox _textUser;
		private TextBox _textCaFile;
		private Label _labelCaCurrent;
		private RadioButton _radioCertificate;
		private RadioButton _radioPassword;
		private Label _labelCertificateCurrent;
		private Label _labelPfxPending;
		private Button _butImportPfx;
		private TextBox _textPassword;
		private Label _labelStatus;

		///<summary>Set when the settings were saved; null when the window closed without saving.</summary>
		public DbSettingsResult Result { get; private set; }

		public FormDbSettings(string configPath,string caCopyPath,DbSettingsRequest dbSettingsRequestStart) {
			_configPath=configPath;
			_caCopyPath=caCopyPath;
			_dbSettingsRequestStart=dbSettingsRequestStart??new DbSettingsRequest();
			BuildControls();
			Load+=FormDbSettings_Load;
		}

		private void BuildControls() {
			Text=TITLE;
			FormBorderStyle=FormBorderStyle.FixedDialog;
			MaximizeBox=false;
			StartPosition=FormStartPosition.CenterScreen;
			AutoScaleMode=AutoScaleMode.Font;
			ClientSize=new Size(560,470);
			try {
				Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			_textServer=AddRow("&Server:",12);
			_textDatabase=AddRow("&Database:",40);
			_textUser=AddRow("&User:",68);
			Controls.Add(new Label() { Text="Office &CA certificate:",Location=new Point(12,103),Size=new Size(116,18) });
			_textCaFile=new TextBox() { Location=new Point(130,100),Size=new Size(330,20) };
			Controls.Add(_textCaFile);
			Button butBrowseCa=new Button() { Text="Browse...",Location=new Point(466,98),Size=new Size(74,24) };
			butBrowseCa.Click+=butBrowseCa_Click;
			Controls.Add(butBrowseCa);
			_labelCaCurrent=new Label() { Location=new Point(130,126),Size=new Size(410,28),ForeColor=SystemColors.GrayText };
			Controls.Add(_labelCaCurrent);
			GroupBox groupCredential=new GroupBox() { Text="Client credential",Location=new Point(12,158),Size=new Size(528,170) };
			_radioCertificate=new RadioButton() { Text="Client &certificate from the computer's certificate store (recommended)",Location=new Point(12,20),
				Size=new Size(500,20) };
			_labelCertificateCurrent=new Label() { Location=new Point(32,44),Size=new Size(480,18),ForeColor=SystemColors.GrayText };
			_butImportPfx=new Button() { Text="&Import .pfx...",Location=new Point(32,66),Size=new Size(110,24) };
			_butImportPfx.Click+=butImportPfx_Click;
			_labelPfxPending=new Label() { Location=new Point(150,71),Size=new Size(370,18) };
			_radioPassword=new RadioButton() { Text="Database &password, stored on this computer (DPAPI, machine scope)",Location=new Point(12,100),
				Size=new Size(500,20) };
			_textPassword=new TextBox() { Location=new Point(32,126),Size=new Size(250,20),UseSystemPasswordChar=true };
			Label labelPasswordHint=new Label() { Text="Blank keeps the stored password.",Location=new Point(290,129),Size=new Size(230,18),
				ForeColor=SystemColors.GrayText };
			_radioCertificate.CheckedChanged+=(sender,e) => SetCredentialEnabled();
			_radioPassword.CheckedChanged+=(sender,e) => SetCredentialEnabled();
			groupCredential.Controls.AddRange(new Control[] { _radioCertificate,_labelCertificateCurrent,_butImportPfx,_labelPfxPending,_radioPassword,
				_textPassword,labelPasswordHint });
			Controls.Add(groupCredential);
			Controls.Add(new Label() { Text="A server on this computer (localhost, 127.0.0.1, ::1) needs no CA or certificate. Any other server is reached "
				+"only encrypted, and its certificate must come from the office CA and name the server.",Location=new Point(12,336),Size=new Size(528,32) });
			_labelStatus=new Label() { Location=new Point(12,372),Size=new Size(528,50) };
			Controls.Add(_labelStatus);
			Button butTest=new Button() { Text="&Test connection",Location=new Point(12,432),Size=new Size(120,26) };
			butTest.Click+=butTest_Click;
			Button butSave=new Button() { Text="Save",Location=new Point(384,432),Size=new Size(75,26) };
			butSave.Click+=butSave_Click;
			Button butCancel=new Button() { Text="Cancel",Location=new Point(465,432),Size=new Size(75,26),DialogResult=DialogResult.Cancel };
			Controls.AddRange(new Control[] { butTest,butSave,butCancel });
			CancelButton=butCancel;
		}

		private TextBox AddRow(string caption,int y) {
			Controls.Add(new Label() { Text=caption,Location=new Point(12,y+3),Size=new Size(116,18) });
			TextBox textBox=new TextBox() { Location=new Point(130,y),Size=new Size(410,20) };
			Controls.Add(textBox);
			return textBox;
		}

		private void FormDbSettings_Load(object sender,EventArgs e) {
			try {
				_dbConnectionConfigCurrent=DbSettingsRunner.ReadCurrent(_configPath);
			}
			catch(DbSettingsException ex) {
				MessageBox.Show(ex.Message+"\r\n\r\nSaving writes a new connection into it.",TITLE);
			}
			_textServer.Text=_dbSettingsRequestStart.Server??_dbConnectionConfigCurrent.ServerName;
			_textDatabase.Text=_dbSettingsRequestStart.Database??_dbConnectionConfigCurrent.Database;
			_textUser.Text=_dbSettingsRequestStart.User??_dbConnectionConfigCurrent.User;
			_textCaFile.Text=_dbSettingsRequestStart.CaFile??"";
			_labelCaCurrent.Text=_dbConnectionConfigCurrent.SslCa=="" ? "Now: none." : "Now: "+_dbConnectionConfigCurrent.SslCa
				+(File.Exists(_dbConnectionConfigCurrent.SslCa) ? "" : " (the file does not exist)")+". A file chosen here is copied there.";
			if(_dbSettingsRequestStart.PfxFile!=null) {
				_pfxPathPending=_dbSettingsRequestStart.PfxFile;
				_pfxPasswordPending=_dbSettingsRequestStart.PfxPassword;
				_labelPfxPending.Text="To import when you save: "+Path.GetFileName(_pfxPathPending);
			}
			if(_dbSettingsRequestStart.DbPassword!=null) {
				_textPassword.Text=_dbSettingsRequestStart.DbPassword;
			}
			ShowCurrentCertificate();
			bool isCertificate=DbSettingsRunner.ResolveAuth(_dbSettingsRequestStart,_dbConnectionConfigCurrent)==DbSettingsAuth.Certificate;
			_radioCertificate.Checked=isCertificate;
			_radioPassword.Checked=!isCertificate;
			SetCredentialEnabled();
		}

		private void ShowCurrentCertificate() {
			string thumbprint=_dbSettingsRequestStart.Thumbprint??_dbConnectionConfigCurrent.ClientCertThumbprint;
			if(string.IsNullOrEmpty(thumbprint)) {
				_labelCertificateCurrent.Text="Now: none.";
				return;
			}
			X509Certificate2 x509Certificate2=null;
			try {
				x509Certificate2=ClientCertificateStore.FindInStore(thumbprint);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			_labelCertificateCurrent.Text=x509Certificate2==null ? "Now: "+thumbprint+" (not in the computer's store)."
				: "Now: "+ClientCertificateStore.Describe(x509Certificate2)+".";
		}

		private void SetCredentialEnabled() {
			_butImportPfx.Enabled=_radioCertificate.Checked;
			_textPassword.Enabled=_radioPassword.Checked;
		}

		private void butBrowseCa_Click(object sender,EventArgs e) {
			using(OpenFileDialog openFileDialog=new OpenFileDialog()) {
				openFileDialog.Title="The office CA certificate";
				openFileDialog.Filter="Certificates (*.pem;*.crt;*.cer)|*.pem;*.crt;*.cer|All files (*.*)|*.*";
				if(openFileDialog.ShowDialog(this)!=DialogResult.OK) {
					return;
				}
				try {
					_labelStatus.Text="CA: "+OfficeCaFile.Describe(OfficeCaFile.Load(openFileDialog.FileName));
					_textCaFile.Text=openFileDialog.FileName;
				}
				catch(DbSettingsException ex) {
					MessageBox.Show(ex.Message,TITLE);
				}
			}
		}

		private void butImportPfx_Click(object sender,EventArgs e) {
			using(OpenFileDialog openFileDialog=new OpenFileDialog()) {
				openFileDialog.Title="The client certificate";
				openFileDialog.Filter="Client certificate (*.pfx;*.p12)|*.pfx;*.p12|All files (*.*)|*.*";
				if(openFileDialog.ShowDialog(this)!=DialogResult.OK) {
					return;
				}
				string password;
				try {
					password=FormSecretPrompt.Ask("The password of "+Path.GetFileName(openFileDialog.FileName)+":");
				}
				catch(DbSettingsException) {
					return;
				}
				try {
					using(X509Certificate2 x509Certificate2=ClientCertificateStore.LoadPfxForCheck(openFileDialog.FileName,password)) {
						_labelPfxPending.Text="To import when you save: "+ClientCertificateStore.Describe(x509Certificate2);
					}
					_pfxPathPending=openFileDialog.FileName;
					_pfxPasswordPending=password;
				}
				catch(DbSettingsException ex) {
					MessageBox.Show(ex.Message,TITLE);
				}
			}
		}

		private DbSettingsRequest RequestFromScreen() {
			DbSettingsRequest dbSettingsRequest=new DbSettingsRequest();
			dbSettingsRequest.Server=_textServer.Text.Trim();
			dbSettingsRequest.Database=_textDatabase.Text.Trim();
			dbSettingsRequest.User=_textUser.Text.Trim();
			dbSettingsRequest.CaFile=_textCaFile.Text.Trim()=="" ? null : _textCaFile.Text.Trim();
			if(_radioCertificate.Checked) {
				dbSettingsRequest.Auth=DbSettingsAuth.Certificate;
				dbSettingsRequest.PfxFile=_pfxPathPending;
				dbSettingsRequest.PfxPassword=_pfxPasswordPending;
				if(_pfxPathPending==null) {
					dbSettingsRequest.Thumbprint=_dbSettingsRequestStart.Thumbprint;
				}
			}
			else {
				dbSettingsRequest.Auth=DbSettingsAuth.Password;
				dbSettingsRequest.DbPassword=_textPassword.Text=="" ? null : _textPassword.Text;
			}
			return dbSettingsRequest;
		}

		private void butTest_Click(object sender,EventArgs e) {
			DbSettingsRequest dbSettingsRequest=RequestFromScreen();
			if(dbSettingsRequest.PfxFile!=null) {
				MessageBox.Show("The client certificate is imported when you save. Save first, then test.",TITLE);
				return;
			}
			string sslCa=dbSettingsRequest.CaFile??_dbConnectionConfigCurrent.SslCa;
			string thumbprint="";
			string password="";
			if(dbSettingsRequest.Auth==DbSettingsAuth.Certificate) {
				thumbprint=DbConnectionConfig.NormalizeThumbprint(dbSettingsRequest.Thumbprint??_dbConnectionConfigCurrent.ClientCertThumbprint);
			}
			else if(dbSettingsRequest.DbPassword!=null) {
				password=dbSettingsRequest.DbPassword;
			}
			else if(dbSettingsRequest.User==_dbConnectionConfigCurrent.User) {
				try {
					password=_dbConnectionConfigCurrent.GetPassword(dbSettingsRequest.Server);
				}
				catch(DbConnectionRefusedException ex) {
					MessageBox.Show(ex.Message,TITLE);
					return;
				}
			}
			Cursor=Cursors.WaitCursor;
			DbSettingsResult dbSettingsResult=DbSettingsRunner.TestConnection(dbSettingsRequest.Server,dbSettingsRequest.Database,dbSettingsRequest.User,
				password,sslCa,thumbprint);
			Cursor=Cursors.Default;
			_labelStatus.Text=dbSettingsResult.Message;
			MessageBox.Show(dbSettingsResult.Message,TITLE,MessageBoxButtons.OK,
				dbSettingsResult.ExitCode==DbSettingsRunner.EXIT_OK ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
		}

		private void butSave_Click(object sender,EventArgs e) {
			Cursor=Cursors.WaitCursor;
			DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(RequestFromScreen(),_configPath,_caCopyPath);
			Cursor=Cursors.Default;
			_labelStatus.Text=dbSettingsResult.Message;
			if(dbSettingsResult.ExitCode!=DbSettingsRunner.EXIT_OK) {
				MessageBox.Show(dbSettingsResult.Message,TITLE,MessageBoxButtons.OK,MessageBoxIcon.Warning);
				return;
			}
			Result=dbSettingsResult;
			_pfxPasswordPending=null;
			if(MessageBox.Show(dbSettingsResult.Message+"\r\n\r\nTest the connection now?",TITLE,MessageBoxButtons.YesNo)==DialogResult.Yes) {
				Cursor=Cursors.WaitCursor;
				DbSettingsResult dbSettingsResultTest=DbSettingsRunner.TestSaved(_configPath);
				Cursor=Cursors.Default;
				MessageBox.Show(dbSettingsResultTest.Message,TITLE,MessageBoxButtons.OK,
					dbSettingsResultTest.ExitCode==DbSettingsRunner.EXIT_OK ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
			}
			DialogResult=DialogResult.OK;
			Close();
		}
	}
}
