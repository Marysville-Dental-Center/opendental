// The Fee Tools' Copy Code tool (Open Dental 26.2 item 25, "Added a new Fee Tool to copy fees from one procedure code to another"; package
// P262-TOOLS). Written from the public release note only; the rules below (which fees are copied, what happens to the destination code's other
// fees, the audit entries) are this program's own (INFERRED, docs/port-notes/P262-TOOLS.md). The window is FormFeeSchedTools, tab Copy Code.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenPmsDentalBusiness {
	public partial class Fees {
		///<summary>Every fee row of a procedure code, whatever its clinic, provider or effective date, in one fee schedule, or in every fee schedule
		///when feeSchedNum is 0.</summary>
		public static List<Fee> GetAllRowsForCode(long codeNum,long feeSchedNum=0) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),codeNum,feeSchedNum);
			}
			string command="SELECT * FROM fee WHERE CodeNum="+POut.Long(codeNum);
			if(feeSchedNum!=0) {
				command+=" AND FeeSched="+POut.Long(feeSchedNum);
			}
			command+=" ORDER BY FeeSched,ClinicNum,ProvNum,DateEffective,FeeNum";
			return Crud.FeeCrud.SelectMany(command);
		}

		///<summary>Copies the fees of one procedure code to another (the Copy Code tool of the Fee Tools window). In every fee schedule where the
		///source code has at least one fee (only feeSchedNum, unless it is 0), the destination code gets exactly the source code's fee rows: the same
		///amounts for the same clinics, providers and effective dates, including blank overrides (-1). A destination row with the same clinic,
		///provider and effective date is updated, a missing one is inserted, and a destination row the source code does not have in that fee
		///schedule is deleted (so is a second destination row with the same clinic, provider and date, left from before such duplicates were
		///refused; of two such source rows only the first is copied). Fee schedules where the source code has no fee are not changed. Fee schedule
		///groups stay in sync because every
		///clinic's rows of a fee schedule are copied. Each change makes a Procedure Fee Edit audit entry (FKey: the destination code) and a Fee Edit
		///entry (FKey: the fee), like the other fee tools. Throws an ApplicationException for an invalid pair of codes.</summary>
		public static FeeCodeCopyResult CopyFeesToCode(long codeNumFrom,long codeNumTo,long feeSchedNum=0) {
			Meth.NoCheckMiddleTierRole();
			if(codeNumFrom==0 || codeNumTo==0) {
				throw new ApplicationException(Lans.g("Fees","Select the procedure code to copy from and the one to copy to."));
			}
			if(codeNumFrom==codeNumTo) {
				throw new ApplicationException(Lans.g("Fees","The procedure code to copy to must be another code."));
			}
			string procCodeFrom=ProcedureCodes.GetStringProcCode(codeNumFrom);
			string procCodeTo=ProcedureCodes.GetStringProcCode(codeNumTo);
			List<Fee> listFeesFrom=GetAllRowsForCode(codeNumFrom,feeSchedNum);
			List<Fee> listFeesTo=GetAllRowsForCode(codeNumTo,feeSchedNum);
			FeeCodeCopyResult feeCodeCopyResult=new FeeCodeCopyResult();
			List<long> listFeeSchedNums=listFeesFrom.Select(x => x.FeeSched).Distinct().ToList();
			for(int i=0;i<listFeeSchedNums.Count;i++) {
				//Rows with the same key and date are not allowed, but older databases can have some. Like every lookup, the first row wins (the rows
				//are ordered by FeeNum within a key and date): a source duplicate is not copied, and a destination duplicate is deleted.
				List<Fee> listFromInSched=listFeesFrom.FindAll(x => x.FeeSched==listFeeSchedNums[i])
					.GroupBy(x => GetKeyAndDateText(x))
					.Select(x => x.First())
					.ToList();
				HashSet<string> hashSetFromKeys=new HashSet<string>(listFromInSched.Select(x => GetKeyAndDateText(x)));
				Dictionary<string,Fee> dictToByKey=new Dictionary<string,Fee>();
				List<Fee> listToDelete=new List<Fee>();
				List<Fee> listToInSched=listFeesTo.FindAll(x => x.FeeSched==listFeeSchedNums[i]);
				for(int j=0;j<listToInSched.Count;j++) {
					string key=GetKeyAndDateText(listToInSched[j]);
					if(hashSetFromKeys.Contains(key) && !dictToByKey.ContainsKey(key)) {
						dictToByKey[key]=listToInSched[j];
						continue;
					}
					listToDelete.Add(listToInSched[j]);
				}
				//Destination rows go first, so no insert below can meet a row with the same key and date.
				for(int j=0;j<listToDelete.Count;j++) {
					Fee fee=listToDelete[j];
					string reason=(hashSetFromKeys.Contains(GetKeyAndDateText(fee)) ? "a second fee with the same clinic, provider and effective date"
						: procCodeFrom+" has no such fee");
					DateTime datePrevious=fee.SecDateTEdit;
					Delete(fee,doCheckFeeSchedGroups:false);
					feeCodeCopyResult.CountDeleted++;
					SecurityLogs.MakeLogEntry(EnumPermType.ProcFeeEdit,0,"Procedure: "+procCodeTo+", Old Fee: "+fee.Amount.ToString("c")+", "
						+GetCopyCodeKeyText(fee)+". Fee deleted using the Copy Code tool in the Fee Tools window: "+reason+".",
						codeNumTo,DateTime.MinValue);
					SecurityLogs.MakeLogEntry(EnumPermType.LogFeeEdit,0,"Fee deleted",fee.FeeNum,datePrevious);
				}
				for(int j=0;j<listFromInSched.Count;j++) {
					Fee feeFrom=listFromInSched[j];
					dictToByKey.TryGetValue(GetKeyAndDateText(feeFrom),out Fee feeTo);
					if(feeTo==null) {
						feeTo=new Fee();
						feeTo.Amount=feeFrom.Amount;
						feeTo.FeeSched=feeFrom.FeeSched;
						feeTo.CodeNum=codeNumTo;
						feeTo.ClinicNum=feeFrom.ClinicNum;
						feeTo.ProvNum=feeFrom.ProvNum;
						feeTo.DateEffective=feeFrom.DateEffective;
						Insert(feeTo,doCheckFeeSchedGroups:false);
						feeCodeCopyResult.CountInserted++;
						SecurityLogs.MakeLogEntry(EnumPermType.ProcFeeEdit,0,"Procedure: "+procCodeTo+", New Fee: "+feeTo.Amount.ToString("c")+", "
							+GetCopyCodeKeyText(feeTo)+". Fee copied from "+procCodeFrom+" using the Copy Code tool in the Fee Tools window.",
							codeNumTo,DateTime.MinValue);
						SecurityLogs.MakeLogEntry(EnumPermType.LogFeeEdit,0,"Fee Inserted",feeTo.FeeNum,DateTime.MinValue);
						continue;
					}
					if(feeTo.Amount==feeFrom.Amount) {
						feeCodeCopyResult.CountUnchanged++;
						continue;
					}
					Fee feeToOld=feeTo.Copy();
					feeTo.Amount=feeFrom.Amount;
					Update(feeTo,feeToOld,doCheckFeeSchedGroups:false);
					feeCodeCopyResult.CountUpdated++;
					SecurityLogs.MakeLogEntry(EnumPermType.ProcFeeEdit,0,"Procedure: "+procCodeTo+", Old Fee: "+feeToOld.Amount.ToString("c")+", "
						+"New Fee: "+feeTo.Amount.ToString("c")+", "+GetCopyCodeKeyText(feeTo)+". Fee copied from "+procCodeFrom
						+" using the Copy Code tool in the Fee Tools window.",codeNumTo,DateTime.MinValue);
					SecurityLogs.MakeLogEntry(EnumPermType.LogFeeEdit,0,"Fee changed",feeTo.FeeNum,feeToOld.SecDateTEdit);
				}
			}
			feeCodeCopyResult.CountFeeScheds=listFeeSchedNums.Count;
			return feeCodeCopyResult;
		}

		///<summary>The fee's schedule, clinic, provider and effective date as one text, without the code: the same text for the source row and the
		///destination row it is copied to.</summary>
		private static string GetKeyAndDateText(Fee fee) {
			return fee.FeeSched+","+fee.ClinicNum+","+fee.ProvNum+","+fee.DateEffective.ToString("yyyy-MM-dd");
		}

		///<summary>"Fee Schedule: Standard, effective 2027-01-01, at clinic: East, for provider: DOC1" for the audit trail.</summary>
		private static string GetCopyCodeKeyText(Fee fee) {
			string text="Fee Schedule: "+FeeScheds.GetDescription(fee.FeeSched)+GetDateEffectiveLogText(fee.DateEffective);
			if(fee.ClinicNum!=0) {
				text+=", at clinic: "+Clinics.GetAbbr(fee.ClinicNum);
			}
			if(fee.ProvNum!=0) {
				text+=", for provider: "+Providers.GetAbbr(fee.ProvNum);
			}
			return text;
		}
	}

	///<summary>What Fees.CopyFeesToCode changed.</summary>
	[Serializable]
	public class FeeCodeCopyResult {
		///<summary>Fee schedules in which the source code has fees (the ones copied).</summary>
		public int CountFeeScheds;
		public int CountInserted;
		public int CountUpdated;
		public int CountDeleted;
		///<summary>Destination rows that already had the source's amount.</summary>
		public int CountUnchanged;

		///<summary>True when nothing was inserted, updated or deleted.</summary>
		public bool IsNoChange {
			get {
				return CountInserted==0 && CountUpdated==0 && CountDeleted==0;
			}
		}
	}
}
