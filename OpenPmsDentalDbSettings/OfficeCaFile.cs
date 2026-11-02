// The office CA certificate file: checked, then copied to %ProgramData% as PEM, where SslCa points.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenPmsDentalDbSettings {
	///<summary>The office CA certificate (owner decision Q5): the server's certificate must chain to it. A PEM file (one or more CERTIFICATE
	///blocks, as the server guide's openssl commands write it) or one DER certificate. Every certificate must be a CA when it says what it is
	///(basic constraints). The tool keeps its own copy (DbConnectionConfig.GetOfficeCaCopyPath), so the connection never depends on the file the
	///administrator picked, which may be on a removable drive or a share.</summary>
	public static class OfficeCaFile {
		private static readonly Regex _regexPemCertificate=new Regex(
			@"-----BEGIN CERTIFICATE-----\s*(?<base64>[A-Za-z0-9+/=\s]+?)\s*-----END CERTIFICATE-----",RegexOptions.CultureInvariant);

		///<summary>The certificates of the file. Throws DbSettingsException (exit code 2) when it is missing, is not a certificate file, or holds
		///a certificate that is not a CA.</summary>
		public static List<X509Certificate2> Load(string path) {
			byte[] arrayBytes;
			try {
				if(!File.Exists(path)) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" does not exist.");
				}
				arrayBytes=File.ReadAllBytes(path);
			}
			catch(IOException ex) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" cannot be read: "+ex.Message);
			}
			catch(UnauthorizedAccessException ex) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" cannot be read: "+ex.Message);
			}
			List<X509Certificate2> listCertificates=new List<X509Certificate2>();
			string text=Encoding.ASCII.GetString(arrayBytes);
			MatchCollection matchCollection=_regexPemCertificate.Matches(text);
			try {
				if(matchCollection.Count>0) {
					foreach(Match match in matchCollection) {
						listCertificates.Add(new X509Certificate2(Convert.FromBase64String(match.Groups["base64"].Value)));
					}
				}
				else {
					listCertificates.Add(new X509Certificate2(arrayBytes));//DER
				}
			}
			catch(Exception ex) when(ex is CryptographicException || ex is FormatException) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" is not a certificate file "
					+"(PEM or DER).");
			}
			foreach(X509Certificate2 x509Certificate2 in listCertificates) {
				if(x509Certificate2.HasPrivateKey) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" holds a private key. Give "
						+"only the certificate (ca.pem), never the CA's key.");
				}
				X509BasicConstraintsExtension x509BasicConstraintsExtension=x509Certificate2.Extensions.OfType<X509BasicConstraintsExtension>()
					.FirstOrDefault();
				if(x509BasicConstraintsExtension!=null && !x509BasicConstraintsExtension.CertificateAuthority) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" holds a certificate that is "
						+"not a certificate authority ("+x509Certificate2.Subject+"). Give the office CA certificate, not the server's.");
				}
			}
			return listCertificates;
		}

		///<summary>The certificates as PEM text, in order.</summary>
		public static string ToPem(List<X509Certificate2> listCertificates) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(X509Certificate2 x509Certificate2 in listCertificates) {
				stringBuilder.Append("-----BEGIN CERTIFICATE-----\n");
				stringBuilder.Append(Convert.ToBase64String(x509Certificate2.RawData,Base64FormattingOptions.InsertLineBreaks).Replace("\r\n","\n"));
				stringBuilder.Append("\n-----END CERTIFICATE-----\n");
			}
			return stringBuilder.ToString();
		}

		///<summary>Writes the certificates as PEM to pathCopy (creating its folder), replacing an earlier copy. Throws IOException or
		///UnauthorizedAccessException.</summary>
		public static void WriteCopy(List<X509Certificate2> listCertificates,string pathCopy) {
			Directory.CreateDirectory(Path.GetDirectoryName(pathCopy));
			string pathTemp=pathCopy+".new";
			File.WriteAllText(pathTemp,ToPem(listCertificates),new UTF8Encoding(false));
			if(File.Exists(pathCopy)) {
				File.Replace(pathTemp,pathCopy,null);
			}
			else {
				File.Move(pathTemp,pathCopy);
			}
		}

		///<summary>"subject, expires yyyy-MM-dd" of the first certificate, for the window.</summary>
		public static string Describe(List<X509Certificate2> listCertificates) {
			if(listCertificates.Count==0) {
				return "";
			}
			return listCertificates[0].Subject+", expires "+listCertificates[0].NotAfter.ToString("yyyy-MM-dd");
		}
	}
}
