using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace OpenPmsDentalBusiness{
	///<summary></summary>
	public class ClaimForms {
		#region Cache Pattern

		private class ClaimFormCache : CacheListAbs<ClaimForm> {
			protected override List<ClaimForm> GetCacheFromDb() {
				string command="SELECT * FROM claimform";
				List<ClaimForm> listClaimForms=Crud.ClaimFormCrud.SelectMany(command);
				foreach(ClaimForm cf in listClaimForms) {
					cf.Items=ClaimFormItems.GetListForForm(cf.ClaimFormNum);
				}
				return listClaimForms;
			}
			protected override List<ClaimForm> TableToList(DataTable table) {
				List<ClaimForm> listClaimForms=Crud.ClaimFormCrud.TableToList(table);
				foreach(ClaimForm cf in listClaimForms) {
					cf.Items=ClaimFormItems.GetListForForm(cf.ClaimFormNum);
				}
				return listClaimForms;
			}
			protected override ClaimForm Copy(ClaimForm claimForm) {
				return claimForm.Copy();
			}
			protected override DataTable ListToTable(List<ClaimForm> listClaimForms) {
				return Crud.ClaimFormCrud.ListToTable(listClaimForms,"ClaimForm");
			}
			protected override void FillCacheIfNeeded() {
				ClaimForms.GetTableFromCache(false);
			}
			protected override bool IsInListShort(ClaimForm claimForm) {
				return !claimForm.IsHidden;
			}
		}
		
		///<summary>The object that accesses the cache in a thread-safe manner.</summary>
		private static ClaimFormCache _claimFormCache=new ClaimFormCache();

		public static List<ClaimForm> GetDeepCopy(bool isShort=false) {
			return _claimFormCache.GetDeepCopy(isShort);
		}

		public static ClaimForm GetFirstOrDefault(Func<ClaimForm,bool> match,bool isShort=false) {
			return _claimFormCache.GetFirstOrDefault(match,isShort);
		}

		///<summary>Refreshes the cache and returns it as a DataTable. This will refresh the ClientWeb's cache and the ServerWeb's cache.</summary>
		public static DataTable RefreshCache() {
			return GetTableFromCache(true);
		}

		///<summary>Fills the local cache with the passed in DataTable.</summary>
		public static void FillCacheFromTable(DataTable table) {
			_claimFormCache.FillCacheFromTable(table);
		}

		///<summary>Always refreshes the ClientWeb's cache.</summary>
		public static DataTable GetTableFromCache(bool doRefreshCache) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				DataTable table=Meth.GetTable(MethodBase.GetCurrentMethod(),doRefreshCache);
				_claimFormCache.FillCacheFromTable(table);
				return table;
			}
			return _claimFormCache.GetTableFromCache(doRefreshCache);
		}

		public static void ClearCache() {
			_claimFormCache.ClearCache();
		}
		#endregion Cache Pattern

		///<summary>Inserts this claimform into database and retrieves the new primary key.
		///Assigns all claimformitems to the claimform and inserts them if the bool is true.</summary>
		public static long Insert(ClaimForm claimForm,bool includeClaimFormItems) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				claimForm.ClaimFormNum=Meth.GetLong(MethodBase.GetCurrentMethod(),claimForm,includeClaimFormItems);
				return claimForm.ClaimFormNum;
			}
			long claimFormNum=Crud.ClaimFormCrud.Insert(claimForm);
			if(includeClaimFormItems) {
				for(int i=0;i<claimForm.Items.Count;i++){
					claimForm.Items[i].ClaimFormNum=claimForm.ClaimFormNum;//so even though the ClaimFormNum is wrong, this line fixes it.
					ClaimFormItems.Insert(claimForm.Items[i]);
				}
			}
			return claimFormNum;
		}

		///<summary>Can be called externally as part of the conversion sequence.  Surround with try catch.
		///Returns the claimform object from the xml file or xml data passed in that can then be inserted if needed.
		///If xmlData is provided then path will be ignored.  If xmlData is not provided, a valid path is required.</summary>
		public static ClaimForm DeserializeClaimForm(string path,string xmlData) {
			Meth.NoCheckMiddleTierRole();
			ClaimForm claimForm = new ClaimForm();
			XmlSerializer xmlSerializer = new XmlSerializer(typeof(ClaimForm));
			if(xmlData=="") {//use path
				if(!File.Exists(path)) {
					throw new ApplicationException(Lans.g("FormClaimForm","File does not exist."));
				}
				try {
					using(TextReader textReader = new StreamReader(path)) {
						claimForm=(ClaimForm)xmlSerializer.Deserialize(textReader);
					}
				}
				catch {
					throw new ApplicationException(Lans.g("FormClaimForm","Invalid file format"));
				}
			}
			else {//use xmlData
				try {
					using(TextReader textReader = new StringReader(xmlData)) {
						claimForm=(ClaimForm)xmlSerializer.Deserialize(textReader);
					}
				}
				catch {
					throw new ApplicationException(Lans.g("FormClaimForm","Invalid file format"));
				}
			}
			return claimForm;
		}

		///<summary></summary>
		public static void Update(ClaimForm claimForm){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),claimForm);
				return;
			}
			//Synch the claim form items associated to this claim form first.
			ClaimFormItems.DeleteAllForClaimForm(claimForm.ClaimFormNum);
			for(int i=0;i<claimForm.Items.Count;i++){
				ClaimFormItems.Insert(claimForm.Items[i]);
			}
			//Now we can update any information specific to the claim form itself.
			Crud.ClaimFormCrud.Update(claimForm);
		}

		///<summary> Called when cancelling out of creating a new claimform, and from the claimform window when clicking delete. Returns true if successful or false if dependencies found.</summary>
		public static bool Delete(ClaimForm claimForm){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),claimForm);
			}
			//first, do dependency testing
			string command="SELECT * FROM insplan WHERE claimformnum = '"
				+claimForm.ClaimFormNum.ToString()+"' ";
			command+=DbHelper.LimitAnd(1);
 			DataTable table=Db.GetTable(command);
			if(table.Rows.Count==1){
				return false;
			}
			//26.2: a provider's default claim form (provider.ClaimFormNum) is in use too.
			if(ClaimFormDefaults.GetProviderAbbrsUsing(claimForm.ClaimFormNum).Count>0) {
				return false;
			}
			//Then, delete the claimform
			command="DELETE FROM claimform "
				+"WHERE ClaimFormNum = '"+POut.Long(claimForm.ClaimFormNum)+"'";
			Db.NonQ(command);
			command="DELETE FROM claimformitem "
				+"WHERE ClaimFormNum = '"+POut.Long(claimForm.ClaimFormNum)+"'";
			Db.NonQ(command);
			return true;
		}
		
		///<summary>Returns the claim form specified by the given claimFormNum</summary>
		public static ClaimForm GetClaimForm(long claimFormNum) {
			Meth.NoCheckMiddleTierRole();
			return GetFirstOrDefault(x => x.ClaimFormNum==claimFormNum);
		}

		///<summary>Returns a list of all internal claims within the OpenPmsDentalBusiness resources.  Throws exceptions.</summary>
		public static List<ClaimForm> GetInternalClaims() {
			Meth.NoCheckMiddleTierRole();
			List<ClaimForm> listClaimFormsInternal = new List<ClaimForm>();
			ResourceSet resourceSet=OpenPmsDentalBusiness.Properties.Resources.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture,true,true);
			//No way to refactor dictionaryEntry out.
			foreach(DictionaryEntry item in resourceSet) {
				if(!item.Key.ToString().StartsWith("ClaimForm")) {
					continue;
				}
				//Resources that start with ClaimForm are serialized ClaimForm objects in XML.
				ClaimForm claimForm = ClaimForms.DeserializeClaimForm("",item.Value.ToString());
				claimForm.IsInternal=true;
				listClaimFormsInternal.Add(claimForm);
			}
			return listClaimFormsInternal;
		}

		///<summary>Description of the built-in claim form for the Medi-Cal Dental (Denti-Cal) TAR/Claim form DC-217, revision 10/19, printed on
		///preprinted stock (Open Dental 25.4). Also the key that finds an installed copy.</summary>
		public const string DescriptionDentiCalDC217_2019="Denti-Cal DC217_2019";

		///<summary>Returns the internal claim form with this description from the OpenPmsDentalBusiness resources, or null if there is none.  Throws if a
		///resource can't be read.</summary>
		public static ClaimForm GetInternalClaim(string description) {
			Meth.NoCheckMiddleTierRole();
			return GetInternalClaims().FirstOrDefault(x => x.Description==description);
		}

		///<summary>Installs the internal claim form with this description as a claim form of the office, with all its items, so it can be chosen for
		///insurance plans; does nothing when a claim form with that description already exists. Returns the ClaimFormNum of the new or existing claim
		///form, or 0 when there is no internal claim form with that description. Uses no cache, so a fork migration step can call it; callers outside
		///a migration refresh the ClaimForms and ClaimFormItems caches afterwards.</summary>
		public static long InsertInternalIfMissing(string description) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),description);
			}
			string command=DbHelper.LimitOrderBy("SELECT ClaimFormNum FROM claimform WHERE Description='"+POut.String(description)+"' "
				+"ORDER BY ClaimFormNum",1);
			long claimFormNumExisting=Db.GetLong(command);
			if(claimFormNumExisting>0) {
				return claimFormNumExisting;
			}
			ClaimForm claimForm=GetInternalClaim(description);
			if(claimForm==null) {
				return 0;
			}
			long claimFormNum=Crud.ClaimFormCrud.InsertNoCache(claimForm);
			for(int i=0;i<claimForm.Items.Count;i++) {
				claimForm.Items[i].ClaimFormNum=claimFormNum;
				Crud.ClaimFormItemCrud.InsertNoCache(claimForm.Items[i]);
			}
			return claimFormNum;
		}

		///<summary>Returns number of insplans affected.</summary>
		public static long Reassign(long claimFormNumOld,long claimFormNumNew) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),claimFormNumOld,claimFormNumNew);
			}
			string command="UPDATE insplan SET ClaimFormNum="+POut.Long(claimFormNumNew)
				+" WHERE ClaimFormNum="+POut.Long(claimFormNumOld);
			return Db.NonQ(command);
		}
	}

	



}









