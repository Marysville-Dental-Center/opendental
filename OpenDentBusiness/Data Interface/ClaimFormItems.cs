using System;
using System.Collections;
using System.Data;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness{
	///<summary></summary>
	public class ClaimFormItems {
		#region Cache Pattern

		private class ClaimFormItemCache : CacheListAbs<ClaimFormItem> {
			protected override List<ClaimFormItem> GetCacheFromDb() {
				string command="SELECT * FROM claimformitem ORDER BY ImageFileName DESC";
				return Crud.ClaimFormItemCrud.SelectMany(command);
			}
			protected override List<ClaimFormItem> TableToList(DataTable table) {
				return Crud.ClaimFormItemCrud.TableToList(table);
			}
			protected override ClaimFormItem Copy(ClaimFormItem claimFormItem) {
				return claimFormItem.Copy();
			}
			protected override DataTable ListToTable(List<ClaimFormItem> listClaimFormItems) {
				return Crud.ClaimFormItemCrud.ListToTable(listClaimFormItems,"ClaimFormItem");
			}
			protected override void FillCacheIfNeeded() {
				ClaimFormItems.GetTableFromCache(false);
			}
		}
		
		///<summary>The object that accesses the cache in a thread-safe manner.</summary>
		private static ClaimFormItemCache _claimFormItemCache=new ClaimFormItemCache();

		public static List<ClaimFormItem> GetWhere(Predicate<ClaimFormItem> match,bool isShort=false) {
			return _claimFormItemCache.GetWhere(match,isShort);
		}

		///<summary>Image names of the ADA and DC-217 claim-form backgrounds that Open Dental drew from its proprietary library. That artwork is
		///copyrighted and isn't shipped, so items that name one of these draw nothing; those claims print on pre-printed forms. The items stay in
		///the claim form so existing claim forms load unchanged.</summary>
		private static readonly string[] _arrayUnshippedBackgrounds=new string[] {
			"ADA2006.gif","ADA2012.gif","ADA2012_J430D.gif","ADA2018_J432.gif","ADA2019_J430.gif","ADA2024_J430.gif","DC-217.gif"
		};

		///<summary>True if imageFileName names an ADA or DC-217 claim-form background, which isn't shipped and is never drawn.</summary>
		public static bool IsUnshippedBackground(string imageFileName) {
			Meth.NoCheckMiddleTierRole();
			return _arrayUnshippedBackgrounds.Contains(imageFileName);
		}

		///<summary>Refreshes the cache and returns it as a DataTable. This will refresh the ClientWeb's cache and the ServerWeb's cache.</summary>
		public static DataTable RefreshCache() {
			return GetTableFromCache(true);
		}

		///<summary>Fills the local cache with the passed in DataTable.</summary>
		public static void FillCacheFromTable(DataTable table) {
			_claimFormItemCache.FillCacheFromTable(table);
		}

		///<summary>Always refreshes the ClientWeb's cache.</summary>
		public static DataTable GetTableFromCache(bool doRefreshCache) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				DataTable table=Meth.GetTable(MethodBase.GetCurrentMethod(),doRefreshCache);
				_claimFormItemCache.FillCacheFromTable(table);
				return table;
			}
			return _claimFormItemCache.GetTableFromCache(doRefreshCache);
		}

		public static void ClearCache() {
			_claimFormItemCache.ClearCache();
		}
		#endregion Cache Pattern

		///<summary></summary>
		public static long Insert(ClaimFormItem claimFormItem) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				claimFormItem.ClaimFormItemNum=Meth.GetLong(MethodBase.GetCurrentMethod(),claimFormItem);
				return claimFormItem.ClaimFormItemNum;
			}
			return Crud.ClaimFormItemCrud.Insert(claimFormItem);
		}

		///<summary></summary>
		public static void Update(ClaimFormItem claimFormItem){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),claimFormItem);
				return;
			}
			Crud.ClaimFormItemCrud.Update(claimFormItem);
		}

		///<summary></summary>
		public static void Delete(ClaimFormItem claimFormItem){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),claimFormItem);
				return;
			}
			string command = "DELETE FROM claimformitem "
				+"WHERE ClaimFormItemNum = '"+POut.Long(claimFormItem.ClaimFormItemNum)+"'";
 			Db.NonQ(command);
		}

		///<summary></summary>
		public static void DeleteAllForClaimForm(long claimFormNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),claimFormNum);
				return;
			}
			string command="DELETE FROM claimformitem WHERE ClaimFormNum = '"+POut.Long(claimFormNum)+"'";
			Db.NonQ(command);
		}

		///<summary>Gets all claimformitems for the specified claimform from the preloaded List.</summary>
		public static List<ClaimFormItem> GetListForForm(long claimFormNum) {
			Meth.NoCheckMiddleTierRole();
			return ClaimFormItems.GetWhere(x => x.ClaimFormNum==claimFormNum);
		}
	}

	

	

}









