// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using SyntheticData.Generation;
using SyntheticData.Infrastructure;
using SyntheticData.Validation;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	public class RehearsalOptions {
		public ServerSettings Server=new ServerSettings();
		public DataSize Size=DataSize.Medium;
		public long Seed=GenerationSettings.DefaultSeed;
		public DateTime AsOf=GenerationSettings.DefaultAsOf;
		///<summary>Overrides of the preset (0 = preset), for quick test runs.</summary>
		public int Families;
		public int Years;
		public int Runs=2;
		///<summary>Filler rows for every table the generator leaves empty, so the checksums cover (almost) all 422 tables.</summary>
		public int FillRemaining=2;
		public string OutDir;
		public bool Keep;
		///<summary>Null: the runbook's server-side copy (step 3.2). Dump: a mariadb-dump backup and restore instead.</summary>
		public CopyMethod? Copy;
		public string SchemaSql=SchemaBuilder.DefaultSchemaSqlPath;
		public string SchemaFrom=SchemaBuilder.DefaultReferenceDatabase;
		///<summary>Part of every scratch database name; unique per rehearsal unless given.</summary>
		public string Tag;
		///<summary>Start of every scratch database name (default synth_m1z_). Must keep the names writable (synth...).</summary>
		public string Prefix=Rehearsal.DbPrefix;
		public bool SkipRoundTrip;
		public string AllowedPath;
	}

	///<summary>The M1-Z switch-gate rehearsal on synthetic data (docs/specs/port-backlog.md, M1-Z; docs/adoption-runbook.md). Each run generates a
	///fresh database, records its baseline, copies it as the runbook does, adopts the copy, migrates, verifies, rehashes and validates, then does it
	///all a second time on the adopted copy to prove idempotence. Several runs prove the whole thing repeats exactly. Every step is timed.
	///</summary>
	public class Rehearsal {
		public const string DbPrefix="synth_m1z_";
		///<summary>The tables that hold the time of adoption. The report comparisons can only allow them whole, so the runs and the second pass are
		///compared on them row by row (RowComparer), from a snapshot taken right after adoption (the run's _snap database).</summary>
		public static readonly string[] ArraySnapshotTables={ "preference","securitylog","securityloghash" };
		private readonly RehearsalOptions _options;
		private readonly Action<string> _log;
		private RunResult _run;

		public Rehearsal(RehearsalOptions options,Action<string> log) {
			_options=options;
			_log=log??(x => { });
		}

		public static string NewTag() {
			return DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"+(Process.GetCurrentProcess().Id%100000).ToString(CultureInfo.InvariantCulture);
		}

		public RehearsalResult Run() {
			string tag=_options.Tag??NewTag();
			Directory.CreateDirectory(_options.OutDir);
			RehearsalResult rehearsalResult=new RehearsalResult {
				StartedUtc=DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'",CultureInfo.InvariantCulture),
				Size=_options.Size.ToString().ToLowerInvariant()+(_options.Families>0 ? " ("+_options.Families+" families)" : ""),
				Seed=_options.Seed,
				FillRemaining=_options.FillRemaining,
				AsOf=_options.AsOf.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
				Commit=Environment.GetEnvironmentVariable("ADOPTION_REHEARSAL_COMMIT")??"",
			};
			using(Db db=Db.Open(_options.Server)) {
				rehearsalResult.ServerVersion=db.ServerVersion();
			}
			List<ValidationReport> listBaselines=new List<ValidationReport>();
			List<ValidationReport> listAdopted=new List<ValidationReport>();
			List<string> listSnapshots=new List<string>();
			for(int i=1;i<=_options.Runs;i++) {
				_run=new RunResult { RunNumber=i };
				rehearsalResult.ListRuns.Add(_run);
				string source=_options.Prefix+tag+"_r"+i+"_src";
				string adopted=_options.Prefix+tag+"_r"+i+"_adopted";
				string snapshot=_options.Prefix+tag+"_r"+i+"_snap";
				listSnapshots.Add(snapshot);
				_log("=== Run "+i+" of "+_options.Runs+": "+source+" -> "+adopted);
				ValidationReport baseline=null;
				ValidationReport afterAdoption=null;
				try {
					RunOnce(source,adopted,snapshot,out baseline,out afterAdoption);
				}
				catch(Exception ex) {
					Exception exInner=ex is TargetInvocationException && ex.InnerException!=null ? ex.InnerException : ex;
					_run.Error=exInner.GetType().Name+": "+exInner.Message;
					_log("Run "+i+" failed: "+exInner);
				}
				finally {
					if(!_options.Keep) {
						Cleanup(source,adopted);
					}
				}
				listBaselines.Add(baseline);
				listAdopted.Add(afterAdoption);
				_log("Run "+i+": "+(_run.Passed ? "PASS" : "FAIL")+" in "+_run.TotalSeconds.ToString("0.0")+" s");
			}
			try {
				if(_options.Runs>1) {
					CompareRuns(rehearsalResult,listBaselines,listAdopted,listSnapshots);
				}
			}
			finally {
				if(!_options.Keep) {
					Cleanup(listSnapshots.ToArray());
				}
			}
			string pathJson=Path.Combine(_options.OutDir,"rehearsal-result.json");
			rehearsalResult.Save(pathJson);
			File.WriteAllText(Path.Combine(_options.OutDir,"rehearsal-result.md"),rehearsalResult.ToMarkdown(),new System.Text.UTF8Encoding(false));
			_log("Results written to "+pathJson+" and rehearsal-result.md");
			return rehearsalResult;
		}

		private void RunOnce(string source,string adopted,string snapshot,out ValidationReport baseline,out ValidationReport afterAdoption) {
			baseline=null;
			afterAdoption=null;
			ServerSettings server=_options.Server;
			AllowedDifferences allowed=AllowedDifferences.Load(FindAllowedPath());
			CopyMethod copyMethod=_options.Copy??CopyMethod.Server;
			_run.DatabaseSource=source;
			_run.DatabaseAdopted=adopted;
			_run.CopyMethod=copyMethod==CopyMethod.Dump ? "mariadb-dump backup and restore" : "the runbook's server-side copy, AdoptionValidation copy";
			using(Db db=Db.Open(server)) {
				//1. Generate the synthetic 25.3.48 database.
				GenerationSettings settings=new GenerationSettings { Seed=_options.Seed,Size=_options.Size,AsOf=_options.AsOf,Families=_options.Families,
					Years=_options.Years,FillRemaining=_options.FillRemaining };
				Coverage.Result coverage=null;
				Time("Generate synthetic 25.3.48 database",() => {
					SchemaBuilder.Create(db,source,true,_options.SchemaSql,_options.SchemaFrom);
					Generator.Run(db,source,settings,x => _log("  "+x));
					coverage=Coverage.Check(db,source);
				});
				OfficeFixture.Summary fixture=Time("Add office fixture (update files, server addresses, stored passwords)",
					() => OfficeFixture.Apply(db,source,_options.Seed));
				_run.FixtureSummary=fixture.ToString();
				//2. Baseline.
				baseline=Time("Record baseline report",() => Validator.Run(db,source,null,null));
				baseline.Save(_run.ReportBaselinePath=Path.Combine(_options.OutDir,"run"+_run.RunNumber+"-1-baseline.json"));
				_run.GenerationInfo=baseline.GenerationInfo;
				_run.Tables=baseline.TableCount;
				_run.Rows=baseline.TotalRows;
				_run.DictTotalsBefore=new SortedDictionary<string,string>(baseline.Totals,StringComparer.Ordinal);
				List<string> listFailedChecks=Validator.FailedChecks(baseline);
				List<string> listErrors=baseline.Totals.Where(x => x.Value.StartsWith("error")).Select(x => x.Key).ToList();
				Check("G1","Generated database is consistent",coverage.IsComplete && listFailedChecks.Count==0 && listErrors.Count==0,
					baseline.TableCount+" tables, "+baseline.TotalRows.ToString("N0")+" rows, "+baseline.Tables.Count(x => x.Rows>0)+" tables with rows; coverage "
					+(Coverage.AllItems.Count()-coverage.AllProblems.Count())+" of "+Coverage.AllItems.Count()+" 24.4-25.3 items; "
					+baseline.Totals.Count(x => x.Key.StartsWith("check."))+" consistency checks, "+listFailedChecks.Count+" failed"
					+(listErrors.Count>0 ? "; total errors: "+string.Join(", ",listErrors) : ""));
				//The FLOAT and DOUBLE values a logical backup would round (runbook step 0.2): the synthetic values are short, so none is expected.
				FloatCheck.Result floatCheck=Time("FLOAT values a logical backup would round (float-check)",() => FloatCheck.Run(db,source));
				_run.FloatCheck=floatCheck.Describe();
				//3. The copy that is adopted, made the way runbook step 3.2 makes it (or, with --copy dump, by a mariadb-dump backup and the restore). Both
				//only create a new database, so a copy left by an earlier --keep run with the same tag is dropped first.
				LocalOnly.EnsureWritableDatabaseName(adopted);
				db.Execute("DROP DATABASE IF EXISTS `"+adopted+"`");
				if(copyMethod==CopyMethod.Dump) {
					(string dump,string client)? toolsFound=DatabaseCopier.FindClientTools();
					if(toolsFound==null) {
						throw new ApplicationException("--copy dump needs mariadb-dump and mariadb (set MARIADB_BIN to their folder).");
					}
					(string dump,string client) tools=toolsFound.Value;
					string pathBackup=Path.Combine(_options.OutDir,source+".sql");
					Time("Back up (mariadb-dump)",() => DatabaseCopier.Backup(server,source,pathBackup,tools.dump),
						() => new FileInfo(pathBackup).Length.ToString("N0")+" bytes");
					Time("Restore the copy",() => DatabaseCopier.Restore(server,pathBackup,adopted,tools.client));
					File.Delete(pathBackup);
				}
				else {
					Time("Copy on the server (AdoptionValidation copy)",() => DatabaseCopier.CopyOnServer(db,source,adopted,x => _log("  "+x)));
				}
				//The runbook's baseline-copy.json (step 3.3): step 6.1 compares the adopted copy with it.
				ValidationReport copyReport=Time("Validate the copy",() => Validator.Run(db,adopted,null,null));
				copyReport.Save(_run.ReportCopyPath=Path.Combine(_options.OutDir,"run"+_run.RunNumber+"-1-copy.json"));
				List<Difference> listCopyDiffs=ReportComparer.Compare(baseline,copyReport,null);
				Check("G2",copyMethod==CopyMethod.Dump ? "Restored copy equals the baseline" : "The copy equals the baseline",listCopyDiffs.Count==0,
					(listCopyDiffs.Count==0 ? "row counts, CHECKSUM TABLE, every column checksum (FLOAT values exactly) and every total equal"
					: ReportComparer.Format(listCopyDiffs))+"; float-check of the source: "+floatCheck.Describe());
				_run.ListCredentials=Time("Credential inventory",() => CredentialInventory.Run(db,adopted)).Select(x => x.ToString()).ToList();
				//4. Before adoption: the fork refuses the copy, and an Open Dental client would use it (control case for the startup model).
				BusinessLayer.Connect(server,adopted);
				ForkGuardResult guardBefore=ForkVersionGuard.Evaluate(ForkVersions.ReadState());
				Check("A1","Fork startup guard refuses the unadopted copy",guardBefore.Action==ForkGuardAction.NeedsAdoption,
					guardBefore.Action+" (exit code "+guardBefore.ExitCode+")");
				List<StartupScenarioResult> listBefore=RunStartupScenarios("unadopted copy");
				StartupScenarioResult control=listBefore.First(x => x.Scenario.StartsWith("Open Dental 25.3.48, update server"));
				Check("A2","Startup model: an Open Dental 25.3.48 client uses the unadopted copy (control)",control.Outcome==OdStartupOutcome.Proceeds,
					control.Summary+"; a 25.4.30 update server: "+listBefore.First(x => x.Scenario.StartsWith("Open Dental 25.4.30, update server")).Summary);
				//5. to 7. Preflight, adoption (Adoption.Run, what FormAdoptDatabase calls), the fork migration with every step again, the schema verifier,
				//the rehash (SecurityHash.RehashNow, what Database Maintenance's Security Hashes tool calls) and the row check against the source:
				//HeadlessAdoption, the code that runbook steps 4 and 5 run as "AdoptionValidation adopt". The office runs this exact sequence, and takes its
				//post-adoption report (step 6.1) before anyone starts the program, as below.
				HeadlessAdoption.Result adoption=HeadlessAdoption.Run(adopted,x => { _run.ListAdoptionLog.Add(x); _log("  "+x); },(name,action) => Time(name,action),
					server,source);
				AdoptionPreflight preflight=adoption.Preflight;
				Check("A3","Preflight accepts the copy",preflight.CanAdopt && !preflight.IsResume && preflight.VersionSource==new Version(25,3,48,0),
					preflight.Message);
				AdoptionResult adoptionResult=adoption.Adoption??new AdoptionResult();
				Check("A4","Adoption finished",adoption.Adoption!=null && adoption.IsAdoptionFinished,
					"dropped column "+adoptionResult.WasPortalEmailColumnDropped+", indexes "+string.Join(",",adoptionResult.ListIndexesDropped)+", "
					+adoptionResult.CountUpdateFileRowsDeleted+" update-file rows deleted, "+adoptionResult.ListPrefsBlanked.Count+" preferences blanked ("
					+string.Join(", ",adoptionResult.ListPrefsBlanked)+")");
				//6. Fork migration (every step again) and schema verifier.
				ForkMigrationResult migrationResult=adoption.Migration;
				_run.ListMigrationLog.AddRange(migrationResult.ListChanges);
				Check("M1","Fork migration changes nothing on the adopted copy",migrationResult.ListChanges.Count==0,
					migrationResult.ListStepsRun.Count+" steps run ("+string.Join(", ",migrationResult.ListStepsRun.Select(x => x.Level))+"), "
					+migrationResult.ListChanges.Count+" changes"+(migrationResult.ListChanges.Count==0 ? "" : ": "+string.Join("; ",migrationResult.ListChanges.Take(10))));
				SchemaVerifyResult verifyResult=adoption.Verify;
				_run.SchemaVerifierReport=verifyResult.GetReport();
				Check("M2","Schema matches Open Dental 25.3.48 exactly",verifyResult.IsMatch && verifyResult.ListDifferences.Count==0,
					verifyResult.CountTablesActual+" of "+verifyResult.CountTablesExpected+" tables; "+verifyResult.ListDifferences.Count+" differences, "
					+verifyResult.ListMismatches.Count+" mismatches"+(verifyResult.ListDifferences.Count==0 ? " (no documented exception needed)" : ": "
					+string.Join("; ",verifyResult.ListDifferences.Take(10).Select(x => x.Table+"."+x.Item))));
				//7. Rehash (Database Maintenance, Tools, Security Hashes), done by HeadlessAdoption above.
				_run.ListHashesBefore=adoption.ListHashesStart;
				_run.ListHashesAfter=adoption.ListHashesAfter;
				HashCount patientAfter=_run.ListHashesAfter.FirstOrDefault(x => x.Table=="patient");
				bool isRehashed=adoption.IsRehashComplete && patientAfter!=null && _run.ListHashesAfter.Sum(x => x.InWindow)>patientAfter.Rows;
				Check("H1","Rehash gives every patient, and every row in the verified window, a fork hash",isRehashed,
					"rehash window from "+adoption.DateRehashWindow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+"; "
					+string.Join("; ",_run.ListHashesAfter.Select(x => x.ToString()))+(adoption.IsRehashLogged ? "; rehash recorded in the security log" : ""));
				//8. Validate: the runbook's step 6, before anything else touches the copy. The adopted copy is compared with the restored copy's report
				//(baseline-copy.json), which G2 showed equals the source's.
				afterAdoption=Time("Record post-adoption report and compare",() => Validator.Run(db,adopted,null,null));
				afterAdoption.Save(_run.ReportAdoptedPath=Path.Combine(_options.OutDir,"run"+_run.RunNumber+"-2-adopted.json"));
				_run.DictTotalsAfter=new SortedDictionary<string,string>(afterAdoption.Totals,StringComparer.Ordinal);
				List<Difference> listDiffs=ReportComparer.Compare(copyReport,afterAdoption,allowed);
				ValidateAgainstBaseline(copyReport,afterAdoption,listDiffs);
				ValidateAdoptionEffects(fixture);
				SnapshotTables(db,adopted,snapshot);
				//9. Open Dental-style startup check on the adopted copy.
				List<StartupScenarioResult> listAfter=Time("Open Dental-style startup check",() => RunStartupScenarios("adopted copy"));
				_run.ListStartupScenarios.AddRange(listAfter);
				_run.ListStartupScenarios.AddRange(listBefore);
				List<StartupScenarioResult> listRelease=listAfter.Where(x => x.IsReleaseClient).ToList();
				Check("V10","Startup model: every Open Dental release client refuses the adopted copy",listRelease.All(x => x.IsRefused),
					listRelease.Count(x => x.IsRefused)+" of "+listRelease.Count+" release-client scenarios exit without converting, writing, installing services or "
					+"copying binaries; "+string.Join("; ",listRelease.Where(x => !x.IsRefused).Select(x => x.Scenario+": "+x.Summary))
					+" (a Debug build of Open Dental skips its version check: "+listAfter.First(x => !x.IsReleaseClient).Outcome+"; Open Dental ships Release builds only)");
				//The tables the allowed list can only allow whole, row by row against the source (what adopt prints in runbook steps 4 and 5).
				AdoptionRowCheck.Result rowCheck=adoption.RowCheck;
				Check("V11","Row by row, adoption changed only its own rows of preference, documentmisc, securitylog, securityloghash and updatehistory",
					rowCheck!=null && rowCheck.Passed,rowCheck==null ? "no row check" : string.Join(" ",rowCheck.ListLines)
					+(rowCheck.Passed ? "" : " Failures: "+string.Join(" ",rowCheck.ListFailures)));
				//10. Data-layer round trip. It writes (and normalizes some cells, moving their rows' audit timestamps), so the runs are compared on the
				//report from before it; the second pass is compared with the report from after it.
				ValidationReport afterFirstPass=afterAdoption;
				if(!_options.SkipRoundTrip) {
					_run.RoundTrip=Time("Data-layer round trip (load and save through OpenDentBusiness)",() => DataLayerRoundTrip.Run(db,adopted,_log));
					RoundTripSummary roundTrip=_run.RoundTrip;
					List<string> listNormalizations=roundTrip.ListNormalizations;
					Check("R1","Every 24.4-25.3 column loads and saves through OpenDentBusiness; every writable one writes",roundTrip.Passed,
						DescribeRoundTrip(roundTrip)+"; first-save normalizations (same meaning in the program): "
						+(listNormalizations.Count==0 ? "none" : string.Join("; ",listNormalizations))
						+(roundTrip.Passed ? "" : "; failures: "+string.Join(" | ",roundTrip.Tables.Where(x => !x.Passed).Select(x => x.Describe()))));
					ValidationReport afterRoundTrip=Time("Record post-round-trip report",() => Validator.Run(db,adopted,null,null));
					List<Difference> listRoundTripDiffs=ReportComparer.Compare(afterAdoption,afterRoundTrip,roundTrip.GetAllowedNormalizations());
					List<Difference> listRoundTripUnexpected=listRoundTripDiffs.Where(x => !x.IsAllowed).ToList();
					Check("R2","Round trip leaves the database unchanged apart from the first-save normalizations",listRoundTripUnexpected.Count==0,
						listRoundTripDiffs.Count==0 ? "row counts and every checksum equal"
						: listRoundTripUnexpected.Count==0 ? "only the normalized columns changed: "+string.Join("; ",listRoundTripDiffs
							.Where(x => x.Kind!=DifferenceKind.ChecksumTableChanged).Select(x => x.Table+"."+x.Column))
						: ReportComparer.Format(listRoundTripDiffs));
					afterFirstPass=afterRoundTrip;
				}
				//11. Idempotence: everything a second time.
				IdempotencePass(db,source,adopted,snapshot,afterFirstPass);
			}
		}

		///<summary>What the round trip covered, column by column: R1's detail, and the counts the validation report quotes.</summary>
		public static string DescribeRoundTrip(RoundTripSummary roundTrip) {
			int countLoaded=roundTrip.Tables.Sum(x => x.ColumnsLoaded);
			int countWritten=roundTrip.Tables.Sum(x => x.ColumnsWritten);
			List<string> listNotWritten=roundTrip.Tables.SelectMany(x => x.ListNotUpdatable.Select(y => x.Table+"."+y)).ToList();
			List<string> listNotLoaded=roundTrip.Tables.Where(x => x.Columns.Count>x.ColumnsLoaded).SelectMany(x => x.ListDocumentedExceptions
				.Select(y => y.Split(':')[0]).Select(y => y.Contains(".") ? y : y+" ("+(x.Columns.Count-x.ColumnsLoaded)+" columns)")).ToList();
			return roundTrip.Tables.Count+" tables, "+roundTrip.ColumnCount+" columns new or changed since 24.3: "+countLoaded+" loaded and saved unchanged ("
				+roundTrip.RowsSaved.ToString("N0")+" rows saved), and "+countWritten+" of them given a new value, written and read back; "
				+listNotWritten.Count+" loaded and saved but not given a new value, because Insert or the server sets them (CRUD types TimeStamp, DateEntry, "
				+"DateTEntry, ExcludeFromUpdate: "+string.Join(", ",listNotWritten)+"); "
				+(roundTrip.ColumnCount-countLoaded)+" not loaded, documented exceptions preserved untouched ("+string.Join(", ",listNotLoaded)+")";
		}

		private void ValidateAgainstBaseline(ValidationReport baseline,ValidationReport after,List<Difference> listDiffs) {
			_run.ListAllowedDifferences=listDiffs.Where(x => x.IsAllowed && x.Kind!=DifferenceKind.ChecksumTableChanged).Select(x => x.ToString()).ToList();
			_run.ListUnexpectedDifferences=listDiffs.Where(x => !x.IsAllowed).Select(x => x.ToString()).ToList();
			HashSet<string> setTouched=new HashSet<string>(listDiffs.Where(x => x.Table!=null).Select(x => x.Table),StringComparer.OrdinalIgnoreCase);
			_run.ListTablesTouched=setTouched.OrderBy(x => x).ToList();
			_run.TablesUntouched=baseline.Tables.Count(x => !setTouched.Contains(x.Name));
			int countUntouchedWithRows=baseline.Tables.Count(x => !setTouched.Contains(x.Name) && x.Rows>0);
			List<Difference> listRows=listDiffs.Where(x => x.Kind==DifferenceKind.RowsChanged).ToList();
			Check("V1","Row counts unchanged except where adoption deletes or adds rows",listRows.All(x => x.IsAllowed) && baseline.TableCount==after.TableCount,
				baseline.TableCount+" tables before, "+after.TableCount+" after; "+(baseline.TotalRows).ToString("N0")+" rows before, "+after.TotalRows.ToString("N0")
				+" after; changed: "+(listRows.Count==0 ? "none" : string.Join(", ",listRows.Select(x => x.Table+" "+x.Before+" -> "+x.After))));
			Check("V2","Checksums of untouched tables identical",listDiffs.Where(x => x.Table!=null).All(x => x.IsAllowed),
				_run.TablesUntouched+" of "+baseline.TableCount+" tables ("+countUntouchedWithRows+" that hold rows and "+(_run.TablesUntouched-countUntouchedWithRows)
				+" empty in both) have identical row counts, CHECKSUM TABLE and column checksums; touched (each by an allowed adoption change): "
				+string.Join(", ",_run.ListTablesTouched));
			Check("V3","Every difference is an allowed adoption difference",_run.ListUnexpectedDifferences.Count==0,
				_run.ListAllowedDifferences.Count+" allowed, "+_run.ListUnexpectedDifferences.Count+" unexpected"
				+(_run.ListUnexpectedDifferences.Count==0 ? "" : ": "+string.Join("; ",_run.ListUnexpectedDifferences.Take(20))));
			List<Difference> listTotals=listDiffs.Where(x => x.Kind==DifferenceKind.TotalChanged || x.Kind==DifferenceKind.TotalAdded
				|| x.Kind==DifferenceKind.TotalRemoved).ToList();
			int countSame=baseline.Totals.Count(x => after.Totals.TryGetValue(x.Key,out string value) && value==x.Value);
			Check("V4","Business totals unchanged (aging, production, income, payment plans and the rest)",listTotals.All(x => x.IsAllowed),
				countSame+" of "+baseline.Totals.Count+" totals identical; "+(listTotals.Count==0 ? "no total changed" : "changed: "
				+string.Join("; ",listTotals.Select(x => x.ToString()))));
		}

		private void ValidateAdoptionEffects(OfficeFixture.Summary fixture) {
			bool hasColumn=BusinessLayer.ColumnExists("patient",Adoption.PATIENT_PORTAL_EMAIL_COLUMN);
			bool hasIndex=BusinessLayer.IndexExists("patient",Adoption.PATIENT_PORTAL_EMAIL_INDEX);
			Check("V5","patient._portal_active_email and its unique index dropped",!hasColumn && !hasIndex,
				"column "+(hasColumn ? "present" : "gone")+", index "+Adoption.PATIENT_PORTAL_EMAIL_INDEX+" "+(hasIndex ? "present" : "gone")
				+"; two active patients may now share an e-mail address");
			long countUpdate=BusinessLayer.Count("SELECT COUNT(*) FROM documentmisc WHERE DocMiscType IN ("+(int)DocumentMiscType.UpdateFiles+","
				+(int)DocumentMiscType.UpdateFilesSegment+")");
			long countOther=BusinessLayer.Count("SELECT COUNT(*) FROM documentmisc WHERE DocMiscType NOT IN ("+(int)DocumentMiscType.UpdateFiles+","
				+(int)DocumentMiscType.UpdateFilesSegment+")");
			Check("V6","Open Dental UpdateFiles rows removed",countUpdate==0 && countOther==fixture.OtherDocumentMiscRows,
				fixture.UpdateFileRows+" UpdateFiles/UpdateFilesSegment rows before, "+countUpdate+" after; other documentmisc rows kept: "+countOther+" of "
				+fixture.OtherDocumentMiscRows);
			List<string> listNotBlank=OfficeFixture.ArrayHqPrefNames.Where(x => (BusinessLayer.GetPref(x)??"")!="").ToList();
			bool isVendorKept=BusinessLayer.GetPref(OfficeFixture.PREF_VENDOR_URL)==OfficeFixture.VENDOR_URL;
			bool isTextKept=(BusinessLayer.GetPref(OfficeFixture.PREF_HQ_TEXT)??"").StartsWith("See https://");
			Check("V7","Open Dental server addresses blanked, other preferences kept",listNotBlank.Count==0 && isVendorKept && isTextKept,
				OfficeFixture.ArrayHqPrefNames.Length+" address preferences, "+listNotBlank.Count+" still set"+(listNotBlank.Count>0 ? " ("+string.Join(", ",listNotBlank)+")" : "")
				+"; vendor address kept "+isVendorKept+"; help text kept "+isTextKept);
			ForkDbState state=ForkVersions.ReadState();
			string latest=ForkMigrationRunner.GetSteps().Max(x => x.Level).ToString(CultureInfo.InvariantCulture);
			bool isSentinel=state.DataBaseVersion==ForkVersions.SentinelVersion.ToString(4) && state.ProgramVersion==ForkVersions.SentinelVersion.ToString(4);
			bool isMarked=state.ForkSchemaLevel==latest && state.OdSchemaCompat==SchemaVerifier.ReferenceVersion
				&& Adoption.ParseAdoptedFromSource(state.ForkAdoptedFrom)==new Version(25,3,48,0) && state.IsAdoptionRecordFinished;
			Check("V8","Sentinel versions and fork markers set",isSentinel && isMarked,
				"DataBaseVersion "+state.DataBaseVersion+", ProgramVersion "+state.ProgramVersion+", ForkSchemaLevel "+state.ForkSchemaLevel+" (latest step "+latest
				+"), OdSchemaCompat "+state.OdSchemaCompat+", ForkAdoptedFrom "+state.ForkAdoptedFrom);
			ForkGuardResult guard=ForkVersionGuard.Evaluate(state);
			Check("V9","Fork startup guard opens the adopted copy",guard.Action==ForkGuardAction.ProceedFork && !guard.HasPendingMigrations,
				guard.Action+", pending migrations "+guard.HasPendingMigrations);
		}

		private void IdempotencePass(Db db,string source,string adopted,string snapshot,ValidationReport afterFirstPass) {
			Stopwatch stopwatch=Stopwatch.StartNew();
			AdoptionPreflight preflight=Adoption.Preflight();
			string refusal="";
			try {
				Adoption.Run(null);
				refusal="not refused";
			}
			catch(ApplicationException ex) {
				refusal=ex.Message;
			}
			Check("I1","A second adoption is refused",!preflight.CanAdopt && preflight.IsAlreadyAdopted && refusal!="not refused",refusal);
			//An operator running runbook step 4 ("AdoptionValidation adopt") a second time on the adopted copy, row check included.
			HeadlessAdoption.Result again=HeadlessAdoption.Run(adopted,null,null,_options.Server,source);
			ForkMigrationResult migrationResult=again.Migration;
			SchemaVerifyResult verifyResult=again.Verify;
			Check("I2","A second fork migration changes nothing and the schema still matches",again.WasAlreadyAdopted && again.Adoption==null
				&& migrationResult.ListChanges.Count==0 && !again.HasPendingMigrations && verifyResult.IsMatch && verifyResult.ListDifferences.Count==0
				&& again.RowCheck!=null && again.RowCheck.Passed,
				"adoption skipped (already adopted); "+migrationResult.ListStepsRun.Count+" steps run, "+migrationResult.ListChanges.Count+" changes; verifier "
				+verifyResult.ListDifferences.Count+" differences; row check against the source "+(again.RowCheck!=null && again.RowCheck.Passed ? "passed"
				: "failed: "+string.Join(" ",again.RowCheck?.ListFailures??new List<string>())));
			ValidationReport afterRerun=Validator.Run(db,adopted,null,null);
			List<Difference> listDiffs=ReportComparer.Compare(afterFirstPass,afterRerun,null);
			//Every table but securitylog and securityloghash by its report; those two gain the rerun's own entry, so they are compared row by row with the
			//snapshot taken right after adoption, and so is preference.
			List<Difference> listUnexpected=listDiffs.Where(x => x.Table!="securitylog" && x.Table!="securityloghash").ToList();
			List<Difference> listHashDiffs=listDiffs.Where(x => x.Column=="SecurityHash").ToList();
			bool isSameHashes=again.ListHashesStart.Select(x => x.ToString()).SequenceEqual(again.ListHashesAfter.Select(x => x.ToString()));
			Check("I3","A second rehash changes nothing",listHashDiffs.Count==0 && isSameHashes && again.IsRehashComplete,
				listHashDiffs.Count==0 && isSameHashes ? "every hash already current: "+string.Join(", ",again.ListHashesStart.Select(x => x.Table+" "+x.Current))
				: ReportComparer.Format(listHashDiffs));
			long expectedAdded=again.IsRehashLogged ? 1 : 0;
			RowComparer.TableDiff diffPreference=RowComparer.Compare(db,snapshot,adopted,"preference");
			RowComparer.TableDiff diffLog=RowComparer.Compare(db,snapshot,adopted,"securitylog");
			RowComparer.TableDiff diffLogHash=RowComparer.Compare(db,snapshot,adopted,"securityloghash");
			List<string> listRowProblems=new List<string>();
			if(!diffPreference.IsIdentical) {
				listRowProblems.Add(diffPreference.Describe());
			}
			foreach(RowComparer.TableDiff diff in new[] { diffLog,diffLogHash }) {
				if(diff.CountOnlyFirst>0 || diff.CountChanged>0 || diff.ListColumnsInOneOnly.Count>0 || diff.CountOnlySecond!=expectedAdded) {
					listRowProblems.Add(diff.Describe()+" (expected only "+expectedAdded+" added row)");
				}
			}
			Check("I4","The whole second pass leaves the database unchanged",listUnexpected.Count==0 && listRowProblems.Count==0,
				listUnexpected.Count==0 && listRowProblems.Count==0
					? "every table except securitylog and securityloghash has the row counts and checksums of the first pass; row by row against the snapshot "
						+"taken after adoption, preference is identical, and securitylog and securityloghash only gained the rerun's own entry ("
						+diffLog.CountOnlySecond+" and "+diffLogHash.CountOnlySecond+" row), with no existing row changed or removed"
					: ReportComparer.Format(listUnexpected)+" "+string.Join("; ",listRowProblems));
			_run.ListSteps.Add(new StepTiming { Name="Second pass (adoption, migration, verifier, rehash, row check, report)",Seconds=stopwatch.Elapsed.TotalSeconds });
		}

		///<summary>Copies the tables that hold the time of adoption (ArraySnapshotTables) into the scratch database snapshot, for the row-by-row
		///comparisons of I4 and X2.</summary>
		private static void SnapshotTables(Db db,string database,string snapshot) {
			LocalOnly.EnsureWritableDatabaseName(snapshot);
			LocalOnly.EnsureWritableDatabaseName(database);
			db.Execute("DROP DATABASE IF EXISTS `"+snapshot+"`");
			db.Execute("CREATE DATABASE `"+snapshot+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			foreach(string table in ArraySnapshotTables) {
				db.Execute("CREATE TABLE `"+snapshot+"`.`"+table+"` LIKE `"+database+"`.`"+table+"`");
				db.Execute("INSERT INTO `"+snapshot+"`.`"+table+"` SELECT * FROM `"+database+"`.`"+table+"`");
			}
		}

		private List<StartupScenarioResult> RunStartupScenarios(string databaseLabel) {
			OdDatabaseState state=OdDatabaseState.Read();
			List<StartupScenarioResult> list=new List<StartupScenarioResult>();
			foreach((string name,OdClient client,bool isClassicView) scenario in OdStartupCheck.StandardScenarios()) {
				OdDatabaseState stateScenario=(OdDatabaseState)typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic)
					.Invoke(state,null);
				stateScenario.IsClassicUpdateView|=scenario.isClassicView;
				OdStartupResult result=OdStartupCheck.Evaluate(scenario.client,stateScenario);
				list.Add(new StartupScenarioResult {
					Database=databaseLabel,
					Scenario=scenario.name,
					IsReleaseClient=!scenario.client.IsDebugBuild,
					Outcome=result.Outcome,
					ExitCode=result.ExitCode,
					IsRefused=result.IsRefused,
					Summary=result.Describe(),
					ListTrace=result.ListTrace,
				});
			}
			return list;
		}

		private void CompareRuns(RehearsalResult rehearsalResult,List<ValidationReport> listBaselines,List<ValidationReport> listAdopted,List<string> listSnapshots) {
			if(listBaselines.Any(x => x==null) || listAdopted.Any(x => x==null)) {
				rehearsalResult.ListCrossRunChecks.Add(new CheckResult { Id="X0",Name="Every run produced its reports",Passed=false,Detail="a run failed early" });
				return;
			}
			List<string> listBaselineDiffs=new List<string>();
			List<string> listAdoptedDiffs=new List<string>();
			//The reports compare every table but the three that hold the time of adoption; those are compared row by row below.
			AllowedDifferences allowedTimes=new AllowedDifferences { Rules=ArraySnapshotTables.Select(x => new AllowedRule { Kind="*",Table=x,
				Reason="compared row by row instead" }).ToList() };
			for(int i=1;i<listBaselines.Count;i++) {
				listBaselineDiffs.AddRange(ReportComparer.Compare(listBaselines[0],listBaselines[i],null).Select(x => "run "+(i+1)+": "+x));
				listAdoptedDiffs.AddRange(ReportComparer.Compare(listAdopted[0],listAdopted[i],allowedTimes).Where(x => !x.IsAllowed).Select(x => "run "+(i+1)+": "+x));
			}
			long countLogTimes=0;
			long countLogHashes=0;
			try {
				CompareSnapshots(listSnapshots,listAdoptedDiffs,ref countLogTimes,ref countLogHashes);
			}
			catch(Exception ex) {
				listAdoptedDiffs.Add("the row-by-row comparison failed: "+ex.Message);
			}
			rehearsalResult.ListCrossRunChecks.Add(new CheckResult { Id="X1",Name="Every run starts from an identical database",Passed=listBaselineDiffs.Count==0,
				Detail=listBaselineDiffs.Count==0 ? listBaselines.Count+" baselines identical (row counts, every checksum, every total)"
					: string.Join("; ",listBaselineDiffs.Take(20)) });
			rehearsalResult.ListCrossRunChecks.Add(new CheckResult { Id="X2",Name="Every run ends with an identical adopted database, apart from the time of adoption",
				Passed=listAdoptedDiffs.Count==0,
				Detail=listAdoptedDiffs.Count==0 ? listAdopted.Count+" adopted databases identical: every table except preference, securitylog and securityloghash "
					+"by its report (row counts, CHECKSUM TABLE, every column checksum, every total); those three row by row, where they differ only in the time "
					+"of adoption: the value of ForkAdoptedFrom, securitylog.LogDateTime ("+countLogTimes+" rows) and the securityloghash.LogHash computed from "
					+"it ("+countLogHashes+" rows)"
					: string.Join("; ",listAdoptedDiffs.Take(20)) });
			List<string> listOutcomes=rehearsalResult.ListRuns.Select(x => string.Join(",",x.ListChecks.Select(y => y.Id+"="+y.Passed))).Distinct().ToList();
			rehearsalResult.ListCrossRunChecks.Add(new CheckResult { Id="X3",Name="Every run gives the same check results",Passed=listOutcomes.Count==1,
				Detail=listOutcomes.Count==1 ? rehearsalResult.ListRuns.Count+" runs, "+rehearsalResult.ListRuns[0].ListChecks.Count+" checks each" : string.Join(" / ",listOutcomes) });
		}

		///<summary>X2's row-by-row part: run 1's snapshot of the tables that hold the time of adoption against every other run's.</summary>
		private void CompareSnapshots(List<string> listSnapshots,List<string> listAdoptedDiffs,ref long countLogTimes,ref long countLogHashes) {
			using(Db db=Db.Open(_options.Server)) {
				for(int i=1;i<listSnapshots.Count;i++) {
					//Row by row: preference may differ only in ForkAdoptedFrom's value; securitylog only in LogDateTime, and securityloghash only in the LogHash
					//computed from it. Everything else in them, every row and every other column, must be identical.
					RowComparer.TableDiff diffPreference=RowComparer.Compare(db,listSnapshots[0],listSnapshots[i],"preference");
					foreach(RowComparer.ChangedRow changed in diffPreference.ListChanged) {
						string name=Convert.ToString(db.Scalar("SELECT PrefName FROM `"+listSnapshots[0]+"`.preference WHERE PrefNum="
							+changed.Key.ToString(CultureInfo.InvariantCulture)));
						if(name!=ForkVersions.PREF_FORK_ADOPTED_FROM || changed.ListColumns.Count!=1 || changed.ListColumns[0]!="ValueString") {
							listAdoptedDiffs.Add("run "+(i+1)+": preference "+name+" (PrefNum "+changed.Key+") differs in "+string.Join(", ",changed.ListColumns));
						}
					}
					if(diffPreference.CountOnlyFirst>0 || diffPreference.CountOnlySecond>0 || diffPreference.ListColumnsInOneOnly.Count>0
						|| diffPreference.CountChanged>diffPreference.ListChanged.Count)
					{
						listAdoptedDiffs.Add("run "+(i+1)+": "+diffPreference.Describe());
					}
					RowComparer.TableDiff diffLog=RowComparer.Compare(db,listSnapshots[0],listSnapshots[i],"securitylog",new[] { "LogDateTime" });
					RowComparer.TableDiff diffLogHash=RowComparer.Compare(db,listSnapshots[0],listSnapshots[i],"securityloghash",new[] { "LogHash" });
					foreach(RowComparer.TableDiff diff in new[] { diffLog,diffLogHash }) {
						if(!diff.IsIdentical) {
							listAdoptedDiffs.Add("run "+(i+1)+": "+diff.Describe());
						}
					}
					countLogTimes+=db.ScalarLong("SELECT COUNT(*) FROM `"+listSnapshots[0]+"`.securitylog a INNER JOIN `"+listSnapshots[i]+"`.securitylog b "
						+"USING(SecurityLogNum) WHERE NOT (a.LogDateTime<=>b.LogDateTime)");
					countLogHashes+=db.ScalarLong("SELECT COUNT(*) FROM `"+listSnapshots[0]+"`.securityloghash a INNER JOIN `"+listSnapshots[i]+"`.securityloghash b "
						+"USING(SecurityLogHashNum) WHERE NOT (BINARY a.LogHash<=>BINARY b.LogHash)");
				}
			}
		}

		private void Cleanup(params string[] arrayDatabases) {
			try {
				using(Db db=Db.Open(_options.Server)) {
					foreach(string database in arrayDatabases) {
						LocalOnly.EnsureWritableDatabaseName(database);
						db.Execute("DROP DATABASE IF EXISTS `"+database+"`");
					}
				}
			}
			catch(Exception ex) {
				_log("Cleanup failed: "+ex.Message);
			}
		}

		private string FindAllowedPath() {
			if(!string.IsNullOrEmpty(_options.AllowedPath)) {
				return _options.AllowedPath;
			}
			const string fileName="allowed-differences.adoption.json";
			for(DirectoryInfo dir=new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));dir!=null;dir=dir.Parent) {
				foreach(string candidate in new[] { Path.Combine(dir.FullName,fileName),Path.Combine(dir.FullName,"tools","SyntheticData",fileName) }) {
					if(File.Exists(candidate)) {
						return candidate;
					}
				}
			}
			throw new FileNotFoundException(fileName+" was not found next to the program or in tools\\SyntheticData.");
		}

		private void Check(string id,string name,bool passed,string detail) {
			CheckResult check=new CheckResult { Id=id,Name=name,Passed=passed,Detail=detail??"" };
			_run.ListChecks.Add(check);
			_log((passed ? "  PASS " : "  FAIL ")+id+" "+name+": "+check.Detail);
		}

		private T Time<T>(string name,Func<T> func,Func<string> note=null) {
			_log("- "+name);
			Stopwatch stopwatch=Stopwatch.StartNew();
			T result=func();
			_run.ListSteps.Add(new StepTiming { Name=name,Seconds=stopwatch.Elapsed.TotalSeconds,Note=note?.Invoke()??"" });
			return result;
		}

		private void Time(string name,Action action,Func<string> note=null) {
			Time<object>(name,() => { action(); return null; },note);
		}
	}
}
