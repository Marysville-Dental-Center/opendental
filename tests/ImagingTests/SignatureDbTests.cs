// 24.4 v24_4-imaging-signature-permissions against a scratch 25.3.48 database: the permission matrix for the logged-on user, the date rules,
// and the security log entries. Messages are suppressed, so no window opens. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingTests {
	[TestClass]
	public class SignatureDbTests {
		[TestInitialize]
		public void TestInitialize() {
			ImagingDb.Begin();
			ImagingDb.SetStorage(DataStorageType.InDatabase);
		}

		[TestCleanup]
		public void TestCleanup() {
			Security.CurUser=new Userod();
		}

		private static Document Signed(Patient patient,int daysOld=0) {
			return ImagingDb.CreateDocument(patient,"synthetic-signature",DateTime.Now.AddDays(-daysOld));
		}

		private static Document Unsigned(Patient patient,int daysOld=0) {
			return ImagingDb.CreateDocument(patient,"",DateTime.Now.AddDays(-daysOld));
		}

		[TestMethod]
		public void Sign_UnsignedNeedsImageSignatureCreate() {
			Patient patient=ImagingDb.CreatePatient("sign1");
			Document unsigned=Unsigned(patient);
			ImagingDb.LogInWith(EnumPermType.ImageEdit);
			Assert.IsFalse(ImageSignaturePermissions.IsAuthorizedToSign(unsigned,suppressMessage:true),"ImageEdit alone no longer signs.");
			ImagingDb.LogInWith(EnumPermType.SignedImageEdit);
			Assert.IsFalse(ImageSignaturePermissions.IsAuthorizedToSign(unsigned,suppressMessage:true));
			ImagingDb.LogInWith(EnumPermType.ImageSignatureCreate);
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedToSign(unsigned,suppressMessage:true));
		}

		[TestMethod]
		public void Sign_SignedNeedsSignedImageEdit() {
			Patient patient=ImagingDb.CreatePatient("sign2");
			Document signed=Signed(patient);
			ImagingDb.LogInWith(EnumPermType.ImageSignatureCreate,EnumPermType.ImageEdit);
			Assert.IsFalse(ImageSignaturePermissions.IsAuthorizedToSign(signed,suppressMessage:true),"Create does not change a signature.");
			ImagingDb.LogInWith(EnumPermType.SignedImageEdit);
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedToSign(signed,suppressMessage:true));
		}

		[TestMethod]
		public void Edit_SignedNeedsBothImageEditAndSignedImageEdit() {
			Patient patient=ImagingDb.CreatePatient("edit1");
			Document signed=Signed(patient);
			Document unsigned=Unsigned(patient);
			ImagingDb.LogInWith(EnumPermType.ImageEdit);
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedToEdit(unsigned,suppressMessage:true));
			Assert.IsFalse(ImageSignaturePermissions.IsAuthorizedToEdit(signed,suppressMessage:true));
			ImagingDb.LogInWith(EnumPermType.SignedImageEdit);
			Assert.IsFalse(ImageSignaturePermissions.IsAuthorizedToEdit(signed,suppressMessage:true),"SignedImageEdit alone is not ImageEdit.");
			ImagingDb.LogInWith(EnumPermType.ImageEdit,EnumPermType.SignedImageEdit);
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedToEdit(signed,suppressMessage:true));
		}

		[TestMethod]
		public void Delete_SignedNeedsBothImageDeleteAndSignedImageEdit() {
			Patient patient=ImagingDb.CreatePatient("del1");
			Document signed=Signed(patient);
			Document unsigned=Unsigned(patient);
			ImagingDb.LogInWith(EnumPermType.ImageDelete);
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedToDelete(unsigned,suppressMessage:true));
			Assert.IsFalse(ImageSignaturePermissions.IsAuthorizedToDelete(signed,suppressMessage:true));
			ImagingDb.LogInWith(EnumPermType.ImageDelete,EnumPermType.SignedImageEdit);
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedToDelete(signed,suppressMessage:true));
		}

		[TestMethod]
		public void Mount_WithASignedItemNeedsSignedImageEdit() {
			Patient patient=ImagingDb.CreatePatient("mount1");
			Document[] arrayMountUnsigned=new Document[] { Unsigned(patient),null,Unsigned(patient) };
			Document[] arrayMountSigned=new Document[] { Unsigned(patient),null,Signed(patient) };
			ImagingDb.LogInWith(EnumPermType.ImageDelete);
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedForSigned(arrayMountUnsigned,suppressMessage:true));
			Assert.IsFalse(ImageSignaturePermissions.IsAuthorizedForSigned(arrayMountSigned,suppressMessage:true));
			ImagingDb.LogInWith(EnumPermType.ImageDelete,EnumPermType.SignedImageEdit);
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedForSigned(arrayMountSigned,suppressMessage:true));
		}

		///<summary>ImageEdit and ImageDelete keep their date restriction; SignedImageEdit cannot lift it.</summary>
		[TestMethod]
		public void DateRestriction_StaysOnImageEditAndImageDelete() {
			Patient patient=ImagingDb.CreatePatient("date1");
			Document signedOld=Signed(patient,daysOld:30);
			Document signedNew=Signed(patient,daysOld:0);
			ImagingDb.LogInWithDays((EnumPermType.ImageEdit,7),(EnumPermType.ImageDelete,7),(EnumPermType.SignedImageEdit,0));
			Assert.IsFalse(ImageSignaturePermissions.IsAuthorizedToEdit(signedOld,suppressMessage:true));
			Assert.IsFalse(ImageSignaturePermissions.IsAuthorizedToDelete(signedOld,suppressMessage:true));
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedToEdit(signedNew,suppressMessage:true));
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedToDelete(signedNew,suppressMessage:true));
			Assert.IsTrue(ImageSignaturePermissions.IsAuthorizedToSign(signedOld,suppressMessage:true),"Signing has no date restriction.");
		}

		[TestMethod]
		public void SignWindowLog_AddedChangedRemoved() {
			Patient patient=ImagingDb.CreatePatient("log1");
			Document document=Unsigned(patient);
			ImagingDb.LogInWith(EnumPermType.ImageSignatureCreate,EnumPermType.SignedImageEdit);
			Document documentOld=document.Copy();
			document.Signature="synthetic-signature";
			document.Note="Reviewed";
			Documents.Update(document);
			ImageSignaturePermissions.LogSignatureChange(documentOld,document);
			SecurityLog securityLog=ImagingDb.GetLogs(EnumPermType.ImageSignatureCreate,patient.PatNum).Last();
			StringAssert.StartsWith(securityLog.LogText,"Signature added: ");
			Assert.AreEqual(document.DocNum,securityLog.FKey);
			documentOld=document.Copy();
			document.Signature="synthetic-signature-2";
			ImageSignaturePermissions.LogSignatureChange(documentOld,document);
			StringAssert.StartsWith(ImagingDb.GetLogs(EnumPermType.SignedImageEdit,patient.PatNum).Last().LogText,"Signature changed: ");
			documentOld=document.Copy();
			document.Signature="";
			ImageSignaturePermissions.LogSignatureChange(documentOld,document);
			StringAssert.StartsWith(ImagingDb.GetLogs(EnumPermType.SignedImageEdit,patient.PatNum).Last().LogText,"Signature removed: ");
			int countBefore=ImagingDb.GetLogs(EnumPermType.SignedImageEdit,patient.PatNum).Count+ImagingDb.GetLogs(EnumPermType.ImageSignatureCreate,patient.PatNum).Count;
			ImageSignaturePermissions.LogSignatureChange(document.Copy(),document);
			int countAfter=ImagingDb.GetLogs(EnumPermType.SignedImageEdit,patient.PatNum).Count+ImagingDb.GetLogs(EnumPermType.ImageSignatureCreate,patient.PatNum).Count;
			Assert.AreEqual(countBefore,countAfter,"No change, no log.");
		}

		[TestMethod]
		public void SignedImageEditedLog_OnlyForSignedImages() {
			Patient patient=ImagingDb.CreatePatient("log2");
			ImageSignaturePermissions.LogSignedImageEdited(Unsigned(patient),"flipped horizontally");
			Assert.AreEqual(0,ImagingDb.GetLogs(EnumPermType.SignedImageEdit,patient.PatNum).Count);
			Document signed=Signed(patient);
			ImageSignaturePermissions.LogSignedImageEdited(signed,"flipped horizontally");
			SecurityLog securityLog=ImagingDb.GetLogs(EnumPermType.SignedImageEdit,patient.PatNum).Single();
			StringAssert.StartsWith(securityLog.LogText,"Signed image edited (flipped horizontally): ");
		}

		///<summary>ImageStore.DeleteDocuments, used by every delete path, logs a signed image under SignedImageEdit.</summary>
		[TestMethod]
		public void DeleteDocuments_LogsSignedImageDeleted() {
			Patient patient=ImagingDb.CreatePatient("log3");
			Document signed=Signed(patient);
			Document unsigned=Unsigned(patient);
			ImageStore.DeleteDocuments(new List<Document> { signed,unsigned },"");
			Assert.IsNull(Documents.GetByNum(signed.DocNum,doReturnNullIfNotFound:true));
			Assert.IsNull(Documents.GetByNum(unsigned.DocNum,doReturnNullIfNotFound:true));
			SecurityLog securityLog=ImagingDb.GetLogs(EnumPermType.SignedImageEdit,patient.PatNum).Single();
			StringAssert.StartsWith(securityLog.LogText,"Signed image deleted: ");
			Assert.AreEqual(signed.DocNum,securityLog.FKey);
		}

		///<summary>The 24.4 upgrade grant (Seed25_3, owned by M1-02) gives both permissions to ImageEdit holders; on a fresh database the
		///administrator group has them, so nothing an office could do before is lost.</summary>
		[TestMethod]
		public void UpgradedDatabase_ImageEditHoldersCanStillSign() {
			List<long> listGroupsImageEdit=ImagingDb.ListGroupsImageEditAtUpgrade;
			Assert.IsTrue(listGroupsImageEdit.Count>0,"Set up: some group has ImageEdit.");
			foreach(long userGroupNum in listGroupsImageEdit) {
				Assert.IsTrue(GroupPermissions.GetWhere(x => x.UserGroupNum==userGroupNum && x.PermType==EnumPermType.ImageSignatureCreate).Count>0,
					"Group "+userGroupNum+" keeps signing.");
				Assert.IsTrue(GroupPermissions.GetWhere(x => x.UserGroupNum==userGroupNum && x.PermType==EnumPermType.SignedImageEdit).Count>0,
					"Group "+userGroupNum+" keeps editing signed images.");
			}
		}
	}
}
