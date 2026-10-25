// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SyntheticData.Infrastructure;

namespace AdoptionValidation {
	///<summary>Rows an office's Open Dental 25.3.48 database carries but SyntheticData leaves out, because they change how a client starts: Open
	///Dental's update files in documentmisc, the addresses of Open Dental's servers in preferences, version history, and stored passwords that Open
	///Dental encrypted with its own key, and report rows under internal names this program does not know, which the fork migration renames. The
	///rehearsal adds them to the generated database before the baseline, so adoption has real work to do and the checks can see it done.
	///Everything here is synthetic: the "update files" are a short placeholder text, never program files, the server addresses are built from
	///the fork's blocked-host list (CodeBase.VendorDirect.HqHosts) so no host name is written in this file, the "ciphertexts" are random bytes
	///from the seed, and the report rows' internal names are made up (Open Dental's are not published).</summary>
	public static class OfficeFixture {
		///<summary>DocMiscType values of Open Dental's own rows (DocumentMiscType in OpenDentBusiness).</summary>
		public const int DocMiscUpdateFiles=0;
		public const int DocMiscUpdateFilesSegment=1;
		public const int DocMiscShareScreenExeSegment=2;
		///<summary>Number of UpdateFilesSegment rows written (FileName 0001 to 0003).</summary>
		public const int CountSegments=3;
		///<summary>Preferences the fixture fills with an address on an Open Dental host. Adoption must blank all of them.</summary>
		public static readonly string[] ArrayHqPrefNames={ "UpdateServerAddress","WebServiceHQServerURL","UpdateWebsitePath","SyntheticFuturePortalUrl" };
		///<summary>A preference holding a vendor address that is not Open Dental's. Adoption must keep it.</summary>
		public const string PREF_VENDOR_URL="SyntheticVendorUrl";
		public const string VENDOR_URL="https://vendor.example/api";
		///<summary>A preference whose text mentions an Open Dental host without being an address. Adoption must keep it.</summary>
		public const string PREF_HQ_TEXT="SyntheticHelpText";
		///<summary>The bridge the fixture stores an Open Dental-encrypted password for.</summary>
		public const string PROGRAM_NAME="XVWeb";
		///<summary>Report rows as Open Dental's own conversion to 24.4 and 25.2 might have written them: the three reports this program names itself
		///(ReportRows25_3), under internal names this program does not know (Open Dental's real names are not published), with descriptions the
		///fork migration recognises. Adoption's fork migration renames each to this program's name, InternalName only. (internal name, description,
		///category, this program's name.)</summary>
		public static readonly (string internalName,string description,int category,string nameFork)[] ArrayReportRowsRenamed={
			("ODSyntheticProdByProc","Production by Procedure",1,"ODProductionByProcedure"),
			("ODSyntheticProcsItemized","Procedures (Individual)",1,"ODProceduresIndividual"),
			("ODSyntheticInsPendingSupp","Insurance Pending Supplementals",3,"ODInsPendingSupplementals"),
		};
		///<summary>Report rows adoption must keep: one under a name this program does not know with a description of no report it names (a report a
		///later Open Dental added, say), and one under a name it knows.</summary>
		public static readonly (string internalName,string description,int category)[] ArrayReportRowsKept={
			("ODSyntheticFutureReport","Synthetic Future Report",3),
			("ODToday","Today",0),
		};

		public class Summary {
			public int UpdateFileRows;
			public int OtherDocumentMiscRows;
			public List<string> ListHqPrefs=new List<string>();
			public int UpdateHistoryRows;
			public List<string> ListCredentialRows=new List<string>();
			public int ReportRowsRenamed;
			public int ReportRowsKept;

			public override string ToString() {
				return UpdateFileRows+" Open Dental update-file rows and "+OtherDocumentMiscRows+" other documentmisc row; "+ListHqPrefs.Count
					+" preferences with Open Dental addresses, 2 that must be kept; "+UpdateHistoryRows+" updatehistory rows; "+ListCredentialRows.Count
					+" stored passwords in Open Dental's encryption ("+string.Join(", ",ListCredentialRows)+"); "+(ReportRowsRenamed+ReportRowsKept)
					+" displayreport rows: "+ReportRowsRenamed+" for the reports this program names itself under names it does not know, which the fork migration "
					+"renames ("+string.Join(", ",ArrayReportRowsRenamed.Select(x => x.internalName+" to "+x.nameFork))+"), and "+ReportRowsKept+" it keeps ("
					+string.Join(", ",ArrayReportRowsKept.Select(x => x.internalName))+").";
			}
		}

		public static Summary Apply(Db db,string database,long seed) {
			LocalOnly.EnsureWritableDatabaseName(database);
			db.Use(database);
			SchemaInfo schema=SchemaInfo.Load(db,database);
			DateTime timestamp=new DateTime(2026,9,30,18,0,0);
			DataSink sink=new DataSink(db,schema,timestamp);
			Rng rng=Rng.Derive(seed,"adoption-fixture");
			Summary summary=new Summary();
			//Open Dental's update files: one legacy UpdateFiles row and three segments, plus a share-screen segment that adoption must keep.
			string placeholder=Convert.ToBase64String(Encoding.UTF8.GetBytes("Synthetic placeholder for an update-file row. Not program files."));
			sink.Insert("documentmisc",new Row { ["DocMiscNum"]=sink.NextId("documentmisc"),["DateCreated"]=new DateTime(2026,3,2),
				["FileName"]="UpdateFiles.zip",["DocMiscType"]=DocMiscUpdateFiles,["RawBase64"]=placeholder });
			for(int i=1;i<=CountSegments;i++) {
				sink.Insert("documentmisc",new Row { ["DocMiscNum"]=sink.NextId("documentmisc"),["DateCreated"]=new DateTime(2026,3,2),
					["FileName"]=i.ToString("0000"),["DocMiscType"]=DocMiscUpdateFilesSegment,["RawBase64"]=placeholder });
			}
			summary.UpdateFileRows=1+CountSegments;
			sink.Insert("documentmisc",new Row { ["DocMiscNum"]=sink.NextId("documentmisc"),["DateCreated"]=new DateTime(2026,3,2),
				["FileName"]="synthetic{00000000-0000-0000-0000-000000000000}1",["DocMiscType"]=DocMiscShareScreenExeSegment,["RawBase64"]=placeholder });
			summary.OtherDocumentMiscRows=1;
			//Addresses of Open Dental's servers, built from the blocked-host list.
			string hostHq=CodeBase.VendorDirect.HqHosts.BlockedDomains[0];
			Dictionary<string,string> dictPrefs=new Dictionary<string,string> {
				["UpdateServerAddress"]="https://"+hostHq+":1943/WebServiceCustomerUpdates/Service1.asmx",
				["WebServiceHQServerURL"]="https://www."+hostHq+"/WebServiceMainHQ/WebServiceMainHQ.asmx",
				["UpdateWebsitePath"]="https://www."+hostHq+"/updates/",
				["SyntheticFuturePortalUrl"]="https://portal."+hostHq+"/office",
				[PREF_VENDOR_URL]=VENDOR_URL,
				[PREF_HQ_TEXT]="See https://"+hostHq+" for help",
				//The preferences an Open Dental client reads at startup (UpdateWindowShowsClassicView, WebServiceServerName, CorruptedDatabase,
				//UpdateInProgressOnComputerName), and RandomPrimaryKeys, which every insert through the business layer reads (adoption's own security
				//log entry is one), at the values an office normally has.
				["RandomPrimaryKeys"]="0",
				["UpdateWindowShowsClassicView"]="0",
				["WebServiceServerName"]="",
				["CorruptedDatabase"]="0",
				["UpdateInProgressOnComputerName"]="",
				["UpdateCode"]="",
				//A password Open Dental stored with its own key: the fork cannot read it, so it is re-entered after adoption.
				["ReportingServerMySqlPassHash"]=FakeCiphertext(rng),
			};
			foreach(KeyValuePair<string,string> pair in dictPrefs) {
				if(db.ScalarLong("SELECT COUNT(*) FROM preference WHERE PrefName="+SqlLiteral.Quote(pair.Key))>0) {
					db.Execute("UPDATE preference SET ValueString="+SqlLiteral.Quote(pair.Value)+" WHERE PrefName="+SqlLiteral.Quote(pair.Key));
					continue;
				}
				sink.Insert("preference",new Row { ["PrefNum"]=sink.NextId("preference"),["PrefName"]=pair.Key,["ValueString"]=pair.Value,["Comments"]="" });
			}
			summary.ListHqPrefs.AddRange(ArrayHqPrefNames);
			summary.ListCredentialRows.Add("preference ReportingServerMySqlPassHash");
			//Open Dental's version history.
			string[] arrayVersions={ "24.3.63.0","25.1.40.0","25.2.33.0","25.3.48.0" };
			for(int i=0;i<arrayVersions.Length;i++) {
				sink.Insert("updatehistory",new Row { ["UpdateHistoryNum"]=sink.NextId("updatehistory"),
					["DateTimeUpdated"]=new DateTime(2025,1,15).AddDays(120*i).AddHours(19),["ProgramVersion"]=arrayVersions[i],["Signature"]="" });
			}
			summary.UpdateHistoryRows=arrayVersions.Length;
			//A bridge with a password Open Dental encrypted, and a clearinghouse password in Open Dental's concealed form.
			long programNum=sink.NextId("program");
			sink.Insert("program",new Row { ["ProgramNum"]=programNum,["ProgName"]=PROGRAM_NAME,["ProgDesc"]="Synthetic imaging bridge",["Enabled"]=1 });
			sink.Insert("programproperty",new Row { ["ProgramPropertyNum"]=sink.NextId("programproperty"),["ProgramNum"]=programNum,
				["PropertyDesc"]="Username",["PropertyValue"]="synthetic.user" });
			sink.Insert("programproperty",new Row { ["ProgramPropertyNum"]=sink.NextId("programproperty"),["ProgramNum"]=programNum,
				["PropertyDesc"]="Password",["PropertyValue"]=FakeCiphertext(rng),["IsMasked"]=1,["IsHighSecurity"]=1 });
			summary.ListCredentialRows.Add("programproperty "+PROGRAM_NAME+" Password");
			//Report rows Open Dental's conversion may have written under names this program does not know, and two that must stay as they are.
			int itemOrder=90;
			foreach((string internalName,string description,int category,string nameFork) report in ArrayReportRowsRenamed) {
				InsertReportRow(sink,report.internalName,report.description,report.category,itemOrder++);
			}
			foreach((string internalName,string description,int category) report in ArrayReportRowsKept) {
				InsertReportRow(sink,report.internalName,report.description,report.category,itemOrder++);
			}
			summary.ReportRowsRenamed=ArrayReportRowsRenamed.Length;
			summary.ReportRowsKept=ArrayReportRowsKept.Length;
			sink.FlushAll();
			int countClearinghouses=db.Execute("UPDATE clearinghouse SET Password="+SqlLiteral.Quote(FakeCiphertext(rng))
				+" WHERE Description='Fictional Claims Exchange'");
			if(countClearinghouses>0) {
				summary.ListCredentialRows.Add("clearinghouse Fictional Claims Exchange");
			}
			return summary;
		}

		private static void InsertReportRow(DataSink sink,string internalName,string description,int category,int itemOrder) {
			sink.Insert("displayreport",new Row { ["DisplayReportNum"]=sink.NextId("displayreport"),["InternalName"]=internalName,["ItemOrder"]=itemOrder,
				["Description"]=description,["Category"]=category,["IsHidden"]=0,["IsVisibleInSubMenu"]=0 });
		}

		///<summary>32 random bytes in base64, without the fork's "osd1$" prefix: what an Open Dental ciphertext looks like to the fork.</summary>
		private static string FakeCiphertext(Rng rng) {
			return Convert.ToBase64String(rng.Bytes(32));
		}
	}
}
