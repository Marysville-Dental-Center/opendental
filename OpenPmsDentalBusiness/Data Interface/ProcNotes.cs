using System;
using System.Data;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenPmsDentalBusiness {
	public class ProcNotes{
		#region Get Methods
		///<summary>Returns all procedure notes for the procedure passed in.  Returned list is not sorted.</summary>
		public static List<ProcNote> GetProcNotesForProc(long procNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<ProcNote>>(MethodBase.GetCurrentMethod(),procNum);
			}
			string command="SELECT * FROM procnote WHERE ProcNum="+POut.Long(procNum);
			return Crud.ProcNoteCrud.SelectMany(command);
		}

		///<summary>The newest note of the procedure (latest EntryDateTime, then highest ProcNoteNum for notes saved in the same second), or null when it
		///has none. Procedure Info and the Group Note window remember it when they open and read it again when they save (Procedure Note Merge).</summary>
		public static ProcNote GetNewestForProc(long procNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<ProcNote>(MethodBase.GetCurrentMethod(),procNum);
			}
			if(procNum==0) {
				return null;
			}
			string command="SELECT * FROM procnote WHERE ProcNum="+POut.Long(procNum)+" ORDER BY EntryDateTime DESC,ProcNoteNum DESC";
			command=DbHelper.LimitOrderBy(command,1);
			return Crud.ProcNoteCrud.SelectOne(command);
		}
		#endregion

		#region Note rows
		///<summary>True when saving procedure over procedureOld writes a new procnote row (Procedures.Update): the note text differs, or its user, its
		///signature or (26.1) the second signature changed. The text is compared exactly, as it always was, so a note stored with \r\n line breaks (the
		///CRUD stores every note that way, POut.StringNote) and read back from a RichTextBox with \n counts as changed. Procedures.Update and the
		///Procedure Note Merge check (ProcNoteMerges.IsNoteRowWritten)
		///both use this, so the windows ask exactly when the save writes a row (M2-NOTES).</summary>
		public static bool IsNoteRowNeeded(Procedure procedure,Procedure procedureOld) {
			return procedure.Note!=procedureOld.Note
				|| procedure.UserNum!=procedureOld.UserNum
				|| procedure.SigIsTopaz!=procedureOld.SigIsTopaz
				|| procedure.Signature!=procedureOld.Signature
				|| IsSecondSignatureChanged(procedure,procedureOld);
		}
		#endregion Note rows

		#region Second signature (26.1)
		///<summary>True when the caller changed the second signature of a group note (procnote.UserNum2, Signature2, SigIsTopaz2): signed, cleared or
		///signed by another user. Procedures.Update then writes a new note row.</summary>
		public static bool IsSecondSignatureChanged(Procedure procedure,Procedure procedureOld) {
			return procedure.UserNum2!=procedureOld.UserNum2
				|| procedure.SigIsTopaz2!=procedureOld.SigIsTopaz2
				|| (procedure.Signature2??"")!=(procedureOld.Signature2??"");
		}

		///<summary>Sets the second signature of a note row that Procedures.Update is about to insert:
		///- The caller changed it (the Group Note window): as the caller set it.
		///- The note text changed: none. The second signature was made over the old text, so editing clears it like the first one.
		///- Otherwise (for example only the first signature changed): the second signature of the newest saved row, when that row has the same text.
		///  It is read from the database because most procedure readers do not load the second signature.</summary>
		public static void SetSecondSignature(ProcNote procNote,Procedure procedure,Procedure procedureOld) {
			procNote.UserNum2=0;
			procNote.Signature2="";
			procNote.SigIsTopaz2=false;
			if(IsSecondSignatureChanged(procedure,procedureOld)) {
				procNote.UserNum2=procedure.UserNum2;
				procNote.Signature2=procedure.Signature2??"";
				procNote.SigIsTopaz2=procedure.SigIsTopaz2;
				return;
			}
			if(ProcNoteMerges.IsNoteTextChanged(procedureOld.Note,procedure.Note)) {
				return;
			}
			ProcNote procNoteNewest=GetNewestForProc(procedure.ProcNum);
			if(procNoteNewest==null || string.IsNullOrEmpty(procNoteNewest.Signature2) || ProcNoteMerges.IsNoteTextChanged(procNoteNewest.Note,procedure.Note)) {
				return;
			}
			procNote.UserNum2=procNoteNewest.UserNum2;
			procNote.Signature2=procNoteNewest.Signature2;
			procNote.SigIsTopaz2=procNoteNewest.SigIsTopaz2;
		}

		///<summary>Copies the second signature of procNote into the procedure's not-in-database fields, for windows that loaded the procedure with a
		///reader that does not select it.</summary>
		public static void CopySecondSignatureTo(ProcNote procNote,Procedure procedure) {
			if(procNote==null) {
				return;
			}
			procedure.UserNum2=procNote.UserNum2;
			procedure.Signature2=procNote.Signature2??"";
			procedure.SigIsTopaz2=procNote.SigIsTopaz2;
		}
		#endregion Second signature (26.1)
		
		public static long Insert(ProcNote procNote){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				procNote.ProcNoteNum=Meth.GetLong(MethodBase.GetCurrentMethod(),procNote);
				return procNote.ProcNoteNum;
			}
			return Crud.ProcNoteCrud.Insert(procNote);
		}

		public static ProcNote GetProcNotesForPat(long patNum, DateTime dateStart, DateTime dateEnd) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<ProcNote>(MethodBase.GetCurrentMethod(),patNum,dateStart,dateEnd);
			}
			string query="SELECT procnote.* FROM procnote "
				+"INNER JOIN procedurelog ON procedurelog.ProcNum=procnote.ProcNum "
				+"WHERE procnote.PatNum="+POut.Long(patNum)+" "
				+"AND procnote.EntryDateTime BETWEEN "+POut.Date(dateStart)+" AND "+POut.Date(dateEnd)+" "
				+"AND procedurelog.ProcStatus!="+POut.Int((int)ProcStat.D)+" "
				+"ORDER BY procnote.EntryDateTime DESC";
			string command=DbHelper.LimitOrderBy(query,1);
			return Crud.ProcNoteCrud.SelectOne(command);
		}

		///<summary>Returns a list of ProcNums from listProcNums where the most recent ProcNote for the proc is signed. Since 26.1 a group note's second
		///signature counts too.</summary>
		public static List<long> GetIsProcNoteSigned(List<long> listProcNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod(),listProcNums);
			}
			if(listProcNums.Count==0) {
				return new List<long>();
			}
			string command="SELECT * FROM procnote WHERE ProcNum IN ("+string.Join(",",listProcNums.Select(x => POut.Long(x)))+")";
			List<ProcNote> listProcNotes=Crud.ProcNoteCrud.SelectMany(command);//get all ProcNotes with ProcNum in the supplied list
			if(listProcNotes.Count==0) {
				return new List<long>();
			}
			return listProcNotes
				.GroupBy(x => x.ProcNum,(x,y) => y.Aggregate((y1,y2) => y1.EntryDateTime>y2.EntryDateTime?y1:y2))//group by ProcNum, get most recent ProcNote
				.Where(x => !string.IsNullOrWhiteSpace(x.Signature) || !string.IsNullOrWhiteSpace(x.Signature2))//where the most recent ProcNote is signed
				.Select(x => x.ProcNum).ToList();//return list of ProcNums
		}

		///<summary>Gets a list of procnotes from the datbase. Used for API.</summary>
		public static List<ProcNote> GetProcNotesForApi(int limit,int offset,long patNum,long procNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<ProcNote>>(MethodBase.GetCurrentMethod(),limit,offset,patNum,procNum);
			}
			string command="SELECT * FROM procnote "
				+"WHERE EntryDateTime>="+POut.DateT(DateTime.MinValue)+" ";//Needed to use WHERE clause so the rest can be AND.
			if(patNum>0) {
				command+="AND procnote.PatNum="+POut.Long(patNum)+" ";
			}
			if(procNum>0) {
				command+="AND procnote.ProcNum="+POut.Long(procNum)+" ";
			}
			command+="ORDER BY procnotenum DESC "//Ensure order for limit and offset. DESC so the most recent is first, like the UI.
				+"LIMIT "+POut.Int(offset)+", "+POut.Int(limit);
			return Crud.ProcNoteCrud.SelectMany(command);
		}

		///<summary>Modifies currentNote and returns the new note string. Also checks PrefName.ProcPromptForAutoNote and remots auto notes if needed.</summary>
		public static string SetProcCompleteNoteHelper(bool isQuickAdd,Procedure procedure,Procedure procedureOld,long provNum,string currentNote="") {
			string procNoteDefault="";
			if(isQuickAdd) {//Quick Procs should insert both TP Default Note and C Default Note.
				procNoteDefault=ProcCodeNotes.GetNote(provNum,procedure.CodeNum,ProcStat.TP);
				if(!string.IsNullOrEmpty(procNoteDefault)) {
					procNoteDefault+="\r\n";
				}
			}
			if(procedureOld.ProcStatus!=ProcStat.C && procedure.ProcStatus==ProcStat.C) {//Only append the default note if the procedure changed status to Completed
				procNoteDefault+=ProcCodeNotes.GetNote(provNum,procedure.CodeNum,ProcStat.C);
				if(currentNote!="" && procNoteDefault!="") { //check to see if a default note is defined.
					currentNote+="\r\n"; //add a new line if there was already a ProcNote on the procedure.
				}
				if(!string.IsNullOrEmpty(procNoteDefault)) {
					currentNote+=procNoteDefault;
				}
			}
			if(procedure.ProcStatus==ProcStat.TP && procedureOld.ProcStatus==ProcStat.D){//Append the TP note if the user had to enter a tooth number or quadrant
				procNoteDefault+=ProcCodeNotes.GetNote(provNum,procedure.CodeNum,ProcStat.TP);
				if(currentNote!="" && procNoteDefault!="") { //check to see if a default note is defined.
					currentNote+="\r\n"; //add a new line if there was already a ProcNote on the procedure.
				}
				if(!string.IsNullOrEmpty(procNoteDefault)) {
					currentNote+=procNoteDefault;
				}
			}
			if(!PrefC.GetBool(PrefName.ProcPromptForAutoNote)) {
				//Users do not want to be prompted for auto notes, so remove them all from the procedure note.
				currentNote=Regex.Replace(currentNote,@"\[\[.+?\]\]","");
			}
			return currentNote;
		}
		
		///<summary>Get a single ProcNote from DB, returns null if not found. </summary>
		public static ProcNote GetOneProcNote(long procNoteNum) {
			if(procNoteNum==0) {
				return null;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<ProcNote>(MethodBase.GetCurrentMethod(),procNoteNum);
			}
			string command="SELECT * FROM procnote "
				+"WHERE ProcNoteNum = "+POut.Long(procNoteNum);
			return Crud.ProcNoteCrud.SelectOne(command);
		}

		///<summary>Helper method for xODApi.ProcNotes POST to use digital signature stamp logic.</summary>
		public static ProcNote ProcNoteSignatureForApi(ProcNote odbProcNote,string apiSignatureString) {
			Meth.NoCheckMiddleTierRole();
			//OpenDentBusiness.Procedures.GetSignatureKeyData()
			string keyData=odbProcNote.Note+odbProcNote.UserNum.ToString();
			keyData=keyData.Replace("\r\n","\n");//We need all newlines to be the same, a mix of \r\n and \n can invalidate the procedure signature.
			//OpenDentBusiness.UI.SignatureBox.SetKeyString()
			UTF8Encoding utf8Encoding=new UTF8Encoding();
			//OpenDental.UI.SignatureBoxWrapper.GetSignature()
			byte[] hashNew=utf8Encoding.GetBytes("0000000000000000"); //Set it to "0000000000000000" (16 zeros) to indicate no key string to be used for encryption.
			//OpenDental.UI.SignatureBox.SetAutoKeyData()
			hashNew=ODCrypt.MD5.Hash(Encoding.UTF8.GetBytes(keyData));
			//OpenDental.UI.SignatureBox.GetSigString()
			odbProcNote.Signature=UI.SigBox.EncryptSigString(hashNew,apiSignatureString);
			return odbProcNote;
		}
		
		/*
		///<summary></summary>
		internal static bool PreviousNoteExists(int procNum){
			string command="SELECT COUNT(*) FROM procnote WHERE ProcNum="+POut.PInt(procNum);
			DataConnection dcon=new DataConnection();
			if(dcon.GetCount(command)=="0"){
				return false;
			}
			return true;
		}*/
	}



}
