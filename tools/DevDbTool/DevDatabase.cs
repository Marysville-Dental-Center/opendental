// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Data;
using System.IO;
using System.Text;
using DataConnectionBase;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace DevDbTool {
	///<summary>Thrown when the tool declines to do something that could destroy data or that the arguments do not allow.</summary>
	public class RefusedException : Exception {
		public RefusedException(string message) : base(message) { }
	}

	///<summary>Creates and upgrades the dev database. Mirrors UnitTests.DatabaseTools.FreshFromDump (load the seed dump, run the convert
	///methods, apply the default auto codes, procedure buttons and quick-add procedures) minus the unit-test-only steps, and the parts of
	///OpenDental's ClassConvertDatabase that guard a conversion (the CorruptedDatabase flag).</summary>
	public class DevDatabase {
		///<summary>A dev database is expected to hold a handful of patients at most. -Force refuses to drop anything bigger, so that a typo in
		///--database or --server cannot wipe a real office database.</summary>
		public const int MaxPatientsForForceDrop=200;

		private readonly Options _options;

		public DevDatabase(Options options) {
			_options=options;
		}

		///<summary>The version of the business layer this tool was built against. OpenDental.exe is built from the same source and carries
		///the same version.</summary>
		public static Version VersionProgram {
			get {
				return typeof(Patient).Assembly.GetName().Version;
			}
		}

		///<summary>Connects to the server without selecting a database.</summary>
		public void ConnectToServer() {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(_options.ServerAndPort,"",_options.User,_options.Password,"","",DatabaseType.MySql,true);
		}

		///<summary>Connects to the target database. Validation runs a harmless UPDATE against the preference table, so skip it until the
		///dump has been loaded.</summary>
		public void ConnectToDatabase(bool skipValidation) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(_options.ServerAndPort,_options.Database,_options.User,_options.Password,"","",DatabaseType.MySql,
				skipValidation);
		}

		///<summary>Prints the server version and makes sure sql_mode is one the convert scripts tolerate. OpenDental.exe itself runs the same
		///"SET GLOBAL sql_mode=''" at every startup (MiscData.SetSqlMode) unless told not to.</summary>
		public void CheckServer() {
			string version=DataCore.GetScalar("SELECT VERSION()");
			Program.Info("Server: "+_options.ServerAndPort+" ("+version+")");
			DataTable table=DataCore.GetTable("SHOW GLOBAL VARIABLES WHERE Variable_name='sql_mode'");
			string sqlMode=table.Rows.Count>0 ? table.Rows[0]["Value"].ToString() : null;
			if(sqlMode==null || (sqlMode!="" && sqlMode.ToUpperInvariant()!="NO_AUTO_CREATE_USER")) {
				Program.Warn("Global sql_mode is '"+sqlMode+"'. The convert scripts need a blank sql_mode, so running SET GLOBAL sql_mode='' "
					+"(OpenDental.exe does the same at startup). Put sql_mode= in my.ini to make it permanent.");
				DataCore.NonQ("SET GLOBAL sql_mode=''");
			}
			string engine=DataCore.GetScalar("SELECT ENGINE FROM information_schema.ENGINES WHERE Support='DEFAULT'");
			if(!string.Equals(engine,"MyISAM",StringComparison.OrdinalIgnoreCase)) {
				Program.Warn("The server's default storage engine is "+engine+". The seed dump creates MyISAM tables and the convert scripts "
					+"create tables in the default engine, so the database will have mixed engines. Set default-storage-engine=MyISAM in my.ini "
					+"to match a standard install.");
			}
		}

		public bool DatabaseExists() {
			string command="SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+POut.String(_options.Database)+"'";
			return DataCore.GetScalar(command)!="0";
		}

		///<summary>Drops the target database after checking that it looks like a dev database rather than real data.</summary>
		public void Drop() {
			string db=_options.Database;
			string command="SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+POut.String(db)+"' AND TABLE_NAME='patient'";
			if(DataCore.GetScalar(command)!="0") {
				long countPatients=PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM `"+db+"`.patient"));
				if(countPatients>MaxPatientsForForceDrop) {
					throw new RefusedException("Refusing to drop '"+db+"': it holds "+countPatients+" patients, which does not look like a dev "
						+"database (the limit is "+MaxPatientsForForceDrop+"). Drop it yourself if you really mean to.");
				}
			}
			Program.Info("Dropping database "+db);
			DataCore.NonQ("DROP DATABASE `"+db+"`");
		}

		public void Create() {
			Program.Info("Creating database "+_options.Database);
			DataCore.NonQ("CREATE DATABASE `"+_options.Database+"` CHARACTER SET utf8");
		}

		///<summary>Runs the seed dump as one multi-statement command, the same way FreshFromDump does. The file is UTF-8.</summary>
		public void LoadDump(string dumpPath) {
			Program.Info("Loading seed dump "+dumpPath);
			string command=File.ReadAllText(dumpPath,new UTF8Encoding(false));
			DataCore.NonQ(command);
			Program.Info("Seed dump loaded: database version "+GetPrefRaw(PrefName.DataBaseVersion));
		}

		///<summary>Runs every convert method newer than the database's version, guarded by the CorruptedDatabase flag like
		///ClassConvertDatabase does: if a step throws, the flag stays set and OpenDental.exe will refuse to open the database.</summary>
		public void Upgrade() {
			Version versionFrom=new Version(GetPrefRaw(PrefName.DataBaseVersion));
			Version versionTo=ConvertDatabases.LatestVersion;
			if(versionFrom>=versionTo) {
				Program.Info("Database is already at "+versionFrom+"; nothing to convert.");
				return;
			}
			Program.Info("Upgrading database from "+versionFrom+" to "+versionTo);
			//Some convert steps and the cache layer expect a current user. Same as FreshFromDump.
			Security.CurUser=Security.CurUser??new Userod();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"1");
			ConvertDatabases.FromVersion=versionFrom;
			ConvertDatabases.InvokeConvertMethods();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"0");
			//Specific caches are invalid after the convert script runs.
			Cache.Refresh(InvalidType.Prefs,InvalidType.Programs);
			Program.Info("Conversion finished: database version "+GetPrefRaw(PrefName.DataBaseVersion));
		}

		///<summary>Runs the fork migrations that take over from the frozen 24.3.63 convert chain (OpenDentBusiness/Misc/ForkMigrations), which bring
		///the schema to Open Dental 25.3.48. They run through the adoption tool's code (Adoption.Run), the only way the fork version markers are
		///written: the startup version guard (ForkVersionGuard) asks for adoption on a 24.3.63 database while this build has fork migration steps, and
		///refuses a database that has ForkSchemaLevel without the adoption markers. Needs ProgramVersion stamped first (StampProgramVersion), because
		///adoption accepts only a database whose DataBaseVersion and ProgramVersion agree.</summary>
		public void RunForkMigrations() {
			Adoption.Run(x => Program.Info("  "+x));
			Cache.Refresh(InvalidType.Prefs);
			Program.Info("Adopted: DataBaseVersion "+GetPrefRaw(PrefName.DataBaseVersion)+", fork schema level "
				+GetPrefRaw(ForkVersions.PREF_FORK_SCHEMA_LEVEL)+", OdSchemaCompat "+GetPrefRaw(ForkVersions.PREF_OD_SCHEMA_COMPAT)+".");
		}

		///<summary>The post-conversion steps from FreshFromDump that make the database usable: default auto codes, procedure buttons and
		///appointment quick-add procedures. Skipped on purpose: setting the Admin password to 'pass' (Middle Tier unit tests only) and the
		///CDCREC rows the HL7 unit tests need.</summary>
		public void ApplyDefaults() {
			Program.Info("Applying default auto codes, procedure buttons and quick-add procedures");
			AutoCodes.SetToDefault();
			ProcButtons.SetToDefault();
			ProcedureCodes.ResetApptProcsQuickAdd();
			ProcedureCodes.RefreshCache();
		}

		///<summary>Records the program version the way OpenDental.exe does after an update on the update server (ProgramVersion pref plus an
		///updatehistory row). Without it a Release build would treat its first start as an update and try to stash its files into the
		///database's UpdateFiles.</summary>
		public void StampProgramVersion() {
			string version=VersionProgram.ToString();
			if(GetPrefRaw(PrefName.ProgramVersion)==version) {
				return;
			}
			Prefs.UpdateStringNoCache(PrefName.ProgramVersion,version);
			UpdateHistories.Insert(new UpdateHistory(version));
			Program.Info("ProgramVersion set to "+version);
		}

		///<summary>Creates an A to Z images folder (with the A-Z subfolders OpenDental.exe uses to recognize one) and points DocPath at it.</summary>
		public void SetAtoZFolder(string path) {
			string fullPath=Path.GetFullPath(path);
			Directory.CreateDirectory(fullPath);
			for(char c='A';c<='Z';c++) {
				Directory.CreateDirectory(Path.Combine(fullPath,c.ToString()));
			}
			if(!fullPath.EndsWith("\\")) {
				fullPath+="\\";
			}
			Prefs.UpdateStringNoCache(PrefName.AtoZfolderUsed,POut.Int((int)DataStorageType.LocalAtoZ));
			Prefs.UpdateStringNoCache(PrefName.DocPath,fullPath);
			Cache.Refresh(InvalidType.Prefs);
			Program.Info("A to Z images folder: "+fullPath);
		}

		///<summary>Reads a preference straight from the table, bypassing the cache.</summary>
		public static string GetPrefRaw(PrefName prefName) {
			return GetPrefRaw(prefName.ToString());
		}

		///<summary>Reads a preference straight from the table by name, for rows that are not in the PrefName enum (such as the fork migration
		///markers).</summary>
		public static string GetPrefRaw(string prefName) {
			DataTable table=DataCore.GetTable("SELECT ValueString FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
			if(table.Rows.Count==0) {
				return null;
			}
			return table.Rows[0][0].ToString();
		}

		///<summary>Finds UnitTests\Resources\dump.txt by walking up from the tool's own folder to the repository root.</summary>
		public static string FindDefaultDump() {
			DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(dir!=null) {
				string candidate=Path.Combine(dir.FullName,"UnitTests","Resources","dump.txt");
				if(File.Exists(candidate)) {
					return candidate;
				}
				dir=dir.Parent;
			}
			return null;
		}
	}
}
