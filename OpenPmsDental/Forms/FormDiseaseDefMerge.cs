// Merge Problems (Open Dental 26.1 item v26_1-lists-problem-merge-keep, package M2-MED): the user chooses which of two problems to keep.
// This window only asks; FormDiseaseDefs runs DiseaseDefs.Merge.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormDiseaseDefMerge:FormODBase {
		private DiseaseDef _diseaseDef1;
		private DiseaseDef _diseaseDef2;
		///<summary>Set when the window closes with OK: the problem the user chose to keep.</summary>
		public DiseaseDef DiseaseDefKeep;
		///<summary>Set when the window closes with OK: the other problem, which the merge deletes.</summary>
		public DiseaseDef DiseaseDefDiscard;

		public FormDiseaseDefMerge(DiseaseDef diseaseDef1,DiseaseDef diseaseDef2) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_diseaseDef1=diseaseDef1;
			_diseaseDef2=diseaseDef2;
		}

		private void FormDiseaseDefMerge_Load(object sender,EventArgs e) {
			//Neither choice starts checked: the user decides which problem survives.
			radioKeep1.Text=DiseaseDefs.GetMergeChoiceText(_diseaseDef1,DiseaseDefs.GetPatientCount(_diseaseDef1.DiseaseDefNum));
			radioKeep2.Text=DiseaseDefs.GetMergeChoiceText(_diseaseDef2,DiseaseDefs.GetPatientCount(_diseaseDef2.DiseaseDefNum));
		}

		private void butOK_Click(object sender,EventArgs e) {
			if(!radioKeep1.Checked && !radioKeep2.Checked) {
				MsgBox.Show(this,"Choose the problem to keep.");
				return;
			}
			DiseaseDef diseaseDefKeep=(radioKeep1.Checked ? _diseaseDef1 : _diseaseDef2);
			DiseaseDef diseaseDefDiscard=(radioKeep1.Checked ? _diseaseDef2 : _diseaseDef1);
			string message=Lan.g(this,"Keep")+" \""+diseaseDefKeep.DiseaseName+"\" "+Lan.g(this,"and delete")+" \""+diseaseDefDiscard.DiseaseName+"\"?\r\n"
				+Lan.g(this,"Everything that uses the deleted problem will use the kept problem. This cannot be undone.");
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,message,Lan.g(this,"Merge Problems"))) {
				return;
			}
			DiseaseDefKeep=diseaseDefKeep;
			DiseaseDefDiscard=diseaseDefDiscard;
			DialogResult=DialogResult.OK;
		}

		private void butCancel_Click(object sender,EventArgs e) {
			DialogResult=DialogResult.Cancel;
		}
	}
}
