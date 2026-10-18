// Tests for the open source CDT and ODCrypt libraries.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text;

namespace CryptoTests {
	internal static class TestUtil {
		internal static string ToHex(byte[] arrayBytes) {
			StringBuilder stringBuilder=new StringBuilder(arrayBytes.Length*2);
			foreach(byte b in arrayBytes) {
				stringBuilder.Append(b.ToString("x2"));
			}
			return stringBuilder.ToString();
		}

		internal static byte[] FromHex(string hex) {
			byte[] arrayBytes=new byte[hex.Length/2];
			for(int i=0;i<arrayBytes.Length;i++) {
				arrayBytes[i]=Convert.ToByte(hex.Substring(i*2,2),16);
			}
			return arrayBytes;
		}

		///<summary>Deterministic pseudo-random bytes so failures are reproducible.</summary>
		internal static byte[] PseudoRandomBytes(int length,int seed) {
			Random random=new Random(seed);
			byte[] arrayBytes=new byte[length];
			random.NextBytes(arrayBytes);
			return arrayBytes;
		}
	}
}
