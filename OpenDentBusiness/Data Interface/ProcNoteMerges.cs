using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>What the user picked in the Procedure Note Merge window.</summary>
	public enum ProcNoteMergeChoice {
		///<summary>Back to the window; nothing is saved.</summary>
		Cancel,
		///<summary>Save my note as I typed it, over the note saved by the other user.</summary>
		KeepMine,
		///<summary>Keep the note saved by the other user, with its signatures. My other changes are still saved.</summary>
		KeepTheirs,
		///<summary>Save the text edited in the merge window.</summary>
		SaveMerged,
	}

	///<summary>Procedure Note Merge (Open Dental 25.4, package M2-NOTES). Procedure notes are versioned: every save inserts a procnote row. When
	///somebody else saved a newer note on the same procedure while a user had Procedure Info or the Group Note window open, saving would silently
	///put an older text back. The windows remember the newest procnote row when they open, read it again when they save, and show the merge window
	///when it changed. The rules live here so the windows only call them.</summary>
	public static class ProcNoteMerges {
		///<summary>The line between the other user's note and my added text in the default merged note. The same line the old automatic merge
		///(preference ProcNoteConcurrencyMerge) and the Chart's audit mode use.</summary>
		public const string Separator="------------------------------------------------------";

		///<summary>True when the office uses the older automatic merge (preference ProcNoteConcurrencyMerge, Procedures.SetNote). Its notes are
		///merged without asking, as before, so the merge window is not shown.</summary>
		public static bool IsAutoMergeOn() {
			return PrefC.GetBool(PrefName.ProcNoteConcurrencyMerge);
		}

		///<summary>The text with every line break written as \n, and null as "". Notes come back from text boxes with \r\n or \n.</summary>
		public static string Normalize(string note) {
			return (note??"").Replace("\r\n","\n").Replace("\r","\n");
		}

		///<summary>True when the two texts differ in more than their line breaks.</summary>
		public static bool IsNoteTextChanged(string noteBefore,string noteAfter) {
			return Normalize(noteBefore)!=Normalize(noteAfter);
		}

		///<summary>True when procNoteNow is a newer note than procNoteAtLoad, the newest note when the window opened (null when there was none):
		///another row was saved since (a different ProcNoteNum or EntryDateTime).</summary>
		public static bool IsNewerNoteSaved(ProcNote procNoteAtLoad,ProcNote procNoteNow) {
			if(procNoteNow==null) {
				return false;//Nothing saved at all. Notes are never deleted.
			}
			if(procNoteAtLoad==null) {
				return true;
			}
			return procNoteNow.ProcNoteNum!=procNoteAtLoad.ProcNoteNum || procNoteNow.EntryDateTime!=procNoteAtLoad.EntryDateTime;
		}

		///<summary>True when saving now needs the merge window: a newer note was saved since the window opened, and this save writes a new note row
		///(isNoteRowWritten: the user changed the note text, or a signature or the note's user changed, see Procedures.Update). The 25.4 rule names
		///only a changed note text; a save that writes only a signature would also put the older text back over the newer one, so it asks too.</summary>
		public static bool IsMergeNeeded(ProcNote procNoteAtLoad,ProcNote procNoteNow,bool isNoteRowWritten) {
			return isNoteRowWritten && IsNewerNoteSaved(procNoteAtLoad,procNoteNow);
		}

		///<summary>The text the user added to the note they loaded. When their note starts with the loaded note, the rest of it (without the leading
		///line breaks); otherwise, for example when they changed or removed loaded text, their whole note. Line breaks come back as \r\n.</summary>
		public static string GetAddedText(string noteLoaded,string noteMine) {
			string loaded=Normalize(noteLoaded);
			string mine=Normalize(noteMine);
			string added=mine;
			if(loaded!="" && mine.StartsWith(loaded)) {
				added=mine.Substring(loaded.Length).TrimStart('\n');
			}
			return added.Replace("\n","\r\n");
		}

		///<summary>The merged note the merge window starts with: the other user's note, a separator line, then my added text. Just their note when I
		///added nothing.</summary>
		public static string GetMergedDefault(string noteTheirs,string noteLoaded,string noteMine) {
			string theirs=Normalize(noteTheirs).Replace("\n","\r\n");
			string added=GetAddedText(noteLoaded,noteMine);
			if(added.Trim()=="") {
				return theirs;
			}
			if(theirs=="") {
				return added;
			}
			return theirs+"\r\n"+Separator+"\r\n"+added;
		}

		///<summary>The note text that is saved for a choice. Null for Cancel.</summary>
		public static string GetResultText(ProcNoteMergeChoice choice,string noteTheirs,string noteMine,string noteMerged) {
			switch(choice) {
				case ProcNoteMergeChoice.KeepMine:
					return noteMine??"";
				case ProcNoteMergeChoice.KeepTheirs:
					return noteTheirs??"";
				case ProcNoteMergeChoice.SaveMerged:
					return noteMerged??"";
				default:
					return null;
			}
		}

		///<summary>True when a signature drawn in the window over noteSigned no longer fits the text being saved, so it is cleared and the user signs
		///again. A merged text that equals the signed one keeps the signature.</summary>
		public static bool IsResignNeeded(bool isSignatureDrawn,string noteSigned,string noteSaved) {
			return isSignatureDrawn && IsNoteTextChanged(noteSigned,noteSaved);
		}

		///<summary>The other user's signers whose signatures are not carried into the saved note, so they get the "signature cleared" alert (when
		///they subscribe to it): the signer of each signed box of their note, unless that user is one of listUserNumsActing (the user at the keyboard
		///and the users switched to in the window). None for Keep Theirs or Cancel, which keep their note as it is.</summary>
		public static List<long> GetTheirSignersToAlert(ProcNoteMergeChoice choice,ProcNote procNoteTheirs,List<long> listUserNumsActing) {
			List<long> listUserNums=new List<long>();
			if(procNoteTheirs==null || choice==ProcNoteMergeChoice.Cancel || choice==ProcNoteMergeChoice.KeepTheirs) {
				return listUserNums;
			}
			if(!string.IsNullOrEmpty(procNoteTheirs.Signature) && procNoteTheirs.UserNum!=0) {
				listUserNums.Add(procNoteTheirs.UserNum);
			}
			if(!string.IsNullOrEmpty(procNoteTheirs.Signature2) && procNoteTheirs.UserNum2!=0) {
				listUserNums.Add(procNoteTheirs.UserNum2);
			}
			List<long> listActing=listUserNumsActing??new List<long>();
			return listUserNums.Distinct().Where(x => !listActing.Contains(x)).ToList();
		}

		///<summary>The permission the merge is logged under: the one the note is saved with, Edit Note on Completed Procedure for a completed note,
		///otherwise Edit Procedure.</summary>
		public static EnumPermType GetLogPermission(bool isTreatedAsComplete) {
			if(isTreatedAsComplete) {
				return EnumPermType.ProcCompleteNote;
			}
			return EnumPermType.ProcEdit;
		}

		///<summary>True for the statuses that count as a completed procedure note (C, EC, EO).</summary>
		public static bool IsCompleteStatus(ProcStat procStat) {
			return procStat.In(ProcStat.C,ProcStat.EC,ProcStat.EO);
		}

		///<summary>The security log text for a resolved merge, or "" for Cancel. procDescription names the procedure, like "D0120 9/14/2026".
		///userNameTheirs is the user who saved the newer note.</summary>
		public static string GetLogText(ProcNoteMergeChoice choice,string procDescription,string userNameTheirs) {
			string prefix=string.IsNullOrWhiteSpace(procDescription) ? "" : procDescription.Trim()+": ";
			switch(choice) {
				case ProcNoteMergeChoice.SaveMerged:
					return prefix+Lans.g("ProcNoteMerges","Procedure note merged with changes by")+" "+userNameTheirs+".";
				case ProcNoteMergeChoice.KeepMine:
					return prefix+Lans.g("ProcNoteMerges","Procedure note saved over changes by")+" "+userNameTheirs+".";
				case ProcNoteMergeChoice.KeepTheirs:
					return prefix+Lans.g("ProcNoteMerges","Procedure note changes discarded; kept the note saved by")+" "+userNameTheirs+".";
				default:
					return "";
			}
		}
	}
}
