// A small password prompt of the connection settings tool, so that a secret never has to go on a command line.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenPmsDentalDbSettings {
	///<summary>Asks for one secret (a .pfx password or the database password) with a masked text box.</summary>
	public class FormSecretPrompt:Form {
		private readonly TextBox _textSecret;

		public FormSecretPrompt(string prompt) {
			Text=FormDbSettings.TITLE;
			FormBorderStyle=FormBorderStyle.FixedDialog;
			MaximizeBox=false;
			MinimizeBox=false;
			StartPosition=FormStartPosition.CenterScreen;
			AutoScaleMode=AutoScaleMode.Font;
			ClientSize=new Size(420,120);
			ShowIcon=false;
			Label labelPrompt=new Label() { Text=prompt,Location=new Point(12,12),Size=new Size(396,32) };
			_textSecret=new TextBox() { Location=new Point(12,48),Size=new Size(396,20),UseSystemPasswordChar=true };
			Button butOK=new Button() { Text="OK",Location=new Point(252,84),Size=new Size(75,24),DialogResult=DialogResult.OK };
			Button butCancel=new Button() { Text="Cancel",Location=new Point(333,84),Size=new Size(75,24),DialogResult=DialogResult.Cancel };
			Controls.AddRange(new Control[] { labelPrompt,_textSecret,butOK,butCancel });
			AcceptButton=butOK;
			CancelButton=butCancel;
		}

		///<summary>The secret typed, or throws DbSettingsException (exit code 6) when the user cancels.</summary>
		public static string Ask(string prompt) {
			using(FormSecretPrompt formSecretPrompt=new FormSecretPrompt(prompt)) {
				if(formSecretPrompt.ShowDialog()!=DialogResult.OK) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_CANCELLED,"Cancelled: nothing was saved.");
				}
				return formSecretPrompt._textSecret.Text;
			}
		}
	}
}
