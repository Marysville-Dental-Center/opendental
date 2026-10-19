using CodeBase;
using DataConnectionBase;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenDentBusiness {
	///<summary>Perform actions in different database contexts. The helpers for Open Dental HQ's own databases (bugs, customers, manual publisher,
	///web chat, triage, eServices and others) were removed.</summary>
	public class DataAction {
		#region Helpers to run directly on a given db.
		#region Actions
		///<summary>Perform the given action as a Middle Tier client using OpenDentalServerMockIIS.</summary>
		public static void RunMiddleTierMock(Action a) {
			MiddleTierRole remotingRolePrevious=RemotingClient.MiddleTierRole;
			OpenDentBusiness.WebServices.OpenDentalServerMockIIS mockPrevious=OpenDentBusiness.WebServices.OpenDentalServerProxy.MockOpenDentalServerCur;
			if(mockPrevious==null) {
				OpenDentBusiness.WebServices.OpenDentalServerProxy.MockOpenDentalServerCur=new OpenDentBusiness.WebServices.OpenDentalServerMockIIS();
			}
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientMT;
			try {
				a();
			}
			finally {
				OpenDentBusiness.WebServices.OpenDentalServerProxy.MockOpenDentalServerCur=mockPrevious;
				RemotingClient.MiddleTierRole=remotingRolePrevious;
			}
		}		

		///<summary>Perform the given action in the context of the dental office db.</summary>
		public static void RunPractice(Action a) {
			Run(a,ConnectionNames.DentalOffice);
		}

		#endregion

		#region Funcs
		///<summary>Perform the given function in the context of the dental office db.</summary>
		public static T GetPractice<T>(Func<T> fn) {
			return GetT(fn,ConnectionNames.DentalOffice);
		}

		#endregion
		#endregion

		#region Helpers to return typed data.
		///<summary>Perform the given function in the context of the given connectionName db and return a DataTable. Typed extension of GetT.</summary>
		public static DataTable GetDataTable(Func<DataTable> fn,ConnectionNames connectionName) {
			return GetT<DataTable>(fn,connectionName);
		}

		///<summary>Perform the given function in the context of the given connectionName db and return an int. Typed extension of GetT.</summary>
		public static int GetInt(Func<int> fn,ConnectionNames connectionName) {
			return GetT<int>(fn,connectionName);
		}

		///<summary>Perform the given function in the context of the given connectionName db and return a long. Typed extension of GetT.</summary>
		public static long GetLong(Func<long> fn,ConnectionNames connectionName) {
			return GetT<long>(fn,connectionName);
		}

		///<summary>Perform the given function in the context of the given connectionName db and return a string. Typed extension of GetT.</summary>
		public static string GetString(Func<string> fn,ConnectionNames connectionName) {
			//String is a reference type and will be set to null if the method happens to fail. Correct that to empty string since OD does not typically expect string to be null.
			return GetT<string>(fn,connectionName)??"";
		}
		#endregion

		#region Run and Get
		///<summary>Perform the given action in the context of the given connectionName db.</summary>
		public static void Run(Action a,ConnectionNames connectionName) {
			GetT(new Func<object>(() => { a(); return null; }),connectionName);
		}

		///<summary>Perform the given action in the context of the given connectionString db.</summary>
		public static void Run(Action a,string connectionString,DatabaseType dbType=DatabaseType.MySql) {
			GetT(new Func<object>(() => { a(); return null; }),connectionString,dbType);
		}
		
		///<summary>Perform the given action in the context of the given connectionString db.</summary>
		public static void Run(Action a,string server,string db,string user,string password,string userLow,string passLow,DatabaseType dbType=DatabaseType.MySql) {
			GetT(new Func<object>(() => { a(); return null; }),server,db,user,password,userLow,passLow,dbType);
		}

		///<summary>Perform the given function in the context of the given connectionString db and return a T.</summary>
		public static T GetT<T>(Func<T> fn,string connectionString,DatabaseType dbType=DatabaseType.MySql) {
			T ret=default(T);
			ExecuteThread(new ODThread((o) => {
				using(DataConnection dataConn=new DataConnection()) {
					dataConn.SetDbT(connectionString,"",dbType);
					ret=fn();
				}
			}));
			return ret;
		}

		///<summary>Perform the given function in the context of the given connectionString db and return a T.</summary>
		public static T GetT<T>(Func<T> fn,string server,string db,string user,string password,string userLow,string passLow,DatabaseType dbType=DatabaseType.MySql) {
			T ret=default(T);
			ExecuteThread(new ODThread((o) => {
				using(DataConnection dataConn=new DataConnection()) {
					dataConn.SetDbT(server,db,user,password,userLow,passLow,dbType,true);
					ret=fn();
				}
			}));
			return ret;
		}

		///<summary>Perform the given function in the context of the given connectionName db and return a T.</summary>
		public static T GetT<T>(Func<T> fn,ConnectionNames connectionName) {
			T ret=default(T);
			ExecuteThread(new ODThread((o) => {
				using(DataConnection dataConn=new DataConnection()) {
					ConnectionStore.SetDbT(connectionName,dataConn:dataConn);
					ret=fn();
				}
			}));
			return ret;
		}

		///<summary>Adds an exception handler to the thread passed in, starts the thread, and then waits until the thread has finished executing.
		///This is just a helper method that will throw any exception that occurs within the thread on the parent (usually main) thread.
		///Throws exceptions.</summary>
		private static void ExecuteThread(ODThread thread) {
			Exception eFinal=null;
			thread.AddExceptionHandler((e) => { eFinal=e; });
			thread.Start(true);
			//This is intended to be a blocking call so give the action as long as it needs to complete.
			thread.Join(Timeout.Infinite);
			if(eFinal!=null) { //We are back on the main thread so it is safe to throw.
				MiscUtils.PreserveExceptionInfoAndThrow(eFinal);
			}
		}
		#endregion
	}
}
