// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SyntheticData.Infrastructure;

namespace SyntheticData.Generation {
	///<summary>The practice set-up: preferences, definitions, users, providers, operatories, procedure codes, fee schedules with dated
	///fees and notes, coverage categories, code groups, carriers, plans and benefits, recall types, views, sheet and eForm definitions,
	///accounts and other lists the families refer to.</summary>
	public static class PracticeGenerator {
		///<summary>Configuration tables copied from --config-from, in this order.</summary>
		public static readonly string[] ConfigTables={
			"preference","definition","usergroup","grouppermission","userod","usergroupattach","program","programproperty",
			"displayreport","displayfield","claimform","claimformitem","sigelementdef",
		};

		public static void Run(World world,Db db) {
			Rng rng=Rng.Derive(world.Settings.Seed,"practice");
			LoadExistingDefs(world,db);
			WritePreferences(world,db);
			CreateDefinitions(world);
			CreateUsers(world,rng);
			CreateProcedureCodes(world);
			CreateFeeSchedules(world);
			CreateProviders(world,rng);
			CreateCoverage(world);
			CreateCarriersAndPlans(world,rng);
			CreateRecallTypes(world);
			CreateApptViews(world);
			CreateSheetDefs(world);
			CreateEFormDefs(world);
			CreateLists(world,rng);
			CreateAccounts(world);
		}

		///<summary>Copies the configuration tables from a local database (for example a fresh 24.3.63 dev database) into the empty target.
		///Columns that exist in both are copied; new 25.3 columns get their defaults.</summary>
		public static void CopyConfiguration(Db db,SchemaInfo schemaTarget,string targetDatabase,string sourceDatabase) {
			LocalOnly.EnsureSafeIdentifier(sourceDatabase);
			if(!db.DatabaseExists(sourceDatabase)) {
				throw new RefusedException("Configuration source database "+sourceDatabase+" does not exist on the local server.");
			}
			SchemaInfo schemaSource=SchemaInfo.Load(db,sourceDatabase);
			foreach(string table in ConfigTables) {
				TableInfo tableTarget=schemaTarget.GetTable(table);
				TableInfo tableSource=schemaSource.GetTable(table);
				if(tableTarget==null || tableSource==null) {
					continue;
				}
				List<string> listColumns=tableTarget.ListColumns.Where(x => !x.IsGenerated && tableSource.HasColumn(x.Name)).Select(x => "`"+x.Name+"`").ToList();
				string columns=string.Join(",",listColumns);
				db.Execute("INSERT INTO `"+targetDatabase+"`.`"+table+"` ("+columns+") SELECT "+columns+" FROM `"+sourceDatabase+"`.`"+table+"`");
			}
		}

		private static void LoadExistingDefs(World world,Db db) {
			foreach(object[] row in db.Rows("SELECT DefNum,Category,ItemName FROM definition ORDER BY DefNum")) {
				var key=(Convert.ToInt32(row[1]),Convert.ToString(row[2]));
				if(!world.DictDefs.ContainsKey(key)) {
					world.DictDefs[key]=Convert.ToInt64(row[0]);
				}
			}
		}

		private static void WritePreferences(World world,Db db) {
			string asOf=world.AsOf.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
			Dictionary<string,string> dictPrefs=new Dictionary<string,string> {
				//An Open Dental 25.3.48 database: the version the adoption tool expects from the office.
				["DataBaseVersion"]="25.3.48.0",
				["ProgramVersion"]="25.3.48.0",
				["PracticeTitle"]="Fictional Family Dental",
				["PracticeAddress"]="100 Lantern Way",
				["PracticeAddress2"]="Suite 2",
				["PracticeCity"]="Ambervale",
				["PracticeST"]="WA",
				["PracticeZip"]="98101",
				["PracticePhone"]="2065550100",
				["PracticeFax"]="2065550199",
				//25.2/25.3 preference named in the published 25.3 database documentation.
				["ApptMirrorSecondary"]="1",
				//Generation record, so a report can name the data it came from. Not an Open Dental preference.
				["SyntheticDataInfo"]="seed="+world.Settings.Seed+";size="+world.Settings.Size.ToString().ToLowerInvariant()+";families="+world.Settings.FamilyCount
					+";years="+world.Settings.YearCount+";asOf="+asOf+";tool=SyntheticData 1",
			};
			foreach(KeyValuePair<string,string> pair in dictPrefs) {
				long count=db.ScalarLong("SELECT COUNT(*) FROM preference WHERE PrefName="+SqlLiteral.Quote(pair.Key));
				if(count>0) {
					db.Execute("UPDATE preference SET ValueString="+SqlLiteral.Quote(pair.Value)+" WHERE PrefName="+SqlLiteral.Quote(pair.Key));
					continue;
				}
				world.Sink.Insert("preference",new Row {
					["PrefNum"]=world.Sink.NextId("preference"),
					["PrefName"]=pair.Key,
					["ValueString"]=pair.Value,
					["Comments"]="",
				});
			}
		}

		private static long EnsureDef(World world,int category,string name,string itemValue="",int color=0,bool isHidden=false) {
			if(world.DictDefs.TryGetValue((category,name),out long defNum)) {
				return defNum;
			}
			int itemOrder=world.DictDefs.Keys.Count(x => x.category==category);
			defNum=world.Sink.NextId("definition");
			world.Sink.Insert("definition",new Row {
				["DefNum"]=defNum,
				["Category"]=category,
				["ItemOrder"]=itemOrder,
				["ItemName"]=name,
				["ItemValue"]=itemValue,
				["ItemColor"]=color,
				["IsHidden"]=isHidden,
			});
			world.DictDefs[(category,name)]=defNum;
			return defNum;
		}

		private static void CreateDefinitions(World world) {
			EnsureDef(world,OdValues.DefAdjTypes,"Courtesy Discount","-");
			EnsureDef(world,OdValues.DefAdjTypes,"Senior Discount","-");
			EnsureDef(world,OdValues.DefAdjTypes,"Finance Charge","+");
			EnsureDef(world,OdValues.DefAdjTypes,"Billing Charge","+");
			EnsureDef(world,OdValues.DefAdjTypes,"Bad Debt Write-off","-");
			EnsureDef(world,OdValues.DefApptConfirmed,"Not Called","");
			EnsureDef(world,OdValues.DefApptConfirmed,"Left Message","");
			EnsureDef(world,OdValues.DefApptConfirmed,"Confirmed","");
			EnsureDef(world,OdValues.DefApptConfirmed,"Arrived","");
			EnsureDef(world,OdValues.DefBillingTypes,"Standard Account","");
			EnsureDef(world,OdValues.DefBillingTypes,"Insurance Only","");
			EnsureDef(world,OdValues.DefBillingTypes,"Collections","C");
			EnsureDef(world,OdValues.DefPaymentTypes,"Check","");
			EnsureDef(world,OdValues.DefPaymentTypes,"Cash","");
			EnsureDef(world,OdValues.DefPaymentTypes,"Credit Card","");
			EnsureDef(world,OdValues.DefPaymentTypes,"Financing","");
			foreach(string category in Catalog.CreateProcCodes().Select(x => x.Category).Distinct()) {
				EnsureDef(world,OdValues.DefProcCodeCats,category,"");
			}
			EnsureDef(world,OdValues.DefRecallUnschedStatus,"Left message to schedule","");
			EnsureDef(world,OdValues.DefRecallUnschedStatus,"Will call back","");
			EnsureDef(world,OdValues.DefImageCats,"X-Rays","XP");
			EnsureDef(world,OdValues.DefImageCats,"Photos","P");
			EnsureDef(world,OdValues.DefImageCats,"Forms","");
			EnsureDef(world,OdValues.DefImageCats,"Letters","");
			EnsureDef(world,OdValues.DefBlockoutTypes,"Lunch","",unchecked((int)0xFFD3D3D3));
			EnsureDef(world,OdValues.DefBlockoutTypes,"Emergency Hold","",unchecked((int)0xFFFFC0C0));
			EnsureDef(world,OdValues.DefBlockoutTypes,"Staff Meeting","",unchecked((int)0xFFC0C0FF));
			EnsureDef(world,OdValues.DefCommLogTypes,"Appointment Related","APPT");
			EnsureDef(world,OdValues.DefCommLogTypes,"Financial","FIN");
			EnsureDef(world,OdValues.DefCommLogTypes,"Insurance","INS");
			EnsureDef(world,OdValues.DefCommLogTypes,"Miscellaneous","MISC");
			EnsureDef(world,OdValues.DefPaySplitUnearnedType,"Prepayment","");
			EnsureDef(world,OdValues.DefInsurancePaymentType,"Insurance Check","");
			EnsureDef(world,OdValues.DefInsurancePaymentType,"Insurance EFT","");
			EnsureDef(world,OdValues.DefTaskPriorities,"Normal","D");
			EnsureDef(world,OdValues.DefTaskPriorities,"High","");
			EnsureDef(world,OdValues.DefProviderSpecialties,"General Dentistry","");
			EnsureDef(world,OdValues.DefProviderSpecialties,"Dental Hygiene","");
			EnsureDef(world,OdValues.DefInsuranceVerificationStatus,"Verified","");
			EnsureDef(world,OdValues.DefInsuranceVerificationStatus,"Needs Follow-up","");
			EnsureDef(world,OdValues.DefPayPlanCategories,"Major Treatment","");
			EnsureDef(world,OdValues.DefPayPlanCategories,"Orthodontics","");
			//Task categories (DefCat 54), shown in every office from 25.2.
			EnsureDef(world,OdValues.DefTaskCategories,"Billing","");
			EnsureDef(world,OdValues.DefTaskCategories,"Insurance Follow-up","");
			EnsureDef(world,OdValues.DefTaskCategories,"Clinical","");
			EnsureDef(world,OdValues.DefTaskCategories,"Front Desk","");
		}

		private static void CreateUsers(World world,Rng rng) {
			long groupAdmin=world.Sink.NextId("usergroup");
			world.Sink.Insert("usergroup",new Row { ["UserGroupNum"]=groupAdmin,["Description"]="Synthetic Administrators" });
			long groupFront=world.Sink.NextId("usergroup");
			world.Sink.Insert("usergroup",new Row { ["UserGroupNum"]=groupFront,["Description"]="Synthetic Front Desk" });
			long groupClinical=world.Sink.NextId("usergroup");
			world.Sink.Insert("usergroup",new Row { ["UserGroupNum"]=groupClinical,["Description"]="Synthetic Clinical" });
			//Permissions, including the 24.4 to 25.2 numbers, so the adoption can be checked for double grants.
			int[] arrayPermsAll={OdValues.PermPaymentCreate,OdValues.PermAdjustmentCreate,OdValues.PermProcComplCreate,OdValues.PermAppointmentCreate,
				OdValues.PermAppointmentEdit,OdValues.PermAccountingCreate,OdValues.PermTreatPlanEdit,OdValues.PermPatientEdit,OdValues.PermPayPlanEdit,
				OdValues.PermImageSignatureCreate,OdValues.PermSignedImageEdit,OdValues.PermBlockoutsFlagged,OdValues.PermPayPlanUnlock,
				OdValues.PermProcTPEditFee,OdValues.PermEFormImport};
			foreach(int perm in arrayPermsAll) {
				GrantPermission(world,groupAdmin,perm);
			}
			foreach(int perm in new[] { OdValues.PermPaymentCreate,OdValues.PermAppointmentCreate,OdValues.PermAppointmentEdit,OdValues.PermPatientEdit,
				OdValues.PermBlockoutsFlagged,OdValues.PermEFormImport })
			{
				GrantPermission(world,groupFront,perm);
			}
			foreach(int perm in new[] { OdValues.PermProcComplCreate,OdValues.PermTreatPlanEdit,OdValues.PermAppointmentEdit,OdValues.PermImageSignatureCreate,
				OdValues.PermBlockoutsFlagged,OdValues.PermProcTPEditFee })
			{
				GrantPermission(world,groupClinical,perm);
			}
			world.UserNumAdmin=AddUser(world,"synth.admin",groupAdmin,0,0,"");
			for(int i=1;i<=Math.Max(2,world.Settings.DentistCount);i++) {
				long employeeNum=AddEmployee(world,rng);
				long userNum=AddUser(world,"frontdesk"+i,groupFront,employeeNum,0,i==1 ? "FICTIONAL\\frontdesk1" : "");
				world.ListFrontDeskUsers.Add(userNum);
			}
			world.UserGroupClinical=groupClinical;
		}

		///<summary>Dentists and hygienists, each with a user and an operatory. One dentist charges his or her own fees for root canals.</summary>
		private static void CreateProviders(World world,Rng rng) {
			long groupClinical=world.UserGroupClinical;
			long specialtyGeneral=world.Def(OdValues.DefProviderSpecialties,"General Dentistry");
			long specialtyHygiene=world.Def(OdValues.DefProviderSpecialties,"Dental Hygiene");
			int itemOrder=0;
			for(int i=0;i<world.Settings.DentistCount;i++) {
				ProviderInfo provider=AddProvider(world,rng,"DDS",false,specialtyGeneral,itemOrder++);
				provider.UserNum=AddUser(world,"dr."+provider.LName.ToLowerInvariant(),groupClinical,0,provider.ProvNum,i==0 ? "FICTIONAL\\"+provider.Abbr.ToLowerInvariant() : "");
				world.ListDentists.Add(provider);
			}
			for(int i=0;i<world.Settings.HygienistCount;i++) {
				ProviderInfo provider=AddProvider(world,rng,"RDH",true,specialtyHygiene,itemOrder++);
				provider.UserNum=AddUser(world,"rdh."+provider.LName.ToLowerInvariant()+i,groupClinical,0,provider.ProvNum,"");
				world.ListHygienists.Add(provider);
			}
			int opOrder=0;
			foreach(ProviderInfo provider in world.AllProviders) {
				long opNum=world.Sink.NextId("operatory");
				provider.OpNum=opNum;
				world.Sink.Insert("operatory",new Row {
					["OperatoryNum"]=opNum,
					["OpName"]=(provider.IsHygienist ? "Hygiene " : "Operatory ")+(opOrder+1),
					["Abbrev"]=(provider.IsHygienist ? "HYG" : "OP")+(opOrder+1),
					["ItemOrder"]=opOrder++,
					["ProvDentist"]=provider.IsHygienist ? world.ListDentists[0].ProvNum : provider.ProvNum,
					["ProvHygienist"]=provider.IsHygienist ? provider.ProvNum : 0,
					["IsHygiene"]=provider.IsHygienist,
					["ClinicNum"]=world.ClinicNum,
					["SetProspective"]=false,
					["IsNewPatAppt"]=!provider.IsHygienist,
				});
			}
			ProviderInfo providerOverride=world.ListDentists.Last();
			foreach(ProcCodeDef code in world.ListCodes.Where(x => x.Code.StartsWith("Z3"))) {
				AddFee(world,world.FeeSchedStandard,code.CodeNum,providerOverride.ProvNum,new DateTime(1,1,1),Math.Round(code.Ucr*1.12m,0));
			}
		}

		private static void GrantPermission(World world,long userGroupNum,int permType) {
			world.Sink.Insert("grouppermission",new Row {
				["GroupPermNum"]=world.Sink.NextId("grouppermission"),
				["UserGroupNum"]=userGroupNum,
				["PermType"]=permType,
			});
		}

		private static long AddEmployee(World world,Rng rng) {
			long employeeNum=world.Sink.NextId("employee");
			world.Sink.Insert("employee",new Row {
				["EmployeeNum"]=employeeNum,
				["LName"]=NameBank.Surname(rng),
				["FName"]=NameBank.GivenName(rng),
				["ClockStatus"]="Home",
				["PhoneExt"]=100+(int)employeeNum,
				["PayrollID"]="SYN"+employeeNum.ToString("000"),
				["EmailWork"]="staff"+employeeNum+"@example.com",
			});
			return employeeNum;
		}

		private static long AddUser(World world,string userName,long userGroupNum,long employeeNum,long provNum,string domainUser) {
			long userNum=world.Sink.NextId("userod");
			world.Sink.Insert("userod",new Row {
				["UserNum"]=userNum,
				["UserName"]=userName,
				["Password"]="",
				["UserGroupNum"]=userGroupNum,
				["EmployeeNum"]=employeeNum,
				["ProvNum"]=provNum,
				["DomainUser"]=domainUser,
				["BadgeId"]="",
			});
			world.Sink.Insert("usergroupattach",new Row {
				["UserGroupAttachNum"]=world.Sink.NextId("usergroupattach"),
				["UserNum"]=userNum,
				["UserGroupNum"]=userGroupNum,
			});
			world.DictUsers[userName]=userNum;
			return userNum;
		}

		private static ProviderInfo AddProvider(World world,Rng rng,string suffix,bool isHygienist,long specialty,int itemOrder) {
			ProviderInfo provider=new ProviderInfo {
				ProvNum=world.Sink.NextId("provider"),
				FName=NameBank.GivenName(rng),
				LName=NameBank.Surname(rng),
				IsHygienist=isHygienist,
			};
			provider.Abbr=(isHygienist ? "H" : "D")+provider.LName.Substring(0,3).ToUpperInvariant()+(itemOrder+1);
			int[] arrayColors={unchecked((int)0xFFB2D6FF),unchecked((int)0xFFC4F0C4),unchecked((int)0xFFFFE0B2),unchecked((int)0xFFE1C4F0)};
			world.Sink.Insert("provider",new Row {
				["ProvNum"]=provider.ProvNum,
				["Abbr"]=provider.Abbr,
				["ItemOrder"]=itemOrder,
				["LName"]=provider.LName,
				["FName"]=provider.FName,
				["Suffix"]=suffix,
				["FeeSched"]=world.FeeSchedStandard,
				["Specialty"]=specialty,
				["IsSecondary"]=isHygienist,
				["ProvColor"]=arrayColors[itemOrder%arrayColors.Length],
				["OutlineColor"]=unchecked((int)0xFF404040),
				["StateLicense"]="SYN"+(10000+provider.ProvNum),
				["NationalProvID"]="",
				["ProvStatus"]=OdValues.ProvActive,
				["SigOnFile"]=true,
				["HourlyProdGoalAmt"]=isHygienist ? 180 : 450,
			});
			return provider;
		}

		private static void CreateProcedureCodes(World world) {
			world.ListCodes=Catalog.CreateProcCodes();
			world.DictCodes=world.ListCodes.ToDictionary(x => x.Code);
			foreach(ProcCodeDef code in world.ListCodes) {
				code.CodeNum=world.Sink.NextId("procedurecode");
				code.CategoryDefNum=world.Def(OdValues.DefProcCodeCats,code.Category);
				world.Sink.Insert("procedurecode",new Row {
					["CodeNum"]=code.CodeNum,
					["ProcCode"]=code.Code,
					["Descript"]=code.Descript,
					["AbbrDesc"]=code.Abbr,
					["ProcTime"]=code.ProcTime,
					["ProcCat"]=code.CategoryDefNum,
					["TreatArea"]=code.TreatArea,
					["NoBillIns"]=code.NoBillIns,
					["IsProsth"]=code.IsProsth,
					["IsHygiene"]=code.IsHygiene,
					["PaintType"]=code.PaintType,
					["LaymanTerm"]=code.Descript,
					["DefaultNote"]="",
					["DefaultClaimNote"]="",
					["DefaultTPNote"]="",
				});
			}
		}

		///<summary>Fee schedules with dated fees (24.4): every schedule has a base row (0001-01-01, always effective); the standard schedule
		///also has yearly increases and one increase dated after the as-of date, which must not apply yet. One provider has his or her own
		///fees in the standard schedule. Schedule notes (24.4) record the changes.</summary>
		private static void CreateFeeSchedules(World world) {
			world.FeeSchedStandard=AddFeeSched(world,"Standard",OdValues.FeeSchedNormal,0);
			world.FeeSchedPpoA=AddFeeSched(world,"PPO Network A",OdValues.FeeSchedNormal,1);
			world.FeeSchedPpoB=AddFeeSched(world,"PPO Network B",OdValues.FeeSchedNormal,2);
			world.FeeSchedCopay=AddFeeSched(world,"Copay Plan",OdValues.FeeSchedCoPay,3);
			world.FeeSchedOutOfNetwork=AddFeeSched(world,"Out of Network Allowed",OdValues.FeeSchedOutNetwork,4);
			DateTime minDate=new DateTime(1,1,1);
			DateTime historyStart=world.Settings.HistoryStart;
			List<DateTime> listIncreases=new List<DateTime>();
			for(int year=historyStart.Year+1;year<=world.AsOf.Year;year++) {
				listIncreases.Add(new DateTime(year,1,1));
			}
			DateTime dateFuture=new DateTime(world.AsOf.Year,world.AsOf.Month,1).AddMonths(2);
			foreach(ProcCodeDef code in world.ListCodes) {
				decimal amount=code.Ucr;
				AddFee(world,world.FeeSchedStandard,code.CodeNum,0,minDate,amount);
				for(int i=0;i<listIncreases.Count;i++) {
					amount=Math.Round(amount*1.04m,0);
					AddFee(world,world.FeeSchedStandard,code.CodeNum,0,listIncreases[i],amount);
				}
				if(code.Code.StartsWith("Z27") || code.Code.StartsWith("Z60")) {
					AddFee(world,world.FeeSchedStandard,code.CodeNum,0,dateFuture,Math.Round(amount*1.05m,0));
				}
				AddFee(world,world.FeeSchedPpoA,code.CodeNum,0,minDate,Math.Round(code.Ucr*0.70m,0));
				if(listIncreases.Count>0) {
					AddFee(world,world.FeeSchedPpoA,code.CodeNum,0,listIncreases.Last().AddMonths(2),Math.Round(code.Ucr*0.72m,0));
				}
				AddFee(world,world.FeeSchedPpoB,code.CodeNum,0,minDate,Math.Round(code.Ucr*0.78m,0));
				AddFee(world,world.FeeSchedOutOfNetwork,code.CodeNum,0,minDate,Math.Round(code.Ucr*0.85m,0));
				if(code.Code.StartsWith("Z0") || code.Code.StartsWith("Z1")) {
					AddFee(world,world.FeeSchedCopay,code.CodeNum,0,minDate,code.Code.StartsWith("Z0") ? 0m : 10m);
				}
				else if(code.Code.StartsWith("Z2") || code.Code.StartsWith("Z7")) {
					AddFee(world,world.FeeSchedCopay,code.CodeNum,0,minDate,Math.Round(code.Ucr*0.35m,0));
				}
			}
			AddFeeSchedNote(world,world.FeeSchedStandard,"Base fees carried over from the previous system.",historyStart.AddDays(-30),"");
			foreach(DateTime dateIncrease in listIncreases) {
				AddFeeSchedNote(world,world.FeeSchedStandard,"Yearly increase of 4% effective "+dateIncrease.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+".",
					dateIncrease.AddDays(-20),"0");
			}
			AddFeeSchedNote(world,world.FeeSchedStandard,"Crown and implant fees rise 5% on "+dateFuture.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+".",
				world.AsOf.AddDays(-12),"");
			AddFeeSchedNote(world,world.FeeSchedPpoA,"Network A contract renewed; new allowed amounts entered.",world.AsOf.AddDays(-200),"");
		}

		private static long AddFeeSched(World world,string description,int feeSchedType,int itemOrder) {
			long feeSchedNum=world.Sink.NextId("feesched");
			world.Sink.Insert("feesched",new Row {
				["FeeSchedNum"]=feeSchedNum,
				["Description"]=description,
				["FeeSchedType"]=feeSchedType,
				["ItemOrder"]=itemOrder,
				["IsHidden"]=false,
				["IsGlobal"]=true,
				["SecUserNumEntry"]=world.UserNumAdmin,
				["SecDateEntry"]=world.Settings.HistoryStart.AddDays(-60),
			});
			return feeSchedNum;
		}

		private static void AddFee(World world,long feeSched,long codeNum,long provNum,DateTime dateEffective,decimal amount) {
			world.Sink.Insert("fee",new Row {
				["FeeNum"]=world.Sink.NextId("fee"),
				["Amount"]=amount,
				["FeeSched"]=feeSched,
				["CodeNum"]=codeNum,
				["ClinicNum"]=0L,
				["ProvNum"]=provNum,
				["SecUserNumEntry"]=world.UserNumAdmin,
				["SecDateEntry"]=dateEffective.Year<1900 ? world.Settings.HistoryStart.AddDays(-60) : dateEffective.AddDays(-14),
				["SecDateTEdit"]=(dateEffective.Year<1900 ? world.Settings.HistoryStart.AddDays(-60) : dateEffective.AddDays(-14)).AddHours(9),
				["DateEffective"]=dateEffective,
			});
			var key=(feeSched,codeNum,provNum);
			if(!world.DictFees.TryGetValue(key,out List<(DateTime dateEffective,decimal amount)> listFees)) {
				listFees=new List<(DateTime,decimal)>();
				world.DictFees[key]=listFees;
			}
			listFees.Add((dateEffective,amount));
			listFees.Sort((x,y) => x.dateEffective.CompareTo(y.dateEffective));
		}

		private static void AddFeeSchedNote(World world,long feeSchedNum,string note,DateTime dateEntry,string clinicNums) {
			world.Sink.Insert("feeschednote",new Row {
				["FeeSchedNoteNum"]=world.Sink.NextId("feeschednote"),
				["FeeSchedNum"]=feeSchedNum,
				["ClinicNums"]=clinicNums,
				["Note"]=note,
				["DateEntry"]=dateEntry,
				["SecUserNumEntry"]=world.UserNumAdmin,
				["SecDateEntry"]=dateEntry,
				["SecDateTEdit"]=dateEntry.AddHours(10),
			});
		}

		private static void CreateCoverage(World world) {
			world.ListCovCats=Catalog.CreateCovCats();
			int order=0;
			foreach(CovCatDef covCat in world.ListCovCats) {
				covCat.CovCatNum=world.Sink.NextId("covcat");
				world.Sink.Insert("covcat",new Row {
					["CovCatNum"]=covCat.CovCatNum,
					["Description"]=covCat.Description,
					["DefaultPercent"]=covCat.DefaultPercent,
					["CovOrder"]=order++,
					["IsHidden"]=false,
					["EbenefitCat"]=covCat.EbenefitCat,
				});
				world.Sink.Insert("covspan",new Row {
					["CovSpanNum"]=world.Sink.NextId("covspan"),
					["CovCatNum"]=covCat.CovCatNum,
					["FromCode"]=covCat.FromCode,
					["ToCode"]=covCat.ToCode,
				});
			}
			world.ListCodeGroups=Catalog.CreateCodeGroups();
			order=0;
			foreach(CodeGroupDef codeGroup in world.ListCodeGroups) {
				codeGroup.CodeGroupNum=world.Sink.NextId("codegroup");
				world.Sink.Insert("codegroup",new Row {
					["CodeGroupNum"]=codeGroup.CodeGroupNum,
					["GroupName"]=codeGroup.GroupName,
					["ProcCodes"]=codeGroup.ProcCodes,
					["ItemOrder"]=order++,
					["CodeGroupFixed"]=codeGroup.Fixed,
					["IsHidden"]=codeGroup.IsHidden,
					["ShowInAgeLimit"]=codeGroup.ShowInAgeLimit,
					["ShowInFrequency"]=codeGroup.ShowInFrequency,
					["ShowInOther"]=codeGroup.ShowInOther,
				});
			}
		}

		private static long CovCatNum(World world,string description) {
			return world.ListCovCats.First(x => x.Description==description).CovCatNum;
		}

		private static long CodeGroupNum(World world,string groupName) {
			return world.ListCodeGroups.First(x => x.GroupName==groupName).CodeGroupNum;
		}

		private static void CreateCarriersAndPlans(World world,Rng rng) {
			for(int i=0;i<Math.Max(3,world.Settings.CarrierCount);i++) {
				long employerNum=world.Sink.NextId("employer");
				world.Sink.Insert("employer",new Row {
					["EmployerNum"]=employerNum,
					["EmpName"]=NameBank.Company(rng),
					["Address"]=NameBank.Street(rng),
					["City"]=NameBank.Town(rng),
					["State"]="WA",
					["Zip"]=NameBank.Zip(rng),
					["Phone"]=NameBank.Phone(rng),
				});
				world.ListEmployerNums.Add(employerNum);
			}
			HashSet<string> setCarrierNames=new HashSet<string>();
			for(int i=0;i<world.Settings.CarrierCount;i++) {
				string carrierName;
				do {
					carrierName=NameBank.CarrierName(rng);
				} while(!setCarrierNames.Add(carrierName));
				long carrierNum=world.Sink.NextId("carrier");
				world.Sink.Insert("carrier",new Row {
					["CarrierNum"]=carrierNum,
					["CarrierName"]=carrierName,
					["Address"]="PO Box "+rng.Between(100,9999),
					["Address2"]="",
					["City"]=NameBank.Town(rng),
					["State"]="WA",
					["Zip"]=NameBank.Zip(rng),
					["Phone"]=NameBank.Phone(rng),
					["ElectID"]="SYN"+(10+i).ToString("00"),
					["NoSendElect"]=false,
					["CDAnetVersion"]="",
					["TIN"]="",
					["SecUserNumEntry"]=world.UserNumAdmin,
					["SecDateEntry"]=world.Settings.HistoryStart.AddDays(-90),
				});
				int planCount=rng.Between(1,3);
				for(int p=0;p<planCount;p++) {
					AddPlan(world,rng,carrierNum,carrierName,i,p);
				}
			}
		}

		private static void AddPlan(World world,Rng rng,long carrierNum,string carrierName,int carrierIndex,int planIndex) {
			InsPlanInfo plan=new InsPlanInfo {
				PlanNum=world.Sink.NextId("insplan"),
				CarrierNum=carrierNum,
				CarrierName=carrierName,
				EmployerNum=rng.Pick(world.ListEmployerNums),
				IsPpo=rng.Chance(0.6),
				AnnualMax=rng.Pick(new[] { 1000m,1500m,1500m,2000m,2500m }),
				Deductible=rng.Pick(new[] { 50m,50m,75m,100m }),
				ExcludesImplants=rng.Chance(0.5),
				ExcludesThirdMolarSurgery=rng.Chance(0.3),
				CrownWaitMonths=rng.Chance(0.3) ? 12 : 0,
				UsesEft=rng.Chance(0.5),
			};
			plan.GroupName=carrierName.Split(' ')[0]+" Group "+(planIndex+1);
			//25.2 made GroupNum nullable; a few plans have no group number at all.
			plan.GroupNum=(carrierIndex+planIndex)%5==4 ? null : "G"+rng.Between(100000,999999);
			plan.FeeSched=plan.IsPpo ? (rng.Chance(0.5) ? world.FeeSchedPpoA : world.FeeSchedPpoB) : 0;
			bool isCopay=!plan.IsPpo && carrierIndex%4==3 && planIndex==0;
			foreach(CovCatDef covCat in world.ListCovCats) {
				int percent=covCat.DefaultPercent;
				if(covCat.Description=="Restorative" || covCat.Description=="Endodontics" || covCat.Description=="Periodontics" || covCat.Description=="Oral Surgery") {
					percent=rng.Pick(new[] { 70,80,80,90 });
				}
				plan.DictPercentByCovCat[covCat.CovCatNum]=percent;
			}
			world.ListPlans.Add(plan);
			world.Sink.Insert("insplan",new Row {
				["PlanNum"]=plan.PlanNum,
				["GroupName"]=plan.GroupName,
				["GroupNum"]=plan.GroupNum,
				["PlanNote"]=planIndex==0 ? "Verified by phone; see benefit notes." : "",
				["FeeSched"]=plan.FeeSched,
				["PlanType"]=plan.IsPpo ? "p" : "",
				["ClaimFormNum"]=0L,
				["CopayFeeSched"]=isCopay ? world.FeeSchedCopay : 0,
				["EmployerNum"]=plan.EmployerNum,
				["CarrierNum"]=carrierNum,
				["AllowedFeeSched"]=!plan.IsPpo && rng.Chance(0.3) ? world.FeeSchedOutOfNetwork : 0,
				["DivisionNo"]="",
				["IsMedical"]=false,
				["CanadianPlanFlag"]="",
				["CanadianDiagnosticCode"]="",
				["CanadianInstitutionCode"]="",
				["RxBIN"]="",
				["SopCode"]="",
				["MonthRenew"]=rng.Chance(0.2) ? 7 : 0,
				["SecUserNumEntry"]=world.UserNumAdmin,
				["SecDateEntry"]=world.Settings.HistoryStart.AddDays(-80),
			});
			long planNum=plan.PlanNum;
			int timePeriod=rng.Chance(0.8) ? OdValues.TimeCalendarYear : OdValues.TimeServiceYear;
			AddBenefit(world,planNum,0,OdValues.BenLimitations,-1,plan.AnnualMax,timePeriod,OdValues.QtyNone,0,OdValues.CovIndividual);
			AddBenefit(world,planNum,0,OdValues.BenDeductible,-1,plan.Deductible,timePeriod,OdValues.QtyNone,0,OdValues.CovIndividual);
			AddBenefit(world,planNum,0,OdValues.BenDeductible,-1,plan.Deductible*3,timePeriod,OdValues.QtyNone,0,OdValues.CovFamily);
			foreach(string covCat in new[] { "Diagnostic","X-Ray","Preventive" }) {
				AddBenefit(world,planNum,CovCatNum(world,covCat),OdValues.BenDeductible,-1,0,timePeriod,OdValues.QtyNone,0,OdValues.CovIndividual);
			}
			foreach(CovCatDef covCat in world.ListCovCats) {
				AddBenefit(world,planNum,covCat.CovCatNum,OdValues.BenCoInsurance,plan.DictPercentByCovCat[covCat.CovCatNum],-1,timePeriod,OdValues.QtyNone,0,OdValues.CovNone);
			}
			//Frequencies and age limits by code group (24.4 code groups).
			AddBenefit(world,planNum,0,OdValues.BenLimitations,-1,-1,timePeriod,OdValues.QtyNumberOfServices,1,OdValues.CovNone,codeGroupNum:CodeGroupNum(world,"BWs"));
			AddBenefit(world,planNum,0,OdValues.BenLimitations,-1,-1,OdValues.TimeNone,OdValues.QtyYears,5,OdValues.CovNone,codeGroupNum:CodeGroupNum(world,"Pano/FMX"));
			AddBenefit(world,planNum,0,OdValues.BenLimitations,-1,-1,timePeriod,OdValues.QtyNumberOfServices,2,OdValues.CovNone,codeGroupNum:CodeGroupNum(world,"Exams"));
			AddBenefit(world,planNum,0,OdValues.BenLimitations,-1,-1,timePeriod,OdValues.QtyNumberOfServices,2,OdValues.CovNone,codeGroupNum:CodeGroupNum(world,"Prophy"));
			AddBenefit(world,planNum,0,OdValues.BenLimitations,-1,-1,OdValues.TimeNone,OdValues.QtyAgeLimit,14,OdValues.CovNone,codeGroupNum:CodeGroupNum(world,"Fluoride"));
			AddBenefit(world,planNum,0,OdValues.BenLimitations,-1,-1,OdValues.TimeNone,OdValues.QtyAgeLimit,16,OdValues.CovNone,codeGroupNum:CodeGroupNum(world,"Sealant"),
				toothRange:"2,3,14,15,18,19,30,31",treatArea:OdValues.AreaTooth);
			//Other Benefits grid (25.3): replacement frequencies for crowns and dentures.
			AddBenefit(world,planNum,0,OdValues.BenLimitations,-1,-1,OdValues.TimeNone,OdValues.QtyYears,5,OdValues.CovNone,codeGroupNum:CodeGroupNum(world,"Crowns"));
			AddBenefit(world,planNum,0,OdValues.BenLimitations,-1,-1,OdValues.TimeNone,OdValues.QtyYears,7,OdValues.CovNone,codeGroupNum:CodeGroupNum(world,"Dentures"));
			AddBenefit(world,planNum,CovCatNum(world,"Orthodontics"),OdValues.BenLimitations,-1,1500,OdValues.TimeLifetime,OdValues.QtyNone,0,OdValues.CovIndividual);
			if(plan.ExcludesImplants) {
				AddBenefit(world,planNum,0,OdValues.BenExclusions,-1,-1,OdValues.TimeNone,OdValues.QtyNone,0,OdValues.CovNone,codeGroupNum:CodeGroupNum(world,"Implants"));
			}
			if(plan.ExcludesThirdMolarSurgery) {
				//Tooth-range exclusion (25.3): surgical removal of third molars is not covered.
				AddBenefit(world,planNum,0,OdValues.BenExclusions,-1,-1,OdValues.TimeNone,OdValues.QtyNone,0,OdValues.CovNone,codeNum:world.Code("Z7210").CodeNum,
					toothRange:"1,16,17,32",treatArea:OdValues.AreaTooth);
			}
			if(plan.CrownWaitMonths>0) {
				AddBenefit(world,planNum,CovCatNum(world,"Crowns"),OdValues.BenWaitingPeriod,-1,-1,OdValues.TimeNone,OdValues.QtyMonths,plan.CrownWaitMonths,OdValues.CovNone);
			}
			if(planNum%3==0) {
				//A range written the way the editor accepts it.
				AddBenefit(world,planNum,0,OdValues.BenLimitations,-1,-1,OdValues.TimeNone,OdValues.QtyYears,3,OdValues.CovNone,codeNum:world.Code("Z2950").CodeNum,
					toothRange:"4-13",treatArea:OdValues.AreaTooth);
			}
		}

		public static void AddBenefit(World world,long planNum,long covCatNum,int benefitType,int percent,decimal monetaryAmt,int timePeriod,int quantityQualifier,
			int quantity,int coverageLevel,long patPlanNum=0,long codeGroupNum=0,long codeNum=0,string toothRange=null,int treatArea=0)
		{
			world.Sink.Insert("benefit",new Row {
				["BenefitNum"]=world.Sink.NextId("benefit"),
				["PlanNum"]=patPlanNum==0 ? planNum : 0,
				["PatPlanNum"]=patPlanNum,
				["CovCatNum"]=covCatNum,
				["BenefitType"]=benefitType,
				["Percent"]=percent,
				["MonetaryAmt"]=monetaryAmt,
				["TimePeriod"]=timePeriod,
				["QuantityQualifier"]=quantityQualifier,
				["Quantity"]=quantity,
				["CodeNum"]=codeNum,
				["CoverageLevel"]=coverageLevel,
				["SecDateTEntry"]=world.Settings.HistoryStart.AddDays(-80).AddHours(10),
				["CodeGroupNum"]=codeGroupNum,
				["TreatArea"]=treatArea,
				["ToothRange"]=toothRange,
			});
		}

		private static void CreateRecallTypes(World world) {
			world.RecallTypeProphy=world.Sink.NextId("recalltype");
			world.Sink.Insert("recalltype",new Row { ["RecallTypeNum"]=world.RecallTypeProphy,["Description"]="Prophy",["DefaultInterval"]=393216,
				["TimePattern"]="/XXXX/",["Procedures"]="Z0120,Z1110" });
			world.RecallTypePerio=world.Sink.NextId("recalltype");
			world.Sink.Insert("recalltype",new Row { ["RecallTypeNum"]=world.RecallTypePerio,["Description"]="Perio",["DefaultInterval"]=262144,
				["TimePattern"]="/XXXXXX/",["Procedures"]="Z4910" });
			world.RecallTypeBitewing=world.Sink.NextId("recalltype");
			world.Sink.Insert("recalltype",new Row { ["RecallTypeNum"]=world.RecallTypeBitewing,["Description"]="4BWX",["DefaultInterval"]=16777216,
				["TimePattern"]="",["Procedures"]="Z0274",["AppendToSpecial"]=true });
		}

		private static void CreateApptViews(World world) {
			//25.2: views can show mirrored appointments in the secondary provider's operatory.
			long viewAll=world.Sink.NextId("apptview");
			world.Sink.Insert("apptview",new Row { ["ApptViewNum"]=viewAll,["Description"]="All Operatories",["ItemOrder"]=0,["RowsPerIncr"]=1,
				["OnlySchedBeforeTime"]=TimeSpan.Zero,["OnlySchedAfterTime"]=TimeSpan.Zero,["ApptTimeScrollStart"]=new TimeSpan(8,0,0),["ShowMirroredAppts"]=true });
			long viewHyg=world.Sink.NextId("apptview");
			world.Sink.Insert("apptview",new Row { ["ApptViewNum"]=viewHyg,["Description"]="Hygiene",["ItemOrder"]=1,["RowsPerIncr"]=1,
				["OnlySchedBeforeTime"]=TimeSpan.Zero,["OnlySchedAfterTime"]=TimeSpan.Zero,["ApptTimeScrollStart"]=new TimeSpan(8,0,0),["ShowMirroredAppts"]=false });
			foreach(ProviderInfo provider in world.AllProviders) {
				world.Sink.Insert("apptviewitem",new Row { ["ApptViewItemNum"]=world.Sink.NextId("apptviewitem"),["ApptViewNum"]=viewAll,["OpNum"]=provider.OpNum });
				world.Sink.Insert("apptviewitem",new Row { ["ApptViewItemNum"]=world.Sink.NextId("apptviewitem"),["ApptViewNum"]=viewAll,["ProvNum"]=provider.ProvNum });
				if(provider.IsHygienist) {
					world.Sink.Insert("apptviewitem",new Row { ["ApptViewItemNum"]=world.Sink.NextId("apptviewitem"),["ApptViewNum"]=viewHyg,["OpNum"]=provider.OpNum });
				}
			}
			string[] arrayElements={"PatientName","Procs","Note"};
			for(int i=0;i<arrayElements.Length;i++) {
				foreach(long view in new[] { viewAll,viewHyg }) {
					world.Sink.Insert("apptviewitem",new Row { ["ApptViewItemNum"]=world.Sink.NextId("apptviewitem"),["ApptViewNum"]=view,
						["ElementDesc"]=arrayElements[i],["ElementOrder"]=i,["ElementColor"]=unchecked((int)0xFF000000) });
				}
			}
		}

		private static void CreateSheetDefs(World world) {
			DateTime created=world.Settings.HistoryStart.AddDays(-30);
			world.SheetDefPayPlan=AddSheetDef(world,"Payment Plan (office layout)",OdValues.SheetPaymentPlan,created);
			AddSheetFieldDef(world,world.SheetDefPayPlan,OdValues.SfStaticText,"","Payment Agreement",50,40,400,30);
			AddSheetFieldDef(world,world.SheetDefPayPlan,OdValues.SfOutputText,"nameLF","",50,90,300,20);
			AddSheetFieldDef(world,world.SheetDefPayPlan,OdValues.SfOutputText,"PayPlanDate","",400,90,200,20);
			AddSheetFieldDef(world,world.SheetDefPayPlan,OdValues.SfSigBox,"","",50,900,360,80);
			//Check sheet (SheetType 28, 25.3) for printing accounting checks.
			world.SheetDefCheck=AddSheetDef(world,"Business Check",OdValues.SheetCheck,world.AsOf.AddDays(-150));
			AddSheetFieldDef(world,world.SheetDefCheck,OdValues.SfOutputText,"checkDate","",620,60,140,20);
			AddSheetFieldDef(world,world.SheetDefCheck,OdValues.SfOutputText,"payee","",80,110,500,20);
			AddSheetFieldDef(world,world.SheetDefCheck,OdValues.SfOutputText,"amountNumeric","",620,110,140,20);
			AddSheetFieldDef(world,world.SheetDefCheck,OdValues.SfOutputText,"amountWords","",60,150,600,20);
			AddSheetFieldDef(world,world.SheetDefCheck,OdValues.SfOutputText,"memo","",60,240,400,20);
			world.SheetDefConsent=AddSheetDef(world,"Treatment Consent",OdValues.SheetConsent,created);
			AddSheetFieldDef(world,world.SheetDefConsent,OdValues.SfStaticText,"","I agree to the treatment described below.",50,60,600,40);
			AddSheetFieldDef(world,world.SheetDefConsent,OdValues.SfOutputText,"patient.nameFL","",50,120,300,20);
			AddSheetFieldDef(world,world.SheetDefConsent,OdValues.SfCheckBox,"risksExplained","",50,160,20,20);
			AddSheetFieldDef(world,world.SheetDefConsent,OdValues.SfSigBox,"","",50,400,360,80);
			AddSheetFieldDef(world,world.SheetDefConsent,OdValues.SfSigBoxPractice,"","",450,400,300,80);
		}

		private static long AddSheetDef(World world,string description,int sheetType,DateTime created) {
			long sheetDefNum=world.Sink.NextId("sheetdef");
			world.Sink.Insert("sheetdef",new Row {
				["SheetDefNum"]=sheetDefNum,
				["Description"]=description,
				["SheetType"]=sheetType,
				["FontSize"]=10f,
				["FontName"]="Arial",
				["Width"]=850,
				["Height"]=1100,
				["PageCount"]=1,
				["DateTCreated"]=created.AddHours(11),
				["RevID"]=1,
			});
			return sheetDefNum;
		}

		private static void AddSheetFieldDef(World world,long sheetDefNum,int fieldType,string fieldName,string fieldValue,int x,int y,int width,int height) {
			world.Sink.Insert("sheetfielddef",new Row {
				["SheetFieldDefNum"]=world.Sink.NextId("sheetfielddef"),
				["SheetDefNum"]=sheetDefNum,
				["FieldType"]=fieldType,
				["FieldName"]=fieldName,
				["FieldValue"]=fieldValue,
				["FontSize"]=10f,
				["FontName"]="Arial",
				["XPos"]=x,
				["YPos"]=y,
				["Width"]=width,
				["Height"]=height,
				["RadioButtonValue"]="",
				["RadioButtonGroup"]="",
				["ReportableName"]="",
				["UiLabelMobile"]="",
				["UiLabelMobileRadioButton"]="",
				["Language"]="",
				["CanElectronicallySign"]=fieldType==OdValues.SfSigBox || fieldType==OdValues.SfSigBoxPractice,
			});
		}

		///<summary>eForm definitions (24.4) with every field type, including PatientList (8, 25.1) and InsPick (9, 25.2), and the
		///AutoImport and PrefillFromGuar flags (25.1).</summary>
		private static void CreateEFormDefs(World world) {
			DateTime created=new DateTime(2024,11,4,9,30,0);
			world.EFormDefRegistration=world.Sink.NextId("eformdef");
			world.Sink.Insert("eformdef",new Row { ["EFormDefNum"]=world.EFormDefRegistration,["FormType"]=OdValues.EFormPatientForm,
				["Description"]="New Patient Registration",["DateTCreated"]=created,["MaxWidth"]=650,["RevID"]=3,["ShowLabelsBold"]=true,
				["SpaceBelowEachField"]=8,["SpaceToRightEachField"]=10,["SaveImageCategory"]=world.Def(OdValues.DefImageCats,"Forms") });
			var listReg=new (int type,string dbLink,string label,string pickList,bool autoImport,bool prefill,bool required,string condParent,string condValue)[] {
				(OdValues.EfLabel,"","Welcome. Please tell us about yourself.","",false,false,false,"",""),
				(OdValues.EfTextField,"FName","First Name","",true,false,true,"",""),
				(OdValues.EfTextField,"LName","Last Name","",true,false,true,"",""),
				(OdValues.EfDateField,"Birthdate","Birthdate","",true,false,true,"",""),
				(OdValues.EfTextField,"Address","Street Address","",true,true,false,"",""),
				(OdValues.EfTextField,"City","City","",true,true,false,"",""),
				(OdValues.EfTextField,"HmPhone","Home Phone","",false,true,false,"",""),
				(OdValues.EfTextField,"Email","E-mail","",true,false,false,"",""),
				(OdValues.EfRadioButtons,"Gender","Gender","Male,Female,Unknown",true,false,false,"",""),
				(OdValues.EfCheckBox,"","Do you have dental insurance?","",false,false,false,"",""),
				(OdValues.EfInsPick,"ins1","Primary Insurance","",true,false,false,"Do you have dental insurance?","X"),
				(OdValues.EfPatientList,"","Other family members we see","",false,false,false,"",""),
				(OdValues.EfPageBreak,"","","",false,false,false,"",""),
				(OdValues.EfSigBox,"","Signature","",false,false,true,"",""),
			};
			for(int i=0;i<listReg.Length;i++) {
				var field=listReg[i];
				long fieldDefNum=AddEFormFieldDef(world,world.EFormDefRegistration,field.type,field.dbLink,field.label,field.pickList,i,field.autoImport,field.prefill,
					field.required,field.condParent,field.condValue);
				world.ListRegistrationFields.Add((fieldDefNum,field.type,field.dbLink,field.label,field.pickList,i));
			}
			world.EFormDefMedical=world.Sink.NextId("eformdef");
			world.Sink.Insert("eformdef",new Row { ["EFormDefNum"]=world.EFormDefMedical,["FormType"]=OdValues.EFormMedicalHistory,
				["Description"]="Medical History",["DateTCreated"]=created.AddDays(1),["MaxWidth"]=650,["RevID"]=2,["SpaceBelowEachField"]=8,
				["SpaceToRightEachField"]=10,["SaveImageCategory"]=world.Def(OdValues.DefImageCats,"Forms") });
			var listMed=new (int type,string dbLink,string label,string pickList)[] {
				(OdValues.EfLabel,"","Medical History",""),
				(OdValues.EfMedicationList,"","Current medications",""),
				(OdValues.EfRadioButtons,"allergy:Penicillin","Allergic to penicillin?","Yes,No"),
				(OdValues.EfRadioButtons,"problem:High blood pressure","High blood pressure?","Yes,No"),
				(OdValues.EfTextField,"","Anything else we should know?",""),
				(OdValues.EfSigBox,"","Signature",""),
			};
			for(int i=0;i<listMed.Length;i++) {
				var field=listMed[i];
				long fieldDefNum=AddEFormFieldDef(world,world.EFormDefMedical,field.type,field.dbLink,field.label,field.pickList,i,field.type==OdValues.EfMedicationList,
					false,false,"","");
				world.ListMedicalFields.Add((fieldDefNum,field.type,field.dbLink,field.label,field.pickList,i));
			}
			string[] arrayRules={"Birthdate","Email","Address"};
			for(int i=0;i<arrayRules.Length;i++) {
				world.Sink.Insert("eformimportrule",new Row { ["EFormImportRuleNum"]=world.Sink.NextId("eformimportrule"),["FieldName"]=arrayRules[i],
					["Situation"]=i%2,["Action"]=i==0 ? 1 : 0 });
			}
		}

		private static long AddEFormFieldDef(World world,long eFormDefNum,int fieldType,string dbLink,string label,string pickList,int itemOrder,bool autoImport,
			bool prefillFromGuar,bool isRequired,string conditionalParent,string conditionalValue)
		{
			long fieldDefNum=world.Sink.NextId("eformfielddef");
			world.Sink.Insert("eformfielddef",new Row {
				["EFormFieldDefNum"]=fieldDefNum,
				["EFormDefNum"]=eFormDefNum,
				["FieldType"]=fieldType,
				["DbLink"]=dbLink,
				["ValueLabel"]=label,
				["ItemOrder"]=itemOrder,
				["PickListVis"]=pickList,
				["PickListDb"]=pickList.Replace("Unknown","U").Replace("Male","M").Replace("Female","F"),
				["IsHorizStacking"]=fieldType==OdValues.EfRadioButtons,
				["IsTextWrap"]=fieldType==OdValues.EfLabel,
				["Width"]=fieldType==OdValues.EfTextField ? 300 : 0,
				["FontScale"]=100,
				["IsRequired"]=isRequired,
				["ConditionalParent"]=conditionalParent,
				["ConditionalValue"]=conditionalValue,
				["ReportableName"]=fieldType==OdValues.EfRadioButtons ? "rpt"+itemOrder : "",
				["MinWidth"]=0,
				["AutoImport"]=autoImport,
				["PrefillFromGuar"]=prefillFromGuar,
			});
			return fieldDefNum;
		}

		private static void CreateLists(World world,Rng rng) {
			string[] arrayMeds={"Amoxicillin","Ibuprofen","Lisinopril","Metformin","Atorvastatin","Levothyroxine","Amlodipine","Omeprazole",
				"Albuterol","Warfarin","Clopidogrel","Sertraline","Chlorhexidine rinse","Phenoxybenzamine (discontinued)","Tetracycline (old entry)"};
			for(int i=0;i<arrayMeds.Length;i++) {
				//25.1: medications can be hidden from pickers; the last two are hidden but still on some patients.
				bool isHidden=i>=arrayMeds.Length-2;
				long medicationNum=world.Sink.NextId("medication");
				world.Sink.Insert("medication",new Row { ["MedicationNum"]=medicationNum,["MedName"]=arrayMeds[i],["GenericNum"]=medicationNum,
					["Notes"]=isHidden ? "Hidden: no longer prescribed." : "",["RxCui"]=0L,["IsHidden"]=isHidden });
				world.ListMedications.Add((medicationNum,arrayMeds[i],isHidden));
			}
			foreach(string allergy in new[] { "Penicillin","Latex","Codeine","Sulfa drugs" }) {
				long allergyDefNum=world.Sink.NextId("allergydef");
				world.Sink.Insert("allergydef",new Row { ["AllergyDefNum"]=allergyDefNum,["Description"]=allergy,["UniiCode"]="" });
				world.ListAllergyDefNums.Add(allergyDefNum);
			}
			string[] arrayDiseases={"High blood pressure","Diabetes","Asthma","Heart murmur","Artificial joint"};
			for(int i=0;i<arrayDiseases.Length;i++) {
				long diseaseDefNum=world.Sink.NextId("diseasedef");
				world.Sink.Insert("diseasedef",new Row { ["DiseaseDefNum"]=diseaseDefNum,["DiseaseName"]=arrayDiseases[i],["ItemOrder"]=i,
					["ICD9Code"]="",["SnomedCode"]="",["Icd10Code"]="" });
				world.ListDiseaseDefNums.Add(diseaseDefNum);
			}
			string[] arraySpecialties={"Oral Surgery","Endodontics","Orthodontics","Periodontics"};
			for(int i=0;i<arraySpecialties.Length;i++) {
				long referralNum=world.Sink.NextId("referral");
				world.Sink.Insert("referral",new Row {
					["ReferralNum"]=referralNum,["LName"]=NameBank.Surname(rng),["FName"]=NameBank.GivenName(rng),["ST"]="WA",
					["Telephone"]="2065550"+(150+i),["Address"]=NameBank.Street(rng),["City"]=NameBank.Town(rng),["Zip"]=NameBank.Zip(rng),
					["Note"]=arraySpecialties[i]+" specialist (fictional).",["Title"]="DDS",["EMail"]="referrals"+i+"@example.org",
					["IsDoctor"]=true,["BusinessName"]="Fictional "+arraySpecialties[i]+" Group",["DisplayNote"]="",["NationalProvID"]="",
				});
				world.ListReferralNums.Add(referralNum);
			}
			foreach(string listName in new[] { "Front Office","Insurance","Billing","Clinical Follow-up" }) {
				long taskListNum=world.Sink.NextId("tasklist");
				world.Sink.Insert("tasklist",new Row { ["TaskListNum"]=taskListNum,["Descript"]=listName,["Parent"]=0L,
					["DateTimeEntry"]=world.Settings.HistoryStart.AddDays(-20).AddHours(9),["TaskListStatus"]=0 });
				world.ListTaskListNums.Add(taskListNum);
			}
			string[] arrayTemplates={"6 months, no interest","12 months, 9.9% APR","Ortho 24 months"};
			for(int i=0;i<arrayTemplates.Length;i++) {
				long templateNum=world.Sink.NextId("payplantemplate");
				world.Sink.Insert("payplantemplate",new Row {
					["PayPlanTemplateNum"]=templateNum,["PayPlanTemplateName"]=arrayTemplates[i],["ClinicNum"]=0L,["APR"]=i==1 ? 9.9 : 0.0,
					["NumberOfPayments"]=new[] { 6,12,24 }[i],["ChargeFrequency"]=OdValues.PayFreqMonthly,["DownPayment"]=i==2 ? 500m : 0m,
					["Note"]="",["SheetDefNum"]=i==1 ? 0L : world.SheetDefPayPlan,
				});
				world.ListPayPlanTemplateNums.Add(templateNum);
			}
			world.Sink.Insert("clearinghouse",new Row { ["ClearinghouseNum"]=world.Sink.NextId("clearinghouse"),["Description"]="Fictional Claims Exchange",
				["ExportPath"]=@"C:\SyntheticClaims\Out\",["Payors"]="",["Eformat"]=1,["ISA05"]="ZZ",["SenderTIN"]="",["ISA07"]="ZZ",["ISA08"]="SYNTHEX",
				["ISA15"]="T",["ResponsePath"]=@"C:\SyntheticClaims\In\",["ClientProgram"]="",["LoginID"]="synth",["SenderName"]="Fictional Family Dental",
				["SenderTelephone"]="2065550100",["GS03"]="SYNTHEX",["ISA02"]="",["ISA04"]="",["ISA16"]="",["SeparatorData"]="",["SeparatorSegment"]="",
				["LocationID"]="",["EnableXConnect"]=true });
			world.Sink.Insert("clearinghouse",new Row { ["ClearinghouseNum"]=world.Sink.NextId("clearinghouse"),["Description"]="Paper claims only",
				["ExportPath"]="",["Payors"]="",["Eformat"]=0,["ISA05"]="",["SenderTIN"]="",["ISA07"]="",["ISA08"]="",["ISA15"]="P",["ResponsePath"]="",
				["ClientProgram"]="",["LoginID"]="",["SenderName"]="",["SenderTelephone"]="",["GS03"]="",["ISA02"]="",["ISA04"]="",["ISA16"]="",
				["SeparatorData"]="",["SeparatorSegment"]="",["LocationID"]="",["EnableXConnect"]=false });
			for(int i=1;i<=4;i++) {
				string computerName="SYN-WS0"+i;
				world.Sink.Insert("computer",new Row { ["ComputerNum"]=world.Sink.NextId("computer"),["CompName"]=computerName,
					["LastHeartBeat"]=world.AsOf.AddHours(17) });
				world.Sink.Insert("computerpref",new Row { ["ComputerPrefNum"]=world.Sink.NextId("computerpref"),["ComputerName"]=computerName,
					["ComputerOS"]="Windows",["VideoRectangle"]="",["CreditCardTerminalId"]=i<=2 ? "TERM-SYN-0"+i : "" });
			}
			string[] arrayFilters={"SHOW PROCESSLIST","information_schema","SELECT GET_LOCK","mysql.event"};
			for(int i=0;i<arrayFilters.Length;i++) {
				world.Sink.Insert("queryfilter",new Row { ["QueryFilterNum"]=world.Sink.NextId("queryfilter"),
					["GroupName"]=i<2 ? "Server housekeeping" : "Background jobs",["FilterText"]=arrayFilters[i] });
			}
			DateTime dateBranding=new DateTime(2025,8,12,15,0,0);
			world.Sink.Insert("branding",new Row { ["BrandingNum"]=world.Sink.NextId("branding"),["BrandingType"]=0,["ClinicNum"]=0L,
				["ValueString"]="Fictional Family Dental",["DateTimeUpdated"]=dateBranding });
			world.Sink.Insert("branding",new Row { ["BrandingNum"]=world.Sink.NextId("branding"),["BrandingType"]=1,["ClinicNum"]=0L,
				["ValueString"]=@"C:\SyntheticImages\logo.png",["DateTimeUpdated"]=dateBranding });
			world.Sink.Insert("branding",new Row { ["BrandingNum"]=world.Sink.NextId("branding"),["BrandingType"]=2,["ClinicNum"]=0L,
				["ValueString"]="{\"primary\":\"#2E6DA4\",\"secondary\":\"#F2F2F2\"}",["DateTimeUpdated"]=dateBranding });
			world.Sink.Insert("eclipboardsheetdef",new Row { ["EClipboardSheetDefNum"]=world.Sink.NextId("eclipboardsheetdef"),["SheetDefNum"]=world.SheetDefConsent,
				["ClinicNum"]=0L,["ResubmitInterval"]=0L,["ItemOrder"]=0,["IgnoreSheetDefNums"]="",["SheetDefNumsConsidered"]=world.SheetDefConsent+","+world.SheetDefPayPlan });
			world.Sink.Insert("eclipboardsheetdef",new Row { ["EClipboardSheetDefNum"]=world.Sink.NextId("eclipboardsheetdef"),["SheetDefNum"]=0L,
				["ClinicNum"]=0L,["ResubmitInterval"]=365L,["ItemOrder"]=1,["IgnoreSheetDefNums"]="",["EFormDefNum"]=world.EFormDefMedical,
				["SheetDefNumsConsidered"]="" });
		}

		private static void CreateAccounts(World world) {
			long Add(string description,int acctType,string bankNumber="") {
				long accountNum=world.Sink.NextId("account");
				world.Sink.Insert("account",new Row { ["AccountNum"]=accountNum,["Description"]=description,["AcctType"]=acctType,["BankNumber"]=bankNumber,
					["Inactive"]=false,["AccountColor"]=unchecked((int)0xFFFFFFFF),["IsRetainedEarnings"]=false });
				return accountNum;
			}
			world.AccountChecking=Add("Operating Checking",OdValues.AcctAsset,"000123456789");
			Add("Equipment",OdValues.AcctAsset);
			Add("Credit Card Payable",OdValues.AcctLiability);
			long retained=world.Sink.NextId("account");
			world.Sink.Insert("account",new Row { ["AccountNum"]=retained,["Description"]="Retained Earnings",["AcctType"]=OdValues.AcctEquity,["BankNumber"]="",
				["IsRetainedEarnings"]=true });
			world.AccountPatientIncome=Add("Patient Fee Income",OdValues.AcctIncome);
			world.AccountInsuranceIncome=Add("Insurance Income",OdValues.AcctIncome);
			foreach(string expense in new[] { "Rent","Dental Supplies","Laboratory Fees","Utilities","Office Supplies" }) {
				world.ListExpenseAccounts.Add((Add(expense,OdValues.AcctExpense),expense));
			}
		}
	}
}
