// Database Maintenance check InsVerifyRedundantEntries (Open Dental 26.2 item 30, "Added new database maintenance method to move redundant InsVerify
// entries"; package P262-TOOLS). Written from the public release note and Open Dental's published 26.2 database documents only: the insverify table
// holds "a row for the most recent time an insplan benefit or patplan enrollment was verified", and "there is never more than one row per plan
// because old ones get moved over to InsVerifyHist". Which row stays and how the others are copied are this program's own choices (INFERRED,
// docs/port-notes/P262-TOOLS.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace OpenPmsDentalBusiness {
	public partial class DatabaseMaintenances {
		///<summary>Insurance verification rows (insverify) that are redundant: a second or later row for the same insurance plan (benefits) or the
		///same patient plan (eligibility). The program reads only the most recently verified row (InsVerifies.GetOneByFKey), so the others are
		///history. Fix keeps the row with the latest last-verified date (on a tie, the newest row) and moves each other row into the verification
		///history (insverifyhist): a copy with every field, its security hash unchanged and the kept row's number, so the history of the plan
		///stays in one place. Then it deletes the moved row. Each move gets a DBM log entry naming both rows. Rows of the verify type None are
		///left alone.</summary>
		[DbmMethodAttr(IsReplicationUnsafe=true)]
		public static string InsVerifyRedundantEntries(bool verbose,DbmMode dbmMode) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),verbose,dbmMode);
			}
			string log="";
			switch(dbmMode) {
				case DbmMode.Check:
					long countRedundant=GetInsVerifyRedundantCount();
					if(countRedundant>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Redundant insurance verification entries (more than one for the same plan)")+": "
							+countRedundant+"\r\n";
					}
					break;
				case DbmMode.Fix:
					int countMoved=MoveInsVerifyRedundantEntries();
					if(countMoved>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Redundant insurance verification entries moved to the verification history")+": "
							+countMoved+"\r\n";
					}
					break;
			}
			return log;
		}

		///<summary>The number of insverify rows that are not the one kept for their plan or patient plan: the rows Fix would move.</summary>
		public static long GetInsVerifyRedundantCount() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod());
			}
			string command="SELECT IFNULL(SUM(dup.CountRows-1),0) FROM ("
					+"SELECT COUNT(*) CountRows FROM insverify "
					+"WHERE VerifyType IN("+POut.Int((int)VerifyTypes.InsuranceBenefit)+","+POut.Int((int)VerifyTypes.PatientEnrollment)+") "
					+"GROUP BY VerifyType,FKey "
					+"HAVING COUNT(*)>1"
				+") dup";
			return PIn.Long(Db.GetScalar(command));
		}

		///<summary>Moves every redundant insverify row into insverifyhist (see InsVerifyRedundantEntries) and returns the number of rows moved.</summary>
		public static int MoveInsVerifyRedundantEntries() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetInt(MethodBase.GetCurrentMethod());
			}
			string command="SELECT insverify.* FROM insverify "
				+"INNER JOIN ("
					+"SELECT VerifyType,FKey FROM insverify "
					+"WHERE VerifyType IN("+POut.Int((int)VerifyTypes.InsuranceBenefit)+","+POut.Int((int)VerifyTypes.PatientEnrollment)+") "
					+"GROUP BY VerifyType,FKey "
					+"HAVING COUNT(*)>1"
				+") dup ON dup.VerifyType=insverify.VerifyType AND dup.FKey=insverify.FKey";
			List<InsVerify> listInsVerifies=Crud.InsVerifyCrud.SelectMany(command);
			List<DbmLog> listDbmLogs=new List<DbmLog>();
			int countMoved=0;
			List<IGrouping<string,InsVerify>> listGroups=listInsVerifies.GroupBy(x => (int)x.VerifyType+","+x.FKey).ToList();
			for(int i=0;i<listGroups.Count;i++) {
				List<InsVerify> listOrdered=GetInsVerifiesKeptFirst(listGroups[i].ToList());
				InsVerify insVerifyKept=listOrdered[0];
				for(int j=1;j<listOrdered.Count;j++) {
					InsVerify insVerifyMoved=listOrdered[j];
					InsVerifyHist insVerifyHist=new InsVerifyHist(insVerifyMoved);
					insVerifyHist.InsVerifyNum=insVerifyKept.InsVerifyNum;//the history of the plan stays with the row the program uses
					long insVerifyHistNum=InsVerifyHists.Insert(insVerifyHist);
					//Delete only after the copy is saved, so a failure never loses a row.
					Db.NonQ("DELETE FROM insverify WHERE InsVerifyNum="+POut.Long(insVerifyMoved.InsVerifyNum));
					countMoved++;
					DbmLogFKeyType dbmLogFKeyType=(insVerifyMoved.VerifyType==VerifyTypes.InsuranceBenefit ? DbmLogFKeyType.InsPlan : DbmLogFKeyType.PatPlan);
					listDbmLogs.Add(new DbmLog(Security.CurUser.UserNum,insVerifyMoved.FKey,dbmLogFKeyType,DbmLogActionType.Delete,
						nameof(InsVerifyRedundantEntries),"Moved redundant insverify row "+insVerifyMoved.InsVerifyNum+" ("+insVerifyMoved.VerifyType
						+", last verified "+insVerifyMoved.DateLastVerified.ToString("yyyy-MM-dd")+", status DefNum "+insVerifyMoved.DefNum+", assigned UserNum "
						+insVerifyMoved.UserNum+") to insverifyhist row "+insVerifyHistNum+". Kept insverify row "+insVerifyKept.InsVerifyNum
						+" (last verified "+insVerifyKept.DateLastVerified.ToString("yyyy-MM-dd")+")."));
				}
			}
			if(listDbmLogs.Count>0) {
				DbmLogs.InsertMany(listDbmLogs);
			}
			return countMoved;
		}

		///<summary>The rows of one plan or patient plan, the row to keep first: the latest DateLastVerified, then the newest row (highest
		///InsVerifyNum).</summary>
		public static List<InsVerify> GetInsVerifiesKeptFirst(List<InsVerify> listInsVerifies) {
			Meth.NoCheckMiddleTierRole();
			return listInsVerifies.OrderByDescending(x => x.DateLastVerified.Date).ThenByDescending(x => x.InsVerifyNum).ToList();
		}
	}
}
