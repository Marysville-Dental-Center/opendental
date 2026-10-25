using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenDentBusiness {
	///<summary>Rules for the two image signature permissions that Open Dental added in 24.4: ImageSignatureCreate (264) and SignedImageEdit (265).
	///An image is signed when document.Signature is not empty. Adding a note and signature to an unsigned image needs ImageSignatureCreate. Changing
	///or removing the note and signature of a signed image needs SignedImageEdit. Editing (flip, rotate, crop, size, Item Info) or deleting a signed
	///image needs SignedImageEdit on top of ImageEdit or ImageDelete, and a mount that holds signed images needs it to be deleted. The two new
	///permissions have no date restriction; ImageEdit and ImageDelete keep theirs. Chart letters are not images, so none of this applies to them.
	///The Can* methods are the bare rules, given what the user holds. The IsAuthorized* methods ask Security for the current user and, unless told
	///not to, show a message that names the missing permission.</summary>
	public static class ImageSignaturePermissions {
		///<summary>What saving the Sign window did to an image's note and signature.</summary>
		public enum SignatureChange {
			///<summary>Neither the note nor the signature changed.</summary>
			None,
			///<summary>Only the note changed on an image that was and stays unsigned.</summary>
			NoteChanged,
			///<summary>An unsigned image got a signature.</summary>
			Added,
			///<summary>A signed image got a different signature or note, and is still signed.</summary>
			Changed,
			///<summary>A signed image lost its signature.</summary>
			Removed,
		}

		#region Rules
		///<summary>True when the document is an image (not a chart letter) and carries a signature. Chart letters (ChartLetterStatus not 0) have their own
		///signing and logging in the chart letter package, so a signed chart letter is never a signed image here.</summary>
		public static bool IsSigned(Document document) {
			return document!=null && document.ChartLetterStatus==0 && !string.IsNullOrEmpty(document.Signature);
		}

		///<summary>True when any of the documents carries a signature. Null entries (empty mount positions) are skipped.</summary>
		public static bool IsAnySigned(IEnumerable<Document> documents) {
			if(documents==null) {
				return false;
			}
			return documents.Any(x => IsSigned(x));
		}

		///<summary>The permission the Sign button needs for this document: ImageSignatureCreate when it has no signature yet, SignedImageEdit when it
		///has one.</summary>
		public static EnumPermType GetSignPermission(Document document) {
			if(IsSigned(document)) {
				return EnumPermType.SignedImageEdit;
			}
			return EnumPermType.ImageSignatureCreate;
		}

		///<summary>Whether a user may open the Sign window for an image.</summary>
		public static bool CanSign(bool isSigned,bool hasImageSignatureCreate,bool hasSignedImageEdit) {
			if(isSigned) {
				return hasSignedImageEdit;
			}
			return hasImageSignatureCreate;
		}

		///<summary>Whether a user may flip, rotate, crop, size or change the Item Info of an image. hasImageEdit already includes the ImageEdit date
		///restriction.</summary>
		public static bool CanEdit(bool isSigned,bool hasImageEdit,bool hasSignedImageEdit) {
			return hasImageEdit && (!isSigned || hasSignedImageEdit);
		}

		///<summary>Whether a user may delete an image, or a mount whose items include a signed image (isAnySigned). hasImageDelete already includes
		///the ImageDelete date restriction.</summary>
		public static bool CanDelete(bool isAnySigned,bool hasImageDelete,bool hasSignedImageEdit) {
			return hasImageDelete && (!isAnySigned || hasSignedImageEdit);
		}

		///<summary>Compares the note and signature before and after the Sign window.</summary>
		public static SignatureChange GetSignatureChange(string signatureOld,string signatureNew,string noteOld,string noteNew) {
			bool wasSigned=!string.IsNullOrEmpty(signatureOld);
			bool isSigned=!string.IsNullOrEmpty(signatureNew);
			bool isNoteChanged=(noteOld??"")!=(noteNew??"");
			if(!wasSigned && isSigned) {
				return SignatureChange.Added;
			}
			if(wasSigned && !isSigned) {
				return SignatureChange.Removed;
			}
			if(wasSigned) {
				if(signatureOld!=signatureNew || isNoteChanged) {
					return SignatureChange.Changed;
				}
				return SignatureChange.None;
			}
			if(isNoteChanged) {
				return SignatureChange.NoteChanged;
			}
			return SignatureChange.None;
		}

		///<summary>The permission a Sign window change is logged under: ImageSignatureCreate for work on an unsigned image, SignedImageEdit for a
		///signed one.</summary>
		public static EnumPermType GetSignatureChangePermission(SignatureChange signatureChange) {
			if(signatureChange==SignatureChange.Changed || signatureChange==SignatureChange.Removed) {
				return EnumPermType.SignedImageEdit;
			}
			return EnumPermType.ImageSignatureCreate;
		}

		///<summary>The start of the security log text for a Sign window change. Empty for None.</summary>
		public static string GetSignatureChangeLogText(SignatureChange signatureChange) {
			switch(signatureChange) {
				case SignatureChange.Added:
					return Lans.g("ContrImages","Signature added");
				case SignatureChange.Changed:
					return Lans.g("ContrImages","Signature changed");
				case SignatureChange.Removed:
					return Lans.g("ContrImages","Signature removed");
				case SignatureChange.NoteChanged:
					return Lans.g("ContrImages","Note changed");
				default:
					return "";
			}
		}

		///<summary>The message shown when a signed image can't be changed. Names the missing permission.</summary>
		public static string GetNotAuthorizedText(EnumPermType enumPermType) {
			string text="";
			if(enumPermType==EnumPermType.SignedImageEdit) {
				text=Lans.g("ContrImages","This image is signed.")+"\r\n";
			}
			return text+Lans.g("Security","Not authorized.")+"\r\n"
				+Lans.g("Security","A user with the SecurityAdmin permission must grant you access for")+":\r\n"
				+GroupPermissions.GetDesc(enumPermType);
		}
		#endregion Rules

		#region Security checks for the current user
		///<summary>Checks the permission the Sign button needs for this document (see GetSignPermission).</summary>
		public static bool IsAuthorizedToSign(Document document,bool suppressMessage=false) {
			return IsAuthorizedNoDate(GetSignPermission(document),suppressMessage);
		}

		///<summary>Checks SignedImageEdit when the document is signed. Always true for an unsigned document. Call it after the ImageEdit or ImageDelete
		///check that the action already makes.</summary>
		public static bool IsAuthorizedForSigned(Document document,bool suppressMessage=false) {
			if(!IsSigned(document)) {
				return true;
			}
			return IsAuthorizedNoDate(EnumPermType.SignedImageEdit,suppressMessage);
		}

		///<summary>Checks SignedImageEdit when any of the documents (for example the items of a mount) is signed.</summary>
		public static bool IsAuthorizedForSigned(IEnumerable<Document> documents,bool suppressMessage=false) {
			if(!IsAnySigned(documents)) {
				return true;
			}
			return IsAuthorizedNoDate(EnumPermType.SignedImageEdit,suppressMessage);
		}

		///<summary>ImageEdit for the document's date, then SignedImageEdit when the document is signed.</summary>
		public static bool IsAuthorizedToEdit(Document document,bool suppressMessage=false) {
			if(document==null) {
				return false;
			}
			if(!Security.IsAuthorized(EnumPermType.ImageEdit,document.DateCreated,suppressMessage)) {
				return false;
			}
			return IsAuthorizedForSigned(document,suppressMessage);
		}

		///<summary>ImageDelete for the document's date, then SignedImageEdit when the document is signed.</summary>
		public static bool IsAuthorizedToDelete(Document document,bool suppressMessage=false) {
			if(document==null) {
				return false;
			}
			if(!Security.IsAuthorized(EnumPermType.ImageDelete,document.DateCreated,suppressMessage)) {
				return false;
			}
			return IsAuthorizedForSigned(document,suppressMessage);
		}

		///<summary>Shows our own message so a signed image says why it is refused. The two signature permissions have no date restriction.</summary>
		private static bool IsAuthorizedNoDate(EnumPermType enumPermType,bool suppressMessage) {
			if(Security.IsAuthorized(enumPermType,DateTime.MinValue,suppressMessage:true)) {
				return true;
			}
			if(!suppressMessage) {
				MessageBox.Show(GetNotAuthorizedText(enumPermType));
			}
			return false;
		}
		#endregion Security checks for the current user

		#region Security log
		///<summary>Logs what the Sign window did to the document, under ImageSignatureCreate or SignedImageEdit. Nothing when nothing changed.</summary>
		public static void LogSignatureChange(Document documentOld,Document documentNew) {
			SignatureChange signatureChange=GetSignatureChange(documentOld.Signature,documentNew.Signature,documentOld.Note,documentNew.Note);
			if(signatureChange==SignatureChange.None) {
				return;
			}
			ImageStore.LogDocument(GetSignatureChangeLogText(signatureChange)+": ",GetSignatureChangePermission(signatureChange),documentNew,
				documentOld.DateTStamp);
		}

		///<summary>Logs an edit of a signed image under SignedImageEdit, next to the ImageEdit entry the action makes. Nothing for an unsigned
		///image. action is a short past-tense description, for example "flipped horizontally".</summary>
		public static void LogSignedImageEdited(Document document,string action) {
			if(!IsSigned(document)) {
				return;
			}
			string text=Lans.g("ContrImages","Signed image edited");
			if(!string.IsNullOrEmpty(action)) {
				text+=" ("+action+")";
			}
			ImageStore.LogDocument(text+": ",EnumPermType.SignedImageEdit,document,document.DateTStamp);
		}

		///<summary>Logs the deletion of a signed image under SignedImageEdit, next to the ImageDelete entry. Nothing for an unsigned image.</summary>
		public static void LogSignedImageDeleted(Document document) {
			if(!IsSigned(document)) {
				return;
			}
			ImageStore.LogDocument(Lans.g("ContrImages","Signed image deleted")+": ",EnumPermType.SignedImageEdit,document,document.DateTStamp);
		}
		#endregion Security log
	}
}
