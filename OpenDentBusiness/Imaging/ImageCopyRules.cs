// Copy and Copy without drawings in the Imaging module (Open Dental 25.2, backlog package B-IMG). The image window draws the picture; these rules
// decide whether a single document is copied from its stored file or drawn first. Licensed under the GNU General Public License, version 2 or
// later. See LICENSE in the repository root.

namespace OpenDentBusiness {
	///<summary>Rules for the Imaging module's two copy commands. Copy puts the image on the clipboard as it is shown, drawings included. Copy without
	///drawings keeps the crop, rotation, flip and brightness/contrast but leaves out every drawing, text, measurement and AI annotation. PDFs and other
	///files that are not images behave like plain Copy.</summary>
	public static class ImageCopyRules {
		///<summary>True if copying a single document has to draw it, false if a copy of its stored file is the same picture. Both commands decide
		///exactly as plain Copy always has (ImageHelper.HasImageBeenEdited, which reads the database): the image is drawn when it has a crop, rotation,
		///flip, brightness/contrast change or any drawing, AI annotations included. So a document whose only change is its drawings is drawn by Copy
		///without drawings too, just without them, and both commands put the same clipboard formats (a bitmap and a JPG file) on the clipboard for the
		///same image. includeDrawings does not change the decision; it only decides whether the drawing step draws the drawings. Always false for
		///files that are not images.</summary>
		public static bool MustDrawForCopy(Document document,bool includeDrawings) {
			return ImageHelper.HasImageBeenEdited(document);
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
