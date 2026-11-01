// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingCapture.Dicom;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>2.25 UIDs (design 7.8; PS3.5 B.2).</summary>
	[TestClass]
	public class DicomUidTests {
		[TestMethod]
		public void FromGuid_KnownValues() {
			//The example of ITU-T X.667 and RFC 4122.
			Assert.AreEqual("2.25.329800735698586629295641978511506172918",DicomUid.FromGuid(new Guid("f81d4fae-7dec-11d0-a765-00a0c91e6bf6")));
			Assert.AreEqual("2.25.0",DicomUid.FromGuid(Guid.Empty));
			Assert.AreEqual("2.25.1",DicomUid.FromGuid(new Guid("00000000-0000-0000-0000-000000000001")));
			Assert.AreEqual("2.25.4294967296",DicomUid.FromGuid(new Guid("00000000-0000-0000-0000-000100000000")));
			Assert.AreEqual("2.25.340282366920938463463374607431768211455",DicomUid.FromGuid(new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff")));
			Assert.AreEqual(44,DicomUid.FromGuid(new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff")).Length);
		}

		[TestMethod]
		public void NewUid_ValidAndUnique() {
			HashSet<string> hashSetUids=new HashSet<string>();
			for(int i=0;i<2000;i++) {
				string uid=DicomUid.NewUid();
				Assert.IsTrue(uid.StartsWith("2.25."));
				Assert.IsTrue(DicomUid.IsValid(uid),uid);
				Assert.IsTrue(hashSetUids.Add(uid));
			}
			Assert.IsTrue(DicomUid.IsValid(DicomUid.ImplementationClassUid));
			Assert.AreEqual("2.25.61294234643478154915668169669400220632",DicomUid.ImplementationClassUid,"Fixed, so every file names the same writer.");
		}

		[TestMethod]
		public void FromName_IsAVersion5UuidAndStable() {
			//RFC 4122 version 5 test vector (DNS namespace, www.example.com).
			Assert.AreEqual(new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"),
				DicomUid.NameBasedGuid(new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8"),"www.example.com"));
			string uid=DicomUid.FromName("mount:db-1:42");
			Assert.AreEqual("2.25.106661106736145043432183371661693252331",uid);
			Assert.AreEqual(uid,DicomUid.FromName("mount:db-1:42"));
			Assert.AreNotEqual(uid,DicomUid.FromName("mount:db-1:43"));
			Assert.IsTrue(DicomUid.IsValid(uid));
		}

		[TestMethod]
		public void IsValid_Ps35Rules() {
			foreach(string uid in new[] { "1.2.840.10008.1.2.1","2.25.0","0","1.0.2" }) {
				Assert.IsTrue(DicomUid.IsValid(uid),uid);
			}
			foreach(string uid in new[] { null,"","1..2",".1","1.","1.02","1.2a","1.2 ","2.25."+new string('1',60),"1,2" }) {
				Assert.IsFalse(DicomUid.IsValid(uid),uid ?? "null");
			}
		}
	}
}
