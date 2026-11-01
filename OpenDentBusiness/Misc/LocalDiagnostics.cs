// M2-MISC (Open Dental 25.4, "more details in the Diagnostics window"): the text of Help, About, Diagnostics. Everything is read on this
// computer and from this office's own database server; nothing is sent anywhere. Replaces BugSubmissions.GetDiagnostics, which built the text from
// the record Open Dental sent to its headquarters with a bug submission.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using CodeBase;
using DataConnectionBase;
using Microsoft.Win32;

namespace OpenDentBusiness {
	///<summary>Builds the Diagnostics text shown from Help, About. Local only: it reads this workstation (through .NET and WMI) and the database
	///server it is connected to. Every line is gathered on its own, so one failure prints an ERROR line instead of stopping the rest. Passwords,
	///password hashes and keys are never included.</summary>
	public static class LocalDiagnostics {
		///<summary>The line between sections.</summary>
		public const string SEPARATOR="-------------";
		private const string FORMAT_DATE_TIME="yyyy-MM-dd HH:mm:ss";

		///<summary>The preferences shown in the Preferences section. The same list Open Dental's diagnostics showed, without the registration key
		///rows (this program has no registration keys) and without the two MySQL password hashes.</summary>
		private static readonly List<PrefName> _listPrefNamesShown=new List<PrefName>() {
			PrefName.AtoZfolderUsed,
			PrefName.ClaimSnapshotEnabled,
			PrefName.ClaimSnapshotRunTime,
			PrefName.ClaimSnapshotTriggerType,
			PrefName.CorruptedDatabase,
			PrefName.DataBaseVersion,
			PrefName.EasyNoClinics,
			PrefName.LanguageAndRegion,
			PrefName.MySqlVersion,
			PrefName.PayPlansVersion,
			PrefName.ProcessSigsIntervalInSecs,
			PrefName.ProgramVersion,
			PrefName.RandomPrimaryKeys,
			PrefName.ReplicationFailureAtServer_id,
			PrefName.ReportingServerCompName,
			PrefName.ReportingServerDbName,
			PrefName.ReportingServerMySqlUser,
			PrefName.ReportingServerURI,
			PrefName.SecurityLogOffAfterMinutes,
			PrefName.WebServiceServerName,
			PrefName.ReadOnlyServerCompName,
			PrefName.ReadOnlyServerDbName,
			PrefName.ReadOnlyServerMySqlUser,
			PrefName.ReadOnlyServerURI,
		};

		///<summary>The preferences shown in the Preferences section, in order. A copy.</summary>
		public static List<PrefName> GetPrefNamesShown() {
			return new List<PrefName>(_listPrefNamesShown);
		}

		///<summary>True for a preference name that can hold a password, a password hash, a key or a token. Such values are never shown, even if a
		///name is added to the list by mistake.</summary>
		public static bool IsSecretPrefName(string prefName) {
			if(string.IsNullOrEmpty(prefName)) {
				return false;
			}
			string name=prefName.ToLowerInvariant();
			return name.Contains("pass")//Password, PassHash, Passphrase
				|| name.Contains("secret")
				|| name.Contains("token")
				|| name.Contains("registrationkey")
				|| name.Contains("apikey")
				|| name.EndsWith("key");
		}

		///<summary>The full Diagnostics text. patNum is the selected patient, or -1 when none.</summary>
		public static string GetDiagnostics(long patNum=-1) {
			Meth.NoCheckMiddleTierRole();
			DiagnosticsText diagnosticsText=new DiagnosticsText();
			DateTime dateTimeWorkstation=DateTime.Now;
			//One round trip for the server's clock, version and time zone, so the clock difference is measured close to dateTimeWorkstation.
			DataTable tableServer=null;
			string errorServer="";
			try {
				tableServer=GetServerInfo();
			}
			catch(Exception ex) {
				errorServer=ex.Message;
			}
			Func<string,string> funcServerValue=(column) => {
				if(tableServer==null) {
					throw new ApplicationException(errorServer);
				}
				return PIn.String(tableServer.Rows[0][column].ToString());
			};
			AddProgram(diagnosticsText);
			AddDateTime(diagnosticsText,dateTimeWorkstation,funcServerValue);
			AddComputer(diagnosticsText);
			AddDatabase(diagnosticsText,funcServerValue);
			AddSession(diagnosticsText,patNum);
			AddPreferences(diagnosticsText);
			return diagnosticsText.ToString();
		}

		#region Sections
		private static void AddProgram(DiagnosticsText diagnosticsText) {
			diagnosticsText.AddSection("Program");
			diagnosticsText.AddLine("Product",() => CodeBase.VendorDirect.ProductIdentity.ProductName);
			diagnosticsText.AddLine("Release version",() => ForkVersions.GetReleaseVersion());
			diagnosticsText.AddLine("Program version (preference)",() => PrefC.GetString(PrefName.ProgramVersion));
			diagnosticsText.AddLine("OpenDentBusiness version",() => MiscData.GetAssemblyVersion());
			diagnosticsText.AddLine("Program path",() => GetProgramPath());
			diagnosticsText.AddLine(".NET runtime",() => "CLR "+Environment.Version);
			diagnosticsText.AddLine(".NET Framework release",() => GetNetFrameworkRelease());
			diagnosticsText.AddLine("Process",() => Environment.Is64BitProcess ? "64-bit" : "32-bit");
			diagnosticsText.AddLine("Middle tier role",() => RemotingClient.MiddleTierRole.ToString());
			diagnosticsText.AddLine("Middle tier OpenDentBusiness version",() => MiscData.GetAssemblyVersionForMiddleTier());
			diagnosticsText.AddLine("Culture",() => CultureInfo.CurrentCulture.Name);
		}

		private static void AddDateTime(DiagnosticsText diagnosticsText,DateTime dateTimeWorkstation,Func<string,string> funcServerValue) {
			diagnosticsText.AddSection("Date and time");
			diagnosticsText.AddLine("Workstation",() => dateTimeWorkstation.ToString(FORMAT_DATE_TIME,CultureInfo.InvariantCulture));
			diagnosticsText.AddLine("Workstation time zone",() => GetTimeZoneText(TimeZoneInfo.Local,dateTimeWorkstation));
			diagnosticsText.AddLine("Database server",() => FormatServerDateTime(funcServerValue("ServerNow")));
			diagnosticsText.AddLine("Database server time zone",() => funcServerValue("SystemTimeZone")+" (session: "+funcServerValue("SessionTimeZone")+")");
			diagnosticsText.AddLine("Difference (server minus workstation)",() => {
				DateTime dateTimeServer=ParseServerDateTime(funcServerValue("ServerNow"));
				return GetClockDifferenceText(dateTimeWorkstation,dateTimeServer);
			});
		}

		private static void AddComputer(DiagnosticsText diagnosticsText) {
			diagnosticsText.AddSection("Computer");
			diagnosticsText.AddLine("Computer name",() => Environment.MachineName);
			diagnosticsText.AddLine("Operating system",() => GetWmiValue("Win32_OperatingSystem","Caption"));
			diagnosticsText.AddLine("OS version and build",() => GetWmiValue("Win32_OperatingSystem","Version")+" build "+GetWmiValue("Win32_OperatingSystem","BuildNumber"));
			diagnosticsText.AddLine("OS architecture",() => Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");
			diagnosticsText.AddLine("Windows session",() => Environment.GetEnvironmentVariable("SESSIONNAME")??"");
			diagnosticsText.AddLine("CPU",() => GetWmiValue("Win32_Processor","Name"));
			diagnosticsText.AddLine("CPU cores",() => GetWmiSum("Win32_Processor","NumberOfCores")+" cores, "+Environment.ProcessorCount+" logical processors");
			diagnosticsText.AddLine("Memory total",() => FormatBytes((long)new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory));
			diagnosticsText.AddLine("Memory free",() => FormatBytes((long)new Microsoft.VisualBasic.Devices.ComputerInfo().AvailablePhysicalMemory));
			diagnosticsText.AddLine("Screens",() => GetScreensText());
			diagnosticsText.AddLine("Screen DPI",() => GetDpiText());
			diagnosticsText.AddLine("Free disk, program drive",() => GetFreeSpaceText(GetProgramPath()));
			diagnosticsText.AddLine("Image folder",() => GetImageFolderText());
			diagnosticsText.AddLine("Free disk, image folder drive",() => {
				if(PrefC.AtoZfolderUsed!=DataStorageType.LocalAtoZ) {
					return "(images are not stored in a folder)";
				}
				return GetFreeSpaceText(ImageStore.GetPreferredAtoZpath());
			});
		}

		private static void AddDatabase(DiagnosticsText diagnosticsText,Func<string,string> funcServerValue) {
			diagnosticsText.AddSection("Database");
			diagnosticsText.AddLine("Server name",() => DataConnection.GetServerName());
			diagnosticsText.AddLine("Database name",() => DataConnection.GetDatabaseName());
			diagnosticsText.AddLine("MySQL user",() => DataConnection.GetMysqlUser());
			diagnosticsText.AddLine("Server version",() => funcServerValue("ServerVersion"));
			diagnosticsText.AddLine("Default storage engine",() => funcServerValue("StorageEngine"));
			diagnosticsText.AddLine("Open Dental schema",() => ForkVersions.GetMarker(ForkVersions.PREF_OD_SCHEMA_COMPAT)??"");
			diagnosticsText.AddLine("Fork schema level",() => ForkVersions.GetMarker(ForkVersions.PREF_FORK_SCHEMA_LEVEL)??"");
			diagnosticsText.AddLine("Adopted from",() => ForkVersions.GetMarker(ForkVersions.PREF_FORK_ADOPTED_FROM)??"");
			diagnosticsText.AddLine("Replication",() => ReplicationServers.IsUsingReplication() ? "true" : "false");
			diagnosticsText.AddLine("Clinics",() => {
				int countTotal=Clinics.GetCount();
				return countTotal+" ("+(countTotal-Clinics.GetCount(true))+" hidden)";
			});
			diagnosticsText.AddLine("Enabled plugins",() => {
				//External plugins have no program name, so use the description with the plugin file name.
				List<string> listPlugins=Programs.GetWhere(x => x.Enabled && !string.IsNullOrWhiteSpace(x.PluginDllName))
					.Select(x => x.ProgName=="" ? x.ProgDesc+"("+x.PluginDllName+")" : x.ProgName).ToList();
				return listPlugins.Count==0 ? "none" : string.Join(", ",listPlugins);
			});
			diagnosticsText.AddLine("Last update",() => {
				List<UpdateHistory> listUpdateHistories=UpdateHistories.GetPreviousUpdateHistories(2);//newest first
				if(listUpdateHistories.Count==0) {
					return "none recorded";
				}
				string text=listUpdateHistories[0].ProgramVersion+" on "
					+listUpdateHistories[0].DateTimeUpdated.ToString(FORMAT_DATE_TIME,CultureInfo.InvariantCulture);
				if(listUpdateHistories.Count>1) {
					text+=", from "+listUpdateHistories[1].ProgramVersion;
				}
				return text;
			});
		}

		private static void AddSession(DiagnosticsText diagnosticsText,long patNum) {
			diagnosticsText.AddSection("Session");
			diagnosticsText.AddLine("Workstation name in the program",() => Security.CurComputerName);
			diagnosticsText.AddLine("UserNum",() => Security.CurUser?.UserNum.ToString()??"");
			diagnosticsText.AddLine("PatNum",() => patNum>0 ? patNum.ToString() : "none selected");
			diagnosticsText.AddLine("ClinicNum",() => Clinics.ClinicNum.ToString());
		}

		private static void AddPreferences(DiagnosticsText diagnosticsText) {
			diagnosticsText.AddSection("Preferences");
			for(int i=0;i<_listPrefNamesShown.Count;i++) {
				PrefName prefName=_listPrefNamesShown[i];
				if(IsSecretPrefName(prefName.ToString())) {
					continue;
				}
				diagnosticsText.AddLine(prefName.ToString(),() => Prefs.GetOne(prefName).ValueString);
			}
		}
		#endregion Sections

		#region Helpers (no database)
		///<summary>The difference between the database server's clock and the workstation's, for example "+3 seconds (server ahead)",
		///"-1 minute 5 seconds (server behind)" or "none". Rounded to whole seconds.</summary>
		public static string GetClockDifferenceText(DateTime dateTimeWorkstation,DateTime dateTimeServer) {
			long seconds=(long)Math.Round((dateTimeServer-dateTimeWorkstation).TotalSeconds,MidpointRounding.AwayFromZero);
			if(seconds==0) {
				return "none";
			}
			long secondsAbs=Math.Abs(seconds);
			List<string> listParts=new List<string>();
			long days=secondsAbs/86400;
			long hours=(secondsAbs%86400)/3600;
			long minutes=(secondsAbs%3600)/60;
			long secondsRest=secondsAbs%60;
			if(days>0) {
				listParts.Add(days+(days==1 ? " day" : " days"));
			}
			if(hours>0) {
				listParts.Add(hours+(hours==1 ? " hour" : " hours"));
			}
			if(minutes>0) {
				listParts.Add(minutes+(minutes==1 ? " minute" : " minutes"));
			}
			if(secondsRest>0) {
				listParts.Add(secondsRest+(secondsRest==1 ? " second" : " seconds"));
			}
			return (seconds>0 ? "+" : "-")+string.Join(" ",listParts)+(seconds>0 ? " (server ahead)" : " (server behind)");
		}

		///<summary>A byte count in binary units with one decimal, for example "15.9 GB". Negative counts give "unknown".</summary>
		public static string FormatBytes(long bytes) {
			if(bytes<0) {
				return "unknown";
			}
			string[] arrayUnits=new string[] { "bytes","KB","MB","GB","TB" };
			double value=bytes;
			int idxUnit=0;
			while(value>=1024 && idxUnit<arrayUnits.Length-1) {
				value/=1024;
				idxUnit++;
			}
			if(idxUnit==0) {
				return bytes+" bytes";
			}
			return value.ToString("0.0",CultureInfo.InvariantCulture)+" "+arrayUnits[idxUnit];
		}

		///<summary>A time zone with its current UTC offset, for example "Pacific Standard Time (UTC-08:00)".</summary>
		public static string GetTimeZoneText(TimeZoneInfo timeZoneInfo,DateTime dateTime) {
			TimeSpan timeSpanOffset=timeZoneInfo.GetUtcOffset(dateTime);
			string offset=(timeSpanOffset<TimeSpan.Zero ? "-" : "+")+timeSpanOffset.Duration().ToString(@"hh\:mm");
			string name=timeZoneInfo.IsDaylightSavingTime(dateTime) ? timeZoneInfo.DaylightName : timeZoneInfo.StandardName;
			return name+" (UTC"+offset+")";
		}

		///<summary>The server's NOW() text as a date and time. Throws if it can't be read.</summary>
		public static DateTime ParseServerDateTime(string serverNow) {
			return DateTime.Parse(serverNow,CultureInfo.InvariantCulture,DateTimeStyles.None);
		}

		private static string FormatServerDateTime(string serverNow) {
			return ParseServerDateTime(serverNow).ToString(FORMAT_DATE_TIME,CultureInfo.InvariantCulture);
		}
		#endregion Helpers (no database)

		#region Helpers (this computer)
		///<summary>The folder the program runs from.</summary>
		private static string GetProgramPath() {
			Assembly assembly=Assembly.GetEntryAssembly();
			if(assembly!=null && !string.IsNullOrEmpty(assembly.Location)) {
				return Path.GetDirectoryName(assembly.Location);
			}
			return AppDomain.CurrentDomain.BaseDirectory;
		}

		///<summary>The installed .NET Framework 4 release number from the registry, for example "533320 (4.8.1 or later)".</summary>
		private static string GetNetFrameworkRelease() {
			using(RegistryKey registryKey=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32)
				.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
			{
				object release=registryKey?.GetValue("Release");
				if(release==null) {
					return "not found";
				}
				int intRelease=Convert.ToInt32(release);
				return intRelease+" ("+GetNetFrameworkName(intRelease)+")";
			}
		}

		///<summary>The .NET Framework version a registry release number stands for, from Microsoft's published table.</summary>
		public static string GetNetFrameworkName(int release) {
			if(release>=533320) {
				return "4.8.1 or later";
			}
			if(release>=528040) {
				return "4.8";
			}
			if(release>=461808) {
				return "4.7.2";
			}
			if(release>=461308) {
				return "4.7.1";
			}
			if(release>=460798) {
				return "4.7";
			}
			return "older than 4.7";
		}

		///<summary>One property of the first WMI object of a class on this computer. Throws if WMI does not answer.</summary>
		private static string GetWmiValue(string wmiClass,string property) {
			using(ManagementObjectSearcher managementObjectSearcher=new ManagementObjectSearcher("SELECT "+property+" FROM "+wmiClass)) {
				foreach(ManagementBaseObject managementBaseObject in managementObjectSearcher.Get()) {
					return (managementBaseObject[property]?.ToString()??"").Trim();
				}
			}
			return "";
		}

		///<summary>The sum of a numeric WMI property over every object of a class, for example the cores of every processor.</summary>
		private static long GetWmiSum(string wmiClass,string property) {
			long sum=0;
			using(ManagementObjectSearcher managementObjectSearcher=new ManagementObjectSearcher("SELECT "+property+" FROM "+wmiClass)) {
				foreach(ManagementBaseObject managementBaseObject in managementObjectSearcher.Get()) {
					sum+=Convert.ToInt64(managementBaseObject[property]??0);
				}
			}
			return sum;
		}

		private static string GetScreensText() {
			System.Windows.Forms.Screen[] arrayScreens=System.Windows.Forms.Screen.AllScreens;
			List<string> listScreens=new List<string>();
			for(int i=0;i<arrayScreens.Length;i++) {
				listScreens.Add(arrayScreens[i].Bounds.Width+"x"+arrayScreens[i].Bounds.Height+(arrayScreens[i].Primary ? " (primary)" : ""));
			}
			return arrayScreens.Length+": "+string.Join(", ",listScreens);
		}

		private static string GetDpiText() {
			using(System.Drawing.Graphics graphics=System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) {
				return graphics.DpiX.ToString("0",CultureInfo.InvariantCulture)+" ("
					+(graphics.DpiX/96f*100f).ToString("0",CultureInfo.InvariantCulture)+"%)";
			}
		}

		private static string GetImageFolderText() {
			switch(PrefC.AtoZfolderUsed) {
				case DataStorageType.InDatabase:
					return "images are stored in the database";
				case DataStorageType.LocalAtoZ:
					return ImageStore.GetPreferredAtoZpath();
				default:
					return PrefC.AtoZfolderUsed.ToString();
			}
		}

		///<summary>Free and total space of the drive or network share that holds the path.</summary>
		private static string GetFreeSpaceText(string path) {
			if(string.IsNullOrWhiteSpace(path)) {
				return "no path";
			}
			string root=Path.GetPathRoot(path);
			if(!GetDiskFreeSpaceEx(root,out ulong bytesFreeToCaller,out ulong bytesTotal,out ulong bytesFreeTotal)) {
				throw new ApplicationException("Unable to read the free space of "+root);
			}
			return FormatBytes((long)bytesFreeToCaller)+" free of "+FormatBytes((long)bytesTotal)+" ("+root+")";
		}

		///<summary>Works for drive letters and for network shares (\\server\share\), unlike DriveInfo.</summary>
		[DllImport("kernel32.dll",SetLastError=true,CharSet=CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetDiskFreeSpaceEx(string directoryName,out ulong bytesFreeToCaller,out ulong bytesTotal,out ulong bytesFreeTotal);
		#endregion Helpers (this computer)

		#region Database server
		///<summary>One row: ServerNow, ServerVersion, SystemTimeZone, SessionTimeZone, StorageEngine.</summary>
		public static DataTable GetServerInfo() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod());
			}
			string command="SELECT DATE_FORMAT(NOW(),'%Y-%m-%d %H:%i:%s') ServerNow,@@version ServerVersion,@@system_time_zone SystemTimeZone,"
				+"@@time_zone SessionTimeZone,"
				+"(SELECT ENGINE FROM information_schema.ENGINES WHERE Support='DEFAULT' LIMIT 1) StorageEngine";
			return Db.GetTable(command);
		}
		#endregion Database server

		///<summary>The Diagnostics text: sections of "label: value" lines. Each value is read inside its own try/catch.</summary>
		public class DiagnosticsText {
			private readonly StringBuilder _stringBuilder=new StringBuilder();

			///<summary>Starts a section with a separator line and its title.</summary>
			public void AddSection(string title) {
				_stringBuilder.AppendLine(SEPARATOR);
				_stringBuilder.AppendLine(title);
			}

			///<summary>Adds "  label: value". If reading the value throws, adds "  label: ERROR: message" and carries on.</summary>
			public void AddLine(string label,Func<string> funcValue) {
				string value;
				try {
					value=funcValue()??"";
				}
				catch(Exception ex) {
					value="ERROR: "+ex.Message;
				}
				_stringBuilder.AppendLine("  "+label+": "+value);
			}

			public override string ToString() {
				return _stringBuilder.ToString();
			}
		}
	}
}
