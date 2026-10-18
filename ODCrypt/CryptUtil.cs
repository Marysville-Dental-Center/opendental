// ODCrypt: open source replacement for the prebuilt ODCrypt.dll.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Security.Cryptography;

namespace ODCrypt {
	///<summary>Cryptographically secure random values and constant-time comparison.</summary>
	public static class CryptUtil {
		///<summary>RNGCryptoServiceProvider.GetBytes is thread safe, so one instance is shared.</summary>
		private static readonly RandomNumberGenerator _rng=new RNGCryptoServiceProvider();

		///<summary>Compares two strings in time that depends only on their lengths, not on where they first differ. Use it to compare hashes and
		///tokens. Two nulls are equal; null never equals a non-null string.</summary>
		public static bool ConstantEquals(string lhs,string rhs) {
			if(lhs==null || rhs==null) {
				return lhs==null && rhs==null;
			}
			if(lhs.Length!=rhs.Length) {
				return false;
			}
			int diff=0;
			for(int i=0;i<lhs.Length;i++) {
				diff|=lhs[i]^rhs[i];
			}
			return diff==0;
		}

		///<summary>Returns byteLength cryptographically random bytes, base64 encoded. Returns an empty string when byteLength is 0.</summary>
		public static string GenerateSalt(int byteLength) {
			if(byteLength<0) {
				throw new ArgumentOutOfRangeException(nameof(byteLength));
			}
			return Convert.ToBase64String(GetRandomBytes(byteLength));
		}

		///<summary>Returns a base64 string encoding the given number of random bits (rounded up to whole bytes), e.g. 128 gives 16 bytes.</summary>
		public static string RandomString(int bits) {
			if(bits<0) {
				throw new ArgumentOutOfRangeException(nameof(bits));
			}
			return Convert.ToBase64String(GetRandomBytes((bits+7)/8));
		}

		///<summary>Returns a cryptographically random value of a primitive type.
		///Signed integer types return values in [0, MaxValue] so they can be used directly with % as an index (callers rely on that).
		///Unsigned integer types use their full range. double and float return values in [0, 1). bool is a fair coin.</summary>
		public static T Random<T>() where T : struct {
			Type type=typeof(T);
			if(type==typeof(int)) {
				return (T)(object)(int)(ToUInt32(GetRandomBytes(4))&0x7FFFFFFFu);
			}
			if(type==typeof(uint)) {
				return (T)(object)ToUInt32(GetRandomBytes(4));
			}
			if(type==typeof(long)) {
				return (T)(object)(long)(ToUInt64(GetRandomBytes(8))&0x7FFFFFFFFFFFFFFFUL);
			}
			if(type==typeof(ulong)) {
				return (T)(object)ToUInt64(GetRandomBytes(8));
			}
			if(type==typeof(short)) {
				return (T)(object)(short)(ToUInt32(GetRandomBytes(4))&0x7FFFu);
			}
			if(type==typeof(ushort)) {
				return (T)(object)(ushort)ToUInt32(GetRandomBytes(4));
			}
			if(type==typeof(byte)) {
				return (T)(object)GetRandomBytes(1)[0];
			}
			if(type==typeof(sbyte)) {
				return (T)(object)(sbyte)(GetRandomBytes(1)[0]&0x7F);
			}
			if(type==typeof(bool)) {
				return (T)(object)((GetRandomBytes(1)[0]&1)==1);
			}
			if(type==typeof(double)) {
				//53 random bits scaled into [0,1).
				return (T)(object)((ToUInt64(GetRandomBytes(8))>>11)*(1.0/(1UL<<53)));
			}
			if(type==typeof(float)) {
				//24 random bits scaled into [0,1).
				return (T)(object)((ToUInt32(GetRandomBytes(4))>>8)*(1.0f/(1<<24)));
			}
			throw new NotSupportedException("CryptUtil.Random does not support type "+type.FullName+".");
		}

		///<summary>Returns count cryptographically random bytes.</summary>
		public static byte[] GetRandomBytes(int count) {
			if(count<0) {
				throw new ArgumentOutOfRangeException(nameof(count));
			}
			byte[] arrayBytes=new byte[count];
			if(count>0) {
				_rng.GetBytes(arrayBytes);
			}
			return arrayBytes;
		}

		private static uint ToUInt32(byte[] arrayBytes) {
			return (uint)arrayBytes[0]|((uint)arrayBytes[1]<<8)|((uint)arrayBytes[2]<<16)|((uint)arrayBytes[3]<<24);
		}

		private static ulong ToUInt64(byte[] arrayBytes) {
			ulong value=0;
			for(int i=0;i<8;i++) {
				value|=(ulong)arrayBytes[i]<<(8*i);
			}
			return value;
		}
	}
}
