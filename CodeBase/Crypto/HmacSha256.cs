// Secret obfuscation and SecurityHash tamper hashes, built on .NET's own crypto providers.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Security.Cryptography;

namespace CodeBase {
	///<summary>HMAC-SHA256 (RFC 2104) over SHA256CryptoServiceProvider. The framework's HMACSHA256 class uses SHA256Managed, which throws on
	///machines with Windows FIPS policy enforced; the CryptoAPI provider used here doesn't. Not thread safe: lock around shared instances.</summary>
	internal sealed class HmacSha256 : IDisposable {
		private const int BlockSize=64;
		private readonly byte[] _arrayInnerPad;
		private readonly byte[] _arrayOuterPad;
		private SHA256 _sha256;

		internal HmacSha256(byte[] arrayKey) {
			if(arrayKey==null) {
				throw new ArgumentNullException(nameof(arrayKey));
			}
			_sha256=new SHA256CryptoServiceProvider();
			if(arrayKey.Length>BlockSize) {
				arrayKey=_sha256.ComputeHash(arrayKey);
			}
			_arrayInnerPad=new byte[BlockSize];
			_arrayOuterPad=new byte[BlockSize];
			for(int i=0;i<BlockSize;i++) {
				byte keyByte=(i<arrayKey.Length) ? arrayKey[i] : (byte)0;
				_arrayInnerPad[i]=(byte)(keyByte^0x36);
				_arrayOuterPad[i]=(byte)(keyByte^0x5c);
			}
		}

		///<summary>Returns the 32-byte MAC of data.</summary>
		internal byte[] ComputeHash(byte[] data) {
			if(_sha256==null) {
				throw new ObjectDisposedException(nameof(HmacSha256));
			}
			byte[] arrayInner=_sha256.ComputeHash(Concat(_arrayInnerPad,data));
			return _sha256.ComputeHash(Concat(_arrayOuterPad,arrayInner));
		}

		///<summary>One-shot HMAC-SHA256.</summary>
		internal static byte[] Compute(byte[] arrayKey,byte[] data) {
			using(HmacSha256 hmac=new HmacSha256(arrayKey)) {
				return hmac.ComputeHash(data);
			}
		}

		///<summary>SHA-256 of data.</summary>
		internal static byte[] Sha256(byte[] data) {
			using(SHA256 sha256=new SHA256CryptoServiceProvider()) {
				return sha256.ComputeHash(data);
			}
		}

		///<summary>Compares two byte arrays in time that depends only on their lengths.</summary>
		internal static bool ConstantEquals(byte[] arrayA,byte[] arrayB) {
			if(arrayA==null || arrayB==null || arrayA.Length!=arrayB.Length) {
				return false;
			}
			int diff=0;
			for(int i=0;i<arrayA.Length;i++) {
				diff|=arrayA[i]^arrayB[i];
			}
			return diff==0;
		}

		public void Dispose() {
			_sha256?.Dispose();
			_sha256=null;
		}

		private static byte[] Concat(byte[] arrayFirst,byte[] arraySecond) {
			byte[] arrayResult=new byte[arrayFirst.Length+arraySecond.Length];
			Buffer.BlockCopy(arrayFirst,0,arrayResult,0,arrayFirst.Length);
			Buffer.BlockCopy(arraySecond,0,arrayResult,arrayFirst.Length,arraySecond.Length);
			return arrayResult;
		}
	}
}
