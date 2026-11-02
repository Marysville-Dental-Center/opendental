// The names the installed program uses for itself at run time, in one place, so the rename to Open PMS Dental changes these values only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;

namespace CodeBase {
	///<summary>The names the program uses for itself at run time that the compiler cannot check: the exe and process name, the files of the
	///release build id, the folders under %LOCALAPPDATA%, %ProgramData% and %TEMP%, the Event Log sources, the Windows service prefix and
	///UpdateFileCopier's defaults. Every such site reads them from here, so a rename changes these values and nothing else. The values are
	///the Open PMS Dental names; the Legacy* values and HL7ServiceFallbackName are the names of builds before the rename, kept so that what they
	///left on a computer is still found. Database table, column and preference names are not here and never change.
	///This file has no dependencies beyond the framework: UpdateFileCopier compiles a linked copy of it, because it replaces CodeBase.dll while
	///it runs.</summary>
	public static class ProgramNames {
		#region Program files
		///<summary>The main program's process name: its exe file name without ".exe". Compare process names with IsProgramProcessName, which
		///matches the whole name, never a prefix (a prefix would also match other programs whose names start the same way).</summary>
		public const string ProcessName="OpenPmsDental";
		///<summary>The main program's exe file name.</summary>
		public const string ExeFileName=ProcessName+".exe";
		///<summary>The business layer's file name.</summary>
		public const string BusinessDllFileName="OpenPmsDentalBusiness.dll";
		///<summary>The dashboard graphs' file name.</summary>
		public const string GraphDllFileName="OpenPmsDentalGraph.dll";
		///<summary>The SFTP storage library's file name (the OpenPmsDentalStorage project).</summary>
		public const string CloudDllFileName="OpenPmsDentalStorage.dll";
		///<summary>The storage library's file name up to v0.0, before the storage rename of 2026-11-02 (docs\port-notes\RENAME.md). Nothing
		///references it any more, so nothing loads it. UpdateFileCopier never deletes a file, so a workstation updated from v0.0 keeps it: the
		///release channel never publishes it again (OpenPmsDentalBusiness.ForkReleases.ListRetiredFiles), Build-Release.ps1 refuses to stage it
		///and setup deletes it on an upgrade ([InstallDelete] in installer\OpenPmsDental.iss).</summary>
		public const string LegacyCloudDllFileName="OpenPmsDentalCloud.dll";
		///<summary>The program that copies a pulled release into the installation folder and restarts the program.</summary>
		public const string UpdateFileCopierExeFileName="UpdateFileCopier.exe";
		#endregion Program files

		#region Local configuration files
		///<summary>This computer's database connection settings, next to the exe. Never published with a release.</summary>
		public const string ConfigFileName="FreeDentalConfig.xml";
		///<summary>The Middle Tier server's database connection settings. Never published with a release.</summary>
		public const string MiddleTierConfigFileName="OpenDentalServerConfig.xml";
		///<summary>The replication service's settings. Never published with a release.</summary>
		public const string ReplicationServiceConfigFileName="OpenDentalReplicationServiceConfig.xml";
		#endregion Local configuration files

		#region Folders
		///<summary>The program's folder under %LOCALAPPDATA%: window sizes, Query Monitor filters, the Waiting Room position and the working
		///copies of chart letters with unsaved edits.</summary>
		public const string LocalAppDataFolderName="OpenPMS-Dental";
		///<summary>The folder under %LOCALAPPDATA% that builds before the rename used. It stays this value after the rename, so files that hold
		///unsaved work there (chart letter working copies) are still found. See GetLocalAppDataFolders.</summary>
		public const string LegacyLocalAppDataFolderName="OpenDental";
		///<summary>The program's folder under %ProgramData%: the SFTP download stash, and the logs of web applications.</summary>
		public const string ProgramDataFolderName="OpenPMS-Dental";
		///<summary>The program's folder under %TEMP% (PrefC.GetTempFolderPath). The program empties it when it closes.</summary>
		public const string TempFolderName="openpms-dental";
		///<summary>UpdateFileCopier's sub folder of TempFolderName, which the close-time clean-up keeps.</summary>
		public const string UpdateFileCopierTempFolderName="updatefilecopier";
		#endregion Folders

		#region Event Log and Windows services
		///<summary>The Event Log source of the main program (time synchronization messages).</summary>
		public const string EventLogSource="OpenPMS-Dental";
		///<summary>The default name of the HL7 Windows service, which is also the Event Log source its messages are written under.</summary>
		public const string HL7ServiceName="OpenPmsDentalHL7";
		///<summary>The name the HL7 service runs under when no installed service whose name starts with ServicePrefix or LegacyServicePrefix
		///was installed from its folder (a service installed with /ServiceName=&lt;another name&gt;). This is Open Dental's run-time default, and
		///it differs from the installer's HL7ServiceName. The service saves its name into a blank HL7Def.HL7ServiceName or eCW HL7ServiceName
		///program property on its first start and refuses to start when they differ later, and Open Dental's setup windows told users to type
		///this name, so it keeps this value through the rename: the HL7 setups of an adopted database hold it, and changing it would stop the
		///HL7 feed of such an install. This program's setup windows suggest HL7ServiceName, the name of the service its installers write.</summary>
		public const string HL7ServiceFallbackName="OpenDentalHL7";
		///<summary>The Event Log source of the HL7 service's messages.</summary>
		public const string EventLogSourceHL7=HL7ServiceName;
		///<summary>The Event Log source of the HL7 service's start and stop messages, written before the service has its name.</summary>
		public const string EventLogSourceHL7Main=HL7ServiceName+".Main";
		///<summary>Every Windows service of this program has a name that starts with this. ServiceManager refuses other names for a new
		///service, and the program starts only services whose names start with it (IsProgramServiceName). Installed services are looked up and
		///listed by this prefix or LegacyServicePrefix (HasServicePrefix).</summary>
		public const string ServicePrefix="OpenPmsDental";
		///<summary>The service prefix of Open Dental (and of builds before the rename). It stays this value: installed services are still looked
		///up and listed by it (HasServicePrefix), so a service installed before the rename, such as an HL7 service named OpenDentHL7, still finds
		///its own name. A service with this prefix is Open Dental's and is never started automatically (IsLegacyServiceName).</summary>
		public const string LegacyServicePrefix="OpenDent";
		#endregion Event Log and Windows services

		#region UpdateFileCopier defaults
		///<summary>The installation folder UpdateFileCopier copies into when it is started without one (debugging only; the program always passes
		///its own folder).</summary>
		public const string DefaultInstallFolder=@"C:\Program Files (x86)\Open PMS - Dental";
		#endregion UpdateFileCopier defaults

		///<summary>True when serviceName starts with ServicePrefix or with LegacyServicePrefix. Use it only where installed services are looked
		///up or listed: the HL7 service finding its own name, ServiceManager's list. Never use it to choose services to start, because it also
		///matches Open Dental's services; use IsProgramServiceName. Case-sensitive, like the service names the installers write.</summary>
		public static bool HasServicePrefix(string serviceName) {
			return IsProgramServiceName(serviceName) || IsLegacyServiceName(serviceName);
		}

		///<summary>True when serviceName is the name of one of this program's own services: it starts with ServicePrefix. These are the only
		///services the program ever starts by itself (CodeBase.ServicesHelper.GetServicesToStart). Case-sensitive.</summary>
		public static bool IsProgramServiceName(string serviceName) {
			if(string.IsNullOrEmpty(serviceName)) {
				return false;
			}
			return serviceName.StartsWith(ServicePrefix,StringComparison.Ordinal);
		}

		///<summary>True when serviceName starts with LegacyServicePrefix (and not with ServicePrefix): a service of Open Dental, such as
		///OpenDentalService, its HL7 service or an eConnector left behind by an Open Dental install, or one installed before the rename. Such a
		///service is looked up and listed, and ServiceManager shows it as Open Dental's, but it is never started automatically. Case-sensitive.</summary>
		public static bool IsLegacyServiceName(string serviceName) {
			if(string.IsNullOrEmpty(serviceName) || IsProgramServiceName(serviceName)) {
				return false;
			}
			return serviceName.StartsWith(LegacyServicePrefix,StringComparison.Ordinal);
		}

		///<summary>True when processName is the main program's process name. Matches the whole name, ignoring case; never a prefix.</summary>
		public static bool IsProgramProcessName(string processName) {
			return string.Equals(processName,ProcessName,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The program's folders under folderLocalAppData (%LOCALAPPDATA%): today's folder first, then the folder builds before the rename
		///used, when it is a different one. Empty when folderLocalAppData is blank.</summary>
		public static List<string> GetLocalAppDataFolders(string folderLocalAppData) {
			List<string> listFolders=new List<string>();
			if(string.IsNullOrEmpty(folderLocalAppData)) {
				return listFolders;
			}
			listFolders.Add(Path.Combine(folderLocalAppData,LocalAppDataFolderName));
			string folderLegacy=Path.Combine(folderLocalAppData,LegacyLocalAppDataFolderName);
			if(!string.Equals(folderLegacy,listFolders[0],StringComparison.OrdinalIgnoreCase)) {
				listFolders.Add(folderLegacy);
			}
			return listFolders;
		}
	}
}
