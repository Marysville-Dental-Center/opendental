using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness{
	///<summary></summary>
	public class PerioMeasures{
		public static List<PerioMeasure> GetForPatient(long patNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<PerioMeasure>>(MethodBase.GetCurrentMethod(),patNum);
			}
			string command =
				"SELECT periomeasure.*"
				+" FROM periomeasure,perioexam"
				+" WHERE periomeasure.PerioExamNum = perioexam.PerioExamNum"
				+" AND perioexam.PatNum = "+POut.Long(patNum);
			return Crud.PerioMeasureCrud.SelectMany(command);
		}

		///<summary>Does nothing for a calculated sequence type (CAL, AttGing), which is never stored.</summary>
		public static void Update(PerioMeasure perioMeasure){
			if(IsCalculatedSequence(perioMeasure.SequenceType)) {
				return;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),perioMeasure);
				return;
			}
			Crud.PerioMeasureCrud.Update(perioMeasure);
			//3-10-10 A bug that only lasted for a few weeks has resulted in a number of duplicate entries for each tooth.
			//So we need to clean up duplicates as we go.  Might put in db maint later.
			string command="DELETE FROM periomeasure WHERE "
				+ "PerioExamNum = "+POut.Long(perioMeasure.PerioExamNum)
				+" AND SequenceType = "+POut.Long((int)perioMeasure.SequenceType)
				+" AND IntTooth = "+POut.Long(perioMeasure.IntTooth)
				+" AND PerioMeasureNum != "+POut.Long(perioMeasure.PerioMeasureNum);
			Db.NonQ(command);
		}

		///<summary>A calculated sequence type (CAL, AttGing) is never stored: nothing is inserted and 0 is returned.</summary>
		public static long Insert(PerioMeasure perioMeasure) {
			if(IsCalculatedSequence(perioMeasure.SequenceType)) {
				return 0;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				perioMeasure.PerioMeasureNum=Meth.GetLong(MethodBase.GetCurrentMethod(),perioMeasure);
				return perioMeasure.PerioMeasureNum;
			}
			return Crud.PerioMeasureCrud.Insert(perioMeasure);
		}

		///<summary>Measures of a calculated sequence type (CAL, AttGing) are left out; they are never stored.</summary>
		public static void InsertMany(List<PerioMeasure> listPerioMeasures) {
			listPerioMeasures=listPerioMeasures.FindAll(x => !IsCalculatedSequence(x.SequenceType));
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listPerioMeasures);
				return;
			}
			Crud.PerioMeasureCrud.InsertMany(listPerioMeasures);
		}

		///<summary></summary>
		public static void Delete(PerioMeasure perioMeasure){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),perioMeasure);
				return;
			}
			string command= "DELETE from periomeasure WHERE PerioMeasureNum = '"
				+perioMeasure.PerioMeasureNum.ToString()+"'";
			Db.NonQ(command);
		}

		///<summary>New measures of a calculated sequence type (CAL, AttGing) are left out; they are never stored.</summary>
		public static bool Sync(List<PerioMeasure> listPerioMeasuresNew,List<PerioMeasure> listPerioMeasuresOld) {
			listPerioMeasuresNew=listPerioMeasuresNew.FindAll(x => !IsCalculatedSequence(x.SequenceType));
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),listPerioMeasuresNew,listPerioMeasuresOld);
			}
			return Crud.PerioMeasureCrud.Sync(listPerioMeasuresNew,listPerioMeasuresOld);
		}

		///<summary>For the current exam, clears existing skipped teeth and resets them to the specified skipped teeth. The ArrayList valid values are 1-32 int.</summary>
		public static void SetSkipped(long perioExamNum,List<int> listSkippedTeeth) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),perioExamNum,listSkippedTeeth);
				return;
			}
			//for(int i=0;i<skippedTeeth.Count;i++){
			//MessageBox.Show(skippedTeeth[i].ToString());
			//}
			//first, delete all skipped teeth for this exam
			string command = "DELETE from periomeasure WHERE "
				+"PerioExamNum = "+perioExamNum.ToString()+" "
				+"AND SequenceType = '"+POut.Long((int)PerioSequenceType.SkipTooth)+"'";
			Db.NonQ(command);
			//then add the new ones in one at a time.
			PerioMeasure perioMeasure;
			//There should only be one periomeasure entry per skipped tooth.
			List<int> listDistinctTeeth=listSkippedTeeth.Distinct().ToList();
			for(int i=0;i<listDistinctTeeth.Count;i++) {
				perioMeasure=new PerioMeasure();
				perioMeasure.PerioExamNum=perioExamNum;
				perioMeasure.SequenceType=PerioSequenceType.SkipTooth;
				perioMeasure.IntTooth=listDistinctTeeth[i];
				perioMeasure.ToothValue=1;
				perioMeasure.MBvalue=-1;
				perioMeasure.Bvalue=-1;
				perioMeasure.DBvalue=-1;
				perioMeasure.MLvalue=-1;
				perioMeasure.Lvalue=-1;
				perioMeasure.DLvalue=-1;
				Insert(perioMeasure);
			}
		}

		///<summary>Used in FormPerio.Add_Click. For the specified exam, gets a list of all skipped teeth. The ArrayList valid values are 1-32 int.</summary>
		public static List<int> GetSkipped(long perioExamNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<int>>(MethodBase.GetCurrentMethod(),perioExamNum);
			}
			string command = "SELECT IntTooth FROM periomeasure WHERE "
				+"SequenceType = '"+POut.Int((int)PerioSequenceType.SkipTooth)+"' "
				+"AND PerioExamNum = '"+perioExamNum.ToString()+"' "
				+"AND ToothValue = '1'";
			DataTable tableSkippedTeeth=Db.GetTable(command);
			List<int> listSkippedTeeth=new List<int>();
			for(int i=0;i<tableSkippedTeeth.Rows.Count;i++){
				listSkippedTeeth.Add(PIn.Int(tableSkippedTeeth.Rows[i][0].ToString()));
			}
			return listSkippedTeeth;
		}

		///<summary>Get a list of periomeasures from the db.</summary>
		public static List<PerioMeasure> GetPerioMeasuresForApi(int limit,int offset,long perioExamNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<PerioMeasure>>(MethodBase.GetCurrentMethod(),limit,offset,perioExamNum);
			}
			string command="SELECT * FROM periomeasure ";
			if(perioExamNum>0) {
				command+="WHERE PerioExamNum="+POut.Long(perioExamNum)+" ";
			}
			command+="ORDER BY PerioMeasureNum "
				+"LIMIT "+POut.Int(offset)+", "+POut.Int(limit)+" ";
			return Crud.PerioMeasureCrud.SelectMany(command);
		}

		///<summary>Gets a PerioMeasure by PerioMeasureNum from the database. Returns null if not found.</summary>
		public static PerioMeasure GetOne(long perioMeasureNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<PerioMeasure>(MethodBase.GetCurrentMethod(),perioMeasureNum);
			}
			string command="SELECT * FROM periomeasure "
				+"WHERE PerioMeasureNum = "+POut.Long(perioMeasureNum);
			return Crud.PerioMeasureCrud.SelectOne(command);
		}
		
		public static List<PerioMeasure> GetAllForExam(long perioExamNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<PerioMeasure>>(MethodBase.GetCurrentMethod(),perioExamNum);
			}
			string command ="SELECT * FROM periomeasure "
				+"WHERE PerioExamNum = "+POut.Long(perioExamNum);
			return Crud.PerioMeasureCrud.SelectMany(command);
		}

		///<summary>A -1 will be changed to a 0. Measures over 100 are changed to 100-measure. i.e. 100-104=-4 for hyperplastic GM.</summary>
		public static int AdjustGMVal(int measure) {
			Meth.NoCheckMiddleTierRole();
			if(measure==-1) {//-1 means no measurement, null.  In the places where this method is used, we have designed it to expect a 0 in those cases.
				return 0;
			}
			else if(measure>100) {
				return 100-measure;
			}
			return measure;//no adjustments needed.
		}

		#region Perio Chart rules (Open Dental 24.4 and 25.2)
		///<summary>The preference row that holds the Perio Setup option "Use the term Recession" (Open Dental 24.4). Open Dental's name for it is not
		///in any public document, so this is the name the feature spec proposes. It is read and written by name and deliberately not registered in
		///PrefName until the name is confirmed (see tests/RegistryTests/PrefNameTests.cs). A database without the row reads as off, the 24.3
		///behavior, and the row is only written once someone turns the option on.</summary>
		public const string PrefNameUseRecessionTerm="PerioUseRecessionTerm";
		///<summary>Label of the gingival margin row, as in 24.3. Translated by the caller.</summary>
		public const string TermGingMarg="Ging Marg";
		///<summary>Label of the gingival margin row when the Recession option is on and the exam has no hyperplasia. Translated by the caller.</summary>
		public const string TermRecession="Recession";

		///<summary>True for the sequence types that the Perio Chart calculates on the fly and never stores: CAL, and AttGing (Open Dental 24.4).</summary>
		public static bool IsCalculatedSequence(PerioSequenceType perioSequenceType) {
			Meth.NoCheckMiddleTierRole();
			return perioSequenceType==PerioSequenceType.CAL || perioSequenceType==PerioSequenceType.AttGing;
		}

		///<summary>Width of attached gingiva at one site, as the Perio Chart shows it in the auto Att Ging row: the MGJ value minus the probing depth
		///of the same site. Null when either value is missing (-1 or less). Zero or below means no attached gingiva and is returned as calculated.
		///</summary>
		public static int? CalcAttachedGingiva(int mgj,int probingDepth) {
			Meth.NoCheckMiddleTierRole();
			if(mgj<0 || probingDepth<0) {
				return null;
			}
			return mgj-probingDepth;
		}

		///<summary>The auto Att Ging cell text for the MGJ and probing cell texts of one site. Blank when either cell is blank.</summary>
		public static string GetAttachedGingivaText(string textMgj,string textProbing) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(textMgj) || string.IsNullOrWhiteSpace(textProbing)) {
				return "";
			}
			int? attachedGingiva=CalcAttachedGingiva(PIn.Int(textMgj,hasExceptions:false),PIn.Int(textProbing,hasExceptions:false));
			if(attachedGingiva==null) {
				return "";
			}
			return attachedGingiva.Value.ToString();
		}

		///<summary>True if a gingival margin value, as stored or as shown in a Perio Chart cell, is hyperplasia: a margin coronal to the CEJ, stored
		///as 101 to 119 (entered with Ging Marg + or Ctrl+number).</summary>
		public static bool IsHyperplasia(int gingMarginValue) {
			Meth.NoCheckMiddleTierRole();
			return gingMarginValue>100;
		}

		///<summary>True if any of the gingival margin cell texts of an exam is hyperplasia. Blank texts are ignored.</summary>
		public static bool HasHyperplasia(IEnumerable<string> listGingMarginTexts) {
			Meth.NoCheckMiddleTierRole();
			foreach(string text in listGingMarginTexts) {
				if(string.IsNullOrWhiteSpace(text)) {
					continue;
				}
				if(IsHyperplasia(PIn.Int(text,hasExceptions:false))) {
					return true;
				}
			}
			return false;
		}

		///<summary>The label of the gingival margin row (Open Dental 24.4): Recession when the Perio Setup option is on, except that it reverts to
		///Ging Marg when the displayed exam has hyperplasia. Stored values never change; only the label does.</summary>
		public static string GetGingMarginTerm(bool useRecessionTerm,bool hasHyperplasia) {
			Meth.NoCheckMiddleTierRole();
			if(useRecessionTerm && !hasHyperplasia) {
				return TermRecession;
			}
			return TermGingMarg;
		}

		///<summary>The Perio Setup option "Use the term Recession", from the preference cache. False when the cache is not loaded or the database has no
		///row for it.</summary>
		public static bool GetUseRecessionTerm() {
			Meth.NoCheckMiddleTierRole();
			bool useRecessionTerm=false;
			ODException.SwallowAnyException(() => {
				if(!Prefs.DictIsNull() && Prefs.GetContainsKey(PrefNameUseRecessionTerm)) {
					useRecessionTerm=PIn.Bool(Prefs.GetOne(PrefNameUseRecessionTerm).ValueString);
				}
			});
			return useRecessionTerm;
		}

		///<summary>Saves the Perio Setup option "Use the term Recession" and updates the local preference cache. The preference row is inserted the
		///first time the option is turned on. Returns true if the setting changed; the caller then signals other workstations
		///(DataValid.SetInvalid(InvalidType.Prefs)) and makes the Setup security log entry.</summary>
		public static bool SetUseRecessionTerm(bool useRecessionTerm) {
			Meth.NoCheckMiddleTierRole();
			if(GetUseRecessionTerm()==useRecessionTerm) {
				return false;
			}
			string valueString=POut.Bool(useRecessionTerm);
			UpsertPrefUseRecessionTerm(valueString);
			Prefs.UpdateValueForKey(new Pref { PrefName=PrefNameUseRecessionTerm,ValueString=valueString });
			return true;
		}

		///<summary>Writes the preference row of the Recession option, inserting it when the database has none. Returns true if a row was inserted or
		///its value changed. Use SetUseRecessionTerm instead, which also updates the cache.</summary>
		public static bool UpsertPrefUseRecessionTerm(string valueString) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),valueString);
			}
			string command="SELECT ValueString FROM preference WHERE PrefName='"+POut.String(PrefNameUseRecessionTerm)+"'";
			DataTable table=Db.GetTable(command);
			if(table.Rows.Count==0) {
				command="INSERT INTO preference (PrefName,ValueString) VALUES('"+POut.String(PrefNameUseRecessionTerm)+"','"+POut.String(valueString)+"')";
				Db.NonQ(command);
				return true;
			}
			if(table.Rows[0]["ValueString"].ToString()==valueString) {
				return false;
			}
			command="UPDATE preference SET ValueString='"+POut.String(valueString)+"' WHERE PrefName='"+POut.String(PrefNameUseRecessionTerm)+"'";
			Db.NonQ(command);
			return true;
		}

		///<summary>True if the tooth has an implant that has not been removed (Open Dental 24.4): a completed, existing-current or existing-other
		///procedure on the tooth whose code paints as an implant and whose tooth chart graphics are not hidden. Hiding the graphics of the implant
		///procedure is how an implant is marked removed. A newer implant procedure that is not hidden keeps the tooth an implant.</summary>
		public static bool IsImplantTooth(int intTooth,List<Procedure> listProcedures,Func<long,ProcedureCode> funcGetProcCode) {
			Meth.NoCheckMiddleTierRole();
			if(listProcedures==null) {
				return false;
			}
			string toothNum=intTooth.ToString();
			for(int i=0;i<listProcedures.Count;i++) {
				Procedure procedure=listProcedures[i];
				if(procedure.ToothNum!=toothNum || !procedure.ProcStatus.In(ProcStat.C,ProcStat.EC,ProcStat.EO)) {
					continue;
				}
				if(procedure.HideGraphics) {//the implant was removed
					continue;
				}
				ProcedureCode procedureCode=funcGetProcCode(procedure.CodeNum);
				if(procedureCode!=null && procedureCode.PaintType==ToothPaintingType.Implant) {
					return true;
				}
			}
			return false;
		}

		///<summary>The patient's birthdate as printed on the Graphical Perio Chart (Open Dental 25.2): the short date of the current culture, or blank
		///when the patient has no birthdate (a year before 1880).</summary>
		public static string GetBirthdateForPrint(DateTime birthdate) {
			Meth.NoCheckMiddleTierRole();
			if(birthdate.Year<1880) {
				return "";
			}
			return birthdate.ToShortDateString();
		}
		#endregion Perio Chart rules (Open Dental 24.4 and 25.2)

		#region Perio Chart rules (Open Dental 26.2)
		///<summary>True if every one of the sites has all of the flags. False when there are no sites. Adding a flag to the selected teeth (26.2)
		///removes it instead when this is true.</summary>
		public static bool IsFlagOnEverySite(IEnumerable<BleedingFlags> listBleedingFlagsSites,BleedingFlags bleedingFlags) {
			Meth.NoCheckMiddleTierRole();
			if(bleedingFlags==BleedingFlags.None || listBleedingFlagsSites==null) {
				return false;
			}
			bool hasSite=false;
			foreach(BleedingFlags bleedingFlagsSite in listBleedingFlagsSites) {
				hasSite=true;
				if((bleedingFlagsSite & bleedingFlags)!=bleedingFlags) {
					return false;
				}
			}
			return hasSite;
		}

		///<summary>The flags of a site after adding (or, with doRemove, removing) the given flags. The site's other flags stay.</summary>
		public static BleedingFlags GetBleedingWithFlag(BleedingFlags bleedingFlagsSite,BleedingFlags bleedingFlags,bool doRemove) {
			Meth.NoCheckMiddleTierRole();
			if(doRemove) {
				return bleedingFlagsSite & ~bleedingFlags;
			}
			return bleedingFlagsSite | bleedingFlags;
		}

		///<summary>The user's Perio Chart setting "Show previous exams" (Open Dental 26.2): the opposite of the userodpref PerioCurrentExamOnly, which
		///Open Dental 26.2 describes as showing values, both probing and non-probing, from the current exam only. True (shown) when the user has no
		///row, as before. Reads the userodpref cache.</summary>
		public static bool GetShowPreviousExams(long userNum) {
			Meth.NoCheckMiddleTierRole();
			return !GetUserOdPrefBool(userNum,UserOdFkeyType.PerioCurrentExamOnly);
		}

		///<summary>The user's Perio Chart setting "Show previous probing values" (Open Dental 26.2): the opposite of the userodpref
		///PerioShowCurrentProbingOnly, which shows only probing values from the current exam. True (shown) when the user has no row. Reads the
		///userodpref cache.</summary>
		public static bool GetShowPreviousProbing(long userNum) {
			Meth.NoCheckMiddleTierRole();
			return !GetUserOdPrefBool(userNum,UserOdFkeyType.PerioShowCurrentProbingOnly);
		}

		///<summary>Saves the user's two Perio Chart settings of Open Dental 26.2 in the userodpref rows PerioCurrentExamOnly and
		///PerioShowCurrentProbingOnly (each the opposite of its check box). A missing row is inserted, as the Perio Chart has always done for
		///PerioCurrentExamOnly when it closes. Returns true if a row was inserted or changed; the caller then signals the userodpref cache
		///(DataValid.SetInvalid(InvalidType.UserOdPrefs)).</summary>
		public static bool SaveShowPrevious(long userNum,bool showPreviousExams,bool showPreviousProbing) {
			Meth.NoCheckMiddleTierRole();
			bool isChanged=UpsertUserOdPrefBool(userNum,UserOdFkeyType.PerioCurrentExamOnly,!showPreviousExams);
			isChanged|=UpsertUserOdPrefBool(userNum,UserOdFkeyType.PerioShowCurrentProbingOnly,!showPreviousProbing);
			return isChanged;
		}

		///<summary>The value of the user's first userodpref row of the type, false when there is none.</summary>
		private static bool GetUserOdPrefBool(long userNum,UserOdFkeyType userOdFkeyType) {
			UserOdPref userOdPref=UserOdPrefs.GetByUserAndFkeyType(userNum,userOdFkeyType).FirstOrDefault();
			if(userOdPref==null) {
				return false;
			}
			return PIn.Bool(userOdPref.ValueString);
		}

		///<summary>Inserts the user's row of the type, or updates the first one when its value differs. Returns true if the database changed.</summary>
		private static bool UpsertUserOdPrefBool(long userNum,UserOdFkeyType userOdFkeyType,bool value) {
			UserOdPref userOdPref=UserOdPrefs.GetByUserAndFkeyType(userNum,userOdFkeyType).FirstOrDefault();
			if(userOdPref==null) {
				UserOdPrefs.Insert(new UserOdPref {
					UserNum=userNum,
					FkeyType=userOdFkeyType,
					ValueString=POut.Bool(value)
				});
				return true;
			}
			UserOdPref userOdPrefOld=userOdPref.Clone();
			userOdPref.ValueString=POut.Bool(value);
			return UserOdPrefs.Update(userOdPref,userOdPrefOld);
		}
		#endregion Perio Chart rules (Open Dental 26.2)

	}
	
	

}















