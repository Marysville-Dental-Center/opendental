// Inventory tracking (docs/specs/inventory-tracking.md): completed procedures whose materials nobody has confirmed yet.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Procedures awaiting materials (supplyprocpending), at most one row per procedure. Not cached.</summary>
	public class SupplyProcPendings {
		///<summary>The rows of the procedures that have one.</summary>
		public static List<SupplyProcPending> GetForProcs(List<long> listProcNums) {
			if(listProcNums.IsNullOrEmpty()) {
				return new List<SupplyProcPending>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyProcPending>>(MethodBase.GetCurrentMethod(),listProcNums);
			}
			return Crud.SupplyProcPendingCrud.SelectMany("SELECT * FROM supplyprocpending WHERE ProcNum IN("
				+string.Join(",",listProcNums.Select(x => POut.Long(x)))+") ORDER BY SupplyProcPendingNum");
		}

		///<summary>Every procedure awaiting materials, oldest first, for the Awaiting Materials list. Columns SupplyProcPendingNum, ProcNum, PatNum,
		///DateTimeEntry, UserNum, ProcDate, ToothNum, Surf, ProcCode, Descript, LName, FName.</summary>
		public static DataTable GetTableAll() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod());
			}
			string command="SELECT supplyprocpending.SupplyProcPendingNum,supplyprocpending.ProcNum,supplyprocpending.PatNum,"
				+"supplyprocpending.DateTimeEntry,supplyprocpending.UserNum,procedurelog.ProcDate,procedurelog.ToothNum,procedurelog.Surf,"
				+"procedurecode.ProcCode,procedurecode.Descript,patient.LName,patient.FName "
				+"FROM supplyprocpending "
				+"INNER JOIN procedurelog ON procedurelog.ProcNum=supplyprocpending.ProcNum "
				+"INNER JOIN procedurecode ON procedurecode.CodeNum=procedurelog.CodeNum "
				+"LEFT JOIN patient ON patient.PatNum=supplyprocpending.PatNum "
				+"ORDER BY supplyprocpending.DateTimeEntry,supplyprocpending.SupplyProcPendingNum";
			return Db.GetTable(command);
		}

		///<summary>Records the procedure as awaiting materials, unless it already is. ProcNum is unique in the table, so a second workstation doing the
		///same at the same time gets the duplicate-key error, which is ignored.</summary>
		public static void InsertIfMissing(long procNum,long patNum,long userNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),procNum,patNum,userNum);
				return;
			}
			if(GetForProcs(new List<long> { procNum }).Count>0) {
				return;
			}
			SupplyProcPending supplyProcPending=new SupplyProcPending();
			supplyProcPending.ProcNum=procNum;
			supplyProcPending.PatNum=patNum;
			supplyProcPending.UserNum=userNum;
			try {
				Crud.SupplyProcPendingCrud.Insert(supplyProcPending);
			}
			catch(Exception) {
				if(GetForProcs(new List<long> { procNum }).Count==0) {
					throw;//Not the duplicate key of a concurrent insert.
				}
			}
		}

		///<summary>Removes the procedures from the awaiting list.</summary>
		public static void DeleteForProcs(List<long> listProcNums) {
			if(listProcNums.IsNullOrEmpty()) {
				return;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listProcNums);
				return;
			}
			Db.NonQ("DELETE FROM supplyprocpending WHERE ProcNum IN("+string.Join(",",listProcNums.Select(x => POut.Long(x)))+")");
		}

		///<summary>For the repair: removes the awaiting rows of procedures that are no longer complete, or no longer exist (a failure between a
		///procedure's restore and the removal of its awaiting row). Returns the number removed.</summary>
		public static long DeleteNotComplete() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod());
			}
			return Db.NonQ("DELETE supplyprocpending FROM supplyprocpending LEFT JOIN procedurelog ON procedurelog.ProcNum=supplyprocpending.ProcNum "
				+"WHERE procedurelog.ProcNum IS NULL OR procedurelog.ProcStatus<>"+POut.Int((int)ProcStat.C));
		}
	}
}
