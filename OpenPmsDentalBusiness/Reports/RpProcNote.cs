using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	public class RpProcNote {
		///<summary>B-SERVER (v24_4-reports-incomplete-procnotes-all): the Report Setup default for "Include all procedures with no notes". A
		///fork-owned preference row, not in the PrefName enum, because Open Dental's own key is not public. A missing row reads as false.</summary>
		public const string PREF_ALL_NO_NOTES="ForkReportsIncompleteProcsAllNoNotes";
			
		///<summary>The Report Setup default for "Include all procedures with no notes". Never throws.</summary>
		public static bool GetAllNoNotesDefault() {
			return PIn.Bool(PrefC.GetRawOrDefault(PREF_ALL_NO_NOTES,"0"));
		}

		///<summary>Stores the Report Setup default for "Include all procedures with no notes". Returns true if it changed. Writes nothing when the
		///value is unchanged, so a database without the row keeps having none until the box is first checked.</summary>
		public static bool SetAllNoNotesDefault(bool isChecked) {
			Meth.NoCheckMiddleTierRole();
			if(GetAllNoNotesDefault()==isChecked) {
				return false;
			}
			return PrefC.UpsertRaw(PREF_ALL_NO_NOTES,POut.Bool(isChecked));
		}

		///<summary>includeAllNoNotes (24.4): also list every completed procedure whose own latest note is blank or missing, not only the ones on a day
		///where no procedure has a note. It implies includeNoNotes.</summary>
		public static DataTable GetData(List<long> listProvNums,List<long> listClinicNums,DateTime dateStart,DateTime dateEnd,bool includeNoNotes,
			bool includeUnsignedNotes,ToothNumberingNomenclature toothNumberFormat,ProcNoteGroupBy groupBy, bool showExcludedCodes = false,
			bool includeAllNoNotes=false)
		{
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),listProvNums,listClinicNums,dateStart,dateEnd,includeNoNotes,includeUnsignedNotes,
					toothNumberFormat,groupBy, showExcludedCodes,includeAllNoNotes);
			}
			string [] arrayExcludedCodes = PrefName.ReportsIncompleteProcsExcludeCodes.GetValueAsText().Split(",",StringSplitOptions.RemoveEmptyEntries);
			string command=GetQuery(listProvNums,listClinicNums,dateStart,dateEnd,includeNoNotes,includeUnsignedNotes,groupBy,showExcludedCodes,
				arrayExcludedCodes,includeAllNoNotes);
			DataTable table=ReportsComplex.RunFuncOnReportServer(() => Db.GetTable(command));
			foreach(DataRow row in table.Rows) {
				row["ToothNum"]=Tooth.Display(row["ToothNum"].ToString(),toothNumberFormat);
			}
			return table;
		}

		///<summary>The report query. Public so it can be tested without the preference cache. arrayExcludedCodes is the
		///ReportsIncompleteProcsExcludeCodes list, used unless showExcludedCodes is true.
		///<para>includeAllNoNotes (24.4): a procedure has no note when its latest procnote row has blank text or it has no procnote row, and it is
		///not part of a group note whose latest note has text (INFERRED: a group note covers its procedures). The same-day option is implied, because
		///every procedure it finds also has no note of its own. The 24.3 rule that a same-day note (even an incomplete one) hides the procedure does
		///not apply.</para></summary>
		public static string GetQuery(List<long> listProvNums,List<long> listClinicNums,DateTime dateStart,DateTime dateEnd,bool includeNoNotes,
			bool includeUnsignedNotes,ProcNoteGroupBy groupBy,bool showExcludedCodes,string[] arrayExcludedCodes,bool includeAllNoNotes=false)
		{
			if(includeAllNoNotes) {
				includeNoNotes=true;
			}
			arrayExcludedCodes=arrayExcludedCodes??new string[0];
			string whereNoNote="";
			string whereUnsignedNote="";
			string whereNotesClause="";
			//Per-procedure test used by includeAllNoNotes: true when the procedure has no note of its own and no group note covers it.
			string hasNoOwnNote="((lastNote.ProcNum IS NULL OR TRIM(COALESCE(lastNote.Note,''))='') AND groupNoted.ProcNum IS NULL)";
			//For the same-day option (24.3): true when no procedure of the patient on that day has a note.
			string hasNoNoteCondition="hasNotes.PatNum IS NULL";
			if(includeAllNoNotes) {
				hasNoNoteCondition=hasNoOwnNote;
				whereNoNote=@"
					LEFT JOIN procnote lastNote ON lastNote.ProcNum=procedurelog.ProcNum
						AND lastNote.EntryDateTime=(SELECT MAX(n3.EntryDateTime)
							FROM procnote n3
							WHERE n3.ProcNum=lastNote.ProcNum)
					LEFT JOIN (
						SELECT DISTINCT procgroupitem.ProcNum
						FROM procgroupitem
						INNER JOIN procedurelog groupItem ON groupItem.ProcNum=procgroupitem.ProcNum
							AND groupItem.ProcDate BETWEEN "+POut.Date(dateStart)+" AND "+POut.Date(dateEnd)+@"
						INNER JOIN procedurelog groupProc ON groupProc.ProcNum=procgroupitem.GroupNum
							AND groupProc.ProcStatus!="+POut.Int((int)ProcStat.D)+@"
						INNER JOIN procnote groupNote ON groupNote.ProcNum=procgroupitem.GroupNum
							AND groupNote.EntryDateTime=(SELECT MAX(gn2.EntryDateTime)
								FROM procnote gn2
								WHERE gn2.ProcNum=groupNote.ProcNum)
						WHERE TRIM(groupNote.Note)!=''
					) groupNoted ON groupNoted.ProcNum=procedurelog.ProcNum ";
			}
			else if(includeNoNotes) {
				whereNoNote=@"
					LEFT JOIN (
						SELECT procedurelog.PatNum,procedurelog.ProcDate,procnote.Note
						FROM procedurelog
						INNER JOIN procnote ON procnote.ProcNum=procedurelog.ProcNum
						INNER JOIN procedurecode ON procedurecode.CodeNum=procedurelog.CodeNum
							AND procedurecode.ProcCode NOT IN ('D9986','D9987')
						WHERE procedurelog.ProcDate BETWEEN "+POut.Date(dateStart)+" AND "+POut.Date(dateEnd)+@"
						AND (procedurelog.ProcStatus="+POut.Int((int)ProcStat.C)+" OR (procedurelog.ProcStatus="+POut.Int((int)ProcStat.EC)
						+@" AND procedurecode.ProcCode='~GRP~'))"
						+@" AND procnote.EntryDateTime=(SELECT MAX(lastnote.EntryDateTime) 
							FROM procnote lastnote 
							WHERE procnote.ProcNum=lastnote.ProcNum)"
						+@" GROUP BY procedurelog.ProcNum
							HAVING LENGTH(procnote.Note)>0
					) hasNotes ON hasNotes.PatNum=procedurelog.PatNum AND hasNotes.ProcDate=procedurelog.ProcDate ";
			}
			if(includeNoNotes) {
				whereNotesClause=@"AND IF(procedurecode.ProcCode IN ('D9986','D9987'),(n1.Note LIKE '%""""%' OR n1.Note REGEXP '\\[Prompt:""[a-zA-Z_0-9]+""\\]'),TRUE) "; // if using no notes option, only show broken appointment codes that have empty quotes or prompts
				if(!includeUnsignedNotes) {
					whereNotesClause+="AND (n1.ProcNum IS NOT NULL OR "+hasNoNoteCondition+")";
				}
			}
			if(includeUnsignedNotes) {
				if(includeNoNotes) {
					whereNotesClause+="AND (n1.ProcNum IS NOT NULL OR "+hasNoNoteCondition+" OR unsignedNotes.ProcNum IS NOT NULL)";
				}
				else {
					whereNotesClause+="AND (n1.ProcNum IS NOT NULL OR unsignedNotes.ProcNum IS NOT NULL)";
				}
				//26.1 (M2-NOTES): a group note signed only in its second box counts as signed, as in the Chart. Signature2 is NULL on rows written
				//before the column existed.
				whereUnsignedNote=@"
					LEFT JOIN procnote unsignedNotes ON unsignedNotes.ProcNum=procedurelog.ProcNum
						AND unsignedNotes.Signature=''
						AND COALESCE(unsignedNotes.Signature2,'')=''
						AND	TRIM(unsignedNotes.Note)!=''
						AND unsignedNotes.EntryDateTime= (SELECT MAX(n2.EntryDateTime) 
								FROM procnote n2 
								WHERE unsignedNotes.ProcNum = n2.ProcNum) ";
			}
			string selectHasNoNote=",(CASE WHEN MAX(hasNotes.PatNum) IS NULL THEN 'X' ELSE '' END) AS HasNoNote ";
			if(includeAllNoNotes) {
				selectHasNoNote=",(CASE WHEN MAX("+hasNoOwnNote+")=1 THEN 'X' ELSE '' END) AS HasNoNote ";
			}
			//COUNT(DISTINCT ...): the note joins can return a procedure more than once (the same-day join once per noted procedure of that day), which
			//made 24.3 show a single procedure as "Multiple procedures" with no code.
			string command=@"SELECT MAX(procedurelog.ProcDate) ProcDate,MAX(CONCAT(CONCAT(patient.LName, ', '),patient.FName)) PatName,procedurelog.PatNum,
				(CASE WHEN COUNT(DISTINCT procedurelog.ProcNum)=1 THEN MAX(procedurecode.ProcCode) ELSE '' END) ProcCode,
				(CASE WHEN COUNT(DISTINCT procedurelog.ProcNum)=1 THEN MAX(procedurecode.Descript) ELSE '"+Lans.g("FormRpProcNote","Multiple procedures")+@"' END) Descript,
				(CASE WHEN COUNT(DISTINCT procedurelog.ProcNum)=1 THEN MAX(procedurelog.ToothNum) ELSE '' END) ToothNum,
				(CASE WHEN COUNT(DISTINCT procedurelog.ProcNum)=1 THEN MAX(procedurelog.Surf) ELSE '' END) Surf "
				+(includeNoNotes || includeUnsignedNotes?",(CASE WHEN MAX(n1.ProcNum) IS NOT NULL THEN 'X' ELSE '' END) AS Incomplete ":"")
				+(includeNoNotes?selectHasNoNote:"")
				+(includeUnsignedNotes?",(CASE WHEN MAX(unsignedNotes.ProcNum) IS NOT NULL THEN 'X' ELSE '' END) AS HasUnsignedNote ":"")+@" 
				FROM procedurelog
				INNER JOIN patient ON procedurelog.PatNum = patient.PatNum 
				INNER JOIN procedurecode ON procedurelog.CodeNum = procedurecode.CodeNum 
				"+(includeNoNotes || includeUnsignedNotes?"LEFT":"INNER")+@" JOIN procnote n1 ON procedurelog.ProcNum = n1.ProcNum 
					AND (n1.Note LIKE '%""""%' OR n1.Note REGEXP '"+@"\\[Prompt:""[a-zA-Z_0-9 ]+""\\]') "//looks for either "" (pre 17.3) or [Prompt:"{word}"] (post 17.3)
				+@" AND n1.EntryDateTime= (SELECT MAX(n2.EntryDateTime) 
				FROM procnote n2 
				WHERE n1.ProcNum = n2.ProcNum) "
				+whereNoNote+" "
				+whereUnsignedNote+@"
				WHERE procedurelog.ProcDate BETWEEN "+POut.Date(dateStart)+" AND "+POut.Date(dateEnd)+@"
				AND (procedurelog.ProcStatus="+POut.Int((int)ProcStat.C)
				+" OR (procedurelog.ProcStatus="+POut.Int((int)ProcStat.EC)+" "
				+@" AND procedurecode.ProcCode='~GRP~')) ";
				if(!showExcludedCodes) {
					command+=$"AND procedurecode.ProcCode NOT IN ('{string.Join("','",arrayExcludedCodes.Select(x => POut.String(x)))}') ";
				}
				command+=whereNotesClause;
			if(listProvNums.Count>0) {
				command+=@"AND procedurelog.ProvNum IN ("+String.Join(",",listProvNums)+") ";
			}
			if(listClinicNums.Count>0) {
				command+=@"AND procedurelog.ClinicNum IN ("+String.Join(",",listClinicNums)+") ";
			}
			if(groupBy==ProcNoteGroupBy.Patient) {
				command+=@"GROUP BY procedurelog.PatNum ";
			}
			else if(groupBy==ProcNoteGroupBy.DateAndPatient) {
				command+=@"GROUP BY procedurelog.ProcDate,procedurelog.PatNum ";
			}
			else {
				command+="GROUP BY procedurelog.ProcNum ";
			}
			command+=@"ORDER BY ProcDate, LName";
			return command;
		}

		///<summary>The different ways this report can be grouped.</summary>
		public enum ProcNoteGroupBy {
			///<summary>0</summary>
			Procedure,
			///<summary>1</summary>
			Patient,
			///<summary>2</summary>
			DateAndPatient
		}
	}
}
