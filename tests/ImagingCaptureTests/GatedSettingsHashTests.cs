// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ImagingCapture.Settings;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>The gated-block hash of design 9.3: which keys it covers, its canonical text, and which edits change it.</summary>
	[TestClass]
	public class GatedSettingsHashTests {
		private readonly List<string> _listPaths=new List<string>();

		///<summary>Every gated key set, plus hardware bindings and a second device that must not matter.</summary>
		private const string IniBase=@"[General]
Version=1

[Session]
Enabled=1
IdleDisableMinutes=8

[Twain]
ThreadModal=1
AcquireTimeoutMinutes=12
Allow16Bit=1
RestrictSources=1
AllowedSources=Scanner One|Sensor Source
JournalKeep=200
DisableIsSafeSources=Sensor Source
SafeDisableCap=0x8002
RefusedDisableCodes=0x8001|0x8002

[Device.12]
SessionMode=PerSlot
Bits=16
ByteOrder16=LE
Polarity=AsDelivered
Headless=1
DisableIsSafe=1
SessionTimeoutMinutes=15
SafeDisableCap=0x8002
RefusedDisableCodes=0x8001|0x8002
ContinuousCap=
RedeliverCap=0x8003
NoActivateCap=0x8004

[Device.13]
Bits=8

[Camera]
Moniker=@device:pnp:x
Name=Camera

[Keys]
Retake=F5
";

		private const string CanonicalBase="capture-gated-settings/1\n"
			+"Device=12\n"
			+"Session.Enabled=1\n"
			+"Session.IdleDisableMinutes=8\n"
			+"Twain.ThreadModal=1\n"
			+"Twain.AcquireTimeoutMinutes=12\n"
			+"Twain.Allow16Bit=1\n"
			+"Twain.RestrictSources=1\n"
			+"Twain.AllowedSources=scanner one|sensor source\n"
			+"Twain.JournalKeep=200\n"
			+"Twain.DisableIsSafeSources=sensor source\n"
			+"Twain.SafeDisableCap=0x8002\n"
			+"Twain.RefusedDisableCodes=0x8001|0x8002\n"
			+"Device.SessionMode=perslot\n"
			+"Device.Bits=16\n"
			+"Device.ByteOrder16=le\n"
			+"Device.Polarity=asdelivered\n"
			+"Device.Headless=1\n"
			+"Device.DisableIsSafe=1\n"
			+"Device.SessionTimeoutMinutes=15\n"
			+"Device.SafeDisableCap=0x8002\n"
			+"Device.RefusedDisableCodes=0x8001|0x8002\n"
			+"Device.ContinuousCap=\n"
			+"Device.RedeliverCap=0x8003\n"
			+"Device.NoActivateCap=0x8004\n";

		[TestCleanup]
		public void Cleanup() {
			foreach(string path in _listPaths) {
				try {
					File.Delete(path);
				}
				catch(Exception) {
				}
			}
		}

		private LocalCaptureConfig Load(string iniText) {
			string path=Path.Combine(Path.GetTempPath(),"GatedSettingsHashTests_"+Guid.NewGuid().ToString("N")+".ini");
			_listPaths.Add(path);
			File.WriteAllText(path,iniText,new UTF8Encoding(false));
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.AreEqual("",config.LastError);
			return config;
		}

		private string Hash(string iniText,long imagingDeviceNum=12) {
			return GatedSettingsHash.Compute(Load(iniText),imagingDeviceNum);
		}

		[TestMethod]
		public void CanonicalText_IsPinned() {
			Assert.AreEqual(CanonicalBase,GatedSettingsHash.GetCanonicalText(Load(IniBase),12));
		}

		[TestMethod]
		public void Hash_IsSha256OfTheCanonicalText() {
			string hash=Hash(IniBase);
			byte[] bytesExpected;
			using(SHA256 sha256=SHA256.Create()) {
				bytesExpected=sha256.ComputeHash(Encoding.UTF8.GetBytes(CanonicalBase));
			}
			Assert.AreEqual(BitConverter.ToString(bytesExpected).Replace("-","").ToLowerInvariant(),hash);
			Assert.AreEqual(64,hash.Length);
			Assert.IsTrue(hash.All(x => (x>='0' && x<='9') || (x>='a' && x<='f')));
			Assert.AreEqual(hash,GatedSettingsHash.ComputeFromCanonicalText(CanonicalBase));
			//A known answer for the empty text pins the encoding (SHA-256 of zero bytes).
			Assert.AreEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",GatedSettingsHash.ComputeFromCanonicalText(""));
		}

		[TestMethod]
		public void EquivalentFiles_HashAlike() {
			string hashBase=Hash(IniBase);
			//Sections and keys in another order, names in other cases, spaces around '=' and inside lists, comments, values in other
			//cases, empty list entries, repeated keys (the last wins), CRLF line ends.
			string iniEquivalent=@"; hand-written
[CAMERA]
Name=Other camera
[device.12]
noactivatecap = 0X8004   ; gated
REDELIVERCAP=0x8003
ContinuousCap=
RefusedDisableCodes= 0x8001 || 0x8002 |
SafeDisableCap=0x8002
SessionTimeoutMinutes=15
DisableIsSafe=1
Headless=1
Polarity=ASDELIVERED
ByteOrder16=le
Bits=8
Bits=16
SessionMode=perslot
[twain]
RefusedDisableCodes=0x8001|0x8002
SafeDisableCap=0x8002
DisableIsSafeSources=  Sensor   Source
JournalKeep=200
AllowedSources=SCANNER ONE | Sensor Source
RestrictSources=1
Allow16Bit=1
AcquireTimeoutMinutes=12
ThreadModal=1
[session]
idledisableminutes=8
enabled=1
[Device.13]
Bits=16
Headless=0
".Replace("\r\n","\n").Replace("\n","\r\n");
			Assert.AreEqual(GatedSettingsHash.GetCanonicalText(Load(IniBase),12),GatedSettingsHash.GetCanonicalText(Load(iniEquivalent),12));
			Assert.AreEqual(hashBase,Hash(iniEquivalent));
		}

		[TestMethod]
		public void UngatedKeysAndOtherDevices_DoNotChangeTheHash() {
			string hashBase=Hash(IniBase);
			Assert.AreEqual(hashBase,Hash(IniBase.Replace("Moniker=@device:pnp:x","Moniker=@device:pnp:y")));
			Assert.AreEqual(hashBase,Hash(IniBase.Replace("Retake=F5","Retake=F6")));
			Assert.AreEqual(hashBase,Hash(IniBase.Replace("[Device.13]\r\nBits=8","[Device.13]\r\nBits=16").Replace("[Device.13]\nBits=8","[Device.13]\nBits=16")));
			Assert.AreEqual(hashBase,Hash(IniBase+"[Sounds]\nSaved=SystemHand\n[Twain]\nNotAGatedKey=1\n[Device.12]\nAnotherKey=x\n"));
			Assert.AreEqual(hashBase,Hash(IniBase.Replace("Version=1","Version=2")));
		}

		[TestMethod]
		public void EveryGatedKey_ChangesTheHash() {
			string hashBase=Hash(IniBase);
			HashSet<string> hashSetHashes=new HashSet<string> { hashBase };
			List<Tuple<string,string>> listKeys=GatedSettingsHash.ListSessionKeys.Select(x => Tuple.Create("Session",x))
				.Concat(GatedSettingsHash.ListTwainKeys.Select(x => Tuple.Create("Twain",x)))
				.Concat(GatedSettingsHash.ListDeviceKeys.Select(x => Tuple.Create("Device.12",x)))
				.ToList();
			Assert.AreEqual(2+9+12,listKeys.Count,"Design 9.3: two [Session], nine [Twain] and twelve [Device.N] gated keys.");
			foreach(Tuple<string,string> tuple in listKeys) {
				Assert.IsTrue(GatedSettingsHash.IsGatedKey(tuple.Item1,tuple.Item2));
				LocalCaptureConfig config=Load(IniBase);
				Assert.IsTrue(config.Set(tuple.Item1,tuple.Item2,"changed value"));
				string hash=GatedSettingsHash.Compute(config,12);
				Assert.IsTrue(hashSetHashes.Add(hash),"Changing "+tuple.Item1+" "+tuple.Item2+" changes the hash, uniquely.");
			}
			//Values, list order and list membership are meaning.
			Assert.AreNotEqual(hashBase,Hash(IniBase.Replace("Bits=16","Bits=8")));
			Assert.AreNotEqual(hashBase,Hash(IniBase.Replace("AllowedSources=Scanner One|Sensor Source","AllowedSources=Sensor Source|Scanner One")));
			Assert.AreNotEqual(hashBase,Hash(IniBase.Replace("AllowedSources=Scanner One|Sensor Source","AllowedSources=Scanner One")));
			Assert.AreNotEqual(hashBase,Hash(IniBase.Replace("AllowedSources=Scanner One|Sensor Source","AllowedSources=ScannerOne|Sensor Source")));
		}

		[TestMethod]
		public void DeviceNumber_IsPartOfTheHash() {
			string iniSameValues=IniBase.Replace("[Device.13]","[Device.99]").Replace("[Device.12]","[Device.13]");
			Assert.AreNotEqual(Hash(IniBase,12),Hash(iniSameValues,13),"The same values under another device hash differently.");
			Assert.AreEqual(GatedSettingsHash.GetCanonicalText(Load(IniBase),12).Replace("Device=12","Device=13"),
				GatedSettingsHash.GetCanonicalText(Load(iniSameValues),13));
			TestUtil.AssertThrows<ArgumentOutOfRangeException>(() => GatedSettingsHash.Compute(Load(IniBase),0));
		}

		[TestMethod]
		public void MissingKeysAndFiles_HashAsEmptyValues() {
			string pathMissing=Path.Combine(Path.GetTempPath(),"GatedSettingsHashTests_missing_"+Guid.NewGuid().ToString("N")+".ini");
			string hashMissingFile=GatedSettingsHash.Compute(LocalCaptureConfig.Load(pathMissing),12);
			Assert.AreEqual(hashMissingFile,GatedSettingsHash.Compute(null,12));
			Assert.AreEqual(hashMissingFile,Hash("[Camera]\nName=x\n"));
			Assert.AreEqual(hashMissingFile,Hash("[Session]\nEnabled=\n[Twain]\nAllowedSources=| |\n[Device.12]\nBits= \n"),
				"A missing key and an empty value both read as the default.");
			Assert.AreNotEqual(hashMissingFile,Hash("[Device.12]\nBits=8\n"),"An explicit value is not the same text as a missing one.");
			string canonical=GatedSettingsHash.GetCanonicalText(null,12);
			Assert.IsTrue(canonical.StartsWith("capture-gated-settings/1\nDevice=12\nSession.Enabled=\n"));
			Assert.AreEqual(2+2+9+12,canonical.Count(x => x=='\n'));
			Assert.IsFalse(File.Exists(pathMissing),"Reading never creates the file.");
		}

		[TestMethod]
		public void IsGatedKey_CoversOnlyTheDesignList() {
			Assert.IsTrue(GatedSettingsHash.IsGatedKey("session","enabled"));
			Assert.IsTrue(GatedSettingsHash.IsGatedKey(" Twain ","AllowedSources"));
			Assert.IsTrue(GatedSettingsHash.IsGatedKey("Device.7","Polarity"));
			Assert.IsFalse(GatedSettingsHash.IsGatedKey("Twain","SessionMode"));
			Assert.IsFalse(GatedSettingsHash.IsGatedKey("Device.7","ThreadModal"));
			Assert.IsFalse(GatedSettingsHash.IsGatedKey("Camera","Moniker"));
			Assert.IsFalse(GatedSettingsHash.IsGatedKey("Keys","Confirm"));
			Assert.IsFalse(GatedSettingsHash.IsGatedKey("General","Version"));
			Assert.IsFalse(GatedSettingsHash.IsGatedKey(null,"Bits"));
		}
	}
}
