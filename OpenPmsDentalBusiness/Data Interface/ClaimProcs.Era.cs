// M2-ERA, v26_1-account-era-claimproc-link: claimproc.Etrans835AttachNum (Open Dental 26.1) remembers the ERA claim, one CLP loop of one 835
// (an etrans835attach row), that a received or supplemental claimproc was entered from. This file holds the claimproc side: setting the link when
// an ERA import enters payment, and clearing it when the claimproc is un-received or the ERA claim is detached. How the ERA windows read the link
// is in OpenPmsDentalBusiness/X12/EraClaimProcLinks.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	public partial class ClaimProcs {
		///<summary>True for the statuses a claimproc entered from an ERA can have: Received, Supplemental, Preauth (a predetermination answered by an
		///ERA) and CapClaim (a capitation claim, whose claimprocs keep their status). Any other status means the payment was taken back
		///(un-received), so the claimproc no longer comes from the ERA claim.</summary>
		public static bool CanHaveEtrans835AttachNum(ClaimProcStatus claimProcStatus) {
			Meth.NoCheckMiddleTierRole();
			return claimProcStatus.In(ClaimProcStatus.Received,ClaimProcStatus.Supplemental,ClaimProcStatus.Preauth,ClaimProcStatus.CapClaim);
		}

		///<summary>Called by Update before it saves: a claimproc set back to Not Received (or any status an ERA does not post) forgets the ERA claim it
		///was entered from (Open Dental 26.1, claimproc.Etrans835AttachNum). In memory only; Update writes it.</summary>
		public static void ClearEtrans835AttachNumIfNotReceived(ClaimProc claimProc) {
			Meth.NoCheckMiddleTierRole();
			if(claimProc==null || claimProc.Etrans835AttachNum==0 || CanHaveEtrans835AttachNum(claimProc.Status)) {
				return;
			}
			claimProc.Etrans835AttachNum=0;
		}

		///<summary>Called by Etranss.TryImportEraClaimData after it has entered the ERA claim's payment on the claimprocs, for the manual "enter
		///payment" flow of the ERA window and for automatic ERA processing alike. Links the claimprocs it entered (and the By Total line, when there
		///is one) to the ERA claim: the etrans835attach row of this 835, this CLP loop and this claim. In memory only; the caller saves the claimprocs
		///(Etranss.ReceiveEraPayment). Leaves the link alone when there is no attach row, so such claimprocs fall back to the matching of 26.1's
		///predecessors.</summary>
		public static void LinkToEraClaim(X835 x835,Hx835_Claim hx835_Claim,Claim claim,List<ClaimProc> listClaimProcsEntered,ClaimProc claimProcByTotal=null) {
			Meth.NoCheckMiddleTierRole();
			if(x835?.EtransSource==null || hx835_Claim==null || claim==null) {
				return;
			}
			long etrans835AttachNum=Etrans835Attaches.GetNumForEraClaim(x835.EtransSource.EtransNum,hx835_Claim.ClpSegmentIndex,claim.ClaimNum);
			if(etrans835AttachNum==0) {
				return;
			}
			List<ClaimProc> listClaimProcs=new List<ClaimProc>();
			if(listClaimProcsEntered!=null) {
				listClaimProcs.AddRange(listClaimProcsEntered);
			}
			if(claimProcByTotal!=null) {
				listClaimProcs.Add(claimProcByTotal);
			}
			SetEtrans835AttachNum(listClaimProcs,etrans835AttachNum);
		}

		///<summary>Sets Etrans835AttachNum in memory on every claimproc of the list that an ERA can post (see CanHaveEtrans835AttachNum).</summary>
		public static void SetEtrans835AttachNum(List<ClaimProc> listClaimProcs,long etrans835AttachNum) {
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<listClaimProcs.Count;i++) {
				if(listClaimProcs[i]==null || !CanHaveEtrans835AttachNum(listClaimProcs[i].Status)) {
					continue;
				}
				listClaimProcs[i].Etrans835AttachNum=etrans835AttachNum;
			}
		}

		///<summary>The claimprocs entered from any of the given ERA claims (etrans835attach rows).</summary>
		public static List<ClaimProc> GetForEtrans835Attaches(List<long> listEtrans835AttachNums) {
			if(listEtrans835AttachNums.IsNullOrEmpty()) {
				return new List<ClaimProc>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<ClaimProc>>(MethodBase.GetCurrentMethod(),listEtrans835AttachNums);
			}
			string command="SELECT * FROM claimproc WHERE Etrans835AttachNum IN("
				+string.Join(",",listEtrans835AttachNums.Distinct().Select(x => POut.Long(x)))+")";
			return Crud.ClaimProcCrud.SelectMany(command);
		}

		///<summary>The ClaimProcNums of the claim's claimprocs that were entered from the ERA claim with this 835 (EtransNum) and CLP loop
		///(ClpSegmentIndex), attached to this claim. Used to keep the link when the attach row is made again (Etrans835Attaches.CreateForClaim).
		///</summary>
		public static List<long> GetClaimProcNumsForEraClaim(long etransNum,int clpSegmentIndex,long claimNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod(),etransNum,clpSegmentIndex,claimNum);
			}
			if(etransNum==0 || claimNum==0) {
				return new List<long>();
			}
			string command="SELECT claimproc.ClaimProcNum FROM etrans835attach "
				+"INNER JOIN claimproc ON claimproc.Etrans835AttachNum=etrans835attach.Etrans835AttachNum "
					+"AND claimproc.ClaimNum="+POut.Long(claimNum)+" "
				+"WHERE etrans835attach.EtransNum="+POut.Long(etransNum)+" "
				+"AND etrans835attach.ClpSegmentIndex="+POut.Int(clpSegmentIndex)+" "
				+"AND etrans835attach.ClaimNum="+POut.Long(claimNum);
			return Db.GetListLong(command);
		}

		///<summary>Points the given claimprocs at the ERA claim (etrans835attach row). Does nothing for an empty list.</summary>
		public static void UpdateEtrans835AttachNum(List<long> listClaimProcNums,long etrans835AttachNum) {
			if(listClaimProcNums.IsNullOrEmpty()) {
				return;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listClaimProcNums,etrans835AttachNum);
				return;
			}
			string command="UPDATE claimproc SET Etrans835AttachNum="+POut.Long(etrans835AttachNum)+" "
				+"WHERE ClaimProcNum IN("+string.Join(",",listClaimProcNums.Distinct().Select(x => POut.Long(x)))+")";
			Db.NonQ(command);
		}

		///<summary>Clears the link on every claimproc entered from one of the given ERA claims. Called before their etrans835attach rows are deleted
		///(the ERA claim is detached from the claim, attached to another claim, or the ERA or the claim is deleted). The claimprocs keep their status
		///and amounts; only the link goes. Does nothing for an empty list.</summary>
		public static void ClearEtrans835AttachNums(List<long> listEtrans835AttachNums) {
			if(listEtrans835AttachNums.IsNullOrEmpty()) {
				return;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listEtrans835AttachNums);
				return;
			}
			List<long> listNums=listEtrans835AttachNums.Where(x => x!=0).Distinct().ToList();
			if(listNums.Count==0) {
				return;
			}
			string command="UPDATE claimproc SET Etrans835AttachNum=0 "
				+"WHERE Etrans835AttachNum IN("+string.Join(",",listNums.Select(x => POut.Long(x)))+")";
			Db.NonQ(command);
		}
	}
}
