// The office CA certificate file: checked, then copied as PEM into the program folder, where SslCa points.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenPmsDentalDbSettings {
	///<summary>The office CA certificate (owner decision Q5): the server's certificate must chain to it. A PEM file (one or more CERTIFICATE
	///blocks, as the server guide's openssl commands write it) or one DER certificate. Every certificate must be a CA when it says what it is
	///(basic constraints). The tool keeps its own copy (DbConnectionConfig.GetOfficeCaCopyPath, in the program folder, protected: WriteCopy), so
	///the connection never depends on the file the administrator picked, which may be on a removable drive or a share.</summary>
	public static class OfficeCaFile {
		private static readonly Regex _regexPemCertificate=new Regex(
			@"-----BEGIN CERTIFICATE-----\s*(?<base64>[A-Za-z0-9+/=\s]+?)\s*-----END CERTIFICATE-----",RegexOptions.CultureInvariant);

		///<summary>The certificates of the file. Throws DbSettingsException (exit code 2) when it is missing, is not a certificate file, or holds
		///a certificate that is not a CA.</summary>
		public static List<X509Certificate2> Load(string path) {
			byte[] arrayBytes;
			try {
				if(!File.Exists(path)) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" does not exist.");
				}
				arrayBytes=File.ReadAllBytes(path);
			}
			catch(IOException ex) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" cannot be read: "+ex.Message);
			}
			catch(UnauthorizedAccessException ex) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" cannot be read: "+ex.Message);
			}
			List<X509Certificate2> listCertificates=new List<X509Certificate2>();
			string text=Encoding.ASCII.GetString(arrayBytes);
			MatchCollection matchCollection=_regexPemCertificate.Matches(text);
			try {
				if(matchCollection.Count>0) {
					foreach(Match match in matchCollection) {
						listCertificates.Add(new X509Certificate2(Convert.FromBase64String(match.Groups["base64"].Value)));
					}
				}
				else {
					listCertificates.Add(new X509Certificate2(arrayBytes));//DER
				}
			}
			catch(Exception ex) when(ex is CryptographicException || ex is FormatException) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" is not a certificate file "
					+"(PEM or DER).");
			}
			foreach(X509Certificate2 x509Certificate2 in listCertificates) {
				if(x509Certificate2.HasPrivateKey) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" holds a private key. Give "
						+"only the certificate (ca.pem), never the CA's key.");
				}
				X509BasicConstraintsExtension x509BasicConstraintsExtension=x509Certificate2.Extensions.OfType<X509BasicConstraintsExtension>()
					.FirstOrDefault();
				if(x509BasicConstraintsExtension!=null && !x509BasicConstraintsExtension.CertificateAuthority) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The office CA certificate file "+path+" holds a certificate that is "
						+"not a certificate authority ("+x509Certificate2.Subject+"). Give the office CA certificate, not the server's.");
				}
			}
			return listCertificates;
		}

		///<summary>The certificates as PEM text, in order.</summary>
		public static string ToPem(List<X509Certificate2> listCertificates) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(X509Certificate2 x509Certificate2 in listCertificates) {
				stringBuilder.Append("-----BEGIN CERTIFICATE-----\n");
				stringBuilder.Append(Convert.ToBase64String(x509Certificate2.RawData,Base64FormattingOptions.InsertLineBreaks).Replace("\r\n","\n"));
				stringBuilder.Append("\n-----END CERTIFICATE-----\n");
			}
			return stringBuilder.ToString();
		}

		///<summary>Writes the certificates as PEM to pathCopy (DbConnectionConfig.GetOfficeCaCopyPath: the ConnectionSettings folder of the program
		///folder), replacing an earlier copy. Every user of the computer verifies the server against this file, so only an administrator may
		///change it (docs/specs/db-connection-security.md section 4.4): the folder and the file get a protected DACL (SYSTEM and Administrators
		///full control, Users read, nothing inherited, owner Administrators), and an existing folder or file that is a link (a reparse point) or
		///belongs to anyone but an administrator is refused (DbSettingsException, exit code 3) before anything is written. Throws IOException or
		///UnauthorizedAccessException.</summary>
		public static void WriteCopy(List<X509Certificate2> listCertificates,string pathCopy) {
			string folder=Path.GetDirectoryName(Path.GetFullPath(pathCopy));
			string pathTemp=pathCopy+".new";
			if(Directory.Exists(folder) || File.Exists(folder)) {
				CheckExisting(folder,isFolderExpected:true);
				Directory.SetAccessControl(folder,GetFolderSecurity());
			}
			else {
				Directory.CreateDirectory(folder,GetFolderSecurity());
			}
			foreach(string path in new[] { pathCopy,pathTemp }) {
				if(File.Exists(path) || Directory.Exists(path)) {
					CheckExisting(path,isFolderExpected:false);
				}
			}
			if(File.Exists(pathTemp)) {
				File.Delete(pathTemp);
			}
			byte[] arrayBytes=new UTF8Encoding(false).GetBytes(ToPem(listCertificates));
			//A new file with its own protected security from the start, never one a user may have made (FileMode.CreateNew).
			using(FileStream fileStream=new FileStream(pathTemp,FileMode.CreateNew,FileSystemRights.Write,FileShare.None,4096,FileOptions.WriteThrough,
				GetFileSecurity()))
			{
				fileStream.Write(arrayBytes,0,arrayBytes.Length);
			}
			if(File.Exists(pathCopy)) {
				File.Replace(pathTemp,pathCopy,null);
			}
			else {
				File.Move(pathTemp,pathCopy);
			}
			//File.Replace keeps the replaced file's DACL, so the result always gets the protected one again.
			File.SetAccessControl(pathCopy,GetFileSecurity());
		}

		///<summary>Throws DbSettingsException (exit code 3) when path is a link (a reparse point: a junction or a symbolic link), is a file where a
		///folder is expected or the other way round, or belongs to anyone but BUILTIN\Administrators, SYSTEM, TrustedInstaller or the account the
		///tool runs as (the elevated administrator). A standard user who made the folder or the file first must never own what the computer
		///trusts.</summary>
		public static void CheckExisting(string path,bool isFolderExpected) {
			FileAttributes fileAttributes=File.GetAttributes(path);
			if((fileAttributes & FileAttributes.ReparsePoint)!=0) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_WRITE_FAILED,path+" is a link (a junction or a symbolic link), so the office CA "
					+"certificate is not written there. Delete it, then save again.");
			}
			bool isFolder=(fileAttributes & FileAttributes.Directory)!=0;
			if(isFolder!=isFolderExpected) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_WRITE_FAILED,path+" is a "+(isFolder ? "folder" : "file")+", not the "
					+(isFolderExpected ? "folder" : "file")+" of the office CA certificate. Delete it, then save again.");
			}
			SecurityIdentifier securityIdentifierOwner;
			if(isFolder) {
				securityIdentifierOwner=(SecurityIdentifier)Directory.GetAccessControl(path,AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier));
			}
			else {
				securityIdentifierOwner=(SecurityIdentifier)File.GetAccessControl(path,AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier));
			}
			if(!IsTrustedOwner(securityIdentifierOwner)) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_WRITE_FAILED,path+" belongs to "+GetAccountName(securityIdentifierOwner)+", not to an "
					+"administrator, so the office CA certificate is not written there. Delete it, then save again.");
			}
		}

		///<summary>True for an owner an administrator's file may have: BUILTIN\Administrators, SYSTEM, TrustedInstaller, or the account this
		///process runs as (the tool's manifest makes that an elevated administrator).</summary>
		private static bool IsTrustedOwner(SecurityIdentifier securityIdentifier) {
			if(securityIdentifier==null) {
				return false;
			}
			if(securityIdentifier.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) || securityIdentifier.IsWellKnown(WellKnownSidType.LocalSystemSid)
				|| securityIdentifier.Value==SID_TRUSTED_INSTALLER)
			{
				return true;
			}
			using(WindowsIdentity windowsIdentity=WindowsIdentity.GetCurrent()) {
				return securityIdentifier.Equals(windowsIdentity.User);
			}
		}

		private static string GetAccountName(SecurityIdentifier securityIdentifier) {
			if(securityIdentifier==null) {
				return "nobody";
			}
			try {
				return securityIdentifier.Translate(typeof(NTAccount)).Value;
			}
			catch(SystemException) {//IdentityNotMappedException, or no domain controller to ask
				return securityIdentifier.Value;
			}
		}

		///<summary>NT SERVICE\TrustedInstaller, which owns what Windows installs.</summary>
		private const string SID_TRUSTED_INSTALLER="S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

		///<summary>True when this process runs with administrator rights (an elevated token), as the tool's manifest asks.</summary>
		public static bool IsElevated() {
			using(WindowsIdentity windowsIdentity=WindowsIdentity.GetCurrent()) {
				return new WindowsPrincipal(windowsIdentity).IsInRole(WindowsBuiltInRole.Administrator);
			}
		}

		///<summary>The protected DACL of the folder: SYSTEM and Administrators full control, Users read and execute, all inherited by its files;
		///nothing from the program folder. Owner Administrators when this process is elevated. Not elevated (only the tests run the tool's code
		///that way) the account this process runs as gets full control too, since it is not an administrator then.</summary>
		public static DirectorySecurity GetFolderSecurity() {
			DirectorySecurity directorySecurity=new DirectorySecurity();
			directorySecurity.SetAccessRuleProtection(true,false);
			InheritanceFlags inheritanceFlags=InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit;
			directorySecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),
				FileSystemRights.FullControl,inheritanceFlags,PropagationFlags.None,AccessControlType.Allow));
			directorySecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),
				FileSystemRights.FullControl,inheritanceFlags,PropagationFlags.None,AccessControlType.Allow));
			directorySecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null),
				FileSystemRights.ReadAndExecute,inheritanceFlags,PropagationFlags.None,AccessControlType.Allow));
			if(IsElevated()) {
				directorySecurity.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));
			}
			else {
				using(WindowsIdentity windowsIdentity=WindowsIdentity.GetCurrent()) {
					directorySecurity.AddAccessRule(new FileSystemAccessRule(windowsIdentity.User,FileSystemRights.FullControl,inheritanceFlags,
						PropagationFlags.None,AccessControlType.Allow));
				}
			}
			return directorySecurity;
		}

		///<summary>The protected DACL of the office CA file: SYSTEM and Administrators full control, Users read; nothing inherited. Owner and
		///the not-elevated case as GetFolderSecurity.</summary>
		public static FileSecurity GetFileSecurity() {
			FileSecurity fileSecurity=new FileSecurity();
			fileSecurity.SetAccessRuleProtection(true,false);
			fileSecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),FileSystemRights.FullControl,
				AccessControlType.Allow));
			fileSecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),
				FileSystemRights.FullControl,AccessControlType.Allow));
			fileSecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null),FileSystemRights.Read,
				AccessControlType.Allow));
			if(IsElevated()) {
				fileSecurity.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));
			}
			else {
				using(WindowsIdentity windowsIdentity=WindowsIdentity.GetCurrent()) {
					fileSecurity.AddAccessRule(new FileSystemAccessRule(windowsIdentity.User,FileSystemRights.FullControl,AccessControlType.Allow));
				}
			}
			return fileSecurity;
		}

		///<summary>"subject, expires yyyy-MM-dd" of the first certificate, for the window.</summary>
		public static string Describe(List<X509Certificate2> listCertificates) {
			if(listCertificates.Count==0) {
				return "";
			}
			return listCertificates[0].Subject+", expires "+listCertificates[0].NotAfter.ToString("yyyy-MM-dd");
		}
	}
}
