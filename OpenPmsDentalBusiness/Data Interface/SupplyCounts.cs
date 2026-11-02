// Inventory tracking (docs/specs/inventory-tracking.md, 2.7): stock counts saved while they are counted, at most one open at a time.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Stock counts (supplycount). Not cached: every workstation reads the open count from the database, so a count started elsewhere is
	///seen at once. Every change of state is one statement whose WHERE names the state it leaves, which MyISAM runs under one lock of supplycount:
	///that keeps "at most one open count" and "posted once" without transactions. InventoryTracking runs the counts; call these through it.
	///</summary>
	public class SupplyCounts {
		#region Get Methods
		///<summary>The office's open count (Open or Posting), or null when none is open. The oldest one, should there ever be two.</summary>
		public static SupplyCount GetOpen() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyCount>(MethodBase.GetCurrentMethod());
			}
			return Crud.SupplyCountCrud.SelectOne("SELECT * FROM supplycount WHERE CountStatus IN("+POut.Int((int)SupplyCountStatus.Open)+","
				+POut.Int((int)SupplyCountStatus.Posting)+") ORDER BY SupplyCountNum LIMIT 1");
		}

		///<summary>The count, or null.</summary>
		public static SupplyCount GetOne(long supplyCountNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyCount>(MethodBase.GetCurrentMethod(),supplyCountNum);
			}
			return Crud.SupplyCountCrud.SelectOne(supplyCountNum);
		}

		///<summary>True when the count is Posting and its post began more than minutes ago, by the database server's clock (the post's own
		///DateTimeClosed is server time too).</summary>
		public static bool IsPostingOlderThan(long supplyCountNum,int minutes) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),supplyCountNum,minutes);
			}
			return PIn.Long(Db.GetCount("SELECT COUNT(*) FROM supplycount WHERE SupplyCountNum="+POut.Long(supplyCountNum)+" "
				+"AND CountStatus="+POut.Int((int)SupplyCountStatus.Posting)+" "
				+"AND DateTimeClosed<DATE_SUB("+DbHelper.Now()+",INTERVAL "+POut.Int(minutes)+" MINUTE)"))>0;
		}
		#endregion Get Methods

		#region Insert/Update
		///<summary>Starts a count for the user unless one is open (Open or Posting), with one INSERT ... SELECT ... WHERE NOT EXISTS statement, which
		///MyISAM runs under one lock of supplycount: of two workstations starting at the same moment only one adds a count, and both then work in it.
		///Returns the open count, or null when the count that is open is Posting (its post runs, or did not finish), which takes no line.</summary>
		public static SupplyCount InsertOpenIfNone(long userNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyCount>(MethodBase.GetCurrentMethod(),userNum);
			}
			//With random primary keys (replication) the key is taken from ReplicationServers.GetKey, as the generated CRUD does, never from
			//AUTO_INCREMENT: two replicated servers would hand out the same next key.
			Db.NonQ("INSERT INTO supplycount ("+(PrefC.RandomKeys ? "SupplyCountNum," : "")
				+"CountStatus,UserNum,DateTimeEntry,UserNumClosed,DateTimeClosed,Reason) "
				+"SELECT "+(PrefC.RandomKeys ? POut.Long(ReplicationServers.GetKey("supplycount","SupplyCountNum"))+"," : "")
				+POut.Int((int)SupplyCountStatus.Open)+","+POut.Long(userNum)+","+DbHelper.Now()+",0,"+POut.DateT(DateTime.MinValue)+",'' "
				+"FROM DUAL WHERE NOT EXISTS(SELECT 1 FROM supplycount WHERE CountStatus IN("+POut.Int((int)SupplyCountStatus.Open)+","
				+POut.Int((int)SupplyCountStatus.Posting)+"))");
			SupplyCount supplyCount=GetOpen();
			if(supplyCount==null || supplyCount.CountStatus!=SupplyCountStatus.Open) {
				return null;
			}
			return supplyCount;
		}

		///<summary>Moves the count from statusFrom to statusTo, as the user, now (server time), with the reason, in one UPDATE ... WHERE
		///CountStatus=statusFrom: of two workstations doing it at the same moment only one moves it. The WHERE makes the matched row the changed
		///row, so the result is right whether the connection reports found or affected rows. Returns true when this call moved it.</summary>
		public static bool SetStatus(long supplyCountNum,SupplyCountStatus statusFrom,SupplyCountStatus statusTo,long userNum,string reason) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),supplyCountNum,statusFrom,statusTo,userNum,reason);
			}
			if(statusFrom==statusTo) {
				throw new ArgumentException("A count's status must change.");
			}
			reason=(reason??"").Trim();
			if(reason.Length>255) {
				reason=reason.Substring(0,255);
			}
			string command="UPDATE supplycount SET CountStatus="+POut.Int((int)statusTo)+",";
			if(statusTo==SupplyCountStatus.Open) {
				//Opened again after a post that failed: no longer closed.
				command+="UserNumClosed=0,DateTimeClosed="+POut.DateT(DateTime.MinValue)+",Reason='' ";
			}
			else {
				command+="UserNumClosed="+POut.Long(userNum)+",DateTimeClosed="+DbHelper.Now()+",Reason='"+POut.String(reason)+"' ";
			}
			command+="WHERE SupplyCountNum="+POut.Long(supplyCountNum)+" AND CountStatus="+POut.Int((int)statusFrom);
			return Db.NonQ(command)>0;
		}

		///<summary>Moves a Posting count to Posted at the end of its post, keeping who began the post, when and the reason. Returns true when it was
		///Posting.</summary>
		public static bool SetPosted(long supplyCountNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),supplyCountNum);
			}
			return Db.NonQ("UPDATE supplycount SET CountStatus="+POut.Int((int)SupplyCountStatus.Posted)+" WHERE SupplyCountNum="+POut.Long(supplyCountNum)
				+" AND CountStatus="+POut.Int((int)SupplyCountStatus.Posting))>0;
		}
		#endregion Insert/Update
	}
}
