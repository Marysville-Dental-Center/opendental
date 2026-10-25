using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	///<summary>Review of one pending insurance row (inspending, Open Dental 25.2), opened from the Family module. Staff can fix the details, delete the row
	///(needs InsPlanEdit, our choice), or convert it to real insurance in one window: pick or create the carrier, pick or create the plan, reuse or
	///create the subscription, choose the ordinal, and optionally convert the same insurance for other family members. Conversion needs CarrierCreate
	///when a carrier is created, InsPlanCreate when a carrier or plan is created, InsPlanEdit to attach to an existing plan, and PatPlanCreate.</summary>
	public partial class FrmInsPendingReview:FrmODBase {
		private long _insPendingNum;
		private InsPending _insPending;
		private Family _family;
		private List<Patient> _listPatientsFamily;
		///<summary>Carriers shown in the carrier list, index for index after the "new carrier" row when there is one.</summary>
		private List<Carrier> _listCarriers=new List<Carrier>();
		///<summary>True when the carrier list starts with the "new carrier" row. Only users with CarrierCreate get it, as in the plan and carrier
		///windows, so offices that limit CarrierCreate to stop duplicate carriers are not bypassed by patient-typed names.</summary>
		private bool _hasNewCarrierRow;
		///<summary>Plans shown after the first "new plan" row, index for index.</summary>
		private List<InsPlan> _listInsPlans=new List<InsPlan>();
		private List<InsPending> _listInsPendingsAlso=new List<InsPending>();
		///<summary>True after the row was converted or deleted, so the caller refreshes.</summary>
		public bool IsChanged;
		///<summary>After a conversion, what it created. Null otherwise.</summary>
		public InsPendingConvertResult ConvertResult;

		public FrmInsPendingReview(long insPendingNum) {
			InitializeComponent();
			_insPendingNum=insPendingNum;
			Load+=FrmInsPendingReview_Load;
			PreviewKeyDown+=FrmInsPendingReview_PreviewKeyDown;
			listCarriers.SelectionChangeCommitted+=listCarriers_SelectionChangeCommitted;
			listPlans.SelectionChangeCommitted+=listPlans_SelectionChangeCommitted;
		}

		private void FrmInsPendingReview_Load(object sender,EventArgs e) {
			Lang.F(this);
			_insPending=InsPendings.GetOne(_insPendingNum);
			if(_insPending==null) {
				MsgBox.Show(this,"This pending insurance was already converted or deleted by another user.");
				IsChanged=true;
				IsDialogOK=false;
				return;
			}
			_family=Patients.GetFamily(_insPending.PatNum);
			_listPatientsFamily=_family.ListPats.Where(x => x.PatStatus!=PatientStatus.Deleted || x.PatNum==_insPending.PatNumSubscriber).ToList();
			textPatient.Text=_family.GetNameInFamFL(_insPending.PatNum);
			if(textPatient.Text=="") {
				textPatient.Text=Patients.GetNameFL(_insPending.PatNum);
			}
			comboSubscriber.Items.AddList(_listPatientsFamily,x => x.GetNameFL());
			if(_listPatientsFamily.Exists(x => x.PatNum==_insPending.PatNumSubscriber)) {
				comboSubscriber.SetSelectedKey<Patient>(_insPending.PatNumSubscriber,x => x.PatNum);
			}
			else {
				//Subscriber outside the family (possible in data Open Dental's API made). Show it so it's not lost.
				comboSubscriber.Items.Add(Patients.GetNameFL(_insPending.PatNumSubscriber)+" ("+Lang.g(this,"not in family")+")",
					Patients.GetLim(_insPending.PatNumSubscriber)??new Patient() { PatNum=_insPending.PatNumSubscriber });
				comboSubscriber.SelectedIndex=comboSubscriber.Items.Count-1;
			}
			comboRelationship.Items.AddEnums<Relat>();
			comboRelationship.SetSelectedEnum(_insPending.Relationship);
			textVIntOrdinal.Value=_insPending.Ordinal;
			textCarrierName.Text=_insPending.CarrierName;
			textPhone.Text=_insPending.Phone;
			textGroupName.Text=_insPending.GroupName;
			textGroupNum.Text=_insPending.GroupNum;
			textEmployer.Text=_insPending.Employer;
			textSubscriberID.Text=_insPending.SubscriberID;
			int ordinalConvert=_insPending.Ordinal;
			if(ordinalConvert<1) {
				ordinalConvert=PatPlans.Refresh(_insPending.PatNum).Count+1;
			}
			textVIntOrdinalConvert.Value=Math.Min(ordinalConvert,9);
			FillCarriers();
			FillAlso();
		}

		///<summary>Reads the edit boxes into a copy of the row. Returns null and tells the user if something is wrong.</summary>
		private InsPending GetFromUI() {
			if(!textVIntOrdinal.IsValid()) {
				MsgBox.Show(this,"Please fix the wanted ordinal first.");
				return null;
			}
			if(comboSubscriber.SelectedIndex==-1 || comboRelationship.SelectedIndex==-1) {
				MsgBox.Show(this,"Please pick a subscriber and a relationship.");
				return null;
			}
			InsPending insPending=_insPending.Copy();
			insPending.PatNumSubscriber=comboSubscriber.GetSelected<Patient>().PatNum;
			insPending.Relationship=comboRelationship.GetSelected<Relat>();
			insPending.Ordinal=(byte)textVIntOrdinal.Value;
			insPending.CarrierName=textCarrierName.Text;
			insPending.Phone=textPhone.Text;
			insPending.GroupName=textGroupName.Text;
			insPending.GroupNum=textGroupNum.Text;
			insPending.Employer=textEmployer.Text;
			insPending.SubscriberID=textSubscriberID.Text;
			InsPendings.Normalize(insPending);
			string error=InsPendings.Validate(insPending);
			if(error!="") {
				MsgBox.Show(error);
				return null;
			}
			return insPending;
		}

		private static bool IsSame(InsPending insPending1,InsPending insPending2) {
			return insPending1.PatNumSubscriber==insPending2.PatNumSubscriber
				&& insPending1.Relationship==insPending2.Relationship
				&& insPending1.Ordinal==insPending2.Ordinal
				&& insPending1.CarrierName==insPending2.CarrierName
				&& insPending1.Phone==insPending2.Phone
				&& insPending1.GroupName==insPending2.GroupName
				&& insPending1.GroupNum==insPending2.GroupNum
				&& insPending1.Employer==insPending2.Employer
				&& insPending1.SubscriberID==insPending2.SubscriberID;
		}

		///<summary>Saves the edit boxes if they changed. Returns false if they are not valid or the row is gone.</summary>
		private bool SaveEdits() {
			InsPending insPending=GetFromUI();
			if(insPending==null) {
				return false;
			}
			if(IsSame(insPending,_insPending)) {
				return true;
			}
			if(InsPendings.GetOne(_insPendingNum)==null) {
				MsgBox.Show(this,"This pending insurance was already converted or deleted by another user.");
				return false;
			}
			InsPendings.Update(insPending);
			SecurityLogs.MakeLogEntry(EnumPermType.InsPlanEdit,insPending.PatNum,"Pending insurance '"+insPending.CarrierName+"' edited.");
			_insPending=insPending;
			return true;
		}

		private void butSaveEdits_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.InsPlanEdit)) {
				return;
			}
			if(!SaveEdits()) {
				return;
			}
			FillCarriers();
			FillAlso();
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.InsPlanEdit)) {
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete this pending insurance?")) {
				return;
			}
			InsPendings.Delete(_insPendingNum);
			SecurityLogs.MakeLogEntry(EnumPermType.InsPlanEdit,_insPending.PatNum,"Pending insurance '"+_insPending.CarrierName+"' (subscriber ID "
				+_insPending.SubscriberID+") deleted.");
			IsChanged=true;
			IsDialogOK=true;
		}

		private void butSearchCarriers_Click(object sender,EventArgs e) {
			FillCarriers();
		}

		///<summary>For users with CarrierCreate, row 0 creates a new carrier from the name and phone; the other rows are exact and near matches. An exact
		///match is selected, otherwise the new carrier row when there is one, otherwise nothing.</summary>
		private void FillCarriers() {
			_listCarriers=InsPendings.GetCarrierMatches(textCarrierName.Text);
			_hasNewCarrierRow=Security.IsAuthorized(EnumPermType.CarrierCreate,true);
			listCarriers.Items.Clear();
			if(_hasNewCarrierRow) {
				listCarriers.Items.Add("<"+Lang.g(this,"New carrier")+": "+textCarrierName.Text.Trim()+">");
			}
			for(int i=0;i<_listCarriers.Count;i++) {
				string text=_listCarriers[i].CarrierName;
				if(!string.IsNullOrEmpty(_listCarriers[i].Phone)) {
					text+="  "+_listCarriers[i].Phone;
				}
				if(!string.IsNullOrEmpty(_listCarriers[i].City)) {
					text+="  "+_listCarriers[i].City+", "+_listCarriers[i].State;
				}
				listCarriers.Items.Add(text,_listCarriers[i]);
			}
			int idx=_listCarriers.FindIndex(x => string.Equals((x.CarrierName??"").Trim(),textCarrierName.Text.Trim(),StringComparison.OrdinalIgnoreCase));
			if(idx==-1 && !_hasNewCarrierRow) {
				listCarriers.SelectedIndex=-1;//No exact match and no permission to create one. The user picks a near match or searches again.
			}
			else {
				listCarriers.SelectedIndex=idx+GetCarrierRowOffset();//the new carrier row when there is no exact match
			}
			FillPlans();
		}

		///<summary>1 when the carrier list starts with the "new carrier" row, otherwise 0.</summary>
		private int GetCarrierRowOffset() {
			return _hasNewCarrierRow ? 1 : 0;
		}

		///<summary>0 for the "new carrier" row or when nothing is selected.</summary>
		private long GetCarrierNumSelected() {
			int idx=listCarriers.SelectedIndex-GetCarrierRowOffset();
			if(listCarriers.SelectedIndex==-1 || idx<0 || idx>=_listCarriers.Count) {
				return 0;
			}
			return _listCarriers[idx].CarrierNum;
		}

		private long GetPlanNumSelected() {
			if(listPlans.SelectedIndex<1) {
				return 0;
			}
			return _listInsPlans[listPlans.SelectedIndex-1].PlanNum;
		}

		private void listCarriers_SelectionChangeCommitted(object sender,EventArgs e) {
			FillPlans();
		}

		private void checkAllPlans_Click(object sender,EventArgs e) {
			FillPlans();
		}

		///<summary>Row 0 creates a new plan from the group name, number and employer; the others are the carrier's plans, by default only those with the
		///same group number and employer. The first matching plan is selected.</summary>
		private void FillPlans() {
			long carrierNum=GetCarrierNumSelected();
			_listInsPlans=InsPendings.GetPlanMatches(carrierNum,textGroupNum.Text,textEmployer.Text,onlyMatching:checkAllPlans.Checked!=true);
			listPlans.Items.Clear();
			string textNew="<"+Lang.g(this,"New plan");
			List<string> listParts=new List<string>() { textGroupName.Text.Trim(),textGroupNum.Text.Trim(),textEmployer.Text.Trim() }.FindAll(x => x!="");
			if(listParts.Count>0) {
				textNew+=": "+string.Join(", ",listParts);
			}
			listPlans.Items.Add(textNew+">");
			for(int i=0;i<_listInsPlans.Count;i++) {
				string text=Lang.g(this,"Group")+" "+_listInsPlans[i].GroupNum+"  "+_listInsPlans[i].GroupName;
				string employer=Employers.GetName(_listInsPlans[i].EmployerNum);
				if(!string.IsNullOrEmpty(employer)) {
					text+="  ("+employer+")";
				}
				listPlans.Items.Add(text,_listInsPlans[i]);
			}
			int idx=0;
			if(_listInsPlans.Count>0 && checkAllPlans.Checked!=true) {
				idx=1;
			}
			listPlans.SetSelected(idx);
			FillSubscription();
		}

		private void listPlans_SelectionChangeCommitted(object sender,EventArgs e) {
			FillSubscription();
		}

		///<summary>Says whether the subscriber's existing subscription to the selected plan will be used, or a new one made with the subscriber ID.</summary>
		private void FillSubscription() {
			long planNum=GetPlanNumSelected();
			long patNumSubscriber=comboSubscriber.SelectedIndex==-1 ? _insPending.PatNumSubscriber : comboSubscriber.GetSelected<Patient>().PatNum;
			InsSub insSub=null;
			if(planNum!=0) {
				insSub=InsSubs.GetListForSubscriber(patNumSubscriber).Find(x => x.PlanNum==planNum);
			}
			if(insSub==null) {
				labelSubscription.Text=Lang.g(this,"New, with subscriber ID")+" "+EFormInsPick.NormalizeSubscriberID(textSubscriberID.Text);
			}
			else {
				labelSubscription.Text=Lang.g(this,"The subscriber's existing subscription, ID")+" "+insSub.SubscriberID;
			}
		}

		///<summary>Other family members' pending rows for the same insurance, all checked.</summary>
		private void FillAlso() {
			List<InsPending> listInsPendingsFamily=InsPendings.GetForPats(_family.ListPats.Select(x => x.PatNum).ToList());
			_listInsPendingsAlso=InsPendings.GetFamilyDuplicates(_insPending,listInsPendingsFamily);
			listAlso.Items.Clear();
			for(int i=0;i<_listInsPendingsAlso.Count;i++) {
				listAlso.Items.Add(_family.GetNameInFamFL(_listInsPendingsAlso[i].PatNum)+" ("+Lang.g("enumRelat",_listInsPendingsAlso[i].Relationship.ToString())
					+", "+Lang.g(this,"ordinal")+" "+_listInsPendingsAlso[i].Ordinal+")",_listInsPendingsAlso[i]);
				listAlso.SetSelected(i,true);
			}
			labelAlso.Visible=_listInsPendingsAlso.Count>0;
			listAlso.Visible=_listInsPendingsAlso.Count>0;
		}

		private void butConvert_Click(object sender,EventArgs e) {
			if(!textVIntOrdinalConvert.IsValid()) {
				MsgBox.Show(this,"Please fix the ordinal first.");
				return;
			}
			if(listCarriers.SelectedIndex==-1 || listPlans.SelectedIndex==-1) {
				if(_hasNewCarrierRow) {
					MsgBox.Show(this,"Please pick a carrier and a plan, or the New rows.");
				}
				else {
					MsgBox.Show(this,"Please pick an existing carrier and a plan, or the New plan row. Creating a carrier needs the Carrier Create permission.");
				}
				return;
			}
			long carrierNum=GetCarrierNumSelected();
			long planNum=GetPlanNumSelected();
			List<EnumPermType> listPerms=InsPendings.GetPermissionsForConvert(carrierNum,planNum);
			for(int i=0;i<listPerms.Count;i++) {
				if(!Security.IsAuthorized(listPerms[i])) {
					return;
				}
			}
			if(!SaveEdits()) {
				return;
			}
			List<InsPending> listInsPendingsAlso=listAlso.GetListSelected<InsPending>();
			try {
				ConvertResult=InsPendings.Convert(_insPending,carrierNum,planNum,textVIntOrdinalConvert.Value,listInsPendingsAlso);
			}
			catch(ApplicationException ex) {
				MsgBox.Show(ex.Message);
				return;
			}
			IsChanged=true;
			string msg=Lang.g(this,"Converted to insurance.");
			if(ConvertResult.IsCarrierCreated) {
				msg+="\r\n"+Lang.g(this,"A new carrier was created.");
			}
			if(ConvertResult.IsPlanCreated) {
				msg+="\r\n"+Lang.g(this,"A new plan was created with default benefits. Review it in the plan window.");
			}
			if(ConvertResult.ListPatNumsChanged.Count>1) {
				msg+="\r\n"+Lang.g(this,"Patients with new coverage:")+" "+ConvertResult.ListPatNumsChanged.Count;
			}
			for(int i=0;i<ConvertResult.ListNotes.Count;i++) {
				msg+="\r\n"+ConvertResult.ListNotes[i];
			}
			MsgBox.Show(msg);
			IsDialogOK=true;
		}

		private void FrmInsPendingReview_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butClose.IsAltKey(Key.C,e)) {
				butClose_Click(this,new EventArgs());
			}
		}

		private void butClose_Click(object sender,EventArgs e) {
			if(IsChanged) {
				IsDialogOK=true;
				return;
			}
			IsDialogOK=false;
		}
	}
}
