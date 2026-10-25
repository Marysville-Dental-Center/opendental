// B-SHELL (v25_3-chart-orthochart-nonmodal): rules for the Ortho Chart window now that it can stay open while the user works elsewhere.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Reflection;

namespace OpenDentBusiness {
	///<summary>How an Ortho Chart window is being closed.</summary>
	public enum OrthoChartCloseKind {
		///<summary>Save clicked.</summary>
		Save,
		///<summary>Closed with [X] or Esc by the user; unsaved edits were already confirmed for discarding.</summary>
		UserCancel,
		///<summary>Closed by the program without asking (forced log off, auto log off, program shut down by another workstation).</summary>
		Forced,
		///<summary>The user chose Save in the log off or close program prompt.</summary>
		PromptSave,
		///<summary>The user chose Don't Save in the log off or close program prompt.</summary>
		PromptDiscard,
		///<summary>The window's patient was merged into another patient, archived or deleted while the window was open.</summary>
		PatientGone,
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
