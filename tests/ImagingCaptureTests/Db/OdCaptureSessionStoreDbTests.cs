// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Session;
using ImagingCapture.Templates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace ImagingCapture.Tests.Db {
	///<summary>OdCaptureSessionStore on the scratch database (CaptureDb): every member does its OpenDentBusiness call against real rows, caches and
	///permissions.</summary>
	[TestClass]
	public class OdCaptureSessionStoreDbTests {
		private OdCaptureSessionStore _store;

		[TestInitialize]
		public void Init() {
			CaptureDb.Begin();
			_store=new OdCaptureSessionStore();
		}

		[TestMethod]
		public void IsAuthorized_FollowsTheUsersGroupPermissions() {
			CaptureDb.LogInWith(EnumPermType.ImageCreate);
			Assert.IsTrue(_store.IsAuthorized(EnumPermType.ImageCreate));
			Assert.IsFalse(_store.IsAuthorized(EnumPermType.Setup),"No message box: the caller shows the refusal.");
			CaptureDb.LogInWith(EnumPermType.Setup);
			Assert.IsFalse(_store.IsAuthorized(EnumPermType.ImageCreate));
			Assert.IsTrue(_store.IsAuthorized(EnumPermType.Setup));
		}

		[TestMethod]
		public void IsCategoryValid_AnImageCategoryOnly() {
			Def category=CaptureDb.CreateImageCategory("session valid");
			Assert.IsTrue(_store.IsCategoryValid(category.DefNum));
			Def defOther=DefT.CreateDefinition(DefCat.AdjTypes,"Capdb adjustment "+CaptureDb.NewSuffix());
			Assert.IsFalse(_store.IsCategoryValid(defOther.DefNum),"A definition of another category is no image category.");
			Assert.IsFalse(_store.IsCategoryValid(0));
			Assert.IsFalse(_store.IsCategoryValid(category.DefNum+100000),"No such definition.");
			//Design 7.12 "Invalid category" is Defs.GetDef returning null: a hidden category still saves, as in the Imaging module.
			Def categoryHidden=CaptureDb.CreateImageCategory("session hidden",isHidden:true);
			Assert.IsTrue(_store.IsCategoryValid(categoryHidden.DefNum));
		}

		[TestMethod]
		public void MountItemsAndTheirDocuments_InItemOrder_DocumentsParallel() {
			long provNum=CaptureDb.CreateProvider("items");
			Patient patient=CaptureDb.CreatePatient("Items",provNum);
			Def category=CaptureDb.CreateImageCategory("items");
			MountDef mountDef=CaptureDb.CreateMountDefFromTemplate(StandardTemplateKind.FourBitewingsHorizontal,category.DefNum,"items");
			Mount mount=CaptureDb.CreateMountFromDef(mountDef,patient,category.DefNum);
			List<MountItem> listMountItems=_store.GetMountItems(mount.MountNum);
			CollectionAssert.AreEqual(new[] { 1,2,3,4 },listMountItems.Select(x => x.ItemOrder).ToList(),"Ordered by ItemOrder (the 4BW slots are inserted in it).");
			CollectionAssert.AreEqual(new[] { "2,3,30,31","4,5,28,29","14,15,18,19","12,13,20,21" },listMountItems.Select(x => x.ToothNumbers).ToList());
			Assert.IsTrue(listMountItems.All(x => x.MountNum==mount.MountNum));
			List<Document> listDocuments=_store.GetDocumentsForMountItems(listMountItems);
			Assert.AreEqual(4,listDocuments.Count);
			Assert.IsTrue(listDocuments.All(x => x==null),"A new mount holds nothing.");
			Document document=CaptureDb.CreateDocumentInSlot(patient,listMountItems[2],category.DefNum);
			listDocuments=_store.GetDocumentsForMountItems(_store.GetMountItems(mount.MountNum));
			Assert.IsNull(listDocuments[0]);
			Assert.IsNull(listDocuments[1]);
			Assert.AreEqual(document.DocNum,listDocuments[2].DocNum,"Parallel to the items: slot 3.");
			Assert.IsNull(listDocuments[3]);
			Assert.AreEqual(0,_store.GetMountItems(mount.MountNum+100000).Count,"No such mount.");
			Assert.AreEqual(0,_store.GetDocumentsForMountItems(new List<MountItem>()).Count);
		}

		[TestMethod]
		public void DefaultProvider_TodaysAppointment_ElseThePrimaryProvider() {
			//Design 10.8: the provider of today's appointment (ProvHyg for a hygiene appointment), else Patient.PriProv.
			long provNumPrimary=CaptureDb.CreateProvider("prim");
			long provNumAppt=CaptureDb.CreateProvider("appt");
			long provNumHyg=CaptureDb.CreateProvider("hyg");
			Patient patient=CaptureDb.CreatePatient("Prov",provNumPrimary);
			Assert.AreEqual(provNumPrimary,_store.GetDefaultProvNum(patient),"No appointment: the primary provider.");
			AppointmentT.CreateAppointment(patient.PatNum,DateTime.Today.AddDays(-1).AddHours(9),0,provNumAppt);
			Assert.AreEqual(provNumPrimary,_store.GetDefaultProvNum(patient),"Yesterday's appointment does not count.");
			Appointment appointment=AppointmentT.CreateAppointment(patient.PatNum,DateTime.Now.AddMinutes(-30),0,provNumAppt);
			Assert.AreEqual(provNumAppt,_store.GetDefaultProvNum(patient),"Today's appointment: its provider.");
			Appointment appointmentOld=appointment.Copy();
			appointment.IsHygiene=true;
			appointment.ProvHyg=provNumHyg;
			Appointments.Update(appointment,appointmentOld);
			Assert.AreEqual(provNumHyg,_store.GetDefaultProvNum(patient),"A hygiene appointment: its hygienist.");
			Assert.AreEqual(Providers.GetAbbr(provNumHyg),_store.GetProviderAbbr(provNumHyg));
			Assert.AreEqual("",_store.GetProviderAbbr(0));
		}

		[TestMethod]
		public void SetupEntries_OnlyTheDevicesSetupEntries_MakeLogEntryWritesTheRowAndItsHash() {
			ImagingDevice imagingDevice=CaptureDb.CreateImagingDevice("setup log");
			ImagingDevice imagingDeviceOther=CaptureDb.CreateImagingDevice("setup log other");
			Userod userod=Security.CurUser;
			Assert.AreEqual(0,_store.GetSetupLogEntries(imagingDevice.ImagingDeviceNum).Count);
			_store.MakeLogEntry(EnumPermType.Setup,0,"Capdb setup entry of the device",imagingDevice.ImagingDeviceNum);
			_store.MakeLogEntry(EnumPermType.Setup,0,"Capdb setup entry of another device",imagingDeviceOther.ImagingDeviceNum);
			_store.MakeLogEntry(EnumPermType.ImageCreate,0,"Capdb ImageCreate entry with the device's number as its FKey",imagingDevice.ImagingDeviceNum);
			List<SecurityLog> listSecurityLogs=_store.GetSetupLogEntries(imagingDevice.ImagingDeviceNum);
			SecurityLog securityLog=listSecurityLogs.Single();
			Assert.AreEqual("Capdb setup entry of the device",securityLog.LogText);
			Assert.AreEqual(EnumPermType.Setup,securityLog.PermType);
			Assert.AreEqual(0,securityLog.PatNum);
			Assert.AreEqual(imagingDevice.ImagingDeviceNum,securityLog.FKey);
			Assert.AreEqual(userod.UserNum,securityLog.UserNum,"The logged-in user.");
			Assert.IsTrue(securityLog.LogDateTime>DateTime.Now.AddMinutes(-5),"The database's time of the entry.");
			Assert.IsTrue(CaptureDb.HasLogHash(securityLog.SecurityLogNum),"SecurityLogs.MakeLogEntry writes the hash row too.");
			Patient patient=CaptureDb.CreatePatient("Log",CaptureDb.CreateProvider("log"));
			_store.MakeLogEntry(EnumPermType.ImageCreate,patient.PatNum,"Capdb entry for a patient",0);
			Assert.AreEqual("Capdb entry for a patient",CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,patient.PatNum).Single().LogText);
		}

		[TestMethod]
		public void MountItemDefs_UpdateMountItemDef_UpdateMountItem_RoundTrip() {
			Def category=CaptureDb.CreateImageCategory("rotate defs");
			MountDef mountDef=CaptureDb.CreateMountDefFromTemplate(StandardTemplateKind.FourBitewingsHorizontal,category.DefNum,"rotate");
			List<MountItemDef> listMountItemDefs=_store.GetMountItemDefs(mountDef.MountDefNum);
			CollectionAssert.AreEqual(new[] { 1,2,3,4 },listMountItemDefs.Select(x => x.ItemOrder).ToList(),"The slots, ordered by ItemOrder.");
			CollectionAssert.AreEqual(new[] { 0,0,180,180 },listMountItemDefs.Select(x => x.RotateOnAcquire).ToList());
			MountItemDef mountItemDef=listMountItemDefs[1];
			mountItemDef.RotateOnAcquire=90;
			_store.UpdateMountItemDef(mountItemDef);
			Assert.AreEqual(90,_store.GetMountItemDefs(mountDef.MountDefNum)[1].RotateOnAcquire,"Read back from the database.");
			Patient patient=CaptureDb.CreatePatient("Rotate",CaptureDb.CreateProvider("rot"));
			Mount mount=CaptureDb.CreateMountFromDef(mountDef,patient,category.DefNum);
			MountItem mountItem=_store.GetMountItems(mount.MountNum).Single(x => x.ItemOrder==2);
			Assert.AreEqual(90,mountItem.RotateOnAcquire,"A mount made from the definition afterwards takes the new rotation.");
			mountItem.RotateOnAcquire=270;
			_store.UpdateMountItem(mountItem);
			Assert.AreEqual(270,_store.GetMountItems(mount.MountNum).Single(x => x.ItemOrder==2).RotateOnAcquire);
			Assert.AreEqual(90,_store.GetMountItemDefs(mountDef.MountDefNum)[1].RotateOnAcquire,"The definition is separate.");
			Assert.AreEqual(0,_store.GetMountItemDefs(mountDef.MountDefNum+100000).Count);
		}
	}
}
