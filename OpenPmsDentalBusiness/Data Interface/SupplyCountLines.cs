// Inventory tracking (docs/specs/inventory-tracking.md, 2.7): the counted lines of the stock counts, each saved as soon as it is counted.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Counted lines of stock counts (supplycountline). Not cached. A line is written only while its count is in the state the statement
	///names (Open, or Posting for the re-read after a failed post), checked inside the same statement, which MyISAM runs under its locks of
	///supplycount and supplycountline: a line can never slip into a count another workstation is posting or has closed. InventoryTracking runs the
	///counts; call these through it.</summary>
	public class SupplyCountLines {
		#region Get Methods
		///<summary>The lines of the count in the order they were first counted, as saved (LotNumber and DateExpiry blank for existing lots;
		///InventoryTracking.GetSavedCountLines fills them in).</summary>
		public static List<SupplyCountLine> GetForCount(long supplyCountNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<SupplyCountLine>>(MethodBase.GetCurrentMethod(),supplyCountNum);
			}
			return Crud.SupplyCountLineCrud.SelectMany("SELECT * FROM supplycountline WHERE SupplyCountNum="+POut.Long(supplyCountNum)
				+" ORDER BY SupplyCountLineNum");
		}

		///<summary>The number of lines of the count.</summary>
		public static long GetCountForCount(long supplyCountNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),supplyCountNum);
			}
			return PIn.Long(Db.GetCount("SELECT COUNT(*) FROM supplycountline WHERE SupplyCountNum="+POut.Long(supplyCountNum)));
		}

		///<summary>The line, or null.</summary>
		public static SupplyCountLine GetOne(long supplyCountLineNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyCountLine>(MethodBase.GetCurrentMethod(),supplyCountLineNum);
			}
			return Crud.SupplyCountLineCrud.SelectOne(supplyCountLineNum);
		}

		///<summary>The count's line of the supply and lot (lotNumber for a lot found in the count, otherwise blank), or null. Lot numbers compare as
		///the unique key does (the table's case-insensitive collation).</summary>
		public static SupplyCountLine GetByKey(long supplyCountNum,long supplyNum,long supplyLotNum,string lotNumber) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<SupplyCountLine>(MethodBase.GetCurrentMethod(),supplyCountNum,supplyNum,supplyLotNum,lotNumber);
			}
			return Crud.SupplyCountLineCrud.SelectOne("SELECT * FROM supplycountline WHERE SupplyCountNum="+POut.Long(supplyCountNum)
				+" AND SupplyNum="+POut.Long(supplyNum)+" AND SupplyLotNum="+POut.Long(supplyLotNum)+" AND LotNumber='"+POut.String(lotNumber??"")+"' "
				+"ORDER BY SupplyCountLineNum LIMIT 1");
		}
		#endregion Get Methods

		#region Insert/Update/Delete
		///<summary>Saves the line into its count, only while the count is Open: one INSERT ... SELECT ... FROM supplycount WHERE the count is Open
		///... ON DUPLICATE KEY UPDATE statement. A supply and lot the count has a line of already (the unique key SupplyCountNum, SupplyNum,
		///SupplyLotNum, LotNumber) gets its line replaced: the new snapshot QtyProgram, the user, the time, the expiry date of a lot found in the
		///count, and QtyCounted, which isAdded adds to the line's counted quantity inside the statement (a scan) instead of replacing it. Two
		///workstations saving the same line at the same moment leave one line, with both scans. Returns the rows the statement reports: 0 when the
		///count is not Open (or, with a connection that reports changed rows, when nothing changed).</summary>
		public static long Upsert(SupplyCountLine supplyCountLine,bool isAdded) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),supplyCountLine,isAdded);
			}
			string lotNumber=supplyCountLine.LotNumber??"";
			if(lotNumber.Length>255) {
				lotNumber=lotNumber.Substring(0,255);
			}
			//With random primary keys (replication) the key is taken from ReplicationServers.GetKey, as the generated CRUD does; on a duplicate of
			//the unique key it is not used.
			string command="INSERT INTO supplycountline ("+(PrefC.RandomKeys ? "SupplyCountLineNum," : "")
				+"SupplyCountNum,SupplyNum,SupplyLotNum,LotNumber,DateExpiry,QtyProgram,QtyCounted,UserNum,DateTimeEntry) "
				+"SELECT "+(PrefC.RandomKeys ? POut.Long(ReplicationServers.GetKey("supplycountline","SupplyCountLineNum"))+"," : "")
				+"supplycount.SupplyCountNum,"+POut.Long(supplyCountLine.SupplyNum)+","+POut.Long(supplyCountLine.SupplyLotNum)+",'"+POut.String(lotNumber)+"',"
				+POut.Date(supplyCountLine.DateExpiry)+","+POut.Double(supplyCountLine.QtyProgram)+","+POut.Double(supplyCountLine.QtyCounted)+","
				+POut.Long(supplyCountLine.UserNum)+","+DbHelper.Now()+" "
				+"FROM supplycount WHERE supplycount.SupplyCountNum="+POut.Long(supplyCountLine.SupplyCountNum)+" "
				+"AND supplycount.CountStatus="+POut.Int((int)SupplyCountStatus.Open)+" "
				+"ON DUPLICATE KEY UPDATE supplycountline.DateExpiry=VALUES(DateExpiry),supplycountline.QtyProgram=VALUES(QtyProgram),"
				+"supplycountline.QtyCounted="+(isAdded ? "ROUND(supplycountline.QtyCounted+VALUES(QtyCounted),2)" : "VALUES(QtyCounted)")+","
				+"supplycountline.UserNum=VALUES(UserNum),supplycountline.DateTimeEntry=VALUES(DateTimeEntry)";
			return Db.NonQ(command);
		}

		///<summary>A lot found in the count (SupplyLotNum 0, lotNumber) that is now the supply's lot supplyLotNum (it was received meanwhile, or the
		///failed post created it): its line becomes the lot's line, unless the lot has a line of its own already, which then counts it and the found
		///lot's line is removed (UPDATE IGNORE, then DELETE). Only while the count has the status statusCount. lotNumber must not be blank.</summary>
		public static void MoveLotFound(long supplyCountNum,long supplyNum,string lotNumber,long supplyLotNum,SupplyCountStatus statusCount) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyCountNum,supplyNum,lotNumber,supplyLotNum,statusCount);
				return;
			}
			if(string.IsNullOrWhiteSpace(lotNumber) || supplyLotNum<=0) {
				throw new ArgumentException("A lot found in the count needs its lot number and the lot it became.");
			}
			string where="WHERE SupplyCountNum="+POut.Long(supplyCountNum)+" AND SupplyNum="+POut.Long(supplyNum)+" AND SupplyLotNum=0 "
				+"AND LotNumber='"+POut.String(lotNumber.Trim())+"' AND SupplyCountNum IN(SELECT supplycount.SupplyCountNum FROM supplycount "
				+"WHERE supplycount.CountStatus="+POut.Int((int)statusCount)+")";
			Db.NonQ("UPDATE IGNORE supplycountline SET SupplyLotNum="+POut.Long(supplyLotNum)+",LotNumber='',DateExpiry="+POut.Date(DateTime.MinValue)+" "+where);
			Db.NonQ("DELETE FROM supplycountline "+where);
		}

		///<summary>Sets the line's snapshot QtyProgram, only while its count is Posting: the re-read from the ledger after a post that failed.
		///</summary>
		public static void UpdateQtyProgramWhilePosting(long supplyCountLineNum,double qtyProgram) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),supplyCountLineNum,qtyProgram);
				return;
			}
			Db.NonQ("UPDATE supplycountline SET QtyProgram="+POut.Double(qtyProgram)+" WHERE SupplyCountLineNum="+POut.Long(supplyCountLineNum)+" "
				+"AND SupplyCountNum IN(SELECT supplycount.SupplyCountNum FROM supplycount WHERE supplycount.CountStatus="
				+POut.Int((int)SupplyCountStatus.Posting)+")");
		}

		///<summary>Deletes the line, only while its count is Open. Returns the number of lines deleted: 0 when the line is gone already or its count is
		///not Open.</summary>
		public static long DeleteWhileOpen(long supplyCountLineNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),supplyCountLineNum);
			}
			return Db.NonQ("DELETE FROM supplycountline WHERE SupplyCountLineNum="+POut.Long(supplyCountLineNum)+" "
				+"AND SupplyCountNum IN(SELECT supplycount.SupplyCountNum FROM supplycount WHERE supplycount.CountStatus="
				+POut.Int((int)SupplyCountStatus.Open)+")");
		}
		#endregion Insert/Update/Delete
	}
}
