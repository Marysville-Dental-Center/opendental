// SWMAP (owner question Switch-over 1, answered 2026-11-07): the settings that release v0.0 kept under names of its own and that this program now
// reads under Open Dental's names, and the fork migration step that moves the rows v0.0 wrote. See docs/port-notes/SWMAP.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	//A feature package's data step, not one of the 25.3.48 schema steps: it lives in the FeatureSteps namespace (see XConnectEnableStep).

	///<summary>An Open Dental preference row that a release v0.0 row moves to.</summary>
	public class OdSettingTarget {
		///<summary>Open Dental's name of the row.</summary>
		public string PrefNameOd;
		///<summary>What this program reads when the row is missing. When Open Dental's row is missing, a value that reads the same is not inserted.
		///</summary>
		public string ValueMissing;
		///<summary>True when the row holds a bool (1/0): values are compared as PIn.Bool reads them. False: compared as milliseconds
		///(ReplicationDelay.ParseMs).</summary>
		public bool IsBool;
		///<summary>Open Dental's value for the value of the v0.0 row, or null to leave Open Dental's row as it is.</summary>
		public Func<string,string> FuncConvert;

		///<summary>True when the two stored values read the same.</summary>
		public bool IsSameValue(string valueA,string valueB) {
			if(IsBool) {
				return PIn.Bool(valueA??"")==PIn.Bool(valueB??"");
			}
			return ReplicationDelay.ParseMs(valueA)==ReplicationDelay.ParseMs(valueB);
		}
	}

	///<summary>A setting that release v0.0 kept in a row of its own, and the Open Dental row or rows this program reads it from now.</summary>
	public class OdSettingRename {
		///<summary>The row release v0.0 wrote.</summary>
		public string PrefNameV0;
		///<summary>The package of the setting, for messages.</summary>
		public string Package;
		public List<OdSettingTarget> ListTargets=new List<OdSettingTarget>();
	}

	///<summary>One preference row the step inserts, changes or deletes.</summary>
	public class OdSettingRowChange {
		public string PrefName;
		///<summary>The stored value before, or null when there is no row.</summary>
		public string ValueBefore;
		///<summary>The value after, or null when the row is deleted.</summary>
		public string ValueAfter;

		public string Describe() {
			if(ValueAfter==null) {
				return "delete "+PrefName+" (was '"+ValueBefore+"')";
			}
			if(ValueBefore==null) {
				return "insert "+PrefName+"='"+ValueAfter+"'";
			}
			return "set "+PrefName+" from '"+ValueBefore+"' to '"+ValueAfter+"'";
		}
	}

	///<summary>A setting this program reads under Open Dental's own name or names, for migration-preview (runbook step 0.2).</summary>
	public class OdSettingRead {
		///<summary>The setting's label in this program's window.</summary>
		public string Label;
		public string Package;
		///<summary>Open Dental's names of the rows it is read from.</summary>
		public string[] ArrayPrefNamesOd;
		///<summary>What this program shows, from the stored values of ArrayPrefNamesOd in that order (null for a missing row).</summary>
		public Func<string[],string> FuncShown;
	}

	///<summary>The settings Open Dental 24.4 to 25.3 added that this program reads under Open Dental's own names (docs/port-notes/SWMAP.md), and the
	///moves of the rows release v0.0 wrote under names of its own. Shared by OdSettingNamesStep and AdoptionValidation's migration-preview, so the
	///two never disagree.
	///The rule (Plan): for each v0.0 row that exists, each Open Dental row gets the converted value, unless it already reads the same, or it is
	///missing and the value reads the same as a missing row; then the v0.0 row is deleted. When both rows exist the v0.0 row wins, because it is the
	///value release v0.0 used. An Open Dental 25.3.48 database never has a v0.0 row, so nothing changes on it.</summary>
	public static class OdSettingNames {
		///<summary>Every setting release v0.0 kept under a name of its own that Open Dental 25.3.48 has under another name. The payment plan lock is
		///inverted; the read delay becomes Open Dental's Enabled and milliseconds rows.</summary>
		public static readonly List<OdSettingRename> ListRenames=new List<OdSettingRename>() {
			RenameBool("ForkApptProvTimeBarsOnOpBackground",ApptTimeBars.PREF_PROV_TIME_ON_OP_BACKGROUND,"B-APPT"),
			RenameBool("ForkApptTimeBarSecondaryColorNoProvTime",ApptTimeBars.PREF_SECONDARY_COLOR_NO_PROV_TIME,"B-APPT"),
			RenameBool("ForkClaimMedReceivedPromptForMedicalEob",ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT,"B-CLAIM"),
			RenameBool("ForkEraAutoSplitCarcDenials",EraAutoSplit.PREF_NAME,"B-ERA"),
			new OdSettingRename {
				PrefNameV0="SecurityLockExcludesPayPlans",
				Package="B-PAY",
				ListTargets=new List<OdSettingTarget>() {
					//v0.0 stored "payment plans are not affected" (1 = not affected); Open Dental stores "included" (1 = blocked by the lock).
					new OdSettingTarget { PrefNameOd=PayPlanGlobalLock.PREF_NAME,ValueMissing="1",IsBool=true,
						FuncConvert=x => PayPlanGlobalLock.GetValueForSetting(PIn.Bool(x??"")) },
				},
			},
			RenameBool("PerioUseRecessionTerm",PerioMeasures.PrefNameUseRecessionTerm,"B-PERIO"),
			new OdSettingRename {
				PrefNameV0="ForkReplicationReadDelayMs",
				Package="B-SERVER",
				ListTargets=new List<OdSettingTarget>() {
					new OdSettingTarget { PrefNameOd=ReplicationDelay.PREF_READ_DELAY_ENABLED,ValueMissing="0",IsBool=true,
						FuncConvert=x => POut.Bool(ReplicationDelay.ParseMs(x)>0) },
					//A delay of 0 keeps Open Dental's milliseconds: Enabled 0 already turns the delay off.
					new OdSettingTarget { PrefNameOd=ReplicationDelay.PREF_READ_DELAY_MS,ValueMissing="0",IsBool=false,
						FuncConvert=x => ReplicationDelay.ParseMs(x)>0 ? POut.Int(ReplicationDelay.ParseMs(x)) : null },
				},
			},
			RenameBool("ForkModuleSwitchBlur",ModuleSwitchBlur.PREF_NAME,"B-SHELL"),
		};

		///<summary>Every setting this program reads under Open Dental's own names, in the order of docs/port-notes/SWMAP.md: the renamed ones, the
		///sales tax procedure code, whose name never changed, and the eight options of package ODOPTS (docs/port-notes/ODOPTS.md), which never had a
		///name of this program's own.</summary>
		public static readonly List<OdSettingRead> ListReads=new List<OdSettingRead>() {
			new OdSettingRead { Label="Sales Tax procedure code",Package="B-ACCT",ArrayPrefNamesOd=new[] { SalesTaxProcs.PREF_PROC_CODE },
				FuncShown=x => string.IsNullOrWhiteSpace(x[0]) ? "blank" : x[0].Trim() },
			ReadBool("Prompt for Medical EOB on primary claims","B-CLAIM",ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT),
			ReadBool("Show provider time on the operatory background in the provider's color","B-APPT",ApptTimeBars.PREF_PROV_TIME_ON_OP_BACKGROUND),
			ReadBool("No provider time set: show the secondary provider color in the time bar","B-APPT",
				ApptTimeBars.PREF_SECONDARY_COLOR_NO_PROV_TIME),
			ReadBool("Auto split ERAs with CARC denial codes listed above","B-ERA",EraAutoSplit.PREF_NAME),
			new OdSettingRead { Label="Payment plans are not affected by the global lock",Package="B-PAY",ArrayPrefNamesOd=new[] { PayPlanGlobalLock.PREF_NAME },
				FuncShown=x => GetTicked(PayPlanGlobalLock.IsOnFromValue(x[0])) },
			ReadBool("Use the term \"Recession\"","B-PERIO",PerioMeasures.PrefNameUseRecessionTerm),
			new OdSettingRead { Label="Read delay after write (ms)",Package="B-SERVER",
				ArrayPrefNamesOd=new[] { ReplicationDelay.PREF_READ_DELAY_ENABLED,ReplicationDelay.PREF_READ_DELAY_MS },
				FuncShown=x => POut.Int(ReplicationDelay.GetReadDelayMsFromValues(x[0],x[1])) },
			ReadBool("Blur when switching between modules to prevent accidental clicking","B-SHELL",ModuleSwitchBlur.PREF_NAME),
			//ODOPTS (2026-11-07): the eight options built after SWMAP, read under Open Dental's names from the start (no release v0.0 row to move).
			new OdSettingRead { Label="Perio Chart: auto Att Ging red if at or below (mm)",Package="ODOPTS",
				ArrayPrefNamesOd=new[] { PerioMeasures.PrefNameRedAttGing },
				FuncShown=x => PerioMeasures.GetRedAttGingFromValue(x[0])?.ToString()??"none (never red)" },
			ReadBool("Offer domain login in the Change User of signature boxes","ODOPTS",DomainLogin.PREF_FOR_SIG_BOXES,defaultValue:true),
			ReadBool(ClaimFeeForSecondary.LABEL,"ODOPTS",ClaimFeeForSecondary.PREF_NAME,defaultValue:false),
			ReadBool("eForm import: an allergy matching no list entry is imported as Other","ODOPTS",EFormImport.PREF_ALLERGIES_AS_OTHER,
				defaultValue:true),
			ReadBool("eForm import: a problem matching no list entry is imported as Other","ODOPTS",EFormImport.PREF_PROBLEMS_AS_OTHER,
				defaultValue:true),
			new OdSettingRead { Label="Insurance History perio scaling procedure code",Package="ODOPTS",
				ArrayPrefNamesOd=new[] { InsHistCategories.PREF_PERIO_SCALING_CODE },
				FuncShown=x => string.IsNullOrWhiteSpace(x[0]) ? "blank (each perio scaling code group's History Proc Code)" : x[0].Trim() },
			new OdSettingRead { Label="Send statements by e-mail when billing creates them",Package="ODOPTS",
				ArrayPrefNamesOd=new[] { StatementAutoSend.PREF_EMAIL },
				//Always is used as Ask until Statement Send Setup is saved in this program (Open Dental's window most likely meant Message-to-Pay).
				FuncShown=x => StatementAutoSend.GetModeFromValue(x[0])==StatementAutoSendMode.Always
					? "Always (asks until Statement Send Setup is saved in this program)"
					: StatementAutoSend.GetModeFromValue(x[0]).ToString() },
			new OdSettingRead { Label="Send statements by text when billing creates them",Package="ODOPTS",
				ArrayPrefNamesOd=new[] { StatementAutoSend.PREF_SMS },
				FuncShown=x => StatementAutoSend.GetModeFromValue(x[0]).ToString()+" (no texting provider: nothing is texted)" },
		};

		private static OdSettingRename RenameBool(string prefNameV0,string prefNameOd,string package) {
			return new OdSettingRename {
				PrefNameV0=prefNameV0,
				Package=package,
				ListTargets=new List<OdSettingTarget>() {
					new OdSettingTarget { PrefNameOd=prefNameOd,ValueMissing="0",IsBool=true,FuncConvert=x => POut.Bool(PIn.Bool(x??"")) },
				},
			};
		}

		private static OdSettingRead ReadBool(string label,string package,string prefNameOd) {
			return new OdSettingRead { Label=label,Package=package,ArrayPrefNamesOd=new[] { prefNameOd },
				FuncShown=x => GetTicked(x[0]!=null && PIn.Bool(x[0])) };
		}

		///<summary>A bool setting whose missing or blank row reads as defaultValue (OdOptions.GetBoolFromValue).</summary>
		private static OdSettingRead ReadBool(string label,string package,string prefNameOd,bool defaultValue) {
			return new OdSettingRead { Label=label,Package=package,ArrayPrefNamesOd=new[] { prefNameOd },
				FuncShown=x => GetTicked(OdOptions.GetBoolFromValue(x[0],defaultValue)) };
		}

		private static string GetTicked(bool isTicked) {
			return isTicked ? "ticked" : "not ticked";
		}

		///<summary>The rows the step changes, in order, worked out from the stored values funcGetRaw returns (null for a missing row). Writes nothing.
		///</summary>
		public static List<OdSettingRowChange> Plan(Func<string,string> funcGetRaw) {
			List<OdSettingRowChange> listChanges=new List<OdSettingRowChange>();
			for(int i=0;i<ListRenames.Count;i++) {
				OdSettingRename rename=ListRenames[i];
				string valueV0=funcGetRaw(rename.PrefNameV0);
				if(valueV0==null) {
					continue;
				}
				for(int j=0;j<rename.ListTargets.Count;j++) {
					OdSettingTarget target=rename.ListTargets[j];
					string valueOd=target.FuncConvert(valueV0);
					if(valueOd==null) {
						continue;
					}
					string valueCur=funcGetRaw(target.PrefNameOd);
					if(target.IsSameValue(valueCur??target.ValueMissing,valueOd)) {
						continue;
					}
					listChanges.Add(new OdSettingRowChange { PrefName=target.PrefNameOd,ValueBefore=valueCur,ValueAfter=valueOd });
				}
				listChanges.Add(new OdSettingRowChange { PrefName=rename.PrefNameV0,ValueBefore=valueV0,ValueAfter=null });
			}
			return listChanges;
		}

		///<summary>Applies the plan to the connected database through the step's context, which logs each statement. Returns the changes made.
		///</summary>
		public static List<OdSettingRowChange> Run(ForkMigrationContext context) {
			List<OdSettingRowChange> listChanges=Plan(x => context.GetPrefRaw(x));
			for(int i=0;i<listChanges.Count;i++) {
				OdSettingRowChange change=listChanges[i];
				string prefName=POut.String(change.PrefName);
				if(change.ValueAfter==null) {
					context.ExecuteData("DELETE FROM preference WHERE PrefName='"+prefName+"'");
				}
				else if(change.ValueBefore==null) {
					context.ExecuteData("INSERT INTO preference (PrefName,ValueString) VALUES ('"+prefName+"','"+POut.String(change.ValueAfter)+"')");
				}
				else {
					context.ExecuteData("UPDATE preference SET ValueString='"+POut.String(change.ValueAfter)+"' WHERE PrefName='"+prefName+"'");
				}
			}
			return listChanges;
		}

		///<summary>One line per setting of ListReads: its label, what this program shows and the stored values, from funcGetRaw (null for a missing
		///row). For migration-preview.</summary>
		public static List<string> DescribeReads(Func<string,string> funcGetRaw) {
			List<string> listLines=new List<string>();
			for(int i=0;i<ListReads.Count;i++) {
				OdSettingRead read=ListReads[i];
				string[] arrayValues=read.ArrayPrefNamesOd.Select(x => funcGetRaw(x)).ToArray();
				string stored=string.Join(", ",read.ArrayPrefNamesOd.Select((x,idx) => x+(arrayValues[idx]==null ? " no row" : "='"+arrayValues[idx]+"'")));
				listLines.Add(read.Label+" ("+read.Package+"): "+read.FuncShown(arrayValues)+" ["+stored+"]");
			}
			return listLines;
		}
	}

	///<summary>Moves the settings release v0.0 kept under names of its own to Open Dental's names (OdSettingNames.Plan), so the program, which now
	///reads Open Dental's names, keeps the values v0.0 used. Runs on every database. On an Open Dental 25.3.48 database (the office's) there is no
	///v0.0 row, so nothing changes: the office's own rows are read as they are. A second run finds nothing to do.</summary>
	[ForkMigration(2026110731,"Settings under Open Dental's names: the release v0.0 rows moved")]
	public class OdSettingNamesStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			if(!context.TableExists("preference")) {
				return;
			}
			List<OdSettingRowChange> listChanges=OdSettingNames.Run(context);
			if(listChanges.Count>0) {
				context.Log("Moved "+listChanges.Count(x => x.ValueAfter==null)+" setting(s) of release v0.0 to Open Dental's names.");
			}
		}
	}
}
