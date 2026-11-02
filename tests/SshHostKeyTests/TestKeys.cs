// Synthetic SSH host keys, made with BouncyCastle for each run, as SSH.NET hands them to the HostKeyReceived event, and a host key store in memory.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenPmsDentalStorage;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Security;

namespace SshHostKeyTests {
	///<summary>Host keys of synthetic servers.</summary>
	public static class TestKeys {
		///<summary>A PKCS#8 PEM private key, new for each call.</summary>
		public static string MakeEd25519Pem() {
			Ed25519KeyPairGenerator ed25519KeyPairGenerator=new Ed25519KeyPairGenerator();
			ed25519KeyPairGenerator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
			return ToPem(ed25519KeyPairGenerator.GenerateKeyPair().Private);
		}

		public static string MakeRsaPem() {
			RsaKeyPairGenerator rsaKeyPairGenerator=new RsaKeyPairGenerator();
			rsaKeyPairGenerator.Init(new KeyGenerationParameters(new SecureRandom(),1024));
			return ToPem(rsaKeyPairGenerator.GenerateKeyPair().Private);
		}

		///<summary>A SEC 1 PEM private key on nistp256 ("EC PRIVATE KEY", as OpenSSL writes it): SSH.NET's PKCS#8 reader does not expect the curve
		///that BouncyCastle writes inside the EC key.</summary>
		public static string MakeEcdsaPem() {
			ECKeyPairGenerator ecKeyPairGenerator=new ECKeyPairGenerator("ECDSA");
			ecKeyPairGenerator.Init(new ECKeyGenerationParameters(SecObjectIdentifiers.SecP256r1,new SecureRandom()));
			byte[] arrayDer=PrivateKeyInfoFactory.CreatePrivateKeyInfo(ecKeyPairGenerator.GenerateKeyPair().Private).ParsePrivateKey().GetDerEncoded();
			return "-----BEGIN EC PRIVATE KEY-----\n"+Convert.ToBase64String(arrayDer,Base64FormattingOptions.InsertLineBreaks)+"\n-----END EC PRIVATE KEY-----\n";
		}

		private static string ToPem(AsymmetricKeyParameter asymmetricKeyParameter) {
			byte[] arrayDer=PrivateKeyInfoFactory.CreatePrivateKeyInfo(asymmetricKeyParameter).GetEncoded();
			return "-----BEGIN PRIVATE KEY-----\n"+Convert.ToBase64String(arrayDer,Base64FormattingOptions.InsertLineBreaks)+"\n-----END PRIVATE KEY-----\n";
		}

		///<summary>What SSH.NET hands the HostKeyReceived event for each host key algorithm of the key.</summary>
		public static List<HostKeyEventArgs> GetHostKeyEvents(string pem) {
			using(MemoryStream memoryStream=new MemoryStream(Encoding.ASCII.GetBytes(pem)))
			using(PrivateKeyFile privateKeyFile=new PrivateKeyFile(memoryStream))
			{
				return privateKeyFile.HostKeyAlgorithms.Cast<KeyHostAlgorithm>().Select(x => new HostKeyEventArgs(x)).ToList();
			}
		}

		///<summary>The event of a new Ed25519 key.</summary>
		public static HostKeyEventArgs NewEd25519Event() {
			return GetHostKeyEvents(MakeEd25519Pem())[0];
		}
	}

	///<summary>A host key store in memory that counts what the check asks of it, and can be made to fail.</summary>
	public class MemoryHostKeyStore:ISshHostKeyStore {
		public readonly Dictionary<string,SshHostKeyFingerprint> DictKeys=new Dictionary<string,SshHostKeyFingerprint>();
		public readonly List<string> ListRefusals=new List<string>();
		public int CountReads;
		public int CountStores;
		public bool DoFailReading;
		public bool DoFailStoring;
		///<summary>Set: the key another connection stores at the same moment as this one, which StoreFirstKey then returns instead.</summary>
		public SshHostKeyFingerprint FingerprintStoredByOther;

		public SshHostKeyFingerprint GetStoredKey(string host,int port) {
			CountReads++;
			if(DoFailReading) {
				throw new ApplicationException("Synthetic read failure.");
			}
			DictKeys.TryGetValue(host+":"+port,out SshHostKeyFingerprint fingerprint);
			return fingerprint;
		}

		public SshHostKeyFingerprint StoreFirstKey(string host,int port,SshHostKeyFingerprint fingerprint) {
			CountStores++;
			if(DoFailStoring) {
				throw new ApplicationException("Synthetic write failure.");
			}
			if(FingerprintStoredByOther!=null) {
				DictKeys[host+":"+port]=FingerprintStoredByOther;
			}
			if(!DictKeys.ContainsKey(host+":"+port)) {
				DictKeys[host+":"+port]=fingerprint;
			}
			return DictKeys[host+":"+port];
		}

		public void RecordRefusedKey(string host,int port,SshHostKeyFingerprint fingerprintStored,SshHostKeyFingerprint fingerprintRefused) {
			ListRefusals.Add(host+":"+port+" "+fingerprintStored+" -> "+fingerprintRefused);
		}
	}
}
