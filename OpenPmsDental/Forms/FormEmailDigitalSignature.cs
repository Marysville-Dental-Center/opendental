using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Shows the certificate of a digital signature (S/MIME): who signed, the authority, the dates, the thumbprint and the version. The trust
	///status and the Trust button went with Direct messaging (the NHINDAnchors and NHINDExternal stores were only for Direct).</summary>
	public partial class FormEmailDigitalSignature:FormODBase {

		private X509Certificate2 _x509Certificate2;

		public FormEmailDigitalSignature(X509Certificate2 x509Certificate2) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_x509Certificate2=x509Certificate2;
		}

		private void FormEmailDigitalSignature_Load(object sender,EventArgs e) {
			textSignedBy.Text=EmailCertificates.GetCertSubjectName(_x509Certificate2);
			textCertificateAuthority.Text=_x509Certificate2.IssuerName.Name;
			textValidFrom.Text=_x509Certificate2.NotBefore.ToShortDateString()+" to "+_x509Certificate2.NotAfter.ToShortDateString();
			textThumbprint.Text=_x509Certificate2.Thumbprint;
			textVersion.Text=_x509Certificate2.Version.ToString();
		}

	}
}
