using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenPmsDentalBusiness{
	///<summary></summary>
	public class ChartViews{
		#region CachePattern
		
		private class ChartViewCache : CacheListAbs<ChartView> {
			protected override List<ChartView> GetCacheFromDb() {
				string command="SELECT * FROM chartview ORDER BY ItemOrder";
				return Crud.ChartViewCrud.SelectMany(command);
			}
			protected override List<ChartView> TableToList(DataTable table) {
				return Crud.ChartViewCrud.TableToList(table);
			}
			protected override ChartView Copy(ChartView chartView) {
				return chartView.Copy();
			}
			protected override DataTable ListToTable(List<ChartView> listChartViews) {
				return Crud.ChartViewCrud.ListToTable(listChartViews,"ChartView");
			}
			protected override void FillCacheIfNeeded() {
				ChartViews.GetTableFromCache(false);
			}
		}
		
		///<summary>The object that accesses the cache in a thread-safe manner.</summary>
		private static ChartViewCache _chartViewCache=new ChartViewCache();

		public static List<ChartView> GetDeepCopy(bool isShort=false) {
			return _chartViewCache.GetDeepCopy(isShort);
		}

		public static ChartView GetFirst(bool isShort=false) {
			return _chartViewCache.GetFirst(isShort);
		}

		///<summary>Refreshes the cache and returns it as a DataTable. This will refresh the ClientWeb's cache and the ServerWeb's cache.</summary>
		public static DataTable RefreshCache() {
			return GetTableFromCache(true);
		}

		///<summary>Fills the local cache with the passed in DataTable.</summary>
		public static void FillCacheFromTable(DataTable table) {
			_chartViewCache.FillCacheFromTable(table);
		}

		///<summary>Always refreshes the ClientWeb's cache.</summary>
		public static DataTable GetTableFromCache(bool doRefreshCache) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				DataTable table=Meth.GetTable(MethodBase.GetCurrentMethod(),doRefreshCache);
				_chartViewCache.FillCacheFromTable(table);
				return table;
			}
			return _chartViewCache.GetTableFromCache(doRefreshCache);
		}

		public static void ClearCache() {
			_chartViewCache.ClearCache();
		}
		#endregion

		///<summary></summary>
		public static long Insert(ChartView chartView) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				chartView.ChartViewNum=Meth.GetLong(MethodBase.GetCurrentMethod(),chartView);
				return chartView.ChartViewNum;
			}
			return Crud.ChartViewCrud.Insert(chartView);
		}

		///<summary></summary>
		public static bool Update(ChartView chartView,ChartView chartViewOld=null) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),chartView,chartViewOld);
			}
			if(chartViewOld is null) { 
				Crud.ChartViewCrud.Update(chartView);
				return true;
			}
			else {
				return Crud.ChartViewCrud.Update(chartView,chartViewOld);
			}
		}

		///<summary></summary>
		public static void Delete(long chartViewNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),chartViewNum);
				return;
			}
			string command= "DELETE FROM chartview WHERE ChartViewNum = "+POut.Long(chartViewNum);
			Db.NonQ(command);
		}

		#region Audit Trail (Open Dental 26.1)
		//Adding, editing, deleting and reordering a chart view write Audit Trail entries under ChartViewsEdit, the permission that gates those actions.

		///<summary>Writes "Chart view (name) added."</summary>
		public static void LogAdded(ChartView chartView) {
			Meth.NoCheckMiddleTierRole();
			SecurityLogs.MakeLogEntry(EnumPermType.ChartViewsEdit,0,GetAddedLogText(chartView),chartView.ChartViewNum,DateTime.MinValue);
		}

		///<summary>Writes "Chart view (name) deleted."</summary>
		public static void LogDeleted(ChartView chartView) {
			Meth.NoCheckMiddleTierRole();
			SecurityLogs.MakeLogEntry(EnumPermType.ChartViewsEdit,0,GetDeletedLogText(chartView),chartView.ChartViewNum,DateTime.MinValue);
		}

		///<summary>Writes "Chart view (name) edited: ..." with every changed setting. Writes nothing and returns false when nothing changed. The display
		///field lists are the view's columns before and after, in order; pass null for both to leave the columns out.</summary>
		public static bool LogEdited(ChartView chartViewOld,ChartView chartViewNew,List<DisplayField> listDisplayFieldsOld,
			List<DisplayField> listDisplayFieldsNew)
		{
			Meth.NoCheckMiddleTierRole();
			List<string> listChanges=GetEditChanges(chartViewOld,chartViewNew,listDisplayFieldsOld,listDisplayFieldsNew);
			if(listChanges.Count==0) {
				return false;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.ChartViewsEdit,0,GetEditedLogText(chartViewNew,listChanges),chartViewNew.ChartViewNum,DateTime.MinValue);
			return true;
		}

		///<summary>Writes "Chart view (name) moved from position a to b." Indexes are 0-based list positions; the text shows them 1-based.</summary>
		public static void LogMoved(ChartView chartView,int indexFrom,int indexTo) {
			Meth.NoCheckMiddleTierRole();
			if(indexFrom==indexTo) {
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.ChartViewsEdit,0,GetMovedLogText(chartView,indexFrom,indexTo),chartView.ChartViewNum,DateTime.MinValue);
		}

		public static string GetAddedLogText(ChartView chartView) {
			Meth.NoCheckMiddleTierRole();
			return "Chart view "+GetAuditName(chartView)+" added.";
		}

		public static string GetDeletedLogText(ChartView chartView) {
			Meth.NoCheckMiddleTierRole();
			return "Chart view "+GetAuditName(chartView)+" deleted.";
		}

		public static string GetEditedLogText(ChartView chartView,List<string> listChanges) {
			Meth.NoCheckMiddleTierRole();
			return "Chart view "+GetAuditName(chartView)+" edited: "+string.Join("; ",listChanges)+".";
		}

		public static string GetMovedLogText(ChartView chartView,int indexFrom,int indexTo) {
			Meth.NoCheckMiddleTierRole();
			return "Chart view "+GetAuditName(chartView)+" moved from position "+(indexFrom+1)+" to "+(indexTo+1)+".";
		}

		///<summary>One "setting old to new" item per changed setting of the Chart View window. ItemOrder is left out (moves have their own entry).</summary>
		public static List<string> GetEditChanges(ChartView chartViewOld,ChartView chartViewNew,List<DisplayField> listDisplayFieldsOld,
			List<DisplayField> listDisplayFieldsNew)
		{
			Meth.NoCheckMiddleTierRole();
			List<string> listChanges=new List<string>();
			if(chartViewOld.Description!=chartViewNew.Description) {
				listChanges.Add("description '"+chartViewOld.Description+"' to '"+chartViewNew.Description+"'");
			}
			AddChange(listChanges,"procedure statuses",GetFlagText(chartViewOld.ProcStatuses),GetFlagText(chartViewNew.ProcStatuses));
			AddChange(listChanges,"show",GetFlagText(chartViewOld.ObjectTypes),GetFlagText(chartViewNew.ObjectTypes));
			AddChange(listChanges,"dates",chartViewOld.DatesShowing.ToString(),chartViewNew.DatesShowing.ToString());
			AddChange(listChanges,"procedure notes",GetOnOff(chartViewOld.ShowProcNotes),GetOnOff(chartViewNew.ShowProcNotes));
			AddChange(listChanges,"audit mode",GetOnOff(chartViewOld.IsAudit),GetOnOff(chartViewNew.IsAudit));
			AddChange(listChanges,"selected teeth only",GetOnOff(chartViewOld.SelectedTeethOnly),GetOnOff(chartViewNew.SelectedTeethOnly));
			AddChange(listChanges,"treatment plan charting",GetOnOff(chartViewOld.IsTpCharting),GetOnOff(chartViewNew.IsTpCharting));
			AddChange(listChanges,"Orion statuses",GetFlagText(chartViewOld.OrionStatusFlags),GetFlagText(chartViewNew.OrionStatusFlags));
			if(listDisplayFieldsOld!=null && listDisplayFieldsNew!=null) {
				AddChange(listChanges,"columns",GetColumnsText(listDisplayFieldsOld),GetColumnsText(listDisplayFieldsNew));
			}
			return listChanges;
		}

		///<summary>Names of the single-bit members that are set, comma separated, or "none". Works for the [Flags] enums of a chart view.</summary>
		public static string GetFlagText<T>(T value) where T:Enum {
			Meth.NoCheckMiddleTierRole();
			long longValue=Convert.ToInt64(value);
			List<string> listNames=new List<string>();
			foreach(T member in Enum.GetValues(typeof(T))) {
				long longMember=Convert.ToInt64(member);
				bool isSingleBit=longMember>0 && (longMember & (longMember-1))==0;
				if(isSingleBit && (longValue & longMember)==longMember) {
					listNames.Add(member.ToString());
				}
			}
			return listNames.Count==0 ? "none" : string.Join(", ",listNames);
		}

		///<summary>The columns in order: internal name, the custom description in parentheses when there is one, and the width.</summary>
		public static string GetColumnsText(List<DisplayField> listDisplayFields) {
			Meth.NoCheckMiddleTierRole();
			if(listDisplayFields.Count==0) {
				return "default";
			}
			return string.Join(", ",listDisplayFields.Select(x => x.InternalName+(string.IsNullOrEmpty(x.Description) ? "" : " ("+x.Description+")")+" "+x.ColumnWidth));
		}

		private static void AddChange(List<string> listChanges,string setting,string valueOld,string valueNew) {
			if(valueOld!=valueNew) {
				listChanges.Add(setting+" "+valueOld+" to "+valueNew);
			}
		}

		private static string GetOnOff(bool value) {
			return value ? "on" : "off";
		}

		private static string GetAuditName(ChartView chartView) {
			return "'"+chartView.Description+"'";
		}
		#endregion Audit Trail (Open Dental 26.1)

		/*
		Only pull out the methods below as you need them.  Otherwise, leave them commented out.

		///<summary></summary>
		public static List<ChartView> Refresh(long patNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<ChartView>>(MethodBase.GetCurrentMethod(),patNum);
			}
			string command="SELECT * FROM chartview WHERE PatNum = "+POut.Long(patNum);
			return Crud.ChartViewCrud.SelectMany(command);
		}

		///<summary>Gets one ChartView from the db.</summary>
		public static ChartView GetOne(long chartViewNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				return Meth.GetObject<ChartView>(MethodBase.GetCurrentMethod(),chartViewNum);
			}
			return Crud.ChartViewCrud.SelectOne(chartViewNum);
		}

		*/
	}
}