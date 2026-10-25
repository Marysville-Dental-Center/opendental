// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace AdoptionValidation {
	///<summary>One named validation with its outcome.</summary>
	public class CheckResult {
		public string Id;
		public string Name;
		public bool Passed;
		public string Detail="";

		public override string ToString() {
			return (Passed ? "PASS " : "FAIL ")+Id+" "+Name+(Detail=="" ? "" : ": "+Detail);
		}
	}

	public class StepTiming {
		public string Name;
		public double Seconds;
		public string Note="";
	}

	public class StartupScenarioResult {
		public string Database;
		public string Scenario;
		public bool IsReleaseClient;
		public OdStartupOutcome Outcome;
		public int ExitCode;
		public bool IsRefused;
		public string Summary;
		public List<string> ListTrace=new List<string>();
	}

	public class RunResult {
		public int RunNumber;
		public string DatabaseSource;
		public string DatabaseAdopted;
		public string CopyMethod;
		public string GenerationInfo;
		public int Tables;
		public long Rows;
		public string FixtureSummary;
		public List<StepTiming> ListSteps=new List<StepTiming>();
		public List<CheckResult> ListChecks=new List<CheckResult>();
		public List<string> ListAdoptionLog=new List<string>();
		public List<string> ListMigrationLog=new List<string>();
		public List<HashCount> ListHashesBefore=new List<HashCount>();
		public List<HashCount> ListHashesAfter=new List<HashCount>();
		public List<string> ListAllowedDifferences=new List<string>();
		public List<string> ListUnexpectedDifferences=new List<string>();
		///<summary>Tables whose row count, CHECKSUM TABLE and every column checksum are unchanged by adoption.</summary>
		public int TablesUntouched;
		public List<string> ListTablesTouched=new List<string>();
		public SortedDictionary<string,string> DictTotalsBefore=new SortedDictionary<string,string>(StringComparer.Ordinal);
		public SortedDictionary<string,string> DictTotalsAfter=new SortedDictionary<string,string>(StringComparer.Ordinal);
		public List<StartupScenarioResult> ListStartupScenarios=new List<StartupScenarioResult>();
		public RoundTripSummary RoundTrip;
		public List<string> ListCredentials=new List<string>();
		public string SchemaVerifierReport="";
		public string ReportBaselinePath;
		///<summary>The restored copy's report: the runbook's baseline-copy.json, which the post-adoption report is compared with (step 6).</summary>
		public string ReportCopyPath;
		public string ReportAdoptedPath;
		public string Error;

		public bool Passed {
			get { return Error==null && ListChecks.Count>0 && ListChecks.All(x => x.Passed); }
		}

		public double TotalSeconds {
			get { return ListSteps.Sum(x => x.Seconds); }
		}
	}

	public class RehearsalResult {
		public string Tool="AdoptionValidation 1";
		public string StartedUtc;
		public string ServerVersion;
		public string Size;
		public long Seed;
		public int FillRemaining;
		public string AsOf;
		public string Commit;
		public List<RunResult> ListRuns=new List<RunResult>();
		public List<CheckResult> ListCrossRunChecks=new List<CheckResult>();

		public bool Passed {
			get { return ListRuns.Count>0 && ListRuns.All(x => x.Passed) && ListCrossRunChecks.All(x => x.Passed); }
		}

		public void Save(string path) {
			File.WriteAllText(path,JsonConvert.SerializeObject(this,Formatting.Indented,new Newtonsoft.Json.Converters.StringEnumConverter()),new UTF8Encoding(false));
		}

		///<summary>A Markdown summary of every result, for docs/m1-validation-report.md.</summary>
		public string ToMarkdown() {
			StringBuilder sb=new StringBuilder();
			sb.AppendLine("# Adoption rehearsal results");
			sb.AppendLine();
			sb.AppendLine("Overall: **"+(Passed ? "PASS" : "FAIL")+"**. "+ListRuns.Count+" runs, size "+Size+", seed "+Seed+", as of "+AsOf
				+", filler rows "+FillRemaining+", server "+ServerVersion+", started "+StartedUtc+(string.IsNullOrEmpty(Commit) ? "" : ", commit "+Commit)+".");
			sb.AppendLine();
			sb.AppendLine("## Step timings (seconds)");
			sb.AppendLine();
			List<string> listStepNames=ListRuns.SelectMany(x => x.ListSteps.Select(y => y.Name)).Distinct().ToList();
			sb.AppendLine("| Step | "+string.Join(" | ",ListRuns.Select(x => "Run "+x.RunNumber))+" |");
			sb.AppendLine("|---|"+string.Join("|",ListRuns.Select(x => "---:"))+"|");
			foreach(string name in listStepNames) {
				sb.AppendLine("| "+name+" | "+string.Join(" | ",ListRuns.Select(x => x.ListSteps.Where(y => y.Name==name).Sum(y => y.Seconds).ToString("0.0")))+" |");
			}
			sb.AppendLine("| **Total** | "+string.Join(" | ",ListRuns.Select(x => "**"+x.TotalSeconds.ToString("0.0")+"**"))+" |");
			sb.AppendLine();
			foreach(RunResult run in ListRuns) {
				sb.AppendLine("## Run "+run.RunNumber+": "+(run.Passed ? "PASS" : "FAIL"));
				sb.AppendLine();
				sb.AppendLine("Source "+run.DatabaseSource+" ("+run.Tables+" tables, "+run.Rows.ToString("N0")+" rows; "+run.GenerationInfo+"), adopted copy "
					+run.DatabaseAdopted+" (copy by "+run.CopyMethod+").");
				sb.AppendLine();
				if(run.Error!=null) {
					sb.AppendLine("Error: "+run.Error);
					sb.AppendLine();
				}
				sb.AppendLine("| Check | Result | Detail |");
				sb.AppendLine("|---|---|---|");
				foreach(CheckResult check in run.ListChecks) {
					sb.AppendLine("| "+check.Id+" "+check.Name+" | "+(check.Passed ? "PASS" : "**FAIL**")+" | "+Escape(check.Detail)+" |");
				}
				sb.AppendLine();
			}
			if(ListCrossRunChecks.Count>0) {
				sb.AppendLine("## Across runs");
				sb.AppendLine();
				sb.AppendLine("| Check | Result | Detail |");
				sb.AppendLine("|---|---|---|");
				foreach(CheckResult check in ListCrossRunChecks) {
					sb.AppendLine("| "+check.Id+" "+check.Name+" | "+(check.Passed ? "PASS" : "**FAIL**")+" | "+Escape(check.Detail)+" |");
				}
				sb.AppendLine();
			}
			RunResult first=ListRuns.FirstOrDefault();
			if(first!=null) {
				sb.AppendLine("## Details of run "+first.RunNumber);
				sb.AppendLine();
				sb.AppendLine("Office fixture: "+first.FixtureSummary);
				sb.AppendLine();
				sb.AppendLine("### Key business totals");
				sb.AppendLine();
				sb.AppendLine("| Total | Before | After |");
				sb.AppendLine("|---|---:|---:|");
				foreach(string key in KeyTotals) {
					first.DictTotalsBefore.TryGetValue(key,out string before);
					first.DictTotalsAfter.TryGetValue(key,out string after);
					sb.AppendLine("| "+key+" | "+(before??"-")+" | "+(after??"-")+" |");
				}
				sb.AppendLine();
				sb.AppendLine("### Differences adoption made (all allowed)");
				sb.AppendLine();
				foreach(string difference in first.ListAllowedDifferences) {
					sb.AppendLine("- "+Escape(difference));
				}
				if(first.ListUnexpectedDifferences.Count>0) {
					sb.AppendLine();
					sb.AppendLine("Unexpected:");
					foreach(string difference in first.ListUnexpectedDifferences) {
						sb.AppendLine("- "+Escape(difference));
					}
				}
				sb.AppendLine();
				sb.AppendLine("### Adoption log (what AdoptionValidation adopt prints in runbook steps 4 and 5)");
				sb.AppendLine();
				sb.AppendLine("```");
				foreach(string line in first.ListAdoptionLog) {
					sb.AppendLine(line);
				}
				sb.AppendLine("```");
				sb.AppendLine();
				sb.AppendLine("### Rehash record");
				sb.AppendLine();
				sb.AppendLine("| Table | Rows | In rehash window | Before: fork / other / blank | After: fork / other / blank | Window rows without a fork hash after |");
				sb.AppendLine("|---|---:|---:|---|---|---:|");
				foreach(HashCount before in first.ListHashesBefore) {
					HashCount after=first.ListHashesAfter.FirstOrDefault(x => x.Table==before.Table)??new HashCount();
					sb.AppendLine("| "+before.Table+" | "+before.Rows+" | "+after.InWindow+" | "+before.Current+" / "+before.Other+" / "+before.Blank+" | "+after.Current+" / "
						+after.Other+" / "+after.Blank+" | "+after.InWindowNotCurrent+" |");
				}
				sb.AppendLine();
				sb.AppendLine("### Open Dental-style startup check");
				sb.AppendLine();
				sb.AppendLine("| Database | Client | Outcome |");
				sb.AppendLine("|---|---|---|");
				foreach(StartupScenarioResult scenario in first.ListStartupScenarios) {
					sb.AppendLine("| "+scenario.Database+" | "+scenario.Scenario+" | "+Escape(scenario.Summary)+" |");
				}
				sb.AppendLine();
				if(first.RoundTrip!=null) {
					sb.AppendLine("### Data-layer round trip");
					sb.AppendLine();
					sb.AppendLine("| Table | Class | Rows | 24.4-25.3 columns | Loaded | Rows saved | New values written | Result |");
					sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---|");
					foreach(TableRoundTrip table in first.RoundTrip.Tables) {
						string notes=string.Join("; ",table.ListDocumentedExceptions.Concat(table.ListUndefinedEnumValues.Select(x => "undefined enum value "+x)));
						sb.AppendLine("| "+table.Table+" | "+(table.TypeName??"-")+" | "+table.Rows+" | "+table.Columns.Count+" | "+table.ColumnsLoaded+" | "+table.RowsSaved+" | "
							+table.ColumnsWritten+" | "+(table.Passed ? "ok" : "**FAILED**: "+Escape(table.Describe()))+(notes=="" ? "" : " ("+Escape(notes)+")")+" |");
					}
					sb.AppendLine();
				}
				sb.AppendLine("### Stored passwords to re-enter (credential inventory of the restored copy)");
				sb.AppendLine();
				foreach(string item in first.ListCredentials) {
					sb.AppendLine("- "+Escape(item));
				}
				sb.AppendLine();
			}
			return sb.ToString();
		}

		public static readonly string[] KeyTotals={
			"patients.families","production.gross","production.net","production.writeoffs","income.patient","income.insurance","income.deposits",
			"aging.stored.bal_0_30","aging.stored.bal_31_60","aging.stored.bal_61_90","aging.stored.bal_over_90","aging.stored.bal_total",
			"aging.stored.ins_est","aging.stored.payplan_due","aging.ledger_total","insurance.outstanding_estimate","insurance.treatment_plan_estimates","payplans.balance",
			"payplans.principal_due","appointments.future_scheduled","appointments.mirrored","fees.rows_dated","accounting.totals.debits",
		};

		private static string Escape(string text) {
			return (text??"").Replace("|","\\|").Replace("\r\n","<br>").Replace("\n","<br>");
		}
	}
}
