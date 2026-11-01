// Tools-tab entries of Database Maintenance that came with Open Dental 25.2 to 25.4 (backlog package B-DBM): Invalid Characters (GDI+), Notes White
// Space (the former NotesWithTooMuchWhiteSpace check) and Base64 Documents (Create Documents From Base64 Notes). FillGridTools adds the rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormDatabaseMaintenance {
		///<summary>Opens the Invalid Characters (GDI+) window. Its Fix is replication-unsafe, so the same replication prompt as for the unsafe Fix
		///methods comes first, and Fix stays disabled when it is not passed.</summary>
		private void InvalidCharsGdiFix() {
			if(_isUsingReplication || !_hasReplicationPermission) {
				VerifyPermissionToRunReplicationUnsafeMethods();
			}
			bool canFix=_hasReplicationPermission && (!_isUsingReplication || _isReplicationPasswordEntered);
			using FormDatabaseMaintInvalidChars formDatabaseMaintInvalidChars=new FormDatabaseMaintInvalidChars(canFix);
			formDatabaseMaintInvalidChars.ShowDialog();
			if(formDatabaseMaintInvalidChars.HasFixed) {
				_isCacheInvalid=true;//Definitions are cached and may have changed.
				SaveLogToFile(formDatabaseMaintInvalidChars.LogText);
			}
		}

		///<summary>Runs DatabaseMaintenances.NotesWithTooMuchWhiteSpace in Fix mode after a confirmation and shows its log.</summary>
		private void NotesWhiteSpaceFix() {
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"This tool trims excessive tabs, blank lines and spaces from appointment, communication, procedure, "
				+"patient, patient field and insurance notes. This cannot be undone. Continue?"))
			{
				return;
			}
			string strResult="";
			ProgressWin progressOD=new ProgressWin();
			progressOD.ActionMain=() => strResult=DatabaseMaintenances.NotesWithTooMuchWhiteSpace(true,DbmMode.Fix);
			try {
				progressOD.ShowDialog();
			}
			catch(Exception ex) {
				strResult=Lan.g(this,"There was an error trimming white space from notes:")+"\r\n"+ex.Message;
			}
			if(progressOD.IsCancelled) {
				return;
			}
			_isCacheInvalid=true;
			SaveLogToFile(nameof(DatabaseMaintenances.NotesWithTooMuchWhiteSpace)+":\r\n"+strResult);
			MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(strResult);
			msgBoxCopyPaste.Show();
		}

		///<summary>Counts the documents whose file is still stored as base64 text in the note, asks to continue, creates their files and shows what
		///was done and what was skipped.</summary>
		private void Base64DocumentsFix() {
			List<long> listDocNums;
			long countDownloadNotes;
			Cursor=Cursors.WaitCursor;
			try {
				listDocNums=DatabaseMaintenances.GetDocNumsWithBase64Notes();
				countDownloadNotes=DatabaseMaintenances.CountDocumentsWithDownloadNotes();
			}
			finally {
				Cursor=Cursors.Default;
			}
			string downloadText="";
			if(countDownloadNotes>0) {
				downloadText="\r\n"+Lan.g(this,"Documents with a download link in the note are left unchanged:")+" "+countDownloadNotes;
			}
			if(listDocNums.Count==0) {
				MessageBox.Show(Lan.g(this,"No documents with base64 notes were found.")+downloadText);
				return;
			}
			string msg=Lan.g(this,"Documents whose file is stored as base64 text in the note:")+" "+listDocNums.Count+downloadText+"\r\n\r\n"
				+Lan.g(this,"Each one gets a real file in the patient's image folder (or in the database, for in-database storage), and the placeholder "
					+"is replaced by the new document. Existing files are never overwritten.")+"\r\n"
				+Lan.g(this,"Continue?");
			if(MessageBox.Show(msg,Lan.g(this,"Base64 Documents"),MessageBoxButtons.OKCancel)
				!=DialogResult.OK)
			{
				return;
			}
			string strResult="";
			ProgressWin progressOD=new ProgressWin();
			progressOD.ActionMain=() => strResult=DatabaseMaintenances.CreateDocumentsFromBase64Notes().GetLog();
			try {
				progressOD.ShowDialog();
			}
			catch(Exception ex) {
				strResult=Lan.g(this,"There was an error creating documents from base64 notes:")+"\r\n"+ex.Message;
			}
			if(progressOD.IsCancelled) {
				return;
			}
			SaveLogToFile(Lan.g(this,"Base64 Documents")+":\r\n"+strResult);
			MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(strResult);
			msgBoxCopyPaste.Show();
		}
	}
}
