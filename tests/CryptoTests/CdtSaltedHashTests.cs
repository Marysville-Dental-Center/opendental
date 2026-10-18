// Tests for the open source CDT and ODCrypt libraries.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CryptoTests {
	///<summary>CDT.Class1.CreateSaltedHash, which backs the SecurityHash columns. Expected values were computed independently with Python's
	///hmac/hashlib from the key derivation documented in CDT/README.md.</summary>
	[TestClass]
	public class CdtSaltedHashTests {
		[TestMethod]
		public void KnownAnswers() {
			Assert.AreEqual("osh1$oulUndPsYqRmZczXoMDnh9ic7sTge635b8I9YMWXSok=",CDT.Class1.CreateSaltedHash("12345"));
			Assert.AreEqual("osh1$C7Qn9u1iY+98i8XZqoTnQH7ymKGQOs9bDaqFCJNlWPc=",CDT.Class1.CreateSaltedHash("102026-10-18 09:30:00"));
			Assert.AreEqual("osh1$EdnKpM4ayik32z9Lsprq6Pemb+igHLXDALcBf6StXaU=",CDT.Class1.CreateSaltedHash(""));
			Assert.AreEqual("osh1$J8kLWLp74PoLAK9tpqFXPboO/ed3eEIwsYsthWK1Fkw=",CDT.Class1.CreateSaltedHash("Zoë é"));
			Assert.AreEqual("osh1$kTpyvHDXNMR2LcdISrRU31m/bHbPwtEpYdXFVdWLwhs=",CDT.Class1.CreateSaltedHash("12345",isPatientTable:true));
		}

		[TestMethod]
		public void NullHashesLikeEmpty() {
			Assert.AreEqual(CDT.Class1.CreateSaltedHash(""),CDT.Class1.CreateSaltedHash(null));
		}

		[TestMethod]
		public void PatientTableUsesSeparateKey() {
			Assert.AreNotEqual(CDT.Class1.CreateSaltedHash("42"),CDT.Class1.CreateSaltedHash("42",isPatientTable:true));
		}

		[TestMethod]
		public void FitsSecurityHashColumn() {
			string hash=CDT.Class1.CreateSaltedHash(new string('9',1000));
			Assert.IsTrue(hash.Length<=255);
			Assert.IsTrue(hash.StartsWith(CDT.Class1.SaltedHashPrefix));
		}

		[TestMethod]
		public void IsCurrentSaltedHash_DetectsFormat() {
			Assert.IsTrue(CDT.Class1.IsCurrentSaltedHash(CDT.Class1.CreateSaltedHash("1")));
			Assert.IsFalse(CDT.Class1.IsCurrentSaltedHash("Q2xhaW1zIGhhc2ggZnJvbSBPcGVuIERlbnRhbA=="));
			Assert.IsFalse(CDT.Class1.IsCurrentSaltedHash(""));
			Assert.IsFalse(CDT.Class1.IsCurrentSaltedHash(null));
		}

		///<summary>SecurityHash.UpdateHashing hashes from several threads and then calls DisposeHashAlgorithm from another one.</summary>
		[TestMethod]
		public void ThreadSafeAndSurvivesDispose() {
			string[] arrayExpected=Enumerable.Range(0,500).Select(x => CDT.Class1.CreateSaltedHash(x.ToString())).ToArray();
			string[] arrayActual=new string[500];
			Parallel.For(0,500,i => {
				arrayActual[i]=CDT.Class1.CreateSaltedHash(i.ToString());
				if(i%97==0) {
					CDT.Class1.DisposeHashAlgorithm();
				}
			});
			CollectionAssert.AreEqual(arrayExpected,arrayActual);
			CDT.Class1.DisposeHashAlgorithm();
			Assert.AreEqual("osh1$oulUndPsYqRmZczXoMDnh9ic7sTge635b8I9YMWXSok=",CDT.Class1.CreateSaltedHash("12345"));
		}
	}
}
