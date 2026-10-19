using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness {
	///<summary></summary>
	public class SigButDefs {
		#region CachePattern

		private class SigButDefCache : CacheListAbs<SigButDef> {
			protected override List<SigButDef> GetCacheFromDb() {
				string command="SELECT * FROM sigbutdef ORDER BY ButtonIndex";
				return Crud.SigButDefCrud.SelectMany(command);
			}
			protected override List<SigButDef> TableToList(DataTable table) {
				return Crud.SigButDefCrud.TableToList(table);
			}
			protected override SigButDef Copy(SigButDef sigButDef) {
				return sigButDef.Copy();
			}
			protected override DataTable ListToTable(List<SigButDef> listSigButDefs) {
				return Crud.SigButDefCrud.ListToTable(listSigButDefs,"SigButDef");
			}
			protected override void FillCacheIfNeeded() {
				SigButDefs.GetTableFromCache(false);
			}
		}
		
		///<summary>The object that accesses the cache in a thread-safe manner.</summary>
		private static SigButDefCache _sigButDefCache=new SigButDefCache();

		public static List<SigButDef> GetDeepCopy(bool isShort=false) {
			return _sigButDefCache.GetDeepCopy(isShort);
		}

		public static List<SigButDef> GetWhere(Predicate<SigButDef> match,bool isShort=false) {
			return _sigButDefCache.GetWhere(match,isShort);
		}

		///<summary>Refreshes the cache and returns it as a DataTable. This will refresh the ClientWeb's cache and the ServerWeb's cache.</summary>
		public static DataTable RefreshCache() {
			return GetTableFromCache(true);
		}

		///<summary>Fills the local cache with the passed in DataTable.</summary>
		public static void FillCacheFromTable(DataTable table) {
			_sigButDefCache.FillCacheFromTable(table);
		}

		///<summary>Always refreshes the ClientWeb's cache.</summary>
		public static DataTable GetTableFromCache(bool doRefreshCache) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				DataTable table=Meth.GetTable(MethodBase.GetCurrentMethod(),doRefreshCache);
				_sigButDefCache.FillCacheFromTable(table);
				return table;
			}
			return _sigButDefCache.GetTableFromCache(doRefreshCache);
		}

		public static void ClearCache() {
			_sigButDefCache.ClearCache();
		}
		#endregion

		///<summary></summary>
		public static void Update(SigButDef sigButDef) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),sigButDef);
				return;
			}
			Crud.SigButDefCrud.Update(sigButDef);
		}

		///<summary></summary>
		public static long Insert(SigButDef sigButDef) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				sigButDef.SigButDefNum=Meth.GetLong(MethodBase.GetCurrentMethod(),sigButDef);
				return sigButDef.SigButDefNum;
			}
			return Crud.SigButDefCrud.Insert(sigButDef);
		}

		///<summary>No need to surround with try/catch, because all deletions are allowed.</summary>
		public static void Delete(SigButDef sigButDef) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),sigButDef);
				return;
			}
			string command="DELETE FROM sigbutdef WHERE SigButDefNum ="+POut.Long(sigButDef.SigButDefNum);
			Db.NonQ(command);
		}

		///<summary>Loops through the SigButDefs passed in and updates the database if any of the ButtonIndexes changed.  Returns true if any changes were made to the database so that the calling class can invalidate the cache.</summary>
		public static bool UpdateButtonIndexIfChanged(SigButDef[] sigButDefArray) {
			Meth.NoCheckMiddleTierRole();
			bool hasChanges=false;
			List<SigButDef> listSigButDefs=GetDeepCopy(false);
			for(int i=0;i<listSigButDefs.Count;i++) {
				for(int j=0;j<sigButDefArray.Length;j++) {
					if(listSigButDefs[i].SigButDefNum!=sigButDefArray[j].SigButDefNum) {
						continue;
					}
					//This is the same SigButDef
					if(listSigButDefs[i].ButtonIndex!=sigButDefArray[j].ButtonIndex) {
						hasChanges=true;
						Update(sigButDefArray[j]);//Update the database with the new button index.
					}
				}
			}
			return hasChanges;
		}

		///<summary>Used in Setup.  The returned list also includes defaults.  The supplied computer name can be blank for the default setup.</summary>
		public static SigButDef[] GetByComputer(string computerName) {
			Meth.NoCheckMiddleTierRole();
			List<SigButDef> listSigButDefs=GetWhere(x => x.ComputerName=="" || x.ComputerName.ToUpper()==computerName.ToUpper());
			listSigButDefs.Sort(CompareButtonsByIndex);
			return listSigButDefs.ToArray();
		}

		private static int CompareButtonsByIndex(SigButDef sigButDefX,SigButDef sigButDefY) {
			Meth.NoCheckMiddleTierRole();
			if(sigButDefX.ButtonIndex!=sigButDefY.ButtonIndex) {
				return sigButDefX.ButtonIndex.CompareTo(sigButDefY.ButtonIndex);
			}
			//we compair y to x here due to a nuance in the way light buttons are drawn. This makes computer specific
			//buttons drawn "on-top-of" the default buttons.
			return sigButDefY.ComputerName.CompareTo(sigButDefX.ComputerName);
		}

		///<summary>Moves the selected item up in the supplied sub list.  Does not update the cache because the user could want to potentially move buttons around a lot.</summary>
		public static List<SigButDef> MoveUp(SigButDef sigButDefSelected,SigButDef[] sigButDefArraySub) {
			Meth.NoCheckMiddleTierRole();
			if(sigButDefSelected.ButtonIndex==0) {//already at top
				return sigButDefArraySub.ToList();
			}
			SigButDef sigButDefOccupied=null;
			int occupiedIdx=-1;
			int selectedIdx=-1;
			for(int i=0;i<sigButDefArraySub.Length;i++) {
				if(sigButDefArraySub[i].SigButDefNum!=sigButDefSelected.SigButDefNum//if not the selected object
					&& sigButDefArraySub[i].ButtonIndex==sigButDefSelected.ButtonIndex-1
					&& (sigButDefArraySub[i].ComputerName!="" || sigButDefSelected.ComputerName==""))
				{
					//We want to swap positions with the selected button, which happens if we are moving a default button or moving to a non-default button.
					sigButDefOccupied=sigButDefArraySub[i].Copy();
					occupiedIdx=i;
				}
				if(sigButDefArraySub[i].SigButDefNum==sigButDefSelected.SigButDefNum) {
					selectedIdx=i;
				}
			}
			if(sigButDefOccupied!=null) {
				sigButDefArraySub[occupiedIdx].ButtonIndex++;
			}
			sigButDefArraySub[selectedIdx].ButtonIndex--;
			List<SigButDef> listSigButDefs=new List<SigButDef>();
			for(int i=0;i<sigButDefArraySub.Length;i++) {
				listSigButDefs.Add(sigButDefArraySub[i].Copy());
			}
			listSigButDefs.Sort(CompareButtonsByIndex);
			return listSigButDefs;
		}

		///<summary>Moves the selected item down in the supplied sub list.  Does not update the cache because the user could want to potentially move buttons around a lot.</summary>
		public static List<SigButDef> MoveDown(SigButDef sigButDefSelected,SigButDef[] sigButDefArraySub) {
			Meth.NoCheckMiddleTierRole();
			int occupiedIdx=-1;
			int selectedIdx=-1;
			SigButDef sigButDefOccupied=null;
			for(int i=0;i<sigButDefArraySub.Length;i++) {
				if(sigButDefArraySub[i].SigButDefNum!=sigButDefSelected.SigButDefNum//if not the selected object
					&& sigButDefArraySub[i].ButtonIndex==sigButDefSelected.ButtonIndex+1 
					&& (sigButDefArraySub[i].ComputerName!="" || sigButDefSelected.ComputerName=="")) 
				{
					//We want to swap positions with the selected button, which happens if we are moving a default button or moving to a non-default button.
					sigButDefOccupied=sigButDefArraySub[i].Copy();
					occupiedIdx=i;
				}
				if(sigButDefArraySub[i].SigButDefNum==sigButDefSelected.SigButDefNum) {
					selectedIdx=i;
				}
			}
			if(sigButDefOccupied!=null) {
				sigButDefArraySub[occupiedIdx].ButtonIndex--;
			}
			sigButDefArraySub[selectedIdx].ButtonIndex++;
			List<SigButDef> listSigButDefs=new List<SigButDef>();
			for(int i=0;i<sigButDefArraySub.Length;i++) {
				listSigButDefs.Add(sigButDefArraySub[i].Copy());
			}
			listSigButDefs.Sort(CompareButtonsByIndex);;
			return listSigButDefs;
		}

		///<summary>Returns the SigButDef with the specified buttonIndex.  Used from the setup page.  The supplied list will already have been filtered by computername.  Supply buttonIndex in 0-based format.</summary>
		public static SigButDef GetByIndex(int buttonIndex,List<SigButDef> listSigButDefsSub) {
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<listSigButDefsSub.Count;i++) {
				if(listSigButDefsSub[i].ButtonIndex==buttonIndex) {
					//Will always return a specific computer's button over a default if there are 2 buttons with the same index.  See CompareButtonsByIndex.
					return listSigButDefsSub[i].Copy();
				}
			}
			return null;
		}

		///<summary>Returns the SigButDef with the specified buttonIndex.  Used from the setup page.  The supplied list will already have been filtered by computername.  Supply buttonIndex in 0-based format.</summary>
		public static SigButDef GetByIndex(int buttonIndex,SigButDef[] sigButDefArraySub) {
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<sigButDefArraySub.Length;i++) {
				if(sigButDefArraySub[i].ButtonIndex==buttonIndex) {
					//Will always return a specific computer's button over a default if there are 2 buttons with the same index.  See CompareButtonsByIndex.
					return sigButDefArraySub[i].Copy();
				}
			}
			return null;
		}

	}













}