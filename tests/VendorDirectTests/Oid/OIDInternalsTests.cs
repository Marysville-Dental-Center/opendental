// Tests for the local OID root (package OID, docs/specs/vendor-direct/newcrop.md section 3.5): generating a 2.25 root, validating an entered
// root, filling the child rows, and the absence of any HQ call from the OID registry.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.Oid {
	[TestClass]
	public class OIDInternalsTests {
		private static List<OIDInternal> CreateList(string root,params string[] arrayChildren) {
			List<OIDInternal> listOIDInternals=new List<OIDInternal>();
			Array arrayTypes=Enum.GetValues(typeof(IdentifierType));
			for(int i=0;i<arrayTypes.Length;i++) {
				OIDInternal oIDInternal=new OIDInternal();
				oIDInternal.OIDInternalNum=i+1;
				oIDInternal.IDType=(IdentifierType)arrayTypes.GetValue(i);
				if(oIDInternal.IDType==IdentifierType.Root) {
					oIDInternal.IDRoot=root;
				}
				else {
					oIDInternal.IDRoot=(i-1<arrayChildren.Length) ? arrayChildren[i-1] : "";
				}
				listOIDInternals.Add(oIDInternal);
			}
			return listOIDInternals;
		}

		private static string GetVal(List<OIDInternal> listOIDInternals,IdentifierType idType) {
			return listOIDInternals.Single(x => x.IDType==idType).IDRoot;
		}

		[TestMethod]
		public void GenerateUuidRoot_IsUnderTheUuidArcAndUnique() {
			Regex regex=new Regex(@"^2\.25\.[1-9][0-9]{0,38}$");
			HashSet<string> hashSetRoots=new HashSet<string>();
			for(int i=0;i<200;i++) {
				string root=OIDInternals.GenerateUuidRoot();
				Assert.IsTrue(regex.IsMatch(root),root);
				Assert.IsTrue(root.Length<=44,root);
				Assert.IsTrue(OIDInternals.IsValidOid(root),root);
				Assert.IsFalse(OIDInternals.IsLegacyRoot(root),root);
				Assert.IsTrue(hashSetRoots.Add(root),"Two generated roots were the same.");
			}
		}

		[TestMethod]
		public void UuidBytesToDecimal_KnownVectors() {
			//00000000-0000-4000-8000-000000000000: only the version and variant bits.
			Assert.AreEqual("302240678275694148452352",OIDInternals.UuidBytesToDecimal(new byte[16]));
			//ffffffff-ffff-4fff-bfff-ffffffffffff: the largest version 4 UUID.
			byte[] arrayOnes=Enumerable.Repeat((byte)0xFF,16).ToArray();
			Assert.AreEqual("340282366920937632322261936355783213055",OIDInternals.UuidBytesToDecimal(arrayOnes));
			Assert.AreEqual(0xFF,arrayOnes[6],"The input array is not changed.");
			//The X.667 example f81d4fae-7dec-11d0-a765-00a0c91e6bf6 is a version 1 UUID; the version nibble is forced to 4 (41d0).
			byte[] arrayExample={ 0xf8,0x1d,0x4f,0xae,0x7d,0xec,0x11,0xd0,0xa7,0x65,0x00,0xa0,0xc9,0x1e,0x6b,0xf6 };
			Assert.AreEqual("329800735698586855969233156254476430326",OIDInternals.UuidBytesToDecimal(arrayExample));
		}

		[TestMethod]
		public void UuidBytesToDecimal_RejectsWrongLength() {
			AssertEx.Throws<ArgumentException>(() => OIDInternals.UuidBytesToDecimal(new byte[15]));
			AssertEx.Throws<ArgumentException>(() => OIDInternals.UuidBytesToDecimal(null));
		}

		[TestMethod]
		public void IsValidOid_AcceptsWellFormedRoots() {
			Assert.IsTrue(OIDInternals.IsValidOid("1.3.6.1.4.1.99999.1"));
			Assert.IsTrue(OIDInternals.IsValidOid("2.25.329800735698586629295641978511506172918"));
			Assert.IsTrue(OIDInternals.IsValidOid("2.16.840.1.113883.3.4337.1486.12345"));
			Assert.IsTrue(OIDInternals.IsValidOid("0.0"));
			Assert.IsTrue(OIDInternals.IsValidOid("1.2.0.3"));
		}

		[TestMethod]
		public void IsValidOid_RejectsMalformedRoots() {
			foreach(string oid in new[] { null,"","1","3.1","1.3.6.01","1.3.6.1.","1..3",".1.3"," 1.3.6","1.3.6 ","1.3.a","01.3","1.3,6","-1.3" }) {
				Assert.IsFalse(OIDInternals.IsValidOid(oid),oid??"(null)");
			}
			string tooLong="1.3."+new string('1',61);
			Assert.AreEqual(65,tooLong.Length);
			Assert.IsFalse(OIDInternals.IsValidOid(tooLong));
			Assert.IsTrue(OIDInternals.IsValidOid(tooLong.Substring(0,64)));
		}

		[TestMethod]
		public void LegacyOidArc_KeepsTheOldValueAndAlias() {
			Assert.AreEqual("2.16.840.1.113883.3.4337",OIDInternals.LegacyOidArc);
#pragma warning disable CS0618 // The obsolete alias must keep returning the same arc until its callers move.
			Assert.AreEqual(OIDInternals.LegacyOidArc,OIDInternals.OpenDentalOID);
#pragma warning restore CS0618
			Assert.IsTrue(OIDInternals.IsLegacyRoot("2.16.840.1.113883.3.4337.1486.12345"));
			Assert.IsFalse(OIDInternals.IsLegacyRoot("2.16.840.1.113883.3.43371"));
			Assert.IsFalse(OIDInternals.IsLegacyRoot("1.3.6.1.4.1.99999.1"));
			Assert.IsFalse(OIDInternals.IsLegacyRoot(""));
			Assert.IsFalse(OIDInternals.IsLegacyRoot(null));
		}

		[TestMethod]
		public void GetRecommendedValue_UsesTheIdentifierTypeNumber() {
			Assert.AreEqual("1.2.3",OIDInternals.GetRecommendedValue("1.2.3",IdentifierType.Root));
			Assert.AreEqual("1.2.3.1",OIDInternals.GetRecommendedValue("1.2.3",IdentifierType.LabOrder));
			Assert.AreEqual("1.2.3.2",OIDInternals.GetRecommendedValue("1.2.3",IdentifierType.Patient));
			Assert.AreEqual("1.2.3.8",OIDInternals.GetRecommendedValue("1.2.3",IdentifierType.Procedure));
			Assert.AreEqual("",OIDInternals.GetRecommendedValue("",IdentifierType.Patient));
		}

		[TestMethod]
		public void ApplyRootToList_BlankRegistryFillsRootAndChildren() {
			List<OIDInternal> listOIDInternals=CreateList("");
			Assert.IsFalse(OIDInternals.HasCustomChildren(listOIDInternals));
			string root=OIDInternals.GenerateUuidRoot();
			OIDInternals.ApplyRootToList(listOIDInternals,root,overwriteChildren:false);
			Assert.AreEqual(root,GetVal(listOIDInternals,IdentifierType.Root));
			foreach(OIDInternal oIDInternal in listOIDInternals.Where(x => x.IDType!=IdentifierType.Root)) {
				Assert.AreEqual(root+"."+(int)oIDInternal.IDType,oIDInternal.IDRoot);
			}
		}

		[TestMethod]
		public void ApplyRootToList_KeepsCustomChildrenUnlessOverwriting() {
			string rootOld="2.16.840.1.113883.3.4337.1486.12345";
			//LabOrder follows the old root, Patient is custom, Provider is blank.
			List<OIDInternal> listOIDInternals=CreateList(rootOld,rootOld+".1","1.3.6.1.4.1.55555.7","");
			Assert.IsTrue(OIDInternals.HasCustomChildren(listOIDInternals));
			OIDInternals.ApplyRootToList(listOIDInternals,"1.3.6.1.4.1.99999.1",overwriteChildren:false);
			Assert.AreEqual("1.3.6.1.4.1.99999.1",GetVal(listOIDInternals,IdentifierType.Root));
			Assert.AreEqual("1.3.6.1.4.1.99999.1.1",GetVal(listOIDInternals,IdentifierType.LabOrder));
			Assert.AreEqual("1.3.6.1.4.1.55555.7",GetVal(listOIDInternals,IdentifierType.Patient),"A custom child is kept.");
			Assert.AreEqual("1.3.6.1.4.1.99999.1.3",GetVal(listOIDInternals,IdentifierType.Provider));
			//Overwriting replaces the custom child too.
			OIDInternals.ApplyRootToList(listOIDInternals,"1.3.6.1.4.1.99999.2",overwriteChildren:true);
			Assert.AreEqual("1.3.6.1.4.1.99999.2.2",GetVal(listOIDInternals,IdentifierType.Patient));
			Assert.IsFalse(OIDInternals.HasCustomChildren(listOIDInternals));
		}

		[TestMethod]
		public void ApplyRootToList_RejectsAnInvalidRootAndChangesNothing() {
			List<OIDInternal> listOIDInternals=CreateList("1.2.3","1.2.3.1");
			AssertEx.Throws<ODException>(() => OIDInternals.ApplyRootToList(listOIDInternals,"1.2.03",overwriteChildren:true));
			Assert.AreEqual("1.2.3",GetVal(listOIDInternals,IdentifierType.Root));
			Assert.AreEqual("1.2.3.1",GetVal(listOIDInternals,IdentifierType.LabOrder));
		}

		[TestMethod]
		public void OidRegistry_MakesNoHqCallAndNamesNoVendor() {
			string root=FoundationLayoutTests.GetRepositoryRoot();
			string form=File.ReadAllText(Path.Combine(root,@"OpenDental\Forms\FormOIDRegistryInternal.cs"));
			string designer=File.ReadAllText(Path.Combine(root,@"OpenDental\Forms\FormOIDRegistryInternal.Designer.cs"));
			foreach(string text in new[] { form,designer }) {
				StringAssert.DoesNotMatch(text,new Regex("CustomerPatNum|RequestCustomerID|CustomerUpdates|WebServiceMainHQ|OpenDentalOID|Open Dental"));
			}
			StringAssert.Contains(form,"OIDInternals.GenerateUuidRoot()");
			StringAssert.Contains(form,"OIDInternals.IsValidOid(");
			//In the business class, the HQ request lives only in CustomerPatNum, which the DoseSpot root still calls until it moves to a local suffix.
			string business=File.ReadAllText(Path.Combine(root,@"OpenDentBusiness\Data Interface\OIDInternals.cs"));
			Assert.AreEqual(1,Regex.Matches(business,@"CustomerUpdatesProxy\.").Count);
			int indexProxy=business.IndexOf("CustomerUpdatesProxy.");
			int indexCustomerPatNum=business.IndexOf("public static long CustomerPatNum");
			int indexGenerate=business.IndexOf("public static string GenerateUuidRoot");
			Assert.IsTrue(indexCustomerPatNum<indexProxy && indexProxy<indexGenerate,"The HQ call is only inside CustomerPatNum.");
		}
	}
}
