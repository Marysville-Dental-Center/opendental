// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using OpenDentBusiness;

namespace AdoptionValidation {
	public enum OdStartupOutcome {
		///<summary>The client opens the database.</summary>
		Proceeds,
		///<summary>The client runs its own database conversion, then opens the database.</summary>
		ConvertsAndProceeds,
		///<summary>The client publishes its own binaries to the database (UpdateFiles), sets ProgramVersion and installs its services.</summary>
		PublishesUpdateAndProceeds,
		///<summary>The client exits without converting or using the database.</summary>
		Exits,
	}

	///<summary>An Open Dental client as the startup check sees it.</summary>
	public class OdClient {
		public Version Version;
		///<summary>Open Dental skips CheckProgramVersion in Debug builds. Installed clients are Release builds.</summary>
		public bool IsDebugBuild;
		///<summary>True when this workstation is the one WebServiceServerName names (or that preference is blank): it may convert and publish.</summary>
		public bool IsUpdateServer=true;
		///<summary>True when an Open Dental Setup.exe sits in the A to Z folder (used only by the "classic" update view).</summary>
		public bool HasSetupInAtoZ;

		public override string ToString() {
			return "Open Dental "+Version.ToString(3)+(IsDebugBuild ? " (Debug build)" : "")+(IsUpdateServer ? ", update server" : ", workstation")
				+(HasSetupInAtoZ ? ", Setup.exe in A to Z" : "");
		}
	}

	///<summary>What an Open Dental client reads at startup, taken from the database.</summary>
	public class OdDatabaseState {
		public string DataBaseVersion;
		public string ProgramVersion;
		public bool IsCorrupted;
		public bool IsClassicUpdateView;
		public string WebServiceServerName="";
		public string UpdateWebsitePath="";
		public string UpdateCode="";
		///<summary>Rows of Open Dental's UpdateFilesSegment type with the consecutive file names 0001, 0002, ... that its client joins.</summary>
		public long CountUpdateSegmentsJoined;
		public long CountUpdateFileRows;

		///<summary>Reads the state through the connected business layer (plain SQL, no cache).</summary>
		public static OdDatabaseState Read() {
			OdDatabaseState state=new OdDatabaseState();
			state.DataBaseVersion=BusinessLayer.GetPref("DataBaseVersion");
			state.ProgramVersion=BusinessLayer.GetPref("ProgramVersion");
			state.IsCorrupted=(BusinessLayer.GetPref("CorruptedDatabase")??"0")=="1";
			state.IsClassicUpdateView=(BusinessLayer.GetPref("UpdateWindowShowsClassicView")??"0")=="1";
			state.WebServiceServerName=BusinessLayer.GetPref("WebServiceServerName")??"";
			state.UpdateWebsitePath=BusinessLayer.GetPref("UpdateWebsitePath")??"";
			state.UpdateCode=BusinessLayer.GetPref("UpdateCode")??"";
			state.CountUpdateFileRows=BusinessLayer.Count("SELECT COUNT(*) FROM documentmisc WHERE DocMiscType IN ("
				+(int)DocumentMiscType.UpdateFiles+","+(int)DocumentMiscType.UpdateFilesSegment+")");
			long count=0;
			while(BusinessLayer.Count("SELECT COUNT(*) FROM documentmisc WHERE DocMiscType="+(int)DocumentMiscType.UpdateFilesSegment
				+" AND FileName='"+(count+1).ToString("0000",CultureInfo.InvariantCulture)+"'")>0)
			{
				count++;
			}
			state.CountUpdateSegmentsJoined=count;
			return state;
		}
	}

	public class OdStartupResult {
		public OdStartupOutcome Outcome;
		///<summary>Open Dental's exit code when the outcome is Exits (0 when it just closes).</summary>
		public int ExitCode;
		public bool RunsConversion;
		///<summary>True when the client would write to the database during the version checks (conversion, UpdateFiles, ProgramVersion).</summary>
		public bool WritesDatabase;
		public bool InstallsServices;
		///<summary>True when the client would copy program files out of the database's UpdateFiles rows.</summary>
		public bool CopiesBinariesFromDatabase;
		///<summary>True when the client would try to download a Setup.exe (from UpdateWebsitePath) or launch the one in the A to Z folder.</summary>
		public bool TriesSetup;
		public List<string> ListTrace=new List<string>();

		public bool IsRefused {
			get { return Outcome==OdStartupOutcome.Exits && !RunsConversion && !WritesDatabase && !InstallsServices && !CopiesBinariesFromDatabase; }
		}

		public string Describe() {
			return Outcome+(Outcome==OdStartupOutcome.Exits ? " (exit code "+ExitCode+")" : "")
				+"; conversion "+(RunsConversion ? "yes" : "no")+", database writes "+(WritesDatabase ? "yes" : "no")
				+", services installed "+(InstallsServices ? "yes" : "no")+", binaries copied from the database "+(CopiesBinariesFromDatabase ? "yes" : "no")
				+", Setup.exe tried "+(TriesSetup ? "yes" : "no");
		}
	}

	///<summary>An executable model of the two version checks an Open Dental client runs at startup, in this order: ClassConvertDatabase.Convert
	///(through PrefL.ConvertDB) and PrefL.CheckProgramVersion. It follows the published GPL source of Open Dental 24.3.63 (the 24_3 branch of this
	///repository, OpenDental/Misc/ClassConvertDatabase.cs and OpenDental/Logic/PrefL.cs), the last public source; backlog risk R1 describes the
	///same path. It never runs Open Dental and never reads update files. Open Dental 25.3 is closed source, so a one-time confirmation with the
	///licensed client on a copy stays an owner decision (R1, strategy item 5).
	///Not modelled, because they do not depend on the version numbers: the checks before ConvertDB (read-only and report server versions, index
	///re-enabling on a restored backup, and the MySQL version check, which can update the MySqlVersion preference and back up, repair and optimize
	///tables when the server version changed), dynamic mode, trial builds and the middle tier.</summary>
	public static class OdStartupCheck {
		public static OdStartupResult Evaluate(OdClient client,OdDatabaseState state) {
			OdStartupResult result=new OdStartupResult();
			Version versionDb=ParseVersion(state.DataBaseVersion);
			Version versionStored=ParseVersion(state.ProgramVersion);
			if(versionDb==null || versionStored==null) {
				result.Outcome=OdStartupOutcome.Exits;
				result.ListTrace.Add("DataBaseVersion or ProgramVersion is not a version number; new Version() throws and the client stops.");
				return result;
			}
			Version versionClient=client.Version;
			//1. ClassConvertDatabase.Convert(DataBaseVersion, client version).
			if(versionDb>=new Version(3,4,0) && state.IsCorrupted) {
				return Exit(result,201,"Convert: CorruptedDatabase is set; the client refuses the database (exit 201).");
			}
			if(versionDb==versionClient) {
				result.ListTrace.Add("Convert: DataBaseVersion equals the client version; no conversion.");
			}
			else if(versionDb>versionClient) {
				result.ListTrace.Add("Convert: DataBaseVersion "+versionDb+" is newer than the client "+versionClient+"; conversion is skipped and left to "
					+"CheckProgramVersion.");
			}
			else if(!client.IsUpdateServer) {
				result.ListTrace.Add("Convert: DataBaseVersion is older, but this workstation is not the update server; left to CheckProgramVersion.");
			}
			else {
				result.RunsConversion=true;
				result.WritesDatabase=true;
				result.ListTrace.Add("Convert: DataBaseVersion "+versionDb+" is older than the client "+versionClient+"; the client converts the database.");
				versionDb=versionClient;
			}
			//2. PrefL.CheckProgramVersion.
			if(client.IsDebugBuild) {
				result.ListTrace.Add("CheckProgramVersion: skipped in Debug builds.");
				return Proceed(result);
			}
			if(state.IsClassicUpdateView) {
				return EvaluateClassic(client,state,versionStored,result);
			}
			if(versionStored>versionClient) {
				result.ListTrace.Add("CheckProgramVersion: ProgramVersion "+versionStored+" is newer than the client; it must update itself from the "
					+"UpdateFiles in the database and restart.");
				if(state.CountUpdateSegmentsJoined==0) {
					result.ListTrace.Add("UpdateClientFromServerUpdateFiles: no UpdateFilesSegment rows, so nothing is unpacked and Manifest.txt is "
						+"missing. The manifest check fails; the client offers to download Setup.exe or exits (exit 312).");
					TrySetupDownload(state,result);
					return Exit(result,312,"The client exits without using the database.");
				}
				result.CopiesBinariesFromDatabase=true;
				result.ListTrace.Add("UpdateClientFromServerUpdateFiles: "+state.CountUpdateSegmentsJoined+" UpdateFilesSegment rows are joined and "
					+"unpacked; if their Manifest.txt says "+versionStored.ToString(3)+" the files are copied over the client.");
				return Exit(result,0,"The client restarts after copying files.");
			}
			if(versionStored<versionClient) {
				if(!client.IsUpdateServer) {
					result.ListTrace.Add("CheckProgramVersion: the client is newer than ProgramVersion and this is not the update server; it offers a "
						+"downgrade from the UpdateFiles in the database.");
					if(state.CountUpdateSegmentsJoined>0) {
						result.CopiesBinariesFromDatabase=true;
					}
					return Exit(result,0,"The client exits (or restarts after the downgrade).");
				}
				result.Outcome=OdStartupOutcome.PublishesUpdateAndProceeds;
				result.WritesDatabase=true;
				result.InstallsServices=true;
				result.ListTrace.Add("CheckProgramVersion: the client is newer than ProgramVersion on the update server; it zips itself into UpdateFiles, "
					+"rehashes, sets ProgramVersion and installs the Open Dental Service, eConnector and API service.");
			}
			if(versionClient<versionDb) {
				return Exit(result,309,"CheckProgramVersion: the client is older than DataBaseVersion (exit 309).");
			}
			if(result.Outcome==OdStartupOutcome.PublishesUpdateAndProceeds) {
				return result;
			}
			return Proceed(result);
		}

		private static OdStartupResult EvaluateClassic(OdClient client,OdDatabaseState state,Version versionStored,OdStartupResult result) {
			result.ListTrace.Add("CheckProgramVersion: the classic update view is on (UpdateWindowShowsClassicView).");
			if(versionStored<client.Version) {
				result.WritesDatabase=true;
				result.ListTrace.Add("Classic: the client is newer; it sets ProgramVersion and adds an updatehistory row.");
				return Proceed(result);
			}
			if(versionStored==client.Version) {
				return Proceed(result);
			}
			if(client.HasSetupInAtoZ) {
				result.TriesSetup=true;
				return Exit(result,0,"Classic: ProgramVersion is newer; the client launches the Setup.exe in the A to Z folder (it installs Open Dental on "
					+"this workstation; the database is not touched) and exits.");
			}
			TrySetupDownload(state,result);
			return Exit(result,0,"Classic: ProgramVersion is newer and there is no Setup.exe in the A to Z folder; the client exits.");
		}

		private static void TrySetupDownload(OdDatabaseState state,OdStartupResult result) {
			if(string.IsNullOrWhiteSpace(state.UpdateWebsitePath)) {
				result.ListTrace.Add("Setup download: UpdateWebsitePath is blank, so there is nothing to download.");
				return;
			}
			result.TriesSetup=true;
			result.ListTrace.Add("Setup download: the client asks Open Dental's update site (UpdateWebsitePath) for a Setup.exe of version "
				+state.ProgramVersion+". The database is not touched.");
		}

		private static OdStartupResult Exit(OdStartupResult result,int exitCode,string trace) {
			result.Outcome=OdStartupOutcome.Exits;
			result.ExitCode=exitCode;
			result.ListTrace.Add(trace);
			return result;
		}

		private static OdStartupResult Proceed(OdStartupResult result) {
			result.Outcome=result.RunsConversion ? OdStartupOutcome.ConvertsAndProceeds : OdStartupOutcome.Proceeds;
			result.ListTrace.Add("The client opens the database.");
			return result;
		}

		private static Version ParseVersion(string value) {
			if(string.IsNullOrWhiteSpace(value) || !Version.TryParse(value.Trim(),out Version version)) {
				return null;
			}
			return new Version(version.Major,version.Minor,Math.Max(version.Build,0),Math.Max(version.Revision,0));
		}

		///<summary>The clients the rehearsal tries against each database: the office's own 25.3.48, the frozen 24.3.63 line, and later Open Dental
		///releases an office might install by mistake; each as an ordinary workstation, as the update server, with the classic update view, and as
		///a Debug build.</summary>
		public static List<(string name,OdClient client,bool isClassicView)> StandardScenarios() {
			List<(string,OdClient,bool)> list=new List<(string,OdClient,bool)>();
			foreach(Version version in new[] { new Version(25,3,48,0),new Version(24,3,63,0),new Version(25,4,30,0),new Version(26,1,70,0) }) {
				list.Add(("Open Dental "+version.ToString(3)+", update server",new OdClient { Version=version,IsUpdateServer=true },false));
				list.Add(("Open Dental "+version.ToString(3)+", workstation",new OdClient { Version=version,IsUpdateServer=false },false));
			}
			list.Add(("Open Dental 25.3.48, classic update view, Setup.exe in A to Z",
				new OdClient { Version=new Version(25,3,48,0),IsUpdateServer=true,HasSetupInAtoZ=true },true));
			list.Add(("Open Dental 25.3.48, Debug build",new OdClient { Version=new Version(25,3,48,0),IsDebugBuild=true },false));
			return list;
		}
	}
}
