// UI Automation for the few steps an out-of-process check needs (a menu's items, a tool's buttons and fields), always scoped to one window of
// the process and always with a timeout: a search from the desktop root took minutes in the spike. Patterns only (Expand, Invoke, Value,
// SelectionItem): no input is sent.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace WindowChecksTests {
	public static class Uia {
		///<summary>Runs the UI Automation call with a timeout; default(T) and a note in error when it fails or does not answer.</summary>
		public static T Try<T>(Func<T> func,int timeoutMs,out string error) {
			error="";
			Task<T> task=Task.Run(func);
			try {
				if(!task.Wait(timeoutMs)) {
					error="UI Automation did not answer within "+timeoutMs/1000+" s";
					return default(T);
				}
				return task.Result;
			}
			catch(AggregateException ex) {
				error=ex.GetBaseException().GetType().Name+": "+ex.GetBaseException().Message;
				return default(T);
			}
		}

		public static AutomationElement Find(AutomationElement root,ControlType controlType,string name,TreeScope treeScope=TreeScope.Descendants) {
			Condition condition=new PropertyCondition(AutomationElement.ControlTypeProperty,controlType);
			if(name!=null) {
				condition=new AndCondition(condition,new PropertyCondition(AutomationElement.NameProperty,name));
			}
			return root.FindFirst(treeScope,condition);
		}

		public static List<string> Names(AutomationElement root,ControlType controlType) {
			return root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,controlType)).Cast<AutomationElement>()
				.Select(x => x.Current.Name).ToList();
		}

		///<summary>Expands the main menu's item (by name) of the window and returns the names of its items, as UI Automation sees them once the
		///drop-down is open: from the item itself, else from the process's drop-down windows.</summary>
		public static List<string> GetMenuItems(IntPtr hwndMain,int processId,string menuName,out AutomationElement automationElementMenu) {
			AutomationElement automationElementMain=AutomationElement.FromHandle(hwndMain);
			automationElementMenu=Find(automationElementMain,ControlType.MenuItem,menuName);
			if(automationElementMenu==null) {
				return null;
			}
			((ExpandCollapsePattern)automationElementMenu.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
			System.Threading.Thread.Sleep(700);
			List<string> listNames=automationElementMenu.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.MenuItem))
				.Cast<AutomationElement>().Select(x => x.Current.Name).ToList();
			if(listNames.Count>0) {
				return listNames;
			}
			foreach(IntPtr hwnd in Native.GetProcessWindows(processId).Where(x => x!=hwndMain)) {
				AutomationElement automationElement=AutomationElement.FromHandle(hwnd);
				listNames.AddRange(Names(automationElement,ControlType.MenuItem));
			}
			return listNames;
		}

		///<summary>Invokes the menu item (by name) in the process's open drop-down windows or under the menu element.</summary>
		public static bool InvokeMenuItem(IntPtr hwndMain,int processId,AutomationElement automationElementMenu,string itemName) {
			AutomationElement automationElementItem=Find(automationElementMenu,ControlType.MenuItem,itemName);
			if(automationElementItem==null) {
				foreach(IntPtr hwnd in Native.GetProcessWindows(processId).Where(x => x!=hwndMain)) {
					automationElementItem=Find(AutomationElement.FromHandle(hwnd),ControlType.MenuItem,itemName);
					if(automationElementItem!=null) {
						break;
					}
				}
			}
			if(automationElementItem==null) {
				return false;
			}
			((InvokePattern)automationElementItem.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
			return true;
		}

		///<summary>Collapses an expanded menu item (closing its drop-down).</summary>
		public static void Collapse(AutomationElement automationElementMenu) {
			try {
				((ExpandCollapsePattern)automationElementMenu.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Collapse();
			}
			catch(Exception) {
			}
		}

		public static bool Invoke(AutomationElement root,ControlType controlType,string name) {
			AutomationElement automationElement=Find(root,controlType,name);
			if(automationElement==null) {
				return false;
			}
			((InvokePattern)automationElement.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
			return true;
		}

		public static bool SetValue(AutomationElement automationElement,string value) {
			if(automationElement==null) {
				return false;
			}
			object pattern;
			if(automationElement.TryGetCurrentPattern(ValuePattern.Pattern,out pattern)) {
				((ValuePattern)pattern).SetValue(value);
				return true;
			}
			return false;
		}
	}
}
