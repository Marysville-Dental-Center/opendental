using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using OpenDental.UI;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormTaskAttachments:FormODBase {
		///<summary>Stores the task that the attachments belong to or will belong to.</summary>
		private Task _task;
		///<summary>Contains all the attachments for the passed in task</summary>
		private List<TaskAttachment> _listTaskAttachments;
		///<summary>Records whether a signal to refresh is required or not. Set to true if user adds or edits an attachment./summary>
		private bool _isRefreshRequired;
		/// <summary>If this  is set true, editing attachments will be disabled. </summary>
		public bool IsReadOnly=false;
		///<summary>26.1: this window is non-modal (TaskAttachmentWindows). Runs each time an attachment is added, changed or deleted from it, so the
		///task window can refresh while this one stays open.</summary>
		public Action ActionAttachmentsChanged;

		public FormTaskAttachments(Task task) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_task=task;
		}

		private void FormTaskAttachments_Load(object sender,EventArgs e) {
			_isRefreshRequired=false;
			FillGrid(true);
			butAdd.Enabled=!IsReadOnly;
		}

		private void FillGrid(bool doRefreshCache=false) {
			_listTaskAttachments=TaskAttachments.GetManyByTaskNum(_task.TaskNum);
	    gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn col=new GridColumn(Lan.g("TableTaskAttachments","Doc"),50,HorizontalAlignment.Center);
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g("TableTaskAttachments","Description"),530);
			gridMain.Columns.Add(col);
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listTaskAttachments.Count;i++){
				GridRow row=new GridRow();
				TaskAttachment taskAttachment=_listTaskAttachments[i];
				if(taskAttachment.DocNum>0) {
					row.Cells.Add("X");
				}
				else {
					row.Cells.Add("");
				}
				row.Cells.Add(taskAttachment.Description);
				row.Tag=taskAttachment;
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
		}

		private void butAdd_Click(object sender,EventArgs e) {
			if(!CanAddAttachment()) {
				return;
			}
			TaskAttachment taskAttachment=new TaskAttachment();
			taskAttachment.IsNew=true;
			//26.1: non-modal, so the user can look for the document in other windows meanwhile.
			TaskAttachmentWindows.ShowAttachmentEdit(this,_task,taskAttachment,isReadOnly:false,actionSaved:OnAttachmentSaved);
		}

		///<summary>An attachment edit window opened from here saved or deleted its attachment.</summary>
		private void OnAttachmentSaved() {
			_isRefreshRequired=true;
			if(!IsDisposed) {
				FillGrid();
			}
			ActionAttachmentsChanged?.Invoke();
		}

		///<summary>The task was deleted while this window is open: attachments can only be viewed from now on.</summary>
		public void SetTaskDeleted() {
			if(IsDisposed) {
				return;
			}
			IsReadOnly=true;
			butAdd.Enabled=false;
			Text=Lan.g(this,"Task Attachments")+" - "+Lan.g(this,"the task was deleted");
			FillGrid();
		}

		private bool CanAddAttachment() {
			if(Tasks.IsTaskDeleted(_task.TaskNum)) {
				MsgBox.Show(this,"The task for this attachment was deleted.");
				return false;
			}
			StringBuilder stringBuilder=new StringBuilder();
			if(PrefC.GetLong(PrefName.TaskAttachmentCategory)==0) {
				stringBuilder.AppendLine("Task attachments have not been set up. Set an Image Category with a usage of TaskAttachment in Setup>Definitions>Image Categories. "+
					"Set the Image Category folder for attachments in Setup>Tasks.");
			}
			if(_task.ObjectType==TaskObjectType.Patient) {
				if(_task.KeyNum==0) {
					stringBuilder.AppendLine("A patient must be attached to task to add new attachments or edit existing attachments.");
				}
			}
			else{
				stringBuilder.AppendLine("Task Object Type must be set to 'Patient'.");
			}
			if(string.IsNullOrWhiteSpace(stringBuilder.ToString())) {
				return true;
			}
			stringBuilder.AppendLine("Any existing attachments are viewable only.");
			MsgBox.Show(this,stringBuilder.ToString());
			return false;
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			TaskAttachment taskAttachment=gridMain.SelectedTag<TaskAttachment>();
			if(taskAttachment==null) {
				MsgBox.Show(this,"Could not edit attachment.");
				return;
			}
			//26.1: non-modal. Opening an attachment that already has an edit window open brings that window to the front.
			TaskAttachmentWindows.ShowAttachmentEdit(this,_task,taskAttachment,IsReadOnly,OnAttachmentSaved);
		}

		private void FormTaskAttachments_FormClosing(object sender,FormClosingEventArgs e) {
			if(DialogResult!=DialogResult.Cancel) {
				return;
			}
			if(_isRefreshRequired) {
				DialogResult=DialogResult.OK;
				return;
			}
		}

	}
}