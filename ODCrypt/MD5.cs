// ODCrypt: open source replacement for the prebuilt ODCrypt.dll.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ODCrypt {
	///<summary>MD5 message digest (RFC 1321). Implemented in managed code so it gives the same bytes on every machine, including ones where
	///Windows FIPS policy makes the framework's MD5 classes throw. MD5 is broken for security purposes; it is kept only because existing user
	///password hashes, signature keys and file checksums stored in the database depend on it.</summary>
	public static class MD5 {
		///<summary>Returns the 16-byte MD5 digest of data.</summary>
		public static byte[] Hash(byte[] data) {
			if(data==null) {
				throw new ArgumentNullException(nameof(data));
			}
			return Md5Managed.ComputeHash(data);
		}
	}

	///<summary>Straightforward RFC 1321 implementation.</summary>
	internal static class Md5Managed {
		///<summary>Per-step additive constants: the integer part of 2^32*abs(sin(i+1)), as listed in RFC 1321.</summary>
		private static readonly uint[] _arrayK=new uint[] {
			0xd76aa478,0xe8c7b756,0x242070db,0xc1bdceee,0xf57c0faf,0x4787c62a,0xa8304613,0xfd469501,
			0x698098d8,0x8b44f7af,0xffff5bb1,0x895cd7be,0x6b901122,0xfd987193,0xa679438e,0x49b40821,
			0xf61e2562,0xc040b340,0x265e5a51,0xe9b6c7aa,0xd62f105d,0x02441453,0xd8a1e681,0xe7d3fbc8,
			0x21e1cde6,0xc33707d6,0xf4d50d87,0x455a14ed,0xa9e3e905,0xfcefa3f8,0x676f02d9,0x8d2a4c8a,
			0xfffa3942,0x8771f681,0x6d9d6122,0xfde5380c,0xa4beea44,0x4bdecfa9,0xf6bb4b60,0xbebfbc70,
			0x289b7ec6,0xeaa127fa,0xd4ef3085,0x04881d05,0xd9d4d039,0xe6db99e5,0x1fa27cf8,0xc4ac5665,
			0xf4292244,0x432aff97,0xab9423a7,0xfc93a039,0x655b59c3,0x8f0ccc92,0xffeff47d,0x85845dd1,
			0x6fa87e4f,0xfe2ce6e0,0xa3014314,0x4e0811a1,0xf7537e82,0xbd3af235,0x2ad7d2bb,0xeb86d391,
		};

		///<summary>Per-step left-rotation amounts.</summary>
		private static readonly int[] _arrayShift=new int[] {
			7,12,17,22,7,12,17,22,7,12,17,22,7,12,17,22,
			5,9,14,20,5,9,14,20,5,9,14,20,5,9,14,20,
			4,11,16,23,4,11,16,23,4,11,16,23,4,11,16,23,
			6,10,15,21,6,10,15,21,6,10,15,21,6,10,15,21,
		};

		internal static byte[] ComputeHash(byte[] data) {
			uint a0=0x67452301;
			uint b0=0xefcdab89;
			uint c0=0x98badcfe;
			uint d0=0x10325476;
			uint[] arrayWords=new uint[16];
			int countFullBlocks=data.Length/64;
			for(int block=0;block<countFullBlocks;block++) {
				LoadWords(data,block*64,arrayWords);
				ProcessBlock(arrayWords,ref a0,ref b0,ref c0,ref d0);
			}
			//Padding: 0x80, zeros up to 56 mod 64, then the message length in bits as a little-endian 64-bit integer.
			int countRemaining=data.Length-countFullBlocks*64;
			int lengthTail=(countRemaining<56) ? 64 : 128;
			byte[] arrayTail=new byte[lengthTail];
			Buffer.BlockCopy(data,countFullBlocks*64,arrayTail,0,countRemaining);
			arrayTail[countRemaining]=0x80;
			ulong lengthBits=(ulong)data.LongLength*8;
			for(int i=0;i<8;i++) {
				arrayTail[lengthTail-8+i]=(byte)(lengthBits>>(8*i));
			}
			for(int offset=0;offset<lengthTail;offset+=64) {
				LoadWords(arrayTail,offset,arrayWords);
				ProcessBlock(arrayWords,ref a0,ref b0,ref c0,ref d0);
			}
			byte[] arrayDigest=new byte[16];
			StoreWord(a0,arrayDigest,0);
			StoreWord(b0,arrayDigest,4);
			StoreWord(c0,arrayDigest,8);
			StoreWord(d0,arrayDigest,12);
			return arrayDigest;
		}

		private static void LoadWords(byte[] buffer,int offset,uint[] arrayWords) {
			for(int i=0;i<16;i++) {
				int index=offset+i*4;
				arrayWords[i]=(uint)(buffer[index]|(buffer[index+1]<<8)|(buffer[index+2]<<16)|(buffer[index+3]<<24));
			}
		}

		private static void StoreWord(uint value,byte[] buffer,int offset) {
			buffer[offset]=(byte)value;
			buffer[offset+1]=(byte)(value>>8);
			buffer[offset+2]=(byte)(value>>16);
			buffer[offset+3]=(byte)(value>>24);
		}

		private static void ProcessBlock(uint[] arrayWords,ref uint a0,ref uint b0,ref uint c0,ref uint d0) {
			uint a=a0;
			uint b=b0;
			uint c=c0;
			uint d=d0;
			for(int i=0;i<64;i++) {
				uint f;
				int g;
				if(i<16) {
					f=(b&c)|(~b&d);
					g=i;
				}
				else if(i<32) {
					f=(d&b)|(~d&c);
					g=(5*i+1)%16;
				}
				else if(i<48) {
					f=b^c^d;
					g=(3*i+5)%16;
				}
				else {
					f=c^(b|~d);
					g=(7*i)%16;
				}
				f=f+a+_arrayK[i]+arrayWords[g];
				a=d;
				d=c;
				c=b;
				b=b+RotateLeft(f,_arrayShift[i]);
			}
			a0+=a;
			b0+=b;
			c0+=c;
			d0+=d;
		}

		private static uint RotateLeft(uint value,int count) {
			return (value<<count)|(value>>(32-count));
		}
	}
}
