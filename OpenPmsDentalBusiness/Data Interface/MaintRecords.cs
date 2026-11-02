// Routine maintenance (docs/specs/routine-maintenance.md): recorded completions.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenPmsDentalBusiness {
	///<summary>Recorded completions of maintenance items. A completion is never edited or deleted.</summary>
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
		#endregion Modification Methods
	}
}
