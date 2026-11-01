using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
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

		///<summary>True when the two texts differ in more than their line breaks. For signatures, whose keys ignore line breaks
		///(Procedures.GetSignatureKeyData). Whether a save writes a note row is decided exactly, by IsNoteRowWritten.</summary>
		public static bool IsNoteTextChanged(string noteBefore,string noteAfter) {
			return Normalize(noteBefore)!=Normalize(noteAfter);
		}

		///<summary>True when the save a window is about to make writes a new procnote row, decided before anything is saved by the rule
		///Procedures.Update uses (ProcNotes.IsNoteRowNeeded, text compared exactly). procedure holds the window's values, noteToSave the note text it
		///will save (the note box's text: a RichTextBox returns \n line breaks, so a note stored with \r\n counts as changed, and Procedures.Update
		///writes a row with it). isSignatureChanged: a signature box was signed, cleared or switched in the window, so the saved signature may change.</summary>
		public static bool IsNoteRowWritten(Procedure procedure,Procedure procedureOld,string noteToSave,bool isSignatureChanged) {
			if(isSignatureChanged) {
				return true;
			}
			Procedure procedureSaved=procedure.Copy();
			procedureSaved.Note=noteToSave;
			return ProcNotes.IsNoteRowNeeded(procedureSaved,procedureOld);
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

		#region Who may change the text
		///<summary>True when the user may edit a procedure note under Procedure Info's rule (FormProcEdit's load): Procedure Note Full, or Procedure
		///Note User when the note is the user's own or not signed. The merge window applies it to the newer note.</summary>
		public static bool CanEditProcNote(bool hasNoteFullPermission,bool hasNoteUserPermission,long userNumCur,long userNumNote,bool isSigBlank) {
			if(hasNoteFullPermission) {
				return true;
			}
			return hasNoteUserPermission && (userNumNote==userNumCur || isSigBlank);
		}

		///<summary>True when the merge window may save a text other than their note (Keep Mine, Save Merged): the user could edit the note text in
		///the window (canEditTextInWindow: the note box was not read-only), and may edit the newer note under the window's rule for signed notes
		///(canEditTheirs: CanEditProcNote in Procedure Info, GroupNoteSignatures.CanEditNoteText in the Group Note window). Otherwise only Keep Theirs
		///and Cancel are offered and the merged note can't be edited, so a user who may not edit a signed note, for example a group note's
		///co-signer, can't change it or put an older text back through the merge window.</summary>
		public static bool CanChangeText(bool canEditTextInWindow,bool canEditTheirs) {
			return canEditTextInWindow && canEditTheirs;
		}

		///<summary>True when the window may act on the choice: Keep Theirs and Cancel always; Keep Mine and Save Merged only when the text may be
		///changed (CanChangeText).</summary>
		public static bool IsChoiceAllowed(ProcNoteMergeChoice choice,bool canChangeText) {
			if(choice==ProcNoteMergeChoice.KeepMine || choice==ProcNoteMergeChoice.SaveMerged) {
				return canChangeText;
			}
			return true;
		}
		#endregion Who may change the text

		#region Window state after a choice
		///<summary>After any choice but Cancel the newer note is what the window saves over: procedureOld takes its text, user and signatures, so
		///Procedures.Update compares against the newest row in the database.</summary>
		public static void TakeAsBaseline(Procedure procedureOld,ProcNote procNoteNow) {
			procedureOld.Note=procNoteNow.Note??"";
			procedureOld.UserNum=procNoteNow.UserNum;
			procedureOld.Signature=procNoteNow.Signature??"";
			procedureOld.SigIsTopaz=procNoteNow.SigIsTopaz;
			ProcNotes.CopySecondSignatureTo(procNoteNow,procedureOld);
		}

		///<summary>Keep Theirs: the procedure takes their note with its user and signatures. noteAsShown is their note as the window's note box returns
		///it once it is put there (a RichTextBox returns \n line breaks). Both copies get that text, so the save compares it equal (ProcNotes.IsNoteRowNeeded)
		///and writes no note row; the newer row stays the newest. Signatures are not affected, their keys ignore line breaks.</summary>
		public static void ApplyTheirNote(Procedure procedure,Procedure procedureOld,ProcNote procNoteTheirs,string noteAsShown) {
			TakeAsBaseline(procedureOld,procNoteTheirs);
			procedureOld.Note=noteAsShown??"";
			procedure.Note=noteAsShown??"";
			procedure.UserNum=procNoteTheirs.UserNum;
			procedure.Signature=procNoteTheirs.Signature??"";
			procedure.SigIsTopaz=procNoteTheirs.SigIsTopaz;
			ProcNotes.CopySecondSignatureTo(procNoteTheirs,procedure);
		}
		#endregion Window state after a choice

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

		///<summary>Who gets the "signature cleared" alert once the save went through: the signers collected from each merge resolved in the window
		///(GetTheirSignersToAlert, kept until the save, since a merge can still stop it), each once, without the users acting at the save (the user at
		///the keyboard, the user switched to, and a signer the save code already alerted in that click). Nothing is alerted for a save that did not
		///happen, because the caller only asks once it has saved.</summary>
		public static List<long> GetSignersToAlertOnSave(List<long> listUserNumsPending,List<long> listUserNumsActing) {
			List<long> listActing=listUserNumsActing??new List<long>();
			return (listUserNumsPending??new List<long>()).Where(x => x!=0).Distinct().Where(x => !listActing.Contains(x)).ToList();
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
