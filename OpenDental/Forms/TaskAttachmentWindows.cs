// M2-MISC (Open Dental 26.1): the Task Attachments and Task Attachment Edit windows are non-modal, so they can stay open while the user works in
// other windows, for example to find the image or document to attach.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Opens the task attachment windows non-modally, owned by the window they are opened from. Keeps at most one Task Attachments window per
	///task and one edit window per attachment (and one new-attachment window per task): opening one that is already open brings it to the front.
	///The task window closes them when it closes, and makes them read-only when the task is deleted.</summary>
	public static class TaskAttachmentWindows {
		private static readonly OpenWindowRegistry<FormTaskAttachments> _registryAttachments=
			new OpenWindowRegistry<FormTaskAttachments>(x => !x.IsDisposed);
		private static readonly OpenWindowRegistry<FormTaskAttachmentEdit> _registryAttachmentEdits=
			new OpenWindowRegistry<FormTaskAttachmentEdit>(x => !x.IsDisposed);

		///<summary>The registry key of a task's Task Attachments window.</summary>
		public static string GetKeyAttachments(long taskNum) {
			return "task:"+taskNum;
		}

		///<summary>The registry key of an attachment's edit window. All new attachments of a task share one key, so only one new-attachment window
		///is open per task.</summary>
		public static string GetKeyAttachmentEdit(long taskNum,TaskAttachment taskAttachment) {
			string attachment=(taskAttachment==null || taskAttachment.IsNew || taskAttachment.TaskAttachmentNum==0) ? "new"
				: taskAttachment.TaskAttachmentNum.ToString();
			return GetKeyAttachments(taskNum)+":attachment:"+attachment;
		}

		///<summary>Shows the task's Task Attachments window, or brings the open one to the front. actionChanged runs each time an attachment is added,
		///changed or deleted from it, while it stays open.</summary>
		public static FormTaskAttachments ShowAttachments(Form formOwner,Task task,bool isReadOnly,Action actionChanged) {
			string key=GetKeyAttachments(task.TaskNum);
			FormTaskAttachments formTaskAttachments=_registryAttachments.Get(key);
			if(formTaskAttachments!=null) {
				Activate(formTaskAttachments);
				return formTaskAttachments;
			}
			formTaskAttachments=new FormTaskAttachments(task);
			formTaskAttachments.IsReadOnly=isReadOnly;
			formTaskAttachments.ActionAttachmentsChanged=actionChanged;
			_registryAttachments.Add(key,formTaskAttachments);
			formTaskAttachments.FormClosed+=(sender,e) => _registryAttachments.Remove(key,formTaskAttachments);
			Show(formTaskAttachments,formOwner);
			return formTaskAttachments;
		}

		///<summary>Shows the edit window of an attachment (or of a new one), or brings the open one to the front. actionSaved runs after the attachment
		///was saved or deleted (the window closed with OK).</summary>
		public static FormTaskAttachmentEdit ShowAttachmentEdit(Form formOwner,Task task,TaskAttachment taskAttachment,bool isReadOnly,Action actionSaved) {
			string key=GetKeyAttachmentEdit(task.TaskNum,taskAttachment);
			FormTaskAttachmentEdit formTaskAttachmentEdit=_registryAttachmentEdits.Get(key);
			if(formTaskAttachmentEdit!=null) {
				Activate(formTaskAttachmentEdit);
				return formTaskAttachmentEdit;
			}
			formTaskAttachmentEdit=new FormTaskAttachmentEdit(task);
			formTaskAttachmentEdit.TaskAttachmentCur=taskAttachment;
			formTaskAttachmentEdit.IsReadOnly=isReadOnly;
			_registryAttachmentEdits.Add(key,formTaskAttachmentEdit);
			formTaskAttachmentEdit.FormClosed+=(sender,e) => {
				_registryAttachmentEdits.Remove(key,formTaskAttachmentEdit);
				if(formTaskAttachmentEdit.DialogResult==DialogResult.OK) {
					actionSaved?.Invoke();
				}
			};
			Show(formTaskAttachmentEdit,formOwner);
			return formTaskAttachmentEdit;
		}

		///<summary>Closes every attachment window of the task (the task window was saved, deleted or closed). Unsaved changes in an edit window are
		///discarded.</summary>
		public static void CloseForTask(long taskNum) {
			string key=GetKeyAttachments(taskNum);
			List<FormTaskAttachmentEdit> listFormsEdit=_registryAttachmentEdits.GetWhere(x => x.StartsWith(key+":"));
			for(int i=0;i<listFormsEdit.Count;i++) {
				listFormsEdit[i].Close();
			}
			FormTaskAttachments formTaskAttachments=_registryAttachments.Get(key);
			formTaskAttachments?.Close();
		}

		///<summary>The task was deleted (by this user or another one): its Task Attachments window becomes read-only. An open edit window tells the
		///user when they try to save, and discards the changes.</summary>
		public static void SetTaskDeleted(long taskNum) {
			FormTaskAttachments formTaskAttachments=_registryAttachments.Get(GetKeyAttachments(taskNum));
			formTaskAttachments?.SetTaskDeleted();
		}

		///<summary>True if the task has an attachment window open.</summary>
		public static bool HasOpenWindows(long taskNum) {
			string key=GetKeyAttachments(taskNum);
			return _registryAttachments.Get(key)!=null || _registryAttachmentEdits.GetWhere(x => x.StartsWith(key+":")).Count>0;
		}

		private static void Show(Form form,Form formOwner) {
			if(formOwner==null || formOwner.IsDisposed) {
				form.Show();
				return;
			}
			form.Show(formOwner);
		}

		private static void Activate(Form form) {
			if(form.WindowState==FormWindowState.Minimized) {
				form.WindowState=FormWindowState.Normal;
			}
			form.BringToFront();
			form.Activate();
		}
	}

	///<summary>Windows that are open, by key. A window that has been closed (funcIsOpen false) is forgotten the next time it is looked up. No
	///WinForms code, so it can be tested on its own.</summary>
	public class OpenWindowRegistry<TWindow> where TWindow:class {
		private readonly Dictionary<string,TWindow> _dictWindows=new Dictionary<string,TWindow>();
		private readonly Func<TWindow,bool> _funcIsOpen;
		private readonly object _lock=new object();

		public OpenWindowRegistry(Func<TWindow,bool> funcIsOpen) {
			_funcIsOpen=funcIsOpen;
		}

		///<summary>The open window with the key, or null.</summary>
		public TWindow Get(string key) {
			lock(_lock) {
				if(!_dictWindows.TryGetValue(key,out TWindow window)) {
					return null;
				}
				if(!_funcIsOpen(window)) {
					_dictWindows.Remove(key);
					return null;
				}
				return window;
			}
		}

		///<summary>Registers a window under the key, replacing a closed one.</summary>
		public void Add(string key,TWindow window) {
			lock(_lock) {
				_dictWindows[key]=window;
			}
		}

		///<summary>Forgets the window, only if it is the one registered under the key.</summary>
		public void Remove(string key,TWindow window) {
			lock(_lock) {
				if(_dictWindows.TryGetValue(key,out TWindow windowRegistered) && ReferenceEquals(windowRegistered,window)) {
					_dictWindows.Remove(key);
				}
			}
		}

		///<summary>The open windows whose key matches.</summary>
		public List<TWindow> GetWhere(Func<string,bool> funcKeyMatches) {
			lock(_lock) {
				List<string> listKeys=_dictWindows.Keys.Where(funcKeyMatches).ToList();
				List<TWindow> listWindows=new List<TWindow>();
				for(int i=0;i<listKeys.Count;i++) {
					TWindow window=_dictWindows[listKeys[i]];
					if(_funcIsOpen(window)) {
						listWindows.Add(window);
					}
					else {
						_dictWindows.Remove(listKeys[i]);
					}
				}
				return listWindows;
			}
		}

		///<summary>The number of open windows.</summary>
		public int Count {
			get {
				return GetWhere(x => true).Count;
			}
		}
	}
}
