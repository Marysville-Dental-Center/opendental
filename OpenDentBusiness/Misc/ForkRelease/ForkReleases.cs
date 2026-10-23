// Publishing a fork release to the database and pulling it on the other workstations (decoupling plan Phase 4, backlog Risk R1).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CodeBase;
using Ionic.Zip;

namespace OpenDentBusiness {
	///<summary>Which build is running or was published: the release version ("0.0"), the build id and the build time.</summary>
	[Serializable]
	public class ReleaseIdentity {
		public string ReleaseVersion="";
		public string BuildId="";
		///<summary>When the build was compiled (UTC), from the ForkBuildUtc assembly metadata that OpenDental.csproj writes. Orders two builds of the
		///same release version. DateTime.MinValue when unknown.</summary>
		public DateTime BuildUtc=DateTime.MinValue;

		public override string ToString() {
			return ReleaseVersion+" (build "+(BuildId.Length>12?BuildId.Substring(0,12):BuildId)
				+(BuildUtc==DateTime.MinValue?"":", built "+BuildUtc.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)+" UTC")+")";
		}
	}

	///<summary>How this workstation relates to the update server, from the WebServiceServerName preference.</summary>
	public enum UpdateServerRole {
		///<summary>WebServiceServerName names another computer. This workstation never publishes.</summary>
		OtherComputer,
		///<summary>WebServiceServerName is blank (the default), so any workstation may act as the update server. A workstation may publish when
		///nothing is published yet or when its build is known to be newer than the published one; it never replaces a published build that is newer or
		///whose order is unknown.</summary>
		AnyWorkstation,
		///<summary>WebServiceServerName names this computer: the update server chosen by the office. It may replace the published build.</summary>
		ThisComputer,
	}

	public enum ReleaseAction {
		///<summary>Nothing to do: this workstation runs the published build, or nothing applies.</summary>
		None,
		///<summary>Offer to store this workstation's binaries as the published release: nothing is published yet, this is the named update server, or
		///(blank WebServiceServerName) this build is known to be newer than the published one.</summary>
		Publish,
		///<summary>A different build is published that is newer, or whose order is unknown and that this workstation may not replace: update this
		///workstation from it.</summary>
		Pull,
		///<summary>This workstation runs a newer release or build than the one published and may not publish it. Refused; no downgrade is offered.
		///</summary>
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
		///<summary>Key of the AssemblyMetadata attribute that holds the build time of OpenDental.exe (written by OpenDental.csproj).</summary>
		public const string BUILD_UTC_METADATA_KEY="ForkBuildUtc";
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

		///<summary>The running program's identity: release version and build time from OpenDental.exe, and build id of the installation folder.
		///Computed once.</summary>
		public static ReleaseIdentity GetRunningIdentity() {
			if(_releaseIdentityRunning==null) {
				ReleaseIdentity releaseIdentity=new ReleaseIdentity();
				releaseIdentity.ReleaseVersion=ForkVersions.GetReleaseVersion();
				releaseIdentity.BuildId=ComputeBuildId(AppDomain.CurrentDomain.BaseDirectory);
				releaseIdentity.BuildUtc=GetBuildUtc();
				_releaseIdentityRunning=releaseIdentity;
			}
			return _releaseIdentityRunning;
		}

		///<summary>The build time (UTC) that OpenDental.csproj stamps into OpenDental.exe as [assembly: AssemblyMetadata("ForkBuildUtc",...)] each time
		///the program is compiled. Reads the given assembly, else the entry assembly. DateTime.MinValue when it has no readable stamp.</summary>
		public static DateTime GetBuildUtc(Assembly assembly=null) {
			assembly=assembly??Assembly.GetEntryAssembly();
			if(assembly==null) {
				return DateTime.MinValue;
			}
			AssemblyMetadataAttribute assemblyMetadataAttribute=assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
				.FirstOrDefault(x => x.Key==BUILD_UTC_METADATA_KEY);
			return ParseBuildUtc(assemblyMetadataAttribute?.Value);
		}

		///<summary>Parses a build time in the form yyyy-MM-ddTHH:mm:ssZ. DateTime.MinValue when blank or malformed.</summary>
		public static DateTime ParseBuildUtc(string value) {
			DateTime dateTime;
			if(string.IsNullOrWhiteSpace(value) || !DateTime.TryParseExact(value.Trim(),"yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture,
				DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out dateTime))
			{
				return DateTime.MinValue;
			}
			return dateTime;
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
		///<summary>The role of this workstation from the WebServiceServerName preference: blank means any workstation, otherwise the named computer
		///is the update server and every other computer is not.</summary>
		public static UpdateServerRole GetUpdateServerRole(string updateServerName,Func<string,bool> funcIsThisComputer) {
			if(string.IsNullOrWhiteSpace(updateServerName)) {
				return UpdateServerRole.AnyWorkstation;
			}
			return funcIsThisComputer(updateServerName.Trim()) ? UpdateServerRole.ThisComputer : UpdateServerRole.OtherComputer;
		}

		///<summary>Orders the published build and the running build. Greater than 0 when the published one is newer, less than 0 when the running one
		///is newer, null when the order is unknown. A higher release version is newer. Within one release version (it is pinned at "0.0" for now), the
		///build times decide; if either is missing or they are equal, the order is unknown.</summary>
		public static int? CompareBuilds(ReleaseManifest releaseManifestPublished,ReleaseIdentity releaseIdentityRunning) {
			int compare=ForkVersions.CompareReleaseVersions(releaseManifestPublished.ReleaseVersion,releaseIdentityRunning.ReleaseVersion);
			if(compare!=0) {
				return compare;
			}
			if(releaseManifestPublished.BuildUtc==DateTime.MinValue || releaseIdentityRunning.BuildUtc==DateTime.MinValue
				|| releaseManifestPublished.BuildUtc==releaseIdentityRunning.BuildUtc)
			{
				return null;
			}
			return releaseManifestPublished.BuildUtc.CompareTo(releaseIdentityRunning.BuildUtc);
		}

		///<summary>What this workstation does with the published release. No case ever offers a downgrade:
		///- Nothing published: the update server (named, or any workstation when WebServiceServerName is blank) offers to publish.
		///- A newer build is published: pull it.
		///- The named update server (WebServiceServerName is this computer) may replace the published build: it offers to publish.
		///- This build is known to be newer (higher release version, or same release and later build time): with a blank WebServiceServerName it offers
		///to publish, otherwise it is refused until the update server publishes it.
		///- Same release and the order is unknown: pull. Only the named update server replaces a build it cannot prove older, so a workstation that
		///still runs an old build never publishes it over the office's release.
		///Debug builds never publish or pull; the version guard still runs for them.</summary>
		public static ReleaseDecision Decide(ReleaseManifest releaseManifestPublished,ReleaseIdentity releaseIdentityRunning,
			UpdateServerRole updateServerRole,bool isDebugBuild,string updateServerName="")
		{
			ReleaseDecision releaseDecision=new ReleaseDecision();
			if(isDebugBuild) {
				releaseDecision.Message="Debug builds do not publish or pull releases.";
				return releaseDecision;
			}
			if(releaseManifestPublished==null) {
				if(updateServerRole!=UpdateServerRole.OtherComputer) {
					releaseDecision.Action=ReleaseAction.Publish;
					releaseDecision.Message="No release is published yet.";
				}
				return releaseDecision;
			}
			if(releaseManifestPublished.BuildId==releaseIdentityRunning.BuildId) {
				return releaseDecision;
			}
			int? compare=CompareBuilds(releaseManifestPublished,releaseIdentityRunning);
			if(compare>0) {
				releaseDecision.Action=ReleaseAction.Pull;
				releaseDecision.Message="A newer build is published: "+Describe(releaseManifestPublished)+".";
				return releaseDecision;
			}
			if(updateServerRole==UpdateServerRole.ThisComputer) {
				releaseDecision.Action=ReleaseAction.Publish;
				releaseDecision.Message="This build is not the published one ("+Describe(releaseManifestPublished)+").";
				return releaseDecision;
			}
			if(compare<0) {
				if(updateServerRole==UpdateServerRole.AnyWorkstation) {
					releaseDecision.Action=ReleaseAction.Publish;
					releaseDecision.Message="This build is newer than the published one ("+Describe(releaseManifestPublished)+").";
					return releaseDecision;
				}
				releaseDecision.Action=ReleaseAction.RefuseNewerThanPublished;
				releaseDecision.Message="This workstation runs "+releaseIdentityRunning+", which is newer than the published release "
					+Describe(releaseManifestPublished)+".\r\n"
					+"Run the new release on the update server"+(string.IsNullOrEmpty(updateServerName)?"":" ("+updateServerName+")")
					+" first so it is published, or install the published release on this workstation.";
				return releaseDecision;
			}
			//Same release version and no build order. Only the named update server may replace the published build, so pull it.
			releaseDecision.Action=ReleaseAction.Pull;
			releaseDecision.Message="The update server published "+Describe(releaseManifestPublished)+".";
			if(updateServerRole==UpdateServerRole.AnyWorkstation) {
				releaseDecision.Message+="\r\nTo publish this build instead, name this computer as the update server (Update Setup) and start it again.";
			}
			return releaseDecision;
		}

		///<summary>True when this workstation may keep running a build other than the published one: the decision would offer to publish it (the
		///named update server, or a build known to be newer with a blank WebServiceServerName). Any other different build must update.</summary>
		public static bool MayRunUnpublishedBuild(ReleaseManifest releaseManifestPublished,ReleaseIdentity releaseIdentityRunning,
			UpdateServerRole updateServerRole)
		{
			if(releaseManifestPublished==null || releaseManifestPublished.BuildId==releaseIdentityRunning.BuildId) {
				return true;
			}
			return Decide(releaseManifestPublished,releaseIdentityRunning,updateServerRole,isDebugBuild:false).Action==ReleaseAction.Publish;
		}

		private static string Describe(ReleaseManifest releaseManifest) {
			ReleaseIdentity releaseIdentity=new ReleaseIdentity();
			releaseIdentity.ReleaseVersion=releaseManifest.ReleaseVersion;
			releaseIdentity.BuildId=releaseManifest.BuildId;
			releaseIdentity.BuildUtc=releaseManifest.BuildUtc;
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
			releaseManifest.BuildUtc=releaseIdentity.BuildUtc;
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

		///<summary>The published manifest (for its build time), or, when it is missing or unreadable, a manifest with only the release version and
		///build id of the ForkProgramVersion marker, whose build order is then unknown.</summary>
		public static ReleaseManifest GetPublishedManifestOrMarker(ReleaseIdentity releaseIdentityMarker) {
			ReleaseManifest releaseManifest=GetPublishedManifest(out string _);
			if(releaseManifest!=null && releaseManifest.BuildId==releaseIdentityMarker.BuildId) {
				return releaseManifest;
			}
			releaseManifest=new ReleaseManifest();
			releaseManifest.ReleaseVersion=releaseIdentityMarker.ReleaseVersion;
			releaseManifest.BuildId=releaseIdentityMarker.BuildId;
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
