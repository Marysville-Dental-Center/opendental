using CodeBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenPmsDentalBusiness {
	///<summary></summary>
	public class Etrans835s{

		#region Methods - Get
		///<summary></summary>
		public static List<Etrans835> GetByEtransNums(params long[] arrayEtransNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Etrans835>>(MethodBase.GetCurrentMethod(),arrayEtransNums);
			}
			if(arrayEtransNums.Length==0) {
				return new List<Etrans835>();
			}
			string command="SELECT * FROM etrans835 WHERE EtransNum IN("+string.Join(",",arrayEtransNums)+")";
			return Crud.Etrans835Crud.SelectMany(command);
		}

		///<summary>All parameters are optional and will be excluded from the query if not set.
		///Strings are considered not set if blank, dates are considered not set if equal to DateTime.MinVal, decimals are not set if negative.</summary>
		public static List<Etrans835> GetFiltered(DateTime dateFrom,DateTime dateTo,string carrierName,string checkTraceNum,decimal insPaidMin,decimal insPaidMax,
			string controlId,List<X835AutoProcessed> listAutoProcessedStatuses=null,bool DoIncludeAcknolwedged=true,params X835Status[] arrayStatuses)
		{
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Etrans835>>(MethodBase.GetCurrentMethod(),dateFrom,dateTo,carrierName,checkTraceNum,
					insPaidMin,insPaidMax,controlId,listAutoProcessedStatuses,DoIncludeAcknolwedged,arrayStatuses);
			}
			string command="SELECT * FROM etrans835 "
				+"INNER JOIN etrans on etrans.EtransNum=etrans835.EtransNum ";
			List<string> listJoinClauses=new List<string>();
			if(carrierName!="") {
				listJoinClauses.Add("LOWER(TRIM(etrans835.PayerName)) LIKE '%"+POut.String(carrierName.ToLower().Trim())+"%'");
			}
			if(checkTraceNum!="") {
				listJoinClauses.Add("LOWER(TRIM(etrans835.TransRefNum)) LIKE '%"+POut.String(checkTraceNum.ToLower().Trim())+"%'");
			}
			if(insPaidMin >= 0) {
				listJoinClauses.Add("etrans835.InsPaid >= "+POut.Decimal(insPaidMin));
			}
			if(insPaidMax >= 0) {
				listJoinClauses.Add("etrans835.InsPaid <= "+POut.Decimal(insPaidMax));
			}
			if(controlId!="") {
				listJoinClauses.Add("LOWER(TRIM(etrans835.ControlId)) LIKE '%"+POut.String(controlId.ToLower().Trim())+"%'");
			}
			if(arrayStatuses.Length > 0) {
				listJoinClauses.Add("etrans835.Status IN ("+string.Join(",",arrayStatuses.Select(x => (int)x))+")");
			}
			if(!DoIncludeAcknolwedged) {
				listJoinClauses.Add("etrans835.IsApproved="+POut.Bool(false));
			}
			if(!listAutoProcessedStatuses.IsNullOrEmpty()) {
				listJoinClauses.Add("etrans835.AutoProcessed IN ("+string.Join(",",listAutoProcessedStatuses.Select(x => (int)x))+")");
			}
			if(listJoinClauses.Count > 0) {
				command+=" AND "+string.Join(" AND ",listJoinClauses);
			}
			List<string> listWhereClauses=new List<string>();
			if(dateFrom!=DateTime.MinValue) {
				listWhereClauses.Add(DbHelper.DtimeToDate("etrans.DateTimeTrans")+" >= "+POut.Date(dateFrom));
			}
			if(dateTo!=DateTime.MinValue) {
				listWhereClauses.Add(DbHelper.DtimeToDate("etrans.DateTimeTrans")+" <= "+POut.Date(dateTo));
			}
			if(listWhereClauses.Count > 0) {
				command+=" WHERE "+string.Join(" AND ",listWhereClauses);
			}
			return Crud.Etrans835Crud.SelectMany(command);
		}
		#endregion Methods - Get

		#region Methods - Modify
		///<summary></summary>
		public static long Insert(Etrans835 etrans835){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				etrans835.Etrans835Num=Meth.GetLong(MethodBase.GetCurrentMethod(),etrans835);
				return etrans835.Etrans835Num;
			}
			return Crud.Etrans835Crud.Insert(etrans835);
		}

		///<summary></summary>
		public static void Update(Etrans835 etrans835,Etrans835 oldEtrans835){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				Meth.GetVoid(MethodBase.GetCurrentMethod(),etrans835,oldEtrans835);
				return;
			}
			Crud.Etrans835Crud.Update(etrans835,oldEtrans835);
		}

		public static void Upsert(Etrans835 etrans835,X835 x835,X835AutoProcessed newAutoProcessedStatus=X835AutoProcessed.None) {
			Etrans835 oldEtrans835=etrans835.Copy();
			if(newAutoProcessedStatus!=X835AutoProcessed.None) {
				etrans835.AutoProcessed=newAutoProcessedStatus;
			}
			etrans835.PayerName=x835.PayerName;
			etrans835.TransRefNum=x835.TransRefNum;
			etrans835.InsPaid=(double)x835.InsPaid;
			etrans835.ControlId=x835.ControlId;
			etrans835.PaymentMethodCode=x835.PaymentMethodCode;
			List<string> listPatNames=x835.ListClaimsPaid.Select(x => x.PatientName.ToString()).Distinct().ToList();
			etrans835.PatientName=(listPatNames.Count>0 ? listPatNames[0] : "");
			if(listPatNames.Count>1) {
				etrans835.PatientName="("+POut.Long(listPatNames.Count)+")";
			}
			etrans835.Status=x835.GetStatus();
			if(etrans835.Etrans835Num==0) {
				Insert(etrans835);
			}
			else {
				Update(etrans835,oldEtrans835);
			}
		}

		#endregion Methods - Modify
		#region Methods - Misc
		#endregion Methods - Misc

		#region M2-ERA: ERAs Automatically Processed report filters (Open Dental 25.4)
		///<summary>Removes, at the same index from both lists, the ERAs that the ERAs Automatically Processed report hides: with doShowPreAuths false,
		///ERAs whose claims are all predeterminations; with doShowZeroPayments false, ERAs whose total payment is zero. The lists must be 1:1 and in the
		///same order (EtransL.GetEraDataFiltered). The ERA text is loaded only when PreAuths are hidden.</summary>
		public static void RemoveHiddenForAutoProcessedReport(List<Etrans> listEtrans,List<Etrans835> listEtrans835s,bool doShowPreAuths,
			bool doShowZeroPayments)
		{
			Meth.NoCheckMiddleTierRole();
			if((doShowPreAuths && doShowZeroPayments) || listEtrans==null || listEtrans835s==null) {
				return;
			}
			Dictionary<long,string> dictMessageTexts=new Dictionary<long,string>();
			if(!doShowPreAuths) {
				List<long> listEtransMessageTextNums=listEtrans.Where(x => x!=null).Select(x => x.EtransMessageTextNum).Distinct().ToList();
				dictMessageTexts=EtransMessageTexts.GetMessageTexts(listEtransMessageTextNums);
			}
			for(int i=Math.Min(listEtrans.Count,listEtrans835s.Count)-1;i>=0;i--) {
				Etrans etrans=listEtrans[i];
				string messageText835="";
				if(etrans!=null && dictMessageTexts.ContainsKey(etrans.EtransMessageTextNum)) {
					messageText835=dictMessageTexts[etrans.EtransMessageTextNum];
				}
				if(!IsHiddenForAutoProcessedReport(listEtrans835s[i],messageText835,etrans?.TranSetId835,doShowPreAuths,doShowZeroPayments)) {
					continue;
				}
				listEtrans.RemoveAt(i);
				listEtrans835s.RemoveAt(i);
			}
		}

		///<summary>True when the report hides this ERA: Show $0 Payments is off and the ERA's total payment (BPR02, etrans835.InsPaid) is zero, or Show
		///PreAuths is off and every claim on the ERA is a predetermination (see IsPreauthOnly). Both on: never hidden.</summary>
		public static bool IsHiddenForAutoProcessedReport(Etrans835 etrans835,string messageText835,string tranSetId835,bool doShowPreAuths,
			bool doShowZeroPayments)
		{
			Meth.NoCheckMiddleTierRole();
			if(!doShowZeroPayments && etrans835!=null && CompareDouble.IsZero(etrans835.InsPaid)) {
				return true;
			}
			if(!doShowPreAuths && IsPreauthOnly(messageText835,tranSetId835)) {
				return true;
			}
			return false;
		}

		///<summary>True when the 835 has at least one claim (CLP loop) and every claim is a predetermination (CLP02 = 25, "Predetermination Pricing
		///Only - No Payment", the same test as Hx835_Claim.IsPreauth). Looks at the EOB (transaction set) the etrans stands for; an etrans from 14.2
		///and older with a blank TranSetId835 stands for every EOB in the file. False for text that is not X12. Reads the segments only, no
		///database.</summary>
		public static bool IsPreauthOnly(string messageText835,string tranSetId835) {
			Meth.NoCheckMiddleTierRole();
			X12object x12object=X12object.ToX12object(messageText835);
			if(x12object==null || x12object.FunctGroups==null || x12object.FunctGroups.Count==0 || x12object.FunctGroups[0].Transactions==null) {
				return false;
			}
			List<X12Transaction> listX12Transactions=x12object.FunctGroups[0].Transactions.FindAll(x => x!=null);
			if(listX12Transactions.Count==0) {
				return false;
			}
			if(!string.IsNullOrEmpty(tranSetId835)) {
				//Mimics X835.ProcessMessage(): the transaction with this ST02, or the first one.
				X12Transaction x12Transaction=listX12Transactions.FirstOrDefault(x => x.Header!=null && x.Header.Get(2)==tranSetId835)??listX12Transactions[0];
				listX12Transactions=new List<X12Transaction>() { x12Transaction };
			}
			List<X12Segment> listSegmentsClp=listX12Transactions
				.Where(x => x.Segments!=null)
				.SelectMany(x => x.Segments)
				.Where(x => x!=null && x.SegmentID=="CLP")
				.ToList();
			return listSegmentsClp.Count>0 && listSegmentsClp.All(x => x.Get(2)=="25");
		}
		#endregion M2-ERA: ERAs Automatically Processed report filters (Open Dental 25.4)
	}

	///<summary>M2-ERA (Open Dental 25.4): the Show PreAuths and Show $0 Payments choices of the ERAs Automatically Processed report, remembered per
	///user (UserNum) while the program runs. Both default to on, which shows what 24.3 showed. Kept in memory: a userodpref row would need a new
	///UserOdFkeyType value, which belongs to the registry packages (see docs/port-notes/M2-ERA.md).</summary>
	public class EraAutoProcessedReportFilters {
		private static readonly object _lock=new object();
		private static readonly Dictionary<long,EraAutoProcessedReportFilters> _dictByUserNum=new Dictionary<long,EraAutoProcessedReportFilters>();
		///<summary>Show ERAs whose claims are all predeterminations.</summary>
		public bool DoShowPreAuths=true;
		///<summary>Show ERAs whose total payment is zero.</summary>
		public bool DoShowZeroPayments=true;

		///<summary>The choices the user made last in this session, or the defaults (both on). Returns a copy.</summary>
		public static EraAutoProcessedReportFilters GetForUser(long userNum) {
			lock(_lock) {
				if(_dictByUserNum.TryGetValue(userNum,out EraAutoProcessedReportFilters filters)) {
					return new EraAutoProcessedReportFilters() { DoShowPreAuths=filters.DoShowPreAuths,DoShowZeroPayments=filters.DoShowZeroPayments };
				}
			}
			return new EraAutoProcessedReportFilters();
		}

		///<summary>Remembers the user's choices for the rest of the session.</summary>
		public static void SetForUser(long userNum,bool doShowPreAuths,bool doShowZeroPayments) {
			lock(_lock) {
				_dictByUserNum[userNum]=new EraAutoProcessedReportFilters() { DoShowPreAuths=doShowPreAuths,DoShowZeroPayments=doShowZeroPayments };
			}
		}

		///<summary>Forgets every user's choices. For tests.</summary>
		public static void ClearAll() {
			lock(_lock) {
				_dictByUserNum.Clear();
			}
		}
	}
}