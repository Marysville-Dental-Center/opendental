// Tests for CodeBase's Secrets and TamperHash and the ODCrypt library.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CryptoTests {
	///<summary>CodeBase.TamperHash.Compute, which backs the SecurityHash columns. Expected values were computed independently with Python's
	///hmac/hashlib from the key derivation documented in CodeBase/Crypto/README.md.</summary>
	[TestClass]
	public class TamperHashTests {
		[TestMethod]
		public void KnownAnswers() {
			Assert.AreEqual("osh1$oulUndPsYqRmZczXoMDnh9ic7sTge635b8I9YMWXSok=",CodeBase.TamperHash.Compute("12345"));
			Assert.AreEqual("osh1$C7Qn9u1iY+98i8XZqoTnQH7ymKGQOs9bDaqFCJNlWPc=",CodeBase.TamperHash.Compute("102026-10-18 09:30:00"));
			Assert.AreEqual("osh1$EdnKpM4ayik32z9Lsprq6Pemb+igHLXDALcBf6StXaU=",CodeBase.TamperHash.Compute(""));
			Assert.AreEqual("osh1$J8kLWLp74PoLAK9tpqFXPboO/ed3eEIwsYsthWK1Fkw=",CodeBase.TamperHash.Compute("Zoë é"));
			Assert.AreEqual("osh1$kTpyvHDXNMR2LcdISrRU31m/bHbPwtEpYdXFVdWLwhs=",CodeBase.TamperHash.Compute("12345",isPatientTable:true));
		}

		[TestMethod]
		public void NullHashesLikeEmpty() {
			Assert.AreEqual(CodeBase.TamperHash.Compute(""),CodeBase.TamperHash.Compute(null));
		}

		[TestMethod]
		public void PatientTableUsesSeparateKey() {
			Assert.AreNotEqual(CodeBase.TamperHash.Compute("42"),CodeBase.TamperHash.Compute("42",isPatientTable:true));
		}

		[TestMethod]
		public void FitsSecurityHashColumn() {
			string hash=CodeBase.TamperHash.Compute(new string('9',1000));
			Assert.IsTrue(hash.Length<=255);
			Assert.IsTrue(hash.StartsWith(CodeBase.TamperHash.Prefix));
		}

		[TestMethod]
		public void IsCurrentSaltedHash_DetectsFormat() {
			Assert.IsTrue(CodeBase.TamperHash.IsCurrentFormat(CodeBase.TamperHash.Compute("1")));
			Assert.IsFalse(CodeBase.TamperHash.IsCurrentFormat("Q2xhaW1zIGhhc2ggZnJvbSBPcGVuIERlbnRhbA=="));
			Assert.IsFalse(CodeBase.TamperHash.IsCurrentFormat(""));
			Assert.IsFalse(CodeBase.TamperHash.IsCurrentFormat(null));
		}

		///<summary>SecurityHash.UpdateHashing hashes from several threads and then calls DisposeHashAlgorithm from another one.</summary>
		[TestMethod]
		public void ThreadSafeAndSurvivesDispose() {
			string[] arrayExpected=Enumerable.Range(0,500).Select(x => CodeBase.TamperHash.Compute(x.ToString())).ToArray();
			string[] arrayActual=new string[500];
			Parallel.For(0,500,i => {
				arrayActual[i]=CodeBase.TamperHash.Compute(i.ToString());
				if(i%97==0) {
					CodeBase.TamperHash.ReleaseProviders();
				}
			});
			CollectionAssert.AreEqual(arrayExpected,arrayActual);
			CodeBase.TamperHash.ReleaseProviders();
			Assert.AreEqual("osh1$oulUndPsYqRmZczXoMDnh9ic7sTge635b8I9YMWXSok=",CodeBase.TamperHash.Compute("12345"));
		}
	}
}
