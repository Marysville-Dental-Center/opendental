// A throwaway certificate authority for the tests of the program's own server check (OfficeCaServerVerifier): CA, intermediate and server
// certificates and CRLs made in memory. Nothing goes into a certificate store of this computer and nothing reaches the network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using OpenPmsDentalDbSettings;

namespace DbConnectionSecurityTests {
	///<summary>A CA (or an intermediate of one) with its private key in memory (CertificateRequest), which issues server and client certificates
	///and signs CRLs. Each certificate gets a random serial number. Dispose drops the key.</summary>
	internal sealed class TestCertificateAuthority:IDisposable {
		///<summary>The CA certificate with its private key (ephemeral, never persisted).</summary>
		public X509Certificate2 Certificate { get; private set; }
		private readonly RSA _rsa;

		private TestCertificateAuthority(X509Certificate2 certificate,RSA rsa) {
			Certificate=certificate;
			_rsa=rsa;
		}

		///<summary>A self-signed root CA valid from five years ago to five years from now, so every certificate it issues fits in its dates.</summary>
		public static TestCertificateAuthority CreateRoot(string subject) {
			RSA rsa=RSA.Create(2048);
			CertificateRequest certificateRequest=new CertificateRequest(subject,rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
			certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
			certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign|X509KeyUsageFlags.CrlSign,true));
			certificateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey,false));
			X509Certificate2 certificate=certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddYears(-5),DateTimeOffset.UtcNow.AddYears(5));
			return new TestCertificateAuthority(certificate,rsa);
		}

		///<summary>An intermediate CA issued by this one, valid from four years ago to four years from now.</summary>
		public TestCertificateAuthority CreateIntermediate(string subject) {
			RSA rsa=RSA.Create(2048);
			CertificateRequest certificateRequest=new CertificateRequest(subject,rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
			certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
			certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign|X509KeyUsageFlags.CrlSign,true));
			certificateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey,false));
			using(X509Certificate2 certificate=certificateRequest.Create(Certificate,DateTimeOffset.UtcNow.AddYears(-4),DateTimeOffset.UtcNow.AddYears(4),
				NewSerial()))
			{
				return new TestCertificateAuthority(certificate.CopyWithPrivateKey(rsa),rsa);
			}
		}

		///<summary>The public part of the CA certificate.</summary>
		public X509Certificate2 GetPublic() {
			return new X509Certificate2(Certificate.RawData);
		}

		///<summary>Writes the public part of the CA certificate (and of extra, after it) as PEM into folder; returns the path.</summary>
		public string WritePem(string folder,string fileName,params X509Certificate2[] arrayExtra) {
			List<X509Certificate2> listCertificates=new List<X509Certificate2> { GetPublic() };
			foreach(X509Certificate2 x509Certificate2 in arrayExtra) {
				listCertificates.Add(new X509Certificate2(x509Certificate2.RawData));
			}
			string path=Path.Combine(folder,fileName);
			File.WriteAllText(path,OfficeCaFile.ToPem(listCertificates),new UTF8Encoding(false));
			return path;
		}

		///<summary>A certificate issued by this CA: the subject (a common name), the DNS names and IP addresses of its subject alternative names
		///(none: no such extension), its dates (null: yesterday to thirty days from now), and its extended key usage (server authentication by
		///default; client authentication; or none at all). Its serial number comes back in arraySerial. withPrivateKey gives the certificate
		///with a key SChannel can use (a PFX round trip, so a fake server can present it), otherwise the public part only.</summary>
		public X509Certificate2 Issue(string subject,string[] arrayDnsNames,IPAddress[] arrayIpAddresses,out byte[] arraySerial,
			DateTimeOffset? notBefore=null,DateTimeOffset? notAfter=null,bool isServerAuth=true,bool isClientAuth=false,bool withPrivateKey=false)
		{
			using(RSA rsa=RSA.Create(2048)) {
				CertificateRequest certificateRequest=new CertificateRequest(subject,rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
				certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
				certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
					X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
				if(isServerAuth || isClientAuth) {
					OidCollection oidCollection=new OidCollection();
					if(isServerAuth) {
						oidCollection.Add(new Oid("1.3.6.1.5.5.7.3.1"));
					}
					if(isClientAuth) {
						oidCollection.Add(new Oid("1.3.6.1.5.5.7.3.2"));
					}
					certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(oidCollection,false));
				}
				if((arrayDnsNames!=null && arrayDnsNames.Length>0) || (arrayIpAddresses!=null && arrayIpAddresses.Length>0)) {
					SubjectAlternativeNameBuilder subjectAlternativeNameBuilder=new SubjectAlternativeNameBuilder();
					foreach(string dnsName in arrayDnsNames??new string[0]) {
						subjectAlternativeNameBuilder.AddDnsName(dnsName);
					}
					foreach(IPAddress ipAddress in arrayIpAddresses??new IPAddress[0]) {
						subjectAlternativeNameBuilder.AddIpAddress(ipAddress);
					}
					certificateRequest.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());
				}
				arraySerial=NewSerial();
				using(X509Certificate2 certificate=certificateRequest.Create(Certificate,notBefore??DateTimeOffset.UtcNow.AddDays(-1),
					notAfter??DateTimeOffset.UtcNow.AddDays(30),arraySerial))
				{
					if(!withPrivateKey) {
						return new X509Certificate2(certificate.RawData);
					}
					using(X509Certificate2 certificateWithKey=certificate.CopyWithPrivateKey(rsa)) {
						return new X509Certificate2(certificateWithKey.Export(X509ContentType.Pfx,""),"",X509KeyStorageFlags.Exportable);
					}
				}
			}
		}

		///<summary>Issue for a server certificate whose serial number the test does not need.</summary>
		public X509Certificate2 Issue(string subject,string[] arrayDnsNames,IPAddress[] arrayIpAddresses,DateTimeOffset? notBefore=null,
			DateTimeOffset? notAfter=null,bool isServerAuth=true,bool isClientAuth=false,bool withPrivateKey=false)
		{
			byte[] arraySerial;
			return Issue(subject,arrayDnsNames,arrayIpAddresses,out arraySerial,notBefore,notAfter,isServerAuth,isClientAuth,withPrivateKey);
		}

		///<summary>A CRL (version 2, DER) signed by this CA, valid from two hours ago for seven days, that revokes these serial numbers (none:
		///an empty CRL, which says every certificate of the CA is good).</summary>
		public byte[] CreateCrl(params byte[][] arraySerials) {
			byte[] arrayAlgorithm=Der(0x30,Der(0x06,new byte[] { 0x2A,0x86,0x48,0x86,0xF7,0x0D,0x01,0x01,0x0B }),new byte[] { 0x05,0x00 });//sha256RSA
			List<byte[]> listTbs=new List<byte[]>();
			listTbs.Add(Der(0x02,new byte[] { 0x01 }));//version 2
			listTbs.Add(arrayAlgorithm);
			listTbs.Add(Certificate.SubjectName.RawData);
			listTbs.Add(DerTime(DateTime.UtcNow.AddHours(-2)));
			listTbs.Add(DerTime(DateTime.UtcNow.AddDays(7)));
			if(arraySerials.Length>0) {
				List<byte[]> listRevoked=new List<byte[]>();
				foreach(byte[] arraySerial in arraySerials) {
					listRevoked.Add(Der(0x30,DerInteger(arraySerial),DerTime(DateTime.UtcNow.AddHours(-1))));
				}
				listTbs.Add(Der(0x30,listRevoked.ToArray()));
			}
			byte[] arrayTbs=Der(0x30,listTbs.ToArray());
			byte[] arraySignature=_rsa.SignData(arrayTbs,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
			byte[] arrayBitString=new byte[arraySignature.Length+1];//no unused bits
			Array.Copy(arraySignature,0,arrayBitString,1,arraySignature.Length);
			return Der(0x30,arrayTbs,arrayAlgorithm,Der(0x03,arrayBitString));
		}

		public void Dispose() {
			Certificate.Reset();
			_rsa.Dispose();
		}

		///<summary>Eight random bytes, the first between 0x01 and 0x7F, so the serial is a positive INTEGER of that length.</summary>
		private static byte[] NewSerial() {
			byte[] arraySerial=new byte[8];
			using(RandomNumberGenerator randomNumberGenerator=RandomNumberGenerator.Create()) {
				randomNumberGenerator.GetBytes(arraySerial);
			}
			arraySerial[0]=(byte)(1+arraySerial[0]%0x7F);
			return arraySerial;
		}

		private static byte[] Der(byte tag,params byte[][] arrayParts) {
			MemoryStream memoryStreamBody=new MemoryStream();
			foreach(byte[] arrayPart in arrayParts) {
				memoryStreamBody.Write(arrayPart,0,arrayPart.Length);
			}
			byte[] arrayBody=memoryStreamBody.ToArray();
			MemoryStream memoryStream=new MemoryStream();
			memoryStream.WriteByte(tag);
			if(arrayBody.Length<0x80) {
				memoryStream.WriteByte((byte)arrayBody.Length);
			}
			else if(arrayBody.Length<0x100) {
				memoryStream.WriteByte(0x81);
				memoryStream.WriteByte((byte)arrayBody.Length);
			}
			else {
				memoryStream.WriteByte(0x82);
				memoryStream.WriteByte((byte)(arrayBody.Length>>8));
				memoryStream.WriteByte((byte)(arrayBody.Length&0xFF));
			}
			memoryStream.Write(arrayBody,0,arrayBody.Length);
			return memoryStream.ToArray();
		}

		private static byte[] DerTime(DateTime dateTimeUtc) {
			return Der(0x17,Encoding.ASCII.GetBytes(dateTimeUtc.ToString("yyMMddHHmmss",System.Globalization.CultureInfo.InvariantCulture)+"Z"));
		}

		private static byte[] DerInteger(byte[] arrayValue) {
			if((arrayValue[0]&0x80)!=0) {
				byte[] arrayPadded=new byte[arrayValue.Length+1];
				Array.Copy(arrayValue,0,arrayPadded,1,arrayValue.Length);
				arrayValue=arrayPadded;
			}
			return Der(0x02,arrayValue);
		}
	}
}
