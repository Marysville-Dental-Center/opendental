// Helpers of the connection security tests: temporary folders and synthetic certificates.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using OpenPmsDentalDbSettings;

namespace DbConnectionSecurityTests {
	///<summary>Temporary folders under %TEMP% and synthetic certificates made in memory (CertificateRequest). Nothing is put in a certificate
	///store and nothing connects anywhere.</summary>
	internal static class TestHelpers {
		///<summary>A password that must never show up in a message, a log or a connection refusal.</summary>
		public const string SECRET="Synthetic-Secret-7f3c;9=x";
		///<summary>A host over the network, from the documentation range: never contacted, the rule refuses first.</summary>
		public const string NETWORK_HOST="db.example.test";
		public const string NETWORK_ADDRESS="192.0.2.10";

		///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
		public static T AssertThrows<T>(Action action) where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				return ex;
			}
			Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail("Expected "+typeof(T).Name+".");
			return null;
		}

		///<summary>The text of a file of the repository: the first folder above the test assembly that holds LICENSE and NOTICE, then pathParts.
		///For source checks of code the tests cannot run (a window).</summary>
		public static string ReadRepoFile(params string[] pathParts) {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null
				&& !(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))))
			{
				directoryInfo=directoryInfo.Parent;
			}
			Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsNotNull(directoryInfo,"Repository root not found above "
				+AppDomain.CurrentDomain.BaseDirectory);
			List<string> listParts=new List<string>() { directoryInfo.FullName };
			listParts.AddRange(pathParts);
			string path=Path.Combine(listParts.ToArray());
			Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(File.Exists(path),"Missing file: "+path);
			return File.ReadAllText(path);
		}

		public static string NewTempFolder() {
			string folder=Path.Combine(Path.GetTempPath(),"dbsec_tests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			return folder;
		}

		public static void DeleteFolder(string folder) {
			try {
				if(folder!=null && Directory.Exists(folder)) {
					Directory.Delete(folder,true);
				}
			}
			catch(IOException) { }
			catch(UnauthorizedAccessException) { }
		}

		///<summary>A self-signed certificate. isCa sets the basic constraints; clientAuth adds the client authentication EKU, serverAuthOnly the
		///server one only.</summary>
		public static X509Certificate2 NewCertificate(string subject,bool isCa,bool isClientAuth=false,bool isServerAuthOnly=false,
			int daysValid=365)
		{
			using(RSA rsa=RSA.Create(2048)) {
				CertificateRequest certificateRequest=new CertificateRequest(subject,rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
				certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(isCa,false,0,true));
				if(isClientAuth || isServerAuthOnly) {
					OidCollection oidCollection=new OidCollection();
					oidCollection.Add(new Oid(isClientAuth ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1"));
					certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(oidCollection,false));
				}
				return certificateRequest.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1),DateTimeOffset.Now.AddDays(daysValid));
			}
		}

		///<summary>Writes a CA certificate as PEM into folder and returns the path.</summary>
		public static string WriteCaPem(string folder,string fileName="office-ca.pem") {
			string path=Path.Combine(folder,fileName);
			using(X509Certificate2 x509Certificate2=NewCertificate("CN=Synthetic Office CA",isCa:true)) {
				File.WriteAllText(path,OfficeCaFile.ToPem(new List<X509Certificate2> { new X509Certificate2(x509Certificate2.RawData) }),
					new UTF8Encoding(false));
			}
			return path;
		}

		///<summary>Writes a .pfx of a client certificate with its private key into folder and returns the path.</summary>
		public static string WritePfx(string folder,string password,bool isClientAuth=true,bool isServerAuthOnly=false,string fileName="client.pfx") {
			string path=Path.Combine(folder,fileName);
			using(X509Certificate2 x509Certificate2=NewCertificate("CN=synthetic-workstations",isCa:false,isClientAuth:isClientAuth,
				isServerAuthOnly:isServerAuthOnly))
			{
				File.WriteAllBytes(path,x509Certificate2.Export(X509ContentType.Pfx,password));
			}
			return path;
		}

		///<summary>A FreeDentalConfig.xml with these DatabaseConnection children (name, value pairs) and an AdminCompNames element.</summary>
		public static string WriteConfig(string folder,params string[] arrayNameValues) {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<ConnectionSettings>\r\n    <DatabaseConnection>\r\n");
			for(int i=0;i+1<arrayNameValues.Length;i+=2) {
				stringBuilder.Append("        <"+arrayNameValues[i]+">"+System.Security.SecurityElement.Escape(arrayNameValues[i+1])+"</"+arrayNameValues[i]+">\r\n");
			}
			stringBuilder.Append("    </DatabaseConnection>\r\n    <DatabaseType>MySql</DatabaseType>\r\n    <UseDynamicMode>False</UseDynamicMode>\r\n"
				+"    <AdminCompNames>\r\n        <CompName>SYNTHETIC-PC</CompName>\r\n    </AdminCompNames>\r\n</ConnectionSettings>\r\n");
			string path=Path.Combine(folder,"FreeDentalConfig.xml");
			File.WriteAllText(path,stringBuilder.ToString(),new UTF8Encoding(false));
			return path;
		}
	}
}
