// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ImagingCapture.Recovery;
using ImagingCapture.Save;
using ImagingCapture.Settings;
using ImagingCapture.Templates;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests.Db {
	///<summary>The unassigned captures window's records on the scratch database (CaptureDb): OdCaptureRecoveryStore itself, and Assign, Reassign
	///and Discard through CaptureRecoveryFiler with the real stores (OdCaptureRecoveryStore, OdRadiographStore) and the real CaptureFileStore on a
	///temporary Imaging folder, with quarantine files written the way the library writes them (RecoveryFiles). Each test has its own patients,
	///4BW mount and files.</summary>
	[TestClass]
	public class CaptureRecoveryDbTests {
		private static readonly DateTime _receivedAt=new DateTime(2026,10,31,10,0,5);
		private RecoveryFiles _files;
		private OdCaptureRecoveryStore _store;
		private List<string> _listLog;
		private long _provNum;
		private Patient _patientFrom;
		private Patient _patientOther;
		private Def _category;
		private ImagingDevice _imagingDevice;
		private Mount _mount;
		private List<MountItem> _listSlots;

		[TestInitialize]
		public void Init() {
			CaptureDb.Begin();
			_files=new RecoveryFiles();
			_store=new OdCaptureRecoveryStore();
			_listLog=new List<string>();
			_provNum=CaptureDb.CreateProvider("recov");
			_patientFrom=CaptureDb.CreatePatient("From",_provNum);
			_patientOther=CaptureDb.CreatePatient("Other",_provNum);
			_category=CaptureDb.CreateImageCategory("recovery X-rays");
			_imagingDevice=CaptureDb.CreateImagingDevice("recovery");
			MountDef mountDef=CaptureDb.CreateMountDefFromTemplate(StandardTemplateKind.FourBitewingsHorizontal,_category.DefNum,"recovery");
			_mount=CaptureDb.CreateMountFromDef(mountDef,_patientFrom,_category.DefNum);
			_listSlots=MountItems.GetItemsForMount(_mount.MountNum);
		}

		[TestCleanup]
		public void Cleanup() {
			_files?.Dispose();
		}

		private CaptureRecoveryFiler Filer() {
			return new CaptureRecoveryFiler(new CaptureRecoveryServices {
				Store=_store,
				RadiographStore=new OdRadiographStore(),
				FileStore=_files.Store,
				FuncNow=() => DateTime.Now,
				FuncLoadConfig=() => LocalCaptureConfig.Load((string)null),
				FuncGetComputerName=() => CaptureDb.ComputerName,
				ActionWriteLog=line => _listLog.Add(line),
			});
		}

		///<summary>The sidecar of a session capture of _patientFrom for slot itemOrder of the 4BW mount, as the session writes it.</summary>
		private Dictionary<string,string> Fields(int itemOrder,string captureId) {
			MountItem slot=_listSlots.Single(x => x.ItemOrder==itemOrder);
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields(_patientFrom.PatNum,captureId,"2026-10-31T10:00:04.500+00:00");
			dictFields["Patient"]=_patientFrom.GetNameLF();
			dictFields["Source"]=CaptureDb.TwainName;
			dictFields["Computer"]=CaptureDb.ComputerName;
			dictFields["Slot"]=MountPlacement.GetSlotLabel(slot);
			dictFields["Target"]=MountPlacement.GetSlotLabel(slot);
			dictFields[CaptureSidecarKeys.MountNum]=ToText(_mount.MountNum);
			dictFields[CaptureSidecarKeys.MountItemNum]=ToText(slot.MountItemNum);
			dictFields[CaptureSidecarKeys.SlotItemOrder]=ToText(slot.ItemOrder);
			dictFields[CaptureSidecarKeys.ToothNumbers]=slot.ToothNumbers;
			dictFields[CaptureSidecarKeys.DocCategory]=ToText(_category.DefNum);
			dictFields[CaptureSidecarKeys.ProvNum]=ToText(_provNum);
			dictFields[CaptureSidecarKeys.ImagingDeviceNum]=ToText(_imagingDevice.ImagingDeviceNum);
			return dictFields;
		}

		private static string ToText(long value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}

		#region OdCaptureRecoveryStore
		[TestMethod]
		public void Store_PatientsMountsDocumentsAndCategories_FromTheDatabase() {
			Assert.AreEqual(_patientFrom.PatNum,_store.GetPatient(_patientFrom.PatNum).PatNum);
			Assert.IsNull(_store.GetPatient(0));
			Assert.IsNull(_store.GetPatient(_patientOther.PatNum+100000),"No such patient.");
			Mount mount=_store.GetMount(_mount.MountNum);
			Assert.AreEqual(_patientFrom.PatNum,mount.PatNum);
			Assert.AreEqual(_mount.Description,mount.Description);
			Assert.IsNull(_store.GetMount(_mount.MountNum+100000),"Mounts.GetByNum gives a new Mount when there is none: the store gives null.");
			Assert.IsNull(_store.GetMount(0));
			CollectionAssert.AreEqual(new[] { 1,2,3,4 },_store.GetMountItems(_mount.MountNum).Select(x => x.ItemOrder).ToList());
			Assert.AreEqual(0,_store.GetPatientDocuments(_patientFrom.PatNum).Count);
			Document document=CaptureDb.CreateDocumentInSlot(_patientFrom,_listSlots[1],_category.DefNum);
			Assert.AreEqual(document.DocNum,_store.GetPatientDocuments(_patientFrom.PatNum).Single().DocNum);
			Assert.AreEqual(0,_store.GetPatientDocuments(_patientOther.PatNum).Count);
			Assert.AreEqual(document.DocNum,_store.GetDocumentsForMountItems(_store.GetMountItems(_mount.MountNum))[1].DocNum);
			//The ImageCreate entries of the patient by DocNum: ImageStore.Import wrote one for the new document.
			List<SecurityLog> listSecurityLogs=_store.GetImageCreateLogEntries(_patientFrom.PatNum,new List<long> { document.DocNum });
			Assert.IsTrue(listSecurityLogs.Count>=1);
			Assert.IsTrue(listSecurityLogs.All(x => x.FKey==document.DocNum && x.PatNum==_patientFrom.PatNum && x.PermType==EnumPermType.ImageCreate));
			Assert.AreEqual(0,_store.GetImageCreateLogEntries(_patientOther.PatNum,new List<long> { document.DocNum }).Count,"Another patient's.");
			Assert.AreEqual(0,_store.GetImageCreateLogEntries(_patientFrom.PatNum,new List<long>()).Count,"No DocNums: nothing, not every entry.");
			//Categories: a hidden one is not offered for filing.
			Def categoryHidden=CaptureDb.CreateImageCategory("recovery hidden",isHidden:true);
			Assert.IsTrue(_store.IsCategoryValid(_category.DefNum));
			Assert.IsFalse(_store.IsCategoryValid(categoryHidden.DefNum));
			Assert.IsFalse(_store.IsCategoryValid(0));
			List<Def> listCategories=_store.GetImageCategories();
			Assert.IsTrue(listCategories.Any(x => x.DefNum==_category.DefNum));
			Assert.IsFalse(listCategories.Any(x => x.DefNum==categoryHidden.DefNum));
			Assert.AreEqual(_provNum,_store.GetDefaultProvNum(_patientFrom));
			Assert.AreEqual(_imagingDevice.Description,_store.GetImagingDevice(_imagingDevice.ImagingDeviceNum).Description);
			Assert.IsNull(_store.GetImagingDevice(_imagingDevice.ImagingDeviceNum+100000));
			Assert.AreEqual(Security.CurUser.UserName,_store.GetUserName());
		}

		[TestMethod]
		public void Store_IsAuthorized_DateRestriction_GivesTheRefusal() {
			CaptureDb.LogInWithDays(Tuple.Create(EnumPermType.ImageEdit,1));
			string message;
			Assert.IsTrue(_store.IsAuthorized(EnumPermType.ImageEdit,DateTime.Today,out message),message);
			Assert.AreEqual("",message);
			Assert.IsFalse(_store.IsAuthorized(EnumPermType.ImageEdit,DateTime.Today.AddDays(-10),out message),"Older than the group's one day.");
			Assert.AreNotEqual("",message,"The refusal, for the window.");
			Assert.IsFalse(_store.IsAuthorized(EnumPermType.ImageDelete,DateTime.MinValue,out message));
			StringAssert.Contains(message,GroupPermissions.GetDesc(EnumPermType.ImageDelete));
		}
		#endregion OdCaptureRecoveryStore

		#region Assign
		[TestMethod]
		public void Assign_IntoTheEmptyOriginalSlot_DocumentRowFileAndEntries_TheFileMovesToResolved() {
			CaptureFileEntry entry=_files.Quarantine(Fields(3,"capdb-assign-"+CaptureDb.NewSuffix()),_receivedAt);
			CaptureFilingResult result=Filer().Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(0,result.ListWarnings.Count,string.Join(" ",result.ListWarnings));
			MountItem slot3=_listSlots.Single(x => x.ItemOrder==3);
			Document document=CaptureDb.GetDocuments(_patientFrom.PatNum).Single();
			Assert.AreEqual(result.Document.DocNum,document.DocNum);
			Assert.AreEqual(slot3.MountItemNum,document.MountItemNum,"Into its original slot.");
			Assert.AreEqual("14,15,18,19",document.ToothNumbers);
			Assert.AreEqual(180,(int)document.DegreesRotated,"The slot's rotation.");
			Assert.AreEqual(_category.DefNum,document.DocCategory);
			Assert.AreEqual(_provNum,document.ProvNum);
			Assert.AreEqual("",document.Description);
			Assert.AreEqual(ImageType.Radiograph,document.ImgType);
			StringAssert.EndsWith(document.FileName,".png");
			Assert.IsTrue(File.Exists(CaptureDb.GetDocumentPath(document)),"The file is in the patient's A to Z folder.");
			Assert.AreEqual(0,CaptureDb.GetDocuments(_patientOther.PatNum).Count,"Never another patient.");
			Assert.AreEqual(4,CaptureDb.GetMountItemRows(_mount.MountNum).Count,"No unmounted item.");
			//ImageCreate: the document (ImageStore), the capture details (FKey=DocNum) and the Assign with the session GUID.
			List<SecurityLog> listSecurityLogs=CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_patientFrom.PatNum).Where(x => x.FKey==document.DocNum).ToList();
			SecurityLog securityLogAssign=listSecurityLogs.Single(x => x.LogText.StartsWith(CaptureRecoveryText.AssignLogPrefix));
			StringAssert.Contains(securityLogAssign.LogText,RecoveryFiles.SessionGuid.ToString("D"));
			StringAssert.Contains(securityLogAssign.LogText,"as DocNum "+document.DocNum);
			Assert.AreEqual(Security.CurUser.UserNum,securityLogAssign.UserNum);
			Assert.IsTrue(listSecurityLogs.Any(x => x.LogText.StartsWith("Radiograph Created: ")),"ImageStore's own entry.");
			//The file: out of the quarantine, with its disposition.
			Assert.AreEqual(0,_files.CountQuarantineFiles());
			Assert.IsTrue(File.Exists(result.PathResolved));
			string sidecar=File.ReadAllText(Path.ChangeExtension(result.PathResolved,".txt"));
			StringAssert.Contains(sidecar,"DispositionDocNum="+document.DocNum);
			StringAssert.Contains(sidecar,"DispositionPatNum="+_patientFrom.PatNum);
			StringAssert.Contains(sidecar,"DispositionUser="+Security.CurUser.UserName);
		}

		[TestMethod]
		public void Assign_TheOriginalSlotHoldsADocument_ARecoveredUnmountedItemRowOfTheMount() {
			MountItem slot2=_listSlots.Single(x => x.ItemOrder==2);
			Document documentInSlot=CaptureDb.CreateDocumentInSlot(_patientFrom,slot2,_category.DefNum);
			CaptureFileEntry entry=_files.Quarantine(Fields(2,"capdb-taken-"+CaptureDb.NewSuffix()),_receivedAt);
			CaptureFilingResult result=Filer().Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.IsTrue(result.IsMountItemCreated);
			List<MountItem> listMountItems=CaptureDb.GetMountItemRows(_mount.MountNum);
			MountItem mountItemNew=listMountItems.Single(x => x.ItemOrder==MountPlacement.ItemOrderUnmounted);
			Document document=Documents.GetByNum(result.Document.DocNum);
			Assert.AreEqual(mountItemNew.MountItemNum,document.MountItemNum,"An unmounted item of the original mount.");
			Assert.AreEqual("Recovered: "+MountPlacement.GetSlotLabel(slot2),document.Description);
			Assert.AreEqual("4,5,28,29",document.ToothNumbers,"The taken slot's teeth.");
			Assert.AreEqual(slot2.MountItemNum,Documents.GetByNum(documentInSlot.DocNum).MountItemNum,"The slot's image is untouched.");
		}

		[TestMethod]
		public void Assign_TheOriginatingPatientWasMergedIntoAnother_Refused_ReassignFilesItForTheSurvivingPatient() {
			//The file waits in the quarantine (the 30-day warning); meanwhile the office merges the duplicate chart _patientFrom into _patientOther.
			//The merge archives _patientFrom and moves its mount to _patientOther. Assign must not file into the archived chart.
			CaptureFileEntry entry=_files.Quarantine(Fields(3,"capdb-merged-"+CaptureDb.NewSuffix()),_receivedAt);
			Assert.IsTrue(Patients.MergeTwoPatients(_patientOther.PatNum,_patientFrom.PatNum));
			Assert.AreEqual(PatientStatus.Archived,CaptureDb.GetPatient(_patientFrom.PatNum).PatStatus,"Patients.MergeTwoPatients archives the chart.");
			Assert.AreEqual(_patientOther.PatNum,Mounts.GetByNum(_mount.MountNum).PatNum,"It moves the mount.");
			Assert.AreEqual(_patientOther.PatNum,_store.GetMergedIntoPatNum(_patientFrom.PatNum));
			Assert.AreEqual(0,_store.GetMergedIntoPatNum(_patientOther.PatNum),"The surviving patient was not merged.");
			string error;
			Assert.IsNull(Filer().PlanAssign(entry,0,out error),"Without the check: an unmounted document in the archived chart.");
			string errorExpected="The originating patient (PatNum "+_patientFrom.PatNum+") was merged into PatNum "+_patientOther.PatNum
				+". Use Reassign to file the image for that patient.";
			Assert.AreEqual(errorExpected,error);
			CaptureFilingResult result=Filer().Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			Assert.AreEqual(errorExpected,result.Error);
			Assert.AreEqual(0,CaptureDb.GetDocuments(_patientFrom.PatNum).Count,"Nothing in the archived chart.");
			Assert.AreEqual(0,CaptureDb.GetDocuments(_patientOther.PatNum).Count);
			Assert.AreEqual(1,_files.CountQuarantineFiles(),"The file stays for Reassign.");
			CaptureFilingResult resultReassign=Filer().Reassign(entry,CaptureDb.GetPatient(_patientOther.PatNum),"The chart was merged.",_category.DefNum);
			Assert.IsTrue(resultReassign.IsDone,resultReassign.Error);
			Assert.AreEqual(_patientOther.PatNum,CaptureDb.GetDocuments(_patientOther.PatNum).Single().PatNum);
			Assert.AreEqual(0,CaptureDb.GetDocuments(_patientFrom.PatNum).Count);
		}

		[TestMethod]
		public void Store_GetMergedIntoPatNum_FollowsALaterMergeOfTheSurvivingPatient() {
			Patient patientThird=CaptureDb.CreatePatient("Third",_provNum);
			Assert.AreEqual(0,_store.GetMergedIntoPatNum(_patientFrom.PatNum));
			Assert.AreEqual(0,_store.GetMergedIntoPatNum(0));
			Assert.IsTrue(Patients.MergeTwoPatients(_patientOther.PatNum,_patientFrom.PatNum));
			Assert.IsTrue(Patients.MergeTwoPatients(patientThird.PatNum,_patientOther.PatNum));
			Assert.AreEqual(patientThird.PatNum,_store.GetMergedIntoPatNum(_patientFrom.PatNum),"The chart that holds the records now.");
			Assert.AreEqual(patientThird.PatNum,_store.GetMergedIntoPatNum(_patientOther.PatNum));
			Assert.AreEqual(0,_store.GetMergedIntoPatNum(patientThird.PatNum));
		}

		[TestMethod]
		public void Assign_TheOriginatingPatientWasDeleted_Refused() {
			CaptureFileEntry entry=_files.Quarantine(Fields(1,"capdb-deleted-"+CaptureDb.NewSuffix()),_receivedAt);
			Patient patientOld=CaptureDb.GetPatient(_patientFrom.PatNum);
			Patient patientDeleted=patientOld.Copy();
			patientDeleted.PatStatus=PatientStatus.Deleted;
			Patients.Update(patientDeleted,patientOld);
			CaptureFilingResult result=Filer().Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			Assert.AreEqual("The originating patient (PatNum "+_patientFrom.PatNum+") was deleted. Use Reassign.",result.Error);
			Assert.AreEqual(0,CaptureDb.GetDocuments(_patientFrom.PatNum).Count);
			Assert.AreEqual(1,_files.CountQuarantineFiles());
		}

		[TestMethod]
		public void Assign_TheOriginatingPatientIsArchivedButNotMerged_Filed_TheConfirmationSaysArchived() {
			//Offices archive inactive patients, and a session can be bound to one (ArchivedPatientSelect): only a merge or a deletion refuses Assign.
			CaptureFileEntry entry=_files.Quarantine(Fields(2,"capdb-archived-"+CaptureDb.NewSuffix()),_receivedAt);
			Patient patientOld=CaptureDb.GetPatient(_patientFrom.PatNum);
			Patient patientArchived=patientOld.Copy();
			patientArchived.PatStatus=PatientStatus.Archived;
			Patients.Update(patientArchived,patientOld);
			string error;
			CaptureAssignPlan plan=Filer().PlanAssign(entry,0,out error);
			Assert.IsNotNull(plan,error);
			StringAssert.EndsWith(CaptureRecoveryText.GetPatientLine(plan.Patient),"(PatNum "+_patientFrom.PatNum+", Archived)",
				"The confirmation does not look like the active chart's.");
			CaptureFilingResult result=Filer().Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			Assert.AreEqual(_listSlots.Single(x => x.ItemOrder==2).MountItemNum,CaptureDb.GetDocuments(_patientFrom.PatNum).Single().MountItemNum,
				"Into its original slot.");
		}

		[TestMethod]
		public void Assign_TheExposureIsAlreadyADocument_ByItsCaptureId_NotFiledAgain() {
			//Design 10.6: one document per TWAIN transfer. The first file is assigned; a second file of the same exposure (its copy) is refused,
			//found through the patient's documents and their ImageCreate entries in the database.
			string captureId="capdb-dup-"+CaptureDb.NewSuffix();
			CaptureFileEntry entry=_files.Quarantine(Fields(1,captureId),_receivedAt);
			CaptureFilingResult result=Filer().Assign(entry,0,0);
			Assert.IsTrue(result.IsDone,result.Error);
			CaptureFileEntry entryCopy=_files.Quarantine(Fields(1,captureId),_receivedAt.AddMinutes(5));
			CaptureFilingResult resultCopy=Filer().Assign(entryCopy,0,0);
			Assert.IsFalse(resultCopy.IsDone);
			StringAssert.Contains(resultCopy.Error,"already has a document of this exposure (DocNum "+result.Document.DocNum+", matched by its capture ID)");
			Assert.AreEqual(1,CaptureDb.GetDocuments(_patientFrom.PatNum).Count);
			Assert.AreEqual(1,_files.CountQuarantineFiles(),"The copy stays where it is.");
		}

		[TestMethod]
		public void Assign_AFileWithoutMetadata_WhoseExposureWasSaved_MatchedByItsTime_TheWholeSecondColumn() {
			//Design 7.12 decision 4: without a captureId the copy matches a document whose DateCreated is within 1 s of its receive time. The
			//database keeps whole seconds (10:00:05.750 is stored as 10:00:05), and the match must still find it.
			DateTime receivedAt=new DateTime(2026,10,31,10,0,5,750);
			AcquiredImage image=TestImages.Image8(6,4,(x,y) => 70);
			image.ReceivedAt=receivedAt;
			MountItem slot1=_listSlots.Single(x => x.ItemOrder==1);
			RadiographSaveResult resultSave=new RadiographImport().Save(new RadiographSaveRequest {
				Image=image,Patient=_patientFrom,DocCategory=_category.DefNum,ProvNum=_provNum,Target=new RadiographTarget { Mount=_mount,MountItem=slot1 },
				SessionGuid=RecoveryFiles.SessionGuid,DeviceDescription=_imagingDevice.Description,ImagingDeviceNum=_imagingDevice.ImagingDeviceNum,
			});
			Assert.IsTrue(resultSave.IsSaved,resultSave.Error);
			Assert.AreEqual(new DateTime(2026,10,31,10,0,5),Documents.GetByNum(resultSave.Document.DocNum).DateCreated,"Stored to the second.");
			Dictionary<string,string> dictFields=Fields(1,"");
			dictFields.Remove("Metadata");
			dictFields.Remove("CaptureId");
			dictFields.Remove("Sequence");
			CaptureFileEntry entry=_files.Quarantine(dictFields,receivedAt);
			Assert.AreEqual("",entry.CaptureId);
			CaptureFilingResult result=Filer().Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"(DocNum "+resultSave.Document.DocNum+", matched by its time)");
			//Two seconds later is another exposure: it is filed (slot 1 is taken, so as a recovered unmounted item).
			CaptureFileEntry entryLater=_files.Quarantine(dictFields,receivedAt.AddSeconds(2));
			CaptureFilingResult resultLater=Filer().Assign(entryLater,0,0);
			Assert.IsTrue(resultLater.IsDone,resultLater.Error);
			Assert.IsTrue(resultLater.IsMountItemCreated);
			Assert.AreEqual(2,CaptureDb.GetDocuments(_patientFrom.PatNum).Count);
		}

		[TestMethod]
		public void Assign_A16BitCapture_UnderVerifiedSixteenBitSettings_ADicomDocumentInItsSlot() {
			//Design 7.8: a 16-bit capture is filed only where the session could store it at 16 bits. The conditions come from the database: the
			//Capture settings save (Allow16Bit=1, Bits=16) and a passed Test capture verification record, both Setup entries of the device.
			string folder=Path.Combine(Path.GetTempPath(),"capdb_recovery16_"+Guid.NewGuid().ToString("N"));
			try {
				CaptureDb.WriteCaptureIni(folder,"[Twain]","Allow16Bit=1","[Device."+ToText(_imagingDevice.ImagingDeviceNum)+"]","Bits=16");
				CaptureDb.SaveSettings(_imagingDevice,folder);
				Dictionary<string,string> dictFields=Fields(2,"capdb-16-"+CaptureDb.NewSuffix());
				//RecoveryFiles' TIFF has no resolution tags (the library's Gray16Tiff has them): the sidecar's values stand in, as for a source
				//that reported none in the file.
				dictFields["XDpi"]="1270";
				dictFields["YDpi"]="1270";
				CaptureFileEntry entry=_files.Quarantine16(dictFields,_receivedAt);
				Assert.IsTrue(entry.IsGray16);
				CaptureRecoveryFiler filer=new CaptureRecoveryFiler(new CaptureRecoveryServices {
					Store=_store,
					RadiographStore=new OdRadiographStore(),
					FileStore=_files.Store,
					FuncNow=() => DateTime.Now,
					FuncLoadConfig=() => CaptureDb.LoadCaptureIni(folder),
					FuncGetComputerName=() => CaptureDb.ComputerName,
					ActionWriteLog=line => _listLog.Add(line),
				});
				//Without a verification record: refused, never converted to 8 bits.
				CaptureFilingResult result=filer.Assign(entry,0,0);
				Assert.IsFalse(result.IsDone);
				StringAssert.Contains(result.Error,"no Test capture verification record exists for this device on this computer");
				Assert.AreEqual(0,CaptureDb.GetDocuments(_patientFrom.PatNum).Count);
				//Test capture passed for 16 bits with these settings (its Setup entry, as Test capture writes it).
				CaptureVerificationRecord record=new CaptureVerificationRecord {
					ImagingDeviceNum=_imagingDevice.ImagingDeviceNum,DeviceDescription=_imagingDevice.Description,SourceName=CaptureDb.TwainName,
					SourceProductName=CaptureDb.TwainName,ComputerName=CaptureDb.ComputerName,
					GatedSettingsHash=GatedSettingsHash.Compute(CaptureDb.LoadCaptureIni(folder),_imagingDevice.ImagingDeviceNum),
					DateTimeVerified=DateTime.Now,BitsRequested=16,BitsDelivered=16,TransferMechanism="Memory",Width=6,Height=4,XDpi=1270,YDpi=1270,
					ByteOrder16="LE",ByteOrderCheck="LooksRight",IsByteOrderConfirmed=true,Polarity="AsDelivered",PixelFlavor="Chocolate",IsPolarityConfirmed=true,
				};
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,record.ToLogText(),_imagingDevice.ImagingDeviceNum,DateTime.MinValue);
				result=filer.Assign(entry,0,0);
				Assert.IsTrue(result.IsDone,result.Error);
				Document document=CaptureDb.GetDocuments(_patientFrom.PatNum).Single();
				MountItem slot2=_listSlots.Single(x => x.ItemOrder==2);
				Assert.AreEqual(slot2.MountItemNum,document.MountItemNum);
				Assert.AreEqual("4,5,28,29",document.ToothNumbers);
				StringAssert.EndsWith(document.FileName,".dcm","16 bits: DICOM, never an 8-bit PNG.");
				Assert.AreEqual(ImageType.Radiograph,document.ImgType);
				Assert.IsTrue(document.WindowingMax>document.WindowingMin,"The stored window of the 16-bit samples.");
				byte[] arrayBytes=File.ReadAllBytes(CaptureDb.GetDocumentPath(document));
				Assert.AreEqual("DICM",System.Text.Encoding.ASCII.GetString(arrayBytes,128,4),"A DICOM file (PS3.10) in the A to Z folder.");
				List<SecurityLog> listSecurityLogs=CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_patientFrom.PatNum).Where(x => x.FKey==document.DocNum).ToList();
				Assert.IsTrue(listSecurityLogs.Any(x => x.LogText.StartsWith(CaptureRecoveryText.AssignLogPrefix)));
				Assert.IsFalse(listSecurityLogs.Any(x => x.LogText.StartsWith(RadiographSavePlanner.CaptureLogPrefix)),
					"A DICOM save writes no capture details entry: the header holds them (cap-3 decision 7).");
			}
			finally {
				try {
					Directory.Delete(folder,true);
				}
				catch {
				}
			}
		}

		[TestMethod]
		public void RecoverRecentCaptures_TheSavedExposuresCopyIsRefused_TheOnlyCopyIsFiledIntoItsSlot() {
			//Design 7.1, decision 4: in the patient's capture session, Recover recent captures files a journal copy that has no document yet.
			string captureIdSaved="capdb-saved-"+CaptureDb.NewSuffix();
			string metadata="{\"schema\":\"capture-metadata/1\",\"captureId\":\""+captureIdSaved+"\",\"sequence\":1}";
			RadiographSaveResult resultSave=new RadiographImport().Save(new RadiographSaveRequest {
				Image=TestImages.Image8(6,4,(x,y) => 50,metadata),Patient=_patientFrom,DocCategory=_category.DefNum,ProvNum=_provNum,
				Target=new RadiographTarget { Mount=_mount,MountItem=_listSlots.Single(x => x.ItemOrder==1) },SessionGuid=RecoveryFiles.SessionGuid,
				DeviceDescription=_imagingDevice.Description,ImagingDeviceNum=_imagingDevice.ImagingDeviceNum,
			});
			Assert.IsTrue(resultSave.IsSaved,resultSave.Error);
			CaptureFileEntry entrySaved=_files.Journal(Fields(1,captureIdSaved),_receivedAt,delivered:"Caller");
			Assert.IsTrue(CaptureRecoveryFiler.IsAssignOffered(entrySaved,_patientFrom.PatNum));
			CaptureFilingResult result=Filer().Assign(entrySaved,0,_patientFrom.PatNum);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,"(DocNum "+resultSave.Document.DocNum+", matched by its capture ID)",
				"Found through the ImageCreate entry the saver wrote in the database.");
			//The only copy of an exposure that never reached the patient's record.
			CaptureFileEntry entryOnly=_files.Journal(Fields(3,"capdb-only-"+CaptureDb.NewSuffix()),_receivedAt.AddSeconds(30),delivered:"None");
			StringAssert.EndsWith(Path.GetFileNameWithoutExtension(entryOnly.ImagePath),"-only");
			result=Filer().Assign(entryOnly,0,_patientFrom.PatNum);
			Assert.IsTrue(result.IsDone,result.Error);
			Document document=Documents.GetByNum(result.Document.DocNum);
			Assert.AreEqual(_patientFrom.PatNum,document.PatNum);
			Assert.AreEqual(_listSlots.Single(x => x.ItemOrder==3).MountItemNum,document.MountItemNum);
			SecurityLog securityLog=CaptureDb.GetLogsForPatient(EnumPermType.ImageCreate,_patientFrom.PatNum)
				.Single(x => x.FKey==document.DocNum && x.LogText.StartsWith(CaptureRecoveryText.RecoverLogPrefix));
			StringAssert.Contains(securityLog.LogText,"as DocNum "+document.DocNum);
			Assert.IsFalse(File.Exists(entryOnly.ImagePath),"The only copy gets its plain name back, so rotation removes it.");
			Assert.IsTrue(File.Exists(result.PathResolved));
			Assert.AreEqual(2,CaptureDb.GetDocuments(_patientFrom.PatNum).Count);
		}

		[TestMethod]
		public void Assign_WithoutImageCreate_Refused_NothingWritten() {
			CaptureDb.LogInWith(EnumPermType.ImageEdit,EnumPermType.ImageDelete);
			CaptureFileEntry entry=_files.Quarantine(Fields(1,"capdb-noperm-"+CaptureDb.NewSuffix()),_receivedAt);
			int countLogs=CaptureDb.GetAllLogsForPatient(_patientFrom.PatNum).Count;
			CaptureFilingResult result=Filer().Assign(entry,0,0);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,GroupPermissions.GetDesc(EnumPermType.ImageCreate));
			Assert.AreEqual(0,CaptureDb.GetDocuments(_patientFrom.PatNum).Count);
			Assert.AreEqual(countLogs,CaptureDb.GetAllLogsForPatient(_patientFrom.PatNum).Count);
			Assert.AreEqual(1,_files.CountQuarantineFiles());
		}
		#endregion Assign

		#region Reassign
		[TestMethod]
		public void Reassign_ToAPatientTheUserFound_AnUnmountedDocument_ImageEditEntriesForBothPatients() {
			CaptureFileEntry entry=_files.Quarantine(Fields(1,"capdb-reassign-"+CaptureDb.NewSuffix()),_receivedAt);
			CaptureFilingResult result=Filer().Reassign(entry,_patientOther,"The wrong patient was selected in the main window.",_category.DefNum);
			Assert.IsTrue(result.IsDone,result.Error);
			Document document=CaptureDb.GetDocuments(_patientOther.PatNum).Single();
			Assert.AreEqual(result.Document.DocNum,document.DocNum);
			Assert.AreEqual(0,document.MountItemNum,"Unmounted: no mount of the other patient.");
			Assert.AreEqual("",document.ToothNumbers,"No tooth numbers.");
			Assert.AreEqual(CaptureRecoveryText.ReassignedDescription,document.Description,"The other patient's name never shows in this chart.");
			Assert.AreEqual(_patientOther.PriProv,document.ProvNum,"The target patient's default provider.");
			Assert.IsTrue(File.Exists(CaptureDb.GetDocumentPath(document)));
			Assert.AreEqual(0,CaptureDb.GetDocuments(_patientFrom.PatNum).Count,"Nothing for the originating patient.");
			SecurityLog securityLogTo=CaptureDb.GetLogsForPatient(EnumPermType.ImageEdit,_patientOther.PatNum).Single();
			StringAssert.StartsWith(securityLogTo.LogText,CaptureRecoveryText.ReassignLogPrefix);
			Assert.AreEqual(document.DocNum,securityLogTo.FKey);
			StringAssert.Contains(securityLogTo.LogText,"PatNum "+_patientFrom.PatNum);
			StringAssert.Contains(securityLogTo.LogText,"PatNum "+_patientOther.PatNum);
			StringAssert.Contains(securityLogTo.LogText,"Reason: The wrong patient was selected in the main window.");
			SecurityLog securityLogFrom=CaptureDb.GetLogsForPatient(EnumPermType.ImageEdit,_patientFrom.PatNum).Single();
			Assert.AreEqual(securityLogTo.LogText,securityLogFrom.LogText,"The originating patient's audit shows where the image went.");
			Assert.AreEqual(0,securityLogFrom.FKey);
			Assert.AreEqual(0,_files.CountQuarantineFiles());
			StringAssert.Contains(File.ReadAllText(Path.ChangeExtension(result.PathResolved,".txt")),"DispositionReason=The wrong patient was selected");
		}

		[TestMethod]
		public void Reassign_ImageEditRestrictedToNewerDates_AnOlderCapture_Refused_NothingWritten() {
			CaptureDb.LogInWithDays(Tuple.Create(EnumPermType.ImageEdit,1),Tuple.Create(EnumPermType.ImageCreate,0));
			CaptureFileEntry entry=_files.Quarantine(Fields(1,"capdb-old-"+CaptureDb.NewSuffix()),DateTime.Today.AddDays(-10).AddHours(9));
			CaptureFilingResult result=Filer().Reassign(entry,_patientOther,"Wrong patient.",_category.DefNum);
			Assert.IsFalse(result.IsDone,"Date-restricted by the receive time (design 10.7).");
			Assert.AreNotEqual("",result.Error);
			Assert.AreEqual(0,CaptureDb.GetDocuments(_patientOther.PatNum).Count);
			Assert.AreEqual(0,CaptureDb.GetLogsForPatient(EnumPermType.ImageEdit,_patientOther.PatNum).Count);
			Assert.AreEqual(1,_files.CountQuarantineFiles());
		}
		#endregion Reassign

		#region Discard
		[TestMethod]
		public void Discard_LoggedUnderImageDeleteFirst_TheFileIsKeptInResolved() {
			CaptureFileEntry entry=_files.Quarantine(Fields(4,"capdb-discard-"+CaptureDb.NewSuffix()),_receivedAt);
			CaptureFilingResult result=Filer().Discard(entry);
			Assert.IsTrue(result.IsDone,result.Error);
			SecurityLog securityLog=CaptureDb.GetLogsForPatient(EnumPermType.ImageDelete,_patientFrom.PatNum).Single();
			Assert.AreEqual(0,securityLog.FKey);
			Assert.AreEqual(Security.CurUser.UserNum,securityLog.UserNum);
			Assert.AreEqual(CaptureRecoveryText.DiscardLogPrefix+CaptureRecoveryText.GetFileText(entry)+", bound patient: "+_patientFrom.GetNameLF()
				+" (PatNum "+_patientFrom.PatNum+"). The image and its sidecar are kept in Capture\\quarantine-resolved\\"+RecoveryFiles.SessionGuid.ToString("D")+".",
				securityLog.LogText);
			Assert.AreEqual(0,CaptureDb.GetDocuments(_patientFrom.PatNum).Count,"Nothing is filed.");
			Assert.AreEqual(0,_files.CountQuarantineFiles());
			Assert.IsTrue(File.Exists(result.PathResolved),"Nothing is deleted.");
			StringAssert.Contains(result.PathResolved,@"Capture\quarantine-resolved\"+RecoveryFiles.SessionGuid.ToString("D"));
		}

		[TestMethod]
		public void Discard_WithoutImageDelete_Refused_NothingLogged() {
			CaptureDb.LogInWith(EnumPermType.ImageCreate,EnumPermType.ImageEdit);
			CaptureFileEntry entry=_files.Quarantine(Fields(4,"capdb-nodel-"+CaptureDb.NewSuffix()),_receivedAt);
			CaptureFilingResult result=Filer().Discard(entry);
			Assert.IsFalse(result.IsDone);
			StringAssert.Contains(result.Error,GroupPermissions.GetDesc(EnumPermType.ImageDelete));
			Assert.AreEqual(0,CaptureDb.GetLogsForPatient(EnumPermType.ImageDelete,_patientFrom.PatNum).Count);
			Assert.AreEqual(1,_files.CountQuarantineFiles());
		}
		#endregion Discard
	}
}
