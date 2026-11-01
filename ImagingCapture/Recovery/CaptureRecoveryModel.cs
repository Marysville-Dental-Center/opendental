// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ImagingCapture.Metadata;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Recovery {
	///<summary>Which files the quarantine window lists.</summary>
	public enum CaptureRecoveryView {
		///<summary>Capture\quarantine: the unassigned captures (design 7.12).</summary>
		Unassigned,
		///<summary>Capture\journal: recent captures (design 7.1). In a capture session, "Recover recent captures": the session patient's copies
		///that have no document yet; outside one, every copy, Reassign only.</summary>
		Journal,
	}

	///<summary>Where the quarantine window was opened from.</summary>
	public sealed class CaptureRecoveryContext {
		///<summary>The capture session's bound patient, or null (Test capture, FormVideo, Setup > Imaging > Devices).</summary>
		public Patient SessionPatient { get; set; }
		public CaptureRecoveryView View { get; set; }
	}

	///<summary>One row of the list.</summary>
	public sealed class CaptureRecoveryItem {
		public CaptureFileEntry Entry { get; set; }
		///<summary>"Doe, Jane | 2026-10-31 10:00:05 | Slot 3 (teeth 6)".</summary>
		public string TitleText { get; set; }="";
		///<summary>What the user can do with it, or why not ("Reassign only: no originating patient.").</summary>
		public string NoteText { get; set; }="";
		public bool IsAssignOffered { get; set; }
		public bool IsReassignOffered { get; set; }
		public bool IsDiscardOffered { get; set; }
		///<summary>Older than 30 days (design 7.12 "Visibility").</summary>
		public bool IsOverdue { get; set; }
		///<summary>Filed or discarded in this window: no action is offered any more.</summary>
		public bool IsDone { get; set; }
		public string DoneText { get; set; }="";
	}

	///<summary>The step the action area shows.</summary>
	public enum CaptureRecoveryStep {
		None,
		///<summary>Assign: the originating patient, the target and the thumbnail, then the explicit confirmation.</summary>
		ConfirmAssign,
		///<summary>Reassign: find the patient (never preselected), both patients, the receive time, a typed reason, then the confirmation.</summary>
		Reassign,
		///<summary>Discard: the file and the confirmation.</summary>
		ConfirmDiscard,
	}

	///<summary>The quarantine window's state and commands (FrmCaptureQuarantine shows it; design 7.1, 7.12, 10.6, 10.7). UI thread only. Every
	///action checks its permission when it is begun (so a user without it is told at once) and the filer checks it again when it is confirmed.
	///Reassign starts with no patient: the window's patient search is the only way to set one, and a new selection or step clears it.</summary>
	public sealed class CaptureRecoveryModel {
		#region Fields
		///<summary>The longest side of a list thumbnail and of the selected image's picture, in pixels.</summary>
		public const int ThumbnailSize=72;
		public const int PreviewSize=320;
		private readonly CaptureRecoveryContext _context;
		private readonly CaptureRecoveryServices _services;
		private readonly CaptureRecoveryFiler _filer;
		private readonly Dictionary<string,CapturePreviewPixels> _dictThumbnails=new Dictionary<string,CapturePreviewPixels>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string,string> _dictThumbnailErrors=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
		private CapturePreviewPixels _previewPixels;
		private string _previewPath;
		private string _previewError="";
		#endregion Fields

		public CaptureRecoveryModel(CaptureRecoveryContext context,CaptureRecoveryServices services) {
			_context=context??new CaptureRecoveryContext();
			_services=services??throw new ArgumentNullException(nameof(services));
			_filer=new CaptureRecoveryFiler(_services);
			View=_context.View;
		}

		#region Properties
		public event Action Changed;
		public int Version { get; private set; }
		public CaptureRecoveryView View { get; private set; }
		public Patient SessionPatient => _context.SessionPatient;
		public long PatNumSession => _context.SessionPatient?.PatNum??0;
		///<summary>Journal view in a session: other patients' copies and copies with no patient are listed too (Reassign only).</summary>
		public bool IsOthersShown { get; private set; }
		public bool IsOthersOptionVisible => View==CaptureRecoveryView.Journal && SessionPatient!=null;
		public List<CaptureRecoveryItem> Items { get; private set; }=new List<CaptureRecoveryItem>();
		public int IdxSelected { get; private set; }=-1;
		public CaptureRecoveryItem Selected => IdxSelected>=0 && IdxSelected<Items.Count ? Items[IdxSelected] : null;
		public CaptureRecoveryStep Step { get; private set; }
		public string ErrorText { get; private set; }="";
		public string ResultText { get; private set; }="";
		///<summary>Every filing done in this window, for the capture session (CaptureSessionController.OnCaptureFiled).</summary>
		public List<CaptureFilingResult> ListResults { get; }=new List<CaptureFilingResult>();
		///<summary>Assign: where it goes.</summary>
		public CaptureAssignPlan AssignPlan { get; private set; }
		///<summary>Reassign: the patient the user found, or null. Never preselected.</summary>
		public Patient ReassignPatient { get; private set; }
		///<summary>Reassign: the originating patient as the database has it now, or null.</summary>
		public Patient ReassignPatientFrom { get; private set; }
		///<summary>The image categories (not hidden), for Assign and Reassign.</summary>
		public List<Def> ListCategories { get; private set; }=new List<Def>();
		///<summary>The category the filing uses: the plan's default for Assign, else the session's or the first; the user may change it.</summary>
		public long CategoryDefNum { get; private set; }

		public string WindowTitle => View==CaptureRecoveryView.Unassigned ? "Unassigned Captures"
			: SessionPatient==null ? "Recent Captures" : "Recover Recent Captures for "+SessionPatient.GetNameFL();

		///<summary>What the list holds.</summary>
		public string ListText {
			get {
				if(View==CaptureRecoveryView.Unassigned) {
					return "Images received on this computer that were not filed: after a session ended or the patient changed, a failed save, extra "
						+"images, images left with the source, and declined redelivered images. Nothing here is removed automatically.";
				}
				if(SessionPatient==null) {
					return "The capture journal: recovery copies of recent images. Most of them were saved normally. A copy can only be reassigned, "
						+"after you checked the patient's images.";
				}
				return "Recent images of "+SessionPatient.GetNameFL()+" in the capture journal that have no document yet."
					+(IsOthersShown ? " Copies of other patients, or with no patient, can only be reassigned." : "");
			}
		}

		public string EmptyText => View==CaptureRecoveryView.Unassigned ? "There are no unassigned captures on this computer."
			: SessionPatient==null ? "The capture journal on this computer is empty." : "No recent capture of "+SessionPatient.GetNameFL()+" is missing.";

		///<summary>The details of the selected capture, one fact per line.</summary>
		public string DetailsText => Selected==null ? "" : GetDetailsText(Selected.Entry);

		public bool IsAssignEnabled => Step==CaptureRecoveryStep.None && Selected!=null && !Selected.IsDone && Selected.IsAssignOffered;
		public bool IsReassignEnabled => Step==CaptureRecoveryStep.None && Selected!=null && !Selected.IsDone && Selected.IsReassignOffered;
		public bool IsDiscardEnabled => Step==CaptureRecoveryStep.None && Selected!=null && !Selected.IsDone && Selected.IsDiscardOffered;
		public bool IsDiscardVisible => View==CaptureRecoveryView.Unassigned;
		public bool IsCategoryVisible => Step==CaptureRecoveryStep.ConfirmAssign || Step==CaptureRecoveryStep.Reassign;
		public bool IsReasonVisible => Step==CaptureRecoveryStep.Reassign;
		public bool IsFindPatientVisible => Step==CaptureRecoveryStep.Reassign;
		public bool IsConfirmVisible => Step!=CaptureRecoveryStep.None;
		public bool IsConfirmEnabled => Step!=CaptureRecoveryStep.Reassign || ReassignPatient!=null;

		///<summary>The heading of the action area.</summary>
		public string StepText {
			get {
				switch(Step) {
					case CaptureRecoveryStep.ConfirmAssign:
						return "Assign this image to its originating patient?";
					case CaptureRecoveryStep.Reassign:
						return "Reassign this image to a different patient";
					case CaptureRecoveryStep.ConfirmDiscard:
						return "Discard this image?";
					default:
						return "";
				}
			}
		}

		///<summary>The patient lines of the step: the originating patient (Assign), both patients (Reassign), the bound patient (Discard).</summary>
		public string StepPatientText {
			get {
				CaptureFileEntry entry=Selected?.Entry;
				switch(Step) {
					case CaptureRecoveryStep.ConfirmAssign:
						return "Patient: "+CaptureRecoveryText.GetPatientLine(AssignPlan?.Patient);
					case CaptureRecoveryStep.Reassign:
						return "Taken for: "+GetFromLine(entry)+"\r\nReassign to: "+(ReassignPatient==null ? "(find the patient)"
							: CaptureRecoveryText.GetPatientLine(ReassignPatient));
					case CaptureRecoveryStep.ConfirmDiscard:
						return "Bound patient: "+(entry==null || entry.IsPatientUnknown ? CaptureRecoveryText.PatientUnknown : entry.PatientName+" (PatNum "+entry.PatNum+")");
					default:
						return "";
				}
			}
		}

		///<summary>What the step will do.</summary>
		public string StepDetailsText {
			get {
				CaptureFileEntry entry=Selected?.Entry;
				if(entry==null) {
					return "";
				}
				string received="Received "+FormatTime(entry.ReceivedAt)+".";
				switch(Step) {
					case CaptureRecoveryStep.ConfirmAssign:
						return received+" It goes into "+(AssignPlan?.TargetText??"")+"."+(AssignPlan==null || AssignPlan.Note=="" ? "" : " "+AssignPlan.Note);
					case CaptureRecoveryStep.Reassign:
						return received+" It is filed for the patient you find as an unmounted document with no tooth numbers."
							+(entry.IsGray16 ? " "+CaptureRecoveryText.GetCopy8BitNote(entry) : "")+" Type why it belongs to that patient.";
					case CaptureRecoveryStep.ConfirmDiscard:
						return received+" It is removed from unassigned captures and kept on this computer in Capture\\quarantine-resolved; the security log "
							+"records the discard.";
					default:
						return "";
				}
			}
		}

		public string ConfirmText {
			get {
				switch(Step) {
					case CaptureRecoveryStep.ConfirmAssign:
						return "Assign to "+(AssignPlan?.Patient?.GetNameFL()??"");
					case CaptureRecoveryStep.Reassign:
						return ReassignPatient==null ? "Reassign" : "Reassign to "+ReassignPatient.GetNameFL();
					case CaptureRecoveryStep.ConfirmDiscard:
						return "Discard";
					default:
						return "";
				}
			}
		}
		#endregion Properties

		#region Methods - list
		///<summary>Reads the list of the current view. Never throws: a problem shows in ErrorText.</summary>
		public void Load() {
			string pathSelected=Selected?.Entry.ImagePath;
			Items=new List<CaptureRecoveryItem>();
			try {
				if(View==CaptureRecoveryView.Unassigned) {
					foreach(CaptureFileEntry entry in _services.FileStore.ListQuarantine()) {
						Items.Add(CreateItem(entry,"",true));
					}
				}
				else {
					LoadJournal();
				}
			}
			catch(Exception ex) {
				ErrorText="The captures could not be listed: "+ex.Message;
			}
			if(ListCategories.Count==0) {
				try {
					ListCategories=_services.Store.GetImageCategories()??new List<Def>();
				}
				catch(Exception ex) {
					ErrorText="The image categories could not be read: "+ex.Message;
				}
			}
			IdxSelected=Items.FindIndex(x => string.Equals(x.Entry.ImagePath,pathSelected,StringComparison.OrdinalIgnoreCase));
			if(IdxSelected==-1 && Items.Count>0) {
				IdxSelected=0;
			}
			ClearStep();
			NotifyChanged();
		}

		///<summary>Design 7.1: in a session, the session patient's copies that have no document yet (CaptureJournalMatch), and with IsOthersShown
		///the copies of other patients or of none, Reassign only. Outside a session every copy, Reassign only. Copies already filed, and copies
		///whose image went to the quarantine (filed from there), are left out.</summary>
		private void LoadJournal() {
			List<CaptureFileEntry> listEntries=_services.FileStore.ListJournal().Where(x => !x.IsResolved && !x.IsCopyOfQuarantined).ToList();
			if(SessionPatient==null) {
				foreach(CaptureFileEntry entry in listEntries) {
					Items.Add(CreateItem(entry,"",false));
				}
				return;
			}
			long patNum=SessionPatient.PatNum;
			List<CaptureFileEntry> listOwn=listEntries.Where(x => x.PatNum==patNum).ToList();
			if(listOwn.Count>0) {
				List<Document> listDocuments;
				List<SecurityLog> listSecurityLogs;
				try {
					listDocuments=_services.Store.GetPatientDocuments(patNum)??new List<Document>();
					listSecurityLogs=_services.Store.GetImageCreateLogEntries(patNum,CaptureJournalMatch.GetDocNumsToCheck(listOwn,listDocuments))
						??new List<SecurityLog>();
				}
				catch(Exception ex) {
					//Without the documents nothing can be said to be missing; nothing of the patient is offered for Assign.
					ErrorText="The patient's documents could not be read, so the missing captures cannot be found: "+ex.Message;
					listOwn.Clear();
					listDocuments=new List<Document>();
					listSecurityLogs=new List<SecurityLog>();
				}
				foreach(CaptureFileEntry entry in listOwn) {
					string how;
					if(CaptureJournalMatch.FindDocument(entry,listDocuments,listSecurityLogs,out how)!=null) {
						continue;
					}
					Items.Add(CreateItem(entry,SessionPatient.GetNameLF(),true));
				}
			}
			if(IsOthersShown) {
				foreach(CaptureFileEntry entry in listEntries.Where(x => x.PatNum!=patNum)) {
					Items.Add(CreateItem(entry,"",false));
				}
			}
		}

		private CaptureRecoveryItem CreateItem(CaptureFileEntry entry,string patientName,bool isAssignAllowedHere) {
			CaptureRecoveryItem item=new CaptureRecoveryItem { Entry=entry };
			item.IsAssignOffered=isAssignAllowedHere && CaptureRecoveryFiler.IsAssignOffered(entry,PatNumSession);
			item.IsReassignOffered=CaptureRecoveryFiler.IsReassignOffered(entry);
			item.IsDiscardOffered=CaptureRecoveryFiler.IsDiscardOffered(entry);
			item.IsOverdue=entry.Kind==CaptureFileKind.Quarantine && entry.IsOverdue(Now);
			string patient=patientName!="" ? patientName
				: entry.IsPatientUnknown ? CaptureRecoveryText.PatientUnknown
				: entry.Kind==CaptureFileKind.Quarantine && entry.PatientName!="" ? entry.PatientName
				: "PatNum "+entry.PatNum.ToString(CultureInfo.InvariantCulture);
			string slotText=CaptureRecoveryText.GetSlotText(entry);
			//A capture of more than 8 bits says so in the list (it is filed differently); the details give every capture's depth.
			item.TitleText=patient+" | "+FormatTime(entry.ReceivedAt)+(slotText=="" ? "" : " | "+slotText)
				+(entry.IsGray16 ? " | "+Session.CaptureBitDepthText.DescribeEntry(entry) : "");
			item.NoteText=GetNoteText(item);
			return item;
		}

		private string GetNoteText(CaptureRecoveryItem item) {
			CaptureFileEntry entry=item.Entry;
			List<string> listNotes=new List<string>();
			if(item.IsOverdue) {
				listNotes.Add("More than 30 days old.");
			}
			if(entry.IsGray16) {
				//Owner decisions 2026-11-01: into its own empty slot as 16-bit DICOM (design 7.8) when 16 bits are allowed for it there, otherwise
				//("fall back to 8") as an explicit 8-bit copy in that slot; anywhere else, and reassigned, as an explicit 8-bit copy; the 16-bit
				//original kept. The note says which applies; the confirmation says which one Assign does now, and why.
				bool hasNoSlot=entry.MountNum<=0 || !entry.IsSlotKnown || entry.IsPatientUnknown || CaptureRecoveryFiler.IsRedeliveredUnconfirmed(entry);
				listNotes.Add(hasNoSlot ? CaptureRecoveryText.Gray16NoSlotNote : CaptureRecoveryText.Gray16Note);
			}
			if(CaptureRecoveryFiler.IsTestCapture(entry)) {
				listNotes.Add(CaptureRecoveryText.TestCaptureNote);
			}
			else if(!item.IsReassignOffered && !item.IsAssignOffered) {
				listNotes.Add("This file cannot be filed here.");
			}
			else if(!item.IsAssignOffered) {
				listNotes.Add(entry.IsPatientUnknown || CaptureRecoveryFiler.IsRedeliveredUnconfirmed(entry) ? "Reassign only: no originating patient."
					: entry.Kind==CaptureFileKind.Journal ? "Reassign only: a copy from "+(SessionPatient==null ? "the journal" : "another patient's session")+"."
					: "Reassign only.");
			}
			return string.Join(" ",listNotes);
		}

		///<summary>Switches between the unassigned captures and the journal, and lists again.</summary>
		public void SetView(CaptureRecoveryView view) {
			if(view==View) {
				return;
			}
			View=view;
			ErrorText="";
			ResultText="";
			Load();
		}

		public void SetOthersShown(bool isShown) {
			if(isShown==IsOthersShown) {
				return;
			}
			IsOthersShown=isShown;
			Load();
		}

		public void Select(int idx) {
			if(idx<-1 || idx>=Items.Count || idx==IdxSelected) {
				return;
			}
			IdxSelected=idx;
			ClearStep();
			ErrorText="";
			ResultText="";
			NotifyChanged();
		}
		#endregion Methods - list

		#region Methods - actions
		///<summary>Assign: ImageCreate, then the confirmation with the originating patient, the target and the thumbnail.</summary>
		public bool BeginAssign() {
			if(!IsAssignEnabled) {
				return false;
			}
			ErrorText="";
			ResultText="";
			if(!CheckPermission(EnumPermType.ImageCreate,DateTime.MinValue)) {
				return false;
			}
			string error;
			CaptureAssignPlan plan=_filer.PlanAssign(Selected.Entry,PatNumSession,out error);
			if(plan==null) {
				ErrorText=error;
				NotifyChanged();
				return false;
			}
			AssignPlan=plan;
			CategoryDefNum=plan.DocCategory>0 ? plan.DocCategory : GetFallbackCategory();
			Step=CaptureRecoveryStep.ConfirmAssign;
			NotifyChanged();
			return true;
		}

		///<summary>Reassign: ImageEdit (date-restricted by the receive time), then the step with no patient chosen.</summary>
		public bool BeginReassign() {
			if(!IsReassignEnabled) {
				return false;
			}
			ErrorText="";
			ResultText="";
			if(!CheckPermission(EnumPermType.ImageEdit,Selected.Entry.ReceivedAt)) {
				return false;
			}
			ReassignPatient=null;//never preselected, not even the session's patient (design 7.12)
			ReassignPatientFrom=null;
			if(!Selected.Entry.IsPatientUnknown) {
				try {
					ReassignPatientFrom=_services.Store.GetPatient(Selected.Entry.PatNum);
				}
				catch(Exception ex) {
					ErrorText="The originating patient could not be read: "+ex.Message;
				}
			}
			CategoryDefNum=GetDefaultCategory(Selected.Entry);
			Step=CaptureRecoveryStep.Reassign;
			NotifyChanged();
			return true;
		}

		///<summary>The patient the user picked in the patient search (FrmPatientSelect), or 0 when the search was cancelled.</summary>
		public bool SetReassignPatient(long patNum) {
			if(Step!=CaptureRecoveryStep.Reassign || patNum<=0) {
				return false;
			}
			Patient patient;
			try {
				patient=_services.Store.GetPatient(patNum);
			}
			catch(Exception ex) {
				ErrorText="The patient could not be read: "+ex.Message;
				NotifyChanged();
				return false;
			}
			if(patient==null) {
				ErrorText="The patient was not found.";
				NotifyChanged();
				return false;
			}
			ReassignPatient=patient;
			ErrorText="";
			NotifyChanged();
			return true;
		}

		///<summary>Discard: ImageDelete (date-restricted by the receive time), then the confirmation.</summary>
		public bool BeginDiscard() {
			if(!IsDiscardEnabled) {
				return false;
			}
			ErrorText="";
			ResultText="";
			if(!CheckPermission(EnumPermType.ImageDelete,Selected.Entry.ReceivedAt)) {
				return false;
			}
			Step=CaptureRecoveryStep.ConfirmDiscard;
			NotifyChanged();
			return true;
		}

		public void SetCategory(long defNum) {
			if(CategoryDefNum==defNum) {
				return;
			}
			CategoryDefNum=defNum;
			NotifyChanged();
		}

		///<summary>Does the step: the filer checks the permission again and files or discards. reason: Reassign's typed reason.</summary>
		public bool Confirm(string reason) {
			CaptureRecoveryItem item=Selected;
			if(item==null || Step==CaptureRecoveryStep.None || item.IsDone) {
				return false;
			}
			CaptureFilingResult result;
			switch(Step) {
				case CaptureRecoveryStep.ConfirmAssign:
					result=_filer.Assign(item.Entry,CategoryDefNum,PatNumSession,AssignPlan);
					break;
				case CaptureRecoveryStep.Reassign:
					result=_filer.Reassign(item.Entry,ReassignPatient,reason,CategoryDefNum);
					break;
				default:
					result=_filer.Discard(item.Entry);
					break;
			}
			if(!result.IsDone) {
				ErrorText=result.Error;
				if(result.IsPlanChanged) {
					//The slot was filled (or the mount changed, or 16 bits became allowed or not allowed for it) since the confirmation was shown: the
					//step shows where and how the capture goes now, and the operator confirms again; nothing was filed.
					string errorPlan;
					CaptureAssignPlan planNow=_filer.PlanAssign(item.Entry,PatNumSession,out errorPlan);
					if(planNow==null) {
						ClearStep();
						ErrorText=(result.Error+" "+errorPlan).Trim();
					}
					else {
						AssignPlan=planNow;
					}
				}
				NotifyChanged();
				return false;
			}
			ListResults.Add(result);
			item.IsDone=true;
			item.DoneText=GetDoneText(result);
			ResultText=item.DoneText+(result.ListWarnings.Count==0 ? "" : " "+string.Join(" ",result.ListWarnings));
			ErrorText="";
			ClearStep();
			NotifyChanged();
			return true;
		}

		public void CancelStep() {
			if(Step==CaptureRecoveryStep.None) {
				return;
			}
			ClearStep();
			ErrorText="";
			NotifyChanged();
		}

		private static string GetDoneText(CaptureFilingResult result) {
			switch(result.Action) {
				case CaptureRecoveryAction.Assign:
					return "Assigned: DocNum "+result.Document.DocNum+" in "+result.TargetText+CaptureRecoveryText.GetCopyDoneText(result)+".";
				case CaptureRecoveryAction.Reassign:
					return "Reassigned to PatNum "+result.PatNum+": DocNum "+result.Document.DocNum+CaptureRecoveryText.GetCopyDoneText(result)+".";
				default:
					return "Discarded.";
			}
		}

		private bool CheckPermission(EnumPermType permType,DateTime date) {
			string message;
			bool isAuthorized;
			try {
				isAuthorized=_services.Store.IsAuthorized(permType,date,out message);
			}
			catch(Exception ex) {
				isAuthorized=false;
				message="The permission could not be checked: "+ex.Message;
			}
			if(isAuthorized) {
				return true;
			}
			ErrorText=string.IsNullOrWhiteSpace(message) ? "Not authorized for "+permType+"." : message;
			NotifyChanged();
			return false;
		}

		private void ClearStep() {
			Step=CaptureRecoveryStep.None;
			AssignPlan=null;
			ReassignPatient=null;
			ReassignPatientFrom=null;
		}

		private long GetDefaultCategory(CaptureFileEntry entry) {
			if(entry.DocCategory>0 && ListCategories.Any(x => x.DefNum==entry.DocCategory)) {
				return entry.DocCategory;
			}
			return GetFallbackCategory();
		}

		private long GetFallbackCategory() {
			return ListCategories.FirstOrDefault()?.DefNum??0;
		}
		#endregion Methods - actions

		#region Methods - pictures
		///<summary>The list's small picture of item (made once), or null with error.</summary>
		public CapturePreviewPixels GetThumbnail(CaptureRecoveryItem item,out string error) {
			error="";
			string path=item?.Entry?.ImagePath;
			if(path==null) {
				error="No file.";
				return null;
			}
			CapturePreviewPixels pixels;
			if(_dictThumbnails.TryGetValue(path,out pixels)) {
				return pixels;
			}
			if(_dictThumbnailErrors.TryGetValue(path,out error)) {
				return null;
			}
			pixels=ReadPicture(item.Entry,ThumbnailSize,out error);
			if(pixels==null) {
				_dictThumbnailErrors[path]=error;
				return null;
			}
			_dictThumbnails[path]=pixels;
			return pixels;
		}

		///<summary>The selected capture's larger picture, as received (not rotated), or null with error.</summary>
		public CapturePreviewPixels GetPreview(out string error) {
			error="";
			CaptureFileEntry entry=Selected?.Entry;
			if(entry==null) {
				return null;
			}
			if(!string.Equals(_previewPath,entry.ImagePath,StringComparison.OrdinalIgnoreCase)) {
				_previewPath=entry.ImagePath;
				_previewPixels=ReadPicture(entry,PreviewSize,out _previewError);
			}
			error=_previewError;
			return _previewPixels;
		}

		private CapturePreviewPixels ReadPicture(CaptureFileEntry entry,int maxSide,out string error) {
			AcquiredImage image=_services.FileStore.ReadImage(entry,out error);
			if(image==null) {
				return null;
			}
			CapturePreviewPixels pixels=CapturePreview.FromImage(image,false,out error);
			return pixels==null ? null : Downscale(pixels,maxSide);
		}

		///<summary>pixels reduced so that the longer side is at most maxSide (nearest pixel), or unchanged when already small enough.</summary>
		public static CapturePreviewPixels Downscale(CapturePreviewPixels pixels,int maxSide) {
			if(pixels==null || maxSide<1 || (pixels.Width<=maxSide && pixels.Height<=maxSide)) {
				return pixels;
			}
			double scale=(double)maxSide/Math.Max(pixels.Width,pixels.Height);
			int width=Math.Max(1,(int)Math.Round(pixels.Width*scale));
			int height=Math.Max(1,(int)Math.Round(pixels.Height*scale));
			int bytesPerPixel=pixels.IsGray ? 1 : 3;
			byte[] arrayPixels=new byte[width*height*bytesPerPixel];
			for(int y=0;y<height;y++) {
				int ySource=Math.Min(pixels.Height-1,(int)(y/scale));
				for(int x=0;x<width;x++) {
					int xSource=Math.Min(pixels.Width-1,(int)(x/scale));
					Buffer.BlockCopy(pixels.Pixels,(ySource*pixels.Width+xSource)*bytesPerPixel,arrayPixels,(y*width+x)*bytesPerPixel,bytesPerPixel);
				}
			}
			return new CapturePreviewPixels { Width=width,Height=height,IsGray=pixels.IsGray,Pixels=arrayPixels };
		}
		#endregion Methods - pictures

		#region Methods - text
		///<summary>Every fact the sidecar gives, for the details box (design 7.12 "Contents").</summary>
		public static string GetDetailsText(CaptureFileEntry entry) {
			StringBuilder stringBuilder=new StringBuilder();
			Action<string,string> add=(label,value) => {
				if(!string.IsNullOrWhiteSpace(value)) {
					stringBuilder.Append(label).Append(": ").Append(value.Trim()).Append("\r\n");
				}
			};
			add("Patient",entry.IsPatientUnknown ? CaptureRecoveryText.PatientUnknown
				: (entry.PatientName=="" ? "" : entry.PatientName+" ")+"(PatNum "+entry.PatNum.ToString(CultureInfo.InvariantCulture)+")");
			add("Session",entry.SessionGuid?.ToString("D"));
			add("Slot",CaptureRecoveryText.GetSlotText(entry));
			add("Mount",entry.MountNum>0 ? "MountNum "+entry.MountNum.ToString(CultureInfo.InvariantCulture) : "");
			add("Received",FormatTime(entry.ReceivedAt));
			add("Device or source",entry.Source);
			add("Computer",entry.Computer);
			add("Windows user",entry.WindowsUser);
			add("Capture ID",entry.CaptureId);
			add("Sequence",entry.Sequence?.ToString(CultureInfo.InvariantCulture));
			CaptureMetadata metadata=CaptureMetadataParser.Parse(entry.MetadataJson);
			add("Acquired at",metadata?.AcquiredAtText);
			add("Reason",entry.Reason);
			add("Delivered",entry.Kind==CaptureFileKind.Journal ? entry.Delivered : "");
			add("Size",entry.Width>0 ? entry.Width+" x "+entry.Height+", "+entry.BitsPerPixel+" bits per pixel" : "");
			add("Bit depth",Session.CaptureBitDepthText.DescribeEntry(entry));
			add("File",entry.ImagePath);
			return stringBuilder.ToString().TrimEnd();
		}

		private string GetFromLine(CaptureFileEntry entry) {
			if(entry==null || entry.IsPatientUnknown) {
				return "Patient unknown";
			}
			if(ReassignPatientFrom!=null) {
				return CaptureRecoveryText.GetPatientLine(ReassignPatientFrom);
			}
			return (entry.PatientName=="" ? "" : entry.PatientName+" ")+"(PatNum "+entry.PatNum.ToString(CultureInfo.InvariantCulture)+", not found)";
		}

		private static string FormatTime(DateTime dateTime) {
			return dateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
		}

		private DateTime Now => _services.FuncNow?.Invoke()??DateTime.Now;

		private void NotifyChanged() {
			Version++;
			Changed?.Invoke();
		}
		#endregion Methods - text
	}
}
