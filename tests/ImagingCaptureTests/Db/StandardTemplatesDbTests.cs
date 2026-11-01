// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Templates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests.Db {
	///<summary>"Add standard templates" on the scratch database (CaptureDb): StandardTemplateInstaller with OdStandardTemplateStore reads the mount
	///definitions fresh (a cache refresh, so a definition another computer added is seen), writes the new definitions and their items and a Setup
	///entry for each, and raises DataValid once, which FormOpenDental turns into this computer's cache refresh and the other computers' signal;
	///running it again adds nothing (design 9.4).</summary>
	[TestClass]
	public class StandardTemplatesDbTests {
		private readonly List<InvalidType[]> _listInvalidTypesSignalled=new List<InvalidType[]>();

		[TestInitialize]
		public void Init() {
			CaptureDb.Begin();
			global::OpenPmsDental.DataValid.EventInvalid+=DataValid_EventInvalid;
		}

		[TestCleanup]
		public void Cleanup() {
			global::OpenPmsDental.DataValid.EventInvalid-=DataValid_EventInvalid;
		}

		///<summary>Stands in for FormOpenDental.DataValid_BecameInvalid, which refreshes this computer's caches of those types (RefreshLocalData)
		///and inserts the signals for the other computers.</summary>
		private void DataValid_EventInvalid(object sender,global::OpenPmsDental.ValidEventArgs e) {
			_listInvalidTypesSignalled.Add(e.ITypes);
			Cache.Refresh(e.ITypes);
		}

		///<summary>Removes the standard templates an earlier test of this run added, so that this test sees them added.</summary>
		private static void DeleteStandardTemplates() {
			Cache.Refresh(InvalidType.ToolButsAndMounts);
			foreach(MountDef mountDef in MountDefs.GetDeepCopy()) {
				if(StandardMountTemplates.ListTemplates.Any(x => string.Equals(x.Description,(mountDef.Description??"").Trim(),StringComparison.OrdinalIgnoreCase))) {
					MountDefs.Delete(mountDef.MountDefNum);
				}
			}
			Cache.Refresh(InvalidType.ToolButsAndMounts);
		}

		[TestMethod]
		public void Install_AddsTheFourTemplates_RowsItemsAndSetupEntries_TheSignal_ThenNothingAgain() {
			DeleteStandardTemplates();
			Def category=CaptureDb.CreateImageCategory("templates");
			int countBefore=new OdStandardTemplateStore().GetMountDefs().Count;
			StandardTemplateInstaller installer=new StandardTemplateInstaller(new OdStandardTemplateStore());
			StandardTemplateResult result=installer.Install(category.DefNum,category.ItemName);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(0,result.ListWarnings.Count,string.Join(" ",result.ListWarnings));
			CollectionAssert.AreEqual(StandardMountTemplates.ListTemplates.Select(x => x.Description).ToList(),result.ListAdded.Select(x => x.Description).ToList());
			//This computer's cache is refreshed by FormOpenDental's handler of DataValid (the test's handler stands in for it), which also signals
			//the other computers: the mount definitions list has the new ones now.
			List<MountDef> listMountDefs=MountDefs.GetDeepCopy();
			for(int i=0;i<StandardMountTemplates.ListTemplates.Count;i++) {
				StandardTemplate template=StandardMountTemplates.ListTemplates[i];
				MountDef mountDefAdded=result.ListAdded[i];
				MountDef mountDef=listMountDefs.Single(x => x.MountDefNum==mountDefAdded.MountDefNum);
				Assert.AreEqual(template.Description,mountDef.Description);
				Assert.AreEqual(category.DefNum,mountDef.DefaultCat,"The X-ray category picked.");
				Assert.AreEqual(template.Width,mountDef.Width);
				Assert.AreEqual(template.Height,mountDef.Height);
				Assert.AreEqual("",mountDef.ScaleValue,"No scale: the clinic sets it.");
				Assert.IsFalse(mountDef.FlipOnAcquire);
				Assert.AreEqual(countBefore+i,mountDef.ItemOrder,"At the end of the mount list.");
				List<MountItemDef> listMountItemDefs=MountItemDefs.GetForMountDef(mountDef.MountDefNum);
				List<MountItemDef> listSlots=listMountItemDefs.Where(x => x.ItemOrder>0).ToList();
				CollectionAssert.AreEqual(template.ListSlots.Select(x => x.ToothNumbers).ToList(),listSlots.Select(x => x.ToothNumbers).ToList());
				CollectionAssert.AreEqual(template.ListSlots.Select(x => x.RotateOnAcquire).ToList(),listSlots.Select(x => x.RotateOnAcquire).ToList());
				Assert.AreEqual(template.Text==null ? 0 : 1,listMountItemDefs.Count(x => x.ItemOrder==0),"The text item.");
				List<SecurityLog> listSecurityLogs=CaptureDb.GetLogsByFKey(EnumPermType.Setup,mountDef.MountDefNum,StandardTemplatePlanner.LogPrefix);
				Assert.AreEqual(1,listSecurityLogs.Count,CaptureDb.Texts(CaptureDb.GetLogsByFKey(EnumPermType.Setup,mountDef.MountDefNum)));
				SecurityLog securityLog=listSecurityLogs[0];
				Assert.AreEqual(0,securityLog.PatNum);
				Assert.AreEqual(StandardTemplatePlanner.BuildLogText(template,mountDefAdded,category.ItemName),securityLog.LogText);
			}
			Assert.AreEqual(1,_listInvalidTypesSignalled.Count,"The other computers are told once.");
			CollectionAssert.AreEqual(new[] { InvalidType.ToolButsAndMounts },_listInvalidTypesSignalled[0]);
			//Idempotent by Description.
			StandardTemplateResult resultAgain=installer.Install(category.DefNum,category.ItemName);
			Assert.IsTrue(resultAgain.IsDone,resultAgain.Error);
			Assert.AreEqual(0,resultAgain.ListAdded.Count);
			Assert.AreEqual(StandardMountTemplates.ListTemplates.Count,resultAgain.Plan.ListLeftAlone.Count);
			Assert.AreEqual(1,_listInvalidTypesSignalled.Count,"Nothing added, nothing signalled.");
			foreach(MountDef mountDefAdded in result.ListAdded) {
				Assert.AreEqual(1,CaptureDb.GetLogsByFKey(EnumPermType.Setup,mountDefAdded.MountDefNum,StandardTemplatePlanner.LogPrefix).Count,"No second entry.");
			}
			Assert.AreEqual(countBefore+StandardMountTemplates.ListTemplates.Count,new OdStandardTemplateStore().GetMountDefs().Count);
		}

		[TestMethod]
		public void Install_WithoutSetup_OrWithoutACategory_AddsNothing() {
			DeleteStandardTemplates();
			Def category=CaptureDb.CreateImageCategory("templates refused");
			int countBefore=new OdStandardTemplateStore().GetMountDefs().Count;
			StandardTemplateResult result=new StandardTemplateInstaller(new OdStandardTemplateStore()).Install(category.DefNum+100000,"none");
			Assert.IsFalse(result.IsDone);
			Assert.AreEqual("Choose the X-ray category the templates save into.",result.Error);
			CaptureDb.LogInWith(EnumPermType.ImageCreate);
			result=new StandardTemplateInstaller(new OdStandardTemplateStore()).Install(category.DefNum,category.ItemName);
			Assert.IsFalse(result.IsDone);
			Assert.AreEqual("Not authorized for Setup: no mount template was added.",result.Error);
			Assert.AreEqual(countBefore,new OdStandardTemplateStore().GetMountDefs().Count);
			Assert.AreEqual(0,_listInvalidTypesSignalled.Count);
		}

		[TestMethod]
		public void GetMountDefs_RefreshesTheCache_ADefinitionAddedOnAnotherComputerIsSeen() {
			OdStandardTemplateStore store=new OdStandardTemplateStore();
			int countBefore=store.GetMountDefs().Count;
			//Another computer adds a mount definition: this computer's cache does not have it yet.
			MountDef mountDef=new MountDef { Description="Capdb other computer "+CaptureDb.NewSuffix(),Width=100,Height=100,ScaleValue="" };
			mountDef.MountDefNum=MountDefs.Insert(mountDef);
			Assert.IsFalse(MountDefs.GetDeepCopy().Any(x => x.MountDefNum==mountDef.MountDefNum),"Not in this computer's cache.");
			List<MountDef> listMountDefs=store.GetMountDefs();
			Assert.AreEqual(countBefore+1,listMountDefs.Count);
			Assert.IsTrue(listMountDefs.Any(x => x.MountDefNum==mountDef.MountDefNum),"Read fresh, so the plan never adds a duplicate of it.");
			store.DeleteMountDef(mountDef.MountDefNum);
			Assert.IsFalse(store.GetMountDefs().Any(x => x.MountDefNum==mountDef.MountDefNum));
		}
	}
}
