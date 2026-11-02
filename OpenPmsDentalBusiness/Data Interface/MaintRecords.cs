// Routine maintenance (docs/specs/routine-maintenance.md): recorded completions.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenPmsDentalBusiness {
	///<summary>Recorded completions of maintenance items. A completion is never deleted; its checklist ticks, results and note can be corrected
	///(Correct) by the user who recorded it or a user with MaintenanceSetup.</summary>
	public class MaintRecords {
		#region Get Methods
		///<summary>Every completion of the given items (all items when the list is null), newest first.</summary>
		public static List<MaintRecord> GetForItems(List<long> listMaintItemNums=null) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<MaintRecord>>(MethodBase.GetCurrentMethod(),listMaintItemNums);
			}
			string command="SELECT * FROM maintrecord ";
			if(listMaintItemNums!=null) {
				if(listMaintItemNums.Count==0) {
					return new List<MaintRecord>();
				}
				command+="WHERE MaintItemNum IN("+string.Join(",",listMaintItemNums.Select(x => POut.Long(x)))+") ";
			}
			command+="ORDER BY DateDone DESC,MaintRecordNum DESC";
			return Crud.MaintRecordCrud.SelectMany(command);
		}

		///<summary>The completions done from dateFrom to dateTo (dates done, inclusive), of one item or of every item when maintItemNum is 0, newest
		///first.</summary>
		public static List<MaintRecord> GetForDateRange(DateTime dateFrom,DateTime dateTo,long maintItemNum=0) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<MaintRecord>>(MethodBase.GetCurrentMethod(),dateFrom,dateTo,maintItemNum);
			}
			string command="SELECT * FROM maintrecord WHERE DateDone BETWEEN "+POut.Date(dateFrom)+" AND "+POut.Date(dateTo)+" ";
			if(maintItemNum!=0) {
				command+="AND MaintItemNum="+POut.Long(maintItemNum)+" ";
			}
			command+="ORDER BY DateDone DESC,MaintRecordNum DESC";
			return Crud.MaintRecordCrud.SelectMany(command);
		}

		///<summary>Null if not found.</summary>
		public static MaintRecord GetOne(long maintRecordNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<MaintRecord>(MethodBase.GetCurrentMethod(),maintRecordNum);
			}
			return Crud.MaintRecordCrud.SelectOne(maintRecordNum);
		}
		#endregion Get Methods

		#region Modification Methods
		///<summary>Records a completion with its checklist ticks and results. The user who did and recorded it is the logged-on user (UserNum is set
		///here, never by the caller) and the time is the server's. IsFlagged is set when any value is flagged. Returns the new MaintRecordNum.</summary>
		public static long Insert(MaintRecord maintRecord,List<MaintRecordValue> listMaintRecordValues) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				maintRecord.MaintRecordNum=Meth.GetLong(MethodBase.GetCurrentMethod(),maintRecord,listMaintRecordValues);
				return maintRecord.MaintRecordNum;
			}
			//Security.CurUser.UserNum gets set on MT by the DtoProcessor so it matches the user from the client WS.
			maintRecord.UserNum=Security.CurUser?.UserNum??0;
			listMaintRecordValues=listMaintRecordValues??new List<MaintRecordValue>();
			maintRecord.IsFlagged=listMaintRecordValues.Any(x => x.IsFlagged);
			Crud.MaintRecordCrud.Insert(maintRecord);
			for(int i=0;i<listMaintRecordValues.Count;i++) {
				listMaintRecordValues[i].MaintRecordNum=maintRecord.MaintRecordNum;
				listMaintRecordValues[i].ItemOrder=i;
				Crud.MaintRecordValueCrud.Insert(listMaintRecordValues[i]);
			}
			return maintRecord.MaintRecordNum;
		}

		///<summary>Corrects a recorded completion: its note and the values of its checklist and result lines. listMaintRecordValues holds lines of the
		///completion (by MaintRecordValueNum) with their corrected ValueText; a line that is not in it keeps its value, and no line is added or
		///removed (MaintCorrections.GetPlan). The flags are worked out here from the item's current fields, IsFlagged of the completion from its
		///lines, UserNumCorrected is the logged-on user and DateTCorrected the server's time; the item, the dates and UserNum never change. Writes
		///the audit trail entry (MaintAudit.GetTextCorrected, under MaintenanceRecord, FKey the MaintItemNum) and returns its text, or "" when nothing
		///changed (then nothing is saved or logged). Throws an ApplicationException, saving nothing, when the completion does not exist, the
		///logged-on user may not correct it (CanCorrect; checked here, so the rule holds through the middle tier), or a line or value is not valid.
		///</summary>
		public static string Correct(long maintRecordNum,string note,List<MaintRecordValue> listMaintRecordValues) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),maintRecordNum,note,listMaintRecordValues);
			}
			MaintRecord maintRecordOld=GetOne(maintRecordNum);
			if(maintRecordOld==null) {
				throw new ApplicationException(Lans.g("MaintRecords","This completion was not found. It may have been removed from the database."));
			}
			//Security.CurUser.UserNum gets set on MT by the DtoProcessor so it matches the user from the client WS.
			if(!CanCorrect(Security.CurUser,maintRecordOld)) {
				throw new ApplicationException(Lans.g("MaintRecords","Only the user who recorded this completion or a user with Maintenance Setup can correct it."));
			}
			List<MaintRecordValue> listValuesOld=MaintRecordValues.GetForRecord(maintRecordNum);
			List<MaintField> listMaintFields=MaintFields.GetForItem(maintRecordOld.MaintItemNum);
			MaintCorrections.CorrectionPlan correctionPlan=MaintCorrections.GetPlan(maintRecordOld,listValuesOld,note,listMaintRecordValues,listMaintFields);
			if(correctionPlan.Error!=null) {
				throw new ApplicationException(correctionPlan.Error);
			}
			if(!correctionPlan.IsChanging) {
				return "";
			}
			for(int i=0;i<correctionPlan.ListValuesChanged.Count;i++) {
				Crud.MaintRecordValueCrud.Update(correctionPlan.ListValuesChanged[i],correctionPlan.ListValuesOld[i]);
			}
			MaintRecord maintRecord=correctionPlan.MaintRecord;
			maintRecord.UserNumCorrected=Security.CurUser.UserNum;
			maintRecord.DateTCorrected=MiscData.GetNowDateTime();
			Crud.MaintRecordCrud.Update(maintRecord,maintRecordOld);
			MaintItem maintItem=MaintItems.GetOne(maintRecordOld.MaintItemNum);
			string logText=MaintAudit.GetTextCorrected(maintItem?.ItemName??"",maintRecordOld,correctionPlan);
			SecurityLogs.MakeLogEntry(EnumPermType.MaintenanceRecord,0,logText,maintRecordOld.MaintItemNum,DateTime.MinValue);
			return logText;
		}
		#endregion Modification Methods

		#region Misc Methods
		///<summary>True when the user may correct the completion: the user who recorded it, or a user with MaintenanceSetup (from the permission
		///cache). False without a user.</summary>
		public static bool CanCorrect(Userod userod,MaintRecord maintRecord) {
			Meth.NoCheckMiddleTierRole();
			if(userod==null || maintRecord==null) {
				return false;
			}
			return MaintCorrections.CanCorrect(userod,maintRecord,GroupPermissions.HasPermission(userod,EnumPermType.MaintenanceSetup,0));
		}
		#endregion Misc Methods
	}
}
