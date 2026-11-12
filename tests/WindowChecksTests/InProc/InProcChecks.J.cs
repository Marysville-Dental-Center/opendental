// Group J's scripts that need the checklist's 0.4 test users (J4 to J13) and the Setup Wizard (J2, J17), in process. The two test groups and users
// are made in the scratch copy by the business layer as Setup > Security's Set All and the tree's unticking make them (UserControlSecurityTree.SetAll's
// own rules), and removed again at the end of group J. Log Off and log on run as a person's would: the main menu's Log Off item, the "Are you sure"
// box answered OK, and the log-on window's user list, blank password and OK.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace WindowChecksTests.InProc {
	public static partial class InProcChecks {
		public const string USER_SETUP="gate-setup";
		public const string USER_NOSETUP="gate-nosetup";
		public const string GROUP_SETUP="Gate Setup";
		public const string GROUP_NOSETUP="Gate NoSetup";
		public const string NOTE_NOT_MARKED="(not marked for EHR reporting)";

		#region Helpers
		///<summary>Presses the window's control (by name, or its text) as a click does, and returns the window it opens (a message too when allowMessage).</summary>
		public static Ui.Opened OpenByButton(Ui ui,IntPtr hwnd,string nameOrText,bool allowMessage=false) {
			return ui.Open("Button "+nameOrText,() => {
				Control control=FindControl(ui,hwnd,nameOrText,isOnThread:true);
				if(control==null) {
					throw new Exception("No button "+nameOrText+" in "+Native.GetText(hwnd));
				}
				if(control is IButtonControl buttonControl) {
					buttonControl.PerformClick();
				}
				else {
					Ui.Call(control,"OnClick",EventArgs.Empty);
				}
			},allowMessage);
		}

		///<summary>Presses the window's control as a click does and returns a window it opens within the time (a message box, say), or an Opened
		///without a window when the handler finishes without one; neither is a failure (the caller decides).</summary>
		public static Ui.Opened PressMaybeMessage(Ui ui,IntPtr hwnd,string nameOrText,int timeoutMs=8000) {
			List<IntPtr> listBefore=ui.GetWindows();
			Ui.Opened opened=new Ui.Opened();
			opened.Posted=ui.Post("Button "+nameOrText,() => {
				Control control=FindControl(ui,hwnd,nameOrText,isOnThread:true);
				if(control==null) {
					throw new Exception("No button "+nameOrText+" in "+Native.GetText(hwnd));
				}
				if(control is IButtonControl buttonControl) {
					buttonControl.PerformClick();
				}
				else {
					Ui.Call(control,"OnClick",EventArgs.Empty);
				}
			});
			System.Diagnostics.Stopwatch stopwatch=System.Diagnostics.Stopwatch.StartNew();
			while(stopwatch.ElapsedMilliseconds<timeoutMs) {
				IntPtr hwndNew=ui.GetWindows().FirstOrDefault(x => !listBefore.Contains(x));
				if(hwndNew!=IntPtr.Zero) {
					System.Threading.Thread.Sleep(300);
					opened.Hwnd=hwndNew;
					return opened;
				}
				if(opened.Posted.ManualResetEventDone.WaitOne(0)) {
					return opened;
				}
				System.Threading.Thread.Sleep(150);
			}
			return opened;
		}

		///<summary>The message box's text, then closes it with the button (OK when it has one). "" when the window is not a message box.</summary>
		private static string ReadAndCloseMessage(Ui ui,Ui.Opened opened,string button="OK") {
			if(!opened.IsOpen || !Native.IsDialogClass(opened.Hwnd)) {
				return "";
			}
			string text=Native.GetDialogMessage(opened.Hwnd);
			if(!Native.ClickDialogButton(opened.Hwnd,button)) {
				Native.CancelDialog(opened.Hwnd);
			}
			ui.WaitGone(opened.Hwnd);
			return text.Trim();
		}

		private static string CurUserName(Ui ui) {
			return ui.Invoke(() => Security.CurUser?.UserName??"");
		}

		///<summary>Log Off (the main menu's item, its "Are you sure" box answered OK) and log on as the user with a blank password in the log-on
		///window. True when the user is logged on with patient #1 selected.</summary>
		public static bool LogOffAndOn(Ui ui,CheckResult checkResult,string userName) {
			string userBefore=CurUserName(ui);
			List<IntPtr> listBefore=ui.GetWindows();
			Ui.Posted posted=ui.Post("Log Off",() => GetMenuItem("Log Off").PerformClick());
			System.Diagnostics.Stopwatch stopwatch=System.Diagnostics.Stopwatch.StartNew();
			IntPtr hwndLogOn=IntPtr.Zero;
			bool isAskAnswered=false;
			while(hwndLogOn==IntPtr.Zero && stopwatch.Elapsed.TotalSeconds<60) {
				foreach(IntPtr hwnd in ui.GetWindows().Where(x => !listBefore.Contains(x))) {
					string typeName=ui.GetWindowTypeName(hwnd);
					if(typeName=="FormLogOn") {
						hwndLogOn=hwnd;
						break;
					}
					if(typeName.EndsWith("/InputBox") && !isAskAnswered) {
						//"Are you sure you would like to log off?" with "Do not show me this message again." unticked: OK.
						System.Windows.FrameworkElement frm=ui.GetFrm(hwnd);
						ui.Post("Log off: OK",() => ClickFrm(ui,frm,"butOK_Click"));
						isAskAnswered=true;
						ui.WaitGone(hwnd,10000);
						continue;
					}
					if(Native.IsDialogClass(hwnd)) {
						checkResult.Note("Log Off showed: "+ui.DescribeWindow(hwnd)+"; answered No/Cancel");
						if(!Native.ClickDialogButton(hwnd,"No")) {
							Native.CancelDialog(hwnd);
						}
						ui.WaitGone(hwnd);
					}
				}
				System.Threading.Thread.Sleep(200);
			}
			if(!checkResult.Expect(hwndLogOn!=IntPtr.Zero,"Log Off ("+userBefore+"): the log-on window opens")) {
				return false;
			}
			System.Threading.Thread.Sleep(500);
			string listed=ui.Invoke(() => {
				Form formLogOn=(Form)Control.FromHandle(hwndLogOn);
				OpenPmsDental.UI.ListBox listUser=(OpenPmsDental.UI.ListBox)Ui.GetField(formLogOn,"listUser");
				List<string> listNames=listUser.Items.GetAll<string>();
				int index=listNames.FindIndex(x => x.Trim().ToLower()==userName.ToLower());
				if(index<0) {
					return "";
				}
				listUser.SetSelected(index,true);
				//With UserNameManualEntry on, OK reads the typed name instead of the list.
				((TextBox)Ui.GetField(formLogOn,"textUser")).Text=listNames[index];
				((TextBox)Ui.GetField(formLogOn,"textPassword")).Text="";
				return string.Join(", ",listNames);
			});
			if(!checkResult.Expect(listed!="","The log-on window lists "+userName,listed)) {
				//Never cancelled (see LogOnAsAdmin): back to Admin.
				LogOnAsAdmin(ui,hwndLogOn,checkResult);
				posted.Wait(60000);
				return false;
			}
			List<IntPtr> listBeforeOk=ui.GetWindows();
			ui.Post("Log on: OK",() => Ui.Call(Control.FromHandle(hwndLogOn),"butOK_Click",null,EventArgs.Empty));
			if(!ui.WaitGone(hwndLogOn,30000)) {
				foreach(IntPtr hwnd in ui.GetWindows().Where(x => !listBeforeOk.Contains(x))) {
					checkResult.Fail("Log on as "+userName+" showed: "+ui.DescribeWindow(hwnd));
					ui.Close(hwnd);
				}
				//The log-on window is still open: back to Admin, never cancelled.
				LogOnAsAdmin(ui,hwndLogOn,checkResult);
				posted.Wait(60000);
				return false;
			}
			posted.Wait(60000);
			//What the log-on opens afterwards (a module's question, a message) is read and closed.
			System.Threading.Thread.Sleep(1500);
			foreach(IntPtr hwnd in ui.GetWindows().Where(x => !listBefore.Contains(x))) {
				checkResult.Note("After the log-on: "+ui.DescribeWindow(hwnd)+"; closed");
				ui.Close(hwnd);
			}
			string userAfter=CurUserName(ui);
			bool isPatient=ui.Invoke(() => FormOpenDental.PatNumCur==1);
			return checkResult.Expect(userAfter==userName && isPatient,"Log Off, log on as "+userName+" (blank password): logged on, patient #1 still selected",
				"user "+userAfter+", patient #1 "+isPatient);
		}

		///<summary>The way out of a log-on window the harness cannot use. It is never cancelled: a log-on window closed with anything but OK makes
		///FormOpenDental.ShowLogOn call Application.Exit, and the main window's FormClosed then ends the process with Environment.Exit, which in
		///process is the test host (no later check, no AssemblyCleanup, no Z, the scratch copy left behind). Instead the message boxes a failed OK left
		///over it are answered, Admin is selected with a blank password (the harness's own start-up log-on) and OK is clicked, so the program goes
		///back to a known user. Returns whether the log-on window went; when it did not, it is left open for Stop, which detaches the main window's
		///closing first.</summary>
		public static bool LogOnAsAdmin(Ui ui,IntPtr hwndLogOn,CheckResult checkResult) {
			if(!Native.IsWindow(hwndLogOn)) {
				return true;
			}
			//"Login failed", "Invalid username, password ...": message boxes over the log-on window.
			foreach(IntPtr hwnd in ui.GetWindows().Where(x => x!=hwndLogOn && Native.IsDialogClass(x))) {
				checkResult?.Note("Over the log-on window: "+ui.DescribeWindow(hwnd)+"; answered");
				if(!Native.ClickDialogButton(hwnd,"OK")) {
					Native.CancelDialog(hwnd);
				}
				ui.WaitGone(hwnd);
			}
			string problem=ui.Invoke(() => {
				Form formLogOn=Control.FromHandle(hwndLogOn) as Form;
				if(formLogOn==null || formLogOn.IsDisposed) {
					return "the log-on window is gone";
				}
				OpenPmsDental.UI.ListBox listUser=(OpenPmsDental.UI.ListBox)Ui.GetField(formLogOn,"listUser");
				List<string> listNames=listUser.Items.GetAll<string>();
				int index=listNames.FindIndex(x => x.Trim().ToLower()=="admin");
				if(index<0) {
					return "Admin is not listed ("+string.Join(", ",listNames)+")";
				}
				listUser.SetSelected(index,true);
				((TextBox)Ui.GetField(formLogOn,"textUser")).Text=listNames[index];
				((TextBox)Ui.GetField(formLogOn,"textPassword")).Text="";
				return "";
			});
			if(problem=="the log-on window is gone") {
				return true;
			}
			if(problem!="") {
				checkResult?.Fail("The log-on window could not go back to Admin: "+problem+"; left open (never cancelled)");
				return false;
			}
			ui.Post("Log on as Admin: OK",() => Ui.Call(Control.FromHandle(hwndLogOn),"butOK_Click",null,EventArgs.Empty));
			bool isGone=ui.WaitGone(hwndLogOn,30000);
			if(!isGone) {
				foreach(IntPtr hwnd in ui.GetWindows().Where(x => x!=hwndLogOn && Native.IsDialogClass(x))) {
					checkResult?.Note("Log on as Admin showed: "+ui.DescribeWindow(hwnd)+"; answered");
					if(!Native.ClickDialogButton(hwnd,"OK")) {
						Native.CancelDialog(hwnd);
					}
					ui.WaitGone(hwnd);
				}
				checkResult?.Fail("Log on as Admin (blank password) did not close the log-on window; left open (never cancelled)");
				return false;
			}
			checkResult?.Note("The log-on window went back to Admin (blank password) instead of being cancelled; user now "+CurUserName(ui));
			return true;
		}

		///<summary>The harness's own check of LogOnAsAdmin (not a checklist item): a log-on as a user the list does not have. The log-on window is
		///not cancelled (that would end the test host), the program is back on Admin with patient #1, and the session goes on.</summary>
		public static void CheckJLOGONFAIL(Ui ui,CheckResult checkResult) {
			CheckResult checkResultInner=new CheckResult("JLOGONFAIL","inner");
			bool isLoggedOn=LogOffAndOn(ui,checkResultInner,"winchk-no-such-user");
			checkResult.Note("The failed log-on's own result: "+string.Join("; ",checkResultInner.ListFailures.Concat(checkResultInner.ListNotes)));
			checkResult.Expect(!isLoggedOn,"A log-on as a user the list does not have fails");
			checkResult.Expect(checkResultInner.ListFailures.Any(x => x.Contains("lists winchk-no-such-user")),"It fails at the user list");
			checkResult.Expect(ui.GetWindows().Count==0,"The log-on window is gone (logged on as Admin, not cancelled)",
				string.Join("; ",ui.GetWindows().Select(x => ui.DescribeWindow(x))));
			string userAfter=CurUserName(ui);
			bool isPatient=ui.Invoke(() => FormOpenDental.PatNumCur==1);
			checkResult.Expect(userAfter.ToLower()=="admin" && isPatient,"Back on Admin with patient #1 selected","user "+userAfter+", patient #1 "+isPatient);
		}

		///<summary>Setup > Chart > EHR, waited for; zero when it did not open.</summary>
		private static IntPtr OpenEhrSetup(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Setup","Chart","EHR");
			return InspectWindow(ui,checkResult,opened,"FormEhrSetup",null,close:false) ? opened.Hwnd : IntPtr.Zero;
		}

		///<summary>Chart > EHR (the Chart toolbar's EHR button): the two provider labels.</summary>
		private static void CheckEhrWindowLabels(Ui ui,CheckResult checkResult,bool isNoteExpected,string shot) {
			GoToModule(ui,EnumModuleType.Chart);
			Control controlChart=ModuleControl("controlChart");
			Ui.Opened opened=ui.Open("Chart > EHR",() => {
				Control toolBar=Ui.AllControls(controlChart).FirstOrDefault(x => x.GetType().Name=="ToolBarOD" && x.Name=="ToolBarMain")
					??Ui.AllControls(controlChart).First(x => x.GetType().Name=="ToolBarOD");
				ClickToolbarButtonOnThread(toolBar,"EHR");
			});
			if(!InspectWindow(ui,checkResult,opened,"FormEHR",null,close:false)) {
				return;
			}
			System.Threading.Thread.Sleep(1000);
			string labelPat=ui.Invoke(() => FindControl(ui,opened.Hwnd,"labelProvPat",true).Text);
			string labelUser=ui.Invoke(() => FindControl(ui,opened.Hwnd,"labelProvUser",true).Text);
			checkResult.Note("Patient's provider label: '"+labelPat+"'; user's provider label: '"+labelUser+"'");
			if(isNoteExpected) {
				checkResult.Expect(labelPat.EndsWith(NOTE_NOT_MARKED),"The patient's provider label ends with '"+NOTE_NOT_MARKED+"' ('"+labelPat+"')");
				checkResult.Expect(labelUser.EndsWith(NOTE_NOT_MARKED),"The user's provider label ends with '"+NOTE_NOT_MARKED+"' ('"+labelUser+"')");
			}
			else {
				checkResult.Expect(!labelPat.Contains(NOTE_NOT_MARKED),"The patient's provider label has no note ('"+labelPat+"')");
				checkResult.Expect(!labelUser.Contains(NOTE_NOT_MARKED) && labelUser!="none","The user's provider label has no note ('"+labelUser+"')");
			}
			checkResult.AddShot(ui.Shot(opened.Hwnd,shot),"The EHR window");
			checkResult.Expect(ui.Close(opened.Hwnd),"The EHR window closes");
			opened.Posted.Wait();
		}

		///<summary>Setup > Chart > EHR > SNOMED CTs: whether the One-Time Tools group shows; the window is left open when keepOpen (its handle in
		///hwndSnomed, the EHR setup's in hwndSetup).</summary>
		private static bool? CheckSnomedTools(Ui ui,CheckResult checkResult,string shot,bool keepOpen,out IntPtr hwndSnomed,out IntPtr hwndSetup) {
			hwndSnomed=IntPtr.Zero;
			hwndSetup=OpenEhrSetup(ui,checkResult);
			if(hwndSetup==IntPtr.Zero) {
				return null;
			}
			Ui.Opened opened=OpenByButton(ui,hwndSetup,"butSnomeds");
			if(!InspectWindow(ui,checkResult,opened,"FormSnomeds",null,close:false)) {
				ui.Close(hwndSetup);
				return null;
			}
			hwndSnomed=opened.Hwnd;
			IntPtr hwnd=opened.Hwnd;
			bool hasTools=ui.Invoke(() => FindControl(ui,hwnd,"groupBox1",true).Visible);
			string groupText=ui.Invoke(() => FindControl(ui,hwnd,"groupBox1",true).Text);
			checkResult.Note("snomed rows: "+DataCore.GetScalar("SELECT COUNT(*) FROM snomed")+"; the tools group: '"+groupText+"'");
			if(shot!=null) {
				checkResult.AddShot(ui.Shot(hwnd,shot),"SNOMED CTs");
			}
			if(!keepOpen) {
				checkResult.Expect(ui.Close(hwnd),"SNOMED CTs closes");
				ui.Close(hwndSetup);
			}
			return hasTools;
		}

		private static long UserNum(string userName) {
			return PIn.Long(DataCore.GetScalar("SELECT UserNum FROM userod WHERE UserName='"+POut.String(userName)+"'"));
		}

		///<summary>The provider of patient #1 (0.4 #4: gate-nosetup's provider, and the names of the EHR row).</summary>
		private static Provider ProviderOfPatient1() {
			long provNum=PIn.Long(DataCore.GetScalar("SELECT PriProv FROM patient WHERE PatNum=1"));
			return Providers.GetProv(provNum);
		}
		#endregion Helpers

		#region 0.4 test users
		///<summary>0.4: the groups "Gate Setup" (Set All, Problem Definition Edit unticked) and "Gate NoSetup" (Set All, Setup and Problem Definition
		///Edit unticked), and the users gate-setup and gate-nosetup (patient #1's provider) with no password; checks their effective permissions.</summary>
		public static void CheckJUSERS(Ui ui,CheckResult checkResult) {
			ui.Invoke(() => {
				foreach(string[] arrayGroup in new[] { new[] { GROUP_SETUP,USER_SETUP },new[] { GROUP_NOSETUP,USER_NOSETUP } }) {
					UserGroup userGroup=new UserGroup { Description=arrayGroup[0] };
					long userGroupNum=UserGroups.Insert(userGroup);
					//Set All, as UserControlSecurityTree.SetAll grants.
					foreach(EnumPermType permType in Enum.GetValues(typeof(EnumPermType)).Cast<EnumPermType>()) {
						if(permType==EnumPermType.SecurityAdmin || permType==EnumPermType.StartupMultiUserOld || permType==EnumPermType.StartupSingleUserOld
							|| permType==EnumPermType.EhrKeyAdd || GroupPermissions.IsReserved(permType))
						{
							continue;
						}
						//Unticked in the tree (only that one permission goes; its sub-items stay).
						if(permType==EnumPermType.ProblemDefEdit || (arrayGroup[0]==GROUP_NOSETUP && permType==EnumPermType.Setup)) {
							continue;
						}
						if(GroupPermissions.DoesPermissionTreatZeroFKeyAsAll(permType)) {
							GroupPermissions.GiveUserGroupPermissionAll(userGroupNum,permType);
							continue;
						}
						GroupPermissions.Insert(new GroupPermission { PermType=permType,UserGroupNum=userGroupNum });
					}
					Userod userod=new Userod { UserName=arrayGroup[1] };
					if(arrayGroup[1]==USER_NOSETUP) {
						userod.ProvNum=PIn.Long(DataCore.GetScalar("SELECT PriProv FROM patient WHERE PatNum=1"));
					}
					Userods.Insert(userod,new List<long> { userGroupNum });
				}
				DataValid.SetInvalid(InvalidType.Security);
			});
			foreach(string[] arrayUser in new[] { new[] { USER_SETUP,"1","0" },new[] { USER_NOSETUP,"0","0" } }) {
				long userNum=UserNum(arrayUser[0]);
				bool hasSetup=ui.Invoke(() => GroupPermissions.HasPermission(Userods.GetUser(userNum),EnumPermType.Setup,0));
				bool hasProblemDefEdit=ui.Invoke(() => GroupPermissions.HasPermission(Userods.GetUser(userNum),EnumPermType.ProblemDefEdit,0));
				checkResult.Expect(userNum>0 && hasSetup==(arrayUser[1]=="1") && hasProblemDefEdit==(arrayUser[2]=="1"),"0.4: "+arrayUser[0]+" (UserNum "+userNum+") has "
					+(arrayUser[1]=="1" ? "Setup" : "not Setup")+" and not Problem Definition Edit","Setup "+hasSetup+", ProblemDefEdit "+hasProblemDefEdit);
			}
			Provider provider=ui.Invoke(() => ProviderOfPatient1());
			checkResult.Note("0.4: groups and users made by the business layer in the scratch copy as Set All and the tree make them (Setup > Security's "
				+"own rules); gate-nosetup's provider is patient #1's ("+provider.Abbr+", '"+provider.LName+", "+provider.FName+"'). 0.4 #6 is not needed: "
				+"the copy's provider names are not blank, so the EHR row uses them as they are");
		}
		#endregion 0.4 test users

		public static void CheckJ4(Ui ui,CheckResult checkResult) {
			if(!LogOffAndOn(ui,checkResult,USER_NOSETUP)) {
				return;
			}
			CheckEhrWindowLabels(ui,checkResult,isNoteExpected:true,shot:"J4.png");
		}

		public static void CheckJ5(Ui ui,CheckResult checkResult) {
			checkResult.Expect(CurUserName(ui)==USER_NOSETUP,"Logged on as "+USER_NOSETUP);
			IntPtr hwnd=OpenEhrSetup(ui,checkResult);
			if(hwnd==IntPtr.Zero) {
				return;
			}
			Dictionary<string,bool> dictVisible=ui.Invoke(() => Ui.AllControls(Control.FromHandle(hwnd)).Where(x => x.Name.StartsWith("but"))
				.GroupBy(x => x.Name).ToDictionary(x => x.Key,x => x.First().Visible));
			checkResult.Note("Buttons shown: "+string.Join(", ",dictVisible.Where(x => x.Value).Select(x => x.Key))+"; hidden: "
				+string.Join(", ",dictVisible.Where(x => !x.Value).Select(x => x.Key)));
			checkResult.Expect(dictVisible.ContainsKey("butProviderKeys") && !dictVisible["butProviderKeys"],"No Provider Keys");
			checkResult.Expect(dictVisible.ContainsKey("butCodeImport") && !dictVisible["butCodeImport"],"No Code System Importer");
			foreach(string name in new[] { "butAllergies","butVaccineDef","butDrugManufacturer","butDrugUnit","butInboundEmail","butReminderRules",
				"butEducationalResources","butTimeSynch","butEhrTriggers" })
			{
				checkResult.Expect(dictVisible.ContainsKey(name) && !dictVisible[name],"No other Setup-only button: "+name+" hidden");
			}
			foreach(string name in new[] { "butRxNorm","butLoincs","butSnomeds","butEmergencyNow" }) {
				checkResult.Expect(dictVisible.ContainsKey(name) && dictVisible[name],name+" is there (the Code Systems buttons and Emergency Now)");
			}
			//Emergency Now: red, then normal again.
			System.Drawing.Color colorBefore=ui.Invoke(() => FindControl(ui,hwnd,"panelEmergencyNow",true).BackColor);
			ui.Post("Emergency Now",() => Ui.Call(Control.FromHandle(hwnd),"butEmergencyNow_Click",null,EventArgs.Empty)).Wait();
			System.Drawing.Color colorOn=ui.Invoke(() => FindControl(ui,hwnd,"panelEmergencyNow",true).BackColor);
			checkResult.Expect(colorOn.ToArgb()==System.Drawing.Color.Red.ToArgb(),"Emergency Now: its panel turns red ("+colorOn.Name+")");
			checkResult.Expect(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='EhrEmergencyNow'")=="1","EhrEmergencyNow is 1");
			checkResult.AddShot(ui.Shot(hwnd,"J5.png"),"EHR setup without Setup, Emergency Now on");
			ui.Post("Emergency Now again",() => Ui.Call(Control.FromHandle(hwnd),"butEmergencyNow_Click",null,EventArgs.Empty)).Wait();
			System.Drawing.Color colorOff=ui.Invoke(() => FindControl(ui,hwnd,"panelEmergencyNow",true).BackColor);
			checkResult.Expect(colorOff.ToArgb()==colorBefore.ToArgb(),"Again: back to normal ("+colorOff.Name+")");
			checkResult.Expect(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='EhrEmergencyNow'")=="0","EhrEmergencyNow is 0 again");
			checkResult.Expect(ui.Close(hwnd),"EHR setup closes");
		}

		public static void CheckJ6(Ui ui,CheckResult checkResult) {
			checkResult.Expect(CurUserName(ui)==USER_NOSETUP,"Logged on as "+USER_NOSETUP);
			bool? hasTools=CheckSnomedTools(ui,checkResult,"J6.png",keepOpen:false,out _,out _);
			checkResult.Expect(Count("SELECT COUNT(*) FROM snomed")>0,"A SNOMED row exists (J3's synthetic TEST0001)");
			checkResult.Expect(hasTools==false,"No One-Time Tools group without Setup although a row exists");
		}

		public static void CheckJ7(Ui ui,CheckResult checkResult) {
			if(!LogOffAndOn(ui,checkResult,USER_SETUP)) {
				return;
			}
			Provider provider=ui.Invoke(() => ProviderOfPatient1());
			int countBefore=Count("SELECT COUNT(*) FROM ehrprovkey");
			IntPtr hwndSetup=OpenEhrSetup(ui,checkResult);
			if(hwndSetup==IntPtr.Zero) {
				return;
			}
			Ui.Opened openedKeys=OpenByButton(ui,hwndSetup,"butProviderKeys");
			if(InspectWindow(ui,checkResult,openedKeys,"FormEhrProviderKeys",null,close:false)) {
				Ui.Opened openedEdit=OpenByButton(ui,openedKeys.Hwnd,"butAdd");
				if(InspectWindow(ui,checkResult,openedEdit,"FormEhrProviderKeyEdit",null,close:false)) {
					IntPtr hwndEdit=openedEdit.Hwnd;
					string noteKey=ui.Invoke(() => Ui.AllControls(Control.FromHandle(hwndEdit)).Select(x => x.Text).FirstOrDefault(x => x.Contains("optional"))??"");
					checkResult.Expect(noteKey.Contains("optional, not checked"),"The key box says 'optional, not checked' ('"+noteKey+"')");
					ui.Invoke(() => {
						FindControl(ui,hwndEdit,"textLName",true).Text=provider.LName;
						FindControl(ui,hwndEdit,"textFName",true).Text=provider.FName;
						FindControl(ui,hwndEdit,"textYear",true).Text="26";
						FindControl(ui,hwndEdit,"textKey",true).Text="";
					});
					checkResult.Note("Last Name '"+provider.LName+"', First Name '"+provider.FName+"' (patient #1's provider "+provider.Abbr+"; the checklist's "
						+"'Dentist, Gate' assumed a new database's names), Year 26, EHR Key empty");
					Ui.Opened openedMsg=PressMaybeMessage(ui,hwndEdit,"butSave");
					if(openedMsg.IsOpen) {
						checkResult.Fail("Save showed: "+ui.DescribeWindow(openedMsg.Hwnd));
						ui.Close(openedMsg.Hwnd);
					}
					checkResult.Expect(ui.WaitGone(hwndEdit),"Save closes the editor");
					openedEdit.Posted.Wait();
				}
				System.Threading.Thread.Sleep(800);
				List<string> listRows=ui.Invoke(() => GridRows(FindControl(ui,openedKeys.Hwnd,"gridMain",true)));
				checkResult.Expect(listRows.Any(x => x.Contains(provider.LName) && x.Contains(provider.FName)),"The row is listed ("+string.Join("; ",listRows)+")");
				checkResult.AddShot(ui.Shot(openedKeys.Hwnd,"J7.png"),"Provider Keys with the row");
				ui.Close(openedKeys.Hwnd);
			}
			ui.Close(hwndSetup);
			checkResult.Expect(Count("SELECT COUNT(*) FROM ehrprovkey")==countBefore+1,"One ehrprovkey row added ("+countBefore+" before; J12 deletes it)");
		}

		public static void CheckJ8(Ui ui,CheckResult checkResult) {
			checkResult.Expect(CurUserName(ui)==USER_SETUP,"Logged on as "+USER_SETUP);
			int countBefore=Count("SELECT COUNT(*) FROM ehrprovkey");
			IntPtr hwndSetup=OpenEhrSetup(ui,checkResult);
			if(hwndSetup==IntPtr.Zero) {
				return;
			}
			Ui.Opened openedKeys=OpenByButton(ui,hwndSetup,"butProviderKeys");
			if(InspectWindow(ui,checkResult,openedKeys,"FormEhrProviderKeys",null,close:false)) {
				Ui.Opened openedEdit=OpenByButton(ui,openedKeys.Hwnd,"butAdd");
				if(InspectWindow(ui,checkResult,openedEdit,"FormEhrProviderKeyEdit",null,close:false)) {
					IntPtr hwndEdit=openedEdit.Hwnd;
					ui.Invoke(() => {
						FindControl(ui,hwndEdit,"textLName",true).Text="Nobody";
						FindControl(ui,hwndEdit,"textFName",true).Text="Gate";
						FindControl(ui,hwndEdit,"textYear",true).Text="26";
					});
					Ui.Opened openedMsg=OpenByButton(ui,hwndEdit,"butSave",allowMessage:true);
					if(openedMsg.IsOpen) {
						checkResult.AddShot(ui.Shot(openedMsg.Hwnd,"J8.png"),"The refusal");
					}
					string text=ReadAndCloseMessage(ui,openedMsg);
					string expected="No provider has the last name 'Nobody' and the first name 'Gate'. Enter the names exactly as they are in the provider's record, "
						+"or add the provider first.";
					checkResult.Expect(Clean2(text)==Clean2(expected),"Save: '"+expected+"'",text);
					checkResult.Expect(Native.IsWindowVisible(hwndEdit),"The editor stays open");
					checkResult.Expect(ui.Close(hwndEdit),"Cancel closes the editor");
					openedEdit.Posted.Wait();
				}
				ui.Close(openedKeys.Hwnd);
			}
			ui.Close(hwndSetup);
			checkResult.Expect(Count("SELECT COUNT(*) FROM ehrprovkey")==countBefore,"Nothing added ("+countBefore+" rows)");
		}

		public static void CheckJ9(Ui ui,CheckResult checkResult) {
			checkResult.Expect(CurUserName(ui)==USER_SETUP,"Logged on as "+USER_SETUP);
			string defsBefore=DataCore.GetScalar("SELECT GROUP_CONCAT(CONCAT(DiseaseDefNum,':',SnomedCode)) FROM diseasedef");
			int countLogBefore=Count("SELECT COUNT(*) FROM securitylog WHERE PermType="+(int)EnumPermType.ProblemDefEdit);
			bool? hasTools=CheckSnomedTools(ui,checkResult,null,keepOpen:true,out IntPtr hwndSnomed,out IntPtr hwndSetup);
			if(hasTools==null) {
				return;
			}
			checkResult.Expect(hasTools==true,"The One-Time Tools group shows (Setup, and a row exists)");
			checkResult.AddShot(ui.Shot(hwndSnomed,"J9.png"),"SNOMED CTs with the One-Time Tools");
			Ui.Opened openedMsg=OpenByButton(ui,hwndSnomed,"butMapICD9",allowMessage:true);
			string text=openedMsg.IsOpen && Native.IsDialogClass(openedMsg.Hwnd) ? Native.GetDialogMessage(openedMsg.Hwnd) : "";
			if(openedMsg.IsOpen) {
				checkResult.AddShot(ui.Shot(openedMsg.Hwnd,"J9-not-authorized.png"),"Map to ICD9's message");
			}
			ReadAndCloseMessage(ui,openedMsg);
			checkResult.Expect(text.Contains("Not authorized") && text.Contains(GroupPermissions.GetDesc(EnumPermType.ProblemDefEdit)),
				"Map to ICD9: the standard 'Not authorized' message for Problem Definition Edit ('"+Clean2(text)+"')",text);
			checkResult.Expect(!text.Contains("Will add SNOMED CT code"),"Before any confirmation");
			System.Threading.Thread.Sleep(800);
			List<IntPtr> listMore=ui.GetWindows().Where(x => Native.IsDialogClass(x)).ToList();
			checkResult.Expect(listMore.Count==0,"No confirmation follows",string.Join("; ",listMore.Select(x => ui.DescribeWindow(x))));
			listMore.ForEach(x => ui.Close(x));
			openedMsg.Posted.Wait();
			ui.Close(hwndSnomed);
			ui.Close(hwndSetup);
			checkResult.Expect(DataCore.GetScalar("SELECT GROUP_CONCAT(CONCAT(DiseaseDefNum,':',SnomedCode)) FROM diseasedef")==defsBefore
				&& Count("SELECT COUNT(*) FROM securitylog WHERE PermType="+(int)EnumPermType.ProblemDefEdit)==countLogBefore,"Nothing changes (problem definitions, no "
				+"Problem Definition Edit log entry)");
		}

		public static void CheckJ10(Ui ui,CheckResult checkResult) {
			if(!LogOffAndOn(ui,checkResult,USER_NOSETUP)) {
				return;
			}
			CheckEhrWindowLabels(ui,checkResult,isNoteExpected:false,shot:"J10.png");
		}

		public static void CheckJ11(Ui ui,CheckResult checkResult) {
			checkResult.Expect(CurUserName(ui)==USER_NOSETUP,"Logged on as "+USER_NOSETUP);
			GoToModule(ui,EnumModuleType.Chart);
			Control controlChart=ModuleControl("controlChart");
			long procNumBefore=PIn.Long(DataCore.GetScalar("SELECT MAX(ProcNum) FROM procedurelog WHERE PatNum=1"));
			Ui.Posted posted=ui.Post("Enter Treatment > TreatPlan > Type Proc Code D0120 > OK",() => {
				((RadioButton)Ui.GetField(controlChart,"radioEntryTP")).Checked=true;
				((Control)Ui.GetField(controlChart,"textProcCode")).Text="D0120";
				Ui.Call(controlChart,"butOK_Click",controlChart,EventArgs.Empty);
			});
			System.Threading.Thread.Sleep(2500);
			List<IntPtr> listWindows=ui.GetWindows();
			foreach(IntPtr hwnd in listWindows) {
				checkResult.Fail("A window opened: "+ui.DescribeWindow(hwnd));
				ui.Close(hwnd);
			}
			posted.Wait();
			string row=DataCore.GetScalar("SELECT CONCAT(ProcNum,'|',ProcStatus,'|',IsCpoe) FROM procedurelog WHERE PatNum=1 ORDER BY ProcNum DESC LIMIT 1");
			checkResult.Note("SELECT ProcNum,ProcStatus,IsCpoe FROM procedurelog WHERE PatNum=1 ORDER BY ProcNum DESC LIMIT 1: "+row);
			string[] arrayRow=row.Split('|');
			long procNum=PIn.Long(arrayRow[0]);
			if(checkResult.Expect(procNum>procNumBefore,"The D0120 is added (ProcNum "+procNum+")")) {
				checkResult.Expect(arrayRow.Length==3 && arrayRow[2]=="1","IsCpoe 1 (the user's provider has an EHR row)",row);
				checkResult.AddShot(ui.Shot(ui.Invoke(() => FormMain.Handle),"J11.png"),"The Chart with the D0120");
				ui.Invoke(() => Procedures.Delete(procNum));
				checkResult.Expect(Count("SELECT COUNT(*) FROM procedurelog WHERE ProcNum="+procNum+" AND ProcStatus=6")==1,"Undo: that D0120 deleted (the row stays with "
					+"status deleted)");
			}
		}

		public static void CheckJ12(Ui ui,CheckResult checkResult) {
			if(!LogOffAndOn(ui,checkResult,USER_SETUP)) {
				return;
			}
			Provider provider=ui.Invoke(() => ProviderOfPatient1());
			string names="'"+provider.LName+", "+provider.FName+"'";
			long userNum=UserNum(USER_SETUP);
			IntPtr hwndSetup=OpenEhrSetup(ui,checkResult);
			if(hwndSetup==IntPtr.Zero) {
				return;
			}
			Ui.Opened openedKeys=OpenByButton(ui,hwndSetup,"butProviderKeys");
			if(InspectWindow(ui,checkResult,openedKeys,"FormEhrProviderKeys",null,close:false)) {
				IntPtr hwndKeys=openedKeys.Hwnd;
				//The row > Year 25 > Save.
				Ui.Opened openedEdit=ui.Open("Provider Keys > double-click the row",() => DoubleClickRowOnThread(FindControl(ui,hwndKeys,"gridMain",true),0));
				if(InspectWindow(ui,checkResult,openedEdit,"FormEhrProviderKeyEdit",null,close:false)) {
					ui.Invoke(() => FindControl(ui,openedEdit.Hwnd,"textYear",true).Text="25");
					Ui.Opened openedMsg=PressMaybeMessage(ui,openedEdit.Hwnd,"butSave");
					if(openedMsg.IsOpen) {
						checkResult.Fail("Save showed: "+ui.DescribeWindow(openedMsg.Hwnd));
						ui.Close(openedMsg.Hwnd);
					}
					checkResult.Expect(ui.WaitGone(openedEdit.Hwnd),"Year 25 > Save closes the editor");
					openedEdit.Posted.Wait();
				}
				checkResult.Expect(DataCore.GetScalar("SELECT YearValue FROM ehrprovkey")=="25","The row's year is 25");
				//Open it again > Delete, confirm.
				Ui.Opened openedEdit2=ui.Open("Provider Keys > double-click the row again",() => DoubleClickRowOnThread(FindControl(ui,hwndKeys,"gridMain",true),0));
				if(InspectWindow(ui,checkResult,openedEdit2,"FormEhrProviderKeyEdit",null,close:false)) {
					Ui.Opened openedConfirm=OpenByButton(ui,openedEdit2.Hwnd,"butDelete",allowMessage:true);
					string text=openedConfirm.IsOpen && Native.IsDialogClass(openedConfirm.Hwnd) ? Native.GetDialogMessage(openedConfirm.Hwnd) : "";
					checkResult.Expect(text.Contains("Delete?"),"Delete asks 'Delete?' ('"+text.Trim()+"'); confirmed with OK");
					ReadAndCloseMessage(ui,openedConfirm,"OK");
					checkResult.Expect(ui.WaitGone(openedEdit2.Hwnd),"The editor closes after the delete");
					openedEdit2.Posted.Wait();
				}
				ui.Close(hwndKeys);
			}
			ui.Close(hwndSetup);
			checkResult.Expect(Count("SELECT COUNT(*) FROM ehrprovkey")==0,"The row is deleted (undoes J7)");
			//The Setup entries of gate-setup (Tools > Audit Trail, permission Setup).
			System.Data.DataTable table=DataCore.GetTable("SELECT LogText,UserNum FROM securitylog WHERE PermType="+(int)EnumPermType.Setup+" AND UserNum="+userNum
				+" AND LogText LIKE 'EHR reporting row%' ORDER BY SecurityLogNum");
			List<string> listTexts=table.Rows.Cast<System.Data.DataRow>().Select(x => x["LogText"].ToString()).ToList();
			checkResult.Note("Setup entries of "+USER_SETUP+": "+string.Join(" || ",listTexts));
			checkResult.Expect(listTexts.Any(x => x.StartsWith("EHR reporting row added: "+names+", year 26."))
				,"'EHR reporting row added: "+names+", year 26.'");
			checkResult.Expect(listTexts.Any(x => x.StartsWith("EHR reporting row added") && x.Contains("The radiology procedures alert (IsAlertRadiologyProcsEnabled) was turned on."))
				,"... followed by 'The radiology procedures alert (IsAlertRadiologyProcsEnabled) was turned on.' (the table was empty at 0.3)");
			checkResult.Expect(listTexts.Any(x => x.StartsWith("EHR reporting row changed from "+names+", year 26 to "+names+", year 25.")),
				"'EHR reporting row changed from "+names+", year 26 to "+names+", year 25.'");
			checkResult.Expect(listTexts.Any(x => x.StartsWith("EHR reporting row deleted:") && x.Contains("was turned off.")),
				"One starting 'EHR reporting row deleted:' (with '... was turned off.')");
			checkResult.Expect(listTexts.Count==3,"Three entries, all with user "+USER_SETUP+" (UserNum "+userNum+"); no key text (the key was empty)");
			Ui.Opened openedAudit=OpenMenu(ui,"Tools","Audit Trail");
			if(InspectWindow(ui,checkResult,openedAudit,"FormAudit",null,close:false)) {
				System.Threading.Thread.Sleep(1000);
				checkResult.AddShot(ui.Shot(openedAudit.Hwnd,"J12.png"),"Tools > Audit Trail");
				ui.Close(openedAudit.Hwnd);
			}
		}

		public static void CheckJ13(Ui ui,CheckResult checkResult) {
			if(!LogOffAndOn(ui,checkResult,"Admin")) {
				return;
			}
			int countProblems=Count("SELECT COUNT(*) FROM diseasedef");
			bool? hasTools=CheckSnomedTools(ui,checkResult,null,keepOpen:true,out IntPtr hwndSnomed,out IntPtr hwndSetup);
			if(hasTools==null) {
				return;
			}
			checkResult.Expect(hasTools==true,"Admin: the One-Time Tools group shows");
			Ui.Opened openedConfirm=OpenByButton(ui,hwndSnomed,"butMapICD9",allowMessage:true);
			string textConfirm=openedConfirm.IsOpen && Native.IsDialogClass(openedConfirm.Hwnd) ? Native.GetDialogMessage(openedConfirm.Hwnd) : "";
			checkResult.Expect(textConfirm.Contains("Will add SNOMED CT code"),"Map to ICD9 asks for confirmation; OK","'"+textConfirm+"'");
			List<IntPtr> listBefore=ui.GetWindows();
			if(openedConfirm.IsOpen) {
				Native.ClickDialogButton(openedConfirm.Hwnd,"OK");
			}
			IntPtr hwndCount=ui.WaitForNewWindow(listBefore.Where(x => x!=openedConfirm.Hwnd).ToList(),20000);
			string textCount=hwndCount!=IntPtr.Zero && Native.IsDialogClass(hwndCount) ? Native.GetDialogMessage(hwndCount) : "";
			if(hwndCount!=IntPtr.Zero) {
				checkResult.AddShot(ui.Shot(hwndCount,"J13.png"),"Map to ICD9's count");
				ui.Close(hwndCount);
			}
			openedConfirm.Posted.Wait();
			checkResult.Expect(textCount.Contains("SNOMED CT codes added: "),"A message with the count ('"+textCount.Trim()+"'; "+countProblems+" problem definitions "
				+"in the copy)");
			string expectedLog="Map to ICD9 (SNOMED CT window): SNOMED CT codes added to "+(countProblems==0 ? "0 " : "");
			string log=DataCore.GetScalar("SELECT LogText FROM securitylog WHERE PermType="+(int)EnumPermType.ProblemDefEdit+" ORDER BY SecurityLogNum DESC LIMIT 1");
			checkResult.Expect(log.StartsWith(expectedLog) && log.EndsWith("problem definitions from their ICD-9 codes."),"Audit Trail, Problem Definition Edit: '"+log+"'",log);
			ui.Close(hwndSnomed);
			ui.Close(hwndSetup);
			//Undo J3's row.
			DataCore.NonQ("DELETE FROM snomed WHERE SnomedCode='TEST0001'");
			checkResult.Expect(Count("SELECT COUNT(*) FROM snomed")==0,"Undo: the synthetic SNOMED row TEST0001 is deleted");
		}

		#region Setup Wizard (J2, J17)
		///<summary>Tools > Setup Wizard > the Basic Features row (double-click) > Next (from its introduction): the progress window and the Basic
		///Features control, or zeros.</summary>
		private static IntPtr OpenWizardFeatures(Ui ui,CheckResult checkResult,out IntPtr hwndWizard,out Control controlFeatures) {
			controlFeatures=null;
			hwndWizard=IntPtr.Zero;
			Ui.Opened openedWizard=OpenMenu(ui,"Tools","Setup Wizard");
			if(!InspectWindow(ui,checkResult,openedWizard,"FormSetupWizard",null,close:false)) {
				return IntPtr.Zero;
			}
			hwndWizard=openedWizard.Hwnd;
			IntPtr hwndWizardLocal=hwndWizard;
			Ui.Opened openedProgress=ui.Open("Basic Features (double-click)",() => {
				Control grid=FindControl(ui,hwndWizardLocal,"gridMain",true);
				int row=FindRow(grid,x => x.Contains("Basic Features"));
				if(row<0) {
					throw new Exception("No Basic Features row: "+string.Join("; ",GridRows(grid)));
				}
				DoubleClickRowOnThread(grid,row);
			});
			if(!InspectWindow(ui,checkResult,openedProgress,"FormSetupWizardProgress",null,close:false)) {
				return IntPtr.Zero;
			}
			IntPtr hwnd=openedProgress.Hwnd;
			ui.Post("Next",() => Ui.Call(Control.FromHandle(hwnd),"butNext_Click",null,EventArgs.Empty)).Wait();
			controlFeatures=ui.Invoke(() => Ui.AllControls(Control.FromHandle(hwnd)).FirstOrDefault(x => x.GetType().Name=="UserControlSetupWizFeatures"));
			string title=ui.Invoke(() => FindControl(ui,hwnd,"labelTitle",true)?.Text??"");
			checkResult.Expect(controlFeatures!=null && title.Contains("Basic Features"),"Setup Wizard > Basic Features ('"+title+"')");
			return hwnd;
		}

		///<summary>Clicks the wizard's EHR box as a person does (its Checked flips, then its Click handler runs) and returns the message box it shows,
		///without waiting for the handler.</summary>
		private static Ui.Opened ClickWizardEhr(Ui ui,Control controlFeatures,string description) {
			return ui.Open(description,() => {
				OpenPmsDental.UI.CheckBox checkEhr=(OpenPmsDental.UI.CheckBox)Ui.GetField(controlFeatures,"checkEhr");
				checkEhr.Checked=!checkEhr.Checked;
				Ui.Call(controlFeatures,"checkEhr_Click",checkEhr,EventArgs.Empty);
			},allowMessage:true);
		}

		private static bool WizardEhrChecked(Ui ui,Control controlFeatures) {
			return ui.Invoke(() => ((OpenPmsDental.UI.CheckBox)Ui.GetField(controlFeatures,"checkEhr")).Checked);
		}

		private const string TEXT_RESTART="You will need to restart the program for the change to take effect.";

		public static void CheckJ2(Ui ui,CheckResult checkResult) {
			IntPtr hwnd=OpenWizardFeatures(ui,checkResult,out IntPtr hwndWizard,out Control controlFeatures);
			if(hwnd==IntPtr.Zero || controlFeatures==null) {
				ui.CloseStrayWindows(checkResult);
				return;
			}
			checkResult.Expect(WizardEhrChecked(ui,controlFeatures),"EHR is ticked (J1 turned it on)");
			//The EHR information text (its info icon).
			string info=ui.Invoke(() => {
				Control labelInfoEhr=(Control)Ui.GetField(controlFeatures,"labelInfoEHR");
				Ui.Call(controlFeatures,"labelInfo_MouseClick",labelInfoEhr,new MouseEventArgs(MouseButtons.Left,1,5,5,0));
				return ((Control)Ui.GetField(controlFeatures,"labelExplanation")).Text;
			});
			checkResult.Note("EHR information: "+Clean2(info));
			checkResult.Expect(info.Contains("Shows the EHR tools") && info.Contains("It changes how prescriptions, patient race, required fields and the Chart work"),
				"The information says what turning EHR on shows and changes",info);
			checkResult.Expect(info.Contains("This program is not certified EHR technology.") && info.Contains("for internal review only"),
				"... that the program is not certified, its measures for internal review only",info);
			checkResult.Expect(!info.ToLower().Contains("contact") && !info.Contains("Open Dental"),"No contact-Open-Dental text",info);
			checkResult.Expect(!System.Text.RegularExpressions.Regex.IsMatch(info,"(?<!not )certified (EHR|by|for|as)") || info.Contains("not certified EHR technology"),
				"No certification claim",info);
			checkResult.AddShot(ui.Shot(hwnd,"J2-info.png"),"Basic Features with the EHR information");
			//Untick: the restart message.
			Ui.Opened openedUntick=ClickWizardEhr(ui,controlFeatures,"Untick EHR");
			string textUntick=ReadAndCloseMessage(ui,openedUntick);
			openedUntick.Posted.Wait();
			checkResult.Expect(textUntick.Contains(TEXT_RESTART),"Untick: the restart message",textUntick);
			checkResult.Expect(!WizardEhrChecked(ui,controlFeatures),"EHR unticked");
			//Tick: the J1 confirmation; Cancel: unchecked.
			Ui.Opened openedTick=ClickWizardEhr(ui,controlFeatures,"Tick EHR");
			string textConfirm=openedTick.IsOpen ? Native.GetDialogMessage(openedTick.Hwnd) : "";
			checkResult.Expect(Clean2(textConfirm)==Clean2(FormShowFeatures.GetEhrOnConfirmationText()),"Tick: the J1 confirmation",textConfirm);
			if(openedTick.IsOpen) {
				checkResult.AddShot(ui.Shot(openedTick.Hwnd,"J2-confirm.png"),"The confirmation");
			}
			List<IntPtr> listBeforeCancel=ui.GetWindows();
			ReadAndCloseMessage(ui,openedTick,"Cancel");
			System.Threading.Thread.Sleep(800);
			List<IntPtr> listAfterCancel=ui.GetWindows().Where(x => !listBeforeCancel.Contains(x)).ToList();
			checkResult.Expect(listAfterCancel.Count==0,"Cancel: no restart message",string.Join("; ",listAfterCancel.Select(x => ui.DescribeWindow(x))));
			listAfterCancel.ForEach(x => ui.Close(x));
			openedTick.Posted.Wait();
			checkResult.Expect(!WizardEhrChecked(ui,controlFeatures),"Cancel: unchecked");
			//Tick > OK: the restart message.
			Ui.Opened openedTick2=ClickWizardEhr(ui,controlFeatures,"Tick EHR again");
			List<IntPtr> listBeforeOk=ui.GetWindows();
			if(openedTick2.IsOpen) {
				Native.ClickDialogButton(openedTick2.Hwnd,"OK");
			}
			IntPtr hwndRestart=ui.WaitForNewWindow(listBeforeOk.Where(x => x!=openedTick2.Hwnd).ToList(),15000);
			string textRestart=hwndRestart!=IntPtr.Zero && Native.IsDialogClass(hwndRestart) ? Native.GetDialogMessage(hwndRestart) : "";
			if(hwndRestart!=IntPtr.Zero) {
				ui.Close(hwndRestart);
			}
			openedTick2.Posted.Wait();
			checkResult.Expect(textRestart.Contains(TEXT_RESTART),"Tick > OK: the restart message",textRestart);
			checkResult.Expect(WizardEhrChecked(ui,controlFeatures),"EHR ticked");
			//Close the wizard with its X (it saves a page only on Next).
			checkResult.Expect(ui.Close(hwnd),"The wizard's page window closes with its X");
			if(hwndWizard!=IntPtr.Zero) {
				ui.Close(hwndWizard);
			}
			checkResult.Expect(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='ShowFeatureEhr'")=="1","ShowFeatureEhr is still 1 (nothing saved)");
			Ui.Opened openedFeatures=OpenMenu(ui,"Setup","Advanced Setup","Show Features");
			if(InspectWindow(ui,checkResult,openedFeatures,"FormShowFeatures",null,close:false)) {
				bool isTicked=ui.Invoke(() => ((OpenPmsDental.UI.CheckBox)FindControl(ui,openedFeatures.Hwnd,"checkEhr",true)).Checked);
				checkResult.Expect(isTicked,"Show Features still has EHR ticked");
				ui.Close(openedFeatures.Hwnd);
			}
		}

		public static void CheckJ17(Ui ui,CheckResult checkResult) {
			string prefsBefore=DataCore.GetScalar("SELECT GROUP_CONCAT(CONCAT(PrefName,'=',ValueString) ORDER BY PrefName) FROM preference WHERE PrefName IN "
				+"('EasyHideCapitation','EasyHideMedicaid','EasyHideInsurance','EasyHideClinical','EasyNoClinics','ShowFeatureMedicalInsurance')");
			IntPtr hwnd=OpenWizardFeatures(ui,checkResult,out IntPtr hwndWizard,out Control controlFeatures);
			if(hwnd==IntPtr.Zero || controlFeatures==null) {
				ui.CloseStrayWindows(checkResult);
				return;
			}
			checkResult.Expect(!WizardEhrChecked(ui,controlFeatures),"EHR is unticked (J16 turned it off)");
			Ui.Opened openedTick=ClickWizardEhr(ui,controlFeatures,"Tick EHR");
			string textConfirm=openedTick.IsOpen ? Native.GetDialogMessage(openedTick.Hwnd) : "";
			checkResult.Expect(Clean2(textConfirm)==Clean2(FormShowFeatures.GetEhrOnConfirmationText()),"Tick: the J1 confirmation",textConfirm);
			List<IntPtr> listBeforeOk=ui.GetWindows();
			if(openedTick.IsOpen) {
				Native.ClickDialogButton(openedTick.Hwnd,"OK");
			}
			IntPtr hwndRestart=ui.WaitForNewWindow(listBeforeOk.Where(x => x!=openedTick.Hwnd).ToList(),15000);
			string textRestart=hwndRestart!=IntPtr.Zero && Native.IsDialogClass(hwndRestart) ? Native.GetDialogMessage(hwndRestart) : "";
			if(hwndRestart!=IntPtr.Zero) {
				ui.Close(hwndRestart);
			}
			openedTick.Posted.Wait();
			checkResult.Expect(textRestart.Contains(TEXT_RESTART),"OK: the restart message",textRestart);
			checkResult.AddShot(ui.Shot(hwnd,"J17-wizard.png"),"Basic Features with EHR ticked");
			//Next: the page is saved.
			List<IntPtr> listBeforeNext=ui.GetWindows();
			ui.Post("Next",() => Ui.Call(Control.FromHandle(hwnd),"butNext_Click",null,EventArgs.Empty)).Wait(20000);
			foreach(IntPtr hwndNew in ui.GetWindows().Where(x => !listBeforeNext.Contains(x))) {
				checkResult.Note("Next showed: "+ui.DescribeWindow(hwndNew));
				ui.Close(hwndNew);
			}
			string ehr=DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='ShowFeatureEhr'");
			string prefsAfter=DataCore.GetScalar("SELECT GROUP_CONCAT(CONCAT(PrefName,'=',ValueString) ORDER BY PrefName) FROM preference WHERE PrefName IN "
				+"('EasyHideCapitation','EasyHideMedicaid','EasyHideInsurance','EasyHideClinical','EasyNoClinics','ShowFeatureMedicalInsurance')");
			checkResult.Expect(ehr=="1","Next: ShowFeatureEhr 1");
			checkResult.Expect(prefsAfter==prefsBefore,"The other six as at 0.3 ("+prefsAfter+")",prefsBefore+" -> "+prefsAfter);
			if(Native.IsWindow(hwnd)) {
				checkResult.Expect(ui.Close(hwnd),"The wizard closes with its X");
			}
			if(hwndWizard!=IntPtr.Zero && Native.IsWindow(hwndWizard)) {
				ui.Close(hwndWizard);
			}
		}

		///<summary>J16 after the restart: Show Features (EHR unticked) printed as J16.png and compared with J1-before.png; closed with its X (nothing
		///saved).</summary>
		public static void CheckJ16SF(Ui ui,CheckResult checkResult) {
			Ui.Opened opened=OpenMenu(ui,"Setup","Advanced Setup","Show Features");
			if(!InspectWindow(ui,checkResult,opened,"FormShowFeatures",null,close:false)) {
				return;
			}
			bool isTicked=ui.Invoke(() => ((OpenPmsDental.UI.CheckBox)FindControl(ui,opened.Hwnd,"checkEhr",true)).Checked);
			checkResult.Expect(!isTicked,"Show Features: EHR unticked");
			checkResult.AddShot(ui.Shot(opened.Hwnd,"J16.png"),"Show Features after J16");
			double share=WindowChecksTests.Shot.CompareFiles("J1-before.png","J16.png",out string text);
			checkResult.Expect(share>=0 && share<0.005,"Show Features matches J1-before.png ("+text+")",text);
			checkResult.Expect(ui.Close(opened.Hwnd),"Show Features closes with its X");
		}

		///<summary>J17's undo: Show Features > untick EHR > Save (as J16).</summary>
		public static void CheckJ17OFF(Ui ui,CheckResult checkResult) {
			ShowFeaturesEhr(ui,checkResult,on:false,shotBefore:null,shotConfirm:null);
		}
		#endregion Setup Wizard (J2, J17)
	}
}
