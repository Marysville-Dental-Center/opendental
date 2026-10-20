// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SyntheticData.Generation;
using SyntheticData.Infrastructure;
using SyntheticData.Validation;

namespace SyntheticData {
	public static class Program {
		public const int ExitOk=0;
		public const int ExitError=1;
		public const int ExitRefused=2;
		public const int ExitNetworkAttempted=3;
		public const int ExitCheckFailed=4;
		public const int ExitUnexpectedDifferences=5;

		public static int Main(string[] args) {
			Options options;
			try {
				options=Options.Parse(args);
			}
			catch(ArgumentException ex) {
				Console.Error.WriteLine(ex.Message);
				Console.Error.WriteLine();
				Console.Error.WriteLine(Options.Usage);
				return ExitRefused;
			}
			if(options.ShowHelp) {
				Console.WriteLine(Options.Usage);
				return ExitOk;
			}
			NetworkGuard.Install();
			Stopwatch stopwatch=Stopwatch.StartNew();
			int exitCode;
			try {
				exitCode=Run(options);
			}
			catch(RefusedException ex) {
				Console.Error.WriteLine("Refused: "+ex.Message);
				exitCode=ExitRefused;
			}
			catch(Exception ex) {
				Console.Error.WriteLine("Failed: "+ex);
				exitCode=ExitError;
			}
			if(NetworkGuard.ListBlocked.Count>0) {
				Console.Error.WriteLine("Outbound network requests were attempted and blocked: "+string.Join(", ",NetworkGuard.ListBlocked));
				exitCode=ExitNetworkAttempted;
			}
			Console.WriteLine("Done in "+stopwatch.Elapsed.TotalSeconds.ToString("0.0")+" s (exit code "+exitCode+").");
			return exitCode;
		}

		public static int Run(Options options) {
			if(options.Command=="compare") {
				return RunCompare(options);
			}
			using(Db db=Db.Open(options.Server)) {
				switch(options.Command) {
					case "schema":
						Info("Creating "+options.Database+" from "+SchemaBuilder.Create(db,options.Database,options.Force,options.SchemaSql,options.SchemaFrom));
						return VerifySchema(db,options);
					case "generate":
						return RunGenerate(db,options);
					case "validate":
						return RunValidate(db,options,options.AsOfGiven ? options.Generation.AsOf : (DateTime?)null);
					case "coverage":
						return RunCoverage(db,options.Database);
				}
			}
			return ExitError;
		}

		private static int RunGenerate(Db db,Options options) {
			Info("Generating "+options.Database+": "+options.Generation.Describe());
			string source=SchemaBuilder.Create(db,options.Database,options.Force,options.SchemaSql,options.SchemaFrom);
			Info("Schema from "+source);
			int exitSchema=VerifySchema(db,options);
			if(exitSchema!=ExitOk) {
				return exitSchema;
			}
			World world=Generator.Run(db,options.Database,options.Generation,Info);
			Info("Wrote "+Generator.Summary(world));
			Coverage.Result coverage=Coverage.Check(db,options.Database);
			PrintCoverage(coverage);
			ValidationReport report=Validator.Run(db,options.Database,options.Generation.AsOf,Info);
			List<string> listFailed=Validator.FailedChecks(report);
			PrintTotals(report);
			if(options.Out!=null) {
				report.Save(options.Out);
				Info("Report written to "+options.Out);
			}
			if(listFailed.Count>0) {
				Console.Error.WriteLine("Consistency checks failed: "+string.Join("; ",listFailed));
				return ExitCheckFailed;
			}
			return coverage.IsComplete ? ExitOk : ExitCheckFailed;
		}

		private static int VerifySchema(Db db,Options options) {
			SchemaInfo schema=SchemaInfo.Load(db,options.Database);
			Info("Schema has "+schema.DictTables.Count+" tables and "+schema.ColumnCount+" columns.");
			string reference=SchemaBuilder.DefaultReferenceDatabase;
			if(db.DatabaseExists(reference) && reference!=options.Database) {
				List<string> listDiffs=SchemaInfo.Diff(SchemaInfo.Load(db,reference),schema);
				if(listDiffs.Count>0) {
					Console.Error.WriteLine("The copy differs from "+reference+":");
					foreach(string diff in listDiffs.Take(50)) {
						Console.Error.WriteLine("  "+diff);
					}
					return ExitCheckFailed;
				}
				Info("Matches "+reference+" (tables, columns, types, nullability, defaults and positions).");
			}
			else if(schema.DictTables.Count!=SchemaBuilder.ExpectedTableCount) {
				Console.Error.WriteLine("Expected "+SchemaBuilder.ExpectedTableCount+" tables.");
				return ExitCheckFailed;
			}
			return ExitOk;
		}

		private static int RunValidate(Db db,Options options,DateTime? asOf) {
			ValidationReport report=Validator.Run(db,options.Database,asOf,Info);
			PrintTotals(report);
			if(options.Out!=null) {
				report.Save(options.Out);
				Info("Report written to "+options.Out);
			}
			List<string> listFailed=Validator.FailedChecks(report);
			foreach(string failed in listFailed) {
				Console.Error.WriteLine("  check failed: "+failed);
			}
			return options.Strict && listFailed.Count>0 ? ExitCheckFailed : ExitOk;
		}

		private static int RunCoverage(Db db,string database) {
			Coverage.Result result=Coverage.Check(db,database);
			PrintCoverage(result);
			return result.IsComplete ? ExitOk : ExitCheckFailed;
		}

		private static int RunCompare(Options options) {
			ValidationReport before=ValidationReport.Load(options.ReportBefore);
			ValidationReport after=ValidationReport.Load(options.ReportAfter);
			AllowedDifferences allowed=options.Allowed==null ? null : AllowedDifferences.Load(options.Allowed);
			List<Difference> listDiffs=ReportComparer.Compare(before,after,allowed);
			Console.WriteLine(ReportComparer.Format(listDiffs));
			return listDiffs.Any(x => !x.IsAllowed) ? ExitUnexpectedDifferences : ExitOk;
		}

		private static void PrintCoverage(Coverage.Result result) {
			int total=Coverage.AllItems.Count();
			int missing=result.ListMissingTables.Count+result.ListEmptyTables.Count+result.ListMissingColumns.Count+result.ListUnfilledColumns.Count;
			Info("Coverage of 24.4-25.3 tables and columns: "+(total-missing)+" of "+total+" hold data.");
			foreach(string item in result.ListMissingTables.Concat(result.ListMissingColumns)) {
				Console.Error.WriteLine("  missing from the schema: "+item);
			}
			foreach(string item in result.ListEmptyTables.Concat(result.ListUnfilledColumns)) {
				Console.Error.WriteLine("  no data: "+item);
			}
			foreach(string item in result.ListKnownEmpty) {
				Info("  left empty on purpose: "+item+" ("+Coverage.DictKnownEmpty[item]+")");
			}
		}

		private static void PrintTotals(ValidationReport report) {
			Info(report.TableCount+" tables, "+report.TotalRows.ToString("N0")+" rows. Key totals as of "+report.AsOf+":");
			foreach(string key in new[] { "patients.families","production.gross","production.net","income.patient","income.insurance","aging.stored.bal_total",
				"aging.ledger_total","aging.stored.bal_over_90","insurance.outstanding_estimate","payplans.balance","appointments.future_scheduled" })
			{
				if(report.Totals.TryGetValue(key,out string value)) {
					Info("  "+key+" = "+value);
				}
			}
		}

		public static void Info(string message) {
			Console.WriteLine(message);
		}
	}
}
