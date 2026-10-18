// ODCrypt: open source replacement for the prebuilt ODCrypt.dll.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ODCrypt {
	///<summary>SHA3-512 as specified in FIPS 202. .NET Framework 4.8 has no SHA-3, so this is a self-contained Keccak-f[1600] sponge.
	///User login hashes of type SHA3_512 are base64(SHA3-512(UTF-16LE(salt+password))), so this must stay byte-for-byte standard.</summary>
	public static class Sha3 {
		///<summary>Returns the 64-byte SHA3-512 digest of data.</summary>
		public static byte[] Hash(byte[] data) {
			if(data==null) {
				throw new ArgumentNullException(nameof(data));
			}
			return Keccak.Sha3_512(data);
		}
	}

	///<summary>Keccak-f[1600] permutation and the SHA3-512 sponge construction.</summary>
	internal static class Keccak {
		///<summary>SHA3-512 rate: 1600-2*512 bits = 72 bytes = 9 lanes.</summary>
		private const int RateBytes=72;
		private const int OutputBytes=64;

		///<summary>Round constants for the iota step.</summary>
		private static readonly ulong[] _arrayRoundConstants=new ulong[] {
			0x0000000000000001UL,0x0000000000008082UL,0x800000000000808AUL,0x8000000080008000UL,
			0x000000000000808BUL,0x0000000080000001UL,0x8000000080008081UL,0x8000000000008009UL,
			0x000000000000008AUL,0x0000000000000088UL,0x0000000080008009UL,0x000000008000000AUL,
			0x000000008000808BUL,0x800000000000008BUL,0x8000000000008089UL,0x8000000000008003UL,
			0x8000000000008002UL,0x8000000000000080UL,0x000000000000800AUL,0x800000008000000AUL,
			0x8000000080008081UL,0x8000000000008080UL,0x0000000080000001UL,0x8000000080008008UL,
		};

		///<summary>Rotation offsets for the rho step, indexed by lane x+5*y.</summary>
		private static readonly int[] _arrayRotations=new int[] {
			0,1,62,28,27,
			36,44,6,55,20,
			3,10,43,25,39,
			41,45,15,21,8,
			18,2,61,56,14,
		};

		internal static byte[] Sha3_512(byte[] data) {
			ulong[] arrayState=new ulong[25];
			int countFullBlocks=data.Length/RateBytes;
			for(int block=0;block<countFullBlocks;block++) {
				AbsorbBlock(arrayState,data,block*RateBytes);
			}
			//Final block with SHA-3 domain padding: append the bits 01, then pad10*1. In bytes that is 0x06 ... 0x80 (0x86 if they coincide).
			byte[] arrayLast=new byte[RateBytes];
			int countRemaining=data.Length-countFullBlocks*RateBytes;
			Buffer.BlockCopy(data,countFullBlocks*RateBytes,arrayLast,0,countRemaining);
			arrayLast[countRemaining]^=0x06;
			arrayLast[RateBytes-1]^=0x80;
			AbsorbBlock(arrayState,arrayLast,0);
			//The 64-byte output is smaller than the rate, so a single squeeze is enough.
			byte[] arrayDigest=new byte[OutputBytes];
			for(int i=0;i<OutputBytes/8;i++) {
				ulong lane=arrayState[i];
				for(int j=0;j<8;j++) {
					arrayDigest[i*8+j]=(byte)(lane>>(8*j));
				}
			}
			return arrayDigest;
		}

		private static void AbsorbBlock(ulong[] arrayState,byte[] buffer,int offset) {
			for(int i=0;i<RateBytes/8;i++) {
				ulong lane=0;
				for(int j=0;j<8;j++) {
					lane|=(ulong)buffer[offset+i*8+j]<<(8*j);
				}
				arrayState[i]^=lane;
			}
			Permute(arrayState);
		}

		///<summary>Destination lane of the pi step for each source lane x+5*y: B[y,2x+3y].</summary>
		private static readonly int[] _arrayPiDestination=CreatePiDestinations();

		private static int[] CreatePiDestinations() {
			int[] arrayDest=new int[25];
			for(int x=0;x<5;x++) {
				for(int y=0;y<5;y++) {
					arrayDest[x+5*y]=y+5*((2*x+3*y)%5);
				}
			}
			return arrayDest;
		}

		///<summary>Keccak-f[1600]: 24 rounds of theta, rho, pi, chi and iota over 25 64-bit lanes.</summary>
		internal static void Permute(ulong[] arrayState) {
			ulong[] arrayB=new ulong[25];
			for(int round=0;round<24;round++) {
				//Theta
				ulong c0=arrayState[0]^arrayState[5]^arrayState[10]^arrayState[15]^arrayState[20];
				ulong c1=arrayState[1]^arrayState[6]^arrayState[11]^arrayState[16]^arrayState[21];
				ulong c2=arrayState[2]^arrayState[7]^arrayState[12]^arrayState[17]^arrayState[22];
				ulong c3=arrayState[3]^arrayState[8]^arrayState[13]^arrayState[18]^arrayState[23];
				ulong c4=arrayState[4]^arrayState[9]^arrayState[14]^arrayState[19]^arrayState[24];
				ulong d0=c4^RotateLeft(c1,1);
				ulong d1=c0^RotateLeft(c2,1);
				ulong d2=c1^RotateLeft(c3,1);
				ulong d3=c2^RotateLeft(c4,1);
				ulong d4=c3^RotateLeft(c0,1);
				for(int y=0;y<25;y+=5) {
					arrayState[y]^=d0;
					arrayState[y+1]^=d1;
					arrayState[y+2]^=d2;
					arrayState[y+3]^=d3;
					arrayState[y+4]^=d4;
				}
				//Rho and pi
				for(int i=0;i<25;i++) {
					arrayB[_arrayPiDestination[i]]=RotateLeft(arrayState[i],_arrayRotations[i]);
				}
				//Chi
				for(int y=0;y<25;y+=5) {
					ulong b0=arrayB[y];
					ulong b1=arrayB[y+1];
					ulong b2=arrayB[y+2];
					ulong b3=arrayB[y+3];
					ulong b4=arrayB[y+4];
					arrayState[y]=b0^(~b1&b2);
					arrayState[y+1]=b1^(~b2&b3);
					arrayState[y+2]=b2^(~b3&b4);
					arrayState[y+3]=b3^(~b4&b0);
					arrayState[y+4]=b4^(~b0&b1);
				}
				//Iota
				arrayState[0]^=_arrayRoundConstants[round];
			}
		}

		private static ulong RotateLeft(ulong value,int count) {
			if(count==0) {
				return value;
			}
			return (value<<count)|(value>>(64-count));
		}
	}
}
