// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Globalization;
using SyntheticData.Generation;
using SyntheticData.Infrastructure;

namespace SyntheticData {
	public class Options {
		public string Command;
		public ServerSettings Server=new ServerSettings();
		public string Database="synth_253";
		public GenerationSettings Generation=new GenerationSettings();
		public bool AsOfGiven;
		public string SchemaSql=SchemaBuilder.DefaultSchemaSqlPath;
		public string SchemaFrom=SchemaBuilder.DefaultReferenceDatabase;
		public bool Force;
		public string Out;
		public string ReportBefore;
		public string ReportAfter;
		public string Allowed;
		public bool Strict;
		public bool ShowHelp;

		public const string Usage=@"SyntheticData: fictional practice data in a copy of the Open Dental 25.3.48 schema, and validation reports.

Usage:
  SyntheticData generate [--database synth_253] [--size small|medium|large] [--seed N] [--as-of yyyy-MM-dd]
                         [--families N] [--years N] [--config-from DB] [--fill-remaining N] [--force]
                         [--schema-sql PATH | --schema-from DB] [--out report.json]
  SyntheticData schema   [--database synth_253] [--force] [--schema-sql PATH | --schema-from DB]
  SyntheticData validate [--database synth_253] [--as-of yyyy-MM-dd] [--out report.json] [--strict]
  SyntheticData coverage [--database synth_253]
  SyntheticData compare  BEFORE.json AFTER.json [--allowed allowed-differences.adoption.json]

Commands:
  generate   Create the database from the 25.3.48 schema (drop it first with --force) and fill it. Prints a summary; with --out
             also writes the validation report.
  schema     Only create the empty schema copy.
  validate   Record row counts, per-table and per-column checksums and business totals (read-only). --strict fails when a
             check.* total is not 0.
  coverage   Check that every table and column new or changed between 24.3 and 25.3.48 holds data.
  compare    Compare two reports. Exit code 0 when every difference is allowed, 5 otherwise.

Connection: --server (localhost, 127.0.0.1 or ::1 only), --port 3306, --user root. The password is read from the
SYNTH_DB_PASSWORD environment variable. Databases written by this tool must be named synth...

Defaults: seed 20261018, size small, as of 2026-09-30, schema from D:\Dev\opendental-schema\fox-db-schema.sql, falling
back to the local database od_253_schema.";

		public static Options Parse(string[] args) {
			Options options=new Options();
			if(args.Length==0) {
				options.ShowHelp=true;
				return options;
			}
			int i=0;
			if(!args[0].StartsWith("-")) {
				options.Command=args[0].ToLowerInvariant();
				i=1;
			}
			for(;i<args.Length;i++) {
				string arg=args[i];
				string Next() {
					if(i+1>=args.Length) {
						throw new ArgumentException(arg+" needs a value.");
					}
					return args[++i];
				}
				switch(arg.ToLowerInvariant()) {
					case "-h": case "--help": case "/?":
						options.ShowHelp=true;
						break;
					case "--server": options.Server.Server=Next(); break;
					case "--port": options.Server.Port=ParseInt(arg,Next()); break;
					case "--user": options.Server.User=Next(); break;
					case "--database": options.Database=Next(); break;
					case "--size": options.Generation.Size=GenerationSettings.ParseSize(Next()); break;
					case "--seed": options.Generation.Seed=long.Parse(Next(),CultureInfo.InvariantCulture); break;
					case "--as-of":
						options.Generation.AsOf=DateTime.ParseExact(Next(),"yyyy-MM-dd",CultureInfo.InvariantCulture);
						options.AsOfGiven=true;
						break;
					case "--families": options.Generation.Families=ParseInt(arg,Next()); break;
					case "--years": options.Generation.Years=ParseInt(arg,Next()); break;
					case "--config-from": options.Generation.ConfigFrom=Next(); break;
					case "--fill-remaining": options.Generation.FillRemaining=ParseInt(arg,Next()); break;
					case "--schema-sql": options.SchemaSql=Next(); options.SchemaFrom=null; break;
					case "--schema-from": options.SchemaFrom=Next(); options.SchemaSql=null; break;
					case "--force": options.Force=true; break;
					case "--out": options.Out=Next(); break;
					case "--allowed": options.Allowed=Next(); break;
					case "--strict": options.Strict=true; break;
					default:
						if(options.Command=="compare" && !arg.StartsWith("-")) {
							if(options.ReportBefore==null) {
								options.ReportBefore=arg;
							}
							else if(options.ReportAfter==null) {
								options.ReportAfter=arg;
							}
							else {
								throw new ArgumentException("compare takes two report files.");
							}
							break;
						}
						throw new ArgumentException("Unknown argument: "+arg);
				}
			}
			if(!options.ShowHelp) {
				switch(options.Command) {
					case "generate": case "schema": case "validate": case "coverage":
						break;
					case "compare":
						if(options.ReportBefore==null || options.ReportAfter==null) {
							throw new ArgumentException("compare needs two report files.");
						}
						break;
					case null:
						throw new ArgumentException("No command given.");
					default:
						throw new ArgumentException("Unknown command: "+options.Command);
				}
			}
			if(options.Generation.Years>30 || options.Generation.Families>200000) {
				throw new ArgumentException("--years is limited to 30 and --families to 200000.");
			}
			return options;
		}

		private static int ParseInt(string name,string value) {
			if(!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int result) || result<0) {
				throw new ArgumentException(name+" needs a whole number, not '"+value+"'.");
			}
			return result;
		}
	}
}
