// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ImagingCapture.Recovery;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>ICaptureRecoveryStore for the quarantine tests: patients, mounts, slots and documents in memory, denied permissions, and every
	///security-log entry written.</summary>
	internal sealed class FakeCaptureRecoveryStore:ICaptureRecoveryStore {
		public readonly Dictionary<long,Patient> DictPatients=new Dictionary<long,Patient>();
		public readonly Dictionary<long,Mount> DictMounts=new Dictionary<long,Mount>();
		public readonly Dictionary<long,List<MountItem>> DictMountItems=new Dictionary<long,List<MountItem>>();
		///<summary>Documents by MountItemNum (a slot's image).</summary>
		public readonly Dictionary<long,Document> DictSlotDocuments=new Dictionary<long,Document>();
		public readonly List<Document> ListPatientDocuments=new List<Document>();
		public readonly List<SecurityLog> ListSecurityLogsExisting=new List<SecurityLog>();
		public readonly HashSet<EnumPermType> HashSetDenied=new HashSet<EnumPermType>();
		public readonly List<Tuple<EnumPermType,DateTime>> ListAuthorizationChecks=new List<Tuple<EnumPermType,DateTime>>();
		public readonly List<SecurityLog> ListLogEntries=new List<SecurityLog>();
		public readonly HashSet<long> HashSetCategories=new HashSet<long> { 20,21 };
		public string ThrowOnMakeLogEntry;
		public long ProvNumDefault=3;

		public bool IsAuthorized(EnumPermType permType,DateTime date,out string message) {
			ListAuthorizationChecks.Add(Tuple.Create(permType,date));
			if(HashSetDenied.Contains(permType)) {
				message="Not authorized for "+permType+".";
				return false;
			}
			message="";
			return true;
		}

		public Patient GetPatient(long patNum) {
			Patient patient;
			return DictPatients.TryGetValue(patNum,out patient) ? patient : null;
		}

		///<summary>PatNum merged from, PatNum merged into (the surviving patient).</summary>
		public readonly Dictionary<long,long> DictMergedInto=new Dictionary<long,long>();

		public long GetMergedIntoPatNum(long patNum) {
			long patNumInto;
			return DictMergedInto.TryGetValue(patNum,out patNumInto) ? patNumInto : 0;
		}

		public Mount GetMount(long mountNum) {
			Mount mount;
			return DictMounts.TryGetValue(mountNum,out mount) ? mount : null;
		}

		public List<MountItem> GetMountItems(long mountNum) {
			List<MountItem> listMountItems;
			return DictMountItems.TryGetValue(mountNum,out listMountItems) ? listMountItems.ToList() : new List<MountItem>();
		}

		public List<Document> GetDocumentsForMountItems(List<MountItem> listMountItems) {
			return listMountItems.Select(x => {
				Document document;
				return DictSlotDocuments.TryGetValue(x.MountItemNum,out document) ? document : null;
			}).ToList();
		}

		public List<Document> GetPatientDocuments(long patNum) {
			return ListPatientDocuments.Where(x => x.PatNum==patNum).ToList();
		}

		public List<SecurityLog> GetImageCreateLogEntries(long patNum,List<long> listDocNums) {
			return ListSecurityLogsExisting.Where(x => x.PatNum==patNum && listDocNums.Contains(x.FKey)).ToList();
		}

		public bool IsCategoryValid(long defNum) {
			return HashSetCategories.Contains(defNum);
		}

		public List<Def> GetImageCategories() {
			return HashSetCategories.Select(x => new Def { DefNum=x,ItemName="Category "+x,Category=DefCat.ImageCats }).ToList();
		}

		public long GetDefaultProvNum(Patient patient) {
			return ProvNumDefault;
		}

		public ImagingDevice GetImagingDevice(long imagingDeviceNum) {
			return imagingDeviceNum==12 ? CaptureControllerHarness.Device() : null;
		}

		public List<SecurityLog> GetSetupLogEntries(long imagingDeviceNum) {
			return new List<SecurityLog>();
		}

		public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
			if(ThrowOnMakeLogEntry!=null) {
				throw new Exception(ThrowOnMakeLogEntry);
			}
			ListLogEntries.Add(new SecurityLog { PermType=permType,PatNum=patNum,LogText=logText,FKey=fKey });
		}

		public string GetUserName() {
			return "Admin";
		}
	}

	///<summary>Writes quarantine files and journal copies the way the library does (a .bmp with its BITMAPFILEHEADER, or a 16-bit baseline TIFF,
	///and a Key=Value sidecar), into a temp Imaging folder, so that the real CaptureFileStore lists and reads them.</summary>
	internal sealed class RecoveryFiles:IDisposable {
		public static readonly Guid SessionGuid=new Guid("6f1c2a4e-1111-4b2c-9d3e-0123456789ab");
		public readonly string Folder;
		private int _counter;

		public RecoveryFiles() {
			Folder=Path.Combine(Path.GetTempPath(),"CaptureRecoveryTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Folder);
		}

		public CaptureFileStore Store => CaptureFileStore.CreateForFolder(Folder,() => new DateTime(2026,10,31,12,0,0));

		///<summary>The sidecar fields of a session capture of patient 42 for slot 3 (MountItemNum 103) of mount 7, category 20, provider 4.</summary>
		public static Dictionary<string,string> SessionFields(long patNum=42,string captureId="cap-7",string acquiredAt="2026-10-31T10:00:04.500+00:00") {
			string metadata="{\"schema\":\"capture-metadata/1\",\"captureId\":\""+captureId+"\",\"sequence\":3,\"acquiredAt\":\""+acquiredAt+"\"}";
			return new Dictionary<string,string> {
				{ "Source","Example Sensor DS" },
				{ "Computer","OPERATORY1" },
				{ "WindowsUser","CLINIC\\xray" },
				{ "Patient",patNum>0 ? "Doe, Jane" : "patient unknown" },
				{ "PatNum",patNum>0 ? patNum.ToString(CultureInfo.InvariantCulture) : "" },
				{ "SessionGuid",SessionGuid.ToString("D") },
				{ "Slot","Slot 3 (teeth 6)" },
				{ "Target","Slot 3 (teeth 6)" },
				{ "Reason","It arrived after a patient change." },
				{ "CaptureId",captureId },
				{ "Sequence","3" },
				{ "ImageKind","Normal" },
				{ "Redelivered","0" },
				{ "RequestedBits","8" },
				{ "TransferMechanism","Native" },
				{ "PixelFlavor","0" },
				{ CaptureSidecarKeys.PixelType,"1" },
				{ CaptureSidecarKeys.BitsPerSample,"8" },
				{ CaptureSidecarKeys.BitDepth16InEffect,"0" },
				{ CaptureSidecarKeys.MountNum,"7" },
				{ CaptureSidecarKeys.MountItemNum,"103" },
				{ CaptureSidecarKeys.SlotItemOrder,"3" },
				{ CaptureSidecarKeys.ToothNumbers,"6" },
				{ CaptureSidecarKeys.DocCategory,"20" },
				{ CaptureSidecarKeys.ProvNum,"4" },
				{ CaptureSidecarKeys.ImagingDeviceNum,"12" },
				{ "Metadata",metadata },
			};
		}

		///<summary>A quarantine .bmp in the session folder (or, with isRoot, at the quarantine root as the CAP-1 library writes them).</summary>
		public CaptureFileEntry Quarantine(Dictionary<string,string> dictFields,DateTime receivedAt,bool isRoot=false) {
			string folder=Path.Combine(Folder,@"Capture\quarantine",isRoot ? "" : SessionGuid.ToString("D"));
			dictFields["Kind"]="quarantine";
			string path=Write(folder,receivedAt,Bmp(TestImages.Dib8Gray(6,4,(x,y) => (byte)(x*30+y))),".bmp",dictFields);
			return Store.ListQuarantine().Single(x => x.ImagePath==path);
		}

		///<summary>A 16-bit quarantine .tif in the session folder.</summary>
		public CaptureFileEntry Quarantine16(Dictionary<string,string> dictFields,DateTime receivedAt) {
			string folder=Path.Combine(Folder,@"Capture\quarantine",SessionGuid.ToString("D"));
			dictFields["Kind"]="quarantine";
			dictFields["RequestedBits"]="16";
			dictFields["TransferMechanism"]="Memory";
			dictFields[CaptureSidecarKeys.BitDepth16InEffect]="1";
			ushort[] arraySamples=new ushort[6*4];
			for(int i=0;i<arraySamples.Length;i++) {
				arraySamples[i]=(ushort)(1000+i*97);
			}
			string path=Write(folder,receivedAt,Tiff16(6,4,arraySamples),".tif",dictFields);
			return Store.ListQuarantine().Single(x => x.ImagePath==path);
		}

		///<summary>A 16-bit journal copy (.tif) with Delivered=Caller, its samples 1000+97*i as Quarantine16's.</summary>
		public CaptureFileEntry Journal16(Dictionary<string,string> dictFields,DateTime receivedAt) {
			string folder=Path.Combine(Folder,@"Capture\journal");
			dictFields["Kind"]="journal copy";
			dictFields["Delivered"]="Caller";
			dictFields["RequestedBits"]="16";
			dictFields["TransferMechanism"]="Memory";
			dictFields[CaptureSidecarKeys.BitDepth16InEffect]="1";
			dictFields.Remove("Slot");
			dictFields.Remove("Patient");
			dictFields.Remove("Reason");
			ushort[] arraySamples=new ushort[6*4];
			for(int i=0;i<arraySamples.Length;i++) {
				arraySamples[i]=(ushort)(1000+i*97);
			}
			string path=Write(folder,receivedAt,Tiff16(6,4,arraySamples),".tif",dictFields);
			return Store.ListJournal().Single(x => x.ImagePath==path);
		}

		///<summary>A journal copy with Delivered (Caller, Quarantine or None).</summary>
		public CaptureFileEntry Journal(Dictionary<string,string> dictFields,DateTime receivedAt,string delivered="Caller") {
			string folder=Path.Combine(Folder,@"Capture\journal");
			dictFields["Kind"]="journal copy";
			dictFields["Delivered"]=delivered;
			dictFields.Remove("Slot");
			dictFields.Remove("Patient");
			dictFields.Remove("Reason");
			string path=Write(folder,receivedAt,Bmp(TestImages.Dib8Gray(6,4,(x,y) => (byte)(x*30+y))),".bmp",dictFields,delivered=="None" ? "-only" : "");
			return Store.ListJournal().Single(x => x.ImagePath==path);
		}

		public int CountQuarantineFiles() {
			string folder=Path.Combine(Folder,@"Capture\quarantine");
			return Directory.Exists(folder) ? Directory.GetFiles(folder,"*.*",SearchOption.AllDirectories).Count(x => !x.EndsWith(".txt")) : 0;
		}

		private string Write(string folder,DateTime receivedAt,byte[] bytes,string extension,Dictionary<string,string> dictFields,string suffix="") {
			Directory.CreateDirectory(folder);
			_counter++;
			string baseName=receivedAt.ToString("yyyyMMdd-HHmmss-fff",CultureInfo.InvariantCulture)+"-"+_counter.ToString("D6",CultureInfo.InvariantCulture)
				+"-abcd"+suffix;
			string path=Path.Combine(folder,baseName+extension);
			File.WriteAllBytes(path,bytes);
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("; test sidecar\r\n");
			stringBuilder.Append("ReceivedAt=").Append(receivedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz",CultureInfo.InvariantCulture)).Append("\r\n");
			foreach(KeyValuePair<string,string> pair in dictFields) {
				stringBuilder.Append(pair.Key).Append('=').Append(pair.Value).Append("\r\n");
			}
			File.WriteAllText(Path.Combine(folder,baseName+".txt"),stringBuilder.ToString(),new UTF8Encoding(false));
			return path;
		}

		private static byte[] Bmp(byte[] dib) {
			byte[] bytes=new byte[14+dib.Length];
			bytes[0]=(byte)'B';
			bytes[1]=(byte)'M';
			Array.Copy(BitConverter.GetBytes(bytes.Length),0,bytes,2,4);
			int headerSize=BitConverter.ToInt32(dib,0);
			int clrUsed=BitConverter.ToInt32(dib,32);
			int bitCount=BitConverter.ToInt16(dib,14);
			int colors=bitCount<=8 ? (clrUsed==0 ? 1<<bitCount : clrUsed) : 0;
			Array.Copy(BitConverter.GetBytes(14+headerSize+colors*4),0,bytes,10,4);
			Buffer.BlockCopy(dib,0,bytes,14,dib.Length);
			return bytes;
		}

		///<summary>A baseline 16-bit grayscale TIFF, little-endian, one strip, BlackIsZero, as Gray16Tiff writes it.</summary>
		private static byte[] Tiff16(int width,int height,ushort[] samples) {
			const int countEntries=9;
			int offsetPixels=8+2+countEntries*12+4;
			byte[] bytes=new byte[offsetPixels+samples.Length*2];
			bytes[0]=(byte)'I';
			bytes[1]=(byte)'I';
			BitConverter.GetBytes((ushort)42).CopyTo(bytes,2);
			BitConverter.GetBytes(8).CopyTo(bytes,4);
			BitConverter.GetBytes((ushort)countEntries).CopyTo(bytes,8);
			int offset=10;
			int[][] arrayEntries={
				new[] { 256,4,width },new[] { 257,4,height },new[] { 258,3,16 },new[] { 259,3,1 },new[] { 262,3,1 },new[] { 273,4,offsetPixels },
				new[] { 277,3,1 },new[] { 278,4,height },new[] { 279,4,samples.Length*2 },
			};
			foreach(int[] arrayEntry in arrayEntries) {
				BitConverter.GetBytes((ushort)arrayEntry[0]).CopyTo(bytes,offset);
				BitConverter.GetBytes((ushort)arrayEntry[1]).CopyTo(bytes,offset+2);
				BitConverter.GetBytes(1).CopyTo(bytes,offset+4);
				if(arrayEntry[1]==3) {
					BitConverter.GetBytes((ushort)arrayEntry[2]).CopyTo(bytes,offset+8);
				}
				else {
					BitConverter.GetBytes(arrayEntry[2]).CopyTo(bytes,offset+8);
				}
				offset+=12;
			}
			for(int i=0;i<samples.Length;i++) {
				BitConverter.GetBytes(samples[i]).CopyTo(bytes,offsetPixels+i*2);
			}
			return bytes;
		}

		public void Dispose() {
			try {
				Directory.Delete(Folder,true);
			}
			catch {
			}
		}
	}

	///<summary>A filer on fakes: the patient 42 (Jane Doe) with mount 7 (slots 101-103 as CaptureControllerHarness.Slots), the patient 77 (John
	///Roe), the categories 20 and 21, and capture.ini at its defaults (Bits=16, Allow16Bit=1; no Test capture record, so no 16-bit DICOM).</summary>
	internal sealed class RecoveryHarness:IDisposable {
		public readonly FakeCaptureRecoveryStore Store=new FakeCaptureRecoveryStore();
		public readonly FakeSessionRadiographStore RadiographStore=new FakeSessionRadiographStore();
		public readonly RecoveryFiles Files=new RecoveryFiles();
		public readonly List<string> ListLog=new List<string>();
		public readonly Patient PatientJane=new Patient { PatNum=42,FName="Jane",LName="Doe",Preferred="",MiddleI="",Birthdate=new DateTime(1980,1,2),PriProv=3 };
		public readonly Patient PatientJohn=new Patient { PatNum=77,FName="John",LName="Roe",Preferred="",MiddleI="",Birthdate=new DateTime(1975,5,6),PriProv=3 };
		public readonly Mount Mount=new Mount { MountNum=7,PatNum=42,Description="FMX",DocCategory=21,DateCreated=new DateTime(2026,10,31,9,0,0) };
		public LocalCaptureConfig Config=LocalCaptureConfig.Load((string)null);

		public RecoveryHarness() {
			Store.DictPatients[42]=PatientJane;
			Store.DictPatients[77]=PatientJohn;
			Store.DictMounts[7]=Mount;
			Store.DictMountItems[7]=CaptureControllerHarness.Slots(3);
		}

		public CaptureRecoveryServices Services => new CaptureRecoveryServices {
			Store=Store,
			RadiographStore=RadiographStore,
			FileStore=Files.Store,
			FuncNow=() => new DateTime(2026,10,31,12,0,0),
			FuncLoadConfig=() => Config,
			FuncGetComputerName=() => "OPERATORY1",
			ActionWriteLog=line => ListLog.Add(line),
		};

		public CaptureRecoveryFiler Filer => new CaptureRecoveryFiler(Services);

		///<summary>Slot k of mount 7 (MountItemNum 100+k).</summary>
		public MountItem Slot(int k) {
			return Store.DictMountItems[7].Single(x => x.MountItemNum==100+k);
		}

		public void Dispose() {
			Files.Dispose();
		}
	}
}
