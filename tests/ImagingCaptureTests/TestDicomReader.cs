// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>One element read back by TestDicomReader.</summary>
	internal sealed class TestDicomElement {
		public uint Tag;
		public string Vr;
		///<summary>The length field as encoded (0xFFFFFFFF for undefined).</summary>
		public uint Length;
		public byte[] Value;
		///<summary>A sequence's items; null for any other VR.</summary>
		public List<List<TestDicomElement>> ListItems;

		public ushort Group => (ushort)(Tag>>16);

		///<summary>The value as text, with the trailing pad (space, or NUL for UI) removed.</summary>
		public string Text {
			get {
				string text=Encoding.ASCII.GetString(Value);
				return Vr=="UI" ? text.TrimEnd('\0') : text.TrimEnd(' ');
			}
		}

		///<summary>The values of a multi-valued string, split at '\'.</summary>
		public string[] Values => Text.Split('\\');

		public ushort US {
			get {
				Assert.AreEqual("US",Vr);
				Assert.AreEqual(2,Value.Length);
				return BitConverter.ToUInt16(Value,0);
			}
		}

		public short SS {
			get {
				Assert.AreEqual("SS",Vr);
				Assert.AreEqual(2,Value.Length);
				return BitConverter.ToInt16(Value,0);
			}
		}

		public uint UL {
			get {
				Assert.AreEqual("UL",Vr);
				Assert.AreEqual(4,Value.Length);
				return BitConverter.ToUInt32(Value,0);
			}
		}

		public override string ToString() {
			return TestDicomReader.FormatTag(Tag)+" "+Vr;
		}
	}

	///<summary>A PS3.10 file read back by TestDicomReader.</summary>
	internal sealed class TestDicomFile {
		public byte[] Preamble;
		public List<TestDicomElement> ListMeta=new List<TestDicomElement>();
		public List<TestDicomElement> ListDataset=new List<TestDicomElement>();

		public TestDicomElement Meta(ushort group,ushort element) {
			return Find(ListMeta,group,element);
		}

		///<summary>The data set element, or null when absent.</summary>
		public TestDicomElement Get(ushort group,ushort element) {
			return Find(ListDataset,group,element);
		}

		public static TestDicomElement Find(List<TestDicomElement> listElements,ushort group,ushort element) {
			uint tag=((uint)group<<16) | element;
			return listElements.FirstOrDefault(x => x.Tag==tag);
		}
	}

	///<summary>A small, test-only reader for the subset DicomIntraOralWriter emits: a PS3.10 file, Explicit VR Little Endian for the File
	///Meta Information and the data set, sequences and items with defined or undefined lengths. Written from PS3.5 and PS3.10 like the
	///writer, so the writer's tests do not depend on a DICOM library. It fails the test on anything malformed: a bad preamble or prefix, a
	///VR that is not two capital letters, an odd length, a length past the end, or tags out of ascending order.</summary>
	internal static class TestDicomReader {
		private static readonly string[] _arrayLongVrs={ "OB","OD","OF","OL","OV","OW","SQ","SV","UC","UN","UR","UT","UV" };
		private const uint UndefinedLength=0xFFFFFFFF;

		public static TestDicomFile Read(byte[] bytes) {
			Assert.IsNotNull(bytes);
			Assert.IsTrue(bytes.Length>132,"Shorter than the preamble and prefix.");
			TestDicomFile testDicomFile=new TestDicomFile();
			testDicomFile.Preamble=bytes.Take(128).ToArray();
			Assert.AreEqual("DICM",Encoding.ASCII.GetString(bytes,128,4),"PS3.10 7.1 prefix.");
			int pos=132;
			//File Meta: group 0002 only, Explicit VR LE. The group length comes first and must cover the rest of the group exactly.
			TestDicomElement elementGroupLength=ReadElement(bytes,ref pos,bytes.Length);
			Assert.AreEqual(0x00020000u,elementGroupLength.Tag,"(0002,0000) comes first.");
			int endMeta=pos+(int)elementGroupLength.UL;
			testDicomFile.ListMeta.Add(elementGroupLength);
			while(pos<endMeta) {
				TestDicomElement element=ReadElement(bytes,ref pos,endMeta);
				Assert.AreEqual(0x0002,element.Group,"Only group 0002 in the File Meta Information: "+element);
				testDicomFile.ListMeta.Add(element);
			}
			Assert.AreEqual(endMeta,pos,"The group length covers the File Meta Information exactly.");
			testDicomFile.ListDataset=ReadDataset(bytes,ref pos,bytes.Length);
			Assert.AreEqual(bytes.Length,pos);
			Assert.IsFalse(testDicomFile.ListDataset.Any(x => x.Group==0x0002),"No group 0002 in the data set.");
			CheckOrder(testDicomFile.ListMeta);
			return testDicomFile;
		}

		private static List<TestDicomElement> ReadDataset(byte[] bytes,ref int pos,int end) {
			List<TestDicomElement> listElements=new List<TestDicomElement>();
			while(pos<end) {
				if(BitConverter.ToUInt32(bytes,pos)==0xE00DFFFE) {//item delimitation (FFFE,E00D), for an undefined-length item
					break;
				}
				listElements.Add(ReadElement(bytes,ref pos,end));
			}
			CheckOrder(listElements);
			return listElements;
		}

		private static void CheckOrder(List<TestDicomElement> listElements) {
			for(int i=1;i<listElements.Count;i++) {
				Assert.IsTrue(listElements[i-1].Tag<listElements[i].Tag,"Ascending tags, no repeats (PS3.5 7.1): "+listElements[i-1]+" then "+listElements[i]);
			}
		}

		private static TestDicomElement ReadElement(byte[] bytes,ref int pos,int end) {
			Assert.IsTrue(pos+8<=end,"Element header past the end at "+pos);
			TestDicomElement element=new TestDicomElement();
			ushort group=BitConverter.ToUInt16(bytes,pos);
			ushort number=BitConverter.ToUInt16(bytes,pos+2);
			element.Tag=((uint)group<<16) | number;
			element.Vr=Encoding.ASCII.GetString(bytes,pos+4,2);
			Assert.IsTrue(element.Vr.All(x => x>='A' && x<='Z'),"VR of "+FormatTag(element.Tag)+": "+element.Vr);
			if(_arrayLongVrs.Contains(element.Vr)) {
				Assert.AreEqual(0,BitConverter.ToUInt16(bytes,pos+6),"Reserved bytes are zero: "+element);
				Assert.IsTrue(pos+12<=end);
				element.Length=BitConverter.ToUInt32(bytes,pos+8);
				pos+=12;
			}
			else {
				element.Length=BitConverter.ToUInt16(bytes,pos+6);
				pos+=8;
			}
			if(element.Vr=="SQ") {
				element.ListItems=ReadItems(bytes,ref pos,end,element.Length);
				element.Value=new byte[0];
				return element;
			}
			Assert.AreNotEqual(UndefinedLength,element.Length,"Undefined length is only for sequences here: "+element);
			Assert.AreEqual(0u,element.Length%2,"Even length (PS3.5 7.1.1): "+element);
			Assert.IsTrue(pos+element.Length<=end,"Value past the end: "+element);
			element.Value=new byte[element.Length];
			Array.Copy(bytes,pos,element.Value,0,(int)element.Length);
			pos+=(int)element.Length;
			return element;
		}

		private static List<List<TestDicomElement>> ReadItems(byte[] bytes,ref int pos,int end,uint length) {
			List<List<TestDicomElement>> listItems=new List<List<TestDicomElement>>();
			int endSequence=length==UndefinedLength ? end : pos+(int)length;
			Assert.IsTrue(endSequence<=end,"Sequence past the end.");
			while(pos<endSequence) {
				uint tag=BitConverter.ToUInt32(bytes,pos);
				uint lengthItem=BitConverter.ToUInt32(bytes,pos+4);
				pos+=8;
				if(tag==0xE0DDFFFE) {//sequence delimitation (FFFE,E0DD)
					Assert.AreEqual(UndefinedLength,length);
					return listItems;
				}
				Assert.AreEqual(0xE000FFFEu,tag,"Item tag (FFFE,E000).");
				int endItem=lengthItem==UndefinedLength ? endSequence : pos+(int)lengthItem;
				listItems.Add(ReadDataset(bytes,ref pos,endItem));
				if(lengthItem==UndefinedLength) {
					Assert.AreEqual(0xE00DFFFEu,BitConverter.ToUInt32(bytes,pos),"Item delimitation.");
					pos+=8;
				}
				else {
					Assert.AreEqual(endItem,pos,"The item length covers the item exactly.");
				}
			}
			Assert.AreNotEqual(UndefinedLength,length,"Undefined-length sequence without a delimiter.");
			Assert.AreEqual(endSequence,pos,"The sequence length covers its items exactly.");
			return listItems;
		}

		public static string FormatTag(uint tag) {
			return "("+(tag>>16).ToString("X4",CultureInfo.InvariantCulture)+","+(tag & 0xFFFF).ToString("X4",CultureInfo.InvariantCulture)+")";
		}
	}
}
