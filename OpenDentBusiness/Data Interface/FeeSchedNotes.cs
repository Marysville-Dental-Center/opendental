using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Dated notes on fee schedules (table feeschednote, added in Open Dental 24.4). Offices use them to record why and when fees changed. A note
	///can be limited to some clinics; an empty clinic list means every clinic.</summary>
	public class FeeSchedNotes {
		#region Get Methods
		///<summary>All notes of one fee schedule, newest DateEntry first.</summary>
		public static List<FeeSchedNote> GetForFeeSched(long feeSchedNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<FeeSchedNote>>(MethodBase.GetCurrentMethod(),feeSchedNum);
			}
			string command="SELECT * FROM feeschednote WHERE FeeSchedNum="+POut.Long(feeSchedNum)+" "
				+"ORDER BY DateEntry DESC,FeeSchedNoteNum DESC";
			return Crud.FeeSchedNoteCrud.SelectMany(command);
		}

		///<summary>Notes of one fee schedule that apply to a clinic, newest first. See FilterForClinic.</summary>
		public static List<FeeSchedNote> GetForFeeSchedAndClinic(long feeSchedNum,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			return FilterForClinic(GetForFeeSched(feeSchedNum),clinicNum);
		}

		///<summary>Gets one note from the database. Returns null if not found.</summary>
		public static FeeSchedNote GetOne(long feeSchedNoteNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<FeeSchedNote>(MethodBase.GetCurrentMethod(),feeSchedNoteNum);
			}
			return Crud.FeeSchedNoteCrud.SelectOne(feeSchedNoteNum);
		}
		#endregion Get Methods

		#region Modification Methods
		///<summary>Sets SecUserNumEntry to the current user; the database sets SecDateEntry. Returns the new key.</summary>
		public static long Insert(FeeSchedNote feeSchedNote) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				feeSchedNote.FeeSchedNoteNum=Meth.GetLong(MethodBase.GetCurrentMethod(),feeSchedNote);
				return feeSchedNote.FeeSchedNoteNum;
			}
			//Security.CurUser.UserNum gets set on MT by the DtoProcessor so it matches the user from the client WS.
			feeSchedNote.SecUserNumEntry=Security.CurUser?.UserNum??0;
			feeSchedNote.ClinicNums=feeSchedNote.ClinicNums??"";
			feeSchedNote.Note=feeSchedNote.Note??"";
			return Crud.FeeSchedNoteCrud.Insert(feeSchedNote);
		}

		///<summary>SecUserNumEntry and SecDateEntry are never changed by an update.</summary>
		public static void Update(FeeSchedNote feeSchedNote) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),feeSchedNote);
				return;
			}
			feeSchedNote.ClinicNums=feeSchedNote.ClinicNums??"";
			feeSchedNote.Note=feeSchedNote.Note??"";
			Crud.FeeSchedNoteCrud.Update(feeSchedNote);
		}

		public static void Delete(long feeSchedNoteNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),feeSchedNoteNum);
				return;
			}
			Crud.FeeSchedNoteCrud.Delete(feeSchedNoteNum);
		}

		///<summary>Deletes every note of a fee schedule. Returns the number deleted.</summary>
		public static long DeleteForFeeSched(long feeSchedNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),feeSchedNum);
			}
			string command="DELETE FROM feeschednote WHERE FeeSchedNum="+POut.Long(feeSchedNum);
			return Db.NonQ(command);
		}

		///<summary>Deletes notes whose fee schedule no longer exists. Called when fee schedules are deleted. Returns the number deleted.</summary>
		public static long DeleteOrphans() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod());
			}
			string command="DELETE FROM feeschednote WHERE FeeSchedNum NOT IN (SELECT FeeSchedNum FROM feesched)";
			return Db.NonQ(command);
		}
		#endregion Modification Methods

		#region Misc Methods
		///<summary>The clinics of a note. Empty means every clinic. Ignores blanks, zeros and anything that is not a number.</summary>
		public static List<long> GetClinicNums(FeeSchedNote feeSchedNote) {
			Meth.NoCheckMiddleTierRole();
			return ParseClinicNums(feeSchedNote?.ClinicNums);
		}

		///<summary>Parses a comma-separated list of clinic numbers. Empty means every clinic. Ignores blanks, zeros, duplicates and anything that is not a
		///number.</summary>
		public static List<long> ParseClinicNums(string clinicNums) {
			Meth.NoCheckMiddleTierRole();
			List<long> listClinicNums=new List<long>();
			if(string.IsNullOrWhiteSpace(clinicNums)) {
				return listClinicNums;
			}
			string[] arrayParts=clinicNums.Split(',');
			for(int i=0;i<arrayParts.Length;i++) {
				if(!long.TryParse(arrayParts[i].Trim(),out long clinicNum) || clinicNum<=0 || listClinicNums.Contains(clinicNum)) {
					continue;
				}
				listClinicNums.Add(clinicNum);
			}
			return listClinicNums;
		}

		///<summary>The comma-separated form stored in feeschednote.ClinicNums, sorted, without zeros or duplicates. Empty for every clinic.</summary>
		public static string ClinicNumsToString(List<long> listClinicNums) {
			Meth.NoCheckMiddleTierRole();
			if(listClinicNums==null) {
				return "";
			}
			return string.Join(",",listClinicNums.Where(x => x>0).Distinct().OrderBy(x => x).Select(x => POut.Long(x)));
		}

		///<summary>True if the note applies to the clinic: notes for every clinic always do, others when their list contains it.</summary>
		public static bool AppliesToClinic(FeeSchedNote feeSchedNote,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			List<long> listClinicNums=GetClinicNums(feeSchedNote);
			return listClinicNums.Count==0 || listClinicNums.Contains(clinicNum);
		}

		///<summary>With a clinic filter (clinicNum 0 or more), keeps the notes whose list contains that clinic plus the notes for every clinic. A clinicNum
		///of -1 means no filter. Keeps the order.</summary>
		public static List<FeeSchedNote> FilterForClinic(List<FeeSchedNote> listFeeSchedNotes,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			if(clinicNum<0) {
				return new List<FeeSchedNote>(listFeeSchedNotes);
			}
			return listFeeSchedNotes.FindAll(x => AppliesToClinic(x,clinicNum));
		}

		///<summary>Sorts newest DateEntry first, then newest row first, the order the Fee Schedule Notes window shows.</summary>
		public static List<FeeSchedNote> SortForDisplay(List<FeeSchedNote> listFeeSchedNotes) {
			Meth.NoCheckMiddleTierRole();
			return listFeeSchedNotes.OrderByDescending(x => x.DateEntry).ThenByDescending(x => x.FeeSchedNoteNum).ToList();
		}

		///<summary>The first line of the note, cut to maxLength characters with "..." when longer. For the notes grid.</summary>
		public static string GetNoteSummary(FeeSchedNote feeSchedNote,int maxLength=100) {
			Meth.NoCheckMiddleTierRole();
			string note=(feeSchedNote?.Note??"").Replace("\r\n","\n").Trim();
			string firstLine=note.Split('\n')[0].TrimEnd();
			bool isCut=(note.Length>firstLine.Length);
			if(firstLine.Length>maxLength) {
				firstLine=firstLine.Substring(0,maxLength);
				isCut=true;
			}
			return firstLine+(isCut ? "..." : "");
		}

		///<summary>Makes the FeeSchedEdit security log entry for a note: "Fee schedule note added for Standard" and the like. action is "added",
		///"edited" or "deleted".</summary>
		public static void MakeLogEntry(FeeSchedNote feeSchedNote,string action) {
			Meth.NoCheckMiddleTierRole();
			string log=Lans.g("FeeSchedNotes","Fee schedule note")+" "+action+" "+Lans.g("FeeSchedNotes","for")+" "
				+FeeScheds.GetDescription(feeSchedNote.FeeSchedNum)
				+" ("+Lans.g("FeeSchedNotes","note date")+" "+feeSchedNote.DateEntry.ToString("yyyy-MM-dd")+")";
			SecurityLogs.MakeLogEntry(EnumPermType.FeeSchedEdit,0,log,feeSchedNote.FeeSchedNum,DateTime.MinValue);
		}
		#endregion Misc Methods
	}
}
