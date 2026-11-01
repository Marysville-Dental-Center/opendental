// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ForkMigrationTests;
using ImagingCapture.Settings;
using ImagingCapture.Templates;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.FileIO;
using OpenDentBusiness.ForkMigrations;
using UnitTestsCore;

namespace ImagingCapture.Tests.Db {
	///<summary>The one scratch database of the capture track's database tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1)
	///upgraded by the fork migration runner to the current schema, with a unique per-run name, dropped at the end of the run, and a temporary A
	///to Z folder. It is built by the first database test that runs (Begin), so the other tests of this assembly never wait for it. Synthetic
	///data only: made-up patients, providers, image categories, users, imaging devices and mounts. Skipped (inconclusive) when the local MariaDB
	///does not answer, when FORKMIGRATION_TEST_DB=skip, or when the database cannot be built.</summary>
	[TestClass]
	public class CaptureDb {
		public static readonly string DbName=TestDb.ScratchName("capdb");
		///<summary>This run's A to Z folder, under the system temp folder. Deleted at the end.</summary>
		public static readonly string AtoZPath=Path.Combine(Path.GetTempPath(),"capdb_atoz_"+Guid.NewGuid().ToString("N").Substring(0,8));
		///<summary>The computer name the capture settings and the session use (capture.ini is per computer). Made up.</summary>
		public const string ComputerName="CAPDB-OPERATORY1";
		///<summary>The TWAIN source name of the synthetic imaging devices.</summary>
		public const string TwainName="Synthetic Sensor DS";
		private static readonly object _lock=new object();
		private static bool _isTried;
		private static bool _isBuilt;
		private static string _skipReason;

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			try {
				if(Directory.Exists(AtoZPath)) {
					Directory.Delete(AtoZPath,true);
				}
			}
			catch(Exception) {
				//Only a temp folder.
			}
			if(!_isBuilt || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		///<summary>Call from [TestInitialize] of every database test: builds the database the first time, skips the test when there is none, and
		///otherwise connects to it, with images in this run's A to Z folder and a user with the capture permissions logged in.</summary>
		public static void Begin() {
			SetCulture();
			lock(_lock) {
				if(!_isTried) {
					_isTried=true;
					Build();
				}
			}
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DbName);
			SetStorageAtoZ();
			LogInWith(EnumPermType.ImageCreate,EnumPermType.ImageEdit,EnumPermType.ImageDelete,EnumPermType.Setup);
		}

		private static void Build() {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				TestDb.DropStaleScratchDatabases();
				TestDb.CreateFresh24_3(DbName);
				_isBuilt=true;
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.AllLocal);
				Directory.CreateDirectory(AtoZPath);
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		///<summary>Lans.g returns the English text without touching the database only in en-US.</summary>
		private static void SetCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
		}

		///<summary>Images go to this run's A to Z folder, as a clinic's local A to Z folder.</summary>
		public static void SetStorageAtoZ() {
			if(PrefC.AtoZfolderUsed!=DataStorageType.LocalAtoZ) {
				Prefs.UpdateInt(PrefName.AtoZfolderUsed,(int)DataStorageType.LocalAtoZ);
				Cache.Refresh(InvalidType.Prefs);
			}
			Directory.CreateDirectory(AtoZPath);
			FileAtoZ.LocalAtoZpath=AtoZPath;
		}

		public static string NewSuffix() {
			return Guid.NewGuid().ToString("N").Substring(0,8);
		}

		#region Users
		///<summary>Logs in a new user whose only group has exactly these permissions, none date-restricted. Returns the user.</summary>
		public static Userod LogInWith(params EnumPermType[] arrayPermTypes) {
			return LogInWithDays(arrayPermTypes.Select(x => Tuple.Create(x,0)).ToArray());
		}

		///<summary>Logs in a new user whose only group has these permissions, each with its "newer than this many days" restriction (0 for none).</summary>
		public static Userod LogInWithDays(params Tuple<EnumPermType,int>[] arrayPerms) {
			long userGroupNum=UserGroupT.CreateUserGroup("Capture db test group "+NewSuffix());
			foreach(Tuple<EnumPermType,int> perm in arrayPerms) {
				GroupPermissions.Insert(new GroupPermission {
					UserGroupNum=userGroupNum,
					PermType=perm.Item1,
					NewerDate=DateTime.MinValue,
					NewerDays=perm.Item2,
				});
			}
			GroupPermissions.RefreshCache();
			Userod userod=UserodT.CreateUser(userGroupNumbers:new List<long> { userGroupNum });
			Security.CurUser=userod;
			return userod;
		}
		#endregion Users

		#region Synthetic records
		///<summary>A made-up provider. Returns its ProvNum (the provider cache is refreshed).</summary>
		public static long CreateProvider(string tag) {
			return ProviderT.CreateProvider("CD"+tag.Substring(0,Math.Min(tag.Length,4))+NewSuffix().Substring(0,3),fName:"Synthetic",lName:"Provider"+tag);
		}

		///<summary>A made-up patient with priProvNum as the primary provider.</summary>
		public static Patient CreatePatient(string tag,long priProvNum) {
			string suffix=NewSuffix();
			return PatientT.CreatePatient(priProvNum:priProvNum,lName:"Capdb"+tag+suffix,fName:"Synthetic",birthDate:new DateTime(1980,1,2));
		}

		///<summary>A new image category (DefCat.ImageCats); the definitions cache is refreshed.</summary>
		public static Def CreateImageCategory(string tag,bool isHidden=false) {
			return DefT.CreateDefinition(DefCat.ImageCats,"Capdb "+tag+" "+NewSuffix(),itemValue:"X",isHidden:isHidden);
		}

		///<summary>A TwainRadiograph imaging device row for this computer; the ToolButsAndMounts cache (which holds the imaging devices) is refreshed.</summary>
		public static ImagingDevice CreateImagingDevice(string tag) {
			ImagingDevice imagingDevice=new ImagingDevice {
				Description="Capdb sensor "+tag,
				ComputerName="",
				DeviceType=EnumImgDeviceType.TwainRadiograph,
				TwainName=TwainName,
				ItemOrder=0,
				ShowTwainUI=false,
			};
			imagingDevice.ImagingDeviceNum=ImagingDevices.Insert(imagingDevice);
			Cache.Refresh(InvalidType.ToolButsAndMounts);
			return imagingDevice;
		}

		///<summary>A mount definition with the layout and slots of a standard template, under a Description of its own (the shared database keeps
		///every test's definitions), inserted as "Add standard templates" inserts one (OdStandardTemplateStore). Returns it as stored.</summary>
		public static MountDef CreateMountDefFromTemplate(StandardTemplateKind kind,long categoryDefNum,string tag) {
			StandardTemplate template=StandardMountTemplates.Get(kind);
			OdStandardTemplateStore store=new OdStandardTemplateStore();
			MountDef mountDef=StandardTemplatePlanner.BuildMountDef(template,categoryDefNum,itemOrder:0);
			mountDef.Description=template.Description+" "+tag+" "+NewSuffix();
			mountDef.MountDefNum=store.InsertMountDef(mountDef);
			mountDef.IsNew=false;
			foreach(MountItemDef mountItemDef in StandardTemplatePlanner.BuildMountItemDefs(template,mountDef.MountDefNum)) {
				store.InsertMountItemDef(mountItemDef);
			}
			Cache.Refresh(InvalidType.ToolButsAndMounts);
			return MountDefs.GetDeepCopy().Single(x => x.MountDefNum==mountDef.MountDefNum);
		}

		///<summary>What ControlImages.ToolBarMountAcquire_Click does after FrmMountAndAcquire with a mount definition picked (ControlImages.cs:3609-3613):
		///the mount and its items from the definition, and the "Mount Created" entry. Returns the mount as the module then shows it (read back).</summary>
		public static Mount CreateMountFromDef(MountDef mountDef,Patient patient,long categoryDefNum) {
			Mount mount=Mounts.CreateMountFromDef(mountDef,patient.PatNum,categoryDefNum);
			Def defDocCategory=Defs.GetDef(DefCat.ImageCats,mount.DocCategory);
			string logText="Mount Created: "+mount.Description+" with category "
				+defDocCategory.ItemName;
			SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,patient.PatNum,logText);
			return Mounts.GetByNum(mount.MountNum);
		}

		///<summary>A document row in slot mountItem of the patient, stored in the A to Z folder as a small PNG (ImageStore.Import, then placed
		///the way the Imaging module places one): another window, or the legacy loop, filed it.</summary>
		public static Document CreateDocumentInSlot(Patient patient,MountItem mountItem,long categoryDefNum) {
			byte[] arrayPng=Png(4,4,90);
			Document document=ImageStore.Import(arrayPng,categoryDefNum,ImageType.Radiograph,patient,"image/png",".png");
			Document documentOld=document.Copy();
			document.MountItemNum=mountItem.MountItemNum;
			document.ToothNumbers=mountItem.ToothNumbers;
			Documents.Update(document,documentOld);
			return Documents.GetByNum(document.DocNum);
		}

		///<summary>An 8-bit grey PNG of one value, for documents made outside the capture code.</summary>
		public static byte[] Png(int width,int height,byte grey) {
			using(System.Drawing.Bitmap bitmap=new System.Drawing.Bitmap(width,height))
			using(MemoryStream memoryStream=new MemoryStream()) {
				for(int y=0;y<height;y++) {
					for(int x=0;x<width;x++) {
						bitmap.SetPixel(x,y,System.Drawing.Color.FromArgb(grey,grey,grey));
					}
				}
				bitmap.Save(memoryStream,System.Drawing.Imaging.ImageFormat.Png);
				return memoryStream.ToArray();
			}
		}
		#endregion Synthetic records

		#region Reading back
		///<summary>Security log rows of one permission for the patient, oldest first.</summary>
		public static List<SecurityLog> GetLogsForPatient(EnumPermType permType,long patNum) {
			string command="SELECT * FROM securitylog WHERE PermType="+POut.Int((int)permType)+" AND PatNum="+POut.Long(patNum)
				+" ORDER BY SecurityLogNum";
			return OpenDentBusiness.Crud.SecurityLogCrud.SelectMany(command);
		}

		///<summary>Security log rows of one permission with this FKey, oldest first.</summary>
		public static List<SecurityLog> GetLogsByFKey(EnumPermType permType,long fKey) {
			string command="SELECT * FROM securitylog WHERE PermType="+POut.Int((int)permType)+" AND FKey="+POut.Long(fKey)+" ORDER BY SecurityLogNum";
			return OpenDentBusiness.Crud.SecurityLogCrud.SelectMany(command);
		}

		///<summary>Security log rows of one permission with this FKey whose text starts with prefix, oldest first. FKey alone is not enough for
		///Setup entries: the Capture settings screen keys them by ImagingDeviceNum and the mount templates by MountDefNum, and the two tables
		///number their rows independently.</summary>
		public static List<SecurityLog> GetLogsByFKey(EnumPermType permType,long fKey,string prefix) {
			return GetLogsByFKey(permType,fKey).Where(x => (x.LogText??"").StartsWith(prefix,StringComparison.Ordinal)).ToList();
		}

		///<summary>The texts of rows, for assertion messages (one line: the test output keeps only the first line of a message).</summary>
		public static string Texts(IEnumerable<SecurityLog> listSecurityLogs) {
			return string.Join(" || ",listSecurityLogs.Select(x => x.PermType+"|"+x.PatNum+"|"+x.FKey+"|"+(x.LogText??"").Replace("\r"," ").Replace("\n"," ")));
		}

		///<summary>Every security log row of the patient, oldest first.</summary>
		public static List<SecurityLog> GetAllLogsForPatient(long patNum) {
			return OpenDentBusiness.Crud.SecurityLogCrud.SelectMany("SELECT * FROM securitylog WHERE PatNum="+POut.Long(patNum)+" ORDER BY SecurityLogNum");
		}

		///<summary>The securityloghash row of a security log row exists (SecurityLogs.MakeLogEntry writes one with every entry).</summary>
		public static bool HasLogHash(long securityLogNum) {
			return DataCore.GetScalar("SELECT COUNT(*) FROM securityloghash WHERE SecurityLogNum="+POut.Long(securityLogNum))!="0";
		}

		public static List<Document> GetDocuments(long patNum) {
			return OpenDentBusiness.Crud.DocumentCrud.SelectMany("SELECT * FROM document WHERE PatNum="+POut.Long(patNum)+" ORDER BY DocNum");
		}

		public static List<MountItem> GetMountItemRows(long mountNum) {
			return OpenDentBusiness.Crud.MountItemCrud.SelectMany("SELECT * FROM mountitem WHERE MountNum="+POut.Long(mountNum)+" ORDER BY MountItemNum");
		}

		public static bool MountExists(long mountNum) {
			return DataCore.GetScalar("SELECT COUNT(*) FROM mount WHERE MountNum="+POut.Long(mountNum))!="0";
		}

		///<summary>The patient as stored (ImageStore.Import sets Patient.ImageFolder the first time).</summary>
		public static Patient GetPatient(long patNum) {
			return Patients.GetPat(patNum);
		}

		///<summary>The full path of a document's file in this run's A to Z folder.</summary>
		public static string GetDocumentPath(Document document) {
			Patient patient=GetPatient(document.PatNum);
			return Path.Combine(AtoZPath,patient.ImageFolder.Substring(0,1).ToUpperInvariant(),patient.ImageFolder,document.FileName);
		}
		#endregion Reading back

		#region capture.ini
		///<summary>A capture.ini of this test in its own temp folder (the folder is the test's to delete), and the config that reads it.</summary>
		public static LocalCaptureConfig WriteCaptureIni(string folder,params string[] arrayLines) {
			Directory.CreateDirectory(folder);
			string path=Path.Combine(folder,"capture.ini");
			File.WriteAllBytes(path,new UTF8Encoding(false).GetBytes(string.Join("\r\n",arrayLines)+"\r\n"));
			return LoadCaptureIni(folder);
		}

		///<summary>The capture.ini of the folder, read fresh (a missing file reads as every default).</summary>
		public static LocalCaptureConfig LoadCaptureIni(string folder) {
			return LocalCaptureConfig.Load(Path.Combine(folder,"capture.ini"),Path.Combine(folder,"fallback.ini"));
		}

		///<summary>The Capture settings screen on the folder's capture.ini for the device, on this computer, with the real store.</summary>
		public static CaptureSettingsModel SettingsModel(ImagingDevice imagingDevice,string folder) {
			CaptureSettingsServices services=new CaptureSettingsServices {
				Store=new OdCaptureSettingsStore(),
				FuncLoadConfig=() => LoadCaptureIni(folder),
				FuncGetComputerName=() => ComputerName,
				FuncNow=() => DateTime.Now,
			};
			CaptureSettingsModel model=new CaptureSettingsModel(new CaptureSettingsDevice {
				ImagingDeviceNum=imagingDevice.ImagingDeviceNum,
				Description=imagingDevice.Description,
				TwainName=imagingDevice.TwainName,
			},services);
			model.Load();
			return model;
		}

		///<summary>A Setup user saves the device's capture settings in the Capture settings screen with each "Section.Key=value" edit ("Device" for
		///the device's section), so the session's hash check passes for them. Fails the test when the save is not logged.</summary>
		public static CaptureSettingsSaveResult SaveSettings(ImagingDevice imagingDevice,string folder,params string[] arrayEdits) {
			CaptureSettingsModel model=SettingsModel(imagingDevice,folder);
			foreach(string edit in arrayEdits) {
				int idxDot=edit.IndexOf('.');
				int idxEquals=edit.IndexOf('=');
				int idx=model.IndexOf(edit.Substring(0,idxDot),edit.Substring(idxDot+1,idxEquals-idxDot-1));
				Assert.AreNotEqual(-1,idx,"Not a setting of the screen: "+edit);
				Assert.IsTrue(model.SetText(idx,edit.Substring(idxEquals+1)));
			}
			CaptureSettingsSaveResult result=model.Save();
			Assert.IsTrue(result.IsSaved && result.IsLogged,"The capture settings were not saved and logged: "+result.Error);
			return result;
		}
		#endregion capture.ini
	}
}
