// Procedure Code Tools, Canada: the Other Codes button (Open Dental 25.4; backlog package M2-CAN, feature v25_4-canada-other-proc-codes).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormProcTools {
		///<summary>Canada only. Null elsewhere.</summary>
		private OpenDental.UI.Button _butOtherCodes;

		///<summary>True for en-CA and fr-CA, as the rest of this window decides it.</summary>
		public static bool IsOtherCodesAvailable() {
			return CultureInfo.CurrentCulture.Name.EndsWith("CA");
		}

		///<summary>Adds the Other Codes button at the bottom left, level with Run Now. Created in code, so it follows Run Now wherever the options above
		///it put that button. Called from the constructor, while the controls still have their designer (96 dpi) bounds.</summary>
		private void AddOtherCodesButton() {
			if(!IsOtherCodesAvailable()) {
				return;
			}
			_butOtherCodes=new OpenDental.UI.Button();
			_butOtherCodes.Name="butOtherCodes";
			_butOtherCodes.Text=Lan.g(this,"Other Codes");
			_butOtherCodes.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
			_butOtherCodes.Size=new Size(LayoutManager.Scale(100),LayoutManager.Scale(butRun.Height));
			_butOtherCodes.Location=new Point(LayoutManager.Scale(labelLineOne.Left),LayoutManager.Scale(butRun.Top));
			_butOtherCodes.Click+=butOtherCodes_Click;
			LayoutManager.Add(_butOtherCodes,this);
		}

		///<summary>Opens Other Procedure Codes. That window checks Security Admin and Procedure Code Edit itself, since the Setup Wizard can open this
		///window without the Security Admin check of Procedure Codes, Tools.</summary>
		private void butOtherCodes_Click(object sender,EventArgs e) {
			using FormProcCodesOther formProcCodesOther=new FormProcCodesOther();
			formProcCodesOther.ShowDialog();
			if(formProcCodesOther.IsChanged) {
				Changed=true;
			}
		}
	}
}
