// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ImagingCapture.Dicom {
	///<summary>DICOM UIDs of the form 2.25.&lt;UUID as an unsigned decimal integer&gt; (PS3.5 B.2, ITU-T X.667), so the fork needs no
	///registered root (design 7.8), and the fixed UIDs the intra-oral writer uses.</summary>
	public static class DicomUid {
		///<summary>Digital Intra-Oral X-Ray Image Storage - For Presentation (PS3.4 B.5).</summary>
		public const string SopClassIntraOralForPresentation="1.2.840.10008.5.1.4.1.1.1.3";
		///<summary>Explicit VR Little Endian (PS3.5 A.2).</summary>
		public const string TransferSyntaxExplicitVrLittleEndian="1.2.840.10008.1.2.1";
		///<summary>A fixed random UUID that names this writer (File Meta Implementation Class UID).</summary>
		private static readonly Guid _guidImplementationClass=new Guid("2e1cd716-cf99-4522-a18f-07053ef653d8");
		///<summary>The fixed random namespace UUID of FromName.</summary>
		private static readonly Guid _guidNameSpace=new Guid("ebf88834-5603-43eb-99f4-a1b5a25be753");

		///<summary>The Implementation Class UID of files this writer makes.</summary>
		public static string ImplementationClassUid => FromGuid(_guidImplementationClass);

		///<summary>A new UID from a random (version 4) UUID.</summary>
		public static string NewUid() {
			return FromGuid(Guid.NewGuid());
		}

		///<summary>"2.25." followed by the UUID read as one 128-bit unsigned integer, most significant digit first as in its canonical text
		///form, in decimal without leading zeros. At most 44 characters.</summary>
		public static string FromGuid(Guid guid) {
			string hex=guid.ToString("N");
			//Four 32-bit words, most significant first; repeated division by 10 gives the decimal digits.
			uint[] arrayWords=new uint[4];
			for(int i=0;i<4;i++) {
				arrayWords[i]=uint.Parse(hex.Substring(i*8,8),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture);
			}
			StringBuilder stringBuilderDigits=new StringBuilder();
			while(arrayWords[0]!=0 || arrayWords[1]!=0 || arrayWords[2]!=0 || arrayWords[3]!=0) {
				ulong remainder=0;
				for(int i=0;i<4;i++) {
					ulong current=(remainder<<32)|arrayWords[i];
					arrayWords[i]=(uint)(current/10);
					remainder=current%10;
				}
				stringBuilderDigits.Insert(0,(char)('0'+(int)remainder));
			}
			if(stringBuilderDigits.Length==0) {
				stringBuilderDigits.Append('0');
			}
			return "2.25."+stringBuilderDigits.ToString();
		}

		///<summary>A UID that is always the same for the same name: 2.25 from a name-based (version 5, SHA-1) UUID in a namespace of this
		///writer's own. For a UID that must stay the same across sessions without being stored, such as one Study Instance UID per mount
		///(design 7.8). The name must be unique worldwide for what it names, for example a database identity plus the mount's key.</summary>
		public static string FromName(string name) {
			return FromGuid(NameBasedGuid(_guidNameSpace,name ?? ""));
		}

		///<summary>An RFC 4122 version 5 UUID: SHA-1 of the namespace UUID (in network byte order) followed by the UTF-8 name.</summary>
		internal static Guid NameBasedGuid(Guid guidNameSpace,string name) {
			byte[] bytesNameSpace=ToNetworkOrder(guidNameSpace);
			byte[] bytesName=Encoding.UTF8.GetBytes(name);
			byte[] bytesInput=new byte[bytesNameSpace.Length+bytesName.Length];
			Buffer.BlockCopy(bytesNameSpace,0,bytesInput,0,bytesNameSpace.Length);
			Buffer.BlockCopy(bytesName,0,bytesInput,bytesNameSpace.Length,bytesName.Length);
			byte[] bytesHash;
			using(SHA1 sha1=SHA1.Create()) {
				bytesHash=sha1.ComputeHash(bytesInput);
			}
			byte[] bytesUuid=new byte[16];
			Array.Copy(bytesHash,bytesUuid,16);
			bytesUuid[6]=(byte)((bytesUuid[6] & 0x0F) | 0x50);//version 5
			bytesUuid[8]=(byte)((bytesUuid[8] & 0x3F) | 0x80);//RFC 4122 variant
			return FromNetworkOrder(bytesUuid);
		}

		///<summary>True for a UID PS3.5 9.1 allows: 1-64 characters, components of digits separated by single periods, and no component
		///with a leading zero unless it is "0".</summary>
		public static bool IsValid(string uid) {
			if(string.IsNullOrEmpty(uid) || uid.Length>64) {
				return false;
			}
			string[] arrayComponents=uid.Split('.');
			foreach(string component in arrayComponents) {
				if(component.Length==0 || (component.Length>1 && component[0]=='0')) {
					return false;
				}
				foreach(char c in component) {
					if(c<'0' || c>'9') {
						return false;
					}
				}
			}
			return true;
		}

		///<summary>The 16 bytes of the UUID as its canonical text reads (.NET stores the first three fields little-endian).</summary>
		private static byte[] ToNetworkOrder(Guid guid) {
			byte[] bytes=guid.ToByteArray();
			SwapFields(bytes);
			return bytes;
		}

		private static Guid FromNetworkOrder(byte[] bytes) {
			byte[] bytesCopy=(byte[])bytes.Clone();
			SwapFields(bytesCopy);
			return new Guid(bytesCopy);
		}

		private static void SwapFields(byte[] bytes) {
			Array.Reverse(bytes,0,4);
			Array.Reverse(bytes,4,2);
			Array.Reverse(bytes,6,2);
		}
	}
}
