// Open Dental 26.2 (P262-GLIDEWELL): one product of a lab case (labcaseproduct): material, shade, teeth and notes. Edits the object in memory; the
// Edit Lab Case window saves the products with the lab case.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Edits one lab case product in memory. Delete sets IsDeleted; the caller removes the product from its list.</summary>
	public partial class FormLabCaseProductEdit:FormODBase {
		public LabCaseProduct LabCaseProductCur;
		public bool IsNew;
		///<summary>True after Delete was clicked (DialogResult is OK).</summary>
		public bool IsDeleted;

		public FormLabCaseProductEdit() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormLabCaseProductEdit_Load(object sender,EventArgs e) {
			textMaterial.Text=LabCaseProductCur.Material;
			textShade.Text=LabCaseProductCur.Shade;
			textToothRange.Text=LabCaseProductCur.ToothRange;
			textNotes.Text=LabCaseProductCur.Notes;
			//Glidewell's product list is not published, so the value is only shown, never picked; a row written elsewhere keeps its value.
			if((int)LabCaseProductCur.GlidewellProduct==0) {
				labelProductListValue.Text="";
			}
			else {
				labelProductListValue.Text=Lan.g(this,"Glidewell product list value")+" "+((int)LabCaseProductCur.GlidewellProduct).ToString()
					+" ("+Lan.g(this,"kept as stored")+")";
			}
			butDelete.Visible=!IsNew;
		}

		private void butDelete_Click(object sender,EventArgs e) {
			IsDeleted=true;
			DialogResult=DialogResult.OK;
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(textMaterial.Text.Trim()=="" && textShade.Text.Trim()=="" && textToothRange.Text.Trim()=="" && textNotes.Text.Trim()=="") {
				MsgBox.Show(this,"Enter the material, shade, teeth or notes of the product.");
				return;
			}
			LabCaseProductCur.Material=textMaterial.Text.Trim();
			LabCaseProductCur.Shade=textShade.Text.Trim();
			LabCaseProductCur.ToothRange=textToothRange.Text.Trim();
			LabCaseProductCur.Notes=textNotes.Text.Trim();
			DialogResult=DialogResult.OK;
		}
	}
}
