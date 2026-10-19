using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CodeBase {

	///<summary></summary>
	public class ODClipboard {

		///<summary>Clears the clipboard.  Returns false if the clipboard could not be cleared.</summary>
		public static bool Clear() {
			try {
				Clipboard.Clear();
				return true;
			}
			catch(Exception) {
				return false;
			}
		}

		///<summary>Writes the text to the user's clipboard.</summary>
		public static void SetClipboard(string text) {
			if(string.IsNullOrEmpty(text)) {
				return;
			}
			Clipboard.SetText(text);
		}

		///<summary>Gets the contents of the user's clipboard as text.</summary>
		public static string GetText() {
			return Clipboard.GetText();
		}


		///<summary>Gets the contents of the user's clipboard as an image. Returns null if the clipboard does not contain an image.</summary>
		public static Bitmap GetImage(bool doShowProgressBar=true) {
			if(!Clipboard.ContainsImage()) {
				return null;
			}
			IDataObject iDataObject=null;
			try{
				iDataObject=Clipboard.GetDataObject();
			}
			catch {
				//rare, but can happen
				return null;
			}
			if(iDataObject is null) {
				return null;
			}
			Bitmap bitmapPaste=null;
			if(iDataObject.GetDataPresent(DataFormats.Bitmap)) {
				bitmapPaste=(Bitmap)iDataObject.GetData(DataFormats.Bitmap);
			}
			return bitmapPaste;
		}

		///<summary>
		///Surround with try/catch. Gets the contents of the user's clipboard as a list of file paths. Returns null if the clipboard does not contain any files.
		///</summary>
		public static string[] GetFileDropList() {
			return (string[])Clipboard.GetDataObject()?.GetData(DataFormats.FileDrop);
		}
	}
}
