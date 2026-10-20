// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DevDbTool {
	///<summary>Command line options. Every option has a default that targets the local dev MariaDB.</summary>
	public class Options {
		///<summary>Environment variable read for the MySQL password when --password is not given, so the script does not have to put it on the
		///command line.</summary>
		public const string PasswordEnvVar="DEVDB_PASSWORD";

		public string Server="127.0.0.1";
		public int Port=3306;
		public string User="root";
		public string Password="";
		public string Database="opendental_dev";
		public string DumpPath="";
		public string AtoZPath="";
		public bool Force;
		public bool WithSampleData;
		public bool VerifyOnly;
		///<summary>Stop at the frozen 24.3.63 convert chain: do not run the fork migrations (OpenDentBusiness/Misc/ForkMigrations), and do not expect
		///their tables and columns when verifying. Gives a plain 24.3.63 database, for testing the migrations themselves.</summary>
		public bool SkipForkMigrations;
		public bool AllowRemoteServer;
		public bool ShowHelp;

		///<summary>Server and port in the "host:port" form DataConnection expects.</summary>
		public string ServerAndPort {
			get {
				return Server+":"+Port;
			}
		}

		public static string Usage {
			get {
				return @"DevDbTool - create a local development database for this codebase.

Usage: DevDbTool.exe [options]

  --server <host>         MySQL/MariaDB host (default 127.0.0.1)
  --port <n>              Port (default 3306)
  --user <name>           MySQL user (default root)
  --password <pw>         MySQL password (default: %"+PasswordEnvVar+@"%, else blank)
  --database <name>       Database to create (default opendental_dev)
  --dump <path>           Seed dump (default: UnitTests\Resources\dump.txt in this repo)
  --force                 Drop and recreate the database if it already exists
  --with-sample-data      Add a few fictional providers, operatories, patients and appointments
  --atoz <path>           Create this A to Z images folder and point the DocPath preference at it
  --verify-only           Only check an existing database; change nothing
  --no-fork-migrations    Stop at the 24.3.63 convert scripts: skip the fork migrations (and do not expect
                          their schema when verifying)
  --allow-remote-server   Allow a server other than localhost or 127.0.0.1

Exit codes: 0 success, 1 error, 2 refused or bad arguments, 3 created but outbound
network requests were attempted (and blocked), 4 verification failed.";
			}
		}

		///<summary>Throws ArgumentException for bad input.</summary>
		public static Options Parse(string[] args) {
			Options options=new Options();
			string passwordEnv=Environment.GetEnvironmentVariable(PasswordEnvVar);
			if(!string.IsNullOrEmpty(passwordEnv)) {
				options.Password=passwordEnv;
			}
			Queue<string> queueArgs=new Queue<string>(args);
			while(queueArgs.Count>0) {
				string arg=queueArgs.Dequeue();
				switch(arg.ToLowerInvariant()) {
					case "--server":
						options.Server=TakeValue(queueArgs,arg);
						break;
					case "--port":
						string port=TakeValue(queueArgs,arg);
						if(!int.TryParse(port,out options.Port) || options.Port<1 || options.Port>65535) {
							throw new ArgumentException("Invalid port: "+port);
						}
						break;
					case "--user":
						options.User=TakeValue(queueArgs,arg);
						break;
					case "--password":
						options.Password=TakeValue(queueArgs,arg,allowEmpty:true);
						break;
					case "--database":
						options.Database=TakeValue(queueArgs,arg);
						break;
					case "--dump":
						options.DumpPath=TakeValue(queueArgs,arg);
						break;
					case "--atoz":
						options.AtoZPath=TakeValue(queueArgs,arg);
						break;
					case "--force":
						options.Force=true;
						break;
					case "--with-sample-data":
						options.WithSampleData=true;
						break;
					case "--verify-only":
						options.VerifyOnly=true;
						break;
					case "--no-fork-migrations":
						options.SkipForkMigrations=true;
						break;
					case "--allow-remote-server":
						options.AllowRemoteServer=true;
						break;
					case "-h":
					case "-?":
					case "/?":
					case "--help":
						options.ShowHelp=true;
						break;
					default:
						throw new ArgumentException("Unknown argument: "+arg);
				}
			}
			options.Validate();
			return options;
		}

		private static string TakeValue(Queue<string> queueArgs,string argName,bool allowEmpty=false) {
			if(queueArgs.Count==0) {
				throw new ArgumentException(argName+" needs a value.");
			}
			string value=queueArgs.Dequeue();
			if(!allowEmpty && string.IsNullOrWhiteSpace(value)) {
				throw new ArgumentException(argName+" needs a value.");
			}
			return value;
		}

		private void Validate() {
			if(ShowHelp) {
				return;
			}
			//The name ends up inside backquoted SQL identifiers, so keep it to plain identifier characters.
			if(!Regex.IsMatch(Database,"^[A-Za-z0-9_]{1,64}$")) {
				throw new ArgumentException("Database name may only contain letters, digits and underscores: "+Database);
			}
			string[] arraySystemDbs={ "mysql","information_schema","performance_schema","sys" };
			if(arraySystemDbs.Contains(Database.ToLowerInvariant())) {
				throw new ArgumentException("Refusing to use system database: "+Database);
			}
			if(Server.Contains(":")) {
				//DataConnection splits "host:port" on the colon, so IPv6 literals cannot work either.
				throw new ArgumentException("Pass the port with --port, not as part of --server (IPv6 literals are not supported; use 127.0.0.1).");
			}
			if(!AllowRemoteServer && !IsLocalHost(Server)) {
				throw new ArgumentException("Server '"+Server+"' is not local. This tool creates and drops databases, so it only talks to "
					+"localhost by default. Pass --allow-remote-server to use a remote development server.");
			}
			if(VerifyOnly && (Force || WithSampleData || AtoZPath!="")) {
				throw new ArgumentException("--verify-only cannot be combined with --force, --with-sample-data or --atoz.");
			}
			if(DumpPath!="" && !File.Exists(DumpPath)) {
				throw new ArgumentException("Dump file not found: "+DumpPath);
			}
		}

		public static bool IsLocalHost(string server) {
			string s=server.Trim().ToLowerInvariant();
			return s=="localhost" || s=="127.0.0.1";
		}
	}
}
