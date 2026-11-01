// UDS+ report for FQHC dental services (Open Dental 25.4, package M2-UDS in docs/specs/port-backlog.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenPmsDentalBusiness {
	///<summary>UDS+ report (Standard Reports > Public Health). For a Federally Qualified Health Center it counts, per UDS dental service line (HRSA's
	///Table 6A dental rows), the visits and the distinct patients with at least one qualifying procedure, and it writes a patient-level file, one row
	///per patient, visit and line, for the center's own UDS+ tooling. No data is sent anywhere.
	///<para>Rules: completed procedures only, by procedure date in the range; deleted procedures (status D) are never complete, so they are left out;
	///patients with status Deleted are left out. A visit is one patient on one date. A service counts once per visit, however often it was done.
	///Which procedure codes count on which line comes from the office's setup (UdsPlusLineMap).</para></summary>
	public class RpUdsPlus {
		public const string INS_NONE="None/Uninsured";
		public const string INS_MEDICAID="Medicaid/CHIP";
		public const string INS_MEDICARE="Medicare";
		public const string INS_DUAL="Dually Eligible (Medicare and Medicaid)";
		public const string INS_OTHER_PUBLIC="Other Public";
		public const string INS_PRIVATE="Private";
		public const string DECLINED="Declined to specify";
		public const string MULTIRACIAL="Multiracial";

		///<summary>Column headers of the patient-level file, in order.</summary>
		public static readonly string[] ArrayCsvColumns=new string[] {
			"PatNum","DateOfBirth","Sex","Zip","Race","RaceCodes","Ethnicity","EthnicityCodes","PreferredLanguage","InsuranceType","InsuranceFilingCode",
			"VisitDate","ProcCodes","UdsLine","UdsService",
		};

		///<summary>CDC race and ethnicity codes of the OMB minimum categories, for databases whose cdcrec table was never filled. From the CDC Race and
		///Ethnicity Code Set (a U.S. government publication); this program's PatRace enum lists the same codes.</summary>
		private static readonly Dictionary<string,string> _dictCdcrecFallback=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
			{ "1002-5","American Indian or Alaska Native" },
			{ "2028-9","Asian" },
			{ "2054-5","Black or African American" },
			{ "2076-8","Native Hawaiian or Other Pacific Islander" },
			{ "2106-3","White" },
			{ "2131-1","Other Race" },
			{ "2135-2","Hispanic or Latino" },
			{ "2186-5","Not Hispanic or Latino" },
		};

		///<summary>Ethnicity codes recognized without the cdcrec table.</summary>
		private static readonly HashSet<string> _hashSetEthnicityCodes=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"2135-2","2186-5",PatientRace.DECLINE_SPECIFY_ETHNICITY_CODE,
		};

		#region Data
		///<summary>CodeNum and ProcCode of every procedure code.</summary>
		public static DataTable GetProcCodeTable() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod());
			}
			return Db.GetTable("SELECT CodeNum,ProcCode FROM procedurecode");
		}

		///<summary>The procedure codes (CodeNums) that count on at least one line of the map.</summary>
		public static List<long> GetCodeNums(UdsPlusLineMap udsPlusLineMap,DataTable tableProcCodes) {
			Meth.NoCheckMiddleTierRole();
			List<long> listCodeNums=new List<long>();
			foreach(DataRow dataRow in tableProcCodes.Rows) {
				if(udsPlusLineMap.GetLinesForCode(dataRow["ProcCode"].ToString()).Count>0) {
					listCodeNums.Add(PIn.Long(dataRow["CodeNum"].ToString()));
				}
			}
			return listCodeNums;
		}

		///<summary>The report's rows: table "Procs" (one row per qualifying procedure, with the patient's demographics), table "Races" (the patients'
		///race and ethnicity rows) and table "Insurance" (the patients' current plans with their dates and filing codes). listClinicNums and listProvNums
		///limit the procedures; an empty list means no limit. listCodeNums must not be empty.</summary>
		public static DataSet GetDataSet(DateTime dateFrom,DateTime dateTo,List<long> listClinicNums,List<long> listProvNums,List<long> listCodeNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetDS(MethodBase.GetCurrentMethod(),dateFrom,dateTo,listClinicNums,listProvNums,listCodeNums);
			}
			string queryProcs=GetProcQuery(dateFrom,dateTo,listClinicNums,listProvNums,listCodeNums);
			string queryRaces=GetRaceQuery(dateFrom,dateTo,listClinicNums,listProvNums,listCodeNums);
			string queryIns=GetInsuranceQuery(dateFrom,dateTo,listClinicNums,listProvNums,listCodeNums);
			return ReportsComplex.RunFuncOnReportServer(() => {
				DataSet dataSet=new DataSet();
				DataTable tableProcs=Db.GetTable(queryProcs);
				tableProcs.TableName="Procs";
				dataSet.Tables.Add(tableProcs);
				DataTable tableRaces=Db.GetTable(queryRaces);
				tableRaces.TableName="Races";
				dataSet.Tables.Add(tableRaces);
				DataTable tableIns=Db.GetTable(queryIns);
				tableIns.TableName="Insurance";
				dataSet.Tables.Add(tableIns);
				return dataSet;
			});
		}

		///<summary>The FROM and WHERE of the qualifying procedures. Public for the tests.</summary>
		public static string GetProcFromWhere(DateTime dateFrom,DateTime dateTo,List<long> listClinicNums,List<long> listProvNums,List<long> listCodeNums) {
			Meth.NoCheckMiddleTierRole();
			if(listCodeNums==null || listCodeNums.Count==0) {
				throw new ArgumentException("No procedure codes to report.");
			}
			string fromWhere="FROM procedurelog "
				+"INNER JOIN patient ON patient.PatNum=procedurelog.PatNum "
				+"INNER JOIN procedurecode ON procedurecode.CodeNum=procedurelog.CodeNum "
				+"WHERE procedurelog.ProcStatus="+POut.Int((int)ProcStat.C)+" "
				+"AND procedurelog.ProcDate BETWEEN "+POut.Date(dateFrom)+" AND "+POut.Date(dateTo)+" "
				+"AND patient.PatStatus!="+POut.Int((int)PatientStatus.Deleted)+" "
				+"AND procedurelog.CodeNum IN ("+string.Join(",",listCodeNums.Select(x => POut.Long(x)))+") ";
			if(listClinicNums!=null && listClinicNums.Count>0) {
				fromWhere+="AND procedurelog.ClinicNum IN ("+string.Join(",",listClinicNums.Select(x => POut.Long(x)))+") ";
			}
			if(listProvNums!=null && listProvNums.Count>0) {
				fromWhere+="AND procedurelog.ProvNum IN ("+string.Join(",",listProvNums.Select(x => POut.Long(x)))+") ";
			}
			return fromWhere;
		}

		public static string GetProcQuery(DateTime dateFrom,DateTime dateTo,List<long> listClinicNums,List<long> listProvNums,List<long> listCodeNums) {
			Meth.NoCheckMiddleTierRole();
			return "SELECT procedurelog.PatNum,procedurelog.ProcDate,procedurecode.ProcCode,patient.Birthdate,patient.Gender,patient.Zip,patient.Language "
				+GetProcFromWhere(dateFrom,dateTo,listClinicNums,listProvNums,listCodeNums)
				+"ORDER BY procedurelog.PatNum,procedurelog.ProcDate,procedurecode.ProcCode";
		}

		private static string GetPatNumSubquery(DateTime dateFrom,DateTime dateTo,List<long> listClinicNums,List<long> listProvNums,List<long> listCodeNums) {
			return "SELECT DISTINCT procedurelog.PatNum "+GetProcFromWhere(dateFrom,dateTo,listClinicNums,listProvNums,listCodeNums);
		}

		public static string GetRaceQuery(DateTime dateFrom,DateTime dateTo,List<long> listClinicNums,List<long> listProvNums,List<long> listCodeNums) {
			Meth.NoCheckMiddleTierRole();
			return "SELECT patientrace.PatNum,patientrace.CdcrecCode,COALESCE(cdcrec.Description,'') Description,"
				+"COALESCE(cdcrec.HeirarchicalCode,'') HeirarchicalCode "
				+"FROM patientrace "
				+"LEFT JOIN cdcrec ON cdcrec.CdcrecCode=patientrace.CdcrecCode "
				+"WHERE patientrace.PatNum IN ("+GetPatNumSubquery(dateFrom,dateTo,listClinicNums,listProvNums,listCodeNums)+") "
				+"ORDER BY patientrace.PatNum,patientrace.PatientRaceNum";
		}

		public static string GetInsuranceQuery(DateTime dateFrom,DateTime dateTo,List<long> listClinicNums,List<long> listProvNums,List<long> listCodeNums) {
			Meth.NoCheckMiddleTierRole();
			return "SELECT patplan.PatNum,patplan.Ordinal,inssub.DateEffective,inssub.DateTerm,COALESCE(insfilingcode.EclaimCode,'') EclaimCode "
				+"FROM patplan "
				+"INNER JOIN inssub ON inssub.InsSubNum=patplan.InsSubNum "
				+"INNER JOIN insplan ON insplan.PlanNum=inssub.PlanNum "
				+"LEFT JOIN insfilingcode ON insfilingcode.InsFilingCodeNum=insplan.FilingCode "
				+"WHERE patplan.PatNum IN ("+GetPatNumSubquery(dateFrom,dateTo,listClinicNums,listProvNums,listCodeNums)+") "
				+"ORDER BY patplan.PatNum,patplan.Ordinal,patplan.PatPlanNum";
		}

		///<summary>Reads the data and counts it. An empty map, or one whose codes match no procedure code, gives an empty result without a query.
		///</summary>
		public static UdsPlusResult Run(UdsPlusLineMap udsPlusLineMap,DateTime dateFrom,DateTime dateTo,List<long> listClinicNums,List<long> listProvNums) {
			Meth.NoCheckMiddleTierRole();
			List<long> listCodeNums=GetCodeNums(udsPlusLineMap,GetProcCodeTable());
			if(listCodeNums.Count==0) {
				return Compute(udsPlusLineMap,null);
			}
			return Compute(udsPlusLineMap,GetDataSet(dateFrom,dateTo,listClinicNums,listProvNums,listCodeNums));
		}
		#endregion Data

		#region Counting
		///<summary>Counts the rows of GetDataSet. A null data set counts nothing (every line shows zero).</summary>
		public static UdsPlusResult Compute(UdsPlusLineMap udsPlusLineMap,DataSet dataSet) {
			Meth.NoCheckMiddleTierRole();
			UdsPlusResult udsPlusResult=new UdsPlusResult();
			Dictionary<UdsPlusLine,UdsPlusLineCount> dictCounts=new Dictionary<UdsPlusLine,UdsPlusLineCount>();
			Dictionary<UdsPlusLine,HashSet<long>> dictPatNums=new Dictionary<UdsPlusLine,HashSet<long>>();
			foreach(UdsPlusLine udsPlusLine in udsPlusLineMap.ListLines) {
				UdsPlusLineCount udsPlusLineCount=new UdsPlusLineCount { Line=udsPlusLine.Line,Description=udsPlusLine.Description };
				udsPlusResult.ListLineCounts.Add(udsPlusLineCount);
				dictCounts[udsPlusLine]=udsPlusLineCount;
				dictPatNums[udsPlusLine]=new HashSet<long>();
			}
			if(dataSet==null || !dataSet.Tables.Contains("Procs")) {
				return udsPlusResult;
			}
			Dictionary<long,List<RaceRow>> dictRaces=GetRaceRows(dataSet.Tables["Races"]);
			Dictionary<long,List<InsRow>> dictIns=GetInsRows(dataSet.Tables["Insurance"]);
			//Visits in the order of the procedure query: by patient, then date.
			List<Visit> listVisits=new List<Visit>();
			Dictionary<(long,DateTime),Visit> dictVisits=new Dictionary<(long,DateTime),Visit>();
			foreach(DataRow dataRow in dataSet.Tables["Procs"].Rows) {
				string procCode=dataRow["ProcCode"].ToString().Trim();
				List<UdsPlusLine> listLines=udsPlusLineMap.GetLinesForCode(procCode);
				if(listLines.Count==0) {
					continue;
				}
				long patNum=PIn.Long(dataRow["PatNum"].ToString());
				DateTime dateVisit=PIn.Date(dataRow["ProcDate"].ToString()).Date;
				if(!dictVisits.TryGetValue((patNum,dateVisit),out Visit visit)) {
					visit=new Visit { PatNum=patNum,DateVisit=dateVisit,DataRowPatient=dataRow };
					dictVisits[(patNum,dateVisit)]=visit;
					listVisits.Add(visit);
				}
				foreach(UdsPlusLine udsPlusLine in listLines) {
					if(!visit.DictCodesByLine.TryGetValue(udsPlusLine,out SortedSet<string> sortedSetCodes)) {
						sortedSetCodes=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
						visit.DictCodesByLine[udsPlusLine]=sortedSetCodes;
					}
					sortedSetCodes.Add(procCode);
				}
			}
			HashSet<long> hashSetPatNumsAll=new HashSet<long>();
			foreach(Visit visit in listVisits) {
				hashSetPatNumsAll.Add(visit.PatNum);
				udsPlusResult.TotalVisits++;
				DataRow dataRowPat=visit.DataRowPatient;
				dictRaces.TryGetValue(visit.PatNum,out List<RaceRow> listRaceRows);
				dictIns.TryGetValue(visit.PatNum,out List<InsRow> listInsRows);
				listRaceRows=listRaceRows??new List<RaceRow>();
				List<RaceRow> listRaces=listRaceRows.FindAll(x => !x.IsEthnicity);
				List<RaceRow> listEthnicities=listRaceRows.FindAll(x => x.IsEthnicity);
				GetInsuranceType(listInsRows,visit.DateVisit,out string insuranceType,out string filingCode);
				//Lines in setup order.
				foreach(UdsPlusLine udsPlusLine in udsPlusLineMap.ListLines) {
					if(!visit.DictCodesByLine.TryGetValue(udsPlusLine,out SortedSet<string> sortedSetCodes)) {
						continue;
					}
					dictCounts[udsPlusLine].Visits++;
					dictPatNums[udsPlusLine].Add(visit.PatNum);
					UdsPlusVisitRow udsPlusVisitRow=new UdsPlusVisitRow();
					udsPlusVisitRow.PatNum=visit.PatNum;
					udsPlusVisitRow.DateOfBirth=PIn.Date(dataRowPat["Birthdate"].ToString());
					udsPlusVisitRow.Sex=GetSexText(PIn.Int(dataRowPat["Gender"].ToString()));
					udsPlusVisitRow.Zip=dataRowPat["Zip"].ToString().Trim();
					udsPlusVisitRow.Race=string.Join("; ",listRaces.Select(x => x.Description));
					udsPlusVisitRow.RaceCodes=string.Join("; ",listRaces.Select(x => x.Code));
					udsPlusVisitRow.Ethnicity=string.Join("; ",listEthnicities.Select(x => x.Description));
					udsPlusVisitRow.EthnicityCodes=string.Join("; ",listEthnicities.Select(x => x.Code));
					udsPlusVisitRow.PreferredLanguage=dataRowPat["Language"].ToString().Trim();
					udsPlusVisitRow.InsuranceType=insuranceType;
					udsPlusVisitRow.InsuranceFilingCode=filingCode;
					udsPlusVisitRow.VisitDate=visit.DateVisit;
					udsPlusVisitRow.ProcCodes=string.Join(";",sortedSetCodes);
					udsPlusVisitRow.UdsLine=udsPlusLine.Line;
					udsPlusVisitRow.UdsService=udsPlusLine.Description;
					udsPlusResult.ListVisitRows.Add(udsPlusVisitRow);
				}
			}
			foreach(UdsPlusLine udsPlusLine in udsPlusLineMap.ListLines) {
				dictCounts[udsPlusLine].Patients=dictPatNums[udsPlusLine].Count;
			}
			udsPlusResult.TotalPatients=hashSetPatNumsAll.Count;
			return udsPlusResult;
		}

		private static Dictionary<long,List<RaceRow>> GetRaceRows(DataTable table) {
			Dictionary<long,List<RaceRow>> dict=new Dictionary<long,List<RaceRow>>();
			if(table==null) {
				return dict;
			}
			foreach(DataRow dataRow in table.Rows) {
				string code=dataRow["CdcrecCode"].ToString().Trim();
				if(code=="") {
					continue;
				}
				long patNum=PIn.Long(dataRow["PatNum"].ToString());
				RaceRow raceRow=GetRaceRow(code,dataRow["Description"].ToString(),dataRow["HeirarchicalCode"].ToString());
				if(!dict.TryGetValue(patNum,out List<RaceRow> listRaceRows)) {
					listRaceRows=new List<RaceRow>();
					dict[patNum]=listRaceRows;
				}
				if(!listRaceRows.Any(x => x.Code.Equals(raceRow.Code,StringComparison.OrdinalIgnoreCase))) {
					listRaceRows.Add(raceRow);
				}
			}
			return dict;
		}

		///<summary>The description and kind (race or ethnicity) of one patientrace code. cdcrec's own description and hierarchy win; without a cdcrec
		///row the OMB minimum categories are still named.</summary>
		public static RaceRow GetRaceRow(string cdcrecCode,string description,string heirarchicalCode) {
			Meth.NoCheckMiddleTierRole();
			RaceRow raceRow=new RaceRow { Code=(cdcrecCode??"").Trim() };
			switch(raceRow.Code.ToUpperInvariant()) {
				case PatientRace.DECLINE_SPECIFY_RACE_CODE:
					raceRow.Description=DECLINED;
					return raceRow;
				case PatientRace.DECLINE_SPECIFY_ETHNICITY_CODE:
					raceRow.Description=DECLINED;
					raceRow.IsEthnicity=true;
					return raceRow;
				case PatientRace.MULTI_RACE_CODE:
					raceRow.Description=MULTIRACIAL;
					return raceRow;
			}
			string hierarchy=(heirarchicalCode??"").Trim();
			raceRow.IsEthnicity=hierarchy!="" ? hierarchy.StartsWith("E",StringComparison.OrdinalIgnoreCase) : _hashSetEthnicityCodes.Contains(raceRow.Code);
			raceRow.Description=(description??"").Trim();
			if(raceRow.Description=="" && !_dictCdcrecFallback.TryGetValue(raceRow.Code,out raceRow.Description)) {
				raceRow.Description=raceRow.Code;
			}
			return raceRow;
		}

		private static Dictionary<long,List<InsRow>> GetInsRows(DataTable table) {
			Dictionary<long,List<InsRow>> dict=new Dictionary<long,List<InsRow>>();
			if(table==null) {
				return dict;
			}
			foreach(DataRow dataRow in table.Rows) {
				long patNum=PIn.Long(dataRow["PatNum"].ToString());
				InsRow insRow=new InsRow {
					Ordinal=PIn.Int(dataRow["Ordinal"].ToString()),
					DateEffective=PIn.Date(dataRow["DateEffective"].ToString()),
					DateTerm=PIn.Date(dataRow["DateTerm"].ToString()),
					EclaimCode=dataRow["EclaimCode"].ToString().Trim(),
				};
				if(!dict.TryGetValue(patNum,out List<InsRow> listInsRows)) {
					listInsRows=new List<InsRow>();
					dict[patNum]=listInsRows;
				}
				listInsRows.Add(insRow);
			}
			return dict;
		}

		///<summary>The UDS insurance category on the visit date, from the patient's plans in effect that day (subscriber effective and term dates; a
		///blank date is open). The plan with the lowest ordinal decides, except that Medicare together with Medicaid is dually eligible. The category
		///comes from the plan's claim filing code: MC Medicaid; MA, MB and 16 Medicare; CH, VA, OF and TV other public; anything else, including no
		///filing code (claims then go out as CI, commercial), private. No plan in effect is uninsured. filingCode is the deciding plan's code.</summary>
		public static void GetInsuranceType(List<InsRow> listInsRows,DateTime dateVisit,out string insuranceType,out string filingCode) {
			Meth.NoCheckMiddleTierRole();
			List<InsRow> listActive=(listInsRows??new List<InsRow>())
				.Where(x => (x.DateEffective.Year<1880 || x.DateEffective.Date<=dateVisit.Date) && (x.DateTerm.Year<1880 || x.DateTerm.Date>=dateVisit.Date))
				.OrderBy(x => x.Ordinal)
				.ToList();
			if(listActive.Count==0) {
				insuranceType=INS_NONE;
				filingCode="";
				return;
			}
			filingCode=listActive[0].EclaimCode;
			List<string> listCategories=listActive.Select(x => GetInsuranceCategory(x.EclaimCode)).ToList();
			if(listCategories.Contains(INS_MEDICARE) && listCategories.Contains(INS_MEDICAID)) {
				insuranceType=INS_DUAL;
				return;
			}
			insuranceType=listCategories[0];
		}

		///<summary>The UDS insurance category of one claim filing code (X12 claim filing indicator).</summary>
		public static string GetInsuranceCategory(string eclaimCode) {
			Meth.NoCheckMiddleTierRole();
			switch((eclaimCode??"").Trim().ToUpperInvariant()) {
				case "MC":
					return INS_MEDICAID;
				case "MA":
				case "MB":
				case "16":
					return INS_MEDICARE;
				case "CH":
				case "VA":
				case "OF":
				case "TV":
					return INS_OTHER_PUBLIC;
				default:
					return INS_PRIVATE;
			}
		}

		public static string GetSexText(int gender) {
			Meth.NoCheckMiddleTierRole();
			if(!Enum.IsDefined(typeof(PatientGender),gender)) {
				return PatientGender.Unknown.ToString();
			}
			return ((PatientGender)gender).ToString();
		}
		#endregion Counting

		#region File
		///<summary>The patient-level file: a header row (ArrayCsvColumns) and one row per patient, visit and line, comma separated, CRLF line ends,
		///dates as yyyy-MM-dd.</summary>
		public static string ToCsv(List<UdsPlusVisitRow> listVisitRows) {
			Meth.NoCheckMiddleTierRole();
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(string.Join(",",ArrayCsvColumns.Select(x => CsvField(x))));
			stringBuilder.Append("\r\n");
			foreach(UdsPlusVisitRow udsPlusVisitRow in listVisitRows) {
				string[] arrayFields=new string[] {
					udsPlusVisitRow.PatNum.ToString(CultureInfo.InvariantCulture),
					FormatDate(udsPlusVisitRow.DateOfBirth),
					udsPlusVisitRow.Sex,
					udsPlusVisitRow.Zip,
					udsPlusVisitRow.Race,
					udsPlusVisitRow.RaceCodes,
					udsPlusVisitRow.Ethnicity,
					udsPlusVisitRow.EthnicityCodes,
					udsPlusVisitRow.PreferredLanguage,
					udsPlusVisitRow.InsuranceType,
					udsPlusVisitRow.InsuranceFilingCode,
					FormatDate(udsPlusVisitRow.VisitDate),
					udsPlusVisitRow.ProcCodes,
					udsPlusVisitRow.UdsLine,
					udsPlusVisitRow.UdsService,
				};
				stringBuilder.Append(string.Join(",",arrayFields.Select(x => CsvField(x))));
				stringBuilder.Append("\r\n");
			}
			return stringBuilder.ToString();
		}

		private static string FormatDate(DateTime date) {
			if(date.Year<1880) {
				return "";
			}
			return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
		}

		///<summary>Quotes a field that holds a comma, quote, line break or leading or trailing space, doubling its quotes.</summary>
		public static string CsvField(string value) {
			Meth.NoCheckMiddleTierRole();
			value=value??"";
			if(value.IndexOfAny(new[] { ',','"','\r','\n' })<0 && value.Trim()==value) {
				return value;
			}
			return "\""+value.Replace("\"","\"\"")+"\"";
		}
		#endregion File

		///<summary>One patient on one date, with the codes done for each line.</summary>
		private class Visit {
			public long PatNum;
			public DateTime DateVisit;
			///<summary>The first procedure row of the visit, which carries the patient's demographics.</summary>
			public DataRow DataRowPatient;
			public Dictionary<UdsPlusLine,SortedSet<string>> DictCodesByLine=new Dictionary<UdsPlusLine,SortedSet<string>>();
		}

		///<summary>A race or ethnicity of a patient.</summary>
		public class RaceRow {
			public string Code="";
			public string Description="";
			public bool IsEthnicity;
		}

		///<summary>A plan of a patient.</summary>
		public class InsRow {
			public int Ordinal;
			public DateTime DateEffective;
			public DateTime DateTerm;
			public string EclaimCode="";
		}
	}

	///<summary>What the UDS+ report counted.</summary>
	public class UdsPlusResult {
		///<summary>One per line of the map, in setup order, zero counts included.</summary>
		public List<UdsPlusLineCount> ListLineCounts=new List<UdsPlusLineCount>();
		///<summary>Visits with at least one service on any line. Not the sum of the lines: a visit with services on two lines counts once.</summary>
		public int TotalVisits;
		///<summary>Distinct patients with at least one service on any line.</summary>
		public int TotalPatients;
		///<summary>The patient-level rows: one per patient, visit and line.</summary>
		public List<UdsPlusVisitRow> ListVisitRows=new List<UdsPlusVisitRow>();
	}

	///<summary>The counts of one UDS line.</summary>
	public class UdsPlusLineCount {
		public string Line="";
		public string Description="";
		///<summary>Visits (patient and date) with at least one of the line's services.</summary>
		public int Visits;
		///<summary>Distinct patients with at least one of the line's services.</summary>
		public int Patients;
	}

	///<summary>One row of the patient-level file.</summary>
	public class UdsPlusVisitRow {
		public long PatNum;
		public DateTime DateOfBirth;
		public string Sex="";
		public string Zip="";
		public string Race="";
		public string RaceCodes="";
		public string Ethnicity="";
		public string EthnicityCodes="";
		public string PreferredLanguage="";
		public string InsuranceType="";
		public string InsuranceFilingCode="";
		public DateTime VisitDate;
		public string ProcCodes="";
		public string UdsLine="";
		public string UdsService="";
	}
}
