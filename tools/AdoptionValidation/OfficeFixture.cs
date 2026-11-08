// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SyntheticData.Infrastructure;

namespace AdoptionValidation {
	///<summary>Rows an office's Open Dental 25.3.48 database carries but SyntheticData leaves out, because they change how a client starts: Open
	///Dental's update files in documentmisc, the addresses of Open Dental's servers in preferences, version history, and stored passwords that Open
	///Dental encrypted with its own key, and report rows under internal names this program does not know, which the fork migration renames. Also the
	///rows the Open Dental 25.4 and 26.1 steps convert: a PhoneExtension grant under its 25.3 number (272), which they move to 274, Blockouts grants
	///(one of them twice, one with a date limit), from which they grant BlockoutAdd and BlockoutDelete, and a claim payment note that is NULL, which
	///they make empty; and the FQHC Dental Sealant Measure with a report grant and a Public Health report after it, among which the UDS+ step adds the
	///25.4 UDS+ report's row and to which it copies the grant; and the ten InsHist*Codes preferences of Insurance History's fixed rows, which the
	///Insurance History step moves into code groups, with the debridement group made the office's own (no fixed type) so that the step also adds one;
	///and the discontinued DXC Patient Credit Score program link, enabled and with two toolbar buttons, which the B-SHELL step disables and whose
	///buttons it deletes (the XVWeb link's button is kept); and settings under Open Dental's own names that this program reads as they are
	///(ArrayOdSettingPrefs).
	///The rehearsal adds them to the generated database before the baseline, so adoption has real work to do and the checks can see it done.
	///Everything here is synthetic: the "update files" are a short placeholder text, never program files, the server addresses are built from
	///the fork's blocked-host list (CodeBase.VendorDirect.HqHosts) so no host name is written in this file, the "ciphertexts" are random bytes
	///from the seed, and the report rows' internal names are made up (Open Dental's are not published).</summary>
	public static class OfficeFixture {
		///<summary>DocMiscType values of Open Dental's own rows (DocumentMiscType in OpenPmsDentalBusiness).</summary>
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
		///<summary>Public Health rows under Open Dental's own names, which this program knows: the FQHC Dental Sealant Measure, which the third user
		///group of ArrayGroupsFixture may run (a Reports grant with its DisplayReportNum as FKey), and a report after it. Adoption's fork migration adds
		///the row of the Open Dental 25.4 UDS+ report right after the sealant measure (ReportRowUdsPlusStep, M2-UDS), moves the later row down one place
		///(ItemOrder only) and gives that group the Reports permission for the new row. (internal name, description.)</summary>
		public static readonly (string internalName,string description)[] ArrayReportRowsPublicHealth={
			(OpenPmsDentalBusiness.ForkMigrations.Features.ReportRowUdsPlus.InternalNameSealant,"FQHC Dental Sealant Measure"),
			("ODRawPopulationData","Raw Population Data"),
		};
		///<summary>DisplayReportCategory.PublicHealth.</summary>
		public const int CategoryPublicHealth=4;
		///<summary>EnumPermType.Reports, the same number in 25.3 and 26.1.</summary>
		public const int PermReports=22;
		///<summary>EnumPermType.Blockouts, the same number in 25.3 and 26.1.</summary>
		public const int PermBlockouts=13;
		///<summary>PhoneExtension as Open Dental 25.3 stored it (Seed26_1.PERM_PHONE_EXTENSION_25_3); Open Dental's Set All grants it too.</summary>
		public const int PermPhoneExtension25_3=272;
		///<summary>The user groups of SyntheticData that get the fixture's grants: PhoneExtension (272) to the first, Blockouts to the other two
		///(twice to the second, as a duplicated row; with a 30-day limit to the third).</summary>
		public static readonly string[] ArrayGroupsFixture={ "Synthetic Administrators","Synthetic Front Desk","Synthetic Clinical" };
		///<summary>What the 25.4 permission split must do with the fixture's grants: one PhoneExtension grant moved, BlockoutAdd and BlockoutDelete
		///granted to two user groups each.</summary>
		public const int CountPhoneExtensionGrants=1;
		public const int CountBlockoutGroups=2;
		///<summary>The ten InsHist*Codes preferences as an office's Open Dental 25.3.48 database holds them (the codes of Insurance History's fixed
		///rows), with SyntheticData's codes. Adoption's Insurance History step (InsHistCodeGroupsStep, M2-HIST) marks the generated code group of each
		///category Show In History; the groups already hold every code but INSHIST_CODE_ADDED, and the four perio quadrants (two codes) share the SRP
		///group. (preference name, value.)</summary>
		public static readonly (string prefName,string value)[] ArrayInsHistPrefs={
			("InsHistBWCodes","Z0272,Z0274"),
			("InsHistPanoCodes","Z0330,Z0210"),
			("InsHistExamCodes","Z0120,Z0140,Z0150"),
			("InsHistProphyCodes","Z1110"),
			("InsHistPerioURCodes","Z4341"),
			("InsHistPerioULCodes","Z4341"),
			("InsHistPerioLRCodes","Z4342"),
			("InsHistPerioLLCodes","Z4342"),
			("InsHistPerioMaintCodes","Z4910,"+INSHIST_CODE_ADDED),
			("InsHistDebridementCodes","Z4355"),
		};
		///<summary>A perio maintenance code the preference names and neither the code group nor the procedure code list has: the step appends it to the
		///group's codes.</summary>
		public const string INSHIST_CODE_ADDED="Z4920";
		///<summary>The fixture makes SyntheticData's full mouth debridement group the office's own (CodeGroupFixed None), so the step adds a group for
		///that category, after every other group.</summary>
		public const string INSHIST_GROUP_ADDED="Full Mouth Debridement";
		///<summary>What the Insurance History step must do with the fixture: mark six code groups (bitewings, pano, exams, prophy, SRP with four
		///quadrant rows, perio maintenance) and add one.</summary>
		public const int CountInsHistGroupsMarked=6;
		public const int CountInsHistGroupsAdded=1;
		///<summary>The DXC Patient Credit Score link as an office's Open Dental 25.3.48 database may still hold it (what Open Dental's own 25.2
		///conversion did to the row is not public, INFERRED): enabled, with a button on these toolbars (EnumToolBar: Account module, main toolbar).
		///Adoption's DXC step (DxcCreditScoreRemovedStep, B-SHELL) sets its Enabled to 0 and deletes both buttons; the XVWeb link and its button on the
		///Imaging module toolbar are kept.</summary>
		public static readonly int[] ArrayDxcToolBars={ 0,7 };
		public const string DXC_PROG_DESC="DXC Patient Credit Score";
		///<summary>EnumToolBar.ImagingModule: the toolbar of the XVWeb link's button, which adoption keeps.</summary>
		public const int TOOLBAR_XVWEB=3;
		public static int CountDxcButtons {
			get { return ArrayDxcToolBars.Length; }
		}
		///<summary>Settings Open Dental 24.4 to 25.3 added that this program reads under Open Dental's own names (docs/port-notes/SWMAP.md), with
		///made-up values: payment plans included in the global lock (so this program's "not affected" box is not ticked), the Recession wording on,
		///provider time on the operatory background on, and a read delay that is switched off. Adoption keeps these rows as they are; migration-preview
		///lists them with what this program shows. (preference name, value.)</summary>
		public static readonly (string prefName,string value)[] ArrayOdSettingPrefs={
			("PayPlansIncludeInGlobalLock","1"),
			("PerioRecessionInsteadOfGM","1"),
			("ApptProvbarShowOnOps","1"),
			("ReplicationWriteReadDelayEnabled","0"),
			("ReplicationWriteReadDelayMS","250"),
		};

		public class Summary {
			public int UpdateFileRows;
			public int OtherDocumentMiscRows;
			public List<string> ListHqPrefs=new List<string>();
			public int UpdateHistoryRows;
			public List<string> ListCredentialRows=new List<string>();
			public int ReportRowsRenamed;
			public int ReportRowsKept;
			public int ReportRowsPublicHealth;
			public int ReportGrantsSealant;
			public int GroupPermRows;
			public int ClaimPaymentNotesNull;
			public int InsHistPrefs;
			public int CodeGroupsUnfixed;
			///<summary>The DXC Patient Credit Score program row and its toolbar buttons, and the XVWeb link's button.</summary>
			public long DxcProgramNum;
			public int DxcButtons;
			public int OtherButtons;

			public override string ToString() {
				return UpdateFileRows+" Open Dental update-file rows and "+OtherDocumentMiscRows+" other documentmisc row; "+ListHqPrefs.Count
					+" preferences with Open Dental addresses, 2 that must be kept; "+UpdateHistoryRows+" updatehistory rows; "+ListCredentialRows.Count
					+" stored passwords in Open Dental's encryption ("+string.Join(", ",ListCredentialRows)+"); "+(ReportRowsRenamed+ReportRowsKept
					+ReportRowsPublicHealth)+" displayreport rows: "+ReportRowsRenamed+" for the reports this program names itself under names it does not know, which the fork migration "
					+"renames ("+string.Join(", ",ArrayReportRowsRenamed.Select(x => x.internalName+" to "+x.nameFork))+"), and "+ReportRowsKept+" it keeps ("
					+string.Join(", ",ArrayReportRowsKept.Select(x => x.internalName))+"), and "+ReportRowsPublicHealth+" Public Health rows ("
					+string.Join(", ",ArrayReportRowsPublicHealth.Select(x => x.internalName))+") among which the 25.4 UDS+ step adds its row, with "+ReportGrantsSealant
					+" Reports grant for the sealant measure it copies; "+GroupPermRows+" grouppermission rows the 25.4 permission split converts ("
					+CountPhoneExtensionGrants+" PhoneExtension under its 25.3 number "+PermPhoneExtension25_3+", Blockouts for "+CountBlockoutGroups+" user groups, one "
					+"of them twice); "+ClaimPaymentNotesNull+" claim payment note set to NULL, which the 26.1 step makes empty; "+InsHistPrefs+" InsHist*Codes preferences, which the Insurance History step "
					+"moves into code groups, and "+CodeGroupsUnfixed+" full mouth debridement code group made the office's own, so that the step adds a group for it; "
					+"the discontinued DXC Patient Credit Score program link (ProgramNum "+DxcProgramNum+") enabled with "+DxcButtons+" toolbar buttons, which the "
					+"B-SHELL step disables and deletes, and "+OtherButtons+" XVWeb toolbar button it keeps.";
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
			foreach((string prefName,string value) pref in ArrayOdSettingPrefs) {
				dictPrefs[pref.prefName]=pref.value;
			}
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
			sink.Insert("toolbutitem",new Row { ["ToolButItemNum"]=sink.NextId("toolbutitem"),["ProgramNum"]=programNum,["ToolBar"]=TOOLBAR_XVWEB,
				["ButtonText"]="XVWeb" });
			summary.OtherButtons=1;
			//The discontinued DXC Patient Credit Score link, still enabled and on two toolbars (B-SHELL's step disables it and deletes the buttons).
			summary.DxcProgramNum=sink.NextId("program");
			sink.Insert("program",new Row { ["ProgramNum"]=summary.DxcProgramNum,["ProgName"]=OpenPmsDentalBusiness.ForkMigrations.FeatureSteps.DxcCreditScoreRemoved.PROG_NAME,
				["ProgDesc"]=DXC_PROG_DESC,["Enabled"]=1 });
			foreach(int toolBar in ArrayDxcToolBars) {
				sink.Insert("toolbutitem",new Row { ["ToolButItemNum"]=sink.NextId("toolbutitem"),["ProgramNum"]=summary.DxcProgramNum,["ToolBar"]=toolBar,
					["ButtonText"]="Credit Score" });
				summary.DxcButtons++;
			}
			//Report rows Open Dental's conversion may have written under names this program does not know, and two that must stay as they are.
			int itemOrder=90;
			foreach((string internalName,string description,int category,string nameFork) report in ArrayReportRowsRenamed) {
				InsertReportRow(sink,report.internalName,report.description,report.category,itemOrder++);
			}
			foreach((string internalName,string description,int category) report in ArrayReportRowsKept) {
				InsertReportRow(sink,report.internalName,report.description,report.category,itemOrder++);
			}
			//The Public Health rows the UDS+ step places its row among: the sealant measure, then a report after it.
			long displayReportNumSealant=0;
			foreach((string internalName,string description) report in ArrayReportRowsPublicHealth) {
				long displayReportNum=InsertReportRow(sink,report.internalName,report.description,CategoryPublicHealth,itemOrder++);
				if(displayReportNumSealant==0) {
					displayReportNumSealant=displayReportNum;
				}
			}
			summary.ReportRowsRenamed=ArrayReportRowsRenamed.Length;
			summary.ReportRowsKept=ArrayReportRowsKept.Length;
			summary.ReportRowsPublicHealth=ArrayReportRowsPublicHealth.Length;
			//Grants the 25.4 permission split converts (Seed26_1): PhoneExtension under its 25.3 number, and Blockouts, from which BlockoutAdd and
			//BlockoutDelete are granted; the duplicated row gets one grant, and the date limit is copied.
			long[] arrayGroups=ArrayGroupsFixture.Select(x => db.ScalarLong("SELECT UserGroupNum FROM usergroup WHERE Description="+SqlLiteral.Quote(x)
				+" ORDER BY UserGroupNum LIMIT 1")).ToArray();
			if(arrayGroups.Any(x => x==0)) {
				throw new ApplicationException("The synthetic user groups "+string.Join(", ",ArrayGroupsFixture)+" were not found.");
			}
			foreach((long userGroupNum,int permType,int newerDays) grant in new[] { (arrayGroups[0],PermPhoneExtension25_3,0),(arrayGroups[1],PermBlockouts,0),
				(arrayGroups[1],PermBlockouts,0),(arrayGroups[2],PermBlockouts,30) })
			{
				sink.Insert("grouppermission",new Row { ["GroupPermNum"]=sink.NextId("grouppermission"),["NewerDate"]=new DateTime(1,1,1),
					["NewerDays"]=grant.newerDays,["UserGroupNum"]=grant.userGroupNum,["PermType"]=grant.permType,["FKey"]=0L });
				summary.GroupPermRows++;
			}
			//The sealant measure for the third group only; the UDS+ step gives that group the same grant for its new row.
			sink.Insert("grouppermission",new Row { ["GroupPermNum"]=sink.NextId("grouppermission"),["NewerDate"]=new DateTime(1,1,1),["NewerDays"]=0,
				["UserGroupNum"]=arrayGroups[2],["PermType"]=PermReports,["FKey"]=displayReportNumSealant });
			summary.ReportGrantsSealant=1;
			sink.FlushAll();
			//A claim payment note left NULL (the 25.3.48 column allows it); the 26.1 step makes it empty. SecDateTEdit is kept, so every run starts from
			//the same database (X1).
			summary.ClaimPaymentNotesNull=db.Execute("UPDATE claimpayment SET Note=NULL,SecDateTEdit=SecDateTEdit WHERE Note='' ORDER BY ClaimPaymentNum LIMIT 1");
			//Insurance History's fixed rows (24.3 to 25.3): the ten preferences an office has set, which the Insurance History step moves into code
			//groups (M2-HIST). The debridement group becomes the office's own, so the step creates the category's group.
			foreach((string prefName,string value) pref in ArrayInsHistPrefs) {
				if(db.ScalarLong("SELECT COUNT(*) FROM preference WHERE PrefName="+SqlLiteral.Quote(pref.prefName))>0) {
					db.Execute("UPDATE preference SET ValueString="+SqlLiteral.Quote(pref.value)+" WHERE PrefName="+SqlLiteral.Quote(pref.prefName));
				}
				else {
					db.Execute("INSERT INTO preference (PrefNum,PrefName,ValueString,Comments) VALUES ("+(db.ScalarLong("SELECT COALESCE(MAX(PrefNum),0)+1 FROM preference"))
						.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+SqlLiteral.Quote(pref.prefName)+","+SqlLiteral.Quote(pref.value)+",'')");
				}
				summary.InsHistPrefs++;
			}
			summary.CodeGroupsUnfixed=db.Execute("UPDATE codegroup SET CodeGroupFixed="+((int)OpenPmsDentalBusiness.EnumCodeGroupFixed.None).ToString(System.Globalization.CultureInfo.InvariantCulture)
				+" WHERE CodeGroupFixed="+((int)OpenPmsDentalBusiness.EnumCodeGroupFixed.FMDebride).ToString(System.Globalization.CultureInfo.InvariantCulture));
			int countClearinghouses=db.Execute("UPDATE clearinghouse SET Password="+SqlLiteral.Quote(FakeCiphertext(rng))
				+" WHERE Description='Fictional Claims Exchange'");
			if(countClearinghouses>0) {
				summary.ListCredentialRows.Add("clearinghouse Fictional Claims Exchange");
			}
			return summary;
		}

		private static long InsertReportRow(DataSink sink,string internalName,string description,int category,int itemOrder) {
			long displayReportNum=sink.NextId("displayreport");
			sink.Insert("displayreport",new Row { ["DisplayReportNum"]=displayReportNum,["InternalName"]=internalName,["ItemOrder"]=itemOrder,
				["Description"]=description,["Category"]=category,["IsHidden"]=0,["IsVisibleInSubMenu"]=0 });
			return displayReportNum;
		}

		///<summary>32 random bytes in base64, without the fork's "osd1$" prefix: what an Open Dental ciphertext looks like to the fork.</summary>
		private static string FakeCiphertext(Rng rng) {
			return Convert.ToBase64String(rng.Bytes(32));
		}
	}
}
