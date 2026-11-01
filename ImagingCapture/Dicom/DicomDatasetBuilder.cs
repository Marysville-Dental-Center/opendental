// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ImagingCapture.Dicom {
	///<summary>One data element to encode.</summary>
	internal sealed class DicomElementToWrite {
		public ushort Group;
		public ushort Element;
		///<summary>The two-letter value representation.</summary>
		public string Vr;
		///<summary>The value bytes, already padded to an even length. Null for a sequence.</summary>
		public byte[] Value;
		///<summary>A sequence's items. Null for any other VR.</summary>
		public List<DicomDatasetBuilder> ListItems;

		public uint Tag => ((uint)Group<<16) | Element;
	}

	///<summary>Builds a data set and encodes it in Explicit VR Little Endian (PS3.5 7.1.2), the only transfer syntax the intra-oral writer
	///uses. Elements may be added in any order; ToBytes writes them in ascending tag order (PS3.5 7.1), sequences with explicit lengths and
	///items with explicit lengths (PS3.5 7.5). String values are checked against their VR's character repertoire and length (PS3.5 6.2):
	///a value that breaks them is a programming error and throws ArgumentException, so callers clean text from devices first
	///(DicomText). Only the default character repertoire is used, so the data set needs no Specific Character Set.</summary>
	internal sealed class DicomDatasetBuilder {
		///<summary>VRs encoded with two reserved bytes and a 32-bit length (PS3.5 Table 7.1-1); every other VR has a 16-bit length.</summary>
		private static readonly string[] _arrayLongVrs={ "OB","OD","OF","OL","OV","OW","SQ","SV","UC","UN","UR","UT","UV" };
		private readonly List<DicomElementToWrite> _listElements=new List<DicomElementToWrite>();

		public IEnumerable<DicomElementToWrite> Elements => _listElements;

		///<summary>A string element. values null or empty gives a zero-length value (Type 2 empty). Several values are joined with '\'.</summary>
		public void AddString(ushort group,ushort element,string vr,params string[] values) {
			if(values==null) {
				values=new string[0];
			}
			foreach(string value in values) {
				CheckStringValue(vr,value ?? "");
			}
			string text=string.Join("\\",values.Select(x => x ?? ""));
			byte[] bytes=Encoding.ASCII.GetBytes(text);
			if(vr=="LT" && bytes.Length>10240) {
				throw new ArgumentException("LT value too long for "+FormatTag(group,element));
			}
			if(bytes.Length%2==1) {
				byte pad=vr=="UI" ? (byte)0 : (byte)' ';
				Array.Resize(ref bytes,bytes.Length+1);
				bytes[bytes.Length-1]=pad;
			}
			Add(group,element,vr,bytes);
		}

		public void AddUS(ushort group,ushort element,ushort value) {
			Add(group,element,"US",BitConverter.GetBytes(value));
		}

		public void AddSS(ushort group,ushort element,short value) {
			Add(group,element,"SS",BitConverter.GetBytes(value));
		}

		public void AddUL(ushort group,ushort element,uint value) {
			Add(group,element,"UL",BitConverter.GetBytes(value));
		}

		public void AddOB(ushort group,ushort element,byte[] value) {
			byte[] bytes=(byte[])value.Clone();
			if(bytes.Length%2==1) {
				Array.Resize(ref bytes,bytes.Length+1);
			}
			Add(group,element,"OB",bytes);
		}

		///<summary>16-bit words, little-endian.</summary>
		public void AddOW(ushort group,ushort element,ushort[] value) {
			byte[] bytes=new byte[value.Length*2];
			for(int i=0;i<value.Length;i++) {
				bytes[i*2]=(byte)(value[i] & 0xFF);
				bytes[i*2+1]=(byte)(value[i]>>8);
			}
			Add(group,element,"OW",bytes);
		}

		///<summary>A sequence of the given items. An empty list gives a zero-length sequence (Type 2 empty).</summary>
		public void AddSequence(ushort group,ushort element,List<DicomDatasetBuilder> listItems) {
			RemoveExisting(group,element);
			_listElements.Add(new DicomElementToWrite { Group=group,Element=element,Vr="SQ",ListItems=listItems ?? new List<DicomDatasetBuilder>() });
		}

		///<summary>The encoded data set, without preamble or File Meta Information.</summary>
		public byte[] ToBytes() {
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(BinaryWriter binaryWriter=new BinaryWriter(memoryStream)) {
					Write(binaryWriter);
				}
				return memoryStream.ToArray();
			}
		}

		private void Write(BinaryWriter binaryWriter) {
			foreach(DicomElementToWrite elementToWrite in _listElements.OrderBy(x => x.Tag)) {
				binaryWriter.Write(elementToWrite.Group);
				binaryWriter.Write(elementToWrite.Element);
				binaryWriter.Write(Encoding.ASCII.GetBytes(elementToWrite.Vr));
				byte[] bytesValue=elementToWrite.Value;
				if(elementToWrite.Vr=="SQ") {
					bytesValue=EncodeItems(elementToWrite.ListItems);
				}
				if(_arrayLongVrs.Contains(elementToWrite.Vr)) {
					binaryWriter.Write((ushort)0);
					binaryWriter.Write((uint)bytesValue.Length);
				}
				else {
					if(bytesValue.Length>0xFFFE) {
						throw new ArgumentException("Value too long for a 16-bit length: "+FormatTag(elementToWrite.Group,elementToWrite.Element));
					}
					binaryWriter.Write((ushort)bytesValue.Length);
				}
				binaryWriter.Write(bytesValue);
			}
		}

		///<summary>Each item as (FFFE,E000), its explicit length, then its elements.</summary>
		private static byte[] EncodeItems(List<DicomDatasetBuilder> listItems) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(BinaryWriter binaryWriter=new BinaryWriter(memoryStream)) {
					foreach(DicomDatasetBuilder item in listItems) {
						byte[] bytesItem=item.ToBytes();
						binaryWriter.Write((ushort)0xFFFE);
						binaryWriter.Write((ushort)0xE000);
						binaryWriter.Write((uint)bytesItem.Length);
						binaryWriter.Write(bytesItem);
					}
				}
				return memoryStream.ToArray();
			}
		}

		private void Add(ushort group,ushort element,string vr,byte[] bytes) {
			RemoveExisting(group,element);
			_listElements.Add(new DicomElementToWrite { Group=group,Element=element,Vr=vr,Value=bytes });
		}

		private void RemoveExisting(ushort group,ushort element) {
			_listElements.RemoveAll(x => x.Group==group && x.Element==element);
		}

		///<summary>PS3.5 Table 6.2-1 for the string VRs the writer uses: repertoire and maximum length of one value.</summary>
		private static void CheckStringValue(string vr,string value) {
			int maxLength;
			Func<char,bool> funcIsAllowed;
			switch(vr) {
				case "AE": maxLength=16; funcIsAllowed=IsDefaultRepertoire; break;
				case "CS": maxLength=16; funcIsAllowed=c => (c>='A' && c<='Z') || (c>='0' && c<='9') || c==' ' || c=='_'; break;
				case "DA": maxLength=8; funcIsAllowed=c => c>='0' && c<='9'; break;
				case "DS": maxLength=16; funcIsAllowed=c => (c>='0' && c<='9') || c=='+' || c=='-' || c=='.' || c=='E' || c=='e' || c==' '; break;
				case "DT": maxLength=26; funcIsAllowed=c => (c>='0' && c<='9') || c=='+' || c=='-' || c=='.' || c==' '; break;
				case "IS": maxLength=12; funcIsAllowed=c => (c>='0' && c<='9') || c=='+' || c=='-' || c==' '; break;
				case "LO": maxLength=64; funcIsAllowed=IsDefaultRepertoire; break;
				case "LT": maxLength=10240; funcIsAllowed=c => (c>=' ' && c<='~') || c=='\r' || c=='\n' || c=='\t' || c=='\f'; break;
				case "PN": maxLength=64; funcIsAllowed=c => IsDefaultRepertoire(c) || c=='^' || c=='='; break;
				case "SH": maxLength=16; funcIsAllowed=IsDefaultRepertoire; break;
				case "TM": maxLength=14; funcIsAllowed=c => (c>='0' && c<='9') || c=='.' || c==' '; break;
				case "UI": maxLength=64; funcIsAllowed=c => (c>='0' && c<='9') || c=='.'; break;
				default: throw new ArgumentException("Not a string VR the writer uses: "+vr);
			}
			if(value.Length>maxLength) {
				throw new ArgumentException(vr+" value longer than "+maxLength.ToString(CultureInfo.InvariantCulture)+": "+value);
			}
			if(vr!="LT" && value.IndexOf('\\')>=0) {
				throw new ArgumentException(vr+" value with a backslash: "+value);
			}
			foreach(char c in value) {
				if(c!='\\' && !funcIsAllowed(c)) {
					throw new ArgumentException(vr+" value with a character outside its repertoire: "+value);
				}
			}
		}

		///<summary>The printable characters of the default repertoire (ISO-IR 6), apart from the backslash, which separates values.</summary>
		private static bool IsDefaultRepertoire(char c) {
			return c>=' ' && c<='~' && c!='\\';
		}

		internal static string FormatTag(ushort group,ushort element) {
			return "("+group.ToString("X4",CultureInfo.InvariantCulture)+","+element.ToString("X4",CultureInfo.InvariantCulture)+")";
		}
	}

	///<summary>Cleans text that comes from a device or from metadata for a DICOM string value: the default repertoire only (other
	///characters become '?'), no backslash (it separates values; it becomes '/'), no leading or trailing spaces, at most maxLength
	///characters.</summary>
	internal static class DicomText {
		public static string Clean(string text,int maxLength) {
			if(string.IsNullOrEmpty(text)) {
				return "";
			}
			StringBuilder stringBuilder=new StringBuilder(text.Length);
			foreach(char c in text) {
				if(c=='\\') {
					stringBuilder.Append('/');
				}
				else if(c>=' ' && c<='~') {
					stringBuilder.Append(c);
				}
				else if(char.IsLowSurrogate(c)) {
					//The high surrogate already gave one '?' for the character.
				}
				else {
					stringBuilder.Append('?');
				}
			}
			string cleaned=stringBuilder.ToString().Trim();
			if(cleaned.Length>maxLength) {
				cleaned=cleaned.Substring(0,maxLength).TrimEnd();
			}
			return cleaned;
		}

		///<summary>A decimal string (DS) of at most 16 characters.</summary>
		public static string FormatDecimal(double value) {
			string text=value.ToString("0.##########",CultureInfo.InvariantCulture);
			if(text.Length>16) {
				text=value.ToString("G9",CultureInfo.InvariantCulture);
			}
			return text;
		}

		///<summary>An integer string (IS).</summary>
		public static string FormatInteger(long value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		public static string FormatDate(DateTime dateTime) {
			return dateTime.ToString("yyyyMMdd",CultureInfo.InvariantCulture);
		}

		///<summary>HHMMSS.FFF.</summary>
		public static string FormatTime(DateTime dateTime) {
			return dateTime.ToString("HHmmss.fff",CultureInfo.InvariantCulture);
		}

		///<summary>YYYYMMDDHHMMSS.FFF&amp;ZZXX: the clock time with its UTC offset (PS3.5 Table 6.2-1, DT).</summary>
		public static string FormatDateTime(DateTimeOffset dateTimeOffset) {
			TimeSpan offset=dateTimeOffset.Offset;
			string sign=offset<TimeSpan.Zero ? "-" : "+";
			offset=offset.Duration();
			return dateTimeOffset.ToString("yyyyMMddHHmmss.fff",CultureInfo.InvariantCulture)+sign
				+offset.Hours.ToString("00",CultureInfo.InvariantCulture)+offset.Minutes.ToString("00",CultureInfo.InvariantCulture);
		}
	}
}
