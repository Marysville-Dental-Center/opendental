// The manifest of a fork release published to the database. This file has no dependencies outside the .NET Framework because
// UpdateFileCopier compiles it too (linked), so the copier can check every file before it replaces the installation.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenDentBusiness {
	///<summary>One file of a release: its path relative to the installation folder (backslashes), size and SHA-256.</summary>
	public class ReleaseManifestFile {
		public string RelativePath;
		public long Size;
		///<summary>Lower-case hex.</summary>
		public string Sha256;
	}

	///<summary>The text manifest of a release published by the update server. It names our product, so a manifest from any other program is never
	///accepted, and carries the SHA-256 of the archive and of every file. Format, one "Key=Value" per line, '#' starts a comment:
	///Format, Product, ReleaseVersion, BuildId, BuildUtc (optional), CompatVersion, PublishedUtc, PublishedBy, ArchiveSha256 (only in the database copy, because the
	///archive itself holds a copy of the manifest), and one "File=sha256|size|relative path" line per file.</summary>
	public class ReleaseManifest {
		///<summary>The product name every manifest must carry. The internal rename of the program comes later; release manifests use the new name
		///from the start so they can never be confused with another program's update files.</summary>
		public const string PRODUCT_NAME="Open PMS - Dental";
		///<summary>Name of the manifest file inside the archive and in the installation folder.</summary>
		public const string FILE_NAME="ForkRelease.manifest";
		public const int FORMAT=1;

		public string Product=PRODUCT_NAME;
		///<summary>The release (product) version, e.g. "0.0". Our own scheme, never compared with Open Dental version numbers.</summary>
		public string ReleaseVersion="";
		///<summary>Identifies the exact build: SHA-256 over the program's own assemblies (see ForkReleases.ComputeBuildId).</summary>
		public string BuildId="";
		///<summary>When the build was compiled (UTC, ForkBuildUtc metadata of OpenDental.exe). Orders two builds of the same release version.
		///DateTime.MinValue when unknown; the "BuildUtc" line is then left out.</summary>
		public DateTime BuildUtc=DateTime.MinValue;
		///<summary>The Open Dental compatible version line of the build (24.3.63.0 until the fork markers take over). Information only.</summary>
		public string CompatVersion="";
		public DateTime PublishedUtc=DateTime.MinValue;
		public string PublishedBy="";
		///<summary>SHA-256 of the zipped archive. Blank in the copy of the manifest that is inside the archive.</summary>
		public string ArchiveSha256="";
		public List<ReleaseManifestFile> ListFiles=new List<ReleaseManifestFile>();

		///<summary>The manifest as text. Pass includeArchiveHash=false for the copy that goes inside the archive.</summary>
		public string Serialize(bool includeArchiveHash=true) {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("# Release manifest. Checked with SHA-256 before any file is used.\n");
			stringBuilder.Append("Format="+FORMAT.ToString(CultureInfo.InvariantCulture)+"\n");
			stringBuilder.Append("Product="+Clean(Product)+"\n");
			stringBuilder.Append("ReleaseVersion="+Clean(ReleaseVersion)+"\n");
			stringBuilder.Append("BuildId="+Clean(BuildId)+"\n");
			if(BuildUtc!=DateTime.MinValue) {
				stringBuilder.Append("BuildUtc="+BuildUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture)+"\n");
			}
			stringBuilder.Append("CompatVersion="+Clean(CompatVersion)+"\n");
			stringBuilder.Append("PublishedUtc="+PublishedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture)+"\n");
			stringBuilder.Append("PublishedBy="+Clean(PublishedBy)+"\n");
			if(includeArchiveHash) {
				stringBuilder.Append("ArchiveSha256="+Clean(ArchiveSha256)+"\n");
			}
			foreach(ReleaseManifestFile releaseManifestFile in ListFiles.OrderBy(x => x.RelativePath,StringComparer.OrdinalIgnoreCase)) {
				stringBuilder.Append("File="+releaseManifestFile.Sha256+"|"+releaseManifestFile.Size.ToString(CultureInfo.InvariantCulture)+"|"
					+Clean(releaseManifestFile.RelativePath)+"\n");
			}
			return stringBuilder.ToString();
		}

		///<summary>Parses manifest text. Throws FormatException for anything malformed: unknown format, a missing required key, a bad hash or an
		///unsafe file path.</summary>
		public static ReleaseManifest Parse(string text) {
			if(text==null) {
				throw new FormatException("The release manifest is empty.");
			}
			ReleaseManifest releaseManifest=new ReleaseManifest();
			releaseManifest.Product="";
			bool hasFormat=false;
			string[] arrayLines=text.Replace("\r\n","\n").Split('\n');
			foreach(string lineRaw in arrayLines) {
				string line=lineRaw.Trim();
				if(line=="" || line.StartsWith("#")) {
					continue;
				}
				int indexEquals=line.IndexOf('=');
				if(indexEquals<=0) {
					throw new FormatException("Bad release manifest line: "+line);
				}
				string key=line.Substring(0,indexEquals);
				string value=line.Substring(indexEquals+1);
				switch(key) {
					case "Format":
						if(value!=FORMAT.ToString(CultureInfo.InvariantCulture)) {
							throw new FormatException("Unknown release manifest format: "+value);
						}
						hasFormat=true;
						break;
					case "Product": releaseManifest.Product=value; break;
					case "ReleaseVersion": releaseManifest.ReleaseVersion=value; break;
					case "BuildId": releaseManifest.BuildId=value; break;
					case "CompatVersion": releaseManifest.CompatVersion=value; break;
					case "PublishedBy": releaseManifest.PublishedBy=value; break;
					case "ArchiveSha256":
						if(value!="" && !IsSha256Hex(value)) {
							throw new FormatException("Bad archive hash in the release manifest.");
						}
						releaseManifest.ArchiveSha256=value;
						break;
					case "PublishedUtc":
						releaseManifest.PublishedUtc=ParseUtc(value,"publish date");
						break;
					case "BuildUtc":
						releaseManifest.BuildUtc=ParseUtc(value,"build time");
						break;
					case "File":
						releaseManifest.ListFiles.Add(ParseFileLine(value));
						break;
					default:
						throw new FormatException("Unknown release manifest key: "+key);
				}
			}
			if(!hasFormat) {
				throw new FormatException("The release manifest has no Format line.");
			}
			if(releaseManifest.ReleaseVersion=="" || releaseManifest.BuildId=="") {
				throw new FormatException("The release manifest has no release version or build id.");
			}
			if(releaseManifest.ListFiles.GroupBy(x => x.RelativePath,StringComparer.OrdinalIgnoreCase).Any(x => x.Count()>1)) {
				throw new FormatException("The release manifest lists a file twice.");
			}
			return releaseManifest;
		}

		private static DateTime ParseUtc(string value,string what) {
			DateTime dateTime;
			if(!DateTime.TryParseExact(value,"yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture,
				DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out dateTime))
			{
				throw new FormatException("Bad "+what+" in the release manifest: "+value);
			}
			return dateTime;
		}

		private static ReleaseManifestFile ParseFileLine(string value) {
			string[] arrayParts=value.Split(new char[] { '|' },3);
			if(arrayParts.Length!=3 || !IsSha256Hex(arrayParts[0])) {
				throw new FormatException("Bad file line in the release manifest: "+value);
			}
			long size;
			if(!long.TryParse(arrayParts[1],NumberStyles.None,CultureInfo.InvariantCulture,out size)) {
				throw new FormatException("Bad file size in the release manifest: "+value);
			}
			if(!IsSafeRelativePath(arrayParts[2])) {
				throw new FormatException("Unsafe file path in the release manifest: "+arrayParts[2]);
			}
			ReleaseManifestFile releaseManifestFile=new ReleaseManifestFile();
			releaseManifestFile.Sha256=arrayParts[0].ToLowerInvariant();
			releaseManifestFile.Size=size;
			releaseManifestFile.RelativePath=arrayParts[2];
			return releaseManifestFile;
		}

		///<summary>True if this manifest names our product. A manifest for any other product is never used.</summary>
		public bool IsOurProduct() {
			return Product==PRODUCT_NAME;
		}

		///<summary>True for a relative path that stays inside the folder it is combined with: no drive, no leading separator, no "." or ".." parts and
		///no characters that are invalid in file names. Separators may be '\' or '/'.</summary>
		public static bool IsSafeRelativePath(string relativePath) {
			if(string.IsNullOrWhiteSpace(relativePath) || relativePath.Length>400) {
				return false;
			}
			if(relativePath.StartsWith("\\") || relativePath.StartsWith("/") || relativePath.Contains(":")) {
				return false;
			}
			string[] arrayParts=relativePath.Split('\\','/');
			char[] arrayInvalid=Path.GetInvalidFileNameChars();
			foreach(string part in arrayParts) {
				if(part=="" || part=="." || part==".." || part.Trim()!=part || part.IndexOfAny(arrayInvalid)>=0) {
					return false;
				}
			}
			return true;
		}

		///<summary>Lower-case hex SHA-256 of the bytes.</summary>
		public static string ComputeSha256(byte[] arrayBytes) {
			using(SHA256 sha256=SHA256.Create()) {
				return ToHex(sha256.ComputeHash(arrayBytes));
			}
		}

		///<summary>Lower-case hex SHA-256 of the stream from its current position to its end.</summary>
		public static string ComputeSha256(Stream stream) {
			using(SHA256 sha256=SHA256.Create()) {
				return ToHex(sha256.ComputeHash(stream));
			}
		}

		///<summary>Lower-case hex SHA-256 of the file.</summary>
		public static string ComputeSha256File(string path) {
			using(FileStream fileStream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
				return ComputeSha256(fileStream);
			}
		}

		///<summary>Checks the files in the folder against this manifest: each listed file must exist with the listed size and SHA-256. Files that are
		///not listed (other than the manifest file itself) are reported too, because the copier would install them. Returns the problems found; an
		///empty list means the folder holds exactly this release.</summary>
		public List<string> VerifyFolder(string folder) {
			List<string> listProblems=new List<string>();
			if(!IsOurProduct()) {
				listProblems.Add("The manifest is for another product: "+Product);
				return listProblems;
			}
			HashSet<string> hashSetListed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach(ReleaseManifestFile releaseManifestFile in ListFiles) {
				if(!IsSafeRelativePath(releaseManifestFile.RelativePath)) {
					listProblems.Add("Unsafe path: "+releaseManifestFile.RelativePath);
					continue;
				}
				string relativeNormalized=releaseManifestFile.RelativePath.Replace('/','\\');
				hashSetListed.Add(relativeNormalized);
				string path=Path.Combine(folder,relativeNormalized);
				if(!File.Exists(path)) {
					listProblems.Add("Missing: "+releaseManifestFile.RelativePath);
					continue;
				}
				FileInfo fileInfo=new FileInfo(path);
				if(fileInfo.Length!=releaseManifestFile.Size) {
					listProblems.Add("Wrong size: "+releaseManifestFile.RelativePath);
					continue;
				}
				if(ComputeSha256File(path)!=releaseManifestFile.Sha256) {
					listProblems.Add("Wrong SHA-256: "+releaseManifestFile.RelativePath);
				}
			}
			string folderFull=Path.GetFullPath(folder).TrimEnd('\\')+"\\";
			foreach(string pathFile in Directory.GetFiles(folder,"*",SearchOption.AllDirectories)) {
				string relative=Path.GetFullPath(pathFile).Substring(folderFull.Length);
				if(relative.Equals(FILE_NAME,StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if(!hashSetListed.Contains(relative)) {
					listProblems.Add("Not in the manifest: "+relative);
				}
			}
			return listProblems;
		}

		public static bool IsSha256Hex(string value) {
			if(value==null || value.Length!=64) {
				return false;
			}
			foreach(char c in value) {
				bool isHex=(c>='0' && c<='9') || (c>='a' && c<='f') || (c>='A' && c<='F');
				if(!isHex) {
					return false;
				}
			}
			return true;
		}

		private static string ToHex(byte[] arrayBytes) {
			StringBuilder stringBuilder=new StringBuilder(arrayBytes.Length*2);
			foreach(byte b in arrayBytes) {
				stringBuilder.Append(b.ToString("x2",CultureInfo.InvariantCulture));
			}
			return stringBuilder.ToString();
		}

		///<summary>Values are single-line; line breaks would let a value forge another key.</summary>
		private static string Clean(string value) {
			return (value??"").Replace("\r"," ").Replace("\n"," ");
		}
	}
}
