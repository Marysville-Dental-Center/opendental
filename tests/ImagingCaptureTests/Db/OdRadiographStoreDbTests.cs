// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImagingCapture.Save;
using ImagingCapture.Templates;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests.Db {
	///<summary>OdRadiographStore and RadiographImport on the scratch database (CaptureDb) and its A to Z folder: the document row and file of
	///ImageStore.Import, the placement update, and the undo of a partial save (design 7.12: MyISAM has no transaction, so the saver deletes the row
	///and the file again when a later step fails).</summary>
	[TestClass]
	public class OdRadiographStoreDbTests {
		private OdRadiographStore _store;
		private long _provNum;
		private Patient _patient;
		private Def _category;
		private Mount _mount;
		private List<MountItem> _listSlots;

		[TestInitialize]
		public void Init() {
			CaptureDb.Begin();
			_store=new OdRadiographStore();
			_provNum=CaptureDb.CreateProvider("radio");
			_patient=CaptureDb.CreatePatient("Radio",_provNum);
			_category=CaptureDb.CreateImageCategory("radiograph store");
			MountDef mountDef=CaptureDb.CreateMountDefFromTemplate(StandardTemplateKind.FourBitewingsHorizontal,_category.DefNum,"radio");
			_mount=CaptureDb.CreateMountFromDef(mountDef,_patient,_category.DefNum);
			_listSlots=MountItems.GetItemsForMount(_mount.MountNum);
		}

		[TestMethod]
		public void Store_EachCall_DoesItsDatabaseWork() {
			Assert.IsTrue(_store.IsCategoryValid(_category.DefNum));
			Assert.IsFalse(_store.IsCategoryValid(_category.DefNum+100000));
			byte[] arrayPng=CaptureDb.Png(6,4,120);
			Document document=_store.Import(arrayPng,_category.DefNum,ImageType.Radiograph,_patient,"image/png",".png");
			Assert.AreNotEqual(0,document.DocNum);
			Assert.AreEqual(_patient.PatNum,document.PatNum);
			string path=CaptureDb.GetDocumentPath(document);
			CollectionAssert.AreEqual(arrayPng,File.ReadAllBytes(path),"The bytes as given, in the patient's A to Z folder.");
			Assert.AreEqual(1,CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_patient.PatNum).Count(x => x.FKey==document.DocNum && x.LogText.StartsWith("Radiograph Created: ")),
				CaptureDb.Texts(CaptureDb.GetAllLogsForPatient(_patient.PatNum)));
			//The unmounted item and the placement.
			MountItem mountItem=MountPlacement.CreateUnmountedItem(_mount.MountNum);
			_store.InsertMountItem(mountItem);
			Assert.AreNotEqual(0,mountItem.MountItemNum);
			Assert.AreEqual(MountPlacement.ItemOrderUnmounted,CaptureDb.GetMountItemRows(_mount.MountNum).Single(x => x.MountItemNum==mountItem.MountItemNum).ItemOrder);
			Document documentOld=document.Copy();
			document.MountItemNum=mountItem.MountItemNum;
			document.ToothNumbers="3";
			document.ProvNum=_provNum;
			_store.UpdateDocument(document,documentOld);
			Document documentRead=Documents.GetByNum(document.DocNum);
			Assert.AreEqual(mountItem.MountItemNum,documentRead.MountItemNum);
			Assert.AreEqual("3",documentRead.ToothNumbers);
			Assert.AreEqual(_provNum,documentRead.ProvNum);
			_mount.ProvNum=_provNum;
			_store.UpdateMount(_mount);
			Assert.AreEqual(_provNum,Mounts.GetByNum(_mount.MountNum).ProvNum);
			_store.MakeLogEntry(EnumPermType.ImageCreate,_patient.PatNum,"Capdb radiograph store entry",document.DocNum);
			Assert.AreEqual(1,CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_patient.PatNum).Count(x => x.FKey==document.DocNum && x.LogText=="Capdb radiograph store entry"),
				CaptureDb.Texts(CaptureDb.GetAllLogsForPatient(_patient.PatNum)));
			//No analysis service is set up for the category: nothing is sent, and nothing fails.
			_store.SendToPearlIfConfigured(_patient,document,arrayPng,mountItem);
			//Deleting: the row and the file, with ImageStore's ImageDelete entry (DocumentCrud.Delete then zeroes the FKey of the document's
			//ImageDelete and ImageEdit entries, as for every deleted document); then the unmounted item.
			_store.DeleteDocument(documentRead,_patient);
			Assert.IsNull(Documents.GetByNum(document.DocNum,doReturnNullIfNotFound:true));
			Assert.IsFalse(File.Exists(path));
			Assert.AreEqual(1,CaptureDb.GetLogsForPatient(EnumPermType.ImageDelete,_patient.PatNum)
				.Count(x => x.LogText=="Document Deleted: "+documentRead.FileName+" with category "+_category.ItemName),
				CaptureDb.Texts(CaptureDb.GetAllLogsForPatient(_patient.PatNum)));
			_store.DeleteMountItem(mountItem);
			Assert.IsFalse(CaptureDb.GetMountItemRows(_mount.MountNum).Any(x => x.MountItemNum==mountItem.MountItemNum));
		}

		[TestMethod]
		public void Save_IntoASlot_TheRowFileAndPlacement() {
			MountItem slot4=_listSlots.Single(x => x.ItemOrder==4);
			RadiographSaveResult result=new RadiographImport().Save(Request(new RadiographTarget { Mount=_mount,MountItem=slot4 }));
			Assert.IsTrue(result.IsSaved,result.Error);
			Document document=CaptureDb.GetDocuments(_patient.PatNum).Single();
			Assert.AreEqual(slot4.MountItemNum,document.MountItemNum);
			Assert.AreEqual("12,13,20,21",document.ToothNumbers);
			Assert.AreEqual(180,(int)document.DegreesRotated);
			Assert.AreEqual(_provNum,document.ProvNum);
			Assert.IsTrue(document.PrintHeading,"Radiographs get PrintHeading (design 10.8).");
			Assert.AreEqual(new DateTime(2026,10,31,10,0,5),document.DateCreated,"The receive time (design 10.8), to the second.");
			Assert.IsTrue(File.Exists(CaptureDb.GetDocumentPath(document)));
			Assert.AreEqual(_provNum,Mounts.GetByNum(_mount.MountNum).ProvNum,"Mount.ProvNum too (design 10.8).");
		}

		[TestMethod]
		public void Save_ThePlacementUpdateFails_TheRowAndTheFileAreDeletedAgain_TheNewUnmountedItemToo() {
			//Every slot of the mount is taken, so the saver inserts an unmounted item; then Documents.Update fails.
			foreach(MountItem slot in _listSlots) {
				CaptureDb.CreateDocumentInSlot(_patient,slot,_category.DefNum);
			}
			int countDocuments=CaptureDb.GetDocuments(_patient.PatNum).Count;
			List<string> listFilesBefore=Directory.GetFiles(Path.GetDirectoryName(CaptureDb.GetDocumentPath(CaptureDb.GetDocuments(_patient.PatNum)[0]))).ToList();
			FailingRadiographStore storeFailing=new FailingRadiographStore();
			RadiographSaveResult result=new RadiographImport(storeFailing,() => DateTime.Now).Save(Request(new RadiographTarget { Mount=_mount,MountItem=null }));
			Assert.IsFalse(result.IsSaved);
			StringAssert.Contains(result.Error,"The database is not reachable.");
			Assert.IsTrue(result.IsRowDeleted);
			Assert.AreNotEqual(0,storeFailing.DocNumImported,"Import had written the row and the file.");
			Assert.IsNull(Documents.GetByNum(storeFailing.DocNumImported,doReturnNullIfNotFound:true),"The row is deleted again.");
			Assert.AreEqual(countDocuments,CaptureDb.GetDocuments(_patient.PatNum).Count);
			CollectionAssert.AreEquivalent(listFilesBefore,
				Directory.GetFiles(Path.GetDirectoryName(CaptureDb.GetDocumentPath(CaptureDb.GetDocuments(_patient.PatNum)[0]))).ToList(),"And its file.");
			Assert.AreEqual(_listSlots.Count,CaptureDb.GetMountItemRows(_mount.MountNum).Count,"The new unmounted item is deleted again.");
		}

		private RadiographSaveRequest Request(RadiographTarget target) {
			return new RadiographSaveRequest {
				Image=TestImages.Image8(6,4,(x,y) => (byte)(x*30+y)),
				Patient=_patient,
				DocCategory=_category.DefNum,
				ProvNum=_provNum,
				Target=target,
				SessionGuid=Guid.NewGuid(),
				DeviceDescription="Capdb sensor",
			};
		}

		///<summary>The real store, except that Documents.Update fails (the database went away between the insert and the update).</summary>
		private sealed class FailingRadiographStore:IRadiographStore {
			private readonly OdRadiographStore _store=new OdRadiographStore();
			public long DocNumImported;

			public bool IsCategoryValid(long defNum) {
				return _store.IsCategoryValid(defNum);
			}

			public Document Import(byte[] bytes,long docCategory,ImageType imageType,Patient patient,string mimeType,string fileExtension) {
				Document document=_store.Import(bytes,docCategory,imageType,patient,mimeType,fileExtension);
				DocNumImported=document.DocNum;
				return document;
			}

			public void InsertMountItem(MountItem mountItem) {
				_store.InsertMountItem(mountItem);
			}

			public void UpdateDocument(Document document,Document documentOld) {
				throw new Exception("The database is not reachable.");
			}

			public void DeleteDocument(Document document,Patient patient) {
				_store.DeleteDocument(document,patient);
			}

			public void DeleteMountItem(MountItem mountItem) {
				_store.DeleteMountItem(mountItem);
			}

			public void UpdateMount(Mount mount) {
				_store.UpdateMount(mount);
			}

			public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
				_store.MakeLogEntry(permType,patNum,logText,fKey);
			}

			public void SendToPearlIfConfigured(Patient patient,Document document,byte[] pngBytes,MountItem mountItem) {
				_store.SendToPearlIfConfigured(patient,document,pngBytes,mountItem);
			}
		}
	}
}
