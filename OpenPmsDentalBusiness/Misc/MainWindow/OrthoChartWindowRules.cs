// B-SHELL (v25_3-chart-orthochart-nonmodal): rules for the Ortho Chart window now that it can stay open while the user works elsewhere.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Reflection;
using System.Windows.Forms;

namespace OpenPmsDentalBusiness {
	///<summary>How an Ortho Chart window is being closed.</summary>
	public enum OrthoChartCloseKind {
		///<summary>Save clicked.</summary>
		Save,
		///<summary>Closed by the user: the drawn [X], Esc, Alt+F4, the window menu or the taskbar. Asks "Unsaved changes will be lost" first when there
		///are edits. The window's default: every close that the program did not mark as one of the kinds below is a user close.</summary>
		UserCancel,
		///<summary>Closed by the program without asking (forced log off, auto log off, program shut down by another workstation, Windows shutting
		///down). Set by MarkAllForForcedClose, or taken from the close reason (see GetClosingPath).</summary>
		Forced,
		///<summary>The user chose Save in the log off or close program prompt.</summary>
		PromptSave,
		///<summary>The user chose Don't Save in the log off or close program prompt, or the window had no unsaved edits to ask about.</summary>
		PromptDiscard,
		///<summary>The window's patient was merged into another patient, archived or deleted while the window was open.</summary>
		PatientGone,
	}

	///<summary>What the window's FormClosing does, decided by OrthoChartWindowRules.GetClosingPath.</summary>
	public enum OrthoChartClosingPath {
		///<summary>Nothing now. The main window (the owner) is closing: WinForms raises FormClosing on its owned windows before the main window's own
		///FormClosing, which then calls CloseOpenForms. That asks about unsaved edits and closes this window with the right close kind. Saving or
		///unlocking here would happen even when the user then cancels the program close.</summary>
		DeferToOwner,
		///<summary>Save already wrote the chart and released the lock (DialogResult OK).</summary>
		AlreadySaved,
		///<summary>The user closed the window: ask about unsaved edits, then discard them (release the lock, delete today's empty row).</summary>
		AskThenDiscard,
		///<summary>The program closes the window: GetCloseActions with GetProgramCloseKind, without asking.</summary>
		Program,
	}

	///<summary>What closing an Ortho Chart window does with its edits and its per-patient edit lock (patientnote.UserNumOrthoLocked).</summary>
	public class OrthoChartCloseActions {
		///<summary>Write the grid to the database (OrthoCharts.Sync and OrthoChartRows.Sync).</summary>
		public bool DoSave;
		///<summary>Delete today's empty row that opening the window added.</summary>
		public bool DoDeleteEmptyRows;
		///<summary>Set patientnote.UserNumOrthoLocked to 0.</summary>
		public bool DoReleaseLock;
	}

	///<summary>Rules for the non-modal Ortho Chart window (Open Dental 25.3: it no longer blocks the rest of the program). Only one window per patient
	///is open at a time. The window stays tied to its patient when the main window selects another patient. The edit lock is released whenever the
	///window closes, for any reason, but only by the window that holds it: a window that only shows the chart read-only (someone else has control,
	///or the user may not edit) never saves and never touches another user's lock.</summary>
	public static class OrthoChartWindowRules {
		///<summary>What to do when the window closes. isLockedByMe: this window holds the edit lock. isReadOnly: the user may not edit ortho charts.</summary>
		public static OrthoChartCloseActions GetCloseActions(OrthoChartCloseKind closeKind,bool isLockedByMe,bool isReadOnly) {
			OrthoChartCloseActions closeActions=new OrthoChartCloseActions();
			if(isReadOnly || !isLockedByMe) {
				return closeActions;//Nothing of ours to save, and the lock (if any) belongs to someone else.
			}
			closeActions.DoReleaseLock=true;
			switch(closeKind) {
				case OrthoChartCloseKind.Save:
				case OrthoChartCloseKind.PromptSave:
				case OrthoChartCloseKind.Forced://As before 25.3: an unattended close keeps the work rather than losing it.
					closeActions.DoSave=true;
					break;
				case OrthoChartCloseKind.UserCancel:
				case OrthoChartCloseKind.PromptDiscard:
				case OrthoChartCloseKind.PatientGone://The rows now belong to another patient (merge) or to a removed patient; nothing is written.
					closeActions.DoDeleteEmptyRows=true;
					break;
			}
			return closeActions;
		}

		///<summary>What FormClosing does. isModal: Form.Modal. dialogResult: Form.DialogResult when FormClosing runs. closeReason: the event's
		///CloseReason. closeKindSet: the window's close kind, UserCancel unless the program set another one before closing it.
		///A window shown with Show() keeps DialogResult None when it is closed with Esc (FormODBase calls Close()), Alt+F4, the window menu or the
		///taskbar; only the drawn [X] sets Cancel. WinForms sets Cancel only for a modal window. So a None close is a user close unless the program
		///marked the window or the close reason says the program, the main window or Windows is closing it.</summary>
		public static OrthoChartClosingPath GetClosingPath(bool isModal,DialogResult dialogResult,CloseReason closeReason,OrthoChartCloseKind closeKindSet) {
			if(!isModal && closeReason==CloseReason.FormOwnerClosing) {
				return OrthoChartClosingPath.DeferToOwner;
			}
			if(IsProgramCloseKind(closeKindSet) || IsProgramCloseReason(closeReason)) {
				return OrthoChartClosingPath.Program;//Checked before DialogResult, so a Cancel left over from an earlier close never makes the program ask.
			}
			if(dialogResult==DialogResult.Cancel || dialogResult==DialogResult.None) {
				return OrthoChartClosingPath.AskThenDiscard;
			}
			return OrthoChartClosingPath.AlreadySaved;//Save (DialogResult OK). Nothing else sets another DialogResult on this window.
		}

		///<summary>The close kind for the Program path: the one the program set, otherwise Forced (Windows shutting down, Application.Exit, a close
		///message from outside the program, or the owner closing a modal window).</summary>
		public static OrthoChartCloseKind GetProgramCloseKind(OrthoChartCloseKind closeKindSet) {
			if(IsProgramCloseKind(closeKindSet)) {
				return closeKindSet;
			}
			return OrthoChartCloseKind.Forced;
		}

		///<summary>True for the close kinds only the program sets on the window right before closing it.</summary>
		public static bool IsProgramCloseKind(OrthoChartCloseKind closeKind) {
			switch(closeKind) {
				case OrthoChartCloseKind.Forced:
				case OrthoChartCloseKind.PromptSave:
				case OrthoChartCloseKind.PromptDiscard:
				case OrthoChartCloseKind.PatientGone:
					return true;
			}
			return false;
		}

		///<summary>True when the close reason says that nobody is there to answer a prompt for this window. TaskManagerClosing is any close message
		///that came from outside the program. FormOwnerClosing reaches here only for a modal window (a non-modal one defers to its owner).</summary>
		public static bool IsProgramCloseReason(CloseReason closeReason) {
			switch(closeReason) {
				case CloseReason.WindowsShutDown:
				case CloseReason.ApplicationExitCall:
				case CloseReason.TaskManagerClosing:
				case CloseReason.FormOwnerClosing:
					return true;
			}
			return false;
		}

		///<summary>True when a graceful log off or program close must ask about this window before closing it: it holds the lock and has edits.</summary>
		public static bool NeedsClosePrompt(bool isLockedByMe,bool isReadOnly,bool hasUnsavedChanges) {
			return !isReadOnly && isLockedByMe && hasUnsavedChanges;
		}

		///<summary>True when the window's patient is gone: the row no longer exists (statusNow null), the patient was deleted, or the patient became
		///archived while the window was open, which is what a merge does to the patient merged from. INFERRED: Open Dental's exact check is not public.
		///</summary>
		public static bool IsPatientGone(PatientStatus statusAtOpen,PatientStatus? statusNow) {
			if(statusNow==null) {
				return true;
			}
			if(statusNow==PatientStatus.Deleted) {
				return true;
			}
			return statusNow==PatientStatus.Archived && statusAtOpen!=PatientStatus.Archived;
		}

		///<summary>The patient's PatStatus read from the database (not the cache), or -1 when there is no such patient. Polled by open Ortho Chart
		///windows together with the edit lock, every 4 seconds.</summary>
		public static int GetPatStatusNoCache(long patNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetInt(MethodBase.GetCurrentMethod(),patNum);
			}
			string raw=Db.GetScalar("SELECT PatStatus FROM patient WHERE PatNum="+POut.Long(patNum));
			if(string.IsNullOrEmpty(raw)) {
				return -1;
			}
			return PIn.Int(raw);
		}

		///<summary>GetPatStatusNoCache as a status, null when the patient row does not exist.</summary>
		public static PatientStatus? ToPatientStatus(int patStatusRaw) {
			if(patStatusRaw<0) {
				return null;
			}
			return (PatientStatus)patStatusRaw;
		}
	}
}
