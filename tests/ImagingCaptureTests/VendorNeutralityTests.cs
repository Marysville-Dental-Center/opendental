// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ImagingCapture.Camera;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>Design 4 (Provenance, Vendor neutrality) and 13 (guard tests): the capture code, its tests and the capture docs name no
	///vendor or device, and trigger and level values have no defaults.
	///
	///The deny-list holds SHA-256 hashes, not names, so that this repository names no vendor even here. A name is hashed after it is
	///lowercased and stripped of everything but ASCII letters and digits ("Ab-Cd Ef" becomes "abcdef"). Text is checked the same
	///way: every word, every camelCase part, and every run of two or three parts joined by camelCase or by one space, '-' or '_'.
	///To add a name, append the hash printed by this PowerShell line, with a comment that describes the entry without naming it:
	///  [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes('name'))).Replace('-','').ToLower()
	///
	///An occurrence the code must keep, such as a compatibility value, goes in VendorNeutralityAllowlist.txt as "path hash".</summary>
	[TestClass]
	public class VendorNeutralityTests {
		///<summary>Vendor, product and project names from the design's background and provenance sections (design 2.4, 4, 8.4) and
		///the vendors of the old sensor paths it mentions.</summary>
		private static readonly string[] _arrayDenyHashes={
			"5147a6de6c26ca2472e0b20e803a1ba7fe6643775294aa714b6263bf176c37d1",//the clinic's sensor and camera vendor, abbreviated
			"221fe1744c5b7b3825b3349507228ef5d9fb8fcf308060be913d977e3219b7b9",//that vendor's imaging software
			"5a2bb24dc707f2a69361994eed72ae17d218224abc13d487b9e89c3cd5873640",//the sensor's manufacturer
			"62b0e738b893107d15cd31ff7ebf3c6a7f692a1f8e28742d5dada2a8c662d4c4",//the private TWAIN source project
			"25b0556df8a5c445c8a26f3f4bf211056441c83534b88a790f972091101c009a",//the sensor product
			"01c9a6197c8a1fee00c4219853304f7642e59b57893e64aa117d19ef8b8aa9e5",//the private TWAIN source's file name
			"600384419727215b7cd4220e9e67bd8d1bf2268105acc1bd7cc8773bb285609c",//the detector's manufacturer
			"c48b4df565b0c96f84fedf18f26596ed40aa9f46f11021af7125d34d1d3acffe",//the detector product
			"9ab02deb2a807028c38430817e805bc87826c38003f0b53ba7953be0ff797917",//the detector product, other form
			"e35e8fd3cdd2e839e72be5b45bc0d88bec5151b0270abdff2301408ee02770d1",//the old module's direct sensor vendor
			"0c6947fda12f6b9a18b81f7c46cdbe0130e2d329d1708487a49f867fc2c1c47e",//the vendor of an unsupported imaging device type
		};
		///<summary>Folders scanned, from the repository root. Missing ones are skipped.</summary>
		private static readonly string[] _arrayFolders={ "ImagingCapture","ImagingDeviceManager",@"docs\imaging",@"tests\ImagingCaptureTests",
			@"tests\ImagingDeviceManagerTests" };
		///<summary>Single files scanned when present: the capture hook partial (design 13), the Video window built on ImagingCapture, the
		///capture design itself, whose background and provenance sections describe the clinic's hardware by role only, and the track's port note.</summary>
		private static readonly string[] _arrayFiles={ @"OpenDental\Main Modules\ControlImages.Acquire.cs",@"OpenDental\Forms\FormVideo.cs",
			@"OpenDental\Forms\FormVideo.designer.cs",@"docs\imaging-capture-design.md",@"docs\port-notes\CAP.md" };
		private static readonly string[] _arrayFoldersSkipped={ "bin","obj","TestResults",".vs" };
		private static readonly string[] _arrayExtensionsBinary={ ".dll",".exe",".pdb",".png",".jpg",".jpeg",".gif",".bmp",".ico",".tif",".tiff",
			".dcm",".zip",".cache" };
		private const string AllowlistFileName="VendorNeutralityAllowlist.txt";

		///<summary>One denied name found in a file.</summary>
		internal sealed class VendorHit {
			public string Path;
			public int Line;
			public string Text;
			public string Hash;

			public override string ToString() {
				return Path+":"+Line+" ("+Text+")";
			}
		}

		#region Tests
		[TestMethod]
		public void DenyList_IsWellFormed() {
			Assert.IsTrue(_arrayDenyHashes.Length>=10);
			Assert.AreEqual(_arrayDenyHashes.Length,_arrayDenyHashes.Distinct().Count(),"No duplicates.");
			foreach(string hash in _arrayDenyHashes) {
				Assert.IsTrue(hash.Length==64 && hash.All(x => (x>='0' && x<='9') || (x>='a' && x<='f')),"Not a lowercase SHA-256: "+hash);
			}
		}

		[TestMethod]
		public void Scanner_FindsWordsCamelCasePartsAndJoinedRuns() {
			//Made-up names, hashed here, stand in for the deny-list.
			HashSet<string> hashSetDeny=new HashSet<string> { Hash("zqvendor"),Hash("zqsensor"),Hash("zqdentalco"),Hash("zq") };
			List<VendorHit> listHits=Scan("a\r\nusing ZqVendorTwain; //the zq-Sensor\nby Zq Dental Co.\nIsZqCamera\n",hashSetDeny,"x.cs");
			List<string> listFound=listHits.Select(x => x.Line+":"+x.Text).ToList();
			CollectionAssert.IsSubsetOf(new[] { "2:zqvendor","2:zqsensor","3:zqdentalco","4:zq" },listFound,string.Join(", ",listFound));
			Assert.AreEqual(0,Scan("zqvendors notzqsensor zq.sensor Zq\tSensor zqvendor2",new HashSet<string> { Hash("zqvendor"),Hash("zqsensor") },"y.cs").Count,
				"Longer words, '.' and tab do not join.");
		}

		[TestMethod]
		public void CaptureSources_NameNoVendor() {
			List<string> listViolations=GetHitsNotAllowed();
			Assert.AreEqual(0,listViolations.Count,"Vendor or device names in capture sources: "+string.Join("; ",listViolations));
		}

		[TestMethod]
		public void Allowlist_EveryEntryIsStillNeeded() {
			List<VendorHit> listHits=ScanAll();
			foreach(Tuple<string,string> tuple in ReadAllowlist()) {
				Assert.IsTrue(_arrayDenyHashes.Contains(tuple.Item2),"The allowlist entry is not on the deny-list: "+tuple.Item1+" "+tuple.Item2);
				Assert.IsTrue(listHits.Any(x => string.Equals(x.Path,tuple.Item1,StringComparison.OrdinalIgnoreCase) && x.Hash==tuple.Item2),
					"The allowlist entry no longer matches anything; remove it: "+tuple.Item1+" "+tuple.Item2);
			}
		}

		[TestMethod]
		public void TriggerAndLevelDefaults_AreEmpty() {
			string path=Path.Combine(Path.GetTempPath(),"ImagingCaptureTests_"+Guid.NewGuid().ToString("N")+".ini");
			foreach(CameraConfig cameraConfig in new[] { new CameraConfig(),CameraConfig.Read(null),CameraConfig.Read(LocalCaptureConfig.Load(path)) }) {
				Assert.IsFalse(cameraConfig.StillEnabled);
				Assert.IsFalse(cameraConfig.ControlPoll.Enabled);
				Assert.IsNull(cameraConfig.ControlPoll.Interface);
				Assert.IsNull(cameraConfig.ControlPoll.Property);
				Assert.IsNull(cameraConfig.ControlPoll.Mask);
				Assert.IsNull(cameraConfig.ControlPoll.ActiveLevel);
				Assert.IsNull(cameraConfig.DebounceMs);
				Assert.AreEqual(0,cameraConfig.Levels.Count);
				Assert.AreEqual("",cameraConfig.Moniker);
				Assert.AreEqual("",cameraConfig.Name);
			}
			Assert.IsFalse(File.Exists(path),"Reading never creates the file.");
		}
		#endregion Tests

		#region Scanning
		private static List<string> GetHitsNotAllowed() {
			List<Tuple<string,string>> listAllowed=ReadAllowlist();
			return ScanAll()
				.Where(x => !listAllowed.Any(y => string.Equals(y.Item1,x.Path,StringComparison.OrdinalIgnoreCase) && y.Item2==x.Hash))
				.Select(x => x.ToString())
				.ToList();
		}

		private static string FindRepositoryRoot() {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				if(File.Exists(Path.Combine(directoryInfo.FullName,"ImagingCapture","xImagingCapture.csproj"))) {
					return directoryInfo.FullName;
				}
				directoryInfo=directoryInfo.Parent;
			}
			Assert.Inconclusive("The repository root was not found above "+AppDomain.CurrentDomain.BaseDirectory);
			return null;
		}

		///<summary>"path hash" pairs from the allowlist file, paths with '/'.</summary>
		private static List<Tuple<string,string>> ReadAllowlist() {
			string pathAllowlist=Path.Combine(FindRepositoryRoot(),"tests","ImagingCaptureTests",AllowlistFileName);
			Assert.IsTrue(File.Exists(pathAllowlist),"Missing "+pathAllowlist);
			List<Tuple<string,string>> listAllowed=new List<Tuple<string,string>>();
			foreach(string line in File.ReadAllLines(pathAllowlist)) {
				string trimmed=line.Trim();
				if(trimmed=="" || trimmed.StartsWith("#")) {
					continue;
				}
				string[] arrayParts=trimmed.Split(new[] { ' ','\t' },StringSplitOptions.RemoveEmptyEntries);
				Assert.IsTrue(arrayParts.Length>=2,"An allowlist line needs a path and a hash: "+line);
				listAllowed.Add(Tuple.Create(arrayParts[0].Replace('\\','/'),arrayParts[1].ToLowerInvariant()));
			}
			return listAllowed;
		}

		private static List<VendorHit> ScanAll() {
			string root=FindRepositoryRoot();
			HashSet<string> hashSetDeny=new HashSet<string>(_arrayDenyHashes);
			List<string> listPaths=new List<string>();
			foreach(string folder in _arrayFolders) {
				string pathFolder=Path.Combine(root,folder);
				if(Directory.Exists(pathFolder)) {
					listPaths.AddRange(GetFiles(pathFolder));
				}
			}
			foreach(string file in _arrayFiles) {
				string pathFile=Path.Combine(root,file);
				if(File.Exists(pathFile)) {
					listPaths.Add(pathFile);
				}
			}
			Assert.IsTrue(listPaths.Count>50,"Too few files found under "+root);
			List<VendorHit> listHits=new List<VendorHit>();
			foreach(string path in listPaths) {
				byte[] bytes=File.ReadAllBytes(path);
				if(Array.IndexOf(bytes,(byte)0,0,Math.Min(bytes.Length,8000))>=0) {
					continue;//binary
				}
				string pathRelative=path.Substring(root.Length).TrimStart('\\','/').Replace('\\','/');
				listHits.AddRange(Scan(Encoding.UTF8.GetString(bytes),hashSetDeny,pathRelative));
			}
			return listHits;
		}

		private static IEnumerable<string> GetFiles(string folder) {
			foreach(string path in Directory.GetFiles(folder)) {
				if(!_arrayExtensionsBinary.Contains(Path.GetExtension(path).ToLowerInvariant())) {
					yield return path;
				}
			}
			foreach(string pathSub in Directory.GetDirectories(folder)) {
				if(_arrayFoldersSkipped.Contains(Path.GetFileName(pathSub),StringComparer.OrdinalIgnoreCase)) {
					continue;
				}
				foreach(string path in GetFiles(pathSub)) {
					yield return path;
				}
			}
		}

		///<summary>The denied names in text, one hit per name per line.</summary>
		internal static List<VendorHit> Scan(string text,HashSet<string> hashSetDeny,string pathRelative) {
			List<VendorHit> listHits=new List<VendorHit>();
			Dictionary<string,string> dictHashes=new Dictionary<string,string>();
			string[] arrayLines=text.Split('\n');
			for(int idxLine=0;idxLine<arrayLines.Length;idxLine++) {
				string line=arrayLines[idxLine];
				List<string> listCandidates=new List<string>();
				List<string> listParts=new List<string>();
				List<bool> listJoined=new List<bool>();
				int idx=0;
				int idxEndPrev=-1;
				while(idx<line.Length) {
					if(!IsAsciiLetterOrDigit(line[idx])) {
						idx++;
						continue;
					}
					int idxStart=idx;
					while(idx<line.Length && IsAsciiLetterOrDigit(line[idx])) {
						idx++;
					}
					string word=line.Substring(idxStart,idx-idxStart);
					bool isJoinedToPrevious=idxEndPrev>=0 && idxStart-idxEndPrev==1 && " -_".IndexOf(line[idxEndPrev])>=0;
					listCandidates.Add(word.ToLowerInvariant());
					List<string> listWordParts=SplitCamelCase(word);
					for(int k=0;k<listWordParts.Count;k++) {
						listParts.Add(listWordParts[k].ToLowerInvariant());
						listJoined.Add(k==0 ? isJoinedToPrevious : true);
					}
					idxEndPrev=idx;
				}
				for(int i=0;i<listParts.Count;i++) {
					string candidate="";
					for(int n=0;n<3 && i+n<listParts.Count;n++) {
						if(n>0 && !listJoined[i+n]) {
							break;
						}
						candidate+=listParts[i+n];
						listCandidates.Add(candidate);
					}
				}
				HashSet<string> hashSetLineHits=new HashSet<string>();
				foreach(string candidate in listCandidates) {
					string hash;
					if(!dictHashes.TryGetValue(candidate,out hash)) {
						hash=Hash(candidate);
						dictHashes[candidate]=hash;
					}
					if(hashSetDeny.Contains(hash) && hashSetLineHits.Add(hash)) {
						listHits.Add(new VendorHit { Path=pathRelative,Line=idxLine+1,Text=candidate,Hash=hash });
					}
				}
			}
			return listHits;
		}

		///<summary>Splits a word where a lowercase letter or digit meets an uppercase one, and before the last capital of a run of
		///capitals that starts a new word: "XMLHttpRequest2" gives "XML", "Http" and "Request2".</summary>
		private static List<string> SplitCamelCase(string word) {
			List<string> listParts=new List<string>();
			int idxStart=0;
			for(int i=1;i<word.Length;i++) {
				char cPrev=word[i-1];
				char c=word[i];
				bool isNextLower=i+1<word.Length && char.IsLower(word[i+1]);
				if(((char.IsLower(cPrev) || char.IsDigit(cPrev)) && char.IsUpper(c)) || (char.IsUpper(cPrev) && char.IsUpper(c) && isNextLower)) {
					listParts.Add(word.Substring(idxStart,i-idxStart));
					idxStart=i;
				}
			}
			listParts.Add(word.Substring(idxStart));
			return listParts;
		}

		private static bool IsAsciiLetterOrDigit(char c) {
			return (c>='a' && c<='z') || (c>='A' && c<='Z') || (c>='0' && c<='9');
		}

		private static string Hash(string text) {
			using(SHA256 sha256=SHA256.Create()) {
				byte[] bytes=sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
				StringBuilder stringBuilder=new StringBuilder(64);
				foreach(byte b in bytes) {
					stringBuilder.Append(b.ToString("x2"));
				}
				return stringBuilder.ToString();
			}
		}
		#endregion Scanning
	}
}
