using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using CodeBase;

namespace DataConnectionBase {
	public class QueryMonitor {
		public const string LOG_DIR="QueryMonitor";
		public static QueryMonitor Monitor {
			get {
				return _monitorT??_monitor;
			}
			set {
				_monitorT=value;
			}
		}
		private static QueryMonitor _monitor=new QueryMonitor();
		[ThreadStatic]
		private static QueryMonitor _monitorT;
		///<summary>Set to true if monitoring queries from FormQueryMonitor.  This will cause the query log to contain runnable queries (i.e. with query
		///parameters replaced with actual parameter value), and will include the method name of the calling method in DataConnection (i.e. GetTable, NonQ, etc).</summary>
		public bool IsMonitoring=false;
		public bool HasStackTrace=false;
		///<summary>The filter texts of the Query Monitor's active filter group. A query whose command text contains any of them (ignoring case) is
		///run as usual but never reported, so it stays out of the feed and the log. Replaced as a whole array, so readers on other threads always see
		///a complete list. Never null. Payloads (Middle Tier) are never filtered.</summary>
		private volatile string[] _arrayFilterTexts=new string[0];

		public event ODEventHandler Fired;

		///<summary>The filter texts currently applied to monitored queries. Empty when nothing is filtered.</summary>
		public string[] FilterTexts {
			get {
				return (string[])_arrayFilterTexts.Clone();
			}
		}

		///<summary>Replaces the filter texts applied to monitored queries. Null, empty and whitespace-only texts are dropped, because they would match
		///every query. Pass null or an empty list to stop filtering.</summary>
		public void SetFilterTexts(IEnumerable<string> listFilterTexts) {
			if(listFilterTexts==null) {
				_arrayFilterTexts=new string[0];
				return;
			}
			_arrayFilterTexts=listFilterTexts.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		}

		///<summary>True when the command text matches one of this monitor's filter texts.</summary>
		public bool IsFiltered(string command) {
			return IsCommandFiltered(command,_arrayFilterTexts);
		}

		///<summary>True when the command text contains any of the filter texts, ignoring case. Null, empty and whitespace-only filter texts never
		///match. A null command never matches.</summary>
		public static bool IsCommandFiltered(string command,IEnumerable<string> listFilterTexts) {
			if(command==null || listFilterTexts==null) {
				return false;
			}
			foreach(string filterText in listFilterTexts) {
				if(string.IsNullOrWhiteSpace(filterText)) {
					continue;
				}
				if(command.IndexOf(filterText,StringComparison.OrdinalIgnoreCase)>=0) {
					return true;
				}
			}
			return false;
		}

		public QueryMonitor() {
			#region ODThread Database Context
			//All new threads that are created will need to know about the QueryMonitor of their parent thread.
			//Set the global static QueryMonitor func and action within ODThread if they haven't been set already (only need to be set once).
			if(ODThread.GetQueryMonitorParent==null) {
				//The QueryMonitor getter will be executed on the parent thread and is designed to store this current QueryMonitor within ODThread.
				//The magic behind this func is that QueryMonitor.Monitor can return a ThreadStatic variable which will be the correct QueryMonitor.
				ODThread.GetQueryMonitorParent=() => QueryMonitor.Monitor;
			}
			if(ODThread.SetQueryMonitorChild==null) {
				//The QueryMonitor setter will be executed on the child thread and is designed to sync the QueryMonitor of the child with the parent.
				//Since we were the ones that just set the func above we can unbox the object passed to us as a QueryMonitor.
				ODThread.SetQueryMonitorChild=(queryMonitor) => {
					//For now the queryMonitor will only be the QueryMonitor of the parent thread.
					if(queryMonitor==null || !(queryMonitor is QueryMonitor queryMonitorParent)) {
						return;
					}
					QueryMonitor.Monitor=queryMonitorParent;
				};
			}
			#endregion
		}


		private void Fire(ODEventType odEventType,object tag) {
			QueryMonitorEvent.Fire(odEventType,tag);//Global event.
			Fired?.Invoke(new ODEventArgs(odEventType,tag));//Specific to this instance.
		}

		public string ProcessMonitoredPayload(Func<string,string> funcProcessPayload,string inputPayload) {
			if(!IsMonitoring) {
				// just process the payload and return its value like normal
				return funcProcessPayload(inputPayload);
			}
			DbQueryObj payloadData=null;
			Stopwatch payloadTimer=null;
			string functionOutput;
			try {
				// format the (stripped) payload for display
				System.Xml.Linq.XDocument parsedPayload=System.Xml.Linq.XDocument.Parse(inputPayload);
				System.Xml.Linq.XElement credentialsElement=parsedPayload.Root.Element("Credentials");
				if(credentialsElement!=null) {
					credentialsElement.Element("Password").Value="*redacted*";
				}
				string formattedPayload=parsedPayload.ToString();
				payloadData=new DbQueryObj(formattedPayload,"");//PayloadMonitor doesn't need to show server information.
				payloadData.IsPayload=true; //This is a payload, not a query.
				if(HasStackTrace) {
					payloadData.StackTrace=Environment.StackTrace;
				}
				Fire(ODEventType.QueryMonitor,payloadData);
				payloadData.DateTimeStart=DateTime.Now;
				payloadTimer=Stopwatch.StartNew();
				// Send the payload to the server and get the output
				functionOutput=funcProcessPayload(inputPayload);
			}
			finally {
				if(payloadTimer!=null && payloadData!=null) {
					payloadTimer.Stop();
					payloadData.DateTimeStop=payloadData.DateTimeStart.Add(payloadTimer.Elapsed);
					Fire(ODEventType.QueryMonitor,payloadData);
				}
			}
			return functionOutput;
		}

		public void RunMonitoredQuery(Action queryAction,DbCommand cmd,bool hasStackTrace=false) {
			if(!IsMonitoring) {
				queryAction();
				return;
			}
			DbQueryObj dbQueryObj=null;
			Stopwatch s=null;
			try {
				StackTrace stackTrace;
				dbQueryObj=new DbQueryObj(cmd.CommandText,cmd.Connection.DataSource);
				dbQueryObj.IsPayload=false; //This is a query, not a payload.
				//order by descending length of parameter name so that replacing parameter '@Note' doesn't replace part of parameter '@NoteBold'
				cmd.Parameters.OfType<DbParameter>().OrderByDescending(x => x.ParameterName.Length)
					.ForEach(x => dbQueryObj.Command=dbQueryObj.Command.Replace(x.ParameterName,"'"+SOut.String(x.Value.ToString())+"'"));
				if(IsFiltered(dbQueryObj.Command)) {
					//Hidden by the active Query Monitor filter group: run it without reporting it, and skip the stack trace and timing work.
					dbQueryObj=null;
					queryAction();
					return;
				}
				stackTrace=new StackTrace();
				dbQueryObj.MethodName=stackTrace.GetFrame(3).GetMethod().Name;
			  if(HasStackTrace || hasStackTrace) {
					dbQueryObj.StackTrace=Environment.StackTrace;
				}
				//Synchronously notify anyone that cares that the query has started to execute.
				Fire(ODEventType.QueryMonitor,dbQueryObj);
				dbQueryObj.DateTimeStart=DateTime.Now;
				//using stopwatch to time queries because the resolution of DateTime.Now is between 0.5 and 15 milliseconds which makes it not suitable for use
				//as a benchmarking tool.  See https://docs.microsoft.com/en-us/dotnet/api/system.datetime.now
				s=Stopwatch.StartNew();
				queryAction();
			}
			finally {
				if(s!=null && dbQueryObj!=null) {
					s.Stop();
					dbQueryObj.DateTimeStop=dbQueryObj.DateTimeStart.Add(s.Elapsed);
					//Synchronously notify anyone that cares that the query has finished executing.
					Fire(ODEventType.QueryMonitor,dbQueryObj);
				}
			}
		}
	}


	public class QueryMonitorEvent {
		public static event ODEventHandler Fired;
		public static void Fire(ODEventType odEventType,object tag) {
			Fired?.Invoke(new ODEventArgs(odEventType,tag));
		}
	}

}
