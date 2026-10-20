// Publishing a fork release to the database and pulling it on the other workstations (decoupling plan Phase 4, backlog Risk R1).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeBase;
using Ionic.Zip;

namespace OpenDentBusiness {
	///<summary>Which build is running or was published: the release version ("0.0") and the build id.</summary>
	[Serializable]
	public class ReleaseIdentity {
		public string ReleaseVersion="";
		public string BuildId="";

		public override string ToString() {
			return ReleaseVersion+" (build "+(BuildId.Length>12?BuildId.Substring(0,12):BuildId)+")";
		}
	}

	public enum ReleaseAction {
		///<summary>Nothing to do: this workstation runs the published build, or nothing applies.</summary>
		None,
		///<summary>This is the update server and it runs a build that is not published yet (same release or newer): store its binaries.</summary>
		Publish,
		///<summary>A different build of the same or a newer release is published: update this workstation from it.</summary>
		Pull,
		///<summary>This workstation runs a newer release than the one published and is not the update server. Refused; no downgrade is offered.</summary>
		RefuseNewerThanPublished,
	}

	public class ReleaseDecision {
		public ReleaseAction Action;
		public string Message="";
	}

	public class ReleaseDownloadResult {
		public bool IsOk;
		public ReleaseManifest Manifest;
		public List<string> ListProblems=new List<string>();
	}

	///<summary>The fork's own release channel. The update server stores its binaries in documentmisc rows of the fork-only types
	///ForkReleaseManifest and ForkReleaseSegment, with a manifest that names our product and the SHA-256 of the archive and of every file. Other
	///workstations pull that release on startup through UpdateFileCopier. Open Dental's UpdateFiles rows are never read, and there is never a
	///downgrade prompt. See docs/release-process.md.</summary>
	public class ForkReleases {
		///<summary>Bytes of archive per segment row. 786,432 bytes is 1MB of base64, well under the default max_allowed_packet.</summary>
		public const int SEGMENT_BYTES=786432;
		///<summary>Sub folders of the installation folder that are published too (not recursive), the same ones UpdateFileCopier copies.</summary>
		public static readonly List<string> ListSpecialDirs=new List<string>() {
			"Sparks3D",
			"OpenDentalReplicationService",
		};
		///<summary>The program's own assemblies. The build id is the SHA-256 over their names and SHA-256 values, so any rebuilt assembly gives a new
		///build id while third-party files are not hashed at every startup.</summary>
		public static readonly List<string> ListBuildIdFiles=new List<string>() {
			"OpenDental.exe",
			"OpenDentBusiness.dll",
			"CodeBase.dll",
			"DataConnectionBase.dll",
			"WpfControlsOD.dll",
			"OpenDentalGraph.dll",
			"OpenDentalCloud.dll",
			"SparksToothChart.dll",
			"ODCrypt.dll",
			"PasswordVaultWrapper.dll",
			"UpdateFileCopier.exe",
		};
		private static ReleaseIdentity _releaseIdentityRunning;

		#region Identity
		///<summary>The build id of the files in the folder (see ListBuildIdFiles). Lower-case hex.</summary>
		public static string ComputeBuildId(string folder) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(string fileName in ListBuildIdFiles.OrderBy(x => x,StringComparer.OrdinalIgnoreCase)) {
				string path=Path.Combine(folder,fileName);
				if(!File.Exists(path)) {
					continue;
				}
				stringBuilder.Append(fileName.ToLowerInvariant()+"|"+ReleaseManifest.ComputeSha256File(path)+"\n");
			}
			return ReleaseManifest.ComputeSha256(Encoding.UTF8.GetBytes(stringBuilder.ToString()));
		}

		///<summary>The running program's identity: release version from OpenDental.exe and build id of the installation folder. Computed once.</summary>
		public static ReleaseIdentity GetRunningIdentity() {
			if(_releaseIdentityRunning==null) {
				ReleaseIdentity releaseIdentity=new ReleaseIdentity();
				releaseIdentity.ReleaseVersion=ForkVersions.GetReleaseVersion();
				releaseIdentity.BuildId=ComputeBuildId(AppDomain.CurrentDomain.BaseDirectory);
				_releaseIdentityRunning=releaseIdentity;
			}
			return _releaseIdentityRunning;
		}

		///<summary>The ForkProgramVersion marker value for a release: "releaseVersion+buildId".</summary>
		public static string FormatProgramVersion(ReleaseIdentity releaseIdentity) {
			return releaseIdentity.ReleaseVersion+"+"+releaseIdentity.BuildId;
		}

		///<summary>Parses a ForkProgramVersion marker value. Returns null when blank or malformed.</summary>
		public static ReleaseIdentity ParseProgramVersion(string value) {
			if(string.IsNullOrWhiteSpace(value)) {
				return null;
			}
			int indexPlus=value.IndexOf('+');
			if(indexPlus<=0 || indexPlus==value.Length-1) {
				return null;
			}
			ReleaseIdentity releaseIdentity=new ReleaseIdentity();
			releaseIdentity.ReleaseVersion=value.Substring(0,indexPlus);
			releaseIdentity.BuildId=value.Substring(indexPlus+1);
			return releaseIdentity;
		}
		#endregion Identity

		#region Decision
		///<summary>What this workstation does with the published release. No case ever offers a downgrade: a workstation whose release is newer than
		///the published one is refused (or, on the update server, publishes), and an older or equal release with a different build pulls.
		///Debug builds never publish or pull; the version guard still runs for them.</summary>
		public static ReleaseDecision Decide(ReleaseManifest releaseManifestPublished,ReleaseIdentity releaseIdentityRunning,bool isUpdateServer,
			bool isDebugBuild,string updateServerName="")
		{
			ReleaseDecision releaseDecision=new ReleaseDecision();
			if(isDebugBuild) {
				releaseDecision.Message="Debug builds do not publish or pull releases.";
				return releaseDecision;
			}
			if(releaseManifestPublished==null) {
				if(isUpdateServer) {
					releaseDecision.Action=ReleaseAction.Publish;
					releaseDecision.Message="No release is published yet.";
				}
				return releaseDecision;
			}
			if(releaseManifestPublished.BuildId==releaseIdentityRunning.BuildId) {
				return releaseDecision;
			}
			int compare=ForkVersions.CompareReleaseVersions(releaseManifestPublished.ReleaseVersion,releaseIdentityRunning.ReleaseVersion);
			if(isUpdateServer) {
				if(compare<=0) {
					releaseDecision.Action=ReleaseAction.Publish;
					releaseDecision.Message="This build is not the published one ("+Describe(releaseManifestPublished)+").";
				}
				else {
					releaseDecision.Action=ReleaseAction.Pull;
					releaseDecision.Message="A newer release is published ("+Describe(releaseManifestPublished)+").";
				}
				return releaseDecision;
			}
			if(compare>=0) {
				releaseDecision.Action=ReleaseAction.Pull;
				releaseDecision.Message="The update server published "+Describe(releaseManifestPublished)+".";
				return releaseDecision;
			}
			releaseDecision.Action=ReleaseAction.RefuseNewerThanPublished;
			releaseDecision.Message="This workstation runs release "+releaseIdentityRunning+", which is newer than the published release "
				+Describe(releaseManifestPublished)+".\r\n"
				+"Run the new release on the update server"+(string.IsNullOrEmpty(updateServerName)?"":" ("+updateServerName+")")
				+" first so it is published, or install the published release on this workstation.";
			return releaseDecision;
		}

		private static string Describe(ReleaseManifest releaseManifest) {
			ReleaseIdentity releaseIdentity=new ReleaseIdentity();
			releaseIdentity.ReleaseVersion=releaseManifest.ReleaseVersion;
			releaseIdentity.BuildId=releaseManifest.BuildId;
			return releaseIdentity.ToString();
		}
		#endregion Decision

		#region Publish
		///<summary>True for files of the installation folder that are published. Local settings, logs and files a plugin marks with a double underscore
		///stay local (the same rules Open Dental used for its update files).</summary>
		public static bool ShouldPublishFile(string fileName) {
			if(fileName.In("FreeDentalConfig.xml","OpenDentalServerConfig.xml","ProximitySettings.txt","OpenDentalReplicationServiceConfig.xml",
				ReleaseManifest.FILE_NAME,"Manifest.txt"))
			{
				return false;
			}
			if(fileName.StartsWith("openlog",StringComparison.OrdinalIgnoreCase) || fileName.Contains("__")) {
				return false;
			}
			return true;
		}

		///<summary>Lists the files of the installation folder that a release contains, as paths relative to the folder.</summary>
		public static List<string> GetReleaseFiles(string folder) {
			List<string> listRelative=new List<string>();
			foreach(string path in Directory.GetFiles(folder)) {
				string name=Path.GetFileName(path);
				if(ShouldPublishFile(name)) {
					listRelative.Add(name);
				}
			}
			foreach(string specialDir in ListSpecialDirs) {
				string folderSpecial=Path.Combine(folder,specialDir);
				if(!Directory.Exists(folderSpecial)) {
					continue;
				}
				foreach(string path in Directory.GetFiles(folderSpecial)) {
					string name=Path.GetFileName(path);
					if(ShouldPublishFile(name)) {
						listRelative.Add(specialDir+"\\"+name);
					}
				}
			}
			return listRelative.OrderBy(x => x,StringComparer.OrdinalIgnoreCase).ToList();
		}

		///<summary>Builds the manifest and zipped archive for the files of the folder. The archive holds the files and a copy of the manifest without
		///the archive hash; the returned manifest has it.</summary>
		public static ReleaseManifest BuildRelease(string folder,ReleaseIdentity releaseIdentity,string publishedBy,DateTime dateTimeUtc,
			out byte[] arrayArchive)
		{
			ReleaseManifest releaseManifest=new ReleaseManifest();
			releaseManifest.ReleaseVersion=releaseIdentity.ReleaseVersion;
			releaseManifest.BuildId=releaseIdentity.BuildId;
			releaseManifest.CompatVersion=ForkVersions.CompatVersion.ToString(4);
			releaseManifest.PublishedUtc=new DateTime(dateTimeUtc.Year,dateTimeUtc.Month,dateTimeUtc.Day,dateTimeUtc.Hour,dateTimeUtc.Minute,
				dateTimeUtc.Second,DateTimeKind.Utc);
			releaseManifest.PublishedBy=publishedBy??"";
			using(ZipFile zipFile=new ZipFile()) {
				foreach(string relative in GetReleaseFiles(folder)) {
					string path=Path.Combine(folder,relative);
					byte[] arrayBytes=File.ReadAllBytes(path);//Read once, so the hash and the archive hold the same bytes.
					ReleaseManifestFile releaseManifestFile=new ReleaseManifestFile();
					releaseManifestFile.RelativePath=relative;
					releaseManifestFile.Size=arrayBytes.LongLength;
					releaseManifestFile.Sha256=ReleaseManifest.ComputeSha256(arrayBytes);
					releaseManifest.ListFiles.Add(releaseManifestFile);
					zipFile.AddEntry(relative.Replace('\\','/'),arrayBytes);
				}
				zipFile.AddEntry(ReleaseManifest.FILE_NAME,Encoding.UTF8.GetBytes(releaseManifest.Serialize(includeArchiveHash:false)));
				using(MemoryStream memoryStream=new MemoryStream()) {
					zipFile.Save(memoryStream);
					arrayArchive=memoryStream.ToArray();
				}
			}
			releaseManifest.ArchiveSha256=ReleaseManifest.ComputeSha256(arrayArchive);
			return releaseManifest;
		}

		///<summary>Publishes the files of the folder as the current release: stores the archive segments and the manifest, then sets the
		///ForkProgramVersion marker. Direct connection only. Callers set UpdateInProgressOnComputerName around it so other workstations wait.</summary>
		public static ReleaseManifest Publish(string folder,ReleaseIdentity releaseIdentity,string publishedBy,DateTime? dateTimeUtc=null) {
			Meth.NoCheckMiddleTierRole();
			byte[] arrayArchive;
			ReleaseManifest releaseManifest=BuildRelease(folder,releaseIdentity,publishedBy,dateTimeUtc??DateTime.UtcNow,out arrayArchive);
			List<string> listSegments=new List<string>();
			for(int offset=0;offset<arrayArchive.Length;offset+=SEGMENT_BYTES) {
				int length=Math.Min(SEGMENT_BYTES,arrayArchive.Length-offset);
				listSegments.Add(Convert.ToBase64String(arrayArchive,offset,length));
			}
			DocumentMiscs.ReplaceForkRelease(Convert.ToBase64String(Encoding.UTF8.GetBytes(releaseManifest.Serialize())),listSegments);
			ForkVersions.SetMarker(ForkVersions.PREF_FORK_PROGRAM_VERSION,FormatProgramVersion(releaseIdentity));
			return releaseManifest;
		}
		#endregion Publish

		#region Pull
		///<summary>The published release manifest, or null when none is published. A manifest that does not parse or names another product is
		///treated as no release; error says why.</summary>
		public static ReleaseManifest GetPublishedManifest(out string error) {
			error="";
			DocumentMisc documentMisc=DocumentMiscs.GetForkReleaseManifest();
			if(documentMisc==null) {
				return null;
			}
			ReleaseManifest releaseManifest;
			try {
				releaseManifest=ReleaseManifest.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(documentMisc.RawBase64??"")));
			}
			catch(Exception ex) {
				error="The published release manifest is not readable: "+ex.Message;
				return null;
			}
			if(!releaseManifest.IsOurProduct()) {
				error="The published release manifest is for another product ("+releaseManifest.Product+").";
				return null;
			}
			return releaseManifest;
		}

		///<summary>Downloads the published release into the empty or missing folder and checks it: product name, archive SHA-256, only listed files,
		///safe paths, the archive's own manifest equal to the published one, and every file's size and SHA-256. On any problem IsOk is false and the
		///folder must not be used.</summary>
		public static ReleaseDownloadResult DownloadAndVerify(string folderDest) {
			ReleaseDownloadResult releaseDownloadResult=new ReleaseDownloadResult();
			ReleaseManifest releaseManifest=GetPublishedManifest(out string error);
			if(releaseManifest==null) {
				releaseDownloadResult.ListProblems.Add(error==""?"No release is published.":error);
				return releaseDownloadResult;
			}
			releaseDownloadResult.Manifest=releaseManifest;
			List<DocumentMisc> listSegments=DocumentMiscs.GetForkReleaseSegments();
			byte[] arrayArchive;
			try {
				using(MemoryStream memoryStream=new MemoryStream()) {
					foreach(DocumentMisc documentMisc in listSegments) {
						byte[] arrayBytes=Convert.FromBase64String(documentMisc.RawBase64??"");
						memoryStream.Write(arrayBytes,0,arrayBytes.Length);
					}
					arrayArchive=memoryStream.ToArray();
				}
			}
			catch(Exception ex) {
				releaseDownloadResult.ListProblems.Add("The release archive is not readable: "+ex.Message);
				return releaseDownloadResult;
			}
			if(ReleaseManifest.ComputeSha256(arrayArchive)!=releaseManifest.ArchiveSha256.ToLowerInvariant()) {
				releaseDownloadResult.ListProblems.Add("The release archive does not match its SHA-256. It may be incomplete; publish it again from the "
					+"update server.");
				return releaseDownloadResult;
			}
			releaseDownloadResult.ListProblems.AddRange(ExtractArchive(arrayArchive,folderDest,releaseManifest));
			if(releaseDownloadResult.ListProblems.Count>0) {
				return releaseDownloadResult;
			}
			releaseDownloadResult.ListProblems.AddRange(releaseManifest.VerifyFolder(folderDest));
			releaseDownloadResult.IsOk=releaseDownloadResult.ListProblems.Count==0;
			return releaseDownloadResult;
		}

		private static List<string> ExtractArchive(byte[] arrayArchive,string folderDest,ReleaseManifest releaseManifest) {
			List<string> listProblems=new List<string>();
			if(Directory.Exists(folderDest)) {
				Directory.Delete(folderDest,true);
			}
			Directory.CreateDirectory(folderDest);
			string folderFull=Path.GetFullPath(folderDest).TrimEnd('\\')+"\\";
			HashSet<string> hashSetListed=new HashSet<string>(releaseManifest.ListFiles.Select(x => x.RelativePath.Replace('/','\\')),
				StringComparer.OrdinalIgnoreCase);
			string manifestInside=null;
			using(MemoryStream memoryStream=new MemoryStream(arrayArchive))
			using(ZipFile zipFile=ZipFile.Read(memoryStream)) {
				foreach(ZipEntry zipEntry in zipFile) {
					if(zipEntry.IsDirectory) {
						continue;
					}
					string relative=zipEntry.FileName.Replace('/','\\');
					if(!ReleaseManifest.IsSafeRelativePath(relative)) {
						listProblems.Add("Unsafe path in the release archive: "+zipEntry.FileName);
						continue;
					}
					if(relative.Equals(ReleaseManifest.FILE_NAME,StringComparison.OrdinalIgnoreCase)) {
						using(MemoryStream memoryStreamManifest=new MemoryStream()) {
							zipEntry.Extract(memoryStreamManifest);
							manifestInside=Encoding.UTF8.GetString(memoryStreamManifest.ToArray());
						}
						continue;
					}
					if(!hashSetListed.Contains(relative)) {
						listProblems.Add("Not in the manifest: "+relative);
						continue;
					}
					string pathDest=Path.GetFullPath(Path.Combine(folderDest,relative));
					if(!pathDest.StartsWith(folderFull,StringComparison.OrdinalIgnoreCase)) {
						listProblems.Add("Unsafe path in the release archive: "+zipEntry.FileName);
						continue;
					}
					Directory.CreateDirectory(Path.GetDirectoryName(pathDest));
					using(FileStream fileStream=new FileStream(pathDest,FileMode.Create,FileAccess.Write)) {
						zipEntry.Extract(fileStream);
					}
				}
			}
			if(manifestInside==null) {
				listProblems.Add("The release archive has no manifest.");
				return listProblems;
			}
			if(manifestInside!=releaseManifest.Serialize(includeArchiveHash:false)) {
				listProblems.Add("The manifest inside the release archive differs from the published manifest.");
				return listProblems;
			}
			File.WriteAllText(Path.Combine(folderDest,ReleaseManifest.FILE_NAME),releaseManifest.Serialize(),new UTF8Encoding(false));
			return listProblems;
		}
		#endregion Pull
	}
}
