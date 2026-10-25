// Copy and Copy without drawings in the Imaging module (Open Dental 25.2, backlog package B-IMG). The image window draws the picture; these rules
// decide whether a single document is copied from its stored file or drawn first. Licensed under the GNU General Public License, version 2 or
// later. See LICENSE in the repository root.

namespace OpenDentBusiness {
	///<summary>Rules for the Imaging module's two copy commands. Copy puts the image on the clipboard as it is shown, drawings included. Copy without
	///drawings keeps the crop, rotation, flip and brightness/contrast but leaves out every drawing, text, measurement and AI annotation. PDFs and other
	///files that are not images behave like plain Copy.</summary>
	public static class ImageCopyRules {
		///<summary>True for the file types that the image window draws itself: the bitmap types ImageHelper knows, and DICOM. False for PDFs and every
		///other file, which both copy commands put on the clipboard as the stored file.</summary>
		public static bool IsImageFile(string fileName) {
			if(string.IsNullOrEmpty(fileName)) {
				return false;
			}
			return ImageHelper.HasImageExtension(fileName) || fileName.EndsWith(".dcm");
		}

		///<summary>True if the document is an image whose crop, rotation, flip or brightness/contrast differ from its stored file. Drawings are not
		///counted: they live in the imagedraw table, never in the stored file. These are the same tests ImageHelper.HasImageBeenEdited makes before it
		///looks for drawings.</summary>
		public static bool HasViewEdits(Document document) {
			if(document==null || !IsImageFile(document.FileName)) {
				return false;
			}
			if(document.CropH!=0 || document.CropW!=0 || document.CropX!=0 || document.CropY!=0 || document.DegreesRotated!=0 || document.IsFlipped) {
				return true;
			}
			if(document.WindowingMin!=0 || (document.WindowingMax!=255 && document.WindowingMax!=0)) {
				return true;
			}
			return false;
		}

		///<summary>True if copying a single document has to draw it, false if a copy of its stored file is the same picture. Copy (includeDrawings
		///true) draws when the image has view edits or any drawing, exactly as before (ImageHelper.HasImageBeenEdited, which reads the database). Copy
		///without drawings draws only for view edits: a document whose only change is its drawings is copied from the stored file, which holds no
		///drawings, so the copy has none even though the window shows them. Always false for files that are not images.</summary>
		public static bool MustDrawForCopy(Document document,bool includeDrawings) {
			if(includeDrawings) {
				return ImageHelper.HasImageBeenEdited(document);
			}
			return HasViewEdits(document);
		}

		///<summary>The security log text for a copy. Copy keeps the text it always had; Copy without drawings says so, so the audit trail tells the two
		///apart. Untranslated, like the rest of the copy log text.</summary>
		public static string GetLogText(string logTextCopy,bool includeDrawings) {
			if(includeDrawings) {
				return logTextCopy;
			}
			return logTextCopy+" without drawings";
		}
	}
}
