// The checklist's set-up prechecks (0.3, read-only queries on the scratch copy) and its clean-up (Z): no process the run started is left, the
// files the checks add are gone, the Release folder is as before, the worktree is clean, and the copy's state is compared with the prechecks
// before the copy is dropped (AssemblyCleanup).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WindowChecksTests {
	///<summary>Not a test class: WinChkFixture calls RunPrechecks once the copy is made and RunCleanUp at the end of the run.</summary>
	public static class PZTests {
		public const string QUERY_COUNTS="SELECT (SELECT COUNT(*) FROM snomed) snomed,(SELECT COUNT(*) FROM ehrprovkey) provkeys,(SELECT COUNT(*) FROM diseasedef) problems,"
			+"(SELECT COUNT(*) FROM eformdef) eforms,(SELECT COUNT(*) FROM imagingdevice) devices,(SELECT COUNT(*) FROM employee) employees,"
			+"(SELECT COUNT(*) FROM payperiod) payperiods,(SELECT COUNT(*) FROM document WHERE PatNum=1) docs,(SELECT COUNT(*) FROM sheetdef) sheetdefs,"
			+"(SELECT COUNT(*) FROM referral) referrals,(SELECT COUNT(*) FROM refattach WHERE PatNum=1) refattach,"
			+"(SELECT COUNT(*) FROM perioexam WHERE PatNum=1) perio,(SELECT COUNT(*) FROM procedurelog WHERE PatNum=1 AND ProcStatus<>6) procs,"
			+"(SELECT COUNT(*) FROM commlog WHERE PatNum=1) commlogs,(SELECT COUNT(*) FROM treatplan WHERE PatNum=1 AND TPStatus=0) savedtps,"
			+"(SELECT COUNT(*) FROM grouppermission WHERE UserGroupNum=1) adminperms,(SELECT COUNT(*) FROM grouppermission WHERE PermType=173) dashperms,"
			+"(SELECT COUNT(*) FROM userodpref WHERE UserNum=1) adminprefs,(SELECT COUNT(*) FROM userodpref WHERE UserNum=1 AND FkeyType=22) provcolour,"
			+"(SELECT COUNT(*) FROM usergroup) groups,(SELECT COUNT(*) FROM emailaddress) emailaddrs";
		private static string _countsAtPrecheck="";
		public const string QUERY_PREFS="SELECT PrefName,ValueString FROM preference WHERE PrefName IN ('ShowFeatureEhr','ForkTimeSyncMode','EhrEmergencyNow',"
			+"'IsAlertRadiologyProcsEnabled','ProgramVersion','ReportFolderName','CommLogAutoSave','EasyHideCapitation','EasyHideMedicaid','EasyHideInsurance',"
			+"'EasyHideClinical','EasyNoClinics','ShowFeatureMedicalInsurance') ORDER BY PrefName";

		///<summary>0.3 on the copy, once per run, right after the copy is made (WinChkFixture.Begin): the base Z compares with.</summary>
		public static void RunPrechecks() {
			CheckResult checkResult=new CheckResult("P0","0.3 prechecks on the scratch copy");
			List<string> listLines=new List<string>();
			string patient=ScratchDb.Rows("SELECT PatNum,LName,FName,PriProv FROM patient WHERE PatNum=1");
			listLines.Add("1. "+patient);
			checkResult.Expect(patient.StartsWith("1|Gatetest|Stage|"),"#1 patient #1 is Gatetest, Stage ("+patient+")");
			string claims=ScratchDb.Rows("SELECT ClaimNum,ClaimStatus,ClaimType FROM claim WHERE PatNum=1");
			listLines.Add("2. "+claims);
			checkResult.Expect(claims.Contains("1|R|P") && claims.Contains("2|R|PreAuth"),"#2 claim 1 and preauth 2 ("+claims+")");
			listLines.Add("3. "+ScratchDb.Rows("SELECT ProvNum,Abbr,LName,FName FROM provider"));
			string computerPref=ScratchDb.Rows("SELECT ComputerPrefNum,ComputerName,GraphicsSimple,DirectXFormat FROM computerpref WHERE ComputerName='"
				+Environment.MachineName+"'");
			listLines.Add("4. this PC's row: "+computerPref);
			checkResult.Expect(computerPref.Split('|').Length>2 && (computerPref.Split('|')[2]=="0" || computerPref.Split('|')[2]=="False"),
				"#4 GraphicsSimple 0 (DirectX) for this PC (the client reads the tinyint as False)");
			string prefs=ScratchDb.Rows(QUERY_PREFS);
			listLines.Add("5. "+prefs);
			checkResult.Expect(!prefs.Contains("ForkTimeSyncMode|1"),"#5 ForkTimeSyncMode absent or 0");
			checkResult.Expect(prefs.Contains("ProgramVersion|99.0.0.0"),"#5 ProgramVersion 99.0.0.0");
			checkResult.Expect(prefs.Contains("ShowFeatureEhr|0") || prefs.Contains("ShowFeatureEhr|;") || prefs.EndsWith("ShowFeatureEhr|"),"#5 ShowFeatureEhr 0 or empty");
			string counts=ScratchDb.Rows(QUERY_COUNTS);
			listLines.Add("6. "+counts);
			File.WriteAllText(Path.Combine(WinChkEnv.OutFolder,"P0-counts.txt"),counts);
			_countsAtPrecheck=counts;
			listLines.Add("7. A to Z folder: the scratch copy's own (DocPath moved to a scratch folder in the temp folder); images stored in folders");
			File.WriteAllLines(Path.Combine(WinChkEnv.OutFolder,"00-prechecks.txt"),listLines);
			checkResult.Note(string.Join(" / ",listLines));
			checkResult.Note("0.4's test users and groups are made by group J in the copy (JUSERS, before J4) and removed at its end; 0.4 #6 (DOC's first "
				+"name) is not needed: the copy's provider names are not blank");
			Results.Write(checkResult);
		}

		///<summary>Z, at the end of every run (AssemblyCleanup), before the copy is dropped.</summary>
		public static void RunCleanUp() {
			CheckResult checkResult=new CheckResult("Z","Clean-up");
			//1. No process the run started is left.
			List<string> listLeft=new List<string>();
			lock(ProgramProcess.ListStarted) {
				foreach(string started in ProgramProcess.ListStarted) {
					int pid=int.Parse(started.Split(' ')[0]);
					try {
						Process process=Process.GetProcessById(pid);
						if(!process.HasExited && started.EndsWith(process.ProcessName+".exe",StringComparison.OrdinalIgnoreCase)) {
							listLeft.Add(started);
						}
					}
					catch(ArgumentException) {
						//gone
					}
				}
			}
			checkResult.Expect(listLeft.Count==0,"Z1: no process this run started is left ("+ProgramProcess.ListStarted.Count+" started)",string.Join(", ",listLeft));
			//2. Files.
			foreach(string file in new[] { "NoDpi.txt","NoCustomBorders.txt" }) {
				checkResult.Expect(!File.Exists(Path.Combine(WinChkEnv.DebugFolder,file)),"Z2: <dbg>\\"+file+" gone");
			}
			string folderRelease=WinChkEnv.ReleaseFolder;
			string pathBefore=Path.Combine(WinChkEnv.OutFolder,"release-files-before.txt");
			if(File.Exists(pathBefore)) {
				List<string> listBefore=File.ReadAllLines(pathBefore).ToList();
				List<string> listNow=Directory.GetFiles(folderRelease,"*",SearchOption.AllDirectories).Select(x => x.Substring(folderRelease.Length+1)).ToList();
				List<string> listNew=listNow.Except(listBefore).ToList();
				foreach(string file in listNew) {
					File.Delete(Path.Combine(folderRelease,file));
				}
				foreach(string folder in Directory.GetDirectories(folderRelease,"*",SearchOption.AllDirectories).OrderByDescending(x => x.Length)) {
					if(!Directory.EnumerateFileSystemEntries(folder).Any() && !listBefore.Any(x => x.StartsWith(folder.Substring(folderRelease.Length+1)+"\\"))) {
						Directory.Delete(folder);
					}
				}
				List<string> listAfter=Directory.GetFiles(folderRelease,"*",SearchOption.AllDirectories).Select(x => x.Substring(folderRelease.Length+1)).ToList();
				checkResult.Expect(listAfter.OrderBy(x => x).SequenceEqual(listBefore.OrderBy(x => x)),"Z2: <rel> again equals release-files-before.txt (removed: "
					+(listNew.Count==0 ? "nothing" : string.Join(", ",listNew))+")");
			}
			checkResult.Expect(!File.Exists(Path.Combine(folderRelease,"FreeDentalConfig.xml")),"Z2: no FreeDentalConfig.xml in <rel>");
			checkResult.Expect(!File.Exists(Path.Combine(WinChkEnv.RepoRoot,"CentralManager","bin","Debug","CentralManagerConfig.xml")),"Z2: the K1 copy is gone");
			checkResult.Expect(!File.Exists(Path.Combine(WinChkEnv.RepoRoot,"OpenPmsDentalHL7","bin","Debug","FreeDentalConfig.xml")),"Z2: the K8 copy is gone");
			checkResult.Expect(!Directory.Exists(Path.Combine(Path.GetTempPath(),"stage1-rdl")),"Z2: %TEMP%\\stage1-rdl gone");
			checkResult.Expect(!File.Exists(Path.Combine(WinChkEnv.RepoRoot,"RdlDesign","bin","Debug","designerstate.xml")),"Z2: RdlDesigner's designerstate.xml gone");
			string pathDebugLogger=Path.Combine(WinChkEnv.DebugFolder,"Logger");
			checkResult.Note("<dbg>\\Logger: "+(Directory.Exists(pathDebugLogger) ? string.Join(", ",Directory.GetDirectories(pathDebugLogger).Select(Path.GetFileName)) : "none")
				+" (the program's own start-up logs; left as the program writes them, as stage 0 found)");
			//0.4's removal, in case group J stopped before its own (the copy's rows; nothing when they are gone).
			try {
				IJTests.RemoveTestUsers();
			}
			catch(Exception ex) {
				checkResult.Note("0.4 removal failed: "+ex.Message);
			}
			//3 and 4. The copy, compared with the prechecks, before it is dropped.
			string countsBefore=_countsAtPrecheck;
			string countsNow=ScratchDb.Rows(QUERY_COUNTS);
			checkResult.Note("Counts at P0: "+countsBefore+"; now: "+countsNow);
			if(countsBefore!="") {
				//Column 4 (eforms): the first opening of Setup > eForms inserts the internal eForms into the database (FrmEFormDefs.InsertInternalEFormsIfNeeded),
				//which is the program's own seeding, not a check's leftover; it is noted, not compared.
				string[] arrayBefore=countsBefore.Split('|');
				string[] arrayNow=countsNow.Split('|');
				if(arrayBefore.Length==arrayNow.Length && arrayBefore.Length>3 && arrayBefore[3]!=arrayNow[3]) {
					checkResult.Note("eformdef rows "+arrayBefore[3]+" -> "+arrayNow[3]+": the internal eForms the program inserts the first time Setup > eForms opens");
					arrayNow[3]=arrayBefore[3];
					countsNow=string.Join("|",arrayNow);
				}
				checkResult.Expect(countsBefore==countsNow,"Z3/Z4: the copy's counts are as at 0.3 (columns: snomed provkeys problems eforms devices employees payperiods docs "
					+"sheetdefs referrals refattach perio procs commlogs savedtps adminperms dashperms adminprefs provcolour groups emailaddrs)",countsBefore+" -> "+countsNow);
			}
			checkResult.Note("Prefs now: "+ScratchDb.Rows(QUERY_PREFS));
			checkResult.Note("Z5: the copy keeps what the program cannot remove (security log entries, the deleted D2391 row) until it is dropped; "
				+"OpenPmsDentalHL7 wrote 'Initialized' and the version message to the Application Event Log");
			checkResult.Note("Groups run: "+(Environment.GetEnvironmentVariable(WinChkEnv.VARIABLE_GROUPS)??"all")+"; the scratch copy "+ScratchDb.Name
				+" and its A to Z folder are dropped right after this check");
			Results.Write(checkResult);
		}
	}
}
