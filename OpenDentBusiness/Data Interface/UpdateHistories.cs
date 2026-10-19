using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using CodeBase;

namespace OpenDentBusiness {
	///<summary></summary>
	public class UpdateHistories{
		///<summary>Gets one UpdateHistory from the db.</summary>
		public static UpdateHistory GetOne(long updateNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				return Meth.GetObject<UpdateHistory>(MethodBase.GetCurrentMethod(),updateNum);
			}
			return Crud.UpdateHistoryCrud.SelectOne(updateNum);
		}

		///<summary></summary>
		public static long Insert(UpdateHistory updateHistory){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				updateHistory.UpdateHistoryNum=Meth.GetLong(MethodBase.GetCurrentMethod(),updateHistory);
				return updateHistory.UpdateHistoryNum;
			}
			return Crud.UpdateHistoryCrud.Insert(updateHistory);
		}

		///<summary></summary>
		public static void Update(UpdateHistory updateHistory){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				Meth.GetVoid(MethodBase.GetCurrentMethod(),updateHistory);
				return;
			}
			Crud.UpdateHistoryCrud.Update(updateHistory);
		}

		///<summary></summary>
		public static void Delete(long updateNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),updateNum);
				return;
			}
			Crud.UpdateHistoryCrud.Delete(updateNum);
		}

		///<summary>All updatehistory entries ordered by DateTimeUpdated.</summary>
		public static List<UpdateHistory> GetAll() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<UpdateHistory>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT * FROM updatehistory ORDER BY DateTimeUpdated";
			return Crud.UpdateHistoryCrud.SelectMany(command);
		}

		///<summary>Get the most recently inserted updatehistory entry. Ordered by DateTimeUpdated.</summary>
		public static UpdateHistory GetLastUpdateHistory() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<UpdateHistory>(MethodBase.GetCurrentMethod());
			}
			string command=@"SELECT * 
				FROM updatehistory
				ORDER BY DateTimeUpdated DESC
				LIMIT 1";
			return Crud.UpdateHistoryCrud.SelectOne(command);
		}

		///<summary>Gets the most recently inserted updatehistory entries. Ordered by DateTimeUpdated.</summary>
		public static List<UpdateHistory> GetPreviousUpdateHistories(int count) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<UpdateHistory>>(MethodBase.GetCurrentMethod(),count);
			}
			string command=@"SELECT * 
				FROM updatehistory
				ORDER BY DateTimeUpdated DESC
				LIMIT "+POut.Int(count);
			return Crud.UpdateHistoryCrud.SelectMany(command);
		}

		///<summary>Returns the latest version information.</summary>
		public static UpdateHistory GetForVersion(string strVersion) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<UpdateHistory>(MethodBase.GetCurrentMethod(),strVersion);
			}
			string command="SELECT * FROM updatehistory WHERE ProgramVersion='"+POut.String(strVersion.ToString())+"'";
			return Crud.UpdateHistoryCrud.SelectOne(command);
		}

		///<summary>Returns the earliest datetime that a version was reached. If that version has not been reached, returns the MinDate.</summary>
		public static DateTime GetDateForVersion(Version version) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				//This line is necessary because Version stores unspecified fields as -1. When Newtonsoft.Json tries to serialize it, it fails because none 
				//of the arguments to the constructor are allowed to be negative.
				version=new Version(Math.Max(version.Major,0),Math.Max(version.Minor,0),Math.Max(version.Build,0),Math.Max(version.Revision,0));
				return Meth.GetObject<DateTime>(MethodBase.GetCurrentMethod(),version);
			}
			List<UpdateHistory> listUpdateHistories=UpdateHistories.GetAll();
			for(int i=0;i<listUpdateHistories.Count;i++) {
				Version versionCompare=new Version();
				ODException.SwallowAnyException(() => { versionCompare=new Version(listUpdateHistories[i].ProgramVersion); });//Just in case.
				if(versionCompare>=version) {
					return listUpdateHistories[i].DateTimeUpdated;
				}
			}
			//The earliest version was later than the version passed in.
			return new DateTime(1,1,1);
		}
		///<summary>Get the most recently signed updatehistory entry. Ordered by DateTimeUpdated.</summary>
		public static UpdateHistory GetLastSignedUpdateHistory() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<UpdateHistory>(MethodBase.GetCurrentMethod());
			}
			string command=@"SELECT * 
				FROM updatehistory
				WHERE Signature!=''
				ORDER BY DateTimeUpdated DESC
				LIMIT 1";
			return Crud.UpdateHistoryCrud.SelectOne(command);
		}

		///<summary>Determines if the License Agreement needs to be "signed". Returns true if no entries contain a signature string, otherwise false.</summary>
		public static bool IsLicenseAgreementNeeded() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod());
			}
			string command=@"SELECT COUNT(*) 
				FROM updatehistory
				WHERE Signature!=''";
			return Db.GetCount(command)=="0";
		}

		//SendSignatureToHQ() was removed. License agreement signatures are no longer collected or sent to Open Dental HQ.
	}
}