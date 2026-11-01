// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DataConnectionBase;
using OpenPmsDentalBusiness;

namespace DevDbTool {
	///<summary>Turns the scratch database of --export-init-sql into the new database an office starts from: removes the rows that belong to the
	///PC or the run that built it, and the seed dump's fictional practice, providers and schedules, and writes the adoption record of a new
	///database. Data only: the schema is left exactly as the fork migrations made it.</summary>
	public class InitSqlScrub {
		///<summary>Replaces every timestamp the build run wrote (the ON UPDATE stamps the convert scripts touched, the update history row, dates
		///the convert scripts put into preferences), so the same source always gives the same file.</summary>
		public static readonly DateTime FixedDateTime=new DateTime(2026,1,1,0,0,0);
		///<summary>DocPath as the seed dump has it.</summary>
		public const string SeedDocPath=@"C:\OpenDentImages\";
		///<summary>The ForkAdoptedFrom value of a new database, for this build's latest fork schema level. "new=1" says the database was created from
		///the file, not copied from an Open Dental database. "source" keeps the line its rows come from, the fork's own 24.3.63 compatibility line
		///that the seed dump is converted on, because the fork migration steps decide what a database still needs from it
		///(Adoption.ParseAdoptedFromSource): a step written later for "databases from before" a version then treats a new database like any other
		///database of that line. "level" is the fork schema level the file was made at. "adopted" ends the record: the version guard and the adoption
		///tools read a record with it as a finished adoption (ForkDbState.IsAdoptionRecordFinished). No times: the file must not change between
		///runs.</summary>
		public static string GetAdoptedFromValue(string level) {
			return "new=1;source="+ForkVersions.CompatVersion.ToString(4)+";level="+level+";adopted="+InitSqlExport.RepoPath;
		}

		///<summary>Tables emptied, and why.</summary>
		public static readonly Dictionary<string,string> DictTablesEmptied=new Dictionary<string,string> {
			{ "computer","workstations of the PCs that used the seed dump and of this run" },
			{ "computerpref","per-workstation settings of the same PCs" },
			{ "securitylog","the audit trail of the seed dump's history and of this run (with computer names)" },
			{ "securityloghash","the hashes of those audit rows" },
			{ "signalod","cache refresh signals of this run" },
			{ "alertitem","notices of Open Dental's eServices from the seed dump" },
			{ "ehrmeasureevent","an event logged when the seed dump was made" },
			{ "schedule","the seed dump's 2007 schedules of its fictional providers" },
			{ "scheduleop","operatories of those schedules" },
			{ "zipcode","the seed dump's fictional zip codes" },
			{ "emailhostingtemplate","Open Dental's mass e-mail templates (an eService this program removed), which load images from Open Dental's web "
				+"site" },
		};
		///<summary>Preferences with the seed dump's fictional practice name, address and phone numbers. Blanked; the office enters its own at the
		///first logon.</summary>
		public static readonly string[] ArrayPracticePrefsBlanked={
			"PracticeTitle","PracticeAddress","PracticeAddress2","PracticeCity","PracticeST","PracticeZip","PracticePhone","PracticeFax",
			"PracticeBankNumber","PracticeBillingPhone","PracticeBillingAddress","PracticeBillingAddress2","PracticeBillingCity","PracticeBillingST",
			"PracticeBillingZip","PracticePayToPhone","PracticePayToAddress","PracticePayToAddress2","PracticePayToCity","PracticePayToST",
			"PracticePayToZip",
		};
		///<summary>Identifiers of the seed dump's fictional providers (social security, license, DEA, NPI and payer numbers). Blanked.</summary>
		public static readonly string[] ArrayProviderColumnsBlanked={
			"FName","MI","Suffix","SSN","StateLicense","DEANum","BlueCrossID","MedicaidID","NationalProvID","CanadianOfficeNum","StateRxID",
			"StateWhereLicensed","EcwID","CustomID",
		};
		public static readonly string[] ArrayProviderClinicColumnsBlanked={ "DEANum","StateLicense","StateRxID","StateWhereLicensed","CareCreditMerchantId" };

		private readonly string _database;
		private readonly DateTime _dateTimeWindowStart;
		private readonly DateTime _dateTimeWindowEnd;
		public readonly List<string> ListLog=new List<string>();

		///<summary>dateTimeServerStart and dateTimeServerEnd: the server's clock when the build started and when it ended. Timestamps between them (with
		///a day of margin each way, for code that writes UTC) were written by the run.</summary>
		public InitSqlScrub(string database,DateTime dateTimeServerStart,DateTime dateTimeServerEnd) {
			_database=database;
			_dateTimeWindowStart=dateTimeServerStart.AddDays(-1);
			_dateTimeWindowEnd=dateTimeServerEnd.AddDays(1);
		}

		public void Run() {
			EmptyTables();
			BlankProviders();
			BlankPracticePrefs();
			SetPrefs();
			ReplaceRunTimestamps();
			ReplaceRunDatesInPrefs();
		}

		private void EmptyTables() {
			foreach(KeyValuePair<string,string> pair in DictTablesEmptied) {
				if(!TableExists(pair.Key)) {
					continue;
				}
				long count=PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM `"+pair.Key+"`"));
				if(count==0) {
					continue;
				}
				DataCore.NonQ("DELETE FROM `"+pair.Key+"`");
				ListLog.Add("Emptied "+pair.Key+" ("+count+" rows): "+pair.Value+".");
			}
			//The seed's birthday reminder rule pointed at one of the removed templates.
			if(ColumnExists("apptreminderrule","EmailHostingTemplateNum")) {
				long count=DataCore.NonQ("UPDATE apptreminderrule SET EmailHostingTemplateNum=0 WHERE EmailHostingTemplateNum<>0");
				if(count>0) {
					ListLog.Add("Reminder rules no longer point at a removed mass e-mail template ("+count+" rows).");
				}
			}
		}

		private void BlankProviders() {
			List<string> listSets=ArrayProviderColumnsBlanked.Where(x => ColumnExists("provider",x)).Select(x => "`"+x+"`=''").ToList();
			//Generic names instead of the fictional ones: "Dentist" for the dentist, "Hygienist" for the secondary (hygienist) provider.
			listSets.Insert(0,"LName=IF(IsSecondary=1,'Hygienist','Dentist')");
			long count=DataCore.NonQ("UPDATE provider SET "+string.Join(",",listSets));
			ListLog.Add("Providers: generic names, identifiers blanked ("+count+" rows).");
			listSets=ArrayProviderClinicColumnsBlanked.Where(x => ColumnExists("providerclinic",x)).Select(x => "`"+x+"`=''").ToList();
			if(listSets.Count>0 && TableExists("providerclinic")) {
				count=DataCore.NonQ("UPDATE providerclinic SET "+string.Join(",",listSets));
				ListLog.Add("Provider clinic rows: identifiers blanked ("+count+" rows).");
			}
		}

		private void BlankPracticePrefs() {
			List<string> listBlanked=new List<string>();
			foreach(string prefName in ArrayPracticePrefsBlanked) {
				string value=DevDatabase.GetPrefRaw(prefName);
				if(string.IsNullOrEmpty(value)) {
					continue;
				}
				DataCore.NonQ("UPDATE preference SET ValueString='' WHERE PrefName='"+POut.String(prefName)+"'");
				listBlanked.Add(prefName);
			}
			ListLog.Add("Practice preferences blanked: "+(listBlanked.Count==0 ? "none" : string.Join(", ",listBlanked))+".");
		}

		private void SetPrefs() {
			DataCore.NonQ("UPDATE preference SET ValueString='"+POut.String(SeedDocPath)+"' WHERE PrefName='DocPath'");
			string level=ForkMigrationHandOff.Runner.LatestLevel;
			string adoptedFrom=GetAdoptedFromValue(level);
			ForkVersions.SetMarker(ForkVersions.PREF_FORK_ADOPTED_FROM,adoptedFrom);
			ListLog.Add("DocPath "+SeedDocPath+"; ForkAdoptedFrom "+adoptedFrom+".");
			long count=DataCore.NonQ("UPDATE userod SET IsPasswordResetRequired=1 WHERE UserName='Admin' AND COALESCE(Password,'')=''");
			ListLog.Add("Admin must set a password at the first logon (IsPasswordResetRequired, "+count+" row).");
		}

		///<summary>One UPDATE per table that sets every date, datetime and timestamp column of a row the run stamped, so the ON UPDATE timestamps of the
		///row are assigned too and do not move again.</summary>
		private void ReplaceRunTimestamps() {
			string command="SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"+POut.String(_database)+"' "
				+"AND DATA_TYPE IN ('date','datetime','timestamp') ORDER BY TABLE_NAME,ORDINAL_POSITION";
			DataTable table=DataCore.GetTable(command);
			string start=_dateTimeWindowStart.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
			string end=_dateTimeWindowEnd.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
			foreach(IGrouping<string,DataRow> grouping in table.Rows.Cast<DataRow>().GroupBy(x => x["TABLE_NAME"].ToString())) {
				List<string> listInWindow=new List<string>();
				List<string> listSets=new List<string>();
				foreach(DataRow row in grouping) {
					string column="`"+row["COLUMN_NAME"].ToString()+"`";
					string fixedValue=row["DATA_TYPE"].ToString()=="date"
						? FixedDateTime.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)
						: FixedDateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
					string inWindow=column+" BETWEEN '"+start+"' AND '"+end+"'";
					listInWindow.Add("("+inWindow+")");
					listSets.Add(column+"=IF("+inWindow+",'"+fixedValue+"',"+column+")");
				}
				string where=string.Join(" OR ",listInWindow);
				long count=PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM `"+grouping.Key+"` WHERE "+where));
				if(count==0) {
					continue;
				}
				DataCore.NonQ("UPDATE `"+grouping.Key+"` SET "+string.Join(",",listSets)+" WHERE "+where);
				ListLog.Add("Timestamps of this run set to "+FixedDateTime.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+" in "+grouping.Key
					+" ("+count+" rows).");
			}
		}

		///<summary>Preferences that the convert scripts set to a date of the run (AgingServiceTimeDue: today at 02:00). The date part becomes the fixed
		///date; the time of day, which is what the preference means, stays.</summary>
		private void ReplaceRunDatesInPrefs() {
			Regex regex=new Regex(@"^(\d{4}-\d{2}-\d{2})((?: \d{2}:\d{2}:\d{2})?)$");
			DataTable table=DataCore.GetTable("SELECT PrefNum,PrefName,ValueString FROM preference ORDER BY PrefNum");
			foreach(DataRow row in table.Rows) {
				string value=row["ValueString"].ToString();
				Match match=regex.Match(value);
				if(!match.Success
					|| !DateTime.TryParseExact(match.Groups[1].Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime date)
					|| date<_dateTimeWindowStart.Date || date>_dateTimeWindowEnd.Date)
				{
					continue;
				}
				string valueNew=FixedDateTime.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+match.Groups[2].Value;
				DataCore.NonQ("UPDATE preference SET ValueString='"+POut.String(valueNew)+"' WHERE PrefNum="+POut.Long(PIn.Long(row["PrefNum"].ToString())));
				ListLog.Add("Preference "+row["PrefName"]+": "+value+" became "+valueNew+".");
			}
		}

		///<summary>Throws when the scrubbed database is not what a new database must be: patient rows, an enabled program link, a table that is not
		///MyISAM, or version markers the startup guard would not open with every step applied.</summary>
		public void CheckDatabase() {
			List<string> listProblems=new List<string>();
			string command="SELECT TABLE_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"+POut.String(_database)+"' AND COLUMN_NAME='PatNum' "
				+"ORDER BY TABLE_NAME";
			foreach(DataRow row in DataCore.GetTable(command).Rows) {
				string tableName=row[0].ToString();
				long count=PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM `"+tableName+"` WHERE PatNum<>0"));
				if(count>0) {
					listProblems.Add(tableName+" has "+count+" row(s) of a patient.");
				}
			}
			foreach(string tableName in new[] { "patient","appointment","computer","computerpref","securitylog" }) {
				if(TableExists(tableName) && PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM `"+tableName+"`"))>0) {
					listProblems.Add(tableName+" is not empty.");
				}
			}
			DataTable tablePrograms=DataCore.GetTable("SELECT ProgName FROM program WHERE Enabled<>0 ORDER BY ProgName");
			foreach(DataRow row in tablePrograms.Rows) {
				listProblems.Add("Program link "+row[0]+" is enabled; a new database has every program link off.");
			}
			command="SELECT TABLE_NAME,ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+POut.String(_database)+"' "
				+"AND (TABLE_TYPE<>'BASE TABLE' OR ENGINE<>'MyISAM') ORDER BY TABLE_NAME";
			foreach(DataRow row in DataCore.GetTable(command).Rows) {
				listProblems.Add("Table "+row[0]+" is "+row[1]+", not a MyISAM table. Set default-storage-engine=MyISAM in my.ini.");
			}
			ForkDbState forkDbState=ForkVersions.ReadState();
			ForkGuardResult forkGuardResult=ForkVersionGuard.Evaluate(forkDbState);
			if(forkGuardResult.Action!=ForkGuardAction.ProceedFork || forkGuardResult.HasPendingMigrations) {
				listProblems.Add("The startup version guard says "+forkGuardResult.Action+" (pending: "+string.Join(", ",forkGuardResult.ListPendingLevels)
					+"): "+forkGuardResult.Message);
			}
			if(!forkDbState.IsAdoptionRecordFinished) {
				listProblems.Add("The adoption record is not finished: "+forkDbState.ForkAdoptedFrom);
			}
			if(listProblems.Count>0) {
				throw new ApplicationException("The scrubbed database is not a clean new database:\r\n  "+string.Join("\r\n  ",listProblems));
			}
			ListLog.Add("Checked: no patient rows, every program link off, every table MyISAM, the startup guard opens it with no step pending.");
		}

		///<summary>What the generated text must not contain: this PC's names, the run's dates (a timestamp the scrub missed) and Open Dental's hosts
		///(CodeBase.VendorDirect.HqHosts). Case-insensitive. Returns one line per find, with the line number.</summary>
		public static List<string> FindLeftovers(string text,List<string> listForbidden,List<string> listRunDates) {
			List<string> listProblems=new List<string>();
			foreach(string value in listForbidden.Concat(listRunDates)) {
				int index=text.IndexOf(value,StringComparison.OrdinalIgnoreCase);
				if(index>=0) {
					listProblems.Add("'"+value+"' "+Where(text,index));
				}
			}
			foreach(string domain in CodeBase.VendorDirect.HqHosts.BlockedDomains) {
				Match match=Regex.Match(text,@"(?i)(?<![a-z0-9-])(?:[a-z0-9-]+\.)*"+Regex.Escape(domain)+"(?![a-z0-9-])");
				if(match.Success) {
					listProblems.Add("Open Dental host '"+match.Value+"' "+Where(text,match.Index));
				}
			}
			return listProblems;
		}

		private static string Where(string text,int index) {
			int line=1;
			for(int i=0;i<index;i++) {
				if(text[i]=='\n') {
					line++;
				}
			}
			int indexLineStart=text.LastIndexOf('\n',index)+1;
			int indexSnippet=Math.Max(indexLineStart,index-60);
			return "at line "+line+": ..."+text.Substring(indexSnippet,Math.Min(160,text.Length-indexSnippet));
		}

		private bool TableExists(string tableName) {
			return DataCore.GetScalar("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+POut.String(_database)+"' AND TABLE_NAME='"
				+POut.String(tableName)+"'")!="0";
		}

		private bool ColumnExists(string tableName,string columnName) {
			return DataCore.GetScalar("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='"+POut.String(_database)+"' AND TABLE_NAME='"
				+POut.String(tableName)+"' AND COLUMN_NAME='"+POut.String(columnName)+"'")!="0";
		}
	}
}
